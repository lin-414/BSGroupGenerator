using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using BSGroupGenerator.Core;
using BSGroupGenerator.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BSGroupGenerator.Wpf.ViewModels;

/// <summary>状态栏写出模式下拉项。</summary>
public sealed record WriteModeItem(WriteMode Mode, string Label);

/// <summary>组列表项。</summary>
public sealed record GroupItem(string Name, int Count)
{
    public string Display => $"{Name}　({Count})";
}

public partial class MainViewModel : ObservableObject
{
    public static string AppTitle => L10n.Tr("L.App_Title");
    public const string DedicatedModName = "BS Group Generator";

    public AppSettings Settings { get; } = AppSettings.Load();
    public GroupStore Store { get; } = new();

    public List<ModEntry> Entries { get; private set; } = [];
    public List<(ModEntry Entry, string Dir)> Mods { get; private set; } = [];
    public ProjectPathResolution? Resolution { get; private set; }
    public ScanResult? Scan { get; private set; }

    private bool _scanning;
    private bool _localizing;
    private bool _scanQueued;
    private bool _closed;
    private string? _bsAppDir;

    // 视图交互统一走「视图注入委托」这一套：下面的 NotifyHandler，以及 Save.cs 里的
    // ConfirmHandler / FolderPicker / FilePicker，由 MainWindow 构造时赋值。
    // 曾并存一组 Notify / Confirm / RequestClose 事件，从未被触发（CS0067 可证），已删除，
    // 免得两套机制并存时看错哪套在生效。
    public Func<string, string, bool, bool>? NotifyHandler { get; set; } // 视图注入：MessageBox 包装 (title, message, warning)

    public void NotifyUser(string title, string message, bool warning = false) =>
        NotifyHandler?.Invoke(title, message, warning);

    // ── 绑定状态 ──
    [ObservableProperty] private string _windowTitle = AppTitle;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private ObservableCollection<Mo2Instance> _instances = [];
    [ObservableProperty] private Mo2Instance? _selectedInstance;
    [ObservableProperty] private ObservableCollection<string> _profiles = [];
    [ObservableProperty] private string? _selectedProfile;
    [ObservableProperty] private ObservableCollection<BodySlideCandidate> _bodySlideDirs = [];
    [ObservableProperty] private BodySlideCandidate? _selectedBodySlide;
    [ObservableProperty] private ObservableCollection<WriteModeItem> _writeModes = [];
    [ObservableProperty] private WriteModeItem? _selectedWriteMode;
    [ObservableProperty] private string _infoLine = L10n.Tr("L.Vm_NotScanned");
    [ObservableProperty] private string _statusCounts = L10n.Tr("L.Vm_NotScanned");
    [ObservableProperty] private string _outputText = L10n.Tr("L.Vm_OutputNone");
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private ObservableCollection<GroupItem> _groups = [];
    [ObservableProperty] private int _selectedGroupIndex = -1;
    [ObservableProperty] private string _groupInfo = L10n.Tr("L.Vm_NoGroupSelected");
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty] private string _scanStatusText = "";
    [ObservableProperty] private string _scanProgressDetail = "";
    [ObservableProperty] private double _scanProgressValue;

    /// <summary>BodySlide 探测或服装扫描进行中 → 主窗口盖进度遮罩（拦截点击，避免被误认为卡死）。</summary>
    public bool IsBusy => IsDetecting || IsScanning;
    partial void OnIsDetectingChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    private readonly StringBuilder _logBuffer = new();

    public MainViewModel()
    {
        WriteModes = BuildWriteModes();
        _selectedWriteMode = WriteModes.FirstOrDefault(m => m.Mode == Settings.WriteMode) ?? WriteModes[0];

        ReloadInstances();
    }

    private static string KindText(ProjectPathKind kind) => kind switch
    {
        ProjectPathKind.Custom => L10n.Tr("L.Kind_Custom"),
        ProjectPathKind.AppDir => L10n.Tr("L.Kind_AppDir"),
        ProjectPathKind.GameDataCalienteTools => L10n.Tr("L.Kind_GameDataCalienteTools"),
        ProjectPathKind.GameDataTools => L10n.Tr("L.Kind_GameDataTools"),
        _ => L10n.Tr("L.Kind_Fallback"),
    };

    private ObservableCollection<WriteModeItem> BuildWriteModes() => new(
    [
        new WriteModeItem(WriteMode.Auto, L10n.Tr("L.Wm_Auto")),
        new WriteModeItem(WriteMode.BodySlideDir, L10n.Tr("L.Wm_BsDir")),
        new WriteModeItem(WriteMode.Mo2Mod, L10n.Tr("L.Wm_Mo2Mod")),
        new WriteModeItem(WriteMode.RealGameData, L10n.Tr("L.Wm_GameData")),
        new WriteModeItem(WriteMode.Custom, L10n.Tr("L.Wm_Custom")),
    ]);

    /// <summary>语言切换后重算所有由代码拼出的绑定串。</summary>
    public void OnLanguageChanged()
    {
        _localizing = true;
        var currentMode = SelectedWriteMode?.Mode ?? Settings.WriteMode;
        WriteModes = BuildWriteModes();
        SelectedWriteMode = WriteModes.FirstOrDefault(m => m.Mode == currentMode);
        _localizing = false;

        if (Resolution is not null)
        {
            var game = string.IsNullOrEmpty(SelectedInstance?.GameName) ? L10n.Tr("L.Word_Unknown") : SelectedInstance!.GameName;
            InfoLine = L10n.TrF("L.Info_Line", game, Resolution.EffectivePath, KindText(Resolution.Kind));
        }
        else
        {
            InfoLine = L10n.Tr("L.Vm_NotScanned");
        }
        LogWriteTarget();
        OnPropertyChanged(nameof(LogText));
        UpdateMembershipMarks(); // 树节点文本也是代码拼的（✔ / 同名冲突 / [组内 x/y]），一并换语言
        UpdateCounts();
        UpdateGroupInfo();
        UpdateTitle();
    }

    public void OnViewClosed()
    {
        _closed = true;
        Settings.Save();
    }

    public void Log(string message)
    {
        if (_closed)
            return;
        _logBuffer.Append($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        if (_logBuffer.Length > 128 * 1024)
        {
            var text = _logBuffer.ToString();
            _logBuffer.Clear();
            _logBuffer.Append(text[^65536..]);
        }
        LogText = _logBuffer.ToString();
    }

    partial void OnSelectedWriteModeChanged(WriteModeItem? value)
    {
        if (value is null || _localizing)
            return;
        Settings.WriteMode = value.Mode;
        Settings.Save();
        // 视图未挂接目录选择器时（构造阶段）不弹框
        if (value.Mode == WriteMode.Custom && string.IsNullOrWhiteSpace(Settings.CustomTargetDir) && FolderPicker is not null)
            BrowseForTarget();
        else
            LogWriteTarget();
    }

    /// <summary>视图弹目录框后回填。</summary>
    public void SetCustomTargetDir(string? dir)
    {
        Settings.CustomTargetDir = dir;
        Settings.Save();
        LogWriteTarget();
    }

    // ── MO2 实例链 ──
    [RelayCommand]
    private void RefreshInstances() => ReloadInstances();

    private void ReloadInstances()
    {
        var found = Mo2Discovery.Discover(Settings.ExtraMo2Dirs);
        Instances = new ObservableCollection<Mo2Instance>(found);
        var selected = found.FirstOrDefault(i => i.InstanceDir == Settings.LastInstanceDir) ?? found.FirstOrDefault();
        SelectedInstance = selected;
        Log(L10n.TrF("L.Log_InstancesFound", found.Count));
        foreach (var inst in found.Take(3))
            Log(L10n.TrF("L.Log_Instance", inst.DisplayName, inst.InstanceDir));
    }

    partial void OnSelectedInstanceChanged(Mo2Instance? value)
    {
        if (value is null)
        {
            Profiles = [];
            return;
        }
        Settings.LastInstanceDir = value.InstanceDir;
        Settings.Save();
        var profiles = value.GetProfiles();
        Profiles = new ObservableCollection<string>(profiles);
        // List<string>.Contains 只接受非空 string；LastProfile 是 string?，须先判空
        //（原写法 profiles.Contains(null) 恒为 false，行为不变，但会报 CS8604）
        var last = Settings.LastProfile;
        var preferred = last is not null && profiles.Contains(last) ? last : profiles.FirstOrDefault();
        SelectedProfile = preferred;
    }

    partial void OnSelectedProfileChanged(string? value)
    {
        if (value is null || SelectedInstance is null)
            return;
        Settings.LastProfile = value;
        Settings.Save();
        LoadProfileMods(value);
    }

    private void LoadProfileMods(string profile)
    {
        var instance = SelectedInstance;
        if (instance is null)
            return;
        try
        {
            Entries = ModListParser.Parse(instance.GetModListPath(profile));
            Mods = ModListParser.GetEnabledModDirectories(Entries, instance.ModsDirectory);
            Log(L10n.TrF("L.Log_ProfileMods", profile, Mods.Count));
            _ = DetectBodySlideAsync();
        }
        catch (Exception ex)
        {
            Log(L10n.TrF("L.Log_ModlistFail", ex.Message));
        }
    }

    // ── BodySlide 链 ──
    private async Task DetectBodySlideAsync()
    {
        IsDetecting = true;
        ScanStatusText = L10n.Tr("L.Scan_Detecting");
        ScanProgressDetail = "";
        try
        {
            await DetectBodySlideCoreAsync();
        }
        finally
        {
            IsDetecting = false;
        }
    }

    [RelayCommand]
    private async Task DetectBodySlideCoreAsync()
    {
        var modsSnapshot = Mods;
        var gamePath = SelectedInstance?.GamePath;
        var previous = SelectedBodySlide?.AppDir;
        var last = Settings.LastBodySlideDir;
        var candidates = await Task.Run(() => BodySlideLocator.FindCandidates(modsSnapshot, gamePath, previous));
        BodySlideDirs = new ObservableCollection<BodySlideCandidate>(candidates);
        var pick = candidates.FirstOrDefault(c => c.AppDir == previous)
                   ?? candidates.FirstOrDefault(c => c.AppDir == last)
                   ?? candidates.FirstOrDefault();
        SelectedBodySlide = pick;
        if (pick is not null)
            Settings.LastBodySlideDir = pick.AppDir;
    }

    partial void OnSelectedBodySlideChanged(BodySlideCandidate? value)
    {
        _bsAppDir = value?.AppDir;
        if (value is not null)
        {
            Settings.LastBodySlideDir = value.AppDir;
            Settings.Save();
        }
        _ = RunScanAsync();
    }

    // ── 扫描 ──
    private sealed record ScanOutcome(
        ProjectPathResolution? Resolution,
        ScanResult? Result,
        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,
        List<string> Errors);

    [RelayCommand]
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
            Resolution = null;
            Scan = null;
            InfoLine = L10n.Tr("L.Vm_NotScanned");
            RebuildTree();
            return;
        }

        _scanning = true;
        IsScanning = true;
        ScanStatusText = L10n.Tr("L.Scan_Preparing");
        ScanProgressDetail = "";
        ScanProgressValue = 0;
        ScanOutcome? outcome = null;
        try
        {
            var modsSnapshot = Mods;
            var instanceSnapshot = SelectedInstance;
            var writeMode = Settings.WriteMode;
            var customDir = Settings.CustomTargetDir;
            var progress = new Progress<ScanProgress>(OnScanProgress);
            outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customDir, progress));
            ScanStatusText = L10n.Tr("L.Scan_Render");
            ScanProgressValue = 100;
            ScanProgressDetail = "";
            ApplyScanOutcome(outcome);
        }
        catch (Exception ex)
        {
            Log(L10n.TrF("L.Log_ScanFail", ex));
        }
        finally
        {
            _scanning = false;
            IsScanning = false;
            if (_scanQueued)
            {
                _scanQueued = false;
                _ = RunScanAsync();
            }
        }

        // 新模组弹窗是模态的：放在遮罩收起之后弹，否则弹窗开着时遮罩会一直挂在底下
        if (outcome is not null && !_closed)
            CheckNewModsAfterScan();
    }

    /// <summary>扫描进度回调（后台线程经 Progress 同步回 UI 线程）。
    /// 枚举目录远快于解析文件，进度条按 0–20% / 20–100% 加权，避免先冲顶又归零。</summary>
    private void OnScanProgress(ScanProgress p)
    {
        if (p.Phase == ScanPhase.Enumerating)
        {
            ScanProgressValue = p.Total == 0 ? 0 : 20.0 * p.Current / p.Total;
            ScanStatusText = L10n.TrF("L.Scan_Enum", p.Current, p.Total);
            ScanProgressDetail = p.FilesFound > 0 ? L10n.TrF("L.Scan_Files", p.FilesFound) : "";
        }
        else
        {
            ScanProgressValue = p.Total == 0 ? 100 : 20.0 + 80.0 * p.Current / p.Total;
            ScanStatusText = L10n.TrF("L.Scan_Parse", p.Current, p.Total);
        }
    }

    private static ScanOutcome ComputeScan(
        string bsDir, List<(ModEntry Entry, string Dir)> mods, Mo2Instance? instance,
        WriteMode writeMode, string? customDir, IProgress<ScanProgress>? progress)
    {
        var errors = new List<string>();
        var configPath = Path.Combine(bsDir, "Config.xml");
        if (!File.Exists(configPath))
            return new ScanOutcome(null, null, [], null, "", [L10n.TrF("L.Log_ConfigMissing", configPath)]);

        var config = new BodySlideConfig(configPath);
        var resolution = BodySlideLocator.ResolveProjectPath(config, bsDir, mods, instance?.GamePath);
        var scan = SliderSetScanner.Scan(resolution, mods, progress);
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
                // 还没有清单 = 输出目录可能来自 WinForms 版：尝试读入它写出的单文件分组，
                // 让老用户已有的组能带进新版继续编辑（保存后该文件会被清理）
                var legacy = Path.Combine(targetDir, SliderGroupFile.DefaultFileName);
                if (File.Exists(legacy))
                    filesToLoad.Add(legacy);
            }

            foreach (var file in filesToLoad)
            {
                if (!SliderGroupFile.TryLoad(file, out var groups, out var error))
                    errors.Add(L10n.TrF("L.Log_ExistingLoadFail", Path.GetFileName(file), error));
                else
                    SliderGroupFile.Merge(existing, groups, out _, out _);
            }
        }

        return new ScanOutcome(resolution, scan, existing, target?.Dir, target?.Description ?? "", errors);
    }

    private void ApplyScanOutcome(ScanOutcome outcome)
    {
        if (_closed)
            return;
        foreach (var error in outcome.Errors)
            Log(L10n.TrF("L.Log_Error", error));

        Resolution = outcome.Resolution;
        Scan = outcome.Result;

        if (outcome.Resolution is null)
        {
            InfoLine = L10n.Tr("L.Info_Unresolved");
            RebuildTree();
            return;
        }

        var kindText = KindText(outcome.Resolution.Kind);
        var game = string.IsNullOrEmpty(SelectedInstance?.GameName) ? L10n.Tr("L.Word_Unknown") : SelectedInstance!.GameName;
        InfoLine = L10n.TrF("L.Info_Line", game, outcome.Resolution.EffectivePath, kindText);
        Log(L10n.TrF("L.Log_EffectivePath", outcome.Resolution.EffectivePath, kindText));
        foreach (var note in outcome.Result?.LayerNotes ?? [])
            Log($"  {note}");

        if (Scan is { } scan)
        {
            Log(L10n.TrF("L.Log_ScanDone", scan.WinnerFileCount, scan.Outfits.Count, scan.Warnings.Count));
            foreach (var warning in scan.Warnings.Take(20))
                Log(L10n.TrF("L.Log_Warning", warning));
        }

        // 只在内存中还没有组时才载入上次写出的文件，避免覆盖未保存的修改
        if (Store.Count == 0 && !Store.Dirty && outcome.ExistingGroups.Count > 0)
        {
            Store.Load(outcome.ExistingGroups);
            Log(L10n.TrF("L.Log_LoadedGroups", outcome.TargetDir, Store.Count));
        }

        RefreshGroupsList();
        RefreshTree();
        LogWriteTarget();
    }

    private bool IsVirtualScan =>
        Resolution is not null
        && Resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools
        && Entries.Count > 0;

    public Dictionary<string, string> OwnerByOutfit()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, owner, outfits) in GetTreeDisplayStructure())
            foreach (var outfit in outfits)
                map.TryAdd(outfit, owner);
        return map;
    }

    private static string OwnerNameOf(Dictionary<string, string> ownerByOutfit, string outfit) =>
        ownerByOutfit.TryGetValue(outfit, out var owner) ? owner : "";

    /// <summary>按树结构返回 分隔符 → 模组 → 服装名（供弹窗使用）。</summary>
    public List<(string? Separator, string Owner, List<string> Outfits)> GetTreeDisplayStructure()
    {
        var result = new List<(string?, string, List<string>)>();
        if (Scan is null)
            return result;

        var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
        foreach (var outfit in Scan.Outfits)
        {
            if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                outfitsByOwner[outfit.OwnerLabel] = list = [];
            list.Add(outfit);
        }

        if (IsVirtualScan)
        {
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            string? separator = null;
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if (entry.IsForeign)
                    continue;

                if (entry.IsSeparator)
                {
                    separator = entry.Name;
                    continue;
                }

                var onDisk = SelectedInstance is not null &&
                             Directory.Exists(System.IO.Path.Combine(SelectedInstance.ModsDirectory, entry.Name));
                outfitsByOwner.TryGetValue(entry.Name, out var outfits);
                outfits ??= [];
                if (outfits.Count > 0)
                    consumed.Add(entry.Name);

                if (!entry.Enabled || !onDisk)
                    continue;
                result.Add((SeparatorTitle(separator), entry.Name, outfits.Select(o => o.Name).ToList()));
            }

            foreach (var (owner, outfits) in outfitsByOwner)
            {
                if (consumed.Contains(owner))
                    continue;
                result.Add((null, owner, outfits.Select(o => o.Name).ToList()));
            }
        }
        else
        {
            foreach (var (owner, outfits) in outfitsByOwner)
                result.Add((null, owner, outfits.Select(o => o.Name).ToList()));
        }

        return result;
    }

    private static string? SeparatorTitle(string? name) => name is null
        ? null
        : name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase)
            ? name[..^"_separator".Length]
            : name;

    public string SeparatorDisplayName(string name) =>
        name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase)
            ? name[..^"_separator".Length]
            : name;

    private bool IsInAnyGroup(string outfit) => Store.IsInAnyGroup(outfit);
}
