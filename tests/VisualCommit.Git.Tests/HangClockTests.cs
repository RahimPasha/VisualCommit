using System.Diagnostics;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The hang watchdog's rules (<see cref="HangClock"/>, D35): a test over the limit, a run that
/// stands still, and the time a test waits for a shared preparation, which does not count.
/// </summary>
public class HangClockTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(3);

    private TimeSpan _now = TimeSpan.FromSeconds(10);

    [Fact]
    public void A_test_over_the_limit_stops_the_run_and_is_named_with_its_start()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t1", "Slow test");
        Advance(179);
        Assert.Null(clock.Check(Limit));

        Advance(2);
        var message = clock.Check(Limit);
        Assert.NotNull(message);
        Assert.Contains("HANG WATCHDOG", message, StringComparison.Ordinal);
        Assert.Contains("Running for 181 seconds (started 10 seconds into the run; 181 seconds count", message, StringComparison.Ordinal);
        Assert.Contains("Slow test", message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_wait_for_a_shared_template_does_not_count_against_the_test()
    {
        // Under load: the test waits 150 s for the template another test is building, then runs 60 s itself.
        var clock = new HangClock(() => _now);
        clock.Started("first", "Builds the template");
        clock.Started("second", "Waits for it");
        var built = clock.Preparing("first", "the ops scenario's template");
        var waited = clock.Preparing("second", "the ops scenario's template");
        Advance(150);
        Assert.Null(clock.Check(Limit));
        built.Dispose();
        waited.Dispose();
        Advance(60);

        Assert.Null(clock.Check(Limit));
        Assert.Equal(TimeSpan.FromSeconds(60), clock.CountedTime("first"));
        Assert.Equal(TimeSpan.FromSeconds(60), clock.CountedTime("second"));

        // The rest of the limit still holds.
        Advance(121);
        Assert.Contains("Running for 331 seconds (started 10 seconds into the run; 181 seconds count", clock.Check(Limit), StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_waits_of_one_test_are_counted_out_once()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t", "Waits for two templates at once");
        var graph = clock.Preparing("t", "the graph scenario's template");
        Advance(20);
        var ops = clock.Preparing("t", "the ops scenario's template");
        Advance(30);
        graph.Dispose();
        Advance(10);
        ops.Dispose();
        ops.Dispose();
        Advance(5);

        Assert.Equal(TimeSpan.FromSeconds(5), clock.CountedTime("t"));
    }

    [Fact]
    public void A_preparation_that_hangs_stops_the_run_and_is_named()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t", "Waits for a build that hangs");
        using var build = clock.Preparing("t", "the sync scenario's template");
        Advance(180);
        Assert.Null(clock.Check(Limit));

        Advance(1);
        var message = clock.Check(Limit);
        Assert.NotNull(message);
        Assert.Contains("Waited for since 181 seconds: the sync scenario's template", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wait_for_a_turn_in_a_shared_queue_does_not_count_and_is_left_to_the_standstill_rule()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t", "Waits for the UI thread");
        var turn = clock.Preparing("t", "its turn on the headless UI thread", heldToLimit: false);
        for (var step = 0; step < 4; step++)
        {
            // Other tests start and finish meanwhile: the queue moves.
            Advance(60);
            clock.Started("other" + step, "Other test");
            clock.Finished("other" + step);
            Assert.Null(clock.Check(Limit));
        }

        turn.Dispose();
        Advance(10);
        Assert.Equal(TimeSpan.FromSeconds(10), clock.CountedTime("t"));

        // A queue that stops moving is still caught.
        Advance(171);
        Assert.Contains("Last event, 181 seconds ago", clock.Check(Limit), StringComparison.Ordinal);
    }

    [Fact]
    public void A_wait_with_a_limit_of_its_own_is_held_to_it_and_holds_the_standstill_rule_back_meanwhile()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t", "Runs a slow program");
        var wait = clock.Preparing("t", "git mergetool --tool-help", limit: TimeSpan.FromMinutes(10));

        // Past the run's limit, with nothing else happening: the wait is within its own limit.
        Advance(400);
        Assert.Null(clock.Check(Limit));
        Assert.Equal(TimeSpan.Zero, clock.CountedTime("t"));

        // Past its own limit, it is caught and named.
        Advance(201);
        var message = clock.Check(Limit);
        Assert.NotNull(message);
        Assert.Contains("Waited for since 601 seconds: git mergetool --tool-help", message, StringComparison.Ordinal);

        // Once it has ended, the run's rules are the run's again.
        wait.Dispose();
        Advance(10);
        Assert.Null(clock.Check(Limit));
        Advance(171);
        Assert.NotNull(clock.Check(Limit));
    }

    [Fact]
    public void A_run_in_which_nothing_happens_stops_even_without_a_test()
    {
        var clock = new HangClock(() => _now);
        clock.Started("t", "Quick test");
        Advance(1);
        clock.Finished("t");
        Advance(180);
        Assert.Null(clock.Check(Limit));

        Advance(1);
        var message = clock.Check(Limit);
        Assert.NotNull(message);
        Assert.Contains("Last event, 181 seconds ago: finished Quick test", message, StringComparison.Ordinal);
        Assert.Contains("No test is running", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_watchdog_does_not_count_a_wait_for_a_shared_preparation_in_a_real_test()
    {
        var countedBefore = HangWatchdogAttribute.CountedTimeOfCurrentTest();
        Assert.NotNull(countedBefore);
        var clock = Stopwatch.StartNew();
        var preparation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = HangWatchdogAttribute.SharedPreparationAsync("a slow preparation", preparation.Task);
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        preparation.SetResult(42);

        Assert.Equal(42, await waiting);

        var counted = HangWatchdogAttribute.CountedTimeOfCurrentTest() - countedBefore;
        var elapsed = clock.Elapsed;
        Assert.NotNull(counted);
        Assert.True(counted.Value <= elapsed - TimeSpan.FromMilliseconds(550), $"Counted {counted.Value.TotalMilliseconds:F0} ms of {elapsed.TotalMilliseconds:F0} ms.");
    }

    private void Advance(int seconds) => _now += TimeSpan.FromSeconds(seconds);
}
