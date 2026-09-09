using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Writes a payload as the JSON body <see cref="WbsStructurePayloadReader.TryRead"/> reads back.
/// </summary>
/// <remarks>
/// The two exist as a pair so that storing a proposal and reading it back cannot drift. The write
/// options match the read options where it matters — camelCase property names and camelCase enum
/// names — which is what makes a stored body legible to a client that never used this package.
/// </remarks>
public static class WbsStructurePayloadWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Serializes <paramref name="payload"/> to its JSON body.
    /// </summary>
    public static string Write(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload, WriteOptions);
    }
}
