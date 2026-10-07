using System.Globalization;

namespace WorkItemManagement.Core;

/// <summary>
/// The display form of <see cref="IWorkItem.Number"/>: <c>WI-42</c>. The one place that formats
/// and parses it, so a work order, a pull-request title and the page that shows the item cannot
/// disagree about what an item is called.
/// </summary>
/// <remarks>
/// <c>WI-</c> rather than <c>#</c>: a delivery repository is a GitHub repository, where <c>#42</c>
/// already means that repository's own issue or pull request 42.
/// </remarks>
public static class WorkItemNumber
{
    public const string Prefix = "WI-";

    /// <summary>Formats a positive number as <c>WI-&lt;n&gt;</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not positive.</exception>
    public static string Format(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return Prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads <c>WI-&lt;n&gt;</c> (prefix in any case). Accepts only a positive integer written plainly:
    /// no sign, no leading zero, no whitespace, nothing after it, and no value past
    /// <see cref="int.MaxValue"/>.
    /// </summary>
    public static bool TryParse(string? value, out int number)
    {
        number = 0;
        if (value is null
            || value.Length <= Prefix.Length
            || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value.AsSpan(Prefix.Length);
        if (digits[0] == '0')
        {
            return false;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            return false;
        }

        number = parsed;
        return true;
    }
}
