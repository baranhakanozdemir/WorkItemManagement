using System.Text.RegularExpressions;

namespace WorkItemManagement.Core.Planning;

/// <summary>
/// #2890 (P4 / #2885): Teaches WBS generation and validation that for eligible .NET delivery
/// projects, the domain layer (entities, DbSets, EF Core mappings, repositories, CRUD application
/// services, API controllers, and tenancy tests) is pre-generated deterministically.
///
/// <para>
/// Planning suppresses boilerplate "create entity / repository / controller / CRUD" stories for
/// generated entities, and shifts stories toward business logic, custom workflows, domain rules,
/// and external integrations on top of the generated foundation, referencing the pre-generated
/// types as the starting point.
/// </para>
/// </summary>
/// <remarks>
/// <para>Trimmed on extraction (WorkItemManagement#6). The original in plusteam also carries the
/// eligibility overloads and the prompt formatter, which resolve a delivery stack
/// (<c>DeliveryStack</c>, <c>DeliveryStackResolver</c>, the backend-framework decision category)
/// and read an <c>IProjectContext</c>. Readiness evaluation calls exactly one member of this
/// class — <see cref="CollectBoilerplateIssues"/> — and that member touches none of them.</para>
/// <para>The split is not visible from the class name, which is why it is written down here:
/// moving this type wholesale would import the delivery-stack surface into a work-item package,
/// and the reason would only surface once the dependency graph turned circular.</para>
/// </remarks>
public static class WbsPreGeneratedDomainLayerRules
{
    private static readonly Regex[] BoilerplateTitlePatterns =
    [
        new Regex(
            @"^\s*(create|add|implement|setup|build|scaffold|generate|define)\s+([a-zA-Z0-9_\-]+\s+)?(entity|entities|model|models|domain\s+model|repository|repositories|crud|controller|controllers|api\s+controller|dbset|dbsets|database\s+table|database\s+tables)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(
            @"\b(crud\s+(endpoints?|operations?|service|api|controller)|(entity|model|repository|controller)\s+boilerplate|basic\s+crud)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(
            @"^\s*(create|add|implement|setup)\s+crud\s+(operations?|endpoints?|service)?\s+for\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(
            @"^\s*(create|setup|configure)\s+dbcontext\s+and\s+dbsets?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <summary>
    /// Returns true if the title matches a known boilerplate CRUD / entity / repository / controller story pattern.
    /// </summary>
    public static bool IsBoilerplateStoryTitle(string? title, IEnumerable<string>? entityNames = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        foreach (var pattern in BoilerplateTitlePatterns)
        {
            if (pattern.IsMatch(title))
            {
                return true;
            }
        }

        if (entityNames is not null)
        {
            foreach (var entity in entityNames.Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                var patterns = EntityPatterns.GetOrAdd(entity, BuildEntityPatterns);
                if (patterns.Declaration.IsMatch(title) || patterns.Crud.IsMatch(title))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// #3209: the per-entity patterns, compiled once per entity name rather than once per
    /// (node × entity) pair.
    /// </summary>
    /// <remarks>
    /// <para>These were built with <c>new Regex(...)</c> inside the matching loop, so a readiness
    /// evaluation over a 1,000-node tree with five pre-generated entities constructed ten thousand
    /// regexes. Measured on that shape: the whole evaluation took <b>~1.3–2.0 seconds</b>, and the
    /// same evaluation with the entity rule skipped took <b>8 ms</b>. The rule was about 99.5% of
    /// the cost of judging a work breakdown, and it is the reason that judgement could approach
    /// Temporal's two-second workflow-task deadlock threshold at all.</para>
    /// <para>Keyed by entity name and unbounded, which is safe because the keys are domain entity
    /// names from published models — tens per project, not user-supplied text.</para>
    /// </remarks>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, EntityPatternPair> EntityPatterns =
        new(StringComparer.Ordinal);

    private static EntityPatternPair BuildEntityPatterns(string entity) =>
        new(
            new Regex(
                $@"^\s*(create|add|implement|setup|build)\s+{Regex.Escape(entity)}\s+(entity|model|repository|service|controller|crud|table)\b",
                RegexOptions.IgnoreCase),
            new Regex(
                $@"\b{Regex.Escape(entity)}\s+crud\b",
                RegexOptions.IgnoreCase));

    private sealed record EntityPatternPair(Regex Declaration, Regex Crud);

    /// <summary>
    /// Returns true if a WBS node represents boilerplate entity / CRUD creation.
    /// </summary>
    public static bool IsBoilerplateNode(WbsNodePayload node, IEnumerable<string>? entityNames = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        return IsBoilerplateStoryTitle(node.Title, entityNames)
            || (node.Kind is WbsNodeKind.UserStory or WbsNodeKind.Task && IsBoilerplateStoryTitle(node.Description, entityNames));
    }

    /// <summary>
    /// Collects review findings for any boilerplate entity / CRUD stories in the WBS payload.
    /// </summary>
    public static IReadOnlyList<string> CollectBoilerplateIssues(
        WbsStructurePayload payload,
        IEnumerable<string>? entityNames = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var issues = new List<string>();
        foreach (var node in payload.Nodes)
        {
            if (node.Kind is WbsNodeKind.UserStory or WbsNodeKind.Task && IsBoilerplateNode(node, entityNames))
            {
                issues.Add(
                    $"Boilerplate domain story: {node.Kind} '{node.EmittedKey}' ({node.Title}) defines "
                    + "boilerplate entity, repository, controller, or CRUD generation that is already pre-generated. "
                    + "Focus stories on business logic, custom workflows, or integrations on top of the pre-generated domain layer.");
            }
        }

        return issues;
    }

    /// <summary>
    /// Validates that a WBS structure payload does not contain boilerplate entity stories for pre-generated entities.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        WbsStructurePayload payload,
        IEnumerable<string>? entityNames = null) =>
        CollectBoilerplateIssues(payload, entityNames);
}
