using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;

namespace VisualCommit.Testing;

/// <summary>
/// The start of a fake <see cref="IGitRepository"/> for view-model tests: a test's fake supplies
/// what it reads, and the rest reports a clean working tree or refuses. A fake that a
/// <c>RepositoryViewModel</c> opens is asked for its status, so the default is a clean tree.
/// Phase 3's reads give empty results (no remotes, no worktrees, nothing detached) and its
/// writes throw <see cref="NotSupportedException"/>. So do phase 4's: its reads give an empty
/// list or null (no merge tool, no operation message), the backups' clean-up removes nothing.
/// Phase 5's reads find nothing (no history, blame, reflog, backups, differences, matches, ignored
/// files or stash paths), the undo history's read sees HEAD alone, so that no action leaves an
/// entry, and an undo step and C12's writes throw. Phase 6's reads find no ignored file in any
/// operation's way and no pull setting, a folder is opened at its top, and the repository's
/// settings write throws (docs/phase-6-design.md 1.2, 8.3, 8.9, 9.3).
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

    public virtual Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

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

    public virtual Task<bool> CommitExistsAsync(string sha, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

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

    public virtual Task CheckoutBranchAsync(string branch, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task CheckoutDetachedAsync(string commit, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RestoreWorkingTreeFilesAsync(string source, IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<bool> PutBackUnclearedStashAsync(string stashCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlySet<string>> FindIgnoredAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

    public virtual Task<IReadOnlyList<string>> FindIgnoredInTheWayOfMergeAsync(string into, string source, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<IReadOnlyList<string>> FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string? mergeParent = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(string tip, string onto, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(RebasePlan plan, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<IReadOnlyList<string>> FindIgnoredInTheWayOfStashAsync(string stashCommit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task CreateBranchAsync(string name, string startPoint, bool checkout, bool noTrack, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<MergeResult> MergeAsync(string reference, MergeMode mode, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
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

    public virtual Task<PushOutcome> PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default) =>
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

    public virtual Task<PushOutcome> PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteRemoteTagAsync(string remote, string tag, string expectedObject, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> StashAsync(string? message, bool includeUntracked, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ApplyResult> ApplyStashAsync(string stashCommit, StashApplyMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DropStashAsync(string stashCommit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<StashPaths> ReadStashPathsAsync(string stashCommit, CancellationToken cancellationToken = default) =>
        Task.FromResult(new StashPaths([], []));

    public virtual Task CreateTagAsync(string name, string target, string? message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task DeleteTagAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task AppendToGitignoreAsync(string pattern, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    // Phase 4 (docs/phase-4-design.md): reads give empty results or null and never throw, the
    // backups' clean-up removes nothing, writes are refused.

    public virtual Task<RebaseResult> RebaseAsync(string onto, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<bool> ReadRebaseAutoStashAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public virtual Task<RebaseList> ReadRebaseListAsync(string onto, string branch, bool fromCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RebaseList(onto, string.Empty, [], 0));

    public virtual Task<RebaseResult> InteractiveRebaseAsync(RebasePlan plan, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<PullResult> IntegratePullAsync(
        GitRef branch,
        PullIntegration integration,
        Func<string, Task>? beforeRebase,
        NetworkOptions? options = null,
        PullIntegrationOptions? pullOptions = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PullConfiguration(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null, null, null));

    public virtual Task<PullConfiguration> ReadPullFastForwardSettingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PullConfiguration(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null, null, null));

    public virtual Task WriteConfigAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<SequenceResult> CherryPickAsync(IReadOnlyList<string> commitsOldestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<SequenceResult> RevertAsync(IReadOnlyList<string> commitsNewestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task QuitSequenceAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ContinueResult> ContinueAsync(InProgressState state, string? message, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<ContinueResult> SkipAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> ReadOperationMessageAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public virtual Task<ConflictContent> ReadConflictAsync(ConflictEntry entry, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConflictContent([], 7, OursBinary: false, TheirsBinary: false, IsSymbolicLink: false, WorkingTreeExists: false));

    public virtual Task<byte[]?> ReadConflictSideAsync(ConflictEntry entry, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(null);

    public virtual Task WriteResolutionAsync(string path, byte[] content, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task TakeSideAsync(string path, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task RemoveConflictedAsync(string path, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<string?> ReadMergeToolAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public virtual Task<MergeToolResult> RunMergeToolAsync(string path, string tool, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<IReadOnlyList<string>> ReadStagedConflictMarkersAsync(IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RemovedBackup>>([]);

    public virtual Task<IReadOnlyList<string>> FindBackupsToRemoveAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task ReadFileHistoryAsync(string startSha, string path, Action<FileHistoryRow> onRow, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public virtual Task<BlameResult> ReadBlameAsync(string? sha, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BlameResult([]));

    public virtual Task<IReadOnlyList<string>> ReadTreeFilesAsync(string sha, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task<ReflogResult> ReadReflogAsync(string fullRef, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReflogResult.Empty);

    public virtual Task<IReadOnlySet<string>> FindLostCommitsAsync(IReadOnlyCollection<string> shas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

    public virtual Task<IReadOnlyList<BackupInfo>> ReadBackupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupInfo>>([]);

    public virtual Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompareResult(from, to, CompareBase.Trees, from, [], RenamesSkipped: false, Counts: null));

    public virtual Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public virtual async Task<UndoState> ReadRefStatesAsync(UndoScope scope, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        new() { Head = await ((IGitRepository)this).ReadHeadAsync(cancellationToken).ConfigureAwait(false) };

    public virtual Task<UndoStepResult> RunUndoStepAsync(UndoStep step, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
