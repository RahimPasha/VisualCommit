// The fake of Azure DevOps' answers that the research left ASSUMED and that WP-LIVE's probes settle
// before part A's checks copy them (docs/phase-7b-design.md 3.7; r5 9, g1 G6; D342, D370, D373, D374).
// The fix package (WP-LF) copied stage 1's run of 2026-11-05 and marked each one MEASURED 2026-11-05:
// - connectionData for a member signed in with a Code-only token (g1 G3.2's shape; the identity's nine
//   member names as served, customDisplayName among them, by default the display name; null leaves it
//   out, as g1 G3.2's sample 1 has it, READ);
// - check 4's refusals of D373's made-up values, each sent as Basic with the empty user: a short one,
//   on connectionData 200 with the Anonymous identity, with and without Suppress, in the private
//   organization too (the fake's 302 and 401 TF400813 stay for no credential at all, MEASURED r5 1.3);
//   an 84-character PAT-shaped one, 401 with an empty body and one Basic challenge, with and without
//   Suppress; with a user (x-access-token) both are ASSUMED alike, the user ignored (READ g1 G1.0);
// - check 1 with a token (D374's made-up organization: 404 "The resource cannot be found." both ways);
// - the core resource area (areas), without a credential and with the token;
// - create: 201 (CreateStatus), isDraft, createdBy.id the account's, mergeStatus succeeded and the
//   merge ref at the read after it; the 409 TF401179 and the 400 TF401398, word for word; an empty
//   title's 400 InvalidArgumentValueException with its own words and eventId 0; one with nothing
//   ahead, 201 with labels; every error's Content-Type without api-version;
// - abandon through PATCH (update): 200 without mergeStatus and lastMergeCommit, and the merge ref
//   gone at the next read;
// - the refs delete (POST …/refs): 200, each update's seven members (no customMessage, rejectedBy);
//   the refs create (oldObjectId forty zeros, the -even branch at main's tip): 200, succeeded and
//   success true (its members ASSUMED alike to the delete's);
// - the sandbox's row in the project's list (ListProject); no BOM on any of these.
// Kept, with their reasons (D370):
// - git's info/refs refusals of the made-up values: the service answers 401, a short value with its
//   TF400813 HTML page (a BOM, text/html, Basic, Bearer and TFS-Federated challenges), an 84-character
//   one empty with one Basic challenge; the fake's git is AuthRemoteServer's, shared with 7a's fake,
//   whose 401 is "Authentication required." with one Basic challenge (the fake's realm), and in
//   process git's paths are 404 (1.9). Git asks the app's helper on any 401 and the helper reads no
//   wwwauth[] (AskPassProtocol), so no path of the app's differs; the statuses agree in host mode.
// - mergeStatus queued in the create's answer (READ r5 sample; stage 1 read succeeded only after).
// Kept READ or ASSUMED, not reached by the live command:
// - a read-only token's write (401 TF400813) and the expired token's body (AccessCheckException with
//   its BOM), which need a token D364 does not make (design 15.7, 16.1);
// - with no fork in the sandbox (D375): parentRepository, the fork shapes (forkSource, sourceRefName
//   refs/pull/<n>/source) and /source refs;
// - outside D352 and D374: another organization's token, an unknown project (TF200016), TF401027's
//   statuses, 429's typeKey, the 5xx bodies, $top's default (101) and its cap (MaxPerPage); a
//   Bearer on connectionData of a private organization (no credential there is r5 1.3's 302 and 401,
//   MEASURED).
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake of Azure DevOps' routes (docs/phase-7b-design.md 3.3): which request is which route,
/// the checks in their order, the routes' answers and the faults (3.4). Every path is below
/// <see cref="Endpoints"/>' web base, names matched without case (MEASURED r5 6.1); a route's key is
/// lower-case, its names decoded (<c>pulls:fernhill-labs/mobile app/mobile-app</c>).
/// </summary>
public sealed partial class FakeAzureDevOps
{
    private const int DefaultTop = 101;

    /// <summary>An api-version's form, ASCII digits only (<c>\d</c> would take any Unicode digit, which <c>int</c> does not read).</summary>
    private static readonly Regex VersionPattern = new(@"^([0-9]+)\.([0-9]+)(-preview(\.([0-9]+))?)?\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private enum Kind
    {
        None,
        Page,
        Connection,
        Areas,
        Repos,
        Repo,
        Pulls,
        Pull,
        Create,
        Update,
        Refs,
        RefUpdates,
    }

    /// <summary>
    /// Claimed: a path with an <c>_apis</c> segment, and every GET but git's <c>…/info/refs</c>
    /// (design 3.3); git's other requests go to the server's git.
    /// </summary>
    protected override bool Claims(string method, string path) =>
        Segments(path).Contains("_apis", StringComparer.OrdinalIgnoreCase)
        || method == "GET" && !path.EndsWith("/info/refs", StringComparison.Ordinal);

    /// <inheritdoc/>
    protected override string? RouteOf(string method, string path) => Parse(method, path).Key;

    /// <summary>The checks in design 3.3's order, then the route's answer.</summary>
    protected override async Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct)
    {
        var request = call.Request;
        var route = Parse(request.Method, request.Path);
        var fault = call.Fault as FakeAzureDevOpsFault;
        var late = fault == FakeAzureDevOpsFault.AnonymousIdentity || fault == FakeAzureDevOpsFault.Delayed || fault == FakeAzureDevOpsFault.CreatedThenDropped;
        if (route.Kind == Kind.Page)
        {
            return fault is not null && !late ? FaultAnswer(call, route, fault, null) : Finish(fault, Page());
        }

        // 1. The organization (MEASURED r5 1.3 without a credential; with a token MEASURED 2026-11-05, D374).
        FakeAzureOrganization? organization;
        if (route.Kind == Kind.Areas && route.Organization is null)
        {
            var named = QueryValue(request.Query, "organizationName") ?? QueryValue(request.Query, "accountName");
            if (named is null)
            {
                return PageNotFound();
            }

            lock (Gate)
            {
                organization = OrganizationLocked(named);
            }

            if (organization is null)
            {
                return HostMissing(named);
            }
        }
        else
        {
            lock (Gate)
            {
                organization = OrganizationLocked(route.Organization!);
            }

            if (organization is null)
            {
                return OrganizationNotFound();
            }
        }

        if (route.Kind == Kind.None)
        {
            return PageNotFound();
        }

        // 2. The api-version (r5 1.4's words, MEASURED; none refused in the fake's words, D370).
        var (served, refusal) = CheckVersion(request, route.Kind);
        if (refusal is not null)
        {
            return refusal;
        }

        // 3. A pending fault.
        if (fault is not null && !late)
        {
            return FaultAnswer(call, route, fault, served);
        }

        // 4. The credentials: Authorization: Basic alone, its user ignored (READ g1 G1.0).
        FakeAzureToken? caller = null;
        if (route.Kind != Kind.Areas)
        {
            var ignored = fault == FakeAzureDevOpsFault.AnonymousIdentity;
            if (!ignored && call.Authorization is FakeAuthorizationForm.Basic or FakeAuthorizationForm.BasicEmptyUser && IsPatShaped(call.Token!))
            {
                var (known, refused) = TokenOf(call.Token!, organization, served!);
                if (refused is not null)
                {
                    return refused;
                }

                caller = known;
                call.Login = known!.Login;
            }

            if (caller is null)
            {
                // connectionData with a Basic credential whose password is no PAT: 200 with the
                // Anonymous identity, with and without Suppress, in a private organization too
                // (MEASURED 2026-11-05 with the empty user, D373's short value; with a user ASSUMED
                // alike, the user ignored, READ g1 G1.0: no path of the app's sends one there; with no
                // credential at all r5 1.3's 302 or 401 stays, MEASURED); a public organization's for
                // anyone (MEASURED r5 1.2).
                var plainBasic = call.Authorization is FakeAuthorizationForm.Basic or FakeAuthorizationForm.BasicEmptyUser;
                if (route.Kind == Kind.Connection && (ignored || organization.PublicProjects || plainBasic))
                {
                    return Finish(fault, Json(200, ConnectionJson(null), served));
                }

                if (!AnonymousMayRead(route, organization))
                {
                    return SignIn(request, served);
                }
            }
        }

        // 5. The route.
        var answer = route.Kind switch
        {
            Kind.Connection => Json(200, ConnectionJson(caller!.User), served),
            Kind.Areas => Json(200, AreaJson(organization), served),
            Kind.Repos => Repositories(route, organization, caller, served!),
            Kind.Repo => SingleRepository(route, organization, caller, served!),
            Kind.Pulls => await PullsAsync(call, route, organization, caller, served!, ct).ConfigureAwait(false),
            Kind.Pull => await PullAsync(route, organization, caller, served!, ct).ConfigureAwait(false),
            Kind.Create => await CreateAsync(call, route, organization, caller!, served!, ct).ConfigureAwait(false),
            Kind.Update => Update(call, route, organization, caller!, served!),
            Kind.Refs => await RefsAsync(call, route, organization, caller, served!, ct).ConfigureAwait(false),
            Kind.RefUpdates => await RefUpdatesAsync(call, route, organization, caller!, served!, ct).ConfigureAwait(false),
            _ => PageNotFound(),
        };
        return Finish(fault, answer);
    }

    /// <summary>A request's route: its kind, names (decoded) and key (lower-case).</summary>
    private sealed record Route(Kind Kind, string? Organization, string? Project, string? Repository, int Number, string? Key);

    /// <summary>Which route a request line is (design 3.3's table); a path with <c>_apis</c> that is none of them is <see cref="Kind.None"/>, without a key.</summary>
    private Route Parse(string method, string path)
    {
        var segments = Segments(path);
        var apis = Array.FindIndex(segments, segment => string.Equals(segment, "_apis", StringComparison.OrdinalIgnoreCase));
        if (apis < 0)
        {
            return method == "GET" ? new Route(Kind.Page, null, null, null, 0, "page") : new Route(Kind.None, null, null, null, 0, null);
        }

        var prefix = segments[..apis];
        var rest = segments[(apis + 1)..];
        bool Is(int index, string literal) => rest.Length > index && string.Equals(rest[index], literal, StringComparison.OrdinalIgnoreCase);
        var none = new Route(Kind.None, prefix.Length > 0 ? prefix[0] : null, null, null, 0, null);
        if (method == "GET" && rest.Length == 2 && Is(0, "resourceAreas") && string.Equals(rest[1], CoreAreaId, StringComparison.OrdinalIgnoreCase) && prefix.Length <= 1)
        {
            return new Route(Kind.Areas, prefix.Length == 1 ? prefix[0] : null, null, null, 0, "areas");
        }

        if (prefix.Length == 1)
        {
            var o = prefix[0];
            if (method == "GET" && rest.Length == 1 && Is(0, "connectionData"))
            {
                return new Route(Kind.Connection, o, null, null, 0, Key("connection", o));
            }

            return method == "GET" && rest.Length == 2 && Is(0, "git") && Is(1, "repositories")
                ? new Route(Kind.Repos, o, null, null, 0, Key("repos", o))
                : none;
        }

        if (prefix.Length != 2 || !Is(0, "git"))
        {
            return none;
        }

        var (org, project) = (prefix[0], prefix[1]);
        var projectKey = ProjectKeyName(org, project);
        if (rest.Length == 3 && Is(1, "pullrequests") && method == "GET" && Number(rest[2]) is { } byId)
        {
            return new Route(Kind.Pull, org, project, null, byId, Key("pull", org, projectKey, byId.ToString(CultureInfo.InvariantCulture)));
        }

        if (!Is(1, "repositories"))
        {
            return none;
        }

        if (rest.Length == 2)
        {
            return method == "GET" ? new Route(Kind.Repos, org, project, null, 0, Key("repos", org, projectKey)) : none;
        }

        var repository = rest[2];
        var named = RepositoryKeyName(org, project, repository);
        return (rest.Length, method) switch
        {
            (3, "GET") => new Route(Kind.Repo, org, project, repository, 0, Key("repo", org, projectKey, named)),
            (4, "GET") when Is(3, "pullrequests") => new Route(Kind.Pulls, org, project, repository, 0, Key("pulls", org, projectKey, named)),
            (4, "POST") when Is(3, "pullrequests") => new Route(Kind.Create, org, project, repository, 0, Key("create", org, projectKey, named)),
            (5, "GET") when Is(3, "pullrequests") && Number(rest[4]) is { } n => new Route(Kind.Pull, org, project, repository, n, Key("pull", org, projectKey, rest[4])),
            (5, "PATCH") when Is(3, "pullrequests") && Number(rest[4]) is { } n => new Route(Kind.Update, org, project, repository, n, Key("update", org, projectKey, rest[4])),
            (4, "GET") when Is(3, "refs") => new Route(Kind.Refs, org, project, repository, 0, Key("refs", org, projectKey, named)),
            (4, "POST") when Is(3, "refs") => new Route(Kind.RefUpdates, org, project, repository, 0, Key("refs", org, projectKey, named)),
            _ => none,
        };
    }

    /// <summary>A project's name in a key: the stored name when the segment is its GUID (the fake's own addresses name it so), else the segment.</summary>
    private string ProjectKeyName(string organization, string project)
    {
        if (!Guid.TryParse(project, out _))
        {
            return project;
        }

        lock (Gate)
        {
            var owner = OrganizationLocked(organization);
            return (owner is null ? null : ProjectLocked(owner, project))?.Name ?? project;
        }
    }

    /// <summary>A repository's name in a key: the stored name when the segment is its GUID, else the segment.</summary>
    private string RepositoryKeyName(string organization, string project, string repository)
    {
        if (!Guid.TryParse(repository, out _))
        {
            return repository;
        }

        lock (Gate)
        {
            var owner = OrganizationLocked(organization);
            var inProject = owner is null ? null : ProjectLocked(owner, project);
            return (inProject is null ? null : RepositoryLocked(inProject, repository))?.Name ?? repository;
        }
    }

    private static string Key(string kind, params string[] names) => (kind + ":" + string.Join('/', names)).ToLowerInvariant();

    private static int? Number(string segment) =>
        int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;

    /// <summary>The path's segments, each percent-decoded (a segment that does not decode kept raw), without a final empty one.</summary>
    private static string[] Segments(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return [.. parts.Select(part =>
        {
            try
            {
                return Uri.UnescapeDataString(part);
            }
            catch (UriFormatException)
            {
                return part;
            }
        })];
    }

    /// <summary>
    /// Check 2 (MEASURED r5 1.4, 0.9): none, 400 <c>VssInvalidApiResourceVersionException</c> in the
    /// fake's words (D370); malformed, that typeKey in the service's words; above 7.2,
    /// <c>VssVersionOutOfRangeException</c>; no <c>-preview</c> on <c>connectionData</c> and the
    /// resource area, or 7.2 elsewhere, <c>VssInvalidPreviewVersionException</c>; a resource version
    /// above 1 on a git route, 404 "Page not found". Returns the version served, which the answer's
    /// <c>Content-Type</c> echoes (a preview without its number served as <c>.1</c>).
    /// </summary>
    private (string? Served, AuthRemoteResponse? Refusal) CheckVersion(AuthRemoteRouteRequest request, Kind kind)
    {
        var raw = QueryValue(request.Query, "api-version") ?? AcceptVersion(request);
        if (raw is null)
        {
            return (null, VersionError("VssInvalidApiResourceVersionException", "The fake of Azure DevOps needs an api-version (D370): the request sent none. Api version string must be in the format: {Major}.{Minor}[-preview[.{ResourceVersion}]]."));
        }

        // Malformed, or a part beyond an int: that typeKey in the service's words (MEASURED r5 1.4).
        var match = VersionPattern.Match(raw);
        var revision = 1;
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || match.Groups[5].Success && !int.TryParse(match.Groups[5].Value, NumberStyles.None, CultureInfo.InvariantCulture, out revision))
        {
            return (null, VersionError("VssInvalidApiResourceVersionException", $"Invalid api version string: \"{raw}\". Api version string must be in the format: {{Major}}.{{Minor}}[-preview[.{{ResourceVersion}}]]."));
        }

        var version = string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");
        if (major > 7 || major == 7 && minor > 2)
        {
            return (null, VersionError("VssVersionOutOfRangeException", OutOfRange(version)));
        }

        var preview = match.Groups[3].Success;
        var previewOnly = kind is Kind.Connection or Kind.Areas;
        if (!preview && (previewOnly || major == 7 && minor == 2))
        {
            return (null, VersionError("VssInvalidPreviewVersionException", $"The requested version \"{version}\" of the resource is under preview. The -preview flag must be supplied in the api-version for such requests. For example: \"{version}-preview\""));
        }

        if (preview && !previewOnly && revision > 1)
        {
            return (null, PageNotFound());
        }

        return (preview ? string.Create(CultureInfo.InvariantCulture, $"{version}-preview.{revision}") : version, null);
    }

    private static string OutOfRange(string version) =>
        $"The requested REST API version of {version} is out of range for this server. The latest REST API version this server supports is 7.2.";

    /// <summary>A version's 400 in r5 1.4's shape (MEASURED; <c>VssInvalidPreviewVersionException</c>'s typeName MEASURED 2026-11-05 at <c>7.1</c>, the other typeNames ASSUMED alike).</summary>
    private AuthRemoteResponse VersionError(string typeKey, string message) =>
        Json(400, ErrorJson(message, $"Microsoft.VisualStudio.Services.WebApi.{typeKey}, Microsoft.VisualStudio.Services.WebApi", typeKey), null);

    /// <summary>The <c>api-version</c> parameter of <c>Accept</c> (READ r5 7: the service takes it there too).</summary>
    private static string? AcceptVersion(AuthRemoteRouteRequest request)
    {
        if (!request.Headers.TryGetValue("Accept", out var accept))
        {
            return null;
        }

        foreach (var part in accept.Split([';', ','], StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("api-version=", StringComparison.OrdinalIgnoreCase))
            {
                return part["api-version=".Length..];
            }
        }

        return null;
    }

    /// <summary>
    /// A token PAT-shaped: the fake's <c>azdo_FAKE-…</c>, or 84 characters with <c>AZDO</c> at 76 to 79
    /// (READ r5 0.6, 5; MEASURED 2026-11-05: a made-up value of that form got the empty 401). Any other
    /// value is no credential (READ g1 G0.2), but on connectionData, where a Basic one gets the
    /// Anonymous identity (MEASURED 2026-11-05 with the empty user; with a user ASSUMED alike).
    /// </summary>
    private static bool IsPatShaped(string token) =>
        token.StartsWith("azdo_FAKE-", StringComparison.Ordinal) || token.Length == 84 && string.CompareOrdinal(token, 76, "AZDO", 0, 4) == 0;

    /// <summary>
    /// Check 4 for a PAT-shaped token: unknown, revoked or another organization's, 401 with an empty
    /// body, no <c>Content-Type</c> and one <c>WWW-Authenticate: Basic realm</c> (MEASURED 2026-11-05 for
    /// an unknown one on connectionData with the empty user, with and without Suppress, D373; with a
    /// user ASSUMED alike, the user ignored, READ g1 G1.0; revoked READ g1 G2, another organization's
    /// ASSUMED); expired, 401 <c>AccessCheckException</c> (READ g1 G2, kept: 15.7).
    /// </summary>
    private (FakeAzureToken? Known, AuthRemoteResponse? Refused) TokenOf(string token, FakeAzureOrganization organization, string served)
    {
        lock (Gate)
        {
            if (!_azureTokens.TryGetValue(token, out var known) || TokenLoginLocked(token) is null || known.Organization != organization)
            {
                return (null, InvalidTokenAnswer());
            }

            return known.Expired ? (null, ExpiredAnswer(served)) : (known, null);
        }
    }

    /// <summary>Whether no credential may read the route: a public project's reads (MEASURED r5 2.1, 3.1); never the organization's list (MEASURED r5 2.1: 302) or a write.</summary>
    private bool AnonymousMayRead(Route route, FakeAzureOrganization organization)
    {
        if (route.Project is null || route.Kind is not (Kind.Repos or Kind.Repo or Kind.Pulls or Kind.Pull or Kind.Refs))
        {
            return false;
        }

        lock (Gate)
        {
            return ProjectLocked(organization, route.Project) is { IsPublic: true };
        }
    }

    /// <summary>Whether the caller sees a project: public, or a signed-in user it has no <see cref="FakeAzureProject.Members"/> for, or one of them.</summary>
    private static bool Readable(FakeAzureProject project, FakeAzureToken? caller) =>
        project.IsPublic || caller is not null && (project.Members.Count == 0 || project.Members.Contains(caller.User));

    /// <summary>Check 5's project and repository: hidden or missing, 404 TF401019 (MEASURED shape; TF200016 for a project's list, ASSUMED).</summary>
    private (FakeAzureProject? Project, FakeAzureRepository? Repository, AuthRemoteResponse? Refusal) Resolve(Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served)
    {
        lock (Gate)
        {
            var project = ProjectLocked(organization, route.Project!);
            if (project is null || !Readable(project, caller))
            {
                return (null, null, route.Kind == Kind.Repos ? ProjectMissing(route.Project!, served) : RepositoryMissing(route.Repository ?? route.Project!, served));
            }

            if (route.Repository is null)
            {
                return (project, null, null);
            }

            var repository = RepositoryLocked(project, route.Repository);
            return repository is null ? (project, null, RepositoryMissing(route.Repository, served)) : (project, repository, null);
        }
    }

    /// <summary>
    /// A write's check 5: a read-only token, 401 TF400813 naming the user (ASSUMED g1 G2); a user the
    /// repository's <see cref="FakeAzureRepository.Contributors"/> leave out, 403
    /// <c>GitNeedsPermissionException</c> TF401027 (READ text; the 403 ASSUMED).
    /// </summary>
    private AuthRemoteResponse? RefuseWrite(FakeAzureRepository repository, FakeAzureToken caller, string permission, string served)
    {
        if (!caller.CodeWrite)
        {
            return Json(401, ErrorJson(NotAuthorized(caller.User.Id.ToString("D", CultureInfo.InvariantCulture)), UnauthorizedTypeName, "UnauthorizedRequestException"), served);
        }

        var contributors = repository.Contributors;
        return contributors.Count > 0 && !contributors.Contains(caller.User)
            ? Json(403, ErrorJson(PermissionText(permission, caller.User.DisplayName), "Microsoft.TeamFoundation.Git.Server.GitNeedsPermissionException, Microsoft.TeamFoundation.Git.Server", "GitNeedsPermissionException"), served)
            : null;
    }

    /// <summary><c>repos:</c> (MEASURED r5 2.1): the organization's readable projects' repositories or one project's, in creation order, unpaged (<c>$top</c> ignored).</summary>
    private AuthRemoteResponse Repositories(Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served)
    {
        List<FakeAzureRepository> listed;
        if (route.Project is not null)
        {
            var (project, _, refusal) = Resolve(route, organization, caller, served);
            if (refusal is not null)
            {
                return refusal;
            }

            lock (Gate)
            {
                listed = [.. _repositories.Where(repository => repository.Project == project)];
            }
        }
        else
        {
            lock (Gate)
            {
                listed = [.. _repositories.Where(repository => repository.Project.Organization == organization && Readable(repository.Project, caller))];
            }
        }

        var facts = listed.Select(FolderFacts).ToList();
        lock (Gate)
        {
            return Json(200, ListJson(listed.Select((repository, index) => (JsonNode)RepositoryJson(repository, facts[index], single: false))), served);
        }
    }

    /// <summary><c>repo:</c> (MEASURED r5 2.1): one repository by name or GUID, with <c>_links</c>, a fork's <c>parentRepository</c>.</summary>
    private AuthRemoteResponse SingleRepository(Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served)
    {
        var (_, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        var facts = FolderFacts(repository!);
        lock (Gate)
        {
            return Json(200, RepositoryJson(repository!, facts, single: true), served);
        }
    }

    /// <summary>
    /// <c>pulls:</c> after the ref sync: <c>searchCriteria.status</c> (<c>active</c> by default,
    /// <c>abandoned</c>, <c>completed</c>, <c>all</c>; READ; any other value <c>active</c>, ASSUMED),
    /// <c>$skip</c>, <c>$top</c> (absent 101, READ community, whatever <see cref="FakeHostingService.MaxPerPage"/> is;
    /// one sent capped at <see cref="FakeHostingService.MaxPerPage"/>, ASSUMED); id descending and <c>count</c> the page's (MEASURED r5 3.1).
    /// </summary>
    private async Task<AuthRemoteResponse> PullsAsync(FakeServiceCall call, Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served, CancellationToken ct)
    {
        var (_, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        await SyncIfPullsAsync(repository!, ct).ConfigureAwait(false);
        var query = call.Request.Query;
        var status = QueryValue(query, "searchCriteria.status")?.ToLowerInvariant();
        var skip = Math.Max(0, IntOf(QueryValue(query, "$skip")) ?? 0);
        // MaxPerPage caps a $top the request sends; with none the page is 101 rows whatever MaxPerPage
        // is, so that a client that forgets $top and expects pages of 100 is caught (design 3.3).
        var top = IntOf(QueryValue(query, "$top")) is { } asked and >= 0 ? Math.Min(asked, MaxPerPage) : DefaultTop;
        List<FakeAzurePullRequest> page;
        lock (Gate)
        {
            page = [.. _pullRequests
                .Where(pull => pull.Repository == repository && status switch
                {
                    "all" => true,
                    "abandoned" => pull.Status == FakeAzurePullRequestStatus.Abandoned,
                    "completed" => pull.Status == FakeAzurePullRequestStatus.Completed,
                    _ => pull.IsActive,
                })
                .OrderByDescending(pull => pull.Id)
                .Skip(skip)
                .Take(top)];
        }

        var forkFacts = page.Select(pull => pull.Fork is null ? default : FolderFacts(pull.Fork)).ToList();
        lock (Gate)
        {
            return Json(200, ListJson(page.Select((pull, index) => (JsonNode)PullRequestJsonLocked(pull, caller is null, single: false, forkFacts[index]))), served);
        }
    }

    /// <summary><c>pull:</c> after the ref sync: by repository, or by project alone (MEASURED r5 3.1); 404 TF401180 (MEASURED).</summary>
    private async Task<AuthRemoteResponse> PullAsync(Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served, CancellationToken ct)
    {
        var (project, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        var pull = PullOf(project!, repository, route.Number);
        if (pull is null)
        {
            return PullMissing(served);
        }

        await SyncIfPullsAsync(pull.Repository, ct).ConfigureAwait(false);
        return SinglePull(pull, caller, served, 200);
    }

    /// <summary>
    /// <c>create:</c> (design 3.3; r5 4, g1 G2): no title, 400 <c>InvalidArgumentValueException</c>
    /// (<see cref="TitleMissing"/>, MEASURED 2026-11-05); a missing branch, 400
    /// <c>GitPullRequestCannotBeActivated</c> TF401398 (MEASURED 2026-11-05; a ref outside
    /// <c>refs/heads/</c> READ); an active one with that source and target, 409
    /// <c>GitPullRequestExistsException</c> TF401179 (MEASURED 2026-11-05); else
    /// <see cref="CreateStatus"/> (201 MEASURED 2026-11-05), <c>status: active</c>, <c>isDraft</c> as
    /// sent, the token's user as <c>createdBy</c> (its <c>id</c> the account's, MEASURED 2026-11-05),
    /// <c>mergeStatus: queued</c> (READ; <c>succeeded</c> at the read after it MEASURED 2026-11-05),
    /// the answer with <c>labels</c> (MEASURED 2026-11-05 on the create with nothing ahead; ASSUMED
    /// alike for a create with commits ahead); one with nothing ahead is made too (MEASURED 2026-11-05). A
    /// fork's source is <c>forkSource.repository.id</c> (READ g1 G1.2, kept: D375).
    /// </summary>
    private async Task<AuthRemoteResponse> CreateAsync(FakeServiceCall call, Route route, FakeAzureOrganization organization, FakeAzureToken caller, string served, CancellationToken ct)
    {
        var (_, target, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        if (RefuseWrite(target!, caller, "PullRequestContribute", served) is { } refused)
        {
            return refused;
        }

        if (ObjectOf(call.Request.Body) is not { } body)
        {
            return InvalidArgument("The fake of Azure DevOps could not read the pull request as a JSON object.", served);
        }

        var title = StringOf(body, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return TitleMissing(served);
        }

        FakeAzureRepository? fork = null;
        if (body["forkSource"]?["repository"]?["id"] is JsonValue forkValue && forkValue.TryGetValue<string>(out var forkId))
        {
            lock (Gate)
            {
                fork = _repositories.Find(repository => repository.Project.Organization == organization && Guid.TryParse(forkId, out var id) && repository.Id == id);
            }

            if (fork is null)
            {
                return RepositoryMissing(forkId, served);
            }

            fork = fork == target ? null : fork;
        }

        var sourceRef = StringOf(body, "sourceRefName");
        var targetRef = StringOf(body, "targetRefName");
        const string Heads = "refs/heads/";
        if (sourceRef is null || targetRef is null || !sourceRef.StartsWith(Heads, StringComparison.Ordinal) || !targetRef.StartsWith(Heads, StringComparison.Ordinal))
        {
            return CannotBeActivated(served);
        }

        var gate = LockOf(target!.FolderName);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var refs = await SyncLockedAsync(target, ct).ConfigureAwait(false);
            var sourceRefs = fork is null ? refs : await ReadRefsAsync(fork.BarePath, ct, "refs/heads/").ConfigureAwait(false);
            if (!refs.TryGetValue(targetRef, out var targetTip) || !sourceRefs.TryGetValue(sourceRef, out var sourceTip))
            {
                return CannotBeActivated(served);
            }

            var (source, into) = (sourceRef[Heads.Length..], targetRef[Heads.Length..]);
            FakeAzurePullRequest pull;
            lock (Gate)
            {
                if (_pullRequests.Exists(other => other.Repository == target && other.IsActive && other.Fork == fork && other.SourceBranch == source && other.TargetBranch == into))
                {
                    // MEASURED 2026-11-05: status, typeName, typeKey and words.
                    return Json(409, ErrorJson("TF401179: An active pull request for the source and target branch already exists.", "Microsoft.TeamFoundation.Git.Server.GitPullRequestExistsException, Microsoft.TeamFoundation.Git.Server", "GitPullRequestExistsException"), served);
                }

                var number = organization.NextPullRequestId++;
                var isDraft = body["isDraft"] is JsonValue draft && draft.TryGetValue<bool>(out var flag) && flag;
                pull = new FakeAzurePullRequest(number, target, fork, title, StringOf(body, "description"), caller.User, source, into, isDraft, MadeUpLocked(5))
                {
                    LastMergeSourceCommit = sourceTip,
                    LastMergeTargetCommit = targetTip,
                };
                _pullRequests.Add(pull);
            }

            return SinglePull(pull, caller, served, CreateStatus, PullAnswer.Created);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// <c>update:</c> (READ g1 G1.1): <c>status</c> (<c>active</c>, <c>abandoned</c>, <c>completed</c>),
    /// <c>title</c> and <c>description</c>; any other member, or another value, 400
    /// <c>InvalidArgumentValueException</c> (READ; the status ASSUMED). The abandon's 200 (MEASURED
    /// 2026-11-05) has no <c>mergeStatus</c> or <c>lastMergeCommit</c> (MEASURED 2026-11-05; a
    /// completion's answered so too, ASSUMED). The merge ref of one abandoned or completed goes at the
    /// next sync (MEASURED 2026-11-05: gone at the first read after an abandon).
    /// </summary>
    private AuthRemoteResponse Update(FakeServiceCall call, Route route, FakeAzureOrganization organization, FakeAzureToken caller, string served)
    {
        var (project, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        var pull = PullOf(project!, repository, route.Number);
        if (pull is null)
        {
            return PullMissing(served);
        }

        if (RefuseWrite(repository!, caller, "PullRequestContribute", served) is { } refused)
        {
            return refused;
        }

        if (ObjectOf(call.Request.Body) is not { } body)
        {
            return InvalidArgument("The fake of Azure DevOps could not read the update as a JSON object.", served);
        }

        FakeAzurePullRequestStatus? status = null;
        string? title = null;
        (bool Given, string? Value) description = default;
        foreach (var (name, value) in body)
        {
            var text = value is JsonValue scalar && scalar.TryGetValue<string>(out var read) ? read : null;
            switch (name.ToLowerInvariant())
            {
                case "status" when text is not null && Enum.TryParse<FakeAzurePullRequestStatus>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(text, out _):
                    status = parsed;
                    break;
                case "title" when !string.IsNullOrWhiteSpace(text):
                    title = text;
                    break;
                case "description" when value is null || text is not null:
                    description = (true, text);
                    break;
                default:
                    return InvalidArgument($"The fake of Azure DevOps updates a pull request's status, title and description only, each with a value it takes; the request sent '{name}'.", served);
            }
        }

        lock (Gate)
        {
            if (status is { } newStatus)
            {
                pull.Status = newStatus;
            }

            if (title is not null)
            {
                pull.Title = title;
            }

            if (description.Given)
            {
                pull.Description = description.Value;
            }
        }

        return SinglePull(pull, caller, served, 200, pull.IsActive ? PullAnswer.Read : PullAnswer.Closed);
    }

    /// <summary><c>GET …/refs?filter=</c> after the ref sync (MEASURED r5 3.3): the refs under <c>refs/&lt;filter&gt;</c>, by name.</summary>
    private async Task<AuthRemoteResponse> RefsAsync(FakeServiceCall call, Route route, FakeAzureOrganization organization, FakeAzureToken? caller, string served, CancellationToken ct)
    {
        var (_, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        await SyncIfPullsAsync(repository!, ct).ConfigureAwait(false);
        var filter = QueryValue(call.Request.Query, "filter") ?? string.Empty;
        var prefix = filter.StartsWith("refs/", StringComparison.Ordinal) ? filter : "refs/" + filter;
        var refs = await ReadRefsAsync(repository!.BarePath, ct, "refs/heads/", "refs/tags/", "refs/pull/").ConfigureAwait(false);
        var rows = refs.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(pair => pair.Key, StringComparer.Ordinal);
        return Json(200, ListJson(rows.Select(pair => (JsonNode)RefJson(repository, pair.Key, pair.Value))), served);
    }

    /// <summary>
    /// <c>POST …/refs</c>: deletes (<c>newObjectId</c> forty zeros) and creates (<c>oldObjectId</c> forty
    /// zeros), with r5 4's results at 200 (<see cref="UpdateRefsAsync"/>); an update that moves a ref
    /// from one commit to another, 400 in the fake's words (no path of the app's or the live command's
    /// sends one, D370). Each update's members
    /// <c>repositoryId, name, oldObjectId, newObjectId, isLocked, updateStatus, success</c> (MEASURED
    /// 2026-11-05 on the clean-up's delete of two branches: 200, <c>succeeded</c> and <c>success: true</c>;
    /// r5 READ <c>customMessage</c> and <c>rejectedBy</c>, which the service did not send); a create's
    /// 200 with <c>succeeded</c> and <c>success: true</c> MEASURED 2026-11-05 (the live command's
    /// <c>-even</c> branch at main's tip), its members ASSUMED alike (the run noted the outcome alone).
    /// </summary>
    private async Task<AuthRemoteResponse> RefUpdatesAsync(FakeServiceCall call, Route route, FakeAzureOrganization organization, FakeAzureToken caller, string served, CancellationToken ct)
    {
        var (_, repository, refusal) = Resolve(route, organization, caller, served);
        if (refusal is not null)
        {
            return refusal;
        }

        if (RefuseWrite(repository!, caller, "GenericContribute", served) is { } refused)
        {
            return refused;
        }

        JsonArray? updates;
        try
        {
            updates = JsonNode.Parse(call.Request.Body) as JsonArray;
        }
        catch (JsonException)
        {
            updates = null;
        }

        var requested = new List<(string Name, string Old, string New)>();
        foreach (var update in updates ?? [])
        {
            if (update is not JsonObject row || StringOf(row, "name") is not { } name || StringOf(row, "oldObjectId") is not { } old || StringOf(row, "newObjectId") is not { } @new
                || (old == ZeroId) == (@new == ZeroId))
            {
                return InvalidArgument("The fake of Azure DevOps deletes and creates refs only: each update needs a name, its oldObjectId and its newObjectId, one of them forty zeros.", served);
            }

            requested.Add((name, old, @new));
        }

        if (updates is null || requested.Count == 0)
        {
            return InvalidArgument("The fake of Azure DevOps takes a JSON array of ref updates.", served);
        }

        var results = await UpdateRefsAsync(repository!, requested, ct).ConfigureAwait(false);
        return Json(200, ListJson(results.Select(result => (JsonNode)new JsonObject
        {
            ["repositoryId"] = repository!.Id.ToString("D", CultureInfo.InvariantCulture),
            ["name"] = result.Name,
            ["oldObjectId"] = result.Old,
            ["newObjectId"] = result.New,
            ["isLocked"] = false,
            ["updateStatus"] = result.Status,
            ["success"] = result.Status == "succeeded",
        })), served);
    }

    private FakeAzurePullRequest? PullOf(FakeAzureProject project, FakeAzureRepository? repository, int number)
    {
        lock (Gate)
        {
            return _pullRequests.Find(pull => pull.Id == number && (repository is null ? pull.Repository.Project == project : pull.Repository == repository));
        }
    }

    private AuthRemoteResponse SinglePull(FakeAzurePullRequest pull, FakeAzureToken? caller, string served, int status, PullAnswer answer = PullAnswer.Read)
    {
        var forkFacts = pull.Fork is null ? default : FolderFacts(pull.Fork);
        var facts = FolderFacts(pull.Repository);
        lock (Gate)
        {
            return Json(status, PullRequestJsonLocked(pull, caller is null, single: true, forkFacts, facts, answer), served);
        }
    }

    private async Task SyncIfPullsAsync(FakeAzureRepository repository, CancellationToken ct)
    {
        bool hasPulls;
        lock (Gate)
        {
            hasPulls = _pullRequests.Exists(pull => pull.Repository == repository);
        }

        if (hasPulls)
        {
            await SyncRefsAsync(repository, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The faults of design 3.4 answered at check 3; see <see cref="FakeAzureDevOpsFault"/>.</summary>
    private AuthRemoteResponse FaultAnswer(FakeServiceCall call, Route route, FakeAzureDevOpsFault fault, string? served)
    {
        if (fault == FakeAzureDevOpsFault.InvalidToken)
        {
            return InvalidTokenAnswer();
        }

        if (fault == FakeAzureDevOpsFault.Unauthorized)
        {
            string id;
            lock (Gate)
            {
                id = call.Token is { } token && _azureTokens.TryGetValue(token, out var known) ? known.User.Id.ToString("D", CultureInfo.InvariantCulture) : AnonymousId;
            }

            return Json(401, ErrorJson(NotAuthorized(id), UnauthorizedTypeName, "UnauthorizedRequestException"), served, Challenges());
        }

        if (fault == FakeAzureDevOpsFault.Expired)
        {
            return ExpiredAnswer(served);
        }

        if (fault == FakeAzureDevOpsFault.SignInRedirect)
        {
            var location = $"http://{call.Request.Host}/_signin?realm={Uri.EscapeDataString(call.Request.Host)}&reply_to={Uri.EscapeDataString(RequestUrl(call.Request))}&redirect=1&hid={InstanceId}";
            return Text(302, "text/html; charset=utf-8", ObjectMoved(location), [KeyValuePair.Create("Location", location)]);
        }

        if (fault == FakeAzureDevOpsFault.NonAuthoritative)
        {
            return Text(203, "text/html; charset=utf-8", "<!DOCTYPE html><html><head><title>Sign in to your account</title></head><body></body></html>");
        }

        if (fault == FakeAzureDevOpsFault.Throttled)
        {
            // r5 7's words (READ), its placeholders <resource name> and <namespace ID> filled in with
            // Core and User, without quotes (the names ASSUMED, as the typeKey).
            return Json(429, ErrorJson("TF400733: The request has been canceled: Request was blocked due to exceeding usage of resource Core in namespace User.", "Microsoft.TeamFoundation.Framework.Server.RequestBlockedException, Microsoft.TeamFoundation.Framework.Server", "RequestBlockedException"), served, RateLimitHeaders(retryAfter: "30", remaining: "0", delay: null));
        }

        if (fault == FakeAzureDevOpsFault.VersionRefused)
        {
            var version = served is null ? "7.1" : served.Split('-')[0];
            return VersionError("VssVersionOutOfRangeException", OutOfRange(version));
        }

        if (fault == FakeAzureDevOpsFault.NotFound)
        {
            return route.Kind is Kind.Pull or Kind.Update ? PullMissing(served) : RepositoryMissing(route.Repository ?? route.Project ?? route.Organization ?? string.Empty, served);
        }

        if (fault == FakeAzureDevOpsFault.RouteMissing)
        {
            return PageNotFound();
        }

        if (fault == FakeAzureDevOpsFault.OrganizationMissing)
        {
            return OrganizationNotFound();
        }

        if (fault == FakeAzureDevOpsFault.ServerError)
        {
            return Json(500, ErrorJson(string.Create(CultureInfo.InvariantCulture, $"TF400898: An Internal Error Occurred. Activity Id: 7b0aac71-0000-4a00-8000-{Interlocked.Read(ref _activity) + 1:x12}."), "Microsoft.TeamFoundation.Framework.Server.UnexpectedServerException, Microsoft.TeamFoundation.Framework.Server", "UnexpectedServerException"), served);
        }

        if (fault == FakeAzureDevOpsFault.BadGateway)
        {
            return Text(503, "text/html; charset=utf-8", "<!DOCTYPE html><html><head><title>Service Unavailable</title></head><body><h1>Service Unavailable</h1></body></html>");
        }

        // Malformed.
        var cut = new AuthRemoteResponse(200, "OK", [KeyValuePair.Create("Content-Type", "application/json; charset=utf-8" + (served is null ? string.Empty : "; api-version=" + served))], Utf8("{\"count\":1,\"value\":[{\"id\":"));
        return cut;
    }

    /// <summary>The late faults on the normal answer: <see cref="FakeAzureDevOpsFault.Delayed"/>'s headers, <see cref="FakeAzureDevOpsFault.CreatedThenDropped"/>'s drop.</summary>
    private static AuthRemoteResponse Finish(FakeAzureDevOpsFault? fault, AuthRemoteResponse answer)
    {
        if (fault == FakeAzureDevOpsFault.Delayed)
        {
            return answer with { Headers = [.. answer.Headers, .. RateLimitHeaders(retryAfter: "1", remaining: "12", delay: "0.500")] };
        }

        return fault == FakeAzureDevOpsFault.CreatedThenDropped ? answer with { Drop = true } : answer;
    }

    /// <summary><c>Retry-After</c> and the <c>X-RateLimit-*</c> headers (READ r5 7), <c>X-RateLimit-Delay</c> only while delaying.</summary>
    private static List<KeyValuePair<string, string>> RateLimitHeaders(string retryAfter, string remaining, string? delay)
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("Retry-After", retryAfter),
            KeyValuePair.Create("X-RateLimit-Resource", "Core"),
        };
        if (delay is not null)
        {
            headers.Add(KeyValuePair.Create("X-RateLimit-Delay", delay));
        }

        headers.Add(KeyValuePair.Create("X-RateLimit-Limit", "200"));
        headers.Add(KeyValuePair.Create("X-RateLimit-Remaining", remaining));
        headers.Add(KeyValuePair.Create("X-RateLimit-Reset", AppClocks.HarnessTime.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        return headers;
    }

    /// <summary>
    /// No credential where one is needed (MEASURED r5 1.3): 302 to the identities' sign-in, or with
    /// <c>X-TFS-FedAuthRedirect: Suppress</c> 401 <c>UnauthorizedRequestException</c> TF400813 naming
    /// the anonymous user, with the three challenges and <c>X-TFS-FedAuthRedirect</c>.
    /// </summary>
    private AuthRemoteResponse SignIn(AuthRemoteRouteRequest request, string? served)
    {
        var location = $"{IdentityHost}_signin?realm=dev.azure.com&reply_to={Uri.EscapeDataString(RequestUrl(request))}&redirect=1&hid={InstanceId}&context=fake";
        if (request.Headers.TryGetValue("X-TFS-FedAuthRedirect", out var suppress) && string.Equals(suppress.Trim(), "Suppress", StringComparison.OrdinalIgnoreCase))
        {
            return Json(401, ErrorJson(NotAuthorized(AnonymousId), UnauthorizedTypeName, "UnauthorizedRequestException"), served, [.. Challenges(), KeyValuePair.Create("X-TFS-FedAuthRedirect", location)]);
        }

        return Text(302, "text/html; charset=utf-8", ObjectMoved(location), [KeyValuePair.Create("Location", location), .. Challenges().Take(2)]);
    }

    /// <summary>The three challenges of the API's 401 (MEASURED r5 1.3; the tenant and region the fake's own).</summary>
    private static List<KeyValuePair<string, string>> Challenges() =>
    [
        KeyValuePair.Create("WWW-Authenticate", $"Bearer authorization_uri=https://login.microsoftonline.com/{TenantId}"),
        KeyValuePair.Create("WWW-Authenticate", $"Basic realm=\"{TfsRealm}\""),
        KeyValuePair.Create("WWW-Authenticate", "TFS-Federated"),
    ];

    private static string ObjectMoved(string location) =>
        $"<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href=\"{System.Net.WebUtility.HtmlEncode(location)}\">here</a>.</h2>\r\n</body></html>\r\n";

    private static string RequestUrl(AuthRemoteRouteRequest request) =>
        $"http://{request.Host}{request.Path}" + (request.Query.Length > 0 ? "?" + request.Query : string.Empty);

    /// <summary>A PAT-shaped token unknown, revoked or another organization's: 401, an empty body and one <c>WWW-Authenticate: Basic realm</c> (MEASURED 2026-11-05 for an unknown one; the realm <see cref="TokenRealm"/>, a made-up host of the measured form).</summary>
    private AuthRemoteResponse InvalidTokenAnswer()
    {
        var headers = new List<KeyValuePair<string, string>>();
        AddActivity(headers);
        headers.Add(KeyValuePair.Create("WWW-Authenticate", $"Basic realm=\"{TokenRealm}\""));
        return new AuthRemoteResponse(401, "Unauthorized", headers, []);
    }

    /// <summary>An expired token: 401 <c>AccessCheckException</c> with its <c>customProperties</c>, led by a BOM (READ g1 G2; on dev.azure.com ASSUMED).</summary>
    private AuthRemoteResponse ExpiredAnswer(string? served) =>
        Json(401, ErrorJson(
            "Access Denied: The Personal Access Token used has expired.",
            "Microsoft.VisualStudio.Services.Security.AccessCheckException, Microsoft.VisualStudio.Services.WebApi",
            "AccessCheckException",
            new JsonObject
            {
                ["Descriptor"] = null,
                ["IdentityDisplayName"] = null,
                ["Token"] = null,
                ["RequestedPermissions"] = 0,
                ["NamespaceId"] = "00000000-0000-0000-0000-000000000000",
            }), served, bom: true);

    /// <summary>An unknown organization: 404 <c>text/plain</c> "The resource cannot be found.", <c>X-TFS-ServiceError</c>, no <c>ActivityId</c> (MEASURED r5 1.3 without a credential; MEASURED 2026-11-05 on connectionData with a token too, D374's made-up organization).</summary>
    private AuthRemoteResponse OrganizationNotFound() =>
        Text(404, "text/plain; charset=utf-8", "The resource cannot be found.", [KeyValuePair.Create("X-TFS-ServiceError", "The%20resource%20cannot%20be%20found.")], activity: false);

    /// <summary>The resource area's unknown organization: 404 TF400677 in its own shape (MEASURED g1 G4.2).</summary>
    private AuthRemoteResponse HostMissing(string name) =>
        Json(404, new JsonObject { ["count"] = 1, ["value"] = new JsonObject { ["Message"] = $"TF400677: Host {name} does not exist." } }, null);

    /// <summary>An unknown route or resource version: 404 <c>text/plain</c> "Page not found" (MEASURED r5 1.4).</summary>
    private AuthRemoteResponse PageNotFound() => Text(404, "text/plain; charset=utf-8", "Page not found");

    /// <summary>A web page: 200 <c>text/html</c> for any path (MEASURED g1 G4.3: the pages are a single-page app).</summary>
    private AuthRemoteResponse Page() =>
        Text(200, "text/html; charset=utf-8", "<!DOCTYPE html><html><head><title>Azure DevOps</title></head><body></body></html>");

    /// <summary>TF401019 (MEASURED r5 1.4; the typeName ASSUMED).</summary>
    private AuthRemoteResponse RepositoryMissing(string name, string? served) =>
        Json(404, ErrorJson($"TF401019: The Git repository with name or identifier {name} does not exist or you do not have permissions for the operation you are attempting.", "Microsoft.TeamFoundation.Git.Server.GitRepositoryNotFoundException, Microsoft.TeamFoundation.Git.Server", "GitRepositoryNotFoundException"), served);

    /// <summary>TF401180 (MEASURED r5 1.4; the typeName ASSUMED).</summary>
    private AuthRemoteResponse PullMissing(string? served) =>
        Json(404, ErrorJson("TF401180: The requested pull request was not found.", "Microsoft.TeamFoundation.Git.Server.GitPullRequestNotFoundException, Microsoft.TeamFoundation.Git.Server", "GitPullRequestNotFoundException"), served);

    /// <summary>An unknown or hidden project's list: 404 TF200016 (ASSUMED, words and shape).</summary>
    private AuthRemoteResponse ProjectMissing(string name, string served) =>
        Json(404, ErrorJson($"TF200016: The following project does not exist: {name}. Verify that the name of the project is correct and that the project exists on the specified Azure DevOps Server.", "Microsoft.TeamFoundation.Core.WebApi.ProjectDoesNotExistWithNameException, Microsoft.TeamFoundation.Core.WebApi", "ProjectDoesNotExistWithNameException"), served);

    /// <summary>TF401398, no final period (READ g1 G2; MEASURED 2026-11-05, a create from a missing branch: status, typeName, typeKey and words).</summary>
    private AuthRemoteResponse CannotBeActivated(string served) =>
        Json(400, ErrorJson("TF401398: The pull request cannot be activated because the source and/or the target branch no longer exists, or the requested refs are not branches", "Microsoft.TeamFoundation.Git.Server.GitPullRequestCannotBeActivated, Microsoft.TeamFoundation.Git.Server", "GitPullRequestCannotBeActivated"), served);

    /// <summary>400 <c>InvalidArgumentValueException</c> (READ g1 G1.1; the status and typeName MEASURED 2026-11-05 on a create; the fake's own words but for <see cref="TitleMissing"/>, D370).</summary>
    private AuthRemoteResponse InvalidArgument(string message, string served, int eventId = 3000) =>
        Json(400, ErrorJson(message, "Microsoft.TeamFoundation.SourceControl.WebServer.InvalidArgumentValueException, Microsoft.TeamFoundation.SourceControl.WebServer", "InvalidArgumentValueException", eventId: eventId), served);

    /// <summary>
    /// A create with an empty title: 400 <c>InvalidArgumentValueException</c>, "Invalid argument
    /// value.\r\nParameter name: Invalid pull request title.", <c>eventId</c> 0 (MEASURED 2026-11-05;
    /// a missing or blank title answered so too, ASSUMED).
    /// </summary>
    private AuthRemoteResponse TitleMissing(string served) =>
        InvalidArgument("Invalid argument value.\r\nParameter name: Invalid pull request title.", served, eventId: 0);

    private const string UnauthorizedTypeName = "Microsoft.TeamFoundation.Framework.Server.UnauthorizedRequestException, Microsoft.TeamFoundation.Framework.Server";

    /// <summary>TF400813 (MEASURED r5 1.3 for the anonymous user; READ g1 G2 for a member).</summary>
    private static string NotAuthorized(string id) => $"TF400813: The user '{id}' is not authorized to access this resource.";

    /// <summary>TF401027 (READ g1 G2: "Details: identity '…', scope 'repository'."; the identity the user's display name, ASSUMED).</summary>
    private static string PermissionText(string permission, string identity) =>
        $"TF401027: You need the Git '{permission}' permission to perform this action. Details: identity '{identity}', scope 'repository'.";

    private static JsonObject? ObjectOf(byte[] body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringOf(JsonObject json, string name) =>
        json[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? IntOf(string? text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>The first value of the query's parameter <paramref name="name"/> (names decoded, <c>+</c> a space); null when absent.</summary>
    private static string? QueryValue(string query, string name)
    {
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            var key = Decode(equals < 0 ? part : part[..equals]);
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return equals < 0 ? string.Empty : Decode(part[(equals + 1)..]);
            }
        }

        return null;
    }

    private static string Decode(string text)
    {
        try
        {
            return Uri.UnescapeDataString(text.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return text;
        }
    }
}
