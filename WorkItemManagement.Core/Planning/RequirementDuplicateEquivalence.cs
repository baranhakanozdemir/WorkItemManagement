
namespace WorkItemManagement.Core.Planning;

/// <summary>
/// Collapses duplicate obligations to a single coverage representative (#2466).
/// </summary>
public static class RequirementDuplicateEquivalence
{

    public static IReadOnlyList<WbsTraceabilityRequirement> SelectIndependentTraceabilityRequirements(
        IReadOnlyList<WbsTraceabilityRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var presentIds = requirements.Select(requirement => requirement.Id).ToHashSet();
        var eligible = requirements
            .Where(requirement => IsIndependentTraceabilityObligation(requirement, presentIds))
            .ToList();
        var groups = BuildTraceabilityGroups(eligible);
        return groups.Select(SelectCanonicalTraceability).ToArray();
    }

    public static IReadOnlyList<IReadOnlyList<WbsTraceabilityRequirement>> BuildTraceabilityCoverageGroups(
        IReadOnlyList<WbsTraceabilityRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var presentIds = requirements.Select(requirement => requirement.Id).ToHashSet();
        var eligible = requirements
            .Where(requirement => IsIndependentTraceabilityObligation(requirement, presentIds))
            .ToList();
        var groups = BuildTraceabilityGroups(eligible)
            .Select(group => group.ToList())
            .ToList();

        foreach (var alias in requirements.Where(requirement => requirement.DuplicateOfRequirementId is not null))
        {
            var targetGroup = groups.FirstOrDefault(group =>
                group.Any(member => member.Id == alias.DuplicateOfRequirementId));
            if (targetGroup is not null && targetGroup.All(member => member.Id != alias.Id))
            {
                targetGroup.Add(alias);
            }
        }

        return groups;
    }

    public static bool IsTraceabilityRequirementCovered(
        WbsTraceabilityRequirement requirement,
        IReadOnlyCollection<Guid> citedRequirementIds,
        IReadOnlyList<IReadOnlyList<WbsTraceabilityRequirement>> groups)
    {
        if (requirement.DuplicateOfRequirementId is Guid duplicateOf
            && citedRequirementIds.Contains(duplicateOf))
        {
            return true;
        }

        foreach (var group in groups)
        {
            if (!group.Any(member => member.Id == requirement.Id))
            {
                continue;
            }

            return group.Any(member => citedRequirementIds.Contains(member.Id));
        }

        return citedRequirementIds.Contains(requirement.Id);
    }

    internal static IReadOnlyList<IReadOnlyList<WbsTraceabilityRequirement>> BuildTraceabilityGroups(
        IReadOnlyList<WbsTraceabilityRequirement> requirements)
    {
        if (requirements.Count == 0)
        {
            return Array.Empty<IReadOnlyList<WbsTraceabilityRequirement>>();
        }

        var groups = new List<List<WbsTraceabilityRequirement>>();
        foreach (var requirement in requirements.OrderBy(
                     candidate => candidate.RequirementNumber,
                     StringComparer.Ordinal))
        {
            var matched = groups.FirstOrDefault(group =>
                group.Any(member => RequirementDuplicateMatcher.AreSameObligation(
                    member.Description,
                    requirement.Description)));
            if (matched is null)
            {
                groups.Add([requirement]);
                continue;
            }

            matched.Add(requirement);
        }

        return groups;
    }

    internal static WbsTraceabilityRequirement SelectCanonicalTraceability(
        IReadOnlyList<WbsTraceabilityRequirement> group) =>
        group
            .OrderBy(requirement => ParseSequenceOrMax(requirement.RequirementNumber))
            .ThenBy(requirement => requirement.RequirementNumber, StringComparer.Ordinal)
            .First();

    private static bool IsIndependentTraceabilityObligation(
        WbsTraceabilityRequirement requirement,
        IReadOnlySet<Guid> presentIds) =>
        requirement.DuplicateOfRequirementId is not Guid duplicateOf || !presentIds.Contains(duplicateOf);

    private static int ParseSequenceOrMax(string requirementNumber) =>
        RequirementNumberAllocator.TryParseSequence(requirementNumber, out var sequence)
            ? sequence
            : int.MaxValue;
}
