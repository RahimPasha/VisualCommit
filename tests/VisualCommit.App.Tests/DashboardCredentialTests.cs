using System.Net;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
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
/// A dashboard row without a tab and the account (docs/phase-7a-design.md 4.7, 4.8, D332): its
/// view model is in summary mode, whose hosting binds nothing, so a network operation reads the
/// URL rules once and binds the row's remotes through the session; signed out of git nothing is
/// read and the user's own helper answers as before. Real git against <see cref="AuthRemoteServer"/>,
/// which takes the account's token alone, with the user's <c>store</c> helper in the app session's
/// own global file.
/// </summary>
public class DashboardCredentialTests
{
    private const string UrlRulesRead = @"git config --null --get-regexp ^url\.";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    /// <summary>The workspace's rows: <c>api</c> level with its remote, <c>docs</c> behind it (a fast-forward).</summary>
    private static readonly string[] Names = ["api", "docs"];

    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    [AvaloniaTheory]
    [InlineData(BulkKind.Fetch)]
    [InlineData(BulkKind.Pull)]
    public async Task Rows_without_a_tab_take_the_account_for_a_bulk_run_started_while_they_read_and_for_a_rows_fetch(BulkKind kind)
    {
        await using var setup = await Setup.StartAsync(bindsGit: true);
        var dashboard = setup.Dashboard;

        // The run starts while every row is still read: its rows wait for their reads (4.3), and
        // each operation reads the rules once the row's refs are there.
        dashboard.OnBecameActive();
        Assert.True(dashboard.IsPassRunning);
        Assert.All(dashboard.Rows, row => Assert.False(row.HasRepository));
        Assert.True(kind == BulkKind.Fetch ? dashboard.Bulk.CanFetchAll : dashboard.Bulk.CanPullAll);
        await (kind == BulkKind.Fetch ? dashboard.Bulk.FetchAllAsync() : dashboard.Bulk.PullAllAsync()).WaitAsync(Patience);

        var run = dashboard.Bulk.Current!;
        Assert.Equal((2, 0, 0), (run.Succeeded, run.Failed, run.Skipped));
        Assert.All(dashboard.Rows, row => Assert.True(row.Repository is { IsSummary: true }));
        Assert.Equal(2, setup.UrlRulesReads);
        Assert.Equal([setup.Stub.Key, setup.Stub.Key], setup.Stub.Credentials.Gets);

        // A row's own Fetch, outside any run.
        var docs = dashboard.Rows[1];
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(3, setup.UrlRulesReads);
        Assert.Equal(3, setup.Stub.Credentials.Gets.Count);
        Assert.Empty(setup.Handler.Prompts);
        Assert.False(File.Exists(setup.UserStore));
        Assert.All(setup.Server.Requests.Where(request => request.UserName is not null), request => Assert.Equal(AskPassProtocol.CredentialUserName, request.UserName));
        setup.AssertTokenNowhere();
    }

    [AvaloniaFact]
    public async Task Without_the_switch_nothing_is_read_and_the_users_helper_answers_and_the_switch_binds_the_next_operation()
    {
        await using var setup = await Setup.StartAsync(bindsGit: false);
        setup.Server.Credentials = new NetworkCredential("alice", "alice-secret");
        File.WriteAllText(setup.UserStore, $"http://alice:alice-secret@127.0.0.1:{setup.Server.Port}\n");
        var dashboard = setup.Dashboard;
        dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows.All(row => row.HasRepository) && !dashboard.IsPassRunning, "the rows' first read");
        var docs = dashboard.Rows[1];

        // BindsGit false: no rules read, the session never asked; the user's helper answers alice's.
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(0, setup.UrlRulesReads);
        Assert.Equal(0, setup.Stub.BindingsForCalls);
        Assert.Empty(setup.Stub.Credentials.Gets);
        Assert.Contains(setup.Server.Requests, request => request is { UserName: "alice", Status: 200 });

        // The switch turned on while the dashboard is open: the next operation is bound. The
        // server now takes the token alone, so had the user's helper been asked first, alice's
        // credential would have failed the fetch and git would have erased it from the file.
        setup.Server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        setup.Stub.BindsGit = true;
        var before = setup.Server.Requests.Count;
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(1, setup.UrlRulesReads);
        Assert.Equal([setup.Stub.Key], setup.Stub.Credentials.Gets);
        Assert.Contains(setup.Server.Requests.Skip(before), request => request is { UserName: AskPassProtocol.CredentialUserName, Status: 200 });
        Assert.Empty(setup.Handler.Prompts);

        // The user's file holds alice's credential alone, as git stored it again after the first
        // fetch (its own spelling of the port's colon): the bound call neither erased nor stored.
        Assert.StartsWith("http://alice:alice-secret@127.0.0.1", Assert.Single(File.ReadAllLines(setup.UserStore)), StringComparison.Ordinal);
        setup.AssertTokenNowhere();
    }

    /// <summary>
    /// The workspace "Web" of <see cref="Names"/>, their <c>origin</c> on the server; the app's
    /// session around it (real git with its own global file holding the user's <c>store</c> helper,
    /// the app's prompt broker with the stub as its credential source); the dashboard, not read yet.
    /// </summary>
    private sealed class Setup : IAsyncDisposable
    {
        private readonly TempWorkspace _copy;
        private readonly TempDirectory _data;
        private readonly IGitPromptBroker _broker;
        private readonly GitCallLog _calls;
        private readonly RecordingLog _log;

        private Setup(TempWorkspace copy, TempDirectory data, AuthRemoteServer server, HostingSessionStub stub, ScriptedPromptHandler handler, string userStore, IGitPromptBroker broker, GitCallLog calls, RecordingLog log, DashboardViewModel dashboard)
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
            Dashboard = dashboard;
        }

        public AuthRemoteServer Server { get; }

        public HostingSessionStub Stub { get; }

        /// <summary>Refuses every prompt, and records it.</summary>
        public ScriptedPromptHandler Handler { get; }

        /// <summary>The user's own <c>store</c> helper's file.</summary>
        public string UserStore { get; }

        public DashboardViewModel Dashboard { get; }

        /// <summary>How often the URL rules were read.</summary>
        public int UrlRulesReads => _calls.Snapshot().Count(record => record.CommandText == UrlRulesRead);

        public static async Task<Setup> StartAsync(bool bindsGit)
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

            var stub = CredentialTestSupport.Stub(server);
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
            return new Setup(copy, data, server, stub, handler, userStore, broker, calls, log, new DashboardViewModel(store.Current.Find("web")!, services, () => true));
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
