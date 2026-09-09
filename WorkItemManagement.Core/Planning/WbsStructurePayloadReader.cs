using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Turns a client's JSON body into a <see cref="WbsStructurePayload"/> the readiness evaluator
/// can judge.
/// </summary>
/// <remarks>
/// <para>This type exists because "deserialize into the payload record" is not the contract, and
/// looks like it is. Between the JSON a client sends and a payload that can be evaluated sit five
/// normalizations and two validations, each added after the missing one produced a wrong answer in
/// production (#2277, #2371, #2374, #2405, #3182, #3186):</para>
/// <list type="bullet">
/// <item>Unknown node properties are dropped — a stray field must not fail an otherwise valid
/// node.</item>
/// <item>Unparseable <c>deliverableId</c> / <c>requirementId</c> scalars are coerced to null with a
/// warning rather than aborting the whole payload.</item>
/// <item>An absent or null <c>kind</c> is rejected. This one is the reason the raw deserializer is
/// not safe to expose: <c>kind</c> is an enum whose zero value is <see cref="WbsNodeKind.Epic"/>,
/// so a plain deserialize turns "the client said nothing" into "the client said Epic", and the node
/// is then refused for carrying a parent an Epic may not have — a correct refusal naming the wrong
/// field.</item>
/// <item>Fields the node's kind may not carry are stripped before typed reading.</item>
/// <item>Every <c>acceptanceCriterionIds</c> entry must be a UUID; a bad entry is reported, never
/// silently dropped.</item>
/// <item>A <c>deliverableId</c> that was present but unreadable fails instead of becoming a
/// legal-looking cross-cutting epic.</item>
/// <item>Task <c>requirementId</c> and non-story story-fields are stripped — a stray field must not
/// cost a parent its coverage.</item>
/// </list>
/// <para>A consumer that deserialized the record directly would inherit all seven defects, and each
/// one fails quietly: the payload parses, evaluation runs, and the verdict is wrong. Reading is
/// therefore offered only through this entry point.</para>
/// <para>Structural validation — node title and key lengths — is deliberately <em>not</em> here.
/// Those bounds are the columns of the <c>wbs_nodes</c> table, which this package does not own;
/// restating them here would fork them from the schema that enforces them.</para>
/// </remarks>
public static class WbsStructurePayloadReader
{
    /// <summary>
    /// Reads a client's JSON body into a normalized payload.
    /// </summary>
    /// <param name="json">The JSON object body. A WBS structure object, not a wrapper.</param>
    /// <param name="payload">The normalized payload, or <c>null</c> when reading failed.</param>
    /// <param name="errors">Why the read failed. Empty on success.</param>
    /// <param name="warnings">
    /// Values that were coerced rather than rejected — an unparseable linkage GUID read as null.
    /// A successful read can still carry warnings, and they are worth surfacing: each one is a
    /// reference the client emitted and this package did not keep.
    /// </param>
    /// <returns><c>true</c> when <paramref name="payload"/> is usable.</returns>
    public static bool TryRead(
        string json,
        out WbsStructurePayload? payload,
        out IReadOnlyList<string> errors,
        out IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(json);

        payload = null;
        var parseWarnings = new List<string>();
        warnings = parseWarnings;

        if (string.IsNullOrWhiteSpace(json))
        {
            errors = ["No JSON object was supplied."];
            return false;
        }

        JsonNode? raw;
        try
        {
            raw = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            errors = [$"WBS JSON parse failed: {ex.Message}"];
            return false;
        }

        if (raw is null)
        {
            errors = ["WBS JSON parsed to null."];
            return false;
        }

        // The raw node is kept unsanitized so the dropped-reference guard below has something to
        // compare against. Sanitizing in place destroys that evidence.
        var sanitized = raw.DeepClone();

        try
        {
            WbsStructureJson.StripUnknownNodeProperties(sanitized);
            WbsStructureJson.SanitizeOptionalGuidFields(sanitized, parseWarnings);

            // Absence is only observable here, before the enum default reads it as Epic.
            if (!WbsStructureJson.TryValidateNodeKindsPresent(sanitized, out var kindErrors))
            {
                errors = [.. parseWarnings, .. kindErrors];
                return false;
            }

            WbsStructureJson.StripKindForbiddenNodeProperties(sanitized);

            if (!WbsStructureJson.TryValidateAcceptanceCriterionIds(sanitized, out var criterionErrors))
            {
                errors = [.. parseWarnings, .. criterionErrors];
                return false;
            }

            payload = sanitized.Deserialize<WbsStructurePayload>(
                WbsStructureJson.CreateToolReadOptions(parseWarnings));
        }
        catch (JsonException ex)
        {
            payload = null;
            errors = [$"WBS JSON deserialization failed: {ex.Message}"];
            return false;
        }

        if (payload is null)
        {
            errors = ["WBS JSON deserialized to null."];
            return false;
        }

        // A deliverable reference that was present must resolve. Absent is legal; unparseable must
        // not become a legal-looking cross-cutting Epic.
        var dropped = new List<string>();
        CollectDroppedDeliverableReferences(raw, payload, dropped);
        if (dropped.Count > 0)
        {
            payload = null;
            errors = [.. parseWarnings, .. dropped];
            return false;
        }

        payload = Normalize(payload);
        errors = [];
        return true;
    }

    /// <summary>
    /// Drops fields the node's kind may not carry.
    /// </summary>
    /// <remarks>
    /// Public because a consumer that builds a payload in code rather than reading one from JSON
    /// needs the same normalization before evaluating it. <see cref="TryRead"/> already applies it.
    /// </remarks>
    public static WbsStructurePayload Normalize(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Nodes is null)
        {
            return payload;
        }

        return payload with
        {
            Nodes = [.. payload.Nodes.Select(NormalizeNode)],
        };
    }

    private static WbsNodePayload NormalizeNode(WbsNodePayload node)
    {
        var stripped = node;
        if (node.Kind is WbsNodeKind.Task && node.RequirementId is not null)
        {
            stripped = stripped with { RequirementId = null };
        }

        if (node.Kind is not WbsNodeKind.UserStory)
        {
            if (stripped.CanCompleteInOneAgentSession is not null)
            {
                stripped = stripped with { CanCompleteInOneAgentSession = null };
            }

            if (stripped.AcceptanceCriteria is not null)
            {
                stripped = stripped with { AcceptanceCriteria = null };
            }

            if (stripped.AcceptanceCriterionIds is not null)
            {
                stripped = stripped with { AcceptanceCriterionIds = null };
            }
        }

        return stripped;
    }

    private static void CollectDroppedDeliverableReferences(
        JsonNode input,
        WbsStructurePayload payload,
        IList<string> errors)
    {
        if (input["nodes"] is not JsonArray rawNodes)
        {
            return;
        }

        var count = Math.Min(rawNodes.Count, payload.Nodes.Count);
        for (var index = 0; index < count; index++)
        {
            if (rawNodes[index] is not JsonObject rawNode)
            {
                continue;
            }

            if (!rawNode.TryGetPropertyValue("deliverableId", out var rawValue)
                || rawValue is null
                || rawValue.GetValueKind() == JsonValueKind.Null)
            {
                // Absent or explicitly null: a legal cross-cutting epic.
                continue;
            }

            if (payload.Nodes[index].DeliverableId is not null)
            {
                continue;
            }

            errors.Add(
                $"Epic node '{payload.Nodes[index].EmittedKey}' carried a deliverableId that could not be "
                + $"read as a GUID (nodes[{index}].deliverableId). A deliverable reference that is present "
                + "must resolve — use a deliverable GUID from Context, or omit the field entirely for "
                + "cross-cutting work.");
        }
    }
}
