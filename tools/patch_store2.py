# -*- coding: utf-8 -*-
# MainForm 接入 GroupStore：分组状态与操作全部委托 Store
import io

def patch(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{path}: match {s.count(old)} != 1: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print(path, 'ok')

p = 'src/BSGroupGenerator/UI/MainForm.cs'
pairs = []
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

# 3) SelectedIndexChanged 同步 Store
rep(
    '''        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) UpdateMembershipMarks(); };''',
    '''        _lstGroups.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingUi) return;
            _store.SelectGroup(SelectedGroupName);
            UpdateMembershipMarks();
        };''')

# 4) CurrentGroup 属性
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

# 5) ApplyChecked 头部
rep(
    '''        var group = CurrentGroup;
        if (group is null)
        {
            MessageBox.Show(this, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
''',
    '''        if (CurrentGroup is null)
        {
            MessageBox.Show(this, "请先在右侧新建或选中一个组。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
''')

# 6) Apply 尾部委托 Store
rep(
    '''        Snapshot();
        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
        _dirty = true;''',
    '''        _store.ApplyToCurrent(names, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{_store.Current!.Name}\\"（现成员 {_store.Current!.Members.Count} 个）。");''')

# 7) ApplyScanOutcome 载入
rep(
    '''        if (!_dirty && _groups.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _groups.AddRange(outcome.ExistingGroups);
            _dirty = false;
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");
        }''',
    '''        if (_store.Count == 0 && !_store.Dirty && outcome.ExistingGroups.Count > 0)
        {
            _store.Load(outcome.ExistingGroups);
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_store.Count} 个组。");
        }''')

# 8) IsInAnyGroup
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

# 9) Undo
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

# 10) Snapshot
rep(
    '''    private void Snapshot()
    {
        _undoStack.Push(_groups.Select(g => g.Clone()).ToList());
        while (_undoStack.Count > 30)
            _undoStack.Pop();
    }''',
    '''    private void Snapshot() => _store.Snapshot();''')

# 11) OnNewGroup
rep(
    '''        Snapshot();
        _dirty = true;
        _groups.Add(new SliderGroup(name));
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        _lstGroups.SelectedIndex = _groups.Count - 1;
        Log($"新建组：{name}");''',
    '''        var (ok, error) = _store.NewGroup(name);
        if (!ok)
        {
            MessageBox.Show(this, error, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Log($"新建组：{name}");
        RefreshGroupsList();''')

# 12) OnRenameGroup
rep(
    '''        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        group.Name = name;
        _dirty = true;
        RefreshGroupsList();''',
    '''        var (ok, error) = _store.RenameGroup(group.Name, name);
        if (!ok)
        {
            MessageBox.Show(this, error, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        RefreshGroupsList();''')

# 13) OnDeleteGroup
rep(
    '''        _groups.Remove(group);
        Log($"删除组：{group.Name}");
        _dirty = true;
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        RefreshTree();''',
    '''        _store.DeleteGroup(group.Name);
        Log($"删除组：{group.Name}");
        _lstGroups.SelectedIndex = -1;
        RefreshGroupsList();
        RefreshTree();''')

# 14) OnImportGroups
rep(
    '''        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);
        if (addedGroups + addedMembers > 0)
            _dirty = true;''',
    '''        var (addedGroups, addedMembers) = _store.Import(imported);''')

# 15) GroupMembersDialog 快照
rep(
    '''        new GroupMembersDialog(group, GetTreeDisplayStructure(), Snapshot, RefreshAfterGroupChange).ShowDialog(this);''',
    '''        new GroupMembersDialog(group, GetTreeDisplayStructure(), _store.Snapshot, RefreshAfterGroupChange).ShowDialog(this);''')

# 16) TrySaveGroups 用 Store 集合
rep(
    '''        if (_groups.Count == 0)
        {
            MessageBox.Show(this, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Any(g => g.Name.Trim().Length == 0))''',
    '''        if (_store.Count == 0)
        {
            MessageBox.Show(this, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_store.Groups.Any(g => g.Name.Trim().Length == 0))''')

rep(
    '''        foreach (var group in _groups)
        {
            var fileName = SliderGroupFile.FileNameForGroup(group.Name);''',
    '''        foreach (var group in _store.Groups)
        {
            var fileName = SliderGroupFile.FileNameForGroup(group.Name);''')

rep(
    '''            foreach (var group in _groups)
                SliderGroupFile.Save(Path.Combine(dir, fileByGroup[group]), new[] { group });''',
    '''            foreach (var group in _store.Groups)
                SliderGroupFile.Save(Path.Combine(dir, fileByGroup[group]), new[] { group });''')

rep(
    '''        _dirty = false;
        var memberCount = _groups.Sum(g => g.Members.Count);
        var fileExamples = string.Join("、", _groups.Take(3).Select(g => SliderGroupFile.FileNameForGroup(g.Name)));
        Log($"已保存 {dir} 下 {_groups.Count} 个组文件（共 {memberCount} 个成员，{target.Value.Description}）。");''',
    '''        _store.MarkSaved();
        var memberCount = _store.Groups.Sum(g => g.Members.Count);
        var fileExamples = string.Join("、", _store.Groups.Take(3).Select(g => SliderGroupFile.FileNameForGroup(g.Name)));
        Log($"已保存 {dir} 下 {_store.Count} 个组文件（共 {memberCount} 个成员，{target.Value.Description}）。");''')

rep(
    '''            $"已保存 {_groups.Count} 个组到目录：\\n{dir}\\n\\n" +
            $"每个组一个文件，文件名即组名：{fileExamples}{(_groups.Count > 3 ? " …" : "")}\\n" +''',
    '''            $"已保存 {_store.Count} 个组到目录：\\n{dir}\\n\\n" +
            $"每个组一个文件，文件名即组名：{fileExamples}{(_store.Count > 3 ? " …" : "")}\\n" +''')

# 17) RuleApply 委托 Store
rep(
    '''        var group = _groups.FirstOrDefault(g => g.Name == groupName);
        if (group is null)
            return -1;
        var includeKw = GroupRules.SplitKeywords(include);
        var excludeKw = GroupRules.SplitKeywords(exclude);
        var ownerByOutfit = OwnerByOutfit();
        Snapshot();
        if (_scan is null)
            return -1;

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
        return applied;''',
    '''        var includeKw = GroupRules.SplitKeywords(include);
        var excludeKw = GroupRules.SplitKeywords(exclude);
        var ownerByOutfit = OwnerByOutfit();
        if (_scan is null)
            return -1;
        var matched = _scan.Outfits
            .Where(o => RuleHits(o.Name, ownerByOutfit, includeKw, excludeKw, matchOwner, unassignedOnly))
            .Select(o => o.Name)
            .ToList();

        var applied = _store.ApplyToGroup(groupName, matched, add);
        Log($"规则归组：{applied} 个服装{(add ? "加入" : "移出")}组 \\"{groupName}\\"（现成员 {_store.GetGroup(groupName)?.Members.Count ?? 0} 个）。");
        return applied;''')

# 18) OnRules 传 Store 集合
rep(
    '''        using var dialog = new RuleGroupDialog(_groups, CurrentGroup?.Name, ownerMap,
            (include, exclude, matchOwner, unassignedOnly) =>
                RuleMatchPreview(ownerMap, include, exclude, matchOwner, unassignedOnly));''',
    '''        using var dialog = new RuleGroupDialog(_store.Groups, CurrentGroup?.Name, ownerMap,
            (include, exclude, matchOwner, unassignedOnly) =>
                RuleMatchPreview(ownerMap, include, exclude, matchOwner, unassignedOnly));''')

# 19) RefreshGroupsList 用 Store
rep(
    '''        var selectedName = CurrentGroup?.Name;
        _lstGroups.BeginUpdate();
        _lstGroups.Items.Clear();
        foreach (var group in _groups)
            _lstGroups.Items.Add($"{group.Name}　({group.Members.Count})");
        _lstGroups.EndUpdate();
        var index = _groups.FindIndex(g => g.Name == selectedName);
        _lstGroups.SelectedIndex = index >= 0 ? index : (_groups.Count > 0 ? 0 : -1);''',
    '''        var selectedName = CurrentGroup?.Name;
        _lstGroups.BeginUpdate();
        _lstGroups.Items.Clear();
        foreach (var group in _store.Groups)
            _lstGroups.Items.Add($"{group.Name}　({group.Members.Count})");
        _lstGroups.EndUpdate();
        var index = _store.Groups.ToList().FindIndex(g => g.Name == selectedName);
        _lstGroups.SelectedIndex = index >= 0 ? index : (_store.Count > 0 ? 0 : -1);''')

# 20) RefreshAfterGroupChange 脏标记经 Store
rep(
    '''    private void RefreshAfterGroupChange()
    {
        _dirty = true;''',
    '''    private void RefreshAfterGroupChange()
    {
        _store.MarkDirty();''')

patch(p, pairs)
