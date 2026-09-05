# -*- coding: utf-8 -*-
# 补丁第 3 步：应用逻辑尾部改为就地更新
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

old = '''        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");

        _dirty = true;

        // 重建树并刷新组列表的成员计数
        RefreshTree();
        _loadingUi = true;
        try
        {
            RefreshGroupsList();
        }
        finally
        {
            _loadingUi = false;
        }
    }'''

new = '''        Log($"已把 {names.Count} 个服装{(add ? "加入" : "移出")}组 \\"{group.Name}\\"（现成员 {group.Members.Count} 个）。");
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
        }

        _loadingUi = true;
        try
        {
            RefreshGroupsList();
        }
        finally
        {
            _loadingUi = false;
        }
    }'''

assert s.count(old) == 1, s.count(old)
s = s.replace(old, new)
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
