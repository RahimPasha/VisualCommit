namespace VisualCommit.App.Services;

/// <summary>
/// What the desktop app's start hands the session: set by <c>Program.Main</c> before Avalonia
/// starts, read by <c>App</c> when the framework is ready. A harness never sets it; its sessions
/// get their own seams through <c>AppSession.Start</c>. No Avalonia type: it exists before Avalonia.
/// </summary>
public sealed class AppLaunch
{
    /// <summary>This process's launch; a launch with the null timeline until <c>Program.Main</c> sets it.</summary>
    public static AppLaunch Current { get; set; } = new();

    /// <summary>The start's timeline, which the session tells of its moments.</summary>
    public IStartupTimeline Timeline { get; init; } = NullStartupTimeline.Instance;

    /// <summary>
    /// The guard that keeps a second VisualCommit off this data folder (D421), which
    /// <c>Program.Main</c> took before Avalonia started and disposes after it ended; null in a
    /// harness, where the guard never runs.
    /// </summary>
    public SingleInstanceGuard? Guard { get; init; }

    /// <summary>
    /// What the window shows: the session (<see cref="AppStartMode.Normal"/>), or, while another
    /// VisualCommit holds the data folder and did not answer, the notice alone, with no session.
    /// </summary>
    public AppStartMode Mode { get; init; } = AppStartMode.Normal;

    /// <summary>
    /// What <c>Program.Main</c> returns once the app has ended: 2 while the notice shows and after
    /// it closes unanswered (D421); 0 after a hand-over to the other instance or a start in
    /// this process.
    /// </summary>
    public int ExitCode { get; set; }
}
