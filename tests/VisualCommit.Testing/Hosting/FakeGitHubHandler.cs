namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake of GitHub's routes in process (docs/phase-7a-design.md 2.3, <see cref="FakeGitHub.CreateHandler"/>):
/// every request is served by its path whatever its host, and recorded, as over TCP; git's paths
/// get 404. A dropped answer (<see cref="FakeGitHubFault.CreatedThenDropped"/>) is an
/// <see cref="HttpRequestException"/>, as a closed connection is. Since phase 7b the conversion is
/// the fakes' shared one (<see cref="FakeServiceConversion"/>, docs/phase-7b-design.md 2.3).
/// </summary>
public sealed class FakeGitHubHandler : HttpMessageHandler
{
    private readonly FakeGitHub _fake;

    internal FakeGitHubHandler(FakeGitHub fake) => _fake = fake;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var routeRequest = await FakeServiceConversion.ToRouteRequestAsync(request, cancellationToken).ConfigureAwait(false);
        var answer = await _fake.ServeAsync(routeRequest, cancellationToken).ConfigureAwait(false);
        return FakeServiceConversion.ToResponse(answer, request, "GitHub");
    }
}
