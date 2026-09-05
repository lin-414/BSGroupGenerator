# -*- coding: utf-8 -*-
# MainForm 接入 GroupStore：分组状态与操作全部委托 Store
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:80]!r}"
    s = s.replace(old, new)

# 1) 字段
rep(
    '''    private readonly List<SliderGroup> _groups = new();
    private bool _dirty;''',
    '''    private readonly GroupStore _store = new();''')

# 2) FormClosing 脏检查
rep(
    '''        _filterDebounce.Stop();
        if (_dirty)
        {''',
    '''        _filterDebounce.Stop();
        if (_store.Dirty)
        {''')

# 3) SelectedIndexChanged：同步 Store 当前组
rep(
    '''        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) UpdateMembershipMarks(); };''',
    '''        _lstGroups.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingUi) return;
            _store.SelectGroup(SelectedGroupName);
            UpdateMembershipMarks();
        };''')

# 4) CurrentGroup 属性改为经 Store 查询
rep(
    '''    private SliderGroup? CurrentGroup =>
        _lstGroups.SelectedIndex >= 0 && _lstGroups.SelectedIndex < _groups.Count
            ? _groups[_lstGroups.SelectedIndex]
            : null;''',
    '''    private string? SelectedGroupName =>
        _lstGroups.SelectedIndex >= 0 && _lstGroups.SelectedIndex < _store.Count
            ? _store.Groups[_lstGroups.SelectedIndex].Name
            : null;

    private SliderGroup? CurrentGroup =>
        SelectedGroupName is null ? null : _store.GetGroup(SelectedGroupName);''')

# 5) ApplyCheckedToCurrentGroup：委托 Store
rep(
    '''    private void ApplyCheckedToCurrentGroup(bool add)
    {
        var group = CurrentGroup;
        if (group is null)
        {
            MessageBox.Show(this, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
''',
    '''    private void ApplyCheckedToCurrentGroup(bool add)
    {
        if (CurrentGroup is null)
        {
            MessageBox.Show(this, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
''')

rep(
    '''        Snapshot();
        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
        _dirty = true;''',
    '''        _store.ApplyToCurrent(names, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{_store.Current!.Name}\\"（现成员 {_store.Current!.Members.Count} 个）。");''')

# 6) ApplyScanOutcome 载入
rep(
    '''        if (!_dirty && _groups.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _groups.AddRange(outcome.ExistingGroups);
            _dirty = false;
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");
        }''',
    '''        if (!_store.Dirty && _store.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _store.Load(outcome.ExistingGroups);
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_store.Count} 个组。");
        }''')

# 7) IsInAnyGroup / GroupNameExists 委托
rep(
    '''    private bool IsInAnyGroup(string outfit)
    {
        foreach (var group in _groups)
        {
            if (group.Members.Contains(outfit, StringComparer.Ordinal))
                return true;
        }
        return false;
    }''',
    '''    private bool IsInAnyGroup(string outfit) => _store.IsInAnyGroup(outfit);''')

rep(
    '''    private bool GroupNameExists(string name, SliderGroup? except = null) =>
        _groups.Any(g => !ReferenceEquals(g, except) &&
                         string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));''',
    '''    private bool GroupNameExists(string name, SliderGroup? except = null) =>
        _store.GroupNameExists(name, except);''')

# 8) Undo / Snapshot 委托
rep(
    '''    private void Snapshot()
    {
        _undoStack.Push(_groups.Select(g => g.Clone()).ToList());
        while (_undoStack.Count > 30)
            _undoStack.Pop();
    }''',
    '''    private void Snapshot() => _store.Snapshot();''')

rep(
    '''    private void Undo()
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
    }''',
    '''    private void Undo()
    {
        var (ok, error) = _store.Undo();
        if (!ok)
        {
            MessageBox.Show(this, error, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Log("已撤销上一步分组操作。");
        RefreshGroupsList();
        RefreshTree();
        UpdateCounts();
    }''')

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('part1 ok')
