using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.GitHub;

/// <summary>
/// One answer of <see cref="GitHubClient"/>: its value, or why it failed (docs/phase-7a-design.md 6.2).
/// </summary>
/// <param name="Value">The parsed body; null on a failure and on a 409.</param>
/// <param name="Failure">Why the request failed; null when it did not (a 409 included, A6's empty repository).</param>
/// <param name="Status">The last answer's status code (304 when the kept body was used); 0 when none came.</param>
/// <param name="MoreExist">A paged request stopped at its cap with a <c>next</c> page left.</param>
/// <param name="OAuthScopes"><c>X-OAuth-Scopes</c> (a classic token's scopes).</param>
/// <param name="Sso"><c>X-GitHub-SSO</c>; for pages, the first <c>partial-results</c> one, else the last page's.</param>
/// <param name="RequestId"><c>X-GitHub-Request-Id</c> of the last answer.</param>
internal sealed record GitHubReply<T>(T? Value, HostingFailure? Failure, int Status, bool MoreExist, string? OAuthScopes, string? Sso, string? RequestId)
{
    public bool Succeeded => Failure is null;
}

/// <summary>
/// GitHub's REST API over one <see cref="HttpClient"/> (docs/phase-7a-design.md 6.2, D285):
/// GitHub's headers on every request and the token only to the API's own scheme, host and port;
/// redirects repeated by hand on the API's address only; <c>Link</c> pages followed on it only; one
/// request at a time; ETags kept in memory per token fingerprint and URL, with the body and the
/// headers read from an answer, at most 512 of them and 32 MB of bodies, the oldest going first,
/// and all forgotten at a sign-out (<see cref="ForgetKept"/>); every answer that is not
/// a value classified into a <see cref="HostingFailure"/>, with one warning that names neither a
/// header, a body, a query nor a token. Every request has 30 s once sent and the caller's
/// cancellation, which throws <see cref="OperationCanceledException"/>.
/// </summary>
internal sealed partial class GitHubClient : IDisposable
{
    /// <summary>The fixed text of a request that got no answer (D284).</summary>
    public const string NetworkText = "Could not reach GitHub.";

    /// <summary>The fixed text of a request whose 30 s ran out (D284).</summary>
    public const string TimeoutText = "GitHub did not answer within 30 seconds.";

    /// <summary>The text of an answer that could not be read.</summary>
    public const string MalformedText = "GitHub's answer could not be read.";

    /// <summary>The largest body read: anything longer is <see cref="HostingFailureKind.Malformed"/>.</summary>
    public const int MaxBodyBytes = 10 * 1024 * 1024;

    /// <summary>The redirects one request follows at most.</summary>
    public const int MaxRedirects = 3;

    /// <summary>How long a request has once sent, unless the constructor says otherwise.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The answers whose ETag and body are kept at most; the oldest goes first.</summary>
    private const int MaxKeptAnswers = 512;

    /// <summary>The bytes the kept answers' bodies take at most, unless the constructor says otherwise; the oldest go first.</summary>
    private const long DefaultKeptBytes = 32L * 1024 * 1024;

    private static readonly TimeSpan SecondaryRetry = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The headers the client reads from an answer beside its body, kept with its ETag: a 304 need
    /// not repeat them (RFC 9110 15.4.5), and without the kept <c>Link</c> a repeated list would stop
    /// at its first page.
    /// </summary>
    private static readonly string[] KeptHeaderNames = ["Link", "X-GitHub-SSO", "X-OAuth-Scopes"];

    private readonly GitHubEndpoints _endpoints;
    private readonly HttpClient _http;
    private readonly IAppLog _log;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Lock _keptGate = new();
    private readonly long _keptBudget;

    // Under _keptGate: the kept answers, their keys from the oldest stored to the newest, and the
    // bytes of their bodies.
    private readonly Dictionary<(string Fingerprint, string Url), KeptAnswer> _kept = [];
    private readonly Queue<(string Fingerprint, string Url)> _keptOrder = new();
    private long _keptBytes;

    /// <summary>A client over one handler; <paramref name="keptBytes"/> bounds the kept answers' bodies, 32 MB unless given.</summary>
    public GitHubClient(GitHubEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null, long? keptBytes = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        _endpoints = endpoints;
        _log = log;
        _clock = clock;
        _timeout = timeout ?? DefaultTimeout;
        _keptBudget = keptBytes ?? DefaultKeptBytes;
        _http = new HttpClient(createHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary><c>VisualCommit/&lt;version&gt;</c>: the Hosting assembly's informational version up to its <c>+</c>.</summary>
    public static string UserAgent { get; } = "VisualCommit/" + VersionOf(typeof(GitHubClient).Assembly);

    public GitHubEndpoints Endpoints => _endpoints;

    /// <summary>
    /// One request of <paramref name="method"/> to <paramref name="url"/>, with <paramref name="body"/>
    /// as JSON when given (a type of <see cref="GitHubJsonContext"/>), its answer parsed as
    /// <paramref name="type"/>. Runs on the thread pool.
    /// </summary>
    public Task<GitHubReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(type);
        return Task.Run(async () => (await SendCoreAsync(method, token, url, body, type, ct).ConfigureAwait(false)).Reply);
    }

    /// <summary>
    /// The pages of a list from <paramref name="first"/>, following <c>Link: rel="next"</c> as
    /// given while it stays on the API's scheme, host, port and path, up to
    /// <paramref name="maxPages"/> (<c>MoreExist</c> when a <c>next</c> remains). A failing page
    /// fails the whole list.
    /// </summary>
    public Task<GitHubReply<List<T>>> GetPagesAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        return Task.Run(() => GetPagesCoreAsync(token, first, type, maxPages, ct));
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Forgets every kept answer, whatever its token (a sign-out, 7.5): the next request of each
    /// URL is a full one. An answer a request still in flight brings is kept as usual.
    /// </summary>
    internal void ForgetKept()
    {
        lock (_keptGate)
        {
            _kept.Clear();
            _keptOrder.Clear();
            _keptBytes = 0;
        }
    }

    /// <summary>Whether two addresses have the same scheme, host and port.</summary>
    internal static bool SameOrigin(Uri a, Uri b) => GitHubUrls.SameOrigin(a, b);

    /// <summary>Whether <paramref name="status"/> is a redirect the client repeats: 301, 302, 307 or 308.</summary>
    internal static bool IsRedirect(int status) => status is 301 or 302 or 307 or 308;

    /// <summary>
    /// The body of <paramref name="content"/>, or null when it is longer than
    /// <paramref name="maxBytes"/> (by its <c>Content-Length</c>, or once that many were read).
    /// </summary>
    internal static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        if (content.Headers.ContentLength is { } length && length > maxBytes)
        {
            return null;
        }

        var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
    }

    /// <summary>The address <paramref name="location"/> names, resolved against <paramref name="current"/>; null when there is none.</summary>
    internal static Uri? Resolve(Uri current, Uri? location) =>
        location is null ? null
        : location.IsAbsoluteUri ? location
        : Uri.TryCreate(current, location, out var resolved) ? resolved : null;

    private static string VersionOf(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    private async Task<GitHubReply<List<T>>> GetPagesCoreAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct)
    {
        var items = new List<T>();
        var url = first;
        string? scopes = null;
        string? sso = null;
        for (var page = 1; ; page++)
        {
            var (reply, link, answered) = await SendCoreAsync(HttpMethod.Get, token, url, null, type, ct).ConfigureAwait(false);
            if (!reply.Succeeded || reply.Value is null)
            {
                return reply;
            }

            items.AddRange(reply.Value);
            scopes ??= reply.OAuthScopes;
            if (sso is null || !sso.StartsWith("partial-results", StringComparison.OrdinalIgnoreCase))
            {
                sso = reply.Sso ?? sso;
            }

            var next = NextOf(link);
            if (next is null)
            {
                return new GitHubReply<List<T>>(items, null, reply.Status, false, scopes, sso, reply.RequestId);
            }

            if (!SameOrigin(next, _endpoints.ApiBase) || !next.AbsolutePath.StartsWith(_endpoints.ApiBase.AbsolutePath, StringComparison.Ordinal))
            {
                var failure = Failed(HostingFailureKind.Malformed, MalformedText, answered, reply.RequestId);
                Warn("GET", answered, $"Link to {GitHubUrls.OriginOf(next)}", reply.RequestId);
                return new GitHubReply<List<T>>(default, failure, reply.Status, false, scopes, sso, reply.RequestId);
            }

            if (page >= maxPages)
            {
                return new GitHubReply<List<T>>(items, null, reply.Status, true, scopes, sso, reply.RequestId);
            }

            url = next;
        }
    }

    /// <summary>One request and its answer, read and classified; with the answer's <c>Link</c> header and the address that answered.</summary>
    private async Task<(GitHubReply<T> Reply, string? Link, Uri Answered)> SendCoreAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var content = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body, GitHubJsonContext.Default.GetTypeInfo(body.GetType())
            ?? throw new ArgumentException($"{body.GetType().Name} is not one of GitHub's shapes.", nameof(body)));
        var fingerprint = Fingerprint(token);
        var exchange = await ExchangeAsync(method, token, fingerprint, url, content, ct).ConfigureAwait(false);
        var headers = exchange.Headers;
        string? Header(string name) => headers.GetValueOrDefault(name);
        var requestId = Header("X-GitHub-Request-Id");
        var scopes = Header("X-OAuth-Scopes");
        var sso = Header("X-GitHub-SSO");
        GitHubReply<T> Reply(T? value, HostingFailure? failure) => new(value, failure, exchange.Status, false, scopes, sso, requestId);

        if (exchange.Failure is { } early)
        {
            return (Reply(default, early), null, exchange.Url);
        }

        var status = exchange.Status;
        if (status is >= 200 and < 300 || status == 304)
        {
            if (Parse(exchange.Body, type) is { } value)
            {
                return (Reply(value, null), Header("Link"), exchange.Url);
            }

            Warn(method.Method, exchange.Url, "Malformed", requestId);
            return (Reply(default, Failed(HostingFailureKind.Malformed, MalformedText, exchange.Url, requestId)), null, exchange.Url);
        }

        if (status == 409)
        {
            return (Reply(default, null), null, exchange.Url);
        }

        var failure = Classify(exchange, requestId);
        Warn(method.Method, exchange.Url, status.ToString(CultureInfo.InvariantCulture), requestId);
        return (Reply(default, failure), null, exchange.Url);
    }

    /// <summary>
    /// Sends the request, repeating a redirect on the API's address up to three times, and reads
    /// the answer's body (at most 10 MB). A 304 to a kept answer gives the kept body. One at a
    /// time; the 30 s start once the request is sent.
    /// </summary>
    private async Task<Exchange> ExchangeAsync(HttpMethod method, string token, string fingerprint, Uri url, byte[]? content, CancellationToken ct)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(_timeout);
            var current = url;
            for (var hop = 0; ; hop++)
            {
                var kept = method == HttpMethod.Get ? KeptFor(fingerprint, current) : null;
                using var request = Request(method, token, current, content, kept);
                try
                {
                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
                    var status = (int)response.StatusCode;
                    var headers = HeadersOf(response);
                    var requestId = headers.GetValueOrDefault("X-GitHub-Request-Id");
                    if (IsRedirect(status))
                    {
                        var location = Resolve(current, response.Headers.Location);
                        var refusal = location is null ? $"{status} without Location"
                            : hop >= MaxRedirects ? $"{status}, a fourth redirect"
                            : !SameOrigin(location, _endpoints.ApiBase) ? $"redirected to {GitHubUrls.OriginOf(location)}"
                            : null;
                        if (refusal is not null)
                        {
                            Warn(method.Method, current, refusal, requestId);
                            return new Exchange(status, [], headers, current, response.ReasonPhrase, Failed(HostingFailureKind.Malformed, MalformedText, current, requestId));
                        }

                        current = location!;
                        continue;
                    }

                    var body = await ReadBoundedAsync(response.Content, MaxBodyBytes, limit.Token).ConfigureAwait(false);
                    if (body is null)
                    {
                        Warn(method.Method, current, "an answer over 10 MB", requestId);
                        return new Exchange(status, [], headers, current, response.ReasonPhrase, Failed(HostingFailureKind.Malformed, MalformedText, current, requestId));
                    }

                    if (status == 304 && kept is not null)
                    {
                        // The 304's own headers over the kept ones, as an HTTP cache does.
                        foreach (var (name, value) in kept.Headers)
                        {
                            headers.TryAdd(name, value);
                        }

                        return new Exchange(status, kept.Body, headers, current, response.ReasonPhrase, null);
                    }

                    if (method == HttpMethod.Get && status == 200 && headers.GetValueOrDefault("ETag") is { Length: > 0 } etag)
                    {
                        Keep(fingerprint, current, new KeptAnswer(etag, body, KeptHeadersOf(headers)));
                    }

                    return new Exchange(status, body, headers, current, response.ReasonPhrase, null);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested)
                {
                    Warn(method.Method, current, "time-out", null);
                    return Unanswered(current, TimeoutText);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
                {
                    // A cancellation that is neither the caller's nor the 30 s (the handler's connect
                    // time-out, a client disposed mid-request) is no answer.
                    Warn(method.Method, current, "Network", null, ex);
                    return Unanswered(current, NetworkText);
                }
            }
        }
        finally
        {
            _one.Release();
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string token, Uri url, byte[]? content, KeptAnswer? kept)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Accept", GitHubApi.MediaType);
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubApi.Version);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (SameOrigin(url, _endpoints.ApiBase))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        if (kept is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", kept.ETag);
        }

        if (content is not null)
        {
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        return request;
    }

    /// <summary>The failure an answer that is neither a value, a redirect nor a 409 stands for (6.2's table, in order).</summary>
    private HostingFailure Classify(Exchange exchange, string? requestId)
    {
        var status = exchange.Status;
        var error = Parse(exchange.Body, GitHubJsonContext.Default.GitHubErrorJson);
        var message = error?.Message is { Length: > 0 } words ? words : null;
        var fallback = exchange.Reason is { Length: > 0 } reason ? reason : string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");
        string? Header(string name) => exchange.Headers.GetValueOrDefault(name);
        HostingFailure Fail(HostingFailureKind kind, string text, string? detail = null, DateTimeOffset? retryAt = null) =>
            new(kind, text, detail, retryAt, WithoutQuery(exchange.Url), requestId);

        var limited = status is 403 or 429;
        if (status == 401)
        {
            return Fail(HostingFailureKind.BadCredentials, message ?? fallback);
        }

        if (status == 403 && Header("X-GitHub-SSO") is { } sso && sso.StartsWith("required", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(HostingFailureKind.SsoRequired, message ?? fallback, SsoUrl(sso));
        }

        if (limited && Header("X-RateLimit-Remaining")?.Trim() == "0")
        {
            var reset = long.TryParse(Header("X-RateLimit-Reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : (DateTimeOffset?)null;
            return Fail(HostingFailureKind.RateLimited, message ?? fallback, retryAt: reset);
        }

        if (limited && (Header("Retry-After") is not null || message?.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Fail(HostingFailureKind.RateLimited, message ?? fallback, retryAt: RetryAfter(Header("Retry-After")));
        }

        return status switch
        {
            403 => Fail(HostingFailureKind.Forbidden, message ?? fallback, Header("X-Accepted-GitHub-Permissions")),
            404 => Fail(HostingFailureKind.NotFound, message ?? fallback),
            410 => Fail(HostingFailureKind.ApiVersion, message ?? fallback),
            400 when message?.Contains("X-GitHub-Api-Version", StringComparison.OrdinalIgnoreCase) == true => Fail(HostingFailureKind.ApiVersion, message),
            422 => Fail(HostingFailureKind.Validation, message ?? fallback, ValidationDetail(error)),
            >= 500 and < 600 => Fail(HostingFailureKind.ServerError, message ?? string.Create(CultureInfo.InvariantCulture, $"GitHub had a problem: HTTP {status}.")),
            _ => Fail(HostingFailureKind.Malformed, MalformedText),
        };
    }

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

    /// <summary>When a secondary rate limit ends: now plus <c>Retry-After</c>'s seconds, its date, or 60 s without one.</summary>
    private DateTimeOffset RetryAfter(string? header)
    {
        var now = _clock.GetUtcNow();
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

    private static T? Parse<T>(byte[] body, JsonTypeInfo<T> type)
    {
        if (body.Length == 0)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(body, type);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>The URL of <c>rel="next"</c> in a <c>Link</c> header, as given; null when there is none or it is not an absolute address.</summary>
    private static Uri? NextOf(string? link)
    {
        if (link is null)
        {
            return null;
        }

        foreach (Match match in LinkPattern().Matches(link))
        {
            if (match.Groups["rel"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("next", StringComparer.OrdinalIgnoreCase))
            {
                return Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var next) ? next : null;
            }
        }

        return null;
    }

    [GeneratedRegex("<(?<url>[^>]*)>\\s*;\\s*rel=\"(?<rel>[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();

    private static Dictionary<string, string> HeadersOf(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        foreach (var (name, values) in response.Content.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        return headers;
    }

    private static Uri WithoutQuery(Uri url) => new(url.GetLeftPart(UriPartial.Path));

    private static HostingFailure Failed(HostingFailureKind kind, string message, Uri url, string? requestId) =>
        new(kind, message, Url: WithoutQuery(url), RequestId: requestId);

    private static Exchange Unanswered(Uri url, string message) =>
        new(0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), url, null, Failed(HostingFailureKind.Network, message, url, null));

    /// <summary>
    /// The one warning of a failure: <c>GitHub: &lt;method&gt; &lt;path&gt; failed: &lt;status or kind&gt; (request &lt;id&gt;).</c>,
    /// the path without its query; the exception of a request that got no answer.
    /// </summary>
    private void Warn(string method, Uri url, string statusOrKind, string? requestId, Exception? exception = null)
    {
        var request = requestId is { Length: > 0 } ? $" (request {requestId})" : string.Empty;
        _log.Warning($"GitHub: {method} {url.AbsolutePath} failed: {statusOrKind}{request}.", exception);
    }

    /// <summary>Those of <paramref name="headers"/> that <see cref="KeptHeaderNames"/> names.</summary>
    private static Dictionary<string, string> KeptHeadersOf(Dictionary<string, string> headers)
    {
        var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in KeptHeaderNames)
        {
            if (headers.TryGetValue(name, out var value))
            {
                kept[name] = value;
            }
        }

        return kept;
    }

    /// <summary>A token's SHA-256, by which kept answers are found: never the token itself.</summary>
    private static string Fingerprint(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private KeptAnswer? KeptFor(string fingerprint, Uri url)
    {
        lock (_keptGate)
        {
            return _kept.GetValueOrDefault((fingerprint, url.AbsoluteUri));
        }
    }

    // A new answer of a URL is the newest kept, an earlier one of it dropped; the oldest go first
    // while more than MaxKeptAnswers would be kept or their bodies would pass the budget. A body
    // over the whole budget is not kept.
    private void Keep(string fingerprint, Uri url, KeptAnswer answer)
    {
        var key = (fingerprint, url.AbsoluteUri);
        lock (_keptGate)
        {
            if (_kept.Remove(key, out var earlier))
            {
                _keptBytes -= earlier.Body.Length;
                var others = _keptOrder.Where(kept => kept != key).ToList();
                _keptOrder.Clear();
                foreach (var other in others)
                {
                    _keptOrder.Enqueue(other);
                }
            }

            if (answer.Body.Length > _keptBudget)
            {
                return;
            }

            while ((_kept.Count >= MaxKeptAnswers || _keptBytes + answer.Body.Length > _keptBudget) && _keptOrder.TryDequeue(out var oldest))
            {
                if (_kept.Remove(oldest, out var dropped))
                {
                    _keptBytes -= dropped.Body.Length;
                }
            }

            _keptOrder.Enqueue(key);
            _kept[key] = answer;
            _keptBytes += answer.Body.Length;
        }
    }

    /// <summary>The last 200 answer of a URL for one token: its ETag, body and the headers of <see cref="KeptHeaderNames"/>, in memory only.</summary>
    private sealed record KeptAnswer(string ETag, byte[] Body, IReadOnlyDictionary<string, string> Headers);

    /// <summary>What came back: the status, body and headers of the address that answered, or the failure that stopped it early.</summary>
    private sealed record Exchange(int Status, byte[] Body, IReadOnlyDictionary<string, string> Headers, Uri Url, string? Reason, HostingFailure? Failure);
}
