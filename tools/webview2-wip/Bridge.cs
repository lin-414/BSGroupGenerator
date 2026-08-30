using System.Text.Json;
using BSGroupGenerator.Core;
using Microsoft.Web.WebView2.Core;

namespace BSGroupGenerator.UI;

/// <summary>
/// WebView2 消息桥：分发前端命令、向页面推送状态快照与日志。
/// 命令协议集中定义在 Handle 中；纯界面逻辑（过滤/勾选/展开）在前端本地处理。
/// </summary>
public class Bridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly AppController _controller;
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private readonly Action<string> _windowAction;
    private readonly Action _beginDrag;
    private readonly Func<bool> _isMaximized;

    public Bridge(AppController controller, Microsoft.Web.WebView2.WinForms.WebView2 webView,
        Action<string> windowAction, Action beginDrag, Func<bool> isMaximized)
    {
        _isMaximized = isMaximized;
        _controller = controller;
        _webView = webView;
        _windowAction = windowAction;
        _beginDrag = beginDrag;

        _controller.StateChanged += PushState;
        _controller.Logged += line => Push(new { type = "log", line });
        _controller.ScanActiveChanged += active => Push(new { type = "scanning", active });
        _webView.WebMessageReceived += OnWebMessage;
    }

    public void PushState() => Push(_controller.BuildState());

    public void PushMaximized(bool value) => Push(new { type = "maximized", value });

    private void Push(object message)
    {
        try
        {
            if (_webView.IsDisposed || _webView.CoreWebView2 is null)
                return;
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions));
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // WebView2 尚未就绪或已释放：丢弃本次推送
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            Handle(doc.RootElement);
        }
        catch (Exception ex)
        {
            Log($"命令处理失败：{ex.Message}");
        }
    }

    private void Handle(JsonElement m)
    {
        var type = m.TryGetProperty("type", out var t) ? t.GetString() : null;
        switch (type)
        {
            case "ready":
                _controller.Init();
                PushState();
                Push(new { type = "maximized", value = _isMaximized() });
                break;

            case "selectInstance":
                _controller.SelectInstance(Str(m, "dir"));
                break;
            case "refreshInstances":
                _controller.RefreshInstances();
                break;
            case "addMo2Dir":
                _controller.AddMo2Dir();
                break;
            case "selectProfile":
                _controller.SelectProfile(Str(m, "name"));
                break;
            case "selectBodySlide":
                _controller.SelectBodySlide(Str(m, "dir"));
                break;
            case "detectBodySlide":
                _controller.DetectBodySlide();
                break;
            case "browseBodySlide":
                _controller.BrowseBodySlide();
                break;
            case "setWriteMode":
                _controller.SetWriteMode(Str(m, "mode"));
                break;
            case "browseTargetDir":
                _controller.BrowseTargetDir();
                break;

            case "newGroup":
                _controller.NewGroup(Str(m, "name"));
                break;
            case "renameGroup":
                _controller.RenameGroup(Str(m, "old"), Str(m, "new"));
                break;
            case "deleteGroup":
                _controller.DeleteGroup(Str(m, "name"));
                break;
            case "selectGroup":
                _controller.SelectGroup(Str(m, "name"));
                break;
            case "applyToGroup":
            {
                var names = m.GetProperty("names").EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => x.Length > 0)
                    .ToList();
                _controller.ApplyToGroup(names, m.GetProperty("add").GetBoolean());
                break;
            }

            case "save":
                _controller.TrySave(showSuccessDialog: true);
                break;
            case "importGroups":
                _controller.ImportGroups();
                break;
            case "diagnostics":
                Push(new { type = "diagnostics", text = _controller.GetDiagnostics() });
                break;
            case "copyText":
                try { Clipboard.SetText(Str(m, "text")); } catch { /* 剪贴板占用时忽略 */ }
                break;

            case "window":
                _windowAction(Str(m, "action"));
                break;
            case "drag":
                _beginDrag();
                break;

            default:
                Log($"未知命令：{type}");
                break;
        }
    }

    private static string Str(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

    private void Log(string line) => Push(new { type = "log", line });
}
