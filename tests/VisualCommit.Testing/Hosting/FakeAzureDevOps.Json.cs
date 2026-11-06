using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The shapes and headers of the fake of Azure DevOps' answers (docs/phase-7b-design.md 3.4):
/// camelCase members in the order the research saw them, dates <see cref="AppClocks.HarnessTime"/>
/// with seven fractional digits and <c>Z</c> (MEASURED r5 2.2), addresses on <see cref="Endpoints"/>,
/// names in their stored spelling. Every value is made up; the services' published constants (the
/// Anonymous identity's <c>aaaaaaaa-…</c>, the core area's id) stay as they are.
/// </summary>
public sealed partial class FakeAzureDevOps
{
    /// <summary>The Anonymous identity's id and descriptor's tail (MEASURED r5 1.2; a published constant).</summary>
    private const string AnonymousId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>The core resource area's id (MEASURED g1 G4.2; a published constant, <c>AzureDevOpsApi.CoreAreaId</c>).</summary>
    private const string CoreAreaId = "79134c72-4a58-4b42-976c-04e7115f32bf";

    /// <summary>The fake's own <c>instanceId</c> (r5 1.2's member; the value made up).</summary>
    private const string InstanceId = "7b0a0001-0000-4a00-8000-00000000fa4e";

    /// <summary>The fake's own <c>deploymentId</c>.</summary>
    private const string DeploymentId = "7b0a0002-0000-4a00-8000-00000000fa4e";

    /// <summary>The fake's own <c>locationServiceData.serviceOwner</c>.</summary>
    private const string ServiceOwner = "7b0a0003-0000-4a00-8000-00000000fa4e";

    /// <summary>The fake's tenant in a member's descriptor (<c>&lt;tenant&gt;\&lt;address&gt;</c>, g1 G3.2 sample 1's form).</summary>
    private const string TenantId = "7b0a0004-0000-4a00-8000-00000000fa4e";

    /// <summary>The service identity that creates merge refs (MEASURED r5 3.3: "Microsoft.VisualStudio.Services.TFS"; its id the fake's).</summary>
    private const string ServiceIdentityId = "7b0a0005-0000-4a00-8000-00000000fa4e";

    /// <summary>The identities' host (r5 3.1: <c>spsprod*.vssps.visualstudio.com</c>; the region the fake's own): never fetched.</summary>
    private const string IdentityHost = "https://spsprodfake.vssps.visualstudio.com/";

    /// <summary>The project date in a pull request's <c>repository.project</c>, with no offset (MEASURED r5 2.2, g4 D4 §3).</summary>
    private const string ZeroDate = "0001-01-01T00:00:00";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>A UTF-8 byte order mark, which leads the expired token's body (READ g1 G2).</summary>
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private long _activity;

    /// <summary>A date as the service writes it: <c>2026-01-02T09:00:00.0000000Z</c> (MEASURED r5 2.2).</summary>
    private static string Time(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static string Seg(string name) => Uri.EscapeDataString(name);

    private string Web(string relative) => new Uri(Endpoints.WebBase, relative).AbsoluteUri;

    /// <summary>An API address of a repository: the project as its GUID (MEASURED r5 2.1).</summary>
    private string ApiUrl(FakeAzureRepository repository, string rest = "") =>
        Web(string.Create(CultureInfo.InvariantCulture, $"{Seg(repository.Organization)}/{repository.Project.Id:D}/_apis/git/repositories/{repository.Id:D}{rest}"));

    /// <summary><c>remoteUrl</c>: <c>http://{o}@127.0.0.1:&lt;port&gt;/{o}/{p}/_git/{r}</c>, the organization as the user (MEASURED r5 2.1), a space as <c>%20</c> (ASSUMED r5 6.1).</summary>
    private string RemoteUrl(FakeAzureRepository repository) =>
        $"{Endpoints.WebBase.Scheme}://{Seg(repository.Organization)}@{Endpoints.WebBase.Authority}/{Seg(repository.Organization)}/{Seg(repository.Project.Name)}/_git/{Seg(repository.Name)}";

    /// <summary><c>webUrl</c>: <c>http://127.0.0.1:&lt;port&gt;/{o}/{p}/_git/{r}</c> (MEASURED r5 2.1).</summary>
    private string WebUrl(FakeAzureRepository repository) =>
        Web($"{Seg(repository.Organization)}/{Seg(repository.Project.Name)}/_git/{Seg(repository.Name)}");

    /// <summary><c>sshUrl</c> in the service's form (MEASURED r5 2.1); nothing serves it.</summary>
    private static string SshUrl(FakeAzureRepository repository) =>
        $"git@ssh.dev.azure.com:v3/{Seg(repository.Organization)}/{Seg(repository.Project.Name)}/{Seg(repository.Name)}";

    /// <summary>
    /// <c>connectionData</c> (r5 1.2, g1 G3.2): a member's identity in g1 G3.2's nesting (MEASURED
    /// 2026-11-05 for a Code-only token: the top-level names and the identity's nine, with
    /// <c>customDisplayName</c>, with and without Suppress), or the Anonymous
    /// identity when <paramref name="user"/> is null (MEASURED r5 1.2); the instance, deployment and
    /// service owner the fake's own.
    /// </summary>
    private static JsonObject ConnectionJson(FakeAzureUser? user) => new()
    {
        ["authenticatedUser"] = IdentityJson(user),
        ["authorizedUser"] = IdentityJson(user),
        ["instanceId"] = InstanceId,
        ["deploymentId"] = DeploymentId,
        ["deploymentType"] = "hosted",
        ["locationServiceData"] = new JsonObject
        {
            ["serviceOwner"] = ServiceOwner,
            ["defaultAccessMappingMoniker"] = "PublicAccessMapping",
            ["lastChangeId"] = 1001,
            ["lastChangeId64"] = 1001,
        },
    };

    /// <summary>
    /// A trimmed identity. The Anonymous one is r5 1.2's byte for byte (MEASURED). A member's nine names
    /// in the order stage 1 measured (MEASURED 2026-11-05; the values' forms READ g1 G3.2 sample 1):
    /// <c>id</c>, <c>descriptor</c> <c>Microsoft.IdentityModel.Claims.ClaimsIdentity;&lt;tenant&gt;\&lt;address&gt;</c>,
    /// <c>subjectDescriptor</c> <c>aad.…</c>, <c>providerDisplayName</c>, <c>customDisplayName</c>
    /// (<see cref="FakeAzureUser.CustomDisplayName"/>: the display name unless set; null leaves it out,
    /// as sample 1 has it, READ), <c>isActive</c>, <c>properties.Account</c> with a capital A (MEASURED
    /// g4 D4 §1) holding the address, <c>resourceVersion</c> 2 and <c>metaTypeId</c> (0 unless set, g1 G0.4).
    /// </summary>
    private static JsonObject IdentityJson(FakeAzureUser? user)
    {
        if (user is null)
        {
            return new JsonObject
            {
                ["id"] = AnonymousId,
                ["descriptor"] = "System:PublicAccess;" + AnonymousId,
                ["providerDisplayName"] = "Anonymous",
                ["customDisplayName"] = "Anonymous",
                ["isActive"] = true,
                ["properties"] = AccountJson("Anonymous"),
                ["resourceVersion"] = 2,
                ["metaTypeId"] = 255,
            };
        }

        var json = new JsonObject
        {
            ["id"] = user.Id.ToString("D", CultureInfo.InvariantCulture),
            ["descriptor"] = $"Microsoft.IdentityModel.Claims.ClaimsIdentity;{TenantId}\\{user.UniqueName}",
            ["subjectDescriptor"] = user.SubjectDescriptor,
            ["providerDisplayName"] = user.DisplayName,
        };
        if (user.CustomDisplayName is { } custom)
        {
            json["customDisplayName"] = custom;
        }

        json["isActive"] = true;
        json["properties"] = AccountJson(user.UniqueName);
        json["resourceVersion"] = 2;
        json["metaTypeId"] = user.MetaTypeId;
        return json;
    }

    private static JsonObject AccountJson(string value) => new()
    {
        ["Account"] = new JsonObject { ["$type"] = "System.String", ["$value"] = value },
    };

    /// <summary>The core resource area of <paramref name="organization"/>: its <c>locationUrl</c> in the stored spelling (MEASURED g1 G4.2 without a credential; MEASURED 2026-11-05 with the token too, A1's spelling read: 200, <c>id, name, locationUrl</c>).</summary>
    private JsonObject AreaJson(FakeAzureOrganization organization) => new()
    {
        ["id"] = CoreAreaId,
        ["name"] = "core",
        ["locationUrl"] = Web(Seg(organization.Name) + "/"),
    };

    /// <summary>
    /// A repository (MEASURED r5 2.1, member order as served): <c>id</c>, <c>name</c>, <c>url</c>,
    /// <c>project</c>, <c>defaultBranch</c> (absent when empty), <c>size</c>, <c>remoteUrl</c>,
    /// <c>sshUrl</c>, <c>webUrl</c>, <c>isDisabled</c>, <c>isInMaintenance</c>; <c>isFork</c> only when
    /// true; on <c>repo:</c> a fork's <c>parentRepository</c> (READ; the list's ASSUMED absent) and
    /// <c>_links</c> (MEASURED).
    /// </summary>
    private JsonObject RepositoryJson(FakeAzureRepository repository, (string? DefaultBranch, long Size) facts, bool single)
    {
        var json = new JsonObject
        {
            ["id"] = repository.Id.ToString("D", CultureInfo.InvariantCulture),
            ["name"] = repository.Name,
            ["url"] = ApiUrl(repository),
            ["project"] = ProjectJson(repository.Project),
        };
        if (facts.DefaultBranch is { } branch)
        {
            json["defaultBranch"] = branch;
        }

        json["size"] = facts.Size;
        json["remoteUrl"] = RemoteUrl(repository);
        json["sshUrl"] = SshUrl(repository);
        json["webUrl"] = WebUrl(repository);
        json["isDisabled"] = repository.IsDisabled;
        json["isInMaintenance"] = false;
        if (repository.ForkOf is { } parent)
        {
            json["isFork"] = true;
            if (single)
            {
                json["parentRepository"] = RepositoryRefJson(parent);
            }
        }

        if (single)
        {
            json["_links"] = new JsonObject
            {
                ["self"] = Link(ApiUrl(repository)),
                ["project"] = Link(Web(string.Create(CultureInfo.InvariantCulture, $"_apis/projects/{repository.Project.Id:D}"))),
                ["web"] = Link(WebUrl(repository)),
                ["ssh"] = Link(SshUrl(repository)),
                ["commits"] = Link(ApiUrl(repository, "/commits")),
                ["refs"] = Link(ApiUrl(repository, "/refs")),
                ["pullRequests"] = Link(ApiUrl(repository, "/pullRequests")),
                ["items"] = Link(ApiUrl(repository, "/items")),
                ["pushes"] = Link(ApiUrl(repository, "/pushes")),
            };
        }

        return json;
    }

    /// <summary>A repository's <c>project</c> (MEASURED r5 2.1): <c>description</c> only when set; its date the harness's.</summary>
    private JsonObject ProjectJson(FakeAzureProject project)
    {
        var json = new JsonObject
        {
            ["id"] = project.Id.ToString("D", CultureInfo.InvariantCulture),
            ["name"] = project.Name,
        };
        if (project.Description is { } description)
        {
            json["description"] = description;
        }

        json["url"] = Web(string.Create(CultureInfo.InvariantCulture, $"_apis/projects/{project.Id:D}"));
        json["state"] = "wellFormed";
        json["revision"] = 11;
        json["visibility"] = project.IsPublic ? "public" : "private";
        json["lastUpdateTime"] = Time(AppClocks.HarnessTime);
        return json;
    }

    /// <summary>A fork's <c>parentRepository</c>, a GitRepositoryRef (READ g1 G1.2): no default branch and no web address.</summary>
    private JsonObject RepositoryRefJson(FakeAzureRepository parent)
    {
        var json = new JsonObject
        {
            ["id"] = parent.Id.ToString("D", CultureInfo.InvariantCulture),
            ["name"] = parent.Name,
            ["url"] = ApiUrl(parent),
            ["project"] = ProjectJson(parent.Project),
            ["collection"] = new JsonObject
            {
                ["id"] = parent.Project.Organization.Id.ToString("D", CultureInfo.InvariantCulture),
                ["name"] = parent.Organization,
                ["url"] = Web(string.Create(CultureInfo.InvariantCulture, $"_apis/projectCollections/{parent.Project.Organization.Id:D}")),
            },
        };
        if (parent.ForkOf is not null)
        {
            json["isFork"] = true;
        }

        json["remoteUrl"] = RemoteUrl(parent);
        json["sshUrl"] = SshUrl(parent);
        return json;
    }

    /// <summary>
    /// A pull request (MEASURED r5 3.1, member order as served; the single's from r5's single GET,
    /// MEASURED, as r1's and r4's appended notes read it). In a list: <c>repository</c> trimmed to
    /// <c>id, name, url, project</c>, its project <c>"unchanged"</c> with the zero date (MEASURED g4 D4 §3),
    /// the description cut to 400 characters (MEASURED r5 0.8), and the tail <c>reviewers, url,
    /// supportsIterations</c>. Alone (<paramref name="single"/>: <c>pull:</c>, create, update):
    /// <c>repository</c> whole, its project with its own state, visibility and dated
    /// <c>lastUpdateTime</c>, then <c>size</c> to <c>isInMaintenance</c> without <c>defaultBranch</c> (as
    /// served), and the tail <c>reviewers, url, _links, supportsIterations, artifactId</c>. Both:
    /// <c>createdBy</c>; no web address. A fork's: <c>sourceRefName</c> <c>refs/pull/&lt;n&gt;/source</c> and
    /// <c>forkSource {name, objectId: null, repository}</c> after <c>reviewers</c> (READ g1 G2.6).
    /// <paramref name="anonymous"/>: <c>uniqueName</c> and <c>imageUrl</c> null (MEASURED r5 3.1). Under
    /// <see cref="FakeHostingService.Gate"/>; <paramref name="facts"/> are the target's when single.
    /// </summary>
    private JsonObject PullRequestJsonLocked(FakeAzurePullRequest pull, bool anonymous, bool single, (string? DefaultBranch, long Size) forkFacts, (string? DefaultBranch, long Size) facts = default, PullAnswer answer = PullAnswer.Read)
    {
        var repository = pull.Repository;
        var json = new JsonObject
        {
            ["repository"] = single
                ? RepositoryJson(repository, (null, facts.Size), single: false)
                : new JsonObject
                {
                    ["id"] = repository.Id.ToString("D", CultureInfo.InvariantCulture),
                    ["name"] = repository.Name,
                    ["url"] = ApiUrl(repository),
                    ["project"] = new JsonObject
                    {
                        ["id"] = repository.Project.Id.ToString("D", CultureInfo.InvariantCulture),
                        ["name"] = repository.Project.Name,
                        ["state"] = "unchanged",
                        ["visibility"] = "unchanged",
                        ["lastUpdateTime"] = ZeroDate,
                    },
                },
            ["pullRequestId"] = pull.Id,
            ["codeReviewId"] = pull.Id,
            ["status"] = pull.Status switch
            {
                FakeAzurePullRequestStatus.Abandoned => "abandoned",
                FakeAzurePullRequestStatus.Completed => "completed",
                _ => "active",
            },
            ["createdBy"] = IdentityRefJson(pull.Creator, anonymous),
            ["creationDate"] = Time(AppClocks.HarnessTime),
        };
        if (!pull.IsActive)
        {
            json["closedDate"] = Time(AppClocks.HarnessTime);
        }

        json["title"] = pull.Title;
        if (pull.Description is { } description)
        {
            json["description"] = single || description.Length <= 400 ? description : description[..400];
        }

        json["sourceRefName"] = pull.Fork is null ? "refs/heads/" + pull.SourceBranch : string.Create(CultureInfo.InvariantCulture, $"refs/pull/{pull.Id}/source");
        json["targetRefName"] = "refs/heads/" + pull.TargetBranch;
        if (answer != PullAnswer.Closed)
        {
            json["mergeStatus"] = pull.MergeStatus;
        }

        json["isDraft"] = pull.IsDraft;
        json["mergeId"] = pull.MergeId.ToString("D", CultureInfo.InvariantCulture);
        if (pull.LastMergeSourceCommit is { } source)
        {
            json["lastMergeSourceCommit"] = CommitJson(repository, source);
        }

        if (pull.LastMergeTargetCommit is { } target)
        {
            json["lastMergeTargetCommit"] = CommitJson(repository, target);
        }

        if (pull.LastMergeCommit is { } merge && answer != PullAnswer.Closed)
        {
            json["lastMergeCommit"] = CommitJson(repository, merge);
        }

        json["reviewers"] = new JsonArray();
        if (answer == PullAnswer.Created)
        {
            json["labels"] = new JsonArray();
        }

        if (pull.Fork is { } fork)
        {
            json["forkSource"] = new JsonObject
            {
                ["name"] = "refs/heads/" + pull.SourceBranch,
                ["objectId"] = null,
                ["repository"] = RepositoryJson(fork, forkFacts, single: false),
            };
        }

        var url = ApiUrl(repository, string.Create(CultureInfo.InvariantCulture, $"/pullRequests/{pull.Id}"));
        json["url"] = url;
        if (single)
        {
            json["_links"] = new JsonObject
            {
                ["self"] = Link(url),
                ["repository"] = Link(ApiUrl(repository)),
                ["workItems"] = Link(url + "/workitems"),
                ["sourceBranch"] = Link(ApiUrl(pull.Fork ?? repository, "/refs/heads/" + pull.SourceBranch)),
                ["targetBranch"] = Link(ApiUrl(repository, "/refs/heads/" + pull.TargetBranch)),
                ["statuses"] = Link(url + "/statuses"),
                ["sourceCommit"] = Link(ApiUrl(repository, "/commits/" + (pull.LastMergeSourceCommit ?? string.Empty))),
                ["targetCommit"] = Link(ApiUrl(repository, "/commits/" + (pull.LastMergeTargetCommit ?? string.Empty))),
                ["createdBy"] = Link(IdentityUrl(pull.Creator)),
                ["iterations"] = Link(url + "/iterations"),
            };
        }

        json["supportsIterations"] = true;
        if (single)
        {
            json["artifactId"] = string.Create(CultureInfo.InvariantCulture, $"vstfs:///Git/PullRequestId/{repository.Project.Id:D}/{repository.Id:D}/{pull.Id}");
        }

        return json;
    }

    /// <summary>
    /// <c>createdBy</c>, an IdentityRef (MEASURED r5 3.1): <c>displayName</c>, <c>url</c> on the
    /// identities' host, <c>_links.avatar</c> under the organization's <c>GraphProfile/MemberAvatars</c>
    /// (MEASURED r5 8.3), <c>id</c>, <c>uniqueName</c> (the address; null anonymously), <c>imageUrl</c>
    /// (null anonymously; its path ASSUMED) and <c>descriptor</c>.
    /// </summary>
    private JsonObject IdentityRefJson(FakeAzureUser user, bool anonymous) => new()
    {
        ["displayName"] = user.DisplayName,
        ["url"] = IdentityUrl(user),
        ["_links"] = new JsonObject
        {
            ["avatar"] = Link(Web($"{Seg(user.Organization.Name)}/_apis/GraphProfile/MemberAvatars/{user.SubjectDescriptor}")),
        },
        ["id"] = user.Id.ToString("D", CultureInfo.InvariantCulture),
        ["uniqueName"] = anonymous ? null : user.UniqueName,
        ["imageUrl"] = anonymous ? null : Web(string.Create(CultureInfo.InvariantCulture, $"{Seg(user.Organization.Name)}/_api/_common/identityImage?id={user.Id:D}")),
        ["descriptor"] = user.SubjectDescriptor,
    };

    private static string IdentityUrl(FakeAzureUser user) =>
        string.Create(CultureInfo.InvariantCulture, $"{IdentityHost}A{InstanceId}/_apis/Identities/{user.Id:D}");

    private JsonObject CommitJson(FakeAzureRepository repository, string sha) => new()
    {
        ["commitId"] = sha,
        ["url"] = ApiUrl(repository, "/commits/" + sha),
    };

    /// <summary>A ref of <c>GET …/refs</c> (MEASURED r5 3.3: <c>{name, objectId, creator, url}</c>; the creator the service identity, ASSUMED for branches).</summary>
    private JsonObject RefJson(FakeAzureRepository repository, string name, string sha) => new()
    {
        ["name"] = name,
        ["objectId"] = sha,
        ["creator"] = new JsonObject
        {
            ["displayName"] = "Microsoft.VisualStudio.Services.TFS",
            ["url"] = string.Create(CultureInfo.InvariantCulture, $"{IdentityHost}A{InstanceId}/_apis/Identities/{ServiceIdentityId}"),
            ["id"] = ServiceIdentityId,
        },
        ["url"] = ApiUrl(repository, "/refs?filter=" + Uri.EscapeDataString(name.StartsWith("refs/", StringComparison.Ordinal) ? name[5..] : name)),
    };

    /// <summary>A list's answer, <c>{"value": […], "count": n}</c> (MEASURED r5 2.1).</summary>
    private static JsonObject ListJson(IEnumerable<JsonNode> rows)
    {
        var value = new JsonArray();
        foreach (var row in rows)
        {
            value.Add(row);
        }

        return new JsonObject { ["value"] = value, ["count"] = value.Count };
    }

    /// <summary>
    /// An error body (MEASURED r5 1.4; MEASURED 2026-11-05 on the 400s and the 409 of stage 1):
    /// <c>{"$id":"1","innerException":null,"message","typeName","typeKey","errorCode":0,"eventId":3000}</c>,
    /// <paramref name="eventId"/> 0 for an empty title's (MEASURED 2026-11-05);
    /// <paramref name="customProperties"/> after <c>$id</c> (the expired token's, READ g1 G2).
    /// </summary>
    private static JsonObject ErrorJson(string message, string typeName, string typeKey, JsonObject? customProperties = null, int eventId = 3000)
    {
        var json = new JsonObject { ["$id"] = "1" };
        if (customProperties is not null)
        {
            json["customProperties"] = customProperties;
        }

        json["innerException"] = null;
        json["message"] = message;
        json["typeName"] = typeName;
        json["typeKey"] = typeKey;
        json["errorCode"] = 0;
        json["eventId"] = eventId;
        return json;
    }

    private static JsonObject Link(string href) => new() { ["href"] = href };

    private static byte[] Serialize(JsonNode node) => JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);

    /// <summary>Which answer a single pull request is: a read, a create's (with <c>labels</c>, MEASURED 2026-11-05 on the create with nothing ahead, ASSUMED alike for a create with commits ahead) or an update's that closed it (without <c>mergeStatus</c> and <c>lastMergeCommit</c>, MEASURED 2026-11-05 on an abandon).</summary>
    private enum PullAnswer
    {
        Read,
        Created,
        Closed,
    }

    /// <summary>
    /// A JSON answer: <c>Content-Type</c> with the served <c>api-version</c> on a 2xx (MEASURED r5 0.9;
    /// none before a version was served), none on an error (MEASURED 2026-11-05 on the version's 400,
    /// the creates' 409 and two 400s; ASSUMED on the other errors, the same exception filter), and
    /// <c>ActivityId</c>, <c>X-TFS-Session</c> and
    /// <c>X-VSS-E2EID</c> one id (MEASURED r5 1.2); no <c>ETag</c>, <c>Link</c> or continuation (MEASURED r5 7).
    /// </summary>
    private AuthRemoteResponse Json(int status, JsonNode body, string? served, IEnumerable<KeyValuePair<string, string>>? extra = null, bool bom = false)
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("Content-Type", "application/json; charset=utf-8" + (served is null || status >= 400 ? string.Empty : "; api-version=" + served)),
        };
        AddActivity(headers);
        headers.AddRange(extra ?? []);
        var bytes = Serialize(body);
        return new AuthRemoteResponse(status, Reason(status), headers, bom ? [.. Bom, .. bytes] : bytes);
    }

    /// <summary>A text answer, with the activity headers unless <paramref name="activity"/> is false (the unknown organization's 404 has none, MEASURED r5 1.3).</summary>
    private AuthRemoteResponse Text(int status, string contentType, string text, IEnumerable<KeyValuePair<string, string>>? extra = null, bool activity = true)
    {
        var headers = new List<KeyValuePair<string, string>> { KeyValuePair.Create("Content-Type", contentType) };
        if (activity)
        {
            AddActivity(headers);
        }

        headers.AddRange(extra ?? []);
        return new AuthRemoteResponse(status, Reason(status), headers, Utf8(text));
    }

    private void AddActivity(List<KeyValuePair<string, string>> headers)
    {
        var id = string.Create(CultureInfo.InvariantCulture, $"7b0aac71-0000-4a00-8000-{Interlocked.Increment(ref _activity):x12}");
        headers.Add(KeyValuePair.Create("ActivityId", id));
        headers.Add(KeyValuePair.Create("X-TFS-Session", id));
        headers.Add(KeyValuePair.Create("X-VSS-E2EID", id));
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        203 => "Non-Authoritative Information",
        302 => "Found",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        503 => "Service Unavailable",
        _ => "OK",
    };
}
