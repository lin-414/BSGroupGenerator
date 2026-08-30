namespace BSGroupGenerator.Core;

/// <summary>
/// 发现 MO2 实例，方式与 MO2 自身一致（instancemanager.cpp）：
/// 全局实例 = %LOCALAPPDATA%\ModOrganizer 下每个包含 ModOrganizer.ini 的子目录；
/// 便携实例 = ModOrganizer.exe 旁的 ModOrganizer.ini。
/// </summary>
public static class Mo2Discovery
{
    public static string GlobalRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModOrganizer");

    public static List<Mo2Instance> Discover(IEnumerable<string> extraDirs)
    {
        var found = new List<Mo2Instance>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(GlobalRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(GlobalRoot))
            {
                var ini = Path.Combine(dir, "ModOrganizer.ini");
                if (File.Exists(ini) && seen.Add(dir))
                    found.Add(new Mo2Instance(dir, ini, Mo2InstanceKind.Global));
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

        var ini = Path.Combine(dir, "ModOrganizer.ini");
        if (File.Exists(ini))
        {
            if (seen.Add(dir))
                list.Add(new Mo2Instance(dir, ini, kind));
            return;
        }

        // 便携安装：ModOrganizer.exe 同级的 ModOrganizer.ini 才是实例 ini
        var exe = Path.Combine(dir, "ModOrganizer.exe");
        if (File.Exists(exe))
        {
            var portableIni = Path.Combine(dir, "ModOrganizer.ini");
            if (File.Exists(portableIni) && seen.Add(dir))
                list.Add(new Mo2Instance(dir, portableIni, Mo2InstanceKind.Portable));
        }
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
