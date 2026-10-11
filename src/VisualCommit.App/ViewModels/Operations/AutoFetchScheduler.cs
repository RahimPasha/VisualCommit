using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Fetches the active tab's repository in the background every <c>AutoFetchMinutes</c>
/// (docs/phase-3-design.md 6.1, D79, D85). One per <see cref="MainWindowViewModel"/>.
/// A placeholder of work package WP0 so that the main window's view model can create, start and
/// dispose it: it does nothing yet. WP9 owns this file and builds it (a 30-second timer from the
/// clock, <see cref="Tick"/> with the rules of 6.1).
/// </summary>
public sealed class AutoFetchScheduler : IDisposable
{
    /// <param name="activeRepository">The active tab's repository, or null.</param>
    /// <param name="isDialogOpen">Whether a dialog is open: no auto-fetch starts then.</param>
    /// <param name="settings">The settings, for <c>AutoFetchMinutes</c>.</param>
    /// <param name="clock">The clock and its timers.</param>
    /// <param name="log">The app's log.</param>
    public AutoFetchScheduler(
        Func<RepositoryViewModel?> activeRepository,
        Func<bool> isDialogOpen,
        ISettingsStore settings,
        TimeProvider clock,
        IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(activeRepository);
        ArgumentNullException.ThrowIfNull(isDialogOpen);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        ActiveRepository = activeRepository;
        IsDialogOpen = isDialogOpen;
        Settings = settings;
        Clock = clock;
        Log = log;
    }

    private Func<RepositoryViewModel?> ActiveRepository { get; }

    private Func<bool> IsDialogOpen { get; }

    private ISettingsStore Settings { get; }

    private TimeProvider Clock { get; }

    private IAppLog Log { get; }

    /// <summary>Starts the timer. Called once the window's tabs exist.</summary>
    public void Start()
    {
    }

    /// <summary>One look at the active tab: starts its auto-fetch when it is due (6.1).</summary>
    public void Tick(DateTimeOffset now)
    {
    }

    public void Dispose()
    {
    }
}
