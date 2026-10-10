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
    private readonly long _quietMilliseconds;
    private readonly long _maximumDelayMilliseconds;
    private readonly Timer _timer;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _changedPaths = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _started;
    private volatile bool _disposed;
    private long _lastChange;
    private long? _firstChange;
    private bool _gitFolderChanged;
    private bool _workingTreeChanged;
    private bool _tooManyPaths;
    private int _pauses;

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
    public RepositoryWatcher(
        string gitDirectory,
        string commonDirectory,
        IAppLog? log = null,
        TimeSpan? quietPeriod = null,
        TimeSpan? maximumDelay = null,
        string? workingDirectory = null,
        Func<IReadOnlyList<string>, CancellationToken, Task<bool>>? allIgnored = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonDirectory);
        _folders = FoldersToWatch(gitDirectory, commonDirectory);
        _workingDirectory = workingDirectory is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        _allIgnored = allIgnored;
        _log = log ?? NullAppLog.Instance;
        _quietMilliseconds = Math.Max(1, (long)(quietPeriod ?? DefaultQuietPeriod).TotalMilliseconds);
        _maximumDelayMilliseconds = Math.Max(_quietMilliseconds, (long)(maximumDelay ?? DefaultMaximumDelay).TotalMilliseconds);
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<RepositoryChangedEventArgs>? Changed;

    /// <summary>The git folders being watched: the git folder, and the common folder when it does not contain the git folder.</summary>
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
                Watch(folder, OnGitFolderEvent);
            }

            if (_workingDirectory is not null)
            {
                Watch(_workingDirectory, OnWorkingTreeEvent);
            }
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
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
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

    private void Watch(string folder, FileSystemEventHandler onEvent)
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
            watcher.Created += onEvent;
            watcher.Changed += onEvent;
            watcher.Deleted += onEvent;
            watcher.Renamed += (sender, e) => onEvent(sender, e);
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
        if (relevant)
        {
            Signal(gitFolder: true, null);
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
            if (string.IsNullOrEmpty(name))
            {
                Signal(gitFolder: false, string.Empty);
            }
            else if (!IsInGitFolder(name))
            {
                Signal(gitFolder: false, name.Replace('\\', '/'));
            }
        }
    }

    private void OnError(object? sender, ErrorEventArgs e)
    {
        // Mostly a buffer overflow: events were lost, so anything may have changed.
        _log.Debug($"The repository watcher lost events: {e.GetException().Message}");
        Signal(gitFolder: true, string.Empty);
    }

    /// <param name="gitFolder">The change is in a git folder.</param>
    /// <param name="workingTreePath">The working-tree path that changed; empty when the system did not say; null for a git-folder change alone.</param>
    private void Signal(bool gitFolder, string? workingTreePath)
    {
        lock (_gate)
        {
            if (_disposed || _pauses > 0)
            {
                return;
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

            var now = Environment.TickCount64;
            _lastChange = now;
            if (_firstChange is null)
            {
                _firstChange = now;
                _timer.Change(_quietMilliseconds, Timeout.Infinite);
            }
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

            var now = Environment.TickCount64;
            var quietFor = now - _lastChange;
            var waitingFor = now - firstChange;
            if (quietFor < _quietMilliseconds && waitingFor < _maximumDelayMilliseconds)
            {
                var wait = Math.Min(_quietMilliseconds - quietFor, _maximumDelayMilliseconds - waitingFor);
                _timer.Change(Math.Max(1, wait), Timeout.Infinite);
                return;
            }

            gitFolder = _gitFolderChanged;
            workingTree = _workingTreeChanged;
            paths = [.. _changedPaths];
            tooMany = _tooManyPaths;
            ResetBurst();
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
