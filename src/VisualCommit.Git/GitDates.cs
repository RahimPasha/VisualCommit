using System.Globalization;

namespace VisualCommit.Git;

/// <summary>Reads the strict ISO 8601 dates git prints for <c>%aI</c> and <c>%cI</c>.</summary>
internal static class GitDates
{
    /// <summary>
    /// Reads a date such as <c>2026-01-01T12:00:00+01:00</c>. Git prints UTC as <c>Z</c> or as
    /// <c>+00:00</c>, depending on its version; both are read.
    /// <para>
    /// Git keeps whatever time-zone offset a commit was made with, and old or broken commits can
    /// carry offsets that <see cref="DateTimeOffset"/> cannot hold (more than 14 hours). Such a
    /// date keeps its clock time and is read as UTC, so that the commit still appears.
    /// </para>
    /// </summary>
    public static bool TryParse(string text, out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        const string WithoutOffset = "yyyy-MM-ddTHH:mm:ss";
        if (text.Length >= WithoutOffset.Length
            && DateTime.TryParseExact(
                text.AsSpan(0, WithoutOffset.Length),
                WithoutOffset,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var clockTime))
        {
            value = new DateTimeOffset(clockTime, TimeSpan.Zero);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Reads a date as <see cref="TryParse"/> does, or gives the Unix epoch (1970-01-01 UTC) for
    /// one that cannot be read. Git cannot print the date of an ident without a time zone, which
    /// broken tools have written into commits: it leaves the field empty or prints the placeholder
    /// itself, and shows such a date as 1970 in its own log. The commit must still appear.
    /// </summary>
    public static DateTimeOffset ParseOrEpoch(string text) =>
        TryParse(text, out var value) ? value : DateTimeOffset.UnixEpoch;
}
