using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>The colour of the status bar's operation text (docs/phase-3-design.md 4e).</summary>
public enum OperationStatusTone
{
    /// <summary><c>VcTextSecondaryBrush</c>: ready, running, succeeded, cancelled, auto-fetch failed.</summary>
    Secondary,

    /// <summary><c>VcWarningBrush</c>: stopped with conflicts.</summary>
    Warning,

    /// <summary><c>VcDangerBrush</c>: failed.</summary>
    Danger,
}

/// <summary>
/// What the status bar shows of one repository's operations (docs/phase-3-design.md 1.9, 4e,
/// D95): the running operation's text and progress, Cancel, how many wait, and afterwards the
/// last result, which stays until the next operation starts. A background operation shows
/// nothing: auto-fetch's own says "Auto-fetch failed" when it fails and "Waiting for auto-fetch…"
/// while an operation of the user waits for it, any other (phase 4's clean-up of the backups,
/// docs/phase-4-design.md section 9) leaves the text as it was; a stage-panel operation (phase
/// 2's writes) leaves "Ready".
/// While ssh shows a notice (3.2: "Confirm user presence for key …", a prompt that gets no
/// dialog) the text is the notice's, whatever the operations do; the progress bar, Cancel and
/// the waiting count go on as before. It follows the queue's events on the synchronization
/// context it was created on.
/// </summary>
public sealed partial class OperationStatusViewModel : ObservableObject, IStatusBarOperation, IDisposable
{
    /// <summary>What an idle repository, or a tab without one, shows.</summary>
    public const string ReadyText = "Ready";

    public const string WaitingForAutoFetchText = "Waiting for auto-fetch…";

    public const string AutoFetchFailedText = "Auto-fetch failed";

    /// <summary>Cancel's tooltip while a network operation runs (1.9).</summary>
    public const string StopGitText = "Stop git";

    /// <summary>Cancel's tooltip while the merge tool runs (docs/phase-4-design.md 11.9): Cancel stops git, its shell and the tool.</summary>
    public const string StopGitAndMergeToolText = "Stop git and the merge tool";

    private readonly IOperationQueue? _queue;
    private readonly PromptNotices? _notices;
    private readonly SynchronizationContext? _ui;
    private string _resultText = ReadyText;
    private OperationStatusTone _resultTone = OperationStatusTone.Secondary;
    private string? _resultToolTip;
    private bool _mergeToolRuns;
    private bool _disposed;

    /// <param name="queue">The repository's queue; null for a tab without a repository, which shows "Ready" (or a notice) for good.</param>
    /// <param name="notices">ssh's notices of the window (<c>DialogHostViewModel.Notices</c>), shown in place of the text while there is one; null for none.</param>
    public OperationStatusViewModel(IOperationQueue? queue, PromptNotices? notices = null)
    {
        _queue = queue;
        _notices = notices;
        _ui = SynchronizationContext.Current;
        if (queue is not null)
        {
            queue.Changed += OnQueueChanged;
            queue.Finished += OnQueueFinished;
        }

        if (notices is not null)
        {
            notices.PropertyChanged += OnNoticesChanged;
        }

        Update();
    }

    /// <summary>The text: "Ready", the running text with its progress, "Waiting for auto-fetch…", or the last result.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = ReadyText;

    [ObservableProperty]
    public partial OperationStatusTone Tone { get; private set; }

    /// <summary>The text's tooltip: the whole message, or git's first error line after a failed auto-fetch; null for none.</summary>
    [ObservableProperty]
    public partial string? ToolTip { get; private set; }

    /// <summary>An operation the user started runs: the progress bar is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    public partial bool IsRunning { get; private set; }

    /// <summary>The running operation's percentage, or null before git gave one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    public partial int? Percent { get; private set; }

    /// <summary>The progress bar has no percentage to show.</summary>
    public bool IsIndeterminate => IsRunning && Percent is null;

    /// <summary>Cancel is offered: a network operation before its cancel window ended, or the merge tool.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool CanCancel { get; private set; }

    /// <summary>
    /// Cancel's tooltip: <see cref="StopGitAndMergeToolText"/> while a local operation that can be
    /// cancelled runs, which is the merge tool's alone (<see cref="GitOperation.CanBeCancelled"/>,
    /// D149); otherwise <see cref="StopGitText"/>.
    /// </summary>
    [ObservableProperty]
    public partial string CancelToolTip { get; private set; } = StopGitText;

    /// <summary>How many operations wait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWaiting), nameof(WaitingText))]
    public partial int Waiting { get; private set; }

    public bool HasWaiting => Waiting > 0;

    /// <summary>"1 waiting".</summary>
    public string WaitingText => string.Create(CultureInfo.InvariantCulture, $"{Waiting} waiting");

    /// <summary>
    /// A dashboard row's Operation cell (docs/phase-6-design.md 2.6): the running text with git's
    /// percentage but without its stage (<c>Fetching from all remotes… 45%</c>), the last result, or
    /// <c>Waiting for auto-fetch…</c>; empty for "Ready". Never ssh's window-wide notice, which would
    /// show in every row at once; the status bar keeps it.
    /// </summary>
    [ObservableProperty]
    public partial string CompactText { get; private set; } = string.Empty;

    /// <summary><see cref="CompactText"/>'s colour: the status bar's tone of the same text.</summary>
    [ObservableProperty]
    public partial OperationStatusTone CompactTone { get; private set; }

    /// <summary>
    /// <see cref="CompactText"/>'s tooltip (2.6): the status bar's whole text, its stage included
    /// (<c>Fetching from all remotes… Counting objects 45%</c>), or its tooltip where it has one of
    /// its own (git's first line after a failed auto-fetch); null while the text is empty.
    /// </summary>
    [ObservableProperty]
    public partial string? CompactToolTip { get; private set; }

    /// <summary>The title of the operation of the user that runs (a row's <c>Cancel {title}</c>, 2.7); null while none runs.</summary>
    [ObservableProperty]
    public partial string? RunningTitle { get; private set; }

    /// <summary>
    /// The operation of the user that runs could be cancelled (2.7): a network operation, or a local
    /// one that can be (<see cref="GitOperation.CanBeCancelled"/>, the merge tool), whether or not its
    /// cancel window is still open; <see cref="CanCancel"/> says whether it can be now. False while
    /// none runs, and for every other local operation, which never could be.
    /// </summary>
    [ObservableProperty]
    public partial bool RunningIsCancellable { get; private set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_queue is not null)
        {
            _queue.Changed -= OnQueueChanged;
            _queue.Finished -= OnQueueFinished;
        }

        if (_notices is not null)
        {
            _notices.PropertyChanged -= OnNoticesChanged;
        }
    }

    /// <summary>Stops git: cancels the running network operation (1.9).</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _queue?.CancelCurrent();

    System.Windows.Input.ICommand IStatusBarOperation.CancelCommand => CancelCommand;

    private void OnQueueChanged(object? sender, EventArgs e) => OnUiThread(Update);

    private void OnNoticesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(PromptNotices.Text))
        {
            OnUiThread(Update);
        }
    }

    private void OnQueueFinished(object? sender, OperationFinishedEventArgs e)
    {
        var (operation, result) = (e.Operation, e.Result);
        OnUiThread(() =>
        {
            // The merge tool's run is over: the next local operation is judged afresh. An operation
            // the queue cancelled before it started (docs/phase-6-design.md 4.5) ends while the
            // merge tool still runs, the queue's current one as shown, which keeps its judgement
            // (also once Cancel was asked, when it can no longer be cancelled).
            var current = _queue?.Current;
            var toolStillRuns = _mergeToolRuns
                && current is { Kind: OperationKind.Local, IsBackground: false }
                && string.Equals(current.Title, RunningTitle, StringComparison.Ordinal);
            if (!operation.IsBackground && !toolStillRuns)
            {
                _mergeToolRuns = false;
            }

            ShowResult(operation, result);
            Update();
        });
    }

    private void ShowResult(GitOperation operation, OperationResult result)
    {
        if (operation.IsBackground)
        {
            // A background operation changes nothing unless auto-fetch fails (4e); another one
            // (the backups' clean-up, phase 4 section 9) changes nothing at all.
            if (result.Outcome == OperationOutcome.Failed && IsAutoFetch(operation.Title))
            {
                (_resultText, _resultTone) = (AutoFetchFailedText, OperationStatusTone.Secondary);
                _resultToolTip = result.Failure?.GitOutput.Split('\n')[0] is { Length: > 0 } line ? line : null;
            }

            return;
        }

        if (operation.Errors == ErrorDisplay.StagePanel)
        {
            // The stage panel shows its own errors; the status bar says nothing of them (1.4, 1.9).
            (_resultText, _resultTone, _resultToolTip) = (ReadyText, OperationStatusTone.Secondary, null);
            return;
        }

        _resultTone = result.Outcome switch
        {
            OperationOutcome.Conflicts => OperationStatusTone.Warning,
            OperationOutcome.Succeeded when result.IsWarning => OperationStatusTone.Warning,
            OperationOutcome.Failed => OperationStatusTone.Danger,
            _ => OperationStatusTone.Secondary,
        };
        _resultText = result.Message.Length > 0 ? result.Message : ReadyText;
        _resultToolTip = result.Message.Length > 0 ? result.Message : null;
    }

    private void Update()
    {
        if (_disposed)
        {
            return;
        }

        if (_queue is null)
        {
            ShowRunning(null);
            Show(ReadyText, OperationStatusTone.Secondary, null, running: false, percent: null);
            return;
        }

        var current = _queue.Current;
        Waiting = _queue.Waiting;
        CanCancel = current?.CanCancel ?? false;
        CancelToolTip = current is { CanCancel: true, Kind: OperationKind.Local } ? StopGitAndMergeToolText : StopGitText;
        ShowRunning(current);
        if (current is { IsBackground: false })
        {
            // The message of the last operation goes as soon as the next one starts.
            (_resultText, _resultTone, _resultToolTip) = (ReadyText, OperationStatusTone.Secondary, null);
            if (current.Errors == ErrorDisplay.StagePanel)
            {
                Show(ReadyText, OperationStatusTone.Secondary, null, running: false, percent: null);
                return;
            }

            var text = current.RunningText;
            var compact = current.RunningText;
            if (current.Progress is { } progress)
            {
                text += " " + progress.Stage;
                if (progress.Percent is { } percent)
                {
                    var shown = string.Create(CultureInfo.InvariantCulture, $" {percent}%");
                    text += shown;
                    compact += shown;
                }
            }

            Show(text, OperationStatusTone.Secondary, null, running: true, percent: current.Progress?.Percent, compact);
            return;
        }

        if (current is { IsBackground: true } && Waiting > 0 && IsAutoFetch(current.Title))
        {
            Show(WaitingForAutoFetchText, OperationStatusTone.Secondary, null, running: false, percent: null);
            return;
        }

        Show(_resultText, _resultTone, _resultToolTip, running: false, percent: null);
    }

    /// <summary>
    /// <see cref="RunningTitle"/> and <see cref="RunningIsCancellable"/> of the queue's running
    /// operation (2.7). A local operation can be cancelled only while the merge tool runs, whose
    /// cancel window never ends (D149), so one seen cancellable stays so until it finishes, also
    /// once Cancel was asked for.
    /// </summary>
    private void ShowRunning(OperationSnapshot? current)
    {
        if (current is not { IsBackground: false })
        {
            RunningTitle = null;
            RunningIsCancellable = false;
            return;
        }

        if (current is { Kind: OperationKind.Local, CanCancel: true })
        {
            _mergeToolRuns = true;
        }

        RunningTitle = current.Title;
        RunningIsCancellable = current.Kind == OperationKind.Network || _mergeToolRuns;
    }

    private void Show(string text, OperationStatusTone tone, string? toolTip, bool running, int? percent, string? compactText = null)
    {
        // A row's compact text (2.6): the text as built, without git's stage and never ssh's
        // notice; empty for "Ready". "Waiting for auto-fetch…" has no tooltip of its own there.
        var ready = compactText is null && string.Equals(text, ReadyText, StringComparison.Ordinal);
        var waiting = string.Equals(text, WaitingForAutoFetchText, StringComparison.Ordinal);
        CompactText = ready ? string.Empty : compactText ?? text;
        CompactTone = ready ? OperationStatusTone.Secondary : tone;
        CompactToolTip = ready || waiting ? null : toolTip ?? text;

        // ssh's notice (3.2) waits for the user, so it is what the text says until its scope ends.
        if (_notices?.Text is { } notice)
        {
            (text, tone, toolTip) = (notice, OperationStatusTone.Secondary, notice);
        }

        Text = text;
        Tone = tone;
        ToolTip = toolTip;
        IsRunning = running;
        Percent = percent;
    }

    /// <summary>Auto-fetch's own operation, by its title (<see cref="AutoFetchScheduler.Title"/>): the only one the auto-fetch texts are about.</summary>
    private static bool IsAutoFetch(string title) => string.Equals(title, AutoFetchScheduler.Title, StringComparison.Ordinal);

    private void OnUiThread(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }
}
