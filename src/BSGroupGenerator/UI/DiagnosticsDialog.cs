namespace BSGroupGenerator.UI;

/// <summary>诊断信息窗口：显示实例发现、路径解析等细节，便于对照真实环境排查。</summary>
public class DiagnosticsDialog : Form
{
    public DiagnosticsDialog(string report)
    {
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }
        Text = "诊断信息";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 560);
        MinimizeBox = false;

        var txt = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            Text = report,
        };

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var btnClose = new Button { Text = "关闭", Size = new Size(90, 28), DialogResult = DialogResult.Cancel };
        var btnCopy = new Button { Text = "复制全部", Size = new Size(90, 28) };
        btnCopy.Click += (_, _) =>
        {
            Clipboard.SetText(txt.Text);
            btnCopy.Text = "已复制";
        };
        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(btnCopy);

        Controls.Add(txt);
        Controls.Add(bottom);
        CancelButton = btnClose;
    }
}
