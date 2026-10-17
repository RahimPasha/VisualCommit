using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The session's record of operations as the activity log reads it (docs/phase-5-design.md 2.3,
/// D173): its events and their order, the running operations, notes made before and after a
/// record, the 500 records it keeps and what it says when one leaves. The queue's own part (the
/// start, the record's fields and times) is <c>OperationLogRecordTests</c>'.
/// </summary>
public class OperationLogTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public void The_log_keeps_the_last_five_hundred_records_and_evicts_the_older_ones_one_by_one()
    {
        var log = new OperationLog();
        var evicted = new List<string>();
        log.Evicted += (_, id) => evicted.Add(id);

        for (var i = 1; i <= 503; i++)
        {
            log.Add(Record($"op-{i}"));
        }

        var kept = log.Snapshot();
        Assert.Equal(500, kept.Count);
        Assert.Equal("op-4", kept[0].Id);
        Assert.Equal("op-503", kept[^1].Id);
        Assert.Equal(["op-1", "op-2", "op-3"], evicted);
    }

    [Fact]
    public void A_record_is_added_before_the_one_it_pushes_out_is_said_to_be_evicted()
    {
        var log = new OperationLog(capacity: 1);
        var events = new List<string>();
        log.Added += (_, record) => events.Add("added " + record.Id);
        log.Evicted += (_, id) =>
        {
            events.Add("evicted " + id);
            Assert.DoesNotContain(log.Snapshot(), record => record.Id == id);
        };

        log.Add(Record("op-1"));
        log.Add(Record("op-2"));

        Assert.Equal(["added op-1", "added op-2", "evicted op-1"], events);
    }

    [Fact]
    public void Running_lists_the_started_operations_in_their_order_until_each_record_comes()
    {
        var log = new OperationLog();
        var started = new List<OperationStart>();
        log.Started += (sender, start) =>
        {
            Assert.Same(log, sender);
            started.Add(start);
        };

        log.Start(Start("op-1", 10));
        log.Start(Start("op-2", 11));
        Assert.Equal(["op-1", "op-2"], log.Running().Select(start => start.Id));

        log.Add(Record("op-1"));

        Assert.Equal(["op-2"], log.Running().Select(start => start.Id));
        Assert.Equal(["op-1", "op-2"], started.Select(start => start.Id));
        Assert.Equal(["op-1"], log.Snapshot().Select(record => record.Id));
    }

    [Fact]
    public void Notes_keep_their_order_after_the_records_own_and_each_late_note_says_the_record_changed()
    {
        var log = new OperationLog();
        var added = new List<OperationRecord>();
        var updated = new List<string>();
        log.Added += (_, record) => added.Add(record);
        log.Updated += (_, id) => updated.Add(id);

        log.Note("op-1", "Removed backup reset-20251120T090001000Z (older than 30 days)");
        log.Note("op-1", "Removed backup branch-20251120T090002000Z (older than 30 days)");
        log.Add(Record("op-1") with { Notes = ["own"] });
        log.Note("op-1", "Removed backup reset-20251220T090001000Z (more than 50 backups)");

        // The record the Added event carries already holds the notes made before it.
        Assert.Equal(
            ["own", "Removed backup reset-20251120T090001000Z (older than 30 days)", "Removed backup branch-20251120T090002000Z (older than 30 days)"],
            Assert.Single(added).Notes);
        Assert.Equal(
            [
                "own",
                "Removed backup reset-20251120T090001000Z (older than 30 days)",
                "Removed backup branch-20251120T090002000Z (older than 30 days)",
                "Removed backup reset-20251220T090001000Z (more than 50 backups)",
            ],
            Assert.Single(log.Snapshot()).Notes);
        Assert.Equal(["op-1"], updated);
    }

    [Fact]
    public void A_failure_summary_set_after_the_record_replaces_its_own_and_one_set_before_wins_over_none()
    {
        var log = new OperationLog();
        log.SetFailureSummary("op-1", "1 conflict prevents checkout. Commit, stash, or discard your changes first.");
        log.Add(Record("op-1"));
        log.Add(Record("op-2") with { FailureSummary = "Git could not push main." });
        log.SetFailureSummary("op-2", "main on origin has changed since: nothing was changed.");

        Assert.Equal(
            ["1 conflict prevents checkout. Commit, stash, or discard your changes first.", "main on origin has changed since: nothing was changed."],
            log.Snapshot().Select(record => record.FailureSummary));
    }

    [Fact]
    public void A_note_for_another_operation_waits_without_touching_the_kept_records()
    {
        var log = new OperationLog();
        var updated = new List<string>();
        log.Updated += (_, id) => updated.Add(id);
        log.Add(Record("op-1"));

        log.Note("op-2", "early");

        Assert.Empty(updated);
        Assert.Empty(Assert.Single(log.Snapshot()).Notes);
    }

    [Fact]
    public async Task The_queue_records_a_background_operation_and_where_a_stage_panel_write_shows_its_failure()
    {
        var log = new OperationLog();
        var gitDirectory = Path.Combine(Path.GetTempPath(), "no-such-repo-" + Guid.NewGuid().ToString("N"), ".git");
        using var queue = new OperationQueue("graph", gitDirectory, () => null, _ => Task.CompletedTask, null, log, NullAppLog.Instance, recordClock: new FrozenClock(Nine));
        var added = new TaskCompletionSource<OperationRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        log.Added += (_, record) =>
        {
            if (record.IsBackground)
            {
                added.TrySetResult(record);
            }
        };

        Assert.True(queue.TryStartInBackground(new GitOperation("Clean up backups", "Cleaning up backups…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Done("Removed 3 backups")))));
        var background = await added.Task.WaitAsync(TestCancelled);
        await queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ =>
            throw new IOException("index.lock exists"))
        {
            Errors = ErrorDisplay.StagePanel,
        }).WaitAsync(TestCancelled);

        Assert.Equal(("Clean up backups", true, OperationOutcome.Succeeded, "Removed 3 backups", ErrorDisplay.Dialog), (background.Title, background.IsBackground, background.Outcome, background.Message, background.Errors));
        var stage = log.Snapshot()[^1];
        Assert.Equal(("Stage", false, OperationOutcome.Failed, ErrorDisplay.StagePanel), (stage.Title, stage.IsBackground, stage.Outcome, stage.Errors));
        Assert.True(stage.Sequence > background.Sequence);
    }

    [Fact]
    public async Task Records_notes_and_starts_from_many_threads_are_all_kept()
    {
        var log = new OperationLog();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                var id = $"op-{thread}-{i}";
                log.Start(Start(id, (thread * 100) + i));
                log.Note(id, "before");
                log.Add(Record(id));
                log.Note(id, "after");
            }
        })));

        Assert.Equal(400, log.Snapshot().Count);
        Assert.All(log.Snapshot(), record => Assert.Equal(["before", "after"], record.Notes));
        Assert.Empty(log.Running());
    }

    private static OperationRecord Record(string id) =>
        new(id, "graph", "Push main", DateTimeOffset.UnixEpoch, TimeSpan.Zero, OperationOutcome.Succeeded, string.Empty);

    private static OperationStart Start(string id, long sequence) => new(id, "key", "graph", "Push main", sequence, Nine);
}
