using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using static VisualCommit.App.Tests.UndoAppTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The toolbar's Undo and Redo as the operations give them (docs/phase-5-design.md 1.1, 7.4; the
/// report's part U, T0, T+ and T−): enabled with what they would do, or disabled with the first
/// reason that holds, in 7.4's order: a user operation that runs or waits (a background one alone gives none), an
/// operation in progress or conflicts (except those an apply's undo puts back), the interactive
/// list, nothing to do, then the entry's own check against what the app last read, which an
/// outside change makes fail and its return passes again; none before the refs are read and after
/// the tab closed. A second click before the toolbar followed the first joins its step. The
/// toolbar reads the same on <see cref="ToolbarViewModel.UpdateUndoStates"/>; WP5 reads it on
/// every change of the queue.
/// </summary>
[Collection(UndoCollection)]
public class UndoToolbarTests
{
    // The "ops" scenario's commits, as ScenarioTests pins them.
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
    private const string FastForwardMe = "d93814d4ea6e21d1a70dd93066dbb23512c03b56";

    private static readonly UndoAvailability Nothing = new(false, "Nothing to undo", false, "Nothing to redo");

    [Fact]
    public async Task The_states_follow_the_history_the_queue_and_an_operation_in_progress()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var toolbar = app.Repository.Toolbar;
        AssertStates(app, Nothing);

        app.Dialogs.EnterName("hotfix", option: false);
        await app.Operations.CreateBranchAsync(MainWork);
        var undoable = new UndoAvailability(true, "Undo creation of hotfix", false, "Nothing to redo");
        AssertStates(app, undoable);
        toolbar.UpdateUndoStates();
        Assert.Equal((true, "Undo creation of hotfix (Ctrl+Z)", false, "Nothing to redo"), (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.CanRedo, toolbar.RedoToolTip));

        // 1. A user operation runs: wait for it. A background one gives no reason (a click queues behind it).
        var (hold, ahead) = HoldQueue(app);
        AssertStates(app, new UndoAvailability(false, "Wait for Held to finish", false, "Wait for Held to finish"));
        hold.SetResult();
        await ahead;
        AssertStates(app, undoable);
        var background = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(app.Repository.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await background.Task;
            return OperationReport.Done(string.Empty);
        })));
        AssertStates(app, undoable);

        // A user operation that waits behind the background one is reason 1 too (7.4: "runs or
        // waits"): a click would queue behind it and find the history moved.
        var waits = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = app.Repository.Queue.RunAsync(new GitOperation("Commit", "Committing…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await waits.Task;
            return OperationReport.Done(string.Empty);
        }));
        AssertStates(app, new UndoAvailability(false, "Wait for Commit to finish", false, "Wait for Commit to finish"));
        background.SetResult();
        waits.SetResult();
        await waiting;
        await app.WaitForIdleAsync();
        AssertStates(app, undoable);

        // 2. An operation in progress gives its reason to both, before "Nothing to redo" (UD14).
        await app.Operations.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);
        AssertStates(app, new UndoAvailability(false, "Finish or abort the merge first", false, "Finish or abort the merge first"));
        app.Dialogs.Confirm();
        await app.Operations.AbortAsync();
        AssertStates(app, undoable);

        // 4. Undone: nothing left to undo, its redo offered.
        await app.Operations.UndoAsync();
        AssertStates(app, new UndoAvailability(false, "Nothing to undo", true, "Redo creation of hotfix"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_terminal_commit_on_the_branch_disables_undo_with_the_entrys_check_until_the_branch_is_back()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);
        var undoable = new UndoAvailability(true, "Undo merge of feature/ff into main", false, "Nothing to redo");
        AssertStates(app, undoable);

        // 5. The check that needs no git fails against the refs read after the outside commit (U23).
        var terminal = await repo.CommitFileAsync("src/terminal.txt", "terminal\n", "Terminal work");
        await app.Repository.RefreshAsync();
        AssertStates(app, new UndoAvailability(false, $"main has moved since (it is at {terminal[..7]})", false, "Nothing to redo"));

        // The step itself refuses too, and says so: nothing is written.
        app.Dialogs.CloseError();
        await app.Operations.UndoAsync();
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Undo failed", $"Can't undo merge of feature/ff into main: main has moved since (it is at {terminal[..7]})"), (error.Title, error.Summary));
        Assert.Equal(terminal, await repo.HeadAsync());

        // The branch back where the entry left it: enabled again, the entry was never removed (7.4).
        await repo.GitAsync("reset", "--quiet", "--hard", FastForwardMe);
        await app.Repository.RefreshAsync();
        AssertStates(app, undoable);
        await app.Operations.UndoAsync();
        Assert.Equal(MainWork, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Conflicts_an_apply_left_keep_its_undo_and_disable_everything_else()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("README.md", "# Operations\n\nMine.\n", "Mine");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = app.Operations.Refs!.Stashes[0].Sha;
        await app.Operations.ApplyStashAsync(stash);
        Assert.Equal("apply of stash \"On main: Experiment\"", app.Repository.UndoHistory.Entries[^1].Text);
        Assert.Contains(app.Repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted);

        // 7.4: the undo of an apply whose recorded files hold every conflicted one puts them back.
        AssertStates(app, new UndoAvailability(true, "Undo apply of stash \"On main: Experiment\"", false, "Resolve the conflicts first"));
        await app.Operations.UndoAsync();
        Assert.Equal("Undid apply of stash \"On main: Experiment\"", app.StatusText);
        Assert.Equal("# Operations\n\nMine.\n", ReadText(repo, "README.md"));
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        AssertStates(app, new UndoAvailability(false, "Nothing to undo", true, "Redo apply of stash \"On main: Experiment\""));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_interactive_list_open_disables_both()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.EnterName("keep", option: false);
        await app.Operations.CreateBranchAsync(null);
        AssertStates(app, new UndoAvailability(true, "Undo creation of keep", false, "Nothing to redo"));

        await app.Operations.OpenInteractiveRebaseAsync(InteractiveList.FromHere());
        Assert.True(app.Repository.IsInteractiveRebaseOpen);
        AssertStates(app, new UndoAvailability(false, "Close the interactive rebase first", false, "Close the interactive rebase first"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_double_click_on_Undo_undoes_its_entry_once()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;
        app.Dialogs.EnterName("hotfix", option: false).EnterName("keep", option: false);
        await app.Operations.CreateBranchAsync(MainWork);
        await app.Operations.CreateBranchAsync(MainWork);

        // The second click comes before the toolbar followed the first: it joins the first's step,
        // which would otherwise find the history moved and end "… cancelled".
        var first = app.Operations.UndoAsync();
        var second = app.Operations.UndoAsync();
        await Task.WhenAll(first, second);
        Assert.Equal("Undid creation of keep", app.StatusText);
        Assert.Equal(1, Ran(app, "Undo creation of keep"));
        Assert.Empty(app.Dialogs.Errors);
        Assert.Equal((1, MainWork), (history.Cursor, await repo.RefTargetAsync("refs/heads/hotfix")));
        Assert.Null(await repo.RefTargetAsync("refs/heads/keep"));

        // Once it has ended, a click undoes the next entry.
        await app.Operations.UndoAsync();
        Assert.Equal("Undid creation of hotfix", app.StatusText);
        Assert.Equal(0, history.Cursor);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Before_the_refs_are_read_both_are_disabled_without_a_reason()
    {
        using var repo = await Scenarios.OperationsAsync();
        var git = await GitRepository.OpenAsync(
            new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!), repo.Path, TestContext.Current.CancellationToken);
        using var repository = new RepositoryViewModel(git, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        Assert.Null(repository.Refs);
        Assert.Equal(UndoAvailability.Disabled, repository.Operations.ReadUndoAvailability());
    }

    [Fact]
    public async Task After_the_tab_closed_both_are_disabled_without_a_reason()
    {
        using var repo = await Scenarios.OperationsAsync();
        var app = await OperationsHarness.OpenAsync(repo);
        AssertStates(app, Nothing);
        app.Dispose();
        Assert.Equal(UndoAvailability.Disabled, app.Operations.ReadUndoAvailability());
    }
}
