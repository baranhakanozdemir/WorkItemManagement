namespace WorkItemManagement.Core.Entities;

/// <summary>
/// #1791 (Epic #1785): single source of truth for the "commit range" summary
/// string closure reports and the final-acceptance page renders. Both closure
/// (workflow-scoped, Infrastructure) and the customer surface (Application)
/// aggregate the same persisted <see cref="WorkItemCommitRef"/>s; extracting
/// the formatter here keeps the wording consistent and eliminates the
/// intentional duplication that would otherwise sit across the two lanes.
/// </summary>
public static class WorkItemCommitRefRangeFormatter
{
    /// <summary>
    /// Returns e.g. <c>"acme/delivery aaaaaaaaaaaa..cccccccccccc (3 commit(s) across 2 work item(s))"</c>.
    /// Null when no commit with a non-blank SHA is recorded — closure and the
    /// customer surface both treat null as "no code reference recorded".
    /// </summary>
    public static string? BuildRangeSummary(IReadOnlyCollection<WorkItemCommitRef>? commitRefs)
    {
        if (commitRefs is null || commitRefs.Count == 0)
        {
            return null;
        }

        var ordered = OrderChronological(commitRefs);
        if (ordered.Length == 0)
        {
            return null;
        }

        var repositories = ordered
            .Select(reference => reference.Repository)
            .Where(repository => !string.IsNullOrWhiteSpace(repository))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(repository => repository, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var firstSha = ShortSha(ordered[0].CommitSha);
        var lastSha = ShortSha(ordered[^1].CommitSha);
        var range = string.Equals(firstSha, lastSha, StringComparison.Ordinal)
            ? firstSha
            : $"{firstSha}..{lastSha}";

        var workItemCount = ordered.Select(reference => reference.WorkItemId).Distinct().Count();
        var repositoryPrefix = repositories.Length == 0
            ? string.Empty
            : string.Join(", ", repositories) + " ";

        return $"{repositoryPrefix}{range} ({ordered.Length} commit(s) across {workItemCount} work item(s))";
    }

    /// <summary>
    /// Orders commit refs oldest-first, filtering out any with a blank SHA.
    /// The customer surface reuses this ordering to render its per-commit list;
    /// closure reuses it internally.
    /// </summary>
    public static WorkItemCommitRef[] OrderChronological(IReadOnlyCollection<WorkItemCommitRef> commitRefs) =>
        commitRefs
            .Where(reference => !string.IsNullOrWhiteSpace(reference.CommitSha))
            .OrderBy(reference => reference.CommittedAt)
            .ThenBy(reference => reference.CommitSha, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Trims a SHA to 12 characters (or shorter if the source is shorter).</summary>
    public static string ShortSha(string sha) =>
        sha.Length > 12 ? sha[..12] : sha;
}
