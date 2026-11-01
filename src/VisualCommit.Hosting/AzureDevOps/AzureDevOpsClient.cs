using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.Http;

namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// One answer of <see cref="AzureDevOpsClient"/>: its value, or why it failed (docs/phase-7b-design.md 6.3).
/// </summary>
/// <param name="Value">The parsed body; null on a failure.</param>
/// <param name="Failure">Why the request failed; null when it did not.</param>
/// <param name="Status">The last answer's status code; 0 when none came.</param>
/// <param name="MoreExist">A walk of pages stopped at its cap with rows left (the <c>$top=1</c> probe found one).</param>
/// <param name="RequestId">The last answer's <c>ActivityId</c> (else <c>X-VSS-E2EID</c>), when it has a request id's shape (D382).</param>
internal sealed record AzureDevOpsReply<T>(T? Value, HostingFailure? Failure, int Status, bool MoreExist, string? RequestId)
{
    public bool Succeeded => Failure is null;
}

/// <summary>
/// Azure DevOps' REST API on the shared HTTP core (docs/phase-7b-design.md 6.3; D358, D360):
/// <see cref="HostingHttpClient"/> with Azure DevOps' profile, which puts the token as <c>Basic</c>
/// of <c>":" + token</c> on the web base's origin alone (web, git and the API on one origin, D285;
/// the API is there for <c>&lt;organization&gt;.visualstudio.com</c> remotes too, MEASURED r5 6.2),
/// asks for no sign-in redirect (<c>X-TFS-FedAuthRedirect: Suppress</c>), refuses the sign-in's
/// redirects, a 203 and an HTML success as refused credentials, refuses a redirect into another
/// organization, and classifies every other answer by its status and <c>typeKey</c>, never by its
/// words (ruling 20). Lists of <c>{"count", "value"}</c> are walked by <c>$top</c> and <c>$skip</c>
/// (<see cref="GetPagesAsync{T}"/>); nothing is kept between answers (no <c>ETag</c>, MEASURED r5 7).
/// </summary>
internal sealed class AzureDevOpsClient : IDisposable
{
    /// <summary>The Anonymous identity's id, a published constant (MEASURED r5 1.2).</summary>
    public const string AnonymousId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>How the Anonymous identity's descriptor starts (MEASURED r5 1.2), compared without case.</summary>
    public const string AnonymousDescriptor = "System:PublicAccess;";

    /// <summary>The warning's <c>&lt;status or kind&gt;</c> of a redirect into another organization on the same origin.</summary>
    public const string OutsideOrganization = "redirected outside the organization";

    /// <summary>The message of an answer that is the sign-in's instead of the API's: a redirect to it, a 203 or an HTML success.</summary>
    public const string SignInPageText = "Azure DevOps answered with its sign-in page.";

    /// <summary>The rows a page asks for unless the constructor says otherwise.</summary>
    public const int DefaultPageSize = 100;

    private const string ActivityIdHeader = "ActivityId";
    private const string EndToEndIdHeader = "X-VSS-E2EID";
    private const string ServiceErrorHeader = "X-TFS-ServiceError";

    private readonly HostingHttpClient _core;

    /// <summary>A client over one handler; <paramref name="pageSize"/> is the rows each page of a walk asks for (<c>$top</c>).</summary>
    public AzureDevOpsClient(AzureDevOpsEndpoints endpoints, Func<HttpMessageHandler> createHandler, IAppLog log, TimeProvider clock, TimeSpan? timeout = null, int pageSize = DefaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(createHandler);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        Endpoints = endpoints;
        PageSize = pageSize;
        _core = new HostingHttpClient(new AzureDevOpsProfile(endpoints, clock), createHandler, log, clock, timeout);
    }

    public AzureDevOpsEndpoints Endpoints { get; }

    /// <summary>The rows each page of a walk asks for.</summary>
    public int PageSize { get; }

    /// <summary>
    /// One request of <paramref name="method"/> to <paramref name="url"/> (which names its
    /// <c>api-version</c>), with <paramref name="body"/> as JSON when given (a type of
    /// <see cref="AzureDevOpsJsonContext"/>), its answer parsed as <paramref name="type"/>. Runs on the thread pool.
    /// </summary>
    public async Task<AzureDevOpsReply<T>> SendAsync<T>(HttpMethod method, string token, Uri url, object? body, JsonTypeInfo<T> type, CancellationToken ct)
    {
        var reply = await _core.SendAsync(method, token, url, body, type, ct).ConfigureAwait(false);
        return new AzureDevOpsReply<T>(reply.Value, reply.Failure, reply.Status, false, reply.RequestId);
    }

    /// <summary>
    /// The rows of a list from <paramref name="url"/> (6.3): pages of <c>$top=&lt;page size&gt;&amp;$skip=&lt;rows so far&gt;</c>
    /// until a short one, at most <paramref name="maxPages"/>; after that many full pages one
    /// <c>$top=1</c> request says whether more exist (<see cref="AzureDevOpsReply{T}.MoreExist"/>):
    /// any row it finds does, also one already shown. A page's first <see cref="PageSize"/> rows are taken, whatever it holds, so the walk keeps at
    /// most <paramref name="maxPages"/> × <see cref="PageSize"/> rows. A row is kept once by
    /// <paramref name="idOf"/>, since a new one between pages shifts the next page by one. A failing
    /// page fails the whole list.
    /// </summary>
    public async Task<AzureDevOpsReply<List<T>>> GetPagesAsync<T>(string token, Uri url, JsonTypeInfo<AzureDevOpsListJson<T>> type, Func<T, string> idOf, int maxPages, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(idOf);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        var rows = new List<T>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skip = 0;
        AzureDevOpsReply<List<T>> Failed(AzureDevOpsReply<AzureDevOpsListJson<T>> reply) => new(default, reply.Failure, reply.Status, false, reply.RequestId);
        for (var page = 0; page < maxPages; page++)
        {
            var pageUrl = WithQuery(url, Invariant($"$top={PageSize}&$skip={skip}"));
            var reply = await SendAsync(HttpMethod.Get, token, pageUrl, null, type, ct).ConfigureAwait(false);
            if (reply.Failure is not null || reply.Value is null)
            {
                return Failed(reply);
            }

            if (HasNullRow(reply.Value))
            {
                return Malformed(pageUrl, reply);
            }

            // At most a page's worth of rows is taken from each page, and the next page starts after
            // them: a server that ignores $top (a page may hold up to 10 MB of rows) cannot carry
            // the walk past maxPages × PageSize rows. A page of at least PageSize rows is full.
            var taken = Math.Min(reply.Value.Value.Count, PageSize);
            foreach (var row in reply.Value.Value.Take(taken))
            {
                if (seen.Add(idOf(row)))
                {
                    rows.Add(row);
                }
            }

            skip += taken;
            if (taken < PageSize)
            {
                return new AzureDevOpsReply<List<T>>(rows, null, reply.Status, false, reply.RequestId);
            }
        }

        var probeUrl = WithQuery(url, Invariant($"$top=1&$skip={skip}"));
        var probe = await SendAsync(HttpMethod.Get, token, probeUrl, null, type, ct).ConfigureAwait(false);
        if (probe.Failure is not null || probe.Value is null)
        {
            return Failed(probe);
        }

        if (HasNullRow(probe.Value))
        {
            return Malformed(probeUrl, probe);
        }

        // Any row past the rows walked means the list holds more than were shown, also one already
        // shown: a request created during the walk (newest first) shifts the list, so the probe finds
        // the last row shown while the new one never was; a server that ignores $skip answers its
        // first rows again while the rest were never shown.
        return new AzureDevOpsReply<List<T>>(rows, null, probe.Status, probe.Value.Value.Count > 0, probe.RequestId);

        // A null row parses (System.Text.Json does not hold a list's elements to their annotations):
        // the page is malformed, as one whose row lacks a member it must have, and nothing throws (D380).
        static bool HasNullRow(AzureDevOpsListJson<T> list) => list.Value.Exists(row => row is null);

        AzureDevOpsReply<List<T>> Malformed(Uri pageUrl, AzureDevOpsReply<AzureDevOpsListJson<T>> reply) =>
            new(default, MalformedAnswer(HttpMethod.Get, pageUrl, reply.RequestId), reply.Status, false, reply.RequestId);
    }

    /// <summary>An answer that succeeded but lacks what the app needs: the core's <see cref="HostingHttpClient.MalformedAnswer"/>, with its warning.</summary>
    public HostingFailure MalformedAnswer(HttpMethod method, Uri url, string? requestId) => _core.MalformedAnswer(method, url, requestId);

    /// <summary>A failure the provider builds from an answer that succeeded, finished as the core's (<see cref="HostingHttpClient.Failed"/>), with its warning.</summary>
    public HostingFailure Failed(HttpMethod method, Uri url, HostingFailure failure, string? requestId) => _core.Failed(method, url, failure, requestId);

    /// <summary>Forgets the core's kept answers (a sign-out); Azure DevOps sends no <c>ETag</c>, so there are none in practice.</summary>
    public void ForgetKept() => _core.ForgetKept();

    public void Dispose() => _core.Dispose();

    /// <summary><paramref name="url"/> with <paramref name="parameters"/> added to its query.</summary>
    public static Uri WithQuery(Uri url, string parameters)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new Uri(url.AbsoluteUri + (url.Query.Length > 0 ? "&" : "?") + parameters);
    }

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    /// <summary>An error body, or null when it is none (an empty, HTML or text body).</summary>
    private static AzureDevOpsErrorJson? ErrorOf(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, AzureDevOpsJsonContext.Default.AzureDevOpsErrorJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The first segment of an address's path, as sent: the organization of every request the client makes.</summary>
    private static string FirstSegment(Uri url)
    {
        var path = url.AbsolutePath.TrimStart('/');
        var slash = path.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? path : path[..slash];
    }

    private static bool IsWeb(Uri url) => url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// Azure DevOps' profile (6.3): the web base as the token's origin, <c>Basic</c> of
    /// <c>":" + token</c> (r5 0.6, READ), <c>X-TFS-FedAuthRedirect: Suppress</c> (the sign-in's 302
    /// becomes a 401, MEASURED r5 1.3) and <c>Accept: application/json</c>, <c>ActivityId</c> (else
    /// <c>X-VSS-E2EID</c>) as the request id, the sign-in's answers refused as credentials, a
    /// redirect into another organization refused, and 6.3's table.
    /// </summary>
    private sealed class AzureDevOpsProfile(AzureDevOpsEndpoints endpoints, TimeProvider clock) : HostingHttpProfile
    {
        private static readonly TimeSpan DefaultRetry = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LongestRetry = TimeSpan.FromDays(1);

        /// <summary>The last second a <see cref="DateTimeOffset"/> holds, as Unix seconds.</summary>
        private static readonly long MaxResetSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

        /// <summary>The <c>typeKey</c>s of a refused <c>api-version</c> (MEASURED r5 1.4).</summary>
        private static readonly string[] VersionKeys = ["VssInvalidPreviewVersionException", "VssVersionOutOfRangeException", "VssInvalidApiResourceVersionException"];

        public override HostingProvider Provider => HostingProvider.AzureDevOps;

        public override Uri TokenOrigin => endpoints.WebBase;

        public override JsonSerializerContext Json => AzureDevOpsJsonContext.Default;

        public override void Authorize(HttpRequestMessage request, string token) =>
            request.Headers.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + token)));

        public override void AddHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("X-TFS-FedAuthRedirect", "Suppress");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
        }

        public override string? RequestIdOf(IReadOnlyDictionary<string, string> headers) =>
            headers.GetValueOrDefault(ActivityIdHeader) is { Length: > 0 } activity ? activity : headers.GetValueOrDefault(EndToEndIdHeader);

        /// <summary>
        /// On A1's <c>connectionData</c> every redirect is <see cref="HostingFailureKind.BadCredentials"/>
        /// (D360). Elsewhere a redirect to the sign-in (a path <c>/_signin</c>, or a host under
        /// <c>.vssps.visualstudio.com</c>) is too, and one on the same origin into another organization
        /// (its first segment another, without case) is <see cref="HostingFailureKind.Malformed"/>.
        /// None of them is followed; any other is left to the core's rule.
        /// </summary>
        public override HostingFailure? RefuseRedirect(HostingExchange exchange, Uri location)
        {
            if (exchange.Url.AbsolutePath.EndsWith("/_apis/connectionData", StringComparison.OrdinalIgnoreCase))
            {
                return new HostingFailure(HostingFailureKind.BadCredentials, SignInPageText);
            }

            if (!IsWeb(location))
            {
                return null;
            }

            if (string.Equals(location.AbsolutePath, "/_signin", StringComparison.OrdinalIgnoreCase)
                || HostingHttpClient.IdnHostOf(location)?.EndsWith(".vssps.visualstudio.com", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HostingFailure(HostingFailureKind.BadCredentials, SignInPageText);
            }

            // On the token's origin the path as .NET will send it must stay in g3 G3.4's form
            // (InPlainForm): .NET keeps an encoded slash or backslash (Location
            // /<org>/%2E%2E%2F<other>/… is sent as /<org>/..%2F<other>/…, its first segment still
            // <org>), and a server that decodes it before resolving dot segments would serve another
            // organization with this one's token.
            return HostingHttpClient.SameOrigin(location, TokenOrigin)
                && (!string.Equals(FirstSegment(location), FirstSegment(exchange.Url), StringComparison.OrdinalIgnoreCase)
                    || !CredentialBinding.InPlainForm(location.GetLeftPart(UriPartial.Path)))
                ? new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(Provider))
                : null;
        }

        /// <summary>A 203, or a success whose <c>Content-Type</c> is HTML or whose body starts with <c>&lt;</c>: the sign-in's page, not the API's (g1 G0.1).</summary>
        public override HostingFailure? RefuseSuccess(HostingExchange exchange)
        {
            var html = exchange.Headers.GetValueOrDefault("Content-Type")?.TrimStart().StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true
                || exchange.Body.SkipWhile(b => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n').FirstOrDefault() == (byte)'<';
            return exchange.Status == 203 || html ? new HostingFailure(HostingFailureKind.BadCredentials, SignInPageText) : null;
        }

        public override string WarningOf(HostingExchange exchange, HostingFailure failure) =>
            HostingHttpClient.IsRedirect(exchange.Status) && failure.Kind == HostingFailureKind.Malformed
                ? OutsideOrganization
                : base.WarningOf(exchange, failure);

        /// <summary>6.3's table, by status and <c>typeKey</c> (ruling 20; r5 1.4, g1 G2).</summary>
        public override HostingFailure Classify(HostingExchange exchange)
        {
            var status = exchange.Status;
            var error = ErrorOf(exchange.Body);
            var typeKey = error?.TypeKey;
            var message = MessageOf(exchange, error);
            HostingFailure Fail(HostingFailureKind kind, PullRequestRefusal refusal = PullRequestRefusal.None) => new(kind, message) { Refusal = refusal };
            return status switch
            {
                401 => Fail(HostingFailureKind.BadCredentials),
                400 when typeKey is not null && VersionKeys.Contains(typeKey, StringComparer.Ordinal) => Fail(HostingFailureKind.ApiVersion),
                403 => Fail(HostingFailureKind.Forbidden),
                404 => Fail(HostingFailureKind.NotFound),
                409 when typeKey == "GitPullRequestExistsException" => Fail(HostingFailureKind.Validation, PullRequestRefusal.AlreadyExists),
                400 when typeKey == "GitPullRequestCannotBeActivated" => Fail(HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing),
                400 or 409 or 422 => Fail(HostingFailureKind.Validation),
                429 => new HostingFailure(HostingFailureKind.RateLimited, message, RetryAt: RetryAt(exchange.Headers)),
                >= 500 and < 600 => Fail(HostingFailureKind.ServerError),
                _ => new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(Provider)),
            };
        }

        /// <summary>
        /// The body's <c>message</c>, else <c>value.Message</c> (TF400677's shape), else
        /// <c>X-TFS-ServiceError</c> URL-decoded, else the reason phrase, else <c>HTTP &lt;status&gt;</c>
        /// (a 5xx's: <see cref="HostingHttpTexts.ServerError"/>).
        /// </summary>
        private string MessageOf(HostingExchange exchange, AzureDevOpsErrorJson? error)
        {
            if (error?.Message is { } message && !string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            if (error?.Value?.Message is { } valueMessage && !string.IsNullOrWhiteSpace(valueMessage))
            {
                return valueMessage;
            }

            if (exchange.Headers.GetValueOrDefault(ServiceErrorHeader) is { } serviceError && Uri.UnescapeDataString(serviceError) is { } decoded && !string.IsNullOrWhiteSpace(decoded))
            {
                return decoded;
            }

            if (exchange.Reason is { } reason && !string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            return exchange.Status is >= 500 and < 600
                ? HostingHttpTexts.ServerError(Provider, exchange.Status)
                : string.Create(CultureInfo.InvariantCulture, $"HTTP {exchange.Status}");
        }

        /// <summary>When a rate limit ends: now plus <c>Retry-After</c> (seconds or a date), else <c>X-RateLimit-Reset</c> (Unix seconds), else 60 s; not before now and a day ahead at most.</summary>
        private DateTimeOffset RetryAt(IReadOnlyDictionary<string, string> headers)
        {
            var now = clock.GetUtcNow();
            DateTimeOffset? at = null;
            if (headers.GetValueOrDefault("Retry-After")?.Trim() is { Length: > 0 } retryAfter)
            {
                if (long.TryParse(retryAfter, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                {
                    at = now + TimeSpan.FromSeconds(Math.Min(seconds, (long)LongestRetry.TotalSeconds));
                }
                else if (DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                {
                    at = date;
                }
            }

            if (at is null
                && headers.GetValueOrDefault("X-RateLimit-Reset")?.Trim() is { Length: > 0 } reset
                && long.TryParse(reset, NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
                && unix <= MaxResetSeconds)
            {
                at = DateTimeOffset.FromUnixTimeSeconds(unix);
            }

            // Between now and a day ahead: a date or a reset in the past (a server's clock behind,
            // or a stale header) means "now", never a time the app would show as already gone.
            var retry = at ?? now + DefaultRetry;
            return retry < now ? now : retry > now + LongestRetry ? now + LongestRetry : retry;
        }
    }
}
