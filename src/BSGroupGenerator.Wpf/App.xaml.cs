using System.Threading;
using BSGroupGenerator.Wpf.Services;
using BSGroupGenerator.Wpf.Views;
using System.Windows;

namespace BSGroupGenerator.Wpf;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 按设置加载色板与语言。既须在 StartupUri 窗口创建前完成，也须早于下面的单实例提示
        //（提示文案走 L10n，语言尚未装载时会回落到键名）
        var settings = Core.AppSettings.Load();
        ThemeManager.Apply(settings.UiTheme);
        L10n.Apply(settings.UiLanguage);

        // 单实例：与 WinForms 版共用同一个名字，两个副本会互相覆盖 settings.json 与分组文件
        _mutex = new Mutex(initiallyOwned: true, @"Local\BSGroupGenerator.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(L10n.Tr("L.Msg_AlreadyRunning"), L10n.Tr("L.Title_Tip"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.ShowError(args.Exception, isFatal: false);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.ShowError(args.ExceptionObject as Exception, isFatal: args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write(args.Exception, isFatal: false);
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { // 非持有线程退出时忽略
        }
        base.OnExit(e);
    }
}
