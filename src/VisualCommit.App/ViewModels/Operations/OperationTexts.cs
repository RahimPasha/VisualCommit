using System.Globalization;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The four texts every operation has (docs/phase-3-design.md 2.33, D113).
/// </summary>
/// <param name="Title">
/// The operation's title, naming its action and its object ("Push main", "Fetch origin"): the
/// status bar's "&lt;Title&gt; failed" and "&lt;Title&gt; cancelled", the log line and the
/// finishing dialog. It is <see cref="Core.Operations.GitOperation.Title"/>.
/// </param>
/// <param name="RunningText">What the status bar shows while it runs ("Pushing main to origin/main…").</param>
/// <param name="ErrorTitle">The error dialog's title for a failure without a dialog of its own ("Push failed"); empty where failures are shown elsewhere (the Remotes form's <c>RemoteError</c>).</param>
/// <param name="FailureSummary">The error dialog's summary for such a failure ("Git could not push main to origin/main."); empty where failures are shown elsewhere.</param>
public sealed record OperationText(string Title, string RunningText, string ErrorTitle, string FailureSummary);

/// <summary>
/// Every text of section 2 of docs/phase-3-design.md that an operation shows, as code: 2.33's
/// table (title, running text, error dialog title, summary of other failures) for every
/// operation, its success and conflict messages, its special failure dialogs and its
/// confirmation. Names are short names (<c>feature/alpha</c>, <c>origin/main</c>, <c>v1.0</c>);
/// a commit is its short SHA, or <c>a1b2c3d "Add core"</c> where a dialog names it. Every count
/// uses the singular for one ("1 file has conflicts", "Cherry-picked 1 of 2 commits", D113).
/// Menu labels and disabled reasons are the menus' own (<c>RepositoryMenus</c>). A WP7
/// sub-package that needs a text not here adds it in a part of its own
/// (<c>OperationTexts.&lt;Package&gt;.cs</c>), so that the packages never edit one file.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>The detail of every confirmation that saves a backup first (design 2).</summary>
    public const string BackupDetail = DialogText.BackupDetail;

    /// <summary>The error dialog's extra button after a checkout blocked by local changes (2.1, 2.31, D80, D95).</summary>
    public const string StashAndCheckOutButton = "Stash changes and check out";

    /// <summary>The error dialog's extra button after a rejected push of the current branch to its upstream (2.19, D95).</summary>
    public const string PullButton = "Pull";

    /// <summary>The error dialog's extra button after a cancelled network operation left lock files (1.8, D95).</summary>
    public const string RemoveLockFilesButton = "Remove lock files";

    /// <summary>The title of a dialog that could not remove a lock file (4f).</summary>
    public const string RemoveLockFilesFailedTitle = "Removing lock files failed";

    /// <summary>Its summary; the output is the system's message.</summary>
    public const string RemoveLockFilesFailedSummary = "A lock file could not be removed.";

    /// <summary>The summary of the "&lt;Title&gt; cancelled" dialog that lists the lock files a stopped git left (1.8); the files follow in the output.</summary>
    public const string StaleLockFilesSummary =
        "Git was stopped while it was updating references. These lock files were left behind and will make later git commands fail:";

    /// <summary>The title of the background fetch (2.17). It shows no running text and no dialog.</summary>
    public const string AutoFetchTitle = "Auto-fetch";

    public const string CheckoutBlockedTitle = "Checkout blocked";

    public const string CherryPickStoppedTitle = "Cherry-pick stopped";

    public const string PushRejectedTitle = "Push rejected";

    public const string ForcePushStoppedTitle = "Force push stopped";

    public const string DeletionStoppedTitle = "Deletion stopped";

    public const string ReappliedWithConflictsTitle = "Changes re-applied with conflicts";

    public const string NotReappliedTitle = "Changes not re-applied";

    public const string Stashed = "Stashed the changes";

    public const string NothingToStash = "There was nothing to stash";

    /// <summary>The summary for a stash that is no longer listed (2.25).</summary>
    public const string StashGone = "This stash no longer exists.";

    // Counts (D113).

    /// <summary>"1 file", "2 files": <paramref name="singular"/> for one, <paramref name="plural"/> (or the singular with "s") otherwise.</summary>
    public static string Count(int count, string singular, string? plural = null) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural ?? singular + "s")}");

    /// <summary>"1 file has conflicts", "3 files have conflicts".</summary>
    public static string FilesHaveConflicts(int count) =>
        count == 1 ? "1 file has conflicts" : string.Create(CultureInfo.InvariantCulture, $"{count} files have conflicts");

    /// <summary>"a", "a and b", "a, b and c": names in a sentence.</summary>
    public static string List(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
        };
    }

    /// <summary>The first 7 characters of a commit id.</summary>
    public static string Short(string sha) => DialogText.ShortSha(sha);

    // 2.1 to 2.3: checkouts.

    /// <summary>
    /// A checkout (2.1, 2.2, 2.3): <paramref name="name"/> is the local branch, the remote branch
    /// (<c>origin/feature/x</c>), the tag or the short SHA it checks out.
    /// </summary>
    public static OperationText CheckOut(string name) =>
        new($"Check out {name}", $"Checking out {name}…", "Checkout failed", $"Git could not check out {name}.");

    /// <summary>"Checked out feature/alpha" (2.1, 2.2).</summary>
    public static string CheckedOut(string branch) => $"Checked out {branch}";

    /// <summary>2.2, when the local branch of that name tracks the remote branch and is behind it.</summary>
    public static string CheckedOutBehind(string branch, int behind, string upstream) =>
        string.Create(CultureInfo.InvariantCulture, $"Checked out {branch}. It is {behind} behind {upstream}: pull to update it.");

    /// <summary>2.2, when the local branch of that name tracks something else (<paramref name="upstream"/>) or nothing (null): it is checked out as is.</summary>
    public static string CheckedOutTracking(string branch, string? upstream) =>
        upstream is null ? $"Checked out {branch}, which has no upstream" : $"Checked out {branch}, which tracks {upstream}";

    /// <summary>"Created feature/x tracking origin/feature/x and checked it out" (2.2).</summary>
    public static string CreatedTrackingAndCheckedOut(string branch, string remoteBranch) =>
        $"Created {branch} tracking {remoteBranch} and checked it out";

    /// <summary>"HEAD is now detached at a1b2c3d" (2.3).</summary>
    public static string DetachedAt(string sha) => $"HEAD is now detached at {Short(sha)}";

    /// <summary>"Checkout blocked" for tracked changes: the dialog offers <see cref="StashAndCheckOutButton"/> (2.1).</summary>
    public static string CheckoutBlockedByChanges(string target) =>
        $"Your changes to these files would be overwritten by checking out {target}.";

    /// <summary>"Checkout blocked" for untracked files in the way: no extra button (2.1, D91).</summary>
    public static string CheckoutBlockedByUntrackedFiles(string target) =>
        $"Untracked files are in the way: {target} has files with the same names. Move or delete them, then check out again.";

    /// <summary>The confirmation before any checkout while HEAD is detached on <paramref name="count"/> commits that nothing else reaches (2.1).</summary>
    public static ConfirmationRequest ConfirmLeaveDetachedCommits(int count) => new(
        "Leave these commits?",
        count == 1
            ? "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph."
            : string.Create(CultureInfo.InvariantCulture, $"HEAD is detached on {count} commits that are on no branch. After this checkout they no longer show in the graph."),
        BackupDetail,
        "Check out",
        IsDestructive: false);

    // 2.31: stash changes and check out.

    /// <summary>2.31's operation: one local operation of three steps.</summary>
    public static OperationText StashAndCheckOut(string target) =>
        new($"Check out {target} with your changes", $"Checking out {target} with your changes…", "Checkout failed", $"Git could not check out {target}.");

    /// <summary>The message of the stash 2.31 makes: <c>VisualCommit: before checking out &lt;target&gt;</c>.</summary>
    public static string StashBeforeCheckout(string target) => $"VisualCommit: before checking out {target}";

    /// <summary>2.31, clean.</summary>
    public static string CheckedOutWithChanges(string target) => $"Checked out {target} and re-applied your changes";

    /// <summary>2.31, the pop conflicted: the status bar's text (the dialog's first two sentences).</summary>
    public static string ReappliedWithConflictsStatus(string target, int conflictedFiles) =>
        $"Checked out {target}. Re-applying your changes stopped with conflicts in {Count(conflictedFiles, "file")}.";

    /// <summary>2.31, the pop conflicted: the summary of the dialog <see cref="ReappliedWithConflictsTitle"/>, which shows no git output.</summary>
    public static string ReappliedWithConflicts(string target, int conflictedFiles) =>
        $"{ReappliedWithConflictsStatus(target, conflictedFiles)} Your changes are kept in the stash \"{StashBeforeCheckout(target)}\".";

    /// <summary>2.31, the pop failed without conflicts: the summary of the dialog <see cref="NotReappliedTitle"/>, with git's output.</summary>
    public static string NotReapplied(string target) =>
        $"Checked out {target}, but your changes could not be re-applied. They are kept in the stash \"{StashBeforeCheckout(target)}\".";

    // 4g: check out and merge.

    /// <summary>
    /// 4g's "Check out &lt;B&gt; and merge &lt;A&gt; into it": the error texts are the checkout's;
    /// a failure of the second step uses <see cref="Merge"/>'s ("Merge failed").
    /// </summary>
    public static OperationText CheckOutAndMerge(string branch, string source) =>
        new($"Check out {branch} and merge {source}", $"Checking out {branch} and merging {source}…", "Checkout failed", $"Git could not check out {branch}.");

    // 2.4 to 2.7: branches.

    public static OperationText CreateBranch(string name) =>
        new($"Create branch {name}", $"Creating {name}…", "Creating the branch failed", $"Git could not create the branch {name}.");

    /// <summary>"Created hotfix" or "Created hotfix and checked it out" (2.4).</summary>
    public static string CreatedBranch(string name, bool checkedOut) =>
        checkedOut ? $"Created {name} and checked it out" : $"Created {name}";

    /// <summary>The name dialog's context for a commit (2.4): <c>From a1b2c3d "Add core"</c>.</summary>
    public static string FromCommit(string sha, string subject) => $"From {DialogText.Commit(sha, subject)}";

    /// <summary>The name dialog's context for a branch, a remote branch or a tag (2.4): <c>From origin/feature/x</c>.</summary>
    public static string From(string name) => $"From {name}";

    /// <summary>The tag dialog's context (4f): <c>At a1b2c3d "Add core"</c>.</summary>
    public static string AtCommit(string sha, string subject) => $"At {DialogText.Commit(sha, subject)}";

    public static OperationText RenameBranch(string branch, string newName) =>
        new($"Rename {branch}", $"Renaming {branch} to {newName}…", "Renaming the branch failed", $"Git could not rename {branch} to {newName}.");

    public static string RenamedBranch(string branch, string newName) => $"Renamed {branch} to {newName}";

    public static OperationText DeleteBranch(string branch) =>
        new($"Delete {branch}", $"Deleting {branch}…", "Deleting the branch failed", $"Git could not delete {branch}.");

    public static string DeletedBranch(string branch) => $"Deleted {branch}";

    /// <summary>
    /// 2.6's confirmation. <paramref name="lacking"/> names what lacks the branch's commits (the
    /// current branch, or "HEAD" while HEAD is detached) when <c>git merge-base --is-ancestor</c>
    /// said no; null when nothing would be lost.
    /// </summary>
    public static ConfirmationRequest ConfirmDeleteBranch(string branch, string? lacking) => new(
        "Delete branch?",
        $"Delete the branch {branch}?" + (lacking is null ? string.Empty : $" It has commits that {lacking} does not have."),
        BackupDetail,
        "Delete");

    /// <summary>2.7 and 2.10: <paramref name="target"/> is what it moves to (<c>origin/main</c>, or a drop's source).</summary>
    public static OperationText FastForward(string branch, string target) =>
        new($"Fast-forward {branch}", $"Fast-forwarding {branch} to {target}…", "Fast-forward failed", $"Git could not fast-forward {branch} to {target}.");

    public static string FastForwarded(string branch, string target) => $"Fast-forwarded {branch} to {target}";

    /// <summary>2.7's summary when git refused because the branch is checked out in a worktree.</summary>
    public static string FastForwardInOtherWorktree(string branch) => $"{branch} is checked out in another worktree.";

    /// <summary>2.7's summary when git refused a non-fast-forward (the branch moved meanwhile).</summary>
    public static string FastForwardNotBehind(string branch, string target) => $"{branch} is not behind {target} any more.";

    // 2.8 to 2.10: merges.

    /// <summary><paramref name="current"/> is the current branch's name, or "HEAD" while HEAD is detached.</summary>
    public static OperationText Merge(string source, string current) =>
        new($"Merge {source}", $"Merging {source} into {current}…", "Merge failed", $"Git could not merge {source} into {current}.");

    public static string MergeUpToDate(string current, string source) => $"{current} is already up to date with {source}";

    public static string MergeFastForwarded(string current, string source) => $"Fast-forwarded {current} to {source}";

    public static string Merged(string source, string current) => $"Merged {source} into {current}";

    /// <summary>"Merge stopped: 1 file has conflicts" (warning colour, no dialog: the banner takes over).</summary>
    public static string MergeStopped(int conflictedFiles) => $"Merge stopped: {FilesHaveConflicts(conflictedFiles)}";

    // 2.11: reset.

    /// <summary><paramref name="current"/> is the current branch's name, or "HEAD" while HEAD is detached.</summary>
    public static OperationText Reset(string current, string sha) =>
        new($"Reset {current}", $"Resetting {current} to {Short(sha)}…", "Reset failed", $"Git could not reset {current} to {Short(sha)}.");

    /// <summary>"Reset main to a1b2c3d (hard)".</summary>
    public static string ResetDone(string current, string sha, ResetMode mode) =>
        $"Reset {current} to {Short(sha)} ({mode.ToString().ToLowerInvariant()})";

    // 2.12, 2.13: cherry-pick.

    public static OperationText CherryPick(string sha) =>
        new($"Cherry-pick {Short(sha)}", $"Cherry-picking {Short(sha)}…", "Cherry-pick failed", $"Git could not cherry-pick {Short(sha)}.");

    /// <summary>"Cherry-picked a1b2c3d onto main".</summary>
    public static string CherryPicked(string sha, string current) => $"Cherry-picked {Short(sha)} onto {current}";

    public static string CherryPickConflicts(int conflictedFiles) => $"Cherry-pick stopped: {FilesHaveConflicts(conflictedFiles)}";

    /// <summary>The summary of <see cref="CherryPickStoppedTitle"/> for a pick that changes nothing (2.12).</summary>
    public static string CherryPickEmpty(string sha, string current) =>
        $"{Short(sha)} changes nothing on {current}: its changes are already there. Abort the cherry-pick to go on.";

    /// <summary>2.13's operation: the summary of its failures is one of the sentences below.</summary>
    public static OperationText CherryPickSeveral(int count) => new(
        $"Cherry-pick {Count(count, "commit")}",
        $"Cherry-picking {Count(count, "commit")}…",
        "Cherry-pick failed",
        $"Git could not cherry-pick {Count(count, "commit")}.");

    public static string CherryPickedSeveral(int count, string current) => $"Cherry-picked {Count(count, "commit")} onto {current}";

    /// <summary>
    /// 2.13's message when a pick stopped with conflicts: "Cherry-picked 1 of 3 commits. a1b2c3d
    /// stopped with conflicts in 1 file; not picked: b2c3d4e "Alpha two"." The list is left out
    /// when the stopped commit was the last one.
    /// </summary>
    /// <param name="done">The commits picked before the stop.</param>
    /// <param name="count">All the commits of the operation.</param>
    /// <param name="stopped">The commit that stopped.</param>
    /// <param name="conflictedFiles">Its conflicted files.</param>
    /// <param name="notPicked">The commits after it, as (SHA, subject), in the order they would have been picked.</param>
    public static string CherryPickSeveralStopped(int done, int count, string stopped, int conflictedFiles, IReadOnlyList<(string Sha, string Subject)> notPicked) =>
        SeveralStopped("Cherry-picked", "picked", done, count, stopped, conflictedFiles, notPicked);

    /// <summary>2.13's summary when a later pick failed otherwise ("Cherry-pick failed").</summary>
    public static string CherryPickSeveralFailed(int done, int count, string failed) =>
        $"{Progress("Cherry-picked", done, count)} Git could not cherry-pick {Short(failed)}.";

    /// <summary>2.13's summary when a later pick changed nothing ("Cherry-pick stopped"): <see cref="CherryPickEmpty"/> after the count.</summary>
    public static string CherryPickSeveralEmpty(int done, int count, string empty, string current) =>
        $"{Progress("Cherry-picked", done, count)} {CherryPickEmpty(empty, current)}";

    // 2.14: revert.

    public static OperationText Revert(string sha) =>
        new($"Revert {Short(sha)}", $"Reverting {Short(sha)}…", "Revert failed", $"Git could not revert {Short(sha)}.");

    public static OperationText RevertSeveral(int count) => new(
        $"Revert {Count(count, "commit")}",
        $"Reverting {Count(count, "commit")}…",
        "Revert failed",
        $"Git could not revert {Count(count, "commit")}.");

    public static string Reverted(string sha) => $"Reverted {Short(sha)}";

    public static string RevertedSeveral(int count) => $"Reverted {Count(count, "commit")}";

    public static string RevertConflicts(int conflictedFiles) => $"Revert stopped: {FilesHaveConflicts(conflictedFiles)}";

    /// <summary>The summary of "Revert failed" for a revert that changes nothing (D117), with git's output beneath.</summary>
    public static string RevertEmpty(string sha, string current) => $"{Short(sha)} changes nothing on {current}: it is already undone.";

    /// <summary>2.14's message when one of several reverts stopped with conflicts, as <see cref="CherryPickSeveralStopped"/> with "Reverted" and "not reverted".</summary>
    public static string RevertSeveralStopped(int done, int count, string stopped, int conflictedFiles, IReadOnlyList<(string Sha, string Subject)> notReverted) =>
        SeveralStopped("Reverted", "reverted", done, count, stopped, conflictedFiles, notReverted);

    public static string RevertSeveralFailed(int done, int count, string failed) =>
        $"{Progress("Reverted", done, count)} Git could not revert {Short(failed)}.";

    // 2.15: abort.

    /// <summary>How 2.15's texts name what is in progress: "merge", "cherry-pick", "revert", "rebase", "git am".</summary>
    public static string InProgressName(InProgressKind kind) => ToolbarViewModel.InProgressName(kind);

    public static OperationText Abort(InProgressKind kind) => kind == InProgressKind.ApplyMailbox
        ? new("Abort git am", "Aborting git am…", "Abort failed", "Git could not abort git am.")
        : new($"Abort the {InProgressName(kind)}", $"Aborting the {InProgressName(kind)}…", "Abort failed", $"Git could not abort the {InProgressName(kind)}.");

    public static string Aborted(InProgressKind kind) =>
        kind == InProgressKind.ApplyMailbox ? "Aborted git am" : $"Aborted the {InProgressName(kind)}";

    // The Abort question of 2.15 (title, section 5's question, the backup's detail, "Abort <kind>")
    // is the banner's, ConflictBannerViewModel.AbortConfirmation: the banner and AbortAsync ask it.

    // 2.16 to 2.18: fetch and pull.

    /// <summary>The toolbar's Fetch and "Fetch all remotes" (2.16).</summary>
    public static OperationText FetchAll { get; } =
        new("Fetch", "Fetching from all remotes…", "Fetch failed", "Git could not fetch from all remotes.");

    public static OperationText Fetch(string remote) =>
        new($"Fetch {remote}", $"Fetching from {remote}…", "Fetch failed", $"Git could not fetch from {remote}.");

    /// <summary>"Git could not fetch from origin and mirror.": every remote that failed (2.16).</summary>
    public static string FetchFailed(IReadOnlyList<string> remotes) => $"Git could not fetch from {List(remotes)}.";

    public const string FetchedAll = "Fetched from all remotes";

    public static string Fetched(string remote) => $"Fetched from {remote}";

    /// <summary>The error dialog's extra button after a rejected push of another branch (2.19, D95).</summary>
    public static string FetchButton(string remote) => $"Fetch {remote}";

    /// <summary>2.18: <paramref name="upstream"/> as the menus name it (<c>origin/main</c>, or a local upstream's branch alone, D114).</summary>
    public static OperationText Pull(string upstream, string branch) =>
        new("Pull", $"Pulling {upstream} into {branch}…", "Pull failed", $"Git could not pull {upstream} into {branch}.");

    public static string PullUpToDate(string branch) => $"{branch} is already up to date";

    /// <summary>"Pulled 1 commit into main (fast-forward)".</summary>
    public static string PulledFastForward(int commits, string branch) => $"Pulled {Count(commits, "commit")} into {branch} (fast-forward)";

    public static string PulledMerged(string upstream, string branch) => $"Pulled and merged {upstream} into {branch}";

    public static string PulledRebased(string branch, string upstream) => $"Pulled and rebased {branch} onto {upstream}";

    public static string PullConflicts(int conflictedFiles) => $"Pull stopped: {FilesHaveConflicts(conflictedFiles)}";

    /// <summary>
    /// 2.18: the fetch found the upstream no more on its remote (deleted there), so nothing was
    /// integrated; <paramref name="remoteBranch"/> is its name there: "feature no longer exists on origin".
    /// </summary>
    public static string PullUpstreamGone(string remoteBranch, string remote) => $"{remoteBranch} no longer exists on {remote}";

    /// <summary>"pull.rebase is set to preserve, which this app does not understand." (2.18).</summary>
    public static string PullSettingNotUnderstood(string setting, string value) =>
        $"{setting} is set to {value}, which this app does not understand.";

    // 2.19 to 2.22: pushes and upstreams.

    /// <summary>2.19 and a drop's push: <paramref name="target"/> is <c>&lt;r&gt;/&lt;u&gt;</c>.</summary>
    public static OperationText Push(string branch, string target) =>
        new($"Push {branch}", $"Pushing {branch} to {target}…", "Push failed", $"Git could not push {branch} to {target}.");

    public static string Pushed(string branch, string target, bool setUpstream) =>
        $"Pushed {branch} to {target}" + (setUpstream ? ", and set it as upstream" : string.Empty);

    /// <summary>The summary of <see cref="PushRejectedTitle"/> (2.19).</summary>
    public static string PushRejected(string remote, string branch) =>
        $"{remote} has commits that your {branch} does not have. Pull first, then push again.";

    public static OperationText ForcePush(string branch, string target) =>
        new($"Force push {branch}", $"Force-pushing {branch} to {target}…", "Force push failed", $"Git could not force-push {branch} to {target}.");

    public static string ForcePushed(string branch, string target) => $"Force-pushed {branch} to {target}";

    /// <summary>The summary of <see cref="ForcePushStoppedTitle"/> (2.20).</summary>
    public static string ForcePushStopped(string target, string branch) =>
        $"{target} has commits that your {branch} does not include. Pull or look at them before forcing.";

    /// <summary>
    /// 2.20's question: <paramref name="remoteBranch"/> is the upstream's name on
    /// <paramref name="remote"/>, the branch the push replaces, which can differ from
    /// <paramref name="branch"/> ("Replace main on origin with your fix?").
    /// </summary>
    public static ConfirmationRequest ConfirmForcePush(string branch, string remote, string remoteBranch) => new(
        $"Force push {branch}?",
        $"Replace {remoteBranch} on {remote} with your {branch}? Commits on {remote} that your branch does not have are dropped there. "
            + $"The push stops if {remote} has commits you have not integrated into your {branch}, even ones the last fetch brought.",
        BackupDetail,
        "Force push");

    public static OperationText SetUpstream(string branch) =>
        new($"Set upstream of {branch}", $"Setting the upstream of {branch}…", "Setting the upstream failed", $"Git could not set the upstream of {branch}.");

    /// <summary>The pick dialog's title for 2.21.</summary>
    public static string SetUpstreamDialogTitle(string branch) => $"Set upstream for {branch}";

    public const string SetUpstreamButton = "Set upstream";

    public static string UpstreamSet(string branch, string upstream) => $"{branch} now tracks {upstream}";

    public static OperationText UnsetUpstream(string branch, string upstream) =>
        new($"Stop tracking {upstream}", $"Removing the upstream of {branch}…", "Setting the upstream failed", $"Git could not remove the upstream of {branch}.");

    public static string UpstreamUnset(string branch, string upstream) => $"{branch} no longer tracks {upstream}";

    public static OperationText DeleteRemoteBranch(string branch, string remote) =>
        new($"Delete {branch} on {remote}", $"Deleting {branch} on {remote}…", "Deleting the remote branch failed", $"Git could not delete {branch} on {remote}.");

    public static string DeletedRemoteBranch(string branch, string remote) => $"Deleted {branch} on {remote}";

    /// <summary>The summary of <see cref="DeletionStoppedTitle"/> for a remote branch (2.22).</summary>
    public static string RemoteBranchChanged(string branch, string remote) =>
        $"{branch} on {remote} has changed since you last fetched. Fetch and look at it before deleting.";

    public static ConfirmationRequest ConfirmDeleteRemoteBranch(string branch, string remote) => new(
        $"Delete {branch} on {remote}?",
        $"Delete the branch {branch} on {remote}? Everyone who uses {remote} loses it.",
        BackupDetail,
        $"Delete on {remote}");

    // 2.23: remotes. Add, rename and change URL show git's message in the form, not a dialog.

    public static OperationText AddRemote(string name) => new($"Add remote {name}", $"Adding remote {name}…", string.Empty, string.Empty);

    public static string AddedRemote(string name) => $"Added remote {name}";

    public static OperationText RenameRemote(string name) => new($"Rename remote {name}", $"Renaming remote {name}…", string.Empty, string.Empty);

    public static string RenamedRemote(string name, string newName) => $"Renamed remote {name} to {newName}";

    public static OperationText SetRemoteUrl(string name) => new($"Change URL of {name}", $"Changing the URL of {name}…", string.Empty, string.Empty);

    public static string ChangedRemoteUrl(string name) => $"Changed the URL of {name}";

    public static OperationText RemoveRemote(string name) =>
        new($"Remove remote {name}", $"Removing remote {name}…", "Removing the remote failed", $"Git could not remove the remote {name}.");

    public static string RemovedRemote(string name) => $"Removed remote {name}";

    /// <summary>2.23's confirmation; <paramref name="shownUrl"/> is the URL with its credentials hidden (D103).</summary>
    public static ConfirmationRequest ConfirmRemoveRemote(string name, int remoteBranches, string shownUrl) => new(
        $"Remove remote {name}?",
        $"Remove the remote {name} and its {Count(remoteBranches, "remote branch", "remote branches")}? "
            + $"Your local branches stay; those that track it lose their upstream. Its URL was {shownUrl}.",
        BackupDetail,
        "Remove");

    // 2.24 to 2.26: stashes. A stash is named stash@{n}.

    public static OperationText Stash { get; } = new("Stash", "Stashing the changes…", "Stash failed", "Git could not stash the changes.");

    public static OperationText ApplyStash(string stash) =>
        new($"Apply {stash}", $"Applying {stash}…", "Applying the stash failed", $"Git could not apply {stash}.");

    public static OperationText PopStash(string stash) =>
        new($"Pop {stash}", $"Popping {stash}…", "Applying the stash failed", $"Git could not apply {stash}.");

    public static string AppliedStash(string stash) => $"Applied {stash}";

    public static string PoppedStash(string stash) => $"Popped {stash}";

    /// <summary>"Applying the stash stopped: 1 file has conflicts", and for a pop " The stash is kept." (2.25).</summary>
    public static string ApplyStashConflicts(int conflictedFiles, bool pop) =>
        $"Applying the stash stopped: {FilesHaveConflicts(conflictedFiles)}" + (pop ? ". The stash is kept." : string.Empty);

    /// <summary>The summary of "Applying the stash failed" for git's <c>would be overwritten by merge</c> (2.25).</summary>
    public static string StashWouldOverwrite(string stash) => $"Your changes to these files would be overwritten by applying {stash}.";

    public static OperationText DropStash(string stash) =>
        new($"Drop {stash}", $"Dropping {stash}…", "Dropping the stash failed", $"Git could not drop {stash}.");

    public static string DroppedStash(string stash) => $"Dropped {stash}";

    public static ConfirmationRequest ConfirmDropStash(string stash, string message) =>
        new("Drop stash?", $"Drop {stash} \"{message}\"?", BackupDetail, "Drop");

    // 2.27 to 2.29: tags.

    public static OperationText CreateTag(string tag) =>
        new($"Create tag {tag}", $"Creating tag {tag}…", "Creating the tag failed", $"Git could not create the tag {tag}.");

    public static string CreatedTag(string tag) => $"Created tag {tag}";

    public static OperationText DeleteTag(string tag) =>
        new($"Delete tag {tag}", $"Deleting tag {tag}…", "Deleting the tag failed", $"Git could not delete the tag {tag}.");

    public static string DeletedTag(string tag) => $"Deleted tag {tag}";

    public static ConfirmationRequest ConfirmDeleteTag(string tag) =>
        new("Delete tag?", $"Delete the tag {tag} here? It stays on remotes it was pushed to.", BackupDetail, "Delete");

    public static OperationText PushTag(string tag, string remote) =>
        new($"Push tag {tag}", $"Pushing tag {tag} to {remote}…", "Pushing the tag failed", $"Git could not push {tag} to {remote}.");

    public static string PushedTag(string tag, string remote) => $"Pushed tag {tag} to {remote}";

    /// <summary>The remote choice dialog's title for pushing a tag (4f).</summary>
    public static string PushTagChoiceTitle(string tag) => $"Push {tag} to…";

    public static OperationText DeleteRemoteTag(string tag, string remote) =>
        new($"Delete tag {tag} on {remote}", $"Deleting tag {tag} on {remote}…", "Deleting the remote tag failed", $"Git could not delete the tag {tag} on {remote}.");

    public static string DeletedRemoteTag(string tag, string remote) => $"Deleted tag {tag} on {remote}";

    /// <summary>The summary of <see cref="DeletionStoppedTitle"/> for a tag (2.29).</summary>
    public static string RemoteTagDiffers(string remote, string tag) => $"{remote} has no tag {tag}, or its {tag} is not the same as yours.";

    /// <summary>The remote choice dialog's title for deleting a tag on a remote (4f).</summary>
    public static string DeleteRemoteTagChoiceTitle(string tag) => $"Delete {tag} on…";

    public static ConfirmationRequest ConfirmDeleteRemoteTag(string tag, string remote) => new(
        $"Delete {tag} on {remote}?",
        $"Delete the tag {tag} on {remote}? Everyone who uses {remote} loses it.",
        BackupDetail,
        $"Delete on {remote}");

    // 2.30: ignore.

    /// <summary>"Ignore run.log" (2.30): also the operation's title.</summary>
    public static string IgnoreFileLabel(string fileName) => $"Ignore {fileName}";

    /// <summary>"Ignore all *.log files" (2.30): also the operation's title.</summary>
    public static string IgnoreExtensionLabel(string extension) => $"Ignore all *.{extension} files";

    /// <summary>2.30: <paramref name="label"/> is the item's label, <paramref name="line"/> the line as written to .gitignore.</summary>
    public static OperationText Ignore(string label, string line) =>
        new(label, $"Adding {line} to .gitignore…", "Ignoring the file failed", "The file .gitignore could not be written.");

    public static string Ignored(string line) => $"Added {line} to .gitignore";

    /// <summary>1.8: the title of the dialog that lists the lock files a cancelled operation left.</summary>
    public static string Cancelled(string title) => $"{title} cancelled";

    private static string Progress(string verb, int done, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{verb} {done} of {Count(count, "commit")}.");

    private static string SeveralStopped(
        string verb, string notVerb, int done, int count, string stopped, int conflictedFiles, IReadOnlyList<(string Sha, string Subject)> rest)
    {
        ArgumentNullException.ThrowIfNull(rest);
        var text = $"{Progress(verb, done, count)} {Short(stopped)} stopped with conflicts in {Count(conflictedFiles, "file")}";
        if (rest.Count > 0)
        {
            text += $"; not {notVerb}: " + string.Join(", ", rest.Select(commit => DialogText.Commit(commit.Sha, commit.Subject)));
        }

        return text + ".";
    }
}
