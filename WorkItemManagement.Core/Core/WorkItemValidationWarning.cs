namespace WorkItemManagement.Core;

/// <summary>
/// Non-blocking validation warning emitted by <see cref="WorkItemManagement.Core.Services.IWorkItemService"/>
/// when a hierarchy convention is violated or a state transition skips intermediate
/// states (docs/22 Part 2, docs/23 WS9). Surfaced alongside the persisted result so the
/// UI can display the warning while still committing the operation.
/// </summary>
public sealed record WorkItemValidationWarning(string Code, string Message);
