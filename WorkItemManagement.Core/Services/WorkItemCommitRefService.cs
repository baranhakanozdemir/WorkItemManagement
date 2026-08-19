using WorkItemManagement.Core.Services;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class WorkItemCommitRefService : AuditedDomainService<WorkItemCommitRef>, IWorkItemCommitRefService
{
    private readonly IWorkItemCommitRefRepository _workItemCommitRefRepository;

    public WorkItemCommitRefService(IWorkItemCommitRefRepository repository, IAuditWriter auditWriter)
        : base(repository, auditWriter)
    {
        _workItemCommitRefRepository = repository;
    }

    public Task<IReadOnlyCollection<WorkItemCommitRef>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _workItemCommitRefRepository.GetAllByProjectAsync(projectId, cancellationToken);

    public Task<WorkItemCommitRef?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemCommitRefRepository.GetByProjectAsync(projectId, id, cancellationToken);

    public Task<WorkItemCommitRef?> UpdateByProjectAsync(Guid projectId, WorkItemCommitRef model, CancellationToken cancellationToken = default) =>
        _workItemCommitRefRepository.UpdateByProjectAsync(projectId, model, cancellationToken);

    public Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemCommitRefRepository.DeleteByProjectAsync(projectId, id, cancellationToken);

    public Task<WorkItemCommitRef?> FindByPullRequestAsync(
        string repositoryFullName,
        int pullRequestNumber,
        CancellationToken cancellationToken = default) =>
        _workItemCommitRefRepository.FindByPullRequestAsync(
            repositoryFullName,
            pullRequestNumber,
            cancellationToken);
}