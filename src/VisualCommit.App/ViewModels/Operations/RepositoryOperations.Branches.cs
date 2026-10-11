// Branches and history: docs/phase-3-design.md 2.0 to 2.15, 2.31, 4g's "Check out and merge",
// section 5's Abort, and their texts in 2.33 (OperationTexts), built by WP7-branches on the helpers
// of RepositoryOperations.Run.cs. Its tests are tests/VisualCommit.App.Tests/RepositoryOperationsTests.Branches.cs.

using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations : IInProgressActions
{
    // ---- Checkouts (2.1 to 2.3, 2.31, 4g) ---------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CheckoutAsync"/>
    public Task CheckoutAsync(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var plan = PlanCheckout(target);
        var text = OperationTexts.CheckOut(plan.Name);
        return GuardAsync(text, async () =>
        {
            if (await ConfirmLeavingDetachedHeadAsync() is not { } backUpDetached)
            {
                return;
            }

            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (backUpDetached)
                    {
                        await SaveDetachedBackupAsync();
                    }

                    return OperationReport.Done(await plan.Run(NetworkOptions.From(context)));
                },
                new RunOptions { DescribeFailure = failure => CheckoutFailureDisplay(failure, target, offerStash: true) });
        });
    }

    /// <inheritdoc cref="IRepositoryActions.StashAndCheckoutAsync"/>
    public Task StashAndCheckoutAsync(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var plan = PlanCheckout(target);
        var name = plan.Name;
        var text = OperationTexts.StashAndCheckOut(name);
        return GuardAsync(text, async () =>
        {
            if (await ConfirmLeavingDetachedHeadAsync() is not { } backUpDetached)
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
                    if (backUpDetached)
                    {
                        await SaveDetachedBackupAsync();
                    }

                    // 1. The tracked changes only: the untracked files blocked nothing, and the
                    // checkout carries them along (D91).
                    var stash = await Git.StashAsync(OperationTexts.StashBeforeCheckout(name), includeUntracked: false, options);
                    if (stash is null)
                    {
                        // Nothing is left to stash (the changes went meanwhile): a plain checkout.
                        return OperationReport.Done(await plan.Run(options));
                    }

                    // 2. The checkout; when it fails, the changes go back at once.
                    try
                    {
                        await plan.Run(options);
                    }
                    catch (GitException checkoutFailure)
                    {
                        var summary = await PopBackAsync(stash, options) ? text.FailureSummary : OperationTexts.CheckoutFailedChangesKept(name);
                        return Failed(text, FailureOf(checkoutFailure, summary));
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
                new RunOptions { HasSeveralSteps = true, DescribeFailure = _ => display });

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
                    if (await RefuseWhileInProgressAsync(text) is { } refusal)
                    {
                        display = new FailureDisplay(mergeText.ErrorTitle, refusal.Failure!.Summary);
                        return refusal;
                    }

                    var options = NetworkOptions.From(context);
                    if (backUpDetached)
                    {
                        await SaveDetachedBackupAsync();
                    }

                    // A failed checkout ends it with the checkout's dialog (2.33), without the stash
                    // button: the merge would then run on top of re-applied changes (4g).
                    await Git.CheckoutBranchAsync(branch, options);
                    try
                    {
                        return await MergeReportAsync(await Git.MergeAsync(sourceRef, MergeMode.Default, options), source, branch);
                    }
                    catch (GitException mergeFailure)
                    {
                        display = new FailureDisplay(mergeText.ErrorTitle, mergeText.FailureSummary);
                        return Failed(text, FailureOf(mergeFailure, mergeText.FailureSummary));
                    }
                },
                new RunOptions
                {
                    HasSeveralSteps = true,
                    DescribeFailure = failure => display ?? CheckoutFailureDisplay(failure, branchRef, offerStash: false),
                });
        });
    }

    // ---- Branches (2.4 to 2.7) --------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CreateBranchAsync"/>
    public async Task CreateBranchAsync(string? start)
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

        var (startPoint, context) = await DescribeStartAsync(start ?? head!.Sha!);
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
            if (checkout && head?.IsDetached == true && await Git.ResolveAsync(startPoint) != head.Sha)
            {
                if (await ConfirmLeavingDetachedHeadAsync() is not { } confirmed)
                {
                    return;
                }

                backUpDetached = confirmed;
            }

            await RunAsync(text, OperationKind.Local, RefreshScope.Refs, async operation =>
            {
                if (backUpDetached)
                {
                    await SaveDetachedBackupAsync();
                }

                await Git.CreateBranchAsync(name, startPoint, checkout, NetworkOptions.From(operation));
                return OperationReport.Done(OperationTexts.CreatedBranch(name, checkout));
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
        await GuardAsync(text, () => RunAsync(text, OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await Git.RenameBranchAsync(branch, newName);
            return OperationReport.Done(OperationTexts.RenamedBranch(branch, newName));
        }));
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
                    return OperationReport.Done(OperationTexts.DeletedBranch(branch));
                });
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
            new RunOptions { Prompts = PromptMode.None, DescribeFailure = failure => FastForwardFailureDisplay(failure, branch, target) }));
    }

    // ---- Merge (2.8 to 2.10) ----------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.MergeAsync"/>
    public Task MergeAsync(string sourceRef, MergeMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        var source = ShortName(sourceRef);
        var current = HeadName;
        var text = mode == MergeMode.FastForwardOnly ? OperationTexts.FastForward(current, source) : OperationTexts.Merge(source, current);
        return GuardAsync(text, () => RunAsync(text, OperationKind.Local, RefreshScope.Refs, async context =>
        {
            if (await RefuseWhileInProgressAsync(text) is { } refusal)
            {
                return refusal;
            }

            return await MergeReportAsync(await Git.MergeAsync(sourceRef, mode, NetworkOptions.From(context)), source, current);
        }));
    }

    // ---- Reset (2.11) -----------------------------------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.ResetAsync"/>
    public Task ResetAsync(string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var text = OperationTexts.Reset(current, commit);
        return GuardAsync(text, async () =>
        {
            if (IsClosed || Refs?.Head.IsUnborn != false)
            {
                // Nothing to reset before the first commit: the menus disable it (2.11).
                return;
            }

            // The dialog is the confirmation, and the only one (2.11).
            var subject = await ReadSubjectAsync(commit) ?? string.Empty;
            if (await ShowDialogAsync(new ResetDialogViewModel(current, commit, subject)) is not { } mode)
            {
                return;
            }

            await RunAsync(text, OperationKind.Local, RefreshScope.Refs, async context =>
            {
                var head = await Git.ResolveAsync("HEAD") ?? throw new InvalidOperationException("HEAD names no commit.");
                var changes = mode == ResetMode.Hard ? await Git.SnapshotForHardResetAsync(commit) : null;
                await SaveBackupAsync(new BackupRequest("reset", [new BackupEntry(null, head)]) { ChangesCommit = changes });
                await Git.ResetAsync(commit, mode, NetworkOptions.From(context));
                return OperationReport.Done(OperationTexts.ResetDone(current, commit, mode));
            });
        });
    }

    // ---- Cherry-pick and revert (2.12 to 2.14) ----------------------------------------------------

    /// <inheritdoc cref="IRepositoryActions.CherryPickAsync"/>
    public Task CherryPickAsync(string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var text = OperationTexts.CherryPick(commit);
        FailureDisplay? display = null;
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

                switch (await Git.CherryPickAsync(commit, NetworkOptions.From(context)))
                {
                    case ApplyResult.Applied:
                        return OperationReport.Done(OperationTexts.CherryPicked(commit, current));

                    case ApplyResult.Conflicts:
                        return OperationReport.Conflicts(OperationTexts.CherryPickConflicts(await CountConflictedFilesAsync()));

                    default:
                        // An empty pick stops with CHERRY_PICK_HEAD: a failure with a dialog of its
                        // own, and the banner offers Abort (2.12).
                        display = new FailureDisplay(OperationTexts.CherryPickStoppedTitle, OperationTexts.CherryPickEmpty(commit, current));
                        return Failed(text, StoppedFailure(display.Summary));
                }
            },
            new RunOptions { DescribeFailure = _ => display }));
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
        var text = OperationTexts.CherryPickSeveral(count);
        FailureDisplay? display = null;
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

                // One git cherry-pick per commit, never git's sequencer (2.13, D88).
                var options = NetworkOptions.From(context);
                for (var done = 0; done < count; done++)
                {
                    var sha = ordered[done];
                    ApplyResult result;
                    try
                    {
                        result = await Git.CherryPickAsync(sha, options);
                    }
                    catch (GitException failure)
                    {
                        return Failed(text, FailureOf(failure, OperationTexts.CherryPickSeveralFailed(done, count, sha)));
                    }

                    switch (result)
                    {
                        case ApplyResult.Conflicts:
                            var conflicts = await CountConflictedFilesAsync();
                            var rest = await DescribeCommitsAsync(ordered.Skip(done + 1));
                            return OperationReport.Conflicts(OperationTexts.CherryPickSeveralStopped(done, count, sha, conflicts, rest));

                        case ApplyResult.Empty:
                            display = new FailureDisplay(OperationTexts.CherryPickStoppedTitle, OperationTexts.CherryPickSeveralEmpty(done, count, sha, current));
                            return Failed(text, StoppedFailure(display.Summary));
                    }
                }

                return OperationReport.Done(OperationTexts.CherryPickedSeveral(count, current));
            },
            new RunOptions { HasSeveralSteps = true, DescribeFailure = _ => display }));
    }

    /// <inheritdoc cref="IRepositoryActions.RevertAsync"/>
    public Task RevertAsync(string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var current = HeadName;
        var text = OperationTexts.Revert(commit);
        return GuardAsync(text, async () =>
        {
            var isMerge = await IsMergeAsync(commit);
            await RunAsync(text, OperationKind.Local, RefreshScope.Refs, async context =>
            {
                if (await RefuseWhileInProgressAsync(text) is { } refusal)
                {
                    return refusal;
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
                    _ => Failed(text, StoppedFailure(OperationTexts.RevertEmpty(commit, current))),
                };
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
        var current = HeadName;
        var text = OperationTexts.RevertSeveral(count);
        return GuardAsync(text, async () =>
        {
            var merges = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sha in ordered)
            {
                if (await IsMergeAsync(sha))
                {
                    merges.Add(sha);
                }
            }

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

                    // One git revert per commit, newest first, never git's sequencer (2.14).
                    var options = NetworkOptions.From(context);
                    for (var done = 0; done < count; done++)
                    {
                        var sha = ordered[done];
                        ApplyResult result;
                        try
                        {
                            result = await Git.RevertAsync(sha, merges.Contains(sha), options);
                        }
                        catch (GitException failure) when (ChangesNothing(failure))
                        {
                            return Failed(text, FailureOf(failure, OperationTexts.RevertSeveralEmpty(done, count, sha, current)));
                        }
                        catch (GitException failure)
                        {
                            return Failed(text, FailureOf(failure, OperationTexts.RevertSeveralFailed(done, count, sha)));
                        }

                        switch (result)
                        {
                            case ApplyResult.Conflicts:
                                var conflicts = await CountConflictedFilesAsync();
                                var rest = await DescribeCommitsAsync(ordered.Skip(done + 1));
                                return OperationReport.Conflicts(OperationTexts.RevertSeveralStopped(done, count, sha, conflicts, rest));

                            case ApplyResult.Empty:
                                return Failed(text, StoppedFailure(OperationTexts.RevertSeveralEmpty(done, count, sha, current)));
                        }
                    }

                    return OperationReport.Done(OperationTexts.RevertedSeveral(count));
                },
                new RunOptions { HasSeveralSteps = true });
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
            if (await ConfirmAsync(ConflictBannerViewModel.AbortConfirmation(state, HasSequencer())))
            {
                await AbortAsync(state);
            }
        });
    }

    /// <inheritdoc cref="IInProgressActions.AbortAsync"/>
    public Task AbortAsync(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActive)
        {
            return Task.CompletedTask;
        }

        var text = OperationTexts.Abort(state.Kind);
        return GuardAsync(text, () => RunAsync(text, OperationKind.Local, RefreshScope.Refs, async context =>
        {
            // abort-<time> keeps HEAD, so commits a sequence already made stay reachable; the
            // -changes snapshot keeps what was done to resolve the conflicts (2.0, 2.15).
            var head = await Git.ResolveAsync("HEAD");
            var changes = await Git.SnapshotTrackedChangesAsync();
            await SaveBackupAsync(new BackupRequest("abort", head is null ? [] : [new BackupEntry(null, head)]) { ChangesCommit = changes });
            await Git.AbortAsync(state.Kind, NetworkOptions.From(context));
            return OperationReport.Done(OperationTexts.Aborted(state.Kind));
        }));
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
                await Git.CheckoutBranchAsync(name, options);
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
            await Git.CheckoutDetachedAsync(target, options);
            return OperationTexts.DetachedAt(await Git.ResolveAsync("HEAD") ?? target);
        });
    }

    /// <summary>
    /// 2.2 (D93): the local branch of the same name when it exists, checked out as it is; otherwise
    /// a new one tracking the remote branch.
    /// </summary>
    private async Task<string> CheckoutRemoteBranchAsync(string remoteRef, NetworkOptions options)
    {
        var shown = ShortName(remoteRef);
        var local = LocalNameOfRemoteBranch(remoteRef);
        if (FindRef(HeadsPrefix + local) is { } existing)
        {
            await Git.CheckoutBranchAsync(local, options);
            if (!string.Equals(existing.UpstreamRef, remoteRef, StringComparison.Ordinal))
            {
                return OperationTexts.CheckedOutTracking(local, UpstreamText(existing));
            }

            return existing.Behind > 0 ? OperationTexts.CheckedOutBehind(local, existing.Behind, shown) : OperationTexts.CheckedOut(local);
        }

        await Git.CheckoutTrackingBranchAsync(local, remoteRef, options);
        return OperationTexts.CreatedTrackingAndCheckedOut(local, shown);
    }

    /// <summary>The local name a remote branch checks out into: the part after <c>&lt;r&gt;/</c>, the remote found by its ref or, for a ref not read yet, by the longest remote name it starts with.</summary>
    private string LocalNameOfRemoteBranch(string remoteRef)
    {
        if (FindRef(remoteRef) is { } gitRef && RepositoryMenuContext.LocalNameOf(gitRef) is { } local)
        {
            return local;
        }

        var name = remoteRef[RemotesPrefix.Length..];
        var remote = RemoteNames
            .Where(candidate => name.StartsWith(candidate + "/", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        if (remote is not null)
        {
            return name[(remote.Length + 1)..];
        }

        var slash = name.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? name[(slash + 1)..] : name;
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
    /// with the menus' reason "Finish or abort the &lt;kind&gt; first" (design 5), read from a fresh
    /// status inside the operation, so one queued behind an operation that stopped is refused too.
    /// Null when nothing is in progress.
    /// </summary>
    private async Task<OperationReport?> RefuseWhileInProgressAsync(OperationText text)
    {
        var state = (await Git.ReadStatusAsync()).InProgress;
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

    /// <summary>Git 2.36's empty revert (D117): it fails with "nothing to commit" and leaves nothing in progress.</summary>
    private static bool ChangesNothing(GitException failure) =>
        failure.StandardOutput.Contains("nothing to commit", StringComparison.Ordinal)
        || failure.StandardError.Contains("nothing to commit", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="sha"/> is a merge commit: from the graph, or read from git.</summary>
    private async Task<bool> IsMergeAsync(string sha)
    {
        if (FindCommit(sha) is { } commit)
        {
            return commit.Parents.Count > 1;
        }

        return (await Git.ReadCommitDetailsAsync(sha)).Parents.Count > 1;
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

    /// <summary>The commits as 2.13's message lists them: SHA and subject.</summary>
    private async Task<IReadOnlyList<(string Sha, string Subject)>> DescribeCommitsAsync(IEnumerable<string> shas)
    {
        var described = new List<(string Sha, string Subject)>();
        foreach (var sha in shas)
        {
            described.Add((sha, await ReadSubjectAsync(sha) ?? string.Empty));
        }

        return described;
    }

    /// <summary>A cherry-pick or revert sequence started in a terminal is in progress (<c>sequencer/</c>, section 5).</summary>
    private bool HasSequencer()
    {
        try
        {
            return Directory.Exists(Path.Combine(Git.GitDirectory, "sequencer"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A checkout's name in texts, and its git calls, which answer the success message.</summary>
    private sealed record CheckoutPlan(string Name, Func<NetworkOptions, Task<string>> Run);
}
