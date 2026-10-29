using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;

namespace VisualCommit.App.Tests;

/// <summary>
/// A hosting session for WP-C's App tests (docs/phase-7a-design.md 4.1, 4.8): one account on
/// <see cref="WebBase"/>, git's credentials from a <see cref="RecordingCredentialSource"/>, every
/// repository's hosting a <see cref="StubRepositoryHosting"/> whose <see cref="RepositoryBindings"/>
/// the test sets, <see cref="BindsGit"/> settable with <see cref="BindingsFor"/> binding the given
/// URLs to the account through <see cref="CredentialBinding.Select"/>, and the push guard's ruling 8
/// for the URLs in <see cref="GitHubUrls"/>. It makes no request.
/// </summary>
internal sealed class HostingSessionStub : IHostingSession
{
    private readonly List<StubRepositoryHosting> _hostings = [];
    private IReadOnlyList<CredentialBinding> _repositoryBindings = [];
    private int _bindingsForCalls;

    public HostingSessionStub(Uri webBase, string login, string? token)
    {
        WebBase = webBase;
        Key = new AccountKey(HostingProvider.GitHub, webBase.Authority, login);
        Credentials = new RecordingCredentialSource(token);
        Account = new AccountStatus(new HostingAccount(Key, login, 1, null, null, UseForGit: true), AccountState.SignedIn, TokenPlace.Session, AccountNoteKind.None, null);
    }

    public Uri WebBase { get; }

    public AccountKey Key { get; }

    /// <summary>What git's <c>get</c> and <c>erase</c> reach.</summary>
    public RecordingCredentialSource Credentials { get; }

    /// <summary>URLs, as configured, of remotes recognised as GitHub by <see cref="SourceCanExist"/>.</summary>
    public HashSet<string> GitHubUrls { get; } = new(StringComparer.Ordinal);

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

    /// <summary>How often <see cref="BindingsFor"/> was asked.</summary>
    public int BindingsForCalls => Volatile.Read(ref _bindingsForCalls);

    public bool IsOff => false;

    public IHostingProvider? GitHub => null;

    public AccountStatus? Account { get; set; }

    public bool BindsGit { get; set; }

    public event EventHandler? AccountChanged
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<SignInResult> SignInAsync(string token, CancellationToken ct) => throw new NotSupportedException("The stub signs nobody in.");

    public Task<string?> SignOutAsync(CancellationToken ct) => throw new NotSupportedException("The stub signs nobody out.");

    public void SetUseForGit(bool useForGit) => BindsGit = useForGit;

    public HostedRepository? Recognise(string url) => null;

    /// <summary>Ruling 8 on a recognised URL: false only for <c>refs/pull/&lt;x&gt;/head</c> or <c>/merge</c> whose <c>&lt;x&gt;</c> is not all digits.</summary>
    public bool SourceCanExist(string remoteUrl, string remoteRef)
    {
        if (!GitHubUrls.Contains(remoteUrl) || !remoteRef.StartsWith("refs/pull/", StringComparison.Ordinal))
        {
            return true;
        }

        var rest = remoteRef["refs/pull/".Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || rest[(slash + 1)..] is not ("head" or "merge"))
        {
            return true;
        }

        return slash > 0 && rest[..slash].All(char.IsAsciiDigit);
    }

    public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites)
    {
        Interlocked.Increment(ref _bindingsForCalls);
        return BindsGit ? CredentialBinding.Select(rewrites.Apply(remoteUrls), WebBase, Key) : [];
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

    /// <summary>A repository's hosting with settable bindings and nothing else: not GitHub's in the left panel, no avatars.</summary>
    internal sealed class StubRepositoryHosting : IRepositoryHosting
    {
        public RepositoryHostingState State => RepositoryHostingState.NotGitHub;

        public IReadOnlyList<CredentialBinding> Bindings { get; set; } = [];

        public AuthorAvatars Avatars => AuthorAvatars.None;

        public bool WantsUrlRewrites => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Update(RepoRefs refs, UrlRewrites rewrites)
        {
        }

        public Task RefreshPullRequestsAsync(CancellationToken ct) => Task.CompletedTask;

        public void ShowAvatarsFor(IReadOnlyCollection<string> emails)
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
