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
    public Task<ConflictContent> ReadConflictAsync(ConflictEntry entry, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.3");

    public Task<byte[]?> ReadConflictSideAsync(ConflictEntry entry, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.6");

    public Task WriteResolutionAsync(string path, byte[] content, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.7");

    public Task TakeSideAsync(string path, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.8");

    public Task RemoveConflictedAsync(string path, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.8");

    public Task<string?> ReadMergeToolAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.9");

    public Task<MergeToolResult> RunMergeToolAsync(string path, string tool, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md 11.9");

    public Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP3: docs/phase-4-design.md section 9");
}
