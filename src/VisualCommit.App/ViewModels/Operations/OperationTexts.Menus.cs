using System.Globalization;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The menus' labels and disabled reasons (docs/phase-3-design.md 4b, 4g, 4h, section 2's
/// "Disabled" lines, D114, D119, D120), which <c>RepositoryMenus</c> builds its items from. Names
/// are short names; an item that opens a dialog or asks first ends in "…". The toolbar's own
/// reasons, which the empty graph area's menu repeats, are <see cref="ToolbarViewModel"/>'s.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>The menus' texts, apart from the operations' own so that their names never meet.</summary>
    public static class MenuTexts
    {
        // Headers.

        /// <summary>The working-changes row's menu header (4b).</summary>
        public const string WorkingChangesHeader = "Working changes";

        /// <summary>A commit's header: <c>a1b2c3d Add core</c>.</summary>
        public static string CommitHeader(string sha, string subject) => $"{Short(sha)} {subject}".TrimEnd();

        /// <summary>Several commits' header: <c>3 commits</c>.</summary>
        public static string CommitsHeader(int count) => Count(count, "commit");

        /// <summary>A stash's header: <c>stash@{0} On main: Experiment</c>.</summary>
        public static string StashHeader(string name, string message) => $"{name} {message}";

        /// <summary>A drop's header: <c>feature/alpha → main</c> (4g).</summary>
        public static string DropHeader(string source, string target) => $"{source} → {target}";

        // Commits (2.3, 2.4, 2.11, 2.12, 2.14, 2.27).

        public const string CheckOutThisCommit = "Check out this commit";

        public const string CreateBranchHere = "Create branch here…";

        public const string CreateTagHere = "Create tag here…";

        public const string RevertThisCommit = "Revert this commit";

        public const string RevertThisMergeCommit = "Revert this merge commit";

        public const string CopySha = "Copy SHA";

        public const string CopyShas = "Copy SHAs";

        /// <summary>2.3: HEAD is detached on the commit already.</summary>
        public const string AlreadyAtThisCommit = "HEAD is already at this commit";

        /// <summary>2.12: a merge commit is not cherry-picked (D94).</summary>
        public const string MergeCommitsCannotBePicked = "Merge commits cannot be cherry-picked here";

        /// <summary>2.13: a selection with a merge commit or a stash.</summary>
        public const string MergesAndStashesCannotBePicked = "Merge commits and stashes cannot be cherry-picked";

        /// <summary>2.14: a selection with a merge commit.</summary>
        public const string RevertMergesOneAtATime = "Revert merge commits one at a time";

        public static string CherryPickOnto(string current) => $"Cherry-pick onto {current}";

        public static string CherryPickSeveralOnto(int count, string current) => $"Cherry-pick {Count(count, "commit")} onto {current}";

        public static string RevertSeveralLabel(int count) => $"Revert {Count(count, "commit")}";

        public static string ResetTo(string current) => $"Reset {current} to this commit…";

        /// <summary>2.12: the commit is in HEAD's history already.</summary>
        public static string AlreadyOn(string current) => $"This commit is already on {current}";

        /// <summary>2.13: some of the selected commits are in HEAD's history already.</summary>
        public static string SomeAlreadyOn(string current) => $"Some of these commits are already on {current}";

        /// <summary>2.8, 2.12, 2.14, D119: "Finish or abort the merge first", the operations' and the toolbar's text (<see cref="FinishOrAbortFirst"/>).</summary>
        public static string FinishOrAbort(Core.Git.InProgressKind kind) => FinishOrAbortFirst(kind);

        // Rebase, interactive rebase and squash (docs/phase-4-design.md 1.1, 2.1, 3, 7.1, 10.4).

        /// <summary>A commit's "Rebase {cur} onto this commit" (1.1).</summary>
        public static string RebaseOntoThisCommit(string current) => $"Rebase {current} onto this commit";

        /// <summary>A commit's "Interactive rebase of {cur} from here…" (2.1).</summary>
        public static string InteractiveRebaseFromHere(string current) => $"Interactive rebase of {current} from here…";

        /// <summary>A branch's or a drop's "Rebase {cur} onto {b}" (1.1).</summary>
        public static string RebaseOntoLabel(string branch, string target) => $"Rebase {branch} onto {target}";

        /// <summary>A branch's or a drop's "Interactive rebase of {cur} onto {b}…" (2.1).</summary>
        public static string InteractiveRebaseOnto(string branch, string target) => $"Interactive rebase of {branch} onto {target}…";

        /// <summary>A drop's "Check out {A} and rebase it onto {B}" (1.1).</summary>
        public static string CheckOutAndRebaseLabel(string branch, string target) => $"Check out {branch} and rebase it onto {target}";

        /// <summary>A drop's "Check out {A} and rebase it onto {B} interactively…" (2.1).</summary>
        public static string CheckOutAndRebaseInteractively(string branch, string target) => $"Check out {branch} and rebase it onto {target} interactively…";

        /// <summary>Several commits' "Squash {N} commits…" (section 3).</summary>
        public static string SquashCommits(int count) => $"Squash {Count(count, "commit")}…";

        /// <summary>The current branch's "Pull from {up} (rebase)" (section 6).</summary>
        public static string PullFromRebase(string upstream) => $"Pull from {upstream} (rebase)";

        /// <summary>10.4: rebase and interactive items while HEAD is detached.</summary>
        public const string CheckOutABranchToRebase = "Check out a branch to rebase";

        /// <summary>10.4: squash while HEAD is detached.</summary>
        public const string CheckOutABranchToSquash = "Check out a branch to squash";

        /// <summary>10.4, D119: conflicts without an operation (a stash that did not apply cleanly).</summary>
        public const string ResolveTheConflictsFirst = ToolbarViewModel.ResolveConflictsText;

        /// <summary>10.4: "from here" on HEAD's own commit.</summary>
        public static string NoCommitsAfterThisOne(string current) => $"{current} has no commits after this one";

        /// <summary>10.4: "from here" on a commit HEAD does not reach.</summary>
        public static string NotOn(string current) => $"This commit is not on {current}";

        /// <summary>10.4: "from here" on a commit HEAD reaches off its linear first-parent line.</summary>
        public static string MergeLiesBetweenThisCommitAnd(string current) => $"A merge lies between this commit and {current}";

        /// <summary>10.4: squash with a merge commit among the selected.</summary>
        public const string MergeCommitsCannotBeSquashed = "Merge commits cannot be squashed";

        /// <summary>10.4: squash with a selected commit HEAD does not reach.</summary>
        public static string OnlyCommitsOfCanBeSquashed(string current) => $"Only commits of {current} can be squashed";

        /// <summary>10.4: squash of commits that are not a first-parent chain.</summary>
        public const string SelectCommitsThatFollowOneAnother = "Select commits that follow one another";

        /// <summary>10.4: squash of a chain off HEAD's linear line.</summary>
        public static string MergeLiesBetweenTheseCommitsAnd(string current) => $"A merge lies between these commits and {current}";

        /// <summary>10.4: squash with the root commit among the selected.</summary>
        public const string FirstCommitCannotBeSquashed = "The first commit cannot be squashed";

        // The conflicted file's menu (7.1, 11.9).

        public const string ResolveConflict = "Resolve conflict";

        public const string OpenInMergeTool = "Open in merge tool";


        // Stashes (2.24 to 2.26).

        public const string ApplyStash = "Apply stash";

        public const string PopStash = "Pop stash";

        public const string DropStash = "Drop stash…";

        public const string StashChanges = "Stash changes…";

        // The working-changes row (4b).

        public const string StageAll = "Stage all";

        public const string UnstageAll = "Unstage all";

        public const string DiscardAllChanges = "Discard all changes…";

        public const string ShowInFolder = "Show in folder";

        public const string NothingToStage = "Nothing to stage";

        public const string NothingStaged = "Nothing staged";

        public const string NothingToDiscard = "Nothing to discard";

        // Branches (2.1, 2.2, 2.4 to 2.10, 2.18 to 2.22).

        public const string CopyBranchName = "Copy branch name";

        public const string SetUpstream = "Set upstream…";

        public const string ChangeUpstream = "Change upstream…";

        public const string CreateBranch = "Create branch…";

        /// <summary>2.6: the branch HEAD is on.</summary>
        public const string CannotDeleteCurrentBranch = "You cannot delete the branch you are on";

        /// <summary>2.8: a merge needs a branch to merge into.</summary>
        public const string CheckOutABranchToMergeInto = "Check out a branch to merge into";

        /// <summary>2.21: nothing to pick in the dialog.</summary>
        public const string NoRemoteBranchesToTrack = "No remote branches to track: fetch or push first";

        public static string CheckOutName(string name) => $"Check out {name}";

        public static string MergeInto(string source, string current) => $"Merge {source} into {current}";

        public static string MergeIntoWithCommit(string source, string current) => $"Merge {source} into {current} (create a merge commit)";

        public static string PullFrom(string upstream) => $"Pull from {upstream}";

        public static string FastForwardTo(string branch, string target) => $"Fast-forward {branch} to {target}";

        public static string PushTo(string branch, string upstream) => $"Push {branch} to {upstream}";

        public static string PushWithDialog(string branch) => $"Push {branch}…";

        public static string ForcePushLabel(string branch) => $"Force push {branch}…";

        public static string RenameLabel(string branch) => $"Rename {branch}…";

        public static string DeleteLabel(string name) => $"Delete {name}…";

        public static string RemoveLabel(string remote) => $"Remove {remote}…";

        public static string StopTracking(string upstream) => $"Stop tracking {upstream}";

        public static string DeleteOnRemoteLabel(string branch, string remote) => $"Delete {branch} on {remote}…";

        /// <summary>2.2, D114: HEAD is on the local branch a remote branch would check out.</summary>
        public static string AlreadyCheckedOut(string branch) => $"{branch} is already checked out";

        /// <summary>2.7, 2.18, D114: the upstream is configured but gone.</summary>
        public static string NoLongerExists(string upstream) => $"{upstream} no longer exists";

        /// <summary>2.20: force push to a gone upstream.</summary>
        public static string NoLongerExistsPushNormally(string upstream) => $"{upstream} no longer exists: push normally";

        /// <summary>2.7: nothing to fast-forward.</summary>
        public static string UpToDateWith(string branch, string upstream) => $"{branch} is up to date with {upstream}";

        /// <summary>2.7, 2.10, 4g: not a fast-forward.</summary>
        public static string HasCommitsThat(string branch, string other) => $"{branch} has commits that {other} does not have";

        /// <summary>
        /// 2.7, D120: a branch checked out in another worktree is neither fast-forwarded nor checked
        /// out here. The folder is named as <c>WorktreeInfo.Path</c> gives it, the full path in the
        /// platform's form (backslashes on Windows; check 30).
        /// </summary>
        public static string CheckedOutInAnotherWorktree(string branch, string path) =>
            $"{branch} is checked out in another worktree ({path})";

        // A drop (4g).

        public static string CheckOutAndMergeLabel(string target, string source) => $"Check out {target} and merge {source} into it";

        public static string PushToAndTrack(string branch, string remoteBranch) => $"Push {branch} to {remoteBranch} and track it";

        /// <summary>4g: a remote whose fetch refspec is not the default; the branch's name there is not known.</summary>
        public static string MapsBranchesDifferently(string remote, string branch) => $"{remote} maps its branches differently: use Push {branch}…";

        // Remotes (2.16, 2.23).

        public const string AddRemote = "Add remote…";

        public const string FetchAllRemotes = "Fetch all remotes";

        public const string ManageRemotes = "Manage remotes…";

        public const string ChangeUrl = "Change URL…";

        public const string CopyUrl = "Copy URL";

        public static string FetchRemote(string remote) => $"Fetch {remote}";

        // Tags (2.27 to 2.29).

        public const string CreateTagAtHead = "Create tag at HEAD…";

        public const string CopyTagName = "Copy tag name";

        public static string PushTagTo(string tag, string remote) => $"Push {tag} to {remote}";

        public static string DeleteTagOnRemote(string tag, string remote) => $"Delete {tag} on {remote}…";

        public static string DeleteTagOnARemote(string tag) => $"Delete {tag} on a remote…";

        // Files (2.30, 2.32, the stage panel and the diff view of phase 2).

        public const string Stage = "Stage";

        public const string MarkAsResolved = "Mark as resolved";

        public const string Unstage = "Unstage";

        public const string DiscardChanges = "Discard changes…";

        public const string OpenFile = "Open file";

        public const string CopyPath = "Copy path";

        public const string StageSelectedLines = "Stage selected lines";

        public const string DiscardSelectedLines = "Discard selected lines…";

        public const string UnstageSelectedLines = "Unstage selected lines";

        public const string StageHunk = "Stage hunk";

        public const string DiscardHunk = "Discard hunk…";

        public const string UnstageHunk = "Unstage hunk";

        public const string StageFile = "Stage file";

        public const string DiscardFile = "Discard file…";

        public const string UnstageFile = "Unstage file";

        /// <summary>4b: a conflicted file is resolved, not discarded.</summary>
        public const string ResolveTheConflictFirst = "Resolve the conflict first";

        /// <summary>4b, D75: an untracked folder that is a repository of its own.</summary>
        public const string NestedRepositoryNotDiscarded = "A repository of its own is not discarded";

        /// <summary>2.30: a tracked file.</summary>
        public const string OnlyUntrackedCanBeIgnored = "Only untracked files can be ignored";

        /// <summary>2.32: a deleted file.</summary>
        public const string NotInWorkingTree = "The file is not in the working tree";

        public static string IgnoreName(string fileName) => $"Ignore {fileName}";

        public static string IgnoreExtension(string extension) =>
            string.Create(CultureInfo.InvariantCulture, $"Ignore all *.{extension} files");
    }
}
