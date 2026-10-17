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
    string Message)
{
    /// <summary>The worktree it ran in (<see cref="Git.WorktreeKey"/>): its own git folder, unique per worktree, unlike <see cref="Repository"/> (docs/phase-5-design.md 2.3).</summary>
    public string RepositoryKey { get; init; } = string.Empty;

    /// <summary>The number it took when it started (<see cref="RecordSequence"/>): the activity log orders its rows by it.</summary>
    public long Sequence { get; init; }

    /// <summary>It succeeded with a warning (<see cref="OperationResult.IsWarning"/>).</summary>
    public bool IsWarning { get; init; }

    /// <summary>Local or network.</summary>
    public OperationKind Kind { get; init; }

    /// <summary>A background operation: auto-fetch or the backups' clean-up.</summary>
    public bool IsBackground { get; init; }

    /// <summary>
    /// Where its failure was shown (<see cref="GitOperation.Errors"/>): a stage panel's write shows
    /// its message in the stage panel, not in the status bar, so the activity log shows none for it
    /// (docs/phase-5-design.md 2.2).
    /// </summary>
    public ErrorDisplay Errors { get; init; }

    /// <summary>The notes made while it ran (<see cref="IOperationLog.Note"/>): the clean-up's <c>Removed backup …</c> lines.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The error dialog's summary of its failure, set after it ended (<see cref="IOperationLog.SetFailureSummary"/>); null otherwise.</summary>
    public string? FailureSummary { get; init; }

    /// <summary>When it started by the record clock (<see cref="AppClocks"/>): what the activity log shows.</summary>
    public DateTimeOffset DisplayedStart { get; init; }

    /// <summary>How long it ran by the record clock: what the activity log shows.</summary>
    public TimeSpan DisplayedDuration { get; init; }
}

/// <summary>An operation that has started and not ended yet: the activity log shows it running (docs/phase-5-design.md 2.3).</summary>
/// <param name="Id">The operation's id.</param>
/// <param name="RepositoryKey">The worktree it runs in (<see cref="Git.WorktreeKey"/>).</param>
/// <param name="Repository">The repository's name.</param>
/// <param name="Title">The operation's title.</param>
/// <param name="Sequence">The number it took when it started (<see cref="RecordSequence"/>).</param>
/// <param name="DisplayedStart">When it started by the record clock.</param>
public sealed record OperationStart(string Id, string RepositoryKey, string Repository, string Title, long Sequence, DateTimeOffset DisplayedStart);

/// <summary>
/// The record of every operation of the app session (T4, docs/phase-5-design.md 2.3, D173). Safe
/// to use from any thread; its events are raised on the thread that acts, so a listener that
/// shows them posts to its own.
/// </summary>
public interface IOperationLog
{
    /// <summary>An operation started (<see cref="Start"/>).</summary>
    event EventHandler<OperationStart>? Started;

    /// <summary>An operation's record was added (<see cref="Add"/>), with the notes made for it before.</summary>
    event EventHandler<OperationRecord>? Added;

    /// <summary>A record changed: a note was added to it, or its failure summary set. The argument is its id.</summary>
    event EventHandler<string>? Updated;

    /// <summary>A record left the log, the oldest past its capacity. The argument is its id.</summary>
    event EventHandler<string>? Evicted;

    /// <summary>An operation of the queue starts: it shows as running until its record is added.</summary>
    void Start(OperationStart start);

    void Add(OperationRecord record);

    /// <summary>
    /// Adds <paramref name="text"/> to the notes of the operation <paramref name="operationId"/>: to
    /// its record when it has one, otherwise kept until its record is added. A note for an id that
    /// gets no record within 10 minutes is dropped.
    /// </summary>
    void Note(string operationId, string text);

    /// <summary>Sets the failure summary the error dialog chose for the operation <paramref name="operationId"/> (<see cref="OperationRecord.FailureSummary"/>), as <see cref="Note"/> keeps a note.</summary>
    void SetFailureSummary(string operationId, string summary);

    /// <summary>The recorded operations, oldest first.</summary>
    IReadOnlyList<OperationRecord> Snapshot();

    /// <summary>The operations that started and have no record yet, in the order they started.</summary>
    IReadOnlyList<OperationStart> Running();
}

/// <summary>Keeps the most recent operation records in memory: the last 500.</summary>
public sealed class OperationLog : IOperationLog
{
    /// <summary>How long a note or a failure summary waits for its operation's record.</summary>
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _gate = new();
    private readonly List<OperationRecord> _records = [];
    private readonly List<OperationStart> _running = [];
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private readonly int _capacity;
    private readonly TimeProvider _time;

    /// <param name="capacity">How many records to keep; older ones are dropped.</param>
    /// <param name="time">The clock a pending note's 10 minutes are measured with; the system clock by default.</param>
    public OperationLog(int capacity = 500, TimeProvider? time = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _time = time ?? TimeProvider.System;
    }

    public event EventHandler<OperationStart>? Started;

    public event EventHandler<OperationRecord>? Added;

    public event EventHandler<string>? Updated;

    public event EventHandler<string>? Evicted;

    public void Start(OperationStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        lock (_gate)
        {
            _running.Add(start);
        }

        Started?.Invoke(this, start);
    }

    public void Add(OperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        List<string> evicted = [];
        lock (_gate)
        {
            _running.RemoveAll(start => string.Equals(start.Id, record.Id, StringComparison.Ordinal));
            if (_pending.Remove(record.Id, out var pending))
            {
                record = record with { Notes = [.. record.Notes, .. pending.Notes], FailureSummary = pending.FailureSummary ?? record.FailureSummary };
            }

            DropExpiredPending();
            _records.Add(record);
            while (_records.Count > _capacity)
            {
                evicted.Add(_records[0].Id);
                _records.RemoveAt(0);
            }
        }

        Added?.Invoke(this, record);
        foreach (var id in evicted)
        {
            Evicted?.Invoke(this, id);
        }
    }

    public void Note(string operationId, string text)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(text);
        Change(operationId, record => record with { Notes = [.. record.Notes, text] }, pending => pending with { Notes = [.. pending.Notes, text] });
    }

    public void SetFailureSummary(string operationId, string summary)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(summary);
        Change(operationId, record => record with { FailureSummary = summary }, pending => pending with { FailureSummary = summary });
    }

    public IReadOnlyList<OperationRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _records];
        }
    }

    public IReadOnlyList<OperationStart> Running()
    {
        lock (_gate)
        {
            return [.. _running];
        }
    }

    /// <summary>Changes the record of <paramref name="operationId"/> when the log has it, or what waits for it otherwise.</summary>
    private void Change(string operationId, Func<OperationRecord, OperationRecord> record, Func<Pending, Pending> pending)
    {
        bool updated;
        lock (_gate)
        {
            var index = _records.FindIndex(kept => string.Equals(kept.Id, operationId, StringComparison.Ordinal));
            updated = index >= 0;
            if (updated)
            {
                _records[index] = record(_records[index]);
            }
            else
            {
                DropExpiredPending();
                var waiting = _pending.GetValueOrDefault(operationId) ?? new Pending([], null, _time.GetUtcNow());
                _pending[operationId] = pending(waiting);
            }
        }

        if (updated)
        {
            Updated?.Invoke(this, operationId);
        }
    }

    /// <summary>Drops what waited more than 10 minutes for a record. Call it under <see cref="_gate"/>.</summary>
    private void DropExpiredPending()
    {
        var now = _time.GetUtcNow();
        foreach (var id in _pending.Where(pair => now - pair.Value.Since > PendingLifetime).Select(pair => pair.Key).ToList())
        {
            _pending.Remove(id);
        }
    }

    /// <summary>What waits for an operation's record: its notes and failure summary, since when.</summary>
    private sealed record Pending(IReadOnlyList<string> Notes, string? FailureSummary, DateTimeOffset Since);
}
