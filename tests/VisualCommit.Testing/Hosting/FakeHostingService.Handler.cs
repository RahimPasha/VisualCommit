using System.Net;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fakes' conversion between <see cref="HttpClient"/>'s messages and their routes' request and
/// answer (docs/phase-7b-design.md 2.3), as 7a's <see cref="FakeGitHubHandler"/> made it: a request
/// served by its path whatever its host, its headers by name without case (values joined by
/// <c>", "</c>), <c>Host</c> added; an answer with its headers; a dropped answer an
/// <see cref="HttpRequestException"/>, as a closed connection is.
/// </summary>
internal static class FakeServiceConversion
{
    /// <summary>A request as the routes take it: method, the address's authority, its raw path and query, headers and body.</summary>
    public static async Task<AuthRemoteRouteRequest> ToRouteRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri ?? throw new InvalidOperationException("A request to the fake needs an absolute address.");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in request.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (request.Content is not null)
        {
            foreach (var (name, values) in request.Content.Headers.NonValidated)
            {
                headers[name] = string.Join(", ", values);
            }
        }

        headers.TryAdd("Host", uri.Authority);
        var query = uri.Query.Length > 0 ? uri.Query[1..] : string.Empty;
        return new AuthRemoteRouteRequest(request.Method.Method, uri.Authority, uri.AbsolutePath, query, headers, body);
    }

    /// <summary>An answer as <see cref="HttpClient"/> gets it; a dropped one throws "The fake of <paramref name="displayName"/> closed the connection without an answer.".</summary>
    /// <exception cref="HttpRequestException">The answer was dropped.</exception>
    public static HttpResponseMessage ToResponse(AuthRemoteResponse answer, HttpRequestMessage request, string displayName)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Drop)
        {
            throw new HttpRequestException($"The fake of {displayName} closed the connection without an answer.");
        }

        var response = new HttpResponseMessage((HttpStatusCode)answer.Status)
        {
            ReasonPhrase = answer.Reason,
            RequestMessage = request,
            Content = new ByteArrayContent(answer.Body),
            Version = HttpVersion.Version11,
        };
        foreach (var (name, value) in answer.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }
}

/// <summary>
/// A fake's routes in process (<see cref="FakeHostingService.CreateHandler"/>): every request served
/// by its path whatever its host, and recorded, as over TCP; git's paths get the fake's answer to an
/// unclaimed path. A dropped answer is an <see cref="HttpRequestException"/>: "The fake of
/// {DisplayName} closed the connection without an answer.".
/// </summary>
internal sealed class FakeHostingServiceHandler(FakeHostingService fake) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var routeRequest = await FakeServiceConversion.ToRouteRequestAsync(request, cancellationToken).ConfigureAwait(false);
        var answer = await fake.ServeAsync(routeRequest, cancellationToken).ConfigureAwait(false);
        return FakeServiceConversion.ToResponse(answer, request, fake.DisplayName);
    }
}
