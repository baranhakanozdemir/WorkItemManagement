using System.Text.Json.Serialization;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Typed projection of a work-breakdown structure emitted by the Summarizer
/// forced-tool-call round (#1505). This is the deliberation artifact shape —
/// not the persisted <see cref="Models.WbsStructures.WbsStructure"/> entity.
/// </summary>
public sealed record WbsStructurePayload(IReadOnlyList<WbsNodePayload> Nodes);

/// <summary>
/// One node in a <see cref="WbsStructurePayload"/>. Parent linkage uses
/// <see cref="EmittedKey"/> handles because the LLM does not know database ids.
/// </summary>
public sealed record WbsNodePayload(
    [property: JsonPropertyName("emittedKey")] string EmittedKey,
    [property: JsonPropertyName("parentEmittedKey")] string? ParentEmittedKey,
    [property: JsonPropertyName("kind")] WbsNodeKind Kind,
    [property: JsonPropertyName("deliverableId")] Guid? DeliverableId,
    [property: JsonPropertyName("requirementId")] Guid? RequirementId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("order")] int Order,
    /// <summary>
    /// #2405: the captured success criteria this node satisfies. A list rather than a scalar,
    /// unlike <c>RequirementId</c>, because one story can satisfy several criteria and one
    /// criterion can need several stories — that many-to-many is the whole reason coverage is
    /// checkable while attribution is not.
    ///
    /// <para>Null and empty both mean "this node satisfies no criterion", which is legal:
    /// scaffolding and spikes satisfy none. What is not legal is naming one that does not
    /// resolve.</para>
    /// </summary>
    [property: JsonPropertyName("acceptanceCriterionIds")] IReadOnlyList<Guid>? AcceptanceCriterionIds = null,
    /// <summary>
    /// #2461: the story's own done-conditions, <em>authored</em> here rather than selected from a
    /// catalogue. This is the field <c>AcceptanceCriterionIds</c> is not: that one distributes
    /// project success criteria the customer already stated, which answer "did the project achieve
    /// its outcome"; this one answers "is this story finished", which nothing in the platform
    /// previously wrote at all.
    ///
    /// <para>Required and non-empty on <see cref="WbsNodeKind.UserStory"/> nodes and rejected on
    /// every other kind. A story is the level at which "done" is defined for a customer, so a story
    /// without one has no testable target and the implementing agent decides for itself when it is
    /// finished — which is how <c>Deliver {Title}</c> reached execution. Epics and tasks are not
    /// that level: an epic is an outcome and a task is a step inside a story whose condition the
    /// story already carries.</para>
    /// </summary>
    [property: JsonPropertyName("acceptanceCriteria")] IReadOnlyList<string>? AcceptanceCriteria = null,
    /// <summary>
    /// #2462: explicit judgement that this executable item can be finished by one agent in one
    /// execution session. Required on generated UserStory and Task nodes; omitted on Epics and
    /// Features. A false value means the node must be split before approval.
    /// </summary>
    [property: JsonPropertyName("canCompleteInOneAgentSession")] bool? CanCompleteInOneAgentSession = null,
    /// <summary>
    /// #2544: work this node cannot start until the named nodes finish. Only UserStory and Task
    /// may carry it. Empty or omitted means the node is independent of its siblings — never infer
    /// an edge from order.
    /// </summary>
    [property: JsonPropertyName("dependsOn")] IReadOnlyList<WbsNodeDependencyPayload>? DependsOn = null);

/// <summary>
/// One stated predecessor of a <see cref="WbsNodePayload"/>. <see cref="EmittedKey"/> names the
/// blocking node; <see cref="Reason"/> is why the work cannot start without it.
/// </summary>
public sealed record WbsNodeDependencyPayload(
    [property: JsonPropertyName("emittedKey")] string EmittedKey,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>
/// Structured failure metadata when the Summarizer exhausts its retry budget.
/// #1736: carries the raw tool output + stop reason of the last failed attempt so we can
/// diagnose why <c>submit_wbs</c> returns an empty structure (truly-empty array vs. a
/// max_tokens truncation vs. a deserialization mismatch) — the raw model output is otherwise
/// not persisted anywhere on failure.
/// </summary>
public sealed record WbsSummarizerFailure(
    int AttemptCount,
    IReadOnlyList<string> ValidationErrors,
    string? RawToolInput = null,
    string? StopReason = null);
