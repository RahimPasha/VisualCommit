using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>The confirmation dialog (D69). When it opens, its Cancel button takes the keyboard focus.</summary>
public partial class ConfirmationView : UserControl
{
    public ConfirmationView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not null)
            {
                // After the layout pass that makes the dialog visible: a hidden button cannot take the focus.
                Dispatcher.UIThread.Post(() => CancelButton.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
            }
        };
    }
}
