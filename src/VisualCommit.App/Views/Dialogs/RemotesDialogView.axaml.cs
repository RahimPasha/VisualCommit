using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The Remotes dialog (<c>RemotesDialog</c>, docs/phase-3-design.md 2.23, 4f). In the list "Close"
/// has the focus; when the card switches to the form, the name box takes it, and back in the list
/// "Close" again.
/// </summary>
public partial class RemotesDialogView : DialogView
{
    private RemotesDialogViewModel? _dialog;

    public RemotesDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as RemotesDialogViewModel);
        DetachedFromVisualTree += (_, _) => Follow(null);
    }

    protected override Control? InitialFocus => CancelButton;

    protected override TextBox? InitialTextBox => _dialog is { IsFormShown: true } ? RemoteNameBox : null;

    private void Follow(RemotesDialogViewModel? dialog)
    {
        if (_dialog is not null)
        {
            _dialog.PropertyChanged -= OnDialogChanged;
        }

        _dialog = dialog;
        if (dialog is not null)
        {
            dialog.PropertyChanged += OnDialogChanged;
        }
    }

    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RemotesDialogViewModel.IsFormShown))
        {
            Dispatcher.UIThread.Post(FocusOnOpening, DispatcherPriority.Loaded);
        }
    }
}
