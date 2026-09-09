
namespace WorkItemManagement.Core.Planning;

/// <summary>
/// #2444: the two countable epic rules from #2429 — at most one cross-cutting Epic per project, and
/// exactly one Epic per deliverable — stated once so the review gate and the structuring repair loop
/// cannot describe the same violation differently.
///
/// <para>They lived as private collectors inside <see cref="WbsTraceabilityEvaluator"/> until a live
/// run emitted five cross-cutting epics against a deploy where the prompt already stated the cap in
/// three sentences. Prompt-only enforcement was not untried; it was tried and the model broke it, so
/// the rules now also feed the repair loop, which needs the same wording the gate uses.</para>
///
/// <para><b>These are findings, not verdicts.</b> Nothing here decides whether a payload is
/// rejected — <see cref="WbsTraceabilityEvaluator"/> folds them into the persisted
/// <c>reviewReady</c> flag, and the structuring activity offers them to the model for a single
/// repair round. The #2371 rule still holds: a shape violation must never be the reason a planning
/// run ends without a plan.</para>
/// </summary>
public static class WbsEpicShapeRules
{
    /// <summary>
    /// Every epic-shape violation in <paramref name="payload"/>, in gate wording. Empty when the
    /// payload conforms, which is the normal case and the one the caller should expect.
    /// </summary>
    public static IReadOnlyList<string> Collect(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var issues = new List<string>();
        CollectCrossCuttingEpicCapIssues(payload, issues);
        CollectDuplicateDeliverableEpicIssues(payload, issues);
        return issues;
    }

    /// <summary>
    /// #2429/#2432. Zero cross-cutting epics is fine and one is fine; only the second is a finding,
    /// so a project with no cross-cutting work is never nagged into inventing some.
    ///
    /// <para>Deliberately not folded into <c>CollectDeliverableCoverageIssues</c>, which is guarded
    /// on a non-empty deliverable catalogue: the cap is a property of the epics the model emitted,
    /// not of the catalogue, and a project with no deliverables recorded yet is exactly where a run
    /// can produce nothing but governance epics.</para>
    /// </summary>
    private static void CollectCrossCuttingEpicCapIssues(
        WbsStructurePayload payload,
        IList<string> issues)
    {
        var crossCutting = payload.Nodes
            .Where(node => node.Kind == WbsNodeKind.Epic && node.DeliverableId is null)
            .ToList();

        if (crossCutting.Count <= 1)
        {
            return;
        }

        issues.Add(
            $"Cross-cutting epic cap exceeded: {crossCutting.Count} Epics carry no deliverable "
            + $"({string.Join(", ", crossCutting.Select(epic => $"'{epic.EmittedKey}'"))}); "
            + "at most one Epic per project may be cross-cutting.");
    }

    /// <summary>
    /// #2429/#2432, the other half of "one epic per deliverable" — and the half that coverage cannot
    /// see. Coverage collapses epic attribution into a <c>HashSet</c> to answer "is this deliverable
    /// claimed?", which two epics naming the same deliverable satisfy just as well as one, and the
    /// cap above only counts the deliverable-<i>less</i> ones. Without this the owner's rule was
    /// enforced on one side and left to the prompt on the other.
    /// </summary>
    private static void CollectDuplicateDeliverableEpicIssues(
        WbsStructurePayload payload,
        IList<string> issues)
    {
        var duplicates = payload.Nodes
            .Where(node => node.Kind == WbsNodeKind.Epic && node.DeliverableId is not null)
            .GroupBy(node => node.DeliverableId!.Value)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicates)
        {
            issues.Add(
                $"Duplicate deliverable attribution: {group.Count()} Epics claim deliverable "
                + $"{group.Key:D} ({string.Join(", ", group.Select(epic => $"'{epic.EmittedKey}'"))}); "
                + "each deliverable is represented by exactly one Epic.");
        }
    }
}
