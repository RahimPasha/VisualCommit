using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;

namespace VisualCommit.App.Tests;

/// <summary>
/// A hosting session for WP-C's App tests (docs/phase-7a-design.md 4.1, 4.8; docs/phase-7b-design.md
/// 8.7): one account of one provider under <see cref="Coverage"/>, git's credentials from a
/// <see cref="RecordingCredentialSource"/>, every repository's hosting a
/// <see cref="StubRepositoryHosting"/> whose <see cref="RepositoryBindings"/> the test sets,
/// <see cref="BindsGit"/> settable with <see cref="BindingsFor"/> binding the given URLs to the
/// account through <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>,
/// and the push guard's rule (<see cref="PullRequestRefs.SourceCanExist"/>) for the URLs in
/// <see cref="GitHubUrls"/> (GitHub's) and <see cref="RecognisedUrls"/> (any provider's). It makes
/// no request.
/// </summary>
internal sealed class HostingSessionStub : IHostingSession
{
    private readonly List<StubRepositoryHosting> _hostings = [];
    private IReadOnlyList<CredentialBinding> _repositoryBindings = [];
    private int _bindingsForCalls;

    /// <summary>7a's stub: a GitHub account <paramref name="login"/> on <paramref name="webBase"/>'s host, GitHub's coverage; the status keeps the login's case.</summary>
    public HostingSessionStub(Uri webBase, string login, string? token)
        : this(new GitCoverage(new AccountKey(HostingProvider.GitHub, webBase.Authority, login), webBase), token, login)
    {
    }

    /// <summary>The coverage's account signed in, answering git with <paramref name="token"/>; a test sets <see cref="Account"/> for a login in another case.</summary>
    public HostingSessionStub(GitCoverage coverage, string? token)
        : this(coverage, token, coverage?.Account.Login!)
    {
    }

    private HostingSessionStub(GitCoverage coverage, string? token, string login)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        Coverage = coverage;
        Key = coverage.Account;
        Credentials = new RecordingCredentialSource(token);
        Account = new AccountStatus(new HostingAccount(Key, login, 1, null, null, UseForGit: true), AccountState.SignedIn, TokenPlace.Session, AccountNoteKind.None, null);
    }

    /// <summary>What the account covers for git: <see cref="BindingsFor"/> applies it.</summary>
    public GitCoverage Coverage { get; }

    public Uri WebBase => Coverage.Origin;

    public AccountKey Key { get; }

    /// <summary>What git's <c>get</c> and <c>erase</c> reach.</summary>
    public RecordingCredentialSource Credentials { get; }

    /// <summary>URLs, as configured, of remotes recognised as GitHub by <see cref="SourceCanExist"/>.</summary>
    public HashSet<string> GitHubUrls { get; } = new(StringComparer.Ordinal);

    /// <summary>URLs, as configured, of remotes <see cref="SourceCanExist"/> takes as the given provider's, an account's or not (D279).</summary>
    public Dictionary<string, HostingProvider> RecognisedUrls { get; } = new(StringComparer.Ordinal);

    /// <summary>The bindings of every repository's hosting, those made already and those to come.</summary>
    public IReadOnlyList<CredentialBinding> RepositoryBindings
    {
        get => _repositoryBindings;
        set
        {
            _repositoryBindings = value;
            lock (_hostings)
            {
                _hostings.ForEach(hosting => hosting.Bindings = value);
            }
        }
    }

    /// <summary>The repositories' hostings made so far, in order (WP-P: a test sets one's <see cref="StubRepositoryHosting.State"/>).</summary>
    public IReadOnlyList<StubRepositoryHosting> Hostings
    {
        get
        {
            lock (_hostings)
            {
                return [.. _hostings];
            }
        }
    }

    /// <summary>How often <see cref="BindingsFor"/> was asked.</summary>
    public int BindingsForCalls => Volatile.Read(ref _bindingsForCalls);

    public bool IsOff => false;

    public IReadOnlyList<HostingProvider> Providers => [Key.Provider];

    public IHostingProvider? GitHub => null;

    public AccountStatus? Account { get; set; }

    public bool BindsGit { get; set; }

    public event EventHandler<HostingAccountChangedEventArgs>? AccountChanged
    {
        add { }
        remove { }
    }

    public bool IsOn(HostingProvider provider) => provider == Key.Provider;

    public IHostingProvider? ProviderOf(HostingProvider provider) => null;

    public AccountStatus? AccountOf(HostingProvider provider) => provider == Key.Provider ? Account : null;

    public Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct) => throw new NotSupportedException("The stub signs nobody in.");

    public Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct) => throw new NotSupportedException("The stub signs nobody out.");

    public void SetUseForGit(HostingProvider provider, bool useForGit)
    {
        if (provider == Key.Provider)
        {
            SetUseForGit(useForGit);
        }
    }

    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct) => throw new NotSupportedException("The stub lists nothing.");

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<SignInResult> SignInAsync(string token, CancellationToken ct) => throw new NotSupportedException("The stub signs nobody in.");

    public Task<string?> SignOutAsync(CancellationToken ct) => throw new NotSupportedException("The stub signs nobody out.");

    public void SetUseForGit(bool useForGit) => BindsGit = useForGit;

    public HostedRepository? Recognise(string url) => null;

    /// <summary>Rulings 8 and 14 on a recognised URL: <see cref="PullRequestRefs.SourceCanExist"/> of its provider; true for a URL not recognised.</summary>
    public bool SourceCanExist(string remoteUrl, string remoteRef) =>
        GitHubUrls.Contains(remoteUrl) ? PullRequestRefs.SourceCanExist(HostingProvider.GitHub, remoteRef)
        : RecognisedUrls.TryGetValue(remoteUrl, out var provider) ? PullRequestRefs.SourceCanExist(provider, remoteRef)
        : true;

    public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites)
    {
        Interlocked.Increment(ref _bindingsForCalls);
        return BindsGit ? CredentialBinding.Select(rewrites.Apply(remoteUrls), Coverage) : [];
    }

    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) => throw new NotSupportedException("The stub lists nothing.");

    public IRepositoryHosting ForRepository(string workingDirectory)
    {
        var hosting = new StubRepositoryHosting { Bindings = _repositoryBindings };
        lock (_hostings)
        {
            _hostings.Add(hosting);
        }

        return hosting;
    }

    public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Credentials.GetAsync(account, cancellationToken);

    public void Refused(AccountKey account) => Credentials.Refused(account);

    public void Dispose()
    {
    }

    /// <summary>
    /// A repository's hosting with settable bindings and state (WP-P, docs/phase-7a-design.md 10.6:
    /// a set state raises <see cref="Changed"/> at once, on the setter's thread) and nothing else:
    /// not GitHub's in the left panel until a test sets a state, no avatars.
    /// </summary>
    internal sealed class StubRepositoryHosting : IRepositoryHosting
    {
        private RepositoryHostingState _state = RepositoryHostingState.NoHostedRemote;
        private EventHandler? _changed;

        public RepositoryHostingState State
        {
            get => _state;
            set
            {
                _state = value;
                _changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public IReadOnlyList<CredentialBinding> Bindings { get; set; } = [];

        public AuthorAvatars Avatars => AuthorAvatars.None;

        public bool WantsUrlRewrites => false;

        public event EventHandler? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void Update(RepoRefs refs, UrlRewrites rewrites)
        {
        }

        public Task RefreshPullRequestsAsync(CancellationToken ct) => Task.CompletedTask;

        public void ShowAvatarsFor(IReadOnlyCollection<AuthorCommit> authors)
        {
        }

        public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) =>
            Task.FromResult(HostingResult<RepositoryInfo>.Fail(new HostingFailure(HostingFailureKind.NotFound, "The stub has no repository.")));

        public Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct) =>
            Task.FromResult(HostingResult<PullRequest>.Fail(new HostingFailure(HostingFailureKind.NotFound, "The stub has no repository.")));

        public void Dispose()
        {
        }
    }
}
