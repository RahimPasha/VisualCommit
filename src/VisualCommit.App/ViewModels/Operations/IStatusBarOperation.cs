using System.ComponentModel;
using System.Windows.Input;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// What the status bar's operation part shows (docs/phase-3-design.md 4e; docs/phase-6-design.md
/// 4.9, D208): the static type of <see cref="RepoTabViewModel.OperationStatus"/>, so that the
/// compiled bindings of <c>StatusBarView.axaml</c> keep their paths whether the active tab shows a
/// repository's queue (<see cref="OperationStatusViewModel"/>) or a dashboard's bulk run
/// (<c>DashboardBulkViewModel</c>). <see cref="Waiting"/> is not bound; phase 3's checks read it.
/// </summary>
public interface IStatusBarOperation : INotifyPropertyChanged
{
    /// <summary>The text: "Ready", the running text with its progress, or the last result.</summary>
    string Text { get; }

    /// <summary>The text's colour.</summary>
    OperationStatusTone Tone { get; }

    /// <summary>The text's tooltip, or null for none.</summary>
    string? ToolTip { get; }

    /// <summary>Something the user started runs: the progress bar is shown.</summary>
    bool IsRunning { get; }

    /// <summary>The progress bar has no percentage to show.</summary>
    bool IsIndeterminate { get; }

    /// <summary>The percentage, or null before there is one.</summary>
    int? Percent { get; }

    /// <summary>Cancel is offered.</summary>
    bool CanCancel { get; }

    /// <summary>What Cancel does.</summary>
    ICommand CancelCommand { get; }

    /// <summary>Cancel's tooltip.</summary>
    string CancelToolTip { get; }

    /// <summary>"1 waiting".</summary>
    string WaitingText { get; }

    /// <summary>Something waits: <see cref="WaitingText"/> is shown.</summary>
    bool HasWaiting { get; }

    /// <summary>How many wait.</summary>
    int Waiting { get; }

    /// <summary>The offer's text and name beside a success (D317, docs/phase-7a-design.md 12.2), "Open in browser"; null without one.</summary>
    string? OfferText { get; }

    /// <summary>The offer's button is shown: the text is a succeeded operation's result that carries an offer.</summary>
    bool HasOffer { get; }

    /// <summary>What the offer's button runs.</summary>
    ICommand OfferCommand { get; }
}
