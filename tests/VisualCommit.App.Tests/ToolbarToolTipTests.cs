using System.ComponentModel;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The toolbar's and the commit button's tooltips with their keys (docs/phase-6-design.md 6.8, 6.12,
/// D199, D215): an enabled button says what it does with its key, a disabled one its reason alone;
/// the reasons alone are the <c>XDisabledReason</c> properties, which the menus read; the key in
/// Windows' and Linux's words and in macOS's. Without a UI.
/// </summary>
public class ToolbarToolTipTests
{
    private const string Main = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    private static readonly StashEntry Experiment = new(0, "0e95aca1", Main, "On main: Experiment", "Test Author", "author@example.com", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static readonly WorkingTreeStatus Changed = new([], [new ChangedFile("README.md", FileChangeKind.Modified)], new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public async Task An_enabled_button_says_what_it_does_with_its_key_in_each_platforms_words()
    {
        using var viewModel = await StartAsync(Refs(Tracking("main", "origin", "refs/heads/main")), Changed);
        var toolbar = viewModel.Toolbar;
        Assert.Equal(
            ["Fetch all remotes (Ctrl+Shift+F)", "Pull from origin/main (Ctrl+Shift+L)", "Push main to origin/main (Ctrl+Shift+K)", "Create a branch at HEAD (Ctrl+B)", "Stash your changes (Ctrl+Shift+S)", "Pop the newest stash (Ctrl+Shift+O)", "Search commits (Ctrl+F)"],
            ToolTips(toolbar));

        toolbar.KeyTexts = new KeyTexts(CommandKey.Cmd);
        Assert.Equal(
            ["Fetch all remotes (Cmd+Shift+F)", "Pull from origin/main (Cmd+Shift+L)", "Push main to origin/main (Cmd+Shift+K)", "Create a branch at HEAD (Cmd+B)", "Stash your changes (Cmd+Shift+S)", "Pop the newest stash (Cmd+Shift+O)", "Search commits (Cmd+F)"],
            ToolTips(toolbar));

        // Every reason null while enabled, and the menus give the enabled buttons none (6.8).
        Assert.All(Reasons(toolbar), Assert.Null);
        var state = ToolbarMenuState.Of(toolbar);
        Assert.All(new[] { state.Fetch, state.Pull, state.Push, state.Branch, state.Stash, state.Pop }, availability => Assert.Equal(new ToolbarAvailability(true, null), availability));
    }

    [Fact]
    public async Task Pull_and_Push_name_the_upstream_by_its_tracking_ref_and_Push_without_one_a_remote()
    {
        // GD15: a branch that tracks refs/pull/7/head on upstream is named by its tracking ref.
        var pr = new GitRef("refs/heads/pr/7", "pr/7", RefKind.LocalBranch, Main, Upstream: "upstream/pr/7", IsHead: true, UpstreamRef: "refs/remotes/upstream/pr/7", UpstreamRemote: "upstream", UpstreamRemoteRef: "refs/pull/7/head");
        using var tracking = await StartAsync(new RepoRefs(new HeadState("pr/7", Main), [pr], [], ["upstream"]), WorkingTreeStatus.Clean);
        Assert.Equal("Pull from upstream/pr/7 (Ctrl+Shift+L)", tracking.Toolbar.PullToolTip);

        // Its Push is disabled: refs/pull/7/head is no branch (R4); its reason names the upstream so too.
        Assert.Equal((false, "upstream/pr/7 is refs/pull/7/head on upstream, not a branch"), (tracking.Toolbar.CanPush, tracking.Toolbar.PushToolTip));

        // A renamed branch's Push names it by its tracking ref (fork's main fetched into fork/up/main).
        var forked = new GitRef("refs/heads/forked", "forked", RefKind.LocalBranch, Main, Upstream: "fork/up/main", IsHead: true, UpstreamRef: "refs/remotes/fork/up/main", UpstreamRemote: "fork", UpstreamRemoteRef: "refs/heads/main");
        using var renamed = await StartAsync(new RepoRefs(new HeadState("forked", Main), [forked], [], ["fork"]), WorkingTreeStatus.Clean);
        Assert.Equal("Push forked to fork/up/main (Ctrl+Shift+K)", renamed.Toolbar.PushToolTip);

        var topic = new GitRef("refs/heads/topic", "topic", RefKind.LocalBranch, Main, IsHead: true);
        using var untracked = await StartAsync(new RepoRefs(new HeadState("topic", Main), [topic], [], ["origin"]), WorkingTreeStatus.Clean);
        Assert.Equal("Push topic to a remote (Ctrl+Shift+K)", untracked.Toolbar.PushToolTip);
        untracked.Toolbar.KeyTexts = new KeyTexts(CommandKey.Cmd);
        Assert.Equal("Push topic to a remote (Cmd+Shift+K)", untracked.Toolbar.PushToolTip);
    }

    [Fact]
    public async Task A_disabled_button_keeps_its_reason_alone_which_is_its_disabled_reason()
    {
        var topic = new GitRef("refs/heads/topic", "topic", RefKind.LocalBranch, Main, IsHead: true);
        using var viewModel = await StartAsync(new RepoRefs(new HeadState("topic", Main), [topic], [], []), WorkingTreeStatus.Clean);
        var toolbar = viewModel.Toolbar;
        string?[] reasons = ["This repository has no remotes", "topic has no upstream: set one first", "Add a remote first", null, "There are no changes to stash", "There are no stashes"];
        Assert.Equal(reasons, Reasons(toolbar).Take(6));
        Assert.Equal(["This repository has no remotes", "topic has no upstream: set one first", "Add a remote first", "Create a branch at HEAD (Ctrl+B)", "There are no changes to stash", "There are no stashes"], ToolTips(toolbar).Take(6));
        var state = ToolbarMenuState.Of(toolbar);
        Assert.Equal(new ToolbarAvailability(false, "There are no stashes"), state.Pop);
        Assert.Equal(new ToolbarAvailability(true, null), state.Branch);

        // Undo and Redo: the reason alone while disabled, what they would do with the key while enabled.
        Assert.Equal(("Nothing to undo", "Nothing to undo", "Nothing to redo", "Nothing to redo"), (toolbar.UndoToolTip, toolbar.UndoDisabledReason, toolbar.RedoToolTip, toolbar.RedoDisabledReason));
        Assert.Null(toolbar.UndoText);
        toolbar.UndoStates = () => new UndoAvailability(true, "Undo commit \"Fix the parser\"", true, "Redo checkout of main");
        toolbar.UpdateUndoStates();
        Assert.Equal(("Undo commit \"Fix the parser\" (Ctrl+Z)", null, "Redo checkout of main (Ctrl+Y)", null), (toolbar.UndoToolTip, toolbar.UndoDisabledReason, toolbar.RedoToolTip, toolbar.RedoDisabledReason));
        Assert.Equal(("Undo commit \"Fix the parser\"", "Redo checkout of main"), (toolbar.UndoText, toolbar.RedoText));
        toolbar.KeyTexts = new KeyTexts(CommandKey.Cmd);
        Assert.Equal(("Undo commit \"Fix the parser\" (Cmd+Z)", "Redo checkout of main (Cmd+Shift+Z)"), (toolbar.UndoToolTip, toolbar.RedoToolTip));
    }

    [Fact]
    public async Task Branch_without_a_first_commit_keeps_its_reason_alone_which_the_menus_read()
    {
        // 6.8, 6.12: an unborn HEAD disables Branch with its reason, its tooltip that reason alone.
        using var viewModel = await StartAsync(new RepoRefs(new HeadState("main", null), [], [], []), Changed);
        var toolbar = viewModel.Toolbar;
        Assert.Equal((false, "Make the first commit first", "Make the first commit first"), (toolbar.CanBranch, toolbar.BranchDisabledReason, toolbar.BranchToolTip));
        Assert.Equal(new ToolbarAvailability(false, "Make the first commit first"), ToolbarMenuState.Of(toolbar).Branch);

        // Search needs only the repository: no reason (11.2).
        Assert.Equal((true, (string?)null, "Search commits (Ctrl+F)"), (toolbar.CanSearch, toolbar.SearchDisabledReason, toolbar.SearchToolTip));
    }

    [Fact]
    public void A_dashboards_Fetch_and_Pull_name_the_workspace_with_their_keys_in_the_toolbars_words()
    {
        // 4.10, 6.8: the dashboard's toolbar's texts take its keys, not the app's (the review's finding 9).
        var toolbar = ToolbarViewModel.ForDashboard(new FixedDashboard());
        Assert.Equal(("Fetch every repository of Web (Ctrl+Shift+F)", "Pull every repository of Web (Ctrl+Shift+L)"), (toolbar.FetchToolTip, toolbar.PullToolTip));

        toolbar.KeyTexts = new KeyTexts(CommandKey.Cmd);
        Assert.Equal(("Fetch every repository of Web (Cmd+Shift+F)", "Pull every repository of Web (Cmd+Shift+L)"), (toolbar.FetchToolTip, toolbar.PullToolTip));
        Assert.Equal((null, null), (toolbar.FetchDisabledReason, toolbar.PullDisabledReason));
    }

    [Fact]
    public void Without_a_repository_nothing_has_a_tooltip_or_a_reason()
    {
        var toolbar = new ToolbarViewModel(null);
        Assert.All(ToolTips(toolbar), Assert.Null);
        Assert.All(Reasons(toolbar), Assert.Null);
        Assert.Null(toolbar.UndoToolTip);
    }

    [Fact]
    public void The_commit_buttons_reason_is_its_tooltip_while_disabled_and_its_text_with_the_key_while_enabled()
    {
        var pickInList = new InProgressState(InProgressKind.CherryPick, Main, "Conflicting change", null, null, 1, false) { Sequenced = true, HasPickHead = true };
        var rebase = new InProgressState(InProgressKind.Rebase, Main, "First change", "feature/rebase", new string('d', 40), 1, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 2 };
        var empty = new InProgressState(InProgressKind.CherryPick, Main, "Alpha one", null, null, 0, true) { HasPickHead = true };
        var merge = new InProgressState(InProgressKind.Merge, Main, "Merge branch 'feature/conflict'", null, null, 0, false);
        var conflicted = new ChangedFile("src/core.txt", FileChangeKind.Conflicted);
        var modified = new ChangedFile("README.md", FileChangeKind.Modified);
        var panel = new WorkingChangesViewModel(new QuietHost(), new MainWindowViewModelTests.FakeSettings());

        // Nothing staged, an empty summary: no reason and no tooltip (6.8).
        panel.Update(Status([], [modified], InProgressState.None), hasHead: true);
        Assert.Equal(("Stage files to commit", null, null), (panel.CommitButtonText, panel.CommitDisabledReason, panel.CommitToolTip));

        // One file staged and a summary: enabled, its text with Ctrl+Enter (D199), Cmd+Enter on macOS.
        panel.Update(Status([modified], [], InProgressState.None), hasHead: true);
        Assert.Null(panel.CommitToolTip);
        panel.Summary = "Note more core work";
        Assert.Equal(("Commit 1 file", null, "Commit 1 file (Ctrl+Enter)"), (panel.CommitButtonText, panel.CommitDisabledReason, panel.CommitToolTip));
        panel.KeyTexts = new KeyTexts(CommandKey.Cmd);
        Assert.Equal("Commit 1 file (Cmd+Enter)", panel.CommitToolTip);
        panel.KeyTexts = new KeyTexts(CommandKey.Ctrl);

        // A merge resolved: "Commit merge (Ctrl+Enter)".
        panel.Update(Status([], [], merge), hasHead: true);
        Assert.Equal(("Commit merge", null, "Commit merge (Ctrl+Enter)"), (panel.CommitButtonText, panel.CommitDisabledReason, panel.CommitToolTip));

        // The empty pick: its reason, as today's tooltip.
        panel.Update(Status([], [], empty), hasHead: true);
        Assert.Equal(("This cherry-pick changes nothing: skip it", "This cherry-pick changes nothing: skip it"), (panel.CommitDisabledReason, panel.CommitToolTip));

        // A list with a conflict: Continue's reason; resolved, "Continue cherry-pick (Ctrl+Enter)".
        panel.Update(Status([], [conflicted], pickInList), hasHead: true);
        Assert.Equal(("Continue cherry-pick", "Resolve the conflicts first (1 file)", "Resolve the conflicts first (1 file)"), (panel.CommitButtonText, panel.CommitDisabledReason, panel.CommitToolTip));
        panel.Update(Status([modified], [], pickInList), hasHead: true);
        Assert.Equal((null, "Continue cherry-pick (Ctrl+Enter)"), (panel.CommitDisabledReason, panel.CommitToolTip));

        // A rebase stop resolved: "Continue rebase (Ctrl+Enter)".
        panel.Update(Status([modified], [], rebase), hasHead: true);
        Assert.Equal(("Continue rebase", null, "Continue rebase (Ctrl+Enter)"), (panel.CommitButtonText, panel.CommitDisabledReason, panel.CommitToolTip));
    }

    private static List<string?> ToolTips(ToolbarViewModel toolbar) =>
        [toolbar.FetchToolTip, toolbar.PullToolTip, toolbar.PushToolTip, toolbar.BranchToolTip, toolbar.StashToolTip, toolbar.PopToolTip, toolbar.SearchToolTip];

    private static List<string?> Reasons(ToolbarViewModel toolbar) =>
        [toolbar.FetchDisabledReason, toolbar.PullDisabledReason, toolbar.PushDisabledReason, toolbar.BranchDisabledReason, toolbar.StashDisabledReason, toolbar.PopDisabledReason, toolbar.SearchDisabledReason];

    private static GitRef Tracking(string name, string remote, string remoteRef) =>
        new($"refs/heads/{name}", name, RefKind.LocalBranch, Main, Upstream: $"{remote}/{name}", IsHead: true, UpstreamRef: $"refs/remotes/{remote}/{name}", UpstreamRemote: remote, UpstreamRemoteRef: remoteRef);

    private static RepoRefs Refs(GitRef head) => new(new HeadState(head.Name, Main), [head], [Experiment], ["origin"]);

    private static WorkingTreeStatus Status(IReadOnlyList<ChangedFile> staged, IReadOnlyList<ChangedFile> unstaged, InProgressState state) =>
        new(staged, unstaged, new HashSet<string>(StringComparer.Ordinal)) { InProgress = state };

    private static async Task<RepositoryViewModel> StartAsync(RepoRefs refs, WorkingTreeStatus status)
    {
        var viewModel = new RepositoryViewModel(new StateRepository(refs, status), new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        await viewModel.StartAsync();
        return viewModel;
    }

    /// <summary>A repository without commits whose refs and status the test gives.</summary>
    private sealed class StateRepository(RepoRefs refs, WorkingTreeStatus status) : FakeRepositoryBase
    {
        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "tooltips");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "tooltips";

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => Task.FromResult(refs);

        public override Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(status);

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

    /// <summary>The workspace "Web" whose Fetch all and Pull all can run, and never change.</summary>
    private sealed class FixedDashboard : IDashboardToolbar
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string WorkspaceName => "Web";

        public VisualCommit.Core.Logging.IAppLog Log => VisualCommit.Core.Logging.NullAppLog.Instance;

        public bool CanFetchAll => true;

        public string? FetchAllDisabledReason => null;

        public bool CanPullAll => true;

        public string? PullAllDisabledReason => null;

        public Task FetchAllAsync() => Task.CompletedTask;

        public Task PullAllAsync() => Task.CompletedTask;
    }

    /// <summary>A stage panel host that does nothing.</summary>
    private sealed class QuietHost : IWorkingChangesHost
    {
        public bool HasHead => true;

        public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> RestoreAsync() => Task.FromResult<string?>(null);

        public Task<string?> CommitAsync(string message, bool amend) => Task.FromResult<string?>(null);

        public Task<(string Subject, string Body)?> ReadHeadMessageAsync() => Task.FromResult<(string Subject, string Body)?>(null);

        public void OpenDiff(ChangedFile file, bool staged)
        {
        }
    }
}
