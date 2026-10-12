using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: the rebases of <see cref="RepositoryOperations"/> (docs/phase-4-design.md sections 1 to 3,
/// D135, D142, D143) against real git and the scenario repos, through <see cref="OperationsHarness"/>:
/// a rebase onto a branch or a commit asks nothing and saves its backup before git runs, ends as
/// 1.4 says, selects the new tip as 1.5 says; git's refusals with their summaries; "Check out
/// {A} and rebase it" with its own refusal of a dirty tree and its return to the user's branch on
/// Abort; Start of the interactive list; and the squash of selected commits.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RebaseCollection)]
public class RepositoryOperationsRebaseTests
{
    // The "rebase" scenario's commits, as Phase4ScenarioTests pins them.
    private const string AddCore = "996fdc12d7467abcae213421b92ad016c5ed26f9";
    private const string TopicOne = "1219e45225ea55ad2c7a0e9412adaeeffd07a43c";
    private const string TopicTwo = "b0dfa3f405dffff59bea3f4f434b16e5490cc597";
    private const string MainWork = "dd6a6fc8095ffa05dc783ce011e322a7241551a4";
    private const string MainMore = "1dd58b8cfc17d4d9b80550eddd80b11ebf94c146";
    private const string OtherOne = "2d4d79a03112c8a5e2f9a783318d588df519af5d";

    [Fact]
    public async Task A_rebase_onto_a_branch_asks_nothing_saves_the_old_tip_and_rebases_onto_the_sha_the_menu_read()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // D135: no question, no dialog; 1.3: git rebase <sha>, no branch argument, no option.
        Assert.Empty(app.Dialogs.Shown);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git rebase {MainMore}" && call.Succeeded);
        Assert.Equal(("Rebased feature/topic onto main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Contains(app.Log.Messages, line => line.Contains("rebase: Rebase feature/topic onto main: Succeeded after ", StringComparison.Ordinal) && line.EndsWith(": Rebased feature/topic onto main", StringComparison.Ordinal));
        AssertRanOnce(app, "Rebase feature/topic onto main");

        Assert.Equal(["Topic two", "Topic one", "Main more", "Main work", "Add core", "Initial commit"], await SubjectsAsync(repo, "feature/topic"));
        Assert.Equal(MainMore, (await repo.GitAsync("rev-parse", "feature/topic~2")).StandardOutput.Trim());
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")));

        // 1.2: one backup, rebase-<time>/feature/topic, the branch's old tip.
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/rebase-{BackupTime}/feature/topic$", backup.Name);
        Assert.Equal(TopicTwo, backup.Sha);

        // Nothing was selected, so nothing is (1.5).
        Assert.True(app.Repository.Selection.IsEmpty);
    }

    [Fact]
    public async Task A_rebase_onto_a_commit_keeps_a_selection_off_the_branch_and_follows_one_of_its_commits()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        // A2 part B: "Main work" was not one of feature/topic's commits: the selection stays on it.
        app.Select(MainWork);
        await app.Operations.RebaseAsync(new RebaseTarget(MainWork, MainWork[..7]));

        Assert.Equal("Rebased feature/topic onto dd6a6fc", app.StatusText);
        Assert.Equal(MainWork, app.Repository.Selection.Single);
        Assert.Equal(MainWork, (await repo.GitAsync("rev-parse", "feature/topic~2")).StandardOutput.Trim());

        // A commit the rebase replays was selected: the branch's new tip is (1.5), whether or not
        // the old commit is still drawn. The commit as the first rebase rewrote it is no longer
        // drawn after the second; the scenario's original, which origin/feature/topic still
        // reaches, is no commit of the branch any more and keeps the selection.
        var rewritten = await app.ShaAsync("feature/topic~1");
        app.Select(rewritten);
        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        var tip = await repo.HeadAsync();
        Assert.Equal("Rebased feature/topic onto main", app.StatusText);
        Assert.Equal(tip, app.Repository.Selection.Single);
        Assert.True(app.Repository.Graph.IndexOf(rewritten) < 0, "No ref reaches the commit the second rebase rewrote.");

        app.Select(TopicOne);
        await app.Operations.RebaseAsync(new RebaseTarget(MainWork, MainWork[..7]));
        Assert.Equal(TopicOne, app.Repository.Selection.Single);
    }

    [Fact]
    public async Task A_branch_that_already_contains_the_target_is_up_to_date_and_its_backup_is_still_saved()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.RebaseAsync(new RebaseTarget(AddCore, AddCore[..7]));

        Assert.Equal("feature/topic is already up to date with 996fdc1", app.StatusText);
        Assert.Equal(TopicTwo, await repo.HeadAsync());
        Assert.Equal(TopicTwo, Assert.Single(await repo.BackupRefsAsync()).Sha);
    }

    [Fact]
    public async Task Git_refuses_a_changed_tracked_file_and_an_untracked_file_in_the_way_with_their_summaries()
    {
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("src/core.txt", "core\nlocal edit\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        // A5 part A step 2.
        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Rebase failed", "Git rebases only a clean working tree: commit or stash your changes first."), (dialog.Title, dialog.Summary));
        AssertOutputOfFailedCall(app, dialog, $"git rebase {MainMore}");
        Assert.Contains("cannot rebase", dialog.Output, StringComparison.Ordinal);
        Assert.Equal(("Rebase feature/topic onto main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(TopicTwo, await repo.HeadAsync());
        Assert.Equal("core\nlocal edit\n", Read(repo, "src/core.txt"));

        // 14.3: the backup was saved before git ran, and stays.
        Assert.Equal(TopicTwo, Assert.Single(await repo.BackupRefsAsync()).Sha);

        // A5 part B: an untracked file where the target has one.
        await repo.GitAsync("checkout", "--", "src/core.txt");
        repo.WriteFile("docs/main.txt", "mine\n");
        await app.Repository.RefreshAsync();
        app.Dialogs.CloseError();

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        dialog = app.Dialogs.Errors[^1];
        Assert.Equal(("Rebase failed", "Untracked files are in the way: move or remove them first."), (dialog.Title, dialog.Summary));
        AssertOutputOfFailedCall(app, dialog, $"git rebase {MainMore}");
        Assert.Equal("mine\n", Read(repo, "docs/main.txt"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_rebase_is_refused_while_an_operation_is_in_progress_or_HEAD_moved_while_it_waited()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.RebaseAsync(new RebaseTarget(await app.ShaAsync("feature/theirs"), "feature/theirs"));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Rebase failed", "Finish or abort the merge first"), (dialog.Title, dialog.Summary));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Empty(await repo.BackupRefsAsync());

        // HEAD moved while it waited (D124): another branch was checked out ahead of it.
        using var rebase = await Scenarios.RebaseAsync();
        using var other = await OperationsHarness.OpenAsync(rebase);
        other.Dialogs.CloseError();
        var (hold, ahead) = HoldQueue(other, () => rebase.CheckoutAsync("main"));
        var waiting = other.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));
        hold.SetResult();
        await ahead;
        await waiting;

        dialog = Assert.Single(other.Dialogs.Errors);
        Assert.Equal("HEAD moved while this waited: it was on feature/topic and is on main now. Nothing was rebased.", dialog.Summary);
        Assert.Empty(await rebase.BackupRefsAsync());
        Assert.Equal(TopicTwo, await rebase.RefTargetAsync("refs/heads/feature/topic"));
    }

    [Fact]
    public async Task A_rebase_that_stops_says_so_in_the_warning_colour_without_a_dialog()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.CheckoutAsync("feature/rebase");
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("main");
        var oldTip = await repo.HeadAsync();

        // C11 step 2.
        await app.Operations.RebaseAsync(new RebaseTarget(main, "main"));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(("Rebase stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Conflicts, app.LastResult?.Outcome);
        var state = app.Operations.Status.InProgress;
        Assert.Equal((InProgressKind.Rebase, "feature/rebase", main, 1, 2), (state.Kind, state.Branch, state.Onto, state.Done, state.Total));
        Assert.Equal(
            $"Rebasing feature/rebase onto main: stopped at {state.Commit![..7]} \"First change\" (1 of 2). 1 file has conflicts.",
            app.Repository.Banner.Text);
        Assert.Equal(oldTip, Assert.Single(await repo.BackupRefsAsync()).Sha);

        // No return file: the rebase did not check out a branch first (1.3).
        Assert.Null(app.Operations.ReadReturnBranch());
    }

    [Fact]
    public async Task Check_out_and_rebase_checks_the_branch_out_rebases_it_and_selects_its_new_tip()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Select(OtherOne);

        // A3 step 5.
        await app.Operations.CheckoutAndRebaseAsync("refs/heads/feature/other", new RebaseTarget(MainMore, "main"));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal("Rebased feature/other onto main", app.StatusText);
        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal(MainMore, (await repo.GitAsync("rev-parse", "feature/other~1")).StandardOutput.Trim());
        Assert.Equal(TopicTwo, await repo.RefTargetAsync("refs/heads/feature/topic"));
        AssertCalledInOrder(app, "git switch --no-guess feature/other", $"git rebase {MainMore}");
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/rebase-{BackupTime}/feature/other$", backup.Name);
        Assert.Equal(OtherOne, backup.Sha);
        Assert.Equal(await repo.HeadAsync(), app.Repository.Selection.Single);
    }

    [Fact]
    public async Task Check_out_and_rebase_refuses_a_changed_tracked_file_before_the_checkout()
    {
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("src/core.txt", "core\nlocal edit\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        // A5 part A step 3: the app's own refusal, no git output.
        await app.Operations.CheckoutAndRebaseAsync("refs/heads/feature/other", new RebaseTarget(MainMore, "main"));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Rebase failed", "Git rebases only a clean working tree: commit or stash your changes first."), (dialog.Title, dialog.Summary));
        Assert.True(string.IsNullOrEmpty(dialog.Output), "The app's own refusal shows no git output.");
        Assert.Equal(("Check out feature/other and rebase it onto main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git switch", StringComparison.Ordinal) || call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(OtherOne, await repo.RefTargetAsync("refs/heads/feature/other"));
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Abort_of_a_rebase_that_checked_out_first_checks_out_again_the_branch_the_user_was_on()
    {
        using var repo = await Scenarios.ConflictsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("main");
        var rebaseTip = await app.ShaAsync("feature/rebase");

        // C13 part C step 10.
        await app.Operations.CheckoutAndRebaseAsync("refs/heads/feature/rebase", new RebaseTarget(main, "main"));

        Assert.Equal(("Rebase stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal("refs/heads/main\n", File.ReadAllText(Path.Combine(repo.Path, ".git", "rebase-merge", RepositoryOperations.ReturnFileName)));
        Assert.Equal("refs/heads/main", app.Operations.ReadReturnBranch());

        // Steps 11 and 12: the question says main is checked out again, and it is.
        app.Dialogs.Confirm();
        await app.Operations.AbortAsync();

        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Abort the rebase?", "Abort the rebase of feature/rebase? The branch goes back to where it was before it started, and main is checked out again."),
            (question.Title, question.Question));
        Assert.Equal("Aborted the rebase", app.StatusText);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(main, await repo.HeadAsync());
        Assert.Equal(rebaseTip, await repo.RefTargetAsync("refs/heads/feature/rebase"));
        AssertCalledInOrder(app, "git rebase --abort", "git switch --no-guess main");
        var backups = (await repo.BackupRefsAsync()).Select(entry => entry.Name).ToList();
        Assert.Equal(3, backups.Count);
        Assert.Contains(backups, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/rebase-{BackupTime}/feature/rebase$"));
        Assert.Contains(backups, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/abort-{BackupTime}$"));
        Assert.Contains(backups, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/abort-{BackupTime}-changes$"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Start_of_the_interactive_list_runs_the_plan_and_says_how_many_commits_the_branch_has()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var list = await app.Repository.Repository.ReadRebaseListAsync(MainMore, "refs/heads/feature/topic", fromCommit: false, options: null, TestContext.Current.CancellationToken);
        Assert.Equal([TopicOne, TopicTwo], list.CommitsOldestFirst.Select(entry => entry.Sha));
        var request = new InteractiveRebaseRequest(new RebaseTarget(MainMore, "main"), "refs/heads/feature/topic", FromHere: false, CheckoutFirst: false);

        // Drop Topic one, keep Topic two.
        var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/feature/topic", [new RebaseStep(TopicOne, RebaseAction.Drop, null), new RebaseStep(TopicTwo, RebaseAction.Pick, null)]);
        Assert.True(await app.Operations.RunInteractiveRebaseAsync(plan, request));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal("Rebased feature/topic: 1 commit", app.StatusText);
        AssertRanOnce(app, "Interactive rebase of feature/topic");
        Assert.Equal(["Topic two", "Main more", "Main work", "Add core", "Initial commit"], await SubjectsAsync(repo, "feature/topic"));
        Assert.Equal(TopicTwo, Assert.Single(await repo.BackupRefsAsync()).Sha);

        // The list read before the branch moved: Start refuses (D124) and the list stays open.
        app.Dialogs.CloseError();
        Assert.False(await app.Operations.RunInteractiveRebaseAsync(plan, request));
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Interactive rebase failed", "feature/topic changed while the list was open: open it again. Nothing was rebased."), (dialog.Title, dialog.Summary));
        Assert.Single(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Start_of_a_drops_list_checks_the_branch_out_first()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var list = await app.Repository.Repository.ReadRebaseListAsync(MainMore, "refs/heads/feature/other", fromCommit: false, options: null, TestContext.Current.CancellationToken);
        var request = new InteractiveRebaseRequest(new RebaseTarget(MainMore, "main"), "refs/heads/feature/other", FromHere: false, CheckoutFirst: true);
        var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/feature/other", [new RebaseStep(OtherOne, RebaseAction.Reword, "Other one, reworded\n\n#1 kept")]);

        Assert.True(await app.Operations.RunInteractiveRebaseAsync(plan, request));

        Assert.Equal("Rebased feature/other: 1 commit", app.StatusText);
        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal("Other one, reworded\n\n#1 kept", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd());
        AssertCalledInOrder(app, "git switch --no-guess feature/other");
        Assert.Equal(OtherOne, Assert.Single(await repo.BackupRefsAsync()).Sha);
    }

    [Fact]
    public async Task Opening_the_list_is_refused_while_an_operation_is_in_progress()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var main = await app.ShaAsync("main");

        await app.Operations.OpenInteractiveRebaseAsync(new InteractiveRebaseRequest(new RebaseTarget(main, "main"), "refs/heads/main", FromHere: true, CheckoutFirst: false));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Interactive rebase failed", "Finish or abort the merge first"), (dialog.Title, dialog.Summary));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git log", StringComparison.Ordinal) && call.CommandText.Contains("--topo-order", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Squash_asks_for_the_message_with_gits_squash_message_and_selects_the_squashed_commit()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var two = await app.ShaAsync("main~4");
        var fixOne = await app.ShaAsync("main~3");
        var three = await app.ShaAsync("main~2");
        MessageDialogViewModel? asked = null;
        app.Dialogs.Answer<MessageDialogViewModel>(dialog =>
        {
            asked = dialog;
            dialog.Description += "\n\n#7 squashed";
            dialog.ConfirmCommand.Execute(null);
        });

        // Section 3: Two, Fix one and Three, a chain on main's line; the menu captured them in any order.
        await app.Operations.SquashAsync([three, two, fixOne]);

        Assert.NotNull(asked);
        Assert.Equal(("Squash 3 commits", "Squash", "Two"), (asked!.Title, asked.ConfirmText, asked.Summary));
        Assert.Equal("Fix one\n\nThree\n\n#42 is the issue\n\n#7 squashed", asked.Description);

        var squashed = await app.ShaAsync("main~2");
        Assert.Equal($"Squashed 3 commits into {squashed[..7]}", app.StatusText);
        Assert.Equal(squashed, app.Repository.Selection.Single);
        Assert.Equal(["Five", "Four", "Two", "One", "Base", "Initial commit"], await SubjectsAsync(repo, "main"));
        Assert.Equal("Two\n\nFix one\n\nThree\n\n#42 is the issue\n\n#7 squashed", (await repo.GitAsync("log", "-1", "--format=%B", squashed)).StandardOutput.TrimEnd());
        Assert.Single(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Squash_cancelled_in_its_dialog_changes_nothing()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        app.Dialogs.Cancel();

        await app.Operations.SquashAsync([await app.ShaAsync("main~1"), head]);

        Assert.Equal(head, await repo.HeadAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_backups_clean_up_runs_after_an_operation_that_saved_one_and_removes_old_backups()
    {
        // Section 9, D139: an old backup (named as 40 days old) goes after the rebase that saved a new one.
        using var repo = await Scenarios.RebaseAsync();
        var old = DateTimeOffset.UtcNow.AddDays(-40).ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        await repo.GitAsync("update-ref", $"refs/visualcommit/backup/reset-{old}", TopicOne);
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // Nothing on screen: the status is the rebase's, and no dialog opened.
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.StartsWith("refs/visualcommit/backup/rebase-", backup.Name, StringComparison.Ordinal);
        Assert.Equal("Rebased feature/topic onto main", app.StatusText);
        Assert.Empty(app.Dialogs.Shown);

        // An operation that saved none does not clean up: the old one, named again, stays.
        await repo.GitAsync("update-ref", $"refs/visualcommit/backup/reset-{old}", TopicOne);
        await app.Operations.CherryPickAsync(OtherOne);
        Assert.Equal(2, (await repo.BackupRefsAsync()).Count);
    }

    /// <summary>The subjects of <paramref name="branch"/>'s history, newest first.</summary>
    private static async Task<List<string>> SubjectsAsync(TempRepo repo, string branch) =>
        [.. (await repo.GitAsync("log", "--format=%s", branch)).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    /// <summary>The app ran these commands, each after the one before (other calls between them allowed).</summary>
    private static void AssertCalledInOrder(OperationsHarness app, params string[] commands)
    {
        var calls = app.Calls.Snapshot().Select(call => call.CommandText).ToList();
        var at = -1;
        foreach (var command in commands)
        {
            var next = calls.FindIndex(at + 1, call => call == command);
            Assert.True(next > at, $"The app did not run \"{command}\" after the calls before it. It ran:\n" + string.Join('\n', calls));
            at = next;
        }
    }
}
