namespace WorkItemManagement.Core;

/// <summary>
/// Kind of participant referenced by <see cref="IWorkItem.AssignedToId"/> (docs/22 Part 1, People Model).
/// </summary>
public enum ParticipantKind
{
    /// <summary>A human platform user (customer or platform-side).</summary>
    PlatformUser = 0,

    /// <summary>An AI agent assignee.</summary>
    Agent = 1,
}
