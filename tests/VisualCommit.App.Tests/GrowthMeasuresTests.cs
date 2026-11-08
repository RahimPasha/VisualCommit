using System.Runtime;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.App.Tests;

/// <summary>
/// The growth measures' own harness (<see cref="GrowthMeasures"/>): what its failure says when a
/// garbage collection ends the region without collection. It starts and ends such regions, which
/// the whole process shares, so it runs in <see cref="GrowthCollection"/>, alone in the process.
/// </summary>
[Collection(GrowthCollection.Name)]
public class GrowthMeasuresTests
{
    /// <summary>Keeps each allocation of <see cref="Allocate"/> reachable from the heap, so that no allocation is taken off it.</summary>
    private static object? _sink;

    [Fact]
    public void A_measure_within_its_budget_returns_the_work_s_result_and_ends_the_region()
    {
        var result = GrowthMeasures.WithoutCollection(64L * 1024 * 1024, () =>
        {
            Allocate(1024 * 1024);
            return 42;
        });

        Assert.Equal(42, result);
        Assert.NotEqual(GCLatencyMode.NoGCRegion, GCSettings.LatencyMode);
    }

    [Fact]
    public void A_measure_that_allocates_past_its_budget_blames_the_budget()
    {
        // 16 MB of budget, 64 MB allocated in kilobyte arrays: the collector ends the region once the
        // small objects' heap has taken the budget.
        const long budget = 16L * 1024 * 1024;
        var failure = Assert.Throws<TrueException>(() => GrowthMeasures.WithoutCollection(budget, () =>
        {
            Allocate(4 * budget);
            return 0;
        }));

        Assert.Contains("the budget is too small", failure.Message, StringComparison.Ordinal);
        Assert.Contains("of the budget's 16,777,216", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("another thread", failure.Message, StringComparison.Ordinal);
        Assert.NotEqual(GCLatencyMode.NoGCRegion, GCSettings.LatencyMode);
    }

    [Fact]
    public void A_collection_from_another_thread_is_named_with_what_the_measure_allocated_and_the_memory_load()
    {
        // A collection that another thread asks for ends the region, whatever is left of the budget:
        // the failure says so, with the facts, and does not send the reader to raise the budget.
        const long budget = 64L * 1024 * 1024;
        var failure = Assert.Throws<TrueException>(() => GrowthMeasures.WithoutCollection(budget, () =>
        {
            Allocate(1024 * 1024);
            var collector = new Thread(() => GC.Collect());
            collector.Start();
            collector.Join();
            return 0;
        }));

        Assert.DoesNotContain("the budget is too small", failure.Message, StringComparison.Ordinal);
        Assert.Contains("of the budget's 67,108,864 allocated", failure.Message, StringComparison.Ordinal);
        Assert.Contains("another thread's collection or memory pressure", failure.Message, StringComparison.Ordinal);
        Assert.Contains("memory load", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the high-load threshold", failure.Message, StringComparison.Ordinal);
        Assert.NotEqual(GCLatencyMode.NoGCRegion, GCSettings.LatencyMode);
    }

    /// <summary>Allocates about <paramref name="bytes"/> in arrays of a kilobyte, each left for the collector.</summary>
    private static void Allocate(long bytes)
    {
        for (long done = 0; done < bytes; done += 1024)
        {
            Volatile.Write(ref _sink, new byte[1024]);
        }

        Volatile.Write(ref _sink, null);
    }
}
