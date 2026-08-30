using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace BSGroupGenerator.Core;

public class SliderGroup
{
    public string Name { get; set; } = "";
    public List<string> Members { get; set; } = new();

    public SliderGroup() { }

    public SliderGroup(string name) => Name = name;

    public SliderGroup(string name, IEnumerable<string> members) : this(name) => Members.AddRange(members);

    /// <summary>深拷贝（撤销快照用）。</summary>
    public SliderGroup Clone() => new(Name, Members);
}

/// <summary>
/// BodySlide 分组文件（&lt;SliderGroups&gt; → &lt;Group name&gt; → &lt;Member name&gt;）的读写。
/// 写出格式与 BodySlide 自身一致：UTF-8 带 BOM、XML 声明、缩进。
/// 注意：BodySlide 的成员匹配是大小写敏感的精确比较，这里原样保存字符串。
/// </summary>
public static class SliderGroupFile
{
    public const string DefaultFileName = "BSGroupGenerator.xml";
    public const string ManifestFileName = "BSGroupGenerator.files.txt";

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>组名转安全文件名（非法字符替换为下划线、Windows 保留名加前缀、自动补 .xml）。</summary>
    public static string FileNameForGroup(string groupName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in groupName.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var name = sb.ToString().TrimEnd('.', ' ');
        if (name.Length == 0)
            name = "未命名组";
        var stem = name.Contains('.') ? name[..name.IndexOf('.')] : name;
        if (ReservedDeviceNames.Contains(stem))
            name = "_" + name; // Windows 保留设备名（CON/NUL/COM1…）连同任意扩展名都禁用
        return name + ".xml";
    }

    public static bool TryLoad(string path, out List<SliderGroup> groups, out string error)
    {
        groups = new List<SliderGroup>();
        try
        {
            groups = Load(path);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static List<SliderGroup> Load(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.None);
        var root = doc.Root ?? throw new InvalidDataException("空文件");
        if (root.Name.LocalName != "SliderGroups")
            throw new InvalidDataException("缺少 <SliderGroups> 根元素，不是 BodySlide 分组文件");

        var groups = new List<SliderGroup>();
        foreach (var groupElement in root.Elements("Group"))
        {
            var name = (string?)groupElement.Attribute("name");
            if (string.IsNullOrEmpty(name))
                continue;
            var group = new SliderGroup(name);
            foreach (var member in groupElement.Elements("Member"))
            {
                var memberName = (string?)member.Attribute("name");
                if (!string.IsNullOrEmpty(memberName) && !group.Members.Contains(memberName, StringComparer.Ordinal))
                    group.Members.Add(memberName);
            }
            groups.Add(group);
        }
        return groups;
    }

    public static void Save(string path, IEnumerable<SliderGroup> groups)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("SliderGroups",
                groups.Select(g => new XElement("Group",
                    new XAttribute("name", g.Name),
                    g.Members.Select(m => new XElement("Member", new XAttribute("name", m)))))));

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            OmitXmlDeclaration = false,
        };
        using var writer = XmlWriter.Create(path, settings);
        doc.Save(writer);
    }

    /// <summary>合并导入（组名按不区分大小写匹配，保留先出现的写法；成员按精确去重）。</summary>
    public static void Merge(List<SliderGroup> target, IEnumerable<SliderGroup> source,
        out int addedGroups, out int addedMembers)
    {
        addedGroups = 0;
        addedMembers = 0;

        foreach (var incoming in source)
        {
            var existing = target.FirstOrDefault(g =>
                string.Equals(g.Name, incoming.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new SliderGroup(incoming.Name);
                target.Add(existing);
                addedGroups++;
            }

            foreach (var member in incoming.Members)
            {
                if (!existing.Members.Contains(member, StringComparer.Ordinal))
                {
                    existing.Members.Add(member);
                    addedMembers++;
                }
            }
        }
    }
}
