using System.Windows;

namespace BSGroupGenerator.Wpf.Services;

/// <summary>界面语言：向 Application 资源装载 Strings/Lang.zh|en 字典（合并字典后加优先，追加到末尾），
/// 与 ThemeManager 同一套换字典机制，切换对 DynamicResource 绑定即时生效。
/// 代码后台/ViewModel 经 Tr/TrF 取词。</summary>
public static class L10n
{
    public const string Zh = "zh";
    public const string En = "en";

    public static string Current { get; private set; } = Zh;

    public static void Apply(string? lang)
    {
        Current = lang == En ? En : Zh;
        var dicts = Application.Current.Resources.MergedDictionaries;
        for (var i = dicts.Count - 1; i >= 0; i--)
        {
            if (dicts[i].Source?.OriginalString.Contains("/Strings/Lang.", StringComparison.OrdinalIgnoreCase) == true)
                dicts.RemoveAt(i);
        }
        dicts.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Strings/Lang.{Current}.xaml"),
        });
    }

    /// <summary>取词；缺键返回键名本身（缺翻译时可见、不空白）。无 Application 上下文（单测、设计器）时同样返回键名。</summary>
    public static string Tr(string key) =>
        Application.Current?.Resources[key] as string ?? key;

    /// <summary>取词 + 格式化。参数用 object?（而非 object）：格式化实参允许为 null，
    /// string.Format 会把它渲染成空串；若声明为非空 object，所有传入可空值的调用点都会报 CS8604。</summary>
    public static string TrF(string key, params object?[] args) =>
        string.Format(Tr(key), args ?? []);
}
