using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>
/// The moments of the app's start, told by the session as they happen: the window opened, git
/// found, the restored tabs counted, a repository's first graph rows drawn. Called from any thread.
/// </summary>
public interface IStartupTimeline
{
    /// <summary>The main window's <c>Opened</c>.</summary>
    void WindowOpened();

    /// <summary>The first search for git ended with a usable git.</summary>
    void GitFound();

    /// <summary>The restored tabs that open a repository, counted before they open; dashboards are not counted.</summary>
    void TabsRestored(int count);

    /// <summary>A repository's first graph rows were drawn.</summary>
    void FirstRows(string repositoryName);
}

/// <summary>A timeline that notes nothing: the harnesses' and the view-model tests'.</summary>
public sealed class NullStartupTimeline : IStartupTimeline
{
    private NullStartupTimeline()
    {
    }

    /// <summary>The one instance.</summary>
    public static NullStartupTimeline Instance { get; } = new();

    public void WindowOpened()
    {
    }

    public void GitFound()
    {
    }

    public void TabsRestored(int count)
    {
    }

    public void FirstRows(string repositoryName)
    {
    }
}

/// <summary>
/// The desktop app's timeline over the process's start time: one Info line for each moment, the
/// first time it is told of it and from any thread, with the whole milliseconds since the process
/// started (D436). The lines say only what is true (D438): the first graph rows only after a restore
/// of one tab or more, since after the user's own open the repository's own first-rows line says it;
/// and the session tells of git only when its first search found a usable one.
/// </summary>
public sealed class StartupTimeline : IStartupTimeline
{
    /// <summary>The window's line; {0} the milliseconds.</summary>
    public const string WindowOpenedFormat = "Start-up: window opened after {0} ms since the process started.";

    /// <summary>Git's line; {0} the milliseconds.</summary>
    public const string GitFoundFormat = "Start-up: Git found after {0} ms since the process started.";

    /// <summary>The first graph rows' line; {0} the repository, {1} the milliseconds, {2} the restored tabs (<see cref="OneRestoredTab"/>, <see cref="RestoredTabsFormat"/>).</summary>
    public const string FirstRowsFormat = "Start-up: first graph rows of {0} after {1} ms since the process started ({2}).";

    /// <summary>The restored tabs of <see cref="FirstRowsFormat"/> when there is one.</summary>
    public const string OneRestoredTab = "1 restored tab";

    /// <summary>The restored tabs of <see cref="FirstRowsFormat"/> when there are more; {0} their count.</summary>
    public const string RestoredTabsFormat = "{0} restored tabs";

    /// <summary>The Warning of <see cref="FromProcess(IAppLog)"/> when the process's start time cannot be read.</summary>
    public const string StartTimeUnreadable = "Start-up: the process's start time could not be read, so no start-up times are logged.";

    /// <summary>What <see cref="_restoredTabs"/> holds until <see cref="TabsRestored"/> is called.</summary>
    private const int NotTold = -1;

    private readonly DateTimeOffset _processStarted;
    private readonly IAppLog _log;
    private readonly TimeProvider _clock;

    // Each set once with Interlocked, so that a line is written once whatever the thread: git's
    // search ends on the thread that ran it, the window and the graph tell on the UI thread.
    private int _windowOpened;
    private int _gitFound;
    private int _firstRows;
    private int _restoredTabs = NotTold;

    /// <summary>
    /// A timeline of a process that started at <paramref name="processStarted"/>, read against
    /// <paramref name="clock"/> (the system's when null), that writes to <paramref name="log"/>.
    /// </summary>
    public StartupTimeline(DateTimeOffset processStarted, IAppLog log, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        _processStarted = processStarted;
        _log = log;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// The timeline of this process, over its start time as the system reports it. When that cannot
    /// be read, the null timeline, after one Warning (<see cref="StartTimeUnreadable"/>).
    /// </summary>
    public static IStartupTimeline FromProcess(IAppLog log) => FromProcess(log, ReadThisProcessStart);

    /// <summary>
    /// The timeline of a process whose start time <paramref name="readStartTime"/> reads, as
    /// <see cref="FromProcess(IAppLog)"/> makes it: the null timeline, after one Warning, when the read
    /// throws what the system's read of a process's start time throws.
    /// </summary>
    public static IStartupTimeline FromProcess(IAppLog log, Func<DateTimeOffset> readStartTime)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(readStartTime);
        DateTimeOffset started;
        try
        {
            started = readStartTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            log.Warning(StartTimeUnreadable);
            return NullStartupTimeline.Instance;
        }

        return new StartupTimeline(started, log);
    }

    public void WindowOpened()
    {
        if (Interlocked.Exchange(ref _windowOpened, 1) == 0)
        {
            _log.Info(string.Format(CultureInfo.InvariantCulture, WindowOpenedFormat, Elapsed()));
        }
    }

    public void GitFound()
    {
        if (Interlocked.Exchange(ref _gitFound, 1) == 0)
        {
            _log.Info(string.Format(CultureInfo.InvariantCulture, GitFoundFormat, Elapsed()));
        }
    }

    /// <summary>Keeps <paramref name="count"/> for the first-rows line; only the first call counts, and it writes nothing.</summary>
    public void TabsRestored(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Interlocked.CompareExchange(ref _restoredTabs, count, NotTold);
    }

    /// <summary>
    /// Writes the first-rows line at the first call, and only when <see cref="TabsRestored"/> came
    /// before it with one tab or more; every later call writes nothing.
    /// </summary>
    public void FirstRows(string repositoryName)
    {
        ArgumentNullException.ThrowIfNull(repositoryName);
        if (Interlocked.Exchange(ref _firstRows, 1) != 0)
        {
            return;
        }

        var restored = Volatile.Read(ref _restoredTabs);
        if (restored < 1)
        {
            return;
        }

        var tabs = restored == 1 ? OneRestoredTab : string.Format(CultureInfo.InvariantCulture, RestoredTabsFormat, restored);
        _log.Info(string.Format(CultureInfo.InvariantCulture, FirstRowsFormat, repositoryName, Elapsed(), tabs));
    }

    /// <summary>The whole milliseconds since the process started, rounded down; 0 for a clock that reads earlier.</summary>
    private long Elapsed() =>
        Math.Max(0, (_clock.GetUtcNow() - _processStarted).Ticks / TimeSpan.TicksPerMillisecond);

    /// <summary>This process's start time, which the system reports as a local time.</summary>
    private static DateTimeOffset ReadThisProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime);
    }
}
