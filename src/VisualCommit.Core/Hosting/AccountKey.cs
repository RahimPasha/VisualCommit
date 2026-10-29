namespace VisualCommit.Core.Hosting;

/// <summary>The hosting services the app knows (docs/phase-7a-design.md 1.2). Phase 7b adds Azure DevOps and GitLab.</summary>
public enum HostingProvider
{
    GitHub,
}

/// <summary>
/// One account on one host (D263): which provider, which host and which login. The host and the
/// login are kept in lower case (invariant), so two keys of one account compare equal; the login's
/// own case is <c>HostingAccount.Login</c>.
/// </summary>
/// <param name="Provider">The hosting service.</param>
/// <param name="Host">The host: <c>github.com</c>, or <c>127.0.0.1:47395</c> for the test harness's fake.</param>
/// <param name="Login">The account's login.</param>
public sealed record AccountKey(HostingProvider Provider, string Host, string Login)
{
    /// <summary>The host in lower case.</summary>
    public string Host { get; } = Normalise(Host);

    /// <summary>The login in lower case.</summary>
    public string Login { get; } = Normalise(Login);

    /// <summary>The provider's id in files and keys: <c>github</c>.</summary>
    public string ProviderId => Provider switch
    {
        HostingProvider.GitHub => "github",
        _ => throw new InvalidOperationException($"Unknown hosting provider {Provider}."),
    };

    /// <summary>The account's id: <c>github:github.com:octocat</c>.</summary>
    public string Id => $"{ProviderId}:{Host}:{Login}";

    public override string ToString() => Id;

    private static string Normalise(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.ToLowerInvariant();
    }
}
