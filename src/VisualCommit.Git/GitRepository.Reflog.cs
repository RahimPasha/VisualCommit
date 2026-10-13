using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The reflog, its lost commits and the backups list (docs/phase-5-design.md 5.2, 5.3, 0.2, D180).
/// Work package WP1 builds them here, the backups on phase 4's parser
/// (<c>GitRepository.Backups.cs</c>); until then each throws.
/// </summary>
public sealed partial class GitRepository
{
    public Task<ReflogResult> ReadReflogAsync(string fullRef, int limit, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The reflog is work package WP1's (docs/phase-5-design.md 5.2).");

    public Task<IReadOnlySet<string>> FindLostCommitsAsync(IReadOnlyCollection<string> shas, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Lost commits are work package WP1's (docs/phase-5-design.md 5.2).");

    public Task<IReadOnlyList<BackupInfo>> ReadBackupsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The backups list is work package WP1's (docs/phase-5-design.md 5.3).");
}
