using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The toolbar with a repository (docs/phase-3-design.md 4d, D90): which buttons are enabled,
/// the reason a disabled one gives (the first that holds, in 4d's order), and what a click calls.
/// </summary>
public class ToolbarTests
{
    private const string Main = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
    private const string Older = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";

    private static readonly StashEntry Experiment = new(0, "0e95aca1", Main, "On main: Experiment", "Test Author", "author@example.com", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static readonly WorkingTreeStatus Changed = new([], [new ChangedFile("README.md", FileChangeKind.Modified)], new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public void A_tab_without_a_repository_has_every_button_disabled_without_a_reason()
    {
        var toolbar = new ToolbarViewModel(null);

        AssertStates(toolbar, fetch: null, pull: null, push: null, branch: null, stash: null, pop: null, enabled: false);
        Assert.Null(toolbar.Actions);
    }

    [Fact]
    public async Task Before_the_refs_are_read_every_button_is_disabled_without_a_reason()
    {
        var repository = new StateRepository(OpsRefs(), WorkingTreeStatus.Clean);
        using var viewModel = Open(repository);

        AssertStates(viewModel.Toolbar, fetch: null, pull: null, push: null, branch: null, stash: null, pop: null, enabled: false);

        await viewModel.StartAsync();
        Assert.True(viewModel.Toolbar.CanFetch);
    }

    [Fact]
    public async Task A_clean_branch_with_its_upstream_and_a_stash_can_do_everything_but_stash()
    {
        using var viewModel = await StartAsync(OpsRefs(), WorkingTreeStatus.Clean);

        AssertReasons(viewModel.Toolbar, fetch: null, pull: null, push: null, branch: null, stash: "There are no changes to stash", pop: null);
    }

    [Fact]
    public async Task Changes_enable_Stash_and_its_reason_follows_the_status()
    {
        var repository = new StateRepository(OpsRefs(), Changed);
        using var viewModel = Open(repository);
        await viewModel.StartAsync();
        Assert.True(viewModel.Toolbar.CanStash);
        Assert.Null(viewModel.Toolbar.StashToolTip);

        repository.Status = WorkingTreeStatus.Clean;
        await viewModel.RefreshStatusAsync();

        Assert.False(viewModel.Toolbar.CanStash);
        Assert.Equal("There are no changes to stash", viewModel.Toolbar.StashToolTip);
    }

    [Fact]
    public async Task An_unborn_branch_with_a_remote_and_an_untracked_file_can_only_fetch()
    {
        var refs = new RepoRefs(new HeadState("main", null), [], [], ["origin"]);
        var untracked = new WorkingTreeStatus([], [new ChangedFile("notes.txt", FileChangeKind.Added)], new HashSet<string>(["notes.txt"], StringComparer.Ordinal));
        using var viewModel = await StartAsync(refs, untracked);

        AssertReasons(
            viewModel.Toolbar,
            fetch: null,
            pull: "main has no upstream: set one first",
            push: "Make the first commit first",
            branch: "Make the first commit first",
            stash: "Make the first commit first",
            pop: "There are no stashes");
    }

    [Fact]
    public async Task Without_remotes_Pull_gives_the_first_of_its_reasons()
    {
        var refs = new RepoRefs(new HeadState("main", Main), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Main, IsHead: true)], [], []);
        using var viewModel = await StartAsync(refs, WorkingTreeStatus.Clean);

        AssertReasons(
            viewModel.Toolbar,
            fetch: "This repository has no remotes",
            pull: "main has no upstream: set one first",
            push: "Add a remote first",
            branch: null,
            stash: "There are no changes to stash",
            pop: "There are no stashes");
    }

    [Fact]
    public async Task A_detached_HEAD_cannot_pull_or_push()
    {
        var refs = new RepoRefs(new HeadState(null, Main), [Tracking("main", Main, isHead: false)], [], ["origin"]);
        using var viewModel = await StartAsync(refs, WorkingTreeStatus.Clean);

        AssertReasons(
            viewModel.Toolbar,
            fetch: null,
            pull: "HEAD is detached: check out a branch to pull",
            push: "HEAD is detached: check out a branch to push",
            branch: null,
            stash: "There are no changes to stash",
            pop: "There are no stashes");
    }

    [Fact]
    public async Task A_branch_with_a_local_upstream_pulls_but_does_not_push()
    {
        var loc = new GitRef("refs/heads/loc", "loc", RefKind.LocalBranch, Older, Upstream: "main", Behind: 1, IsHead: true, UpstreamRef: "refs/heads/main", UpstreamRemote: ".", UpstreamRemoteRef: "refs/heads/main");
        var refs = new RepoRefs(new HeadState("loc", Older), [loc, Tracking("main", Main, isHead: false)], [], ["origin"]);
        using var viewModel = await StartAsync(refs, WorkingTreeStatus.Clean);

        Assert.True(viewModel.Toolbar.CanPull);
        Assert.Equal("loc tracks a local branch", viewModel.Toolbar.PushToolTip);
        Assert.False(viewModel.Toolbar.CanPush);
    }

    [Fact]
    public async Task A_local_upstream_pulls_even_without_remotes()
    {
        var loc = new GitRef("refs/heads/loc", "loc", RefKind.LocalBranch, Older, Upstream: "main", IsHead: true, UpstreamRef: "refs/heads/main", UpstreamRemote: ".", UpstreamRemoteRef: "refs/heads/main");
        var refs = new RepoRefs(new HeadState("loc", Older), [loc], [], []);
        using var viewModel = await StartAsync(refs, WorkingTreeStatus.Clean);

        Assert.True(viewModel.Toolbar.CanPull);
        Assert.Equal("Add a remote first", viewModel.Toolbar.PushToolTip);
    }

    [Fact]
    public async Task A_gone_upstream_stops_Pull_but_not_Push()
    {
        var gone = Tracking("main", Main, isHead: true) with { UpstreamGone = true };
        var refs = new RepoRefs(new HeadState("main", Main), [gone], [], ["origin"]);
        using var viewModel = await StartAsync(refs, WorkingTreeStatus.Clean);

        Assert.Equal("origin/main no longer exists", viewModel.Toolbar.PullToolTip);
        Assert.True(viewModel.Toolbar.CanPush);
    }

    [Theory]
    [InlineData(InProgressKind.Merge, "Finish or abort the merge first")]
    [InlineData(InProgressKind.CherryPick, "Finish or abort the cherry-pick first")]
    [InlineData(InProgressKind.Revert, "Finish or abort the revert first")]
    [InlineData(InProgressKind.Rebase, "Finish or abort the rebase first")]
    public async Task An_operation_in_progress_stops_Pull_only(InProgressKind kind, string reason)
    {
        var status = Changed with { InProgress = new InProgressState(kind, Older, "Conflicting change", null, null, 0, false) };
        using var viewModel = await StartAsync(OpsRefs(), status);

        AssertReasons(viewModel.Toolbar, fetch: null, pull: reason, push: null, branch: null, stash: null, pop: null);
    }

    [Fact]
    public async Task The_states_follow_a_refresh_of_the_refs_even_when_only_an_upstream_changed()
    {
        var topic = new GitRef("refs/heads/topic", "topic", RefKind.LocalBranch, Main, IsHead: true);
        var repository = new StateRepository(new RepoRefs(new HeadState("topic", Main), [topic], [], ["origin"]), WorkingTreeStatus.Clean);
        using var viewModel = Open(repository);
        await viewModel.StartAsync();
        Assert.Equal("topic has no upstream: set one first", viewModel.Toolbar.PullToolTip);

        repository.Refs = repository.Refs with
        {
            Refs = [topic with { Upstream = "origin/published", UpstreamRef = "refs/remotes/origin/published", UpstreamRemote = "origin", UpstreamRemoteRef = "refs/heads/published" }],
        };
        await viewModel.RefreshAsync();

        Assert.True(viewModel.Toolbar.CanPull);
        Assert.Null(viewModel.Toolbar.PullToolTip);
    }

    [Fact]
    public async Task Each_button_calls_its_action_and_stays_enabled_while_it_runs()
    {
        using var viewModel = await StartAsync(OpsRefs(), Changed);
        var actions = new FakeActions();
        viewModel.Toolbar.Actions = actions;
        var toolbar = viewModel.Toolbar;

        actions.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetching = toolbar.FetchCommand.ExecuteAsync(null);
        Assert.True(toolbar.FetchCommand.CanExecute(null), "Fetch stays enabled while its fetch runs.");
        var again = toolbar.FetchCommand.ExecuteAsync(null);
        actions.Hold.SetResult();
        await Task.WhenAll(fetching, again);

        await toolbar.PullCommand.ExecuteAsync(null);
        await toolbar.PushCommand.ExecuteAsync(null);
        await toolbar.BranchCommand.ExecuteAsync(null);
        await toolbar.StashCommand.ExecuteAsync(null);
        await toolbar.PopCommand.ExecuteAsync(null);

        Assert.Equal(["fetch", "fetch", "pull", "push", "branch", "stash", "pop"], actions.Calls);
    }

    [Fact]
    public async Task An_action_that_throws_is_logged_and_the_toolbar_goes_on()
    {
        var log = new RecordingLog();
        var repository = new StateRepository(OpsRefs(), WorkingTreeStatus.Clean);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), log);
        await viewModel.StartAsync();
        viewModel.Toolbar.Actions = new FakeActions { Failure = new InvalidOperationException("boom") };

        await viewModel.Toolbar.FetchCommand.ExecuteAsync(null);

        Assert.Contains(log.Messages, message => message.Contains("the toolbar's Fetch failed", StringComparison.Ordinal));
        Assert.True(viewModel.Toolbar.CanFetch);
    }

    [Fact]
    public async Task Without_actions_of_its_own_the_toolbar_asks_the_repositorys_operations()
    {
        using var viewModel = await StartAsync(OpsRefs(), WorkingTreeStatus.Clean);

        // Until RepositoryOperations implements IToolbarActions (WP7) there is nothing to call:
        // a click is logged and does nothing.
        Assert.Equal((object)viewModel.Operations as IToolbarActions, viewModel.Toolbar.Actions);
        await viewModel.Toolbar.FetchCommand.ExecuteAsync(null);
    }

    [AvaloniaFact]
    public async Task In_the_window_the_buttons_follow_the_active_tab_and_a_disabled_one_shows_its_reason()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null], activeTab: 1);
        await app.WaitUntilReadyAsync();

        // An empty tab: only Theme is enabled, as in phase 0, and no button has a tooltip.
        foreach (var id in new[] { "UndoButton", "RedoButton", "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "SearchButton" })
        {
            var button = (Button)app.FindByAutomationId(id);
            Assert.False(button.IsEffectivelyEnabled, $"{id} is disabled in an empty tab.");
            Assert.Null(ToolTip.GetTip(button));
        }

        Assert.True(app.FindByAutomationId("ThemeSwitch").IsEffectivelyEnabled);

        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[0]);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0 && repository.Refs is not null, "the graph");
        app.Settle();

        foreach (var id in new[] { "FetchButton", "PullButton", "PushButton", "BranchButton", "PopButton" })
        {
            var button = (Button)app.FindByAutomationId(id);
            Assert.True(button.IsEffectivelyEnabled, $"{id} is enabled.");
            Assert.Null(ToolTip.GetTip(button));
            Assert.Equal(Brush(app, "VcTextPrimaryBrush"), ((ISolidColorBrush)button.Foreground!).Color);
        }

        foreach (var id in new[] { "UndoButton", "RedoButton", "SearchButton" })
        {
            Assert.False(app.FindByAutomationId(id).IsEffectivelyEnabled);
            Assert.Null(ToolTip.GetTip(app.FindByAutomationId(id)));
        }

        var stash = (ToolbarButton)app.FindByAutomationId("StashButton");
        Assert.False(stash.IsEffectivelyEnabled);
        Assert.Equal("There are no changes to stash", ToolTip.GetTip(stash));
        Assert.Equal(Brush(app, "VcTextDisabledBrush"), ((ISolidColorBrush)stash.Foreground!).Color);

        var actions = new FakeActions();
        repository.Toolbar.Actions = actions;
        app.Click(app.FindByAutomationId("FetchButton"));
        app.Click(app.FindByAutomationId("PopButton"));
        await app.WaitForAsync(() => actions.Calls.Count == 2, "the clicks");
        Assert.Equal(["fetch", "pop"], actions.Calls);

        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.MoveMouse(new Avalonia.Point(700, 400));
            app.MoveMouse(stash);
            await app.WaitForAsync(() => ToolTip.GetIsOpen(stash), "the tooltip", TimeSpan.FromSeconds(5));
            app.Settle();
            TestPictures.Save(app.Capture(), $"toolbar-stash-tooltip-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
        }
    }

    private static GitRef Tracking(string name, string sha, bool isHead) =>
        new($"refs/heads/{name}", name, RefKind.LocalBranch, sha, Upstream: $"origin/{name}", IsHead: isHead, UpstreamRef: $"refs/remotes/origin/{name}", UpstreamRemote: "origin", UpstreamRemoteRef: $"refs/heads/{name}");

    private static RepoRefs OpsRefs() => new(
        new HeadState("main", Main),
        [Tracking("main", Main, isHead: true), new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Main, RemoteName: "origin")],
        [Experiment],
        ["origin"]);

    private static RepositoryViewModel Open(StateRepository repository) =>
        new(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));

    private static async Task<RepositoryViewModel> StartAsync(RepoRefs refs, WorkingTreeStatus status)
    {
        var viewModel = Open(new StateRepository(refs, status));
        await viewModel.StartAsync();
        return viewModel;
    }

    private static void AssertReasons(ToolbarViewModel toolbar, string? fetch, string? pull, string? push, string? branch, string? stash, string? pop) =>
        AssertStates(toolbar, fetch, pull, push, branch, stash, pop, enabled: true);

    private static void AssertStates(ToolbarViewModel toolbar, string? fetch, string? pull, string? push, string? branch, string? stash, string? pop, bool enabled)
    {
        IEnumerable<string?> expected = [fetch, pull, push, branch, stash, pop];
        IEnumerable<string?> actual = [toolbar.FetchToolTip, toolbar.PullToolTip, toolbar.PushToolTip, toolbar.BranchToolTip, toolbar.StashToolTip, toolbar.PopToolTip];
        Assert.Equal(expected, actual);
        Assert.Equal(
            [enabled && fetch is null, enabled && pull is null, enabled && push is null, enabled && branch is null, enabled && stash is null, enabled && pop is null],
            [toolbar.FetchCommand.CanExecute(null), toolbar.PullCommand.CanExecute(null), toolbar.PushCommand.CanExecute(null), toolbar.BranchCommand.CanExecute(null), toolbar.StashCommand.CanExecute(null), toolbar.PopCommand.CanExecute(null)]);
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    /// <summary>The toolbar's actions, recorded.</summary>
    private sealed class FakeActions : IToolbarActions
    {
        public List<string> Calls { get; } = [];

        /// <summary>When set, Fetch waits for it.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public Exception? Failure { get; init; }

        public async Task FetchAllAsync()
        {
            await Record("fetch");
            if (Hold is { } hold)
            {
                await hold.Task;
            }
        }

        public Task PullAsync() => Record("pull");

        public Task PushAsync() => Record("push");

        public Task CreateBranchAtHeadAsync() => Record("branch");

        public Task StashAsync() => Record("stash");

        public Task PopAsync() => Record("pop");

        private Task Record(string call)
        {
            Calls.Add(call);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    /// <summary>A repository without commits whose refs and status a test sets.</summary>
    private sealed class StateRepository(RepoRefs refs, WorkingTreeStatus status) : FakeRepositoryBase
    {
        public RepoRefs Refs { get; set; } = refs;

        public WorkingTreeStatus Status { get; set; } = status;

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "toolbar");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "toolbar";

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Refs);

        public override Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => new QuietWatcher();

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
