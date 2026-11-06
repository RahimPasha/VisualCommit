using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of Azure DevOps in process (docs/phase-7b-design.md 3.3, 3.4, 3.6): every route, the
/// checks in their order, the shapes and headers a client must survive, each fault once and then the
/// normal answer; Anonymous for no credential, a short password and <c>Bearer</c>; the empty 401 for
/// unknown, revoked and other organizations' tokens; the resource area both ways; the sign-in
/// redirects; a 401-character description; a create at 201 and 200, queued then succeeded; a hold;
/// and no request record holding a token. On the bare folders of a copy of "hosting7b", seeded here
/// as the scenario seeds them, but with harbor-co's project public.
/// </summary>
public sealed class FakeAzureDevOpsTests
{
    private const string Organization = "Fernhill-Labs";
    private const string Project = "Fernhill-Labs/Mobile%20App";
    private const string Connection = "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1";
    private const string Repositories = "Fernhill-Labs/_apis/git/repositories?api-version=7.1";
    private const string Pulls = Project + "/_apis/git/repositories/mobile-app/pullrequests";
    private const string AnonymousId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string Realm = "Basic realm=\"https://tfsprodfake.app.visualstudio.com/\"";

    /// <summary>A refused credential's one challenge: the realm a host of the service's measured form, without <c>.app.</c> (MEASURED 2026-11-05).</summary>
    private const string TokenRealm = "Basic realm=\"https://tfsprodfake.visualstudio.com/\"";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connection_data_gives_a_members_identity_and_the_anonymous_one_where_no_credential_counts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        var member = await SendAsync(fake, HttpMethod.Get, Connection, fixture.AdaToken);
        Assert.Equal(200, member.Status);
        Assert.Equal("application/json; charset=utf-8; api-version=7.1-preview.1", member.Header("Content-Type"));
        Assert.Equal(member.Header("ActivityId"), member.Header("X-TFS-Session"));
        Assert.Equal(member.Header("ActivityId"), member.Header("X-VSS-E2EID"));
        Assert.Null(member.Header("ETag"));
        Assert.Null(member.Header("Link"));
        var user = member.Json.GetProperty("authenticatedUser");
        // The identity's nine names in the service's order, customDisplayName among them (MEASURED
        // 2026-11-05, a Code-only token), the display name unless set.
        Assert.Equal(
            ["id", "descriptor", "subjectDescriptor", "providerDisplayName", "customDisplayName", "isActive", "properties", "resourceVersion", "metaTypeId"],
            user.EnumerateObject().Select(property => property.Name));
        Assert.Equal(fixture.Ada.Id.ToString(), user.GetProperty("id").GetString());
        Assert.Equal(("Ada Lovelace", "Ada Lovelace"), (user.GetProperty("providerDisplayName").GetString(), user.GetProperty("customDisplayName").GetString()));
        Assert.StartsWith("Microsoft.IdentityModel.Claims.ClaimsIdentity;", user.GetProperty("descriptor").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("\\ada.lovelace@example.com", user.GetProperty("descriptor").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("aad.", user.GetProperty("subjectDescriptor").GetString(), StringComparison.Ordinal);
        Assert.Contains("\"properties\":{\"Account\":{\"$type\":\"System.String\",\"$value\":\"ada.lovelace@example.com\"}}", member.Body, StringComparison.Ordinal);
        Assert.Equal(0, user.GetProperty("metaTypeId").GetInt32());
        Assert.Equal(user.GetRawText(), member.Json.GetProperty("authorizedUser").GetRawText());
        Assert.Equal(Fixture.AdaLogin(fixture), fake.Requests[^1].Login);

        fixture.Ada.CustomDisplayName = "Ada";
        fixture.Ada.MetaTypeId = 255;
        var custom = (await SendAsync(fake, HttpMethod.Get, Connection, fixture.AdaToken)).Json.GetProperty("authenticatedUser");
        Assert.Equal(("Ada", 255), (custom.GetProperty("customDisplayName").GetString(), custom.GetProperty("metaTypeId").GetInt32()));

        // Null leaves customDisplayName out, as g1 G3.2's sample 1 (READ).
        fixture.Ada.CustomDisplayName = null;
        var sampleOne = (await SendAsync(fake, HttpMethod.Get, Connection, fixture.AdaToken)).Json.GetProperty("authenticatedUser");
        Assert.Equal(
            ["id", "descriptor", "subjectDescriptor", "providerDisplayName", "isActive", "properties", "resourceVersion", "metaTypeId"],
            sampleOne.EnumerateObject().Select(property => property.Name));

        // harbor-co allows public projects: no credential, a password that is no PAT and Bearer are
        // each the Anonymous identity (MEASURED r5 1.2; READ g1 G0.2).
        const string HarborConnection = "harbor-co/_apis/connectionData?api-version=7.1-preview.1";
        foreach (var edit in new Action<HttpRequestMessage>[]
        {
            _ => { },
            request => request.Headers.Authorization = Basic("x-access-token", "short-value"),
            request => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.AuthorToken),
        })
        {
            var anonymous = await SendAsync(fake, HttpMethod.Get, HarborConnection, token: null, edit: edit);
            Assert.Equal(200, anonymous.Status);
            Assert.Contains(
                "{\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"descriptor\":\"System:PublicAccess;aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"providerDisplayName\":\"Anonymous\",\"customDisplayName\":\"Anonymous\",\"isActive\":true,\"properties\":{\"Account\":{\"$type\":\"System.String\",\"$value\":\"Anonymous\"}},\"resourceVersion\":2,\"metaTypeId\":255}",
                anonymous.Body,
                StringComparison.Ordinal);
            Assert.Null(fake.Requests[^1].Login);
        }

        // Fernhill-Labs is private, yet a Basic password that is no PAT is the Anonymous identity there
        // too: with the empty user, as the probe and the client send it, with and without Suppress
        // (MEASURED 2026-11-05, D373's short value); with a user, ASSUMED alike (the user ignored, READ
        // g1 G1.0; no path of the app's sends one to connectionData). A 200 that A1 refuses (6.4).
        foreach (var edit in new Action<HttpRequestMessage>[]
        {
            request => request.Headers.Authorization = Basic(string.Empty, "short-value"),
            request =>
            {
                request.Headers.Authorization = Basic(string.Empty, "short-value");
                Suppress(request);
            },
            request =>
            {
                request.Headers.Authorization = Basic("x-access-token", "short-value");
                Suppress(request);
            },
        })
        {
            var plain = await SendAsync(fake, HttpMethod.Get, Connection, token: null, edit: edit);
            Assert.Equal((200, "application/json; charset=utf-8; api-version=7.1-preview.1"), (plain.Status, plain.Header("Content-Type")));
            Assert.Equal(["authenticatedUser", "authorizedUser", "instanceId", "deploymentId", "deploymentType", "locationServiceData"], plain.Json.EnumerateObject().Select(property => property.Name));
            Assert.Equal((AnonymousId, "Anonymous"), (plain.Json.GetProperty("authenticatedUser").GetProperty("id").GetString(), plain.Json.GetProperty("authenticatedUser").GetProperty("providerDisplayName").GetString()));
            Assert.Empty(plain.WwwAuthenticate);
            Assert.Null(fake.Requests[^1].Login);
        }

        // No credential at all: the sign-in's 302 off the fake's origin, or 401 TF400813 with Suppress
        // (MEASURED r5 1.3).
        var redirected = await SendAsync(fake, HttpMethod.Get, Connection, token: null);
        Assert.Equal(302, redirected.Status);
        Assert.StartsWith("https://spsprodfake.vssps.visualstudio.com/_signin?", redirected.Header("Location"), StringComparison.Ordinal);
        Assert.Contains("Object moved", redirected.Body, StringComparison.Ordinal);
        var suppressed = await SendAsync(fake, HttpMethod.Get, Connection, token: null, edit: Suppress);
        AssertError(suppressed, 401, "UnauthorizedRequestException", $"TF400813: The user '{AnonymousId}' is not authorized to access this resource.");
        Assert.Equal(3, suppressed.WwwAuthenticate.Count);
        Assert.Contains(Realm, suppressed.WwwAuthenticate);
        Assert.StartsWith("https://spsprodfake.vssps.visualstudio.com/_signin?", suppressed.Header("X-TFS-FedAuthRedirect"), StringComparison.Ordinal);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_pat_shaped_token_that_is_unknown_revoked_or_another_organizations_gets_an_empty_401_and_an_expired_one_a_bom()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        var patShaped = new string('x', 76) + "AZDO" + "yyyy";
        Assert.Equal(84, patShaped.Length);

        foreach (var token in new[] { "azdo_FAKE-Nobody-9", patShaped, fixture.AuthorToken })
        {
            var refused = await SendAsync(fake, HttpMethod.Get, Connection, token);
            Assert.Equal((401, 0), (refused.Status, refused.Bytes.Length));
            Assert.Equal([TokenRealm], refused.WwwAuthenticate);
            Assert.Null(refused.Header("Content-Type"));
        }

        var other = fake.AddToken(Organization, fixture.Octo);
        Assert.Equal("azdo_FAKE-Fernhill-Labs-2", other);
        fake.Revoke(other);
        Assert.Equal((401, 0), ((await SendAsync(fake, HttpMethod.Get, Repositories, other)).Status, (await SendAsync(fake, HttpMethod.Get, Repositories, other)).Bytes.Length));

        var expiring = fake.AddToken(Organization, fixture.Octo);
        fake.Expire(expiring);
        var expired = await SendAsync(fake, HttpMethod.Get, Repositories, expiring);
        Assert.Equal(401, expired.Status);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, expired.Bytes[..3]);
        var body = JsonDocument.Parse(expired.Bytes.AsMemory(3)).RootElement;
        Assert.Equal(("AccessCheckException", "Access Denied: The Personal Access Token used has expired."), (body.GetProperty("typeKey").GetString(), body.GetProperty("message").GetString()));
        Assert.Equal("00000000-0000-0000-0000-000000000000", body.GetProperty("customProperties").GetProperty("NamespaceId").GetString());
        Assert.All(fake.Requests.Where(request => request.Status == 401), request => Assert.Null(request.Login));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task The_organization_comes_first_then_the_version_in_the_services_words()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        foreach (var token in new[] { null, fixture.AdaToken })
        {
            var missing = await SendAsync(fake, HttpMethod.Get, "No-Such-Org/_apis/connectionData", token);
            Assert.Equal((404, "The resource cannot be found."), (missing.Status, missing.Body));
            Assert.Equal("text/plain; charset=utf-8", missing.Header("Content-Type"));
            Assert.Equal("The%20resource%20cannot%20be%20found.", missing.Header("X-TFS-ServiceError"));
            Assert.Null(missing.Header("ActivityId"));
        }

        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories", fixture.AdaToken), 400, "VssInvalidApiResourceVersionException",
            "The fake of Azure DevOps needs an api-version (D370): the request sent none. Api version string must be in the format: {Major}.{Minor}[-preview[.{ResourceVersion}]].");
        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=abc", fixture.AdaToken), 400, "VssInvalidApiResourceVersionException",
            "Invalid api version string: \"abc\". Api version string must be in the format: {Major}.{Minor}[-preview[.{ResourceVersion}]].");

        // A part beyond an int, or digits that are not ASCII (Arabic-Indic seven and one), is malformed too.
        foreach (var (sent, raw) in new[] { ("99999999999.0", "99999999999.0"), ("7.1-preview.99999999999", "7.1-preview.99999999999"), ("%D9%A7.%D9%A1", "٧.١") })
        {
            AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=" + sent, fixture.AdaToken), 400, "VssInvalidApiResourceVersionException",
                $"Invalid api version string: \"{raw}\". Api version string must be in the format: {{Major}}.{{Minor}}[-preview[.{{ResourceVersion}}]].");
        }
        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=7.3", fixture.AdaToken), 400, "VssVersionOutOfRangeException",
            "The requested REST API version of 7.3 is out of range for this server. The latest REST API version this server supports is 7.2.");
        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/connectionData?api-version=7.1", fixture.AdaToken), 400, "VssInvalidPreviewVersionException",
            "The requested version \"7.1\" of the resource is under preview. The -preview flag must be supplied in the api-version for such requests. For example: \"7.1-preview\"");
        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=7.2", fixture.AdaToken), 400, "VssInvalidPreviewVersionException",
            "The requested version \"7.2\" of the resource is under preview. The -preview flag must be supplied in the api-version for such requests. For example: \"7.2-preview\"");
        var unknownRevision = await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=7.1-preview.2", fixture.AdaToken);
        Assert.Equal((404, "Page not found", "text/plain; charset=utf-8"), (unknownRevision.Status, unknownRevision.Body, unknownRevision.Header("Content-Type")));
        var preview = await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories?api-version=7.2-preview", fixture.AdaToken);
        Assert.Equal((200, "application/json; charset=utf-8; api-version=7.2-preview.1"), (preview.Status, preview.Header("Content-Type")));
        var accepted = await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositories", fixture.AdaToken, edit: request => request.Headers.TryAddWithoutValidation("Accept", "application/json;api-version=7.1"));
        Assert.Equal((200, "application/json; charset=utf-8; api-version=7.1"), (accepted.Status, accepted.Header("Content-Type")));

        // An unknown route under _apis is "Page not found", after the organization's check.
        var route = await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/_apis/git/repositoriesX?api-version=7.1", fixture.AdaToken);
        Assert.Equal((404, "Page not found"), (route.Status, route.Body));
        Assert.Null(fake.Requests[^1].Route);
        Assert.Equal(404, (await SendAsync(fake, HttpMethod.Get, "No-Such-Org/_apis/git/repositories?api-version=abc", fixture.AdaToken)).Status);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task The_core_resource_area_gives_the_stored_spelling_at_the_root_and_under_the_organization_without_a_credential()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        const string Area = "_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf";

        foreach (var (path, token) in new[]
        {
            (Area + "?organizationName=fernhill-LABS&api-version=7.1-preview.1", (string?)null),
            (Area + "?accountName=FERNHILL-labs&api-version=5.0-preview.1", null),
            ("fernhill-labs/" + Area + "?api-version=7.1-preview.1", null),
            ("fernhill-labs/" + Area + "?api-version=7.1-preview.1", fixture.AdaToken),
            ("fernhill-labs/" + Area + "?api-version=7.1-preview.1", "azdo_FAKE-Nobody-9"),
        })
        {
            var answer = await SendAsync(fake, HttpMethod.Get, path, token);
            Assert.Equal(200, answer.Status);
            Assert.Equal("{\"id\":\"79134c72-4a58-4b42-976c-04e7115f32bf\",\"name\":\"core\",\"locationUrl\":\"http://127.0.0.1:2/Fernhill-Labs/\"}", answer.Body);
            Assert.Equal("areas", fake.Requests[^1].Route);
        }

        var unknown = await SendAsync(fake, HttpMethod.Get, Area + "?organizationName=no-such-org&api-version=7.1-preview.1");
        Assert.Equal((404, "{\"count\":1,\"value\":{\"Message\":\"TF400677: Host no-such-org does not exist.\"}}"), (unknown.Status, unknown.Body));
        Assert.Equal((404, "The resource cannot be found."), ((await SendAsync(fake, HttpMethod.Get, "no-such-org/" + Area + "?api-version=7.1-preview.1")).Status, (await SendAsync(fake, HttpMethod.Get, "no-such-org/" + Area + "?api-version=7.1-preview.1")).Body));
        AssertError(await SendAsync(fake, HttpMethod.Get, Area + "?organizationName=Fernhill-Labs&api-version=7.1"), 400, "VssInvalidPreviewVersionException", null);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Repositories_are_listed_in_creation_order_and_one_answers_with_its_links_and_parent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        var listed = await SendAsync(fake, HttpMethod.Get, Repositories + "&$top=1", fixture.AdaToken);
        Assert.Equal(200, listed.Status);
        var rows = listed.Json.GetProperty("value").EnumerateArray().ToList();
        Assert.Equal(["mobile-app", "mobile-app-fork", "sandbox", Scenarios.Hosting7bLongRepository], rows.Select(row => row.GetProperty("name").GetString()));
        Assert.Equal(4, listed.Json.GetProperty("count").GetInt32());
        Assert.Equal(["value", "count"], listed.Json.EnumerateObject().Select(property => property.Name));
        var mobile = rows[0];
        Assert.Equal(
            ["id", "name", "url", "project", "defaultBranch", "size", "remoteUrl", "sshUrl", "webUrl", "isDisabled", "isInMaintenance"],
            mobile.EnumerateObject().Select(property => property.Name));
        Assert.Equal("refs/heads/main", mobile.GetProperty("defaultBranch").GetString());
        Assert.Equal("http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", mobile.GetProperty("remoteUrl").GetString());
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", mobile.GetProperty("webUrl").GetString());
        Assert.Equal($"http://127.0.0.1:2/Fernhill-Labs/{fixture.MobileApp.Project.Id}/_apis/git/repositories/{fixture.MobileApp.Id}", mobile.GetProperty("url").GetString());
        var project = mobile.GetProperty("project");
        Assert.Equal(("Mobile App", "private", "wellFormed", "The mobile app."), (project.GetProperty("name").GetString(), project.GetProperty("visibility").GetString(), project.GetProperty("state").GetString(), project.GetProperty("description").GetString()));
        Assert.Equal("2026-01-02T09:00:00.0000000Z", project.GetProperty("lastUpdateTime").GetString());
        Assert.True(rows[1].GetProperty("isFork").GetBoolean());
        Assert.False(rows[1].TryGetProperty("parentRepository", out _));
        Assert.False(rows[2].TryGetProperty("defaultBranch", out _));
        Assert.Equal(0, rows[2].GetProperty("size").GetInt64());
        Assert.All(rows.Where(row => row.GetProperty("name").GetString() != "mobile-app-fork"), row => Assert.False(row.TryGetProperty("isFork", out _)));

        // One project's list; one repository by name in another case or by GUID.
        Assert.Equal(4, (await SendAsync(fake, HttpMethod.Get, Project + "/_apis/git/repositories?api-version=7.1", fixture.AdaToken)).Json.GetProperty("count").GetInt32());
        var byName = await SendAsync(fake, HttpMethod.Get, "FERNHILL-LABS/mobile%20app/_apis/git/repositories/MOBILE-APP-FORK?api-version=7.1", fixture.AdaToken);
        Assert.Equal(200, byName.Status);
        Assert.Equal("repo:fernhill-labs/mobile app/mobile-app-fork", fake.Requests[^1].Route);
        Assert.Equal(["self", "project", "web", "ssh", "commits", "refs", "pullRequests", "items", "pushes"], byName.Json.GetProperty("_links").EnumerateObject().Select(property => property.Name));
        var parent = byName.Json.GetProperty("parentRepository");
        Assert.Equal(("mobile-app", fixture.MobileApp.Id.ToString()), (parent.GetProperty("name").GetString(), parent.GetProperty("id").GetString()));
        Assert.False(parent.TryGetProperty("webUrl", out _));
        var byId = await SendAsync(fake, HttpMethod.Get, $"{Project}/_apis/git/repositories/{fixture.MobileApp.Id}?api-version=7.1", fixture.AdaToken);
        Assert.Equal(("mobile-app", "repo:fernhill-labs/mobile app/mobile-app"), (byId.Json.GetProperty("name").GetString(), fake.Requests[^1].Route));

        // The fake's own addresses, the project as its GUID, are answered, keyed by the stored names.
        foreach (var (address, key) in new[]
        {
            (mobile.GetProperty("url").GetString()!, "repo:fernhill-labs/mobile app/mobile-app"),
            (byName.Json.GetProperty("_links").GetProperty("self").GetProperty("href").GetString()!, "repo:fernhill-labs/mobile app/mobile-app-fork"),
        })
        {
            Assert.Contains($"/{fixture.MobileApp.Project.Id}/", address, StringComparison.Ordinal);
            var followed = await SendAsync(fake, HttpMethod.Get, address + "?api-version=7.1", fixture.AdaToken);
            Assert.Equal((200, key), (followed.Status, fake.Requests[^1].Route));
        }
        AssertError(await SendAsync(fake, HttpMethod.Get, Project + "/_apis/git/repositories/missing?api-version=7.1", fixture.AdaToken), 404, "GitRepositoryNotFoundException",
            "TF401019: The Git repository with name or identifier missing does not exist or you do not have permissions for the operation you are attempting.");
        AssertError(await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/Nowhere/_apis/git/repositories?api-version=7.1", fixture.AdaToken), 404, "ProjectDoesNotExistWithNameException", null);

        // A project hidden from Ada: not listed, and its repositories are not found.
        fixture.MobileApp.Project.Members = [fixture.Octo];
        Assert.Equal(0, (await SendAsync(fake, HttpMethod.Get, Repositories, fixture.AdaToken)).Json.GetProperty("count").GetInt32());
        AssertError(await SendAsync(fake, HttpMethod.Get, Project + "/_apis/git/repositories/mobile-app?api-version=7.1", fixture.AdaToken), 404, "GitRepositoryNotFoundException", null);
        fixture.MobileApp.Project.Members = [];

        // harbor-co's public project is read without a credential, but its organization's list is not.
        var anonymous = await SendAsync(fake, HttpMethod.Get, "harbor-co/Docks/_apis/git/repositories?api-version=7.1");
        Assert.Equal((200, "public"), (anonymous.Status, anonymous.Json.GetProperty("value")[0].GetProperty("project").GetProperty("visibility").GetString()));
        Assert.Equal(302, (await SendAsync(fake, HttpMethod.Get, "harbor-co/_apis/git/repositories?api-version=7.1")).Status);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Pull_requests_are_listed_newest_first_after_the_sync_with_their_descriptions_cut_and_read_alone_whole()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        fixture.AddScenarioPullRequests();
        var longText = new string('d', 401);
        var described = fake.AddPullRequest(fixture.MobileApp, "Try an idea", fixture.Octo, "topic/no-request", "main", description: longText);
        Assert.Equal(14, described.Id);

        var listed = await SendAsync(fake, HttpMethod.Get, Pulls + "?searchCriteria.status=active&$top=100&$skip=0&api-version=7.1", fixture.AdaToken);
        Assert.Equal(200, listed.Status);
        var rows = listed.Json.GetProperty("value").EnumerateArray().ToList();
        Assert.Equal([14, 13, 12, 11], rows.Select(row => row.GetProperty("pullRequestId").GetInt32()));
        Assert.Equal(4, listed.Json.GetProperty("count").GetInt32());
        Assert.Equal(400, rows[0].GetProperty("description").GetString()!.Length);
        Assert.All(rows, row =>
        {
            Assert.Equal("succeeded", row.GetProperty("mergeStatus").GetString());
            Assert.False(row.TryGetProperty("artifactId", out _));
            Assert.False(row.TryGetProperty("_links", out _));
            Assert.DoesNotContain(row.EnumerateObject(), property => property.Name.Contains("web", StringComparison.OrdinalIgnoreCase));
            var project = row.GetProperty("repository").GetProperty("project");
            Assert.Equal(("unchanged", "unchanged", "0001-01-01T00:00:00"), (project.GetProperty("state").GetString(), project.GetProperty("visibility").GetString(), project.GetProperty("lastUpdateTime").GetString()));
        });
        var eleven = rows[3];
        Assert.Equal(
            ["repository", "pullRequestId", "codeReviewId", "status", "createdBy", "creationDate", "title", "sourceRefName", "targetRefName", "mergeStatus", "isDraft", "mergeId", "lastMergeSourceCommit", "lastMergeTargetCommit", "lastMergeCommit", "reviewers", "url", "supportsIterations"],
            eleven.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("refs/heads/feature/sign-in", "refs/heads/main", false), (eleven.GetProperty("sourceRefName").GetString(), eleven.GetProperty("targetRefName").GetString(), eleven.GetProperty("isDraft").GetBoolean()));
        Assert.Equal(fixture.MobileApp.Id.ToString(), eleven.GetProperty("repository").GetProperty("id").GetString());
        var creator = eleven.GetProperty("createdBy");
        Assert.Equal(("Ada Lovelace", "ada.lovelace@example.com"), (creator.GetProperty("displayName").GetString(), creator.GetProperty("uniqueName").GetString()));
        Assert.StartsWith("https://spsprodfake.vssps.visualstudio.com/", creator.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal("2026-01-02T09:00:00.0000000Z", eleven.GetProperty("creationDate").GetString());
        Assert.True(rows[2].GetProperty("isDraft").GetBoolean());
        var fork = rows[1];
        Assert.Equal("refs/pull/13/source", fork.GetProperty("sourceRefName").GetString());
        var forkSource = fork.GetProperty("forkSource");
        Assert.Equal(["name", "objectId", "repository"], forkSource.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("refs/heads/fix-typo", JsonValueKind.Null, "mobile-app-fork"), (forkSource.GetProperty("name").GetString(), forkSource.GetProperty("objectId").ValueKind, forkSource.GetProperty("repository").GetProperty("name").GetString()));
        Assert.False(rows[3].TryGetProperty("forkSource", out _));

        // Alone: the whole description, artifactId and _links; by project alone too.
        var alone = await SendAsync(fake, HttpMethod.Get, Pulls + "/14?api-version=7.1", fixture.AdaToken);
        Assert.Equal((401, "pull:fernhill-labs/mobile app/14"), (alone.Json.GetProperty("description").GetString()!.Length, fake.Requests[^1].Route));
        Assert.Equal($"vstfs:///Git/PullRequestId/{fixture.MobileApp.Project.Id}/{fixture.MobileApp.Id}/14", alone.Json.GetProperty("artifactId").GetString());
        Assert.Equal(["self", "repository", "workItems", "sourceBranch", "targetBranch", "statuses", "sourceCommit", "targetCommit", "createdBy", "iterations"], alone.Json.GetProperty("_links").EnumerateObject().Select(property => property.Name));

        // Alone, the member order as served and the repository whole: its project with its own state,
        // visibility and date, not the list's "unchanged" and zero date (r5's single GET).
        Assert.Equal(
            ["repository", "pullRequestId", "codeReviewId", "status", "createdBy", "creationDate", "title", "description", "sourceRefName", "targetRefName", "mergeStatus", "isDraft", "mergeId", "lastMergeSourceCommit", "lastMergeTargetCommit", "lastMergeCommit", "reviewers", "url", "_links", "supportsIterations", "artifactId"],
            alone.Json.EnumerateObject().Select(property => property.Name));
        var aloneRepository = alone.Json.GetProperty("repository");
        Assert.Equal(
            ["id", "name", "url", "project", "size", "remoteUrl", "sshUrl", "webUrl", "isDisabled", "isInMaintenance"],
            aloneRepository.EnumerateObject().Select(property => property.Name));
        var aloneProject = aloneRepository.GetProperty("project");
        Assert.Equal(
            ("The mobile app.", "wellFormed", "private", "2026-01-02T09:00:00.0000000Z"),
            (aloneProject.GetProperty("description").GetString(), aloneProject.GetProperty("state").GetString(), aloneProject.GetProperty("visibility").GetString(), aloneProject.GetProperty("lastUpdateTime").GetString()));

        // Its own url, the project as its GUID, is answered.
        var selfUrl = alone.Json.GetProperty("url").GetString()!;
        Assert.Contains($"/{fixture.MobileApp.Project.Id}/", selfUrl, StringComparison.Ordinal);
        var self = await SendAsync(fake, HttpMethod.Get, selfUrl + "?api-version=7.1", fixture.AdaToken);
        Assert.Equal((200, 14, "pull:fernhill-labs/mobile app/14"), (self.Status, self.Json.GetProperty("pullRequestId").GetInt32(), fake.Requests[^1].Route));
        Assert.Equal(11,(await SendAsync(fake, HttpMethod.Get, Project + "/_apis/git/pullrequests/11?api-version=7.1", fixture.AdaToken)).Json.GetProperty("pullRequestId").GetInt32());
        AssertError(await SendAsync(fake, HttpMethod.Get, Pulls + "/99?api-version=7.1", fixture.AdaToken), 404, "GitPullRequestNotFoundException", "TF401180: The requested pull request was not found.");

        // Statuses, pages and the page's cap.
        described.Status = FakeAzurePullRequestStatus.Abandoned;
        Assert.Equal([14], Numbers(await SendAsync(fake, HttpMethod.Get, Pulls + "?searchCriteria.status=abandoned&api-version=7.1", fixture.AdaToken)));
        Assert.Equal([14, 13, 12, 11], Numbers(await SendAsync(fake, HttpMethod.Get, Pulls + "?searchCriteria.status=all&api-version=7.1", fixture.AdaToken)));
        Assert.Equal([13, 12, 11], Numbers(await SendAsync(fake, HttpMethod.Get, Pulls + "?searchCriteria.status=bogus&api-version=7.1", fixture.AdaToken)));
        var page = await SendAsync(fake, HttpMethod.Get, Pulls + "?$top=1&$skip=1&api-version=7.1", fixture.AdaToken);
        Assert.Equal([12], Numbers(page));
        Assert.Equal(1, page.Json.GetProperty("count").GetInt32());
        fake.MaxPerPage = 2;
        Assert.Equal([13, 12], Numbers(await SendAsync(fake, HttpMethod.Get, Pulls + "?$top=50&api-version=7.1", fixture.AdaToken)));
        Assert.Equal([13, 12, 11], Numbers(await SendAsync(fake, HttpMethod.Get, Pulls + "?api-version=7.1", fixture.AdaToken)));

        // A public project's pull request read without a credential has no address or image.
        fake.AddPullRequest(fixture.Manifest, Scenarios.Hosting7bManifestRequestTitle, fixture.Author, "update-manifest", "main", id: 5);
        var anonymous = await SendAsync(fake, HttpMethod.Get, "harbor-co/Docks/_apis/git/repositories/manifest/pullrequests?api-version=7.1");
        var anonymousCreator = anonymous.Json.GetProperty("value")[0].GetProperty("createdBy");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "Test Author"), (anonymousCreator.GetProperty("uniqueName").ValueKind, anonymousCreator.GetProperty("imageUrl").ValueKind, anonymousCreator.GetProperty("displayName").GetString()));
        Assert.Equal("queued", anonymous.Json.GetProperty("value")[0].GetProperty("mergeStatus").GetString());
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Without_top_a_page_holds_101_pull_requests_whatever_max_per_page_is_and_a_top_sent_is_capped()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        var sandbox = fake.Repository(Organization, "Mobile App", "sandbox");

        // 102 pull requests marked Conflicts: no merge commit for any (design 3.5's cost rule).
        for (var n = 0; n < 102; n++)
        {
            fake.AddPullRequest(sandbox, "Try idea " + n, fixture.Octo, "topic-" + n, "main").Conflicts = true;
        }

        const string SandboxPulls = Project + "/_apis/git/repositories/sandbox/pullrequests";
        Assert.Equal(100, fake.MaxPerPage);
        var whole = await SendAsync(fake, HttpMethod.Get, SandboxPulls + "?api-version=7.1", fixture.AdaToken);
        Assert.Equal((200, 101), (whole.Status, whole.Json.GetProperty("count").GetInt32()));
        Assert.Equal(Enumerable.Range(2, 101).Reverse(), Numbers(whole));
        var asked = await SendAsync(fake, HttpMethod.Get, SandboxPulls + "?$top=150&api-version=7.1", fixture.AdaToken);
        Assert.Equal(100, asked.Json.GetProperty("count").GetInt32());
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_repositorys_answer_survives_files_vanishing_under_its_objects_while_they_are_counted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        // As git's object writes and receive-pack's quarantine do: files made and removed under objects/.
        var churn = Path.Combine(fixture.MobileApp.BarePath, "objects", "incoming-churn");
        Directory.CreateDirectory(churn);
        var names = Enumerable.Range(0, 32).Select(n => Path.Combine(churn, $"tmp_obj_{n}")).ToArray();
        using var stop = new CancellationTokenSource();
        var churner = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                foreach (var name in names)
                {
                    try
                    {
                        File.WriteAllBytes(name, [1]);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                    }
                }

                foreach (var name in names)
                {
                    try
                    {
                        File.Delete(name);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                    }
                }
            }
        }, Cancelled);

        try
        {
            for (var i = 0; i < 200; i++)
            {
                var answer = await SendAsync(fake, HttpMethod.Get, Project + "/_apis/git/repositories/mobile-app?api-version=7.1", fixture.AdaToken);
                Assert.Equal(200, answer.Status);
            }
        }
        finally
        {
            await stop.CancelAsync();
            await churner;
        }

        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_create_is_queued_then_succeeded_at_201_and_200_and_its_refusals_are_the_services()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        fixture.AddScenarioPullRequests();
        static string Body(string source, string target, string? title = "Try an idea", string extra = "") =>
            $"{{\"sourceRefName\":\"{source}\",\"targetRefName\":\"{target}\"{(title is null ? string.Empty : $",\"title\":\"{title}\"")},\"description\":\"An idea.\",\"isDraft\":false{extra}}}";

        var created = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/topic/no-request", "refs/heads/main"));
        Assert.Equal(201, created.Status);
        Assert.Equal("create:fernhill-labs/mobile app/mobile-app", fake.Requests[^1].Route);
        Assert.Equal((14, "active", "queued", "Ada Lovelace"), (created.Json.GetProperty("pullRequestId").GetInt32(), created.Json.GetProperty("status").GetString(), created.Json.GetProperty("mergeStatus").GetString(), created.Json.GetProperty("createdBy").GetProperty("displayName").GetString()));
        Assert.False(created.Json.TryGetProperty("lastMergeCommit", out _));
        Assert.True(created.Json.TryGetProperty("artifactId", out _));

        // The create's members in the service's order, labels among them (MEASURED 2026-11-05 on the
        // create with nothing ahead, below; ASSUMED alike for this one, with commits ahead).
        Assert.Equal(
            ["repository", "pullRequestId", "codeReviewId", "status", "createdBy", "creationDate", "title", "description", "sourceRefName", "targetRefName", "mergeStatus", "isDraft", "mergeId", "lastMergeSourceCommit", "lastMergeTargetCommit", "reviewers", "labels", "url", "_links", "supportsIterations", "artifactId"],
            created.Json.EnumerateObject().Select(property => property.Name));
        var read = await SendAsync(fake, HttpMethod.Get, Pulls + "/14?api-version=7.1", fixture.AdaToken);
        Assert.Equal("succeeded", read.Json.GetProperty("mergeStatus").GetString());
        Assert.Equal(fake.PullRequests(fixture.MobileApp).Single(pull => pull.Id == 14).LastMergeCommit, read.Json.GetProperty("lastMergeCommit").GetProperty("commitId").GetString());

        fake.CreateStatus = 200;
        var draft = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/docs/guide", "refs/heads/feature/sign-in", extra: ",\"isDraft\":true").Replace("\"isDraft\":false,", string.Empty, StringComparison.Ordinal));
        Assert.Equal((200, 15, true), (draft.Status, draft.Json.GetProperty("pullRequestId").GetInt32(), draft.Json.GetProperty("isDraft").GetBoolean()));

        // Nothing ahead is made too, its answer with labels and without lastMergeCommit (MEASURED
        // 2026-11-05); a fork's source by forkSource.repository.id.
        var level = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/main", "refs/heads/docs/guide"));
        Assert.Equal(200, level.Status);
        Assert.Equal(
            ["repository", "pullRequestId", "codeReviewId", "status", "createdBy", "creationDate", "title", "description", "sourceRefName", "targetRefName", "mergeStatus", "isDraft", "mergeId", "lastMergeSourceCommit", "lastMergeTargetCommit", "reviewers", "labels", "url", "_links", "supportsIterations", "artifactId"],
            level.Json.EnumerateObject().Select(property => property.Name));
        var fromFork = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/fix-typo", "refs/heads/docs/guide", extra: $",\"forkSource\":{{\"repository\":{{\"id\":\"{fixture.Fork.Id}\"}}}}"));
        Assert.Equal(("refs/pull/17/source", "mobile-app-fork"), (fromFork.Json.GetProperty("sourceRefName").GetString(), fromFork.Json.GetProperty("forkSource").GetProperty("repository").GetProperty("name").GetString()));

        // The refusals.
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/feature/sign-in", "refs/heads/main")), 409, "GitPullRequestExistsException",
            "TF401179: An active pull request for the source and target branch already exists.");
        const string CannotBeActivated = "TF401398: The pull request cannot be activated because the source and/or the target branch no longer exists, or the requested refs are not branches";
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/missing", "refs/heads/main")), 400, "GitPullRequestCannotBeActivated", CannotBeActivated);
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("topic/no-request", "main")), 400, "GitPullRequestCannotBeActivated", CannotBeActivated);
        // An empty title (MEASURED 2026-11-05, eventId 0) and a missing one (ASSUMED alike); the
        // errors' Content-Type has no api-version (MEASURED 2026-11-05).
        const string TitleMissing = "Invalid argument value.\r\nParameter name: Invalid pull request title.";
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/topic/no-request", "refs/heads/docs/guide", title: null)), 400, "InvalidArgumentValueException", TitleMissing, eventId: 0);
        var empty = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/topic/no-request", "refs/heads/docs/guide", title: string.Empty));
        AssertError(empty, 400, "InvalidArgumentValueException", TitleMissing, eventId: 0);
        Assert.Equal("application/json; charset=utf-8", empty.Header("Content-Type"));
        var duplicate = await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/feature/sign-in", "refs/heads/main"));
        Assert.Equal("application/json; charset=utf-8", duplicate.Header("Content-Type"));
        var readOnly = fake.AddToken(Organization, fixture.Ada, codeWrite: false);
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", readOnly, Body("refs/heads/topic/no-request", "refs/heads/docs/guide")), 401, "UnauthorizedRequestException",
            $"TF400813: The user '{fixture.Ada.Id}' is not authorized to access this resource.");
        fixture.MobileApp.Contributors = [fixture.Octo];
        AssertError(await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, Body("refs/heads/topic/no-request", "refs/heads/docs/guide")), 403, "GitNeedsPermissionException",
            "TF401027: You need the Git 'PullRequestContribute' permission to perform this action. Details: identity 'Ada Lovelace', scope 'repository'.");
        fixture.MobileApp.Contributors = [];
        Assert.Equal(302, (await SendAsync(fake, HttpMethod.Post, "harbor-co/Docks/_apis/git/repositories/manifest/pullrequests?api-version=7.1", token: null, Body("refs/heads/main", "refs/heads/main"))).Status);
        Assert.Equal([11, 12, 13, 14, 15, 16, 17], fake.PullRequests(fixture.MobileApp).Select(pull => pull.Id));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task An_update_abandons_or_renames_and_refuses_any_other_member()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        fixture.AddScenarioPullRequests();

        var abandoned = await SendAsync(fake, HttpMethod.Patch, Pulls + "/11?api-version=7.1", fixture.AdaToken, "{\"status\":\"abandoned\"}");
        Assert.Equal((200, "abandoned", "update:fernhill-labs/mobile app/11"), (abandoned.Status, abandoned.Json.GetProperty("status").GetString(), fake.Requests[^1].Route));
        Assert.True(abandoned.Json.TryGetProperty("closedDate", out _));

        // The abandon's answer has no mergeStatus or lastMergeCommit (MEASURED 2026-11-05), a read
        // of an active one has both.
        Assert.False(abandoned.Json.TryGetProperty("mergeStatus", out _));
        Assert.False(abandoned.Json.TryGetProperty("lastMergeCommit", out _));
        Assert.Equal(["reviewers", "url", "_links", "supportsIterations", "artifactId"], abandoned.Json.EnumerateObject().Select(property => property.Name).TakeLast(5));
        Assert.True((await SendAsync(fake, HttpMethod.Get, Pulls + "/12?api-version=7.1", fixture.AdaToken)).Json.TryGetProperty("mergeStatus", out _));
        Assert.Equal(FakeAzurePullRequestStatus.Abandoned, fake.PullRequests(fixture.MobileApp)[0].Status);
        var renamed = await SendAsync(fake, HttpMethod.Patch, Pulls + "/12?api-version=7.1", fixture.AdaToken, "{\"title\":\"Write the user guide\",\"description\":\"Longer.\"}");
        Assert.Equal(("Write the user guide", "Longer."), (renamed.Json.GetProperty("title").GetString(), renamed.Json.GetProperty("description").GetString()));
        AssertError(await SendAsync(fake, HttpMethod.Patch, Pulls + "/12?api-version=7.1", fixture.AdaToken, "{\"isDraft\":false}"), 400, "InvalidArgumentValueException", null);
        AssertError(await SendAsync(fake, HttpMethod.Patch, Pulls + "/12?api-version=7.1", fixture.AdaToken, "{\"status\":\"merged\"}"), 400, "InvalidArgumentValueException", null);
        AssertError(await SendAsync(fake, HttpMethod.Patch, Pulls + "/99?api-version=7.1", fixture.AdaToken, "{\"status\":\"abandoned\"}"), 404, "GitPullRequestNotFoundException", null);
        Assert.Equal("Write the user guide", fake.PullRequests(fixture.MobileApp)[1].Title);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Refs_are_listed_after_the_sync_and_deleted_and_created_with_the_services_results()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        fixture.AddScenarioPullRequests();
        const string Refs = Project + "/_apis/git/repositories/mobile-app/refs";

        var pulls = await SendAsync(fake, HttpMethod.Get, Refs + "?filter=pull/&api-version=7.1", fixture.AdaToken);
        Assert.Equal(("refs:fernhill-labs/mobile app/mobile-app", 3), (fake.Requests[^1].Route, pulls.Json.GetProperty("count").GetInt32()));
        Assert.Equal(["refs/pull/11/merge", "refs/pull/12/merge", "refs/pull/13/merge"], pulls.Json.GetProperty("value").EnumerateArray().Select(row => row.GetProperty("name").GetString()));
        Assert.Equal("Microsoft.VisualStudio.Services.TFS", pulls.Json.GetProperty("value")[0].GetProperty("creator").GetProperty("displayName").GetString());
        // A conflicting pull request has no merge commit and loses its merge ref at the next sync (READ r5 8.4).
        fake.PullRequests(fixture.MobileApp)[1].Conflicts = true;
        var conflicting = await SendAsync(fake, HttpMethod.Get, Pulls + "/12?api-version=7.1", fixture.AdaToken);
        Assert.Equal("conflicts", conflicting.Json.GetProperty("mergeStatus").GetString());
        Assert.False(conflicting.Json.TryGetProperty("lastMergeCommit", out _));
        Assert.Equal(["refs/pull/11/merge", "refs/pull/13/merge"], (await SendAsync(fake, HttpMethod.Get, Refs + "?filter=pull/&api-version=7.1", fixture.AdaToken)).Json.GetProperty("value").EnumerateArray().Select(row => row.GetProperty("name").GetString()));
        var heads = (await SendAsync(fake, HttpMethod.Get, Refs + "?filter=heads/&api-version=7.1", fixture.AdaToken)).Json.GetProperty("value").EnumerateArray().ToDictionary(row => row.GetProperty("name").GetString()!, row => row.GetProperty("objectId").GetString()!);
        Assert.Equal(["refs/heads/docs/guide", "refs/heads/feature/sign-in", "refs/heads/main", "refs/heads/topic/no-request"], heads.Keys);

        var tip = heads["refs/heads/topic/no-request"];
        var deleted = await SendAsync(fake, HttpMethod.Post, Refs + "?api-version=7.1", fixture.AdaToken,
            $"[{{\"name\":\"refs/heads/topic/no-request\",\"oldObjectId\":\"{tip}\",\"newObjectId\":\"0000000000000000000000000000000000000000\"}},{{\"name\":\"refs/heads/docs/guide\",\"oldObjectId\":\"{tip}\",\"newObjectId\":\"0000000000000000000000000000000000000000\"}},{{\"name\":\"refs/pull/11/merge\",\"oldObjectId\":\"{tip}\",\"newObjectId\":\"0000000000000000000000000000000000000000\"}}]");
        Assert.Equal(200, deleted.Status);
        var results = deleted.Json.GetProperty("value").EnumerateArray().Select(row => (row.GetProperty("updateStatus").GetString(), row.GetProperty("success").GetBoolean())).ToList();
        Assert.Equal([("succeeded", true), ("staleOldObjectId", false), ("invalidRefName", false)], results);
        // Each update's seven members, without r5's customMessage and rejectedBy (MEASURED 2026-11-05, the clean-up's delete).
        string[] updateMembers = ["repositoryId", "name", "oldObjectId", "newObjectId", "isLocked", "updateStatus", "success"];
        Assert.Equal(updateMembers, deleted.Json.GetProperty("value")[0].EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("refs/heads/topic/no-request", (await SendAsync(fake, HttpMethod.Get, Refs + "?filter=heads/&api-version=7.1", fixture.AdaToken)).Body, StringComparison.Ordinal);

        // A create (oldObjectId forty zeros) at a commit of the repository: 200, succeeded, the branch
        // there (MEASURED 2026-11-05: the live command's -even branch at main's tip; its members ASSUMED
        // alike). A create of a ref that is there, staleOldObjectId; at no commit, unresolvableToCommit
        // (READ enum, ASSUMED); a move from one commit to another, 400 in the fake's words (D370).
        var main = heads["refs/heads/main"];
        var made = await SendAsync(fake, HttpMethod.Post, Refs + "?api-version=7.1", fixture.AdaToken,
            $"[{{\"name\":\"refs/heads/main-even\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}},{{\"name\":\"refs/heads/main\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}},{{\"name\":\"refs/heads/nowhere\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{new string('e', 40)}\"}}]");
        Assert.Equal(200, made.Status);
        var createdRow = made.Json.GetProperty("value")[0];
        Assert.Equal(updateMembers, createdRow.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("refs/heads/main-even", "0000000000000000000000000000000000000000", main), (createdRow.GetProperty("name").GetString(), createdRow.GetProperty("oldObjectId").GetString(), createdRow.GetProperty("newObjectId").GetString()));
        Assert.Equal([("succeeded", true), ("staleOldObjectId", false), ("unresolvableToCommit", false)], made.Json.GetProperty("value").EnumerateArray().Select(row => (row.GetProperty("updateStatus").GetString(), row.GetProperty("success").GetBoolean())));
        var after = (await SendAsync(fake, HttpMethod.Get, Refs + "?filter=heads/&api-version=7.1", fixture.AdaToken)).Json.GetProperty("value").EnumerateArray().ToDictionary(row => row.GetProperty("name").GetString()!, row => row.GetProperty("objectId").GetString()!);
        Assert.Equal((main, main, false), (after["refs/heads/main-even"], after["refs/heads/main"], after.ContainsKey("refs/heads/nowhere")));
        // A create whose name is a folder of a ref there (refs/heads/feature beside the scenario's
        // feature/sign-in), has one as its folder (main-even/x), or meets an earlier create of the
        // same batch (batch, then batch/inner): refNameConflict with HTTP 200, the others written
        // (READ enum, the status ASSUMED); never git's own refusal of the whole batch.
        var conflicts = await SendAsync(fake, HttpMethod.Post, Refs + "?api-version=7.1", fixture.AdaToken,
            $"[{{\"name\":\"refs/heads/feature\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}},{{\"name\":\"refs/heads/Main-Even/x\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}},{{\"name\":\"refs/heads/batch\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}},{{\"name\":\"refs/heads/batch/inner\",\"oldObjectId\":\"0000000000000000000000000000000000000000\",\"newObjectId\":\"{main}\"}}]");
        Assert.Equal(200, conflicts.Status);
        Assert.Equal([("refNameConflict", false), ("refNameConflict", false), ("succeeded", true), ("refNameConflict", false)], conflicts.Json.GetProperty("value").EnumerateArray().Select(row => (row.GetProperty("updateStatus").GetString(), row.GetProperty("success").GetBoolean())));
        var afterConflicts = (await SendAsync(fake, HttpMethod.Get, Refs + "?filter=heads/&api-version=7.1", fixture.AdaToken)).Json.GetProperty("value").EnumerateArray().Select(row => row.GetProperty("name").GetString()).ToList();
        Assert.Equal((true, false, false, true), (afterConflicts.Contains("refs/heads/batch"), afterConflicts.Contains("refs/heads/batch/inner"), afterConflicts.Contains("refs/heads/feature"), afterConflicts.Contains("refs/heads/feature/sign-in")));
        AssertError(await SendAsync(fake, HttpMethod.Post, Refs + "?api-version=7.1", fixture.AdaToken, $"[{{\"name\":\"refs/heads/main-even\",\"oldObjectId\":\"{main}\",\"newObjectId\":\"{tip}\"}}]"), 400, "InvalidArgumentValueException", null);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Each_fault_is_answered_once_and_then_the_normal_answer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        var route = "connection:" + Organization;

        async Task<Answer> FailOnceAsync(FakeAzureDevOpsFault fault, string? token = null, Action<HttpRequestMessage>? edit = null)
        {
            fake.FailNext(route, fault);
            var failed = await SendAsync(fake, HttpMethod.Get, Connection, token ?? fixture.AdaToken, edit: edit);
            var normal = await SendAsync(fake, HttpMethod.Get, Connection, fixture.AdaToken);
            Assert.Equal(200, normal.Status);
            Assert.Equal(fixture.Ada.Id.ToString(), normal.Json.GetProperty("authenticatedUser").GetProperty("id").GetString());
            return failed;
        }

        var invalid = await FailOnceAsync(FakeAzureDevOpsFault.InvalidToken);
        Assert.Equal((401, 0), (invalid.Status, invalid.Bytes.Length));
        AssertError(await FailOnceAsync(FakeAzureDevOpsFault.Unauthorized), 401, "UnauthorizedRequestException", $"TF400813: The user '{fixture.Ada.Id}' is not authorized to access this resource.");
        var expired = await FailOnceAsync(FakeAzureDevOpsFault.Expired);
        Assert.Equal((401, (byte)0xEF), (expired.Status, expired.Bytes[0]));
        var redirect = await FailOnceAsync(FakeAzureDevOpsFault.SignInRedirect, edit: Suppress);
        Assert.Equal(302, redirect.Status);
        Assert.StartsWith("http://127.0.0.1:2/_signin?", redirect.Header("Location"), StringComparison.Ordinal);
        var nonAuthoritative = await FailOnceAsync(FakeAzureDevOpsFault.NonAuthoritative);
        Assert.Equal((203, "text/html; charset=utf-8"), (nonAuthoritative.Status, nonAuthoritative.Header("Content-Type")));
        var anonymous = await FailOnceAsync(FakeAzureDevOpsFault.AnonymousIdentity);
        Assert.Equal((200, AnonymousId), (anonymous.Status, anonymous.Json.GetProperty("authenticatedUser").GetProperty("id").GetString()));
        var throttled = await FailOnceAsync(FakeAzureDevOpsFault.Throttled);
        AssertError(throttled, 429, "RequestBlockedException", "TF400733: The request has been canceled: Request was blocked due to exceeding usage of resource Core in namespace User.");
        Assert.Equal(("30", "0", null), (throttled.Header("Retry-After"), throttled.Header("X-RateLimit-Remaining"), throttled.Header("X-RateLimit-Delay")));
        var delayed = await FailOnceAsync(FakeAzureDevOpsFault.Delayed);
        Assert.Equal((200, "0.500", "1"), (delayed.Status, delayed.Header("X-RateLimit-Delay"), delayed.Header("Retry-After")));
        AssertError(await FailOnceAsync(FakeAzureDevOpsFault.VersionRefused), 400, "VssVersionOutOfRangeException", null);
        AssertError(await FailOnceAsync(FakeAzureDevOpsFault.NotFound), 404, "GitRepositoryNotFoundException", null);
        var routeMissing = await FailOnceAsync(FakeAzureDevOpsFault.RouteMissing);
        Assert.Equal((404, "Page not found"), (routeMissing.Status, routeMissing.Body));
        var organizationMissing = await FailOnceAsync(FakeAzureDevOpsFault.OrganizationMissing);
        Assert.Equal((404, "The resource cannot be found.", null), (organizationMissing.Status, organizationMissing.Body, organizationMissing.Header("ActivityId")));
        var serverError = await FailOnceAsync(FakeAzureDevOpsFault.ServerError);
        Assert.Equal(500, serverError.Status);
        Assert.StartsWith("TF400898: An Internal Error Occurred.", serverError.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
        var badGateway = await FailOnceAsync(FakeAzureDevOpsFault.BadGateway);
        Assert.Equal((503, "text/html; charset=utf-8"), (badGateway.Status, badGateway.Header("Content-Type")));
        var malformed = await FailOnceAsync(FakeAzureDevOpsFault.Malformed);
        Assert.Equal(200, malformed.Status);
        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(malformed.Bytes));

        // On a pull request's route NotFound is TF401180; a create made in full loses its answer.
        fixture.AddScenarioPullRequests();
        fake.FailNext("PULL:fernhill-labs/mobile app/11", FakeAzureDevOpsFault.NotFound);
        AssertError(await SendAsync(fake, HttpMethod.Get, Pulls + "/11?api-version=7.1", fixture.AdaToken), 404, "GitPullRequestNotFoundException", null);
        Assert.Equal(200, (await SendAsync(fake, HttpMethod.Get, Pulls + "/11?api-version=7.1", fixture.AdaToken)).Status);
        fake.FailNext("create:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.CreatedThenDropped);
        var lost = await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken,
            "{\"sourceRefName\":\"refs/heads/topic/no-request\",\"targetRefName\":\"refs/heads/main\",\"title\":\"Try an idea\"}"));
        Assert.Equal("The fake of Azure DevOps closed the connection without an answer.", lost.Message);
        Assert.Contains(fake.PullRequests(fixture.MobileApp), pull => pull.Id == 14 && pull.Title == "Try an idea");
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_held_route_waits_pages_answer_any_path_and_no_request_record_holds_a_token()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        var hold = fake.HoldNext("Connection:FERNHILL-LABS");
        Assert.Equal("connection:fernhill-labs", hold.Route);
        var sent = SendAsync(fake, HttpMethod.Get, Connection, fixture.AdaToken);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        Assert.False(sent.IsCompleted);
        hold.Dispose();
        Assert.Equal(200, (await sent.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Status);

        foreach (var path in new[] { "Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11", "Fernhill-Labs/_usersSettings/tokens", "no/such/page" })
        {
            var page = await SendAsync(fake, HttpMethod.Get, path);
            Assert.Equal((200, "text/html; charset=utf-8", "page"), (page.Status, page.Header("Content-Type"), fake.Requests[^1].Route));
        }

        // In process git's paths are not served.
        var git = await SendAsync(fake, HttpMethod.Get, "Fernhill-Labs/Mobile%20App/_git/mobile-app/info/refs?service=git-upload-pack", fixture.AdaToken);
        Assert.Equal((404, "Not found.\n"), (git.Status, git.Body));

        fixture.AddScenarioPullRequests();
        await SendAsync(fake, HttpMethod.Post, Pulls + "?api-version=7.1", fixture.AdaToken, "{\"sourceRefName\":\"refs/heads/topic/no-request\",\"targetRefName\":\"refs/heads/main\",\"title\":\"Try an idea\"}");
        Assert.All(fake.Requests, request => Assert.DoesNotContain("azdo_FAKE", request.ToString(), StringComparison.Ordinal));
        Assert.Contains(fake.Requests, request => request.Login == Fixture.AdaLogin(fixture));
        Assert.Empty(fake.Errors);
    }

    private static List<int> Numbers(Answer answer) =>
        [.. answer.Json.GetProperty("value").EnumerateArray().Select(row => row.GetProperty("pullRequestId").GetInt32())];

    private static void Suppress(HttpRequestMessage request) => request.Headers.TryAddWithoutValidation("X-TFS-FedAuthRedirect", "Suppress");

    private static AuthenticationHeaderValue Basic(string user, string password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));

    private static void AssertError(Answer answer, int status, string typeKey, string? message, int eventId = 3000)
    {
        Assert.Equal(status, answer.Status);
        var json = answer.Json;
        Assert.Equal(["$id", "innerException", "message", "typeName", "typeKey", "errorCode", "eventId"], json.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("1", typeKey, 0, eventId), (json.GetProperty("$id").GetString(), json.GetProperty("typeKey").GetString(), json.GetProperty("errorCode").GetInt32(), json.GetProperty("eventId").GetInt32()));
        if (message is not null)
        {
            Assert.Equal(message, json.GetProperty("message").GetString());
        }
    }

    /// <summary>Sends one request in process, the token as <c>Basic base64(":" + token)</c> (r5 5).</summary>
    private static async Task<Answer> SendAsync(FakeAzureDevOps fake, HttpMethod method, string pathAndQuery, string? token = null, string? body = null, Action<HttpRequestMessage>? edit = null)
    {
        using var client = new HttpClient(fake.CreateHandler());
        using var request = new HttpRequestMessage(method, new Uri(fake.Endpoints.WebBase, pathAndQuery));
        if (token is not null)
        {
            request.Headers.Authorization = Basic(string.Empty, token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        edit?.Invoke(request);
        using var response = await client.SendAsync(request, Cancelled);
        var bytes = await response.Content.ReadAsByteArrayAsync(Cancelled);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.NonValidated.Concat(response.Content.Headers.NonValidated))
        {
            headers[name] = string.Join(", ", values);
        }

        var challenges = response.Headers.NonValidated.TryGetValues("WWW-Authenticate", out var found) ? found.ToList() : [];
        return new Answer((int)response.StatusCode, bytes, headers, challenges);
    }

    private sealed record Answer(int Status, byte[] Bytes, IReadOnlyDictionary<string, string> Headers, IReadOnlyList<string> WwwAuthenticate)
    {
        public string Body => Encoding.UTF8.GetString(Bytes);

        public JsonElement Json => JsonDocument.Parse(Bytes).RootElement.Clone();

        public string? Header(string name) => Headers.GetValueOrDefault(name);
    }

    /// <summary>A copy of "hosting7b" with its fake of Azure DevOps in process, seeded as the scenario but for harbor-co's public project and its user's token.</summary>
    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(TempRepo copy, FakeAzureDevOps fake)
        {
            Copy = copy;
            Fake = fake;
        }

        public TempRepo Copy { get; }

        public FakeAzureDevOps Fake { get; }

        public FakeAzureUser Ada { get; private set; } = null!;

        public FakeAzureUser Octo { get; private set; } = null!;

        public FakeAzureUser Author { get; private set; } = null!;

        public string AdaToken { get; private set; } = null!;

        public string AuthorToken { get; private set; } = null!;

        public FakeAzureRepository MobileApp { get; private set; } = null!;

        public FakeAzureRepository Fork { get; private set; } = null!;

        public FakeAzureRepository Manifest { get; private set; } = null!;

        public static string AdaLogin(Fixture fixture) => $"Fernhill-Labs/{fixture.Ada.Id}";

        public static async Task<Fixture> CreateAsync()
        {
            var copy = await Scenarios.Hosting7bAsync();
            var fixture = new Fixture(copy, FakeAzureDevOps.InProcess(copy.SiblingPath(Scenarios.AzureDevOpsFolder)));
            var fake = fixture.Fake;
            fake.AddOrganization(Organization);
            fake.AddProject(Organization, "Mobile App", description: "The mobile app.");
            fixture.Ada = fake.AddUser(Organization, Scenarios.AdaName);
            fixture.Octo = fake.AddUser(Organization, Scenarios.OctoName);
            fixture.AdaToken = fake.AddToken(Organization, fixture.Ada);
            fake.AddOrganization("harbor-co", publicProjects: true);
            fake.AddProject("harbor-co", "Docks", isPublic: true);
            fixture.Author = fake.AddUser("harbor-co", TempRepo.AuthorName);
            fixture.AuthorToken = fake.AddToken("harbor-co", fixture.Author);
            fixture.MobileApp = await fake.AddRepositoryAsync(Organization, "Mobile App", "mobile-app", ct: Cancelled);
            fixture.Fork = await fake.AddRepositoryAsync(Organization, "Mobile App", "mobile-app-fork", forkOf: fixture.MobileApp, ct: Cancelled);
            await fake.AddRepositoryAsync(Organization, "Mobile App", "sandbox", ct: Cancelled);
            await fake.AddRepositoryAsync(Organization, "Mobile App", Scenarios.Hosting7bLongRepository, ct: Cancelled);
            fixture.Manifest = await fake.AddRepositoryAsync("harbor-co", "Docks", "manifest", ct: Cancelled);
            Assert.Equal(("azdo_FAKE-Fernhill-Labs-1", "azdo_FAKE-harbor-co-1"), (fixture.AdaToken, fixture.AuthorToken));
            return fixture;
        }

        /// <summary>mobile-app's !11, !12 (a draft) and !13 (from the fork), as the scenario's.</summary>
        public void AddScenarioPullRequests()
        {
            Fake.AddPullRequest(MobileApp, "Add the sign-in page", Ada, "feature/sign-in", "main", id: 11);
            Fake.AddPullRequest(MobileApp, "Write the guide", Octo, "docs/guide", "main", draft: true, id: 12);
            Fake.AddPullRequest(MobileApp, "Fix a typo in the greeting", Ada, "fix-typo", "main", fork: Fork, id: 13);
        }

        public async ValueTask DisposeAsync()
        {
            await Fake.DisposeAsync();
            Copy.Dispose();
        }
    }
}
