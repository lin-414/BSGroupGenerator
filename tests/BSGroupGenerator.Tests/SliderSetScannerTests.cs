using Xunit;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

public class SliderSetScannerTests
{
    private static ProjectPathResolution VirtualResolution(string gameData, string suffix) => new()
    {
        EffectivePath = System.IO.Path.Combine(gameData, suffix),
        Kind = ProjectPathKind.GameDataCalienteTools,
        GameDataPath = gameData,
    };

    private static string SliderSetXml(params string[] names) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<SliderSetInfo version=\"2\">\n" +
        string.Join("", names.Select(n => $"    <SliderSet name=\"{n}\">\n        <Mesh name=\"x\"/>\n    </SliderSet>\n")) +
        "</SliderSetInfo>\n";

    [Fact]
    public void VirtualLayersOverlayByPriority()
    {
        using var temp = new TempDir();
        var gameData = temp.Sub("Data");
        var modA = temp.Sub("mods", "ArmorPackA"); // 优先级更高
        var modB = temp.Sub("mods", "ArmorPackB");

        temp.File("mods", "ArmorPackA", "CalienteTools", "BodySlide", "SliderSets", "A.xml", SliderSetXml("SharedOutfit", "OnlyA"));
        temp.File("mods", "ArmorPackB", "CalienteTools", "BodySlide", "SliderSets", "A.xml", SliderSetXml("SharedOutfit", "OnlyB"));
        temp.File("mods", "ArmorPackB", "CalienteTools", "BodySlide", "SliderSets", "B.xml", SliderSetXml("OnlyBFile"));
        temp.File("Data", "CalienteTools", "BodySlide", "SliderSets", "Vanilla.xml", SliderSetXml("DataOutfit"));

        var mods = new List<(ModEntry, string)>
        {
            (new ModEntry("ArmorPackA", true, false, false, 0), modA),
            (new ModEntry("ArmorPackB", true, false, false, 1), modB),
        };

        var result = SliderSetScanner.Scan(VirtualResolution(gameData, @"CalienteTools\BodySlide"), mods);

        var names = result.Outfits.Select(o => o.Name).ToList();
        // modA 的 A.xml 覆盖 modB 的同名文件：OnlyB 只能来自 B.xml
        Assert.Contains("SharedOutfit", names);
        Assert.Contains("OnlyA", names);
        Assert.Contains("OnlyBFile", names);
        Assert.Contains("DataOutfit", names);
        Assert.DoesNotContain("OnlyB", names);

        var shared = result.Outfits.First(o => o.Name == "SharedOutfit");
        Assert.Equal("ArmorPackA", shared.OwnerLabel);

        var data = result.Outfits.First(o => o.Name == "DataOutfit");
        Assert.Equal("游戏Data（本体）", data.OwnerLabel);
    }

    [Fact]
    public void DuplicateSetNamesAcrossDifferentFilesFlagConflict()
    {
        using var temp = new TempDir();
        var gameData = temp.Sub("Data");
        var modA = temp.Sub("mods", "A");
        var modB = temp.Sub("mods", "B");

        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "X.xml", SliderSetXml("Dup", "A1"));
        temp.File("mods", "B", "CalienteTools", "BodySlide", "SliderSets", "Y.xml", SliderSetXml("Dup", "B1"));

        var mods = new List<(ModEntry, string)>
        {
            (new ModEntry("A", true, false, false, 0), modA),
            (new ModEntry("B", true, false, false, 1), modB),
        };

        var result = SliderSetScanner.Scan(VirtualResolution(gameData, @"CalienteTools\BodySlide"), mods);

        Assert.Equal(3, result.Outfits.Count);
        var dup = result.Outfits.First(o => o.Name == "Dup");
        Assert.True(dup.HasConflict);
        Assert.Equal("A", dup.OwnerLabel); // 先见者胜
    }

    [Fact]
    public void DuplicateSetNamesWithinSameLayerResolveDeterministically()
    {
        using var temp = new TempDir();
        var gameData = temp.Sub("Data");
        var modA = temp.Sub("mods", "A");

        // 同一模组内两个文件定义同名滑块组：层内按相对路径字面序取先者，不依赖文件系统枚举顺序
        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "B.xml", SliderSetXml("Dup", "B1"));
        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "A.xml", SliderSetXml("Dup", "A1"));

        var mods = new List<(ModEntry, string)> { (new ModEntry("A", true, false, false, 0), modA) };
        var result = SliderSetScanner.Scan(VirtualResolution(gameData, @"CalienteTools\BodySlide"), mods);

        Assert.Equal(3, result.Outfits.Count);
        var dup = result.Outfits.First(o => o.Name == "Dup");
        Assert.True(dup.HasConflict);
        Assert.Equal("A.xml", Path.GetFileName(dup.SourceFile));
        Assert.Equal("A", dup.OwnerLabel);
    }

    [Fact]
    public void ParsesOspAndSkipsBrokenFiles()
    {
        using var temp = new TempDir();
        var gameData = temp.Sub("Data");
        var modA = temp.Sub("mods", "A");

        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "Proj.osp", SliderSetXml("OspOutfit"));
        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "Broken.xml",
            "<SliderSetInfo><SliderSet name='Unfinished");

        var mods = new List<(ModEntry, string)> { (new ModEntry("A", true, false, false, 0), modA) };
        var result = SliderSetScanner.Scan(VirtualResolution(gameData, @"CalienteTools\BodySlide"), mods);

        Assert.Contains(result.Outfits, o => o.Name == "OspOutfit");
        Assert.Contains(result.Warnings, w => w.Contains("Broken.xml"));
    }

    [Fact]
    public void AppDirKindScansSingleRealDirectory()
    {
        using var temp = new TempDir();
        var appDir = temp.Sub("BS");
        Directory.CreateDirectory(System.IO.Path.Combine(appDir, "SliderSets"));
        temp.File("BS", "SliderSets", "CBBE.xml", SliderSetXml("CBBE Body"));

        temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", "X.xml", SliderSetXml("ShouldNotAppear"));

        var mods = new List<(ModEntry, string)> { (new ModEntry("A", true, false, false, 0), temp.Sub("mods", "A")) };
        var resolution = new ProjectPathResolution
        {
            EffectivePath = appDir,
            Kind = ProjectPathKind.AppDir,
            GameDataPath = temp.Path,
        };

        var result = SliderSetScanner.Scan(resolution, mods);

        Assert.Single(result.Outfits);
        Assert.Equal("CBBE Body", result.Outfits[0].Name);
    }

    /// <summary>
    /// 解析阶段是并行的，但"同名滑块组先见者胜"依赖文件顺序。用跨文件重名 + 多种并行度断言：
    /// 结果必须与顺序实现完全一致，**包括 Outfits 的出现顺序**（它就是归属判定的落点）。
    /// 若把并行改成"边解析边写共享字典"，这里会随线程调度而变，从而暴露问题。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public void ParallelParsingKeepsFirstSeenWinsOrder(int parallelism)
    {
        using var temp = new TempDir();
        var gameData = temp.Sub("Data");
        var modA = temp.Sub("mods", "A");
        var modB = temp.Sub("mods", "B");

        // 12 个文件，名字空间故意重叠，制造大量跨文件重名
        for (var i = 0; i < 6; i++)
        {
            var shared = $"Shared{i % 3:D2}";
            temp.File("mods", "A", "CalienteTools", "BodySlide", "SliderSets", $"A{i:D2}.xml",
                SliderSetXml(shared, $"A{i:D2}"));
            temp.File("mods", "B", "CalienteTools", "BodySlide", "SliderSets", $"B{i:D2}.xml",
                SliderSetXml(shared, $"B{i:D2}"));
        }

        var mods = new List<(ModEntry, string)>
        {
            (new ModEntry("A", true, false, false, 0), modA),
            (new ModEntry("B", true, false, false, 1), modB),
        };
        var resolution = VirtualResolution(gameData, @"CalienteTools\BodySlide");

        var baseline = SliderSetScanner.Scan(resolution, mods, progress: null, parseParallelism: 1);
        var actual = SliderSetScanner.Scan(resolution, mods, progress: null, parseParallelism: parallelism);

        Assert.Equal(baseline.Outfits.Select(o => o.Name), actual.Outfits.Select(o => o.Name));
        Assert.Equal(baseline.Outfits.Select(o => o.OwnerLabel), actual.Outfits.Select(o => o.OwnerLabel));
        Assert.Equal(baseline.Outfits.Select(o => o.SourceFile), actual.Outfits.Select(o => o.SourceFile));
        Assert.Equal(baseline.Outfits.Select(o => o.HasConflict), actual.Outfits.Select(o => o.HasConflict));
        // 重名确实被触发，否则这个用例什么都没测到
        Assert.Contains(actual.Outfits, o => o.HasConflict);
    }

    /// <summary>
    /// 损坏文件**不贡献任何名字**（半途读到的也不能留下），且只留一条警告——
    /// 与改用 XmlReader 之前 XDocument.Load 一次性失败的语义一致。
    /// </summary>
    [Fact]
    public void BrokenFileContributesNothingAndWarnsOnce()
    {
        using var temp = new TempDir();
        var broken = temp.File("broken.xml",
            "<SliderSetInfo><SliderSet name=\"ReadBeforeFailure\"><SliderSet name='Unfinished");

        var warnings = new List<string>();
        var names = SliderSetScanner.ParseSliderSetNames(broken, warnings).ToList();

        Assert.Empty(names);
        Assert.Single(warnings);
        Assert.Contains("broken.xml", warnings[0]);
    }

    /// <summary>带命名空间的 &lt;SliderSet&gt; 不算命中，对齐原实现 DescendantsAndSelf("SliderSet") 的匹配范围。</summary>
    [Fact]
    public void NamespacedSliderSetIsIgnored()
    {
        using var temp = new TempDir();
        var file = temp.File("ns.xml",
            "<?xml version=\"1.0\"?>\n<Root xmlns=\"urn:example\">\n    <SliderSet name=\"Ignored\"/>\n</Root>\n");

        var warnings = new List<string>();
        Assert.Empty(SliderSetScanner.ParseSliderSetNames(file, warnings).ToList());
        Assert.Empty(warnings);
    }
}
