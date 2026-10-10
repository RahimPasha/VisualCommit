using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Watches a repository's git folder for changes made outside the app, such as a commit or a
/// checkout in a terminal (D52). Refs, HEAD and the stash all live there; the working tree is not
/// watched until phase 2 shows it.
/// <para>
/// Changes under <c>objects/</c> and to <c>*.lock</c> files are left out: git writes objects and
/// lock files on the way to the change that matters (a ref, HEAD, the index), which follows. The
/// rest is debounced: <see cref="Changed"/> is raised once the changes have been quiet for the
/// quiet period, and while they keep coming at least once per maximum delay, so a long operation
/// still shows progress. A lost event (the system's buffer overflowed) counts as a change.
/// </para>
/// </summary>
public sealed class RepositoryWatcher : IRepositoryWatcher
{
    public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private readonly Lock _raiseGate = new();
    private readonly IReadOnlyList<string> _folders;
    private readonly IAppLog _log;
    private readonly long _quietMilliseconds;
    private readonly long _maximumDelayMilliseconds;
    private readonly Timer _timer;
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _started;
    private volatile bool _disposed;
    private long _lastChange;
    private long? _firstChange;

    /// <param name="gitDirectory">The repository's git folder.</param>
    /// <param name="commonDirectory">The git folder with the refs and objects; for a linked worktree it differs from <paramref name="gitDirectory"/>.</param>
    /// <param name="log">Where failures to watch are reported.</param>
    /// <param name="quietPeriod">How long changes must have stopped before <see cref="Changed"/> is raised. Default 300 ms.</param>
    /// <param name="maximumDelay">The longest a change waits while further changes keep coming. Default 2 seconds.</param>
    public RepositoryWatcher(
        string gitDirectory,
        string commonDirectory,
        IAppLog? log = null,
        TimeSpan? quietPeriod = null,
        TimeSpan? maximumDelay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonDirectory);
        _folders = FoldersToWatch(gitDirectory, commonDirectory);
        _log = log ?? NullAppLog.Instance;
        _quietMilliseconds = Math.Max(1, (long)(quietPeriod ?? DefaultQuietPeriod).TotalMilliseconds);
        _maximumDelayMilliseconds = Math.Max(_quietMilliseconds, (long)(maximumDelay ?? DefaultMaximumDelay).TotalMilliseconds);
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler? Changed;

    /// <summary>The folders being watched: the git folder, and the common folder when it does not contain the git folder.</summary>
    public IReadOnlyList<string> Folders => _folders;

    /// <summary>
    /// Starts watching. A folder that cannot be watched (it is gone, or on Linux the system's
    /// limit on watches is reached) is logged and left out; this never throws.
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
            foreach (var folder in _folders)
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
                    watcher.Created += OnFileEvent;
                    watcher.Changed += OnFileEvent;
                    watcher.Deleted += OnFileEvent;
                    watcher.Renamed += OnFileEvent;
                    watcher.Error += OnError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    watcher?.Dispose();
                    _log.Warning($"Cannot watch {folder} for changes; changes made outside the app will not show until the repository is refreshed.", ex);
                }
            }
        }
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
                watcher.Created -= OnFileEvent;
                watcher.Changed -= OnFileEvent;
                watcher.Deleted -= OnFileEvent;
                watcher.Renamed -= OnFileEvent;
                watcher.Error -= OnError;
                watcher.Dispose();
            }

            _watchers.Clear();
            _timer.Dispose();
        }
    }

    /// <summary>
    /// Whether a change at <paramref name="relativePath"/> (relative to a watched git folder) can
    /// matter to the views: anything but objects and lock files.
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

        return !(path.Equals("objects", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("objects/", StringComparison.OrdinalIgnoreCase));
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

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // A folder "changes" when an entry in it is created, deleted or renamed (Windows reports
        // that as a new write time). The entry's own event says whether it matters; the folder's
        // would turn every lock file into a change.
        if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
        {
            return;
        }

        var relevant = IsRelevant(e.Name) || (e is RenamedEventArgs renamed && IsRelevant(renamed.OldName));
        if (relevant)
        {
            Signal();
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Mostly a buffer overflow: events were lost, so something may have changed.
        _log.Debug($"The repository watcher lost events: {e.GetException().Message}");
        Signal();
    }

    private void Signal()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = Environment.TickCount64;
            _lastChange = now;
            if (_firstChange is null)
            {
                _firstChange = now;
                _timer.Change(_quietMilliseconds, Timeout.Infinite);
            }
        }
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _firstChange is not { } firstChange)
            {
                return;
            }

            var now = Environment.TickCount64;
            var quietFor = now - _lastChange;
            var waitingFor = now - firstChange;
            if (quietFor < _quietMilliseconds && waitingFor < _maximumDelayMilliseconds)
            {
                var wait = Math.Min(_quietMilliseconds - quietFor, _maximumDelayMilliseconds - waitingFor);
                _timer.Change(Math.Max(1, wait), Timeout.Infinite);
                return;
            }

            _firstChange = null;
        }

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
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // An exception here would end the process: this runs on a timer thread.
                _log.Error("A handler of the repository watcher's Changed event failed.", ex);
            }
        }
    }
}
