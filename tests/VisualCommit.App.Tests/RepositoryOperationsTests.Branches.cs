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
using Stopped = VisualCommit.Testing.Scenarios.InProgressKind;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP7-branches: the branch and history operations of <see cref="RepositoryOperations"/>
/// (docs/phase-3-design.md 2.1 to 2.15, 2.31, 4g's "Check out and merge", section 5's Abort)
/// against real git and the scenario repos, through <see cref="OperationsHarness"/>: each
/// operation's confirmation, the backup refs it saves and what they hold, what git holds after it,
/// the status bar's text and colour, the error dialog with git's output and its extra button,
/// cancel, a closed tab, and that each ran once.
/// </summary>
public partial class RepositoryOperationsTests
{
    private const string BackupTime = "[0-9]{8}T[0-9]{9}Z";

    // ---- 2.1: check out a local branch ---------------------------------------------------------

    [Fact]
    public async Task Checking_out_a_local_branch_switches_to_it_and_says_so()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.CheckoutAsync("refs/heads/feature/ff");

        Assert.Equal("feature/ff", await repo.CurrentBranchAsync());
        Assert.Equal(("Checked out feature/ff", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal("feature/ff", app.Operations.CurrentBranch?.Name);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(1, Ran(app, "Check out feature/ff"));
    }

    [Fact]
    public async Task A_checkout_blocked_by_tracked_changes_stashes_checks_out_and_reapplies_them_on_request()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.CheckoutAsync("refs/heads/feature/other");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "Your changes to these files would be overwritten by checking out feature/other.", "Stash changes and check out"),
            (dialog.Title, dialog.Summary, dialog.ExtraAction));
        Assert.Equal("In blocked", dialog.RepositoryText);
        Assert.Equal("git switch --no-guess feature/other", dialog.Command);
        Assert.StartsWith("error: Your local changes to the following files would be overwritten by checkout:", dialog.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked", dialog.Output, StringComparison.Ordinal);

        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal(("Checked out feature/other and re-applied your changes", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.Equal("one (local)\ntwo\nthree\nfour\nfive\nsix\nseven (other)\n", Read(repo, "notes.txt"));
        Assert.Equal(1, Ran(app, "Check out feature/other"));
        Assert.Equal(1, Ran(app, "Check out feature/other with your changes"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Changes_that_conflict_on_the_new_branch_stay_in_the_stash_and_a_dialog_says_where()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true).CloseError();

        await app.Operations.CheckoutAsync("refs/heads/feature/clash");

        Assert.Equal(2, app.Dialogs.Errors.Count);
        var information = app.Dialogs.Errors[1];
        Assert.Equal("Changes re-applied with conflicts", information.Title);
        Assert.Equal(
            "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file. Your changes are kept in the stash \"VisualCommit: before checking out feature/clash\".",
            information.Summary);
        Assert.False(information.HasOutput);
        Assert.False(information.HasCommand);
        Assert.False(information.HasExtraAction);

        Assert.Equal("feature/clash", await repo.CurrentBranchAsync());
        Assert.Equal(("Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        var stash = Assert.Single(await repo.StashListAsync());
        Assert.Equal("On main: VisualCommit: before checking out feature/clash", stash.Message);
        Assert.Equal(["UU notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.StartsWith("<<<<<<< Updated upstream\none (clash)\n=======\none (local)\n>>>>>>> Stashed changes\n", Read(repo, "notes.txt"), StringComparison.Ordinal);
        Assert.Equal(InProgressKind.None, app.Operations.Status.InProgress.Kind);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_blocked_by_untracked_files_offers_no_stash_and_changes_nothing()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync("refs/heads/feature/addfile");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "Untracked files are in the way: feature/addfile has files with the same names. Move or delete them, then check out again."),
            (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        Assert.Contains("error: Your local changes to the following files would be overwritten by checkout:", dialog.Output, StringComparison.Ordinal);
        Assert.Contains("error: The following untracked working tree files would be overwritten by checkout:", dialog.Output, StringComparison.Ordinal);
        Assert.Equal(("Check out feature/addfile failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.Empty(await repo.StashListAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_git_refuses_for_another_reason_shows_checkout_failed_with_gits_words()
    {
        using var repo = await Scenarios.StoppedAsync(Stopped.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync("refs/heads/feature/alpha");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout failed", "Git could not check out feature/alpha."), (dialog.Title, dialog.Summary));
        Assert.Contains("fatal: cannot switch branch while merging", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("git switch --no-guess feature/alpha", dialog.Command);
        Assert.False(dialog.HasExtraAction);
        Assert.Equal(("Check out feature/alpha failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task When_the_checkout_fails_after_the_stash_the_changes_are_popped_back_at_once()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.StashAndCheckoutAsync("refs/heads/nowhere");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout failed", "Git could not check out nowhere."), (dialog.Title, dialog.Summary));
        Assert.Equal("git switch --no-guess nowhere", dialog.Command);
        Assert.Equal(("Check out nowhere with your changes failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesLocal, Read(repo, "notes.txt"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stashing_and_checking_out_keeps_what_was_staged_staged()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        await repo.GitAsync("add", "notes.txt");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.StashAndCheckoutAsync("refs/heads/feature/other");

        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal("Checked out feature/other and re-applied your changes", app.StatusText);
        Assert.Equal(["M  notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.Empty(await repo.StashListAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    // ---- 2.2: check out a remote branch --------------------------------------------------------

    [Fact]
    public async Task Checking_out_a_remote_branch_without_a_local_one_creates_it_tracking()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--delete", "--force", "feature/alpha");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.CheckoutAsync("refs/remotes/origin/feature/alpha");

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal("origin/feature/alpha", (await repo.GitAsync("rev-parse", "--abbrev-ref", "feature/alpha@{upstream}")).StandardOutput.Trim());
        Assert.Equal(await app.ShaAsync("refs/remotes/origin/feature/alpha"), await repo.HeadAsync());
        Assert.Equal("Created feature/alpha tracking origin/feature/alpha and checked it out", app.StatusText);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, Ran(app, "Check out origin/feature/alpha"));
    }

    [Fact]
    public async Task Checking_out_a_remote_branch_switches_to_the_local_one_of_that_name_as_it_is()
    {
        using var repo = await Scenarios.OperationsAsync();
        var alphaOne = (await repo.GitAsync("rev-parse", "feature/alpha~1")).StandardOutput.Trim();
        await repo.GitAsync("branch", "--force", "feature/alpha", alphaOne);
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.CheckoutAsync("refs/remotes/origin/feature/alpha");

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal(alphaOne, await repo.HeadAsync());
        Assert.Equal("Checked out feature/alpha. It is 1 behind origin/feature/alpha: pull to update it.", app.StatusText);

        await app.Operations.CheckoutAsync("refs/heads/main");
        await repo.GitAsync("branch", "--unset-upstream", "feature/alpha");
        await app.Repository.RefreshAsync();
        await app.Operations.CheckoutAsync("refs/remotes/origin/feature/alpha");

        Assert.Equal("Checked out feature/alpha, which has no upstream", app.StatusText);
        Assert.Equal(alphaOne, await repo.HeadAsync());
    }

    // ---- 2.3: check out a commit or a tag ------------------------------------------------------

    [Fact]
    public async Task Checking_out_a_tag_or_a_commit_detaches_head_there()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var addCore = await app.ShaAsync("refs/tags/v1.0^{commit}");
        var initial = await app.ShaAsync("main~2");

        await app.Operations.CheckoutAsync("refs/tags/v1.0");

        Assert.Equal(addCore, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded);
        Assert.Equal($"HEAD is now detached at {addCore[..7]}", app.StatusText);
        Assert.Equal(1, Ran(app, "Check out v1.0"));

        await app.Operations.CheckoutAsync(initial);

        Assert.Equal(initial, await repo.HeadAsync());
        Assert.Equal($"HEAD is now detached at {initial[..7]}", app.StatusText);
        Assert.Equal(1, Ran(app, $"Check out {initial[..7]}"));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Leaving_a_detached_head_whose_commits_nothing_reaches_asks_and_backs_them_up()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var detached = await repo.HeadAsync();
        var initial = await app.ShaAsync("refs/heads/loc");
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.CheckoutAsync("refs/heads/main");

        Assert.Equal(detached, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(0, Ran(app, "Check out main"));

        await app.Operations.CheckoutAsync("refs/heads/main");

        var request = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Leave these commits?", "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph.", "A backup is saved first, so this can be undone.", "Check out"),
            (request.Title, request.Question, request.Detail, request.ConfirmText));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("Checked out main", app.StatusText);
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/detached-{BackupTime}$", backup.Name);
        Assert.Equal(detached, backup.Sha);
        Assert.Equal("side", (await repo.OpenSibling(Scenarios.EdgeCasesWorktree).GitAsync("symbolic-ref", "--short", "HEAD")).StandardOutput.Trim());

        // From a branch, and back from a detached HEAD that is on a branch: no question, no backup.
        await app.Operations.CheckoutAsync(initial);
        Assert.Equal($"HEAD is now detached at {initial[..7]}", app.StatusText);
        await app.Operations.CheckoutAsync("refs/heads/main");

        Assert.Equal("Checked out main", app.StatusText);
        Assert.Equal(2, app.Dialogs.Confirmations.Count);
        Assert.Single(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_blocked_checkout_from_a_detached_head_asks_about_its_commits_once()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        repo.WriteFile("src/detached.txt", "detached, edited\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var detached = await repo.HeadAsync();
        app.Dialogs.Confirm().CloseError(clickExtra: true).CloseError();

        await app.Operations.CheckoutAsync("refs/heads/main");

        Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal("Checkout blocked", app.Dialogs.Errors[0].Title);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(1, Ran(app, "Check out main with your changes"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/detached-{BackupTime}$", backup.Name);
        Assert.Equal(detached, backup.Sha);
        app.AssertScriptFollowed();
    }

    // ---- 2.4: create a branch ------------------------------------------------------------------

    [Fact]
    public async Task Creating_a_branch_at_a_commit_names_the_commit_and_checks_the_branch_out()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await app.ShaAsync("main");
        app.Dialogs.EnterName("hotfix");

        await app.Operations.CreateBranchAsync(mainWork);

        var dialog = Assert.IsType<BranchNameDialogViewModel>(Assert.Single(app.Dialogs.Shown));
        Assert.Equal(("Create branch", $"From {mainWork[..7]} \"Main work\""), (dialog.Title, dialog.Context));
        Assert.Equal("hotfix", await repo.CurrentBranchAsync());
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Equal("Created hotfix and checked it out", app.StatusText);
        Assert.Equal(1, Ran(app, "Create branch hotfix"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_branch_from_a_remote_branch_tracks_it_and_one_from_a_tag_starts_at_its_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.EnterName("alpha-copy", option: false).EnterName("release-1.0", option: false);

        await app.Operations.CreateBranchAsync("refs/remotes/origin/feature/alpha");

        Assert.Equal("From origin/feature/alpha", ((NameDialogViewModel)app.Dialogs.Shown[0]).Context);
        Assert.Equal("Created alpha-copy", app.StatusText);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(await app.ShaAsync("refs/remotes/origin/feature/alpha"), await repo.RefTargetAsync("refs/heads/alpha-copy"));
        Assert.Equal("branch.alpha-copy.remote origin\nbranch.alpha-copy.merge refs/heads/feature/alpha",
            (await repo.GitAsync("config", "--get-regexp", @"^branch\.alpha-copy\.")).StandardOutput.Trim().ReplaceLineEndings("\n"));

        await app.Operations.CreateBranchAsync("refs/tags/v1.0");

        Assert.Equal("From v1.0", ((NameDialogViewModel)app.Dialogs.Shown[1]).Context);
        Assert.Equal("Created release-1.0", app.StatusText);
        Assert.Equal(await app.ShaAsync("refs/tags/v1.0^{commit}"), await repo.RefTargetAsync("refs/heads/release-1.0"));
        Assert.False((await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.release-1\.0\.")).Succeeded);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Creating_a_branch_at_head_names_heads_commit_and_a_cancelled_dialog_creates_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        app.Dialogs.Cancel().EnterName("topic", option: false);

        await app.Operations.CreateBranchAsync(null);

        Assert.Equal(["feature/alpha", "feature/conflict", "feature/ff", "main"], app.Operations.LocalBranchNames);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await ((IToolbarActions)app.Operations).CreateBranchAtHeadAsync();

        Assert.All(app.Dialogs.Shown, dialog => Assert.Equal($"From {head[..7]} \"Main work\"", ((NameDialogViewModel)dialog).Context));
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/topic"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("Created topic", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_name_the_dialog_refuses_cannot_be_confirmed()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Operations.IgnoreCaseInNames = true;
        app.Dialogs.EnterName("feature").EnterName("Main");

        await app.Operations.CreateBranchAsync(null);
        await app.Operations.CreateBranchAsync(null);

        Assert.Equal(2, app.Dialogs.Problems.Count);
        Assert.Contains("This conflicts with the branch feature/alpha.", app.Dialogs.Problems[0], StringComparison.Ordinal);
        Assert.Contains("A branch named main exists in another case.", app.Dialogs.Problems[1], StringComparison.Ordinal);
        Assert.Equal(["feature/alpha", "feature/conflict", "feature/ff", "main"], app.Operations.LocalBranchNames);
    }

    [Fact]
    public async Task Creating_and_checking_out_a_branch_elsewhere_from_a_detached_head_asks_first()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var detached = await repo.HeadAsync();
        var initial = await app.ShaAsync("refs/heads/loc");
        app.Dialogs.EnterName("here").EnterName("old").Confirm();

        // At HEAD itself nothing is left behind: no question.
        await app.Operations.CreateBranchAsync(null);
        Assert.Equal("here", await repo.CurrentBranchAsync());
        Assert.Empty(app.Dialogs.Confirmations);

        // Back to the detached HEAD whose commit nothing else reaches.
        await repo.DetachAsync(detached);
        await repo.GitAsync("branch", "--delete", "--force", "here");
        await app.Repository.RefreshAsync();
        await app.Operations.CreateBranchAsync(initial);

        Assert.Equal("Leave these commits?", Assert.Single(app.Dialogs.Confirmations).Title);
        Assert.Equal("old", await repo.CurrentBranchAsync());
        Assert.Equal("Created old and checked it out", app.StatusText);
        Assert.Equal(detached, Assert.Single(await repo.BackupRefsAsync()).Sha);
        app.AssertScriptFollowed();
    }

    // ---- 2.5: rename a branch ------------------------------------------------------------------

    [Fact]
    public async Task Renaming_a_branch_keeps_its_upstream_and_a_cancelled_dialog_renames_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Cancel().EnterName("feature/beta");

        await app.Operations.RenameBranchAsync("refs/heads/feature/alpha");
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await app.Operations.RenameBranchAsync("refs/heads/feature/alpha");

        var dialog = Assert.IsType<BranchNameDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal(("Rename branch", "Rename feature/alpha"), (dialog.Title, dialog.Context));
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal("Renamed feature/alpha to feature/beta", app.StatusText);
        Assert.Equal("origin/feature/alpha", (await repo.GitAsync("rev-parse", "--abbrev-ref", "feature/beta@{upstream}")).StandardOutput.Trim());
        Assert.Equal(["feature/beta", "feature/conflict", "feature/ff", "main"], app.Operations.LocalBranchNames);
        Assert.Equal(1, Ran(app, "Rename feature/alpha"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Renaming_the_current_branch_works_and_a_failure_shows_gits_message()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.EnterName("trunk").EnterName("feature/x").CloseError();

        await app.Operations.RenameBranchAsync("refs/heads/main");
        Assert.Equal("trunk", await repo.CurrentBranchAsync());
        Assert.Equal("Renamed main to trunk", app.StatusText);

        // The dialog checked the names it had; git refuses a name another process took meanwhile.
        await repo.CreateBranchAsync("feature/x");
        await app.Operations.RenameBranchAsync("refs/heads/feature/ff");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Renaming the branch failed", "Git could not rename feature/ff to feature/x."), (dialog.Title, dialog.Summary));
        Assert.Contains("already exists", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("Rename feature/ff failed", app.StatusText);
    }

    // ---- 2.6: delete a local branch ------------------------------------------------------------

    [Fact]
    public async Task Deleting_a_branch_asks_in_its_two_forms_backs_it_up_and_deletes_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CreateBranchAsync("merged", "main~1");
        using var app = await OperationsHarness.OpenAsync(repo);
        var ff = await app.ShaAsync("refs/heads/feature/ff");
        var merged = await app.ShaAsync("refs/heads/merged");
        app.Dialogs.Confirm(yes: false).Confirm().Confirm();

        await app.Operations.DeleteBranchAsync("refs/heads/feature/ff");
        Assert.Equal(ff, await repo.RefTargetAsync("refs/heads/feature/ff"));
        Assert.Empty(await repo.BackupRefsAsync());

        await app.Operations.DeleteBranchAsync("refs/heads/feature/ff");
        await app.Operations.DeleteBranchAsync("refs/heads/merged");

        var questions = app.Dialogs.Confirmations;
        Assert.All(questions, request => Assert.Equal(("Delete branch?", OperationTexts.BackupDetail, "Delete", true), (request.Title, request.Detail, request.ConfirmText, request.IsDestructive)));
        Assert.Equal("Delete the branch feature/ff? It has commits that main does not have.", questions[1].Question);
        Assert.Equal("Delete the branch merged?", questions[2].Question);
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/ff"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/merged"));
        Assert.Equal("Deleted merged", app.StatusText);
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Contains(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/branch-{BackupTime}/feature/ff$") && backup.Sha == ff);
        Assert.Contains(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/branch-{BackupTime}/merged$") && backup.Sha == merged);
        Assert.Equal(1, Ran(app, "Delete feature/ff"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_branch_head_is_on_is_not_deleted()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.DeleteBranchAsync("refs/heads/main");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deleting the branch failed", "You cannot delete the branch you are on"), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasOutput);
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/main"));
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task On_a_detached_head_the_delete_question_names_head()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm();

        await app.Operations.DeleteBranchAsync("refs/heads/feature/alpha");

        Assert.Equal("Delete the branch feature/alpha? It has commits that HEAD does not have.", Assert.Single(app.Dialogs.Confirmations).Question);
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.NotNull(await repo.RefTargetAsync("refs/tags/feature/alpha"));
        Assert.Equal("Deleted feature/alpha", app.StatusText);
    }

    // ---- 2.7: fast-forward a branch that is not checked out ------------------------------------

    [Fact]
    public async Task A_branch_behind_its_upstream_is_fast_forwarded_without_checking_it_out()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--force", "feature/alpha", "feature/alpha~1");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.FastForwardBranchAsync("refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha");

        Assert.Equal(await app.ShaAsync("refs/remotes/origin/feature/alpha"), await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("Fast-forwarded feature/alpha to origin/feature/alpha", app.StatusText);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, Ran(app, "Fast-forward feature/alpha"));
    }

    [Fact]
    public async Task A_fast_forward_git_refuses_says_why_in_its_own_sentences()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var side = await app.ShaAsync("refs/heads/side");
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");
        app.Dialogs.CloseError().CloseError();

        await app.Operations.FastForwardBranchAsync("refs/heads/side", "refs/heads/main");
        await app.Operations.FastForwardBranchAsync("refs/heads/feature/alpha", "refs/heads/loc");

        Assert.Equal(("Fast-forward failed", "side is checked out in another worktree."), (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary));
        Assert.Contains("refusing to fetch into", app.Dialogs.Errors[0].Output, StringComparison.Ordinal);
        Assert.Equal(("Fast-forward failed", "feature/alpha is not behind loc any more."), (app.Dialogs.Errors[1].Title, app.Dialogs.Errors[1].Summary));
        Assert.Contains("non-fast-forward", app.Dialogs.Errors[1].Output, StringComparison.Ordinal);
        Assert.Equal(("Fast-forward feature/alpha failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(side, await repo.RefTargetAsync("refs/heads/side"));
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/feature/alpha"));

        // A local upstream reads as the branch's name (D114).
        await app.Operations.FastForwardBranchAsync("refs/heads/loc", "refs/heads/main");
        Assert.Equal("Fast-forwarded loc to main", app.StatusText);
        Assert.Equal(await app.ShaAsync("refs/heads/main"), await repo.RefTargetAsync("refs/heads/loc"));
        app.AssertScriptFollowed();
    }

    // ---- 2.8 to 2.10: merge --------------------------------------------------------------------

    [Fact]
    public async Task A_merge_fast_forwards_when_it_can_and_says_so()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);

        Assert.Equal(await app.ShaAsync("refs/heads/feature/ff"), await repo.HeadAsync());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("Fast-forwarded main to feature/ff", app.StatusText);
        Assert.StartsWith("merge refs/heads/feature/ff: Fast-forward", (await repo.GitAsync("reflog", "-1", "--format=%gs", "main")).StandardOutput, StringComparison.Ordinal);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, Ran(app, "Merge feature/ff"));
    }

    [Fact]
    public async Task A_merge_commit_has_the_message_git_writes_for_the_short_name()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var ff = await app.ShaAsync("refs/heads/feature/ff");

        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.NoFastForward);

        Assert.Equal("Merged feature/ff into main", app.StatusText);
        Assert.Equal("Merge branch 'feature/ff'", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());
        Assert.Equal($"{mainWork} {ff}", (await repo.GitAsync("log", "-1", "--format=%P")).StandardOutput.Trim());

        await app.Operations.MergeAsync("refs/remotes/origin/feature/alpha", MergeMode.Default);

        Assert.Equal("Merged origin/feature/alpha into main", app.StatusText);
        Assert.Equal("Merge remote-tracking branch 'origin/feature/alpha'", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());
        var head = await repo.HeadAsync();

        await app.Operations.MergeAsync("refs/heads/feature/alpha", MergeMode.Default);

        Assert.Equal("main is already up to date with feature/alpha", app.StatusText);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task A_merge_runs_on_the_branch_even_when_a_tag_has_its_name()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.MergeAsync("refs/heads/feature/alpha", MergeMode.Default);

        Assert.Equal(await app.ShaAsync("refs/heads/feature/alpha"), await repo.HeadAsync());
        Assert.Equal("Fast-forwarded main to feature/alpha", app.StatusText);
    }

    [Fact]
    public async Task A_merge_stopped_by_a_conflict_is_no_failure()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();

        await app.Operations.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);

        Assert.Equal(("Merge stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        Assert.True(app.Repository.Banner.IsVisible);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(["UU src/core.txt"], await PorcelainAsync(repo));
        Assert.StartsWith("Merge branch 'feature/conflict'", File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fast_forward_only_merge_moves_the_branch_or_fails()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.FastForwardOnly);

        Assert.Equal(await app.ShaAsync("refs/heads/feature/ff"), await repo.HeadAsync());
        Assert.Equal("Fast-forwarded main to feature/ff", app.StatusText);
        Assert.Equal(1, Ran(app, "Fast-forward main"));
        var head = await repo.HeadAsync();

        await app.Operations.MergeAsync("refs/heads/feature/conflict", MergeMode.FastForwardOnly);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Fast-forward failed", "Git could not fast-forward main to feature/conflict."), (dialog.Title, dialog.Summary));
        Assert.Contains("Not possible to fast-forward", dialog.Output, StringComparison.Ordinal);
        Assert.Equal(("Fast-forward main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(InProgressKind.None, app.Operations.Status.InProgress.Kind);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_merge_cherry_pick_or_revert_during_an_operation_in_progress_is_refused_with_the_menus_reason()
    {
        using var repo = await Scenarios.StoppedAsync(Stopped.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        var alphaOne = await app.ShaAsync("refs/heads/feature/alpha~1");
        app.Dialogs.CloseError().CloseError().CloseError().CloseError();

        await app.Operations.MergeAsync("refs/heads/feature/alpha", MergeMode.Default);
        await app.Operations.CherryPickAsync(alphaOne);
        await app.Operations.RevertAsync(head);
        await app.Operations.CheckoutAndMergeAsync("refs/heads/feature/ff", "refs/heads/feature/alpha");

        Assert.Equal(
            [("Merge failed", "Finish or abort the merge first"), ("Cherry-pick failed", "Finish or abort the merge first"), ("Revert failed", "Finish or abort the merge first"), ("Merge failed", "Finish or abort the merge first")],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasOutput));
        Assert.Equal(("Check out feature/ff and merge feature/alpha failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        Assert.Equal(await app.ShaAsync("refs/heads/feature/conflict"), File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_HEAD")).Trim());
        app.AssertScriptFollowed();
    }

    // ---- 4g: check out and merge ---------------------------------------------------------------

    [Fact]
    public async Task Check_out_and_merge_checks_out_the_target_then_merges_into_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");
        var ff = await app.ShaAsync("refs/heads/feature/ff");

        await app.Operations.CheckoutAndMergeAsync("refs/heads/feature/alpha", "refs/heads/feature/ff");

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal("Merged feature/ff into feature/alpha", app.StatusText);
        Assert.Equal($"{alpha} {ff}", (await repo.GitAsync("log", "-1", "--format=%P")).StandardOutput.Trim());
        Assert.StartsWith("Merge branch 'feature/ff'", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(1, Ran(app, "Check out feature/alpha and merge feature/ff"));
        Assert.Empty(app.Dialogs.Shown);

        await app.Operations.CheckoutAndMergeAsync("refs/heads/feature/ff", "refs/heads/feature/conflict");

        Assert.Equal("feature/ff", await repo.CurrentBranchAsync());
        Assert.Equal(("Merge stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Check_out_and_merge_stops_at_a_blocked_checkout_without_offering_the_stash()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAndMergeAsync("refs/heads/feature/other", "refs/heads/feature/clash");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "Your changes to these files would be overwritten by checking out feature/other."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        Assert.Equal("Check out feature/other and merge feature/clash failed", app.StatusText);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await repo.StashListAsync());
        app.AssertScriptFollowed();
    }

    // ---- 2.11: reset ---------------------------------------------------------------------------

    [Fact]
    public async Task A_soft_or_mixed_reset_asks_through_its_dialog_and_backs_up_head()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var addCore = await app.ShaAsync("main~1");
        app.Dialogs.Cancel().Reset(ResetMode.Soft).Reset(ResetMode.Mixed);

        await app.Operations.ResetAsync(addCore);
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await app.Operations.ResetAsync(addCore);

        var dialog = Assert.IsType<ResetDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal(($"Reset main to {addCore[..7]}?", $"Move main to \"Add core\" ({addCore[..7]})."), (dialog.Title, dialog.Text));
        Assert.Equal(addCore, await repo.HeadAsync());
        Assert.Equal($"Reset main to {addCore[..7]} (soft)", app.StatusText);
        Assert.Equal(["M  src/core.txt"], await PorcelainAsync(repo));
        var soft = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/reset-{BackupTime}$", soft.Name);
        Assert.Equal(mainWork, soft.Sha);

        await repo.GitAsync("reset", "--hard", mainWork);
        await app.Repository.RefreshAsync();
        await app.Operations.ResetAsync(addCore);

        Assert.Equal($"Reset main to {addCore[..7]} (mixed)", app.StatusText);
        Assert.Equal([" M src/core.txt"], await PorcelainAsync(repo));
        Assert.Equal(2, (await repo.BackupRefsAsync()).Count);
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Equal(2, Ran(app, "Reset main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_hard_reset_saves_the_changes_it_discards_with_the_index_as_second_parent()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("README.md", "# Operations\n\nLocal edit.\n").WriteFile("scratch.txt", "scratch\n").WriteFile("todo.txt", "todo\n");
        await repo.GitAsync("add", "scratch.txt");
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var addCore = await app.ShaAsync("main~1");
        app.Dialogs.Reset(ResetMode.Hard);

        await app.Operations.ResetAsync(addCore);

        Assert.Equal(addCore, await repo.HeadAsync());
        Assert.Equal($"Reset main to {addCore[..7]} (hard)", app.StatusText);
        Assert.Equal(["?? todo.txt"], await PorcelainAsync(repo));
        Assert.Equal("# Operations\n", Read(repo, "README.md"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")));
        Assert.Equal("todo\n", Read(repo, "todo.txt"));

        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        var head = Assert.Single(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/reset-{BackupTime}$"));
        var changes = Assert.Single(backups, backup => backup.Name == head.Name + "-changes");
        Assert.Equal(mainWork, head.Sha);
        var parents = (await repo.GitAsync("log", "-1", "--format=%P", changes.Sha)).StandardOutput.Trim().Split(' ');
        Assert.Equal(2, parents.Length);
        Assert.Equal(mainWork, parents[0]);
        Assert.Equal("# Operations\n\nLocal edit.\n", await ShowAsync(repo, changes.Sha, "README.md"));
        Assert.Equal("scratch\n", await ShowAsync(repo, changes.Sha, "scratch.txt"));
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", $"{changes.Sha}:todo.txt")).Succeeded);
        Assert.Equal("# Operations\n", await ShowAsync(repo, parents[1], "README.md"));
        Assert.Equal("scratch\n", await ShowAsync(repo, parents[1], "scratch.txt"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_mixed_reset_saves_the_index_it_replaces_when_something_is_staged()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\nunstaged line\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var addCore = await app.ShaAsync("main~1");
        app.Dialogs.Reset(ResetMode.Mixed);

        await app.Operations.ResetAsync(addCore);

        // The staged version is in neither the working tree nor the new index; the -changes ref
        // keeps it, as a commit of the old index on HEAD.
        Assert.Equal($"Reset main to {addCore[..7]} (mixed)", app.StatusText);
        Assert.Equal([" M src/core.txt"], await PorcelainAsync(repo));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        var head = Assert.Single(backups, backup => Regex.IsMatch(backup.Name, $"^refs/visualcommit/backup/reset-{BackupTime}$"));
        var changes = Assert.Single(backups, backup => backup.Name == head.Name + "-changes");
        Assert.Equal(mainWork, head.Sha);
        Assert.Equal(mainWork, (await repo.GitAsync("log", "-1", "--format=%P", changes.Sha)).StandardOutput.Trim());
        Assert.Equal("core\nmain work\nstaged line\n", await ShowAsync(repo, changes.Sha, "src/core.txt"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task On_a_detached_head_the_reset_moves_head()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var detached = await repo.HeadAsync();
        var mainWork = await app.ShaAsync("refs/heads/main");
        app.Dialogs.Reset(ResetMode.Mixed);

        await app.Operations.ResetAsync(mainWork);

        Assert.Equal($"Reset HEAD to {mainWork[..7]}?", app.Dialogs.Shown.Single().Title);
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Equal($"Reset HEAD to {mainWork[..7]} (mixed)", app.StatusText);
        Assert.Equal(detached, Assert.Single(await repo.BackupRefsAsync()).Sha);
        Assert.Equal(1, Ran(app, "Reset HEAD"));
        app.AssertScriptFollowed();
    }

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

    /// <summary>
    /// The error dialog shows the output and the command of the operation's failed git call
    /// <paramref name="command"/>, as the call log recorded it and the queue cleans it (1.6): on
    /// every git version, whatever its words.
    /// </summary>
    private static void AssertOutputOfFailedCall(OperationsHarness app, ErrorDialogViewModel dialog, string command)
    {
        var call = app.Calls.Snapshot().Last(record => record.CommandText == command);
        Assert.False(call.Succeeded);
        Assert.NotEqual(string.Empty, dialog.Output);
        Assert.Equal(GitOutputCleaner.Clean(call.StandardError, call.StandardOutput), dialog.Output);
        Assert.Equal(command, dialog.Command);
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

    // ---- A closed tab --------------------------------------------------------------------------

    [Fact]
    public async Task After_the_tab_closed_no_branch_or_history_operation_runs_or_shows_anything()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        var refs = (await repo.GitAsync("for-each-ref")).StandardOutput;
        var addCore = await app.ShaAsync("main~1");
        app.Repository.Dispose();

        await app.Operations.CheckoutAsync("refs/heads/feature/ff");
        await app.Operations.StashAndCheckoutAsync("refs/heads/feature/ff");
        await app.Operations.CreateBranchAsync(null);
        await app.Operations.RenameBranchAsync("refs/heads/feature/ff");
        await app.Operations.DeleteBranchAsync("refs/heads/feature/ff");
        await app.Operations.FastForwardBranchAsync("refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha");
        await app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);
        await app.Operations.CheckoutAndMergeAsync("refs/heads/feature/alpha", "refs/heads/feature/ff");
        await app.Operations.ResetAsync(addCore);
        await app.Operations.CherryPickAsync(addCore);
        await app.Operations.CherryPickSeveralAsync([addCore, head]);
        await app.Operations.RevertAsync(head);
        await app.Operations.RevertSeveralAsync([addCore, head]);
        await app.Operations.AbortAsync(new InProgressState(InProgressKind.Merge, null, null, null, null, 0, false));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(refs, (await repo.GitAsync("for-each-ref")).StandardOutput);
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains(" after ", StringComparison.Ordinal) && message.Contains("Succeeded", StringComparison.Ordinal));
    }

    // ---- In the window (the headless driver): the dialogs and results as the checks show them ----
    // Until the menus are built the operations are started by their methods, as a menu item would.
    // With VISUALCOMMIT_TEST_PICTURES set, each step is saved as a picture to look at (TestPictures).

    [AvaloniaFact]
    public async Task In_the_window_a_blocked_checkout_its_stash_and_its_conflicts_show_as_checks_14_and_14b_say()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var operations = (await GraphShownAsync(app)).Operations;

        var run = operations.CheckoutAsync("refs/heads/feature/addfile");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal("Checkout blocked", TextOf(app, "ErrorTitle"));
        Assert.Equal("Untracked files are in the way: feature/addfile has files with the same names. Move or delete them, then check out again.", TextOf(app, "ErrorSummary"));
        Assert.Equal("In blocked", TextOf(app, "ErrorRepository"));
        Assert.Contains("newfile.txt", TextOf(app, "ErrorOutput"), StringComparison.Ordinal);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        Assert.Equal("Check out feature/addfile failed", TextOf(app, "OperationStatus"));
        TestPictures.Save(app, "wp7-14a-untracked-in-the-way");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await run;

        run = operations.CheckoutAsync("refs/heads/feature/other");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal("Your changes to these files would be overwritten by checking out feature/other.", TextOf(app, "ErrorSummary"));
        Assert.Equal("Stash changes and check out", ((Button)app.FindByAutomationId("ErrorExtraButton")).Content);
        Assert.True(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        TestPictures.Save(app, "wp7-14b-tracked-changes-in-the-way");
        app.Click(app.FindByAutomationId("ErrorExtraButton"));
        await run;
        await app.WaitForIdleAsync();
        Assert.Null(app.TopDialogView);
        Assert.Equal("Checked out feature/other and re-applied your changes", TextOf(app, "OperationStatus"));
        Assert.Equal("feature/other", TextOf(app, "CurrentBranch"));
        TestPictures.Save(app, "wp7-14c-stashed-and-checked-out");

        run = operations.CheckoutAsync("refs/heads/feature/clash");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        app.Click(app.FindByAutomationId("ErrorExtraButton"));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is ErrorDialogViewModel { Title: "Changes re-applied with conflicts" }, "the information dialog");
        Assert.Equal(
            "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file. Your changes are kept in the stash \"VisualCommit: before checking out feature/clash\".",
            TextOf(app, "ErrorSummary"));
        Assert.False(app.FindByAutomationId("ErrorOutput").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ErrorCopyButton").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        TestPictures.Save(app, "wp7-14d-reapplied-with-conflicts");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await run;
        await app.WaitForIdleAsync();
        Assert.Equal("Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.", TextOf(app, "OperationStatus"));
        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", TextOf(app, "BannerText"));
        Assert.False(app.FindByAutomationId("BannerAbortButton").IsEffectivelyVisible);
        TestPictures.Save(app, "wp7-14e-stash-conflict-banner");

        run = operations.CheckoutAsync("refs/heads/main");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Checkout failed", "Git could not check out main."), (TextOf(app, "ErrorTitle"), TextOf(app, "ErrorSummary")));
        Assert.StartsWith("error: you need to resolve your current index first", TextOf(app, "ErrorOutput"), StringComparison.Ordinal);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        Assert.Equal("Check out main failed", TextOf(app, "OperationStatus"));
        TestPictures.Save(app, "wp7-14f-checkout-failed");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await run;
    }

    [AvaloniaFact]
    public async Task In_the_window_merge_abort_reset_create_delete_and_an_empty_pick_show_as_the_checks_say()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var operations = repository.Operations;
        var addCore = (await repo.GitAsync("rev-parse", "main~1")).StandardOutput.Trim();
        var alphaOne = (await repo.GitAsync("rev-parse", "feature/alpha~1")).StandardOutput.Trim();

        // Check 9: the merge stops, the banner takes over; its Abort asks and backs up.
        await operations.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);
        await app.WaitForIdleAsync();
        Assert.Equal("Merge stopped: 1 file has conflicts", TextOf(app, "OperationStatus"));
        Assert.Equal("Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.", TextOf(app, "BannerText"));
        Assert.Null(app.TopDialogView);
        TestPictures.Save(app, "wp7-09a-merge-stopped");
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(("Abort the merge?", "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost."),
            (TextOf(app, "ConfirmTitle"), TextOf(app, "ConfirmQuestion")));
        TestPictures.Save(app, "wp7-09b-abort-question");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => app.TopDialogView is null && repository.OperationStatus.Text == "Aborted the merge", "the abort");
        await app.WaitForIdleAsync();
        Assert.False(app.FindByAutomationId("ConflictBanner").IsEffectivelyVisible);
        Assert.Equal(2, (await repo.BackupRefsAsync()).Count);
        TestPictures.Save(app, "wp7-09c-aborted");

        // Check 10: the reset dialog is the confirmation; Hard turns its button to danger.
        var reset = operations.ResetAsync(addCore);
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        Assert.Equal(($"Reset main to {addCore[..7]}?", $"Move main to \"Add core\" ({addCore[..7]})."), (TextOf(app, "ResetTitle"), TextOf(app, "ResetText")));
        app.Click(app.FindByAutomationId("ResetHard"));
        Assert.Equal(ResetMode.Hard, app.Dialog<ResetDialogViewModel>().Mode);
        TestPictures.Save(app, "wp7-10a-reset-hard");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await reset;
        await app.WaitForIdleAsync();
        Assert.Equal($"Reset main to {addCore[..7]} (hard)", TextOf(app, "OperationStatus"));
        Assert.Equal(addCore, await repo.HeadAsync());
        TestPictures.Save(app, "wp7-10b-reset-done");

        // Check 13: the toolbar's Branch opens the name dialog at HEAD; Delete asks in its long form.
        app.Click(app.FindByAutomationId("BranchButton"));
        await app.WaitForDialogAsync<BranchNameDialogViewModel>();
        Assert.Equal($"From {addCore[..7]} \"Add core\"", TextOf(app, "NameDialogContext"));
        app.TypeInto("NameBox", "hotfix");
        TestPictures.Save(app, "wp7-13a-create-branch");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => app.TopDialogView is null && repository.OperationStatus.Text == "Created hotfix and checked it out", "the new branch");
        await app.WaitForIdleAsync();
        Assert.Equal("hotfix", TextOf(app, "CurrentBranch"));
        TestPictures.Save(app, "wp7-13b-created");

        var delete = operations.DeleteBranchAsync("refs/heads/feature/ff");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(("Delete branch?", "Delete the branch feature/ff? It has commits that hotfix does not have."), (TextOf(app, "ConfirmTitle"), TextOf(app, "ConfirmQuestion")));
        TestPictures.Save(app, "wp7-13c-delete-question");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await delete;
        await app.WaitForIdleAsync();
        Assert.Equal("Deleted feature/ff", TextOf(app, "OperationStatus"));

        // Check 25 step 7: a pick that changes nothing stops; the banner offers Abort.
        await operations.CherryPickAsync(alphaOne);
        await app.WaitForIdleAsync();
        Assert.Equal($"Cherry-picked {alphaOne[..7]} onto hotfix", TextOf(app, "OperationStatus"));
        var pick = operations.CherryPickAsync(alphaOne);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Cherry-pick stopped", $"{alphaOne[..7]} changes nothing on hotfix: its changes are already there. Abort the cherry-pick to go on."),
            (TextOf(app, "ErrorTitle"), TextOf(app, "ErrorSummary")));
        Assert.Equal($"Cherry-pick {alphaOne[..7]} failed", TextOf(app, "OperationStatus"));

        // Git's output and the command, which the operation reads from the session's call log (2.12, 1.6).
        Assert.True(app.FindByAutomationId("ErrorOutput").IsEffectivelyVisible);
        Assert.StartsWith("The previous cherry-pick is now empty", TextOf(app, "ErrorOutput"), StringComparison.Ordinal);
        Assert.Equal($"git cherry-pick {alphaOne}", TextOf(app, "ErrorCommand"));
        TestPictures.Save(app, "wp7-25a-empty-pick");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await pick;
        await app.WaitForIdleAsync();
        Assert.Equal($"Cherry-picking {alphaOne[..7]} \"Alpha one\" changes nothing: its changes are already here. Abort to go on.", TextOf(app, "BannerText"));
        TestPictures.Save(app, "wp7-25b-empty-pick-banner");
    }

    [AvaloniaFact]
    public async Task In_the_window_leaving_a_detached_head_asks_as_check_32_says()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var operations = (await GraphShownAsync(app)).Operations;

        var checkout = operations.CheckoutAsync("refs/heads/main");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(
            ("Leave these commits?", "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph.", "A backup is saved first, so this can be undone.", "Check out"),
            (TextOf(app, "ConfirmTitle"), TextOf(app, "ConfirmQuestion"), TextOf(app, "ConfirmDetail"), ((Button)app.FindByAutomationId("ConfirmButton")).Content as string));
        TestPictures.Save(app, "wp7-32a-leave-these-commits");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await checkout;
        await app.WaitForIdleAsync();
        Assert.Equal(("Checked out main", "main"), (TextOf(app, "OperationStatus"), TextOf(app, "CurrentBranch")));
        Assert.Single(await repo.BackupRefsAsync());
        TestPictures.Save(app, "wp7-32b-checked-out-main");
    }

    // ---- Helpers -------------------------------------------------------------------------------

    /// <summary>Waits until the active tab's graph has loaded and its queue is idle.</summary>
    private static async Task<RepositoryViewModel> GraphShownAsync(ShellDriver app)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync(repository);
        return repository;
    }

    /// <summary>The text a text block or a text box with this automation id shows (the dialog on top first, then the window).</summary>
    private static string TextOf(ShellDriver app, string automationId) => app.FindByAutomationId(automationId) switch
    {
        TextBlock block => block.Text ?? string.Empty,
        TextBox box => box.Text ?? string.Empty,
        var other => throw new InvalidOperationException($"'{automationId}' is a {other.GetType().Name}."),
    };

    /// <summary>How many times the queue ran the operation <paramref name="title"/> to its end (its log line, 1.7).</summary>
    private static int Ran(OperationsHarness app, string title) =>
        app.Log.Messages.Count(message => message.Contains($": {title}: ", StringComparison.Ordinal) && message.Contains(" after ", StringComparison.Ordinal));

    private static async Task<IReadOnlyList<string>> PorcelainAsync(TempRepo repo) =>
        (await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToList();

    private static string Read(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    private static async Task<string> ShowAsync(TempRepo repo, string commit, string path) =>
        (await repo.GitAsync("show", $"{commit}:{path}")).StandardOutput;
}
