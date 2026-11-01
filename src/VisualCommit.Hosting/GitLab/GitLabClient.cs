using System.Globalization;
using System.Text.Json.Serialization;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Http;

namespace VisualCommit.Hosting.GitLab;

/// <summary>
/// GitLab's REST API v4 on the shared HTTP core (docs/phase-7b-design.md 7.3; D358, ruling 4): a
/// <see cref="HostingHttpClient"/> with <see cref="GitLabProfile"/>.
/// </summary>
internal static class GitLabClient
{
    /// <summary>A client of <paramref name="endpoints"/>' API over one handler; the core's limits unless given.</summary>
    public static HostingHttpClient Create(GitLabEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null, long? keptBytes = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(clock);
        return new HostingHttpClient(new GitLabProfile(endpoints, clock), createHandler, log, clock, timeout, keptBytes);
    }
}

/// <summary>
/// GitLab's profile (docs/phase-7b-design.md 7.3): the API base as the token's origin;
/// <c>Authorization: Bearer</c>, which both token kinds take (r6 1.1 READ), one header name for the
/// live command's request log and the redirect rule (g4 D1); <c>Accept: application/json</c> and no
/// version header (r6 9); <c>Link</c> pages (r6 7, MEASURED) and <c>Link</c> kept with an answer,
/// since a 304 has no body (r6 8); <c>x-request-id</c>; GitLab's words for a failure (r6 10, g2 G2);
/// and kinds from the status and shape F's <c>error</c> code alone, never from a sentence or the
/// <c>scope</c> list, whose members vary by route (g2 G0.4). A moved project's GET is 301 to
/// <c>projects/&lt;id&gt;</c> on the same origin (r6 3.2, MEASURED), which the core follows; a 304
/// counts against GitLab's limit (r6 8, MEASURED): kept answers save bytes, not budget.
/// </summary>
internal sealed class GitLabProfile(GitLabEndpoints endpoints, TimeProvider clock) : HostingHttpProfile
{
    /// <summary>The request id GitLab sends on every answer (MEASURED r6 8), in lower case; the core's headers ignore case.</summary>
    public const string RequestIdHeader = "x-request-id";

    /// <summary>The longest <c>Retry-After</c> taken: a day, as 7a's GitHub client (<c>GitHubClient.cs:493-507</c>).</summary>
    public const int MaxRetrySeconds = 86400;

    private static readonly string[] Kept = ["Link"];

    /// <summary>The last epoch second a <see cref="DateTimeOffset"/> holds: 9999-12-31 23:59:59 UTC.</summary>
    private static readonly long MaxResetSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static readonly TimeSpan DefaultRetry = TimeSpan.FromSeconds(60);

    public GitLabEndpoints Endpoints { get; } = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

    public override HostingProvider Provider => HostingProvider.GitLab;

    public override Uri TokenOrigin => Endpoints.ApiBase;

    public override JsonSerializerContext Json => GitLabJsonContext.Default;

    public override IReadOnlyCollection<string> KeptHeaderNames => Kept;

    /// <summary>
    /// The project id of <paramref name="fullName"/>: <see cref="Uri.EscapeDataString"/> of its path,
    /// whose <c>%2F</c> reaches the request line as written (r6 6.2, MEASURED in net10.0).
    /// </summary>
    public static string ProjectId(string fullName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullName);
        return Uri.EscapeDataString(fullName);
    }

    /// <summary>The API's address of <paramref name="relative"/>: <c>new Uri(ApiBase.AbsoluteUri + relative)</c>, so an escaped <c>%2F</c> stays.</summary>
    public Uri Url(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        return new Uri(Endpoints.ApiBase.AbsoluteUri + relative);
    }

    public override void Authorize(HttpRequestMessage request, string token) => request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

    public override void AddHeaders(HttpRequestMessage request) => request.Headers.TryAddWithoutValidation("Accept", "application/json");

    public override Uri? NextPage(HostingExchange exchange) => HostingLinks.Next(exchange.Headers);

    public override string? RequestIdOf(IReadOnlyDictionary<string, string> headers) => headers.GetValueOrDefault(RequestIdHeader);

    /// <summary>
    /// A redirect of anything but a GET is refused, <see cref="HostingFailureKind.Malformed"/>, and
    /// nothing is sent to its <c>Location</c>: GitLab answers a non-GET to a moved project 405 (r6
    /// 3.2, READ) and redirects none, so a redirected POST is a broken server's, and repeating it
    /// would make a second request, or send its body to a path 7.4 does not name (an issue's, say).
    /// </summary>
    public override HostingFailure? RefuseRedirect(HostingExchange exchange, Uri location) =>
        exchange.Method == HttpMethod.Get ? null : new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(Provider));

    /// <summary>The status, and for a refused redirect of a non-GET <c>&lt;status&gt;, a redirected &lt;method&gt;</c>.</summary>
    public override string WarningOf(HostingExchange exchange, HostingFailure failure) =>
        exchange.Method != HttpMethod.Get && HostingHttpClient.IsRedirect(exchange.Status)
            ? string.Create(CultureInfo.InvariantCulture, $"{exchange.Status}, a redirected {exchange.Method.Method}")
            : base.WarningOf(exchange, failure);

    /// <summary>
    /// The failure an answer stands for (7.3's table, in order). The message is GitLab's words
    /// (<see cref="GitLabError.Of"/>), else <c>&lt;code&gt; &lt;reason phrase&gt;</c>, and for a server
    /// error without words the core's <see cref="HostingHttpTexts.ServerError"/>. A
    /// <see cref="PullRequestRefusal"/> is set only on a create's POST to <c>…/merge_requests</c>:
    /// a 409 <see cref="PullRequestRefusal.AlreadyExists"/>; a 400 of shape C by its keys,
    /// <c>source_branch</c> <see cref="PullRequestRefusal.HeadMissing"/>, <c>target_branch</c>
    /// <see cref="PullRequestRefusal.BaseMissing"/>, both <see cref="PullRequestRefusal.BranchesMissing"/>.
    /// A 403 is never a rate limit (r6 8); there is no single sign-on (r6 11), API version (r6 9) or
    /// <see cref="PullRequestRefusal.NoCommits"/>: GitLab creates a request without commits (g2 G1.4).
    /// </summary>
    public override HostingFailure Classify(HostingExchange exchange)
    {
        var status = exchange.Status;
        var error = GitLabError.Of(exchange.Body);
        var fallback = exchange.Reason is { Length: > 0 } reason
            ? string.Create(CultureInfo.InvariantCulture, $"{status} {reason}")
            : string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");
        var message = error.Text ?? fallback;
        var create = IsCreate(exchange);
        static HostingFailure Fail(HostingFailureKind kind, string text, string? detail = null, DateTimeOffset? retryAt = null) => new(kind, text, detail, retryAt);

        switch (status)
        {
            case 401:
                return Fail(HostingFailureKind.BadCredentials, message);
            case 403:
                return Fail(HostingFailureKind.Forbidden, message, error.Code is "insufficient_scope" or "insufficient_granular_scope" ? error.Code : null);
            case 404:
                return Fail(HostingFailureKind.NotFound, message);
            case 409:
                return Fail(HostingFailureKind.Validation, message) with { Refusal = create ? PullRequestRefusal.AlreadyExists : PullRequestRefusal.None };
            case 400:
                return Fail(HostingFailureKind.Validation, message) with { Refusal = create ? BranchRefusal(error.Fields) : PullRequestRefusal.None };
            case 405 or 422:
                return Fail(HostingFailureKind.Validation, message);
            case 429:
                return Fail(HostingFailureKind.RateLimited, message, retryAt: RetryAt(exchange.Headers));
            case 408 or (>= 500 and < 600):
                return Fail(HostingFailureKind.ServerError, error.Text ?? HostingHttpTexts.ServerError(Provider, status));
            default:
                return Fail(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(Provider));
        }
    }

    /// <summary>Whether <paramref name="exchange"/> is a create's POST to <c>projects/:id/merge_requests</c>.</summary>
    private static bool IsCreate(HostingExchange exchange) =>
        exchange.Method == HttpMethod.Post && exchange.Url.AbsolutePath.EndsWith("/merge_requests", StringComparison.Ordinal);

    /// <summary>Shape C's keys on a create (g2 G1.1): the branch, or both branches, that do not exist.</summary>
    private static PullRequestRefusal BranchRefusal(IReadOnlyList<string> fields) =>
        (fields.Contains("source_branch"), fields.Contains("target_branch")) switch
        {
            (true, true) => PullRequestRefusal.BranchesMissing,
            (true, false) => PullRequestRefusal.HeadMissing,
            (false, true) => PullRequestRefusal.BaseMissing,
            _ => PullRequestRefusal.None,
        };

    /// <summary>
    /// When a 429 ends (r6 8, READ): now plus <c>Retry-After</c>'s seconds or its HTTP-date; else
    /// <c>ratelimit-reset</c> (epoch seconds); else <c>ratelimit-resettime</c> (an HTTP-date); else
    /// now plus 60 s. A value that is neither digits nor an HTTP-date (<c>1.5</c>, <c>10:30</c>)
    /// gives way to the next. Every end lies between now and now plus a day
    /// (<see cref="MaxRetrySeconds"/>): one in the past is now, one later is a day from now. A
    /// <c>ratelimit-reset</c> of digits past the last second a <see cref="DateTimeOffset"/> holds throws
    /// <see cref="OverflowException"/>, which the core turns into <see cref="HostingFailureKind.Malformed"/>
    /// (D380), as GitHub's <c>X-RateLimit-Reset</c> past year 9999 is.
    /// </summary>
    private DateTimeOffset RetryAt(IReadOnlyDictionary<string, string> headers)
    {
        var now = clock.GetUtcNow();
        var latest = now.AddSeconds(MaxRetrySeconds);
        DateTimeOffset Bounded(DateTimeOffset at) => at < now ? now : at > latest ? latest : at;
        if (headers.GetValueOrDefault("Retry-After")?.Trim() is { Length: > 0 } retry)
        {
            if (retry.All(char.IsAsciiDigit))
            {
                return now + TimeSpan.FromSeconds(long.TryParse(retry, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? Math.Min(seconds, MaxRetrySeconds) : MaxRetrySeconds);
            }

            if (HttpDate(retry) is { } date)
            {
                return Bounded(date);
            }
        }

        if (headers.GetValueOrDefault("ratelimit-reset")?.Trim() is { Length: > 0 } reset && reset.All(char.IsAsciiDigit))
        {
            if (!long.TryParse(reset, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) || epoch > MaxResetSeconds)
            {
                throw new OverflowException("ratelimit-reset is past the last second a DateTimeOffset holds.");
            }

            return Bounded(DateTimeOffset.FromUnixTimeSeconds(epoch));
        }

        return headers.GetValueOrDefault("ratelimit-resettime") is { Length: > 0 } resetTime && HttpDate(resetTime) is { } at ? Bounded(at) : now + DefaultRetry;
    }

    /// <summary>An HTTP-date in its one current form (RFC 9110 5.6.7's IMF-fixdate, .NET's <c>"r"</c>), such as <c>Sun, 06 Nov 1994 08:49:37 GMT</c>; null for any other text.</summary>
    private static DateTimeOffset? HttpDate(string text) =>
        DateTimeOffset.TryParseExact(text.Trim(), "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
}
