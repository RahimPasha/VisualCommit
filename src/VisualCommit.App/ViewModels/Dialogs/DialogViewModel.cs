using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// One dialog of the window's dialog layer (D69, D104; docs/phase-3-design.md 4f): its title,
/// its Cancel and when it has closed. The layer finds the view for a dialog by its type
/// (<c>DialogViewLocator</c>). Every dialog has a <c>CancelButton</c> bound to
/// <see cref="CancelCommand"/>; Esc cancels the top dialog the same way.
/// </summary>
public abstract partial class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The card's title.</summary>
    public abstract string Title { get; }

    /// <summary>Completes when the dialog has closed, by any button, Esc or <see cref="Close"/>.</summary>
    public Task Closed => _closed.Task;

    /// <summary>The dialog has closed.</summary>
    public bool IsClosed => _closed.Task.IsCompleted;

    /// <summary>Raised once, on the thread that closed the dialog, when it closes. The dialog host removes it from the layer then.</summary>
    internal event EventHandler? Closing;

    /// <summary>Closes the dialog as Cancel does: what the user did not confirm does not happen.</summary>
    [RelayCommand]
    public void Cancel()
    {
        if (!IsClosed)
        {
            OnCancel();
            Close();
        }
    }

    /// <summary>Closes the dialog. Closing it a second time does nothing.</summary>
    protected void Close()
    {
        if (_closed.TrySetResult())
        {
            Closing?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>What Cancel and Esc do before the dialog closes: a dialog with a result sets its cancelled result.</summary>
    protected virtual void OnCancel()
    {
    }
}

/// <summary>A dialog that ends with an answer, such as the confirmation's yes or no.</summary>
/// <typeparam name="TResult">The answer's type.</typeparam>
public abstract class DialogViewModel<TResult> : DialogViewModel
{
    private readonly TaskCompletionSource<TResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The answer; set when the dialog closes, Cancel and Esc giving <see cref="CancelledResult"/>.</summary>
    public Task<TResult> Result => _result.Task;

    /// <summary>What Cancel and Esc answer.</summary>
    protected abstract TResult CancelledResult { get; }

    /// <summary>Answers and closes the dialog.</summary>
    protected void Complete(TResult result)
    {
        if (_result.TrySetResult(result))
        {
            Close();
        }
    }

    protected override void OnCancel() => _result.TrySetResult(CancelledResult);
}
