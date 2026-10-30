using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting;

/// <summary>
/// A repository on a hosting service (docs/phase-7a-design.md 1.4). Two are equal when their
/// provider is and their host, owner and name are, ignoring case, as GitHub compares them.
/// </summary>
/// <param name="Provider">The hosting service.</param>
/// <param name="Host">Its host, as <see cref="GitHubEndpoints.Host"/>.</param>
/// <param name="Owner">The owner's login: a user or an organisation.</param>
/// <param name="Name">The repository's name.</param>
public sealed record HostedRepository(HostingProvider Provider, string Host, string Owner, string Name)
{
    /// <summary><c>owner/name</c>.</summary>
    public string FullName => $"{Owner}/{Name}";

    public bool Equals(HostedRepository? other) =>
        other is not null
        && Provider == other.Provider
        && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Owner, other.Owner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => HashCode.Combine(
        Provider,
        StringComparer.OrdinalIgnoreCase.GetHashCode(Host),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Owner),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
}

/// <summary>A signed-in account (docs/phase-7a-design.md 1.4): never its token.</summary>
/// <param name="Key">The account's key, in lower case.</param>
/// <param name="Login">The login in its own case.</param>
/// <param name="Id">The user's id on the service.</param>
/// <param name="Name">The user's name, when they gave one.</param>
/// <param name="AvatarUrl">The user's avatar, when the service names one.</param>
/// <param name="UseForGit">"Use for git over HTTPS" (D255).</param>
public sealed record HostingAccount(AccountKey Key, string Login, long Id, string? Name, Uri? AvatarUrl, bool UseForGit);

/// <summary>One repository as the service describes it (A2, A3).</summary>
/// <param name="Parent">For a fork, the repository it was forked from.</param>
/// <param name="ParentDefaultBranch">For a fork, its parent's default branch.</param>
public sealed record RepositoryInfo(
    HostedRepository Repository,
    string DefaultBranch,
    bool IsPrivate,
    bool IsFork,
    bool IsArchived,
    string? Description,
    Uri CloneUrl,
    Uri HtmlUrl,
    DateTimeOffset UpdatedAt,
    bool CanPush,
    HostedRepository? Parent,
    string? ParentDefaultBranch);

/// <summary>The account's repositories (A2).</summary>
/// <param name="Repositories">The repositories listed.</param>
/// <param name="MoreExist">The cap was reached (1,000 repositories).</param>
/// <param name="SomeHiddenBySso">Some were left out because an organisation requires single sign-on.</param>
public sealed record RepositoryList(IReadOnlyList<RepositoryInfo> Repositories, bool MoreExist, bool SomeHiddenBySso);

/// <summary>An open pull request (A4, A5).</summary>
/// <param name="HeadRepository">The repository its head is in; null when the fork was deleted.</param>
public sealed record PullRequest(
    int Number,
    string Title,
    string? Author,
    bool IsDraft,
    Uri HtmlUrl,
    string HeadRef,
    string HeadSha,
    HostedRepository? HeadRepository,
    string BaseRef,
    HostedRepository BaseRepository,
    DateTimeOffset CreatedAt)
{
    /// <summary>Its head is in another repository than its base, or in a fork that was deleted.</summary>
    public bool IsFromFork => HeadRepository is null || !HeadRepository.Equals(BaseRepository);
}

/// <summary>A repository's open pull requests (A4).</summary>
/// <param name="PullRequests">The pull requests listed.</param>
/// <param name="MoreExist">The cap was reached (300 pull requests, ruling 14).</param>
public sealed record PullRequestList(IReadOnlyList<PullRequest> PullRequests, bool MoreExist);

/// <summary>A pull request to create (A5).</summary>
/// <param name="HeadOwner">The head's owner, only across forks.</param>
public sealed record NewPullRequest(string Title, string Body, string Head, string? HeadOwner, string Base, bool Draft);

/// <summary>A commit author's account on the service (A6).</summary>
public sealed record AuthorAvatar(long UserId, string Login, Uri AvatarUrl);

/// <summary>Commit authors by e-mail (A6).</summary>
/// <param name="ByEmail">Lower-case e-mail to its account; null when the service does not know it.</param>
public sealed record AuthorMap(IReadOnlyDictionary<string, AuthorAvatar?> ByEmail);

/// <summary>
/// A commit author to look up (A6; D311, D339): the e-mail, with the newest of its commits that a
/// remote-tracking branch of a remote on the service reaches and that remote's repository. The
/// lookup searches that repository's history from that commit, so an author whose commits are only
/// on a feature branch, or only on a fork, is found.
/// </summary>
/// <param name="Email">The author's e-mail, lower-cased.</param>
/// <param name="Sha">The newest commit of the author's that <paramref name="Repository"/>'s branch reaches.</param>
/// <param name="Repository">The repository of the remote whose branch reaches it.</param>
public sealed record AuthorCommit(string Email, string Sha, HostedRepository Repository);

/// <summary>Why a request to the hosting service failed (docs/phase-7a-design.md 1.4, 6.2).</summary>
public enum HostingFailureKind
{
    Network,
    BadCredentials,
    SsoRequired,
    RateLimited,
    Forbidden,
    NotFound,
    ApiVersion,
    Validation,
    ServerError,
    Malformed,
}

/// <summary>A failed request (docs/phase-7a-design.md 1.4).</summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="Message">The service's own words (ruling 15), or for a network failure or time-out the client's fixed texts (D284).</param>
/// <param name="Detail">A 422's form (6.3, A5) or <c>X-Accepted-GitHub-Permissions</c>.</param>
/// <param name="RetryAt">When a rate limit ends.</param>
/// <param name="Url">The address the failure names.</param>
/// <param name="RequestId"><c>X-GitHub-Request-Id</c>, for the log.</param>
public sealed record HostingFailure(
    HostingFailureKind Kind,
    string Message,
    string? Detail = null,
    DateTimeOffset? RetryAt = null,
    Uri? Url = null,
    string? RequestId = null);

/// <summary>The result of a request: its value, or why it failed.</summary>
public sealed record HostingResult<T>(T? Value, HostingFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static HostingResult<T> Ok(T value) => new(value, null);

    public static HostingResult<T> Fail(HostingFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(default, failure);
    }
}
