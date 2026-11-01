using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting;

/// <summary>Whether the account can be used (docs/phase-7a-design.md 1.5).</summary>
public enum AccountState
{
    SignedIn,
    NeedsSignIn,
}

/// <summary>Where the account's token is kept (D253).</summary>
public enum TokenPlace
{
    /// <summary>In this computer's keychain.</summary>
    Keychain,

    /// <summary>In memory, until the app closes.</summary>
    Session,
}

/// <summary>Which of the app's notes applies to the account (docs/phase-7a-design.md 8.2).</summary>
public enum AccountNoteKind
{
    None,
    NoKeychain,
    SaveFailed,
    Refused,
    TokenMissing,
    ReadFailed,
    SessionEnded,
}

/// <summary>The signed-in account, or the one that needs signing in again.</summary>
/// <param name="Account">The account.</param>
/// <param name="State">Whether it can be used.</param>
/// <param name="Place">Where its token is kept; null without one.</param>
/// <param name="NoteKind">Which of the app's texts applies (8.2).</param>
/// <param name="Note">The store's reason, when one exists.</param>
public sealed record AccountStatus(HostingAccount Account, AccountState State, TokenPlace? Place, AccountNoteKind NoteKind, string? Note);

/// <summary>A sign-in's outcome.</summary>
/// <param name="Status">The account afterwards; null when the sign-in failed.</param>
/// <param name="Failure">The service refused the token or was not reached.</param>
/// <param name="SaveFailedReason">The keychain could not keep the token: it is kept for the session (ruling 19).</param>
public sealed record SignInResult(AccountStatus? Status, HostingFailure? Failure, string? SaveFailedReason);

/// <summary>Which provider's account, or its state, changed (docs/phase-7b-design.md 1.5).</summary>
public sealed class HostingAccountChangedEventArgs(HostingProvider provider) : EventArgs
{
    public HostingProvider Provider { get; } = provider;
}

/// <summary>A1's input (docs/phase-7b-design.md 1.5): the provider, the token and Azure DevOps' organization. Its text never shows the token.</summary>
/// <param name="Provider">The provider to sign in to.</param>
/// <param name="Token">The token typed.</param>
/// <param name="Organization">Azure DevOps' organization; null for the others.</param>
public sealed record SignInRequest(HostingProvider Provider, string Token, string? Organization = null)
{
    public override string ToString() => Organization is null
        ? $"SignInRequest({Provider}, ***)"
        : $"SignInRequest({Provider}, ***, {Organization})";
}

/// <summary>
/// The app session's hosting (docs/phase-7a-design.md 1.5, docs/phase-7b-design.md 1.5): a slot per
/// provider with its account and token, recognition of the services' URLs and the bindings of git's
/// credentials to the accounts. One per app session; its events come through the post delegate, on
/// the UI thread.
/// </summary>
public interface IHostingSession : IGitCredentialSource, IDisposable
{
    /// <summary>Every provider is off: nothing is recognised and no request is made.</summary>
    bool IsOff { get; }

    /// <summary>The providers on, in <see cref="HostingProviders.InAccountsOrder"/>.</summary>
    IReadOnlyList<HostingProvider> Providers { get; }

    /// <summary>Whether <paramref name="provider"/> is on.</summary>
    bool IsOn(HostingProvider provider);

    /// <summary><paramref name="provider"/>'s provider; null while it is off.</summary>
    IHostingProvider? ProviderOf(HostingProvider provider);

    /// <summary><paramref name="provider"/>'s account, signed in or needing sign-in; null without one.</summary>
    AccountStatus? AccountOf(HostingProvider provider);

    /// <summary>An account or its state changed; the arguments name its provider.</summary>
    event EventHandler<HostingAccountChangedEventArgs>? AccountChanged;

    /// <summary>Reads <c>accounts.json</c> and each account's token, once (ruling 6).</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Whether a keychain is there: the one cached probe, which never prompts.</summary>
    Task<bool> KeychainAvailableAsync(CancellationToken ct);

    /// <summary>A1 with the request's token; replaces that provider's account (D348), at once (D256).</summary>
    Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct);

    /// <summary>Signs out <paramref name="provider"/>'s account (GitHub's alone deletes <c>avatars/</c>, D291): null, or why the keychain kept its entry.</summary>
    Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct);

    /// <summary>"Use for git over HTTPS" of <paramref name="provider"/>'s account, at the settings screen's Save (D216).</summary>
    void SetUseForGit(HostingProvider provider, bool useForGit);

    /// <summary>The repository a URL names on a provider that is on, without the network.</summary>
    HostedRepository? Recognise(string url);

    /// <summary>The push guard (ruling 14): the recognising provider's rule; true when none recognises the URL.</summary>
    bool SourceCanExist(string remoteUrl, string remoteRef);

    /// <summary>The bindings of a clone of <paramref name="url"/>, the URL after the user's <c>insteadOf</c> rules (9.3).</summary>
    IReadOnlyList<CredentialBinding> BindingsForClone(string url);

    /// <summary>Some provider's account is signed in, its token in hand and "Use for git over HTTPS" on.</summary>
    bool BindsGit { get; }

    /// <summary>
    /// A dashboard row without a tab (4.7, D332): empty unless <see cref="BindsGit"/>; else the union of
    /// the accounts' bindings of <paramref name="rewrites"/>' <see cref="UrlRewrites.Apply"/> of <paramref name="remoteUrls"/>.
    /// </summary>
    IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites);

    /// <summary>A2 with <paramref name="provider"/>'s account's token.</summary>
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct);

    /// <summary>The hosting of one repository tab; <see cref="NullRepositoryHosting.Instance"/> while all are off.</summary>
    IRepositoryHosting ForRepository(string workingDirectory);

    // 7a's GitHub-named members (ruling 1; docs/phase-7b-design.md 0.4, 1.5), kept while a caller
    // uses them; WP-N removes what is left.

    /// <summary>GitHub's provider; null while off.</summary>
    IHostingProvider? GitHub => ProviderOf(HostingProvider.GitHub);

    /// <summary>GitHub's account, signed in or needing sign-in; null without one.</summary>
    AccountStatus? Account => AccountOf(HostingProvider.GitHub);

    /// <summary>A1 on GitHub with <paramref name="token"/>.</summary>
    Task<SignInResult> SignInAsync(string token, CancellationToken ct) => SignInAsync(new SignInRequest(HostingProvider.GitHub, token), ct);

    /// <summary>Signs GitHub's account out and deletes <c>avatars/</c> (D291).</summary>
    Task<string?> SignOutAsync(CancellationToken ct) => SignOutAsync(HostingProvider.GitHub, ct);

    /// <summary>GitHub's "Use for git over HTTPS".</summary>
    void SetUseForGit(bool useForGit) => SetUseForGit(HostingProvider.GitHub, useForGit);

    /// <summary>A2 with GitHub's account's token.</summary>
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) => ListRepositoriesAsync(HostingProvider.GitHub, ct);
}

/// <summary>Where a repository's pull requests stand (docs/phase-7a-design.md 1.5, docs/phase-7b-design.md 1.5).</summary>
public enum PullRequestsState
{
    /// <summary>No remote is recognised as a provider's (7a's <c>NotGitHub</c>).</summary>
    NoHostedRemote,
    SignedOut,
    Loading,
    Failed,
    Loaded,

    /// <summary>The listed remote is Azure DevOps' in an organization the held account does not cover (D356, D366): nothing is sent.</summary>
    OtherOrganization,
}

/// <summary>A repository's hosting as the views show it (docs/phase-7a-design.md 1.5, 7.8; docs/phase-7b-design.md 1.5).</summary>
/// <param name="PullRequests">Where its pull requests stand.</param>
/// <param name="Remotes">Each remote recognised, of any provider, to its repository.</param>
/// <param name="Repository">The repository whose pull requests are listed (0.2); while <see cref="PullRequestsState.OtherOrganization"/>, the remote's.</param>
/// <param name="List">The listed provider's open pull requests, once loaded.</param>
/// <param name="Failure">Why the list failed.</param>
/// <param name="ByRef">
/// A full ref name (<c>refs/heads/x</c> through an upstream of the same name,
/// <c>refs/remotes/origin/x</c>) to its open pull request, of the listed provider.
/// </param>
public sealed record RepositoryHostingState(
    PullRequestsState PullRequests,
    IReadOnlyDictionary<string, HostedRepository> Remotes,
    HostedRepository? Repository,
    PullRequestList? List,
    HostingFailure? Failure,
    IReadOnlyDictionary<string, PullRequest> ByRef)
{
    private static readonly IReadOnlyDictionary<string, HostedRepository> NoRemotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal);

    /// <summary>A repository without a remote on a known service (7a's <c>NotGitHub</c>).</summary>
    public static RepositoryHostingState NoHostedRemote { get; } = new(
        PullRequestsState.NoHostedRemote,
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal),
        null,
        null,
        null,
        new Dictionary<string, PullRequest>(StringComparer.Ordinal));

    /// <summary>
    /// The remotes of <see cref="Repository"/>'s provider and host (on Azure DevOps of its
    /// <see cref="HostedRepository.OwnerRoot"/> too, without case): where a request can go; empty
    /// without <see cref="Repository"/> and while <see cref="PullRequestsState.OtherOrganization"/>.
    /// Computed, never stored, since the record is copied with <c>with</c> (docs/phase-7b-design.md 1.4).
    /// </summary>
    public IReadOnlyDictionary<string, HostedRepository> RequestRemotes =>
        Repository is not { } listed || PullRequests == PullRequestsState.OtherOrganization
            ? NoRemotes
            : Remotes
                .Where(remote => remote.Value.Provider == listed.Provider
                    && string.Equals(remote.Value.Host, listed.Host, StringComparison.OrdinalIgnoreCase)
                    && (listed.Provider != HostingProvider.AzureDevOps
                        || string.Equals(remote.Value.OwnerRoot, listed.OwnerRoot, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(remote => remote.Key, remote => remote.Value, StringComparer.Ordinal);
}

/// <summary>The order in which a repository's remotes are tried for the listed repository (0.2).</summary>
public static class HostingRemoteOrder
{
    /// <summary><c>origin</c>, <c>upstream</c>, then the others by ordinal name.</summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> remoteNames)
    {
        ArgumentNullException.ThrowIfNull(remoteNames);
        return [.. remoteNames
            .OrderBy(name => name switch { "origin" => 0, "upstream" => 1, _ => 2 })
            .ThenBy(name => name, StringComparer.Ordinal)];
    }
}

/// <summary>The avatars a repository's authors have on the service (D252): lower-case e-mail to the local image.</summary>
/// <param name="FileByEmail">Lower-case e-mail to a local PNG or JPEG.</param>
public sealed record AuthorAvatars(IReadOnlyDictionary<string, string> FileByEmail)
{
    /// <summary>No avatar: every author looks as before phase 7a.</summary>
    public static AuthorAvatars None { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The image of <paramref name="email"/>'s author (lower-cased, invariant), or null.</summary>
    public string? FileOf(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return FileByEmail.TryGetValue(email.ToLowerInvariant(), out var file) ? file : null;
    }
}

/// <summary>
/// The hosting of one repository tab (docs/phase-7a-design.md 1.5, 7.8): its pull requests, its
/// bindings and its authors' avatars. Its events come through the post delegate, on the UI thread.
/// </summary>
public interface IRepositoryHosting : IDisposable
{
    RepositoryHostingState State { get; }

    /// <summary>Empty unless signed in, "Use for git over HTTPS" on and the token in hand.</summary>
    IReadOnlyList<CredentialBinding> Bindings { get; }

    AuthorAvatars Avatars { get; }

    /// <summary>Signed in and not off: the tab reads <see cref="UrlRewrites"/> with its refs.</summary>
    bool WantsUrlRewrites { get; }

    /// <summary><see cref="State"/>, <see cref="Bindings"/>, <see cref="Avatars"/> or <see cref="WantsUrlRewrites"/> changed.</summary>
    event EventHandler? Changed;

    /// <summary>On the UI thread, after each refs read.</summary>
    void Update(RepoRefs refs, UrlRewrites rewrites);

    Task RefreshPullRequestsAsync(CancellationToken ct);

    /// <summary>Authors of graph commits on known remotes' branches (D311), each with its newest such commit and that remote (D339); capped lookups; GitHub remotes' authors only.</summary>
    void ShowAvatarsFor(IReadOnlyCollection<AuthorCommit> authors);

    /// <summary>A3 of the first remote on the service in <see cref="HostingRemoteOrder"/>; on the UI thread; kept per candidate.</summary>
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct);

    Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct);
}

/// <summary>
/// A hosting session with every provider off (docs/phase-7a-design.md 1.5, docs/phase-7b-design.md
/// 1.5): no account, no binding, nothing recognised, no request; every repository's hosting is
/// <see cref="NullRepositoryHosting"/>.
/// </summary>
public sealed class NullHostingSession : IHostingSession
{
    private NullHostingSession()
    {
    }

    public static NullHostingSession Instance { get; } = new();

    public bool IsOff => true;

    public IReadOnlyList<HostingProvider> Providers => [];

    public bool BindsGit => false;

    /// <summary>Never raised.</summary>
    public event EventHandler<HostingAccountChangedEventArgs>? AccountChanged
    {
        add { }
        remove { }
    }

    public bool IsOn(HostingProvider provider) => false;

    public IHostingProvider? ProviderOf(HostingProvider provider) => null;

    public AccountStatus? AccountOf(HostingProvider provider) => null;

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    /// <summary>Fails: there is no service to sign in to.</summary>
    public Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new SignInResult(null, OffFailure(request.Provider), null));
    }

    public Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct) => Task.FromResult<string?>(null);

    public void SetUseForGit(HostingProvider provider, bool useForGit)
    {
    }

    public HostedRepository? Recognise(string url) => null;

    public bool SourceCanExist(string remoteUrl, string remoteRef) => true;

    public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites) => [];

    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct) =>
        Task.FromResult(HostingResult<RepositoryList>.Fail(OffFailure(provider)));

    public IRepositoryHosting ForRepository(string workingDirectory) => NullRepositoryHosting.Instance;

    public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Task.FromResult<GitCredential?>(null);

    public void Refused(AccountKey account)
    {
    }

    public void Dispose()
    {
    }

    /// <summary>7a's "GitHub is off.", in the provider's name.</summary>
    private static HostingFailure OffFailure(HostingProvider provider) =>
        new(HostingFailureKind.Network, $"{HostingProviders.DisplayName(provider)} is off.") { Provider = provider };
}

/// <summary>
/// The hosting of a repository without a remote on a known service, and of every repository while
/// the service is off or in a dashboard's summary row (docs/phase-7a-design.md 1.5, 1.7): not
/// GitHub, no bindings, no avatars, no URL rewrites wanted, never changed; disposing it does nothing.
/// </summary>
public sealed class NullRepositoryHosting : IRepositoryHosting
{
    private static readonly HostingFailure NoRemoteFailure = new(HostingFailureKind.NotFound, "This repository has no GitHub remote.");

    private NullRepositoryHosting()
    {
    }

    public static NullRepositoryHosting Instance { get; } = new();

    public RepositoryHostingState State => RepositoryHostingState.NoHostedRemote;

    public IReadOnlyList<CredentialBinding> Bindings => [];

    public AuthorAvatars Avatars => AuthorAvatars.None;

    public bool WantsUrlRewrites => false;

    /// <summary>Never raised.</summary>
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Update(RepoRefs refs, UrlRewrites rewrites)
    {
    }

    public Task RefreshPullRequestsAsync(CancellationToken ct) => Task.CompletedTask;

    public void ShowAvatarsFor(IReadOnlyCollection<AuthorCommit> authors)
    {
    }

    public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) =>
        Task.FromResult(HostingResult<RepositoryInfo>.Fail(NoRemoteFailure));

    public Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct) =>
        Task.FromResult(HostingResult<PullRequest>.Fail(NoRemoteFailure));

    public void Dispose()
    {
    }
}
