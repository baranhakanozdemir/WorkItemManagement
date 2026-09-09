using System.Globalization;
using System.Text.RegularExpressions;


namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Deterministic gate asserting that a WBS payload remains traceable to the customer's
/// stated objectives and deliverables (#2288).
/// </summary>
public static partial class WbsTraceabilityEvaluator
{
    public static WbsReviewReadinessResult Evaluate(
        WbsStructurePayload payload,
        WbsTraceabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<string>();
        var observations = new List<string>();

        if (context.Deliverables.Count > 0)
        {
            CollectDeliverableCoverageIssues(payload, context.Deliverables, issues);
        }

        // #2453 stopped requiring a node to *implement* an objective as a unit of work — that
        // pairing was unsatisfiable while the catalog also said objectives were not citable.
        // #2610 puts objectives (and the problem statement) back in the cite catalog, so
        // coverage of those identities is now the requirement-coverage pass below. The
        // dangling-reference pass here stays for prose "OBJ-N" mentions that do not resolve.
        if (context.Objectives.Count > 0)
        {
            CollectInvalidObjectiveReferenceIssues(payload, context.Objectives, issues);
        }

        // Unconditional, unlike the two above. Guarding on a non-empty catalogue would also
        // skip the dangling-reference pass, and that pass is at its most useful precisely when
        // the catalogue is empty: a persisted node still pointing at a requirement that has
        // since been deleted would then read as review-ready. The coverage loop is naturally
        // empty for an empty catalogue, so there is nothing to save by guarding.
        CollectRequirementCoverageIssues(payload, context.Requirements, issues, observations);

        // Unconditional for the same reason as requirements above: the dangling-reference pass is at
        // its most useful when the catalogue is empty.
        CollectAcceptanceCriterionCoverageIssues(payload, context.AcceptanceCriteria, issues);

        // #2429, unconditional because both are properties of the epics the model emitted rather
        // than of any catalogue. #2444 moved the rules themselves to WbsEpicShapeRules so the
        // structuring repair loop can offer the model the same wording this gate reports; the
        // decision about what a violation costs stays here, where it is only a reviewReady flag.
        issues.AddRange(WbsEpicShapeRules.Collect(payload));

        return new WbsReviewReadinessResult(issues.Count == 0, issues)
        {
            Observations = observations,
        };
    }

    /// <summary>
    /// #2400: coverage, not attribution — the same rule #2371 settled for deliverables. A node
    /// may legitimately implement no requirement (scaffolding, spikes, cross-cutting work), so
    /// nothing here demands that nodes carry a <c>requirementId</c>. What must hold is the
    /// reverse: a requirement the customer stated with no work against it is a gap in the plan.
    ///
    /// <para>#2633: a missing <c>requirementId</c> is not itself a gap when a node already
    /// describes the same obligation. That finding is reported as an observation and does
    /// not fold into <see cref="WbsReviewReadinessResult.IsReviewReady"/>.</para>
    ///
    /// <para>Unresolved references are the resolver's job (the reference resolver on the writing side, which stays with the consumer that persists nodes),
    /// which rejects the payload before it is persisted. This runs over payloads that are
    /// already stored, so an unresolvable reference here would mean a row that got past that
    /// gate — reported rather than ignored, because silently treating it as "no requirement"
    /// is how a broken link becomes invisible.</para>
    /// </summary>
    private static void CollectRequirementCoverageIssues(
        WbsStructurePayload payload,
        IReadOnlyList<WbsTraceabilityRequirement> requirements,
        IList<string> issues,
        IList<string> observations)
    {
        var referenced = payload.Nodes
            .Where(node => node.RequirementId is not null)
            .Select(node => node.RequirementId!.Value)
            .ToHashSet();

        var groups = RequirementDuplicateEquivalence.BuildTraceabilityCoverageGroups(requirements);
        var coverageObligations = RequirementDuplicateEquivalence.SelectIndependentTraceabilityRequirements(
            requirements);

        foreach (var requirement in coverageObligations)
        {
            if (RequirementDuplicateEquivalence.IsTraceabilityRequirementCovered(
                    requirement,
                    referenced,
                    groups))
            {
                continue;
            }

            // #2782: a reviewer decided this clarification answer is an assumption or constraint
            // that no node implements, so it is discharged rather than blocking.
            //
            // Reported as an observation rather than dropped: #2618 exists because
            // clarification-derived scope going unnoticed caused a real problem, and the rule that
            // fixes this must not reintroduce it. The item stays visible, it just stops failing the
            // gate — surfaced and decided, never auto-ignored.
            //
            // Scoped to clarification-derived requirements deliberately. The classifier for
            // "is this deliverable work" is a person, not the data — source category records who
            // decided, not what the answer meant — but that does not make disposition a general
            // escape hatch. An ordinary requirement carrying a disposition is still a coverage gap.
            // Attribution is part of the condition, not decoration. The discharge is defined as a
            // recorded human decision, so a disposition with nobody attached is a field that got
            // set rather than a decision that got made — and the whole design rests on the
            // difference.
            if (requirement.IsClarificationDerived
                && requirement.CoverageDisposition == RequirementCoverageDisposition.NoDeliverableWork
                && !string.IsNullOrWhiteSpace(requirement.CoverageDispositionBy))
            {
                observations.Add(
                    $"Requirement '{requirement.RequirementNumber}' ({requirement.Description}) "
                    + "is uncited and discharged: a reviewer recorded it as an assumption or "
                    + "constraint with no deliverable work.");
                continue;
            }

            // #3223: an objective or problem statement is what the project is FOR, not work it
            // contains. It is met by the plan as a whole, so demanding a node that cites it either
            // fails the gate forever — which it did, on every run — or forces a synthetic row into
            // the customer's plan that describes no work. Still reported, so it stays visible and
            // an empty plan cannot go green on goals nobody planned against.
            if (requirement.CoverageObligation == WbsCoverageObligationKind.SatisfiedByPlan)
            {
                observations.Add(
                    $"Goal '{requirement.RequirementNumber}' ({requirement.Description}) is "
                    + "satisfied by the plan as a whole rather than by any single node.");
                continue;
            }

            // #2633: a missing RequirementId is a citation defect, not a scope gap, when a node
            // already describes the same obligation. Only a requirement with no matching work
            // stays a blocking coverage gap.
            if (TryFindUncitedCoveringNode(requirement, payload, out var covering))
            {
                observations.Add(
                    $"Requirement '{requirement.RequirementNumber}' ({requirement.Description}) "
                    + $"is covered by '{covering.Title}' without a requirementId citation.");
                continue;
            }

            issues.Add(
                $"Coverage gap: Requirement '{requirement.RequirementNumber}' "
                + $"({requirement.Description}) is not implemented by any WBS node.");
        }

        var requirementIds = requirements.Select(requirement => requirement.Id).ToHashSet();
        foreach (var node in payload.Nodes)
        {
            if (node.RequirementId is Guid requirementId && !requirementIds.Contains(requirementId))
            {
                issues.Add(
                    $"Unresolved requirement reference: Node '{node.EmittedKey}' references requirementId "
                    + $"{requirementId:D} which does not resolve to a requirement on this project.");
            }
        }
    }

    /// <summary>
    /// #2405: coverage, not attribution — the rule #2371 settled for deliverables and #2400 for
    /// requirements. A story may legitimately satisfy no criterion (scaffolding, spikes), so nothing
    /// here demands that nodes carry criteria. What must hold is the reverse: a criterion the
    /// <b>customer</b> stated with no work against it is a gap in the plan.
    ///
    /// <para>Only customer-stated criteria are required to be covered. A platform-proposed criterion
    /// left uncovered is the platform's own suggestion going unused, which is not a promise broken
    /// to anyone — and <see cref="WbsTraceabilityAcceptanceCriterion.IsCustomerStated"/> carries that
    /// judgement from the entity rather than re-deriving it here, so the two cannot disagree about
    /// whose criterion may be dropped.</para>
    /// </summary>
    private static void CollectAcceptanceCriterionCoverageIssues(
        WbsStructurePayload payload,
        IReadOnlyList<WbsTraceabilityAcceptanceCriterion> criteria,
        IList<string> issues)
    {
        var satisfied = payload.Nodes
            .SelectMany(node => node.AcceptanceCriterionIds ?? [])
            .ToHashSet();

        foreach (var criterion in criteria)
        {
            if (!criterion.IsCustomerStated || satisfied.Contains(criterion.Id))
            {
                continue;
            }

            issues.Add(
                $"Coverage gap: Acceptance criterion '{criterion.RequirementNumber}' "
                + $"({criterion.Statement}) is not satisfied by any WBS node.");
        }

        var criterionIds = criteria.Select(criterion => criterion.Id).ToHashSet();
        foreach (var node in payload.Nodes)
        {
            foreach (var criterionId in node.AcceptanceCriterionIds ?? [])
            {
                if (criterionIds.Contains(criterionId))
                {
                    continue;
                }

                issues.Add(
                    $"Unresolved acceptance criterion reference: Node '{node.EmittedKey}' references "
                    + $"{criterionId:D} which does not resolve to a success criterion on this project.");
            }
        }
    }

    private static void CollectDeliverableCoverageIssues(
        WbsStructurePayload payload,
        IReadOnlyList<WbsTraceabilityDeliverable> deliverables,
        IList<string> issues)
    {
        var deliverableIds = deliverables
            .Select(deliverable => deliverable.Id)
            .ToHashSet();

        var epicDeliverableIds = payload.Nodes
            .Where(node => node.Kind == WbsNodeKind.Epic && node.DeliverableId is not null)
            .Select(node => node.DeliverableId!.Value)
            .ToHashSet();

        foreach (var deliverable in deliverables)
        {
            if (!epicDeliverableIds.Contains(deliverable.Id))
            {
                issues.Add(
                    $"Coverage gap: Deliverable '{deliverable.Name}' ({deliverable.Id}) has no Epic in the WBS.");
            }
        }

        foreach (var epic in payload.Nodes.Where(node => node.Kind == WbsNodeKind.Epic))
        {
            if (epic.DeliverableId is null)
            {
                continue;
            }

            if (!deliverableIds.Contains(epic.DeliverableId.Value))
            {
                issues.Add(
                    $"Unresolved deliverable reference: Epic '{epic.EmittedKey}' references deliverableId "
                    + $"{epic.DeliverableId} which does not resolve to a deliverable on this project.");
            }
        }
    }

    private static void CollectInvalidObjectiveReferenceIssues(
        WbsStructurePayload payload,
        IReadOnlyList<WbsTraceabilityObjective> objectives,
        IList<string> issues)
    {
        var validObjectiveNumbers = objectives
            .Select(objective => ParseObjectiveNumber(objective.RequirementNumber))
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .ToHashSet();

        foreach (var node in payload.Nodes)
        {
            var nodeText = BuildNodeText(node);
            foreach (Match match in ObjectiveReferenceRegex().Matches(nodeText))
            {
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var referencedNumber))
                {
                    continue;
                }

                if (!validObjectiveNumbers.Contains(referencedNumber))
                {
                    issues.Add(
                        $"Node '{node.EmittedKey}' references {match.Value}, "
                        + "which is not a stated customer objective.");
                }
            }
        }
    }

    /// <summary>
    /// #2633: the work is in the plan and simply does not name the requirement.
    /// Uses <see cref="RequirementDuplicateMatcher.LooksLikeCoveredWork"/>, not the
    /// three-token hint — a false match here opens the gate.
    /// </summary>
    internal static bool TryFindUncitedCoveringNode(
        WbsTraceabilityRequirement requirement,
        WbsStructurePayload payload,
        out WbsNodePayload covering)
    {
        var match = payload.Nodes.FirstOrDefault(node =>
            node.RequirementId is null
            && RequirementDuplicateMatcher.LooksLikeCoveredWork(
                BuildNodeText(node),
                requirement.Description));

        if (match is null)
        {
            covering = null!;
            return false;
        }

        covering = match;
        return true;
    }

    internal static int? ParseObjectiveNumber(string requirementNumber)
    {
        if (string.IsNullOrWhiteSpace(requirementNumber))
        {
            return null;
        }

        var match = ObjectiveReferenceRegex().Match(requirementNumber.Trim());
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string BuildNodeText(WbsNodePayload node) =>
        string.Join(
            ' ',
            new[] { node.Title, node.Description }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

    [GeneratedRegex(@"\bOBJ-(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ObjectiveReferenceRegex();
}
