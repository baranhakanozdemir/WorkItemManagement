using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

public sealed class WbsPreGeneratedDomainLayerRulesTests
{

    // Ported from plusteam (WorkItemManagement#6). The eligibility and prompt-formatting
    // tests are NOT ported: those members stayed behind because they resolve a delivery
    // stack and read a project context, neither of which this package knows about. What is
    // ported is every test of the members that moved.

    [Theory]
    [InlineData("Create Invoice entity")]
    [InlineData("Create customer model")]
    [InlineData("Implement Product repository")]
    [InlineData("Add Order controller")]
    [InlineData("Create basic CRUD for Users")]
    [InlineData("Setup DbContext and DbSets")]
    [InlineData("Scaffold Subscription API controller")]
    [InlineData("Implement Member repository interface")]
    [InlineData("Create database tables for Tenants")]
    [InlineData("Add CRUD endpoints for Catalog")]
    public void IsBoilerplateStoryTitle_identifies_boilerplate_entity_and_crud_stories(string title)
    {
        Assert.True(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle(title));
    }

    [Theory]
    [InlineData("Implement Stripe webhook processing for invoice payments")]
    [InlineData("Calculate tiered customer discount rules")]
    [InlineData("Enforce order approval workflow for large purchases")]
    [InlineData("Integrate Slack notifications on team invite")]
    [InlineData("User signs in with OAuth2 provider")]
    [InlineData("Export quarterly sales report as PDF")]
    public void IsBoilerplateStoryTitle_allows_business_logic_and_workflow_stories(string title)
    {
        Assert.False(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle(title));
    }

    [Fact]
    public void IsBoilerplateStoryTitle_detects_named_pre_generated_entities()
    {
        string[] entities = ["Invoice", "Customer", "Subscription"];

        Assert.True(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle("Create Invoice entity", entities));
        Assert.True(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle("Implement Customer repository", entities));
        Assert.True(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle("Subscription CRUD", entities));
        Assert.False(WbsPreGeneratedDomainLayerRules.IsBoilerplateStoryTitle("Process Invoice payment retry", entities));
    }

    [Fact]
    public void CollectBoilerplateIssues_flags_boilerplate_user_stories_and_tasks()
    {
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-billing", null, WbsNodeKind.Epic, Guid.NewGuid(), null, "Billing Engine", null, 0),
            new WbsNodePayload("feature-invoices", "epic-billing", WbsNodeKind.Feature, null, null, "Invoice Management", null, 1),
            new WbsNodePayload("story-create-entity", "feature-invoices", WbsNodeKind.UserStory, null, null, "Create Invoice entity", null, 2),
            new WbsNodePayload("story-calc-tax", "feature-invoices", WbsNodeKind.UserStory, null, null, "Calculate regional tax rules on invoice finalization", null, 3),
            new WbsNodePayload("task-repo", "story-calc-tax", WbsNodeKind.Task, null, null, "Implement Invoice repository", null, 4),
        ]);

        var issues = WbsPreGeneratedDomainLayerRules.CollectBoilerplateIssues(payload);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, issue => issue.Contains("story-create-entity", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("task-repo", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Contains("story-calc-tax", StringComparison.Ordinal));
    }
}
