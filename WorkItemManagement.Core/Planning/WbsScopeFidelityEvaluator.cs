using System.Text.RegularExpressions;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// #2612: the only planning check whose denominator is the customer's own words.
/// </summary>
/// <remarks>
/// <para>Every other gate measures internal consistency — that the JSON is well-formed
/// (<c>WbsSummarizer.ValidatePayload</c>), that parents have children
/// (<see cref="WbsReviewReadinessEvaluator"/>), that cited ids resolve
/// (<c>WbsRequirementReferenceResolver</c>), that every catalogued requirement is covered
/// (<see cref="WbsTraceabilityEvaluator"/>). A plan can satisfy all of them and still be about
/// the wrong project, because the catalogue it is scored against is itself derived. Derived
/// error is invisible to a gate that scores against the derivation.</para>
/// <para>The incident: one deliverable and five objectives produced 95 nodes across six epics,
/// two of them dashboards for a project with one page, and named scope the customer never
/// mentioned. Structurally valid, fully traceable, 100% requirement-covered, review-ready — and
/// wrong. The customer found it by reading the plan.</para>
/// <para><b>Only proportionality blocks.</b> It is a count against a stated bound, so it has no
/// false-positive class: either the plan is bigger than the scope allows or it is not. The other
/// two findings rest on reading titles, and #2453 is the standing precedent for what a semantic
/// obligation costs when it becomes a hard gate — objective coverage was required, no output
/// could satisfy it, and every run failed. They are reported instead, where a human reading the
/// plan can weigh them.</para>
/// </remarks>
public static partial class WbsScopeFidelityEvaluator
{
    /// <summary>
    /// Words that carry no scope meaning. A node title matching an objective only through one of
    /// these has said nothing about it. Same intent as
    /// <c>WbsReviewReadinessEvaluator.FillerTokens</c>, kept separate because that list is tuned
    /// for parent/child mirroring and this one for customer vocabulary.
    /// </summary>
    private static readonly HashSet<string> ScopeStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "all", "an", "and", "any", "are", "as", "at", "be", "build", "by", "can", "create",
        "data", "deliver", "delivery", "develop", "development", "each", "enable", "ensure", "for",
        "from", "implement", "implementation", "in", "into", "is", "it", "its", "make", "manage",
        "management", "new", "of", "on", "or", "over", "per", "product", "project", "provide",
        "set", "setup", "so", "solution", "support", "system", "that", "the", "their", "them",
        "then", "this", "to", "up", "use", "user", "users", "via", "when", "which", "with", "work",
    };

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenSeparatorRegex();

    public static WbsScopeFidelityResult Evaluate(
        WbsStructurePayload payload,
        WbsTraceabilityContext context,
        WbsScopeFidelityBounds? bounds = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);

        var effectiveBounds = bounds ?? WbsScopeFidelityBounds.Default;
        var blocking = new List<string>();
        var observations = new List<string>();

        CollectProportionalityIssues(payload, context, effectiveBounds, blocking);
        CollectUnservedObjectiveObservations(payload, context, observations);
        CollectUninvitedScopeObservations(payload, context, observations);

        return new WbsScopeFidelityResult(blocking, observations);
    }

    /// <summary>
    /// Plan size against captured scope size, with the bound stated rather than implied.
    /// </summary>
    /// <remarks>
    /// <para>Nothing anywhere related the two before this: <c>DefineWorkBreakdownWorkflow</c>
    /// fans out children per parent with no global budget, so over-elaboration compounds at every
    /// level and is reported by nothing.</para>
    /// <para>Epic count is deliberately not checked here. <see cref="WbsEpicShapeRules"/> already
    /// bounds it from both sides — one Epic per deliverable, at most one cross-cutting — and two
    /// rules describing the same violation differently is the problem #2444 was filed to fix.</para>
    /// <para><b>Skipped entirely when nothing was captured.</b> A project with no deliverables and
    /// no objectives has no denominator, and scoring against the fixed allowance alone would fail
    /// every plan for a reason the customer cannot act on. That is the #2453 failure mode, and
    /// being unsatisfiable is worse than being absent.</para>
    /// </remarks>
    private static void CollectProportionalityIssues(
        WbsStructurePayload payload,
        WbsTraceabilityContext context,
        WbsScopeFidelityBounds bounds,
        IList<string> issues)
    {
        var scopeItems = WbsScopeFidelityBounds.ScopeItemCount(context);
        if (scopeItems == 0)
        {
            return;
        }

        // #3306: requirements are part of the denominator, because they are what the plan's
        // mandatory shape is driven by. Scoring a requirement-heavy project against the scope-item
        // share alone made this bound unsatisfiable — see NodesPerRequirement for the arithmetic.
        var requirementCount = WbsScopeFidelityBounds.RequirementCount(context);
        var allowance = bounds.NodeAllowance(scopeItems, requirementCount);
        if (payload.Nodes.Count <= allowance)
        {
            return;
        }

        issues.Add(
            $"Plan size is out of proportion to captured scope: {payload.Nodes.Count} nodes for "
            + $"{context.Deliverables.Count} deliverable(s), {context.Objectives.Count} "
            + $"objective(s) and {requirementCount} requirement(s). The bound is "
            + $"{bounds.FixedNodeAllowance} nodes, plus {bounds.NodesPerScopeItem} per captured "
            + $"deliverable or objective, plus {bounds.NodesPerRequirement} per captured "
            + $"requirement, so at most {allowance} here. Either the plan elaborates work nobody "
            + "asked for, or the scope captured for this project is incomplete.");
    }

    /// <summary>
    /// Objectives that no node appears to serve (#2612, expected behaviour 2).
    /// </summary>
    /// <remarks>
    /// Reported, never blocking. #2453 removed the requirement that a node <em>cite</em> an
    /// objective, for the good reason that an objective is a business outcome and no unit of work
    /// implements one — the product existing does. That ruling stands and this does not reopen it:
    /// nothing here asks for a citation, and nothing here fails a plan. It answers a weaker and
    /// still useful question — does the plan's vocabulary touch this objective at all — so an
    /// objective the plan silently forgot is visible to whoever reads it.
    /// </remarks>
    private static void CollectUnservedObjectiveObservations(
        WbsStructurePayload payload,
        WbsTraceabilityContext context,
        IList<string> observations)
    {
        if (context.Objectives.Count == 0 || payload.Nodes.Count == 0)
        {
            return;
        }

        var planVocabulary = BuildVocabulary(payload.Nodes
            .SelectMany(node => new[] { node.Title, node.Description }));

        foreach (var objective in context.Objectives)
        {
            var objectiveTerms = Tokenize(objective.Title);
            if (objectiveTerms.Count == 0)
            {
                // Nothing distinctive to look for — a title of stop words alone. Silence beats a
                // finding the reader cannot act on.
                continue;
            }

            if (objectiveTerms.Overlaps(planVocabulary))
            {
                continue;
            }

            observations.Add(
                $"No work appears to serve objective '{objective.RequirementNumber}' "
                + $"({objective.Title}): none of its terms appear in any node title or "
                + "description.");
        }
    }

    /// <summary>
    /// Epics that introduce a capability absent from the captured problem, objectives and
    /// deliverables (#2612, expected behaviour 3) — "person-level dashboard" in the incident.
    /// </summary>
    /// <remarks>
    /// <para>Only Epics, and only ones carrying no deliverable. An Epic attributed to a captured
    /// deliverable is by definition about something the customer stated, and one that names a
    /// deliverable which does not resolve is already a coverage finding on the customer screen.
    /// Features, stories and tasks are decomposition detail — they are supposed to introduce
    /// vocabulary their parent does not have, which is the whole point of decomposing.</para>
    /// <para>Reported rather than blocking, on the same reasoning as objective service: the
    /// judgement is lexical. A legitimate cross-cutting Epic — "CI pipeline", "Observability" —
    /// will often share no vocabulary with the captured scope, and failing the run for that would
    /// make the gate unsatisfiable for exactly the projects that need one.</para>
    /// </remarks>
    private static void CollectUninvitedScopeObservations(
        WbsStructurePayload payload,
        WbsTraceabilityContext context,
        IList<string> observations)
    {
        var capturedVocabulary = BuildCustomerStatedVocabulary(context);
        if (capturedVocabulary.Count == 0)
        {
            // Same guard as proportionality: with nothing captured, every Epic reads as invented.
            return;
        }

        // Every Epic, attributed or not. Restricting this to deliverable-less Epics would have
        // made the finding avoidable by attaching any valid deliverable id — an Epic named for an
        // unrelated capability would then pass here AND pass WbsEpicShapeRules, which only counts
        // attributions rather than reading them. An id proves the plan filed the Epic somewhere,
        // not that the Epic is about what was filed.
        foreach (var epic in payload.Nodes.Where(node => node.Kind == WbsNodeKind.Epic))
        {
            var epicTerms = Tokenize(epic.Title);
            if (epicTerms.Count == 0 || epicTerms.Overlaps(capturedVocabulary))
            {
                continue;
            }

            observations.Add(
                $"Epic '{epic.Title}' names scope that appears nowhere in the customer's captured "
                + "problems, objectives or deliverables. It is either cross-cutting work the plan "
                + "is right to add, or scope the plan invented.");
        }
    }

    /// <summary>
    /// What the customer said, and only that: problems, objectives, deliverables.
    /// </summary>
    /// <remarks>
    /// Requirements and success criteria are deliberately excluded, which is the whole point of
    /// #2612. Both are catalogues the platform derives, and requirements demonstrably carry
    /// platform-authored content — <c>ClarificationResponseWriter</c> writes <c>CL-###</c> rows
    /// from questions and options the platform generated, tagged
    /// <c>ClarificationAssumed</c>/<c>ConversationalRefinement</c>. Include them and a capability
    /// invented during clarification appears in the denominator, vouches for the Epic built on it,
    /// and this gate reports nothing — which is the exact defect #2610 describes and the reason
    /// scoring against derived artifacts cannot see derived error.
    /// </remarks>
    private static HashSet<string> BuildCustomerStatedVocabulary(WbsTraceabilityContext context) =>
        BuildVocabulary(
            context.Problems.Select(problem => problem.Statement)
                .Concat(context.Problems.Select(problem => problem.Impact))
                .Concat(context.Objectives.Select(objective => objective.Title))
                .Concat(context.Deliverables.Select(deliverable => deliverable.Name)));

    private static HashSet<string> BuildVocabulary(IEnumerable<string?> sources)
    {
        var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            vocabulary.UnionWith(Tokenize(source));
        }
        return vocabulary;
    }

    /// <summary>
    /// Distinctive terms only: stop words and one-character fragments carry no scope meaning, and
    /// matching on them would report every plan as faithful.
    /// </summary>
    private static HashSet<string> Tokenize(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        foreach (var token in TokenSeparatorRegex().Split(text))
        {
            if (token.Length > 1 && !ScopeStopWords.Contains(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }
}

/// <summary>
/// The bound plan size is measured against (#2612). Stated as data so the number appears in the
/// finding the customer reads, rather than being a constant only the code knows.
/// </summary>
/// <param name="FixedNodeAllowance">
/// Nodes every project may have regardless of captured scope — the cross-cutting Epic and its
/// decomposition, scaffolding, delivery setup. Without it a one-deliverable project would be held
/// to a budget that leaves no room for the work that is not about a deliverable at all.
/// </param>
/// <param name="NodesPerScopeItem">
/// Nodes allowed per captured deliverable or objective. Calibrated against the incident: one
/// deliverable and five objectives is six scope items, so 12 + (6 × 8) = 60 — comfortably above a
/// sane plan for that project, and well below the 95 nodes the run actually produced. The bound is
/// meant to catch elaboration that has run away, not to make plans small.
/// </param>
/// <param name="NodesPerRequirement">
/// #3306: nodes allowed per captured requirement, on top of the scope-item share.
/// <para><b>Why the scope-item share alone could not work.</b> The allowance was derived from
/// deliverables and objectives, but the plan's mandatory shape is driven by <em>requirements</em>:
/// the deterministic seed emits one Feature per requirement, and a Feature with no UserStory
/// beneath it fails review-readiness for shallow decomposition. So a project with more requirements
/// than <c>12 + 8 × scopeItems</c> had no satisfiable plan at all — with the budget live the deeper
/// levels were dropped and the gate refused the plan for having no stories; without it the same
/// plan was refused here for being out of proportion. A bound no output can satisfy is the #2453
/// failure mode, and this is it arriving through arithmetic.</para>
/// <para><b>Why three.</b> It is the smallest decomposition a captured requirement can legitimately
/// have and still pass the gate — the Feature the seed emits for it, one UserStory beneath that,
/// and one Task beneath that. Below three the bound forbids a shape the gate requires. Above it the
/// number would start licensing elaboration rather than admitting the floor, which is the job the
/// scope-item share already does.</para>
/// </param>
public sealed record WbsScopeFidelityBounds(
    int FixedNodeAllowance,
    int NodesPerScopeItem,
    int NodesPerRequirement = 3)
{
    public static WbsScopeFidelityBounds Default { get; } =
        new(FixedNodeAllowance: 12, NodesPerScopeItem: 8, NodesPerRequirement: 3);

    public static int ScopeItemCount(WbsTraceabilityContext? context) =>
        context is null ? 0 : context.Deliverables.Count + context.Objectives.Count;

    /// <summary>#3306: the requirement count the per-requirement floor is charged against.</summary>
    public static int RequirementCount(WbsTraceabilityContext? context) =>
        context is null ? 0 : context.Requirements.Count;

    /// <summary>
    /// The bound a finished plan is measured against, and the same number fan-out generates to.
    /// </summary>
    /// <remarks>
    /// #3306: <paramref name="requirementCount"/> is required rather than defaulted. A caller that
    /// omitted it would size against a smaller allowance than the one the plan is judged by, and
    /// that disagreement is exactly the defect this parameter exists to remove — it must not be
    /// reachable by forgetting an argument.
    /// </remarks>
    public int NodeAllowance(int scopeItems, int requirementCount) =>
        FixedNodeAllowance
        + (NodesPerScopeItem * Math.Max(0, scopeItems))
        + (NodesPerRequirement * Math.Max(0, requirementCount));

    /// <summary>
    /// A parent level is truncation-suspect unless both the project and the level are small.
    /// Growing captured scope must not weaken the check: a 48-feature emission with one story
    /// is truncated whether the project has six scope items or a hundred (#2628 review).
    /// </summary>
    /// <remarks>
    /// <see cref="FixedNodeAllowance"/> is the same constant that starts the proportionality
    /// ceiling. A project with more captured items than that is not "small", and a level
    /// with that many parents is large enough that missing children look like mid-emission
    /// truncation. With no captured scope there is no denominator, so the 80% rule stays.
    /// </remarks>
    public bool IsTruncationSuspectParentLevel(int parentCount, int scopeItems)
    {
        if (scopeItems <= 0)
            return true;

        if (scopeItems > FixedNodeAllowance)
            return true;

        return parentCount >= FixedNodeAllowance;
    }
}

/// <summary>
/// Fidelity findings, split by what they cost. <see cref="BlockingIssues"/> fold into the
/// review-ready verdict; <see cref="Observations"/> are reported to whoever reads the plan and
/// never fail a run — see the type remarks on <see cref="WbsScopeFidelityEvaluator"/> for why the
/// line is drawn there.
/// </summary>
public sealed record WbsScopeFidelityResult(
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> Observations);
