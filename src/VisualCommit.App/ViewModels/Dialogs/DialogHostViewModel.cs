using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>What a confirmation asks (D69).</summary>
/// <param name="Title">The card's title, such as "Discard changes?".</param>
/// <param name="Question">The question under it, such as "Discard the changes to Calculator.cs?".</param>
/// <param name="Detail">A second line in the secondary colour, or empty.</param>
/// <param name="ConfirmText">The label of the button that goes ahead, such as "Discard".</param>
/// <param name="IsDestructive">The button that goes ahead has the danger colour.</param>
public sealed record ConfirmationRequest(string Title, string Question, string Detail, string ConfirmText, bool IsDestructive = true);

/// <summary>Asks the user to confirm, in a dialog drawn inside the main window (D69).</summary>
public interface IDialogService
{
    /// <summary>Shows the dialog and completes with true when the user goes ahead, false when they cancel.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);
}

/// <summary>One open confirmation: what it says and the two answers.</summary>
public sealed partial class ConfirmationViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal ConfirmationViewModel(ConfirmationRequest request) => Request = request;

    public ConfirmationRequest Request { get; }

    public string Title => Request.Title;

    public string Question => Request.Question;

    public string Detail => Request.Detail;

    public bool HasDetail => Request.Detail.Length > 0;

    public string ConfirmText => Request.ConfirmText;

    public bool IsDestructive => Request.IsDestructive;

    internal Task<bool> Answer => _answer.Task;

    [RelayCommand]
    private void Confirm() => _answer.TrySetResult(true);

    [RelayCommand]
    private void Cancel() => _answer.TrySetResult(false);
}

/// <summary>
/// The window's dialog layer (D69): at most one confirmation at a time, shown over a dimmed
/// backdrop. A second request while one is open waits for the first to be answered.
/// </summary>
public sealed partial class DialogHostViewModel : ObservableObject, IDialogService
{
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>The confirmation on screen, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial ConfirmationViewModel? Current { get; private set; }

    public bool IsOpen => Current is not null;

    public async Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _one.WaitAsync();
        try
        {
            var dialog = new ConfirmationViewModel(request);
            Current = dialog;
            return await dialog.Answer;
        }
        finally
        {
            Current = null;
            _one.Release();
        }
    }

    /// <summary>Esc: cancels the open confirmation. Returns whether one was open.</summary>
    public bool CancelCurrent()
    {
        if (Current is not { } dialog)
        {
            return false;
        }

        dialog.CancelCommand.Execute(null);
        return true;
    }
}
