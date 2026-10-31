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

/// <summary>
/// The app session's hosting (docs/phase-7a-design.md 1.5): the account, its token, recognition of
/// the services' URLs and the bindings of git's credentials to the account. One per app session;
/// its events come through the post delegate, on the UI thread.
/// </summary>
public interface IHostingSession : IGitCredentialSource, IDisposable
{
    /// <summary>GitHub is off: nothing is recognised and no request is made.</summary>
    bool IsOff { get; }

    /// <summary>GitHub's provider; null while off.</summary>
    IHostingProvider? GitHub { get; }

    /// <summary>The account, signed in or needing sign-in; null without one.</summary>
    AccountStatus? Account { get; }

    /// <summary>The account or its state changed.</summary>
    event EventHandler? AccountChanged;

    /// <summary>Reads <c>accounts.json</c> and the token, once (ruling 6).</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Whether a keychain is there: a cached probe that never prompts.</summary>
    Task<bool> KeychainAvailableAsync(CancellationToken ct);

    /// <summary>A1 with <paramref name="token"/>; replaces the account (D263), at once (D256).</summary>
    Task<SignInResult> SignInAsync(string token, CancellationToken ct);

    /// <summary>Signs out and deletes <c>avatars/</c> (D291): null, or why the keychain kept its entry.</summary>
    Task<string?> SignOutAsync(CancellationToken ct);

    /// <summary>"Use for git over HTTPS", at the settings screen's Save (D216).</summary>
    void SetUseForGit(bool useForGit);

    /// <summary>The repository a URL names on a known service, without the network.</summary>
    HostedRepository? Recognise(string url);

    /// <summary>The push guard (ruling 8); true when the URL is not recognised.</summary>
    bool SourceCanExist(string remoteUrl, string remoteRef);

    /// <summary>The bindings of a clone of <paramref name="url"/>, the URL after the user's <c>insteadOf</c> rules (9.3).</summary>
    IReadOnlyList<CredentialBinding> BindingsForClone(string url);

    /// <summary>Signed in, the token in hand, "Use for git over HTTPS" on, and not off.</summary>
    bool BindsGit { get; }

    /// <summary>
    /// A dashboard row without a tab (4.7, D332): empty unless <see cref="BindsGit"/>; else the
    /// bindings of <paramref name="rewrites"/>' <see cref="UrlRewrites.Apply"/> of <paramref name="remoteUrls"/>.
    /// </summary>
    IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites);

    /// <summary>A2 with the account's token.</summary>
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct);

    /// <summary>The hosting of one repository tab; <see cref="NullRepositoryHosting.Instance"/> while off.</summary>
    IRepositoryHosting ForRepository(string workingDirectory);
}

/// <summary>Where a repository's pull requests stand (docs/phase-7a-design.md 1.5).</summary>
public enum PullRequestsState
{
    NotGitHub,
    SignedOut,
    Loading,
    Failed,
    Loaded,
}

/// <summary>A repository's hosting as the views show it (docs/phase-7a-design.md 1.5, 7.8).</summary>
/// <param name="PullRequests">Where its pull requests stand.</param>
/// <param name="Remotes">Each remote recognised, to its repository.</param>
/// <param name="Repository">The repository whose pull requests are listed (0.2).</param>
/// <param name="List">The open pull requests, once loaded.</param>
/// <param name="Failure">Why the list failed.</param>
/// <param name="ByRef">
/// A full ref name (<c>refs/heads/x</c> through an upstream of the same name,
/// <c>refs/remotes/origin/x</c>) to its open pull request.
/// </param>
public sealed record RepositoryHostingState(
    PullRequestsState PullRequests,
    IReadOnlyDictionary<string, HostedRepository> Remotes,
    HostedRepository? Repository,
    PullRequestList? List,
    HostingFailure? Failure,
    IReadOnlyDictionary<string, PullRequest> ByRef)
{
    /// <summary>A repository without a remote on a known service.</summary>
    public static RepositoryHostingState NotGitHub { get; } = new(
        PullRequestsState.NotGitHub,
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal),
        null,
        null,
        null,
        new Dictionary<string, PullRequest>(StringComparer.Ordinal));
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

    /// <summary>Authors of graph commits on known remotes' branches (D311), each with its newest such commit and that remote (D339); capped lookups.</summary>
    void ShowAvatarsFor(IReadOnlyCollection<AuthorCommit> authors);

    /// <summary>A3 of the first remote on the service in <see cref="HostingRemoteOrder"/>; on the UI thread; kept per candidate.</summary>
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct);

    Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct);
}

/// <summary>
/// A hosting session with GitHub off (docs/phase-7a-design.md 1.5): no account, no binding,
/// nothing recognised, no request; every repository's hosting is <see cref="NullRepositoryHosting"/>.
/// </summary>
public sealed class NullHostingSession : IHostingSession
{
    private static readonly HostingFailure OffFailure = new(HostingFailureKind.Network, "GitHub is off.");

    private NullHostingSession()
    {
    }

    public static NullHostingSession Instance { get; } = new();

    public bool IsOff => true;

    public IHostingProvider? GitHub => null;

    public AccountStatus? Account => null;

    public bool BindsGit => false;

    /// <summary>Never raised.</summary>
    public event EventHandler? AccountChanged
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    /// <summary>Fails: there is no service to sign in to.</summary>
    public Task<SignInResult> SignInAsync(string token, CancellationToken ct) =>
        Task.FromResult(new SignInResult(null, OffFailure, null));

    public Task<string?> SignOutAsync(CancellationToken ct) => Task.FromResult<string?>(null);

    public void SetUseForGit(bool useForGit)
    {
    }

    public HostedRepository? Recognise(string url) => null;

    public bool SourceCanExist(string remoteUrl, string remoteRef) => true;

    public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites) => [];

    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) =>
        Task.FromResult(HostingResult<RepositoryList>.Fail(OffFailure));

    public IRepositoryHosting ForRepository(string workingDirectory) => NullRepositoryHosting.Instance;

    public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Task.FromResult<GitCredential?>(null);

    public void Refused(AccountKey account)
    {
    }

    public void Dispose()
    {
    }
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

    public RepositoryHostingState State => RepositoryHostingState.NotGitHub;

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
