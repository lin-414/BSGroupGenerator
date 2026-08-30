using System.Xml.Linq;

namespace BSGroupGenerator.Core;

public class OutfitEntry
{
    public required string Name { get; init; }
    public required string OwnerLabel { get; init; }
    public required string SourceFile { get; init; }
    /// <summary>同样的滑块组名在更弱的覆盖层（或更后的文件）里也出现过。</summary>
    public bool HasConflict { get; set; }
}

public class ScanResult
{
    public List<OutfitEntry> Outfits { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> LayerNotes { get; } = new();
    public int WinnerFileCount { get; set; }
}

/// <summary>
/// 按 BodySlide 的实际行为扫描服装（滑块组）：
/// 有效项目路径若是虚拟 Data 之下的目录（MO2 常态），则按 modlist 优先级模拟 USVFS 覆盖——
/// 相对路径相同的文件由更强的模组获胜；然后对获胜文件解析 &lt;SliderSet name="..."&gt;，
/// 同名滑块组先见者胜（与 BodySlideApp::LoadSliderSets 一致，成员名大小写敏感、不做任何变换）。
/// </summary>
public static class SliderSetScanner
{
    public static ScanResult Scan(ProjectPathResolution resolution, List<(ModEntry Entry, string Dir)> enabledMods)
    {
        var result = new ScanResult();

        // 覆盖层：从强到弱
        var layers = new List<(string Label, string Dir)>();
        var suffix = ProjectPathKind.AppDir == resolution.Kind
            ? null
            : BodySlideLocator.GetSuffixUnder(resolution.EffectivePath, resolution.GameDataPath);

        if (suffix is not null && !string.IsNullOrWhiteSpace(resolution.GameDataPath))
        {
            foreach (var (_, dir) in enabledMods)
                layers.Add(($"{Path.GetFileName(dir.TrimEnd('\\', '/'))}", Path.Combine(dir, suffix)));
            layers.Add(("游戏Data（本体）", Path.Combine(resolution.GameDataPath, suffix)));
            result.LayerNotes.Add($"虚拟覆盖目录：{suffix}（{layers.Count} 层：{enabledMods.Count} 个启用模组 + 游戏Data 本体）");
        }
        else
        {
            layers.Add(("BodySlide 本体", Path.Combine(resolution.EffectivePath, "SliderSets")));
            result.LayerNotes.Add($"单一目录模式：{layers[0].Dir}");
        }

        // 相对路径 → 最强层的文件
        var winners = new List<(string RelPath, string Label, string FullPath)>();
        var winnerByRel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, dir) in layers)
        {
            if (!Directory.Exists(dir))
                continue;
            var layerFileCount = 0;
            List<string> files;
            try
            {
                // 一次遍历所有文件再按扩展名过滤；忽略无权限目录，避免个别目录异常中断整个扫描
                files = Directory.EnumerateFiles(
                        dir, "*",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                        })
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".osp", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"枚举 {dir} 失败：{ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                if (rel is null)
                    continue;
                layerFileCount++;
                if (winnerByRel.TryAdd(rel, label))
                    winners.Add((rel, label, file));
            }
            result.LayerNotes.Add($"  层 {label}: {layerFileCount} 个文件");
        }

        result.WinnerFileCount = winners.Count;

        // 解析获胜文件中的滑块组名（先见者胜）
        var byName = new Dictionary<string, OutfitEntry>(StringComparer.Ordinal);
        foreach (var (rel, label, fullPath) in winners)
        {
            foreach (var name in ParseSliderSetNames(fullPath, result.Warnings))
            {
                if (byName.TryGetValue(name, out var existing))
                {
                    existing.HasConflict = true;
                    continue;
                }
                byName[name] = new OutfitEntry
                {
                    Name = name,
                    OwnerLabel = label,
                    SourceFile = rel,
                };
            }
        }

        result.Outfits.AddRange(byName.Values);
        return result;
    }

    /// <summary>解析 &lt;SliderSet name="..."&gt;——服装名就是这个 name 属性，逐字符原样使用。</summary>
    public static IEnumerable<string> ParseSliderSetNames(string path, IList<string> warnings)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.None);
        }
        catch (Exception ex)
        {
            warnings.Add($"无法解析 {Path.GetFileName(path)}：{ex.Message}");
            yield break;
        }

        if (doc.Root is null)
            yield break;

        foreach (var element in doc.Root.DescendantsAndSelf("SliderSet"))
        {
            var name = (string?)element.Attribute("name");
            if (!string.IsNullOrEmpty(name))
                yield return name;
        }
    }
}
