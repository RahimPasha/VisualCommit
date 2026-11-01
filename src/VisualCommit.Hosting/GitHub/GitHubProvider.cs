using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting.GitHub;

/// <summary>
/// GitHub as a hosting service (docs/phase-7a-design.md 6.3; D252, D257, D258): recognition and
/// the push guard without the network, and A1 to A6 through <see cref="GitHubClient"/>, each with
/// the account's token, which the provider never keeps. Paths are relative to the API's address,
/// each segment escaped. An avatar is kept only on GitHub's own avatar host (github.com) or on the
/// fake's web address; any other counts as none, with one warning per provider. A token of another
/// provider's form is refused without a request (D378), and A5's refusal carries the
/// <see cref="PullRequestRefusal"/> its detail stands for (docs/phase-7b-design.md 5.4).
/// </summary>
public sealed class GitHubProvider : IHostingProvider, IDisposable
{
    /// <summary>A1's text for a token that is empty or blank: no request is made.</summary>
    public const string EmptyTokenText = "The token is empty.";

    /// <summary>The <see cref="HostingFailure.Detail"/> prefix of a token of another provider's form, before that provider's id (D378).</summary>
    public const string OtherProviderDetail = "other-provider:";

    /// <summary>A2's cap: 10 pages of 100 repositories (ruling 14).</summary>
    public const int RepositoryPages = 10;

    /// <summary>A4's cap: 3 pages of 100 pull requests (ruling 14).</summary>
    public const int PullRequestPages = 3;

    /// <summary>A6's lookups by e-mail per call, at most.</summary>
    public const int MaxAuthorLookups = 20;

    private const string AvatarHost = "avatars.githubusercontent.com";

    private readonly GitHubClient _client;
    private readonly IAppLog _log;
    private int _warnedOfAvatar;

    public GitHubProvider(GitHubEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(log);
        Endpoints = endpoints;
        _log = log;
        _client = new GitHubClient(endpoints, createHandler, log, clock);
    }

    /// <summary>GitHub's page for a new fine-grained token, relative to the web base (D295): the App's <c>TokenPage</c> text.</summary>
    public const string TokenPagePath = "settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write";

    public HostingProvider Kind => HostingProvider.GitHub;

    public GitHubEndpoints Endpoints { get; }

    /// <summary><see cref="GitHubEndpoints.Host"/>.</summary>
    public string Host => Endpoints.Host;

    /// <summary><see cref="GitHubEndpoints.WebBase"/>.</summary>
    public Uri WebBase => Endpoints.WebBase;

    public HostedRepository? Recognise(string url) => GitHubUrls.Recognise(Endpoints, url);

    public bool SourceCanExist(string remoteRef) => GitHubUrls.SourceCanExist(remoteRef);

    /// <summary>7a's rule (D278): the web base's scheme, host and port, any path.</summary>
    public GitCoverage CoverageOf(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new GitCoverage(account, Endpoints.WebBase);
    }

    /// <summary>By host: a GitHub repository on the account's host, which is this provider's.</summary>
    public bool Covers(HostedRepository repository, AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(account);
        return repository.Provider == HostingProvider.GitHub
            && account.Provider == HostingProvider.GitHub
            && string.Equals(repository.Host, Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(account.Host, Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>D295's page for a new fine-grained token (<see cref="TokenPagePath"/>); <paramref name="organization"/> is ignored.</summary>
    public Uri TokenPage(string? organization) => new(Endpoints.WebBase, TokenPagePath);

    /// <summary><c>{web base}{owner}/{name}/pulls</c>.</summary>
    public Uri PullRequestsPage(HostedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new Uri(Endpoints.WebBase, $"{repository.Owner}/{repository.Name}/pulls");
    }

    /// <summary><c>refs/pull/&lt;n&gt;/head</c>.</summary>
    public string? PullRequestHeadRef(PullRequest pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return PullRequestRefs.HeadOf(Kind, pull.Number);
    }

    /// <summary>A1 through the interface: 7a's <see cref="CheckTokenAsync(string, CancellationToken)"/>; <paramref name="organization"/> is ignored.</summary>
    public Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, string? organization, CancellationToken ct) => CheckTokenAsync(token, ct);

    /// <summary>A2 through the interface: 7a's <see cref="ListRepositoriesAsync(string, CancellationToken)"/>; <paramref name="account"/> is ignored.</summary>
    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, HostingAccount account, CancellationToken ct) => ListRepositoriesAsync(token, ct);

    /// <summary>A1: <c>GET user</c>. An empty token is refused without a request.</summary>
    public async Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, CancellationToken ct)
    {
        if (Refusal(token) is { } refused)
        {
            return HostingResult<HostingAccount>.Fail(refused);
        }

        var url = Api("user");
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitHubJsonContext.Default.GitHubUserJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<HostingAccount>.Fail(failure);
        }

        if (reply.Value is not { Login: { Length: > 0 } login } user)
        {
            return HostingResult<HostingAccount>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
        }

        var key = new AccountKey(HostingProvider.GitHub, Endpoints.Host, login);
        return HostingResult<HostingAccount>.Ok(new HostingAccount(key, login, user.Id, user.Name, AvatarOf(user.AvatarUrl), UseForGit: true));
    }

    /// <summary>A2: <c>GET user/repos?per_page=100&amp;sort=updated</c>, up to 10 pages, never <c>type</c>.</summary>
    public async Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, CancellationToken ct)
    {
        if (Refusal(token) is { } refused)
        {
            return HostingResult<RepositoryList>.Fail(refused);
        }

        var url = Api("user/repos?per_page=100&sort=updated");
        var reply = await _client.GetPagesAsync(token, url, GitHubJsonContext.Default.ListGitHubRepositoryJson, RepositoryPages, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<RepositoryList>.Fail(failure);
        }

        if (reply.Value is null)
        {
            return HostingResult<RepositoryList>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
        }

        var repositories = new List<RepositoryInfo>();
        foreach (var json in reply.Value)
        {
            if (Info(json, withParent: false) is not { } info)
            {
                return HostingResult<RepositoryList>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
            }

            repositories.Add(info);
        }

        var hidden = reply.Sso?.StartsWith("partial-results", StringComparison.OrdinalIgnoreCase) == true;
        return HostingResult<RepositoryList>.Ok(new RepositoryList(repositories, reply.MoreExist, hidden));
    }

    /// <summary>A3: <c>GET repos/{o}/{r}</c>, a fork with its parent; a repository moved to <c>repositories/&lt;id&gt;</c> is followed.</summary>
    public async Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<RepositoryInfo>.Fail(refused);
        }

        var url = Api(RepositoryPath(repository));
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitHubJsonContext.Default.GitHubRepositoryJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<RepositoryInfo>.Fail(failure);
        }

        return reply.Value is not null && Info(reply.Value, withParent: true) is { } info
            ? HostingResult<RepositoryInfo>.Ok(info)
            : HostingResult<RepositoryInfo>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
    }

    /// <summary>A4: <c>GET repos/{o}/{r}/pulls?state=open&amp;per_page=100</c>, up to 3 pages.</summary>
    public async Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<PullRequestList>.Fail(refused);
        }

        var url = Api(RepositoryPath(repository) + "/pulls?state=open&per_page=100");
        var reply = await _client.GetPagesAsync(token, url, GitHubJsonContext.Default.ListGitHubPullRequestJson, PullRequestPages, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequestList>.Fail(failure);
        }

        if (reply.Value is null)
        {
            return HostingResult<PullRequestList>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
        }

        var pulls = new List<PullRequest>();
        foreach (var json in reply.Value)
        {
            if (Pull(json, repository) is not { } pull)
            {
                return HostingResult<PullRequestList>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
            }

            pulls.Add(pull);
        }

        return HostingResult<PullRequestList>.Ok(new PullRequestList(pulls, reply.MoreExist));
    }

    /// <summary>
    /// A5: <c>POST repos/{o}/{r}/pulls</c> on the base repository, once (never retried): <c>head</c>
    /// the branch, or <c>owner:branch</c> across forks with <c>maintainer_can_modify: true</c>.
    /// A 422's detail is the client's (6.3's forms), and its <see cref="HostingFailure.Refusal"/>
    /// the reason that detail stands for (<see cref="RefusalOf"/>); its message and detail stay GitHub's.
    /// </summary>
    public async Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseRepository);
        ArgumentNullException.ThrowIfNull(request);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<PullRequest>.Fail(refused);
        }

        var crossFork = request.HeadOwner is not null;
        var body = new GitHubNewPullRequestJson
        {
            Title = request.Title,
            Body = request.Body,
            Head = crossFork ? request.HeadOwner + ":" + request.Head : request.Head,
            Base = request.Base,
            Draft = request.Draft,
            MaintainerCanModify = crossFork ? true : null,
        };
        var url = Api(RepositoryPath(baseRepository) + "/pulls");
        var reply = await _client.SendAsync(HttpMethod.Post, token, url, body, GitHubJsonContext.Default.GitHubPullRequestJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequest>.Fail(failure.Kind == HostingFailureKind.Validation ? failure with { Refusal = RefusalOf(failure.Detail) } : failure);
        }

        return reply.Value is not null && Pull(reply.Value, baseRepository) is { } pull
            ? HostingResult<PullRequest>.Ok(pull)
            : HostingResult<PullRequest>.Fail(Malformed(HttpMethod.Post, url, reply.RequestId));
    }

    /// <summary>
    /// A6 (D252): with <paramref name="branch"/>, the first page of its commits in
    /// <paramref name="repository"/>, each lower-cased author e-mail to its GitHub user (the first
    /// commit of an e-mail wins; null when GitHub links none); a 409 (an empty repository) is an
    /// empty map. Then at most 20 lookups for <paramref name="lookups"/> whose e-mail is not yet in
    /// the map, in order, each in its own repository's history from its own commit (D339):
    /// <c>repos/{owner}/{name}/commits?sha=&lt;SHA&gt;&amp;author=&lt;e-mail&gt;&amp;per_page=1</c>.
    /// A lookup answered 409, or 404 in a repository other than <paramref name="repository"/>
    /// (empty, or one GitHub does not let the token read), is left out with the rest of that
    /// repository's, asked no more in this call, and the others go on. One answered 422, 404 in
    /// <paramref name="repository"/> or a server error (GitHub lacks its commit: 500 without a body
    /// beside <c>author</c>, the live run of 2026-10-30) is left out alone and the others go on
    /// (D342, amending D339). Any other failure (the token refused or limited, the API version,
    /// no answer, an answer not read) stops them and is left out. A lookup left out is not in the
    /// map, so it is asked again at a later run.
    /// </summary>
    public async Task<HostingResult<AuthorMap>> ReadAuthorsAsync(string token, HostedRepository repository, string? branch, IReadOnlyCollection<AuthorCommit> lookups, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(lookups);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<AuthorMap>.Fail(refused);
        }

        var commits = RepositoryPath(repository) + "/commits";
        var map = new Dictionary<string, AuthorAvatar?>(StringComparer.Ordinal);
        if (branch is not null)
        {
            var url = Api($"{commits}?sha={Uri.EscapeDataString(branch)}&per_page=100");
            var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitHubJsonContext.Default.ListGitHubCommitItemJson, ct).ConfigureAwait(false);
            if (reply.Failure is { } failure)
            {
                return HostingResult<AuthorMap>.Fail(failure);
            }

            if (reply.Status == 409)
            {
                return HostingResult<AuthorMap>.Ok(new AuthorMap(map));
            }

            if (reply.Value is null)
            {
                return HostingResult<AuthorMap>.Fail(Malformed(HttpMethod.Get, url, reply.RequestId));
            }

            foreach (var item in reply.Value)
            {
                if (item.Commit?.Author?.Email is { Length: > 0 } email)
                {
                    map.TryAdd(email.ToLowerInvariant(), Author(item.Author));
                }
            }
        }

        var asked = lookups
            .Where(lookup => lookup is not null && !string.IsNullOrWhiteSpace(lookup.Email))
            .Select(lookup => lookup with { Email = lookup.Email.ToLowerInvariant() })
            .DistinctBy(lookup => lookup.Email, StringComparer.Ordinal)
            .Where(lookup => !map.ContainsKey(lookup.Email))
            .Take(MaxAuthorLookups)
            .ToList();
        var unreadable = new HashSet<HostedRepository>();
        foreach (var lookup in asked)
        {
            if (unreadable.Contains(lookup.Repository))
            {
                continue;
            }

            var url = Api($"{RepositoryPath(lookup.Repository)}/commits?sha={Uri.EscapeDataString(lookup.Sha)}&author={Uri.EscapeDataString(lookup.Email)}&per_page=1");
            var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitHubJsonContext.Default.ListGitHubCommitItemJson, ct).ConfigureAwait(false);
            var kind = reply.Failure?.Kind;
            if (reply.Status == 409 || (kind is HostingFailureKind.NotFound && !repository.Equals(lookup.Repository)))
            {
                // An empty repository, or another remote's that is gone or private to others: none of
                // its addresses is found there, so its other lookups are not asked in this call; the
                // rest go on.
                unreadable.Add(lookup.Repository);
                continue;
            }

            if (kind is HostingFailureKind.Validation or HostingFailureKind.ServerError or HostingFailureKind.NotFound)
            {
                // A commit GitHub does not have (a remote-tracking branch GitHub has since lost or
                // rewritten, a remote pointed elsewhere): github.com answers such a lookup 500 without
                // a body (the live run of 2026-10-30), and a 422 is taken the same, in any repository.
                // A 404 in the listed repository (the tab's own: A3's, or for a fork the parent A3
                // names) is taken as this commit's, not the repository's (not measured). Only this
                // address is not found, and it is asked again at a later run (D342).
                continue;
            }

            if (!reply.Succeeded || reply.Value is null)
            {
                break;
            }

            map[lookup.Email] = reply.Value is [var first, ..] ? Author(first.Author) : null;
        }

        return HostingResult<AuthorMap>.Ok(new AuthorMap(map));
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Forgets the client's kept answers (a sign-out, 7.5). An answer a request still in flight
    /// brings is kept under its token's fingerprint, which no other token's request finds.
    /// </summary>
    internal void ForgetKept() => _client.ForgetKept();

    /// <summary><c>repos/{owner}/{name}</c>, each segment escaped.</summary>
    private static string RepositoryPath(HostedRepository repository) =>
        $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}";

    /// <summary>
    /// Why a token is refused without a request: empty or blank (<see cref="EmptyTokenText"/>);
    /// holding anything but visible ASCII, which no GitHub token does and no header may carry
    /// (GitHub's own "Bad credentials"); or of GitLab's or Azure DevOps' form
    /// (<see cref="HostingProviders.TokenFormOf"/>; D378, amending D298), "Bad credentials" with the
    /// detail <c>other-provider:&lt;id&gt;</c>, so that it is disclosed to no other service. A token
    /// of GitHub's form, or of none, may be sent: null.
    /// </summary>
    private static HostingFailure? Refusal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new HostingFailure(HostingFailureKind.BadCredentials, EmptyTokenText);
        }

        if (!token.All(c => c is > ' ' and < '\u007F'))
        {
            return new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials");
        }

        return HostingProviders.TokenFormOf(token) is { } form && form != HostingProvider.GitHub
            ? new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials", OtherProviderDetail + HostingProviders.Id(form))
            : null;
    }

    /// <summary>
    /// The reason a 422's detail (<see cref="GitHubClient"/>'s forms) stands for, as the create
    /// form matched it in 7a: starting "A pull request already exists"
    /// <see cref="PullRequestRefusal.AlreadyExists"/>, starting "No commits between"
    /// <see cref="PullRequestRefusal.NoCommits"/>, "head is invalid"
    /// <see cref="PullRequestRefusal.HeadMissing"/>, "base is invalid"
    /// <see cref="PullRequestRefusal.BaseMissing"/>; else <see cref="PullRequestRefusal.None"/>.
    /// GitHub never gives <see cref="PullRequestRefusal.BranchesMissing"/>.
    /// </summary>
    internal static PullRequestRefusal RefusalOf(string? detail) => detail switch
    {
        null => PullRequestRefusal.None,
        _ when detail.StartsWith("A pull request already exists", StringComparison.Ordinal) => PullRequestRefusal.AlreadyExists,
        _ when detail.StartsWith("No commits between", StringComparison.Ordinal) => PullRequestRefusal.NoCommits,
        "head is invalid" => PullRequestRefusal.HeadMissing,
        "base is invalid" => PullRequestRefusal.BaseMissing,
        _ => PullRequestRefusal.None,
    };

    private Uri Api(string relative) => new(Endpoints.ApiBase, relative);

    /// <summary>A repository as A2 and A3 describe it; null when a field the app needs is missing or unreadable.</summary>
    private RepositoryInfo? Info(GitHubRepositoryJson json, bool withParent)
    {
        if (json.DefaultBranch is not { Length: > 0 } defaultBranch
            || !Uri.TryCreate(json.CloneUrl, UriKind.Absolute, out var cloneUrl)
            || !Uri.TryCreate(json.HtmlUrl, UriKind.Absolute, out var htmlUrl)
            || json.UpdatedAt is not { } updatedAt)
        {
            return null;
        }

        var parent = withParent && json.Parent is { } parentJson ? Hosted(parentJson) : null;
        return new RepositoryInfo(
            Hosted(json),
            defaultBranch,
            json.Private,
            json.Fork,
            json.Archived,
            json.Description,
            cloneUrl,
            htmlUrl,
            updatedAt,
            json.Permissions?.Push ?? false,
            parent,
            parent is null ? null : json.Parent!.DefaultBranch);
    }

    /// <summary>A pull request of A4 or A5 on <paramref name="baseRepository"/> (its <c>base.repo</c> when given); null when it cannot be read.</summary>
    private PullRequest? Pull(GitHubPullRequestJson json, HostedRepository baseRepository)
    {
        if (!Uri.TryCreate(json.HtmlUrl, UriKind.Absolute, out var htmlUrl))
        {
            return null;
        }

        return new PullRequest(
            json.Number,
            json.Title,
            json.User?.Login is { Length: > 0 } author ? author : null,
            json.Draft,
            htmlUrl,
            json.Head.Ref,
            json.Head.Sha,
            json.Head.Repo is { } head ? Hosted(head) : null,
            json.Base.Ref,
            json.Base.Repo is { } @base ? Hosted(@base) : baseRepository,
            json.CreatedAt);
    }

    private HostedRepository Hosted(GitHubRepositoryJson json) => new(HostingProvider.GitHub, Endpoints.Host, json.Owner.Login, json.Name);

    /// <summary>A6's user of a commit: null when GitHub links none (null or <c>{}</c>), or when its avatar is not on GitHub's own host.</summary>
    private AuthorAvatar? Author(GitHubUserJson? user) =>
        user is { Login: { Length: > 0 } login } && AvatarOf(user.AvatarUrl) is { } avatar
            ? new AuthorAvatar(user.Id, login, avatar)
            : null;

    /// <summary>
    /// The avatar's address when it is <c>https</c> on <c>avatars.githubusercontent.com</c>
    /// (github.com) or on the web address's scheme, host and port (the fake, D252, D286); else null,
    /// with one warning per provider that names the host only (a host without an IDN form is on
    /// neither and named <see cref="Http.HostingHttpClient.UnreadableHost"/>, D380).
    /// </summary>
    private Uri? AvatarOf(string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return null;
        }

        if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out var url)
            && (GitHubUrls.IsGitHubCom(Endpoints)
                ? url.Scheme == Uri.UriSchemeHttps && string.Equals(Http.HostingHttpClient.IdnHostOf(url), AvatarHost, StringComparison.OrdinalIgnoreCase) && url.Port == 443
                : GitHubUrls.SameOrigin(url, Endpoints.WebBase)))
        {
            return url;
        }

        if (Interlocked.Exchange(ref _warnedOfAvatar, 1) == 0)
        {
            var where = url is null ? "an address that could not be read" : GitHubUrls.OriginOf(url);
            _log.Warning($"GitHub named an avatar on {where}, which is not GitHub's avatar host: such authors are shown without one.");
        }

        return null;
    }

    /// <summary>An answer that succeeded but lacks what the app needs: the core's <see cref="Http.HostingHttpClient.MalformedAnswer"/>, its text and its warning.</summary>
    private HostingFailure Malformed(HttpMethod method, Uri url, string? requestId) => _client.MalformedAnswer(method, url, requestId);
}
