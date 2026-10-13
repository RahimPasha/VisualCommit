using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="ManualTimeProvider"/>, the clock the tests move by hand: its time, its timers, and
/// the lock it never holds while a callback runs.
/// </summary>
public class ManualTimeProviderTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void The_time_moves_only_when_advanced()
    {
        var start = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var clock = new ManualTimeProvider(start);
        var before = clock.GetTimestamp();
        Assert.Equal(start, clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(TimeSpan.FromMilliseconds(1500), clock.GetElapsedTime(before));
        Assert.Equal(start.AddMilliseconds(1500), clock.GetUtcNow());
        Assert.Equal(TimeSpan.FromMilliseconds(1500), clock.Elapsed);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void A_timer_fires_once_its_due_time_is_reached_with_the_clock_at_that_time_on_the_advancing_thread()
    {
        var clock = new ManualTimeProvider();
        var fired = new List<(TimeSpan At, int Thread, object? State)>();
        using var timer = clock.CreateTimer(state => fired.Add((clock.Elapsed, Environment.CurrentManagedThreadId, state)), "state", Second, Timeout.InfiniteTimeSpan);

        clock.Advance(Second - TimeSpan.FromTicks(1));
        Assert.Empty(fired);

        clock.Advance(Second);
        var only = Assert.Single(fired);
        Assert.Equal(Second, only.At);
        Assert.Equal(Environment.CurrentManagedThreadId, only.Thread);
        Assert.Equal("state", only.State);
        Assert.Equal(Second * 2 - TimeSpan.FromTicks(1), clock.Elapsed);
        Assert.Equal(0, clock.ArmedTimers);
    }

    [Fact]
    public void Timers_fire_in_the_order_they_fall_due_and_one_armed_again_by_its_callback_fires_again()
    {
        var clock = new ManualTimeProvider();
        var fired = new List<(string Name, TimeSpan At)>();
        ITimer? again = null;
        again = clock.CreateTimer(
            _ =>
            {
                fired.Add(("again", clock.Elapsed));
                if (fired.Count(entry => entry.Name == "again") < 3)
                {
                    again!.Change(Second, Timeout.InfiniteTimeSpan);
                }
            },
            null,
            Second,
            Timeout.InfiniteTimeSpan);
        using var late = clock.CreateTimer(_ => fired.Add(("late", clock.Elapsed)), null, Second * 2, Timeout.InfiniteTimeSpan);
        using var tie = clock.CreateTimer(_ => fired.Add(("tie", clock.Elapsed)), null, Second, Timeout.InfiniteTimeSpan);
        using var periodic = clock.CreateTimer(_ => fired.Add(("periodic", clock.Elapsed)), null, Second * 1.5, Second * 1.5);

        clock.Advance(Second * 5);

        // Due at the same time, the one armed first fires first: "again" before "tie" at 1 s,
        // "late" (armed at the start) before "again" (armed again at 1 s) at 2 s.
        Assert.Equal(
            [
                ("again", Second), ("tie", Second), ("periodic", Second * 1.5), ("late", Second * 2), ("again", Second * 2),
                ("periodic", Second * 3), ("again", Second * 3), ("periodic", Second * 4.5),
            ],
            fired);
        again.Dispose();
    }

    [Fact]
    public void A_timer_armed_with_no_wait_fires_at_the_next_advance_and_a_disposed_or_disarmed_one_never()
    {
        var clock = new ManualTimeProvider();
        var fired = 0;
        var timer = clock.CreateTimer(_ => fired++, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        clock.Advance(Second * 10);
        Assert.Equal(0, fired);

        Assert.True(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
        clock.Advance(TimeSpan.Zero);
        Assert.Equal(1, fired);

        Assert.True(timer.Change(Second, Timeout.InfiniteTimeSpan));
        Assert.True(timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
        clock.Advance(Second * 10);
        Assert.Equal(1, fired);

        Assert.True(timer.Change(Second, Timeout.InfiniteTimeSpan));
        timer.Dispose();
        Assert.False(timer.Change(Second, Timeout.InfiniteTimeSpan));
        clock.Advance(Second * 10);
        Assert.Equal(1, fired);
        Assert.Equal(0, clock.ArmedTimers);
    }

    [Fact]
    public async Task A_callback_may_wait_for_a_lock_whose_holder_arms_the_timer()
    {
        // The code under test arms its timer while it holds its own lock, and its callback takes
        // that lock: the clock must not hold its own lock while the callback runs.
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider();
        var codeLock = new Lock();
        using var holding = new ManualResetEventSlim();
        using var callbackRunning = new ManualResetEventSlim();
        var armedByHolder = false;
        var timer = clock.CreateTimer(
            _ =>
            {
                callbackRunning.Set();
                lock (codeLock)
                {
                }
            },
            null,
            Second,
            Timeout.InfiniteTimeSpan);

        var holder = Task.Run(
            () =>
            {
                lock (codeLock)
                {
                    holding.Set();
                    if (callbackRunning.Wait(TimeSpan.FromSeconds(10), token))
                    {
                        armedByHolder = timer.Change(Second, Timeout.InfiniteTimeSpan);
                    }
                }
            },
            token);
        Assert.True(holding.Wait(TimeSpan.FromSeconds(10), token), "The holder did not take the lock.");

        var advancing = Task.Run(() => clock.Advance(Second), token);

        await Task.WhenAll(holder, advancing).WaitAsync(TimeSpan.FromSeconds(30), token);
        Assert.True(armedByHolder, "The holder could not arm the timer while the callback waited.");
        Assert.Equal(1, clock.ArmedTimers);
        timer.Dispose();
    }
}
