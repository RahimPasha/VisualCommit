using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The performance series' arithmetic (<see cref="PerformanceSeries"/>, docs/phase-8a-design.md 10.4):
/// pure, with no window and no app. <see cref="PerformanceSeries.ParseRuns"/> is called with the value
/// and never through the variable: this project runs every class in one process, so a variable set
/// here would make the gate's pass run the series.
/// </summary>
public class PerformanceSeriesTests
{
    [Fact]
    public void The_median_is_the_middle_value_of_an_odd_count_and_the_mean_of_the_two_middle_values_of_an_even_one()
    {
        Assert.Equal(1513, PerformanceSeries.Median([1600, 1513, 1402]));
        Assert.Equal(7, PerformanceSeries.Median([7]));
        Assert.Equal(1457.5, PerformanceSeries.Median([1600, 1402, 1513, 1300]));
        Assert.Equal(37, PerformanceSeries.Median([36, 38]));
        Assert.Throws<ArgumentException>(() => PerformanceSeries.Median([]));
    }

    [Fact]
    public void The_range_is_the_lowest_and_the_highest_value()
    {
        Assert.Equal((1402.0, 1600.0), PerformanceSeries.Range([1513, 1600, 1402, 1540, 1499]));
        Assert.Equal((412.0, 412.0), PerformanceSeries.Range([412]));
        Assert.Throws<ArgumentException>(() => PerformanceSeries.Range([]));
    }

    [Theory]
    [InlineData(1513, PerformanceSeries.TimeStep, 1900)]
    [InlineData(412, PerformanceSeries.MemoryStep, 520)]
    [InlineData(37, PerformanceSeries.PullReadsStep, 50)]
    [InlineData(400, PerformanceSeries.TimeStep, 500)]
    [InlineData(1440.4, PerformanceSeries.TimeStep, 1900)]
    [InlineData(0, PerformanceSeries.TimeStep, 0)]
    public void A_bound_is_the_slowest_run_times_1_25_rounded_up_to_its_step(double slowest, int step, long bound)
    {
        Assert.Equal(bound, PerformanceSeries.Bound(slowest, step));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("5", 5)]
    [InlineData("1", 1)]
    [InlineData("20", 20)]
    public void ParseRuns_takes_a_whole_number_from_1_to_20_and_null_for_unset(string? value, int? runs)
    {
        Assert.Equal(runs, PerformanceSeries.ParseRuns(value));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("x")]
    [InlineData("-5")]
    [InlineData(" 5")]
    [InlineData("")]
    public void ParseRuns_refuses_any_other_value_naming_the_variable(string value)
    {
        var refused = Assert.Throws<ArgumentException>(() => PerformanceSeries.ParseRuns(value));
        Assert.StartsWith($"VISUALCOMMIT_PERF_RUNS is \"{value}\": it takes a whole number of runs per measure from 1 to 20, or is left unset.", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_line_gives_the_median_the_range_the_runs_and_the_bound()
    {
        double[] times = [1513, 1600, 1402.24, 1540, 1499];

        Assert.Equal("M2: median 1513 ms, range 1402.2 to 1600 ms, 5 runs; bound not set", PerformanceSeries.Summary("M2", "ms", times, null));
        Assert.Equal("M2: median 1513 ms, range 1402.2 to 1600 ms, 5 runs; bound 1600 ms, held", PerformanceSeries.Summary("M2", "ms", times, 1600));
        Assert.Equal("M2: median 1513 ms, range 1402.2 to 1600 ms, 5 runs; bound 1500 ms, not held", PerformanceSeries.Summary("M2", "ms", times, 1500));
        Assert.Equal("M4 after the load: median 412.5 MB, range 412.5 to 412.5 MB, 1 run; bound 520 MB, held", PerformanceSeries.Summary("M4 after the load", "MB", [412.5], 520));
        Assert.Equal(412.5, PerformanceSeries.Megabytes(432_537_600));
    }
}
