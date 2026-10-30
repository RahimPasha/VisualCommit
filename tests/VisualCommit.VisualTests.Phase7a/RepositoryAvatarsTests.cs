using System.Diagnostics;
using System.Globalization;
using System.Text;
using VisualCommit.App.Services;
using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// What a repository tab tells its hosting about its authors (docs/phase-7a-design.md 11.3, 11.4;
/// D257, D311), with fakes of <see cref="IHostingSession"/> and <see cref="IRepositoryHosting"/> in
/// hand-built <see cref="TabServices"/>: the set (lower-case, newest first, at most 200, of the first
/// 2,000 rows) of the commits a GitHub remote's branch reaches only; nothing without an account's
/// listing or in summary mode; not twice, but again after a sign-out; the avatars to the details
/// panel; and the line the graph's view writes.
/// </summary>
public sealed class RepositoryAvatarsTests : IAsyncLifetime
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The tests need git."));

    private static readonly HostedRepository OctoHello = new(HostingProvider.GitHub, "github.com", "octo", "hello");

    private readonly TempDirectory _data = new("phase7a repository avatars");
    private readonly FakeSession _session = new();
    private readonly RecordingLog _log = new();
    private TempRepo? _repo;

    public async ValueTask InitializeAsync()
    {
        // Octo's and Ada's commits are on origin's main; Bob's only on another server's branch;
        // Lin's only local, on main.
        var repo = await TempRepo.CreateAsync("hello");
        _repo = repo;
        await repo.CommitFileAsAsync("a.txt", "1\n", "One", "Octo Cat", "octo@example.com");
        var onOrigin = await repo.CommitFileAsAsync("b.txt", "2\n", "Two", "Ada Lovelace", "Ada@Example.com");
        await repo.GitAsync("remote", "add", "origin", "https://github.com/octo/hello.git");
        await repo.GitAsync("remote", "add", "other", "https://example.com/other.git");
        await repo.GitAsync("update-ref", "refs/remotes/origin/main", onOrigin);
        var onOther = await repo.CommitFileAsAsync("c.txt", "3\n", "Three", "Bob", "bob@example.com");
        await repo.GitAsync("update-ref", "refs/remotes/other/topic", onOther);
        await repo.GitAsync("reset", "--hard", "--quiet", onOrigin);
        await repo.CommitFileAsAsync("d.txt", "4\n", "Four", "Lin", "lin@example.com");
        await repo.CommitFileAsAsync("e.txt", "5\n", "Five", "Ada Lovelace", "ada@example.com");
    }

    public ValueTask DisposeAsync()
    {
        _repo?.Dispose();
        _data.Dispose();
        return ValueTask.CompletedTask;
    }

    private static RepositoryHostingState State(PullRequestsState state, bool gitHub = true) =>
        new(state, gitHub ? new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = OctoHello } : new Dictionary<string, HostedRepository>(StringComparer.Ordinal), gitHub ? OctoHello : null, null, null, new Dictionary<string, PullRequest>(StringComparer.Ordinal));

    private TabServices Services() => new(
        new NoRepositories(),
        new NoFolders(),
        new JsonSettingsStore(Path.Combine(_data.Path, "settings.json")),
        new JsonSessionStore(Path.Combine(_data.Path, "session.json")),
        new DateDisplay(TimeZoneInfo.Utc),
        _log,
        hosting: _session);

    private async Task<RepositoryViewModel> OpenAsync(string path, bool summary = false)
    {
        var repository = await GitRepository.OpenAsync(Runner, path, TestContext.Current.CancellationToken);
        var services = Services();
        var viewModel = summary
            ? RepositoryViewModel.ForSummary(repository, services)
            : new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, services: services);
        await viewModel.StartAsync();
        await WaitAsync(() => summary || viewModel.Graph.IsComplete || viewModel.Graph.Count >= RepositoryViewModel.AvatarRows, "the graph to load");
        return viewModel;
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"Timed out waiting for {what}.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task The_set_is_the_authors_a_github_branch_reaches_lower_case_newest_first_and_the_avatars_reach_the_details()
    {
        using var viewModel = await OpenAsync(_repo!.Path);
        var hosting = _session.Hostings.Single();
        Assert.Empty(hosting.Sent);

        hosting.Raise(State(PullRequestsState.Loading));

        // Lin's commit is only local and Bob's only on another server: neither address is sent. Ada's
        // newest commit is local, but her older one is on origin.
        Assert.Equal([["ada@example.com", "octo@example.com"]], hosting.Sent);

        // The same set is not sent again, whatever state follows.
        hosting.Raise(State(PullRequestsState.Loaded));
        hosting.Raise(State(PullRequestsState.Failed));
        Assert.Single(hosting.Sent);

        // The avatars applied reach the details panel, which finds the shown commit's author.
        var avatars = new AuthorAvatars(new Dictionary<string, string>(StringComparer.Ordinal) { ["octo@example.com"] = Path.Combine(_data.Path, "1001.png") });
        hosting.Raise(State(PullRequestsState.Loaded), avatars);
        Assert.Same(avatars, viewModel.Avatars);
        Assert.Same(avatars, viewModel.Details.Avatars);
    }

    [Fact]
    public async Task Nothing_is_sent_without_an_account_s_listing_or_in_summary_mode()
    {
        using var viewModel = await OpenAsync(_repo!.Path);
        var hosting = _session.Hostings.Single();
        hosting.Raise(RepositoryHostingState.NotGitHub);
        hosting.Raise(State(PullRequestsState.SignedOut));
        Assert.Empty(hosting.Sent);

        using var summary = await OpenAsync(_repo.Path, summary: true);
        Assert.Single(_session.Hostings);
        Assert.IsType<NullRepositoryHosting>(summary.Hosting);
    }

    [Fact]
    public async Task A_sign_out_forgets_the_set_so_the_next_sign_in_sends_it_again()
    {
        using var viewModel = await OpenAsync(_repo!.Path);
        var hosting = _session.Hostings.Single();
        hosting.Raise(State(PullRequestsState.Loaded));
        hosting.Raise(State(PullRequestsState.SignedOut));
        hosting.Raise(State(PullRequestsState.Loading));
        Assert.Equal(2, hosting.Sent.Count);
        Assert.Equal(hosting.Sent[0], hosting.Sent[1]);

        // A repository whose remote is no longer GitHub's forgets it too.
        hosting.Raise(RepositoryHostingState.NotGitHub);
        hosting.Raise(State(PullRequestsState.Loaded));
        Assert.Equal(3, hosting.Sent.Count);
    }

    [Fact]
    public async Task A_loading_graph_is_followed_until_2000_rows_and_no_later_row_is_told()
    {
        // 2,100 commits on origin's main: the 100 oldest by old@example.com, row 2000 on.
        using var big = await TempRepo.CreateAsync("big");
        var import = new StringBuilder();
        for (var i = 1; i <= 2100; i++)
        {
            var email = i <= 100 ? "old@example.com" : string.Create(CultureInfo.InvariantCulture, $"author{i % 300}@example.com");
            var message = string.Create(CultureInfo.InvariantCulture, $"Commit {i}\n");
            import.Append(CultureInfo.InvariantCulture, $"commit refs/heads/main\nauthor A <{email}> {1767268800 + i} +0000\ncommitter A <{email}> {1767268800 + i} +0000\ndata {Encoding.UTF8.GetByteCount(message)}\n{message}");
            import.Append('\n');
        }

        await big.GitWithInputAsync(import.ToString(), "fast-import", "--quiet");
        await big.GitAsync("reset", "--hard", "--quiet", "main");
        await big.GitAsync("remote", "add", "origin", "https://github.com/octo/big.git");
        await big.GitAsync("update-ref", "refs/remotes/origin/main", "main");

        using var viewModel = await OpenAsync(big.Path);
        await WaitAsync(() => viewModel.Graph.IsComplete, "the whole graph");
        var hosting = _session.Hostings.Single();
        hosting.Raise(State(PullRequestsState.Loaded));

        // 300 addresses among the newest 2,000 rows: the first 200 of them, newest first; none of the oldest.
        var sent = Assert.Single(hosting.Sent);
        Assert.Equal(RepositoryViewModel.AvatarAuthors, sent.Count);
        Assert.Equal(sent.Count, sent.Distinct().Count());
        Assert.Equal("author0@example.com", sent[0]);
        Assert.DoesNotContain("old@example.com", sent);

        // The static pass over the same graph says the same.
        Assert.Equal(sent, RepositoryViewModel.AuthorsOnGitHub(viewModel.Graph, hosting.State.Remotes));
    }

    [Fact]
    public async Task The_graph_s_line_is_written_at_debug_and_matched_by_the_harness()
    {
        using var viewModel = await OpenAsync(_repo!.Path);
        viewModel.NoteAvatarsShown(2);
        var entry = Assert.Single(_log.Entries, entry => entry.Message.Contains("avatars", StringComparison.Ordinal));
        Assert.Equal((LogLevel.Debug, "hello: showing GitHub avatars for 2 authors"), (entry.Level, entry.Message));
        var match = AppLogFile.AvatarsShown("hello").Match("2026-01-01 12:00:00.000 +00:00 [DBG] " + entry.Message);
        Assert.True(match.Success);
        Assert.Equal("2", match.Groups["count"].Value);
    }

    /// <summary>The session's fake: one <see cref="FakeHosting"/> per repository it is asked for; otherwise off.</summary>
    private sealed class FakeSession : IHostingSession
    {
        private readonly List<FakeHosting> _hostings = [];

        public IReadOnlyList<FakeHosting> Hostings
        {
            get
            {
                lock (_hostings)
                {
                    return [.. _hostings];
                }
            }
        }

        public bool IsOff => false;

        public IHostingProvider? GitHub => null;

        public AccountStatus? Account => null;

        public bool BindsGit => false;

        public event EventHandler? AccountChanged
        {
            add { }
            remove { }
        }

        public IRepositoryHosting ForRepository(string workingDirectory)
        {
            var hosting = new FakeHosting();
            lock (_hostings)
            {
                _hostings.Add(hosting);
            }

            return hosting;
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(false);

        public Task<SignInResult> SignInAsync(string token, CancellationToken ct) => throw new NotSupportedException();

        public Task<string?> SignOutAsync(CancellationToken ct) => throw new NotSupportedException();

        public void SetUseForGit(bool useForGit)
        {
        }

        public HostedRepository? Recognise(string url) => null;

        public bool SourceCanExist(string remoteUrl, string remoteRef) => true;

        public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

        public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites) => [];

        public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Task.FromResult<GitCredential?>(null);

        public void Refused(AccountKey account)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A repository's fake hosting: its state and avatars set by the test and announced through <see cref="Changed"/>; the sets it is told, recorded.</summary>
    private sealed class FakeHosting : IRepositoryHosting
    {
        public RepositoryHostingState State { get; private set; } = RepositoryHostingState.NotGitHub;

        public IReadOnlyList<CredentialBinding> Bindings => [];

        public AuthorAvatars Avatars { get; private set; } = AuthorAvatars.None;

        public bool WantsUrlRewrites => false;

        public List<IReadOnlyList<string>> Sent { get; } = [];

        public event EventHandler? Changed;

        /// <summary>Sets the state (and the avatars) and raises <see cref="Changed"/>, as the session's post would on the UI thread.</summary>
        public void Raise(RepositoryHostingState state, AuthorAvatars? avatars = null)
        {
            State = state;
            Avatars = avatars ?? Avatars;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Update(RepoRefs refs, UrlRewrites rewrites)
        {
        }

        public Task RefreshPullRequestsAsync(CancellationToken ct) => Task.CompletedTask;

        public void ShowAvatarsFor(IReadOnlyCollection<string> emails) => Sent.Add([.. emails]);

        public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
