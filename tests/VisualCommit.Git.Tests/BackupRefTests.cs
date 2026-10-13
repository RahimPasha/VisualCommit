using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Phase 3's backup refs (design 2.0, D83): one timestamp and one <c>git update-ref --stdin</c>
/// transaction per operation, the retry naming on the kind-and-time part, and the snapshots that
/// join a batch (a hard reset's over untracked and staged-new files, an abort's). What each ref
/// holds is read back with plain git.
/// </summary>
public partial class BackupRefTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 10, 11, 10, 15, 0, 123, TimeSpan.Zero);

    [Fact]
    public async Task One_batch_writes_every_ref_under_one_timestamp_in_the_documented_names()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var before = DateTimeOffset.UtcNow;

        var result = await repository.SaveBackupRefsAsync(
            new BackupRequest("remote", [new BackupEntry("origin/main", "refs/remotes/origin/main"), new BackupEntry("origin/feature/alpha", "refs/remotes/origin/feature/alpha")]),
            TestCancelled);

        // D67's format, in UTC: yyyyMMdd'T'HHmmssfff'Z'.
        Assert.Matches(TimePattern(), result.Time);
        var time = DateTimeOffset.ParseExact(result.Time, "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        Assert.InRange(time, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(
            [$"refs/visualcommit/backup/remote-{result.Time}/origin/main", $"refs/visualcommit/backup/remote-{result.Time}/origin/feature/alpha"],
            result.Refs);
        Assert.Equal(
            [
                new RefEntry($"refs/visualcommit/backup/remote-{result.Time}/origin/feature/alpha", await RevAsync(repo, "refs/remotes/origin/feature/alpha")),
                new RefEntry($"refs/visualcommit/backup/remote-{result.Time}/origin/main", await RevAsync(repo, "refs/remotes/origin/main")),
            ],
            await repo.BackupRefsAsync());

        // Outside --branches --remotes --tags: the graph's history does not show them (D47).
        Assert.DoesNotContain(await GitLinesAsync(repo, "for-each-ref", "--format=%(refname)", "refs/heads", "refs/remotes", "refs/tags"), name => name.Contains("visualcommit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_tag_is_backed_up_as_its_tag_object_and_a_stash_as_its_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        var tag = await repository.SaveBackupRefsAsync(new BackupRequest("tag", [new BackupEntry("v1.0", "refs/tags/v1.0")]), TestCancelled);
        var stash = await repository.SaveBackupRefsAsync(new BackupRequest("stash", [new BackupEntry(null, await RevAsync(repo, "refs/stash"))]), TestCancelled);

        // The annotated tag keeps its message; the stash keeps its index parent for git stash store.
        Assert.Equal("tag", await GitTextAsync(repo, "cat-file", "-t", tag.Refs[0]));
        Assert.Equal(await repo.RefTargetAsync("refs/tags/v1.0"), await repo.RefTargetAsync(tag.Refs[0]));
        Assert.Contains("Version 1.0", await GitTextAsync(repo, "cat-file", "-p", tag.Refs[0]), StringComparison.Ordinal);
        Assert.Equal(await RevAsync(repo, "refs/stash"), await repo.RefTargetAsync(stash.Refs[0]));
        Assert.Equal(2, (await GitTextAsync(repo, "log", "-1", "--format=%P", stash.Refs[0])).Split(' ').Length);
    }

    [Fact]
    public async Task A_collision_retries_the_whole_batch_with_a_suffix_on_the_kind_and_time()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        repository.BackupClock = new FixedClock(FixedTime);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var changes = await RevAsync(repo, "refs/stash");
        var request = new BackupRequest("reset", [new BackupEntry(null, mainWork)]) { ChangesCommit = changes };

        var first = await repository.SaveBackupRefsAsync(request, TestCancelled);
        var second = await repository.SaveBackupRefsAsync(request, TestCancelled);
        var third = await repository.SaveBackupRefsAsync(request, TestCancelled);

        Assert.Equal("20261011T101500123Z", first.Time);
        Assert.Equal("20261011T101500123Z-2", second.Time);
        Assert.Equal("20261011T101500123Z-3", third.Time);
        Assert.Equal(["refs/visualcommit/backup/reset-20261011T101500123Z-2", "refs/visualcommit/backup/reset-20261011T101500123Z-2-changes"], second.Refs);
        Assert.Equal(6, (await repo.BackupRefsAsync()).Count);
        Assert.Equal(changes, await repo.RefTargetAsync("refs/visualcommit/backup/reset-20261011T101500123Z-3-changes"));
    }

    [Fact]
    public async Task A_name_with_slashes_never_clashes_with_a_folder_of_the_same_name()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        repository.BackupClock = new FixedClock(FixedTime);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        // branch-<time>/feature exists, so branch-<time>/feature/alpha cannot (a ref is not a
        // folder); the retry puts its suffix before the name, not at the end of it.
        var folder = await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("feature", mainWork)]), TestCancelled);
        var below = await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("feature/alpha", "refs/heads/feature/alpha")]), TestCancelled);

        Assert.Equal(["refs/visualcommit/backup/branch-20261011T101500123Z/feature"], folder.Refs);
        Assert.Equal(["refs/visualcommit/backup/branch-20261011T101500123Z-2/feature/alpha"], below.Refs);
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/alpha"), await repo.RefTargetAsync(below.Refs[0]));
    }

    [Fact]
    public async Task Names_that_differ_only_in_case_get_a_number_so_that_the_batch_can_be_written()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        repository.BackupClock = new FixedClock(FixedTime);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var alpha = await RevAsync(repo, "refs/heads/feature/alpha");

        // A server can have Foo and foo; where refs are files the two loose refs would be one file.
        var result = await repository.SaveBackupRefsAsync(
            new BackupRequest("remote", [new BackupEntry("origin/Foo", mainWork), new BackupEntry("origin/foo", alpha), new BackupEntry("origin/FOO/x", mainWork)]),
            TestCancelled);

        const string Stem = "refs/visualcommit/backup/remote-20261011T101500123Z";
        Assert.Equal([$"{Stem}/origin/Foo", $"{Stem}/origin/foo-2", $"{Stem}/origin/FOO-3/x"], result.Refs);
        Assert.Equal(mainWork, await repo.RefTargetAsync($"{Stem}/origin/Foo"));
        Assert.Equal(alpha, await repo.RefTargetAsync($"{Stem}/origin/foo-2"));
        Assert.Equal(mainWork, await repo.RefTargetAsync($"{Stem}/origin/FOO-3/x"));
    }

    [Fact]
    public async Task A_batch_that_fails_for_another_reason_writes_nothing_and_throws_gits_text()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        // The transaction writes all of its refs or none.
        var error = await Assert.ThrowsAsync<GitException>(() => repository.SaveBackupRefsAsync(
            new BackupRequest("remote", [new BackupEntry("origin/main", "refs/remotes/origin/main"), new BackupEntry("origin/gone", "refs/remotes/origin/gone")]),
            TestCancelled));
        Assert.Contains("refs/remotes/origin/gone", error.StandardError, StringComparison.Ordinal);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_request_that_cannot_be_written_as_refs_is_refused_before_git_runs()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveBackupRefsAsync(new BackupRequest("reset", []), TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveBackupRefsAsync(new BackupRequest("Reset/x", [new BackupEntry(null, head)]), TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry(null, head), new BackupEntry("x", head)]), TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("x", head), new BackupEntry("x", head)]), TestCancelled));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    // ---- The hard reset's snapshot (2.11) ----------------------------------------------------------

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_an_untracked_file_that_the_target_tracks()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");
        repo.WriteFile("src/alpha.txt", "mine\n").WriteFile("todo.txt", "todo\n");

        var changes = await repository.SnapshotForHardResetAsync(alphaTwo, TestCancelled);
        Assert.NotNull(changes);
        await repository.ResetAsync(alphaTwo, ResetMode.Hard, cancellationToken: TestCancelled);

        // Git overwrote it without a word; the snapshot has it as it was. todo.txt, which the
        // target does not track, is neither touched nor saved.
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, ReadText(repo, "src/alpha.txt"));
        Assert.Equal("todo\n", ReadText(repo, "todo.txt"));
        Assert.Equal("mine\n", await ShowFileAsync(repo, changes, "src/alpha.txt"));
        Assert.DoesNotContain("todo.txt", await TreeFilesAsync(repo, changes));
        Assert.Contains("src/alpha.txt", await GitTextAsync(repo, "log", "-1", "--format=%b", changes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_an_ignored_file_that_the_target_tracks()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");
        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "src/alpha.txt\n");
        repo.WriteFile("src/alpha.txt", "ignored, mine\n");
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));

        var changes = await repository.SnapshotForHardResetAsync(alphaTwo, TestCancelled);
        Assert.NotNull(changes);
        await repository.ResetAsync(alphaTwo, ResetMode.Hard, cancellationToken: TestCancelled);

        // git status does not list it, and git reset --hard overwrites it all the same.
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, ReadText(repo, "src/alpha.txt"));
        Assert.Equal("ignored, mine\n", await ShowFileAsync(repo, changes, "src/alpha.txt"));
    }

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_a_staged_new_file_and_both_versions_of_a_staged_and_edited_file()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        repo.WriteFile("src/new.txt", "new\n");
        await repo.GitAsync("add", "src/new.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged\nedited\n");
        await repo.GitAsync("rm", "--quiet", "README.md");

        var changes = await repository.SnapshotForHardResetAsync(mainWork, TestCancelled);
        Assert.NotNull(changes);
        await repository.ResetAsync(mainWork, ResetMode.Hard, cancellationToken: TestCancelled);

        // Git deleted the new file and brought the others back.
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "new.txt")));
        Assert.Equal(Scenarios.OperationsFiles.Readme, ReadText(repo, "README.md"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));

        // The snapshot: the working tree (README.md deleted), then the index as its second parent.
        Assert.Equal(["src/core.txt", "src/new.txt"], await TreeFilesAsync(repo, changes));
        Assert.Equal("core\nmain work\nstaged\nedited\n", await ShowFileAsync(repo, changes, "src/core.txt"));
        Assert.Equal("new\n", await ShowFileAsync(repo, changes, "src/new.txt"));
        var index = await RevAsync(repo, changes + "^2");
        Assert.Equal(["src/core.txt", "src/new.txt"], await TreeFilesAsync(repo, index));
        Assert.Equal("core\nmain work\nstaged\n", await ShowFileAsync(repo, index, "src/core.txt"));
        Assert.Equal(mainWork, await RevAsync(repo, changes + "^1"));
        Assert.Equal(mainWork, await RevAsync(repo, index + "^1"));
        Assert.Equal("VisualCommit <snapshot@visualcommit.invalid>", await GitTextAsync(repo, "log", "-1", "--format=%an <%ae>", changes));
    }

    [Fact]
    public async Task A_hard_reset_snapshot_with_a_conflict_has_no_index_parent()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var repository = await OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var conflicted = ReadText(repo, "src/core.txt");

        var changes = await repository.SnapshotForHardResetAsync(mainWork, TestCancelled);

        Assert.NotNull(changes);
        Assert.Equal(mainWork, await GitTextAsync(repo, "log", "-1", "--format=%P", changes));
        Assert.Equal(conflicted, await ShowFileAsync(repo, changes, "src/core.txt"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain")); // The real index is not touched.
    }

    [Fact]
    public async Task Taking_a_snapshot_leaves_the_real_index_alone()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged\nedited\n");
        var index = File.ReadAllBytes(Path.Combine(repo.Path, ".git", "index"));

        Assert.NotNull(await repository.SnapshotForHardResetAsync(await RevAsync(repo, "refs/heads/main~1"), TestCancelled));
        Assert.NotNull(await repository.SnapshotTrackedChangesAsync(TestCancelled));

        Assert.Equal(index, File.ReadAllBytes(Path.Combine(repo.Path, ".git", "index")));
        Assert.Equal(["MM src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Empty(await repo.BackupRefsAsync()); // A snapshot gets its ref only in the operation's batch.
    }

    // ---- The mixed reset's snapshot of the index (2.11) ---------------------------------------------

    [Fact]
    public async Task A_mixed_reset_snapshot_keeps_a_staged_version_that_the_working_tree_no_longer_has()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var addCore = await RevAsync(repo, "refs/heads/main~1");
        Assert.Null(await repository.SnapshotIndexAsync(TestCancelled)); // Nothing staged: nothing to keep.
        repo.WriteFile("src/core.txt", "v2, staged\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "v3, in the working tree\n");

        var index = await repository.SnapshotIndexAsync(TestCancelled);
        Assert.NotNull(index);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, mainWork)]) { ChangesCommit = index }, TestCancelled);
        await repository.ResetAsync(addCore, ResetMode.Mixed, cancellationToken: TestCancelled);

        // The new index is the target's; the staged version lives on in the -changes ref.
        Assert.Equal(" M src/core.txt", Assert.Single(await GitLinesAsync(repo, "status", "--porcelain")));
        Assert.Equal("v2, staged\n", await ShowFileAsync(repo, backup.Refs[1], "src/core.txt"));
        Assert.Equal(mainWork, await GitTextAsync(repo, "log", "-1", "--format=%P", backup.Refs[1]));
        Assert.Equal("VisualCommit: index before a mixed reset", await GitTextAsync(repo, "log", "-1", "--format=%s", backup.Refs[1]));
    }

    // ---- The abort's snapshot (2.15) ----------------------------------------------------------------

    [Fact]
    public async Task An_abort_snapshot_holds_the_changed_tracked_files_and_not_the_untracked_ones()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);
        var repository = await OpenAsync(repo);
        var coreAgain = await repo.HeadAsync();
        repo.WriteFile("README.md", "# Operations\n\nEdited while reverting.\n").WriteFile("notes.txt", "untracked\n");

        var changes = await repository.SnapshotTrackedChangesAsync(TestCancelled);

        Assert.NotNull(changes);
        Assert.Equal(coreAgain, await GitTextAsync(repo, "log", "-1", "--format=%P", changes));
        Assert.Equal(["README.md", "src/core.txt"], await TreeFilesAsync(repo, changes));
        Assert.Equal("# Operations\n\nEdited while reverting.\n", await ShowFileAsync(repo, changes, "README.md"));
        Assert.Equal(ReadText(repo, "src/core.txt"), await ShowFileAsync(repo, changes, "src/core.txt"));
        Assert.Equal("VisualCommit: snapshot before an abort", await GitTextAsync(repo, "log", "-1", "--format=%s", changes));
    }

    [Fact]
    public async Task Without_changed_tracked_files_there_is_no_snapshot()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("todo.txt", "todo\n");
        var repository = await OpenAsync(repo);

        Assert.Null(await repository.SnapshotTrackedChangesAsync(TestCancelled));
        Assert.Null(await repository.SnapshotForHardResetAsync(await repo.HeadAsync(), TestCancelled));
    }

    [GeneratedRegex(@"^\d{8}T\d{9}Z$")]
    private static partial Regex TimePattern();

    /// <summary>A backup clock that always says <paramref name="now"/>: for naming, so that two batches collide, and for the clean-up.</summary>
    private sealed class FixedClock(DateTimeOffset now) : BackupClock(TimeProvider.System)
    {
        public override DateTimeOffset Now => now;

        public override DateTimeOffset NextName() => now;
    }
}
