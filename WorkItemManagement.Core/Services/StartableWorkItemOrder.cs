namespace WorkItemManagement.Core.Services;

/// <summary>
/// #21: the order in which ready stories are started (docs/33 §2.2). Readiness — every dependency
/// Done — is decided before this runs, so it only orders stories that are ready at the same time:
/// customer priority first, then the plan's natural order, then the WI number.
/// </summary>
internal sealed class StartableWorkItemOrder : IComparer<WorkItem>
{
    /// <summary>
    /// Prefix of a key the delivery lead authored in the plan (written by plusteam-shared's
    /// <c>WbsLeadAuthoredWorkItemKeys</c>). Only these keys carry plan order: a sign-off key is
    /// <c>wbs:story:{guid}</c>, an imported key is <c>workmgmt:…</c>, and a follow-up bug has none.
    /// </summary>
    internal const string LeadAuthoredKeyPrefix = "wbs:lead:";

    public static readonly StartableWorkItemOrder Instance = new();

    public int Compare(WorkItem? x, WorkItem? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var byPriority = PriorityRank(x.Priority).CompareTo(PriorityRank(y.Priority));
        if (byPriority != 0)
        {
            return byPriority;
        }

        var byPlan = ComparePlanOrder(PlanKey(x), PlanKey(y));
        if (byPlan != 0)
        {
            return byPlan;
        }

        // Unnumbered (0) items sort after numbered ones, so a missing number never jumps the queue.
        var byNumber = NumberRank(x.Number).CompareTo(NumberRank(y.Number));
        return byNumber != 0 ? byNumber : x.Id.CompareTo(y.Id);
    }

    /// <summary>
    /// Mapped explicitly: the enum's numeric values (Medium = 0, Low = 1, High = 2, Critical = 3)
    /// are not in order of importance.
    /// </summary>
    private static int PriorityRank(WorkItemPriority priority) => priority switch
    {
        WorkItemPriority.Critical => 0,
        WorkItemPriority.High => 1,
        WorkItemPriority.Medium => 2,
        WorkItemPriority.Low => 3,
        _ => 2,
    };

    private static long NumberRank(int number) => number > 0 ? number : long.MaxValue;

    private static string? PlanKey(WorkItem item) =>
        item.WbsKey is { } key && key.StartsWith(LeadAuthoredKeyPrefix, StringComparison.Ordinal)
            ? key[LeadAuthoredKeyPrefix.Length..]
            : null;

    /// <summary>
    /// Lead-authored keys come first, in natural order. Every other item has no plan order and
    /// compares equal, so the WI number decides between them.
    /// </summary>
    private static int ComparePlanOrder(string? x, string? y) =>
        (x, y) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => CompareNatural(x, y),
        };

    /// <summary>
    /// Compares runs of digits as numbers and everything else ordinally, so <c>E1-F2</c> sorts
    /// before <c>E1-F10</c> and <c>E2</c> before <c>E10</c>.
    /// </summary>
    internal static int CompareNatural(string x, string y)
    {
        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var xStart = i;
                var yStart = j;
                while (i < x.Length && char.IsAsciiDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsAsciiDigit(y[j]))
                {
                    j++;
                }

                var byNumber = CompareDigitRuns(x.AsSpan(xStart, i - xStart), y.AsSpan(yStart, j - yStart));
                if (byNumber != 0)
                {
                    return byNumber;
                }

                continue;
            }

            var byChar = x[i].CompareTo(y[j]);
            if (byChar != 0)
            {
                return byChar;
            }

            i++;
            j++;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }

    /// <summary>Numeric comparison of two digit runs of any length, without parsing.</summary>
    private static int CompareDigitRuns(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        x = x.TrimStart('0');
        y = y.TrimStart('0');
        var byLength = x.Length.CompareTo(y.Length);
        return byLength != 0 ? byLength : x.SequenceCompareTo(y);
    }
}
