using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using static VisualCommit.App.Tests.UndoAppTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The backups list's restores (docs/phase-5-design.md 7.10, 12.1, 12.2, 13.2; the report's B9 and
/// its decisions BD15 to BD17, BD20, BD21) against the "reflog" scenario's backups, through
/// <see cref="ViewModels.Operations.RepositoryOperations.RestoreBackupAsync"/>, with the harnesses'
/// fixed backup clock (design 2.3): each one operation with 12.1's title and 12.2's result, its ref
/// writes named <c>VisualCommit: &lt;entry text&gt;</c>, D75's question where a file would be
/// overwritten, 7.5's question before a push back, and each recorded in the undo history like any
/// action, undone and redone.
/// </summary>
[Collection(UndoCollection)]
public class UndoRestoreTests
{
    // The "ops" scenario's commits, as ScenarioTests pins them.
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
    private const string AddCore = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";

    [Fact]
    public async Task A_deleted_branch_is_restored_at_its_backup_with_a_reflog_of_its_own_and_undone_as_a_creation()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var backup = await BackupAsync(app, "branch");
        var tip = backup.Main!.Commit;

        var calls = app.Calls.Snapshot().Count;
        await app.Operations.RestoreBackupAsync(backup);
        Assert.Equal("Restored branch doomed", app.StatusText);
        Assert.Contains(app.Log.Messages, message => message.Contains(": Restore branch doomed: Succeeded after ", StringComparison.Ordinal));
        var write = Assert.Single(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal) && call.StandardInput is not null);
        Assert.Equal("git update-ref -m \"VisualCommit: restore of branch doomed\" --create-reflog --stdin", write.CommandText);
        Assert.Equal($"create refs/heads/doomed {tip}\n", write.StandardInput);
        Assert.Equal(tip, await repo.RefTargetAsync("refs/heads/doomed"));
        Assert.Equal("VisualCommit: restore of branch doomed", await NewestReflogAsync(repo, "refs/heads/doomed"));
        Assert.Null(await ConfigAsync(repo, "branch.doomed.remote"));
        Assert.Contains(backup.Main.FullName, (await repo.BackupRefsAsync()).Select(entry => entry.Name));
        AssertStates(app, new UndoAvailability(true, "Undo restore of branch doomed", false, "Nothing to redo"));

        await app.Operations.UndoAsync();
        Assert.Null(await repo.RefTargetAsync("refs/heads/doomed"));
        await app.Operations.RedoAsync();
        Assert.Equal(tip, await repo.RefTargetAsync("refs/heads/doomed"));

        // Restored again while it exists: refused, nothing written.
        app.Dialogs.CloseError();
        await app.Operations.RestoreBackupAsync(backup);
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Restoring the branch failed", "Can't restore branch doomed: a branch named doomed exists again"), (error.Title, error.Summary));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_deleted_remote_branch_is_pushed_back_after_the_question_with_a_lease_that_it_must_not_exist()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var backup = await BackupAsync(app, "remote-branch");
        var commit = backup.Main!.Commit;

        app.Dialogs.Confirm(yes: false).Confirm();
        await app.Operations.RestoreBackupAsync(backup);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/feature/gone"));
        await app.Operations.RestoreBackupAsync(backup);

        var question = app.Dialogs.Confirmations[0];
        Assert.Equal(
            ("Push feature/gone back to origin?", $"origin gets feature/gone back at {commit[..7]}.", string.Empty, "Push", false),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Equal("Pushed feature/gone back to origin", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git push --progress --force-with-lease=refs/heads/feature/gone: origin {commit}:refs/heads/feature/gone");
        Assert.Equal(commit, await RemoteHeadAsync(repo, "refs/heads/feature/gone"));
        Assert.Equal(commit, await repo.RefTargetAsync("refs/remotes/origin/feature/gone"));
        AssertStates(app, new UndoAvailability(true, "Undo push of feature/gone back to origin", false, "Nothing to redo"));

        // Undone as a push that made the branch: asked, then deleted with a lease.
        app.Dialogs.Confirm();
        await app.Operations.UndoAsync();
        Assert.Equal("Delete feature/gone on origin?", app.Dialogs.Confirmations[^1].Title);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/feature/gone"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Discarded_files_are_restored_after_D75s_question_with_the_newer_version_saved_first()
    {
        using var repo = await Scenarios.ReflogAsync();
        repo.WriteFile("src/app.txt", "app\nmy own edit\n");
        using var app = await OpenWithBackupClockAsync(repo);
        var backup = await BackupAsync(app, "discard");
        var before = await repo.BackupRefsAsync();

        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(backup);

        // BD15: only src/app.txt differs both from HEAD and from the backup.
        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Restore discarded changes?", "app.txt has changed since the discard. Put back the version from before the discard?", "A snapshot of the newer changes is saved first.", "Restore", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Equal("Restored 2 files", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git restore --source={backup.Main!.Target} --worktree -- notes/todo.txt src/app.txt");
        var saved = Assert.Single(await repo.BackupRefsAsync(), entry => !before.Contains(entry));
        Assert.Equal("app\nmy own edit\n", (await repo.GitAsync("show", saved.Sha + ":src/app.txt")).StandardOutput);
        Assert.Equal(("app\nmain work\nstaged\n", "todo\n"), (ReadText(repo, "src/app.txt"), ReadText(repo, "notes/todo.txt")));
        Assert.Equal("restore of the discarded files", app.Repository.UndoHistory.Entries[^1].Text);

        // Undone: the files as the restore found them.
        await app.Operations.UndoAsync();
        Assert.Equal("app\nmy own edit\n", ReadText(repo, "src/app.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "notes", "todo.txt")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_aborts_changes_are_restored_without_a_question_and_a_skips_with_it_each_counting_its_paths()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);

        await app.Operations.RestoreBackupAsync(await BackupAsync(app, "abort"));
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Equal("Restored 2 files", app.StatusText);
        Assert.Equal("restore of the changes", app.Repository.UndoHistory.Entries[^1].Text);

        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(await BackupAsync(app, "skip"));
        Assert.Equal("app.txt has changed since the discard. Put back the version from before the discard?", Assert.Single(app.Dialogs.Confirmations).Question);
        Assert.Equal("Restored 1 file", app.StatusText);
        Assert.Equal(2, app.Repository.UndoHistory.Entries.Count);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_mixed_resets_staged_changes_are_staged_again_for_its_own_paths_and_undone_for_them()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var backup = await BackupAsync(app, "reset", snapshotSubject: "VisualCommit: index before a mixed reset");
        var snapshot = backup.Refs.Single(entry => entry.IsChanges).Target;

        await app.Operations.RestoreBackupAsync(backup);
        Assert.Equal("Restored the staged changes", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git restore --staged --source={snapshot} -- src/app.txt");
        Assert.Equal("MM src/app.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        var entry = app.Repository.UndoHistory.Entries[^1];
        Assert.Equal("restore of the staged changes", entry.Text);
        Assert.Equal(["src/app.txt"], entry.Captured.IndexPaths);

        // Undone for those paths alone; redone the same (BD16).
        await app.Operations.UndoAsync();
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        await app.Operations.RedoAsync();
        Assert.Equal("MM src/app.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());

        // Something staged now: refused with the item's reason (7.10).
        app.Dialogs.CloseError();
        await app.Operations.RestoreBackupAsync(backup);
        Assert.Equal("Unstage your changes first", Assert.Single(app.Dialogs.Errors).Summary);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_hard_resets_changes_come_back_with_its_index_for_its_own_paths_when_nothing_is_staged()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\nunstaged line\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Reset(ResetMode.Hard);
        await app.Operations.ResetAsync("8beca345049bf7e8bab3a7b4f1427eff941b87e3");
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        var backup = await BackupAsync(app, "reset");
        var index = (await repo.GitAsync("rev-parse", backup.Refs.Single(entry => entry.IsChanges).Target + "^2")).StandardOutput.Trim();

        await app.Operations.RestoreBackupAsync(backup);

        // BD16: the index for the paths the snapshot's index changed, then the files; no question.
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Equal("Restored 1 file", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git restore --staged --source={index} -- src/core.txt");
        Assert.Equal("MM src/core.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        Assert.Equal("core\nmain work\nstaged line\n", (await repo.GitAsync("show", ":src/core.txt")).StandardOutput);
        Assert.Equal("core\nmain work\nstaged line\nunstaged line\n", ReadText(repo, "src/core.txt"));
        var entry = app.Repository.UndoHistory.Entries[^1];
        Assert.Equal(("restore of the changes", index), (entry.Text, entry.Captured.IndexSource));

        // Undone: the files, then the index for the same paths.
        await app.Operations.UndoAsync();
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_staged_deletion_of_a_file_the_resets_target_lacks_too_is_left_out_of_the_staged_changes_put_back()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("extra.txt", "extra\n", "Add extra");
        await repo.GitAsync("rm", "--quiet", "extra.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Reset(ResetMode.Mixed);
        await app.Operations.ResetAsync(MainWork);
        Assert.Equal(" M src/core.txt", await PorcelainAsync(repo));
        var backup = await BackupAsync(app, "reset", snapshotSubject: "VisualCommit: index before a mixed reset");
        var snapshot = backup.Refs.Single(entry => entry.IsChanges).Target;

        // The snapshot's index changed extra.txt too, which neither it nor HEAD has: git would
        // refuse a path that matches nothing, and the whole restore with it (BD16's correction).
        await app.Operations.RestoreBackupAsync(backup);
        Assert.Equal("Restored the staged changes", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git restore --staged --source={snapshot} -- src/core.txt");
        Assert.Equal("M  src/core.txt", await PorcelainAsync(repo));
        var entry = app.Repository.UndoHistory.Entries[^1];
        Assert.Equal("restore of the staged changes", entry.Text);
        Assert.Equal(["src/core.txt"], entry.Captured.IndexPaths);

        // Its undo and redo name the same path, which each finds.
        await app.Operations.UndoAsync();
        Assert.Equal("Undid restore of the staged changes", app.StatusText);
        Assert.Equal(" M src/core.txt", await PorcelainAsync(repo));
        await app.Operations.RedoAsync();
        Assert.Equal("Redid restore of the staged changes", app.StatusText);
        Assert.Equal("M  src/core.txt", await PorcelainAsync(repo));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_hard_resets_index_put_back_leaves_out_a_staged_deletion_of_a_file_its_target_lacks_too()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("extra.txt", "extra\n", "Add extra");
        await repo.GitAsync("rm", "--quiet", "extra.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Reset(ResetMode.Hard);
        await app.Operations.ResetAsync(MainWork);
        Assert.Empty(await PorcelainAsync(repo));
        var backup = await BackupAsync(app, "reset");
        var index = (await repo.GitAsync("rev-parse", backup.Refs.Single(entry => entry.IsChanges).Target + "^2")).StandardOutput.Trim();

        await app.Operations.RestoreBackupAsync(backup);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git restore --staged --source={index} -- src/core.txt");
        Assert.Equal("M  src/core.txt", await PorcelainAsync(repo));
        Assert.Equal("core\nmain work\nstaged line\n", ReadText(repo, "src/core.txt"));
        var entry = app.Repository.UndoHistory.Entries[^1];
        Assert.Equal(("restore of the changes", index), (entry.Text, entry.Captured.IndexSource));
        Assert.Equal(["src/core.txt"], entry.Captured.IndexPaths);

        await app.Operations.UndoAsync();
        Assert.Empty(await PorcelainAsync(repo));
        await app.Operations.RedoAsync();
        Assert.Equal("M  src/core.txt", await PorcelainAsync(repo));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_restore_git_refuses_records_nothing_so_the_entries_before_it_stay_undoable()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;

        // A mixed reset of the staged change, then a hard reset with it staged again: a snapshot
        // of the index each, whose restores put an index back.
        app.Dialogs.Reset(ResetMode.Mixed);
        await app.Operations.ResetAsync(AddCore);
        var mixed = await BackupAsync(app, "reset", snapshotSubject: "VisualCommit: index before a mixed reset");
        await repo.GitAsync("add", "src/core.txt");
        await app.Repository.RefreshAsync();
        app.Dialogs.Reset(ResetMode.Hard);
        await app.Operations.ResetAsync(MainWork);
        var hard = await BackupAsync(app, "reset");
        Assert.NotEqual(mixed.Name, hard.Name);
        string[] entries = ["reset of main to 8beca34 (mixed)", "reset of main to 88d6b91 (hard)"];
        Assert.Equal(entries, history.Entries.Select(entry => entry.Text));

        // A merge in progress with nothing staged and no conflicts: the app's own checks let both
        // restores through, and the git layer refuses them before writing anything.
        await repo.GitAsync("merge", "--no-ff", "--no-commit", "--strategy=ours", "feature/conflict");
        await app.Repository.RefreshAsync();
        Assert.True(app.Repository.Status.InProgress.IsActive);
        app.Dialogs.CloseError().CloseError();
        await app.Operations.RestoreBackupAsync(hard);
        await app.Operations.RestoreBackupAsync(mixed);
        Assert.Equal(
            ["Can't restore the changes: Finish or abort the merge first", "Can't restore the staged changes: Finish or abort the merge first"],
            app.Dialogs.Errors.Select(error => error.Summary));
        Assert.Equal(entries, history.Entries.Select(entry => entry.Text));
        Assert.Equal(2, history.Cursor);

        // The merge gone, the last reset is still the one Undo offers, and undone.
        await repo.GitAsync("merge", "--abort");
        await app.Repository.RefreshAsync();
        AssertStates(app, new UndoAvailability(true, "Undo reset of main to 88d6b91 (hard)", false, "Nothing to redo"));
        await app.Operations.UndoAsync();
        Assert.Equal("Undid reset of main to 88d6b91 (hard)", app.StatusText);
        Assert.Equal(AddCore, await repo.HeadAsync());
        Assert.Equal("M  src/core.txt", await PorcelainAsync(repo));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_dropped_stash_and_deleted_tags_come_back_and_a_force_pushs_backup_is_pushed_back_with_its_lease()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);

        var stash = await BackupAsync(app, "stash");
        await app.Operations.RestoreBackupAsync(stash);
        Assert.Equal("Restored the stash as stash@{0}", app.StatusText);
        var listed = (await repo.StashListAsync())[0];
        Assert.Equal((stash.Main!.Target, "On main: Try a README"), (listed.Sha, listed.Message));
        Assert.Equal("restore of stash \"On main: Try a README\"", app.Repository.UndoHistory.Entries[^1].Text);

        var tag = await BackupAsync(app, "tag");
        await app.Operations.RestoreBackupAsync(tag);
        Assert.Equal("Restored tag v0.9", app.StatusText);
        Assert.Equal(tag.Main!.Target, await repo.RefTargetAsync("refs/tags/v0.9"));
        var undone = await BackupAsync(app, "tag-undone");
        await app.Operations.RestoreBackupAsync(undone);
        Assert.Equal("Restored tag v1.0-rc", app.StatusText);
        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", "refs/tags/v1.0-rc")).StandardOutput.Trim());

        // The force push's backup maps back through topic's upstream; its lease is the commit the push left.
        var forced = await BackupAsync(app, "force-push");
        var forcedCommit = forced.Main!.Commit;
        var left = await repo.RefTargetAsync("refs/remotes/origin/topic");
        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(forced);
        Assert.Equal("Push topic back to origin?", Assert.Single(app.Dialogs.Confirmations).Title);
        Assert.Equal("Pushed topic back to origin", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git push --progress --force-with-lease=refs/heads/topic:{left} origin {forcedCommit}:refs/heads/topic");
        Assert.Equal(forcedCommit, await RemoteHeadAsync(repo, "refs/heads/topic"));
        app.AssertScriptFollowed();
    }

    /// <summary>The working tree's status as <c>git status --porcelain</c> gives it, without the end of its last line.</summary>
    private static async Task<string> PorcelainAsync(TempRepo repo) => (await repo.GitAsync("status", "--porcelain")).StandardOutput.TrimEnd();
}
