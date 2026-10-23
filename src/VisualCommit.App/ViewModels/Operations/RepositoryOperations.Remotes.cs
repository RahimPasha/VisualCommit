// Remotes and the network: docs/phase-3-design.md 2.16, 2.18 to 2.23, 2.29 (pushing and
// deleting tags on a remote), the Remotes dialog's actions (IRemoteActions), 1.8's cancelled
// fetches and pushes, and their texts in 2.33 (OperationTexts). Clone is not here (the welcome
// page), nor auto-fetch (AutoFetchScheduler, WP9), whose title is 2.33's "Auto-fetch". Built by
// WP7-remotes on the helpers of RepositoryOperations.Run.cs; its tests are in
// tests/VisualCommit.App.Tests/RepositoryOperations{Fetch,Pull,Push,Remotes}Tests.cs. The toolbar's
// Fetch all and Pull are in RepositoryOperations.FetchPull.cs (docs/phase-6-design.md 18). Phase 6
// (WP-RA, docs/phase-6-design.md 10.4): a remote branch is split into its remote and the branch
// there by the remotes' fetch lines, and a push that would stop every fetch of its remote is
// refused before git.
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
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string LocalRemote = ".";

    /// <summary>
    /// The network operations' options: git's prompts reach the app's dialogs (3.3), and what the
    /// undo history records of each (<paramref name="undo"/>, docs/phase-5-design.md 7.2).
    /// </summary>
    private static RunOptions NetworkRun(UndoScope undo, string? dedupeKey = null, Func<GitFailure, FailureDisplay?>? describeFailure = null) =>
        new() { DedupeKey = dedupeKey, Prompts = PromptMode.Ask, DescribeFailure = describeFailure, Undo = undo };

    // 2.16: fetch.

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
            NetworkRun(UndoScope.None, "fetch:" + remote)));
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

    /// <summary>
    /// A drop's push of <paramref name="branchRef"/> onto the remote branch
    /// <paramref name="remoteBranchRef"/> (4g): to the branch on its remote that fills it, as the
    /// remote's fetch lines map it back (docs/phase-6-design.md 10.4 #8): <c>refs/heads/&lt;A&gt;:refs/heads/&lt;y&gt;</c>
    /// on <c>&lt;r&gt;</c>, named <c>{T}</c> in its texts; onto a renamed branch titled
    /// <c>Push {A} to {T}</c> (GD24). Not started for a ref that is no such branch (a menu built
    /// before the lines changed).
    /// </summary>
    public Task PushBranchToAsync(string branchRef, string remoteBranchRef, bool setUpstream)
    {
        ArgumentNullException.ThrowIfNull(branchRef);
        ArgumentNullException.ThrowIfNull(remoteBranchRef);
        if (FindRef(branchRef) is not { Kind: RefKind.LocalBranch } branch || SplitRemoteBranch(remoteBranchRef) is not { Remote: { } remote, Branch: { } name } split)
        {
            Log.Info($"{Repository.Name}: Push: not started, {branchRef} or {remoteBranchRef} is not known, or is no branch of one remote.");
            return Task.CompletedTask;
        }

        var target = FindRef(remoteBranchRef)?.Name ?? ShortName(remoteBranchRef);
        var renamed = !split.IsIdentity(remoteBranchRef);
        var text = renamed ? OperationTexts.PushToRemoteBranch(branch.Name, target) : OperationTexts.Push(branch.Name, target);
        var toUpstream = string.Equals(branch.UpstreamRef, remoteBranchRef, StringComparison.Ordinal);
        return GuardAsync(text, () => PushToAsync(branch, remote, HeadsPrefix + name, target, setUpstream, toUpstream, dedupe: !setUpstream, text));
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
    /// pushed to its upstream (<paramref name="toUpstream"/>), otherwise "Fetch &lt;r&gt;". Refused
    /// before git when it would stop every fetch of <paramref name="remote"/>
    /// (<see cref="RefuseIfPushStopsFetch"/>). <paramref name="text"/> is its texts when not
    /// <see cref="OperationTexts.Push"/>'s (the drop's push onto a renamed remote branch, GD24).
    /// </summary>
    private Task<OperationResult> PushToAsync(GitRef branch, string remote, string remoteRef, string target, bool setUpstream, bool toUpstream, bool dedupe, OperationText? text = null)
    {
        var request = new PushRequest(remote, branch.Name, remoteRef) { SetUpstream = setUpstream };
        var current = toUpstream && string.Equals(Refs?.Head.BranchName, branch.Name, StringComparison.Ordinal);
        var texts = text ?? OperationTexts.Push(branch.Name, target);
        return RunAsync(
            texts,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                if (await RefuseIfPushStopsFetchAsync(texts, branch.Name, remote, remoteRef, target) is { } refused)
                {
                    return refused;
                }

                UndoCapture.Push(remote, await Git.PushAsync(request, NetworkOptions.From(context), context.CancellationToken));
                return OperationReport.Done(OperationTexts.Pushed(branch.Name, target, setUpstream));
            },
            NetworkRun(
                UndoScope.Push(branch.Name, remote, force: false),
                dedupe ? $"push:{branch.FullName}->{remote}/{remoteRef}" : null,
                failure => PushFailureDisplay(failure, branch.Name, remote, current)));
    }

    /// <summary>
    /// 10.4's guard, inside a push's operation before git, with the fetch lines read there (one
    /// <c>git config --null --get-regexp ^remote\.</c>, <see cref="IGitRepository.ReadFetchRefspecsAsync"/>):
    /// a push of <paramref name="branch"/> to <paramref name="remoteRef"/> on <paramref name="remote"/>
    /// is refused when a remote-tracking ref the remote's lines fetch that ref into is fetched from
    /// more than one ref of the remote (R2) and none of them is a branch the remote has already
    /// (one ref fills it, and the refs hold it): the push would create the ref beside the one that
    /// fills that tracking ref, and every later fetch of the remote would fail (0.5's P, C2). Both
    /// are asked of <paramref name="remote"/>'s own lines (<see cref="FetchRefspecs.SourceOf(string, string)"/>):
    /// another remote that also fills the tracking ref (a mirror line) makes it ambiguous between
    /// remotes, which says nothing about this remote's own two sources. Only destinations under
    /// <c>refs/remotes/</c> count: a mirror line also maps the ref to the local branch of its name
    /// (10.1). The lines follow the fetch, not the push, rightly. Null when the push may run, and
    /// when the lines were not read (a fake repository). Also the restores' and undo's pushes of a
    /// branch (RepositoryOperations.Undo.cs).
    /// </summary>
    private async Task<OperationReport?> RefuseIfPushStopsFetchAsync(OperationText text, string branch, string remote, string remoteRef, string target)
    {
        if (await Git.ReadFetchRefspecsAsync() is not { } refspecs)
        {
            return null;
        }

        var destinations = refspecs.DestinationsOf(remote, remoteRef)
            .Where(destination => destination.StartsWith(RemotesPrefix, StringComparison.Ordinal))
            .ToList();
        var ambiguous = destinations.FirstOrDefault(destination => refspecs.SourceOf(destination, remote).Kind == RemoteSourceKind.AmbiguousRefs);
        if (ambiguous is null
            || destinations.Exists(destination => refspecs.SourceOf(destination, remote).Kind == RemoteSourceKind.Mapped && FindRef(destination) is not null))
        {
            return null;
        }

        return Refused(text, OperationTexts.PushWouldStopFetch(branch, target, ambiguous[RemotesPrefix.Length..], remote));
    }

    /// <summary>
    /// Where <c>refs/remotes/&lt;…&gt;</c> is fetched from, as the remote's fetch lines map it back
    /// (<see cref="RepoRefs.SourceOf(GitRef)"/>, docs/phase-6-design.md 10.4 #7): its remote and the
    /// branch's name there (<see cref="RemoteSource.Remote"/>, <see cref="RemoteSource.Branch"/>);
    /// null for a ref that one ref of one remote, a branch, does not fill (the operation logs "not
    /// started": a menu built before the lines changed). A ref the refs do not hold is looked up
    /// by its name, and its remote must be configured.
    /// </summary>
    private RemoteSource? SplitRemoteBranch(string remoteBranchRef)
    {
        if (Refs is not { } refs)
        {
            return null;
        }

        if (FindRef(remoteBranchRef) is { Kind: RefKind.RemoteBranch } gitRef)
        {
            return refs.SourceOf(gitRef) is { IsBranch: true } mapped ? mapped : null;
        }

        return refs.SourceOf(remoteBranchRef) is { IsBranch: true, Remote: { } configured } source
            && RemoteNames.Contains(configured, StringComparer.Ordinal)
            ? source
            : null;
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
        OperationReport? refused = null;
        return GuardAsync(text, () => ConfirmAndRunAsync(
            OperationTexts.ConfirmForcePush(branch.Name, remote, ShortName(remoteRef)),
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async _ =>
            {
                // 10.4's guard before anything is written, the backup included.
                refused = await RefuseIfPushStopsFetchAsync(text, branch.Name, remote, remoteRef, upstream);
                if (refused is not null)
                {
                    return null;
                }

                // UpstreamRef's commit as it is just before the push (2.20).
                var commit = await Git.ResolveAsync(upstreamRef);
                return commit is null ? null : new BackupRequest("force-push", [new BackupEntry(ShortName(upstreamRef), commit)]);
            },
            async (context, _) =>
            {
                if (refused is not null)
                {
                    return refused;
                }

                UndoCapture.Push(remote, await Git.PushAsync(request, NetworkOptions.From(context), context.CancellationToken));
                return OperationReport.Done(OperationTexts.ForcePushed(branch.Name, upstream));
            },
            NetworkRun(UndoScope.Push(branch.Name, remote, force: true), describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
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
            // Only a branch one ref of one remote fills (docs/phase-6-design.md 10.4 #11): git
            // refuses a ref two remotes or none fetch, and one fetched from two refs would track
            // git's first candidate.
            List<GitRef> remoteBranches = Refs is { } refs
                ? [.. refs.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch && refs.SourceOf(gitRef).IsBranch)]
                : [];
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
            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async _ =>
                {
                    await Git.SetUpstreamAsync(name, target);
                    return OperationReport.Done(OperationTexts.UpstreamSet(name, picked));
                },
                new RunOptions { Undo = UndoScope.Upstream(name) });
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
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async _ =>
            {
                await Git.UnsetUpstreamAsync(branch.Name);
                return OperationReport.Done(OperationTexts.UpstreamUnset(branch.Name, upstream));
            },
            new RunOptions { Undo = UndoScope.Upstream(branch.Name) }));
    }

    // 2.22: delete a branch on a remote.

    public Task DeleteRemoteBranchAsync(string remoteBranchRef)
    {
        ArgumentNullException.ThrowIfNull(remoteBranchRef);
        if (SplitRemoteBranch(remoteBranchRef) is not { Remote: { } remote, Branch: { } name })
        {
            Log.Info($"{Repository.Name}: Delete on a remote: not started, {remoteBranchRef} is not a remote branch.");
            return Task.CompletedTask;
        }

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
                UndoCapture.Push(remote, new PushOutcome([new PushedRef(HeadsPrefix + name, expected, null)]));
                return OperationReport.Done(OperationTexts.DeletedRemoteBranch(name, remote));
            },
            NetworkRun(UndoScope.DeleteRemoteBranch(name, remote), describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
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
                },
                new RunOptions { Undo = UndoScope.RemoveRemote(remote) });
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
        var error = await RunRemoteFormAsync(OperationTexts.AddRemote(name), () => Git.AddRemoteAsync(name, url), OperationTexts.AddedRemote(name), UndoScope.AddRemote(name));
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
        return RunRemoteFormAsync(OperationTexts.RenameRemote(name), () => Git.RenameRemoteAsync(name, newName), OperationTexts.RenamedRemote(name, newName), UndoScope.RenameRemote(name, newName));
    }

    /// <summary>Changes a remote's URL and waits. Null, or git's message for the form.</summary>
    public Task<string?> SetRemoteUrlAsync(string name, string url)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(url);
        return RunRemoteFormAsync(OperationTexts.SetRemoteUrl(name), () => Git.SetRemoteUrlAsync(name, url), OperationTexts.ChangedRemoteUrl(name), UndoScope.SetRemoteUrl(name));
    }

    /// <summary>One local operation of the Remotes form, recorded as <paramref name="undo"/> says: null when it succeeded, otherwise what the form's <c>RemoteError</c> shows (git's own message).</summary>
    private async Task<string?> RunRemoteFormAsync(OperationText text, Func<Task> write, string done, UndoScope undo)
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
            new RunOptions { ShowsErrorDialog = false, Undo = undo });
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
                    UndoCapture.Push(remote, await Git.PushTagAsync(remote, tag, NetworkOptions.From(context), context.CancellationToken));
                    return OperationReport.Done(OperationTexts.PushedTag(tag, remote));
                },
                NetworkRun(UndoScope.PushTag(tag, remote)));
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
                    UndoCapture.Push(remote, new PushOutcome([new PushedRef(TagsPrefix + tag, expected, null)]));
                    return OperationReport.Done(OperationTexts.DeletedRemoteTag(tag, remote));
                },
                NetworkRun(UndoScope.DeleteRemoteTag(tag, remote), describeFailure: failure => failure.Kind == GitFailureKind.PushRejectedStale
                    ? new FailureDisplay(OperationTexts.DeletionStoppedTitle, OperationTexts.RemoteTagDiffers(remote, tag))
                    : null));
        });
    }
}
