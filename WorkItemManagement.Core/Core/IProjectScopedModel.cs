using DomainServices.Core.Models;

namespace WorkItemManagement.Core;

public interface IProjectScopedModel : ICoreDomainModel
{
    Guid ProjectId { get; set; }
}
