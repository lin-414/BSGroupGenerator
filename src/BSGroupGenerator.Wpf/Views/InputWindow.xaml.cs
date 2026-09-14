using System.ComponentModel;
using System.Windows;

namespace BSGroupGenerator.Wpf.Views;

/// <summary>简单输入框（新建/重命名组、预设命名用），等价于 WinForms 版 InputDialog。</summary>
public partial class InputWindow : Window
{
    public string Label { get; }
    public string Value { get; set; } = "";

    private InputWindow(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        Label = label;
        Value = initial;
        DataContext = this;
        Loaded += (_, _) => { Box.SelectAll(); Box.Focus(); };
    }

    public static string? Show(Window? owner, string title, string label, string initial = "")
    {
        var win = new InputWindow(title, label, initial);
        if (owner is not null)
            win.Owner = owner;
        return win.ShowDialog() == true ? win.Value.Trim() : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        // 保持 DataContext 同步（TextBox 双向绑定已写回）
        base.OnClosing(e);
    }
}
