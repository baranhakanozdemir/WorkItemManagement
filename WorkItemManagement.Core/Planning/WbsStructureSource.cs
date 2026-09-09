namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Provenance for a persisted <see cref="Models.WbsStructures.WbsStructure"/> emission (#1587).
/// Consumed by customer planning state to gate approval.
/// </summary>
public enum WbsStructureSource
{
    Summarizer = 0,
    DeterministicFallback = 1,
    SummarizerNonConverged = 2,
    SummarizerDependencyCycleSalvaged = 3,

    /// <summary>
    /// #3236: the tree named prerequisites that do not exist, so those links were removed and the
    /// rest of the plan kept. Distinct from <see cref="SummarizerDependencyCycleSalvaged"/> because
    /// the repair is different in kind — a cycle is an ordering the plan cannot satisfy, an orphan
    /// is a reference to work that was never planned.
    /// </summary>
    SummarizerOrphanDependencySalvaged = 4,

    /// <summary>
    /// #3281: the round-table lead authored the tree itself, during the discussion, through the
    /// <c>wbs</c> CLI. The plan that reaches persistence is the one the deliberation decided on,
    /// rather than a summarizer's reconstruction of it from the closing message.
    ///
    /// <para>This is the first value other than <see cref="Summarizer"/> that names a real
    /// deliberation, and therefore the first that may be review-ready. Every other value is a
    /// degradation and <c>WbsReviewReadinessEvaluator</c> adds an issue for it; this one adds none,
    /// which is why <c>WbsPlanningQualityGate.ShouldHalt</c> had to stop treating "not Summarizer"
    /// as a synonym for "degraded" in the same commit that added this member.</para>
    /// </summary>
    LeadAuthored = 5,
}
