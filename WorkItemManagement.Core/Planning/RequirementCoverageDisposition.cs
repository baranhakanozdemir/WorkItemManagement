namespace WorkItemManagement.Core.Planning;

/// <summary>
/// #2782: a reviewer's recorded decision about whether a requirement demands deliverable work.
/// </summary>
/// <remarks>
/// <para>The traceability gate blocks a plan when a requirement is cited by no WBS node. That is
/// right for anything the plan must build, and wrong for a clarification answer that only narrows
/// scope: an answer confirming that everything in scope shares one variant is context, and no node
/// legitimately implements it.</para>
/// <para><b>Nothing here is inferred.</b> A clarification answer can equally <em>add</em> scope
/// ("Should we support SSO?" → "Yes"), and the two are indistinguishable in the data: source
/// category records who decided, not what the answer meant, and nothing on the gap payload carries
/// the axis. #2618 exists because clarification-derived scope going unnoticed caused a real problem,
/// so exempting the whole class would reopen it for exactly the answers the guardrail protects.</para>
/// <para>So the only reliable classifier is a person, and the discharge <em>is</em> that person's
/// recorded decision — never a heuristic. An item is surfaced and decided, not auto-ignored.</para>
/// </remarks>
public enum RequirementCoverageDisposition
{
    /// <summary>
    /// Undecided. The requirement is an ordinary coverage obligation and an uncited one blocks the
    /// plan — the behaviour every requirement has until someone says otherwise.
    /// </summary>
    None = 0,

    /// <summary>
    /// A reviewer decided this requirement is an assumption or constraint that no node implements.
    /// It stays in the catalogue, stays citable, and stops being a coverage obligation.
    /// </summary>
    NoDeliverableWork = 1,
}
