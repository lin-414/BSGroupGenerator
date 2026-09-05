using Xunit;
using BSGroupGenerator.Core;

namespace BSGroupGenerator.Tests;

public class GroupStoreTests
{
    [Fact]
    public void NewGroupValidatesAndSelects()
    {
        var store = new GroupStore();
        Assert.False(store.NewGroup("").Ok);
        store.NewGroup("3BA");
        Assert.False(store.NewGroup("3ba").Ok); // 忽略大小写的重名
        Assert.Equal("3BA", store.CurrentGroupName);
        Assert.True(store.Dirty);
    }

    [Fact]
    public void ApplyToCurrentAddsAndRemovesWithoutDuplication()
    {
        var store = new GroupStore();
        store.NewGroup("UBE");
        store.ApplyToCurrent(new[] { "OutfitA", "OutfitB" }, add: true);
        store.ApplyToCurrent(new[] { "OutfitA" }, add: true); // 重复加入

        var group = store.Current!;
        Assert.Equal(new[] { "OutfitA", "OutfitB" }, group.Members);

        store.ApplyToCurrent(new[] { "OutfitA" }, add: false);
        Assert.Equal(new[] { "OutfitB" }, group.Members);
    }

    [Fact]
    public void UndoRestoresPreviousState()
    {
        var store = new GroupStore();
        store.NewGroup("UBE");
        store.ApplyToGroup("UBE", new[] { "A", "B" }, add: true);
        store.ApplyToGroup("UBE", new[] { "C" }, add: true);

        Assert.True(store.Undo().Ok);
        Assert.Equal(new[] { "A", "B" }, store.GetGroup("UBE")!.Members);

        Assert.True(store.Undo().Ok);
        Assert.Empty(store.GetGroup("UBE")!.Members);
    }

    [Fact]
    public void UndoSurvivesDeleteAndRestoreGroup()
    {
        var store = new GroupStore();
        store.NewGroup("UBE");
        store.ApplyToGroup("UBE", new[] { "A" }, add: true);
        store.DeleteGroup("UBE");

        Assert.True(store.Undo().Ok);
        Assert.NotNull(store.GetGroup("UBE"));
        Assert.Contains("UBE", store.Groups.Select(g => g.Name));
    }

    [Fact]
    public void RenameUpdatesCurrentSelection()
    {
        var store = new GroupStore();
        store.NewGroup("Old");
        store.RenameGroup("Old", "New");
        Assert.Equal("New", store.CurrentGroupName);
    }

    [Fact]
    public void ImportMergesAndCounts()
    {
        var store = new GroupStore();
        store.NewGroup("UBE");
        store.ApplyToGroup("UBE", new[] { "A" }, add: true);

        var incoming = new List<SliderGroup>
        {
            new("UBE", new[] { "A", "B" }),
            new("3BA", new[] { "X" }),
        };
        var (addedGroups, addedMembers) = store.Import(incoming);

        Assert.Equal(1, addedGroups);
        Assert.Equal(2, addedMembers);
        Assert.Equal(new[] { "A", "B" }, store.GetGroup("UBE")!.Members);
    }

    [Fact]
    public void LoadReplacesAndClearsDirty()
    {
        var store = new GroupStore();
        store.NewGroup("Temp");
        Assert.True(store.Dirty);

        store.Load(new List<SliderGroup> { new("Loaded", new[] { "X" }) });
        Assert.False(store.Dirty);
        Assert.Equal("Loaded", store.CurrentGroupName);
    }

    [Fact]
    public void RuleApplyAddsByNames()
    {
        var store = new GroupStore();
        store.NewGroup("UBE");

        var applied = store.ApplyToGroup("UBE", new[] { "A", "A", "B" }, add: true);
        Assert.Equal(2, applied);
        Assert.Equal(new[] { "A", "B" }, store.GetGroup("UBE")!.Members);
    }
}
