using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's members of <see cref="IGitRepository"/> that are not built yet (docs/phase-3-design.md,
/// section 8). Work package WP0 declared every member at once, so that no later package edits
/// the interface; each package deletes the stubs it implements from this file, and the file goes
/// when the last one is gone. The comment above each group names the package and its file.
/// </summary>
public sealed partial class GitRepository
{
    private static NotImplementedException NotBuilt(string member) =>
        new($"GitRepository.{member} is not built yet (phase 3).");

    // WP1: GitRepository.Branches.cs, .History.cs, .InProgress.cs, .Backups.cs.

    public Task<int> CountDetachedCommitsAsync(CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CountDetachedCommitsAsync));

    public Task<bool> IsAncestorAsync(string ancestor, string descendant, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(IsAncestorAsync));

    public Task<int> CountCommitsAsync(string from, string to, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CountCommitsAsync));

    public Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(ResolveAsync));

    public Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(ReadWorktreesAsync));

    public Task<BackupResult> SaveBackupRefsAsync(BackupRequest request, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(SaveBackupRefsAsync));

    public Task<string?> SnapshotForHardResetAsync(string target, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(SnapshotForHardResetAsync));

    public Task<string?> SnapshotTrackedChangesAsync(CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(SnapshotTrackedChangesAsync));

    public Task CheckoutBranchAsync(string branch, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CheckoutBranchAsync));

    public Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CheckoutTrackingBranchAsync));

    public Task CheckoutDetachedAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CheckoutDetachedAsync));

    public Task CreateBranchAsync(string name, string startPoint, bool checkout, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CreateBranchAsync));

    public Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(RenameBranchAsync));

    public Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(DeleteBranchAsync));

    public Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(FastForwardBranchAsync));

    public Task<MergeResult> MergeAsync(string reference, MergeMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(MergeAsync));

    public Task ResetAsync(string commit, ResetMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(ResetAsync));

    public Task<ApplyResult> CherryPickAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CherryPickAsync));

    public Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(RevertAsync));

    public Task AbortAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(AbortAsync));

    // WP2: GitRepository.Remotes.cs (fetch, pull's integration, pushes, upstreams, remotes; the
    // tag push and remote tag deletion of 2.29 are pushes too).

    public Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(FetchAsync));

    public Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(IntegratePullAsync));

    public Task PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(PushAsync));

    public Task SetUpstreamAsync(string branch, string remoteBranchRef, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(SetUpstreamAsync));

    public Task UnsetUpstreamAsync(string branch, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(UnsetUpstreamAsync));

    public Task DeleteRemoteBranchAsync(string remote, string branch, string expectedCommit, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(DeleteRemoteBranchAsync));

    public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync(CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(ReadRemotesAsync));

    public Task AddRemoteAsync(string name, string url, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(AddRemoteAsync));

    public Task RenameRemoteAsync(string name, string newName, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(RenameRemoteAsync));

    public Task SetRemoteUrlAsync(string name, string url, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(SetRemoteUrlAsync));

    public Task RemoveRemoteAsync(string name, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(RemoveRemoteAsync));

    public Task PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(PushTagAsync));

    public Task DeleteRemoteTagAsync(string remote, string tag, string expectedObject, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(DeleteRemoteTagAsync));
}
