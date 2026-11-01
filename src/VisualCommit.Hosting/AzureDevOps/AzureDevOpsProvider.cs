using System.Globalization;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.Http;

namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// Azure DevOps as a hosting service (docs/phase-7b-design.md 6; D350, D359, D360, D361): recognition,
/// the push guard and the coverage without the network, and A1 to A5 through
/// <see cref="AzureDevOpsClient"/>, each with the account's token, which the provider never keeps.
/// Every request is on the web base's origin and inside one organization: A1's two reads in the
/// typed one, A2's list in the account's, and A3 to A5 below the repository's own API path
/// (<see cref="AzureDevOpsUrls.ApiPath"/>), built from names, never an organization-wide route
/// (D352's guard, 15.4). A token of another provider's form, an empty one, or one of more than
/// visible ASCII is refused without a request (D378), as is a repository that is not this
/// service's or not <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>. The failures it makes itself name
/// Azure DevOps; the core's name it too.
/// </summary>
public sealed class AzureDevOpsProvider : IHostingProvider, IDisposable
{
    /// <summary><see cref="HostingFailure.Detail"/> of A1's refusal of the Anonymous identity (g1 G0.3): the token was not taken as a sign-in.</summary>
    public const string AnonymousDetail = "anonymous";

    /// <summary>The <see cref="HostingFailure.Detail"/> prefix of a token of another provider's form, before that provider's id (D378).</summary>
    public const string OtherProviderDetail = GitHubProvider.OtherProviderDetail;

    /// <summary>A token that is empty or blank: no request is made.</summary>
    public const string EmptyTokenText = "The token is empty.";

    /// <summary>A token with characters beyond visible ASCII, which no Azure DevOps token has and no header may carry: no request is made.</summary>
    public const string TokenCharactersText = "The token holds characters that no token has.";

    /// <summary>A token of GitHub's or GitLab's form (D378): no request is made.</summary>
    public const string OtherProviderText = "The token is another provider's.";

    /// <summary>A1's organization that <see cref="OrganizationOf"/> does not take: no request is made.</summary>
    public const string NotAnOrganizationText = "That is not the name or the address of an Azure DevOps organization.";

    /// <summary>A1's refusal of the Anonymous identity's answer.</summary>
    public const string AnonymousText = "Azure DevOps answered as it does for a visitor.";

    /// <summary>A2 to A5's account or repository that is not this service's, or not <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>: no request is made.</summary>
    public const string NotThisServiceText = "That is not a repository or an account of this Azure DevOps.";

    /// <summary>A5 across organizations, which Azure DevOps' forks are not (r5 3.4): no request is made.</summary>
    public const string OtherOrganizationText = "A pull request's head must be in its base's organization.";

    /// <summary>A4's cap: 3 pages of 100 pull requests (7a's 300).</summary>
    public const int PullRequestPages = 3;

    private const string HeadsPrefix = "refs/heads/";

    private readonly AzureDevOpsClient _client;
    private readonly IAppLog _log;

    public AzureDevOpsProvider(AzureDevOpsEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(log);
        Endpoints = endpoints;
        _log = log;
        _client = new AzureDevOpsClient(endpoints, createHandler, log, clock);
    }

    public HostingProvider Kind => HostingProvider.AzureDevOps;

    public AzureDevOpsEndpoints Endpoints { get; }

    /// <summary><see cref="AzureDevOpsEndpoints.Host"/>: <c>dev.azure.com</c> for both of the service's host families (r5 6.2).</summary>
    public string Host => Endpoints.Host;

    /// <summary><see cref="AzureDevOpsEndpoints.WebBase"/>: web, git and the API (D285).</summary>
    public Uri WebBase => Endpoints.WebBase;

    /// <summary>
    /// The organization <paramref name="text"/> names, a name or a URL on the web base or
    /// <c>&lt;organization&gt;.visualstudio.com</c>; null when it is not usable (ruling 16, WP-A's box;
    /// <see cref="AzureDevOpsUrls.OrganizationOf"/>).
    /// </summary>
    public static string? OrganizationOf(string? text, AzureDevOpsEndpoints endpoints) => AzureDevOpsUrls.OrganizationOf(text, endpoints);

    public HostedRepository? Recognise(string url) => AzureDevOpsUrls.Recognise(Endpoints, url);

    /// <summary><see cref="PullRequestRefs.SourceCanExist"/> for Azure DevOps (ruling 14).</summary>
    public bool SourceCanExist(string remoteRef) => PullRequestRefs.SourceCanExist(Kind, remoteRef);

    /// <summary>
    /// D359: the web base, the account's organization as the first path segment, with the service's
    /// <c>https://&lt;organization&gt;.visualstudio.com</c>, and <c>x-access-token@</c> as the account's user.
    /// </summary>
    public GitCoverage CoverageOf(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new GitCoverage(account, WebBase)
        {
            Organization = account.Login,
            OrganizationHost = Endpoints.OrganizationHosts,
            HelperUserIsAccounts = true,
        };
    }

    /// <summary>D350: an Azure DevOps repository on this host whose organization is the account's, without case.</summary>
    public bool Covers(HostedRepository repository, AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(account);
        return repository.Provider == HostingProvider.AzureDevOps
            && IsOurs(account)
            && string.Equals(repository.Host, Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(repository.OwnerRoot, account.Login, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>{web base}&lt;org&gt;/_usersSettings/tokens</c> (MEASURED r5 3.2), the organization by <see cref="OrganizationOf"/>.</summary>
    /// <exception cref="ArgumentException"><see cref="OrganizationOf"/> takes no organization from <paramref name="organization"/>.</exception>
    public Uri TokenPage(string? organization)
    {
        var name = OrganizationOf(organization, Endpoints) ?? throw new ArgumentException("Not an Azure DevOps organization.", nameof(organization));
        return new Uri(WebBase, AzureDevOpsUrls.Escape(name) + "/_usersSettings/tokens");
    }

    /// <summary><c>{web URL}/pullrequests</c> (any path answers 200, g1 G4.3).</summary>
    /// <exception cref="ArgumentException">The repository is not <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>.</exception>
    public Uri PullRequestsPage(HostedRepository repository) => new(AzureDevOpsUrls.WebUrlOf(Endpoints, repository).AbsoluteUri + "/pullrequests");

    /// <summary>None (D361): an Azure DevOps request is checked out through a remote's branch alone.</summary>
    public string? PullRequestHeadRef(PullRequest pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return PullRequestRefs.HeadOf(Kind, pull.Number);
    }

    /// <summary>
    /// A1 (6.4, D360): with the token, <c>GET &lt;org&gt;/_apis/connectionData</c> at the preview
    /// version; a 404 is <see cref="HostingFailureKind.NotFound"/> (no such organization); a 200
    /// naming the Anonymous identity is <see cref="HostingFailureKind.BadCredentials"/> with
    /// <see cref="AnonymousDetail"/>. Only then the organization's core resource area, whose
    /// <c>locationUrl</c> gives the organization's spelling; when it gives none, the typed name, with
    /// one warning. The account: the organization as its login, id 0, no avatar, the user's GUID,
    /// and the first display name that is neither an address nor the sign-in name.
    /// </summary>
    public async Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, string? organization, CancellationToken ct)
    {
        if (Refusal(token) is { } refused)
        {
            return HostingResult<HostingAccount>.Fail(refused);
        }

        if (OrganizationOf(organization, Endpoints) is not { } typed)
        {
            return HostingResult<HostingAccount>.Fail(Own(HostingFailureKind.Validation, NotAnOrganizationText));
        }

        var url = new Uri(Endpoints.ApiOf(typed), "_apis/connectionData?api-version=" + AzureDevOpsApi.ConnectionDataVersion);
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<HostingAccount>.Fail(failure);
        }

        if (MemberOf(reply.Value?.AuthenticatedUser) is not { } member)
        {
            return HostingResult<HostingAccount>.Fail(_client.Failed(HttpMethod.Get, url, new HostingFailure(HostingFailureKind.BadCredentials, AnonymousText, AnonymousDetail), reply.RequestId));
        }

        var spelling = await SpellingAsync(token, typed, ct).ConfigureAwait(false);
        var key = new AccountKey(HostingProvider.AzureDevOps, Endpoints.Host, spelling);
        return HostingResult<HostingAccount>.Ok(new HostingAccount(key, spelling, 0, NameOf(member.Identity), null, UseForGit: true, member.Id.ToString("D", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// A2 (6.5): <c>GET &lt;org&gt;/_apis/git/repositories</c>, or <c>&lt;org&gt;/&lt;ListProject&gt;/…</c> when the
    /// endpoints name one (D352), unpaged (MEASURED r5 2.1), never <c>includeLinks</c>; by name, then
    /// project, without case. A disabled repository (it cannot be cloned) and one without readable
    /// addresses or names are left out.
    /// </summary>
    public async Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, HostingAccount account, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (Refusal(token) is { } refused)
        {
            return HostingResult<RepositoryList>.Fail(refused);
        }

        var organization = account.Login;
        if (!IsOurs(account.Key) || !AzureDevOpsUrls.IsOrganizationName(organization) || (Endpoints.ListProject is { } listed && !AzureDevOpsUrls.IsName(listed)))
        {
            return HostingResult<RepositoryList>.Fail(Own(HostingFailureKind.Validation, NotThisServiceText));
        }

        var path = AzureDevOpsUrls.Escape(organization) + "/" + (Endpoints.ListProject is { } project ? AzureDevOpsUrls.Escape(project) + "/" : string.Empty);
        var url = new Uri(WebBase, path + "_apis/git/repositories?api-version=" + AzureDevOpsApi.Version);
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, AzureDevOpsJsonContext.Default.RepositoryList, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<RepositoryList>.Fail(failure);
        }

        // A null row parses (the elements of a list are not held to their annotations): the answer
        // is malformed, as one whose row lacks a member it must have, and nothing throws (D380).
        if (reply.Value!.Value.Exists(json => json is null))
        {
            return HostingResult<RepositoryList>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
        }

        var repositories = reply.Value.Value
            .Where(json => !json.IsDisabled)
            .Select(json => Info(json, organization))
            .OfType<RepositoryInfo>()
            .OrderBy(info => info.Repository.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.Repository.Owner, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return HostingResult<RepositoryList>.Ok(new RepositoryList(repositories, MoreExist: false, SomeHiddenBySso: false));
    }

    /// <summary>A3 (6.5): <c>GET</c> the repository by name; a fork's parent read by a second <c>GET</c> gives its default branch, null when that fails.</summary>
    public async Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if ((Refusal(token) ?? Invalid(repository)) is { } refused)
        {
            return HostingResult<RepositoryInfo>.Fail(refused);
        }

        var url = AzureDevOpsUrls.ApiPath(Endpoints, repository, "?api-version=" + AzureDevOpsApi.Version);
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, AzureDevOpsJsonContext.Default.AzureDevOpsRepositoryJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<RepositoryInfo>.Fail(failure);
        }

        if (Info(reply.Value!, repository.OwnerRoot) is not { } info)
        {
            return HostingResult<RepositoryInfo>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
        }

        if (info.Parent is { } parent)
        {
            var parentReply = await _client.SendAsync(HttpMethod.Get, token, AzureDevOpsUrls.ApiPath(Endpoints, parent, "?api-version=" + AzureDevOpsApi.Version), null, AzureDevOpsJsonContext.Default.AzureDevOpsRepositoryJson, ct).ConfigureAwait(false);
            if (parentReply.Value?.DefaultBranch is { } parentBranch)
            {
                info = info with { ParentDefaultBranch = Branch(parentBranch) };
            }
        }

        return HostingResult<RepositoryInfo>.Ok(info);
    }

    /// <summary>
    /// A4 (6.6): the active pull requests below the repository's API path, walked by <c>$top</c> and
    /// <c>$skip</c>, 3 pages of 100. The web address is built (the answer has none, MEASURED r5 3.2);
    /// the author is <c>createdBy.displayName</c>, null when it is an address, never <c>uniqueName</c>;
    /// a fork's head is <c>forkSource</c>'s (READ g1 G2.6). No checkout ref (D361).
    /// </summary>
    public async Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if ((Refusal(token) ?? Invalid(repository)) is { } refused)
        {
            return HostingResult<PullRequestList>.Fail(refused);
        }

        var url = AzureDevOpsUrls.ApiPath(Endpoints, repository, "/pullrequests?searchCriteria.status=active&api-version=" + AzureDevOpsApi.Version);
        var reply = await _client.GetPagesAsync(token, url, AzureDevOpsJsonContext.Default.PullRequestList, IdOf, PullRequestPages, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequestList>.Fail(failure);
        }

        var pulls = new List<PullRequest>();
        foreach (var json in reply.Value!)
        {
            if (Pull(json, repository) is not { } pull)
            {
                return HostingResult<PullRequestList>.Fail(_client.MalformedAnswer(HttpMethod.Get, url, reply.RequestId));
            }

            pulls.Add(pull);
        }

        return HostingResult<PullRequestList>.Ok(new PullRequestList(pulls, reply.MoreExist));
    }

    /// <summary>
    /// A5 (6.6): <c>POST</c> below the base repository's API path, once (never retried): both refs as
    /// <c>refs/heads/…</c>, the title, the description and the draft flag. A head repository other
    /// than the base is read first (one <c>GET</c>) for its id, sent as <c>forkSource.repository.id</c>;
    /// one of another organization is refused without a request. 200 and 201 succeed (g1 G0.12). A
    /// refusal's <see cref="HostingFailure.Refusal"/> is the client's, by <c>typeKey</c>.
    /// </summary>
    public async Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseRepository);
        ArgumentNullException.ThrowIfNull(request);
        if ((Refusal(token) ?? Invalid(baseRepository)) is { } refused)
        {
            return HostingResult<PullRequest>.Fail(refused);
        }

        string? forkId = null;
        if (request.HeadRepository is { } head && !head.Equals(baseRepository))
        {
            if (Invalid(head) is { } invalid)
            {
                return HostingResult<PullRequest>.Fail(invalid);
            }

            if (!string.Equals(head.OwnerRoot, baseRepository.OwnerRoot, StringComparison.OrdinalIgnoreCase))
            {
                return HostingResult<PullRequest>.Fail(Own(HostingFailureKind.Validation, OtherOrganizationText));
            }

            var headReply = await _client.SendAsync(HttpMethod.Get, token, AzureDevOpsUrls.ApiPath(Endpoints, head, "?api-version=" + AzureDevOpsApi.Version), null, AzureDevOpsJsonContext.Default.AzureDevOpsRepositoryJson, ct).ConfigureAwait(false);
            if (headReply.Failure is { } headFailure)
            {
                return HostingResult<PullRequest>.Fail(headFailure);
            }

            forkId = headReply.Value!.Id;
        }

        var body = new AzureDevOpsNewPullRequestJson
        {
            SourceRefName = HeadsPrefix + request.Head,
            TargetRefName = HeadsPrefix + request.Base,
            Title = request.Title,
            Description = request.Body,
            IsDraft = request.Draft,
            ForkSource = forkId is null ? null : new AzureDevOpsNewForkSourceJson { Repository = new AzureDevOpsIdJson { Id = forkId } },
        };
        var url = AzureDevOpsUrls.ApiPath(Endpoints, baseRepository, "/pullrequests?api-version=" + AzureDevOpsApi.Version);
        var reply = await _client.SendAsync(HttpMethod.Post, token, url, body, AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson, ct).ConfigureAwait(false);
        if (reply.Failure is { } failure)
        {
            return HostingResult<PullRequest>.Fail(failure);
        }

        return Pull(reply.Value!, baseRepository) is { } pull
            ? HostingResult<PullRequest>.Ok(pull)
            : HostingResult<PullRequest>.Fail(_client.MalformedAnswer(HttpMethod.Post, url, reply.RequestId));
    }

    public void Dispose() => _client.Dispose();

    /// <summary>Forgets the client's kept answers: WP-S calls it at this provider's sign-out.</summary>
    internal void ForgetKept() => _client.ForgetKept();

    /// <summary>
    /// The member a <c>connectionData</c>'s <c>authenticatedUser</c> names; null for the Anonymous
    /// identity, which a token the service ignored gets (g1 G0.3): no user or no GUID <c>id</c>, the
    /// Anonymous id, a descriptor starting <c>System:PublicAccess;</c> (without case) whatever its id,
    /// or <c>properties.Account.$value</c> <c>Anonymous</c> (MEASURED r5 1.2); never by <c>metaTypeId</c> (g1 G0.4).
    /// </summary>
    private static (AzureDevOpsIdentityJson Identity, Guid Id)? MemberOf(AzureDevOpsIdentityJson? user)
    {
        if (user is null || !Guid.TryParse(user.Id, out var id) || id == Guid.Parse(AzureDevOpsClient.AnonymousId))
        {
            return null;
        }

        return user.Descriptor?.StartsWith(AzureDevOpsClient.AnonymousDescriptor, StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(user.Properties?.Account?.Value, "Anonymous", StringComparison.OrdinalIgnoreCase)
            ? null
            : (user, id);
    }

    /// <summary>
    /// The account's name (6.4): the first of <c>customDisplayName</c> and <c>providerDisplayName</c>
    /// that is neither e-mail-shaped nor the sign-in name (<c>Account.$value</c>, an address, which
    /// serves this comparison alone and is never kept or logged, D346); else null.
    /// </summary>
    private static string? NameOf(AzureDevOpsIdentityJson user)
    {
        var signIn = user.Properties?.Account?.Value;
        return new[] { user.CustomDisplayName, user.ProviderDisplayName }
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) && !IsEmailShaped(name) && !string.Equals(name, signIn, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>D368's test: an <c>@</c> (or a full-width one, D382) with a non-blank character on each side, anywhere in the text.</summary>
    private static bool IsEmailShaped(string text)
    {
        for (var i = 1; i < text.Length - 1; i++)
        {
            if (text[i] is '@' or HostingHttpClient.FullWidthAt && !char.IsWhiteSpace(text[i - 1]) && !char.IsWhiteSpace(text[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The organization's spelling (6.4 step 2): its core resource area's <c>locationUrl</c>, on the
    /// web base's origin with one segment equal to the typed name without case. Any other answer or
    /// failure keeps the typed name with one warning (the core's for a failure); the caller's
    /// cancellation throws.
    /// </summary>
    private async Task<string> SpellingAsync(string token, string typed, CancellationToken ct)
    {
        var url = new Uri(Endpoints.ApiOf(typed), $"_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?api-version={AzureDevOpsApi.ConnectionDataVersion}");
        var reply = await _client.SendAsync(HttpMethod.Get, token, url, null, AzureDevOpsJsonContext.Default.AzureDevOpsResourceAreaJson, ct).ConfigureAwait(false);
        if (reply.Failure is not null)
        {
            return typed;
        }

        if (WebAddress(reply.Value?.LocationUrl) is { } location
            && HostingHttpClient.SameOrigin(location, WebBase)
            && location.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var named]
            && AzureDevOpsUrls.IsOrganizationName(named)
            && string.Equals(named, typed, StringComparison.OrdinalIgnoreCase))
        {
            return named;
        }

        _log.Warning("Azure DevOps: the organization's resource area did not name it; the name as typed is kept.");
        return typed;
    }

    /// <summary>A repository as A2 and A3 describe it, in <paramref name="organization"/>; null when its addresses or names cannot be used.</summary>
    private RepositoryInfo? Info(AzureDevOpsRepositoryJson json, string organization)
    {
        if (WebAddress(json.RemoteUrl) is not { } cloneUrl || WebAddress(json.WebUrl) is not { } htmlUrl)
        {
            return null;
        }

        var repository = new HostedRepository(HostingProvider.AzureDevOps, Host, organization + "/" + json.Project.Name, json.Name);
        if (AzureDevOpsUrls.NamesOf(repository) is null)
        {
            return null;
        }

        var parent = json.ParentRepository is { Name: { Length: > 0 } parentName, Project.Name: { Length: > 0 } parentProject }
            ? new HostedRepository(HostingProvider.AzureDevOps, Host, organization + "/" + parentProject, parentName)
            : null;
        return new RepositoryInfo(
            repository,
            json.DefaultBranch is { } branch ? Branch(branch) : string.Empty,
            string.Equals(json.Project.Visibility, "private", StringComparison.OrdinalIgnoreCase),
            json.IsFork,
            IsArchived: false,
            Description: null,
            cloneUrl,
            htmlUrl,
            DateTimeOffset.MinValue,
            CanPush: true,
            parent is not null && AzureDevOpsUrls.NamesOf(parent) is not null ? parent : null,
            ParentDefaultBranch: null);
    }

    /// <summary>A pull request of A4 or A5 on <paramref name="baseRepository"/>; null when it cannot be read.</summary>
    private PullRequest? Pull(AzureDevOpsPullRequestJson json, HostedRepository baseRepository)
    {
        if (json.PullRequestId < 1)
        {
            return null;
        }

        string headRef;
        HostedRepository? head;
        if (json.ForkSource is { } fork)
        {
            headRef = fork.Name is { } name ? Branch(name) : string.Empty;
            head = fork.Repository is { Name: { Length: > 0 } forkName, Project.Name: { Length: > 0 } forkProject }
                ? new HostedRepository(HostingProvider.AzureDevOps, Host, baseRepository.OwnerRoot + "/" + forkProject, forkName)
                : null;
            head = head is not null && AzureDevOpsUrls.NamesOf(head) is not null ? head : null;
        }
        else
        {
            headRef = Branch(json.SourceRefName);
            head = baseRepository;
        }

        return new PullRequest(
            json.PullRequestId,
            json.Title,
            json.CreatedBy?.DisplayName is { } author && !string.IsNullOrWhiteSpace(author) && !IsEmailShaped(author) ? author : null,
            json.IsDraft,
            new Uri(AzureDevOpsUrls.WebUrlOf(Endpoints, baseRepository).AbsoluteUri + "/pullrequest/" + json.PullRequestId.ToString(CultureInfo.InvariantCulture)),
            headRef,
            json.LastMergeSourceCommit?.CommitId ?? string.Empty,
            head,
            Branch(json.TargetRefName),
            baseRepository,
            AzureDevOpsDates.DateOf(json.CreationDate));
    }

    private static string IdOf(AzureDevOpsPullRequestJson json) => json.PullRequestId.ToString(CultureInfo.InvariantCulture);

    /// <summary>A branch's name without <c>refs/heads/</c>.</summary>
    private static string Branch(string reference) => reference.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? reference[HeadsPrefix.Length..] : reference;

    /// <summary>
    /// An address an answer gives, used only once its scheme is <c>http</c> or <c>https</c> (off
    /// Windows .NET reads <c>/x</c> as <c>file:///x</c>, and on Windows <c>//host/x</c> as a share); else null.
    /// </summary>
    private static Uri? WebAddress(string? text) =>
        text is not null
        && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(text, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;

    private bool IsOurs(AccountKey account) =>
        account.Provider == HostingProvider.AzureDevOps && string.Equals(account.Host, Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>A repository of A3 to A5 that is not this service's or not <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>: a validation failure, without a request.</summary>
    private HostingFailure? Invalid(HostedRepository repository) =>
        repository.Provider == HostingProvider.AzureDevOps
        && string.Equals(repository.Host, Host, StringComparison.OrdinalIgnoreCase)
        && AzureDevOpsUrls.NamesOf(repository) is not null
            ? null
            : Own(HostingFailureKind.Validation, NotThisServiceText);

    /// <summary>
    /// Why a token is refused without a request (7a's rule, D378): empty or blank; holding anything
    /// but visible ASCII; or of GitHub's or GitLab's form (<see cref="HostingProviders.TokenFormOf"/>),
    /// with the detail <c>other-provider:&lt;id&gt;</c>, so that it is disclosed to no other service.
    /// </summary>
    private static HostingFailure? Refusal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Own(HostingFailureKind.BadCredentials, EmptyTokenText);
        }

        if (!token.All(c => c is > ' ' and < '\u007F'))
        {
            return Own(HostingFailureKind.BadCredentials, TokenCharactersText);
        }

        return HostingProviders.TokenFormOf(token) is { } form && form != HostingProvider.AzureDevOps
            ? Own(HostingFailureKind.BadCredentials, OtherProviderText, OtherProviderDetail + HostingProviders.Id(form))
            : null;
    }

    /// <summary>A failure the provider makes itself, without a request: it names Azure DevOps.</summary>
    private static HostingFailure Own(HostingFailureKind kind, string message, string? detail = null) =>
        new(kind, message, detail) { Provider = HostingProvider.AzureDevOps };
}
