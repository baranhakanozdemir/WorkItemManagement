using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

/// <summary>
/// One test per defect the read boundary exists to prevent. Each of these is a bug that reached
/// production in plusteam before the corresponding step was added, and each fails quietly without
/// it: the payload parses, evaluation runs, and the verdict is wrong.
/// </summary>
public sealed class WbsStructurePayloadReaderTests
{
    private const string DeliverableId = "11111111-1111-1111-1111-111111111111";
    private const string RequirementId = "22222222-2222-2222-2222-222222222222";
    private const string CriterionId = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public void Reads_a_well_formed_payload()
    {
        var json = $$"""
        {
          "nodes": [
            { "emittedKey": "epic-1", "kind": "epic", "title": "Billing", "order": 0,
              "deliverableId": "{{DeliverableId}}" },
            { "emittedKey": "story-1", "parentEmittedKey": "epic-1", "kind": "userStory",
              "title": "As a customer I can pay an invoice", "order": 0,
              "requirementId": "{{RequirementId}}", "canCompleteInOneAgentSession": true }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Equal(2, payload!.Nodes.Count);
        Assert.Equal(WbsNodeKind.Epic, payload.Nodes[0].Kind);
        Assert.Equal(WbsNodeKind.UserStory, payload.Nodes[1].Kind);
        Assert.Equal(Guid.Parse(DeliverableId), payload.Nodes[0].DeliverableId);
    }

    [Fact]
    public void An_absent_kind_is_rejected_rather_than_read_as_Epic()
    {
        // The defect this prevents: kind is an enum whose zero value is Epic, so a plain
        // deserialize turns "the client said nothing" into "the client said Epic". The node is then
        // refused for carrying a parent an Epic may not have — a correct refusal naming the wrong
        // field, which is what the client gets told to fix.
        var json = """
        {
          "nodes": [
            { "emittedKey": "epic-1", "kind": "epic", "title": "Billing", "order": 0 },
            { "emittedKey": "orphan", "parentEmittedKey": "epic-1", "title": "No kind", "order": 0 }
          ]
        }
        """;

        Assert.False(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _));
        Assert.Null(payload);
        Assert.Contains(errors, e => e.Contains("requires kind", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("an absent kind is not read as Epic", StringComparison.Ordinal));
    }

    [Fact]
    public void An_explicitly_null_kind_is_rejected_too()
    {
        var json = """
        { "nodes": [ { "emittedKey": "n1", "kind": null, "title": "T", "order": 0 } ] }
        """;

        Assert.False(WbsStructurePayloadReader.TryRead(json, out _, out var errors, out _));
        Assert.Contains(errors, e => e.Contains("the property was null", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stray_property_is_dropped_rather_than_failing_the_node()
    {
        var json = """
        {
          "nodes": [
            { "emittedKey": "epic-1", "kind": "epic", "title": "Billing", "order": 0,
              "estimatedHours": 12, "notes": "anything" }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Equal("epic-1", Assert.Single(payload!.Nodes).EmittedKey);
    }

    [Fact]
    public void An_unparseable_requirementId_is_coerced_to_null_with_a_warning()
    {
        // Lenient by decision: an unparseable requirement link is coerced with a warning rather
        // than aborting the run. The warning is the point — the reference the client emitted and
        // this package did not keep is reported, not swallowed.
        var json = """
        {
          "nodes": [
            { "emittedKey": "story-1", "kind": "userStory", "title": "T", "order": 0,
              "requirementId": "R-014" }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out var warnings),
            string.Join("; ", errors));
        Assert.Null(Assert.Single(payload!.Nodes).RequirementId);
        Assert.Contains(warnings, w => w.Contains("R-014", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unparseable_deliverableId_fails_rather_than_becoming_a_cross_cutting_epic()
    {
        // Strict, unlike requirementId: a deliverable reference that was present must resolve.
        // Coercing it to null would turn an emitted reference into a legal-looking absence.
        var json = """
        {
          "nodes": [
            { "emittedKey": "epic-1", "kind": "epic", "title": "Billing", "order": 0,
              "deliverableId": "not-a-guid" }
          ]
        }
        """;

        Assert.False(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _));
        Assert.Null(payload);
        Assert.Contains(errors, e => e.Contains("could not be read as a GUID", StringComparison.Ordinal));
    }

    [Fact]
    public void An_omitted_deliverableId_is_a_legal_cross_cutting_epic()
    {
        var json = """
        { "nodes": [ { "emittedKey": "epic-1", "kind": "epic", "title": "Cross-cutting", "order": 0 } ] }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Null(Assert.Single(payload!.Nodes).DeliverableId);
    }

    [Fact]
    public void A_non_uuid_acceptanceCriterionId_is_reported_not_silently_dropped()
    {
        var json = """
        {
          "nodes": [
            { "emittedKey": "story-1", "kind": "userStory", "title": "T", "order": 0,
              "acceptanceCriterionIds": ["criterion-1"] }
          ]
        }
        """;

        Assert.False(WbsStructurePayloadReader.TryRead(json, out _, out var errors, out _));
        Assert.Contains(errors, e => e.Contains("criterion-1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_task_requirementId_is_stripped_so_it_cannot_cost_its_parent_coverage()
    {
        var json = $$"""
        {
          "nodes": [
            { "emittedKey": "task-1", "kind": "task", "title": "T", "order": 0,
              "requirementId": "{{RequirementId}}" }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Null(Assert.Single(payload!.Nodes).RequirementId);
    }

    [Fact]
    public void Story_only_fields_are_stripped_from_non_stories()
    {
        var json = $$"""
        {
          "nodes": [
            { "emittedKey": "task-1", "kind": "task", "title": "T", "order": 0,
              "canCompleteInOneAgentSession": true,
              "acceptanceCriteria": ["done"],
              "acceptanceCriterionIds": ["{{CriterionId}}"] }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        var node = Assert.Single(payload!.Nodes);
        Assert.Null(node.CanCompleteInOneAgentSession);
        Assert.Null(node.AcceptanceCriteria);
        Assert.Null(node.AcceptanceCriterionIds);
    }

    [Fact]
    public void Kind_is_read_case_insensitively()
    {
        var json = """
        {
          "nodes": [
            { "emittedKey": "e", "kind": "Epic", "title": "T", "order": 0 },
            { "emittedKey": "s", "parentEmittedKey": "e", "kind": "USERSTORY", "title": "T", "order": 1 }
          ]
        }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Equal(WbsNodeKind.Epic, payload!.Nodes[0].Kind);
        Assert.Equal(WbsNodeKind.UserStory, payload.Nodes[1].Kind);
    }

    [Fact]
    public void An_order_delivered_as_a_string_is_read()
    {
        var json = """
        { "nodes": [ { "emittedKey": "e", "kind": "epic", "title": "T", "order": "3" } ] }
        """;

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _),
            string.Join("; ", errors));
        Assert.Equal(3, Assert.Single(payload!.Nodes).Order);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_is_an_error_not_an_empty_payload(string json)
    {
        Assert.False(WbsStructurePayloadReader.TryRead(json, out var payload, out var errors, out _));
        Assert.Null(payload);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Malformed_json_reports_the_parse_failure()
    {
        Assert.False(WbsStructurePayloadReader.TryRead("{ \"nodes\": [", out _, out var errors, out _));
        Assert.Contains(errors, e => e.Contains("parse failed", StringComparison.Ordinal));
    }

    [Fact]
    public void A_written_payload_reads_back_unchanged()
    {
        // The writer and reader are a pair. If they drift, a stored proposal means one thing on
        // write and another on read, and nothing in either path would report it.
        var original = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-1", null, WbsNodeKind.Epic, Guid.Parse(DeliverableId), null,
                "Billing", "The billing epic", 0),
            new WbsNodePayload("story-1", "epic-1", WbsNodeKind.UserStory, null,
                Guid.Parse(RequirementId), "As a customer I can pay", null, 1,
                CanCompleteInOneAgentSession: true),
        ]);

        var json = WbsStructurePayloadWriter.Write(original);

        Assert.True(WbsStructurePayloadReader.TryRead(json, out var round, out var errors, out _),
            string.Join("; ", errors));
        Assert.Equal(original.Nodes.Count, round!.Nodes.Count);
        for (var i = 0; i < original.Nodes.Count; i++)
        {
            Assert.Equal(original.Nodes[i].EmittedKey, round.Nodes[i].EmittedKey);
            Assert.Equal(original.Nodes[i].ParentEmittedKey, round.Nodes[i].ParentEmittedKey);
            Assert.Equal(original.Nodes[i].Kind, round.Nodes[i].Kind);
            Assert.Equal(original.Nodes[i].DeliverableId, round.Nodes[i].DeliverableId);
            Assert.Equal(original.Nodes[i].RequirementId, round.Nodes[i].RequirementId);
            Assert.Equal(original.Nodes[i].Title, round.Nodes[i].Title);
            Assert.Equal(original.Nodes[i].Description, round.Nodes[i].Description);
            Assert.Equal(original.Nodes[i].Order, round.Nodes[i].Order);
            Assert.Equal(
                original.Nodes[i].CanCompleteInOneAgentSession,
                round.Nodes[i].CanCompleteInOneAgentSession);
        }
    }
}
