namespace VisualCommit.Core.Hosting;

/// <summary>
/// The hosting services the app knows (docs/phase-7a-design.md 1.2, docs/phase-7b-design.md 1.2):
/// 7b's are appended, so GitHub keeps its ordinal. Their names and ids are in <see cref="HostingProviders"/>.
/// </summary>
public enum HostingProvider
{
    GitHub,
    AzureDevOps,
    GitLab,
}

/// <summary>
/// One account on one host (D263): which provider, which host and which login. The host and the
/// login are kept in lower case (invariant), so two keys of one account compare equal; the login's
/// own case is <c>HostingAccount.Login</c>.
/// </summary>
/// <param name="Provider">The hosting service.</param>
/// <param name="Host">The host: <c>github.com</c>, <c>dev.azure.com</c>, <c>gitlab.com</c>, or <c>127.0.0.1:&lt;port&gt;</c> for a test harness's fake.</param>
/// <param name="Login">The account's login: GitHub's and GitLab's username, Azure DevOps' organization.</param>
public sealed record AccountKey(HostingProvider Provider, string Host, string Login)
{
    /// <summary>The host in lower case.</summary>
    public string Host { get; } = Normalise(Host);

    /// <summary>The login in lower case.</summary>
    public string Login { get; } = Normalise(Login);

    /// <summary>The provider's id in files and keys: <c>github</c>, <c>azure-devops</c>, <c>gitlab</c> (<see cref="HostingProviders.Id"/>).</summary>
    public string ProviderId => HostingProviders.Id(Provider);

    /// <summary>
    /// The account's id: <c>github:github.com:octocat</c>, <c>gitlab:gitlab.com:ada</c>,
    /// <c>azure-devops:dev.azure.com:&lt;organization&gt;</c> (an Azure DevOps key's login is the organization).
    /// </summary>
    public string Id => $"{ProviderId}:{Host}:{Login}";

    public override string ToString() => Id;

    private static string Normalise(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.ToLowerInvariant();
    }
}
