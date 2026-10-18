// The toolbar's Fetch all and Pull (docs/phase-3-design.md 2.16, 2.18; docs/phase-4-design.md
// section 6), moved here unchanged from RepositoryOperations.Remotes.cs (docs/phase-6-design.md
// 18) so that work package WP-B builds the bulk runs' entry points beside them: FetchAllAsync(BulkPart)
// and PullAsync(BulkPart) (4.6), Pull's settings read before its fetch (8.9) and the pull's ignored
// files (9.10). Their tests are in tests/VisualCommit.App.Tests/RepositoryOperations{Fetch,Pull}Tests.cs.

using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    // 2.16: fetch every remote.

    public Task FetchAllAsync()
    {
        var text = OperationTexts.FetchAll;
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                await Git.FetchAsync(null, NetworkOptions.From(context), noAutoMaintenance: false, context.CancellationToken);
                return OperationReport.Done(OperationTexts.FetchedAll);
            },
            NetworkRun(UndoScope.None, "fetch:*", FetchAllFailureDisplay)));
    }

    /// <summary>
    /// The remotes a fetch of all remotes could not fetch, in order, from the lines
    /// <c>error: Could not fetch &lt;r&gt;</c> that <see cref="IGitRepository.FetchAsync"/> adds
    /// to git's output for each (D109).
    /// </summary>
    public static IReadOnlyList<string> FailedRemotes(string gitOutput)
    {
        ArgumentNullException.ThrowIfNull(gitOutput);
        return [.. gitOutput.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(OperationTexts.CouldNotFetchPrefix, StringComparison.Ordinal))
            .Select(line => line[OperationTexts.CouldNotFetchPrefix.Length..].Trim())
            .Where(remote => remote.Length > 0)
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>"Fetch failed" naming every remote that failed (2.16): "Git could not fetch from origin and mirror."</summary>
    private static FailureDisplay? FetchAllFailureDisplay(GitFailure failure)
    {
        var failed = FailedRemotes(failure.GitOutput);
        return failed.Count == 0 ? null : new FailureDisplay(OperationTexts.FetchAll.ErrorTitle, OperationTexts.FetchFailed(failed));
    }

    // 2.18: pull.

    public Task PullAsync()
    {
        if (CurrentBranch is not { UpstreamRef: not null } branch)
        {
            Log.Info($"{Repository.Name}: Pull: not started, HEAD is not on a branch with an upstream.");
            return Task.CompletedTask;
        }

        var upstream = UpstreamText(branch) ?? ShortName(branch.UpstreamRef);
        var text = OperationTexts.Pull(upstream, branch.Name);
        return PullWithAsync(branch, upstream, text, PullIntegration.AsConfigured, "pull:" + branch.FullName);
    }

    /// <summary>
    /// "Pull from {up} (rebase)" (docs/phase-4-design.md section 6, D147): phase 3's pull with its
    /// integration forced to a rebase, as <c>git pull --rebase</c>; git's pull settings are not
    /// read. Its dedupe key <c>pull:&lt;ref&gt;:rebase</c> never merges it with a plain Pull.
    /// </summary>
    public Task PullRebaseAsync()
    {
        if (CurrentBranch is not { UpstreamRef: not null } branch)
        {
            Log.Info($"{Repository.Name}: Pull (rebase): not started, HEAD is not on a branch with an upstream.");
            return Task.CompletedTask;
        }

        var upstream = UpstreamText(branch) ?? ShortName(branch.UpstreamRef);
        var text = OperationTexts.PullRebase(upstream, branch.Name);
        return PullWithAsync(branch, upstream, text, PullIntegration.Rebase, $"pull:{branch.FullName}:rebase");
    }

    /// <summary>A pull as one operation (2.18, section 6): a network one, or a local one for a local upstream, which skips the fetch.</summary>
    private Task PullWithAsync(GitRef branch, string upstream, OperationText text, PullIntegration integration, string dedupeKey) =>
        GuardAsync(text, async () =>
        {
            var notice = new StopNotice();
            await RunAsync(
                text,
                branch.HasLocalUpstream ? OperationKind.Local : OperationKind.Network,
                RefreshScope.Refs,
                context => PullStepsAsync(context, branch, upstream, text, integration, notice),
                NetworkRun(UndoScope.Pull(branch.Name), dedupeKey, failure => PullRefusalDisplay(failure, text)));
            await FinishAsync(notice, new SelectionAfter());
        });

    /// <summary>
    /// C12's words for a pull that git refused because changes are in the way (docs/phase-5-design.md
    /// 9.3), under "Pull failed". Its merge's or fast-forward's call (<c>git merge …</c>) is told as a
    /// merge's, whatever block git printed ("1 conflict prevents the merge. …" for a changed file, an
    /// untracked one or an untracked folder; "Your staged changes prevent the merge. …"); its
    /// rebase's in phase 4's words ("Git rebases only a clean working tree: …"), a refused checkout
    /// inside the rebase never as a merge's. Null for any other failure.
    /// </summary>
    private static FailureDisplay? PullRefusalDisplay(GitFailure failure, OperationText text) =>
        failure.CommandText.StartsWith("git merge ", StringComparison.Ordinal) ? BlockedByChangesDisplay(failure, text, "merge")
        : RebaseRefusalSummary(failure) is { } rebase ? new FailureDisplay(text.ErrorTitle, rebase)
        : null;

    /// <summary>
    /// Pull's two steps (2.18, D84): the fetch of the upstream's remote, which can be cancelled
    /// (skipped for a local upstream); the end of the cancel window, which stops here when the
    /// user had cancelled, so nothing is integrated after Cancel; then the integration, never
    /// cancelled: git pull's choice, or a rebase chosen for this pull (section 6). Every pull that
    /// rebases saves 1.2's backup first, through the git layer's callback (D147), and a rebase
    /// that stops is told by the rebase folder, with or without conflicted files. The message
    /// counts what HEAD gained, read after the pull; commits git's fork point left out are
    /// reported (warning).
    /// </summary>
    private async Task<OperationReport> PullStepsAsync(OperationContext context, GitRef branch, string upstream, OperationText text, PullIntegration integration, StopNotice notice)
    {
        // The pull may have waited in the queue: it pulls into the branch, from the upstream, that
        // the user saw when they chose it, or into nothing (design 1.3).
        if (await RefuseIfPullTargetMovedAsync(text, branch, upstream) is { } refusal)
        {
            return refusal;
        }

        var options = NetworkOptions.From(context);
        if (!branch.HasLocalUpstream)
        {
            var remote = branch.UpstreamRemote ?? throw new InvalidOperationException($"{branch.Name}'s upstream names no remote.");
            await Git.FetchAsync(remote, options, noAutoMaintenance: false, context.CancellationToken);
        }

        context.EndCancelWindow();

        // Once more after the fetch, which can take long: a terminal may have moved HEAD meanwhile.
        if (await RefuseIfHeadMovedAsync(text, new HeadState(branch.Name, branch.TargetSha), OperationTexts.NothingPulled, fresh: true) is { } moved)
        {
            return moved;
        }

        PullResult pulled;
        try
        {
            pulled = await Git.IntegratePullAsync(
                branch,
                integration,
                oldTip => SaveRebaseBackupAsync(branch.Name, oldTip),
                options,
                cancellationToken: CancellationToken.None);
        }
        catch (GitException exception) when (exception.StandardError.TrimEnd().EndsWith(OperationTexts.SettingNotUnderstoodEnd, StringComparison.Ordinal))
        {
            // "pull.rebase is set to preserve, which this app does not understand." is the summary.
            return Failed(text, FailureOf(exception, exception.StandardError.Trim()));
        }

        switch (pulled.Outcome)
        {
            case PullOutcome.UpToDate:
                return OperationReport.Done(OperationTexts.PullUpToDate(branch.Name));
            case PullOutcome.FastForward:
                return OperationReport.Done(OperationTexts.PulledFastForward(pulled.CommitCount, branch.Name));
            case PullOutcome.Merged:
                return OperationReport.Done(OperationTexts.PulledMerged(upstream, branch.Name));
            case PullOutcome.Rebased:
                // Git's fork point left out commits the upstream once had (0.1): a success said in
                // the warning colour (auto-fetch counts it as a fetch). Changes rebase.autoStash put
                // back with conflicts make it 1.4's warning (the review's findings 12 and 19).
                var rebased = pulled.LeftOutByForkPoint > 0
                    ? OperationTexts.PulledRebasedLeftOut(branch.Name, upstream, pulled.LeftOutByForkPoint)
                    : OperationTexts.PulledRebased(branch.Name, upstream);
                return pulled.AutostashConflicted ? OperationReport.Conflicts(OperationTexts.WithAutostashConflicted(rebased))
                    : pulled.LeftOutByForkPoint > 0 ? OperationReport.Warning(rebased)
                    : OperationReport.Done(rebased);
            case PullOutcome.Conflicts:
                var conflicts = await CountConflictedFilesAsync();
                var stopped = conflicts > 0 || (await Git.ReadStatusAsync()).InProgress.Kind != InProgressKind.Rebase
                    ? OperationReport.Conflicts(OperationTexts.PullConflicts(conflicts))
                    : await RebaseStopReportAsync(context, 0, notice); // A rebase stopped with no conflicted file (section 6, 1.4).

                // Git's fork point left out commits the upstream once had, also when the rebase
                // stopped (10.2, D147; the review's finding 20); the stop's own warning tone.
                return pulled.LeftOutByForkPoint > 0
                    ? stopped with { Message = OperationTexts.WithLeftOutByForkPoint(stopped.Message, upstream, pulled.LeftOutByForkPoint) }
                    : stopped;
            case PullOutcome.UpstreamGone:
                return OperationReport.Done(OperationTexts.PullUpstreamGone(
                    ShortName(branch.UpstreamRemoteRef ?? branch.UpstreamRef!), branch.UpstreamRemote ?? LocalRemote));
            default:
                throw new InvalidOperationException($"Unknown pull outcome {pulled.Outcome}.");
        }
    }

    /// <summary>
    /// A refusal when HEAD is no longer on <paramref name="branch"/>, or the branch's upstream is
    /// no longer the one the pull was chosen with, read now; null when both are as they were.
    /// </summary>
    private async Task<OperationReport?> RefuseIfPullTargetMovedAsync(OperationText text, GitRef branch, string upstream)
    {
        var refs = await Git.ReadRefsAsync();
        string summary;
        if (!string.Equals(refs.Head.BranchName, branch.Name, StringComparison.Ordinal))
        {
            summary = OperationTexts.HeadMoved(new HeadState(branch.Name, branch.TargetSha), refs.Head, OperationTexts.NothingPulled);
        }
        else if (refs.Refs.FirstOrDefault(gitRef => gitRef.FullName == branch.FullName) is var now
            && !string.Equals(now?.UpstreamRef, branch.UpstreamRef, StringComparison.Ordinal))
        {
            summary = OperationTexts.UpstreamChanged(branch.Name, upstream, now is { UpstreamRef: { } upstreamRef } ? UpstreamText(now) ?? ShortName(upstreamRef) : null);
        }
        else
        {
            return null;
        }

        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}");
        return Failed(text, StoppedFailure(summary));
    }
}
