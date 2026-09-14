using System.Xml;

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

public enum ScanPhase
{
    /// <summary>逐模组枚举覆盖层目录。</summary>
    Enumerating,
    /// <summary>解析获胜的滑块组文件。</summary>
    Parsing,
}

/// <summary>扫描进度：Current/Total 按阶段计；FilesFound 为至今发现的滑块组文件数。</summary>
public sealed record ScanProgress(ScanPhase Phase, int Current, int Total, int FilesFound);

/// <summary>
/// 按 BodySlide 的实际行为扫描服装（滑块组）：
/// 有效项目路径若是虚拟 Data 之下的目录（MO2 常态），则按 modlist 优先级模拟 USVFS 覆盖——
/// 相对路径相同的文件由更强的模组获胜；然后对获胜文件解析 &lt;SliderSet name="..."&gt;，
/// 同名滑块组先见者胜（与 BodySlideApp::LoadSliderSets 一致，成员名大小写敏感、不做任何变换）。
/// </summary>
public static class SliderSetScanner
{
    /// <summary>
    /// 解析阶段的默认并行度：按处理器数，但封顶 8。
    /// 超过 8 路并发读文件的收益已很有限（实测 1000 个文件 31 MB：8 路与 32 路相差不大），
    /// 而机械盘上并发过多会因寻道抖动反而变慢。
    /// </summary>
    private static readonly int DefaultParseParallelism =
        Math.Max(1, Math.Min(Environment.ProcessorCount, 8));

    /// <param name="parseParallelism">解析阶段的并行度；0 = 自动（见 <see cref="DefaultParseParallelism"/>）。</param>
    public static ScanResult Scan(ProjectPathResolution resolution, List<(ModEntry Entry, string Dir)> enabledMods,
        IProgress<ScanProgress>? progress = null, int parseParallelism = 0)
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
        var filesFound = 0;

        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var (label, dir) = layers[layerIndex];
            if (!Directory.Exists(dir))
            {
                progress?.Report(new ScanProgress(ScanPhase.Enumerating, layerIndex + 1, layers.Count, filesFound));
                continue;
            }
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

            // 层内文件顺序决定同名滑块组的归属（先见者胜），必须确定：显式按相对路径排序，
            // 对齐 BodySlide 的 wxDir::GetAllFiles 在 NTFS 上返回的字面序，避免依赖文件系统枚举顺序。
            files.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var rel = BodySlideLocator.GetSuffixUnder(file, dir);
                if (rel is null)
                    continue;
                layerFileCount++;
                if (winnerByRel.TryAdd(rel, label))
                    winners.Add((rel, label, file));
            }
            filesFound += layerFileCount;
            result.LayerNotes.Add($"  层 {label}: {layerFileCount} 个文件");
            progress?.Report(new ScanProgress(ScanPhase.Enumerating, layerIndex + 1, layers.Count, filesFound));
        }

        result.WinnerFileCount = winners.Count;

        // 解析获胜文件中的滑块组名。文件之间彼此独立，可并行；但"同名先见者胜"依赖 winners 的顺序，
        // 所以**按索引收集、再按原顺序合并**，绝不能边解析边写共享字典——否则结果随线程调度而变。
        // 实测（1000 个文件 / 31 MB，801 层）：端到端 166 ms → 47 ms；
        // 其中解析阶段并行度 1 为 134 ms、默认并行度 55 ms（2.45×）。
        var namesPerFile = new List<string>[winners.Count];
        var errorPerFile = new string?[winners.Count];
        var parsed = 0;
        var parallelism = parseParallelism > 0 ? parseParallelism : DefaultParseParallelism;
        progress?.Report(new ScanProgress(ScanPhase.Parsing, 0, winners.Count, winners.Count));

        Parallel.For(0, winners.Count,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            i =>
            {
                var names = new List<string>();
                errorPerFile[i] = ReadSliderSetNames(winners[i].FullPath, names);
                namesPerFile[i] = names;
                // 节流：Progress<T> 每次 Report 都会投递到 UI 线程，不能逐个上报
                var done = Interlocked.Increment(ref parsed);
                if (done % 32 == 0 || done == winners.Count)
                    progress?.Report(new ScanProgress(ScanPhase.Parsing, done, winners.Count, winners.Count));
            });

        var byName = new Dictionary<string, OutfitEntry>(StringComparer.Ordinal);
        for (var i = 0; i < winners.Count; i++)
        {
            var (rel, label, _) = winners[i];
            // 警告也按文件顺序合并，保证多次扫描的警告次序稳定
            if (errorPerFile[i] is { } error)
                result.Warnings.Add(error);
            foreach (var name in namesPerFile[i])
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
        var names = new List<string>();
        if (ReadSliderSetNames(path, names) is { } error)
        {
            warnings.Add(error);
            yield break;
        }
        foreach (var name in names)
            yield return name;
    }

    /// <summary>
    /// 流式读取 &lt;SliderSet name="..."&gt; 的 name 属性：成功返回 null，失败返回错误消息。
    /// 用 XmlReader 而非 XDocument——不建 DOM，实测快 1.3–1.8 倍，分配也少得多（并行时这点更关键）。
    /// 名字先收进 <paramref name="names"/>，整文件读通才算数：文件损坏则该文件**不贡献任何名字**、
    /// 只留一条警告，与原先 XDocument.Load 一次性失败的语义一致（不会因为读了一半就留下部分结果）。
    /// 另外要求命名空间为空，对齐原实现 DescendantsAndSelf("SliderSet") 的匹配范围。
    /// </summary>
    private static string? ReadSliderSetNames(string path, List<string> names)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Prohibit, // 既省事，也避免外部实体（XXE）
            CloseInput = true,
        };

        try
        {
            using var reader = XmlReader.Create(path, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element
                    || reader.LocalName != "SliderSet"
                    || reader.NamespaceURI.Length != 0)
                    continue;
                if (reader.GetAttribute("name") is { Length: > 0 } name)
                    names.Add(name);
            }
            return null;
        }
        catch (Exception ex)
        {
            names.Clear();
            return $"无法解析 {Path.GetFileName(path)}：{ex.Message}";
        }
    }
}
