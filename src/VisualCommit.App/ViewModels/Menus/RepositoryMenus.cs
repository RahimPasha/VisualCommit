// The menu builders: docs/phase-3-design.md 4a (the model and its rules), 4b (the menu of every
// object, its items, labels, order, enabled state and disabled reasons), 4g (the drop's menu),
// 4h (several commits), D77 and D92 (what is left out), D114 (separators, a local upstream's
// name, a gone upstream, "Check out <r>/<b>" while on <b>), D119 (no stash while an operation is
// in progress) and D120 (a drop's entries that would check out a branch of another worktree).
// Each builder is a pure function of a RepositoryMenuContext; its items start IRepositoryActions
// on the objects captured when the menu opened. Tests: tests/VisualCommit.App.Tests/RepositoryMenusTests.cs.
// Phase 5's items and builders are in three more parts (docs/phase-5-design.md 13.1, section 16):
// RepositoryMenus.Pages.cs (file history, blame, the reflog and the backups; WP6),
// RepositoryMenus.Filters.cs (compare, hide and solo; WP7) and RepositoryMenus.ActivityLog.cs (the
// activity log's rows; WP5). The builders here call the hooks of the first two where 13.1 puts
// the items; a hook that gives nothing leaves its menu as it was.

using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// Builds the context menu of every object of a repository (docs/phase-3-design.md 4b) as data
/// (<see cref="MenuModel"/>, D86): pure functions of a <see cref="RepositoryMenuContext"/>, whose
/// items start <see cref="IRepositoryActions"/> on the objects the menu captured when it opened
/// (full ref names and SHAs, never rows). Items for later phases are left out, not disabled
/// (D77, D92). Every menu starts with its header; groups are separated by one separator, and a
/// group whose items are all left out leaves none (<see cref="Build"/>, D114).
/// </summary>
public static partial class RepositoryMenus
{
    private const string HeadsPrefix = "refs/heads/";

    /// <summary>
    /// One selected commit, right-clicked on its row outside its labels, or its <c>HEAD</c> or "+N"
    /// label (4b): header <c>a1b2c3d Add core</c>; check out, create branch, create tag — cherry-pick,
    /// revert, reset — copy SHA. <paramref name="sha"/> is in <see cref="RepositoryMenuContext.SelectedCommits"/>.
    /// A stash row has the stash's menu (<see cref="ForStash"/>).
    /// </summary>
    public static MenuModel ForCommit(RepositoryMenuContext context, string sha, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sha);
        ArgumentNullException.ThrowIfNull(actions);
        var commit = context.FindSelected(sha) ?? new CommitMenuInfo(sha, string.Empty, [], CommitKind.Commit, -1, IsReachableFromHead: false);
        if (commit.IsStash)
        {
            return ForStash(context, sha, actions);
        }

        var current = context.CurrentName;
        var detachedHere = context.IsDetached && string.Equals(context.Head.Sha, sha, StringComparison.Ordinal);

        var pickReason = commit.IsMerge ? MergeCommitsCannotBePicked
            : commit.IsReachableFromHead ? AlreadyOn(current)
            : context.IsUnborn ? ToolbarViewModel.UnbornText
            : InProgressReason(context);

        // docs/phase-4-design.md 1.1 and 2.1: the object's own reasons first, then HEAD's state.
        var rebaseReason = commit.IsReachableFromHead ? AlreadyOn(current) : RebaseStateReason(context);
        var fromHereReason = commit.HeadLineDepth == 0 ? NoCommitsAfterThisOne(current)
            : !commit.IsReachableFromHead ? NotOn(current)
            : commit.HeadLineDepth is null ? MergeLiesBetweenThisCommitAnd(current)
            : RebaseStateReason(context);
        var onto = new RebaseTarget(sha, OperationTexts.Short(sha));
        var branchRef = context.CurrentBranch?.FullName ?? HeadsPrefix + current;

        return Build(
            Header(CommitHeader(sha, commit.Subject)),
            [
                Item(actions, CheckOutThisCommit, () => actions.CheckoutAsync(sha), detachedHere ? AlreadyAtThisCommit : null),
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(sha)),
                Item(actions, CreateTagHere, () => actions.CreateTagAsync(sha)),
            ],
            [
                Item(actions, CherryPickOnto(current), () => actions.CherryPickAsync(sha), pickReason),
                Item(actions, commit.IsMerge ? RevertThisMergeCommit : RevertThisCommit, () => actions.RevertAsync(sha), InProgressReason(context)),
                Item(actions, RebaseOntoThisCommit(current), () => actions.RebaseAsync(onto), rebaseReason),
                Item(
                    actions,
                    InteractiveRebaseFromHere(current),
                    () => actions.OpenInteractiveRebaseAsync(new InteractiveRebaseRequest(onto, branchRef, FromHere: true, CheckoutFirst: false)),
                    fromHereReason),
                Item(actions, ResetTo(current), () => actions.ResetAsync(sha), context.IsUnborn ? ToolbarViewModel.UnbornText : null),
            ],
            [
                Item(actions, CopySha, () => actions.CopyShaAsync(sha)),
            ]);
    }

    /// <summary>
    /// Two or more selected commits (4b, 4h): header <c>3 commits</c>; cherry-pick N commits, revert N
    /// commits — copy SHAs, on <see cref="RepositoryMenuContext.SelectedCommits"/>.
    /// </summary>
    public static MenuModel ForCommits(RepositoryMenuContext context, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var commits = context.SelectedCommits;
        var shas = commits.Select(commit => commit.Sha).ToList();
        var current = context.CurrentName;

        var pickReason = commits.Any(commit => commit.IsMerge || commit.IsStash) ? MergesAndStashesCannotBePicked
            : commits.Any(commit => commit.IsReachableFromHead) ? SomeAlreadyOn(current)
            : context.IsUnborn ? ToolbarViewModel.UnbornText
            : InProgressReason(context);
        var revertReason = commits.Any(commit => commit.IsMerge || commit.IsStash) ? RevertMergesOneAtATime : InProgressReason(context);

        return Build(
            Header(CommitsHeader(commits.Count)),
            [
                Item(actions, SquashCommits(commits.Count), () => actions.SquashAsync(shas), SquashReason(context, commits)),
                Item(actions, CherryPickSeveralOnto(commits.Count, current), () => actions.CherryPickSeveralAsync(shas), pickReason),
                Item(actions, RevertSeveralLabel(commits.Count), () => actions.RevertSeveralAsync(shas), revertReason),
            ],
            [
                Item(actions, CopyShas, () => actions.CopyShasAsync(shas)),
            ]);
    }

    /// <summary>
    /// A stash: its graph row, its left-panel row or its <c>stash@{n}</c> label (4b): header
    /// <c>stash@{0} On main: Experiment</c>; apply, pop, drop… — copy SHA. <paramref name="stashSha"/> is the stash's commit.
    /// Apply and pop are left to git, also during an operation in progress (D119).
    /// </summary>
    public static MenuModel ForStash(RepositoryMenuContext context, string stashSha, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stashSha);
        ArgumentNullException.ThrowIfNull(actions);
        var header = context.FindStash(stashSha) is { } stash
            ? StashHeader(stash.Name, stash.Message)
            : CommitHeader(stashSha, context.FindSelected(stashSha)?.Subject ?? string.Empty);

        return Build(
            Header(header),
            [
                Item(actions, ApplyStash, () => actions.ApplyStashAsync(stashSha)),
                Item(actions, PopStash, () => actions.PopStashAsync(stashSha), command: IsNewestStash(context, stashSha) ? AppCommand.Pop : null),
                Item(actions, DropStash, () => actions.DropStashAsync(stashSha), isDestructive: true),
            ],
            [
                Item(actions, CopySha, () => actions.CopyShaAsync(stashSha)),
            ]);
    }

    /// <summary>
    /// The working-changes row (4b): header "Working changes"; stage all, unstage all, discard all
    /// changes… — stash changes…, create branch here… — show in folder.
    /// </summary>
    public static MenuModel ForWorkingChanges(RepositoryMenuContext context, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var hasUnstaged = context.Status.Unstaged.Count > 0;
        var hasStaged = context.Status.Staged.Count > 0;

        return Build(
            Header(WorkingChangesHeader),
            [
                Item(actions, StageAll, actions.StageAllAsync, hasUnstaged ? null : NothingToStage, command: AppCommand.StageAll),
                Item(actions, UnstageAll, actions.UnstageAllAsync, hasStaged ? null : NothingStaged, command: AppCommand.UnstageAll),
                Item(actions, DiscardAllChanges, actions.DiscardAllAsync, hasUnstaged ? null : NothingToDiscard, isDestructive: true),
            ],
            [
                StashItem(context, actions),
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(null), context.IsUnborn ? ToolbarViewModel.UnbornText : null, command: AppCommand.CreateBranch),
            ],
            [
                .. ReflogItems(context, actions),
                Item(actions, ShowInFolder, () => actions.ShowInFolderAsync(null)),
            ]);
    }

    /// <summary>
    /// A local branch, its left-panel row or its graph label (4b): header <c>&lt;b&gt;</c>; check out,
    /// merge (both), pull or fast-forward — push, force push… — create branch here…, create tag
    /// here…, rename…, delete… — set or change upstream…, stop tracking — copy branch name.
    /// <paramref name="branchRef"/> is <c>refs/heads/&lt;b&gt;</c>.
    /// </summary>
    public static MenuModel ForLocalBranch(RepositoryMenuContext context, string branchRef, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(branchRef);
        ArgumentNullException.ThrowIfNull(actions);
        var branch = context.FindRef(branchRef);
        var name = branch?.Name ?? ShortName(branchRef);
        if (branch is null)
        {
            return Build(Header(name));
        }

        var isCurrent = IsCurrent(context, branch);
        var upstream = RepositoryMenuContext.UpstreamText(branch);
        var hasUpstream = upstream is not null;
        var hasRemoteUpstream = hasUpstream && !branch.HasLocalUpstream;
        var current = context.CurrentName;
        var mergeReason = MergeReason(context);

        MenuItem? pullOrFastForward = null;
        MenuItem? pullRebase = null;
        if (hasUpstream && isCurrent)
        {
            pullOrFastForward = AsToolbar(Item(actions, PullFrom(upstream!), actions.PullAsync, command: AppCommand.Pull), context.Toolbar.Pull);

            // docs/phase-4-design.md section 6: enabled exactly as the toolbar's Pull (D90).
            pullRebase = AsToolbar(Item(actions, PullFromRebase(upstream!), actions.PullRebaseAsync), context.Toolbar.Pull);
        }
        else if (hasUpstream)
        {
            var reason = branch.UpstreamGone ? NoLongerExists(upstream!)
                : branch.Behind == 0 && branch.Ahead == 0 ? UpToDateWith(name, upstream!)
                : branch.Ahead > 0 ? HasCommitsThat(name, upstream!)
                : context.OtherWorktreeOf(branchRef) is { } path ? CheckedOutInAnotherWorktree(name, path)
                : null;
            var target = branch.UpstreamRef ?? string.Empty;
            pullOrFastForward = Item(actions, FastForwardTo(name, upstream!), () => actions.FastForwardBranchAsync(branchRef, target), reason);
        }

        MenuItem? push = null;
        MenuItem? forcePush = null;
        if (!branch.HasLocalUpstream)
        {
            var pushReason = context.HasRemotes ? null : ToolbarViewModel.AddRemoteText;
            push = Item(actions, hasRemoteUpstream ? PushTo(name, upstream!) : PushWithDialog(name), () => actions.PushBranchAsync(branchRef), pushReason, command: isCurrent ? AppCommand.Push : null);
            if (hasRemoteUpstream)
            {
                forcePush = Item(
                    actions,
                    ForcePushLabel(name),
                    () => actions.ForcePushAsync(branchRef),
                    branch.UpstreamGone ? NoLongerExistsPushNormally(upstream!) : pushReason,
                    isDestructive: true);
            }
        }

        // Only a branch one ref of one remote fills can be tracked (docs/phase-6-design.md 10.4 #11):
        // git refuses R1 and R3, and R2 would get git's first candidate.
        var hasRemoteBranches = context.RemoteBranches.Any(remoteBranch => context.Refs.SourceOf(remoteBranch).IsBranch);
        var onto = new RebaseTarget(branch.TargetSha, name);
        return Build(
            Header(name),
            [
                isCurrent ? null : Item(actions, CheckOutName(name), () => actions.CheckoutAsync(branchRef)),
                isCurrent ? null : Item(actions, MergeInto(name, current), () => actions.MergeAsync(branchRef, MergeMode.Default), mergeReason),
                isCurrent ? null : Item(actions, MergeIntoWithCommit(name, current), () => actions.MergeAsync(branchRef, MergeMode.NoFastForward), mergeReason),
                isCurrent ? null : RebaseOntoItem(context, onto, actions),
                isCurrent ? null : InteractiveRebaseOntoItem(context, onto, actions),
                pullOrFastForward,
                pullRebase,
            ],
            [push, forcePush],
            [
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(branchRef)),
                Item(actions, CreateTagHere, () => actions.CreateTagAsync(branchRef)),
                Item(actions, RenameLabel(name), () => actions.RenameBranchAsync(branchRef)),
                Item(actions, DeleteLabel(name), () => actions.DeleteBranchAsync(branchRef), isCurrent ? CannotDeleteCurrentBranch : null, isDestructive: true),
            ],
            [
                hasUpstream
                    ? Item(actions, ChangeUpstream, () => actions.SetUpstreamAsync(branchRef))
                    : Item(actions, SetUpstream, () => actions.SetUpstreamAsync(branchRef), hasRemoteBranches ? null : NoRemoteBranchesToTrack),
                hasUpstream ? Item(actions, StopTracking(upstream!), () => actions.UnsetUpstreamAsync(branchRef)) : null,
            ],
            [
                isCurrent ? null : CompareItem(context, branch, actions),
                BranchReflogItem(context, branch, actions),
                .. HideAndSoloItems(context, branch, actions),
            ],
            [
                Item(actions, CopyBranchName, () => actions.CopyRefNameAsync(branchRef)),
            ]);
    }

    /// <summary>
    /// A remote branch, its row or its label (4b): header <c>&lt;r&gt;/&lt;b&gt;</c>; check out,
    /// merge (both) — create branch here…, create tag here… — delete on the remote… — copy branch
    /// name. <paramref name="remoteBranchRef"/> is <c>refs/remotes/&lt;r&gt;/&lt;b&gt;</c>. How its
    /// remote fetches it (<see cref="RepoRefs.SourceOf(GitRef)"/>, docs/phase-6-design.md 10.4 #6):
    /// its checkout goes into its local name (10.6), disabled while HEAD is on it, and with R1 or
    /// R3 when it has none; its deletion names the branch on the remote, disabled with R1 to R4
    /// unless it is a branch that one ref of one remote fills.
    /// </summary>
    public static MenuModel ForRemoteBranch(RepositoryMenuContext context, string remoteBranchRef, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(remoteBranchRef);
        ArgumentNullException.ThrowIfNull(actions);
        var branch = context.FindRef(remoteBranchRef);
        if (branch is null)
        {
            return Build(Header(ShortName(remoteBranchRef)));
        }

        var name = branch.Name;
        var source = context.Refs.SourceOf(branch);
        var local = context.Refs.LocalNameOf(branch);
        var current = context.CurrentName;
        var mergeReason = MergeReason(context);
        var checkoutReason = local is null ? RemoteSourceReason(source, name, needsBranch: false)
            : string.Equals(context.Head.BranchName, local, StringComparison.Ordinal) ? AlreadyCheckedOut(local)
            : null;
        var onto = new RebaseTarget(branch.TargetSha, name);

        // The deletion names the branch on its remote; any other ref keeps phase 5's label.
        var (deleted, remote) = source is { IsBranch: true, Branch: { } onRemote, Remote: { } mapped }
            ? (onRemote, mapped)
            : (NameOnItsRemote(branch), branch.RemoteName ?? string.Empty);

        return Build(
            Header(name),
            [
                Item(actions, CheckOutName(name), () => actions.CheckoutAsync(remoteBranchRef), checkoutReason),
                Item(actions, MergeInto(name, current), () => actions.MergeAsync(remoteBranchRef, MergeMode.Default), mergeReason),
                Item(actions, MergeIntoWithCommit(name, current), () => actions.MergeAsync(remoteBranchRef, MergeMode.NoFastForward), mergeReason),
                RebaseOntoItem(context, onto, actions),
                InteractiveRebaseOntoItem(context, onto, actions),
            ],
            [
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(remoteBranchRef)),
                Item(actions, CreateTagHere, () => actions.CreateTagAsync(remoteBranchRef)),
            ],
            [
                Item(actions, DeleteOnRemoteLabel(deleted, remote), () => actions.DeleteRemoteBranchAsync(remoteBranchRef), RemoteSourceReason(source, name, needsBranch: true), isDestructive: true),
            ],
            [
                CompareItem(context, branch, actions),
                .. HideAndSoloItems(context, branch, actions),
            ],
            [
                Item(actions, CopyBranchName, () => actions.CopyRefNameAsync(remoteBranchRef)),
            ]);
    }

    /// <summary>
    /// A remote's row in the left panel (4b): header <c>&lt;r&gt;</c>; fetch — change URL…, rename…,
    /// remove… — copy URL, manage remotes….
    /// </summary>
    public static MenuModel ForRemote(RepositoryMenuContext context, string remote, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(
            Header(remote),
            [
                Item(actions, FetchRemote(remote), () => actions.FetchAsync(remote)),
            ],
            [
                Item(actions, ChangeUrl, () => actions.ShowEditRemoteAsync(remote)),
                Item(actions, RenameLabel(remote), () => actions.ShowEditRemoteAsync(remote)),
                Item(actions, RemoveLabel(remote), () => actions.RemoveRemoteAsync(remote), isDestructive: true),
            ],
            [
                RemoteFilterItem(context, remote, actions),
            ],
            [
                Item(actions, CopyUrl, () => actions.CopyRemoteUrlAsync(remote)),
                Item(actions, ManageRemotes, actions.ManageRemotesAsync),
            ]);
    }

    /// <summary>
    /// A tag, its row or its label (4b): header <c>&lt;t&gt;</c>; check out, create branch here… —
    /// push to a remote, delete…, delete on a remote… — copy tag name. <paramref name="tagRef"/> is <c>refs/tags/&lt;t&gt;</c>.
    /// </summary>
    public static MenuModel ForTag(RepositoryMenuContext context, string tagRef, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tagRef);
        ArgumentNullException.ThrowIfNull(actions);
        var tag = context.FindRef(tagRef);
        var name = tag?.Name ?? ShortName(tagRef);
        if (tag is null)
        {
            return Build(Header(name));
        }

        var detachedHere = context.IsDetached && string.Equals(context.Head.Sha, tag.TargetSha, StringComparison.Ordinal);
        var remoteReason = context.HasRemotes ? null : ToolbarViewModel.AddRemoteText;
        var oneRemote = context.Remotes.Count == 1 ? context.Remotes[0] : null;

        return Build(
            Header(name),
            [
                Item(actions, CheckOutName(name), () => actions.CheckoutAsync(tagRef), detachedHere ? AlreadyAtThisCommit : null),
                Item(actions, CreateBranchHere, () => actions.CreateBranchAsync(tagRef)),
            ],
            [
                Item(actions, oneRemote is null ? PushWithDialog(name) : PushTagTo(name, oneRemote), () => actions.PushTagAsync(tagRef), remoteReason),
                Item(actions, DeleteLabel(name), () => actions.DeleteTagAsync(tagRef), isDestructive: true),
                Item(
                    actions,
                    oneRemote is null ? DeleteTagOnARemote(name) : DeleteTagOnRemote(name, oneRemote),
                    () => actions.DeleteRemoteTagAsync(tagRef),
                    remoteReason,
                    isDestructive: true),
            ],
            [
                CompareItem(context, tag, actions),
            ],
            [
                Item(actions, CopyTagName, () => actions.CopyRefNameAsync(tagRef)),
            ]);
    }

    /// <summary>
    /// A left-panel section header (4b), headed by the section's title: "Local branches" create
    /// branch…; "Remotes" add remote…, fetch all remotes, manage remotes…; "Tags" create tag at
    /// HEAD…; "Stashes" stash changes…. Null for "Pull requests", which has no menu (D92).
    /// </summary>
    public static MenuModel? ForSection(RepositoryMenuContext context, RefSection section, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var unborn = context.IsUnborn ? ToolbarViewModel.UnbornText : null;
        return section switch
        {
            RefSection.LocalBranches => Build(
                Header(LeftPanelViewModel.LocalBranchesTitle),
                [
                    Item(actions, CreateBranch, () => actions.CreateBranchAsync(null), unborn, command: AppCommand.CreateBranch),
                    .. ReflogItems(context, actions),
                    .. SectionFilterItems(context, actions),
                ]),
            RefSection.Remotes => Build(
                Header(LeftPanelViewModel.RemotesTitle),
                [
                    Item(actions, AddRemote, actions.ShowAddRemoteAsync),
                    Item(actions, FetchAllRemotes, actions.FetchAllAsync, context.HasRemotes ? null : ToolbarViewModel.NoRemotesText, command: AppCommand.Fetch),
                    Item(actions, ManageRemotes, actions.ManageRemotesAsync),
                    .. SectionFilterItems(context, actions),
                ]),
            RefSection.Tags => Build(
                Header(LeftPanelViewModel.TagsTitle),
                [Item(actions, CreateTagAtHead, () => actions.CreateTagAsync(null), unborn)]),
            RefSection.Stashes => Build(
                Header(LeftPanelViewModel.StashesTitle),
                [StashItem(context, actions)]),
            _ => null,
        };
    }

    /// <summary>
    /// A file in the stage panel's unstaged list (4b): header the file name; stage (or mark as
    /// resolved), discard changes… — ignore the file, ignore its extension — open file, show in
    /// folder, copy path.
    /// </summary>
    public static MenuModel ForUnstagedFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(
            Header(file.FileName),
            [
                file.IsConflicted ? Item(actions, ResolveConflict, () => ResolveConflictAsync(actions, file.File)) : null,
                file.IsConflicted && OffersMergeTool(context, file.Path)
                    ? Item(actions, OpenInMergeTool, () => actions.OpenMergeToolAsync(file.Path), context.MergeTool is null ? OperationTexts.SetMergeTool : null)
                    : null,
                Item(actions, file.IsConflicted ? MarkAsResolved : Stage, () => actions.StageFilesAsync([file.File])),
                Item(actions, DiscardChanges, () => actions.DiscardFilesAsync([file.File]), DiscardReason(file)),
            ],
            IgnoreItems(file, actions),
            FileHistoryItems(context, file, DiffSide.Unstaged, actions),
            FileItems(file, context, actions));
    }

    /// <summary>
    /// Whether the conflicted file's menu offers "Open in merge tool" (docs/phase-4-design.md 7.1,
    /// 11.9): for text and binary conflicts only, both sides modified or both added and neither a
    /// submodule nor a symbolic link, because git never runs the tool for the others (0.4). A
    /// path the status does not describe is offered, and git decides.
    /// </summary>
    public static bool OffersMergeTool(RepositoryMenuContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(path);
        var entry = context.Status.Conflicts.FirstOrDefault(conflict => string.Equals(conflict.Path, path, StringComparison.Ordinal));
        return entry is null
            || (entry.Kind is ConflictKind.BothModified or ConflictKind.BothAdded
                && !entry.IsSubmodule
                && entry.OursMode != SymbolicLinkMode
                && entry.TheirsMode != SymbolicLinkMode);
    }

    /// <summary>A symbolic link's mode in git's index (0.4).</summary>
    private const string SymbolicLinkMode = "120000";

    private static Task ResolveConflictAsync(IRepositoryActions actions, ChangedFile file)
    {
        actions.ResolveConflict(file);
        return Task.CompletedTask;
    }

    /// <summary>A file in the staged list (4b): header the file name; unstage — open file, show in folder, copy path.</summary>
    public static MenuModel ForStagedFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(
            Header(file.FileName),
            [Item(actions, Unstage, () => actions.UnstageFilesAsync([file.File]))],
            FileHistoryItems(context, file, DiffSide.Staged, actions),
            FileItems(file, context, actions));
    }

    /// <summary>A file in a commit's changed files (4b): header the file name; open file, show in folder, copy path.</summary>
    public static MenuModel ForCommitFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(Header(file.FileName), FileHistoryItems(context, file, DiffSide.Commit, actions), FileItems(file, context, actions));
    }

    /// <summary>
    /// A right-click on the diff view's text or header (4b): header the file name; the selected
    /// lines' and the hunk's actions (<paramref name="lines"/>), the file's own, the two ignore
    /// items for an untracked file, then open file, show in folder, copy path. A commit's diff has
    /// the last three only.
    /// </summary>
    public static MenuModel ForDiff(RepositoryMenuContext context, DiffMenuTarget diff, IRepositoryActions actions, IDiffMenuActions lines)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(lines);
        var file = diff.File;
        var header = Header(file.FileName);
        var selected = diff.HasSelectedLines && diff.ActsOnLines;
        var hunk = diff.ActsOnLines ? diff.Hunk : null;

        switch (diff.Side)
        {
            case DiffSide.Unstaged:
                return Build(
                    header,
                    [
                        selected ? Item(actions, StageSelectedLines, lines.StageLinesAsync) : null,
                        selected ? Item(actions, DiscardSelectedLines, lines.DiscardLinesAsync) : null,
                    ],
                    [
                        hunk is { } stageHunk ? Item(actions, StageHunk, () => lines.RunHunkActionAsync(stageHunk, LineAction.Stage)) : null,
                        hunk is { } discardHunk ? Item(actions, DiscardHunk, () => lines.RunHunkActionAsync(discardHunk, LineAction.Discard)) : null,
                    ],
                    [
                        Item(actions, file.IsConflicted ? MarkAsResolved : StageFile, () => actions.StageFilesAsync([file.File])),
                        Item(actions, DiscardFile, () => actions.DiscardFilesAsync([file.File]), DiscardReason(file)),
                    ],
                    file.IsUntracked ? IgnoreItems(file, actions) : [],
                    FileHistoryItems(context, file, diff.Side, actions, ofOpenDiff: true),
                    FileItems(file, context, actions));

            case DiffSide.Staged:
                return Build(
                    header,
                    [selected ? Item(actions, UnstageSelectedLines, lines.UnstageLinesAsync) : null],
                    [hunk is { } unstageHunk ? Item(actions, UnstageHunk, () => lines.RunHunkActionAsync(unstageHunk, LineAction.Unstage)) : null],
                    [Item(actions, UnstageFile, () => actions.UnstageFilesAsync([file.File]))],
                    FileHistoryItems(context, file, diff.Side, actions, ofOpenDiff: true),
                    FileItems(file, context, actions));

            default:
                return Build(header, FileHistoryItems(context, file, diff.Side, actions, ofOpenDiff: true), FileItems(file, context, actions));
        }
    }

    /// <summary>
    /// The empty graph area, below the last row or a graph without commits (4b): header the
    /// repository's name; fetch all remotes, pull, push — create branch…, stash changes… — manage
    /// remotes…, enabled as the toolbar's buttons (<see cref="RepositoryMenuContext.Toolbar"/>).
    /// </summary>
    public static MenuModel ForEmptyGraphArea(RepositoryMenuContext context, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var toolbar = context.Toolbar;
        return Build(
            Header(context.RepositoryName),
            [
                AsToolbar(Item(actions, FetchAllRemotes, actions.FetchAllAsync, command: AppCommand.Fetch), toolbar.Fetch),
                AsToolbar(Item(actions, ToolbarPull, actions.PullAsync, command: AppCommand.Pull), toolbar.Pull),
                AsToolbar(Item(actions, ToolbarPush, actions.PushCurrentBranchAsync, command: AppCommand.Push), toolbar.Push),
            ],
            [
                AsToolbar(Item(actions, CreateBranch, () => actions.CreateBranchAsync(null), command: AppCommand.CreateBranch), toolbar.Branch),
                AsToolbar(Item(actions, StashChanges, actions.StashAsync, command: AppCommand.Stash), toolbar.Stash),
            ],
            [
                .. ReflogItems(context, actions),
                Item(actions, ManageRemotes, actions.ManageRemotesAsync),
            ]);
    }

    /// <summary>
    /// The menu of a drop of the branch <paramref name="sourceRef"/> onto <paramref name="targetRef"/>
    /// (4g): header <c>&lt;A&gt; → &lt;B&gt;</c>; onto the current branch merge (both) and
    /// fast-forward; onto another local branch fast-forward and check out and merge; onto a remote
    /// branch push and push and track. <see cref="RepositoryMenuContext.Drop"/> holds what the drop read;
    /// without it a fast-forward is left to git. An entry that would check out, or move, a branch
    /// checked out in another worktree is disabled (2.7, D120).
    /// </summary>
    public static MenuModel ForDrop(RepositoryMenuContext context, string sourceRef, string targetRef, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sourceRef);
        ArgumentNullException.ThrowIfNull(targetRef);
        ArgumentNullException.ThrowIfNull(actions);
        var source = context.FindRef(sourceRef)?.Name ?? ShortName(sourceRef);
        var targetBranch = context.FindRef(targetRef);
        var target = targetBranch?.Name ?? ShortName(targetRef);
        var header = Header(DropHeader(source, target));
        var notAncestor = context.Drop is { TargetIsAncestorOfSource: false } ? HasCommitsThat(target, source) : null;
        var rebaseItems = DropRebaseItems(context, sourceRef, source, targetBranch, target, actions);

        if (targetRef.StartsWith("refs/remotes/", StringComparison.Ordinal))
        {
            // The pushes go to the branch on the remote that fills the target (docs/phase-6-design.md
            // 10.4 #5): disabled with R1 to R4 unless one ref of one remote, a branch, fills it.
            var fetched = targetBranch is not null ? context.Refs.SourceOf(targetBranch) : context.Refs.SourceOf(targetRef);
            var reason = RemoteSourceReason(fetched, target, needsBranch: true);
            return Build(
                header,
                [
                    Item(actions, PushTo(source, target), () => actions.PushBranchToAsync(sourceRef, targetRef, setUpstream: false), reason),
                    Item(actions, PushToAndTrack(source, target), () => actions.PushBranchToAsync(sourceRef, targetRef, setUpstream: true), reason),
                ],
                rebaseItems,
                [DropCompareItem(context, sourceRef, targetRef, actions)]);
        }

        var isCurrent = targetBranch is not null ? IsCurrent(context, targetBranch) : string.Equals(HeadsPrefix + context.Head.BranchName, targetRef, StringComparison.Ordinal);
        if (isCurrent)
        {
            var mergeReason = InProgressReason(context);
            return Build(
                header,
                [
                    Item(actions, MergeInto(source, target), () => actions.MergeAsync(sourceRef, MergeMode.Default), mergeReason),
                    Item(actions, MergeIntoWithCommit(source, target), () => actions.MergeAsync(sourceRef, MergeMode.NoFastForward), mergeReason),
                    Item(actions, FastForwardTo(target, source), () => actions.MergeAsync(sourceRef, MergeMode.FastForwardOnly), notAncestor ?? mergeReason),
                ],
                rebaseItems,
                [DropCompareItem(context, sourceRef, targetRef, actions)]);
        }

        var worktree = context.OtherWorktreeOf(targetRef) is { } path ? CheckedOutInAnotherWorktree(target, path) : null;
        return Build(
            header,
            [
                Item(actions, FastForwardTo(target, source), () => actions.FastForwardBranchAsync(targetRef, sourceRef), notAncestor ?? worktree),
                Item(actions, CheckOutAndMergeLabel(target, source), () => actions.CheckoutAndMergeAsync(targetRef, sourceRef), worktree),
            ],
            rebaseItems,
            [DropCompareItem(context, sourceRef, targetRef, actions)]);
    }

    /// <summary>
    /// A drop's rebase items (docs/phase-4-design.md 1.1, 2.1, 7.1, 14.3), which change the source
    /// A: none for a remote source (a remote branch is never rebased); for the current branch
    /// "Rebase {A} onto {B}" and "Interactive rebase of {A} onto {B}…"; for another local branch
    /// "Check out {A} and rebase it onto {B}" and "… interactively…", which check A out first and
    /// so are not disabled while HEAD is detached. Reasons, the object's own first: A already
    /// contains B (<see cref="DropFacts.SourceContainsTarget"/>; not for the interactive items), A
    /// checked out in another worktree (the checkout items), then HEAD's state.
    /// </summary>
    private static MenuItem?[] DropRebaseItems(RepositoryMenuContext context, string sourceRef, string source, GitRef? targetBranch, string target, IRepositoryActions actions)
    {
        if (!sourceRef.StartsWith(HeadsPrefix, StringComparison.Ordinal) || targetBranch is null)
        {
            return [];
        }

        var onto = new RebaseTarget(targetBranch.TargetSha, target);
        var upToDate = context.Drop?.SourceContainsTarget == true ? UpToDateWith(source, target) : null;
        var sourceIsCurrent = string.Equals(HeadsPrefix + context.Head.BranchName, sourceRef, StringComparison.Ordinal);
        if (sourceIsCurrent)
        {
            return
            [
                Item(actions, RebaseOntoLabel(source, target), () => actions.RebaseAsync(onto), upToDate ?? RebaseStateReason(context)),
                Item(
                    actions,
                    InteractiveRebaseOnto(source, target),
                    () => actions.OpenInteractiveRebaseAsync(new InteractiveRebaseRequest(onto, sourceRef, FromHere: false, CheckoutFirst: false)),
                    RebaseStateReason(context)),
            ];
        }

        var worktree = context.OtherWorktreeOf(sourceRef) is { } path ? CheckedOutInAnotherWorktree(source, path) : null;
        var state = context.IsUnborn ? ToolbarViewModel.UnbornText : OperationOrConflictsReason(context);
        return
        [
            Item(actions, CheckOutAndRebaseLabel(source, target), () => actions.CheckoutAndRebaseAsync(sourceRef, onto), upToDate ?? worktree ?? state),
            Item(
                actions,
                CheckOutAndRebaseInteractively(source, target),
                () => actions.OpenInteractiveRebaseAsync(new InteractiveRebaseRequest(onto, sourceRef, FromHere: false, CheckoutFirst: true)),
                worktree ?? state),
        ];
    }

    /// <summary>
    /// A branch's "Rebase {cur} onto {b}" (1.1): disabled when HEAD already reaches the branch's
    /// tip (<see cref="RepositoryMenuContext.TipsOnHead"/>), then by HEAD's state.
    /// </summary>
    private static MenuItem RebaseOntoItem(RepositoryMenuContext context, RebaseTarget onto, IRepositoryActions actions)
    {
        var current = context.CurrentName;
        var reason = context.TipsOnHead.Contains(onto.Sha) ? UpToDateWith(current, onto.Name) : RebaseStateReason(context);
        return Item(actions, RebaseOntoLabel(current, onto.Name), () => actions.RebaseAsync(onto), reason);
    }

    /// <summary>A branch's "Interactive rebase of {cur} onto {b}…" (2.1): only HEAD's state disables it (1.1's reasons 3 to 5).</summary>
    private static MenuItem InteractiveRebaseOntoItem(RepositoryMenuContext context, RebaseTarget onto, IRepositoryActions actions)
    {
        var current = context.CurrentName;
        var branchRef = context.CurrentBranch?.FullName ?? HeadsPrefix + current;
        return Item(
            actions,
            InteractiveRebaseOnto(current, onto.Name),
            () => actions.OpenInteractiveRebaseAsync(new InteractiveRebaseRequest(onto, branchRef, FromHere: false, CheckoutFirst: false)),
            RebaseStateReason(context));
    }

    /// <summary>
    /// "Squash {N} commits…" (docs/phase-4-design.md section 3): its reasons in order, or null when
    /// the selected commits are a first-parent chain on HEAD's linear line without a merge or the
    /// root, HEAD is on a branch, and nothing is in progress or conflicted.
    /// </summary>
    private static string? SquashReason(RepositoryMenuContext context, IReadOnlyList<CommitMenuInfo> commits)
    {
        var current = context.CurrentName;
        if (commits.Any(commit => commit.IsMerge || commit.IsStash))
        {
            return MergeCommitsCannotBeSquashed;
        }

        if (commits.Any(commit => !commit.IsReachableFromHead))
        {
            return OnlyCommitsOfCanBeSquashed(current);
        }

        if (!IsFirstParentChain(commits))
        {
            return SelectCommitsThatFollowOneAnother;
        }

        if (commits.Any(commit => commit.HeadLineDepth is null))
        {
            return MergeLiesBetweenTheseCommitsAnd(current);
        }

        if (commits.Any(commit => commit.Parents.Count == 0))
        {
            return FirstCommitCannotBeSquashed;
        }

        return context.IsDetached ? CheckOutABranchToSquash : OperationOrConflictsReason(context);
    }

    /// <summary>
    /// Whether <paramref name="commits"/> follow one another: walking first parents from the one
    /// no other selected commit has as its first parent reaches every selected commit.
    /// </summary>
    private static bool IsFirstParentChain(IReadOnlyList<CommitMenuInfo> commits)
    {
        var bySha = commits.GroupBy(commit => commit.Sha, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var firstParents = new HashSet<string>(bySha.Values.Where(commit => commit.Parents.Count > 0).Select(commit => commit.Parents[0]), StringComparer.Ordinal);
        var newest = bySha.Values.Where(commit => !firstParents.Contains(commit.Sha)).ToList();
        if (newest.Count != 1)
        {
            return false;
        }

        var reached = 1;
        var walk = newest[0];
        while (walk.Parents.Count > 0 && bySha.TryGetValue(walk.Parents[0], out var parent) && reached < bySha.Count)
        {
            walk = parent;
            reached++;
        }

        return reached == bySha.Count;
    }

    /// <summary>
    /// 1.1's reasons 3 to 5 for the rebase, interactive rebase and squash items: HEAD detached
    /// ("Check out a branch to rebase"), unborn, an operation in progress, conflicts without one.
    /// </summary>
    private static string? RebaseStateReason(RepositoryMenuContext context) =>
        context.IsDetached ? CheckOutABranchToRebase
        : context.IsUnborn ? ToolbarViewModel.UnbornText
        : OperationOrConflictsReason(context);

    /// <summary>"Finish or abort the {kind} first" while an operation is in progress, "Resolve the conflicts first" while files conflict without one (D119); null otherwise.</summary>
    private static string? OperationOrConflictsReason(RepositoryMenuContext context) =>
        context.InProgress.IsActive ? FinishOrAbort(context.InProgress.Kind)
        : context.Status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted) ? ResolveTheConflictsFirst
        : null;

    /// <summary>
    /// A menu from its header and groups of items (4a, D114): the header first; the items of each
    /// group in order, null items (left out) dropped; one separator between two groups that keep
    /// items, so that no separator follows the header, ends the menu, or follows another.
    /// </summary>
    public static MenuModel Build(MenuHeader header, params IEnumerable<MenuItem?>[] groups)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(groups);
        var entries = new List<MenuEntry> { header };
        foreach (var group in groups)
        {
            var items = group.OfType<MenuItem>().ToList();
            if (items.Count == 0)
            {
                continue;
            }

            if (entries.Count > 1)
            {
                entries.Add(new MenuSeparator());
            }

            entries.AddRange(items);
        }

        return new MenuModel(entries);
    }

    /// <summary>
    /// An item that starts <paramref name="action"/> when invoked: enabled when
    /// <paramref name="disabledReason"/> is null, otherwise disabled with that reason as its
    /// tooltip. The action's task is not awaited (the menu has closed); a failure that gets past
    /// the operation's own handling is logged to <see cref="IRepositoryActions.Log"/>.
    /// <paramref name="command"/> is the keyed command it runs on the same object (6.7), or null.
    /// </summary>
    public static MenuItem Item(IRepositoryActions actions, string label, Func<Task> action, string? disabledReason = null, bool isDestructive = false, AppCommand? command = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(action);
        return new MenuItem(label, () => Start(actions, label, action), disabledReason is null, disabledReason, isDestructive) { Command = command };
    }

    /// <summary>The empty graph area's "Pull" (4b): the toolbar's Pull.</summary>
    private const string ToolbarPull = "Pull";

    /// <summary>The empty graph area's "Push" (4b): the toolbar's Push.</summary>
    private const string ToolbarPush = "Push";

    private static MenuHeader Header(string text, string? toolTip = null) => new(text, toolTip);

    /// <summary>The short name of a full ref, when the context does not know it: <c>refs/heads/x</c> → <c>x</c>.</summary>
    private static string ShortName(string fullRef)
    {
        foreach (var prefix in new[] { HeadsPrefix, "refs/remotes/", "refs/tags/" })
        {
            if (fullRef.StartsWith(prefix, StringComparison.Ordinal))
            {
                return fullRef[prefix.Length..];
            }
        }

        return fullRef;
    }

    /// <summary>
    /// A remote branch's name less <c>&lt;its remote&gt;/</c>, as phase 5 named the branch on the
    /// remote (<c>origin/pr/1</c> → <c>pr/1</c>): the deletion's label for a ref that no branch of
    /// one ref fills (docs/phase-6-design.md 10.4 #6).
    /// </summary>
    private static string NameOnItsRemote(GitRef remoteBranch)
    {
        var prefix = remoteBranch.RemoteName + "/";
        return remoteBranch.RemoteName is not null && remoteBranch.Name.StartsWith(prefix, StringComparison.Ordinal)
            ? remoteBranch.Name[prefix.Length..]
            : remoteBranch.Name;
    }

    private static bool IsCurrent(RepositoryMenuContext context, GitRef branch) =>
        branch.Kind == RefKind.LocalBranch && (branch.IsHead || string.Equals(context.Head.BranchName, branch.Name, StringComparison.Ordinal));

    /// <summary>"Finish or abort the &lt;kind&gt; first" while an operation is in progress (2.8, 2.12, 2.14); null otherwise.</summary>
    private static string? InProgressReason(RepositoryMenuContext context) =>
        context.InProgress.IsActive ? FinishOrAbort(context.InProgress.Kind) : null;

    /// <summary>A merge (2.8): HEAD must be on a branch, and nothing may be in progress.</summary>
    private static string? MergeReason(RepositoryMenuContext context) =>
        context.IsDetached ? CheckOutABranchToMergeInto : InProgressReason(context);

    /// <summary>"Stash changes…", enabled as the toolbar's Stash (2.24, D119), with its key (6.7).</summary>
    private static MenuItem StashItem(RepositoryMenuContext context, IRepositoryActions actions) =>
        Item(actions, StashChanges, actions.StashAsync, ToolbarViewModel.StashReason(context.Head, context.Status), command: AppCommand.Stash);

    /// <summary>Whether <paramref name="stashSha"/> is <c>stash@{0}</c>, whose Pop is the toolbar's (6.7).</summary>
    private static bool IsNewestStash(RepositoryMenuContext context, string stashSha) =>
        context.FindStash(stashSha) is { Index: 0 };

    /// <summary>An item enabled as a toolbar button is (4b's empty graph area): disabled with the button's reason, or without one before the refs are read.</summary>
    private static MenuItem AsToolbar(MenuItem item, ToolbarAvailability availability) =>
        availability.IsEnabled ? item : item with { IsEnabled = false, DisabledReason = availability.Reason };

    private static string? DiscardReason(FileMenuTarget file) =>
        file.IsConflicted ? ResolveTheConflictFirst
        : file.IsNestedRepository ? NestedRepositoryNotDiscarded
        : null;

    /// <summary>"Ignore &lt;file name&gt;" and, with an extension, "Ignore all *.&lt;ext&gt; files" (2.30), disabled on a tracked file.</summary>
    private static MenuItem?[] IgnoreItems(FileMenuTarget file, IRepositoryActions actions)
    {
        var reason = file.IsUntracked ? null : OnlyUntrackedCanBeIgnored;
        return
        [
            Item(actions, IgnoreName(file.FileName), () => actions.IgnoreFileAsync(file.Path), reason),
            file.Extension is { } extension ? Item(actions, IgnoreExtension(extension), () => actions.IgnoreExtensionAsync(file.Path), reason) : null,
        ];
    }

    /// <summary>
    /// Open file, show in folder, copy path (2.32); with an editor chosen, Open in editor after
    /// Open file (docs/phase-6-design.md 12.1, <see cref="RepositoryMenuContext.Editor"/>), never asking.
    /// </summary>
    private static MenuItem?[] FileItems(FileMenuTarget file, RepositoryMenuContext context, IRepositoryActions actions) =>
    [
        Item(actions, OpenFile, () => actions.OpenFileAsync(file.Path), file.ExistsInWorkingTree ? null : NotInWorkingTree),
        context.Editor is null ? null : Item(actions, OpenInEditor, () => actions.OpenInEditorAsync(file.Path), file.ExistsInWorkingTree ? null : NotInWorkingTree),
        Item(actions, ShowInFolder, () => actions.ShowInFolderAsync(file.Path)),
        Item(actions, CopyPath, () => actions.CopyPathAsync(file.Path)),
    ];

    private static void Start(IRepositoryActions actions, string label, Func<Task> action)
    {
        var log = actions.Log ?? NullAppLog.Instance;
        Task task;
        try
        {
            task = action();
        }
        catch (Exception ex)
        {
            log.Error($"The menu item \"{label}\" failed.", ex);
            return;
        }

        _ = task.ContinueWith(
            failed => log.Error($"The menu item \"{label}\" failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
