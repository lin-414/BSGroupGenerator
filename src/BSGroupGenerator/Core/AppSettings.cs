using System.Text.Json;
using System.Text.Json.Serialization;

namespace BSGroupGenerator.Core;

public enum WriteMode
{
    /// <summary>按有效项目路径自动选择写入位置。</summary>
    Auto = 0,
    /// <summary>写到 BodySlide.exe 旁的 SliderGroups。</summary>
    BodySlideDir = 1,
    /// <summary>写到 MO2 专用小模组（mods\BS Group Generator\CalienteTools\BodySlide\SliderGroups）。</summary>
    Mo2Mod = 2,
    /// <summary>写到游戏真实 Data（GameDataPath\CalienteTools\BodySlide\SliderGroups）。</summary>
    RealGameData = 3,
    /// <summary>写到用户通过"浏览…"指定的任意路径。</summary>
    Custom = 4,
}

public class AppSettings
{
    public List<string> ExtraMo2Dirs { get; set; } = new();
    public string? LastInstanceDir { get; set; }
    public string? LastProfile { get; set; }
    public string? LastBodySlideDir { get; set; }
    public string? CustomTargetDir { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WriteMode WriteMode { get; set; } = WriteMode.Auto;

    /// <summary>上次扫描时含服装的模组（OwnerLabel）基线。已改用 KnownOwnersByProfile，仅作旧数据迁移用。</summary>
    public List<string>? KnownOwners { get; set; }

    /// <summary>含服装模组基线，按 "实例目录|Profile" 隔离——切换 Profile 不会误报新模组。</summary>
    public Dictionary<string, List<string>> KnownOwnersByProfile { get; set; } = new();

    /// <summary>规则归组预设：可在新装模组弹窗里一键批量执行。</summary>
    public List<RulePreset> RulePresets { get; set; } = new();

    /// <summary>界面主题："boutique"（暗色，默认）或 "light"（上一版亮色）。</summary>
    public string UiTheme { get; set; } = "boutique";

    /// <summary>界面语言："zh"（中文，默认）或 "en"。</summary>
    public string UiLanguage { get; set; } = "zh";

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BSGroupGenerator");
    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // 设置损坏时回到默认值即可
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // 忽略保存失败（如目录权限问题）
        }
    }
}
