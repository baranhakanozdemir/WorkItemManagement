using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public interface IWorkItemCommitRef : IProjectScopedModel
{
    Guid WorkItemId { get; set; }

    string CommitSha { get; set; }

    string Repository { get; set; }

    DateTimeOffset CommittedAt { get; set; }
}
