using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>What a confirmation asks (D69).</summary>
/// <param name="Title">The card's title, such as "Discard changes?".</param>
/// <param name="Question">The question under it, such as "Discard the changes to Calculator.cs?".</param>
/// <param name="Detail">A second line in the secondary colour, or empty.</param>
/// <param name="ConfirmText">The label of the button that goes ahead, such as "Discard".</param>
/// <param name="IsDestructive">The button that goes ahead has the danger colour.</param>
public sealed record ConfirmationRequest(string Title, string Question, string Detail, string ConfirmText, bool IsDestructive = true)
{
    /// <summary>The repository to name under the title ("In &lt;name&gt;", 4f), or null for none.</summary>
    public string? RepositoryName { get; init; }
}

/// <summary>Asks the user to confirm, in a dialog drawn inside the main window (D69).</summary>
public interface IDialogService
{
    /// <summary>Shows the dialog and completes with true when the user goes ahead, false when they cancel.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);
}

/// <summary>One open confirmation: what it says and the two answers.</summary>
public sealed partial class ConfirmationViewModel : DialogViewModel<bool>
{
    internal ConfirmationViewModel(ConfirmationRequest request)
    {
        Request = request;
        RepositoryName = request.RepositoryName;
    }

    public ConfirmationRequest Request { get; }

    public override string Title => Request.Title;

    public string Question => Request.Question;

    public string Detail => Request.Detail;

    public bool HasDetail => Request.Detail.Length > 0;

    public string ConfirmText => Request.ConfirmText;

    public bool IsDestructive => Request.IsDestructive;

    internal Task<bool> Answer => Result;

    protected override bool CancelledResult => false;

    [RelayCommand]
    private void Confirm() => Complete(true);
}

/// <summary>
/// The window's dialog layer (D69), as a stack (D104): a dialog opened while another is open goes
/// on top of it, over a backdrop of its own; the one below stays drawn, takes no input, and gets
/// the focus back when the top one closes. Esc cancels the top dialog only. Views are found by
/// the dialog's type (<c>DialogViewLocator</c>).
/// </summary>
public sealed partial class DialogHostViewModel : ObservableObject, IDialogService
{
    /// <summary>The open dialogs, the bottom one first: what the dialog layer draws, in this order.</summary>
    public ObservableCollection<DialogViewModel> Open { get; } = [];

    /// <summary>The top dialog, which takes the input, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial DialogViewModel? Current { get; private set; }

    public bool IsOpen => Current is not null;

    /// <summary>
    /// Raised when a dialog has closed, just before it leaves <see cref="Open"/>: the window moves
    /// the keyboard focus away from it first (to the dialog below, D104).
    /// </summary>
    public event EventHandler<DialogViewModel>? Removing;

    public Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ShowAsync(new ConfirmationViewModel(request));
    }

    /// <summary>Shows <paramref name="dialog"/> on top of any open one and completes with its answer when it closes.</summary>
    public Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        Show(dialog);
        return dialog.Result;
    }

    /// <summary>Shows <paramref name="dialog"/> on top of any open one; completes when it closes.</summary>
    public Task ShowAsync(DialogViewModel dialog)
    {
        Show(dialog);
        return dialog.Closed;
    }

    /// <summary>
    /// Esc: the top dialog handles it (<see cref="DialogViewModel.HandleEscape"/>: it cancels, or
    /// goes back a step); the dialogs below see nothing of it. Returns whether one was open.
    /// </summary>
    public bool CancelCurrent()
    {
        if (Current is not { } dialog)
        {
            return false;
        }

        dialog.HandleEscape();
        return true;
    }

    private void Show(DialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (dialog.IsClosed || Open.Contains(dialog))
        {
            return;
        }

        dialog.Closing += OnClosing;
        Open.Add(dialog);
        Current = dialog;
    }

    private void OnClosing(object? sender, EventArgs e)
    {
        if (sender is not DialogViewModel dialog)
        {
            return;
        }

        dialog.Closing -= OnClosing;
        Removing?.Invoke(this, dialog);
        Open.Remove(dialog);
        Current = Open.Count > 0 ? Open[^1] : null;
    }
}
