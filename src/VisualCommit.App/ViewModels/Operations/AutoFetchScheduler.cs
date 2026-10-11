using System.Globalization;
using System.Runtime.CompilerServices;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Fetches the active tab's repository in the background every <c>AutoFetchMinutes</c>
/// (docs/phase-3-design.md 1.8, 2.17, 6.1, D79, D85, D109). One per <see cref="MainWindowViewModel"/>.
/// Every 30 seconds of its clock it looks at the active tab (<see cref="Tick"/>): when the
/// interval is above 0, the repository has a remote, its queue is idle, no dialog is open, and
/// the last fetch (a fetch or pull that succeeded, an auto-fetch started, or the moment opening
/// the repository began) is at least the interval ago, it starts the fetch of every remote, each
/// call with <c>--no-auto-maintenance</c>, as the queue's background operation: it never prompts
/// (<see cref="PromptMode.Deny"/>), is never pre-empted or stopped, and a failure only shows
/// "Auto-fetch failed" in the status bar and the log (<see cref="OperationStatusViewModel"/>, the queue).
/// </summary>
public sealed class AutoFetchScheduler : IDisposable
{
    /// <summary>The environment variable that overrides <c>AutoFetchMinutes</c> when it holds a whole number (6.1); the harnesses set it to 0.</summary>
    public const string MinutesVariable = "VISUALCOMMIT_AUTO_FETCH_MINUTES";

    /// <summary>The operation's title (2.33): what the log names.</summary>
    public const string Title = "Auto-fetch";

    /// <summary>How often the scheduler looks at the active tab.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>What the scheduler knows of each repository it has seen, kept no longer than the repository.</summary>
    private readonly ConditionalWeakTable<RepositoryViewModel, Tracked> _tracked = new();
    private readonly Lock _gate = new();
    private SynchronizationContext? _ui;
    private ITimer? _timer;
    private bool _disposed;

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

    /// <summary>
    /// Reads an environment variable: the process's own. Tests give their own, because the test
    /// processes set <see cref="MinutesVariable"/> to 0 for everything else they run.
    /// </summary>
    public Func<string, string?> ReadEnvironment { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>
    /// The interval: the environment variable when it holds a whole number, otherwise the
    /// setting; zero (or less) turns auto-fetch off.
    /// </summary>
    public TimeSpan Interval
    {
        get
        {
            var minutes = int.TryParse(ReadEnvironment(MinutesVariable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var overridden)
                ? overridden
                : Settings.Current.AutoFetchMinutes;
            return minutes > 0 ? TimeSpan.FromMinutes(minutes) : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Starts the timer: every <see cref="TickInterval"/> it calls <see cref="Tick"/> on the
    /// synchronization context of this call (the UI thread), where the repository's state and its
    /// queue live. Called once the window's tabs exist.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _timer is not null)
            {
                return;
            }

            _ui = SynchronizationContext.Current;
            _timer = Clock.CreateTimer(_ => OnTimer(), null, TickInterval, TickInterval);
        }
    }

    /// <summary>One look at the active tab: starts its auto-fetch when it is due (6.1). Call it on the UI thread.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (_disposed)
        {
            return;
        }

        var repository = ActiveRepository();
        if (repository is null)
        {
            return;
        }

        // Seen once, a repository is followed from then on: its fetches and pulls move the time.
        var tracked = Track(repository, now);
        var interval = Interval;
        if (interval <= TimeSpan.Zero
            || repository.Refs is not { Remotes.Count: > 0 }
            || repository.Queue.Current is not null
            || repository.Queue.Waiting > 0
            || IsDialogOpen()
            || now - tracked.LastFetch < interval)
        {
            return;
        }

        if (repository.Queue.TryStartInBackground(CreateOperation(repository.Repository)))
        {
            // A failing remote is tried again one interval later, not at every tick.
            tracked.LastFetch = now;
            Log.Debug($"{repository.Name}: auto-fetch started");
        }
    }

    /// <summary>
    /// The auto-fetch (2.17): 2.16's fetch of every remote, each call with
    /// <c>--no-auto-maintenance</c> (D109), as a background network operation that denies every
    /// prompt and refreshes the refs afterwards.
    /// </summary>
    public static GitOperation CreateOperation(IGitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new GitOperation(
            Title,
            string.Empty,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                await repository.FetchAsync(null, NetworkOptions.From(context), noAutoMaintenance: true, context.CancellationToken);
                return OperationReport.Done(string.Empty);
            })
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        };
    }

    public void Dispose()
    {
        ITimer? timer;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
        foreach (var (repository, tracked) in _tracked)
        {
            repository.Queue.Finished -= tracked.OnFinished;
        }

        _tracked.Clear();
    }

    /// <summary>Whether a finished operation fetched: a fetch, a pull or an auto-fetch, by its title (2.33) or its dedupe key (1.2).</summary>
    internal static bool Fetched(GitOperation operation, OperationResult result) =>
        result.Outcome == OperationOutcome.Succeeded
        && (operation.Title is "Fetch" or "Pull" or Title
            || operation.Title.StartsWith("Fetch ", StringComparison.Ordinal)
            || operation.DedupeKey?.StartsWith("fetch:", StringComparison.Ordinal) == true
            || operation.DedupeKey?.StartsWith("pull:", StringComparison.Ordinal) == true);

    private void OnTimer()
    {
        var ui = _ui;
        if (ui is null)
        {
            Tick(Clock.GetUtcNow());
        }
        else
        {
            ui.Post(_ => Tick(Clock.GetUtcNow()), null);
        }
    }

    private Tracked Track(RepositoryViewModel repository, DateTimeOffset now)
    {
        if (_tracked.TryGetValue(repository, out var tracked))
        {
            return tracked;
        }

        // The moment opening began: a tab made active long after it opened fetches at once.
        tracked = new Tracked(this, now - repository.TimeSinceOpening);
        _tracked.Add(repository, tracked);
        repository.Queue.Finished += tracked.OnFinished;
        return tracked;
    }

    /// <summary>One repository's last fetch.</summary>
    private sealed class Tracked(AutoFetchScheduler owner, DateTimeOffset opened)
    {
        private long _lastFetch = opened.UtcTicks;

        public DateTimeOffset LastFetch
        {
            get => new(Interlocked.Read(ref _lastFetch), TimeSpan.Zero);
            set => Interlocked.Exchange(ref _lastFetch, value.UtcTicks);
        }

        /// <summary>The queue's event, on any thread: a fetch or pull that succeeded counts as the last fetch.</summary>
        public void OnFinished(object? sender, OperationFinishedEventArgs e)
        {
            if (Fetched(e.Operation, e.Result))
            {
                LastFetch = owner.Clock.GetUtcNow();
            }
        }
    }
}
