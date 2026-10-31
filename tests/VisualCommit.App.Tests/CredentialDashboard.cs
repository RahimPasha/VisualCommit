using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
/// that refuses every prompt; the dashboard, not read yet. git traces to a file of the session's
/// own (<see cref="TracePath"/>), which leaves its standard error as it is.
/// </summary>
internal sealed class CredentialDashboard : IAsyncDisposable
{
    /// <summary>The command of the URL rules' read (<see cref="IGitRepository.ReadUrlRewritesAsync"/>).</summary>
    public const string UrlRulesRead = @"git config --null --get-regexp ^url\.";

    /// <summary>A GitHub token as the tests write them, the stub's revoked ones included; <see cref="Describe"/> masks every one.</summary>
    private static readonly Regex AnyToken = new("github_pat_[^\\s\"'&]*", RegexOptions.CultureInvariant);

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

    /// <summary>Where every git call of the session writes its <c>GIT_TRACE</c> lines.</summary>
    public string TracePath => _data.Combine("git-trace.txt");

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

        var environment = new Dictionary<string, string?>(GitIsolation.SessionGitEnvironment(data.Path))
        {
            // An absolute path: git appends its trace there, and its standard error is unchanged.
            ["GIT_TRACE"] = data.Combine("git-trace.txt"),
        };
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

    /// <summary>The token in no log line, git command, git output, git trace or the user's file (4.9).</summary>
    public void AssertTokenNowhere()
    {
        Assert.DoesNotContain(_log.Messages, message => message.Contains(CredentialTestSupport.Token, StringComparison.Ordinal));
        Assert.DoesNotContain(_calls.Snapshot(), record => (record.CommandText + record.StandardOutput + record.StandardError).Contains(CredentialTestSupport.Token, StringComparison.Ordinal));
        Assert.DoesNotContain(CredentialTestSupport.Token, ReadTrace(), StringComparison.Ordinal);
        if (File.Exists(UserStore))
        {
            Assert.DoesNotContain(CredentialTestSupport.Token, File.ReadAllText(UserStore), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// What tells why a bulk run's rows did not end as a test expects, for its failure message:
    /// each row's state, result and kept dialog; the session's operation calls and URL rules reads
    /// with git's standard error; the server's requests (never a password); the stub's counts; the
    /// prompts; every log line; and git's trace of the helper's runs (<c>--vc-credential</c>), each
    /// with git's time. A helper run without a line of the broker's for it was lost on the way
    /// (CI's macOS run 40878208671). Every token masked.
    /// </summary>
    public string Describe(DashboardViewModel dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        var text = new StringBuilder();
        var run = dashboard.Bulk.Current;
        text.AppendLine(CultureInfo.InvariantCulture, $"The run: {(run is null ? "none" : $"{run.Kind}, {run.Succeeded} succeeded, {run.Failed} failed, {run.Skipped} skipped")}");
        foreach (var row in run?.Rows ?? [])
        {
            var result = row.Result;
            var display = row.KeptDialog?.Display;
            text.AppendLine(CultureInfo.InvariantCulture, $"  {row.Row.Name}: {row.State}, {result?.Outcome.ToString() ?? "no result"}, failure {result?.Failure?.Kind.ToString() ?? "none"}, refused account {result?.RefusedAccount?.Login ?? "none"}");
            text.AppendLine(CultureInfo.InvariantCulture, $"    kept dialog: {display?.Title ?? "none"} | {display?.Summary} | {display?.Extra?.Label ?? "no extra button"}");
        }

        text.AppendLine("Git calls of the operations and the URL rules reads:");
        foreach (var call in _calls.Snapshot().Where(call => call.OperationId is not null || call.CommandText == UrlRulesRead))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  [{call.OperationId ?? "no operation"}] {call.CommandText}: {call.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? call.Outcome.ToString()}");
            foreach (var line in Lines(GitCommand.HideCredentials(call.StandardError)))
            {
                text.AppendLine("    " + line);
            }
        }

        text.AppendLine("The server's requests:");
        foreach (var request in Server.Requests)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {request.Method} {request.Path}: {request.Status}, user {request.UserName ?? "none"}, given user {request.GivenUserName ?? "none"}");
        }

        foreach (var error in Server.Errors)
        {
            text.AppendLine("  error: " + error);
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"The stub: {Stub.Credentials.Gets.Count} gets, {Stub.Credentials.Refusals.Count} refusals, {Stub.BindingsForCalls} BindingsFor calls; {UrlRulesReads} URL rules reads.");
        text.AppendLine("Prompts:");
        foreach (var prompt in Handler.Prompts)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {prompt.Kind} for {prompt.Url ?? "no URL"}, account {prompt.Account?.Login ?? "none"}");
        }

        text.AppendLine("The log:");
        foreach (var (level, message, exception) in _log.Entries)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {level}: {message}{(exception is null ? string.Empty : " " + exception.Message)}");
        }

        text.AppendLine("git's trace of the helper's runs:");
        foreach (var line in Lines(ReadTrace()).Where(line => line.Contains(AskPassProtocol.CredentialArgument, StringComparison.Ordinal)))
        {
            text.AppendLine("  " + line);
        }

        return AnyToken.Replace(text.ToString(), "<token>");
    }

    private static IEnumerable<string> Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0);

    /// <summary>The trace so far, empty when git wrote none; read while git may still append.</summary>
    private string ReadTrace()
    {
        if (!File.Exists(TracePath))
        {
            return string.Empty;
        }

        using var stream = new FileStream(TracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
