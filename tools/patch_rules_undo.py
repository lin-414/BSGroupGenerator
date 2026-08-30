# -*- coding: utf-8 -*-
# 规则归组 + 撤销 + 发布清理
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

# ── Core/SliderGroupFile.cs：SliderGroup 支持拷贝 ──
patch('src/BSGroupGenerator/Core/SliderGroupFile.cs', [(
'''public class SliderGroup
{
    public string Name { get; set; } = "";
    public List<string> Members { get; } = new();

    public SliderGroup() { }

    public SliderGroup(string name) => Name = name;
}''',
'''public class SliderGroup
{
    public string Name { get; set; } = "";
    public List<string> Members { get; set; } = new();

    public SliderGroup() { }

    public SliderGroup(string name) => Name = name;

    public SliderGroup(string name, IEnumerable<string> members) : this(name) => Members.AddRange(members);

    /// <summary>深拷贝（撤销快照用）。</summary>
    public SliderGroup Clone() => new(Name, Members);
}''')])

# ── UI/MainForm.cs ──
pairs = []

# 字段
pairs.append((
'''    private readonly Button _btnViewMembers = new() { Text = "查看组", MinimumSize = new Size(84, 30) };''',
'''    private readonly Button _btnViewMembers = new() { Text = "查看组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnRules = new() { Text = "规则归组", MinimumSize = new Size(84, 30) };
    private readonly Button _btnUndo = new() { Text = "撤销", MinimumSize = new Size(100, 32) };
    private readonly Stack<List<SliderGroup>> _undoStack = new();'''))

# 接线
pairs.append((
'''        _btnImport.Click += OnImportGroups;
        _btnViewMembers.Click += OnViewMembers;''',
'''        _btnImport.Click += OnImportGroups;
        _btnViewMembers.Click += OnViewMembers;
        _btnRules.Click += OnRules;
        _btnUndo.Click += (_, _) => Undo();'''))

# 菜单：撤销
pairs.append((
'''        mnuFile.DropDownItems.Add(mnuSave);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());''',
'''        var mnuUndo = new ToolStripMenuItem("撤销分组修改(&Z)") { ShortcutKeys = Keys.Control | Keys.Z };
        mnuUndo.Click += (_, _) => Undo();
        mnuFile.DropDownItems.Add(mnuUndo);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add(mnuSave);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());'''))

# 布局：按钮行
pairs.append((
'''        _btnAddToGroup.Margin = new Padding(0, 2, 8, 2);
        _btnRemoveFromGroup.Margin = new Padding(0, 2, 0, 2);
        applyButtons.Controls.AddRange([_btnAddToGroup, _btnRemoveFromGroup]);''',
'''        _btnAddToGroup.Margin = new Padding(0, 2, 8, 2);
        _btnRemoveFromGroup.Margin = new Padding(0, 2, 8, 2);
        _btnUndo.Margin = new Padding(0, 2, 0, 2);
        applyButtons.Controls.AddRange([_btnAddToGroup, _btnRemoveFromGroup, _btnUndo]);'''))
pairs.append((
'''        _btnNewGroup.Margin = new Padding(0, 2, 8, 2);
        _btnRenameGroup.Margin = new Padding(0, 2, 8, 2);
        _btnDeleteGroup.Margin = new Padding(0, 2, 8, 2);
        _btnViewMembers.Margin = new Padding(0, 2, 0, 2);
        groupButtons.Controls.AddRange([_btnNewGroup, _btnRenameGroup, _btnDeleteGroup, _btnViewMembers]);''',
'''        _btnNewGroup.Margin = new Padding(0, 2, 8, 2);
        _btnRenameGroup.Margin = new Padding(0, 2, 8, 2);
        _btnDeleteGroup.Margin = new Padding(0, 2, 8, 2);
        _btnViewMembers.Margin = new Padding(0, 2, 8, 2);
        _btnRules.Margin = new Padding(0, 2, 0, 2);
        groupButtons.Controls.AddRange([_btnNewGroup, _btnRenameGroup, _btnDeleteGroup, _btnViewMembers, _btnRules]);'''))

# 撤销快照调用点
pairs.append((
'''        foreach (var name in names)
            ApplyMembership(group, name, add);''',
'''        Snapshot();
        foreach (var name in names)
            ApplyMembership(group, name, add);'''))
pairs.append((
'''        _dirty = true;
        _groups.Add(new SliderGroup(name));''',
'''        Snapshot();
        _dirty = true;
        _groups.Add(new SliderGroup(name));'''))
pairs.append((
'''        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        group.Name = name;''',
'''        Snapshot();
        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        group.Name = name;'''))
pairs.append((
'''        _groups.Remove(group);
        Log($"删除组：{group.Name}");''',
'''        Snapshot();
        _groups.Remove(group);
        Log($"删除组：{group.Name}");'''))
pairs.append((
'''        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);''',
'''        Snapshot();
        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);'''))

# 查看组对话框挂快照回调
pairs.append((
'''        new GroupMembersDialog(group, GetTreeDisplayStructure(), RefreshAfterGroupChange).ShowDialog(this);''',
'''        new GroupMembersDialog(group, GetTreeDisplayStructure(), Snapshot, RefreshAfterGroupChange).ShowDialog(this);'''))

# 撤销 / 规则归组方法
pairs.append((
'''    /// <summary>按左侧树的结构返回显示数据：分隔符 → 模组 → 服装（供查看组窗口使用）。</summary>''',
'''    private void Snapshot()
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
        Log($"规则归组：{applied} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
        return applied;
    }

    private Dictionary<string, string> OwnerByOutfit()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, owner, outfits) in GetTreeDisplayStructure())
            foreach (var outfit in outfits)
                map.TryAdd(outfit.Name, owner);
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

    /// <summary>按左侧树的结构返回显示数据：分隔符 → 模组 → 服装（供查看组窗口使用）。</summary>'''))

patch('src/BSGroupGenerator/UI/MainForm.cs', pairs)

# ── UI/GroupMembersDialog.cs：快照回调 ──
patch('src/BSGroupGenerator/UI/GroupMembersDialog.cs', [(
'''    private readonly SliderGroup _group;
    private readonly List<(string? Separator, string Owner, List<string> Outfits)> _structure;
    private readonly Action _onChanged;''',
'''    private readonly SliderGroup _group;
    private readonly List<(string? Separator, string Owner, List<string> Outfits)> _structure;
    private readonly Action _beforeChange;
    private readonly Action _onChanged;'''),(
'''    public GroupMembersDialog(SliderGroup group,
        List<(string? Separator, string Owner, List<string> Outfits)> structure, Action onChanged)
    {
        _group = group;
        _structure = structure;
        _onChanged = onChanged;''',
'''    public GroupMembersDialog(SliderGroup group,
        List<(string? Separator, string Owner, List<string> Outfits)> structure,
        Action beforeChange, Action onChanged)
    {
        _group = group;
        _structure = structure;
        _beforeChange = beforeChange;
        _onChanged = onChanged;'''),(
'''        foreach (var name in selected)
            _group.Members.RemoveAll(m => m == name);
        _onChanged();
        Rebuild();''',
'''        _beforeChange();
        foreach (var name in selected)
            _group.Members.RemoveAll(m => m == name);
        _onChanged();
        Rebuild();''')])
