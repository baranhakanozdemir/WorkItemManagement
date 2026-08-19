using DomainServices.Core.Responses;
using DomainServices.Core.Services;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Services;

public interface IWorkItemService : IDomainService<WorkItem>
{
    Task<IReadOnlyCollection<WorkItem>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> ListByProjectAsync(
        Guid projectId,
        WorkItemState? state = null,
        WorkItemType? type = null,
        Guid? assignedTo = null,
        bool? blocked = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetStartableByProjectAsync(
        Guid projectId,
        Guid? assignedTo = null,
        int maxPerAssignee = 1,
        CancellationToken cancellationToken = default);

    Task<WorkItem?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetSubtreeByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<IBaseResponse<WorkItem>> UpdateByProjectAsync(
        Guid projectId,
        Guid id,
        WorkItem model,
        string actor,
        CancellationToken cancellationToken = default);

    Task<IBaseResponse> DeleteByProjectAsync(
        Guid projectId,
        Guid id,
        string actor,
        CancellationToken cancellationToken = default);

    Task<WorkItem?> AddByProjectAsync(Guid projectId, WorkItem model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create the work item, returning hierarchy warnings (docs/22 Part 2) alongside
    /// the persisted entity. Priority defaults to <see cref="WorkItemPriority.Medium"/>
    /// when unset; hierarchy violations are reported as warnings, not exceptions.
    /// </summary>
    Task<WorkItemMutationResult> CreateWithWarningsAsync(
        Guid enterpriseId,
        WorkItem model,
        string userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Single transition method (docs/22 Part 2, docs/23 WS9). Backlog→ToDo→Doing→Testing→Review→Done.
    /// Skipping intermediate states is allowed but logged at Warning level.
    /// Any active state may transition to Cancelled. Done and Cancelled are terminal and
    /// block any further transition.
    /// </summary>
    Task<WorkItemMutationResult> TransitionAsync(
        Guid workItemId,
        WorkItemState to,
        string userName,
        CancellationToken cancellationToken = default);

    Task<WorkItemMutationResult> TransitionByProjectAsync(
        Guid projectId,
        Guid workItemId,
        WorkItemState to,
        string userName,
        CancellationToken cancellationToken = default);
}
