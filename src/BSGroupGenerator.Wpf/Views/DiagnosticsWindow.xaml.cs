using System.Windows;
using BSGroupGenerator.Wpf.Services;

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
        Notify.Info(this, L10n.Tr("L.Diag_Title"), L10n.Tr("L.Msg_CopiedToClipboard"));
    }
}
