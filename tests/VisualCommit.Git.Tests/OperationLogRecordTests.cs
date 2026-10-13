using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What phase 5's activity log reads of the operations (docs/phase-5-design.md 2.3, D173): the
/// queue's start event and the record's new fields with the record clock's times; the log's events,
/// notes and failure summaries that come before or after the record, and its evictions.
/// </summary>
public class OperationLogRecordTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_queue_raises_the_start_and_records_the_key_number_kind_and_frozen_times()
    {
        var log = new OperationLog();
        var gitDirectory = Path.Combine(Path.GetTempPath(), "no-such-repo-" + Guid.NewGuid().ToString("N"), ".git");
        using var queue = new OperationQueue("graph", gitDirectory, () => null, _ => Task.CompletedTask, null, log, NullAppLog.Instance, recordClock: new FrozenClock(Nine));
        var started = new List<OperationStart>();
        var added = new List<OperationRecord>();
        log.Started += (_, start) =>
        {
            started.Add(start);
            Assert.Single(log.Running());
        };
        log.Added += (_, record) => added.Add(record);
        var before = RecordSequence.Next();

        await queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await Task.Delay(20, TestCancelled);
            return OperationReport.Warning("Pushed main, with a warning");
        })).WaitAsync(TestCancelled);

        var start = Assert.Single(started);
        var record = Assert.Single(added);
        Assert.Equal((record.Id, WorktreeKey.Of(gitDirectory), "graph", "Push main", Nine), (start.Id, start.RepositoryKey, start.Repository, start.Title, start.DisplayedStart));
        Assert.Equal(start.Sequence, record.Sequence);
        Assert.True(record.Sequence > before);
        Assert.Equal((WorktreeKey.Of(gitDirectory), OperationKind.Network, false, true), (record.RepositoryKey, record.Kind, record.IsBackground, record.IsWarning));
        Assert.Equal((Nine, TimeSpan.Zero), (record.DisplayedStart, record.DisplayedDuration));

        // The queue's own time stays the system's: the scan for lock files after a cancel uses it (D126).
        Assert.NotEqual(Nine, record.StartedAt);
        Assert.True(record.Duration > TimeSpan.Zero);
        Assert.Empty(log.Running());
    }

    [Fact]
    public void Notes_and_a_failure_summary_wait_for_their_record_or_join_it()
    {
        var log = new OperationLog();
        var updated = new List<string>();
        log.Updated += (_, id) => updated.Add(id);

        log.Note("op-1", "Removed backup reset-20251101T090000000Z (older than 30 days)");
        log.SetFailureSummary("op-1", "Git could not push main.");
        Assert.Empty(updated);
        log.Add(Record("op-1"));

        var record = Assert.Single(log.Snapshot());
        Assert.Equal(["Removed backup reset-20251101T090000000Z (older than 30 days)"], record.Notes);
        Assert.Equal("Git could not push main.", record.FailureSummary);

        log.Note("op-1", "Removed backup discard-20251101T090000000Z (older than 30 days)");
        log.SetFailureSummary("op-1", "Push rejected.");
        Assert.Equal(["op-1", "op-1"], updated);
        record = Assert.Single(log.Snapshot());
        Assert.Equal(2, record.Notes.Count);
        Assert.Equal("Push rejected.", record.FailureSummary);
    }

    [Fact]
    public void A_note_that_waits_more_than_ten_minutes_is_dropped()
    {
        var clock = new ManualTimeProvider(Nine);
        var log = new OperationLog(time: clock);

        log.Note("op-7", "early");
        clock.Advance(TimeSpan.FromMinutes(11));
        log.Note("op-8", "later");
        log.Add(Record("op-7"));
        log.Add(Record("op-8"));

        Assert.Empty(log.Snapshot()[0].Notes);
        Assert.Equal(["later"], log.Snapshot()[1].Notes);
    }

    [Fact]
    public void Past_its_capacity_the_log_evicts_the_oldest_record_and_says_so()
    {
        var log = new OperationLog(capacity: 2);
        var evicted = new List<string>();
        log.Evicted += (_, id) => evicted.Add(id);

        log.Add(Record("op-1"));
        log.Add(Record("op-2"));
        log.Add(Record("op-3"));

        Assert.Equal(["op-1"], evicted);
        Assert.Equal(["op-2", "op-3"], log.Snapshot().Select(record => record.Id));

        // A note for an evicted operation waits as for one that has not ended; it never reappears.
        log.Note("op-1", "late");
        Assert.Equal(["op-2", "op-3"], log.Snapshot().Select(record => record.Id));
    }

    private static OperationRecord Record(string id) =>
        new(id, "graph", "Push main", DateTimeOffset.UnixEpoch, TimeSpan.Zero, OperationOutcome.Succeeded, string.Empty);
}
