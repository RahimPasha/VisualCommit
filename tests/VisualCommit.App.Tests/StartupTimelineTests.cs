using System.ComponentModel;
using System.Globalization;
using VisualCommit.App.Services;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The start-up timeline's lines (<see cref="StartupTimeline"/>, D436, D438): each once per process,
/// at Info, with the whole milliseconds since the process started by the timeline's clock; the
/// first-rows line only after a restore of one tab or more. No session: a clock the test moves
/// (<see cref="ManualTimeProvider"/>) and a log it reads (<see cref="RecordingLog"/>).
/// </summary>
public class StartupTimelineTests
{
    [Fact]
    public void Each_line_is_written_at_Info_with_the_whole_milliseconds_since_the_process_started()
    {
        var (timeline, clock, log) = Started();

        clock.Advance(TimeSpan.FromTicks(12_349_000));
        timeline.WindowOpened();
        clock.Advance(TimeSpan.FromTicks(7_651_000));
        timeline.GitFound();
        timeline.TabsRestored(1);
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        timeline.FirstRows("graph");

        Assert.Equal(
            [
                (LogLevel.Info, "Start-up: window opened after 1234 ms since the process started."),
                (LogLevel.Info, "Start-up: Git found after 2000 ms since the process started."),
                (LogLevel.Info, "Start-up: first graph rows of graph after 3000 ms since the process started (1 restored tab)."),
            ],
            log.Entries.Select(entry => (entry.Level, entry.Message)));
    }

    [Fact]
    public void A_second_call_of_each_writes_nothing()
    {
        var (timeline, clock, log) = Started();
        timeline.TabsRestored(2);

        timeline.WindowOpened();
        timeline.GitFound();
        timeline.FirstRows("graph");
        clock.Advance(TimeSpan.FromSeconds(5));
        timeline.WindowOpened();
        timeline.GitFound();
        timeline.FirstRows("graph");
        timeline.FirstRows("linear");

        Assert.Equal(
            [
                "Start-up: window opened after 0 ms since the process started.",
                "Start-up: Git found after 0 ms since the process started.",
                "Start-up: first graph rows of graph after 0 ms since the process started (2 restored tabs).",
            ],
            log.Messages);
    }

    [Theory]
    [InlineData(1, "(1 restored tab)")]
    [InlineData(2, "(2 restored tabs)")]
    [InlineData(10, "(10 restored tabs)")]
    public void The_first_rows_line_counts_the_restored_tabs(int restored, string count)
    {
        var (timeline, clock, log) = Started();
        timeline.TabsRestored(restored);
        clock.Advance(TimeSpan.FromMilliseconds(4321));

        timeline.FirstRows("large-history-v1");

        Assert.Equal($"Start-up: first graph rows of large-history-v1 after 4321 ms since the process started {count}.", Assert.Single(log.Messages));
    }

    [Fact]
    public void No_first_rows_line_comes_without_TabsRestored_or_after_TabsRestored_of_0()
    {
        // Without TabsRestored: the first call is the one, so a restore told later changes nothing.
        var (without, _, withoutLog) = Started();
        without.FirstRows("graph");
        without.TabsRestored(1);
        without.FirstRows("graph");
        Assert.Empty(withoutLog.Messages);

        // After TabsRestored(0), the user's own open: the first TabsRestored wins.
        var (none, _, noneLog) = Started();
        none.TabsRestored(0);
        none.TabsRestored(2);
        none.FirstRows("graph");
        Assert.Empty(noneLog.Messages);

        // The first TabsRestored wins over a later one.
        var (first, _, firstLog) = Started();
        first.TabsRestored(3);
        first.TabsRestored(5);
        first.FirstRows("graph");
        Assert.Equal("Start-up: first graph rows of graph after 0 ms since the process started (3 restored tabs).", Assert.Single(firstLog.Messages));
    }

    [Fact]
    public void A_clock_before_the_process_start_gives_0()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingLog();
        var timeline = new StartupTimeline(clock.GetUtcNow() + TimeSpan.FromSeconds(5), log, clock);

        timeline.WindowOpened();

        Assert.Equal("Start-up: window opened after 0 ms since the process started.", Assert.Single(log.Messages));
    }

    [Fact]
    public async Task A_hundred_calls_of_each_from_parallel_tasks_write_one_line_each()
    {
        var (timeline, clock, log) = Started();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        timeline.TabsRestored(2);

        using var start = new ManualResetEventSlim();
        var calls = Enumerable.Range(0, 100).SelectMany(i => new Action[]
        {
            timeline.WindowOpened,
            timeline.GitFound,
            () => timeline.TabsRestored(7),
            () => timeline.FirstRows("graph"),
        })
        .Select(call => Task.Run(
            () =>
            {
                start.Wait(TestContext.Current.CancellationToken);
                call();
            },
            TestContext.Current.CancellationToken))
        .ToList();
        start.Set();
        await Task.WhenAll(calls);

        Assert.Equal(
            [
                "Start-up: Git found after 250 ms since the process started.",
                "Start-up: first graph rows of graph after 250 ms since the process started (2 restored tabs).",
                "Start-up: window opened after 250 ms since the process started.",
            ],
            log.Messages.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Under_de_DE_the_milliseconds_have_no_group_separator()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var (timeline, clock, log) = Started();
            clock.Advance(TimeSpan.FromTicks(123_456_000));

            timeline.WindowOpened();

            Assert.Equal("Start-up: window opened after 12345 ms since the process started.", Assert.Single(log.Messages));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void FromProcess_gives_a_timeline_whose_window_line_has_N_of_0_or_more()
    {
        var log = new RecordingLog();

        var timeline = StartupTimeline.FromProcess(log);
        timeline.WindowOpened();

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Matches(@"^Start-up: window opened after \d+ ms since the process started\.$", entry.Message);
    }

    [Theory]
    [InlineData("InvalidOperationException")]
    [InlineData("Win32Exception")]
    [InlineData("NotSupportedException")]
    public void A_start_time_that_cannot_be_read_gives_the_null_timeline_and_one_Warning(string failure)
    {
        var log = new RecordingLog();
        Exception thrown = failure switch
        {
            "InvalidOperationException" => new InvalidOperationException("No process is associated with this object."),
            "Win32Exception" => new Win32Exception(5),
            _ => new NotSupportedException("Not on this platform."),
        };

        var timeline = StartupTimeline.FromProcess(log, () => throw thrown);
        timeline.WindowOpened();
        timeline.GitFound();
        timeline.TabsRestored(1);
        timeline.FirstRows("graph");

        Assert.Same(NullStartupTimeline.Instance, timeline);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(
            (LogLevel.Warning, "Start-up: the process's start time could not be read, so no start-up times are logged."),
            (entry.Level, entry.Message));
    }

    /// <summary>A timeline whose process started at the clock's present, its clock and its log.</summary>
    private static (StartupTimeline Timeline, ManualTimeProvider Clock, RecordingLog Log) Started()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingLog();
        return (new StartupTimeline(clock.GetUtcNow(), log, clock), clock, log);
    }
}
