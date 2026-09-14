using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BSGroupGenerator.Wpf.Services;
using BSGroupGenerator.Wpf.ViewModels;
using Microsoft.Win32;

namespace BSGroupGenerator.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private RuleGroupWindow? _ruleWindow;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.NotifyHandler = (title, message, warning) =>
        {
            MessageBox.Show(this, message, title, MessageBoxButton.OK,
                warning ? MessageBoxImage.Warning : MessageBoxImage.Information);
            return false;
        };
        _vm.ConfirmHandler = (title, message) =>
            MessageBox.Show(this, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        _vm.FolderPicker = description => PickFolder(description);
        _vm.FilePicker = _ => PickImportFile();
        _vm.NewModsDetected += request => Dispatcher.Invoke(() => ShowNewMods(request));
        _vm.SaveCompleted += (dir, bsAppDir) => Dispatcher.Invoke(() => ShowSaveSuccess(dir, bsAppDir));
        _vm.UpdateAvailable += (tag, current) => Dispatcher.Invoke(() =>
        {
            if (MessageBox.Show(this, L10n.TrF("L.Msg_UpdateAvailable", tag, current),
                    L10n.Tr("L.Title_CheckUpdate"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                MainViewModel.OpenUrl("https://github.com/lin-414/BSGroupGenerator/releases/latest");
        });

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.LogText))
                LogBox.ScrollToEnd();
        };

        HookDragDrop();
        SyncThemeChecks();
        SyncLangChecks();
        Loaded += (_, _) => _ = _vm.CheckForUpdatesAsync(reportUpToDate: false);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F1)
            {
                e.Handled = true;
                ShowHelp();
            }
        };
    }

    /// <summary>把分组 XML 拖到窗口任意位置即可导入（等价于「导入现有组文件…」）。</summary>
    private void HookDragDrop()
    {
        AllowDrop = true;
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effects = DragDropEffects.Copy;
        };
        Drop += (_, e) =>
        {
            var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (files is not null && files.Length > 0)
                _vm.ImportFiles(files.Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)));
        };
    }

    // ── 界面主题（设置菜单）──────────────────────────────────────────────
    private void SyncThemeChecks()
    {
        MiThemeBoutique.IsChecked = ThemeManager.Current == ThemeManager.Boutique;
        MiThemeLight.IsChecked = ThemeManager.Current == ThemeManager.Light;
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string theme } || theme == ThemeManager.Current)
            return;
        ThemeManager.Apply(theme);
        _vm.Settings.UiTheme = theme;
        _vm.Settings.Save();
        SyncThemeChecks();
    }

    // ── 界面语言（设置菜单）──────────────────────────────────────────────
    private void SyncLangChecks()
    {
        MiLangZh.IsChecked = L10n.Current == L10n.Zh;
        MiLangEn.IsChecked = L10n.Current == L10n.En;
    }

    private void Lang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string lang } || lang == L10n.Current)
            return;
        L10n.Apply(lang);
        _vm.Settings.UiLanguage = lang;
        _vm.Settings.Save();
        SyncLangChecks();
        _vm.OnLanguageChanged();
    }

    private string? PickFolder(string description)
    {
        var dialog = new OpenFolderDialog { Title = description };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private string? PickImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = L10n.Tr("L.Pick_ImportGroups"),
            Filter = L10n.Tr("L.Pick_ImportGroupsFilter"),
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private void ShowSaveSuccess(string dir, string? bsAppDir)
    {
        var memberCount = _vm.Store.Groups.Sum(g => g.Members.Count);
        var examples = string.Join("、", _vm.Store.Groups.Take(3)
            .Select(g => BSGroupGenerator.Core.SliderGroupFile.FileNameForGroup(g.Name)));
        new SaveSuccessWindow(_vm.Store.Count, dir, examples, memberCount,
            _vm.ResolveTargetDescription(), bsAppDir,
            customNote: _vm.Settings.WriteMode == Core.WriteMode.Custom).ShowDialog();
    }

    private void ShowNewMods(NewModsRequest request)
    {
        var window = new NewModsWindow(request) { Owner = this };
        window.ShowDialog();
    }

    private void ShowHelp() => new HelpWindow { Owner = this }.ShowDialog();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Diagnostics_Click(object sender, RoutedEventArgs e) =>
        new DiagnosticsWindow(_vm.BuildDiagnostics()) { Owner = this }.ShowDialog();

    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void CheckUpdate_Click(object sender, RoutedEventArgs e) =>
        _ = _vm.CheckForUpdatesAsync(reportUpToDate: true);

    private void AddMo2_Click(object sender, RoutedEventArgs e)
    {
        var dir = PickFolder(L10n.Tr("L.Pick_Mo2Dir"));
        if (dir is not null)
            _vm.AddMo2Directory(dir);
    }

    private void BrowseBodySlide_Click(object sender, RoutedEventArgs e)
    {
        var dir = PickFolder(L10n.Tr("L.Pick_BodySlideDir"));
        if (dir is not null)
            _vm.UseBodySlideDirectory(dir);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var file = PickImportFile();
        if (file is not null)
            _vm.ImportFiles([file]);
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = InputWindow.Show(this, L10n.Tr("L.Title_NewGroup"), L10n.Tr("L.Prompt_GroupName"));
        if (name is not null)
            _vm.NewGroupCommand.Execute(name);
    }

    private void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        var current = _vm.Store.Current?.Name;
        if (current is null)
            return;
        var name = InputWindow.Show(this, L10n.Tr("L.Title_RenameGroup"), L10n.Tr("L.Prompt_NewGroupName"), current);
        if (name is not null)
            _vm.RenameGroupCommand.Execute(name);
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var group = _vm.Store.Current;
        if (group is null)
            return;
        if (MessageBox.Show(this, L10n.TrF("L.Msg_ConfirmDeleteGroup", group.Name, group.Members.Count),
                L10n.Tr("L.Title_Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            _vm.DeleteGroupCommand.Execute(null);
    }

    private void ViewMembers_Click(object sender, RoutedEventArgs e)
    {
        var group = _vm.Store.Current;
        if (group is null)
        {
            MessageBox.Show(this, L10n.Tr("L.Msg_SelectGroupFirst"), L10n.Tr("L.Title_Tip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new GroupMembersWindow(group, _vm.GetTreeDisplayStructure(),
            beforeChange: () => _vm.Store.Snapshot(),
            onChanged: () =>
            {
                _vm.Store.MarkDirtyFromUi();
                _vm.RefreshGroupsList();
                _vm.RefreshTree();
            })
        { Owner = this }.ShowDialog();
    }

    private void Rules_Click(object sender, RoutedEventArgs e) => OpenRuleEditor();

    private void RulePresets_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RulePresetsWindow(_vm.Settings.RulePresets, OpenRuleEditor) { Owner = this };
        dialog.ShowDialog();
        if (dialog.Changed)
        {
            _vm.Settings.Save();
            _vm.Log(L10n.TrF("L.Log_PresetsUpdated", _vm.Settings.RulePresets.Count));
        }
    }

    /// <summary>打开规则归组编辑器（非模态；规则预设窗口的「新建预设」也走这里）。</summary>
    private void OpenRuleEditor()
    {
        if (_ruleWindow is { } existing && existing.IsLoaded)
        {
            existing.Activate(); // 已打开时不再叠加新窗口（两个窗口叠在一起会互相干扰点击）
            return;
        }
        if (_vm.Store.Count == 0)
        {
            MessageBox.Show(this, L10n.Tr("L.Msg_NeedGroupFirst"), L10n.Tr("L.Title_Tip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_vm.Scan is null || _vm.Scan.Outfits.Count == 0)
        {
            MessageBox.Show(this, L10n.Tr("L.Msg_NoOutfits"), L10n.Tr("L.Title_Tip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var ownerMap = _vm.OwnerByOutfit();
        RuleGroupWindow? window = null;
        window = new RuleGroupWindow(_vm.Store.Groups, _vm.Store.Current?.Name, _vm.GetTreeDisplayStructure(),
            (modInclude, outfitInclude, outfitExclude, unassignedOnly) =>
                _vm.RuleMatchPreview(ownerMap, modInclude, outfitInclude, outfitExclude, unassignedOnly),
            onApply: () =>
            {
                if (window is null)
                    return;
                var applied = _vm.RuleApply(window.GroupName, window.Add, window.ModInclude,
                    window.OutfitInclude, window.OutfitExclude, window.UnassignedOnly);
                if (applied < 0)
                    return;
                _vm.RefreshGroupsList();
                _vm.RefreshTree();
                window.UpdatePreview();
            },
            onSavePreset: preset => _vm.SaveRulePreset(preset))
        { Owner = this };
        _ruleWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_ruleWindow, window)) _ruleWindow = null; };
        window.Show();
    }

    private void Groups_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ViewMembers_Click(sender, e);

    private void Output_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dir = _vm.ResolveOutputDirectory();
        if (dir is null)
            MessageBox.Show(this, L10n.Tr("L.Msg_OutputDirUndetermined"), L10n.Tr("L.Title_Tip"));
        else
            MainViewModel.OpenDirectory(dir);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.Store.Dirty)
        {
            var choice = MessageBox.Show(this,
                L10n.Tr("L.Msg_UnsavedOnExit"),
                L10n.Tr("L.Title_UnsavedChanges"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (choice == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (choice == MessageBoxResult.Yes && !_vm.TrySaveGroups(showSuccessDialog: false))
            {
                e.Cancel = true;
                return;
            }
        }
        _vm.OnViewClosed();
        base.OnClosing(e);
    }
}
