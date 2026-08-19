using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkItemManagement.Core;

/// <summary>
/// Deserialises concrete <see cref="WorkItem"/> subtypes from the existing
/// <c>type</c> enum field (docs/22 Part 1) without conflicting with
/// <see cref="JsonPolymorphicAttribute"/> metadata property names.
/// </summary>
public sealed class WorkItemJsonConverter : JsonConverter<WorkItem>
{
    public override WorkItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var type = ReadType(root);
        var raw = root.GetRawText();

        return type switch
        {
            WorkItemType.Epic => JsonSerializer.Deserialize<Epic>(raw, options),
            WorkItemType.Feature => JsonSerializer.Deserialize<Feature>(raw, options),
            WorkItemType.UserStory => JsonSerializer.Deserialize<UserStory>(raw, options),
            WorkItemType.Task => JsonSerializer.Deserialize<WorkItemManagement.Core.Task>(raw, options),
            WorkItemType.Bug => JsonSerializer.Deserialize<Bug>(raw, options),
            _ => throw new JsonException($"Unsupported work item type '{type}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkItem value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);

    private static WorkItemType ReadType(JsonElement root)
    {
        if (!TryGetProperty(root, nameof(WorkItem.Type), out var property))
        {
            throw new JsonException("Work item JSON must include a 'type' property.");
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numeric))
        {
            return (WorkItemType)numeric;
        }

        if (property.ValueKind == JsonValueKind.String
            && Enum.TryParse<WorkItemType>(property.GetString(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new JsonException("Work item type is invalid.");
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
