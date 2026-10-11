namespace VisualCommit.Core.Git;

/// <summary>A configured remote, as <c>git remote -v</c> lists it (phase 3 design 2.23).</summary>
/// <param name="Name">The remote's name.</param>
/// <param name="FetchUrl">Its URL as configured, credentials included: show it only through <see cref="GitCommand.HideCredentials"/> (D103).</param>
/// <param name="PushUrl">Its push URL when it differs from <paramref name="FetchUrl"/>; null otherwise.</param>
public sealed record RemoteInfo(string Name, string FetchUrl, string? PushUrl)
{
    /// <summary>The fetch URL as the app shows it: credentials hidden (D103).</summary>
    public string ShownUrl => GitCommand.HideCredentials(FetchUrl);
}

/// <summary>A worktree of the repository, as <c>git worktree list --porcelain</c> lists it (phase 3 design 2.7).</summary>
/// <param name="Path">Its working tree, with the platform's separators.</param>
/// <param name="Branch">The full name of the branch checked out there, or null (detached, or bare).</param>
/// <param name="IsMain">The main worktree (listed first).</param>
public sealed record WorktreeInfo(string Path, string? Branch, bool IsMain);
