using System.Globalization;
using System.Net;
using System.Net.Sockets;
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
/// the texts, and log lines that hold no query, token, header or body; D380's rows; D382's screen of
/// the path and the request id's shape.
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
    [InlineData("https://a\u200Db.example/api/v4/user", "redirected to https://(an unreadable host):443")]
    [InlineData("https://ada\uFF20example.com/api/v4/user", "redirected to https://(an unreadable host):443")]
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
        // The time-out alone has a short limit: every other request has the default 30 s, so a
        // slow machine cannot turn one of them into a time-out.
        using (var held = new Setup(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Not reached.");
        }, timeout: TimeSpan.FromSeconds(1)))
        {
            var timedOut = (await held.GetAsync("hold?private_token=x&page=1").WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Failure!;
            Assert.Equal((HostingFailureKind.Network, HostingProvider.GitLab, "https://gitlab.example/api/v4/hold", (string?)null),
                (timedOut.Kind, timedOut.Provider, timedOut.Url!.AbsoluteUri, timedOut.RequestId));
            Assert.Equal(new[] { "GitLab: GET /api/v4/hold failed: time-out." }, held.Log.Lines.Select(line => line.Message));
        }

        using var setup = Setup.Scripted(request =>
        {
            var path = request.RequestUri!.AbsolutePath["/api/v4/".Length..];
            var id = ("X-Request-Id", "R-" + path);
            switch (path)
            {
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
        });
        setup.Profile.OnSuccess = exchange => exchange.Url.AbsolutePath.EndsWith("/refused-success", StringComparison.Ordinal) ? new HostingFailure(HostingFailureKind.BadCredentials, "Refused.") : null;
        setup.Profile.OnRedirect = (_, location) => location.AbsolutePath == "/refuse" ? new HostingFailure(HostingFailureKind.BadCredentials, "Refused.") : null;
        setup.Profile.OnClassify = exchange => exchange.Status == 403
            ? new HostingFailure(HostingFailureKind.Forbidden, "Kept.", Url: new Uri("https://gitlab.example/kept"), RequestId: "kept")
            : null;
        (string Path, HostingFailureKind Kind, string? RequestId)[] cases =
        [
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

        // A failure the profile's client built from a reply that succeeded is finished alike.
        var built = setup.Client.Failed(HttpMethod.Get, new Uri(Origin, "built?private_token=x"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous."), "R-8");
        Assert.Equal((HostingFailureKind.BadCredentials, "Anonymous.", HostingProvider.GitLab, "https://gitlab.example/api/v4/built", "R-8"),
            (built.Kind, built.Message, built.Provider, built.Url!.AbsoluteUri, built.RequestId));

        // What the profile set itself is kept.
        var kept = (await setup.GetAsync("kept-by-the-profile?private_token=x")).Failure!;
        Assert.Equal((HostingProvider.GitLab, "https://gitlab.example/kept", "kept"), (kept.Provider, kept.Url!.AbsoluteUri, kept.RequestId));

        Assert.Equal(
            new[]
            {
                "GitLab: GET /api/v4/unreachable failed: Network (HttpRequestException).",
                "GitLab: GET /api/v4/classified failed: 404 (request R-classified).",
                "GitLab: GET /api/v4/refused-success failed: 200 (request R-refused-success).",
                "GitLab: GET /api/v4/refused-redirect failed: 301 (request R-refused-redirect).",
                "GitLab: GET /api/v4/elsewhere failed: redirected to https://elsewhere.example:443 (request R-elsewhere).",
                "GitLab: GET /api/v4/big failed: an answer over 10 MB (request R-big).",
                "GitLab: GET /api/v4/unreadable failed: Malformed (request R-unreadable).",
                "GitLab: GET /api/v4/pages failed: Link to https://elsewhere.example:443 (request R-pages).",
                "GitLab: POST /api/v4/answer failed: Malformed (request R-9).",
                "GitLab: GET /api/v4/built failed: BadCredentials (request R-8).",
                "GitLab: GET /api/v4/kept-by-the-profile failed: 403 (request kept).",
            },
            setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task E_mail_addresses_are_screened_out_of_every_failures_message_and_detail()
    {
        using var setup = Setup.Scripted(request => RecordingHttpHandler.Json(
            request.RequestUri!.AbsolutePath.EndsWith("/ok", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.Forbidden, "{}"));
        setup.Profile.OnClassify = _ => new HostingFailure(HostingFailureKind.Forbidden, "The identity 'Ada Lovelace <ada@example.com>' may not push; ask eff@example.com.", "ada@example.com and git@example.com");
        setup.Profile.OnSuccess = _ => new HostingFailure(HostingFailureKind.BadCredentials, "Signed in as ada@example.com.", "No address here.");

        var classified = (await setup.GetAsync("push")).Failure!;
        var refused = (await setup.GetAsync("ok")).Failure!;
        var built = setup.Client.Failed(HttpMethod.Get, new Uri(Origin, "built"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous, not ada@example.com.", "ada@example.com"), null);

        Assert.Equal(("The identity 'Ada Lovelace <e-mail> may not push; ask <e-mail>", "<e-mail> and <e-mail>"), (classified.Message, classified.Detail));
        Assert.Equal(("Signed in as <e-mail>", "No address here."), (refused.Message, refused.Detail));
        Assert.Equal(("Anonymous, not <e-mail>", "<e-mail>"), (built.Message, built.Detail));
        Assert.All(setup.Log.Lines, line => Assert.DoesNotContain("@", line.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ada@example.com", "<e-mail>")]
    [InlineData("from ada@example.com, today", "from <e-mail> today")]
    [InlineData("a@b@c and d@e", "<e-mail> and <e-mail>")]
    [InlineData("an @ alone, @start and end@", "an @ alone, @start and end@")]
    [InlineData("A pull request already exists for octo:feature/login.", "A pull request already exists for octo:feature/login.")]
    // A branch name with an '@' is screened too, GitHub's words included (D368's rule as written).
    [InlineData("A pull request already exists for octo:user@fix.", "A pull request already exists for <e-mail>")]
    [InlineData("No commits between main and user@fix", "No commits between main and <e-mail>")]
    [InlineData("", "")]
    // D382: a full-width at sign (U+FF20) is one too.
    [InlineData("from ada\uFF20example.com, today", "from <e-mail> today")]
    [InlineData("an \uFF20 alone", "an \uFF20 alone")]
    public void An_e_mail_shaped_run_is_an_at_sign_between_non_blank_characters(string text, string screened)
    {
        Assert.Equal(screened, HostingHttpClient.ScreenEmail(text));
    }

    [Theory]
    [InlineData("/api/v4/users/ada@example.com/keys", "/api/v4/users/<e-mail>/keys")]
    [InlineData("/api/v4/users/ada%40example.com", "/api/v4/users/<e-mail>")]
    [InlineData("/api/v4/users/ada%EF%BC%A0example.com", "/api/v4/users/<e-mail>")]
    [InlineData("/api/v4/users/Ada%20Lovelace%20%3Cada%40example.com%3E/x", "/api/v4/users/<e-mail>/x")]
    [InlineData("/api/v4/a%2Fada%40example.com/x", "/api/v4/<e-mail>/x")]
    [InlineData("/ada%40example.com/b%40example.com/", "/<e-mail>/<e-mail>/")]
    [InlineData("/api/v4/projects/7/merge_requests", "/api/v4/projects/7/merge_requests")]
    [InlineData("/api/v4/a%20b/%40start/end%40", "/api/v4/a%20b/%40start/end%40")]
    // Decoded once only: %2540 is a "%40" in the server's path, no at sign.
    [InlineData("/api/v4/x%2540y", "/api/v4/x%2540y")]
    [InlineData("/api/v4/bad%ZZ%40", "/api/v4/bad%ZZ%40")]
    [InlineData("/", "/")]
    public void A_path_segment_that_holds_an_address_once_percent_decoded_is_screened_whole(string path, string screened)
    {
        Assert.Equal(screened, HostingHttpClient.ScreenPath(path));
    }

    [Fact]
    public async Task An_address_a_redirect_or_a_next_page_puts_in_the_path_reaches_neither_the_warning_nor_the_failures_address()
    {
        // D382, the second hunt's finding: the server names the path, so D368's screen covers it.
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v4/plain" => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "/api/v4/users/ada@example.com/x?private_token=x")),
            "/api/v4/encoded" => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "/api/v4/users/ada%40example.com/x")),
            "/api/v4/full-width" => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "/api/v4/users/ada%EF%BC%A0example.com/x")),
            "/api/v4/items" => RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":1}]", ("Link", "<https://gitlab.example/api/v4/users/ada%40example.com/items?page=2>; rel=\"next\"")),
            "/api/v4/kept" => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{}"),
            _ => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"),
        });
        setup.Profile.OnClassify = exchange => exchange.Status == 403
            ? new HostingFailure(HostingFailureKind.Forbidden, "Kept.", Url: new Uri("https://gitlab.example/users/ada@example.com/kept"))
            : null;

        var failures = new List<HostingFailure>();
        foreach (var path in new[] { "plain", "encoded", "full-width" })
        {
            failures.Add((await setup.GetAsync(path)).Failure!);
        }

        failures.Add((await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items"), CoreTestJsonContext.Default.ListCoreTestJson, 10, Cancelled)).Failure!);
        failures.Add((await setup.GetAsync("kept")).Failure!);
        failures.Add(setup.Client.Failed(HttpMethod.Get, new Uri(Origin, "users/ada%40example.com"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous."), null));

        Assert.Equal(
            new[]
            {
                "https://gitlab.example/api/v4/users/%3Ce-mail%3E/x",
                "https://gitlab.example/api/v4/users/%3Ce-mail%3E/x",
                "https://gitlab.example/api/v4/users/%3Ce-mail%3E/x",
                "https://gitlab.example/api/v4/users/%3Ce-mail%3E/items",
                "https://gitlab.example/users/%3Ce-mail%3E/kept",
                "https://gitlab.example/api/v4/users/%3Ce-mail%3E",
            },
            failures.Select(failure => failure.Url!.AbsoluteUri));
        Assert.Equal(
            new[]
            {
                "GitLab: GET /api/v4/users/<e-mail>/x failed: 404.",
                "GitLab: GET /api/v4/users/<e-mail>/x failed: 404.",
                "GitLab: GET /api/v4/users/<e-mail>/x failed: 404.",
                "GitLab: GET /api/v4/users/<e-mail>/items failed: 404.",
                "GitLab: GET /api/v4/kept failed: 403.",
                "GitLab: GET /api/v4/users/<e-mail> failed: BadCredentials.",
            },
            setup.Log.Lines.Select(line => line.Message));
        Assert.Equal("/api/v4/users/ada@example.com/x", setup.Recorder.Sent[1].Url.AbsolutePath);
        foreach (var secret in new[] { "ada", "example.com", "@", "%40", "\uFF20", "%EF%BC%A0" })
        {
            Assert.DoesNotContain(secret, setup.Log.Written, StringComparison.Ordinal);
            Assert.All(failures, failure => Assert.DoesNotContain(secret, failure.Url!.AbsoluteUri, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("C0DE:1234:5678ABC:9DEF012:6543210F", true)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", true)]
    [InlineData("01HV7B4K5Z9X2A3M4N5P6Q7R8S", true)]
    [InlineData("a.b_c-d:e", true)]
    [InlineData("", false)]
    [InlineData("R 1", false)]
    [InlineData("Bearer glpat-FAKE-ada-1", false)]
    [InlineData("ada@example.com", false)]
    [InlineData("R-1/../x", false)]
    [InlineData("<b>R-1</b>", false)]
    [InlineData("R-1, R-2", false)]
    [InlineData("R-\u00E9", false)]
    public async Task A_request_id_is_kept_only_with_a_request_ids_shape_in_the_reply_the_failure_and_the_log(string id, bool kept)
    {
        // D382: the id is the server's, copied raw into every warning and failure before.
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{\"message\":\"No.\"}", ("X-Request-Id", id)));

        var reply = await setup.GetAsync("projects/7");

        var expected = kept ? id : null;
        Assert.Equal((expected, expected), (reply.RequestId, reply.Failure!.RequestId));
        Assert.Equal(kept ? $"GitLab: GET /api/v4/projects/7 failed: 403 (request {id})." : "GitLab: GET /api/v4/projects/7 failed: 403.", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task A_request_id_of_up_to_128_characters_of_its_set_is_kept_and_none_other_also_from_the_profile_or_the_client()
    {
        Assert.Equal(new string('a', 128), HostingHttpClient.ShapedRequestId(new string('a', 128)));
        Assert.Null(HostingHttpClient.ShapedRequestId(new string('a', 129)));
        Assert.Null(HostingHttpClient.ShapedRequestId("R-1\n"));
        Assert.Null(HostingHttpClient.ShapedRequestId("R-1\r\nX-Injected: 1"));
        Assert.Null(HostingHttpClient.ShapedRequestId(null));

        // A failure's own id and the one a client passes go through the same shape.
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{}", ("X-Request-Id", "R-1")));
        setup.Profile.OnClassify = _ => new HostingFailure(HostingFailureKind.Forbidden, "Kept.", RequestId: "id of ada@example.com");
        var classified = (await setup.GetAsync("kept")).Failure!;
        var built = setup.Client.Failed(HttpMethod.Get, new Uri(Origin, "built"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous."), new string('b', 129));
        var malformed = setup.Client.MalformedAnswer(HttpMethod.Get, new Uri(Origin, "answer"), "R 9");

        Assert.Equal(((string?)null, (string?)null, (string?)null), (classified.RequestId, built.RequestId, malformed.RequestId));
        Assert.Equal(
            new[] { "GitLab: GET /api/v4/kept failed: 403.", "GitLab: GET /api/v4/built failed: BadCredentials.", "GitLab: GET /api/v4/answer failed: Malformed." },
            setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task A_token_the_server_echoes_reaches_neither_a_failure_nor_the_log()
    {
        // 5.2's "never a token": a request id's shape admits every token form (D378), and a
        // Location's path or host, a message, a reason phrase and a profile's detail can hold it.
        using var setup = Setup.Scripted(request =>
        {
            static HttpResponseMessage Echo(HttpStatusCode status, string json, params (string Name, string Value)[] headers) => RecordingHttpHandler.Json(status, json, headers);
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/v4/located":
                    return Echo(HttpStatusCode.Found, "{}", ("Location", $"/api/v4/echo/{Token}/x"));
                case "/api/v4/encoded":
                    return Echo(HttpStatusCode.Found, "{}", ("Location", "/api/v4/echo/glpat%2DFAKE%2Dada%2D1"));
                case "/api/v4/message":
                    return Echo(HttpStatusCode.Unauthorized, $"{{\"message\":\"Bad credentials: {Token}\"}}", ("X-Request-Id", "R-" + Token));
                case "/api/v4/reason":
                    var reasoned = Echo(HttpStatusCode.Unauthorized, string.Empty);
                    reasoned.ReasonPhrase = "Bad token " + Token;
                    return reasoned;
                case "/api/v4/profile":
                    return Echo(HttpStatusCode.Forbidden, "{}");
                case "/api/v4/off":
                    return Echo(HttpStatusCode.Found, "{}", ("Location", $"https://{Token}.example/x"));
                default:
                    return Echo(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}", ("X-Request-Id", Token));
            }
        });
        setup.Profile.OnClassify = exchange => exchange.Url.AbsolutePath == "/api/v4/profile"
            ? new HostingFailure(HostingFailureKind.Forbidden, "No.", Detail: "Ask for " + Token, RequestId: Token)
            : null;
        setup.Profile.OnWarning = (exchange, _) => exchange.Url.AbsolutePath == "/api/v4/profile" ? "403 for " + Token : null;

        var failures = new List<HostingFailure>();
        foreach (var path in new[] { "id", "located", "encoded", "message", "reason", "profile", "off" })
        {
            failures.Add((await setup.GetAsync(path)).Failure!);
        }

        Assert.Equal(
            new[]
            {
                "https://gitlab.example/api/v4/id",
                "https://gitlab.example/api/v4/echo/%3Ctoken%3E/x",
                "https://gitlab.example/api/v4/echo/%3Ctoken%3E",
                "https://gitlab.example/api/v4/message",
                "https://gitlab.example/api/v4/reason",
                "https://gitlab.example/api/v4/profile",
                "https://gitlab.example/api/v4/off",
            },
            failures.Select(failure => failure.Url!.AbsoluteUri));
        Assert.Equal(
            new[] { "Not Found", "Not Found", "Not Found", "Bad credentials: <token>", "Bad token <token>", "No.", HostingHttpTexts.Malformed(HostingProvider.GitLab) },
            failures.Select(failure => failure.Message));
        Assert.Equal("Ask for <token>", failures[5].Detail);
        Assert.All(failures, failure => Assert.Null(failure.RequestId));
        Assert.Equal(
            new[]
            {
                "GitLab: GET /api/v4/id failed: 404.",
                "GitLab: GET /api/v4/echo/<token>/x failed: 404.",
                "GitLab: GET /api/v4/echo/<token> failed: 404.",
                "GitLab: GET /api/v4/message failed: 401.",
                "GitLab: GET /api/v4/reason failed: 401.",
                "GitLab: GET /api/v4/profile failed: 403 for <token>.",
                "GitLab: GET /api/v4/off failed: redirected to https://<token>.example:443.",
            },
            setup.Log.Lines.Select(line => line.Message));

        // The premise: the server was given the token, and named it back.
        Assert.Contains(setup.Recorder.Sent, sent => sent.Url.AbsolutePath == $"/api/v4/echo/{Token}/x" && sent.Header("Authorization") == "Bearer " + Token);
        foreach (var held in new[] { Token, "FAKE-ada" })
        {
            Assert.DoesNotContain(held, setup.Log.Written, StringComparison.OrdinalIgnoreCase);
            Assert.All(failures, failure => Assert.DoesNotContain(held, $"{failure.Url} {failure.Message} {failure.Detail} {failure.RequestId}", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void A_failure_a_client_builds_screens_the_token_it_is_given_as_the_core_screens_its_own()
    {
        // An address a client builds from a record whose path holds the token (a hostile server's
        // project path, say): given the request's token, MalformedAnswer and Failed screen it in the
        // address, the message, the detail, the request id and the warning; without one, as before.
        using var setup = Setup.Scripted(_ => throw new InvalidOperationException("No request is sent here."));
        var held = new Uri(Origin, $"projects/{Token}%2Fp/merge_requests?private_token=x");

        var malformed = setup.Client.MalformedAnswer(HttpMethod.Get, held, "R-1", Token);
        var built = setup.Client.Failed(HttpMethod.Post, new Uri(Origin, $"projects/{Token}"),
            new HostingFailure(HostingFailureKind.BadCredentials, $"Anonymous {Token}.", "Ask for " + Token.ToUpperInvariant()), "R-" + Token, Token);
        var unscreened = setup.Client.MalformedAnswer(HttpMethod.Get, held, "R-3");
        var unscreenedBuilt = setup.Client.Failed(HttpMethod.Get, new Uri(Origin, $"projects/{Token}"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous."), "R-4");

        Assert.Equal(
            ("https://gitlab.example/api/v4/projects/%3Ctoken%3E/merge_requests", "R-1", HostingHttpTexts.Malformed(HostingProvider.GitLab)),
            (malformed.Url!.AbsoluteUri, malformed.RequestId, malformed.Message));
        Assert.Equal(
            ("https://gitlab.example/api/v4/projects/%3Ctoken%3E", (string?)null, "Anonymous <token>.", "Ask for <token>"),
            (built.Url!.AbsoluteUri, built.RequestId, built.Message, built.Detail));
        Assert.Equal(
            ($"https://gitlab.example/api/v4/projects/{Token}%2Fp/merge_requests", "R-3", $"https://gitlab.example/api/v4/projects/{Token}", "R-4"),
            (unscreened.Url!.AbsoluteUri, unscreened.RequestId, unscreenedBuilt.Url!.AbsoluteUri, unscreenedBuilt.RequestId));
        Assert.Equal(
            new[]
            {
                "GitLab: GET /api/v4/projects/<token>/merge_requests failed: Malformed (request R-1).",
                "GitLab: POST /api/v4/projects/<token> failed: BadCredentials.",
                $"GitLab: GET /api/v4/projects/{Token}%2Fp/merge_requests failed: Malformed (request R-3).",
                $"GitLab: GET /api/v4/projects/{Token} failed: BadCredentials (request R-4).",
            },
            setup.Log.Lines.Select(line => line.Message));
        Assert.Empty(setup.Recorder.Sent);
    }

    [Fact]
    public async Task A_credential_the_profile_sends_in_another_form_is_screened_as_the_token_is()
    {
        // Azure DevOps' Basic of ":<token>" in Base64, which a server can echo as it is sent.
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + Token));
        using var setup = Setup.Scripted(request => RecordingHttpHandler.Json(HttpStatusCode.Unauthorized,
            $"{{\"message\":\"Bad credentials: {request.Headers.Authorization}\"}}"));
        setup.Profile.AuthorizationOf = token => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + token));

        var failure = (await setup.GetAsync("user")).Failure!;

        Assert.Equal("Basic " + basic, Assert.Single(setup.Recorder.Sent).Header("Authorization"));
        Assert.Equal("Bad credentials: Basic <token>", failure.Message);
        Assert.Equal("GitLab: GET /api/v4/user failed: 401.", Assert.Single(setup.Log.Lines).Message);
        Assert.DoesNotContain(basic, setup.Log.Written, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("malformed")]
    [InlineData("located")]
    [InlineData("message")]
    [InlineData("reason")]
    public async Task A_token_echoed_on_the_wire_reaches_no_line_of_the_core_or_the_session_and_no_failure(string echo)
    {
        // The hunt's (a) to (f) over the real handler: an API that names back the token it was
        // given, in GitHub's request id, a same-origin Location, a 401's message or its status line.
        var token = "ghp_FAKE" + new string('a', 36);
        await using var server = new RawHttpServer(head =>
        {
            var given = RawHttpServer.HeaderOf(head, "Authorization")?.Replace("Bearer ", string.Empty, StringComparison.Ordinal) ?? "none";
            var path = head.Split(' ')[1];
            static byte[] Answer(string status, string body, string headers = "") => RawHttpServer.Bytes(
                $"HTTP/1.1 {status}\r\n{headers}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
            return echo switch
            {
                "github" => Answer("404 Not Found", "{\"message\":\"Not Found\"}", $"X-GitHub-Request-Id: {given}\r\n"),
                "malformed" => Answer("200 OK", "{}", $"X-GitHub-Request-Id: {given}\r\n"),
                "located" when path == "/api/v3/user" => Answer("302 Found", string.Empty, $"Location: /api/v3/echo/{given}\r\n"),
                "located" => Answer("404 Not Found", "{\"message\":\"Not Found\"}"),
                "message" => Answer("401 Unauthorized", $"{{\"message\":\"Bad credentials: {given}\"}}", $"X-GitHub-Request-Id: {given}\r\n"),
                _ => Answer($"401 Bad token {given}", string.Empty),
            };
        });
        var endpoints = GitHubEndpoints.Local(server.Port);
        var log = new ListLog();
        using var data = new TempDirectory("hosting-echo");
        using var session = new HostingSession(new AppPaths(data.Path), new HostingOptions(endpoints, HostingOptions.DefaultHandler(endpoints)),
            new global::VisualCommit.Hosting.Tokens.MemoryTokenStore(), log, action => action(), TimeProvider.System);

        var failure = (await session.SignInAsync(token, Cancelled)).Failure!;

        Assert.All(server.Heads, head => Assert.Equal("Bearer " + token, RawHttpServer.HeaderOf(head, "Authorization")));
        Assert.Equal(echo == "located" ? 2 : 1, server.Heads.Count);
        var (kind, message) = echo switch
        {
            "github" or "located" => (HostingFailureKind.NotFound, "Not Found"),
            "malformed" => (HostingFailureKind.Malformed, GitHubClient.MalformedText),
            "message" => (HostingFailureKind.BadCredentials, "Bad credentials: <token>"),
            _ => (HostingFailureKind.BadCredentials, "Bad token <token>"),
        };
        Assert.Equal((kind, message, (string?)null), (failure.Kind, failure.Message, failure.RequestId));
        Assert.Equal(echo == "located" ? "/api/v3/echo/%3Ctoken%3E" : "/api/v3/user", failure.Url!.AbsolutePath);
        Assert.Equal(
            new[]
            {
                $"GitHub: GET {(echo == "located" ? "/api/v3/echo/<token>" : "/api/v3/user")} failed: {(echo == "malformed" ? "Malformed" : echo is "message" or "reason" ? "401" : "404")}.",
                $"GitHub: signing in on 127.0.0.1:{server.Port} failed: {kind}, {message}",
            },
            log.Lines.Where(line => line.Level == LogLevel.Warning).Select(line => line.Message));
        Assert.DoesNotContain(token, log.Written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, $"{failure.Url} {failure.Message} {failure.Detail}", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"été\"")]
    [InlineData("\"e 1\"")]
    [InlineData("\"e\u00011\"")]
    public async Task An_etag_of_anything_but_visible_ascii_is_not_kept_so_the_next_get_is_a_full_one(string etag)
    {
        // RFC 9110 8.8.3's etagc admits obs-text, which .NET refuses to send back (D380: nothing a
        // server sends breaks the core); W/"…" and "…" of visible ASCII are kept, as every service's are.
        using var setup = Setup.Scripted(request => request.Headers.Contains("If-None-Match")
            ? RecordingHttpHandler.Json(HttpStatusCode.NotModified, string.Empty)
            : RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"name\":\"a\"}", ("ETag", etag)));

        var first = await setup.GetAsync("projects/7");
        var second = await setup.GetAsync("projects/7");

        Assert.Equal(("a", 200, "a", 200), (first.Value?.Name, first.Status, second.Value?.Name, second.Status));
        Assert.All(setup.Recorder.Sent, sent => Assert.Null(sent.Header("If-None-Match")));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task An_etag_in_obs_text_on_the_wire_leaves_the_next_get_of_its_url_sent_and_answered()
    {
        // The hunt's run: the kept ETag made every later GET of the URL fail unsent as Network.
        var answers = 0;
        await using var server = new RawHttpServer(_ => RawHttpServer.Bytes(Interlocked.Increment(ref answers) == 1
            ? "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nETag: \"été\"\r\nContent-Length: 12\r\nConnection: close\r\n\r\n{\"name\":\"a\"}"
            : "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 12\r\nConnection: close\r\n\r\n{\"name\":\"b\"}"));
        var origin = new Uri(server.Base, "api/v4/");
        using var setup = new WireSetup(origin, HostingOptions.DefaultHandler(origin));

        var first = await setup.GetAsync("user");
        var second = await setup.GetAsync("user");
        var third = await setup.GetAsync("user");

        Assert.Equal(("a", "b", "b"), (first.Value?.Name, second.Value?.Name, third.Value?.Name));
        Assert.Equal(3, server.Heads.Count);
        Assert.All(server.Heads, head => Assert.Null(RawHttpServer.HeaderOf(head, "If-None-Match")));
        Assert.Empty(setup.Log.Lines);
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
    [InlineData("<//a.example/p?page=2>; rel=\"next\"", null)]
    [InlineData("<\\\\a.example\\p?page=2>; rel=\"next\"", null)]
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
    [InlineData("https://a\u200Db.example/api/v4/items?page=2", "https://(an unreadable host):443")]
    [InlineData("https://ada\uFF20example.com/api/v4/items?page=2", "https://(an unreadable host):443")]
    public async Task A_next_page_off_the_origin_or_its_path_is_malformed_and_not_asked_for(string next, string origin)
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":1}]", ("Link", $"<{next}>; rel=\"next\""), ("X-Request-Id", "R-1")));

        var reply = await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items"), CoreTestJsonContext.Default.ListCoreTestJson, 10, Cancelled);

        Assert.Equal((HostingFailureKind.Malformed, (List<CoreTestJson>?)null, (Uri?)null), (reply.Failure!.Kind, reply.Value, reply.Next));
        Assert.Single(setup.Recorder.Sent);
        Assert.Equal($"GitLab: GET /api/v4/items failed: Link to {origin} (request R-1).", Assert.Single(setup.Log.Lines).Message);
    }

    // The review before the gate, tokens-1: the profile is asked of a next page on the origin and
    // under its path, before it is asked for or given as Next at the cap; a refusal or a hook that
    // throws (D380) ends the list Malformed with one warning and nothing sent there.
    [Theory]
    [InlineData("refused", 10)]
    [InlineData("refused", 1)]
    [InlineData("throws", 10)]
    public async Task A_next_page_the_profile_does_not_follow_is_malformed_and_not_asked_for(string hook, int maxPages)
    {
        using var setup = Setup.Scripted(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"number\":1}]", ("Link", "<https://gitlab.example/api/v4/items?page=2>; rel=\"next\""), ("X-Request-Id", "R-1")));
        var asked = new List<string>();
        setup.Profile.OnPage = next =>
        {
            asked.Add(next.AbsoluteUri);
            return hook == "throws" ? throw new OverflowException("Too large.") : false;
        };

        var reply = await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items?page=1"), CoreTestJsonContext.Default.ListCoreTestJson, maxPages, Cancelled);

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", (List<CoreTestJson>?)null, (Uri?)null, "R-1"),
            (reply.Failure!.Kind, reply.Failure.Message, reply.Value, reply.Next, reply.Failure.RequestId));
        Assert.Equal(["https://gitlab.example/api/v4/items?page=2"], asked);
        Assert.Single(setup.Recorder.Sent);
        Assert.Equal(
            hook == "throws" ? "GitLab: GET /api/v4/items failed: Malformed (OverflowException) (request R-1)." : "GitLab: GET /api/v4/items failed: Link outside the API (request R-1).",
            Assert.Single(setup.Log.Lines).Message);
        Assert.All(setup.Log.Entries, entry => Assert.Null(entry.Exception));
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
        Assert.Equal(new[] { "GitLab: GET /api/v4/slow failed: time-out.", "GitLab: GET /api/v4/gone failed: Network (HttpRequestException)." }, setup.Log.Lines.Select(line => line.Message));
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
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath switch
        {
            var path when path.EndsWith("/moved", StringComparison.Ordinal) =>
                RecordingHttpHandler.Json(HttpStatusCode.Found, "{\"secret\":\"BODY-SECRET\"}", ("Location", "https://elsewhere.example/HEADER-SECRET?q=QUERY-SECRET"), ("X-Secret", "HEADER-SECRET")),

            // D380: .NET's own messages quote what a proxy or a server sent; none reaches the log.
            var path when path.EndsWith("/thrown", StringComparison.Ordinal) => throw new HttpRequestException(
                HttpRequestError.InvalidResponse,
                $"Received an invalid header line: 'X-Echo Bearer {Token}'; proxy 'http://proxyuser:PROXY-SECRET@127.0.0.1:9/'; ada@example.com",
                new IOException("BODY-SECRET " + Token)),
            _ => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{\"message\":\"BODY-SECRET ada@example.com\"}", ("X-Secret", "HEADER-SECRET"), ("X-Request-Id", "R-1")),
        });

        await setup.GetAsync("projects/7?private_token=QUERY-SECRET&author=ada%40example.com");
        await setup.GetAsync("moved?private_token=QUERY-SECRET");
        await setup.Client.GetPagesAsync(Token, new Uri(Origin, "items?private_token=QUERY-SECRET"), CoreTestJsonContext.Default.ListCoreTestJson, 3, Cancelled);
        setup.Client.MalformedAnswer(HttpMethod.Get, new Uri(Origin, "answer?private_token=QUERY-SECRET"), null);
        var thrown = await setup.GetAsync("thrown?private_token=QUERY-SECRET");

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitLab."), (thrown.Failure!.Kind, thrown.Failure.Message));
        Assert.Equal("GitLab: GET /api/v4/thrown failed: Network (HttpRequestException InvalidResponse).", setup.Log.Lines[^1].Message);
        Assert.Equal(5, setup.Log.Lines.Count);
        Assert.All(setup.Log.Entries, entry => Assert.Null(entry.Exception));
        foreach (var line in setup.Log.Lines)
        {
            Assert.Equal(LogLevel.Warning, line.Level);
        }

        foreach (var secret in new[] { Token, "QUERY-SECRET", "HEADER-SECRET", "BODY-SECRET", "PROXY-SECRET", "proxyuser", "private_token", "?", "ada", "example.com", TestProfile.HeaderValue, HostingHttpClient.UserAgent })
        {
            Assert.DoesNotContain(secret, setup.Log.Written, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_exception_is_described_by_its_type_its_http_error_and_an_inner_sockets_code_alone()
    {
        // D380: the findings' messages, as .NET wrote them (measured on 10.0), and what the log says of each.
        const string Echoed = "Bearer " + Token;
        (Exception Exception, string Described)[] rows =
        [
            (new HttpRequestException(HttpRequestError.ProxyTunnelError, "The proxy tunnel request to proxy 'http://proxyuser:PROXY-SECRET@127.0.0.1:50637/' failed with status code '407'."),
                "HttpRequestException ProxyTunnelError"),
            (new HttpRequestException(HttpRequestError.InvalidResponse, $"Received an invalid header line: 'X-Echo {Echoed}'."), "HttpRequestException InvalidResponse"),
            (new HttpRequestException(HttpRequestError.InvalidResponse, "Received an invalid header line: 'Set-Cookie session=SECRET-COOKIE-123'."), "HttpRequestException InvalidResponse"),
            (new HttpRequestException(HttpRequestError.InvalidResponse, "Received an invalid header name: 'ada@example.com x'."), "HttpRequestException InvalidResponse"),
            (new HttpIOException(HttpRequestError.InvalidResponse, "Received chunk header length could not be parsed: '7B-22-6C-6F-67-69-6E-22'."), "HttpIOException InvalidResponse"),
            (new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely. ada@example.com"), "HttpIOException ResponseEnded"),
            (new HttpRequestException(HttpRequestError.ConnectionError, "No connection could be made (127.0.0.1:9)", new SocketException((int)SocketError.ConnectionRefused)),
                "HttpRequestException ConnectionError, SocketException ConnectionRefused"),
            (new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known. (gitlab.example:443)", new IOException("ada@example.com", new SocketException((int)SocketError.HostNotFound))),
                "HttpRequestException NameResolutionError, SocketException HostNotFound"),
            (new HttpRequestException("No such host is known. (gitlab.example:443)"), "HttpRequestException"),
            (new TaskCanceledException("The operation was canceled.", new TimeoutException("A connection could not be made within 10 seconds.")), "TaskCanceledException"),
            (new IOException("Unable to read data: " + Token, new SocketException((int)SocketError.ConnectionReset)), "IOException, SocketException ConnectionReset"),
            (new SocketException((int)SocketError.ConnectionAborted), "SocketException ConnectionAborted"),
        ];

        Assert.Equal(rows.Select(row => row.Described), rows.Select(row => HostingHttpClient.Describe(row.Exception)));
    }

    [Theory]
    [InlineData("echo")]
    [InlineData("cookie")]
    [InlineData("address")]
    [InlineData("chunk")]
    public async Task An_answer_that_breaks_http_on_the_wire_is_no_answer_and_the_log_holds_nothing_it_sent(string answer)
    {
        // D380, the finding's (b) to (e) over the real handler: .NET's exception quotes the line.
        await using var server = new RawHttpServer(head => RawHttpServer.Bytes(answer switch
        {
            "echo" => $"HTTP/1.1 200 OK\r\nX-Echo {RawHttpServer.HeaderOf(head, "Authorization")}\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}",
            "cookie" => "HTTP/1.1 200 OK\r\nSet-Cookie session=SECRET-COOKIE-123\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}",
            "address" => "HTTP/1.1 200 OK\r\nada@example.com x: y\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}",
            _ => "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n{\"login\":\"ada\",\"email\":\"ada@example.com\"}\r\n",
        }));
        var origin = new Uri(server.Base, "api/v4/");
        var handler = HostingOptions.DefaultHandler(origin);
        using var setup = new WireSetup(origin, handler);

        var premise = await ThrownReadingAsync(handler(), new Uri(origin, "user"));
        var reply = await setup.GetAsync("user");

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitLab.", 0), (reply.Failure!.Kind, reply.Failure.Message, reply.Status));
        Assert.Equal(2, server.Heads.Count);
        Assert.All(server.Heads, head => Assert.Equal("Bearer " + Token, RawHttpServer.HeaderOf(head, "Authorization")));
        var secret = answer switch { "echo" => Token, "cookie" => "SECRET-COOKIE-123", "address" => "ada@example.com", _ => "7B-22" };
        Assert.Contains(secret, premise, StringComparison.Ordinal);
        var (level, message, exception) = Assert.Single(setup.Log.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Null(exception);
        Assert.Matches(@"^GitLab: GET /api/v4/user failed: Network \(Http(Request|IO)Exception( [A-Za-z]+)?(, SocketException [A-Za-z]+)?\)\.$", message);
        foreach (var held in new[] { Token, "SECRET-COOKIE-123", "ada", "example.com", "7B-22", "Received" })
        {
            Assert.DoesNotContain(held, setup.Log.Written, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_proxy_that_refuses_its_password_leaves_the_password_out_of_the_log()
    {
        // D380, the finding's (a): a proxy that keeps answering 407 (no Proxy-Authenticate), whose
        // address holds its user and password, as HTTPS_PROXY gives them.
        await using var proxy = new RawHttpServer(_ => RawHttpServer.Bytes("HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        var proxyAddress = new Uri($"http://proxyuser:PROXY-SECRET@127.0.0.1:{proxy.Port}/");
        var origin = new Uri("https://127.0.0.2:9/api/v4/");
        HttpMessageHandler Handler() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseProxy = true,
            Proxy = new FixedProxy(proxyAddress),
        };
        using var setup = new WireSetup(origin, Handler);

        var premise = await ThrownReadingAsync(Handler(), new Uri(origin, "user"));
        var reply = await setup.GetAsync("user");

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitLab."), (reply.Failure!.Kind, reply.Failure.Message));
        Assert.NotEmpty(proxy.Heads);
        Assert.All(proxy.Heads, head => Assert.StartsWith("CONNECT 127.0.0.2:9 ", head, StringComparison.Ordinal));
        Assert.All(proxy.Heads, head => Assert.Null(RawHttpServer.HeaderOf(head, "Authorization")));
        Assert.Contains("PROXY-SECRET", premise, StringComparison.Ordinal);
        var (_, message, exception) = Assert.Single(setup.Log.Entries);
        Assert.Null(exception);
        Assert.Equal("GitLab: GET /api/v4/user failed: Network (HttpRequestException ProxyTunnelError).", message);
        foreach (var held in new[] { "PROXY-SECRET", "proxyuser", Token, "127.0.0.1" })
        {
            Assert.DoesNotContain(held, setup.Log.Written, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("http://user:hunter2@127.0.0.1:5000/api/v3/next", "http://127.0.0.1:5000/api/v3/next")]
    [InlineData("https://user:hunter2@gitlab.example/api/v4/next?private_token=x#f", "https://gitlab.example/api/v4/next")]
    [InlineData("https://user@gitlab.example:443/api/v4/a", "https://gitlab.example/api/v4/a")]
    [InlineData("https://:hunter2@gitlab.example:8443/a%20b?q", "https://gitlab.example:8443/a%20b")]
    [InlineData("https://gitlab.example/a?private_token=x#f", "https://gitlab.example/a")]
    [InlineData("https://gitlab.example/", "https://gitlab.example/")]
    // D382: nor an address in its path.
    [InlineData("https://user:hunter2@gitlab.example/api/v4/users/ada%40example.com/keys?q", "https://gitlab.example/api/v4/users/%3Ce-mail%3E/keys")]
    [InlineData("https://gitlab.example/api/v4/users/ada@example.com", "https://gitlab.example/api/v4/users/%3Ce-mail%3E")]
    public void An_address_without_its_query_keeps_no_userinfo(string url, string without)
    {
        Assert.Equal(without, HostingHttpClient.WithoutQuery(new Uri(url)).AbsoluteUri);
    }

    [Fact]
    public async Task A_failures_address_keeps_no_userinfo_a_same_origin_redirects_location_and_the_profiles_own_included()
    {
        // D380, the finding's 302 with a user and password in Location, then a 404.
        using var setup = Setup.Scripted(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v4/user" => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "https://user:hunter2@gitlab.example/api/v4/next?private_token=x")),
            "/api/v4/kept" => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{}"),
            _ => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"),
        });
        setup.Profile.OnClassify = exchange => exchange.Status == 403
            ? new HostingFailure(HostingFailureKind.Forbidden, "Kept.", Url: new Uri("https://user:hunter2@gitlab.example/kept?private_token=x"))
            : null;

        var redirected = (await setup.GetAsync("user")).Failure!;
        var kept = (await setup.GetAsync("kept")).Failure!;
        var built = setup.Client.Failed(HttpMethod.Get, new Uri("https://user:hunter2@gitlab.example/api/v4/built?private_token=x"), new HostingFailure(HostingFailureKind.BadCredentials, "Anonymous."), null);

        Assert.Equal((HostingFailureKind.Validation, "Not Found", "https://gitlab.example/api/v4/next"), (redirected.Kind, redirected.Message, redirected.Url!.AbsoluteUri));
        Assert.Equal("https://gitlab.example/kept", kept.Url!.AbsoluteUri);
        Assert.Equal("https://gitlab.example/api/v4/built", built.Url!.AbsoluteUri);
        Assert.Equal("Bearer " + Token, setup.Recorder.Sent[1].Header("Authorization"));
        Assert.DoesNotContain("hunter2", setup.Log.Written, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("InvalidDataException")]
    [InlineData("InvalidOperationException")]
    public async Task A_body_that_cannot_be_decompressed_is_malformed_and_nothing_throws(string thrown)
    {
        // D380: .NET's gzip and deflate decoders throw InvalidDataException, its brotli decoder
        // InvalidOperationException, as the body is read (measured).
        using var setup = Setup.Scripted(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ThrowingContent(thrown == "InvalidDataException"
                ? new InvalidDataException("The archive entry was compressed using an unsupported compression method. ada@example.com")
                : new InvalidOperationException("Decoder ran into invalid data. " + Token)),
            Headers = { { "X-Request-Id", "R-7" } },
        });

        var reply = await setup.GetAsync("user?private_token=x");

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab, "https://gitlab.example/api/v4/user", "R-7", 200),
            (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.Provider, reply.Failure.Url!.AbsoluteUri, reply.Failure.RequestId, reply.Status));
        Assert.Equal($"GitLab: GET /api/v4/user failed: a body that could not be decompressed ({thrown}) (request R-7).", Assert.Single(setup.Log.Lines).Message);
        Assert.All(setup.Log.Entries, entry => Assert.Null(entry.Exception));
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("deflate")]
    [InlineData("br")]
    public async Task A_body_its_content_encoding_does_not_decode_on_the_wire_is_malformed_and_nothing_throws(string encoding)
    {
        // D380, the finding's answers: a 200 whose Content-Encoding names a coding its body is not in.
        const string Body = "{\"login\":\"ada\",\"email\":\"ada@example.com\"}";
        await using var server = new RawHttpServer(_ => RawHttpServer.Bytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Encoding: {encoding}\r\nContent-Length: {Body.Length}\r\nX-Request-Id: R-7\r\nConnection: close\r\n\r\n{Body}"));
        var origin = new Uri(server.Base, "api/v4/");
        using var setup = new WireSetup(origin, HostingOptions.DefaultHandler(origin));

        var reply = await setup.GetAsync("user");

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", "R-7", 200), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.RequestId, reply.Status));
        Assert.Matches(@"^GitLab: GET /api/v4/user failed: a body that could not be decompressed \(Invalid(Data|Operation)Exception\) \(request R-7\)\.$", Assert.Single(setup.Log.Lines).Message);
        Assert.All(setup.Log.Entries, entry => Assert.Null(entry.Exception));
    }

    [Theory]
    [InlineData("classify")]
    [InlineData("success")]
    [InlineData("redirect")]
    public async Task A_profile_hook_that_throws_on_an_answer_gives_malformed_and_nothing_throws(string hook)
    {
        // D380: a header value out of range, say, read by a profile's hook.
        using var setup = Setup.Scripted(_ => hook switch
        {
            "classify" => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, "{}", ("X-Request-Id", "R-6")),
            "success" => RecordingHttpHandler.Json(HttpStatusCode.OK, "{}", ("X-Request-Id", "R-6")),
            _ => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}", ("Location", "/api/v4/next"), ("X-Request-Id", "R-6")),
        });
        setup.Profile.OnClassify = _ => throw new ArgumentOutOfRangeException("seconds", "Valid values are between -62135596800 and 253402300799. ada@example.com");
        setup.Profile.OnSuccess = _ => throw new FormatException("Not a date: " + Token);
        setup.Profile.OnRedirect = (_, _) => throw new OverflowException("Too large.");

        var reply = await setup.GetAsync("user?private_token=x");

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", "https://gitlab.example/api/v4/user", "R-6"),
            (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.Url!.AbsoluteUri, reply.Failure.RequestId));
        var thrown = hook switch { "classify" => "ArgumentOutOfRangeException", "success" => "FormatException", _ => "OverflowException" };
        Assert.Equal($"GitLab: GET /api/v4/user failed: Malformed ({thrown}) (request R-6).", Assert.Single(setup.Log.Lines).Message);
        Assert.All(setup.Log.Entries, entry => Assert.Null(entry.Exception));
        Assert.Single(setup.Recorder.Sent);
    }

    [Fact]
    public async Task A_location_on_the_wire_whose_host_has_no_idn_form_is_malformed_and_nothing_throws()
    {
        // D380, the hunt's host: Location's bytes are the UTF-8 of https://a<U+200D>b.example/..., which
        // .NET reads as that host; its IdnHost throws, and the core takes it as on no origin.
        var location = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("https://a\u200Db.example/api/v4/user"));
        await using var server = new RawHttpServer(_ => RawHttpServer.Bytes(
            $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nX-Request-Id: R-8\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        var origin = new Uri(server.Base, "api/v4/");
        using var setup = new WireSetup(origin, HostingOptions.DefaultHandler(origin));

        var reply = await setup.GetAsync("user");

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", "R-8", 302), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.RequestId, reply.Status));
        Assert.Equal("GitLab: GET /api/v4/user failed: redirected to https://(an unreadable host):443 (request R-8).", Assert.Single(setup.Log.Lines).Message);
        Assert.Single(server.Heads);
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
        Assert.Equal("xn--bcher-kva.example", HostingHttpClient.IdnHostOf(new Uri("https://b\u00FCcher.example/")));

        // D380: hosts Uri.TryCreate takes whose IdnHost throws (measured) are on no origin, their own
        // included, and named without the host as sent.
        foreach (var unreadable in new[] { "https://ada\uFF20example.com/x", "https://a\u200Db.example/x", "https://\u2488.example/x", "https://a\uFF0Fb.example/x", "https://a\uFF1A80.example/x" })
        {
            Assert.True(Uri.TryCreate(unreadable, UriKind.Absolute, out var url), unreadable);
            Assert.Throws<UriFormatException>(() => url.IdnHost);
            Assert.Null(HostingHttpClient.IdnHostOf(url));
            Assert.False(HostingHttpClient.SameOrigin(url, Origin));
            Assert.False(HostingHttpClient.SameOrigin(Origin, url));
            Assert.False(HostingHttpClient.SameOrigin(url, url));
            Assert.Equal("https://(an unreadable host):443", HostingHttpClient.OriginOf(url));
            Assert.Equal(HostingHttpClient.OriginOf(url), GitHubUrls.OriginOf(url));
            Assert.False(GitHubUrls.SameOrigin(url, url));
        }

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

    /// <summary>What .NET's own exception says when <paramref name="url"/> is read with the token over <paramref name="handler"/>, without the core: the finding's premise (D380).</summary>
    private static async Task<string> ThrownReadingAsync(HttpMessageHandler handler, Uri url)
    {
        using var http = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Cancelled);
            await response.Content.ReadAsByteArrayAsync(Cancelled);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return ex.ToString();
        }

        return string.Empty;
    }

    /// <summary>A body whose reading throws <c>thrown</c>, as .NET's decoders do on a body their coding does not hold.</summary>
    private sealed class ThrowingContent(Exception thrown) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.FromException(thrown);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>A proxy for every address, with the user and password its address holds, as <c>HTTPS_PROXY</c> gives them.</summary>
    private sealed class FixedProxy(Uri address) : IWebProxy
    {
        public ICredentials? Credentials { get; set; } = new NetworkCredential(
            Uri.UnescapeDataString(address.UserInfo.Split(':')[0]), Uri.UnescapeDataString(address.UserInfo.Split(':')[1]));

        public Uri GetProxy(Uri destination) => address;

        public bool IsBypassed(Uri host) => false;
    }

    /// <summary>A client over <see cref="TestProfile"/> on another origin and a real handler, with its log.</summary>
    private sealed class WireSetup : IDisposable
    {
        public WireSetup(Uri origin, Func<HttpMessageHandler> createHandler)
        {
            Origin = origin;
            Client = new HostingHttpClient(new TestProfile(origin), createHandler, Log, new ManualTimeProvider(AppClocks.HarnessTime));
        }

        public Uri Origin { get; }

        public ListLog Log { get; } = new();

        public HostingHttpClient Client { get; }

        public Task<HostingReply<CoreTestJson>> GetAsync(string relative) =>
            Client.SendAsync(HttpMethod.Get, Token, new Uri(Origin, relative), null, CoreTestJsonContext.Default.CoreTestJson, Cancelled);

        public void Dispose() => Client.Dispose();
    }

    /// <summary>
    /// A profile naming GitLab on <see cref="Origin"/>, or on the origin given: <c>Bearer</c>, one
    /// header of its own, 409 as empty success, <c>Link</c> pages, <c>X-Request-Id</c>, and
    /// <c>Link</c> and <c>X-Kept</c> kept; each hook settable, and every exchange it is given recorded.
    /// </summary>
    private sealed class TestProfile(Uri? origin = null) : HostingHttpProfile
    {
        public const string HeaderName = "X-Profile";

        public const string HeaderValue = "PROFILE-HEADER";

        private readonly List<HostingExchange> _seen = [];
        private readonly List<(HostingExchange Exchange, Uri Location)> _redirects = [];

        public override HostingProvider Provider => HostingProvider.GitLab;

        public override Uri TokenOrigin => origin ?? Origin;

        public override JsonSerializerContext Json => CoreTestJsonContext.Default;

        public override IReadOnlyCollection<string> KeptHeaderNames => ["Link", "X-Kept"];

        public Func<HostingExchange, HostingFailure?>? OnClassify { get; set; }

        public Func<HostingExchange, HostingFailure?>? OnSuccess { get; set; }

        public Func<HostingExchange, Uri, HostingFailure?>? OnRedirect { get; set; }

        public Func<HostingExchange, HostingFailure, string?>? OnWarning { get; set; }

        /// <summary>Whether a next page is followed; every one unless set.</summary>
        public Func<Uri, bool>? OnPage { get; set; }

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

        /// <summary>The <c>Authorization</c> value of a token; <c>Bearer &lt;token&gt;</c> unless set.</summary>
        public Func<string, string>? AuthorizationOf { get; set; }

        public override void Authorize(HttpRequestMessage request, string token) =>
            request.Headers.TryAddWithoutValidation("Authorization", AuthorizationOf?.Invoke(token) ?? "Bearer " + token);

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

        public override bool FollowsPage(Uri next) => OnPage?.Invoke(next) ?? true;

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
