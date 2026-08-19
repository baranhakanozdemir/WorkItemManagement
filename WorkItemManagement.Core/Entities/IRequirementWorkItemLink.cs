using DomainServices.Core.Models;

namespace WorkItemManagement.Core.Entities;

/// <summary>
/// Many-to-many trace link between a requirement and a work item (epic / feature / user story).
/// Requirements and work items live in separate hierarchies so each can evolve independently;
/// this link record makes coverage a query rather than an embedded list. Only
/// <c>IFunctionalRequirement</c> and <c>INonFunctionalRequirement</c> participate — drivers,
/// principles, and constraints govern rather than translate into work.
/// </summary>
public interface IRequirementWorkItemLink : ICoreDomainModel
{
    Guid RequirementId { get; set; }
    Guid WorkItemId { get; set; }
    WorkItemKind WorkItemKind { get; set; }
    string? Note { get; set; }
    DateTimeOffset LinkedAt { get; set; }
    string LinkedBy { get; set; }
}
