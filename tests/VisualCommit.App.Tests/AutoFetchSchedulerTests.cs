using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Auto-fetch's scheduler (docs/phase-3-design.md 1.8, 2.17, 6.1, D79, D85, D109) with a manual
/// clock and fake repositories: when it fetches, what it runs, and what keeps it from running.
/// No test waits for a real timer: each calls <see cref="AutoFetchScheduler.Tick"/> with a chosen
/// time, or fires the clock's timer by hand.
/// </summary>
public class AutoFetchSchedulerTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_interval_is_the_setting_unless_the_environment_variable_holds_a_whole_number()
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        string? variable = null;
        using var scheduler = new AutoFetchScheduler(() => null, () => false, settings, new ManualClock(Opened), new RecordingLog())
        {
            ReadEnvironment = name => name == AutoFetchScheduler.MinutesVariable ? variable : null,
        };

        Assert.Equal(TimeSpan.FromMinutes(5), scheduler.Interval);
        variable = "2";
        Assert.Equal(TimeSpan.FromMinutes(2), scheduler.Interval);
        variable = "0";
        Assert.Equal(TimeSpan.Zero, scheduler.Interval);
        variable = "soon";
        Assert.Equal(TimeSpan.FromMinutes(5), scheduler.Interval);
        settings.Update(current => current with { AutoFetchMinutes = 0 });
        Assert.Equal(TimeSpan.Zero, scheduler.Interval);
        settings.Update(current => current with { AutoFetchMinutes = -3 });
        Assert.Equal(TimeSpan.Zero, scheduler.Interval);
    }

    [Fact]
    public void The_test_processes_turn_it_off()
    {
        using var scheduler = new AutoFetchScheduler(() => null, () => false, new MainWindowViewModelTests.FakeSettings(), TimeProvider.System, new RecordingLog());

        Assert.Equal("0", Environment.GetEnvironmentVariable(AutoFetchScheduler.MinutesVariable));
        Assert.Equal(TimeSpan.Zero, scheduler.Interval);
    }

    [Fact]
    public async Task The_first_fetch_comes_one_interval_after_opening_and_the_next_one_interval_after_it()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository);
        using var scheduler = Scheduler(clock, () => viewModel);

        await TickAsync(scheduler, clock, viewModel);
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        clock.Advance(TimeSpan.FromSeconds(1));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Equal([(null, true)], repository.Fetches);

        clock.Advance(TimeSpan.FromMinutes(4));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);
        clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Equal(2, repository.Fetches.Count);
    }

    [Fact]
    public async Task It_runs_as_a_background_operation_that_denies_prompts_and_refreshes_the_refs()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository);
        using var scheduler = Scheduler(clock, () => viewModel);
        var finished = new List<GitOperation>();
        viewModel.Queue.Finished += (_, e) => finished.Add(e.Operation);
        var refsRead = repository.RefsRead;

        await TickAsync(scheduler, clock, viewModel);
        clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync(scheduler, clock, viewModel);

        var operation = Assert.Single(finished);
        Assert.Equal(("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.Refs), (operation.Title, operation.RunningText, operation.Kind, operation.Refresh));
        Assert.True(operation.IsBackground);
        Assert.Equal(PromptMode.Deny, operation.Prompts);
        Assert.Null(operation.DedupeKey);
        Assert.True(repository.RefsRead > refsRead, "The refs are read again after the fetch.");

        // Nothing of a background success is shown (4e).
        Assert.Equal("Ready", viewModel.OperationStatus.Text);
    }

    [Fact]
    public async Task A_fetch_or_pull_the_user_made_moves_the_next_auto_fetch_but_a_failed_one_does_not()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository);
        using var scheduler = Scheduler(clock, () => viewModel);
        await TickAsync(scheduler, clock, viewModel);

        clock.Advance(TimeSpan.FromMinutes(3));
        await viewModel.Queue.RunAsync(Operation("Fetch", dedupeKey: "fetch:*"));
        clock.Advance(TimeSpan.FromMinutes(4));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        clock.Advance(TimeSpan.FromMinutes(1));
        await viewModel.Queue.RunAsync(Operation("Pull", dedupeKey: "pull:refs/heads/main"));
        clock.Advance(TimeSpan.FromMinutes(4));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        // A failed fetch and another operation do not count.
        await viewModel.Queue.RunAsync(Operation("Fetch origin", fails: true));
        await viewModel.Queue.RunAsync(Operation("Create branch hotfix"));
        clock.Advance(TimeSpan.FromMinutes(1));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);

        Assert.True(AutoFetchScheduler.Fetched(Operation("Fetch origin"), new OperationResult(OperationOutcome.Succeeded, "Fetched from origin", null)));
        Assert.False(AutoFetchScheduler.Fetched(Operation("Fetch origin"), new OperationResult(OperationOutcome.Cancelled, "Fetch origin cancelled", null)));
        Assert.False(AutoFetchScheduler.Fetched(Operation("Push main"), new OperationResult(OperationOutcome.Succeeded, "Pushed", null)));
    }

    [Fact]
    public async Task Nothing_starts_without_a_remote_while_a_dialog_is_open_while_the_queue_is_busy_or_when_it_is_off()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository);
        var dialogOpen = true;
        var minutes = "5";
        using var scheduler = new AutoFetchScheduler(() => viewModel, () => dialogOpen, new MainWindowViewModelTests.FakeSettings(), clock, new RecordingLog())
        {
            ReadEnvironment = _ => minutes,
            SinceOpening = _ => TimeSpan.Zero,
        };
        await TickAsync(scheduler, clock, viewModel);
        clock.Advance(TimeSpan.FromMinutes(10));

        // A dialog is open.
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);
        dialogOpen = false;

        // The queue runs the user's operation, or one waits.
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = viewModel.Queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await held.Task;
            return OperationReport.Done("Pushed main");
        }));
        scheduler.Tick(clock.GetUtcNow());
        Assert.Empty(repository.Fetches);
        held.SetResult();
        await running;
        await viewModel.Queue.WhenIdleAsync();

        // Turned off.
        minutes = "0";
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        // No remote.
        minutes = "5";
        repository.Remotes = [];
        await viewModel.RefreshAsync();
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        repository.Remotes = ["origin"];
        await viewModel.RefreshAsync();
        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);
    }

    [Fact]
    public async Task Only_the_active_tabs_repository_fetches_and_one_opened_long_ago_fetches_when_it_becomes_active()
    {
        var clock = new ManualClock(Opened);
        var first = new RemoteRepository();
        var second = new RemoteRepository();
        using var firstViewModel = await OpenAsync(first);
        using var secondViewModel = await OpenAsync(second);
        RepositoryViewModel? active = null;
        using var scheduler = Scheduler(clock, () => active, repository => repository == secondViewModel ? TimeSpan.FromMinutes(20) : TimeSpan.Zero);

        // No repository: nothing to do.
        scheduler.Tick(clock.GetUtcNow());

        active = firstViewModel;
        await TickAsync(scheduler, clock, firstViewModel);
        clock.Advance(TimeSpan.FromMinutes(2));
        await TickAsync(scheduler, clock, firstViewModel);
        Assert.Empty(first.Fetches);

        // The second tab began opening 20 minutes before it was first seen: it fetches at once.
        active = secondViewModel;
        await TickAsync(scheduler, clock, secondViewModel);
        Assert.Single(second.Fetches);
        Assert.Empty(first.Fetches);

        clock.Advance(TimeSpan.FromMinutes(10));
        await TickAsync(scheduler, clock, secondViewModel);
        Assert.Equal(2, second.Fetches.Count);
        Assert.Empty(first.Fetches);
    }

    [Fact]
    public async Task A_failure_shows_quietly_in_the_status_bar_and_is_tried_again_only_one_interval_later()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository
        {
            Failure = new GitException("git fetch --progress --no-auto-maintenance origin", 128, "fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect\n"),
        };
        var dialogs = new CountingDialogs();
        using var viewModel = await OpenAsync(repository, dialogs: dialogs);
        using var scheduler = Scheduler(clock, () => viewModel);
        await TickAsync(scheduler, clock, viewModel);
        clock.Advance(TimeSpan.FromMinutes(5));

        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);
        Assert.Equal("Auto-fetch failed", viewModel.OperationStatus.Text);
        Assert.Equal("fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect", viewModel.OperationStatus.ToolTip);
        Assert.Equal(0, dialogs.Asked);

        clock.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);
        clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Equal(2, repository.Fetches.Count);
    }

    [Fact]
    public async Task Start_ticks_every_30_seconds_of_the_clock_and_Dispose_stops_it()
    {
        var clock = new ManualClock(Opened);
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository);
        var scheduler = Scheduler(clock, () => viewModel);

        scheduler.Start();
        var timer = Assert.Single(clock.Timers);
        Assert.Equal((TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)), (timer.DueTime, timer.Period));

        timer.Fire();
        await viewModel.Queue.WhenIdleAsync();
        clock.Advance(TimeSpan.FromMinutes(5));
        timer.Fire();
        await viewModel.Queue.WhenIdleAsync();
        Assert.Single(repository.Fetches);

        scheduler.Dispose();
        Assert.True(timer.IsDisposed);
        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick(clock.GetUtcNow());
        await viewModel.Queue.WhenIdleAsync();
        Assert.Single(repository.Fetches);
    }

    // docs/phase-6-design.md 4.3, 4.11, 4.13: a dashboard tab, and the common git folder.

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task The_windows_scheduler_fetches_nothing_while_a_dashboard_tab_is_active_and_its_repository_tab_when_it_is()
    {
        using var provider = new BulkProvider();
        var api = provider.Add("api");
        var docs = provider.Add("docs");
        var (window, _) = WindowWithAutoFetch(provider, provider.PathOf("docs"));
        var repoTab = window.ActiveTab;
        Assert.True(await repoTab.OpenAsync(api.WorkingDirectory));
        window.ShowWorkspace("web", null);
        await DashboardTestSupport.WaitUntilAsync(() => window.ActiveTab.Dashboard?.Rows[0].HasRepository == true, "the row's read");
        Assert.True(window.ActiveTab.IsDashboard);

        // The window's own scheduler, an hour apart: nothing on a dashboard tab (4.3, D85).
        window.AutoFetch.Tick(Opened);
        window.AutoFetch.Tick(Opened.AddHours(1));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((0, 0), (api.Fetches, docs.Fetches));

        // Its repository tab active: tracked at the first tick, fetched at the next one due.
        window.ActivateTabCommand.Execute(repoTab);
        window.AutoFetch.Tick(Opened.AddHours(1));
        window.AutoFetch.Tick(Opened.AddHours(2));
        await repoTab.Repository!.Queue.WhenIdleAsync();
        Assert.Equal((1, 0), (api.Fetches, docs.Fetches));
        window.CloseAll();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task The_windows_scheduler_skips_a_tick_while_a_bulk_slot_fetches_from_the_common_folder()
    {
        using var provider = new BulkProvider();
        var api = provider.Add("api");
        var (window, services) = WindowWithAutoFetch(provider);
        Assert.True(await window.ActiveTab.OpenAsync(api.WorkingDirectory));
        window.AutoFetch.Tick(Opened);

        var slot = services.BulkSlots.Request(WorktreeKey.Of(api.CommonDirectory));
        window.AutoFetch.Tick(Opened.AddHours(1));
        await window.ActiveTab.Repository!.Queue.WhenIdleAsync();
        Assert.Equal(0, api.Fetches);

        slot.Release();
        window.AutoFetch.Tick(Opened.AddHours(1).AddSeconds(30));
        await window.ActiveTab.Repository!.Queue.WhenIdleAsync();
        Assert.Equal(1, api.Fetches);
        window.CloseAll();
    }

    /// <summary>
    /// A window over <paramref name="provider"/>'s fakes whose own scheduler is on (its interval's
    /// variable read as 5 minutes, its timer on a clock that never moves), with a workspace "Web"
    /// of <paramref name="folders"/>.
    /// </summary>
    private static (MainWindowViewModel Window, TabServices Services) WindowWithAutoFetch(BulkProvider provider, params string[] folders)
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        var services = new TabServices(
            provider,
            new NoFolders(),
            settings,
            new DashboardTestSupport.MemorySession(),
            new DateDisplay(TimeZoneInfo.Utc),
            new RecordingLog(),
            clock: new ManualTimeProvider(),
            workspaces: DashboardTestSupport.Store(folders),
            readClock: new ManualTimeProvider());
        var window = new MainWindowViewModel(
            settings,
            new MainWindowViewModelTests.FakeThemes(),
            _ => Task.FromResult(new GitDetection(GitAvailability.Available, "git", new GitVersion(2, 47, 1), null)),
            services.Log,
            services,
            services.Session,
            readEnvironment: name => name == AutoFetchScheduler.MinutesVariable ? "5" : null);
        return (window, services);
    }

    [Fact]
    public async Task No_auto_fetch_starts_while_another_worktrees_queue_fetches_from_the_common_folder_and_the_next_tick_after_it_does()
    {
        var clock = new ManualClock(Opened);
        var services = AutoFetchServices();
        var repository = new RemoteRepository();
        var sibling = new RemoteRepository("auto-fetch-wt") { Common = repository.GitDirectory };
        using var viewModel = await OpenAsync(repository, services);
        using var other = await OpenAsync(sibling, services);
        services.OpenRepositories.RegisterTab(viewModel);
        services.OpenRepositories.RegisterTab(other);
        using var scheduler = Scheduler(clock, () => viewModel, busy: vm => services.BulkSlots.IsCommonFolderBusy(vm, services.OpenRepositories));
        scheduler.Tick(clock.GetUtcNow());

        var finish = new TaskCompletionSource();
        var fetching = other.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await finish.Task;
            return OperationReport.Done("Fetched from all remotes");
        }));
        clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        finish.SetResult();
        await fetching;
        clock.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Equal([(null, true)], repository.Fetches);
    }

    [Fact]
    public async Task No_auto_fetch_starts_while_a_bulk_runs_slot_is_out_on_the_common_folder()
    {
        var clock = new ManualClock(Opened);
        var services = AutoFetchServices();
        var repository = new RemoteRepository();
        using var viewModel = await OpenAsync(repository, services);
        using var scheduler = Scheduler(clock, () => viewModel, busy: vm => services.BulkSlots.IsCommonFolderBusy(vm, services.OpenRepositories));
        scheduler.Tick(clock.GetUtcNow());

        var slot = services.BulkSlots.Request(WorktreeKey.Of(repository.CommonDirectory));
        clock.Advance(TimeSpan.FromMinutes(5));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Empty(repository.Fetches);

        slot.Release();
        clock.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(scheduler, clock, viewModel);
        Assert.Single(repository.Fetches);
    }

    /// <summary>A scheduler on the manual clock, with the setting's 5 minutes; each repository began opening <paramref name="openedAgo"/> before it is first seen (none by default).</summary>
    private static AutoFetchScheduler Scheduler(ManualClock clock, Func<RepositoryViewModel?> active, Func<RepositoryViewModel, TimeSpan>? openedAgo = null, Func<RepositoryViewModel, bool>? busy = null) =>
        new(active, () => false, new MainWindowViewModelTests.FakeSettings(), clock, new RecordingLog())
        {
            ReadEnvironment = _ => null,
            SinceOpening = openedAgo ?? (_ => TimeSpan.Zero),
            CommonFolderBusy = busy ?? (_ => false),
        };

    /// <summary>The session's services: the registry and the slots the common folder's check reads.</summary>
    private static TabServices AutoFetchServices() =>
        DashboardTestSupport.Services(new NoRepositories(), readClock: new ManualTimeProvider(), log: new RecordingLog());

    /// <summary>Opens a view model on <paramref name="repository"/> with the session's <paramref name="services"/> and reads its refs.</summary>
    private static async Task<RepositoryViewModel> OpenAsync(RemoteRepository repository, TabServices services)
    {
        var viewModel = new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, services: services);
        await viewModel.StartAsync();
        return viewModel;
    }

    private static async Task TickAsync(AutoFetchScheduler scheduler, ManualClock clock, RepositoryViewModel viewModel)
    {
        scheduler.Tick(clock.GetUtcNow());
        await viewModel.Queue.WhenIdleAsync();
    }

    /// <summary>Opens a view model on <paramref name="repository"/> and reads its refs.</summary>
    private static async Task<RepositoryViewModel> OpenAsync(RemoteRepository repository, VisualCommit.App.ViewModels.Dialogs.IDialogService? dialogs = null)
    {
        var viewModel = new RepositoryViewModel(
            repository,
            new MainWindowViewModelTests.FakeSettings(),
            new DateDisplay(TimeZoneInfo.Utc),
            dialogs: dialogs);
        await viewModel.StartAsync();
        return viewModel;
    }

    private static GitOperation Operation(string title, string? dedupeKey = null, bool fails = false) =>
        new(title, title + "…", OperationKind.Network, RefreshScope.None, _ => fails
            ? throw new GitException("git fetch origin", 128, "fatal: no")
            : Task.FromResult(OperationReport.Done(title + " done")))
        {
            DedupeKey = dedupeKey,
        };

    /// <summary>A manual clock whose timers the test fires.</summary>
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public List<ManualTimer> Timers { get; } = [];

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(() => callback(state), dueTime, period);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(Action fire, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        public TimeSpan DueTime { get; } = dueTime;

        public TimeSpan Period { get; } = period;

        public bool IsDisposed { get; private set; }

        public void Fire()
        {
            if (!IsDisposed)
            {
                fire();
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => !IsDisposed;

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CountingDialogs : VisualCommit.App.ViewModels.Dialogs.IDialogService
    {
        public int Asked { get; private set; }

        public Task<bool> ConfirmAsync(VisualCommit.App.ViewModels.Dialogs.ConfirmationRequest request)
        {
            Asked++;
            return Task.FromResult(false);
        }
    }

    /// <summary>No folder is chosen.</summary>
    private sealed class NoFolders : VisualCommit.App.Services.IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    /// <summary>No repository opens: the auto-fetch tests make their view models themselves.</summary>
    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();
    }

    /// <summary>A repository with one commit and a remote; its fetches are recorded, and fail when told to.</summary>
    private sealed class RemoteRepository(string name = "auto-fetch") : FakeRepositoryBase
    {
        private static readonly string Head = new('a', 40);

        public List<(string? Remote, bool NoAutoMaintenance)> Fetches { get; } = [];

        public IReadOnlyList<string> Remotes { get; set; } = ["origin"];

        public Exception? Failure { get; init; }

        public int RefsRead { get; private set; }

        /// <summary>Another worktree's git folder as its common folder (a linked worktree), or null for its own.</summary>
        public string? Common { get; init; }

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), name);

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => Common ?? GitDirectory;

        public override string Name => name;

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
        {
            RefsRead++;
            return Task.FromResult(new RepoRefs(new HeadState("main", Head), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Head, IsHead: true)], [], Remotes));
        }

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => new QuietWatcher();

        public override Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default)
        {
            Fetches.Add((remote, noAutoMaintenance));
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        private sealed class QuietWatcher : IRepositoryWatcher
        {
            public event EventHandler<RepositoryChangedEventArgs>? Changed
            {
                add { }
                remove { }
            }

            public void Start()
            {
            }

            public IDisposable Pause() => this;

            public void Dispose()
            {
            }
        }
    }
}
