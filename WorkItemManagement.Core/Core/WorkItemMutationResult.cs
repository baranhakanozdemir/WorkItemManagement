namespace WorkItemManagement.Core;

/// <summary>
/// Envelope returned by <see cref="WorkItemManagement.Core.Services.IWorkItemService"/> mutations
/// that may emit non-blocking <see cref="WorkItemValidationWarning"/> records. <see cref="Item"/>
/// is null when the underlying entity was not found (e.g., a transition targeted an unknown id);
/// non-null on every successful create, update, or transition. Warnings is empty (never null)
/// when no hierarchy or skip-state violation was detected.
/// </summary>
public sealed record WorkItemMutationResult
{
    public WorkItem? Item { get; }

    public IReadOnlyList<WorkItemValidationWarning> Warnings { get; }

    public WorkItemMutationResult(WorkItem? item, IReadOnlyList<WorkItemValidationWarning>? warnings)
    {
        Item = item;
        Warnings = warnings ?? Array.Empty<WorkItemValidationWarning>();
    }

    public static WorkItemMutationResult Missing { get; } = new(null, null);

    public static WorkItemMutationResult Success(WorkItem item) => new(item, null);

    public static WorkItemMutationResult Success(WorkItem item, IReadOnlyList<WorkItemValidationWarning>? warnings) =>
        new(item, warnings);
}
