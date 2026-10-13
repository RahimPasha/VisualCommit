using System.Globalization;

namespace VisualCommit.Git;

/// <summary>
/// Reads the raw dates git prints as Unix seconds and a zone: <c>author-time 1767268800</c> and
/// <c>author-tz +0100</c> in a blame's porcelain, <c>HEAD@{1767268800 +0000}</c> in a reflog walk
/// with <c>--date=raw</c> (docs/phase-5-design.md 0.1, 0.2).
/// </summary>
internal static class GitRawDates
{
    /// <summary>
    /// Reads <paramref name="seconds"/> since the epoch in the zone <paramref name="zone"/>
    /// (<c>+hhmm</c> or <c>-hhmm</c>). A zone that <see cref="DateTimeOffset"/> cannot hold (more
    /// than 14 hours, which old or broken commits carry) is read as UTC, as
    /// <see cref="GitDates.TryParse"/> reads such a date.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> seconds, ReadOnlySpan<char> zone, out DateTimeOffset value)
    {
        value = default;
        if (!long.TryParse(seconds, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var unix)
            || unix < DateTimeOffset.MinValue.ToUnixTimeSeconds()
            || unix > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }

        var instant = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (zone.Length != 5
            || zone[0] is not ('+' or '-')
            || !int.TryParse(zone[1..3], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(zone[3..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        var offset = new TimeSpan(hours, minutes, 0);
        if (zone[0] == '-')
        {
            offset = -offset;
        }

        if (offset.Duration() > TimeSpan.FromHours(14))
        {
            // The clock time the zone gives, kept as UTC; at the ends of the calendar, the instant.
            var utc = instant.UtcDateTime;
            var fits = offset > TimeSpan.Zero ? DateTime.MaxValue - utc >= offset : utc - DateTime.MinValue >= offset.Duration();
            value = fits ? new DateTimeOffset(utc.Add(offset), TimeSpan.Zero) : instant;
            return true;
        }

        value = instant.ToOffset(offset);
        return true;
    }
}
