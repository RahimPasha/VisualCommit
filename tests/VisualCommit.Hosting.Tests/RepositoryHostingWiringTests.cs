using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The hosting in a repository's view model (docs/phase-7a-design.md 7.10, 7.13; D281, D289,
/// D290, D332), with real git against a fake of GitHub on port 0 and <see cref="TabServices"/> built
/// by hand with a <see cref="HostingSession"/>: no URL-rewrites read signed out; one per refresh
/// signed in, its rules in the bindings; a sign-in with the tab open starting the read, also for a
/// remote that is GitHub's only through the user's <c>insteadOf</c>; a succeeded
/// network operation refreshing the pull requests, a local or a failed one not; a dashboard row's
/// view model without a hosting until promoted; the Debug line once per applied list; and
/// <see cref="RepositoryViewModel.IsWatcherQuiet"/> waiting for a held read.
/// </summary>
public sealed class RepositoryHostingWiringTests
{
    private const string RulesRead = "git config --null --get-regexp ^url\\.";

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("git was not found on PATH.");

    [AvaloniaFact]
    public async Task Signed_out_reads_no_rules_and_signed_in_one_per_refresh_with_its_rules_in_the_bindings()
    {
        await using var wiring = await Wiring.StartAsync();
        using (var signedOut = await wiring.OpenAsync())
        {
            await WaitForAsync(() => signedOut.HostingState.PullRequests == PullRequestsState.SignedOut, "the signed-out state");
            Assert.Equal(0, wiring.RulesReads());
            Assert.Empty(signedOut.Hosting.Bindings);
        }

        await wiring.SignInAsync();
        using var viewModel = await wiring.OpenAsync();
        await WaitForAsync(() => viewModel.HostingState.PullRequests == PullRequestsState.Loaded && viewModel.IsWatcherQuiet, "the list");
        Assert.Equal(1, wiring.RulesReads());
        var url = Scenarios.HostingUrl("octo/hello", wiring.Fake.Port);
        Assert.Equal([new CredentialBinding(url, HostingKey(wiring))], viewModel.Hosting.Bindings);
        Assert.Equal(4, viewModel.HostingState.List!.PullRequests.Count);
        Assert.Single(wiring.Log.Lines, line => line.Message == "hello: listed 4 open pull requests of octo/hello");

        // A rule and a remote that needs it: the refresh reads the rules once more and binds through them.
        var (marked, refsMarked) = (wiring.RulesReads(), wiring.RefsReads());
        await wiring.Served.Hello.GitAsync("config", $"url.{wiring.Fake.Endpoints.WebBase}.insteadOf", "gh:");
        await wiring.Served.Hello.GitAsync("remote", "add", "ada", "gh:ada/hello.git");
        await Dispatcher.UIThread.InvokeAsync(viewModel.RefreshAsync);
        await WaitForAsync(() => viewModel.Hosting.Bindings.Count == 2 && viewModel.IsWatcherQuiet, "the rule's binding");
        Assert.Contains(new CredentialBinding(Scenarios.HostingUrl("ada/hello", wiring.Fake.Port), HostingKey(wiring)), viewModel.Hosting.Bindings);
        Assert.InRange(wiring.RulesReads() - marked, 1, 3);
        Assert.Equal(wiring.RefsReads() - refsMarked, wiring.RulesReads() - marked);
    }

    [AvaloniaFact]
    public async Task A_sign_in_with_the_tab_open_starts_the_rules_read_and_the_list()
    {
        await using var wiring = await Wiring.StartAsync();
        using var viewModel = await wiring.OpenAsync();
        Assert.Equal(0, wiring.RulesReads());

        await wiring.SignInAsync();

        await WaitForAsync(() => viewModel.Hosting.Bindings.Count == 1 && viewModel.HostingState.PullRequests == PullRequestsState.Loaded && viewModel.IsWatcherQuiet, "the bindings and the list");
        Assert.Equal(1, wiring.RulesReads());
    }

    [AvaloniaFact]
    public async Task A_sign_in_with_the_tab_open_reads_the_rules_of_a_remote_github_only_through_them()
    {
        await using var wiring = await Wiring.StartAsync();
        await wiring.Served.Hello.GitAsync("config", $"url.{wiring.Fake.Endpoints.WebBase}.insteadOf", "gh:");
        await wiring.Served.Hello.GitAsync("remote", "set-url", "origin", "gh:octo/hello.git");
        using var viewModel = await wiring.OpenAsync();
        Assert.Same(RepositoryHostingState.NotGitHub, viewModel.HostingState);
        Assert.Equal(0, wiring.RulesReads());

        // Nothing but the wish for the rules changes at the sign-in; that alone starts their read.
        await wiring.SignInAsync();

        await WaitForAsync(() => viewModel.HostingState.PullRequests == PullRequestsState.Loaded && viewModel.IsWatcherQuiet, "the list through the rule");
        Assert.Equal(1, wiring.RulesReads());
        Assert.Equal("octo/hello", viewModel.HostingState.Repository!.FullName);
        Assert.Equal([new CredentialBinding(Scenarios.HostingUrl("octo/hello", wiring.Fake.Port), HostingKey(wiring))], viewModel.Hosting.Bindings);
    }

    [AvaloniaFact]
    public async Task A_succeeded_network_operation_refreshes_the_pull_requests_and_a_local_or_failed_one_does_not()
    {
        await using var wiring = await Wiring.StartAsync();
        await wiring.SignInAsync();
        using var viewModel = await wiring.OpenAsync();
        await WaitForAsync(() => viewModel.HostingState.PullRequests == PullRequestsState.Loaded, "the list");
        await Settle();
        var lists = wiring.Lists();

        // Held, a refresh of either is seen as it reaches the fake: the fake records a list only
        // after the git it runs for it, which a loaded machine slows ten times over (D237).
        using (var hold = wiring.Fake.HoldNext("pulls:octo/hello"))
        {
            await viewModel.Queue.RunAsync(Operation(OperationKind.Local, fail: false));
            await viewModel.Queue.RunAsync(Operation(OperationKind.Network, fail: true));

            // The queue raises Finished before RunAsync's task completes, so a refresh posted from
            // it has started once the dispatcher has gone round below that post's priority.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Settle();
            Assert.False(hold.Arrived.IsCompleted, "A local or a failed operation asked for the pull requests.");
        }

        Assert.Equal(lists, wiring.Lists());

        await viewModel.Queue.RunAsync(Operation(OperationKind.Network, fail: false));
        await WaitForAsync(() => wiring.Lists() == lists + 1, "the refresh after the fetch");

        // A late refresh of the local or the failed operation would come on top.
        await Settle();
        Assert.Equal(lists + 1, wiring.Lists());
    }

    [AvaloniaFact]
    public async Task A_dashboard_rows_view_model_holds_no_hosting_until_a_tab_adopts_it()
    {
        await using var wiring = await Wiring.StartAsync();
        await wiring.SignInAsync();
        var repository = await wiring.Services.Repositories.OpenAsync(wiring.Served.Hello.Path);
        using var viewModel = RepositoryViewModel.ForSummary(repository, wiring.Services);

        await viewModel.StartAsync();
        await Settle();
        Assert.Same(NullRepositoryHosting.Instance, viewModel.Hosting);
        Assert.Equal(0, wiring.RulesReads());
        Assert.True(viewModel.IsWatcherQuiet);

        viewModel.Promote(Stopwatch.StartNew());
        Assert.IsType<RepositoryHosting>(viewModel.Hosting);
        await viewModel.StartAsync();
        await WaitForAsync(() => viewModel.HostingState.PullRequests == PullRequestsState.Loaded, "the adopted tab's list");
        Assert.Equal(1, wiring.RulesReads());
    }

    [AvaloniaFact]
    public async Task A_held_rules_read_keeps_the_view_model_from_being_quiet_after_a_sign_in_and_after_a_refresh()
    {
        await using var wiring = await Wiring.StartAsync();
        var repository = new HeldRulesRepository(Scenarios.HostingUrl("octo/hello", wiring.Fake.Port));
        using var viewModel = new RepositoryViewModel(repository, wiring.Services.Settings, wiring.Services.Dates, wiring.Log, null, null, wiring.Services);
        await viewModel.StartAsync();
        Assert.True(viewModel.IsWatcherQuiet);

        var held = repository.Hold();
        await wiring.SignInAsync();
        await WaitForAsync(() => repository.RulesReads == 1, "the read after the sign-in");
        Assert.False(viewModel.IsWatcherQuiet);
        held.SetResult(UrlRewrites.None);
        await WaitForAsync(() => viewModel.IsWatcherQuiet, "the read's end");

        held = repository.Hold();
        await viewModel.RefreshAsync();
        Assert.Equal(2, repository.RulesReads);
        Assert.False(viewModel.IsWatcherQuiet, "The refresh's status read has ended and its rules read is held.");
        held.SetResult(UrlRewrites.None);
        await WaitForAsync(() => viewModel.IsWatcherQuiet, "the second read's end");
        Assert.Single(viewModel.Hosting.Bindings);
    }

    private static Core.Hosting.AccountKey HostingKey(Wiring wiring) => new(Core.Hosting.HostingProvider.GitHub, wiring.Fake.Endpoints.Host, "octo");

    private static GitOperation Operation(OperationKind kind, bool fail) =>
        new(fail ? "Fetch" : kind == OperationKind.Network ? "Fetch" : "Stage", "Working…", kind, RefreshScope.None, _ =>
            fail ? throw new InvalidOperationException("It failed.") : Task.FromResult(OperationReport.Done("Done")));

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var limit = DateTime.UtcNow + Limit;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, $"Still waiting after 60 s for {what}.");
            await Task.Delay(10);
        }
    }

    /// <summary>A copy of "hosting" served on port 0, a hosting session over its fake, and tab services built by hand with real git.</summary>
    private sealed class Wiring : IAsyncDisposable
    {
        private Wiring(ServedHosting served, TempDirectory data, GitCallLog calls, ListLog log, HostingSession session, TabServices services)
        {
            Log = log;
            Served = served;
            Data = data;
            Calls = calls;
            Session = session;
            Services = services;
        }

        public ServedHosting Served { get; }

        public FakeGitHub Fake => Served.GitHub;

        public TempDirectory Data { get; }

        public GitCallLog Calls { get; }

        public ListLog Log { get; }

        public HostingSession Session { get; }

        public TabServices Services { get; }

        public static async Task<Wiring> StartAsync()
        {
            var hello = await Scenarios.HostingAsync();
            var served = await Scenarios.ServeHostingAsync(hello, port: 0, ownsCopy: true);
            var data = new TempDirectory("hosting-wiring");
            var calls = new GitCallLog();
            var runner = new GitRunner(GitPath, calls, environment: GitIsolation.SessionGitEnvironment(data.Path));
            var log = new ListLog();
            var session = new HostingSession(new AppPaths(data.Path), new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler), new FakeTokenStore(), log, action => Dispatcher.UIThread.Post(action), TimeProvider.System);
            var services = new TabServices(
                new Repositories(runner),
                new NoFolders(),
                new JsonSettingsStore(Path.Combine(data.Path, "settings.json"), log),
                new JsonSessionStore(Path.Combine(data.Path, "session.json"), log),
                new DateDisplay(TimeZoneInfo.Utc),
                log,
                gitCalls: calls,
                hosting: session);
            await Task.Run(() => session.StartAsync(CancellationToken.None));
            return new Wiring(served, data, calls, log, session, services);
        }

        public async Task<RepositoryViewModel> OpenAsync()
        {
            var repository = await Services.Repositories.OpenAsync(Served.Hello.Path);
            var viewModel = new RepositoryViewModel(repository, Services.Settings, Services.Dates, Log, null, null, Services);
            await viewModel.StartAsync();
            await WaitForAsync(() => viewModel.IsWatcherQuiet, "the opening's reads");
            return viewModel;
        }

        public async Task SignInAsync()
        {
            var result = await Task.Run(() => Session.SignInAsync(Served.OctoToken, CancellationToken.None));
            Assert.Null(result.Failure);
        }

        public int RulesReads() => Calls.Snapshot().Count(call => call.CommandText == RulesRead);

        public int RefsReads() => Calls.Snapshot().Count(call => call.CommandText == "git symbolic-ref -q HEAD");

        public int Lists() => Fake.Requests.Count(request => request.Route == "pulls:octo/hello");

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            try
            {
                TokenTripwire.AssertAbsent(Served.OctoToken, [Data.Path], Calls);
                Assert.All(Log.Lines, line => Assert.DoesNotContain("_FAKE-", line.Message, StringComparison.Ordinal));
            }
            finally
            {
                await Served.DisposeAsync();
                Data.Dispose();
            }
        }
    }

    private sealed class Repositories(GitRunner runner) : IRepositoryProvider
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

    /// <summary>A repository with one GitHub remote whose URL-rewrites read the test holds (as <c>RepositoryWatcherQuietTests</c>' one).</summary>
    private sealed class HeldRulesRepository(string url) : FakeRepositoryBase
    {
        private const string Main = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
        private TaskCompletionSource<UrlRewrites>? _held;
        private int _rulesReads;

        public int RulesReads => Volatile.Read(ref _rulesReads);

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "hosting-held-rules");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "hosting-held-rules";

        public TaskCompletionSource<UrlRewrites> Hold() => _held = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepoRefs(new HeadState("main", Main), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Main, IsHead: true)], [], ["origin"])
            {
                RemoteUrls = [new RemoteUrl("origin", url, IsPushUrl: false)],
            });

        public override Task<UrlRewrites> ReadUrlRewritesAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _rulesReads);
            return Interlocked.Exchange(ref _held, null)?.Task ?? Task.FromResult(UrlRewrites.None);
        }

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => new QuietWatcher();
    }

    private sealed class QuietWatcher : IRepositoryWatcher
    {
        public event EventHandler<RepositoryChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public IDisposable Pause() => new Resume();

        public void Dispose()
        {
        }

        private sealed class Resume : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
