using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>
/// 应用业务流程控制器：承载原 WinForms 主窗体的全部状态与流程，
/// 不依赖任何 UI 控件；通过事件通知界面（WebView2 Bridge 订阅后推送快照）。
/// 原生对话框（目录/文件选择、警告确认）仍使用 WinForms 对话框。
/// </summary>
public class AppController
{
    private readonly AppSettings _settings = AppSettings.Load();
    private List<Mo2Instance> _instances = new();
    private Mo2Instance? _instance;
    private List<string> _profiles = new();
    private string? _profile;
    private List<ModEntry> _entries = new();
    private List<(ModEntry Entry, string Dir)> _mods = new();
    private List<BodySlideCandidate> _bsCandidates = new();
    private string? _bsAppDir;
    private ProjectPathResolution? _resolution;
    private ScanResult? _scan;
    private HashSet<string>? _conflictNames;
    private readonly List<SliderGroup> _groups = new();
    private string? _currentGroupName;
    private bool _dirty;
    private bool _scanning;
    private bool _scanQueued;

    private const string DedicatedModName = "BS Group Generator";

    /// <summary>原生对话框的宿主窗口。</summary>
    public IWin32Window? DialogOwner { get; set; }

    /// <summary>把回调封送回 UI 线程（扫描完成后推送用）。</summary>
    public Action<Action>? Marshal { get; set; }

    public event Action? StateChanged;
    public event Action<string>? Logged;
    public event Action<bool>? ScanActiveChanged;

    public bool Dirty => _dirty;
    public bool Scanning => _scanning;

    /// <summary>退出前保存应用设置（上次实例/Profile/BodySlide 等）。</summary>
    public void SaveSettings() => _settings.Save();

    private void Log(string message) => Logged?.Invoke(message);
    private void Push() => StateChanged?.Invoke();

    private SliderGroup? CurrentGroup => _groups.FirstOrDefault(g =>
        string.Equals(g.Name, _currentGroupName, StringComparison.Ordinal));

    // ── 初始化与选择链 ────────────────────────────────────────────────
    public void Init() => ReloadInstances();

    public void RefreshInstances() => ReloadInstances();

    public void AddMo2Dir()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 MO2 实例目录（含 ModOrganizer.ini）或便携安装目录（含 ModOrganizer.exe）",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK)
            return;

        var instance = Mo2Discovery.CreateFromDirectory(dialog.SelectedPath);
        if (instance is null)
        {
            MessageBox.Show(DialogOwner,
                "该目录下没有 ModOrganizer.ini，也不像便携安装（缺 ModOrganizer.exe）。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!_settings.ExtraMo2Dirs.Contains(instance.InstanceDir, StringComparer.OrdinalIgnoreCase))
            _settings.ExtraMo2Dirs.Add(instance.InstanceDir);
        _settings.Save();
        ReloadInstances();
    }

    private void ReloadInstances()
    {
        _instances = Mo2Discovery.Discover(_settings.ExtraMo2Dirs);
        Log($"发现 {_instances.Count} 个 MO2 实例。");
        if (_instances.Count == 0)
        {
            Log("未找到实例：可点\"添加 MO2 目录\"选择含 ModOrganizer.ini 的实例目录或便携安装目录。");
            _instance = null;
            OnInstanceChanged();
            return;
        }
        var last = _instances.FirstOrDefault(i =>
            string.Equals(i.InstanceDir, _settings.LastInstanceDir, StringComparison.OrdinalIgnoreCase));
        _instance = last ?? _instances[0];
        if (_instance is not null)
            _settings.LastInstanceDir = _instance.InstanceDir;
        OnInstanceChanged();
    }

    public void SelectInstance(string dir)
    {
        var inst = _instances.FirstOrDefault(i =>
            string.Equals(i.InstanceDir, dir, StringComparison.OrdinalIgnoreCase));
        if (inst is null || ReferenceEquals(inst, _instance))
            return;
        _instance = inst;
        _settings.LastInstanceDir = inst.InstanceDir;
        OnInstanceChanged();
    }

    private void OnInstanceChanged()
    {
        _profiles = _instance?.GetProfiles() ?? new List<string>();
        _profile = null;
        _entries = new List<ModEntry>();
        _mods = new List<(ModEntry, string)>();

        if (_instance is null)
        {
            Log("未选择 MO2 实例（也可直接手动指定 BodySlide 目录）。");
        }
        else
        {
            Log($"实例 {_instance.Name}：mods = {_instance.ModsDirectory}");
            if (_profiles.Count == 0)
                Log("警告：该实例没有可用的 profile（目录下缺少 modlist.txt）。");

            var target = _instance.SelectedProfile is { Length: > 0 } sp && _profiles.Contains(sp)
                ? sp
                : _settings.LastProfile is { Length: > 0 } lp && _profiles.Contains(lp)
                    ? lp
                    : _profiles.FirstOrDefault();
            _profile = target;
            _settings.LastProfile = _profile;
        }
        OnProfileChanged();
    }

    public void SelectProfile(string name)
    {
        if (_profiles.Contains(name) && name != _profile)
        {
            _profile = name;
            _settings.LastProfile = _profile;
            OnProfileChanged();
        }
    }

    private void OnProfileChanged()
    {
        _entries = new List<ModEntry>();
        _mods = new List<(ModEntry, string)>();

        if (_instance is not null && _profile is not null)
        {
            var modListPath = _instance.GetModListPath(_profile);
            if (File.Exists(modListPath))
            {
                var entries = ModListParser.Parse(modListPath);
                _entries = entries;
                _mods = ModListParser.GetEnabledModDirectories(entries, _instance.ModsDirectory);
                var listed = entries.Count(e => e.Enabled && !e.IsForeign && !e.IsSeparator);
                Log($"Profile \"{_profile}\"：启用模组 {_mods.Count} 个（另有 {Math.Max(0, listed - _mods.Count)} 个目录不存在，已忽略）。");
            }
            else
            {
                Log($"警告：找不到 {modListPath}");
            }
        }

        _ = DetectBodySlideAsync();
    }

    // ── BodySlide 定位 ────────────────────────────────────────────────
    public void DetectBodySlide() => _ = DetectBodySlideAsync();

    private async Task DetectBodySlideAsync()
    {
        var previous = _bsAppDir;
        var modsSnapshot = _mods;
        var gamePath = _instance?.GamePath;
        _bsCandidates = await Task.Run(() =>
            BodySlideLocator.FindCandidates(modsSnapshot, gamePath, previous));

        _bsAppDir = previous is not null
            ? _bsCandidates.FirstOrDefault(c =>
                  string.Equals(c.AppDir, previous, StringComparison.OrdinalIgnoreCase))?.AppDir
            : null;
        _bsAppDir ??= _bsCandidates.FirstOrDefault(c =>
            string.Equals(c.AppDir, _settings.LastBodySlideDir, StringComparison.OrdinalIgnoreCase))?.AppDir;
        _bsAppDir ??= _bsCandidates.FirstOrDefault()?.AppDir;
        if (_bsAppDir is not null)
            _settings.LastBodySlideDir = _bsAppDir;
        else
            Log("未自动找到 BodySlide：请点\"浏览…\"选择含 BodySlide.exe 和 Config.xml 的目录（通常是 MO2 里 BodySlide 模组所在的目录）。");
        Push();
        await RunScanAsync();
    }

    public void SelectBodySlide(string dir)
    {
        if (string.Equals(_bsAppDir, dir, StringComparison.OrdinalIgnoreCase))
            return;
        _bsAppDir = dir;
        _settings.LastBodySlideDir = dir;
        Log($"BodySlide 目录：{dir}");
        _ = RunScanAsync();
        Push();
    }

    public void BrowseBodySlide()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择含 BodySlide.exe（或 BodySlide x64.exe）和 Config.xml 的目录",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK)
            return;

        var dir = dialog.SelectedPath;
        if (!File.Exists(Path.Combine(dir, "Config.xml")))
        {
            MessageBox.Show(DialogOwner, "所选目录下没有 Config.xml，无法作为 BodySlide 目录。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _bsAppDir = dir;
        _settings.LastBodySlideDir = dir;
        Log($"BodySlide 目录（手动）：{dir}");
        _ = RunScanAsync();
        Push();
    }

    // ── 输出位置 ──────────────────────────────────────────────────────
    public void SetWriteMode(string mode)
    {
        if (Enum.TryParse<WriteMode>(mode, ignoreCase: true, out var parsed))
        {
            _settings.WriteMode = parsed;
            _settings.Save();
            LogWriteTarget();
            Push();
        }
    }

    public void BrowseTargetDir()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择分组文件的保存目录（每个组生成一个 <组名>.xml）",
            ShowNewFolderButton = true,
        };
        var initial = _settings.CustomTargetDir;
        if (string.IsNullOrEmpty(initial) && _resolution is not null)
            initial = Path.Combine(_resolution.EffectivePath, "SliderGroups");
        if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
            dialog.SelectedPath = initial;
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK)
            return;

        _settings.CustomTargetDir = dialog.SelectedPath;
        _settings.WriteMode = WriteMode.Custom;
        _settings.Save();
        Log($"自定义输出目录：{dialog.SelectedPath}");
        Push();
    }

    private (string Dir, string Description)? ResolveWriteTarget()
    {
        if (_resolution is null || _bsAppDir is null)
            return null;
        return ResolveWriteTargetCore(_resolution, _bsAppDir, _instance, _settings.WriteMode, _settings.CustomTargetDir);
    }

    private static (string Dir, string Description)? ResolveWriteTargetCore(
        ProjectPathResolution resolution, string bsAppDir, Mo2Instance? instance, WriteMode mode, string? customDir)
    {
        var virtualKind = resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools;

        (string, string)? Mo2Mod() =>
            instance is null || !Directory.Exists(instance.ModsDirectory)
                ? null
                : (Path.Combine(instance.ModsDirectory, DedicatedModName, "CalienteTools", "BodySlide", "SliderGroups"),
                   $"MO2 专用模组（{DedicatedModName}）");

        (string, string)? RealData() =>
            string.IsNullOrWhiteSpace(resolution.GameDataPath)
                ? null
                : (Path.Combine(resolution.GameDataPath, "CalienteTools", "BodySlide", "SliderGroups"),
                   "游戏真实 Data");

        return mode switch
        {
            WriteMode.BodySlideDir =>
                (Path.Combine(bsAppDir, "SliderGroups"), "BodySlide 程序目录"),
            WriteMode.Mo2Mod => Mo2Mod(),
            WriteMode.RealGameData => RealData(),
            WriteMode.Custom => string.IsNullOrWhiteSpace(customDir)
                ? null
                : (customDir, "自定义位置"),
            _ => !virtualKind
                ? (Path.Combine(resolution.EffectivePath, "SliderGroups"), "有效项目路径\\SliderGroups（自动）")
                : Mo2Mod() ?? RealData(),
        };
    }

    private void LogWriteTarget()
    {
        var target = ResolveWriteTarget();
        if (target is null)
        {
            var hint = _settings.WriteMode == WriteMode.Custom
                ? "未设置（点\"浏览…\"选择保存目录）"
                : "未确定（先完成扫描）";
            Log($"输出目录：{hint}。");
            return;
        }
        Log($"输出目录：{target.Value.Dir}（{target.Value.Description}）");
    }

    // ── 扫描 ──────────────────────────────────────────────────────────
    private async Task RunScanAsync()
    {
        if (_scanning)
        {
            _scanQueued = true; // 扫描进行中又收到新请求：完成后补一次，避免丢扫描
            return;
        }
        var bsDir = _bsAppDir;
        if (bsDir is null)
        {
            _resolution = null;
            _scan = null;
            Push();
            return;
        }

        _scanning = true;
        ScanActiveChanged?.Invoke(true);
        try
        {
            var modsSnapshot = _mods;
            var instanceSnapshot = _instance;
            var writeMode = _settings.WriteMode;
            var customDir = _settings.CustomTargetDir;
            var outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customDir));
            Marshal?.Invoke(() => ApplyScanOutcome(outcome));
        }
        catch (Exception ex)
        {
            Log($"扫描失败：{ex}");
        }
        finally
        {
            var wasQueued = _scanQueued;
            _scanning = false;
            ScanActiveChanged?.Invoke(false);
            Marshal?.Invoke(() =>
            {
                if (wasQueued)
                {
                    _scanQueued = false;
                    _ = RunScanAsync();
                }
                else
                {
                    Push();
                }
            });
        }
    }

    private sealed record ScanOutcome(
        ProjectPathResolution? Resolution,
        ScanResult? Result,
        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,
        List<string> Errors);

    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, Mo2Instance? instance, WriteMode writeMode, string? customDir)
    {
        var errors = new List<string>();
        var configPath = Path.Combine(bsDir, "Config.xml");
        if (!File.Exists(configPath))
            return new ScanOutcome(null, null, new List<SliderGroup>(), null, "",
                new List<string> { $"找不到 {configPath}" });

        var config = new BodySlideConfig(configPath);
        var resolution = BodySlideLocator.ResolveProjectPath(config, bsDir, mods, instance?.GamePath);
        var scan = SliderSetScanner.Scan(resolution, mods);

        var target = ResolveWriteTargetCore(resolution, bsDir, instance, writeMode, customDir);
        var existing = new List<SliderGroup>();
        if (target?.Dir is { } targetDir)
        {
            // 载入上次生成的组：优先按清单，兼容旧版单文件
            var manifestPath = Path.Combine(targetDir, SliderGroupFile.ManifestFileName);
            var filesToLoad = new List<string>();
            if (File.Exists(manifestPath))
            {
                filesToLoad.AddRange(File.ReadAllLines(manifestPath)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 &&
                                   line.IndexOf('/') < 0 && line.IndexOf('\\') < 0)
                    .Select(line => Path.Combine(targetDir, line))
                    .Where(File.Exists));
            }
            else
            {
                var legacy = Path.Combine(targetDir, SliderGroupFile.DefaultFileName);
                if (File.Exists(legacy))
                    filesToLoad.Add(legacy);
            }

            foreach (var file in filesToLoad)
            {
                if (!SliderGroupFile.TryLoad(file, out var groups, out var error))
                    errors.Add($"读取已有分组文件失败：{Path.GetFileName(file)}：{error}");
                else
                    SliderGroupFile.Merge(existing, groups, out _, out _);
            }
        }

        return new ScanOutcome(resolution, scan, existing, target?.Dir, target?.Description ?? "", errors);
    }

    private void ApplyScanOutcome(ScanOutcome outcome)
    {
        foreach (var error in outcome.Errors)
            Log($"错误：{error}");

        _resolution = outcome.Resolution;
        _scan = outcome.Result;
        _conflictNames = outcome.Result is null
            ? null
            : outcome.Result.Outfits.Where(o => o.HasConflict).Select(o => o.Name).ToHashSet(StringComparer.Ordinal);

        if (outcome.Resolution is null)
        {
            Log("有效项目路径：无法解析（见日志）");
            Push();
            return;
        }

        var kindText = BodySlideLocator.DescribeKind(outcome.Resolution.Kind);
        Log($"BodySlide 有效项目路径：{outcome.Resolution.EffectivePath}（{kindText}）");
        foreach (var note in outcome.Result?.LayerNotes ?? new List<string>())
            Log($"  {note}");

        if (_scan is { } scan)
        {
            Log($"扫描完成：{scan.WinnerFileCount} 个有效滑块组文件，{scan.Outfits.Count} 个服装，{scan.Warnings.Count} 个警告。");
            foreach (var warning in scan.Warnings.Take(20))
                Log($"  警告：{warning}");
        }

        // 只在内存中还没有组时才载入上次写出的文件，避免覆盖未保存的修改
        if (!_dirty && _groups.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _groups.AddRange(outcome.ExistingGroups);
            _dirty = false;
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");
            if (_currentGroupName is null && _groups.Count > 0)
                _currentGroupName = _groups[0].Name;
        }

        LogWriteTarget();
        Push();
    }

    // ── 组操作 ────────────────────────────────────────────────────────
    private bool IsInAnyGroup(string outfit)
    {
        foreach (var group in _groups)
        {
            if (group.Members.Contains(outfit, StringComparer.Ordinal))
                return true;
        }
        return false;
    }

    private bool GroupNameExists(string name, SliderGroup? except = null) =>
        _groups.Any(g => !ReferenceEquals(g, except) &&
                         string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    public void NewGroup(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
            return;
        if (GroupNameExists(name))
        {
            MessageBox.Show(DialogOwner, "已存在同名组（忽略大小写）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _dirty = true;
        _groups.Add(new SliderGroup(name));
        _currentGroupName = name;
        Log($"新建组：{name}");
        Push();
    }

    public void RenameGroup(string oldName, string newName)
    {
        var group = _groups.FirstOrDefault(g => g.Name == oldName);
        if (group is null)
            return;
        newName = newName.Trim();
        if (newName.Length == 0 || newName == group.Name)
            return;
        if (GroupNameExists(newName, group))
        {
            MessageBox.Show(DialogOwner, "已存在同名组（忽略大小写）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Log($"组 \"{group.Name}\" 重命名为 \"{newName}\"。");
        group.Name = newName;
        _dirty = true;
        Push();
    }

    public void DeleteGroup(string name)
    {
        var group = _groups.FirstOrDefault(g => g.Name == name);
        if (group is null)
            return;
        if (MessageBox.Show(DialogOwner, $"确定删除组 \"{group.Name}\"？（成员 {group.Members.Count} 个）",
                "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        _groups.Remove(group);
        if (_currentGroupName == name)
            _currentGroupName = _groups.FirstOrDefault()?.Name;
        _dirty = true;
        Log($"删除组：{group.Name}");
        Push();
    }

    public void SelectGroup(string name)
    {
        if (!string.Equals(_currentGroupName, name, StringComparison.Ordinal))
        {
            _currentGroupName = name;
            Push();
        }
    }

    public void ApplyToGroup(IReadOnlyList<string> names, bool add)
    {
        var group = CurrentGroup;
        if (group is null)
        {
            MessageBox.Show(DialogOwner, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var distinct = names.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
            return;

        foreach (var name in distinct)
        {
            if (add)
            {
                if (!group.Members.Contains(name, StringComparer.Ordinal))
                    group.Members.Add(name);
            }
            else
            {
                group.Members.RemoveAll(m => m == name);
            }
        }
        _dirty = true;
        Log($"已把 {distinct.Count} 个服装{(add ? "加入" : "移出")}组 \"{group.Name}\"（现成员 {group.Members.Count} 个）。");
        Push();
    }

    public void ImportGroups()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入 BodySlide 分组文件",
            Filter = "分组文件 (*.xml)|*.xml|全部文件 (*.*)|*.*",
        };
        var initialDir = _resolution is not null
            ? Path.Combine(_resolution.EffectivePath, "SliderGroups")
            : _bsAppDir;
        if (initialDir is not null && Directory.Exists(initialDir))
            dialog.InitialDirectory = initialDir;
        if (dialog.ShowDialog(DialogOwner) != DialogResult.OK)
            return;

        if (!SliderGroupFile.TryLoad(dialog.FileName, out var imported, out var error))
        {
            MessageBox.Show(DialogOwner, $"导入失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);
        if (addedGroups + addedMembers > 0)
            _dirty = true;
        Log($"从 {dialog.FileName} 导入：新增 {addedGroups} 个组、{addedMembers} 个成员。");
        Push();
    }

    // ── 保存 ──────────────────────────────────────────────────────────
    public bool TrySave(bool showSuccessDialog) => TrySaveGroups(showSuccessDialog);

    private bool TrySaveGroups(bool showSuccessDialog)
    {
        var target = ResolveWriteTarget();
        if (target is null)
        {
            var hint = _settings.WriteMode == WriteMode.Custom
                ? "请先点\"浏览…\"选择保存目录。"
                : "尚未完成扫描或无法确定写入位置。";
            MessageBox.Show(DialogOwner, hint, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Count == 0)
        {
            MessageBox.Show(DialogOwner, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Any(g => g.Name.Trim().Length == 0))
        {
            MessageBox.Show(DialogOwner, "存在空名称的组，请先重命名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // 组名 → 文件名，检查重名冲突
        var fileByGroup = new Dictionary<SliderGroup, string>();
        var groupByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in _groups)
        {
            var fileName = SliderGroupFile.FileNameForGroup(group.Name);
            if (groupByFile.TryGetValue(fileName, out var other))
            {
                MessageBox.Show(DialogOwner,
                    $"组 \"{group.Name}\" 和 \"{other}\" 的文件名相同（都叫 {fileName}），请重命名其中一个组。",
                    "文件名冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            groupByFile[fileName] = group.Name;
            fileByGroup[group] = fileName;
        }

        var dir = target.Value.Dir;
        try
        {
            Directory.CreateDirectory(dir);

            // 清理旧版单文件
            var legacy = Path.Combine(dir, SliderGroupFile.DefaultFileName);
            if (File.Exists(legacy))
            {
                File.Delete(legacy);
                Log($"已删除旧版单文件：{SliderGroupFile.DefaultFileName}");
            }

            // 按清单清理已改名/已删除组留下的旧文件
            var manifestPath = Path.Combine(dir, SliderGroupFile.ManifestFileName);
            if (File.Exists(manifestPath))
            {
                foreach (var line in File.ReadAllLines(manifestPath))
                {
                    var old = line.Trim();
                    if (old.Length == 0 || groupByFile.ContainsKey(old))
                        continue;
                    try
                    {
                        File.Delete(Path.Combine(dir, old));
                        Log($"已删除旧分组文件：{old}");
                    }
                    catch
                    {
                        // 删除失败不阻断保存
                    }
                }
            }

            // 每个组一个文件：<组名>.xml
            foreach (var group in _groups)
                SliderGroupFile.Save(Path.Combine(dir, fileByGroup[group]), new[] { group });

            File.WriteAllLines(manifestPath,
                groupByFile.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            Log($"写入失败：{ex.Message}");
            MessageBox.Show(DialogOwner, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _dirty = false;
        var memberCount = _groups.Sum(g => g.Members.Count);
        var fileExamples = string.Join("、", _groups.Take(3).Select(g => SliderGroupFile.FileNameForGroup(g.Name)));
        Log($"已保存 {dir} 下 {_groups.Count} 个组文件（共 {memberCount} 个成员，{target.Value.Description}）。");

        if (showSuccessDialog)
        {
            var customNote = _settings.WriteMode == WriteMode.Custom
                ? "\n\n注意：自定义目录不一定是 BodySlide 能读取的地方（有效项目路径的 SliderGroups 或模组的 SliderGroups）。若 BodySlide 里看不到新组，请把文件移动到这些位置。"
                : "";
            MessageBox.Show(DialogOwner,
                $"已保存 {_groups.Count} 个组到目录：\n{dir}\n\n" +
                $"每个组一个文件，文件名即组名：{fileExamples}{(_groups.Count > 3 ? " …" : "")}\n" +
                $"共 {memberCount} 个成员（{target.Value.Description}）。{customNote}\n\n" +
                "请重启 BodySlide 使其生效。若 BodySlide 是通过 MO2 启动的，建议同时重启 MO2 以刷新虚拟文件系统。",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        Push();
        return true;
    }

    // ── 展示数据 ──────────────────────────────────────────────────────
    /// <summary>按左侧树的结构返回显示数据：分隔符 → 模组 → 服装。</summary>
    private List<(string? Separator, string Owner, List<OutfitEntry> Outfits)> GetTreeDisplayStructure()
    {
        var result = new List<(string?, string, List<OutfitEntry>)>();
        if (_scan is null)
            return result;

        var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
        foreach (var outfit in _scan.Outfits)
        {
            if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                outfitsByOwner[outfit.OwnerLabel] = list = new List<OutfitEntry>();
            list.Add(outfit);
        }

        if (IsVirtualScan)
        {
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            string? separator = null;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.IsForeign)
                    continue;

                if (entry.IsSeparator)
                {
                    separator = SeparatorTitle(entry.Name);
                    continue;
                }

                if (!entry.Enabled || _instance is null ||
                    !Directory.Exists(Path.Combine(_instance.ModsDirectory, entry.Name)))
                    continue;

                if (outfitsByOwner.TryGetValue(entry.Name, out var outfits) && outfits.Count > 0)
                {
                    consumed.Add(entry.Name);
                    result.Add((separator, entry.Name, outfits));
                }
            }

            // 未归属到启用模组的服装（如游戏 Data 本体），不带分隔符放在最后
            foreach (var (owner, list) in outfitsByOwner)
            {
                if (!consumed.Contains(owner) && list.Count > 0)
                    result.Add((null, owner, list));
            }
        }
        else
        {
            foreach (var (owner, list) in outfitsByOwner)
                result.Add((null, owner, list));
        }

        return result;
    }

    private static string SeparatorTitle(string name) =>
        name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase)
            ? name[..^"_separator".Length]
            : name;

    /// <summary>是否来自虚拟 Data 汇聚点（决定树是否按 MO2 结构展示）。</summary>
    private bool IsVirtualScan =>
        _resolution is not null
        && _resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools
        && _entries.Count > 0;

    private string BuildInfoText()
    {
        if (_resolution is null)
            return _bsAppDir is null ? "尚未扫描。" : "有效项目路径：无法解析（见日志）";
        var kindText = BodySlideLocator.DescribeKind(_resolution.Kind);
        var game = string.IsNullOrEmpty(_instance?.GameName) ? "未知" : _instance!.GameName;
        return $"游戏：{game}　·　有效项目路径：{_resolution.EffectivePath}（{kindText}）";
    }

    /// <summary>推送给前端的全量状态快照。</summary>
    public object BuildState()
    {
        var target = _resolution is not null && _bsAppDir is not null
            ? ResolveWriteTargetCore(_resolution, _bsAppDir, _instance, _settings.WriteMode, _settings.CustomTargetDir)
            : null;

        var total = _scan?.Outfits.Count ?? 0;
        var assigned = _scan is null ? 0 : _scan.Outfits.Count(o => IsInAnyGroup(o.Name));

        return new
        {
            type = "state",
            dirty = _dirty,
            scanning = _scanning,
            instances = _instances.Select(i => new { dir = i.InstanceDir, display = i.DisplayName }).ToList(),
            selectedInstance = _instance?.InstanceDir,
            profiles = _profiles,
            selectedProfile = _profile,
            game = string.IsNullOrEmpty(_instance?.GameName) ? null : _instance!.GameName,
            bsCandidates = _bsCandidates.Select(c => new { dir = c.AppDir, source = c.Source }).ToList(),
            selectedBs = _bsAppDir,
            writeMode = _settings.WriteMode.ToString(),
            customTargetDir = string.IsNullOrEmpty(_settings.CustomTargetDir) ? null : _settings.CustomTargetDir,
            targetDir = target?.Dir,
            targetDesc = target?.Description,
            infoText = BuildInfoText(),
            virtualScan = IsVirtualScan,
            sections = GetTreeDisplayStructure().Select(x => new
            {
                separator = x.Separator,
                owner = x.Owner,
                outfits = x.Outfits.Select(o => new
                {
                    name = o.Name,
                    conflict = o.HasConflict,
                }).ToList(),
            }).ToList(),
            groups = _groups.Select(g => new { name = g.Name, count = g.Members.Count, members = g.Members }).ToList(),
            currentGroup = _currentGroupName,
            counts = new { mods = _mods.Count, outfits = total, assigned, unassigned = total - assigned },
        };
    }

    public string GetDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MO2 实例 ===");
        foreach (var instance in _instances)
        {
            sb.AppendLine($"[{instance.DisplayName}]");
            sb.AppendLine($"  实例目录: {instance.InstanceDir}");
            sb.AppendLine($"  mods:     {instance.ModsDirectory} ({(Directory.Exists(instance.ModsDirectory) ? "存在" : "不存在")})");
            sb.AppendLine($"  profiles: {instance.ProfilesDirectory}");
            sb.AppendLine($"  gameName: {instance.GameName}");
            sb.AppendLine($"  gamePath: {instance.GamePath}");
        }
        if (_instances.Count == 0)
            sb.AppendLine("（无）");

        sb.AppendLine();
        sb.AppendLine("=== 当前 Profile ===");
        sb.AppendLine($"{_profile ?? "（无）"} — 启用模组 {_mods.Count} 个");
        foreach (var (entry, dir) in _mods.Take(200))
            sb.AppendLine($"  #{entry.Priority} {entry.Name} → {(Directory.Exists(dir) ? "存在" : "缺失")}");

        sb.AppendLine();
        sb.AppendLine("=== BodySlide ===");
        sb.AppendLine($"目录: {_bsAppDir ?? "（未选择）"}");
        if (_resolution is not null)
        {
            sb.AppendLine($"Kind: {_resolution.Kind}");
            sb.AppendLine($"有效项目路径: {_resolution.EffectivePath}");
            sb.AppendLine($"GameDataPath: {_resolution.GameDataPath}（来自 MO2: {_resolution.GameDataPathFromMo2}）");
            sb.AppendLine("解析步骤:");
            foreach (var step in _resolution.Steps)
                sb.AppendLine($"  - {step}");
        }

        sb.AppendLine();
        sb.AppendLine("=== 扫描结果 ===");
        if (_scan is null)
        {
            sb.AppendLine("（未扫描）");
        }
        else
        {
            foreach (var note in _scan.LayerNotes)
                sb.AppendLine(note);
            sb.AppendLine($"服装总数: {_scan.Outfits.Count}（同名冲突 {_scan.Outfits.Count(o => o.HasConflict)}）");
            sb.AppendLine($"输出目录: {ResolveWriteTarget()?.Dir ?? "（未定）"}");
        }
        return sb.ToString();
    }
}
