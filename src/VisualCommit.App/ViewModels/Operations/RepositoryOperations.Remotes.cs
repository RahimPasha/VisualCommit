// Remotes and the network: docs/phase-3-design.md 2.16, 2.18 to 2.23, 2.29 (pushing and
// deleting tags on a remote), the Remotes dialog's actions (IRemoteActions), 1.8's cancelled
// fetches and pushes, and their texts in 2.33 (OperationTexts). Clone is not here (the welcome
// page), nor auto-fetch (AutoFetchScheduler, WP9), whose title is 2.33's "Auto-fetch". Built by
// WP7-remotes on the helpers of RepositoryOperations.Run.cs; its tests are in
// tests/VisualCommit.App.Tests/RepositoryOperations{Fetch,Pull,Push,Remotes}Tests.cs.
//
// Every operation here that talks to a remote is a Network operation with PromptMode.Ask (3.3):
// git's and ssh's prompts reach the app's dialogs, the status bar offers Cancel, and a cancel
// that left lock files opens 1.8's dialog (RunAsync). Only Fetch, Pull and Push without choices
// set a dedupe key (1.2), so that a second click while the same operation waits or runs does
// nothing more.

using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string LocalRemote = ".";

    /// <summary>The network operations' options: git's prompts reach the app's dialogs (3.3).</summary>
    private static RunOptions NetworkRun(string? dedupeKey = null, Func<GitFailure, FailureDisplay?>? describeFailure = null) =>
        new() { DedupeKey = dedupeKey, Prompts = PromptMode.Ask, DescribeFailure = describeFailure };

    // 2.16: fetch.

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
            NetworkRun("fetch:*", FetchAllFailureDisplay)));
    }

    public Task FetchAsync(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        var text = OperationTexts.Fetch(remote);
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                await Git.FetchAsync(remote, NetworkOptions.From(context), noAutoMaintenance: false, context.CancellationToken);
                return OperationReport.Done(OperationTexts.Fetched(remote));
            },
            NetworkRun("fetch:" + remote)));
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
                NetworkRun(dedupeKey));
            await FinishAsync(notice, new SelectionAfter());
        });

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
        if (await RefuseIfHeadMovedAsync(text, new HeadState(branch.Name, branch.TargetSha), OperationTexts.NothingPulled) is { } moved)
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
                CancellationToken.None);
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
                // Git's fork point left out commits the upstream once had (0.1): a success said in the warning colour (auto-fetch counts it as a fetch).
                return pulled.LeftOutByForkPoint > 0
                    ? OperationReport.Warning(OperationTexts.PulledRebasedLeftOut(branch.Name, upstream, pulled.LeftOutByForkPoint))
                    : OperationReport.Done(OperationTexts.PulledRebased(branch.Name, upstream));
            case PullOutcome.Conflicts:
                var conflicts = await CountConflictedFilesAsync();
                if (conflicts > 0 || (await Git.ReadStatusAsync()).InProgress.Kind != InProgressKind.Rebase)
                {
                    return OperationReport.Conflicts(OperationTexts.PullConflicts(conflicts));
                }

                // A rebase stopped with no conflicted file (section 6, 1.4).
                return await RebaseStopReportAsync(context, 0, notice);
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

    // 2.19: push.

    public Task PushCurrentBranchAsync()
    {
        if (CurrentBranch is not { } branch)
        {
            Log.Info($"{Repository.Name}: Push: not started, HEAD is not on a branch.");
            return Task.CompletedTask;
        }

        return PushBranchAsync(branch.FullName);
    }

    public Task PushBranchAsync(string branchRef)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        if (FindRef(branchRef) is not { Kind: RefKind.LocalBranch } branch)
        {
            Log.Info($"{Repository.Name}: Push: not started, there is no local branch {branchRef}.");
            return Task.CompletedTask;
        }

        if (branch.HasLocalUpstream)
        {
            // D101: a branch that tracks a local branch is not pushed; the menus leave it out.
            Log.Info($"{Repository.Name}: Push {branch.Name}: not started, it tracks a local branch.");
            return Task.CompletedTask;
        }

        if (branch is { UpstreamRemote: { } remote, UpstreamRemoteRef: { } remoteRef })
        {
            var target = UpstreamText(branch) ?? $"{remote}/{ShortName(remoteRef)}";
            return GuardAsync(OperationTexts.Push(branch.Name, target), () => PushToAsync(branch, remote, remoteRef, target, setUpstream: false, toUpstream: true, dedupe: true));
        }

        return PushThroughDialogAsync(branch);
    }

    public Task PushBranchToAsync(string branchRef, string remoteBranchRef, bool setUpstream)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        ArgumentNullException.ThrowIfNull(remoteBranchRef);
        if (FindRef(branchRef) is not { Kind: RefKind.LocalBranch } branch || SplitRemoteBranch(remoteBranchRef) is not { } split)
        {
            Log.Info($"{Repository.Name}: Push: not started, {branchRef} or {remoteBranchRef} is not known.");
            return Task.CompletedTask;
        }

        var (remote, name) = split;
        var target = $"{remote}/{name}";
        var toUpstream = string.Equals(branch.UpstreamRef, remoteBranchRef, StringComparison.Ordinal);
        return GuardAsync(
            OperationTexts.Push(branch.Name, target),
            () => PushToAsync(branch, remote, HeadsPrefix + name, target, setUpstream, toUpstream, dedupe: !setUpstream));
    }

    /// <summary>
    /// The push dialog for a branch without an upstream (2.19, 4f): the remote, the branch's name
    /// there and "Set as upstream"; then the push, which a second click never merges with another.
    /// </summary>
    private Task PushThroughDialogAsync(GitRef branch)
    {
        var remotes = RemoteNames;
        if (remotes.Count == 0)
        {
            Log.Info($"{Repository.Name}: Push {branch.Name}: not started, the repository has no remotes.");
            return Task.CompletedTask;
        }

        return GuardAsync(OperationTexts.Push(branch.Name, $"{remotes[0]}/{branch.Name}"), async () =>
        {
            if (await ShowDialogAsync(new PushDialogViewModel(branch.Name, remotes)) is not { } chosen)
            {
                return;
            }

            await PushToAsync(branch, chosen.Remote, HeadsPrefix + chosen.Branch, $"{chosen.Remote}/{chosen.Branch}", chosen.SetUpstream, toUpstream: false, dedupe: false);
        });
    }

    /// <summary>
    /// One push of a local branch (2.19): "Push rejected" offers "Pull" when the current branch was
    /// pushed to its upstream (<paramref name="toUpstream"/>), otherwise "Fetch &lt;r&gt;".
    /// </summary>
    private Task<OperationResult> PushToAsync(GitRef branch, string remote, string remoteRef, string target, bool setUpstream, bool toUpstream, bool dedupe)
    {
        var request = new PushRequest(remote, branch.Name, remoteRef) { SetUpstream = setUpstream };
        var current = toUpstream && string.Equals(Refs?.Head.BranchName, branch.Name, StringComparison.Ordinal);
        return RunAsync(
            OperationTexts.Push(branch.Name, target),
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                await Git.PushAsync(request, NetworkOptions.From(context), context.CancellationToken);
                return OperationReport.Done(OperationTexts.Pushed(branch.Name, target, setUpstream));
            },
            NetworkRun(
                dedupe ? $"push:{branch.FullName}->{remote}/{remoteRef}" : null,
                failure => PushFailureDisplay(failure, branch.Name, remote, current)));
    }

    /// <summary>
    /// <c>refs/remotes/&lt;r&gt;/&lt;x&gt;</c> as its remote and the branch's name there: from the
    /// refs as read, else by the longest configured remote whose name starts it; null when no
    /// remote does.
    /// </summary>
    private (string Remote, string Name)? SplitRemoteBranch(string remoteBranchRef)
    {
        if (FindRef(remoteBranchRef) is { Kind: RefKind.RemoteBranch, RemoteName: { } known } gitRef && gitRef.Name.StartsWith(known + "/", StringComparison.Ordinal))
        {
            return (known, gitRef.Name[(known.Length + 1)..]);
        }

        if (!remoteBranchRef.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = remoteBranchRef[RemotesPrefix.Length..];
        var remote = RemoteNames
            .Where(name => rest.StartsWith(name + "/", StringComparison.Ordinal) && rest.Length > name.Length + 1)
            .OrderByDescending(name => name.Length)
            .FirstOrDefault();
        return remote is null ? null : (remote, rest[(remote.Length + 1)..]);
    }

    // 2.20: force push with lease.

    public Task ForcePushAsync(string branchRef)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        if (FindRef(branchRef) is not { Kind: RefKind.LocalBranch, UpstreamRef: { } upstreamRef, UpstreamRemote: { } remote, UpstreamRemoteRef: { } remoteRef } branch
            || remote == LocalRemote)
        {
            Log.Info($"{Repository.Name}: Force push: not started, {branchRef} has no remote upstream.");
            return Task.CompletedTask;
        }

        var upstream = UpstreamText(branch) ?? ShortName(upstreamRef);
        var text = OperationTexts.ForcePush(branch.Name, upstream);
        var request = new PushRequest(remote, branch.Name, remoteRef) { ForceWithLease = true };
        return GuardAsync(text, () => ConfirmAndRunAsync(
            OperationTexts.ConfirmForcePush(branch.Name, remote, ShortName(remoteRef)),
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async _ =>
            {
                // UpstreamRef's commit as it is just before the push (2.20).
                var commit = await Git.ResolveAsync(upstreamRef);
                return commit is null ? null : new BackupRequest("force-push", [new BackupEntry(ShortName(upstreamRef), commit)]);
            },
            async (context, _) =>
            {
                await Git.PushAsync(request, NetworkOptions.From(context), context.CancellationToken);
                return OperationReport.Done(OperationTexts.ForcePushed(branch.Name, upstream));
            },
            NetworkRun(describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
                ? new FailureDisplay(OperationTexts.ForcePushStoppedTitle, OperationTexts.ForcePushStopped(upstream, branch.Name))
                : null)));
    }

    // 2.21: set or remove the upstream.

    public Task SetUpstreamAsync(string branchRef)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        var name = ShortName(branchRef);
        var text = OperationTexts.SetUpstream(name);
        return GuardAsync(text, async () =>
        {
            var remoteBranches = Refs?.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch).ToList() ?? [];
            if (remoteBranches.Count == 0)
            {
                Log.Info($"{Repository.Name}: {text.Title}: not started, there are no remote branches to track.");
                return;
            }

            var branch = FindRef(branchRef);
            var current = branch is { UpstreamRef: { } upstreamRef, HasLocalUpstream: false } ? ShortName(upstreamRef) : null;
            var dialog = new PickDialogViewModel(
                OperationTexts.SetUpstreamDialogTitle(name),
                [.. remoteBranches.Select(gitRef => gitRef.Name)],
                current,
                OperationTexts.SetUpstreamButton);
            if (await ShowDialogAsync(dialog) is not { } picked)
            {
                return;
            }

            var target = remoteBranches.First(gitRef => gitRef.Name == picked).FullName;
            await RunAsync(text, OperationKind.Local, RefreshScope.Refs, async _ =>
            {
                await Git.SetUpstreamAsync(name, target);
                return OperationReport.Done(OperationTexts.UpstreamSet(name, picked));
            });
        });
    }

    public Task UnsetUpstreamAsync(string branchRef)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        if (FindRef(branchRef) is not { Kind: RefKind.LocalBranch, UpstreamRef: { } upstreamRef } branch)
        {
            Log.Info($"{Repository.Name}: Stop tracking: not started, {branchRef} has no upstream.");
            return Task.CompletedTask;
        }

        var upstream = UpstreamText(branch) ?? ShortName(upstreamRef);
        var text = OperationTexts.UnsetUpstream(branch.Name, upstream);
        return GuardAsync(text, () => RunAsync(text, OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await Git.UnsetUpstreamAsync(branch.Name);
            return OperationReport.Done(OperationTexts.UpstreamUnset(branch.Name, upstream));
        }));
    }

    // 2.22: delete a branch on a remote.

    public Task DeleteRemoteBranchAsync(string remoteBranchRef)
    {
        ArgumentNullException.ThrowIfNull(remoteBranchRef);
        if (SplitRemoteBranch(remoteBranchRef) is not { } split)
        {
            Log.Info($"{Repository.Name}: Delete on a remote: not started, {remoteBranchRef} is not a remote branch.");
            return Task.CompletedTask;
        }

        var (remote, name) = split;

        var text = OperationTexts.DeleteRemoteBranch(name, remote);
        string? expected = null;
        return GuardAsync(text, () => ConfirmAndRunAsync(
            OperationTexts.ConfirmDeleteRemoteBranch(name, remote),
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async _ =>
            {
                // Read inside the operation: a fetch since the menu opened has moved it (2.22).
                expected = await ResolveOrFailAsync(remoteBranchRef);
                return new BackupRequest("remote-branch", [new BackupEntry($"{remote}/{name}", expected)]);
            },
            async (context, _) =>
            {
                await Git.DeleteRemoteBranchAsync(remote, name, expected!, NetworkOptions.From(context), context.CancellationToken);
                return OperationReport.Done(OperationTexts.DeletedRemoteBranch(name, remote));
            },
            NetworkRun(describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
                ? new FailureDisplay(OperationTexts.DeletionStoppedTitle, OperationTexts.RemoteBranchChanged(name, remote))
                : null)));
    }

    /// <summary>The object <paramref name="fullRef"/> names now, unpeeled; a <see cref="GitException"/> as git says it when it names nothing.</summary>
    private async Task<string> ResolveOrFailAsync(string fullRef) =>
        await Git.ResolveAsync(fullRef)
        ?? throw new GitException($"git rev-parse --verify --quiet {fullRef}", 1, $"fatal: {fullRef} does not exist here any more.\n");

    // 2.23: the Remotes dialog and the remote's menu.

    public Task ManageRemotesAsync() => ShowRemotesDialogAsync(null, add: false);

    public Task ShowAddRemoteAsync() => ShowRemotesDialogAsync(null, add: true);

    public Task ShowEditRemoteAsync(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        return ShowRemotesDialogAsync(remote, add: false);
    }

    /// <summary>
    /// Opens the Remotes dialog (2.23, 4f) once its list is read: on its list, on its form for a
    /// new remote (<paramref name="add"/>), or on its form for <paramref name="edit"/>; completes
    /// when it closes. The dialog runs the add, rename, change-URL and remove operations through
    /// <see cref="IRemoteActions"/>.
    /// </summary>
    private async Task ShowRemotesDialogAsync(string? edit, bool add)
    {
        if (IsClosed || Dialogs is not { } layer)
        {
            Log.Info($"{Repository.Name}: the Remotes dialog was not shown: {(IsClosed ? "the tab is closed" : "there is no dialog layer")}.");
            return;
        }

        var dialog = new RemotesDialogViewModel(this, IgnoreCase, Log);
        await dialog.Loading;
        if (add)
        {
            dialog.AddCommand.Execute(null);
        }
        else if (edit is not null && dialog.Rows.FirstOrDefault(row => row.Name == edit) is { } row)
        {
            dialog.EditCommand.Execute(row);
        }

        if (IsClosed)
        {
            return;
        }

        if (NamesRepositoryInDialogs)
        {
            dialog.RepositoryName = Repository.Name;
        }

        await layer.ShowAsync(dialog);
    }

    public Task RemoveRemoteAsync(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        var text = OperationTexts.RemoveRemote(remote);
        return GuardAsync(text, async () =>
        {
            var shownUrl = (await Git.ReadRemotesAsync()).FirstOrDefault(info => info.Name == remote)?.ShownUrl ?? string.Empty;
            await ConfirmAndRunAsync(
                OperationTexts.ConfirmRemoveRemote(remote, RemoteBranchesOf(remote).Count, shownUrl),
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async _ =>
                {
                    // Every refs/remotes/<r>/* as it is now, in one batch (2.0, 2.23).
                    var refs = await Git.ReadRefsAsync();
                    var entries = refs.Refs
                        .Where(gitRef => gitRef.Kind == RefKind.RemoteBranch && gitRef.RemoteName == remote)
                        .Select(gitRef => new BackupEntry(gitRef.Name, gitRef.TargetSha))
                        .ToList();
                    return new BackupRequest("remote", entries);
                },
                async (_, _) =>
                {
                    await Git.RemoveRemoteAsync(remote);
                    return OperationReport.Done(OperationTexts.RemovedRemote(remote));
                });
        });
    }

    // The Remotes dialog's own actions (IRemoteActions, 2.23): git's refusal goes to the form's
    // RemoteError, not to an error dialog.

    /// <summary>The configured remotes, with their URLs as configured, sorted by name, for the Remotes dialog.</summary>
    public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync() => Git.ReadRemotesAsync();

    /// <summary>Adds a remote and waits; with <paramref name="fetchNow"/> also queues its fetch ("Fetch &lt;name&gt;"), which it does not wait for. Null, or git's message for the form.</summary>
    public async Task<string?> AddRemoteAsync(string name, string url, bool fetchNow)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(url);
        var error = await RunRemoteFormAsync(OperationTexts.AddRemote(name), () => Git.AddRemoteAsync(name, url), OperationTexts.AddedRemote(name));
        if (error is null && fetchNow)
        {
            // An operation of its own, waited for by nobody: its result shows in the status bar,
            // a prompt for it stacks over the dialog, a failure opens its error dialog.
            _ = FetchAsync(name);
        }

        return error;
    }

    /// <summary>Renames a remote and waits. Null, or git's message for the form.</summary>
    public Task<string?> RenameRemoteAsync(string name, string newName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(newName);
        return RunRemoteFormAsync(OperationTexts.RenameRemote(name), () => Git.RenameRemoteAsync(name, newName), OperationTexts.RenamedRemote(name, newName));
    }

    /// <summary>Changes a remote's URL and waits. Null, or git's message for the form.</summary>
    public Task<string?> SetRemoteUrlAsync(string name, string url)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(url);
        return RunRemoteFormAsync(OperationTexts.SetRemoteUrl(name), () => Git.SetRemoteUrlAsync(name, url), OperationTexts.ChangedRemoteUrl(name));
    }

    /// <summary>One local operation of the Remotes form: null when it succeeded, otherwise what the form's <c>RemoteError</c> shows (git's own message).</summary>
    private async Task<string?> RunRemoteFormAsync(OperationText text, Func<Task> write, string done)
    {
        var result = await RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await write();
                return OperationReport.Done(done);
            },
            new RunOptions { ShowsErrorDialog = false });
        return result.Outcome switch
        {
            OperationOutcome.Succeeded => null,
            OperationOutcome.Failed when result.Failure is { GitOutput.Length: > 0 } failure => failure.GitOutput,
            _ => result.Message,
        };
    }

    // 2.29: tags on a remote.

    public Task PushTagAsync(string tagRef)
    {
        ArgumentNullException.ThrowIfNull(tagRef);
        var tag = ShortName(tagRef);
        var remotes = RemoteNames;
        if (remotes.Count == 0)
        {
            Log.Info($"{Repository.Name}: Push tag {tag}: not started, the repository has no remotes.");
            return Task.CompletedTask;
        }

        return GuardAsync(OperationTexts.PushTag(tag, remotes[0]), async () =>
        {
            var remote = remotes.Count == 1
                ? remotes[0]
                : await ShowDialogAsync(new ChoiceDialogViewModel(OperationTexts.PushTagChoiceTitle(tag), remotes, OperationTexts.PushTagChoiceButton));
            if (remote is null)
            {
                return;
            }

            await RunAsync(
                OperationTexts.PushTag(tag, remote),
                OperationKind.Network,
                RefreshScope.Refs,
                async context =>
                {
                    await Git.PushTagAsync(remote, tag, NetworkOptions.From(context), context.CancellationToken);
                    return OperationReport.Done(OperationTexts.PushedTag(tag, remote));
                },
                NetworkRun());
        });
    }

    public Task DeleteRemoteTagAsync(string tagRef)
    {
        ArgumentNullException.ThrowIfNull(tagRef);
        var tag = ShortName(tagRef);
        var remotes = RemoteNames;
        if (remotes.Count == 0)
        {
            Log.Info($"{Repository.Name}: Delete tag {tag} on a remote: not started, the repository has no remotes.");
            return Task.CompletedTask;
        }

        return GuardAsync(OperationTexts.DeleteRemoteTag(tag, remotes[0]), async () =>
        {
            var remote = remotes.Count == 1
                ? remotes[0]
                : await ShowDialogAsync(new ChoiceDialogViewModel(OperationTexts.DeleteRemoteTagChoiceTitle(tag), remotes, OperationTexts.DeleteRemoteTagChoiceButton, isDestructive: true));
            if (remote is null)
            {
                return;
            }

            var text = OperationTexts.DeleteRemoteTag(tag, remote);
            string? expected = null;
            await ConfirmAndRunAsync(
                OperationTexts.ConfirmDeleteRemoteTag(tag, remote),
                text,
                OperationKind.Network,
                RefreshScope.Refs,
                async _ =>
                {
                    // The local tag object, unpeeled: what the backup keeps and the lease expects (2.29).
                    expected = await ResolveOrFailAsync(TagsPrefix + tag);
                    return new BackupRequest("remote-tag", [new BackupEntry($"{remote}/{tag}", expected)]);
                },
                async (context, _) =>
                {
                    await Git.DeleteRemoteTagAsync(remote, tag, expected!, NetworkOptions.From(context), context.CancellationToken);
                    return OperationReport.Done(OperationTexts.DeletedRemoteTag(tag, remote));
                },
                NetworkRun(describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
                    ? new FailureDisplay(OperationTexts.DeletionStoppedTitle, OperationTexts.RemoteTagDiffers(remote, tag))
                    : null));
        });
    }
}
