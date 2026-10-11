using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Phase 3's branch operations against real git on the scenario repos (design 2.1 to 2.10, D93,
/// D94): checkouts, create, rename, delete, the fast-forward of a branch that is not checked out
/// (with the linked worktree that refuses it), merges with git's own messages, and the reads they
/// rely on. Expected states are read back with plain git; a merge's message is compared with the
/// one <c>git merge &lt;short name&gt;</c> writes in a second copy.
/// </summary>
public class BranchOperationTests
{
    // ---- Reads ------------------------------------------------------------------------------------

    [Fact]
    public async Task Reads_resolve_names_count_commits_and_tell_ancestors()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        Assert.Equal(mainWork, await repository.ResolveAsync("refs/heads/main", TestCancelled));
        Assert.Null(await repository.ResolveAsync("refs/heads/no-such-branch", TestCancelled));

        // An annotated tag resolves to the tag object, not its commit.
        Assert.Equal(await RevAsync(repo, "refs/tags/v1.0"), await repository.ResolveAsync("refs/tags/v1.0", TestCancelled));
        Assert.Equal("tag", await GitTextAsync(repo, "cat-file", "-t", (await repository.ResolveAsync("refs/tags/v1.0", TestCancelled))!));

        Assert.True(await repository.IsAncestorAsync("refs/heads/main", "refs/heads/feature/ff", TestCancelled));
        Assert.True(await repository.IsAncestorAsync("refs/heads/main", "refs/heads/main", TestCancelled));
        Assert.False(await repository.IsAncestorAsync("refs/heads/feature/alpha", "refs/heads/main", TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.IsAncestorAsync("refs/heads/no-such-branch", "refs/heads/main", TestCancelled));

        Assert.Equal(2, await repository.CountCommitsAsync("refs/heads/main", "refs/heads/feature/alpha", TestCancelled));
        Assert.Equal(1, await repository.CountCommitsAsync("refs/heads/feature/alpha", "refs/heads/main", TestCancelled));
        Assert.Equal(0, await repository.CountCommitsAsync("refs/heads/feature/ff", "refs/heads/main", TestCancelled));
        Assert.Equal(
            int.Parse(await GitTextAsync(repo, "rev-list", "--count", "refs/heads/main..refs/heads/feature/alpha"), System.Globalization.CultureInfo.InvariantCulture),
            await repository.CountCommitsAsync("refs/heads/main", "refs/heads/feature/alpha", TestCancelled));

        // HEAD on a branch leaves nothing behind.
        Assert.Equal(0, await repository.CountDetachedCommitsAsync(TestCancelled));
    }

    [Fact]
    public async Task The_worktrees_are_read_with_the_branch_each_has_checked_out()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);

        var worktrees = await repository.ReadWorktreesAsync(TestCancelled);

        Assert.Equal(2, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.Null(worktrees[0].Branch); // HEAD is detached there.
        AssertSameFolder(repo.Path, worktrees[0].Path);
        Assert.False(worktrees[1].IsMain);
        Assert.Equal("refs/heads/side", worktrees[1].Branch);
        AssertSameFolder(repo.SiblingPath(Scenarios.EdgeCasesWorktree), worktrees[1].Path);
    }

    [Fact]
    public void Worktree_lists_are_parsed_with_bare_and_detached_entries()
    {
        var worktrees = GitRepository.ParseWorktrees(
            "worktree /srv/repo.git\nbare\n\nworktree /srv/one\nHEAD 1111111111111111111111111111111111111111\nbranch refs/heads/feature/x\n\n" +
            "worktree /srv/two\nHEAD 2222222222222222222222222222222222222222\ndetached\nlocked\n\n");

        Assert.Equal(3, worktrees.Count);
        Assert.Equal([true, false, false], worktrees.Select(worktree => worktree.IsMain));
        Assert.Equal([null, "refs/heads/feature/x", null], worktrees.Select(worktree => worktree.Branch));
        Assert.Equal(Path.GetFullPath("/srv/one"), worktrees[1].Path);
    }

    // ---- Checkouts --------------------------------------------------------------------------------

    [Fact]
    public async Task Checking_out_a_local_branch_switches_to_it_and_never_to_a_tag_of_the_same_name()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);

        // The tag feature/alpha (at Main work) and the branch feature/alpha (at Alpha work).
        await repository.CheckoutBranchAsync("feature/alpha", cancellationToken: TestCancelled);
        Assert.Equal("refs/heads/feature/alpha", await GitTextAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/alpha"), await repo.HeadAsync());

        // The local branch origin/main, beside the remote branch of the same short name.
        await repository.CheckoutBranchAsync("origin/main", cancellationToken: TestCancelled);
        Assert.Equal("refs/heads/origin/main", await GitTextAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Equal(await RevAsync(repo, "refs/heads/origin/main"), await repo.HeadAsync());
    }

    [Fact]
    public async Task Checking_out_a_name_that_only_a_remote_branch_has_is_refused_without_guessing()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--delete", "--force", "feature/alpha");
        var repository = await OpenAsync(repo);

        // --no-guess: git would otherwise create feature/alpha from origin/feature/alpha.
        await Assert.ThrowsAsync<GitException>(() => repository.CheckoutBranchAsync("feature/alpha", cancellationToken: TestCancelled));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/alpha"));
    }

    [Fact]
    public async Task Checking_out_a_remote_branch_creates_a_branch_that_tracks_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--delete", "--force", "feature/alpha");
        var repository = await OpenAsync(repo);

        await repository.CheckoutTrackingBranchAsync("feature/alpha", "refs/remotes/origin/feature/alpha", cancellationToken: TestCancelled);

        Assert.Equal("feature/alpha", await repo.CurrentBranchAsync());
        Assert.Equal(await RevAsync(repo, "refs/remotes/origin/feature/alpha"), await repo.HeadAsync());
        Assert.Equal("origin", await GitTextAsync(repo, "config", "branch.feature/alpha.remote"));
        Assert.Equal("refs/heads/feature/alpha", await GitTextAsync(repo, "config", "branch.feature/alpha.merge"));
        Assert.Equal("refs/remotes/origin/feature/alpha", await GitTextAsync(repo, "rev-parse", "--symbolic-full-name", "feature/alpha@{upstream}"));

        // An existing branch of that name is not overwritten.
        await repo.CheckoutAsync("main");
        var error = await Assert.ThrowsAsync<GitException>(
            () => repository.CheckoutTrackingBranchAsync("feature/alpha", "refs/remotes/origin/main", cancellationToken: TestCancelled));
        Assert.Contains("already exists", error.StandardError, StringComparison.Ordinal);
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    [Fact]
    public async Task Checking_out_a_commit_detaches_HEAD_there()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        await repository.CheckoutDetachedAsync(addCore, cancellationToken: TestCancelled);

        Assert.Equal(addCore, await repo.HeadAsync());
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).ExitCode);
        Assert.Equal(0, await repository.CountDetachedCommitsAsync(TestCancelled)); // main reaches it.
        Assert.Null((await repository.ReadRefsAsync(TestCancelled)).Head.BranchName);
    }

    [Fact]
    public async Task Leaving_a_detached_HEAD_counts_its_commits_and_its_backup_keeps_them()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);
        var detachedWork = await repo.HeadAsync();

        // git rev-list --count HEAD --not --branches --remotes --tags, as the design asks it.
        Assert.Equal("1", await GitTextAsync(repo, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes", "--tags"));
        Assert.Equal(1, await repository.CountDetachedCommitsAsync(TestCancelled));

        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("detached", [new BackupEntry(null, detachedWork)]), TestCancelled);
        await repository.CheckoutBranchAsync("main", cancellationToken: TestCancelled);

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(0, await repository.CountDetachedCommitsAsync(TestCancelled));
        var refs = await repo.BackupRefsAsync();
        Assert.Equal([new RefEntry($"refs/visualcommit/backup/detached-{backup.Time}", detachedWork)], refs);
        Assert.Equal(backup.Refs, refs.Select(entry => entry.Name));

        // The commit no branch reaches is gone from the graph's history, and kept by the backup.
        Assert.DoesNotContain(detachedWork, await GitLinesAsync(repo, "rev-list", "--branches", "--remotes", "--tags", "HEAD"));
        Assert.Equal("Detached work", await GitTextAsync(repo, "log", "-1", "--format=%s", backup.Refs[0]));

        // The linked worktree keeps its branch.
        Assert.Equal("side", await GitInAsync(repo, repo.SiblingPath(Scenarios.EdgeCasesWorktree), "symbolic-ref", "--short", "HEAD"));
    }

    [Fact]
    public async Task A_checkout_blocked_by_local_changes_throws_with_gits_lists_and_changes_nothing()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        var repository = await OpenAsync(repo);

        // Only the tracked change is in the way.
        var tracked = await Assert.ThrowsAsync<GitException>(() => repository.CheckoutBranchAsync("feature/other", cancellationToken: TestCancelled));
        Assert.Contains("Your local changes to the following files would be overwritten by checkout", tracked.StandardError, StringComparison.Ordinal);
        Assert.Contains("notes.txt", tracked.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked working tree files", tracked.StandardError, StringComparison.Ordinal);
        Assert.Equal("git switch --no-guess feature/other", tracked.CommandText);

        // Both: git prints both lists in one run (design 7.1: feature/addfile changes notes.txt too).
        var both = await Assert.ThrowsAsync<GitException>(() => repository.CheckoutBranchAsync("feature/addfile", cancellationToken: TestCancelled));
        Assert.Contains("Your local changes to the following files would be overwritten by checkout", both.StandardError, StringComparison.Ordinal);
        Assert.Contains("The following untracked working tree files would be overwritten by checkout", both.StandardError, StringComparison.Ordinal);
        Assert.Contains("newfile.txt", both.StandardError, StringComparison.Ordinal);

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesLocal, ReadText(repo, "notes.txt"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NewFileUntracked, ReadText(repo, "newfile.txt"));
    }

    [Fact]
    public async Task A_checkout_carries_local_changes_along_when_nothing_is_in_the_way()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        var repository = await OpenAsync(repo);
        await repo.GitAsync("checkout", "--", "notes.txt");
        repo.WriteFile("extra.txt", "extra\n");
        await repo.GitAsync("add", "extra.txt");

        // A staged new file and an untracked one that feature/other does not have.
        await repository.CheckoutBranchAsync("feature/other", cancellationToken: TestCancelled);

        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesOther, ReadText(repo, "notes.txt"));
        Assert.Equal(["A  extra.txt", "?? newfile.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    // ---- Create, rename, delete -------------------------------------------------------------------

    [Fact]
    public async Task Creating_a_branch_from_each_kind_of_start_point()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/tags/v1.0^{commit}");

        // A commit, without checkout.
        await repository.CreateBranchAsync("hotfix", mainWork, checkout: false, cancellationToken: TestCancelled);
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/hotfix"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await GitLinesAsync(repo, "config", "--get-regexp", @"^branch\.hotfix\."));

        // A remote branch: git sets it as the upstream (branch.autoSetupMerge).
        await repository.CreateBranchAsync("alpha-copy", "refs/remotes/origin/feature/alpha", checkout: false, cancellationToken: TestCancelled);
        Assert.Equal(await RevAsync(repo, "refs/remotes/origin/feature/alpha"), await repo.RefTargetAsync("refs/heads/alpha-copy"));
        Assert.Equal(["branch.alpha-copy.remote origin", "branch.alpha-copy.merge refs/heads/feature/alpha"], await GitLinesAsync(repo, "config", "--get-regexp", @"^branch\.alpha-copy\."));

        // A tag's commit: no upstream; the branch points at the commit, not the tag object.
        await repository.CreateBranchAsync("release-1.0", "refs/tags/v1.0^{commit}", checkout: false, cancellationToken: TestCancelled);
        Assert.Equal(addCore, await repo.RefTargetAsync("refs/heads/release-1.0"));
        Assert.Empty(await GitLinesAsync(repo, "config", "--get-regexp", @"^branch\.release-1\.0\."));

        // A local branch, with checkout; a name with slashes.
        await repository.CreateBranchAsync("release/2.0/hotfix", "refs/heads/feature/ff", checkout: true, cancellationToken: TestCancelled);
        Assert.Equal("release/2.0/hotfix", await repo.CurrentBranchAsync());
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/ff"), await repo.HeadAsync());
    }

    [Fact]
    public async Task Creating_a_branch_whose_name_clashes_with_a_folder_is_refused_by_git()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        // Git 2.36 says "cannot lock ref" for both folder clashes; the wording is git's to change.
        await Assert.ThrowsAsync<GitException>(() => repository.CreateBranchAsync("feature", mainWork, checkout: false, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.CreateBranchAsync("feature/alpha/x", mainWork, checkout: true, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.CreateBranchAsync("main", mainWork, checkout: false, cancellationToken: TestCancelled));

        Assert.Null(await repo.RefTargetAsync("refs/heads/feature"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/alpha/x"));
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    [Fact]
    public async Task Renaming_the_current_branch_moves_HEAD_and_its_upstream_with_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        await repository.RenameBranchAsync("main", "trunk", TestCancelled);

        Assert.Equal("refs/heads/trunk", await GitTextAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/trunk"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(["branch.trunk.remote origin", "branch.trunk.merge refs/heads/main"], await GitLinesAsync(repo, "config", "--get-regexp", @"^branch\.trunk\."));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Renaming_a_branch_with_an_upstream_into_a_folder_keeps_the_upstream()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");

        await repository.RenameBranchAsync("feature/alpha", "team/alpha/next", TestCancelled);

        Assert.Equal(alphaTwo, await repo.RefTargetAsync("refs/heads/team/alpha/next"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/alpha"));
        Assert.Equal("refs/remotes/origin/feature/alpha", await GitTextAsync(repo, "rev-parse", "--symbolic-full-name", "team/alpha/next@{upstream}"));
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var renamed = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/team/alpha/next");
        Assert.Equal("refs/remotes/origin/feature/alpha", renamed.UpstreamRef);
        Assert.Equal("main", refs.Head.BranchName);

        // Into a name that clashes: git refuses and nothing moves.
        await Assert.ThrowsAsync<GitException>(() => repository.RenameBranchAsync("feature/ff", "main", TestCancelled));
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/feature/ff"));
    }

    [Fact]
    public async Task Deleting_a_branch_after_its_backup_removes_it_and_the_backup_keeps_its_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var fastForwardMe = await RevAsync(repo, "refs/heads/feature/ff");

        // 2.6's question: does HEAD have the branch's commits?
        Assert.False(await repository.IsAncestorAsync("refs/heads/feature/ff", "HEAD", TestCancelled));

        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("feature/ff", "refs/heads/feature/ff")]), TestCancelled);
        await repository.DeleteBranchAsync("feature/ff", TestCancelled);

        Assert.Null(await repo.RefTargetAsync("refs/heads/feature/ff"));
        Assert.Equal([new RefEntry($"refs/visualcommit/backup/branch-{backup.Time}/feature/ff", fastForwardMe)], await repo.BackupRefsAsync());

        // Unmerged: git branch -d would have refused it; --force deletes it.
        Assert.DoesNotContain(fastForwardMe, await GitLinesAsync(repo, "rev-list", "--branches", "--remotes", "--tags"));
    }

    [Fact]
    public async Task Deleting_the_current_branch_or_one_checked_out_in_a_linked_worktree_is_refused_by_git()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);

        var linked = await Assert.ThrowsAsync<GitException>(() => repository.DeleteBranchAsync("side", TestCancelled));
        Assert.Contains("side", linked.StandardError, StringComparison.Ordinal);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/side"));

        await repo.CheckoutAsync("main");
        await Assert.ThrowsAsync<GitException>(() => repository.DeleteBranchAsync("main", TestCancelled));
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/main"));
    }

    // ---- Fast-forward of a branch that is not checked out ----------------------------------------

    [Fact]
    public async Task A_branch_that_is_not_checked_out_is_fast_forwarded_without_touching_the_working_tree()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var head = await repo.HeadAsync();

        // loc's upstream is the local branch main (behind 1).
        await repository.FastForwardBranchAsync("loc", "refs/heads/main", TestCancelled);
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/loc"));

        // To a remote branch, into a local branch named like it.
        await repository.FastForwardBranchAsync("origin/main", "refs/remotes/origin/main", TestCancelled);
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/origin/main"));

        Assert.Equal(head, await repo.HeadAsync());
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "FETCH_HEAD")));
    }

    [Fact]
    public async Task A_fast_forward_is_refused_for_a_branch_checked_out_in_a_linked_worktree_and_for_a_non_fast_forward()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);
        var initial = await RevAsync(repo, "refs/heads/side");
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var worktree = repo.SiblingPath(Scenarios.EdgeCasesWorktree);

        // side is behind main, but checked out in edges-wt: git fetch . refuses it (update-ref would
        // move it, and that worktree would then show the commits as staged reverse changes).
        var linked = await Assert.ThrowsAsync<GitException>(() => repository.FastForwardBranchAsync("side", "refs/heads/main", TestCancelled));
        Assert.Contains("refusing to fetch into branch", linked.StandardError, StringComparison.Ordinal);
        Assert.Equal(initial, await repo.RefTargetAsync("refs/heads/side"));
        Assert.Equal(string.Empty, await GitInAsync(repo, worktree, "status", "--porcelain"));

        // main is not checked out anywhere (HEAD is detached), but side is behind it, not ahead.
        var behind = await Assert.ThrowsAsync<GitException>(() => repository.FastForwardBranchAsync("main", "refs/heads/side", TestCancelled));
        Assert.Contains("non-fast-forward", behind.StandardError, StringComparison.Ordinal);
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/main"));
    }

    [Fact]
    public async Task The_checked_out_branch_cannot_be_fast_forwarded_this_way()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--force", "feature/alpha", "refs/heads/main~1");
        await repo.CheckoutAsync("feature/alpha");
        var repository = await OpenAsync(repo);
        var before = await repo.HeadAsync();

        await Assert.ThrowsAsync<GitException>(() => repository.FastForwardBranchAsync("feature/alpha", "refs/heads/main", TestCancelled));
        Assert.Equal(before, await repo.HeadAsync());
    }

    // ---- Merge ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_merge_fast_forwards_when_it_can()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var fastForwardMe = await RevAsync(repo, "refs/heads/feature/ff");

        Assert.Equal(MergeResult.FastForward, await repository.MergeAsync("refs/heads/feature/ff", MergeMode.Default, cancellationToken: TestCancelled));

        Assert.Equal(fastForwardMe, await repo.HeadAsync());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.StartsWith("merge refs/heads/feature/ff: Fast-forward", await GitTextAsync(repo, "reflog", "-1", "--format=%gs", "refs/heads/main"), StringComparison.Ordinal);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_merge_commit_gets_the_message_git_writes_for_the_short_name()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var reference = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var fastForwardMe = await RevAsync(repo, "refs/heads/feature/ff");

        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/ff", MergeMode.NoFastForward, cancellationToken: TestCancelled));

        // What git writes for "git merge --no-ff feature/ff", made in the other copy.
        await reference.GitAsync("merge", "--no-edit", "--no-ff", "feature/ff");
        Assert.Equal(await GitTextAsync(reference, "log", "-1", "--format=%B"), await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal("Merge branch 'feature/ff'", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal($"{mainWork} {fastForwardMe}", await GitTextAsync(repo, "log", "-1", "--format=%P"));
        Assert.Equal(["A\tdocs/ff.txt"], await GitLinesAsync(repo, "diff", "--name-status", "HEAD^1", "HEAD"));
        Assert.Equal("Test Author <author@example.com> 2026-01-02T09:00:00+00:00", await GitTextAsync(repo, "log", "-1", "--format=%an <%ae> %aI"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Merging_a_remote_branch_and_merging_into_another_branch_keep_gits_wording()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var reference = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaTwo = await RevAsync(repo, "refs/remotes/origin/feature/alpha");

        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/remotes/origin/feature/alpha", MergeMode.Default, cancellationToken: TestCancelled));
        await reference.GitAsync("merge", "--no-edit", "origin/feature/alpha");
        Assert.Equal("Merge remote-tracking branch 'origin/feature/alpha'", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(await GitTextAsync(reference, "log", "-1", "--format=%B"), await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(alphaTwo, await GitTextAsync(repo, "rev-parse", "HEAD^2"));

        // Already merged: nothing to do.
        var merge = await repo.HeadAsync();
        Assert.Equal(MergeResult.UpToDate, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.Default, cancellationToken: TestCancelled));
        Assert.Equal(MergeResult.UpToDate, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.NoFastForward, cancellationToken: TestCancelled));
        Assert.Equal(merge, await repo.HeadAsync());

        // From another branch, git adds " into <branch>".
        await repo.CheckoutAsync("feature/ff");
        await reference.CheckoutAsync("feature/ff");
        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.Default, cancellationToken: TestCancelled));
        await reference.GitAsync("merge", "--no-edit", "feature/alpha");
        Assert.Equal(await GitTextAsync(reference, "log", "-1", "--format=%B"), await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal("Merge branch 'feature/alpha' into feature/ff", await GitTextAsync(repo, "log", "-1", "--format=%B"));
    }

    [Fact]
    public async Task A_merge_takes_the_branch_and_not_a_tag_of_the_same_name()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("main");
        var repository = await OpenAsync(repo);
        var alphaWork = await RevAsync(repo, "refs/heads/feature/alpha");
        var mainWork = await RevAsync(repo, "refs/heads/main");
        Assert.Equal(mainWork, await RevAsync(repo, "refs/tags/feature/alpha")); // The tag is at main: merging it would do nothing.

        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.NoFastForward, cancellationToken: TestCancelled));

        Assert.Equal("Merge branch 'feature/alpha'", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal($"{mainWork} {alphaWork}", await GitTextAsync(repo, "log", "-1", "--format=%P"));
    }

    [Fact]
    public async Task A_merge_follows_merge_ff_as_configured_and_fast_forward_only_refuses_a_merge()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        var refused = await Assert.ThrowsAsync<GitException>(
            () => repository.MergeAsync("refs/heads/feature/conflict", MergeMode.FastForwardOnly, cancellationToken: TestCancelled));
        Assert.Contains("Not possible to fast-forward", refused.StandardError, StringComparison.Ordinal);
        Assert.Equal(mainWork, await repo.HeadAsync());

        await repo.ConfigAsync("merge.ff", "false");
        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/ff", MergeMode.Default, cancellationToken: TestCancelled));
        Assert.Equal("Merge branch 'feature/ff'", await GitTextAsync(repo, "log", "-1", "--format=%B"));

        await repo.GitAsync("reset", "--hard", "--quiet", mainWork);
        await repo.ConfigAsync("merge.ff", "true");
        Assert.Equal(MergeResult.FastForward, await repository.MergeAsync("refs/heads/feature/ff", MergeMode.FastForwardOnly, cancellationToken: TestCancelled));
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/ff"), await repo.HeadAsync());
    }

    [Fact]
    public async Task A_merge_with_merge_log_set_gets_gits_shortlog_once()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var reference = await Scenarios.OperationsAsync();
        await repo.ConfigAsync("merge.log", "true");
        await reference.ConfigAsync("merge.log", "true");
        var repository = await OpenAsync(repo);

        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.Default, cancellationToken: TestCancelled));
        await reference.GitAsync("merge", "--no-edit", "feature/alpha");

        var message = await GitTextAsync(repo, "log", "-1", "--format=%B");
        Assert.Equal(await GitTextAsync(reference, "log", "-1", "--format=%B"), message);
        Assert.StartsWith("Merge branch 'feature/alpha'\n\n* feature/alpha:\n", message.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Single(message.Split('\n'), line => line.Contains("Alpha two", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_merge_that_stops_with_a_conflict_returns_it_and_leaves_gits_merge_state()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        Assert.Equal(MergeResult.Conflicts, await repository.MergeAsync("refs/heads/feature/conflict", MergeMode.Default, cancellationToken: TestCancelled));

        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(conflicting, ReadGitFile(repo, "MERGE_HEAD").Trim());
        Assert.Equal("Merge branch 'feature/conflict'\n\n# Conflicts:\n#\tsrc/core.txt\n", ReadGitFile(repo, "MERGE_MSG"));

        // Git names the ref as the app passed it in the conflict marker.
        Assert.Equal("core\n<<<<<<< HEAD\nmain work\n=======\nconflict\n>>>>>>> refs/heads/feature/conflict\n", ReadText(repo, "src/core.txt"));

        var status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(InProgressKind.Merge, status.InProgress.Kind);
        Assert.Equal("Merge branch 'feature/conflict'", status.InProgress.Subject);
    }

    [Fact]
    public async Task A_merge_refused_by_local_changes_throws_instead_of_reporting_conflicts()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nlocal\n");
        var repository = await OpenAsync(repo);

        var error = await Assert.ThrowsAsync<GitException>(
            () => repository.MergeAsync("refs/heads/feature/conflict", MergeMode.Default, cancellationToken: TestCancelled));

        Assert.Contains("would be overwritten by merge", error.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }
}
