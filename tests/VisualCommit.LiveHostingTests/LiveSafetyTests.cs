using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using VisualCommit.Testing;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// The live command's safety nets proven offline (the lead's ruling for WP-LIVE; docs/phase-7a-design.md
/// 14.3, 14.5, D323, D324, D327): <see cref="LiveSettings"/>' message, <see cref="LiveRun"/>'s search
/// for the token in its notes, messages, a failed step's exception and run.txt,
/// <see cref="LiveRequestLog"/>'s record and its refusal of a request that would break D327's reach,
/// <see cref="GitHubLiveApi"/>'s messages and bodies, and what <see cref="LiveLeftovers"/> lets the
/// clean-up remove, with made-up tokens and in-process handlers: no network, no variables, no app.
/// They run in every run of the project, with or without the owner's token.
/// </summary>
public sealed class LiveSafetyTests
{
    /// <summary>A made-up token: no account's, and with <c>-</c>, which no GitHub token holds, so no scanner takes it for one.</summary>
    private const string Token = "github_pat_LIVE-SAFETY-made-up-0123456789-abcdefghijklmn";

    /// <summary>A made-up token that URL-escaping changes (<c>+</c>, <c>/</c>, <c>=</c>), for that form.</summary>
    private const string EscapingToken = "made+up/token=for-the-escaped-form-0123456789";

    private const string Heading = "The live command needs two Windows user environment variables, set once on this machine (D260): VISUALCOMMIT_LIVE_GITHUB_TOKEN, a fine-grained token for the sandbox repository only, and VISUALCOMMIT_LIVE_GITHUB_REPO, that repository as owner/name. ";

    private const string Ending = " Set them in Windows' Environment Variables dialog, then start a new terminal.";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    // ---- LiveSettings (14.3) -------------------------------------------------------------------

    [Theory]
    [InlineData(null, null, "Missing: VISUALCOMMIT_LIVE_GITHUB_TOKEN and VISUALCOMMIT_LIVE_GITHUB_REPO.")]
    [InlineData("  ", "\t", "Missing: VISUALCOMMIT_LIVE_GITHUB_TOKEN and VISUALCOMMIT_LIVE_GITHUB_REPO.")]
    [InlineData(null, "octo/sandbox", "Missing: VISUALCOMMIT_LIVE_GITHUB_TOKEN.")]
    [InlineData(Token, null, "Missing: VISUALCOMMIT_LIVE_GITHUB_REPO.")]
    [InlineData(Token, "octo", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"octo\", not owner/name.")]
    [InlineData(Token, "octo/", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"octo/\", not owner/name.")]
    [InlineData(Token, "/sandbox", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"/sandbox\", not owner/name.")]
    [InlineData(Token, "octo/sandbox/x", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"octo/sandbox/x\", not owner/name.")]
    [InlineData(Token, "oc to/sandbox", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"oc to/sandbox\", not owner/name.")]
    [InlineData(Token, "x/..", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"x/..\", not owner/name.")]
    [InlineData(Token, "../y", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"../y\", not owner/name.")]
    [InlineData(Token, "./.", "VISUALCOMMIT_LIVE_GITHUB_REPO is \"./.\", not owner/name.")]
    [InlineData(null, "octo", "Missing: VISUALCOMMIT_LIVE_GITHUB_TOKEN. VISUALCOMMIT_LIVE_GITHUB_REPO is \"octo\", not owner/name.")]
    public void Without_both_variables_every_test_fails_with_one_message(string? token, string? repository, string detail)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => LiveSettings.Resolve(token, repository));
        Assert.Equal(Heading + detail + Ending, refused.Message);
    }

    [Theory]
    [InlineData(Token)]
    [InlineData("ghp_madeUpClassic-0123456789")]
    [InlineData("GITHUB_PAT_shouted-made-up")]
    public void A_token_in_the_repository_variable_is_never_shown(string value)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => LiveSettings.Resolve(Token, value));
        Assert.Equal(Heading + "VISUALCOMMIT_LIVE_GITHUB_REPO holds what looks like a token, not owner/name." + Ending, refused.Message);
        Assert.False(refused.Message.Contains(value, StringComparison.Ordinal), "The message shows the variable's value.");
    }

    [Fact]
    public void The_sandbox_shows_its_name_and_never_the_token()
    {
        var sandbox = LiveSettings.Resolve(" " + Token + " ", " octo-cat/visualcommit.sandbox_1 ");
        Assert.Equal(("octo-cat", "visualcommit.sandbox_1", "octo-cat/visualcommit.sandbox_1"), (sandbox.Owner, sandbox.Name, sandbox.FullName));
        Assert.Equal(".dotfiles", LiveSettings.Resolve(Token, "octo/.dotfiles").Name);
        Assert.True(string.Equals(sandbox.Token, Token, StringComparison.Ordinal), "The token was not trimmed as read.");
        Assert.Equal(sandbox.FullName, sandbox.ToString());
        Assert.Equal("fine-grained (github_pat_)", LiveSettings.KindOf(Token));
        Assert.Equal("classic (ghp_)", LiveSettings.KindOf("ghp_madeUp"));
        Assert.Equal("of another kind (neither github_pat_ nor ghp_)", LiveSettings.KindOf(EscapingToken));
    }

    // ---- The token's forms (14.5) --------------------------------------------------------------

    /// <summary>Texts that hold <see cref="EscapingToken"/> in each form the search knows, by index.</summary>
    private static string Holding(int form) => form switch
    {
        0 => $"GitHub said: {EscapingToken}.",
        1 => $"https://example.invalid/?token={Uri.EscapeDataString(EscapingToken)}",
        2 => "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + EscapingToken)),
        3 => "kept as " + Convert.ToBase64String(Encoding.UTF8.GetBytes(EscapingToken)),
        _ => $"a prefix cut wrongly: {EscapingToken[3..(3 + LiveTokenForms.PartLength)]}…",
    };

    [Theory]
    [InlineData(0, "as written")]
    [InlineData(1, "URL-escaped")]
    [InlineData(2, "as the base64 of x-access-token:<token>")]
    [InlineData(3, "as base64")]
    [InlineData(4, "16 of its characters in a row")]
    public void Each_form_of_the_token_is_found(int form, string name)
    {
        Assert.NotEqual(EscapingToken, Uri.EscapeDataString(EscapingToken));
        Assert.Equal(name, LiveTokenForms.FoundIn(Holding(form), EscapingToken));
        Assert.False(name.Contains(EscapingToken, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("The token is fine-grained (github_pat_).")]
    [InlineData("GET /repos/octo/sandbox: 200 (request C0DE:1234:5678)")]
    [InlineData("github_pat_LIVE")]
    [InlineData("")]
    public void A_text_without_the_token_passes(string text) =>
        Assert.Null(LiveTokenForms.FoundIn(text, Token));

    [Fact]
    public void Without_a_token_nothing_is_searched_for() =>
        Assert.Null(LiveTokenForms.FoundIn(Holding(0), null));

    // ---- LiveRun (14.5, D323) ------------------------------------------------------------------

    [Fact]
    public void A_clean_note_is_kept_and_written_to_the_output_and_run_txt()
    {
        using var folder = new TempDirectory("live-safety");
        var file = folder.Combine("github", "run.txt");
        var output = new List<string>();
        var run = new LiveRun(Token, file, output.Add);
        run.Note("The sandbox octo/sandbox; the token is fine-grained (github_pat_).");
        run.Note("The duplicate create: 422 (request X).\n{\"message\":\"Validation Failed\"}");
        run.Dispose();

        Assert.Equal(run.Notes, output);
        Assert.Empty(run.Failures);
        var text = File.ReadAllText(file);
        Assert.StartsWith("VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5), written ", text, StringComparison.Ordinal);
        Assert.EndsWith(
            "\n\nThe sandbox octo/sandbox; the token is fine-grained (github_pat_).\nThe duplicate create: 422 (request X).\n{\"message\":\"Validation Failed\"}\n",
            text,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_note_that_holds_the_token_is_withheld_before_the_output_and_fails_the_run(int form)
    {
        using var folder = new TempDirectory("live-safety");
        var file = folder.Combine("run.txt");
        var output = new List<string>();
        var run = new LiveRun(EscapingToken, file, output.Add);
        run.Note("Before.");
        run.Note(Holding(form));
        run.Note("After.");

        Assert.Equal(["Before.", LiveRun.WithheldNote, "After."], output);
        Assert.Equal(output, run.Notes);
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.StartsWith("The live command's record failed: A note held the token (", failed.Message, StringComparison.Ordinal);
        Assert.Null(LiveTokenForms.FoundIn(failed.Message, EscapingToken));

        // The file is written with the note's stand-in, and holds no form of the token.
        var text = File.ReadAllText(file);
        Assert.Contains("\nBefore.\n" + LiveRun.WithheldNote + "\nAfter.\n", text, StringComparison.Ordinal);
        Assert.Null(LiveTokenForms.FoundIn(text, EscapingToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_run_txt_that_holds_the_token_is_not_written_and_fails_the_run(int form)
    {
        using var folder = new TempDirectory("live-safety");
        var file = folder.Combine("run.txt");
        File.WriteAllText(file, "An earlier run's record.");
        var run = new LiveRun(EscapingToken, file, _ => { });

        run.WriteRunFile("VisualCommit's live command\n" + Holding(form) + "\n");

        Assert.False(File.Exists(file), "run.txt holds the token, or an earlier run's file is left to be read as this run's.");
        var failure = Assert.Single(run.Failures);
        Assert.StartsWith("run.txt was withheld: its text held the token (", failure, StringComparison.Ordinal);
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.Null(LiveTokenForms.FoundIn(failed.Message, EscapingToken));
    }

    [Fact]
    public void A_run_without_notes_writes_that_it_took_none_in_place_of_an_earlier_file_and_passes()
    {
        using var folder = new TempDirectory("live-safety");
        var file = folder.Combine("run.txt");
        File.WriteAllText(file, "An earlier run's record.");
        new LiveRun(null, file, _ => { }).Dispose();
        var text = File.ReadAllText(file);
        Assert.StartsWith("VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5), written ", text, StringComparison.Ordinal);
        Assert.EndsWith(" UTC.\n\n" + LiveRun.NoNotes + "\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_is_screened_for_the_token_and_a_find_fails_the_run()
    {
        using var folder = new TempDirectory("live-safety");
        var run = new LiveRun(Token, folder.Combine("run.txt"), _ => { });
        Assert.Equal("The sign-in failed: Bad credentials", run.Screen("The sign-in failed: Bad credentials"));
        Assert.Empty(run.Failures);
        Assert.Equal(LiveRun.WithheldText, run.Screen($"The sign-in failed: {Token} is not valid"));
        Assert.Equal("A message held the token (as written); it was withheld.", Assert.Single(run.Failures));
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.Null(LiveTokenForms.FoundIn(failed.Message, Token));
    }

    [Fact]
    public void A_failed_step_s_exception_that_holds_the_token_is_withheld_whole()
    {
        using var folder = new TempDirectory("live-safety");
        var run = new LiveRun(EscapingToken, folder.Combine("run.txt"), _ => { });

        // An assertion that shows a git value, as Assert.Equal on remote.origin.url would, and an
        // exception that holds the token only in an inner one (Exception.ToString shows it).
        var assertion = Record.Exception(() => Assert.Equal("https://github.com/octo/sandbox.git", $"https://x-access-token:{EscapingToken}@github.com/octo/sandbox.git"));
        var wrapped = new InvalidOperationException("The clone failed.", new HttpRequestException("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + EscapingToken))));
        foreach (var original in new[] { assertion!, wrapped })
        {
            var thrown = Assert.ThrowsAny<Exception>(() => run.FailureOf(original, "4, the clone").Throw());
            Assert.NotSame(original, thrown);
            Assert.IsType<XunitException>(thrown);
            Assert.Null(thrown.InnerException);
            Assert.Equal($"Step 4, the clone failed with {original.GetType().Name}, whose text held the token, so it is withheld (D323).", thrown.Message);
            Assert.Null(LiveTokenForms.FoundIn(thrown.ToString(), EscapingToken));
        }

        Assert.Equal(2, run.Failures.Count);
        Assert.All(run.Failures, failure => Assert.StartsWith("The failure of step 4, the clone held the token (", failure, StringComparison.Ordinal));

        // A clean exception comes out as itself, and fails nothing more.
        var clean = new XunitException("The browser does not list octo/sandbox.");
        Assert.Same(clean, Assert.ThrowsAny<Exception>(() => run.FailureOf(clean, "3, the browser").Throw()));
        Assert.Equal(2, run.Failures.Count);
    }

    // ---- LiveRequestLog (14.5) -----------------------------------------------------------------

    [Fact]
    public async Task The_request_log_keeps_four_names_of_the_answer_and_no_value_of_a_header_or_a_body()
    {
        const string Cookie = "session=made-up-cookie-1234";
        const string Echo = "made-up-echo-5678";
        const string RequestBody = "made-up-request-body-9012";
        const string AnswerBody = "{\"made-up-answer\":\"3456\"}";
        const string Scopes = "repo, admin:org";
        const string Email = "author@example.invalid";
        var inner = new StubHandler(_ =>
        {
            var answer = new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(AnswerBody, Encoding.UTF8, "application/json") };
            answer.Headers.Add("X-GitHub-Request-Id", "C0DE:1234:5678");
            answer.Headers.Add("X-GitHub-Api-Version-Selected", "2026-03-10");
            answer.Headers.Add("X-OAuth-Scopes", Scopes);
            answer.Headers.Add("GitHub-Authentication-Token-Expiration", "2026-12-31 23:59:59 UTC");
            answer.Headers.Add("Set-Cookie", Cookie);
            answer.Headers.Add("X-Echo", Echo);
            return answer;
        });
        var store = new LiveRequestLog.Store();
        using var client = new HttpClient(new LiveRequestLog(inner, store));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/octo/sandbox/commits?author={Uri.EscapeDataString(Email)}")
        {
            Content = new StringContent(RequestBody),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        request.Headers.Add("X-Echo", Echo);

        using var response = await client.SendAsync(request, Cancelled);

        // The caller gets GitHub's answer unchanged.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(AnswerBody, await response.Content.ReadAsStringAsync(Cancelled));
        Assert.Equal([Echo], response.Headers.GetValues("X-Echo"));

        var record = Assert.Single(store.Snapshot());
        Assert.Equal(
            ("POST", "https", "api.github.com", "/repos/octo/sandbox/commits", (int?)201, true),
            (record.Method, record.Scheme, record.Host, record.Path, record.Status, record.AuthorizationSent));
        Assert.Equal(("C0DE:1234:5678", "2026-03-10", true, "2026-12-31", (string?)null), (record.RequestId, record.ApiVersionSelected, record.ScopesHeaderPresent, record.TokenExpiration, record.FailedWith));
        Assert.True(DateTimeOffset.UtcNow - record.Time < TimeSpan.FromMinutes(1), "The record's time is not the request's.");

        // No value but those four reaches the record, its text or its line.
        var texts = typeof(LiveRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => (string?)property.GetValue(record))
            .Append(record.ToString())
            .Append(record.Describe())
            .Concat(LiveRequestLog.PerHost([record]))
            .OfType<string>()
            .ToList();
        foreach (var secret in new[] { Token, Cookie, Echo, RequestBody, AnswerBody, "made-up-answer", Scopes, Email, Uri.EscapeDataString(Email), "author=" })
        {
            Assert.DoesNotContain(texts, text => text.Contains(secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_request_without_an_answer_is_recorded_by_its_failure_and_the_failure_goes_on()
    {
        var store = new LiveRequestLog.Store();
        using var client = new HttpClient(new LiveRequestLog(new StubHandler(_ => throw new HttpRequestException("made-up: " + Token)), store));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://avatars.githubusercontent.com/u/1001?s=64&v=4");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, Cancelled));

        var record = Assert.Single(store.Snapshot());
        Assert.Equal(("GET", "avatars.githubusercontent.com", "/u/1001", (int?)null, false, "HttpRequestException"), (record.Method, record.Host, record.Path, record.Status, record.AuthorizationSent, record.FailedWith));
        Assert.Equal("GET https://avatars.githubusercontent.com/u/1001 failed: HttpRequestException without Authorization (request none)", record.Describe());
        Assert.Null(LiveTokenForms.FoundIn(record.ToString(), Token));
    }

    [Fact]
    public async Task One_store_takes_the_records_of_many_handlers_at_once()
    {
        var store = new LiveRequestLog.Store();
        var clients = Enumerable.Range(0, 4).Select(_ => new HttpClient(new LiveRequestLog(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), store))).ToList();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 200).Select(index => Task.Run(
                async () => (await clients[index % clients.Count].GetAsync(new Uri($"https://api.github.com/n/{index}"), Cancelled)).Dispose(),
                Cancelled)));
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }

        Assert.Equal(200, store.Count);
        Assert.Equal(Enumerable.Range(0, 200).Select(index => $"/n/{index}").Order(StringComparer.Ordinal), store.Snapshot().Select(record => record.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_reach_rules_name_every_request_that_breaks_them()
    {
        static LiveRequest Request(string scheme, string host, bool authorization) =>
            new(DateTimeOffset.UtcNow, "GET", scheme, host, "/x", 200, authorization, null, null, false, null, null);

        LiveRequest[] requests =
        [
            Request("https", "api.github.com", authorization: true),
            Request("https", "avatars.githubusercontent.com", authorization: false),
            Request("http", "api.github.com", authorization: true),
            Request("https", "avatars.githubusercontent.com", authorization: true),
            Request("https", "github.com", authorization: true),
        ];

        Assert.Equal(
            [
                "A request was not https: GET http://api.github.com/x 200 with Authorization (request none).",
                "Authorization went to another host than api.github.com: GET https://avatars.githubusercontent.com/x 200 with Authorization (request none).",
                "Authorization went to another host than api.github.com: GET https://github.com/x 200 with Authorization (request none).",
            ],
            LiveRequestLog.ReachProblems(requests));
        Assert.Empty(LiveRequestLog.ReachProblems(requests.Take(2)));
        Assert.Equal(
            [
                "The app's requests to api.github.com: 2, 2 with Authorization and 0 without; answered 200 ×2; API version selected not sent ×2; X-OAuth-Scopes absent ×2; the token expires not sent ×2.",
                "The app's requests to avatars.githubusercontent.com: 2, 1 with Authorization and 1 without; answered 200 ×2; API version selected not sent ×2; X-OAuth-Scopes absent ×2; the token expires not sent ×2.",
                "The app's requests to github.com: 1, 1 with Authorization and 0 without; answered 200 ×1; API version selected not sent ×1; X-OAuth-Scopes absent ×1; the token expires not sent ×1.",
            ],
            LiveRequestLog.PerHost(requests));
    }

    [Fact]
    public void The_answers_headers_are_shown_per_request_and_per_host()
    {
        static LiveRequest Request(int? status, string? version, bool scopes, string? expiry, string? failed = null) =>
            new(DateTimeOffset.UtcNow, "GET", "https", "api.github.com", "/user", status, true, "C0DE", version, scopes, expiry, failed);

        Assert.Equal("API version selected 2026-03-10, X-OAuth-Scopes absent, the token expires 2026-12-31", Request(200, "2026-03-10", false, "2026-12-31").Headers());
        Assert.Equal("API version selected not sent, X-OAuth-Scopes present, the token expires not sent", Request(200, null, true, null).Headers());
        Assert.Equal(
            "The app's requests to api.github.com: 4, 4 with Authorization and 0 without; answered 200 ×2, 404 ×1, failed: HttpRequestException ×1; "
                + "API version selected 2026-03-10 ×2, not sent ×1; X-OAuth-Scopes absent ×3; the token expires 2026-12-31 ×3.",
            Assert.Single(LiveRequestLog.PerHost(
            [
                Request(200, "2026-03-10", false, "2026-12-31"),
                Request(404, null, false, "2026-12-31"),
                Request(null, null, false, null, "HttpRequestException"),
                Request(200, "2026-03-10", false, "2026-12-31"),
            ])));
    }

    [Theory]
    [InlineData("http://api.github.com/user", true, "not https")]
    [InlineData("http://avatars.githubusercontent.com/u/1001", false, "not https")]
    [InlineData("https://avatars.githubusercontent.com/u/1001", true, "Authorization for another host than api.github.com")]
    [InlineData("https://github.com/octo/sandbox", true, "Authorization for another host than api.github.com")]
    [InlineData("https://api.github.com:8443/user", true, "Authorization for another host than api.github.com")]
    [InlineData("https://api.github.com.example.invalid/user", true, "Authorization for another host than api.github.com")]
    public async Task A_request_that_would_break_the_reach_never_leaves(string url, bool authorization, string reason)
    {
        var inner = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var store = new LiveRequestLog.Store();
        using var client = new HttpClient(new LiveRequestLog(inner, store));
        using var request = new HttpRequestMessage(HttpMethod.Get, url + "?s=64&email=author%40example.invalid");
        if (authorization)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        }

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, Cancelled));

        Assert.Empty(inner.Requests);
        var uri = new Uri(url);
        Assert.Equal($"The live command refused GET {uri.Scheme}://{uri.Authority}{uri.AbsolutePath}: {reason} (D327).", refused.Message);
        Assert.DoesNotContain("author", refused.Message, StringComparison.Ordinal);
        var record = Assert.Single(store.Snapshot());
        Assert.Equal(((int?)null, authorization, "refused: " + reason), (record.Status, record.AuthorizationSent, record.FailedWith));
        Assert.NotEmpty(LiveRequestLog.ReachProblems([record]));
    }

    [Theory]
    [InlineData("https://api.github.com/user", true)]
    [InlineData("https://API.GitHub.com/user", true)]
    [InlineData("https://avatars.githubusercontent.com/u/1001?s=64&v=4", false)]
    public async Task A_request_within_the_reach_goes_on_unchanged(string url, bool authorization)
    {
        var inner = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var store = new LiveRequestLog.Store();
        using var client = new HttpClient(new LiveRequestLog(inner, store));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (authorization)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        }

        using var response = await client.SendAsync(request, Cancelled);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(inner.Requests);
        Assert.Empty(LiveRequestLog.ReachProblems(store.Snapshot()));
    }

    [Theory]
    [InlineData("2026-12-31 23:59:59 UTC", "2026-12-31")]
    [InlineData("2026-12-31 23:59:59 -0800", "2026-12-31")]
    [InlineData("soon", "present, not a date")]
    [InlineData(null, null)]
    public void The_token_s_expiry_is_kept_as_its_date(string? header, string? kept) =>
        Assert.Equal(kept, LiveRequestLog.ExpirationDate(header));

    // ---- GitHubLiveApi (14.5) ------------------------------------------------------------------

    [Fact]
    public async Task The_test_s_own_calls_send_the_app_s_headers_and_fail_with_status_message_and_request_id()
    {
        var handler = new StubHandler(_ =>
        {
            var answer = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"Not Found\",\"status\":\"404\"}") };
            answer.Headers.Add("X-GitHub-Request-Id", "C0DE:0001");
            answer.Headers.Add("X-RateLimit-Remaining", "4999");
            return answer;
        });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        var failed = await Assert.ThrowsAsync<GitHubLiveException>(() => api.OpenPullRequestsAsync(Cancelled));

        Assert.Equal("GitHub answered 404 Not Found for the open pull requests (request C0DE:0001).", failed.Message);
        Assert.Equal("4999", api.RateLimitRemaining);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "https://api.github.com/repos/octo/sandbox/pulls?state=open&per_page=100"), (sent.Method, sent.Url));
        Assert.True(string.Equals(sent.Authorization, "Bearer " + Token, StringComparison.Ordinal), "The call did not send the token as a Bearer token.");
        Assert.Equal(("application/vnd.github+json", "2026-03-10", GitHubLiveApi.UserAgent), (sent.Accept, sent.Version, sent.UserAgent));
    }

    [Fact]
    public async Task An_answer_that_holds_the_token_never_reaches_a_message()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent($"{{\"message\":\"Bad credentials for {Token}\"}}") });
        var withheld = new List<string>();
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler, withheld.Add);

        var failed = await Assert.ThrowsAsync<GitHubLiveException>(() => api.BranchesUnderAsync("visualcommit-live/", Cancelled));
        Assert.Equal("GitHub's answer was withheld: it held the token.", failed.Message);
        Assert.Null(LiveTokenForms.FoundIn(failed.Message, Token));
        Assert.Equal("https://api.github.com/repos/octo/sandbox/git/matching-refs/heads/visualcommit-live/", Assert.Single(handler.Requests).Url);

        // The find is told to the run, which then fails at its end.
        Assert.Equal("GitHub's answer held the token (as written); it was withheld.", Assert.Single(withheld));

        var answer = await api.ReadRepositoryAsync(Cancelled);
        Assert.Equal("401 (request none)", answer.ToString());
    }

    [Theory]
    [InlineData(401, "{\"message\":\"Bad credentials\"}", "GitHub's body as sent:\n{\"message\":\"Bad credentials\"}")]
    [InlineData(422, "{\"message\":\"Validation Failed\"}", "GitHub's body as sent:\n{\"message\":\"Validation Failed\"}")]
    [InlineData(200, "[{\"full_name\":\"someone/private-thing\"},{\"full_name\":\"octo/sandbox\"}]", "GitHub's body, a 2xx's, by its shape only: an array of 2.")]
    [InlineData(201, "{\"full_name\":\"octo/sandbox\",\"description\":\"x\"}", "GitHub's body, a 2xx's, by its shape only: an object.")]
    [InlineData(200, "<html>made-up</html>", "GitHub's body, a 2xx's, by its shape only: no JSON, 20 characters.")]
    [InlineData(500, "", "GitHub's body: (empty).")]
    [InlineData(204, "", "GitHub's body: (empty).")]
    public async Task A_body_is_kept_whole_only_for_a_refusal(int status, string body, string kept)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        var answer = await api.ProbeTypeWithVisibilityAsync(Cancelled);

        Assert.Equal(kept, answer.BodyForNotes);
    }

    [Fact]
    public async Task The_clean_up_may_remove_only_the_sandbox_s_own_live_branches_and_their_pull_requests()
    {
        var handler = new StubHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.Contains("/git/matching-refs/", StringComparison.Ordinal)
                ? """
                  [{"ref":"refs/heads/main"},{"ref":"refs/heads/visualcommit-livex"},{"ref":"refs/heads/visualcommit-live"},
                   {"ref":"refs/heads/visualcommit-live/20261030-120000Z"},{"ref":"refs/heads/visualcommit-live/x/y"},
                   {"ref":"refs/heads/visualcommit-live/"},{"ref":"refs/tags/visualcommit-live/v1"}]
                  """
                : """
                  [{"number":1,"head":{"ref":"visualcommit-live/20261030-120000Z","repo":{"full_name":"Octo/Sandbox"}}},
                   {"number":2,"head":{"ref":"visualcommit-live/20261030-120000Z","repo":{"full_name":"someone/sandbox"}}},
                   {"number":3,"head":{"ref":"visualcommit-live/20261030-120000Z","repo":null}},
                   {"number":4,"head":{"ref":"visualcommit-livex","repo":{"full_name":"octo/sandbox"}}},
                   {"number":5,"head":{"ref":"feature/visualcommit-live/x","repo":{"full_name":"octo/sandbox"}}},
                   {"number":6,"head":{"ref":"visualcommit-live/x/y","repo":{"full_name":"octo/sandbox"}}}]
                  """),
        });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        var branches = await api.BranchesUnderAsync(LiveLeftovers.Prefix, Cancelled);
        var open = await api.OpenPullRequestsAsync(Cancelled);

        Assert.Equal(["visualcommit-live/20261030-120000Z", "visualcommit-live/x/y"], branches.Under);
        Assert.Equal(
            ["refs/heads/main", "refs/heads/visualcommit-livex", "refs/heads/visualcommit-live", "refs/heads/visualcommit-live/", "refs/tags/visualcommit-live/v1"],
            branches.Others);
        Assert.Equal([1, 6], open.Where(pull => LiveLeftovers.IsLeftover(pull, "octo/sandbox")).Select(pull => pull.Number));
        Assert.Equal([1], open.Where(pull => LiveLeftovers.IsFrom(pull, "visualcommit-live/20261030-120000Z", "octo/sandbox")).Select(pull => pull.Number));
        Assert.DoesNotContain(open, pull => LiveLeftovers.IsFrom(pull, "visualcommit-live/20261030-120000", "octo/sandbox"));
    }

    [Theory]
    [InlineData("""[{"sha":"a","author":{"login":"octo"}}]""", "octo")]
    [InlineData("""[{"sha":"a","author":null}]""", null)]
    [InlineData("""[{"sha":"a"}]""", null)]
    [InlineData("[]", null)]
    public async Task The_latest_commit_s_linked_author_is_read_by_its_login(string body, string? login)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        Assert.Equal(login, await api.LatestCommitAuthorAsync("main", Cancelled));
        Assert.Equal("https://api.github.com/repos/octo/sandbox/commits?sha=main&per_page=1", Assert.Single(handler.Requests).Url);
    }

    [Theory]
    [InlineData("""[{"sha":"a","commit":{"author":{"email":"x@example.invalid"}}}]""", "a", "x@example.invalid")]
    [InlineData("""[{"sha":"a","commit":{"author":{}}}]""", null, null)]
    [InlineData("""[{"sha":"","commit":{"author":{"email":"x@example.invalid"}}}]""", null, null)]
    [InlineData("""[{"sha":"a","author":{"login":"octo"}}]""", null, null)]
    [InlineData("[]", null, null)]
    public async Task The_latest_commit_s_sha_and_author_s_address_are_read_for_the_avatars_lookup(string body, string? sha, string? email)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        var latest = await api.LatestCommitAsync("main", Cancelled);

        Assert.Equal((sha, email), (latest?.Sha, latest?.Email));
        Assert.Equal("https://api.github.com/repos/octo/sandbox/commits?sha=main&per_page=1", Assert.Single(handler.Requests).Url);
    }

    [Fact]
    public async Task The_avatars_lookup_is_one_get_of_the_sandbox_s_commits_by_sha_and_escaped_address()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);

        Assert.Equal(200, (await api.ProbeLookupAsync("a", "x@example.invalid", Cancelled)).Status);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "https://api.github.com/repos/octo/sandbox/commits?sha=a&author=x%40example.invalid&per_page=1"), (request.Method, request.Url));
    }

    [Fact]
    public void A_pull_request_is_read_from_GitHub_s_object()
    {
        using var document = JsonDocument.Parse("""{"number":7,"head":{"ref":"visualcommit-live/a","repo":{"full_name":"octo/sandbox"}}}""");
        Assert.Equal(new LivePullRequest(7, "visualcommit-live/a", "octo/sandbox"), LivePullRequest.From(document.RootElement));
    }

    // ---- The probes beside the fake, and the premises (D342) -----------------------------------

    /// <summary>GitHub's answer to an API version it does not have, as the live run of 2026-10-30 recorded it.</summary>
    private const string VersionRefused = """{"message":"Bad Request","errors":"The version you specified in the \"X-GitHub-API-Version\" request header, \"2000-01-01\", is not a supported version. The following versions are currently supported: \"2026-03-10\" (most recent) and \"2022-11-28\".","documentation_url":"https://docs.github.com/rest","status":"400"}""";

    /// <summary>GitHub's answer to the duplicate create, as that run recorded it, with a made-up owner.</summary>
    private const string DuplicateRefused = """{"message":"Validation Failed","errors":[{"resource":"PullRequest","code":"custom","message":"A pull request already exists for octo:visualcommit-live/20261030-232555Z."}],"documentation_url":"https://docs.github.com/rest/pulls/pulls#create-a-pull-request","status":"422"}""";

    [Fact]
    public async Task A_probe_s_note_shows_its_content_type_selected_version_and_rate_limit()
    {
        var empty = await AnswerAsync(500, string.Empty, answer => answer.Headers.Add("X-RateLimit-Remaining", "4990"));
        Assert.Equal(("Content-Type not sent, API version selected not sent, X-RateLimit-Remaining 4990", "(empty)"), (empty.HeadersForNotes, empty.Shape));

        var json = await AnswerAsync(404, """{"message":"Not Found"}""", answer => answer.Headers.Add("X-GitHub-Api-Version-Selected", "2026-03-10"));
        Assert.Equal(("Content-Type application/json; charset=utf-8, API version selected 2026-03-10, X-RateLimit-Remaining not sent", "an object"), (json.HeadersForNotes, json.Shape));
    }

    [Fact]
    public async Task The_fake_s_answer_is_same_or_its_status_message_errors_and_shape_where_they_differ()
    {
        // GitHub's own words from the fake are "same", whatever its documentation_url and status field.
        Assert.Equal("same", LiveAnswer.FakeBeside(await AnswerAsync(400, VersionRefused), await AnswerAsync(400, VersionRefused.Replace("\"documentation_url\":\"https://docs.github.com/rest\",\"status\":\"400\"", "\"documentation_url\":\"https://example.invalid/\"", StringComparison.Ordinal))));
        Assert.Equal("same", LiveAnswer.FakeBeside(await AnswerAsync(500, string.Empty), await AnswerAsync(500, string.Empty)));

        // errors compared as JSON, whatever escaping each chose.
        Assert.Equal("same", LiveAnswer.FakeBeside(await AnswerAsync(422, """{"message":"Validation Failed","errors":[{"message":"can't be blank"}]}"""), await AnswerAsync(422, """{"message":"Validation Failed","errors":[{"message":"can't be blank"}]}""")));

        // The fake's values of what differs, and only those.
        Assert.Equal(
            "message \"Unsupported 'X-GitHub-Api-Version' header: 2000-01-01.\", no errors",
            LiveAnswer.FakeBeside(await AnswerAsync(400, VersionRefused), await AnswerAsync(400, """{"message":"Unsupported 'X-GitHub-Api-Version' header: 2000-01-01.","documentation_url":"https://docs.github.com/rest","status":"400"}""")));
        Assert.Equal(
            "status 422, message \"No commit found for SHA: ffff\"",
            LiveAnswer.FakeBeside(await AnswerAsync(404, """{"message":"Not Found","status":"404"}"""), await AnswerAsync(422, """{"message":"No commit found for SHA: ffff","status":"422"}""")));
        Assert.Equal(
            "status 422, message \"No commit found for SHA: ffff\", body an object",
            LiveAnswer.FakeBeside(await AnswerAsync(500, string.Empty), await AnswerAsync(422, """{"message":"No commit found for SHA: ffff"}""")));
        Assert.Equal(
            "no message, no errors, body (empty)",
            LiveAnswer.FakeBeside(await AnswerAsync(500, """{"message":"Server Error","errors":"x"}"""), await AnswerAsync(500, string.Empty)));
        Assert.Equal("body an array of 0", LiveAnswer.FakeBeside(await AnswerAsync(200, "[{\"sha\":\"a\"}]"), await AnswerAsync(200, "[]")));

        // Without GitHub's answer: the fake's status, message and errors.
        Assert.Equal("status 409, message \"Git Repository is empty.\"", LiveAnswer.FakeBeside(null, await AnswerAsync(409, """{"message":"Git Repository is empty.","documentation_url":"https://docs.github.com/rest","status":"409"}""")));
        Assert.Equal("status 422, message \"Validation Failed\", errors [{\"code\":\"invalid\"}]", LiveAnswer.FakeBeside(null, await AnswerAsync(422, """{"message":"Validation Failed","errors":[{"code":"invalid"}]}""")));
    }

    [Fact]
    public async Task Each_side_s_address_is_hidden_before_the_comparison_and_never_shown()
    {
        const string Address = "author@example.invalid";
        const string FakeAddress = "sandbox-author@visualcommit.invalid";
        var github = await AnswerAsync(422, """{"message":"No commit by Author@Example.invalid","errors":["author%40example.invalid"]}""");
        Assert.Equal("same", LiveAnswer.FakeBeside(github, await AnswerAsync(422, """{"message":"No commit by sandbox-author@visualcommit.invalid","errors":["sandbox-author%40visualcommit.invalid"]}"""), Address, FakeAddress));

        var differs = LiveAnswer.FakeBeside(github, await AnswerAsync(404, """{"message":"Nothing for sandbox-author@visualcommit.invalid"}"""), Address, FakeAddress);
        Assert.Equal("status 404, message \"Nothing for <the address>\", no errors", differs);
        Assert.Equal("a <the address> b <the address> c", LiveAnswer.Hide("a AUTHOR@example.invalid b author%40example.invalid c", Address));
        Assert.Equal("as it is", LiveAnswer.Hide("as it is", null));
    }

    [Fact]
    public async Task The_version_premise_takes_a_400_whose_message_or_errors_string_names_the_header()
    {
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, VersionRefused)));
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Unsupported 'X-GitHub-Api-Version' header: 2000-01-01."}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request"}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request","errors":["X-GitHub-Api-Version"]}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, string.Empty)));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(410, VersionRefused)));
    }

    [Fact]
    public async Task The_duplicate_create_premise_takes_a_422_whose_first_error_says_one_exists_already()
    {
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, DuplicateRefused)));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, DuplicateRefused.Replace("A pull request", "a pull request", StringComparison.Ordinal))));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":"A pull request already exists for octo:x."}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":[]}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":[{"resource":"PullRequest","code":"custom","message":"No commits between main and x"}]}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(201, """{"number":2}""")));
    }

    [Theory]
    [InlineData(200, "[{\"sha\":\"a\"}]", 1, true)]
    [InlineData(200, "[]", 0, true)]
    [InlineData(200, "[]", 1, false)]
    [InlineData(200, "{}", 0, false)]
    [InlineData(404, "[]", 0, false)]
    public async Task The_lookups_premise_takes_a_200_with_an_array_of_its_length(int status, string body, int count, bool holds) =>
        Assert.Equal(holds, LivePremises.IsArrayOf(await AnswerAsync(status, body), count));

    [Theory]
    [InlineData(404, true)]
    [InlineData(422, true)]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(599, true)]
    [InlineData(200, false)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(409, false)]
    [InlineData(410, false)]
    public async Task An_unknown_sha_s_lookup_premise_takes_the_statuses_after_which_the_provider_goes_on(int status, bool holds) =>
        Assert.Equal(holds, LivePremises.LeavesOutOnlyItsAddress(await AnswerAsync(status, string.Empty)));

    [Fact]
    public void An_unknown_sha_is_forty_lower_case_hex_digits_not_all_f_and_the_same_for_one_stamp()
    {
        var sha = LivePremises.UnknownSha("20261030-232555Z");
        Assert.Matches("^[0-9a-f]{40}$", sha);
        Assert.NotEqual(new string('f', 40), sha);
        Assert.Equal(sha, LivePremises.UnknownSha("20261030-232555Z"));
        Assert.NotEqual(sha, LivePremises.UnknownSha("20261030-232556Z"));
    }

    [Fact]
    public async Task The_version_rewriter_replaces_the_version_alone_and_never_touches_authorization()
    {
        var inner = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var store = new LiveRequestLog.Store();
        using var client = new HttpClient(new LiveRequestLog(new LiveVersionRewriter("2000-01-01", inner), store));
        using var asked = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user?per_page=1");
        asked.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        asked.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        asked.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        asked.Headers.TryAddWithoutValidation("User-Agent", "VisualCommit/0.1.0");
        using var bare = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");

        (await client.SendAsync(asked, Cancelled)).Dispose();
        (await client.SendAsync(bare, Cancelled)).Dispose();

        Assert.Equal(2, inner.Requests.Count);
        var sent = inner.Requests[0];
        Assert.Equal(("https://api.github.com/user?per_page=1", "application/vnd.github+json", "2000-01-01", "VisualCommit/0.1.0"), (sent.Url, sent.Accept, sent.Version, sent.UserAgent));
        Assert.True(string.Equals(sent.Authorization, "Bearer " + Token, StringComparison.Ordinal), "The rewriter changed Authorization.");
        Assert.Equal(("2000-01-01", (string?)null), (inner.Requests[1].Version, inner.Requests[1].Authorization));

        // Behind the log, which saw Authorization go to api.github.com alone.
        Assert.Equal([true, false], store.Snapshot().Select(record => record.AuthorizationSent));
        Assert.Empty(LiveRequestLog.ReachProblems(store.Snapshot()));
    }

    [Fact]
    public void The_provider_s_log_is_searched_for_the_token_in_its_lines_and_their_exceptions()
    {
        var log = new LiveLog();
        log.Write(VisualCommit.Core.Logging.LogLevel.Warning, "GitHub: GET /repos/octo/sandbox/commits failed: 500 (request C0DE).");
        Assert.Equal((1, (string?)null), (log.Count, log.FoundIn(EscapingToken)));

        log.Write(VisualCommit.Core.Logging.LogLevel.Error, "GitHub: GET /user failed.", new HttpRequestException("made-up: " + Uri.EscapeDataString(EscapingToken)));
        Assert.Equal((2, "URL-escaped"), (log.Count, log.FoundIn(EscapingToken)));
    }

    [Fact]
    public async Task The_fake_in_process_answers_the_probes_through_its_own_handler_with_its_own_token()
    {
        await using var fake = await LiveFake.StartAsync("octo", "sandbox", Cancelled);

        // Its address, 127.0.0.1:1, has no server: an answer came from the handler in process.
        var version = await fake.Api.ProbeVersionAsync("2000-01-01", Cancelled);
        Assert.True(LivePremises.NamesVersionHeader(version), $"The fake answered {version}.");
        Assert.StartsWith("FAKE-", version.RequestId, StringComparison.Ordinal);
        Assert.Equal(422, (await fake.Api.ProbeTypeWithVisibilityAsync(Cancelled)).Status);
        Assert.True(LivePremises.IsArrayOf(await fake.Api.ProbeLookupAsync(fake.Sha, LiveFake.Email, Cancelled), 1));
        Assert.True(LivePremises.IsArrayOf(await fake.Api.ProbeLookupAsync(fake.Sha, "no-such-author@visualcommit.invalid", Cancelled), 0));
        Assert.True(LivePremises.LeavesOutOnlyItsAddress(await fake.Api.ProbeLookupAsync(new string('f', 40), LiveFake.Email, Cancelled)));
        Assert.Equal(409, (await fake.EmptyApi.ProbeCommitsAsync("main", Cancelled)).Status);
    }

    [Fact]
    public void The_order_names_every_live_test_once()
    {
        var methods = typeof(GitHubLiveTests).GetMethods()
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<Xunit.v3.IFactAttribute>().Any())
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(methods, GitHubLiveOrder.Methods.Order(StringComparer.Ordinal));
        Assert.Equal(GitHubLiveOrder.Methods.Count, GitHubLiveOrder.Methods.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>An answer with <paramref name="status"/> and <paramref name="body"/> (JSON, or none at all) as the test's own call reads it, its headers set by <paramref name="headers"/>.</summary>
    private static async Task<LiveAnswer> AnswerAsync(int status, string body, Action<HttpResponseMessage>? headers = null)
    {
        var handler = new StubHandler(_ =>
        {
            var answer = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = body.Length == 0 ? new ByteArrayContent([]) : new StringContent(body, Encoding.UTF8, "application/json"),
            };
            headers?.Invoke(answer);
            return answer;
        });
        using var api = new GitHubLiveApi(new LiveSandbox("octo", "sandbox", Token), handler);
        return await api.ProbeTypeWithVisibilityAsync(Cancelled);
    }

    /// <summary>An in-process handler: answers every request with <paramref name="answer"/>'s, and keeps what each request sent.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Authorization, string? Accept, string? Version, string? UserAgent)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add((
                    request.Method.Method,
                    request.RequestUri!.AbsoluteUri,
                    request.Headers.Authorization?.ToString(),
                    request.Headers.Accept.ToString(),
                    request.Headers.TryGetValues("X-GitHub-Api-Version", out var version) ? version.Single() : null,
                    request.Headers.TryGetValues("User-Agent", out var agent) ? string.Join(' ', agent) : null));
            }

            return Task.FromResult(answer(request));
        }
    }
}
