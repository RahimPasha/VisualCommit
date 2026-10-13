using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using Stop = VisualCommit.Testing.Scenarios.ConflictsStop;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: lists of picks and reverts with one git call (docs/phase-4-design.md section 4, D136,
/// D145), and Continue and Skip (5.1 to 5.4, 7.2, D144) through <see cref="RepositoryOperations"/>
/// against real git: the status of every way a list ends, Continue for a merge, a list and a rebase
/// (the stage panel's message, the operation's own, the author of a rebase's commit), the question
/// about staged markers, Skip's backup, Abort of a list, and the refusals when the state changed.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RebaseCollection)]
public class RepositoryOperationsSequenceTests
{
    [Fact]
    public async Task A_list_of_picks_is_one_git_call_that_stops_and_Continue_commits_the_stage_panels_message_then_picks_the_rest()
    {
        using var repo = await Scenarios.ConflictsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var clean = await app.ShaAsync("feature/picks~2");
        var conflicting = await app.ShaAsync("feature/picks~1");
        var last = await app.ShaAsync("feature/picks");
        var main = await repo.HeadAsync();

        // C12 step 2: the menu captured the commits in any order; git gets them oldest first.
        await app.Operations.CherryPickSeveralAsync([last, conflicting, clean]);

        Assert.Single(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git cherry-pick ", StringComparison.Ordinal));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git cherry-pick {clean} {conflicting} {last}");
        Assert.Equal(($"Cherry-picked 1 of 3 commits; stopped at {conflicting[..7]} with conflicts in 1 file", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Shown);
        var state = app.Operations.Status.InProgress;
        Assert.Equal((InProgressKind.CherryPick, conflicting, true, true, 1), (state.Kind, state.Commit, state.Sequenced, state.HasPickHead, state.Remaining));
        Assert.Equal(
            $"Cherry-picking {conflicting[..7]} \"Pick conflicting\". 1 file has conflicts. 1 more commit waits: Continue picks it.",
            app.Repository.Banner.Text);
        Assert.Equal(main, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());

        // Resolved and staged; C12 step 8: the stage panel's message, its "#" line kept (D68).
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesOurs.Replace("line 2 ours", "line 2 ours and picked", StringComparison.Ordinal));
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, "Pick conflicting, resolved\n\n#7 Line 2 keeps main's text.");

        Assert.Equal(("Cherry-pick finished", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, "Continue the cherry-pick");
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(["Pick last", "Pick conflicting, resolved", "Pick clean", "Ours edits"], (await repo.GitAsync("log", "-4", "--format=%s")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("Pick conflicting, resolved\n\n#7 Line 2 keeps main's text.", (await repo.GitAsync("log", "-1", "--format=%B", "HEAD~1")).StandardOutput.TrimEnd());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        Assert.False(app.Operations.Status.InProgress.IsActive);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Abort_of_a_list_asks_the_question_that_drops_the_commits_already_made()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.CherryPicks);
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = File.ReadAllText(Path.Combine(repo.Path, ".git", "sequencer", "head")).Trim();
        Assert.NotEqual(main, await repo.HeadAsync());
        app.Dialogs.Confirm();

        // C13 part B steps 7 and 8.
        await app.Operations.AbortAsync();

        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Abort the cherry-pick?", "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.", "Abort cherry-pick"),
            (question.Title, question.Question, question.ConfirmText));
        Assert.Equal("Aborted the cherry-pick", app.StatusText);
        Assert.Equal(main, await repo.HeadAsync());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Skip_saves_head_and_the_resolution_then_skips_and_the_rebase_stops_at_the_next_commit()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Rebase);
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("main");
        var state = app.Operations.Status.InProgress;
        Assert.Equal((InProgressKind.Rebase, 1, 2), (state.Kind, state.Done, state.Total));

        // C13 part A step 3 (the question is RepositoryViewModel's, before this).
        await app.Operations.SkipAsync(state);

        Assert.Equal(("Rebase stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, $"Skip {state.Commit![..7]}");
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git rebase --skip");
        var next = app.Operations.Status.InProgress;
        Assert.Equal(("Second change", 2, 2), (next.Subject, next.Done, next.Total));
        Assert.Equal(main, await repo.HeadAsync());

        // skip-<time> → HEAD at the stop, skip-<time>-changes → the conflicted file, markers included.
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        var skip = Assert.Single(backups, entry => Regex.IsMatch(entry.Name, $"^refs/visualcommit/backup/skip-{BackupTime}$"));
        Assert.Equal(main, skip.Sha);
        var changes = Assert.Single(backups, entry => Regex.IsMatch(entry.Name, $"^refs/visualcommit/backup/skip-{BackupTime}-changes$"));
        Assert.Equal(main, (await repo.GitAsync("rev-parse", changes.Sha + "^1")).StandardOutput.Trim());
        Assert.Contains("<<<<<<< HEAD", await ShowAsync(repo, changes.Sha, "notes.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Continue_of_a_rebase_commits_with_the_stopped_commits_author_and_goes_on_to_the_end()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.CheckoutAsync("feature/rebase");
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("main");
        var firstDate = (await repo.GitAsync("log", "-1", "--format=%at", "feature/rebase~1")).StandardOutput.Trim();
        await app.Operations.RebaseAsync(new RebaseTarget(main, "main"));

        // C11 steps 6 to 8: resolved, then the banner's Continue (no message: the rebase's own).
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesRebaseFirstResolved);
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal(("Rebase stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        AssertCalledInOrder(app, "git commit --no-verify --cleanup=whitespace --file=-", "git rebase --continue");
        Assert.Equal(("First change", firstDate, main), ((await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim(), (await repo.GitAsync("log", "-1", "--format=%at")).StandardOutput.Trim(), (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim()));
        Assert.Equal(("Second change", 2, 2), (app.Operations.Status.InProgress.Subject, app.Operations.Status.InProgress.Done, app.Operations.Status.InProgress.Total));

        // Steps 10 and 11: the second stop, resolved, continued to the end.
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesSecond);
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal(("Rebased feature/rebase onto main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal("feature/rebase", await repo.CurrentBranchAsync());
        Assert.Equal(["Second change", "First change", "Ours edits"], (await repo.GitAsync("log", "-3", "--format=%s")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")));

        // Continue saves no backup: only the rebase's own one.
        Assert.Single(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Continue_of_a_merge_commits_it_with_the_message_git_left()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesTheirs);
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();

        // C10 step 12.
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal("Merge committed", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git diff --cached --check --no-color --no-ext-diff -- notes.txt");
        Assert.Equal("Merge branch 'feature/theirs'", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd());
        Assert.Equal(2, (await repo.GitAsync("log", "-1", "--format=%P")).StandardOutput.Trim().Split(' ').Length);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task The_banners_Continue_commits_the_stage_panels_message_when_the_user_changed_it()
    {
        // 5.4 and 10.3: "Continue commits the merge with the message in the stage panel."
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesTheirs);
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();
        var changes = app.Repository.Changes;
        await WaitUntilAsync(() => changes.Summary == "Merge branch 'feature/theirs'", "the stage panel to fill in MERGE_MSG");
        Assert.Null(changes.ContinueMessage);
        changes.Summary = "Merge feature/theirs by hand";
        changes.Description = "#12 Theirs wins.";

        await app.Repository.Banner.ContinueCommand.ExecuteAsync(null);

        Assert.Equal("Merge committed", app.StatusText);
        Assert.Equal("Merge feature/theirs by hand\n\n#12 Theirs wins.", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd());
        await WaitUntilAsync(() => changes.Summary.Length == 0 && changes.Description.Length == 0, "the committed message to leave the boxes");

        // The boxes as filled in: no message, so git's own (MERGE_MSG less its comment block).
        using var untouched = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var other = await OperationsHarness.OpenAsync(untouched);
        untouched.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesTheirs);
        await untouched.GitAsync("add", "notes.txt");
        await other.Repository.RefreshAsync();
        await WaitUntilAsync(() => other.Repository.Changes.Summary.Length > 0, "the stage panel to fill in MERGE_MSG");

        await other.Repository.Banner.ContinueCommand.ExecuteAsync(null);

        Assert.Equal("Merge committed", other.StatusText);
        Assert.Equal("Merge branch 'feature/theirs'", (await untouched.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd());
    }

    /// <summary>Waits, up to ten seconds, until <paramref name="condition"/> holds on the UI's view models.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, $"Timed out waiting for {what}.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Continue_asks_first_when_the_files_conflicted_at_the_stop_have_markers_staged()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);

        // The file staged as git left it, its markers in it (7.2, C14).
        await repo.GitAsync("add", "notes.txt");
        await app.Repository.RefreshAsync();
        var state = app.Operations.Status.InProgress;
        app.Dialogs.Confirm(yes: false);

        await app.Operations.ContinueAsync(state, null);

        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("1 file still has conflict markers.", "Continue anyway? The markers are committed with it.", string.Empty, "Continue", false, "Cancel"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Equal(0, Ran(app, "Continue the merge"));
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));

        // "Continue" goes ahead: the markers are committed.
        app.Dialogs.Confirm();
        await app.Operations.ContinueAsync(state, null);
        Assert.Equal("Merge committed", app.StatusText);
        Assert.Contains("<<<<<<< HEAD", await ShowAsync(repo, "HEAD", "notes.txt"), StringComparison.Ordinal);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_banners_Skip_drops_a_message_typed_for_the_skipped_commit_and_the_next_stop_fills_its_own()
    {
        // The review's finding 17: the corrected message of "First change" stayed after Skip, and
        // Continue at "Second change" committed it.
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Rebase);
        using var app = await OperationsHarness.OpenAsync(repo);
        var changes = app.Repository.Changes;
        await WaitUntilAsync(() => changes.Summary == "First change", "the stage panel to fill in the stopped commit's message");
        changes.Summary = "First change, corrected";
        app.Dialogs.Confirm();

        await app.Repository.Banner.SkipCommand.ExecuteAsync(null);

        Assert.Equal("Second change", app.Operations.Status.InProgress.Subject);
        await WaitUntilAsync(() => changes.Summary == "Second change", "the next stop's own message");
        Assert.Null(changes.ContinueMessage);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Continue_is_refused_when_the_state_changed_since_the_click()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var clicked = app.Operations.Status.InProgress;
        await repo.GitAsync("merge", "--abort");
        app.Dialogs.CloseError();

        await app.Operations.ContinueAsync(clicked, null);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Continue failed", "The merge changed while this waited: look at it again. Nothing was continued."), (dialog.Title, dialog.Summary));
        Assert.Equal(("Continue the merge failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        // Conflicts are back: Continue's own reason.
        using var stopped = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var other = await OperationsHarness.OpenAsync(stopped);
        other.Dialogs.CloseError();
        await other.Operations.ContinueAsync(other.Operations.Status.InProgress, null);
        Assert.Equal("Resolve the conflicts first (1 file). Nothing was continued.", Assert.Single(other.Dialogs.Errors).Summary);
        Assert.True(File.Exists(Path.Combine(stopped.Path, ".git", "MERGE_HEAD")));
    }

    [Fact]
    public async Task A_rebases_Continue_is_disabled_and_refused_while_another_tracked_file_is_unstaged()
    {
        // The review's finding 1: the resolution staged and README.md changed but not staged; git's
        // --continue would refuse after the app's commit, so the banner's and the stage panel's
        // Continue are disabled with the reason, and a Continue that waited is refused unrun.
        const string reason = "Stage or stash the other changed files first: git continues a rebase only when nothing else is unstaged";
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Rebase);
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesRebaseFirstResolved);
        await repo.GitAsync("add", "notes.txt");
        repo.WriteFile("README.md", "touched by an editor\n");
        await app.Repository.RefreshAsync();
        var state = app.Operations.Status.InProgress;

        Assert.Equal((true, false, reason), (app.Repository.Banner.HasContinue, app.Repository.Banner.CanContinue, app.Repository.Banner.ContinueToolTip));
        Assert.Equal((false, reason), (app.Repository.Changes.CanCommit, app.Repository.Changes.CommitToolTip));
        app.Dialogs.CloseError();

        await app.Operations.ContinueAsync(state, "Typed\n");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Continue failed", reason + ". Nothing was continued."), (dialog.Title, dialog.Summary));
        Assert.Equal(("Continue the rebase failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal) || call.CommandText == "git rebase --continue");
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(state, app.Operations.Status.InProgress);

        // Once README.md is put back, Continue is enabled again and goes on.
        await repo.GitAsync("checkout", "--", "README.md");
        await app.Repository.RefreshAsync();
        Assert.Equal((true, (string?)null), (app.Repository.Banner.CanContinue, app.Repository.Banner.ContinueToolTip));
    }

    [Fact]
    public async Task Continue_that_finishes_a_rebase_whose_stashed_changes_conflict_when_put_back_says_so()
    {
        // The review's findings 12 and 19: git stashed g.txt's change at the start
        // (rebase.autoStash); main changed g.txt too, so putting it back after the last commit
        // conflicts. Continue says so in 1.4's words, and the banner shows the conflicted file.
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("f.txt", "base\n").WriteFile("g.txt", "g base\n");
        await repo.CommitAsync("Initial commit");
        await repo.CreateBranchAsync("topic");
        repo.WriteFile("f.txt", "main\n").WriteFile("g.txt", "g main\n");
        await repo.CommitAsync("Main edits f and g");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("f.txt", "topic\n", "T conflicts");
        await repo.ConfigAsync("rebase.autoStash", "true");
        repo.WriteFile("g.txt", "g mine\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.RebaseAsync(new RebaseTarget(await app.ShaAsync("main"), "main"));
        Assert.Equal("Rebase stopped: 1 file has conflicts", app.StatusText);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        await app.Repository.RefreshAsync();

        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal(
            ("Rebased topic onto main; your changes conflicted when put back: they are also kept in the stash", OperationStatusTone.Warning),
            (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Shown);
        Assert.False(app.Operations.Status.InProgress.IsActive);
        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", app.Repository.Banner.Text);
        Assert.Single(await repo.StashListAsync());
    }

    [Fact]
    public async Task The_git_layers_refusal_of_a_rebases_Continue_over_unstaged_files_says_what_to_do()
    {
        // When the status the app read did not show the change (the wrapper hides it), the git
        // layer's own check refuses (finding 1, part b): "Continue failed", the same sentence.
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Rebase);
        using var app = await OperationsHarness.OpenWrappedAsync(repo, HidingUnstaged.Wrap);
        var head = await repo.HeadAsync();
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesRebaseFirstResolved);
        await repo.GitAsync("add", "notes.txt");
        repo.WriteFile("README.md", "touched by an editor\n");
        await app.Repository.RefreshAsync();
        Assert.True(app.Repository.Banner.CanContinue);
        app.Dialogs.CloseError();

        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Continue failed", "Stage or stash the other changed files first: git continues a rebase only when nothing else is unstaged."),
            (dialog.Title, dialog.Summary));
        Assert.Contains("README.md", dialog.Output ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal));
    }

    /// <summary>An <see cref="IGitRepository"/> whose status reads show no unstaged change other than conflicted files.</summary>
    public class HidingUnstaged : DispatchProxy
    {
        private IGitRepository _inner = null!;

        public static IGitRepository Wrap(IGitRepository inner)
        {
            var proxy = Create<IGitRepository, HidingUnstaged>();
            ((HidingUnstaged)(object)proxy)._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            try
            {
                var result = targetMethod.Invoke(_inner, args);
                return targetMethod.Name == nameof(IGitRepository.ReadStatusAsync) && result is Task<WorkingTreeStatus> status ? HideAsync(status) : result;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(ex.InnerException);
                throw;
            }

            static async Task<WorkingTreeStatus> HideAsync(Task<WorkingTreeStatus> read)
            {
                var status = await read;
                return status with { Unstaged = [.. status.Unstaged.Where(file => file.Kind == FileChangeKind.Conflicted || status.IsUntracked(file.Path))] };
            }
        }
    }

    [Fact]
    public async Task A_pick_of_a_list_that_changes_nothing_stops_and_Skip_goes_on_to_the_end()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alphaOne = await app.ShaAsync("feature/alpha~1");
        var alphaTwo = await app.ShaAsync("feature/alpha");
        await app.Operations.CherryPickAsync(alphaOne);

        // Section 4's "Empty": Alpha one's changes are already on main.
        await app.Operations.CherryPickSeveralAsync([alphaTwo, alphaOne]);

        Assert.Equal(($"Cherry-picked 0 of 2 commits; {alphaOne[..7]} changes nothing", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Shown);
        var state = app.Operations.Status.InProgress;
        Assert.True(state.Empty);
        Assert.Equal((true, "This cherry-pick changes nothing: skip it"), (app.Repository.Banner.HasContinue, app.Repository.Banner.ContinueToolTip));

        await app.Operations.SkipAsync(state);

        Assert.Equal("Cherry-pick finished", app.StatusText);
        Assert.Equal("Alpha two", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.False(app.Operations.Status.InProgress.IsActive);
    }

    [Fact]
    public async Task Continue_and_Skip_of_a_list_that_stops_again_say_how_far_it_came_and_a_refused_commit_opens_the_dialog()
    {
        // Main and the side edit f.txt (each pick conflicts); C adds u.txt, which is untracked on main.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("side");
        await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
        await repo.CheckoutAsync("side");
        var a = await repo.CommitFileAsync("f.txt", "side 1\n", "Side one");
        var b = await repo.CommitFileAsync("f.txt", "side 2\n", "Side two");
        var c = await repo.CommitFileAsync("u.txt", "tracked\n", "Side adds u");
        await repo.CheckoutAsync("main");
        repo.WriteFile("u.txt", "untracked\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.CherryPickSeveralAsync([a, b, c]);
        Assert.Equal(($"Cherry-picked 0 of 3 commits; stopped at {a[..7]} with conflicts in 1 file", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));

        // Skip (its question is RepositoryViewModel's): Side two stops with conflicts. Git keeps no
        // count of the skipped commit, so 10.2's K of N come from its list as it is now.
        await app.Operations.SkipAsync(app.Operations.Status.InProgress);
        Assert.Equal(($"Cherry-picked 0 of 2 commits; stopped at {b[..7]} with conflicts in 1 file", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Errors);

        // Continue commits Side two; git cannot pick Side adds u over the untracked file: 5.1's dialog.
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        await app.Repository.RefreshAsync();
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal(($"Cherry-picked 1 of 2 commits; git could not pick {c[..7]}", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        var state = app.Operations.Status.InProgress;
        Assert.Equal((c, true, false), (state.Commit, state.Sequenced, state.HasPickHead));
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Cherry-pick stopped", app.Repository.Banner.Text), (dialog.Title, dialog.Summary));
        Assert.Equal($"Cherry-pick stopped at {c[..7]} \"Side adds u\": git could not pick it. Skip it, or abort.", dialog.Summary);
        Assert.Contains("untracked working tree files would be overwritten", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("git cherry-pick --continue", dialog.Command);
        Assert.Equal("untracked\n", File.ReadAllText(Path.Combine(repo.Path, "u.txt")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Skip_of_a_single_pick_says_which_commit_it_skipped()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var conflicting = await app.ShaAsync("feature/conflict");
        var head = await repo.HeadAsync();
        await app.Operations.CherryPickAsync(conflicting);

        await app.Operations.SkipAsync(app.Operations.Status.InProgress);

        Assert.Equal($"Skipped {conflicting[..7]}", app.StatusText);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(app.Operations.Status.InProgress.IsActive);
        Assert.Equal(2, (await repo.BackupRefsAsync()).Count);
    }

    [Fact]
    public async Task A_list_paused_after_a_commit_in_a_terminal_is_continued_with_the_rest()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.CherryPicks);
        repo.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesOurs.Replace("line 2 ours", "line 2 ours and picked", StringComparison.Ordinal));
        await repo.GitAsync("add", "notes.txt");
        await repo.GitAsync("commit", "--no-edit");
        using var app = await OperationsHarness.OpenAsync(repo);
        var state = app.Operations.Status.InProgress;
        Assert.Equal((true, false, true), (state.Sequenced, state.HasPickHead, state.AbortKeepsHead));
        Assert.Equal("Cherry-pick paused: 1 more commit waits. Continue picks it.", app.Repository.Banner.Text);
        Assert.Equal((true, "There is nothing to skip: the stopped commit was committed"), (app.Repository.Banner.HasSkip, app.Repository.Banner.SkipToolTip));

        await app.Operations.ContinueAsync(state, null);

        Assert.Equal("Cherry-pick finished", app.StatusText);
        Assert.Equal("Pick last", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
    }

    [Fact]
    public async Task A_list_git_refuses_at_its_start_is_cleared_and_keeps_the_users_staged_change()
    {
        using var repo = await Scenarios.ConflictsAsync();
        repo.WriteFile("notes.txt", "staged edit\n");
        await repo.GitAsync("add", "notes.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var head = await repo.HeadAsync();

        await app.Operations.CherryPickSeveralAsync([await app.ShaAsync("feature/picks~2"), await app.ShaAsync("feature/picks")]);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Cherry-pick failed", "Git would overwrite your changes: commit or stash them first. Nothing was cherry-picked."), (dialog.Title, dialog.Summary));
        Assert.NotEqual(string.Empty, dialog.Output);
        Assert.Equal(("Cherry-pick 2 commits failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        Assert.Equal("staged edit\n", (await repo.GitAsync("show", ":notes.txt")).StandardOutput);
    }

    [Fact]
    public async Task Several_reverts_are_one_git_call_newest_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await repo.CommitFileAsync("src/a.txt", "a\n", "Add a");
        await repo.CommitFileAsync("src/b.txt", "b\n", "Add b");
        await app.Repository.RefreshAsync();
        var a = await app.ShaAsync("HEAD~1");
        var b = await app.ShaAsync("HEAD");

        await app.Operations.RevertSeveralAsync([a, b]);

        Assert.Single(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git revert ", StringComparison.Ordinal));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git revert --no-edit {b} {a}");
        Assert.Equal("Reverted 2 commits", app.StatusText);
        Assert.Equal(["Revert \"Add a\"", "Revert \"Add b\""], (await repo.GitAsync("log", "-2", "--format=%s")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

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
