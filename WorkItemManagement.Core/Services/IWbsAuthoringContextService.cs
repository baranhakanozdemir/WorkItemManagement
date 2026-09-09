namespace WorkItemManagement.Core.Services;

/// <summary>
/// An obligation a breakdown must cover.
/// </summary>
/// <param name="Id">The requirement's identifier, which is what a node cites.</param>
/// <param name="RequirementNumber">Its human-readable number, e.g. <c>R-014</c>.</param>
/// <param name="Description">
/// The requirement's full text. Never truncated: a caller cites requirements by identifier and
/// needs the text to plan against, so a shortened description defeats the purpose of the call.
/// </param>
/// <param name="DeliverableId">The deliverable it belongs to, when it belongs to one.</param>
public sealed record WbsAuthoringRequirement(
    Guid Id,
    string RequirementNumber,
    string Description,
    Guid? DeliverableId);

/// <summary>
/// Something the project will deliver, which an epic may be scoped to.
/// </summary>
/// <param name="Id">The deliverable's identifier, which is what an epic cites.</param>
/// <param name="Name">Its name.</param>
/// <param name="Purpose">What it is for.</param>
/// <param name="DeliverableType">Its type.</param>
public sealed record WbsAuthoringDeliverable(
    Guid Id,
    string Name,
    string Purpose,
    string DeliverableType);

/// <summary>
/// Reads what a project's breakdown must cover.
/// </summary>
/// <remarks>
/// <para>A service rather than a pair of entities, deliberately. Requirements are stored as a base
/// type with subtypes across more than one table. A consumer that mapped a single flat table would
/// read some rows and silently miss others — and because the readiness evaluator then reports the
/// missing ones as uncovered, a partial read produces a coverage failure the breakdown does not
/// deserve, indistinguishable from a real one. Keeping the storage shape behind this interface
/// keeps that hazard here.</para>
/// <para>The results feed
/// <see cref="WorkItemManagement.Core.Planning.WbsTraceabilityContext"/>, which is what the
/// readiness evaluator judges coverage against.</para>
/// </remarks>
public interface IWbsAuthoringContextService
{
    /// <summary>
    /// Every requirement the project's breakdown must cover.
    /// </summary>
    Task<IReadOnlyList<WbsAuthoringRequirement>> GetRequirementsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every deliverable an epic may be scoped to.
    /// </summary>
    Task<IReadOnlyList<WbsAuthoringDeliverable>> GetDeliverablesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
