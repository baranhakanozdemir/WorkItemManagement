namespace WorkItemManagement.Core;

/// <summary>
/// Typed blocker classification for work-item dependencies (docs/22 Part 1 — Relationships).
/// </summary>
public enum BlockerKind
{
    /// <summary>Another work item is blocking progress.</summary>
    WorkItem = 0,

    /// <summary>A pending decision or sign-off gate is blocking progress.</summary>
    DecisionGate = 1,

    /// <summary>An external dependency (customer credentials, third party, etc.).</summary>
    External = 2,
}
