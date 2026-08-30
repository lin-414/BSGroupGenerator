using BSGroupGenerator.Core;

namespace BSGroupGenerator.UI;

/// <summary>简单输入框（新建/重命名组用）。</summary>
public static class InputDialog
{
    public static string? Show(IWin32Window? owner, string title, string label, string initial = "")
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(380, 120),
            ShowInTaskbar = false,
        };
        try
        {
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }

        var lbl = new Label
        {
            Text = label,
            Location = new Point(12, 12),
            Size = new Size(356, 20),
        };
        var txt = new TextBox
        {
            Location = new Point(12, 38),
            Size = new Size(356, 24),
            Text = initial,
        };
        var ok = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Location = new Point(212, 76),
            Size = new Size(75, 28),
        };
        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(293, 76),
            Size = new Size(75, 28),
        };

        form.Controls.AddRange([lbl, txt, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) => { txt.SelectAll(); txt.Focus(); };

        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
