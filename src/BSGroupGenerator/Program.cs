namespace BSGroupGenerator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ReportCrash(e.Exception, isFatal: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportCrash(e.ExceptionObject as Exception, isFatal: e.IsTerminating);
        Application.Run(new UI.MainForm());
    }

    private static void ReportCrash(Exception? ex, bool isFatal)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BSGroupGenerator");
            Directory.CreateDirectory(dir);
            var crashPath = Path.Combine(dir, "crash.log");
            if (File.Exists(crashPath) && new FileInfo(crashPath).Length > 512 * 1024)
            {
                var text = File.ReadAllText(crashPath);
                File.WriteAllText(crashPath, text[(text.Length / 2)..]); // 超过 512KB 保留后半
            }
            File.AppendAllText(crashPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(isFatal ? "致命" : "UI")}异常：\n{ex}\n\n");
        }
        catch
        {
            // 日志失败不影响提示
        }

        MessageBox.Show(
            isFatal
                ? $"发生未处理的错误，程序即将退出。\n详细信息已写入 %APPDATA%\\BSGroupGenerator\\crash.log\n\n{ex?.Message}"
                : $"发生了一个错误，已忽略（详情见 %APPDATA%\\BSGroupGenerator\\crash.log）。\n\n{ex?.Message}",
            isFatal ? "错误" : "提示",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
