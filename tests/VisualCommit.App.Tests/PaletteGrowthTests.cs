using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The palette's build and ranking against the size of a repository's refs (docs/phase-6-design.md
/// 5.12), alone in the process: each measure runs in a region of no garbage collection, which the
/// whole process shares.
/// </summary>
[Collection(PaletteGrowthTests.Alone)]
public class PaletteGrowthTests
{
    /// <summary>The collection that runs these tests with no other test beside them.</summary>
    public const string Alone = "Palette growth, alone";

    /// <summary>What one measure may allocate: the larger allocates about 80 MB.</summary>
    private const long NoCollectionBudget = 200L * 1024 * 1024;

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
        (ulong Work, double Milliseconds, int Items) Measure(RepositoryMenuContext context)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.True(GC.TryStartNoGCRegion(NoCollectionBudget), "The region without garbage collection did not start.");
            try
            {
                var watch = Stopwatch.StartNew();
                var start = ThreadWork.Now();
                var items = AppCommands.RepositoryItems(context, actions, keys, false, null, null, []);
                var ranked = PaletteMatcher.Rank("v", [], items, []);
                var work = ThreadWork.Now() - start;
                var milliseconds = watch.Elapsed.TotalMilliseconds;
                Assert.NotEmpty(ranked);
                Assert.True(GCSettings.LatencyMode == GCLatencyMode.NoGCRegion, "A garbage collection ran during the measure: the budget is too small.");
                return (work, milliseconds, items.Count);
            }
            finally
            {
                if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                {
                    GC.EndNoGCRegion();
                }
            }
        }

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
        Assert.True(fastestLarge < 2_000, string.Create(CultureInfo.InvariantCulture, $"10,000 tags took {fastestLarge:F0} ms."));
    }
}

/// <summary>Runs <see cref="PaletteGrowthTests"/> with no other test beside it.</summary>
[CollectionDefinition(PaletteGrowthTests.Alone, DisableParallelization = true)]
public sealed class PaletteGrowthCollection
{
}
