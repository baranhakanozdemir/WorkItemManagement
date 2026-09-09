namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Kind discriminator for nodes in a planning workflow's structured work-breakdown
/// emission (#1503 / Epic #1502). Mirrors <c>WorkItemType</c> so the materializer
/// in <c>CreateWorkBreakdownActivities</c> can map 1:1 without a translation table.
/// <see cref="Bug"/> is intentionally omitted — the planning workflow does not emit
/// defects; bugs are filed by humans against materialized work items.
/// </summary>
public enum WbsNodeKind
{
    /// <summary>Root node for one Deliverable; the materializer projects this to an Epic work item.</summary>
    Epic = 0,

    /// <summary>Children normally User Stories; materializes to a Feature work item.</summary>
    Feature = 1,

    /// <summary>Children normally Tasks; carries acceptance criteria derived from requirements.</summary>
    UserStory = 2,

    /// <summary>The smallest unit of executable work; materializes to a Task work item.</summary>
    Task = 3,
}
