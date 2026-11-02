using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// 7a's <see cref="HostingSessionSetup"/> over the three fakes in process, each optional
/// (docs/phase-7b-design.md 9.10): GitHub's octo (1001), Azure DevOps' organizations
/// <see cref="Organization"/> and <see cref="OtherOrganization"/> with a project <see cref="Project"/>
/// and a user Ada in each, GitLab's ada (2001) granted the group <see cref="Group"/>'s top. Each
/// fake's handler sits behind a recorder; a <see cref="FakeTokenStore"/> unless given, a data folder
/// of its own, a <see cref="ManualTimeProvider"/> at the harness's time and a
/// <see cref="HostingTestThread"/> as the post delegate. With <c>repositories</c>, one repository
/// each with an open request from <c>feature/sign-in</c>: <c>fake-org/P/r</c> (!11),
/// <c>other-org/P/r</c>, <c>fake-group/sub/web-app</c> (!21) and <c>octo/web-app</c> (#7). The
/// session is made, not started. Disposing it checks that no token it minted or a test gave
/// <see cref="Track"/> reached the data folder or the log, that no line holds a token of any fake's
/// form, and that no fake saw an error. Made-up names only.
/// </summary>
internal sealed class ProvidersSessionSetup : IAsyncDisposable
{
    public const string Organization = "fake-org";
    public const string OtherOrganization = "other-org";
    public const string Project = "P";
    public const string Group = "fake-group/sub";
    public const string AzureRepository = "r";
    public const string GitLabProject = Group + "/web-app";
    public const string GitHubRepository = "octo/web-app";

    // The fakes' tokens in a log line (github_pat_FAKE-, ghp_FAKE-, azdo_FAKE-, glpat-FAKE-, or one
    // a server echoed without its prefix): "FAKE-", but for the fakes' request ids, which warnings
    // name (GitHub's FAKE-0001, GitLab's FAKE-GL-000001).
    private static readonly System.Text.RegularExpressions.Regex FakeToken = new(@"FAKE-(?!\d{4}\b|GL-\d{6}\b)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private readonly Lock _gate = new();
    private readonly List<string> _tokens = [];
    private readonly Dictionary<HostingProvider, List<RecordingHttpHandler>> _recorders = [];
    private readonly List<HostingSession> _sessions = [];
    private readonly AzureDevOpsEndpoints? _azureDevOpsEndpoints;

    private ProvidersSessionSetup(TempDirectory root, ITokenStore tokens, bool gitHub, bool azureDevOps, bool gitLab, AzureDevOpsEndpoints? azureDevOpsEndpoints)
    {
        Root = root;
        Tokens = tokens;
        Paths = new AppPaths(Data.Path);
        _azureDevOpsEndpoints = azureDevOpsEndpoints;
        if (gitHub)
        {
            GitHub = FakeGitHub.InProcess(Path.Combine(root.Path, "github"));
            GitHub.AddUser("octo", 1001, Scenarios.OctoName);
            GitHubToken = Track(GitHub.AddToken("octo"));
        }

        if (azureDevOps)
        {
            AzureDevOps = FakeAzureDevOps.InProcess(Path.Combine(root.Path, "azure-devops"));
            AzureDevOps.AddOrganization(Organization);
            AzureDevOps.AddProject(Organization, Project);
            Ada = AzureDevOps.AddUser(Organization, Scenarios.AdaName);
            AzureDevOpsToken = Track(AzureDevOps.AddToken(Organization, Ada));
            AzureDevOps.AddOrganization(OtherOrganization);
            AzureDevOps.AddProject(OtherOrganization, Project);
            OtherAda = AzureDevOps.AddUser(OtherOrganization, Scenarios.AdaName);
            OtherOrganizationToken = Track(AzureDevOps.AddToken(OtherOrganization, OtherAda));
        }

        if (gitLab)
        {
            GitLab = FakeGitLab.InProcess(Path.Combine(root.Path, "gitlab"));
            GitLab.AddUser("ada", 2001, Scenarios.AdaName);
            GitLabToken = Track(GitLab.AddToken("ada"));
            GitLab.Grant("fake-group", "ada", 40);
        }

        Session = NewSession();
    }

    public TempDirectory Root { get; }

    public FakeGitHub? GitHub { get; }

    public FakeAzureDevOps? AzureDevOps { get; }

    public FakeGitLab? GitLab { get; }

    public string GitHubToken { get; } = string.Empty;

    public string AzureDevOpsToken { get; } = string.Empty;

    public string OtherOrganizationToken { get; } = string.Empty;

    public string GitLabToken { get; } = string.Empty;

    /// <summary>Ada in <see cref="Organization"/>.</summary>
    public FakeAzureUser? Ada { get; }

    /// <summary>Ada in <see cref="OtherOrganization"/>.</summary>
    public FakeAzureUser? OtherAda { get; }

    public FakeAzureRepository? AzureRepo { get; private set; }

    public FakeAzureRepository? OtherAzureRepo { get; private set; }

    public FakeGitLabProject? WebApp { get; private set; }

    public ITokenStore Tokens { get; }

    /// <summary><see cref="Tokens"/> as the harness's store, when it is one.</summary>
    public FakeTokenStore FakeTokens => (FakeTokenStore)Tokens;

    public TempDirectory Data { get; } = new("providers-data");

    public AppPaths Paths { get; }

    public HostingTestThread Thread { get; } = new();

    public ListLog Log { get; } = new();

    public ManualTimeProvider Clock { get; } = new(AppClocks.HarnessTime);

    public HostingSession Session { get; }

    /// <summary>Every token the fakes minted for the test, the setup's and those given to <see cref="Track"/>, for the tripwire.</summary>
    public IReadOnlyList<string> AllTokens
    {
        get
        {
            lock (_gate)
            {
                return [.. _tokens];
            }
        }
    }

    /// <summary>Starts a setup with the fakes asked for; <paramref name="azureDevOpsEndpoints"/> in place of the fake's own (<c>Local(2)</c>), such as <see cref="AzureDevOpsEndpoints.Service"/>.</summary>
    public static async Task<ProvidersSessionSetup> StartAsync(
        bool gitHub = true,
        bool azureDevOps = true,
        bool gitLab = true,
        bool repositories = false,
        AzureDevOpsEndpoints? azureDevOpsEndpoints = null,
        ITokenStore? tokens = null)
    {
        var root = new TempDirectory("providers-fakes");
        ProvidersSessionSetup? setup = null;
        try
        {
            setup = new ProvidersSessionSetup(root, tokens ?? new FakeTokenStore(), gitHub, azureDevOps, gitLab, azureDevOpsEndpoints);
            if (repositories)
            {
                await setup.AddRepositoriesAsync();
            }

            return setup;
        }
        catch
        {
            if (setup is not null)
            {
                await setup.DisposeAsync();
            }
            else
            {
                root.Dispose();
            }

            throw;
        }
    }

    /// <summary>The options of a session over the fakes given: each provider's endpoints with its fake's handler behind a recorder; a provider without a fake off.</summary>
    public HostingOptions Options()
    {
        var options = GitHub is { } gitHub ? new HostingOptions(gitHub.Endpoints, () => Recorded(HostingProvider.GitHub, gitHub.CreateHandler())) : HostingOptions.Off;
        return options with
        {
            AzureDevOps = AzureDevOps is null ? null : _azureDevOpsEndpoints ?? AzureDevOps.Endpoints,
            CreateAzureDevOpsHandler = AzureDevOps is { } azure
                ? () => Recorded(HostingProvider.AzureDevOps, _azureDevOpsEndpoints is { } service && service != azure.Endpoints ? new ServiceAnswers(azure.CreateHandler(), service.WebBase) : azure.CreateHandler())
                : null,
            GitLab = GitLab?.Endpoints,
            CreateGitLabHandler = GitLab is { } gitLab ? () => Recorded(HostingProvider.GitLab, gitLab.CreateHandler()) : null,
        };
    }

    /// <summary>
    /// <paramref name="token"/>, which a test had a fake mint (<c>setup.Track(setup.GitLab!.AddToken("bob"))</c>),
    /// searched for at the disposal as the setup's own: in the data folder and, in any case, in the log.
    /// </summary>
    public string Track(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        lock (_gate)
        {
            if (!_tokens.Contains(token, StringComparer.Ordinal))
            {
                _tokens.Add(token);
            }
        }

        return token;
    }

    /// <summary>Another session over the same data folder, store and fakes: the app's next start; <see cref="Options"/>, <see cref="Tokens"/> and <see cref="Log"/> unless given.</summary>
    public HostingSession NewSession(ITokenStore? tokens = null, HostingOptions? options = null, IAppLog? log = null)
    {
        var session = new HostingSession(Paths, options ?? Options(), tokens ?? Tokens, log ?? Log, Thread.Post, Clock);
        lock (_gate)
        {
            _sessions.Add(session);
        }

        return session;
    }

    /// <summary>Every request a session sent to <paramref name="provider"/>'s fake, in order.</summary>
    public IReadOnlyList<RecordingHttpHandler.SentRequest> Sent(HostingProvider provider)
    {
        lock (_gate)
        {
            return _recorders.TryGetValue(provider, out var recorders) ? [.. recorders.SelectMany(recorder => recorder.Sent)] : [];
        }
    }

    /// <summary>Every request the sessions sent, to any fake.</summary>
    public int SentCount => HostingProviders.InAccountsOrder.Sum(provider => Sent(provider).Count);

    /// <summary>The scenario's token of <paramref name="provider"/> (Azure DevOps': <see cref="Organization"/>'s).</summary>
    public string TokenOf(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => GitHubToken,
        HostingProvider.GitLab => GitLabToken,
        _ => AzureDevOpsToken,
    };

    /// <summary>Starts <see cref="Session"/> once and signs in to <paramref name="provider"/> (the scenario's token, Azure DevOps in <see cref="Organization"/>, unless given), asserting it worked.</summary>
    public async Task<AccountStatus> SignInAsync(HostingProvider provider, string? token = null, string? organization = null, HostingSession? session = null)
    {
        session ??= Session;
        await session.StartAsync(TestContext.Current.CancellationToken);
        var result = await session.SignInAsync(
            new SignInRequest(provider, token ?? TokenOf(provider), provider == HostingProvider.AzureDevOps ? organization ?? Organization : null),
            TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        return result.Status!;
    }

    /// <summary>The account file as written now.</summary>
    public AccountsFile LoadFile() => new JsonAccountStore(Paths.AccountsFile, new ListLog()).Load();

    /// <summary>The warnings logged so far.</summary>
    public IReadOnlyList<string> Warnings => [.. Log.Lines.Where(line => line.Level == LogLevel.Warning).Select(line => line.Message)];

    public async ValueTask DisposeAsync()
    {
        HostingSession[] sessions;
        lock (_gate)
        {
            sessions = [.. _sessions];
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }

        await Thread.IdleAsync();
        Thread.Dispose();
        try
        {
            foreach (var token in AllTokens)
            {
                TokenTripwire.AssertAbsent(token, [Data.Path], null);
                Assert.DoesNotContain(token, Log.Written, StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(Log.Lines, line => Assert.False(FakeToken.IsMatch(line.Message), "A log line holds a token of a fake's form."));
            Assert.Empty(Thread.Errors);
            Assert.Empty(GitHub?.Errors ?? []);
            Assert.Empty(AzureDevOps?.Errors ?? []);
            Assert.Empty(GitLab?.Errors ?? []);
        }
        finally
        {
            foreach (var fake in new IAsyncDisposable?[] { GitHub, AzureDevOps, GitLab })
            {
                if (fake is not null)
                {
                    await fake.DisposeAsync();
                }
            }

            Data.Dispose();
            Root.Dispose();
        }
    }

    private RecordingHttpHandler Recorded(HostingProvider provider, HttpMessageHandler inner)
    {
        var recorder = new RecordingHttpHandler(inner);
        lock (_gate)
        {
            if (!_recorders.TryGetValue(provider, out var recorders))
            {
                _recorders[provider] = recorders = [];
            }

            recorders.Add(recorder);
        }

        return recorder;
    }

    /// <summary>
    /// The in-process fake of Azure DevOps reached under other endpoints (such as
    /// <see cref="AzureDevOpsEndpoints.Service"/>): its answers name its own <c>Local(2)</c> base
    /// (<c>remoteUrl</c>, <c>webUrl</c>, <c>locationUrl</c>), which the provider reads only as its own
    /// origin's (WP-AZ's As built), so each such address is written on <paramref name="webBase"/>'s
    /// origin, its user kept, as the service names its own.
    /// </summary>
    private sealed class ServiceAnswers(HttpMessageHandler inner, Uri webBase) : DelegatingHandler(inner)
    {
        private static readonly System.Text.RegularExpressions.Regex LocalBase = new(@"http://([^/""@\s]*@)?127\.0\.0\.1:2/", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Content is not { } content || content.Headers.ContentType?.MediaType != "application/json")
            {
                return response;
            }

            var text = await content.ReadAsStringAsync(cancellationToken);
            var origin = webBase.GetLeftPart(UriPartial.Authority);
            var rewritten = LocalBase.Replace(text, match => origin.Insert(origin.IndexOf("//", StringComparison.Ordinal) + 2, match.Groups[1].Value) + "/");
            var replaced = new StringContent(rewritten, System.Text.Encoding.UTF8, "application/json");
            foreach (var header in content.Headers.Where(header => !header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)))
            {
                replaced.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = replaced;
            content.Dispose();
            return response;
        }
    }

    // One source with main and feature/sign-in, then each fake's repository with an open request.
    private async Task AddRepositoriesAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var source = await TempRepo.CreateAsync("providers-source");
        await source.CommitFileAsync("README.md", "providers\n", "Initial commit");
        await source.CreateBranchAsync("feature/sign-in", "main");
        await source.CheckoutAsync("feature/sign-in");
        await source.CommitFileAsync("sign-in.txt", "sign in\n", "Add the sign-in page");
        await source.CheckoutAsync("main");
        if (AzureDevOps is { } azure)
        {
            AzureRepo = await azure.AddRepositoryAsync(Organization, Project, AzureRepository, source.Path, ct: ct);
            OtherAzureRepo = await azure.AddRepositoryAsync(OtherOrganization, Project, AzureRepository, source.Path, ct: ct);
            azure.AddPullRequest(AzureRepo, "Add the sign-in page", Ada!, "feature/sign-in", "main", id: 11);
            azure.AddPullRequest(OtherAzureRepo, "Add the sign-in page elsewhere", OtherAda!, "feature/sign-in", "main", id: 31);
        }

        if (GitLab is { } gitLab)
        {
            WebApp = await gitLab.AddProjectAsync(GitLabProject, source.Path, ct: ct);
            gitLab.AddMergeRequest(WebApp, "Add the sign-in page", "ada", "feature/sign-in", "main", iid: 21);
        }

        if (GitHub is { } gitHub)
        {
            await gitHub.AddRepositoryAsync("octo", "web-app", source.Path, ct: ct);
            gitHub.AddPullRequest("octo", "web-app", "Add the sign-in page", "octo", "feature/sign-in", "main", number: 7);
        }
    }
}
