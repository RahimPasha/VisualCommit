using System.Globalization;

namespace VisualCommit.Core;

/// <summary>
/// How the app shows dates: absolute, as <c>yyyy-MM-dd HH:mm</c>, in the local time zone or the
/// one <see cref="TimeZoneVariable"/> names (D43).
/// </summary>
public sealed class DateDisplay
{
    /// <summary>Environment variable that overrides the time zone, with an id such as <c>UTC</c>. The test harnesses set it.</summary>
    public const string TimeZoneVariable = "VISUALCOMMIT_TIME_ZONE";

    public DateDisplay(TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        TimeZone = timeZone;
    }

    /// <summary>The zone dates are shown in.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// The zone for this process: the one <see cref="TimeZoneVariable"/> names when it is set and
    /// known, otherwise the local zone.
    /// </summary>
    public static DateDisplay Resolve()
    {
        var id = Environment.GetEnvironmentVariable(TimeZoneVariable);
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return new DateDisplay(TimeZoneInfo.FindSystemTimeZoneById(id.Trim()));
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // An unknown id: fall back to the local zone rather than refuse to start.
            }
        }

        return new DateDisplay(TimeZoneInfo.Local);
    }

    /// <summary>The date as the Date column shows it, such as <c>2026-01-01 12:00</c>.</summary>
    public string Format(DateTimeOffset date) =>
        TimeZoneInfo.ConvertTime(date, TimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
