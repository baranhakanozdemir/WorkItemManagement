
namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Project-scoped inputs for deterministic WBS traceability checks (#2288).
/// </summary>
public sealed record WbsTraceabilityContext(
    IReadOnlyList<WbsTraceabilityObjective> Objectives,
    IReadOnlyList<WbsTraceabilityDeliverable> Deliverables)
{
    /// <summary>
    /// The project's functional requirements (#2400). An init property rather than a third
    /// positional parameter so a caller that has not been taught to load requirements yet
    /// keeps compiling and simply reports no requirement coverage — the alternative was every
    /// existing construction site silently passing an empty list to satisfy the signature,
    /// which reads identically to "this project has no requirements" and hides which is which.
    /// </summary>
    public IReadOnlyList<WbsTraceabilityRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// The project's captured success criteria (#2405), for the same reason and by the same
    /// mechanism as <see cref="Requirements"/>: an init property so a caller not yet taught to load
    /// them keeps compiling and reports no criterion coverage, rather than every construction site
    /// passing an empty list to satisfy a signature — which reads identically to "this project
    /// captured no criteria" and hides which is which.
    /// </summary>
    public IReadOnlyList<WbsTraceabilityAcceptanceCriterion> AcceptanceCriteria { get; init; } = [];

    /// <summary>
    /// The problems the customer said they have (#2612), by the same init-property mechanism and
    /// for the same reason as <see cref="Requirements"/>.
    /// </summary>
    /// <remarks>
    /// Used only by <c>WbsScopeFidelityEvaluator</c>, and it is the one part of that check the
    /// other catalogues cannot stand in for. <see cref="Requirements"/> carries clarification
    /// residue — <c>ClarificationResponseWriter</c> writes <c>CL-###</c> rows from
    /// platform-authored questions and assumptions — so a capability the platform invented during
    /// clarification can appear there and then vouch for itself. The problem statement is what the
    /// customer said before any of that, so it is the one catalogue the platform cannot have
    /// authored — which is what makes it usable as the check on the others.
    /// <para>(The sentence above was truncated in the source this type was extracted from, with no
    /// closing tag; it is completed here from the argument the paragraph already makes.)</para>
    /// </remarks>
    public IReadOnlyList<WbsTraceabilityProblem> Problems { get; init; } = [];

    /// <summary>
    /// The approved domain entities pre-generated for this delivery project (#2888, #2890).
    /// </summary>
    public IReadOnlyList<string> PreGeneratedEntities { get; init; } = [];
}

/// <summary>
/// One captured problem, offered to the scope-fidelity check as customer-stated vocabulary
/// (#2612). Both fields <c>ProblemDefinition</c> requires: the problem as stated, and who it
/// hurts — customers name the domain in either one, and often only in the second.
/// </summary>
public sealed record WbsTraceabilityProblem(
    string RequirementNumber,
    string Statement,
    string? Impact);

public sealed record WbsTraceabilityObjective(
    string RequirementNumber,
    string Title,
    int Priority);

public sealed record WbsTraceabilityDeliverable(
    Guid Id,
    string Name);

/// <summary>
/// One functional requirement offered to the work-breakdown stage as a referencable identity
/// (#2400). <see cref="RequirementNumber"/> and <see cref="Description"/> exist so an uncovered
/// requirement can be reported the way the customer wrote it rather than as a bare GUID —
/// "by number and description", which is what the issue asks the report to say.
///
/// <para>Description, not Title: <c>Requirement</c> carries no Title. Objectives do, which is
/// why <see cref="WbsTraceabilityObjective"/> differs here rather than by oversight.</para>
/// </summary>
/// <param name="Id">The requirement's identifier. This is what a node cites.</param>
/// <param name="RequirementNumber">Its human-readable number, e.g. <c>R-014</c>.</param>
/// <param name="Description">
/// The requirement's full text. Coverage is judged against the words, so a truncated
/// description changes the verdict rather than merely shortening the display.
/// </param>
/// <param name="DuplicateOfRequirementId">
/// The requirement this one duplicates, when it duplicates one. A duplicate is covered when
/// its representative is covered — see <see cref="RequirementDuplicateEquivalence"/>.
/// </param>
/// <param name="IsClarificationDerived">
/// #2782: whether this requirement is the persisted answer to a clarification question rather than
/// something the customer captured directly. Carried because the coverage discharge is scoped to
/// this class and nothing else — an ordinary requirement cannot be disposed out of the plan.
/// </param>
/// <param name="CoverageDisposition">
/// #2782: a reviewer's recorded decision that this requirement demands no deliverable work.
/// Carried rather than recomputed, for the same reason
/// <see cref="WbsTraceabilityAcceptanceCriterion.IsCustomerStated"/> is: the decision is made on the
/// entity, and a second copy of the judgement here could disagree with it.
/// </param>
/// <param name="CoverageDispositionBy">
/// Who recorded it. Carried because the gate discharges on a <em>recorded reviewer decision</em>,
/// and the enum alone cannot show there was one — an unattributed value is a field someone set, not
/// a decision someone made. The gate refuses to honour it.
/// </param>
public sealed record WbsTraceabilityRequirement(
    Guid Id,
    string RequirementNumber,
    string Description,
    Guid? DuplicateOfRequirementId = null,
    bool IsClarificationDerived = false,
    RequirementCoverageDisposition CoverageDisposition = RequirementCoverageDisposition.None,
    string? CoverageDispositionBy = null)
{
    /// <summary>
    /// #3223: whether this requirement obliges a WBS node, or is satisfied by the plan as a whole.
    /// </summary>
    /// <remarks>
    /// <para>Carried rather than recomputed here, for the same reason
    /// <see cref="WbsTraceabilityAcceptanceCriterion.IsCustomerStated"/> is: the classification is
    /// made once, from the real <c>Requirement</c>, by
    /// <c>WbsWorkBreakdownRequirementFilter.CoverageObligationFor</c>. A second copy of that
    /// judgement derived from the requirement number here could disagree with the one generation
    /// used — which is precisely the drift #3223 is about.</para>
    /// <para><b>Defaults to <see cref="WbsCoverageObligationKind.Node"/> deliberately.</b> A
    /// projection site that has not been taught to set it keeps demanding coverage rather than
    /// silently excusing it. An over-demanded requirement is a visible finding; an under-demanded
    /// one is a hole in the plan nobody sees.</para>
    /// </remarks>
    public WbsCoverageObligationKind CoverageObligation { get; init; } = WbsCoverageObligationKind.Node;
}

/// <summary>
/// #3223: the projection-side mirror of <c>WbsWorkBreakdownRequirementFilter.WbsCoverageObligation</c>.
/// </summary>
/// <remarks>
/// Duplicated as a Core-level enum because the classification lives in Application (it needs the
/// requirement subtypes) while this record is read by Core-level evaluators that do not reference
/// it. <c>WbsCoverageObligationContractTests</c> fails if the two ever diverge, so the copy cannot
/// rot silently — the same device the codebase already uses for the clarification category literal.
/// </remarks>
public enum WbsCoverageObligationKind
{
    None = 0,
    Node = 1,
    SatisfiedByPlan = 2,
}

/// <summary>
/// One captured success criterion offered to the work-breakdown stage as a referencable identity
/// (#2405). <see cref="RequirementNumber"/> and <see cref="Statement"/> exist so an uncovered
/// criterion can be reported the way the customer wrote it rather than as a bare GUID.
///
/// <para><see cref="IsCustomerStated"/> is carried rather than recomputed. The boundary is drawn on
/// <c>ProjectSuccessCriterion</c>, and #2405's rule is that a *customer* criterion may not be
/// silently dropped — a second copy of that judgement here could disagree with the entity's and
/// quietly excuse the exact case the rule exists for.</para>
///
/// <para><see cref="Provenance"/> is carried <em>as well as</em> the boolean, not instead of it.
/// The boolean is the coverage rule's question and the enum is the evidence: #2149 exists because
/// three disagreeing provenance representations were consolidated, and reducing the enum to a
/// boolean here and re-expanding it on the node row would relabel a criterion the customer typed
/// (<c>CustomerStated</c>) as one read out of a document.</para>
/// </summary>
public sealed record WbsTraceabilityAcceptanceCriterion(
    Guid Id,
    string RequirementNumber,
    string Statement,
    bool IsCustomerStated,
    ProvenanceSource Provenance =
        ProvenanceSource.ImportedFromDocument);
