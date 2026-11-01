using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Http;

namespace VisualCommit.Hosting.GitHub;

/// <summary>
/// One answer of <see cref="GitHubClient"/>: its value, or why it failed (docs/phase-7a-design.md 6.2).
/// </summary>
/// <param name="Value">The parsed body; null on a failure and on a 409.</param>
/// <param name="Failure">Why the request failed; null when it did not (a 409 included, A6's empty repository).</param>
/// <param name="Status">The last answer's status code (304 when the kept body was used); 0 when none came.</param>
/// <param name="MoreExist">A paged request stopped at its cap with a <c>next</c> page left.</param>
/// <param name="OAuthScopes"><c>X-OAuth-Scopes</c> (a classic token's scopes); for pages, the first page's.</param>
/// <param name="Sso"><c>X-GitHub-SSO</c>; for pages, the first <c>partial-results</c> one, else the last page's.</param>
/// <param name="RequestId"><c>X-GitHub-Request-Id</c> of the last answer.</param>
internal sealed record GitHubReply<T>(T? Value, HostingFailure? Failure, int Status, bool MoreExist, string? OAuthScopes, string? Sso, string? RequestId)
{
    public bool Succeeded => Failure is null;
}

/// <summary>
/// GitHub's REST API on the shared HTTP core (docs/phase-7a-design.md 6.2, D285;
/// docs/phase-7b-design.md 5.3): <see cref="HostingHttpClient"/> with GitHub's profile, which adds
/// GitHub's headers and <c>Bearer</c> token on the API's own scheme, host and port, follows
/// <c>Link</c> pages, takes a 409 as success without a value, keeps <c>Link</c>, <c>X-GitHub-SSO</c>
/// and <c>X-OAuth-Scopes</c> with a kept answer, and classifies every other answer by 7a's table.
/// The core keeps 7a's rules: one request at a time, 30 s once sent and the caller's cancellation
/// (which throws <see cref="OperationCanceledException"/>), redirects repeated by hand on the API's
/// address only, the 10 MB bound, ETags kept in memory per token fingerprint and URL (at most 512 and
/// 32 MB of bodies, forgotten at a sign-out, <see cref="ForgetKept"/>), and one warning per failure
/// that names neither a header, a body, a query nor a token.
/// </summary>
internal sealed class GitHubClient : IDisposable
{
    /// <summary>The fixed text of a request that got no answer (D284): <see cref="HostingHttpTexts.Network"/> for GitHub.</summary>
    public const string NetworkText = "Could not reach GitHub.";

    /// <summary>The fixed text of a request whose 30 s ran out (D284): <see cref="HostingHttpTexts.Timeout"/> for GitHub.</summary>
    public const string TimeoutText = "GitHub did not answer within 30 seconds.";

    /// <summary>The text of an answer that could not be read: <see cref="HostingHttpTexts.Malformed"/> for GitHub.</summary>
    public const string MalformedText = "GitHub's answer could not be read.";

    /// <summary>The largest body read: anything longer is <see cref="HostingFailureKind.Malformed"/>.</summary>
    public const int MaxBodyBytes = HostingHttpClient.MaxBodyBytes;

    /// <summary>The redirects one request follows at most.</summary>
    public const int MaxRedirects = HostingHttpClient.MaxRedirects;

    /// <summary>How long a request has once sent, unless the constructor says otherwise.</summary>
    public static readonly TimeSpan DefaultTimeout = HostingHttpClient.DefaultTimeout;

    private const string RequestIdHeader = "X-GitHub-Request-Id";
    private const string ScopesHeader = "X-OAuth-Scopes";
    private const string SsoHeader = "X-GitHub-SSO";

    private static readonly TimeSpan SecondaryRetry = TimeSpan.FromSeconds(60);

    private readonly HostingHttpClient _core;

    /// <summary>A client over one handler; <paramref name="keptBytes"/> bounds the kept answers' bodies, 32 MB unless given.</summary>
    public GitHubClient(GitHubEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null, long? keptBytes = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        Endpoints = endpoints;
        _core = new HostingHttpClient(new GitHubProfile(endpoints, clock), createHandler, log, clock, timeout, keptBytes);
    }

    /// <summary><c>VisualCommit/&lt;version&gt;</c>: the Hosting assembly's informational version up to its <c>+</c> (<see cref="HostingHttpClient.UserAgent"/>).</summary>
    public static string UserAgent => HostingHttpClient.UserAgent;

    public GitHubEndpoints Endpoints { get; }

    /// <summary>
    /// One request of <paramref name="method"/> to <paramref name="url"/>, with <paramref name="body"/>
    /// as JSON when given (a type of <see cref="GitHubJsonContext"/>), its answer parsed as
    /// <paramref name="type"/>. Runs on the thread pool.
    /// </summary>
    public Task<GitHubReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var sent = _core.SendAsync(method, token, url, body, type, ct);
        return ReplyAsync(sent);

        static async Task<GitHubReply<T>> ReplyAsync(Task<HostingReply<T>> sent)
        {
            var reply = await sent.ConfigureAwait(false);
            return new GitHubReply<T>(reply.Value, reply.Failure, reply.Status, false, Header(reply.Headers, ScopesHeader), Header(reply.Headers, SsoHeader), reply.RequestId);
        }
    }

    /// <summary>
    /// The pages of a list from <paramref name="first"/>, following <c>Link: rel="next"</c> as
    /// given while it stays on the API's scheme, host, port and path, up to
    /// <paramref name="maxPages"/> (<c>MoreExist</c> when a <c>next</c> remains). A failing page
    /// fails the whole list.
    /// </summary>
    public Task<GitHubReply<List<T>>> GetPagesAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct)
    {
        var pages = new PageHeaders();
        var sent = _core.GetPagesAsync(token, first, type, maxPages, ct, pages.Observe);
        return ReplyAsync(sent, pages);

        static async Task<GitHubReply<List<T>>> ReplyAsync(Task<HostingReply<List<T>>> sent, PageHeaders pages)
        {
            var reply = await sent.ConfigureAwait(false);

            // 7a's: the pages' scopes and single sign-on once the answer the reply is of was read
            // as a page (a list, or a next page off the API's address); else that answer's own
            // (a page that failed or held no value).
            var (scopes, sso) = pages.Saw(reply.Headers)
                ? (pages.Scopes, pages.Sso)
                : (Header(reply.Headers, ScopesHeader), Header(reply.Headers, SsoHeader));
            return new GitHubReply<List<T>>(reply.Value, reply.Failure, reply.Status, reply.Next is not null, scopes, sso, reply.RequestId);
        }
    }

    public void Dispose() => _core.Dispose();

    /// <summary>
    /// Forgets every kept answer, whatever its token (a sign-out, 7.5): the next request of each
    /// URL is a full one. An answer a request still in flight brings is kept as usual.
    /// </summary>
    internal void ForgetKept() => _core.ForgetKept();

    /// <summary>An answer that succeeded but lacks what the app needs: <see cref="MalformedText"/>, with the warning the core writes for one.</summary>
    internal HostingFailure MalformedAnswer(HttpMethod method, Uri url, string? requestId) => _core.MalformedAnswer(method, url, requestId);

    /// <summary>Whether two addresses have the same scheme, host and port.</summary>
    internal static bool SameOrigin(Uri a, Uri b) => HostingHttpClient.SameOrigin(a, b);

    /// <summary>Whether <paramref name="status"/> is a redirect the client repeats: 301, 302, 307 or 308.</summary>
    internal static bool IsRedirect(int status) => HostingHttpClient.IsRedirect(status);

    /// <summary>
    /// The body of <paramref name="content"/>, or null when it is longer than
    /// <paramref name="maxBytes"/> (by its <c>Content-Length</c>, or once that many were read).
    /// </summary>
    internal static Task<byte[]?> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken ct) => HostingHttpClient.ReadBoundedAsync(content, maxBytes, ct);

    /// <summary>The address <paramref name="location"/> names, resolved against <paramref name="current"/>; null when there is none.</summary>
    internal static Uri? Resolve(Uri current, Uri? location) => HostingHttpClient.Resolve(current, location);

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name) => headers.GetValueOrDefault(name);

    /// <summary>Whether <paramref name="text"/> names <c>X-GitHub-Api-Version</c> in any case (GitHub writes <c>X-GitHub-API-Version</c>).</summary>
    private static bool NamesVersionHeader([NotNullWhen(true)] string? text) => text?.Contains("X-GitHub-Api-Version", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// A 422's detail (6.3, A5): the first error's <c>message</c> when it has one; else
    /// <c>&lt;field&gt; is invalid</c>, <c>is missing</c> or <c>already exists</c> by its code; else
    /// GitHub's <c>message</c>.
    /// </summary>
    private static string? ValidationDetail(GitHubErrorJson? error)
    {
        var first = error?.Errors is [var item, ..] ? item : null;
        if (first?.Message is { Length: > 0 } message)
        {
            return message;
        }

        return first?.Field is { Length: > 0 } field
            ? first.Code switch
            {
                "invalid" => field + " is invalid",
                "missing" or "missing_field" => field + " is missing",
                "already_exists" => field + " already exists",
                _ => error!.Message,
            }
            : error?.Message;
    }

    /// <summary>The address of <c>X-GitHub-SSO: required; url=&lt;address&gt;</c>.</summary>
    private static string? SsoUrl(string header)
    {
        var at = header.IndexOf("url=", StringComparison.OrdinalIgnoreCase);
        return at < 0 ? null : header[(at + 4)..].Trim();
    }

    private static GitHubErrorJson? ErrorOf(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, GitHubJsonContext.Default.GitHubErrorJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// GitHub's profile (5.3): the API base as the token's origin, <c>Bearer</c>, <c>Accept</c> and
    /// <c>X-GitHub-Api-Version</c>, 7a's table, 409 as success without a value, <c>Link</c> pages,
    /// <c>X-GitHub-Request-Id</c>, and 7a's three kept headers.
    /// </summary>
    private sealed class GitHubProfile(GitHubEndpoints endpoints, TimeProvider clock) : HostingHttpProfile
    {
        /// <summary>
        /// The headers kept with an ETag: a 304 need not repeat them (RFC 9110 15.4.5), and without
        /// the kept <c>Link</c> a repeated list would stop at its first page.
        /// </summary>
        private static readonly string[] Kept = ["Link", SsoHeader, ScopesHeader];

        public override HostingProvider Provider => HostingProvider.GitHub;

        public override Uri TokenOrigin => endpoints.ApiBase;

        public override JsonSerializerContext Json => GitHubJsonContext.Default;

        public override IReadOnlyCollection<string> KeptHeaderNames => Kept;

        public override void Authorize(HttpRequestMessage request, string token) => request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        public override void AddHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("Accept", GitHubApi.MediaType);
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubApi.Version);
        }

        public override bool SucceedsEmpty(int status) => status == 409;

        public override Uri? NextPage(HostingExchange exchange) => HostingLinks.Next(exchange.Headers);

        public override string? RequestIdOf(IReadOnlyDictionary<string, string> headers) => Header(headers, RequestIdHeader);

        /// <summary>The failure an answer that is neither a value, a redirect nor a 409 stands for (7a design 6.2's table, in order).</summary>
        public override HostingFailure Classify(HostingExchange exchange)
        {
            var status = exchange.Status;
            var error = ErrorOf(exchange.Body);
            var message = error?.Message is { Length: > 0 } words ? words : null;

            // An unsupported X-GitHub-Api-Version is a 400 "Bad Request" whose explanation is errors
            // as one string (the live run of 2026-10-30, D342): the first item's message.
            var explained = error?.Errors is [{ Message: { Length: > 0 } first }, ..] ? first : null;
            var fallback = exchange.Reason is { Length: > 0 } reason ? reason : string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");
            string? Of(string name) => Header(exchange.Headers, name);
            static HostingFailure Fail(HostingFailureKind kind, string text, string? detail = null, DateTimeOffset? retryAt = null) => new(kind, text, detail, retryAt);

            var limited = status is 403 or 429;
            if (status == 401)
            {
                return Fail(HostingFailureKind.BadCredentials, message ?? fallback);
            }

            if (status == 403 && Of(SsoHeader) is { } sso && sso.StartsWith("required", StringComparison.OrdinalIgnoreCase))
            {
                return Fail(HostingFailureKind.SsoRequired, message ?? fallback, SsoUrl(sso));
            }

            if (limited && Of("X-RateLimit-Remaining")?.Trim() == "0")
            {
                var reset = long.TryParse(Of("X-RateLimit-Reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                    : (DateTimeOffset?)null;
                return Fail(HostingFailureKind.RateLimited, message ?? fallback, retryAt: reset);
            }

            if (limited && (Of("Retry-After") is not null || message?.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase) == true))
            {
                return Fail(HostingFailureKind.RateLimited, message ?? fallback, retryAt: RetryAfter(Of("Retry-After")));
            }

            return status switch
            {
                403 => Fail(HostingFailureKind.Forbidden, message ?? fallback, Of("X-Accepted-GitHub-Permissions")),
                404 => Fail(HostingFailureKind.NotFound, message ?? fallback),
                410 => Fail(HostingFailureKind.ApiVersion, message ?? fallback),
                400 when NamesVersionHeader(explained) => Fail(HostingFailureKind.ApiVersion, explained),
                400 when NamesVersionHeader(message) => Fail(HostingFailureKind.ApiVersion, message),
                422 => Fail(HostingFailureKind.Validation, message ?? fallback, ValidationDetail(error)),
                >= 500 and < 600 => Fail(HostingFailureKind.ServerError, message ?? HostingHttpTexts.ServerError(Provider, status)),
                _ => Fail(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(Provider)),
            };
        }

        /// <summary>When a secondary rate limit ends: now plus <c>Retry-After</c>'s seconds, its date, or 60 s without one.</summary>
        private DateTimeOffset RetryAfter(string? header)
        {
            var now = clock.GetUtcNow();
            if (header is null)
            {
                return now + SecondaryRetry;
            }

            if (long.TryParse(header.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return now + TimeSpan.FromSeconds(Math.Min(seconds, 86400));
            }

            return DateTimeOffset.TryParse(header, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : now + SecondaryRetry;
        }
    }

    /// <summary>
    /// What a list's pages said (5.3): the first page's <c>X-OAuth-Scopes</c>, and the first
    /// <c>partial-results</c> <c>X-GitHub-SSO</c>, else the last page's; and the headers of the last
    /// page read, by which a reply is known to be of a page.
    /// </summary>
    private sealed class PageHeaders
    {
        private IReadOnlyDictionary<string, string>? _last;

        public string? Scopes { get; private set; }

        public string? Sso { get; private set; }

        public void Observe(IReadOnlyDictionary<string, string> headers)
        {
            _last = headers;
            Scopes ??= Header(headers, ScopesHeader);
            if (Sso is null || !Sso.StartsWith("partial-results", StringComparison.OrdinalIgnoreCase))
            {
                Sso = Header(headers, SsoHeader) ?? Sso;
            }
        }

        public bool Saw(IReadOnlyDictionary<string, string> headers) => ReferenceEquals(_last, headers);
    }
}
