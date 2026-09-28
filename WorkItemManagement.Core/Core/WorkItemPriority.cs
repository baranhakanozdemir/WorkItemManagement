namespace WorkItemManagement.Core;

/// <summary>
/// Priority levels for work items (docs/22 Part 2). Medium is the default when unset.
/// </summary>
public enum WorkItemPriority
{
    /// <summary>Normal priority — the default.</summary>
    Medium = 0,

    /// <summary>Nice-to-have; addressed when capacity allows.</summary>
    Low = 1,

    /// <summary>Important and next in line, but does not preempt active work.</summary>
    High = 2,

    /// <summary>Blocks the project or breaks something live; must be addressed now.</summary>
    Critical = 3,
}
