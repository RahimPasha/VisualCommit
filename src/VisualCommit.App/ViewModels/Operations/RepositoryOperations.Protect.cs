// C12, protected changes (docs/phase-5-design.md section 9, D153, D166, D179; the report's part P):
// the automatic stash before a checkout of a commit the current branch reaches (9.1, 9.2), the
// refusals a checkout makes before git (9.3's first table), the refusal message of every checkout
// path and of the merges, picks and reverts that git refuses for the same reason (9.3), and the
// check a stash's apply or pop makes before git (9.3's last row). The checkout paths themselves are
// in RepositoryOperations.Branches.cs, the pull's in RepositoryOperations.FetchPull.cs, the stash's
// in RepositoryOperations.StashTags.cs. Built by work package WP4; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsProtectTests.cs. Phase 6's Keep ignored files
// (D193, D196, D221; docs/phase-6-design.md section 9, work package WP-IA): the setting, the
// checkouts' refusal display with D179's words, and the app's own check before git of the
// operations git has no exact option for; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsKeepIgnoredTests.cs.

using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// Keep ignored files (D193, D196, docs/phase-6-design.md 9.1): every checkout, merge, rebase,
    /// cherry-pick, revert and stash apply or pop refuses to overwrite or delete an ignored file,
    /// read inside each operation when it runs (as <c>StashIncludeUntracked</c> is read), so a
    /// queued operation takes the value at its start (D124); on without settings (view-model
    /// tests) and by default. Every app call of the checkouts, <c>CreateBranchAsync</c> and
    /// <c>MergeAsync</c> passes it (git's <c>--no-overwrite-ignore</c>), and the operations git has
    /// no option for run the app's own check before git while it is on
    /// (<see cref="RefuseIfIgnoredInTheWayAsync"/>). Off turns off only D193's extension (D221):
    /// the automatic stash's checkout passes true whatever it says and asks <c>check-ignore</c>
    /// after a refusal (D179), and undo and redo keep D169's guards.
    /// </summary>
    private bool KeepIgnored => Services?.Settings.Current.KeepIgnoredFiles ?? true;

    // ---- 9.1: which checkouts stash first -----------------------------------------------------------

    /// <summary>
    /// Whether a checkout of <paramref name="target"/> is 9.1's first row, the automatic stash's,
    /// with HEAD as the click sees it (<paramref name="seen"/>): a commit or a tag (not a branch),
    /// HEAD on a branch at another commit, and the branch reaches the target through any parent.
    /// The graph answers for a loaded row of HEAD's graph (a date-ordered log loads every commit
    /// between HEAD and a loaded ancestor); otherwise <c>git merge-base --is-ancestor</c>. Either
    /// operation decides again when HEAD moved meanwhile (<see cref="RefuseIfAutoStashTargetMovedAsync"/>,
    /// <see cref="RefuseIfNowAutoStashTargetAsync"/>). Null for every other checkout, which git
    /// runs as before, with <c>--no-overwrite-ignore</c> while Keep ignored files is on (D193,
    /// docs/phase-6-design.md 9.11): HEAD's own commit too (D179, the report's PD7), a detached or
    /// unborn HEAD, and a commit the branch does not reach.
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
                    await Git.CheckoutDetachedAsync(target.Commit, keepIgnored: true, options);
                }
                catch (GitException refused)
                {
                    // 5. What was in the way, read before the stash comes back; then the stash back.
                    var failure = FailureOf(refused, text.FailureSummary);
                    var blocked = await CheckoutRefusalDisplayAsync(failure, target.Commit, name, offerStash: false, keptIgnored: true);
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
    /// working tree (a file held open for reading on Windows, 0.7 and 15.9: an untracked one stops
    /// its clean after it deleted the other untracked files; a tracked one stops its reset after it
    /// rewrote the other tracked files): what git took out is put back from the stash
    /// (<see cref="IGitRepository.PutBackUnclearedStashAsync"/>), nothing is checked out, the stash
    /// is kept, and "Checkout failed" says so with git's output: that the changes are in both
    /// places only when every path is back, otherwise to keep the stash. The stash is not given to
    /// the undo history: nothing was checked out.
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
            allBack = await Git.PutBackUnclearedStashAsync(stash, options);
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: {text.Title}: the files git took out could not all be put back.", ex);
        }

        var display = new FailureDisplay(text.ErrorTitle, allBack ? OperationTexts.AutoStashNotCleared(name) : OperationTexts.AutoStashPartlyCleared(name));
        return (display, Failed(text, FailureOf(failure, display.Summary)));
    }

    /// <summary>
    /// The dialog of a checkout git refused (docs/phase-6-design.md 9.11; phase 5's 9.2 step 5 and
    /// 9.3), for every checkout path, which catches git's refusal inside its operation: phase 5's
    /// <see cref="CheckoutFailureDisplay"/> first ("Checkout blocked" with D153's sentence and
    /// git's count, "Stash changes and check out" for tracked changes when
    /// <paramref name="offerStash"/>). Git's refusal for an ignored file under
    /// <c>--no-overwrite-ignore</c> is its untracked-files block, which its words cannot tell from
    /// an untracked file's; so for an untracked or folder block with paths, when the checkout passed
    /// the option (<paramref name="keptIgnored"/>: Keep ignored files was on, or the automatic
    /// stash's checkout, which always does), the paths are asked about
    /// (<see cref="IgnoredBlockSummaryAsync"/>): when every one is ignored, D179's text names
    /// <paramref name="name"/> with git's count (a folder once) and the dialog offers nothing (a
    /// stash keeps no ignored file: D91, phase 5's PD8). Null for a failure that is no refusal for
    /// something in the way (2.33's default).
    /// </summary>
    /// <param name="failure">Git's refusal.</param>
    /// <param name="target">What is checked out, as <see cref="CheckoutAsync(string)"/> names it: what the extra button checks out.</param>
    /// <param name="name">The target as D179's text names it: the branch, the remote branch, the tag, the short SHA, or the start point as the name dialog named it.</param>
    /// <param name="offerStash">Offer "Stash changes and check out" for tracked changes in the way.</param>
    /// <param name="keptIgnored">The checkout passed <c>--no-overwrite-ignore</c>.</param>
    private async Task<FailureDisplay?> CheckoutRefusalDisplayAsync(GitFailure failure, string target, string name, bool offerStash, bool keptIgnored)
    {
        if (CheckoutFailureDisplay(failure, target, offerStash) is not { } blocked)
        {
            return null;
        }

        return keptIgnored && await IgnoredBlockSummaryAsync(failure, name) is { } ignored
            ? new FailureDisplay(OperationTexts.CheckoutBlockedTitle, ignored)
            : blocked;
    }

    /// <summary>
    /// D179's text for a checkout git refused only because ignored files are in the way: for an
    /// untracked or folder block with paths, one <c>git check-ignore --stdin -z</c> of the paths git
    /// named (<see cref="IGitRepository.FindIgnoredAsync"/>, a folder by its files); when every one
    /// is ignored, "1 ignored file is in the way: {name} tracks a file of the same name. Move it
    /// away first." with git's count. Null for any other refusal, and when the read fails (logged:
    /// phase 5's message then stands).
    /// </summary>
    private async Task<string?> IgnoredBlockSummaryAsync(GitFailure failure, string name)
    {
        var paths = failure.BlockedPaths;
        if (failure.Kind is not (GitFailureKind.CheckoutBlockedByUntrackedFiles or GitFailureKind.CheckoutBlockedByUntrackedFolder) || paths.Count == 0)
        {
            return null;
        }

        try
        {
            var ignored = await Git.FindIgnoredAsync(paths);
            return paths.All(ignored.Contains) ? OperationTexts.IgnoredFilesInTheWay(paths.Count, name) : null;
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: the ignored files in the checkout's way could not be read.", ex);
            return null;
        }
    }

    // ---- Keep ignored files: the app's own check before git (docs/phase-6-design.md 9.3 to 9.9) ------

    /// <summary>
    /// 9.9: the app's own check of an operation git has no option for (or none that is exact: a
    /// merge), made inside the operation while Keep ignored files is on, after its own refusals
    /// before git and before its first write (backups, the undo capture, git). <paramref name="read"/>
    /// is one of the git layer's five reads (9.4 to 9.8), which lists the ignored files the
    /// operation would overwrite or delete; when it lists any, the operation is refused with
    /// 9.15's text naming them (<paramref name="operation"/>: "merge", "rebase", "squash",
    /// "cherry-pick", "revert"; null for a stash's apply or pop), its own error title and no git
    /// output, and nothing is written or recorded. Null when nothing is in the way, and when the
    /// read fails (a bad revision, on which git fails too, or git not starting): logged, and the
    /// operation goes on to git (9.3), as D179's count does.
    /// </summary>
    private async Task<OperationReport?> RefuseIfIgnoredInTheWayAsync(OperationText text, string? operation, Func<Task<IReadOnlyList<string>>> read)
    {
        IReadOnlyList<string> ignored;
        try
        {
            ignored = await read();
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: {text.Title}: the ignored files in its way could not be read; git runs it as it would.", ex);
            return null;
        }

        if (ignored.Count == 0)
        {
            return null;
        }

        return Refused(text, operation is null ? OperationTexts.IgnoredFilesInTheWayOfStash(ignored) : OperationTexts.IgnoredFilesInTheWayOf(ignored, operation));
    }

    /// <summary>
    /// <paramref name="commits"/> as full SHAs, which the git layer's reads of 9.5 need (its
    /// <c>--stdin</c> resolves no name, 0.4): the graph's commits are full already; another name is
    /// resolved (<c>&lt;name&gt;^{commit}</c>). Null when one names no commit (git then fails as
    /// it would).
    /// </summary>
    private async Task<IReadOnlyList<string>?> FullShasAsync(IReadOnlyList<string> commits)
    {
        var full = new List<string>(commits.Count);
        foreach (var commit in commits)
        {
            if (IsFullSha(commit))
            {
                full.Add(commit);
            }
            else if (await Git.ResolveAsync(commit + "^{commit}") is { } sha)
            {
                full.Add(sha);
            }
            else
            {
                return null;
            }
        }

        return full;
    }

    /// <summary>9.5's read for <paramref name="commits"/> in the order git applies them (picks oldest first, reverts newest first), with a merge reverted with <c>-m 1</c> named by its first parent (<paramref name="mergeParent"/>).</summary>
    private async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string? mergeParent = null) =>
        await FullShasAsync(commits) is { } full
            ? await Git.FindIgnoredInTheWayOfCommitsAsync(full, revert, mergeParent is null ? null : (await FullShasAsync([mergeParent]))?[0])
            : [];

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
