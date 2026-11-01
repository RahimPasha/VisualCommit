namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The credential a request to a fake carried (docs/phase-7b-design.md 1.9, 2.3), read by the core
/// before any route: one of <c>Authorization: Bearer</c>, <c>Authorization: Basic</c> (with a user,
/// or <see cref="BasicEmptyUser"/>, <c>":" + token</c>), a <c>PRIVATE-TOKEN</c> header or a
/// <c>private_token</c> query parameter; <see cref="Other"/> for another scheme, a Basic header that
/// cannot be read, or two credentials at once.
/// </summary>
public enum FakeAuthorizationForm
{
    None,
    Bearer,
    Basic,
    BasicEmptyUser,
    PrivateToken,
    Query,
    Other,
}

/// <summary>One request a fake answered (its API and pages; git's are <see cref="FakeHostingService.GitRequests"/>). Never holds a token.</summary>
/// <param name="Method"><c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c>.</param>
/// <param name="Host">The host it was sent to: <c>127.0.0.1:&lt;port&gt;</c>, or any host in process.</param>
/// <param name="Path">The path without the query, raw as received (GitLab's <c>%2F</c> kept).</param>
/// <param name="Query">The query without <c>?</c>; empty when none. A <c>private_token</c> parameter's value is never kept: it reads <c>private_token=***</c>.</param>
/// <param name="Route">The route's key, lower-case; null for a path no route serves.</param>
/// <param name="Authorization">The form of the credential it carried.</param>
/// <param name="Login">The login its token signed in as (<see cref="FakeServiceCall.Login"/>); null when none did.</param>
/// <param name="Status">The answer's status code; 0 for <see cref="FakeServiceFault.Dropped"/>, which answered nothing.</param>
/// <param name="Body">The body of a POST, PUT or PATCH; null for the others.</param>
public sealed record FakeServiceRequest(string Method, string Host, string Path, string Query, string? Route, FakeAuthorizationForm Authorization, string? Login, int Status, string? Body);

/// <summary>
/// A failure a fake answers once in place of a route's normal answer (<see cref="FakeHostingService.FailNext"/>,
/// docs/phase-7b-design.md 1.9, 2.3). Each fake derives a sealed one with static members and a
/// private constructor; the core answers <see cref="Dropped"/> and <see cref="ServedThenDropped"/>
/// itself, and hands every other one to the fake's answer (<see cref="FakeServiceCall.Fault"/>),
/// which answers it where its checks put it.
/// </summary>
public class FakeServiceFault
{
    /// <summary>A fault named <paramref name="name"/>, as its static member is.</summary>
    protected FakeServiceFault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Its name, as its static member's.</summary>
    public string Name { get; }

    /// <summary>The connection closed without an answer before anything was done: nothing is served; recorded with status 0.</summary>
    public static FakeServiceFault Dropped { get; } = new(nameof(Dropped));

    /// <summary>The request served in full (a create made), then the connection closed without an answer (a lost answer).</summary>
    public static FakeServiceFault ServedThenDropped { get; } = new(nameof(ServedThenDropped));

    public override string ToString() => Name;
}

/// <summary>
/// A hold on a route's next request (<see cref="FakeHostingService.HoldNext"/>): the request waits,
/// before anything is decided about it, until the hold is disposed (at most 120 s) or, in process,
/// the request's own cancellation ends its wait (<see cref="Abandoned"/>). A git route's request
/// waits at the server's gate (<see cref="AuthRemoteServer.HoldNextRequest"/>).
/// </summary>
public sealed class FakeServiceHold : IDisposable
{
    private readonly Action _release;
    private int _disposed;

    internal FakeServiceHold(string route, Task arrived, Task released, Task abandoned, Action release)
    {
        Route = route;
        Arrived = arrived;
        Released = released;
        Abandoned = abandoned;
        _release = release;
    }

    /// <summary>The route held: its lower-case key (<c>user</c>), or <c>git:</c> and the repository's folder name in the folder's case (<c>git:Fernhill-Labs/Mobile App/_git/mobile-app</c>).</summary>
    public string Route { get; }

    /// <summary>Completes when the request has arrived and waits; cancelled when the hold is disposed first.</summary>
    public Task Arrived { get; }

    /// <summary>Completes when the hold is disposed, which lets the request go on.</summary>
    public Task Released { get; }

    /// <summary>
    /// Completes when the held request's own cancellation ended its wait before the hold let it go
    /// (in process only, as 7a's <see cref="FakeGitHubHold.Abandoned"/>); never for a git route.
    /// </summary>
    public Task Abandoned { get; }

    /// <summary>Lets the held request go on, or drops the hold when none arrived.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release();
        }
    }

    public override string ToString() => $"FakeServiceHold({Route})";
}

/// <summary>
/// One request on its way through a fake (<see cref="FakeHostingService"/>): the request, its route,
/// the fault waiting for it (never one of the core's two), the credential's form and token, and the
/// login it signed in as once the fake's checks set it. <see cref="ToString"/> never shows the token.
/// </summary>
public sealed class FakeServiceCall
{
    internal FakeServiceCall(AuthRemoteRouteRequest request, string? route, FakeServiceFault? fault, FakeAuthorizationForm authorization, string? token)
    {
        Request = request;
        Route = route;
        Fault = fault;
        Authorization = authorization;
        Token = token;
    }

    /// <summary>The request as the server or the in-process handler gave it.</summary>
    public AuthRemoteRouteRequest Request { get; }

    /// <summary>The route's lower-case key (the fake's <c>RouteOf</c>); null when none serves the path.</summary>
    public string? Route { get; }

    /// <summary>The fault taken for this request, which the fake answers where its checks put it; null when none.</summary>
    public FakeServiceFault? Fault { get; }

    /// <summary>The credential's form.</summary>
    public FakeAuthorizationForm Authorization { get; }

    /// <summary>The token the credential carried (Bearer's, Basic's password, <c>PRIVATE-TOKEN</c>'s, <c>private_token</c>'s); null for <see cref="FakeAuthorizationForm.None"/> and <see cref="FakeAuthorizationForm.Other"/>.</summary>
    public string? Token { get; }

    /// <summary>The login the token signed in as, set by the fake's checks; recorded with the request.</summary>
    public string? Login { get; set; }

    public override string ToString() => $"FakeServiceCall({Request.Method} {Request.Path}, {Route ?? "no route"}, {Authorization}, {(Token is null ? "no token" : "***")}, {Login ?? "no login"})";
}
