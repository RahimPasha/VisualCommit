using System.Globalization;
using System.Text;

namespace VisualCommit.Testing;

/// <summary>
/// What the hang watchdog (<see cref="HangWatchdogAttribute"/>) goes by: the tests that are
/// running, the shared preparations that are under way, and the last thing that happened, each
/// with its time on one clock. Separate from the watchdog so that its rules can be tested with a
/// clock a test moves by hand.
/// <para>
/// A test's counted time is the time since it started less the time it spent waiting for a
/// shared preparation (<see cref="Preparing"/>): a scenario template or the 100k-commit repo,
/// which the first test of a process that needs it builds and the others wait for. That wait is
/// the cost of the whole process, not of the test that happens to come first, and under load it
/// can take a good part of the limit. A preparation is held to the limit itself, so a build that
/// hangs is still caught.
/// </para>
/// <para>
/// A wait may name a limit of its own in place of the run's (a program that under the default
/// run's load is known to take longer than the run's limit, such as <c>git mergetool
/// --tool-help</c>, docs/phase-6-design.md 8.13). It is held to that limit instead, and while it
/// is within it the "nothing happens" rule waits for it: the wait itself is the run's progress,
/// and its own limit still catches it if it hangs.
/// </para>
/// </summary>
/// <param name="now">The clock: the time since some fixed start, which only moves forward.</param>
public sealed class HangClock(Func<TimeSpan> now)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TestEntry> _tests = [];
    private readonly Dictionary<long, (string What, TimeSpan StartedAt, TimeSpan? Limit)> _preparations = [];
    private long _nextPreparation;
    private TimeSpan _lastEventAt = now();
    private string _lastEvent = "no test has started";

    /// <summary>Notes that the test <paramref name="test"/> (its unique id) started now.</summary>
    public void Started(string test, string name)
    {
        lock (_gate)
        {
            _tests[test] = new TestEntry(name, now());
            NoteLocked("started " + name);
        }
    }

    /// <summary>Notes that the test <paramref name="test"/> finished now.</summary>
    public void Finished(string test)
    {
        lock (_gate)
        {
            var name = _tests.Remove(test, out var entry) ? entry.Name : test;
            NoteLocked("finished " + name);
        }
    }

    /// <summary>
    /// Notes that <paramref name="test"/> (null outside a test, as in a class fixture) waits from
    /// now for the shared preparation <paramref name="what"/>, until the result is disposed. The
    /// wait does not count against the test; the preparation counts against the limit on its
    /// own, unless <paramref name="heldToLimit"/> is false: a wait in a queue that moves as other
    /// tests finish (the headless UI thread), which the "nothing happens" rule watches already.
    /// With <paramref name="limit"/>, the wait is held to that limit instead of the run's, and the
    /// "nothing happens" rule waits while it is within it. Waits of one test may overlap: only the
    /// time in which it waits for none counts.
    /// </summary>
    public IDisposable Preparing(string? test, string what, bool heldToLimit = true, TimeSpan? limit = null)
    {
        lock (_gate)
        {
            var id = _nextPreparation++;
            var at = now();
            if (heldToLimit || limit is not null)
            {
                _preparations[id] = (what, at, limit);
            }

            if (test is not null && _tests.TryGetValue(test, out var entry) && entry.Waits++ == 0)
            {
                entry.WaitStartedAt = at;
            }

            NoteLocked("waiting for " + what);
            return new Preparation(this, id, test, what);
        }
    }

    /// <summary>
    /// The time <paramref name="test"/> has run so far, less its waits for shared preparations;
    /// null when it is not running.
    /// </summary>
    public TimeSpan? CountedTime(string test)
    {
        lock (_gate)
        {
            return _tests.TryGetValue(test, out var entry) ? Counted(entry, now()) : null;
        }
    }

    /// <summary>
    /// Null while all is well. Otherwise the watchdog's message: a test whose counted time, or a
    /// preparation whose time, is over <paramref name="limit"/>, or a run in which nothing
    /// started, finished or was waited for in that long.
    /// </summary>
    public string? Check(TimeSpan limit)
    {
        lock (_gate)
        {
            var at = now();
            var overdue = _tests.Values.Any(test => Counted(test, at) > limit);
            var stuckPreparation = _preparations.Values.Any(preparation => at - preparation.StartedAt > (preparation.Limit ?? limit));
            var waitingWithinOwnLimit = _preparations.Values.Any(preparation => preparation.Limit is { } own && at - preparation.StartedAt <= own);
            var standingStill = at - _lastEventAt > limit && !waitingWithinOwnLimit;
            if (!overdue && !stuckPreparation && !standingStill)
            {
                return null;
            }

            var message = new StringBuilder()
                .AppendLine()
                .AppendLine(CultureInfo.InvariantCulture, $"HANG WATCHDOG: the test run is stuck (limit {limit.TotalSeconds:F0} seconds). Stopping the test process.")
                .AppendLine(CultureInfo.InvariantCulture, $"  {at.TotalSeconds:F0} seconds into the run. Last event, {(at - _lastEventAt).TotalSeconds:F0} seconds ago: {_lastEvent}");
            if (_tests.Count == 0)
            {
                message.AppendLine("  No test is running: the run is stuck between tests or after the last one.");
            }

            foreach (var test in _tests.Values.OrderBy(test => test.StartedAt))
            {
                var counted = Counted(test, at);
                message.AppendLine(CultureInfo.InvariantCulture, $"  Running for {(at - test.StartedAt).TotalSeconds:F0} seconds (started {test.StartedAt.TotalSeconds:F0} seconds into the run; {counted.TotalSeconds:F0} seconds count, the rest waited for shared preparations): {test.Name}");
            }

            foreach (var preparation in _preparations.Values.OrderBy(preparation => preparation.StartedAt))
            {
                message.AppendLine(CultureInfo.InvariantCulture, $"  Waited for since {(at - preparation.StartedAt).TotalSeconds:F0} seconds: {preparation.What}");
            }

            return message.ToString();
        }
    }

    private static TimeSpan Counted(TestEntry test, TimeSpan at) =>
        at - test.StartedAt - test.Waited - (test.Waits > 0 ? at - test.WaitStartedAt : TimeSpan.Zero);

    private void NoteLocked(string what)
    {
        _lastEvent = what;
        _lastEventAt = now();
    }

    private void EndPreparation(long id, string? test, string what)
    {
        lock (_gate)
        {
            _preparations.Remove(id);
            if (test is not null && _tests.TryGetValue(test, out var entry) && entry.Waits > 0 && --entry.Waits == 0)
            {
                entry.Waited += now() - entry.WaitStartedAt;
            }

            NoteLocked("waited for " + what);
        }
    }

    private sealed class TestEntry(string name, TimeSpan startedAt)
    {
        public string Name { get; } = name;

        public TimeSpan StartedAt { get; } = startedAt;

        /// <summary>The time spent in waits that have ended.</summary>
        public TimeSpan Waited { get; set; }

        /// <summary>How many waits are under way now.</summary>
        public int Waits { get; set; }

        /// <summary>When the waits under way began to overlap: the start of the first of them.</summary>
        public TimeSpan WaitStartedAt { get; set; }
    }

    private sealed class Preparation(HangClock clock, long id, string? test, string what) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                clock.EndPreparation(id, test, what);
            }
        }
    }
}
