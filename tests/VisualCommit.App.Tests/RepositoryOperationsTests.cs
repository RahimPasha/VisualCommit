using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="RepositoryOperations"/> against real scenario repositories, through
/// <see cref="OperationsHarness"/>. This part tests the helpers every operation shares
/// (<c>RepositoryOperations.Run.cs</c>: a success, a failure in the error dialog with 2.33's texts
/// or a dialog of its own and an extra button, a conflict, a cancel and its lock files, a closed
/// tab, an <see cref="IOException"/>, confirmations with a backup, names, lookups) and that the
/// toolbar and the Remotes dialog reach the operations of the same meaning. The WP7
/// sub-packages' tests are classes of their own, by subject, in one xunit collection per
/// sub-package (<see cref="RepositoryOperationsTestSupport"/> says why):
/// <see cref="RepositoryOperationsCheckoutTests"/>, <see cref="RepositoryOperationsBranchesTests"/>,
/// <see cref="RepositoryOperationsMergeTests"/> and <see cref="RepositoryOperationsHistoryTests"/>
/// (WP7-branches); <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>
/// (WP7-remotes); <see cref="RepositoryOperationsStashTagsTests"/> and
/// <see cref="RepositoryOperationsFilesTests"/> (WP7-stashtags); and
/// <see cref="RepositoryOperationsQueueTests"/> for operations that wait in the queue. The helpers
/// they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
[Collection(RepositoryOperationsTestSupport.SharedCollection)]
public class RepositoryOperationsTests
{
    [Fact]
    public async Task A_successful_operation_leaves_its_message_in_the_status_bar_and_reads_the_refs_again()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.CreateBranch("topic");

        var result = await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await app.Operations.Git.CreateBranchAsync("topic", "refs/heads/main", checkout: false);
            return OperationReport.Done(OperationTexts.CreatedBranch("topic", checkedOut: false));
        });

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal("Created topic", app.StatusText);
        Assert.Equal(OperationStatusTone.Secondary, app.StatusTone);
        Assert.NotNull(app.Operations.FindRef("refs/heads/topic"));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Contains(app.Log.Messages, message => message.Contains(": Create branch topic: Succeeded after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failure_opens_the_error_dialog_with_the_tables_title_and_summary_and_gits_output()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.CheckOut("nowhere");
        app.Dialogs.CloseError();

        var result = await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await app.Operations.Git.CheckoutBranchAsync("nowhere");
            return OperationReport.Done(OperationTexts.CheckedOut("nowhere"));
        });

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal("Check out nowhere failed", app.StatusText);
        Assert.Equal(OperationStatusTone.Danger, app.StatusTone);
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Checkout failed", dialog.Title);
        Assert.Equal("Git could not check out nowhere.", dialog.Summary);
        Assert.Equal($"In {app.Repository.Name}", dialog.RepositoryText);
        Assert.Contains("nowhere", dialog.Output, StringComparison.Ordinal);
        Assert.StartsWith("git ", dialog.Command, StringComparison.Ordinal);
        Assert.False(dialog.HasExtraAction);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_blocked_by_tracked_changes_offers_its_extra_button_which_starts_its_action()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var clicked = new List<string>();
        var text = OperationTexts.CheckOut("feature/other");
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await app.Operations.Git.CheckoutBranchAsync("feature/other");
                return OperationReport.Done(OperationTexts.CheckedOut("feature/other"));
            },
            new RunOptions
            {
                DescribeFailure = failure => app.Operations.CheckoutFailureDisplay(failure, "refs/heads/feature/other", offerStash: true) is { } display
                    ? display with { Extra = new ErrorAction(display.Extra!.Label, () => { clicked.Add(display.Extra.Label); return Task.CompletedTask; }) }
                    : null,
            });

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Checkout blocked", dialog.Title);
        Assert.Equal("Your changes to these files would be overwritten by checking out feature/other.", dialog.Summary);
        Assert.Equal("Stash changes and check out", dialog.ExtraAction);
        Assert.Contains("Your local changes to the following files would be overwritten by checkout", dialog.Output, StringComparison.Ordinal);
        Assert.Equal(["Stash changes and check out"], clicked);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_blocked_by_untracked_files_says_so_without_an_extra_button()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.RunAsync(
            OperationTexts.CheckOut("feature/addfile"),
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await app.Operations.Git.CheckoutBranchAsync("feature/addfile");
                return OperationReport.Done(OperationTexts.CheckedOut("feature/addfile"));
            },
            new RunOptions { DescribeFailure = failure => app.Operations.CheckoutFailureDisplay(failure, "refs/heads/feature/addfile", offerStash: true) });

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", OperationTexts.CheckoutBlockedByUntrackedFiles("feature/addfile")), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        Assert.Equal("Check out feature/addfile failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public void The_extra_buttons_are_named_as_D95_names_them()
    {
        using var repository = Detached();
        var operations = repository.Operations;
        static GitFailure Fail(GitFailureKind kind) => new(kind, "x failed.", "out", "git x", 1);

        var blocked = operations.CheckoutFailureDisplay(Fail(GitFailureKind.CheckoutBlockedByLocalChanges), "refs/remotes/origin/feature/x", offerStash: true)!;
        Assert.Equal(("Checkout blocked", "Your changes to these files would be overwritten by checking out origin/feature/x.", "Stash changes and check out"),
            (blocked.Title, blocked.Summary, blocked.Extra!.Label));
        Assert.Null(operations.CheckoutFailureDisplay(Fail(GitFailureKind.CheckoutBlockedByLocalChanges), "refs/heads/b", offerStash: false)!.Extra);
        Assert.Null(operations.CheckoutFailureDisplay(Fail(GitFailureKind.Other), "refs/heads/b", offerStash: true));

        var rejected = operations.PushFailureDisplay(Fail(GitFailureKind.PushRejected), "main", "origin", currentToUpstream: true)!;
        Assert.Equal(("Push rejected", "origin has commits that your main does not have. Pull first, then push again.", "Pull"),
            (rejected.Title, rejected.Summary, rejected.Extra!.Label));
        Assert.Equal("Fetch origin", operations.PushFailureDisplay(Fail(GitFailureKind.PushRejected), "topic", "origin", currentToUpstream: false)!.Extra!.Label);
        Assert.Null(operations.PushFailureDisplay(Fail(GitFailureKind.PushRejectedStale), "main", "origin", currentToUpstream: true));
        Assert.Equal("Remove lock files", OperationTexts.RemoveLockFilesButton);
    }

    [Fact]
    public async Task An_operation_that_reports_its_own_failure_keeps_its_summary_and_may_choose_its_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.CherryPick(await app.ShaAsync("refs/heads/feature/ff"));
        var own = new GitFailure(GitFailureKind.Other, "It changes nothing.", "nothing to commit", "git cherry-pick 1234567", 1);
        app.Dialogs.CloseError().CloseError();

        await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Status, _ => Task.FromResult(RepositoryOperations.Failed(text, own)));
        FailureDisplay? chosen = null;
        await app.Operations.RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Status,
            _ =>
            {
                chosen = new FailureDisplay(OperationTexts.CherryPickStoppedTitle, own.Summary);
                return Task.FromResult(RepositoryOperations.Failed(text, own));
            },
            new RunOptions { DescribeFailure = _ => chosen });

        Assert.Equal(("Cherry-pick failed", "It changes nothing."), (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary));
        Assert.Equal(("Cherry-pick stopped", "It changes nothing.", "nothing to commit"), (app.Dialogs.Errors[1].Title, app.Dialogs.Errors[1].Summary, app.Dialogs.Errors[1].Output));
        Assert.Equal($"{text.Title} failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_conflict_is_no_failure_its_message_stays_in_the_warning_colour_without_a_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        var result = await app.Operations.RunAsync(OperationTexts.Merge("feature/conflict", "main"), OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            var merge = await app.Operations.Git.MergeAsync("refs/heads/feature/conflict", MergeMode.Default);
            Assert.Equal(MergeResult.Conflicts, merge);
            return OperationReport.Conflicts(OperationTexts.MergeStopped(await app.Operations.CountConflictedFilesAsync()));
        });

        Assert.Equal(OperationOutcome.Conflicts, result.Outcome);
        Assert.Equal("Merge stopped: 1 file has conflicts", app.StatusText);
        Assert.Equal(OperationStatusTone.Warning, app.StatusTone);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
    }

    [Fact]
    public async Task A_cancelled_operation_shows_no_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        var result = await app.Operations.RunAsync(OperationTexts.Fetch("origin"), OperationKind.Network, RefreshScope.Refs, _ => throw new OperationCanceledException());

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal("Fetch origin cancelled", app.StatusText);
        Assert.Equal(OperationStatusTone.Secondary, app.StatusTone);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task A_cancelled_network_operation_that_left_lock_files_lists_them_and_removes_them_on_request()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var lockFile = Path.Combine(app.Operations.Git.GitDirectory, "refs", "remotes", "origin", "main.lock");
        app.Dialogs.CloseError(clickExtra: true);

        var result = await app.Operations.RunAsync(OperationTexts.Fetch("origin"), OperationKind.Network, RefreshScope.Refs, context =>
        {
            // The git call that was stopped, as the session's call log records it for the operation.
            File.WriteAllText(lockFile, "left by a stopped git");
            app.Calls.Add(new GitCallRecord(DateTimeOffset.UtcNow, TimeSpan.Zero, "git fetch --progress origin", repo.Path, GitCallOutcome.Cancelled, null, string.Empty, string.Empty, context.OperationId));
            throw new OperationCanceledException();
        });

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Fetch origin cancelled", dialog.Title);
        Assert.Equal(OperationTexts.StaleLockFilesSummary, dialog.Summary);
        Assert.Equal(".git/refs/remotes/origin/main.lock", dialog.Output);

        // 4f: ErrorCommand is the cancelled git call (check 17b).
        Assert.Equal("git fetch --progress origin", dialog.Command);
        Assert.Equal("Remove lock files", dialog.ExtraAction);
        Assert.False(File.Exists(lockFile));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_lock_file_that_cannot_be_removed_opens_a_dialog_with_the_systems_message()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var lockFile = Path.Combine(repo.Path, ".git", "index.lock");
        File.WriteAllText(lockFile, "held");
        app.Dialogs.CloseError();

        using (new FileStream(lockFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            if (OperatingSystem.IsWindows())
            {
                await app.Operations.RemoveLockFilesAsync([lockFile, Path.Combine(repo.Path, ".git", "gone.lock")]);
            }
            else
            {
                // Only Windows refuses to delete an open file; elsewhere a folder without write
                // permission does, which root ignores, so this part is Windows-only.
                return;
            }
        }

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal((OperationTexts.RemoveLockFilesFailedTitle, OperationTexts.RemoveLockFilesFailedSummary), (dialog.Title, dialog.Summary));
        Assert.False(string.IsNullOrWhiteSpace(dialog.Output));
        Assert.True(File.Exists(lockFile));
    }

    [Fact]
    public async Task An_IOException_or_an_unexpected_exception_is_a_failure_in_the_error_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.Ignore(OperationTexts.IgnoreFileLabel("run.log"), "/logs/run.log");
        app.Dialogs.CloseError().CloseError();

        var io = await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Status, _ => throw new IOException("The disk is full."));
        var unexpected = await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Status, _ => throw new InvalidOperationException("Something broke."));

        Assert.Equal((OperationOutcome.Failed, OperationOutcome.Failed), (io.Outcome, unexpected.Outcome));
        Assert.Equal(("Ignoring the file failed", "The file .gitignore could not be written.", "The disk is full."),
            (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary, app.Dialogs.Errors[0].Output));
        Assert.Equal("Unexpected error: Something broke.", app.Dialogs.Errors[1].Output);
        Assert.Equal("Ignore run.log failed", app.StatusText);
        Assert.Contains(app.Log.Entries, entry => entry.Level == Core.Logging.LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Outside_the_queue_a_guarded_operation_shows_its_failure_and_never_throws()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.CheckOut("feature/alpha");
        app.Dialogs.CloseError().CloseError();

        await app.Operations.GuardAsync(text, () => throw new GitException("git rev-list --count HEAD", 128, "fatal: bad revision 'HEAD'\n"));
        var count = await app.Operations.GuardAsync(text, () => Task.FromException<int>(new IOException("Access is denied.")), fallback: -1);
        await app.Operations.GuardAsync(text, () => throw new OperationCanceledException());

        Assert.Equal(-1, count);
        Assert.Equal(2, app.Dialogs.Errors.Count);
        Assert.Equal(("Checkout failed", "Git could not check out feature/alpha.", "fatal: bad revision 'HEAD'", "git rev-list --count HEAD"),
            (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary, app.Dialogs.Errors[0].Output, app.Dialogs.Errors[0].Command));
        Assert.Equal("Access is denied.", app.Dialogs.Errors[1].Output);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task After_the_tab_closed_nothing_starts_and_nothing_is_shown()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var text = OperationTexts.DeleteBranch("feature/ff");
        app.Repository.Dispose();
        var ran = false;

        var result = await app.Operations.RunAsync(text, OperationKind.Local, RefreshScope.Refs, _ =>
        {
            ran = true;
            return Task.FromResult(OperationReport.Done("Deleted feature/ff"));
        });

        Assert.True(app.Operations.IsClosed);
        Assert.False(ran);
        Assert.Equal((OperationOutcome.Cancelled, "Delete feature/ff cancelled"), (result.Outcome, result.Message));
        Assert.False(await app.Operations.ConfirmAsync(OperationTexts.ConfirmDeleteBranch("feature/ff", null)));
        Assert.Null(await app.Operations.ShowDialogAsync(app.Operations.NewBranchDialog("From main")));
        await app.Operations.GuardAsync(text, () => throw new IOException("Too late."));
        Assert.Empty(app.Dialogs.Shown);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/feature/ff"));
    }

    [Fact]
    public async Task An_operation_that_fails_after_its_tab_closed_shows_no_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        var result = await app.Operations.RunAsync(OperationTexts.CheckOut("nowhere"), OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            // The tab closes while the local operation runs: it runs to its end (1.8).
            app.Repository.Dispose();
            await app.Operations.Git.CheckoutBranchAsync("nowhere");
            return OperationReport.Done("Checked out nowhere");
        });

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Contains(app.Log.Messages, message => message.Contains("Check out nowhere failed after the tab closed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_declined_confirmation_does_nothing_and_a_confirmed_one_saves_its_backup_before_it_runs()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var tip = await app.ShaAsync("refs/heads/feature/ff");
        var text = OperationTexts.DeleteBranch("feature/ff");
        var confirmation = OperationTexts.ConfirmDeleteBranch("feature/ff", null);
        BackupResult? seen = null;
        app.Dialogs.Confirm(yes: false).Confirm();

        Task<OperationResult?> Delete() => app.Operations.ConfirmAndRunAsync(
            confirmation,
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            _ => Task.FromResult<BackupRequest?>(new BackupRequest("branch", [new BackupEntry("feature/ff", "refs/heads/feature/ff")])),
            async (_, backup) =>
            {
                seen = backup;
                await app.Operations.Git.DeleteBranchAsync("feature/ff");
                return OperationReport.Done(OperationTexts.DeletedBranch("feature/ff"));
            });

        Assert.Null(await Delete());
        Assert.Equal(tip, await repo.RefTargetAsync("refs/heads/feature/ff"));
        Assert.Empty(await repo.BackupRefsAsync());

        var result = await Delete();

        Assert.Equal(OperationOutcome.Succeeded, result!.Outcome);
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/ff"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/branch-[0-9]{8}T[0-9]{9}Z/feature/ff$", backup.Name);
        Assert.Equal(tip, backup.Sha);
        Assert.Equal([backup.Name], seen!.Refs);
        Assert.Equal("Deleted feature/ff", app.StatusText);
        Assert.Equal(["Delete branch?", "Delete branch?"], app.Dialogs.Confirmations.Select(request => request.Title));
        Assert.All(app.Dialogs.Confirmations, request => Assert.Equal("A backup is saved first, so this can be undone.", request.Detail));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_backup_that_cannot_be_saved_stops_the_operation_before_it_changes_anything()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var ran = false;
        app.Dialogs.Confirm().CloseError();

        var result = await app.Operations.ConfirmAndRunAsync(
            OperationTexts.ConfirmDeleteBranch("feature/ff", null),
            OperationTexts.DeleteBranch("feature/ff"),
            OperationKind.Local,
            RefreshScope.Refs,
            _ => Task.FromResult<BackupRequest?>(new BackupRequest("branch", [new BackupEntry("feature/ff", "refs/heads/no-such-branch")])),
            (_, _) =>
            {
                ran = true;
                return Task.FromResult(OperationReport.Done("Deleted feature/ff"));
            });

        Assert.Equal(OperationOutcome.Failed, result!.Outcome);
        Assert.False(ran);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/feature/ff"));
        Assert.Equal(("Deleting the branch failed", "Git could not delete feature/ff."), (app.Dialogs.Errors.Single().Title, app.Dialogs.Errors.Single().Summary));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Dialogs_name_the_repository_only_when_the_windows_rule_says_so()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Cancel().Cancel().Cancel();

        // No window here: the rule the window would set (more than one tab with a repository, 4f).
        await app.Operations.ShowDialogAsync(app.Operations.NewTagDialog("At HEAD"));
        app.Services.NamesRepositoryInDialogs = () => true;
        await app.Operations.ShowDialogAsync(app.Operations.NewTagDialog("At HEAD"));
        await app.Operations.ConfirmAsync(OperationTexts.ConfirmDeleteTag("v1.0"));

        Assert.Null(app.Dialogs.Shown[0].RepositoryName);
        Assert.Equal(app.Repository.Name, app.Dialogs.Shown[1].RepositoryName);
        Assert.Equal(app.Repository.Name, app.Dialogs.Confirmations.Single().RepositoryName);
    }

    [Fact]
    public async Task Names_are_checked_against_the_repositorys_branches_tags_and_remotes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var operations = app.Operations;
        operations.IgnoreCaseInNames = true;

        Assert.Equal("A branch named feature/alpha exists.", operations.BranchNameError("feature/alpha"));
        Assert.Equal("This conflicts with the branch feature/alpha.", operations.BranchNameError("feature"));
        Assert.Equal("A branch named main exists in another case.", operations.BranchNameError("Main"));
        Assert.Equal("This is not a valid branch name: it contains a space.", operations.BranchNameError("my branch"));
        Assert.Null(operations.BranchNameError("feature/ff", renaming: "feature/ff"));
        Assert.Null(operations.BranchNameError("topic"));
        Assert.Equal("A tag named v1.0 exists.", operations.TagNameError("v1.0"));
        Assert.Null(operations.TagNameError("v2.0"));
        Assert.Equal("A remote named origin exists.", operations.RemoteNameError("origin"));
        Assert.Null(operations.RemoteNameError("origin", renaming: "origin"));

        operations.IgnoreCaseInNames = false;
        Assert.Null(operations.BranchNameError("Main"));

        var dialog = operations.NewBranchDialog("From main");
        dialog.Name = "main";
        Assert.Equal("A branch named main exists.", dialog.NameError);
        var rename = operations.RenameBranchDialog("feature/ff");
        Assert.Equal("feature/ff", rename.Name);
        Assert.False(rename.CanConfirm);
        var tag = operations.NewTagDialog("At HEAD");
        tag.Name = "v1.1";
        Assert.Equal("A tag named v1.1 exists.", tag.NameError);
    }

    [Fact]
    public async Task The_lookups_find_refs_stashes_commits_and_the_current_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var operations = app.Operations;
        var head = await app.ShaAsync("HEAD");
        var stash = await app.ShaAsync("refs/stash");

        Assert.Equal("main", operations.CurrentBranch?.Name);
        Assert.Equal("main", operations.HeadName);
        Assert.Equal("origin/main", RepositoryOperations.UpstreamText(operations.CurrentBranch!));
        Assert.Equal(RefKind.Tag, operations.FindRef("refs/tags/v1.0")?.Kind);
        Assert.Null(operations.FindRef("refs/heads/nowhere"));
        Assert.Equal(["origin"], operations.RemoteNames);
        Assert.Equal(["feature/alpha", "feature/conflict", "feature/ff", "main"], operations.LocalBranchNames);
        Assert.Equal(["v1.0", "v1.1"], operations.TagNames);
        Assert.Equal(["origin/feature/alpha", "origin/main"], operations.RemoteBranchesOf("origin").Select(gitRef => gitRef.Name));
        Assert.Equal("stash@{0}", operations.FindStash(stash)?.Name);
        Assert.Null(operations.FindStash(head));
        Assert.Equal("Main work", operations.FindCommit(head)?.Subject);
        Assert.Equal($"{head[..7]} \"Main work\"", await operations.DescribeCommitAsync(head));
        Assert.Equal(new string('0', 7), await operations.DescribeCommitAsync(new string('0', 40)));
        Assert.False(operations.Status.HasChanges);
        Assert.Equal(0, await operations.CountConflictedFilesAsync());
    }

    [Fact]
    public void Short_names_are_the_ones_texts_show()
    {
        Assert.Equal("feature/alpha", RepositoryOperations.ShortName("refs/heads/feature/alpha"));
        Assert.Equal("origin/feature/alpha", RepositoryOperations.ShortName("refs/remotes/origin/feature/alpha"));
        Assert.Equal("v1.0", RepositoryOperations.ShortName("refs/tags/v1.0"));
        Assert.Equal("a1b2c3d", RepositoryOperations.ShortName("a1b2c3d4e5f60718293a4b5c6d7e8f9012345678"));
        Assert.Equal("stash@{0}", RepositoryOperations.ShortName("stash@{0}"));
        Assert.True(RepositoryOperations.IsFullSha(new string('a', 64)));
        Assert.False(RepositoryOperations.IsFullSha("A1B2C3D4E5F60718293A4B5C6D7E8F9012345678"));
        Assert.False(RepositoryOperations.IsFullSha("main"));
    }

    [Fact]
    public async Task Lock_files_are_listed_relative_to_the_working_tree_or_in_full_outside_it()
    {
        using var repo = await Scenarios.LinearAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var inside = Path.Combine(app.Repository.Path, ".git", "refs", "heads", "main.lock");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "packed-refs.lock");

        Assert.Equal(".git/refs/heads/main.lock", app.Operations.ShowPath(inside));
        Assert.Equal(outside, app.Operations.ShowPath(outside));
    }

    [Fact]
    public async Task The_toolbar_and_the_remotes_dialog_start_the_operations_of_the_same_meaning()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var queue = new RecordingQueue();
        var operations = new RepositoryOperations(app.Repository, queue, app.Services, app.Log);
        IToolbarActions toolbar = operations;
        IRemoteActions remotes = operations;

        // Each call either reaches the stub of its operation (until its sub-package has built it)
        // or, once built, queues the operation, or opens the dialog, of that meaning (2.33, 4d,
        // 2.23). The queue here runs nothing, and the script cancels every dialog.
        await AssertStartsAsync(() => toolbar.FetchAllAsync(), nameof(RepositoryOperations.FetchAllAsync), () => queue.Has("Fetch"));
        await AssertStartsAsync(() => toolbar.PullAsync(), nameof(RepositoryOperations.PullAsync), () => queue.Has("Pull"));
        await AssertStartsAsync(() => toolbar.PushAsync(), nameof(RepositoryOperations.PushCurrentBranchAsync), () => queue.Has("Push main"));
        await AssertStartsAsync(() => toolbar.CreateBranchAtHeadAsync(), nameof(RepositoryOperations.CreateBranchAsync), () => app.Dialogs.Shown.OfType<BranchNameDialogViewModel>().Any());
        await AssertStartsAsync(() => toolbar.StashAsync(), nameof(RepositoryOperations.StashAsync), () => app.Dialogs.Shown.OfType<StashDialogViewModel>().Any());
        await AssertStartsAsync(() => toolbar.PopAsync(), nameof(RepositoryOperations.PopNewestStashAsync), () => queue.Has("Pop stash@{0}"));

        IReadOnlyList<RemoteInfo>? read = null;
        await AssertStartsAsync(async () => read = await remotes.ReadRemotesAsync(), nameof(RepositoryOperations.ReadRemotesAsync), () => read?.Single().Name == "origin");
        await AssertStartsAsync(() => remotes.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), fetchNow: false), nameof(RepositoryOperations.AddRemoteAsync), () => queue.Has("Add remote mirror"));
        await AssertStartsAsync(() => remotes.RenameRemoteAsync("origin", "upstream"), nameof(RepositoryOperations.RenameRemoteAsync), () => queue.Has("Rename remote origin"));
        await AssertStartsAsync(() => remotes.SetRemoteUrlAsync("origin", "https://example.com/x.git"), nameof(RepositoryOperations.SetRemoteUrlAsync), () => queue.Has("Change URL of origin"));
        await AssertStartsAsync(() => remotes.RemoveRemoteAsync("origin"), nameof(RepositoryOperations.RemoveRemoteAsync), () => app.Dialogs.Confirmations.Any(request => request.Title == "Remove remote origin?"));
        Assert.Equal((object)app.Repository.Operations, app.Repository.Toolbar.Actions);
    }

    /// <summary>
    /// <paramref name="call"/> reached <paramref name="method"/>: either its stub (a
    /// <see cref="NotImplementedException"/> naming the method, while its sub-package has not built
    /// it), or, built, it did what <paramref name="started"/> checks.
    /// </summary>
    private static async Task AssertStartsAsync(Func<Task> call, string method, Func<bool> started)
    {
        var exception = await Record.ExceptionAsync(call);
        if (exception is NotImplementedException notBuilt)
        {
            Assert.StartsWith("WP7: ", notBuilt.Message, StringComparison.Ordinal);
            Assert.EndsWith(": " + method, notBuilt.Message, StringComparison.Ordinal);
            return;
        }

        Assert.Null(exception);
        Assert.True(started(), $"{method} did not start what it should.");
    }

    /// <summary>A repository view model that was never opened, whose operations serve the pure helpers.</summary>
    private static RepositoryViewModel Detached() =>
        new(new EmptyRepository(), new MainWindowViewModelTests.FakeSettings(), new Core.DateDisplay(TimeZoneInfo.Utc));

    private sealed class EmptyRepository : FakeRepositoryBase
    {
        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "empty");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "empty";

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepoRefs(new HeadState("main", null), [], [], []));

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => throw new NotSupportedException();
    }

    /// <summary>A queue that runs nothing: it records each operation and answers that it was cancelled.</summary>
    private sealed class RecordingQueue : IOperationQueue
    {
        private readonly List<GitOperation> _operations = [];

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<OperationFinishedEventArgs>? Finished
        {
            add { }
            remove { }
        }

        public OperationSnapshot? Current => null;

        public int Waiting => 0;

        public OperationResult? LastResult => null;

        public bool Has(string title) => _operations.Any(operation => operation.Title == title);

        public Task<OperationResult> RunAsync(GitOperation operation)
        {
            _operations.Add(operation);
            return Task.FromResult(new OperationResult(OperationOutcome.Cancelled, $"{operation.Title} cancelled", null));
        }

        public bool TryStartInBackground(GitOperation operation) => false;

        public Task WhenIdleAsync() => Task.CompletedTask;

        public void CancelCurrent()
        {
        }

        public void DropWaiting()
        {
        }

        public void Dispose()
        {
        }
    }
}
