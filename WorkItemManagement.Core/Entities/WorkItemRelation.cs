using DomainServices.Core.Models;
using DomainServices.Core.Validation;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public class WorkItemRelation : CoreDomainModel, IWorkItemRelation
{
    public Guid ProjectId { get; set; }

    public Guid SourceItemId { get; set; }

    public Guid TargetItemId { get; set; }

    public WorkItem? SourceItem { get; set; }

    public WorkItem? TargetItem { get; set; }

    public WorkItemRelation()
    {
        Id = Guid.NewGuid();
    }

    public WorkItemRelation(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
    }

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(ProjectId != Guid.Empty, nameof(ProjectId), "Project id is required.");
        validator.Require(SourceItemId != Guid.Empty, nameof(SourceItemId), "Source item id is required.");
        validator.Require(TargetItemId != Guid.Empty, nameof(TargetItemId), "Target item id is required.");
        validator.Require(
            SourceItemId != TargetItemId,
            nameof(TargetItemId),
            "A work item cannot relate to itself.");
    }
}
