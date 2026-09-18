using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using BSGroupGenerator.Wpf.Views;
using Xunit;

namespace BSGroupGenerator.Wpf.Tests;

/// <summary>帮助窗口的目录超链接必须真的能跳。
///
/// 起因是一次真实崩溃：<c>Hyperlink.NavigateUri</c> 是**相对** URI（<c>"#sec1"</c>），而处理函数读了
/// <c>e.Uri.Fragment</c> —— <c>Uri.Fragment</c> 对相对 URI 会抛 InvalidOperationException
/// （"This operation is not supported for a relative URI"），于是目录里每一条链接点下去都弹一次错误框。
///
/// 这里把每条链接的 RequestNavigate 真发一遍。**同时断言 Handled 已置位**——否则万一处理函数压根没被
/// 调用（例如事件名写错），测试会「因为什么都没发生」而假通过。
///
/// 与 MainWindowCommandTests 同属 WpfSta 集合：两者都要构造真实窗口，而 WPF 的 Application
/// 是进程级单例、只能构造一次。并行跑会有一个实例撞在 InvalidOperationException 上。</summary>
[Collection(WpfStaCollection.Name)]
public class HelpWindowTests
{
    [Fact]
    public void TocLinksNavigateWithoutThrowing()
    {
        Exception? captured = null;
        var handled = new List<bool>();

        // WPF 窗口只能在 STA 线程上构造，而 xUnit 默认跑在 MTA 线程池线程上。
        var thread = new Thread(() =>
        {
            try
            {
                // HelpWindow.xaml 用了 {StaticResource AppFont} 与 {StaticResource AppWindow}，
                // 解析时必须在 Application 资源里能找到。（L10n.Tr 没有语言字典时回落到键名，不影响本用例。）
                var app = Application.Current ?? new Application();
                if (app.Resources.MergedDictionaries.Count == 0)
                {
                    // pack URI 必须带程序集名，否则按**入口程序集**（测试宿主）解析而找不到
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/BSGroupGenerator;component/Themes/Controls.xaml"),
                    });
                }

                var window = new HelpWindow();
                var doc = FindDocument(window);
                Assert.NotNull(doc);

                var links = doc!.Blocks.OfType<Paragraph>()
                    .SelectMany(p => p.Inlines.OfType<Hyperlink>())
                    .ToList();
                Assert.NotEmpty(links);

                foreach (var link in links)
                {
                    var args = new RequestNavigateEventArgs(link.NavigateUri!, "help")
                    {
                        RoutedEvent = Hyperlink.RequestNavigateEvent,
                    };
                    link.RaiseEvent(args);
                    handled.Add(args.Handled);
                }
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
        Assert.NotEmpty(handled);
        Assert.All(handled, h => Assert.True(h, "目录链接的 RequestNavigate 没有被处理"));
    }

    private static FlowDocument? FindDocument(DependencyObject root)
    {
        if (root is FlowDocumentScrollViewer viewer && viewer.Document is not null)
            return viewer.Document;
        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject d && FindDocument(d) is { } found)
                return found;
        return null;
    }
}
