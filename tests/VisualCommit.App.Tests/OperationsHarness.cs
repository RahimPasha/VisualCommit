using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Runs <see cref="RepositoryOperations"/> against a real scenario repository (docs/phase-3-design.md
/// 7.4, section 8's WP7): the repository's view model, opened and read as a tab opens it, with a
/// real dialog layer whose dialogs <see cref="Dialogs"/> answers as the test scripts them, a
/// <see cref="FakeClipboard"/> and a <see cref="FakeFileLauncher"/>, and a log to look at. No
/// window and no UI thread: the operations run on the test's own context, as phase 2's
/// <c>WorkingChangesTests</c> run the writes. A typical test:
/// <code>
/// using var repo = await Scenarios.OperationsAsync();
/// using var app = await OperationsHarness.OpenAsync(repo);
/// app.Dialogs.Confirm();
/// await app.Operations.DeleteBranchAsync("refs/heads/feature/ff");
/// Assert.Equal("Deleted feature/ff", app.StatusText);
/// Assert.Null(await repo.RefTargetAsync("refs/heads/feature/ff"));
/// </code>
/// Every public operation completes when its operation and its refresh have ended, so a test
/// asserts right after awaiting it.
/// </summary>
internal sealed class OperationsHarness : IDisposable
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private OperationsHarness(TempRepo repo, RepositoryViewModel repository, DialogHostViewModel host, TabServices services, RecordingLog log, FakeClipboard clipboard, FakeFileLauncher launcher)
    {
        Repo = repo;
        Repository = repository;
        DialogHost = host;
        Services = services;
        Log = log;
        Clipboard = clipboard;
        Launcher = launcher;
        Dialogs = new ScriptedDialogs(host);
    }

    /// <summary>The scenario repository, for git's state (<see cref="TempRepo.GitAsync"/>, <see cref="TempRepo.RefTargetAsync"/>, …).</summary>
    public TempRepo Repo { get; }

    /// <summary>The repository's view model, started.</summary>
    public RepositoryViewModel Repository { get; }

    /// <summary>The operations under test.</summary>
    public RepositoryOperations Operations => Repository.Operations;

    /// <summary>The dialog layer the operations open their dialogs on.</summary>
    public DialogHostViewModel DialogHost { get; }

    /// <summary>The script that answers the dialogs, and the dialogs it saw.</summary>
    public ScriptedDialogs Dialogs { get; }

    public TabServices Services { get; }

    public RecordingLog Log { get; }

    /// <summary>What "Copy …" put on the clipboard.</summary>
    public FakeClipboard Clipboard { get; }

    /// <summary>What "Open file" and "Show in folder" asked for.</summary>
    public FakeFileLauncher Launcher { get; }

    /// <summary>What the status bar says about the operations (4e).</summary>
    public string StatusText => Repository.OperationStatus.Text;

    public OperationStatusTone StatusTone => Repository.OperationStatus.Tone;

    /// <summary>The last operation's result.</summary>
    public OperationResult? LastResult => Repository.Queue.LastResult;

    /// <summary>The settings the operations read and write (the stash dialog's last choice, 6.2).</summary>
    public MainWindowViewModelTests.FakeSettings Settings => (MainWindowViewModelTests.FakeSettings)Services.Settings;

    /// <summary>Opens <paramref name="repo"/> as a tab would and reads it.</summary>
    public static async Task<OperationsHarness> OpenAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var git = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var log = new RecordingLog();
        var host = new DialogHostViewModel();
        var clipboard = new FakeClipboard();
        var launcher = new FakeFileLauncher();
        var settings = new MainWindowViewModelTests.FakeSettings();
        var dates = new DateDisplay(TimeZoneInfo.Utc);
        var services = new TabServices(new NoRepositories(), new NoFolders(), settings, new MemorySession(), dates, log, dialogs: host, launcher: launcher, clipboard: clipboard);
        var repository = new RepositoryViewModel(git, settings, dates, log, dialogs: host, services: services);
        var harness = new OperationsHarness(repo, repository, host, services, log, clipboard, launcher);
        await repository.StartAsync();
        return harness;
    }

    /// <summary>The full id <paramref name="revision"/> names now.</summary>
    public async Task<string> ShaAsync(string revision) => (await Repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>Selects these commits in the graph, as clicks and Ctrl+clicks would (4h).</summary>
    public void Select(params string[] shas) => Repository.Selection = new GraphSelection(shas);

    /// <summary>Waits until the repository's queue has nothing running or waiting.</summary>
    public Task WaitForIdleAsync() => Repository.Queue.WhenIdleAsync();

    /// <summary>
    /// Fails the test with what the dialog script noted: answers that did not fit, dialogs that
    /// came unscripted (error dialogs aside), and answers that were never used.
    /// </summary>
    public void AssertScriptFollowed()
    {
        Assert.Empty(Dialogs.Problems);
        Assert.Equal(0, Dialogs.Unused);
    }

    public void Dispose()
    {
        Dialogs.Dispose();
        Repository.Dispose();
    }

    private sealed class MemorySession : ISessionStore
    {
        public SessionState Current { get; private set; } = new();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
