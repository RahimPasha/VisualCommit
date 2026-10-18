using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Keep ignored files (D193, D196, docs/phase-6-design.md 9.3 to 9.10): the reads that find the
/// ignored files an operation without git's own option would overwrite or delete, and the pull's
/// check inside <see cref="IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>.
/// Work package WP1 builds them; until then each finds nothing and the pull's check changes nothing.
/// </summary>
public sealed partial class GitRepository
{
    private static readonly Task<IReadOnlyList<string>> NothingInTheWay = Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>The ignored files in the way of a merge (9.4). Work package WP1's; finds nothing yet.</summary>
    public Task<IReadOnlyList<string>> FindIgnoredInTheWayOfMergeAsync(string into, string source, CancellationToken cancellationToken = default) => NothingInTheWay;

    /// <summary>The ignored files in the way of cherry-picks or reverts (9.5). Work package WP1's; finds nothing yet.</summary>
    public Task<IReadOnlyList<string>> FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string? mergeParent = null, CancellationToken cancellationToken = default) => NothingInTheWay;

    /// <summary>The ignored files in the way of a rebase onto a commit (9.6). Work package WP1's; finds nothing yet.</summary>
    public Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(string tip, string onto, CancellationToken cancellationToken = default) => NothingInTheWay;

    /// <summary>The ignored files in the way of an interactive rebase or a squash (9.7). Work package WP1's; finds nothing yet.</summary>
    public Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(RebasePlan plan, CancellationToken cancellationToken = default) => NothingInTheWay;

    /// <summary>The ignored files in the way of a stash's apply or pop (9.8). Work package WP1's; finds nothing yet.</summary>
    public Task<IReadOnlyList<string>> FindIgnoredInTheWayOfStashAsync(string stashCommit, CancellationToken cancellationToken = default) => NothingInTheWay;

    /// <summary>
    /// The pull's check (9.10), once <paramref name="arguments"/> (the fast-forward, the merge or
    /// the rebase, <paramref name="rebased"/>) are chosen and before anything is written: with
    /// <see cref="PullIntegrationOptions.KeepIgnored"/> a fast-forward or merge gets
    /// <c>--no-overwrite-ignore</c> before <paramref name="head"/> and the merge's check, a rebase the
    /// rebase's, an unborn branch (<paramref name="before"/> null) its tree's paths. Returns the
    /// arguments to run and the ignored files in the way (none: integrate). Work package WP1 gives
    /// it its body; until then it returns <paramref name="arguments"/> as they are and nothing in the way.
    /// </summary>
    private Task<(string[] Arguments, IReadOnlyList<string> Ignored)> KeepIgnoredInPullAsync(
        GitRef branch,
        string head,
        string? before,
        string[] arguments,
        bool rebased,
        PullIntegrationOptions? pullOptions,
        CancellationToken cancellationToken) =>
        Task.FromResult<(string[], IReadOnlyList<string>)>((arguments, []));
}
