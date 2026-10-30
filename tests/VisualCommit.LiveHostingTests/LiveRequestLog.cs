using System.Globalization;

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
internal sealed class LiveRequestLog(HttpMessageHandler inner, LiveRequestLog.Store store) : DelegatingHandler(inner)
{
    /// <summary>The only host that may see the token (D327).</summary>
    public const string ApiHost = "api.github.com";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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
