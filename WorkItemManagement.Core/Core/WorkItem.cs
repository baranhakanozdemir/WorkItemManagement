using System.Text.Json.Serialization;
using DomainServices.Core.Models;
using DomainServices.Core.Validation;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core;

[JsonConverter(typeof(WorkItemJsonConverter))]
public abstract class WorkItem : CoreDomainModel, IWorkItem
{
    public const int MaxTitleLength = 500;
    public const int MaxDescriptionLength = 8000;
    public const int MaxWbsKeyLength = 128;

    /// <inheritdoc />
    public Guid ProjectId { get; set; }

    /// <inheritdoc />
    public WorkItemType Type { get; set; }

    /// <inheritdoc />
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Description { get; set; }

    /// <inheritdoc />
    public WorkItemState State { get; set; } = WorkItemState.Backlog;

    /// <inheritdoc />
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;

    /// <inheritdoc />
    public Guid? ParentId { get; set; }

    /// <inheritdoc />
    public Guid? AssignedToId { get; set; }

    /// <inheritdoc />
    public ParticipantKind? AssignedToKind { get; set; }

    /// <inheritdoc />
    public string? WbsKey { get; set; }

    public WorkItem? Parent { get; set; }

    public ICollection<WorkItem> Children { get; set; } = new List<WorkItem>();

    public ICollection<RequirementWorkItemLink> RequirementLinks { get; set; } = new List<RequirementWorkItemLink>();

    public ICollection<WorkItemBlocker> Blockers { get; set; } = new List<WorkItemBlocker>();

    public ICollection<WorkItemCommitRef> CommitRefs { get; set; } = new List<WorkItemCommitRef>();

    public WorkItem()
    {
        Id = Guid.NewGuid();
    }

    public WorkItem(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
    }

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(ProjectId != Guid.Empty, nameof(ProjectId), "Project id is required.");
        validator.Require(Enum.IsDefined(Type), nameof(Type), "Work item type is required.");
        validator.Require(
            !string.IsNullOrWhiteSpace(Title),
            nameof(Title),
            "Title is required.");
        validator.Require(
            string.IsNullOrWhiteSpace(Title) || Title.Length <= MaxTitleLength,
            nameof(Title),
            $"Title must be {MaxTitleLength} characters or fewer.");
        validator.Require(
            Description is null || Description.Length <= MaxDescriptionLength,
            nameof(Description),
            $"Description must be {MaxDescriptionLength} characters or fewer.");
        validator.Require(Enum.IsDefined(State), nameof(State), "Work item state is required.");
        validator.Require(Enum.IsDefined(Priority), nameof(Priority), "Work item priority is required.");
        validator.Require(
            AssignedToKind is null || Enum.IsDefined(AssignedToKind.Value),
            nameof(AssignedToKind),
            "Assigned-to kind is invalid.");
        validator.Require(
            AssignedToId is null || AssignedToKind is not null,
            nameof(AssignedToKind),
            "Assigned-to kind is required when assignee is set.");
        validator.Require(
            AssignedToKind is null || AssignedToId is not null,
            nameof(AssignedToId),
            "Assigned-to id is required when assignee kind is set.");
        validator.Require(
            ParentId is null || ParentId != Id,
            nameof(ParentId),
            "A work item cannot be its own parent.");
        validator.Require(
            WbsKey is null || WbsKey.Length <= MaxWbsKeyLength,
            nameof(WbsKey),
            $"WbsKey must be {MaxWbsKeyLength} characters or fewer.");
    }
}
