using DomainServices.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Services;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// #23: a lane holding a story or bug in progress is not offered another one. That keeps dispatch
/// off busy lanes, and makes a merged lane's next story newly startable for the merge handler.
/// </summary>
public class WorkItemStartableBusyLaneTests
{
    private static readonly Guid ProjectId = Guid.Parse("23b05e1a-0000-4000-8000-000000000001");
    private static readonly Guid EnterpriseId = Guid.Parse("23b05e1a-0000-4000-8000-000000000002");
    private static readonly Guid LaneA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000023");
    private static readonly Guid LaneB = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000023");

    [Theory]
    [InlineData(WorkItemState.Doing)]
    [InlineData(WorkItemState.Testing)]
    [InlineData(WorkItemState.Review)]
    public async Task A_lane_with_a_story_in_progress_is_offered_nothing(WorkItemState inProgress)
    {
        var repository = new RecordingWorkItemRepository();
        repository.Seed(Story("wbs:lead:E1-F1-S1", number: 1, LaneA, inProgress));
        repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));
        var freeLane = repository.Seed(Story("wbs:lead:E1-F1-S3", number: 3, LaneB));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId);

        Assert.Equal([freeLane.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task A_bug_in_progress_makes_its_lane_busy_too()
    {
        var repository = new RecordingWorkItemRepository();
        repository.Seed(new Bug
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = "Follow-up bug",
            Number = 1,
            State = WorkItemState.Doing,
            AssignedToId = LaneA,
            AssignedToKind = ParticipantKind.Agent,
        });
        repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId);

        Assert.Empty(startable);
    }

    [Fact]
    public async Task A_task_in_progress_does_not_make_its_lane_busy()
    {
        var repository = new RecordingWorkItemRepository();
        repository.Seed(new WorkItemManagement.Core.Task
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = "A step inside a story",
            Number = 1,
            State = WorkItemState.Doing,
            AssignedToId = LaneA,
            AssignedToKind = ParticipantKind.Agent,
        });
        var next = repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId);

        Assert.Equal([next.Id], startable.Select(item => item.Id));
    }

    [Theory]
    [InlineData(WorkItemState.Done)]
    [InlineData(WorkItemState.Cancelled)]
    public async Task A_finished_story_does_not_make_its_lane_busy(WorkItemState finished)
    {
        var repository = new RecordingWorkItemRepository();
        repository.Seed(Story("wbs:lead:E1-F1-S1", number: 1, LaneA, finished));
        var next = repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));

        var startable = await Service(repository).GetStartableByProjectAsync(ProjectId);

        Assert.Equal([next.Id], startable.Select(item => item.Id));
    }

    [Fact]
    public async Task When_the_lanes_story_is_done_its_next_story_becomes_startable()
    {
        // What the merge handler compares: the top-per-lane list before the merge and after it.
        var repository = new RecordingWorkItemRepository();
        var inReview = repository.Seed(Story("wbs:lead:E1-F1-S1", number: 1, LaneA, WorkItemState.Review));
        var next = repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));
        var service = Service(repository);

        var before = await service.GetStartableByProjectAsync(ProjectId);
        inReview.State = WorkItemState.Done;
        var after = await service.GetStartableByProjectAsync(ProjectId);

        Assert.DoesNotContain(next.Id, before.Select(item => item.Id));
        Assert.Contains(next.Id, after.Select(item => item.Id));
    }

    [Fact]
    public async Task Asking_for_a_busy_lane_returns_nothing_and_a_free_lane_its_stories()
    {
        var repository = new RecordingWorkItemRepository();
        repository.Seed(Story("wbs:lead:E1-F1-S1", number: 1, LaneA, WorkItemState.Doing));
        repository.Seed(Story("wbs:lead:E1-F1-S2", number: 2, LaneA));
        var b1 = repository.Seed(Story("wbs:lead:E1-F2-S1", number: 3, LaneB));
        var b2 = repository.Seed(Story("wbs:lead:E1-F2-S2", number: 4, LaneB));
        var service = Service(repository);

        var busy = await service.GetStartableByProjectAsync(ProjectId, assignedTo: LaneA, maxPerAssignee: 50);
        var free = await service.GetStartableByProjectAsync(ProjectId, assignedTo: LaneB, maxPerAssignee: 50);

        Assert.Empty(busy);
        Assert.Equal([b1.Id, b2.Id], free.Select(item => item.Id));
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
        Guid lane,
        WorkItemState state = WorkItemState.ToDo) =>
        new()
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = $"Story {wbsKey}",
            AcceptanceCriteria = "It works.",
            WbsKey = wbsKey,
            Number = number,
            State = state,
            AssignedToId = lane,
            AssignedToKind = ParticipantKind.Agent,
        };
}
