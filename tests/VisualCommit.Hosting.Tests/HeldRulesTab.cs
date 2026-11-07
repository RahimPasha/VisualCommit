using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// One repository tab built by hand over a hosting scenario's clone, signed out, whose URL rules
/// reads a test holds or fails (D290's window, D388): GitHub's <c>hello</c> of "hosting" (7a), or
/// GitLab's <c>web-app</c> and Azure DevOps' <c>mobile-app</c> of "hosting7b", their fakes on port 0.
/// Real git runs through one runner with the session's git environment
/// (<see cref="GitIsolation.SessionGitEnvironment"/>); a <see cref="HostingSession"/> over the fakes
/// is git's credential source for the app's askpass broker (<see cref="PromptDialogs.CreateBroker"/>),
/// whose prompts a <see cref="ScriptedPromptHandler"/> refuses and records; the clone is opened
/// through <see cref="RulesReads"/>, a proxy of its repository; the tab's stacked sign-in
/// (<see cref="TabServices.SignIn"/>) signs in through the session as the dialog would. As
/// <c>RepositoryHostingWiringTests</c>' wiring, with the broker and the proxy added.
/// </summary>
internal sealed class HeldRulesTab : IAsyncDisposable
{
    /// <summary>The command of the URL rules' read (<see cref="IGitRepository.ReadUrlRewritesAsync"/>).</summary>
    public const string RulesRead = @"git config --null --get-regexp ^url\.";

    /// <summary>Git 2.46 sends a bound URL's first request with the account's token (D340's <c>proactiveAuth</c>; part B's <c>ProactiveAuthVersion</c>).</summary>
    public static readonly GitVersion ProactiveAuthVersion = new(2, 46, 0);

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private readonly IAsyncDisposable _served;
    private readonly TempDirectory _data;
    private readonly IGitPromptBroker _broker;
    private readonly string[] _tokens;
    private readonly string[] _folders;

    private HeldRulesTab(HostingProvider provider, IAsyncDisposable served, TempDirectory data, Scenario scenario, GitCallLog calls, ListLog log, ScriptedPromptHandler prompts, IGitPromptBroker broker, HostingSession session, TabServices services, RepositoryViewModel repository, RulesReads reads, GitVersion? version)
    {
        Provider = provider;
        _served = served;
        _data = data;
        _broker = broker;
        _tokens = scenario.Tokens;
        _folders = [data.Path, .. scenario.Folders];
        Clone = scenario.Repository;
        Token = scenario.Token;
        Organization = scenario.Organization;
        GitRequestsOf = scenario.GitRequests;
        CreatedPosts = scenario.CreatedPosts;
        Calls = calls;
        Log = log;
        Prompts = prompts;
        Session = session;
        Services = services;
        Repository = repository;
        Reads = reads;
        Version = version;
    }

    public HostingProvider Provider { get; }

    /// <summary>The clone the tab is open on.</summary>
    public TempRepo Clone { get; }

    /// <summary>The scenario's token of the provider's account (octo's, ada's).</summary>
    public string Token { get; }

    /// <summary>Azure DevOps' organization of the scenario; null for the others.</summary>
    public string? Organization { get; }

    public GitCallLog Calls { get; }

    public ListLog Log { get; }

    /// <summary>Refuses every prompt of git's, and records it.</summary>
    public ScriptedPromptHandler Prompts { get; }

    public HostingSession Session { get; }

    public TabServices Services { get; }

    public RepositoryViewModel Repository { get; }

    /// <summary>What holds or fails the tab's URL rules reads.</summary>
    public RulesReads Reads { get; }

    /// <summary>The version of the git the runner found (null when it could not tell).</summary>
    public GitVersion? Version { get; }

    /// <summary>Git's requests to the provider's fake so far.</summary>
    public Func<IReadOnlyList<AuthRemoteRequest>> GitRequestsOf { get; }

    /// <summary>How many creates the provider's fake answered 201 so far.</summary>
    public Func<int> CreatedPosts { get; }

    /// <summary>
    /// The tab on <paramref name="provider"/>'s clone of its scenario, signed out, once its first
    /// load is quiet and its state says <see cref="PullRequestsState.SignedOut"/>.
    /// <paramref name="prepare"/> changes the clone before the tab opens, given the fake's web base.
    /// </summary>
    public static async Task<HeldRulesTab> OpenAsync(HostingProvider provider, CancellationToken ct, Func<TempRepo, Uri, Task>? prepare = null)
    {
        var (served, scenario) = await ServeAsync(provider, ct);
        var data = new TempDirectory("held rules");
        try
        {
            if (prepare is not null)
            {
                await prepare(scenario.Repository, scenario.WebBase);
            }

            var calls = new GitCallLog();
            var log = new ListLog();
            var environment = GitIsolation.SessionGitEnvironment(data.Path);
            var detection = await GitLocator.DetectAsync(calls, log, environment: environment, cancellationToken: ct);
            var runner = detection.Runner ?? throw new InvalidOperationException("The tests need git, and no usable git was found.");
            var prompts = new ScriptedPromptHandler();
            var session = new HostingSession(new AppPaths(data.Path), scenario.Options, new FakeTokenStore(), log, action => Dispatcher.UIThread.Post(action), TimeProvider.System);
            var broker = PromptDialogs.CreateBroker(prompts, log, session, environment, () => detection.Version);
            var repositories = new Repositories(runner);
            var services = new TabServices(
                repositories,
                new NoFolders(),
                new JsonSettingsStore(Path.Combine(data.Path, "settings.json"), log),
                new JsonSessionStore(Path.Combine(data.Path, "session.json"), log),
                new DateDisplay(TimeZoneInfo.Utc),
                log,
                prompts: broker,
                gitCalls: calls,
                hosting: session);
            await Task.Run(() => session.StartAsync(ct), ct);
            var request = new SignInRequest(provider, scenario.Token, scenario.Organization);
            services.SignIn = async signedInTo =>
            {
                Assert.Equal(provider, signedInTo);
                var result = await Task.Run(() => session.SignInAsync(request, ct), ct);
                return result.Status is { State: AccountState.SignedIn };
            };

            var repository = await services.Repositories.OpenAsync(scenario.Repository.Path, ct);
            var viewModel = new RepositoryViewModel(repository, services.Settings, services.Dates, log, null, services.Dialogs, services);
            repositories.Reads!.TabLifetime = ((VisualCommit.App.ViewModels.Pages.IGraphAreaPageHost)viewModel).Lifetime;
            await viewModel.StartAsync();
            await WaitForAsync(() => viewModel.IsWatcherQuiet && viewModel.HostingState.PullRequests == PullRequestsState.SignedOut, "the signed-out tab's first load");
            return new HeldRulesTab(provider, served, data, scenario, calls, log, prompts, broker, session, services, viewModel, repositories.Reads!, detection.Version);
        }
        catch
        {
            data.Dispose();
            await served.DisposeAsync();
            throw;
        }
    }

    /// <summary>Signs the provider's account in through the session, as the accounts' dialog does; the tab hears of it through the session's post.</summary>
    public async Task SignInAsync(CancellationToken ct)
    {
        var result = await Task.Run(() => Session.SignInAsync(new SignInRequest(Provider, Token, Organization), ct), ct);
        Assert.Null(result.Failure);
    }

    /// <summary>How often the URL rules were read by git (the call log's count; a read <see cref="RulesReads"/> fails runs no git).</summary>
    public int RulesReadCalls() => Calls.Snapshot().Count(call => call.CommandText == RulesRead);

    /// <summary>The log's messages since <paramref name="mark"/> lines.</summary>
    public List<string> MessagesSince(int mark) => [.. Log.Lines.Skip(mark).Select(line => line.Message)];

    /// <summary>The form the tab's dialog layer shows, once it has its base.</summary>
    public async Task<CreatePullRequestDialogViewModel> WaitForFormAsync()
    {
        await WaitForAsync(() => Services.DialogHost.Current is CreatePullRequestDialogViewModel, "the create's form");
        var form = (CreatePullRequestDialogViewModel)Services.DialogHost.Current!;
        await form.Starting;
        return form;
    }

    /// <summary>The call log's last sequence number: a mark for <see cref="CallsSince"/>.</summary>
    public long CallMark() => Calls.Snapshot().Select(call => call.Sequence).DefaultIfEmpty(0).Max();

    /// <summary>The git calls recorded after <paramref name="mark"/>, in order.</summary>
    public List<GitCallRecord> CallsSince(long mark) => [.. Calls.Snapshot().Where(call => call.Sequence > mark).OrderBy(call => call.Sequence)];

    /// <summary>
    /// D388's create (docs/phase-7b-design.md 13.3; 7a design 12.3, 12.5): signed out, Create on
    /// <c>local-work</c>, the stacked sign-in, then Create as soon as the form has its base, inside
    /// D290's window. With <paramref name="readFails"/> false the tab's first rules read after the
    /// sign-in is held: Create waits for it before anything of the operation runs (no git call, no
    /// request), and once it is let go pushes with the account's token, adding no rules read. With it
    /// true the tab's reads fail (<see cref="RulesReads.FailTabReads"/>, its warning logged): the
    /// operation reads the rules itself, once, and pushes with the token. Either way: no prompt, git's requests by version
    /// (<see cref="AssertThroughHelper"/>), one POST answered as created, the status
    /// <paramref name="created"/>, the broker's one bound line and the operation's one line of what
    /// it did, its count 1.
    /// </summary>
    public async Task CreateOnLocalWorkInTheWindowAsync(bool readFails, string title, string created, CancellationToken ct)
    {
        var name = Repository.Name;
        var gitMark = GitRequestsOf().Count;
        var posts = CreatedPosts();
        var logMark = Log.Lines.Count;
        var callMark = CallMark();
        if (readFails)
        {
            Reads.FailTabReads();
        }
        else
        {
            Reads.Hold();
        }

        var opening = Repository.Operations.CreatePullRequestAsync("refs/heads/local-work", null);
        var form = await WaitForFormAsync();
        Assert.Equal("origin/main", form.BaseText);
        var failed = $"{name}: the URL rewrites could not be read; the last ones read are kept.";
        if (readFails)
        {
            await WaitForAsync(() => Log.Lines.Any(line => line.Message == failed), "the tab's failed rules read");
        }
        else
        {
            await WaitForAsync(() => Reads.Waiting == 1, "the tab's held rules read");
        }

        // D290: no bindings yet.
        Assert.Empty(Repository.Hosting.Bindings);
        var creating = form.CreateCommand.ExecuteAsync(null);
        if (!readFails)
        {
            await SettleAsync();
            Assert.False(creating.IsCompleted, "The create ended while the tab's rules read was held.");
            Assert.DoesNotContain(CallsSince(callMark), call => call.OperationId is not null || call.CommandText.StartsWith("git push", StringComparison.Ordinal));
            Assert.Equal(gitMark, GitRequestsOf().Count);
            Assert.Equal(1, Reads.Reads);
            Reads.Release();
        }

        await creating.WaitAsync(Limit, ct);
        Assert.True(form.IsClosed, form.Error ?? "The form is still open.");
        await opening.WaitAsync(Limit, ct);
        await WaitForAsync(() => Repository.Queue.Current is null && Repository.IsWatcherQuiet, "the create's end");

        Assert.Empty(Prompts.Prompts);
        AssertThroughHelper(gitMark, "git-receive-pack");
        Assert.Equal(posts + 1, CreatedPosts());
        Assert.Equal(created, Repository.OperationStatus.Text);
        var messages = MessagesSince(logMark);
        Assert.Single(messages, message => message.EndsWith(" URLs bound to the account (Ask).", StringComparison.Ordinal));
        Assert.Contains($"{name}: {title}: 1 of 1 URLs bound to the account (Ask).", messages);
        Assert.Single(messages, message => message.Contains(" URLs bound to the account once ", StringComparison.Ordinal));
        Assert.Contains(readFails ? $"{name}: {title}: 1 URLs bound to the account once the URL rules were read." : $"{name}: {title}: 1 URLs bound to the account once the tab's URL rules were read.", messages);
        Assert.Equal(readFails, messages.Contains(failed));

        // Before the push one rules read ran git: the tab's held one, or the operation's own (the tab's failed ones run none).
        var calls = CallsSince(callMark);
        var push = calls.FindIndex(call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.True(push >= 0, "No push ran.");
        Assert.Equal(1, calls.Take(push).Count(call => call.CommandText == RulesRead));
    }

    /// <summary>
    /// The git requests of one network operation, since <paramref name="mark"/> (part N's "Git's
    /// version", N4; B7's order): from git 2.46 the first carries the account's token; below 2.46 the
    /// first goes without credentials and, on Azure DevOps' <c>&lt;organization&gt;@</c> URL, the
    /// second with that user, each 401, before the helper's. After that prefix every request is
    /// <c>x-access-token</c>'s answered 200, the service's POST (<paramref name="service"/>,
    /// <c>git-receive-pack</c> or <c>git-upload-pack</c>) among them: no other 401 and no other user.
    /// </summary>
    public void AssertThroughHelper(int mark, string service)
    {
        var requests = GitRequestsOf().Skip(mark).ToList();
        (string? GivenUserName, int Status)[] first = Version >= ProactiveAuthVersion
            ? [("x-access-token", 200)]
            : Provider == HostingProvider.AzureDevOps ? [(null, 401), (Organization, 401), ("x-access-token", 200)] : [(null, 401)];
        Assert.True(
            requests.Take(first.Length).Select(request => (request.GivenUserName, request.Status)).SequenceEqual(first),
            $"The first git requests were {Describe(requests.Take(first.Length))}, with git {Version}.");
        Assert.All(requests.Skip(first.Length), request => Assert.True(
            request is { GivenUserName: "x-access-token", Status: 200 },
            $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} answered {request.Status} after the first requests, with git {Version}."));
        var post = Assert.Single(requests, request => request.Method == "POST" && request.Path.EndsWith(service, StringComparison.Ordinal));
        Assert.Equal((200, "x-access-token"), (post.Status, post.GivenUserName));
    }

    public async ValueTask DisposeAsync()
    {
        Repository.Dispose();
        Session.Dispose();
        (_broker as IDisposable)?.Dispose();
        try
        {
            foreach (var token in _tokens)
            {
                TokenTripwire.AssertAbsent(token, _folders, Calls);
                Assert.DoesNotContain(token, Log.Written, StringComparison.Ordinal);
            }
        }
        finally
        {
            await _served.DisposeAsync();
            _data.Dispose();
        }
    }

    /// <summary>Waits on the UI thread, 10 ms a look, at most 60 s.</summary>
    public static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var limit = DateTime.UtcNow + Limit;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, $"Still waiting after 60 s for {what}.");
            await Task.Delay(10);
        }
    }

    /// <summary>Lets the UI thread run what is posted, for a little while.</summary>
    public static async Task SettleAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
        }
    }

    private static string Describe(IEnumerable<AuthRemoteRequest> requests) =>
        string.Join("; ", requests.Select(request => $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} {request.Status}"));

    private static async Task<(IAsyncDisposable Served, Scenario Scenario)> ServeAsync(HostingProvider provider, CancellationToken ct)
    {
        if (provider == HostingProvider.GitHub)
        {
            var hello = await Scenarios.HostingAsync();
            ServedHosting served;
            try
            {
                served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: ct, ownsCopy: true);
            }
            catch
            {
                hello.Dispose();
                throw;
            }

            return (served, new Scenario(
                new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler),
                served.GitHub.Endpoints.WebBase,
                served.Hello,
                served.OctoToken,
                null,
                [served.OctoToken, served.OctoClassicToken, served.AdaToken],
                [served.Hello.Path, served.Tool.Path],
                () => served.GitHub.GitRequests,
                () => served.GitHub.Requests.Count(request => request.Method == "POST" && request.Route is { } route && route.StartsWith("create:", StringComparison.Ordinal) && request.Status == 201)));
        }

        var copy = await Scenarios.Hosting7bAsync();
        ServedHosting7b served7b;
        try
        {
            served7b = await Scenarios.ServeHosting7bAsync(copy, azureDevOpsPort: 0, gitLabPort: 0, gitHubPort: 0, ownsCopy: true, ct: ct);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        var gitLab = provider == HostingProvider.GitLab;
        var fake = gitLab ? (FakeHostingService)served7b.GitLab : served7b.AzureDevOps;
        var options = new HostingOptions(served7b.GitHub.Endpoints, served7b.GitHub.CreateHandler)
        {
            AzureDevOps = served7b.AzureDevOps.Endpoints,
            CreateAzureDevOpsHandler = served7b.AzureDevOps.CreateHandler,
            GitLab = served7b.GitLab.Endpoints,
            CreateGitLabHandler = served7b.GitLab.CreateHandler,
        };
        return (served7b, new Scenario(
            options,
            gitLab ? served7b.GitLab.Endpoints.WebBase : served7b.AzureDevOps.Endpoints.WebBase,
            gitLab ? served7b.WebApp : served7b.MobileApp,
            gitLab ? served7b.GitLabToken : served7b.AzureDevOpsToken,
            gitLab ? null : served7b.Organization,
            [served7b.AzureDevOpsToken, served7b.GitLabToken, served7b.GitLabLegacyToken, served7b.GitHubToken],
            [served7b.MobileApp.Path, served7b.Manifest.Path, served7b.WebApp.Path],
            () => fake.GitRequests,
            () => fake.Requests.Count(request => request.Method == "POST" && request.Route is { } route && route.StartsWith("create:", StringComparison.Ordinal) && request.Status is 200 or 201)));
    }

    private sealed record Scenario(
        HostingOptions Options,
        Uri WebBase,
        TempRepo Repository,
        string Token,
        string? Organization,
        string[] Tokens,
        string[] Folders,
        Func<IReadOnlyList<AuthRemoteRequest>> GitRequests,
        Func<int> CreatedPosts);

    /// <summary>Opens the clone with real git, through <see cref="RulesReads"/>.</summary>
    private sealed class Repositories(IGitRunner runner) : IRepositoryProvider
    {
        public RulesReads? Reads { get; private set; }

        public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            var repository = await Task.Run(() => GitRepository.OpenAsync(runner, path, cancellationToken), cancellationToken);
            var (proxy, reads) = RulesReads.Wrap(repository);
            Reads = reads;
            return proxy;
        }

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

/// <summary>
/// The real repository, except its URL rules reads (<see cref="IGitRepository.ReadUrlRewritesAsync"/>):
/// while <see cref="Hold"/> holds them they wait for <see cref="Release"/> before git runs, and after
/// <see cref="FailTabReads"/> the tab's own reads fail as git's would (a <see cref="GitException"/>,
/// exit 128, without running git). The tab reads under its lifetime's token (<see cref="TabLifetime"/>,
/// <c>IGraphAreaPageHost.Lifetime</c>); an operation's own read at its start (D332, D388) under the
/// operation's cancellation, and that read runs. Failing the tab's reads for good, not once,
/// keeps the window open: after a failed read the tab reads again at its hosting's next change while
/// the rules are not known, which would close it, or be in flight, when the operation starts. Every
/// other call goes straight to the real repository (as App.Tests' <c>HeldReads</c>).
/// </summary>
internal class RulesReads : DispatchProxy
{
    private IGitRepository _inner = null!;
    private TaskCompletionSource _gate = Released();
    private volatile bool _failTabReads;
    private int _reads;
    private int _waiting;

    /// <summary>How many reads were asked for.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <summary>How many reads wait for <see cref="Release"/> now.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    public static (IGitRepository Repository, RulesReads Reads) Wrap(IGitRepository inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        var proxy = Create<IGitRepository, RulesReads>();
        var reads = (RulesReads)(object)proxy;
        reads._inner = inner;
        return (proxy, reads);
    }

    /// <summary>From now on, the reads wait for <see cref="Release"/>.</summary>
    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The reads that wait run now, and those that come later run at once.</summary>
    public void Release() => _gate.TrySetResult();

    /// <summary>The tab's lifetime, which the tab's own reads run under.</summary>
    public CancellationToken TabLifetime { get; set; }

    /// <summary>From now on, every read under <see cref="TabLifetime"/> (the tab's) fails without running git.</summary>
    public void FailTabReads() => _failTabReads = true;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return targetMethod.Name == nameof(IGitRepository.ReadUrlRewritesAsync)
            ? ReadAsync(args is [CancellationToken token] ? token : CancellationToken.None)
            : Forward(targetMethod, args);
    }

    private static TaskCompletionSource Released()
    {
        var released = new TaskCompletionSource();
        released.SetResult();
        return released;
    }

    private async Task<UrlRewrites> ReadAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        if (_failTabReads && cancellationToken.CanBeCanceled && cancellationToken == TabLifetime)
        {
            await Task.Yield();
            throw new GitException(HeldRulesTab.RulesRead, 128, "fatal: bad config line 1 in file .git/config");
        }

        var gate = _gate.Task;
        if (!gate.IsCompleted)
        {
            Interlocked.Increment(ref _waiting);
            try
            {
                await gate.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _waiting);
            }
        }

        return await _inner.ReadUrlRewritesAsync(cancellationToken);
    }

    private object? Forward(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }
}
