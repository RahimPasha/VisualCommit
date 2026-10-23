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
    /// that may allocate <paramref name="budget"/> bytes, and asserts that no collection ran.
    /// </summary>
    public static T WithoutCollection<T>(long budget, Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.True(GC.TryStartNoGCRegion(budget), "The region without garbage collection did not start.");
        try
        {
            var result = work();
            Assert.True(GCSettings.LatencyMode == GCLatencyMode.NoGCRegion, "A garbage collection ran during the measure: the budget is too small.");
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
