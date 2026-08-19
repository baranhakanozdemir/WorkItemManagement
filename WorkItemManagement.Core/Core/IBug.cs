namespace WorkItemManagement.Core;

public interface IBug : IWorkItem
{
    Guid? CausedByWorkItemId { get; set; }

    string? CausedByCommitRef { get; set; }
}
