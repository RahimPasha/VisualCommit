using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// docs/phase-5-design.md 7.3's sequences for checkouts and branches, each action made by the git
/// call the app makes and recorded as the app records it: a checkout of a branch, of a tag, of a
/// remote branch into a new tracking branch, the automatic stash's checkout (9.2, D166), D91's
/// "Stash changes and check out", a branch created with and without its checkout, renamed,
/// deleted with its upstream, fast-forwarded, an interactive rebase, a squash, a pull and a merge
/// finished after its conflict. Each is undone to the state before it and redone to the state after.
/// </summary>
public class UndoStepBranchTests
{
    [Fact]
    public async Task A_checkout_of_a_branch_and_of_a_tag_switch_back_carrying_work()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.TagAsync("v-old", "HEAD~1");

        // A staged change to a file both commits have alike, and an untracked file: carried both ways.
        repo.WriteFile("a.txt", "a-staged\n").WriteFile("scratch.txt", "scratch\n");
        await repo.GitAsync("add", "a.txt");
        var start = await PictureAsync(repo);
        var branch = await RecordAsync(repository, UndoScope.Checkout("feature", "main"), () => repository.CheckoutBranchAsync("feature", cancellationToken: TestCancelled));
        var onFeature = await PictureAsync(repo);
        branch = await UndoAsync(repository, branch);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal("refs/heads/feature", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "@{-1}")).StandardOutput.Trim());
        await RedoAsync(repository, branch);
        Assert.Equal(onFeature, await PictureAsync(repo));

        var tag = await RecordAsync(repository, UndoScope.Checkout("v-old", "feature"), () => repository.CheckoutDetachedAsync("refs/tags/v-old", cancellationToken: TestCancelled));
        var detached = await PictureAsync(repo);
        tag = await UndoAsync(repository, tag);
        Assert.Equal(onFeature, await PictureAsync(repo));
        await RedoAsync(repository, tag);
        Assert.Equal(detached, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_remote_branch_checked_out_into_a_tracking_branch_is_undone_with_the_branch_and_its_keys()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("branch", "-D", "feature");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Checkout("origin/feature", "main", "feature"), () => repository.CheckoutTrackingBranchAsync("feature", "refs/remotes/origin/feature", cancellationToken: TestCancelled));
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
            await repository.CheckoutDetachedAsync(target, cancellationToken: TestCancelled);
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
            await repository.CheckoutBranchAsync("feature", cancellationToken: TestCancelled);
            Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task Branches_created_renamed_deleted_and_fast_forwarded_come_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        // Created from a remote branch, which sets its upstream (branch.autoSetupMerge), without its checkout.
        var created = await RecordAsync(repository, UndoScope.CreateBranch("tracked", "main"), () => repository.CreateBranchAsync("tracked", "refs/remotes/origin/feature", checkout: false, cancellationToken: TestCancelled));
        Assert.Contains(created.After.Config, key => key.Key == "branch.tracked.remote");
        var withTracked = await PictureAsync(repo);
        created = await UndoAsync(repository, created);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, created);
        Assert.Equal(withTracked, await PictureAsync(repo));

        // Created and checked out.
        var checkedOut = await RecordAsync(repository, UndoScope.CreateBranch("topic", "main"), () => repository.CreateBranchAsync("topic", "HEAD~1", checkout: true, cancellationToken: TestCancelled));
        var onTopic = await PictureAsync(repo);
        checkedOut = await UndoAsync(repository, checkedOut);
        Assert.Equal(withTracked, await PictureAsync(repo));
        await RedoAsync(repository, checkedOut);
        Assert.Equal(onTopic, await PictureAsync(repo));

        // Renamed, the checked-out branch and its keys with it.
        var renamed = await RecordAsync(repository, UndoScope.RenameBranch("topic", "topic2"), () => repository.RenameBranchAsync("topic", "topic2", TestCancelled));
        var afterRename = await PictureAsync(repo);
        renamed = await UndoAsync(repository, renamed);
        Assert.Equal(onTopic, await PictureAsync(repo));
        await RedoAsync(repository, renamed);
        Assert.Equal(afterRename, await PictureAsync(repo));

        // Deleted with every key of its: they come back with it, and a reflog of its own.
        await repo.GitAsync("config", "branch.feature.description", "The feature");
        var beforeDelete = await PictureAsync(repo);
        var deleted = await RecordAsync(repository, UndoScope.DeleteBranch("feature"), () => repository.DeleteBranchAsync("feature", TestCancelled));
        var afterDelete = await PictureAsync(repo);
        deleted = await UndoAsync(repository, deleted);
        Assert.Equal(beforeDelete, await PictureAsync(repo));
        Assert.Contains("VisualCommit: undo deletion of feature", (await repo.GitAsync("log", "-g", "--format=%gs", "refs/heads/feature")).StandardOutput, StringComparison.Ordinal);
        await RedoAsync(repository, deleted);
        Assert.Equal(afterDelete, await PictureAsync(repo));

        // A branch that is not checked out, fast-forwarded.
        var main = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();
        var fastForward = await RecordAsync(repository, UndoScope.FastForwardBranch("behind"), () => repository.FastForwardBranchAsync("behind", main, TestCancelled));
        var forwarded = await PictureAsync(repo);
        fastForward = await UndoAsync(repository, fastForward);
        Assert.Equal(afterDelete, await PictureAsync(repo));
        await RedoAsync(repository, fastForward);
        Assert.Equal(forwarded, await PictureAsync(repo));
    }

    [Fact]
    public async Task An_interactive_rebase_and_a_squash_are_undone_and_redone()
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

        var basis = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var pair = await repository.ReadRebaseListAsync(basis, "refs/heads/feature", fromCommit: true, cancellationToken: TestCancelled);
        var squashPlan = RebaseTestSupport.Plan(pair, "refs/heads/feature", (RebaseAction.Pick, null), (RebaseAction.Fixup, null));
        var squash = await RecordAsync(repository, UndoScope.Squash(2, "feature"), () => repository.InteractiveRebaseAsync(squashPlan, cancellationToken: TestCancelled));
        var squashed = await PictureAsync(repo);
        squash = await UndoAsync(repository, squash);
        Assert.Equal(rebased, await PictureAsync(repo));
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
    public async Task A_merge_finished_after_its_conflicts_is_undone_in_one_step()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CommitFileAsync("src/c.txt", "c-main\n", "c4");
        var start = await PictureAsync(repo);

        // The pending entry's state before, completed by the commit that ends the merge (7.2).
        var scope = UndoScope.Merge("refs/heads/feature", "main");
        var before = await repository.ReadRefStatesAsync(scope, cancellationToken: TestCancelled);
        Assert.Equal(MergeResult.Conflicts, await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled));
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
