using DomainServices.Core.Models;
using DomainServices.Core.Validation;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public class WorkItemBlocker : CoreDomainModel, IWorkItemBlocker
{
    public const int MaxExternalNoteLength = 2000;

    public Guid ProjectId { get; set; }

    public Guid BlockedItemId { get; set; }

    public BlockerKind Kind { get; set; }

    public Guid? BlockerItemId { get; set; }

    public Guid? GateId { get; set; }

    public string? ExternalNote { get; set; }

    public WorkItem? BlockedItem { get; set; }

    public WorkItemBlocker()
    {
        Id = Guid.NewGuid();
    }

    public WorkItemBlocker(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
    }

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(ProjectId != Guid.Empty, nameof(ProjectId), "Project id is required.");
        validator.Require(BlockedItemId != Guid.Empty, nameof(BlockedItemId), "Blocked item id is required.");
        validator.Require(Enum.IsDefined(Kind), nameof(Kind), "Blocker kind is required.");
        validator.Require(
            Kind != BlockerKind.WorkItem || BlockerItemId is not null,
            nameof(BlockerItemId),
            "Blocker item id is required when kind is WorkItem.");
        validator.Require(
            Kind != BlockerKind.DecisionGate || GateId is not null,
            nameof(GateId),
            "Gate id is required when kind is DecisionGate.");
        validator.Require(
            Kind != BlockerKind.External || !string.IsNullOrWhiteSpace(ExternalNote),
            nameof(ExternalNote),
            "External note is required when kind is External.");
        validator.Require(
            ExternalNote is null || ExternalNote.Length <= MaxExternalNoteLength,
            nameof(ExternalNote),
            $"External note must be {MaxExternalNoteLength} characters or fewer.");
    }
}
