using System.Globalization;
using System.Runtime;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The measures of work that must grow in step with its input (the palette's matcher and build,
/// docs/phase-6-design.md 5.12; the diff view's build and drawing): each runs in a region without
/// garbage collection, which the whole process shares, so their tests run in
/// <see cref="GrowthCollection"/>, alone in the process. The collector's work follows the live
/// objects and the processor's caches, not the work measured: with it a ratio of a thread's own
/// work grew 29 times for 10 times the items on CI's Windows runner (run 39936260636).
/// </summary>
internal static class GrowthMeasures
{
    /// <summary>
    /// Runs <paramref name="work"/> after a full collection in a region of no garbage collection
    /// that may allocate <paramref name="budget"/> bytes, and asserts that no collection ran. The
    /// region also ends when any thread of the process asks for a collection, or under memory
    /// pressure, with the budget far from used: the failure blames the budget only when the process
    /// allocated at least the budget during the measure, and otherwise says what it allocated and
    /// the machine's memory load (<see cref="CollectionRan"/>).
    /// </summary>
    public static T WithoutCollection<T>(long budget, Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        Assert.True(GC.TryStartNoGCRegion(budget), "The region without garbage collection did not start.");
        try
        {
            var result = work();
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            var stillInRegion = GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;
            Assert.True(stillInRegion, stillInRegion ? null : CollectionRan(allocated, budget, GC.GetGCMemoryInfo()));
            return result;
        }
        finally
        {
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }
        }
    }

    /// <summary>
    /// The failure of a measure whose region a collection ended: the budget is blamed only when the
    /// process allocated at least <paramref name="budget"/> bytes during the measure (every thread's
    /// allocations count against the region's budget); otherwise another thread's collection or
    /// memory pressure ended it, and the message gives the memory load and the high-load threshold
    /// of the last collection (<paramref name="memory"/>).
    /// </summary>
    internal static string CollectionRan(long allocated, long budget, GCMemoryInfo memory) =>
        allocated >= budget
            ? string.Create(CultureInfo.InvariantCulture, $"A garbage collection ran during the measure, which allocated {allocated:N0} bytes of the budget's {budget:N0}: the budget is too small.")
            : string.Create(CultureInfo.InvariantCulture, $"A garbage collection ran during the measure with {allocated:N0} bytes of the budget's {budget:N0} allocated: another thread's collection or memory pressure ended the region (memory load {memory.MemoryLoadBytes:N0} bytes, the high-load threshold {memory.HighMemoryLoadThresholdBytes:N0} bytes).");
}

/// <summary>
/// Runs the growth measures' tests (<see cref="GrowthMeasures"/>) with no other test beside them:
/// xunit runs a collection without parallelization after the parallel ones. Another test process
/// (CI's Git.Tests) may still run beside it; the measures count the thread's own work.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GrowthCollection
{
    public const string Name = "Growth measures, alone";
}
