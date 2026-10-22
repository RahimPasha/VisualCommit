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
/// WP7-branches: the checkouts of <see cref="RepositoryOperations"/> (docs/phase-3-design.md 2.1
/// to 2.3: a local branch, a remote branch, a commit or a tag, with the stash a blocked checkout
/// offers and the question a detached HEAD asks), also in the window (checks 14, 14b and 32),
/// against real git and the scenario repos, through <see cref="OperationsHarness"/>: each
/// operation's confirmation, the backup refs it saves and what they hold, what git holds after it,
/// the status bar's text and colour, the error dialog with git's output and its extra button,
/// cancel, and that each ran once. WP7-branches' tests are four classes in one xunit collection,
/// which runs beside those of the other sub-packages (<see cref="RepositoryOperationsTestSupport"/>
/// says why): <see cref="RepositoryOperationsCheckoutTests"/>, <see cref="RepositoryOperationsBranchesTests"/>,
/// <see cref="RepositoryOperationsMergeTests"/> and <see cref="RepositoryOperationsHistoryTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
[Collection(RepositoryOperationsTestSupport.BranchesCollection)]
public class RepositoryOperationsCheckoutTests
{
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
        Assert.Equal(("Checkout blocked", "1 conflict prevents checkout. Commit, stash, or discard your changes first.", "Stash changes and check out"),
            (dialog.Title, dialog.Summary, dialog.ExtraAction));
        Assert.Equal("In blocked", dialog.RepositoryText);
        Assert.Equal("git switch --no-guess --no-overwrite-ignore feature/other", dialog.Command);
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

        // C12 (design 9.3): one path under each of git's two headers.
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "2 conflicts prevent checkout. Commit, stash, or discard your changes first."),
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
    public async Task A_checkout_during_a_merge_is_refused_before_git_with_the_menus_reason()
    {
        // C12 (design 9.3): git would refuse with "cannot switch branch while merging"; the app
        // refuses first, as "Checkout blocked" with no git output (the report's PD5, phase 3's check 9).
        using var repo = await Scenarios.StoppedAsync(Stopped.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var calls = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync("refs/heads/feature/alpha");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "Finish or abort the merge first"), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasOutput);
        Assert.False(dialog.HasCommand);
        Assert.False(dialog.HasExtraAction);
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git switch", StringComparison.Ordinal));
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
        Assert.Equal("git switch --no-guess --no-overwrite-ignore nowhere", dialog.Command);
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
        Assert.Equal("2 conflicts prevent checkout. Commit, stash, or discard your changes first.", TextOf(app, "ErrorSummary"));
        Assert.Equal("In blocked", TextOf(app, "ErrorRepository"));
        Assert.Contains("newfile.txt", TextOf(app, "ErrorOutput"), StringComparison.Ordinal);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        Assert.Equal("Check out feature/addfile failed", TextOf(app, "OperationStatus"));
        TestPictures.Save(app, "wp7-14a-untracked-in-the-way");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await run;

        run = operations.CheckoutAsync("refs/heads/feature/other");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal("1 conflict prevents checkout. Commit, stash, or discard your changes first.", TextOf(app, "ErrorSummary"));
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
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is ErrorDialogViewModel { Title: "Changes re-applied with conflicts" }, "the information dialog", OperationChainTimeout);
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

        // C12 (design 9.3): the conflicted file refuses the checkout before git, with no output.
        run = operations.CheckoutAsync("refs/heads/main");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Checkout blocked", "Resolve the conflicts first"), (TextOf(app, "ErrorTitle"), TextOf(app, "ErrorSummary")));
        Assert.False(app.FindByAutomationId("ErrorOutput").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ErrorCopyButton").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsEffectivelyVisible);
        Assert.Equal("Check out main failed", TextOf(app, "OperationStatus"));
        TestPictures.Save(app, "wp7-14f-checkout-failed");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await run;
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
}
