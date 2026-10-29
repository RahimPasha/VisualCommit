using System.Net;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.AskPass;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A dashboard over real git for the credential classes (docs/phase-7a-design.md 4.8): the
/// workspace "Web" of <see cref="Names"/>, their <c>origin</c> on an <see cref="AuthRemoteServer"/>
/// that takes the account's token (<see cref="CredentialTestSupport.Token"/>) alone; the app's
/// session around it, with real git through one runner whose own global file holds the user's
/// <c>store</c> helper (never the test process's isolation file), the app's prompt broker with a
/// <see cref="HostingSessionStub"/> as its credential source and a <see cref="ScriptedPromptHandler"/>
/// that refuses every prompt; the dashboard, not read yet.
/// </summary>
internal sealed class CredentialDashboard : IAsyncDisposable
{
    /// <summary>The command of the URL rules' read (<see cref="IGitRepository.ReadUrlRewritesAsync"/>).</summary>
    public const string UrlRulesRead = @"git config --null --get-regexp ^url\.";

    /// <summary>The workspace's rows: <c>api</c> level with its remote, <c>docs</c> behind it (a fast-forward).</summary>
    public static readonly string[] Names = ["api", "docs"];

    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    private readonly TempWorkspace _copy;
    private readonly TempDirectory _data;
    private readonly IGitPromptBroker _broker;
    private readonly GitCallLog _calls;
    private readonly RecordingLog _log;

    private CredentialDashboard(TempWorkspace copy, TempDirectory data, AuthRemoteServer server, HostingSessionStub stub, ScriptedPromptHandler handler, string userStore, IGitPromptBroker broker, GitCallLog calls, RecordingLog log, TabServices services)
    {
        _copy = copy;
        _data = data;
        Server = server;
        Stub = stub;
        Handler = handler;
        UserStore = userStore;
        _broker = broker;
        _calls = calls;
        _log = log;
        Services = services;
        Dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
    }

    public AuthRemoteServer Server { get; }

    public HostingSessionStub Stub { get; }

    /// <summary>Refuses every prompt, and records it.</summary>
    public ScriptedPromptHandler Handler { get; }

    /// <summary>The user's own <c>store</c> helper's file.</summary>
    public string UserStore { get; }

    public TabServices Services { get; }

    public DashboardViewModel Dashboard { get; }

    /// <summary>How often the URL rules were read.</summary>
    public int UrlRulesReads => _calls.Snapshot().Count(record => record.CommandText == UrlRulesRead);

    /// <param name="bindsGit">The stub's switch at the start.</param>
    /// <param name="token">What the stub answers git's <c>get</c> with; another than the server's is a revoked token.</param>
    public static async Task<CredentialDashboard> StartAsync(bool bindsGit, string token = CredentialTestSupport.Token)
    {
        var copy = await Scenarios.WorkspaceAsync();
        var data = new TempDirectory("dashboard-credentials");
        var server = await AuthRemoteServer.StartForFolderAsync(Path.GetDirectoryName(copy.RemoteOf("docs"))!);
        server.RequireAuth = true;
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        foreach (var name in Names)
        {
            await copy.Repo(name).GitAsync("remote", "set-url", "origin", server.UrlOf(name));
        }

        var environment = GitIsolation.SessionGitEnvironment(data.Path);
        var userStore = data.Combine("user-store");
        await copy.Repo("api").GitAsync("config", "--file", environment["GIT_CONFIG_GLOBAL"]!, "credential.helper", $"store --file='{userStore.Replace('\\', '/')}'");

        var stub = CredentialTestSupport.Stub(server, token: token);
        stub.BindsGit = bindsGit;
        var log = new RecordingLog();
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls, environment: environment);
        var store = new InMemoryWorkspaceStore(new WorkspaceList { Workspaces = [new Workspace("web", "Web", [.. Names.Select(name => new WorkspaceRepository(copy.PathOf(name)))])] });
        var handler = new ScriptedPromptHandler();
        var broker = PromptDialogs.CreateBroker(handler, log, credentials: stub, sessionEnvironment: environment);
        var services = new TabServices(
            new RealRepositories(runner),
            new NoFolders(),
            new MainWindowViewModelTests.FakeSettings(),
            new DashboardTestSupport.MemorySession(),
            new DateDisplay(TimeZoneInfo.Utc),
            log,
            prompts: broker,
            gitCalls: calls,
            workspaces: store,
            hosting: stub);
        return new CredentialDashboard(copy, data, server, stub, handler, userStore, broker, calls, log, services);
    }

    /// <summary>The token in no log line, git command, git output or the user's file (4.9).</summary>
    public void AssertTokenNowhere()
    {
        Assert.DoesNotContain(_log.Messages, message => message.Contains(CredentialTestSupport.Token, StringComparison.Ordinal));
        Assert.DoesNotContain(_calls.Snapshot(), record => (record.CommandText + record.StandardOutput + record.StandardError).Contains(CredentialTestSupport.Token, StringComparison.Ordinal));
        if (File.Exists(UserStore))
        {
            Assert.DoesNotContain(CredentialTestSupport.Token, File.ReadAllText(UserStore), StringComparison.Ordinal);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dashboard.Dispose();
        (_broker as IDisposable)?.Dispose();
        await Server.DisposeAsync();
        _copy.Dispose();
        _data.Dispose();
    }

    /// <summary>Opens repositories with real git through the session's one runner, as <see cref="GitRepositoryProvider"/> does.</summary>
    private sealed class RealRepositories(GitRunner runner) : IRepositoryProvider
    {
        public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) =>
            await Task.Run(() => GitRepository.OpenAsync(runner, path, cancellationToken), cancellationToken);

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
