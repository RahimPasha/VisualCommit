// Undo and redo: docs/phase-5-design.md section 7 (D154, D155, D163, D165 to D172) and the
// report's part U with its decisions UD1 to UD20. The undo history's recording inside every
// recorded operation (7.7), the toolbar's Undo and Redo as one operation each with their states
// (7.4, 7.5), what each status read does to the history (7.2, 7.4), the restore bar's tie (7.9,
// UD18), the backups the history keeps from the clean-up (7.8, D171) and the backups list's
// restores (7.10, the report's BD15 to BD17 and BD20). The git layer runs the reads and the steps
// (IGitRepository.ReadRefStatesAsync, RunUndoStepAsync); Core's UndoSequence and UndoChecks make
// the effects and the checks; this file decides when, asks, and says what happened.

using System.Security.Cryptography;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>A mixed reset's <c>-changes</c> snapshot is the index alone, named so by its subject (12.3, 7.10).</summary>
    private const string MixedResetSnapshotSubject = "VisualCommit: index before a mixed reset";

    /// <summary>How many bytes of a file the backups list's question compares (BD15); a larger file counts as changed.</summary>
    private const long CompareBytes = 64L * 1024 * 1024;

    /// <summary>How many entries of a remote-tracking reflog a force push's lease is looked for in (7.10).</summary>
    private const int ForcePushReflogEntries = 500;

    /// <summary>The kinds whose action can stop with the banner showing (7.2): their entry is pending until it ends.</summary>
    private static readonly HashSet<UndoKind> StoppingKinds =
    [
        UndoKind.Merge, UndoKind.CheckoutAndMerge, UndoKind.CherryPick, UndoKind.Revert, UndoKind.Rebase,
        UndoKind.CheckoutAndRebase, UndoKind.InteractiveRebase, UndoKind.Squash, UndoKind.Pull,
    ];

    /// <summary>
    /// The in-memory checks last made for the toolbar (7.4), one per direction: kept while the
    /// entry, the refs and the status they were made against stay the same objects, so that the
    /// toolbar can read the states on every change of the queue for the price of four comparisons.
    /// </summary>
    private readonly CheckedStep?[] _checked = new CheckedStep?[2];

    /// <summary>
    /// The step a click started that has not ended: its direction, its entry and its task. A second
    /// click for the same entry while it asks, waits or runs (a fast double click, a key pressed
    /// twice before the toolbar followed) shares it, as the toolbar's Pop does; on its own it would
    /// find the history moved and end cancelled.
    /// </summary>
    private (UndoDirection Direction, UndoEntry Entry, Task Task)? _step;

    /// <summary>How many operations are between the start and the end of their recording now.</summary>
    private int _recordingsOpen;

    /// <summary>How many recordings have started or ended so far: a status read made while it changed may be older than what they did.</summary>
    private long _recordingEvents;

    /// <summary>1 while a read that decides whether a pending entry's operation ended runs (<see cref="RemovePendingIfEndedAsync"/>).</summary>
    private int _pendingCheckRuns;

    /// <summary>The toolbar's Undo and its key (7.4, 7.5, D163): the entry before the history's cursor, applied backwards as one operation.</summary>
    public Task UndoAsync() => StepAsync(UndoDirection.Undo);

    /// <summary>The toolbar's Redo and its key: the entry after the history's cursor, applied forwards as one operation.</summary>
    public Task RedoAsync() => StepAsync(UndoDirection.Redo);

    /// <summary>
    /// The toolbar's Undo and Redo as they stand (1.1, 7.4): each enabled with <c>Undo {text}</c>
    /// (<c>Redo {text}</c>) when its entry passes the checks that need no git against what the app
    /// last read, otherwise disabled with the first reason that holds: <c>Wait for {title} to
    /// finish</c> while a user operation runs (a background one gives none: a click queues behind
    /// it), <c>Finish or abort the {kind} first</c> or <c>Resolve the conflicts first</c>, <c>Close
    /// the interactive rebase first</c>, <c>Nothing to undo</c> (<c>Nothing to redo</c>), or the
    /// check's own text (7.6, without its <c>Can't undo …: </c>). Both disabled without a tooltip
    /// before the refs are read and after the tab closed. Cheap enough to read on every change of
    /// the queue: the checks are kept until the entry, the refs or the status change. Read it on
    /// the UI thread.
    /// </summary>
    public UndoAvailability ReadUndoAvailability()
    {
        if (IsClosed || Refs is null)
        {
            return UndoAvailability.Disabled;
        }

        var undo = ReasonAgainst(UndoDirection.Undo, includeBusy: true);
        var redo = ReasonAgainst(UndoDirection.Redo, includeBusy: true);
        return new UndoAvailability(undo.Enabled, undo.ToolTip, redo.Enabled, redo.ToolTip);
    }

    /// <summary>
    /// Hook (7.2, 7.4): the repository shows a newly read status, after a refresh's refs too
    /// (<see cref="RepositoryViewModel"/>'s <c>OnStatusApplied</c>), on the UI thread. A pending
    /// entry whose stopped operation is no longer in progress ended outside the app (a terminal):
    /// it goes once a read begun now says so too (<see cref="RemovePendingIfEndedAsync"/>), since
    /// this read may have begun before the action stopped. The toolbar then reads its states
    /// again, whose checks follow the new status.
    /// </summary>
    public void NoteUndoState(WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (Repository.UndoHistory.Pending is { } pending && !status.InProgress.IsActive)
        {
            _ = RemovePendingIfEndedAsync(pending);
        }

        Repository.Toolbar.UpdateUndoStates();
    }

    /// <summary>
    /// A status read showed nothing in progress while <paramref name="pending"/> waits (7.2). That
    /// read may have begun before the action stopped (a refresh in flight when the merge started,
    /// shown after its entry was added), so a read begun now decides: the entry goes when it too
    /// shows nothing in progress, the entry is still the pending one, and no operation recorded
    /// meanwhile (Continue, Skip, Abort and the stage panel's Commit settle the entry themselves,
    /// and an operation's refresh reads the status again after its recording). One such read at a
    /// time: a read begun while one runs would decide the same.
    /// </summary>
    private async Task RemovePendingIfEndedAsync(UndoEntry pending)
    {
        if (Volatile.Read(ref _recordingsOpen) > 0 || Interlocked.Exchange(ref _pendingCheckRuns, 1) == 1)
        {
            return;
        }

        try
        {
            var events = Interlocked.Read(ref _recordingEvents);
            var inProgress = await Git.ReadInProgressStateAsync(CancellationToken.None);
            var history = Repository.UndoHistory;
            if (IsClosed
                || Volatile.Read(ref _recordingsOpen) > 0
                || Interlocked.Read(ref _recordingEvents) != events
                || !ReferenceEquals(history.Pending, pending))
            {
                return;
            }

            if (inProgress.IsActive)
            {
                Log.Debug($"{Repository.Name}: the stopped {pending.Text} is still in progress; its undo entry stays.");
                return;
            }

            Log.Info($"{Repository.Name}: the stopped {pending.Text} ended outside the app; its undo entry goes.");
            history.RemovePending();
            Repository.Toolbar.UpdateUndoStates();
        }
        catch (Exception ex)
        {
            // Nothing waits for this read: it never throws. After the tab closed it says nothing.
            if (!IsClosed)
            {
                Log.Warning($"{Repository.Name}: whether the stopped {pending.Text} ended could not be read; its undo entry stays.", ex);
            }
        }
        finally
        {
            Volatile.Write(ref _pendingCheckRuns, 0);
        }
    }

    /// <summary>
    /// The backup refs the undo histories of every worktree of this repository need (7.8, D171):
    /// the clean-up keeps them while their entries exist (<see cref="RepositoryViewModel.BackupsToKeep"/>).
    /// </summary>
    public IReadOnlySet<string> UndoBackupsToKeep() =>
        Services?.UndoHistories.BackupsOf(Git.CommonDirectory) ?? Repository.UndoHistory.Backups;

    /// <summary>
    /// Hook (7.9, UD18): the restore bar's Restore of <paramref name="snapshot"/>. While the discard
    /// that saved it is the entry before the history's cursor, Undo would be enabled for it, and its
    /// files are still as the discard left them, the Restore is that entry's Undo: it runs, and the
    /// result is true. Otherwise false, and the Restore runs as in phase 2 (D75's question when a file
    /// changed since), recorded as an entry of its own.
    /// </summary>
    public async Task<bool> TryRestoreAsUndoAsync(DiscardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (IsClosed
            || Refs is null
            || Repository.UndoHistory.ToUndo is not { Kind: UndoKind.Discard, State: UndoEntryState.Done } entry
            || !string.Equals(entry.Captured.Snapshot?.Ref, snapshot.Ref, StringComparison.Ordinal)
            || !ReasonAgainst(UndoDirection.Undo, includeBusy: false).Enabled)
        {
            return false;
        }

        // The bar's own check first (D75): a file changed since the discard asks instead.
        var root = Git.WorkingDirectory;
        var unchanged = await Task.Run(() => entry.After.Files.All(file => string.Equals(Sha256Of(root, file.Path), file.Sha256, StringComparison.OrdinalIgnoreCase)));
        if (!unchanged)
        {
            return false;
        }

        await StepAsync(UndoDirection.Undo);
        return true;
    }

    /// <summary>
    /// A backup's own restore in the backups list (7.10): its kind decides, as the list's menu
    /// does. Each is one operation, recorded in the undo history like any action, whose ref writes
    /// say <c>VisualCommit: &lt;entry text&gt;</c> (BD17): <c>Restore branch {b}</c> and <c>Restore
    /// tag {t}</c> create the ref at the backup's object; <c>Restore stash</c> stores it in the list
    /// again with its commit's subject; <c>Restore the discarded files…</c> and <c>Restore the
    /// changes…</c> ask D75's question when a file would be overwritten (BD15), save the newer
    /// version first, put a hard reset's index back for its own paths when nothing is staged now
    /// (BD16), then the snapshot's files; <c>Restore the staged changes</c> stages a mixed reset's
    /// snapshot for its own paths (BD16); <c>Push {b} back to {r}…</c> asks 7.5's question and pushes
    /// with a lease that says the branch must not exist, or for a force push's backup that it
    /// still holds the commit the force push left. A kind without a restore does nothing. A backup
    /// whose commit a <c>gc</c> pruned after the list read it (its ref gone meanwhile) is refused
    /// with 7.6's "the commits no longer exist" (design 5.2's <c>cat-file</c> check): by the step's
    /// own check of the objects it writes, and before the reads and questions that come first.
    /// </summary>
    public Task RestoreBackupAsync(BackupInfo backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        var main = backup.Main;
        var changes = backup.Refs.FirstOrDefault(entry => entry.IsChanges);
        var name = main?.Name ?? string.Empty;
        switch (backup.Kind)
        {
            case "branch" when main is not null:
                return RestoreBranchAsync(name, main);
            case "tag" or "tag-undone" when main is not null:
                return RestoreTagAsync(name, main);
            case "stash" when main is not null:
                return RestoreStashAsync(main);
            case "discard" when main is not null:
                return RestoreFilesAsync(main, discarded: true, putsIndexBack: false);
            case "reset" when changes is not null && string.Equals(changes.Subject, MixedResetSnapshotSubject, StringComparison.Ordinal):
                return RestoreStagedChangesAsync(changes);
            case "reset" or "abort" or "skip" when changes is not null:
                return RestoreFilesAsync(changes, discarded: false, putsIndexBack: backup.Kind == "reset");
            case "remote-branch" or "remote-tag" when main is not null:
                return PushBackAsync(name, main, isTag: backup.Kind == "remote-tag");
            case "force-push" when main is not null:
                return PushForcePushBackAsync(name, main);
            default:
                Log.Info($"{Repository.Name}: the backup {backup.Name} has no restore of its own.");
                return Task.CompletedTask;
        }
    }

    // ---- Undo and redo (7.5) ----------------------------------------------------------------------

    /// <summary>
    /// One step as one operation of the queue (7.5): the entry at the cursor, asked about first when
    /// the step changes a remote (D155, D170: 7.5's questions), a network operation then; titled
    /// <c>Undo {text}</c> or <c>Redo {text}</c>, refreshing the refs. Nothing happens when there is
    /// no finished entry to step over; a click while the step for the same entry has not ended
    /// gets that step's task.
    /// </summary>
    private Task StepAsync(UndoDirection direction)
    {
        if (IsClosed)
        {
            return Task.CompletedTask;
        }

        var history = Repository.UndoHistory;
        if ((direction == UndoDirection.Undo ? history.ToUndo : history.ToRedo) is not { State: UndoEntryState.Done } entry)
        {
            Log.Info($"{Repository.Name}: {(direction == UndoDirection.Undo ? OperationTexts.NothingToUndo : OperationTexts.NothingToRedo)}.");
            return Task.CompletedTask;
        }

        var text = OperationTexts.Step(direction, entry.Text);
        if (_step is { Task.IsCompleted: false } started && started.Direction == direction && ReferenceEquals(started.Entry, entry))
        {
            Log.Info($"{Repository.Name}: {text.Title}: started already; the click joins it.");
            return started.Task;
        }

        var step = AskAndRunStepAsync(direction, entry, text);
        _step = (direction, entry, step);
        return step;
    }

    /// <summary><see cref="StepAsync"/>'s step: 7.5's question when it changes a remote, then its operation.</summary>
    private Task AskAndRunStepAsync(UndoDirection direction, UndoEntry entry, OperationText text) =>
        GuardAsync(text, async () =>
        {
            var step = UndoStep.For(entry, direction);
            if (step.ChangesRemote && !await ConfirmAsync(await RemoteQuestionAsync(step)))
            {
                return;
            }

            await RunAsync(
                text,
                step.ChangesRemote ? OperationKind.Network : OperationKind.Local,
                RefreshScope.Refs,
                context => RunStepAsync(context, text, step));
        });

    /// <summary>
    /// Inside the step's operation: the step runs in the git layer (7.5 steps 1 to 5), the history's
    /// cursor moves over the entry it returns (rewritten with what the step made), and what the
    /// app shows follows: the restore bar of a discard (7.9), the message of a commit (7.5). A
    /// refused step changes nothing and says why in the error dialog.
    /// </summary>
    private async Task<OperationReport> RunStepAsync(OperationContext context, OperationText text, UndoStep step)
    {
        var history = Repository.UndoHistory;
        var entry = step.Entry;
        if (!ReferenceEquals(step.Direction == UndoDirection.Undo ? history.ToUndo : history.ToRedo, entry))
        {
            // The history moved between the click and now (another tab on this worktree): this is
            // not the step that was asked for.
            Log.Info($"{Repository.Name}: {text.Title}: the undo history changed before it ran; nothing was done.");
            throw new OperationCanceledException();
        }

        // The commit an undo leaves staged, whose message the stage panel gets back (7.5).
        var message = entry.Kind is UndoKind.Commit or UndoKind.Amend ? await ReadMessageAsync(entry.After.Head.Sha) : null;
        var result = await Git.RunUndoStepAsync(step, NetworkOptions.From(context), context.CancellationToken);
        if (result.Refusal is { } refusal)
        {
            return Refused(context, text, refusal, OperationTexts.CantStep(step.Direction, entry.Text, await ReasonOfAsync(refusal)));
        }

        if (step.Direction == UndoDirection.Undo)
        {
            history.Undone(result.Entry);
        }
        else
        {
            history.Redone(result.Entry);
        }

        await AfterStepAsync(step, message, context.CancellationToken);
        return OperationReport.Done(OperationTexts.Stepped(step.Direction, entry.Text));
    }

    /// <summary>
    /// What follows a step in what the app shows: a discard's restore bar goes with its Undo and
    /// comes back with its Redo, for the same snapshot (7.9); an undone commit's message fills the
    /// stage panel's empty boxes, and its Redo takes it out of boxes that still hold it (7.5).
    /// </summary>
    private async Task AfterStepAsync(UndoStep step, (string Subject, string Body)? message, CancellationToken cancellationToken)
    {
        var undo = step.Direction == UndoDirection.Undo;
        if (step.Entry is { Kind: UndoKind.Discard, Captured.Snapshot: { } snapshot })
        {
            if (undo)
            {
                Repository.HideRestoreBar(snapshot);
            }
            else
            {
                await Repository.ShowRestoreBarAsync(snapshot, cancellationToken);
            }
        }

        if (message is { } filled)
        {
            if (undo)
            {
                Repository.Changes.PutBackUndoneMessage(filled.Subject, filled.Body);
            }
            else
            {
                Repository.Changes.TakeBackRedoneMessage(filled.Subject, filled.Body);
            }
        }
    }

    /// <summary>A commit's subject and body, or null when it cannot be read: the stage panel then keeps its boxes.</summary>
    private async Task<(string Subject, string Body)?> ReadMessageAsync(string? commit)
    {
        if (commit is null)
        {
            return null;
        }

        try
        {
            var details = await Git.ReadCommitDetailsAsync(commit);
            return (details.Subject, details.Body);
        }
        catch (GitException ex)
        {
            Log.Warning($"{Repository.Name}: the message of {commit} could not be read for the stage panel.", ex);
            return null;
        }
    }

    /// <summary>
    /// 7.5's question before a step that changes a remote, about its first remote ref: the remote,
    /// the ref's name there, and the commits it holds before and after (an annotated tag named by
    /// the commit it points to, UD10).
    /// </summary>
    private async Task<ConfirmationRequest> RemoteQuestionAsync(UndoStep step)
    {
        var remote = step.Effects.OfType<UndoEffect.RemoteRef>().First();
        var isTag = remote.Ref.StartsWith(TagsPrefix, StringComparison.Ordinal);
        return OperationTexts.RemoteStepQuestion(
            step.Direction,
            remote.Remote,
            ShortName(remote.Ref),
            isTag,
            await ShortCommitAsync(remote.From, isTag),
            await ShortCommitAsync(remote.To, isTag));
    }

    /// <summary>The short SHA of <paramref name="value"/>, a tag object peeled to its commit first.</summary>
    private async Task<string?> ShortCommitAsync(string? value, bool peel)
    {
        if (value is null)
        {
            return null;
        }

        var commit = peel ? await Git.ResolveAsync(value + "^{commit}") ?? value : value;
        return OperationTexts.Short(commit);
    }

    /// <summary>A refusal's reason (7.6, 7.12); an operation in progress named by its kind, read now.</summary>
    private async Task<string> ReasonOfAsync(UndoRefusal refusal)
    {
        if (refusal.Kind != UndoRefusalKind.InProgress)
        {
            return OperationTexts.RefusalReason(refusal, Repository.Path);
        }

        var state = await Git.ReadInProgressStateAsync();
        return state.IsActive ? OperationTexts.FinishOrAbortFirst(state.Kind) : OperationTexts.RefusalReason(refusal, Repository.Path);
    }

    /// <summary>
    /// A refused step or restore (7.5, 7.6, D95): the error dialog with <paramref name="summary"/>,
    /// and git's output and command when git refused (UD13: the paths in a move's way, a lease
    /// that failed, UD9), found in the operation's own calls; the app's own checks show no output.
    /// A lease's summary is 7.5's, without the prefix (UD9).
    /// </summary>
    private OperationReport Refused(OperationContext context, OperationText text, UndoRefusal refusal, string summary)
    {
        if (refusal.Kind == UndoRefusalKind.RemoteChanged)
        {
            summary = OperationTexts.RemoteChangedSince(refusal.Name ?? string.Empty, refusal.Remote ?? string.Empty);
        }

        Log.Info($"{Repository.Name}: {text.Title}: refused, {summary}");
        return Failed(text, RefusalFailure(context, refusal, summary));
    }

    /// <summary>The failure a refusal shows: git's own call when git refused, otherwise the summary alone.</summary>
    private GitFailure RefusalFailure(OperationContext context, UndoRefusal refusal, string summary)
    {
        if (refusal.GitOutput is not { Length: > 0 } output)
        {
            return new GitFailure(GitFailureKind.Other, summary, string.Empty, string.Empty, null);
        }

        var call = Services?.GitCalls?.Snapshot().LastOrDefault(record =>
            string.Equals(record.OperationId, context.OperationId, StringComparison.Ordinal)
            && !record.Succeeded
            && string.Equals(record.StandardError.Trim(), output, StringComparison.Ordinal));
        return call is null
            ? new GitFailure(GitFailureKind.Other, summary, output, string.Empty, null)
            : new GitFailure(
                GitFailureClassifier.Classify(call.StandardError, call.StandardOutput, call.ExitCode),
                summary,
                GitOutputCleaner.Clean(call.StandardError, call.StandardOutput),
                call.CommandText,
                call.ExitCode);
    }

    // ---- The toolbar's states (7.4) ----------------------------------------------------------------

    /// <summary>
    /// The state of Undo or Redo: enabled with its tooltip, or the first of 7.4's reasons that holds.
    /// <paramref name="includeBusy"/> false leaves out the running operation (the restore bar's
    /// Restore, which queues as any write does).
    /// </summary>
    private (bool Enabled, string ToolTip) ReasonAgainst(UndoDirection direction, bool includeBusy)
    {
        var history = Repository.UndoHistory;
        var entry = direction == UndoDirection.Undo ? history.ToUndo : history.ToRedo;
        var status = Status;
        string? reason = null;
        if (includeBusy && Queue.Current is { IsBackground: false } current)
        {
            reason = OperationTexts.WaitFor(current.Title);
        }
        else if (status.InProgress.IsActive)
        {
            reason = OperationTexts.FinishOrAbortFirst(status.InProgress.Kind);
        }
        else if (Conflicted(status) is { Count: > 0 } conflicted && !PutsConflictsBack(direction, entry, conflicted))
        {
            reason = OperationTexts.ResolveConflictsFirst;
        }
        else if (Repository.IsInteractiveRebaseOpen)
        {
            reason = OperationTexts.CloseInteractiveRebaseFirst;
        }
        else if (entry is not { State: UndoEntryState.Done })
        {
            reason = direction == UndoDirection.Undo ? OperationTexts.NothingToUndo : OperationTexts.NothingToRedo;
        }
        else if (CheckInMemory(direction, entry) is { } refusal)
        {
            reason = OperationTexts.RefusalReason(refusal, Repository.Path);
        }

        return reason is null ? (true, OperationTexts.StepToolTip(direction, entry!.Text)) : (false, reason);
    }

    /// <summary>The conflicted files of <paramref name="status"/>.</summary>
    private static IReadOnlyList<string> Conflicted(WorkingTreeStatus status) =>
        [.. status.Unstaged.Concat(status.Staged).Where(file => file.Kind == FileChangeKind.Conflicted).Select(file => file.Path).Distinct(StringComparer.Ordinal)];

    /// <summary>7.4's exception to "Resolve the conflicts first": the undo of an apply or a pop whose recorded files hold every conflicted one puts them back as they were.</summary>
    private static bool PutsConflictsBack(UndoDirection direction, UndoEntry? entry, IReadOnlyList<string> conflicted) =>
        direction == UndoDirection.Undo
        && entry is { Kind: UndoKind.ApplyStash or UndoKind.PopStash }
        && conflicted.All(path => entry.Before.Files.Any(file => string.Equals(file.Path, path, StringComparison.Ordinal)));

    /// <summary>
    /// 7.4's checks that need no git, against the refs and the status as last read: made once per
    /// entry, refs and status, then kept. The worktrees' branches are checked inside the step,
    /// where the refs are read with the worktrees that have them checked out.
    /// </summary>
    private UndoRefusal? CheckInMemory(UndoDirection direction, UndoEntry entry)
    {
        var refs = Refs!;
        var status = Status;
        var slot = (int)direction;
        if (_checked[slot] is { } kept && ReferenceEquals(kept.Entry, entry) && ReferenceEquals(kept.Refs, refs) && ReferenceEquals(kept.Status, status))
        {
            return kept.Refusal;
        }

        UndoRefusal? refusal;
        try
        {
            refusal = UndoChecks.FirstRefusalInMemory(UndoStep.For(entry, direction), refs, status, []);
        }
        catch (InvalidOperationException ex)
        {
            // Recorded entries are checked when they are added; this one lacks what its kind needs.
            Log.Warning($"{Repository.Name}: the {direction} of {entry.Text} cannot be made.", ex);
            refusal = new UndoRefusal(UndoRefusalKind.CommitsGone);
        }

        _checked[slot] = new CheckedStep(entry, refs, status, refusal);
        return refusal;
    }

    /// <summary>The SHA-256 of a working-tree file's bytes, upper-case hex, or null when it does not exist or cannot be read.</summary>
    private static string? Sha256Of(string root, string path)
    {
        var full = System.IO.Path.Combine(root, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        try
        {
            if (!File.Exists(full))
            {
                return null;
            }

            using var stream = File.OpenRead(full);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "unreadable";
        }
    }

    // ---- Recording (7.7) ------------------------------------------------------------------------------

    internal partial async Task<UndoRecording?> BeginUndoRecordingAsync(UndoScope scope, OperationContext context)
    {
        // Counted before anything can fail, for RemovePendingIfEndedAsync: the end of the
        // recording, which every operation reaches, counts it out.
        Interlocked.Increment(ref _recordingsOpen);
        Interlocked.Increment(ref _recordingEvents);
        var pending = Repository.UndoHistory.Pending;
        if (scope.RemovesPending || scope.CompletesPending)
        {
            return pending is null ? null : new UndoRecording(scope, null, context.OperationId, scope.RemovesPending ? UndoRecordingMode.RemovePending : UndoRecordingMode.CompletePending, WasInProgress: true);
        }

        if (!scope.IsRecorded)
        {
            return null;
        }

        // The stage panel's Commit that ends a stopped operation completes its pending entry, and
        // records nothing for one started outside the app (7.2).
        if (scope.Kind is UndoKind.Commit or UndoKind.Amend && Status.InProgress.IsActive)
        {
            return pending is null ? null : new UndoRecording(scope, null, context.OperationId, UndoRecordingMode.CompletePending, WasInProgress: true);
        }

        var before = await Git.ReadRefStatesAsync(scope.BeforeAction(), cancellationToken: CancellationToken.None);
        return new UndoRecording(scope, before, context.OperationId, UndoRecordingMode.Record, Status.InProgress.IsActive);
    }

    internal partial async Task EndUndoRecordingAsync(UndoRecording? recording, UndoCaptured captured, OperationReport? report)
    {
        try
        {
            await RecordAsync(recording, captured);
        }
        finally
        {
            Interlocked.Decrement(ref _recordingsOpen);
            Interlocked.Increment(ref _recordingEvents);
        }
    }

    /// <summary>
    /// <see cref="EndUndoRecordingAsync"/>'s work: the pending entry completed or removed, or the
    /// read after the action and its entry when anything changed (7.7 steps 4 and 5).
    /// </summary>
    private async Task RecordAsync(UndoRecording? recording, UndoCaptured captured)
    {
        if (recording is null)
        {
            return;
        }

        var history = Repository.UndoHistory;
        switch (recording.Mode)
        {
            case UndoRecordingMode.RemovePending:
                // D165: an Abort that ended the operation takes its entry; one git refused leaves it.
                if (!(await Git.ReadInProgressStateAsync(CancellationToken.None)).IsActive)
                {
                    history.RemovePending();
                }

                return;

            case UndoRecordingMode.CompletePending:
                await CompletePendingAsync();
                return;
        }

        var scope = recording.Scope;
        var before = recording.Before!;
        captured = await CompleteCaptureAsync(scope, before, captured);
        var after = await Git.ReadRefStatesAsync(scope.AfterAction(captured.ChangedFiles), cancellationToken: CancellationToken.None);

        // An action that stopped (a merge's conflicts change no ref) is pending (7.2); one refused
        // while an operation was in progress already is not.
        var kind = scope.Kind!.Value;
        var stopped = StoppingKinds.Contains(kind) && !recording.WasInProgress && (await Git.ReadInProgressStateAsync(CancellationToken.None)).IsActive;
        if (!stopped && !Changed(before, after, captured))
        {
            Log.Debug($"{Repository.Name}: {scope.Text}: nothing changed, nothing is recorded.");
            return;
        }

        var entry = new UndoEntry(kind, TextOf(scope, captured, after), recording.OperationId, before, after, captured)
        {
            Scope = scope,
            State = stopped ? UndoEntryState.Pending : UndoEntryState.Done,
        };
        if (WhyNotRecorded(entry) is { } why)
        {
            Log.Info($"{Repository.Name}: {entry.Text}: not recorded, {why}.");
            return;
        }

        history.Add(entry);
    }

    /// <summary>
    /// Continue, Skip or the stage panel's Commit ended a stopped operation (7.2): its pending entry
    /// becomes one entry from before the action to now, read with the action's own scope. One that
    /// stopped again stays pending.
    /// </summary>
    private async Task CompletePendingAsync()
    {
        var history = Repository.UndoHistory;
        if (history.Pending is not { } pending || (await Git.ReadInProgressStateAsync(CancellationToken.None)).IsActive)
        {
            return;
        }

        var after = await Git.ReadRefStatesAsync(pending.Scope.AfterAction([]), cancellationToken: CancellationToken.None);
        var completed = pending with { After = after, State = UndoEntryState.Done };
        if (!Changed(pending.Before, after, pending.Captured) || WhyNotRecorded(completed) is not null)
        {
            history.RemovePending();
            return;
        }

        history.Complete(after);
    }

    /// <summary>
    /// What the recorder adds to what the operation captured (7.7 step 3): a hard reset's snapshot
    /// as its undo needs it (its files, its paths and the index it holds: given by the reset that
    /// made it, or read from the snapshot commit, UndoCapture's "completed by the recorder"), whose
    /// paths the read after it records, and the bytes an ignore appended to <c>.gitignore</c>, read
    /// from the file after the append.
    /// </summary>
    private async Task<UndoCaptured> CompleteCaptureAsync(UndoScope scope, UndoState before, UndoCaptured captured)
    {
        if (captured is { ResetMode: ResetMode.Hard, ResetSnapshot: { } changes, Snapshot: null })
        {
            var details = await Git.ReadCommitDetailsAsync(changes);
            var snapshot = await ReadSnapshotAsync(captured.Backups.FirstOrDefault(name => name.EndsWith("-changes", StringComparison.Ordinal)) ?? string.Empty, changes, details.Body);
            captured = captured with
            {
                Snapshot = snapshot,
                ResetIndex = details.Parents.Count > 1 ? details.Parents[1] : null,
            };
        }

        if (captured is { ResetMode: ResetMode.Hard, ResetSnapshot: not null, Snapshot: { } files })
        {
            captured = captured with { ChangedFiles = [.. captured.ChangedFiles.Union(files.Paths, StringComparer.Ordinal)] };
        }

        if (scope.Gitignore && captured.GitignoreAppended is null)
        {
            var path = System.IO.Path.Combine(Git.WorkingDirectory, ".gitignore");
            var skip = before.Gitignore is { Exists: true } was ? was.Length : 0;
            if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path);
                if (bytes.Length > skip)
                {
                    captured = captured with { GitignoreAppended = System.Text.Encoding.Latin1.GetString(bytes, (int)skip, bytes.Length - (int)skip) };
                }
            }
        }

        return captured;
    }

    /// <summary>
    /// Whether the action changed anything its scope names, or a remote (a push's values), or put
    /// an index back. Files by their content: the read before an apply keeps their bytes, the read
    /// after it their SHA-256 only (<see cref="UndoScope.AfterAction"/>).
    /// </summary>
    private static bool Changed(UndoState before, UndoState after, UndoCaptured captured) =>
        before.Head != after.Head
        || !SameMap(before.Refs, after.Refs)
        || !SameMap(before.SymbolicRefs, after.SymbolicRefs)
        || !before.Stashes.SequenceEqual(after.Stashes)
        || !before.Config.SequenceEqual(after.Config)
        || before.Files.Count != after.Files.Count
        || before.Files.Zip(after.Files).Any(pair => !pair.First.SameContent(pair.Second))
        || !before.IndexEntries.SequenceEqual(after.IndexEntries, StringComparer.Ordinal)
        || !string.Equals(before.Status, after.Status, StringComparison.Ordinal)
        || before.Gitignore != after.Gitignore
        || captured.Push is { Refs.Count: > 0 }
        || captured.IndexSource is not null;

    private static bool SameMap(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));

    /// <summary>An entry's text: its scope's (7.6), a stash's from the list's message of the stash it made (never <c>stash@{n}</c>).</summary>
    private static string TextOf(UndoScope scope, UndoCaptured captured, UndoState after) =>
        scope.Kind == UndoKind.Stash && captured.Stash is { } stash && after.Stashes.FirstOrDefault(entry => string.Equals(entry.Sha, stash, StringComparison.Ordinal)) is { } made
            ? UndoTexts.Stash(made.Message)
            : scope.Text;

    /// <summary>
    /// Why an entry is not added, or null: D91's checkout whose pop conflicted (its stash is kept
    /// and named, 7.3); a checkout that left HEAD where it was and created nothing; a finished
    /// entry whose sequences cannot be made from what was read (a recording that missed a value),
    /// which would only fail later.
    /// </summary>
    private string? WhyNotRecorded(UndoEntry entry)
    {
        if (entry.Kind == UndoKind.StashAndCheckout && entry.Captured.Stash is { } kept && entry.After.Stashes.Any(stash => string.Equals(stash.Sha, kept, StringComparison.Ordinal)))
        {
            return "its stash was kept";
        }

        if (entry.Kind is UndoKind.Checkout or UndoKind.AutoStashCheckout or UndoKind.StashAndCheckout
            && entry.Before.Head == entry.After.Head
            && !entry.After.Refs.Keys.Except(entry.Before.Refs.Keys, StringComparer.Ordinal).Any())
        {
            return "HEAD did not move";
        }

        if (entry.State != UndoEntryState.Done)
        {
            return null;
        }

        try
        {
            _ = UndoSequence.For(entry, UndoDirection.Undo);
            _ = UndoSequence.For(entry, UndoDirection.Redo);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning($"{Repository.Name}: {entry.Text} cannot be undone from what was read.", ex);
            return "its undo cannot be made";
        }
    }

    // ---- The backups list's restores (7.10) ----------------------------------------------------------

    /// <summary>"Restore branch {b}": the branch created at the backup's commit with a reflog of its own; the backup has no upstream keys.</summary>
    private Task RestoreBranchAsync(string branch, BackupRefInfo backup)
    {
        var text = OperationTexts.RestoreBranch(branch);
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                var refusal = await RunEffectsAsync(context, UndoKind.CreateBranch, UndoTexts.RestoreBranch(branch), [new UndoEffect.SetRef(HeadsPrefix + branch, null, backup.Commit) { CreateReflog = true }]);
                return refusal is null ? OperationReport.Done(OperationTexts.RestoredBranch(branch)) : await RestoreRefusedAsync(context, text, refusal);
            },
            new RunOptions { Undo = UndoScope.RestoreBranch(branch) }));
    }

    /// <summary>"Restore tag {t}": the tag created at the backup's object, the same tag object for an annotated tag.</summary>
    private Task RestoreTagAsync(string tag, BackupRefInfo backup)
    {
        var text = OperationTexts.RestoreTag(tag);
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                var refusal = await RunEffectsAsync(context, UndoKind.CreateTag, UndoTexts.RestoreTag(tag), [new UndoEffect.TagRef(tag, backup.Target, Present: true)]);
                return refusal is null ? OperationReport.Done(OperationTexts.RestoredTag(tag)) : await RestoreRefusedAsync(context, text, refusal);
            },
            new RunOptions { Undo = UndoScope.RestoreTag(tag) }));
    }

    /// <summary>"Restore stash": the stash stored in the list again with its commit's subject, as <c>stash@{0}</c>.</summary>
    private Task RestoreStashAsync(BackupRefInfo backup)
    {
        var text = OperationTexts.RestoreStash;
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                var refusal = await RunEffectsAsync(context, UndoKind.RestoreStash, UndoTexts.RestoreStash(backup.Subject), [new UndoEffect.StashStore(backup.Target, backup.Subject)]);
                return refusal is null ? OperationReport.Done(OperationTexts.RestoredStash) : await RestoreRefusedAsync(context, text, refusal);
            },
            new RunOptions { Undo = UndoScope.RestoreStash(backup.Subject) }));
    }

    /// <summary>
    /// "Restore the discarded files…" and "Restore the changes…" (7.10, BD15, BD16, BD20): D75's
    /// question when a file of the snapshot would be overwritten; on Restore those files are saved
    /// in a snapshot of their own first; a hard reset's snapshot puts the index back for its own
    /// paths when nothing is staged now; then the snapshot's files; then the clean-up when a
    /// snapshot was saved. The status counts every path of the snapshot. The undo history learns
    /// what was put back only when the step went through: a refused or failed one changed nothing,
    /// and would otherwise be recorded as an entry whose undo refuses and blocks the older ones.
    /// </summary>
    private Task RestoreFilesAsync(BackupRefInfo backup, bool discarded, bool putsIndexBack)
    {
        var text = discarded ? OperationTexts.RestoreDiscardedFiles : OperationTexts.RestoreTheChanges;
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            // Design 5.2: the snapshot must still exist before it is read; the step checks it again.
            if (!await Git.CommitExistsAsync(backup.Target))
            {
                await ShowRefusalAsync(text, CommitsGoneSummary(text));
                return;
            }

            var snapshot = await ReadSnapshotAsync(backup.FullName, backup.Target, backup.Body);
            var changed = await ChangedSinceSnapshotAsync(snapshot);
            if (changed.Count > 0 && !await ConfirmAsync(OperationTexts.RestoreChangedFilesQuestion(changed)))
            {
                return;
            }

            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    if (changed.Count > 0)
                    {
                        // D75: the newer version first, in a snapshot of its own.
                        await Git.SaveSnapshotAsync(changed);
                    }

                    var index = putsIndexBack ? await IndexOfHardResetAsync(snapshot.Commit) : null;
                    List<UndoEffect> effects = index is null ? [] : [index];
                    effects.Add(new UndoEffect.RestoreSnapshot(snapshot, []));
                    var entryText = discarded ? UndoTexts.Restore : UndoTexts.RestoreChanges;
                    var refusal = await RunEffectsAsync(context, UndoKind.Restore, entryText, effects);
                    if (refusal is null)
                    {
                        UndoCapture.Snapshot(snapshot);
                        if (index is not null)
                        {
                            UndoCapture.IndexRestored(index.Source, index.Paths);
                        }
                    }

                    if (changed.Count > 0)
                    {
                        await CleanUpBackupsAsync();
                    }

                    return refusal is null ? OperationReport.Done(OperationTexts.RestoredFiles(snapshot.Paths.Count)) : await RestoreRefusedAsync(context, text, refusal);
                },
                new RunOptions { Undo = UndoScope.RestoreChanges(snapshot.Paths, discarded) });
        });
    }

    /// <summary>
    /// A hard reset's snapshot's index (its second parent) put back for the paths it differs in
    /// from the snapshot's first parent (BD16) and from HEAD (<see cref="DifferingFromHeadAsync"/>),
    /// when nothing is staged now; null when something is, when the index had conflicts (no second
    /// parent) or when no such path is left.
    /// </summary>
    private async Task<UndoEffect.IndexTo?> IndexOfHardResetAsync(string snapshot)
    {
        var parents = (await Git.ReadCommitDetailsAsync(snapshot)).Parents;
        if (parents.Count < 2 || (await Git.ReadStatusAsync()).Staged.Count > 0 || await Git.ResolveAsync("HEAD") is not { } head)
        {
            return null;
        }

        var paths = await DifferingFromHeadAsync(parents[1], head, await PathsChangedByAsync(parents[1]));
        return paths.Count == 0 ? null : new UndoEffect.IndexTo(parents[1], head) { Paths = paths };
    }

    /// <summary>
    /// "Restore the staged changes" (7.10, BD16, BD21): a mixed reset's snapshot staged again for
    /// the paths it differs in from its parent and from HEAD (<see cref="DifferingFromHeadAsync"/>),
    /// while nothing is staged ("Unstage your changes first" otherwise, the item's disabled
    /// reason). Recorded only when the step went through, as <see cref="RestoreFilesAsync"/>.
    /// </summary>
    private Task RestoreStagedChangesAsync(BackupRefInfo backup)
    {
        var text = OperationTexts.RestoreStagedChanges;
        return GuardAsync(text, () => RunAsync(
            text,
            OperationKind.Local,
            RefreshScope.Refs,
            async context =>
            {
                // Design 5.2: the snapshot must still exist before its paths are read.
                if (!await Git.CommitExistsAsync(backup.Target))
                {
                    return await RestoreRefusedAsync(context, text, new UndoRefusal(UndoRefusalKind.CommitsGone));
                }

                var status = await Git.ReadStatusAsync();
                if (status.Staged.Count > 0 || Conflicted(status).Count > 0)
                {
                    return Failed(text, new GitFailure(GitFailureKind.Other, OperationTexts.UnstageYourChangesFirst, string.Empty, string.Empty, null));
                }

                if (await Git.ResolveAsync("HEAD") is not { } head
                    || await DifferingFromHeadAsync(backup.Target, head, await PathsChangedByAsync(backup.Target)) is not { Count: > 0 } paths)
                {
                    return OperationReport.Done(OperationTexts.RestoredStagedChanges);
                }

                var refusal = await RunEffectsAsync(context, UndoKind.RestoreStagedChanges, UndoTexts.RestoreStagedChanges, [new UndoEffect.IndexTo(backup.Target, head) { Paths = paths }]);
                if (refusal is not null)
                {
                    return await RestoreRefusedAsync(context, text, refusal);
                }

                UndoCapture.IndexRestored(backup.Target, paths);
                return OperationReport.Done(OperationTexts.RestoredStagedChanges);
            },
            new RunOptions { Undo = UndoScope.RestoreStagedChanges() }));
    }

    /// <summary>
    /// "Push {b} back to {r}…" of a deleted remote branch or tag (7.10): the remote and the name
    /// split by the remote list's longest matching name, as the deletion did; 7.5's question; a push
    /// whose lease says the ref must not exist.
    /// </summary>
    private Task PushBackAsync(string name, BackupRefInfo backup, bool isTag)
    {
        var remote = RemoteNames.Where(candidate => name.StartsWith(candidate + "/", StringComparison.Ordinal)).MaxBy(candidate => candidate.Length);
        var rest = remote is null ? name : name[(remote.Length + 1)..];
        var text = OperationTexts.PushBack(rest, remote ?? name);
        if (remote is null)
        {
            return GuardAsync(text, () => ShowRefusalAsync(text, OperationTexts.RemoteNoLongerExists(name.Split('/')[0])));
        }

        var remoteRef = (isTag ? TagsPrefix : HeadsPrefix) + rest;
        return PushBackAsync(text, remote, remoteRef, rest, isTag, lease: null, backup);
    }

    /// <summary>
    /// "Push {b} back to {r}…" of a force push's backup (7.10): <c>refs/remotes/&lt;name&gt;</c> maps
    /// back to its remote and branch through the local branch whose upstream it is, otherwise by the
    /// remote list's longest matching name (the default fetch refspec); the lease is the commit the
    /// force push left, read from the remote-tracking reflog.
    /// </summary>
    private Task PushForcePushBackAsync(string name, BackupRefInfo backup)
    {
        var tracking = Refs?.Refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.LocalBranch
            && string.Equals(gitRef.UpstreamRef, RemotesPrefix + name, StringComparison.Ordinal)
            && gitRef is { UpstreamRemote: not null, UpstreamRemoteRef: not null });
        string? remote;
        string? remoteRef;
        if (tracking is { UpstreamRemote: { } upstreamRemote, UpstreamRemoteRef: { } upstreamRef })
        {
            (remote, remoteRef) = (upstreamRemote, upstreamRef);
        }
        else
        {
            remote = RemoteNames.Where(candidate => name.StartsWith(candidate + "/", StringComparison.Ordinal)).MaxBy(candidate => candidate.Length);
            remoteRef = remote is null ? null : HeadsPrefix + name[(remote.Length + 1)..];
        }

        var branch = remoteRef is null ? name : ShortName(remoteRef);
        var text = OperationTexts.PushBack(branch, remote ?? name);
        if (remote is null || remoteRef is null)
        {
            return GuardAsync(text, () => ShowRefusalAsync(text, OperationTexts.RemoteOfBackupNotFound));
        }

        return GuardAsync(text, async () =>
        {
            var lease = ForcePushLease(await Git.ReadReflogAsync(RemotesPrefix + name, ForcePushReflogEntries), backup.Commit);
            if (lease is null)
            {
                await ShowRefusalAsync(text, OperationTexts.ForcePushCommitNotFound);
                return;
            }

            await PushBackAsync(text, remote, remoteRef, branch, isTag: false, lease, backup);
        });
    }

    /// <summary>7.5's question for a push back, then the push with its lease (<paramref name="lease"/>: what the remote must hold; null: nothing), recorded as a push.</summary>
    private Task PushBackAsync(OperationText text, string remote, string remoteRef, string name, bool isTag, string? lease, BackupRefInfo backup) => GuardAsync(text, async () =>
    {
        if (IsClosed)
        {
            return;
        }

        // Design 5.2: no question about a commit that is gone; the step checks it again.
        if (!await Git.CommitExistsAsync(backup.Target))
        {
            await ShowRefusalAsync(text, CommitsGoneSummary(text));
            return;
        }

        if (!await ConfirmAsync(OperationTexts.PushBackQuestion(name, remote, isTag, OperationTexts.Short(backup.Commit))))
        {
            return;
        }

        await RunAsync(
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                var entryText = isTag ? UndoTexts.PushTagBack(name, remote) : UndoTexts.PushBack(name, remote);
                var refusal = await RunEffectsAsync(context, isTag ? UndoKind.PushTag : UndoKind.Push, entryText, [new UndoEffect.RemoteRef(remote, remoteRef, lease, backup.Target)]);
                if (refusal is not null)
                {
                    return await RestoreRefusedAsync(context, text, refusal);
                }

                UndoCapture.Push(remote, new PushOutcome([new PushedRef(remoteRef, lease, backup.Target)]));
                return OperationReport.Done(OperationTexts.PushedBack(name, remote));
            },
            new RunOptions { Undo = isTag ? UndoScope.PushTagBack(name, remote) : UndoScope.PushBack(name, remote) });
    });

    /// <summary>
    /// The commit a force push left on the remote (7.10), its backup's lease: in the remote-tracking
    /// ref's reflog (newest first), the <c>update by push</c> entry whose older neighbour is the
    /// commit the push replaced. Null when the reflog no longer has it.
    /// </summary>
    public static string? ForcePushLease(ReflogResult remoteReflog, string backupCommit)
    {
        ArgumentNullException.ThrowIfNull(remoteReflog);
        ArgumentNullException.ThrowIfNull(backupCommit);
        var entries = remoteReflog.Entries;
        for (var index = 0; index + 1 < entries.Count; index++)
        {
            if (string.Equals(entries[index].Message, "update by push", StringComparison.Ordinal)
                && string.Equals(entries[index + 1].Sha, backupCommit, StringComparison.Ordinal))
            {
                return entries[index].Sha;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs a restore's effects as a step of their own inside its operation (7.10): the git layer's
    /// checks, then the effects in their order, put back when one refuses; the ref writes say
    /// <c>VisualCommit: <paramref name="entryText"/></c> (BD17). Returns the refusal, or null.
    /// </summary>
    private async Task<UndoRefusal?> RunEffectsAsync(OperationContext context, UndoKind kind, string entryText, IReadOnlyList<UndoEffect> effects)
    {
        var state = new UndoState { Head = Refs?.Head ?? new HeadState(null, null) };
        var entry = new UndoEntry(kind, entryText, context.OperationId, state, state, UndoCaptured.None);
        var step = new UndoStep(entry, UndoDirection.Redo, effects) { Message = $"VisualCommit: {entryText}" };
        return (await Git.RunUndoStepAsync(step, NetworkOptions.From(context), context.CancellationToken)).Refusal;
    }

    /// <summary>A refused restore: <c>Can't restore branch doomed: a branch named doomed exists again</c>, or a lease's 7.5 summary.</summary>
    private async Task<OperationReport> RestoreRefusedAsync(OperationContext context, OperationText text, UndoRefusal refusal) =>
        Refused(context, text, refusal, OperationTexts.CantRestore(text, await ReasonOfAsync(refusal)));

    /// <summary>7.6's "the commits no longer exist" after <c>Can't restore …: </c>, as the step's own check words it, for a restore refused before its step.</summary>
    private string CommitsGoneSummary(OperationText text) =>
        OperationTexts.CantRestore(text, OperationTexts.RefusalReason(new UndoRefusal(UndoRefusalKind.CommitsGone), Repository.Path));

    /// <summary>A restore refused before its operation: the error dialog with <paramref name="reason"/> alone.</summary>
    private Task ShowRefusalAsync(OperationText text, string reason)
    {
        Log.Info($"{Repository.Name}: {text.Title}: refused, {reason}");
        return ShowFailureAsync(new FailureDisplay(text.ErrorTitle, reason), new GitFailure(GitFailureKind.Other, reason, string.Empty, string.Empty, null));
    }

    /// <summary>
    /// A snapshot commit as the restores and the undo of a hard reset need it (D67): its paths from
    /// its message's body (<paramref name="body"/>), one per line, and those its tree holds.
    /// </summary>
    private async Task<DiscardSnapshot> ReadSnapshotAsync(string refName, string commit, string body)
    {
        var paths = body.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();
        var tree = (await Git.ReadTreeFilesAsync(commit)).ToHashSet(StringComparer.Ordinal);
        return new DiscardSnapshot(refName, commit, paths, new HashSet<string>(paths.Where(tree.Contains), StringComparer.Ordinal));
    }

    /// <summary>The paths <paramref name="commit"/> changes against its first parent, both names of a rename (BD16's <c>diff-tree --no-renames</c> list).</summary>
    private async Task<IReadOnlyList<string>> PathsChangedByAsync(string commit) =>
        [.. (await Git.ReadCommitDetailsAsync(commit)).Files
            .SelectMany(file => file.OldPath is { } old ? new[] { file.Path, old } : [file.Path])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Of BD16's <paramref name="paths"/>, those whose version in <paramref name="source"/> differs
    /// from <paramref name="head"/>'s, which the index holds while nothing is staged: the others
    /// need nothing put back, and a path that neither has (a staged deletion of a file the reset's
    /// target lacks too) makes <c>git restore --staged --source=&lt;s&gt; -- &lt;paths&gt;</c> fail
    /// as a whole ("did not match any file(s) known to git"). The restore's undo and redo name
    /// the same paths (<see cref="UndoCaptured.IndexPaths"/>), each in the index or in HEAD there.
    /// </summary>
    private async Task<IReadOnlyList<string>> DifferingFromHeadAsync(string source, string head, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return paths;
        }

        var differing = (await Git.CompareAsync(head, source, threeDot: false, countCommits: false)).Files
            .SelectMany(file => file.OldPath is { } old ? new[] { file.Path, old } : [file.Path])
            .ToHashSet(StringComparer.Ordinal);
        return [.. paths.Where(differing.Contains)];
    }

    /// <summary>
    /// The paths of <paramref name="snapshot"/> whose working-tree file differs both from HEAD's
    /// version (git's status lists it; a missing file counts as one) and from the snapshot's (BD15):
    /// a restore would overwrite work that is in neither.
    /// </summary>
    private async Task<IReadOnlyList<string>> ChangedSinceSnapshotAsync(DiscardSnapshot snapshot)
    {
        var status = await Git.ReadStatusAsync();
        var differ = status.Staged.Concat(status.Unstaged)
            .SelectMany(file => file.OldPath is { } old ? new[] { file.Path, old } : [file.Path])
            .ToHashSet(StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var path in snapshot.Paths.Where(differ.Contains))
        {
            var mine = await Git.ReadFileAsync(FileVersion.InWorkingTree(path), CompareBytes);
            var theirs = snapshot.Present.Contains(path) ? await Git.ReadFileAsync(FileVersion.InCommit(snapshot.Commit, path), CompareBytes) : null;
            var same = (mine, theirs) switch
            {
                (null, null) => !File.Exists(System.IO.Path.Combine(Git.WorkingDirectory, path)) && !snapshot.Present.Contains(path),
                ({ } a, { } b) => a.AsSpan().SequenceEqual(b),
                _ => false,
            };
            if (!same)
            {
                changed.Add(path);
            }
        }

        return changed;
    }

    /// <summary>What the recording read before an operation's work, for the read after it (7.7).</summary>
    /// <param name="Scope">What the operation declared.</param>
    /// <param name="Before">The state before its work; null when it completes or removes the pending entry.</param>
    /// <param name="OperationId">The operation the entry is recorded in.</param>
    /// <param name="Mode">What the read after it does.</param>
    /// <param name="WasInProgress">An operation was in progress when it began, as the app last read it: one that is after it did not stop there.</param>
    internal sealed record UndoRecording(UndoScope Scope, UndoState? Before, string OperationId, UndoRecordingMode Mode, bool WasInProgress);

    /// <summary>What the recording does after an operation's work (7.2, 7.7).</summary>
    internal enum UndoRecordingMode
    {
        /// <summary>Reads again and adds an entry when anything changed.</summary>
        Record,

        /// <summary>Continue, Skip or the stage panel's Commit: completes the pending entry when the stopped operation ended.</summary>
        CompletePending,

        /// <summary>Abort (D165): the pending entry goes when the operation ended.</summary>
        RemovePending,
    }

    /// <summary>The in-memory checks made for one direction, against the objects they were made with.</summary>
    private sealed record CheckedStep(UndoEntry Entry, RepoRefs Refs, WorkingTreeStatus Status, UndoRefusal? Refusal);
}
