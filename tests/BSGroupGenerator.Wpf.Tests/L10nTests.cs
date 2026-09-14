using BSGroupGenerator.Wpf.Services;
using BSGroupGenerator.Wpf.ViewModels;
using Xunit;

namespace BSGroupGenerator.Wpf.Tests;

/// <summary>无 Application 上下文（单测、设计器）时取词不抛异常、回落到键名。
/// 服装节点文本在构造函数里取词，因此该路径必须有此保证。</summary>
public class L10nTests
{
    [Fact]
    public void TrWithoutApplicationFallsBackToKey()
    {
        Assert.Equal("L.Tree_ConflictSuffix", L10n.Tr("L.Tree_ConflictSuffix"));
    }

    [Fact]
    public void ConflictOutfitNode_UsesLocalizedSuffix()
    {
        var node = new OutfitNodeVM("A", hasConflict: true, isMember: false, isChecked: false);

        Assert.Equal("A" + L10n.Tr("L.Tree_ConflictSuffix"), node.Text);
    }
}
