using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Xunit;
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
/// <para>
/// The time a test waits for a shared preparation, a scenario template or the 100k-commit repo
/// that one test of the process builds for all (<see cref="SharedPreparationAsync{T}"/>), does
/// not count against that test; the preparation is held to the limit on its own
/// (<see cref="HangClock"/>).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class HangWatchdogAttribute : BeforeAfterTestAttribute
{
    /// <summary>Overrides the limit, in seconds. For trying the watchdog out.</summary>
    public const string LimitVariable = "VISUALCOMMIT_TEST_HANG_SECONDS";

    /// <summary>The longest a single test may run, and the longest the run may stand still. No test comes close.</summary>
    private static readonly TimeSpan Limit = ReadLimit() ?? TimeSpan.FromMinutes(3);

    private static readonly long RunStart = Stopwatch.GetTimestamp();
    private static readonly HangClock Clock = new(() => Stopwatch.GetElapsedTime(RunStart));
    private static readonly Lock StartGate = new();
    private static Thread? watcher;

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

    /// <summary>
    /// Awaits <paramref name="preparation"/>, something the tests of a process share and the
    /// first of them builds (<paramref name="what"/> names it in the watchdog's message), without
    /// counting the wait against the test that awaits it. Outside a test, or in an assembly
    /// without the watchdog, it just awaits.
    /// </summary>
    public static async Task<T> SharedPreparationAsync<T>(string what, Task<T> preparation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.IsCompleted)
        {
            return await preparation.ConfigureAwait(false);
        }

        using (Clock.Preparing(TestContext.Current.Test?.UniqueID, what))
        {
            return await preparation.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The time the current test has run so far, less its waits for shared preparations: what
    /// the watchdog holds to the limit. Null outside a test, or in an assembly without the watchdog.
    /// </summary>
    public static TimeSpan? CountedTimeOfCurrentTest() =>
        TestContext.Current.Test?.UniqueID is { } test ? Clock.CountedTime(test) : null;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test) => Clock.Started(test.UniqueID, test.TestDisplayName);

    public override void After(MethodInfo methodUnderTest, IXunitTest test) => Clock.Finished(test.UniqueID);

    private static void Check()
    {
        if (Clock.Check(Limit) is not { } message)
        {
            return;
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
