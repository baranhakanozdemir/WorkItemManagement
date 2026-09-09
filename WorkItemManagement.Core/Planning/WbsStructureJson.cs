using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Linq;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Shared JSON helpers for WBS structured-output payloads (#2275).
/// </summary>
/// <remarks>
/// Trimmed on extraction (WorkItemManagement#6) to the reading half. The schema-property
/// members that stayed behind describe the Anthropic tool schema and read their bounds off
/// plusteam entity constants; they author a request rather than read a response, and the
/// bounds belong to a table this package does not own.
/// </remarks>
public static class WbsStructureJson
{

    public static JsonSerializerOptions CreateToolReadOptions(IList<string>? guidParseWarnings = null)
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                guidParseWarnings is null
                    ? new LenientNullableGuidJsonConverter()
                    : new LenientNullableGuidJsonConverter(guidParseWarnings),
            },
        };
    }

    /// <summary>
    /// Replaces invalid linkage-field values with JSON null and records path-qualified warnings
    /// before strict deserialization runs.
    /// </summary>
    private static readonly HashSet<string> KnownNodeProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "emittedKey",
        "parentEmittedKey",
        "kind",
        "deliverableId",
        "requirementId",
        "acceptanceCriterionIds",
        "acceptanceCriteria",
        "canCompleteInOneAgentSession",
        "dependsOn",
        "title",
        "description",
        "order",
    };

    /// <summary>
    /// #3186: drops properties the WBS node schema does not declare. A stray field must not
    /// fail deserialization of an otherwise valid node.
    /// </summary>
    public static void StripUnknownNodeProperties(JsonNode? input)
    {
        if (input?["nodes"] is not JsonArray nodes)
        {
            return;
        }

        foreach (var item in nodes)
        {
            if (item is not JsonObject node)
            {
                continue;
            }

            var unknown = node
                .Select(property => property.Key)
                .Where(key => !KnownNodeProperties.Contains(key))
                .ToArray();
            foreach (var key in unknown)
            {
                node.Remove(key);
            }
        }
    }

    /// <summary>
    /// #3186: drops fields the node's <c>kind</c> may not carry, before typed validation or
    /// deserialization. A Task with a string <c>acceptanceCriterionIds</c> or a non-boolean
    /// <c>canCompleteInOneAgentSession</c> must not fail the whole branch.
    /// </summary>
    public static void StripKindForbiddenNodeProperties(JsonNode? input)
    {
        if (input?["nodes"] is not JsonArray nodes)
        {
            return;
        }

        foreach (var item in nodes)
        {
            if (item is not JsonObject node)
            {
                continue;
            }

            if (!TryFindProperty(node, "kind", out var kindNode)
                || kindNode?.GetValueKind() != JsonValueKind.String)
            {
                continue;
            }

            var kind = kindNode.GetValue<string>();
            var isUserStory = string.Equals(kind, "UserStory", StringComparison.OrdinalIgnoreCase);
            var isTask = string.Equals(kind, "Task", StringComparison.OrdinalIgnoreCase);

            if (!isUserStory)
            {
                RemovePropertyIgnoreCase(node, "canCompleteInOneAgentSession");
                RemovePropertyIgnoreCase(node, "acceptanceCriteria");
                RemovePropertyIgnoreCase(node, "acceptanceCriterionIds");
            }

            if (isTask)
            {
                RemovePropertyIgnoreCase(node, "requirementId");
            }
        }
    }

    public static void SanitizeOptionalGuidFields(JsonNode? input, IList<string> warnings)
    {
        if (input?["nodes"] is not JsonArray nodes)
        {
            return;
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index] is not JsonObject node)
            {
                continue;
            }

            SanitizeGuidProperty(node, "deliverableId", $"nodes[{index}].deliverableId", warnings);
            SanitizeGuidProperty(node, "requirementId", $"nodes[{index}].requirementId", warnings);
        }
    }

    /// <summary>
    /// #2374: reports every node whose <c>kind</c> is absent or null, before deserialization turns
    /// absence into <see cref="Models.WbsNodes.WbsNodeKind.Epic"/> — the enum's zero value, which
    /// makes "the model said Epic" indistinguishable from "the model said nothing".
    /// <para>
    /// The check lives at the JSON boundary because that is the last point where absence is still
    /// observable. Downstream, a parented node that omitted <c>kind</c> is rejected for carrying a
    /// <c>parentEmittedKey</c> an Epic may not have — a correct refusal naming the wrong field, and
    /// that wrong field is what the repair loop feeds back to the model on the retry.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> when every node carries a kind, so callers can fail on the field the
    /// model actually omitted rather than on a consequence of the default.</returns>
    public static bool TryValidateNodeKindsPresent(JsonNode? input, out IReadOnlyList<string> errors)
    {
        var missing = new List<string>();

        if (input?["nodes"] is JsonArray nodes)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] is not JsonObject node)
                {
                    continue;
                }

                // Deserialization is case-insensitive, so any spelling of the property counts as
                // present.
                var present = TryFindProperty(node, "kind", out var kind);
                if (present && kind is not null)
                {
                    continue;
                }

                var absence = present ? "null" : "omitted";
                missing.Add(
                    $"Node {DescribeNode(node, index)} requires kind: the property was {absence}. "
                    + "Emit one of Epic, Feature, UserStory, Task — an absent kind is not read as Epic.");
            }
        }

        errors = missing;
        return missing.Count == 0;
    }

    /// <summary>
    /// #2405: reports every <c>acceptanceCriterionIds</c> entry that is not a UUID, before strict
    /// deserialization turns it into a <c>JsonException</c>.
    ///
    /// <para>Deliberately not routed through <see cref="SanitizeOptionalGuidFields"/>, which coerces
    /// an unparseable scalar to null with a warning. That leniency (#2277) is right for a scalar
    /// linkage field, where null is a legal value the model might have meant. It is wrong here:
    /// #2405 says a criterion reference that is present must resolve, and silently dropping a bad
    /// entry from the list is precisely how #2361 turned an emitted reference into a legal-looking
    /// absence. Rejecting with the offending value named lets the repair loop feed the model
    /// something it can act on, which coercion cannot.</para>
    /// </summary>
    /// <returns><c>true</c> when every entry is a UUID.</returns>
    public static bool TryValidateAcceptanceCriterionIds(JsonNode? input, out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();

        if (input?["nodes"] is JsonArray nodes)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] is not JsonObject node)
                {
                    continue;
                }

                if (!TryFindProperty(node, "acceptanceCriterionIds", out var value) || value is null)
                {
                    // Absent or explicitly null: legal, and means the node satisfies no criterion.
                    continue;
                }

                if (value is not JsonArray entries)
                {
                    problems.Add(
                        $"Node {DescribeNode(node, index)} has acceptanceCriterionIds that is not an array. "
                        + "Emit an array of exact criterion GUIDs, or omit the property.");
                    continue;
                }

                for (var entry = 0; entry < entries.Count; entry++)
                {
                    var candidate = entries[entry];
                    var text = candidate?.GetValueKind() == JsonValueKind.String
                        ? candidate.GetValue<string>()
                        : candidate?.ToJsonString();

                    if (candidate?.GetValueKind() == JsonValueKind.String
                        && Guid.TryParseExact(text, "D", out _))
                    {
                        continue;
                    }

                    problems.Add(
                        $"Node {DescribeNode(node, index)} has acceptanceCriterionIds[{entry}] = "
                        + $"'{text ?? "null"}', which is not a criterion GUID. Copy an exact GUID from the "
                        + "criterion catalog, or omit the property — a list position, a number and a "
                        + "human-readable identifier are all rejected rather than interpreted.");
                }
            }
        }

        errors = problems;
        return problems.Count == 0;
    }

    private static string DescribeNode(JsonObject node, int index) =>
        TryFindProperty(node, "emittedKey", out var emittedKey)
        && emittedKey?.GetValueKind() == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(emittedKey.GetValue<string>())
            ? $"'{emittedKey.GetValue<string>()}'"
            : $"at nodes[{index}]";

    private static void RemovePropertyIgnoreCase(JsonObject node, string propertyName)
    {
        string? match = null;
        foreach (var property in node)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                match = property.Key;
                break;
            }
        }

        if (match is not null)
        {
            node.Remove(match);
        }
    }

    private static bool TryFindProperty(JsonObject node, string propertyName, out JsonNode? value)
    {
        foreach (var property in node)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value?.GetValueKind() == JsonValueKind.Null ? null : property.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static void SanitizeGuidProperty(
        JsonObject node,
        string propertyName,
        string path,
        IList<string> warnings)
    {
        if (!node.TryGetPropertyValue(propertyName, out var valueNode) || valueNode is null)
        {
            return;
        }

        if (valueNode.GetValueKind() == JsonValueKind.Null)
        {
            return;
        }

        if (valueNode.GetValueKind() == JsonValueKind.String)
        {
            var value = valueNode.GetValue<string>();
            if (string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _))
            {
                return;
            }

            node[propertyName] = null;
            warnings.Add(
                $"Tool input JSON deserialization failed: The JSON value could not be converted to System.Guid. "
                + $"Path: $.{path} | Value: '{value}'. Treated as null; use a valid GUID or null.");
            return;
        }

        node[propertyName] = null;
        warnings.Add(
            $"Tool input JSON deserialization failed: The JSON value could not be converted to System.Guid. "
            + $"Path: $.{path} | Unexpected {valueNode.GetValueKind()} token. Treated as null.");
    }
}
