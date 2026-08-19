using DomainServices.Core.Services;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core.Services;

public interface IRequirementWorkItemLinkService : IDomainService<RequirementWorkItemLink>
{
    Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default);
}
