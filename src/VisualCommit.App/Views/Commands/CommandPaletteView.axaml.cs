using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views.Dialogs;

namespace VisualCommit.App.Views.Commands;

/// <summary>
/// The command palette's view (<c>CommandPalette</c>, docs/phase-6-design.md 5.2, 5.3, 5.8):
/// its box takes the focus on opening (the caret at the end) and keeps it; on the box's
/// tunnelling pass Up and Down move the highlight by one, wrapping, PageUp and PageDown by
/// <see cref="CommandPaletteViewModel.PageRows"/>, stopping at the ends, and Enter runs the
/// highlighted entry, each handled; Home, End, Left, Right and the text keys stay the box's. The
/// pointer: moving over a row highlights it, a press and release on one row runs it, the wheel
/// scrolls the list, a press outside the card cancels the palette and is handled (KD15).
/// </summary>
public partial class CommandPaletteView : DialogView
{
    private CommandPaletteViewModel? _viewModel;
    private ListBoxItem? _pressed;

    public CommandPaletteView()
    {
        InitializeComponent();
        CommandPaletteBox.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
        PaletteBackdrop.AddHandler(PointerPressedEvent, OnBackdropPressed, RoutingStrategies.Tunnel);
        CommandPaletteList.AddHandler(PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        CommandPaletteList.AddHandler(PointerPressedEvent, OnListPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        CommandPaletteList.AddHandler(PointerReleasedEvent, OnListReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => Follow(DataContext as CommandPaletteViewModel);
    }

    protected override TextBox? InitialTextBox => CommandPaletteBox;

    private void Follow(CommandPaletteViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel.SelectQueryRequested -= OnSelectQueryRequested;
        }

        _viewModel = viewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelChanged;
            viewModel.SelectQueryRequested += OnSelectQueryRequested;
        }
    }

    /// <summary>The highlight moved: its row scrolls whole into view (5.8).</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.HighlightedIndex) && _viewModel is { HighlightedIndex: >= 0 and var index })
        {
            CommandPaletteList.ScrollIntoView(index);
        }
    }

    /// <summary>Mod+Shift+P while the palette is the top dialog (6.3, KD20): the box's whole text is selected.</summary>
    private void OnSelectQueryRequested(object? sender, EventArgs e) => CommandPaletteBox.SelectAll();

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } palette || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                palette.MoveHighlight(-1, wrap: true);
                break;
            case Key.Down:
                palette.MoveHighlight(1, wrap: true);
                break;
            case Key.PageUp:
                palette.MoveHighlight(-CommandPaletteViewModel.PageRows, wrap: false);
                break;
            case Key.PageDown:
                palette.MoveHighlight(CommandPaletteViewModel.PageRows, wrap: false);
                break;
            case Key.Enter:
                palette.RunHighlighted();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>A press on the backdrop outside the card cancels the palette; neither it nor its release reaches what lies under it (KD15).</summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (ReferenceEquals(source, CommandPaletteCard) || CommandPaletteCard.IsVisualAncestorOf(source)))
        {
            return;
        }

        e.Handled = true;
        _viewModel?.Cancel();
    }

    /// <summary>The pointer's move over a row highlights it (5.8): the highlight follows the pointer, there is no hover look.</summary>
    private void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is { } palette && RowAt(e.Source) is { } row && CommandPaletteList.IndexFromContainer(row) is >= 0 and var index)
        {
            palette.HighlightedIndex = index;
        }
    }

    private void OnListPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = RowAt(e.Source);
        if (_pressed is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && _viewModel is { } palette
            && CommandPaletteList.IndexFromContainer(_pressed) is >= 0 and var index)
        {
            palette.HighlightedIndex = index;
        }
    }

    /// <summary>A press and release on one row runs its entry (5.8); a disabled one does nothing and the palette stays.</summary>
    private void OnListReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressed;
        _pressed = null;
        if (pressed is null || e.InitialPressMouseButton != MouseButton.Left || !ReferenceEquals(RowAt(e.Source), pressed))
        {
            return;
        }

        if (pressed.DataContext is PaletteEntry entry)
        {
            _viewModel?.Run(entry);
        }

        // The box keeps the focus (the rows take none, 5.8).
        e.Handled = true;
    }

    private static ListBoxItem? RowAt(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
}
