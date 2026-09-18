namespace BSGroupGenerator.Core;

/// <summary>
/// 发现 MO2 实例，方式与 MO2 自身一致（instancemanager.cpp）：
/// 全局实例 = %LOCALAPPDATA%\ModOrganizer 下每个包含 ModOrganizer.ini 的子目录；
/// 便携实例 = ModOrganizer.exe 旁的 ModOrganizer.ini。
/// </summary>
public static class Mo2Discovery
{
    /// <summary>测试可替换的全局实例根目录（null = 真实 <c>%LOCALAPPDATA%\ModOrganizer</c>）。
    /// 单测必须能挡住对用户真实 MO2 目录的枚举，否则用例结果取决于跑测试那台机器装了什么。</summary>
    public static string? GlobalRootOverride { get; set; }

    public static string GlobalRoot => GlobalRootOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModOrganizer");

    public static List<Mo2Instance> Discover(IEnumerable<string> extraDirs)
    {
        var found = new List<Mo2Instance>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(GlobalRoot))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(GlobalRoot))
                {
                    var ini = Path.Combine(dir, "ModOrganizer.ini");
                    if (File.Exists(ini) && seen.Add(dir))
                        found.Add(new Mo2Instance(dir, ini, Mo2InstanceKind.Global));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // 枚举失败（权限不足、磁盘/设备错误）就跳过这个根目录：Discover 在 ViewModel 的
                // 构造函数链里被调用，这里抛出去等于"启动即崩"，而少列几个实例最多是重新指一次目录
            }
        }

        foreach (var dir in CommonPortableLocations())
        {
            TryAdd(found, seen, dir, Mo2InstanceKind.Portable);
        }

        foreach (var dir in extraDirs)
        {
            TryAdd(found, seen, dir, Mo2InstanceKind.Manual);
        }

        return found;
    }

    /// <summary>把用户浏览的目录转为实例（实例目录或便携安装根目录皆可）。</summary>
    public static Mo2Instance? CreateFromDirectory(string dir)
    {
        var list = new List<Mo2Instance>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAdd(list, seen, dir, Mo2InstanceKind.Manual);
        return list.FirstOrDefault();
    }

    private static void TryAdd(List<Mo2Instance> list, HashSet<string> seen, string dir, Mo2InstanceKind kind)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;

        // 判据只有 ModOrganizer.ini。便携安装的 ini 就写在 exe 同级，与上面这条是**同一个路径**，
        // 所以"只有 ModOrganizer.exe 没有 ini"的目录不登记（MO2 自己也不认这种目录为实例）。
        var ini = Path.Combine(dir, "ModOrganizer.ini");
        if (File.Exists(ini) && seen.Add(dir))
            list.Add(new Mo2Instance(dir, ini, kind));
    }

    private static IEnumerable<string> CommonPortableLocations()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        yield return Path.Combine(programFiles, "Mod Organizer 2");
        yield return Path.Combine(programFilesX86, "Mod Organizer 2");
        yield return Path.Combine(documents, "Mod Organizer 2");
    }
}
