using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Git;
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
    [Fact]
    public async Task A_deleted_branch_is_restored_at_its_backup_with_a_reflog_of_its_own_and_undone_as_a_creation()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenAsync(repo);
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
        using var app = await OpenAsync(repo);
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
        using var app = await OpenAsync(repo);
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
        using var app = await OpenAsync(repo);

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
        using var app = await OpenAsync(repo);
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
    public async Task A_dropped_stash_and_deleted_tags_come_back_and_a_force_pushs_backup_is_pushed_back_with_its_lease()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenAsync(repo);

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

    /// <summary>The "reflog" scenario opened with the harnesses' fixed backup clock, after the clean-up at opening (which removes its two old backups).</summary>
    private static async Task<OperationsHarness> OpenAsync(TempRepo repo)
    {
        var app = await OperationsHarness.OpenWrappedAsync(repo, git =>
        {
            ((GitRepository)git).BackupClock = BackupClock.Fixed(AppClocks.HarnessTime);
            return git;
        });
        await WaitUntilAsync(() => app.Log.Messages.Any(message => message.Contains($": {RepositoryViewModel.CleanUpBackupsTitle}: ", StringComparison.Ordinal)), "the clean-up at opening");
        await app.WaitForIdleAsync();
        return app;
    }

    /// <summary>The backup of <paramref name="kind"/> the list shows (the newest), with a snapshot of that subject where given.</summary>
    private static async Task<BackupInfo> BackupAsync(OperationsHarness app, string kind, string? snapshotSubject = null) =>
        (await app.Repository.Repository.ReadBackupsAsync()).First(backup => backup.Kind == kind
            && (snapshotSubject is null || backup.Refs.Any(entry => entry.IsChanges && entry.Subject == snapshotSubject)));
}
