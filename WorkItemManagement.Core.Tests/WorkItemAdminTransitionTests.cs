using DomainServices.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Services;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// #18: a platform admin can move a work item to any state to unblock a stuck project, while the
/// normal transition keeps refusing backward moves and moves out of Done or Cancelled.
/// </summary>
public class WorkItemAdminTransitionTests
{
    private static readonly Guid ProjectId = Guid.Parse("5a7e0c11-0000-4000-8000-000000000001");
    private static readonly Guid OtherProjectId = Guid.Parse("5a7e0c11-0000-4000-8000-000000000003");
    private static readonly Guid EnterpriseId = Guid.Parse("5a7e0c11-0000-4000-8000-000000000002");

    [Theory]
    [InlineData(WorkItemState.Review, WorkItemState.ToDo)]
    [InlineData(WorkItemState.Done, WorkItemState.Doing)]
    [InlineData(WorkItemState.Cancelled, WorkItemState.Backlog)]
    public async Task An_admin_can_make_a_move_the_normal_rules_refuse(WorkItemState from, WorkItemState to)
    {
        var (service, repository, stored) = Arrange(from);

        var result = await service.AdminTransitionByProjectAsync(ProjectId, stored.Id, to, "admin");

        Assert.NotNull(result.Item);
        Assert.Equal(to, result.Item!.State);
        Assert.Equal((stored.Id, to), Assert.Single(repository.Updates));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task An_admin_move_to_Done_with_evidence_succeeds()
    {
        var (service, repository, stored) = Arrange(WorkItemState.Doing, hasEvidence: true);

        var result = await service.AdminTransitionByProjectAsync(ProjectId, stored.Id, WorkItemState.Done, "admin");

        Assert.Equal(WorkItemState.Done, result.Item!.State);
        Assert.Equal((stored.Id, WorkItemState.Done), Assert.Single(repository.Updates));
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == WorkItemService.DeliveryEvidenceRequiredCode);
    }

    [Fact]
    public async Task An_admin_move_to_Done_without_evidence_is_refused_with_the_public_code()
    {
        var (service, repository, stored) = Arrange(WorkItemState.Doing, hasEvidence: false);

        var result = await service.AdminTransitionByProjectAsync(ProjectId, stored.Id, WorkItemState.Done, "admin");

        Assert.Equal(WorkItemState.Doing, result.Item!.State);
        Assert.Equal(WorkItemState.Doing, repository.Stored(stored.Id).State);
        Assert.Empty(repository.Updates);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkItemService.DeliveryEvidenceRequiredCode, warning.Code);
        Assert.Equal("work-item.transition.delivery-evidence-required", WorkItemService.DeliveryEvidenceRequiredCode);
    }

    [Fact]
    public async Task An_admin_move_to_the_current_state_is_a_no_op_even_for_Done()
    {
        var (service, repository, stored) = Arrange(WorkItemState.Done);

        var result = await service.AdminTransitionByProjectAsync(ProjectId, stored.Id, WorkItemState.Done, "admin");

        Assert.Equal(WorkItemState.Done, result.Item!.State);
        Assert.Empty(repository.Updates);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task An_admin_move_on_an_item_in_another_project_is_missing()
    {
        var (service, repository, stored) = Arrange(WorkItemState.Review);

        var result = await service.AdminTransitionByProjectAsync(OtherProjectId, stored.Id, WorkItemState.ToDo, "admin");

        Assert.Same(WorkItemMutationResult.Missing, result);
        Assert.Equal(WorkItemState.Review, repository.Stored(stored.Id).State);
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task An_item_in_another_project_is_missing_even_with_an_unknown_state()
    {
        var (service, repository, stored) = Arrange(WorkItemState.Review);

        var result = await service.AdminTransitionByProjectAsync(OtherProjectId, stored.Id, (WorkItemState)99, "admin");

        Assert.Same(WorkItemMutationResult.Missing, result);
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task An_admin_move_to_an_unknown_state_throws()
    {
        var (service, _, stored) = Arrange(WorkItemState.Review);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AdminTransitionByProjectAsync(ProjectId, stored.Id, (WorkItemState)99, "admin"));
    }

    [Theory]
    [InlineData(WorkItemState.Review, WorkItemState.ToDo, "work-item.transition.backward")]
    [InlineData(WorkItemState.Done, WorkItemState.Doing, "work-item.transition.terminal")]
    [InlineData(WorkItemState.Cancelled, WorkItemState.Backlog, "work-item.transition.terminal")]
    public async Task The_normal_transition_still_refuses_those_moves(WorkItemState from, WorkItemState to, string code)
    {
        var (service, repository, stored) = Arrange(from);

        var result = await service.TransitionByProjectAsync(ProjectId, stored.Id, to, "agent");

        Assert.Equal(from, result.Item!.State);
        Assert.Equal(from, repository.Stored(stored.Id).State);
        Assert.Empty(repository.Updates);
        Assert.Equal(code, Assert.Single(result.Warnings).Code);
    }

    private static (WorkItemService Service, RecordingWorkItemRepository Repository, WorkItem Stored) Arrange(
        WorkItemState state,
        bool hasEvidence = false)
    {
        var repository = new RecordingWorkItemRepository();
        var stored = repository.Seed(new UserStory
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = "Serve the agent download",
            AcceptanceCriteria = "A signed-in user can download the agent.",
            Number = 12,
            State = state,
        });
        var service = new WorkItemService(
            repository,
            NullAuditWriter.Instance,
            NullLogger<WorkItemService>.Instance,
            NullWorkItemStateSync.Instance,
            commitRefs: null,
            blockers: null,
            completionOverride: new FixedCompletionOverride(hasEvidence));
        return (service, repository, stored);
    }

    private sealed class FixedCompletionOverride(bool hasEvidence) : IWorkItemCompletionOverride
    {
        public Task<bool> HasForWorkItemAsync(
            Guid projectId,
            Guid workItemId,
            CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(hasEvidence);
    }
}
