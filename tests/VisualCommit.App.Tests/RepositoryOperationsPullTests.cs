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
/// classes in one xunit collection, which runs beside those of the other sub-packages
/// (<see cref="RepositoryOperationsTestSupport"/> says why): <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RemotesCollection)]
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
        // docs/phase-6-design.md 4.8 (D211): git's "Not possible to fast-forward" gets its own
        // summary, and the merge keeps ignored files (9.10).
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Pull failed", "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.", "git merge --ff-only --no-overwrite-ignore FETCH_HEAD"),
            (error.Title, error.Summary, error.Command));
        Assert.Contains("Not possible to fast-forward", error.Output, StringComparison.Ordinal);
        AssertOutputOfFailedCall(app, error, "git merge --ff-only --no-overwrite-ignore FETCH_HEAD");
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(local, await repo.HeadAsync());
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_bulk_runs_pull_refused_by_pull_ff_only_opens_no_dialog_and_keeps_the_one_it_would_have_opened()
    {
        // docs/phase-6-design.md 4.6 (D195): PullAsync(part) defers "Pull failed" with 4.8's summary,
        // and ShowDeferredAsync later opens the toolbar's identical dialog.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        var kept = new List<DeferredDialog>();
        using var group = new PromptGroup();

        var result = await app.Operations.PullAsync(new BulkPart(CancellationToken.None, group, kept.Add));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal((OperationOutcome.Failed, "Pull failed"), (result?.Outcome, result?.Message));
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        var dialog = Assert.Single(kept);
        Assert.Equal(
            ("Pull failed", "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main."),
            (dialog.Display?.Title, dialog.Display?.Summary));
        Assert.Equal("git merge --ff-only --no-overwrite-ignore FETCH_HEAD", dialog.Failure?.CommandText);
        Assert.Equal(local, await repo.HeadAsync());

        // Opened later, as often as asked: the dialog the toolbar's Pull opens.
        app.Dialogs.CloseError().CloseError();
        await app.Operations.ShowDeferredAsync(dialog);
        await app.Operations.ShowDeferredAsync(dialog);
        app.Dialogs.CloseError();
        await app.Operations.PullAsync();
        var (first, second, toolbar) = (app.Dialogs.Errors[0], app.Dialogs.Errors[1], app.Dialogs.Errors[2]);
        foreach (var shown in new[] { first, second })
        {
            Assert.Equal(
                (toolbar.Title, toolbar.Summary, toolbar.Output, toolbar.Command, toolbar.ExtraAction, toolbar.RepositoryName),
                (shown.Title, shown.Summary, shown.Output, shown.Command, shown.ExtraAction, shown.RepositoryName));
        }

        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_bulk_runs_pull_without_an_upstream_starts_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.GitAsync("branch", "--unset-upstream");
        using var app = await OperationsHarness.OpenAsync(repo);
        using var group = new PromptGroup();

        Assert.Null(await app.Operations.PullAsync(new BulkPart(CancellationToken.None, group, _ => Assert.Fail("Nothing to keep."))));
        Assert.Null(app.LastResult);
    }

    [Fact]
    public async Task A_plain_merge_refused_by_merge_ff_only_keeps_the_default_summary()
    {
        // 4.8's E5: merge.ff=only (pull.ff unset) refuses git's own merge the same way, with no
        // --ff-only on its command line; pull.ff's sentence would name the wrong key.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("merge.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "Git could not pull origin/main into main.", "git merge --no-edit --no-overwrite-ignore FETCH_HEAD"), (error.Title, error.Summary, error.Command));
        Assert.Contains("Not possible to fast-forward", error.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("pull.ff", error.Summary, StringComparison.Ordinal);
        Assert.Equal(local, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task With_keep_ignored_files_off_the_pulls_merge_runs_without_gits_option()
    {
        // 9.10: Keep ignored files is read when the pull runs; off, git's default.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Settings.Update(settings => settings with { KeepIgnoredFiles = false });
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.", "git merge --ff-only FETCH_HEAD"), (error.Summary, error.Command));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => IsIgnoredRead(call.CommandText));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing()
    {
        // docs/phase-6-design.md 8.9 (D219): read before the fetch, so nothing is fetched.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var head = await repo.HeadAsync();
        var tracking = await repo.RefTargetAsync("refs/remotes/origin/main");
        await repo.ConfigAsync("pull.rebase", "preserve");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "pull.rebase is set to preserve, which this app does not understand.", "git config --get pull.rebase"), (error.Title, error.Summary, error.Command));
        Assert.Equal("Pull failed", app.StatusText);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(tracking, await repo.RefTargetAsync("refs/remotes/origin/main"));
        var calls = OperationCalls(app, mark);
        Assert.DoesNotContain(calls, call => call.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal(
            ["git config --get pull.ff", "git config --get branch.main.rebase", "git config --get pull.rebase", "git config --type=bool --get pull.rebase"],
            calls.Where(call => call.StartsWith("git config --get ", StringComparison.Ordinal) || call.StartsWith("git config --type=bool ", StringComparison.Ordinal)));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_chosen_rebase_refuses_a_pull_ff_git_pull_rebase_refuses_before_the_fetch()
    {
        // D231: "Pull from {up} (rebase)" counts only pull.ff, and refuses before the fetch a value
        // git pull --rebase refuses (foo); a rebase setting git would refuse does not count.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        var head = await repo.HeadAsync();
        var tracking = await repo.RefTargetAsync("refs/remotes/origin/main");
        await repo.ConfigAsync("pull.ff", "foo");
        await repo.ConfigAsync("pull.rebase", "preserve");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.PullRebaseAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "pull.ff is set to foo, which this app does not understand.", "git config --get pull.ff"), (error.Title, error.Summary, error.Command));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(tracking, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.DoesNotContain(OperationCalls(app, mark), call => call.StartsWith("git fetch", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_chosen_rebase_reads_pull_ff_before_the_fetch_and_only_gives_way_to_the_rebase()
    {
        // D231: pull.ff=only gives way to the rebase; an invalid pull.rebase is no reason to refuse.
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        await repo.ConfigAsync("pull.rebase", "preserve");
        using var app = await OperationsHarness.OpenAsync(repo);
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.PullRebaseAsync();

        Assert.Equal("Pulled and rebased main onto origin/main", app.StatusText);
        Assert.Equal(remote, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());
        var calls = OperationCalls(app, mark);
        var read = calls.IndexOf("git config --get pull.ff");
        Assert.InRange(read, 0, calls.IndexOf("git fetch --progress origin") - 1);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Theory]
    [InlineData("fast-forward", "pull.ff=only", "merge")]
    [InlineData("merge", null, "merge")]
    [InlineData("rebase", "pull.rebase=true", "rebase")]
    public async Task An_ignored_file_in_the_pulls_way_fails_it_with_the_files_name_and_no_git_write(string mode, string? setting, string operation)
    {
        // 9.10, 9.15, 9.17: the check inside the pull's integration, after the fetch; "Pull failed"
        // with 9.15's sentence, no git output or command (PullRefusalDisplay gives none); no git
        // merge or rebase, no backup.
        using var repo = await IgnoredInTheWayAsync(mode, setting);
        var head = await repo.HeadAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", $"1 ignored file is in the way of the {operation}: secret.env. Move it away first."), (error.Title, error.Summary));
        Assert.False(error.HasOutput);
        Assert.False(error.HasCommand);
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        var calls = OperationCalls(app, mark);
        Assert.Contains("git fetch --progress origin", calls);
        Assert.DoesNotContain(calls, call => call.StartsWith("git merge ", StringComparison.Ordinal) || call.StartsWith("git rebase", StringComparison.Ordinal));
        var reads = calls.Where(IsIgnoredRead).ToList();
        Assert.True(IgnoredReadsOf(mode) == reads.Count, $"The {mode}'s reads: {string.Join(" | ", reads)}");
        Assert.Contains("git check-ignore --stdin -z", reads);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("LOCAL\n", Read(repo, "secret.env"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Contains(app.Log.Messages, message => message.Contains($": Pull: refused, 1 ignored file is in the way of the {operation}: secret.env.", StringComparison.Ordinal));
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

    // ---- 9.10: the reads of the pull's check --------------------------------------------------------

    /// <summary>
    /// <c>main</c> tracking <c>origin/main</c>, to which another clone pushed "Upstream adds secret"
    /// (<c>secret.env</c> "UP"), ignored here by <c>.git/info/exclude</c> and on disk as "LOCAL"; with
    /// a commit of its own unless <paramref name="mode"/> is the fast-forward, and
    /// <paramref name="setting"/> (<c>key=value</c>) in its configuration (the git layer's
    /// <c>IntegratePullTests</c> build the same).
    /// </summary>
    private static async Task<TempRepo> IgnoredInTheWayAsync(string mode, string? setting)
    {
        var repo = await TempRepo.CreateAsync("pull-ignored");
        await repo.CommitFileAsync("README", "base\n", "base");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", setUpstream: true, "main");
        await Scenarios.PushFromElsewhereAsync(repo, "main", "secret.env", "UP\n", "Upstream adds secret");
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "info", "exclude"), "secret.env\n", TestContext.Current.CancellationToken);
        if (mode != "fast-forward")
        {
            await repo.CommitFileAsync("local.txt", "local\n", "Local work");
        }

        if (setting is not null)
        {
            var at = setting.IndexOf('=', StringComparison.Ordinal);
            await repo.ConfigAsync(setting[..at], setting[(at + 1)..]);
        }

        repo.WriteFile("secret.env", "LOCAL\n");
        return repo;
    }

    /// <summary>
    /// The reads of 9.4 and 9.6 the pull makes before it refuses, in order (9.10): the merge's (the
    /// fast-forward's too, which then asks whether the branches diverged), the rebase's after the
    /// pull's own <c>--is-ancestor</c> question that chose it.
    /// </summary>
    private static int IgnoredReadsOf(string mode) => mode switch
    {
        "fast-forward" => 5,
        "merge" => 4,
        _ => 6,
    };

    /// <summary>The command texts of the calls operations made since <paramref name="mark"/>.</summary>
    private static List<string> OperationCalls(OperationsHarness app, int mark) =>
        [.. app.Calls.Snapshot().Skip(mark).Where(call => call.OperationId is not null).Select(call => call.CommandText)];

    /// <summary>9.3 to 9.8's reads, and the pull's own <c>--is-ancestor</c> question (RepositoryOperationsKeepIgnoredTests' list).</summary>
    private static bool IsIgnoredRead(string command) =>
        command == "git check-ignore --stdin -z"
        || command.StartsWith("git diff-tree --stdin", StringComparison.Ordinal)
        || command.StartsWith("git diff-tree -r -z --no-renames --name-only --diff-filter=", StringComparison.Ordinal)
        || command.StartsWith("git merge-base --all", StringComparison.Ordinal)
        || command.StartsWith("git merge-base --is-ancestor", StringComparison.Ordinal)
        || command.StartsWith("git log --no-renames --name-only -z --root --no-merges", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --min-parents=2", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --topo-order --boundary", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --stdin --no-walk=unsorted", StringComparison.Ordinal)
        || command.StartsWith("git ls-tree -r -z --name-only", StringComparison.Ordinal);
}
