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
/// The desktop app's timeline over the process's start time. It writes no line: each moment is
/// taken and dropped, and <see cref="FromProcess"/> gives the null timeline.
/// </summary>
public sealed class StartupTimeline : IStartupTimeline
{
    /// <summary>
    /// A timeline from <paramref name="processStarted"/> by <paramref name="clock"/> (the system's
    /// when null), to log to <paramref name="log"/>. This stub only checks <paramref name="log"/>: it
    /// keeps none of its arguments and writes no line.
    /// </summary>
    public StartupTimeline(DateTimeOffset processStarted, IAppLog log, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(log);
    }

    /// <summary>The timeline of this process. This stub checks <paramref name="log"/> and returns the null timeline.</summary>
    public static IStartupTimeline FromProcess(IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return NullStartupTimeline.Instance;
    }

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
