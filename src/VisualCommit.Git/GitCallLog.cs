using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git;

/// <summary>
/// Keeps the git call records of the app session in memory, in two stores (docs/phase-5-design.md
/// 2.3, D173), so that a burst of reads never takes an operation's commands away from the activity
/// log:
/// <list type="bullet">
/// <item>the calls of an operation (<see cref="GitCallRecord.OperationId"/>) are kept as long as
/// the operation's record is, until the operation log evicts it (<see cref="IOperationLog.Evicted"/>),
/// within a memory budget for all of them that drops the oldest operation's calls first
/// (<see cref="HasDroppedCalls"/>);</item>
/// <item>the reads, the calls outside an operation, are kept in a ring of their own, the newest
/// <see cref="DefaultReadCapacity"/>.</item>
/// </list>
/// Without an operation log, an operation's calls leave only by the budget.
/// </summary>
public sealed class GitCallLog : IGitCallLog
{
    /// <summary>How many reads are kept: the newest 2,000.</summary>
    public const int DefaultReadCapacity = 2000;

    /// <summary>
    /// How much memory the calls of the kept operations may take: 64 MB. Each output is cut to 16
    /// KiB already (<see cref="GitCallRecord.MaxOutputLength"/>), so that is about a thousand calls
    /// with full outputs, and many more as git's outputs usually are.
    /// </summary>
    public const long DefaultOperationCallBudget = 64L * 1024 * 1024;

    /// <summary>What a record takes beyond its texts, roughly: the object, its fields, the store's node.</summary>
    private const long RecordOverhead = 200;

    private readonly Lock _gate = new();

    /// <summary>Every kept record, in the order it was added: what <see cref="Snapshot"/> returns.</summary>
    private readonly LinkedList<Kept> _all = new();

    /// <summary>The kept reads, oldest first.</summary>
    private readonly Queue<LinkedListNode<Kept>> _reads = new();

    /// <summary>The kept calls of each operation, by its id.</summary>
    private readonly Dictionary<string, OperationCalls> _operations = new(StringComparer.Ordinal);

    /// <summary>The ids of <see cref="_operations"/>, oldest first by their first kept call: the order the budget drops them in.</summary>
    private readonly LinkedList<string> _operationOrder = new();

    /// <summary>The operations whose calls the budget dropped, until their records leave the operation log.</summary>
    private readonly HashSet<string> _dropped = new(StringComparer.Ordinal);

    private readonly int _readCapacity;
    private readonly long _operationCallBudget;
    private long _operationCallBytes;

    /// <param name="capacity">How many reads to keep (calls outside an operation); older ones are dropped.</param>
    /// <param name="operations">
    /// The operation log of the same session: an operation's calls leave when its record does. Null
    /// keeps them until the budget drops them.
    /// </param>
    /// <param name="operationCallBudget">How many bytes the kept operations' calls may take, roughly; the oldest operation's go first.</param>
    public GitCallLog(int capacity = DefaultReadCapacity, IOperationLog? operations = null, long operationCallBudget = DefaultOperationCallBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(operationCallBudget, 1);
        _readCapacity = capacity;
        _operationCallBudget = operationCallBudget;
        if (operations is not null)
        {
            operations.Evicted += (_, operationId) => Forget(operationId);
        }
    }

    public event EventHandler<GitCallRecord>? Added;

    public event EventHandler<IReadOnlyList<GitCallRecord>>? Removed;

    public void Add(GitCallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var removed = new List<GitCallRecord>();
        lock (_gate)
        {
            var kept = new Kept(record, SizeOf(record));
            var node = _all.AddLast(kept);
            if (record.OperationId is { } operationId)
            {
                if (!_operations.TryGetValue(operationId, out var calls))
                {
                    calls = new OperationCalls(_operationOrder.AddLast(operationId));
                    _operations[operationId] = calls;
                }

                calls.Nodes.Add(node);
                calls.Bytes += kept.Bytes;
                _operationCallBytes += kept.Bytes;

                // Past the budget the oldest operation's calls go first, whole; the operation of
                // this call goes last, when it alone is past the budget.
                while (_operationCallBytes > _operationCallBudget && _operationOrder.First is { } oldest)
                {
                    _dropped.Add(oldest.Value);
                    RemoveOperation(oldest.Value, removed);
                }
            }
            else
            {
                _reads.Enqueue(node);
                while (_reads.Count > _readCapacity)
                {
                    var read = _reads.Dequeue();
                    _all.Remove(read);
                    removed.Add(read.Value.Record);
                }
            }
        }

        Added?.Invoke(this, record);
        RaiseRemoved(removed);
    }

    public IReadOnlyList<GitCallRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _all.Select(kept => kept.Record)];
        }
    }

    public bool HasDroppedCalls(string operationId)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        lock (_gate)
        {
            return _dropped.Contains(operationId);
        }
    }

    /// <summary>The operation's record left the operation log: its calls go too.</summary>
    private void Forget(string operationId)
    {
        var removed = new List<GitCallRecord>();
        lock (_gate)
        {
            _dropped.Remove(operationId);
            RemoveOperation(operationId, removed);
        }

        RaiseRemoved(removed);
    }

    /// <summary>Takes the operation's kept calls out of both lists, adding them to <paramref name="removed"/>. Call it under <see cref="_gate"/>.</summary>
    private void RemoveOperation(string operationId, List<GitCallRecord> removed)
    {
        if (!_operations.Remove(operationId, out var calls))
        {
            return;
        }

        _operationOrder.Remove(calls.OrderNode);
        _operationCallBytes -= calls.Bytes;
        foreach (var node in calls.Nodes)
        {
            _all.Remove(node);
            removed.Add(node.Value.Record);
        }
    }

    private void RaiseRemoved(List<GitCallRecord> removed)
    {
        if (removed.Count > 0)
        {
            Removed?.Invoke(this, removed);
        }
    }

    /// <summary>About how many bytes a record takes in memory: its texts as UTF-16 and a fixed overhead.</summary>
    private static long SizeOf(GitCallRecord record)
    {
        long characters = record.CommandText.Length
            + record.StandardOutput.Length
            + record.StandardError.Length
            + (record.StandardInput?.Length ?? 0)
            + (record.WorkingDirectory?.Length ?? 0)
            + (record.OperationId?.Length ?? 0);
        return (characters * sizeof(char)) + RecordOverhead;
    }

    /// <summary>A kept record and what it takes in memory.</summary>
    private sealed record Kept(GitCallRecord Record, long Bytes);

    /// <summary>One operation's kept calls, in the order they were added, and where it stands in <see cref="_operationOrder"/>.</summary>
    private sealed class OperationCalls(LinkedListNode<string> orderNode)
    {
        public LinkedListNode<string> OrderNode { get; } = orderNode;

        public List<LinkedListNode<Kept>> Nodes { get; } = [];

        public long Bytes { get; set; }
    }
}
