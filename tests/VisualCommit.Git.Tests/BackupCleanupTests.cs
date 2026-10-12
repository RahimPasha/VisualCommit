using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The backups' clean-up (docs/phase-4-design.md section 9, D139) with a fixed backup clock: a
/// backup is the refs that share kind, time and number; those older than 30 days go, then the
/// oldest beyond the count; nothing goes while any worktree has an operation in progress, nor the
/// restore bar's snapshot (<c>keep</c>), nor a ref that fits no scheme; one transaction that a ref
/// changed since the read makes delete nothing; one log line per backup removed.
/// </summary>
public class BackupCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Backups_older_than_30_days_are_removed_with_all_their_refs()
    {
        using var repo = await RepoAsync();
        var old = Stamp(Now.AddDays(-31));
        var justOld = Stamp(Now.AddDays(-30).AddMilliseconds(-1));
        var thirtyDays = Stamp(Now.AddDays(-30));
        var recent = Stamp(Now.AddDays(-29));
        await CreateRefsAsync(repo, $"reset-{old}", $"reset-{old}-changes", $"discard-{justOld}", $"stash-{thirtyDays}", $"branch-{recent}/feature/x");
        var log = new ListLog();
        var repository = await OpenAtNowAsync(repo, log);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        Assert.Equal(
            [
                ($"reset-{old}", $"reset-{old} reset-{old}-changes", BackupRemovalReason.OlderThanDays),
                ($"discard-{justOld}", $"discard-{justOld}", BackupRemovalReason.OlderThanDays),
            ],
            removed.Select(backup => (backup.Name, string.Join(' ', backup.Refs.Select(Short).Order(StringComparer.Ordinal)), backup.Reason)));
        Assert.Equal(Refs($"branch-{recent}/feature/x", $"stash-{thirtyDays}"), await BackupNamesAsync(repo));
        Assert.Equal(
            [
                $"{repository.Name}: Removed backup reset-{old} (older than 30 days)",
                $"{repository.Name}: Removed backup discard-{justOld} (older than 30 days)",
            ],
            log.Entries.Where(entry => entry.Level == LogLevel.Info && entry.Message.Contains("Removed backup", StringComparison.Ordinal)).Select(entry => entry.Message));
    }

    [Fact]
    public async Task Beyond_50_backups_the_oldest_go_and_a_backup_of_several_refs_counts_once()
    {
        using var repo = await RepoAsync();

        // 52 backups within the 30 days, the oldest of them a removed remote's four refs.
        var names = new List<string>();
        var oldest = Stamp(Now.AddDays(-20));
        names.AddRange(new[] { "origin/a", "origin/b", "origin/c", "origin/d" }.Select(branch => $"remote-{oldest}/{branch}"));
        for (var i = 1; i <= 51; i++)
        {
            names.Add($"tag-{Stamp(Now.AddDays(-20).AddMinutes(i))}/v{i}");
        }

        await CreateRefsAsync(repo, [.. names]);
        var log = new ListLog();
        var repository = await OpenAtNowAsync(repo, log);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        var second = $"tag-{Stamp(Now.AddDays(-20).AddMinutes(1))}";
        Assert.Equal([$"remote-{oldest}", second], removed.Select(backup => backup.Name));
        Assert.All(removed, backup => Assert.Equal(BackupRemovalReason.BeyondCount, backup.Reason));
        Assert.Equal(4, removed[0].Refs.Count);
        Assert.Equal(50, (await BackupNamesAsync(repo)).Length);
        Assert.Contains($"{repository.Name}: Removed backup {second} (more than 50 backups)", log.Entries.Select(entry => entry.Message));
    }

    [Fact]
    public async Task Kinds_with_hyphens_numbers_changes_and_named_refs_are_grouped_by_kind_time_and_number()
    {
        using var repo = await RepoAsync();
        var time = Stamp(Now.AddDays(-40));
        string[] refs =
        [
            $"force-push-{time}/origin/main",
            $"remote-branch-{time}/origin/feature/x",
            $"remote-tag-{time}/origin/v1.0",
            $"reset-{time}",
            $"reset-{time}-changes",
            $"reset-{time}-2",
            $"reset-{time}-2-changes",
            $"rebase-{time}/main",
            $"skip-{time}",
            $"skip-{time}-changes",
            $"abort-{time}-3",
            $"detached-{time}",
        ];
        await CreateRefsAsync(repo, refs);
        var repository = await OpenAtNowAsync(repo);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        // Of one millisecond, oldest first: by number (none is 1), then by kind.
        Assert.Equal(
            [
                $"detached-{time}",
                $"force-push-{time}",
                $"rebase-{time}",
                $"remote-branch-{time}",
                $"remote-tag-{time}",
                $"reset-{time}",
                $"skip-{time}",
                $"reset-{time}-2",
                $"abort-{time}-3",
            ],
            removed.Select(backup => backup.Name));
        Assert.Equal(Refs($"remote-branch-{time}/origin/feature/x"), removed.Single(backup => backup.Name == $"remote-branch-{time}").Refs.Select(Short));
        Assert.Equal(Refs($"reset-{time}", $"reset-{time}-changes"), removed.Single(backup => backup.Name == $"reset-{time}").Refs.Select(Short).Order(StringComparer.Ordinal));
        Assert.Equal(Refs($"reset-{time}-2", $"reset-{time}-2-changes"), removed.Single(backup => backup.Name == $"reset-{time}-2").Refs.Select(Short).Order(StringComparer.Ordinal));
        Assert.Equal(Refs($"skip-{time}", $"skip-{time}-changes"), removed.Single(backup => backup.Name == $"skip-{time}").Refs.Select(Short).Order(StringComparer.Ordinal));
        Assert.Empty(await BackupNamesAsync(repo));
    }

    [Fact]
    public async Task Discard_snapshots_are_named_by_the_backup_clock_and_age_with_the_other_backups()
    {
        using var repo = await RepoAsync();
        var repository = await OpenAtNowAsync(repo);
        repo.WriteFile("README.md", "changed\n");
        repository.BackupClock = new FixedClock(Now.AddDays(-45));
        var old = await repository.SaveSnapshotAsync(["README.md"], TestCancelled);
        repository.BackupClock = new FixedClock(Now.AddDays(-1));
        var recent = await repository.SaveSnapshotAsync(["README.md"], TestCancelled);
        var again = await repository.SaveSnapshotAsync(["README.md"], TestCancelled);
        repository.BackupClock = new FixedClock(Now);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        Assert.Equal($"refs/visualcommit/backup/discard-{Stamp(Now.AddDays(-45))}", old.Ref);
        Assert.Equal($"refs/visualcommit/backup/discard-{Stamp(Now.AddDays(-1))}", recent.Ref);
        Assert.Equal($"refs/visualcommit/backup/discard-{Stamp(Now.AddDays(-1))}-2", again.Ref);
        var gone = Assert.Single(removed);
        Assert.Equal(($"discard-{Stamp(Now.AddDays(-45))}", BackupRemovalReason.OlderThanDays), (gone.Name, gone.Reason));
        Assert.Equal([old.Ref], gone.Refs);
        Assert.Equal([recent.Ref, again.Ref], (await repo.BackupRefsAsync()).Select(entry => entry.Name));
    }

    [Fact]
    public async Task The_restore_bars_snapshot_is_kept_and_still_counts()
    {
        using var repo = await RepoAsync();
        var kept = Stamp(Now.AddDays(-60));
        var older = Stamp(Now.AddDays(-10));
        var newer = Stamp(Now.AddDays(-5));
        await CreateRefsAsync(repo, $"discard-{kept}", $"stash-{older}", $"stash-{newer}");
        var repository = await OpenAtNowAsync(repo);

        var removed = await repository.CleanUpBackupsAsync(
            new BackupRetention(30, 2),
            new HashSet<string>(StringComparer.Ordinal) { $"refs/visualcommit/backup/discard-{kept}" },
            cancellationToken: TestCancelled);

        // Too old, but the restore bar offers it; with it, three backups: the oldest other one goes.
        Assert.Equal([($"stash-{older}", BackupRemovalReason.BeyondCount)], removed.Select(backup => (backup.Name, backup.Reason)));
        Assert.Equal(Refs($"discard-{kept}", $"stash-{newer}"), await BackupNamesAsync(repo));
    }

    [Fact]
    public async Task Refs_that_fit_no_scheme_are_never_removed_nor_counted()
    {
        using var repo = await RepoAsync();
        var old = Stamp(Now.AddDays(-90));
        string[] unknown =
        [
            "notes",
            $"Reset-{old}",
            $"reset_{old}",
            $"reset-{old[..^2]}Z",
            "reset-20261341T120000000Z",
            $"reset-{old}-changes/extra",
            $"reset-{old}-x",
            $"9reset-{old}",
            $"-{old}",
        ];
        await CreateRefsAsync(repo, [.. unknown, $"stash-{Stamp(Now.AddDays(-1))}"]);
        var repository = await OpenAtNowAsync(repo);

        var removed = await repository.CleanUpBackupsAsync(new BackupRetention(0, 0), NoKeep, cancellationToken: TestCancelled);

        // Even with nothing to keep, only the backup that fits a scheme goes.
        Assert.Equal([$"stash-{Stamp(Now.AddDays(-1))}"], removed.Select(backup => backup.Name));
        Assert.Equal(Refs(unknown).Order(StringComparer.Ordinal), await BackupNamesAsync(repo));
    }

    [Fact]
    public async Task A_time_later_than_now_counts_as_now()
    {
        using var repo = await RepoAsync();
        var future = Stamp(Now.AddDays(10));
        var hourAgo = Stamp(Now.AddHours(-1));
        var old = Stamp(Now.AddDays(-40));
        await CreateRefsAsync(repo, $"tag-{future}/v2", $"tag-{hourAgo}/v1", $"tag-{old}/v0");
        var repository = await OpenAtNowAsync(repo);

        var removed = await repository.CleanUpBackupsAsync(new BackupRetention(30, 1), NoKeep, cancellationToken: TestCancelled);

        // The future one is the newest (now), not ten days ahead: it stays, the hour-old one goes.
        Assert.Equal(
            [($"tag-{old}", BackupRemovalReason.OlderThanDays), ($"tag-{hourAgo}", BackupRemovalReason.BeyondCount)],
            removed.Select(backup => (backup.Name, backup.Reason)));
        Assert.Equal(Refs($"tag-{future}/v2"), await BackupNamesAsync(repo));
    }

    [Fact]
    public async Task A_ref_changed_after_the_read_makes_the_transaction_delete_nothing()
    {
        using var repo = await RepoAsync();
        var first = Stamp(Now.AddDays(-40));
        var second = Stamp(Now.AddDays(-35));
        await CreateRefsAsync(repo, $"reset-{first}", $"reset-{first}-changes", $"stash-{second}");
        var log = new ListLog();
        var repository = await OpenAtNowAsync(repo, log);
        var other = await RevAsync(repo, "HEAD~1");
        repository.AfterBackupRefsRead = () => repo.GitAsync("update-ref", $"refs/visualcommit/backup/reset-{first}-changes", other);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        Assert.Empty(removed);
        Assert.Equal(Refs($"reset-{first}", $"reset-{first}-changes", $"stash-{second}"), await BackupNamesAsync(repo));
        Assert.Equal(other, await repo.RefTargetAsync($"refs/visualcommit/backup/reset-{first}-changes"));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("deleted nothing", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("Removed backup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nothing_to_remove_makes_no_write()
    {
        using var repo = await RepoAsync();
        await CreateRefsAsync(repo, $"stash-{Stamp(Now.AddDays(-1))}");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        repository.BackupClock = new FixedClock(Now);

        Assert.Empty(await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled));
        Assert.Empty(await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled));

        Assert.DoesNotContain(calls.Snapshot(), call => call.CommandText.Contains("update-ref", StringComparison.Ordinal));
        Assert.Single(await BackupNamesAsync(repo));
    }

    [Theory]
    [InlineData(Scenarios.InProgressKind.Merge)]
    [InlineData(Scenarios.InProgressKind.CherryPick)]
    [InlineData(Scenarios.InProgressKind.Revert)]
    [InlineData(Scenarios.InProgressKind.Rebase)]
    public async Task An_operation_in_progress_makes_the_whole_clean_up_wait(Scenarios.InProgressKind kind)
    {
        using var repo = await Scenarios.StoppedAsync(kind);
        var old = Stamp(Now.AddDays(-40));
        await CreateRefsAsync(repo, $"abort-{old}", $"rebase-{old}/feature/conflict");
        var log = new ListLog();
        var repository = await OpenAtNowAsync(repo, log);

        var removed = await repository.CleanUpBackupsAsync(new BackupRetention(30, 0), NoKeep, cancellationToken: TestCancelled);

        Assert.Empty(removed);
        Assert.Equal(2, (await BackupNamesAsync(repo)).Length);
        var waits = Assert.Single(log.Entries, entry => entry.Message.Contains("clean-up waits", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, waits.Level);
        Assert.StartsWith($"{repository.Name}: The backups' clean-up waits: ", waits.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pick_stopped_in_a_linked_worktree_makes_it_wait_and_once_ended_it_runs()
    {
        using var repo = await RepoAsync();
        var linked = Sibling(repo, "linked tree");
        await StopPickInLinkedWorktreeAsync(repo, linked);
        var old = Stamp(Now.AddDays(-40));
        await CreateRefsAsync(repo, $"reset-{old}");
        var log = new ListLog();
        var repository = await OpenAtNowAsync(repo, log);

        Assert.Empty(await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("a cherry-pick is in progress in", StringComparison.Ordinal));

        await GitInAsync(repo, linked, "cherry-pick", "--abort");
        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        Assert.Equal([$"reset-{old}"], removed.Select(backup => backup.Name));
    }

    [Fact]
    public async Task Opened_in_a_linked_worktree_it_waits_for_a_merge_in_the_main_one()
    {
        using var repo = await RepoAsync();
        var linked = Sibling(repo, "linked");
        await repo.GitAsync("worktree", "add", "--quiet", "-b", "other", linked, "HEAD");
        await repo.CreateBranchAsync("side", "HEAD~1");
        await repo.CheckoutAsync("side");
        await repo.CommitFileAsync("README.md", "side\n", "Side");
        await repo.CheckoutAsync("main");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "--no-edit", "side")).ExitCode);
        var old = Stamp(Now.AddDays(-40));
        await CreateRefsAsync(repo, $"reset-{old}");
        var log = new ListLog();
        var repository = await GitRepository.OpenAsync(NewRunner(), linked, TestCancelled, log);
        repository.BackupClock = new FixedClock(Now);

        var removed = await repository.CleanUpBackupsAsync(BackupRetention.Default, NoKeep, cancellationToken: TestCancelled);

        Assert.Empty(removed);
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("a merge is in progress in", StringComparison.Ordinal));
        Assert.Single(await BackupNamesAsync(repo));
    }

    private static readonly IReadOnlySet<string> NoKeep = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>A repository with two commits on main (README.md), its backups to be written by the test.</summary>
    private static async Task<TempRepo> RepoAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("README.md", "first\n", "First");
            await repo.CommitFileAsync("README.md", "second\n", "Second");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<GitRepository> OpenAtNowAsync(TempRepo repo, ListLog? log = null)
    {
        var repository = await GitRepository.OpenAsync(NewRunner(), repo.Path, TestCancelled, log);
        repository.BackupClock = new FixedClock(Now);
        return repository;
    }

    /// <summary>Writes the backup refs <paramref name="names"/> (after the prefix), all at HEAD, in one transaction.</summary>
    private static async Task CreateRefsAsync(TempRepo repo, params string[] names)
    {
        var head = await repo.HeadAsync();
        var input = new StringBuilder();
        foreach (var name in names)
        {
            input.Append(CultureInfo.InvariantCulture, $"create refs/visualcommit/backup/{name} {head}\n");
        }

        await repo.GitWithInputAsync(input.ToString(), "update-ref", "--stdin");
    }

    /// <summary>The backup refs that exist, after the prefix, in git's order.</summary>
    private static async Task<string[]> BackupNamesAsync(TempRepo repo) =>
        [.. (await repo.BackupRefsAsync()).Select(entry => entry.Name["refs/visualcommit/backup/".Length..])];

    private static string[] Refs(params string[] names) => names;

    /// <summary>A backup ref's name after the prefix.</summary>
    private static string Short(string refName) => refName["refs/visualcommit/backup/".Length..];

    private static string Stamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds the linked worktree <paramref name="folder"/> on a new branch from main's first
    /// commit and stops there a single cherry-pick of main's second commit, which conflicts.
    /// </summary>
    private static async Task StopPickInLinkedWorktreeAsync(TempRepo repo, string folder)
    {
        await repo.GitAsync("worktree", "add", "--quiet", "-b", "other", folder, "HEAD~1");
        var second = await RevAsync(repo, "HEAD");
        File.WriteAllText(Path.Combine(folder, "README.md"), "other\n");
        await GitInAsync(repo, folder, "commit", "--quiet", "--all", "--message", "Other");
        var pick = await repo.Runner.RunAsync(new GitCommand("cherry-pick", second) { WorkingDirectory = folder, Environment = repo.Environment });
        Assert.Equal(1, pick.ExitCode);

        // In the linked worktree's own git folder (<common>/worktrees/<its name>/), and only there.
        Assert.True(File.Exists(Path.Combine(await GitInAsync(repo, folder, "rev-parse", "--absolute-git-dir"), "CHERRY_PICK_HEAD")));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
