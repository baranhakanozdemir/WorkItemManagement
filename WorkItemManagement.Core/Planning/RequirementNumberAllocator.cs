namespace WorkItemManagement.Core.Planning;

/// <summary>Parses and formats project-scoped requirement numbers (e.g. R-001).</summary>
public static class RequirementNumberAllocator
{
    public static string Format(int sequence) => $"R-{sequence:D3}";

    public static bool TryParseSequence(string requirementNumber, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(requirementNumber)
            || !requirementNumber.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(requirementNumber.AsSpan(2), out sequence) && sequence > 0;
    }
}
