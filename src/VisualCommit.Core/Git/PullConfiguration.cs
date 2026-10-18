namespace VisualCommit.Core.Git;

/// <summary>What <c>pull.ff</c> asks of a pull's merge (docs/phase-6-design.md 8.9).</summary>
public enum PullFastForwardSetting
{
    /// <summary>Not set.</summary>
    Unset,

    /// <summary>True: <c>git merge --ff</c>.</summary>
    Always,

    /// <summary>False: <c>git merge --no-ff</c>.</summary>
    Never,

    /// <summary><c>only</c>: <c>git merge --ff-only</c>, which wins over a rebase setting.</summary>
    Only,
}

/// <summary>What a pull's rebase setting (<c>branch.&lt;b&gt;.rebase</c>, else <c>pull.rebase</c>) asks for (8.9).</summary>
public enum PullRebaseSetting
{
    /// <summary>No rebase: unset or false.</summary>
    None,

    /// <summary>True, <c>interactive</c> or <c>i</c>: a plain rebase (D102).</summary>
    Rebase,

    /// <summary><c>merges</c> or <c>m</c>: <c>git rebase --rebase-merges</c>.</summary>
    Merges,
}

/// <summary>
/// Git's pull settings for one branch as Pull reads them, before its fetch
/// (<see cref="IGitRepository.ReadPullConfigurationAsync"/>, docs/phase-6-design.md 8.9, amending
/// D84): the settings screen's "in effect" line reads the same.
/// </summary>
/// <param name="FastForward">What <c>pull.ff</c> asks.</param>
/// <param name="Rebase">What the rebase setting asks.</param>
/// <param name="RebaseKey">The key that decided <paramref name="Rebase"/>: <c>branch.&lt;b&gt;.rebase</c> when set, else <c>pull.rebase</c>.</param>
/// <param name="RebaseValue">That key's value as set, or null when unset.</param>
/// <param name="FastForwardValue"><c>pull.ff</c>'s value as set, or null when unset.</param>
/// <param name="Problem">
/// Pull's sentence for a value it does not understand (<c>pull.rebase is set to preserve, which
/// this app does not understand.</c>), or null: Pull then fails before anything is fetched.
/// </param>
/// <param name="ProblemKey">The key of <paramref name="Problem"/>'s value, or null.</param>
public sealed record PullConfiguration(
    PullFastForwardSetting FastForward,
    PullRebaseSetting Rebase,
    string RebaseKey,
    string? RebaseValue,
    string? FastForwardValue,
    string? Problem,
    string? ProblemKey)
{
    /// <summary>
    /// The failure Pull reports for <see cref="Problem"/>, as git's own refusal reads: the
    /// command <c>git config --get &lt;key&gt;</c>, exit 0, the sentence as its standard error.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no problem.</exception>
    public GitException NotUnderstood() =>
        Problem is { } problem && ProblemKey is { } key
            ? new GitException($"git config --get {key}", 0, problem)
            : throw new InvalidOperationException("The pull configuration has no problem to report.");
}

/// <summary>
/// What a pull's integration step is given beside its <see cref="NetworkOptions"/>
/// (<c>IntegratePullAsync</c>'s last optional parameter, docs/phase-6-design.md 8.9, 9.10).
/// </summary>
/// <param name="Configuration">The pull settings read before the fetch (8.9): the integration follows them; null: it reads them itself, as phase 4's does.</param>
/// <param name="KeepIgnored">Keep ignored files (9.10): the integration refuses when an ignored file is in its way. False is git's default.</param>
public sealed record PullIntegrationOptions(PullConfiguration? Configuration = null, bool KeepIgnored = false);
