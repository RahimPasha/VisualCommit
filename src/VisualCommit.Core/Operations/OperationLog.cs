namespace VisualCommit.Core.Operations;

/// <summary>One operation that ended (phase 3 design 1.7). Phase 5's activity log groups the git call records under these by <see cref="Id"/>.</summary>
/// <param name="Id">The operation's id, as <see cref="Git.GitCallRecord.OperationId"/> names it.</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Title">The operation's title.</param>
/// <param name="StartedAt">When it started to run (not when it was queued).</param>
/// <param name="Duration">How long it ran, its refresh included.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Message">The status bar's text for it.</param>
public sealed record OperationRecord(
    string Id,
    string Repository,
    string Title,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    OperationOutcome Outcome,
    string Message);

/// <summary>The record of every operation of the app session (T4). Safe to use from any thread.</summary>
public interface IOperationLog
{
    void Add(OperationRecord record);

    /// <summary>The recorded operations, oldest first.</summary>
    IReadOnlyList<OperationRecord> Snapshot();
}

/// <summary>Keeps the most recent operation records in memory: the last 500.</summary>
public sealed class OperationLog : IOperationLog
{
    private readonly Lock _gate = new();
    private readonly Queue<OperationRecord> _records = new();
    private readonly int _capacity;

    /// <param name="capacity">How many records to keep; older ones are dropped.</param>
    public OperationLog(int capacity = 500)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public void Add(OperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            _records.Enqueue(record);
            while (_records.Count > _capacity)
            {
                _records.Dequeue();
            }
        }
    }

    public IReadOnlyList<OperationRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _records];
        }
    }
}
