using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 4's lists of picks and reverts, Continue and Skip (docs/phase-4-design.md sections 4,
/// 5 and 8) as stubs, so that the other work packages build against their contract. Work package
/// WP2 replaces this file with <c>GitRepository.History.cs</c> and <c>GitRepository.Sequence.cs</c>.
/// </summary>
public sealed partial class GitRepository
{
    public Task<SequenceResult> CherryPickAsync(IReadOnlyList<string> commitsOldestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md section 4");

    public Task<SequenceResult> RevertAsync(IReadOnlyList<string> commitsNewestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md section 4");

    public Task QuitSequenceAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md section 4");

    public Task<ContinueResult> ContinueAsync(InProgressState state, string? message, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md 5.4");

    public Task<ContinueResult> SkipAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md 5.4");

    public Task<string?> ReadOperationMessageAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("WP2: docs/phase-4-design.md 5.3");
}
