namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Shared normalization for requirement text deduplication (#2466).
/// </summary>
internal static class RequirementDescriptionNormalizer
{
    internal const int MinSharedSignificantTokensForParaphrase = 6;

    /// <summary>
    /// #2467: the bar for *naming* a probable duplicate while explaining a coverage gap, which is
    /// lower than <see cref="MinSharedSignificantTokensForParaphrase"/> because the two decisions
    /// have different costs — see <c>RequirementDuplicateMatcher.LooksLikeSameObligation</c>.
    /// </summary>
    internal const int MinSharedSignificantTokensForHint = 3;

    /// <summary>
    /// #2633: the bar for treating uncited work as coverage. Five shared tokens, not three —
    /// a false positive here opens the approval gate, which is the collapse cost, not the hint cost.
    /// </summary>
    internal const int MinSharedSignificantTokensForCoverage = 5;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "into", "is", "it",
        "must", "of", "on", "or", "so", "that", "the", "their", "then", "there", "these", "this",
        "to", "using", "with",
    };

    internal static string NormalizeExact(string value) =>
        string.Concat(value.Trim().ToLowerInvariant().Where(character => !char.IsWhiteSpace(character)));

    internal static HashSet<string> SignificantTokens(string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
        {
            return tokens;
        }

        var current = new List<char>();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Add(character);
                continue;
            }

            AddToken(current, tokens);
            current.Clear();
        }

        AddToken(current, tokens);
        return tokens;
    }

    internal static int CountSharedSignificantTokens(string left, string right)
    {
        var leftTokens = SignificantTokens(left);
        var rightTokens = SignificantTokens(right);
        return leftTokens.Count(token => rightTokens.Contains(token));
    }

    internal static bool MeetsParaphraseOverlapThreshold(string left, string right) =>
        MeetsOverlapThreshold(left, right, MinSharedSignificantTokensForParaphrase, 0.6);

    internal static bool MeetsHintOverlapThreshold(string left, string right) =>
        MeetsOverlapThreshold(left, right, MinSharedSignificantTokensForHint, 0.5);

    internal static bool MeetsCoverageOverlapThreshold(string left, string right) =>
        MeetsOverlapThreshold(left, right, MinSharedSignificantTokensForCoverage, 0.5);

    private static bool MeetsOverlapThreshold(
        string left,
        string right,
        int minimumSharedTokens,
        double minimumSharedRatio)
    {
        var leftTokens = SignificantTokens(left);
        var rightTokens = SignificantTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return false;
        }

        var shared = leftTokens.Count(token => rightTokens.Contains(token));
        if (shared < minimumSharedTokens)
        {
            return false;
        }

        var smaller = Math.Min(leftTokens.Count, rightTokens.Count);
        return shared >= (int)Math.Ceiling(smaller * minimumSharedRatio);
    }

    private static void AddToken(IReadOnlyList<char> characters, ISet<string> tokens)
    {
        if (characters.Count < 4)
        {
            return;
        }

        var token = new string(characters.ToArray());
        if (!StopWords.Contains(token))
        {
            tokens.Add(token);
        }
    }
}
