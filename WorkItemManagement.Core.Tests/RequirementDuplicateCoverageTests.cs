using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

/// <summary>
/// #2466: paraphrased duplicate requirements must not make the coverage gate unsatisfiable.
/// </summary>
public sealed class RequirementDuplicateCoverageTests
{
    private static readonly Guid CanonicalId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DuplicateId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string CanonicalDescription =
        "The inventory dashboard must display widget stock levels using color-coded badges for low, normal, and surplus states";

    private const string ParaphraseDescription =
        "Widget stock indicators on the inventory dashboard must use color-coded badges so status is visible at a glance";

    [Fact]
    public void Matcher_treats_paraphrased_restatements_as_the_same_obligation()
    {
        Assert.True(RequirementDuplicateMatcher.AreSameObligation(
            CanonicalDescription,
            ParaphraseDescription));
    }

    [Fact]
    public void Matcher_does_not_link_opposite_obligations_that_share_tokens()
    {
        const string allow =
            "System must allow administrators to delete customer records permanently after account closure";
        const string deny =
            "System must not allow administrators to delete customer records permanently after account closure";

        Assert.False(RequirementDuplicateMatcher.AreSameObligation(allow, deny));
    }

    /// <summary>
    /// #2633 / #2634: the hint bar accepts "encrypt … at rest" vs "delete … at rest"
    /// (three shared nouns). Coverage must not — that false positive would open the gate.
    /// </summary>
    [Theory]
    [InlineData("Encrypt customer records at rest", "Delete customer records at rest")]
    [InlineData("Create customer payment records at rest", "Archive customer payment records at rest")]
    public void Covered_work_refuses_overlapping_nouns_with_a_different_action(string requirement, string node)
    {
        Assert.True(
            RequirementDuplicateMatcher.LooksLikeSameObligation(requirement, node),
            "if the hint no longer matches, this no longer pins the stronger bar");
        Assert.False(RequirementDuplicateMatcher.LooksLikeCoveredWork(node, requirement));
    }

    [Fact]
    public void Covered_work_accepts_the_hosting_paraphrase_that_prompted_2633()
    {
        const string requirement =
            "Where will the dashboard web application be hosted → Azure App Service with private endpoint and VNet integration";
        const string node =
            "Provision App Service with private endpoint and VNet integration; disable public access";

        Assert.True(RequirementDuplicateMatcher.LooksLikeCoveredWork(node, requirement));
    }

    [Fact]
    public void A_plan_citing_the_canonical_requirement_passes_when_a_paraphrase_duplicate_also_exists()
    {
        var requirements = new[]
        {
            new WbsTraceabilityRequirement(CanonicalId, "R-012", CanonicalDescription),
            new WbsTraceabilityRequirement(
                DuplicateId,
                "R-022",
                ParaphraseDescription,
                DuplicateOfRequirementId: CanonicalId),
        };

        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "story-1",
                "epic-1",
                WbsNodeKind.UserStory,
                null,
                CanonicalId,
                "Color-coded widget stock badges",
                null,
                0),
        ]);

        var result = WbsTraceabilityEvaluator.Evaluate(
            payload,
            new WbsTraceabilityContext([], [])
            {
                Requirements = requirements,
            });

        Assert.True(result.IsReviewReady, string.Join("; ", result.Issues));
    }

    [Fact]
    public void A_plan_citing_the_linked_duplicate_passes_when_the_canonical_is_the_coverage_obligation()
    {
        var requirements = new[]
        {
            new WbsTraceabilityRequirement(CanonicalId, "R-012", CanonicalDescription),
            new WbsTraceabilityRequirement(
                DuplicateId,
                "R-022",
                ParaphraseDescription,
                DuplicateOfRequirementId: CanonicalId),
        };

        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload(
                "story-1",
                "epic-1",
                WbsNodeKind.UserStory,
                null,
                DuplicateId,
                "Color-coded widget stock badges",
                null,
                0),
        ]);

        var result = WbsTraceabilityEvaluator.Evaluate(
            payload,
            new WbsTraceabilityContext([], [])
            {
                Requirements = requirements,
            });

        Assert.True(result.IsReviewReady, string.Join("; ", result.Issues));
    }

    [Fact]
    public void SelectIndependentTraceabilityRequirements_keeps_one_obligation_per_duplicate_group()
    {
        var requirements = new[]
        {
            new WbsTraceabilityRequirement(CanonicalId, "R-012", CanonicalDescription),
            new WbsTraceabilityRequirement(DuplicateId, "R-022", ParaphraseDescription),
        };

        var independent = RequirementDuplicateEquivalence.SelectIndependentTraceabilityRequirements(requirements);

        Assert.Single(independent);
        Assert.Equal("R-012", independent[0].RequirementNumber);
    }

    [Fact]
    public void SelectIndependentTraceabilityRequirements_keeps_orphan_linked_rows_when_canonical_is_absent()
    {
        var requirements = new[]
        {
            new WbsTraceabilityRequirement(
                DuplicateId,
                "R-022",
                ParaphraseDescription,
                DuplicateOfRequirementId: CanonicalId),
        };

        var independent = RequirementDuplicateEquivalence.SelectIndependentTraceabilityRequirements(requirements);

        Assert.Single(independent);
        Assert.Equal(DuplicateId, independent[0].Id);
    }
}
