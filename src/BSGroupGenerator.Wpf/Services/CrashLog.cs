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
        System.Windows.MessageBox.Show(
            isFatal
                ? $"发生未处理的错误，程序即将退出。\n详细信息已写入 %APPDATA%\\BSGroupGenerator\\crash.log\n\n{ex?.Message}"
                : $"发生了一个错误，已忽略（详情见 %APPDATA%\\BSGroupGenerator\\crash.log）。\n\n{ex?.Message}",
            isFatal ? "错误" : "提示",
            System.Windows.MessageBoxButton.OK,
            isFatal ? System.Windows.MessageBoxImage.Error : System.Windows.MessageBoxImage.Warning);
    }
}
