using DomainServices.Core.Services;
using WorkItemManagement.Core.Services;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class WorkItemRelationService : AuditedDomainService<WorkItemRelation>, IWorkItemRelationService
{
    private readonly IWorkItemRelationRepository _workItemRelationRepository;

    public WorkItemRelationService(IWorkItemRelationRepository repository, IAuditWriter auditWriter)
        : base(repository, auditWriter)
    {
        _workItemRelationRepository = repository;
    }

    public Task<IReadOnlyCollection<WorkItemRelation>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _workItemRelationRepository.GetAllByProjectAsync(projectId, cancellationToken);

    public Task<WorkItemRelation?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemRelationRepository.GetByProjectAsync(projectId, id, cancellationToken);

    public Task<WorkItemRelation?> UpdateByProjectAsync(Guid projectId, WorkItemRelation model, CancellationToken cancellationToken = default) =>
        _workItemRelationRepository.UpdateByProjectAsync(projectId, model, cancellationToken);

    public Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemRelationRepository.DeleteByProjectAsync(projectId, id, cancellationToken);
}