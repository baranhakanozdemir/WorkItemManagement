using System.Globalization;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Resolves model-emitted WBS deliverable references against the project deliverable catalog.
/// </summary>
public static class WbsDeliverableReferenceResolver
{
    public static WbsDeliverableReferenceResolution Resolve(
        WbsStructurePayload payload,
        IReadOnlyList<WbsTraceabilityDeliverable> deliverables)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(deliverables);

        var deliverablesById = deliverables.ToDictionary(deliverable => deliverable.Id);
        var errors = new List<string>();
        List<WbsNodePayload>? rewritten = null;

        for (var index = 0; index < payload.Nodes.Count; index++)
        {
            var node = payload.Nodes[index];
            if (node.Kind is not WbsNodeKind.Epic || node.DeliverableId is null)
            {
                rewritten?.Add(node);
                continue;
            }

            var deliverableId = node.DeliverableId.Value;
            if (deliverablesById.ContainsKey(deliverableId))
            {
                rewritten?.Add(node);
                continue;
            }

            if (TryResolvePositionalIndex(deliverableId, deliverables, out var resolvedDeliverable))
            {
                rewritten ??= payload.Nodes.Take(index).ToList();
                rewritten.Add(node with { DeliverableId = resolvedDeliverable.Id });
                continue;
            }

            errors.Add(
                $"Epic '{node.EmittedKey}' references unresolved deliverableId {deliverableId}. "
                + "Use one of the project deliverable GUIDs, not a positional index: "
                + FormatDeliverableCatalog(deliverables));
            rewritten?.Add(node);
        }

        return new WbsDeliverableReferenceResolution(
            errors.Count == 0 && rewritten is not null
                ? new WbsStructurePayload(rewritten)
                : payload,
            errors);
    }

    internal static bool TryResolvePositionalIndex(
        Guid deliverableId,
        IReadOnlyList<WbsTraceabilityDeliverable> deliverables,
        out WbsTraceabilityDeliverable deliverable)
    {
        deliverable = default!;

        var value = deliverableId.ToString("N");
        if (!value.StartsWith("00000000000000000000", StringComparison.Ordinal))
        {
            return false;
        }

        var indexText = value[20..];
        if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var oneBasedIndex)
            || oneBasedIndex <= 0
            || oneBasedIndex > deliverables.Count)
        {
            return false;
        }

        deliverable = deliverables[oneBasedIndex - 1];
        return true;
    }

    private static string FormatDeliverableCatalog(IReadOnlyList<WbsTraceabilityDeliverable> deliverables)
    {
        if (deliverables.Count == 0)
        {
            return "(none captured)";
        }

        return string.Join(
            "; ",
            deliverables.Select((deliverable, index) =>
                $"{index + 1}. '{deliverable.Name}' ({deliverable.Id:D})"));
    }
}

public sealed record WbsDeliverableReferenceResolution(
    WbsStructurePayload Payload,
    IReadOnlyList<string> Errors);
