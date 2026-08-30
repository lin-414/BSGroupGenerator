namespace BSGroupGenerator.Core;

public enum Mo2InstanceKind
{
    Global,
    Portable,
    Manual,
}

/// <summary>
/// 一个 MO2 实例（全局实例目录或便携安装）。
/// ini 键名与 MO2 2.4.x / 2.5.x 源码一致：[Settings] base_directory / mod_directory / profiles_directory，
/// [General] gameName / gamePath / selected_profile。
/// </summary>
public class Mo2Instance
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    public string Name { get; }
    public string InstanceDir { get; }
    public string IniPath { get; }
    public Mo2InstanceKind Kind { get; }

    public Mo2Instance(string instanceDir, string iniPath, Mo2InstanceKind kind, string? name = null)
    {
        InstanceDir = Path.GetFullPath(instanceDir).TrimEnd('\\', '/');
        IniPath = iniPath;
        Kind = kind;
        Name = string.IsNullOrEmpty(name) ? Path.GetFileName(InstanceDir.TrimEnd('\\', '/')) : name;
        _sections = File.Exists(iniPath)
            ? IniParser.ParseFile(iniPath)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    public string DisplayName
    {
        get
        {
            var kind = Kind switch
            {
                Mo2InstanceKind.Portable => "便携",
                Mo2InstanceKind.Global => "全局",
                _ => "手动",
            };
            var game = string.IsNullOrEmpty(GameName) ? "未配置游戏" : GameName;
            return $"{Name}（{kind} · {game}）";
        }
    }

    public string BaseDirectory
    {
        get
        {
            var value = Get("Settings", "base_directory");
            if (string.IsNullOrWhiteSpace(value))
                return InstanceDir;
            return Path.GetFullPath(value.Replace("%BASE_DIR%", InstanceDir));
        }
    }

    public string ModsDirectory
    {
        get
        {
            var value = Get("Settings", "mod_directory");
            return ResolveDir(value, "mods");
        }
    }

    public string ProfilesDirectory
    {
        get
        {
            var value = Get("Settings", "profiles_directory");
            return ResolveDir(value, "profiles");
        }
    }

    public string OverwriteDirectory
    {
        get
        {
            var value = Get("Settings", "overwrite_directory");
            return ResolveDir(value, "overwrite");
        }
    }

    public string GameName => Get("General", "gameName");
    public string GamePath => Get("General", "gamePath");
    public string SelectedProfile => Get("General", "selected_profile");

    /// <summary>实例内所有 profile 名（目录下含 modlist.txt）。</summary>
    public List<string> GetProfiles()
    {
        var result = new List<string>();
        var dir = ProfilesDirectory;
        if (!Directory.Exists(dir))
            return result;
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            if (File.Exists(Path.Combine(sub, "modlist.txt")))
                result.Add(Path.GetFileName(sub));
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public string GetModListPath(string profile) => Path.Combine(ProfilesDirectory, profile, "modlist.txt");

    public bool IsValid => File.Exists(IniPath) || Directory.Exists(ModsDirectory);

    private string ResolveDir(string? configured, string defaultFolder)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(BaseDirectory, defaultFolder);
        return Path.GetFullPath(configured.Replace("%BASE_DIR%", BaseDirectory));
    }

    private string Get(string section, string key)
    {
        if (_sections.TryGetValue(section, out var entries) && entries.TryGetValue(key, out var value))
            return value;
        return "";
    }
}
