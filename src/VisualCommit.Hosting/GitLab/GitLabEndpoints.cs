namespace VisualCommit.Hosting.GitLab;

/// <summary>Where GitLab is (docs/phase-7b-design.md 1.4): its web address, which also serves git, and its API's address.</summary>
/// <param name="WebBase">The web address, ending in <c>/</c>.</param>
/// <param name="ApiBase">The REST API's address, ending in <c>/</c>.</param>
public sealed record GitLabEndpoints(Uri WebBase, Uri ApiBase)
{
    /// <summary>gitlab.com: <c>https://gitlab.com/</c> and <c>https://gitlab.com/api/v4/</c>.</summary>
    public static GitLabEndpoints GitLabCom { get; } = new(new Uri("https://gitlab.com/"), new Uri("https://gitlab.com/api/v4/"));

    /// <summary>The test harness's fake on <paramref name="port"/>: <c>http://127.0.0.1:&lt;port&gt;/</c> and its <c>api/v4/</c>.</summary>
    public static GitLabEndpoints Local(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        var web = new Uri(FormattableString.Invariant($"http://127.0.0.1:{port}/"));
        return new GitLabEndpoints(web, new Uri(web, "api/v4/"));
    }

    /// <summary>The web address's host, with its port when it is not the scheme's: <c>gitlab.com</c> or <c>127.0.0.1:&lt;port&gt;</c>.</summary>
    public string Host => WebBase.Authority;
}
