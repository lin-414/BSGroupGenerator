using System.Diagnostics;
using System.Windows;
using BSGroupGenerator.Wpf.ViewModels;

namespace BSGroupGenerator.Wpf.Views;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/lin-414/BSGroupGenerator";

    public AboutWindow()
    {
        InitializeComponent();
        var version = typeof(AboutWindow).Assembly.GetName().Version;
        VersionLabel.Text = "v" + (version is null ? "?" : version.ToString(3));
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => MainViewModel.OpenUrl(RepoUrl);

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
