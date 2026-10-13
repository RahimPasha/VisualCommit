using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadBackupsAsync"/> (docs/phase-5-design.md 5.3, D139): the backups the
/// clean-up has left, newest first, grouped by phase 4's pattern, each ref with what it holds and
/// the commit it keeps.
/// </summary>
public class BackupReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lists_each_backup_newest_first_with_its_refs_and_their_commits()
    {
        using var repo = await TempRepo.CreateAsync("backups");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");
        await repo.AnnotatedTagAsync("v1", "Release one\n\nNotes.", first);
        repo.WriteFile("f.txt", "work\n");
        var stash = await repo.StashAsync("Some work");
        var tree = (await repo.GitAsync("rev-parse", "HEAD^{tree}")).StandardOutput.Trim();
        var snapshot = (await repo.GitWithInputAsync("VisualCommit: index before a mixed reset\n\nf.txt\ng.txt\n", "commit-tree", tree, "-p", second)).StandardOutput.Trim();
        var repository = await OpenAsync(repo);
        repository.BackupClock = BackupClock.Fixed(Now);

        await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, second)]) { ChangesCommit = snapshot }, TestCancelled);
        await repository.SaveBackupRefsAsync(new BackupRequest("remote", [new BackupEntry("origin/b", second), new BackupEntry("origin/a", first)]), TestCancelled);
        await repository.SaveBackupRefsAsync(new BackupRequest("tag", [new BackupEntry("v1", "refs/tags/v1")]), TestCancelled);
        await repository.SaveBackupRefsAsync(new BackupRequest("stash", [new BackupEntry(null, stash)]), TestCancelled);

        var backups = await repository.ReadBackupsAsync(TestCancelled);

        Assert.Equal(
            [
                ("stash-20260102T090004000Z", "stash", Now.AddSeconds(4)),
                ("tag-20260102T090003000Z", "tag", Now.AddSeconds(3)),
                ("remote-20260102T090002000Z", "remote", Now.AddSeconds(2)),
                ("reset-20260102T090001000Z", "reset", Now.AddSeconds(1)),
            ],
            backups.Select(backup => (backup.Name, backup.Kind, backup.Time)));
        Assert.All(backups, backup => Assert.Null(backup.Number));

        const string Prefix = GitRepository.BackupRefPrefix;
        var stashRef = Assert.Single(backups[0].Refs);
        Assert.Equal(new BackupRefInfo(Prefix + "stash-20260102T090004000Z", stash, null, false, "On main: Some work", string.Empty, stash, "On main: Some work"), stashRef);

        // A tag object: its own message, and the commit it tags with that commit's subject.
        var tagObject = await repo.RefTargetAsync("refs/tags/v1");
        Assert.Equal(new BackupRefInfo(Prefix + "tag-20260102T090003000Z/v1", tagObject!, "v1", false, "Release one", "Notes.", first, "First"), Assert.Single(backups[1].Refs));

        // Named refs in git's order of their names.
        Assert.Equal(
            [(Prefix + "remote-20260102T090002000Z/origin/a", "origin/a", first, "First"), (Prefix + "remote-20260102T090002000Z/origin/b", "origin/b", second, "Second")],
            backups[2].Refs.Select(info => (info.FullName, info.Name, info.Commit, info.CommitSubject)));
        Assert.Equal("origin/a", backups[2].Main?.Name);

        // The snapshot's subject names its kind and its body lists its paths.
        Assert.Equal(
            [
                new BackupRefInfo(Prefix + "reset-20260102T090001000Z", second, null, false, "Second", string.Empty, second, "Second"),
                new BackupRefInfo(Prefix + "reset-20260102T090001000Z-changes", snapshot, null, true, "VisualCommit: index before a mixed reset", "f.txt\ng.txt", snapshot, "VisualCommit: index before a mixed reset"),
            ],
            backups[3].Refs);
        Assert.Equal(second, backups[3].Main?.Commit);
    }

    [Fact]
    public async Task A_retried_name_carries_its_number_and_orders_after_the_first()
    {
        using var repo = await TempRepo.CreateAsync("backups-retry");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");
        var repository = await OpenAsync(repo);
        repository.BackupClock = new SameTime(Now);

        // The second batch's name is taken: it is written under the next number.
        await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("a", first)]), TestCancelled);
        await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("a", second)]), TestCancelled);

        var backups = await repository.ReadBackupsAsync(TestCancelled);

        Assert.Equal(
            [("branch-20260102T090000000Z-2", (int?)2, second), ("branch-20260102T090000000Z", null, first)],
            backups.Select(backup => (backup.Name, backup.Number, backup.Main?.Commit)));
    }

    [Fact]
    public async Task A_snapshot_comes_after_the_named_refs_it_sorts_before()
    {
        using var repo = await TempRepo.CreateAsync("backups-named-changes");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        const string Stem = GitRepository.BackupRefPrefix + "rebase-20260102T090000000Z";
        await repo.GitAsync("update-ref", Stem + "/feature", first);
        await repo.GitAsync("update-ref", Stem + "-changes", first);
        var repository = await OpenAsync(repo);

        var backup = Assert.Single(await repository.ReadBackupsAsync(TestCancelled));

        Assert.Equal([(Stem + "/feature", false), (Stem + "-changes", true)], backup.Refs.Select(info => (info.FullName, info.IsChanges)));
        Assert.Equal("feature", backup.Main?.Name);
    }

    [Fact]
    public async Task Refs_that_fit_no_scheme_are_left_out_and_no_backups_is_an_empty_list()
    {
        using var repo = await TempRepo.CreateAsync("backups-odd");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var repository = await OpenAsync(repo);
        Assert.Empty(await repository.ReadBackupsAsync(TestCancelled));

        await repo.GitAsync("update-ref", GitRepository.BackupRefPrefix + "junk", first);
        await repo.GitAsync("update-ref", GitRepository.BackupRefPrefix + "reset-20261399T999999999Z", first);
        await repo.GitAsync("update-ref", GitRepository.BackupRefPrefix + "Reset-20260102T090000000Z", first);

        Assert.Empty(await repository.ReadBackupsAsync(TestCancelled));
    }

    [Fact]
    public async Task A_tag_of_a_tag_keeps_the_commit_at_its_end_and_a_tag_of_a_tree_keeps_none()
    {
        // Git 2.36 peels for-each-ref's "*" one step, Git 2.55 to the end: both give the commit.
        using var repo = await TempRepo.CreateAsync("backups-nested");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        await repo.AnnotatedTagAsync("inner", "Inner", first);
        await repo.AnnotatedTagAsync("outer", "Outer", "refs/tags/inner");
        var tree = (await repo.GitAsync("rev-parse", "HEAD^{tree}")).StandardOutput.Trim();
        await repo.AnnotatedTagAsync("of-tree", "Of a tree", tree);
        var repository = await OpenAsync(repo);
        repository.BackupClock = BackupClock.Fixed(Now);
        await repository.SaveBackupRefsAsync(new BackupRequest("tag", [new BackupEntry("outer", "refs/tags/outer"), new BackupEntry("of-tree", "refs/tags/of-tree")]), TestCancelled);

        var refs = Assert.Single(await repository.ReadBackupsAsync(TestCancelled)).Refs;

        Assert.Equal([("of-tree", "Of a tree", string.Empty, string.Empty), ("outer", "Outer", first, "First")], refs.Select(info => (info.Name!, info.Subject, info.Commit, info.CommitSubject)));
    }

    /// <summary>A clock that names every backup with the same time, so that a second batch is retried under the next number.</summary>
    private sealed class SameTime(DateTimeOffset now) : BackupClock(TimeProvider.System)
    {
        public override DateTimeOffset Now => now;

        public override DateTimeOffset NextName() => now;
    }
}
