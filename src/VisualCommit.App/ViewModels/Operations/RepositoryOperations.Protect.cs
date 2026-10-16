// C12, protected changes (docs/phase-5-design.md section 9, D153, D166, D179; the report's part P):
// the automatic stash before a checkout of a commit the current branch reaches (9.1, 9.2), the
// refusals a checkout makes before git (9.3's first table), the refusal message of every checkout
// path and of the merges, picks and reverts that git refuses for the same reason (9.3), and the
// check a stash's apply or pop makes before git (9.3's last row). The checkout paths themselves are
// in RepositoryOperations.Branches.cs, the pull's in RepositoryOperations.Remotes.cs, the stash's
// in RepositoryOperations.StashTags.cs. Built by work package WP4; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsProtectTests.cs.

using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    // ---- 9.1: which checkouts stash first -----------------------------------------------------------

    /// <summary>
    /// Whether a checkout of <paramref name="target"/> is 9.1's first row, the automatic stash's,
    /// with HEAD as the click sees it (<paramref name="seen"/>): a commit or a tag (not a branch),
    /// HEAD on a branch at another commit, and the branch reaches the target through any parent.
    /// The graph answers for a loaded row of HEAD's graph (a date-ordered log loads every commit
    /// between HEAD and a loaded ancestor); otherwise <c>git merge-base --is-ancestor</c>. Either
    /// operation decides again when HEAD moved meanwhile (<see cref="RefuseIfAutoStashTargetMovedAsync"/>,
    /// <see cref="RefuseIfNowAutoStashTargetAsync"/>). Null for every other checkout, which git
    /// runs as before: HEAD's own commit too (D179, the report's PD7), a detached or unborn HEAD,
    /// and a commit the branch does not reach.
    /// </summary>
    private async Task<AutoStashTarget?> AutoStashTargetAsync(string target, HeadState? seen)
    {
        if (IsBranchTarget(target) || seen is not { BranchName: not null, Sha: { } head })
        {
            return null;
        }

        var commit = IsFullSha(target)
            ? target
            : FindRef(target)?.TargetSha is { } tagged && IsFullSha(tagged) && FindCommit(tagged) is not null ? tagged
            : await Git.ResolveAsync(target + "^{commit}");
        if (commit is null || string.Equals(commit, head, StringComparison.Ordinal))
        {
            return null;
        }

        var graph = Repository.Graph;
        var reached = graph.IndexOf(commit) >= 0 && string.Equals(graph.Refs.Head.Sha, head, StringComparison.Ordinal)
            ? graph.IsReachableFromHead(commit)
            : await Git.IsAncestorAsync(commit, "HEAD");
        return reached ? new AutoStashTarget(commit, seen) : null;
    }

    // ---- 9.2: the automatic stash -------------------------------------------------------------------

    /// <summary>
    /// The automatic stash's checkout (9.2, D153 (2), D179): one local operation of several steps,
    /// titled as the checkout (<paramref name="text"/>), which never asks. Inside it: HEAD where
    /// the click saw it, or still on a branch that reaches the target (D124); refused before any
    /// write while an operation is in progress or files conflict; every change stashed with the
    /// untracked files when there is any (<c>git stash push --include-untracked --message "Auto
    /// stash before checking out {name}"</c>, without <c>GIT_LITERAL_PATHSPECS</c>, D108); then
    /// <c>git switch --detach --no-overwrite-ignore &lt;sha&gt;</c>. A refused checkout pops the
    /// stash back (<c>git stash pop --index</c>, D91's fallback) and shows 9.3's message without
    /// "Stash changes and check out"; a stash git saved but could not finish puts back the
    /// untracked files git had deleted and checks nothing out. The result names the stash, which
    /// nothing pops later (D153 (3)); the undo history records the checkout with the stash
    /// (<see cref="UndoScope.AutoStashCheckout"/>, D166). With <paramref name="checkExists"/> (a
    /// reflog row's or a backup's commit, docs/phase-5-design.md 5.2) it is refused first when the
    /// commit no longer exists.
    /// </summary>
    private async Task CheckoutWithAutoStashAsync(string name, AutoStashTarget target, OperationText text, bool checkExists)
    {
        var message = OperationTexts.AutoStashMessage(name);

        // The status bar's first text is the stash's when the last status read has changes for it.
        var running = Status.HasChanges ? text with { RunningText = OperationTexts.StashingYourChanges } : text;
        FailureDisplay? display = null;
        await RunAsync(
            running,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                var options = NetworkOptions.From(context);

                // A reflog row's or a backup's commit (design 5.2): it still exists.
                if (checkExists && await RefuseIfCommitGoneAsync(text, target.Commit) is { } gone)
                {
                    return gone;
                }

                // 1. HEAD and the status as they are now.
                if (await RefuseIfAutoStashTargetMovedAsync(text, target) is { } moved)
                {
                    return moved;
                }

                var status = await Git.ReadStatusAsync();
                if (RefusalOf(status) is { } refusal)
                {
                    // A stash during a merge without conflicts would delete MERGE_HEAD (0.7).
                    display = new FailureDisplay(OperationTexts.CheckoutBlockedTitle, refusal);
                    return Refused(text, refusal);
                }

                // 2. and 3. Every change, untracked files included; ignored files are none (PD8).
                string? stash = null;
                if (status.HasChanges)
                {
                    context.SetRunningText(OperationTexts.StashingYourChanges);
                    var before = await Git.ResolveAsync("refs/stash");
                    try
                    {
                        stash = await Git.StashAsync(message, includeUntracked: true, options);
                    }
                    catch (GitException failure)
                    {
                        (display, var report) = await StashNotFinishedAsync(text, name, failure, before, options);
                        return report;
                    }

                    UndoCapture.Stash(stash, includedUntracked: true, message);
                }

                // 4. The checkout that refuses rather than overwrite or delete an ignored file.
                context.SetRunningText(text.RunningText);
                try
                {
                    await Git.CheckoutDetachedAsync(target.Commit, noOverwriteIgnore: true, options);
                }
                catch (GitException refused)
                {
                    // 5. What was in the way, read before the stash comes back; then the stash back.
                    var failure = FailureOf(refused, text.FailureSummary);
                    var blocked = await AutoStashRefusalDisplayAsync(failure, name);
                    if (stash is not null && !await PopBackAsync(stash, options))
                    {
                        display = new FailureDisplay(text.ErrorTitle, OperationTexts.AutoStashKept(name));
                    }
                    else
                    {
                        display = blocked ?? new FailureDisplay(text.ErrorTitle, text.FailureSummary);
                    }

                    return Failed(text, failure with { Summary = display.Summary });
                }

                // 6. Done: the stash stays until the user pops or drops it.
                return OperationReport.Done(stash is null ? OperationTexts.DetachedAt(target.Commit) : OperationTexts.ChangesInAutoStash(name));
            },
            new RunOptions
            {
                HasSeveralSteps = true,
                DescribeFailure = _ => display,
                Undo = UndoScope.AutoStashCheckout(name, target.Seen.BranchName),
            });
    }

    /// <summary>
    /// 9.2 step 1 (D124): HEAD where the click saw it goes on; otherwise the choice is made again,
    /// and the stash is taken when HEAD is still on a branch, not at the target, and that branch
    /// reaches it (<c>git merge-base --is-ancestor</c>). Otherwise refused, nothing written: "HEAD
    /// moved while this waited: it was on main and is on other now. Nothing was checked out." (or
    /// "main moved while this waited: it was at … and is at … now. …" for the same branch).
    /// </summary>
    private async Task<OperationReport?> RefuseIfAutoStashTargetMovedAsync(OperationText text, AutoStashTarget target)
    {
        var seen = target.Seen;
        var now = await ReadHeadAtStartAsync();
        if (now == seen || await ReachesFromBranchAsync(now, target.Commit))
        {
            return null;
        }

        return Refused(text, CheckoutHeadMoved(seen, now));
    }

    /// <summary>
    /// 9.1's choice made again inside a checkout the click gave no automatic stash (D124): when
    /// HEAD is no longer where the click saw it (<paramref name="seen"/>) and is now on a branch,
    /// not at the commit or tag <paramref name="target"/>, that reaches it (<c>git merge-base
    /// --is-ancestor</c>), the checkout would need the automatic stash and
    /// <c>--no-overwrite-ignore</c>, without which git overwrites an ignored file the target tracks
    /// (0.7). Refused before any write, as the automatic stash's own operation refuses a target it
    /// no longer reaches: "HEAD moved while this waited: it was detached at 41f8398 and is on main
    /// now. Nothing was checked out." (or "main moved while this waited: …" for the same branch).
    /// Null for a branch or a remote branch, when HEAD is where the click saw it or no refs had
    /// been read then, and when the target still takes no stash.
    /// </summary>
    private async Task<OperationReport?> RefuseIfNowAutoStashTargetAsync(OperationText text, string target, HeadState? seen)
    {
        if (IsBranchTarget(target) || seen is null)
        {
            return null;
        }

        var now = await ReadHeadAtStartAsync();
        if (now == seen || now.BranchName is null || await Git.ResolveAsync(target + "^{commit}") is not { } commit
            || !await ReachesFromBranchAsync(now, commit))
        {
            return null;
        }

        return Refused(text, CheckoutHeadMoved(seen, now));
    }

    /// <summary>Whether HEAD, read as <paramref name="now"/>, is on a branch at another commit than <paramref name="commit"/> that reaches it (<c>git merge-base --is-ancestor</c>).</summary>
    private async Task<bool> ReachesFromBranchAsync(HeadState now, string commit) =>
        now is { BranchName: not null, Sha: { } head }
        && !string.Equals(head, commit, StringComparison.Ordinal)
        && await Git.IsAncestorAsync(commit, "HEAD");

    /// <summary>D124's refusal of a checkout whose HEAD moved from <paramref name="seen"/> to <paramref name="now"/>: the branch's own sentence when it is the same branch.</summary>
    private static string CheckoutHeadMoved(HeadState seen, HeadState now) =>
        string.Equals(now.BranchName, seen.BranchName, StringComparison.Ordinal) && seen.BranchName is not null
            ? OperationTexts.HeadCommitMoved(seen.BranchName, seen.Sha ?? string.Empty, now.Sha ?? string.Empty, OperationTexts.NothingCheckedOut)
            : OperationTexts.HeadMoved(seen, now, OperationTexts.NothingCheckedOut);

    /// <summary>A branch or a remote branch, which 9.1 never gives the automatic stash.</summary>
    private static bool IsBranchTarget(string target) =>
        target.StartsWith(HeadsPrefix, StringComparison.Ordinal) || target.StartsWith(RemotesPrefix, StringComparison.Ordinal);

    /// <summary>
    /// 9.2 step 3's failure: when <c>refs/stash</c> did not move from <paramref name="before"/>,
    /// git saved nothing and changed nothing (a file another program held exclusively): the
    /// checkout's default dialog. When it moved, git saved the stash but stopped clearing the
    /// working tree (a file held open for reading on Windows, 0.7: the other untracked files
    /// deleted, the tracked changes left): every untracked file of the stash's third parent that is
    /// missing now is put back (<c>git restore --source=&lt;stash&gt;^3 --worktree</c>), nothing is
    /// checked out, the stash is kept, and "Checkout failed" says so with git's output. The stash
    /// is not given to the undo history: nothing was checked out.
    /// </summary>
    private async Task<(FailureDisplay? Display, OperationReport Report)> StashNotFinishedAsync(OperationText text, string name, GitException failure, string? before, NetworkOptions options)
    {
        var stash = await Git.ResolveAsync("refs/stash");
        if (stash is null || string.Equals(stash, before, StringComparison.Ordinal))
        {
            return (null, Failed(text, FailureOf(failure, text.FailureSummary)));
        }

        Log.Warning($"{Repository.Name}: {text.Title}: git saved the stash {stash} but could not clear the working tree: {failure.Message}");
        var allBack = false;
        try
        {
            var untracked = (await Git.ReadStashPathsAsync(stash)).Untracked;
            var missing = untracked.Where(path => !ExistsInWorkingTree(path)).ToList();
            await Git.RestoreWorkingTreeFilesAsync(stash + "^3", missing, options);
            allBack = untracked.All(ExistsInWorkingTree);
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: {text.Title}: the untracked files git deleted could not all be put back.", ex);
        }

        var display = new FailureDisplay(text.ErrorTitle, allBack ? OperationTexts.AutoStashNotCleared(name) : OperationTexts.AutoStashPartlyCleared(name));
        return (display, Failed(text, FailureOf(failure, display.Summary)));
    }

    /// <summary>
    /// 9.2 step 5's dialog for a checkout refused after the automatic stash (or on a clean tree):
    /// "Checkout blocked" without "Stash changes and check out" (9.3). After the stash only
    /// ignored files (or a submodule) can be in the way, so when every path git named is ignored
    /// (<see cref="IGitRepository.FindIgnoredAsync"/>, a folder by its files) the ignored-files
    /// text names the target; otherwise D153's sentence with git's count. Null for a failure that
    /// is no refusal for changes in the way.
    /// </summary>
    private async Task<FailureDisplay?> AutoStashRefusalDisplayAsync(GitFailure failure, string name)
    {
        if (CheckoutFailureDisplay(failure, name, offerStash: false) is not { } blocked)
        {
            return null;
        }

        var paths = failure.BlockedPaths;
        if (failure.Kind is GitFailureKind.CheckoutBlockedByUntrackedFiles or GitFailureKind.CheckoutBlockedByUntrackedFolder && paths.Count > 0)
        {
            try
            {
                var ignored = await Git.FindIgnoredAsync(paths);
                if (paths.All(ignored.Contains))
                {
                    return blocked with { Summary = OperationTexts.IgnoredFilesInTheWay(paths.Count, name) };
                }
            }
            catch (GitException ex)
            {
                Log.Warning($"{Repository.Name}: the ignored files in the checkout's way could not be read.", ex);
            }
        }

        return blocked;
    }

    // ---- 9.3: the refusal message -------------------------------------------------------------------

    /// <summary>
    /// 9.3's refusals before git, for every checkout path, read fresh: an operation in progress
    /// ("Finish or abort the {kind} first": git would refuse with <c>cannot switch branch while
    /// …</c>) or conflicted files ("Resolve the conflicts first": <c>you need to resolve your
    /// current index first</c>), with the dialog "Checkout blocked" and no git output (the report's
    /// PD5). Null when the checkout may go to git.
    /// </summary>
    private async Task<(OperationReport Report, FailureDisplay Display)?> RefuseCheckoutAsync(OperationText text)
    {
        if (await InProgressOrConflictedRefusalAsync() is not { } summary)
        {
            return null;
        }

        return (Refused(text, summary), new FailureDisplay(OperationTexts.CheckoutBlockedTitle, summary));
    }

    /// <summary>
    /// 9.3's second table: the dialog of a merge, cherry-pick or revert (<paramref name="operation"/>,
    /// "merge" also for a pull's merge or fast-forward) that git refused because changes are in the
    /// way. Something staged ("Your staged changes prevent the merge. …") or git's blocks with the
    /// number of paths they name ("1 conflict prevents the merge. …"); the operation keeps its own
    /// error title (the report's PD6). Null for any other failure.
    /// </summary>
    private static FailureDisplay? BlockedByChangesDisplay(GitFailure failure, OperationText text, string operation)
    {
        if (failure.Kind == GitFailureKind.MergeBlockedByStagedChanges)
        {
            return new FailureDisplay(text.ErrorTitle, OperationTexts.StagedChangesPrevent(operation));
        }

        return failure.BlockedPaths.Count > 0 && failure.Kind != GitFailureKind.Other
            ? new FailureDisplay(text.ErrorTitle, OperationTexts.ConflictsPrevent(failure.BlockedPaths.Count, operation))
            : null;
    }

    // ---- 9.3: a stash's apply or pop, checked before git --------------------------------------------

    /// <summary>
    /// 9.3's last row (D179): how many of <paramref name="stash"/>'s paths are in the way, read
    /// before git runs: those its changes touch (both sides of a rename) that have a local change in
    /// a fresh status (staged, unstaged, untracked or conflicted, a rename's two paths), and its
    /// untracked files that exist in the working tree. Stricter than git on purpose: git's own
    /// refusal still writes the untracked files, and a staged overlap becomes a conflict (0.5).
    /// 0 when nothing is in the way, and when the stash's paths cannot be read (git's own words
    /// then say what is wrong).
    /// </summary>
    private async Task<int> CountStashConflictsAsync(string stash)
    {
        StashPaths paths;
        try
        {
            paths = await Git.ReadStashPathsAsync(stash);
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: the paths of the stash {stash} could not be read; git checks the apply itself.", ex);
            return 0;
        }

        var status = await Git.ReadStatusAsync();
        var local = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in status.Staged.Concat(status.Unstaged))
        {
            local.Add(file.Path);
            if (file.OldPath is { } old)
            {
                local.Add(old);
            }
        }

        var blocked = new HashSet<string>(paths.Changed.Where(local.Contains), StringComparer.Ordinal);
        blocked.UnionWith(paths.Untracked.Where(ExistsInWorkingTree));
        return blocked.Count;
    }

    /// <summary>Whether <paramref name="path"/> (relative to the working tree, with <c>/</c>) is there: a file, a folder or a symbolic link, also a broken one.</summary>
    private bool ExistsInWorkingTree(string path)
    {
        var full = Path.Combine(Repository.Path, path);
        return Path.Exists(full) || new FileInfo(full).LinkTarget is not null;
    }

    /// <summary>A checkout that 9.1 gives the automatic stash: the target's commit, and HEAD as the click saw it (on a branch).</summary>
    private sealed record AutoStashTarget(string Commit, HeadState Seen);
}
