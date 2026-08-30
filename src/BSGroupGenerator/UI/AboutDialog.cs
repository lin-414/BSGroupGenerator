using System.Diagnostics;

namespace BSGroupGenerator.UI;

/// <summary>关于窗口：版本、作者、仓库链接与简介。</summary>
public class AboutDialog : Form
{
    private const string RepoUrl = "https://github.com/lin-414/BSGroupGenerator";

    public AboutDialog()
    {
        Text = "关于 BS Group Generator";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 378);

        var pic = new PictureBox
        {
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 14, 0),
        };
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            pic.Image = icon?.ToBitmap();
        }
        catch
        {
            // 提取失败时留空
        }

        var name = new Label
        {
            Text = "BS Group Generator",
            Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        var version = new Label
        {
            // ProductVersion 形如 "0.1.0+<提交哈希>"，只展示语义版本
            Text = "v" + Application.ProductVersion.Split('+')[0],
            Font = new Font("Microsoft YaHei UI", 9f),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0),
        };

        var nameStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
        };
        nameStack.Controls.Add(name);
        nameStack.Controls.Add(version);

        var header = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
        };
        header.Controls.Add(pic);
        header.Controls.Add(nameStack);

        var description = new Label
        {
            Text = "读取 Mod Organizer 2 安装的模组，把 BodySlide 滑块组批量划进分组，" +
                   "生成原生格式的分组文件。支持天际 SE/AE、辐射 4 等所有 BodySlide 支持的游戏。",
            AutoSize = true,
            MaximumSize = new Size(452, 0),
            Margin = new Padding(0, 14, 0, 6),
        };

        var repoLink = new LinkLabel
        {
            Text = "GitHub：github.com/lin-414/BSGroupGenerator",
            AutoSize = true,
            LinkColor = Color.FromArgb(79, 140, 255),
            Margin = new Padding(0, 0, 0, 4),
        };
        repoLink.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = RepoUrl, UseShellExecute = true });
            }
            catch
            {
                // 浏览器打开失败时忽略
            }
        };

        var author = new Label
        {
            Text = "作者：lin-414",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        };

        var compat = new Label
        {
            Text = "分组文件与 BodySlide 完全兼容；每个组生成一个以组名命名的独立文件，" +
                   "存放在 BodySlide 的 SliderGroups 目录。",
            AutoSize = true,
            MaximumSize = new Size(452, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 0),
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(22, 20, 22, 8),
        };
        flow.Controls.Add(header);
        flow.Controls.Add(description);
        flow.Controls.Add(repoLink);
        flow.Controls.Add(author);
        flow.Controls.Add(compat);

        var btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
            Height = 38,
        };

        Controls.Add(flow);
        Controls.Add(btnOk);
        AcceptButton = btnOk;
        CancelButton = btnOk;
    }
}
