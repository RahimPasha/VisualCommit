using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Core;
using VisualCommit.Hosting.GitHub;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake's routes (docs/phase-7a-design.md 2.4): which request is which route, the checks in
/// r5 10.3's order, the routes' answers, GitHub's headers (r5 10.4), the faults (r5 10.5) and a
/// renamed repository's redirects (<see cref="Rename"/>).
/// </summary>
public sealed partial class FakeGitHub
{
    private const string ApiPrefix = "/api/v3/";
    private const string AvatarPrefix = "/avatars/u/";
    private const int RateLimit = 5000;

    /// <summary>GitHub's other supported API version, as the live run of 2026-10-30 recorded GitHub naming it; the fake refuses it all the same (D266).</summary>
    private const string OtherSupportedVersion = "2022-11-28";

    /// <summary>Whether the fake answers this request itself (its API, avatars and pages), decided from the request line alone; git's paths are not claimed.</summary>
    internal bool Claims(string method, string path) =>
        path.StartsWith(ApiPrefix, StringComparison.Ordinal) || path == "/api/v3"
        || method == "GET" && (path.StartsWith(AvatarPrefix, StringComparison.Ordinal) || PageOf(path) is not null);

    /// <summary>Answers one request, over TCP or in process: held first when its route is (<see cref="HoldNext"/>), then checked and served, then recorded.</summary>
    internal async Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = RouteOf(request.Method, request.Path);
        if (route is not null)
        {
            await WaitAtHoldAsync(route.Key, ct).ConfigureAwait(false);
        }

        var call = new Call(request, route);
        var response = await AnswerAsync(call, ct).ConfigureAwait(false);
        var body = request.Method is "POST" or "PATCH" ? Encoding.UTF8.GetString(request.Body) : null;
        lock (_gate)
        {
            _requests.Add(new FakeGitHubRequest(
                request.Method,
                request.Host,
                request.Path,
                request.Query,
                route?.Key,
                request.Headers.ContainsKey("Authorization"),
                call.Login,
                request.Headers.ContainsKey("If-None-Match"),
                response.Status,
                body));
        }

        return response;
    }

    private async Task WaitAtHoldAsync(string key, CancellationToken ct)
    {
        PendingHold? hold;
        lock (_gate)
        {
            _holds.Remove(key, out hold);
        }

        if (hold is null)
        {
            return;
        }

        hold.Arrived.TrySetResult();
        try
        {
            await hold.Released.Task.WaitAsync(HoldLimit, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            RecordError($"The hold on {key} was not released within {HoldLimit.TotalSeconds:F0} s; the request went on.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Given up by its sender in process (over a server ct is only the server's stop): neither
            // answered nor recorded, so a test waiting for this looks at once.
            hold.Abandoned.TrySetResult();
            throw;
        }
    }

    private async Task<AuthRemoteResponse> AnswerAsync(Call call, CancellationToken ct)
    {
        var request = call.Request;
        var route = call.Route;
        if (route?.Kind == RouteKind.Avatar)
        {
            return Avatar(route);
        }

        if (route?.Kind == RouteKind.Page)
        {
            return Text(200, "text/html; charset=utf-8", $"<!doctype html><title>{route.Rest}</title>");
        }

        if (!request.Path.StartsWith(ApiPrefix, StringComparison.Ordinal) && request.Path != "/api/v3")
        {
            // In process, git's paths and any other: no route serves them.
            return Text(404, "text/plain; charset=utf-8", "Not found.\n");
        }

        // The checks, in order (r5 10.3).
        if (!request.Headers.ContainsKey("User-Agent"))
        {
            return Error(call, 403, "Request forbidden by administrative rules. Please make sure your request has a User-Agent header.");
        }

        var version = request.Headers.GetValueOrDefault("X-GitHub-Api-Version");
        if (version != GitHubApi.Version)
        {
            return VersionRefused(call, version);
        }

        if (request.Headers.GetValueOrDefault("Accept") is not { } accept || !accept.Contains(GitHubApi.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            return Error(call, 415, "Unsupported 'Accept' header.");
        }

        FakeGitHubFault? fault = null;
        if (route is not null)
        {
            lock (_gate)
            {
                _faults.Remove(route.Key, out fault);
            }
        }

        if (fault is not null && fault.Kind is not (FakeGitHubFaultKind.SsoPartialResults or FakeGitHubFaultKind.CreatedThenDropped))
        {
            return FaultAnswer(call, fault);
        }

        var authorization = request.Headers.GetValueOrDefault("Authorization");
        if (authorization is null || authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Error(call, 401, "Requires authentication");
        }

        if (TokenOf(authorization) is not { } token)
        {
            return Error(call, 401, "Bad credentials");
        }

        call.Login = token.Login;
        call.Token = token;
        if (route is null)
        {
            return Error(call, 404, "Not Found");
        }

        if (route.Kind is RouteKind.Repo or RouteKind.Pulls or RouteKind.Create or RouteKind.Update or RouteKind.Commits
            && (route.Repository is null || !Visible(route.Repository, token)))
        {
            return Error(call, 404, "Not Found");
        }

        if (route.Moved)
        {
            return Renamed(call);
        }

        var answer = route.Kind switch
        {
            RouteKind.User => User(call),
            RouteKind.UserRepos => UserRepositories(call),
            RouteKind.Repo => Json(call, 200, RepositoryJson(route.Repository!, token.Login, withParent: true)),
            RouteKind.Pulls => await PullsAsync(call, route.Repository!, ct).ConfigureAwait(false),
            RouteKind.Create => await CreateAsync(call, route.Repository!, ct).ConfigureAwait(false),
            RouteKind.Update => Update(call, route.Repository!, route.Number),
            RouteKind.Commits => await CommitsAsync(call, route.Repository!, ct).ConfigureAwait(false),
            _ => Error(call, 404, "Not Found"),
        };

        if (fault?.Kind == FakeGitHubFaultKind.SsoPartialResults)
        {
            answer = answer with { Headers = [.. answer.Headers, KeyValuePair.Create("X-GitHub-SSO", "partial-results; organizations=1002")] };
        }

        return fault?.Kind == FakeGitHubFaultKind.CreatedThenDropped ? answer with { Drop = true } : answer;
    }

    /// <summary>
    /// 400 for an <c>X-GitHub-Api-Version</c> other than <see cref="GitHubApi.Version"/>, in GitHub's
    /// shape (the live run of 2026-10-30, D342): <c>message</c> "Bad Request", the explanation in
    /// <c>errors</c> as one string. A version GitHub does not support gets GitHub's words byte for
    /// byte; none, or GitHub's other version, which GitHub accepts, gets the fake's own, which name
    /// the header too (the fake is stricter than GitHub, D266).
    /// </summary>
    private AuthRemoteResponse VersionRefused(Call call, string? version)
    {
        var none = string.IsNullOrEmpty(version);
        var sent = none ? "none" : $"\"{version}\"";
        var explanation = none || version == OtherSupportedVersion
            ? $"The fake of GitHub accepts only \"{GitHubApi.Version}\" in the \"X-GitHub-API-Version\" request header (D266); the request sent {sent}."
            : $"The version you specified in the \"X-GitHub-API-Version\" request header, {sent}, is not a supported version. The following versions are currently supported: \"{GitHubApi.Version}\" (most recent) and \"{OtherSupportedVersion}\".";
        return Json(call, 400, ErrorJson(400, "Bad Request", JsonValue.Create(explanation)));
    }

    // ---- Routes ------------------------------------------------------------------------------------

    private AuthRemoteResponse User(Call call)
    {
        FakeUser? user;
        lock (_gate)
        {
            user = UserLocked(call.Login!);
        }

        return user is null ? Error(call, 404, "Not Found") : Json(call, 200, UserJson(user, withName: true));
    }

    /// <summary>
    /// A2: those the login owns or may push to, and the listed records, paged. A fine-grained token
    /// reaches every repository of its login (GitHub's "All repositories"); "Only select
    /// repositories" is not modelled, for which GitHub lists the selected repositories and the
    /// owner's public ones (the live run of 2026-10-30: the sandbox and 11 public).
    /// </summary>
    private AuthRemoteResponse UserRepositories(Call call)
    {
        var query = call.Query;
        var type = query.Value("type");
        var visibility = query.Value("visibility");
        var affiliation = query.Value("affiliation");
        if (type is not null && (visibility is not null || affiliation is not null))
        {
            return Error(call, 422, "If you specify visibility or affiliation, you cannot specify type.", documentationUrl: ListRepositoriesDocumentationUrl);
        }

        var login = call.Login!;
        var token = call.Token!;
        List<FakeRepository> all;
        lock (_gate)
        {
            all = [.. _repositories];
        }

        var affiliations = (affiliation ?? "owner,collaborator,organization_member").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<FakeRepository> listed = all
            .Where(repository => repository.IsOwnedBy(login) || repository.MayPush(login))
            .Where(repository => Visible(repository, token))
            .Where(repository => visibility switch
            {
                "public" => !repository.IsPrivate,
                "private" => repository.IsPrivate,
                _ => true,
            })
            .Where(repository => affiliations.Contains(AffiliationOf(repository, login), StringComparer.Ordinal))
            .Where(repository => type switch
            {
                "owner" => repository.IsOwnedBy(login),
                "public" => !repository.IsPrivate,
                "private" => repository.IsPrivate,
                "member" => !repository.IsOwnedBy(login),
                _ => true,
            });

        var sort = query.Value("sort") ?? "full_name";
        var byName = sort == "full_name";
        var ascending = (query.Value("direction") ?? (byName ? "asc" : "desc")) == "asc";
        List<FakeRepository> sorted = byName
            ? [.. listed.OrderBy(repository => repository.CurrentFullName, StringComparer.OrdinalIgnoreCase)]
            : [.. listed.OrderByDescending(repository => repository.UpdatedAt).ThenBy(repository => repository.CurrentFullName, StringComparer.OrdinalIgnoreCase)];
        if (ascending != byName)
        {
            sorted.Reverse();
        }

        var (page, link) = Paged(call, sorted);
        return Json(call, 200, new JsonArray([.. page.Select(repository => (JsonNode)RepositoryJson(repository, login))]), link);
    }

    private string AffiliationOf(FakeRepository repository, string login)
    {
        if (repository.IsOwnedBy(login))
        {
            return "owner";
        }

        lock (_gate)
        {
            return UserLocked(repository.Owner) is { IsOrganization: true } ? "organization_member" : "collaborator";
        }
    }

    /// <summary>A4: after a ref sync, by number descending, filtered by <c>state</c>, <c>head</c> (<c>owner:ref</c>) and <c>base</c>, paged.</summary>
    private async Task<AuthRemoteResponse> PullsAsync(Call call, FakeRepository repository, CancellationToken ct)
    {
        await SyncRefsAsync(repository.FullName, ct).ConfigureAwait(false);
        var query = call.Query;
        var state = query.Value("state") ?? "open";
        var head = query.Value("head");
        var baseRef = query.Value("base");
        var (headOwner, headRef) = head is not null && head.IndexOf(':', StringComparison.Ordinal) is > 0 and var colon
            ? (head[..colon], head[(colon + 1)..])
            : ((string?)null, (string?)null);
        var pulls = PullRequests(repository.FullName)
            .Where(pull => state == "all" || pull.State == state)
            .Where(pull => headOwner is null || string.Equals(pull.HeadOwner(repository.Owner), headOwner, StringComparison.OrdinalIgnoreCase) && pull.HeadRef == headRef)
            .Where(pull => baseRef is null || pull.BaseRef == baseRef)
            .OrderByDescending(pull => pull.Number)
            .ToList();
        var (page, link) = Paged(call, pulls);
        return Json(call, 200, new JsonArray([.. page.Select(pull => (JsonNode)PullJson(pull, repository, call.Login))]), link);
    }

    /// <summary>A5: the refusals in order (2.4), else the pull request made, <c>refs/pull/&lt;n&gt;/head</c> written, 201.</summary>
    private async Task<AuthRemoteResponse> CreateAsync(Call call, FakeRepository repository, CancellationToken ct)
    {
        if (BodyOf(call) is not { } body)
        {
            return Error(call, 400, "Problems parsing JSON");
        }

        var login = call.Login!;
        if (call.Token!.Kind == FakeTokenKind.FineGrained && !repository.MayPush(login))
        {
            return Error(call, 403, "Resource not accessible by personal access token", [KeyValuePair.Create("X-Accepted-GitHub-Permissions", "pull_requests=write")]);
        }

        var gate = LockOf(repository);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var bare = repository.BarePath!;
            var refs = await SyncLockedAsync(repository, ct).ConfigureAwait(false);
            var title = StringOf(body, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                return Invalid(call, new JsonObject { ["resource"] = "PullRequest", ["field"] = "title", ["code"] = "missing_field" });
            }

            var baseRef = StringOf(body, "base");
            if (baseRef is null || !refs.ContainsKey("refs/heads/" + baseRef))
            {
                return Invalid(call, new JsonObject { ["resource"] = "PullRequest", ["field"] = "base", ["code"] = "invalid" });
            }

            var head = StringOf(body, "head") ?? string.Empty;
            var colon = head.IndexOf(':', StringComparison.Ordinal);
            var (owner, branch) = colon > 0 ? (head[..colon], head[(colon + 1)..]) : (repository.Owner, head);
            FakeRepository? headRepository;
            lock (_gate)
            {
                headRepository = string.Equals(owner, repository.Owner, StringComparison.OrdinalIgnoreCase)
                    ? repository
                    : _repositories.Find(fork => fork.IsOwnedBy(owner) && fork.BarePath is not null && string.Equals(fork.ForkOf, repository.FullName, StringComparison.OrdinalIgnoreCase));
            }

            var headRefs = headRepository == repository || headRepository is null
                ? refs
                : await ReadRefsAsync(headRepository.BarePath!, ct, "refs/heads/").ConfigureAwait(false);
            if (branch.Length == 0 || headRepository is null || !headRefs.TryGetValue("refs/heads/" + branch, out var headSha))
            {
                return Invalid(call, new JsonObject { ["resource"] = "PullRequest", ["field"] = "head", ["code"] = "invalid" });
            }

            var label = headRepository.Owner + ":" + branch;
            if (PullRequests(repository.FullName).Any(pull => pull.IsOpen && pull.BaseRef == baseRef && pull.HeadRef == branch
                && string.Equals(pull.HeadOwner(repository.Owner), headRepository.Owner, StringComparison.OrdinalIgnoreCase)))
            {
                return Invalid(call, new JsonObject { ["resource"] = "PullRequest", ["code"] = "custom", ["message"] = $"A pull request already exists for {label}." });
            }

            int number;
            lock (_gate)
            {
                number = NextNumberLocked(repository);
            }

            var pullRef = PullRef(number);
            var fromFork = headRepository != repository;
            if (fromFork)
            {
                await GitOkAsync(bare, null, ct, "-c", "gc.auto=0", "fetch", "--quiet", "--no-tags", headRepository.BarePath!, $"+refs/heads/{branch}:{pullRef}").ConfigureAwait(false);
            }

            if (await AheadAsync(bare, "refs/heads/" + baseRef, fromFork ? pullRef : "refs/heads/" + branch, ct).ConfigureAwait(false) == 0)
            {
                if (fromFork)
                {
                    await GitOkAsync(bare, null, ct, "update-ref", "-d", pullRef).ConfigureAwait(false);
                }

                return Invalid(call, new JsonObject { ["resource"] = "PullRequest", ["code"] = "custom", ["message"] = $"No commits between {baseRef} and {branch}" });
            }

            if (!fromFork)
            {
                await GitOkAsync(bare, null, ct, "update-ref", pullRef, headSha).ConfigureAwait(false);
            }

            var pull = new FakePullRequest(repository.FullName, number, title, StringOf(body, "body") ?? string.Empty, login, branch, fromFork ? headRepository.FullName : null, baseRef, BoolOf(body, "draft") ?? false)
            {
                MaintainerCanModify = BoolOf(body, "maintainer_can_modify") ?? true,
                HeadSha = headSha,
            };
            lock (_gate)
            {
                _pulls.Add(pull);
            }

            var location = new Uri(Endpoints.ApiBase, string.Create(CultureInfo.InvariantCulture, $"repos/{repository.CurrentFullName}/pulls/{number}")).AbsoluteUri;
            return Json(call, 201, PullJson(pull, repository, login), [KeyValuePair.Create("Location", location)]);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The PATCH of a pull request: <c>state</c>, <c>title</c>, <c>body</c>; 200.</summary>
    private AuthRemoteResponse Update(Call call, FakeRepository repository, int number)
    {
        if (BodyOf(call) is not { } body)
        {
            return Error(call, 400, "Problems parsing JSON");
        }

        if (PullRequests(repository.FullName).FirstOrDefault(candidate => candidate.Number == number) is not { } pull)
        {
            return Error(call, 404, "Not Found");
        }

        if (StringOf(body, "state") is "open" or "closed")
        {
            pull.State = StringOf(body, "state")!;
        }

        if (StringOf(body, "title") is { Length: > 0 } title)
        {
            pull.Title = title;
        }

        if (StringOf(body, "body") is { } text)
        {
            pull.Body = text;
        }

        return Json(call, 200, PullJson(pull, repository, call.Login));
    }

    /// <summary>
    /// A6: one <c>git log</c> from <c>sha</c> (the default branch), filtered by <c>author</c> (an
    /// e-mail, or a login through <see cref="LinkEmail"/>), paged; 409 when empty; for a revision
    /// git cannot resolve, 404 "Not Found", or 500 without a body beside <c>author</c> (D342).
    /// </summary>
    private async Task<AuthRemoteResponse> CommitsAsync(Call call, FakeRepository repository, CancellationToken ct)
    {
        if (repository.BarePath is not { } bare || IsEmpty(bare))
        {
            return Error(call, 409, "Git Repository is empty.");
        }

        var sha = call.Query.Value("sha") ?? repository.DefaultBranch;
        if (await LogAsync(bare, sha, ct).ConfigureAwait(false) is not { } commits)
        {
            // github.com, 2026-10-30, three samples: a SHA it does not have is 404 alone and 500 with
            // an empty body beside author. A branch name GitHub lacks is taken to answer as an
            // unknown SHA (not measured).
            return call.Query.Value("author") is null
                ? Error(call, 404, "Not Found", documentationUrl: ListCommitsDocumentationUrl)
                : Empty(call, 500);
        }

        if (call.Query.Value("author") is { } author)
        {
            Dictionary<string, string> emails;
            lock (_gate)
            {
                emails = new Dictionary<string, string>(_emails, StringComparer.OrdinalIgnoreCase);
            }

            commits = author.Contains('@', StringComparison.Ordinal)
                ? [.. commits.Where(commit => string.Equals(commit.AuthorEmail, author, StringComparison.OrdinalIgnoreCase))]
                : [.. commits.Where(commit => emails.TryGetValue(commit.AuthorEmail, out var login) && string.Equals(login, author, StringComparison.OrdinalIgnoreCase))];
        }

        var (page, link) = Paged(call, commits);
        return Json(call, 200, new JsonArray([.. page.Select(commit => (JsonNode)CommitJson(repository, commit))]), link);
    }

    /// <summary>An avatar, without auth: the user's bytes; 404 for an id the fake does not know.</summary>
    private AuthRemoteResponse Avatar(Route route)
    {
        FakeUser? user;
        lock (_gate)
        {
            user = _users.Values.FirstOrDefault(candidate => candidate.Id == route.UserId);
        }

        return user is null
            ? Text(404, "text/plain; charset=utf-8", "Not found.\n")
            : new AuthRemoteResponse(200, "OK", [KeyValuePair.Create("Content-Type", "image/png"), KeyValuePair.Create("Cache-Control", "max-age=300")], user.AvatarBytes);
    }

    // ---- Faults ------------------------------------------------------------------------------------

    private AuthRemoteResponse FaultAnswer(Call call, FakeGitHubFault fault)
    {
        var route = call.Route!;
        switch (fault.Kind)
        {
            case FakeGitHubFaultKind.BadCredentials:
                return Error(call, 401, "Bad credentials");
            case FakeGitHubFaultKind.PrimaryRateLimit:
                var token = call.Request.Headers.GetValueOrDefault("Authorization") is { } authorization ? TokenOf(authorization) : null;
                long id;
                lock (_gate)
                {
                    id = token is null ? 0 : UserLocked(token.Login)?.Id ?? 0;
                }

                return Error(call, 403, string.Create(CultureInfo.InvariantCulture, $"API rate limit exceeded for user ID {id}."),
                [
                    KeyValuePair.Create("X-RateLimit-Limit", RateLimit.ToString(CultureInfo.InvariantCulture)),
                    KeyValuePair.Create("X-RateLimit-Remaining", "0"),
                    KeyValuePair.Create("X-RateLimit-Used", RateLimit.ToString(CultureInfo.InvariantCulture)),
                    KeyValuePair.Create("X-RateLimit-Reset", RateReset),
                    KeyValuePair.Create("X-RateLimit-Resource", "core"),
                ]);
            case FakeGitHubFaultKind.SecondaryRateLimit:
                return Error(call, 403, "You have exceeded a secondary rate limit. Please wait a few minutes before you try again.", [KeyValuePair.Create("Retry-After", "60")]);
            case FakeGitHubFaultKind.SsoRequired:
                return Error(call, 403, "Resource protected by organization SAML enforcement.",
                    [KeyValuePair.Create("X-GitHub-SSO", $"required; url={Web("orgs/acme/sso?authorization_request=FAKE").AbsoluteUri}")]);
            case FakeGitHubFaultKind.Forbidden:
                var permission = route.Kind switch
                {
                    RouteKind.Pulls => "pull_requests=read",
                    RouteKind.Create or RouteKind.Update => "pull_requests=write",
                    RouteKind.Commits => "contents=read",
                    _ => "metadata=read",
                };
                return Error(call, 403, "Resource not accessible by personal access token", [KeyValuePair.Create("X-Accepted-GitHub-Permissions", permission)]);
            case FakeGitHubFaultKind.NotFound:
                return Error(call, 404, "Not Found");
            case FakeGitHubFaultKind.ApiVersionGone:
                return Error(call, 410, $"API version {GitHubApi.Version} is no longer supported.");
            case FakeGitHubFaultKind.ServerError:
                return Error(call, 500, "Server Error");
            case FakeGitHubFaultKind.BadGateway:
                return Text(502, "text/html; charset=utf-8", "<html><head><title>502 Bad Gateway</title></head><body><h1>502 Bad Gateway</h1></body></html>");
            case FakeGitHubFaultKind.Malformed:
                return new AuthRemoteResponse(200, "OK", [KeyValuePair.Create("Content-Type", "application/json; charset=utf-8"), RequestIdHeader()], "{\"login\":"u8.ToArray());
            default:
                var location = fault.Location?.AbsoluteUri ?? MovedLocation(call);
                var moved = new JsonObject { ["message"] = "Moved Permanently", ["url"] = location, ["documentation_url"] = DocumentationUrl };
                return Json(call, 301, moved, [KeyValuePair.Create("Location", location)]);
        }
    }

    /// <summary>
    /// A request of an older name of a renamed repository (<see cref="Rename"/>): 301 to
    /// <see cref="MovedLocation"/>, or 307 for a request with a body, as GitHub redirects it.
    /// </summary>
    private AuthRemoteResponse Renamed(Call call)
    {
        var location = MovedLocation(call);
        var status = call.Request.Method is "POST" or "PATCH" ? 307 : 301;
        var moved = new JsonObject { ["message"] = ReasonOf(status), ["url"] = location, ["documentation_url"] = DocumentationUrl };
        return Json(call, status, moved, [KeyValuePair.Create("Location", location)]);
    }

    /// <summary>Where <see cref="FakeGitHubFault.Moved"/> and a renamed repository's older name send a repository's route: <c>api/v3/repositories/&lt;id&gt;</c> plus the rest of the path, the query kept.</summary>
    private string MovedLocation(Call call)
    {
        var route = call.Route!;
        var relative = route.Repository is { } repository
            ? string.Create(CultureInfo.InvariantCulture, $"repositories/{repository.Id}") + (route.Rest.Length > 0 ? "/" + route.Rest : string.Empty)
            : call.Request.Path[ApiPrefix.Length..];
        return new Uri(Endpoints.ApiBase, relative + (call.Request.Query.Length > 0 ? "?" + call.Request.Query : string.Empty)).AbsoluteUri;
    }

    // ---- Answers and headers -----------------------------------------------------------------------

    /// <summary>A5's 422 "Validation Failed" with <paramref name="error"/> (<c>resource</c> first, as GitHub's) as its one error, on GitHub's documentation of the create.</summary>
    private AuthRemoteResponse Invalid(Call call, JsonObject error) =>
        Json(call, 422, ErrorJson(422, "Validation Failed", new JsonArray(error), CreatePullRequestDocumentationUrl));

    private AuthRemoteResponse Error(Call call, int status, string message, IEnumerable<KeyValuePair<string, string>>? extra = null, string documentationUrl = DocumentationUrl) =>
        Json(call, status, ErrorJson(status, message, documentationUrl: documentationUrl), null, extra);

    /// <summary>An answer of <paramref name="status"/> without a body or a <c>Content-Type</c>, with GitHub's common headers and counted (A6's 500, D342).</summary>
    private AuthRemoteResponse Empty(Call call, int status)
    {
        var headers = new List<KeyValuePair<string, string>>();
        AddCommonHeaders(call, headers, counted: true, notModified: false);
        return new AuthRemoteResponse(status, ReasonOf(status), headers, []);
    }

    private AuthRemoteResponse Json(Call call, int status, JsonNode node, IEnumerable<KeyValuePair<string, string>> extra) => Json(call, status, node, null, extra);

    /// <summary>
    /// A JSON answer with GitHub's headers (r5 10.4): <c>ETag</c> on a success, and a matching
    /// <c>If-None-Match</c> answered 304 without a body and not counted; the rate limit's headers
    /// for a signed-in request; <c>X-OAuth-Scopes</c> for a classic token; the token's expiry
    /// when set, except on a 304 (GitHub's had none in the live run of 2026-10-30); <c>Link</c>
    /// for a page; <c>X-GitHub-Request-Id</c>.
    /// </summary>
    private AuthRemoteResponse Json(Call call, int status, JsonNode node, string? link = null, IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        var body = Serialize(node);
        var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Content-Type", "application/json; charset=utf-8") };
        if (status is >= 200 and < 300)
        {
            var etag = "W/\"" + Convert.ToHexStringLower(SHA256.HashData(body))[..32] + "\"";
            if (call.Request.Headers.GetValueOrDefault("If-None-Match") is { } given
                && given.Split(',', StringSplitOptions.TrimEntries).Contains(etag, StringComparer.Ordinal))
            {
                var unchanged = new List<KeyValuePair<string, string>> { KeyValuePair.Create("ETag", etag) };
                AddCommonHeaders(call, unchanged, counted: false, notModified: true);
                return new AuthRemoteResponse(304, ReasonOf(304), unchanged, []);
            }

            headers.Add(KeyValuePair.Create("ETag", etag));
        }

        AddCommonHeaders(call, headers, counted: true, notModified: false);
        if (link is not null)
        {
            headers.Add(KeyValuePair.Create("Link", link));
        }

        headers.AddRange(extra ?? []);
        return new AuthRemoteResponse(status, ReasonOf(status), headers, body);
    }

    /// <summary>
    /// The rate limit's headers, <c>X-OAuth-Scopes</c> and the token's expiry for a signed-in
    /// request, the request counted when <paramref name="counted"/>, the expiry left out of a 304
    /// (<paramref name="notModified"/>); then <c>X-GitHub-Request-Id</c>.
    /// </summary>
    private void AddCommonHeaders(Call call, List<KeyValuePair<string, string>> headers, bool counted, bool notModified)
    {
        if (call.Token is { } token)
        {
            int used;
            lock (_gate)
            {
                used = _used.GetValueOrDefault(token.Login) + (counted ? 1 : 0);
                _used[token.Login] = used;
            }

            headers.Add(KeyValuePair.Create("X-RateLimit-Limit", RateLimit.ToString(CultureInfo.InvariantCulture)));
            headers.Add(KeyValuePair.Create("X-RateLimit-Remaining", Math.Max(0, RateLimit - used).ToString(CultureInfo.InvariantCulture)));
            headers.Add(KeyValuePair.Create("X-RateLimit-Used", used.ToString(CultureInfo.InvariantCulture)));
            headers.Add(KeyValuePair.Create("X-RateLimit-Reset", RateReset));
            headers.Add(KeyValuePair.Create("X-RateLimit-Resource", "core"));
            if (token.Kind == FakeTokenKind.Classic)
            {
                headers.Add(KeyValuePair.Create("X-OAuth-Scopes", string.Join(", ", token.Scopes)));
            }

            if (!notModified && token.Expires is { } expires)
            {
                headers.Add(KeyValuePair.Create("GitHub-Authentication-Token-Expiration", expires.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)));
            }
        }

        headers.Add(RequestIdHeader());
    }

    private KeyValuePair<string, string> RequestIdHeader() =>
        KeyValuePair.Create("X-GitHub-Request-Id", string.Create(CultureInfo.InvariantCulture, $"FAKE-{Interlocked.Increment(ref _requestCount) % 10000:D4}"));

    /// <summary>The rate limit's reset: an hour after <see cref="AppClocks.HarnessTime"/>, in Unix seconds.</summary>
    private static string RateReset => AppClocks.HarnessTime.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private AuthRemoteResponse Text(int status, string contentType, string text) =>
        new(status, ReasonOf(status), [KeyValuePair.Create("Content-Type", contentType), RequestIdHeader()], Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// One page of <paramref name="items"/> by <c>per_page</c> (30, at most <see cref="MaxPerPage"/>)
    /// and <c>page</c>, and its <c>Link</c> header: <c>prev</c> and <c>first</c> past page 1,
    /// <c>next</c> and <c>last</c> before the last, absolute on the API's address, the other
    /// parameters kept; null when everything fits on one page.
    /// </summary>
    private (IReadOnlyList<T> Page, string? Link) Paged<T>(Call call, IReadOnlyList<T> items)
    {
        var query = call.Query;
        var perPage = Math.Clamp(query.Number("per_page") ?? 30, 1, MaxPerPage);
        var page = Math.Max(query.Number("page") ?? 1, 1);
        var last = Math.Max(1, (items.Count + perPage - 1) / perPage);
        var slice = items.Skip((page - 1) * perPage).Take(perPage).ToList();
        if (last == 1)
        {
            return (slice, null);
        }

        var path = call.Request.Path[ApiPrefix.Length..];
        string Link(int target, string rel) =>
            $"<{new Uri(Endpoints.ApiBase, path + "?" + query.WithPage(target)).AbsoluteUri}>; rel=\"{rel}\"";

        var links = new List<string>();
        if (page > 1)
        {
            links.Add(Link(page - 1, "prev"));
        }

        if (page < last)
        {
            links.Add(Link(page + 1, "next"));
            links.Add(Link(last, "last"));
        }

        if (page > 1)
        {
            links.Add(Link(1, "first"));
        }

        return (slice, string.Join(", ", links));
    }

    private static string ReasonOf(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        301 => "Moved Permanently",
        304 => "Not Modified",
        307 => "Temporary Redirect",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        410 => "Gone",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        _ => "Status",
    };

    // ---- Requests ----------------------------------------------------------------------------------

    /// <summary>The token of a <c>Bearer</c> or <c>token</c> header, when the fake knows it and it is not revoked.</summary>
    private FakeToken? TokenOf(string authorization)
    {
        var space = authorization.IndexOf(' ', StringComparison.Ordinal);
        if (space < 0 || authorization[..space] is not ({ } scheme) || !(scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || scheme.Equals("token", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        lock (_gate)
        {
            return _tokens.TryGetValue(authorization[(space + 1)..].Trim(), out var token) && !token.Revoked ? token : null;
        }
    }

    /// <summary>Whether the token's login sees the repository: a public one, or one it owns or pushes to (a classic token needs <c>repo</c> for a private one).</summary>
    private static bool Visible(FakeRepository repository, FakeToken token) =>
        !repository.IsPrivate
        || (repository.IsOwnedBy(token.Login) || repository.MayPush(token.Login))
            && (token.Kind == FakeTokenKind.FineGrained || token.Scopes.Contains("repo", StringComparer.Ordinal));

    private static JsonObject? BodyOf(Call call)
    {
        try
        {
            return JsonNode.Parse(call.Request.Body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringOf(JsonObject body, string name) =>
        body[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool? BoolOf(JsonObject body, string name) =>
        body[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    /// <summary>Which route a request is (2.4's keys, lower-case); null for a path no route serves.</summary>
    private Route? RouteOf(string method, string path)
    {
        if (path.StartsWith(AvatarPrefix, StringComparison.Ordinal))
        {
            return method == "GET" && long.TryParse(path[AvatarPrefix.Length..].TrimEnd('/'), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? new Route(string.Create(CultureInfo.InvariantCulture, $"avatar:{id}"), RouteKind.Avatar, null, UserId: id)
                : null;
        }

        if (!path.StartsWith(ApiPrefix, StringComparison.Ordinal))
        {
            return method == "GET" && PageOf(path) is { } page ? new Route(page.Key, RouteKind.Page, null, Rest: page.Title) : null;
        }

        var segments = path[ApiPrefix.Length..].TrimEnd('/').Split('/');
        if (segments is ["repositories", var idText, ..]
            && long.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var repositoryId)
            && RepositoryById(repositoryId) is { } byId)
        {
            segments = ["repos", byId.Owner, byId.CurrentName, .. segments[2..]];
        }

        switch (segments)
        {
            case ["user"] when method == "GET":
                return new Route("user", RouteKind.User, null);
            case ["user", "repos"] when method == "GET":
                return new Route("user/repos", RouteKind.UserRepos, null);
            case ["repos", var owner, var name, .. var rest]:
                FakeRepository? repository;
                lock (_gate)
                {
                    repository = RepositoryLocked(owner + "/" + name);
                }

                var fullName = (repository?.CurrentFullName ?? owner + "/" + name).ToLowerInvariant();
                var restPath = string.Join('/', rest);

                // An older name of a renamed repository: the route is its, and redirected.
                var asked = owner + "/" + (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name);
                var moved = repository is not null && !string.Equals(asked, repository.CurrentFullName, StringComparison.OrdinalIgnoreCase);
                return (rest, method) switch
                {
                    ([], "GET") => new Route("repo:" + fullName, RouteKind.Repo, repository, Moved: moved),
                    (["pulls"], "GET") => new Route("pulls:" + fullName, RouteKind.Pulls, repository, Rest: restPath, Moved: moved),
                    (["pulls"], "POST") => new Route("create:" + fullName, RouteKind.Create, repository, Rest: restPath, Moved: moved),
                    (["pulls", var n], "PATCH") when int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                        => new Route("update:" + fullName, RouteKind.Update, repository, number, Rest: restPath, Moved: moved),
                    (["commits"], "GET") => new Route("commits:" + fullName, RouteKind.Commits, repository, Rest: restPath, Moved: moved),
                    _ => null,
                };
            default:
                return null;
        }
    }

    /// <summary>A web page's route key and title: <c>/o/r</c>, <c>/o/r/pulls</c>, <c>/o/r/pull/&lt;n&gt;</c>, <c>/settings/…</c>; null for any other path, git's included.</summary>
    private static (string Key, string Title)? PageOf(string path)
    {
        var segments = path.Trim('/').Split('/');
        if (segments is ["settings", ..])
        {
            return ("page:settings", "Settings");
        }

        if (segments.Any(segment => segment.Length == 0) || segments[0] is "api" or "avatars")
        {
            return null;
        }

        return segments switch
        {
            [var owner, var name] when !name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) => (("page:" + owner + "/" + name).ToLowerInvariant(), owner + "/" + name),
            [var owner, var name, "pulls"] => (("page:" + owner + "/" + name).ToLowerInvariant(), owner + "/" + name + " pull requests"),
            [var owner, var name, "pull", var n] when n.All(char.IsAsciiDigit) => (("page:" + owner + "/" + name).ToLowerInvariant(), $"{owner}/{name} #{n}"),
            _ => null,
        };
    }

    private enum RouteKind
    {
        User,
        UserRepos,
        Repo,
        Pulls,
        Create,
        Update,
        Commits,
        Avatar,
        Page,
    }

    /// <summary>A route: its key, kind, repository (null when the fake has none of that name), pull request number, avatar's user id, the rest of the path after the repository (a page's title), and whether it named a renamed repository's older name.</summary>
    private sealed record Route(string Key, RouteKind Kind, FakeRepository? Repository, int Number = 0, long UserId = 0, string Rest = "", bool Moved = false);

    /// <summary>One request on its way through the checks: who it signed in as, once it has.</summary>
    private sealed class Call(AuthRemoteRouteRequest request, Route? route)
    {
        public AuthRemoteRouteRequest Request { get; } = request;

        public Route? Route { get; } = route;

        public Query Query { get; } = new(request.Query);

        public string? Login { get; set; }

        public FakeToken? Token { get; set; }
    }

    /// <summary>A request's query: its parameters in order, read with <c>+</c> as a space.</summary>
    private sealed class Query(string raw)
    {
        private readonly List<(string Key, string Value, string Raw)> _parameters = [.. Parse(raw)];

        public string? Value(string key) => _parameters.FindIndex(parameter => parameter.Key == key) is >= 0 and var index ? _parameters[index].Value : null;

        public int? Number(string key) => int.TryParse(Value(key), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

        /// <summary>The query with <c>page</c> set to <paramref name="page"/>, last, the other parameters as they came.</summary>
        public string WithPage(int page) =>
            string.Join('&', [.. _parameters.Where(parameter => parameter.Key != "page").Select(parameter => parameter.Raw), string.Create(CultureInfo.InvariantCulture, $"page={page}")]);

        private static IEnumerable<(string Key, string Value, string Raw)> Parse(string raw)
        {
            foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = part.IndexOf('=', StringComparison.Ordinal);
                var key = Uri.UnescapeDataString((equals < 0 ? part : part[..equals]).Replace('+', ' '));
                var value = equals < 0 ? string.Empty : Uri.UnescapeDataString(part[(equals + 1)..].Replace('+', ' '));
                yield return (key, value, part);
            }
        }
    }
}
