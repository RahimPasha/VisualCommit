using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Every action the menus, the double-clicks and the drop of docs/phase-3-design.md section 4 can
/// start on one repository: the operations of section 2 and the file actions of 2.30 and 2.32.
/// <see cref="RepositoryOperations"/> implements it; the menu builders (<c>RepositoryMenus</c>)
/// take it, so that they are tested with a recording fake.
/// <para>
/// Objects are named as the menu captured them when it opened, never by row numbers (4b, 4h):
/// a full ref name (<c>refs/heads/feature/alpha</c>, <c>refs/remotes/origin/main</c>,
/// <c>refs/tags/v1.0</c>), a full commit SHA, a stash's commit SHA, a remote's name, or a
/// repository-relative path with <c>/</c>.
/// </para>
/// <para>
/// No method throws for the operation's own failure: each asks its confirmation and dialogs,
/// runs through the queue, and shows its result in the status bar and its failure in the error
/// dialog (D95). Each task completes when the operation has ended, or when a dialog was cancelled.
/// </para>
/// </summary>
public interface IRepositoryActions
{
    /// <summary>Where a menu logs an action that failed past the operation's own handling.</summary>
    IAppLog Log { get; }

    // Branches and history (RepositoryOperations.Branches.cs, sub-package WP7-branches).

    /// <summary>
    /// Checks out <paramref name="target"/> (2.1 to 2.3): a local branch (<c>refs/heads/…</c>),
    /// a remote branch (<c>refs/remotes/…</c>, into a tracking branch, 2.2), a tag
    /// (<c>refs/tags/…</c>, detached) or a commit SHA (detached). While HEAD is detached on
    /// commits nothing else reaches, it confirms and backs them up first.
    /// </summary>
    Task CheckoutAsync(string target);

    /// <summary>
    /// "Stash changes and check out" (2.31, D80, D91): stashes the tracked changes, checks out
    /// <paramref name="target"/> (as <see cref="CheckoutAsync"/> names it) and re-applies them.
    /// </summary>
    Task StashAndCheckoutAsync(string target);

    /// <summary>
    /// Opens the name dialog and creates a branch (2.4) at <paramref name="start"/>: a full ref
    /// name, a commit SHA, or null for HEAD (the toolbar's Branch, the "Local branches" header,
    /// the empty graph area, the working-changes row). "Check out the new branch" is ticked.
    /// </summary>
    Task CreateBranchAsync(string? start);

    /// <summary>Opens the name dialog with the old name and renames the local branch (2.5).</summary>
    Task RenameBranchAsync(string branchRef);

    /// <summary>Confirms, backs up and deletes the local branch (2.6).</summary>
    Task DeleteBranchAsync(string branchRef);

    /// <summary>
    /// Fast-forwards the local branch <paramref name="branchRef"/>, which is not checked out, to
    /// <paramref name="targetRef"/> (its upstream's full ref, or a drop's source) with
    /// <c>git fetch .</c> (2.7, 4g).
    /// </summary>
    Task FastForwardBranchAsync(string branchRef, string targetRef);

    /// <summary>
    /// Merges <paramref name="sourceRef"/> (a local or remote branch) into the current branch
    /// (2.8): <see cref="MergeMode.NoFastForward"/> always makes a merge commit (2.9),
    /// <see cref="MergeMode.FastForwardOnly"/> is the drop's fast-forward of the current branch (2.10).
    /// </summary>
    Task MergeAsync(string sourceRef, MergeMode mode);

    /// <summary>4g's "Check out &lt;B&gt; and merge &lt;A&gt; into it": one local operation of two steps.</summary>
    Task CheckoutAndMergeAsync(string branchRef, string sourceRef);

    /// <summary>Opens the reset dialog and resets the current branch (or a detached HEAD) to <paramref name="commit"/> after its backups (2.11).</summary>
    Task ResetAsync(string commit);

    /// <summary>Cherry-picks one commit onto HEAD (2.12).</summary>
    Task CherryPickAsync(string commit);

    /// <summary>
    /// Cherry-picks several commits with one git call that takes the whole list, oldest first by
    /// the graph's rows: <c>git cherry-pick &lt;oldest&gt; … &lt;newest&gt;</c> (docs/phase-4-design.md
    /// section 4, D136). Git stops at the first commit that does not apply cleanly and keeps the
    /// rest in its list, which the banner's Continue and Skip go on with.
    /// <paramref name="commits"/> are the SHAs the menu captured, in any order.
    /// </summary>
    Task CherryPickSeveralAsync(IReadOnlyList<string> commits);

    /// <summary>Reverts one commit, a merge commit with <c>-m 1</c> (2.14).</summary>
    Task RevertAsync(string commit);

    /// <summary>
    /// Reverts several commits with one git call that takes the whole list, newest first by the
    /// graph's rows: <c>git revert --no-edit &lt;newest&gt; … &lt;oldest&gt;</c> (section 4, D136).
    /// Git stops at the first that does not apply cleanly and keeps the rest in its list, as for
    /// <see cref="CherryPickSeveralAsync"/>. Merge commits are not reverted in a list (the menu
    /// disables it).
    /// </summary>
    Task RevertSeveralAsync(IReadOnlyList<string> commits);

    /// <summary>Confirms, backs up and aborts the merge, cherry-pick, revert, rebase or <c>git am</c> in progress (2.15, section 5).</summary>
    Task AbortAsync();

    // Remotes and the network (RepositoryOperations.Remotes.cs, sub-package WP7-remotes).

    /// <summary>Fetches every remote (2.16, D109): the toolbar's Fetch and "Fetch all remotes".</summary>
    Task FetchAllAsync();

    /// <summary>Fetches one remote (2.16): "Fetch &lt;r&gt;".</summary>
    Task FetchAsync(string remote);

    /// <summary>Pulls the current branch from its upstream as git is configured (2.18, D84).</summary>
    Task PullAsync();

    /// <summary>The toolbar's and the empty graph area's Push: the current branch to its upstream, or the push dialog (2.19).</summary>
    Task PushCurrentBranchAsync();

    /// <summary>"Push &lt;b&gt; to &lt;r&gt;/&lt;u&gt;" or "Push &lt;b&gt;…" (2.19): to its upstream, or through the push dialog when it has none.</summary>
    Task PushBranchAsync(string branchRef);

    /// <summary>4g's push of a local branch to a remote branch (<c>refs/remotes/&lt;r&gt;/&lt;x&gt;</c>), with <c>--set-upstream</c> for "and track it".</summary>
    Task PushBranchToAsync(string branchRef, string remoteBranchRef, bool setUpstream);

    /// <summary>Confirms, backs up and force-pushes with lease the local branch to its upstream (2.20, D100).</summary>
    Task ForcePushAsync(string branchRef);

    /// <summary>"Set upstream…" or "Change upstream…": the pick dialog of remote branches (2.21).</summary>
    Task SetUpstreamAsync(string branchRef);

    /// <summary>"Stop tracking &lt;r&gt;/&lt;u&gt;" (2.21).</summary>
    Task UnsetUpstreamAsync(string branchRef);

    /// <summary>Confirms, backs up and deletes the branch on its remote with a lease (2.22): <paramref name="remoteBranchRef"/> is <c>refs/remotes/&lt;r&gt;/&lt;b&gt;</c>.</summary>
    Task DeleteRemoteBranchAsync(string remoteBranchRef);

    /// <summary>"Manage remotes…": the Remotes dialog (2.23, 4f).</summary>
    Task ManageRemotesAsync();

    /// <summary>"Add remote…": the Remotes dialog, opened on its form for a new remote (2.23).</summary>
    Task ShowAddRemoteAsync();

    /// <summary>"Change URL…" and "Rename &lt;r&gt;…": the Remotes dialog, opened on its form for <paramref name="remote"/> (2.23).</summary>
    Task ShowEditRemoteAsync(string remote);

    /// <summary>"Remove &lt;r&gt;…": confirms, backs up its remote branches and removes the remote (2.23). Also the Remotes dialog's Remove.</summary>
    Task RemoveRemoteAsync(string remote);

    /// <summary>"Push &lt;t&gt; to &lt;r&gt;" or "Push &lt;t&gt;…" (the remote choice dialog with several remotes) (2.29).</summary>
    Task PushTagAsync(string tagRef);

    /// <summary>"Delete &lt;t&gt; on &lt;r&gt;…" or "Delete &lt;t&gt; on a remote…": confirms, backs up and deletes with a lease (2.29).</summary>
    Task DeleteRemoteTagAsync(string tagRef);

    // Stashes and tags (RepositoryOperations.StashTags.cs, sub-package WP7-stashtags).

    /// <summary>Opens the stash dialog and stashes the changes (2.24): the toolbar's Stash and "Stash changes…".</summary>
    Task StashAsync();

    /// <summary>Applies the stash whose commit is <paramref name="stash"/> (2.25).</summary>
    Task ApplyStashAsync(string stash);

    /// <summary>Pops the stash whose commit is <paramref name="stash"/> (2.25).</summary>
    Task PopStashAsync(string stash);

    /// <summary>The toolbar's Pop: pops <c>stash@{0}</c> (2.25).</summary>
    Task PopNewestStashAsync();

    /// <summary>Confirms, backs up and drops the stash whose commit is <paramref name="stash"/> (2.26).</summary>
    Task DropStashAsync(string stash);

    /// <summary>Opens the tag dialog and creates a tag (2.27) at <paramref name="target"/>: a full ref name, a commit SHA, or null for HEAD.</summary>
    Task CreateTagAsync(string? target);

    /// <summary>Confirms, backs up and deletes the local tag (2.28).</summary>
    Task DeleteTagAsync(string tagRef);

    // Files (RepositoryOperations.Files.cs, sub-package WP7-stashtags).

    /// <summary>"Stage" and "Mark as resolved" (a conflicted file): phase 2's stage, through the repository (D76).</summary>
    Task StageFilesAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>"Unstage": phase 2's unstage.</summary>
    Task UnstageFilesAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>"Discard changes…": phase 2's discard, with its confirmation and snapshot (D62, D67).</summary>
    Task DiscardFilesAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>The working-changes row's "Stage all".</summary>
    Task StageAllAsync();

    /// <summary>The working-changes row's "Unstage all".</summary>
    Task UnstageAllAsync();

    /// <summary>The working-changes row's "Discard all changes…" (phase 2's Discard all).</summary>
    Task DiscardAllAsync();

    /// <summary>"Ignore &lt;file name&gt;": appends <c>/&lt;path&gt;</c>, escaped, to .gitignore (2.30).</summary>
    Task IgnoreFileAsync(string path);

    /// <summary>"Ignore all *.&lt;ext&gt; files": appends <c>*.&lt;ext&gt;</c>, escaped, to .gitignore (2.30).</summary>
    Task IgnoreExtensionAsync(string path);

    /// <summary>"Open file": the working tree's version, in the system's application for it (2.32).</summary>
    Task OpenFileAsync(string path);

    /// <summary>"Show in folder": the file selected in the file manager, or the working tree's folder for null (2.32).</summary>
    Task ShowInFolderAsync(string? path);

    /// <summary>"Copy SHA" (2.32).</summary>
    Task CopyShaAsync(string sha);

    /// <summary>"Copy SHAs": one per line, in the graph's row order (2.32).</summary>
    Task CopyShasAsync(IReadOnlyList<string> shas);

    /// <summary>"Copy path": the repository-relative path with <c>/</c> (2.32).</summary>
    Task CopyPathAsync(string path);

    /// <summary>"Copy branch name" and "Copy tag name": the ref's short name (2.32).</summary>
    Task CopyRefNameAsync(string fullRef);

    /// <summary>"Copy URL": the remote's fetch URL with its user information removed (2.23, 2.32, D103).</summary>
    Task CopyRemoteUrlAsync(string remote);

    /// <summary>Opens a working-tree file's diff, as a click on its row does (D63).</summary>
    void OpenDiff(ChangedFile file, bool staged);

    // Phase 4: rebase and conflicts (docs/phase-4-design.md; RepositoryOperations.Rebase.cs and
    // .Conflicts.cs, work package WP4).

    /// <summary>
    /// "Rebase {cur} onto this commit", "Rebase {cur} onto {b}", "Rebase {cur} onto {r}/{b}" and a
    /// drop's "Rebase {A} onto {B}" (1.1, 1.3, D142): one local operation that checks HEAD and
    /// what is in progress, saves the backup of 1.2 and runs <c>git rebase &lt;onto's SHA&gt;</c>.
    /// It asks nothing (D135).
    /// </summary>
    Task RebaseAsync(RebaseTarget onto);

    /// <summary>
    /// A drop's "Check out {A} and rebase it onto {B}" (1.3): one operation of several steps that
    /// refuses a dirty tree (unless <c>rebase.autoStash</c>), checks out
    /// <paramref name="branchRef"/> (phase 3's checkout), saves its backup and rebases it onto
    /// <paramref name="onto"/>; when the rebase stops, the app's Abort checks out again the branch
    /// the user was on.
    /// </summary>
    Task CheckoutAndRebaseAsync(string branchRef, RebaseTarget onto);

    /// <summary>The "Interactive rebase…" items (2.1): opens the interactive rebase's list in the graph area (2.2).</summary>
    Task OpenInteractiveRebaseAsync(InteractiveRebaseRequest request);

    /// <summary>
    /// "Squash {N} commits…" (section 3): the message dialog with git's squash message, then one
    /// local operation that saves the backup and runs an interactive rebase that joins
    /// <paramref name="commits"/> (the SHAs the menu captured, in any order).
    /// </summary>
    Task SquashAsync(IReadOnlyList<string> commits);

    /// <summary>"Pull from {up} (rebase)" (section 6, D147): the current branch's pull with its integration forced to a rebase.</summary>
    Task PullRebaseAsync();

    /// <summary>"Resolve conflict", and a click on a conflicted file (11.1, D148): opens the conflict resolver on <paramref name="file"/>.</summary>
    void ResolveConflict(ChangedFile file);

    /// <summary>"Open in merge tool" (11.9, D149): runs git's configured merge tool on <paramref name="path"/> as an operation that can be cancelled.</summary>
    Task OpenMergeToolAsync(string path);

    // Phase 5: history, traceability and undo (docs/phase-5-design.md 13.1). The pages' actions
    // are in RepositoryOperations.Pages.cs (WP6), compare, hide and solo in .Filters.cs (WP7), the
    // backups list's restores in .Undo.cs (WP3).

    /// <summary>"File history" (4.1, 4.4): the history page of a file, in place of the graph.</summary>
    Task ShowFileHistoryAsync(FileHistoryRequest request);

    /// <summary>"Blame", "Blame at this commit", "Blame before this commit" (4.2, 4.4): the blame page of a file's version.</summary>
    Task ShowBlameAsync(BlameRequest request);

    /// <summary>"Show reflog" and "Show reflog of {b}" (5.1): the reflog page on HEAD's reflog (null) or a local branch's (its full name).</summary>
    Task ShowReflogAsync(string? branchRef);

    /// <summary>"Show backups" (5.1, 5.3): the reflog page on the backups list.</summary>
    Task ShowBackupsAsync();

    /// <summary>"Show in graph" (4.1, 5.2, 11.3): closes the page, selects the commit and reveals it.</summary>
    Task ShowInGraphAsync(string sha);

    // A reflog row's and a backup row's commit actions (5.2, 5.3): phase 3's operations, which
    // first check inside their operation that the commit still exists (`git cat-file -e
    // <sha>^{commit}`): a gc may have pruned it since the page read it. Refused otherwise, with
    // "The commit {short} no longer exists." and nothing changed.

    /// <summary>"Create branch here…" of a reflog row or a backup: <see cref="CreateBranchAsync"/> at the commit it keeps, which must still exist.</summary>
    Task CreateBranchAtKeptCommitAsync(string sha);

    /// <summary>"Check out this commit" of a reflog row or a backup: <see cref="CheckoutAsync"/> of the commit it keeps, which must still exist.</summary>
    Task CheckoutKeptCommitAsync(string sha);

    /// <summary>"Reset {cur} to this commit…" of a reflog row: <see cref="ResetAsync"/> to the commit it keeps, which must still exist.</summary>
    Task ResetToKeptCommitAsync(string sha);

    /// <summary>"Cherry-pick onto {cur}" of a reflog row: <see cref="CherryPickAsync"/> of the commit it keeps, which must still exist.</summary>
    Task CherryPickKeptCommitAsync(string sha);

    /// <summary>
    /// A backup's own restore in the backups list (7.10): its kind decides ("Restore branch {b}",
    /// "Restore tag {t}", "Restore stash", "Restore the discarded files…", "Restore the changes…",
    /// "Restore the staged changes", "Push {b} back to {r}…"), as one operation recorded in the
    /// undo history.
    /// </summary>
    Task RestoreBackupAsync(BackupInfo backup);

    /// <summary>"Compare {b} with {cur}" and a drop's "Compare {A} with {B}" (6.2): the comparison of <paramref name="toRef"/> since it left <paramref name="fromRef"/>, in the right panel.</summary>
    Task CompareBranchesAsync(string fromRef, string toRef);

    /// <summary>"Hide {b}" (8.1): the branch (its full name), and a local branch's remote-tracking upstream with it.</summary>
    Task HideBranchAsync(string branchRef);

    /// <summary>"Show {b}" (8.1): shows a hidden branch again, and the upstream that was hidden with it.</summary>
    Task ShowBranchAsync(string branchRef);

    /// <summary>"Solo {b}" (8.1): the graph shows the soloed branches' histories and HEAD's; a local branch's upstream is soloed with it.</summary>
    Task SoloBranchAsync(string branchRef);

    /// <summary>"Stop soloing {b}" (8.1).</summary>
    Task StopSoloingBranchAsync(string branchRef);

    /// <summary>The sections' "Show all hidden branches" (8.3).</summary>
    Task ShowAllHiddenBranchesAsync();

    /// <summary>The sections' "Stop soloing" (8.3): solo mode ends.</summary>
    Task StopSoloingAsync();

    /// <summary>"Hide every branch of {r}" (8.1): the remote's branches, now and later.</summary>
    Task HideRemoteAsync(string remote);

    /// <summary>"Show every branch of {r}" (8.1).</summary>
    Task ShowRemoteAsync(string remote);

    /// <summary>The activity log's "Copy command", "Copy output" and "Copy title" (2.2): the text as shown.</summary>
    Task CopyTextAsync(string text);
}
