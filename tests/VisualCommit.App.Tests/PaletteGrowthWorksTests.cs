using System.Runtime.InteropServices;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the palette's build test keeps of its measures (<see cref="PaletteGrowthWorks"/>) and what
/// a failed measure leaves: the record's line and the test's failure, each with every work measured
/// so far, the failed one named. The measures here are made up and start no region without
/// collection, so these tests run beside the others.
/// </summary>
public class PaletteGrowthWorksTests
{
    private const string Small = "small";
    private const string Large = "large";

    private static readonly string Runtime = $"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.RuntimeIdentifier}";

    [Fact]
    public void A_run_measures_small_then_large_four_times_and_keeps_the_best_of_the_three_rounds()
    {
        var contexts = new List<string>();
        var records = new List<string>();

        var works = PaletteGrowthWorks.Measure(Small, Large, Measures(failAt: null, contexts), Recorder(records, null));

        Assert.Equal([Small, Large, Small, Large, Small, Large, Small, Large], contexts);
        Assert.Empty(records);
        // The warm-up's 100 and 1001 are kept but not counted among the best.
        Assert.Equal(102UL, works.BestSmall);
        Assert.Equal(1003UL, works.BestLarge);
        Assert.Equal(4.0, works.FastestSmall);
        Assert.Equal(3.0, works.FastestLarge);
        Assert.Equal((20, 70), (works.SmallCount, works.LargeCount));
        Assert.Equal($"the work at 2,500 and 10,000 tags: warm-up 100/1001, rounds 102/1003, 104/1005, 106/1007; items 20 and 70; {Runtime}", works.Describe());
    }

    [Fact]
    public void A_large_measure_that_fails_in_a_round_leaves_the_small_measure_of_that_round_in_the_record_and_the_failure()
    {
        // The fifth measure is the 10,000 tags' of the second round: its 2,500 tags' work, 104, is kept.
        var records = new List<string>();
        const string data = "the work at 2,500 and 10,000 tags: warm-up 100/1001, rounds 102/1003, 104/failed; items 20 and 70";

        var thrown = Record.Exception(() => PaletteGrowthWorks.Measure(Small, Large, Measures(failAt: 5, []), Recorder(records, null)));

        Assert.Equal([$"the measure failed (A collection ran during measure 5.); {data}; {Runtime}"], records);
        var failure = Assert.IsType<XunitException>(thrown);
        Assert.StartsWith("A collection ran during measure 5.", failure.Message, StringComparison.Ordinal);
        Assert.EndsWith($"\nThe data: {data}; {Runtime}.", failure.Message, StringComparison.Ordinal);
        Assert.IsType<TrueException>(failure.InnerException);
    }

    [Fact]
    public void A_large_warm_up_that_fails_names_the_small_warm_up_s_work_as_the_small_one()
    {
        var records = new List<string>();
        const string data = "the work at 2,500 and 10,000 tags: warm-up 100/failed, rounds none; items 20 and not counted";

        var thrown = Record.Exception(() => PaletteGrowthWorks.Measure(Small, Large, Measures(failAt: 1, []), Recorder(records, null)));

        Assert.Equal([$"the measure failed (A collection ran during measure 1.); {data}; {Runtime}"], records);
        var failure = Assert.IsType<XunitException>(thrown);
        Assert.EndsWith($"\nThe data: {data}; {Runtime}.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_small_warm_up_that_fails_says_so_and_counts_nothing()
    {
        var records = new List<string>();
        const string data = "the work at 2,500 and 10,000 tags: warm-up failed/-, rounds none; items not counted";

        var thrown = Record.Exception(() => PaletteGrowthWorks.Measure(Small, Large, Measures(failAt: 0, []), Recorder(records, null)));

        Assert.Equal([$"the measure failed (A collection ran during measure 0.); {data}; {Runtime}"], records);
        var failure = Assert.IsType<XunitException>(thrown);
        Assert.EndsWith($"\nThe data: {data}; {Runtime}.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_measure_whose_record_was_not_written_gives_the_data_and_the_note_in_its_failure()
    {
        // The data then reaches no file: the failure is the only place it is.
        const string note = "The record x was not written (IOException: the disk is full).";
        const string data = "the work at 2,500 and 10,000 tags: warm-up 100/1001, rounds 102/1003, 104/1005, 106/failed; items 20 and 70";

        var failure = Assert.Throws<XunitException>(() => PaletteGrowthWorks.Measure(Small, Large, Measures(failAt: 7, []), Recorder([], note)));

        Assert.StartsWith("A collection ran during measure 7.", failure.Message, StringComparison.Ordinal);
        Assert.EndsWith($"\nThe data: {data}; {Runtime}.\n{note}", failure.Message, StringComparison.Ordinal);
        Assert.IsType<TrueException>(failure.InnerException);
    }

    /// <summary>
    /// Made-up measures: the call with index i returns the work 100 + i for the small context and
    /// 1000 + i for the large, 10 - i milliseconds and 20 or 70 items; the call <paramref name="failAt"/>
    /// fails as a measure whose region ended does, with a message of several lines.
    /// </summary>
    private static Func<string, (ulong Work, double Milliseconds, int Items)> Measures(int? failAt, List<string> contexts)
    {
        var calls = 0;
        return context =>
        {
            var index = calls++;
            contexts.Add(context);
            Assert.True(index != failAt, $"A collection ran during measure {index}.");
            return context == Small ? ((ulong)(100 + index), 10.0 - index, 20) : ((ulong)(1000 + index), 10.0 - index, 70);
        };
    }

    /// <summary>A record that keeps each line in <paramref name="lines"/> and returns <paramref name="note"/>, null for a line written.</summary>
    private static Func<string, string?> Recorder(List<string> lines, string? note) => line =>
    {
        lines.Add(line);
        return note;
    };
}
