// Phase 5's file history, blame, reflog and backups items (docs/phase-5-design.md 4.4, 5.1, 5.2,
// 5.3, 7.10, 13.1) and the menus of their pages: the builders of RepositoryMenus.cs call the
// hooks below where 13.1 puts their items, and the pages build their rows' menus with
// ForFileHistoryRow, ForBlameRun, ForReflogEntry, ForBackup and ForReflogSource. The texts are in
// OperationTexts.Menus.Pages.cs; the order and separators of the pages' menus are decision BD8 of
// docs/test-reports/phase-5.md.

using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// A row of the file history page (4.1, BD8): header <c>{short} {subject}</c>; show in graph,
    /// blame at this commit (of a deleted file, its first parent's version) — copy SHA.
    /// <paramref name="inGraph"/> says whether the graph shows the commit (4.3).
    /// </summary>
    public static MenuModel ForFileHistoryRow(RepositoryMenuContext context, FileHistoryRow row, bool inGraph, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(actions);
        var sha = row.Commit.Sha;
        var blame = BlameOfHistoryRow(row);
        return Build(
            Header(CommitHeader(sha, row.Commit.Subject)),
            [
                ShowInGraphItem(sha, inGraph, actions),
                blame is null ? null : Item(actions, BlameAtThisCommit, () => actions.ShowBlameAsync(blame)),
            ],
            [
                Item(actions, CopySha, () => actions.CopyShaAsync(sha)),
            ]);
    }

    /// <summary>
    /// What "Blame at this commit" of a history row opens (4.4): that commit's version of the path
    /// at it; for a row that deleted the file, its first parent's, titled <c>before {short}</c>.
    /// Null for a deletion without a parent, which git never lists.
    /// </summary>
    public static BlameRequest? BlameOfHistoryRow(FileHistoryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Status != FileChangeKind.Deleted)
        {
            return new BlameRequest(row.Path, row.Commit.Sha);
        }

        return row.Commit.Parents.Count > 0 ? new BlameRequest(row.Path, row.Commit.Parents[0], Before(row.Commit.Sha)) : null;
    }

    /// <summary>
    /// A run of the blame's gutter (4.2, BD8): header <c>{short} {subject}</c>; show in graph —
    /// blame before this commit (the commit's first parent's version of the path it had there,
    /// from <c>previous</c>; disabled at the file's first commit), file history (from the run's
    /// commit) — copy SHA. A run not committed yet: header <c>Not committed yet</c> and show in
    /// graph alone, which selects the working-changes row.
    /// </summary>
    public static MenuModel ForBlameRun(RepositoryMenuContext context, BlameCommit commit, bool inGraph, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(actions);
        var sha = commit.Sha;
        if (commit.IsUncommitted)
        {
            return Build(Header(NotCommittedYet), [ShowInGraphItem(sha, inGraph, actions)]);
        }

        var before = commit is { PreviousSha: { } previous, PreviousPath: { } previousPath } ? new BlameRequest(previousPath, previous) : null;
        return Build(
            Header(CommitHeader(sha, commit.Summary)),
            [
                ShowInGraphItem(sha, inGraph, actions),
            ],
            [
                Item(actions, BlameBeforeThisCommit, () => before is null ? Task.CompletedTask : actions.ShowBlameAsync(before), before is null ? FilesFirstCommit : null),
                Item(actions, FileHistory, () => actions.ShowFileHistoryAsync(new FileHistoryRequest(commit.Path, sha))),
            ],
            [
                Item(actions, CopySha, () => actions.CopyShaAsync(sha)),
            ]);
    }

    /// <summary>
    /// A row of the reflog page (5.2, BD8): header <c>{short} {subject}</c>; create branch here…,
    /// check out this commit — reset {cur} to this commit…, cherry-pick onto {cur} — show in
    /// graph, copy SHA. Each is phase 3's operation with its own question and backup; their
    /// reasons come from the entry (its parents, HEAD at it), not from the graph.
    /// </summary>
    public static MenuModel ForReflogEntry(RepositoryMenuContext context, ReflogEntry entry, bool inGraph, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(actions);
        var sha = entry.Sha;
        var current = context.CurrentName;
        var headIsHere = string.Equals(context.Head.Sha, sha, StringComparison.Ordinal);
        var pickReason = entry.Parents.Count > 1 ? MergeCommitsCannotBePicked
            : headIsHere ? AlreadyOn(current)
            : context.IsUnborn ? ToolbarViewModel.UnbornText
            : InProgressReason(context);
        return Build(
            Header(CommitHeader(sha, entry.Subject)),
            [
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(sha)),
                Item(actions, CheckOutThisCommit, () => actions.CheckoutAsync(sha), context.IsDetached && headIsHere ? AlreadyAtThisCommit : null),
            ],
            [
                Item(actions, ResetTo(current), () => actions.ResetAsync(sha), context.IsUnborn ? ToolbarViewModel.UnbornText : null),
                Item(actions, CherryPickOnto(current), () => actions.CherryPickAsync(sha), pickReason),
            ],
            [
                ShowInGraphItem(sha, inGraph, actions),
                Item(actions, CopySha, () => actions.CopyShaAsync(sha)),
            ]);
    }

    /// <summary>
    /// A row of the backups list (5.3, 7.10, BD8): header the backup in words (12.3); its own
    /// restore, by kind — then, for a backup that holds a commit (a <c>discard-</c> snapshot
    /// included), create branch here…, check out this commit — copy SHA.
    /// </summary>
    public static MenuModel ForBackup(RepositoryMenuContext context, BackupInfo backup, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(actions);
        var commit = backup.Main?.Commit is { Length: > 0 } kept ? kept : null;
        return Build(
            Header(BackupWords(backup, context.Remotes)),
            [RestoreItem(context, backup, actions)],
            commit is null
                ? []
                : [
                    Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(commit)),
                    Item(actions, CheckOutThisCommit, () => actions.CheckoutAsync(commit), context.IsDetached && string.Equals(context.Head.Sha, commit, StringComparison.Ordinal) ? AlreadyAtThisCommit : null),
                ],
            commit is null ? [] : [Item(actions, CopySha, () => actions.CopyShaAsync(commit))]);
    }

    /// <summary>
    /// The reflog page's source menu (5.1, 5.2, BD7): header <c>Reflog and backups</c>; HEAD, every
    /// local branch by its full name in git's order of the refs — backups. Remote branches and
    /// <c>refs/stash</c> are left out (5.2). <paramref name="choose"/> gets <c>HEAD</c>, a local
    /// branch's full ref name, or null for the backups.
    /// </summary>
    public static MenuModel ForReflogSource(RepositoryMenuContext context, Action<string?> choose)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(choose);
        return Build(
            Header(ReflogSourceHeader),
            [
                new MenuItem(HeadSource, () => choose(HeadSource)),
                .. context.LocalBranches.Select(branch => new MenuItem(branch.Name, () => choose(branch.FullName))),
            ],
            [new MenuItem(BackupsSource, () => choose(null))]);
    }

    /// <summary>
    /// "File history" and "Blame" (4.4), a group of their own before "Open file" in the file
    /// menus: of the stage lists' files (<paramref name="side"/> Unstaged or Staged), a commit's
    /// files and a diff's. Left out in the conflict resolver's header menu
    /// (<see cref="FileMenuTarget.InConflictResolver"/>) and for a nested repository; disabled for
    /// a file in no commit yet and before the first commit.
    /// </summary>
    private static IEnumerable<MenuItem?> FileHistoryItems(RepositoryMenuContext context, FileMenuTarget file, DiffSide side, IRepositoryActions actions)
    {
        if (file.InConflictResolver || file.IsNestedRepository)
        {
            return [];
        }

        var (history, blame, reason) = side == DiffSide.Commit ? CommitFilePages(context, file) : WorkingTreeFilePages(context, file, side);
        if (history is null && blame is null && reason is null)
        {
            return [];
        }

        return
        [
            Item(actions, FileHistory, () => history is null ? Task.CompletedTask : actions.ShowFileHistoryAsync(history), history is null ? reason : null),
            Item(actions, Blame, () => blame is null ? Task.CompletedTask : actions.ShowBlameAsync(blame), blame is null ? reason : null),
        ];
    }

    /// <summary>
    /// What a stage list's file opens (4.4's table): its history from HEAD under its path in HEAD
    /// (a staged rename's old path), and the working tree's blame, or HEAD's version when the file
    /// is not in the working tree (a deletion, titled <c>at HEAD</c>; a conflicted file HEAD has).
    /// An untracked file and a staged new one are in no commit yet; nothing is before the first commit.
    /// </summary>
    private static (FileHistoryRequest? History, BlameRequest? Blame, string? Reason) WorkingTreeFilePages(RepositoryMenuContext context, FileMenuTarget file, DiffSide side)
    {
        if (context.IsUnborn || context.Head.Sha is not { } head)
        {
            return (null, null, ToolbarViewModel.UnbornText);
        }

        if (file.IsUntracked || (file.File.Kind == FileChangeKind.Added && !file.IsConflicted))
        {
            return (null, null, InNoCommitYet);
        }

        var history = new FileHistoryRequest(side == DiffSide.Staged && file.File.OldPath is { } old ? old : file.Path, HeadSource);
        if (file.ExistsInWorkingTree)
        {
            return (history, new BlameRequest(file.Path, null), null);
        }

        // A conflicted file whose version HEAD lacks (only theirs has it, or ours deleted it).
        var headLacksIt = file.IsConflicted && context.Status.FindConflict(file.Path)?.Kind is ConflictKind.AddedByThem or ConflictKind.DeletedByUs or ConflictKind.BothDeleted;
        return (history, headLacksIt ? null : new BlameRequest(history.Path, head, AtHead), InNoCommitYet);
    }

    /// <summary>
    /// What a commit's file opens (4.4's table): its history from that commit under its path
    /// there and its blame at that commit; a file the commit deleted, its first parent's version
    /// titled <c>before {short}</c>. A stash's file has its history from the stash's base and its
    /// blame at the stash itself.
    /// </summary>
    private static (FileHistoryRequest? History, BlameRequest? Blame, string? Reason) CommitFilePages(RepositoryMenuContext context, FileMenuTarget file)
    {
        if (file.CommitSha is not { } sha)
        {
            return (null, null, null);
        }

        var start = context.FindStash(sha) is { } stash ? stash.BaseSha : sha;
        var history = new FileHistoryRequest(file.Path, start);
        if (file.File.Kind != FileChangeKind.Deleted)
        {
            return (history, new BlameRequest(file.Path, sha), null);
        }

        // The first parent as the details read it; git's own name for it when the menu was not told (the page resolves it).
        return (history, new BlameRequest(file.Path, file.CommitParent ?? sha + "^", Before(sha)), null);
    }

    /// <summary>"Show reflog" and "Show backups" (5.1): in the working-changes row's, the empty graph area's and the Local branches section's menus, enabled in an unborn repository too (BD22).</summary>
    private static IEnumerable<MenuItem?> ReflogItems(RepositoryMenuContext context, IRepositoryActions actions) =>
    [
        Item(actions, ShowReflog, () => actions.ShowReflogAsync(null)),
        Item(actions, ShowBackups, actions.ShowBackupsAsync),
    ];

    /// <summary>"Show reflog of {b}" (5.1): in a local branch's menu, after its compare; null for a remote branch.</summary>
    private static MenuItem? BranchReflogItem(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions) =>
        branch.Kind == RefKind.LocalBranch ? Item(actions, ShowReflogOf(branch.Name), () => actions.ShowReflogAsync(branch.FullName)) : null;

    /// <summary>"Show in graph" (11.3): disabled for a commit the graph does not have (4.3).</summary>
    private static MenuItem ShowInGraphItem(string sha, bool inGraph, IRepositoryActions actions) =>
        Item(actions, ShowInGraph, () => actions.ShowInGraphAsync(sha), inGraph ? null : NotInTheGraph);

    /// <summary>
    /// A backup's own restore (7.10), or null for a kind that has none (a reset or an abort
    /// without its snapshot, <c>detached</c>, <c>rebase</c>, <c>remote</c>). Each runs
    /// <see cref="IRepositoryActions.RestoreBackupAsync"/>, which decides by the kind.
    /// </summary>
    private static MenuItem? RestoreItem(RepositoryMenuContext context, BackupInfo backup, IRepositoryActions actions)
    {
        var name = backup.Main?.Name ?? string.Empty;
        var changes = backup.Refs.FirstOrDefault(entry => entry.IsChanges);
        MenuItem Restore(string label, string? reason = null) => Item(actions, label, () => actions.RestoreBackupAsync(backup), reason);

        switch (backup.Kind)
        {
            case "branch":
                return Restore(RestoreBranch(name), context.FindRef(HeadsPrefix + name) is null ? null : BranchExists(name));
            case "tag" or "tag-undone":
                return Restore(RestoreTag(name), context.FindRef("refs/tags/" + name) is null ? null : TagExists(name));
            case "stash":
                return Restore(RestoreStash);
            case "discard":
                return Restore(RestoreTheDiscardedFiles);
            case "reset" when changes is not null && IsMixedResetSnapshot(changes):
                // While the index differs from HEAD (7.10): something staged, or files in conflict.
                return Restore(RestoreTheStagedChanges, context.Status.Staged.Count > 0 || context.Status.Conflicts.Count > 0 ? UnstageYourChangesFirst : null);
            case "reset" or "abort" or "skip" when changes is not null:
                return Restore(RestoreTheChanges);
            case "remote-branch" or "remote-tag":
                var (remote, rest, configured) = SplitRemote(name, context.Remotes);
                return Restore(PushBackTo(rest, remote), configured ? null : NoLongerExists(remote));
            case "force-push":
                return ForcePushRestore(context, name, Restore);
            default:
                return null;
        }
    }

    /// <summary>
    /// A force push's backup (7.10): <c>refs/remotes/&lt;name&gt;</c> maps back to its remote and
    /// branch through the local branch whose upstream it is, otherwise by the remote list's longest
    /// matching name (the default fetch refspec); disabled when neither finds it. Whether the
    /// remote-tracking reflog still has the commit the force push left is read by the restore
    /// itself, which says so when it does not.
    /// </summary>
    private static MenuItem ForcePushRestore(RepositoryMenuContext context, string name, Func<string, string?, MenuItem> restore)
    {
        var tracking = context.LocalBranches.FirstOrDefault(branch =>
            string.Equals(branch.UpstreamRef, "refs/remotes/" + name, StringComparison.Ordinal) && branch is { UpstreamRemote: not null, UpstreamRemoteRef: not null });
        if (tracking is { UpstreamRemote: { } upstreamRemote, UpstreamRemoteRef: { } remoteRef })
        {
            return restore(PushBackTo(ShortName(remoteRef), upstreamRemote), null);
        }

        var (remote, rest, configured) = SplitRemote(name, context.Remotes);
        return restore(PushBackTo(rest, remote), configured ? null : RemoteOfBackupNotFound);
    }
}
