namespace BSGroupGenerator.Core;

/// <summary>极简 INI 解析器，覆盖 MO2 的 ModOrganizer.ini（QSettings 格式）所需的部分。</summary>
public static class IniParser
{
    public static Dictionary<string, Dictionary<string, string>> ParseFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd());
    }

    public static Dictionary<string, Dictionary<string, string>> Parse(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sections[""] = current;

        foreach (var raw in content.Split('\r', '\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                if (!sections.TryGetValue(name, out var section))
                {
                    section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[name] = section;
                }
                current = section;
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            current[key] = value;
        }

        return sections;
    }
}
