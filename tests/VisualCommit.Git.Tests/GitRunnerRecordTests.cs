using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What a git call's record carries for phase 5's activity log (docs/phase-5-design.md 2.3, D173):
/// the number it took when it started, the record clock's times, the standard input of a command
/// that records it (and of no other), and whether it ran on a temporary index.
/// </summary>
public class GitRunnerRecordTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly string GitPath =
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_record_takes_its_number_at_the_start_and_the_record_clocks_times()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls, NullAppLog.Instance, new FrozenClock(Nine));

        await runner.RunAsync(new GitCommand("--version"), TestCancelled);
        await runner.RunAsync(new GitCommand("--version"), TestCancelled);

        var records = calls.Snapshot();
        Assert.Equal(2, records.Count);
        Assert.True(records[1].Sequence > records[0].Sequence);
        Assert.All(records, record => Assert.Equal((Nine, TimeSpan.Zero), (record.DisplayedStart, record.DisplayedDuration)));

        // The record's own times stay the system's.
        Assert.NotEqual(Nine, records[0].StartedAt);
        Assert.Null(records[0].StandardInput);
        Assert.False(records[0].OnTemporaryIndex);
    }

    [Fact]
    public async Task Standard_input_is_kept_only_for_a_command_that_records_it()
    {
        using var folder = new TempDirectory("runner-input");
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);

        await runner.RunAsync(new GitCommand("hash-object", "--stdin") { WorkingDirectory = folder.Path, StandardInput = "kept\n", RecordsStandardInput = true }, TestCancelled);
        await runner.RunAsync(new GitCommand("hash-object", "--stdin") { WorkingDirectory = folder.Path, StandardInput = "a commit message\n" }, TestCancelled);

        var records = calls.Snapshot();
        Assert.Equal("kept\n", records[0].StandardInput);
        Assert.Null(records[1].StandardInput);
    }

    [Fact]
    public async Task A_call_with_its_own_index_file_is_marked_as_on_a_temporary_index()
    {
        using var folder = new TempDirectory("runner-index");
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);

        await runner.RunAsync(
            new GitCommand("--version") { Environment = new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = Path.Combine(folder.Path, "index") } },
            TestCancelled);
        await runner.RunAsync(new GitCommand("--version") { Environment = new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = null } }, TestCancelled);

        var records = calls.Snapshot();
        Assert.True(records[0].OnTemporaryIndex);
        Assert.False(records[1].OnTemporaryIndex);
    }
}
