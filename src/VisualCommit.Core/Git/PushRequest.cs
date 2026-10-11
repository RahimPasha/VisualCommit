namespace VisualCommit.Core.Git;

/// <summary>
/// One push of a local branch (phase 3 design 2.19, 2.20 and the drop of 4g):
/// <c>git push --progress [--set-upstream] &lt;remote&gt; refs/heads/&lt;branch&gt;:&lt;remote ref&gt;</c>.
/// </summary>
/// <param name="Remote">The remote's name; never <c>.</c> (a local upstream is not pushed to, D101).</param>
/// <param name="Branch">The local branch's short name.</param>
/// <param name="RemoteRef">The branch's full name on the remote, such as <c>refs/heads/main</c> (the upstream's <see cref="GitRef.UpstreamRemoteRef"/>).</param>
public sealed record PushRequest(string Remote, string Branch, string RemoteRef)
{
    /// <summary><c>--set-upstream</c>: the pushed branch becomes the upstream.</summary>
    public bool SetUpstream { get; init; }

    /// <summary>
    /// Force push with <c>--force-with-lease=&lt;remote ref&gt; --force-if-includes</c> and no
    /// expected value (2.20, D100).
    /// </summary>
    public bool ForceWithLease { get; init; }
}
