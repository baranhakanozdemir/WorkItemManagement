using WorkItemManagement.Core.Services;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class RequirementWorkItemLinkService : AuditedDomainService<RequirementWorkItemLink>, IRequirementWorkItemLinkService
{
    private readonly IRequirementWorkItemLinkRepository _repository;

    public RequirementWorkItemLinkService(IRequirementWorkItemLinkRepository repository, IAuditWriter auditWriter)
        : base(repository, auditWriter)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default) =>
        _repository.GetLinksForRequirementAsync(requirementId, cancellationToken);

    public Task<IReadOnlyList<RequirementWorkItemLink>> GetLinksForWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default) =>
        _repository.GetLinksForWorkItemAsync(workItemId, cancellationToken);
}
