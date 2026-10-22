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

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }
}
