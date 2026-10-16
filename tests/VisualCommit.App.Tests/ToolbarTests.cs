using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
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
    public async Task An_operation_in_progress_stops_Pull_and_Stash(InProgressKind kind, string reason)
    {
        // D119: a stash during a merge would drop the merge without a word.
        var status = Changed with { InProgress = new InProgressState(kind, Older, "Conflicting change", null, null, 0, false) };
        using var viewModel = await StartAsync(OpsRefs(), status);

        AssertReasons(viewModel.Toolbar, fetch: null, pull: reason, push: null, branch: null, stash: reason, pop: null);
    }

    [Fact]
    public async Task Conflicts_left_by_a_stash_stop_Stash_and_nothing_else()
    {
        var status = new WorkingTreeStatus([], [new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal));
        using var viewModel = await StartAsync(OpsRefs(), status);

        AssertReasons(viewModel.Toolbar, fetch: null, pull: null, push: null, branch: null, stash: "Resolve the conflicts first", pop: null);
    }

    [Fact]
    public void Several_reasons_for_Stash_give_the_first_in_its_order()
    {
        // 2.24's order (D119): HEAD unborn, an operation in progress, conflicts without one, nothing to stash.
        var merge = new InProgressState(InProgressKind.Merge, Older, "Conflicting change", null, null, 0, false);
        var unborn = new HeadState("main", null);
        var born = new HeadState("main", Main);
        var conflicted = new WorkingTreeStatus([], [new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("Make the first commit first", ToolbarViewModel.StashReason(unborn, conflicted with { InProgress = merge }));
        Assert.Equal("Make the first commit first", ToolbarViewModel.StashReason(unborn, WorkingTreeStatus.Clean));
        Assert.Equal("Finish or abort the merge first", ToolbarViewModel.StashReason(born, conflicted with { InProgress = merge }));
        Assert.Equal("Finish or abort the merge first", ToolbarViewModel.StashReason(born, WorkingTreeStatus.Clean with { InProgress = merge }));
        Assert.Equal("Finish or abort the merge first", ToolbarViewModel.StashReason(born, Changed with { InProgress = merge }));
        Assert.Equal("Finish or abort the git am first", ToolbarViewModel.StashReason(born, WorkingTreeStatus.Clean with { InProgress = merge with { Kind = InProgressKind.ApplyMailbox } }));
        Assert.Equal("Resolve the conflicts first", ToolbarViewModel.StashReason(born, conflicted));
        Assert.Equal("There are no changes to stash", ToolbarViewModel.StashReason(born, WorkingTreeStatus.Clean));
        Assert.Null(ToolbarViewModel.StashReason(born, Changed));
    }

    [Fact]
    public async Task An_operation_in_progress_with_nothing_to_stash_gives_Stash_the_operations_reason()
    {
        // A merge resolved to "ours" leaves nothing changed, and git's stash would still drop the merge (D119).
        var status = WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Merge, Older, "Merge branch 'feature/conflict'", null, null, 0, false) };
        using var viewModel = await StartAsync(OpsRefs(), status);

        Assert.False(viewModel.Toolbar.CanStash);
        Assert.Equal("Finish or abort the merge first", viewModel.Toolbar.StashToolTip);
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

    [Fact]
    public void A_tab_without_a_repository_has_Undo_Redo_and_Search_disabled_without_a_reason()
    {
        var toolbar = new ToolbarViewModel(null);

        Assert.Equal(
            (false, (string?)null, false, (string?)null, false, (string?)null),
            (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip, toolbar.CanSearch, toolbar.SearchToolTip));
        Assert.False(toolbar.UndoCommand.CanExecute(null));
        Assert.False(toolbar.RedoCommand.CanExecute(null));
        Assert.False(toolbar.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task With_a_repository_Search_is_enabled_with_its_key_and_Undo_and_Redo_have_nothing_to_do()
    {
        // docs/phase-5-design.md 1.1 and 7.4: with nothing in the undo history, Undo and Redo are
        // each disabled with its reason 4; Search needs only the repository (11.2).
        var repository = new StateRepository(OpsRefs(), WorkingTreeStatus.Clean);
        using var viewModel = Open(repository);
        Assert.Equal((ToolbarViewModel.SearchText, true), (viewModel.Toolbar.SearchToolTip, viewModel.Toolbar.CanSearch));

        await viewModel.StartAsync();

        var toolbar = viewModel.Toolbar;
        Assert.Equal((false, "Nothing to undo", false, "Nothing to redo"), (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip));
        Assert.Equal((ToolbarViewModel.SearchText, true), (toolbar.SearchToolTip, toolbar.CanSearch));
        Assert.True(toolbar.SearchCommand.CanExecute(null));

        // Ctrl+F, or Cmd+F where the platform's command modifier is Cmd (macOS).
        Assert.Contains(ToolbarViewModel.SearchGestureText, new[] { "Ctrl+F", "Cmd+F" });
        Assert.Equal($"Search commits ({ToolbarViewModel.SearchGestureText})", ToolbarViewModel.SearchText);
    }

    [Fact]
    public async Task Undo_and_Redo_show_the_state_the_undo_history_gives_and_call_their_actions()
    {
        using var viewModel = await StartAsync(OpsRefs(), Changed);
        var toolbar = viewModel.Toolbar;
        var actions = new FakeActions();
        toolbar.Actions = actions;
        toolbar.UndoStates = () => new UndoAvailability(true, "Undo commit \"Describe the lines\"", false, "Nothing to redo");

        toolbar.UpdateUndoStates();
        Assert.Equal((true, "Undo commit \"Describe the lines\"", false, "Nothing to redo"), (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip));
        Assert.True(toolbar.UndoCommand.CanExecute(null));
        Assert.False(toolbar.RedoCommand.CanExecute(null));
        await toolbar.UndoCommand.ExecuteAsync(null);

        toolbar.UndoStates = () => new UndoAvailability(false, "Nothing to undo", true, "Redo commit \"Describe the lines\"");
        toolbar.UpdateUndoStates();
        Assert.Equal((false, "Nothing to undo", true, "Redo commit \"Describe the lines\""), (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip));
        await toolbar.RedoCommand.ExecuteAsync(null);

        // A state given without a tooltip (not known yet) is its reason 4, never an enabled button without words.
        toolbar.UndoStates = () => UndoAvailability.Disabled with { CanUndo = true };
        toolbar.UpdateUndoStates();
        Assert.Equal((false, "Nothing to undo", false, "Nothing to redo"), (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip));

        Assert.Equal(["undo", "redo"], actions.Calls);
    }

    [Fact]
    public async Task Undo_and_Redo_are_read_again_when_the_queue_changes_without_a_refresh()
    {
        // 7.4's reason 1 and the entry an action adds show as soon as the queue has started, waited
        // or finished an operation: the toolbar listens to the queue itself.
        using var viewModel = await StartAsync(OpsRefs(), WorkingTreeStatus.Clean);
        var toolbar = viewModel.Toolbar;
        var reads = 0;
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        toolbar.UndoStates = () =>
        {
            Interlocked.Increment(ref reads);
            return viewModel.Queue.Current is { } current
                ? new UndoAvailability(false, $"Wait for {current.Title} to finish", false, $"Wait for {current.Title} to finish")
                : new UndoAvailability(true, "Undo creation of hotfix", false, "Nothing to redo");
        };

        var operation = viewModel.Queue.RunAsync(new GitOperation("Create branch hotfix", "Creating hotfix…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await running.Task;
            return OperationReport.Done("Created hotfix");
        }));
        await WaitForAsync(() => toolbar.UndoToolTip == "Wait for Create branch hotfix to finish", "the running operation's reason");
        Assert.False(toolbar.CanUndo);

        running.SetResult();
        await operation.WaitAsync(TestContext.Current.CancellationToken);
        await WaitForAsync(() => toolbar.CanUndo, "Undo after the operation");
        Assert.Equal("Undo creation of hotfix", toolbar.UndoToolTip);
        Assert.True(reads >= 2, $"The states were read {reads} times: at the start and after the end at least.");
    }

    [Fact]
    public async Task Search_is_disabled_while_the_interactive_rebase_list_is_open_and_runs_its_action_otherwise()
    {
        using var viewModel = await StartAsync(OpsRefs(), WorkingTreeStatus.Clean);
        var toolbar = viewModel.Toolbar;
        var searches = 0;
        toolbar.SearchAction = () => searches++;
        toolbar.SearchCommand.Execute(null);
        Assert.Equal(1, searches);

        var request = new InteractiveRebaseRequest(new RebaseTarget(Older, "8beca34"), "refs/heads/main", FromHere: true, CheckoutFirst: false);
        await viewModel.ShowInteractiveRebaseAsync(request, new RebaseList(Older, Main, [new RebaseListEntry(Main, "Main work", "Test Author", "Main work\n")], 0));

        Assert.True(viewModel.IsInteractiveRebaseOpen);
        Assert.Equal((false, "Close the interactive rebase first"), (toolbar.CanSearch, toolbar.SearchToolTip));
        Assert.False(toolbar.SearchCommand.CanExecute(null));

        viewModel.InteractiveRebase!.CancelCommand.Execute(null);
        await WaitForAsync(() => !viewModel.IsInteractiveRebaseOpen, "the list closed");
        Assert.Equal((true, ToolbarViewModel.SearchText), (toolbar.CanSearch, toolbar.SearchToolTip));
    }

    [Fact]
    public async Task A_Search_that_fails_is_logged_and_the_toolbar_goes_on()
    {
        var log = new RecordingLog();
        var repository = new StateRepository(OpsRefs(), WorkingTreeStatus.Clean);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), log);
        await viewModel.StartAsync();
        viewModel.Toolbar.SearchAction = () => throw new InvalidOperationException("boom");

        viewModel.Toolbar.SearchCommand.Execute(null);

        Assert.Contains(log.Messages, message => message.Contains("the toolbar's Search failed", StringComparison.Ordinal));
        Assert.True(viewModel.Toolbar.CanSearch);
    }

    [AvaloniaFact]
    public async Task In_the_window_the_buttons_follow_the_active_tab_and_a_disabled_one_shows_its_reason()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null], activeTab: 1);
        await app.WaitUntilReadyAsync();

        // An empty tab: only Theme is enabled, as in phase 0, and no button has a tooltip or a help text.
        foreach (var id in new[] { "UndoButton", "RedoButton", "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "SearchButton" })
        {
            var button = (Button)app.FindByAutomationId(id);
            Assert.False(button.IsEffectivelyEnabled, $"{id} is disabled in an empty tab.");
            Assert.Null(ToolTip.GetTip(button));
            Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(button)), $"{id} has a help text in an empty tab.");
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

        // Phase 5 (docs/phase-5-design.md 1.1, 7.4, 14.1): with nothing recorded, Undo and Redo are
        // disabled and say so; Search is enabled with its key. Their tooltips are their help texts.
        foreach (var (id, tip) in new[] { ("UndoButton", "Nothing to undo"), ("RedoButton", "Nothing to redo") })
        {
            var button = (ToolbarButton)app.FindByAutomationId(id);
            Assert.False(button.IsEffectivelyEnabled, $"{id} is enabled.");
            Assert.Equal(tip, ToolTip.GetTip(button));
            Assert.Equal(tip, AutomationProperties.GetHelpText(button));
            Assert.Equal(Brush(app, "VcTextDisabledBrush"), ((ISolidColorBrush)button.Foreground!).Color);
        }

        var search = (ToolbarButton)app.FindByAutomationId("SearchButton");
        var searchText = $"Search commits ({(app.CommandModifier == Avalonia.Input.KeyModifiers.Meta ? "Cmd" : "Ctrl")}+F)";
        Assert.True(search.IsEffectivelyEnabled);
        Assert.Equal(searchText, ToolTip.GetTip(search));
        Assert.Equal(searchText, AutomationProperties.GetHelpText(search));
        Assert.Equal(Brush(app, "VcTextPrimaryBrush"), ((ISolidColorBrush)search.Foreground!).Color);

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
            TestPictures.Save(app, $"toolbar-stash-tooltip-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
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

    /// <summary>Waits for a condition that a post to the test's context or the thread pool makes true.</summary>
    private static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10))
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
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

        public Task UndoAsync() => Record("undo");

        public Task RedoAsync() => Record("redo");

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
