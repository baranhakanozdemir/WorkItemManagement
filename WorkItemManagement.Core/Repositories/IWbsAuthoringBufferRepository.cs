using DomainServices.Core.Persistence;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Repositories;

public interface IWbsAuthoringBufferRepository : IRepository<WbsAuthoringBuffer>
{
    /// <summary>
    /// The project's current proposal, or <c>null</c> when nothing has been authored yet.
    /// </summary>
    Task<WbsAuthoringBuffer?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the project's proposal.
    /// </summary>
    /// <returns><c>false</c> when there was nothing to remove.</returns>
    Task<bool> DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
