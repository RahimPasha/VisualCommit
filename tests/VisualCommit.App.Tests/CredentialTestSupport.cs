using VisualCommit.Testing;

namespace VisualCommit.App.Tests;

/// <summary>What the credential classes of the App tests share.</summary>
internal static class CredentialTestSupport
{
    public const string Token = "github_pat_FAKE-octocat-1";

    /// <summary>A stub signed in to the server's host as <paramref name="login"/>, answering git with <paramref name="token"/>.</summary>
    public static HostingSessionStub Stub(AuthRemoteServer server, string login = "octocat", string? token = Token) =>
        new(new Uri($"http://127.0.0.1:{server.Port}/"), login, token);
}
