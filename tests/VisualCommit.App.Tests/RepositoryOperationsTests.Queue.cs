using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Operations that wait in the queue (docs/phase-3-design.md 1.3): what the user saw when they
/// chose one may have changed by the time it runs, because an operation queued ahead of it, or a
/// terminal, moved HEAD. Each decides and checks again inside its operation, and refuses, with
/// the error dialog of its own kind and no question, rather than act on something the user did
/// not name. Each test holds the queue with an operation of its own, queues the operation under
/// test behind it, changes the repository, then lets the queue go on.
/// </summary>
public partial class RepositoryOperationsTests
{
    private static readonly TimeSpan QueueTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_checkout_queued_behind_commits_made_on_a_detached_head_refuses_rather_than_leave_them_behind()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.DetachAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().Confirm();
        string? picked = null;

        // HEAD is detached on main's commit: nothing would be left behind when the checkout is
        // chosen, so it asks nothing. The operation ahead of it then commits on the detached HEAD.
        var (hold, ahead) = HoldQueue(app, async () => picked = await repo.CommitAsync("Picked onto the detached HEAD"));
        var checkout = app.Operations.CheckoutAsync("refs/heads/feature/ff");
        await WaitForQueuedAsync(app, 1);
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(picked, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(app.Dialogs.Confirmations);
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Checkout failed", "HEAD moved while this waited: it is now detached on 1 commit that is on no branch. Nothing was checked out, so it is not left behind."),
            (dialog.Title, dialog.Summary));
        Assert.Equal(("Check out feature/ff failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        // Chosen again, with the refs read since, the checkout asks and backs the commit up (2.1).
        await app.Operations.CheckoutAsync("refs/heads/feature/ff");

        Assert.Equal("Leave these commits?", Assert.Single(app.Dialogs.Confirmations).Title);
        Assert.Equal("feature/ff", await repo.CurrentBranchAsync());
        Assert.Equal(picked, Assert.Single(await repo.BackupRefsAsync()).Sha);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_branch_created_and_checked_out_behind_commits_made_on_a_detached_head_is_not_created()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.DetachAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var start = await app.ShaAsync("refs/heads/feature/ff");
        app.Dialogs.EnterName("topic").CloseError();

        var (hold, ahead) = HoldQueue(app, () => repo.CommitAsync("Picked onto the detached HEAD"));
        var create = app.Operations.CreateBranchAsync(start);
        await WaitForQueuedAsync(app, 1);
        hold.SetResult();
        await Task.WhenAll(ahead, create).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Null(await repo.RefTargetAsync("refs/heads/topic"));
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded);
        Assert.Equal(
            "HEAD moved while this waited: it is now detached on 1 commit that is on no branch. Nothing was checked out, so it is not left behind.",
            Assert.Single(app.Dialogs.Errors).Summary);
        Assert.Equal("Create branch topic failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_pull_queued_behind_a_checkout_pulls_into_nothing_else()
    {
        using var repo = await Scenarios.OperationsAsync();

        // main tracks the local feature/ff, which has a commit main lacks.
        await repo.GitAsync("branch", "--set-upstream-to=feature/ff", "main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");
        var main = await app.ShaAsync("refs/heads/main");
        app.Dialogs.CloseError();

        var (hold, ahead) = HoldQueue(app);
        var checkout = app.Operations.CheckoutAsync("refs/heads/feature/alpha");
        var pull = app.Operations.PullAsync();
        Assert.Equal(2, app.Repository.Queue.Waiting);
        hold.SetResult();
        await Task.WhenAll(ahead, checkout, pull).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal(main, await repo.RefTargetAsync("refs/heads/main"));
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Pull failed", "HEAD moved while this waited: it was on main and is on feature/alpha now. Nothing was pulled."),
            (dialog.Title, dialog.Summary));
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_pull_whose_branch_got_another_upstream_while_it_waited_pulls_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--set-upstream-to=feature/ff", "main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("refs/heads/main");
        app.Dialogs.CloseError();

        var (hold, ahead) = HoldQueue(app, () => repo.GitAsync("branch", "--set-upstream-to=feature/alpha", "main"));
        var pull = app.Operations.PullAsync();
        hold.SetResult();
        await Task.WhenAll(ahead, pull).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(main, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(
            "main's upstream changed while this waited: it is feature/alpha now, not feature/ff. Nothing was pulled.",
            Assert.Single(app.Dialogs.Errors).Summary);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_reset_whose_branch_was_left_while_its_dialog_was_open_resets_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var initial = await app.ShaAsync("main~2");
        var main = await app.ShaAsync("refs/heads/main");
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");

        // The checkout happens while the dialog is open (a terminal, or an operation ahead).
        app.Dialogs.Answer<ResetDialogViewModel>(dialog =>
        {
            repo.CheckoutAsync("feature/alpha").GetAwaiter().GetResult();
            dialog.Mode = ResetMode.Hard;
            dialog.ConfirmCommand.Execute(null);
        }).CloseError();

        await app.Operations.ResetAsync(initial);

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal(main, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Empty(await repo.BackupRefsAsync());
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Reset failed", "HEAD moved while this waited: it was on main and is on feature/alpha now. Nothing was reset."),
            (dialog.Title, dialog.Summary));
        Assert.Equal(("Reset main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_reset_queued_behind_a_commit_on_its_branch_resets_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var initial = await app.ShaAsync("main~2");
        var main = await app.ShaAsync("refs/heads/main");
        string? committed = null;
        app.Dialogs.Reset(ResetMode.Hard).CloseError();

        var (hold, ahead) = HoldQueue(app, async () => committed = await repo.CommitAsync("Committed while the reset waited"));
        var reset = app.Operations.ResetAsync(initial);
        await WaitForQueuedAsync(app, 1);
        hold.SetResult();
        await Task.WhenAll(ahead, reset).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(committed, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(
            $"main moved while this waited: it was at {main[..7]} and is at {committed![..7]} now. Nothing was reset.",
            Assert.Single(app.Dialogs.Errors).Summary);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_merge_queued_behind_a_checkout_merges_into_nothing_else()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");
        app.Dialogs.CloseError();

        var (hold, ahead) = HoldQueue(app);
        var checkout = app.Operations.CheckoutAsync("refs/heads/feature/alpha");
        var merge = app.Operations.MergeAsync("refs/heads/feature/ff", MergeMode.Default);
        Assert.Equal(2, app.Repository.Queue.Waiting);
        hold.SetResult();
        await Task.WhenAll(ahead, checkout, merge).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal(
            "HEAD moved while this waited: it was on main and is on feature/alpha now. Nothing was merged.",
            Assert.Single(app.Dialogs.Errors).Summary);
        Assert.Equal("Merge feature/ff failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Push_clicked_again_after_a_commit_made_while_a_push_runs_pushes_that_commit_too()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CommitFileAsync("src/first.txt", "first\n", "First change");

        // origin.git's pre-receive hook holds the push until the test lets it go.
        var origin = repo.SiblingPath("origin.git");
        var entered = Path.Combine(origin, "hook-entered");
        var gate = Path.Combine(origin, "hook-gate");
        var hook = Path.Combine(origin, "hooks", "pre-receive");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\ntouch hook-entered\nwhile [ ! -f hook-gate ]; do sleep 0.1; done\ncat >/dev/null\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        using var app = await OperationsHarness.OpenAsync(repo);
        var first = app.Operations.PushCurrentBranchAsync();
        await WaitUntilAsync(() => File.Exists(entered), "the push to reach the remote");

        // The push has chosen what it sends; the user commits and clicks Push again.
        var second = await repo.CommitFileAsync("src/second.txt", "second\n", "Second change");
        var again = app.Operations.PushCurrentBranchAsync();
        Assert.Equal(1, app.Repository.Queue.Waiting);
        File.WriteAllText(gate, string.Empty);
        await Task.WhenAll(first, again).WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(second, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal(0, app.Operations.CurrentBranch?.Ahead);
        Assert.Equal("Pushed main to origin/main", app.StatusText);
        Assert.Equal(2, Ran(app, "Push main"));
    }

    [Fact]
    public async Task A_stalled_auto_fetch_that_an_operation_waits_for_can_be_cancelled_and_its_lock_files_removed()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);
        using var scheduler = new AutoFetchScheduler(() => app.Repository, () => false, app.Settings, TimeProvider.System, app.Log)
        {
            ReadEnvironment = _ => "1",
            SinceOpening = _ => TimeSpan.FromHours(1),
        };
        var lockFile = Path.Combine(repo.Path, ".git", "refs", "remotes", "origin", "main.lock");
        app.Dialogs.CloseError(clickExtra: true);

        // The auto-fetch's pack never comes, as on a connection that went silent.
        var held = server.HoldNextFetchPack();
        scheduler.Tick(DateTimeOffset.UtcNow);
        await held.WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);
        Assert.True(app.Repository.Queue.Current?.IsBackground);
        Assert.False(app.Repository.OperationStatus.CanCancel);
        File.WriteAllText(lockFile, "left by the stopped auto-fetch");

        var (hold, staged) = HoldQueue(app);
        hold.SetResult();
        Assert.Equal((OperationStatusViewModel.WaitingForAutoFetchText, true), (app.StatusText, app.Repository.OperationStatus.CanCancel));
        app.Repository.OperationStatus.CancelCommand.Execute(null);
        await staged.WaitAsync(QueueTimeout, TestContext.Current.CancellationToken);
        var dialog = await app.Dialogs.WaitForAsync<ErrorDialogViewModel>(QueueTimeout);
        server.Release();

        Assert.Equal(("Auto-fetch cancelled", OperationTexts.StaleLockFilesSummary, ".git/refs/remotes/origin/main.lock"), (dialog.Title, dialog.Summary, dialog.Output));
        await WaitUntilAsync(() => !File.Exists(lockFile), "Remove lock files");
        Assert.Contains(
            "git -c http.lowSpeedLimit=1 -c http.lowSpeedTime=60 fetch --progress --no-auto-maintenance origin",
            app.Calls.Snapshot().Select(call => call.CommandText));
        Assert.Contains(app.Log.Messages, message => message.Contains(": Auto-fetch: Cancelled after ", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_cancelled_fetch_lists_neither_another_worktrees_index_lock_nor_a_lock_taken_again_since()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("worktree", "add", "--quiet", repo.SiblingPath("wt"), "feature/ff");
        using var app = await OperationsHarness.OpenAsync(repo);
        var gitFolder = app.Operations.Git.GitDirectory;
        var refLock = Path.Combine(gitFolder, "refs", "remotes", "origin", "main.lock");
        var otherWorktree = Path.Combine(gitFolder, "worktrees", "wt", "index.lock");
        ErrorDialogViewModel? shown = null;

        // While the dialog is open, the stale lock goes (a terminal's git removed it) and a live
        // git takes that name again: "Remove lock files" must leave the new one alone.
        app.Dialogs.Answer<ErrorDialogViewModel>(dialog =>
        {
            shown = dialog;
            File.Delete(refLock);
            File.WriteAllText(refLock, "held by a live git");
            File.SetLastWriteTimeUtc(refLock, DateTime.UtcNow.AddSeconds(5));
            dialog.ExtraCommand.Execute(null);
        });

        var result = await app.Operations.RunAsync(OperationTexts.Fetch("origin"), OperationKind.Network, RefreshScope.Refs, _ =>
        {
            File.WriteAllText(refLock, "left by a stopped git");
            File.WriteAllText(otherWorktree, "held by a git in the other worktree");
            throw new OperationCanceledException();
        });

        Assert.Equal([Path.GetFullPath(refLock)], result.StaleLockFiles);
        Assert.Equal(".git/refs/remotes/origin/main.lock", shown!.Output);
        Assert.True(File.Exists(refLock), "The lock a live git took again was removed.");
        Assert.True(File.Exists(otherWorktree));
        app.AssertScriptFollowed();
    }

    /// <summary>
    /// Queues an operation of the test's own that waits until the returned source is set, then
    /// runs <paramref name="then"/> (a change to the repository, as an operation ahead of the one
    /// under test would make it); waits until it runs.
    /// </summary>
    private static (TaskCompletionSource Hold, Task<OperationResult> Ahead) HoldQueue(OperationsHarness app, Func<Task>? then = null)
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ahead = app.Repository.Queue.RunAsync(new GitOperation("Held", "Holding…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await hold.Task;
            if (then is not null)
            {
                await then();
            }

            return OperationReport.Done("Held");
        }));
        Assert.NotNull(app.Repository.Queue.Current);
        return (hold, ahead);
    }

    /// <summary>Waits until <paramref name="count"/> operations wait in the queue.</summary>
    private static Task WaitForQueuedAsync(OperationsHarness app, int count) =>
        WaitUntilAsync(() => app.Repository.Queue.Waiting == count, $"{count} waiting operation(s)");
}
