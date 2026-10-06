using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Xunit.v3;

namespace VisualCommit.Testing;

/// <summary>
/// Stops a test run that hangs, and says where. A test assembly turns it on with
/// <c>[assembly: HangWatchdog]</c>.
/// <para>
/// A hung test otherwise shows nothing: the run just never ends, locally until someone kills it
/// and in CI until the job times out, and neither tells which test it was. With the watchdog, a
/// test that runs longer than the limit, or a run in which no test starts or finishes for that
/// long, ends the test process with a message naming the tests that were running and the last
/// one that started or finished.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class HangWatchdogAttribute : BeforeAfterTestAttribute
{
    /// <summary>Overrides the limit, in seconds. For trying the watchdog out.</summary>
    public const string LimitVariable = "VISUALCOMMIT_TEST_HANG_SECONDS";

    /// <summary>The longest a single test may run, and the longest the run may stand still. No test comes close.</summary>
    private static readonly TimeSpan Limit = ReadLimit() ?? TimeSpan.FromMinutes(3);

    private static readonly ConcurrentDictionary<string, (string Name, long StartedAt)> Running = new();
    private static readonly Lock StartGate = new();
    private static Thread? watcher;
    private static long lastEventAt = Stopwatch.GetTimestamp();
    private static string lastEvent = "no test has started";

    /// <summary>
    /// Starts the watch as soon as the test framework reads the attribute, so that a run that
    /// hangs before its first test, or in a test whose runner does not call the hooks below, is
    /// still caught by the "nothing happens" rule.
    /// </summary>
    public HangWatchdogAttribute()
    {
        lock (StartGate)
        {
            if (watcher is not null)
            {
                return;
            }

            // A thread of its own, not a timer: a timer needs the thread pool, and a hang can
            // be exactly a thread pool with no thread to spare.
            watcher = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    Check();
                }
            })
            {
                IsBackground = true,
                Name = "Hang watchdog",
            };
            watcher.Start();
        }
    }

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        Running[test.UniqueID] = (test.TestDisplayName, Stopwatch.GetTimestamp());
        Note("started " + test.TestDisplayName);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        Running.TryRemove(test.UniqueID, out _);
        Note("finished " + test.TestDisplayName);
    }

    private static void Note(string what)
    {
        Volatile.Write(ref lastEvent, what);
        Volatile.Write(ref lastEventAt, Stopwatch.GetTimestamp());
    }

    private static void Check()
    {
        var running = Running.Values.ToList();
        var overdue = running.Where(test => Stopwatch.GetElapsedTime(test.StartedAt) > Limit).ToList();
        var standingStill = Stopwatch.GetElapsedTime(Volatile.Read(ref lastEventAt)) > Limit;
        if (overdue.Count == 0 && !standingStill)
        {
            return;
        }

        var message = new StringBuilder()
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"HANG WATCHDOG: the test run is stuck (limit {Limit.TotalSeconds:F0} seconds). Stopping the test process.")
            .AppendLine(CultureInfo.InvariantCulture, $"  Last event, {Stopwatch.GetElapsedTime(Volatile.Read(ref lastEventAt)).TotalSeconds:F0} seconds ago: {Volatile.Read(ref lastEvent)}");
        if (running.Count == 0)
        {
            message.AppendLine("  No test is running: the run is stuck between tests or after the last one.");
        }

        foreach (var test in running.OrderBy(test => test.StartedAt))
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"  Running for {Stopwatch.GetElapsedTime(test.StartedAt).TotalSeconds:F0} seconds: {test.Name}");
        }

        // Both streams: which one the test runner shows for a dead test process varies.
        Console.Error.WriteLine(message);
        Console.Error.Flush();
        Console.Out.WriteLine(message);
        Console.Out.Flush();
        Process.GetCurrentProcess().Kill();
    }

    private static TimeSpan? ReadLimit() =>
        double.TryParse(Environment.GetEnvironmentVariable(LimitVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
}
