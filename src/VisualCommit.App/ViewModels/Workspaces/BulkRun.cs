using System.ComponentModel;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>Fetch all or Pull all (docs/phase-6-design.md 4.3).</summary>
public enum BulkKind
{
    /// <summary>Fetch all: the toolbar's Fetch of every remote in each row.</summary>
    Fetch,

    /// <summary>Pull all: the toolbar's Pull in each row.</summary>
    Pull,
}

/// <summary>Where a row of a bulk run is (docs/phase-6-design.md 4.3).</summary>
public enum BulkRowState
{
    /// <summary>The run waits for the row's read: its first one, or one the run started at its turn.</summary>
    Reading,

    /// <summary>Left out, with the toolbar's reason or for its State (no repository behind it).</summary>
    Skipped,

    /// <summary>Asked for a slot, waiting for its turn.</summary>
    InLine,

    /// <summary>Its operation was called with a slot and has not returned.</summary>
    Running,

    /// <summary>Its operation returned: <see cref="BulkRunRow.Result"/>.</summary>
    Ended,

    /// <summary>Stopped before its turn (<see cref="BulkRun.StopStarting"/>): "Fetch cancelled".</summary>
    CancelledInLine,
}

/// <summary>One row of a bulk run (docs/phase-6-design.md 4.3): its dashboard row and what became of it.</summary>
public sealed class BulkRunRow
{
    internal BulkRunRow(DashboardRowViewModel row) => Row = row;

    /// <summary>The dashboard's row; its view model is looked up at its turn, never kept from the run's start.</summary>
    public DashboardRowViewModel Row { get; }

    public BulkRowState State { get; internal set; }

    /// <summary>Why it was left out (the toolbar's reason), or null.</summary>
    public string? Reason { get; internal set; }

    /// <summary>Its operation's result, once it ended.</summary>
    public OperationResult? Result { get; internal set; }

    /// <summary>The dialog its operation would have opened (4.6, D195), kept for its Details….</summary>
    public DeferredDialog? KeptDialog { get; internal set; }

    /// <summary>The row is done: left out, ended, or stopped in line.</summary>
    public bool IsDone => State is BulkRowState.Skipped or BulkRowState.Ended or BulkRowState.CancelledInLine;

    /// <summary>The row's slot request while it waits for its turn or runs.</summary>
    internal BulkSlotRequest? Request { get; set; }
}

/// <summary>
/// One click of Fetch all or Pull all on one dashboard (docs/phase-6-design.md 4.2 to 4.6, 4.9,
/// D195, D210, D211): every row decided at the start, in row order, by the toolbar's rule
/// (<see cref="ToolbarViewModel.StatesFor"/>) from its last read, a row still being read once its
/// read ends; each row in line asks the window's <see cref="BulkSlots"/> for a slot and, at its
/// turn, is decided again from its current view model and state (one without a view model, or a
/// closed one, gives its slot back and is read again first); with its slot it calls the toolbar's
/// operation with the run's <see cref="BulkPart"/> (its cancellation, prompt group and the row's
/// deferral), and gives the slot back when the call returns. Lives on the UI thread.
/// <para>
/// <see cref="StopStarting"/> starts no more rows: rows in line or still being read end
/// <see cref="BulkRowState.CancelledInLine"/>. <see cref="CancelAll"/> does that and cancels the
/// run's own operations (<see cref="RunOptions.Cancellation"/>); <see cref="Abandon"/> (its
/// dashboard closed) does Cancel all and lets a result that comes afterwards open its dialog in the
/// view model that ran it (4.6). At the end the run disposes its prompt group and its cancellation,
/// logs its result and raises <see cref="Changed"/>.
/// </para>
/// </summary>
public sealed class BulkRun
{
    private readonly BulkSlots _slots;
    private readonly OpenRepositories? _registry;
    private readonly IAppLog _log;
    private readonly string _workspace;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SynchronizationContext? _ui;
    private bool _started;

    /// <param name="kind">Fetch all or Pull all.</param>
    /// <param name="workspace">The workspace's name, for the log.</param>
    /// <param name="rows">The dashboard's rows, in their order.</param>
    /// <param name="slots">The window's slots.</param>
    /// <param name="registry">The registry, for the common folder's check of 4.3 (another queue fetching from it); none in tests that need none.</param>
    /// <param name="log">The app's log.</param>
    public BulkRun(BulkKind kind, string workspace, IReadOnlyList<DashboardRowViewModel> rows, BulkSlots slots, OpenRepositories? registry, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(log);
        Kind = kind;
        _workspace = workspace;
        _slots = slots;
        _registry = registry;
        _log = log;
        _ui = SynchronizationContext.Current;
        Rows = [.. rows.Select(row => new BulkRunRow(row))];
    }

    public BulkKind Kind { get; }

    /// <summary>The run's rows, in the dashboard's order at its start.</summary>
    public IReadOnlyList<BulkRunRow> Rows { get; }

    /// <summary>The run's prompts (4.4): one group from its start to its end.</summary>
    public PromptGroup PromptGroup { get; } = new();

    /// <summary>The run is under way: some row is not done.</summary>
    public bool IsRunning => _started && !_completion.Task.IsCompleted;

    /// <summary>Cancel all was clicked (4.5): the run waits for its running rows.</summary>
    public bool IsCancelling { get; private set; }

    /// <summary>Its dashboard closed (4.6): a result that comes now opens its dialog in the view model that ran it.</summary>
    public bool IsAbandoned { get; private set; }

    /// <summary>Completes when every row is done.</summary>
    public Task Completion => _completion.Task;

    /// <summary>The rows acted on (4.9's <c>{n}</c>): every row not left out.</summary>
    public int ActedOn => Rows.Count(row => row.State != BulkRowState.Skipped);

    /// <summary>The rows ended or stopped in line (4.9's <c>{k}</c>, WD13).</summary>
    public int Done => Rows.Count(row => row.State is BulkRowState.Ended or BulkRowState.CancelledInLine);

    /// <summary>The rows running now.</summary>
    public int RunningRows => Rows.Count(row => row.State == BulkRowState.Running);

    /// <summary>The rows in line (4.9's <c>{w} waiting</c>).</summary>
    public int InLine => Rows.Count(row => row.State == BulkRowState.InLine);

    public int Succeeded => Rows.Count(row => row is { State: BulkRowState.Ended, Result.Outcome: OperationOutcome.Succeeded });

    public int Failed => Rows.Count(row => row is { State: BulkRowState.Ended, Result.Outcome: OperationOutcome.Failed });

    /// <summary>Pull all's rows that stopped with conflicts.</summary>
    public int Stopped => Rows.Count(row => row is { State: BulkRowState.Ended, Result.Outcome: OperationOutcome.Conflicts });

    public int Cancelled => Rows.Count(row => row.State == BulkRowState.CancelledInLine || row is { State: BulkRowState.Ended, Result.Outcome: OperationOutcome.Cancelled });

    public int Skipped => Rows.Count(row => row.State == BulkRowState.Skipped);

    /// <summary>The result (4.9): "Fetched 9 repositories; 1 failed".</summary>
    public string ResultText => OperationTexts.BulkResult(Kind, Succeeded, Failed, Stopped, Cancelled, Skipped);

    /// <summary>The run's text (4.9): running, cancelling, or its result.</summary>
    public string Text =>
        !IsRunning ? ResultText
        : IsCancelling ? OperationTexts.BulkCancelling(RunningRows)
        : OperationTexts.BulkRunning(Kind, ActedOn, Done);

    /// <summary>The text's tone: danger when a row failed, else warning when one stopped (at the end); secondary otherwise.</summary>
    public OperationStatusTone Tone =>
        IsRunning ? OperationStatusTone.Secondary
        : Failed > 0 ? OperationStatusTone.Danger
        : Stopped > 0 ? OperationStatusTone.Warning
        : OperationStatusTone.Secondary;

    /// <summary>The determinate progress while it runs: <c>⌊100 × {k} / {n}⌋</c>.</summary>
    public int Percent => ActedOn == 0 ? 0 : 100 * Done / ActedOn;

    /// <summary>Raised on the UI thread when a row's state, the counts or the run's state change.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Starts the run: decides every row in row order (left out, still being read, or in line with
    /// its slot asked for) and logs its start. Call it once, on the UI thread.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("A bulk run starts once.");
        }

        _started = true;
        foreach (var row in Rows)
        {
            row.Row.ClearBulk();
        }

        // Every row is decided at the start, in row order, before any turn comes (4.2).
        var turns = new List<BulkRunRow>();
        foreach (var row in Rows)
        {
            switch (Decide(row.Row))
            {
                case Decision.Skip skip:
                    Skip(row, skip.Reason);
                    break;
                case Decision.Read:
                    Show(row, BulkRowState.Reading);
                    turns.Add(row);
                    break;
                default:
                    Show(row, BulkRowState.InLine);
                    turns.Add(row);
                    break;
            }
        }

        _log.Info(OperationTexts.BulkStartedLine(_workspace, Kind, ActedOn, Skipped));
        foreach (var row in turns)
        {
            _ = RunRowAsync(row);
        }

        RaiseChanged();
        EndIfDone();
    }

    /// <summary>
    /// Starts no more rows (4.5, 4.12): rows in line and still being read end "Fetch cancelled"
    /// (secondary) until the next run or their next operation, and their slot requests are
    /// withdrawn; running rows go on.
    /// </summary>
    public void StopStarting()
    {
        if (!IsRunning)
        {
            return;
        }

        var cancelled = OperationTexts.BulkRowCancelled(Kind);
        foreach (var row in Rows.Where(row => row.State is BulkRowState.InLine or BulkRowState.Reading))
        {
            row.State = BulkRowState.CancelledInLine;
            row.Request?.Withdraw();
            row.Request = null;
            row.Row.ShowBulk(new DashboardCellText(cancelled, DashboardTone.Secondary, cancelled), heldReason: null, clearsOnNextOperation: true);
        }

        // The waits for reads end too (a folder check that has not returned holds nothing, 2.10).
        _stop.Cancel();
        RaiseChanged();
        EndIfDone();
    }

    /// <summary>
    /// Cancel all (4.5): <see cref="StopStarting"/>, then every running row's operation is
    /// cancelled through the run's cancellation (a pull past its fetch finishes and counts by its
    /// outcome); the run says "Cancelling: waiting for {r} repositories…" until it ends.
    /// </summary>
    public void CancelAll()
    {
        if (!IsRunning)
        {
            return;
        }

        IsCancelling = true;
        StopStarting();
        if (IsRunning)
        {
            _cancellation.Cancel();
            RaiseChanged();
        }
    }

    /// <summary>
    /// Its dashboard closed or was disposed (4.5, 4.6, 4.12): Cancel all; a result that comes
    /// afterwards is kept nowhere and opens its dialog in the view model that ran it.
    /// </summary>
    public void Abandon()
    {
        IsAbandoned = true;
        CancelAll();
    }

    private async Task RunRowAsync(BulkRunRow row)
    {
        try
        {
            while (true)
            {
                if (row.State == BulkRowState.Reading && !await WaitForReadAsync(row))
                {
                    return;
                }

                if (row.State == BulkRowState.CancelledInLine)
                {
                    return;
                }

                // Decided from the read that ended (4.2), then in line for a slot.
                switch (Decide(row.Row))
                {
                    case Decision.Skip skip:
                        Skip(row, skip.Reason);
                        return;
                    case Decision.Read:
                        // A read ended and still nothing to decide by: nothing behind the row.
                        Skip(row, reason: null);
                        return;
                }

                if (!await TakeTurnAsync(row))
                {
                    return;
                }

                if (row.State != BulkRowState.Reading)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            // An operation reports its own failures; this is what slipped past the run. No status
            // bar says it, so the row does: "{Title} failed" in the danger tone, the exception's
            // message in its tooltip, until the row's next operation (4.6).
            _log.Error($"{_workspace}: {OperationTexts.BulkName(Kind)}: {row.Row.Name} failed.", ex);
            row.Request?.Release();
            row.Request = null;
            if (!row.IsDone)
            {
                var failed = $"{(Kind == BulkKind.Fetch ? OperationTexts.FetchAll.Title : "Pull")} failed";
                row.State = BulkRowState.Ended;
                row.Result = new OperationResult(OperationOutcome.Failed, failed, null);
                row.Row.ShowBulk(new DashboardCellText(failed, DashboardTone.Danger, $"{failed}: {ex.Message}"), heldReason: null, clearsOnNextOperation: true);
            }
        }
        finally
        {
            // The last row's end is the run's end: its result, never "waiting for 0".
            if (!EndIfDone())
            {
                RaiseChanged();
            }
        }
    }

    /// <summary>
    /// The row in line asks for its slot and, at its turn, is decided again from its current view
    /// model and state (4.3). False when the row is done; true with the row back in
    /// <see cref="BulkRowState.Reading"/> when it had no view model (or a closed one).
    /// </summary>
    private async Task<bool> TakeTurnAsync(BulkRunRow row)
    {
        if (row.Row.Repository is not { IsClosed: false } first)
        {
            Show(row, BulkRowState.Reading);
            return true;
        }

        Show(row, BulkRowState.InLine);
        var common = WorktreeKey.Of(first.Repository.CommonDirectory);
        var request = _slots.Request(common, _registry is null ? null : () => _registry.NetworkQueueOn(common, row.Row.Repository?.Queue));
        row.Request = request;
        RaiseChanged();
        if (!await request.Granted || row.State == BulkRowState.CancelledInLine)
        {
            request.Release();
            return false;
        }

        try
        {
            // The row's turn (4.3): its folder checked again (D207, D204: a folder that lies inside
            // another working tree now is never acted on as the outer one), then its current view
            // model decided, never the one of the run's start.
            if (row.Row.Repository is { IsClosed: false } current)
            {
                WorkspaceFolderProbe? found;
                try
                {
                    found = await row.Row.CheckAtTurnAsync(current, _stop.Token);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    // Stopped while the folder was checked: StopStarting ended the row in line.
                    return false;
                }

                if (row.State == BulkRowState.CancelledInLine)
                {
                    return false;
                }

                if (found is { State: not WorkspaceFolderState.Ready } && row.Row.FolderState is null)
                {
                    // A followed tab's folder: the tab keeps its view model, the row is left out
                    // with the folder state's reason.
                    Skip(row, found.ToolTip);
                    return false;
                }
            }

            switch (Decide(row.Row))
            {
                case Decision.Skip skip:
                    Skip(row, skip.Reason);
                    return false;
                case Decision.Read:
                    Show(row, BulkRowState.Reading);
                    return true;
            }

            var repository = row.Row.Repository!;
            Show(row, BulkRowState.Running);
            RaiseChanged();
            var part = new BulkPart(_cancellation.Token, PromptGroup, dialog => Keep(row, repository, dialog));
            var result = await (Kind == BulkKind.Fetch ? repository.Operations.FetchAllAsync(part) : repository.Operations.PullAsync(part));
            if (result is null)
            {
                // Nothing started: HEAD was not on a branch with an upstream after all. Left out
                // with the toolbar's reason, the row's copy's or else its view model's, if any.
                Skip(row, DecideReason(row.Row) ?? ReasonOf(repository));
                return false;
            }

            row.Result = result;
            row.State = BulkRowState.Ended;
            if (repository.Queue.Current is { IsBackground: false })
            {
                // The row's queue started its next operation (a tab's Push queued behind the row's
                // fetch) before the call returned: the kept dialog and the run's text go, as they
                // go when the next operation starts (D95, 4.6), and the row shows that operation.
                row.Row.ShowBulk(null, heldReason: null, clearsOnNextOperation: false);
                row.Row.ForgetKeptDialog();
                return false;
            }

            // The status shows the result, but not of an operation the queue cancelled before it
            // started (one the row's call merged into, D123): "Fetch cancelled" says so in the row;
            // nor a failure outside the queue (4.6): "Fetch failed" in the danger tone.
            var cancelled = OperationTexts.BulkRowCancelled(Kind);
            row.Row.ShowBulk(
                result.Outcome == OperationOutcome.Cancelled ? new DashboardCellText(cancelled, DashboardTone.Secondary, cancelled)
                    : RepositoryOperations.FailedOutsideTheQueue(result) ? new DashboardCellText(result.Message, DashboardTone.Danger, result.Message)
                    : null,
                heldReason: null,
                clearsOnNextOperation: true);
            return false;
        }
        finally
        {
            request.Release();
            if (ReferenceEquals(row.Request, request))
            {
                row.Request = null;
            }
        }
    }

    /// <summary>
    /// Waits for the row's read (4.2, 4.3): a first read running, a followed tab's first read, or
    /// the read the run starts itself for a row without a view model (2.10's trigger 5), also with
    /// the dashboard in the background. False when the run stopped starting rows meanwhile.
    /// </summary>
    private async Task<bool> WaitForReadAsync(BulkRunRow row)
    {
        var started = false;
        while (true)
        {
            if (_stop.IsCancellationRequested || row.State == BulkRowState.CancelledInLine)
            {
                return false;
            }

            var target = row.Row;
            if (Decide(target) is not Decision.Read)
            {
                return true;
            }

            // A read runs, a followed tab's first read has not ended, or the row's view model is
            // closed and the registry has not let it go yet (its tab closing, 3.6): its next
            // change says more.
            if (target.IsReading || target.Repository is { HasFirstRead: false } or { IsClosed: true })
            {
                await NextChangeAsync(target);
                continue;
            }

            if (started)
            {
                // The run's read ended and left nothing to decide by.
                return true;
            }

            started = true;
            if (target.StartRead() is { } read)
            {
                try
                {
                    await read.Completion.WaitAsync(_stop.Token);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>Completes at the row's next change, or when the run stops starting rows.</summary>
    private async Task NextChangeAsync(DashboardRowViewModel row)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, PropertyChangedEventArgs e) => changed.TrySetResult();
        row.PropertyChanged += OnChanged;
        try
        {
            await changed.Task.WaitAsync(_stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        finally
        {
            row.PropertyChanged -= OnChanged;
        }
    }

    /// <summary>
    /// 4.2's table from <paramref name="row"/>'s state now, for a run of <paramref name="kind"/>: a
    /// folder state or a failed read leaves it out with nothing new in its Operation cell; a row not
    /// read yet, or without a live view model, waits for a read; otherwise the toolbar's rule, its
    /// first reason in <c>Reasons</c>' order. The one rule: the dashboard's toolbar asks it too
    /// (<see cref="DashboardBulkViewModel"/>: a run would act on a row it does not leave out, 4.10).
    /// </summary>
    internal static Decision Decide(DashboardRowViewModel row, BulkKind kind)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.FolderState is not null || row.ErrorText is not null)
        {
            return new Decision.Skip(null);
        }

        if (!row.HasRepository || row.Refs is null || row.Status is null)
        {
            return Decision.Read.Instance;
        }

        return ReasonOf(row.Refs, row.Status, kind) is { } reason ? new Decision.Skip(reason) : Decision.Go.Instance;
    }

    private Decision Decide(DashboardRowViewModel row) => Decide(row, Kind);

    private string? DecideReason(DashboardRowViewModel row) => ReasonOf(row.Refs, row.Status, Kind);

    /// <summary>The toolbar's reason from <paramref name="repository"/>'s own refs and status, or null.</summary>
    private string? ReasonOf(RepositoryViewModel repository) => ReasonOf(repository.Refs, repository.Status, Kind);

    private static string? ReasonOf(RepoRefs? refs, WorkingTreeStatus? status, BulkKind kind)
    {
        if (refs is null || status is null)
        {
            return null;
        }

        var states = ToolbarViewModel.StatesFor(refs, status);
        return kind == BulkKind.Fetch ? states.Fetch.Reason : states.Pull.Reason;
    }

    private void Skip(BulkRunRow row, string? reason)
    {
        row.State = BulkRowState.Skipped;
        row.Reason = reason;
        row.Row.ShowBulk(
            reason is null ? null : new DashboardCellText(reason, DashboardTone.Secondary, reason),
            heldReason: null,
            clearsOnNextOperation: true);
    }

    /// <summary>A row reading, in line or running: "Waiting to fetch…" while it waits (none while it runs), and the row held (4.7).</summary>
    private void Show(BulkRunRow row, BulkRowState state)
    {
        row.State = state;
        var waiting = OperationTexts.WaitingFor(Kind);
        row.Row.ShowBulk(
            state == BulkRowState.Running ? null : new DashboardCellText(waiting, DashboardTone.Secondary, waiting),
            OperationTexts.WaitForBulkRun(Kind),
            clearsOnNextOperation: false);
    }

    /// <summary>
    /// A row's dialog, deferred (4.6): kept for its Details…, or, once the run is abandoned, opened
    /// at once in <paramref name="repository"/> (posted, so that the call returns and the slot goes
    /// back while the dialog is open).
    /// </summary>
    private void Keep(BulkRunRow row, RepositoryViewModel repository, DeferredDialog dialog)
    {
        if (IsAbandoned)
        {
            void Open() => _ = repository.Operations.ShowDeferredAsync(dialog);
            if (_ui is null)
            {
                Open();
            }
            else
            {
                _ui.Post(_ => Open(), null);
            }

            return;
        }

        row.KeptDialog = dialog;
        row.Row.Keep(dialog, repository);
    }

    /// <summary>Ends the run when every row is done (true, <see cref="Changed"/> raised); false while a row is not.</summary>
    private bool EndIfDone()
    {
        if (!_started || _completion.Task.IsCompleted || !Rows.All(row => row.IsDone))
        {
            return false;
        }

        foreach (var row in Rows)
        {
            row.Row.ShowBulkHeld(null);
        }

        // Every row's call has returned: no registration on the cancellation is left (4.3).
        PromptGroup.Dispose();
        _cancellation.Dispose();
        _log.Info(OperationTexts.BulkEndedLine(_workspace, Kind, ResultText));
        _completion.TrySetResult();
        RaiseChanged();
        return true;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>What 4.2's table decides for a row now.</summary>
    internal abstract record Decision
    {
        /// <summary>Left out, with the toolbar's reason, or none for a row without a repository.</summary>
        public sealed record Skip(string? Reason) : Decision;

        /// <summary>Its read must end first.</summary>
        public sealed record Read : Decision
        {
            public static Read Instance { get; } = new();
        }

        /// <summary>In line.</summary>
        public sealed record Go : Decision
        {
            public static Go Instance { get; } = new();
        }
    }
}
