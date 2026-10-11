using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing;

/// <summary>
/// The start of a fake <see cref="IGitRepository"/> for view-model tests: a test's fake supplies
/// what it reads, and the rest reports a clean working tree or refuses. A fake that a
/// <c>RepositoryViewModel</c> opens is asked for its status, so the default is a clean tree.
/// Phase 3's reads give empty results (no remotes, no worktrees, nothing detached) and its
/// writes throw <see cref="NotSupportedException"/>.
/// </summary>
public abstract class FakeRepositoryBase : IGitRepository
{
    public abstract string WorkingDirectory { get; }

    public abstract string GitDirectory { get; }

    public abstract string CommonDirectory { get; }

    public abstract string Name { get; }

    public abstract Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default);

    public abstract Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default);

    public abstract Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default);

    public abstract IRepositoryWatcher CreateWatcher();

    public virtual Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkingTreeStatus.Clean);

    public virtual Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task StageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task UnstageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task ApplyPatchAsync(string patch, bool toIndex, PatchDirection direction, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<DiscardSnapshot> SaveSnapshotAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DiscardAsync(IReadOnlyList<string> paths, IReadOnlySet<string> untracked, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RestoreSnapshotAsync(DiscardSnapshot snapshot, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string> CommitAsync(string message, bool amend, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    // Phase 3 (docs/phase-3-design.md, section 2): reads give empty results, writes are refused.

    public virtual Task<int> CountDetachedCommitsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public virtual Task<bool> IsAncestorAsync(string ancestor, string descendant, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public virtual Task<int> CountCommitsAsync(string from, string to, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public virtual Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public virtual Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorktreeInfo>>([]);

    public virtual Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RemoteInfo>>([]);

    public virtual Task<BackupResult> SaveBackupRefsAsync(BackupRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> SnapshotForHardResetAsync(string target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> SnapshotTrackedChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> SnapshotIndexAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CheckoutBranchAsync(string branch, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CheckoutDetachedAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CreateBranchAsync(string name, string startPoint, bool checkout, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<MergeResult> MergeAsync(string reference, MergeMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task ResetAsync(string commit, ResetMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ApplyResult> CherryPickAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task AbortAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task SetUpstreamAsync(string branch, string remoteBranchRef, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task UnsetUpstreamAsync(string branch, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteRemoteBranchAsync(string remote, string branch, string expectedCommit, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task AddRemoteAsync(string name, string url, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RenameRemoteAsync(string name, string newName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task SetRemoteUrlAsync(string name, string url, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RemoveRemoteAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteRemoteTagAsync(string remote, string tag, string expectedObject, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> StashAsync(string? message, bool includeUntracked, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ApplyResult> ApplyStashAsync(string stashCommit, StashApplyMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DropStashAsync(string stashCommit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CreateTagAsync(string name, string target, string? message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteTagAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task AppendToGitignoreAsync(string pattern, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
