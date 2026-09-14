using System.Windows;

namespace BSGroupGenerator.Wpf.Views;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(string report)
    {
        InitializeComponent();
        ReportBox.Text = report;
    }

    private void Report_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ReportBox.ScrollToEnd();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ReportBox.Text);
        MessageBox.Show(this, "已复制到剪贴板。", "诊断信息", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
