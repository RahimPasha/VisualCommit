using System.Net;
using System.Text;
using System.Web;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.Http;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Azure DevOps' API client (docs/phase-7b-design.md 6.3): its headers on every request and the
/// token (<c>Basic</c> of <c>":" + token</c>) on the web base alone; each row of 6.3's table, by
/// status and <c>typeKey</c>, the 401's four bodies among them; the sign-in's redirects, a redirect
/// into another organization, a 203 and an HTML success refused, nothing sent where they point;
/// the message's sources, e-mails screened; the request id; the warning; and the walk of
/// <c>$top</c> and <c>$skip</c> pages. Against the fake in process, and scripts where the fake
/// cannot answer as asked. Made-up values only.
/// </summary>
public sealed class AzureDevOpsClientTests
{
    private const string Token = "azdo_FAKE-Fernhill-Labs-1";
    private const string Organization = "Fernhill-Labs";

    private static readonly AzureDevOpsEndpoints Service = AzureDevOpsEndpoints.Service;

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_request_carries_its_headers_and_the_token_goes_only_to_the_web_base()
    {
        using var root = new TempDirectory("azure-client");
        await using var fake = FakeAzureDevOps.InProcess(root.Path);
        fake.AddOrganization(Organization);
        var ada = fake.AddUser(Organization, "Ada Lovelace");
        var token = fake.AddToken(Organization, ada);
        var recorder = new RecordingHttpHandler(fake.CreateHandler());
        using var client = new AzureDevOpsClient(fake.Endpoints, () => recorder, new ListLog(), new ManualTimeProvider());

        var connection = await client.SendAsync(HttpMethod.Get, token, Web(fake, "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, Cancelled);
        var area = await client.SendAsync(HttpMethod.Get, token, Web(fake, $"Fernhill-Labs/_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?api-version=7.1-preview.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsResourceAreaJson, Cancelled);
        var list = await client.SendAsync(HttpMethod.Get, token, Web(fake, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled);

        Assert.Equal(ada.Id.ToString(), connection.Value!.AuthenticatedUser!.Id);
        Assert.Equal("http://127.0.0.1:2/Fernhill-Labs/", area.Value!.LocationUrl);
        Assert.Empty(list.Value!.Value);
        Assert.Equal(3, recorder.Sent.Count);
        Assert.All(recorder.Sent, sent =>
        {
            Assert.Equal("Suppress", sent.Header("X-TFS-FedAuthRedirect"));
            Assert.Equal("application/json", sent.Header("Accept"));
            Assert.Equal(HostingHttpClient.UserAgent, sent.Header("User-Agent"));
            Assert.Equal(":" + token, BasicOf(sent));
        });

        // Another host, or another port or scheme of the web base's host, gets no token.
        foreach (var elsewhere in new[] { "http://127.0.0.2:2/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", "http://127.0.0.1:3/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", "https://127.0.0.1:2/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1" })
        {
            var refused = await client.SendAsync(HttpMethod.Get, token, new Uri(elsewhere), null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, Cancelled);
            Assert.Equal(HostingFailureKind.BadCredentials, refused.Failure!.Kind);
            Assert.Null(recorder.Sent[^1].Header("Authorization"));
            Assert.Equal("Suppress", recorder.Sent[^1].Header("X-TFS-FedAuthRedirect"));
        }

        Assert.DoesNotContain(fake.Requests, request => request.Path.Contains(token, StringComparison.Ordinal) || request.Query.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_401_is_refused_credentials_whatever_its_body_and_the_warning_names_the_path_and_the_activity()
    {
        var bodies = new Func<HttpResponseMessage>[]
        {
            // Empty, as for an unknown token (MEASURED 2026-11-05, D373's PAT-shaped value; READ g1 G2).
            () => Answer(HttpStatusCode.Unauthorized, string.Empty, null, ("WWW-Authenticate", "Basic realm=\"https://tfsprodfake.visualstudio.com/\""), ("ActivityId", "7b0aac71-0000-4a00-8000-000000000001")),
            // HTML (g1 G0.2), and the same led by a BOM.
            () => Answer(HttpStatusCode.Unauthorized, "<html><body>Sign in</body></html>", "text/html", ("ActivityId", "7b0aac71-0000-4a00-8000-000000000001")),
            () => AnswerBytes(HttpStatusCode.Unauthorized, [0xEF, 0xBB, 0xBF, .. "<html><body>Sign in</body></html>"u8.ToArray()], ("ActivityId", "7b0aac71-0000-4a00-8000-000000000001")),
            // UnauthorizedRequestException TF400813 (MEASURED r5 1.3).
            () => Answer(HttpStatusCode.Unauthorized, Error("TF400813: The user 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' is not authorized to access this resource.", "UnauthorizedRequestException"), "application/json", ("ActivityId", "7b0aac71-0000-4a00-8000-000000000001")),
            // AccessCheckException, led by a BOM (READ g1 G2).
            () => AnswerBytes(HttpStatusCode.Unauthorized, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Error("Access Denied: The Personal Access Token used has expired.", "AccessCheckException"))], ("ActivityId", "7b0aac71-0000-4a00-8000-000000000001")),
        };
        var messages = new List<string>();
        foreach (var body in bodies)
        {
            var log = new ListLog();
            var (client, recorder) = Scripted(_ => body(), log);
            using (client)
            {
                var reply = await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, Cancelled);
                var failure = reply.Failure!;
                Assert.Equal((HostingFailureKind.BadCredentials, HostingProvider.AzureDevOps, "7b0aac71-0000-4a00-8000-000000000001"), (failure.Kind, failure.Provider, failure.RequestId));
                Assert.Equal("https://dev.azure.com/Fernhill-Labs/_apis/connectionData", failure.Url!.AbsoluteUri);
                Assert.Equal(["Azure DevOps: GET /Fernhill-Labs/_apis/connectionData failed: 401 (request 7b0aac71-0000-4a00-8000-000000000001)."], log.Lines.Select(line => line.Message));
                Assert.Single(recorder.Sent);
                messages.Add(failure.Message);
            }
        }

        Assert.Equal(["Unauthorized", "Unauthorized", "Unauthorized", "TF400813: The user 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' is not authorized to access this resource.", "Access Denied: The Personal Access Token used has expired."], messages);
    }

    /// <summary>One row of 6.3's table, scripted.</summary>
    private sealed record Row(int Status, string Body, string? ContentType, HostingFailureKind Kind, PullRequestRefusal Refusal, string Message, (string Name, string Value)[]? Headers = null, string? Reason = null);

    [Fact]
    public async Task Each_row_of_the_table_gives_its_kind_by_status_and_type_key_with_the_message_from_its_source()
    {
        var rows = new Row[]
        {
            new(400, Error("The requested version \"7.1\" of the resource is under preview.", "VssInvalidPreviewVersionException"), "application/json", HostingFailureKind.ApiVersion, PullRequestRefusal.None, "The requested version \"7.1\" of the resource is under preview."),
            new(400, Error("The requested REST API version of 9.0 is out of range for this server.", "VssVersionOutOfRangeException"), "application/json", HostingFailureKind.ApiVersion, PullRequestRefusal.None, "The requested REST API version of 9.0 is out of range for this server."),
            new(400, Error("Invalid api version string.", "VssInvalidApiResourceVersionException"), "application/json", HostingFailureKind.ApiVersion, PullRequestRefusal.None, "Invalid api version string."),
            new(403, Error("TF401027: You need the Git 'PullRequestContribute' permission to perform this action. Details: identity 'Octo Cat', scope 'repository'.", "GitNeedsPermissionException"), "application/json", HostingFailureKind.Forbidden, PullRequestRefusal.None, "TF401027: You need the Git 'PullRequestContribute' permission to perform this action. Details: identity 'Octo Cat', scope 'repository'."),
            new(403, string.Empty, null, HostingFailureKind.Forbidden, PullRequestRefusal.None, "Forbidden"),
            new(404, Error("TF401019: The Git repository with name or identifier nowhere does not exist or you do not have permissions for the operation you are attempting.", "GitRepositoryNotFoundException"), "application/json", HostingFailureKind.NotFound, PullRequestRefusal.None, "TF401019: The Git repository with name or identifier nowhere does not exist or you do not have permissions for the operation you are attempting."),
            // An unknown organization: text, not JSON, its words in X-TFS-ServiceError (MEASURED r5 1.3).
            new(404, "The resource cannot be found.", "text/plain", HostingFailureKind.NotFound, PullRequestRefusal.None, "The resource cannot be found.", [("X-TFS-ServiceError", "The%20resource%20cannot%20be%20found.")]),
            new(404, "Page not found", "text/plain", HostingFailureKind.NotFound, PullRequestRefusal.None, "Not Found"),
            // TF400677's shape (MEASURED g1 G4.2).
            new(404, """{"count":1,"value":{"Message":"TF400677: Host nowhere does not exist."}}""", "application/json", HostingFailureKind.NotFound, PullRequestRefusal.None, "TF400677: Host nowhere does not exist."),
            new(409, Error("TF401179: An active pull request for the source and target branch already exists.", "GitPullRequestExistsException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.AlreadyExists, "TF401179: An active pull request for the source and target branch already exists."),
            new(400, Error("TF401398: The pull request cannot be activated because the source and/or the target branch no longer exists, or the requested refs are not branches", "GitPullRequestCannotBeActivated"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing, "TF401398: The pull request cannot be activated because the source and/or the target branch no longer exists, or the requested refs are not branches"),
            new(400, Error("The pull request needs a title.", "InvalidArgumentValueException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "The pull request needs a title."),
            // An empty title's words hold a CR LF (MEASURED 2026-11-05): the message is one line, each
            // run of control characters one space, so that the app's log keeps one entry a line; so are
            // X-TFS-ServiceError's decoded words; a message of control characters alone is none, and
            // the reason phrase stands in.
            new(400, Error("Invalid argument value.\r\nParameter name: Invalid pull request title.", "InvalidArgumentValueException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "Invalid argument value. Parameter name: Invalid pull request title."),
            new(404, "Not here.", "text/plain", HostingFailureKind.NotFound, PullRequestRefusal.None, "Line one. Line two.", [("X-TFS-ServiceError", "Line%20one.%0D%0ALine%20two.%0A")]),
            new(400, Error("\u0001\u0007", "InvalidArgumentValueException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "Bad Request"),
            // GitPullRequestExistsException's key on a 400, or the other's on a 409, is no refusal of its kind.
            new(400, Error("Exists.", "GitPullRequestExistsException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "Exists."),
            new(409, Error("Cannot.", "GitPullRequestCannotBeActivated"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "Cannot."),
            new(409, string.Empty, null, HostingFailureKind.Validation, PullRequestRefusal.None, "Conflict"),
            new(422, string.Empty, null, HostingFailureKind.Validation, PullRequestRefusal.None, "HTTP 422", Reason: string.Empty),
            new(500, Error("TF400898: An Internal Error Occurred. Activity Id: 7b0aac71-0000-4a00-8000-000000000009.", "UnexpectedServerException"), "application/json", HostingFailureKind.ServerError, PullRequestRefusal.None, "TF400898: An Internal Error Occurred. Activity Id: 7b0aac71-0000-4a00-8000-000000000009."),
            new(503, "<html><body>Service Unavailable</body></html>", "text/html", HostingFailureKind.ServerError, PullRequestRefusal.None, "Service Unavailable"),
            new(502, string.Empty, null, HostingFailureKind.ServerError, PullRequestRefusal.None, "Azure DevOps had a problem: HTTP 502.", Reason: string.Empty),
            new(418, Error("A teapot.", "TeapotException"), "application/json", HostingFailureKind.Malformed, PullRequestRefusal.None, "Azure DevOps' answer could not be read."),
            // A BOM'd error is read (g1 G0.8: the core strips it).
            new(400, "﻿" + Error("Exists again.", "GitPullRequestExistsException"), "application/json", HostingFailureKind.Validation, PullRequestRefusal.None, "Exists again."),
        };

        foreach (var row in rows)
        {
            var (client, _) = Scripted(_ =>
            {
                var response = Answer((HttpStatusCode)row.Status, row.Body, row.ContentType, [.. row.Headers ?? []]);
                if (row.Reason is not null)
                {
                    response.ReasonPhrase = row.Reason;
                }

                return response;
            });
            using (client)
            {
                var reply = await client.SendAsync(HttpMethod.Post, Token, new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?api-version=7.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson, Cancelled);
                var failure = reply.Failure!;
                Assert.True((row.Kind, row.Refusal, row.Message, HostingProvider.AzureDevOps) == (failure.Kind, failure.Refusal, failure.Message, failure.Provider), $"{row.Status} {row.Body}: {failure.Kind} {failure.Refusal} {failure.Message}");
                Assert.Null(failure.Detail);
            }
        }
    }

    [Fact]
    public async Task A_429_names_when_it_ends_by_retry_after_then_the_reset_then_a_minute_never_before_now_and_a_day_at_most()
    {
        var clock = new ManualTimeProvider();
        var now = clock.GetUtcNow();
        var cases = new (string Name, string Value, DateTimeOffset RetryAt)[]
        {
            ("Retry-After", "30", now.AddSeconds(30)),
            ("Retry-After", "999999", now.AddDays(1)),
            ("Retry-After", now.AddHours(2).ToString("R", System.Globalization.CultureInfo.InvariantCulture), now.AddHours(2)),

            // A date or a reset in the past is now, never a time already gone (the app shows it as when to try again).
            ("Retry-After", "Wed, 21 Oct 2015 07:28:00 GMT", now),
            ("X-RateLimit-Reset", "1", now),
            ("X-RateLimit-Reset", now.AddMinutes(5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), now.AddMinutes(5)),
            ("X-RateLimit-Reset", "99999999999999999", now.AddSeconds(60)),
            ("X-Other", "1", now.AddSeconds(60)),
        };
        foreach (var (name, value, retryAt) in cases)
        {
            var recorder = new RecordingHttpHandler(_ => Answer((HttpStatusCode)429, Error("TF400733: The request has been canceled.", "RequestBlockedException"), "application/json", (name, value)));
            using var client = new AzureDevOpsClient(Service, () => recorder, new ListLog(), clock);
            var failure = (await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.RateLimited, retryAt), (failure.Kind, failure.RetryAt));
        }
    }

    [Fact]
    public async Task The_sign_ins_redirects_are_refused_credentials_and_nothing_is_sent_where_they_point()
    {
        var connection = new Uri(Service.WebBase, "fake-org/_apis/connectionData?api-version=7.1-preview.1");
        var repositories = new Uri(Service.WebBase, "fake-org/_apis/git/repositories?api-version=7.1");
        var cases = new (Uri Url, string Location)[]
        {
            (connection, "https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com&reply_to=x"),
            (repositories, "https://spsprodfake2.vssps.visualstudio.com/_signin?realm=dev.azure.com"),
            (repositories, "https://app.vssps.visualstudio.com/elsewhere"),
            (repositories, "https://dev.azure.com/_signin?realm=dev.azure.com"),
            // Every redirect of connectionData, the same origin's in the same organization too (D360).
            (connection, "https://dev.azure.com/fake-org/_apis/connectionData?api-version=7.1-preview.1&again=1"),
            (connection, "/fake-org/_apis/connectionData?api-version=7.1-preview.1&again=1"),
        };
        foreach (var (url, location) in cases)
        {
            var (client, recorder) = Scripted(_ => Answer(HttpStatusCode.Found, "<html>Object moved</html>", "text/html", ("Location", location)));
            using (client)
            {
                var failure = (await client.SendAsync(HttpMethod.Get, Token, url, null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, Cancelled)).Failure!;
                Assert.Equal((HostingFailureKind.BadCredentials, AzureDevOpsClient.SignInPageText), (failure.Kind, failure.Message));
                Assert.Single(recorder.Sent);
            }
        }

        // The fake's own sign-in redirect despite Suppress (its fault), on its own origin: nothing is sent there.
        using var root = new TempDirectory("azure-client");
        await using var fake = FakeAzureDevOps.InProcess(root.Path);
        fake.AddOrganization(Organization);
        var token = fake.AddToken(Organization, fake.AddUser(Organization, "Ada Lovelace"));
        var inner = new RecordingHttpHandler(fake.CreateHandler());
        using var fakeClient = new AzureDevOpsClient(fake.Endpoints, () => inner, new ListLog(), new ManualTimeProvider());
        fake.FailNext("repos:fernhill-labs", FakeAzureDevOpsFault.SignInRedirect);
        var signIn = (await fakeClient.SendAsync(HttpMethod.Get, token, Web(fake, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;
        Assert.Equal(HostingFailureKind.BadCredentials, signIn.Kind);
        Assert.Single(inner.Sent);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task A_redirect_into_another_organization_is_malformed_and_one_in_the_same_is_followed()
    {
        var log = new ListLog();
        var (client, recorder) = Scripted(request => request.RequestUri!.AbsolutePath.StartsWith("/fake-org/", StringComparison.Ordinal) && !request.RequestUri.Query.Contains("moved", StringComparison.Ordinal)
            ? Answer(HttpStatusCode.Found, string.Empty, null, ("Location", request.RequestUri.AbsolutePath.Contains("same", StringComparison.Ordinal) ? "/FAKE-ORG/_apis/git/repositories?api-version=7.1&moved=1" : "/other-org/_apis/git/repositories?api-version=7.1"), ("ActivityId", "7b0aac71-0000-4a00-8000-00000000000a"))
            : Answer(HttpStatusCode.OK, """{"count":0,"value":[]}""", "application/json"), log);
        using (client)
        {
            var outside = (await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "fake-org/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.Malformed, "Azure DevOps' answer could not be read."), (outside.Kind, outside.Message));
            Assert.Single(recorder.Sent);
            Assert.DoesNotContain(recorder.Sent, sent => sent.Url.AbsolutePath.StartsWith("/other-org", StringComparison.Ordinal));
            Assert.Equal("Azure DevOps: GET /fake-org/_apis/git/repositories failed: redirected outside the organization (request 7b0aac71-0000-4a00-8000-00000000000a).", log.Lines[^1].Message);

            var same = await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "fake-org/same/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled);
            Assert.True(same.Succeeded);
            Assert.Equal(3, recorder.Sent.Count);
            Assert.Equal(":" + Token, BasicOf(recorder.Sent[^1]));

            // Another origin: the core's rule, nothing sent there.
            var (offOrigin, offRecorder) = Scripted(_ => Answer(HttpStatusCode.Found, string.Empty, null, ("Location", "https://example.test/fake-org/_apis/git/repositories")));
            using (offOrigin)
            {
                Assert.Equal(HostingFailureKind.Malformed, (await offOrigin.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "fake-org/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!.Kind);
                Assert.Single(offRecorder.Sent);
            }
        }
    }

    [Fact]
    public async Task A_redirect_on_the_origin_whose_path_leaves_the_plain_form_is_malformed_and_not_followed()
    {
        // .NET sends /fake-org/%2E%2E%2Fother-org/… as /fake-org/..%2Fother-org/…: its first segment
        // stays fake-org, but a server that decodes %2F before resolving dot segments serves other-org.
        foreach (var location in new[]
        {
            "/fake-org/%2E%2E%2Fother-org/_apis/git/repositories?api-version=7.1",
            "/fake-org/%2e%2e%5Cother-org/_apis/git/repositories?api-version=7.1",
            "https://dev.azure.com/fake-org/p%2Fq/_apis/git/repositories?api-version=7.1",
            "/fake-org/%252E%252E/other-org/_apis/git/repositories?api-version=7.1",
        })
        {
            var log = new ListLog();
            var (client, recorder) = Scripted(request => request.RequestUri!.AbsolutePath == "/fake-org/_apis/git/repositories"
                ? Answer(HttpStatusCode.Found, string.Empty, null, ("Location", location))
                : Answer(HttpStatusCode.OK, """{"count":0,"value":[]}""", "application/json"), log);
            using (client)
            {
                var failure = (await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "fake-org/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure;
                Assert.True(failure is { Kind: HostingFailureKind.Malformed, Provider: HostingProvider.AzureDevOps }, location);
                Assert.Single(recorder.Sent);
                Assert.Contains(AzureDevOpsClient.OutsideOrganization, log.Lines[^1].Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task A_null_row_on_a_page_or_in_the_probe_is_malformed_and_nothing_throws()
    {
        var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");
        static string IdOf(AzureDevOpsPullRequestJson row) => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // System.Text.Json does not hold a list's elements to their annotations, so a null row parses.
        var (onPage, pageRecorder) = Scripted(_ => Answer(HttpStatusCode.OK, """{"value":[{"pullRequestId":2,"title":"Request 2","sourceRefName":"refs/heads/b2","targetRefName":"refs/heads/main"},null],"count":2}""", "application/json"));
        using (onPage)
        {
            var failed = await onPage.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, IdOf, 3, Cancelled);
            Assert.Equal(((List<AzureDevOpsPullRequestJson>?)null, HostingFailureKind.Malformed, HostingProvider.AzureDevOps), (failed.Value, failed.Failure!.Kind, failed.Failure.Provider));
            Assert.Single(pageRecorder.Sent);
        }

        var (inProbe, probeRecorder) = Scripted(request => request.RequestUri!.Query.Contains("$top=1&", StringComparison.Ordinal)
            ? Answer(HttpStatusCode.OK, """{"value":[null],"count":1}""", "application/json")
            : Page(request, [3, 2, 1]), pageSize: 2);
        using (inProbe)
        {
            var failed = await inProbe.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, IdOf, 1, Cancelled);
            Assert.Equal(((List<AzureDevOpsPullRequestJson>?)null, HostingFailureKind.Malformed), (failed.Value, failed.Failure!.Kind));
            Assert.Equal(2, probeRecorder.Sent.Count);
        }
    }

    [Fact]
    public async Task A_203_and_an_html_success_are_refused_credentials_and_a_boms_json_is_read()
    {
        var cases = new Func<HttpResponseMessage>[]
        {
            () => Answer(HttpStatusCode.NonAuthoritativeInformation, """{"count":0,"value":[]}""", "application/json"),
            () => Answer(HttpStatusCode.OK, "<!DOCTYPE html><html><head><title>Sign in</title></head></html>", "text/html"),
            // Typed text/html though its body does not start with '<': the Content-Type alone refuses it.
            () => Answer(HttpStatusCode.OK, "Sign in to continue", "text/html"),
            () => Answer(HttpStatusCode.OK, "  <html></html>", "application/json"),
            () => AnswerBytes(HttpStatusCode.OK, [0xEF, 0xBB, 0xBF, .. "<html></html>"u8.ToArray()]),
        };
        foreach (var answer in cases)
        {
            var (client, _) = Scripted(_ => answer());
            using (client)
            {
                var failure = (await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;
                Assert.Equal((HostingFailureKind.BadCredentials, AzureDevOpsClient.SignInPageText), (failure.Kind, failure.Message));
            }
        }

        var (bom, _) = Scripted(_ => AnswerBytes(HttpStatusCode.OK, [0xEF, 0xBB, 0xBF, .. """{"count":0,"value":[]}"""u8.ToArray()]));
        using (bom)
        {
            Assert.True((await bom.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Succeeded);
        }

        // The fake's 203 (its fault, g1 G0.1).
        using var root = new TempDirectory("azure-client");
        await using var fake = FakeAzureDevOps.InProcess(root.Path);
        fake.AddOrganization(Organization);
        var token = fake.AddToken(Organization, fake.AddUser(Organization, "Ada Lovelace"));
        using var fakeClient = new AzureDevOpsClient(fake.Endpoints, fake.CreateHandler, new ListLog(), new ManualTimeProvider());
        fake.FailNext("connection:fernhill-labs", FakeAzureDevOpsFault.NonAuthoritative);
        Assert.Equal(HostingFailureKind.BadCredentials, (await fakeClient.SendAsync(HttpMethod.Get, token, Web(fake, "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson, Cancelled)).Failure!.Kind);
    }

    [Fact]
    public async Task The_fakes_failures_are_classified_and_the_request_id_is_its_activity()
    {
        using var root = new TempDirectory("azure-client");
        await using var fake = FakeAzureDevOps.InProcess(root.Path);
        fake.AddOrganization(Organization);
        var ada = fake.AddUser(Organization, "Ada Lovelace");
        var token = fake.AddToken(Organization, ada);
        var expired = fake.AddToken(Organization, ada);
        fake.Expire(expired);
        var log = new ListLog();
        using var client = new AzureDevOpsClient(fake.Endpoints, fake.CreateHandler, log, new ManualTimeProvider());
        var connection = Web(fake, "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1");
        var repositories = Web(fake, "Fernhill-Labs/_apis/git/repositories?api-version=7.1");

        async Task<HostingFailure> FailureAsync(string usedToken, Uri url) =>
            (await client.SendAsync(HttpMethod.Get, usedToken, url, null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;

        var unknown = await FailureAsync("azdo_FAKE-Nobody-9", connection);
        Assert.Equal((HostingFailureKind.BadCredentials, "Unauthorized"), (unknown.Kind, unknown.Message));
        Assert.Matches("^7b0aac71-0000-4a00-8000-[0-9a-f]{12}$", unknown.RequestId!);
        Assert.Equal($"Azure DevOps: GET /Fernhill-Labs/_apis/connectionData failed: 401 (request {unknown.RequestId}).", log.Lines[^1].Message);
        Assert.Equal(("Access Denied: The Personal Access Token used has expired.", HostingFailureKind.BadCredentials), ((await FailureAsync(expired, repositories)).Message, (await FailureAsync(expired, repositories)).Kind));

        // An unknown organization: 404 text, X-TFS-ServiceError, no ActivityId (MEASURED r5 1.3).
        var nowhere = await FailureAsync(token, Web(fake, "vc-none-0123456789ab/_apis/connectionData?api-version=7.1-preview.1"));
        Assert.Equal((HostingFailureKind.NotFound, "The resource cannot be found.", null), (nowhere.Kind, nowhere.Message, nowhere.RequestId));
        Assert.Equal("Azure DevOps: GET /vc-none-0123456789ab/_apis/connectionData failed: 404.", log.Lines[^1].Message);

        // connectionData at 7.1 (no preview) is refused by the version (MEASURED r5 0.1).
        Assert.Equal(HostingFailureKind.ApiVersion, (await FailureAsync(token, Web(fake, "Fernhill-Labs/_apis/connectionData?api-version=7.1"))).Kind);

        foreach (var (fault, kind) in new[]
        {
            (FakeAzureDevOpsFault.Throttled, HostingFailureKind.RateLimited),
            (FakeAzureDevOpsFault.VersionRefused, HostingFailureKind.ApiVersion),
            (FakeAzureDevOpsFault.ServerError, HostingFailureKind.ServerError),
            (FakeAzureDevOpsFault.BadGateway, HostingFailureKind.ServerError),
            (FakeAzureDevOpsFault.Malformed, HostingFailureKind.Malformed),
            (FakeAzureDevOpsFault.Unauthorized, HostingFailureKind.BadCredentials),
            (FakeAzureDevOpsFault.InvalidToken, HostingFailureKind.BadCredentials),
            (FakeAzureDevOpsFault.Expired, HostingFailureKind.BadCredentials),
            (FakeAzureDevOpsFault.NotFound, HostingFailureKind.NotFound),
            (FakeAzureDevOpsFault.RouteMissing, HostingFailureKind.NotFound),
            (FakeAzureDevOpsFault.OrganizationMissing, HostingFailureKind.NotFound),
        })
        {
            fake.FailNext("repos:fernhill-labs", fault);
            var failure = await FailureAsync(token, repositories);
            Assert.True((kind, HostingProvider.AzureDevOps) == (failure.Kind, failure.Provider), fault.Name);
            Assert.True(await FailureOrNullAsync() is null, fault.Name + " then the normal answer");
        }

        Assert.DoesNotContain(log.Lines, line => line.Message.Contains(token, StringComparison.Ordinal) || line.Message.Contains('?', StringComparison.Ordinal) || line.Message.Contains("api-version", StringComparison.Ordinal));
        Assert.All(log.Entries, entry => Assert.Null(entry.Exception));

        async Task<HostingFailure?> FailureOrNullAsync() =>
            (await client.SendAsync(HttpMethod.Get, token, repositories, null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure;
    }

    [Fact]
    public async Task An_email_in_a_message_or_in_x_tfs_service_error_is_screened_from_the_failure_and_the_log()
    {
        var bodies = new Func<HttpResponseMessage>[]
        {
            () => Answer(HttpStatusCode.Forbidden, Error("TF401027: You need the Git 'GenericContribute' permission to perform this action. Details: identity 'ada.lovelace@example.com', scope 'repository'.", "GitNeedsPermissionException"), "application/json"),
            () => Answer(HttpStatusCode.Forbidden, "denied", "text/plain", ("X-TFS-ServiceError", "TF401027%3A%20identity%20%27ada.lovelace%40example.com%27")),
        };
        foreach (var body in bodies)
        {
            var log = new ListLog();
            var (client, _) = Scripted(_ => body(), log);
            using (client)
            {
                var failure = (await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/_apis/git/repositories?api-version=7.1"), null, AzureDevOpsJsonContext.Default.RepositoryList, Cancelled)).Failure!;
                Assert.Equal(HostingFailureKind.Forbidden, failure.Kind);
                Assert.Contains(HostingHttpClient.ScreenedEmail, failure.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("example.com", failure.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("@", log.Written, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task The_request_id_is_the_activity_else_the_end_to_end_id_and_only_of_a_request_ids_shape()
    {
        var cases = new ((string, string)[] Headers, string? RequestId)[]
        {
            ([("ActivityId", "7b0aac71-0000-4a00-8000-000000000001"), ("X-VSS-E2EID", "7b0aac71-0000-4a00-8000-000000000002")], "7b0aac71-0000-4a00-8000-000000000001"),
            ([("X-VSS-E2EID", "7b0aac71-0000-4a00-8000-000000000002")], "7b0aac71-0000-4a00-8000-000000000002"),
            ([("ActivityId", "not an id")], null),
            ([("ActivityId", Token)], null),
            ([], null),
        };
        foreach (var (headers, requestId) in cases)
        {
            var (client, _) = Scripted(_ => Answer(HttpStatusCode.NotFound, Error("TF401019: nowhere.", "GitRepositoryNotFoundException"), "application/json", headers));
            using (client)
            {
                var reply = await client.SendAsync(HttpMethod.Get, Token, new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/nowhere?api-version=7.1"), null, AzureDevOpsJsonContext.Default.AzureDevOpsRepositoryJson, Cancelled);
                Assert.Equal((requestId, requestId), (reply.RequestId, reply.Failure!.RequestId));
            }
        }
    }

    [Fact]
    public async Task The_walk_pages_by_top_and_skip_probes_once_after_its_cap_and_keeps_a_shifted_row_once()
    {
        // Five requests, newest first; ids 5 to 1.
        var ids = new List<int> { 5, 4, 3, 2, 1 };
        var (client, recorder) = Scripted(request => Page(request, ids), pageSize: 2);
        using (client)
        {
            var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");

            var capped = await client.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 2, Cancelled);
            Assert.Equal([5, 4, 3, 2], capped.Value!.Select(row => row.PullRequestId));
            Assert.True(capped.MoreExist);
            Assert.Equal(
                ["$top=2&$skip=0", "$top=2&$skip=2", "$top=1&$skip=4"],
                recorder.Sent.Select(sent => sent.Url.Query[(sent.Url.Query.IndexOf("$top", StringComparison.Ordinal))..]));
            Assert.All(recorder.Sent, sent => Assert.StartsWith("?searchCriteria.status=active&api-version=7.1&", sent.Url.Query, StringComparison.Ordinal));

            var whole = await client.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 3, Cancelled);
            Assert.Equal([5, 4, 3, 2, 1], whole.Value!.Select(row => row.PullRequestId));
            Assert.False(whole.MoreExist);
            Assert.Equal(6, recorder.Sent.Count);

            // At the cap with nothing left, the probe finds none.
            ids.RemoveAt(ids.Count - 1);
            var exact = await client.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 2, Cancelled);
            Assert.Equal([5, 4, 3, 2], exact.Value!.Select(row => row.PullRequestId));
            Assert.False(exact.MoreExist);
        }

        // A request created between pages shifts the next page by one: its repeated row is kept once.
        var shifting = new List<int> { 5, 4, 3, 2, 1 };
        var (shifted, shiftedRecorder) = Scripted(request =>
        {
            var answer = Page(request, shifting);
            if (shifting[0] == 5)
            {
                shifting.Insert(0, 6);
            }

            return answer;
        }, pageSize: 2);
        using (shifted)
        {
            var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");
            var rows = await shifted.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 5, Cancelled);
            Assert.Equal([5, 4, 3, 2, 1], rows.Value!.Select(row => row.PullRequestId));
            Assert.Equal(4, shiftedRecorder.Sent.Count);
        }

        // A failing page fails the whole list.
        var (failing, _) = Scripted(request => request.RequestUri!.Query.Contains("$skip=2", StringComparison.Ordinal)
            ? Answer(HttpStatusCode.InternalServerError, Error("TF400898: An Internal Error Occurred.", "UnexpectedServerException"), "application/json")
            : Page(request, [5, 4, 3, 2, 1]), pageSize: 2);
        using (failing)
        {
            var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");
            var failed = await failing.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 3, Cancelled);
            Assert.Equal((null, HostingFailureKind.ServerError), (failed.Value, failed.Failure!.Kind));
        }
    }

    [Fact]
    public async Task A_page_holding_more_rows_than_its_top_gives_a_pages_worth_and_the_cap_still_holds()
    {
        // A server that ignores $top and answers 250 requests from $skip on (ids skip+1 to skip+250).
        var (client, recorder) = Scripted(request =>
        {
            var skip = int.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["$skip"]!, System.Globalization.CultureInfo.InvariantCulture);
            var rows = Enumerable.Range(skip + 1, 250).Select(id => $$"""{"pullRequestId":{{id}},"title":"Request {{id}}","sourceRefName":"refs/heads/b{{id}}","targetRefName":"refs/heads/main"}""");
            return Answer(HttpStatusCode.OK, $$"""{"value":[{{string.Join(',', rows)}}],"count":250}""", "application/json");
        });
        using (client)
        {
            var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");

            var rows = await client.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, row => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), 3, Cancelled);

            Assert.Equal(Enumerable.Range(1, 300), rows.Value!.Select(row => row.PullRequestId));
            Assert.True(rows.MoreExist);
            Assert.Equal(
                ["$top=100&$skip=0", "$top=100&$skip=100", "$top=100&$skip=200", "$top=1&$skip=300"],
                recorder.Sent.Select(sent => sent.Url.Query[(sent.Url.Query.IndexOf("$top", StringComparison.Ordinal))..]));
        }
    }

    [Fact]
    public async Task Any_row_the_probe_finds_means_more_exist_also_one_already_shown()
    {
        var url = new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?searchCriteria.status=active&api-version=7.1");
        static string IdOf(AzureDevOpsPullRequestJson row) => row.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Newest first; a request created just before the probe shifts the list by one, so the
        // probe finds the last row already shown, while the new one was never shown.
        var shifting = new List<int> { 5, 4, 3, 2, 1 };
        var (shifted, shiftedRecorder) = Scripted(request =>
        {
            if (request.RequestUri!.Query.Contains("$top=1&", StringComparison.Ordinal))
            {
                shifting.Insert(0, 6);
            }

            return Page(request, shifting);
        }, pageSize: 2);
        using (shifted)
        {
            var rows = await shifted.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, IdOf, 2, Cancelled);
            Assert.Equal([5, 4, 3, 2], rows.Value!.Select(row => row.PullRequestId));
            Assert.True(rows.MoreExist);
            Assert.Equal(3, shiftedRecorder.Sent.Count);
        }

        // A server that honours $top and ignores $skip answers the first rows on every page: one
        // page's worth is shown once, and the probe's row, already shown, still says more exist.
        var (ignoring, ignoringRecorder) = Scripted(request =>
        {
            var top = int.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["$top"]!, System.Globalization.CultureInfo.InvariantCulture);
            var rows = Enumerable.Range(1, top).Select(id => $$"""{"pullRequestId":{{id}},"title":"Request {{id}}","sourceRefName":"refs/heads/b{{id}}","targetRefName":"refs/heads/main"}""");
            return Answer(HttpStatusCode.OK, $$"""{"value":[{{string.Join(',', rows)}}],"count":{{top}}}""", "application/json");
        }, pageSize: 2);
        using (ignoring)
        {
            var rows = await ignoring.GetPagesAsync(Token, url, AzureDevOpsJsonContext.Default.PullRequestList, IdOf, 3, Cancelled);
            Assert.Equal([1, 2], rows.Value!.Select(row => row.PullRequestId));
            Assert.True(rows.MoreExist);
            Assert.Equal(4, ignoringRecorder.Sent.Count);
        }
    }

    [Fact]
    public async Task A_body_is_camel_case_json_of_the_contexts_shapes_and_another_type_is_refused()
    {
        var (client, recorder) = Scripted(_ => Answer(HttpStatusCode.Created, """{"pullRequestId":14,"title":"Add the login form","sourceRefName":"refs/heads/feature/login","targetRefName":"refs/heads/main"}""", "application/json"));
        using (client)
        {
            var body = new AzureDevOpsNewPullRequestJson
            {
                SourceRefName = "refs/heads/feature/login",
                TargetRefName = "refs/heads/main",
                Title = "Add the login form",
                IsDraft = true,
                ForkSource = new AzureDevOpsNewForkSourceJson { Repository = new AzureDevOpsIdJson { Id = "7b0a0000-0000-4000-8000-000000000001" } },
            };
            var reply = await client.SendAsync(HttpMethod.Post, Token, new Uri(Service.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests?api-version=7.1"), body, AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson, Cancelled);

            Assert.Equal(14, reply.Value!.PullRequestId);
            Assert.Equal("""{"sourceRefName":"refs/heads/feature/login","targetRefName":"refs/heads/main","title":"Add the login form","isDraft":true,"forkSource":{"repository":{"id":"7b0a0000-0000-4000-8000-000000000001"}}}""", recorder.Sent[0].Body);
            Assert.Equal("application/json; charset=utf-8", recorder.Sent[0].Header("Content-Type"));
            await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(HttpMethod.Post, Token, new Uri(Service.WebBase, "x?api-version=7.1"), new { Title = "x" }, AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson, Cancelled));
        }
    }

    /// <summary>A page of <paramref name="ids"/> by the request's <c>$top</c> and <c>$skip</c>.</summary>
    private static HttpResponseMessage Page(HttpRequestMessage request, IReadOnlyList<int> ids)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var top = int.Parse(query["$top"]!, System.Globalization.CultureInfo.InvariantCulture);
        var skip = int.Parse(query["$skip"]!, System.Globalization.CultureInfo.InvariantCulture);
        var rows = ids.Skip(skip).Take(top).Select(id => $$"""{"pullRequestId":{{id}},"title":"Request {{id}}","sourceRefName":"refs/heads/b{{id}}","targetRefName":"refs/heads/main"}""").ToList();
        return Answer(HttpStatusCode.OK, $$"""{"value":[{{string.Join(',', rows)}}],"count":{{rows.Count}}}""", "application/json");
    }

    private static Uri Web(FakeAzureDevOps fake, string relative) => new(fake.Endpoints.WebBase, relative);

    private static (AzureDevOpsClient Client, RecordingHttpHandler Recorder) Scripted(Func<HttpRequestMessage, HttpResponseMessage> script, ListLog? log = null, int pageSize = 100)
    {
        var recorder = new RecordingHttpHandler(script);
        return (new AzureDevOpsClient(Service, () => recorder, log ?? new ListLog(), new ManualTimeProvider(), pageSize: pageSize), recorder);
    }

    /// <summary>The credential of a <c>Basic</c> header, decoded; null without one.</summary>
    private static string? BasicOf(RecordingHttpHandler.SentRequest sent) =>
        sent.Header("Authorization") is { } header && header.StartsWith("Basic ", StringComparison.Ordinal)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]))
            : null;

    /// <summary>An error body in r5 1.4's shape (MEASURED), made-up words.</summary>
    private static string Error(string message, string typeKey) =>
        $$"""{"$id":"1","innerException":null,"message":{{System.Text.Json.JsonSerializer.Serialize(message)}},"typeName":"Microsoft.Fake.{{typeKey}}, Microsoft.Fake","typeKey":"{{typeKey}}","errorCode":0,"eventId":3000}""";

    private static HttpResponseMessage Answer(HttpStatusCode status, string body, string? contentType, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        if (contentType is not null)
        {
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType + "; charset=utf-8");
        }

        foreach (var (name, value) in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }

    private static HttpResponseMessage AnswerBytes(HttpStatusCode status, byte[] body, params (string Name, string Value)[] headers)
    {
        var response = Answer(status, string.Empty, "application/json", headers);
        response.Content = new ByteArrayContent(body);
        response.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
        return response;
    }
}
