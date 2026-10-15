using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.UndoAppTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The undo history's recording through <see cref="RepositoryOperations"/> against real git
/// (docs/phase-5-design.md 7.2, 7.7, 7.11; the report's part U): the entry each kind of action
/// leaves, with its text (7.6); no entry for an action that changed nothing or was refused; the
/// undo and the redo of the entries as the toolbar runs them (7.5), with what the stage panel
/// shows; the pending entry of a stopped action, completed by the stage panel's Commit and removed
/// by Abort (D165); and one history per worktree (7.1).
/// </summary>
[Collection(UndoCollection)]
public class UndoRecordingTests
{
    // The "ops" scenario's commits, as ScenarioTests pins them.
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
    private const string AddCore = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";
    private const string FastForwardMe = "d93814d4ea6e21d1a70dd93066dbb23512c03b56";

    [Fact]
    public async Task A_commit_is_recorded_its_undo_leaves_it_staged_with_its_message_and_its_redo_takes_the_message_out()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;

        // Staging prepares a commit: not recorded (7.2).
        await app.Repository.StageAsync(app.Repository.Status.Unstaged);
        Assert.Empty(history.Entries);

        var changes = app.Repository.Changes;
        changes.Summary = "Describe more";
        changes.Description = "The core says more.";
        await changes.CommitCommand.ExecuteAsync(null);
        var commit = await repo.HeadAsync();
        var entry = Assert.Single(history.Entries);
        Assert.Equal((UndoKind.Commit, "commit \"Describe more\"", UndoEntryState.Done), (entry.Kind, entry.Text, entry.State));
        Assert.Equal((MainWork, commit), (entry.Before.Head.Sha, entry.After.Head.Sha));
        Assert.Equal((string.Empty, string.Empty), (changes.Summary, changes.Description));
        AssertStates(app, new UndoAvailability(true, "Undo commit \"Describe more\"", false, "Nothing to redo"));

        await app.Operations.UndoAsync();
        Assert.Equal("Undid commit \"Describe more\"", app.StatusText);
        Assert.Equal(MainWork, await repo.HeadAsync());
        Assert.Equal("M  src/core.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        Assert.Equal("VisualCommit: undo commit \"Describe more\"", await NewestReflogAsync(repo, "HEAD"));
        Assert.Equal("VisualCommit: undo commit \"Describe more\"", await NewestReflogAsync(repo, "refs/heads/main"));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git update-ref -m ", StringComparison.Ordinal)
            && call.StandardInput == $"update refs/heads/main {MainWork} {commit}\n");
        Assert.Equal(("Describe more", "The core says more."), (changes.Summary, changes.Description));
        Assert.Equal(0, history.Cursor);
        AssertStates(app, new UndoAvailability(false, "Nothing to undo", true, "Redo commit \"Describe more\""));

        await app.Operations.RedoAsync();
        Assert.Equal("Redid commit \"Describe more\"", app.StatusText);
        Assert.Equal(commit, await repo.HeadAsync());
        Assert.Equal("VisualCommit: redo commit \"Describe more\"", await NewestReflogAsync(repo, "HEAD"));
        Assert.Equal((string.Empty, string.Empty), (changes.Summary, changes.Description));
        AssertStates(app, new UndoAvailability(true, "Undo commit \"Describe more\"", false, "Nothing to redo"));

        // A message changed after the undo stays when the commit is redone.
        await app.Operations.UndoAsync();
        changes.Summary = "My own words";
        await app.Operations.RedoAsync();
        Assert.Equal(("My own words", "The core says more."), (changes.Summary, changes.Description));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_action_that_changed_nothing_or_was_refused_records_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var calls = app.Calls.Snapshot().Count;
        var reflog = await NewestReflogAsync(repo, "HEAD");

        // Up to date: git changes nothing.
        await app.Operations.MergeAsync("refs/tags/v1.0", MergeMode.Default);
        Assert.Equal("main is already up to date with v1.0", app.StatusText);

        // Refused by the app before git (2.24's "There are no changes to stash").
        app.Dialogs.Stash("Nothing here");
        await app.Operations.StashAsync();

        Assert.Empty(app.Repository.UndoHistory.Entries);
        AssertStates(app, new UndoAvailability(false, "Nothing to undo", false, "Nothing to redo"));

        // The reads write nothing (7.7 step 4): HEAD's reflog is as it was.
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal(reflog, await NewestReflogAsync(repo, "HEAD"));
    }

    [Fact]
    public async Task A_branch_created_without_its_checkout_is_deleted_by_undo_and_created_again_by_redo()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.EnterName("hotfix", option: false);
        await app.Operations.CreateBranchAsync(MainWork);
        Assert.Equal("Created hotfix", app.StatusText);
        Assert.Equal("creation of hotfix", Assert.Single(app.Repository.UndoHistory.Entries).Text);

        var calls = app.Calls.Snapshot().Count;
        await app.Operations.UndoAsync();
        Assert.Equal("Undid creation of hotfix", app.StatusText);
        Assert.Null(await repo.RefTargetAsync("refs/heads/hotfix"));
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("reflog", "exists", "refs/heads/hotfix")).ExitCode);
        var transaction = Assert.Single(app.Calls.Snapshot().Skip(calls), call => call.StandardInput is not null && call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal("git update-ref -m \"VisualCommit: undo creation of hotfix\" --stdin", transaction.CommandText);
        Assert.Equal($"delete refs/heads/hotfix {MainWork}\n", transaction.StandardInput);
        // The branch had no keys: the step reads the settings (7.5 step 2) and writes none.
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git config", StringComparison.Ordinal) && !call.CommandText.Contains("--list", StringComparison.Ordinal));

        await app.Operations.RedoAsync();
        Assert.Equal("Redid creation of hotfix", app.StatusText);
        Assert.Equal(MainWork, await repo.RefTargetAsync("refs/heads/hotfix"));
        Assert.Equal("VisualCommit: redo creation of hotfix", await NewestReflogAsync(repo, "refs/heads/hotfix"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_hard_reset_is_undone_with_its_files_and_index_from_the_snapshot_and_redone_with_a_new_one()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\nunstaged line\n").WriteFile("scratch.txt", "scratch\n").WriteFile("todo.txt", "todo\n");
        await repo.GitAsync("add", "scratch.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        var start = await PictureAsync(repo);

        app.Dialogs.Reset(ResetMode.Hard);
        await app.Operations.ResetAsync(AddCore);
        Assert.Equal("Reset main to 8beca34 (hard)", app.StatusText);
        var done = await PictureAsync(repo);
        var entry = Assert.Single(app.Repository.UndoHistory.Entries);
        Assert.Equal("reset of main to 8beca34 (hard)", entry.Text);
        Assert.NotNull(entry.Captured.ResetIndex);
        Assert.Equal(["scratch.txt", "src/core.txt"], entry.Captured.Snapshot!.Paths);
        Assert.Contains(entry.Captured.Snapshot.Ref, app.Repository.BackupsToKeep);

        var calls = app.Calls.Snapshot().Count;
        await app.Operations.UndoAsync();
        Assert.Equal("Undid reset of main to 8beca34 (hard)", app.StatusText);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(
            ["git update-index -q --refresh", $"git read-tree -m -u {AddCore} {MainWork}", $"git update-ref -m \"VisualCommit: undo reset of main to 8beca34 (hard)\" refs/heads/main {MainWork} {AddCore}"],
            app.Calls.Snapshot().Skip(calls).Select(call => call.CommandText).Where(command => command.StartsWith("git update-index", StringComparison.Ordinal) || command.StartsWith("git read-tree", StringComparison.Ordinal) || command.StartsWith("git update-ref", StringComparison.Ordinal)));

        await app.Operations.RedoAsync();
        Assert.Equal("Redid reset of main to 8beca34 (hard)", app.StatusText);
        Assert.Equal(done, await PictureAsync(repo));
        var redone = Assert.Single(app.Repository.UndoHistory.Entries);
        Assert.Equal(4, (await repo.BackupRefsAsync()).Count);
        Assert.Equal(entry.Captured.ResetSnapshot, redone.Captured.ResetSnapshot);
        Assert.NotEqual(entry.Captured.Snapshot.Ref, redone.Captured.Snapshot!.Ref);
        Assert.Equal("reset: moving to " + AddCore, await NewestReflogAsync(repo, "HEAD"));

        // Undone again from the redo's own snapshot.
        await app.Operations.UndoAsync();
        Assert.Equal(start, await PictureAsync(repo));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_merge_that_stops_is_pending_until_the_stage_panels_commit_ends_it_and_an_abort_removes_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;
        app.Dialogs.EnterName("hotfix", option: false);
        await app.Operations.CreateBranchAsync(MainWork);

        await app.Operations.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);
        Assert.Equal("Merge stopped: 1 file has conflicts", app.StatusText);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal((UndoEntryState.Pending, "merge of feature/conflict into main"), (history.Pending!.State, history.Pending.Text));
        AssertStates(app, new UndoAvailability(false, "Finish or abort the merge first", false, "Finish or abort the merge first"));

        // D165: the abort takes the pending entry and records none.
        app.Dialogs.Confirm();
        await app.Operations.AbortAsync();
        Assert.Equal("Aborted the merge", app.StatusText);
        Assert.Equal("creation of hotfix", Assert.Single(history.Entries).Text);
        AssertStates(app, new UndoAvailability(true, "Undo creation of hotfix", false, "Nothing to redo"));

        // Stopped again, resolved and committed in the stage panel: one entry from before the merge.
        await app.Operations.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await app.Repository.StageAsync(app.Repository.Status.Unstaged);
        app.Repository.Changes.Summary = "Merge branch 'feature/conflict'";
        await app.Repository.Changes.CommitCommand.ExecuteAsync(null);
        var merge = await repo.HeadAsync();
        Assert.Equal(2, history.Entries.Count);
        var completed = history.Entries[^1];
        Assert.Equal((UndoKind.Merge, UndoEntryState.Done, MainWork, merge), (completed.Kind, completed.State, completed.Before.Head.Sha, completed.After.Head.Sha));
        Assert.Null(history.Pending);

        await app.Operations.UndoAsync();
        Assert.Equal("Undid merge of feature/conflict into main", app.StatusText);
        Assert.Equal(MainWork, await repo.HeadAsync());
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_list_of_picks_that_stops_is_one_entry_once_skip_ends_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;
        var alphaOne = await app.ShaAsync("feature/alpha~1");
        var conflicting = await app.ShaAsync("feature/conflict");

        await app.Operations.CherryPickSeveralAsync([conflicting, alphaOne]);
        Assert.True(app.Repository.Status.InProgress.IsActive);
        Assert.Equal((UndoKind.CherryPick, "cherry-pick of 2 commits"), (history.Pending!.Kind, history.Pending.Text));

        // Skip ends the list: the pending entry becomes one entry from before the picks (7.2).
        await app.Operations.SkipAsync(app.Repository.Status.InProgress);
        Assert.False(app.Repository.Status.InProgress.IsActive);
        var entry = Assert.Single(history.Entries);
        Assert.Equal((UndoEntryState.Done, MainWork), (entry.State, entry.Before.Head.Sha));
        Assert.NotEqual(MainWork, entry.After.Head.Sha);
        Assert.Equal(entry.After.Head.Sha, await repo.HeadAsync());

        await app.Operations.UndoAsync();
        Assert.Equal("Undid cherry-pick of 2 commits", app.StatusText);
        Assert.Equal(MainWork, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Two_worktrees_of_one_repository_have_a_history_each_and_the_clean_up_keeps_the_backups_of_both()
    {
        using var repo = await Scenarios.OperationsAsync();
        var linked = repo.SiblingPath("ops-wt");
        await repo.GitAsync("worktree", "add", "--quiet", linked, "feature/ff");
        using var main = await OperationsHarness.OpenAsync(repo);
        using var other = await OperationsHarness.OpenAsync(repo.OpenSibling("ops-wt"), main.Services);
        Assert.NotSame(main.Repository.UndoHistory, other.Repository.UndoHistory);

        main.Dialogs.Confirm();
        await main.Operations.DeleteBranchAsync("refs/heads/feature/alpha");
        other.Dialogs.Reset(ResetMode.Soft);
        await other.Operations.ResetAsync(MainWork);

        Assert.Equal("deletion of feature/alpha", Assert.Single(main.Repository.UndoHistory.Entries).Text);
        Assert.Equal("reset of feature/ff to 88d6b91 (soft)", Assert.Single(other.Repository.UndoHistory.Entries).Text);

        // Both histories' backups are kept, whichever worktree cleans up (7.8, D171).
        var backups = (await repo.BackupRefsAsync()).Select(backup => backup.Name).ToList();
        Assert.Equal(2, backups.Count);
        Assert.Equal(backups.Order(StringComparer.Ordinal), main.Repository.BackupsToKeep.Order(StringComparer.Ordinal));
        Assert.Equal(backups.Order(StringComparer.Ordinal), other.Repository.BackupsToKeep.Order(StringComparer.Ordinal));

        // A tab opened again on the same worktree finds its history (7.1).
        using var again = await OperationsHarness.OpenAsync(repo, main.Services);
        Assert.Same(main.Repository.UndoHistory, again.Repository.UndoHistory);
        main.AssertScriptFollowed();
        other.AssertScriptFollowed();
    }
}
