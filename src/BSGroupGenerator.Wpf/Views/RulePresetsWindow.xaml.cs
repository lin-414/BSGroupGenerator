using System.Windows;
using BSGroupGenerator.Core;

using BSGroupGenerator.Wpf.Services;

namespace BSGroupGenerator.Wpf.Views;

/// <summary>规则预设管理：说明工作流，查看/删除已保存预设；「新建预设」拉起规则归组编辑器。</summary>
public partial class RulePresetsWindow : Window
{
    private readonly List<RulePreset> _presets;
    private readonly Action _openEditor;
    private List<(RulePreset Preset, string Title, string Detail)> _rows = [];

    /// <summary>是否有预设被删除（宿主据此保存设置）。</summary>
    public bool Changed { get; private set; }

    public RulePresetsWindow(List<RulePreset> presets, Action openEditor)
    {
        InitializeComponent();
        _presets = presets;
        _openEditor = openEditor;
        Reload();
    }

    private void Reload()
    {
        _rows = _presets.Select(p =>
        {
            var group = p.GroupName.Length == 0 ? L10n.Tr("L.Word_Unspecified") : p.GroupName;
            var title = L10n.TrF("L.RulePresets_RowTitle", p.Name,
                L10n.Tr(p.Add ? "L.Word_Add" : "L.Word_Remove"), group);
            var detail = L10n.TrF("L.RulePresets_RowDetail",
                string.IsNullOrWhiteSpace(p.ModInclude) ? L10n.Tr("L.Word_Unlimited") : p.ModInclude,
                string.IsNullOrWhiteSpace(p.OutfitInclude) ? L10n.Tr("L.Word_All") : p.OutfitInclude,
                string.IsNullOrWhiteSpace(p.OutfitExclude) ? L10n.Tr("L.Word_None") : p.OutfitExclude);
            if (p.UnassignedOnly)
                detail += L10n.Tr("L.RulePresets_UnassignedSuffix");
            return (Preset: p, Title: title, Detail: detail);
        }).ToList();
        List.ItemsSource = _rows;
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        // 编辑器是独立窗口，本窗口不会随之刷新，先关掉避免留下过期列表
        Close();
        _openEditor();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var idx = List.SelectedIndex;
        if (idx < 0 || idx >= _rows.Count)
        {
            MessageBox.Show(this, L10n.Tr("L.RulePresets_SelectToDelete"), L10n.Tr("L.Title_Tip"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _presets.Remove(_rows[idx].Preset);
        Changed = true;
        Reload();
    }
}
