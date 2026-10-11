using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using Stopped = VisualCommit.Testing.Scenarios.InProgressKind;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP7-branches: cherry-pick, revert and abort in <see cref="RepositoryOperations"/>
/// (docs/phase-3-design.md 2.12 to 2.15, 2.31, section 5's Abort),
/// against real git and the scenario repos, through <see cref="OperationsHarness"/>: each
/// operation's confirmation, the backup refs it saves and what they hold, what git holds after it,
/// the status bar's text and colour, the error dialog with git's output and its extra button,
/// cancel, and that each ran once. WP7-branches' tests are four classes, so that xunit runs them
/// side by side: <see cref="RepositoryOperationsCheckoutTests"/>, <see cref="RepositoryOperationsBranchesTests"/>,
/// <see cref="RepositoryOperationsMergeTests"/> and <see cref="RepositoryOperationsHistoryTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
public class RepositoryOperationsHistoryTests
{
    // ---- 2.12, 2.13: cherry-pick ---------------------------------------------------------------

    [Fact]
    public async Task A_cherry_pick_applies_the_commit_on_head()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");

        await app.Operations.CherryPickAsync(alphaOne);

        Assert.Equal($"Cherry-picked {alphaOne[..7]} onto main", app.StatusText);
        Assert.Equal(mainWork, (await repo.GitAsync("log", "-1", "--format=%P")).StandardOutput.Trim());
        Assert.Equal("Alpha one", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());
        Assert.Equal(Scenarios.OperationsFiles.AlphaOne, Read(repo, "src/alpha.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD")));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, Ran(app, $"Cherry-pick {alphaOne[..7]}"));
    }

    [Fact]
    public async Task A_cherry_pick_that_conflicts_stops_and_one_that_changes_nothing_says_so()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var conflicting = await app.ShaAsync("refs/heads/feature/conflict");
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");
        app.Dialogs.CloseError();

        await app.Operations.CherryPickAsync(conflicting);

        Assert.Equal(("Cherry-pick stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(InProgressKind.CherryPick, app.Operations.Status.InProgress.Kind);

        await repo.GitAsync("cherry-pick", "--abort");
        await app.Operations.CherryPickAsync(alphaOne);
        await app.Operations.CherryPickAsync(alphaOne);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Cherry-pick stopped", $"{alphaOne[..7]} changes nothing on main: its changes are already there. Abort the cherry-pick to go on."),
            (dialog.Title, dialog.Summary));
        Assert.Equal(($"Cherry-pick {alphaOne[..7]} failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD")));
        Assert.True(app.Operations.Status.InProgress.Empty);

        // Check 25 step 7: git's own output and the command of the failed call (2.12, 1.6), which
        // the operation reads from the session's call log.
        AssertOutputOfFailedCall(app, dialog, $"git cherry-pick {alphaOne}");
        Assert.StartsWith("The previous cherry-pick is now empty", dialog.Output, StringComparison.Ordinal);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_revert_that_stops_on_a_commit_that_changes_nothing_shows_the_banner_and_no_dialog()
    {
        // D117's other case, check 12 part E: a git that keeps REVERT_HEAD after a revert that
        // changes nothing (Git 2.36 and 2.55 fail it instead, which the test above covers).
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenWrappedAsync(repo, StoppingRevert.Wrap);
        var mainWork = await repo.HeadAsync();
        await repo.GitAsync("revert", "--no-edit", mainWork);
        await app.Repository.RefreshAsync();
        var head = await repo.HeadAsync();

        await app.Operations.RevertAsync(mainWork);

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(($"Revert stopped: {mainWork[..7]} changes nothing on main: it is already undone.", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Conflicts, app.LastResult?.Outcome);
        Assert.Equal((InProgressKind.Revert, true), (app.Operations.Status.InProgress.Kind, app.Operations.Status.InProgress.Empty));
        Assert.Equal(($"Reverting {mainWork[..7]} \"Main work\" changes nothing: it is already undone. Abort to go on.", "Abort revert"), (app.Repository.Banner.Text, app.Repository.Banner.AbortText));
        Assert.Equal(head, await repo.HeadAsync());

        // Several: the count so far, then the same sentence; still no dialog.
        File.Delete(Path.Combine(repo.Path, ".git", "REVERT_HEAD"));
        await app.Repository.RefreshAsync();
        var addCore = await app.ShaAsync("main~2");
        await app.Operations.RevertSeveralAsync([addCore, mainWork]);

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(($"Reverted 0 of 2 commits. {mainWork[..7]} changes nothing on main: it is already undone.", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    /// <summary>
    /// The real repository, except that <see cref="IGitRepository.RevertAsync"/> stops as a git
    /// that keeps <c>REVERT_HEAD</c> after a revert that changes nothing would (D117): it leaves
    /// <c>REVERT_HEAD</c> naming the commit, changes nothing else and answers <see cref="ApplyResult.Empty"/>.
    /// </summary>
    public class StoppingRevert : DispatchProxy
    {
        private IGitRepository _inner = null!;

        public static IGitRepository Wrap(IGitRepository inner)
        {
            var proxy = Create<IGitRepository, StoppingRevert>();
            ((StoppingRevert)(object)proxy)._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IGitRepository.RevertAsync))
            {
                File.WriteAllText(Path.Combine(_inner.GitDirectory, "REVERT_HEAD"), (string)args![0]! + "\n");
                return Task.FromResult(ApplyResult.Empty);
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(ex.InnerException);
                throw;
            }
        }
    }


    [Fact]
    public async Task Several_commits_are_picked_one_at_a_time_oldest_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alphaTwo = await app.ShaAsync("refs/heads/feature/alpha");
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");

        await app.Operations.CherryPickSeveralAsync([alphaTwo, alphaOne]);

        Assert.Equal("Cherry-picked 2 commits onto main", app.StatusText);
        Assert.Equal("Alpha two\nAlpha one\nMain work", (await repo.GitAsync("log", "-3", "--format=%s")).StandardOutput.Trim().ReplaceLineEndings("\n"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, Read(repo, "src/alpha.txt"));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD")));
        Assert.Equal(1, Ran(app, "Cherry-pick 2 commits"));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Picking_several_stops_at_the_first_conflict_and_names_what_was_not_picked()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CreateBranchAsync("later");
        await repo.CheckoutAsync("later");
        var later = await repo.CommitFileAsync("docs/later.txt", "later\n", "Later work");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var conflicting = await app.ShaAsync("refs/heads/feature/conflict");
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");

        await app.Operations.CherryPickSeveralAsync([later, conflicting]);

        Assert.Equal(($"Cherry-picked 0 of 2 commits. {conflicting[..7]} stopped with conflicts in 1 file; not picked: {later[..7]} \"Later work\".", OperationStatusTone.Warning),
            (app.StatusText, app.StatusTone));
        Assert.Equal(InProgressKind.CherryPick, app.Operations.Status.InProgress.Kind);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        Assert.Empty(app.Dialogs.Shown);

        await repo.GitAsync("cherry-pick", "--abort");
        await app.Repository.RefreshAsync();
        await app.Operations.CherryPickSeveralAsync([conflicting, alphaOne]);

        Assert.Equal($"Cherry-picked 1 of 2 commits. {conflicting[..7]} stopped with conflicts in 1 file.", app.StatusText);
        Assert.Equal("Alpha one", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal(conflicting, File.ReadAllText(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD")).Trim());
    }

    [Fact]
    public async Task Picking_several_reports_an_empty_pick_or_a_failed_one_with_the_count_so_far()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alphaTwo = await app.ShaAsync("refs/heads/feature/alpha");
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");
        var ff = await app.ShaAsync("refs/heads/feature/ff");
        app.Dialogs.CloseError().CloseError();

        await app.Operations.CherryPickAsync(alphaOne);
        await app.Operations.CherryPickSeveralAsync([alphaTwo, alphaOne]);

        Assert.Equal(("Cherry-pick stopped", $"Cherry-picked 0 of 2 commits. {alphaOne[..7]} changes nothing on main: its changes are already there. Abort the cherry-pick to go on."),
            (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary));
        AssertOutputOfFailedCall(app, app.Dialogs.Errors[0], $"git cherry-pick {alphaOne}");
        Assert.Equal("Cherry-pick 2 commits failed", app.StatusText);

        await repo.GitAsync("cherry-pick", "--abort");
        repo.WriteFile("docs/ff.txt", "in the way\n");
        await app.Repository.RefreshAsync();
        await app.Operations.CherryPickSeveralAsync([ff, alphaTwo]);

        // Alpha two (older) is picked first; Fast-forward me then finds an untracked file in its way.
        Assert.Equal(("Cherry-pick failed", $"Cherry-picked 1 of 2 commits. Git could not cherry-pick {ff[..7]}."), (app.Dialogs.Errors[1].Title, app.Dialogs.Errors[1].Summary));
        Assert.Contains("docs/ff.txt", app.Dialogs.Errors[1].Output, StringComparison.Ordinal);
        Assert.Equal(("Cherry-pick 2 commits failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("Alpha two", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        app.AssertScriptFollowed();
    }

    // ---- 2.14: revert --------------------------------------------------------------------------

    [Fact]
    public async Task A_revert_undoes_the_commit_and_one_already_undone_fails_with_gits_output()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        app.Dialogs.CloseError();

        await app.Operations.RevertAsync(mainWork);

        Assert.Equal($"Reverted {mainWork[..7]}", app.StatusText);
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {mainWork}.", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim().ReplaceLineEndings("\n"));
        Assert.Equal(Scenarios.OperationsFiles.Core, Read(repo, "src/core.txt"));
        Assert.Equal(1, Ran(app, $"Revert {mainWork[..7]}"));
        var head = await repo.HeadAsync();

        await app.Operations.RevertAsync(mainWork);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Revert failed", $"{mainWork[..7]} changes nothing on main: it is already undone."), (dialog.Title, dialog.Summary));
        Assert.Contains("nothing to commit", dialog.Output, StringComparison.Ordinal);
        Assert.StartsWith("git revert --no-edit ", dialog.Command, StringComparison.Ordinal);
        Assert.Equal(($"Revert {mainWork[..7]} failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(InProgressKind.None, app.Operations.Status.InProgress.Kind);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_merge_commit_is_reverted_against_its_first_parent()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        var mainWork = await repo.HeadAsync();
        await app.Operations.MergeAsync("refs/heads/feature/alpha", MergeMode.NoFastForward);
        var merge = await repo.HeadAsync();
        await app.Operations.RevertAsync(merge);

        Assert.Equal($"Reverted {merge[..7]}", app.StatusText);
        Assert.Equal(merge, (await repo.GitAsync("log", "-1", "--format=%P")).StandardOutput.Trim());
        Assert.Equal("D\tsrc/alpha.txt", (await repo.GitAsync("diff", "--name-status", "HEAD^", "HEAD")).StandardOutput.Trim());

        // -m 1: git names the first parent as the side kept.
        Assert.Equal(
            $"Revert \"Merge branch 'feature/alpha'\"\n\nThis reverts commit {merge}, reversing\nchanges made to {mainWork}.",
            (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim().ReplaceLineEndings("\n"));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task A_revert_that_conflicts_stops_and_several_are_reverted_newest_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var addCore = await app.ShaAsync("main~1");

        await app.Operations.RevertSeveralAsync([addCore, mainWork]);

        Assert.Equal("Reverted 2 commits", app.StatusText);
        Assert.Equal("Revert \"Add core\"\nRevert \"Main work\"\nMain work", (await repo.GitAsync("log", "-3", "--format=%s")).StandardOutput.Trim().ReplaceLineEndings("\n"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.Equal(1, Ran(app, "Revert 2 commits"));

        await repo.GitAsync("reset", "--hard", mainWork);
        await repo.CommitFileAsync("src/core.txt", Scenarios.OperationsFiles.CoreAgain, "Core again");
        await app.Repository.RefreshAsync();
        await app.Operations.RevertSeveralAsync([addCore, mainWork]);

        Assert.Equal(($"Reverted 0 of 2 commits. {mainWork[..7]} stopped with conflicts in 1 file; not reverted: {addCore[..7]} \"Add core\".", OperationStatusTone.Warning),
            (app.StatusText, app.StatusTone));
        Assert.Equal(InProgressKind.Revert, app.Operations.Status.InProgress.Kind);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));

        await repo.GitAsync("revert", "--abort");
        await app.Repository.RefreshAsync();
        await app.Operations.RevertAsync(mainWork);

        Assert.Equal(("Revert stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(mainWork, File.ReadAllText(Path.Combine(repo.Path, ".git", "REVERT_HEAD")).Trim());
        Assert.Empty(app.Dialogs.Shown);
    }

    // ---- 2.15: abort ---------------------------------------------------------------------------

    [Fact]
    public async Task Aborting_a_merge_backs_up_head_and_the_conflicted_files_first()
    {
        using var repo = await Scenarios.StoppedAsync(Stopped.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var conflicted = Read(repo, "src/core.txt");

        await app.Operations.AbortAsync(app.Operations.Status.InProgress);

        Assert.Equal("Aborted the merge", app.StatusText);
        Assert.Empty(app.Dialogs.Shown);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.Empty(await PorcelainAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, Read(repo, "src/core.txt"));
        Assert.False(app.Repository.Banner.IsVisible);
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        var head = Assert.Single(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/abort-{BackupTime}$"));
        var changes = Assert.Single(backups, backup => backup.Name == head.Name + "-changes");
        Assert.Equal(mainWork, head.Sha);
        Assert.Equal(mainWork, (await repo.GitAsync("log", "-1", "--format=%P", changes.Sha)).StandardOutput.Trim());
        Assert.Equal(conflicted, await ShowAsync(repo, changes.Sha, "src/core.txt"));
        Assert.Contains("<<<<<<<", conflicted, StringComparison.Ordinal);
        Assert.Equal(1, Ran(app, "Abort the merge"));
    }

    [Fact]
    public async Task The_banners_abort_asks_its_question_and_reaches_the_operation()
    {
        using var repo = await Scenarios.StoppedAsync(Stopped.CherryPick);
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await app.ShaAsync("refs/remotes/origin/main");
        var picked = await repo.HeadAsync();
        app.Dialogs.Confirm(yes: false).Confirm();

        Assert.Same(app.Operations, app.Repository.InProgressActions);
        await app.Repository.Banner.AbortCommand.ExecuteAsync(null);
        Assert.Equal(picked, await repo.HeadAsync());
        Assert.Empty(await repo.BackupRefsAsync());

        await app.Operations.AbortAsync();

        var request = app.Dialogs.Confirmations[1];
        Assert.Equal(("Abort the cherry-pick?", "Abort cherry-pick"), (request.Title, request.ConfirmText));
        Assert.Equal(
            "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.",
            request.Question);
        Assert.Equal("Aborted the cherry-pick", app.StatusText);
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer")));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(picked, Assert.Single(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/abort-{BackupTime}$")).Sha);
        Assert.Equal(2, backups.Count);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task An_abort_git_refuses_shows_abort_failed()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.AbortAsync(new InProgressState(InProgressKind.Merge, null, null, null, null, 0, false));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Abort failed", "Git could not abort the merge."), (dialog.Title, dialog.Summary));
        Assert.StartsWith("git merge --abort", dialog.Command, StringComparison.Ordinal);
        Assert.Equal(("Abort the merge failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }
}
