using System.Globalization;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The order in which a start's restored tabs open when the active tab is a restored repository's
/// (D457 (5)). The active tab opens first and alone, so that the processes of the other openings
/// do not hold back the history the user looks at (Q1, D50). The others are released at the
/// earliest of: its graph has its first page; its opening or first load ends without one; its tab
/// is closed; <see cref="LongestWait"/> has passed on the restore clock. Then they open in tab
/// order, at most <see cref="OpensAtOnce"/> before their own first pages, as "Add repositories"
/// opens and a dashboard reads (D207). A waiting tab the user shows opens at once, beside them; a
/// waiting tab that is closed never opens; once the window closes nothing more opens.
/// <para>
/// The window makes one for such a start and drops it when the start has ended. It keeps no closed
/// tab: an entry forgets its tab when the tab is closed and when its task has ended, and once every
/// task has ended the order lets go of the lifetime too. Its state is guarded by a lock, and
/// <c>open</c> is always called outside it: in the app every call comes on the UI thread, and the
/// order's awaits resume there; without a synchronization context (view-model tests on the thread
/// pool) its continuations run on the pool beside the caller.
/// </para>
/// </summary>
public sealed class RestoreOrder
{
    /// <summary>How many released tabs open at once before their first pages, as "Add repositories" opens and a dashboard reads (D207).</summary>
    public const int OpensAtOnce = 4;

    /// <summary>The line logged at Info when the order starts: the first tab's name, the others' count, <see cref="OpensAtOnce"/>.</summary>
    public const string StartedFormat = "Restore order: {0} opens first; the other restored tabs ({1}) wait for its first page, at most 5 s, then open at most {2} at once (a tab shown meanwhile opens at once).";

    /// <summary>
    /// The line logged at Info at the release: the first tab's name, the reason, the whole
    /// milliseconds on the restore clock since it began opening, the count still waiting, and
    /// <see cref="OpensAtOnce"/>.
    /// </summary>
    public const string ReleasedFormat = "Restore order: {0}: {1}, {2} ms after it began opening; the other restored tabs ({3}) start opening, at most {4} at once.";

    /// <summary>The release's reason when the first tab's graph has its first page.</summary>
    public const string FirstPageReason = "its graph has its first page";

    /// <summary>The release's reason when the first tab's opening or first load ended without a first page.</summary>
    public const string EndedReason = "its opening ended without a first page";

    /// <summary>The release's reason when the first tab was closed.</summary>
    public const string ClosedReason = "its tab was closed";

    /// <summary>The release's reason when <see cref="LongestWait"/> passed first.</summary>
    public const string WaitedReason = "5 s passed without a first page";

    /// <summary>The line logged at Debug when a waiting tab is shown and opens at once: its name.</summary>
    public const string ShownFormat = "Restore order: {0} was shown while it waited and opens now.";

    /// <summary>The line logged at Debug when a waiting tab is closed: its name.</summary>
    public const string DroppedFormat = "Restore order: {0} was closed while it waited and does not open.";

    /// <summary>The line logged at Debug when the order stops while tabs wait: their count.</summary>
    public const string StoppedFormat = "Restore order: the window closed with {0} restored tabs still waiting; they do not open.";

    /// <summary>The longest the other restored tabs wait for the first one, on the restore clock.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(5);

    private readonly Lock _gate = new();
    private readonly List<Entry> _entries;
    private readonly Entry _first;
    private readonly Func<RepoTabViewModel, string, Task<bool>> _open;
    private readonly TimeProvider _clock;
    private readonly IAppLog _log;
    private readonly CancellationToken _lifetime;
    private readonly TaskCompletionSource _firstLoadEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Cancels the first tab's 5 s at the release or the stop; linked to the lifetime.</summary>
    private CancellationTokenSource? _watch;
    private CancellationTokenRegistration _stopping;
    private Stage _stage = Stage.First;
    private bool _started;
    private bool _openedAny;
    private int _slots;
    private int _unfinished;
    private long _startedAt;

    /// <param name="first">The active restored tab, which opens first.</param>
    /// <param name="restored">Every restored tab in tab order, <paramref name="first"/> among them; each is read once, with its pending folder.</param>
    /// <param name="open">Opens a tab's folder: the window's <c>OpenAsync(path, remember: false)</c>.</param>
    /// <param name="clock">The restore clock, on which <see cref="LongestWait"/> and the release line's milliseconds run.</param>
    /// <param name="log">The session's log.</param>
    /// <param name="lifetime">The window's start-up token: its cancellation stops the order.</param>
    public RestoreOrder(
        RepoTabViewModel first,
        IReadOnlyList<RepoTabViewModel> restored,
        Func<RepoTabViewModel, string, Task<bool>> open,
        TimeProvider clock,
        IAppLog log,
        CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _entries = [.. restored.Select(tab => new Entry(tab, tab.PendingPath ?? throw new ArgumentException("Every restored tab has a pending folder.", nameof(restored))))];
        _first = _entries.FirstOrDefault(entry => ReferenceEquals(entry.Tab, first))
            ?? throw new ArgumentException("The first tab is one of the restored tabs.", nameof(first));
        _open = open;
        _clock = clock;
        _log = log;
        _lifetime = lifetime;
        _unfinished = _entries.Count;
        _watch = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    }

    /// <summary>The order's own course: the first tab opens alone, then the others are released, until it stops.</summary>
    private enum Stage
    {
        First,
        Released,
        Stopped,
    }

    /// <summary>Where a restored tab stands in the order.</summary>
    private enum EntryState
    {
        /// <summary>Not asked to open.</summary>
        Waiting,

        /// <summary>Opened after the release, counted among the <see cref="OpensAtOnce"/>.</summary>
        OpeningWithSlot,

        /// <summary>The first tab, or a waiting tab the user showed.</summary>
        OpeningWithoutSlot,

        /// <summary>Its trigger came: its slot, if it held one, is free; it may still be loading its history.</summary>
        Settled,

        /// <summary>Closed while it waited, or left waiting when the order stopped; never opened.</summary>
        Dropped,
    }

    /// <summary>
    /// Completes when the task of a tab the order opened ends first, whether it showed its graph or
    /// failed (the merge tools' lookup starts there, D202), or at the stop when the order opened none.
    /// A waiting tab's drop does not complete it: that tab never loaded.
    /// </summary>
    public Task FirstLoadEnded => _firstLoadEnded.Task;

    /// <summary>
    /// Starts the order, once: logs it, opens the first tab and arms its <see cref="LongestWait"/>.
    /// Returns a task per restored tab, in tab order, which completes with the outcome of the task
    /// its opening returned, or when the tab is dropped.
    /// </summary>
    public IReadOnlyList<Task> Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                throw new InvalidOperationException("The restore order has started already.");
            }

            _started = true;
        }

        IReadOnlyList<Task> tasks = [.. _entries.Select(entry => entry.Done.Task)];
        _log.Info(Format(StartedFormat, _first.Name, _entries.Count - 1, OpensAtOnce));

        // A lifetime that has ended already stops the order here, and nothing opens.
        var stopping = _lifetime.Register(static order => ((RestoreOrder)order!).Stop(), this);
        Opening opening;
        Task delay;
        lock (_gate)
        {
            _stopping = stopping;
            if (_stage != Stage.First || _first.Tab is not { } tab || _watch is not { } watch)
            {
                return tasks;
            }

            _first.State = EntryState.OpeningWithoutSlot;
            _openedAny = true;
            opening = Watched(_first, tab);

            // The 5 s count from the moment the first tab is asked to open, where its own Q1 clock
            // starts too.
            _startedAt = _clock.GetTimestamp();
            delay = Task.Delay(LongestWait, _clock, watch.Token);
        }

        Open(opening, delay);
        return tasks;
    }

    /// <summary>
    /// A tab was shown (every activation passes here): a waiting tab opens at once, without a slot,
    /// and the others keep waiting. Any other tab is left as it is.
    /// </summary>
    public void Shown(RepoTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Entry entry;
        lock (_gate)
        {
            if (Find(tab) is not { State: EntryState.Waiting } waiting)
            {
                return;
            }

            entry = waiting;
            entry.State = EntryState.OpeningWithoutSlot;
            _openedAny = true;
        }

        _log.Debug(Format(ShownFormat, entry.Name));
        Open(new Opening(entry, tab, null), null);
    }

    /// <summary>
    /// A tab is closing, before it is disposed and before its neighbour is shown: a waiting tab is
    /// dropped and never opens; the first tab before the release releases the others; a released
    /// tab frees its slot. The waiting tabs this lets in have been asked to open when it returns.
    /// The order forgets the tab.
    /// </summary>
    public void Closed(RepoTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Entry? dropped = null;
        Release? release = null;
        var next = new List<Opening>();
        EventHandler? handler;
        lock (_gate)
        {
            if (Find(tab) is not { } entry)
            {
                return;
            }

            switch (entry.State)
            {
                case EntryState.Waiting:
                    entry.State = EntryState.Dropped;
                    dropped = entry;
                    break;
                case EntryState.OpeningWithoutSlot when ReferenceEquals(entry, _first):
                    release = ReleaseLocked(ClosedReason);
                    break;
                case EntryState.OpeningWithSlot:
                    entry.State = EntryState.Settled;
                    _slots--;
                    next = TakeSlotsLocked();
                    break;
            }

            handler = Forget(entry);
        }

        if (handler is not null)
        {
            tab.RepositoryOpened -= handler;
        }

        if (dropped is not null)
        {
            _log.Debug(Format(DroppedFormat, dropped.Name));
            Finish(dropped, null);
        }

        Carry(release);
        OpenAll(next);
    }

    /// <summary>
    /// The window is closing: nothing more opens, every waiting tab is dropped (the first's too, if
    /// it was not asked to open yet) and the 5 s stop. The tabs already opening end as before.
    /// </summary>
    public void Stop()
    {
        var dropped = new List<Entry>();
        var handlers = new List<(RepoTabViewModel Tab, EventHandler Handler)>();
        bool openedNone;
        lock (_gate)
        {
            if (_stage == Stage.Stopped)
            {
                return;
            }

            _stage = Stage.Stopped;
            foreach (var entry in _entries)
            {
                if (entry.State == EntryState.Waiting)
                {
                    entry.State = EntryState.Dropped;
                    dropped.Add(entry);
                    Forget(entry);
                }
                else if (entry is { Handler: { } handler, Tab: { } tab })
                {
                    entry.Handler = null;
                    handlers.Add((tab, handler));
                }
            }

            openedNone = !_openedAny;
        }

        foreach (var (tab, handler) in handlers)
        {
            tab.RepositoryOpened -= handler;
        }

        if (dropped.Count > 0)
        {
            _log.Debug(Format(StoppedFormat, dropped.Count));
        }

        CancelWatch();
        foreach (var entry in dropped)
        {
            Finish(entry, null);
        }

        if (openedNone)
        {
            _firstLoadEnded.TrySetResult();
        }
    }

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.InvariantCulture, format, values);

    /// <summary>The entry that still holds <paramref name="tab"/>; null once it forgot it. Under the lock.</summary>
    private Entry? Find(RepoTabViewModel tab) => _entries.Find(entry => ReferenceEquals(entry.Tab, tab));

    /// <summary>An opening the order watches for its trigger: its <c>RepositoryOpened</c> is heard from now on. Under the lock.</summary>
    private static Opening Watched(Entry entry, RepoTabViewModel tab)
    {
        var opened = new TaskCompletionSource<RepositoryViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        entry.Opened = opened;
        return new Opening(entry, tab, opened);
    }

    /// <summary>
    /// The entry lets go of its tab, the view model it showed and its handler, which is returned for
    /// the caller to remove from the tab outside the lock. Under the lock.
    /// </summary>
    private static EventHandler? Forget(Entry entry)
    {
        var handler = entry.Handler;
        entry.Handler = null;
        entry.Tab = null;
        entry.Opened = null;
        return handler;
    }

    /// <summary>
    /// The release, once: the first tab is settled, the line is made and the slots are filled in tab
    /// order. Null when the order was released or stopped already. Under the lock.
    /// </summary>
    private Release? ReleaseLocked(string reason)
    {
        if (_stage != Stage.First)
        {
            return null;
        }

        _stage = Stage.Released;
        if (_first.State == EntryState.OpeningWithoutSlot)
        {
            _first.State = EntryState.Settled;
        }

        (RepoTabViewModel Tab, EventHandler Handler)? detached = _first is { Handler: { } handler, Tab: { } tab } ? (tab, handler) : null;
        _first.Handler = null;
        _first.Opened = null;
        var waiting = _entries.Count(entry => entry.State == EntryState.Waiting);
        var milliseconds = (long)_clock.GetElapsedTime(_startedAt).TotalMilliseconds;
        var line = Format(ReleasedFormat, _first.Name, reason, milliseconds, waiting, OpensAtOnce);
        return new Release(line, detached, TakeSlotsLocked());
    }

    /// <summary>Carries out a release outside the lock: the line first, then the 5 s stopped, then the openings.</summary>
    private void Carry(Release? release)
    {
        if (release is null)
        {
            return;
        }

        if (release.Detached is var (tab, handler))
        {
            tab.RepositoryOpened -= handler;
        }

        _log.Info(release.Line);
        CancelWatch();
        OpenAll(release.Next);
    }

    /// <summary>The waiting tabs that take the free slots now, in tab order, each marked as opening. Under the lock.</summary>
    private List<Opening> TakeSlotsLocked()
    {
        var next = new List<Opening>();
        if (_stage != Stage.Released)
        {
            return next;
        }

        foreach (var entry in _entries)
        {
            if (_slots >= OpensAtOnce)
            {
                break;
            }

            if (entry is { State: EntryState.Waiting, Tab: { } tab })
            {
                entry.State = EntryState.OpeningWithSlot;
                _slots++;
                _openedAny = true;
                next.Add(Watched(entry, tab));
            }
        }

        return next;
    }

    private void OpenAll(List<Opening> openings)
    {
        foreach (var opening in openings)
        {
            Open(opening, null);
        }
    }

    /// <summary>
    /// Asks for one tab's opening, outside the lock. A watched opening hears the tab's
    /// <c>RepositoryOpened</c> first, since a provider that has answered already reaches it inside
    /// the call; its trigger is then waited for.
    /// </summary>
    private void Open(Opening opening, Task? delay)
    {
        var entry = opening.Entry;
        if (opening.Opened is not null)
        {
            EventHandler handler = (sender, _) => OnRepositoryOpened(entry, sender as RepoTabViewModel);
            var subscribe = false;
            lock (_gate)
            {
                if (ReferenceEquals(entry.Opened, opening.Opened))
                {
                    entry.Handler = handler;
                    subscribe = true;
                }
            }

            if (subscribe)
            {
                opening.Tab.RepositoryOpened += handler;
            }
        }

        Task<bool> task;
        try
        {
            task = _open(opening.Tab, entry.Folder);
        }
        catch (Exception ex)
        {
            task = Task.FromException<bool>(ex);
        }

        _ = FollowAsync(entry, task);
        if (opening.Opened is { } opened)
        {
            _ = WatchAsync(entry, task, opened.Task, delay);
        }
    }

    /// <summary>The tab shows the view model it opened: the order takes it, to wait for its first page, and stops listening.</summary>
    private void OnRepositoryOpened(Entry entry, RepoTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        TaskCompletionSource<RepositoryViewModel>? opened = null;
        EventHandler? handler;
        lock (_gate)
        {
            handler = entry.Handler;
            entry.Handler = null;
            if (handler is not null && entry.State is EntryState.OpeningWithSlot or EntryState.OpeningWithoutSlot)
            {
                opened = entry.Opened;
            }
        }

        if (handler is not null)
        {
            tab.RepositoryOpened -= handler;
        }

        if (opened is not null && tab.Repository is { } repository)
        {
            opened.TrySetResult(repository);
        }
    }

    /// <summary>
    /// Waits for a watched tab's trigger: its view model's first page, its opening's end, or, for
    /// the first tab, <paramref name="delay"/>. The first tab's trigger releases the others; a
    /// released tab's frees its slot. A delay that ended by its cancellation is no trigger: the
    /// release or the stop that cancelled it has acted already.
    /// </summary>
    private async Task WatchAsync(Entry entry, Task<bool> opening, Task<RepositoryViewModel> opened, Task? delay)
    {
        var ended = await Task.WhenAny(Waits(opening, opened, delay));
        if (ended == opened)
        {
            ended = await Task.WhenAny(Waits(opening, opened.Result.FirstPage, delay));
        }

        string reason;
        if (delay is not null && ended == delay)
        {
            if (!delay.IsCompletedSuccessfully)
            {
                return;
            }

            reason = WaitedReason;
        }
        else
        {
            reason = opened.IsCompletedSuccessfully && opened.Result.Graph.Count > 0 ? FirstPageReason : EndedReason;
        }

        if (ReferenceEquals(entry, _first))
        {
            Release? release;
            lock (_gate)
            {
                release = ReleaseLocked(reason);
            }

            Carry(release);
            return;
        }

        List<Opening> next;
        lock (_gate)
        {
            if (entry.State != EntryState.OpeningWithSlot)
            {
                return;
            }

            entry.State = EntryState.Settled;
            _slots--;
            next = TakeSlotsLocked();
        }

        OpenAll(next);

        static Task[] Waits(Task first, Task second, Task? delay) => delay is null ? [first, second] : [first, second, delay];
    }

    /// <summary>
    /// Follows an opening to its end: the tab's task in <see cref="Start"/>'s list takes its outcome,
    /// <see cref="FirstLoadEnded"/> completes if no opening ended before, and the entry forgets the tab.
    /// </summary>
    private async Task FollowAsync(Entry entry, Task<bool> opening)
    {
        await ((Task)opening).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        RepoTabViewModel? tab;
        EventHandler? handler;
        lock (_gate)
        {
            tab = entry.Tab;
            handler = Forget(entry);
        }

        if (tab is not null && handler is not null)
        {
            tab.RepositoryOpened -= handler;
        }

        _firstLoadEnded.TrySetResult();
        Finish(entry, opening);
    }

    /// <summary>
    /// Completes an entry's task, once: with its opening's outcome, or at once for a dropped entry.
    /// When the last has completed, the order lets go of the lifetime.
    /// </summary>
    private void Finish(Entry entry, Task<bool>? opening)
    {
        bool last;
        lock (_gate)
        {
            if (entry.Finished)
            {
                return;
            }

            entry.Finished = true;
            last = --_unfinished == 0;
        }

        if (opening is null)
        {
            entry.Done.TrySetResult();
        }
        else
        {
            entry.Done.TrySetFromTask(opening);
        }

        if (last)
        {
            _stopping.Unregister();
            CancelWatch();
        }
    }

    /// <summary>Ends the first tab's 5 s, once, and lets the lifetime go of the source linked to it.</summary>
    private void CancelWatch()
    {
        CancellationTokenSource? watch;
        lock (_gate)
        {
            (watch, _watch) = (_watch, null);
        }

        if (watch is null)
        {
            return;
        }

        watch.Cancel();
        watch.Dispose();
    }

    /// <summary>A restored tab in the order.</summary>
    private sealed class Entry(RepoTabViewModel tab, string folder)
    {
        /// <summary>The tab, until it is closed or its opening's task has ended.</summary>
        public RepoTabViewModel? Tab { get; set; } = tab;

        /// <summary>The tab's pending folder when the order was made.</summary>
        public string Folder { get; } = folder;

        /// <summary>The folder's name, as the tab strip shows it.</summary>
        public string Name { get; } = WorkspaceTexts.FolderName(folder);

        public EntryState State { get; set; } = EntryState.Waiting;

        /// <summary>The task in <see cref="Start"/>'s list.</summary>
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The <c>RepositoryOpened</c> handler on the tab while the order listens.</summary>
        public EventHandler? Handler { get; set; }

        /// <summary>The view model the tab showed, for a watched opening until its trigger.</summary>
        public TaskCompletionSource<RepositoryViewModel>? Opened { get; set; }

        public bool Finished { get; set; }
    }

    /// <summary>A tab to open outside the lock, and the source its view model is handed to when the order watches it.</summary>
    private readonly record struct Opening(Entry Entry, RepoTabViewModel Tab, TaskCompletionSource<RepositoryViewModel>? Opened);

    /// <summary>A release decided under the lock: its line, the first tab's handler to remove, and the tabs to open.</summary>
    private sealed record Release(string Line, (RepoTabViewModel Tab, EventHandler Handler)? Detached, List<Opening> Next);
}
