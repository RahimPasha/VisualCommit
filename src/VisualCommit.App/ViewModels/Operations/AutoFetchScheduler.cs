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
/// interval is above 0, the repository has a remote, its queue is idle, no dialog is open, no
/// other queue and no bulk run's slot fetches from its common git folder (<see cref="CommonFolderBusy"/>,
/// docs/phase-6-design.md 4.3), and the last fetch (a fetch or pull that succeeded, an auto-fetch started, or the moment opening
/// the repository began) is at least the interval ago, it starts the fetch of every remote, each
/// call with <c>--no-auto-maintenance</c>, as the queue's background operation: it never prompts
/// (<see cref="PromptMode.Deny"/>), is never pre-empted, ends by itself when its connection
/// stalls (<see cref="NetworkTimeout"/>), can be cancelled by the user while an operation of
/// theirs waits for it (the status bar's Cancel beside "Waiting for auto-fetch…"; the lock files a
/// cancel leaves open the "Auto-fetch cancelled" dialog), and a failure only shows
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
    /// How long ago opening a repository began, which the first auto-fetch counts from: the
    /// repository's own stopwatch. Tests give their own, because they move a manual clock.
    /// </summary>
    public Func<RepositoryViewModel, TimeSpan> SinceOpening { get; init; } = repository => repository.TimeSinceOpening;

    /// <summary>
    /// Whether another queue or a bulk run's slot fetches from the repository's common git folder
    /// now (docs/phase-6-design.md 4.3, D210): the tick is skipped, to try again at a later one,
    /// since two fetches of one common folder that overlap lose one of them on its remote-tracking
    /// refs (0.2). The window gives <c>BulkSlots.IsCommonFolderBusy</c> over its registry; false by
    /// default.
    /// </summary>
    public Func<RepositoryViewModel, bool> CommonFolderBusy { get; init; } = static _ => false;

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
            || now - tracked.LastFetch < interval
            || CommonFolderBusy(repository))
        {
            return;
        }

        if (repository.Queue.TryStartInBackground(CreateOperation(repository.Repository, repository.Hosting.Bindings)))
        {
            // A failing remote is tried again one interval later, not at every tick.
            tracked.LastFetch = now;
            Log.Debug($"{repository.Name}: auto-fetch started");
        }
    }

    /// <summary>
    /// The network timeout of every auto-fetch call, as git settings (<c>git -c</c>): an HTTP
    /// transfer that moves less than 1 byte a second for 60 seconds, a dead connection (Wi-Fi or
    /// VPN gone, a server that went silent), is ended by git itself, and the auto-fetch fails.
    /// A live server sends at least its keep-alive packets (every 5 seconds) while it prepares a
    /// pack, so a slow fetch is not cut. ssh has no such setting that would not replace the user's
    /// own ssh command; a stalled ssh auto-fetch is ended by the user's Cancel (1.8).
    /// </summary>
    public static readonly IReadOnlyList<string> NetworkTimeout = ["http.lowSpeedLimit=1", "http.lowSpeedTime=60"];

    /// <summary>
    /// The auto-fetch (2.17): 2.16's fetch of every remote, each call with
    /// <c>--no-auto-maintenance</c> (D109) and <see cref="NetworkTimeout"/>, as a background
    /// network operation that denies every prompt and refreshes the refs afterwards.
    /// <paramref name="bindings"/> are the tab's URLs whose credentials come from the hosting
    /// account (docs/phase-7a-design.md 1.7, 4.7), which take the account's token without a prompt.
    /// </summary>
    public static GitOperation CreateOperation(IGitRepository repository, IReadOnlyList<CredentialBinding>? bindings = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new GitOperation(
            Title,
            string.Empty,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                var options = NetworkOptions.From(context) with { Configuration = NetworkTimeout };
                await repository.FetchAsync(null, options, noAutoMaintenance: true, context.CancellationToken);
                return OperationReport.Done(string.Empty);
            })
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,

            // Bound URLs take the account's token and still never prompt (D255, D79).
            CredentialBindings = bindings ?? [],
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

    /// <summary>
    /// Whether a finished operation fetched: a fetch, a pull (a pull with rebase too,
    /// docs/phase-4-design.md section 6) or an auto-fetch, by its title (2.33, 10.1) or its dedupe
    /// key (1.2; <c>pull:&lt;ref&gt;:rebase</c>).
    /// </summary>
    public static bool Fetched(GitOperation operation, OperationResult result) =>
        result.Outcome == OperationOutcome.Succeeded
        && (operation.Title == OperationTexts.FetchAll.Title
            || operation.Title is OperationTexts.PullTitle or OperationTexts.PullRebaseTitle or Title
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
        tracked = new Tracked(this, repository, now - SinceOpening(repository));
        _tracked.Add(repository, tracked);
        repository.Queue.Finished += tracked.OnFinished;
        return tracked;
    }

    /// <summary>Shows what an auto-fetch the user cancelled left (1.8): the lock files of "Auto-fetch cancelled", on the UI thread.</summary>
    private void ShowCancelled(RepositoryViewModel repository, OperationResult result)
    {
        void Show() => _ = repository.Operations.ShowResultAsync(new OperationText(Title, string.Empty, string.Empty, string.Empty), result);
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            Show();
        }
        else
        {
            _ui.Post(_ => Show(), null);
        }
    }

    /// <summary>One repository's last fetch.</summary>
    private sealed class Tracked(AutoFetchScheduler owner, RepositoryViewModel repository, DateTimeOffset opened)
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
            else if (e.Operation.IsBackground && e.Result is { Outcome: OperationOutcome.Cancelled, StaleLockFiles.Count: > 0 })
            {
                owner.ShowCancelled(repository, e.Result);
            }
        }
    }
}
