using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// docs/phase-5-design.md 7.3's sequences for checkouts and branches, each action made by the git
/// call the app makes and recorded as the app records it: a checkout of a branch, of a tag, one
/// that left a detached HEAD's commits, of a remote branch into a new tracking branch, the
/// automatic stash's checkout (9.2, D166), D91's
/// "Stash changes and check out", a branch created with and without its checkout, renamed,
/// deleted with its upstream, fast-forwarded, an interactive rebase, a squash, a pull that
/// fast-forwarded and one that rebased, and a merge finished after its conflict. Each is undone to the state before it and redone to the state after.
/// </summary>
public class UndoStepBranchTests
{
    [Fact]
    public async Task A_checkout_of_a_branch_switches_back_carrying_work()
    {
        using var repo = await UndoRepoAsync();
        var repository = await WithWorkToCarryAsync(repo);
        var start = await PictureAsync(repo);
        var branch = await RecordAsync(repository, UndoScope.Checkout("feature", "main"), () => repository.CheckoutBranchAsync("feature", keepIgnored: false, cancellationToken: TestCancelled));
        var onFeature = await PictureAsync(repo);
        branch = await UndoAsync(repository, branch);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal("refs/heads/feature", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "@{-1}")).StandardOutput.Trim());
        await RedoAsync(repository, branch);
        Assert.Equal(onFeature, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_checkout_of_a_tag_switches_back_carrying_work()
    {
        using var repo = await UndoRepoAsync();
        var repository = await WithWorkToCarryAsync(repo);
        await repository.CheckoutBranchAsync("feature", keepIgnored: false, cancellationToken: TestCancelled);
        var onFeature = await PictureAsync(repo);

        var tag = await RecordAsync(repository, UndoScope.Checkout("v-old", "feature"), () => repository.CheckoutDetachedAsync("refs/tags/v-old", keepIgnored: false, cancellationToken: TestCancelled));
        var detached = await PictureAsync(repo);
        tag = await UndoAsync(repository, tag);
        Assert.Equal(onFeature, await PictureAsync(repo));
        await RedoAsync(repository, tag);
        Assert.Equal(detached, await PictureAsync(repo));
    }

    /// <summary>
    /// Opens <paramref name="repo"/> (the undo repository) after tagging <c>c2</c> <c>v-old</c> and
    /// making the work a checkout carries both ways: a staged change to a file every commit
    /// involved has alike (<c>a.txt</c>), and an untracked file.
    /// </summary>
    private static async Task<GitRepository> WithWorkToCarryAsync(TempRepo repo)
    {
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.TagAsync("v-old", "HEAD~1");
        repo.WriteFile("a.txt", "a-staged\n").WriteFile("scratch.txt", "scratch\n");
        await repo.GitAsync("add", "a.txt");
        return repository;
    }

    [Fact]
    public async Task A_checkout_that_left_a_detached_heads_commits_switches_back_to_them()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("checkout", "--quiet", "--detach", "HEAD~1");
        var left = await repo.CommitFileAsync("d.txt", "detached\n", "d1");
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        // The app saves the commits it leaves behind (phase 3's detached-<time>) before the checkout.
        var entry = await RecordAsync(repository, UndoScope.Checkout("main", null), async () =>
        {
            var backup = await repository.SaveBackupRefsAsync(new BackupRequest("detached", [new BackupEntry(null, left)]), TestCancelled);
            UndoCapture.Backups(backup.Refs);
            await repository.CheckoutBranchAsync("main", keepIgnored: false, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);
        Assert.Single(entry.Captured.Backups);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(left, (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim());
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_remote_branch_checked_out_into_a_tracking_branch_is_undone_with_the_branch_and_its_keys()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("branch", "-D", "feature");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Checkout("origin/feature", "main", "feature"), () => repository.CheckoutTrackingBranchAsync("feature", "refs/remotes/origin/feature", keepIgnored: false, cancellationToken: TestCancelled));
        var done = await PictureAsync(repo);
        Assert.Contains(entry.After.Config, key => key.Key == "branch.feature.merge");

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.Equal("refs/remotes/origin/feature", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "feature@{upstream}")).StandardOutput.Trim());
    }

    [Fact]
    public async Task The_automatic_stashs_checkout_switches_back_and_pops_its_stash_and_is_redone_with_a_new_stash()
    {
        using var repo = await UndoRepoAsync();
        var (repository, runner) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("notes/u.txt", "untracked\n");
        var target = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var message = $"Auto stash before checking out {target[..7]}";
        var start = await PictureAsync(repo);

        // 9.2's steps: the stash with untracked files and the message, then the detached checkout.
        var entry = await RecordAsync(repository, UndoScope.AutoStashCheckout(target[..7], "main"), async () =>
        {
            var stash = await repository.StashAsync(message, includeUntracked: true, cancellationToken: TestCancelled);
            UndoCapture.Stash(stash, includedUntracked: true, message);
            await repository.CheckoutDetachedAsync(target, keepIgnored: false, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);

        runner.Advance();
        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Empty(await StashShasAsync(repo));

        runner.Advance();
        var redone = await RedoAsync(repository, entry);
        Assert.Equal(done with { Stashes = (await PictureAsync(repo)).Stashes }, await PictureAsync(repo));
        Assert.Equal([$"On main: {message}"], (await repo.StashListAsync()).Select(stash => stash.Message));
        Assert.NotEqual(entry.Captured.Stash, redone.Captured.Stash);
        Assert.Equal([redone.Captured.Stash!], await StashShasAsync(repo));

        await UndoAsync(repository, redone);
        Assert.Equal(start, await PictureAsync(repo));
    }

    [Fact]
    public async Task Stash_changes_and_check_out_is_undone_by_stashing_switching_back_and_popping()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);

        // A partly staged file, which the pop with its index gives back as it was, and an
        // untracked file, which D91's stash leaves where it is.
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.StashAndCheckout("feature", "main"), async () =>
        {
            var stash = await repository.StashAsync("Stash before checking out feature", includeUntracked: false, cancellationToken: TestCancelled);
            await repository.CheckoutBranchAsync("feature", keepIgnored: false, cancellationToken: TestCancelled);
            Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_branch_created_from_a_remote_branch_is_deleted_with_its_upstream_keys()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        // Created from a remote branch, which sets its upstream (branch.autoSetupMerge), without its checkout.
        var created = await RecordAsync(repository, UndoScope.CreateBranch("tracked", "main"), () => repository.CreateBranchAsync("tracked", "refs/remotes/origin/feature", checkout: false, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled));
        Assert.Contains(created.After.Config, key => key.Key == "branch.tracked.remote");
        var withTracked = await PictureAsync(repo);
        created = await UndoAsync(repository, created);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, created);
        Assert.Equal(withTracked, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_branch_created_and_checked_out_is_left_and_deleted()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var checkedOut = await RecordAsync(repository, UndoScope.CreateBranch("topic", "main"), () => repository.CreateBranchAsync("topic", "HEAD~1", checkout: true, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled));
        var onTopic = await PictureAsync(repo);
        checkedOut = await UndoAsync(repository, checkedOut);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, checkedOut);
        Assert.Equal(onTopic, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_checked_out_branch_renamed_is_renamed_back_with_its_keys()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("checkout", "--quiet", "feature");
        var start = await PictureAsync(repo);

        var renamed = await RecordAsync(repository, UndoScope.RenameBranch("feature", "feature2"), () => repository.RenameBranchAsync("feature", "feature2", TestCancelled));
        var afterRename = await PictureAsync(repo);
        Assert.Contains("branch.feature2.merge", afterRename.Config, StringComparison.Ordinal);
        renamed = await UndoAsync(repository, renamed);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, renamed);
        Assert.Equal(afterRename, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_deleted_branch_comes_back_with_its_keys_and_a_reflog_of_its_own()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("config", "branch.feature.description", "The feature");
        var start = await PictureAsync(repo);

        var deleted = await RecordAsync(repository, UndoScope.DeleteBranch("feature"), () => repository.DeleteBranchAsync("feature", TestCancelled));
        var afterDelete = await PictureAsync(repo);
        deleted = await UndoAsync(repository, deleted);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Contains("VisualCommit: undo deletion of feature", (await repo.GitAsync("log", "-g", "--format=%gs", "refs/heads/feature")).StandardOutput, StringComparison.Ordinal);
        await RedoAsync(repository, deleted);
        Assert.Equal(afterDelete, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_branch_fast_forwarded_without_its_checkout_is_set_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var main = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();
        var fastForward = await RecordAsync(repository, UndoScope.FastForwardBranch("behind"), () => repository.FastForwardBranchAsync("behind", main, TestCancelled));
        var forwarded = await PictureAsync(repo);
        fastForward = await UndoAsync(repository, fastForward);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, fastForward);
        Assert.Equal(forwarded, await PictureAsync(repo));
    }

    [Fact]
    public async Task An_interactive_rebase_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CheckoutAsync("feature");
        await repo.CommitFileAsync("f.txt", "f2\n", "f3");
        var start = await PictureAsync(repo);
        var main = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();

        var list = await repository.ReadRebaseListAsync(main, "refs/heads/feature", fromCommit: false, cancellationToken: TestCancelled);
        var plan = RebaseTestSupport.Plan(list, "refs/heads/feature", (RebaseAction.Pick, null), (RebaseAction.Drop, null), (RebaseAction.Pick, null));
        var rebase = await RecordAsync(repository, UndoScope.InteractiveRebase("feature", "feature"), () => repository.InteractiveRebaseAsync(plan, cancellationToken: TestCancelled));
        var rebased = await PictureAsync(repo);
        rebase = await UndoAsync(repository, rebase);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, rebase);
        Assert.Equal(rebased, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_squash_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CheckoutAsync("feature");
        await repo.CommitFileAsync("f.txt", "f2\n", "f3");
        var start = await PictureAsync(repo);

        // The last two commits joined into one, as the graph's Squash builds its plan.
        var basis = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var pair = await repository.ReadRebaseListAsync(basis, "refs/heads/feature", fromCommit: true, cancellationToken: TestCancelled);
        var squashPlan = RebaseTestSupport.Plan(pair, "refs/heads/feature", (RebaseAction.Pick, null), (RebaseAction.Fixup, null));
        var squash = await RecordAsync(repository, UndoScope.Squash(2, "feature"), () => repository.InteractiveRebaseAsync(squashPlan, cancellationToken: TestCancelled));
        var squashed = await PictureAsync(repo);
        squash = await UndoAsync(repository, squash);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, squash);
        Assert.Equal(squashed, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_pull_is_undone_leaving_what_it_fetched()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await Scenarios.PushFromElsewhereAsync(repo, "main", "o.txt", "other\n", "From elsewhere");
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);
        var main = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.FullName == "refs/heads/main");

        var entry = await RecordAsync(repository, UndoScope.Pull("main"), () => repository.IntegratePullAsync(main, cancellationToken: TestCancelled));
        var done = await PictureAsync(repo);
        Assert.NotEqual(entry.Before.Head.Sha, entry.After.Head.Sha);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_pull_that_rebased_the_branchs_own_commit_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await Scenarios.PushFromElsewhereAsync(repo, "main", "o.txt", "other\n", "From elsewhere");
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        await repo.CommitFileAsync("mine.txt", "mine\n", "Mine");
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);
        var main = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.FullName == "refs/heads/main");

        // The rebase replays "Mine" onto what was fetched (7.2: a rebase of every kind, a pull's too).
        var entry = await RecordAsync(repository, UndoScope.Pull("main"), () => repository.IntegratePullAsync(main, PullIntegration.Rebase, beforeRebase: null, cancellationToken: TestCancelled));
        var done = await PictureAsync(repo);
        Assert.Equal((await repo.GitAsync("rev-parse", "origin/main")).StandardOutput.Trim(), (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_merge_finished_after_its_conflicts_is_undone_in_one_step()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CommitFileAsync("src/c.txt", "c-main\n", "c4");
        var start = await PictureAsync(repo);

        // The pending entry's state before, completed by the commit that ends the merge (7.2).
        var scope = UndoScope.Merge("refs/heads/feature", "main");
        var before = await repository.ReadRefStatesAsync(scope, cancellationToken: TestCancelled);
        Assert.Equal(MergeResult.Conflicts, await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, keepIgnored: false, cancellationToken: TestCancelled));
        repo.WriteFile("src/c.txt", "c-resolved\n");
        await repository.StageAsync(["src/c.txt"], TestCancelled);
        await repository.CommitAsync("Merge feature", amend: false, TestCancelled);
        var after = await repository.ReadRefStatesAsync(scope, cancellationToken: TestCancelled);
        var entry = new UndoEntry(UndoKind.Merge, scope.Text, "op-1", before, after, UndoCaptured.None) { Scope = scope };
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }
}
