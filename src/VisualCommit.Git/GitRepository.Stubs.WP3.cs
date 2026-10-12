using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 4's conflict members, the merge tool and the backups' clean-up (docs/phase-4-design.md
/// sections 9 and 11) as stubs, so that the other work packages build against their contract.
/// Work package WP3 replaces this file with <c>GitRepository.Conflicts.cs</c> and its part of
/// <c>GitRepository.Backups.cs</c>.
/// </summary>
public sealed partial class GitRepository
{
    public Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md section 9");
}
