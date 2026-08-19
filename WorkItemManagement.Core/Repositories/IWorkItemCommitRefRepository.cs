using DomainServices.Core.Persistence;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Repositories;

public interface IWorkItemCommitRefRepository : IRepository<WorkItemCommitRef>
{
    Task<IReadOnlyCollection<WorkItemCommitRef>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkItemCommitRef?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItemCommitRef?> UpdateByProjectAsync(Guid projectId, WorkItemCommitRef model, CancellationToken cancellationToken = default);

    Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);

    Task<WorkItemCommitRef?> FindByPullRequestAsync(
        string repositoryFullName,
        int pullRequestNumber,
        CancellationToken cancellationToken = default);
}