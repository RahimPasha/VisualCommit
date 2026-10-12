using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: "Pull from {up} (rebase)" (docs/phase-4-design.md section 6, D147) against a folder remote
/// through <see cref="OperationsHarness"/>: the pull's steps with its integration forced to a
/// rebase, the backup it saves through the git layer's callback before git rebases, a rebase that
/// stops (told by the rebase folder) and is continued or skipped (check A4), the configured pull's
/// backup when git's settings say rebase, and its dedupe key apart from a plain Pull's.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RebaseCollection)]
public class RepositoryOperationsPullRebaseTests
{
    [Fact]
    public async Task A_pull_with_rebase_saves_the_old_tip_and_rebases_without_arguments()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        using var app = await OperationsHarness.OpenAsync(repo);

        // A4 part A.
        await app.Operations.PullRebaseAsync();

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(("Pulled and rebased main onto origin/main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, "Pull (rebase)");
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git rebase");
        Assert.Equal(elsewhere, (await repo.GitAsync("rev-parse", "main~1")).StandardOutput.Trim());
        Assert.Equal("Local work", (await repo.GitAsync("log", "-1", "--format=%s", "main")).StandardOutput.Trim());
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/rebase-{BackupTime}/main$", backup.Name);
        Assert.Equal(local, backup.Sha);
    }

    [Fact]
    public async Task A_pull_whose_rebase_puts_back_stashed_changes_with_conflicts_says_so_in_the_warning_colour()
    {
        // The review's findings 12 and 19: rebase.autoStash stashes the change of src/shared.txt,
        // which the upstream changed too; the rebase ends rebased and the put-back conflicts (1.4).
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/shared.txt", "shared elsewhere\n", "Shared elsewhere");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        await repo.ConfigAsync("rebase.autoStash", "true");
        repo.WriteFile("src/shared.txt", "shared mine\n");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullRebaseAsync();

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(
            ("Pulled and rebased main onto origin/main; your changes conflicted when put back: they are also kept in the stash", OperationStatusTone.Warning),
            (app.StatusText, app.StatusTone));
        Assert.Equal("Local work", (await repo.GitAsync("log", "-1", "--format=%s", "main")).StandardOutput.Trim());
        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", app.Repository.Banner.Text);
        Assert.Single(await repo.StashListAsync());
    }

    [Fact]
    public async Task A_pull_with_rebase_that_stops_is_continued_with_the_stopped_commits_author()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CheckoutAsync("published");
        await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        var unpushed = await repo.HeadAsync();
        var date = (await repo.GitAsync("log", "-1", "--format=%at")).StandardOutput.Trim();
        using var app = await OperationsHarness.OpenAsync(repo);

        // A4 part B step 3: a stop is no failure (D95).
        await app.Operations.PullRebaseAsync();

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(("Pull stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        var state = app.Operations.Status.InProgress;
        Assert.Equal((InProgressKind.Rebase, "published", 1, 1), (state.Kind, state.Branch, state.Done, state.Total));
        Assert.Equal(
            $"Rebasing published onto origin/published: stopped at {unpushed[..7]} \"Unpushed work\" (1 of 1). 1 file has conflicts.",
            app.Repository.Banner.Text);
        Assert.Equal(unpushed, Assert.Single(await repo.BackupRefsAsync()).Sha);

        // Steps 5 and 6: resolved, marked resolved (it has no markers: no question), continued.
        repo.WriteFile("src/published.txt", "published\nelsewhere\nunpushed\n");
        await app.Repository.RefreshAsync();
        await app.Operations.StageFilesAsync([new ChangedFile("src/published.txt", FileChangeKind.Conflicted)]);
        Assert.Empty(app.Dialogs.Shown);
        await app.Operations.ContinueAsync(app.Operations.Status.InProgress, null);

        Assert.Equal("Rebased published onto origin/published", app.StatusText);
        Assert.Equal("published", await repo.CurrentBranchAsync());
        Assert.Equal(("Unpushed work", date), ((await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim(), (await repo.GitAsync("log", "-1", "--format=%at")).StandardOutput.Trim()));
        Assert.Equal("published\nelsewhere\nunpushed\n", Read(repo, "src/published.txt"));
        Assert.Single(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_pull_with_rebase_that_stops_is_skipped_after_its_backup()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CheckoutAsync("published");
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        using var app = await OperationsHarness.OpenAsync(repo);
        await app.Operations.PullRebaseAsync();
        var state = app.Operations.Status.InProgress;

        // A4 part C step 9: a Skip that ends the rebase shows Continue's done text.
        await app.Operations.SkipAsync(state);

        Assert.Equal("Rebased published onto origin/published", app.StatusText);
        AssertRanOnce(app, $"Skip {state.Commit![..7]}");
        Assert.Equal(elsewhere, await repo.HeadAsync());
        var names = (await repo.BackupRefsAsync()).Select(entry => entry.Name).ToList();
        Assert.Equal(3, names.Count);
        Assert.Contains(names, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/rebase-{BackupTime}/published$"));
        Assert.Contains(names, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/skip-{BackupTime}$"));
        Assert.Contains(names, name => Regex.IsMatch(name, $"^refs/visualcommit/backup/skip-{BackupTime}-changes$"));
    }

    [Fact]
    public async Task The_configured_pull_saves_the_same_backup_when_git_rebases()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.ConfigAsync("pull.rebase", "true");
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal("Pulled and rebased main onto origin/main", app.StatusText);
        Assert.Equal(local, Assert.Single(await repo.BackupRefsAsync()).Sha);

        // A pull that only fast-forwards saves none.
        using var behind = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(behind, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work");
        using var other = await OperationsHarness.OpenAsync(behind);
        await other.Operations.PullRebaseAsync();
        Assert.Equal("Pulled 1 commit into main (fast-forward)", other.StatusText);
        Assert.Empty(await behind.BackupRefsAsync());
    }

    [Fact]
    public async Task A_rebase_that_git_s_fork_point_shortened_is_a_success_said_in_the_warning_colour()
    {
        // Section 6, 10.2: topic's upstream is the local main, which once held "Kept for a while"
        // (topic has it) and was reset back over it; git's fork point leaves it out.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "Initial commit");
        await repo.CommitFileAsync("x.txt", "x\n", "Kept for a while");
        await repo.CreateBranchAsync("topic");
        await repo.CheckoutAsync("topic");
        await repo.GitAsync("branch", "--set-upstream-to=main");
        await repo.CommitFileAsync("t.txt", "t\n", "Topic work");
        await repo.CheckoutAsync("main");
        await repo.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
        await repo.CommitFileAsync("m.txt", "m\n", "Main later work");
        await repo.CheckoutAsync("topic");
        using var app = await OperationsHarness.OpenAsync(repo);
        var finished = new List<(GitOperation Operation, OperationResult Result)>();
        app.Repository.Queue.Finished += (_, e) => finished.Add((e.Operation, e.Result));

        await app.Operations.PullRebaseAsync();

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(
            ("Pulled and rebased topic onto main; git left out 1 commit that main once had", OperationStatusTone.Warning),
            (app.StatusText, app.StatusTone));
        Assert.Equal(["Topic work", "Main later work", "Initial commit"], (await repo.GitAsync("log", "--format=%s")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));

        // Not a conflict: a success with the warning colour, logged as one, which auto-fetch counts as a fetch.
        var (operation, result) = Assert.Single(finished);
        Assert.Equal((OperationOutcome.Succeeded, true), (result.Outcome, result.IsWarning));
        Assert.True(AutoFetchScheduler.Fetched(operation, result));
        Assert.Contains(app.Log.Messages, message => Regex.IsMatch(message, @": Pull \(rebase\): Succeeded after \d+ ms: Pulled and rebased topic onto main; git left out 1 commit that main once had$"));
    }

    [Fact]
    public async Task A_pull_with_rebase_and_a_plain_pull_are_never_merged_while_one_waits()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var (hold, ahead) = HoldQueue(app);

        var plain = app.Operations.PullAsync();
        var rebase = app.Operations.PullRebaseAsync();
        var again = app.Operations.PullRebaseAsync();
        hold.SetResult();
        await ahead;
        await Task.WhenAll(plain, rebase, again);

        // pull:<ref> and pull:<ref>:rebase (D147): one of each ran; the second chosen rebase pull joined the first.
        Assert.Equal(1, Ran(app, "Pull"));
        Assert.Equal(1, Ran(app, "Pull (rebase)"));
        Assert.True(AutoFetchScheduler.Fetched(
            new GitOperation(OperationTexts.PullRebaseTitle, string.Empty, OperationKind.Network, RefreshScope.Refs, _ => Task.FromResult(OperationReport.Done(string.Empty))),
            new OperationResult(OperationOutcome.Succeeded, string.Empty, null)));
    }
}
