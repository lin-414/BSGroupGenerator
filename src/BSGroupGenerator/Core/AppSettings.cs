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
