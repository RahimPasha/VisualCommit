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
