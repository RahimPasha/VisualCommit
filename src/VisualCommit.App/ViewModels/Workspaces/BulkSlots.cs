using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The window's slots for the rows of bulk runs (docs/phase-6-design.md 4.3, D210): at most
/// <see cref="Count"/> at once over every run of the window, first come, first served, never two
/// at once to one common git folder (<c>WorktreeKey.Of(CommonDirectory)</c>), nor one to a common
/// folder another queue is fetching from (the request's <c>busyQueue</c>, which the run gives as
/// <see cref="OpenRepositories.NetworkQueueOn"/> with the row's own queue excepted): such a request
/// is passed over, and looked at again when a slot comes back or that queue raises
/// <see cref="IOperationQueue.Changed"/>. A request withdrawn is never granted.
/// <c>TabServices.BulkSlots</c>, one per window. Its state is guarded by a lock; a grant completes
/// its request's task, whose continuation runs where it was awaited (the UI thread).
/// </summary>
public sealed class BulkSlots
{
    /// <summary>How many rows of all the window's runs run at once.</summary>
    public const int Count = 4;

    private readonly Lock _gate = new();
    private readonly List<BulkSlotRequest> _waiting = [];
    private readonly List<BulkSlotRequest> _granted = [];

    /// <summary>How many slots are out now.</summary>
    public int Held
    {
        get
        {
            lock (_gate)
            {
                return _granted.Count;
            }
        }
    }

    /// <summary>How many requests wait for a slot now.</summary>
    public int Waiting
    {
        get
        {
            lock (_gate)
            {
                return _waiting.Count;
            }
        }
    }

    /// <summary>Whether a slot is out on the common git folder <paramref name="commonKey"/> (auto-fetch skips a tick then, 4.3).</summary>
    public bool IsHeldFor(string commonKey)
    {
        ArgumentNullException.ThrowIfNull(commonKey);
        lock (_gate)
        {
            return _granted.Any(request => WorktreeKey.Comparer.Equals(request.CommonKey, commonKey));
        }
    }

    /// <summary>
    /// Auto-fetch's check of 4.3 (<c>AutoFetchScheduler.CommonFolderBusy</c>): another queue that
    /// <paramref name="registry"/> holds on <paramref name="repository"/>'s common git folder runs a
    /// network operation (its own queue excepted), or a slot is out on that folder.
    /// </summary>
    public bool IsCommonFolderBusy(RepositoryViewModel repository, OpenRepositories registry)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(registry);
        var common = WorktreeKey.Of(repository.Repository.CommonDirectory);
        return IsHeldFor(common) || registry.NetworkQueueOn(common, repository.Queue) is not null;
    }

    /// <summary>
    /// Asks for a slot for a row on the common git folder <paramref name="commonKey"/>; granted at
    /// once when one is free. <paramref name="busyQueue"/>, when given, names a queue that fetches
    /// from that folder now (null when none): the request is passed over while it does. Call it on
    /// the UI thread: a queue's change is looked at on the caller's synchronization context.
    /// </summary>
    public BulkSlotRequest Request(string commonKey, Func<IOperationQueue?>? busyQueue = null)
    {
        ArgumentNullException.ThrowIfNull(commonKey);
        var request = new BulkSlotRequest(this, commonKey, busyQueue, SynchronizationContext.Current);
        lock (_gate)
        {
            _waiting.Add(request);
        }

        Grant();
        return request;
    }

    /// <summary>A request is withdrawn: it is never granted; false when it was granted already.</summary>
    internal bool Withdraw(BulkSlotRequest request)
    {
        bool withdrawn;
        lock (_gate)
        {
            withdrawn = _waiting.Remove(request);
        }

        if (withdrawn)
        {
            request.Unwatch();
            request.Complete(granted: false);
            Grant();
        }

        return withdrawn;
    }

    /// <summary>A granted slot comes back: the next requests are served.</summary>
    internal void Release(BulkSlotRequest request)
    {
        bool released;
        lock (_gate)
        {
            released = _granted.Remove(request);
        }

        if (released)
        {
            Grant();
        }
    }

    /// <summary>
    /// Serves the waiting requests in order, while a slot is free: a request whose common folder
    /// holds a slot, or one granted in this round, is passed over, and so is one whose
    /// <c>busyQueue</c> fetches now, which then watches that queue.
    /// </summary>
    internal void Grant()
    {
        var granted = new List<BulkSlotRequest>();
        var watched = new List<(BulkSlotRequest Request, IOperationQueue Queue)>();
        lock (_gate)
        {
            foreach (var request in _waiting.ToList())
            {
                if (_granted.Count >= Count)
                {
                    break;
                }

                if (_granted.Any(other => WorktreeKey.Comparer.Equals(other.CommonKey, request.CommonKey)))
                {
                    continue;
                }

                if (request.BusyQueue?.Invoke() is { } queue)
                {
                    watched.Add((request, queue));
                    continue;
                }

                _waiting.Remove(request);
                _granted.Add(request);
                granted.Add(request);
            }
        }

        foreach (var (request, queue) in watched)
        {
            request.Watch(queue);
        }

        foreach (var request in granted)
        {
            request.Unwatch();
            request.Complete(granted: true);
        }

        // A queue that ended its network operation before it was watched raises nothing more:
        // look again at once.
        if (watched.Any(entry => !ReferenceEquals(entry.Request.BusyQueue?.Invoke(), entry.Queue)))
        {
            Grant();
        }
    }
}

/// <summary>
/// One row's request for a slot of <see cref="BulkSlots"/> (docs/phase-6-design.md 4.3): its task
/// completes true when it is granted and false when it is withdrawn first. A granted slot is
/// held until <see cref="Release"/>; release and withdrawal may be called more than once.
/// </summary>
public sealed class BulkSlotRequest
{
    private readonly BulkSlots _slots;
    private readonly SynchronizationContext? _context;
    private readonly TaskCompletionSource<bool> _granted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _gate = new();
    private IOperationQueue? _watched;

    internal BulkSlotRequest(BulkSlots slots, string commonKey, Func<IOperationQueue?>? busyQueue, SynchronizationContext? context)
    {
        _slots = slots;
        CommonKey = commonKey;
        BusyQueue = busyQueue;
        _context = context;
    }

    /// <summary>The common git folder the slot is for.</summary>
    public string CommonKey { get; }

    /// <summary>True once granted, false once withdrawn before.</summary>
    public Task<bool> Granted => _granted.Task;

    /// <summary>The slot is out: granted and not released.</summary>
    public bool IsGranted => _granted.Task is { IsCompletedSuccessfully: true, Result: true } && !IsReleased;

    /// <summary>The slot was given back.</summary>
    public bool IsReleased { get; private set; }

    internal Func<IOperationQueue?>? BusyQueue { get; }

    /// <summary>Withdraws a request not granted yet (4.5's <c>StopStarting</c>); gives a granted slot back.</summary>
    public void Withdraw()
    {
        if (!_slots.Withdraw(this))
        {
            Release();
        }
    }

    /// <summary>Gives the slot back once the row's operation has returned (4.3).</summary>
    public void Release()
    {
        if (_granted.Task is not { IsCompletedSuccessfully: true, Result: true } || IsReleased)
        {
            return;
        }

        IsReleased = true;
        _slots.Release(this);
    }

    internal void Complete(bool granted) => _granted.TrySetResult(granted);

    /// <summary>Looks again at the waiting requests once <paramref name="queue"/> changes: its network operation may have ended.</summary>
    internal void Watch(IOperationQueue queue)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_watched, queue))
            {
                return;
            }

            if (_watched is { } old)
            {
                old.Changed -= OnQueueChanged;
            }

            _watched = queue;
            queue.Changed += OnQueueChanged;
        }
    }

    internal void Unwatch()
    {
        lock (_gate)
        {
            if (_watched is { } old)
            {
                old.Changed -= OnQueueChanged;
                _watched = null;
            }
        }
    }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        Unwatch();

        // The registry is the UI thread's: the requests are looked at there again.
        if (_context is null || SynchronizationContext.Current == _context)
        {
            _slots.Grant();
        }
        else
        {
            _context.Post(_ => _slots.Grant(), null);
        }
    }
}
