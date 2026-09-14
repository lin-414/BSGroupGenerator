using Xunit;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

public class GroupRulesTests
{
    [Theory]
    [InlineData("ube; 3ba", new[] { "ube", "3ba" })]
    [InlineData("ube，3ba；bodyslide", new[] { "ube", "3ba", "bodyslide" })]
    [InlineData(" ; ，", new string[] { })]
    public void SplitKeywordsHandlesSeparators(string input, string[] expected)
        => Assert.Equal(expected, GroupRules.SplitKeywords(input));

    [Fact]
    public void MatchesRequiresIncludeHit()
    {
        Assert.True(GroupRules.Matches("Dawn Priestess UBE", "IVY模组", ["ube"], [], matchOwner: false));
        Assert.False(GroupRules.Matches("Dawn Priestess UBE", "IVY模组", ["cbbe"], [], matchOwner: false));
        Assert.True(GroupRules.Matches("任意服装", "IVY模组", [], [], matchOwner: false)); // 留空=全部
    }

    [Fact]
    public void MatchesOwnerWhenEnabled()
    {
        Assert.True(GroupRules.Matches("某服装", "HIMBO Core", ["himbo"], [], matchOwner: true));
        Assert.False(GroupRules.Matches("某服装", "HIMBO Core", ["himbo"], [], matchOwner: false));
    }

    [Fact]
    public void ExcludeWinsOverInclude()
    {
        Assert.False(GroupRules.Matches("UBE 汉化版", "模组", ["ube"], ["汉化"], matchOwner: false));
        Assert.True(GroupRules.Matches("UBE 原版", "模组", ["ube"], ["汉化"], matchOwner: false));
    }
}
