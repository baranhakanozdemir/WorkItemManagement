using System.Text.Json.Serialization;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Typed projection of a work-breakdown structure a client proposed (#1505). This is the proposal
/// shape, not the persisted node hierarchy that exists once a breakdown is approved.
/// </summary>
/// <remarks>
/// Read one of these from JSON through <see cref="WbsStructurePayloadReader.TryRead"/>, never by
/// deserializing this record directly — see that type for the seven defects the direct path
/// inherits.
/// </remarks>
/// <param name="Nodes">The proposed nodes, in no required order.</param>
public sealed record WbsStructurePayload(IReadOnlyList<WbsNodePayload> Nodes);

/// <summary>
/// One node in a <see cref="WbsStructurePayload"/>. Parent linkage uses
/// <see cref="WbsNodePayload.EmittedKey"/> handles because the author does not know database ids.
/// </summary>
/// <param name="EmittedKey">The author's handle for this node, unique within the payload.</param>
/// <param name="ParentEmittedKey">
/// The handle of this node's parent, or <c>null</c> for a root. An Epic may not carry one.
/// </param>
/// <param name="Kind">
/// What the node is. Never leave this to the default: the enum's zero value is
/// <see cref="WbsNodeKind.Epic"/>, so an omitted kind read by a plain deserializer becomes a
/// deliberate Epic. <see cref="WbsStructurePayloadReader"/> rejects an omitted kind for exactly
/// that reason.
/// </param>
/// <param name="DeliverableId">
/// The deliverable this node is scoped to. Omitted means cross-cutting, which is legal. A value
/// that is present but unreadable is an error, not a cross-cutting node.
/// </param>
/// <param name="RequirementId">
/// The requirement this node covers. Stripped on <see cref="WbsNodeKind.Task"/> nodes: a task
/// carrying one would cost its parent story the coverage credit.
/// </param>
/// <param name="Title">The node's title.</param>
/// <param name="Description">Optional longer text.</param>
/// <param name="Order">Sibling ordering. Never an ordering constraint — see <paramref name="DependsOn"/>.</param>
/// <param name="AcceptanceCriterionIds">
/// #2405: the captured success criteria this node satisfies. A list rather than a scalar, unlike
/// <paramref name="RequirementId"/>, because one story can satisfy several criteria and one
/// criterion can need several stories — that many-to-many is the whole reason coverage is checkable
/// while attribution is not.
/// <para>Null and empty both mean "this node satisfies no criterion", which is legal: scaffolding
/// and spikes satisfy none. What is not legal is naming one that does not resolve.</para>
/// </param>
/// <param name="AcceptanceCriteria">
/// #2461: the story's own done-conditions, <em>authored</em> here rather than selected from a
/// catalogue. This is the field <paramref name="AcceptanceCriterionIds"/> is not: that one
/// distributes project success criteria the customer already stated, which answer "did the project
/// achieve its outcome"; this one answers "is this story finished", which nothing in the platform
/// previously wrote at all.
/// <para>Required and non-empty on <see cref="WbsNodeKind.UserStory"/> nodes and rejected on every
/// other kind. A story is the level at which "done" is defined for a customer, so a story without
/// one has no testable target and the implementing agent decides for itself when it is finished.
/// Epics and tasks are not that level: an epic is an outcome, and a task is a step inside a story
/// whose condition the story already carries.</para>
/// </param>
/// <param name="CanCompleteInOneAgentSession">
/// #2462: explicit judgement that this executable item can be finished by one agent in one
/// execution session. Required on generated UserStory and Task nodes; omitted on Epics and
/// Features. A false value means the node must be split before approval.
/// </param>
/// <param name="DependsOn">
/// #2544: work this node cannot start until the named nodes finish. Only UserStory and Task may
/// carry it. Empty or omitted means the node is independent of its siblings — never infer an edge
/// from <paramref name="Order"/>.
/// </param>
public sealed record WbsNodePayload(
    [property: JsonPropertyName("emittedKey")] string EmittedKey,
    [property: JsonPropertyName("parentEmittedKey")] string? ParentEmittedKey,
    [property: JsonPropertyName("kind")] WbsNodeKind Kind,
    [property: JsonPropertyName("deliverableId")] Guid? DeliverableId,
    [property: JsonPropertyName("requirementId")] Guid? RequirementId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("acceptanceCriterionIds")] IReadOnlyList<Guid>? AcceptanceCriterionIds = null,
    [property: JsonPropertyName("acceptanceCriteria")] IReadOnlyList<string>? AcceptanceCriteria = null,
    [property: JsonPropertyName("canCompleteInOneAgentSession")] bool? CanCompleteInOneAgentSession = null,
    [property: JsonPropertyName("dependsOn")] IReadOnlyList<WbsNodeDependencyPayload>? DependsOn = null);

/// <summary>
/// One stated predecessor of a <see cref="WbsNodePayload"/>.
/// </summary>
/// <param name="EmittedKey">The handle of the blocking node.</param>
/// <param name="Reason">Why the work cannot start without it.</param>
public sealed record WbsNodeDependencyPayload(
    [property: JsonPropertyName("emittedKey")] string EmittedKey,
    [property: JsonPropertyName("reason")] string Reason);
