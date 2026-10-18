using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>This repository's own settings, written by the settings screen's "This repository" (docs/phase-6-design.md 8.3).</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// <c>git config --local --replace-all</c> or <c>--unset-all</c> per write, in order (8.3).
    /// Work package WP2 gives it its body; nothing calls it before then.
    /// </summary>
    public Task WriteConfigAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Work package WP2 builds WriteConfigAsync (docs/phase-6-design.md 8.3).");
}
