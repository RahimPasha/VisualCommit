using System.Diagnostics;
using System.Globalization;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The palette's ranking against the number of entries and its build and ranking against the size
/// of a repository's refs (docs/phase-6-design.md 5.12), alone in the process: each measure runs
/// in a region of no garbage collection, which the whole process shares (<see cref="GrowthMeasures"/>).
/// The ratios compare the thread's own work (<see cref="ThreadWork"/>); the stopwatch's times are
/// reported, and held only to a gross ceiling.
/// </summary>
[Collection(GrowthCollection.Name)]
public class PaletteGrowthTests
{
    /// <summary>What one measure of the build may allocate: the larger allocates about 80 MB.</summary>
    private const long BuildBudget = 200L * 1024 * 1024;

    /// <summary>What one measure of the matcher may allocate: 3,000 entries ranked allocate well under a megabyte.</summary>
    private const long RankBudget = 64L * 1024 * 1024;

    [Fact]
    public void A_query_over_3000_entries_takes_the_work_of_about_ten_over_300()
    {
        // 5.12: a warm-up round, then 300 and 3,000 entries ranked interleaved, the best of three each.
        // What is caught is work that grows faster than the entries: linear work gives about 10 times
        // as much. The work is the thread's own (ThreadWork), not a stopwatch's time, which under the
        // default run's load also counts the waits for a processor; the measures are a fraction of a
        // millisecond, so they run alone and without a collection, which beside other tests made the
        // ratio pass 25 with linear work; the times are reported.
        var small = PaletteMatcherTests.Many(300);
        var large = PaletteMatcherTests.Many(3_000);
        (ulong Work, double Milliseconds) Measure(IReadOnlyList<PaletteEntry> items) =>
            GrowthMeasures.WithoutCollection(RankBudget, () =>
            {
                var watch = Stopwatch.StartNew();
                var start = ThreadWork.Now();
                var ranked = PaletteMatcher.Rank("check feat", [], items, []);
                var work = ThreadWork.Now() - start;
                var milliseconds = watch.Elapsed.TotalMilliseconds;
                Assert.Equal(items.Count / 2, ranked.Count);
                return (work, milliseconds);
            });

        Measure(small);
        Measure(large);
        var (bestSmall, bestLarge) = (ulong.MaxValue, ulong.MaxValue);
        var (fastestSmall, fastestLarge) = (double.MaxValue, double.MaxValue);
        for (var round = 0; round < 3; round++)
        {
            var (work, milliseconds) = Measure(small);
            (bestSmall, fastestSmall) = (Math.Min(bestSmall, work), Math.Min(fastestSmall, milliseconds));
            (work, milliseconds) = Measure(large);
            (bestLarge, fastestLarge) = (Math.Min(bestLarge, work), Math.Min(fastestLarge, milliseconds));
        }

        var ratio = (double)bestLarge / Math.Max(bestSmall, 1);
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"Ranked: 300 entries in {fastestSmall:F2} ms, 3,000 in {fastestLarge:F2} ms; the thread's work {ratio:F1} times as much."));
        Assert.True(ratio < 25, string.Create(CultureInfo.InvariantCulture, $"3,000 entries took {ratio:F1} times the work of 300 ({bestLarge} against {bestSmall})."));

        // A gross ceiling on the time: the laptop alone takes under a millisecond.
        Assert.True(fastestLarge < 1_000, string.Create(CultureInfo.InvariantCulture, $"3,000 entries took {fastestLarge:F0} ms."));
    }

    [Fact]
    public void The_build_and_a_one_letter_query_grow_in_step_with_the_refs()
    {
        // 5.12: 100 local branches, 200 remote branches and 10,000 tags (some 74,000 items) against
        // 2,500 tags (some 22,000), the best of three each, interleaved. The work is the thread's own
        // (ThreadWork), with no garbage collection while it is measured: the collector's work grows
        // faster than the items it keeps alive, and its budget follows the processor's caches, so
        // with it 1,000 against 10,000 tags grew 29 times on CI's Windows runner in a Release build
        // (run 39936260636), where this machine gave 7 to 9, as much as the lookup that was linear
        // once made it grow. Linear work grows as the items do (n log n a little more), work that
        // grows with their square as its square; the limit is the items' growth to the power 1.5.
        var (actions, _) = RecordingActions.Create();
        var keys = new KeyTexts(CommandKey.Ctrl);
        var small = PaletteEntriesTests.SizeContext(tags: 2_500);
        var large = PaletteEntriesTests.SizeContext(tags: 10_000);
        (ulong Work, double Milliseconds, int Items) Measure(RepositoryMenuContext context) =>
            GrowthMeasures.WithoutCollection(BuildBudget, () =>
            {
                var watch = Stopwatch.StartNew();
                var start = ThreadWork.Now();
                var items = AppCommands.RepositoryItems(context, actions, keys, false, null, null, []);
                var ranked = PaletteMatcher.Rank("v", [], items, []);
                var work = ThreadWork.Now() - start;
                var milliseconds = watch.Elapsed.TotalMilliseconds;
                Assert.NotEmpty(ranked);
                return (work, milliseconds, items.Count);
            });

        Measure(small);
        Measure(large);
        var (bestSmall, bestLarge) = (ulong.MaxValue, ulong.MaxValue);
        var (fastestSmall, fastestLarge) = (double.MaxValue, double.MaxValue);
        var (smallCount, largeCount) = (0, 0);
        for (var round = 0; round < 3; round++)
        {
            var (work, milliseconds, count) = Measure(small);
            (bestSmall, fastestSmall, smallCount) = (Math.Min(bestSmall, work), Math.Min(fastestSmall, milliseconds), count);
            (work, milliseconds, count) = Measure(large);
            (bestLarge, fastestLarge, largeCount) = (Math.Min(bestLarge, work), Math.Min(fastestLarge, milliseconds), count);
        }

        var ratio = (double)bestLarge / Math.Max(bestSmall, 1);
        var limit = Math.Pow((double)largeCount / smallCount, 1.5);
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"Built and ranked: 2,500 tags ({smallCount} items) in {fastestSmall:F0} ms, 10,000 tags ({largeCount} items) in {fastestLarge:F0} ms; the thread's work {ratio:F1} times as much (limit {limit:F1})."));
        Assert.True(largeCount > 60_000, $"The large context gave {largeCount} items.");
        Assert.True(ratio < limit, string.Create(CultureInfo.InvariantCulture, $"10,000 tags took {ratio:F1} times the work of 2,500 ({bestLarge} against {bestSmall}), {(double)largeCount / smallCount:F1} times the items; the limit is {limit:F1}."));

        // A gross ceiling on the time, as the diff view's: a stopwatch also counts the waits for a
        // processor, which another test process beside this one (CI's Git.Tests) makes long.
        Assert.True(fastestLarge < 20_000, string.Create(CultureInfo.InvariantCulture, $"10,000 tags took {fastestLarge:F0} ms."));
    }
}
