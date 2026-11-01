using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.Http;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The shared HTTP core (docs/phase-7b-design.md 5.2, 5.5; D284, D285, D358, D368) through a test
/// profile naming GitLab over scripted answers: the token on the profile's origin alone, the headers
/// on every hop, redirects and the profile's refusals of them, the 10 MB bound, a byte order mark,
/// refused successes and empty ones, the provider, address and request id on every failure, e-mail
/// screening, kept answers, headers without case, pages, one request at a time, the time limit,
/// the texts, and log lines that hold no query, token, header or body.
/// </summary>
public sealed class HostingHttpClientTests
{
    private const string Token = "glpat-FAKE-ada-1";

    private static readonly Uri Origin = new("https://gitlab.example/api/v4/");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_token_goes_to_the_profiles_origin_alone_and_every_request_carries_the_headers()
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}"));
        string[] targets =
        [
            "https://gitlab.example/api/v4/user",
            "https://gitlab.example/elsewhere/on/the/origin",
            "http://gitlab.example/api/v4/user",
            "https://gitlab.example:8443/api/v4/user",
            "https://elsewhere.example/api/v4/user",
        ];

        foreach (var target in targets)
        {
            Assert.True((await setup.GetAsync(new Uri(target))).Succeeded, target);
        }

        Assert.Equal("VisualCommit/0.1.0", HostingHttpClient.UserAgent);
        Assert.Equal(GitHubClient.UserAgent, HostingHttpClient.UserAgent);
        Assert.Equal(new[] { true, true, false, false, false }, setup.Recorder.Sent.Select(sent => sent.Header("Authorization") == "Bearer " + Token));
        Assert.All(setup.Recorder.Sent.Skip(2), sent => Assert.Null(sent.Header("Authorization")));
        Assert.All(setup.Recorder.Sent, sent =>
        {
            Assert.Equal(HostingHttpClient.UserAgent, sent.Header("User-Agent"));
            Assert.Equal(TestProfile.HeaderValue, sent.Header(TestProfile.HeaderName));
        });
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task A_redirect_on_the_origin_is_repeated_with_its_method_body_and_headers_after_the_profile_was_asked()
    {
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath == "/api/v4/projects/7/merge_requests"
            ? RecordingHttpHandler.Json(HttpStatusCode.TemporaryRedirect, "{}", ("Location", "/api/v4/projects/8/merge_requests"), ("X-Request-Id", "R-1"))
            : RecordingHttpHandler.Json(HttpStatusCode.Created, "{\"name\":\"made\",\"number\":8}"));

        var reply = await setup.Client.SendAsync(HttpMethod.Post, Token, new Uri(Origin, "projects/7/merge_requests?x=1"), new CoreTestJson { Name = "Add the form" },
            CoreTestJsonContext.Default.CoreTestJson, Cancelled);

        Assert.Equal((true, 201, "made", 8), (reply.Succeeded, reply.Status, reply.Value!.Name, reply.Value.Number));
        Assert.Equal(
            new[] { "POST https://gitlab.example/api/v4/projects/7/merge_requests?x=1", "POST https://gitlab.example/api/v4/projects/8/merge_requests" },
            setup.Recorder.Sent.Select(sent => $"{sent.Method} {sent.Url.AbsoluteUri}"));
        Assert.All(setup.Recorder.Sent, sent =>
        {
            Assert.Equal("{\"name\":\"Add the form\",\"number\":0}", sent.Body);
            Assert.Equal("application/json; charset=utf-8", sent.Header("Content-Type"));
            Assert.Equal("Bearer " + Token, sent.Header("Authorization"));
            Assert.Equal(HostingHttpClient.UserAgent, sent.Header("User-Agent"));
            Assert.Equal(TestProfile.HeaderValue, sent.Header(TestProfile.HeaderName));
        });
        var (exchange, location) = Assert.Single(setup.Profile.Redirects);
        Assert.Equal((307, "https://gitlab.example/api/v4/projects/7/merge_requests?x=1", "POST", 0), (exchange.Status, exchange.Url.AbsoluteUri, exchange.Method.Method, exchange.Body.Length));
        Assert.Equal("https://gitlab.example/api/v4/projects/8/merge_requests", location.AbsoluteUri);
        Assert.Empty(setup.Log.Lines);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Three_redirects_are_followed_and_a_fourth_is_malformed(int redirects, bool succeeds)
    {
        using var setup = Setup.Scripted(request =>
        {
            var hop = int.Parse(request.RequestUri!.Segments[^1], CultureInfo.InvariantCulture);
            return hop < redirects
                ? RecordingHttpHandler.Json(HttpStatusCode.MovedPermanently, "{}", ("Location", FormattableString.Invariant($"/api/v4/hop/{hop + 1}")))
                : RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}");
        });

        var reply = await setup.GetAsync("hop/0");

        Assert.Equal(succeeds, reply.Succeeded);
        Assert.Equal(4, setup.Recorder.Sent.Count);

        // The profile is asked on every redirect, the fourth too, before the core's rule.
        Assert.Equal(succeeds ? 3 : 4, setup.Profile.Redirects.Count);
        if (!succeeds)
        {
            Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.Provider));
            Assert.Equal("GitLab: GET /api/v4/hop/3 failed: 301, a fourth redirect.", Assert.Single(setup.Log.Lines).Message);
        }
    }

    [Theory]
    [InlineData(null, "302 without Location")]
    [InlineData("https://elsewhere.example/api/v4/user", "redirected to https://elsewhere.example:443")]
    [InlineData("http://gitlab.example/api/v4/user", "redirected to http://gitlab.example:80")]
    [InlineData("https://gitlab.example:8443/api/v4/user", "redirected to https://gitlab.example:8443")]
    public async Task A_redirect_without_a_location_or_off_the_origin_is_malformed_and_nothing_is_sent_there(string? location, string warning)
    {
        using var setup = Setup.Scripted(_ => location is null
            ? RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("X-Request-Id", "R-1"))
            : RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", location), ("X-Request-Id", "R-1")));

        var reply = await setup.GetAsync("user?private_token=x");

        var failure = reply.Failure!;
        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab, "https://gitlab.example/api/v4/user", "R-1", 302),
            (failure.Kind, failure.Message, failure.Provider, failure.Url!.AbsoluteUri, failure.RequestId, reply.Status));
        Assert.Equal("https://gitlab.example/api/v4/user?private_token=x", Assert.Single(setup.Recorder.Sent).Url.AbsoluteUri);
        Assert.Equal($"GitLab: GET /api/v4/user failed: {warning} (request R-1).", Assert.Single(setup.Log.Lines).Message);
        Assert.Equal(location is null ? 0 : 1, setup.Profile.Redirects.Count);
    }

    [Fact]
    public async Task A_redirect_the_profile_refuses_ends_the_request_on_the_same_origin_too()
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "/users/sign_in"), ("X-Request-Id", "R-1")));
        setup.Profile.OnRedirect = (_, location) => location.AbsolutePath == "/users/sign_in" ? new HostingFailure(HostingFailureKind.BadCredentials, "Sign in first.") : null;

        var refused = await setup.GetAsync("user?x=1");

        var failure = refused.Failure!;
        Assert.Equal((HostingFailureKind.BadCredentials, "Sign in first.", HostingProvider.GitLab, "https://gitlab.example/api/v4/user", "R-1"),
            (failure.Kind, failure.Message, failure.Provider, failure.Url!.AbsoluteUri, failure.RequestId));
        Assert.Single(setup.Recorder.Sent);

        // The warning names the status, unless the profile words it.
        setup.Profile.OnWarning = (exchange, given) => given.Kind == HostingFailureKind.BadCredentials && HostingHttpClient.IsRedirect(exchange.Status) ? "sent to sign in" : null;
        await setup.GetAsync("user");

        Assert.Equal(
            new[] { "GitLab: GET /api/v4/user failed: 302 (request R-1).", "GitLab: GET /api/v4/user failed: sent to sign in (request R-1)." },
            setup.Log.Lines.Select(line => line.Message));
        Assert.Equal(2, setup.Recorder.Sent.Count);
    }

    [Fact]
    public async Task An_answer_over_10_mb_is_malformed_by_its_length_or_by_reading_and_one_of_10_mb_is_read()
    {
        var size = 0;
        var sized = true;
        using var setup = Setup.Scripted(_ =>
        {
            var json = Encoding.UTF8.GetBytes("\"" + new string('x', size - 2) + "\"");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = sized ? new ByteArrayContent(json) : new UnsizedContent(json) };
        });

        foreach (var (bytes, withLength) in new[] { (HostingHttpClient.MaxBodyBytes + 1, true), (HostingHttpClient.MaxBodyBytes + 1, false), (HostingHttpClient.MaxBodyBytes, false) })
        {
            (size, sized) = (bytes, withLength);
            var reply = await setup.Client.SendAsync(HttpMethod.Get, Token, new Uri(Origin, "big"), null, CoreTestJsonContext.Default.String, Cancelled);

            if (bytes > HostingHttpClient.MaxBodyBytes)
            {
                Assert.Equal((HostingFailureKind.Malformed, HostingProvider.GitLab, 200), (reply.Failure!.Kind, reply.Failure.Provider, reply.Status));
            }
            else
            {
                Assert.Equal(bytes - 2, reply.Value!.Length);
            }
        }

        Assert.Equal(new[] { "GitLab: GET /api/v4/big failed: an answer over 10 MB.", "GitLab: GET /api/v4/big failed: an answer over 10 MB." }, setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task A_leading_byte_order_mark_is_removed_before_a_success_or_an_error_is_read()
    {
        static HttpResponseMessage Marked(HttpStatusCode status, string json) =>
            new(status) { Content = new ByteArrayContent([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(json)]) };
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath.EndsWith("/ok", StringComparison.Ordinal)
            ? Marked(HttpStatusCode.OK, "{\"name\":\"marked\"}")
            : Marked(HttpStatusCode.BadRequest, "{\"message\":\"The branch is missing.\"}"));

        var ok = await setup.GetAsync("ok");
        var error = await setup.GetAsync("error");

        Assert.Equal("marked", ok.Value!.Name);
        Assert.Equal((HostingFailureKind.Validation, "The branch is missing."), (error.Failure!.Kind, error.Failure.Message));
        Assert.All(setup.Profile.Seen, exchange => Assert.Equal((byte)'{', exchange.Body[0]));
        Assert.Equal(2, setup.Profile.Seen.Count);
    }

    [Fact]
    public async Task A_success_the_profile_refuses_is_not_parsed_and_the_others_are_offered_to_it_first()
    {
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath.EndsWith("/html", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation) { Content = new StringContent("<html>sign in</html>", Encoding.UTF8, "text/html"), Headers = { { "X-Request-Id", "R-2" } } }
            : RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}"));
        setup.Profile.OnSuccess = exchange => exchange.Status == 203 ? new HostingFailure(HostingFailureKind.BadCredentials, "Not signed in.") : null;

        var refused = await setup.GetAsync("html?x=1");
        var ok = await setup.GetAsync("json");

        Assert.Equal((HostingFailureKind.BadCredentials, "Not signed in.", HostingProvider.GitLab, "https://gitlab.example/api/v4/html", "R-2", 203),
            (refused.Failure!.Kind, refused.Failure.Message, refused.Failure.Provider, refused.Failure.Url!.AbsoluteUri, refused.Failure.RequestId, refused.Status));
        Assert.Equal("a", ok.Value!.Name);
        Assert.Equal(new[] { 203, 200 }, setup.Profile.Seen.Select(exchange => exchange.Status));
        Assert.Equal("text/html; charset=utf-8", setup.Profile.Seen[0].Headers["content-type"]);
        Assert.Equal("GitLab: GET /api/v4/html failed: 203 (request R-2).", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task A_status_the_profile_takes_as_empty_success_is_neither_parsed_nor_classified()
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.Conflict, "{\"message\":\"empty\"}", ("X-Request-Id", "R-3")));

        var reply = await setup.GetAsync("projects/7/repository/commits");

        Assert.Equal((true, 409, (CoreTestJson?)null, "R-3"), (reply.Succeeded, reply.Status, reply.Value, reply.RequestId));
        Assert.Empty(setup.Profile.Seen);
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task An_answer_that_cannot_be_read_is_malformed()
    {
        using var setup = Setup.Scripted(request => RecordingHttpHandler.Json(HttpStatusCode.OK, request.RequestUri!.AbsolutePath.EndsWith("/empty", StringComparison.Ordinal) ? string.Empty : "not json", ("X-Request-Id", "R-4")));

        foreach (var path in new[] { "text", "empty" })
        {
            var reply = await setup.GetAsync(path);
            Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", "R-4"), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.RequestId));
        }

        Assert.Equal(new[] { "GitLab: GET /api/v4/text failed: Malformed (request R-4).", "GitLab: GET /api/v4/empty failed: Malformed (request R-4)." }, setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task Every_failure_names_the_provider_its_address_without_the_query_and_its_request_id()
    {
        using var setup = new Setup(async (request, ct) =>
        {
            var path = request.RequestUri!.AbsolutePath["/api/v4/".Length..];
            var id = ("X-Request-Id", "R-" + path);
            switch (path)
            {
                case "hold":
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    throw new InvalidOperationException("Not reached.");
                case "unreachable":
                    throw new HttpRequestException("No such host is known.");
                case "classified":
                    return RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"404 Project Not Found\"}", id);
                case "refused-success":
                    return RecordingHttpHandler.Json(HttpStatusCode.OK, "{}", id);
                case "refused-redirect":
                    return RecordingHttpHandler.Json(HttpStatusCode.MovedPermanently, "{}", ("Location", "/refuse"), id);
                case "elsewhere":
                    return RecordingHttpHandler.Json(HttpStatusCode.MovedPermanently, "{}", ("Location", "https://elsewhere.example/"), id);
                case "big":
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[HostingHttpClient.MaxBodyBytes + 1]), Headers = { { id.Item1, id.Item2 } } };
                case "unreadable":
                    return RecordingHttpHandler.Json(HttpStatusCode.OK, "[", id);
                case "kept-by-the-profile":
                    return RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{}", id);
                default:
                    return RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"name\":\"a\"}]", ("Link", "<https://elsewhere.example/api/v4/pages?page=2>; rel=\"next\""), id);
            }
        }, timeout: TimeSpan.FromSeconds(1));
        setup.Profile.OnSuccess = exchange => exchange.Url.AbsolutePath.EndsWith("/refused-success", StringComparison.Ordinal) ? new HostingFailure(HostingFailureKind.BadCredentials, "Refused.") : null;
        setup.Profile.OnRedirect = (_, location) => location.AbsolutePath == "/refuse" ? new HostingFailure(HostingFailureKind.BadCredentials, "Refused.") : null;
        setup.Profile.OnClassify = exchange => exchange.Status == 403
            ? new HostingFailure(HostingFailureKind.Forbidden, "Kept.", Url: new Uri("https://gitlab.example/kept"), RequestId: "kept")
            : null;
        (string Path, HostingFailureKind Kind, string? RequestId)[] cases =
        [
            ("hold", HostingFailureKind.Network, null),
            ("unreachable", HostingFailureKind.Network, null),
            ("classified", HostingFailureKind.Validation, "R-classified"),
            ("refused-success", HostingFailureKind.BadCredentials, "R-refused-success"),
            ("refused-redirect", HostingFailureKind.BadCredentials, "R-refused-redirect"),
            ("elsewhere", HostingFailureKind.Malformed, "R-elsewhere"),
            ("big", HostingFailureKind.Malformed, "R-big"),
            ("unreadable", HostingFailureKind.Malformed, "R-unreadable"),
        ];

        foreach (var (path, kind, requestId) in cases)
        {
            var failure = (await setup.GetAsync(path + "?private_token=x&page=1")).Failure!;
            Assert.Equal((kind, HostingProvider.GitLab, "https://gitlab.example/api/v4/" + path, requestId), (failure.Kind, failure.Provider, failure.Url!.AbsoluteUri, failure.RequestId));
        }

        var pages = (await setup.Client.GetPagesAsync(Token, new Uri(Origin, "pages?private_token=x"), CoreTestJsonContext.Default.ListCoreTestJson, 10, Cancelled)).Failure!;
        Assert.Equal((HostingFailureKind.Malformed, HostingProvider.GitLab, "https://gitlab.example/api/v4/pages", "R-pages"), (pages.Kind, pages.Provider, pages.Url!.AbsoluteUri, pages.RequestId));
        var answer = setup.Client.MalformedAnswer(HttpMethod.Post, new Uri(Origin, "answer?private_token=x"), "R-9");
        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab, "https://gitlab.example/api/v4/answer", "R-9"),
            (answer.Kind, answer.Message, answer.Provider, answer.Url!.AbsoluteUri, answer.RequestId));

        // What the profile set itself is kept.
        var kept = (await setup.GetAsync("kept-by-the-profile?private_token=x")).Failure!;
        Assert.Equal((HostingProvider.GitLab, "https://gitlab.example/kept", "kept"), (kept.Provider, kept.Url!.AbsoluteUri, kept.RequestId));

        Assert.Equal(
            new[]
            {
                "GitLab: GET /api/v4/hold failed: time-out.",
                "GitLab: GET /api/v4/unreachable failed: Network.",
                "GitLab: GET /api/v4/classified failed: 404 (request R-classified).",
                "GitLab: GET /api/v4/refused-success failed: 200 (request R-refused-success).",
                "GitLab: GET /api/v4/refused-redirect failed: 301 (request R-refused-redirect).",
                "GitLab: GET /api/v4/elsewhere failed: redirected to https://elsewhere.example:443 (request R-elsewhere).",
                "GitLab: GET /api/v4/big failed: an answer over 10 MB (request R-big).",
                "GitLab: GET /api/v4/unreadable failed: Malformed (request R-unreadable).",
                "GitLab: GET /api/v4/pages failed: Link to https://elsewhere.example:443 (request R-pages).",
                "GitLab: POST /api/v4/answer failed: Malformed (request R-9).",
                "GitLab: GET /api/v4/kept-by-the-profile failed: 403 (request kept).",
            },
            setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task E_mail_addresses_are_screened_out_of_every_failures_message_and_detail()
    {
        using var setup = Setup.Scripted(request => RecordingHttpHandler.Json(
            request.RequestUri!.AbsolutePath.EndsWith("/ok", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.Forbidden, "{}"));
        setup.Profile.OnClassify = _ => new HostingFailure(HostingFailureKind.Forbidden, "The identity 'Ada Lovelace <ada@example.com>' may not push; ask eff@example.org.", "ada@example.com and git@example.com");
        setup.Profile.OnSuccess = _ => new HostingFailure(HostingFailureKind.BadCredentials, "Signed in as ada@example.com.", "No address here.");

        var classified = (await setup.GetAsync("push")).Failure!;
        var refused = (await setup.GetAsync("ok")).Failure!;

        Assert.Equal(("The identity 'Ada Lovelace <e-mail> may not push; ask <e-mail>", "<e-mail> and <e-mail>"), (classified.Message, classified.Detail));
        Assert.Equal(("Signed in as <e-mail>", "No address here."), (refused.Message, refused.Detail));
        Assert.All(setup.Log.Lines, line => Assert.DoesNotContain("@", line.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ada@example.com", "<e-mail>")]
    [InlineData("from ada@example.com, today", "from <e-mail> today")]
    [InlineData("a@b@c and d@e", "<e-mail> and <e-mail>")]
    [InlineData("an @ alone, @start and end@", "an @ alone, @start and end@")]
    [InlineData("A pull request already exists for octo:feature/login.", "A pull request already exists for octo:feature/login.")]
    [InlineData("", "")]
    public void An_e_mail_shaped_run_is_an_at_sign_between_non_blank_characters(string text, string screened)
    {
        Assert.Equal(screened, HostingHttpClient.ScreenEmail(text));
    }

    [Fact]
    public async Task A_kept_answer_serves_a_304_with_its_body_and_kept_headers_under_the_304s_own()
    {
        using var setup = Setup.Scripted(request => request.Headers.IfNoneMatch.ToString() == "\"v1\""
            ? RecordingHttpHandler.Json(HttpStatusCode.NotModified, string.Empty, ("ETag", "\"v1\""), ("X-Kept", "fresh"), ("X-Request-Id", "R-2"))
            : RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"kept\"}", ("ETag", "\"v1\""), ("Link", "<https://gitlab.example/api/v4/x?page=2>; rel=\"next\""),
                ("X-Kept", "old"), ("X-Other", "not kept"), ("X-Request-Id", "R-1")));
        var url = new Uri(Origin, "projects/7");

        var first = await setup.GetAsync(url);
        var second = await setup.GetAsync(url);
        var others = await setup.GetAsync(url, "glpat-FAKE-eff-1");
        var posted = await setup.Client.SendAsync(HttpMethod.Post, Token, url, null, CoreTestJsonContext.Default.CoreTestJson, Cancelled);
        setup.Client.ForgetKept();
        var forgotten = await setup.GetAsync(url);
        var again = await setup.GetAsync(url);

        Assert.Equal((200, 304, 200, 200, 200, 304), (first.Status, second.Status, others.Status, posted.Status, forgotten.Status, again.Status));
        Assert.Equal(("kept", "kept"), (first.Value!.Name, second.Value!.Name));
        Assert.Equal(("fresh", "R-2", "R-2", "\"v1\"", false), (second.Headers["x-kept"], second.Headers["X-Request-Id"], second.RequestId, second.Headers["ETag"], second.Headers.ContainsKey("X-Other")));
        Assert.Equal("<https://gitlab.example/api/v4/x?page=2>; rel=\"next\"", second.Headers["link"]);
        Assert.Null(second.Next);
        Assert.Equal(new[] { false, true, false, false, false, true }, setup.Recorder.Sent.Select(sent => sent.Header("If-None-Match") is not null));
        Assert.Equal((false, true), (setup.Profile.Seen[0].Status == 304, setup.Profile.Seen[1].Status == 304));
        Assert.Equal("kept", JsonSerializer.Deserialize(setup.Profile.Seen[1].Body, CoreTestJsonContext.Default.CoreTestJson)!.Name);
    }

    [Fact]
    public async Task Kept_answers_are_bounded_by_their_bytes_and_the_oldest_stored_goes_first()
    {
        // Bodies of 1,000 bytes under a budget of 2,500: two are kept. The script answers 304 to a
        // URL's ETag.
        using var setup = Setup.Scripted(request =>
        {
            var etag = $"\"{request.RequestUri!.AbsolutePath[^1]}\"";
            if (request.Headers.IfNoneMatch.ToString() == etag)
            {
                return RecordingHttpHandler.Json(HttpStatusCode.NotModified, string.Empty, ("ETag", etag));
            }

            const string Start = "{\"name\":\"";
            return RecordingHttpHandler.Json(HttpStatusCode.OK, Start + new string('x', 1000 - Start.Length - 2) + "\"}", ("ETag", etag));
        }, keptBytes: 2500);

        foreach (var (path, status) in new[] { ("a", 200), ("b", 200), ("c", 200), ("b", 304), ("a", 200), ("c", 304), ("b", 200) })
        {
            Assert.Equal(status, (await setup.GetAsync(path)).Status);
        }
    }

    [Fact]
    public async Task Headers_are_read_without_case_and_a_header_given_twice_is_joined()
    {
        using var setup = Setup.Scripted(_ =>
        {
            var response = RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}", ("x-request-id", "R-5"), ("ratelimit-remaining", "59"));
            response.Headers.TryAddWithoutValidation("X-Twice", "one");
            response.Headers.TryAddWithoutValidation("x-twice", "two");
            return response;
        });

        var reply = await setup.GetAsync("user");

        Assert.Equal(("R-5", "59", "one, two"), (reply.RequestId, reply.Headers["RateLimit-Remaining"], reply.Headers["X-TWICE"]));
        Assert.Equal("application/json; charset=utf-8", reply.Headers["Content-Type"]);
    }

    [Theory]
    [InlineData("<https://a.example/p?page=2>; rel=\"next\", <https://a.example/p?page=9>; rel=\"last\"", "https://a.example/p?page=2")]
    [InlineData("<https://a.example/p?page=9>; rel=\"last\", <https://a.example/p?page=3>; rel=\"prev next\"", "https://a.example/p?page=3")]
    [InlineData("<https://a.example/p?page=2>;rel=\"NEXT\"", "https://a.example/p?page=2")]
    [InlineData("</p?page=2>; rel=\"next\"", null)]
    [InlineData("<https://a.example/p?page=1>; rel=\"prev\"", null)]
    [InlineData("", null)]
    public void HostingLinks_reads_the_next_page_of_a_link_header_as_given(string link, string? next)
    {
        var withoutCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Link"] = link };
        var withCase = new Dictionary<string, string>(StringComparer.Ordinal) { ["link"] = link };

        Assert.Equal(next, HostingLinks.Next(withoutCase)?.AbsoluteUri);
        Assert.Equal(next, HostingLinks.Next(withCase)?.AbsoluteUri);
        Assert.Null(HostingLinks.Next(new Dictionary<string, string>()));
    }

    [Theory]
    [InlineData(3, 3, "https://gitlab.example/api/v4/items?page=4")]
    [InlineData(5, 5, null)]
    [InlineData(10, 5, null)]
    public async Task Pages_follow_the_profiles_next_up_to_the_cap_and_give_the_next_page_left(int maxPages, int count, string? next)
    {
        using var setup = Setup.Scripted(request =>
        {
            var page = int.Parse(request.RequestUri!.Query["?page=".Length..], CultureInfo.InvariantCulture);
            var id = ("X-Request-Id", FormattableString.Invariant($"R-{page}"));
            return page < 5
                ? RecordingHttpHandler.Json(HttpStatusCode.OK, FormattableString.Invariant($"[{{\"number\":{page}}}]"), ("Link", FormattableString.Invariant($"<https://gitlab.example/api/v4/items?page={page + 1}>; rel=\"next\"")), id)
                : RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":5}]", id);
        });
        var observed = new List<string>();

        var reply = await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items?page=1"), CoreTestJsonContext.Default.ListCoreTestJson, maxPages, Cancelled, headers => observed.Add(headers["X-Request-Id"]));

        Assert.True(reply.Succeeded);
        Assert.Equal(Enumerable.Range(1, count), reply.Value!.Select(item => item.Number));
        Assert.Equal(next, reply.Next?.AbsoluteUri);
        Assert.Equal(FormattableString.Invariant($"R-{count}"), reply.RequestId);
        Assert.Equal(Enumerable.Range(1, count).Select(page => FormattableString.Invariant($"R-{page}")), observed);
        Assert.Equal(count, setup.Recorder.Sent.Count);
    }

    [Theory]
    [InlineData("https://elsewhere.example/api/v4/items?page=2", "https://elsewhere.example:443")]
    [InlineData("http://gitlab.example/api/v4/items?page=2", "http://gitlab.example:80")]
    [InlineData("https://gitlab.example/other/items?page=2", "https://gitlab.example:443")]
    public async Task A_next_page_off_the_origin_or_its_path_is_malformed_and_not_asked_for(string next, string origin)
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":1}]", ("Link", $"<{next}>; rel=\"next\""), ("X-Request-Id", "R-1")));

        var reply = await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items"), CoreTestJsonContext.Default.ListCoreTestJson, 10, Cancelled);

        Assert.Equal((HostingFailureKind.Malformed, (List<CoreTestJson>?)null, (Uri?)null), (reply.Failure!.Kind, reply.Value, reply.Next));
        Assert.Single(setup.Recorder.Sent);
        Assert.Equal($"GitLab: GET /api/v4/items failed: Link to {origin} (request R-1).", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task A_failing_page_fails_the_list()
    {
        using var setup = Setup.Scripted(request => request.RequestUri!.Query == "?page=1"
            ? RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":1}]", ("Link", "<https://gitlab.example/api/v4/items?page=2>; rel=\"next\""), ("X-Request-Id", "R-1"))
            : RecordingHttpHandler.Json(HttpStatusCode.InternalServerError, "{\"message\":\"500 Internal Server Error\"}", ("X-Request-Id", "R-2")));
        setup.Profile.OnClassify = exchange => new HostingFailure(HostingFailureKind.ServerError, HostingHttpTexts.ServerError(HostingProvider.GitLab, exchange.Status));
        var observed = new List<string>();

        var reply = await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items?page=1"), CoreTestJsonContext.Default.ListCoreTestJson, 10, Cancelled, headers => observed.Add(headers["X-Request-Id"]));

        Assert.Equal((HostingFailureKind.ServerError, "GitLab had a problem: HTTP 500.", "R-2", 500), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.RequestId, reply.Status));
        Assert.Null(reply.Value);
        Assert.Equal(new[] { "R-1" }, observed);
    }

    [Fact]
    public async Task A_second_call_waits_while_the_first_is_held_and_its_own_cancellation_ends_the_wait()
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var setup = new Setup(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/held", StringComparison.Ordinal))
            {
                held.TrySetResult();
                await release.Task.WaitAsync(ct);
            }

            return RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}");
        });

        var first = setup.GetAsync("held");
        await held.Task.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        var second = setup.GetAsync("second");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);
        var third = setup.Client.SendAsync(HttpMethod.Get, Token, new Uri(Origin, "third"), null, CoreTestJsonContext.Default.CoreTestJson, cancel.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Cancelled);

        Assert.False(second.IsCompleted);
        Assert.Single(setup.Recorder.Sent);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => third.WaitAsync(TimeSpan.FromSeconds(60), Cancelled));
        Assert.Single(setup.Recorder.Sent);

        release.SetResult();
        Assert.True((await first.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Succeeded);
        Assert.True((await second.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Succeeded);
        Assert.Equal(new[] { "/api/v4/held", "/api/v4/second" }, setup.Recorder.Sent.Select(sent => sent.Url.AbsolutePath));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task Cancelling_a_call_in_flight_throws_and_writes_nothing()
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var setup = new Setup(async (_, ct) =>
        {
            held.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Not reached.");
        });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);

        var call = setup.Client.SendAsync(HttpMethod.Get, Token, new Uri(Origin, "user"), null, CoreTestJsonContext.Default.CoreTestJson, cancel.Token);
        await held.Task.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(60), Cancelled));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task The_time_limit_gives_the_providers_time_out_text_and_no_answer_its_network_text()
    {
        using var setup = new Setup(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/slow", StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            throw new HttpRequestException("No such host is known. (gitlab.example:443)");
        }, timeout: TimeSpan.FromSeconds(1));

        var slow = await setup.GetAsync("slow").WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        var gone = await setup.GetAsync("gone");

        Assert.Equal((HostingFailureKind.Network, "GitLab did not answer within 30 seconds.", 0), (slow.Failure!.Kind, slow.Failure.Message, slow.Status));
        Assert.Equal((HostingFailureKind.Network, "Could not reach GitLab.", 0), (gone.Failure!.Kind, gone.Failure.Message, gone.Status));
        Assert.Equal(new[] { "GitLab: GET /api/v4/slow failed: time-out.", "GitLab: GET /api/v4/gone failed: Network." }, setup.Log.Lines.Select(line => line.Message));
        Assert.Empty(slow.Headers);
    }

    [Theory]
    [InlineData(HostingProvider.GitHub, "Could not reach GitHub.", "GitHub did not answer within 30 seconds.", "GitHub's answer could not be read.", "GitHub had a problem: HTTP 502.")]
    [InlineData(HostingProvider.GitLab, "Could not reach GitLab.", "GitLab did not answer within 30 seconds.", "GitLab's answer could not be read.", "GitLab had a problem: HTTP 502.")]
    [InlineData(HostingProvider.AzureDevOps, "Could not reach Azure DevOps.", "Azure DevOps did not answer within 30 seconds.", "Azure DevOps' answer could not be read.", "Azure DevOps had a problem: HTTP 502.")]
    public void The_texts_name_the_provider(HostingProvider provider, string network, string timeout, string malformed, string serverError)
    {
        Assert.Equal(
            (network, timeout, malformed, serverError),
            (HostingHttpTexts.Network(provider), HostingHttpTexts.Timeout(provider), HostingHttpTexts.Malformed(provider), HostingHttpTexts.ServerError(provider, 502)));
    }

    [Fact]
    public void GitHubs_texts_are_the_github_clients_constants()
    {
        Assert.Equal(
            (GitHubClient.NetworkText, GitHubClient.TimeoutText, GitHubClient.MalformedText),
            (HostingHttpTexts.Network(HostingProvider.GitHub), HostingHttpTexts.Timeout(HostingProvider.GitHub), HostingHttpTexts.Malformed(HostingProvider.GitHub)));
    }

    [Fact]
    public async Task No_log_line_holds_a_query_a_token_a_header_or_a_body()
    {
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath.EndsWith("/moved", StringComparison.Ordinal)
            ? RecordingHttpHandler.Json(HttpStatusCode.Found, "{\"secret\":\"BODY-SECRET\"}", ("Location", "https://elsewhere.example/HEADER-SECRET?q=QUERY-SECRET"), ("X-Secret", "HEADER-SECRET"))
            : RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{\"message\":\"BODY-SECRET ada@example.com\"}", ("X-Secret", "HEADER-SECRET"), ("X-Request-Id", "R-1")));

        await setup.GetAsync("projects/7?private_token=QUERY-SECRET&author=ada%40example.com");
        await setup.GetAsync("moved?private_token=QUERY-SECRET");
        await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items?private_token=QUERY-SECRET"), CoreTestJsonContext.Default.ListCoreTestJson, 3, Cancelled);
        setup.Client.MalformedAnswer(HttpMethod.Get, new Uri(Origin, "answer?private_token=QUERY-SECRET"), null);

        Assert.Equal(4, setup.Log.Lines.Count);
        foreach (var line in setup.Log.Lines)
        {
            Assert.Equal(LogLevel.Warning, line.Level);
            foreach (var secret in new[] { Token, "QUERY-SECRET", "HEADER-SECRET", "BODY-SECRET", "private_token", "?", "ada", "example.com", TestProfile.HeaderValue, HostingHttpClient.UserAgent })
            {
                Assert.DoesNotContain(secret, line.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task A_body_of_a_type_outside_the_profiles_json_is_an_argument_exception_and_nothing_is_sent()
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "{}"));

        var refused = await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Client.SendAsync(HttpMethod.Post, Token, new Uri(Origin, "x"), new CoreOtherJson(), CoreTestJsonContext.Default.CoreTestJson, Cancelled));

        Assert.StartsWith("CoreOtherJson is not one of GitLab's shapes.", refused.Message, StringComparison.Ordinal);
        Assert.Empty(setup.Recorder.Sent);
    }

    [Fact]
    public void The_helpers_compare_and_name_origins_and_resolve_redirects()
    {
        Assert.True(HostingHttpClient.SameOrigin(new Uri("https://GitLab.example/a"), new Uri("https://gitlab.example:443/b?c")));
        Assert.False(HostingHttpClient.SameOrigin(new Uri("https://gitlab.example/"), new Uri("http://gitlab.example/")));
        Assert.False(HostingHttpClient.SameOrigin(new Uri("https://gitlab.example/"), new Uri("https://gitlab.example:8443/")));
        Assert.Equal("https://gitlab.example:443", HostingHttpClient.OriginOf(new Uri("https://gitlab.example/secret?q=1")));
        Assert.Equal(new[] { 301, 302, 307, 308 }, Enumerable.Range(300, 10).Where(HostingHttpClient.IsRedirect));
        Assert.Equal("https://gitlab.example/api/v4/b", HostingHttpClient.Resolve(new Uri("https://gitlab.example/api/v4/a"), new Uri("b", UriKind.Relative))!.AbsoluteUri);
        Assert.Null(HostingHttpClient.Resolve(new Uri("https://gitlab.example/"), null));
        Assert.Equal("https://gitlab.example/a", HostingHttpClient.WithoutQuery(new Uri("https://gitlab.example/a?private_token=x#f")).AbsoluteUri);
    }

    /// <summary>A handler that answers each request with what <c>answer</c> gives, asynchronously, with the request's cancellation.</summary>
    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await answer(request, cancellationToken).ConfigureAwait(false);
            response.RequestMessage = request;
            return response;
        }
    }

    /// <summary>A body whose length is not known before it is read (no <c>Content-Length</c>).</summary>
    private sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// A profile naming GitLab on <see cref="Origin"/>: <c>Bearer</c>, one header of its own, 409 as
    /// empty success, <c>Link</c> pages, <c>X-Request-Id</c>, and <c>Link</c> and <c>X-Kept</c>
    /// kept; each hook settable, and every exchange it is given recorded.
    /// </summary>
    private sealed class TestProfile : HostingHttpProfile
    {
        public const string HeaderName = "X-Profile";

        public const string HeaderValue = "PROFILE-HEADER";

        private readonly List<HostingExchange> _seen = [];
        private readonly List<(HostingExchange Exchange, Uri Location)> _redirects = [];

        public override HostingProvider Provider => HostingProvider.GitLab;

        public override Uri TokenOrigin => Origin;

        public override JsonSerializerContext Json => CoreTestJsonContext.Default;

        public override IReadOnlyCollection<string> KeptHeaderNames => ["Link", "X-Kept"];

        public Func<HostingExchange, HostingFailure?>? OnClassify { get; set; }

        public Func<HostingExchange, HostingFailure?>? OnSuccess { get; set; }

        public Func<HostingExchange, Uri, HostingFailure?>? OnRedirect { get; set; }

        public Func<HostingExchange, HostingFailure, string?>? OnWarning { get; set; }

        /// <summary>Every exchange given to <see cref="Classify"/> or <see cref="RefuseSuccess"/>, in order.</summary>
        public IReadOnlyList<HostingExchange> Seen
        {
            get
            {
                lock (_seen)
                {
                    return [.. _seen];
                }
            }
        }

        public IReadOnlyList<(HostingExchange Exchange, Uri Location)> Redirects
        {
            get
            {
                lock (_redirects)
                {
                    return [.. _redirects];
                }
            }
        }

        public override void Authorize(HttpRequestMessage request, string token) => request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        public override void AddHeaders(HttpRequestMessage request) => request.Headers.TryAddWithoutValidation(HeaderName, HeaderValue);

        public override HostingFailure Classify(HostingExchange exchange)
        {
            See(exchange);
            if (OnClassify?.Invoke(exchange) is { } given)
            {
                return given;
            }

            var message = exchange.Body.Length == 0 ? null : JsonSerializer.Deserialize(exchange.Body, CoreTestJsonContext.Default.CoreErrorJson)?.Message;
            return new HostingFailure(HostingFailureKind.Validation, message ?? exchange.Reason ?? "HTTP " + exchange.Status.ToString(CultureInfo.InvariantCulture));
        }

        public override HostingFailure? RefuseSuccess(HostingExchange exchange)
        {
            See(exchange);
            return OnSuccess?.Invoke(exchange);
        }

        public override HostingFailure? RefuseRedirect(HostingExchange exchange, Uri location)
        {
            lock (_redirects)
            {
                _redirects.Add((exchange, location));
            }

            return OnRedirect?.Invoke(exchange, location);
        }

        public override bool SucceedsEmpty(int status) => status == 409;

        public override Uri? NextPage(HostingExchange exchange) => HostingLinks.Next(exchange.Headers);

        public override string? RequestIdOf(IReadOnlyDictionary<string, string> headers) => headers.GetValueOrDefault("X-Request-Id");

        public override string WarningOf(HostingExchange exchange, HostingFailure failure) => OnWarning?.Invoke(exchange, failure) ?? base.WarningOf(exchange, failure);

        private void See(HostingExchange exchange)
        {
            lock (_seen)
            {
                _seen.Add(exchange);
            }
        }
    }

    /// <summary>A client over <see cref="TestProfile"/> and a recording handler, with its log.</summary>
    private sealed class Setup : IDisposable
    {
        public Setup(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer, TimeSpan? timeout = null, long? keptBytes = null)
        {
            Recorder = new RecordingHttpHandler(new AsyncHandler(answer));
            Client = new HostingHttpClient(Profile, () => Recorder, Log, new ManualTimeProvider(AppClocks.HarnessTime), timeout, keptBytes);
        }

        public TestProfile Profile { get; } = new();

        public RecordingHttpHandler Recorder { get; }

        public ListLog Log { get; } = new();

        public HostingHttpClient Client { get; }

        public static Setup Scripted(Func<HttpRequestMessage, HttpResponseMessage> script, long? keptBytes = null) =>
            new((request, _) => Task.FromResult(script(request)), keptBytes: keptBytes);

        public Task<HostingReply<CoreTestJson>> GetAsync(string relative) => GetAsync(new Uri(Origin, relative));

        public Task<HostingReply<CoreTestJson>> GetAsync(Uri url, string token = Token) =>
            Client.SendAsync(HttpMethod.Get, token, url, null, CoreTestJsonContext.Default.CoreTestJson, Cancelled);

        public void Dispose() => Client.Dispose();
    }
}

/// <summary>The test profile's one shape: a name and a number.</summary>
internal sealed class CoreTestJson
{
    public string? Name { get; set; }

    public int Number { get; set; }
}

/// <summary>The test profile's error body.</summary>
internal sealed class CoreErrorJson
{
    public string? Message { get; set; }
}

/// <summary>A shape outside the test profile's context.</summary>
internal sealed class CoreOtherJson
{
    public string? Other { get; set; }
}

/// <summary>The test profile's shapes, in snake_case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(CoreTestJson))]
[JsonSerializable(typeof(List<CoreTestJson>))]
[JsonSerializable(typeof(CoreErrorJson))]
[JsonSerializable(typeof(string))]
internal sealed partial class CoreTestJsonContext : JsonSerializerContext;
