using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public interface IWorkItemRelation : IProjectScopedModel
{
    Guid SourceItemId { get; set; }

    Guid TargetItemId { get; set; }
}
