// Branches and history: docs/phase-3-design.md 2.0 to 2.15, 2.31, 4g's "Check out and merge",
// section 5's Abort, and their texts in 2.33 (OperationTexts), built by WP7-branches on the helpers
// of RepositoryOperations.Run.cs. Its tests are tests/VisualCommit.App.Tests/RepositoryOperations{Checkout,Branches,Merge,History}Tests.cs.
// Phase 4 (WP4) picks and reverts several commits with one git call that takes the list
// (docs/phase-4-design.md section 4, D136) and has Abort check out again the branch a "Check out
// and rebase" left (1.3). Phase 5's C12 (WP4, docs/phase-5-design.md section 9) gives every
// checkout here its refusals before git and 9.3's message, the checkout of a commit the branch
// reaches its automatic stash, and merges, picks and reverts their refusal texts; those parts are
// in RepositoryOperations.Protect.cs. The checkout of a remote branch is in
// RepositoryOperations.RemoteBranches.cs (docs/phase-6-design.md 18). Phase 6's Keep ignored
// files (WP-IA, docs/phase-6-design.md 9.4, 9.5, 9.11) gives every checkout and merge git's
// --no-overwrite-ignore, catches each checkout's refusal inside its operation for D179's words,
// and runs the app's own check before a merge, a pick or a revert, one or a list.

using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations : IInProgressActions
{
    // ---- Checkouts (2.1 to 2.3, 2.31, 4g) ---------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CheckoutAsync"/>
    /// <remarks>
    /// C12 (docs/phase-5-design.md 9.1): a commit or a tag the current branch reaches is checked out
    /// after the automatic stash (<see cref="CheckoutWithAutoStashAsync"/>); every other checkout
    /// runs as phase 3's, refused before git while an operation is in progress or files conflict,
    /// or when HEAD moved while it waited onto a branch that reaches the commit or tag
    /// (<see cref="RefuseIfNowAutoStashTargetAsync"/>), and a refusal for changes in the way is
    /// shown with 9.3's message. While Keep ignored files is on (D193, docs/phase-6-design.md 9.11)
    /// git gets <c>--no-overwrite-ignore</c>, and a refusal only for ignored files in the way says
    /// so in D179's words (<see cref="CheckoutRefusalDisplayAsync"/>).
    /// </remarks>
    public Task CheckoutAsync(string target) => CheckoutAsync(target, checkExists: false);

    /// <inheritdoc cref="IRepositoryActions.CheckoutKeptCommitAsync"/>
    public Task CheckoutKeptCommitAsync(string sha) => CheckoutAsync(sha, checkExists: true);

    /// <summary>
    /// <see cref="CheckoutAsync(string)"/>; with <paramref name="checkExists"/> (a reflog row's or a
    /// backup's commit, design 5.2) refused when the commit no longer exists, before anything reads
    /// it (9.1's choice would fail on it) and again inside the operation that checks it out.
    /// </summary>
    private Task CheckoutAsync(string target, bool checkExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var plan = PlanCheckout(target);
        var text = OperationTexts.CheckOut(plan.Name);
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            if (checkExists && !await Git.CommitExistsAsync(target))
            {
                await RefuseAsync(text, OperationTexts.CommitGone(target), RefreshScope.Refs);
                return;
            }

            // 9.1's choice is made on HEAD as the click sees it, and made again inside the
            // operation when HEAD is no longer there (D124).
            var seen = Refs?.Head;
            if (await AutoStashTargetAsync(target, seen) is { } autoStash)
            {
                await CheckoutWithAutoStashAsync(plan.Name, autoStash, text, checkExists);
                return;
            }

            if (await ConfirmLeavingDetachedHeadAsync() is not { } backUpDetached)
            {
                return;
            }

            FailureDisplay? display = null;
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (checkExists && await RefuseIfCommitGoneAsync(text, target) is { } gone)
                    {
                        return gone;
                    }

                    if (await RefuseCheckoutAsync(text) is { } blocked)
                    {
                        display = blocked.Display;
                        return blocked.Report;
                    }

                    if (await RefuseIfNowAutoStashTargetAsync(text, target, seen) is { } moved)
                    {
                        return moved;
                    }

                    if (await GuardDetachedHeadAsync(text, backUpDetached) is { } refusal)
                    {
                        return refusal;
                    }

                    // Git's refusal is shown in its operation, where the ignored files in its way
                    // can be read (docs/phase-6-design.md 9.11).
                    var keepIgnored = KeepIgnored;
                    try
                    {
                        return OperationReport.Done(await plan.Run(NetworkOptions.From(context)));
                    }
                    catch (GitException refused)
                    {
                        var failure = FailureOf(refused, text.FailureSummary);
                        if (await CheckoutRefusalDisplayAsync(failure, target, plan.Name, offerStash: true, keepIgnored) is not { } shown)
                        {
                            throw;
                        }

                        // The detached commits were confirmed and backed up already: 2.31 does not ask again.
                        display = shown is { Extra: { } extra } && backUpDetached
                            ? shown with { Extra = extra with { Run = () => StashAndCheckoutAsync(target, detachedBackedUp: true) } }
                            : shown;
                        return Failed(text, failure with { Summary = display.Summary });
                    }
                },
                new RunOptions
                {
                    DescribeFailure = _ => display,
                    Undo = UndoScope.Checkout(plan.Name, Refs?.Head.BranchName, CreatedBranchOf(target)),
                });
        });
    }

    /// <inheritdoc cref="IRepositoryActions.StashAndCheckoutAsync"/>
    public Task StashAndCheckoutAsync(string target) => StashAndCheckoutAsync(target, detachedBackedUp: false);

    /// <summary>2.31; <paramref name="detachedBackedUp"/>: the checkout it follows asked about the detached commits and saved them.</summary>
    private Task StashAndCheckoutAsync(string target, bool detachedBackedUp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var plan = PlanCheckout(target);
        var name = plan.Name;
        var text = OperationTexts.StashAndCheckOut(name);
        return GuardAsync(text, async () =>
        {
            var leaving = detachedBackedUp ? false : await ConfirmLeavingDetachedHeadAsync();
            if (leaving is not { } backUpDetached)
            {
                return;
            }

            FailureDisplay? display = null;
            var conflicted = -1;
            var result = await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    var options = NetworkOptions.From(context);

                    // After a blocked checkout that asked and saved the backup, nothing more to do.
                    if (!detachedBackedUp && await GuardDetachedHeadAsync(text, backUpDetached) is { } refusal)
                    {
                        return refusal;
                    }

                    // 1. The tracked changes only: the untracked files blocked nothing, and the
                    // checkout carries them along (D91).
                    var keepIgnored = KeepIgnored;
                    var stash = await Git.StashAsync(OperationTexts.StashBeforeCheckout(name), includeUntracked: false, options);
                    UndoCapture.Stash(stash, includedUntracked: false, OperationTexts.StashBeforeCheckout(name));
                    if (stash is null)
                    {
                        // Nothing is left to stash (the changes went meanwhile): a plain checkout,
                        // whose refusal is shown as every checkout's (docs/phase-6-design.md 9.11).
                        try
                        {
                            return OperationReport.Done(await plan.Run(options));
                        }
                        catch (GitException refused)
                        {
                            var failure = FailureOf(refused, text.FailureSummary);
                            if (await CheckoutRefusalDisplayAsync(failure, target, name, offerStash: false, keepIgnored) is not { } shown)
                            {
                                throw;
                            }

                            display = shown;
                            return Failed(text, failure with { Summary = shown.Summary });
                        }
                    }

                    // 2. The checkout; when it fails, the changes go back at once. Only ignored files
                    // can be in its way now (the untracked files blocked nothing): what was is read
                    // before the stash comes back (docs/phase-6-design.md 9.11).
                    try
                    {
                        await plan.Run(options);
                    }
                    catch (GitException checkoutFailure)
                    {
                        var ignored = keepIgnored ? await IgnoredBlockSummaryAsync(FailureOf(checkoutFailure, text.FailureSummary), name) : null;
                        if (!await PopBackAsync(stash, options))
                        {
                            return Failed(text, FailureOf(checkoutFailure, OperationTexts.CheckoutFailedChangesKept(name)));
                        }

                        if (ignored is not null)
                        {
                            display = new FailureDisplay(OperationTexts.CheckoutBlockedTitle, ignored);
                        }

                        return Failed(text, FailureOf(checkoutFailure, ignored ?? text.FailureSummary));
                    }

                    // 3. The changes again, staged ones staged (--index, or without it when git refuses).
                    ApplyResult popped;
                    try
                    {
                        popped = await Git.ApplyStashAsync(stash, StashApplyMode.PopKeepingIndex, options);
                    }
                    catch (GitException popFailure)
                    {
                        display = new FailureDisplay(OperationTexts.NotReappliedTitle, OperationTexts.NotReapplied(name));
                        return Failed(text, FailureOf(popFailure, display.Summary));
                    }

                    if (popped == ApplyResult.Conflicts)
                    {
                        conflicted = await CountConflictedFilesAsync();
                        return OperationReport.Conflicts(OperationTexts.ReappliedWithConflictsStatus(name, conflicted));
                    }

                    return OperationReport.Done(OperationTexts.CheckedOutWithChanges(name));
                },
                new RunOptions
                {
                    HasSeveralSteps = true,
                    DescribeFailure = _ => display,
                    Undo = UndoScope.StashAndCheckout(name, Refs?.Head.BranchName, CreatedBranchOf(target)),
                });

            if (result.Outcome == OperationOutcome.Conflicts && conflicted >= 0)
            {
                // D95: the one conflict that opens a dialog, so the user knows where the changes are.
                await ShowInformationAsync(OperationTexts.ReappliedWithConflictsTitle, OperationTexts.ReappliedWithConflicts(name, conflicted));
            }
        });
    }

    /// <inheritdoc cref="IRepositoryActions.CheckoutAndMergeAsync"/>
    public Task CheckoutAndMergeAsync(string branchRef, string sourceRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        var branch = ShortName(branchRef);
        var source = ShortName(sourceRef);
        var text = OperationTexts.CheckOutAndMerge(branch, source);
        var mergeText = OperationTexts.Merge(source, branch);
        return GuardAsync(text, async () =>
        {
            if (await ConfirmLeavingDetachedHeadAsync() is not { } backUpDetached)
            {
                return;
            }

            FailureDisplay? display = null;
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    // Its checkout's refusals before git (C12, design 9.3, the report's PD5).
                    if (await RefuseCheckoutAsync(text) is { } blocked)
                    {
                        display = blocked.Display;
                        return blocked.Report;
                    }

                    // Keep ignored files (docs/phase-6-design.md 9.4, 9.9): the merge's check, into
                    // the branch it checks out, before that checkout ("Merge failed").
                    var keepIgnored = KeepIgnored;
                    if (keepIgnored
                        && await RefuseIfIgnoredInTheWayAsync(text, "merge", () => Git.FindIgnoredInTheWayOfMergeAsync(branchRef, sourceRef)) is { } ignored)
                    {
                        display = new FailureDisplay(mergeText.ErrorTitle, ignored.Failure!.Summary);
                        return ignored;
                    }

                    var options = NetworkOptions.From(context);
                    if (await GuardDetachedHeadAsync(text, backUpDetached) is { } detached)
                    {
                        return detached;
                    }

                    // A failed checkout ends it with the checkout's dialog (2.33), without the stash
                    // button: the merge would then run on top of re-applied changes (4g).
                    try
                    {
                        await Git.CheckoutBranchAsync(branch, keepIgnored: keepIgnored, options);
                    }
                    catch (GitException checkoutFailure)
                    {
                        var failure = FailureOf(checkoutFailure, text.FailureSummary);
                        if (await CheckoutRefusalDisplayAsync(failure, branchRef, branch, offerStash: false, keepIgnored) is not { } shown)
                        {
                            throw;
                        }

                        display = shown;
                        return Failed(text, failure with { Summary = shown.Summary });
                    }

                    try
                    {
                        return await MergeReportAsync(await Git.MergeAsync(sourceRef, MergeMode.Default, keepIgnored: keepIgnored, options), source, branch);
                    }
                    catch (GitException mergeFailure)
                    {
                        var failure = FailureOf(mergeFailure, mergeText.FailureSummary);
                        display = BlockedByChangesDisplay(failure, mergeText, "merge") ?? new FailureDisplay(mergeText.ErrorTitle, mergeText.FailureSummary);
                        return Failed(text, failure with { Summary = display.Summary });
                    }
                },
                new RunOptions
                {
                    HasSeveralSteps = true,
                    DescribeFailure = failure => display ?? CheckoutFailureDisplay(failure, branchRef, offerStash: false),
                    Undo = UndoScope.CheckoutAndMerge(branch, source, Refs?.Head.BranchName),
                });
        });
    }

    // The checkouts' error dialog (2.31, 2.33), which C12 changes (docs/phase-5-design.md 9.3).

    /// <summary>"Stash changes and check out" (2.31): only for a checkout blocked by tracked changes.</summary>
    public ErrorAction StashAndCheckoutAction(string target) =>
        new(OperationTexts.StashAndCheckOutButton, () => StashAndCheckoutAsync(target));

    /// <summary>
    /// The dialog of a checkout's failure (2.1 to 2.3, C12's 9.3): "Checkout blocked" with D153's
    /// sentence and git's count of the paths in the way (<see cref="GitFailure.BlockedPaths"/>, at
    /// least 1), git's output naming them. For tracked changes alone it offers "Stash changes and
    /// check out" when <paramref name="offerStash"/> (D80, D91: phase 3's checkouts of
    /// <see cref="CheckoutAsync"/>, not 4g's drop, "Create branch" or an automatic stash's
    /// refusal); untracked files or a folder in the way offer nothing (D91). Null for any other
    /// failure (2.33's default).
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <param name="target">What is checked out, as <see cref="CheckoutAsync"/> names it: what the extra button checks out.</param>
    /// <param name="offerStash">Offer "Stash changes and check out".</param>
    public FailureDisplay? CheckoutFailureDisplay(GitFailure failure, string target, bool offerStash)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(target);
        var summary = OperationTexts.ConflictsPreventCheckout(Math.Max(1, failure.BlockedPaths.Count));
        return failure.Kind switch
        {
            GitFailureKind.CheckoutBlockedByLocalChanges => new FailureDisplay(
                OperationTexts.CheckoutBlockedTitle,
                summary,
                offerStash ? StashAndCheckoutAction(target) : null),
            GitFailureKind.CheckoutBlockedByUntrackedFiles or GitFailureKind.CheckoutBlockedByUntrackedFolder => new FailureDisplay(
                OperationTexts.CheckoutBlockedTitle,
                summary),
            _ => null,
        };
    }

    // ---- Branches (2.4 to 2.7) --------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CreateBranchAsync"/>
    public Task CreateBranchAsync(string? start) => CreateBranchAsync(start, checkExists: false);

    /// <inheritdoc cref="IRepositoryActions.CreateBranchAtKeptCommitAsync"/>
    public Task CreateBranchAtKeptCommitAsync(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        return CreateBranchAsync(sha, checkExists: true);
    }

    /// <summary>
    /// <see cref="CreateBranchAsync(string?)"/>; with <paramref name="checkExists"/> (a reflog row's
    /// or a backup's commit, design 5.2) refused inside the operation when the commit no longer
    /// exists. Before the dialog nothing needs it: a commit whose subject cannot be read is named
    /// by its short SHA.
    /// </summary>
    private async Task CreateBranchAsync(string? start, bool checkExists)
    {
        if (IsClosed)
        {
            return;
        }

        var head = Refs?.Head;
        if (start is null && head?.Sha is null)
        {
            // An unborn HEAD has no commit to start from: the menus disable it ("Make the first commit first").
            Log.Info($"{Repository.Name}: Create branch: HEAD has no commit to start from.");
            return;
        }

        var startNamed = start ?? head!.Sha!;
        var (startPoint, context) = await DescribeStartAsync(startNamed);
        var answer = await ShowDialogAsync(NewBranchDialog(context));
        if (answer is null)
        {
            return;
        }

        var name = answer.Name;
        var checkout = answer.IsOptionChecked;
        var text = OperationTexts.CreateBranch(name);
        await GuardAsync(text, async () =>
        {
            // Checking the new branch out leaves a detached HEAD: 2.1's rule for every checkout (D83),
            // unless the branch starts where HEAD is.
            var backUpDetached = false;
            if (checkout && Refs?.Head is { IsDetached: true } now && await Git.ResolveAsync(startPoint) != now.Sha)
            {
                if (await ConfirmLeavingDetachedHeadAsync() is not { } confirmed)
                {
                    return;
                }

                backUpDetached = confirmed;
            }

            FailureDisplay? display = null;
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async operation =>
                {
                    if (checkExists && await RefuseIfCommitGoneAsync(text, startPoint) is { } gone)
                    {
                        return gone;
                    }

                    // Its checkout's refusals before git (C12, design 9.3); git creates no branch then.
                    if (checkout && await RefuseCheckoutAsync(text) is { } blocked)
                    {
                        display = blocked.Display;
                        return blocked.Report;
                    }

                    // HEAD as it is now, not as it was when the dialog opened (2.1's rule, D83).
                    if (checkout && await GuardDetachedHeadAsync(text, backUpDetached, keptBy: startPoint) is { } refusal)
                    {
                        return refusal;
                    }

                    // A start point fetched from two refs or by two remotes gets no upstream
                    // (D201); false while the refspecs are not read (docs/phase-6-design.md 9.11).
                    var noTrack = Refs?.SourceOf(startPoint) is { Kind: RemoteSourceKind.AmbiguousRemotes or RemoteSourceKind.AmbiguousRefs };
                    var keepIgnored = KeepIgnored;
                    try
                    {
                        await Git.CreateBranchAsync(name, startPoint, checkout, noTrack, keepIgnored: keepIgnored, NetworkOptions.From(operation));
                    }
                    catch (GitException refused) when (checkout)
                    {
                        // Its checkout's refusal, shown in the operation (docs/phase-6-design.md 9.11),
                        // naming the start as the dialog names it.
                        var failure = FailureOf(refused, text.FailureSummary);
                        if (await CheckoutRefusalDisplayAsync(failure, startPoint, ShortName(startNamed), offerStash: false, keepIgnored) is not { } shown)
                        {
                            throw;
                        }

                        display = shown;
                        return Failed(text, failure with { Summary = shown.Summary });
                    }

                    FollowCreatedBranch(HeadsPrefix + name);
                    return OperationReport.Done(OperationTexts.CreatedBranch(name, checkout));
                },
                new RunOptions
                {
                    // A refused checkout shows 9.3's message without "Stash changes and check out"
                    // (the report's PD4): the branch it would check out does not exist.
                    DescribeFailure = failure => display ?? (checkout ? CheckoutFailureDisplay(failure, startPoint, offerStash: false) : null),
                    Undo = UndoScope.CreateBranch(name, Refs?.Head.BranchName),
                });
        });
    }

    /// <inheritdoc cref="IRepositoryActions.RenameBranchAsync"/>
    public async Task RenameBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        if (IsClosed)
        {
            return;
        }

        var branch = ShortName(branchRef);
        var answer = await ShowDialogAsync(RenameBranchDialog(branch));
        if (answer is null)
        {
            return;
        }

        var newName = answer.Name;
        var text = OperationTexts.RenameBranch(branch, newName);
        await GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await Git.RenameBranchAsync(branch, newName);
                FollowRenamedBranch(HeadsPrefix + branch, HeadsPrefix + newName);
                return OperationReport.Done(OperationTexts.RenamedBranch(branch, newName));
            },
            new RunOptions { Undo = UndoScope.RenameBranch(branch, newName) }));
    }

    /// <inheritdoc cref="IRepositoryActions.DeleteBranchAsync"/>
    public Task DeleteBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        var branch = ShortName(branchRef);
        var text = OperationTexts.DeleteBranch(branch);
        return GuardAsync(text, async () =>
        {
            if (string.Equals(Refs?.Head.BranchName, branch, StringComparison.Ordinal))
            {
                // The menus disable it (2.6); git would refuse it too.
                await ShowDialogAsync(new ErrorDialogViewModel(text.ErrorTitle, OperationTexts.CannotDeleteCurrentBranch, Repository.Name, clipboard: Services?.Clipboard, log: Log));
                return;
            }

            // "It has commits that main does not have." when HEAD lacks them (2.6).
            var lacking = Refs?.Head.IsUnborn != false || !await Git.IsAncestorAsync(branchRef, "HEAD") ? HeadName : null;
            await ConfirmAndRunAsync(
                OperationTexts.ConfirmDeleteBranch(branch, lacking),
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                _ => Task.FromResult<BackupRequest?>(new BackupRequest("branch", [new BackupEntry(branch, branchRef)])),
                async (_, _) =>
                {
                    await Git.DeleteBranchAsync(branch);
                    FollowDeletedBranch(HeadsPrefix + branch);
                    return OperationReport.Done(OperationTexts.DeletedBranch(branch));
                },
                new RunOptions { Undo = UndoScope.DeleteBranch(branch) });
        });
    }

    /// <inheritdoc cref="IRepositoryActions.FastForwardBranchAsync"/>
    public Task FastForwardBranchAsync(string branchRef, string targetRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRef);
        var branch = ShortName(branchRef);
        var target = ShortName(targetRef);
        var text = OperationTexts.FastForward(branch, target);
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await Git.FastForwardBranchAsync(branch, targetRef);
                return OperationReport.Done(OperationTexts.FastForwarded(branch, target));
            },
            new RunOptions
            {
                Prompts = PromptMode.None,
                DescribeFailure = failure => FastForwardFailureDisplay(failure, branch, target),
                Undo = UndoScope.FastForwardBranch(branch),
            }));
    }

    // ---- Merge (2.8 to 2.10) ----------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.MergeAsync"/>
    public Task MergeAsync(string sourceRef, MergeMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        var source = ShortName(sourceRef);
        var current = HeadName;
        var seen = Refs?.Head;
        var text = mode == MergeMode.FastForwardOnly ? OperationTexts.FastForward(current, source) : OperationTexts.Merge(source, current);
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                if (await RefuseWhileInProgressAsync(text) is { } refusal)
                {
                    return refusal;
                }

                if (await RefuseIfHeadMovedAsync(text, seen, OperationTexts.NothingMerged) is { } moved)
                {
                    return moved;
                }

                // Keep ignored files (docs/phase-6-design.md 9.4): git's option is exact for a
                // fast-forward only, and whether a merge fast-forwards is git's to find out.
                var keepIgnored = KeepIgnored;
                if (keepIgnored && await RefuseIfIgnoredInTheWayAsync(text, "merge", () => Git.FindIgnoredInTheWayOfMergeAsync("HEAD", sourceRef)) is { } ignored)
                {
                    return ignored;
                }

                return await MergeReportAsync(await Git.MergeAsync(sourceRef, mode, keepIgnored: keepIgnored, NetworkOptions.From(context)), source, current);
            },
            new RunOptions
            {
                // C12: git refused for changes in the way (design 9.3's second table).
                DescribeFailure = failure => BlockedByChangesDisplay(failure, text, "merge"),
                Undo = UndoScope.Merge(source, current, fastForwardOnly: mode == MergeMode.FastForwardOnly),
            }));
    }

    // ---- Reset (2.11) -----------------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.ResetAsync"/>
    public Task ResetAsync(string commit) => ResetAsync(commit, checkExists: false);

    /// <inheritdoc cref="IRepositoryActions.ResetToKeptCommitAsync"/>
    public Task ResetToKeptCommitAsync(string sha) => ResetAsync(sha, checkExists: true);

    /// <summary>
    /// <see cref="ResetAsync(string)"/>; with <paramref name="checkExists"/> (a reflog row's commit,
    /// design 5.2) refused when the commit no longer exists, before the dialog asks about it and
    /// again inside the operation.
    /// </summary>
    private Task ResetAsync(string commit, bool checkExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.Reset(current, commit);
        return GuardAsync(text, async () =>
        {
            if (IsClosed || seen is not { IsUnborn: false })
            {
                // Nothing to reset before the first commit: the menus disable it (2.11).
                return;
            }

            if (checkExists && !await Git.CommitExistsAsync(commit))
            {
                await RefuseAsync(text, OperationTexts.CommitGone(commit), RefreshScope.Refs);
                return;
            }

            // The dialog is the confirmation, and the only one (2.11).
            var subject = await ReadSubjectAsync(commit) ?? string.Empty;
            if (await ShowDialogAsync(new ResetDialogViewModel(current, commit, subject)) is not { } mode)
            {
                return;
            }

            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (checkExists && await RefuseIfCommitGoneAsync(text, commit) is { } gone)
                    {
                        return gone;
                    }

                    // The dialog named the branch and what it would lose: HEAD must still be there.
                    if (await RefuseIfHeadMovedAsync(text, seen, OperationTexts.NothingReset, sameCommit: true) is { } moved)
                    {
                        return moved;
                    }

                    var head = (await ReadHeadAtStartAsync()).Sha ?? throw new InvalidOperationException("HEAD names no commit.");
                    // Hard: the changes it discards; mixed: the index it replaces, when anything is
                    // staged, so that "this can be undone" holds for a staged version too.
                    var hard = mode == ResetMode.Hard ? await Git.SnapshotForHardResetWithFilesAsync(commit) : null;
                    var changes = mode switch
                    {
                        ResetMode.Hard => hard?.Commit,
                        ResetMode.Mixed => await Git.SnapshotIndexAsync(),
                        _ => null,
                    };
                    var saved = await SaveBackupAsync(new BackupRequest("reset", [new BackupEntry(null, head)]) { ChangesCommit = changes });

                    // A hard reset's undo needs the snapshot's index and files, known here (7.7 step 3).
                    var snapshotRef = saved?.Refs.FirstOrDefault(name => name.EndsWith("-changes", StringComparison.Ordinal));
                    if (hard is { Paths: { } paths, Present: { } present } && snapshotRef is not null)
                    {
                        UndoCapture.Reset(mode, changes, hard.Index, new DiscardSnapshot(snapshotRef, hard.Commit, paths, present));
                    }
                    else
                    {
                        UndoCapture.Reset(mode, changes);
                    }
                    await Git.ResetAsync(commit, mode, NetworkOptions.From(context));
                    return OperationReport.Done(OperationTexts.ResetDone(current, commit, mode));
                },
                new RunOptions { Undo = UndoScope.Reset(current, commit, mode) });
        });
    }

    // ---- Cherry-pick and revert (2.12 to 2.14) ----------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CherryPickAsync"/>
    public Task CherryPickAsync(string commit) => CherryPickAsync(commit, checkExists: false);

    /// <inheritdoc cref="IRepositoryActions.CherryPickKeptCommitAsync"/>
    public Task CherryPickKeptCommitAsync(string sha) => CherryPickAsync(sha, checkExists: true);

    /// <summary><see cref="CherryPickAsync(string)"/>; with <paramref name="checkExists"/> (a reflog row's commit, design 5.2) refused inside the operation when the commit no longer exists.</summary>
    private Task CherryPickAsync(string commit, bool checkExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.CherryPick(commit);
        FailureDisplay? display = null;
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                if (checkExists && await RefuseIfCommitGoneAsync(text, commit) is { } gone)
                {
                    return gone;
                }

                if (await RefuseWhileInProgressAsync(text) is { } refusal)
                {
                    return refusal;
                }

                if (await RefuseIfHeadMovedAsync(text, seen, OperationTexts.NothingCherryPicked) is { } moved)
                {
                    return moved;
                }

                // Keep ignored files (docs/phase-6-design.md 9.5): git has no option for a pick.
                if (KeepIgnored && await RefuseIfIgnoredInTheWayAsync(text, "cherry-pick", () => FindIgnoredInTheWayOfCommitsAsync([commit], revert: false)) is { } ignored)
                {
                    return ignored;
                }

                switch (await Git.CherryPickAsync(commit, NetworkOptions.From(context)))
                {
                    case ApplyResult.Applied:
                        return OperationReport.Done(OperationTexts.CherryPicked(commit, current));

                    case ApplyResult.Conflicts:
                        return OperationReport.Conflicts(OperationTexts.CherryPickConflicts(await CountConflictedFilesAsync()));

                    default:
                        // An empty pick stops with CHERRY_PICK_HEAD: a failure with a dialog of its
                        // own, with git's output and the command, and the banner offers Abort (2.12).
                        display = new FailureDisplay(OperationTexts.CherryPickStoppedTitle, OperationTexts.CherryPickEmpty(commit, current));
                        return Failed(text, FailureOfLastCall(context, command => IsCallOn(command, "cherry-pick", commit), display.Summary));
                }
            },
            new RunOptions
            {
                DescribeFailure = failure => display ?? BlockedByChangesDisplay(failure, text, "cherry-pick"),
                Undo = UndoScope.CherryPick([commit], seen?.BranchName),
            }));
    }

    /// <inheritdoc cref="IRepositoryActions.CherryPickSeveralAsync"/>
    public Task CherryPickSeveralAsync(IReadOnlyList<string> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        var ordered = OrderByRow(commits, oldestFirst: true);
        if (ordered.Count == 1)
        {
            return CherryPickAsync(ordered[0]);
        }

        var count = ordered.Count;
        var current = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.CherryPickSeveral(count);
        return GuardAsync(text, () => RunListAsync(
            text,
            InProgressKind.CherryPick,
            seen,
            OperationTexts.NothingCherryPicked,
            ordered,
            options => Git.CherryPickAsync(ordered, options),
            OperationTexts.CherryPickedSeveral(count, current),
            UndoScope.CherryPick(ordered, seen?.BranchName)));
    }

    /// <summary>
    /// Several picks or reverts with one git call that takes the whole list (docs/phase-4-design.md
    /// section 4, D136, D145): refused while an operation is in progress or HEAD moved since the
    /// click; then <paramref name="apply"/> and how it ended, read from git's files: all applied
    /// (<paramref name="done"/>), stopped with conflicts ("Cherry-picked {K} of {N} commits;
    /// stopped at {short} with conflicts in {M} files"), on a commit that changes nothing ("…;
    /// {short} changes nothing"), or on one git could not apply without a conflict ("…; git could
    /// not pick {short}"), the last also in the error dialog "Cherry-pick stopped" with git's output
    /// and the banner's text (5.1). Every stop is in the warning colour and leaves the rest in
    /// git's list for the banner's Continue. A list git refused at its start was cleared by the
    /// git layer with <c>--quit</c>, which keeps the user's changes, and says why in C12's words
    /// (docs/phase-5-design.md 9.3): "Your staged changes prevent the cherry-pick. …" or "{N}
    /// conflicts prevent the cherry-pick. …" (the revert's alike). Phase 4's "Git would overwrite
    /// your changes: commit or stash them first. Nothing was cherry-picked." is kept for a refusal
    /// that names no conflict and no staged change.
    /// </summary>
    private async Task RunListAsync(
        OperationText text,
        InProgressKind kind,
        HeadState? seen,
        string nothing,
        IReadOnlyList<string> commits,
        Func<NetworkOptions, Task<SequenceResult>> apply,
        string done,
        UndoScope undo)
    {
        var revert = kind == InProgressKind.Revert;
        var notice = new StopNotice();
        await RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                if (await RefuseWhileInProgressAsync(text) is { } refusal)
                {
                    return refusal;
                }

                if (await RefuseIfHeadMovedAsync(text, seen, nothing) is { } moved)
                {
                    return moved;
                }

                // Keep ignored files (docs/phase-6-design.md 9.5): the list as git gets it.
                if (KeepIgnored
                    && await RefuseIfIgnoredInTheWayAsync(text, revert ? "revert" : "cherry-pick", () => FindIgnoredInTheWayOfCommitsAsync(commits, revert)) is { } ignored)
                {
                    return ignored;
                }

                var result = await apply(NetworkOptions.From(context));
                if (result.Stop == SequenceStop.None)
                {
                    return OperationReport.Done(done);
                }

                var (state, conflicts) = await FreshStateAsync();
                var sha = state.Commit ?? string.Empty;
                switch (result.Stop)
                {
                    case SequenceStop.Empty:
                        return OperationReport.Conflicts(OperationTexts.ListStoppedEmpty(kind, result.Applied, result.Total, sha));

                    case SequenceStop.Refused:
                        var summary = ConflictBannerViewModel.TextFor(state, conflicts) ?? OperationTexts.ListStoppedRefused(kind, result.Applied, result.Total, sha);
                        notice.Display = new FailureDisplay(kind == InProgressKind.Revert ? OperationTexts.RevertStoppedTitle : OperationTexts.CherryPickStoppedTitle, summary);
                        notice.Failure = FailureOfLastCall(context, command => command.StartsWith($"git {(kind == InProgressKind.Revert ? "revert" : "cherry-pick")} ", StringComparison.Ordinal), summary);
                        return OperationReport.Conflicts(OperationTexts.ListStoppedRefused(kind, result.Applied, result.Total, sha));

                    default:
                        return OperationReport.Conflicts(OperationTexts.ListStoppedWithConflicts(kind, result.Applied, result.Total, sha, conflicts));
                }
            },
            new RunOptions
            {
                HasSeveralSteps = true,

                // A list git refused at its start for changes in the way: C12's words (design 9.3),
                // or phase 4's sentence for a refusal that names no conflict and no staged change.
                DescribeFailure = failure => BlockedByChangesDisplay(failure, text, kind == InProgressKind.Revert ? "revert" : "cherry-pick")
                    ?? (failure.GitOutput.Contains("would be overwritten", StringComparison.Ordinal)
                        ? new FailureDisplay(text.ErrorTitle, OperationTexts.ListRefusedAtStart(kind))
                        : null),
                Undo = undo,
            });
        await FinishAsync(notice, new SelectionAfter());
    }

    /// <inheritdoc cref="IRepositoryActions.RevertAsync"/>
    public Task RevertAsync(string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.Revert(commit);
        return GuardAsync(text, async () =>
        {
            var parents = await ParentsOfAsync(commit);
            var isMerge = parents.Count > 1;
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (await RefuseWhileInProgressAsync(text) is { } refusal)
                    {
                        return refusal;
                    }

                    if (await RefuseIfHeadMovedAsync(text, seen, OperationTexts.NothingReverted) is { } moved)
                    {
                        return moved;
                    }

                    // Keep ignored files (docs/phase-6-design.md 9.5): a merge, reverted with -m 1,
                    // by its change against its first parent.
                    if (KeepIgnored
                        && await RefuseIfIgnoredInTheWayAsync(text, "revert", () => FindIgnoredInTheWayOfCommitsAsync([commit], revert: true, isMerge ? parents[0] : null)) is { } ignored)
                    {
                        return ignored;
                    }

                    ApplyResult result;
                    try
                    {
                        result = await Git.RevertAsync(commit, isMerge, NetworkOptions.From(context));
                    }
                    catch (GitException failure) when (ChangesNothing(failure))
                    {
                        // D117: an ordinary failure, git's output beneath the summary.
                        return Failed(text, FailureOf(failure, OperationTexts.RevertEmpty(commit, current)));
                    }

                    return result switch
                    {
                        ApplyResult.Applied => OperationReport.Done(OperationTexts.Reverted(commit)),
                        ApplyResult.Conflicts => OperationReport.Conflicts(OperationTexts.RevertConflicts(await CountConflictedFilesAsync())),

                        // A git that keeps REVERT_HEAD after a revert that changes nothing (D117's
                        // other case): a stop, not a failure; the banner says so and offers Abort,
                        // and no dialog opens (2.33, check 12 part E).
                        _ => OperationReport.Conflicts(OperationTexts.RevertStoppedEmpty(commit, current)),
                    };
                },
                new RunOptions
                {
                    DescribeFailure = failure => BlockedByChangesDisplay(failure, text, "revert"),
                    Undo = UndoScope.Revert([commit], seen?.BranchName),
                });
        });
    }

    /// <inheritdoc cref="IRepositoryActions.RevertSeveralAsync"/>
    public Task RevertSeveralAsync(IReadOnlyList<string> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        var ordered = OrderByRow(commits, oldestFirst: false);
        if (ordered.Count == 1)
        {
            return RevertAsync(ordered[0]);
        }

        var count = ordered.Count;
        var seen = Refs?.Head;
        var text = OperationTexts.RevertSeveral(count);
        return GuardAsync(text, async () =>
        {
            foreach (var sha in ordered)
            {
                if (await IsMergeAsync(sha))
                {
                    // The menu disables it ("Revert merge commits one at a time", section 4).
                    Log.Info($"{Repository.Name}: {text.Title}: not started, {OperationTexts.Short(sha)} is a merge commit.");
                    return;
                }
            }

            // One git revert of the whole list, newest first (D136).
            await RunListAsync(
                text,
                InProgressKind.Revert,
                seen,
                OperationTexts.NothingReverted,
                ordered,
                options => Git.RevertAsync(ordered, options),
                OperationTexts.RevertedSeveral(count),
                UndoScope.Revert(ordered, seen?.BranchName));
        });
    }

    // ---- Abort (2.15, section 5) -------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.AbortAsync"/>
    /// <remarks>Asks the banner's question (section 5, D110) for what the last status read shows in progress, then <see cref="AbortAsync(InProgressState)"/>.</remarks>
    public Task AbortAsync()
    {
        var state = Status.InProgress;
        if (!state.IsActive)
        {
            return Task.CompletedTask;
        }

        return GuardAsync(OperationTexts.Abort(state.Kind), async () =>
        {
            if (await ConfirmAsync(AbortConfirmation(state)))
            {
                await AbortAsync(state);
            }
        });
    }

    /// <summary>
    /// The banner's Abort question for <paramref name="state"/> (section 5, D110), with the
    /// branch a rebase begun with "Check out {A} and rebase it" checks out again (1.3, 14.3).
    /// </summary>
    public ConfirmationRequest AbortConfirmation(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var returnBranch = state.Kind == InProgressKind.Rebase && ReadReturnBranch() is { } fullRef ? ShortName(fullRef) : null;
        return ConflictBannerViewModel.AbortConfirmation(state, returnBranch);
    }

    /// <inheritdoc cref="IInProgressActions.AbortAsync"/>
    /// <remarks>
    /// A rebase that "Check out {A} and rebase it onto {B}" began checks out again the branch the
    /// user was on (<see cref="ReturnFileName"/>, read before git's abort, 1.3), so that
    /// everything is where it was.
    /// </remarks>
    public Task AbortAsync(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActive)
        {
            return Task.CompletedTask;
        }

        var text = OperationTexts.Abort(state.Kind);
        FailureDisplay? display = null;
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                var options = NetworkOptions.From(context);
                var returnBranch = state.Kind == InProgressKind.Rebase ? ReadReturnBranch() : null;

                // abort-<time> keeps HEAD, so commits a sequence already made stay reachable; the
                // -changes snapshot keeps what was done to resolve the conflicts (2.0, 2.15).
                var head = await Git.ResolveAsync("HEAD");
                var changes = await Git.SnapshotTrackedChangesAsync();
                await SaveBackupAsync(new BackupRequest("abort", head is null ? [] : [new BackupEntry(null, head)]) { ChangesCommit = changes });
                await Git.AbortAsync(state.Kind, options);

                if (returnBranch is not null && ShortName(returnBranch) is var branch
                    && !string.Equals((await Git.ReadHeadAsync()).BranchName, branch, StringComparison.Ordinal))
                {
                    var keepIgnored = KeepIgnored;
                    try
                    {
                        await Git.CheckoutBranchAsync(branch, keepIgnored: keepIgnored, options);
                    }
                    catch (GitException checkoutFailure)
                    {
                        // The rebase is aborted; only the way back failed.
                        var failure = FailureOf(checkoutFailure, OperationTexts.CheckOut(branch).FailureSummary);
                        display = await CheckoutRefusalDisplayAsync(failure, returnBranch, branch, offerStash: false, keepIgnored)
                            ?? new FailureDisplay(OperationTexts.CheckOut(branch).ErrorTitle, failure.Summary);
                        return Failed(text, failure with { Summary = display.Summary });
                    }
                }

                return OperationReport.Done(OperationTexts.Aborted(state.Kind));
            },

            // An abort is not undone (D165): the stopped action's pending entry goes, and the
            // backups list restores what the abort's backups keep.
            new RunOptions { DescribeFailure = _ => display, Undo = UndoScope.Abort }));
    }

    // ---- Helpers -----------------------------------------------------------------------------------

    /// <summary>
    /// What a checkout of <paramref name="target"/> runs (2.1 to 2.3): its name in texts, and the git
    /// calls that answer the success message.
    /// </summary>
    private CheckoutPlan PlanCheckout(string target)
    {
        var name = ShortName(target);
        if (target.StartsWith(HeadsPrefix, StringComparison.Ordinal))
        {
            return new CheckoutPlan(name, async options =>
            {
                await Git.CheckoutBranchAsync(name, keepIgnored: KeepIgnored, options);
                return OperationTexts.CheckedOut(name);
            });
        }

        if (target.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            return new CheckoutPlan(name, options => CheckoutRemoteBranchAsync(target, options));
        }

        // A tag or a commit: HEAD is detached (2.3); git peels a tag to its commit.
        return new CheckoutPlan(name, async options =>
        {
            await Git.CheckoutDetachedAsync(target, keepIgnored: KeepIgnored, options);
            return OperationTexts.DetachedAt(await Git.ResolveAsync("HEAD") ?? target);
        });
    }

    /// <summary>
    /// 2.1's rule for every checkout: while HEAD is detached on commits that nothing else reaches,
    /// asks "Leave these commits?". True when the operation must save <c>detached-&lt;time&gt;</c>
    /// first, false when nothing would be left behind, null when the user did not confirm.
    /// </summary>
    private async Task<bool?> ConfirmLeavingDetachedHeadAsync()
    {
        if (IsClosed)
        {
            return null;
        }

        if (Refs?.Head.IsDetached != true)
        {
            return false;
        }

        var count = await Git.CountDetachedCommitsAsync();
        if (count == 0)
        {
            return false;
        }

        return await ConfirmAsync(OperationTexts.ConfirmLeaveDetachedCommits(count)) ? true : null;
    }

    /// <summary>
    /// 2.1's rule again inside a checkout's operation, where HEAD no longer moves: the question was
    /// asked from the refs as they were when the checkout was chosen, and an operation queued
    /// ahead of it (cherry-picks onto a detached HEAD) or a terminal may have moved HEAD since.
    /// When the user confirmed, saves <c>detached-&lt;time&gt;</c>; otherwise, when HEAD is now
    /// detached on commits that nothing else reaches (and that <paramref name="keptBy"/>, the
    /// start of a new branch, does not keep), refuses: the user was never asked about them.
    /// Null when the checkout may go on.
    /// </summary>
    private async Task<OperationReport?> GuardDetachedHeadAsync(OperationText text, bool backUpDetached, string? keptBy = null)
    {
        if (backUpDetached)
        {
            await SaveDetachedBackupAsync();
            return null;
        }

        var head = await ReadHeadAtStartAsync();
        if (!head.IsDetached)
        {
            return null;
        }

        var count = await Git.CountDetachedCommitsAsync();
        if (count == 0 || (keptBy is not null && await Git.ResolveAsync(keptBy) == head.Sha))
        {
            return null;
        }

        var summary = OperationTexts.DetachedCommitsSinceChosen(count);
        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}");
        return Failed(text, StoppedFailure(summary));
    }

    /// <summary>
    /// An operation on HEAD (merge, reset, cherry-pick, revert, pull) that waited in the queue
    /// checks, inside its operation, that HEAD is still where the user saw it when they chose it
    /// (<paramref name="seen"/>, the refs as last read then): on the same branch, or still
    /// detached, and with <paramref name="sameCommit"/> at the same commit too. Otherwise it
    /// refuses, saying where HEAD went and <paramref name="nothing"/>, so it never acts on a branch
    /// the user did not name. Null when HEAD is where it was, or when no refs had been read.
    /// HEAD is the undo recording's read of it (<see cref="ReadHeadAtStartAsync"/>), or with
    /// <paramref name="fresh"/> read now: for a check after a wait inside the operation (a pull's fetch).
    /// </summary>
    private async Task<OperationReport?> RefuseIfHeadMovedAsync(OperationText text, HeadState? seen, string nothing, bool sameCommit = false, bool fresh = false)
    {
        if (seen is null)
        {
            return null;
        }

        var now = fresh ? await Git.ReadHeadAsync() : await ReadHeadAtStartAsync();
        string summary;
        if (!string.Equals(now.BranchName, seen.BranchName, StringComparison.Ordinal))
        {
            summary = OperationTexts.HeadMoved(seen, now, nothing);
        }
        else if (sameCommit && !string.Equals(now.Sha, seen.Sha, StringComparison.Ordinal))
        {
            summary = OperationTexts.HeadCommitMoved(seen.BranchName ?? "HEAD", seen.Sha ?? string.Empty, now.Sha ?? string.Empty, nothing);
        }
        else
        {
            return null;
        }

        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}");
        return Failed(text, StoppedFailure(summary));
    }

    /// <summary>Saves <c>detached-&lt;time&gt;</c> → HEAD's commit, inside the checkout's operation (2.0, 2.1).</summary>
    private async Task SaveDetachedBackupAsync()
    {
        if (await Git.ResolveAsync("HEAD") is { } head)
        {
            await SaveBackupAsync(new BackupRequest("detached", [new BackupEntry(null, head)]));
        }
    }

    /// <summary>2.31 after a failed checkout: pops the stash back on the commit it was made on; false when the changes stay in the stash.</summary>
    private async Task<bool> PopBackAsync(string stash, NetworkOptions options)
    {
        try
        {
            return await Git.ApplyStashAsync(stash, StashApplyMode.PopKeepingIndex, options) == ApplyResult.Applied;
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: the changes could not be popped back after a failed checkout: {ex.Message}");
            return false;
        }
    }

    /// <summary>The name dialog's start point and context for 2.4: a full SHA, <c>refs/heads/…</c>, <c>refs/remotes/…</c> or <c>refs/tags/&lt;t&gt;^{commit}</c>.</summary>
    private async Task<(string StartPoint, string Context)> DescribeStartAsync(string start)
    {
        if (start.StartsWith(HeadsPrefix, StringComparison.Ordinal) || start.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            return (start, OperationTexts.From(ShortName(start)));
        }

        if (start.StartsWith(TagsPrefix, StringComparison.Ordinal))
        {
            return (start + "^{commit}", OperationTexts.From(ShortName(start)));
        }

        return (start, "From " + await DescribeCommitAsync(start));
    }

    /// <summary>The message of an ended merge (2.8 to 2.10); a conflict is no failure (D95).</summary>
    private async Task<OperationReport> MergeReportAsync(MergeResult result, string source, string current) => result switch
    {
        MergeResult.UpToDate => OperationReport.Done(OperationTexts.MergeUpToDate(current, source)),
        MergeResult.FastForward => OperationReport.Done(OperationTexts.MergeFastForwarded(current, source)),
        MergeResult.Merged => OperationReport.Done(OperationTexts.Merged(source, current)),
        _ => OperationReport.Conflicts(OperationTexts.MergeStopped(await CountConflictedFilesAsync())),
    };

    /// <summary>
    /// A merge, cherry-pick or revert started while another operation is in progress is refused
    /// with the menus' reason "Finish or abort the &lt;kind&gt; first" (design 5), read fresh from the
    /// git folder inside the operation (<see cref="IGitRepository.ReadInProgressStateAsync"/>), so
    /// one queued behind an operation that stopped is refused too.
    /// Null when nothing is in progress.
    /// </summary>
    private async Task<OperationReport?> RefuseWhileInProgressAsync(OperationText text)
    {
        var state = await Git.ReadInProgressStateAsync();
        if (!state.IsActive)
        {
            return null;
        }

        var summary = OperationTexts.FinishOrAbortFirst(state.Kind);
        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}.");
        return Failed(text, StoppedFailure(summary));
    }

    /// <summary>A failure the app tells itself, with no git output and no command (the error dialog then shows the summary alone).</summary>
    private static GitFailure StoppedFailure(string summary) => new(GitFailureKind.Other, summary, string.Empty, string.Empty, null);

    /// <summary>
    /// Design 5.2's check inside an operation a reflog row or a backup started: its commit still
    /// exists (<c>git cat-file -e &lt;sha&gt;^{commit}</c>), since a <c>gc</c> may have pruned it after
    /// the page read it. The refusal ("The commit c7bf508 no longer exists.") changes nothing;
    /// null when it exists.
    /// </summary>
    private async Task<OperationReport?> RefuseIfCommitGoneAsync(OperationText text, string sha) =>
        await Git.CommitExistsAsync(sha) ? null : Refused(text, OperationTexts.CommitGone(sha));

    /// <summary>2.7's dialog: its two sentences when git's line says why, otherwise 2.33's default.</summary>
    private static FailureDisplay? FastForwardFailureDisplay(GitFailure failure, string branch, string target)
    {
        const string Title = "Fast-forward failed";
        if (failure.GitOutput.Contains("refusing to fetch into", StringComparison.Ordinal))
        {
            return new FailureDisplay(Title, OperationTexts.FastForwardInOtherWorktree(branch));
        }

        return failure.GitOutput.Contains("non-fast-forward", StringComparison.Ordinal)
            ? new FailureDisplay(Title, OperationTexts.FastForwardNotBehind(branch, target))
            : null;
    }

    /// <summary>Whether <paramref name="command"/> is the call <c>git &lt;verb&gt; … &lt;sha&gt;</c> (a cherry-pick or revert of that commit).</summary>
    private static bool IsCallOn(string command, string verb, string sha) =>
        command.StartsWith($"git {verb} ", StringComparison.Ordinal) && command.EndsWith(" " + sha, StringComparison.Ordinal);

    /// <summary>Git 2.36's empty revert (D117): it fails with "nothing to commit" and leaves nothing in progress.</summary>
    private static bool ChangesNothing(GitException failure) =>
        failure.StandardOutput.Contains("nothing to commit", StringComparison.Ordinal)
        || failure.StandardError.Contains("nothing to commit", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="sha"/> is a merge commit: from the graph, or read from git.</summary>
    private async Task<bool> IsMergeAsync(string sha) => (await ParentsOfAsync(sha)).Count > 1;

    /// <summary><paramref name="sha"/>'s parents, first parent first: from the graph, or read from git.</summary>
    private async Task<IReadOnlyList<string>> ParentsOfAsync(string sha)
    {
        if (FindCommit(sha) is { } commit)
        {
            return commit.Parents;
        }

        return (await Git.ReadCommitDetailsAsync(sha)).Parents;
    }

    /// <summary>
    /// The commits in the graph's row order: oldest first (read from the bottom up, 2.13) or newest
    /// first (2.14); repeated ones once, those the graph has not loaded last in the order given.
    /// </summary>
    private List<string> OrderByRow(IReadOnlyList<string> commits, bool oldestFirst)
    {
        var graph = Repository.Graph;
        var rows = commits.Distinct(StringComparer.Ordinal).Select(sha => (Sha: sha, Row: graph.IndexOf(sha))).ToList();
        var loaded = rows.Where(commit => commit.Row >= 0);
        var ordered = oldestFirst ? loaded.OrderByDescending(commit => commit.Row) : loaded.OrderBy(commit => commit.Row);
        return [.. ordered.Select(commit => commit.Sha), .. rows.Where(commit => commit.Row < 0).Select(commit => commit.Sha)];
    }

    /// <summary>A checkout's name in texts, and its git calls, which answer the success message.</summary>
    private sealed record CheckoutPlan(string Name, Func<NetworkOptions, Task<string>> Run);
}
