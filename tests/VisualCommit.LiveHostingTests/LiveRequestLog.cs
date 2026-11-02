using System.Globalization;
using System.Security.Cryptography;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// One request the app sent, as <see cref="LiveRequestLog"/> keeps it (docs/phase-7a-design.md
/// 14.5): when, the method, the scheme, the host (with its port when not the scheme's) and the path
/// without the query (a query may hold an author's e-mail); the status, or the exception's type when
/// no answer came; whether <c>Authorization</c> was sent, never its value; and of the answer only
/// <c>X-GitHub-Request-Id</c>, <c>X-GitHub-Api-Version-Selected</c>, whether <c>X-OAuth-Scopes</c> was
/// present and <c>GitHub-Authentication-Token-Expiration</c>'s date.
/// </summary>
internal sealed record LiveRequest(
    DateTimeOffset Time,
    string Method,
    string Scheme,
    string Host,
    string Path,
    int? Status,
    bool AuthorizationSent,
    string? RequestId,
    string? ApiVersionSelected,
    bool ScopesHeaderPresent,
    string? TokenExpiration,
    string? FailedWith)
{
    /// <summary>The provider whose reach the request was judged by (docs/phase-7b-design.md 15.4); GitHub for 7a's log.</summary>
    public HostingProvider Provider { get; init; } = HostingProvider.GitHub;

    /// <summary>The credential's form, never its value (15.4).</summary>
    public LiveCredentialForm Form { get; init; }

    /// <summary>The answer's rate-limit, expiry and <c>api-version</c> headers by name (15.4's <c>Headers</c>; named so beside 7a's <see cref="Headers()"/>).</summary>
    public IReadOnlyDictionary<string, string> KeptHeaders { get; init; } = new Dictionary<string, string>();

    /// <summary>One line: <c>GET https://api.github.com/user 200 with Authorization (request …)</c>.</summary>
    public string Describe() =>
        $"{Method} {Scheme}://{Host}{Path} {(Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : "failed: " + FailedWith)} "
        + $"{(AuthorizationSent ? "with" : "without")} Authorization (request {RequestId ?? "none"})";

    /// <summary>
    /// The three headers of the answer that run.txt shows (14.5): <c>API version selected 2026-03-10,
    /// X-OAuth-Scopes absent, the token expires 2026-12-31</c>, each "not sent" when it was not.
    /// </summary>
    public string Headers() =>
        $"API version selected {ApiVersionSelected ?? "not sent"}, X-OAuth-Scopes {(ScopesHeaderPresent ? "present" : "absent")}, "
        + $"the token expires {TokenExpiration ?? "not sent"}";
}

/// <summary>
/// The app's requests in the live command (docs/phase-7a-design.md 14.5): a handler between the
/// session's clients and GitHub that records each request as a <see cref="LiveRequest"/> and passes
/// the request and the answer on unchanged. It reads no body, and of the headers only the four
/// <see cref="LiveRequest"/> names, and of <c>Authorization</c> only whether it was sent. One
/// <see cref="Store"/> serves every handler the session creates (its client's and the avatar
/// cache's). A request that would break D327's reach (<see cref="RefusalOf"/>) is recorded and
/// refused before it leaves, so that a regression in the app never sends the owner's token
/// elsewhere; <see cref="ReachProblems"/> then names it among the run's problems.
/// </summary>
internal sealed class LiveRequestLog : DelegatingHandler
{
    /// <summary>The only host that may see the token (D327).</summary>
    public const string ApiHost = "api.github.com";

    private readonly Store store;
    private readonly LiveReach? reach;

    /// <summary>7a's log for GitHub (D327's reach).</summary>
    public LiveRequestLog(HttpMessageHandler inner, Store store)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>A new provider's log (15.4): every request passes <paramref name="reach"/>'s guard (D352, D371, D374) before it leaves, and a refusal names the origin, never the path.</summary>
    public LiveRequestLog(HttpMessageHandler inner, Store store, LiveReach reach)
        : this(inner, store)
    {
        ArgumentNullException.ThrowIfNull(reach);
        this.reach = reach;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (reach is not null)
        {
            return await SendWithinAsync(reach, request, cancellationToken).ConfigureAwait(false);
        }

        var time = DateTimeOffset.UtcNow;
        var uri = request.RequestUri ?? throw new InvalidOperationException("A request without a URL.");
        var authorization = request.Headers.Contains("Authorization");
        if (RefusalOf(uri, authorization) is { } refusal)
        {
            // An HttpRequestException, as for a request that did not reach its host: the app's client
            // and its avatar cache take it as a network failure, so the test goes on to its clean-up
            // and its record, where ReachProblems names the request. Its message has no query.
            store.Add(new LiveRequest(time, request.Method.Method, uri.Scheme, uri.Authority, uri.AbsolutePath, null, authorization, null, null, false, null, "refused: " + refusal));
            throw new HttpRequestException($"The live command refused {request.Method.Method} {uri.Scheme}://{uri.Authority}{uri.AbsolutePath}: {refusal} (D327).");
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            store.Add(new LiveRequest(time, request.Method.Method, uri.Scheme, uri.Authority, uri.AbsolutePath, null, authorization, null, null, false, null, ex.GetType().Name));
            throw;
        }

        store.Add(new LiveRequest(
            time,
            request.Method.Method,
            uri.Scheme,
            uri.Authority,
            uri.AbsolutePath,
            (int)response.StatusCode,
            authorization,
            First(response, "X-GitHub-Request-Id"),
            First(response, "X-GitHub-Api-Version-Selected"),
            response.Headers.Contains("X-OAuth-Scopes"),
            ExpirationDate(First(response, "GitHub-Authentication-Token-Expiration")),
            null));
        return response;
    }

    /// <summary>A new provider's request: the guard first, then the record with its form, request id and kept headers; a redirect is the answer, never followed (the inner handler follows none).</summary>
    private async Task<HttpResponseMessage> SendWithinAsync(LiveReach within, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var time = DateTimeOffset.UtcNow;
        var uri = request.RequestUri ?? throw new InvalidOperationException("A request without a URL.");
        var form = LiveReach.FormOf(request);
        LiveRequest Record(int? status, string? failedWith, HttpResponseMessage? response) =>
            new(time, request.Method.Method, uri.Scheme, uri.Authority, uri.AbsolutePath, status, form != LiveCredentialForm.None,
                response is null ? null : First(response, "ActivityId") ?? First(response, "x-request-id"), null, false, null, failedWith)
            {
                Provider = within.Provider,
                Form = form,
                KeptHeaders = response is null ? new Dictionary<string, string>() : KeptOf(response),
            };

        if ((LiveReach.HostRefusalOf(request, within) ?? within.RefusalOf(uri, form)) is { } refusal)
        {
            store.Add(Record(null, "refused: " + refusal, null));
            throw new HttpRequestException($"The live command refused {request.Method.Method} {uri.Scheme}://{uri.Authority}: {refusal} (D352, D371).");
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            store.Add(Record(null, ex.GetType().Name, null));
            throw;
        }

        store.Add(Record((int)response.StatusCode, null, response));
        within.Answered(uri, form, (int)response.StatusCode);
        return response;
    }

    /// <summary>
    /// The rate-limit, expiry and API version headers of an answer (15.4), by lower-case name, values
    /// joined; and as <c>api-version</c> the version Azure DevOps served, which it names as a parameter
    /// of <c>Content-Type</c>, not as a header (r5 1.2, design 3.4).
    /// </summary>
    internal static Dictionary<string, string> KeptOf(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            var lower = name.ToLowerInvariant();
            if (lower.StartsWith("x-ratelimit", StringComparison.Ordinal) || lower.StartsWith("ratelimit", StringComparison.Ordinal)
                || lower is "retry-after" or "api-supported-versions" or "x-vss-tokenexpiration" || lower.Contains("expir", StringComparison.Ordinal))
            {
                kept[lower] = string.Join(", ", values);
            }
            else if (lower == "content-type"
                && values.SelectMany(value => value.Split(';')).Select(part => part.Trim()).FirstOrDefault(part => part.StartsWith("api-version=", StringComparison.OrdinalIgnoreCase)) is { } version)
            {
                kept["api-version"] = version["api-version=".Length..];
            }
        }

        return kept;
    }

    /// <summary>
    /// A new provider's reach over <paramref name="requests"/> (15.4): every one <c>https</c>, none the
    /// guard refused, none with a credential to another origin than <paramref name="within"/>'s. One
    /// line per request that breaks a rule.
    /// </summary>
    internal static IReadOnlyList<string> ReachProblems(IEnumerable<LiveRequest> requests, LiveReach within)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(within);
        var problems = new List<string>();
        foreach (var request in requests)
        {
            if (!string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                problems.Add($"A request was not https: {request.Describe()}.");
            }

            if (request.FailedWith?.StartsWith("refused", StringComparison.Ordinal) == true)
            {
                problems.Add($"The guard refused a request (D352, D371): {request.Method} {request.Scheme}://{request.Host}: {request.FailedWith}.");
            }

            if (request.Form != LiveCredentialForm.None && !string.Equals(request.Host, within.Host, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"A credential went to another host than {within.Host}: {request.Describe()}.");
            }
        }

        return problems;
    }

    /// <summary>A new provider's requests per host (15.4): counts, the forms, the statuses and the kept headers' names, each with its count.</summary>
    internal static IReadOnlyList<string> PerHostOf(IEnumerable<LiveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        return
        [
            .. requests.GroupBy(request => request.Host, StringComparer.OrdinalIgnoreCase).Select(host =>
                $"The requests to {host.Key}: {host.Count().ToString(CultureInfo.InvariantCulture)}; credentials {Counted(host, request => request.Form.ToString())}; "
                + $"answered {Counted(host, request => request.Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : "failed: " + request.FailedWith)}; "
                + $"headers kept {(host.Any(request => request.KeptHeaders.Count > 0) ? string.Join(", ", host.SelectMany(request => request.KeptHeaders.Keys).GroupBy(name => name).Select(group => $"{group.Key} ×{group.Count().ToString(CultureInfo.InvariantCulture)}")) : "none")}."),
        ];
    }

    /// <summary>
    /// Why a request to <paramref name="uri"/> must not leave (D327), or null: it is not <c>https</c>,
    /// or it carries <c>Authorization</c> to another host than <see cref="ApiHost"/> on port 443.
    /// </summary>
    internal static string? RefusalOf(Uri uri, bool authorization)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return "not https";
        }

        return authorization && !(string.Equals(uri.IdnHost, ApiHost, StringComparison.OrdinalIgnoreCase) && uri.Port == 443)
            ? $"Authorization for another host than {ApiHost}"
            : null;
    }

    /// <summary>
    /// D327's reach, against <paramref name="requests"/>: every one <c>https</c>, and every one that
    /// sent <c>Authorization</c> to <see cref="ApiHost"/>. One line per request that breaks a rule,
    /// also for one that <see cref="RefusalOf"/> kept from leaving.
    /// </summary>
    internal static IReadOnlyList<string> ReachProblems(IEnumerable<LiveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var problems = new List<string>();
        foreach (var request in requests)
        {
            if (!string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                problems.Add($"A request was not https: {request.Describe()}.");
            }

            if (request.AuthorizationSent && !string.Equals(request.Host, ApiHost, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"Authorization went to another host than {ApiHost}: {request.Describe()}.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Per host, in the order first seen: the requests with and without <c>Authorization</c>, the
    /// statuses, and of the answers the API version GitHub selected, whether <c>X-OAuth-Scopes</c> was
    /// present and the token's expiry, each value with its count (run.txt, 14.5).
    /// </summary>
    internal static IReadOnlyList<string> PerHost(IEnumerable<LiveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        return
        [
            .. requests.GroupBy(request => request.Host, StringComparer.OrdinalIgnoreCase).Select(host =>
            {
                var with = host.Count(request => request.AuthorizationSent);
                var statuses = Counted(host, request => request.Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : "failed: " + request.FailedWith);
                var answered = host.Where(request => request.Status is not null).ToList();
                var headers = answered.Count == 0
                    ? string.Empty
                    : $"; API version selected {Counted(answered, request => request.ApiVersionSelected ?? "not sent")}"
                        + $"; X-OAuth-Scopes {Counted(answered, request => request.ScopesHeaderPresent ? "present" : "absent")}"
                        + $"; the token expires {Counted(answered, request => request.TokenExpiration ?? "not sent")}";
                return $"The app's requests to {host.Key}: {host.Count().ToString(CultureInfo.InvariantCulture)}, "
                    + $"{with.ToString(CultureInfo.InvariantCulture)} with Authorization and {(host.Count() - with).ToString(CultureInfo.InvariantCulture)} without; answered {statuses}{headers}.";
            }),
        ];
    }

    /// <summary>Each value of <paramref name="value"/> over <paramref name="requests"/> with its count, in the order first seen: <c>200 ×3, 404 ×1</c>.</summary>
    private static string Counted(IEnumerable<LiveRequest> requests, Func<LiveRequest, string> value) =>
        string.Join(", ", requests.GroupBy(value).Select(group => $"{group.Key} ×{group.Count().ToString(CultureInfo.InvariantCulture)}"));

    private static string? First(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) ? values.FirstOrDefault() : null;

    /// <summary>The date of <c>GitHub-Authentication-Token-Expiration</c> (<c>yyyy-MM-dd HH:mm:ss UTC</c>): its first ten characters when they are one, else "present, not a date".</summary>
    internal static string? ExpirationDate(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "present, not a date";
    }

    /// <summary>The records, safe from any thread: the session's client and its avatar cache add to one store.</summary>
    internal sealed class Store
    {
        private readonly Lock _gate = new();
        private readonly List<LiveRequest> _requests = [];

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _requests.Count;
                }
            }
        }

        public void Add(LiveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (_gate)
            {
                _requests.Add(request);
            }
        }

        /// <summary>The records so far, in the order their answers (or failures) came.</summary>
        public IReadOnlyList<LiveRequest> Snapshot()
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }
}

/// <summary>A request's credential, by its form alone (docs/phase-7b-design.md 15.4).</summary>
public enum LiveCredentialForm
{
    None,
    Basic,
    Bearer,
    OtherScheme,
    PrivateToken,
    JobToken,
    QueryToken,
}

/// <summary>
/// D352's guard for a new provider (15.4; D371, D374): what the live command's requests may reach
/// with which credential. <see cref="RefusalOf"/> judges a request before it leaves, naming the
/// origin and never the path; <see cref="Answered"/> sees each answer (D374's 404). Safe from any thread.
/// </summary>
internal abstract class LiveReach
{
    private static readonly string[] QueryTokens = ["private_token", "access_token", "job_token"];

    public abstract HostingProvider Provider { get; }

    /// <summary>The one host a credential may go to: <c>dev.azure.com</c>, <c>gitlab.com</c>.</summary>
    public abstract string Host { get; }

    /// <summary><c>https://&lt;host&gt;:443</c>, what a refusal names.</summary>
    public string Origin => $"https://{Host}:443";

    /// <summary>Azure DevOps' guard over the sandbox <paramref name="organization"/>/<paramref name="project"/>/<paramref name="repository"/>, with D374's made-up organization drawn for this run unless given.</summary>
    public static AzureDevOpsReach AzureDevOps(string organization, string project, string repository, string? madeUpOrganization = null) =>
        new(organization, project, repository, madeUpOrganization ?? AzureDevOpsReach.DrawMadeUpOrganization());

    /// <summary>GitLab's guard: <c>https://gitlab.com:443/api/v4/</c> alone, Bearer or no credential (D349).</summary>
    public static GitLabReach GitLab() => new();

    /// <summary>The credential's form: a query token, <c>PRIVATE-TOKEN</c>, <c>JOB-TOKEN</c>, else <c>Authorization</c>'s scheme (two values: another scheme).</summary>
    public static LiveCredentialForm FormOf(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is { } uri && HasQueryToken(uri))
        {
            return LiveCredentialForm.QueryToken;
        }

        if (request.Headers.Contains("PRIVATE-TOKEN"))
        {
            return LiveCredentialForm.PrivateToken;
        }

        if (request.Headers.Contains("JOB-TOKEN"))
        {
            return LiveCredentialForm.JobToken;
        }

        if (!request.Headers.TryGetValues("Authorization", out var values))
        {
            return LiveCredentialForm.None;
        }

        var list = values.ToList();
        var scheme = list.Count == 1 ? list[0].Split(' ', 2)[0] : string.Empty;
        return scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) ? LiveCredentialForm.Basic
            : scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ? LiveCredentialForm.Bearer
            : LiveCredentialForm.OtherScheme;
    }

    /// <summary>Why a request to <paramref name="uri"/> with <paramref name="form"/> must not leave, or null; never names the path.</summary>
    public abstract string? RefusalOf(Uri uri, LiveCredentialForm form);

    /// <summary>
    /// Why <paramref name="request"/>'s <c>Host</c> header keeps it from leaving, or null: one set to
    /// another authority than its URL's, which <c>SocketsHttpHandler</c> would send as the TLS server
    /// name and the <c>Host</c>, with the credential, while the URL the guard judges and the store
    /// records names the origin. Names the origin, never the header's value. No request of the app's
    /// or of the command's sets it.
    /// </summary>
    public static string? HostRefusalOf(HttpRequestMessage request, LiveReach within)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(within);
        return request.Headers.Host is { } host && request.RequestUri is { } uri && !string.Equals(host, uri.Authority, StringComparison.OrdinalIgnoreCase)
            ? $"its Host header names another authority than {within.Origin}"
            : null;
    }

    /// <summary>Sees the answer of a request the guard let through.</summary>
    public virtual void Answered(Uri uri, LiveCredentialForm form, int status)
    {
    }

    /// <summary>Whether <paramref name="uri"/> is <c>https</c> on <see cref="Host"/> at port 443.</summary>
    protected bool OnOrigin(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443
        && string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>The path as written (the original text's, before .NET removes a dot segment) and as sent, each split into its raw segments.</summary>
    protected static IEnumerable<string[]> RawSegments(Uri uri)
    {
        yield return uri.AbsolutePath.Split('/')[1..];
        var text = uri.OriginalString;
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0 && text.IndexOf('/', scheme + 3) is var start and >= 0)
        {
            var end = text.IndexOfAny(['?', '#'], start);
            yield return (end < 0 ? text[start..] : text[start..end]).Split('/')[1..];
        }
    }

    /// <summary>A decoded segment that is <c>.</c> or <c>..</c>.</summary>
    protected static bool IsDotSegment(string raw) => Uri.UnescapeDataString(raw) is "." or "..";

    /// <summary>The names of the query's parameters, decoded, in order (a repeated one as often as it shows).</summary>
    protected static IReadOnlyList<string> QueryNames(Uri uri) =>
        [.. uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(pair => Uri.UnescapeDataString(pair.Split('=', 2)[0]))];

    /// <summary>The value of the query's <paramref name="name"/> (decoded, case ignored), or null.</summary>
    protected static string? QueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
            }
        }

        return null;
    }

    private static bool HasQueryToken(Uri uri) => QueryTokens.Any(name => QueryValue(uri, name) is not null);
}

/// <summary>
/// Azure DevOps' guard (15.4): <c>https://dev.azure.com:443</c> alone, Basic or no credential, no
/// segment empty, <c>.</c>, <c>..</c> or holding <c>/</c>, <c>\</c> or <c>%</c> once decoded, and only
/// the sandbox's paths and D374's made-up organization's connectionData, compared without case, and
/// only the query parameters the app's client and the command send (<see cref="SentQueryNames"/>),
/// none repeated; <c>organizationName</c> only on the root form of the core area, naming the sandbox's
/// organization. D374's read with the token is admitted once per guard (per run), after its 404.
/// </summary>
internal sealed class AzureDevOpsReach : LiveReach
{
    /// <summary>
    /// The query parameters the app's client (6.5: <c>api-version</c>, <c>$top</c>, <c>$skip</c>,
    /// <c>searchCriteria.status</c>) and the command (<c>filter</c> of the refs read, <c>service</c> of
    /// info/refs) send; none names another repository, collection or organization, as Merge Bases'
    /// <c>otherRepositoryId</c> and <c>otherCollectionId</c>, <c>hostId</c> or <c>accountName</c> would.
    /// </summary>
    internal static readonly IReadOnlySet<string> SentQueryNames =
        new HashSet<string>(["api-version", "$top", "$skip", "searchCriteria.status", "filter", "service"], StringComparer.OrdinalIgnoreCase);

    private readonly string _organization;
    private readonly string _project;
    private readonly string _repository;
    private readonly Lock _gate = new();
    private int? _madeUpAnswer;
    private bool _madeUpTokenAdmitted;

    internal AzureDevOpsReach(string organization, string project, string repository, string madeUpOrganization)
    {
        _organization = organization;
        _project = project;
        _repository = repository;
        MadeUpOrganization = madeUpOrganization;
    }

    public override HostingProvider Provider => HostingProvider.AzureDevOps;

    public override string Host => "dev.azure.com";

    /// <summary>D374's organization for this run: <c>vc-none-</c> and 12 random lower-case hex digits. It is no secret: run.txt names it.</summary>
    public string MadeUpOrganization { get; }

    /// <summary>A new made-up organization's name (D374).</summary>
    public static string DrawMadeUpOrganization() => "vc-none-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    public override string? RefusalOf(Uri uri, LiveCredentialForm form)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!OnOrigin(uri))
        {
            return $"it is not on {Origin}";
        }

        if (form is not (LiveCredentialForm.None or LiveCredentialForm.Basic))
        {
            return $"{Origin} takes Basic or no credential, not {form}";
        }

        if (RawSegments(uri).Any(segments => segments.Any(raw => raw.Length == 0 || IsDotSegment(raw) || Uri.UnescapeDataString(raw).IndexOfAny(['/', '\\', '%']) >= 0)))
        {
            return $"a path segment on {Origin} is empty, a dot segment, or holds a slash, a backslash or a percent sign once decoded";
        }

        var s = uri.AbsolutePath.Split('/')[1..].Select(Uri.UnescapeDataString).ToArray();
        if (QueryRefusalOf(s, uri) is { } query)
        {
            return query;
        }

        return Admits(s, uri, form) ? null : $"the path is none of the sandbox's on {Origin} (D352, D371, D374)";
    }

    /// <summary>Why the query keeps the request from leaving, or null: a repeated parameter, or one that neither the app's client nor the command sends; <c>organizationName</c> only on the root form of the core area. Names no parameter.</summary>
    private string? QueryRefusalOf(string[] s, Uri uri)
    {
        var names = QueryNames(uri);
        if (names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            return $"a query parameter on {Origin} is repeated";
        }

        var rootArea = s.Length == 3 && Is(s[0], "_apis") && Is(s[1], "resourceAreas") && Is(s[2], AzureDevOpsApi.CoreAreaId);
        return names.All(name => SentQueryNames.Contains(name) || (rootArea && Is(name, "organizationName")))
            ? null
            : $"a query parameter on {Origin} is none that the app or the command sends (D352, D371)";
    }

    public override void Answered(Uri uri, LiveCredentialForm form, int status)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!IsMadeUpConnectionData(uri.AbsolutePath.Split('/')[1..].Select(Uri.UnescapeDataString).ToArray()))
        {
            return;
        }

        lock (_gate)
        {
            if (form == LiveCredentialForm.None)
            {
                _madeUpAnswer = status;
            }
            else
            {
                // Any answer to a read with a credential closes the gate, which admits one anyway.
                _madeUpTokenAdmitted = true;
            }
        }
    }

    private bool Admits(string[] s, Uri uri, LiveCredentialForm form)
    {
        if (s.Length == 3 && Is(s[0], _organization) && Is(s[1], "_apis") && Is(s[2], "connectionData"))
        {
            return true;
        }

        if ((s.Length == 3 && Is(s[0], "_apis") && Is(s[1], "resourceAreas") && Is(s[2], AzureDevOpsApi.CoreAreaId) && QueryValue(uri, "organizationName") is { } name && Is(name, _organization))
            || (s.Length == 4 && Is(s[0], _organization) && Is(s[1], "_apis") && Is(s[2], "resourceAreas") && Is(s[3], AzureDevOpsApi.CoreAreaId)))
        {
            return true;
        }

        var inProject = s.Length >= 3 && Is(s[0], _organization) && Is(s[1], _project);
        if (inProject && s.Length == 5 && Is(s[2], "_apis") && Is(s[3], "git") && Is(s[4], "repositories"))
        {
            return true;
        }

        if (inProject && s.Length >= 6 && Is(s[2], "_apis") && Is(s[3], "git") && Is(s[4], "repositories") && Is(s[5], _repository))
        {
            return true;
        }

        if (inProject && s.Length >= 5 && Is(s[2], "_git") && Is(s[3], _repository))
        {
            return true;
        }

        if (IsMadeUpConnectionData(s))
        {
            if (form == LiveCredentialForm.None)
            {
                return true;
            }

            lock (_gate)
            {
                // Once per guard, which is once per run: the check that admits it takes it.
                if (_madeUpAnswer != 404 || _madeUpTokenAdmitted)
                {
                    return false;
                }

                _madeUpTokenAdmitted = true;
                return true;
            }
        }

        return false;
    }

    private bool IsMadeUpConnectionData(string[] s) =>
        s.Length == 3 && Is(s[0], MadeUpOrganization) && Is(s[1], "_apis") && Is(s[2], "connectionData");

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>GitLab's guard (15.4, D349): <c>https://gitlab.com:443/api/v4/</c> alone, Bearer or no credential, no dot segment.</summary>
internal sealed class GitLabReach : LiveReach
{
    public override HostingProvider Provider => HostingProvider.GitLab;

    public override string Host => "gitlab.com";

    public override string? RefusalOf(Uri uri, LiveCredentialForm form)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!OnOrigin(uri))
        {
            return $"it is not on {Origin}";
        }

        if (form is not (LiveCredentialForm.None or LiveCredentialForm.Bearer))
        {
            return $"{Origin} takes Bearer or no credential, not {form}";
        }

        var segments = RawSegments(uri).ToList();
        if (segments.Any(raw => raw.Length < 3 || raw[0] != "api" || raw[1] != "v4" || raw.Any(IsDotSegment) || raw.Any(segment => segment.Contains('\\', StringComparison.Ordinal))))
        {
            return $"the path is not under {Origin}/api/v4/";
        }

        return null;
    }
}
