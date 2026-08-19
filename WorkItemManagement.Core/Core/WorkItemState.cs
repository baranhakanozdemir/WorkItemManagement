namespace WorkItemManagement.Core;

/// <summary>
/// Single-valued lifecycle state on every work item (docs/22 Part 2).
/// </summary>
public enum WorkItemState
{
    /// <summary>Captured, not yet scheduled for active work.</summary>
    Backlog = 0,

    /// <summary>Scheduled, ready to be picked up.</summary>
    ToDo = 1,

    /// <summary>Actively being worked.</summary>
    Doing = 2,

    /// <summary>Built, undergoing quality verification (QA, automated tests).</summary>
    Testing = 3,

    /// <summary>Tested, awaiting sign-off or approval per the People Model.</summary>
    Review = 4,

    /// <summary>Complete and approved. Terminal.</summary>
    Done = 5,

    /// <summary>
    /// Work that was dropped (deprioritized, made obsolete, superseded).
    /// A terminal state distinct from Done — not a lifecycle position.
    /// </summary>
    Cancelled = 6,
}
