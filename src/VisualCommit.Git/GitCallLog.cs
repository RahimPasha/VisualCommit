using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>Keeps the most recent git call records in memory.</summary>
public sealed class GitCallLog : IGitCallLog
{
    private readonly Lock _gate = new();
    private readonly Queue<GitCallRecord> _records = new();
    private readonly int _capacity;

    /// <param name="capacity">How many records to keep; older ones are dropped.</param>
    public GitCallLog(int capacity = 2000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public event EventHandler<GitCallRecord>? Added;

    public void Add(GitCallRecord record)
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

        Added?.Invoke(this, record);
    }

    public IReadOnlyList<GitCallRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _records];
        }
    }
}
