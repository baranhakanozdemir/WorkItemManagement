using DomainServices.Core.Models;
using WorkItemManagement.Core.Entities;

namespace WorkItemManagement.Core;

/// <summary>
/// Base domain contract for the canonical work-item model (docs/22 Part 1).
/// </summary>
public interface IWorkItem : ICoreDomainModel
{
    /// <summary>Project-scope boundary; all queries filter by this.</summary>
    Guid ProjectId { get; set; }

    WorkItemType Type { get; set; }

    string Title { get; set; }

    string? Description { get; set; }

    WorkItemState State { get; set; }

    WorkItemPriority Priority { get; set; }

    Guid? ParentId { get; set; }

    Guid? AssignedToId { get; set; }

    ParticipantKind? AssignedToKind { get; set; }

    /// <summary>
    /// Optional out-of-band external key used by source adapters and planning automation
    /// (e.g. the WBS keys emitted by the planning workflow, future ADO/Jira/GitHub adapters)
    /// to make their writes idempotent. Null on items created by the customer through the
    /// UI; non-null on items created by a generator that needs to dedupe across re-runs.
    /// </summary>
    string? WbsKey { get; set; }

    /// <summary>
    /// The item's number within its project — 1, 2, 3… in creation order, never reused — shown
    /// as <c>WI-&lt;n&gt;</c> (<see cref="WorkItemNumber"/>). Assigned by the consumer's persistence
    /// on insert and owned by the server: an update never changes it. <c>0</c> means not assigned yet.
    /// </summary>
    int Number { get; set; }
}
