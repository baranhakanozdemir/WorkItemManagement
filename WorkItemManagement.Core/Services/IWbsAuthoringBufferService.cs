using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Services;

/// <summary>
/// What a project's proposed breakdown looks like right now, without its body.
/// </summary>
/// <param name="ProjectId">The project the proposal belongs to.</param>
/// <param name="NodeCount">How many nodes the stored proposal holds.</param>
/// <param name="AuthoredBy">Who last wrote it, when the writer identified itself.</param>
/// <param name="AuthoredAt">When it was last written.</param>
public sealed record WbsAuthoringBufferSnapshot(
    Guid ProjectId,
    int NodeCount,
    string? AuthoredBy,
    DateTimeOffset AuthoredAt);

/// <summary>
/// Stores and returns a project's proposed work-breakdown structure while it is being authored.
/// </summary>
/// <remarks>
/// <para>Writing and judging are separate operations here, deliberately. <see cref="ReplaceAsync"/>
/// returns a write result — node count and timestamp — never a readiness verdict. Folding the gate
/// into the write would make a rejected evaluation look like a failed write when the proposal is in
/// fact stored, and an author who is told the write failed will write it again.</para>
/// <para>To judge a proposal, read it and run
/// <see cref="WbsReviewReadinessEvaluator.Evaluate(WbsStructurePayload, WbsStructureSource, WbsTraceabilityContext)"/>.
/// That call is a pure function: it needs no database and no service.</para>
/// </remarks>
public interface IWbsAuthoringBufferService
{
    /// <summary>
    /// The project's current proposal, or <c>null</c> when nothing has been authored yet.
    /// </summary>
    Task<WbsStructurePayload?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The project's current proposal described without reading its body.
    /// </summary>
    Task<WbsAuthoringBufferSnapshot?> GetSnapshotAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the project's proposal with <paramref name="payload"/>.
    /// </summary>
    /// <remarks>
    /// The payload is normalized on the way in, by the same rules
    /// <see cref="WbsStructurePayloadReader"/> applies when reading — so what is stored is what a
    /// later read returns, and a proposal cannot be made to mean one thing on write and another on
    /// read.
    /// </remarks>
    Task<WbsAuthoringBufferSnapshot> ReplaceAsync(
        Guid projectId,
        WbsStructurePayload payload,
        string authoredBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the project's proposal.
    /// </summary>
    /// <returns><c>false</c> when there was nothing to remove.</returns>
    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
