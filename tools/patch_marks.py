# -*- coding: utf-8 -*-
# 切换分组不再重建树：就地更新成员标注，保留展开/滚动/勾选，方便连续快速分组
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:70]!r}"
    s = s.replace(old, new)

# 1) 切组 → 就地更新标注
rep(
    '        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) RefreshTree(); };',
    '        _lstGroups.SelectedIndexChanged += (_, _) => { if (!_loadingUi) UpdateMembershipMarks(); };')

# 2) 新增就地更新方法（放在 ApplyCheckedToCurrentGroup 之前）
rep(
    '    /// <summary>把左侧勾选的内容（分隔符/模组/服装）应用到当前组。</summary>',
    '''    /// <summary>
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
                    node.ForeColor = inGroup > 0 ? MemberGreen : SystemColors.WindowText;
                }
            }
        }
        finally
        {
            _tree.EndUpdate();
        }
        UpdateGroupInfo();
    }

    /// <summary>把左侧勾选的内容（分隔符/模组/服装）应用到当前组。</summary>''')

# 3) 应用后：就地更新（仅看未分配模式例外，需要重建）
rep(
    '''        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
        _dirty = true;

        // 重建树：显示新的成员关系，勾选自然清空
        RefreshTree();''',
    '''        foreach (var name in names)
            ApplyMembership(group, name, add);
        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
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
        }''')

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
