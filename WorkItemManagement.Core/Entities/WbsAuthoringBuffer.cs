using DomainServices.Core.Models;
using DomainServices.Core.Validation;

namespace WorkItemManagement.Core.Entities;

/// <summary>
/// A proposed work-breakdown structure held for a project while it is being authored, before any
/// of it becomes executable work.
/// </summary>
/// <remarks>
/// <para>One row per project: authoring replaces the proposal rather than versioning it. The
/// breakdown under review is whatever was last written, and a client that reads the buffer back is
/// reading its own most recent submission.</para>
/// <para>The payload is stored as its JSON text rather than as mapped node rows. That is
/// deliberate: a buffer holds what a client proposed, including a proposal that would not survive
/// validation, and shredding it into typed rows would force it to be well-formed before it can be
/// stored — which is exactly the state an author needs to be able to save and come back to. The
/// node hierarchy becomes rows when the breakdown is approved, and that hierarchy belongs to the
/// consumer that owns it, not to this buffer.</para>
/// <para><see cref="CoreDomainModel.EnterpriseId"/> and <see cref="CoreDomainModel.IsDeleted"/>
/// behave exactly as they do on <see cref="WorkItemManagement.Core.WorkItem"/>, so a consumer must
/// register the same tenant and soft-delete query filters. A divergence there returns wrong rows
/// silently rather than failing.</para>
/// </remarks>
public class WbsAuthoringBuffer : CoreDomainModel
{
    public const int MaxAuthoredByLength = 256;

    public Guid ProjectId { get; set; }

    /// <summary>
    /// The proposed structure, as the JSON body the author submitted.
    /// </summary>
    /// <remarks>
    /// Read it back through <see cref="Planning.WbsStructurePayloadReader.TryRead"/>. Deserializing
    /// it directly skips the normalizations that make a payload safe to evaluate — see that type
    /// for what each one prevents.
    /// </remarks>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// The node count of the stored payload, so a caller can size a proposal without reading and
    /// parsing the whole body.
    /// </summary>
    public int NodeCount { get; set; }

    public string? AuthoredBy { get; set; }

    public DateTimeOffset AuthoredAt { get; set; }

    public WbsAuthoringBuffer()
    {
        Id = Guid.NewGuid();
    }

    public WbsAuthoringBuffer(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
    }

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(ProjectId != Guid.Empty, nameof(ProjectId), "Project id is required.");
        validator.Require(
            !string.IsNullOrWhiteSpace(PayloadJson),
            nameof(PayloadJson),
            "Payload JSON is required.");
        validator.Require(NodeCount >= 0, nameof(NodeCount), "Node count cannot be negative.");
        validator.Require(
            AuthoredBy is null || AuthoredBy.Length <= MaxAuthoredByLength,
            nameof(AuthoredBy),
            $"Authored by must be {MaxAuthoredByLength} characters or fewer.");
        validator.Require(
            AuthoredAt != default,
            nameof(AuthoredAt),
            "Authored at is required.");
    }
}
