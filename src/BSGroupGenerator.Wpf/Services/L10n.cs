using System.Windows;

namespace BSGroupGenerator.Wpf.Services;

/// <summary>界面语言：向 Application 资源装载 Strings/Lang.zh|en|ru|fr 字典（合并字典后加优先，追加到末尾），
/// 与 ThemeManager 同一套换字典机制，切换对 DynamicResource 绑定即时生效。
/// 代码后台/ViewModel 经 Tr/TrF 取词——Tr 读的是普通字典快照，见 <see cref="Apply"/>。</summary>
public static class L10n
{
    public const string Zh = "zh";
    public const string En = "en";
    public const string Ru = "ru";
    public const string Fr = "fr";

    /// <summary>受支持的语言代码，同时也是 Strings/Lang.*.xaml 的文件名。
    /// 新增语言只需在此追加常量并补一份同名语言文件；Apply 对未知值一律回落到 Zh。</summary>
    public static readonly string[] Supported = [Zh, En, Ru, Fr];

    public static string Current { get; private set; } = Zh;

    /// <summary>当前语言的普通字典快照，只读、整体替换。扫描与 BodySlide 探测在 Task.Run 的后台线程里
    /// 取词，而 ResourceDictionary 属于 WPF 对象树（跨线程读就是线程违规）；快照是不可变引用，
    /// 后台线程读到的要么是完整旧值、要么是完整新值，不存在读到半个字典的中间态。</summary>
    private static volatile Dictionary<string, string>? _snapshot;

    public static void Apply(string? lang)
    {
        Current = Array.Find(Supported, l => string.Equals(l, lang, StringComparison.OrdinalIgnoreCase)) ?? Zh;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Strings/Lang.{Current}.xaml"),
        };

        // ① 合并字典：XAML 侧的 DynamicResource 引用靠它，换语言后自动刷新
        var dicts = Application.Current?.Resources.MergedDictionaries;
        if (dicts is not null)
        {
            for (var i = dicts.Count - 1; i >= 0; i--)
            {
                if (dicts[i].Source?.OriginalString.Contains("/Strings/Lang.", StringComparison.OrdinalIgnoreCase) == true)
                    dicts.RemoveAt(i);
            }
            dicts.Add(dictionary);
        }

        // ② 快照：代码侧（Tr）只读这一份。键用 Ordinal——资源键是 ASCII 标识符，
        // 忽略大小写只会让 L.xxx 与 L.Xxx 这种拼写漂移静默取到值。
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in dictionary.Keys)
        {
            if (key is string name && dictionary[key] is string value)
                snapshot[name] = value;
        }
        _snapshot = snapshot;
    }

    /// <summary>取词；缺键返回键名本身（缺翻译时可见、不空白）。尚未装载语言（单测、设计器）
    /// 或没有 Application 上下文的场景下同样返回键名。</summary>
    public static string Tr(string key) =>
        _snapshot is { } snapshot && snapshot.TryGetValue(key, out var value) ? value : key;

    /// <summary>取词 + 格式化。参数用 object?（而非 object）：格式化实参允许为 null，
    /// string.Format 会把它渲染成空串；若声明为非空 object，所有传入可空值的调用点都会报 CS8604。</summary>
    public static string TrF(string key, params object?[] args) =>
        string.Format(Tr(key), args ?? []);
}
