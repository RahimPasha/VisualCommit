using System.Diagnostics;
using System.Threading.Channels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Watches a repository for changes made outside the app (D52, D71): its git folder, where refs,
/// HEAD, the index and the stash live, and its working tree, where files are edited.
/// <para>
/// In the git folder, changes under <c>objects/</c> and to <c>*.lock</c> files are left out: git
/// writes objects and lock files on the way to the change that matters (a ref, HEAD, the index),
/// which follows. In the working tree, changes under <c>.git</c> are left to the git folder's
/// watcher, and a burst of changes whose paths git reports as all ignored is dropped (build
/// output, dependency folders). The rest is debounced: <see cref="Changed"/> is raised once the
/// changes have been quiet for the quiet period, and while they keep coming at least once per
/// maximum delay, so a long operation still shows progress. A lost event (the system's buffer
/// overflowed) counts as a change in both. While the app writes, it pauses the watcher
/// (<see cref="Pause"/>) and refreshes once itself.
/// </para>
/// <para>
/// A test switch can hold every event for a lag before the watcher looks at it (R8, D262): then
/// the pause, the folder check and the counting happen at delivery, late, as macOS reports a
/// write a while after it.
/// </para>
/// </summary>
public sealed class RepositoryWatcher : IRepositoryWatcher
{
    public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromSeconds(2);

    /// <summary>Beyond this many changed paths in one burst the ignore check is skipped: the burst counts as a change.</summary>
    private const int MaxPathsToCheck = 500;

    private readonly Lock _gate = new();
    private readonly Lock _raiseGate = new();
    private readonly IReadOnlyList<string> _folders;
    private readonly string? _workingDirectory;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<bool>>? _allIgnored;
    private readonly IAppLog _log;
    private readonly TimeProvider _time;
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maximumDelay;
    private readonly ITimer _timer;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _changedPaths = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary><see cref="_lifetime"/>'s token, taken at construction: the held events' consumer still reads it once the source is disposed.</summary>
    private readonly CancellationToken _lifetimeToken;

    /// <summary>How long each event is held before it is looked at; zero: none.</summary>
    private readonly TimeSpan _eventLag;

    /// <summary>The events held for <see cref="_eventLag"/>, in arrival order; null without a lag.</summary>
    private readonly Channel<HeldEvent>? _held;
    private bool _started;
    private volatile bool _disposed;

    /// <summary>The time (a <see cref="TimeProvider.GetTimestamp"/>) of the burst's latest change.</summary>
    private long _lastChange;

    /// <summary>The time of the burst's first change; null while no burst is waiting.</summary>
    private long? _firstChange;
    private bool _gitFolderChanged;
    private bool _workingTreeChanged;
    private bool _tooManyPaths;
    private int _pauses;

    /// <summary>Bursts taken off the wait whose raise has not ended yet (the ignore check, <see cref="Changed"/>'s handlers).</summary>
    private int _raising;

    /// <param name="gitDirectory">The repository's git folder.</param>
    /// <param name="commonDirectory">The git folder with the refs and objects; for a linked worktree it differs from <paramref name="gitDirectory"/>.</param>
    /// <param name="log">Where failures to watch are reported.</param>
    /// <param name="quietPeriod">How long changes must have stopped before <see cref="Changed"/> is raised. Default 300 ms.</param>
    /// <param name="maximumDelay">The longest a change waits while further changes keep coming. Default 2 seconds.</param>
    /// <param name="workingDirectory">The working tree to watch as well, or null to watch the git folder only.</param>
    /// <param name="allIgnored">
    /// Whether git ignores every one of these paths (relative to the working tree, with forward
    /// slashes). A burst of working-tree changes for which it says yes is dropped. Null: none is.
    /// </param>
    /// <param name="time">
    /// The clock the quiet period and the maximum delay are measured on, and whose timer raises
    /// <see cref="Changed"/>. Null: the system's, whose timer raises it on the thread pool. A test
    /// passes a clock it moves by hand, so that a slow machine cannot split a burst.
    /// </param>
    /// <param name="eventLag">
    /// How long every file-system event is held before the watcher looks at it, on the system's
    /// clock, never on <paramref name="time"/> (R8, D262: <c>GitRepository.WatcherLag</c>). Null
    /// or not positive: none, and the events are handled as they come.
    /// </param>
    public RepositoryWatcher(
        string gitDirectory,
        string commonDirectory,
        IAppLog? log = null,
        TimeSpan? quietPeriod = null,
        TimeSpan? maximumDelay = null,
        string? workingDirectory = null,
        Func<IReadOnlyList<string>, CancellationToken, Task<bool>>? allIgnored = null,
        TimeProvider? time = null,
        TimeSpan? eventLag = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonDirectory);
        _folders = FoldersToWatch(gitDirectory, commonDirectory);
        _workingDirectory = workingDirectory is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        _allIgnored = allIgnored;
        _log = log ?? NullAppLog.Instance;
        _time = time ?? TimeProvider.System;

        // Whole milliseconds, at least one, as the timer counts them.
        var quietMilliseconds = Math.Max(1, (long)(quietPeriod ?? DefaultQuietPeriod).TotalMilliseconds);
        _quietPeriod = TimeSpan.FromMilliseconds(quietMilliseconds);
        _maximumDelay = TimeSpan.FromMilliseconds(Math.Max(quietMilliseconds, (long)(maximumDelay ?? DefaultMaximumDelay).TotalMilliseconds));
        _timer = _time.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _lifetimeToken = _lifetime.Token;
        if (eventLag is { } lag && lag > TimeSpan.Zero)
        {
            _eventLag = lag;
            _held = Channel.CreateUnbounded<HeldEvent>(new UnboundedChannelOptions { SingleReader = true });
        }
    }

    public event EventHandler<RepositoryChangedEventArgs>? Changed;

    /// <summary>
    /// For tests: called with the name of each event (relative to the folder that reported it;
    /// null when the system did not say, or events were lost) once the event has been counted in
    /// the burst that waits, outside the watcher's locks, on the thread that reported it. An event
    /// the watcher leaves out, or that comes while it is paused or after it is disposed, is not
    /// passed on. A test that holds the clock waits for this to know that every event of a burst
    /// has arrived before it moves the clock.
    /// </summary>
    internal Action<string?>? EventCounted { get; set; }

    /// <summary>The git folders being watched: the git folder, and the common folder when it does not contain the git folder.</summary>
    public IReadOnlyList<string> Folders => _folders;

    /// <summary>
    /// Whether no change the watcher has counted is waiting: none waits for its quiet period, and
    /// no burst is being raised (its ignore check or <see cref="Changed"/>'s handlers running). A
    /// change the system has not reported yet is not known here: macOS reports a write a while
    /// after it. The harnesses read it to wait for the refresh a change starts before they mark
    /// the calls of a check's next step.
    /// </summary>
    public bool IsQuiet
    {
        get
        {
            lock (_gate)
            {
                return _firstChange is null && _raising == 0;
            }
        }
    }

    /// <summary>
    /// Starts watching. A folder that cannot be watched (it is gone, or on Linux the system's
    /// limit on watches is reached) is logged and left out; this never throws. On Linux, watching
    /// a folder adds a watch for each folder in it, which takes a while in a large working tree:
    /// call this off the UI thread. <see cref="Pause"/> and <see cref="Dispose"/> do not wait for it.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            if (_held is not null)
            {
                _ = Task.Run(() => DeliverHeldAsync(_held.Reader, _lifetimeToken));
            }
        }

        var watchers = new List<FileSystemWatcher>();
        foreach (var folder in _folders)
        {
            Watch(folder, OnGitFolderEvent, watchers);
        }

        if (_workingDirectory is not null)
        {
            Watch(_workingDirectory, OnWorkingTreeEvent, watchers);
        }

        lock (_gate)
        {
            if (!_disposed)
            {
                _watchers.AddRange(watchers);
                return;
            }
        }

        // Disposed while starting.
        foreach (var watcher in watchers)
        {
            watcher.Dispose();
        }
    }

    /// <summary>
    /// Stops reporting changes until the returned object is disposed: the app's own write is
    /// running, and the app refreshes once when it ends (D71). Changes that happen meanwhile are
    /// not reported later; the app's refresh reads them.
    /// </summary>
    public IDisposable Pause()
    {
        lock (_gate)
        {
            _pauses++;
            ResetBurst();
        }

        return new Resume(this);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _timer.Dispose();
            _held?.Writer.TryComplete();
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    /// <summary>
    /// Whether a change at <paramref name="relativePath"/> (relative to a watched git folder) can
    /// matter to the views: anything but objects, lock files and git's file-system monitor. The
    /// monitor (<c>core.fsmonitor</c>) writes a cookie file into the git folder for every
    /// <c>git status</c>, so the app's own reads would otherwise refresh it without end.
    /// </summary>
    internal static bool IsRelevant(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return true; // The system did not say what changed.
        }

        var path = relativePath.Replace('\\', '/');
        if (path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(IsUnder(path, "objects") || IsUnder(path, "fsmonitor--daemon") || path.Equals("fsmonitor--daemon.ipc", StringComparison.OrdinalIgnoreCase));

        static bool IsUnder(string path, string folder) =>
            path.Equals(folder, StringComparison.OrdinalIgnoreCase)
            || (path.Length > folder.Length && path[folder.Length] == '/' && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a change at <paramref name="relativePath"/> (relative to the working tree) is in its <c>.git</c>, which the git folder's watcher covers.</summary>
    internal static bool IsInGitFolder(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        return path.Equals(".git", StringComparison.OrdinalIgnoreCase) || path.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The git folder of a linked worktree lies inside the common folder, and watching the common
    /// folder with its subfolders covers both; two watchers would only report everything twice.
    /// </summary>
    private static List<string> FoldersToWatch(string gitDirectory, string commonDirectory)
    {
        var git = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gitDirectory));
        var common = Path.TrimEndingDirectorySeparator(Path.GetFullPath(commonDirectory));
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(git, common, comparison) || IsInside(git, common, comparison))
        {
            return [common];
        }

        return IsInside(common, git, comparison) ? [git] : [git, common];
    }

    private static bool IsInside(string path, string folder, StringComparison comparison) =>
        path.Length > folder.Length
        && path.StartsWith(folder, comparison)
        && (path[folder.Length] == Path.DirectorySeparatorChar || path[folder.Length] == Path.AltDirectorySeparatorChar);

    private void Watch(string folder, FileSystemEventHandler onEvent, List<FileSystemWatcher> watchers)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                InternalBufferSize = 64 * 1024,
            };
            if (_held is null)
            {
                watcher.Created += onEvent;
                watcher.Changed += onEvent;
                watcher.Deleted += onEvent;
                watcher.Renamed += (sender, e) => onEvent(sender, e);
                watcher.Error += OnError;
            }
            else
            {
                watcher.Created += (sender, e) => Hold(() => onEvent(sender, e));
                watcher.Changed += (sender, e) => Hold(() => onEvent(sender, e));
                watcher.Deleted += (sender, e) => Hold(() => onEvent(sender, e));
                watcher.Renamed += (sender, e) => Hold(() => onEvent(sender, e));
                watcher.Error += (sender, e) => Hold(() => OnError(sender, e));
            }

            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
        }
        catch (Exception ex)
        {
            watcher?.Dispose();
            _log.Warning($"Cannot watch {folder} for changes; changes made outside the app will not show until the repository is refreshed.", ex);
        }
    }

    /// <summary>Holds an event for the lag: <paramref name="deliver"/> runs once it has passed. Once disposed, nothing is held.</summary>
    private void Hold(Action deliver) =>
        _held!.Writer.TryWrite(new HeldEvent(Stopwatch.GetTimestamp() + (long)(_eventLag.TotalSeconds * Stopwatch.Frequency), deliver));

    /// <summary>
    /// The held events' one consumer, on the thread pool: each in arrival order once its lag has
    /// passed. It stops when the watcher is disposed and drops what it holds.
    /// </summary>
    private async Task DeliverHeldAsync(ChannelReader<HeldEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var held))
                {
                    // A delay may end a little early; it is waited again until the event is due.
                    var left = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), held.Due);
                    while (left > TimeSpan.Zero)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(left.TotalMilliseconds)), cancellationToken).ConfigureAwait(false);
                        left = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), held.Due);
                    }

                    if (_disposed)
                    {
                        return;
                    }

                    try
                    {
                        held.Deliver();
                    }
                    catch (Exception ex)
                    {
                        _log.Error("A held event of the repository watcher failed.", ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed: what is still held is dropped.
        }
    }

    private void OnGitFolderEvent(object sender, FileSystemEventArgs e)
    {
        // A folder "changes" when an entry in it is created, deleted or renamed (Windows reports
        // that as a new write time). The entry's own event says whether it matters; the folder's
        // would turn every lock file into a change.
        if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
        {
            return;
        }

        var relevant = IsRelevant(e.Name) || (e is RenamedEventArgs renamed && IsRelevant(renamed.OldName));
        if (relevant && Signal(gitFolder: true, null))
        {
            EventCounted?.Invoke(e.Name);
        }
    }

    private void OnWorkingTreeEvent(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
        {
            return;
        }

        foreach (var name in e is RenamedEventArgs renamed ? new[] { e.Name, renamed.OldName } : [e.Name])
        {
            var counted = false;
            if (string.IsNullOrEmpty(name))
            {
                counted = Signal(gitFolder: false, string.Empty);
            }
            else if (!IsInGitFolder(name))
            {
                counted = Signal(gitFolder: false, name.Replace('\\', '/'));
            }

            if (counted)
            {
                EventCounted?.Invoke(name);
            }
        }
    }

    private void OnError(object? sender, ErrorEventArgs e)
    {
        // Mostly a buffer overflow: events were lost, so anything may have changed.
        _log.Debug($"The repository watcher lost events: {e.GetException().Message}");
        if (Signal(gitFolder: true, string.Empty))
        {
            EventCounted?.Invoke(null);
        }
    }

    /// <param name="gitFolder">The change is in a git folder.</param>
    /// <param name="workingTreePath">The working-tree path that changed; empty when the system did not say; null for a git-folder change alone.</param>
    /// <returns>Whether the change was counted: false while paused or once disposed.</returns>
    private bool Signal(bool gitFolder, string? workingTreePath)
    {
        lock (_gate)
        {
            if (_disposed || _pauses > 0)
            {
                return false;
            }

            _gitFolderChanged |= gitFolder;
            if (workingTreePath is not null)
            {
                _workingTreeChanged = true;
                if (workingTreePath.Length == 0 || _changedPaths.Count >= MaxPathsToCheck)
                {
                    _tooManyPaths = true;
                }
                else
                {
                    _changedPaths.Add(workingTreePath);
                }
            }

            var now = _time.GetTimestamp();
            _lastChange = now;
            if (_firstChange is null)
            {
                _firstChange = now;
                _timer.Change(_quietPeriod, Timeout.InfiniteTimeSpan);
            }

            return true;
        }
    }

    private void ResetBurst()
    {
        _firstChange = null;
        _gitFolderChanged = false;
        _workingTreeChanged = false;
        _tooManyPaths = false;
        _changedPaths.Clear();
    }

    private void OnTimer(object? state)
    {
        bool gitFolder;
        bool workingTree;
        List<string> paths;
        bool tooMany;
        lock (_gate)
        {
            if (_disposed || _firstChange is not { } firstChange)
            {
                return;
            }

            var now = _time.GetTimestamp();
            var quietFor = _time.GetElapsedTime(_lastChange, now);
            var waitingFor = _time.GetElapsedTime(firstChange, now);
            if (quietFor < _quietPeriod && waitingFor < _maximumDelay)
            {
                var wait = TimeSpan.FromTicks(Math.Min((_quietPeriod - quietFor).Ticks, (_maximumDelay - waitingFor).Ticks));

                // Rounded up to whole milliseconds, at least one: the timer counts whole
                // milliseconds, and a wait it cut short would only wake this again too early.
                _timer.Change(TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(wait.TotalMilliseconds))), Timeout.InfiniteTimeSpan);
                return;
            }

            gitFolder = _gitFolderChanged;
            workingTree = _workingTreeChanged;
            paths = [.. _changedPaths];
            tooMany = _tooManyPaths;
            ResetBurst();

            // Counted before the burst leaves the wait, so that IsQuiet is never true in between.
            _raising++;
        }

        try
        {
            // One raise at a time: a handler that takes longer than the quiet period is not run
            // again beside itself by the next burst.
            lock (_raiseGate)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    if (workingTree && !gitFolder && !tooMany && paths.Count > 0 && AllIgnored(paths))
                    {
                        return;
                    }

                    Changed?.Invoke(this, new RepositoryChangedEventArgs(gitFolder, workingTree));
                }
                catch (OperationCanceledException) when (_disposed)
                {
                }
                catch (Exception ex)
                {
                    // An exception here would end the process: this runs on a timer thread.
                    _log.Error("A handler of the repository watcher's Changed event failed.", ex);
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _raising--;
            }
        }
    }

    /// <summary>Whether git ignores every path of the burst. When git cannot say, the burst counts as a change.</summary>
    private bool AllIgnored(List<string> paths)
    {
        if (_allIgnored is null)
        {
            return false;
        }

        try
        {
            return _allIgnored(paths, _lifetime.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Debug($"Could not ask git which changed files it ignores: {ex.Message}");
            return false;
        }
    }

    /// <summary>An event held for the lag: when it is due (a <see cref="Stopwatch.GetTimestamp"/>) and the handler it goes to.</summary>
    private readonly record struct HeldEvent(long Due, Action Deliver);

    private sealed class Resume(RepositoryWatcher watcher) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            lock (watcher._gate)
            {
                watcher._pauses--;
            }
        }
    }
}
