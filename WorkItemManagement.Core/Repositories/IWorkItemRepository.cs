using DomainServices.Core.Persistence;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Repositories;

public interface IWorkItemRepository : IRepository<WorkItem>
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

    Task<WorkItem?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItem?> UpdateByProjectAsync(Guid projectId, WorkItem model, CancellationToken cancellationToken = default);

    Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetSubtreeAsync(Guid rootId, Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetByStateAsync(WorkItemState state, Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetBlockedAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetByRequirementAsync(Guid requirementId, Guid projectId, CancellationToken cancellationToken = default);
}
