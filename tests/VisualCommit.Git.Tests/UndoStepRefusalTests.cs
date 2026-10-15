using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Recorded actions whose undo or redo meets a repository that changed since (docs/phase-5-design.md
/// 7.4 to 7.6): stale stat data is no reason, two changed files in the way are counted by the app,
/// an ignored file in the way refuses an undo and a redo, a remote's lease refuses, a branch
/// checked out in another worktree refuses, a redo whose move refuses after its switch puts
/// the switch back, and a stash is not popped onto a commit other than the one it was made on (a
/// stash's, the automatic stash's and D91's undo). A refused step leaves HEAD, the refs, the
/// index's entries, the files and HEAD's reflog as they were (the switch put back aside, which
/// writes its two entries).
/// </summary>
public class UndoStepRefusalTests
{
    [Fact]
    public async Task Stale_stat_data_is_no_reason_and_two_changed_files_are_counted()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var merge = await RecordAsync(repository, UndoScope.Merge("refs/heads/feature", "main"), () => repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled));

        // The undo changes src/c.txt and removes f.txt: both edited since.
        repo.WriteFile("src/c.txt", "c-edited\n").WriteFile("f.txt", "f-edited\n");
        var still = await StillAsync(repo);
        var refusal = await RefusedAsync(repository, merge, UndoDirection.Undo);
        Assert.Equal(UndoRefusalKind.LocalChangesInTheWay, refusal.Kind);
        Assert.Equal(["f.txt", "src/c.txt"], refusal.Paths);
        Assert.Equal(still, await StillAsync(repo));

        // The same bytes written back, with new times: the stat data alone differs from the index's.
        await repo.GitAsync("checkout", "--quiet", "--", "src/c.txt", "f.txt");
        foreach (var path in new[] { "src/c.txt", "f.txt" })
        {
            var full = Path.Combine(repo.Path, path);
            File.WriteAllBytes(full, File.ReadAllBytes(full));
            File.SetLastWriteTimeUtc(full, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        await UndoAsync(repository, merge);
        Assert.False(File.Exists(Path.Combine(repo.Path, "f.txt")));
    }

    [Fact]
    public async Task An_ignored_file_where_the_undo_or_the_redo_adds_one_refuses_them_and_stays()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);

        // The undo of a revert that removed g.txt brings it back where an ignored g.txt now is.
        var added = await repo.CommitFileAsync("g.txt", "tracked\n", "add g");
        File.WriteAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "g.txt\nf.txt\n");
        var revert = await RecordAsync(repository, UndoScope.Revert([added], "main"), () => repository.RevertAsync(added, isMerge: false, cancellationToken: TestCancelled));
        repo.WriteFile("g.txt", "ignored, mine\n");
        var still = await StillAsync(repo);
        var undo = await RefusedAsync(repository, revert, UndoDirection.Undo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.IgnoredFilesInTheWay, Paths: undo.Paths), undo);
        Assert.Equal(["g.txt"], undo.Paths);
        Assert.Equal("ignored, mine\n", ReadText(repo, "g.txt"));
        Assert.Equal(still, await StillAsync(repo));
        File.Delete(Path.Combine(repo.Path, "g.txt"));

        // The redo of an undone merge brings f.txt back where an ignored f.txt now is.
        var merge = await RecordAsync(repository, UndoScope.Merge("refs/heads/feature", "main"), () => repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled));
        merge = await UndoAsync(repository, merge);
        repo.WriteFile("f.txt", "ignored, mine\n");
        still = await StillAsync(repo);
        var redo = await RefusedAsync(repository, merge, UndoDirection.Redo);
        Assert.Equal(UndoRefusalKind.IgnoredFilesInTheWay, redo.Kind);
        Assert.Equal(["f.txt"], redo.Paths);
        Assert.Equal("ignored, mine\n", ReadText(repo, "f.txt"));
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task A_push_whose_branch_moved_on_the_remote_since_is_refused_by_its_lease()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CommitFileAsync("p.txt", "pushed\n", "p1");
        var push = await RecordAsync(repository, UndoScope.Push("main", "origin", force: false), async () =>
            UndoCapture.Push("origin", await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main"), NetworkOptions.None, TestCancelled)));

        var theirs = await Scenarios.PushFromElsewhereAsync(repo, "main", "o.txt", "other\n", "Theirs");
        var refusal = await RefusedAsync(repository, push, UndoDirection.Undo);
        Assert.Equal((UndoRefusalKind.RemoteChanged, "main", "origin"), (refusal.Kind, refusal.Name, refusal.Remote));
        Assert.Contains("(stale info)", refusal.GitOutput, StringComparison.Ordinal);
        Assert.Contains(theirs, (await repo.GitAsync("ls-remote", repo.SiblingPath("origin.git"), "refs/heads/main")).StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_branch_checked_out_in_another_worktree_is_not_moved()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var main = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();
        var fastForward = await RecordAsync(repository, UndoScope.FastForwardBranch("behind"), () => repository.FastForwardBranchAsync("behind", main, TestCancelled));
        var folder = repo.SiblingPath("behind-worktree");
        await repo.GitAsync("worktree", "add", "--quiet", folder, "behind");
        var still = await StillAsync(repo);

        var refusal = await RefusedAsync(repository, fastForward, UndoDirection.Undo);
        Assert.Equal((UndoRefusalKind.CheckedOutElsewhere, "behind"), (refusal.Kind, refusal.Name));
        AssertSameFolder(folder, Assert.Single(refusal.Paths!));
        Assert.Equal(still, await StillAsync(repo));
        Assert.Equal(main, await TargetAsync(repo, "refs/heads/behind"));
    }

    [Fact]
    public async Task A_redo_of_check_out_and_merge_whose_move_refuses_switches_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var entry = await RecordAsync(repository, UndoScope.CheckoutAndMerge("behind", "refs/heads/feature", "main"), async () =>
        {
            await repository.CheckoutBranchAsync("behind", cancellationToken: TestCancelled);
            await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled);
        });
        entry = await UndoAsync(repository, entry);

        // src/c.txt is alike on main and behind, and the merge changes it.
        repo.WriteFile("src/c.txt", "c-edited\n");
        var start = await PictureAsync(repo);
        var refusal = await RefusedAsync(repository, entry, UndoDirection.Redo);
        Assert.Equal(UndoRefusalKind.LocalChangesInTheWay, refusal.Kind);
        Assert.Equal(["src/c.txt"], refusal.Paths);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    [Fact]
    public async Task A_stash_is_not_popped_back_onto_a_commit_made_since()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("u.txt", "untracked\n");
        var stash = await RecordAsync(repository, UndoScope.Stash("wip"), async () =>
        {
            var made = await repository.StashAsync("wip", includeUntracked: true, cancellationToken: TestCancelled);
            UndoCapture.Stash(made, includedUntracked: true, "wip");
        });

        // A terminal commits another a.txt: the status is still the clean one recorded, but the
        // pop would conflict there and leave its untracked file behind (0.5).
        var moved = await repo.CommitFileAsync("a.txt", "a-other\n", "c4");
        var start = await PictureAsync(repo);
        var still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: moved), await RefusedAsync(repository, stash, UndoDirection.Undo));
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(still, await StillAsync(repo));
        Assert.False(File.Exists(Path.Combine(repo.Path, "u.txt")));
    }

    [Fact]
    public async Task The_automatic_stashs_checkout_is_not_undone_onto_a_branch_moved_since()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var feature = (await repo.GitAsync("rev-parse", "feature")).StandardOutput.Trim();
        repo.WriteFile("b.txt", "b-unstaged\n").WriteFile("notes/u.txt", "untracked\n");
        var target = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var message = $"Auto stash before checking out {target[..7]}";
        var auto = await RecordAsync(repository, UndoScope.AutoStashCheckout(target[..7], "main"), async () =>
        {
            var made = await repository.StashAsync(message, includeUntracked: true, cancellationToken: TestCancelled);
            UndoCapture.Stash(made, includedUntracked: true, message);
            await repository.CheckoutDetachedAsync(target, cancellationToken: TestCancelled);
        });

        // main moved to feature's tip, whose b.txt differs from the one the stash was made on.
        await repo.GitAsync("branch", "--force", "main", "feature");
        var start = await PictureAsync(repo);
        var still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: feature), await RefusedAsync(repository, auto, UndoDirection.Undo));
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task Stash_changes_and_check_out_is_not_undone_onto_a_branch_moved_since()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var feature = (await repo.GitAsync("rev-parse", "feature")).StandardOutput.Trim();
        repo.WriteFile("a.txt", "a-unstaged\n");
        var entry = await RecordAsync(repository, UndoScope.StashAndCheckout("behind", "main"), async () =>
        {
            var made = await repository.StashAsync("Stash before checking out behind", includeUntracked: false, cancellationToken: TestCancelled);
            await repository.CheckoutBranchAsync("behind", cancellationToken: TestCancelled);
            Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(made!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
        });

        // The undo stashes, switches back to main and pops there: main moved, so it is refused
        // before the stash, which the pop would otherwise merge onto feature's tip.
        await repo.GitAsync("branch", "--force", "main", "feature");
        var start = await PictureAsync(repo);
        var still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: feature), await RefusedAsync(repository, entry, UndoDirection.Undo));
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(still, await StillAsync(repo));
    }

    /// <summary>What a refused step must leave as it was (7.5): every ref (the backups too), the index's entries, the files, the stash list and HEAD's reflog.</summary>
    private static async Task<(RepoPicture Picture, string Refs, string Reflog)> StillAsync(TempRepo repo) =>
        (await PictureAsync(repo) with { Status = string.Empty },
            (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput,
            await HeadReflogAsync(repo));
}
