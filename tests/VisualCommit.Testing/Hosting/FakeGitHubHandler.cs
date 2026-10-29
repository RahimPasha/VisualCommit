using System.Net;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake of GitHub's routes in process (docs/phase-7a-design.md 2.3, <see cref="FakeGitHub.CreateHandler"/>):
/// every request is served by its path whatever its host, and recorded, as over TCP; git's paths
/// get 404. A dropped answer (<see cref="FakeGitHubFault.CreatedThenDropped"/>) is an
/// <see cref="HttpRequestException"/>, as a closed connection is.
/// </summary>
public sealed class FakeGitHubHandler : HttpMessageHandler
{
    private readonly FakeGitHub _fake;

    internal FakeGitHubHandler(FakeGitHub fake) => _fake = fake;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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
        var routeRequest = new AuthRemoteRouteRequest(request.Method.Method, uri.Authority, uri.AbsolutePath, query, headers, body);
        var answer = await _fake.ServeAsync(routeRequest, cancellationToken).ConfigureAwait(false);
        if (answer.Drop)
        {
            throw new HttpRequestException("The fake of GitHub closed the connection without an answer.");
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
