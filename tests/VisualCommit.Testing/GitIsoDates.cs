using System.Globalization;

namespace VisualCommit.Testing;

/// <summary>
/// Reads the dates git prints in its strict ISO 8601 form (<c>%aI</c>, <c>%cI</c>,
/// <c>--date=iso-strict</c>), for tests that compare them with a date of their own. How git
/// writes UTC there depends on its version: git 2.36 writes <c>+00:00</c>, git 2.55 (CI's
/// runners) writes <c>Z</c>. A test compares the <see cref="DateTimeOffset"/> it gets here, an
/// instant, and its <see cref="DateTimeOffset.Offset"/>, the zone git kept, never git's text.
/// </summary>
public static class GitIsoDates
{
    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss'Z'"];

    /// <summary>Reads one date; anything but git's strict ISO form is refused (<see cref="FormatException"/>).</summary>
    public static DateTimeOffset Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DateTimeOffset.ParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    /// <summary>Reads one date per line of git's output; empty lines are left out.</summary>
    public static IReadOnlyList<DateTimeOffset> ParseLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Parse)];
    }
}
