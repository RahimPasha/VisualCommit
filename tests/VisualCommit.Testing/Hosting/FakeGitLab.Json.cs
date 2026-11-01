using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The shapes of the fake's answers (docs/phase-7b-design.md 4.4): snake_case members; dates in UTC
/// with milliseconds and <c>Z</c> (MEASURED r6 4.1, 13.1); addresses on <see cref="Endpoints"/>; made-up
/// values in the services' member names and order. The JSON is built under the fake's lock.
/// </summary>
public sealed partial class FakeGitLab
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Time(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static byte[] Serialize(JsonNode node) => JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);

    private string Web(string relative) => new Uri(Endpoints.WebBase, relative).AbsoluteUri;

    /// <summary>
    /// <c>GET user</c>'s user (r6 1.3, READ: <c>id</c>, <c>username</c>, <c>name</c>, <c>state</c>,
    /// <c>locked</c>, <c>avatar_url</c>, <c>web_url</c>, <c>bot</c>, <c>created_at</c>,
    /// <c>public_email</c>, <c>email</c>, <c>commit_email</c>, <c>two_factor_enabled</c>,
    /// <c>can_create_project</c>, <c>identities</c>); its addresses the made-up
    /// <c>&lt;username&gt;@example.com</c>, which a client must not keep (D346); no avatar (D349).
    /// </summary>
    private JsonObject CurrentUserJson(FakeGitLabUser user)
    {
        var json = UserJson(user);
        json["created_at"] = Time(AppClocks.HarnessTime.AddYears(-1));
        json["email"] = user.Email;
        json["commit_email"] = user.Email;
        json["two_factor_enabled"] = false;
        json["can_create_project"] = true;
        json["identities"] = new JsonArray();
        return json;
    }

    /// <summary>A user as an MR's <c>author</c> or a project's <c>owner</c> (MEASURED r6 4.1's members, <c>public_email</c> among them, g4 D4 §5).</summary>
    private JsonObject UserJson(FakeGitLabUser user) => new()
    {
        ["id"] = user.Id,
        ["username"] = user.Username,
        ["public_email"] = user.Email,
        ["name"] = user.Name,
        ["state"] = "active",
        ["locked"] = false,
        ["avatar_url"] = null,
        ["web_url"] = Web(user.Username),
        ["bot"] = false,
    };

    /// <summary>
    /// <c>personal_access_tokens/self</c>'s token (r6 1.4, READ): <c>scopes</c> a legacy token's, a
    /// fine-grained one's <c>["granular"]</c> (ASSUMED) with <c>granular_scopes</c> of raw permission
    /// names (READ g2 G2.4; the user's in one item, the projects' under <c>all_memberships</c>, ASSUMED);
    /// <c>expires_at</c> a date or null.
    /// </summary>
    private JsonObject TokenJson(GitLabToken token, FakeGitLabUser user)
    {
        var granular = token.Kind == FakeGitLabTokenKind.FineGrained;
        var json = new JsonObject
        {
            ["id"] = token.Id,
            ["name"] = "VisualCommit",
            ["revoked"] = false,
            ["created_at"] = Time(AppClocks.HarnessTime.AddDays(-7)),
            ["description"] = "Made by the fake of GitLab.",
            ["scopes"] = granular ? new JsonArray("granular") : new JsonArray([.. token.Permissions.Order(StringComparer.Ordinal).Select(scope => (JsonNode?)scope)]),
            ["user_id"] = user.Id,
            ["last_used_at"] = Time(AppClocks.HarnessTime),
            ["last_used_ips"] = new JsonArray(),
            ["active"] = !token.IsExpired,
            ["granular"] = granular,
            ["expires_at"] = token.Expires?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        if (granular)
        {
            var scopes = new JsonArray();
            foreach (var group in token.Permissions.GroupBy(FakeGitLabPermissions.IsUserBoundary).OrderByDescending(group => group.Key))
            {
                scopes.Add(new JsonObject
                {
                    ["access"] = group.Key ? "user" : "all_memberships",
                    ["permissions"] = new JsonArray([.. group.Select(FakeGitLabPermissions.RawName).Order(StringComparer.Ordinal).Select(name => (JsonNode?)name)]),
                    ["project_id"] = null,
                    ["group_id"] = null,
                });
            }

            json["granular_scopes"] = scopes;
        }

        return json;
    }

    /// <summary>
    /// A project (r6 3.1): with <paramref name="simple"/> the 20 keys of <c>BasicProjectDetails</c>
    /// (MEASURED anonymous); else those and <c>archived</c>, <c>empty_repo</c>, <c>merge_requests_enabled</c>,
    /// a fork's <c>forked_from_project</c> (when its parent is visible) and <c>mr_default_target_self</c>,
    /// a user namespace's <c>owner</c>, and for a signed-in user <c>permissions</c> (READ). An empty
    /// repository's <c>default_branch</c> is <c>main</c> with <c>empty_repo: true</c> (ASSUMED).
    /// </summary>
    private JsonObject ProjectJsonLocked(FakeGitLabProject project, string? login, bool simple, bool empty, string defaultBranch)
    {
        var json = new JsonObject
        {
            ["id"] = project.Id,
            ["description"] = project.Description,
            ["name"] = project.Name,
            ["name_with_namespace"] = string.Join(" / ", project.Path.Split('/')),
            ["path"] = project.Name,
            ["path_with_namespace"] = project.Path,
            ["created_at"] = Time(project.CreatedAt),
            ["default_branch"] = defaultBranch,
            ["tag_list"] = new JsonArray(),
            ["topics"] = new JsonArray(),
            ["ssh_url_to_repo"] = $"git@{Endpoints.WebBase.Host}:{project.Path}.git",
            ["http_url_to_repo"] = Web(project.Path + ".git"),
            ["web_url"] = Web(project.Path),
            ["readme_url"] = null,
            ["forks_count"] = _projects.Count(other => other.ForkOf == project),
            ["avatar_url"] = null,
            ["star_count"] = 0,
            ["last_activity_at"] = Time(project.LastActivityAt),
            ["visibility"] = project.Visibility,
            ["namespace"] = NamespaceJsonLocked(project.Namespace),
        };
        if (simple)
        {
            return json;
        }

        json["archived"] = project.IsArchived;
        json["empty_repo"] = empty;
        json["merge_requests_enabled"] = project.MergeRequestsEnabled;
        if (_users.GetValueOrDefault(project.Namespace) is { } owner)
        {
            json["owner"] = UserJson(owner);
        }

        if (project.ForkOf is { } parent)
        {
            json["mr_default_target_self"] = false;
            if (VisibleLocked(parent, login))
            {
                json["forked_from_project"] = ProjectJsonLocked(parent, login, simple: true, empty: false, DefaultBranchOf(parent));
            }
        }

        if (login is not null)
        {
            var projectAccess = OwnsLocked(project, login) ? 50 : ProjectGrantLocked(project, login);
            var groupAccess = GroupGrantLocked(project, login);
            json["permissions"] = new JsonObject
            {
                ["project_access"] = AccessJson(projectAccess),
                ["group_access"] = AccessJson(groupAccess),
            };
        }

        return json;
    }

    private static JsonObject? AccessJson(int? level) =>
        level is { } value ? new JsonObject { ["access_level"] = value, ["notification_level"] = 3 } : null;

    /// <summary>
    /// A project's <c>namespace</c> (r6 3.1, MEASURED members): a user's (<c>kind</c> <c>user</c>, the
    /// user's id) or a group's (<c>kind</c> <c>group</c>, a made-up id from 9001, <c>parent_id</c> its
    /// parent group's).
    /// </summary>
    private JsonObject NamespaceJsonLocked(string fullPath)
    {
        var last = fullPath[(fullPath.LastIndexOf('/') + 1)..];
        if (_users.GetValueOrDefault(fullPath) is { } user)
        {
            return new JsonObject
            {
                ["id"] = user.Id,
                ["name"] = user.Name,
                ["path"] = user.Username,
                ["kind"] = "user",
                ["full_path"] = user.Username,
                ["parent_id"] = null,
                ["avatar_url"] = null,
                ["web_url"] = Web(user.Username),
            };
        }

        var slash = fullPath.LastIndexOf('/');
        return new JsonObject
        {
            ["id"] = GroupIdLocked(fullPath),
            ["name"] = last,
            ["path"] = last,
            ["kind"] = "group",
            ["full_path"] = fullPath,
            ["parent_id"] = slash < 0 ? null : GroupIdLocked(fullPath[..slash]),
            ["avatar_url"] = null,
            ["web_url"] = Web("groups/" + fullPath),
        };
    }

    private long GroupIdLocked(string fullPath)
    {
        if (!_groupIds.TryGetValue(fullPath, out var id))
        {
            _groupIds[fullPath] = id = _nextGroupId++;
        }

        return id;
    }

    /// <summary>
    /// A merge request (r6 4.1, MEASURED members): <c>references</c> <c>!&lt;iid&gt;</c>, <c>web_url</c>
    /// <c>&lt;web&gt;&lt;target path&gt;/-/merge_requests/&lt;iid&gt;</c>, <c>draft</c> and
    /// <c>work_in_progress</c> alike, <c>source_project_id</c> and <c>target_project_id</c> but no source
    /// path (MEASURED), a fork's <c>allow_collaboration</c>; with <paramref name="single"/> the single MR's
    /// <c>diff_refs</c>, <c>changes_count</c> and the rest (MEASURED r6 4.1; values ASSUMED).
    /// <c>merge_status</c> is always <c>can_be_merged</c> and <c>detailed_merge_status</c>
    /// <c>mergeable</c> (ASSUMED: the fake computes no merge).
    /// </summary>
    private JsonObject MergeRequestJsonLocked(FakeMergeRequest request, bool single)
    {
        var author = _users.GetValueOrDefault(request.Author);
        var json = new JsonObject
        {
            ["id"] = request.Id,
            ["iid"] = request.Iid,
            ["project_id"] = request.Target.Id,
            ["title"] = request.Title,
            ["description"] = request.Description ?? string.Empty,
            ["state"] = request.State,
            ["created_at"] = Time(request.CreatedAt),
            ["updated_at"] = Time(request.UpdatedAt),
            ["merged_at"] = null,
            ["closed_at"] = request.State == "closed" ? Time(request.UpdatedAt) : null,
            ["target_branch"] = request.TargetBranch,
            ["source_branch"] = request.SourceBranch,
            ["author"] = author is null ? null : UserJson(author),
            ["assignees"] = new JsonArray(),
            ["assignee"] = null,
            ["reviewers"] = new JsonArray(),
            ["source_project_id"] = request.Source.Id,
            ["target_project_id"] = request.Target.Id,
            ["labels"] = new JsonArray(),
            ["draft"] = request.Draft,
            ["work_in_progress"] = request.Draft,
            ["milestone"] = null,
            ["merge_status"] = "can_be_merged",
            ["detailed_merge_status"] = "mergeable",
            ["sha"] = request.HeadSha,
            ["merge_commit_sha"] = null,
            ["squash_commit_sha"] = null,
            ["reference"] = "!" + request.Iid.ToString(CultureInfo.InvariantCulture),
            ["references"] = new JsonObject
            {
                ["short"] = "!" + request.Iid.ToString(CultureInfo.InvariantCulture),
                ["relative"] = "!" + request.Iid.ToString(CultureInfo.InvariantCulture),
                ["full"] = request.Target.Path + "!" + request.Iid.ToString(CultureInfo.InvariantCulture),
            },
            ["web_url"] = Web(string.Create(CultureInfo.InvariantCulture, $"{request.Target.Path}/-/merge_requests/{request.Iid}")),
            ["time_stats"] = new JsonObject { ["time_estimate"] = 0, ["total_time_spent"] = 0, ["human_time_estimate"] = null, ["human_total_time_spent"] = null },
            ["squash"] = false,
            ["has_conflicts"] = false,
            ["prepared_at"] = Time(request.CreatedAt),
        };
        if (request.Source != request.Target)
        {
            json["allow_collaboration"] = request.AllowCollaboration;
            json["allow_maintainer_to_push"] = request.AllowCollaboration;
        }

        if (single)
        {
            json["diff_refs"] = new JsonObject { ["base_sha"] = request.TargetSha, ["head_sha"] = request.HeadSha, ["start_sha"] = request.TargetSha };
            json["changes_count"] = null;
            json["user"] = new JsonObject { ["can_merge"] = false };
            json["subscribed"] = false;
            json["merge_error"] = null;
            json["first_contribution"] = false;
            json["pipeline"] = null;
            json["head_pipeline"] = null;
        }

        return json;
    }

    /// <summary>A branch (r6 13.3, READ: <c>name</c>, <c>commit</c>, <c>merged</c>, <c>protected</c>, <c>default</c>, <c>web_url</c> and the rest); <c>protected</c> for the default branch alone (ASSUMED: gitlab.com's default protection).</summary>
    private JsonObject BranchJson(FakeGitLabProject project, (string Name, string Sha, string Subject) branch, string defaultBranch, bool canPush) => new()
    {
        ["name"] = branch.Name,
        ["commit"] = new JsonObject
        {
            ["id"] = branch.Sha,
            ["short_id"] = branch.Sha[..Math.Min(8, branch.Sha.Length)],
            ["title"] = branch.Subject,
            ["web_url"] = Web($"{project.Path}/-/commit/{branch.Sha}"),
        },
        ["merged"] = false,
        ["protected"] = branch.Name == defaultBranch,
        ["developers_can_push"] = false,
        ["developers_can_merge"] = false,
        ["can_push"] = canPush,
        ["default"] = branch.Name == defaultBranch,
        ["web_url"] = Web($"{project.Path}/-/tree/{Uri.EscapeDataString(branch.Name)}"),
    };

    /// <summary>The branch a project's bare repository's <c>HEAD</c> names; <c>main</c> without one (ASSUMED r6 3.1, the instance's default).</summary>
    private static string DefaultBranchOf(FakeGitLabProject project)
    {
        const string Prefix = "ref: refs/heads/";
        if (project.BarePath is null || !File.Exists(Path.Combine(project.BarePath, "HEAD")))
        {
            return "main";
        }

        var head = File.ReadAllText(Path.Combine(project.BarePath, "HEAD")).Trim();
        return head.StartsWith(Prefix, StringComparison.Ordinal) ? head[Prefix.Length..] : "main";
    }

    /// <summary>Whether a project's bare repository has no branch, read from its files; true without one.</summary>
    private static bool IsEmptyRepository(FakeGitLabProject project)
    {
        if (project.BarePath is not { } bare)
        {
            return true;
        }

        var heads = Path.Combine(bare, "refs", "heads");
        if (Directory.Exists(heads) && Directory.EnumerateFiles(heads, "*", SearchOption.AllDirectories).Any())
        {
            return false;
        }

        var packed = Path.Combine(bare, "packed-refs");
        return !File.Exists(packed) || !File.ReadLines(packed).Any(line => line.Contains(" refs/heads/", StringComparison.Ordinal));
    }
}
