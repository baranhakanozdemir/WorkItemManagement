using DomainServices.Core.Persistence;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Repositories;

public interface IWorkItemRelationRepository : IRepository<WorkItemRelation>
{
    Task<IReadOnlyCollection<WorkItemRelation>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkItemRelation?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItemRelation?> UpdateByProjectAsync(Guid projectId, WorkItemRelation model, CancellationToken cancellationToken = default);

    Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

}