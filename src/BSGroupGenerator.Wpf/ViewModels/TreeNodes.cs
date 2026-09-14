using System.Collections.ObjectModel;
using BSGroupGenerator.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BSGroupGenerator.Wpf.ViewModels;

public enum NodeKind { Separator, Mod, Outfit }

/// <summary>树节点 VM：分隔符(S)/模组(M)/服装(O) 三层。
/// 勾选语义：叶子 = true/false；容器（分隔符/模组）按子节点聚合——全选 true、全不选 false、
/// 部分选中 null（复选框显示半选态）。用户勾选容器向下级联；勾选/取消子节点向上聚合。</summary>
public partial class NodeVM : ObservableObject
{
    public NodeVM(NodeKind kind) => Kind = kind;

    public NodeKind Kind { get; }
    public NodeVM? Parent { get; set; }
    public ObservableCollection<NodeVM> Children { get; } = new();

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool? _isChecked;
    [ObservableProperty] private bool _isMember;
    [ObservableProperty] private bool _isSeparator;
    [ObservableProperty] private bool _isConflict;
    [ObservableProperty] private bool _isPlaceholder;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
                OnExpanded();
        }
    }

    /// <summary>首次展开时物化子节点（模组懒加载）。</summary>
    protected virtual void OnExpanded() { }

    /// <summary>双击行标题展开/折叠（与 WinForms 版行为一致）。</summary>
    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    private bool _cascading;

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_cascading)
            return;
        CascadeDown(value == true);
        Parent?.RefreshAggregated();
    }

    /// <summary>用户勾选后向下级联：容器把整棵子树置为同一布尔值（占位节点跳过，null 不会来自用户点击）。</summary>
    private void CascadeDown(bool value)
    {
        foreach (var child in Children)
        {
            if (child.IsPlaceholder)
                continue;
            child._cascading = true;
            child.IsChecked = value;
            child.CascadeDown(value);
            child._cascading = false;
        }
    }

    /// <summary>子节点状态变化后，自底向上重算祖先的聚合勾选态。
    /// 公开给手动重建/剪枝子树的窗口（新装模组弹窗等）在操作后调用。</summary>
    public void RefreshAggregated()
    {
        _cascading = true;
        IsChecked = ComputeChecked();
        _cascading = false;
        Parent?.RefreshAggregated();
    }

    private bool? ComputeChecked()
    {
        var real = Children.Where(c => !c.IsPlaceholder).ToList();
        if (real.Count == 0)
            return IsChecked; // 未物化的模组等：子态未知，保持原值
        if (real.All(c => c.IsChecked == true))
            return true;
        if (real.All(c => c.IsChecked == false))
            return false;
        return null; // 混合（含半选的子容器）
    }

    public IEnumerable<NodeVM> WalkSelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var n in child.WalkSelfAndDescendants())
                yield return n;
    }
}

public sealed class SeparatorNodeVM : NodeVM
{
    public SeparatorNodeVM(string title) : base(NodeKind.Separator)
    {
        Text = title;
        IsSeparator = true;
        IsChecked = false;
    }
}

public sealed class OutfitNodeVM : NodeVM
{
    public OutfitNodeVM(string outfitName, bool hasConflict, bool isMember, bool isChecked)
        : base(NodeKind.Outfit)
    {
        OutfitName = outfitName;
        HasConflict = hasConflict;
        Text = (isMember ? "✔ " : "") + (hasConflict ? outfitName + "（同名冲突）" : outfitName);
        IsMember = isMember;
        IsConflict = hasConflict;
        IsChecked = isChecked;
    }

    public string OutfitName { get; }
    public bool HasConflict { get; }
}

/// <summary>模组节点：懒加载——挂一个空占位子节点让展开箭头出现，首次展开才物化服装行。</summary>
public sealed class ModNodeVM : NodeVM
{
    private readonly Func<string, bool> _isMember;
    private bool _materialized;

    public ModNodeVM(string header, string owner, List<OutfitEntry> visibleOutfits,
        Func<string, bool> isMember, bool allMember) : base(NodeKind.Mod)
    {
        Owner = owner;
        Outfits = visibleOutfits;
        _isMember = isMember;
        Text = header;
        IsMember = allMember && visibleOutfits.Count > 0;
        IsChecked = allMember && visibleOutfits.Count > 0;
        Children.Add(new NodeVM(NodeKind.Outfit) { IsPlaceholder = true }); // 占位
    }

    public string Owner { get; }
    public List<OutfitEntry> Outfits { get; }

    public void Rebadge(string header, bool allMember)
    {
        Text = header;
        IsMember = allMember && Outfits.Count > 0;
    }

    /// <summary>子节点是手动构建（而非懒物化）时调用，防止首次展开被清空。</summary>
    public void MarkMaterialized() => _materialized = true;

    protected override void OnExpanded()
    {
        if (_materialized)
            return;
        _materialized = true;
        Children.Clear();
        foreach (var outfit in Outfits)
            Children.Add(new OutfitNodeVM(outfit.Name, outfit.HasConflict, _isMember(outfit.Name), IsChecked == true)
            {
                Parent = this,
            });
    }
}
