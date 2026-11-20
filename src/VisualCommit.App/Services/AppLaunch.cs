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
}
