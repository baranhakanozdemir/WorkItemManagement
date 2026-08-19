using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Services;

/// <summary>
/// Result of a best-effort work-management state write-back.
/// </summary>
public sealed record WorkItemStateSyncResult(bool Succeeded, string? Error = null)
{
    public static WorkItemStateSyncResult Ok { get; } = new(true);
}

/// <summary>
/// Package-local hook for propagating a work-item state change to an external
/// work-management system. Implementations are best-effort and must never throw
/// for a routine sync failure.
/// </summary>
public interface IWorkItemStateSync
{
    Task<WorkItemStateSyncResult> WriteBackStateAsync(
        WorkItem workItem,
        string actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op <see cref="IWorkItemStateSync"/> for use when no external
/// work-management integration is wired.
/// </summary>
public sealed class NullWorkItemStateSync : IWorkItemStateSync
{
    public static NullWorkItemStateSync Instance { get; } = new();

    private NullWorkItemStateSync()
    {
    }

    public Task<WorkItemStateSyncResult> WriteBackStateAsync(
        WorkItem workItem,
        string actor,
        CancellationToken cancellationToken = default)
        => System.Threading.Tasks.Task.FromResult(WorkItemStateSyncResult.Ok);
}
