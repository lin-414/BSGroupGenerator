# -*- coding: utf-8 -*-
# 帮助窗口接线：菜单项 + F1
import io

p = 'src/BSGroupGenerator/UI/MainForm.cs'
s = io.open(p, encoding='utf-8').read()

def rep(old, new, count=1):
    global s
    assert s.count(old) == count, f"match {s.count(old)} != {count}: {old[:70]!r}"
    s = s.replace(old, new)

# F1 打开帮助：开启 KeyPreview 并挂 KeyDown
rep(
    '        Text = "BS Group Generator — BodySlide 分组生成工具";',
    '''        Text = "BS Group Generator — BodySlide 分组生成工具";
        KeyPreview = true;''')

rep(
    '        Load += (_, _) => ReloadInstances();',
    '''        Load += (_, _) => ReloadInstances();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F1)
            {
                e.Handled = true;
                new HelpDialog().ShowDialog(this);
            }
        };''')

# 使用说明：MessageBox 整块替换为帮助窗口
start = s.find('        mnuUsage.Click += (_, _) => MessageBox.Show(this,')
marker = '"使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);'
end = s.find(marker, start)
assert start != -1 and end != -1 and start < end, (start, end)
s = s[:start] + '        mnuUsage.Click += (_, _) => new HelpDialog().ShowDialog(this);' + s[end + len(marker):]

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
