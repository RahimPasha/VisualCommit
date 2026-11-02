using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
/// without the userinfo and the query and its request id when that has a request id's shape
/// (<see cref="ShapedRequestId"/>, D382), with e-mail addresses screened out of its message, its
/// detail and its address's path (<see cref="ScreenPath"/>, D382), and the request's token as
/// <see cref="ScreenedToken"/> in all of them (and no request id that holds it), as a server can echo
/// it there (5.2's "never a token"; the core's own requests, and a client's failure given its
/// request's token), and writes one warning that names neither a header, a body, a query, a token,
/// an address nor an exception's text: an
/// exception is named by <see cref="Describe"/> alone, and none is passed to the log (D380).
/// Nothing an answer holds throws out of the core: a body that cannot be decompressed, a profile's
/// hook that throws on what the server sent, and a redirect or a next page to a host without an IDN
/// form (on no origin, <see cref="SameOrigin"/>), are <see cref="HostingFailureKind.Malformed"/> (D380).
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

    /// <summary>What each e-mail-shaped run of a failure's message and detail becomes (D368), and each path segment that holds one (D382).</summary>
    public const string ScreenedEmail = "<e-mail>";

    /// <summary>What each of a request's token's occurrences in a failure and its warning becomes, and each path segment that holds one.</summary>
    public const string ScreenedToken = "<token>";

    /// <summary>The full-width at sign (U+FF20), which the screen takes for <c>@</c> (D382).</summary>
    public const char FullWidthAt = '\uFF20';

    /// <summary>What <see cref="OriginOf"/> names for a host that has no IDN form (D380).</summary>
    public const string UnreadableHost = "(an unreadable host)";

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
    /// with the provider's text, the address without its query, and the warning the core writes for
    /// one; <paramref name="token"/>, the request's, screened as in <see cref="Failed"/>.
    /// </summary>
    public HostingFailure MalformedAnswer(HttpMethod method, Uri url, string? requestId, string? token = null) =>
        Failed(method, url, new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), requestId, token);

    /// <summary>
    /// A failure the profile's client built itself from a reply that succeeded (an answer that
    /// names an anonymous user, say), finished as the core finishes its own: the profile's
    /// provider; the address without its query and <paramref name="requestId"/> where
    /// <paramref name="failure"/> left them null; each e-mail-shaped run of its message and detail
    /// as <see cref="ScreenedEmail"/> (D368); and one warning that names its kind. Given the
    /// request's <paramref name="token"/>, each segment of the address's path that holds it
    /// (<see cref="ScreenPath"/>; its userinfo and query are dropped, its host kept as given) and
    /// each of its occurrences in the message, the detail and the warning is
    /// <see cref="ScreenedToken"/>, and a request id that holds it is dropped, as in the core's own
    /// failures (an address a client builds from a record whose path holds the token, D382);
    /// without one, none is screened, as for 7a's GitHub.
    /// </summary>
    public HostingFailure Failed(HttpMethod method, Uri url, HostingFailure failure, string? requestId, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(failure);
        var secrets = token is null ? [] : SecretsOf(token);
        var finished = Finish(failure, url, requestId, secrets);
        Warn(method.Method, url, finished.Kind.ToString(), finished.RequestId, secrets);
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

    /// <summary>
    /// Whether two addresses have the same scheme, host and port. An address whose host has no
    /// IDN form (<see cref="IdnHostOf"/> null) is on no origin, its own included: never throws (D380).
    /// </summary>
    public static bool SameOrigin(Uri a, Uri b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && IdnHostOf(a) is { } host
            && string.Equals(host, IdnHostOf(b), StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;
    }

    /// <summary>
    /// <c>&lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;</c> of <paramref name="uri"/>, for a log line: never its path or query,
    /// and <see cref="UnreadableHost"/> for a host without an IDN form, never the host as sent (D380).
    /// </summary>
    public static string OriginOf(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return FormattableString.Invariant($"{uri.Scheme}://{IdnHostOf(uri) ?? UnreadableHost}:{uri.Port}");
    }

    /// <summary>
    /// <paramref name="uri"/>'s <see cref="Uri.IdnHost"/>; null when it has none. <see cref="Uri.IdnHost"/>
    /// throws <see cref="UriFormatException"/> on hosts <see cref="Uri.TryCreate(string, UriKind, out Uri)"/>
    /// takes (U+200D, U+FF20, U+2488, U+FF0F, U+FF1A in the host, MEASURED on .NET 10), as a server's
    /// <c>Location</c> or <c>Link</c> can name them (D380).
    /// </summary>
    public static string? IdnHostOf(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        try
        {
            return uri.IdnHost;
        }
        catch (UriFormatException)
        {
            return null;
        }
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

    /// <summary>
    /// <paramref name="text"/> with each e-mail-shaped run (an <c>@</c>, or a full-width one,
    /// <see cref="FullWidthAt"/>, between non-blank characters, the whole run of them) as
    /// <see cref="ScreenedEmail"/> (D368, D382).
    /// </summary>
    public static string ScreenEmail(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return HasAt(text) ? EmailPattern().Replace(text, ScreenedEmail) : text;
    }

    /// <summary>
    /// <paramref name="path"/> (an address's escaped path) with each segment that holds an
    /// e-mail-shaped run once percent-decoded (so <c>%40</c> and a full-width at sign, sent as
    /// <c>%EF%BC%A0</c>, count) as <see cref="ScreenedEmail"/>, every other segment as it was
    /// (D382): a <c>Location</c> or a <c>Link</c> the server names can put an address in the path
    /// the core logs and keeps. A segment that holds one of <paramref name="secrets"/> (the request's
    /// token, in any case), as sent or once percent-decoded, becomes <see cref="ScreenedToken"/>, and a path that
    /// still holds one across its segments becomes <c>/&lt;token&gt;</c> whole: a server can echo the token there.
    /// </summary>
    public static string ScreenPath(string path, params string[] secrets)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(secrets);
        if (!HasAt(path) && !path.Contains('%', StringComparison.Ordinal) && !HoldsAny(path, secrets))
        {
            return path;
        }

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var decoded = Uri.UnescapeDataString(segments[i]);
            if (HasAt(decoded) && EmailPattern().IsMatch(decoded))
            {
                segments[i] = ScreenedEmail;
            }
            else if (HoldsAny(decoded, secrets) || HoldsAny(segments[i], secrets))
            {
                segments[i] = ScreenedToken;
            }
        }

        var screened = string.Join('/', segments);
        return HoldsAny(Uri.UnescapeDataString(screened), secrets) ? "/" + ScreenedToken : screened;
    }

    /// <summary>
    /// The address without its userinfo, query and fragment (D380): its scheme, host, port and
    /// path, each path segment that holds an address as <see cref="ScreenedEmail"/> and one that
    /// holds one of <paramref name="secrets"/> as <see cref="ScreenedToken"/> (<see cref="ScreenPath"/>, D382).
    /// </summary>
    public static Uri WithoutQuery(Uri url, params string[] secrets)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new Uri(url.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped) + ScreenPath(url.AbsolutePath, secrets));
    }

    /// <summary>
    /// <paramref name="id"/> when it has a request id's shape and holds none of
    /// <paramref name="secrets"/> (the request's token, which that shape admits), else null (D382):
    /// 1 to 128 of <c>A</c> to <c>Z</c>, <c>a</c> to <c>z</c>, <c>0</c> to <c>9</c>, <c>:</c>,
    /// <c>.</c>, <c>_</c> and <c>-</c> (GitHub's <c>C0DE:1234:…</c>, Azure DevOps' GUIDs, GitLab's
    /// ids). The id is the server's and goes into every warning and failure, so any other is kept nowhere.
    /// </summary>
    public static string? ShapedRequestId(string? id, params string[] secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        return id is not null && RequestIdPattern().IsMatch(id) && !HoldsAny(id, secrets) ? id : null;
    }

    /// <summary>
    /// What a log line says of <paramref name="exception"/> (D380): its type's name, with its
    /// <see cref="HttpRequestError"/> when it has one other than <see cref="HttpRequestError.Unknown"/>,
    /// and the first <see cref="SocketException"/> inside it with its <see cref="SocketError"/>, such as
    /// <c>HttpRequestException ConnectionError, SocketException ConnectionRefused</c>. Never its
    /// message, nor an inner one's: .NET's messages quote what a proxy or a server sent (a proxy's
    /// userinfo, a header line with a token echoed back, a cookie, an address, body bytes).
    /// </summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var described = Named(exception);
        if (exception is SocketException)
        {
            return described;
        }

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SocketException)
            {
                return described + ", " + Named(inner);
            }
        }

        return described;

        static string Named(Exception given)
        {
            var name = given.GetType().Name;
            var code = given switch
            {
                HttpRequestException { HttpRequestError: not HttpRequestError.Unknown } request => request.HttpRequestError.ToString(),
                HttpIOException { HttpRequestError: not HttpRequestError.Unknown } io => io.HttpRequestError.ToString(),
                SocketException socket => socket.SocketErrorCode.ToString(),
                _ => null,
            };
            return code is null ? name : name + " " + code;
        }
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
        var secrets = SecretsOf(token);
        for (var page = 1; ; page++)
        {
            var (reply, exchange) = await SendCoreAsync(HttpMethod.Get, token, url, null, type, ct).ConfigureAwait(false);
            if (!reply.Succeeded || reply.Value is null)
            {
                return reply;
            }

            pageHeaders?.Invoke(reply.Headers);
            items.AddRange(reply.Value);
            Uri? next;
            try
            {
                next = _profile.NextPage(exchange);
            }
            catch (Exception ex)
            {
                return reply with { Value = default, Failure = Unreadable(exchange, ex, secrets) };
            }

            if (next is null)
            {
                return reply with { Value = items };
            }

            var origin = _profile.TokenOrigin;
            if (!SameOrigin(next, origin) || !next.AbsolutePath.StartsWith(origin.AbsolutePath, StringComparison.Ordinal))
            {
                var failure = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, reply.RequestId, secrets);
                Warn(HttpMethod.Get.Method, exchange.Url, $"Link to {OriginOf(next)}", failure.RequestId, secrets);
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
        var secrets = SecretsOf(token);
        var (exchange, early) = await ExchangeAsync(method, token, Fingerprint(token), secrets, url, content, ct).ConfigureAwait(false);
        var requestId = RequestIdOf(exchange.Headers, secrets);
        HostingReply<T> Reply(T? value, HostingFailure? failure) => new(value, failure, exchange.Status, null, exchange.Headers, requestId);

        if (early is not null)
        {
            return (Reply(default, early), exchange);
        }

        try
        {
            var status = exchange.Status;
            var success = status is >= 200 and < 300 || status == 304;
            if (success && _profile.RefuseSuccess(exchange) is { } refused)
            {
                return (Reply(default, Refused(exchange, refused, requestId, secrets)), exchange);
            }

            if (_profile.SucceedsEmpty(status))
            {
                return (Reply(default, null), exchange);
            }

            if (!success)
            {
                return (Reply(default, Refused(exchange, _profile.Classify(exchange), requestId, secrets)), exchange);
            }

            if (Parse(exchange.Body, type) is { } value)
            {
                return (Reply(value, null), exchange);
            }
        }
        catch (Exception ex)
        {
            // A hook that throws on what the server sent (a header value out of range, say) does
            // not throw out of the core (D380): the answer could not be read.
            return (Reply(default, Unreadable(exchange, ex, secrets)), exchange);
        }

        var malformed = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, requestId, secrets);
        Warn(method.Method, exchange.Url, "Malformed", malformed.RequestId, secrets);
        return (Reply(default, malformed), exchange);
    }

    /// <summary>
    /// Sends the request, repeating a redirect on the token's origin up to three times, and reads
    /// the answer's body (at most 10 MB). A 304 to a kept answer gives the kept body. One at a
    /// time, and nothing sent for a request cancelled before its turn; the time limit starts once
    /// the request is sent. A failure here is finished and warned.
    /// </summary>
    private async Task<(HostingExchange Exchange, HostingFailure? Failure)> ExchangeAsync(HttpMethod method, string token, string fingerprint, string[] secrets, Uri url, byte[]? content, CancellationToken ct)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // SemaphoreSlim still gives its turn to a waiter cancelled just before the request ahead
            // frees it (it takes the waiter out of its queue later, off the cancelling thread).
            ct.ThrowIfCancellationRequested();
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
                        HostingFailure? refused;
                        try
                        {
                            refused = location is null ? null : _profile.RefuseRedirect(redirect, location);
                        }
                        catch (Exception ex)
                        {
                            return (redirect, Unreadable(redirect, ex, secrets));
                        }

                        if (refused is not null)
                        {
                            return (redirect, Refused(redirect, refused, RequestIdOf(headers, secrets), secrets));
                        }

                        var refusal = location is null ? $"{status} without Location"
                            : hop >= MaxRedirects ? $"{status}, a fourth redirect"
                            : !SameOrigin(location, _profile.TokenOrigin) ? $"redirected to {OriginOf(location)}"
                            : null;
                        if (refusal is not null)
                        {
                            return (redirect, Stopped(redirect, refusal, secrets));
                        }

                        current = location!;
                        continue;
                    }

                    byte[]? read;
                    try
                    {
                        read = await ReadBoundedAsync(response.Content, MaxBodyBytes, limit.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
                    {
                        // A body that its Content-Encoding (gzip, deflate, br) does not decode: .NET's
                        // decoders throw these (D380, measured), and they are no network failure.
                        var undecoded = Answer([]);
                        return (undecoded, Stopped(undecoded, $"a body that could not be decompressed ({Describe(ex)})", secrets));
                    }

                    if (read is null)
                    {
                        var oversized = Answer([]);
                        return (oversized, Stopped(oversized, "an answer over 10 MB", secrets));
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

                    // An ETag of anything but visible ASCII (RFC 9110's obs-text, a control character,
                    // a space) is not kept: .NET refuses to send it in If-None-Match, so every later
                    // GET of the URL would fail unsent (D380: nothing a server sends breaks the core).
                    if (method == HttpMethod.Get && status == 200 && headers.GetValueOrDefault("ETag") is { Length: > 0 } etag && IsVisibleAscii(etag))
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
                    Warn(method.Method, current, "time-out", null, secrets);
                    return Unanswered(method, current, HostingHttpTexts.Timeout(_profile.Provider), secrets);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
                {
                    // A cancellation that is neither the caller's nor the time limit (the handler's
                    // connect time-out, a client disposed mid-request) is no answer. The exception
                    // is named by its type and codes alone, and not passed to the log (D380).
                    Warn(method.Method, current, $"Network ({Describe(ex)})", null, secrets);
                    return Unanswered(method, current, HostingHttpTexts.Network(_profile.Provider), secrets);
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
    private HostingFailure Refused(HostingExchange exchange, HostingFailure failure, string? requestId, string[] secrets)
    {
        var finished = Finish(failure, exchange.Url, requestId, secrets);
        string warning;
        try
        {
            warning = _profile.WarningOf(exchange, finished);
        }
        catch (Exception ex)
        {
            warning = $"{finished.Kind} ({Describe(ex)})";
        }

        Warn(exchange.Method.Method, exchange.Url, warning, finished.RequestId, secrets);
        return finished;
    }

    /// <summary>
    /// The core's own refusal of an answer (a redirect it does not follow, a body over 10 MB or one
    /// that could not be decompressed): <see cref="HostingFailureKind.Malformed"/>, with its warning.
    /// </summary>
    private HostingFailure Stopped(HostingExchange exchange, string warning, string[] secrets)
    {
        var failure = Finish(new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(_profile.Provider)), exchange.Url, RequestIdOf(exchange.Headers, secrets), secrets);
        Warn(exchange.Method.Method, exchange.Url, warning, failure.RequestId, secrets);
        return failure;
    }

    /// <summary>
    /// An answer a profile's hook threw on (D380): <see cref="HostingFailureKind.Malformed"/>, with
    /// the warning <c>Malformed (&lt;the exception, described&gt;)</c>.
    /// </summary>
    private HostingFailure Unreadable(HostingExchange exchange, Exception exception, string[] secrets) => Stopped(exchange, $"Malformed ({Describe(exception)})", secrets);

    /// <summary>
    /// The profile's request id of an answer's headers when it has a request id's shape
    /// (<see cref="ShapedRequestId"/>, D382) and none of the request's <paramref name="secrets"/> in it;
    /// null when it has not, or when the hook throws on them (D380).
    /// </summary>
    private string? RequestIdOf(IReadOnlyDictionary<string, string> headers, string[] secrets)
    {
        try
        {
            return ShapedRequestId(_profile.RequestIdOf(headers), secrets);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private (HostingExchange Exchange, HostingFailure Failure) Unanswered(HttpMethod method, Uri url, string message, string[] secrets) =>
        (new HostingExchange(method, url, 0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
         Finish(new HostingFailure(HostingFailureKind.Network, message), url, null, secrets));

    /// <summary>
    /// What every failure gets (5.2): the profile's provider; the address, the failure's own or
    /// else the request's, without its userinfo and query (D380) and with each path segment that
    /// holds an address screened (D382); the request id where the failure left it null, kept only
    /// with a request id's shape (D382); e-mail addresses screened out of its message and detail (D368).
    /// </summary>
    private HostingFailure Finish(HostingFailure failure, Uri url, string? requestId, string[] secrets) => failure with
    {
        Provider = _profile.Provider,
        Url = WithoutQuery(failure.Url ?? url, secrets),
        RequestId = ShapedRequestId(failure.RequestId ?? requestId, secrets),
        Message = ScreenEmail(ScreenSecrets(failure.Message, secrets)),
        Detail = failure.Detail is null ? null : ScreenEmail(ScreenSecrets(failure.Detail, secrets)),
    };

    /// <summary>
    /// The one warning of a failure: <c>&lt;name&gt;: &lt;method&gt; &lt;path&gt; failed: &lt;status or kind&gt; (request &lt;id&gt;).</c>,
    /// the path without its query and with each segment that holds an address screened, the id only
    /// with a request id's shape (D382), and each of the request's <paramref name="secrets"/> in the
    /// line as <see cref="ScreenedToken"/>. No exception is passed to the log (D380): the log's own
    /// text of one (its <c>ToString()</c>) would quote what the server or the proxy sent.
    /// </summary>
    private void Warn(string method, Uri url, string statusOrKind, string? requestId, string[] secrets)
    {
        var request = ShapedRequestId(requestId, secrets) is { } id ? $" (request {id})" : string.Empty;
        _log.Warning(ScreenSecrets($"{Name}: {method} {ScreenPath(url.AbsolutePath, secrets)} failed: {statusOrKind}{request}.", secrets));
    }

    /// <summary>
    /// What a request's token is screened as (5.2: never a token in a log line or a failure): the
    /// token, and the credential <see cref="HostingHttpProfile.Authorize"/> sends in
    /// <c>Authorization</c> (what follows its scheme) when that does not hold the token as given,
    /// such as a <c>Basic</c> one in Base64; the longest first. A server can echo either into a
    /// request id, a <c>Location</c>, a message or a reason phrase.
    /// </summary>
    internal string[] SecretsOf(string token)
    {
        if (token.Length == 0)
        {
            return [];
        }

        var secrets = new List<string> { token };
        using var probe = new HttpRequestMessage(HttpMethod.Get, _profile.TokenOrigin);
        _profile.Authorize(probe, token);
        if (probe.Headers.NonValidated.TryGetValues("Authorization", out var values))
        {
            foreach (var value in values)
            {
                var space = value.IndexOf(' ', StringComparison.Ordinal);
                var credential = (space < 0 ? value : value[(space + 1)..]).Trim();
                if (credential.Length > 0 && !credential.Contains(token, StringComparison.Ordinal) && !secrets.Contains(credential, StringComparer.Ordinal))
                {
                    secrets.Add(credential);
                }
            }
        }

        return [.. secrets.OrderByDescending(secret => secret.Length)];
    }

    /// <summary>
    /// <paramref name="text"/> with each occurrence of each of <paramref name="secrets"/> as
    /// <see cref="ScreenedToken"/>, in any case: a host is read in lower case, so a token a server
    /// puts in a <c>Location</c>'s or a <c>Link</c>'s host reaches the warning so.
    /// </summary>
    private static string ScreenSecrets(string text, string[] secrets)
    {
        foreach (var secret in secrets)
        {
            text = text.Replace(secret, ScreenedToken, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private static bool HoldsAny(string text, string[] secrets) => secrets.Any(secret => text.Contains(secret, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether every character of <paramref name="text"/> is visible ASCII, <c>!</c> to <c>~</c>: an ETag the core can send back.</summary>
    private static bool IsVisibleAscii(string text) => text.All(c => c is >= '!' and <= '~');

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

    private static bool HasAt(string text) => text.Contains('@', StringComparison.Ordinal) || text.Contains(FullWidthAt, StringComparison.Ordinal);

    [GeneratedRegex(@"\S+[@\uFF20]\S+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    // \z, not $: $ matches before a last line end too.
    [GeneratedRegex(@"^[A-Za-z0-9:._-]{1,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RequestIdPattern();

    /// <summary>The last 200 answer of a URL for one token: its ETag, body and the profile's kept headers, in memory only.</summary>
    private sealed record KeptAnswer(string ETag, byte[] Body, IReadOnlyDictionary<string, string> Headers);
}
