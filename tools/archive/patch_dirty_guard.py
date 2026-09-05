# -*- coding: utf-8 -*-
# 退出保护：未保存修改时拦截 + OnGenerate 改为可复用的 TrySaveGroups
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:70]!r}"
    s = s.replace(old, new)

# 1) 脏标记字段
rep(
    "    private readonly List<SliderGroup> _groups = new();",
    "    private readonly List<SliderGroup> _groups = new();\n    private bool _dirty;")

# 2) 退出保护挂接
rep(
    "        Load += (_, _) => ReloadInstances();\n        FormClosing += (_, _) => _settings.Save();",
    "        Load += (_, _) => ReloadInstances();\n        FormClosing += OnFormClosingGuard;")

rep(
    '    private void Log(string message) =>',
    '''    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        if (_dirty)
        {
            var choice = MessageBox.Show(this,
                "当前有未保存的分组修改，直接退出会丢失。\\n\\n是：保存并退出\\n否：不保存，直接退出\\n取消：留在程序",
                "未保存的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (choice == DialogResult.Yes && !TrySaveGroups(showSuccessDialog: false))
            {
                e.Cancel = true; // 保存失败（如写入出错），留在程序处理
                return;
            }
        }
        _settings.Save();
    }

    private void Log(string message) =>''')

# 3) OnGenerate → TrySaveGroups
rep(
    '''    private void OnGenerate(object? sender, EventArgs e)
    {
        var target = ResolveWriteTarget();
        if (target is null)
        {
            MessageBox.Show(this, "尚未完成扫描或无法确定写入位置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_groups.Count == 0)
        {
            MessageBox.Show(this, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_groups.Any(g => g.Name.Trim().Length == 0))
        {
            MessageBox.Show(this, "存在空名称的组，请先重命名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }''',
    '''    private void OnGenerate(object? sender, EventArgs e) => TrySaveGroups(showSuccessDialog: true);

    private bool TrySaveGroups(bool showSuccessDialog)
    {
        var target = ResolveWriteTarget();
        if (target is null)
        {
            MessageBox.Show(this, "尚未完成扫描或无法确定写入位置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Count == 0)
        {
            MessageBox.Show(this, "当前没有任何组。请先新建组并勾选服装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (_groups.Any(g => g.Name.Trim().Length == 0))
        {
            MessageBox.Show(this, "存在空名称的组，请先重命名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }''')

rep(
    '''                    "文件名冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;''',
    '''                    "文件名冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;''')

rep(
    '''        catch (Exception ex)
        {
            Log($"写入失败：{ex.Message}");
            MessageBox.Show(this, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var memberCount = _groups.Sum(g => g.Members.Count);''',
    '''        catch (Exception ex)
        {
            Log($"写入失败：{ex.Message}");
            MessageBox.Show(this, $"写入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _dirty = false;
        var memberCount = _groups.Sum(g => g.Members.Count);''')

rep(
    '''        var customNote2 = _settings.WriteMode == WriteMode.Custom''',
    '''        if (!showSuccessDialog)
            return true;

        var customNote2 = _settings.WriteMode == WriteMode.Custom''')

# 4) 各处修改置脏
rep(
    '''        // 重建树并刷新组列表的成员计数
        RefreshTree();''',
    '''        _dirty = true;

        // 重建树并刷新组列表的成员计数
        RefreshTree();''')

rep(
    '''        _groups.Add(new SliderGroup(name));
        _lstGroups.SelectedIndex = -1;''',
    '''        _dirty = true;
        _groups.Add(new SliderGroup(name));
        _lstGroups.SelectedIndex = -1;''')

rep(
    '''        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        group.Name = name;''',
    '''        Log($"组 \\"{group.Name}\\" 重命名为 \\"{name}\\"。");
        group.Name = name;
        _dirty = true;''')

rep(
    '''        _groups.Remove(group);
        Log($"删除组：{group.Name}");''',
    '''        _groups.Remove(group);
        Log($"删除组：{group.Name}");
        _dirty = true;''')

rep(
    '''        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);
        Log($"从 {dialog.FileName} 导入：新增 {addedGroups} 个组、{addedMembers} 个成员。");''',
    '''        SliderGroupFile.Merge(_groups, imported, out var addedGroups, out var addedMembers);
        if (addedGroups + addedMembers > 0)
            _dirty = true;
        Log($"从 {dialog.FileName} 导入：新增 {addedGroups} 个组、{addedMembers} 个成员。");''')

rep(
    '''    private void RefreshAfterGroupChange()
    {
        if (CurrentGroup is { } group)
            Log($"组 \\"{group.Name}\\" 现有成员 {group.Members.Count} 个。");''',
    '''    private void RefreshAfterGroupChange()
    {
        _dirty = true;
        if (CurrentGroup is { } group)
            Log($"组 \\"{group.Name}\\" 现有成员 {group.Members.Count} 个。");''')

# 5) 扫描载入旧组后视为干净状态
rep(
    '''        if (_groups.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _groups.AddRange(outcome.ExistingGroups);
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");
        }''',
    '''        if (_groups.Count == 0 && outcome.ExistingGroups.Count > 0)
        {
            _groups.AddRange(outcome.ExistingGroups);
            _dirty = false;
            Log($"已从 {outcome.TargetDir} 载入上次生成的 {_groups.Count} 个组。");
        }''')

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('patch ok')
