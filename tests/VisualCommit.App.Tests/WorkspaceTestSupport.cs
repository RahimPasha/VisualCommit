using System.Diagnostics;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the workspace tests share (docs/phase-6-design.md 1.12): a session's services on a store in
/// memory with real git behind the provider and a fake folder dialog, a host that records what it
/// is asked, a tree of real repositories, and a folder link as Windows and the others make one.
/// </summary>
internal static class WorkspaceTestSupport
{
    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    /// <summary>A session's services with <paramref name="store"/>, real git, a fake folder dialog and a recording host.</summary>
    public static Session NewSession(IWorkspaceStore? store = null, IRepositoryProvider? provider = null)
    {
        var calls = new GitCallLog();
        var log = new RecordingLog();
        var folders = new FakeFolderPicker();
        var repositories = provider ?? new RealRepositories(calls, log);
        var settings = new MainWindowViewModelTests.FakeSettings();
        var services = new TabServices(repositories, folders, settings, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc), log, gitCalls: calls, workspaces: store ?? new InMemoryWorkspaceStore());
        var host = new RecordingHost();
        services.WorkspaceHost = host;
        return new Session(services, folders, host, calls, log);
    }

    /// <summary>
    /// The working tree's top of the repository at <paramref name="folder"/> as git reports it and
    /// the app saves it (<c>IGitRepository.WorkingDirectory</c>), opened by a provider of its own so
    /// that no session's call log or app log sees it. Compare a saved or answered path with this,
    /// never with the folder's own text: on macOS the temporary folder's <c>/var/…</c> is git's
    /// <c>/private/var/…</c>, which <c>WorkspacePaths.Same</c> tells apart (part W's conventions).
    /// </summary>
    public static async Task<string> GitTopAsync(string folder) =>
        (await new RealRepositories(new GitCallLog(), NullAppLog.Instance).OpenAsync(folder)).WorkingDirectory;

    /// <summary>A plain folder with repositories in it, made with real git (<see cref="Tree"/>).</summary>
    public static async Task<Tree> TreeAsync()
    {
        var first = await TempRepo.CreateAsync("api");
        await first.CommitFileAsync("README.md", "api\n", "Initial commit");
        return new Tree(first);
    }

    /// <summary>
    /// Makes <paramref name="link"/> a link to the folder <paramref name="target"/>: a junction on
    /// Windows (no privilege needed), a symbolic link elsewhere.
    /// </summary>
    public static void LinkFolder(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/d", "/c", "mklink", "/J", link, target },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start)!;

        // A line on each at most: neither pipe can fill while the other is read.
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link), $"mklink /J {link} {target} failed ({process.ExitCode}): {output}{error}");
    }

    /// <summary>A session's services and the fakes around them.</summary>
    public sealed record Session(TabServices Services, FakeFolderPicker Folders, RecordingHost Host, GitCallLog Calls, RecordingLog Log)
    {
        public WorkspaceCommands Commands => Services.WorkspaceCommands;

        public IWorkspaceStore Store => Services.Workspaces;

        public Workspace Workspace(string id) => Store.Current.Find(id) ?? throw new InvalidOperationException($"No workspace {id}.");

        public IReadOnlyList<string> PathsOf(string id) => [.. Workspace(id).Repositories.Select(repository => repository.Path)];

        /// <summary>Waits until the dialog on top is a <typeparamref name="T"/> (a flow opens it after git answered) and returns it.</summary>
        public async Task<T> DialogAsync<T>()
            where T : DialogViewModel
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                if (Services.DialogHost.Current is T dialog)
                {
                    return dialog;
                }

                if (clock.Elapsed > ShellDriver.DefaultWaitTimeout)
                {
                    throw new TimeoutException($"No {typeof(T).Name} opened; the dialog on top is {Services.DialogHost.Current?.GetType().Name ?? "none"}.");
                }

                await Task.Delay(10);
            }
        }
    }

    /// <summary>
    /// A plain folder (<see cref="Root"/>) holding the repository <c>api</c> with one commit, and
    /// whatever the test adds with <see cref="InitAsync"/>, all made with real git and the tests'
    /// isolation; deleted with the first repository.
    /// </summary>
    public sealed class Tree(TempRepo first) : IDisposable
    {
        public TempRepo Api => first;

        public string Root => Path.GetDirectoryName(first.Path)!;

        public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Makes an empty repository at <paramref name="relative"/> (folders made as needed).</summary>
        public async Task<string> InitAsync(string relative)
        {
            var path = PathOf(relative);
            await first.GitAsync("init", "--quiet", "--initial-branch", "main", path);
            return path;
        }

        /// <summary>Makes a plain folder at <paramref name="relative"/>.</summary>
        public string Folder(string relative) => Directory.CreateDirectory(PathOf(relative)).FullName;

        public void Dispose() => first.Dispose();
    }

    /// <summary>Opens repositories with real git, as the app's provider does (its log line included).</summary>
    public sealed class RealRepositories(IGitCallLog calls, IAppLog log) : IRepositoryProvider
    {
        private int _opens;

        /// <summary>How many opens were asked for.</summary>
        public int Opens => _opens;

        public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _opens);
            return await Task.Run(() => GitRepository.OpenAsync(new GitRunner(GitPath, calls), path, cancellationToken, log), cancellationToken);
        }

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    /// <summary>The window as the workspace code sees it: what it was asked to show, the open tabs' folders and an open dashboard's rows it is told of.</summary>
    public sealed class RecordingHost : IWorkspaceHost
    {
        public List<(string Id, RepoTabViewModel? From)> Shown { get; } = [];

        /// <summary>The working trees of the open tabs, in tab order.</summary>
        public List<string> OpenFolders { get; } = [];

        /// <summary>The open dashboards' rows: workspace id, saved path, the working directory its open repository reports.</summary>
        public Dictionary<(string Id, string SavedPath), string> OpenedRows { get; } = [];

        /// <summary>The workspaces whose dashboard is open.</summary>
        public HashSet<string> OpenDashboards { get; } = new(StringComparer.Ordinal);

        public void ShowWorkspace(string workspaceId, RepoTabViewModel? from) => Shown.Add((workspaceId, from));

        public IReadOnlyList<string> OpenRepositoryFolders() => [.. OpenFolders];

        public bool HasOpenDashboard(string workspaceId) => OpenDashboards.Contains(workspaceId);

        public string? OpenedWorkingDirectoryOf(string workspaceId, string savedPath) =>
            OpenedRows.TryGetValue((workspaceId, savedPath), out var opened) ? opened : null;
    }

    /// <summary>A store that is read-only for <paramref name="cause"/>, with <paramref name="initial"/>.</summary>
    public sealed class ReadOnlyStore(WorkspaceReadOnlyCause cause, WorkspaceList? initial = null) : IWorkspaceStore
    {
        public WorkspaceList Current { get; private set; } = initial ?? new WorkspaceList();

        public bool IsReadOnly => true;

        public WorkspaceReadOnlyCause? ReadOnlyCause => cause;

        public string? Notice => null;

        public bool SaveFailed => false;

        public int Updates { get; private set; }

        public event EventHandler? Changed;

        public void Update(Func<WorkspaceList, WorkspaceList> change)
        {
            Updates++;
            var next = change(Current);
            if (!ReferenceEquals(next, Current))
            {
                Current = next;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>A repository that only knows its folder: opened at <paramref name="workingDirectory"/>, at its top unless told otherwise.</summary>
    public sealed class FolderRepository(string workingDirectory, bool atTop = true) : FakeRepositoryBase
    {
        public override string WorkingDirectory => workingDirectory;

        public override string GitDirectory => Path.Combine(workingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => Path.GetFileName(workingDirectory);

        public override Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) => Task.FromResult(atTop);

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => throw new NotSupportedException();
    }

    /// <summary>
    /// Opens every folder as a <see cref="FolderRepository"/> but those in <see cref="Refused"/>,
    /// each open held until <see cref="Release"/> when <see cref="Hold"/> is set; counts the opens
    /// running at once.
    /// </summary>
    public sealed class GatedRepositories : IRepositoryProvider
    {
        private readonly Lock _gate = new();
        private readonly List<TaskCompletionSource> _held = [];
        private int _running;

        /// <summary>Opens wait until released.</summary>
        public bool Hold { get; init; }

        /// <summary>Folders git "refuses": their opens throw <see cref="NotARepositoryException"/>.</summary>
        public HashSet<string> Refused { get; } = new(StringComparer.Ordinal);

        /// <summary>The most opens that ran at once.</summary>
        public int MostAtOnce { get; private set; }

        /// <summary>The opens asked for.</summary>
        public int Opens { get; private set; }

        /// <summary>The opens waiting for <see cref="Release"/>.</summary>
        public int Waiting
        {
            get
            {
                lock (_gate)
                {
                    return _held.Count;
                }
            }
        }

        public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource? held = null;
            lock (_gate)
            {
                Opens++;
                _running++;
                MostAtOnce = Math.Max(MostAtOnce, _running);
                if (Hold)
                {
                    held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _held.Add(held);
                }
            }

            try
            {
                if (held is not null)
                {
                    await held.Task.WaitAsync(cancellationToken);
                }

                return Refused.Contains(path) ? throw new NotARepositoryException(path) : new FolderRepository(path);
            }
            finally
            {
                lock (_gate)
                {
                    _running--;
                }
            }
        }

        /// <summary>Lets the waiting opens go on, the first <paramref name="count"/> of them, or all.</summary>
        public void Release(int count = int.MaxValue)
        {
            List<TaskCompletionSource> released;
            lock (_gate)
            {
                released = [.. _held.Take(count)];
                _held.RemoveRange(0, released.Count);
            }

            released.ForEach(open => open.SetResult());
        }

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// A file system of folders the test lists: each folder's folders and the ones with a
    /// <c>.git</c> entry. The check of one folder can wait until <see cref="Release"/>
    /// (<see cref="HoldAt"/>), and the check of another can take at least the scan's progress
    /// interval since the scan began (<see cref="SlowAt"/>), so that the scan reports its progress
    /// then: time the fake makes pass for the scan's own throttle, not a wait of the test.
    /// </summary>
    public sealed class FakeScanFileSystem : IScanFileSystem
    {
        private readonly Dictionary<string, List<string>> _folders = new(StringComparer.Ordinal);
        private readonly HashSet<string> _repositories = new(StringComparer.Ordinal);
        private readonly ManualResetEventSlim _release = new();
        private string? _holdAt;
        private string? _slowAt;
        private int _waiting;

        /// <summary>The checks waiting for <see cref="Release"/> now.</summary>
        public int Waiting => Volatile.Read(ref _waiting);

        /// <summary>Adds <paramref name="relative"/> (its parents too) under <paramref name="root"/>, with a <c>.git</c> entry when <paramref name="repository"/>.</summary>
        public string Add(string root, string relative, bool repository = false)
        {
            var path = root;
            foreach (var name in relative.Split('/'))
            {
                if (!_folders.TryGetValue(path, out var children))
                {
                    _folders[path] = children = [];
                }

                if (!children.Contains(name))
                {
                    children.Add(name);
                }

                path = Path.Combine(path, name);
            }

            _folders.TryAdd(path, []);
            if (repository)
            {
                _repositories.Add(path);
            }

            return path;
        }

        /// <summary>Makes the check of <paramref name="path"/>'s <c>.git</c> entry wait until <see cref="Release"/>.</summary>
        public void HoldAt(string path) => _holdAt = path;

        /// <summary>
        /// Makes the check of <paramref name="path"/> last the scan's progress interval, so that the
        /// scan reports its progress right after it whatever it reported before: under load the
        /// root's listing alone can outlast the interval and restart the scan's clock (2026-10-23).
        /// </summary>
        public void SlowAt(string path) => _slowAt = path;

        public void Release() => _release.Set();

        public IReadOnlyList<ScanFolder> ListFolders(string path)
        {
            return _folders.TryGetValue(path, out var children) ? [.. children.Select(name => new ScanFolder(name, false, false))] : [];
        }

        public bool HasGitEntry(string path)
        {
            if (path == _slowAt)
            {
                var check = Stopwatch.StartNew();
                while (check.Elapsed < RepositoryScanner.ProgressInterval)
                {
                    Thread.Sleep(5);
                }
            }

            if (path == _holdAt)
            {
                Interlocked.Increment(ref _waiting);
                _release.Wait(TimeSpan.FromMinutes(2));
                Interlocked.Decrement(ref _waiting);
            }

            return _repositories.Contains(path);
        }
    }

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }
}
