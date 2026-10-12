using System.Globalization;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of phase 4's rebases, lists of picks and reverts, Continue and Skip
/// (docs/phase-4-design.md 10.1, 10.2, 7.2): the operations' four texts, their special failure
/// summaries, their results in the status bar and the Skip question. <c>{b}</c> is the branch
/// being rebased, <c>{target}</c> a branch's short name, <c>&lt;r&gt;/&lt;b&gt;</c> or a commit's
/// short SHA. Counts use the singular for one (D113). Built by work package WP4.
/// </summary>
public static partial class OperationTexts
{
    // 10.1: the operations.

    /// <summary>"Rebase {b} onto {target}" (1.3).</summary>
    public static OperationText RebaseOnto(string branch, string target) =>
        new($"Rebase {branch} onto {target}", $"Rebasing {branch} onto {target}…", RebaseFailedTitle, $"Rebase {branch} onto {target} failed.");

    /// <summary>
    /// "Check out {A} and rebase it onto {B}" (1.3): one operation of several steps. Its running
    /// text is the first step's, <c>Checking out {A}…</c>; after the checkout the operation shows
    /// <see cref="RebaseOnto"/>'s, <c>Rebasing {A} onto {B}…</c>
    /// (<see cref="VisualCommit.Core.Operations.OperationContext.SetRunningText"/>). A failure names the step
    /// that failed (<see cref="CheckOut"/>'s or <see cref="RebaseOnto"/>'s texts).
    /// </summary>
    public static OperationText CheckOutAndRebase(string branch, string target) =>
        new($"Check out {branch} and rebase it onto {target}", $"Checking out {branch}…", RebaseFailedTitle, $"Rebase {branch} onto {target} failed.");

    /// <summary>"Interactive rebase of {b}" (2.4).</summary>
    public static OperationText InteractiveRebase(string branch) =>
        new($"Interactive rebase of {branch}", $"Rebasing {branch}…", "Interactive rebase failed", $"Interactive rebase of {branch} failed.");

    /// <summary>"Interactive rebase of {b}" that checks the branch out first (2.4): its running text is the checkout's, then <see cref="InteractiveRebase"/>'s.</summary>
    public static OperationText InteractiveRebaseWithCheckout(string branch) =>
        InteractiveRebase(branch) with { RunningText = $"Checking out {branch}…" };

    /// <summary>"Squash {N} commits" (section 3).</summary>
    public static OperationText Squash(int count) =>
        new($"Squash {Count(count, "commit")}", $"Squashing {Count(count, "commit")}…", "Squash failed", $"Squash {Count(count, "commit")} failed.");

    /// <summary>"Pull (rebase)" (section 6): phase 3's pull texts with its own title.</summary>
    public static OperationText PullRebase(string upstream, string branch) =>
        Pull(upstream, branch) with { Title = PullRebaseTitle };

    /// <summary>The title of a pull whose integration is forced to a rebase (section 6, D147).</summary>
    public const string PullRebaseTitle = "Pull (rebase)";

    /// <summary>"Continue the {kind}" (5.4).</summary>
    public static OperationText Continue(InProgressKind kind) =>
        new($"Continue the {InProgressName(kind)}", $"Continuing the {InProgressName(kind)}…", "Continue failed", $"Git could not continue the {InProgressName(kind)}.");

    /// <summary>"Skip {short}" (5.4).</summary>
    public static OperationText Skip(string sha) =>
        new($"Skip {Short(sha)}", $"Skipping {Short(sha)}…", "Skip failed", $"Git could not skip {Short(sha)}.");

    /// <summary>The error dialog's title of a rebase that failed (10.1).</summary>
    public const string RebaseFailedTitle = "Rebase failed";

    /// <summary>The title of the error dialog a rebase stop with nothing to look at opens (5.1, amending D95).</summary>
    public const string RebaseStoppedTitle = "Rebase stopped";

    /// <summary>The title of the error dialog a revert list stopped on a commit git could not apply opens (5.1).</summary>
    public const string RevertStoppedTitle = "Revert stopped";

    // 10.1: special summaries.

    /// <summary>A rebase git refused for local changes, and the app's own check of "Check out {A} and rebase it" (1.3, 14.3).</summary>
    public const string RebaseNeedsCleanTree = "Git rebases only a clean working tree: commit or stash your changes first.";

    /// <summary>A rebase git refused because untracked files are in the way.</summary>
    public const string RebaseUntrackedInTheWay = "Untracked files are in the way: move or remove them first.";

    /// <summary>A list of picks (reverts) git refused at its start (section 4, D145): nothing was applied, the user's changes are kept.</summary>
    public static string ListRefusedAtStart(InProgressKind kind) =>
        $"Git would overwrite your changes: commit or stash them first. Nothing was {(kind == InProgressKind.Revert ? "reverted" : "cherry-picked")}.";

    /// <summary>The last sentence of a rebase refused because HEAD moved while it waited (D124).</summary>
    public const string NothingRebased = "Nothing was rebased.";

    /// <summary>"{b} changed while the list was open: open it again. Nothing was rebased." (2.4, D124).</summary>
    public static string ChangedWhileListOpen(string branch) => $"{branch} changed while the list was open: open it again. {NothingRebased}";

    /// <summary>The git layer refused a plan whose branch moved after the app's own check (D124): Start's reason "HEAD moved: open the list again" as a sentence.</summary>
    public const string HeadMovedSinceList = "HEAD moved while the list was open: open the list again. " + NothingRebased;

    /// <summary>The last sentence of a Continue refused because the state changed while it waited (5.4).</summary>
    public const string NothingContinued = "Nothing was continued.";

    /// <summary>
    /// Continue's disabled reason at a rebase stop while a tracked file that is not conflicted has
    /// unstaged changes (submodules left out): git's <c>--continue</c> refuses then (the review's
    /// finding 1). With a period, the summary of "Continue failed" when the git layer refuses.
    /// </summary>
    public const string ContinueNeedsNothingUnstaged = "Stage or stash the other changed files first: git continues a rebase only when nothing else is unstaged";

    /// <summary>The last sentence of a Skip refused because the state changed while it waited (5.4).</summary>
    public const string NothingSkipped = "Nothing was skipped.";

    /// <summary>
    /// Continue's or Skip's refusal when what is in progress changed since the click (5.4: another
    /// kind, another stopped commit, nothing any more): "The rebase changed while this waited:
    /// look at it again. Nothing was continued."
    /// </summary>
    public static string StateChangedWhileWaiting(InProgressKind kind, string nothing) =>
        $"The {InProgressName(kind)} changed while this waited: look at it again. {nothing}";

    // 2.1: what the interactive rebase's list says when it cannot be made.

    /// <summary>
    /// The list's own message (2.1), shown in the list in place of its rows with only Cancel, or
    /// null when <paramref name="list"/> can be shown: merge commits in the range ("A merge lies
    /// between {target} and {b}: interactive rebase would flatten it."), no commit of the branch's
    /// own after the target ("{b} has no commits of its own after {target}."), more than
    /// <see cref="InteractiveListLimit"/> commits ("{N} commits are too many for one interactive
    /// rebase.").
    /// </summary>
    public static string? InteractiveListMessage(RebaseList list, string branch, string target)
    {
        ArgumentNullException.ThrowIfNull(list);
        return list.MergeCount > 0 ? $"A merge lies between {target} and {branch}: interactive rebase would flatten it."
            : list.CommitsOldestFirst.Count == 0 ? $"{branch} has no commits of its own after {target}."
            : list.CommitsOldestFirst.Count > InteractiveListLimit ? $"{Count(list.CommitsOldestFirst.Count, "commit")} are too many for one interactive rebase."
            : null;
    }

    /// <summary>The most commits one interactive rebase's list holds (2.1).</summary>
    public const int InteractiveListLimit = 1000;

    // 10.2: results in the status bar.

    /// <summary>"Rebased {b} onto {target}".</summary>
    public static string Rebased(string branch, string target) => $"Rebased {branch} onto {target}";

    /// <summary>"{b} is already up to date with {target}".</summary>
    public static string RebaseUpToDate(string branch, string target) => $"{branch} is already up to date with {target}";

    /// <summary>Rebased, but the changes <c>rebase.autoStash</c> put back conflicted (1.4; warning).</summary>
    public static string RebasedAutostashConflicted(string branch, string target) => WithAutostashConflicted(Rebased(branch, target));

    /// <summary>
    /// 1.4's warning for any rebase that ended rebased while the changes <c>rebase.autoStash</c>
    /// put back conflicted (the review's findings 12 and 19): the operation's own result followed
    /// by "; your changes conflicted when put back: they are also kept in the stash" ("Rebased
    /// {b}: {N} commits; …", "Squashed {N} commits into {short}; …", "Pulled and rebased {b} onto
    /// {up}; …", Continue's and Skip's "Rebased {b} onto {onto}; …").
    /// </summary>
    public static string WithAutostashConflicted(string result) => $"{result}; {AutostashConflictedClause}";

    /// <summary>1.4's clause for changes <c>rebase.autoStash</c> put back with conflicts.</summary>
    private const string AutostashConflictedClause = "your changes conflicted when put back: they are also kept in the stash";

    /// <summary>"Rebased {b}: {N} commits", the commits on the branch after its base (2.4).</summary>
    public static string InteractiveRebased(string branch, int commits) => $"Rebased {branch}: {Count(commits, "commit")}";

    /// <summary>"Squashed {N} commits into {short}" (section 3).</summary>
    public static string Squashed(int count, string sha) => $"Squashed {Count(count, "commit")} into {Short(sha)}";

    /// <summary>"Rebase stopped: {N} files have conflicts" (1.4; warning).</summary>
    public static string RebaseStopped(int conflictedFiles) => $"Rebase stopped: {FilesHaveConflicts(conflictedFiles)}";

    /// <summary>"Rebase stopped at {short} "{subject}"", a stop with no conflicted file (1.4; warning).</summary>
    public static string RebaseStoppedAt(string sha, string? subject) =>
        subject is null ? $"Rebase stopped at {Short(sha)}" : $"Rebase stopped at {Short(sha)} \"{subject}\"";

    /// <summary>"Cherry-picked {K} of {N} commits; stopped at {short} with conflicts in {M} files" (section 4; warning); "Reverted …" likewise.</summary>
    public static string ListStoppedWithConflicts(InProgressKind kind, int applied, int total, string sha, int conflictedFiles) =>
        $"{ListProgress(kind, applied, total)}; stopped at {Short(sha)} with conflicts in {Count(conflictedFiles, "file")}";

    /// <summary>"Cherry-picked {K} of {N} commits; {short} changes nothing" (section 4; warning).</summary>
    public static string ListStoppedEmpty(InProgressKind kind, int applied, int total, string sha) =>
        $"{ListProgress(kind, applied, total)}; {Short(sha)} changes nothing";

    /// <summary>"Cherry-picked {K} of {N} commits; git could not pick {short}" (section 4; warning).</summary>
    public static string ListStoppedRefused(InProgressKind kind, int applied, int total, string sha) =>
        $"{ListProgress(kind, applied, total)}; git could not {(kind == InProgressKind.Revert ? "revert" : "pick")} {Short(sha)}";

    /// <summary>Continue's text when the operation is finished (10.2): "Merge committed", "Cherry-pick finished", "Revert finished", "Rebased {b} onto {onto}".</summary>
    public static string ContinueDone(InProgressKind kind, string? branch, string onto) => kind switch
    {
        InProgressKind.Merge => "Merge committed",
        InProgressKind.CherryPick => "Cherry-pick finished",
        InProgressKind.Revert => "Revert finished",
        _ => branch is null ? $"Rebased onto {onto}" : Rebased(branch, onto),
    };

    /// <summary>"Skipped {short}": only a single pick or revert skipped, with nothing after it (10.2).</summary>
    public static string Skipped(string sha) => $"Skipped {Short(sha)}";

    /// <summary>A pull's rebase from which git's fork point left out commits the upstream once had (section 6; warning).</summary>
    public static string PulledRebasedLeftOut(string branch, string upstream, int leftOut) =>
        $"Pulled and rebased {branch} onto {upstream}; git left out {Count(leftOut, "commit")} that {upstream} once had";

    // 7.2: Skip's question.

    /// <summary>
    /// The question Skip asks (7.2, D144): "Skip {short}?", "\"{subject}\" is left out of the
    /// {rebase, cherry-pick, revert}, and changes made to resolve its conflicts are lost.", the
    /// backup's detail, and the danger button "Skip".
    /// </summary>
    public static ConfirmationRequest ConfirmSkip(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var sha = state.Commit ?? string.Empty;
        var subject = state.Subject ?? Short(sha);
        return new ConfirmationRequest(
            $"Skip {Short(sha)}?",
            $"\"{subject}\" is left out of the {InProgressName(state.Kind)}, and changes made to resolve its conflicts are lost.",
            BackupDetail,
            "Skip");
    }

    private static string ListProgress(InProgressKind kind, int applied, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"{(kind == InProgressKind.Revert ? "Reverted" : "Cherry-picked")} {applied} of {Count(total, "commit")}");
}
