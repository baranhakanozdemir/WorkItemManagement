using DomainServices.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Services;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// #21: among stories that are ready at the same time, the one with the highest customer priority
/// starts first, then the plan's natural order, then the WI number (docs/33 §2.2).
/// </summary>
public class WorkItemStartableOrderTests
{
    private static readonly Guid ProjectId = Guid.Parse("21a7e0c1-0000-4000-8000-000000000001");
    private static readonly Guid EnterpriseId = Guid.Parse("21a7e0c1-0000-4000-8000-000000000002");

    [Fact]
    public async Task Plan_order_is_natural_so_F2_starts_before_F10()
    {
        var repository = new RecordingWorkItemRepository();
        var f10 = repository.Seed(Story("wbs:lead:E1-F10", number: 1));
        var f2 = repository.Seed(Story("wbs:lead:E1-F2", number: 2));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([f2.Id, f10.Id], startable.Select(item => item.Id));
    }

    [Theory]
    [InlineData("wbs:lead:E2", "wbs:lead:E10")]
    [InlineData("wbs:lead:E1-F2-S9", "wbs:lead:E1-F2-S10")]
    [InlineData("wbs:lead:E1-F2", "wbs:lead:E1-F2-S1")]
    [InlineData("wbs:lead:E1-F02", "wbs:lead:E1-F3")]
    public async Task Natural_order_compares_number_runs_as_numbers(string first, string second)
    {
        var repository = new RecordingWorkItemRepository();
        var later = repository.Seed(Story(second, number: 1));
        var earlier = repository.Seed(Story(first, number: 2));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([earlier.Id, later.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task A_higher_priority_ready_story_starts_first_whatever_its_key()
    {
        var repository = new RecordingWorkItemRepository();
        var medium = repository.Seed(Story("wbs:lead:E1-F1", number: 1, WorkItemPriority.Medium));
        var low = repository.Seed(Story("wbs:lead:E1-F0", number: 2, WorkItemPriority.Low));
        var critical = repository.Seed(Story("wbs:lead:E9-F9", number: 3, WorkItemPriority.Critical));
        var high = repository.Seed(Story("wbs:lead:E5-F5", number: 4, WorkItemPriority.High));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([critical.Id, high.Id, medium.Id, low.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task Priority_never_overrides_a_dependency()
    {
        var repository = new RecordingWorkItemRepository();
        var dependency = repository.Seed(Story("wbs:lead:E1-F1", number: 1, state: WorkItemState.Doing));
        var critical = Story("wbs:lead:E1-F2", number: 2, WorkItemPriority.Critical);
        critical.Blockers.Add(new WorkItemBlocker
        {
            ProjectId = ProjectId,
            BlockedItemId = critical.Id,
            Kind = BlockerKind.WorkItem,
            BlockerItemId = dependency.Id,
        });
        repository.Seed(critical);
        var ready = repository.Seed(Story("wbs:lead:E1-F3", number: 3, WorkItemPriority.Low));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([ready.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task Stories_without_plan_order_start_in_WI_number_order()
    {
        var repository = new RecordingWorkItemRepository();
        var second = repository.Seed(Story($"wbs:story:{Guid.Parse("00000000-0000-4000-8000-000000000001")}", number: 8));
        var first = repository.Seed(Story($"wbs:story:{Guid.Parse("ffffffff-0000-4000-8000-000000000001")}", number: 7));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([first.Id, second.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task At_the_same_priority_planned_stories_come_before_items_without_plan_order()
    {
        var repository = new RecordingWorkItemRepository();
        var bug = repository.Seed(new Bug
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = "Follow-up bug",
            Number = 1,
            State = WorkItemState.ToDo,
            AssignedToId = Guid.NewGuid(),
            AssignedToKind = ParticipantKind.Agent,
        });
        var planned = repository.Seed(Story("wbs:lead:E3-F4", number: 9));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId, maxPerAssignee: 50);

        Assert.Equal([planned.Id, bug.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task Each_lane_still_gets_its_own_first_story()
    {
        var repository = new RecordingWorkItemRepository();
        var laneA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
        var laneB = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");
        var aLater = repository.Seed(Story("wbs:lead:E1-F10", number: 1, assignee: laneA));
        var aFirst = repository.Seed(Story("wbs:lead:E1-F2", number: 2, assignee: laneA));
        var bOnly = repository.Seed(Story("wbs:lead:E1-F3", number: 3, assignee: laneB));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId);

        Assert.Equal([aFirst.Id, bOnly.Id], startable.Select(item => item.Id));
        Assert.DoesNotContain(aLater.Id, startable.Select(item => item.Id));
    }

    private static WorkItemService Service(RecordingWorkItemRepository repository) =>
        new(
            repository,
            NullAuditWriter.Instance,
            NullLogger<WorkItemService>.Instance,
            NullWorkItemStateSync.Instance,
            commitRefs: null,
            blockers: null,
            completionOverride: NullWorkItemCompletionOverride.Instance);

    private static UserStory Story(
        string wbsKey,
        int number,
        WorkItemPriority priority = WorkItemPriority.Medium,
        WorkItemState state = WorkItemState.ToDo,
        Guid? assignee = null) =>
        new()
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = $"Story {wbsKey}",
            AcceptanceCriteria = "It works.",
            WbsKey = wbsKey,
            Number = number,
            Priority = priority,
            State = state,
            AssignedToId = assignee ?? Guid.NewGuid(),
            AssignedToKind = ParticipantKind.Agent,
        };
}
