using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The search over the graph's rows (docs/phase-5-design.md 3.2, 0.3, D174). Work package WP1 builds
/// it here; until then it throws.
/// </summary>
public sealed partial class GitRepository
{
    public Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Search is work package WP1's (docs/phase-5-design.md 3.2).");
}
