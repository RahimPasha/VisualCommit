using System.Security.Cryptography;
using System.Text;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.Http;

namespace VisualCommit.Hosting.GitLab;

/// <summary>
/// GitLab as a hosting service (docs/phase-7b-design.md 7.4; D345, D358, rulings 11, 12, 14, 17 to
/// 20): recognition, coverage and the push guard without the network, and A1 to A5 through the
/// shared HTTP core with <see cref="GitLabProfile"/>, each with the account's token, which the
/// provider never keeps. Paths are relative to the API's address, a project's id its path escaped
/// whole (<c>%2F</c> between segments, r6 6.2) or its number. A token that is empty, holds anything
/// but visible ASCII or has another provider's form (D378) is refused without a request.
/// </summary>
public sealed class GitLabProvider : IHostingProvider, IDisposable
{
    /// <summary>A1's text for a token that is empty or blank: no request is made (7a's).</summary>
    public const string EmptyTokenText = "The token is empty.";

    /// <summary>The text of a token refused without a request for its characters or its form: GitLab's own 401 (r6 1.2).</summary>
    public const string UnauthorizedText = "401 Unauthorized";

    /// <summary>The <see cref="HostingFailure.Detail"/> prefix of a token of another provider's form, before that provider's id (D378).</summary>
    public const string OtherProviderDetail = "other-provider:";

    /// <summary>A2's cap: 10 pages of 100 projects (ruling 18).</summary>
    public const int RepositoryPages = 10;

    /// <summary>A4's cap: 3 pages of 100 merge requests (ruling 18).</summary>
    public const int RequestPages = 3;

    /// <summary>A4's lookups of a fork's project per call, at most, in list order.</summary>
    public const int MaxForkLookups = 20;

    /// <summary>The least access level that may push: Developer (30; ASSUMED, r6 3.1).</summary>
    public const int DeveloperAccess = 30;

    /// <summary>The fine-grained token's page, relative to the web base (g2 G5.3 READ): it takes no prefill.</summary>
    public const string TokenPagePath = "-/user_settings/personal_access_tokens/granular/new";

    /// <summary>The legacy token's page prefilled, relative to the web base: the server redirects it to the legacy form with the name and <c>api</c> (g2 G5.3 READ).</summary>
    public const string LegacyTokenPagePath = "-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api";

    private readonly HostingHttpClient _client;
    private readonly GitLabProfile _profile;
    private readonly IAppLog _log;
    private readonly Lock _forksGate = new();

    // Under _forksGate: each fork's project looked up by A4, by the token's fingerprint and the
    // fork's numeric id; null when GitLab answered 403 or 404 (a fork that token cannot read). Kept
    // until ForgetKept; a lookup made with one token is never shown to another.
    private readonly Dictionary<(string Fingerprint, long Id), HostedRepository?> _forks = [];

    public GitLabProvider(GitLabEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        Endpoints = endpoints;
        _log = log;
        _profile = new GitLabProfile(endpoints, clock);
        _client = new HostingHttpClient(_profile, createHandler, log, clock);
    }

    public HostingProvider Kind => HostingProvider.GitLab;

    public GitLabEndpoints Endpoints { get; }

    /// <summary><see cref="GitLabEndpoints.Host"/>.</summary>
    public string Host => Endpoints.Host;

    /// <summary><see cref="GitLabEndpoints.WebBase"/>.</summary>
    public Uri WebBase => Endpoints.WebBase;

    /// <summary>The legacy token's page, prefilled with the name VisualCommit and the <c>api</c> scope (<see cref="LegacyTokenPagePath"/>); <see cref="TokenPage"/> is the fine-grained one.</summary>
    public Uri LegacyTokenPage => new(Endpoints.WebBase.AbsoluteUri + LegacyTokenPagePath);

    /// <summary>
    /// The title A5 sends (GitLab's <c>draft_title</c>; r6 5.1 READ, the expression ASSUMED from the
    /// docs): <paramref name="title"/> unchanged unless <paramref name="draft"/>; with it, unchanged
    /// when it starts with <c>Draft:</c>, <c>[Draft]</c> or <c>(Draft)</c> in any case, else with
    /// <c>Draft: </c> before it. GitLab has no draft parameter: the title makes a request a draft.
    /// </summary>
    public static string TitleFor(string title, bool draft)
    {
        ArgumentNullException.ThrowIfNull(title);
        return !draft || IsDraftTitle(title) ? title : "Draft: " + title;
    }

    public HostedRepository? Recognise(string url) => GitLabUrls.Recognise(Endpoints, url);

    /// <summary><see cref="PullRequestRefs.SourceCanExist"/> for GitLab: <c>refs/merge-requests/&lt;x&gt;/…</c> only with an <c>&lt;x&gt;</c> of digits (ruling 14).</summary>
    public bool SourceCanExist(string remoteRef) => PullRequestRefs.SourceCanExist(Kind, remoteRef);

    /// <summary>
    /// D359's coverage: the web base's scheme, host and port, any path; a URL whose path lacks
    /// <c>.git</c> bound with its <c>.git</c> twin (ruling 12), and <c>x-access-token@</c> the
    /// account's user (ruling 11).
    /// </summary>
    public GitCoverage CoverageOf(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new GitCoverage(account, Endpoints.WebBase) { GitTwin = true, HelperUserIsAccounts = true };
    }

    /// <summary>By host, without case: a GitLab repository on the account's host, which is this provider's.</summary>
    public bool Covers(HostedRepository repository, AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(account);
        return repository.Provider == HostingProvider.GitLab
            && account.Provider == HostingProvider.GitLab
            && string.Equals(repository.Host, account.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(account.Host, Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The fine-grained token's page (<see cref="TokenPagePath"/>), which takes no prefill; <paramref name="organization"/> is ignored.</summary>
    public Uri TokenPage(string? organization) => new(Endpoints.WebBase.AbsoluteUri + TokenPagePath);

    /// <summary><c>{web base}{namespace}/{name}/-/merge_requests</c>, each segment escaped (g2 G4, MEASURED).</summary>
    public Uri PullRequestsPage(HostedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new Uri(Endpoints.WebBase.AbsoluteUri + WebPath(repository) + "/-/merge_requests");
    }

    /// <summary><c>refs/merge-requests/&lt;iid&gt;/head</c>, in the target project for a fork's request too (r6 4.2, MEASURED).</summary>
    public string? PullRequestHeadRef(PullRequest pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return PullRequestRefs.HeadOf(Kind, pull.Number);
    }

    /// <summary>
    /// A1: <c>GET user</c> for both token kinds, never <c>personal_access_tokens/self</c> (ruling 17):
    /// the key (GitLab, <see cref="Host"/>, <c>username</c>), <c>id</c>, <c>name</c> unless it is
    /// e-mail-shaped (D368's run; then null), no avatar (D349), "Use for git over HTTPS" on.
    /// <paramref name="organization"/> is ignored.
    /// </summary>
    public async Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, string? organization, CancellationToken ct)
    {
        if (Refusal(token) is { } refused)
        {
            return HostingResult<HostingAccount>.Fail(refused);
        }

        var url = _profile.Url("user");
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitLabJsonContext.Default.GitLabUserJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<HostingAccount>.Fail(failure);
        }

        if (reply.Value is not { Username: { Length: > 0 } username } user || string.IsNullOrWhiteSpace(username))
        {
            return HostingResult<HostingAccount>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
        }

        var name = user.Name is { Length: > 0 } given && !string.IsNullOrWhiteSpace(given) && HostingHttpClient.ScreenEmail(given) == given ? given : null;
        var key = new AccountKey(HostingProvider.GitLab, Endpoints.Host, username);
        return HostingResult<HostingAccount>.Ok(new HostingAccount(key, username, user.Id, name, AvatarUrl: null, UseForGit: true));
    }

    /// <summary>
    /// A2: <c>GET projects?membership=true&amp;archived=false&amp;order_by=last_activity_at&amp;sort=desc&amp;per_page=100</c>,
    /// never <c>simple</c>, up to <see cref="RepositoryPages"/> pages (ruling 18), in GitLab's order.
    /// A row whose path or addresses cannot be read is left out, with one warning naming its id.
    /// <paramref name="account"/> is not read: the list is the token's.
    /// </summary>
    public async Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, HostingAccount account, CancellationToken ct)
    {
        if (Refusal(token) is { } refused)
        {
            return HostingResult<RepositoryList>.Fail(refused);
        }

        var url = _profile.Url("projects?membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100");
        var reply = await _client.GetPagesAsync(token, url, GitLabJsonContext.Default.ListGitLabProjectJson, RepositoryPages, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<RepositoryList>.Fail(failure);
        }

        if (reply.Value is null)
        {
            return HostingResult<RepositoryList>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
        }

        var repositories = new List<RepositoryInfo>();
        foreach (var json in reply.Value)
        {
            if (Info(json, withParent: false) is { } info)
            {
                repositories.Add(info);
            }
            else
            {
                _log.Warning(FormattableString.Invariant($"GitLab listed project {json.Id} without a path or an address the app can read: it is left out of the list."));
            }
        }

        return HostingResult<RepositoryList>.Ok(new RepositoryList(repositories, reply.Next is not null, SomeHiddenBySso: false));
    }

    /// <summary>
    /// A3: <c>GET projects/&lt;id&gt;</c>, a moved project's 301 followed, named by the answer's
    /// <c>path_with_namespace</c> (7a design 19.2); a fork with <c>forked_from_project</c> as its
    /// parent and the parent's default branch.
    /// </summary>
    public async Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<RepositoryInfo>.Fail(refused);
        }

        var (json, failure, url, requestId) = await ProjectAsync(token, GitLabProfile.ProjectId(repository.FullName), ct).ConfigureAwait(false);
        if (failure is not null)
        {
            return HostingResult<RepositoryInfo>.Fail(failure);
        }

        return Info(json!, withParent: true) is { } info
            ? HostingResult<RepositoryInfo>.Ok(info)
            : HostingResult<RepositoryInfo>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, requestId));
    }

    /// <summary>
    /// A4: <c>GET projects/&lt;id&gt;/merge_requests?state=opened&amp;per_page=100</c> (<c>open</c> is a
    /// 400, r6 4.1), up to <see cref="RequestPages"/> pages, <paramref name="repository"/> as each
    /// one's base. A request's head is in the base when its <c>source_project_id</c> is its
    /// <c>target_project_id</c>, with no request; else in the fork that <c>GET projects/&lt;source id&gt;</c>
    /// names, asked once per id and kept until <see cref="ForgetKept"/>, at most
    /// <see cref="MaxForkLookups"/> a call in list order. A fork answered 403 or 404 (one the token
    /// cannot read), or past the cap, gives a null head repository, which the App takes as a deleted
    /// fork's (its head ref is still the base's, r6 4.2); any other failure fails the call.
    /// </summary>
    public async Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<PullRequestList>.Fail(refused);
        }

        var url = _profile.Url($"projects/{GitLabProfile.ProjectId(repository.FullName)}/merge_requests?state=opened&per_page=100");
        var reply = await _client.GetPagesAsync(token, url, GitLabJsonContext.Default.ListGitLabMergeRequestJson, RequestPages, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequestList>.Fail(failure);
        }

        if (reply.Value is null || reply.Value.Any(json => ReadableWebUrl(json.WebUrl) is null))
        {
            return HostingResult<PullRequestList>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
        }

        var heads = new Dictionary<long, HostedRepository?>();
        var lookups = 0;
        var fingerprint = Fingerprint(token);
        foreach (var sourceId in reply.Value.Where(json => json.SourceProjectId != json.TargetProjectId).Select(json => json.SourceProjectId).Distinct())
        {
            if (KnownFork(fingerprint, sourceId, out var known))
            {
                heads[sourceId] = known;
                continue;
            }

            if (lookups >= MaxForkLookups)
            {
                heads[sourceId] = null;
                continue;
            }

            lookups++;
            var (fork, lookupFailure, lookupUrl, lookupId) = await ProjectAsync(token, sourceId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
            if (lookupFailure is { Kind: HostingFailureKind.Forbidden or HostingFailureKind.NotFound })
            {
                heads[sourceId] = Remember(fingerprint, sourceId, null);
                continue;
            }

            if (lookupFailure is not null)
            {
                return HostingResult<PullRequestList>.Fail(lookupFailure);
            }

            if (Hosted(fork!.PathWithNamespace) is not { } head)
            {
                return HostingResult<PullRequestList>.Fail(_client.MalformedAnswer(HttpMethod.Get, lookupUrl, lookupId));
            }

            heads[sourceId] = Remember(fingerprint, sourceId, head);
        }

        var pulls = reply.Value
            .Select(json => Pull(json, repository, json.SourceProjectId == json.TargetProjectId ? repository : heads[json.SourceProjectId]))
            .ToList();
        return HostingResult<PullRequestList>.Ok(new PullRequestList(pulls, reply.Next is not null));
    }

    /// <summary>
    /// A5, never retried: <c>GET projects/&lt;id&gt;</c> of the source (<see cref="NewPullRequest.HeadRepository"/>,
    /// else <paramref name="baseRepository"/>; <see cref="NewPullRequest.HeadOwner"/> is not read)
    /// and, when it is another repository, of the base, a moved project's 301 followed; a failure
    /// there is returned and nothing is posted. Then one <c>POST projects/&lt;the source's number&gt;/merge_requests</c>
    /// (a number, so a moved source gives no 405): <c>source_branch</c>, <c>target_branch</c>,
    /// <c>title</c> by <see cref="TitleFor"/>, <c>description</c> as typed (its quick actions run,
    /// ruling 20), and across projects <c>target_project_id</c> and, for a public source,
    /// <c>allow_collaboration: true</c> (the docs' condition; 7a's <c>maintainer_can_modify</c>);
    /// never <c>draft</c> (r6 5.1). A refusal carries the <see cref="PullRequestRefusal"/> its answer stands for (<see cref="GitLabProfile"/>).
    /// </summary>
    public async Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseRepository);
        ArgumentNullException.ThrowIfNull(request);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<PullRequest>.Fail(refused);
        }

        var sourceRepository = request.HeadRepository ?? baseRepository;
        var (source, sourceFailure, sourceUrl, sourceRequestId) = await ProjectAsync(token, GitLabProfile.ProjectId(sourceRepository.FullName), ct).ConfigureAwait(false);
        if (sourceFailure is not null)
        {
            return HostingResult<PullRequest>.Fail(sourceFailure);
        }

        var target = source!;
        if (!sourceRepository.Equals(baseRepository))
        {
            var (based, baseFailure, _, _) = await ProjectAsync(token, GitLabProfile.ProjectId(baseRepository.FullName), ct).ConfigureAwait(false);
            if (baseFailure is not null)
            {
                return HostingResult<PullRequest>.Fail(baseFailure);
            }

            target = based!;
        }

        // The head is read before anything is posted: a source whose path cannot be read fails
        // the call with nothing made, as a failed GET does (7.4).
        var across = source!.Id != target.Id;
        if ((across ? Hosted(source.PathWithNamespace) : baseRepository) is not { } head)
        {
            return HostingResult<PullRequest>.Fail(_client.MalformedAnswer(HttpMethod.Get, sourceUrl, sourceRequestId));
        }

        var body = new GitLabNewMergeRequestJson
        {
            SourceBranch = request.Head,
            TargetBranch = request.Base,
            Title = TitleFor(request.Title, request.Draft),
            Description = request.Body,
            TargetProjectId = across ? target.Id : null,
            AllowCollaboration = across && source.Visibility == "public" ? true : null,
        };
        var url = _profile.Url($"projects/{source.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}/merge_requests");
        var reply = await _client.SendAsync(HttpMethod.Post, token, url, body, GitLabJsonContext.Default.GitLabMergeRequestJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequest>.Fail(failure);
        }

        return reply.Value is { } json && ReadableWebUrl(json.WebUrl) is not null
            ? HostingResult<PullRequest>.Ok(Pull(json, baseRepository, head))
            : HostingResult<PullRequest>.Fail(_client.MalformedAnswer(HttpMethod.Post, url, reply.RequestId));
    }

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Forgets the client's kept answers and A4's forks (a sign-out). An answer or a fork that a
    /// request still in flight brings is kept under its token's fingerprint, which no other token's
    /// request finds.
    /// </summary>
    internal void ForgetKept()
    {
        _client.ForgetKept();
        lock (_forksGate)
        {
            _forks.Clear();
        }
    }

    /// <summary>
    /// Why a token is refused without a request (7a's rule, <c>GitHubProvider</c>'s; D378): empty or
    /// blank (<see cref="EmptyTokenText"/>); holding anything but visible ASCII, which no header
    /// may carry (<see cref="UnauthorizedText"/>); or of GitHub's or Azure DevOps' form
    /// (<see cref="HostingProviders.TokenFormOf"/>), <see cref="UnauthorizedText"/> with the detail
    /// <c>other-provider:&lt;id&gt;</c>, so that it is disclosed to no other service. A <c>glpat-</c>
    /// token, or one of no form, may be sent: null.
    /// </summary>
    private static HostingFailure? Refusal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new HostingFailure(HostingFailureKind.BadCredentials, EmptyTokenText) { Provider = HostingProvider.GitLab };
        }

        if (!token.All(c => c is > ' ' and < '\u007F'))
        {
            return new HostingFailure(HostingFailureKind.BadCredentials, UnauthorizedText) { Provider = HostingProvider.GitLab };
        }

        return HostingProviders.TokenFormOf(token) is { } form && form != HostingProvider.GitLab
            ? new HostingFailure(HostingFailureKind.BadCredentials, UnauthorizedText, OtherProviderDetail + HostingProviders.Id(form)) { Provider = HostingProvider.GitLab }
            : null;
    }

    /// <summary>GitLab's draft prefixes (r6 5.1 READ): <c>Draft:</c>, <c>[Draft]</c> or <c>(Draft)</c> at the start, in any case.</summary>
    private static bool IsDraftTitle(string title) =>
        title.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("[Draft]", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("(Draft)", StringComparison.OrdinalIgnoreCase);

    /// <summary>A repository's path on the web, each segment escaped.</summary>
    private static string WebPath(HostedRepository repository) =>
        string.Join('/', repository.FullName.Split('/').Select(Uri.EscapeDataString));

    /// <summary>An address an answer names, when it is absolute <c>http</c> or <c>https</c> (5's As built: off Windows <c>/x</c> reads as <c>file:///x</c>); else null.</summary>
    private static Uri? ReadableWebUrl(string? text) =>
        text is { Length: > 0 }
        && (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(text, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;

    /// <summary>A3's read of one project by <paramref name="id"/> (its escaped path or its number), a 301 followed; a success without a value is malformed.</summary>
    private async Task<(GitLabProjectJson? Json, HostingFailure? Failure, Uri Url, string? RequestId)> ProjectAsync(string token, string id, CancellationToken ct)
    {
        var url = _profile.Url("projects/" + id);
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, GitLabJsonContext.Default.GitLabProjectJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return (null, failure, url, reply.RequestId);
        }

        return reply.Value is { } json
            ? (json, null, url, reply.RequestId)
            : (null, _client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId), url, reply.RequestId);
    }

    /// <summary>
    /// The key A4's forks are kept under for <paramref name="token"/>: its SHA-256 in hex, as the
    /// core keys its kept answers, so that no token is shown a lookup another token made.
    /// </summary>
    private static string Fingerprint(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private bool KnownFork(string fingerprint, long id, out HostedRepository? repository)
    {
        lock (_forksGate)
        {
            return _forks.TryGetValue((fingerprint, id), out repository);
        }
    }

    private HostedRepository? Remember(string fingerprint, long id, HostedRepository? repository)
    {
        lock (_forksGate)
        {
            _forks[(fingerprint, id)] = repository;
        }

        return repository;
    }

    private HostedRepository? Hosted(string pathWithNamespace) =>
        GitLabUrls.SplitPathWithNamespace(pathWithNamespace) is var (owner, name) ? new HostedRepository(HostingProvider.GitLab, Endpoints.Host, owner, name) : null;

    /// <summary>
    /// A project as A2 and A3 describe it: <c>default_branch</c> or <c>""</c> (an empty project);
    /// private unless <c>public</c>; a fork when <c>forked_from_project</c> is there; the time of its
    /// last activity or <see cref="DateTimeOffset.MinValue"/>; push when the higher access level is
    /// Developer or above (ASSUMED, r6 3.1), or when GitLab gives no <c>permissions</c>. Null when its
    /// path or either address cannot be read.
    /// </summary>
    private RepositoryInfo? Info(GitLabProjectJson json, bool withParent)
    {
        if (Hosted(json.PathWithNamespace) is not { } repository
            || ReadableWebUrl(json.HttpUrlToRepo) is not { } cloneUrl
            || ReadableWebUrl(json.WebUrl) is not { } webUrl)
        {
            return null;
        }

        var parent = withParent && json.ForkedFromProject is { } parentJson ? Hosted(parentJson.PathWithNamespace) : null;
        var canPush = json.Permissions is not { } permissions
            || Math.Max(permissions.ProjectAccess?.AccessLevel ?? 0, permissions.GroupAccess?.AccessLevel ?? 0) >= DeveloperAccess;
        return new RepositoryInfo(
            repository,
            json.DefaultBranch ?? string.Empty,
            json.Visibility != "public",
            json.ForkedFromProject is not null,
            json.Archived,
            json.Description,
            cloneUrl,
            webUrl,
            json.LastActivityAt ?? DateTimeOffset.MinValue,
            canPush,
            parent,
            parent is null ? null : json.ForkedFromProject!.DefaultBranch);
    }

    /// <summary>A merge request of A4 or A5 into <paramref name="baseRepository"/>, its head in <paramref name="head"/>.</summary>
    private static PullRequest Pull(GitLabMergeRequestJson json, HostedRepository baseRepository, HostedRepository? head) =>
        new(
            json.Iid,
            json.Title,
            json.Author?.Username is { Length: > 0 } author ? author : null,
            json.Draft,
            ReadableWebUrl(json.WebUrl)!,
            json.SourceBranch,
            json.Sha ?? string.Empty,
            head,
            json.TargetBranch,
            baseRepository,
            json.CreatedAt);
}
