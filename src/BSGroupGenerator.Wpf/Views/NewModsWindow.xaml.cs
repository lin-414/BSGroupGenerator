using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BSGroupGenerator.Wpf.ViewModels;

using BSGroupGenerator.Wpf.Services;

namespace BSGroupGenerator.Wpf.Views;

/// <summary>
/// 新装模组归组弹窗：三层勾选树（分隔符→模组→服装），支持分多批换目标组归组；
/// 已应用的项即时移除，全部处理完自动关闭。
/// </summary>
public partial class NewModsWindow : Window
{
    private readonly NewModsRequest _request;
    private int _modCount;
    private int _remainingTotal;
    private int _totalCount;

    public NewModsWindow(NewModsRequest request)
    {
        InitializeComponent();
        _request = request;
        _modCount = request.Entries.Count;
        _remainingTotal = request.Entries.Sum(e => e.Outfits.Count);
        _totalCount = _remainingTotal;

        TopLabel.Text = TopText();
        GroupCombo.ItemsSource = request.Groups;
        var preselect = request.Groups.ToList().FindIndex(g => g.Name == request.PreselectGroup);
        GroupCombo.SelectedIndex = preselect >= 0 ? preselect : (request.Groups.Count > 0 ? 0 : -1);

        if (request.PresetNames.Count > 0)
        {
            PresetsButton.Content = L10n.TrF("L.NewMods_PresetsBtn", request.PresetNames.Count);
            ToolTipService.SetToolTip(PresetsButton,
                L10n.Tr("L.NewMods_PresetsTip"));
        }
        else
        {
            PresetsButton.Visibility = Visibility.Collapsed;
        }

        BuildTree();
        UpdateSummary();
    }

    private string TopText() =>
        _remainingTotal < _totalCount
            ? L10n.TrF("L.NewMods_TopRemaining", _remainingTotal, _modCount, _totalCount) +
              "\n" + L10n.Tr("L.NewMods_TopRemainingHint")
            : L10n.TrF("L.NewMods_TopFirst", _modCount, _remainingTotal) +
              "\n" + L10n.Tr("L.NewMods_TopFirstHint");

    private void BuildTree()
    {
        var roots = new ObservableCollection<NodeVM>();
        SeparatorNodeVM? currentSepNode = null;
        string? currentSepName = null;
        var started = false;

        foreach (var (separator, owner, outfits) in _request.Entries)
        {
            if (!started || currentSepName != separator)
            {
                currentSepName = separator;
                started = true;
                currentSepNode = string.IsNullOrEmpty(separator) ? null : new SeparatorNodeVM(separator!);
                if (currentSepNode is not null)
                    roots.Add(currentSepNode);
            }

            var modNode = new ModNodeVM($"{owner}　({outfits.Count})", owner, [], _ => false, false)
            {
                Text = $"{owner}　({outfits.Count})",
                IsExpanded = true, // 触发懒物化（同时保证展开状态一致）
            };
            // ModNodeVM 懒物化回调未提供 isMember——此处服装直接构建（全部默认勾选）
            foreach (var outfit in outfits)
                modNode.Children.Add(new OutfitNodeVM(outfit, hasConflict: false, isMember: false, isChecked: true)
                {
                    Parent = modNode,
                });
            modNode.MarkMaterialized(); // 子节点已手动构建，防止展开时被懒物化清空

            if (currentSepNode is not null)
            {
                modNode.Parent = currentSepNode;
                currentSepNode.Children.Add(modNode);
            }
            else
            {
                roots.Add(modNode);
            }
        }

        Tree.ItemsSource = roots;
        RefreshAggregates(roots);
    }

    /// <summary>手动构建/剪枝不会触发聚合，自底向上重算所有容器的勾选态。</summary>
    private static void RefreshAggregates(IEnumerable<NodeVM> roots)
    {
        foreach (var node in Walk(roots).Where(n => n is ModNodeVM or SeparatorNodeVM).Reverse())
            node.RefreshAggregated();
    }

    private List<string> SelectedOutfits =>
        Walk(Tree.ItemsSource!.Cast<NodeVM>())
            .Where(n => n.IsChecked == true && n is OutfitNodeVM)
            .Select(n => ((OutfitNodeVM)n).OutfitName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private void UpdateSummary()
    {
        var selected = SelectedOutfits;
        SummaryLabel.Text = selected.Count == 0
            ? L10n.Tr("L.NewMods_NoneSelected")
            : L10n.TrF("L.NewMods_Summary", selected.Count, CountOwners(selected));
    }

    private int CountOwners(List<string> outfits)
    {
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Walk(Tree.ItemsSource!.Cast<NodeVM>()))
        {
            if (node is OutfitNodeVM outfit && outfits.Contains(outfit.OutfitName) && node.Parent is ModNodeVM parent)
                owners.Add(parent.Owner);
        }
        return owners.Count;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedOutfits;
        if (selected.Count == 0)
        {
            MessageBox.Show(this, L10n.Tr("L.NewMods_SelectFirst"), L10n.Tr("L.Title_Tip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (GroupCombo.SelectedItem is not BSGroupGenerator.Core.SliderGroup group)
        {
            MessageBox.Show(this, L10n.Tr("L.NewMods_SelectTarget"), L10n.Tr("L.Title_Tip"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _request.ApplyBatch(group.Name, selected);
        RemoveApplied(selected);
    }

    private void Presets_Click(object sender, RoutedEventArgs e)
    {
        var newlyAssigned = _request.ApplyPresets();
        if (newlyAssigned.Count > 0)
            RemoveApplied(newlyAssigned);
    }

    /// <summary>剔除已归组的服装并修剪空节点；全部处理完后自动关闭。</summary>
    private void RemoveApplied(IEnumerable<string> outfits)
    {
        var applied = new HashSet<string>(outfits, StringComparer.Ordinal);
        var roots = Tree.ItemsSource!.Cast<NodeVM>().ToList();

        foreach (var modNode in Walk(roots).OfType<ModNodeVM>().ToList())
        {
            foreach (var child in modNode.Children
                         .Where(c => c is OutfitNodeVM o && applied.Contains(o.OutfitName))
                         .ToList())
                modNode.Children.Remove(child);
            if (modNode.Children.Count == 0 && modNode.Parent is not null)
                modNode.Parent.Children.Remove(modNode);
        }
        foreach (var sep in roots.OfType<SeparatorNodeVM>().ToList())
        {
            if (sep.Children.Count == 0)
                roots.Remove(sep);
        }

        Tree.ItemsSource = roots;
        RefreshAggregates(roots);
        _remainingTotal = Walk(roots).Count(n => n is OutfitNodeVM);
        TopLabel.Text = TopText();
        UpdateSummary();
        if (_remainingTotal == 0)
            Close();
    }

    private static IEnumerable<NodeVM> Walk(IEnumerable<NodeVM> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Children))
                yield return child;
        }
    }
}
