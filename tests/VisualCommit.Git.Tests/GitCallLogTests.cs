using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The session's record of git calls (docs/phase-5-design.md 2.3, D173): the reads in a ring of
/// their own, an operation's calls kept as long as its record, within a memory budget that drops
/// the oldest operation's calls first.
/// </summary>
public class GitCallLogTests
{
    private static GitCallRecord Record(string command, string? operationId = null, string output = "") =>
        new(DateTimeOffset.Now, TimeSpan.FromMilliseconds(5), command, null, GitCallOutcome.Completed, 0, output, string.Empty, operationId);

    private static OperationRecord Operation(string id) =>
        new(id, "graph", "Push main", DateTimeOffset.UnixEpoch, TimeSpan.Zero, OperationOutcome.Succeeded, string.Empty);

    [Fact]
    public void Snapshot_returns_the_records_oldest_first()
    {
        var log = new GitCallLog();
        log.Add(Record("git one"));
        log.Add(Record("git two"));
        log.Add(Record("git three"));

        Assert.Equal(["git one", "git two", "git three"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void Only_the_newest_records_are_kept_once_the_capacity_is_reached()
    {
        var log = new GitCallLog(capacity: 2);
        log.Add(Record("git one"));
        log.Add(Record("git two"));
        log.Add(Record("git three"));

        Assert.Equal(["git two", "git three"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void Added_is_raised_for_every_record()
    {
        var log = new GitCallLog();
        var seen = new List<string>();
        log.Added += (sender, record) =>
        {
            Assert.Same(log, sender);
            seen.Add(record.CommandText);
        };

        log.Add(Record("git one"));
        log.Add(Record("git two"));

        Assert.Equal(["git one", "git two"], seen);
    }

    [Fact]
    public void A_snapshot_does_not_change_when_more_records_arrive()
    {
        var log = new GitCallLog();
        log.Add(Record("git one"));
        var snapshot = log.Snapshot();

        log.Add(Record("git two"));

        Assert.Single(snapshot);
    }

    [Fact]
    public void A_capacity_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitCallLog(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitCallLog(operationCallBudget: 0));
    }

    [Fact]
    public void The_default_ring_keeps_two_thousand_reads()
    {
        var log = new GitCallLog();
        for (var i = 0; i < GitCallLog.DefaultReadCapacity + 5; i++)
        {
            log.Add(Record($"git read {i}"));
        }

        var kept = log.Snapshot();
        Assert.Equal(2000, kept.Count);
        Assert.Equal("git read 5", kept[0].CommandText);
    }

    [Fact]
    public void A_burst_of_reads_never_takes_an_operations_calls_away()
    {
        var log = new GitCallLog(capacity: 3);
        var removed = new List<string>();
        log.Removed += (sender, records) =>
        {
            Assert.Same(log, sender);
            removed.AddRange(records.Select(record => record.CommandText));
        };

        log.Add(Record("git read 1"));
        log.Add(Record("git switch main", "op-1"));
        log.Add(Record("git read 2"));
        log.Add(Record("git status", "op-1"));
        for (var i = 3; i <= 6; i++)
        {
            log.Add(Record($"git read {i}"));
        }

        // Three reads left the ring of three, oldest first, one per Removed; the operation's calls stay, in the order they came.
        Assert.Equal(["git read 1", "git read 2", "git read 3"], removed);
        Assert.Equal(["git switch main", "git status", "git read 4", "git read 5", "git read 6"], log.Snapshot().Select(record => record.CommandText));
        Assert.False(log.HasDroppedCalls("op-1"));
    }

    [Fact]
    public void An_operations_calls_leave_with_its_record_and_say_so()
    {
        var operations = new OperationLog(capacity: 1);
        var log = new GitCallLog(operations: operations);
        IReadOnlyList<GitCallRecord>? removed = null;
        log.Removed += (_, records) => removed = records;
        log.Add(Record("git fetch origin", "op-1"));
        log.Add(Record("git rev-parse HEAD"));
        log.Add(Record("git push origin main", "op-2"));
        log.Add(Record("git fetch --prune origin", "op-1"));
        operations.Add(Operation("op-1"));
        Assert.Null(removed);

        // op-2's record pushes op-1's out of the operation log of one: op-1's calls go with it.
        operations.Add(Operation("op-2"));

        Assert.Equal(["git fetch origin", "git fetch --prune origin"], removed!.Select(record => record.CommandText));
        Assert.Equal(["git rev-parse HEAD", "git push origin main"], log.Snapshot().Select(record => record.CommandText));
        Assert.False(log.HasDroppedCalls("op-1"), "An evicted operation's calls are gone with its record, not dropped by the budget.");
    }

    [Fact]
    public void Past_the_budget_the_oldest_operations_calls_go_first_whole()
    {
        // Each record takes 200 bytes and two per character of its texts: 20 characters and an
        // id of 4 come to 248; the budget holds four such calls.
        var log = new GitCallLog(operationCallBudget: 1000);
        var removed = new List<string>();
        log.Removed += (_, records) => removed.AddRange(records.Select(record => record.CommandText));
        log.Add(Record("git one 1", "op-1", output: "12345678901"));
        log.Add(Record("git one 2", "op-1", output: "12345678901"));
        log.Add(Record("git two 1", "op-2", output: "12345678901"));
        log.Add(Record("git read", output: new string('x', 4000)));
        log.Add(Record("git two 2", "op-2", output: "12345678901"));
        Assert.Empty(removed);

        log.Add(Record("git thr 1", "op-3", output: "12345678901"));

        Assert.Equal(["git one 1", "git one 2"], removed);
        Assert.True(log.HasDroppedCalls("op-1"));
        Assert.False(log.HasDroppedCalls("op-2"));
        Assert.Equal(["git two 1", "git read", "git two 2", "git thr 1"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void A_dropped_operation_stays_marked_until_its_record_leaves()
    {
        var operations = new OperationLog(capacity: 1);
        var log = new GitCallLog(operations: operations, operationCallBudget: 500);
        log.Add(Record("git one", "op-1", output: new string('a', 100)));
        log.Add(Record("git two", "op-2", output: new string('b', 100)));
        operations.Add(Operation("op-1"));
        Assert.True(log.HasDroppedCalls("op-1"));

        operations.Add(Operation("op-2"));

        Assert.False(log.HasDroppedCalls("op-1"));
        Assert.Equal(["git two"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void An_operation_alone_past_the_budget_goes_too_and_its_later_calls_are_kept_again()
    {
        var log = new GitCallLog(operationCallBudget: 300);
        log.Add(Record("git big", "op-1", output: new string('x', 100)));
        Assert.Empty(log.Snapshot());
        Assert.True(log.HasDroppedCalls("op-1"));

        log.Add(Record("git small", "op-1"));

        Assert.Equal(["git small"], log.Snapshot().Select(record => record.CommandText));
        Assert.True(log.HasDroppedCalls("op-1"));
    }

    [Fact]
    public async Task Records_added_from_many_threads_are_all_kept_or_removed_once()
    {
        var log = new GitCallLog(capacity: 50);
        var removed = 0;
        log.Removed += (_, records) => Interlocked.Add(ref removed, records.Count);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                log.Add(Record($"git {thread} {i}", i % 2 == 0 ? $"op-{thread}" : null));
            }
        })));

        Assert.Equal(400 + 50, log.Snapshot().Count);
        Assert.Equal(400 - 50, removed);
    }
}
