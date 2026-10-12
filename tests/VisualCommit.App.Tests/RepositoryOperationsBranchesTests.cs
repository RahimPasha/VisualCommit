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
/// WP7-branches: the branch operations of <see cref="RepositoryOperations"/>
/// (docs/phase-3-design.md 2.4 to 2.7: create, rename, delete, fast-forward a branch that is not
/// checked out), and that nothing of the branch and history operations runs after the tab closed,
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
public class RepositoryOperationsBranchesTests
{
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
}
