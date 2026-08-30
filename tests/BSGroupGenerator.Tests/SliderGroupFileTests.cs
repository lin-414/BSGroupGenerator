using Xunit;
using System.Text;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

public class SliderGroupFileTests
{
    [Fact]
    public void SaveProducesBomAndRoundtrips()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "groups.xml");

        var groups = new List<SliderGroup>
        {
            new("armor"),
            new("clothing"),
        };
        groups[0].Members.Add("CBBE");
        groups[0].Members.Add("Some Outfit v2");
        groups[1].Members.Add("Dress");

        SliderGroupFile.Save(path, groups);

        var bytes = System.IO.File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var text = System.IO.File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("<SliderGroups>", text);
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", text);

        Assert.True(SliderGroupFile.TryLoad(path, out var loaded, out var error));
        Assert.Empty(error);
        Assert.Equal(2, loaded.Count);
        Assert.Equal("armor", loaded[0].Name);
        Assert.Equal(new[] { "CBBE", "Some Outfit v2" }, loaded[0].Members);
    }

    [InlineData("CON", "_CON.xml")]
    [InlineData("NUL", "_NUL.xml")]
    [InlineData("CON.x", "_CON.x.xml")]
    [Theory]
    [InlineData("UBE", "UBE.xml")]
    [InlineData("服装/护甲", "服装_护甲.xml")]
    [InlineData("  UBE  ", "UBE.xml")]
    [InlineData("a:b<c>d", "a_b_c_d.xml")]
    public void FileNameForGroupSanitizes(string name, string expected)
        => Assert.Equal(expected, SliderGroupFile.FileNameForGroup(name));

    [Fact]
    public void LoadRejectsNonGroupXml()
    {
        using var temp = new TempDir();
        var path = temp.File("other.xml", "<?xml version=\"1.0\"?><SomethingElse/>");

        Assert.False(SliderGroupFile.TryLoad(path, out _, out var error));
        Assert.Contains("SliderGroups", error);
    }

    [Fact]
    public void MergeIsCaseInsensitiveOnGroupNamesAndExactOnMembers()
    {
        var target = new List<SliderGroup> { new("Armor") };
        target[0].Members.Add("CBBE");

        var incoming = new List<SliderGroup> { new("ARMOR"), new("New Group") };
        incoming[0].Members.Add("CBBE");       // 重复，忽略
        incoming[0].Members.Add("OtherOutfit"); // 新增
        incoming[1].Members.Add("Dress");

        SliderGroupFile.Merge(target, incoming, out var addedGroups, out var addedMembers);

        Assert.Equal(1, addedGroups);
        Assert.Equal(2, addedMembers);
        Assert.Equal(2, target.Count);
        Assert.Equal("Armor", target[0].Name); // 保留先出现的写法
        Assert.Equal(new[] { "CBBE", "OtherOutfit" }, target[0].Members);
    }
}
