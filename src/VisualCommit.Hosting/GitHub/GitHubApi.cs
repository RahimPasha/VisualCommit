namespace VisualCommit.Hosting.GitHub;

/// <summary>What every request to GitHub's REST API names (docs/phase-7a-design.md 1.4, 6.2).</summary>
public static class GitHubApi
{
    /// <summary>The API version every request asks for (<c>X-GitHub-Api-Version</c>).</summary>
    public const string Version = "2026-03-10";

    /// <summary>The media type every request accepts.</summary>
    public const string MediaType = "application/vnd.github+json";
}
