using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>
/// 预览某个组内的全部服装：与主界面一样按分隔符 → 模组分层级显示，
/// 可按服装/模组/分隔符名过滤，勾选成员后可批量移出。
/// </summary>
public class GroupMembersDialog : Form
{
    private static readonly Font BoldFont = new("Microsoft YaHei UI", 9F, FontStyle.Bold);

    private readonly SliderGroup _group;
    private readonly List<(string? Separator, string Owner, List<string> Outfits)> _structure;
    private readonly Action _beforeChange;
    private readonly Action _onChanged;
    private readonly TextBox _txtFilter = new() { Width = 260, PlaceholderText = "过滤服装 / 模组 / 分隔符…" };
    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        CheckBoxes = true,
        ShowLines = false,
        FullRowSelect = true,
        ItemHeight = 20,
        BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly Label _lblCount = new()
    {
        Dock = DockStyle.Bottom,
        Height = 22,
        ForeColor = SystemColors.GrayText,
        Padding = new Padding(4, 2, 2, 0),
    };
    private bool _updatingChecks;

    public GroupMembersDialog(SliderGroup group,
        List<(string? Separator, string Owner, List<string> Outfits)> structure,
        Action beforeChange, Action onChanged)
    {
        _group = group;
        _structure = structure;
        _beforeChange = beforeChange;
        _onChanged = onChanged;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }
        Text = $"组「{group.Name}」的成员";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 660);
        MinimumSize = new Size(500, 420);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(6, 8, 6, 2),
            WrapContents = false,
        };
        _txtFilter.Margin = new Padding(2, 2, 10, 2);
        top.Controls.Add(_txtFilter);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(6, 6, 6, 6),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var btnClose = new Button { Text = "关闭", MinimumSize = new Size(84, 30), DialogResult = DialogResult.Cancel };
        var btnRemove = new Button { Text = "移出所选", MinimumSize = new Size(100, 30) };
        btnRemove.Click += (_, _) => RemoveChecked();
        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(btnRemove);

        Controls.Add(_tree);
        Controls.Add(top);
        Controls.Add(_lblCount);
        Controls.Add(bottom);
        CancelButton = btnClose;

        _tree.BeforeCheck += (_, e) =>
        {
            if (e.Node?.Tag is string tag &&
                (tag.StartsWith("O:", StringComparison.Ordinal) ||
                 tag.StartsWith("M:", StringComparison.Ordinal) ||
                 tag.StartsWith("S:", StringComparison.Ordinal)))
                return;
            e.Cancel = true;
        };
        _tree.AfterCheck += (_, e) =>
        {
            if (_updatingChecks || e.Node?.Tag is not string tag)
                return;
            _updatingChecks = true;
            try
            {
                if (tag.StartsWith("M:", StringComparison.Ordinal))
                {
                    foreach (TreeNode child in e.Node.Nodes)
                        child.Checked = e.Node.Checked;
                }
                else if (tag.StartsWith("S:", StringComparison.Ordinal))
                {
                    foreach (var modNode in Walk(e.Node.Nodes)
                                 .Where(n => n.Tag is string t && t.StartsWith("M:", StringComparison.Ordinal)))
                    {
                        modNode.Checked = e.Node.Checked;
                        foreach (TreeNode child in modNode.Nodes)
                            child.Checked = e.Node.Checked;
                    }
                }
            }
            finally
            {
                _updatingChecks = false;
            }
        };
        _txtFilter.TextChanged += (_, _) => Rebuild();
        Rebuild();
    }

    private void Rebuild()
    {
        var filter = _txtFilter.Text.Trim();
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var shown = 0;
        try
        {
            // 连续相同名称的分隔符共用同一个节点（一个分隔符下有很多模组时，避免标题重复几十次）
            TreeNode? currentSepNode = null;
            string? currentSepName = null;
            var started = false;

            foreach (var (separator, owner, outfits) in _structure)
            {
                var memberOutfits = outfits
                    .Where(o => _group.Members.Contains(o, StringComparer.Ordinal))
                    .ToList();
                if (memberOutfits.Count == 0)
                    continue;

                // 过滤：模组名或分隔符名命中 → 显示该模组全部成员；否则只显示命中的服装
                var modVisible = filter.Length == 0
                                 || owner.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                 || (separator is not null &&
                                     separator.Contains(filter, StringComparison.OrdinalIgnoreCase));
                var visibleOutfits = modVisible
                    ? memberOutfits
                    : memberOutfits.Where(o => o.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                if (visibleOutfits.Count == 0)
                    continue;

                if (!started || currentSepName != separator)
                {
                    currentSepName = separator;
                    started = true;
                    currentSepNode = separator is not null
                        ? new TreeNode(separator)
                        {
                            Tag = "S:",
                            ForeColor = SystemColors.GrayText,
                            NodeFont = BoldFont,
                        }
                        : null;
                    if (currentSepNode is not null)
                        _tree.Nodes.Add(currentSepNode);
                }

                var modNode = new TreeNode($"{owner}　({visibleOutfits.Count})")
                {
                    Tag = "M:",
                    NodeFont = BoldFont,
                };
                foreach (var outfit in visibleOutfits)
                    modNode.Nodes.Add(new TreeNode(outfit) { Tag = "O:" + outfit });

                if (currentSepNode is not null)
                    currentSepNode.Nodes.Add(modNode);
                else
                    _tree.Nodes.Add(modNode);

                if (filter.Length > 0)
                {
                    modNode.Expand();
                    currentSepNode?.Expand();
                }
                shown += visibleOutfits.Count;
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
        _lblCount.Text = $"组内共 {_group.Members.Count} 个服装，当前显示 {shown} 个";
    }

    private void RemoveChecked()
    {
        var selected = Walk(_tree.Nodes)
            .Where(n => n.Checked && n.Tag is string t && t.StartsWith("O:", StringComparison.Ordinal))
            .Select(n => (n.Tag as string)![2..])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "请先勾选要移出的服装（可勾选模组或分隔符批量全选）。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"确定把 {selected.Count} 个服装移出组「{_group.Name}」？", "确认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        _beforeChange();
        foreach (var name in selected)
            _group.Members.RemoveAll(m => m == name);
        _onChanged();
        Rebuild();
    }

    private static IEnumerable<TreeNode> Walk(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Nodes))
                yield return child;
        }
    }
}
