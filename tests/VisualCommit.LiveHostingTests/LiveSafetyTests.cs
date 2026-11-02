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
        Assert.Null(reach.RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.Basic));
        Assert.Null(reach.RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.None));
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
        var refusal = AzureReach().RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.Basic);
        Assert.NotNull(refusal);
        Assert.DoesNotContain("visualcommit-sandbox", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fernhill", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_guard_refuses_a_dot_segment_as_written_and_another_scheme_of_credential()
    {
        var reach = AzureReach();
        var dotted = new Uri("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/../visualcommit-sandbox/info/refs", new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, dotted, LiveCredentialForm.Basic));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, new Uri("https://dev.azure.com/Fernhill-Labs/_apis/connectionData"), LiveCredentialForm.Bearer));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, new Uri("https://dev.azure.com/Fernhill-Labs/_apis/connectionData?private_token=x"), LiveCredentialForm.QueryToken));
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
        Assert.Null(reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.None));
        if (anonymous is { } status)
        {
            reach.Answered(path, LiveCredentialForm.None, status);
        }

        Assert.Equal(basic, reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic) is null);
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, new Uri("https://dev.azure.com/vc-none-ffffffffffff/_apis/connectionData"), LiveCredentialForm.None));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/git/repositories"), LiveCredentialForm.None));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, new Uri($"https://dev.azure.com/{MadeUpOrganization}/{Project}/_git/{Repository}/info/refs"), LiveCredentialForm.None));
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
        Assert.NotNull(LiveReach.GitLab().RefusalOf(HttpMethod.Get, new Uri(url), form));

    [Fact]
    public void GitLab_s_guard_admits_its_api_with_bearer_or_no_credential()
    {
        Assert.Null(LiveReach.GitLab().RefusalOf(HttpMethod.Get, new Uri("https://gitlab.com/api/v4/projects/ada%2Fvisualcommit-sandbox/merge_requests?state=opened"), LiveCredentialForm.Bearer));
        Assert.Null(LiveReach.GitLab().RefusalOf(HttpMethod.Get, new Uri("https://gitlab.com/api/v4/user"), LiveCredentialForm.None));
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
    public void D373_s_draw_is_made_again_while_the_token_is_found_in_it_either_way_round()
    {
        var clean = new string('x', 40);
        var draws = new Queue<string>(["made-up " + ProviderToken[..16] + " value", ProviderToken[20..36], Convert.ToBase64String(Encoding.UTF8.GetBytes(ProviderToken)), clean, "never drawn"]);
        Assert.Equal(clean, LiveMadeUp.Draw(ProviderToken, draws.Dequeue));
        Assert.Equal("never drawn", Assert.Single(draws));
        Assert.Equal("first", LiveMadeUp.Draw(null, () => "first"));
    }

    [Fact]
    public async Task D373_s_probes_note_no_made_up_value_they_send()
    {
        using var folder = new TempDirectory("live-safety");
        var sent = new List<string>();
        var stub = new StubHandler(request =>
        {
            if (request.Headers.Authorization is { Scheme: "Basic", Parameter: { } parameter })
            {
                lock (sent)
                {
                    sent.Add(Encoding.UTF8.GetString(Convert.FromBase64String(parameter))[1..]);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"message":"TF400813: made-up refusal"}""", Encoding.UTF8, "application/json") };
        });
        using var api = AzureApi(stub);
        var problems = new List<string>();
        await AzureTests(folder, out var notes).ProbeMadeUpAsync(api, null, "git/2.56.0", problems);
        Assert.Equal(8, sent.Count);
        var values = sent.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(2, values.Count);
        Assert.Contains(LiveMadeUp.Short, values);
        Assert.Contains(values, value => value.Length == 84);
        Assert.NotEmpty(notes);
        foreach (var line in notes.Concat(problems))
        {
            foreach (var value in values)
            {
                Assert.DoesNotContain(value, line, StringComparison.Ordinal);
                Assert.Null(LiveTokenForms.FoundIn(line, value));
            }
        }
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

        // Hunt 3's M8: a request is the sandbox's own only when both project ids are numbers and equal.
        static LiveMergeRequest Read(string json)
        {
            using var document = JsonDocument.Parse(json);
            return LiveMergeRequest.From(document.RootElement);
        }

        Assert.True(LiveProviderLeftovers.IsLeftover(Read("""{"iid":7,"source_branch":"visualcommit-live/x","source_project_id":5,"target_project_id":5}""")));
        Assert.False(LiveProviderLeftovers.IsLeftover(Read("""{"iid":8,"source_branch":"visualcommit-live/x","source_project_id":6,"target_project_id":5}""")));
        Assert.False(LiveProviderLeftovers.IsLeftover(Read("""{"iid":9,"source_branch":"visualcommit-live/x","source_project_id":null,"target_project_id":5}""")));
        Assert.False(LiveProviderLeftovers.IsLeftover(Read("""{"iid":10,"source_branch":"visualcommit-live/x","target_project_id":5}""")));
        Assert.False(LiveProviderLeftovers.IsLeftover(Read("""{"iid":11,"source_branch":"visualcommit-live/x","source_project_id":5}""")));
        Assert.False(LiveProviderLeftovers.IsLeftover(Read("""{"iid":12,"source_branch":"visualcommit-live/x","source_project_id":"5","target_project_id":5}""")));
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

    [Theory]
    [InlineData("""{"value":[{"name":"refs/heads/visualcommit-live/x","success":true,"updateStatus":"succeeded"},{"name":"refs/heads/visualcommit-live/x-even","success":false,"updateStatus":"staleOldObjectId"}],"count":2}""", "refs/heads/visualcommit-live/x-even: success false, updateStatus staleOldObjectId")]
    [InlineData("""{"value":[{"name":"refs/heads/visualcommit-live/x-even","success":false,"updateStatus":"rejectedByPolicy"},{"name":"refs/heads/visualcommit-live/x","success":true,"updateStatus":"succeeded"}],"count":2}""", "refs/heads/visualcommit-live/x-even: success false, updateStatus rejectedByPolicy")]
    [InlineData("""{"value":[{"success":true},{"success":true,"updateStatus":"forcePushRequired"}],"count":2}""", "refs/heads/visualcommit-live/x-even: success true, updateStatus forcePushRequired")]
    [InlineData("""{"value":[{"name":"refs/heads/visualcommit-live/x","success":true,"updateStatus":"succeeded"}],"count":1}""", "the refs POST answered 1 updates for 2 refs|refs/heads/visualcommit-live/x-even: no update in the answer")]
    [InlineData("""{"value":[{"name":"refs/heads/visualcommit-live/x","success":true},{"name":"refs/heads/visualcommit-live/x","success":true}],"count":2}""", "refs/heads/visualcommit-live/x-even: no update in the answer")]
    [InlineData("""{"count":2}""", "the refs POST answered 200 with no value list, so no update of refs/heads/visualcommit-live/x, refs/heads/visualcommit-live/x-even is shown to have taken")]
    [InlineData("""{"value":[{"name":"refs/heads/visualcommit-live/x","success":true,"updateStatus":"succeeded"},{"name":"refs/heads/visualcommit-live/x-even","success":true}],"count":2}""", "")]
    public void A_refs_post_at_200_fails_each_ref_its_answer_does_not_show_deleted(string body, string failures) =>
        Assert.Equal(
            failures.Split('|', StringSplitOptions.RemoveEmptyEntries),
            AzureDevOpsLiveApi.RefFailures(Answer(200, body), ["refs/heads/visualcommit-live/x", "refs/heads/visualcommit-live/x-even"]));

    [Fact]
    public async Task The_clean_up_s_refs_delete_goes_beside_the_fake_notes_each_update_and_fails_on_one_that_did_not_take_at_200()
    {
        const string branch = "visualcommit-live/20261102-010203Z";
        const string even = branch + LiveSteps.EvenSuffix;
        using var folder = new TempDirectory("live-safety");
        await using var fake = await LiveAzureFake.StartAsync(Project, Repository, branch, even, Cancelled);
        Assert.Equal([LiveLeftovers.HeadsPrefix + branch, LiveLeftovers.HeadsPrefix + even], fake.Tips.Keys.Order(StringComparer.Ordinal));
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"value":[{"name":"refs/heads/{{branch}}","success":true,"updateStatus":"succeeded"},{"name":"refs/heads/{{even}}","success":false,"updateStatus":"staleOldObjectId"}],"count":2}""", Encoding.UTF8, "application/json"),
        });
        using var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen());
        List<(string Name, string ObjectId)> refs = [(LiveLeftovers.HeadsPrefix + branch, "1111111111111111111111111111111111111111"), (LiveLeftovers.HeadsPrefix + even, "2222222222222222222222222222222222222222")];
        var notes = new List<string>();
        var run = new LiveRun(ProviderToken, folder.Combine("run.txt"), notes.Add, new LiveScreen(), HostingProvider.AzureDevOps);
        var problems = new List<string>();

        await AzureDevOpsLiveTests.DeleteRefsAsync(run, api, refs, "Clean-up: the refs POST deleting 2 branches", "deleting the run's branches", problems, Cancelled, beside: true, fake: fake);

        Assert.Equal("POST", Assert.Single(stub.Requests).Method);
        var note = Assert.Single(notes);
        Assert.Contains("its updates: 2 updates: 1 success true, updateStatus succeeded; 2 success false, updateStatus staleOldObjectId; the first's members name, success, updateStatus", note, StringComparison.Ordinal);
        Assert.Contains("; the fake: ", note, StringComparison.Ordinal);
        Assert.Contains("its updates: 2 updates: 1 success true, updateStatus succeeded; 2 success true, updateStatus succeeded", note[note.IndexOf("; the fake: ", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain(branch, note, StringComparison.Ordinal);
        Assert.Empty(await fake.Api.RefsAsync("heads/" + LiveLeftovers.Prefix, Cancelled));
        var problem = Assert.Single(problems);
        Assert.Contains(LiveLeftovers.HeadsPrefix + even + ": success false, updateStatus staleOldObjectId", problem, StringComparison.Ordinal);

        notes.Clear();
        problems.Clear();
        await AzureDevOpsLiveTests.DeleteRefsAsync(run, api, refs, "Clean-up: the refs POST deleting 2 branches", "deleting the run's branches", problems, Cancelled, beside: true);
        Assert.Contains("; the fake: not asked", Assert.Single(notes), StringComparison.Ordinal);
        Assert.Single(problems);

        notes.Clear();
        problems.Clear();
        await AzureDevOpsLiveTests.DeleteRefsAsync(run, api, refs, "Leftover branches deleted in one refs POST", "deleting the leftover branches", problems, Cancelled);
        Assert.DoesNotContain("the fake:", Assert.Single(notes), StringComparison.Ordinal);
        Assert.Contains("deleting the leftover branches answered 200 but did not delete everything: " + LiveLeftovers.HeadsPrefix + even, Assert.Single(problems), StringComparison.Ordinal);
    }

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
/// The second review's findings on the new providers' guard, premises and clean-up (docs/phase-7b-design.md
/// 15.4, 15.5, 15.7): the <c>Host</c> header, the query, D374's read with the token once, a probe without
/// an answer as a broken premise, and each clean-up call on its own; made-up values and stubs only.
/// </summary>
public sealed partial class LiveSafetyTests
{
    private const string LiveBranch = "visualcommit-live/20261102-050000Z";

    private const string LiveEven = LiveBranch + "-even";

    private const string UserIdOfTheRun = "7b0a0201-0000-4a00-8000-00000000fa4e";

    private const string PushedHead = "3333333333333333333333333333333333333333";

    [Theory]
    [InlineData("fernhill-labs.visualstudio.com")]
    [InlineData("vssps.dev.azure.com")]
    [InlineData("dev.azure.com:444")]
    public async Task A_host_header_naming_another_authority_is_refused_before_it_leaves(string host)
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new LiveRequestLog(stub, store, AzureReach()));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://dev.azure.com/{Organization}/_apis/connectionData?api-version=7.1-preview.1"));
        request.Headers.Host = host;
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":made-up-not-a-token")));
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, Cancelled));
        Assert.Empty(stub.Requests);
        Assert.DoesNotContain("visualstudio", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fernhill", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("refused: it sets a Host header, which no request of the app's or the command's to https://dev.azure.com:443 sets", Assert.Single(store.Snapshot()).FailedWith, StringComparison.Ordinal);

        // Hunt 3's M5: one naming the URL's own authority is refused too, since no request of the app's or the command's sets one.
        using var same = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://dev.azure.com/{Organization}/_apis/connectionData?api-version=7.1-preview.1"));
        same.Headers.Host = "DEV.AZURE.COM";
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(same, Cancelled));
        Assert.Empty(stub.Requests);
        using var plain = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://dev.azure.com/{Organization}/_apis/connectionData?api-version=7.1-preview.1"));
        using var answered = await client.SendAsync(plain, Cancelled);
        Assert.Single(stub.Requests);
    }

    [Theory]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/commits/1111111111111111111111111111111111111111/mergebases?otherCommitId=2222222222222222222222222222222222222222&otherRepositoryId=7b0a0011-0000-4a00-8000-00000000fa4e&otherCollectionId=7b0a0012-0000-4a00-8000-00000000fa4e&api-version=7.1")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=Fernhill-Labs&organizationName=other-org")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=Fernhill-Labs&accountName=other-org")]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=Fernhill-Labs&hostId=7b0a0012-0000-4a00-8000-00000000fa4e")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=other-org")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=Fernhill-Labs")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests?api-version=7.1&API-VERSION=7.0")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/items?path=%2F&project=other-project&api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/connectionData?connectOptions=1&lastChangeId=-1")]
    public void The_guard_refuses_a_query_that_names_another_repository_or_organization_or_repeats_a_parameter(string url)
    {
        var refusal = AzureReach().RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.Basic);
        Assert.NotNull(refusal);
        Assert.Contains("query parameter", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("Fernhill", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("other-org", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://dev.azure.com/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?organizationName=fernhill-labs")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests?searchCriteria.status=active&$top=100&api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests?searchCriteria.status=active&api-version=7.1&$top=1&$skip=100")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?filter=heads%2Fvisualcommit-live%2F&api-version=7.1")]
    public void The_guard_admits_the_query_parameters_the_app_and_the_command_send(string url) =>
        Assert.Null(AzureReach().RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.Basic));

    [Fact]
    public void D374_s_read_with_the_token_is_admitted_once_per_run()
    {
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1");
        var upper = new Uri($"https://dev.azure.com/{MadeUpOrganization.ToUpperInvariant()}/_apis/connectionData?api-version=7.1-preview.1");
        var reach = AzureReach();
        reach.Answered(path, LiveCredentialForm.None, 404);
        Assert.Null(reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));
        Assert.NotNull(reach.RefusalOf(HttpMethod.Get, upper, LiveCredentialForm.Basic));
        Assert.Null(reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.None));

        var answered = AzureReach();
        answered.Answered(path, LiveCredentialForm.None, 404);
        answered.Answered(path, LiveCredentialForm.Basic, 200);
        Assert.NotNull(answered.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));
    }

    [Theory]
    [InlineData(new[] { 200, 404 }, false)]
    [InlineData(new[] { 302, 404 }, false)]
    [InlineData(new[] { 401, 404 }, false)]
    [InlineData(new[] { 404, 200 }, false)]
    [InlineData(new[] { 404, 302, 404 }, false)]
    [InlineData(new[] { 404, 404 }, true)]
    public void D374_s_read_with_the_token_stays_barred_once_a_read_without_one_answered_otherwise(int[] answers, bool basic)
    {
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1");
        var reach = AzureReach();
        foreach (var status in answers)
        {
            Assert.Null(reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.None));
            reach.Answered(path, LiveCredentialForm.None, status);
        }

        Assert.Equal(basic, reach.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic) is null);
    }

    [Fact]
    public void D374_s_read_with_the_token_stays_barred_once_a_read_without_one_was_refused_or_unanswered()
    {
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1");
        var unanswered = AzureReach();
        Assert.Null(unanswered.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.None));
        unanswered.Failed(path, LiveCredentialForm.None);
        unanswered.Answered(path, LiveCredentialForm.None, 404);
        Assert.NotNull(unanswered.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));

        var refused = AzureReach();
        Assert.NotNull(refused.RefusalOf(HttpMethod.Get, new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1&hostId=x"), LiveCredentialForm.None));
        refused.Answered(path, LiveCredentialForm.None, 404);
        Assert.NotNull(refused.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));

        var failedWithToken = AzureReach();
        failedWithToken.Answered(path, LiveCredentialForm.None, 404);
        Assert.Null(failedWithToken.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));
        failedWithToken.Failed(path, LiveCredentialForm.Basic);
        Assert.NotNull(failedWithToken.RefusalOf(HttpMethod.Get, path, LiveCredentialForm.Basic));
    }

    [Fact]
    public async Task D374_s_read_with_the_token_is_never_sent_after_a_200_then_a_404_without_one()
    {
        var store = new LiveRequestLog.Store();
        var answers = new Queue<HttpStatusCode>([HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound]);
        var stub = new StubHandler(_ => new HttpResponseMessage(answers.Dequeue()) { Content = new StringContent("{}") });
        using var client = new HttpClient(new LiveRequestLog(stub, store, AzureReach()));
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1");
        HttpRequestMessage Read(bool basic)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (basic)
            {
                request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":made-up-not-a-token")));
            }

            return request;
        }

        using (var first = await client.SendAsync(Read(basic: false), Cancelled))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Read(basic: true), Cancelled));
        using (var second = await client.SendAsync(Read(basic: false), Cancelled))
        {
            Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Read(basic: true), Cancelled));
        Assert.Equal(2, stub.Requests.Count);
        Assert.All(stub.Requests, request => Assert.Null(request.Authorization));
    }

    [Fact]
    public async Task A_refused_request_s_problems_and_lines_name_its_origin_and_never_its_path()
    {
        using var folder = new TempDirectory("live-safety");
        var reach = AzureReach();
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new LiveRequestLog(stub, store, reach));
        foreach (var url in new[]
        {
            $"https://{Organization.ToLowerInvariant()}.visualstudio.com/other-team-project/_apis/git/repositories/payroll-service/pullrequests",
            $"http://dev.azure.com/{Organization}/other-team-project/_apis/git/repositories/payroll-service",
            $"https://dev.azure.com/{Organization}/other-team-project/_apis/git/repositories/payroll-service",
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":made-up")));
            await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, Cancelled));
        }

        Assert.Empty(stub.Requests);
        var records = store.Snapshot();
        Assert.All(records, record => Assert.True(record.Refused));
        var problems = LiveRequestLog.ReachProblems(records, reach);
        Assert.Equal(3, problems.Count);
        Assert.All(problems, problem => Assert.StartsWith("The guard refused a request (D352, D371): GET ", problem, StringComparison.Ordinal));
        var lines = problems.Concat(records.Select(record => record.DescribeForRun())).Concat(records.Select(record => record.DescribeOrigin())).ToList();

        var notes = new List<string>();
        var run = new LiveRun(ProviderToken, folder.Combine("run.txt"), notes.Add, new LiveScreen(), HostingProvider.AzureDevOps);
        Assert.Throws<XunitException>(() => LiveSteps.Finish(run, store, reach, [], null, signInStarted: DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Contains(notes, note => note.StartsWith("Problem: A request was sent before the sign-in: GET ", StringComparison.Ordinal));
        foreach (var line in lines.Concat(notes))
        {
            foreach (var segment in new[] { "other-team-project", "payroll-service", "_apis", "pullrequests" })
            {
                Assert.DoesNotContain(segment, line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/forks/7b0a0012-0000-4a00-8000-00000000fa4e?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/forkSyncRequests?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/importRequests?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/7b0a0011-0000-4a00-8000-00000000fa4e")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests/3/statuses?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests/7b0a0011-0000-4a00-8000-00000000fa4e")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs/heads?api-version=7.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/items?api-version=7.1")]
    public void The_guard_admits_below_the_repository_only_what_the_app_and_the_command_send(string url)
    {
        var refusal = AzureReach().RefusalOf(HttpMethod.Get, new Uri(url), LiveCredentialForm.Basic);
        Assert.NotNull(refusal);
        Assert.DoesNotContain("visualcommit-sandbox", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.Null(AzureReach().RefusalOf(HttpMethod.Get, new Uri("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/PullRequests/12?api-version=7.1"), LiveCredentialForm.Basic));
        Assert.Null(AzureReach().RefusalOf(HttpMethod.Get, new Uri("https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1"), LiveCredentialForm.Basic));
    }

    [Fact]
    public async Task D374_s_read_with_the_token_without_an_answer_is_a_problem()
    {
        using var folder = new TempDirectory("live-safety");
        var notes = new List<string>();
        var stub = new StubHandler(request => request.Headers.Authorization is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"message":"TF400813: made-up 404"}""", Encoding.UTF8, "application/json") }
            : throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error"));
        using var api = AzureApi(stub);
        var run = new LiveRun(ProviderToken, folder.Combine("run.txt"), notes.Add, new LiveScreen(), HostingProvider.AzureDevOps);
        var problems = new List<string>();
        await AzureDevOpsLiveTests.ProbeMadeUpOrganizationAsync(run, api, null, [], MadeUpOrganization, problems);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Contains("D374's read with the token had no answer", Assert.Single(problems), StringComparison.Ordinal);

        var silent = new StubHandler(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error"));
        using var second = AzureApi(silent);
        problems.Clear();
        await AzureDevOpsLiveTests.ProbeMadeUpOrganizationAsync(run, second, null, [], MadeUpOrganization, problems);
        Assert.Single(silent.Requests);
        Assert.Contains("D374's read without a credential had no answer", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task D373_s_made_up_probes_without_an_answer_are_each_a_problem()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = new StubHandler(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error"));
        using var api = AzureApi(stub);
        var problems = new List<string>();
        await AzureTests(folder, out _).ProbeMadeUpAsync(api, null, "git/2.56.0", problems);
        Assert.Equal(8, stub.Requests.Count);
        Assert.Equal(8, problems.Count);
        Assert.All(problems, problem => Assert.Contains("had no answer, so D373's premise is not shown", problem, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Azure_DevOps_duplicate_and_missing_branch_without_an_answer_are_each_a_problem()
    {
        using var folder = new TempDirectory("live-safety");
        var abandoned = false;
        var stub = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Post)
            {
                throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error");
            }

            if (request.Method == HttpMethod.Patch)
            {
                abandoned = true;
                return Json(HttpStatusCode.OK, """{"status":"abandoned"}""");
            }

            if (url.Contains("/refs?", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, url.Contains("pull%2F7%2Fmerge", StringComparison.Ordinal) && !abandoned
                    ? """{"value":[{"name":"refs/pull/7/merge","objectId":"4444444444444444444444444444444444444444"}],"count":1}"""
                    : """{"value":[],"count":0}""");
            }

            return Json(HttpStatusCode.OK, $$$"""{"pullRequestId":7,"status":"active","createdBy":{"id":"{{{UserIdOfTheRun}}}"},"lastMergeSourceCommit":{"commitId":"{{{PushedHead}}}"}}""");
        });
        using var api = AzureApi(stub);
        var problems = new List<string>();
        var made = new LiveMade(LiveBranch, LiveEven, "main", "made-up title") { Number = 7, UserId = UserIdOfTheRun, Head = PushedHead };
        await AzureTests(folder, out var notes).ProbeCreateAsync(api, null, made, 7, problems);
        Assert.Equal(3, stub.Requests.Count(request => request.Method == "POST"));
        Assert.Equal(2, problems.Count);
        Assert.Contains("The duplicate create had no answer", problems[0], StringComparison.Ordinal);
        Assert.Contains("The create from a missing branch had no answer", problems[1], StringComparison.Ordinal);
        Assert.Contains(notes, note => note.StartsWith("Nothing ahead: skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GitLab_s_duplicate_and_missing_branch_without_an_answer_are_each_a_problem()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = new StubHandler(request => request.Method == HttpMethod.Post
            ? throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error")
            : Json(HttpStatusCode.OK, $$"""{"iid":7,"draft":true,"title":"Draft: made-up title","sha":"{{PushedHead}}"}"""));
        using var api = GitLabApi(stub);
        var problems = new List<string>();
        var made = new LiveMade(LiveBranch, LiveEven, "main", "made-up title") { Number = 7, Head = PushedHead };
        await GitLabTests(folder, out _).ProbeCreateAsync(api, null, made, 7, problems);
        Assert.Equal(3, stub.Requests.Count(request => request.Method == "POST"));
        Assert.Equal(2, problems.Count);
        Assert.Contains("The duplicate create had no answer", problems[0], StringComparison.Ordinal);
        Assert.Contains("The create from a missing branch had no answer", problems[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Azure_DevOps_clean_up_goes_on_after_a_call_that_throws_and_still_sends_the_refs_post()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = AzureCleanUpStub();
        using var api = AzureApi(stub);
        var problems = new List<string>();
        await AzureTests(folder, out _).CleanUpAsync(api, null, [LiveBranch, LiveEven], problems);
        Assert.Equal(["GET pullrequests", "PATCH 11", "PATCH 14", "GET refs", "POST refs"], [.. stub.Requests.Select(AzureCall)]);
        Assert.Contains("abandoning !11: LiveServiceException", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Azure_DevOps_leftovers_go_on_after_a_call_that_throws_and_still_send_the_refs_post()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = AzureCleanUpStub();
        using var api = AzureApi(stub);
        var problems = new List<string>();
        await AzureTests(folder, out _).RemoveLeftoversAsync(api, problems, Cancelled);
        Assert.Equal(["GET pullrequests", "PATCH 11", "PATCH 14", "GET refs", "POST refs"], [.. stub.Requests.Select(AzureCall)]);
        Assert.Contains("abandoning !11: LiveServiceException", Assert.Single(problems), StringComparison.Ordinal);

        var unlisted = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/pullrequests", StringComparison.Ordinal)
            ? throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error")
            : AzureCleanUpAnswer(request));
        using var second = AzureApi(unlisted);
        problems.Clear();
        await AzureTests(folder, out _).RemoveLeftoversAsync(second, problems, Cancelled);
        Assert.Equal(["GET pullrequests", "GET refs", "POST refs"], [.. unlisted.Requests.Select(AzureCall)]);
        Assert.Single(problems);
    }

    [Fact]
    public async Task GitLab_s_clean_up_goes_on_after_a_call_that_throws_and_still_deletes_both_branches()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = GitLabCleanUpStub();
        using var api = GitLabApi(stub);
        var problems = new List<string>();
        await GitLabTests(folder, out _).CleanUpAsync(api, [LiveBranch, LiveEven], problems);
        Assert.Equal(["GET merge_requests", "PUT 21", "DELETE 21", "PUT 22", "DELETE 22", "DELETE branch", "DELETE branch"], [.. stub.Requests.Select(GitLabCall)]);
        Assert.Contains("closing !21: LiveServiceException", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitLab_s_leftovers_go_on_after_a_call_that_throws_and_still_delete_the_branches()
    {
        using var folder = new TempDirectory("live-safety");
        var stub = GitLabCleanUpStub();
        using var api = GitLabApi(stub);
        var problems = new List<string>();
        await GitLabTests(folder, out _).RemoveLeftoversAsync(api, problems, Cancelled);
        Assert.Equal(["GET merge_requests", "PUT 21", "DELETE 21", "PUT 22", "DELETE 22", "GET branches", "DELETE branch", "DELETE branch"], [.. stub.Requests.Select(GitLabCall)]);
        Assert.Contains("closing !21: LiveServiceException", Assert.Single(problems), StringComparison.Ordinal);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static AzureDevOpsLiveApi AzureApi(HttpMessageHandler stub) =>
        new(new LiveAzureSandbox(Organization, Project, Repository, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), AzureReach()), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), new LiveScreen(), madeUpOrganization: MadeUpOrganization);

    private static GitLabLiveApi GitLabApi(HttpMessageHandler stub) =>
        new(new LiveGitLabSandbox("ada", Project, ProviderToken), new LiveRequestLog(stub, new LiveRequestLog.Store(), LiveReach.GitLab()), VisualCommit.Hosting.GitLab.GitLabEndpoints.GitLabCom.ApiBase, TimeSpan.FromSeconds(5), new LiveScreen());

    private static AzureDevOpsLiveTests AzureTests(TempDirectory folder, out List<string> notes)
    {
        var kept = new List<string>();
        notes = kept;
        return new AzureDevOpsLiveTests(new AzureDevOpsLiveRun(ProviderToken, folder.Combine("azure-run.txt"), kept.Add, new LiveScreen()));
    }

    private static GitLabLiveTests GitLabTests(TempDirectory folder, out List<string> notes)
    {
        var kept = new List<string>();
        notes = kept;
        return new GitLabLiveTests(new GitLabLiveRun(ProviderToken, folder.Combine("gitlab-run.txt"), kept.Add, new LiveScreen()));
    }

    /// <summary>Azure DevOps over a stub: the run's !11 and !14 active, among another; the first abandon throws; the refs read names both branches; the refs POST takes both.</summary>
    private static StubHandler AzureCleanUpStub()
    {
        var first = true;
        return new StubHandler(request =>
        {
            if (request.Method == HttpMethod.Patch && first)
            {
                first = false;
                throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error");
            }

            return AzureCleanUpAnswer(request);
        });
    }

    private static HttpResponseMessage AzureCleanUpAnswer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Patch)
        {
            return Json(HttpStatusCode.OK, """{"status":"abandoned"}""");
        }

        if (path.EndsWith("/pullrequests", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, $$"""{"value":[{"pullRequestId":11,"sourceRefName":"refs/heads/{{LiveBranch}}","status":"active"},{"pullRequestId":12,"sourceRefName":"refs/heads/feature","status":"active"},{"pullRequestId":14,"sourceRefName":"refs/heads/{{LiveEven}}","status":"active"}],"count":3}""");
        }

        return request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, $$"""{"value":[{"name":"refs/heads/{{LiveBranch}}","success":true,"updateStatus":"succeeded"},{"name":"refs/heads/{{LiveEven}}","success":true,"updateStatus":"succeeded"}],"count":2}""")
            : Json(HttpStatusCode.OK, $$"""{"value":[{"name":"refs/heads/{{LiveBranch}}","objectId":"1111111111111111111111111111111111111111"},{"name":"refs/heads/{{LiveEven}}","objectId":"2222222222222222222222222222222222222222"}],"count":2}""");
    }

    private static string AzureCall((string Method, string Url, string? Authorization, string? Accept, string? Version, string? UserAgent) request)
    {
        var path = new Uri(request.Url).AbsolutePath;
        return request.Method == "PATCH" ? "PATCH " + path[(path.LastIndexOf('/') + 1)..]
            : path.EndsWith("/refs", StringComparison.Ordinal) ? $"{request.Method} refs"
            : $"{request.Method} pullrequests";
    }

    /// <summary>GitLab over a stub: the run's !21 and !22 opened; the first close throws; every other call answers as GitLab does.</summary>
    private static StubHandler GitLabCleanUpStub()
    {
        var first = true;
        return new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put)
            {
                if (first)
                {
                    first = false;
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "made-up connection error");
                }

                return Json(HttpStatusCode.OK, """{"state":"closed"}""");
            }

            if (request.Method == HttpMethod.Delete)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return path.EndsWith("/merge_requests", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, $$"""[{"iid":21,"source_branch":"{{LiveBranch}}","source_project_id":5,"target_project_id":5},{"iid":22,"source_branch":"{{LiveEven}}","source_project_id":5,"target_project_id":5}]""")
                : Json(HttpStatusCode.OK, $$"""[{"name":"{{LiveBranch}}"},{"name":"{{LiveEven}}"}]""");
        });
    }

    private static string GitLabCall((string Method, string Url, string? Authorization, string? Accept, string? Version, string? UserAgent) request)
    {
        var path = new Uri(request.Url).AbsolutePath;
        return path.Contains("/repository/branches/", StringComparison.Ordinal) ? $"{request.Method} branch"
            : path.EndsWith("/repository/branches", StringComparison.Ordinal) ? $"{request.Method} branches"
            : path.EndsWith("/merge_requests", StringComparison.Ordinal) ? $"{request.Method} merge_requests"
            : $"{request.Method} {path[(path.LastIndexOf('/') + 1)..]}";
    }
}

/// <summary>
/// Hunt 3's findings on the new providers' guard (docs/phase-7b-design.md 15.4): the method (M4), any
/// <c>Host</c> header (M5), a query parameter named for a token and GitLab's underscore headers (M6),
/// and D374's bar after a refusal by any check (M7); each request sent through the log over a stub,
/// which sees nothing the guard refuses. Made-up values only.
/// </summary>
public sealed partial class LiveSafetyTests
{
    private const string GitLabProject = "https://gitlab.com/api/v4/projects/ada%2Fvisualcommit-sandbox";

    /// <summary>Sends one request through <paramref name="reach"/>'s log over a stub that answers <paramref name="status"/>; the stub's requests and the store.</summary>
    private static async Task<(StubHandler Stub, LiveRequestLog.Store Store, HttpRequestException? Refused)> SendThroughAsync(LiveReach reach, HttpRequestMessage request, HttpStatusCode status = HttpStatusCode.OK)
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("{}") });
        using var client = new HttpClient(new LiveRequestLog(stub, store, reach));
        try
        {
            using var answer = await client.SendAsync(request, Cancelled);
            return (stub, store, null);
        }
        catch (HttpRequestException refused)
        {
            return (stub, store, refused);
        }
    }

    private static HttpRequestMessage Request(string method, string url, string? scheme)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (scheme is "Basic")
        {
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":made-up-not-a-token")));
        }
        else if (scheme is "Bearer")
        {
            request.Headers.Authorization = new("Bearer", "made-up-not-a-token");
        }

        return request;
    }

    // ---- M4: the method --------------------------------------------------------------------------

    [Theory]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories?api-version=7.1")]
    [InlineData("DELETE", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox?api-version=7.1")]
    [InlineData("PATCH", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox?api-version=7.1")]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/_apis/resourceAreas/79134c72-4a58-4b42-976c-04e7115f32bf")]
    [InlineData("PUT", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests?api-version=7.1")]
    [InlineData("DELETE", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests/3?api-version=7.1")]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/pullrequests/3?api-version=7.1")]
    [InlineData("PATCH", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1")]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_git/visualcommit-sandbox/git-receive-pack")]
    [InlineData("HEAD", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox?api-version=7.1")]
    [InlineData("get", "https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1")]
    public async Task Azure_DevOps_guard_refuses_a_method_neither_the_app_nor_the_command_sends_to_the_path(string method, string url)
    {
        var (stub, store, refused) = await SendThroughAsync(AzureReach(), Request(method, url, "Basic"));
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        var failed = Assert.Single(store.Snapshot()).FailedWith!;
        foreach (var text in new[] { failed, refused.Message })
        {
            Assert.Contains(method, text, StringComparison.Ordinal);
            Assert.Contains("https://dev.azure.com", text, StringComparison.Ordinal);
            Assert.DoesNotContain("visualcommit-sandbox", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fernhill", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("_apis", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("POST", "https://dev.azure.com/vc-none-0123456789ab/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("DELETE", "https://dev.azure.com/vc-none-0123456789ab/_apis/connectionData")]
    public async Task D374_s_path_takes_get_alone_and_another_method_without_a_credential_bars_the_read_with_the_token(string method, string url)
    {
        var reach = AzureReach();
        var path = new Uri($"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1");
        reach.Answered(path, LiveCredentialForm.None, 404);
        var (stub, _, refused) = await SendThroughAsync(reach, Request(method, url, null));
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        var (basic, _, barred) = await SendThroughAsync(reach, Request("GET", path.AbsoluteUri, "Basic"));
        Assert.NotNull(barred);
        Assert.Empty(basic.Requests);
    }

    [Theory]
    [InlineData("DELETE", GitLabProject)]
    [InlineData("PUT", GitLabProject)]
    [InlineData("POST", "https://gitlab.com/api/v4/projects")]
    [InlineData("POST", "https://gitlab.com/api/v4/user")]
    [InlineData("POST", GitLabProject + "/repository/branches?branch=x&ref=main")]
    [InlineData("GET", GitLabProject + "/repository/branches/main")]
    [InlineData("PUT", GitLabProject + "/repository/branches/visualcommit-live%2Fx")]
    [InlineData("DELETE", GitLabProject + "/repository/branches")]
    [InlineData("PUT", GitLabProject + "/merge_requests/3/merge")]
    [InlineData("POST", GitLabProject + "/merge_requests/3")]
    [InlineData("DELETE", GitLabProject + "/merge_requests")]
    [InlineData("PATCH", GitLabProject + "/merge_requests/3")]
    [InlineData("GET", GitLabProject + "/members")]
    [InlineData("POST", GitLabProject + "/fork")]
    [InlineData("DELETE", "https://gitlab.com/api/v4/personal_access_tokens/self")]
    [InlineData("GET", "https://gitlab.com/api/v4/personal_access_tokens")]
    [InlineData("get", "https://gitlab.com/api/v4/user")]
    public async Task GitLab_s_guard_refuses_a_path_or_method_neither_the_app_nor_the_command_sends(string method, string url)
    {
        var (stub, store, refused) = await SendThroughAsync(LiveReach.GitLab(), Request(method, url, "Bearer"));
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        var failed = Assert.Single(store.Snapshot()).FailedWith!;
        Assert.Contains("https://gitlab.com", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("visualcommit-sandbox", failed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/v4/projects", failed, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_admitted_path_takes_the_methods_the_app_and_the_command_send_it()
    {
        var azure = AzureReach();
        var repository = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repository}";
        foreach (var (method, url) in new[]
        {
            ("GET", $"https://dev.azure.com/{Organization}/_apis/connectionData?api-version=7.1-preview.1"),
            ("GET", $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories?api-version=7.1&$top=100&$skip=0"),
            ("GET", repository + "?api-version=7.1"),
            ("GET", repository + "/pullrequests?searchCriteria.status=active&$top=100&api-version=7.1"),
            ("POST", repository + "/pullrequests?api-version=7.1"),
            ("GET", repository + "/pullrequests/7?api-version=7.1"),
            ("PATCH", repository + "/pullrequests/7?api-version=7.1"),
            ("GET", repository + "/refs?filter=heads%2Fvisualcommit-live%2F&api-version=7.1"),
            ("POST", repository + "/refs?api-version=7.1"),
            ("GET", $"https://dev.azure.com/{Organization}/{Project}/_git/{Repository}/info/refs?service=git-upload-pack"),
            ("GET", $"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1"),
        })
        {
            var form = url.Contains(MadeUpOrganization, StringComparison.Ordinal) ? LiveCredentialForm.None : LiveCredentialForm.Basic;
            Assert.Null(azure.RefusalOf(new HttpMethod(method), new Uri(url), form));
        }

        var gitlab = LiveReach.GitLab();
        foreach (var (method, url) in new[]
        {
            ("GET", "https://gitlab.com/api/v4/user"),
            ("GET", "https://gitlab.com/api/v4/personal_access_tokens/self"),
            ("GET", "https://gitlab.com/api/v4/projects?membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100&page=2"),
            ("GET", GitLabProject),
            ("GET", "https://gitlab.com/api/v4/projects/123"),
            ("GET", GitLabProject + "/merge_requests?state=opened&per_page=100"),
            ("POST", "https://gitlab.com/api/v4/projects/123/merge_requests"),
            ("GET", GitLabProject + "/merge_requests/7"),
            ("PUT", GitLabProject + "/merge_requests/7"),
            ("DELETE", GitLabProject + "/merge_requests/7"),
            ("GET", GitLabProject + "/repository/branches?search=%5Evisualcommit-live%2F&per_page=100"),
            ("DELETE", GitLabProject + "/repository/branches/visualcommit-live%2F20261102-010203Z"),
        })
        {
            Assert.Null(gitlab.RefusalOf(new HttpMethod(method), new Uri(url), LiveCredentialForm.Bearer));
        }
    }

    // ---- M5: any Host header -----------------------------------------------------------------------

    [Theory]
    [InlineData("elsewhere.example:99999")]
    [InlineData("elsewhere.example, dev.azure.com")]
    [InlineData("dev.azure.com")]
    public async Task A_host_header_is_refused_whatever_it_holds_judged_on_the_raw_headers(string host)
    {
        var request = Request("GET", $"https://dev.azure.com/{Organization}/_apis/connectionData?api-version=7.1-preview.1", "Basic");
        request.Headers.TryAddWithoutValidation("Host", host);
        var (stub, store, refused) = await SendThroughAsync(AzureReach(), request);
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        Assert.DoesNotContain("elsewhere", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("elsewhere", Assert.Single(store.Snapshot()).FailedWith!, StringComparison.Ordinal);

        var gitlab = Request("GET", "https://gitlab.com/api/v4/user", "Bearer");
        gitlab.Headers.TryAddWithoutValidation("Host", host.Replace("dev.azure.com", "gitlab.com", StringComparison.Ordinal));
        var (gitlabStub, _, gitlabRefused) = await SendThroughAsync(LiveReach.GitLab(), gitlab);
        Assert.NotNull(gitlabRefused);
        Assert.Empty(gitlabStub.Requests);
    }

    // ---- M6: a parameter named for a token, GitLab's underscore headers -----------------------------

    [Theory]
    [InlineData("https://gitlab.com/api/v4/user?bearer_token=made-up", null, LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/projects?membership=true;private_token=made-up", null, LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/user?private_token[]=made-up", null, LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/user?OAuth_Token=made-up", null, LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/user?x=1&my%5Ftoken=made-up", null, LiveCredentialForm.QueryToken)]
    [InlineData("https://gitlab.com/api/v4/user", "PRIVATE_TOKEN", LiveCredentialForm.PrivateToken)]
    [InlineData("https://gitlab.com/api/v4/user", "JOB_TOKEN", LiveCredentialForm.JobToken)]
    [InlineData("https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1;private_token=made-up", null, LiveCredentialForm.QueryToken)]
    public async Task A_parameter_named_for_a_token_and_gitlab_s_underscore_headers_are_credentials_the_guard_refuses(string url, string? header, LiveCredentialForm form)
    {
        var request = Request("GET", url, null);
        if (header is not null)
        {
            request.Headers.TryAddWithoutValidation(header, "made-up");
        }

        Assert.Equal(form, LiveReach.FormOf(request));
        var (stub, _, refused) = await SendThroughAsync(url.Contains("gitlab", StringComparison.Ordinal) ? LiveReach.GitLab() : AzureReach(), request);
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
    }

    // ---- M7: D374's bar after a refusal by any check ------------------------------------------------

    [Fact]
    public async Task D374_s_read_without_a_credential_that_the_host_check_refuses_bars_the_read_with_the_token()
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        using var client = new HttpClient(new LiveRequestLog(stub, store, AzureReach()));
        var path = $"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1";
        using (var first = await client.SendAsync(Request("GET", path, null), Cancelled))
        {
            Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        }

        var hosted = Request("GET", path, null);
        hosted.Headers.Host = "elsewhere.example";
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(hosted, Cancelled));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Request("GET", path, "Basic"), Cancelled));
        var only = Assert.Single(stub.Requests);
        Assert.Null(only.Authorization);
    }

    // ---- The verification of hunt 3's fixes: the guard ----------------------------------------------

    [Theory]
    [InlineData("PRIVATE-TOKEN", LiveCredentialForm.PrivateToken)]
    [InlineData("PRIVATE_TOKEN", LiveCredentialForm.PrivateToken)]
    [InlineData("JOB-TOKEN", LiveCredentialForm.JobToken)]
    [InlineData("JOB_TOKEN", LiveCredentialForm.JobToken)]
    public async Task A_token_header_among_the_content_s_headers_is_read_as_its_form_and_refused(string header, LiveCredentialForm form)
    {
        var request = Request("POST", GitLabProject + "/merge_requests", null);
        Assert.True(request.Content!.Headers.TryAddWithoutValidation(header, "made-up"));
        Assert.Equal(form, LiveReach.FormOf(request));
        var (stub, store, refused) = await SendThroughAsync(LiveReach.GitLab(), request);
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        Assert.Equal(form, Assert.Single(store.Snapshot()).Form);
    }

    [Theory]
    [InlineData("https://gitlab.com/api/v4/user?[private_token]=made-up")]
    [InlineData("https://gitlab.com/api/v4/user?%5Bprivate_token%5D=made-up")]
    [InlineData("https://gitlab.com/api/v4/user?private_token]=made-up")]
    [InlineData("https://gitlab.com/api/v4/user?private_token%5D=made-up")]
    [InlineData("https://gitlab.com/api/v4/user?state=opened&[access_token]x=made-up")]
    public async Task A_parameter_whose_name_holds_token_anywhere_is_a_query_token_the_guard_refuses(string url)
    {
        var request = Request("GET", url, null);
        Assert.Equal(LiveCredentialForm.QueryToken, LiveReach.FormOf(request));
        var (stub, _, refused) = await SendThroughAsync(LiveReach.GitLab(), request);
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public void None_of_the_parameters_the_app_and_the_command_send_is_named_for_a_token()
    {
        foreach (var name in new[] { "membership", "archived", "order_by", "sort", "per_page", "page", "simple", "state", "search", "api-version", "$top", "$skip", "searchCriteria.status", "filter", "service", "organizationName" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://gitlab.com/api/v4/projects?{name}=x");
            Assert.Equal(LiveCredentialForm.None, LiveReach.FormOf(request));
        }
    }

    [Theory]
    [InlineData("GET", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox?api-version=7.1", "X-HTTP-Method-Override", "DELETE", false)]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1", "X-HTTP-Method-Override", "PATCH", false)]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1", "X-HTTP-Method-Override", "PATCH", true)]
    [InlineData("GET", "https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", "X-HTTP-Method", "DELETE", false)]
    [InlineData("GET", "https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", "x-method-override", "PUT", false)]
    [InlineData("POST", GitLabProject + "/merge_requests", "X-HTTP-Method-Override", "DELETE", false)]
    [InlineData("POST", GitLabProject + "/merge_requests", "X-HTTP-Method", "DELETE", true)]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1", "X_HTTP_METHOD_OVERRIDE", "PATCH", false)]
    [InlineData("POST", "https://dev.azure.com/Fernhill-Labs/visualcommit-sandbox/_apis/git/repositories/visualcommit-sandbox/refs?api-version=7.1", "x_http_method", "PATCH", true)]
    [InlineData("GET", "https://dev.azure.com/Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", "X_METHOD_OVERRIDE", "PUT", false)]
    [InlineData("POST", GitLabProject + "/merge_requests", "X-HTTP_Method-Override", "DELETE", false)]
    [InlineData("POST", GitLabProject + "/merge_requests", "X_Method-Override", "DELETE", true)]
    public async Task A_method_override_header_is_refused_before_it_leaves_naming_the_origin_alone(string method, string url, string header, string value, bool inContent)
    {
        var gitlab = url.Contains("gitlab", StringComparison.Ordinal);
        var request = Request(method, url, gitlab ? "Bearer" : "Basic");
        System.Net.Http.Headers.HttpHeaders headers = inContent ? request.Content!.Headers : request.Headers;
        Assert.True(headers.TryAddWithoutValidation(header, value));
        var (stub, store, refused) = await SendThroughAsync(gitlab ? LiveReach.GitLab() : AzureReach(), request);
        Assert.NotNull(refused);
        Assert.Empty(stub.Requests);
        foreach (var text in new[] { refused.Message, Assert.Single(store.Snapshot()).FailedWith! })
        {
            Assert.Contains(gitlab ? "https://gitlab.com" : "https://dev.azure.com", text, StringComparison.Ordinal);
            Assert.DoesNotContain("visualcommit-sandbox", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fernhill", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("https://dev.azure.com/vc-none-0123456789ab/_apis/connectionData/?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/vc-none-0123456789ab/_apis//connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com//vc-none-0123456789ab/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/vc-none-0123456789ab/_apis/connectionData/x?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/_apis/connectionData?organizationName=VC-NONE-0123456789AB&api-version=7.1-preview.1")]
    [InlineData("https://vc-none-0123456789ab.visualstudio.com/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/Fernhill-Labs/vc-none-0123456789ab/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/%2576c-none-0123456789ab/_apis/connectionData?api-version=7.1-preview.1")]
    [InlineData("https://dev.azure.com/vc-none-0123456789ab%2F_apis/connectionData?api-version=7.1-preview.1")]
    public async Task D374_s_read_without_a_credential_refused_for_a_url_that_names_the_made_up_organization_anywhere_bars_the_read_with_the_token(string refusedUrl)
    {
        var store = new LiveRequestLog.Store();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        using var client = new HttpClient(new LiveRequestLog(stub, store, AzureReach()));
        var path = $"https://dev.azure.com/{MadeUpOrganization}/_apis/connectionData?api-version=7.1-preview.1";
        using (var first = await client.SendAsync(Request("GET", path, null), Cancelled))
        {
            Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Request("GET", refusedUrl, null), Cancelled));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Request("GET", path, "Basic"), Cancelled));
        Assert.Null(Assert.Single(stub.Requests).Authorization);
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
        foreach (var glued in new[] { "jane.roe@corp.test@example.com", "jane.roe%40corp.test@example.com", "jane.roe%2540corp.test@example.com", "jane.roe＠corp.test@example.com" })
        {
            var fresh = Screen();
            Assert.Equal("author <an address>", fresh.Apply("author " + glued));
            Assert.Equal("an address", fresh.SurvivorIn("author " + glued));
            Assert.False(LiveScreen.IsKept(glued));
        }

        Assert.True(LiveScreen.IsKept("ada+commits@example.com"));
        Assert.True(LiveScreen.IsKept("ada%2Bcommits@example.com"));
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
        var context = SignInContext(OtherHostId);
        Assert.Equal(125, context.Length);
        var location = SignInLocation(context);
        var body = ObjectMoved(location);
        screen.LearnFromAnswer([new("Location", [location]), new("X-TFS-FedAuthRedirect", [location])], body);
        Assert.Equal(2, screen.Counts.Ids);
        var screened = screen.Apply($"Location {location}; body {body}; ids {InstanceId} {OtherHostId}");
        Assert.DoesNotContain(InstanceId, screened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OtherHostId, screened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reply_to", screened, StringComparison.Ordinal);
        Assert.DoesNotContain("context=", screened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hid=", screened, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/_signin?<the sign-in query>", screened, StringComparison.Ordinal);
        Assert.Equal(screened, screen.Apply(screened));
        Assert.Null(screen.SurvivorIn(screened));
        Assert.Equal("https://spsprodfake.vssps.visualstudio.com/A<the organization's id>/_apis/Identities/x", screen.Apply($"https://spsprodfake.vssps.visualstudio.com/A{OtherHostId}/_apis/Identities/x"));
    }

    [Fact]
    public void A_302_after_the_identity_read_reaches_run_txt_without_its_sign_in_query_or_either_host_id()
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var screen = Screen();
        using (var identity = JsonDocument.Parse($$$"""{"authenticatedUser":{"id":"{{{UserId}}}"},"instanceId":"{{{InstanceId}}}"}"""))
        {
            screen.Learn(identity.RootElement);
        }

        var context = SignInContext(OtherHostId);
        var location = SignInLocation(context);
        var body = ObjectMoved(location);
        screen.LearnFromAnswer([new("Location", [location])], body);
        var output = new List<string>();
        var run = new LiveRun("made-up-token-for-the-screen-0123456789", file, output.Add, screen, HostingProvider.AzureDevOps);
        run.Note($"Azure DevOps' answer to connectionData with a made-up value: 302 Found; Location {location}; the fake: same. {body}");
        run.Dispose();
        var text = File.ReadAllText(file);
        foreach (var hidden in new[] { InstanceId, OtherHostId, context, "context=", "hid=", "reply_to", "Fernhill" })
        {
            Assert.DoesNotContain(hidden, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hidden, Assert.Single(output), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(2, CountOf(text, "/_signin?<the sign-in query>"));
    }

    [Fact]
    public void A_sign_in_context_is_read_in_the_url_token_form_with_each_pad_count()
    {
        var digits = new List<char>();
        foreach (var filler in new[] { string.Empty, "a", "ab" })
        {
            var json = $$$"""{"ht":2,"hid":"{{{OtherHostId}}}","qs":{},"rr":"{{{filler}}}","vh":"","cv":"","cs":""}""";
            var token = UrlToken(json);
            digits.Add(token[^1]);
            Assert.Equal(OtherHostId, LiveScreen.HidOfContext(token));
            Assert.Equal(OtherHostId, LiveScreen.HidOfContext(Convert.ToBase64String(Encoding.UTF8.GetBytes(json))));
        }

        Assert.Equal(['0', '1', '2'], digits.Order());
        Assert.Null(LiveScreen.HidOfContext("not-a-context"));
    }

    [Fact]
    public void An_unscreened_sign_in_query_or_context_is_a_survivor_and_a_context_is_replaced_anywhere()
    {
        var screen = Screen();
        Assert.Equal("a sign-in query", screen.SurvivorIn("https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com"));
        Assert.Equal("a sign-in query once percent-decoded", screen.SurvivorIn("x=https%3A%2F%2Fspsprodfake.vssps.visualstudio.com%2F_signin%3Frealm%3Ddev.azure.com"));
        var context = SignInContext(OtherHostId);
        Assert.Equal("a sign-in context", screen.SurvivorIn($"see ?context={context}"));
        Assert.Equal("see ?context=<the organization's id>", screen.Apply($"see ?context={context}"));
        Assert.Equal("see ?context=plain", screen.Apply("see ?context=plain"));
        Assert.Null(screen.SurvivorIn("see ?context=plain and /_signin? alone"));
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

        screen.LearnFromAnswer(
            [new("X-VSS-ResourceTenant", [LiveScreen.ZeroId]), new("WWW-Authenticate", ["Basic realm=\"https://dev.azure.com/\"", "TFS-Federated"])],
            string.Empty);
        screen.AddId(LiveScreen.ZeroId.Replace("-", string.Empty, StringComparison.Ordinal), LiveScreen.TenantPlaceholder);

        var zeroObject = new string('0', 40);
        var text = $"{LiveScreen.AnonymousId} {AzureDevOpsApi.CoreAreaId} {LiveScreen.GitAreaId} {LiveScreen.ZeroId} {zeroObject}";
        Assert.Equal(text, screen.Apply(text));
        Assert.Equal(0, screen.Counts.Ids);
    }

    [Fact]
    public void A_zero_tenant_is_never_learned_so_zero_object_ids_and_zero_guids_stay_as_sent()
    {
        var screen = Screen();
        using (var connection = JsonDocument.Parse($$$"""{"authenticatedUser":{"id":"{{{UserId}}}","subjectDescriptor":"aad.QnJhY2tlbnJpZGdlLW1hZGUtdXA"},"instanceId":"{{{InstanceId}}}"}"""))
        {
            screen.Learn(connection.RootElement);
        }

        Assert.Equal(3, screen.Counts.Ids);
        screen.LearnFromAnswer(
            [new("X-VSS-ResourceTenant", [LiveScreen.ZeroId]), new("WWW-Authenticate", ["Basic realm=\"https://dev.azure.com/\"", "TFS-Federated"])],
            string.Empty);
        Assert.Equal(3, screen.Counts.Ids);
        var body = """The body as sent: {"name":"refs/heads/visualcommit-live/x","oldObjectId":"1f2e3d4c5b6a79881f2e3d4c5b6a79881f2e3d4c","newObjectId":"0000000000000000000000000000000000000000"}""";
        Assert.Equal(body, screen.Apply(body));
        Assert.Equal("""{"serviceOwner":"00000000-0000-0000-0000-000000000000"}""", screen.Apply("""{"serviceOwner":"00000000-0000-0000-0000-000000000000"}"""));
        Assert.Equal("<the user's id> <the organization's id>", screen.Apply($"{UserId} {InstanceId}"));
    }

    [Fact]
    public void Accounts_json_written_by_the_app_with_an_escaped_learned_address_is_a_problem()
    {
        foreach (var address in new[] { "rowan.vale@corp.test", "rowan.vale+azure@corp.test", "jörg.vale@corp.test" })
        {
            using var folder = new TempDirectory("live-screen");
            var screen = Screen();
            using (var connection = JsonDocument.Parse(JsonSerializer.Serialize(new { authenticatedUser = new { id = UserId, providerDisplayName = address, properties = new { Account = new Dictionary<string, string> { ["$type"] = "System.String", ["$value"] = address } } } })))
            {
                screen.Learn(connection.RootElement);
            }

            Assert.Equal(1, screen.Counts.Addresses);
            var file = Path.Combine(folder.Path, "accounts.json");
            new VisualCommit.Hosting.JsonAccountStore(file, VisualCommit.Core.Logging.NullAppLog.Instance).Save(new VisualCommit.Hosting.AccountsFile
            {
                Accounts = [new VisualCommit.Hosting.AccountRecord { Provider = "azure-devops", Host = "dev.azure.com", Login = Org, Name = address }],
            });
            var written = File.ReadAllText(file);
            if (address.Contains('+', StringComparison.Ordinal) || !address.All(char.IsAscii))
            {
                Assert.DoesNotContain(address, written, StringComparison.OrdinalIgnoreCase);
            }

            var run = new LiveRun("made-up-token-for-the-screen-0123456789", Path.Combine(folder.Path, "run.txt"), _ => { }, screen, HostingProvider.AzureDevOps);
            Assert.Equal("The data folder's accounts.json held a learned address (D372).", Assert.Single(run.DataFolderProblems(folder.Path)));
            Assert.Equal("a learned address", screen.SurvivorInDataFile(written));
        }

        var unlearned = Screen();
        unlearned.AddAddress("someone.else@corp.test");
        Assert.Null(unlearned.SurvivorInDataFile("""{"accounts":[{"name":"rowan.vale+azure@corp.test"}]}"""));
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
    public void Notes_are_screened_and_one_that_still_holds_a_value_is_withheld_before_the_output_and_fails_the_run()
    {
        using var folder = new TempDirectory("live-screen");
        var output = new List<string>();
        var file = folder.Combine("run.txt");
        File.WriteAllText(file, "an earlier run's");
        var run = new LiveRun("made-up-token-for-the-screen-0123456789", file, output.Add, Screen(), HostingProvider.AzureDevOps);
        run.Note("GET https://dev.azure.com/Fernhill-Labs/_apis/connectionData: 200");
        Assert.Equal("GET https://dev.azure.com/<the organization>/_apis/connectionData: 200", Assert.Single(output));
        run.Note("Location state=realm%3Ddev.azure.com%26reply_to%3Dhttps%253A%252F%252Fdev.azure.com%252FFernhill-Labs%252F_apis");
        run.Note("x%252FFernhill-Labs%252Fy");
        Assert.Equal(
            ["A note was withheld: it still held the organization's name once percent-decoded.", "A note was withheld: it still held the organization's name once percent-decoded."],
            output.Skip(1));
        var failed = Assert.Throws<InvalidOperationException>(run.Dispose);
        Assert.Contains("A note still held the organization's name once percent-decoded once screened (D372)", failed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Fernhill", failed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fernhill", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("an earlier run's", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_txt_with_a_survivor_is_withheld_and_fails_the_run()
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        File.WriteAllText(file, "an earlier run's");
        var run = new LiveRun("made-up-token-for-the-screen-0123456789", file, _ => { }, Screen(), HostingProvider.AzureDevOps);
        run.WriteRunFile("x%252FFernhill-Labs%252Fy");
        Assert.Contains("run.txt was withheld: its screened text still held the organization's name once percent-decoded", Assert.Single(run.Failures), StringComparison.Ordinal);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void A_withheld_run_txt_on_a_provider_s_first_run_fails_with_its_reason()
    {
        using var folder = new TempDirectory("live-screen");
        var file = Path.Combine(folder.Path, "azure-devops", "run.txt");
        var run = new LiveRun(null, file, _ => { }, Screen(), HostingProvider.AzureDevOps);
        run.WriteRunFile("x%252FFernhill-Labs%252Fy");
        Assert.Contains(run.Failures, failure => failure.StartsWith("run.txt was withheld: its screened text still held", StringComparison.Ordinal));
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
    public void A_message_a_check_or_a_failure_that_still_holds_a_value_once_screened_is_withheld_and_fails_the_run()
    {
        using var folder = new TempDirectory("live-screen");
        var output = new List<string>();
        var screen = Screen();
        screen.AddAddress(Address);
        var run = new LiveRun("made-up-token-for-the-screen-0123456789", folder.Combine("run.txt"), output.Add, screen, HostingProvider.AzureDevOps);
        Assert.Equal("(withheld: it still held the organization's name once percent-decoded)", run.Screen("reply_to%3Dhttps%253A%252F%252Fdev.azure.com%252FFernhill-Labs"));
        var thrown = Assert.Throws<XunitException>(() => run.FailureOf(new InvalidOperationException("reply_to%3Dhttps%253A%252F%252Fdev.azure.com%252FFernhill-Labs"), "2, the sign-in").Throw());
        Assert.Equal("Step 2, the sign-in failed with InvalidOperationException, whose text still held the organization's name once percent-decoded once screened, so it is withheld (D372).", thrown.Message);
        Assert.Null(thrown.InnerException);
        var check = Assert.Throws<XunitException>(() => run.Check(false, () => "the login is fernhill%252Dlabs"));
        Assert.Equal("(withheld: it still held the organization's name once percent-decoded)", check.Message);
        run.Note("by ada.lovelace%2540corp.test");
        Assert.Equal("A note was withheld: it still held a learned address once percent-decoded.", Assert.Single(output));
        Assert.Equal(4, run.Failures.Count);
        Assert.All(run.Failures, failure => Assert.DoesNotContain("Fernhill", failure, StringComparison.OrdinalIgnoreCase));
        Assert.Throws<InvalidOperationException>(run.Dispose);
    }

    /// <summary>A sign-in context as the service sends it (MEASURED r5 p03, p15): 93 bytes of JSON in the UrlToken form, 125 characters ending in its pad count.</summary>
    private static string SignInContext(string hid) => UrlToken($$$"""{"ht":2,"hid":"{{{hid}}}","qs":{},"rr":"","vh":"","cv":"","cs":""}""");

    /// <summary>ASP.NET's UrlToken form: base64url without its padding, then the number of <c>=</c> it had.</summary>
    private static string UrlToken(string json)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var trimmed = base64.TrimEnd('=');
        return trimmed.Replace('+', '-').Replace('/', '_') + (base64.Length - trimmed.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A 302's <c>Location</c> in the measured order: realm, reply_to, redirect, hid, context, then <c>#ctx=</c>.</summary>
    private static string SignInLocation(string context) =>
        $"https://spsprodfake.vssps.visualstudio.com/_signin?realm=dev.azure.com&reply_to=https%3A%2F%2Fdev.azure.com%2FFernhill-Labs%2F_apis%2FconnectionData&redirect=1&hid={InstanceId}&context={context}#ctx=made-up";

    /// <summary>The 302's "Object moved" body, its <c>href</c> with <c>&amp;amp;</c> as written.</summary>
    private static string ObjectMoved(string location) =>
        $"<html><head><title>Object moved</title></head><body><h2>Object moved to <a href=\"{location.Replace("&", "&amp;", StringComparison.Ordinal)}\">here</a>.</h2></body></html>";

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
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

    // ---- Hunt 3's findings on the screen (M1 to M3) -----------------------------------------------

    private const string HuntOrg = "fabrikam7";

    private const string HuntAddress = "pat.user@corp.test";

    private const string HuntToken = "made-up-token-for-the-screen-0123456789";

    private const string BodyNote = "Azure DevOps answered 404, the body as sent: ";

    /// <summary>
    /// Hunt 3's three bodies (M1), each with the note it leaves, or null when it is withheld: JSON's
    /// line escapes before the organization; <c>\/</c> and <c>\uXXXX</c> before it, inside it and in
    /// the address; HTML character references in the address.
    /// </summary>
    public static TheoryData<string, string?> HuntBodies => new()
    {
        {
            """{"$id":"1","message":"TF401019: no repository in\nfabrikam7\tfor\npat.user@corp.test.","typeKey":"GitRepositoryNotFoundException"}""",
            """{"$id":"1","message":"TF401019: no repository in\n<the organization>\tfor\n<the user's address>.","typeKey":"GitRepositoryNotFoundException"}"""
        },
        {
            """{"url":"https:\/\/dev.azure.com\/fabrikam7\/_apis","in":"\u002Ffabrikam7","by":"pat.user\u0040corp.test","name":"\u0066abrikam7"}""",
            null
        },
        {
            """<html><body><h2>Sign in to fabrikam7&#47;sandbox as pat.user&#64;corp.test or PAT.USER&#x40;CORP.TEST &amp;fabrikam7</h2></body></html>""",
            """<html><body><h2>Sign in to <the organization>&#47;sandbox as <the user's address> or <the user's address> &amp;<the organization></h2></body></html>"""
        },
    };

    [Theory]
    [MemberData(nameof(HuntBodies))]
    public void A_body_s_json_escapes_and_character_references_leave_neither_the_organization_nor_a_learned_address(string body, string? kept)
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var screen = Screen(HuntOrg);
        screen.AddAddress(HuntAddress);
        var output = new List<string>();
        var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.AzureDevOps);
        run.Note(BodyNote + body);
        if (kept is null)
        {
            Assert.StartsWith(LiveRun.ScreenedNotePrefix, Assert.Single(output), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(run.Dispose);
        }
        else
        {
            Assert.Equal(BodyNote + kept, Assert.Single(output));
            run.Dispose();
        }

        foreach (var text in new[] { output[0], File.ReadAllText(file) })
        {
            var decoded = FullyDecoded(text);
            Assert.DoesNotContain(HuntOrg, decoded, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(HuntAddress, decoded, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Json_escapes_and_character_references_bound_the_organization_and_hide_nothing_from_the_search()
    {
        var screen = Screen(HuntOrg);
        screen.AddAddress(HuntAddress);
        var screened = screen.Apply("""in\nfabrikam7\tor\rfabrikam7\b, \u002Ffabrikam7 and \u00e9fabrikam7; by pat.user&#64;corp.test, pat.user&#x40;corp.test and PAT.USER&#X40;CORP.TEST.""");
        Assert.Equal("""in\n<the organization>\tor\r<the organization>\b, \u002F<the organization> and \u00e9<the organization>; by <the user's address>, <the user's address> and <the user's address>.""", screened);
        Assert.Equal(screened, screen.Apply(screened));
        Assert.Null(screen.SurvivorIn(screened));
        Assert.Equal("fabrikam7x and \\nfabrikam7x", screen.Apply("fabrikam7x and \\nfabrikam7x"));

        Assert.Equal("the organization's name once decoded", screen.SurvivorIn("""{"name":"\u0066abrikam7"}"""));
        Assert.Equal("the organization's name once decoded", screen.SurvivorIn("Sign in to &#102;abrikam7"));
        Assert.Equal("the organization's name once decoded", screen.SurvivorIn("x%20%5Cu0066abrikam7"));
        Assert.Equal("a learned address once decoded", screen.SurvivorIn("""{"by":"pat.user\u0040corp.test"}"""));
        Assert.Equal("an address once decoded", screen.SurvivorIn("by bob&#x40;other.test"));
        Assert.Equal("an address once decoded", screen.SurvivorIn("by bob&commat;other.test"));
        Assert.Equal("the organization's name once percent-decoded", screen.SurvivorIn("x%252Ffabrikam7%252Fy"));
        Assert.Null(screen.SurvivorIn("""C:\Users\x\new and \u0041 and &amp; and %C0%AE"""));
    }

    [Fact]
    public void An_address_with_an_apostrophe_an_equals_sign_or_a_slash_is_learned_and_hidden_whole()
    {
        var screen = Screen(HuntOrg);
        using (var answer = JsonDocument.Parse("""{"authenticatedUser":{"properties":{"Account":{"$type":"System.String","$value":"mary.o'connor@corp.test"}}},"createdBy":{"uniqueName":"pat=dev@corp.test"},"mailAddress":"team/ops@corp.test","remoteUrl":"https://fabrikam7@dev.azure.com/fabrikam7/p/_git/r","sshUrl":"git@ssh.dev.azure.com:v3/fabrikam7/p/r"}"""))
        {
            screen.Learn(answer.RootElement);
        }

        Assert.Equal("by <the user's address>, <the user's address> and <the user's address>.", screen.Apply("by mary.o'connor@corp.test, pat=dev@corp.test and team/ops@corp.test."));
        Assert.Equal("https://<the organization>@dev.azure.com/<the organization>/p/_git/r and git@ssh.dev.azure.com:v3/<the organization>/p/r", screen.Apply("https://fabrikam7@dev.azure.com/fabrikam7/p/_git/r and git@ssh.dev.azure.com:v3/fabrikam7/p/r"));
        Assert.Equal("a learned address", screen.SurvivorInDataFile("""{"name":"mary.o'connor@corp.test"}"""));
        Assert.Equal("from <an address> and '<an address>' now", Screen(HuntOrg).Apply("from mary.o'connor@corp.test and 'bob@other.test' now"));

        var git = Screen(HuntOrg);
        git.LearnFromGit("pat=dev@corp.test\nmary.o'connor@corp.test\n");
        Assert.Equal("<the user's address> and <the user's address>", git.Apply("pat=dev@corp.test and mary.o'connor@corp.test"));
    }

    [Fact]
    public void An_escaped_hid_is_learned_from_an_answer_and_replaced_and_one_never_learned_is_a_survivor()
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var screen = Screen();
        var location = $"https://dev.azure.com/_signedout?reply_to=https%3A%2F%2Fspsprodfake.vssps.visualstudio.com%2F_apis%2Fconnect%3Frealm%3Ddev.azure.com%26hid%3D{OtherHostId}";
        screen.LearnFromAnswer([new("Location", [location])], null);
        Assert.Equal(1, screen.Counts.Ids);
        var output = new List<string>();
        var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.AzureDevOps);
        run.Note($"Azure DevOps' answer: 302 Found; Location {location}");
        run.Dispose();
        foreach (var text in new[] { Assert.Single(output), File.ReadAllText(file) })
        {
            Assert.DoesNotContain(OtherHostId, FullyDecoded(text), StringComparison.OrdinalIgnoreCase);
        }

        var unlearned = Screen();
        Assert.Equal("a host id once percent-decoded", unlearned.SurvivorIn($"reply_to=x%3Frealm%3Ddev.azure.com%26hid%3D{OtherHostId}"));
        Assert.Equal("a host id", unlearned.SurvivorIn($"x?hid={OtherHostId}"));
        Assert.Null(unlearned.SurvivorIn("x?hid=<the organization's id> and hid%3D<the organization's id>"));
    }

    // ---- The verification of hunt 3's fixes: the screen ---------------------------------------------

    [Theory]
    [InlineData("x fabrik%61m7&#65; y")]
    [InlineData("\\u0041fabrik%61m7")]
    [InlineData("&#65;fabrik%61m7")]
    public void Every_reading_the_three_decodings_reach_is_searched_not_one_chain_of_them(string text) =>
        Assert.NotNull(Screen(HuntOrg).SurvivorIn(text));

    [Theory]
    [InlineData(6, true)]
    [InlineData(8, false)]
    public void A_text_whose_readings_pass_the_limit_is_withheld_whole(int depth, bool complete)
    {
        // Three sites, each needing depth passes of one decoding alone: (depth + 1)³ - 1 readings.
        var text = string.Join(
            ' ',
            new string('\\', 1 << (depth - 1)) + "u0041",
            "%" + string.Concat(Enumerable.Repeat("25", depth - 1)) + "41",
            "&" + string.Concat(Enumerable.Repeat("amp;", depth - 1)) + "#65;");
        var (readings, done) = LiveScreen.Decodings(text);
        Assert.Equal(complete, done);
        Assert.Equal(complete ? ((depth + 1) * (depth + 1) * (depth + 1)) - 1 : LiveScreen.ReadingLimit, readings.Count);
        Assert.Equal(complete, readings.Contains(("A A A", false)));
        Assert.Equal(complete ? null : LiveScreen.NestedPastTheSearch, Screen(HuntOrg).SurvivorIn(text));
    }

    [Fact]
    public void An_escape_whose_digits_run_into_the_name_or_the_address_bounds_it_and_is_searched()
    {
        var screen = Screen(HuntOrg);
        screen.AddAddress(HuntAddress);
        const string text = "in &#x2Ffabrikam7 by pat.user&#x40corp.test";
        Assert.Equal("in &#x2F<the organization> by <the user's address>", screen.Apply(text));
        Assert.NotNull(screen.SurvivorIn(text));
        Assert.Equal("a learned address", screen.SurvivorIn("by pat.user&#x40corp.test"));
        Assert.Equal("by <the user's address> and <the user's address>", screen.Apply("by pat.user&#64corp.test and PAT.USER&#X40CORP.TEST"));
        foreach (var (raw, screened) in new[]
        {
            ("&#xfabrikam7", "&#x<the organization>"),
            ("%Ffabrikam7", "%F<the organization>"),
            ("\\u00fabrikam7", "\\u00<the organization>"),
            ("&#1fabrikam7", "&#1<the organization>"),
        })
        {
            Assert.Equal(screened, screen.Apply(raw));
            Assert.Equal("the organization's name", screen.SurvivorIn(raw));
            Assert.Null(screen.SurvivorIn(screened));
        }
    }

    [Fact]
    public void A_learned_address_leaves_no_part_of_itself_behind_in_any_escaped_form()
    {
        var screen = Screen(HuntOrg);
        using (var answer = JsonDocument.Parse("""{"authenticatedUser":{"properties":{"Account":{"$value":"mary.o'connor@corp.test"}}}}"""))
        {
            screen.Learn(answer.RootElement);
        }

        foreach (var form in new[] { "mary.o'connor@corp.test", "mary.o%27connor@corp.test", "mary.o&#39;connor@corp.test", "mary.o&apos;connor@corp.test", "mary.o&#x27;connor@corp.test", "mary.o\\u0027connor@corp.test", "<p>Signed in as mary.o&#39;connor@corp.test</p>", "?hint=mary.o%27connor@corp.test" })
        {
            var decoded = FullyDecoded(screen.Apply("by " + form));
            Assert.DoesNotContain("mary", decoded, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connor", decoded, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal((1, 1, 0), screen.Counts);
        var git = Screen(HuntOrg);
        git.LearnFromGit("pat=dev@corp.test\npat/dev@corp.test");
        Assert.Equal(2, git.Counts.Addresses);
        foreach (var form in new[] { "pat%3Ddev@corp.test", "pat&#61;dev@corp.test", "pat%2Fdev@corp.test", "pat&#47;dev@corp.test" })
        {
            var decoded = FullyDecoded(git.Apply("author " + form));
            Assert.DoesNotContain("pat", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("dev", decoded, StringComparison.Ordinal);
        }

        // A value that is no address as a whole keeps its address learned: the descriptor's and mailto's.
        var composite = Screen(HuntOrg);
        using (var answer = JsonDocument.Parse("""{"descriptor":"Microsoft.IdentityModel.Claims.ClaimsIdentity;7b0a0104-0000-4a00-8000-00000000fa4e\\ada@corp.test","link":"mailto:bob@corp.test"}"""))
        {
            composite.Learn(answer.RootElement);
        }

        Assert.Equal("a learned address", composite.SurvivorInDataFile("""{"name":"ada@corp.test"}"""));
        Assert.Equal("a learned address", composite.SurvivorInDataFile("""{"name":"bob@corp.test"}"""));

        // A reference that encodes a bracket still ends a run, so the address learned beside it is the address alone.
        var html = Screen(HuntOrg);
        html.LearnFromAnswer([], "<p>Ada&lt;ada@corp.test&gt; signed in</p>");
        Assert.Equal("a learned address", html.SurvivorInDataFile("""{"name":"ada@corp.test"}"""));

        var unlearned = Screen(HuntOrg);
        foreach (var form in new[] { "john.o&#39;neil@other.test", "a&amp;b@other.test", "pat&#46;user@corp.test", "x&#39;&#39;y@other.test" })
        {
            Assert.Equal("by <an address>", unlearned.Apply("by " + form));
        }
    }

    [Fact]
    public void A_zero_padded_character_reference_is_decoded_for_the_search()
    {
        var screen = Screen(HuntOrg);
        screen.AddAddress(HuntAddress);
        Assert.Equal("the organization's name once decoded", screen.SurvivorIn("x &#00000102;abrikam7 y"));
        Assert.Equal("the organization's name once decoded", screen.SurvivorIn("x &#x0000066;abrikam7 y"));
        Assert.Equal("a learned address once decoded", screen.SurvivorIn("by pat.user&#x0000040;corp.test"));
    }

    // ---- The lead's last round (N1 to N4): escaped values, cut addresses, GitLab's namespace -------

    /// <summary>A learned address at the organization's domain (N1): the organization's rule would cut it in any form Apply does not replace.</summary>
    private const string OrgDomainAddress = "pat.user@fabrikam7.test";

    /// <summary>
    /// The verifiers' bodies (N1): <see cref="OrgDomainAddress"/> with its at sign as a JSON escape, a
    /// zero-padded reference, <c>&amp;commat;</c> and <c>&amp;amp;#64;</c>, in a URL's user part, and a
    /// learned hid whose hyphens are references.
    /// </summary>
    public static TheoryData<string> EscapedLearnedValues => new()
    {
        "{\"by\":\"pat.user\\u0040fabrikam7.test\"}",
        "<p>Signed in as pat.user&#064;fabrikam7.test</p>",
        "<p>Signed in as pat.user&#x0040;fabrikam7.test</p>",
        "<p>Signed in as pat.user&commat;fabrikam7.test</p>",
        "<p>Signed in as pat.user&amp;#64;fabrikam7.test</p>",
        "remote https://pat.user&amp;#64;fabrikam7.test@dev.azure.com/fabrikam7/p/_git/r",
        "Location https://dev.azure.com/_signedout?realm=dev.azure.com&hid=7b0a0103&#45;0000&#45;4a00&#45;8000&#45;00000000fa4e",
    };

    [Theory]
    [MemberData(nameof(EscapedLearnedValues))]
    public void A_learned_value_in_a_form_the_screen_does_not_replace_is_withheld_whole_and_fails_the_run(string body)
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var screen = Screen(HuntOrg);
        screen.AddAddress(OrgDomainAddress);
        screen.AddId(OtherHostId, LiveScreen.OrganizationIdPlaceholder);
        var output = new List<string>();
        var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.AzureDevOps);
        run.Note(BodyNote + body);
        var message = run.Screen("The service said: " + body);
        var failure = Assert.Throws<XunitException>(() => run.FailureOf(new InvalidOperationException("The service said: " + body), "2, the sign-in").Throw());
        var told = new List<string>();
        string answer;
        using (var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(HuntOrg, "p", "r", HuntToken), new SocketsHttpHandler(), AzureDevOpsEndpoints.Service.WebBase, TimeSpan.FromSeconds(5), screen, told.Add))
        {
            answer = api.Screen(body);
        }

        Assert.StartsWith(LiveRun.ScreenedNotePrefix, Assert.Single(output), StringComparison.Ordinal);
        Assert.StartsWith("(withheld: it still held ", message, StringComparison.Ordinal);
        Assert.EndsWith("so it is withheld (D372).", failure.Message, StringComparison.Ordinal);
        Assert.StartsWith("Azure DevOps's answer was withheld: it held ", answer, StringComparison.Ordinal);
        Assert.Single(told);
        var direct = new LiveRun(HuntToken, folder.Combine("direct.txt"), _ => { }, screen, HostingProvider.AzureDevOps);
        direct.WriteRunFile(body);
        Assert.StartsWith("run.txt was withheld: its screened text still held ", Assert.Single(direct.Failures), StringComparison.Ordinal);
        Assert.False(File.Exists(folder.Combine("direct.txt")));
        Assert.Throws<InvalidOperationException>(run.Dispose);
        foreach (var text in new[] { output[0], message, failure.Message, answer, File.ReadAllText(file) })
        {
            var decoded = FullyDecoded(text);
            foreach (var hidden in new[] { "pat.user", HuntOrg, "00000000fa4e" })
            {
                Assert.DoesNotContain(hidden, decoded, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void A_learned_value_written_as_itself_is_replaced_as_before_and_a_text_without_one_is_left_as_it_is()
    {
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var screen = Screen(HuntOrg);
        screen.AddAddress(OrgDomainAddress);
        screen.AddId(OtherHostId, LiveScreen.OrganizationIdPlaceholder);
        var output = new List<string>();
        var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.AzureDevOps);
        run.Note($"by pat.user@fabrikam7.test, PAT.USER%40FABRIKAM7.TEST and pat.user&#64;fabrikam7.test in fabrikam7, x?hid={OtherHostId}");
        const string none = "{\"path\":\"C:\\\\Users\\\\x\",\"note\":\"\\u0041 &amp; %41 &#65; bob=a\"}";
        run.Note(none);
        run.Dispose();
        Assert.Equal(["by <the user's address>, <the user's address> and <the user's address> in <the organization>, x?hid=<the organization's id>", none], output);
    }

    /// <summary>
    /// The last verification's texts (the lead's ruling after N1): an address at the organization's
    /// domain that the screen never learned, its at sign escaped, as a reference without its <c>;</c>,
    /// with it, as <c>&amp;commat;</c>, or raw in JSON; the organization's rule must not cut its domain
    /// and leave its local part.
    /// </summary>
    public static TheoryData<string> UnlearnedAddressesAtTheOrganizationsDomain => new()
    {
        "by bob&#x40fabrikam7.test",
        "by bob&#64fabrikam7.test",
        "<p>Reviewer: colleague&#64;fabrikam7.test</p>",
        "<p>Reviewer: colleague&#x40;fabrikam7.test</p>",
        "<p>Reviewer: colleague&commat;fabrikam7.test</p>",
        "{\"reviewer\":\"colleague@fabrikam7.test\"}",
        "See https://dev.azure.com/fabrikam7/_usersSettings/about?user=colleague%40fabrikam7.test for it.",
    };

    [Theory]
    [MemberData(nameof(UnlearnedAddressesAtTheOrganizationsDomain))]
    public void An_unlearned_address_at_the_organization_s_domain_leaves_no_part_of_itself(string text)
    {
        var screen = Screen(HuntOrg);
        screen.LearnFromAnswer([], "{\"message\":" + System.Text.Json.JsonSerializer.Serialize(text) + "}");

        var screened = LiveRun.ScreenedBy(screen, text, out var kind);

        if (screened is not null)
        {
            var decoded = FullyDecoded(screened);
            Assert.True(kind is null, kind);
            foreach (var hidden in new[] { "bob", "colleague", HuntOrg })
            {
                Assert.DoesNotContain(hidden, decoded, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>The verifier's five texts (N2), each an answer's body or header and the note that shows it: an address with <c>=</c>, <c>/</c> or <c>+</c> before its <c>@</c> inside a message.</summary>
    public static TheoryData<string, string?, string> AddressesInMessages => new()
    {
        { string.Empty, """{"$id":"1","message":"TF401019: the user pat=dev@corp.test has no access.","typeKey":"UnauthorizedRequestException"}""", string.Empty },
        { string.Empty, """{"message":"The owner team/ops@corp.test must approve it."}""", string.Empty },
        { string.Empty, """{"message":"Pushed by Pat <pat=dev@corp.test> to main."}""", string.Empty },
        { "pat=dev@corp.test", null, "The answer's X-Pushed-By: pat=dev@corp.test" },
        { string.Empty, """{"message":"Approved by pat+ci=x@corp.test."}""", string.Empty },
    };

    [Theory]
    [MemberData(nameof(AddressesInMessages))]
    public void An_address_with_an_equals_sign_a_slash_or_a_plus_in_a_message_is_learned_whole_and_hidden_whole(string header, string? body, string note)
    {
        using var folder = new TempDirectory("live-screen");
        var screen = Screen(HuntOrg);
        screen.LearnFromAnswer(header.Length == 0 ? [] : [new("X-Pushed-By", [header])], body);
        var output = new List<string>();
        var run = new LiveRun(HuntToken, folder.Combine("run.txt"), output.Add, screen, HostingProvider.AzureDevOps);
        run.Note(body ?? note);
        run.Dispose();
        var kept = Assert.Single(output);
        Assert.Contains(LiveScreen.AddressPlaceholder, kept, StringComparison.Ordinal);
        foreach (var head in new[] { "pat=", "team/", "pat+ci" })
        {
            Assert.DoesNotContain(head, kept, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_run_s_widest_whole_address_tail_stops_at_a_url_s_marks_and_a_reference_and_is_none_without_one()
    {
        Assert.Equal("pat=dev@corp.test", LiveScreen.WholeTailOf("pat=dev@corp.test."));
        Assert.Equal("team/ops@corp.test", LiveScreen.WholeTailOf("https://example.test//team/ops@corp.test"));
        Assert.Equal("ada@corp.test", LiveScreen.WholeTailOf("ClaimsIdentity;7b0a0104\\ada@corp.test"));
        Assert.Equal("bob@corp.test", LiveScreen.WholeTailOf("mailto:bob@corp.test"));
        Assert.Equal("hint=pat=dev@corp.test", LiveScreen.WholeTailOf("x/?hint=pat=dev@corp.test"));
        Assert.Null(LiveScreen.WholeTailOf("mary.o&#39;connor@corp.test"));
        Assert.Null(LiveScreen.WholeTailOf("a&amp;b@other.test"));
        Assert.Null(LiveScreen.WholeTailOf("@corp.test"));
    }

    [Theory]
    [InlineData("by bob&#x40cafe.test")]
    [InlineData("by bob&#X0040beef.test")]
    [InlineData("by bob&#6412.test")]
    [InlineData("by bob&#00649.test")]
    public void An_at_sign_s_reference_whose_digits_run_on_is_still_read_as_an_at_sign(string text) =>
        Assert.Equal("an address once decoded", Screen(HuntOrg).SurvivorIn(text));

    [Fact]
    public void A_hid_or_a_sign_in_context_whose_characters_are_references_is_replaced_whole()
    {
        var screen = Screen();
        var hid = OtherHostId.Replace("-", "&#45;", StringComparison.Ordinal);
        Assert.Equal("x?realm=dev.azure.com&hid=<the organization's id>", screen.Apply($"x?realm=dev.azure.com&hid={hid}"));
        Assert.Equal("x?hid=<the organization's id>", screen.Apply($"x?hid=7b0a0103&#x2D;0000&#x2d;4a00-8000&#45;00000000fa4e"));
        var context = SignInContext(OtherHostId);
        var escaped = "&#" + ((int)context[0]).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" + context[1..];
        Assert.Equal("see ?context=<the organization's id>", screen.Apply($"see ?context={escaped}"));
        Assert.Equal("see ?context=plain&#45;value", screen.Apply("see ?context=plain&#45;value"));
        Assert.Equal("x?hid=<the organization's id>&#38;realm=dev.azure.com&#x26;context=<the organization's id>", screen.Apply($"x?hid=7b0a0103&#38;realm=dev.azure.com&#x26;context={context}"));
    }

    /// <summary>GitLab's made-up namespaces (N3, D384): dotted, and with <c>_</c> and <c>-</c>.</summary>
    public static TheoryData<string> MadeUpNamespaces => new() { "j.q.public", "a_b-c.d" };

    [Theory]
    [MemberData(nameof(MadeUpNamespaces))]
    public void GitLab_s_namespace_is_hidden_in_any_case_in_paths_urls_ids_and_members(string name)
    {
        var screen = LiveRun.ScreenOf(HostingProvider.GitLab, null, LiveSettings.NamespaceOf($"{name}/visualcommit-sandbox"));
        var id = Uri.EscapeDataString($"{name}/visualcommit-sandbox");
        var texts = new[]
        {
            $$"""{"id":1001,"username":"{{name}}","web_url":"https://gitlab.com/{{name}}","path_with_namespace":"{{name}}/visualcommit-sandbox","ssh_url_to_repo":"git@gitlab.com:{{name}}/visualcommit-sandbox.git","http_url_to_repo":"https://gitlab.com/{{name}}/visualcommit-sandbox.git"}""",
            $"GET https://gitlab.com/api/v4/projects/{id} 200 and /projects/{id.ToUpperInvariant()}/merge_requests and %2F{name}%2f",
            $"git clone -- https://oauth2:***@gitlab.com/{name.ToUpperInvariant()}/visualcommit-sandbox.git and https://{name}@gitlab.com/x",
            $"The account's login is {name}, not {name}.",
            $"The sandbox {new LiveGitLabSandbox(name, "visualcommit-sandbox", HuntToken)}.",
        };
        using var folder = new TempDirectory("live-screen");
        var file = folder.Combine("run.txt");
        var output = new List<string>();
        var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.GitLab);
        foreach (var text in texts)
        {
            run.Note(text);
        }

        run.Dispose();
        Assert.Equal(texts.Length, output.Count);
        foreach (var text in output.Append(File.ReadAllText(file)))
        {
            Assert.DoesNotContain(name, FullyDecoded(text), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(LiveRun.ScreenedNotePrefix, text, StringComparison.Ordinal);
        }

        Assert.Contains("\"username\":\"<the namespace>\"", output[0], StringComparison.Ordinal);
        Assert.Contains("/projects/<the namespace>%2Fvisualcommit-sandbox 200", output[1], StringComparison.Ordinal);
        Assert.EndsWith("not <the namespace>.", output[3], StringComparison.Ordinal);
        Assert.Equal("The sandbox <the namespace>/visualcommit-sandbox.", output[4]);
    }

    [Theory]
    [MemberData(nameof(MadeUpNamespaces))]
    public void GitLab_s_namespace_escaped_is_withheld_and_a_longer_name_holding_it_is_left(string name)
    {
        var screen = LiveRun.ScreenOf(HostingProvider.GitLab, "Fernhill-Labs", name);
        Assert.Equal((1, 0, 0), screen.Counts);
        Assert.Equal("Fernhill-Labs", screen.Apply("Fernhill-Labs"));
        var longer = $"{name}x and {name}.x and {name}_x and x{name} and {name}-tools";
        Assert.Equal(longer, screen.Apply(longer));
        Assert.Null(screen.SurvivorIn(longer));
        var json = name.Replace(".", "\\u002E", StringComparison.Ordinal).Replace("_", "\\u005F", StringComparison.Ordinal);
        foreach (var escaped in new[] { $"x%252F{name}%252Fy", $"{{\"username\":\"{json}\"}}", "&#" + ((int)name[0]).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" + name[1..], name.Replace(".", "%2E", StringComparison.Ordinal).Replace("_", "%5F", StringComparison.Ordinal) })
        {
            using var folder = new TempDirectory("live-screen");
            var file = folder.Combine("run.txt");
            File.WriteAllText(file, "an earlier run's");
            var output = new List<string>();
            var run = new LiveRun(HuntToken, file, output.Add, screen, HostingProvider.GitLab);
            run.Note(escaped);
            Assert.StartsWith(LiveRun.ScreenedNotePrefix + "the namespace's name once", Assert.Single(output), StringComparison.Ordinal);
            run.WriteRunFile(escaped);
            Assert.False(File.Exists(file));
            Assert.Throws<InvalidOperationException>(run.Dispose);
        }
    }

    [Fact]
    public void GitLab_s_sandbox_shows_every_segment_that_is_the_login_as_the_namespace()
    {
        Assert.Equal("<the namespace>/visualcommit-sandbox", new LiveGitLabSandbox("j.q.public", "visualcommit-sandbox", HuntToken).ToString());
        Assert.Equal("<the namespace>/tools/<the namespace>", new LiveGitLabSandbox("j.q.public/tools", "J.Q.Public", HuntToken).ToString());
        Assert.Equal("j.q.public/tools/J.Q.Public", new LiveGitLabSandbox("j.q.public/tools", "J.Q.Public", HuntToken).FullName);
        Assert.Equal("a_b-c.d", LiveSettings.NamespaceOf("a_b-c.d/group/visualcommit-sandbox"));
        Assert.Null(LiveSettings.NamespaceOf("../visualcommit-sandbox"));
        Assert.Null(LiveSettings.NamespaceOf("j q/visualcommit-sandbox"));
        Assert.Null(LiveSettings.NamespaceOf(null));
    }

    /// <summary><paramref name="text"/> with JSON's string escapes, HTML's character references and percent escapes decoded until nothing changes: the test's own reading.</summary>
    private static string FullyDecoded(string text)
    {
        while (true)
        {
            var next = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\\(?:u([0-9A-Fa-f]{4})|([""\\/bfnrt]))",
                match => match.Groups[1].Success
                    ? ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString()
                    : match.Groups[2].Value switch { "n" => "\n", "t" => "\t", "r" => "\r", "b" => "\b", "f" => "\f", var other => other });
            next = Uri.UnescapeDataString(System.Net.WebUtility.HtmlDecode(next.Replace("&commat;", "@", StringComparison.Ordinal)));
            if (string.Equals(next, text, StringComparison.Ordinal))
            {
                return text;
            }

            text = next;
        }
    }
}
