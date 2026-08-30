using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

public class MainForm : Form
{
    // ── 控件 ─────────────────────────────────────────────────────────────
    private readonly MenuStrip _menu = new();
    private readonly ComboBox _cboInstances = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboProfiles = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboBodySlide = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboWriteMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly Button _btnRefreshMo2 = new() { Text = "刷新", AutoSize = true };
    private readonly Button _btnAddMo2 = new() { Text = "添加 MO2 目录…", AutoSize = true };
    private readonly Button _btnDetectBs = new() { Text = "自动检测", AutoSize = true };
    private readonly Button _btnBrowseBs = new() { Text = "浏览…", AutoSize = true };
    private readonly Label _lblInfo = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _txtFilter = new() { Width = 180, PlaceholderText = "过滤服装 / 模组…" };
    private readonly CheckBox _chkUnassigned = new() { Text = "仅看未分配", AutoSize = true };
    private readonly TreeView _tree = new()
    {
        CheckBoxes = true,
        HideSelection = false,
        BorderStyle = BorderStyle.FixedSingle,
        ShowLines = false,
        FullRowSelect = true,
        ItemHeight = 22,
    };
    private readonly ListBox _lstGroups = new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        IntegralHeight = false,
        ItemHeight = 22,
    };
    private readonly Button _btnNewGroup = new() { Text = "新建组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnRenameGroup = new() { Text = "重命名", MinimumSize = new Size(84, 30) };
    private readonly Button _btnDeleteGroup = new() { Text = "删除组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnImport = new() { Text = "导入现有组文件…", MinimumSize = new Size(160, 30) };
    private readonly Button _btnViewMembers = new() { Text = "查看组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnRules = new() { Text = "规则归组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnUndo = new() { Text = "撤销", MinimumSize = new Size(100, 32) };
    private readonly Stack<List<SliderGroup>> _undoStack = new();
    private readonly Button _btnAddToGroup = new() { Text = "加入当前组", MinimumSize = new Size(150, 32) };
    private readonly Button _btnRemoveFromGroup = new() { Text = "移出当前组", MinimumSize = new Size(120, 32) };
    private readonly Label _lblGroupInfo = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly Button _btnGenerate = new()
    {
        Text = "保存分组文件",
        MinimumSize = new Size(150, 32),
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
    };
    private readonly Button _btnBrowseTarget = new() { Text = "浏览…", MinimumSize = new Size(76, 32) };
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusCounts = new()
    {
        Spring = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly ToolStripStatusLabel _statusTarget = new() { TextAlign = ContentAlignment.MiddleRight };
    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9F),
        BackColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly TableLayoutPanel _topPanel = new();
    private readonly SplitContainer _mainSplit = new();

    // ── 状态 ─────────────────────────────────────────────────────────────
    private readonly AppSettings _settings = AppSettings.Load();
    private List<Mo2Instance> _instances = new();
    private Mo2Instance? _instance;
    private string? _profile;
    private List<(ModEntry Entry, string Dir)> _mods = new();
    private List<ModEntry> _entries = new();
    private string? _bsAppDir;
    private ProjectPathResolution? _resolution;
    private ScanResult? _scan;
    private HashSet<string>? _conflictNames;
    private bool _scanQueued;
    private bool _closed;
    private readonly System.Windows.Forms.Timer _filterDebounce = new() { Interval = 350 };
    private readonly List<SliderGroup> _groups = new();
    private bool _dirty;
    private bool _updatingChecks;
    private bool _scanning;
    private bool _loadingUi;

    private static readonly Color MemberGreen = Color.FromArgb(0, 128, 0);
    private static readonly Font BoldFont = new("Microsoft YaHei UI", 9F, FontStyle.Bold);

    private const string DedicatedModName = "BS Group Generator";

    public MainForm()
    {
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }
        Text = "BS Group Generator — BodySlide 分组生成工具";
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1160, 780);
        MinimumSize = new Size(1000, 660);

        BuildMenu();
        BuildTopPanel();
        BuildMainSplit();
        BuildGenerateBar();
        BuildLogPanel();
        BuildStatusBar();

        // 后加入的控件先停靠：菜单最上、顶栏其次；底部依次为状态栏、日志、生成栏；中间填充
        Controls.Add(_mainSplit);
        Controls.Add(_generateBar);
        Controls.Add(_logPanel);
        Controls.Add(_statusStrip);
        Controls.Add(_topPanel);
        Controls.Add(_menu);

        Load += (_, _) => ReloadInstances();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F1)
            {
                e.Handled = true;
                new HelpDialog().ShowDialog(this);
            }
        };
        FormClosing += OnFormClosingGuard;

        _cboInstances.SelectedIndexChanged += (_, _) => OnInstanceChanged();
        _cboProfiles.SelectedIndexChanged += (_, _) => OnProfileChanged();
        _cboBodySlide.SelectedIndexChanged += (_, _) => OnBodySlideChanged();
        _cboWriteMode.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingUi) return;
            if (_cboWriteMode.SelectedIndex >= 0)
                _settings.WriteMode = (WriteMode)_cboWriteMode.SelectedIndex;
            if (_settings.WriteMode == WriteMode.Custom && string.IsNullOrEmpty(_settings.CustomTargetDir) && Visible)
                BrowseForTarget();
            LogWriteTarget();
        };
        _btnBrowseTarget.Click += (_, _) => BrowseForTarget();
        _btnRefreshMo2.Click += (_, _) => ReloadInstances();
        _btnAddMo2.Click += OnAddMo2;
        _btnDetectBs.Click += async (_, _) => await DetectBodySlideAsync();
        _btnBrowseBs.Click += OnBrowseBodySlide;
        _btnNewGroup.Click += OnNewGroup;
        _btnRenameGroup.Click += OnRenameGroup;
        _btnDeleteGroup.Click += OnDeleteGroup;
        _btnImport.Click += OnImportGroups;
        _btnViewMembers.Click += OnViewMembers;
        _btnRules.Click += OnRules;
        _btnUndo.Click += (_, _) => Undo();
        _lstGroups.DoubleClick += (_, _) => { if (!_loadingUi) OnViewMembers(_lstGroups, EventArgs.Empty); };
        _btnGenerate.Click += OnGenerate;
        _tree.AfterCheck += OnTreeAfterCheck;
        _tree.BeforeCheck += OnTreeBeforeCheck;
        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) UpdateMembershipMarks(); };
        _btnAddToGroup.Click += (_, _) => ApplyCheckedToCurrentGroup(add: true);
        _btnRemoveFromGroup.Click += (_, _) => ApplyCheckedToCurrentGroup(add: false);
        _txtFilter.TextChanged += (_, _) => { _filterDebounce.Stop(); _filterDebounce.Start(); };
        _filterDebounce.Tick += (_, _) => { _filterDebounce.Stop(); RefreshTree(); };
        _chkUnassigned.CheckedChanged += (_, _) => RefreshTree();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // SplitContainer 只有在拿到实际宽度后才能可靠设置分割位置；左右各占一半
        try { _mainSplit.SplitterDistance = Math.Max(200, (_mainSplit.Width - _mainSplit.SplitterWidth) / 2); } catch { }
    }

    // ── 界面搭建 ─────────────────────────────────────────────────────────
    private void BuildMenu()
    {
        var mnuFile = new ToolStripMenuItem("文件(&F)");
        var mnuSave = new ToolStripMenuItem("保存分组文件(&S)") { ShortcutKeys = Keys.Control | Keys.S };
        mnuSave.Click += OnGenerate;
        var mnuExit = new ToolStripMenuItem("退出(&X)") { ShortcutKeys = Keys.Alt | Keys.F4 };
        mnuExit.Click += (_, _) => Close();
        var mnuUndo = new ToolStripMenuItem("撤销分组修改(&Z)") { ShortcutKeys = Keys.Control | Keys.Z };
        mnuUndo.Click += (_, _) => Undo();
        mnuFile.DropDownItems.Add(mnuUndo);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add(mnuSave);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add(mnuExit);

        var mnuTools = new ToolStripMenuItem("工具(&T)");
        var mnuDiag = new ToolStripMenuItem("诊断信息(&D)");
        mnuDiag.Click += (_, _) => ShowDiagnostics();
        mnuTools.DropDownItems.Add(mnuDiag);

        var mnuHelp = new ToolStripMenuItem("帮助(&H)");
        var mnuUsage = new ToolStripMenuItem("使用说明(&U)");
        mnuUsage.Click += (_, _) => new HelpDialog().ShowDialog(this);
        var mnuAbout = new ToolStripMenuItem("关于(&A)");
        mnuAbout.Click += (_, _) => new AboutDialog().ShowDialog(this);
        mnuHelp.DropDownItems.Add(mnuUsage);
        mnuHelp.DropDownItems.Add(mnuAbout);

        _menu.Items.AddRange([mnuFile, mnuTools, mnuHelp]);
    }

    private void BuildTopPanel()
    {
        _topPanel.Dock = DockStyle.Top;
        _topPanel.AutoSize = true;
        _topPanel.ColumnCount = 4;
        _topPanel.RowCount = 5;
        _topPanel.Padding = new Padding(12, 8, 12, 6);
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));   // 标签：按最宽文本自适应，右对齐
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++)
            _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddRow(0, "MO2 实例", _cboInstances, _btnRefreshMo2, _btnAddMo2);
        AddRow(1, "配置 Profile", _cboProfiles);
        AddRow(2, "BodySlide", _cboBodySlide, _btnDetectBs, _btnBrowseBs);

        _cboWriteMode.Items.AddRange(["自动（推荐）", "BodySlide 程序目录", "MO2 专用模组", "游戏真实 Data", "自定义（浏览选择）"]);
        _cboWriteMode.SelectedIndex = (int)_settings.WriteMode is >= 0 and < 5 ? (int)_settings.WriteMode : 0;
        AddRow(3, "输出位置", _cboWriteMode, _btnBrowseTarget);

        _lblInfo.Text = "尚未扫描。";
        _lblInfo.Margin = new Padding(3, 6, 3, 2);
        _topPanel.Controls.Add(_lblInfo, 0, 4);
        _topPanel.SetColumnSpan(_lblInfo, 4);
    }

    private void AddRow(int row, string labelText, Control fill, params Control[] buttons)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 6, 8, 6),
        };
        _topPanel.Controls.Add(label, 0, row);
        fill.Dock = DockStyle.Fill;
        fill.Margin = new Padding(0, 4, 6, 4);
        _topPanel.Controls.Add(fill, 1, row);
        var col = 2;
        foreach (var button in buttons)
        {
            button.Margin = new Padding(0, 4, 6, 4);
            button.Anchor = AnchorStyles.Left;
            _topPanel.Controls.Add(button, col, row);
            col++;
        }
    }

    private void BuildMainSplit()
    {
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.None; // 缩放窗口时左右按比例保持均等

        // 左：过滤栏 + 模组树
        var leftTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(2, 8, 2, 4),
            WrapContents = false,
        };
        _txtFilter.Margin = new Padding(2, 2, 10, 2);
        _chkUnassigned.Margin = new Padding(2, 6, 2, 2);
        leftTop.Controls.AddRange([_txtFilter, _chkUnassigned]);

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_tree);
        left.Controls.Add(leftTop);
        _tree.Dock = DockStyle.Fill;

        // 右：分组面板
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 2, 2, 2) };
        var groupHeader = new Label
        {
            Text = "分组（选中组 → 左侧勾选 → 点「加入当前组」）",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(0),
        };
        var applyButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(0, 4, 0, 2),
            WrapContents = false,
        };
        _btnAddToGroup.Margin = new Padding(0, 2, 8, 2);
        _btnRemoveFromGroup.Margin = new Padding(0, 2, 8, 2);
        _btnUndo.Margin = new Padding(0, 2, 0, 2);
        applyButtons.Controls.AddRange([_btnAddToGroup, _btnRemoveFromGroup, _btnUndo]);
        var groupButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(0, 4, 0, 4),
            ColumnCount = 5,
        };
        for (var i = 0; i < 5; i++)
            groupButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        foreach (var b in new[] { _btnNewGroup, _btnRenameGroup, _btnDeleteGroup, _btnViewMembers, _btnRules })
        {
            b.Margin = new Padding(0, 2, 8, 2);
            b.Dock = DockStyle.Fill;
        }
        groupButtons.Controls.Add(_btnNewGroup, 0, 0);
        groupButtons.Controls.Add(_btnRenameGroup, 1, 0);
        groupButtons.Controls.Add(_btnDeleteGroup, 2, 0);
        groupButtons.Controls.Add(_btnViewMembers, 3, 0);
        groupButtons.Controls.Add(_btnRules, 4, 0);

        var groupBottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            Padding = new Padding(0, 4, 0, 4),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        _btnImport.Margin = new Padding(0, 2, 0, 8);
        _lblGroupInfo.Margin = new Padding(0, 0, 0, 0);
        groupBottom.Controls.AddRange([_btnImport, _lblGroupInfo]);
        _lstGroups.Dock = DockStyle.Fill;

        // 加载顺序决定停靠次序（后加先停）
        right.Controls.Add(_lstGroups);
        right.Controls.Add(groupButtons);
        right.Controls.Add(applyButtons);
        right.Controls.Add(groupBottom);
        right.Controls.Add(groupHeader);

        _mainSplit.Panel1.Controls.Add(left);
        _mainSplit.Panel2.Controls.Add(right);
    }

    private readonly Panel _generateBar = new() { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(12, 8, 12, 8) };

    private void BuildGenerateBar()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _btnGenerate.Anchor = AnchorStyles.Right;
        _btnGenerate.Margin = new Padding(0);
        table.Controls.Add(new Panel(), 0, 0);
        table.Controls.Add(_btnGenerate, 1, 0);
        _generateBar.Controls.Add(table);
    }

    private readonly Panel _logPanel = new() { Dock = DockStyle.Bottom, Height = 104, Padding = new Padding(12, 0, 12, 8) };

    private void BuildLogPanel()
    {
        _logPanel.Controls.Add(_txtLog);
    }

    private void BuildStatusBar()
    {
        _statusStrip.SizingGrip = false;
        _statusTarget.ToolTipText = "";
        _statusStrip.Items.AddRange([_statusCounts, _statusTarget]);
    }

    // ── 数据加载 ─────────────────────────────────────────────────────────
    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        _filterDebounce.Stop();
        if (_dirty)
        {
            var choice = MessageBox.Show(this,
                "当前有未保存的分组修改，直接退出会丢失。\n\n是：保存并退出\n否：不保存，直接退出\n取消：留在程序",
                "未保存的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (choice == DialogResult.Yes && !TrySaveGroups(showSuccessDialog: false))
            {
                e.Cancel = true; // 保存失败（如写入出错），留在程序处理
                return;
            }
        }
        _closed = true;
        _settings.Save();
    }

    private void Log(string message)
    {
        if (_closed || _txtLog.IsDisposed)
            return;
        if (_txtLog.TextLength > 131072)
            _txtLog.Text = _txtLog.Text[65536..];
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void ReloadInstances()
    {
        var empty = false;
        _loadingUi = true;
        try
        {
            _instances = Mo2Discovery.Discover(_settings.ExtraMo2Dirs);
            _cboInstances.DataSource = null;
            _cboInstances.DataSource = _instances;
            _cboInstances.DisplayMember = nameof(Mo2Instance.DisplayName);

            Log($"发现 {_instances.Count} 个 MO2 实例。");
            if (_instances.Count == 0)
            {
                Log("未找到实例：可用\"添加 MO2 目录\"选择含 ModOrganizer.ini 的实例目录或便携安装目录。");
                empty = true;
            }
            else
            {
                var last = _instances.FirstOrDefault(i =>
                    string.Equals(i.InstanceDir, _settings.LastInstanceDir, StringComparison.OrdinalIgnoreCase));
                _cboInstances.SelectedItem = last ?? _instances[0];
            }
        }
        finally
        {
            _loadingUi = false;
        }

        if (empty)
            OnInstanceChangedCore();
        else
            OnInstanceChanged();
    }

    private void OnInstanceChanged()
    {
        if (_loadingUi) return;
        OnInstanceChangedCore();
    }

    private void OnInstanceChangedCore()
    {
        _loadingUi = true;
        try
        {
            _instance = _cboInstances.SelectedItem as Mo2Instance;
            _profile = null;
            _cboProfiles.DataSource = null;

            if (_instance is null)
            {
                _lblInfo.Text = "未选择 MO2 实例（也可直接手动指定 BodySlide 目录）";
                _mods = new List<(ModEntry, string)>();
                _entries = new List<ModEntry>();
            }
            else
            {
                _settings.LastInstanceDir = _instance.InstanceDir;
                _lblInfo.Text = $"游戏：{(string.IsNullOrEmpty(_instance.GameName) ? "未知" : _instance.GameName)}";
                var profiles = _instance.GetProfiles();
                Log($"实例 {_instance.Name}：mods = {_instance.ModsDirectory}");
                if (profiles.Count == 0)
                    Log("警告：该实例没有可用的 profile（目录下缺少 modlist.txt）。");
                _cboProfiles.DataSource = profiles;

                var target = _instance.SelectedProfile is { Length: > 0 } sp && profiles.Contains(sp)
                    ? sp
                    : _settings.LastProfile is { Length: > 0 } lp && profiles.Contains(lp)
                        ? lp
                        : profiles.FirstOrDefault();
                _cboProfiles.SelectedItem = target;
            }
        }
        finally
        {
            _loadingUi = false;
        }
        OnProfileChanged();
    }

    private void OnProfileChanged()
    {
        if (_loadingUi) return;
        _profile = _cboProfiles.SelectedItem as string;
        _settings.LastProfile = _profile;
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

    private async Task DetectBodySlideAsync()
    {
        _loadingUi = true;
        List<BodySlideCandidate> candidates;
        try
        {
            var previous = _bsAppDir;
            var modsSnapshot = _mods;
            var gamePath = _instance?.GamePath;
            candidates = await Task.Run(() => BodySlideLocator.FindCandidates(modsSnapshot, gamePath, previous));
            _cboBodySlide.DataSource = null;
            _cboBodySlide.DataSource = candidates;

            var selected = previous is not null
                ? candidates.FirstOrDefault(c => string.Equals(c.AppDir, previous, StringComparison.OrdinalIgnoreCase))
                : null;
            selected ??= candidates.FirstOrDefault(c =>
                string.Equals(c.AppDir, _settings.LastBodySlideDir, StringComparison.OrdinalIgnoreCase));
            selected ??= candidates.FirstOrDefault();
            _cboBodySlide.SelectedItem = selected;

            if (candidates.Count == 0)
            {
                _bsAppDir = null;
                Log("未自动找到 BodySlide：请点\"浏览…\"选择含 BodySlide.exe 和 Config.xml 的目录（通常是 MO2 里 BodySlide 模组所在的目录）。");
            }
        }
        catch (Exception ex)
        {
            Log($"查找 BodySlide 失败：{ex.Message}");
        }
        finally
        {
            _loadingUi = false;
        }
        OnBodySlideChanged();
    }

    private void OnBrowseBodySlide(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择含 BodySlide.exe（或 BodySlide x64.exe）和 Config.xml 的目录",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var dir = dialog.SelectedPath;
        if (!File.Exists(Path.Combine(dir, "Config.xml")))
        {
            MessageBox.Show(this, "所选目录下没有 Config.xml，无法作为 BodySlide 目录。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _loadingUi = true;
        try
        {
            var candidates = _cboBodySlide.DataSource as List<BodySlideCandidate> ?? new List<BodySlideCandidate>();
            candidates.RemoveAll(c => string.Equals(c.AppDir, dir, StringComparison.OrdinalIgnoreCase));
            var candidate = new BodySlideCandidate(
                dir,
                SafeFindExe(dir),
                "手动选择");
            candidates.Insert(0, candidate);
            _cboBodySlide.DataSource = null;
            _cboBodySlide.DataSource = candidates;
            _cboBodySlide.SelectedItem = candidate;
        }
        finally
        {
            _loadingUi = false;
        }
        OnBodySlideChanged();
    }

    private static string SafeFindExe(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "BodySlide*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private void OnBodySlideChanged()
    {
        if (_loadingUi) return;
        _bsAppDir = (_cboBodySlide.SelectedItem as BodySlideCandidate)?.AppDir;
        if (_bsAppDir is not null)
            _settings.LastBodySlideDir = _bsAppDir;
        _ = RunScanAsync();
    }

    // ── 扫描 ─────────────────────────────────────────────────────────────
    private sealed record ScanOutcome(
        ProjectPathResolution? Resolution,
        ScanResult? Result,
        List<SliderGroup> ExistingGroups,
        string? TargetDir,
        string TargetDescription,
        List<string> Errors);

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
            RefreshTree();
            return;
        }

        _scanning = true;
        try
        {
            var modsSnapshot = _mods.ToList();
            var instanceSnapshot = _instance;
            var writeMode = _settings.WriteMode;
            var customDir = _settings.CustomTargetDir;
            var outcome = await Task.Run(() => ComputeScan(bsDir, modsSnapshot, instanceSnapshot, writeMode, customDir));
            ApplyScanOutcome(outcome);
        }
        catch (Exception ex)
        {
            Log($"扫描失败：{ex}");
        }
        finally
        {
            _scanning = false;
            if (_scanQueued)
            {
                _scanQueued = false;
                _ = RunScanAsync();
            }
        }
    }

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
        if (_closed || IsDisposed)
            return;
        foreach (var error in outcome.Errors)
            Log($"错误：{error}");

        _resolution = outcome.Resolution;
        _scan = outcome.Result;
        _conflictNames = outcome.Result is null
            ? null
            : outcome.Result.Outfits.Where(o => o.HasConflict).Select(o => o.Name).ToHashSet(StringComparer.Ordinal);

        if (outcome.Resolution is null)
        {
            _lblInfo.Text = "有效项目路径：无法解析（见日志）";
            RefreshTree();
            return;
        }

        var kindText = BodySlideLocator.DescribeKind(outcome.Resolution.Kind);
        var game = string.IsNullOrEmpty(_instance?.GameName) ? "未知" : _instance!.GameName;
        _lblInfo.Text = $"游戏：{game}　·　有效项目路径：{outcome.Resolution.EffectivePath}（{kindText}）";
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
        }

        _loadingUi = true;
        try
        {
            RefreshGroupsList();
            _lstGroups.SelectedIndex = _groups.Count > 0 ? 0 : -1;
        }
        finally
        {
            _loadingUi = false;
        }
        RefreshTree();
        LogWriteTarget();
    }

    // ── 模组树 ───────────────────────────────────────────────────────────
    private bool IsInAnyGroup(string outfit)
    {
        foreach (var group in _groups)
        {
            if (group.Members.Contains(outfit, StringComparer.Ordinal))
                return true;
        }
        return false;
    }

    private void RefreshTree()
    {
        if (_closed || IsDisposed)
            return;
        _updatingChecks = true;
        _tree.BeginUpdate();
        HashSet<string> expanded = new();
        string? topChain = null;
        try
        {
            // 记录展开状态与滚动位置，重建后恢复
            foreach (var node in Walk(_tree.Nodes))
                if (node.IsExpanded)
                    expanded.Add(Chain(node));
            if (_tree.TopNode is { } top)
                topChain = Chain(top);

            _tree.Nodes.Clear();
            if (_scan is null)
            {
                UpdateCounts();
                return;
            }

            var filter = _txtFilter.Text.Trim();
            var group = CurrentGroup;
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
            foreach (var outfit in _scan.Outfits)
            {
                if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                    outfitsByOwner[outfit.OwnerLabel] = list = new List<OutfitEntry>();
                list.Add(outfit);
            }

            if (IsVirtualScan)
                BuildStructuredTree(outfitsByOwner, filter, group);
            else
                BuildFlatTree(outfitsByOwner, filter, group);

            if (_tree.Nodes.Count == 0)
                _tree.Nodes.Add(new TreeNode("（没有符合过滤条件的模组）") { ForeColor = SystemColors.GrayText });

            foreach (var node in Walk(_tree.Nodes))
                if (expanded.Contains(Chain(node)))
                    node.Expand();
        }
        finally
        {
            _tree.EndUpdate();
            _updatingChecks = false;
        }

        if (topChain is not null)
        {
            foreach (var node in Walk(_tree.Nodes))
            {
                if (Chain(node) != topChain)
                    continue;
                try { _tree.TopNode = node; } catch { /* 结构变化时无法恢复则忽略 */ }
                break;
            }
        }
        UpdateCounts();
    }

    /// <summary>服装来自虚拟 Data 汇聚点时，可以按 MO2 的模组结构展示。</summary>
    private bool IsVirtualScan =>
        _resolution is not null
        && _resolution.Kind is ProjectPathKind.GameDataCalienteTools or ProjectPathKind.GameDataTools
        && _entries.Count > 0;

    /// <summary>单目录模式（服装全部在 BodySlide 目录内）：按来源平铺。</summary>
    private void BuildFlatTree(Dictionary<string, List<OutfitEntry>> outfitsByOwner, string filter, SliderGroup? group)
    {
        foreach (var (owner, outfits) in outfitsByOwner)
        {
            if (!ModVisible(outfits.Count, outfits.Count(o => OutfitVisible(o, owner)), filter))
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, outfits.Where(o => OutfitVisible(o, owner)).ToList(), filter, group));
        }
    }

    /// <summary>
    /// 按 MO2 左侧栏的结构展示：modlist.txt 首行是最高优先级（MO2 界面最底部），
    /// 倒序遍历即 MO2 的自上而下顺序；分隔符作为灰色粗体分组标题。
    /// 只显示含 BodySlide 服装的启用模组——没有服装的模组无法归组，显示只是噪音。
    /// </summary>
    private void BuildStructuredTree(Dictionary<string, List<OutfitEntry>> outfitsByOwner, string filter, SliderGroup? group)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        TreeNode? separator = null;

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.IsForeign)
                continue;

            if (entry.IsSeparator)
            {
                separator = new TreeNode(SeparatorTitle(entry.Name))
                {
                    Tag = "S:",
                    ForeColor = SystemColors.GrayText,
                    NodeFont = BoldFont,
                };
                _tree.Nodes.Add(separator);
                continue;
            }

            var onDisk = _instance is not null &&
                         Directory.Exists(Path.Combine(_instance.ModsDirectory, entry.Name));
            outfitsByOwner.TryGetValue(entry.Name, out var outfits);
            outfits ??= new List<OutfitEntry>();
            if (outfits.Count > 0)
                consumed.Add(entry.Name);

            // 未启用或目录缺失的模组不可能被 BodySlide 加载，跳过
            if (!entry.Enabled || !onDisk)
                continue;

            var visibleOutfits = outfits.Where(o => OutfitVisible(o, entry.Name)).ToList();
            if (!ModVisible(outfits.Count, visibleOutfits.Count, filter))
                continue;
            AddUnder(separator, BuildOutfitModNode(entry.Name, outfits, visibleOutfits, filter, group));
        }

        // 没有归属到任何启用模组的服装（如游戏真实 Data 本体的文件），放在最后
        foreach (var (owner, outfits) in outfitsByOwner)
        {
            if (consumed.Contains(owner))
                continue;
            var visibleOutfits = outfits.Where(o => OutfitVisible(o, owner)).ToList();
            if ((_chkUnassigned.Checked || filter.Length > 0) && visibleOutfits.Count == 0)
                continue;
            _tree.Nodes.Add(BuildOutfitModNode(owner, outfits, visibleOutfits, filter, group));
        }

        // 清理没有可见内容的分隔符节点
        for (var i = _tree.Nodes.Count - 1; i >= 0; i--)
        {
            if (_tree.Nodes[i].Tag is string tag && tag.StartsWith("S:", StringComparison.Ordinal)
                && _tree.Nodes[i].Nodes.Count == 0)
            {
                _tree.Nodes.RemoveAt(i);
            }
        }
    }

    private bool ModVisible(int outfitTotal, int visibleCount, string filter)
    {
        if (outfitTotal == 0)
            return false;
        if ((_chkUnassigned.Checked || filter.Length > 0) && visibleCount == 0)
            return false;
        return true;
    }

    private TreeNode BuildOutfitModNode(string owner, List<OutfitEntry> outfits, List<OutfitEntry> visibleOutfits,
        string filter, SliderGroup? group)
    {
        // 过滤时标明是"该模组有几个服装命中"：模组名本身可能不含关键词（如 "UB EBodyslide"），
        // 命中的是它内部的某件服装，避免看起来像按单字母匹配
        var header = filter.Length > 0 && visibleOutfits.Count < outfits.Count
            ? $"{owner}　(匹配 {visibleOutfits.Count}/{outfits.Count})"
            : $"{owner}　({outfits.Count})";
        var modNode = new TreeNode(header)
        {
            Name = header,
            Tag = "M:" + owner,
            NodeFont = BoldFont,
        };

        var inGroup = 0;
        foreach (var outfit in visibleOutfits)
        {
            var member = group is not null && group.Members.Contains(outfit.Name, StringComparer.Ordinal);
            if (member)
                inGroup++;
            var baseText = outfit.HasConflict ? $"{outfit.Name}（同名冲突）" : outfit.Name;
            modNode.Nodes.Add(new TreeNode((member ? "✔ " : "") + baseText)
            {
                Name = baseText,
                Tag = "O:" + outfit.Name,
                ForeColor = member ? MemberGreen : outfit.HasConflict ? SystemColors.HotTrack : SystemColors.WindowText,
            });
        }

        if (inGroup > 0)
        {
            modNode.Text = $"{header}　[组内 {inGroup}/{visibleOutfits.Count}]";
            modNode.Name = modNode.Text;
            modNode.ForeColor = inGroup == visibleOutfits.Count ? MemberGreen : SystemColors.WindowText;
        }

        if (filter.Length > 0)
            modNode.Expand();
        return modNode;
    }

    private void AddUnder(TreeNode? separator, TreeNode node)
    {
        if (separator is null)
            _tree.Nodes.Add(node);
        else
            separator.Nodes.Add(node);
    }

    private static string SeparatorTitle(string name) =>
        name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase)
            ? name[..^"_separator".Length]
            : name;

    private static IEnumerable<TreeNode> Walk(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Nodes))
                yield return child;
        }
    }

    /// <summary>节点在树中的稳定标识（父链 + 标签 + 序号），用于重建后恢复展开/滚动。</summary>
    private static string Chain(TreeNode node)
    {
        var parent = node.Parent;
        var tag = node.Tag as string ?? "";
        return (parent is null ? "" : Chain(parent)) + $"{tag}{node.Index}/";
    }

    private bool OutfitVisible(OutfitEntry outfit, string owner)
    {
        if (_chkUnassigned.Checked && IsInAnyGroup(outfit.Name))
            return false;
        var filter = _txtFilter.Text.Trim();
        if (filter.Length == 0)
            return true;
        return owner.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || TextFilter.Matches(outfit.Name, filter);
    }

    private SliderGroup? CurrentGroup =>
        _lstGroups.SelectedIndex >= 0 && _lstGroups.SelectedIndex < _groups.Count
            ? _groups[_lstGroups.SelectedIndex]
            : null;

    /// <summary>
    /// 就地更新成员标注（绿色 ✔ / [组内 x/y]），不重建树——
    /// 保留展开状态与滚动位置，方便连续快速分组。
    /// </summary>
    private void UpdateMembershipMarks()
    {
        var group = CurrentGroup;
        _tree.BeginUpdate();
        try
        {
            foreach (var node in Walk(_tree.Nodes))
            {
                var tag = node.Tag as string;
                if (tag is null)
                    continue;

                if (tag.StartsWith("O:", StringComparison.Ordinal))
                {
                    var outfit = tag["O:".Length..];
                    var member = group is not null && group.Members.Contains(outfit, StringComparer.Ordinal);
                    var isConflict = _conflictNames is not null && _conflictNames.Contains(outfit);
                    var targetText = (member ? "✔ " : "") + node.Name;
                    var targetColor = member ? MemberGreen : isConflict ? SystemColors.HotTrack : SystemColors.WindowText;
                    if (node.Text != targetText)
                        node.Text = targetText;
                    if (node.ForeColor != targetColor)
                        node.ForeColor = targetColor;
                }
                else if (tag.StartsWith("M:", StringComparison.Ordinal))
                {
                    var visible = node.Nodes.OfType<TreeNode>().ToList();
                    var inGroup = 0;
                    foreach (TreeNode child in visible)
                    {
                        var outfit = (child.Tag as string)?["O:".Length..];
                        if (outfit is not null && group is not null &&
                            group.Members.Contains(outfit, StringComparer.Ordinal))
                            inGroup++;
                    }
                    var targetText = inGroup > 0 ? $"{node.Name}　[组内 {inGroup}/{visible.Count}]" : node.Name;
                    if (node.Text != targetText)
                        node.Text = targetText;
                    node.ForeColor = visible.Count > 0 && inGroup == visible.Count
                        ? MemberGreen
                        : SystemColors.WindowText;
                }
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
        UpdateGroupInfo();
    }

    /// <summary>把左侧勾选的内容（分隔符/模组/服装）应用到当前组。</summary>
    private void ApplyCheckedToCurrentGroup(bool add)
    {
        var group = CurrentGroup;
        if (group is null)
        {
            MessageBox.Show(this, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Walk(_tree.Nodes))
        {
            if (!node.Checked)
                continue;
            var tag = node.Tag as string ?? "";
            if (tag.StartsWith("O:", StringComparison.Ordinal))
            {
                names.Add(tag["O:".Length..]);
            }
            else if (tag.StartsWith("M:", StringComparison.Ordinal))
            {
                foreach (TreeNode child in node.Nodes)
                {
                    if ((child.Tag as string)?.StartsWith("O:", StringComparison.Ordinal) == true)
                        names.Add((child.Tag as string)!["O:".Length..]);
                }
            }
            else if (tag.StartsWith("S:", StringComparison.Ordinal))
            {
                foreach (var modNode in Walk(node.Nodes)
                             .Where(n => n.Tag is string t && t.StartsWith("M:", StringComparison.Ordinal)))
                {
                    foreach (TreeNode child in modNode.Nodes)
                    {
                        if ((child.Tag as string)?.StartsWith("O:", StringComparison.Ordinal) == true)
                            names.Add((child.Tag as string)!["O:".Length..]);
                    }
                }
            }
        }

        if (names.Count == 0)
        {
            MessageBox.Show(this, "请先在左侧勾选要操作的服装、模组或分隔符。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Snapshot();
        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \"{group.Name}\"（现成员 {group.Members.Count} 个）。");
        _dirty = true;

        // 就地更新标注，保留展开与滚动位置；仅看未分配模式下视图依赖成员关系，需要重建
        if (_chkUnassigned.Checked)
        {
            RefreshTree();
        }
        else
        {
            _tree.BeginUpdate();
            try
            {
                foreach (var node in Walk(_tree.Nodes))
                    node.Checked = false;
            }
            finally
            {
                _tree.EndUpdate();
            }
            UpdateMembershipMarks();
        }

        _loadingUi = true;
        try
        {
            RefreshGroupsList();
        }
        finally
        {
            _loadingUi = false;
        }
    }

    private void OnViewMembers(object? sender, EventArgs e)
    {
        var group = CurrentGroup;
        if (group is null)
        {
            MessageBox.Show(this, "请先选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        new GroupMembersDialog(group, GetTreeDisplayStructure(), Snapshot, RefreshAfterGroupChange).ShowDialog(this);
    }

    private void Snapshot()
    {
        _undoStack.Push(_groups.Select(g => g.Clone()).ToList());
        while (_undoStack.Count > 30)
            _undoStack.Pop();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            MessageBox.Show(this, "没有可撤销的操作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var restored = _undoStack.Pop();
        _dirty = true;
        _groups.Clear();
        _groups.AddRange(restored);
        Log("已撤销上一步分组操作。");
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        RefreshTree();
        UpdateCounts();
    }

    private void OnRules(object? sender, EventArgs e)
    {
        if (_scan is null || _scan.Outfits.Count == 0)
        {
            MessageBox.Show(this, "尚未扫描到任何服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new RuleGroupDialog(_groups,
            (include, exclude, matchOwner, unassignedOnly) =>
                RuleMatchPreview(include, exclude, matchOwner, unassignedOnly));
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var applied = RuleApply(dialog.GroupName, dialog.Add, dialog.Include, dialog.Exclude,
            dialog.MatchOwner, dialog.UnassignedOnly);
        if (applied < 0)
            return;
        MessageBox.Show(this,
            $"已按规则把 {applied} 个服装{(dialog.Add ? "加入" : "移出")}组。",
            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshGroupsList();
        RefreshTree();
        UpdateCounts();
    }

    private List<string> RuleMatchPreview(string include, string exclude, bool matchOwner, bool unassignedOnly)
    {
        var includeKw = GroupRules.SplitKeywords(include);
        var excludeKw = GroupRules.SplitKeywords(exclude);
        var ownerByOutfit = OwnerByOutfit();
        return _scan.Outfits
            .Where(o => RuleHits(o.Name, ownerByOutfit, includeKw, excludeKw, matchOwner, unassignedOnly))
            .Select(o => o.Name)
            .ToList();
    }

    private int RuleApply(string groupName, bool add, string include, string exclude,
        bool matchOwner, bool unassignedOnly)
    {
        var group = _groups.FirstOrDefault(g => g.Name == groupName);
        if (group is null)
            return -1;
        var includeKw = GroupRules.SplitKeywords(include);
        var excludeKw = GroupRules.SplitKeywords(exclude);
        var ownerByOutfit = OwnerByOutfit();
        Snapshot();

        var applied = 0;
        foreach (var outfit in _scan.Outfits)
        {
            if (!RuleHits(outfit.Name, ownerByOutfit, includeKw, excludeKw, matchOwner, unassignedOnly))
                continue;
            if (add)
            {
                if (!group.Members.Contains(outfit.Name, StringComparer.Ordinal))
                {
                    group.Members.Add(outfit.Name);
                    applied++;
                }
            }
            else if (group.Members.RemoveAll(m => m == outfit.Name) > 0)
            {
                applied++;
            }
        }

        _dirty = true;
        Log($"规则归组：{applied} 个服装{(add ? "加入" : "移出")}组 \"{group.Name}\"（现成员 {group.Members.Count} 个）。");
        return applied;
    }

    private Dictionary<string, string> OwnerByOutfit()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, owner, outfits) in GetTreeDisplayStructure())
            foreach (var outfit in outfits)
                map.TryAdd(outfit, owner);
        return map;
    }

    private bool RuleHits(string name, Dictionary<string, string> ownerByOutfit,
        List<string> includeKw, List<string> excludeKw, bool matchOwner, bool unassignedOnly)
    {
        if (unassignedOnly && IsInAnyGroup(name))
            return false;
        var owner = ownerByOutfit.TryGetValue(name, out var o) ? o : "";
        return GroupRules.Matches(name, owner, includeKw, excludeKw, matchOwner);
    }

    /// <summary>按左侧树的结构返回显示数据：分隔符 → 模组 → 服装（供查看组窗口使用）。</summary>
    private List<(string? Separator, string Owner, List<string> Outfits)> GetTreeDisplayStructure()
    {
        var result = new List<(string?, string, List<string>)>();
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
                    var names = outfits.Select(o => o.Name).ToList();
                    foreach (var name in names)
                        consumed.Add(name);
                    result.Add((separator, entry.Name, names));
                }
            }

            // 未归属到启用模组的服装（如游戏 Data 本体），不带分隔符放在最后
            foreach (var (owner, list) in outfitsByOwner)
            {
                var names = list.Select(o => o.Name).Where(n => consumed.Add(n)).ToList();
                if (names.Count > 0)
                    result.Add((null, owner, names));
            }
        }
        else
        {
            foreach (var (owner, list) in outfitsByOwner)
                result.Add((null, owner, list.Select(o => o.Name).ToList()));
        }

        return result;
    }

    private void RefreshAfterGroupChange()
    {
        _dirty = true;
        if (CurrentGroup is { } group)
            Log($"组 \"{group.Name}\" 现有成员 {group.Members.Count} 个。");
        RefreshGroupsList();
        RefreshTree();
        UpdateCounts();
    }

    private void OnTreeBeforeCheck(object? sender, TreeViewCancelEventArgs e)
    {
        // 模组节点（M:）、服装节点（O:）可勾选；分隔符（S:）勾选 = 全选其下所有模组
        if (e.Node?.Tag is string tag &&
            (tag.StartsWith("M:", StringComparison.Ordinal) ||
             tag.StartsWith("O:", StringComparison.Ordinal) ||
             tag.StartsWith("S:", StringComparison.Ordinal)))
            return;
        e.Cancel = true;
    }

    private void OnTreeAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_updatingChecks)
            return;

        // 勾选只是"选中待操作的内容"：父节点勾选联动子节点；
        // 真正写入组要点「加入当前组 / 移出当前组」按钮
        var node = e.Node!;
        var tag = node.Tag as string ?? "";
        _updatingChecks = true;
        try
        {
            if (tag.StartsWith("M:", StringComparison.Ordinal))
            {
                foreach (TreeNode child in node.Nodes)
                    child.Checked = node.Checked;
            }
            else if (tag.StartsWith("S:", StringComparison.Ordinal))
            {
                foreach (var modNode in Walk(node.Nodes)
                             .Where(n => n.Tag is string t && t.StartsWith("M:", StringComparison.Ordinal)))
                {
                    modNode.Checked = node.Checked;
                    foreach (TreeNode child in modNode.Nodes)
                        child.Checked = node.Checked;
                }
            }
        }
        finally
        {
            _updatingChecks = false;
        }
    }

    private static void ApplyMembership(SliderGroup group, string outfit, bool member)
    {
        if (member)
        {
            if (!group.Members.Contains(outfit, StringComparer.Ordinal))
                group.Members.Add(outfit);
        }
        else
        {
            group.Members.RemoveAll(m => m == outfit);
        }
    }

    private void UpdateCounts()
    {
        if (_scan is null)
        {
            _statusCounts.Text = "尚未扫描。";
            return;
        }
        var total = _scan.Outfits.Count;
        var assigned = _scan.Outfits.Count(o => IsInAnyGroup(o.Name));
        var modCount = Walk(_tree.Nodes)
            .Count(n => n.Tag is string tag && tag.StartsWith("M:", StringComparison.Ordinal));
        _statusCounts.Text = $"模组 {modCount} · 服装 {total} · 已分配 {assigned} · 未分配 {total - assigned}";
    }

    // ── 组操作 ───────────────────────────────────────────────────────────
    private void RefreshGroupsList()
    {
        var selectedName = CurrentGroup?.Name;
        _lstGroups.BeginUpdate();
        _lstGroups.Items.Clear();
        foreach (var group in _groups)
            _lstGroups.Items.Add($"{group.Name}　({group.Members.Count})");
        _lstGroups.EndUpdate();
        var index = _groups.FindIndex(g => g.Name == selectedName);
        _lstGroups.SelectedIndex = index >= 0 ? index : (_groups.Count > 0 ? 0 : -1);
        UpdateGroupInfo();
    }

    private void UpdateGroupInfo()
    {
        var group = CurrentGroup;
        _lblGroupInfo.Text = group is null ? "当前未选中组" : $"当前组：{group.Name} · 成员 {group.Members.Count}";
    }

    private bool GroupNameExists(string name, SliderGroup? except = null) =>
        _groups.Any(g => !ReferenceEquals(g, except) &&
                         string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    private void OnNewGroup(object? sender, EventArgs e)
    {
        var name = InputDialog.Show(this, "新建组", "组名：");
        if (string.IsNullOrEmpty(name))
            return;
        if (GroupNameExists(name))
        {
            MessageBox.Show(this, "已存在同名组（忽略大小写）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Snapshot();
        _dirty = true;
        _groups.Add(new SliderGroup(name));
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        _lstGroups.SelectedIndex = _groups.Count - 1;
        Log($"新建组：{name}");
    }

    private void OnRenameGroup(object? sender, EventArgs e)
    {
        var group = CurrentGroup;
        if (group is null)
            return;
        var name = InputDialog.Show(this, "重命名组", "新组名：", group.Name);
        if (string.IsNullOrEmpty(name) || name == group.Name)
            return;
        if (GroupNameExists(name, group))
        {
            MessageBox.Show(this, "已存在同名组（忽略大小写）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Snapshot();
        Log($"组 \"{group.Name}\" 重命名为 \"{name}\"。");
        group.Name = name;
        _dirty = true;
        RefreshGroupsList();
    }

    private void OnDeleteGroup(object? sender, EventArgs e)
    {
        var group = CurrentGroup;
        if (group is null)
            return;
        if (MessageBox.Show(this, $"确定删除组 \"{group.Name}\"？（成员 {group.Members.Count} 个）",
                "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        Snapshot();
        _groups.Remove(group);
        Log($"删除组：{group.Name}");
        _dirty = true;
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        RefreshTree();
    }

    private void OnImportGroups(object? sender, EventArgs e)
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
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (!SliderGroupFile.TryLoad(dialog.FileName, out var imported, out var error))
        {
            MessageBox.Show(this, $"导入失败：{error}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Snapshot();
        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);
        if (addedGroups + addedMembers > 0)
            _dirty = true;
        Log($"从 {dialog.FileName} 导入：新增 {addedGroups} 个组、{addedMembers} 个成员。");
        RefreshGroupsList();
        RefreshTree();
    }

    // ── 写出 ─────────────────────────────────────────────────────────────
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
                ? "未设置（点\"浏览…\"选择保存位置）"
                : "未确定（先完成扫描）";
            _statusTarget.Text = $"写出：{hint}";
            _statusTarget.ToolTipText = "";
            Log($"写出目标：{hint}。");
            return;
        }
        _statusTarget.Text = $"输出：{target.Value.Description}";
        _statusTarget.ToolTipText = target.Value.Dir;
        Log($"输出目录：{target.Value.Dir}（{target.Value.Description}）");
    }

    private void BrowseForTarget()
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
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.CustomTargetDir = dialog.SelectedPath;
        _settings.Save();
        Log($"自定义输出目录：{dialog.SelectedPath}");
        if (_cboWriteMode.SelectedIndex != (int)WriteMode.Custom)
            _cboWriteMode.SelectedIndex = (int)WriteMode.Custom; // 触发模式切换，否则所选路径会被忽略
        else
            LogWriteTarget();
    }

    private void OnGenerate(object? sender, EventArgs e) => TrySaveGroups(showSuccessDialog: true);

    private bool TrySaveGroups(bool showSuccessDialog)
    {
        var target = ResolveWriteTarget();
        if (target is null)
        {
            MessageBox.Show(this, "尚未完成扫描或无法确定写入位置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Count == 0)
        {
            MessageBox.Show(this, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Any(g => g.Name.Trim().Length == 0))
        {
            MessageBox.Show(this, "存在空名称的组，请先重命名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this,
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
            MessageBox.Show(this, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _dirty = false;
        var memberCount = _groups.Sum(g => g.Members.Count);
        var fileExamples = string.Join("、", _groups.Take(3).Select(g => SliderGroupFile.FileNameForGroup(g.Name)));
        Log($"已保存 {dir} 下 {_groups.Count} 个组文件（共 {memberCount} 个成员，{target.Value.Description}）。");
        if (!showSuccessDialog)
            return true;

        var customNote2 = _settings.WriteMode == WriteMode.Custom
            ? "\n\n注意：自定义目录不一定是 BodySlide 能读取的地方（有效项目路径的 SliderGroups 或模组的 SliderGroups）。若 BodySlide 里看不到新组，请把文件移动到这些位置。"
            : "";
        MessageBox.Show(this,
            $"已保存 {_groups.Count} 个组到目录：\n{dir}\n\n" +
            $"每个组一个文件，文件名即组名：{fileExamples}{(_groups.Count > 3 ? " …" : "")}\n" +
            $"共 {memberCount} 个成员（{target.Value.Description}）。{customNote2}\n\n" +
            "请重启 BodySlide 使其生效。若 BodySlide 是通过 MO2 启动的，建议同时重启 MO2 以刷新虚拟文件系统。",
            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return true;
    }

    // ── 其他 ─────────────────────────────────────────────────────────────
    private void OnAddMo2(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 MO2 实例目录（含 ModOrganizer.ini）或便携安装目录（含 ModOrganizer.exe）",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var instance = Mo2Discovery.CreateFromDirectory(dialog.SelectedPath);
        if (instance is null)
        {
            MessageBox.Show(this,
                "该目录下没有 ModOrganizer.ini，也不像便携安装（缺 ModOrganizer.exe）。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!_settings.ExtraMo2Dirs.Contains(instance.InstanceDir, StringComparer.OrdinalIgnoreCase))
            _settings.ExtraMo2Dirs.Add(instance.InstanceDir);
        _settings.Save();
        ReloadInstances();
    }

    private string BuildDiagnostics()
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

    private void ShowDiagnostics() =>
        new DiagnosticsDialog(BuildDiagnostics()).ShowDialog(this);
}
