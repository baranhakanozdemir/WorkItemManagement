using System.Text.RegularExpressions;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Detects when two requirement statements carry the same obligation (#2466).
/// </summary>
public static partial class RequirementDuplicateMatcher
{
    public static bool AreSameObligation(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(
                RequirementDescriptionNormalizer.NormalizeExact(left),
                RequirementDescriptionNormalizer.NormalizeExact(right),
                StringComparison.Ordinal))
        {
            return true;
        }

        if (HasContradictoryNegation(left, right))
        {
            return false;
        }

        return RequirementDescriptionNormalizer.MeetsParaphraseOverlapThreshold(left, right);
    }

    /// <summary>
    /// A weaker bar than <see cref="AreSameObligation(string, string)"/>, for explaining a coverage
    /// gap rather than collapsing one (#2467). Collapsing has to be conservative — a wrong collapse
    /// silently retires an obligation the customer stated — while a wrong hint costs one sentence
    /// the customer can dismiss, so the paraphrase band below the collapse threshold is worth
    /// naming. Contradictory negation still disqualifies: "must" and "must not" are not the same
    /// obligation at any threshold.
    /// </summary>
    public static bool LooksLikeSameObligation(string left, string right)
    {
        if (AreSameObligation(left, right))
        {
            return true;
        }

        return !HasContradictoryNegation(left, right)
            && RequirementDescriptionNormalizer.MeetsHintOverlapThreshold(left, right);
    }

    /// <summary>
    /// #2633: whether uncited planned work covers a requirement well enough to stop blocking
    /// approval. <see cref="LooksLikeSameObligation"/> is the hint bar — three shared tokens,
    /// false positives allowed because they used to cost one sentence. Opening the gate is
    /// the collapse cost, so this requires <see cref="AreSameObligation"/> or five shared
    /// significant tokens. Opposite actions that share a noun phrase ("encrypt … at rest"
    /// vs "delete … at rest") stay a gap.
    /// </summary>
    public static bool LooksLikeCoveredWork(string nodeText, string requirementText)
    {
        if (AreSameObligation(nodeText, requirementText))
        {
            return true;
        }

        return !HasContradictoryNegation(nodeText, requirementText)
            && RequirementDescriptionNormalizer.MeetsCoverageOverlapThreshold(nodeText, requirementText);
    }

    // The Requirement-entity overload of AreSameObligation stays in plusteam
    // (WorkItemManagement#6). It only forwarded to the string overload above, and carrying it here
    // would have pulled the requirement entity and its subtype hierarchy into a work-item package
    // for a one-line delegation. Callers holding entities pass .Description.

    /// <summary>
    /// Word-level, so bare forms ("never retries", "does not retry") count as much as the negated
    /// modals ("must not retry"). The inverse of an obligation shares almost all of its vocabulary,
    /// which is exactly the shape the overlap thresholds accept.
    /// </summary>
    private static bool HasContradictoryNegation(string left, string right) =>
        NegationRegex().IsMatch(left) != NegationRegex().IsMatch(right);

    [GeneratedRegex(@"\b(?:not|never|cannot)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegationRegex();
}
