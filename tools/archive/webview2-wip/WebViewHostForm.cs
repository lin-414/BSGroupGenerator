using System.Runtime.InteropServices;
using BSGroupGenerator.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BSGroupGenerator.UI;

/// <summary>
/// 无边框宿主窗体：内嵌 WebView2 渲染全部界面（wwwroot 嵌入资源，经 https://app.local/ 提供）。
/// 缩放边缘由 WM_NCHITTEST 处理；标题栏拖拽优先用 WebView2 的 app-region 支持，旧运行时回退到消息拖拽。
/// </summary>
public class WebViewHostForm : Form
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCLIENT = 0x1;
    private const int HTCAPTION = 0x2;
    private const int HTLEFT = 0xA;
    private const int HTRIGHT = 0xB;
    private const int HTTOP = 0xC;
    private const int HTTOPLEFT = 0xD;
    private const int HTTOPRIGHT = 0xE;
    private const int HTBOTTOM = 0xF;
    private const int HTBOTTOMLEFT = 0x10;
    private const int HTBOTTOMRIGHT = 0x11;
    private const int EDGE = 8;

    private readonly WebView2 _web = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(22, 22, 25),
    };
    private readonly AppController _controller = new();
    private Bridge? _bridge;
    private bool _initialized;

    public WebViewHostForm()
    {
        Text = "BS Group Generator — BodySlide 分组生成工具";
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1000, 660);
        ClientSize = new Size(1320, 860);
        StartPosition = FormStartPosition.CenterScreen;
        ApplyThemeColors();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            try { BeginInvoke(ApplyThemeColors); } catch { /* 已关闭 */ }
        };
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 提取失败时使用默认图标
        }

        Controls.Add(_web);
        ApplyChrome();

        Load += async (_, _) => await InitWebViewAsync();
        Resize += (_, _) =>
        {
            ApplyChrome();
            _bridge?.PushMaximized(WindowState == FormWindowState.Maximized);
        };
        FormClosing += OnFormClosingGuard;
    }

    // ── WebView2 初始化 ───────────────────────────────────────────────
    private async Task InitWebViewAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BSGroupGenerator", "WebView2");

        CoreWebView2Environment env;
        try
        {
            env = await CoreWebView2Environment.CreateAsync(null, userData);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "未检测到 WebView2 运行时，无法显示界面。\n" +
                "请安装 Microsoft Edge WebView2 常青运行时后重试：\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                ex.Message,
                "缺少 WebView2 运行时", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        await _web.EnsureCoreWebView2Async(env);
        var core = _web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        try
        {
            // 让 app-region: drag 的网页区域具备系统标题栏能力（拖拽/双击最大化/右键系统菜单）
            core.Settings.IsNonClientRegionSupportEnabled = true;
        }
        catch
        {
            // 旧运行时无此属性：回退到消息拖拽
        }

        core.AddWebResourceRequestedFilter("https://app.local/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;

        _bridge = new Bridge(_controller, _web,
            WindowAction, BeginDrag, () => WindowState == FormWindowState.Maximized);
        _controller.DialogOwner = this;
        _controller.Marshal = action => BeginInvoke(action);

        core.Navigate("https://app.local/index.html");
    }

    // ── 嵌入资源 → https://app.local/ ─────────────────────────────────
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        var name = uri.AbsolutePath == "/" ? "index.html" : uri.AbsolutePath.TrimStart('/');
        var stream = GetResource(name);
        if (stream is null)
        {
            e.Response = _web.CoreWebView2.Environment.CreateWebResourceResponse(
                new MemoryStream("\"404\""u8.ToArray()), 404, "Not Found", "Content-Type: text/plain");
            return;
        }

        var contentType = name switch
        {
            var n when n.EndsWith(".html", StringComparison.OrdinalIgnoreCase) => "text/html; charset=utf-8",
            var n when n.EndsWith(".css", StringComparison.OrdinalIgnoreCase) => "text/css; charset=utf-8",
            var n when n.EndsWith(".js", StringComparison.OrdinalIgnoreCase) => "text/javascript; charset=utf-8",
            var n when n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) => "image/svg+xml",
            _ => "application/octet-stream",
        };
        e.Response = _web.CoreWebView2.Environment.CreateWebResourceResponse(
            stream, 200, "OK", $"Content-Type: {contentType}");
    }

    private static Stream? GetResource(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Contains(".."))
            return null;
        var fullName = "BSGroupGenerator.wwwroot." + name.Replace('/', '.');
        return typeof(WebViewHostForm).Assembly.GetManifestResourceStream(fullName);
    }

    // ── 窗口行为 ──────────────────────────────────────────────────────
    private void WindowAction(string action)
    {
        switch (action)
        {
            case "min":
                WindowState = FormWindowState.Minimized;
                break;
            case "max":
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
                break;
            case "close":
                Close();
                break;
        }
    }

    public void BeginDrag()
    {
        ReleaseCapture();
        _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    private bool SystemLight()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is 1;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyThemeColors()
    {
        BackColor = SystemLight() ? Color.FromArgb(238, 240, 244) : Color.FromArgb(22, 22, 25);
    }

    private void ApplyChrome()
    {
        // 最大化时系统会给窗口加一圈不可见的缩放边距，收起内边距避免内容被裁
        Padding = WindowState == FormWindowState.Maximized ? Padding.Empty : new Padding(6);
        try
        {
            // Win11 圆角
            var preference = 2; // DWMWCP_ROUND
            _ = DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        }
        catch
        {
            // Win10 无此属性
        }
    }

    private void OnFormClosingGuard(object? sender, FormClosingEventArgs e)
    {
        if (_controller.Dirty)
        {
            var choice = MessageBox.Show(this,
                "当前有未保存的分组修改，直接退出会丢失。\n\n是：保存并退出\n否：不保存，直接退出\n取消：留在程序",
                "未保存的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (choice == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (choice == DialogResult.Yes && !_controller.TrySave(showSuccessDialog: false))
            {
                e.Cancel = true; // 保存失败（如写入出错），留在程序处理
                return;
            }
        }
        _controller.SaveSettings();
    }

    // ── 缩放边缘 ──────────────────────────────────────────────────────
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            // 只有窗体自身区域（含 6px 内边距）会走到这里；WebView2 子控件的命中不会经过
            var x = (short)(m.LParam.ToInt32() & 0xFFFF);
            var y = (short)(m.LParam.ToInt32() >> 16);
            var pt = PointToClient(new Point(x, y));
            var size = ClientSize;

            var hit = HTCLIENT;
            var left = pt.X <= EDGE;
            var right = pt.X >= size.Width - EDGE;
            var topEdge = pt.Y <= EDGE;
            var bottomEdge = pt.Y >= size.Height - EDGE;

            if (topEdge && left) hit = HTTOPLEFT;
            else if (topEdge && right) hit = HTTOPRIGHT;
            else if (bottomEdge && left) hit = HTBOTTOMLEFT;
            else if (bottomEdge && right) hit = HTBOTTOMRIGHT;
            else if (left) hit = HTLEFT;
            else if (right) hit = HTRIGHT;
            else if (topEdge) hit = HTTOP;
            else if (bottomEdge) hit = HTBOTTOM;

            if (hit != HTCLIENT)
            {
                m.Result = (IntPtr)hit;
                return;
            }
        }
        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
