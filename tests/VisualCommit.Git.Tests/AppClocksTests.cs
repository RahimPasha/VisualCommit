using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The clocks and numbers of phase 5's records (docs/phase-5-design.md 2.3, D173): the frozen record
/// clock, the fixed backup clock that names each backup one second after the last, the environment
/// variables that ask for them, and the counter that orders the records by start.
/// </summary>
public class AppClocksTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_frozen_clock_always_says_its_time_and_no_time_passes()
    {
        var clock = new FrozenClock(Nine);
        var start = clock.GetTimestamp();

        Assert.Equal(Nine, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetElapsedTime(start));
        Assert.Equal(Nine, AppClocks.HarnessTime);
    }

    [Fact]
    public void The_fixed_backup_clock_names_each_backup_a_second_after_the_last_and_keeps_its_now()
    {
        var clock = BackupClock.Fixed(Nine);

        Assert.Equal(Nine.AddSeconds(1), clock.NextName());
        Assert.Equal(Nine.AddSeconds(2), clock.NextName());
        Assert.Equal(Nine, clock.Now);
        Assert.Equal(Nine.AddSeconds(3), clock.NextName());
    }

    [Fact]
    public async Task A_repository_with_the_fixed_clock_names_its_backups_in_order_the_same_in_every_run()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        repository.BackupClock = BackupClock.Fixed(Nine);
        var main = (await repo.GitAsync("rev-parse", "refs/heads/main")).StandardOutput.Trim();

        var first = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, main)]), TestCancelled);
        var second = await repository.SaveBackupRefsAsync(new BackupRequest("branch", [new BackupEntry("main", main)]), TestCancelled);

        Assert.Equal("20260102T090001000Z", first.Time);
        Assert.Equal("20260102T090002000Z", second.Time);
    }

    [Fact]
    public void The_environment_asks_for_the_harness_clocks_or_gets_the_systems()
    {
        var record = Environment.GetEnvironmentVariable(AppClocks.RecordClockVariable);
        var backup = Environment.GetEnvironmentVariable(AppClocks.BackupClockVariable);
        try
        {
            Environment.SetEnvironmentVariable(AppClocks.RecordClockVariable, null);
            Environment.SetEnvironmentVariable(AppClocks.BackupClockVariable, null);
            Assert.Same(TimeProvider.System, AppClocks.RecordClockFromEnvironment());
            Assert.Same(BackupClock.System, AppClocks.BackupClockFromEnvironment());

            Environment.SetEnvironmentVariable(AppClocks.RecordClockVariable, "frozen");
            Environment.SetEnvironmentVariable(AppClocks.BackupClockVariable, "fixed");
            Assert.Equal(Nine, AppClocks.RecordClockFromEnvironment().GetUtcNow());
            var fixedClock = AppClocks.BackupClockFromEnvironment();
            Assert.Equal((Nine, Nine.AddSeconds(1)), (fixedClock.Now, fixedClock.NextName()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppClocks.RecordClockVariable, record);
            Environment.SetEnvironmentVariable(AppClocks.BackupClockVariable, backup);
        }
    }

    [Fact]
    public async Task Record_numbers_increase_on_every_thread()
    {
        var taken = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Enumerable.Range(0, 1000).Select(_ => RecordSequence.Next()).ToList(), TestCancelled)));

        var all = taken.SelectMany(numbers => numbers).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.All(taken, numbers => Assert.True(numbers.SequenceEqual(numbers.Order()), "A thread's numbers do not increase."));
        Assert.True(RecordSequence.Next() > all.Max());
    }

    [Fact]
    public void A_worktrees_key_is_its_full_git_folder_without_a_trailing_separator()
    {
        var folder = Path.Combine(Path.GetTempPath(), "repo", ".git");

        Assert.Equal(Path.GetFullPath(folder), WorktreeKey.Of(folder + Path.DirectorySeparatorChar));
        Assert.True(WorktreeKey.Comparer.Equals(WorktreeKey.Of(folder), WorktreeKey.Of(Path.Combine(folder, "..", ".git"))));
        Assert.Equal(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), WorktreeKey.Comparer.Equals("A", "a"));
    }
}
