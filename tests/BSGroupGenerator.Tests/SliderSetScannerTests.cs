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
}
