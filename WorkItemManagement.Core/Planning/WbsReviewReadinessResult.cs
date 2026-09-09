namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Outcome of the review-readiness quality gate for a WBS payload (#1587).
/// Separate from structural schema validation performed by the Summarizer.
/// </summary>
public sealed record WbsReviewReadinessResult(
    bool IsReviewReady,
    IReadOnlyList<string> Issues)
{
    /// <summary>
    /// #2633: findings that are reported and never fold into <see cref="IsReviewReady"/> —
    /// a requirement whose work is present but uncited. Init so existing constructions stay
    /// a pass with no observations.
    /// </summary>
    public IReadOnlyList<string> Observations { get; init; } = [];
}
