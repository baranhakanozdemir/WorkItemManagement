using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

public sealed class WbsTraceabilityEvaluatorTests
{
    [Fact]
    public void Evaluate_passes_when_no_objectives_or_deliverables_are_captured()
    {
        var payload = EpicOnlyPayload(Guid.NewGuid());
        var context = new WbsTraceabilityContext([], []);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.True(result.IsReviewReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_requires_every_stated_deliverable_to_have_an_epic()
    {
        var deliverableA = Guid.NewGuid();
        var deliverableB = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-a", null, WbsNodeKind.Epic, deliverableA, null, "Portal", null, 0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [
                new WbsTraceabilityDeliverable(deliverableA, "Customer Portal"),
                new WbsTraceabilityDeliverable(deliverableB, "Admin Console"),
            ]);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.False(result.IsReviewReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains("Admin Console", StringComparison.Ordinal)
                && issue.Contains("Coverage gap", StringComparison.Ordinal)
                && issue.Contains("has no Epic", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_lists_every_untraced_deliverable()
    {
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "epic-foreign",
                null,
                WbsNodeKind.Epic,
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                null,
                "Foreign epic",
                null,
                0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [
                new WbsTraceabilityDeliverable(Guid.NewGuid(), "Backend Service"),
                new WbsTraceabilityDeliverable(Guid.NewGuid(), "Desktop Agent"),
                new WbsTraceabilityDeliverable(Guid.NewGuid(), "Mobile Application"),
            ]);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("Backend Service", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("Desktop Agent", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("Mobile Application", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("Unresolved deliverable reference", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_rejects_epic_deliverable_ids_that_are_not_stated()
    {
        var statedDeliverable = Guid.NewGuid();
        var foreignDeliverable = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "epic-foreign",
                null,
                WbsNodeKind.Epic,
                foreignDeliverable,
                null,
                "Foreign epic",
                null,
                0),
        ]);
        var context = new WbsTraceabilityContext(
            [],
            [new WbsTraceabilityDeliverable(statedDeliverable, "Customer Portal")]);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.False(result.IsReviewReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains("Unresolved deliverable reference", StringComparison.Ordinal)
                && issue.Contains("does not resolve", StringComparison.Ordinal));
    }

    /// <summary>
    /// #2453. The owner decision: a WBS node is never required to cite an objective. An objective is
    /// a business outcome the product achieves as a whole, and requiring a unit of work to implement
    /// one made the gate unsatisfiable — #2450 keeps objectives out of the requirement catalog, so
    /// structuring is told they are not citable and the plan was then failed for not citing them.
    /// OBJ-003 here has nothing against it and the plan is ready anyway.
    /// </summary>
    [Fact]
    public void Evaluate_does_not_require_an_objective_to_be_referenced_by_any_node()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "epic-portal",
                null,
                WbsNodeKind.Epic,
                deliverableId,
                null,
                "OBJ-1: Registration and sign-in",
                "Covers objective 1.",
                0),
            new WbsNodePayload(
                "feature-profile",
                "epic-portal",
                WbsNodeKind.Feature,
                null,
                null,
                "OBJ-2: Profile creation",
                null,
                1),
        ]);
        var context = new WbsTraceabilityContext(
            [
                new WbsTraceabilityObjective("OBJ-001", "Customer can register and sign in", 1),
                new WbsTraceabilityObjective("OBJ-002", "Customer creates a profile", 2),
                new WbsTraceabilityObjective("OBJ-003", "Customer can unregister", 3),
            ],
            [new WbsTraceabilityDeliverable(deliverableId, "Customer Portal")]);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.True(result.IsReviewReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_passes_when_all_objectives_and_deliverables_are_traceable()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "epic-portal",
                null,
                WbsNodeKind.Epic,
                deliverableId,
                null,
                "Customer Portal",
                "OBJ-1 registration, OBJ-2 profile, OBJ-3 unregister.",
                0),
        ]);
        var context = new WbsTraceabilityContext(
            [
                new WbsTraceabilityObjective("OBJ-001", "Customer can register and sign in", 1),
                new WbsTraceabilityObjective("OBJ-002", "Customer creates a profile", 2),
                new WbsTraceabilityObjective("OBJ-003", "Customer can unregister", 3),
            ],
            [new WbsTraceabilityDeliverable(deliverableId, "Customer Portal")]);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.True(result.IsReviewReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_rejects_obj_references_that_do_not_match_stated_objectives()
    {
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "epic-derived",
                null,
                WbsNodeKind.Epic,
                Guid.NewGuid(),
                null,
                "Pack Delivery",
                "OBJ-5: Pack intake, operator mapping, and delivery.",
                0),
        ]);
        var context = new WbsTraceabilityContext(
            [
                new WbsTraceabilityObjective("OBJ-001", "Customer can register and sign in", 1),
                new WbsTraceabilityObjective("OBJ-002", "Customer creates a profile", 2),
                new WbsTraceabilityObjective("OBJ-003", "Customer can reset password", 3),
                new WbsTraceabilityObjective("OBJ-004", "Customer can unregister", 4),
            ],
            []);

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.False(result.IsReviewReady);
        // #2453: the only remaining objective finding. OBJ-001..OBJ-004 are each unreferenced and
        // that is no longer a finding, so this issue stands alone — a node asserting something
        // untrue about the customer's own objectives, which is a defect regardless of coverage.
        var issue = Assert.Single(result.Issues);
        Assert.Contains("OBJ-5", issue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a stated customer objective", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void An_uncited_objective_in_the_cite_catalog_is_a_coverage_gap()
    {
        var objectiveId = Guid.NewGuid();
        var payload = new WbsStructurePayload([]);
        var context = new WbsTraceabilityContext([], [])
        {
            Requirements =
            [
                new WbsTraceabilityRequirement(objectiveId, "OBJ-001", "Customer can register"),
            ],
        };

        var result = WbsTraceabilityEvaluator.Evaluate(payload, context);

        Assert.False(result.IsReviewReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains("OBJ-001", StringComparison.Ordinal)
                && issue.Contains("Coverage gap", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseObjectiveNumber_normalizes_zero_padded_requirement_numbers()
    {
        Assert.Equal(4, WbsTraceabilityEvaluator.ParseObjectiveNumber("OBJ-004"));
        Assert.Equal(1, WbsTraceabilityEvaluator.ParseObjectiveNumber("obj-1"));
        Assert.Null(WbsTraceabilityEvaluator.ParseObjectiveNumber("DERIVED-1"));
    }

    private static WbsStructurePayload EpicOnlyPayload(Guid deliverableId) =>
        new(
        [
            new WbsNodePayload("epic-only", null, WbsNodeKind.Epic, deliverableId, null, "Epic", null, 0),
        ]);
}
