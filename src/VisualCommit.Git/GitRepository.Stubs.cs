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

    // WP4: GitRepository.Stash.cs, .Tags.cs, .Ignore.cs.

    public Task<string?> StashAsync(string? message, bool includeUntracked, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(StashAsync));

    public Task<ApplyResult> ApplyStashAsync(string stashCommit, StashApplyMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(ApplyStashAsync));

    public Task DropStashAsync(string stashCommit, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(DropStashAsync));

    public Task CreateTagAsync(string name, string target, string? message, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(CreateTagAsync));

    public Task DeleteTagAsync(string name, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(DeleteTagAsync));

    public Task AppendToGitignoreAsync(string pattern, CancellationToken cancellationToken = default) =>
        throw NotBuilt(nameof(AppendToGitignoreAsync));
}
