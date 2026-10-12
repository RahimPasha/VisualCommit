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
/// last result, which stays until the next operation starts. A background operation (auto-fetch)
/// shows nothing unless it fails; a stage-panel operation (phase 2's writes) leaves "Ready".
/// While ssh shows a notice (3.2: "Confirm user presence for key …", a prompt that gets no
/// dialog) the text is the notice's, whatever the operations do; the progress bar, Cancel and
/// the waiting count go on as before. It follows the queue's events on the synchronization
/// context it was created on.
/// </summary>
public sealed partial class OperationStatusViewModel : ObservableObject, IDisposable
{
    /// <summary>What an idle repository, or a tab without one, shows.</summary>
    public const string ReadyText = "Ready";

    public const string WaitingForAutoFetchText = "Waiting for auto-fetch…";

    public const string AutoFetchFailedText = "Auto-fetch failed";

    private readonly IOperationQueue? _queue;
    private readonly PromptNotices? _notices;
    private readonly SynchronizationContext? _ui;
    private string _resultText = ReadyText;
    private OperationStatusTone _resultTone = OperationStatusTone.Secondary;
    private string? _resultToolTip;
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

    /// <summary>Cancel is offered: a network operation before its cancel window ended.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool CanCancel { get; private set; }

    /// <summary>How many operations wait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWaiting), nameof(WaitingText))]
    public partial int Waiting { get; private set; }

    public bool HasWaiting => Waiting > 0;

    /// <summary>"1 waiting".</summary>
    public string WaitingText => string.Create(CultureInfo.InvariantCulture, $"{Waiting} waiting");

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
            ShowResult(operation, result);
            Update();
        });
    }

    private void ShowResult(GitOperation operation, OperationResult result)
    {
        if (operation.IsBackground)
        {
            // A background operation changes nothing unless it fails (4e).
            if (result.Outcome == OperationOutcome.Failed)
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
            Show(ReadyText, OperationStatusTone.Secondary, null, running: false, percent: null);
            return;
        }

        var current = _queue.Current;
        Waiting = _queue.Waiting;
        CanCancel = current?.CanCancel ?? false;
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
            if (current.Progress is { } progress)
            {
                text += " " + progress.Stage;
                if (progress.Percent is { } percent)
                {
                    text += string.Create(CultureInfo.InvariantCulture, $" {percent}%");
                }
            }

            Show(text, OperationStatusTone.Secondary, null, running: true, percent: current.Progress?.Percent);
            return;
        }

        if (current is { IsBackground: true } && Waiting > 0)
        {
            Show(WaitingForAutoFetchText, OperationStatusTone.Secondary, null, running: false, percent: null);
            return;
        }

        Show(_resultText, _resultTone, _resultToolTip, running: false, percent: null);
    }

    private void Show(string text, OperationStatusTone tone, string? toolTip, bool running, int? percent)
    {
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
