namespace WorkItemManagement.Core.Services;

/// <summary>
/// Package-local hook that lets a host recognise completion evidence recorded
/// out of band — e.g. a human-approved "manual delivery exception" that marks a
/// work item delivered without an automated commit/PR trail. When this returns
/// <c>true</c> for a work item, the completion-evidence gate is satisfied even
/// when no qualifying commit reference exists.
/// </summary>
public interface IWorkItemCompletionOverride
{
    Task<bool> HasForWorkItemAsync(
        Guid projectId,
        Guid workItemId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op <see cref="IWorkItemCompletionOverride"/> for use when no manual
/// completion override is wired. Never reports an override.
/// </summary>
public sealed class NullWorkItemCompletionOverride : IWorkItemCompletionOverride
{
    public static NullWorkItemCompletionOverride Instance { get; } = new();

    private NullWorkItemCompletionOverride()
    {
    }

    public Task<bool> HasForWorkItemAsync(
        Guid projectId,
        Guid workItemId,
        CancellationToken cancellationToken = default)
        => System.Threading.Tasks.Task.FromResult(false);
}
