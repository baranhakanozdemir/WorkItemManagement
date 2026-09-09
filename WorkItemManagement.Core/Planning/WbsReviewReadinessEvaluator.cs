using System.Text.RegularExpressions;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Minimum quality bar before a WBS may unlock customer approval (#1587).
/// </summary>
public static partial class WbsReviewReadinessEvaluator
{
    public const double MinimumChildCoverageRatio = 0.80;

    private static readonly Regex MirrorTitlePattern = MirrorTitleRegex();

    /// <summary>
    /// Words that carry no decomposition meaning: a child whose only addition is one of these has
    /// still said nothing its parent did not. Stripped from both sides before comparison.
    /// </summary>
    private static readonly HashSet<string> FillerTokens = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "as", "at", "by", "for", "from", "in", "into", "it", "its", "of", "on",
        "or", "that", "the", "this", "to", "with",
        "build", "create", "deliver", "delivery", "develop", "development", "general", "generic",
        "implement", "implementation", "item", "misc", "miscellaneous", "other", "phase",
        "related", "stage", "task", "various", "work",
    };

    public static WbsReviewReadinessResult Evaluate(
        WbsStructurePayload payload,
        WbsStructureSource source,
        WbsTraceabilityContext? capturedScope = null,
        WbsScopeFidelityBounds? bounds = null,
        IEnumerable<string>? preGeneratedEntities = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var issues = new List<string>();

        if (source is WbsStructureSource.DeterministicFallback)
        {
            issues.Add("Structure was produced by the deterministic fallback, not deliberation.");
        }

        if (source is WbsStructureSource.SummarizerNonConverged)
        {
            issues.Add("Deliberation did not converge before summarization.");
        }

        if (source is WbsStructureSource.SummarizerDependencyCycleSalvaged)
        {
            issues.Add("Dependency links formed a cycle, so +team.ai removed the cycle-causing link(s) before review.");
        }

        // #3236: a separate sentence rather than a shared "links were removed" one. The customer's
        // next action differs — a broken cycle means the order was wrong, a removed orphan means
        // something was named as a prerequisite that nobody planned, and that second case is worth
        // looking for in the plan rather than accepting as a tidy-up.
        if (source is WbsStructureSource.SummarizerOrphanDependencySalvaged)
        {
            issues.Add(
                "Some work named prerequisites that are not in this plan, so +team.ai removed those "
                + "links before review.");
        }

        CollectStructuralQualityIssues(payload, issues);
        issues.AddRange(WbsSessionSizingRules.CollectReviewIssues(payload));
        var entities = (preGeneratedEntities ?? capturedScope?.PreGeneratedEntities)?.ToList();
        if (entities is { Count: >= DomainModelEligibility.MinimumEntityCount })
        {
            issues.AddRange(WbsPreGeneratedDomainLayerRules.CollectBoilerplateIssues(payload, entities));
        }
        CollectDecompositionDepthIssues(
            payload.Nodes,
            issues,
            WbsScopeFidelityBounds.ScopeItemCount(capturedScope),
            bounds ?? WbsScopeFidelityBounds.Default);

        return new WbsReviewReadinessResult(issues.Count == 0, issues);
    }

    /// <summary>
    /// Structural quality checks used by the Summarizer retry loop. Does not apply
    /// provenance-based review-readiness rules (non-converged output may still persist
    /// with <see cref="WbsStructureSource.SummarizerNonConverged"/> and ReviewReady=false).
    /// </summary>
    public static bool TryValidateStructure(WbsStructurePayload payload, out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var issues = new List<string>();
        CollectStructuralQualityIssues(payload, issues);
        errors = issues;
        return issues.Count == 0;
    }

    /// <summary>
    /// Indexes nodes by emitted key, keeping only keys that identify exactly one node.
    ///
    /// <para>Generated payloads are duplicate-free (<c>WbsSummarizer.ValidatePayload</c> rejects
    /// repeats), but a payload rebuilt from persisted rows carries whatever is in the table — and a
    /// plain <c>ToDictionary</c> there throws, which the review path turns into "no findings at all"
    /// (#2430). An ambiguous key yields no lookup instead, so one unattributable relationship is lost
    /// rather than the whole evaluation.</para>
    /// </summary>
    internal static Dictionary<string, WbsNodePayload> IndexUnambiguousByEmittedKey(
        IReadOnlyList<WbsNodePayload> nodes) =>
        nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.EmittedKey))
            .GroupBy(node => node.EmittedKey, StringComparer.Ordinal)
            .Where(group => !group.Skip(1).Any())
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static void CollectStructuralQualityIssues(WbsStructurePayload payload, IList<string> issues)
    {
        var nodes = payload.Nodes;
        var nodesByKey = IndexUnambiguousByEmittedKey(nodes);

        foreach (var node in nodes)
        {
            if (IsMirrorFillerTitle(node.Title))
            {
                issues.Add($"Node '{node.EmittedKey}' uses template mirror filler title '{node.Title}'.");
            }

            if (node.Kind is WbsNodeKind.UserStory or WbsNodeKind.Task
                && node.ParentEmittedKey is not null
                && nodesByKey.TryGetValue(node.ParentEmittedKey, out var parent)
                && IsMirrorOfParent(node.Title, parent.Title))
            {
                issues.Add(
                    $"Node '{node.EmittedKey}' mirrors its parent '{node.ParentEmittedKey}' without meaningful decomposition.");
            }
        }

        var epics = nodes.Where(node => node.Kind == WbsNodeKind.Epic).ToList();
        foreach (var epic in epics)
        {
            var featureCount = nodes.Count(node =>
                node.Kind == WbsNodeKind.Feature
                && string.Equals(node.ParentEmittedKey, epic.EmittedKey, StringComparison.Ordinal));

            if (featureCount == 0 && nodes.Any(node => node.Kind == WbsNodeKind.Feature))
            {
                issues.Add($"Epic '{epic.EmittedKey}' has no features while other epics do.");
            }
        }
    }

    private static void CollectDecompositionDepthIssues(
        IReadOnlyList<WbsNodePayload> nodes,
        IList<string> issues,
        int capturedScopeItems,
        WbsScopeFidelityBounds bounds)
    {
        var hasEpic = nodes.Any(node => node.Kind == WbsNodeKind.Epic);
        var hasFeature = nodes.Any(node => node.Kind == WbsNodeKind.Feature);
        var hasStory = nodes.Any(node => node.Kind == WbsNodeKind.UserStory);

        if (hasFeature && hasStory)
        {
            CollectChildCoverageIssue(
                nodes,
                parentKind: WbsNodeKind.Feature,
                childKind: WbsNodeKind.UserStory,
                parentLabel: "features",
                childLabel: "user stories",
                capturedScopeItems,
                bounds,
                issues);
        }
        if (hasEpic && !hasFeature)
        {
            issues.Add("Structure contains epics but no features — decomposition is too shallow for customer review.");
        }

        if (hasFeature && !hasStory)
        {
            issues.Add("Structure contains features but no user stories — decomposition is too shallow for customer review.");
        }
    }

    private static void CollectChildCoverageIssue(
        IReadOnlyList<WbsNodePayload> nodes,
        WbsNodeKind parentKind,
        WbsNodeKind childKind,
        string parentLabel,
        string childLabel,
        int capturedScopeItems,
        WbsScopeFidelityBounds bounds,
        IList<string> issues)
    {
        var parents = nodes
            .Where(node => node.Kind == parentKind)
            .ToArray();
        if (parents.Length == 0)
        {
            return;
        }

        // #2628: a small complete tree is not depth-truncated. The 80% ratio only fires
        // when this level is large enough, on the same scale as the proportionality ceiling.
        if (!bounds.IsTruncationSuspectParentLevel(parents.Length, capturedScopeItems))
        {
            return;
        }

        var parentKeysWithChildren = nodes
            .Where(node => node.Kind == childKind && !string.IsNullOrWhiteSpace(node.ParentEmittedKey))
            .Select(node => node.ParentEmittedKey!)
            .ToHashSet(StringComparer.Ordinal);
        var coveredParents = parents.Count(parent => parentKeysWithChildren.Contains(parent.EmittedKey));
        var coverageRatio = (double)coveredParents / parents.Length;
        if (coverageRatio >= MinimumChildCoverageRatio)
        {
            return;
        }

        var requiredParents = (int)Math.Ceiling(parents.Length * MinimumChildCoverageRatio);
        var coverageVerb = coveredParents == 1 ? "has" : "have";
        issues.Add(
            $"Only {coveredParents} of {parents.Length} {parentLabel} {coverageVerb} {childLabel}; "
            + $"at least {requiredParents} are required for customer review. Structure appears depth-truncated.");
    }

    internal static bool IsMirrorFillerTitle(string title) =>
        !string.IsNullOrWhiteSpace(title)
        && MirrorTitlePattern.IsMatch(title.Trim());

    /// <summary>
    /// Returns the share of <paramref name="parentKind"/> nodes that have at least one
    /// <paramref name="childKind"/> child. Used by WBS fan-out repair loops (#2112).
    /// </summary>
    public static double GetChildCoverageRatio(
        IReadOnlyList<WbsNodePayload> nodes,
        WbsNodeKind parentKind,
        WbsNodeKind childKind)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var parents = nodes.Where(node => node.Kind == parentKind).ToArray();
        if (parents.Length == 0)
        {
            return 1.0;
        }

        var parentKeysWithChildren = nodes
            .Where(node => node.Kind == childKind && !string.IsNullOrWhiteSpace(node.ParentEmittedKey))
            .Select(node => node.ParentEmittedKey!)
            .ToHashSet(StringComparer.Ordinal);

        return (double)parents.Count(parent => parentKeysWithChildren.Contains(parent.EmittedKey))
            / parents.Length;
    }

    /// <summary>
    /// Parents of <paramref name="parentKind"/> that have no <paramref name="childKind"/> child.
    /// </summary>
    public static IReadOnlyList<WbsNodePayload> GetParentsWithoutChildren(
        IReadOnlyList<WbsNodePayload> nodes,
        WbsNodeKind parentKind,
        WbsNodeKind childKind) =>
        GetParentsWithoutChildren(nodes, parentKind, childKind, StringComparer.Ordinal);

    internal static IReadOnlyList<WbsNodePayload> GetParentsWithoutChildren(
        IReadOnlyList<WbsNodePayload> nodes,
        WbsNodeKind parentKind,
        WbsNodeKind childKind,
        IEqualityComparer<string> parentKeyComparer)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var parentKeysWithChildren = nodes
            .Where(node => node.Kind == childKind && !string.IsNullOrWhiteSpace(node.ParentEmittedKey))
            .Select(node => node.ParentEmittedKey!)
            .ToHashSet(parentKeyComparer);

        return nodes
            .Where(node => node.Kind == parentKind && !parentKeysWithChildren.Contains(node.EmittedKey))
            .OrderBy(node => node.Order)
            .ThenBy(node => node.EmittedKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool MeetsMinimumChildCoverage(
        IReadOnlyList<WbsNodePayload> nodes,
        WbsNodeKind parentKind,
        WbsNodeKind childKind,
        int capturedScopeItems = 0,
        WbsScopeFidelityBounds? bounds = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var parentCount = nodes.Count(node => node.Kind == parentKind);
        var effective = bounds ?? WbsScopeFidelityBounds.Default;
        if (!effective.IsTruncationSuspectParentLevel(parentCount, capturedScopeItems))
        {
            return true;
        }

        return GetChildCoverageRatio(nodes, parentKind, childKind) >= MinimumChildCoverageRatio;
    }

    /// <summary>
    /// True when a child only restates its parent and contributes nothing of its own (#2420).
    /// The single implementation of the rule — <c>WbsBranchSummarizer</c> calls this one instead
    /// of keeping a copy, because two copies of a rule that must agree eventually stop agreeing.
    /// </summary>
    /// <remarks>
    /// Substring containment is deliberately not used. A well-formed WBS repeats its parent's noun
    /// phrase in the children ("Recording" -> "Recording playback controls") and names some children
    /// more briefly than their parent ("Recording playback" -> "Playback"); containment flags both,
    /// so the rule fired on the naming convention decomposition is supposed to follow. Because the
    /// same predicate gates the summarizer retry loop, that also pushed generation to rename correct
    /// children into titles that no longer echoed their parent. Comparing meaningful token sets
    /// flags restatement only: identical wording, a filler prefix ("Deliver: Recording"), or filler
    /// padding ("Recording tasks").
    /// </remarks>
    internal static bool IsMirrorOfParent(string childTitle, string parentTitle)
    {
        if (string.IsNullOrWhiteSpace(childTitle) || string.IsNullOrWhiteSpace(parentTitle))
        {
            return false;
        }

        var childTokens = GetMeaningfulTokens(childTitle);
        var parentTokens = GetMeaningfulTokens(parentTitle);

        // A title made of nothing but filler ("Deliver: work") leaves no tokens to compare, and two
        // empty sets are equal, so an unrelated pair would read as a mirror. Fall back to comparing
        // the titles themselves, which is the only claim the token sets can no longer support.
        if (childTokens.Count == 0 || parentTokens.Count == 0)
        {
            return string.Equals(
                CollapseWhitespace(childTitle),
                CollapseWhitespace(parentTitle),
                StringComparison.OrdinalIgnoreCase);
        }

        return childTokens.SetEquals(parentTokens);
    }

    /// <summary>
    /// Lower-cased content words of a title, with filler words dropped and plurals collapsed, so
    /// that "Deliver: Recording tasks" and "Recording task" reduce to the same set.
    /// </summary>
    private static HashSet<string> GetMeaningfulTokens(string title)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in TokenRegex().Matches(title).Cast<Match>())
        {
            var token = match.Value.ToLowerInvariant();
            if (FillerTokens.Contains(token))
            {
                continue;
            }

            // Filler is checked on both forms: singularizing first would turn "this" into "thi"
            // and smuggle it past the list, while checking only the raw token would keep "tasks".
            var singular = Singularize(token);
            if (!FillerTokens.Contains(singular))
            {
                tokens.Add(singular);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Trailing-plural collapse, so a pluralized restatement cannot defeat the check: "controls"
    /// for "control", "policies" for "policy", "boxes" for "box", "statuses" for "status".
    /// Endings that are not plural markers are left alone ("class", "analysis", "status").
    /// </summary>
    private static string Singularize(string token)
    {
        if (token.Length < 4 || !token.EndsWith('s'))
        {
            return token;
        }

        if (token.EndsWith("ies", StringComparison.Ordinal))
        {
            return string.Concat(token.AsSpan(0, token.Length - 3), "y");
        }

        // "-es" is the plural only where the stem itself ends in a sibilant, which is what separates
        // "statuses" and "classes" from "cases"; the trailing-s rule below handles the latter.
        if (token.EndsWith("uses", StringComparison.Ordinal)
            || token.EndsWith("sses", StringComparison.Ordinal)
            || token.EndsWith("xes", StringComparison.Ordinal)
            || token.EndsWith("zes", StringComparison.Ordinal)
            || token.EndsWith("ches", StringComparison.Ordinal)
            || token.EndsWith("shes", StringComparison.Ordinal))
        {
            return token[..^2];
        }

        return token.EndsWith("ss", StringComparison.Ordinal)
            || token.EndsWith("us", StringComparison.Ordinal)
            || token.EndsWith("is", StringComparison.Ordinal)
                ? token
                : token[..^1];
    }

    private static string CollapseWhitespace(string title) =>
        WhitespaceRegex().Replace(title.Trim(), " ");

    [GeneratedRegex(@"^(Deliver|Implement)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MirrorTitleRegex();

    /// <summary>
    /// Unicode letters, marks and digits: an ASCII-only class produces no tokens at all for a
    /// non-Latin title, which would make every such pair compare equal.
    /// </summary>
    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
