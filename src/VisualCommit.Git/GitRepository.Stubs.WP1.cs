using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 4's rebase members (docs/phase-4-design.md sections 1 to 3 and 6) as stubs, so that the
/// other work packages build against their contract. Work package WP1 replaces this file with
/// <c>GitRepository.Rebase.cs</c> and moves the pull's overload into <c>GitRepository.Remotes.cs</c>.
/// </summary>
public sealed partial class GitRepository
{
    public Task<RebaseResult> RebaseAsync(string onto, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP1: docs/phase-4-design.md 1.3");

    public Task<RebaseList> ReadRebaseListAsync(string onto, string branch, bool fromCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP1: docs/phase-4-design.md 2.3");

    public Task<RebaseResult> InteractiveRebaseAsync(RebasePlan plan, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP1: docs/phase-4-design.md 2.4, 2.5");

    /// <summary>
    /// Phase 3's pull for <see cref="PullIntegration.AsConfigured"/> without a callback; the rest
    /// of section 6 (the chosen rebase, the backup's callback) is WP1's.
    /// </summary>
    public Task<PullResult> IntegratePullAsync(
        GitRef branch,
        PullIntegration integration,
        Func<string, Task>? beforeRebase,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default) =>
        integration == PullIntegration.AsConfigured && beforeRebase is null
            ? IntegratePullAsync(branch, options, cancellationToken)
            : throw new NotImplementedException("WP1: docs/phase-4-design.md section 6");
}
