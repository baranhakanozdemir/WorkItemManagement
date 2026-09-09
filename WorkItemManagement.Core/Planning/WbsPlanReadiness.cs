
namespace WorkItemManagement.Core.Planning;

/// <summary>
/// The gate verdict for a whole plan: structural quality (#1684, #2420) and traceability to what
/// the customer stated (#2288), over the same deliverable-resolved payload.
///
/// #2430 gave this rule a second caller. It used to live inside the generating workflow, which
/// meant the verdict existed only at generation time — the persisted <c>ReviewReady</c> flag was
/// the only record of it, and nothing could ask whether an edited plan now passes. One
/// implementation, so a plan repaired at the gate is judged by the rule that rejected it.
/// </summary>
public static class WbsPlanReadiness
{
    public static WbsPlanReadinessResult Evaluate(
        WbsStructurePayload payload,
        WbsStructureSource source,
        WbsTraceabilityContext traceabilityContext)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(traceabilityContext);

        var resolvedPayload = WbsDeliverableReferenceResolver
            .Resolve(payload, traceabilityContext.Deliverables)
            .Payload;
        var structural = WbsReviewReadinessEvaluator.Evaluate(
            resolvedPayload,
            source,
            traceabilityContext);
        var traceability = WbsTraceabilityEvaluator.Evaluate(resolvedPayload, traceabilityContext);

        // #2612: the third dimension. Structural asks whether the plan is well-formed and
        // traceability whether it covers the catalogue; neither asks whether the plan is about the
        // project the customer described, because both score against artifacts the platform
        // derived. Only the blocking half folds into the verdict — see WbsScopeFidelityEvaluator
        // for why the other findings are reported rather than enforced.
        var fidelity = WbsScopeFidelityEvaluator.Evaluate(resolvedPayload, traceabilityContext);

        return new WbsPlanReadinessResult(
            structural.IsReviewReady && traceability.IsReviewReady && fidelity.BlockingIssues.Count == 0,
            structural.Issues,
            traceability.Issues)
        {
            ScopeFidelityIssues = fidelity.BlockingIssues,
            ScopeFidelityObservations = fidelity.Observations,
            TraceabilityObservations = traceability.Observations,
        };
    }
}

/// <summary>
/// The verdict, with each half's issues kept apart: a structural finding is something the plan
/// says badly, a traceability finding is something the plan leaves out, and only the first is
/// fixable by editing a title.
/// </summary>
public sealed record WbsPlanReadinessResult(
    bool IsReviewReady,
    IReadOnlyList<string> StructuralIssues,
    IReadOnlyList<string> TraceabilityIssues)
{
    /// <summary>
    /// #2612: findings measured against the customer's stated scope that fold into
    /// <see cref="IsReviewReady"/>. Init properties rather than positional parameters so every
    /// existing construction site keeps compiling and reports no fidelity findings — which is
    /// what a caller that has not been taught to compute them actually knows.
    /// </summary>
    public IReadOnlyList<string> ScopeFidelityIssues { get; init; } = [];

    /// <summary>
    /// #2612: fidelity findings that are reported and never block — a plan may be offered for
    /// approval carrying these. They rest on reading titles rather than counting, and #2453 is the
    /// precedent for what a semantic obligation costs when it becomes a hard gate.
    /// </summary>
    public IReadOnlyList<string> ScopeFidelityObservations { get; init; } = [];

    /// <summary>
    /// #2633: missing citations where the work is already in the plan. Reported, never blocking.
    /// </summary>
    public IReadOnlyList<string> TraceabilityObservations { get; init; } = [];
}
