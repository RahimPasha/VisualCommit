using System.Globalization;
using Avalonia.Media;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 5's pixel helpers for the graph's lines (docs/test-reports/phase-5.md, part F's
/// conventions and decision FD15; package WP2b): a stretch of a lane's centre read as a dashed
/// line or as a solid one. They take a screenshot and window positions, so the scripted checks and
/// the App tests read the pictures alike. Each has a form that lists what is wrong, for a check's
/// own assertion, and a form that throws with that list.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The shortest run of dash or gap pixels FD15 lets through inside the range: a dash or gap of 3, give or take one.</summary>
    public const int DashRunMinimum = 2;

    /// <summary>The longest run of dash or gap pixels FD15 lets through inside the range.</summary>
    public const int DashRunMaximum = 4;

    /// <summary>How far apart two dashes start: a dash of 3 and a gap of 3.</summary>
    public const int DashPeriod = 6;

    /// <summary>How far the start of a dash may be from <see cref="DashPeriod"/> after the one before.</summary>
    public const int DashPeriodTolerance = 1;

    /// <summary>
    /// What breaks FD15's dash rule on the pixels at <paramref name="x"/> from <paramref name="top"/>
    /// to <paramref name="bottom"/> (window positions, both included): each pixel is a dash pixel
    /// when it is nearer to <paramref name="lane"/> than to the row's <paramref name="background"/>
    /// (the sum of the channels' differences), a gap pixel otherwise; every run of either kind is
    /// <see cref="DashRunMinimum"/> to <see cref="DashRunMaximum"/> long, except that the first and
    /// the last, which the range may cut, may be shorter (never longer: a cut run is no longer than
    /// a whole one); consecutive dashes start <see cref="DashPeriod"/> ±
    /// <see cref="DashPeriodTolerance"/> apart throughout, across rows; and there are at least
    /// <paramref name="minimumDashes"/> dashes, a dash cut by the range included. Empty when the
    /// pixels are dashed as the rule says.
    /// </summary>
    public static IReadOnlyList<string> DashedPixelProblems(Screenshot screenshot, int x, int top, int bottom, Color lane, Color background, int minimumDashes)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(bottom, top);

        // The runs of one kind of pixel, top to bottom: (first y, length, dash or gap).
        var runs = new List<(int Start, int Length, bool IsDash)>();
        for (var y = top; y <= bottom; y++)
        {
            var pixel = screenshot.PixelAt(x, y);
            var isDash = Distance(pixel, lane) < Distance(pixel, background);
            if (runs.Count > 0 && runs[^1].IsDash == isDash)
            {
                runs[^1] = runs[^1] with { Length = runs[^1].Length + 1 };
            }
            else
            {
                runs.Add((y, 1, isDash));
            }
        }

        var problems = new List<string>();
        for (var i = 0; i < runs.Count; i++)
        {
            // The range may cut the first and the last run short, never long: a cut run is no
            // longer than a whole one, so a line solid or missing at either end is not dashed.
            var run = runs[i];
            var isCut = i == 0 || i == runs.Count - 1;
            if (run.Length > DashRunMaximum || (!isCut && run.Length < DashRunMinimum))
            {
                var limit = isCut
                    ? string.Create(CultureInfo.InvariantCulture, $"more than {DashRunMaximum}")
                    : string.Create(CultureInfo.InvariantCulture, $"not {DashRunMinimum} to {DashRunMaximum}");
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The {(run.IsDash ? "dash" : "gap")} from y {run.Start} to {run.Start + run.Length - 1} is {run.Length} pixels long, {limit}."));
            }
        }

        // A dash that starts at the range's top may be cut there: its start is not where it begins.
        var starts = runs.Where(run => run.IsDash && run.Start > top).Select(run => run.Start).ToList();
        for (var i = 1; i < starts.Count; i++)
        {
            var apart = starts[i] - starts[i - 1];
            if (Math.Abs(apart - DashPeriod) > DashPeriodTolerance)
            {
                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The dashes at y {starts[i - 1]} and {starts[i]} start {apart} pixels apart, not {DashPeriod} ± {DashPeriodTolerance}."));
            }
        }

        var dashes = runs.Count(run => run.IsDash);
        if (dashes < minimumDashes)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"There are {dashes} dashes, fewer than {minimumDashes}."));
        }

        if (problems.Count > 0)
        {
            problems.Add("The runs: " + string.Join(", ", runs.Select(run => string.Create(
                CultureInfo.InvariantCulture,
                $"{(run.IsDash ? "dash" : "gap")} {run.Start}-{run.Start + run.Length - 1}"))));
        }

        return problems;
    }

    /// <summary>
    /// Throws unless the pixels at <paramref name="x"/> from <paramref name="top"/> to
    /// <paramref name="bottom"/> are dashed as FD15 says (<see cref="DashedPixelProblems"/>).
    /// <paramref name="what"/> names the line in the message.
    /// </summary>
    public static void AssertDashedPixels(Screenshot screenshot, int x, int top, int bottom, Color lane, Color background, int minimumDashes, string what)
    {
        var problems = DashedPixelProblems(screenshot, x, top, bottom, lane, background, minimumDashes);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"{what} at x {x} from y {top} to {bottom} is not dashed in {lane}: {string.Join(" ", problems)}"));
        }
    }

    /// <summary>
    /// What breaks a solid line on the pixels at <paramref name="x"/> from <paramref name="top"/>
    /// to <paramref name="bottom"/> (window positions, both included): every pixel is
    /// <paramref name="lane"/>, within 3 per channel (FD15). Empty when they all are.
    /// </summary>
    public static IReadOnlyList<string> SolidPixelProblems(Screenshot screenshot, int x, int top, int bottom, Color lane)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(bottom, top);

        var problems = new List<string>();
        for (var y = top; y <= bottom; y++)
        {
            var pixel = screenshot.PixelAt(x, y);
            if (Math.Abs(pixel.R - lane.R) > 3 || Math.Abs(pixel.G - lane.G) > 3 || Math.Abs(pixel.B - lane.B) > 3)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"y {y} is {pixel}."));
            }
        }

        return problems;
    }

    /// <summary>
    /// Throws unless every pixel at <paramref name="x"/> from <paramref name="top"/> to
    /// <paramref name="bottom"/> is <paramref name="lane"/> within 3 per channel
    /// (<see cref="SolidPixelProblems"/>). <paramref name="what"/> names the line in the message.
    /// </summary>
    public static void AssertSolidPixels(Screenshot screenshot, int x, int top, int bottom, Color lane, string what)
    {
        var problems = SolidPixelProblems(screenshot, x, top, bottom, lane);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"{what} at x {x} from y {top} to {bottom} is not solid {lane}: {string.Join(" ", problems)}"));
        }
    }

    private static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
}
