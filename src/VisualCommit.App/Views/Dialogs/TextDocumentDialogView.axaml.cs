using System.ComponentModel;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The text dialog (<c>TextDocumentDialog</c>, D432): the shown text in <c>TextDocumentText</c>, from
/// its top at opening and after each switch. The text takes the keyboard focus when the dialog opens,
/// without the keyboard's frame, as the diff's text takes it; Close and Esc give the focus back to the
/// button that opened the dialog (the window's dialog layer keeps it, D104).
/// </summary>
public partial class TextDocumentDialogView : DialogView
{
    private TextDocumentDialogViewModel? _model;

    public TextDocumentDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as TextDocumentDialogViewModel);
    }

    private void Follow(TextDocumentDialogViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        if (model is null)
        {
            return;
        }

        model.PropertyChanged += OnModelChanged;
        TextDocumentText.Show(model.Shown.Text);

        // After the layout pass that shows the dialog: a hidden control cannot take the focus.
        Dispatcher.UIThread.Post(() => TextDocumentText.FocusText(), DispatcherPriority.Loaded);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextDocumentDialogViewModel.Shown) && _model is { } model)
        {
            TextDocumentText.Show(model.Shown.Text);
        }
    }
}
