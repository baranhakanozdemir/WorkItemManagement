namespace WorkItemManagement.Core.Planning;

/// <summary>
/// The entity-count floor above which a plan is checked for re-planning a pre-generated
/// domain layer.
/// </summary>
/// <remarks>
/// Trimmed on extraction (WorkItemManagement#6). The original in plusteam also carries
/// <c>Evaluate(DomainModel?)</c> and its result type; readiness only ever reads this
/// constant, and <c>DomainModel</c> is a plusteam concept. Moving the whole class would
/// have pulled the domain-model surface into a work-item package for one integer.
/// </remarks>
public static class DomainModelEligibility
{
    public const int MinimumEntityCount = 3;
}
