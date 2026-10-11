using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP7-remotes: pull in <see cref="RepositoryOperations"/> (docs/phase-3-design.md 2.18),
/// against the scenario repositories, through <see cref="OperationsHarness"/>: for each, its
/// confirmation, the backup it saves, what git holds afterwards, the status bar's text, its
/// failure dialog with git's own output and its extra button, its cancel, and that a second
/// click runs nothing more. Folder remotes serve most of them. WP7-remotes' tests are four
/// classes, so that xunit runs them side by side:
/// <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
public class RepositoryOperationsPullTests
{
    // 2.18: pull.

    [Fact]
    public async Task Pull_merges_diverged_branches_as_git_pull_would_and_says_so()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        IToolbarActions toolbar = app.Operations;

        await toolbar.PullAsync();

        Assert.Equal(("Pulled and merged origin/main into main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        var parents = (await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD")).StandardOutput.Trim().Split(' ');
        Assert.Equal([local, remote], parents[1..]);
        Assert.StartsWith("Merge branch 'main' of ", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(2, app.Operations.CurrentBranch?.Ahead);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Pull_fast_forwards_counts_the_commits_and_says_when_there_was_nothing_to_pull()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal("Pulled 1 commit into main (fast-forward)", app.StatusText);
        Assert.Equal(remote, await repo.HeadAsync());

        await app.Operations.PullAsync();

        Assert.Equal("main is already up to date", app.StatusText);
        Assert.Equal(remote, await repo.HeadAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_of_a_branch_whose_upstream_was_deleted_on_the_remote_says_so_and_integrates_nothing()
    {
        // origin's published is deleted there (a merged pull request); origin/published stays here (D99).
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CheckoutAsync("published");
        var head = await repo.CommitFileAsync("own.txt", "own\n", "Own work");
        var deleted = await repo.GitAsync("--git-dir=" + repo.SiblingPath("origin.git"), "branch", "-D", "published");
        Assert.True(deleted.Succeeded, deleted.StandardError);
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal(("published no longer exists on origin", OperationOutcome.Succeeded), (app.StatusText, app.LastResult?.Outcome));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git merge", StringComparison.Ordinal) || call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_refused_by_pull_ff_only_shows_gits_message_after_the_fetch_ran()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "Git could not pull origin/main into main.", "git merge --ff-only FETCH_HEAD"), (error.Title, error.Summary, error.Command));
        Assert.Contains("Not possible to fast-forward", error.Output, StringComparison.Ordinal);
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(local, await repo.HeadAsync());
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var head = await repo.HeadAsync();
        await repo.ConfigAsync("pull.rebase", "preserve");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "pull.rebase is set to preserve, which this app does not understand."), (error.Title, error.Summary));
        Assert.Equal("Pull failed", app.StatusText);
        Assert.Equal(head, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Pull_that_stops_with_conflicts_says_so_in_the_warning_colour_without_a_dialog()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/shared.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/shared.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal(("Pull stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Conflicts, app.LastResult?.Outcome);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_rebases_when_git_is_told_to()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.rebase", "true");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal("Pulled and rebased main onto origin/main", app.StatusText);
        Assert.Equal(remote, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());
        Assert.Equal("Local change", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Pull_from_a_local_upstream_skips_the_fetch_and_names_the_branch_alone()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("loc");
        var main = await repo.RefTargetAsync("refs/heads/main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);

        await app.Operations.PullAsync();

        Assert.Contains("Pulling main into loc…", texts);
        Assert.Equal("Pulled 1 commit into loc (fast-forward)", app.StatusText);
        Assert.Equal(main, await repo.HeadAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_cancelled_during_its_fetch_integrates_nothing()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();

        var held = server.HoldNextFetchPack();
        var pull = app.Operations.PullAsync();
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => app.StatusText.EndsWith('%'), "the pull's percentage");
        Assert.Matches(@"^Pulling origin/main into main… Counting objects \d{1,3}%$", app.StatusText);

        app.Repository.Queue.CancelCurrent();
        await pull.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        server.Release();

        Assert.Equal(("Pull cancelled", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.Empty(app.Dialogs.Shown);
    }
}
