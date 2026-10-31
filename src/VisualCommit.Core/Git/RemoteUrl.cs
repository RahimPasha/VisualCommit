namespace VisualCommit.Core.Git;

/// <summary>
/// One configured URL of a remote (docs/phase-7a-design.md 1.2), as the refs read reads it
/// (<c>remote.&lt;r&gt;.url</c> or <c>remote.&lt;r&gt;.pushurl</c>): as configured, before git's
/// <c>insteadOf</c> rules, and never shown.
/// </summary>
/// <param name="Remote">The remote's name.</param>
/// <param name="Url">The URL as configured.</param>
/// <param name="IsPushUrl">It is a <c>pushurl</c>, not a <c>url</c>.</param>
public sealed record RemoteUrl(string Remote, string Url, bool IsPushUrl);
