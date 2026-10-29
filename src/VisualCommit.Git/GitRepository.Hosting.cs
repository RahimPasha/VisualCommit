using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>What the hosting reads of the repository (docs/phase-7a-design.md 1.3): its URL rewrites.</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// The repository's URL rewrites: one <c>git config --null --get-regexp ^url\.</c>, exit 1
    /// meaning none (4.3).
    /// </summary>
    /// <remarks>Still no rules and no git call: WP-C reads them (docs/phase-7a-design.md 4.3).</remarks>
    public Task<UrlRewrites> ReadUrlRewritesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UrlRewrites.None);
}
