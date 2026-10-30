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
}

/// <summary>
/// The app's requests in the live command (docs/phase-7a-design.md 14.5): a handler between the
/// session's clients and GitHub that records each request as a <see cref="LiveRequest"/> and passes
/// the request and the answer on unchanged. It reads no body, and of the headers only the four
/// <see cref="LiveRequest"/> names, and of <c>Authorization</c> only whether it was sent. One
/// <see cref="Store"/> serves every handler the session creates (its client's and the avatar
/// cache's).
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
    /// D327's reach, against <paramref name="requests"/>: every one <c>https</c>, and every one that
    /// sent <c>Authorization</c> to <see cref="ApiHost"/>. One line per request that breaks a rule.
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

    /// <summary>Per host, in the order first seen: the requests with and without <c>Authorization</c> and the statuses (run.txt, 14.5).</summary>
    internal static IReadOnlyList<string> PerHost(IEnumerable<LiveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        return
        [
            .. requests.GroupBy(request => request.Host, StringComparer.OrdinalIgnoreCase).Select(host =>
            {
                var with = host.Count(request => request.AuthorizationSent);
                var statuses = string.Join(", ", host
                    .GroupBy(request => request.Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : "failed: " + request.FailedWith)
                    .Select(group => $"{group.Key} ×{group.Count().ToString(CultureInfo.InvariantCulture)}"));
                return $"The app's requests to {host.Key}: {host.Count().ToString(CultureInfo.InvariantCulture)}, "
                    + $"{with.ToString(CultureInfo.InvariantCulture)} with Authorization and {(host.Count() - with).ToString(CultureInfo.InvariantCulture)} without; answered {statuses}.";
            }),
        ];
    }

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
