using DomainServices.Core.Persistence;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Repositories;

public interface IWorkItemBlockerRepository : IRepository<WorkItemBlocker>
{
    Task<IReadOnlyCollection<WorkItemBlocker>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkItemBlocker?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItemBlocker?> UpdateByProjectAsync(Guid projectId, WorkItemBlocker model, CancellationToken cancellationToken = default);

    Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

}