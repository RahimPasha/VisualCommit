namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// Where Azure DevOps is (docs/phase-7b-design.md 1.4): web, git and the API on one origin (D285),
/// each organization under its own first path segment.
/// </summary>
/// <param name="WebBase">The web address, ending in <c>/</c>.</param>
/// <param name="OrganizationHosts">Whether <c>https://&lt;organization&gt;.visualstudio.com</c> is the same service's too (the service only).</param>
public sealed record AzureDevOpsEndpoints(Uri WebBase, bool OrganizationHosts)
{
    /// <summary>The service: <c>https://dev.azure.com/</c>, with the organizations' <c>visualstudio.com</c> hosts.</summary>
    public static AzureDevOpsEndpoints Service { get; } = new(new Uri("https://dev.azure.com/"), OrganizationHosts: true);

    /// <summary>The test harness's fake on <paramref name="port"/>: <c>http://127.0.0.1:&lt;port&gt;/</c>, without organization hosts (D363).</summary>
    public static AzureDevOpsEndpoints Local(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        return new AzureDevOpsEndpoints(new Uri(FormattableString.Invariant($"http://127.0.0.1:{port}/")), OrganizationHosts: false);
    }

    /// <summary>The web address's host, with its port when it is not the scheme's: <c>dev.azure.com</c> or <c>127.0.0.1:&lt;port&gt;</c>.</summary>
    public string Host => WebBase.Authority;

    /// <summary>The live command's sandbox project (D352): A2 lists only it; null: the organization's whole list.</summary>
    public string? ListProject { get; init; }

    /// <summary>The API's address of <paramref name="organization"/>: <see cref="WebBase"/>, the escaped organization and <c>/</c>.</summary>
    public Uri ApiOf(string organization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        return new Uri(WebBase, Uri.EscapeDataString(organization) + "/");
    }
}
