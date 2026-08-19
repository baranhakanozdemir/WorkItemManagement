using DomainServices.Core.Models;
using DomainServices.Core.Validation;

namespace WorkItemManagement.Core.Entities;

public class RequirementWorkItemLink : CoreDomainModel, IRequirementWorkItemLink
{
    public RequirementWorkItemLink()
    {
        Id = Guid.NewGuid();
        LinkedAt = DateTimeOffset.UtcNow;
    }

    public RequirementWorkItemLink(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
        LinkedBy = createdBy;
    }

    /// <inheritdoc />
    public Guid RequirementId { get; set; }

    /// <inheritdoc />
    public Guid WorkItemId { get; set; }

    /// <inheritdoc />
    public WorkItemKind WorkItemKind { get; set; }

    /// <inheritdoc />
    public string? Note { get; set; }

    /// <inheritdoc />
    public DateTimeOffset LinkedAt { get; set; }

    /// <inheritdoc />
    public string LinkedBy { get; set; } = string.Empty;

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(
            RequirementId != Guid.Empty,
            nameof(RequirementId),
            "Requirement id is required.");
        validator.Require(
            WorkItemId != Guid.Empty,
            nameof(WorkItemId),
            "Work item id is required.");
        validator.Require(
            Enum.IsDefined(WorkItemKind),
            nameof(WorkItemKind),
            "Work item kind is required.");
        validator.Require(
            !string.IsNullOrWhiteSpace(LinkedBy),
            nameof(LinkedBy),
            "LinkedBy is required.");
    }
}
