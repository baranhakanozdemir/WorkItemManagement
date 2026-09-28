using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Tests;

public class WorkItemTypeSystemTests
{
    [Theory]
    [InlineData(WorkItemType.Epic)]
    [InlineData(WorkItemType.Feature)]
    [InlineData(WorkItemType.UserStory)]
    [InlineData(WorkItemType.Task)]
    [InlineData(WorkItemType.Bug)]
    public void WorkItemType_HasExpectedValues(WorkItemType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Fact]
    public void WorkItemType_HasExactlyFiveValues()
    {
        Assert.Equal(5, Enum.GetValues<WorkItemType>().Length);
    }

    [Theory]
    [InlineData(WorkItemState.Backlog)]
    [InlineData(WorkItemState.ToDo)]
    [InlineData(WorkItemState.Doing)]
    [InlineData(WorkItemState.Testing)]
    [InlineData(WorkItemState.Review)]
    [InlineData(WorkItemState.Done)]
    [InlineData(WorkItemState.Cancelled)]
    public void WorkItemState_HasExpectedValues(WorkItemState state)
    {
        Assert.True(Enum.IsDefined(state));
    }

    [Fact]
    public void WorkItemState_IncludesCancelledAsDistinctTerminalState()
    {
        Assert.Equal(7, Enum.GetValues<WorkItemState>().Length);
        Assert.Contains(WorkItemState.Cancelled, Enum.GetValues<WorkItemState>());
    }

    [Theory]
    [InlineData(WorkItemPriority.Critical)]
    [InlineData(WorkItemPriority.High)]
    [InlineData(WorkItemPriority.Medium)]
    [InlineData(WorkItemPriority.Low)]
    public void WorkItemPriority_HasExpectedValues(WorkItemPriority priority)
    {
        Assert.True(Enum.IsDefined(priority));
    }

    [Fact]
    public void WorkItemPriority_MediumIsClrDefault()
    {
        Assert.Equal(WorkItemPriority.Medium, default);
        Assert.Equal(4, Enum.GetValues<WorkItemPriority>().Length);
    }

    [Theory]
    [InlineData(BlockerKind.WorkItem)]
    [InlineData(BlockerKind.DecisionGate)]
    [InlineData(BlockerKind.External)]
    public void BlockerKind_HasExpectedValues(BlockerKind kind)
    {
        Assert.True(Enum.IsDefined(kind));
    }

    [Fact]
    public void BlockerKind_HasExactlyThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<BlockerKind>().Length);
    }
}
