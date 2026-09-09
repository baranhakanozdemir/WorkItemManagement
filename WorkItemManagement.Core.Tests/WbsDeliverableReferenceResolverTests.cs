using WorkItemManagement.Core.Planning;
using System.Globalization;

namespace WorkItemManagement.Core.Tests;

public sealed class WbsDeliverableReferenceResolverTests
{
    [Fact]
    public void Resolve_maps_one_based_index_guids_to_project_deliverables()
    {
        var first = new WbsTraceabilityDeliverable(Guid.NewGuid(), "Backend Service");
        var second = new WbsTraceabilityDeliverable(Guid.NewGuid(), "Desktop Agent");
        var third = new WbsTraceabilityDeliverable(Guid.NewGuid(), "Mobile Application");
        var payload = new WbsStructurePayload(
        [
            Epic("epic-backend", IndexGuid(1)),
            Epic("epic-desktop", IndexGuid(2)),
            Epic("epic-mobile", IndexGuid(3)),
        ]);

        var result = WbsDeliverableReferenceResolver.Resolve(payload, [first, second, third]);

        Assert.Empty(result.Errors);
        Assert.Equal(first.Id, result.Payload.Nodes[0].DeliverableId);
        Assert.Equal(second.Id, result.Payload.Nodes[1].DeliverableId);
        Assert.Equal(third.Id, result.Payload.Nodes[2].DeliverableId);
    }

    [Fact]
    public void Resolve_rejects_unresolved_deliverable_reference()
    {
        var stated = new WbsTraceabilityDeliverable(Guid.NewGuid(), "Backend Service");
        var payload = new WbsStructurePayload([Epic("epic-missing", IndexGuid(3))]);

        var result = WbsDeliverableReferenceResolver.Resolve(payload, [stated]);

        var error = Assert.Single(result.Errors);
        Assert.Contains("epic-missing", error, StringComparison.Ordinal);
        Assert.Contains("unresolved deliverableId", error, StringComparison.Ordinal);
        Assert.Contains(stated.Id.ToString("D"), error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_treats_index_guid_suffix_as_decimal()
    {
        var deliverables = Enumerable.Range(1, 16)
            .Select(index => new WbsTraceabilityDeliverable(Guid.NewGuid(), $"Deliverable {index}"))
            .ToArray();
        var payload = new WbsStructurePayload([Epic("epic-tenth", IndexGuid(10))]);

        var result = WbsDeliverableReferenceResolver.Resolve(payload, deliverables);

        Assert.Empty(result.Errors);
        Assert.Equal(deliverables[9].Id, result.Payload.Nodes[0].DeliverableId);
    }

    [Fact]
    public void Resolve_leaves_project_deliverable_guids_unchanged()
    {
        var deliverable = new WbsTraceabilityDeliverable(Guid.NewGuid(), "Backend Service");
        var payload = new WbsStructurePayload([Epic("epic-backend", deliverable.Id)]);

        var result = WbsDeliverableReferenceResolver.Resolve(payload, [deliverable]);

        Assert.Empty(result.Errors);
        Assert.Same(payload, result.Payload);
    }

    private static WbsNodePayload Epic(string key, Guid deliverableId) =>
        new(key, null, WbsNodeKind.Epic, deliverableId, null, key, null, 0);

    private static Guid IndexGuid(int index) =>
        Guid.Parse($"00000000-0000-0000-0000-{index.ToString("d12", CultureInfo.InvariantCulture)}");
}
