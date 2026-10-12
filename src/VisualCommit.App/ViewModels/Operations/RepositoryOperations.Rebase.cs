// Rebases, the interactive rebase, squash, Continue and Skip: docs/phase-4-design.md sections 1
// to 3 and 5, D135, D142 to D144, with their texts in OperationTexts.Rebase.cs (section 10).
// Built by work package WP4 on the helpers of RepositoryOperations.Run.cs. Its tests are
// tests/VisualCommit.App.Tests/RepositoryOperations{Rebase,Sequence}Tests.cs.
//
// No rebase asks a question (D135): each saves the backup rebase-<time>/<b> of the branch's old
// tip inside its operation, before git runs, and the backups' clean-up follows (D139). Skip asks
// (its question is RepositoryViewModel's, as Abort's) and saves skip-<time>.

using System.Text;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// The file in the rebase folder (<c>rebase-merge/</c>, or <c>rebase-apply/</c> under the apply
    /// backend) that names the branch the user was on before "Check out {A} and rebase it onto
    /// {B}" (1.3, 14.3): its full ref name and a line feed. It goes away with the folder; the
    /// app's Abort checks that branch out again.
    /// </summary>
    public const string ReturnFileName = "visualcommit-return";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    // ---- 1.3: rebase onto a commit or a branch ------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.RebaseAsync"/>
    /// <remarks>
    /// One local operation "Rebase {b} onto {target}": refuses when an operation is in progress or
    /// files conflict, when HEAD moved off the branch since the click (D124) or is detached; saves
    /// the backup; runs <c>git rebase &lt;sha&gt;</c>; reports 1.4's outcome. A stop with nothing
    /// to look at opens the error dialog (5.1). After a rebase, the branch's new tip is selected
    /// when the selected commit was one of the commits it rebased (1.5).
    /// </remarks>
    public Task RebaseAsync(RebaseTarget onto)
    {
        ArgumentNullException.ThrowIfNull(onto);
        var branch = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.RebaseOnto(branch, onto.Name);
        return GuardAsync(text, async () =>
        {
            var notice = new StopNotice();
            var selection = new SelectionAfter();
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (await RefuseRebaseAsync(text, seen) is { } refusal)
                    {
                        return refusal;
                    }

                    var tip = await Git.ResolveAsync("HEAD") ?? throw new InvalidOperationException("HEAD names no commit.");
                    selection.Follows = await SelectedCommitIsRebasedAsync(tip, onto.Sha);
                    await SaveRebaseBackupAsync(branch, tip);
                    var result = await Git.RebaseAsync(onto.Sha, NetworkOptions.From(context));
                    return await RebaseReportAsync(context, result, branch, onto.Name, selection, notice);
                },
                new RunOptions { DescribeFailure = RebaseFailureDisplay });
            await FinishAsync(notice, selection);
        });
    }

    /// <inheritdoc cref="IRepositoryActions.CheckoutAndRebaseAsync"/>
    /// <remarks>
    /// One operation of several steps (1.3): unless <c>rebase.autoStash</c> is set, a working tree
    /// with changes to tracked files is refused before anything changes (the checkout would carry
    /// them along and leave the user on A when git then refuses); then phase 3's checkout of A,
    /// its backup and <c>git rebase &lt;B's tip&gt;</c>. When the rebase stops, the branch the
    /// user was on is written to <see cref="ReturnFileName"/> for the app's Abort. While HEAD is
    /// detached on commits nothing else reaches, phase 3's question comes first (14.3).
    /// </remarks>
    public Task CheckoutAndRebaseAsync(string branchRef, RebaseTarget onto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        ArgumentNullException.ThrowIfNull(onto);
        var branch = ShortName(branchRef);
        var text = OperationTexts.CheckOutAndRebase(branch, onto.Name);
        return GuardAsync(text, async () =>
        {
            if (await ConfirmLeavingDetachedHeadAsync() is not { } backUpDetached)
            {
                return;
            }

            FailureDisplay? display = null;
            var notice = new StopNotice();
            var selection = new SelectionAfter();
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    var options = NetworkOptions.From(context);

                    // 1. Nothing in progress, and a clean working tree unless git stashes the changes itself.
                    if (await RefuseBeforeCheckoutAsync(text) is { } refusal)
                    {
                        display = new FailureDisplay(OperationTexts.RebaseFailedTitle, refusal.Failure!.Summary);
                        return refusal;
                    }

                    if (await GuardDetachedHeadAsync(text, backUpDetached) is { } detached)
                    {
                        return detached;
                    }

                    // 2. Phase 3's checkout.
                    var previous = (await Git.ReadHeadAsync()).BranchName;
                    try
                    {
                        await Git.CheckoutBranchAsync(branch, options);
                    }
                    catch (GitException checkoutFailure)
                    {
                        var failure = FailureOf(checkoutFailure, OperationTexts.CheckOut(branch).FailureSummary);
                        display = CheckoutFailureDisplay(failure, branchRef, offerStash: false)
                            ?? new FailureDisplay(OperationTexts.CheckOut(branch).ErrorTitle, OperationTexts.CheckOut(branch).FailureSummary);
                        return Failed(text, failure with { Summary = display.Summary });
                    }

                    // 3. The backup and the rebase, which the status bar now names (10.1).
                    context.SetRunningText(OperationTexts.RebaseOnto(branch, onto.Name).RunningText);
                    var tip = await Git.ResolveAsync("HEAD") ?? throw new InvalidOperationException("HEAD names no commit.");
                    selection.Follows = await SelectedCommitIsRebasedAsync(tip, onto.Sha);
                    await SaveRebaseBackupAsync(branch, tip);
                    RebaseResult result;
                    try
                    {
                        result = await Git.RebaseAsync(onto.Sha, options);
                    }
                    catch (GitException rebaseFailure)
                    {
                        var failure = FailureOf(rebaseFailure, OperationTexts.RebaseOnto(branch, onto.Name).FailureSummary);
                        display = RebaseFailureDisplay(failure) ?? new FailureDisplay(OperationTexts.RebaseFailedTitle, failure.Summary);
                        return Failed(text, failure with { Summary = display.Summary });
                    }

                    if (result.Outcome == RebaseOutcome.Stopped && previous is not null && !string.Equals(previous, branch, StringComparison.Ordinal))
                    {
                        WriteReturnBranch(HeadsPrefix + previous);
                    }

                    return await RebaseReportAsync(context, result, branch, onto.Name, selection, notice);
                },
                new RunOptions { HasSeveralSteps = true, DescribeFailure = _ => display });
            await FinishAsync(notice, selection);
        });
    }

    // ---- 2.1 to 2.4: the interactive rebase -----------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.OpenInteractiveRebaseAsync"/>
    /// <remarks>
    /// Refuses, in the error dialog and before the list opens, while an operation is in progress
    /// or files conflict (the menus disable the items then; this is for a click that waited);
    /// reads the commits the list holds (2.3, <see cref="IGitRepository.ReadRebaseListAsync"/>)
    /// and hands them to <see cref="RepositoryViewModel.ShowInteractiveRebaseAsync"/>, which
    /// shows them, or the list's own message when they cannot be listed (2.1:
    /// <see cref="OperationTexts.InteractiveListMessage"/>).
    /// </remarks>
    public Task OpenInteractiveRebaseAsync(InteractiveRebaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = OperationTexts.InteractiveRebase(ShortName(request.BranchRef));
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            var status = await Git.ReadStatusAsync();
            if (RefusalOf(status) is { } summary)
            {
                await ShowDialogAsync(new ErrorDialogViewModel(text.ErrorTitle, summary, Repository.Name, clipboard: Services?.Clipboard, log: Log));
                return;
            }

            var list = await Git.ReadRebaseListAsync(request.Onto.Sha, request.BranchRef, request.FromHere);
            if (!IsClosed)
            {
                await Repository.ShowInteractiveRebaseAsync(request, list);
            }
        });
    }

    /// <summary>
    /// The list's Start (2.4), for <see cref="IInteractiveRebaseHost.StartAsync"/>: runs
    /// <paramref name="plan"/> as the operation "Interactive rebase of {b}". It refuses while an
    /// operation is in progress or files conflict, and unless the branch is where the list was
    /// read from (HEAD on it at <see cref="RebasePlan.ExpectedTip"/>; for a drop's source that is
    /// not checked out, its tip unchanged, D124); for that source, 1.3's working-tree check and the
    /// checkout come first. Then the backup and <c>git rebase -i</c> with the app's todo. True when
    /// the rebase is done or stopped (the list closes), false when it was refused or git refused
    /// before anything started (the list stays open; the error dialog shows why).
    /// </summary>
    public Task<bool> RunInteractiveRebaseAsync(RebasePlan plan, InteractiveRebaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);
        var branch = ShortName(request.BranchRef);
        var text = request.CheckoutFirst ? OperationTexts.InteractiveRebaseWithCheckout(branch) : OperationTexts.InteractiveRebase(branch);
        return GuardAsync(
            text,
            async () =>
            {
                var backUpDetached = false;
                if (request.CheckoutFirst)
                {
                    if (await ConfirmLeavingDetachedHeadAsync() is not { } confirmed)
                    {
                        return false;
                    }

                    backUpDetached = confirmed;
                }

                var started = false;
                FailureDisplay? display = null;
                var notice = new StopNotice();
                var selection = new SelectionAfter();
                await RunAsync(
                    text,
                    OperationKind.Local,
                    RefreshScope.Refs,
                    async context =>
                    {
                        var options = NetworkOptions.From(context);
                        if (await RefuseWhileInProgressOrConflictedAsync(text) is { } refusal)
                        {
                            return refusal;
                        }

                        string? previous = null;
                        if (request.CheckoutFirst)
                        {
                            if (!string.Equals(await Git.ResolveAsync(request.BranchRef), plan.ExpectedTip, StringComparison.OrdinalIgnoreCase))
                            {
                                return Refused(text, OperationTexts.ChangedWhileListOpen(branch));
                            }

                            if (await RefuseDirtyTreeAsync(text) is { } dirty)
                            {
                                display = new FailureDisplay(text.ErrorTitle, OperationTexts.RebaseNeedsCleanTree);
                                return dirty;
                            }

                            if (await GuardDetachedHeadAsync(text, backUpDetached) is { } detached)
                            {
                                return detached;
                            }

                            previous = (await Git.ReadHeadAsync()).BranchName;
                            try
                            {
                                await Git.CheckoutBranchAsync(branch, options);
                            }
                            catch (GitException checkoutFailure)
                            {
                                var failure = FailureOf(checkoutFailure, OperationTexts.CheckOut(branch).FailureSummary);
                                display = CheckoutFailureDisplay(failure, request.BranchRef, offerStash: false)
                                    ?? new FailureDisplay(OperationTexts.CheckOut(branch).ErrorTitle, failure.Summary);
                                return Failed(text, failure with { Summary = display.Summary });
                            }

                            // The checkout's step is done: the status bar names the rebase (10.1).
                            context.SetRunningText(OperationTexts.InteractiveRebase(branch).RunningText);
                        }
                        else if (await RefuseIfListMovedAsync(text, plan, branch) is { } moved)
                        {
                            return moved;
                        }

                        selection.Follows = await SelectedCommitIsRebasedAsync(plan.ExpectedTip, plan.Onto);
                        await SaveRebaseBackupAsync(branch, plan.ExpectedTip);
                        RebaseResult result;
                        try
                        {
                            result = await Git.InteractiveRebaseAsync(plan, options);
                        }
                        catch (GitException rebaseFailure)
                        {
                            var failure = FailureOf(rebaseFailure, text.FailureSummary);
                            display = InteractiveFailureDisplay(failure, text);
                            return Failed(text, failure with { Summary = display.Summary });
                        }

                        started = true;
                        if (result.Outcome == RebaseOutcome.Stopped && previous is not null && !string.Equals(previous, branch, StringComparison.Ordinal))
                        {
                            WriteReturnBranch(HeadsPrefix + previous);
                        }

                        if (result.Outcome == RebaseOutcome.Stopped)
                        {
                            return await RebaseStopReportAsync(context, result.ConflictedFiles, notice);
                        }

                        var tip = result.NewTip ?? await Git.ResolveAsync("HEAD") ?? plan.ExpectedTip;
                        selection.NewTip = tip;
                        return Rebased(OperationTexts.InteractiveRebased(branch, await Git.CountCommitsAsync(plan.Onto, tip)), result.AutostashConflicted);
                    },
                    new RunOptions { HasSeveralSteps = request.CheckoutFirst, DescribeFailure = _ => display });
                await FinishAsync(notice, selection);
                return started;
            },
            fallback: false);
    }

    // ---- Section 3: squash several selected commits ----------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.SquashAsync"/>
    /// <remarks>
    /// Reads the "from here" list of the oldest selected commit's parent (2.3), opens the message
    /// dialog "Squash {N} commits" with git's squash message (the commits' messages oldest first,
    /// joined by a blank line), then runs one local operation: the HEAD check (D124), the
    /// in-progress refusal, the backup and an interactive rebase that joins the selected commits
    /// into one group with the dialog's message (<see cref="RebaseTodo.SquashPlan"/>). The squashed
    /// commit is selected (3, 14.3).
    /// </remarks>
    public Task SquashAsync(IReadOnlyList<string> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        var ordered = OrderByRow(commits, oldestFirst: true);
        var count = ordered.Count;
        var branch = HeadName;
        var seen = Refs?.Head;
        var text = OperationTexts.Squash(count);
        return GuardAsync(text, async () =>
        {
            if (IsClosed || count < 2 || seen is not { BranchName: { } current })
            {
                // The menu disables it then (section 3).
                Log.Info($"{Repository.Name}: {text.Title}: not started, {(count < 2 ? "fewer than two commits" : "HEAD is not on a branch")}.");
                return;
            }

            var branchRef = HeadsPrefix + current;
            var parent = (FindCommit(ordered[0])?.Parents ?? (await Git.ReadCommitDetailsAsync(ordered[0])).Parents).FirstOrDefault()
                ?? throw new InvalidOperationException("The first commit cannot be squashed.");
            var list = await Git.ReadRebaseListAsync(parent, branchRef, fromCommit: true);
            var message = RebaseTodo.SquashMessage(list.CommitsOldestFirst.Take(count).Select(entry => entry.Message));
            if (await ShowDialogAsync(new MessageDialogViewModel(text.Title, message, SquashConfirmText)) is not { } squashMessage)
            {
                return;
            }

            var plan = RebaseTodo.SquashPlan(list, branchRef, ordered, squashMessage);
            var notice = new StopNotice();
            var selection = new SelectionAfter();
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (await RefuseWhileInProgressOrConflictedAsync(text) is { } refusal)
                    {
                        return refusal;
                    }

                    if (await RefuseIfListMovedAsync(text, plan, branch, squash: true) is { } moved)
                    {
                        return moved;
                    }

                    await SaveRebaseBackupAsync(branch, plan.ExpectedTip);
                    var result = await Git.InteractiveRebaseAsync(plan, NetworkOptions.From(context));
                    if (result.Outcome == RebaseOutcome.Stopped)
                    {
                        return await RebaseStopReportAsync(context, result.ConflictedFiles, notice);
                    }

                    // The squashed commit is the new tip's ancestor by the commits after it (WP1, 14.2).
                    var tip = result.NewTip ?? await Git.ResolveAsync("HEAD") ?? plan.ExpectedTip;
                    var after = list.CommitsOldestFirst.Count - count;
                    var squashed = after == 0 ? tip : await Git.ResolveAsync($"{tip}~{after}") ?? tip;
                    selection.Follows = true;
                    selection.NewTip = squashed;
                    return Rebased(OperationTexts.Squashed(count, squashed), result.AutostashConflicted);
                },
                new RunOptions { DescribeFailure = failure => InteractiveRefusalDisplay(failure, text, squash: true) });
            await FinishAsync(notice, selection);
        });
    }

    /// <summary>The confirm button of the squash's message dialog (section 3).</summary>
    public const string SquashConfirmText = "Squash";

    // ---- 5.4: Continue and Skip --------------------------------------------------------------------------

    /// <inheritdoc cref="IInProgressActions.ContinueAsync"/>
    public Task ContinueAsync(InProgressState state, string? message)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActive || state.Kind == InProgressKind.ApplyMailbox)
        {
            return Task.CompletedTask;
        }

        var text = OperationTexts.Continue(state.Kind);
        return GuardAsync(text, async () =>
        {
            // 5.1's markers: the paths that were conflicted at the stop, staged with markers, ask first.
            if (!await ConfirmStagedMarkersAsync(state))
            {
                return;
            }

            var onto = OntoName(state);
            var notice = new StopNotice();
            var selection = new SelectionAfter();
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    // A rebase's Continue is also refused while other tracked files are unstaged
                    // (git's --continue would refuse after the app's commit, the review's finding 1).
                    var status = await Git.ReadStatusAsync();
                    var now = status.InProgress;
                    var conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
                    var button = ConflictBannerViewModel.ContinueFor(now, conflicts, status.HasUnstagedTrackedChanges);
                    if (RefuseChangedState(text, state, now, conflicts, button, OperationTexts.NothingContinued) is { } refusal)
                    {
                        return refusal;
                    }

                    await FollowBranchAsync(now, selection);
                    var result = await Git.ContinueAsync(now, message, NetworkOptions.From(context));
                    return await SequenceStepReportAsync(context, now, result, onto, skipped: false, selection, notice);
                },
                new RunOptions { HasSeveralSteps = true, DescribeFailure = failure => ContinueRefusalDisplay(failure, text) });
            await FinishAsync(notice, selection);
        });
    }

    /// <summary>
    /// The dialog of the git layer's refusal of a rebase's Continue while other tracked files are
    /// unstaged (<see cref="GitRepository.RebaseUnstagedChangesMessage"/>): "Continue failed" with
    /// <see cref="OperationTexts.ContinueNeedsNothingUnstaged"/> as its summary; null for any other
    /// failure (2.33's default).
    /// </summary>
    private static FailureDisplay? ContinueRefusalDisplay(GitFailure failure, OperationText text) =>
        failure.GitOutput.Contains(GitRepository.RebaseUnstagedChangesMessage, StringComparison.Ordinal)
            ? new FailureDisplay(text.ErrorTitle, OperationTexts.ContinueNeedsNothingUnstaged + ".")
            : null;

    /// <inheritdoc cref="IInProgressActions.SkipAsync"/>
    public Task SkipAsync(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActive || state.Kind is InProgressKind.ApplyMailbox or InProgressKind.Merge)
        {
            return Task.CompletedTask;
        }

        var text = OperationTexts.Skip(state.Commit ?? string.Empty);
        return GuardAsync(text, async () =>
        {
            var onto = OntoName(state);
            var notice = new StopNotice();
            var selection = new SelectionAfter();
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    var (now, conflicts) = await FreshStateAsync();
                    if (RefuseChangedState(text, state, now, conflicts, ConflictBannerViewModel.SkipFor(now), OperationTexts.NothingSkipped) is { } refusal)
                    {
                        return refusal;
                    }

                    // Skip loses the stopped commit's resolution: HEAD and the changes are kept first (5.1, D144).
                    var head = await Git.ResolveAsync("HEAD");
                    var changes = await Git.SnapshotTrackedChangesAsync();
                    await SaveBackupAsync(new BackupRequest("skip", head is null ? [] : [new BackupEntry(null, head)]) { ChangesCommit = changes });
                    await FollowBranchAsync(now, selection);
                    var result = await Git.SkipAsync(now, NetworkOptions.From(context));
                    return await SequenceStepReportAsync(context, now, result, onto, skipped: true, selection, notice);
                },
                new RunOptions { HasSeveralSteps = true, DescribeFailure = failure => ContinueRefusalDisplay(failure, text) });
            await FinishAsync(notice, selection);
        });
    }

    // ---- The rebase folder's return file (1.3, 14.3) ---------------------------------------------------

    /// <summary>
    /// The branch a rebase begun with "Check out {A} and rebase it onto {B}" checks out again after
    /// the app's Abort (<see cref="ReturnFileName"/>): its full ref name, or null when the rebase in
    /// progress has no such file (a rebase from a terminal, or one that did not check out first).
    /// </summary>
    public string? ReadReturnBranch()
    {
        try
        {
            foreach (var folder in RebaseFolders())
            {
                var path = Path.Combine(folder, ReturnFileName);
                if (File.Exists(path))
                {
                    var line = File.ReadAllText(path).Split('\n')[0].Trim();
                    return line.StartsWith(HeadsPrefix, StringComparison.Ordinal) && line.Length > HeadsPrefix.Length ? line : null;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"{Repository.Name}: the rebase's {ReturnFileName} could not be read.", ex);
        }

        return null;
    }

    /// <summary>Writes <see cref="ReturnFileName"/> into the rebase folder that exists, UTF-8 without a byte order mark; logged, never failing the rebase.</summary>
    private void WriteReturnBranch(string fullRef)
    {
        try
        {
            if (RebaseFolders().FirstOrDefault() is { } folder)
            {
                File.WriteAllText(Path.Combine(folder, ReturnFileName), fullRef + "\n", Utf8WithoutBom);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"{Repository.Name}: the rebase's {ReturnFileName} could not be written; Abort will not check {fullRef} out again.", ex);
        }
    }

    /// <summary>The rebase folders that exist in the repository's (worktree's) git folder: <c>rebase-merge/</c> first.</summary>
    private IEnumerable<string> RebaseFolders() =>
        new[] { "rebase-merge", "rebase-apply" }.Select(name => Path.Combine(Git.GitDirectory, name)).Where(Directory.Exists);

    // ---- Helpers ---------------------------------------------------------------------------------------

    /// <summary>
    /// The name of a branch that points at a rebase's new base, from the refs as last read (5.2,
    /// 14.3): a local branch first, then a remote branch, each in git's order; its short SHA when
    /// none does. Null for any other operation.
    /// </summary>
    public string? OntoName(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state is not { Kind: InProgressKind.Rebase, Onto: { } onto })
        {
            return null;
        }

        var refs = Refs?.Refs ?? [];
        return refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.LocalBranch && gitRef.TargetSha == onto)?.Name
            ?? refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.RemoteBranch && gitRef.TargetSha == onto)?.Name
            ?? OperationTexts.Short(onto);
    }

    /// <summary>Saves <c>rebase-&lt;time&gt;/&lt;b&gt;</c> → <paramref name="tip"/>, the branch's old tip (1.2).</summary>
    private Task SaveRebaseBackupAsync(string branch, string tip) =>
        SaveBackupAsync(new BackupRequest("rebase", [new BackupEntry(branch, tip)]));

    /// <summary>
    /// 1.3's refusals of a rebase onto, read inside its operation: an operation in progress or
    /// files that conflict, HEAD no longer on the branch the item named (D124), HEAD detached.
    /// </summary>
    private async Task<OperationReport?> RefuseRebaseAsync(OperationText text, HeadState? seen)
    {
        if (await RefuseWhileInProgressOrConflictedAsync(text) is { } refusal)
        {
            return refusal;
        }

        if (await RefuseIfHeadMovedAsync(text, seen, OperationTexts.NothingRebased) is { } moved)
        {
            return moved;
        }

        return (await Git.ReadHeadAsync()).BranchName is null
            ? Refused(text, OperationTexts.MenuTexts.CheckOutABranchToRebase)
            : null;
    }

    /// <summary>
    /// A refusal while an operation is in progress ("Finish or abort the {kind} first") or files
    /// conflict without one ("Resolve the conflicts first"), from a fresh status (1.3); null otherwise.
    /// </summary>
    private async Task<OperationReport?> RefuseWhileInProgressOrConflictedAsync(OperationText text) =>
        RefusalOf(await Git.ReadStatusAsync()) is { } summary ? Refused(text, summary) : null;

    /// <summary>Why a rebase cannot start on <paramref name="status"/>: the menus' reasons (D119), or null.</summary>
    private static string? RefusalOf(WorkingTreeStatus status) =>
        status.InProgress.IsActive ? OperationTexts.FinishOrAbortFirst(status.InProgress.Kind)
        : status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted) ? OperationTexts.MenuTexts.ResolveTheConflictsFirst
        : null;

    /// <summary>
    /// 1.3's step 1: a working tree with staged or unstaged changes to tracked files is refused
    /// (untracked files are git's to judge), unless git stashes them itself
    /// (<see cref="RebaseStashesChangesAsync"/>); null when the rebase may go on.
    /// </summary>
    private async Task<OperationReport?> RefuseDirtyTreeAsync(OperationText text)
    {
        if (await RebaseStashesChangesAsync())
        {
            return null;
        }

        return IsDirty(await Git.ReadStatusAsync()) ? Refused(text, OperationTexts.RebaseNeedsCleanTree) : null;
    }

    /// <summary>
    /// 1.3's step 1 of "Check out {A} and rebase it onto {B}", before the checkout: git's
    /// <c>rebase.autoStash</c> (<see cref="RebaseStashesChangesAsync"/>), then one status read,
    /// refused while an operation is in progress or files conflict, and for staged or unstaged
    /// changes to tracked files unless git stashes them itself; null when it may go on.
    /// </summary>
    private async Task<OperationReport?> RefuseBeforeCheckoutAsync(OperationText text)
    {
        var autoStash = await RebaseStashesChangesAsync();
        var status = await Git.ReadStatusAsync();
        if (RefusalOf(status) is { } summary)
        {
            return Refused(text, summary);
        }

        return !autoStash && IsDirty(status) ? Refused(text, OperationTexts.RebaseNeedsCleanTree) : null;
    }

    /// <summary>Staged or unstaged changes to tracked files (untracked files are git's to judge, 1.3).</summary>
    private static bool IsDirty(WorkingTreeStatus status) =>
        status.Staged.Count > 0 || status.Unstaged.Any(file => !status.IsUntracked(file.Path));

    /// <summary>
    /// Whether git's configuration has <c>rebase.autoStash</c> set (1.3, D142: <c>git config
    /// --type=bool --get rebase.autoStash</c>), so that git stashes the changes itself.
    /// </summary>
    private Task<bool> RebaseStashesChangesAsync() => Git.ReadRebaseAutoStashAsync();

    /// <summary>
    /// Start's refusal when the branch is not where the list was read from (2.4, D124): HEAD on
    /// another branch ("HEAD moved while this waited: …") or the branch at another commit ("{b}
    /// changed while the list was open: open it again."); null when it is where it was. A
    /// <paramref name="squash"/> has no list: "{b} changed while this waited: select the commits
    /// again. Nothing was squashed." (the review's finding 16).
    /// </summary>
    private async Task<OperationReport?> RefuseIfListMovedAsync(OperationText text, RebasePlan plan, string branch, bool squash = false)
    {
        var head = await Git.ReadHeadAsync();
        if (!string.Equals(HeadsPrefix + head.BranchName, plan.BranchRef, StringComparison.Ordinal))
        {
            return Refused(text, OperationTexts.HeadMoved(new HeadState(branch, plan.ExpectedTip), head, squash ? OperationTexts.NothingSquashed : OperationTexts.NothingRebased));
        }

        return string.Equals(head.Sha, plan.ExpectedTip, StringComparison.OrdinalIgnoreCase)
            ? null
            : Refused(text, squash ? OperationTexts.ChangedWhileSquashWaited(branch) : OperationTexts.ChangedWhileListOpen(branch));
    }

    /// <summary>A refusal the app tells itself before git runs (no git output), logged.</summary>
    private OperationReport Refused(OperationText text, string summary)
    {
        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}");
        return Failed(text, StoppedFailure(summary));
    }

    /// <summary>
    /// 1.4's table for a rebase the app started: up to date, rebased (with a warning when the
    /// changes git stashed conflicted when put back), or stopped (<see cref="RebaseStopReportAsync"/>).
    /// </summary>
    private async Task<OperationReport> RebaseReportAsync(OperationContext context, RebaseResult result, string branch, string target, SelectionAfter selection, StopNotice notice)
    {
        switch (result.Outcome)
        {
            case RebaseOutcome.UpToDate:
                return OperationReport.Done(OperationTexts.RebaseUpToDate(branch, target));

            case RebaseOutcome.Rebased:
                selection.NewTip = result.NewTip ?? await Git.ResolveAsync("HEAD");
                return Rebased(OperationTexts.Rebased(branch, target), result.AutostashConflicted);

            default:
                return await RebaseStopReportAsync(context, result.ConflictedFiles, notice);
        }
    }

    /// <summary>
    /// A rebase that ended rebased (1.4): <paramref name="done"/> as a success, or, when the
    /// changes <c>rebase.autoStash</c> put back conflicted, as <see cref="OperationReport.Conflicts"/>
    /// with 1.4's warning after it (<see cref="OperationTexts.WithAutostashConflicted"/>), so that
    /// the status bar says the changes are also kept in the stash and the banner shows the
    /// conflicted files: for every rebase the app runs (the review's findings 12 and 19).
    /// </summary>
    private static OperationReport Rebased(string done, bool autostashConflicted) =>
        autostashConflicted ? OperationReport.Conflicts(OperationTexts.WithAutostashConflicted(done)) : OperationReport.Done(done);

    /// <summary>
    /// A rebase that stopped (1.4, 5.1): "Rebase stopped: {N} files have conflicts", or with none
    /// "Rebase stopped at {short} "{subject}"" (warning; no dialog, D95). A stop with nothing to look
    /// at that is not empty (an untracked file in the way, a failed command, the app's own amend)
    /// also opens the error dialog "Rebase stopped" with git's output and the banner's text.
    /// </summary>
    private async Task<OperationReport> RebaseStopReportAsync(OperationContext context, int conflicts, StopNotice notice)
    {
        if (conflicts > 0)
        {
            return OperationReport.Conflicts(OperationTexts.RebaseStopped(conflicts));
        }

        var state = (await Git.ReadStatusAsync()).InProgress;
        if (state.StopKind is RebaseStopKind.Rescheduled or RebaseStopKind.ExecFailed or RebaseStopKind.OwnAmendFailed)
        {
            var summary = ConflictBannerViewModel.TextFor(state, 0, OntoName(state)) ?? OperationTexts.RebaseStoppedAt(state.Commit ?? string.Empty, state.Subject);
            notice.Display = new FailureDisplay(OperationTexts.RebaseStoppedTitle, summary);
            notice.Failure = FailureOfLastCall(context, command => command.StartsWith("git rebase", StringComparison.Ordinal) || command.StartsWith("git commit", StringComparison.Ordinal), summary);
        }

        return OperationReport.Conflicts(OperationTexts.RebaseStoppedAt(state.Commit ?? string.Empty, state.Subject));
    }

    /// <summary>
    /// How Continue or Skip ended (5.4, 10.2): done ("Merge committed", "Cherry-pick finished",
    /// "Revert finished", "Rebased {b} onto {onto}"; "Skipped {short}" for a single pick or revert
    /// skipped), or at the next stop, told as the stops of 1.4 and section 4 are.
    /// </summary>
    private async Task<OperationReport> SequenceStepReportAsync(
        OperationContext context, InProgressState state, ContinueResult result, string? onto, bool skipped, SelectionAfter selection, StopNotice notice)
    {
        var (now, conflicts) = await FreshStateAsync();
        if (result.Outcome == ContinueOutcome.Done || !now.IsActive)
        {
            if (state.Kind == InProgressKind.Rebase)
            {
                selection.NewTip = result.NewTip ?? await Git.ResolveAsync("HEAD");
            }

            var single = !ConflictBannerViewModel.InList(state) && state.Kind is InProgressKind.CherryPick or InProgressKind.Revert;
            return Rebased(
                skipped && single ? OperationTexts.Skipped(state.Commit ?? string.Empty) : OperationTexts.ContinueDone(state.Kind, state.Branch, onto ?? string.Empty),
                result.AutostashConflicted);
        }

        if (now.Kind == InProgressKind.Rebase)
        {
            return await RebaseStopReportAsync(context, conflicts, notice);
        }

        var stop = ListStopOf(now, conflicts);
        if (stop == SequenceStop.Refused)
        {
            // 5.1: a stop with nothing to look at opens the error dialog, as at the list's first run.
            var summary = ConflictBannerViewModel.TextFor(now, conflicts) ?? ListStepStopped(now, conflicts, stop);
            notice.Display = new FailureDisplay(now.Kind == InProgressKind.Revert ? OperationTexts.RevertStoppedTitle : OperationTexts.CherryPickStoppedTitle, summary);
            notice.Failure = FailureOfLastCall(context, command => command.StartsWith($"git {(now.Kind == InProgressKind.Revert ? "revert" : "cherry-pick")} ", StringComparison.Ordinal), summary);
        }

        return OperationReport.Conflicts(ListStepStopped(now, conflicts, stop));
    }

    /// <summary>
    /// Where a list stopped after Continue or Skip, as the git layer tells the first run's stops
    /// (<see cref="SequenceStop"/>): a commit that changes nothing; one git could not apply without
    /// a conflict (git's list with no pick head and nothing to commit); else conflicts, also with no
    /// file conflicted (rerere's <c>autoUpdate</c>, D128).
    /// </summary>
    private static SequenceStop ListStopOf(InProgressState state, int conflicts) =>
        conflicts > 0 ? SequenceStop.Conflicts
        : state.Empty ? SequenceStop.Empty
        : state is { Sequenced: true, HasPickHead: false, AbortKeepsHead: false } ? SequenceStop.Refused
        : SequenceStop.Conflicts;

    /// <summary>
    /// The status bar's text when Continue or Skip of a list stopped again (10.2, as at its first
    /// run): "Cherry-picked {K} of {N} commits; stopped at {short} with conflicts in {M} files",
    /// "…; {short} changes nothing", "…; git could not pick {short}" ("Reverted …"), with K and N
    /// from git's list as it is now (<see cref="InProgressState.Done"/>, <see cref="InProgressState.Total"/>).
    /// </summary>
    private static string ListStepStopped(InProgressState state, int conflicts, SequenceStop stop)
    {
        var sha = state.Commit ?? string.Empty;
        return stop switch
        {
            SequenceStop.Empty => OperationTexts.ListStoppedEmpty(state.Kind, state.Done, state.Total, sha),
            SequenceStop.Refused => OperationTexts.ListStoppedRefused(state.Kind, state.Done, state.Total, sha),
            _ => OperationTexts.ListStoppedWithConflicts(state.Kind, state.Done, state.Total, sha, conflicts),
        };
    }

    /// <summary>The in-progress state and the number of conflicted files as they are now (a fresh status read).</summary>
    private async Task<(InProgressState State, int Conflicts)> FreshStateAsync()
    {
        var status = await Git.ReadStatusAsync();
        return (status.InProgress, status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted));
    }

    /// <summary>
    /// Continue's and Skip's refusal when the state changed since the click (5.4): nothing in
    /// progress any more, another kind or another stopped commit, or the button would be disabled
    /// now (conflicts back: its reason); null when it may go on.
    /// </summary>
    private OperationReport? RefuseChangedState(OperationText text, InProgressState clicked, InProgressState now, int conflicts, (bool Shown, string? DisabledReason) button, string nothing)
    {
        var same = now.IsActive
            && now.Kind == clicked.Kind
            && string.Equals(now.Commit, clicked.Commit, StringComparison.Ordinal)
            && now.Done == clicked.Done;
        if (!same)
        {
            return Refused(text, OperationTexts.StateChangedWhileWaiting(clicked.Kind, nothing));
        }

        return !button.Shown ? Refused(text, OperationTexts.StateChangedWhileWaiting(clicked.Kind, nothing))
            : button.DisabledReason is { } reason ? Refused(text, $"{reason}. {nothing}")
            : null;
    }

    /// <summary>
    /// For a rebase that Continue or Skip may finish (1.5): whether the selected commit is one of
    /// the branch's commits being rebased (its tip is still the old one while the rebase runs).
    /// </summary>
    private async Task FollowBranchAsync(InProgressState state, SelectionAfter selection)
    {
        if (state is { Kind: InProgressKind.Rebase, Branch: { } branch, Onto: { } onto } && await Git.ResolveAsync(HeadsPrefix + branch) is { } oldTip)
        {
            selection.Follows = await SelectedCommitIsRebasedAsync(oldTip, onto);
        }
    }

    /// <summary>
    /// Whether the commit selected in the graph is one of the commits a rebase of
    /// <paramref name="oldTip"/> onto <paramref name="onto"/> replays (1.5): reachable from the old
    /// tip and not from the new base. False for no commit, several, or the working-changes row.
    /// </summary>
    private async Task<bool> SelectedCommitIsRebasedAsync(string oldTip, string onto)
    {
        if (Repository.Selection.Single is not { } selected || Repository.IsWorkingRowSelected)
        {
            return false;
        }

        try
        {
            return await Git.IsAncestorAsync(selected, oldTip) && !await Git.IsAncestorAsync(selected, onto);
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: whether {selected} is rebased could not be read; the selection stays.", ex);
            return false;
        }
    }

    /// <summary>
    /// After a rebase's operation: opens the error dialog of a stop with nothing to look at
    /// (<see cref="StopNotice"/>), and selects the rebased branch's new tip when 1.5 says so.
    /// </summary>
    private async Task FinishAsync(StopNotice notice, SelectionAfter selection)
    {
        if (IsClosed)
        {
            return;
        }

        if (selection is { Follows: true, NewTip: { } tip })
        {
            Repository.Selection = GraphSelection.Of(tip);
        }

        if (notice is { Display: { } display, Failure: { } failure })
        {
            await ShowFailureAsync(display, failure);
        }
    }

    /// <summary>
    /// The dialog of a rebase's failure (1.4, 10.1): "Rebase failed" with its special summaries
    /// for local changes and for untracked files in the way; null for git's other refusals
    /// (2.33's default: the title and summary of the operation's texts).
    /// </summary>
    private static FailureDisplay? RebaseFailureDisplay(GitFailure failure) =>
        RebaseRefusalSummary(failure) is { } summary ? new FailureDisplay(OperationTexts.RebaseFailedTitle, summary) : null;

    /// <summary>The dialog of an interactive rebase's or a squash's failure: <paramref name="text"/>'s title, with 10.1's special summaries and D124's.</summary>
    private static FailureDisplay InteractiveFailureDisplay(GitFailure failure, OperationText text) =>
        InteractiveRefusalDisplay(failure, text) ?? new(text.ErrorTitle, text.FailureSummary);

    /// <summary>
    /// The dialog of a git refusal of an interactive rebase or a squash that has a summary of its
    /// own (10.1's special summaries, the git layer's D124 refusal); null for anything else, so
    /// that the app's own refusals keep their summaries ("Finish or abort the merge first",
    /// "{b} changed while the list was open: …") and git's other failures get 2.33's default.
    /// </summary>
    private static FailureDisplay? InteractiveRefusalDisplay(GitFailure failure, OperationText text, bool squash = false) =>
        RebaseRefusalSummary(failure) is { } summary ? new FailureDisplay(text.ErrorTitle, summary)
        : failure.GitOutput.Contains(RebaseHeadMoved, StringComparison.Ordinal) ? new FailureDisplay(text.ErrorTitle, squash ? OperationTexts.HeadMovedSinceSquash : OperationTexts.HeadMovedSinceList)
        : null;

    /// <summary>The git layer's refusal of a plan whose branch moved (<c>GitRepository.RebaseHeadMovedMessage</c>).</summary>
    private const string RebaseHeadMoved = "HEAD is no longer on the branch at the commit the list was read from.";

    /// <summary>10.1's special summaries, by git's classifying lines (the same on Git 2.36 and 2.55).</summary>
    private static string? RebaseRefusalSummary(GitFailure failure)
    {
        var output = failure.GitOutput;
        if (output.Contains("cannot rebase: You have unstaged changes", StringComparison.Ordinal)
            || output.Contains("cannot rebase: Your index contains uncommitted changes", StringComparison.Ordinal))
        {
            return OperationTexts.RebaseNeedsCleanTree;
        }

        return output.Contains("untracked working tree files would be overwritten", StringComparison.Ordinal)
            ? OperationTexts.RebaseUntrackedInTheWay
            : null;
    }

    /// <summary>The error dialog a stop with nothing to look at opens after its operation (5.1), set inside the operation.</summary>
    private sealed class StopNotice
    {
        public FailureDisplay? Display { get; set; }

        public GitFailure? Failure { get; set; }
    }

    /// <summary>1.5's selection after a rebase: whether it follows the branch, and the commit it goes to.</summary>
    private sealed class SelectionAfter
    {
        /// <summary>The selected commit was one of the commits the rebase replays (or the squash's, 3).</summary>
        public bool Follows { get; set; }

        /// <summary>The commit to select when the rebase ends rebased: the branch's new tip, or the squashed commit.</summary>
        public string? NewTip { get; set; }
    }
}
