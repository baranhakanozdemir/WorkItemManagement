using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

/// <summary>
/// #2612 — the gate whose denominator is the customer's own words.
/// </summary>
/// <remarks>
/// The run that prompted it was structurally valid, fully traceable, 100% requirement-covered and
/// review-ready: 95 nodes and six epics for one deliverable and five objectives, two of them
/// dashboards for a project with one page. Every existing gate measures internal consistency, so a
/// plan organised around the wrong things passes all of them.
/// </remarks>
public sealed class WbsScopeFidelityEvaluatorTests
{
    // ── Proportionality: the blocking half ───────────────────────────────

    [Fact]
    public void The_incident_plan_is_rejected_as_out_of_proportion()
    {
        // One deliverable, five objectives, 95 nodes. The shape of the real failure.
        var deliverable = Guid.NewGuid();
        var context = new WbsTraceabilityContext(
            Objectives(5),
            [new WbsTraceabilityDeliverable(deliverable, "Radiology reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(PayloadOfSize(95, deliverable), context);

        var issue = Assert.Single(result.BlockingIssues);
        Assert.Contains("95 nodes", issue, StringComparison.Ordinal);
        // The bound is stated, not implied: 12 + (6 x 8).
        Assert.Contains("at most 60", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plan_within_the_bound_passes()
    {
        var deliverable = Guid.NewGuid();
        var context = new WbsTraceabilityContext(
            Objectives(5),
            [new WbsTraceabilityDeliverable(deliverable, "Radiology reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(PayloadOfSize(60, deliverable), context);

        Assert.Empty(result.BlockingIssues);
    }

    [Fact]
    public void The_bound_grows_with_captured_scope()
    {
        // The same 95 nodes are proportionate for a project that captured ten deliverables and
        // ten objectives. The rule is proportion, not a plan-size cap.
        var context = new WbsTraceabilityContext(
            Objectives(10),
            Enumerable.Range(0, 10)
                .Select(i => new WbsTraceabilityDeliverable(Guid.NewGuid(), $"Deliverable {i}"))
                .ToArray());

        var result = WbsScopeFidelityEvaluator.Evaluate(PayloadOfSize(95, Guid.NewGuid()), context);

        Assert.Empty(result.BlockingIssues);
    }

    [Fact]
    public void Nothing_captured_means_no_proportionality_finding()
    {
        // With no denominator there is no proportion to measure, and scoring against the fixed
        // allowance alone would fail every plan for a reason nobody can act on. #2453 is what
        // happens when a gate becomes unsatisfiable.
        var result = WbsScopeFidelityEvaluator.Evaluate(
            PayloadOfSize(400, Guid.NewGuid()),
            new WbsTraceabilityContext([], []));

        Assert.Empty(result.BlockingIssues);
    }

    [Fact]
    public void Truncation_suspect_does_not_loosen_as_captured_scope_grows()
    {
        // #2628 review: FixedNodeAllowance + scopeItems made a 48-feature emission look
        // complete once the project had 100 captured items. The exemption is only for a
        // small project and a small level.
        var bounds = WbsScopeFidelityBounds.Default;

        Assert.False(bounds.IsTruncationSuspectParentLevel(5, 6));
        Assert.True(bounds.IsTruncationSuspectParentLevel(12, 6));
        Assert.True(bounds.IsTruncationSuspectParentLevel(48, 6));
        Assert.True(bounds.IsTruncationSuspectParentLevel(48, 100));
        Assert.True(bounds.IsTruncationSuspectParentLevel(5, 100));
        Assert.True(bounds.IsTruncationSuspectParentLevel(5, 0));
    }

    [Fact]
    public void The_bound_is_the_one_the_caller_supplies()
    {
        var context = new WbsTraceabilityContext(
            Objectives(1),
            [new WbsTraceabilityDeliverable(Guid.NewGuid(), "Portal")]);
        var strict = new WbsScopeFidelityBounds(FixedNodeAllowance: 1, NodesPerScopeItem: 1);

        var result = WbsScopeFidelityEvaluator.Evaluate(PayloadOfSize(10, Guid.NewGuid()), context, strict);

        Assert.Contains("at most 3", Assert.Single(result.BlockingIssues), StringComparison.Ordinal);
    }

    // ── Objective service: reported, never blocking ──────────────────────

    [Fact]
    public void An_objective_no_node_mentions_is_observed_but_does_not_block()
    {
        // #2453 removed the requirement that a node CITE an objective, and this does not reopen
        // it: no citation is asked for and no plan is failed. It answers the weaker question —
        // does the plan's vocabulary touch this objective at all.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
            new WbsNodePayload("story-1", "epic-1", WbsNodeKind.UserStory, null, null, "Open a study", null, 1),
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Reduce radiologist reporting turnaround", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Empty(result.BlockingIssues);
        Assert.Contains(
            result.Observations,
            observation => observation.Contains("OBJ-1", StringComparison.Ordinal)
                && observation.Contains("No work appears to serve", StringComparison.Ordinal));
    }

    [Fact]
    public void An_objective_the_plan_speaks_to_is_not_observed()
    {
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
            new WbsNodePayload(
                "story-1", "epic-1", WbsNodeKind.UserStory, null, null,
                "Show reporting turnaround on the dashboard", null, 1),
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Reduce radiologist reporting turnaround", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Empty(result.Observations);
    }

    [Fact]
    public void Matching_on_a_stop_word_alone_does_not_count_as_service()
    {
        // "Build the system" shares "build", "the" and "system" with almost any plan. If those
        // counted, every objective would read as served and the check would report nothing ever.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Build the system", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Build the system for auditors", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Contains(result.Observations, o => o.Contains("OBJ-1", StringComparison.Ordinal));
    }

    // ── Invented scope: reported, never blocking ─────────────────────────

    [Fact]
    public void An_epic_naming_scope_nobody_asked_for_is_observed()
    {
        // "person-level dashboard", from the incident.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
            new WbsNodePayload("epic-2", null, WbsNodeKind.Epic, null, null, "Person-level dashboard", null, 1),
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Radiologists read studies faster", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Empty(result.BlockingIssues);
        Assert.Contains(
            result.Observations,
            observation => observation.Contains("Person-level dashboard", StringComparison.Ordinal)
                && observation.Contains("appears nowhere in the customer's captured", StringComparison.Ordinal));
    }

    [Fact]
    public void Attaching_a_deliverable_id_does_not_excuse_an_epic_that_is_about_something_else()
    {
        // An id proves the plan filed the Epic somewhere, not that the Epic is about what it was
        // filed under. Checking only deliverable-less Epics would make the finding avoidable by
        // attaching any valid id — and WbsEpicShapeRules counts attributions without reading them,
        // so nothing else would catch it either.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Zephyr", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Radiologists read studies faster", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Contains(result.Observations, o => o.Contains("Zephyr", StringComparison.Ordinal));
    }

    [Fact]
    public void An_epic_named_for_its_deliverable_is_not_reported()
    {
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [new WbsTraceabilityDeliverable(deliverable, "Radiology reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Empty(result.Observations);
    }

    [Fact]
    public void Decomposition_below_the_epic_may_introduce_its_own_vocabulary()
    {
        // A story is supposed to say something its parent did not — that is what decomposing is.
        // Reporting stories as invented scope would report every plan.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
            new WbsNodePayload("story-1", "epic-1", WbsNodeKind.UserStory, null, null, "Zephyr caching layer", null, 1),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.DoesNotContain(result.Observations, o => o.Contains("Zephyr", StringComparison.Ordinal));
    }

    [Fact]
    public void Clarification_residue_cannot_vouch_for_the_scope_it_invented()
    {
        // The failure this gate exists to see. ClarificationResponseWriter writes CL-### rows from
        // platform-authored questions and assumptions, so "person-level view" can enter the
        // requirement catalogue without the customer ever saying it. Score against that catalogue
        // and the invented Epic cites the invention as its justification — which is #2610's
        // mechanism, and precisely why a derived denominator cannot see derived error.
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, null, null, "Person-level view", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [new WbsTraceabilityDeliverable(Guid.NewGuid(), "Radiology reading portal")])
        {
            Requirements =
            [
                new WbsTraceabilityRequirement(Guid.NewGuid(), "CL-004", "Provide a person-level view"),
            ],
        };

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        Assert.Contains(
            result.Observations,
            o => o.Contains("Person-level view", StringComparison.Ordinal));
    }

    [Fact]
    public void The_captured_problem_is_part_of_the_customers_words()
    {
        // An Epic can be grounded in the problem statement without matching any deliverable name,
        // and the problem is the one part of the captured scope no other catalogue stands in for.
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, null, null, "Audit trail", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [new WbsTraceabilityDeliverable(Guid.NewGuid(), "Reading portal")])
        {
            Problems =
            [
                new WbsTraceabilityProblem("PRB-1", "Nobody can reconstruct who changed a report", "Failed audit reviews"),
            ],
        };

        var result = WbsScopeFidelityEvaluator.Evaluate(payload, context);

        // "audit" reaches the vocabulary through the problem's impact, which is often the only
        // place a customer names the domain.
        Assert.Empty(result.Observations);
    }

    // ── The verdict wiring ───────────────────────────────────────────────

    [Fact]
    public void Plan_readiness_blocks_on_proportionality_and_reports_the_rest()
    {
        var deliverable = Guid.NewGuid();
        var nodes = new List<WbsNodePayload>
        {
            new("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
        };
        nodes.AddRange(Enumerable.Range(0, 94).Select(i => new WbsNodePayload(
            $"task-{i}", "epic-1", WbsNodeKind.Task, null, null, $"Step {i}", null, i + 1)));

        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Reduce reporting turnaround", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var readiness = WbsPlanReadiness.Evaluate(
            new WbsStructurePayload(nodes),
            WbsStructureSource.Summarizer,
            context);

        Assert.False(readiness.IsReviewReady);
        Assert.NotEmpty(readiness.ScopeFidelityIssues);
        Assert.NotEmpty(readiness.ScopeFidelityObservations);
    }

    [Fact]
    public void Plan_readiness_is_unchanged_for_a_plan_in_proportion()
    {
        // Guards the wiring in both directions: a fidelity dimension that fails everything is
        // indistinguishable from one that catches the incident, from the failing side alone.
        var deliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, deliverable, null, "Reading portal", null, 0),
            new WbsNodePayload(
                "story-1", "epic-1", WbsNodeKind.UserStory, null, null, "Reduce reporting turnaround", null, 1)
            {
                AcceptanceCriteria = ["Turnaround is visible on the study list"],
                CanCompleteInOneAgentSession = true,
            },
        ]);
        var context = new WbsTraceabilityContext(
            [new WbsTraceabilityObjective("OBJ-1", "Reduce reporting turnaround", 1)],
            [new WbsTraceabilityDeliverable(deliverable, "Reading portal")]);

        var readiness = WbsPlanReadiness.Evaluate(payload, WbsStructureSource.Summarizer, context);

        Assert.Empty(readiness.ScopeFidelityIssues);
        Assert.Empty(readiness.ScopeFidelityObservations);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static WbsTraceabilityObjective[] Objectives(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new WbsTraceabilityObjective($"OBJ-{i}", $"Objective {i}", i))
            .ToArray();

    /// <summary>One epic on the given deliverable, padded with tasks to the requested node count.</summary>
    private static WbsStructurePayload PayloadOfSize(int nodeCount, Guid deliverableId)
    {
        var nodes = new List<WbsNodePayload>
        {
            new("epic-1", null, WbsNodeKind.Epic, deliverableId, null, "Objective 1 portal", null, 0),
        };

        nodes.AddRange(Enumerable.Range(0, nodeCount - 1).Select(i => new WbsNodePayload(
            $"task-{i}", "epic-1", WbsNodeKind.Task, null, null, $"Objective {(i % 10) + 1} step {i}", null, i + 1)));

        return new WbsStructurePayload(nodes);
    }
}
