using System.IO;

namespace BSGroupGenerator.Wpf.Services;

/// <summary>崩溃日志：与 WinForms 版相同的 %APPDATA%\BSGroupGenerator\crash.log 与格式。</summary>
public static class CrashLog
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BSGroupGenerator");
    private static string Path2 => System.IO.Path.Combine(Dir, "crash.log");

    public static void Write(Exception? ex, bool isFatal)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(Path2) && new FileInfo(Path2).Length > 512 * 1024)
            {
                var text = File.ReadAllText(Path2);
                File.WriteAllText(Path2, text[(text.Length / 2)..]); // 超过 512KB 保留后半
            }
            File.AppendAllText(Path2,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(isFatal ? "致命" : "UI")}异常：\n{ex}\n\n");
        }
        catch
        {
            // 日志失败不影响提示
        }
    }

    public static void ShowError(Exception? ex, bool isFatal)
    {
        Write(ex, isFatal);
        // 文案走 L10n：崩溃处理器可能在 Application 已拆解后才触发，
        // Tr/TrF 在无 Application 上下文时回落到键名，不会二次抛异常。
        // 异常消息直接作为 TrF 的实参传入即可，不要转义花括号——string.Format 只解析
        // 格式串（即资源值），实参里的花括号是字面量，转义反而会在弹框里显示成 {{ }}。
        // 真正的风险在资源值本身含不配对的括号，已由 wpf-l10n-verify 的格式串校验拦截。
        System.Windows.MessageBox.Show(
            L10n.TrF(isFatal ? "L.Crash_Fatal" : "L.Crash_NonFatal", ex?.Message ?? ""),
            L10n.Tr(isFatal ? "L.Title_Error" : "L.Title_Tip"),
            System.Windows.MessageBoxButton.OK,
            isFatal ? System.Windows.MessageBoxImage.Error : System.Windows.MessageBoxImage.Warning);
    }
}
