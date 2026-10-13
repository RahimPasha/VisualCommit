using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// What two commits or branches differ in (docs/phase-5-design.md 6.3, 0.3, D177). Work package WP1
/// builds it here, on the changed files' parser of <c>GitRepository.Commits.cs</c>
/// (<c>ParseNameStatus</c>); until then it throws.
/// </summary>
public sealed partial class GitRepository
{
    public Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Compare is work package WP1's (docs/phase-5-design.md 6.3).");
}
