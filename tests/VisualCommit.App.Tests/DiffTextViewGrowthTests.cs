using System.Diagnostics;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The diff view's build and drawing against the number of lines (phase 2's Q2, D63), alone in the
/// process: each measure runs in a region of no garbage collection (<see cref="GrowthMeasures"/>),
/// inline at the diff body's size at 1100×700 (440×514).
/// </summary>
[Collection(GrowthCollection.Name)]
public class DiffTextViewGrowthTests
{
    [AvaloniaFact]
    public async Task Building_and_drawing_a_diff_grows_in_step_with_its_lines()
    {
        // What is caught is work that grows faster than the number of lines: the 60,000-line diff is
        // compared with a 6,000-line one, each the best of three, interleaved. Linear work gives a
        // ratio of 10 or less (drawing the view is a fixed cost), quadratic work about 100. The work
        // is the UI thread's own (ThreadWork), not a stopwatch's time: under the default run's load,
        // and on CI's 3-processor runners, the time also counted the waits for a processor, and
        // the ratio of times reached 25 to 100 with linear work (CI runs 39153491762, 39158437982,
        // and the side-by-side builds of phase 6's wave 2). Nor the collector's: with collections
        // in the measures, beside the other tests, the ratio of the thread's work passed 25 too.
        var diffs = await DiffScenario.DiffsAsync();
        var small = DiffScenario.Parse(["@@ -0,0 +1,6000 @@", .. Enumerable.Range(1, 6_000).Select(line => $"+line {line}")]);
        var view = new DiffTextView { FilePath = "data/large.txt", Highlighting = false };
        using var host = DiffHost.Show(view, 440, 514, ThemeVariant.Dark);
        host.Capture();

        // A first round warms both paths up and says what a measure allocates: the budget of the
        // region without collection is twice the most, and at least 64 MB.
        var allocated = 0L;
        foreach (var diff in new[] { small, diffs.Large })
        {
            var before = GC.GetTotalAllocatedBytes(precise: true);
            view.Diff = diff;
            host.Capture();
            allocated = Math.Max(allocated, GC.GetTotalAllocatedBytes(precise: true) - before);
        }

        var budget = Math.Max(64L * 1024 * 1024, 2 * allocated);
        (ulong Work, double Milliseconds) Measure(FileDiff diff) =>
            GrowthMeasures.WithoutCollection(budget, () =>
            {
                var watch = Stopwatch.StartNew();
                var start = ThreadWork.Now();
                view.Diff = diff;
                host.Capture();
                return (ThreadWork.Now() - start, watch.Elapsed.TotalMilliseconds);
            });

        var (bestSmall, bestLarge) = (ulong.MaxValue, ulong.MaxValue);
        var (fastestSmall, fastestLarge) = (double.MaxValue, double.MaxValue);
        for (var round = 0; round < 3; round++)
        {
            var (work, milliseconds) = Measure(small);
            (bestSmall, fastestSmall) = (Math.Min(bestSmall, work), Math.Min(fastestSmall, milliseconds));
            (work, milliseconds) = Measure(diffs.Large);
            (bestLarge, fastestLarge) = (Math.Min(bestLarge, work), Math.Min(fastestLarge, milliseconds));
        }

        Assert.Equal(60_001, view.Rows.Count);
        var ratio = (double)bestLarge / Math.Max(bestSmall, 1);
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"Built and drawn: 6,000 lines in {fastestSmall:F0} ms, 60,000 lines in {fastestLarge:F0} ms; the thread's work {ratio:F1} times as much; a measure allocated up to {allocated / (1024 * 1024)} MB."));
        Assert.True(ratio < 25, string.Create(CultureInfo.InvariantCulture, $"60,000 lines took {ratio:F1} times the work of 6,000 lines ({bestLarge} against {bestSmall})."));

        // A loose ceiling on the time still catches work gone gross.
        Assert.True(fastestLarge < 20_000, string.Create(CultureInfo.InvariantCulture, $"60,000 lines took {fastestLarge:F0} ms."));
    }
}
