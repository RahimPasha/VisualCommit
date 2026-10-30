using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Hosting;
using VisualCommit.Hosting.GitHub;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// What the sandbox is, as <c>GET /repos/{o}/{r}</c> answered it (docs/phase-7a-design.md 14.4 step
/// 2): its full name as GitHub writes it, whether it is private, its visibility, its default branch
/// and whether the token may push.
/// </summary>
internal sealed record LiveRepository(string FullName, bool IsPrivate, string Visibility, string DefaultBranch, bool CanPush);

/// <summary>A pull request as <c>GET …/pulls</c> lists it and <c>GET …/pulls/{n}</c> reads it: its number, its head's branch and its head's repository (null for a deleted fork).</summary>
internal sealed record LivePullRequest(int Number, string HeadRef, string? HeadRepository)
{
    /// <summary>The three fields of GitHub's pull request object.</summary>
    internal static LivePullRequest From(JsonElement pull) => new(
        pull.GetProperty("number").GetInt32(),
        pull.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty,
        pull.GetProperty("head").TryGetProperty("repo", out var repo) && repo.ValueKind == JsonValueKind.Object
            ? repo.GetProperty("full_name").GetString()
            : null);
}

/// <summary>The branches <c>git/matching-refs</c> answered: those under the prefix asked for, by name, and any other ref it returned, whole and never touched.</summary>
internal sealed record LiveBranches(IReadOnlyList<string> Under, IReadOnlyList<string> Others);

/// <summary>
/// What the live command may remove from the sandbox (D324; docs/phase-7a-design.md 14.4 step 3),
/// decided here and nowhere else: the branches whose full ref starts with
/// <c>refs/heads/visualcommit-live/</c>, compared ordinally, whatever GitHub's matching-refs made of
/// the trailing slash, and the open pull requests from such a branch of the sandbox itself, never
/// one from a fork.
/// </summary>
internal static class LiveLeftovers
{
    /// <summary>The branches the command pushes, and the only ones it removes (D324).</summary>
    public const string Prefix = "visualcommit-live/";

    public const string HeadsPrefix = "refs/heads/";

    /// <summary>Whether <paramref name="pull"/> comes from a <see cref="Prefix"/> branch of <paramref name="sandbox"/> itself (its head's repository, case ignored).</summary>
    internal static bool IsLeftover(LivePullRequest pull, string sandbox)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return pull.HeadRef.StartsWith(Prefix, StringComparison.Ordinal) && IsFromTheSandbox(pull, sandbox);
    }

    /// <summary>Whether <paramref name="pull"/> comes from exactly <paramref name="branch"/> of <paramref name="sandbox"/> itself: the one the clean-up may close.</summary>
    internal static bool IsFrom(LivePullRequest pull, string branch, string sandbox)
    {
        ArgumentNullException.ThrowIfNull(pull);
        return string.Equals(pull.HeadRef, branch, StringComparison.Ordinal) && IsFromTheSandbox(pull, sandbox);
    }

    /// <summary><paramref name="refs"/> split into the branches under <paramref name="prefix"/> (their names after <c>refs/heads/</c>) and every other ref, whole.</summary>
    internal static LiveBranches Branches(IEnumerable<string> refs, string prefix)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        var under = new List<string>();
        var others = new List<string>();
        foreach (var reference in refs)
        {
            if (reference.StartsWith(HeadsPrefix + prefix, StringComparison.Ordinal) && reference.Length > HeadsPrefix.Length + prefix.Length)
            {
                under.Add(reference[HeadsPrefix.Length..]);
            }
            else
            {
                others.Add(reference);
            }
        }

        return new LiveBranches(under, others);
    }

    private static bool IsFromTheSandbox(LivePullRequest pull, string sandbox) =>
        string.Equals(pull.HeadRepository, sandbox, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One answer of GitHub to the test's own call: the status, <c>X-GitHub-Request-Id</c>, GitHub's
/// <c>message</c>, the body as sent and parsed, the API version GitHub selected, whether
/// <c>X-OAuth-Scopes</c> was present, the token's expiry date and <c>X-RateLimit-Remaining</c>.
/// <see cref="ToString"/> shows the status and the request id only.
/// </summary>
internal sealed record LiveAnswer(
    int Status,
    string? RequestId,
    string? Message,
    JsonElement? Json,
    string Text,
    string? ApiVersionSelected,
    bool ScopesHeaderPresent,
    string? TokenExpiration,
    string? RateLimitRemaining)
{
    public override string ToString() => $"{Status.ToString(CultureInfo.InvariantCulture)} (request {RequestId ?? "none"})";

    /// <summary>
    /// The body for a note: GitHub's text as sent for a status that is not 2xx (its refusals are what
    /// the fake copies, 2.4); for a 2xx only its shape, since a 2xx's body is a record, which may name
    /// repositories outside the sandbox (14.4 step 3: counts, no names).
    /// </summary>
    public string BodyForNotes => Status is >= 200 and < 300
        ? "GitHub's body, a 2xx's, by its shape only: " + (Json switch
        {
            { ValueKind: JsonValueKind.Array } array => $"an array of {array.GetArrayLength().ToString(CultureInfo.InvariantCulture)}.",
            { ValueKind: JsonValueKind.Object } => "an object.",
            _ => $"no JSON, {Text.Length.ToString(CultureInfo.InvariantCulture)} characters.",
        })
        : "GitHub's body as sent:\n" + Text;
}

/// <summary>A call of the test's own that GitHub did not answer as the step needs: the status, GitHub's message and the request id, never a request or an answer.</summary>
internal sealed class GitHubLiveException(string message) : Exception(message);

/// <summary>
/// The live command's own calls to GitHub's API, outside the app (docs/phase-7a-design.md 14.5):
/// read the sandbox and a pull request, list the open pull requests and the
/// <c>visualcommit-live/</c> branches, close a pull request, delete a branch, the duplicate create
/// (r5 14) and the probes of GitHub's texts the fake writes from memory (2.4, 15 point 1). Its own
/// <see cref="HttpClient"/> over a <see cref="SocketsHttpHandler"/> that follows no redirect; each
/// call 30 s at most; <c>Authorization: Bearer</c>, <c>Accept</c> and <c>X-GitHub-Api-Version</c> as
/// the app sends them and its own <c>User-Agent</c>. A failure's message holds the status, GitHub's
/// <c>message</c> and the request id, and is searched for the token (<see cref="LiveTokenForms"/>)
/// before it is thrown; a find is withheld and told to the run (<see cref="LiveRun.Withheld"/>),
/// which then fails.
/// </summary>
internal sealed class GitHubLiveApi : IDisposable
{
    public const string UserAgent = "VisualCommit-live-tests";

    /// <summary>How long one call may take.</summary>
    public static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(30);

    private readonly LiveSandbox _sandbox;
    private readonly HttpClient _client;
    private readonly Action<string>? _withheld;

    /// <summary>The calls to github.com; <paramref name="withheld"/> is told when an answer held the token.</summary>
    public GitHubLiveApi(LiveSandbox sandbox, Action<string>? withheld = null)
        : this(sandbox, new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
        }, withheld)
    {
    }

    /// <summary>The calls over <paramref name="handler"/>: an in-process one for <see cref="LiveSafetyTests"/>.</summary>
    internal GitHubLiveApi(LiveSandbox sandbox, HttpMessageHandler handler, Action<string>? withheld = null)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(handler);
        _sandbox = sandbox;
        _withheld = withheld;
        _client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = GitHubEndpoints.GitHubCom.ApiBase,
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary><c>X-RateLimit-Remaining</c> of the last answer that sent it, or null.</summary>
    public string? RateLimitRemaining { get; private set; }

    /// <summary>The sandbox's path under the API: <c>repos/{o}/{r}</c>.</summary>
    private string RepositoryPath => $"repos/{Uri.EscapeDataString(_sandbox.Owner)}/{Uri.EscapeDataString(_sandbox.Name)}";

    /// <summary><c>GET /repos/{o}/{r}</c>, as answered.</summary>
    public Task<LiveAnswer> ReadRepositoryAsync(CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, RepositoryPath, null, cancellationToken);

    /// <summary>The sandbox's open pull requests, up to 100 (<c>GET …/pulls?state=open&amp;per_page=100</c>), or a failure.</summary>
    /// <exception cref="GitHubLiveException">GitHub did not answer 200 with a list.</exception>
    public async Task<IReadOnlyList<LivePullRequest>> OpenPullRequestsAsync(CancellationToken cancellationToken, string? head = null)
    {
        var query = head is null ? string.Empty : "&head=" + Uri.EscapeDataString(head);
        var answer = await ExpectAsync(HttpMethod.Get, $"{RepositoryPath}/pulls?state=open&per_page=100{query}", null, 200, "the open pull requests", cancellationToken);
        return [.. ListOf(answer).Select(LivePullRequest.From)];
    }

    /// <summary>
    /// The branches under <paramref name="prefix"/> (<c>GET …/git/matching-refs/heads/&lt;prefix&gt;</c>),
    /// split by <see cref="LiveLeftovers.Branches"/>: a ref GitHub returns that is not under the
    /// prefix, as written with its slash, is kept apart and never removed.
    /// </summary>
    /// <exception cref="GitHubLiveException">GitHub did not answer 200 with a list.</exception>
    public async Task<LiveBranches> BranchesUnderAsync(string prefix, CancellationToken cancellationToken)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{RepositoryPath}/git/matching-refs/heads/{EscapePath(prefix)}", null, 200, $"the branches under {prefix}", cancellationToken);
        return LiveLeftovers.Branches(ListOf(answer).Select(reference => reference.GetProperty("ref").GetString() ?? string.Empty), prefix);
    }

    /// <summary>
    /// The login GitHub links to the latest commit of <paramref name="branch"/>
    /// (<c>GET …/commits?sha=&lt;branch&gt;&amp;per_page=1</c>, its <c>author</c>), or null when it links
    /// none: whether the app has an avatar to show (11.3).
    /// </summary>
    /// <exception cref="GitHubLiveException">GitHub did not answer 200 with a list.</exception>
    public async Task<string?> LatestCommitAuthorAsync(string branch, CancellationToken cancellationToken)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{RepositoryPath}/commits?sha={Uri.EscapeDataString(branch)}&per_page=1", null, 200, "the latest commit", cancellationToken);
        return ListOf(answer).FirstOrDefault() is { ValueKind: JsonValueKind.Object } commit
            && commit.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object
            && author.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String
            ? login.GetString()
            : null;
    }

    /// <summary><c>PATCH …/pulls/{n}</c> with <c>{"state":"closed"}</c>, as answered (200 expected).</summary>
    public Task<LiveAnswer> ClosePullRequestAsync(int number, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Patch, $"{RepositoryPath}/pulls/{number.ToString(CultureInfo.InvariantCulture)}", new JsonObject { ["state"] = "closed" }, cancellationToken);

    /// <summary><c>DELETE …/git/refs/heads/{branch}</c>, as answered (204 expected).</summary>
    public Task<LiveAnswer> DeleteBranchAsync(string branch, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, $"{RepositoryPath}/git/refs/heads/{EscapePath(branch)}", null, cancellationToken);

    /// <summary><c>GET …/pulls/{n}</c>, as answered.</summary>
    public Task<LiveAnswer> ReadPullRequestAsync(int number, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"{RepositoryPath}/pulls/{number.ToString(CultureInfo.InvariantCulture)}", null, cancellationToken);

    /// <summary><c>POST …/pulls</c> with a head, a base, a title and draft, as answered: the duplicate create (r5 14).</summary>
    public Task<LiveAnswer> CreatePullRequestAsync(string head, string baseBranch, string title, bool draft, CancellationToken cancellationToken) =>
        SendAsync(
            HttpMethod.Post,
            $"{RepositoryPath}/pulls",
            new JsonObject { ["title"] = title, ["head"] = head, ["base"] = baseBranch, ["draft"] = draft },
            cancellationToken);

    /// <summary><c>GET /repos/{o}/{r}</c> asking for API version <paramref name="version"/>, as answered: GitHub's text for a version it does not know (2.4).</summary>
    public Task<LiveAnswer> ProbeVersionAsync(string version, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, RepositoryPath, null, cancellationToken, version);

    /// <summary><c>GET /user/repos?type=owner&amp;visibility=all</c>, as answered: GitHub's text for <c>type</c> beside <c>visibility</c> (2.4).</summary>
    public Task<LiveAnswer> ProbeTypeWithVisibilityAsync(CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, "user/repos?type=owner&visibility=all&per_page=1", null, cancellationToken);

    /// <summary><c>GET …/commits?sha={sha}&amp;per_page=1</c>, as answered: GitHub's text for a SHA it does not know (2.4).</summary>
    public Task<LiveAnswer> ProbeCommitsAsync(string sha, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"{RepositoryPath}/commits?sha={Uri.EscapeDataString(sha)}&per_page=1", null, cancellationToken);

    /// <summary>"GitHub answered {status} {message} for {what} (request {id})", searched for the token.</summary>
    public string Describe(LiveAnswer answer, string what)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return Screen($"GitHub answered {answer.Status.ToString(CultureInfo.InvariantCulture)} {answer.Message ?? "(no message)"} for {what} (request {answer.RequestId ?? "none"})");
    }

    /// <summary><paramref name="text"/>, or a sentence without it when it holds the token; a find is told to the run (<see cref="LiveRun.Withheld"/>).</summary>
    public string Screen(string text)
    {
        if (LiveTokenForms.FoundIn(text, _sandbox.Token) is not { } form)
        {
            return text;
        }

        _withheld?.Invoke($"GitHub's answer held the token ({form}); it was withheld.");
        return "GitHub's answer was withheld: it held the token";
    }

    /// <summary>One call, answered: a status GitHub sent, whatever it is.</summary>
    /// <exception cref="GitHubLiveException">No answer came within <see cref="CallLimit"/>, or GitHub was not reached.</exception>
    internal async Task<LiveAnswer> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken cancellationToken, string version = GitHubApi.Version)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sandbox.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubApi.MediaType));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", version);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        var shown = $"{method.Method} /{path.Split('?')[0]}";
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(CallLimit);
        try
        {
            using var response = await _client.SendAsync(request, limit.Token);
            var text = await response.Content.ReadAsStringAsync(limit.Token);
            JsonElement? json = null;
            string? message = null;
            try
            {
                using var document = JsonDocument.Parse(text);
                json = document.RootElement.Clone();
                if (json.Value.ValueKind == JsonValueKind.Object && json.Value.TryGetProperty("message", out var said) && said.ValueKind == JsonValueKind.String)
                {
                    message = said.GetString();
                }
            }
            catch (JsonException)
            {
                // Not JSON (an HTML error page, or no body): the status and the request id say enough.
            }

            var remaining = First(response, "X-RateLimit-Remaining");
            if (remaining is not null)
            {
                RateLimitRemaining = remaining;
            }

            return new LiveAnswer(
                (int)response.StatusCode,
                First(response, "X-GitHub-Request-Id"),
                message,
                json,
                text,
                First(response, "X-GitHub-Api-Version-Selected"),
                response.Headers.Contains("X-OAuth-Scopes"),
                LiveRequestLog.ExpirationDate(First(response, "GitHub-Authentication-Token-Expiration")),
                remaining);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubLiveException($"GitHub did not answer {shown} within {CallLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s.");
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubLiveException($"{shown} did not reach GitHub: {ex.HttpRequestError}.");
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>One call that must answer <paramref name="status"/>; else a <see cref="GitHubLiveException"/> with <see cref="Describe"/>'s sentence.</summary>
    private async Task<LiveAnswer> ExpectAsync(HttpMethod method, string path, JsonObject? body, int status, string what, CancellationToken cancellationToken)
    {
        var answer = await SendAsync(method, path, body, cancellationToken);
        return answer.Status == status
            ? answer
            : throw new GitHubLiveException(Describe(answer, what) + ".");
    }

    /// <summary>The answer's body as a JSON array.</summary>
    /// <exception cref="GitHubLiveException">The body is no array.</exception>
    private static IEnumerable<JsonElement> ListOf(LiveAnswer answer) =>
        answer.Json is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray()
            : throw new GitHubLiveException($"GitHub's answer {answer} is no list.");

    /// <summary>A branch name for a path: each segment escaped, the slashes kept.</summary>
    private static string EscapePath(string branch) => string.Join('/', branch.Split('/').Select(Uri.EscapeDataString));

    private static string? First(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) ? values.FirstOrDefault() : null;
}
