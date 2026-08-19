using DomainServices.Core.Persistence;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Repositories;

public interface IRequirementWorkItemLinkRepository : IRepository<RequirementWorkItemLink>
{
    /// <summary>Trace links anchored to a specific requirement.</summary>
    Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default);

    /// <summary>Trace links anchored to a specific work item.</summary>
    Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default);
}
