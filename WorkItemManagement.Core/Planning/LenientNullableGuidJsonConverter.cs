using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Parses nullable GUID fields from model JSON. Unparseable strings become <see langword="null"/>
/// and are recorded as warnings instead of aborting deserialization (#2275).
/// </summary>
public sealed class LenientNullableGuidJsonConverter : JsonConverter<Guid?>
{
    private readonly IList<string>? _warnings;

    public LenientNullableGuidJsonConverter()
    {
    }

    public LenientNullableGuidJsonConverter(IList<string> warnings)
    {
        _warnings = warnings;
    }

    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                {
                    var value = reader.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return null;
                    }

                    if (Guid.TryParse(value, out var guid))
                    {
                        return guid;
                    }

                    _warnings?.Add($"Unparseable GUID value '{value}'; treated as null.");
                    return null;
                }
            default:
                if (_warnings is null)
                {
                    throw new JsonException(
                        $"Unexpected token {reader.TokenType} when parsing a nullable GUID.");
                }

                _warnings.Add(
                    $"Unexpected {reader.TokenType} token when parsing a nullable GUID; treated as null.");
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
