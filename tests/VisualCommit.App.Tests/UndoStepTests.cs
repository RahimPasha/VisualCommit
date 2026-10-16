using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using static VisualCommit.App.Tests.UndoAppTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The toolbar's Undo and Redo as operations against real git (docs/phase-5-design.md 7.5, 7.6,
/// 12.1, 12.2, 13.2; the report's part U and its decisions UD9, UD10, UD13): the question before a
/// step that changes a remote, in each of its forms, and nothing started when it is cancelled; a
/// lease that fails; a step refused by local changes in a move's way (the app's count, git's line
/// below) and by an ignored file (the app's own check, no output); a stash's and a tag's steps;
/// a discard's restore bar going and coming back with its Undo and Redo (7.9).
/// </summary>
[Collection(UndoCollection)]
public class UndoStepTests
{
    // The "ops" and "sync" scenarios' commits, as ScenarioTests pins them.
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
    private const string FastForwardMe = "d93814d4ea6e21d1a70dd93066dbb23512c03b56";
    private const string TopicWork = "77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02";

    [Fact]
    public async Task Undoing_a_push_that_made_a_branch_asks_first_deletes_it_with_a_lease_and_its_redo_pushes_it_again()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;
        app.Dialogs.Push(setUpstream: true);
        await app.Operations.PushBranchAsync("refs/heads/topic");
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", app.StatusText);
        Assert.Equal("push of topic to origin", Assert.Single(history.Entries).Text);

        // Cancelled: nothing starts (13.2).
        app.Dialogs.Confirm(yes: false);
        var calls = app.Calls.Snapshot().Count;
        await app.Operations.UndoAsync();
        var question = app.Dialogs.Confirmations[^1];
        Assert.Equal(
            ("Delete topic on origin?", "Undoing the push deletes topic on origin. Anyone who fetched it keeps it.", string.Empty, "Undo", false),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Empty(app.Calls.Snapshot().Skip(calls));
        Assert.Equal(TopicWork, await RemoteHeadAsync(repo, "refs/heads/topic"));
        Assert.Equal(1, history.Cursor);

        app.Dialogs.Confirm();
        await app.Operations.UndoAsync();
        Assert.Equal("Undid push of topic to origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/topic"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/topic"));
        Assert.Null(await ConfigAsync(repo, "branch.topic.remote"));
        Assert.Equal(TopicWork, await repo.RefTargetAsync("refs/heads/topic"));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git push --progress --force-with-lease=refs/heads/topic:{TopicWork} origin --delete refs/heads/topic");

        app.Dialogs.Confirm();
        await app.Operations.RedoAsync();
        question = app.Dialogs.Confirmations[^1];
        Assert.Equal(("Push topic to origin again?", "origin gets topic back at 77d782e.", "Redo"), (question.Title, question.Question, question.ConfirmText));
        Assert.Equal("Redid push of topic to origin", app.StatusText);
        Assert.Equal(TopicWork, await RemoteHeadAsync(repo, "refs/heads/topic"));
        Assert.Equal(("origin", "refs/heads/topic"), (await ConfigAsync(repo, "branch.topic.remote"), await ConfigAsync(repo, "branch.topic.merge")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_undo_whose_lease_fails_changes_nothing_says_so_and_keeps_its_entry()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.PushBranchAsync("refs/heads/published");
        Assert.Equal("Pushed published to origin/published", app.StatusText);
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nunpushed\nelsewhere\n", "Published elsewhere");

        app.Dialogs.Confirm().CloseError();
        await app.Operations.UndoAsync();

        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Undo the push to origin?", "published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 8e65315 keeps it."),
            (question.Title, question.Question));
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Undo failed", "published on origin has changed since: nothing was changed."), (error.Title, error.Summary));
        Assert.Contains("(stale info)", error.Output, StringComparison.Ordinal);
        Assert.StartsWith("git push --progress --force-with-lease=refs/heads/published:8e65315", error.Command, StringComparison.Ordinal);
        Assert.Equal("Undo push of published to origin failed", app.StatusText);
        Assert.Equal(elsewhere, await RemoteHeadAsync(repo, "refs/heads/published"));

        // UD9: the entry stays; outside changes never remove one (7.4).
        AssertStates(app, new UndoAvailability(true, "Undo push of published to origin", false, "Nothing to redo"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_move_refused_for_changes_in_its_way_counts_them_shows_gits_line_and_changes_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);
        Assert.Equal("Fast-forwarded main to feature/ff", app.StatusText);

        // "Merge feature/ff into main" is git's merge as configured, which fast-forwards here (phase 3's
        // 2.8): not --ff-only, which the drop's "Fast-forward main to feature/ff" runs (U19 step 1).
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git merge --no-edit --no-log -m \"Merge branch 'feature/ff'\" refs/heads/feature/ff");
        Assert.Equal("merge of feature/ff into main", Assert.Single(app.Repository.UndoHistory.Entries).Text);
        repo.WriteFile("docs/ff.txt", "mine\n");
        await app.Repository.RefreshAsync();

        // Checked only inside the step (7.4): enabled until then.
        AssertStates(app, new UndoAvailability(true, "Undo merge of feature/ff into main", false, "Nothing to redo"));
        app.Dialogs.CloseError();
        await app.Operations.UndoAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Undo failed", "Can't undo merge of feature/ff into main: 1 conflict prevents it. Commit, stash, or discard your changes first."),
            (error.Title, error.Summary));
        Assert.Equal($"git read-tree -m -u {FastForwardMe} {MainWork}", error.Command);
        AssertOutputOfFailedCall(app, error, error.Command!);
        Assert.Equal("Undo merge of feature/ff into main failed", app.StatusText);
        Assert.Equal((FastForwardMe, "mine\n"), (await repo.HeadAsync(), ReadText(repo, "docs/ff.txt")));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));
        AssertStates(app, new UndoAvailability(true, "Undo merge of feature/ff into main", false, "Nothing to redo"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_redo_refused_for_an_ignored_file_in_its_way_says_so_without_gits_output()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);
        await app.Operations.UndoAsync();
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "docs")));
        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "docs/ff.txt\n");
        repo.WriteFile("docs/ff.txt", "my ff notes\n");

        app.Dialogs.CloseError();
        var calls = app.Calls.Snapshot().Count;
        await app.Operations.RedoAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Redo failed", "Can't redo merge of feature/ff into main: 1 ignored file is in the way: docs/ff.txt. Move it away first."),
            (error.Title, error.Summary));
        Assert.Equal((false, false), (error.HasOutput, error.HasCommand));
        Assert.Equal("Redo merge of feature/ff into main failed", app.StatusText);
        Assert.Equal((MainWork, "my ff notes\n"), (await repo.HeadAsync(), ReadText(repo, "docs/ff.txt")));

        // The guard is the app's own check (D169): neither read-tree nor update-ref ran.
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git read-tree", StringComparison.Ordinal) || call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));

        // Moved away: the redo goes through.
        File.Delete(Path.Combine(repo.Path, "docs", "ff.txt"));
        await app.Operations.RedoAsync();
        Assert.Equal(FastForwardMe, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_stash_is_popped_back_by_undo_and_made_again_by_redo_with_its_message_and_a_pop_is_stored_back()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n").WriteFile("todo.txt", "Later\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;
        var start = await PictureAsync(repo);

        app.Dialogs.Stash("Save my work");
        await app.Operations.StashAsync();
        var stash = (await repo.StashListAsync())[0];
        Assert.Equal("stash \"On main: Save my work\"", Assert.Single(history.Entries).Text);
        Assert.Equal(stash.Sha, history.Entries[0].Captured.Stash);
        var stashed = await PictureAsync(repo);

        await app.Operations.UndoAsync();
        Assert.Equal("Undid stash \"On main: Save my work\"", app.StatusText);
        Assert.Equal(start, await PictureAsync(repo));

        await app.Operations.RedoAsync();
        Assert.Equal(stashed, await PictureAsync(repo));
        Assert.Equal(stash.Sha, Assert.Single(history.Entries).Captured.Stash);

        // A pop saves a backup first (D171), which the entry keeps from the clean-up (7.8).
        await app.Operations.PopStashAsync(stash.Sha);
        Assert.Equal("Popped stash@{0}", app.StatusText);
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Equal(stash.Sha, backup.Sha);
        Assert.StartsWith("refs/visualcommit/backup/stash-", backup.Name, StringComparison.Ordinal);
        Assert.Contains(backup.Name, app.Repository.BackupsToKeep);
        Assert.Equal("pop of stash \"On main: Save my work\"", history.Entries[^1].Text);

        await app.Operations.UndoAsync();
        Assert.Equal("Undid pop of stash \"On main: Save my work\"", app.StatusText);
        Assert.Equal(stashed, await PictureAsync(repo));
        Assert.Equal((stash.Sha, "On main: Save my work"), ((await repo.StashListAsync())[0].Sha, (await repo.StashListAsync())[0].Message));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_annotated_tag_undone_is_kept_in_a_backup_and_comes_back_as_the_same_object()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.EnterName("v1.2", "#42 hotfix");
        await app.Operations.CreateTagAsync(MainWork);
        var tag = await repo.RefTargetAsync("refs/tags/v1.2");
        Assert.Equal("creation of tag v1.2", Assert.Single(app.Repository.UndoHistory.Entries).Text);

        await app.Operations.UndoAsync();
        Assert.Null(await repo.RefTargetAsync("refs/tags/v1.2"));
        var kept = Assert.Single(await repo.BackupRefsAsync());
        Assert.StartsWith("refs/visualcommit/backup/tag-undone-", kept.Name, StringComparison.Ordinal);
        Assert.EndsWith("/v1.2", kept.Name, StringComparison.Ordinal);
        Assert.Equal(tag, kept.Sha);
        Assert.Contains(kept.Name, app.Repository.BackupsToKeep);

        await app.Operations.RedoAsync();
        Assert.Equal(tag, await repo.RefTargetAsync("refs/tags/v1.2"));
        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", "v1.2")).StandardOutput.Trim());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_ignore_and_a_removed_remote_are_undone_and_redone_with_their_bytes_keys_and_branches()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        repo.WriteFile("logs/run.log", "run\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var history = app.Repository.UndoHistory;

        // The app's .gitignore: undone by deleting the file it made, redone by its bytes again (7.3).
        await app.Operations.IgnoreFileAsync("logs/run.log");
        Assert.Equal("ignore of /logs/run.log", history.Entries[^1].Text);
        Assert.Equal("/logs/run.log\n", history.Entries[^1].Captured.GitignoreAppended);
        await app.Operations.UndoAsync();
        Assert.False(File.Exists(Path.Combine(repo.Path, ".gitignore")));
        await app.Operations.RedoAsync();
        Assert.Equal("/logs/run.log\n", ReadText(repo, ".gitignore"));

        // A removed remote comes back with its keys, the upstreams that named it and its branches.
        var config = (await repo.GitAsync("config", "--local", "--list")).StandardOutput;
        var refs = (await repo.GitAsync("for-each-ref", "refs/remotes")).StandardOutput;
        app.Dialogs.Confirm();
        await app.Operations.RemoveRemoteAsync("origin");
        Assert.Equal("removal of remote origin", history.Entries[^1].Text);
        Assert.Empty((await repo.GitAsync("for-each-ref", "refs/remotes")).StandardOutput);
        await app.Operations.UndoAsync();
        Assert.Equal("Undid removal of remote origin", app.StatusText);
        Assert.Equal(
            config.Split('\n').Order(StringComparer.Ordinal),
            (await repo.GitAsync("config", "--local", "--list")).StandardOutput.Split('\n').Order(StringComparer.Ordinal));
        Assert.Equal(refs, (await repo.GitAsync("for-each-ref", "refs/remotes")).StandardOutput);
        await app.Operations.RedoAsync();
        Assert.Empty((await repo.GitAsync("for-each-ref", "refs/remotes")).StandardOutput);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_discards_undo_closes_its_restore_bar_its_redo_shows_it_again_and_the_bars_restore_is_its_undo()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmine\n").WriteFile("todo.txt", "todo\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var changes = app.Repository.Changes;
        var history = app.Repository.UndoHistory;
        app.Dialogs.Confirm();
        await app.Repository.DiscardAsync([app.Repository.Status.Unstaged.Single(file => file.Path == "src/core.txt")]);
        Assert.Equal("Discarded changes to core.txt.", changes.RestoreText);
        var entry = Assert.Single(history.Entries);
        Assert.Equal("discard of core.txt", entry.Text);

        await app.Operations.UndoAsync();
        Assert.Equal("core\nmain work\nmine\n", ReadText(repo, "src/core.txt"));
        Assert.False(changes.HasRestore);
        Assert.Contains(entry.Captured.Snapshot!.Ref, app.Repository.BackupsToKeep);

        await app.Operations.RedoAsync();
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.Equal("Discarded changes to core.txt.", changes.RestoreText);
        Assert.Single(await repo.BackupRefsAsync());

        // The bar's Restore is the entry's Undo while its files are as the discard left them (7.9, UD18).
        await app.Repository.RestoreAsync();
        Assert.Equal("Undid discard of core.txt", app.StatusText);
        Assert.Equal("core\nmain work\nmine\n", ReadText(repo, "src/core.txt"));
        Assert.Equal((0, false), (history.Cursor, changes.HasRestore));

        // Otherwise it runs as in phase 2 and is recorded as an entry of its own.
        await app.Operations.RedoAsync();
        app.Dialogs.EnterName("keep", option: false);
        await app.Operations.CreateBranchAsync(MainWork);
        await app.Repository.RestoreAsync();
        Assert.Equal("restore of the discarded files", history.Entries[^1].Text);
        Assert.Equal("core\nmain work\nmine\n", ReadText(repo, "src/core.txt"));
        Assert.False(changes.HasRestore);

        // Its undo writes back what the restore found, and leaves the bar closed (UD3).
        await app.Operations.UndoAsync();
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.False(changes.HasRestore);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_bars_restore_clicked_while_a_recorded_operation_runs_restores_after_it_and_undoes_nothing_else()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmine\n");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => RepositoryOperationsProtectTests.Intercepting.Wrap(inner, new()
        {
            [nameof(IGitRepository.CreateBranchAsync)] = args => HeldAsync(gate.Task, () => inner.CreateBranchAsync((string)args[0]!, (string)args[1]!, (bool)args[2]!, (NetworkOptions?)args[3], (CancellationToken)args[4]!)),
        }));
        var history = app.Repository.UndoHistory;
        app.Dialogs.Confirm();
        await app.Repository.DiscardAsync([app.Repository.Status.Unstaged.Single(file => file.Path == "src/core.txt")]);

        // A recorded operation runs; the bar's Restore is clicked meanwhile (15.9). It is not the
        // discard's Undo now, which would find the history moved and do nothing: it runs as in
        // phase 2, after the branch's creation, and is recorded after it.
        app.Dialogs.EnterName("keep", option: false);
        var branch = app.Operations.CreateBranchAsync(MainWork);
        await WaitUntilAsync(() => app.Repository.Queue.Current is not null, "the branch's creation to start");
        var restore = app.Repository.RestoreAsync();
        await WaitUntilAsync(() => app.Repository.Queue.Waiting > 0 || restore.IsCompleted, "the restore to queue");
        gate.SetResult();
        await branch;
        Assert.Null(await restore);
        await app.WaitForIdleAsync();

        Assert.Equal("core\nmain work\nmine\n", ReadText(repo, "src/core.txt"));
        Assert.Equal(MainWork, await repo.RefTargetAsync("refs/heads/keep"));
        Assert.False(app.Repository.Changes.HasRestore);
        Assert.Equal(
            ("discard of core.txt | creation of keep | restore of the discarded files", 3),
            (string.Join(" | ", history.Entries.Select(entry => entry.Text)), history.Cursor));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_bars_restore_saves_a_file_written_after_its_question_with_the_newer_versions()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmine\n").WriteFile("todo.txt", "todo\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm();
        await app.Repository.DiscardAsync([.. app.Repository.Status.Unstaged]);
        var before = await repo.BackupRefsAsync();

        // todo.txt changed since the discard: D75's question, while which core.txt is edited too.
        repo.WriteFile("todo.txt", "todo again\n");
        app.Dialogs.Answer<ConfirmationViewModel>(dialog =>
        {
            repo.WriteFile("src/core.txt", "typed while the question was open\n");
            dialog.ConfirmCommand.Execute(null);
        });
        Assert.Null(await app.Repository.RestoreAsync());

        Assert.Equal("todo.txt has changed since the discard. Put back the version from before the discard?", Assert.Single(app.Dialogs.Confirmations, question => question.Title == "Restore discarded changes?").Question);
        var saved = Assert.Single(await repo.BackupRefsAsync(), entry => !before.Contains(entry));
        Assert.Equal("todo again\n", (await repo.GitAsync("show", saved.Sha + ":todo.txt")).StandardOutput);
        Assert.Equal("typed while the question was open\n", (await repo.GitAsync("show", saved.Sha + ":src/core.txt")).StandardOutput);
        Assert.Equal(("core\nmain work\nmine\n", "todo\n"), (ReadText(repo, "src/core.txt"), ReadText(repo, "todo.txt")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Two_tabs_on_one_worktree_an_undo_that_ends_after_the_other_tab_recorded_keeps_that_entry()
    {
        using var repo = await Scenarios.OperationsAsync();
        var histories = new UndoHistories();
        var stepEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stepGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branchGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var a = await OperationsHarness.OpenAsync(repo, null, inner => RepositoryOperationsProtectTests.Intercepting.Wrap(inner, new()
        {
            [nameof(IGitRepository.RunUndoStepAsync)] = args => HeldStepAsync(inner, args, stepEntered, stepGate.Task),
        }), undoHistories: histories);
        using var b = await OperationsHarness.OpenAsync(repo, null, inner => RepositoryOperationsProtectTests.Intercepting.Wrap(inner, new()
        {
            [nameof(IGitRepository.CreateBranchAsync)] = args => HeldAsync(branchGate.Task, () => inner.CreateBranchAsync((string)args[0]!, (string)args[1]!, (bool)args[2]!, (NetworkOptions?)args[3], (CancellationToken)args[4]!)),
        }), undoHistories: histories);
        var history = a.Repository.UndoHistory;
        Assert.Same(history, b.Repository.UndoHistory);

        a.Dialogs.EnterName("x", option: false);
        await a.Operations.CreateBranchAsync(MainWork);
        b.Dialogs.EnterName("y", option: false);
        var creatingY = b.Operations.CreateBranchAsync(MainWork);
        await WaitUntilAsync(() => b.Repository.Queue.Current is not null, "tab B's operation to start");

        // Tab A undoes x; its step holds inside git while tab B's creation of y ends (15.9).
        await a.Repository.RefreshAsync();
        var undo = a.Operations.UndoAsync();
        await stepEntered.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        branchGate.SetResult();
        await WaitUntilAsync(() => File.Exists(Path.Combine(repo.Path, ".git", "refs", "heads", "y")), "y to be created");
        await Task.WhenAny(creatingY, Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        stepGate.SetResult();
        await undo;
        await creatingY;
        await a.WaitForIdleAsync();
        await b.WaitForIdleAsync();

        // y was recorded after x's undo moved the cursor, not in x's place.
        Assert.Equal(("creation of y", 1), (string.Join(" | ", history.Entries.Select(entry => entry.Text)), history.Cursor));
        Assert.Null(await repo.RefTargetAsync("refs/heads/x"));
        Assert.Equal(MainWork, await repo.RefTargetAsync("refs/heads/y"));
        Assert.Equal("Undid creation of x", a.StatusText);
    }

    private static async Task<UndoStepResult> HeldStepAsync(IGitRepository inner, object?[] args, TaskCompletionSource entered, Task gate)
    {
        entered.TrySetResult();
        await gate;
        return await inner.RunUndoStepAsync((UndoStep)args[0]!, (NetworkOptions)args[1]!, (CancellationToken)args[2]!);
    }

    private static async Task HeldAsync(Task gate, Func<Task> call)
    {
        await gate;
        await call();
    }
}
