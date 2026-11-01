using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Http;

/// <summary>
/// The shared HTTP core of the hosting providers' API clients (docs/phase-7b-design.md 5.2; D284,
/// D285, D358, D368), driven by one <see cref="HostingHttpProfile"/>: one <see cref="HttpClient"/>,
/// one request at a time, every request with 30 s once sent and the caller's cancellation (which
/// throws <see cref="OperationCanceledException"/>); the core's <c>User-Agent</c> and the profile's
/// headers on every request, the token only to the profile's origin; redirects repeated by hand
/// on that origin only, after the profile has had its say; bodies of at most 10 MB, a leading
/// byte order mark removed; ETags kept in memory per token fingerprint and URL, with the body and
/// the profile's headers, at most 512 of them and 32 MB of bodies, the oldest going first, and all
/// forgotten by <see cref="ForgetKept"/>; pages followed while they stay under the origin's path.
/// Every failure it finishes (each one an answer or no answer gives, and those a client passes to
/// <see cref="Failed"/> or <see cref="MalformedAnswer"/>) names the profile's provider, its address
/// without the query and its request id, with e-mail addresses screened out of its message and
/// detail, and writes one warning that names neither a header, a body, a query nor a token.
/// </summary>
internal sealed partial class HostingHttpClient : IDisposable
{
    /// <summary>The largest body read: anything longer is <see cref="HostingFailureKind.Malformed"/>.</summary>
    public const int MaxBodyBytes = 10 * 1024 * 1024;

    /// <summary>The redirects one request follows at most.</summary>
    public const int MaxRedirects = 3;

    /// <summary>The answers whose ETag and body are kept at most; the oldest goes first.</summary>
    public const int MaxKeptAnswers = 512;

    /// <summary>The bytes the kept answers' bodies take at most, unless the constructor says otherwise; the oldest go first.</summary>
    public const long DefaultKeptBytes = 32L * 1024 * 1024;

    /// <summary>What each e-mail-shaped run of a failure's message and detail becomes (D368).</summary>
    public const string ScreenedEmail = "<e-mail>";

    /// <summary>How long a request has once sent, unless the constructor says otherwise.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    private readonly HostingHttpProfile _profile;
    private readonly HttpClient _http;
    private readonly IAppLog _log;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Lock _keptGate = new();
    private readonly long _keptBudget;

    // Under _keptGate: the kept answers, their keys from the oldest stored to the newest, and the
    // bytes of their bodies.
    private readonly Dictionary<(string Fingerprint, string Url), KeptAnswer> _kept = [];
    private readonly Queue<(string Fingerprint, string Url)> _keptOrder = new();
    private long _keptBytes;

    /// <summary>
    /// A client over one handler; <paramref name="timeout"/> is 30 s and <paramref name="keptBytes"/>
    /// 32 MB unless given. The time limit runs on real time, whatever <paramref name="clock"/> is.
    /// </summary>
    public HostingHttpClient(HostingHttpProfile profile, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null, long? keptBytes = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        _profile = profile;
        _log = log;
        Clock = clock;
        _timeout = timeout ?? DefaultTimeout;
        _keptBudget = keptBytes ?? DefaultKeptBytes;
        _http = new HttpClient(createHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary><c>VisualCommit/&lt;version&gt;</c>: the Hosting assembly's informational version up to its <c>+</c>.</summary>
    public static string UserAgent { get; } = "VisualCommit/" + VersionOf(typeof(HostingHttpClient).Assembly);

    /// <summary>The profile the client sends by.</summary>
    public HostingHttpProfile Profile => _profile;

    /// <summary>The clock the profile's client reads (a rate limit's end); the time limit does not.</summary>
    public TimeProvider Clock { get; }

    private string Name => HostingProviders.DisplayName(_profile.Provider);

    /// <summary>
    /// One request of <paramref name="method"/> to <paramref name="url"/>, with <paramref name="body"/>
    /// as JSON in UTF-8 when given (a type of the profile's <see cref="HostingHttpProfile.Json"/>),
    /// its answer parsed as <paramref name="type"/>. Runs on the thread pool. The reply's
    /// <see cref="HostingReply{T}.Next"/> is null: only <see cref="GetPagesAsync{T}"/> pages.
    /// </summary>
    public Task<HostingReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(type);
        return Task.Run(async () => (await SendCoreAsync(method, token, url, body, type, ct).ConfigureAwait(false)).Reply);
    }

    /// <summary>
    /// The pages of a list from <paramref name="first"/>, following the profile's
    /// <see cref="HostingHttpProfile.NextPage"/> as given while it stays on the token's origin and
    /// under its path, up to <paramref name="maxPages"/> (<see cref="HostingReply{T}.Next"/> is
    /// the next page left then). A failing page fails the whole list. <paramref name="pageHeaders"/>
    /// is given each page's headers whose answer succeeded, in order, before the next is asked.
    /// </summary>
    public Task<HostingReply<List<T>>> GetPagesAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages, CancellationToken ct,
        Action<IReadOnlyDictionary<string, string>>? pageHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        return Task.Run(() => GetPagesCoreAsync(token, first, type, maxPages, pageHeaders, ct));
    }

    /// <summary>
    /// An answer that succeeded but lacks what the app needs: <see cref="HostingFailureKind.Malformed"/>
    /// with the provider's text, the address without its query, and the warning the core writes for one.
    /// </summary>
    public HostingFailure MalformedAnswer(HttpMethod method, Uri url, string? requestId) =>
        Failed(method, url, new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), requestId);

    /// <summary>
    /// A failure the profile's client built itself from a reply that succeeded (Azure DevOps'
    /// anonymous connectionData, say), finished as the core finishes its own: the profile's
    /// provider; the address without its query and <paramref name="requestId"/> where
    /// <paramref name="failure"/> left them null; each e-mail-shaped run of its message and detail
    /// as <see cref="ScreenedEmail"/> (D368); and one warning that names its kind.
    /// </summary>
    public HostingFailure Failed(HttpMethod method, Uri url, HostingFailure failure, string? requestId)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(failure);
        var finished = Finish(failure, url, requestId);
        Warn(method.Method, url, finished.Kind.ToString(), finished.RequestId);
        return finished;
    }

    /// <summary>
    /// Forgets every kept answer, whatever its token (a sign-out): the next request of each URL is
    /// a full one. An answer a request still in flight brings is kept as usual.
    /// </summary>
    public void ForgetKept()
    {
        lock (_keptGate)
        {
            _kept.Clear();
            _keptOrder.Clear();
            _keptBytes = 0;
        }
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Whether two addresses have the same scheme, host and port.</summary>
    public static bool SameOrigin(Uri a, Uri b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;
    }

    /// <summary><c>&lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;</c> of <paramref name="uri"/>, for a log line: never its path or query.</summary>
    public static string OriginOf(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return FormattableString.Invariant($"{uri.Scheme}://{uri.IdnHost}:{uri.Port}");
    }

    /// <summary>Whether <paramref name="status"/> is a redirect the client repeats: 301, 302, 307 or 308.</summary>
    public static bool IsRedirect(int status) => status is 301 or 302 or 307 or 308;

    /// <summary>The address <paramref name="location"/> names, resolved against <paramref name="current"/>; null when there is none.</summary>
    public static Uri? Resolve(Uri current, Uri? location) =>
        location is null ? null
        : location.IsAbsoluteUri ? location
        : Uri.TryCreate(current, location, out var resolved) ? resolved : null;

    /// <summary>
    /// The body of <paramref name="content"/>, or null when it is longer than
    /// <paramref name="maxBytes"/> (by its <c>Content-Length</c>, or once that many were read).
    /// </summary>
    public static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
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

    /// <summary><paramref name="text"/> with each e-mail-shaped run (an <c>@</c> between non-blank characters, the whole run of them) as <see cref="ScreenedEmail"/> (D368).</summary>
    public static string ScreenEmail(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains('@', StringComparison.Ordinal) ? EmailPattern().Replace(text, ScreenedEmail) : text;
    }

    /// <summary>The address without its query and fragment.</summary>
    public static Uri WithoutQuery(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new Uri(url.GetLeftPart(UriPartial.Path));
    }

    private static string VersionOf(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    private async Task<HostingReply<List<T>>> GetPagesCoreAsync<T>(string token, Uri first, JsonTypeInfo<List<T>> type, int maxPages,
        Action<IReadOnlyDictionary<string, string>>? pageHeaders, CancellationToken ct)
    {
        var items = new List<T>();
        var url = first;
        for (var page = 1; ; page++)
        {
            var (reply, exchange) = await SendCoreAsync(HttpMethod.Get, token, url, null, type, ct).ConfigureAwait(false);
            if (!reply.Succeeded || reply.Value is null)
            {
                return reply;
            }

            pageHeaders?.Invoke(reply.Headers);
            items.AddRange(reply.Value);
            var next = _profile.NextPage(exchange);
            if (next is null)
            {
                return reply with { Value = items };
            }

            var origin = _profile.TokenOrigin;
            if (!SameOrigin(next, origin) || !next.AbsolutePath.StartsWith(origin.AbsolutePath, StringComparison.Ordinal))
            {
                var failure = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, reply.RequestId);
                Warn(HttpMethod.Get.Method, exchange.Url, $"Link to {OriginOf(next)}", failure.RequestId);
                return reply with { Value = default, Failure = failure };
            }

            if (page >= maxPages)
            {
                return reply with { Value = items, Next = next };
            }

            url = next;
        }
    }

    /// <summary>One request and its answer, read and classified; with the exchange that answered.</summary>
    private async Task<(HostingReply<T> Reply, HostingExchange Exchange)> SendCoreAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var content = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body, _profile.Json.GetTypeInfo(body.GetType())
            ?? throw new ArgumentException($"{body.GetType().Name} is not one of {HostingProviders.Possessive(_profile.Provider)} shapes.", nameof(body)));
        var (exchange, early) = await ExchangeAsync(method, token, Fingerprint(token), url, content, ct).ConfigureAwait(false);
        var requestId = _profile.RequestIdOf(exchange.Headers);
        HostingReply<T> Reply(T? value, HostingFailure? failure) => new(value, failure, exchange.Status, null, exchange.Headers, requestId);

        if (early is not null)
        {
            return (Reply(default, early), exchange);
        }

        var status = exchange.Status;
        var success = status is >= 200 and < 300 || status == 304;
        if (success && _profile.RefuseSuccess(exchange) is { } refused)
        {
            return (Reply(default, Refused(exchange, refused, requestId)), exchange);
        }

        if (_profile.SucceedsEmpty(status))
        {
            return (Reply(default, null), exchange);
        }

        if (!success)
        {
            return (Reply(default, Refused(exchange, _profile.Classify(exchange), requestId)), exchange);
        }

        if (Parse(exchange.Body, type) is { } value)
        {
            return (Reply(value, null), exchange);
        }

        var malformed = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, requestId);
        Warn(method.Method, exchange.Url, "Malformed", malformed.RequestId);
        return (Reply(default, malformed), exchange);
    }

    /// <summary>
    /// Sends the request, repeating a redirect on the token's origin up to three times, and reads
    /// the answer's body (at most 10 MB). A 304 to a kept answer gives the kept body. One at a
    /// time; the time limit starts once the request is sent. A failure here is finished and warned.
    /// </summary>
    private async Task<(HostingExchange Exchange, HostingFailure? Failure)> ExchangeAsync(HttpMethod method, string token, string fingerprint, Uri url, byte[]? content, CancellationToken ct)
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
                    HostingExchange Answer(byte[] body) => new(method, current, status, body, headers) { Reason = response.ReasonPhrase };
                    if (IsRedirect(status))
                    {
                        var redirect = Answer([]);
                        var location = Resolve(current, response.Headers.Location);
                        if (location is not null && _profile.RefuseRedirect(redirect, location) is { } refused)
                        {
                            return (redirect, Refused(redirect, refused, _profile.RequestIdOf(headers)));
                        }

                        var refusal = location is null ? $"{status} without Location"
                            : hop >= MaxRedirects ? $"{status}, a fourth redirect"
                            : !SameOrigin(location, _profile.TokenOrigin) ? $"redirected to {OriginOf(location)}"
                            : null;
                        if (refusal is not null)
                        {
                            return (redirect, Stopped(redirect, refusal));
                        }

                        current = location!;
                        continue;
                    }

                    var read = await ReadBoundedAsync(response.Content, MaxBodyBytes, limit.Token).ConfigureAwait(false);
                    if (read is null)
                    {
                        var oversized = Answer([]);
                        return (oversized, Stopped(oversized, "an answer over 10 MB"));
                    }

                    var body = WithoutByteOrderMark(read);
                    if (status == 304 && kept is not null)
                    {
                        // The 304's own headers over the kept ones, as an HTTP cache does.
                        foreach (var (name, value) in kept.Headers)
                        {
                            headers.TryAdd(name, value);
                        }

                        return (Answer(kept.Body), null);
                    }

                    if (method == HttpMethod.Get && status == 200 && headers.GetValueOrDefault("ETag") is { Length: > 0 } etag)
                    {
                        Keep(fingerprint, current, new KeptAnswer(etag, body, KeptHeadersOf(headers)));
                    }

                    return (Answer(body), null);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested)
                {
                    Warn(method.Method, current, "time-out", null);
                    return Unanswered(method, current, HostingHttpTexts.Timeout(_profile.Provider));
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
                {
                    // A cancellation that is neither the caller's nor the time limit (the handler's
                    // connect time-out, a client disposed mid-request) is no answer.
                    Warn(method.Method, current, "Network", null, ex);
                    return Unanswered(method, current, HostingHttpTexts.Network(_profile.Provider));
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
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        _profile.AddHeaders(request);
        if (SameOrigin(url, _profile.TokenOrigin))
        {
            _profile.Authorize(request, token);
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

    /// <summary>A failure the profile gave for <paramref name="exchange"/>, finished, with its warning.</summary>
    private HostingFailure Refused(HostingExchange exchange, HostingFailure failure, string? requestId)
    {
        var finished = Finish(failure, exchange.Url, requestId);
        Warn(exchange.Method.Method, exchange.Url, _profile.WarningOf(exchange, finished), finished.RequestId);
        return finished;
    }

    /// <summary>The core's own refusal of an answer (a redirect it does not follow, a body over 10 MB): <see cref="HostingFailureKind.Malformed"/>, with its warning.</summary>
    private HostingFailure Stopped(HostingExchange exchange, string warning)
    {
        var failure = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, _profile.RequestIdOf(exchange.Headers));
        Warn(exchange.Method.Method, exchange.Url, warning, failure.RequestId);
        return failure;
    }

    private (HostingExchange Exchange, HostingFailure Failure) Unanswered(HttpMethod method, Uri url, string message) =>
        (new HostingExchange(method, url, 0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
         Finish(new HostingFailure(HostingFailureKind.Network, message), url, null));

    /// <summary>
    /// What every failure gets (5.2): the profile's provider; the address without its query and
    /// the request id where the failure left them null; e-mail addresses screened out of its
    /// message and detail (D368).
    /// </summary>
    private HostingFailure Finish(HostingFailure failure, Uri url, string? requestId) => failure with
    {
        Provider = _profile.Provider,
        Url = failure.Url ?? WithoutQuery(url),
        RequestId = failure.RequestId ?? requestId,
        Message = ScreenEmail(failure.Message),
        Detail = failure.Detail is null ? null : ScreenEmail(failure.Detail),
    };

    /// <summary>
    /// The one warning of a failure: <c>&lt;name&gt;: &lt;method&gt; &lt;path&gt; failed: &lt;status or kind&gt; (request &lt;id&gt;).</c>,
    /// the path without its query; the exception of a request that got no answer.
    /// </summary>
    private void Warn(string method, Uri url, string statusOrKind, string? requestId, Exception? exception = null)
    {
        var request = requestId is { Length: > 0 } ? $" (request {requestId})" : string.Empty;
        _log.Warning($"{Name}: {method} {url.AbsolutePath} failed: {statusOrKind}{request}.", exception);
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

    private static byte[] WithoutByteOrderMark(byte[] body) => body.AsSpan().StartsWith(ByteOrderMark) ? body[ByteOrderMark.Length..] : body;

    /// <summary>The answer's headers and its content's, without case; a header given twice is joined with <c>", "</c>.</summary>
    private static Dictionary<string, string> HeadersOf(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(HttpHeadersNonValidated given)
        {
            foreach (var (name, values) in given)
            {
                var joined = string.Join(", ", values);
                headers[name] = headers.TryGetValue(name, out var earlier) ? earlier + ", " + joined : joined;
            }
        }

        Add(response.Headers.NonValidated);
        Add(response.Content.Headers.NonValidated);
        return headers;
    }

    /// <summary>Those of <paramref name="headers"/> that the profile's <see cref="HostingHttpProfile.KeptHeaderNames"/> names.</summary>
    private Dictionary<string, string> KeptHeadersOf(Dictionary<string, string> headers)
    {
        var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _profile.KeptHeaderNames)
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

    [GeneratedRegex(@"\S+@\S+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    /// <summary>The last 200 answer of a URL for one token: its ETag, body and the profile's kept headers, in memory only.</summary>
    private sealed record KeptAnswer(string ETag, byte[] Body, IReadOnlyDictionary<string, string> Headers);
}
