# -*- coding: utf-8 -*-
# 输出重构：每个组一个 <组名>.xml，清单跟踪清理旧文件，自定义模式改为选目录
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

# ── Core/SliderGroupFile.cs：清单常量 + 组名转文件名 ──
patch('src/BSGroupGenerator/Core/SliderGroupFile.cs', [(
'''    public const string DefaultFileName = "BSGroupGenerator.xml";
''',
'''    public const string DefaultFileName = "BSGroupGenerator.xml";
    public const string ManifestFileName = "BSGroupGenerator.files.txt";

    /// <summary>组名转安全文件名（非法字符替换为下划线，自动补 .xml）。</summary>
    public static string FileNameForGroup(string groupName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in groupName.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var name = sb.ToString().TrimEnd('.', ' ');
        return (name.Length == 0 ? "未命名组" : name) + ".xml";
    }
''')])

# ── Core/AppSettings.cs：自定义输出从"文件"改为"目录" ──
patch('src/BSGroupGenerator/Core/AppSettings.cs', [(
'''    public string? CustomTargetPath { get; set; }''',
'''    public string? CustomTargetDir { get; set; }''')])

# ── UI/MainForm.cs ──
pairs = []

# ScanOutcome 字段
pairs.append((
'''        List<SliderGroup> ExistingGroups,
        string? TargetPath,
        string TargetDescription,''',
'''        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,'''))

# RunScanAsync 快照
pairs.append((
'''            var customPath = _settings.CustomTargetPath;
            var outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customPath));''',
'''            var customDir = _settings.CustomTargetDir;
            var outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customDir));'''))

# ComputeScan：按清单/旧文件载入
pairs.append((
'''    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, Mo2Instance? instance, WriteMode writeMode, string? customPath)
    {''',
'''    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, Mo2Instance? instance, WriteMode writeMode, string? customDir)
    {'''))
pairs.append((
'''        var target = ResolveWriteTargetCore(resolution, bsDir, instance, writeMode, customPath);
        var existing = new List<SliderGroup>();
        if (target?.Path is { } targetPath && File.Exists(targetPath) &&
            !SliderGroupFile.TryLoad(targetPath, out existing, out var error))
        {
            errors.Add($"读取已有分组文件失败：{error}");
        }

        return new ScanOutcome(resolution, scan, existing, target?.Path, target?.Description ?? "", errors);''',
'''        var target = ResolveWriteTargetCore(resolution, bsDir, instance, writeMode, customDir);
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
                                   line.IndexOf('/') < 0 && line.IndexOf('\\\\') < 0)
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

        return new ScanOutcome(resolution, scan, existing, target?.Dir, target?.Description ?? "", errors);'''))

# ResolveWriteTarget：目录化
pairs.append((
'''        return ResolveWriteTargetCore(_resolution, _bsAppDir, _instance, _settings.WriteMode, _settings.CustomTargetPath);''',
'''        return ResolveWriteTargetCore(_resolution, _bsAppDir, _instance, _settings.WriteMode, _settings.CustomTargetDir);'''))
pairs.append((
'''    private static (string Path, string Description)? ResolveWriteTargetCore(
        ProjectPathResolution resolution, string bsAppDir, Mo2Instance? instance, WriteMode mode, string? customPath)
    {
        var fileName = SliderGroupFile.DefaultFileName;
        var virtualKind = resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools;

        (string, string)? Mo2Mod() =>
            instance is null || !Directory.Exists(instance.ModsDirectory)
                ? null
                : (Path.Combine(instance.ModsDirectory, DedicatedModName, "CalienteTools", "BodySlide", "SliderGroups", fileName),
                   $"MO2 专用模组（{DedicatedModName}）");

        (string, string)? RealData() =>
            string.IsNullOrWhiteSpace(resolution.GameDataPath)
                ? null
                : (Path.Combine(resolution.GameDataPath, "CalienteTools", "BodySlide", "SliderGroups", fileName),
                   "游戏真实 Data");

        return mode switch
        {
            WriteMode.BodySlideDir =>
                (Path.Combine(bsAppDir, "SliderGroups", fileName), "BodySlide 程序目录"),
            WriteMode.Mo2Mod => Mo2Mod(),
            WriteMode.RealGameData => RealData(),
            WriteMode.Custom => string.IsNullOrWhiteSpace(customPath)
                ? null
                : (customPath, "自定义位置"),
            _ => !virtualKind
                ? (Path.Combine(resolution.EffectivePath, "SliderGroups", fileName), "有效项目路径\\\\SliderGroups（自动）")
                : Mo2Mod() ?? RealData(),
        };
    }''',
'''    private static (string Dir, string Description)? ResolveWriteTargetCore(
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
                ? (Path.Combine(resolution.EffectivePath, "SliderGroups"), "有效项目路径\\\\SliderGroups（自动）")
                : Mo2Mod() ?? RealData(),
        };
    }'''))

# ApplyScanOutcome 日志
pairs.append((
'''            Log($"已载入上次生成的分组文件（{_groups.Count} 个组）：{outcome.TargetPath}");''',
'''            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");'''))

# LogWriteTarget
pairs.append((
'''        _statusTarget.Text = $"写出：{target.Value.Description}";
        _statusTarget.ToolTipText = target.Value.Path;
        Log($"写出目标：{target.Value.Path}（{target.Value.Description}）");''',
'''        _statusTarget.Text = $"输出：{target.Value.Description}";
        _statusTarget.ToolTipText = target.Value.Dir;
        Log($"输出目录：{target.Value.Dir}（{target.Value.Description}）");'''))

# BrowseForTarget：选目录
pairs.append((
'''        using var dialog = new SaveFileDialog
        {
            Title = "选择分组文件保存位置",
            Filter = "分组文件 (*.xml)|*.xml|全部文件 (*.*)|*.*",
            FileName = SliderGroupFile.DefaultFileName,
        };
        var initial = _settings.CustomTargetPath;
        if (string.IsNullOrEmpty(initial) && _resolution is not null)
            initial = Path.Combine(_resolution.EffectivePath, "SliderGroups", SliderGroupFile.DefaultFileName);
        if (!string.IsNullOrEmpty(initial))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(initial));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                dialog.InitialDirectory = dir;
                dialog.FileName = Path.GetFileName(initial);
            }
        }
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.CustomTargetPath = dialog.FileName;
        _settings.Save();
        Log($"自定义保存位置：{dialog.FileName}");''',
'''        using var dialog = new FolderBrowserDialog
        {
            Description = "选择分组文件的保存目录（每个组生成一个 <组名>.xml）",
            ShowNewFolderButton = true,
        };
        var initial = _settings.CustomTargetDir;
        if (string.IsNullOrEmpty(initial) && _resolution is not null)
            initial = Path.Combine(_resolution.EffectivePath, "SliderGroups");
        if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
            dialog.SelectedPath = initial;
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.CustomTargetDir = dialog.SelectedPath;
        _settings.Save();
        Log($"自定义输出目录：{dialog.SelectedPath}");'''))

# OnGenerate：按组拆文件 + 清理 + 清单
pairs.append((
'''        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target.Value.Path)!);
            SliderGroupFile.Save(target.Value.Path, _groups);
        }
        catch (Exception ex)
        {
            Log($"写入失败：{ex.Message}");
            MessageBox.Show(this, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var memberCount = _groups.Sum(g => g.Members.Count);
        Log($"已写出 {target.Value.Path}（{_groups.Count} 个组，{memberCount} 个成员，{target.Value.Description}）。");
        var customNote = _settings.WriteMode == WriteMode.Custom
            ? "\\n\\n注意：自定义位置不一定是 BodySlide 能读取的地方（有效项目路径的 SliderGroups 或模组的 SliderGroups）。若 BodySlide 里看不到新组，请把文件移动到这些位置。"
            : "";
        MessageBox.Show(this,
            $"已保存分组文件：\\n{target.Value.Path}\\n\\n" +
            $"共 {_groups.Count} 个组，{memberCount} 个成员（{target.Value.Description}）。{customNote}\\n\\n" +
            "请重启 BodySlide 使其生效。若 BodySlide 是通过 MO2 启动的，建议同时重启 MO2 以刷新虚拟文件系统。",
            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }''',
'''        // 组名 → 文件名，检查重名冲突
        var fileByGroup = new Dictionary<SliderGroup, string>();
        var groupByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in _groups)
        {
            var fileName = SliderGroupFile.FileNameForGroup(group.Name);
            if (groupByFile.TryGetValue(fileName, out var other))
            {
                MessageBox.Show(this,
                    $"组 \\"{group.Name}\\" 和 \\"{other}\\" 的文件名相同（都叫 {fileName}），请重命名其中一个组。",
                    "文件名冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
            MessageBox.Show(this, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var memberCount = _groups.Sum(g => g.Members.Count);
        var fileExamples = string.Join("、", _groups.Take(3).Select(g => SliderGroupFile.FileNameForGroup(g.Name)));
        Log($"已保存 {dir} 下 {_groups.Count} 个组文件（共 {memberCount} 个成员，{target.Value.Description}）。");
        var customNote2 = _settings.WriteMode == WriteMode.Custom
            ? "\\n\\n注意：自定义目录不一定是 BodySlide 能读取的地方（有效项目路径的 SliderGroups 或模组的 SliderGroups）。若 BodySlide 里看不到新组，请把文件移动到这些位置。"
            : "";
        MessageBox.Show(this,
            $"已保存 {_groups.Count} 个组到目录：\\n{dir}\\n\\n" +
            $"每个组一个文件，文件名即组名：{fileExamples}{(_groups.Count > 3 ? " …" : "")}\\n" +
            $"共 {memberCount} 个成员（{target.Value.Description}）。{customNote2}\\n\\n" +
            "请重启 BodySlide 使其生效。若 BodySlide 是通过 MO2 启动的，建议同时重启 MO2 以刷新虚拟文件系统。",
            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }'''))

# 诊断信息
pairs.append((
'''            sb.AppendLine($"写出目标: {ResolveWriteTarget()?.Path ?? "（未定）"}");''',
'''            sb.AppendLine($"输出目录: {ResolveWriteTarget()?.Dir ?? "（未定）"}");'''))

patch('src/BSGroupGenerator/UI/MainForm.cs', pairs)
