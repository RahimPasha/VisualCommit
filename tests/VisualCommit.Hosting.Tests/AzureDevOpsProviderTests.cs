using System.Net;
using System.Text;
using System.Web;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Azure DevOps as a hosting service (docs/phase-7b-design.md 6.4 to 6.6), against the fake in
/// process on a copy of "hosting7b"'s bare folders, seeded as the scenario seeds them, and scripts
/// where the fake cannot answer as asked: A1's account from exactly two reads, its refusals each
/// after one request or none; A2's order, project, empty and disabled repositories and a fork's
/// parent; A3's parent; A4's drafts, forks, addresses and authors, and its walk of 301 rows; A5's
/// body, a fork after one read, 200 and 201, the refusals, one POST, another organization; the
/// other members; and Azure DevOps named on every failure. Made-up names only.
/// ASSUMED answers these tests rest on, for WP-LIVE's first stage (D342, design 6.8): connectionData's
/// 404 for an unknown organization read with a token (D374's probe), the core resource area read
/// with a token, the create's POST by the repository's name, and a message naming an e-mail; kept as
/// marked (D375): a fork's create body and the fork shapes A2 and A4 read.
/// </summary>
public sealed class AzureDevOpsProviderTests
{
    private const string Organization = "Fernhill-Labs";
    private const string Project = "Mobile App";
    private const string RepositoryPath = "/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A1_gives_the_organizations_account_in_its_spelling_from_exactly_two_reads()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Ada.MetaTypeId = 255;

        var result = await fixture.Provider.CheckTokenAsync(fixture.AdaToken, "fernhill-labs", Cancelled);

        var account = result.Value!;
        Assert.Equal(new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "fernhill-labs"), account.Key);
        Assert.Equal(("Fernhill-Labs", 0L, "Ada Lovelace", (Uri?)null, true), (account.Login, account.Id, account.Name, account.AvatarUrl, account.UseForGit));
        Assert.Equal(fixture.Ada.Id.ToString("D"), account.UserId);
        Assert.Equal(
            ["GET /fernhill-labs/_apis/connectionData?api-version=7.1-preview.1", $"GET /fernhill-labs/_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?api-version=7.1-preview.1"],
            fixture.Sent());
        Assert.All(fixture.Recorder.Sent, sent => Assert.StartsWith("Basic ", sent.Header("Authorization"), StringComparison.Ordinal));
        Assert.Empty(fixture.Log.Lines);

        // The organization as either URL form the fake has: its first segment on the web base.
        var fromUrl = await fixture.Provider.CheckTokenAsync(fixture.AdaToken, "http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", Cancelled);
        Assert.Equal("Fernhill-Labs", fromUrl.Value!.Login);
        Assert.DoesNotContain("@", account.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A1_names_the_member_by_a_display_name_that_is_no_address()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;
        var cases = new (FakeAzureUser User, string? Name)[]
        {
            (fake.AddUser(Organization, "grace.hopper@example.com"), null),
            (Custom(fake.AddUser(Organization, "Octo Cat Two"), "Ada"), "Ada"),
            (Custom(fake.AddUser(Organization, "x.y@example.com"), "y.z@example.com"), null),
            (Custom(fake.AddUser(Organization, "Linus Two"), "someone ＠ example"), "someone ＠ example"),
            (Custom(fake.AddUser(Organization, "Linus Three"), "a＠b"), "Linus Three"),
        };
        foreach (var (user, name) in cases)
        {
            var account = (await fixture.Provider.CheckTokenAsync(fake.AddToken(Organization, user), Organization, Cancelled)).Value!;
            Assert.Equal(name, account.Name);
            Assert.DoesNotContain("example.com", account.ToString(), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("@", fixture.Log.Written, StringComparison.Ordinal);

        static FakeAzureUser Custom(FakeAzureUser user, string customName)
        {
            user.CustomDisplayName = customName;
            return user;
        }
    }

    [Fact]
    public async Task A1_refuses_the_anonymous_answer_an_unknown_organization_a_401_and_a_203_each_after_one_request()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fake = fixture.Fake;

        fake.FailNext("connection:fernhill-labs", FakeAzureDevOpsFault.AnonymousIdentity);
        var anonymous = (await fixture.Provider.CheckTokenAsync(fixture.AdaToken, Organization, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.BadCredentials, AzureDevOpsProvider.AnonymousDetail, HostingProvider.AzureDevOps), (anonymous.Kind, anonymous.Detail, anonymous.Provider));
        Assert.Equal(["GET /Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1"], fixture.Sent());
        Assert.Equal("Azure DevOps: GET /Fernhill-Labs/_apis/connectionData failed: BadCredentials (request " + anonymous.RequestId + ").", fixture.Log.Lines[^1].Message);

        // An unknown organization: connectionData's 404 (MEASURED r5 1.3 without a credential; with one ASSUMED until D374's probe).
        var nowhere = (await fixture.Provider.CheckTokenAsync(fixture.AdaToken, "vc-none-0123456789ab", Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.NotFound, HostingProvider.AzureDevOps), (nowhere.Kind, nowhere.Provider));
        Assert.Equal(2, fixture.Recorder.Sent.Count);

        var unknown = (await fixture.Provider.CheckTokenAsync("azdo_FAKE-Nobody-9", Organization, Cancelled)).Failure!;
        Assert.Equal(HostingFailureKind.BadCredentials, unknown.Kind);
        Assert.Equal(3, fixture.Recorder.Sent.Count);

        fake.FailNext("connection:fernhill-labs", FakeAzureDevOpsFault.NonAuthoritative);
        Assert.Equal(HostingFailureKind.BadCredentials, (await fixture.Provider.CheckTokenAsync(fixture.AdaToken, Organization, Cancelled)).Failure!.Kind);
        Assert.Equal(4, fixture.Recorder.Sent.Count);

        // A PAT-shaped token of Azure DevOps' own form goes to the service, which refuses it.
        var patShaped = new string('x', 76) + "AZDO" + "yyyy";
        Assert.Equal(HostingFailureKind.BadCredentials, (await fixture.Provider.CheckTokenAsync(patShaped, Organization, Cancelled)).Failure!.Kind);
        Assert.Equal(5, fixture.Recorder.Sent.Count);
        Assert.DoesNotContain(fixture.Recorder.Sent, sent => sent.Url.AbsolutePath.Contains("resourceAreas", StringComparison.Ordinal));
    }

    /// <summary>
    /// connectionData answers the fake does not give, each refused after that one request: the
    /// anonymous shapes with <see cref="AzureDevOpsProvider.AnonymousDetail"/> (6.4; WP-A words its
    /// text by it), the HTML 200 by the client's refusal of a success, without a detail.
    /// </summary>
    public static TheoryData<string, string?, string?> ScriptedConnections => new()
    {
        // The Anonymous descriptor with another id (r5 1.2's descriptor, MEASURED).
        { """{"authenticatedUser":{"id":"7b0a1111-0000-4a00-8000-000000000009","descriptor":"System:PublicAccess;7b0a1111-0000-4a00-8000-000000000009","providerDisplayName":"Visitor","properties":{"Account":{"$type":"System.String","$value":"Visitor"}}}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        { """{"authenticatedUser":{"id":"7b0a1111-0000-4a00-8000-000000000009","descriptor":"system:publicaccess;x","providerDisplayName":"Visitor"}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        // Account.$value Anonymous with another id and descriptor.
        { """{"authenticatedUser":{"id":"7b0a1111-0000-4a00-8000-000000000009","descriptor":"Microsoft.Fake;x","providerDisplayName":"Visitor","properties":{"Account":{"$type":"System.String","$value":"Anonymous"}}}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        { """{"authenticatedUser":{"id":"AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA","descriptor":"Microsoft.Fake;x"}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        { """{"authenticatedUser":{"descriptor":"Microsoft.Fake;x"}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        { """{"authenticatedUser":{"id":"not-a-guid","descriptor":"Microsoft.Fake;x"}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        { """{"authorizedUser":{"id":"7b0a1111-0000-4a00-8000-000000000009"}}""", "application/json", AzureDevOpsProvider.AnonymousDetail },
        // An HTML 200 (g1 G0.1).
        { "<!DOCTYPE html><html><head><title>Sign in</title></head></html>", "text/html", null },
    };

    [Theory]
    [MemberData(nameof(ScriptedConnections))]
    public async Task A1_refuses_an_anonymous_or_html_connection_after_that_one_request(string body, string? contentType, string? detail)
    {
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.OK, body, contentType));
        using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());

        var failure = (await provider.CheckTokenAsync("azdo_FAKE-Fernhill-Labs-1", "myorg", Cancelled)).Failure!;

        Assert.Equal((HostingFailureKind.BadCredentials, detail, HostingProvider.AzureDevOps), (failure.Kind, failure.Detail, failure.Provider));
        Assert.Single(recorder.Sent);
    }

    [Fact]
    public async Task A1_keeps_the_typed_name_when_the_resource_area_fails_or_names_no_spelling()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Fake.FailNext("areas", FakeAzureDevOpsFault.ServerError);
        var failed = (await fixture.Provider.CheckTokenAsync(fixture.AdaToken, "FERNHILL-labs", Cancelled)).Value!;
        Assert.Equal("FERNHILL-labs", failed.Login);
        Assert.Single(fixture.Log.Lines);
        Assert.Equal(2, fixture.Recorder.Sent.Count);

        const string Member = """{"authenticatedUser":{"id":"7b0a1111-0000-4a00-8000-000000000001","descriptor":"Microsoft.Fake;x","providerDisplayName":"Ada Lovelace","properties":{"Account":{"$type":"System.String","$value":"ada.lovelace@example.com"}}}}""";
        foreach (var location in new[] { "https://dev.azure.com/other-org/", "https://example.test/myorg/", "http://dev.azure.com/MyOrg/", "/MyOrg/", "https://dev.azure.com/MyOrg/x/", "https://dev.azure.com/", "not a url", "https://dev.azure.com/My%20Org/" })
        {
            var log = new ListLog();
            var recorder = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath.EndsWith("connectionData", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, Member, "application/json")
                : Answer(HttpStatusCode.OK, $$"""{"id":"{{AzureDevOpsApi.CoreAreaId}}","name":"core","locationUrl":{{System.Text.Json.JsonSerializer.Serialize(location)}}}""", "application/json"));
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, log, new ManualTimeProvider());
            var account = (await provider.CheckTokenAsync("azdo_FAKE-myorg-1", "myorg", Cancelled)).Value!;
            Assert.True(account.Login == "myorg", location);
            Assert.Equal(["Azure DevOps: the organization's resource area did not name it; the name as typed is kept."], log.Lines.Select(line => line.Message));
            Assert.Equal(2, recorder.Sent.Count);
        }

        var spelt = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath.EndsWith("connectionData", StringComparison.Ordinal)
            ? Answer(HttpStatusCode.OK, Member, "application/json")
            : Answer(HttpStatusCode.OK, $$"""{"id":"{{AzureDevOpsApi.CoreAreaId}}","name":"core","locationUrl":"https://DEV.azure.com/MyOrg/"}""", "application/json"));
        using var service = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => spelt, new ListLog(), new ManualTimeProvider());
        var named = (await service.CheckTokenAsync("azdo_FAKE-myorg-1", "https://myorg.visualstudio.com/", Cancelled)).Value!;
        Assert.Equal(("MyOrg", "myorg", "dev.azure.com"), (named.Login, named.Key.Login, named.Key.Host));
        Assert.Equal(["/myorg/_apis/connectionData", $"/myorg/_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}"], spelt.Sent.Select(sent => sent.Url.AbsolutePath));
        Assert.All(spelt.Sent, sent => Assert.Equal("dev.azure.com", sent.Url.Host));
    }

    [Fact]
    public async Task A1_sends_nothing_for_an_empty_token_a_bad_organization_or_another_providers_token()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cases = new (string Token, string? Organization, HostingFailureKind Kind, string? Detail)[]
        {
            (string.Empty, Organization, HostingFailureKind.BadCredentials, null),
            ("   ", Organization, HostingFailureKind.BadCredentials, null),
            ("azdo_FAKE-töken", Organization, HostingFailureKind.BadCredentials, null),
            ("azdo FAKE", Organization, HostingFailureKind.BadCredentials, null),
            ("github_pat_FAKE-octo-1", Organization, HostingFailureKind.BadCredentials, "other-provider:github"),
            ("glpat-FAKE-ada-1", Organization, HostingFailureKind.BadCredentials, "other-provider:gitlab"),
            (fixture.AdaToken, null, HostingFailureKind.Validation, null),
            (fixture.AdaToken, "my org", HostingFailureKind.Validation, null),
            (fixture.AdaToken, "https://example.com/Fernhill-Labs", HostingFailureKind.Validation, null),
            (fixture.AdaToken, "http://127.0.0.1:2/", HostingFailureKind.Validation, null),
        };
        foreach (var (token, organization, kind, detail) in cases)
        {
            var failure = (await fixture.Provider.CheckTokenAsync(token, organization, Cancelled)).Failure!;
            Assert.True((kind, detail, HostingProvider.AzureDevOps) == (failure.Kind, failure.Detail, failure.Provider), $"{token} {organization}");
        }

        Assert.Empty(fixture.Recorder.Sent);
        Assert.Empty(fixture.Fake.Requests);
    }

    /// <summary>
    /// Every redirect of connectionData is refused credentials, whatever its status (D360): a 303
    /// and a 300, which the core does not follow (the profile's table, one warning), and a 302
    /// without a Location, which the core stops (A1 then, a second warning). Elsewhere a 303 or a
    /// 305 to the sign-in is refused credentials as a 302 to it is (its host also with DNS's
    /// trailing dot), and one to nowhere else stays
    /// the core's "could not be read". Each after that one request; nothing is followed.
    /// </summary>
    [Fact]
    public async Task Every_redirect_of_A1_and_A3s_303_to_the_sign_in_are_refused_credentials_after_one_request()
    {
        var connection = "/myorg/_apis/connectionData failed: ";
        var answers = new (Func<HttpResponseMessage> Answer, string[] Warnings)[]
        {
            (() => Redirect((HttpStatusCode)303, "https://dev.azure.com/myorg/_apis/connectionData?api-version=7.1-preview.1&again=1"), [connection + "303"]),
            (() => Redirect((HttpStatusCode)300, "/myorg/_apis/connectionData?api-version=7.1-preview.1&again=1"), [connection + "300"]),
            (() => Redirect((HttpStatusCode)303, null), [connection + "303"]),
            (() => Redirect(HttpStatusCode.Found, null), [connection + "302 without Location", connection + "BadCredentials"]),
            (() => Redirect(HttpStatusCode.TemporaryRedirect, null), [connection + "307 without Location", connection + "BadCredentials"]),
        };
        foreach (var (answer, warnings) in answers)
        {
            var log = new ListLog();
            var recorder = new RecordingHttpHandler(_ => answer());
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, log, new ManualTimeProvider());

            var failure = (await provider.CheckTokenAsync("azdo_FAKE-myorg-1", "myorg", Cancelled)).Failure!;

            Assert.Equal((HostingFailureKind.BadCredentials, AzureDevOpsClient.SignInPageText, HostingProvider.AzureDevOps, (string?)null), (failure.Kind, failure.Message, failure.Provider, failure.Detail));
            Assert.Equal(("https://dev.azure.com/myorg/_apis/connectionData", "7b0aac71-0000-4a00-8000-0000000000ff"), (failure.Url!.AbsoluteUri, failure.RequestId));
            Assert.Single(recorder.Sent);
            Assert.Equal(warnings.Select(warning => "Azure DevOps: GET " + warning + " (request 7b0aac71-0000-4a00-8000-0000000000ff)."), log.Lines.Select(line => line.Message));
        }

        var repository = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org/proj", "repo");
        var cases = new (HttpStatusCode Status, string? Location, HostingFailureKind Kind)[]
        {
            ((HttpStatusCode)303, "https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com&reply_to=x", HostingFailureKind.BadCredentials),
            ((HttpStatusCode)303, "https://app.vssps.visualstudio.com/elsewhere", HostingFailureKind.BadCredentials),
            // The same host written with DNS's trailing dot, which Uri.IdnHost keeps, is the sign-in too.
            ((HttpStatusCode)303, "https://spsprodfake.vssps.visualstudio.com./x", HostingFailureKind.BadCredentials),
            (HttpStatusCode.Found, "https://spsprodfake.vssps.visualstudio.com./x", HostingFailureKind.BadCredentials),
            (HttpStatusCode.PermanentRedirect, "https://SPSPRODFAKE.VSSPS.VISUALSTUDIO.COM./x", HostingFailureKind.BadCredentials),
            ((HttpStatusCode)303, "https://spsprodfake.vssps.visualstudio.com./_signin", HostingFailureKind.BadCredentials),
            (HttpStatusCode.Found, "https://spsprodfake.vssps.visualstudio.com./_signin", HostingFailureKind.BadCredentials),
            ((HttpStatusCode)305, "/_signin?realm=dev.azure.com", HostingFailureKind.BadCredentials),
            ((HttpStatusCode)300, "https://dev.azure.com/_signin", HostingFailureKind.BadCredentials),
            ((HttpStatusCode)303, "/fake-org/proj/_apis/git/repositories/repo?api-version=7.1&again=1", HostingFailureKind.Malformed),
            ((HttpStatusCode)303, null, HostingFailureKind.Malformed),
            (HttpStatusCode.Found, null, HostingFailureKind.Malformed),
        };
        foreach (var (status, location, kind) in cases)
        {
            var recorder = new RecordingHttpHandler(_ => Redirect(status, location));
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());

            var failure = (await provider.ReadRepositoryAsync("azdo_FAKE-fake-org-1", repository, Cancelled)).Failure!;

            Assert.True((kind, HostingProvider.AzureDevOps) == (failure.Kind, failure.Provider), $"{(int)status} {location}");
            Assert.Equal(kind == HostingFailureKind.BadCredentials ? AzureDevOpsClient.SignInPageText : "Azure DevOps' answer could not be read.", failure.Message);
            Assert.Single(recorder.Sent);
        }
    }

    /// <summary>
    /// A redirect on the API's origin, in the request's organization, to a page of the web (a path
    /// whose first segment after the organization that starts with <c>_</c> is not <c>_apis</c>:
    /// the token page, a repository's web page, also with an <c>_apis</c> segment further on) is
    /// "could not be read" and nothing is sent there, for A3 and A4 alike; the warning says why.
    /// One to the API's routes (<c>_apis</c> right after the organization, the project or a team)
    /// is still followed.
    /// </summary>
    [Fact]
    public async Task A3_and_A4_refuse_a_redirect_to_the_organizations_web_pages_and_send_nothing_there()
    {
        var repository = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org/proj", "repo");
        const string Row = """{"id":"r1","name":"repo","project":{"name":"proj"},"remoteUrl":"https://dev.azure.com/fake-org/proj/_git/repo","webUrl":"https://dev.azure.com/fake-org/proj/_git/repo"}""";
        var pages = new[]
        {
            "/fake-org/_usersSettings/tokens", "https://dev.azure.com/fake-org/proj/_git/repo", "/FAKE-ORG/proj/_git/repo/pullrequests?_a=active",
            "/fake-org/proj/_git/repo/_apis", "/fake-org/_usersSettings/_apis/tokens", "/fake-org/proj/%5Fgit/repo/_apis/git",
        };
        foreach (var location in pages)
        {
            var log = new ListLog();

            // Only the two requests' own addresses are redirected; anything followed is answered with
            // the repository's row, so a followed redirect would read as a success.
            var recorder = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath.StartsWith("/fake-org/proj/_apis/git/repositories/repo", StringComparison.Ordinal)
                ? Redirect(HttpStatusCode.Found, location)
                : Answer(HttpStatusCode.OK, Row, "application/json"));
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, log, new ManualTimeProvider());

            var read = (await provider.ReadRepositoryAsync("azdo_FAKE-fake-org-1", repository, Cancelled)).Failure;
            var pulls = (await provider.ListPullRequestsAsync("azdo_FAKE-fake-org-1", repository, Cancelled)).Failure;

            Assert.True(read is { Kind: HostingFailureKind.Malformed, Provider: HostingProvider.AzureDevOps }, location);
            Assert.True(pulls is { Kind: HostingFailureKind.Malformed, Provider: HostingProvider.AzureDevOps }, location);
            Assert.Equal(2, recorder.Sent.Count);
            Assert.All(recorder.Sent, sent => Assert.StartsWith("/fake-org/proj/_apis/git/repositories/repo", sent.Url.AbsolutePath, StringComparison.Ordinal));
            Assert.Equal(
                [
                    "Azure DevOps: GET /fake-org/proj/_apis/git/repositories/repo failed: 302, redirected outside the API (request 7b0aac71-0000-4a00-8000-0000000000ff).",
                    "Azure DevOps: GET /fake-org/proj/_apis/git/repositories/repo/pullrequests failed: 302, redirected outside the API (request 7b0aac71-0000-4a00-8000-0000000000ff).",
                ],
                log.Lines.Select(line => line.Message));
        }

        var routes = new[]
        {
            "/fake-org/proj/_apis/git/repositories/repo?api-version=7.1&again=1", "/fake-org/_apis/git/repositories/repo?again=1",
            "/fake-org/proj/team/_apis/git/repositories/repo?again=1", "/FAKE-ORG/proj/%5FAPIS/git/repositories/repo?again=1",
        };
        foreach (var location in routes)
        {
            var recorder = new RecordingHttpHandler(request => request.RequestUri!.Query.Contains("again=1", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, Row, "application/json")
                : Redirect(HttpStatusCode.Found, location));
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());

            var read = await provider.ReadRepositoryAsync("azdo_FAKE-fake-org-1", repository, Cancelled);

            Assert.True(read.Failure is null, location);
            Assert.Equal(2, recorder.Sent.Count);
            Assert.Equal(new Uri(new Uri("https://dev.azure.com/"), location).AbsolutePath, recorder.Sent[1].Url.AbsolutePath);
        }
    }

    [Fact]
    public async Task A2_lists_by_name_reads_an_empty_repository_and_leaves_a_disabled_one_out()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Fake.Repository(Organization, Project, "sandbox").IsDisabled = true;
        var account = (await fixture.Provider.CheckTokenAsync(fixture.AdaToken, Organization, Cancelled)).Value!;

        var list = (await fixture.Provider.ListRepositoriesAsync(fixture.AdaToken, account, Cancelled)).Value!;

        Assert.Equal([Scenarios.Hosting7bLongRepository, "mobile-app", "mobile-app-fork"], list.Repositories.Select(info => info.Repository.Name));
        Assert.Equal((false, false), (list.MoreExist, list.SomeHiddenBySso));
        var mobile = list.Repositories[1];
        Assert.Equal(new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs/Mobile App", "mobile-app"), mobile.Repository);
        Assert.Equal(("main", true, false, false, (string?)null), (mobile.DefaultBranch, mobile.IsPrivate, mobile.IsFork, mobile.IsArchived, mobile.Description));
        Assert.Equal("http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", mobile.CloneUrl.OriginalString);
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", mobile.HtmlUrl.OriginalString);
        Assert.Equal((DateTimeOffset.MinValue, true, (HostedRepository?)null, (string?)null), (mobile.UpdatedAt, mobile.CanPush, mobile.Parent, mobile.ParentDefaultBranch));
        Assert.Equal(string.Empty, list.Repositories[0].DefaultBranch);
        Assert.True(list.Repositories[2].IsFork);
        Assert.Equal("GET /Fernhill-Labs/_apis/git/repositories?api-version=7.1", fixture.Sent()[^1]);

        fixture.Fake.FailNext("repos:fernhill-labs", FakeAzureDevOpsFault.ServerError);
        var failed = (await fixture.Provider.ListRepositoriesAsync(fixture.AdaToken, account, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.ServerError, HostingProvider.AzureDevOps), (failed.Kind, failed.Provider));

        // The live command's project alone (D352).
        var projectRecorder = new RecordingHttpHandler(fixture.Fake.CreateHandler());
        using var inProject = new AzureDevOpsProvider(fixture.Fake.Endpoints with { ListProject = Project }, () => projectRecorder, new ListLog(), new ManualTimeProvider());
        Assert.Equal(3, (await inProject.ListRepositoriesAsync(fixture.AdaToken, account, Cancelled)).Value!.Repositories.Count);
        Assert.Equal("/Fernhill-Labs/Mobile%20App/_apis/git/repositories?api-version=7.1", projectRecorder.Sent.Single().Url.PathAndQuery);
    }

    [Fact]
    public async Task A2_orders_by_name_then_project_reads_a_forks_parent_and_leaves_out_rows_it_cannot_use()
    {
        const string Rows = """
            {"value":[
              {"id":"1","name":"app","project":{"name":"Zeta","visibility":"public"},"remoteUrl":"https://myorg@dev.azure.com/myorg/Zeta/_git/app","webUrl":"https://dev.azure.com/myorg/Zeta/_git/app"},
              {"id":"2","name":"App","project":{"name":"alpha","visibility":"private"},"defaultBranch":"refs/heads/trunk","remoteUrl":"https://myorg@dev.azure.com/myorg/alpha/_git/App","webUrl":"https://dev.azure.com/myorg/alpha/_git/App","isFork":true,"parentRepository":{"id":"9","name":"origin","project":{"name":"alpha"}}},
              {"id":"3","name":"relative","project":{"name":"p"},"remoteUrl":"/myorg/p/_git/relative","webUrl":"https://dev.azure.com/myorg/p/_git/relative"},
              {"id":"4","name":"filed","project":{"name":"p"},"remoteUrl":"https://myorg@dev.azure.com/myorg/p/_git/filed","webUrl":"file:///myorg/p/_git/filed"},
              {"id":"5","name":"..","project":{"name":"p"},"remoteUrl":"https://dev.azure.com/myorg/p/_git/x","webUrl":"https://dev.azure.com/myorg/p/_git/x"},
              {"id":"6","name":"disabled","project":{"name":"p"},"remoteUrl":"https://dev.azure.com/myorg/p/_git/disabled","webUrl":"https://dev.azure.com/myorg/p/_git/disabled","isDisabled":true},
              {"id":"7","name":"bare","project":{"name":"p"}},
              {"id":"8","name":"a/b","project":{"name":"p"},"remoteUrl":"https://dev.azure.com/myorg/p/_git/a%2Fb","webUrl":"https://dev.azure.com/myorg/p/_git/a%2Fb"},
              {"id":"9","name":"x","project":{"name":"p\\q"},"remoteUrl":"https://dev.azure.com/myorg/p%5Cq/_git/x","webUrl":"https://dev.azure.com/myorg/p%5Cq/_git/x"}
            ],"count":9}
            """;
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.OK, Rows, "application/json"));
        using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());
        var account = new HostingAccount(new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "myorg"), "MyOrg", 0, null, null, true, "7b0a1111-0000-4a00-8000-000000000001");

        var list = (await provider.ListRepositoriesAsync("azdo_FAKE-myorg-1", account, Cancelled)).Value!;

        Assert.Equal(["MyOrg/alpha/App", "MyOrg/Zeta/app"], list.Repositories.Select(info => info.Repository.FullName));
        var fork = list.Repositories[0];
        Assert.Equal(("trunk", true, true), (fork.DefaultBranch, fork.IsPrivate, fork.IsFork));
        Assert.Equal(new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "MyOrg/alpha", "origin"), fork.Parent);
        Assert.Null(fork.ParentDefaultBranch);
        Assert.False(list.Repositories[1].IsPrivate);
        Assert.Equal("/MyOrg/_apis/git/repositories?api-version=7.1", recorder.Sent.Single().Url.PathAndQuery);
        Assert.DoesNotContain("include", recorder.Sent.Single().Url.Query, StringComparison.OrdinalIgnoreCase);

        // Another provider's or host's account, or a login outside the grammar, is refused without a request.
        foreach (var other in new[]
        {
            account with { Key = new AccountKey(HostingProvider.GitHub, "dev.azure.com", "myorg") },
            account with { Key = new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "myorg") },
            account with { Login = "my.org" },
        })
        {
            var failure = (await provider.ListRepositoriesAsync("azdo_FAKE-myorg-1", other, Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.Validation, HostingProvider.AzureDevOps), (failure.Kind, failure.Provider));
        }

        Assert.Single(recorder.Sent);
    }

    [Fact]
    public async Task A2_and_A3_take_a_rows_addresses_only_when_they_name_that_repository_with_no_other_user()
    {
        const string Token = "azdo_FAKE-fake-org-1";
        const string Web = "https://dev.azure.com/fake-org/proj/_git/repo";
        var account = new HostingAccount(new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org"), "fake-org", 0, null, null, true, "7b0a1111-0000-4a00-8000-000000000001");
        var repository = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org/proj", "repo");
        var refused = new (string RemoteUrl, string WebUrl)[]
        {
            // An e-mail as the user, another host with an e-mail in the path, the request's token echoed in the name.
            ("https://ada%40example.com@dev.azure.com/fake-org/proj/_git/repo", Web),
            ("https://fake-org@other.example/x.git", "https://other.example/ada@example.com"),
            ($"https://fake-org@dev.azure.com/fake-org/proj/_git/repo-{Token}", Web),

            // Another repository, project or organization; a user on the web address; a password.
            ("https://fake-org@dev.azure.com/fake-org/proj/_git/other", Web),
            ("https://fake-org@dev.azure.com/fake-org/other/_git/repo", Web),
            ("https://other-org@dev.azure.com/other-org/proj/_git/repo", Web),
            ("https://fake-org@dev.azure.com/fake-org/proj/_git/repo", "https://ada@dev.azure.com/fake-org/proj/_git/repo"),
            ("https://fake-org@dev.azure.com/fake-org/proj/_git/repo", "https://dev.azure.com/fake-org/proj/_git/repo-other"),
            ("https://fake-org:x@dev.azure.com/fake-org/proj/_git/repo", Web),
            ("https://x-access-token@dev.azure.com/fake-org/proj/_git/repo", Web),
            ("ssh://fake-org@ssh.dev.azure.com/v3/fake-org/proj/repo", Web),
        };
        var kept = new (string RemoteUrl, string WebUrl)[]
        {
            ("https://fake-org@dev.azure.com/fake-org/proj/_git/repo", Web),
            ("https://FAKE-ORG@dev.azure.com/Fake-Org/proj/_git/repo", "https://dev.azure.com/fake-org/proj/_git/repo/"),
            ("https://dev.azure.com/fake-org/proj/_git/repo", "https://fake-org.visualstudio.com/DefaultCollection/proj/_git/repo"),
        };
        foreach (var (remoteUrl, webUrl, keep) in refused.Select(row => (row.RemoteUrl, row.WebUrl, false)).Concat(kept.Select(row => (row.RemoteUrl, row.WebUrl, true))))
        {
            var row = $$"""{"id":"r1","name":"repo","project":{"name":"proj"},"remoteUrl":"{{remoteUrl}}","webUrl":"{{webUrl}}"}""";
            var recorder = new RecordingHttpHandler(request => Answer(HttpStatusCode.OK, request.RequestUri!.AbsolutePath.EndsWith("/_apis/git/repositories", StringComparison.Ordinal) ? $$"""{"count":1,"value":[{{row}}]}""" : row, "application/json"));
            using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());

            var list = (await provider.ListRepositoriesAsync(Token, account, Cancelled)).Value!.Repositories;
            var read = await provider.ReadRepositoryAsync(Token, repository, Cancelled);

            Assert.Equal(2, recorder.Sent.Count);
            if (keep)
            {
                var info = Assert.Single(list);
                Assert.Equal((repository, remoteUrl, webUrl), (info.Repository, info.CloneUrl.OriginalString, info.HtmlUrl.OriginalString));
                Assert.Equal(info, read.Value);
            }
            else
            {
                Assert.True(list.Count == 0, remoteUrl + " " + webUrl);
                Assert.Equal((HostingFailureKind.Malformed, HostingProvider.AzureDevOps), (read.Failure!.Kind, read.Failure.Provider));
            }

            Assert.DoesNotContain("example.com", string.Join(' ', list) + read.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(Token, string.Join(' ', list) + read.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A2_and_A4_refuse_a_null_row_as_malformed_and_nothing_throws()
    {
        // System.Text.Json does not hold a list's elements to their annotations, so a null row parses.
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.OK, """{"value":[null],"count":1}""", "application/json"));
        using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());
        var account = new HostingAccount(new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "myorg"), "MyOrg", 0, null, null, true, "7b0a1111-0000-4a00-8000-000000000001");

        var repositories = (await provider.ListRepositoriesAsync("azdo_FAKE-myorg-1", account, Cancelled)).Failure!;
        var pulls = (await provider.ListPullRequestsAsync("azdo_FAKE-myorg-1", new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "MyOrg/p", "app"), Cancelled)).Failure!;

        Assert.Equal((HostingFailureKind.Malformed, HostingProvider.AzureDevOps), (repositories.Kind, repositories.Provider));
        Assert.Equal((HostingFailureKind.Malformed, HostingProvider.AzureDevOps), (pulls.Kind, pulls.Provider));
        Assert.Equal(2, recorder.Sent.Count);
    }

    [Fact]
    public async Task A2_refuses_a_list_project_that_is_no_single_name_and_A4_drops_a_fork_source_of_such_a_name()
    {
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.OK, """
            {"value":[
              {"pullRequestId":3,"title":"Fix a typo","sourceRefName":"refs/pull/3/source","targetRefName":"refs/heads/main","forkSource":{"name":"refs/heads/fix-typo","repository":{"id":"9","name":"a/b","project":{"name":"p"}}}},
              {"pullRequestId":2,"title":"Fix another","sourceRefName":"refs/pull/2/source","targetRefName":"refs/heads/main","forkSource":{"name":"refs/heads/fix-more","repository":{"id":"8","name":"fork","project":{"name":"p\\q"}}}},
              {"pullRequestId":1,"title":"Fix a third","sourceRefName":"refs/pull/1/source","targetRefName":"refs/heads/main","forkSource":{"name":"refs/heads/fix-last","repository":{"id":"7","name":"fork","project":{"name":"p"}}}}
            ],"count":3}
            """, "application/json"));
        var account = new HostingAccount(new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "myorg"), "MyOrg", 0, null, null, true, "7b0a1111-0000-4a00-8000-000000000001");
        foreach (var project in new[] { string.Empty, ".", "..", "a/b", "a\\b" })
        {
            using var inProject = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service with { ListProject = project }, () => recorder, new ListLog(), new ManualTimeProvider());
            var failure = (await inProject.ListRepositoriesAsync("azdo_FAKE-myorg-1", account, Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.Validation, HostingProvider.AzureDevOps), (failure.Kind, failure.Provider));
        }

        Assert.Empty(recorder.Sent);
        using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());
        var pulls = (await provider.ListPullRequestsAsync("azdo_FAKE-myorg-1", new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "MyOrg/p", "app"), Cancelled)).Value!.PullRequests;

        Assert.Equal([("fix-typo", (HostedRepository?)null), ("fix-more", null), ("fix-last", new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "MyOrg/p", "fork"))], pulls.Select(pull => (pull.HeadRef, pull.HeadRepository)));
    }

    [Fact]
    public async Task A3_reads_a_repository_and_a_forks_parent_with_its_default_branch_by_a_second_read()
    {
        await using var fixture = await Fixture.CreateAsync();

        var fork = (await fixture.Provider.ReadRepositoryAsync(fixture.AdaToken, fixture.ForkRepository, Cancelled)).Value!;
        Assert.Equal((fixture.MobileRepository, "main", true), (fork.Parent, fork.ParentDefaultBranch, fork.IsFork));
        Assert.Equal(["GET " + RepositoryPath + "-fork?api-version=7.1", "GET " + RepositoryPath + "?api-version=7.1"], fixture.Sent());

        fixture.Fake.FailNext("repo:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.ServerError);
        var parentFailed = (await fixture.Provider.ReadRepositoryAsync(fixture.AdaToken, fixture.ForkRepository, Cancelled)).Value!;
        Assert.Equal((fixture.MobileRepository, (string?)null), (parentFailed.Parent, parentFailed.ParentDefaultBranch));

        var mobile = (await fixture.Provider.ReadRepositoryAsync(fixture.AdaToken, new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "fernhill-labs/mobile app", "MOBILE-APP"), Cancelled)).Value!;
        // The organization as asked, the project and name in the answer's spelling.
        Assert.Equal(("fernhill-labs/Mobile App", "mobile-app", (HostedRepository?)null), (mobile.Repository.Owner, mobile.Repository.Name, mobile.Parent));
        Assert.Equal(5, fixture.Recorder.Sent.Count);

        var missing = (await fixture.Provider.ReadRepositoryAsync(fixture.AdaToken, fixture.MobileRepository with { Name = "nowhere" }, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.NotFound, HostingProvider.AzureDevOps), (missing.Kind, missing.Provider));

        // Not of this service, or not <org>/<project>/<repo>: no request.
        var sent = fixture.Recorder.Sent.Count;
        foreach (var other in new[]
        {
            new HostedRepository(HostingProvider.GitHub, "127.0.0.1:2", "Fernhill-Labs/Mobile App", "mobile-app"),
            new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs/Mobile App", "mobile-app"),
            new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs", "mobile-app"),
            new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs/Mobile App/x", "mobile-app"),
            new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs/..", "mobile-app"),
        })
        {
            var refused = (await fixture.Provider.ReadRepositoryAsync(fixture.AdaToken, other, Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.Validation, HostingProvider.AzureDevOps), (refused.Kind, refused.Provider));
            Assert.Equal(HostingFailureKind.Validation, (await fixture.Provider.ListPullRequestsAsync(fixture.AdaToken, other, Cancelled)).Failure!.Kind);
            Assert.Equal(HostingFailureKind.Validation, (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, other, NewRequest("topic/no-request"), Cancelled)).Failure!.Kind);
        }

        Assert.Equal(sent, fixture.Recorder.Sent.Count);
    }

    [Fact]
    public async Task A4_lists_the_active_requests_with_drafts_forks_built_addresses_and_no_address_as_an_author()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddScenarioPullRequests();
        var eve = fixture.Fake.AddUser(Organization, "eve.example@example.com");
        fixture.Fake.AddPullRequest(fixture.MobileApp, "Try an idea", eve, "topic/no-request", "main", id: 14);
        var closed = fixture.Fake.AddPullRequest(fixture.MobileApp, "An old idea", fixture.Octo, "docs/guide", "main", id: 10);
        closed.Status = FakeAzurePullRequestStatus.Abandoned;

        var list = (await fixture.Provider.ListPullRequestsAsync(fixture.AdaToken, fixture.MobileRepository, Cancelled)).Value!;

        Assert.Equal([14, 13, 12, 11], list.PullRequests.Select(pull => pull.Number));
        Assert.False(list.MoreExist);
        Assert.Equal(["GET " + RepositoryPath + "/pullrequests?searchCriteria.status=active&api-version=7.1&$top=100&$skip=0"], fixture.Sent());
        var (eves, typo, guide, signIn) = (list.PullRequests[0], list.PullRequests[1], list.PullRequests[2], list.PullRequests[3]);
        Assert.Equal(("Add the sign-in page", "Ada Lovelace", false, "feature/sign-in", "main"), (signIn.Title, signIn.Author, signIn.IsDraft, signIn.HeadRef, signIn.BaseRef));
        Assert.Equal((fixture.MobileRepository, fixture.MobileRepository, false), (signIn.HeadRepository, signIn.BaseRepository, signIn.IsFromFork));
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11", signIn.HtmlUrl.AbsoluteUri);
        Assert.Matches("^[0-9a-f]{40}$", signIn.HeadSha);
        Assert.Equal(AppClocks.HarnessTime, signIn.CreatedAt);
        Assert.Equal(("Octo Cat", true), (guide.Author, guide.IsDraft));
        Assert.Equal(("fix-typo", fixture.ForkRepository, true), (typo.HeadRef, typo.HeadRepository, typo.IsFromFork));
        Assert.Null(eves.Author);
        Assert.All(list.PullRequests, pull => Assert.Null(fixture.Provider.PullRequestHeadRef(pull)));
        Assert.DoesNotContain(list.PullRequests, pull => pull.ToString().Contains('@', StringComparison.Ordinal));

        fixture.Fake.FailNext("pulls:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.Throttled);
        var limited = (await fixture.Provider.ListPullRequestsAsync(fixture.AdaToken, fixture.MobileRepository, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.RateLimited, HostingProvider.AzureDevOps), (limited.Kind, limited.Provider));
    }

    [Fact]
    public async Task A4_walks_three_hundred_and_one_rows_in_four_requests_below_the_repositorys_path()
    {
        var recorder = new RecordingHttpHandler(request =>
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var top = int.Parse(query["$top"]!, System.Globalization.CultureInfo.InvariantCulture);
            var skip = int.Parse(query["$skip"]!, System.Globalization.CultureInfo.InvariantCulture);
            var rows = Enumerable.Range(1, 301).Reverse().Skip(skip).Take(top)
                .Select(id => $$"""{"pullRequestId":{{id}},"title":"Request {{id}}","createdBy":{"displayName":"Ada Lovelace"},"creationDate":"2026-01-02T09:00:00.0000000Z","sourceRefName":"refs/heads/b{{id}}","targetRefName":"refs/heads/main","isDraft":false}""");
            return Answer(HttpStatusCode.OK, $$"""{"value":[{{string.Join(',', rows)}}],"count":{{Math.Min(top, Math.Max(0, 301 - skip))}}}""", "application/json");
        });
        using var provider = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => recorder, new ListLog(), new ManualTimeProvider());
        var repository = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs/Mobile App", "mobile-app");

        var list = (await provider.ListPullRequestsAsync("azdo_FAKE-Fernhill-Labs-1", repository, Cancelled)).Value!;

        Assert.Equal(300, list.PullRequests.Count);
        Assert.True(list.MoreExist);
        Assert.Equal((301, 2), (list.PullRequests[0].Number, list.PullRequests[^1].Number));
        Assert.Equal(["$top=100&$skip=0", "$top=100&$skip=100", "$top=100&$skip=200", "$top=1&$skip=300"], recorder.Sent.Select(sent => sent.Url.Query[sent.Url.Query.IndexOf("$top", StringComparison.Ordinal)..]));
        Assert.All(recorder.Sent, sent => Assert.StartsWith(RepositoryPath + "/pullrequests?", sent.Url.PathAndQuery, StringComparison.Ordinal));
        Assert.Equal("https://dev.azure.com/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/301", list.PullRequests[0].HtmlUrl.AbsoluteUri);
    }

    [Fact]
    public async Task A5_creates_once_with_its_body_at_201_and_200_and_reads_the_answer()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, new NewPullRequest("Try an idea", "An idea.", "topic/no-request", "someone-else", "main", Draft: true), Cancelled)).Value!;

        Assert.Equal((1, "Try an idea", true, "topic/no-request", "main", "Ada Lovelace"), (created.Number, created.Title, created.IsDraft, created.HeadRef, created.BaseRef, created.Author));
        Assert.Equal((fixture.MobileRepository, false), (created.HeadRepository, created.IsFromFork));
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/1", created.HtmlUrl.AbsoluteUri);
        Assert.Equal(["POST " + RepositoryPath + "/pullrequests?api-version=7.1"], fixture.Sent());
        Assert.Equal("""{"sourceRefName":"refs/heads/topic/no-request","targetRefName":"refs/heads/main","title":"Try an idea","description":"An idea.","isDraft":true}""", fixture.Recorder.Sent[0].Body);
        Assert.Equal(201, fixture.Fake.Requests[^1].Status);

        fixture.Fake.CreateStatus = 200;
        var at200 = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("docs/guide"), Cancelled)).Value!;
        Assert.Equal((2, "docs/guide", false), (at200.Number, at200.HeadRef, at200.IsDraft));
        Assert.Equal(200, fixture.Fake.Requests[^1].Status);
        Assert.Equal(2, fixture.Fake.Requests.Count(request => request.Method == "POST"));
    }

    [Fact]
    public async Task A5_refusals_carry_their_reason_by_type_key_each_after_one_post()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("topic/no-request"), Cancelled)).Succeeded);

        var cases = new (NewPullRequest Request, HostingFailureKind Kind, PullRequestRefusal Refusal)[]
        {
            (NewRequest("topic/no-request"), HostingFailureKind.Validation, PullRequestRefusal.AlreadyExists),
            (NewRequest("nowhere"), HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing),
            (NewRequest("feature/sign-in") with { Base = "nowhere" }, HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing),
            (NewRequest("feature/sign-in") with { Title = string.Empty }, HostingFailureKind.Validation, PullRequestRefusal.None),
        };
        foreach (var (request, kind, refusal) in cases)
        {
            var posts = fixture.Fake.Requests.Count(sent => sent.Method == "POST");
            var failure = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, request, Cancelled)).Failure!;
            Assert.True((kind, refusal, HostingProvider.AzureDevOps) == (failure.Kind, failure.Refusal, failure.Provider), $"{request}: {failure.Kind} {failure.Refusal} {failure.Message}");
            Assert.Equal(posts + 1, fixture.Fake.Requests.Count(sent => sent.Method == "POST"));
        }

        // A non-contributor's create (TF401027, READ text, 403 ASSUMED): its e-mail-free words, no reason.
        fixture.MobileApp.Contributors = [fixture.Octo];
        var forbidden = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("feature/sign-in"), Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.Forbidden, PullRequestRefusal.None), (forbidden.Kind, forbidden.Refusal));
        Assert.DoesNotContain("@", forbidden.Message, StringComparison.Ordinal);
        Assert.Equal(6, fixture.Fake.Requests.Count(sent => sent.Method == "POST"));
    }

    [Fact]
    public async Task A5_across_a_fork_reads_the_fork_once_for_its_id_and_refuses_another_organization_without_a_request()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("fix-typo") with { HeadRepository = fixture.ForkRepository }, Cancelled)).Value!;

        Assert.Equal(("fix-typo", fixture.ForkRepository, true), (created.HeadRef, created.HeadRepository, created.IsFromFork));
        Assert.Equal(["GET " + RepositoryPath + "-fork?api-version=7.1", "POST " + RepositoryPath + "/pullrequests?api-version=7.1"], fixture.Sent());
        Assert.Contains($"\"forkSource\":{{\"repository\":{{\"id\":\"{fixture.Fork.Id:D}\"}}}}", fixture.Recorder.Sent[1].Body, StringComparison.Ordinal);

        // The base as its own head is no fork: no read, no forkSource.
        Assert.True((await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("docs/guide") with { HeadRepository = fixture.MobileRepository }, Cancelled)).Succeeded);
        Assert.Equal(3, fixture.Recorder.Sent.Count);
        Assert.DoesNotContain("forkSource", fixture.Recorder.Sent[2].Body, StringComparison.Ordinal);

        var harbor = new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "harbor-co/Docks", "manifest");
        var other = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("main") with { HeadRepository = harbor }, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.Validation, AzureDevOpsProvider.OtherOrganizationText, HostingProvider.AzureDevOps), (other.Kind, other.Message, other.Provider));
        Assert.Equal(3, fixture.Recorder.Sent.Count);

        // A fork that cannot be read fails the create before any POST.
        var gone = (await fixture.Provider.CreatePullRequestAsync(fixture.AdaToken, fixture.MobileRepository, NewRequest("fix-typo") with { HeadRepository = fixture.ForkRepository with { Name = "gone-fork" } }, Cancelled)).Failure!;
        Assert.Equal(HostingFailureKind.NotFound, gone.Kind);
        Assert.Equal("GET", fixture.Recorder.Sent[^1].Method);
    }

    [Fact]
    public async Task The_other_members_cover_by_organization_without_case_and_build_the_pages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var provider = fixture.Provider;
        var key = new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs");

        Assert.Equal((HostingProvider.AzureDevOps, "127.0.0.1:2", "http://127.0.0.1:2/"), (provider.Kind, provider.Host, provider.WebBase.AbsoluteUri));
        Assert.Equal(fixture.Fake.Endpoints, provider.Endpoints);
        Assert.True(provider.Covers(new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "FERNHILL-LABS/Mobile App", "x"), key));
        Assert.False(provider.Covers(new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "harbor-co/Docks", "manifest"), key));
        Assert.False(provider.Covers(new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs/Mobile App", "x"), key));
        Assert.False(provider.Covers(new HostedRepository(HostingProvider.GitHub, "127.0.0.1:2", "Fernhill-Labs/Mobile App", "x"), key));
        Assert.False(provider.Covers(fixture.MobileRepository, new AccountKey(HostingProvider.GitHub, "127.0.0.1:2", "Fernhill-Labs")));

        var coverage = provider.CoverageOf(key);
        Assert.Equal((key, provider.WebBase, "fernhill-labs", false, true, false), (coverage.Account, coverage.Origin, coverage.Organization, coverage.OrganizationHost, coverage.HelperUserIsAccounts, coverage.GitTwin));

        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/_usersSettings/tokens", provider.TokenPage("Fernhill-Labs").AbsoluteUri);
        Assert.Throws<ArgumentException>(() => provider.TokenPage(null));
        Assert.Throws<ArgumentException>(() => provider.TokenPage("my org"));
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests", provider.PullRequestsPage(fixture.MobileRepository).AbsoluteUri);
        Assert.Equal(fixture.MobileRepository, provider.Recognise("http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app"));
        Assert.Null(provider.Recognise("https://dev.azure.com/Fernhill-Labs/Mobile%20App/_git/mobile-app"));
        Assert.Equal((false, true, false, true), (provider.SourceCanExist("refs/pull/7/head"), provider.SourceCanExist("refs/pull/7/merge"), provider.SourceCanExist("refs/pull/fix/merge"), provider.SourceCanExist("refs/heads/main")));
        Assert.Equal("Fernhill-Labs", AzureDevOpsProvider.OrganizationOf(" http://127.0.0.1:2/Fernhill-Labs/x ", provider.Endpoints));

        // The service's: the organization's own host covered too; both URL forms give the token page.
        using var service = new AzureDevOpsProvider(AzureDevOpsEndpoints.Service, () => new RecordingHttpHandler(_ => throw new InvalidOperationException("No request is made.")), new ListLog(), new ManualTimeProvider());
        var serviceKey = new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "myorg");
        Assert.True(service.CoverageOf(serviceKey).OrganizationHost);
        Assert.Equal("https://dev.azure.com/myorg/_usersSettings/tokens", service.TokenPage("https://myorg.visualstudio.com/").AbsoluteUri);
        Assert.Equal("https://dev.azure.com/MyOrg/_usersSettings/tokens", service.TokenPage("https://dev.azure.com/MyOrg/p/_git/r").AbsoluteUri);
        Assert.Equal(new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "myorg/p", "r"), service.Recognise("https://myorg.visualstudio.com/DefaultCollection/p/_git/r"));
        Assert.True(service.Covers(service.Recognise("git@ssh.dev.azure.com:v3/MyOrg/p/r")!, serviceKey));
        Assert.Empty(fixture.Recorder.Sent);
        service.ForgetKept();
    }

    private static NewPullRequest NewRequest(string head) => new("A request", "Its description.", head, null, "main", Draft: false);

    private static HttpResponseMessage Answer(HttpStatusCode status, string body, string? contentType)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        if (contentType is not null)
        {
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType + "; charset=utf-8");
        }

        response.Headers.TryAddWithoutValidation("ActivityId", "7b0aac71-0000-4a00-8000-0000000000ff");
        return response;
    }

    /// <summary>A redirect of <paramref name="status"/> with an HTML body (as Azure DevOps' "Object moved"), to <paramref name="location"/> when given.</summary>
    private static HttpResponseMessage Redirect(HttpStatusCode status, string? location)
    {
        var response = Answer(status, "<html><body>Object moved</body></html>", "text/html");
        if (location is not null)
        {
            response.Headers.TryAddWithoutValidation("Location", location);
        }

        return response;
    }

    /// <summary>The fake of Azure DevOps in process on a copy of "hosting7b"'s bare folders, seeded as the scenario's half (design 3.5), and the provider over it.</summary>
    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(TempRepo copy, FakeAzureDevOps fake)
        {
            Copy = copy;
            Fake = fake;
            Recorder = new RecordingHttpHandler(fake.CreateHandler());
            Provider = new AzureDevOpsProvider(fake.Endpoints, () => Recorder, Log, new ManualTimeProvider());
        }

        public TempRepo Copy { get; }

        public FakeAzureDevOps Fake { get; }

        public RecordingHttpHandler Recorder { get; }

        public ListLog Log { get; } = new();

        public AzureDevOpsProvider Provider { get; }

        public FakeAzureUser Ada { get; private set; } = null!;

        public FakeAzureUser Octo { get; private set; } = null!;

        public string AdaToken { get; private set; } = null!;

        public FakeAzureRepository MobileApp { get; private set; } = null!;

        public FakeAzureRepository Fork { get; private set; } = null!;

        public HostedRepository MobileRepository { get; } = new(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs/Mobile App", "mobile-app");

        public HostedRepository ForkRepository { get; } = new(HostingProvider.AzureDevOps, "127.0.0.1:2", "Fernhill-Labs/Mobile App", "mobile-app-fork");

        public static async Task<Fixture> CreateAsync()
        {
            var copy = await Scenarios.Hosting7bAsync();
            var fixture = new Fixture(copy, FakeAzureDevOps.InProcess(copy.SiblingPath(Scenarios.AzureDevOpsFolder)));
            var fake = fixture.Fake;
            fake.AddOrganization(Organization);
            fake.AddProject(Organization, Project);
            fixture.Ada = fake.AddUser(Organization, Scenarios.AdaName);
            fixture.Octo = fake.AddUser(Organization, Scenarios.OctoName);
            fixture.AdaToken = fake.AddToken(Organization, fixture.Ada);
            fake.AddOrganization("harbor-co");
            fake.AddProject("harbor-co", "Docks");
            fixture.MobileApp = await fake.AddRepositoryAsync(Organization, Project, "mobile-app", ct: Cancelled);
            fixture.Fork = await fake.AddRepositoryAsync(Organization, Project, "mobile-app-fork", forkOf: fixture.MobileApp, ct: Cancelled);
            await fake.AddRepositoryAsync(Organization, Project, "sandbox", ct: Cancelled);
            await fake.AddRepositoryAsync(Organization, Project, Scenarios.Hosting7bLongRepository, ct: Cancelled);
            await fake.AddRepositoryAsync("harbor-co", "Docks", "manifest", ct: Cancelled);
            return fixture;
        }

        /// <summary>mobile-app's !11, !12 (a draft) and !13 (from the fork), as the scenario's.</summary>
        public void AddScenarioPullRequests()
        {
            Fake.AddPullRequest(MobileApp, "Add the sign-in page", Ada, "feature/sign-in", "main", id: 11);
            Fake.AddPullRequest(MobileApp, "Write the guide", Octo, "docs/guide", "main", draft: true, id: 12);
            Fake.AddPullRequest(MobileApp, "Fix a typo in the greeting", Ada, "fix-typo", "main", fork: Fork, id: 13);
        }

        /// <summary>What the provider sent, as <c>METHOD /path?query</c>.</summary>
        public IReadOnlyList<string> Sent() => [.. Recorder.Sent.Select(sent => sent.Method + " " + sent.Url.PathAndQuery)];

        public async ValueTask DisposeAsync()
        {
            Provider.Dispose();
            await Fake.DisposeAsync();
            Copy.Dispose();
        }
    }
}
