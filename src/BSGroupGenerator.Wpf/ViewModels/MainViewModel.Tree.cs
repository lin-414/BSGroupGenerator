using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using BSGroupGenerator.Core;
using BSGroupGenerator.Wpf.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BSGroupGenerator.Wpf.ViewModels;

public partial class MainViewModel
{
    /// <summary>重建树（RefreshTree 别名，与 WinForms 命名对齐）。</summary>
    public void RefreshTree() => RebuildTree();

    [ObservableProperty] private ObservableCollection<NodeVM> _treeRoots = [];
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _modFilterText = "";
    [ObservableProperty] private bool _unassignedOnly;

    private CancellationTokenSource? _filterDebounce;

    partial void OnFilterTextChanged(string value) => DebounceRebuild();
    partial void OnModFilterTextChanged(string value) => DebounceRebuild();
    partial void OnUnassignedOnlyChanged(bool value) => RebuildTree();

    private void DebounceRebuild()
    {
        _filterDebounce?.Cancel();
        var cts = _filterDebounce = new CancellationTokenSource();
        var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        _ = Task.Delay(350, cts.Token).ContinueWith(_ =>
        {
            if (!cts.IsCancellationRequested)
                RebuildTree();
        }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, scheduler);
    }

    private bool OutfitVisible(OutfitEntry outfit)
    {
        if (UnassignedOnly && IsInAnyGroup(outfit.Name))
            return false;
        return TextFilter.Matches(outfit.Name, FilterText);
    }

    private bool ModVisible(string modName, int outfitTotal, int visibleCount, string outfitFilter)
    {
        if (outfitTotal == 0)
            return false;
        if (ModFilterText.Trim().Length > 0 &&
            !modName.Contains(ModFilterText.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        // 服装关键字过滤或"仅看未分配"下，没有可见内容的模组一并隐藏
        if ((outfitFilter.Length > 0 || UnassignedOnly) && visibleCount == 0)
            return false;
        return true;
    }

    public void RebuildTree()
    {
        if (_closed)
            return;

        // 记录展开状态，重建后恢复
        HashSet<string> expanded = new();
        foreach (var node in WalkRoots())
            if (node.IsExpanded)
                expanded.Add(Chain(node));

        var filter = FilterText.Trim();
        var modFilter = ModFilterText.Trim();
        var newRoots = new ObservableCollection<NodeVM>();
        if (Scan is not null)
        {
            var outfitsByOwner = new Dictionary<string, List<OutfitEntry>>();
            foreach (var outfit in Scan.Outfits)
            {
                if (!outfitsByOwner.TryGetValue(outfit.OwnerLabel, out var list))
                    outfitsByOwner[outfit.OwnerLabel] = list = [];
                list.Add(outfit);
            }

            if (IsVirtualScan)
                BuildStructuredTree(newRoots, outfitsByOwner, filter);
            else
                BuildFlatTree(newRoots, outfitsByOwner, filter);

            if (newRoots.Count == 0)
                newRoots.Add(new NodeVM(NodeKind.Outfit) { Text = L10n.Tr("L.Tree_NoMatchingMods"), IsPlaceholder = true });
        }

        TreeRoots = newRoots;
        foreach (var node in WalkRoots())
            if (expanded.Contains(Chain(node)))
                node.IsExpanded = true;

        UpdateCounts();
        UpdateTitle();
    }

    private IEnumerable<NodeVM> WalkRoots()
    {
        foreach (var root in TreeRoots)
            foreach (var n in root.WalkSelfAndDescendants())
                yield return n;
    }

    private static string Chain(NodeVM node)
    {
        var parent = node.Parent;
        var index = parent?.Children.IndexOf(node) ?? -1;
        return (parent is null ? "" : Chain(parent)) + $"{node.Kind}{index}/";
    }

    private void BuildFlatTree(ObservableCollection<NodeVM> roots,
        Dictionary<string, List<OutfitEntry>> outfitsByOwner, string filter)
    {
        foreach (var (owner, outfits) in outfitsByOwner)
        {
            var visibleOutfits = outfits.Where(OutfitVisible).ToList();
            if (!ModVisible(owner, outfits.Count, visibleOutfits.Count, filter))
                continue;
            roots.Add(BuildOutfitModNode(roots, owner, outfits, visibleOutfits, filter));
        }
    }

    private void BuildStructuredTree(ObservableCollection<NodeVM> roots,
        Dictionary<string, List<OutfitEntry>> outfitsByOwner, string filter)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        SeparatorNodeVM? separator = null;

        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];
            if (entry.IsForeign)
                continue;

            if (entry.IsSeparator)
            {
                separator = new SeparatorNodeVM(SeparatorDisplayName(entry.Name));
                roots.Add(separator);
                continue;
            }

            var onDisk = SelectedInstance is not null &&
                         Directory.Exists(Path.Combine(SelectedInstance.ModsDirectory, entry.Name));
            outfitsByOwner.TryGetValue(entry.Name, out var outfits);
            outfits ??= [];
            if (outfits.Count > 0)
                consumed.Add(entry.Name);

            // 未启用或目录缺失的模组不可能被 BodySlide 加载，跳过
            if (!entry.Enabled || !onDisk)
                continue;

            var visibleOutfits = outfits.Where(OutfitVisible).ToList();
            if (!ModVisible(entry.Name, outfits.Count, visibleOutfits.Count, filter))
                continue;
            var node = BuildOutfitModNode(roots, entry.Name, outfits, visibleOutfits, filter);
            if (separator is not null)
            {
                node.Parent = separator;
                separator.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        // 没有归属到任何启用模组的服装（如游戏真实 Data 本体的文件），放在最后
        foreach (var (owner, outfits) in outfitsByOwner)
        {
            if (consumed.Contains(owner))
                continue;
            var visibleOutfits = outfits.Where(OutfitVisible).ToList();
            if ((UnassignedOnly || filter.Length > 0) && visibleOutfits.Count == 0)
                continue;
            if (!ModVisible(owner, outfits.Count, visibleOutfits.Count, filter))
                continue;
            roots.Add(BuildOutfitModNode(roots, owner, outfits, visibleOutfits, filter));
        }

        // 清理没有可见内容的分隔符节点
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            if (roots[i] is SeparatorNodeVM && roots[i].Children.Count == 0)
                roots.RemoveAt(i);
        }
    }

    private ModNodeVM BuildOutfitModNode(ObservableCollection<NodeVM> roots, string owner,
        List<OutfitEntry> outfits, List<OutfitEntry> visibleOutfits, string filter)
    {
        // 过滤/仅看未分配时标明是"该模组有几个服装可见"
        var narrowed = filter.Length > 0 || UnassignedOnly;
        var header = narrowed && visibleOutfits.Count < outfits.Count
            ? L10n.TrF("L.Tree_MatchHeader", owner, visibleOutfits.Count, outfits.Count)
            : $"{owner}　({outfits.Count})";

        var group = Store.Current;
        var inGroup = visibleOutfits.Count(o => group is not null && group.Members.Contains(o.Name, StringComparer.Ordinal));

        var node = new ModNodeVM(header, owner, visibleOutfits, IsInAnyGroup, false);
        if (inGroup > 0)
        {
            node.Text = L10n.TrF("L.Tree_InGroupBadge", header, inGroup, visibleOutfits.Count);
            node.IsMember = inGroup == visibleOutfits.Count && visibleOutfits.Count > 0;
        }

        node.Parent = null;
        if (filter.Length > 0)
        {
            // 过滤：命中内容必须立即可见
            node.IsExpanded = true; // 触发物化
        }
        return node;
    }

    /// <summary>成员标注就地更新（绿色 ✔ / [组内 x/y]），不重建树——保留展开与滚动位置。</summary>
    private void UpdateMembershipMarks()
    {
        var group = Store.Current;
        foreach (var node in WalkRoots())
        {
            switch (node)
            {
                case OutfitNodeVM outfit:
                {
                    var member = group is not null && group.Members.Contains(outfit.OutfitName, StringComparer.Ordinal);
                    var targetText = (member ? "✔ " : "") +
                                     (outfit.HasConflict ? outfit.OutfitName + L10n.Tr("L.Tree_ConflictSuffix") : outfit.OutfitName);
                    if (node.Text != targetText)
                        node.Text = targetText;
                    node.IsMember = member;
                    break;
                }
                case ModNodeVM mod:
                {
                    var inGroup = mod.Outfits.Count(o =>
                        group is not null && group.Members.Contains(o.Name, StringComparer.Ordinal));
                    var baseHeader = RebuildHeaderBase(mod);
                    var targetText = inGroup > 0 ? L10n.TrF("L.Tree_InGroupBadge", baseHeader, inGroup, mod.Outfits.Count) : baseHeader;
                    if (node.Text != targetText)
                        node.Text = targetText;
                    node.IsMember = mod.Outfits.Count > 0 && inGroup == mod.Outfits.Count;
                    break;
                }
            }
        }
    }

    /// <summary>从现有文本还原"基础头部"（去掉 [组内 x/y] 徽标）。</summary>
    private static string RebuildHeaderBase(ModNodeVM mod)
    {
        var text = mod.Text;
        var idx = text.IndexOf(L10n.Tr("L.Tree_InGroupPrefix"), StringComparison.Ordinal);
        return idx >= 0 ? text[..idx] : text;
    }

    /// <summary>把左侧勾选的内容（分隔符/模组/服装）应用到当前组。</summary>
    [RelayCommand]
    private void ApplyCheckedToCurrentGroup(string parameter)
    {
        var add = parameter != "remove";
        if (Store.Current is null)
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), L10n.Tr("L.Msg_SelectGroupFirstSide"));
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in WalkRoots())
        {
            if (node.IsChecked != true)
                continue;
            switch (node)
            {
                case OutfitNodeVM outfit:
                    names.Add(outfit.OutfitName);
                    break;
                case ModNodeVM mod:
                    foreach (var outfit in mod.Outfits)
                        names.Add(outfit.Name);
                    break;
                case SeparatorNodeVM:
                    foreach (var modNode in node.WalkSelfAndDescendants().OfType<ModNodeVM>())
                        foreach (var outfit in modNode.Outfits)
                            names.Add(outfit.Name);
                    break;
            }
        }

        if (names.Count == 0)
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), L10n.Tr("L.Msg_NothingChecked"));
            return;
        }

        Store.ApplyToCurrent(names, add);
        Log(L10n.TrF("L.Log_Applied", names.Count, L10n.Tr(add ? "L.Word_Add" : "L.Word_Remove"), Store.Current!.Name, Store.Current!.Members.Count));
        RefreshTree();
        RefreshGroupsListPreserveSelection();
    }

    [RelayCommand]
    private void Undo()
    {
        var (ok, error) = Store.Undo();
        if (!ok)
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), error ?? L10n.Tr("L.Msg_NothingToUndo"));
            return;
        }
        Log(L10n.Tr("L.Log_Undone"));
        RefreshGroupsList();
        RefreshTree();
    }

    private void UpdateCounts()
    {
        if (Scan is null)
        {
            StatusCounts = L10n.Tr("L.Vm_NotScanned");
            return;
        }
        var total = Scan.Outfits.Count;
        var assigned = Scan.Outfits.Count(o => IsInAnyGroup(o.Name));
        var modCount = WalkRoots().Count(n => n.Kind == NodeKind.Mod);
        StatusCounts = L10n.TrF("L.Vm_StatusCounts", modCount, total, assigned, total - assigned);
    }

    public void UpdateTitle()
    {
        IsDirty = Store.Dirty;
        WindowTitle = Store.Dirty ? AppTitle + L10n.Tr("L.Vm_UnsavedSuffix") : AppTitle;
    }

    // ── 组列表 ──
    public void RefreshGroupsList()
    {
        var selectedName = Store.Current?.Name;
        Groups = new ObservableCollection<GroupItem>(
            Store.Groups.Select(g => new GroupItem(g.Name, g.Members.Count)));
        var index = Store.Groups.ToList().FindIndex(g => g.Name == selectedName);
        SelectedGroupIndex = index >= 0 ? index : (Store.Count > 0 ? 0 : -1);
        UpdateGroupInfo();
    }

    /// <summary>应用勾选后调用：不清空选中位置。</summary>
    private void RefreshGroupsListPreserveSelection() => RefreshGroupsList();

    partial void OnSelectedGroupIndexChanged(int value)
    {
        if (value < 0 || value >= Store.Count)
            return;
        Store.SelectGroup(Store.Groups[value].Name);
        UpdateGroupInfo();
        UpdateMembershipMarks();
    }

    private void UpdateGroupInfo()
    {
        GroupInfo = Store.Current is null ? L10n.Tr("L.Vm_NoGroupSelected") : L10n.TrF("L.Vm_GroupInfo", Store.Current.Name, Store.Current.Members.Count);
    }

    // ── 组操作 ──
    [RelayCommand]
    private void NewGroup(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var (ok, error) = Store.NewGroup(name);
        if (!ok)
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), error ?? L10n.Tr("L.Msg_CreateFailed"));
            return;
        }
        Log(L10n.TrF("L.Log_NewGroup", name));
        RefreshGroupsList();
    }

    [RelayCommand]
    private void RenameGroup(string? newName)
    {
        var group = Store.Current;
        if (group is null || string.IsNullOrWhiteSpace(newName) || newName == group.Name)
            return;
        if (Store.GroupNameExists(newName, group))
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), L10n.Tr("L.Msg_DuplicateGroup"), warning: true);
            return;
        }
        Store.Snapshot();
        var (ok, error) = Store.RenameGroup(group.Name, newName);
        if (!ok)
        {
            NotifyUser(L10n.Tr("L.Title_Tip"), error ?? L10n.Tr("L.Msg_RenameFailed"));
            return;
        }
        Log(L10n.TrF("L.Log_Renamed", group.Name, newName));
        RefreshGroupsList();
    }

    [RelayCommand]
    private void DeleteGroup()
    {
        var group = Store.Current;
        if (group is null)
            return;
        Store.Snapshot();
        Store.DeleteGroup(group.Name);
        Log(L10n.TrF("L.Log_DeletedGroup", group.Name));
        SelectedGroupIndex = -1;
        RefreshGroupsList();
        RefreshTree();
    }

    public void ImportFiles(IEnumerable<string> files)
    {
        var any = false;
        var failures = new List<string>();
        foreach (var file in files)
        {
            if (!SliderGroupFile.TryLoad(file, out var imported, out var error))
            {
                failures.Add($"{Path.GetFileName(file)}：{error}");
                continue;
            }
            if (!any)
            {
                Store.Snapshot();
                any = true;
            }
            var (addedGroups, addedMembers) = Store.Import(imported);
            Log(L10n.TrF("L.Log_Imported", file, addedGroups, addedMembers));
        }
        if (any)
        {
            RefreshGroupsList();
            RefreshTree();
        }
        if (failures.Count > 0)
            NotifyUser(L10n.Tr("L.Title_Error"), L10n.Tr("L.Msg_ImportFailed") + string.Join("\n", failures), warning: true);
    }
}
