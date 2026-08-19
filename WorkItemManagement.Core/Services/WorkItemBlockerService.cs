using DomainServices.Core.Services;
using WorkItemManagement.Core.Services;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class WorkItemBlockerService : AuditedDomainService<WorkItemBlocker>, IWorkItemBlockerService
{
    private readonly IWorkItemBlockerRepository _workItemBlockerRepository;

    public WorkItemBlockerService(IWorkItemBlockerRepository repository, IAuditWriter auditWriter)
        : base(repository, auditWriter)
    {
        _workItemBlockerRepository = repository;
    }

    public Task<IReadOnlyCollection<WorkItemBlocker>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _workItemBlockerRepository.GetAllByProjectAsync(projectId, cancellationToken);

    public Task<WorkItemBlocker?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemBlockerRepository.GetByProjectAsync(projectId, id, cancellationToken);

    public Task<WorkItemBlocker?> UpdateByProjectAsync(Guid projectId, WorkItemBlocker model, CancellationToken cancellationToken = default) =>
        _workItemBlockerRepository.UpdateByProjectAsync(projectId, model, cancellationToken);

    public Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemBlockerRepository.DeleteByProjectAsync(projectId, id, cancellationToken);
}