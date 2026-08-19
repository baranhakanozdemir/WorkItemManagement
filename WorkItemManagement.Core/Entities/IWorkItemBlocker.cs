using WorkItemManagement.Core;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public interface IWorkItemBlocker : IProjectScopedModel
{
    Guid BlockedItemId { get; set; }

    BlockerKind Kind { get; set; }

    Guid? BlockerItemId { get; set; }

    Guid? GateId { get; set; }

    string? ExternalNote { get; set; }
}
