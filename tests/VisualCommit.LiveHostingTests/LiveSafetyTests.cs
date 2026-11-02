using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.AzureDevOps;
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
public sealed partial class LiveSafetyTests
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

        // errors compared as JSON, whatever escaping each chose: GitHub's own beside System.Text.Json's
        // default, which writes ", ', < and > as \u escapes, in one string and in an array's object.
        const string Explanation = "the \"X-GitHub-API-Version\" header, can't be <2000-01-01>";
        var escaped = JsonSerializer.Serialize(new { message = "Bad Request", errors = Explanation });
        var escapedArray = JsonSerializer.Serialize(new { message = "Validation Failed", errors = new[] { new { message = "can't be blank" } } });
        Assert.DoesNotContain("'", escaped + escapedArray, StringComparison.Ordinal);
        Assert.DoesNotContain("<", escaped, StringComparison.Ordinal);
        Assert.Equal(
            "same",
            LiveAnswer.FakeBeside(await AnswerAsync(400, """{"message":"Bad Request","errors":"the \"X-GitHub-API-Version\" header, can't be <2000-01-01>"}"""), await AnswerAsync(400, escaped)));
        Assert.Equal(
            "same",
            LiveAnswer.FakeBeside(await AnswerAsync(422, """{"message":"Validation Failed","errors":[{"message":"can't be blank"}]}"""), await AnswerAsync(422, escapedArray)));
        Assert.Equal("\"the \\\"X-GitHub-API-Version\\\" header, can't be <2000-01-01>\"", (await AnswerAsync(400, escaped)).ErrorsText);
        Assert.Equal("""[{"message":"can't be blank"}]""", (await AnswerAsync(422, escapedArray)).ErrorsText);

        // errors that differ in their value alone.
        Assert.Equal(
            "errors \"the version 2000-01-02 is not supported\"",
            LiveAnswer.FakeBeside(
                await AnswerAsync(400, """{"message":"Bad Request","errors":"the version 2000-01-01 is not supported"}"""),
                await AnswerAsync(400, """{"message":"Bad Request","errors":"the version 2000-01-02 is not supported"}""")));

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
    public async Task The_version_premise_takes_a_400_whose_first_error_or_message_names_the_header_as_the_client_reads_them()
    {
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, VersionRefused)));
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Unsupported 'X-GitHub-Api-Version' header: 2000-01-01."}""")));

        // errors in the client's other shapes (D342): an array's first string, or its first object's message.
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request","errors":["X-GitHub-Api-Version"]}""")));
        Assert.True(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request","errors":[7,{"code":"invalid","message":"The X-GitHub-API-Version header is not supported."}]}""")));

        // Only the first error counts, as in the client; and only a 400 with one of them.
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request","errors":["Bad value","X-GitHub-Api-Version"]}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request","errors":[{"code":"invalid","field":"X-GitHub-Api-Version"}]}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, """{"message":"Bad Request"}""")));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(400, string.Empty)));
        Assert.False(LivePremises.NamesVersionHeader(await AnswerAsync(410, VersionRefused)));
    }

    [Fact]
    public async Task The_duplicate_create_premise_takes_a_422_whose_detail_as_the_client_reads_it_says_one_exists_already()
    {
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, DuplicateRefused)));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, DuplicateRefused.Replace("A pull request", "a pull request", StringComparison.Ordinal))));

        // errors as one string or an array of strings (D342's converter), and GitHub's message when no
        // error has one, as the client's ValidationDetail reads them.
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":"A pull request already exists for octo:x."}""")));
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":[null,"A pull request already exists for octo:x."]}""")));
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"A pull request already exists for octo:x.","errors":[]}""")));
        Assert.True(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"A pull request already exists for octo:x.","errors":[{"resource":"PullRequest","code":"custom"}]}""")));
        Assert.Equal("head is invalid", LivePremises.ValidationDetail(await AnswerAsync(422, """{"message":"A pull request already exists for octo:x.","errors":[{"field":"head","code":"invalid"}]}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"A pull request already exists for octo:x.","errors":[{"field":"head","code":"invalid"}]}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":[]}""")));
        Assert.False(LivePremises.IsDuplicateRefusal(await AnswerAsync(422, """{"message":"Validation Failed","errors":["No commits between main and x","A pull request already exists for octo:x."]}""")));
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
    public async Task Check_3_s_provider_sends_through_the_log_then_the_rewriter_to_github_s_api_alone()
    {
        var inner = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(VersionRefused, Encoding.UTF8, "application/json") });
        var store = new LiveRequestLog.Store();
        var log = new LiveLog();
        using (var provider = GitHubLiveTests.Provider(store, log, "2000-01-01", () => inner))
        {
            Assert.NotNull((await provider.CheckTokenAsync(Token, Cancelled)).Failure);
        }

        // The rewriter, behind the log, changed the version alone; the log saw the request as sent.
        var sent = Assert.Single(inner.Requests);
        Assert.Equal(("GET", "https://api.github.com/user", "2000-01-01"), (sent.Method, sent.Url, sent.Version));
        Assert.True(string.Equals(sent.Authorization, "Bearer " + Token, StringComparison.Ordinal), "The provider's request did not carry the token as a Bearer token.");
        var record = Assert.Single(store.Snapshot());
        Assert.Equal(("GET", "https", "api.github.com", "/user", (int?)400, true), (record.Method, record.Scheme, record.Host, record.Path, record.Status, record.AuthorizationSent));
        Assert.Empty(LiveRequestLog.ReachProblems(store.Snapshot()));
        Assert.Null(log.FoundIn(Token));
    }

    [Fact]
    public void The_owner_s_token_is_refused_for_another_api_than_github_s()
    {
        var github = VisualCommit.Hosting.GitHubEndpoints.GitHubCom.ApiBase;
        var fake = VisualCommit.Hosting.GitHubEndpoints.Local(1).ApiBase;
        Assert.True(GitHubLiveApi.SendsTheTokenElsewhere(fake, Token, Token));
        Assert.False(GitHubLiveApi.SendsTheTokenElsewhere(fake, "github_pat_FAKE_made-up", Token));
        Assert.False(GitHubLiveApi.SendsTheTokenElsewhere(fake, Token, null));
        Assert.False(GitHubLiveApi.SendsTheTokenElsewhere(github, Token, Token));
        Assert.False(GitHubLiveApi.SendsTheTokenElsewhere(new Uri("https://API.GitHub.com/"), Token, Token));
        Assert.False(GitHubLiveApi.SendsTheTokenElsewhere(null, Token, Token));
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
    public async Task Check_1_s_probes_with_a_fake_standing_in_for_github_note_same_break_nothing_and_name_no_address()
    {
        using var folder = new TempDirectory("live-safety");
        var output = new List<string>();
        var run = new LiveRun(Token, folder.Combine("run.txt"), output.Add);
        await using var github = await LiveFake.StartAsync("octo", "sandbox", Cancelled);

        var broken = await new GitHubLiveTests(run).ProbeTextsAsync(github.Api, "octo", "sandbox", "main", "20261030-232555Z");

        Assert.Empty(broken);
        var probes = run.Notes.Where(note => note.StartsWith("GitHub's text for ", StringComparison.Ordinal)).ToList();
        Assert.Equal(10, probes.Count);

        // The fake answers a wrong version before it reads the token, so without the rate limit's
        // headers, and may answer a lookup without a body, so without Content-Type.
        Assert.All(probes.SkipLast(1), note => Assert.Matches(@"^GitHub's text for .+: \d{3} \(request FAKE-\d{4}\); Content-Type (application/json; charset=utf-8|not sent), API version selected not sent, X-RateLimit-Remaining (\d+|not sent); the fake: same\. GitHub's body", note));
        Assert.Equal("GitHub's text for the commits of an empty repository: not tried (the sandbox has a commit); the fake: status 409, message \"Git Repository is empty.\".", probes[^1]);

        // The latest commit's author's address, which three of the lookups asked with, is in no note.
        run.Dispose();
        Assert.Equal(run.Notes, output);
        Assert.DoesNotContain(run.Notes, note => note.Contains(LiveFake.Email, StringComparison.OrdinalIgnoreCase) || note.Contains(Uri.EscapeDataString(LiveFake.Email), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(LiveFake.Email, File.ReadAllText(folder.Combine("run.txt")), StringComparison.OrdinalIgnoreCase);
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

/// <summary>
/// The new providers' safety nets offline (docs/phase-7b-design.md 15.9): the variables, the token
/// after any user, each credential form and request id, D352's guard with D374's path, GitLab's guard,
/// no redirect followed, D373's made-up values and the clean-up's selection; made-up values and
/// in-process handlers only.
/// </summary>
public sealed partial class LiveSafetyTests
{
    private const string AzureRepo = "Fernhill-Labs/visualcommit-sandbox/visualcommit-sandbox";

    private const string Organization = "Fernhill-Labs";

    private const string Project = "visualcommit-sandbox";

    private const string Repository = "visualcommit-sandbox";

    private const string MadeUpOrganization = "vc-none-0123456789ab";

    /// <summary>A made-up token: no account's.</summary>
    private const string ProviderToken = "LIVE-SAFETY-made-up-provider-token-0123456789-abcdefghijklmnop";

    private static string AzureHeading => "The live command against Azure DevOps needs two Windows user environment variables, set once on this machine (D346): VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN, the sandbox's token, and VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO, the sandbox as <organization>/<project>/<repository>. ";

    // ---- The variables (15.2) ----------------------------------------------------------------

    [Theory]
    [InlineData(null, null, "Missing: VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN and VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO.")]
    [InlineData(null, AzureRepo, "Missing: VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN.")]
    [InlineData(ProviderToken, null, "Missing: VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO.")]
    [InlineData(ProviderToken, "Fernhill-Labs/visualcommit-sandbox", "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO is not <organization>/<project>/<repository>.")]
    [InlineData(ProviderToken, "Fernhill_Labs/p/r", "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO is not <organization>/<project>/<repository>.")]
    [InlineData(ProviderToken, "Fernhill-Labs/../r", "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO is not <organization>/<project>/<repository>.")]
    [InlineData(ProviderToken, "glpat-madeUpValue", "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO holds what looks like a token, not <organization>/<project>/<repository>.")]
    [InlineData(ProviderToken, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO holds what looks like a token, not <organization>/<project>/<repository>.")]
    [InlineData(ProviderToken, "a/" + ProviderToken, "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO holds what looks like a token, not <organization>/<project>/<repository>.")]
    public void Azure_DevOps_variables_fail_with_a_message_that_names_no_value(string? token, string? repository, string detail)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => LiveSettings.ResolveAzureDevOps(token, repository));
        Assert.Equal(AzureHeading + detail + " Set them in Windows' Environment Variables dialog, then start a new terminal.", refused.Message);
        Assert.DoesNotContain("Fernhill", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_sandboxes_of_the_new_providers_read_their_variables_and_hide_the_organization()
    {
        var azure = LiveSettings.ResolveAzureDevOps(" " + ProviderToken + " ", AzureRepo);
        Assert.Equal((Organization, Project, Repository), (azure.Organization, azure.Project, azure.Repository));
        Assert.Equal("<organization>/visualcommit-sandbox/visualcommit-sandbox", azure.ToString());
        Assert.True(string.Equals(azure.Token, ProviderToken, StringComparison.Ordinal), "The token was not trimmed as read.");
        var gitlab = LiveSettings.ResolveGitLab(ProviderToken, "ada/group/sub/visualcommit-sandbox");
        Assert.Equal(("ada/group/sub", "visualcommit-sandbox", "ada"), (gitlab.Namespace, gitlab.Project, gitlab.Login));
        Assert.Throws<InvalidOperationException>(() => LiveSettings.ResolveGitLab(ProviderToken, "visualcommit-sandbox"));
        Assert.Throws<InvalidOperationException>(() => LiveSettings.ResolveGitLab(ProviderToken, string.Join('/', Enumerable.Repeat("g", 22))));
        Assert.Equal(21, LiveSettings.ResolveGitLab(ProviderToken, string.Join('/', Enumerable.Repeat("g", 21))).FullName.Split('/').Length);
        Assert.Equal("of 84 characters (Azure DevOps' current length)", LiveSettings.KindOf(HostingProvider.AzureDevOps, new string('x', 84)));
        Assert.Equal("of neither of Azure DevOps' two lengths", LiveSettings.KindOf(HostingProvider.AzureDevOps, ProviderToken));
        Assert.Equal("GitLab's (glpat-)", LiveSettings.KindOf(HostingProvider.GitLab, "glpat-made-up"));
        Assert.Equal(6, LiveSettings.NewVariables.Length + 2);
        foreach (var variable in LiveSettings.NewVariables)
        {
            Assert.Contains(new KeyValuePair<string, string?>(variable, null), GitIsolation.MachineIndependence);
        }
    }

    // ---- The token after any user (g4 D1.6) ---------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("oauth2")]
    [InlineData("x-access-token")]
    [InlineData("abcd")]
    [InlineData("abcde")]
    [InlineData("abcdef")]
    public void The_token_is_found_in_base64_after_any_user(string user) =>
        Assert.NotNull(LiveTokenForms.FoundIn("Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + ProviderToken)), ProviderToken));

    // ---- The forms, the request id and the guard (15.4) ----------------------------------------

    [Theory]
    [InlineData("Bearer", null, null, LiveCredentialForm.Bearer)]
    [InlineData("Basic", null, null, LiveCredentialForm.Basic)]
    [InlineData("token", null, null, LiveCredentialForm.OtherScheme)]
    [InlineData(null, "PRIVATE-TOKEN", null, LiveCredentialForm.PrivateToken)]
    [InlineData(null, "JOB-TOKEN", null, LiveCredentialForm.JobToken)]
    [InlineData(null, null, "private_token", LiveCredentialForm.QueryToken)]
    [InlineData(null, null, "access_token", LiveCredentialForm.QueryToken)]
    [InlineData(null, null, null, LiveCredentialForm.None)]
    public void Each_credential_form_is_read_by_its_form_alone(string? scheme, string? header, string? query, LiveCredentialForm form)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gitlab.com/api/v4/user" + (query is null ? string.Empty : $"?{query}=made-up"));
        if (scheme is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", scheme + " made-up");
        }

        if (header is not null)
        {
            request.Headers.TryAddWithoutValidation(header, "made-up");
        }

        Assert.Equal(form, LiveReach.FormOf(request));
    }

    [Theory]
    [InlineData("ActivityId", "4c7d1e2a-made-up")]
    [InlineData("x-request-id", "01JMADEUPREQUESTID")]
    public async Task A_new_provider_s_request_is_recorded_with_its_form_request_id_and_kept_headers(string header, string id)
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ =>
        {
            var answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            answer.Headers.TryAddWithoutValidation(header, id);
            answer.Headers.TryAddWithoutValidation("RateLimit-Remaining", "1999");
            answer.Headers.TryAddWithoutValidation("X-Secret-Header", "never kept");
            return answer;
        });
        using var client = new HttpClient(new LiveRequestLog(stub, store, LiveReach.GitLab()));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gitlab.com/api/v4/user");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + ProviderToken);
        using var response = await client.SendAsync(request, Cancelled);
        var record = Assert.Single(store.Snapshot());
        Assert.Equal((HostingProvider.GitLab, LiveCredentialForm.Bearer, id, true), (record.Provider, record.Form, record.RequestId, record.AuthorizationSent));
        Assert.Equal(["ratelimit-remaining"], record.KeptHeaders.Keys);
        Assert.DoesNotContain(ProviderToken, record.Describe(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/fernhill-labs/_apis/CONNECTIONDATA")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=Fernhill-Labs&api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests/3?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/info/refs?service=git-upload-pack")]
    public void The_guard_admits_the_sandbox_s_paths_with_basic_or_no_credential(string url)
    {
        var reach = AzureReach();
        Assert.Null(reach.RefusalOf(new Uri(url), LiveCredentialForm.Basic));
        Assert.Null(reach.RefusalOf(new Uri(url), LiveCredentialForm.None));
    }

    [Theory]
    [InlineData("https://dev.azure.com/other-org/_apis/connectionData")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/other-project/_apis/git/repositories")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/other-repo")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/7b0a0010-0000-4a00-8000-00000000fa4e/_apis/git/repositories/visualcommit-sandbox")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/7b0a0011-0000-4a00-8000-00000000fa4e")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/git/repositories")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/projects")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/pullrequests/3")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/%2E%2E/x")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/a%2Fb")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/a%5Cb")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox//info/refs")]
    [InlineData("https://Fernhill-Labs.visualstudio.com/visualcommit-sandbox/_git/visualcommit-sandbox/info/refs")]
    [InlineData("https://vssps.dev.azure.com/Fernhill-Labs/_apis/connectionData")]
    [InlineData("http://dev.azure.com/Fernhill-Labs/_apis/connectionData")]
    [InlineData("https://dev.azure.com:444/Fernhill-Labs/_apis/connectionData")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/4e080c62-fa21-4fbc-8fef-2a10a2b38049?organizationName=Fernhill-Labs")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=other-org")]
    public void The_guard_refuses_everything_else_naming_the_origin_and_never_the_path(string url)
    {
        var refusal = AzureReach().RefusalOf(new Uri(url), LiveCredentialForm.Basic);
        Assert.NotNull(refusal);
        Assert.DoesNotContain("visualcommit-sandbox", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fernhill", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_guard_refuses_a_dot_segment_as_written_and_another_scheme_of_credential()
    {
        var reach = AzureReach();
        var dotted = new Uri("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/../visualcommit-sandbox/info/refs", new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
        Assert.NotNull(reach.RefusalOf(dotted, LiveCredentialForm.Basic));
        Assert.NotNull(reach.RefusalOf(new Uri("https://dev.azure.com/Fernhill-Labs/_apis/connectionData"), LiveCredentialForm.Bearer));
        Assert.NotNull(reach.RefusalOf(new Uri("https://dev.azure.com/Fernhill-Labs/_apis/connectionData?private_token=x"), LiveCredentialForm.QueryToken));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(404, true)]
    [InlineData(200, false)]
    [InlineData(302, false)]
    [InlineData(401, false)]
    public void D374_s_path_takes_basic_only_after_its_404_without_one_in_this_run(int? anonymous, bool basic)
    {
        var reach = AzureReach();
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData");
        Assert.Null(reach.RefusalOf(path, LiveCredentialForm.None));
        if (anonymous is { } status)
        {
            reach.Answered(path, LiveCredentialForm.None, status);
        }

        Assert.Equal(basic, reach.RefusalOf(path, LiveCredentialForm.Basic) is null);
        Assert.NotNull(reach.RefusalOf(new Uri("https://dev.azure.com/vc-none-ffffffffffff/_apis/connectionData"), LiveCredentialForm.None));
        Assert.NotNull(reach.RefusalOf(new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/git/repositories"), LiveCredentialForm.None));
        Assert.NotNull(reach.RefusalOf(new Uri($"https://dev.azure.com/{MadeUpOrganization}/{Project}/_git/{Repository}/info/refs"), LiveCredentialForm.None));
        Assert.Matches("^vc-none-[0-9a-f]{12}$", AzureDevOpsReach.DrawMadeUpOrganization());
    }

    [Fact]
    public async Task D374_s_probe_over_a_stub_that_answers_200_sends_nothing_with_the_token_and_notes_the_skip()
    {
        using var folder = new TempDirectory("live-safety");
        var notes = new List<string>();
        var reach = AzureReach();
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, store, reach), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen(), madeUpOrganization: MadeUpOrganization);
        var run = new LiveRun(ProviderToken, folder.Combine("run.txt"), notes.Add, new LiveScreen(), HostingProvider.AzureDevOps);
        var problems = new List<string>();
        await AzureDevOpsLiveTests.ProbeMadeUpOrganizationAsync(run, api, null, [], MadeUpOrganization, problems);
        var sent = Assert.Single(store.Snapshot());
        Assert.Equal(LiveCredentialForm.None, sent.Form);
        Assert.Single(stub.Requests);
        Assert.Contains(notes, note => note.StartsWith("D374's read with the token: skipped", StringComparison.Ordinal));
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("https://gitlab.example/api/v4/user", LiveCredentialForm.Bearer)]
    [InlineData("https://gitlab.com/ada/visualcommit-sandbox", LiveCredentialForm.Bearer)]
    [InlineData("https://gitlab.com/api/v4/user", LiveCredentialForm.PrivateToken)]
    [InlineData("https://gitlab.com/api/v4/user?private_token=x", LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/user", LiveCredentialForm.Basic)]
    [InlineData("http://gitlab.com/api/v4/user", LiveCredentialForm.None)]
    public void GitLab_s_guard_refuses_another_host_a_web_path_and_other_credentials(string url, LiveCredentialForm form) =>
        Assert.NotNull(LiveReach.GitLab().RefusalOf(new Uri(url), form));

    [Fact]
    public void GitLab_s_guard_admits_its_api_with_bearer_or_no_credential()
    {
        Assert.Null(LiveReach.GitLab().RefusalOf(new Uri("https://gitlab.com/api/v4/projects/ada%2Fvisualcommit-sandbox/merge_requests?state=opened"), LiveCredentialForm.Bearer));
        Assert.Null(LiveReach.GitLab().RefusalOf(new Uri("https://gitlab.com/api/v4/user"), LiveCredentialForm.None));
    }

    [Fact]
    public async Task A_refused_request_never_leaves_and_names_the_origin_alone()
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new LiveRequestLog(stub, store, AzureReach()));
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://dev.azure.com/Fernhill-Labs/_apis/projects"), Cancelled));
        Assert.Empty(stub.Requests);
        Assert.DoesNotContain("_apis", refused.Message, StringComparison.Ordinal);
        Assert.StartsWith("refused: ", Assert.Single(store.Snapshot()).FailedWith, StringComparison.Ordinal);
        Assert.NotEmpty(LiveRequestLog.ReachProblems(store.Snapshot(), AzureReach()));
    }

    [Fact]
    public async Task Each_new_live_api_follows_no_redirect_and_records_the_302_as_one_request()
    {
        using (var handler = LiveServiceApi.NewHandler())
        {
            Assert.False(handler.AllowAutoRedirect);
            Assert.False(handler.UseCookies);
        }

        HttpResponseMessage Redirect(HttpRequestMessage request) => new(HttpStatusCode.Found) { Headers = { Location = new Uri("https://login.example/_signin") } };
        var azureStore = new LiveRequestLog.Store();
        using (var azure = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(new StubHandler(Redirect), azureStore, AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen()))
        {
            Assert.Equal(302, (await azure.RepositoryAsync(Cancelled)).Status);
        }

        var gitlabStore = new LiveRequestLog.Store();
        using (var gitlab = new GitLabLiveApi(new LiveGitLabSandbox("ada", Project, ProviderToken), new LiveRequestLog(new StubHandler(Redirect), gitlabStore, LiveReach.GitLab()), VisualCommit.Hosting.GitLab.GitLabEndpoints.GitLabCom.ApiBase, TimeSpan.FromSeconds(5), new LiveScreen()))
        {
            Assert.Equal(302, (await gitlab.UserAsync(LiveCredential.Token, Cancelled)).Status);
        }

        Assert.Equal((302, 302), (Assert.Single(azureStore.Snapshot()).Status, Assert.Single(gitlabStore.Snapshot()).Status));
    }

    // ---- D373's made-up values and the clean-up's selection --------------------------------------

    [Fact]
    public void D373_s_made_up_values_have_their_shapes_hold_no_16_of_the_token_s_characters_and_are_named_by_kind()
    {
        var azure = LiveMadeUp.AzureShaped(ProviderToken);
        Assert.Equal(84, azure.Length);
        Assert.Equal("AZDO", azure[76..80]);
        Assert.Equal(HostingProvider.AzureDevOps, HostingProviders.TokenFormOf(azure));
        Assert.True(azure.All(char.IsAsciiLetterOrDigit));
        var gitlab = LiveMadeUp.GitLabShaped(ProviderToken);
        Assert.Matches("^glpat-[A-Za-z0-9]{20}$", gitlab);
        foreach (var value in new[] { azure, gitlab, LiveMadeUp.Short })
        {
            Assert.Null(LiveTokenForms.FoundIn(value, ProviderToken));
            Assert.Null(LiveTokenForms.FoundIn(ProviderToken, value));
        }

        Assert.DoesNotContain(LiveMadeUp.Short, LiveMadeUp.ShortValue.Name, StringComparison.Ordinal);
        Assert.Null(LiveTokenForms.FoundIn(LiveCredential.MadeUp(azure, "a made-up value of 84 characters").ToString(), azure));
    }

    [Fact]
    public void The_clean_up_selects_only_the_sandbox_s_own_live_branches_and_their_requests()
    {
        Assert.True(LiveProviderLeftovers.IsLeftover(new LiveAzurePullRequest(1, "refs/heads/visualcommit-live/20261102-010203Z", "active", false)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveAzurePullRequest(2, "refs/heads/visualcommit-live/x", "active", true)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveAzurePullRequest(3, "refs/heads/main", "active", false)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveAzurePullRequest(4, "refs/heads/visualcommit-live/", "active", false)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveAzurePullRequest(5, "refs/heads/visualcommit-livex/y", "active", false)));
        Assert.True(LiveProviderLeftovers.IsLeftover(new LiveMergeRequest(1, "visualcommit-live/20261102-010203Z", false)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveMergeRequest(2, "visualcommit-live/x", true)));
        Assert.False(LiveProviderLeftovers.IsLeftover(new LiveMergeRequest(3, "feature/visualcommit-live/x", false)));
        Assert.Equal(
            ["refs/heads/visualcommit-live/a"],
            LiveProviderLeftovers.Branches([("refs/heads/visualcommit-live/a", "1"), ("refs/heads/visualcommit-livex", "2"), ("refs/tags/visualcommit-live/b", "3"), ("refs/heads/visualcommit-live/", "4")]).Select(reference => reference.Name));
    }

    [Fact]
    public void The_new_orders_name_every_live_test_once()
    {
        foreach (var (type, methods) in new[] { (typeof(AzureDevOpsLiveTests), AzureDevOpsLiveOrder.Methods), (typeof(GitLabLiveTests), GitLabLiveOrder.Methods) })
        {
            var declared = type.GetMethods()
                .Where(method => method.GetCustomAttributes(inherit: true).OfType<Xunit.v3.IFactAttribute>().Any())
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal);
            Assert.Equal(declared, methods.Order(StringComparer.Ordinal));
            Assert.Equal(methods.Count, methods.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(methods.ToList().IndexOf("Check_2_the_providers_and_the_session") + 1, methods.ToList().IndexOf("Check_2b_probes"));
        }
    }

    // ---- What the review of WP-LIVE1 found -------------------------------------------------------

    [Fact]
    public void Each_live_method_s_call_limits_sum_under_the_watchdog_s_600_s()
    {
        var ordered = AzureDevOpsLiveOrder.Methods.Select(method => nameof(AzureDevOpsLiveTests) + "." + method)
            .Concat(GitLabLiveOrder.Methods.Select(method => nameof(GitLabLiveTests) + "." + method));
        Assert.Equal(ordered.Order(StringComparer.Ordinal), LiveSteps.Sums.Keys.Order(StringComparer.Ordinal));
        Assert.All(LiveSteps.Sums, sum => Assert.True(sum.Value < TimeSpan.FromSeconds(600), $"{sum.Key} sums to {sum.Value.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} s."));
    }

    [Fact]
    public void The_kept_headers_hold_the_api_version_azure_devops_names_in_content_type()
    {
        using var answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        answer.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8; api-version=7.1");
        answer.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "199");
        var kept = LiveRequestLog.KeptOf(answer);
        Assert.Equal(["api-version", "x-ratelimit-remaining"], kept.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("7.1", kept["api-version"]);
    }

    [Fact]
    public void Each_new_live_api_s_public_constructor_builds_the_log_over_a_handler_that_follows_no_redirect()
    {
        var store = new LiveRequestLog.Store();
        using var azure = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), AzureReach(), store, new LiveScreen());
        using var gitlab = new GitLabLiveApi(new LiveGitLabSandbox("ada", Project, ProviderToken), LiveReach.GitLab(), store, new LiveScreen());
        foreach (var api in new LiveServiceApi[] { azure, gitlab })
        {
            var inner = Assert.IsType<SocketsHttpHandler>(Assert.IsType<LiveRequestLog>(api.Handler).InnerHandler);
            Assert.Equal((false, false, DecompressionMethods.All, TimeSpan.FromSeconds(10)), (inner.AllowAutoRedirect, inner.UseCookies, inner.AutomaticDecompression, inner.ConnectTimeout));
        }

        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void Beside_compares_gitlab_s_shape_f_description()
    {
        static LiveServiceAnswer F(string description) => Answer(403, $$"""{"error":"insufficient_granular_scope","error_description":"{{description}}","scope":"read_user"}""");
        Assert.Equal("The request requires higher privileges.", F("The request requires higher privileges.").Message);
        Assert.Equal("same", LiveServiceAnswer.Beside(F("The request requires higher privileges."), F("The request requires higher privileges."), [], []));
        Assert.Equal("message Another text.", LiveServiceAnswer.Beside(F("The request requires higher privileges."), F("Another text."), [], []));
        Assert.Equal("shape B", Answer(400, """{"message":"shape B","error_description":"not read"}""").Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_info_refs_probe_goes_as_git_sends_it_and_the_core_area_as_the_client_does(bool suppress)
    {
        var stub = new HeadersStub();
        using var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen());
        await api.InfoRefsAsync(LiveMadeUp.ShortValue, suppress, LiveSteps.AgentOf("git version 2.56.0.windows.4"), Cancelled);
        var git = Assert.Single(stub.Requests);
        Assert.Equal("git/2.56.0.windows.4", git.Headers["User-Agent"]);
        Assert.Equal("*/*", git.Headers["Accept"]);
        Assert.Equal("version=2", git.Headers["Git-Protocol"]);
        Assert.Equal(suppress, git.Headers.ContainsKey("X-TFS-FedAuthRedirect"));
        await api.CoreAreaAsync(LiveCredential.Token, Cancelled);
        var area = stub.Requests[1];
        Assert.Equal($"https://dev.azure.com/{Organization}/_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?api-version={AzureDevOpsApi.ConnectionDataVersion}", area.Url);
        Assert.Equal("application/json", area.Headers["Accept"]);
        Assert.NotEqual("git/2.56.0.windows.4", area.Headers["User-Agent"]);
    }

    [Theory]
    [InlineData(200, """{"value":[{"name":"refs/heads/visualcommit-live/x-even","success":true,"updateStatus":"succeeded"}],"count":1}""", true)]
    [InlineData(200, """{"value":[{"name":"refs/heads/visualcommit-live/x-even","success":true}],"count":1}""", true)]
    [InlineData(200, """{"value":[{"name":"refs/heads/visualcommit-live/x-even","success":false,"updateStatus":"staleOldObjectId"}],"count":1}""", false)]
    [InlineData(200, """{"value":[],"count":0}""", false)]
    [InlineData(400, """{"value":[{"success":true}]}""", false)]
    public void A_refs_post_took_only_when_its_update_says_so(int status, string body, bool took) =>
        Assert.Equal(took, AzureDevOpsLiveApi.RefUpdated(Answer(status, body)));

    [Fact]
    public async Task A_poll_stops_when_the_ref_comes_holds_each_read_to_what_is_left_and_restores_the_limit()
    {
        var reads = 0;
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Interlocked.Increment(ref reads) < 3 ? """{"value":[],"count":0}""" : """{"value":[{"name":"refs/pull/7/merge","objectId":"0123"}],"count":1}"""),
        });
        using var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen());
        var appeared = await LiveSteps.PollAsync(api, () => api.RefsAsync("pull/7/merge", Cancelled), refs => refs.Count > 0, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(10));
        Assert.Equal((true, 3, (string?)null), (appeared.Done, appeared.Reads, appeared.Failure));
        Assert.Equal(TimeSpan.FromSeconds(5), api.Limit);
        var stayed = await LiveSteps.PollAsync(api, () => api.RefsAsync("pull/7/merge", Cancelled), refs => refs.Count == 0, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(50));
        Assert.False(stayed.Done);
        Assert.True(stayed.Reads > 1, $"{stayed.Reads} reads");
        Assert.True(stayed.Took < TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), api.Limit);
    }

    [Fact]
    public async Task The_identity_read_is_sent_once_per_run_and_its_ids_are_learned_before_any_note()
    {
        using var folder = new TempDirectory("live-safety");
        var notes = new List<string>();
        const string userId = "7b0a0201-0000-4a00-8000-00000000fa4e";
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"authenticatedUser":{"id":"{{userId}}","descriptor":"Microsoft.IdentityModel.Claims.ClaimsIdentity;7b0a0202-0000-4a00-8000-00000000fa4e\\ada@example.com","providerDisplayName":"Ada"},"instanceId":"7b0a0203-0000-4a00-8000-00000000fa4e"}"""),
        });
        var screen = new LiveScreen();
        screen.AddOrganization(Organization);
        var run = new LiveRun(ProviderToken, folder.Combine("run.txt"), notes.Add, screen, HostingProvider.AzureDevOps);
        using var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), screen);
        var problems = new List<string>();
        await AzureDevOpsLiveTests.IdentityOnceAsync(run, api, problems);
        await AzureDevOpsLiveTests.IdentityOnceAsync(run, api, problems);
        Assert.Single(stub.Requests);
        Assert.True(run.IdentityRead);
        Assert.Empty(problems);
        run.Note($"TF400813: The user '{userId}' is not authorized.");
        Assert.DoesNotContain(userId, notes[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_fakes_beside_hold_the_run_s_branch_ahead_and_the_branch_with_nothing_ahead_for_check_2b()
    {
        const string branch = "visualcommit-live/20261102-010203Z";
        await using (var azure = await LiveAzureFake.StartAsync(Project, Repository, branch, branch + LiveSteps.EvenSuffix, Cancelled))
        {
            var draft = await azure.Api.CreateAsync(branch, LiveAzureFake.DefaultBranch, "VisualCommit live test", true, Cancelled);
            Assert.True(draft.Status is 200 or 201, $"{draft}: {draft.Text}");
            Assert.Equal(409, (await azure.Api.CreateAsync(branch, LiveAzureFake.DefaultBranch, "VisualCommit live test", true, Cancelled)).Status);
            Assert.Contains((await azure.Api.RefsAsync("heads/" + LiveLeftovers.Prefix, Cancelled)).Select(reference => reference.Name), name => name == LiveLeftovers.HeadsPrefix + branch + LiveSteps.EvenSuffix);
        }

        await using (var gitlab = await LiveGitLabFake.StartAsync(Project, branch, branch + LiveSteps.EvenSuffix, Cancelled))
        {
            var draft = await gitlab.Api.CreateAsync(branch, LiveAzureFake.DefaultBranch, "Draft: VisualCommit live test", Cancelled);
            Assert.True(draft.Status == 201, $"{draft}: {draft.Text}");
            var even = await gitlab.Api.CreateAsync(branch + LiveSteps.EvenSuffix, LiveAzureFake.DefaultBranch, "VisualCommit live test", Cancelled);
            Assert.True(even.Status == 201, $"{even}: {even.Text}");   // 4.3 (h): a branch with nothing ahead is allowed; (e) is the default branch into itself
        }
    }

    [Fact]
    public async Task The_fake_beside_is_asked_for_its_304_with_its_own_etag()
    {
        await using var fake = await LiveGitLabFake.StartAsync(Project, null, null, Cancelled);
        using var service = new GitLabLiveApi(new LiveGitLabSandbox("ada", Project, ProviderToken), new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotModified)), VisualCommit.Hosting.GitLab.GitLabEndpoints.GitLabCom.ApiBase, TimeSpan.FromSeconds(5), new LiveScreen());
        Assert.Equal(304, (await GitLabLiveTests.UserWithOwnETagAsync(fake.Api, service, "W/\"made-up-service-etag\"", Cancelled)).Status);
        Assert.Equal(304, (await GitLabLiveTests.UserWithOwnETagAsync(service, service, "W/\"made-up-service-etag\"", Cancelled)).Status);
    }

    [Fact]
    public void The_push_waits_for_its_own_record_never_a_background_one()
    {
        static VisualCommit.Core.Operations.OperationRecord Record(long sequence, bool background, VisualCommit.Core.Operations.OperationKind kind) =>
            new("id", "sandbox", "title", DateTimeOffset.UnixEpoch, TimeSpan.Zero, VisualCommit.Core.Operations.OperationOutcome.Succeeded, "message") { Sequence = sequence, IsBackground = background, Kind = kind };
        Assert.False(LiveApp.IsPushRecord(Record(5, true, VisualCommit.Core.Operations.OperationKind.Local), 4));
        Assert.False(LiveApp.IsPushRecord(Record(5, true, VisualCommit.Core.Operations.OperationKind.Network), 4));
        Assert.False(LiveApp.IsPushRecord(Record(5, false, VisualCommit.Core.Operations.OperationKind.Local), 4));
        Assert.False(LiveApp.IsPushRecord(Record(4, false, VisualCommit.Core.Operations.OperationKind.Network), 4));
        Assert.True(LiveApp.IsPushRecord(Record(5, false, VisualCommit.Core.Operations.OperationKind.Network), 4));
    }

    private static LiveServiceAnswer Answer(int status, string body)
    {
        using var document = JsonDocument.Parse(body);
        return new LiveServiceAnswer(status, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json; charset=utf-8" }, body, false, document.RootElement.Clone());
    }

    private static AzureDevOpsReach AzureReach() => LiveReach.AzureDevOps(Organization, Project, Repository, MadeUpOrganization);

    /// <summary>A handler that answers 401 and keeps each request's URL and headers by name.</summary>
    private sealed class HeadersStub : HttpMessageHandler
    {
        public List<(string Url, Dictionary<string, string> Headers)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add((request.RequestUri!.AbsoluteUri, request.Headers.ToDictionary(header => header.Key, header => string.Join(' ', header.Value), StringComparer.OrdinalIgnoreCase)));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}

/// <summary>
/// The screen of the new providers' notes (docs/phase-7b-design.md 15.3, 15.9; D372): the
/// organization at each boundary, addresses, ids learned from made-up copies of the services'
/// answers (their member names and nesting, made-up values), kept addresses, encoded survivors,
/// withholding and <see cref="LiveRun.FailureOf"/>; GitHub's run unchanged. Made-up values only.
/// </summary>
public sealed class LiveScreenTests
{
    private const string Org = "Fernhill-Labs";
    private const string UserId = "7b0a0101-0000-4a00-8000-00000000fa4e";
    private const string InstanceId = "7b0a0102-0000-4a00-8000-00000000fa4e";
    private const string OtherHostId = "7b0a0103-0000-4a00-8000-00000000fa4e";
    private const string Tenant = "7b0a0104-0000-4a00-8000-00000000fa4e";
    private const string Address = "ada.lovelace@corp.test";

    private static LiveScreen Screen(string organization = Org)
    {
        var screen = new LiveScreen();
        screen.AddOrganization(organization);
        return screen;
    }

    [Theory]
    [InlineData("Fernhill-Labs", "<the organization>")]
    [InlineData("FERNHILL-LABS is the name", "<the organization> is the name")]
    [InlineData("https://dev.azure.com/fernhill-labs/p", "https://dev.azure.com/<the organization>/p")]
    [InlineData("x%2FFernhill-Labs%2Fy", "x%2F<the organization>%2Fy")]
    [InlineData("Fernhill-Labs.visualstudio.com", "<the organization>.visualstudio.com")]
    [InlineData("Fernhill-Labsx and Fernhill-Labs-x and xFernhill-Labs", "Fernhill-Labsx and Fernhill-Labs-x and xFernhill-Labs")]
    public void The_organization_is_hidden_in_any_case_at_a_boundary_and_never_inside_a_longer_name(string text, string screened) =>
        Assert.Equal(screened, Screen().Apply(text));

    [Fact]
    public void Addresses_are_hidden_learned_or_not_and_d325_s_and_example_com_ones_kept()
    {
        var screen = Screen();
        screen.AddAddress(Address);
        Assert.Equal("by <the user's address> and <the user's address>.", screen.Apply($"by {Address} and {Uri.EscapeDataString(Address)}."));
        Assert.Equal("from <an address>, kept live-test@visualcommit.invalid and ada@example.com.", screen.Apply("from bob@other.test, kept live-test@visualcommit.invalid and ada@example.com."));
        Assert.Equal("git@ssh.dev.azure.com:v3/<the organization>/p/r", screen.Apply("git@ssh.dev.azure.com:v3/Fernhill-Labs/p/r"));
    }

    [Fact]
    public void A_url_s_user_is_the_organization_s_and_both_are_screened_with_the_rest_kept()
    {
        var screen = Screen("o");
        var screened = screen.Apply("https://o@dev.azure.com/o/p/_git/r");
        Assert.Equal("https://<the organization>@dev.azure.com/<the organization>/p/_git/r", screened);
        Assert.Null(screen.SurvivorIn(screened));
        Assert.Equal("https://***@dev.azure.com/<the organization>/p/_git/r", screen.Apply("https://***@dev.azure.com/o/p/_git/r"));
    }

    [Fact]
    public void Ids_and_addresses_are_learned_from_a_made_up_copy_of_connection_data()
    {
        var screen = Screen();
        var connection = $$$"""
            {"authenticatedUser":{"id":"{{{UserId}}}","descriptor":"Microsoft.IdentityModel.Claims.ClaimsIdentity;{{{Tenant}}}\\{{{Address}}}","subjectDescriptor":"aad.N2IwYTAxMDEtbWFkZS11cC1zdWJqZWN0","providerDisplayName":"Ada Lovelace","isActive":true,"properties":{"Account":{"$type":"System.String","$value":"{{{Address}}}"}},"resourceVersion":2,"metaTypeId":0},
             "authorizedUser":{"id":"{{{UserId}}}"},"instanceId":"{{{InstanceId}}}","deploymentId":"7b0a0105-0000-4a00-8000-00000000fa4e","deploymentType":"hosted","locationServiceData":{"serviceOwner":"7b0a0106-0000-4a00-8000-00000000fa4e"}}
            """;
        using (var document = JsonDocument.Parse(connection))
        {
            screen.Learn(document.RootElement);
        }

        var text = $"user {UserId}, host {InstanceId}, tenant {Tenant}, subject aad.N2IwYTAxMDEtbWFkZS11cC1zdWJqZWN0, address {Address}";
        Assert.Equal("user <the user's id>, host <the organization's id>, tenant <the tenant>, subject <the user's id>, address <the user's address>", screen.Apply(text));
        var gitlab = new LiveScreen();
        using (var user = JsonDocument.Parse("""{"id":1001,"username":"ada","name":"Ada Lovelace","email":"ada.private@corp.test","commit_email":"ada.commits@corp.test","public_email":"","author":{"public_email":"ada.public@corp.test"}}"""))
        {
            gitlab.Learn(user.RootElement);
        }

        Assert.Equal(3, gitlab.Counts.Addresses);
        Assert.Equal("a learned address", gitlab.SurvivorInDataFile("ada.commits%40corp.test"));
    }

    [Fact]
    public void Ids_in_any_case_without_hyphens_escaped_and_in_vssps_paths_are_replaced()
    {
        var screen = Screen();
        screen.AddId(InstanceId, LiveScreen.OrganizationIdPlaceholder);
        var upper = InstanceId.ToUpperInvariant();
        var bare = InstanceId.Replace("-", string.Empty, StringComparison.Ordinal);
        var screened = screen.Apply($"https://spsprodfake.vssps.visualstudio.com/A{upper}/_apis/Identities/x {bare} {Uri.EscapeDataString(InstanceId)}");
        Assert.Equal("https://spsprodfake.vssps.visualstudio.com/A<the organization's id>/_apis/Identities/x <the organization's id> <the organization's id>", screened);
    }

    [Fact]
    public void Sign_in_ids_are_learned_from_a_302_s_location_its_object_moved_body_and_its_context()
    {
        var screen = Screen();
        var context = Convert.ToBase64String(Encoding.UTF8.GetBytes($$$"""{"ht":2,"hid":"{{{OtherHostId}}}","qs":{}}"""));
        var location = $"https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com&reply_to=https%3A%2F%2Fdev.azure.com%2FFernhill-Labs&redirect=1&hid={InstanceId}&context={Uri.EscapeDataString(context)}#ctx=made-up";
        var body = $"<html><head><title>Object moved</title></head><body><h2>Object moved to <a href=\"https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com&amp;hid={InstanceId}&amp;context={context}\">here</a>.</h2></body></html>";
        screen.LearnFromAnswer([new("Location", [location])], body);
        Assert.Equal(2, screen.Counts.Ids);
        var screened = screen.Apply($"Location {location}; body {body}; ids {InstanceId} {OtherHostId}");
        Assert.DoesNotContain(InstanceId, screened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OtherHostId, screened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reply_to", screened, StringComparison.Ordinal);
        Assert.Contains("/_signin?<the sign-in query>", screened, StringComparison.Ordinal);
        Assert.Null(screen.SurvivorIn(screened));
    }

    [Fact]
    public void A_tenant_is_learned_from_its_header_and_from_a_bearer_challenge_after_a_basic_one()
    {
        var screen = new LiveScreen();
        screen.LearnFromAnswer(
            [new("X-VSS-ResourceTenant", [Tenant]), new("WWW-Authenticate", ["Basic realm=\"https://dev.azure.com/\"", $"Bearer authorization_uri=https://login.microsoftonline.com/{OtherHostId}"])],
            null);
        Assert.Equal("<the tenant> <the tenant>", screen.Apply($"{Tenant} {OtherHostId}"));
    }

    [Fact]
    public void The_anonymous_id_and_the_area_ids_are_left_untouched()
    {
        var screen = Screen();
        screen.AddId(LiveScreen.AnonymousId, LiveScreen.UserIdPlaceholder);
        screen.AddId(AzureDevOpsApi.CoreAreaId, LiveScreen.OrganizationIdPlaceholder);
        screen.AddId(LiveScreen.GitAreaId, LiveScreen.OrganizationIdPlaceholder);
        using (var anonymous = JsonDocument.Parse($$$"""{"authenticatedUser":{"id":"{{{LiveScreen.AnonymousId}}}"},"instanceId":"{{{AzureDevOpsApi.CoreAreaId}}}"}"""))
        {
            screen.Learn(anonymous.RootElement);
        }

        var text = $"{LiveScreen.AnonymousId} {AzureDevOpsApi.CoreAreaId} {LiveScreen.GitAreaId}";
        Assert.Equal(text, screen.Apply(text));
        Assert.Equal(0, screen.Counts.Ids);
    }

    [Theory]
    [InlineData("the organization Fernhill-Labs", "the organization's name")]
    [InlineData("x%2FFernhill-Labs%2Fy", "the organization's name")]
    [InlineData("x%252FFernhill-Labs%252Fy", "the organization's name once percent-decoded")]
    [InlineData("by ada.lovelace@corp.test", "a learned address")]
    [InlineData("by ada.lovelace%40corp.test", "a learned address")]
    [InlineData("by bob%40other.test", "an address once percent-decoded")]
    [InlineData("by live-test@visualcommit.invalid in vc-none-0123456789ab", null)]
    [InlineData("an invalid escape %C0%AE stays", null)]
    public void Run_txt_s_survivors_are_found_raw_and_decoded_and_never_named(string text, string? kind)
    {
        var screen = Screen();
        screen.AddAddress(Address);
        Assert.Equal(kind, screen.SurvivorIn(text));
    }

    [Fact]
    public void A_log_and_accounts_json_naming_the_organization_have_no_survivor_but_a_learned_address_is_one()
    {
        var screen = Screen("o");
        screen.AddAddress(Address);
        Assert.Null(screen.SurvivorInDataFile("git clone --progress -- https://***@dev.azure.com/o/p/_git/r r"));
        Assert.Null(screen.SurvivorInDataFile("""{"version":1,"accounts":[{"provider":"azure-devops","host":"dev.azure.com","login":"o","id":0}]}"""));
        Assert.Equal("a learned address", screen.SurvivorInDataFile("author ada.lovelace%40corp.test"));
    }

    [Fact]
    public void Notes_are_screened_and_a_run_txt_with_a_survivor_is_withheld_and_fails_the_run()
    {
        using var folder = new TempDirectory("live-screen");
        var output = new List<string>();
        var file = folder.Combine("run.txt");
        File.WriteAllText(file, "an earlier run's");
        var run = new LiveRun("made-up-token-for-the-screen-0123456789", file, output.Add, Screen(), HostingProvider.AzureDevOps);
        run.Note("GET https://dev.azure.com/Fernhill-Labs/_apis/connectionData: 200");
        Assert.Equal("GET https://dev.azure.com/<the organization>/_apis/connectionData: 200", Assert.Single(output));
        run.Note("x%252FFernhill-Labs%252Fy");
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.Contains("still held the organization's name once percent-decoded", failed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Fernhill", failed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void A_withheld_run_txt_on_a_provider_s_first_run_fails_with_its_reason()
    {
        using var folder = new TempDirectory("live-screen");
        var file = Path.Combine(folder.Path, "azure-devops", "run.txt");
        var run = new LiveRun(null, file, _ => { }, Screen(), HostingProvider.AzureDevOps);
        run.Note("x%252FFernhill-Labs%252Fy");
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.Contains("run.txt was withheld: its screened text still held", failed.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(file));
        const string token = "made-up-token-for-the-screen-0123456789";
        var held = new LiveRun(token, Path.Combine(folder.Path, "gitlab", "run.txt"), _ => { }, Screen(), HostingProvider.GitLab);
        held.WriteRunFile("a text holding " + token);
        Assert.Contains(held.Failures, failure => failure.StartsWith("run.txt was withheld: its text held the token", StringComparison.Ordinal));
    }

    [Fact]
    public void A_clean_run_txt_names_the_provider_and_passes()
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var run = new LiveRun(null, file, _ => { }, Screen(), HostingProvider.GitLab);
        run.Note("D374's made-up organization vc-none-0123456789ab; D325's live-test@visualcommit.invalid.");
        run.Dispose();
        var text = File.ReadAllText(file);
        Assert.StartsWith("VisualCommit's live command against GitLab (docs/phase-7b-design.md 15.7)", text, StringComparison.Ordinal);
        Assert.Contains("vc-none-0123456789ab", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_naming_the_organization_is_rethrown_screened_and_a_check_is_screened()
    {
        using var folder = new TempDirectory("live-screen");
        var run = new LiveRun(null, folder.Combine("run.txt"), _ => { }, Screen(), HostingProvider.AzureDevOps);
        var thrown = Assert.Throws<XunitException>(() => run.FailureOf(new InvalidOperationException("dev.azure.com/Fernhill-Labs refused"), "2, the sign-in").Throw());
        Assert.Equal("Step 2, the sign-in failed with InvalidOperationException: dev.azure.com/<the organization> refused", thrown.Message);
        var clean = new InvalidOperationException("nothing to hide");
        Assert.Same(clean, Assert.Throws<InvalidOperationException>(() => run.FailureOf(clean, "1").Throw()));
        var check = Assert.Throws<XunitException>(() => run.Check(false, () => "Fernhill-Labs is wrong"));
        Assert.Equal("<the organization> is wrong", check.Message);
        run.Dispose();
    }

    [Fact]
    public void GitHub_s_run_keeps_7a_s_notes_unscreened()
    {
        using var folder = new TempDirectory("live-screen");
        var output = new List<string>();
        var run = new LiveRun(null, folder.Combine("run.txt"), output.Add);
        run.Note("octo/sandbox by sandbox-author@visualcommit.invalid and Fernhill-Labs");
        Assert.Equal("octo/sandbox by sandbox-author@visualcommit.invalid and Fernhill-Labs", Assert.Single(output));
        Assert.Null(run.Screener);
        run.Dispose();
        Assert.StartsWith("VisualCommit's live command against GitHub (docs/phase-7a-design.md 14.5)", File.ReadAllText(folder.Combine("run.txt")), StringComparison.Ordinal);
    }
}
