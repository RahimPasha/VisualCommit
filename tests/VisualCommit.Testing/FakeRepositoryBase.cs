using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing;

/// <summary>
/// The start of a fake <see cref="IGitRepository"/> for view-model tests: a test's fake supplies
/// what it reads, and the rest reports a clean working tree or refuses. A fake that a
/// <c>RepositoryViewModel</c> opens is asked for its status, so the default is a clean tree.
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
}
