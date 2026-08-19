using DomainServices.Core.Services;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Services;

public interface IWorkItemBlockerService : IDomainService<WorkItemBlocker>
{
    Task<IReadOnlyCollection<WorkItemBlocker>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkItemBlocker?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItemBlocker?> UpdateByProjectAsync(Guid projectId, WorkItemBlocker model, CancellationToken cancellationToken = default);

    Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

}