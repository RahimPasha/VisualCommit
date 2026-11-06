// What the live command settles (docs/phase-7b-design.md 4.7, D342, D375). WP-LIVE's stage 1 ran
// with D364's fine-grained token (D375) and WP-LF copied it: MEASURED 2026-11-06 (Check_1b, Check_2,
// Check_2b): `user` (200, its 41 members in order), `self`'s check 5 403 and its text, a made-up
// `glpat-` value's 401 on `GET user` (D373), the projects with `permissions`, the merge requests'
// 200, `ratelimit-*` on every answer (`burst_traffic_per_user_free_plan`, 100, with a token) and
// across a 304, the create (201, `Draft:`, `references`), the 409, the missing-branch 400 and
// nothing ahead (201 without `first_contribution`), a single one's members in order, the clean-up's
// PUT, DELETE and branch delete, git through the app's helper (Check_2's clone and pushes); MEASURED
// 2026-11-05 (the first run): `GET projects` without Project: Read among the user permissions is check
// 5's 403 naming `user` (D385). Kept as marked: a legacy token's sign-in (`user`, `self`) and check
// 4's 403 with its `scope` lists, on GitLab's docs and source (READ, the lists ASSUMED; D375); a
// selected-projects token's list (ASSUMED, as 7.5) and git's 403s (READ g2 G3, as 4.5 marks them),
// which need a token D364 does not make; the expired and revoked 401s (READ, 15.7); a project route
// with Project: Read among the user permissions alone (ASSUMED: the project permission's 403). A
// list's rows, a fork's among them, are r6 4.1's captured ones (MEASURED there, the same members in
// the same order; the sandbox had no open one to list on 2026-11-06). Kept ASSUMED: the moved
// project's query order, an empty repository's `default_branch`, an unknown `target_project_id`,
// the 5xx bodies, the values of the members the client does not read.
// Kept ASSUMED too (4.3, 4.4 and the build's own choices, D370): create's (a) before (b), its missing
// parameters joined by ", " in Grape's declared order (title, source_branch, target_branch) and (e)'s
// "not a fork" body; a fine-grained token's `scopes: ["granular"]`; the 400 texts for an invalid
// `order_by`, `sort`, `target_project_id` and `state_event`; PUT for the author or a Developer;
// `EndpointRateLimited`'s `RateLimit-Name: projects_api` on every route; `merge_status`
// `can_be_merged` and `detailed_merge_status` `mergeable`; a branch's `protected` for the default
// branch alone; a 204's `Cache-Control: no-cache`.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake's routes (docs/phase-7b-design.md 4.3): which request is which route, the checks in g2
/// G2.1's order, the routes' answers, GitLab's headers (4.4, MEASURED r6 7, 8, 13.1), the faults
/// (<see cref="FakeGitLabFault"/>) and a moved project's redirects (<see cref="Move"/>). Below
/// <c>api/v4/</c> a project's <c>:id</c> is its number or its path escaped (<c>%2F</c> or <c>%2f</c>),
/// without case, a trailing <c>/</c> allowed (MEASURED r6 3.2); bodies are a form or JSON.
/// </summary>
public sealed partial class FakeGitLab
{
    private const string ApiPrefix = "/api/v4/";
    private const int AuthenticatedLimit = 100;
    private const int AnonymousLimit = 500;

    private enum RouteKind
    {
        Miss,
        User,
        Self,
        Projects,
        Project,
        MergeRequests,
        MergeRequest,
        Create,
        Update,
        Delete,
        Branches,
        DeleteBranch,
        Page,
        GitRedirect,
    }

    /// <summary>
    /// Whether the fake answers this request itself, from the request line alone (4.3): <c>api/v4/…</c>,
    /// every GET but git's (a page), and so a <c>.git</c>-less <c>GET &lt;path&gt;/info/refs</c> (4.5).
    /// Git's paths, a segment ending in <c>.git</c> with more after it, are host mode's.
    /// </summary>
    protected override bool Claims(string method, string path) =>
        path.StartsWith(ApiPrefix, StringComparison.Ordinal) || path == "/api/v4" || method == "GET" && !IsGitPath(path);

    /// <summary>The route's key (4.3's table): <c>user</c>, <c>self</c>, <c>projects</c>, <c>project:&lt;path&gt;</c>, <c>mrs:&lt;path&gt;</c>, <c>mr:&lt;path&gt;/&lt;iid&gt;</c>, <c>create:</c>, <c>update:</c>, <c>delete:</c>, <c>branches:</c>, <c>page</c>; null for a route GitLab does not have.</summary>
    protected override string? RouteOf(string method, string path) => Parse(method, path).Key;

    /// <summary>Answers a claimed request: a page, git's redirect, a route miss, or the API's checks then its route (4.3).</summary>
    protected override async Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct)
    {
        var route = Parse(call.Request.Method, call.Request.Path);
        var fault = call.Fault as FakeGitLabFault;
        if (route.Kind == RouteKind.Miss)
        {
            // An unknown route, an unescaped '/' in :id among them: 404 shape E (MEASURED r6 3.2, 13.1).
            return ErrorJson(call, 404, new JsonObject { ["error"] = "404 Not Found" });
        }

        // Check 1: a pending fault, before the credential is read (READ g2 G2.1).
        if (fault is not null && fault != FakeGitLabFault.CreatedThenDropped)
        {
            return FaultAnswer(call, route, fault);
        }

        var response = route.Kind switch
        {
            RouteKind.Page => Page(call),
            RouteKind.GitRedirect => GitRedirect(call),
            _ => await ApiAsync(call, route, ct).ConfigureAwait(false),
        };
        return fault == FakeGitLabFault.CreatedThenDropped ? response with { Drop = true } : response;
    }

    /// <summary>Checks 2 to 6 (4.3, READ g2 G2.1), then the route.</summary>
    private async Task<AuthRemoteResponse> ApiAsync(FakeServiceCall call, Route route, CancellationToken ct)
    {
        var need = NeedOf(route.Kind);

        // Check 2: the token from Authorization: Bearer alone; PRIVATE-TOKEN, private_token and Basic
        // are none (D370: gitlab.com takes the first two, READ r6 1.1).
        var bearer = call.Authorization == FakeAuthorizationForm.Bearer ? call.Token : null;
        GitLabToken? token = null;
        if (bearer is not null)
        {
            bool revoked;
            lock (Gate)
            {
                token = _tokens.GetValueOrDefault(bearer);
                revoked = token is not null && TokenLoginLocked(bearer) is null;
            }

            // Check 3: unknown 401 shape A (MEASURED 2026-11-06, D373's made-up glpat- value; READ g2
            // G2.1); revoked and expired 401 shape F with the challenge (READ g2 G2.2, kept: 15.7).
            if (token is null)
            {
                return Unauthorized(call);
            }

            if (revoked)
            {
                return InvalidToken(call, RevokedText);
            }

            if (token.IsExpired)
            {
                return InvalidToken(call, ExpiredText);
            }

            call.Login = token.Username;
        }

        var login = call.Login;
        FakeGitLabProject? project = null;
        var moved = false;
        bool visible;
        lock (Gate)
        {
            if (route.Id is not null)
            {
                (project, moved) = ResolveLocked(route.Id);
            }

            visible = project is not null && VisibleLocked(project, login);
        }

        // Check 3, no token: 401 shape A without WWW-Authenticate (MEASURED r6 1.2: user, self, and a
        // POST, PUT or DELETE), but the project list and a project's reads go on (MEASURED r6 3.2): a
        // public one answers, an unknown or hidden one is check 6's 404 "404 Project Not Found", as
        // gitlab.com answered anonymously for a missing project, its .git form and its double escape.
        if (token is null && !(route.Kind == RouteKind.Projects || need.Read && route.Id is not null))
        {
            return Unauthorized(call);
        }

        // Check 4: a legacy token without the route's scope (READ g2 G2.3; the lists ASSUMED).
        if (token is { Kind: FakeGitLabTokenKind.Legacy } && need.Scopes is not null && !need.Scopes.Any(token.Has))
        {
            return InsufficientScope(call, need.ScopeList!);
        }

        // Check 5: a fine-grained token without the route's permission: 403 naming the boundary and
        // the label (the user form MEASURED 2026-11-06 on self and 2026-11-05 on GET projects, D385;
        // the project form READ g2 G2.4); a public project's reads pass; a project hidden from the user 404.
        if (token is { Kind: FakeGitLabTokenKind.FineGrained } && !token.Has(need.Label))
        {
            if (FakeGitLabPermissions.IsUserBoundary(need.Label) || route.Id is null)
            {
                return InsufficientGranular(call, need.Label, userBoundary: true);
            }

            if (project is not null && !visible)
            {
                return ErrorJson(call, 404, new JsonObject { ["message"] = "404 Not Found" });
            }

            if (project is not null && !(need.Read && project.Visibility == "public"))
            {
                return InsufficientGranular(call, need.Label, userBoundary: false);
            }
        }

        // Check 6: the route. Unknown or hidden 404 shape A (MEASURED r6 3.2); a moved project's old
        // path: GET 301, any other method 405 (MEASURED r6 3.2; 405 READ).
        if (route.Id is not null)
        {
            if (project is null || !visible)
            {
                return ErrorJson(call, 404, new JsonObject { ["message"] = "404 Project Not Found" });
            }

            if (moved)
            {
                return call.Request.Method == "GET"
                    ? MovedPermanently(call, MovedLocation(project, route.Rest, call.Request.Query))
                    : ErrorJson(call, 405, new JsonObject { ["message"] = "Non GET methods are not allowed for moved projects" });
            }
        }

        return route.Kind switch
        {
            RouteKind.User => CurrentUser(call),
            RouteKind.Self => Self(call, token!),
            RouteKind.Projects => Projects(call),
            RouteKind.Project => ProjectAnswer(call, project!),
            RouteKind.MergeRequests => await MergeRequestsAsync(call, project!, ct).ConfigureAwait(false),
            RouteKind.MergeRequest => await MergeRequestAsync(call, project!, route.Iid!, ct).ConfigureAwait(false),
            RouteKind.Create => await CreateAsync(call, project!, ct).ConfigureAwait(false),
            RouteKind.Update => UpdateMergeRequest(call, project!, route.Iid!),
            RouteKind.Delete => DeleteMergeRequest(call, project!, route.Iid!),
            RouteKind.Branches => await BranchesAnswerAsync(call, project!, ct).ConfigureAwait(false),
            RouteKind.DeleteBranch => await DeleteBranchAnswerAsync(call, project!, route.Branch!, ct).ConfigureAwait(false),
            _ => ErrorJson(call, 404, new JsonObject { ["error"] = "404 Not Found" }),
        };
    }

    /// <summary><c>GET user</c>: the token's user, 200 (MEASURED 2026-11-06 with a fine-grained token; READ r6 1.3 for a legacy one, D375).</summary>
    private AuthRemoteResponse CurrentUser(FakeServiceCall call)
    {
        JsonObject json;
        lock (Gate)
        {
            json = CurrentUserJson(_users[call.Login!]);
        }

        return Json(call, 200, json);
    }

    /// <summary><c>GET personal_access_tokens/self</c>: the token (READ r6 1.4); without Personal Access Token: Read check 5's 403 (MEASURED 2026-11-06).</summary>
    private AuthRemoteResponse Self(FakeServiceCall call, GitLabToken token)
    {
        JsonObject json;
        lock (Gate)
        {
            json = TokenJson(token, _users[token.Username]);
        }

        return Json(call, 200, json);
    }

    /// <summary>
    /// <c>GET projects</c> (4.3): <c>membership=true</c> the user's, through groups too (READ g2 G5.4),
    /// empty without a token (MEASURED r6 1.2); else the public and the user's (and the internal ones
    /// for a signed-in user); <c>archived</c>; <c>simple</c> (always without a token, READ r6 3.1);
    /// <c>order_by</c> (default <c>created_at</c>) and <c>sort</c> (default <c>desc</c>), READ; paged; a
    /// row with <c>permissions</c> (MEASURED 2026-11-06). A fine-grained token needs Project: Read among
    /// its user permissions (<see cref="FakeGitLabPermissions.UserProjectRead"/>), else check 5's 403
    /// naming <c>user</c> (MEASURED 2026-11-05, D385).
    /// Another <c>order_by</c> or <c>sort</c> 400 shape E "&lt;name&gt; does not have a valid value"
    /// (ASSUMED: Grape's text, MEASURED for <c>state</c>, r6 4.1).
    /// </summary>
    private AuthRemoteResponse Projects(FakeServiceCall call)
    {
        var query = new Query(call.Request.Query);
        var orderBy = query.Get("order_by") ?? "created_at";
        if (orderBy is not ("id" or "name" or "path" or "created_at" or "updated_at" or "last_activity_at" or "star_count" or "similarity"))
        {
            return ErrorJson(call, 400, new JsonObject { ["error"] = "order_by does not have a valid value" });
        }

        var sort = query.Get("sort") ?? "desc";
        if (sort is not ("asc" or "desc"))
        {
            return ErrorJson(call, 400, new JsonObject { ["error"] = "sort does not have a valid value" });
        }

        var login = call.Login;
        var membership = query.Get("membership") == "true";
        var archived = query.Get("archived");
        var simple = login is null || query.Get("simple") == "true";
        List<FakeGitLabProject> rows;
        lock (Gate)
        {
            rows = [.. _projects.Where(project =>
                (membership ? AccessLocked(project, login) >= 10 : VisibleLocked(project, login))
                && (archived != "true" || project.IsArchived)
                && (archived != "false" || !project.IsArchived))];
        }

        Func<FakeGitLabProject, IComparable> key = orderBy switch
        {
            // GitLab's name and path are the project's own, its last segment (READ r6 3.1); the fake's name is its path.
            "name" or "path" => project => project.Name.ToLowerInvariant(),
            "created_at" => project => project.CreatedAt,
            "updated_at" or "last_activity_at" => project => project.LastActivityAt,
            _ => project => project.Id,
        };
        rows = sort == "asc"
            ? [.. rows.OrderBy(key).ThenBy(project => project.Id)]
            : [.. rows.OrderByDescending(key).ThenByDescending(project => project.Id)];
        var (slice, headers) = Paged(query, rows, "projects");
        var array = new JsonArray();
        lock (Gate)
        {
            foreach (var project in slice)
            {
                array.Add(ProjectJsonLocked(project, login, simple, IsEmptyRepository(project), DefaultBranchOf(project)));
            }
        }

        return Json(call, 200, array, headers);
    }

    /// <summary><c>GET projects/:id</c>: the project with its access (READ r6 3.1, 3.2).</summary>
    private AuthRemoteResponse ProjectAnswer(FakeServiceCall call, FakeGitLabProject project)
    {
        JsonObject json;
        lock (Gate)
        {
            json = ProjectJsonLocked(project, call.Login, simple: false, IsEmptyRepository(project), DefaultBranchOf(project));
        }

        return Json(call, 200, json);
    }

    /// <summary>
    /// <c>GET projects/:id/merge_requests</c> after the ref sync: <c>state</c> (<c>all</c> by default,
    /// READ; an unknown one 400 shape E, MEASURED r6 4.1), <c>source_branch</c>, <c>target_branch</c>;
    /// <c>created_at</c> descending (MEASURED); paged.
    /// </summary>
    private async Task<AuthRemoteResponse> MergeRequestsAsync(FakeServiceCall call, FakeGitLabProject project, CancellationToken ct)
    {
        var query = new Query(call.Request.Query);
        var state = query.Get("state") ?? "all";
        if (state is not ("opened" or "closed" or "locked" or "merged" or "all"))
        {
            return ErrorJson(call, 400, new JsonObject { ["error"] = "state does not have a valid value" });
        }

        await SyncRefsAsync(project, ct).ConfigureAwait(false);
        var sourceBranch = query.Get("source_branch");
        var targetBranch = query.Get("target_branch");
        List<FakeMergeRequest> rows;
        lock (Gate)
        {
            rows = [.. _mergeRequests
                .Where(request => request.Target == project && !request.Deleted
                    && (state == "all" || request.State == state)
                    && (sourceBranch is null || request.SourceBranch == sourceBranch)
                    && (targetBranch is null || request.TargetBranch == targetBranch))
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id)];
        }

        var (slice, headers) = Paged(query, rows, call.Request.Path[ApiPrefix.Length..].TrimEnd('/'));
        var array = new JsonArray();
        lock (Gate)
        {
            foreach (var request in slice)
            {
                array.Add(MergeRequestJsonLocked(request, single: false));
            }
        }

        return Json(call, 200, array, headers);
    }

    /// <summary><c>GET …/merge_requests/:iid</c>: not digits 400 shape E (MEASURED g2 G4); unknown 404 "404 Not found", its <c>f</c> lower-case (MEASURED g2 G4).</summary>
    private async Task<AuthRemoteResponse> MergeRequestAsync(FakeServiceCall call, FakeGitLabProject project, string iidText, CancellationToken ct)
    {
        if (!TryIid(iidText, out var iid))
        {
            return InvalidIid(call);
        }

        await SyncRefsAsync(project, ct).ConfigureAwait(false);
        JsonObject? json;
        lock (Gate)
        {
            var request = MergeRequestLocked(project, iid);
            json = request is null ? null : MergeRequestJsonLocked(request, single: true);
        }

        return json is null ? MergeRequestNotFound(call) : Json(call, 200, json);
    }

    /// <summary>
    /// <c>POST projects/:id/merge_requests</c>, <c>:id</c> the source (READ g2 G1.1; (a)'s place ASSUMED):
    /// (a) a missing parameter 400 shape E "&lt;name&gt; is missing" (READ g2 G1.1), several joined by
    /// ", " in the order the route declares them, title, source_branch, target_branch (ASSUMED: Grape's
    /// join, r6 5.2's example; lib/api/merge_requests.rb:423-425); (b) below Developer on the source, or
    /// its merge requests off, 403 (READ g2 G1.2); (c) an unknown <c>target_project_id</c> 404 "404 Not
    /// found" (READ, ASSUMED), one that is no number 400 shape E "target_project_id is invalid"
    /// (ASSUMED: Grape's text); (d) a target unreadable or with merge requests off 403; (e) 422 shape B,
    /// the same project and branch, or a source outside the target's fork network (that body ASSUMED);
    /// (f) 409 shape B for an open one with the same projects and branches, naming it <c>!&lt;iid&gt;</c>
    /// for a fork's too (READ: <c>conflicting_mr.to_reference</c> with no <c>from</c>,
    /// app/models/merge_request.rb:1441-1446, :1055-1058); (g) 400 shape C for a missing branch, the
    /// source's first; (h) 201, the iid next in the target, the title and description as sent,
    /// <c>draft</c> from the title, nothing ahead needed, the head ref written. MEASURED 2026-11-06 with
    /// a fine-grained token: (f)'s 409 <c>{"message":["Another open merge request already exists for
    /// this source branch: !&lt;iid&gt;"]}</c>, (g)'s 400 <c>{"message":{"source_branch":["does not exist"]}}</c>,
    /// (h)'s 201 with <c>draft</c> for a <c>Draft:</c> title and <c>references</c>, and a create with
    /// nothing ahead 201 without <c>first_contribution</c>; the rest as marked.
    /// </summary>
    private async Task<AuthRemoteResponse> CreateAsync(FakeServiceCall call, FakeGitLabProject source, CancellationToken ct)
    {
        var parameters = ParametersOf(call.Request);
        var missing = new[] { "title", "source_branch", "target_branch" }.Where(name => string.IsNullOrEmpty(parameters.GetValueOrDefault(name))).ToList();
        if (missing.Count > 0)
        {
            return ErrorJson(call, 400, new JsonObject { ["error"] = string.Join(", ", missing.Select(name => name + " is missing")) });
        }

        var sourceBranch = parameters["source_branch"];
        var targetBranch = parameters["target_branch"];
        var title = parameters["title"];
        var login = call.Login!;
        FakeGitLabProject target;
        lock (Gate)
        {
            if (AccessLocked(source, login) < 30 || !source.MergeRequestsEnabled)
            {
                return Forbidden(call);
            }

            target = source;
            if (parameters.GetValueOrDefault("target_project_id") is { } targetId)
            {
                if (!long.TryParse(targetId, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    return ErrorJson(call, 400, new JsonObject { ["error"] = "target_project_id is invalid" });
                }

                var found = _projects.FirstOrDefault(project => project.Id == id);
                if (found is null)
                {
                    return MergeRequestNotFound(call);
                }

                target = found;
            }

            if (!VisibleLocked(target, login) || !target.MergeRequestsEnabled)
            {
                return Forbidden(call);
            }
        }

        if (source == target && sourceBranch == targetBranch)
        {
            return ErrorJson(call, 422, new JsonObject { ["message"] = new JsonArray("You can't use same project/branch for source and target") });
        }

        if (source != target && !InOneForkNetwork(source, target))
        {
            return ErrorJson(call, 422, new JsonObject { ["message"] = new JsonArray("Source project is not a fork of the target project") });
        }

        var gate = LockOf(target.Folder ?? target.Path);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (target.BarePath is not null)
            {
                await SyncLockedAsync(target, ct).ConfigureAwait(false);
            }

            FakeMergeRequest? open;
            lock (Gate)
            {
                open = _mergeRequests.FirstOrDefault(request => !request.Deleted && request.State == "opened"
                    && request.Source == source && request.SourceBranch == sourceBranch
                    && request.Target == target && request.TargetBranch == targetBranch);
            }

            if (open is not null)
            {
                var reference = "!" + open.Iid.ToString(CultureInfo.InvariantCulture);
                return ErrorJson(call, 409, new JsonObject { ["message"] = new JsonArray("Another open merge request already exists for this source branch: " + reference) });
            }

            var errors = new JsonObject();
            if (!await HasBranchAsync(source, sourceBranch, ct).ConfigureAwait(false))
            {
                errors["source_branch"] = new JsonArray("does not exist");
            }

            if (!await HasBranchAsync(target, targetBranch, ct).ConfigureAwait(false))
            {
                errors["target_branch"] = new JsonArray("does not exist");
            }

            if (errors.Count > 0)
            {
                return ErrorJson(call, 400, new JsonObject { ["message"] = errors });
            }

            FakeMergeRequest created;
            lock (Gate)
            {
                created = AddMergeRequestLocked(target, source, title, login, sourceBranch, targetBranch, parameters.GetValueOrDefault("description"), iid: null);
                created.AllowCollaboration = source != target && parameters.GetValueOrDefault("allow_collaboration") == "true";
            }

            if (target.BarePath is not null)
            {
                await SyncLockedAsync(target, ct).ConfigureAwait(false);
            }

            JsonObject json;
            lock (Gate)
            {
                json = MergeRequestJsonLocked(created, single: true, created: true);
            }

            return Json(call, 201, json);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// <c>PUT …/merge_requests/:iid</c> with <c>state_event</c> <c>close</c> or <c>reopen</c> (READ g2 G1.5):
    /// 200 with the state (the close's 200 MEASURED 2026-11-06, stage 1's clean-up; reopen READ);
    /// unknown 404 "404 Not found" (READ g2 G1.5); neither its author nor a Developer
    /// 403 (ASSUMED: who may update); another <c>state_event</c> 400 shape E "state_event does not have a
    /// valid value" (ASSUMED: Grape's text).
    /// </summary>
    private AuthRemoteResponse UpdateMergeRequest(FakeServiceCall call, FakeGitLabProject project, string iidText)
    {
        if (!TryIid(iidText, out var iid))
        {
            return InvalidIid(call);
        }

        var stateEvent = ParametersOf(call.Request).GetValueOrDefault("state_event");
        JsonObject json;
        lock (Gate)
        {
            var request = MergeRequestLocked(project, iid);
            if (request is null)
            {
                return MergeRequestNotFound(call);
            }

            if (!request.Author.Equals(call.Login, StringComparison.OrdinalIgnoreCase) && AccessLocked(project, call.Login) < 30)
            {
                return Forbidden(call);
            }

            switch (stateEvent)
            {
                case null:
                    break;
                case "close" when request.State == "opened":
                    request.State = "closed";
                    request.UpdatedAt = AppClocks.HarnessTime;
                    break;
                case "reopen" when request.State == "closed":
                    request.State = "opened";
                    request.UpdatedAt = AppClocks.HarnessTime;
                    break;
                case "close" or "reopen":
                    break;
                default:
                    return ErrorJson(call, 400, new JsonObject { ["error"] = "state_event does not have a valid value" });
            }

            json = MergeRequestJsonLocked(request, single: true);
        }

        return Json(call, 200, json);
    }

    /// <summary><c>DELETE …/merge_requests/:iid</c>: 204 for an Owner alone, else 403; unknown 404 "404 Not found" (READ g2 G1.6; the Owner's 204 MEASURED 2026-11-06, stage 1's clean-up). Its head ref goes at the next sync (ASSUMED).</summary>
    private AuthRemoteResponse DeleteMergeRequest(FakeServiceCall call, FakeGitLabProject project, string iidText)
    {
        if (!TryIid(iidText, out var iid))
        {
            return InvalidIid(call);
        }

        lock (Gate)
        {
            var request = MergeRequestLocked(project, iid);
            if (request is null)
            {
                return MergeRequestNotFound(call);
            }

            if (AccessLocked(project, call.Login) < 50)
            {
                return Forbidden(call);
            }

            request.Deleted = true;
        }

        return NoContent(call);
    }

    /// <summary><c>GET …/repository/branches</c>: <c>search</c> <c>^term</c> a prefix, <c>term$</c> a suffix, else a part, without case (READ g2 G1.7); by name; paged.</summary>
    private async Task<AuthRemoteResponse> BranchesAnswerAsync(FakeServiceCall call, FakeGitLabProject project, CancellationToken ct)
    {
        var query = new Query(call.Request.Query);
        var search = query.Get("search");
        var branches = (await BranchesAsync(project, ct).ConfigureAwait(false)).Where(branch => Matches(branch.Name, search)).ToList();
        var (slice, headers) = Paged(query, branches, call.Request.Path[ApiPrefix.Length..].TrimEnd('/'));
        var defaultBranch = DefaultBranchOf(project);
        bool canPush;
        lock (Gate)
        {
            canPush = AccessLocked(project, call.Login) >= 30;
        }

        var array = new JsonArray([.. slice.Select(branch => (JsonNode?)BranchJson(project, branch, defaultBranch, canPush))]);
        return Json(call, 200, array, headers);

        static bool Matches(string name, string? search) => search switch
        {
            null or "" => true,
            ['^', .. var prefix] => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            [.. var suffix, '$'] => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase),
            _ => name.Contains(search, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary><c>DELETE …/repository/branches/:branch</c> (the name one escaped segment): 403 without push, 404 "404 Branch Not Found", else 204 (READ g2 G1.7; the 204 MEASURED 2026-11-06, stage 1's clean-up).</summary>
    private async Task<AuthRemoteResponse> DeleteBranchAnswerAsync(FakeServiceCall call, FakeGitLabProject project, string branchSegment, CancellationToken ct)
    {
        lock (Gate)
        {
            if (AccessLocked(project, call.Login) < 30)
            {
                return Forbidden(call);
            }
        }

        var name = Uri.UnescapeDataString(branchSegment);
        if (!await HasBranchAsync(project, name, ct).ConfigureAwait(false))
        {
            return ErrorJson(call, 404, new JsonObject { ["message"] = "404 Branch Not Found" });
        }

        await DeleteBranchAsync(project, name, ct).ConfigureAwait(false);
        return NoContent(call);
    }

    /// <summary>Any other GET: 200 <c>text/html</c>; a moved project's old web path 301 to its new path, the rest kept (MEASURED g2 G4).</summary>
    private AuthRemoteResponse Page(FakeServiceCall call)
    {
        var segments = call.Request.Path.Trim('/').Split('/');
        lock (Gate)
        {
            foreach (var (oldPath, project) in _movedFrom)
            {
                var old = oldPath.Split('/');
                if (segments.Length >= old.Length
                    && old.Select((segment, i) => segment.Equals(segments[i], StringComparison.OrdinalIgnoreCase)).All(same => same)
                    && (segments.Length == old.Length || segments[old.Length] == "-"))
                {
                    var rest = string.Join('/', segments.Skip(old.Length));
                    var location = Web(project.Path + (rest.Length > 0 ? "/" + rest : string.Empty));
                    return Html(301, "<html><body>You are being <a href=\"" + location + "\">redirected</a>.</body></html>", location);
                }
            }
        }

        return Html(200, "<!doctype html><title>" + System.Net.WebUtility.HtmlEncode(call.Request.Path) + " · GitLab</title>", null);
    }

    /// <summary>A <c>.git</c>-less <c>GET &lt;path&gt;/info/refs</c>: 301 to <c>&lt;path&gt;.git/info/refs</c>, the query kept (MEASURED r6 6.1), absolute as Rails writes it; git follows (MEASURED r7 6).</summary>
    private AuthRemoteResponse GitRedirect(FakeServiceCall call)
    {
        var path = call.Request.Path;
        var location = Endpoints.WebBase.GetLeftPart(UriPartial.Authority) + path[..^"/info/refs".Length] + ".git/info/refs"
            + (call.Request.Query.Length > 0 ? "?" + call.Request.Query : string.Empty);
        return Html(301, "<html><body>You are being <a href=\"" + location + "\">redirected</a>.</body></html>", location);
    }

    /// <summary>A fault's answer (4.4).</summary>
    private AuthRemoteResponse FaultAnswer(FakeServiceCall call, Route route, FakeGitLabFault fault)
    {
        var need = NeedOf(route.Kind);
        if (fault == FakeGitLabFault.Unauthorized)
        {
            return Unauthorized(call);
        }

        if (fault == FakeGitLabFault.Expired || fault == FakeGitLabFault.Revoked)
        {
            return InvalidToken(call, fault == FakeGitLabFault.Expired ? ExpiredText : RevokedText);
        }

        if (fault == FakeGitLabFault.InsufficientScope)
        {
            return InsufficientScope(call, need.ScopeList ?? "api");
        }

        if (fault == FakeGitLabFault.InsufficientGranular)
        {
            return InsufficientGranular(call, need.Label, userBoundary: FakeGitLabPermissions.IsUserBoundary(need.Label) || route.Id is null);
        }

        if (fault == FakeGitLabFault.Forbidden)
        {
            return Forbidden(call);
        }

        if (fault == FakeGitLabFault.NotFound)
        {
            return ErrorJson(call, 404, new JsonObject { ["message"] = "404 Not Found" });
        }

        if (fault == FakeGitLabFault.RateLimited)
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                KeyValuePair.Create("Content-Type", "text/plain"),
                KeyValuePair.Create("Retry-After", "60"),
                KeyValuePair.Create("RateLimit-ResetTime", AppClocks.HarnessTime.AddMinutes(1).ToString("r", CultureInfo.InvariantCulture)),
            };
            AddCommonHeaders(call, headers, exhausted: true);
            return new AuthRemoteResponse(429, ReasonOf(429), headers, "Retry later\n"u8.ToArray());
        }

        if (fault == FakeGitLabFault.EndpointRateLimited)
        {
            // READ r6 8; its RateLimit-Name projects_api on every route ASSUMED.
            var headers = new List<KeyValuePair<string, string>>
            {
                KeyValuePair.Create("Content-Type", "application/json"),
                KeyValuePair.Create("Cache-Control", "no-cache"),
                KeyValuePair.Create("Retry-After", "600"),
                KeyValuePair.Create("RateLimit-Name", "projects_api"),
                RequestIdHeader(),
            };
            return new AuthRemoteResponse(429, ReasonOf(429), headers, Serialize(new JsonObject { ["message"] = new JsonObject { ["error"] = "This endpoint has been requested too many times. Try again later." } }));
        }

        if (fault == FakeGitLabFault.Timeout)
        {
            return ErrorJson(call, 408, new JsonObject { ["message"] = new JsonObject { ["error"] = "Request timed out" } });
        }

        if (fault == FakeGitLabFault.ServerError)
        {
            return ErrorJson(call, 500, new JsonObject { ["message"] = "500 Internal Server Error" });
        }

        if (fault == FakeGitLabFault.BadGateway)
        {
            return Html(502, "<html><head><title>502 Bad Gateway</title></head><body><h1>502 Bad Gateway</h1></body></html>", null);
        }

        if (fault == FakeGitLabFault.Malformed)
        {
            var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Content-Type", "application/json") };
            AddCommonHeaders(call, headers);
            return new AuthRemoteResponse(200, ReasonOf(200), headers, "{\"id\":"u8.ToArray());
        }

        // MovedTo: 301 as a moved project's (MEASURED r6 3.2).
        return MovedPermanently(call, fault.Location!.AbsoluteUri);
    }

    // The texts of shape F (READ g2 G2.1, G2.2) and of a fine-grained refusal (its user form MEASURED
    // 2026-11-06 and 2026-11-05, D385; the project form READ g2 G2.4).
    private const string ExpiredText = "Token is expired. You can either do re-authorization or token refresh.";
    private const string RevokedText = "Token was revoked. You have to re-authorize from the user.";
    private const string ScopeText = "The request requires higher privileges than provided by the access token.";

    private static string GranularText(string boundary, string label) =>
        $"Access denied: This operation requires a fine-grained personal access token with the following {boundary} permissions: [{label}].";

    /// <summary>401 shape A "401 Unauthorized", no <c>WWW-Authenticate</c>: no token (MEASURED r6 1.2) or an unknown one (MEASURED 2026-11-06: a made-up <c>glpat-</c> value on <c>GET user</c>, D373; READ g2 G2.1).</summary>
    private AuthRemoteResponse Unauthorized(FakeServiceCall call) => ErrorJson(call, 401, new JsonObject { ["message"] = "401 Unauthorized" });

    /// <summary>401 shape F <c>invalid_token</c> with <c>WWW-Authenticate: Bearer realm="Protected by OAuth 2.0", error=…, error_description=…</c> (READ g2 G2.2).</summary>
    private AuthRemoteResponse InvalidToken(FakeServiceCall call, string description) =>
        ErrorJson(
            call,
            401,
            new JsonObject { ["error"] = "invalid_token", ["error_description"] = description },
            [KeyValuePair.Create("WWW-Authenticate", $"Bearer realm=\"Protected by OAuth 2.0\", error=\"invalid_token\", error_description=\"{description}\"")]);

    /// <summary>403 shape F <c>insufficient_scope</c>, <c>scope</c> after the other two, the same list in the challenge (READ g2 G2.3; the list ASSUMED).</summary>
    private AuthRemoteResponse InsufficientScope(FakeServiceCall call, string scopes) =>
        ErrorJson(
            call,
            403,
            new JsonObject { ["error"] = "insufficient_scope", ["error_description"] = ScopeText, ["scope"] = scopes },
            [KeyValuePair.Create("WWW-Authenticate", $"Bearer realm=\"Protected by OAuth 2.0\", error=\"insufficient_scope\", error_description=\"{ScopeText}\", scope=\"{scopes}\"")]);

    /// <summary>403 shape F <c>insufficient_granular_scope</c> naming the boundary (<c>user</c>, as on <c>GET projects</c>, r6 2.2, or <c>project</c>) and the label as GitLab writes it (<see cref="FakeGitLabPermissions.LabelOf"/>), no <c>WWW-Authenticate</c>, <c>Content-Type: application/json</c> (MEASURED 2026-11-06 on <c>personal_access_tokens/self</c> and 2026-11-05 on <c>GET projects</c>, D385, both naming <c>user</c>; the <c>project</c> form READ g2 G2.4).</summary>
    private AuthRemoteResponse InsufficientGranular(FakeServiceCall call, string label, bool userBoundary) =>
        ErrorJson(call, 403, new JsonObject
        {
            ["error"] = "insufficient_granular_scope",
            ["error_description"] = GranularText(userBoundary ? "user" : "project", FakeGitLabPermissions.LabelOf(label)),
        });

    /// <summary>403 shape A "403 Forbidden" (READ r6 5.2, g2 G1.2).</summary>
    private AuthRemoteResponse Forbidden(FakeServiceCall call) => ErrorJson(call, 403, new JsonObject { ["message"] = "403 Forbidden" });

    /// <summary>404 shape A "404 Not found", lower-case <c>f</c>: an unknown merge request (MEASURED g2 G4) or <c>target_project_id</c> (READ g2 G1.1).</summary>
    private AuthRemoteResponse MergeRequestNotFound(FakeServiceCall call) => ErrorJson(call, 404, new JsonObject { ["message"] = "404 Not found" });

    /// <summary>400 shape E "merge_request_iid is invalid" (MEASURED g2 G4).</summary>
    private AuthRemoteResponse InvalidIid(FakeServiceCall call) => ErrorJson(call, 400, new JsonObject { ["error"] = "merge_request_iid is invalid" });

    /// <summary>301 <c>text/plain</c> "This resource has been moved permanently to &lt;location&gt;" with that <c>Location</c> (MEASURED r6 3.2).</summary>
    private AuthRemoteResponse MovedPermanently(FakeServiceCall call, string location)
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("Content-Type", "text/plain; charset=utf-8"),
            KeyValuePair.Create("Location", location),
        };
        AddCommonHeaders(call, headers);
        return new AuthRemoteResponse(301, ReasonOf(301), headers, Encoding.UTF8.GetBytes("This resource has been moved permanently to " + location));
    }

    /// <summary>A moved project's 301 target: <c>&lt;api&gt;projects/&lt;id&gt;[/rest]</c> and the query, its parameters in name order (MEASURED r6 3.2; the order ASSUMED).</summary>
    private string MovedLocation(FakeGitLabProject project, string rest, string query)
    {
        var sorted = string.Join('&', query.Split('&', StringSplitOptions.RemoveEmptyEntries).OrderBy(part => part.Split('=')[0], StringComparer.Ordinal));
        return Endpoints.ApiBase.AbsoluteUri + "projects/" + project.Id.ToString(CultureInfo.InvariantCulture)
            + (rest.Length > 0 ? "/" + rest : string.Empty)
            + (sorted.Length > 0 ? "?" + sorted : string.Empty);
    }

    /// <summary>
    /// A JSON answer (MEASURED r6 8, 13.1): <c>Content-Type: application/json</c> without a charset; on a
    /// 200 GET a weak <c>ETag</c>, and a matching <c>If-None-Match</c> answered 304 with no body, the
    /// request still counted; <c>Cache-Control</c>; the <c>ratelimit-*</c>, <c>x-request-id</c> and
    /// <c>x-gitlab-meta</c>.
    /// </summary>
    private AuthRemoteResponse Json(FakeServiceCall call, int status, JsonNode node, IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        var body = Serialize(node);
        var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Content-Type", "application/json") };
        if (status == 200 && call.Request.Method == "GET")
        {
            var etag = "W/\"" + Convert.ToHexStringLower(SHA256.HashData(body))[..32] + "\"";
            if (call.Request.Headers.GetValueOrDefault("If-None-Match") is { } given
                && given.Split(',', StringSplitOptions.TrimEntries).Contains(etag, StringComparer.Ordinal))
            {
                var unchanged = new List<KeyValuePair<string, string>> { KeyValuePair.Create("ETag", etag), KeyValuePair.Create("Cache-Control", "max-age=0, private, must-revalidate") };
                AddCommonHeaders(call, unchanged);
                return new AuthRemoteResponse(304, ReasonOf(304), unchanged, []);
            }

            headers.Add(KeyValuePair.Create("ETag", etag));
        }

        headers.Add(KeyValuePair.Create("Cache-Control", "max-age=0, private, must-revalidate"));
        AddCommonHeaders(call, headers);
        headers.AddRange(extra ?? []);
        return new AuthRemoteResponse(status, ReasonOf(status), headers, body);
    }

    /// <summary>An error's JSON answer: <c>Cache-Control: no-store, private, max-age=0</c> (MEASURED r6 8), the common headers.</summary>
    private AuthRemoteResponse ErrorJson(FakeServiceCall call, int status, JsonNode node, IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("Content-Type", "application/json"),
            KeyValuePair.Create("Cache-Control", "no-store, private, max-age=0"),
        };
        AddCommonHeaders(call, headers);
        headers.AddRange(extra ?? []);
        return new AuthRemoteResponse(status, ReasonOf(status), headers, Serialize(node));
    }

    /// <summary>A 204 with no body (READ g2 G1.6, G1.7); its <c>Cache-Control: no-cache</c> ASSUMED.</summary>
    private AuthRemoteResponse NoContent(FakeServiceCall call)
    {
        var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Cache-Control", "no-cache") };
        AddCommonHeaders(call, headers);
        return new AuthRemoteResponse(204, ReasonOf(204), headers, []);
    }

    private AuthRemoteResponse Html(int status, string html, string? location)
    {
        var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Content-Type", "text/html; charset=utf-8"), RequestIdHeader() };
        if (location is not null)
        {
            headers.Add(KeyValuePair.Create("Location", location));
        }

        return new AuthRemoteResponse(status, ReasonOf(status), headers, Encoding.UTF8.GetBytes(html));
    }

    /// <summary>
    /// The headers on every API answer (MEASURED r6 8, 13.1): <c>ratelimit-observed</c>,
    /// <c>ratelimit-remaining</c>, <c>ratelimit-reset</c> (the next minute's epoch), <c>ratelimit-limit</c>
    /// and <c>ratelimit-name</c>, per user (<c>burst_traffic_per_user_free_plan</c>, 100, MEASURED
    /// 2026-11-06 with a fine-grained token on every answer of stage 1, a 304 among them; r6 READ
    /// <c>throttle_authenticated_api</c>, 2000) or anonymous (<c>throttle_unauthenticated_api</c>, 500,
    /// MEASURED), each request counted, a 304 too (MEASURED); the fake's count never resets (ASSUMED:
    /// no test waits a window out, and the client reads none of them but on a 429);
    /// then <c>x-request-id</c> and <c>x-gitlab-meta</c> (MEASURED).
    /// </summary>
    private void AddCommonHeaders(FakeServiceCall call, List<KeyValuePair<string, string>> headers, bool exhausted = false)
    {
        var login = call.Login;
        var limit = login is null ? AnonymousLimit : AuthenticatedLimit;
        int used;
        lock (_used)
        {
            used = _used[login ?? string.Empty] = _used.GetValueOrDefault(login ?? string.Empty) + 1;
        }

        headers.Add(KeyValuePair.Create("ratelimit-observed", (exhausted ? limit : used).ToString(CultureInfo.InvariantCulture)));
        headers.Add(KeyValuePair.Create("ratelimit-remaining", (exhausted ? 0 : Math.Max(0, limit - used)).ToString(CultureInfo.InvariantCulture)));
        headers.Add(KeyValuePair.Create("ratelimit-reset", AppClocks.HarnessTime.AddMinutes(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        headers.Add(KeyValuePair.Create("ratelimit-limit", limit.ToString(CultureInfo.InvariantCulture)));
        headers.Add(KeyValuePair.Create("ratelimit-name", login is null ? "throttle_unauthenticated_api" : "burst_traffic_per_user_free_plan"));
        var id = RequestIdHeader();
        headers.Add(id);
        headers.Add(KeyValuePair.Create("x-gitlab-meta", $"{{\"correlation_id\":\"{id.Value}\",\"version\":\"1\"}}"));
    }

    private KeyValuePair<string, string> RequestIdHeader() =>
        KeyValuePair.Create("x-request-id", string.Create(CultureInfo.InvariantCulture, $"FAKE-GL-{Interlocked.Increment(ref _requestCount):D6}"));

    /// <summary>
    /// One page of <paramref name="items"/> by <c>per_page</c> (20, at most <see cref="FakeHostingService.MaxPerPage"/>,
    /// clamped silently) and <c>page</c>, past the end empty (MEASURED r6 7); the headers <c>Link</c>
    /// (<c>next</c>, <c>prev</c>, <c>first</c>, <c>last</c>, absolute on the API's address, every parameter
    /// kept), <c>x-next-page</c> (empty on the last), <c>x-page</c>, <c>x-per-page</c>, <c>x-prev-page</c>
    /// (empty on the first), <c>x-total</c>, <c>x-total-pages</c> (MEASURED).
    /// </summary>
    private (List<T> Slice, List<KeyValuePair<string, string>> Headers) Paged<T>(Query query, IReadOnlyList<T> items, string relativePath)
    {
        var perPage = Math.Min(query.Number("per_page") is { } asked and > 0 ? asked : 20, MaxPerPage);
        var page = query.Number("page") is { } number and > 0 ? number : 1;
        var totalPages = Math.Max(1, (items.Count + perPage - 1) / perPage);
        var slice = items.Skip((int)Math.Min(int.MaxValue, (long)(page - 1) * perPage)).Take(perPage).ToList();
        string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
        string Link(int target, string rel) => $"<{Endpoints.ApiBase.AbsoluteUri}{relativePath}?{query.WithPage(target, perPage)}>; rel=\"{rel}\"";

        var links = new List<string>();
        if (page < totalPages)
        {
            links.Add(Link(page + 1, "next"));
        }

        if (page > 1 && page <= totalPages)
        {
            links.Add(Link(page - 1, "prev"));
        }

        links.Add(Link(1, "first"));
        links.Add(Link(totalPages, "last"));
        var headers = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("Link", string.Join(", ", links)),
            KeyValuePair.Create("x-next-page", page < totalPages ? Text(page + 1) : string.Empty),
            KeyValuePair.Create("x-page", Text(page)),
            KeyValuePair.Create("x-per-page", Text(perPage)),
            KeyValuePair.Create("x-prev-page", page > 1 ? Text(page - 1) : string.Empty),
            KeyValuePair.Create("x-total", Text(items.Count)),
            KeyValuePair.Create("x-total-pages", Text(totalPages)),
        };
        return (slice, headers);
    }

    /// <summary>The project a route's <c>:id</c> names: by number, or by its path now, without case; else by an old path (moved). Under the lock.</summary>
    private (FakeGitLabProject? Project, bool Moved) ResolveLocked(string id)
    {
        if (id.Length > 0 && id.All(char.IsAsciiDigit))
        {
            return (long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? _projects.FirstOrDefault(project => project.Id == number) : null, false);
        }

        var path = Uri.UnescapeDataString(id);
        if (ProjectLocked(path) is { } current)
        {
            return (current, false);
        }

        return _movedFrom.TryGetValue(path, out var moved) ? (moved, true) : (null, false);
    }

    private FakeMergeRequest? MergeRequestLocked(FakeGitLabProject project, int iid) =>
        _mergeRequests.FirstOrDefault(request => request.Target == project && request.Iid == iid && !request.Deleted);

    /// <summary>
    /// Whether <paramref name="source"/> and <paramref name="target"/> are in one fork network, the same
    /// root at the top of their <see cref="FakeGitLabProject.ForkOf"/>: a fork into its parent, the parent
    /// into its fork and one sibling into another pass (READ: <c>validate_fork</c> refuses only when
    /// <c>source_project_missing?</c>, <c>!source_project.in_fork_network_of?(target_project)</c>,
    /// app/models/merge_request.rb:1481-1487, :1508-1513).
    /// </summary>
    private static bool InOneForkNetwork(FakeGitLabProject source, FakeGitLabProject target)
    {
        static FakeGitLabProject Root(FakeGitLabProject project)
        {
            while (project.ForkOf is { } parent)
            {
                project = parent;
            }

            return project;
        }

        return Root(source) == Root(target);
    }

    private static bool TryIid(string text, out int iid) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out iid) && iid > 0;

    /// <summary>A POST's or PUT's parameters: the query's, then the body's, a form or JSON (READ r6 5.1).</summary>
    private static Dictionary<string, string> ParametersOf(AuthRemoteRouteRequest request)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in new Query(request.Query).Pairs)
        {
            parameters[name] = value;
        }

        var text = Encoding.UTF8.GetString(request.Body);
        var contentType = request.Headers.GetValueOrDefault("Content-Type") ?? string.Empty;
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (JsonNode.Parse(text) is JsonObject json)
                {
                    foreach (var (name, value) in json)
                    {
                        if (value is JsonValue scalar)
                        {
                            parameters[name] = scalar.GetValueKind() switch
                            {
                                JsonValueKind.String => scalar.GetValue<string>(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => scalar.ToJsonString(),
                            };
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // GitLab answers an unreadable body 400; the fake reads it as no parameters, so (a) answers.
            }
        }
        else
        {
            foreach (var (name, value) in new Query(text).Pairs)
            {
                parameters[name] = value;
            }
        }

        return parameters;
    }

    /// <summary>Whether a path is git's: a segment ending in <c>.git</c> with more after it (<c>/g/p.git/info/refs</c>).</summary>
    private static bool IsGitPath(string path)
    {
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Length > 4 && segments[i].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A request's route from its method and raw path (4.3's table).</summary>
    private Route Parse(string method, string path)
    {
        if (!path.StartsWith(ApiPrefix, StringComparison.Ordinal) && path != "/api/v4")
        {
            const string InfoRefs = "/info/refs";
            return method == "GET" && path.EndsWith(InfoRefs, StringComparison.Ordinal) && path.Length > InfoRefs.Length + 1
                ? new Route(RouteKind.GitRedirect, "page")
                : new Route(RouteKind.Page, "page");
        }

        var rest = path == "/api/v4" ? string.Empty : path[ApiPrefix.Length..];
        if (rest.EndsWith('/'))
        {
            rest = rest[..^1];
        }

        var s = rest.Split('/');
        switch (s)
        {
            case ["user"] when method == "GET":
                return new Route(RouteKind.User, "user");
            case ["personal_access_tokens", "self"] when method == "GET":
                return new Route(RouteKind.Self, "self");
            case ["projects"] when method == "GET":
                return new Route(RouteKind.Projects, "projects");
            case ["projects", { Length: > 0 } id, .. var tail]:
                var key = KeyPathOf(id);
                var tailText = string.Join('/', tail);
                return (tail, method) switch
                {
                    ([], "GET") => new Route(RouteKind.Project, "project:" + key, id),
                    (["merge_requests"], "GET") => new Route(RouteKind.MergeRequests, "mrs:" + key, id, tailText),
                    (["merge_requests"], "POST") => new Route(RouteKind.Create, "create:" + key, id, tailText),
                    (["merge_requests", { Length: > 0 } iid], "GET") => new Route(RouteKind.MergeRequest, $"mr:{key}/{iid}", id, tailText, iid),
                    (["merge_requests", { Length: > 0 } iid], "PUT") => new Route(RouteKind.Update, $"update:{key}/{iid}", id, tailText, iid),
                    (["merge_requests", { Length: > 0 } iid], "DELETE") => new Route(RouteKind.Delete, $"delete:{key}/{iid}", id, tailText, iid),
                    (["repository", "branches"], "GET") => new Route(RouteKind.Branches, "branches:" + key, id, tailText),
                    (["repository", "branches", { Length: > 0 } branch], "DELETE") => new Route(RouteKind.DeleteBranch, "branches:" + key, id, tailText, Branch: branch),
                    _ => new Route(RouteKind.Miss, null),
                };
            default:
                return new Route(RouteKind.Miss, null);
        }
    }

    /// <summary>The path in a route's key: a number's project's path now (the number when none), else the <c>:id</c> decoded once.</summary>
    private string KeyPathOf(string id)
    {
        if (id.All(char.IsAsciiDigit))
        {
            lock (Gate)
            {
                return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && _projects.FirstOrDefault(project => project.Id == number) is { } project
                    ? project.Path
                    : id;
            }
        }

        return Uri.UnescapeDataString(id);
    }

    /// <summary>What a route needs (4.3's checks 4 and 5): the fine-grained label (r6 2.2), the legacy scopes it takes and the <c>scope</c> list its 403 names (READ g2 G2.3: <c>user</c>'s, the list's and the create's; the others API::API's alone, ASSUMED), and whether it reads.</summary>
    private static Need NeedOf(RouteKind kind) => kind switch
    {
        RouteKind.User => new(FakeGitLabPermissions.UserRead, [FakeGitLabPermissions.ReadUser, FakeGitLabPermissions.ReadApi, FakeGitLabPermissions.Api], "read_user ai_workflows api read_api", Read: true),
        RouteKind.Self => new(FakeGitLabPermissions.PersonalAccessTokenRead, null, null, Read: true),
        RouteKind.Projects => new(FakeGitLabPermissions.UserProjectRead, ReadScopes, "api read_api", Read: true),
        RouteKind.Project => new(FakeGitLabPermissions.ProjectRead, ReadScopes, "api read_api", Read: true),
        RouteKind.MergeRequests => new(FakeGitLabPermissions.MergeRequestRead, ReadScopes, "ai_workflows api read_api", Read: true),
        RouteKind.MergeRequest => new(FakeGitLabPermissions.MergeRequestRead, ReadScopes, "api read_api", Read: true),
        RouteKind.Create => new(FakeGitLabPermissions.MergeRequestCreate, [FakeGitLabPermissions.Api], "mcp ai_workflows api", Read: false),
        RouteKind.Update => new(FakeGitLabPermissions.MergeRequestUpdate, [FakeGitLabPermissions.Api], "api", Read: false),
        RouteKind.Delete => new(FakeGitLabPermissions.MergeRequestDelete, [FakeGitLabPermissions.Api], "api", Read: false),
        RouteKind.Branches => new(FakeGitLabPermissions.BranchRead, ReadScopes, "api read_api", Read: true),
        RouteKind.DeleteBranch => new(FakeGitLabPermissions.BranchDelete, [FakeGitLabPermissions.Api], "api", Read: false),
        _ => new(FakeGitLabPermissions.ProjectRead, ReadScopes, "api read_api", Read: true),
    };

    private static readonly string[] ReadScopes = [FakeGitLabPermissions.ReadApi, FakeGitLabPermissions.Api];

    private static string ReasonOf(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        304 => "Not Modified",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        408 => "Request Timeout",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        _ => "Status",
    };

    /// <summary>A parsed route: its kind, key, raw <c>:id</c>, the raw path after it, an iid's text and a branch's raw segment.</summary>
    private sealed record Route(RouteKind Kind, string? Key, string? Id = null, string Rest = "", string? Iid = null, string? Branch = null);

    private sealed record Need(string Label, string[]? Scopes, string? ScopeList, bool Read);

    /// <summary>A query's parameters, decoded with <c>+</c> as a space, in order.</summary>
    private sealed class Query(string text)
    {
        public IReadOnlyList<(string Name, string Value)> Pairs { get; } =
            [.. text.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                var equals = part.IndexOf('=', StringComparison.Ordinal);
                static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
                return equals < 0 ? (Decode(part), string.Empty) : (Decode(part[..equals]), Decode(part[(equals + 1)..]));
            })];

        public string? Get(string name) => Pairs.Where(pair => pair.Name == name).Select(pair => pair.Value).FirstOrDefault();

        public int? Number(string name) => int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

        /// <summary>The parameters with <c>page</c> and <c>per_page</c> set, the others kept in order.</summary>
        public string WithPage(int page, int perPage)
        {
            var parts = Pairs.Where(pair => pair.Name is not ("page" or "per_page")).Select(pair => Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(pair.Value)).ToList();
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
            parts.Add("per_page=" + perPage.ToString(CultureInfo.InvariantCulture));
            return string.Join('&', parts);
        }
    }
}
