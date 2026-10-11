using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Session;

namespace VisualCommit.App.Views;

public partial class MainWindow : Window
{
    private ISessionStore? _session;
    private double _preferredLeft = PanelLayout.LeftDefault;
    private double _preferredRight = PanelLayout.RightDefault;
    private PixelPoint? _normalPosition;
    private Size? _normalSize;
    private bool _wasMaximized;
    private GridSplitter? _dragged;
    private readonly List<(int Order, IEscapeHandler Handler)> _escapeHandlers = [];
    private readonly Dictionary<DialogViewModel, IInputElement?> _focusBeforeDialog = [];
    private DialogHostViewModel? _dialogs;

    public MainWindow()
    {
        InitializeComponent();

        MainArea.SizeChanged += (_, _) => FitPanels();
        foreach (var splitter in new[] { LeftSplitter, RightSplitter })
        {
            splitter.DragStarted += (_, _) => _dragged = splitter;
            splitter.DragCompleted += (_, _) => PanelDragged(splitter);
        }

        // Seen even when a control under the focus has handled it, as the diff's text may.
        AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Bubble, handledEventsToo: true);

        // Shift+F10 asks for the focused element's context menu, as the Menu key does: Avalonia
        // raises the request only for the Menu key (docs/phase-3-design.md section 0, 4a).
        AddHandler(KeyDownEvent, OnShiftF10, RoutingStrategies.Tunnel, handledEventsToo: true);

        // The window's own links of the Esc chain; the menu layer and the drag service add theirs.
        AddEscapeHandler(new EscapeHandler(CancelTopDialog), EscapeOrder.Dialog);
        AddEscapeHandler(new EscapeHandler(CloseDiff), EscapeOrder.Diff);
        DataContextChanged += (_, _) => FollowDialogs((DataContext as MainWindowViewModel)?.Dialogs);

        PositionChanged += (_, _) => RememberNormalPlacement();
        SizeChanged += (_, _) => RememberNormalPlacement();
        PropertyChanged += (_, e) =>
        {
            // Minimised keeps what the window was before, so that closing it from the taskbar
            // restores it the way it was shown.
            if (e.Property == WindowStateProperty && WindowState != WindowState.Minimized)
            {
                _wasMaximized = WindowState == WindowState.Maximized;
            }
        };
    }

    /// <summary>The widths the side panels are drawn at now, for tests and the session.</summary>
    public (double Left, double Right) PanelWidths =>
        (MainArea.ColumnDefinitions[0].ActualWidth, MainArea.ColumnDefinitions[2].ActualWidth);

    /// <summary>
    /// Restores the window's size, position and panel widths from the session (D46) and saves
    /// them there from now on: the panel widths when the user drags an edge, the window's
    /// placement when it closes. Call before the window is shown.
    /// </summary>
    public void UseSession(ISessionStore session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        var state = session.Current;
        _preferredLeft = state.LeftPanelWidth ?? PanelLayout.LeftDefault;
        _preferredRight = state.RightPanelWidth ?? PanelLayout.RightDefault;

        if (state.Window is { } placement)
        {
            Width = Math.Max(placement.Width, MinWidth);
            Height = Math.Max(placement.Height, MinHeight);
            _normalSize = new Size(Width, Height);
            var position = new PixelPoint(placement.X, placement.Y);
            if (IsOnAScreen(position))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = position;
                _normalPosition = position;
            }

            // A window restored maximised keeps the normal size and position it had, so that
            // they are saved again even if it is never shown un-maximised this time.
            if (placement.IsMaximized)
            {
                WindowState = WindowState.Maximized;
                _wasMaximized = true;
            }
        }

        FitPanels();
    }

    /// <summary>
    /// Adds a link to the Esc chain (docs/phase-3-design.md section 8): on Esc the handlers are
    /// asked in their <paramref name="order"/> (<see cref="EscapeOrder"/>) until one handles it.
    /// Disposing the result removes the link.
    /// </summary>
    public IDisposable AddEscapeHandler(IEscapeHandler handler, int order)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var entry = (order, handler);
        var at = _escapeHandlers.FindIndex(other => other.Order > order);
        _escapeHandlers.Insert(at < 0 ? _escapeHandlers.Count : at, entry);
        return new Removal(() => _escapeHandlers.Remove(entry));
    }

    /// <summary>
    /// The Closing hook (1.8): while an operation the user started runs, the close waits for it in
    /// "Finishing operations" and the window closes by itself afterwards; see
    /// <see cref="MainWindowViewModel.TryClose"/>.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel && DataContext is MainWindowViewModel shell && !shell.TryClose(Close))
        {
            e.Cancel = true;
        }

        if (!e.Cancel)
        {
            SavePlacement();
        }
    }

    /// <summary>
    /// The user let go of a panel's edge: that panel's width as it is now becomes its preferred
    /// width. The other panel keeps its preference, even if it is drawn narrower at the moment
    /// because the window is too small for both (a click on an edge without moving it changes
    /// nothing).
    /// </summary>
    private void PanelDragged(GridSplitter splitter)
    {
        _dragged = null;
        var columns = MainArea.ColumnDefinitions;
        var (fittedLeft, fittedRight) = PanelLayout.Fit(MainArea.Bounds.Width, _preferredLeft, _preferredRight);
        if (splitter == LeftSplitter && Math.Abs(columns[0].ActualWidth - fittedLeft) > 0.5)
        {
            _preferredLeft = columns[0].ActualWidth;
        }
        else if (splitter == RightSplitter && Math.Abs(columns[2].ActualWidth - fittedRight) > 0.5)
        {
            _preferredRight = columns[2].ActualWidth;
        }

        // The splitter may have given the graph's column a fixed width; it must stay the one that
        // takes the rest.
        columns[1].Width = new GridLength(1, GridUnitType.Star);
        FitPanels();

        _session?.Update(state => state with { LeftPanelWidth = _preferredLeft, RightPanelWidth = _preferredRight });
    }

    private void FitPanels()
    {
        var available = MainArea.Bounds.Width;
        if (_dragged is not null || available <= 0)
        {
            return;
        }

        var (left, right) = PanelLayout.Fit(available, _preferredLeft, _preferredRight);
        var columns = MainArea.ColumnDefinitions;
        if (Math.Abs(columns[0].Width.Value - left) > 0.01 || !columns[0].Width.IsAbsolute)
        {
            columns[0].Width = new GridLength(left);
        }

        if (Math.Abs(columns[2].Width.Value - right) > 0.01 || !columns[2].Width.IsAbsolute)
        {
            columns[2].Width = new GridLength(right);
        }
    }

    private void RememberNormalPlacement()
    {
        if (WindowState == WindowState.Normal && IsVisible)
        {
            _normalPosition = Position;
            _normalSize = ClientSize;
        }
    }

    private void SavePlacement()
    {
        if (_session is null)
        {
            return;
        }

        RememberNormalPlacement();
        var position = _normalPosition ?? Position;
        var size = _normalSize ?? ClientSize;
        var placement = new WindowPlacement(position.X, position.Y, size.Width, size.Height, _wasMaximized);
        _session.Update(state => state with { Window = placement });
    }

    private bool IsOnAScreen(PixelPoint position)
    {
        try
        {
            // The title bar's left end must be on a screen, so the window can be grabbed and moved.
            return Screens.ScreenFromPoint(position + new PixelPoint(40, 10)) is not null;
        }
        catch (Exception)
        {
            // A platform without screen information (headless tests): leave the position to the system.
            return false;
        }
    }

    /// <summary>
    /// Esc goes down the chain (<see cref="AddEscapeHandler"/>): an open menu closes, a drag is
    /// cancelled, the top dialog is cancelled, or else the open diff closes, unless a text box has
    /// the focus (the commit message).
    /// </summary>
    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        foreach (var (_, handler) in _escapeHandlers.ToList())
        {
            if (handler.HandleEscape())
            {
                e.Handled = true;
                return;
            }
        }
    }

    private bool CancelTopDialog() => DataContext is MainWindowViewModel shell && shell.Dialogs.CancelCurrent();

    private bool CloseDiff()
    {
        if (DataContext is not MainWindowViewModel shell || FocusManager?.GetFocusedElement() is TextBox)
        {
            return false;
        }

        if (shell.ActiveTab.Repository is { IsDiffOpen: true } repository)
        {
            repository.CloseDiff();
            return true;
        }

        return false;
    }

    /// <summary>Shift+F10 raises the context request on the focused element, without a position (4a).</summary>
    private void OnShiftF10(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F10 || e.KeyModifiers != KeyModifiers.Shift || FocusManager?.GetFocusedElement() is not Control focused)
        {
            return;
        }

        var request = new ContextRequestedEventArgs();
        focused.RaiseEvent(request);
        e.Handled = true;
    }

    /// <summary>
    /// The dialog below gets the focus back when the one on top of it closes (D104): the element
    /// that had the focus when each dialog opened is kept, and given back while a dialog is still
    /// open. When the last dialog closes, its view is hidden before it leaves the window, as phase
    /// 2's dialog was hidden: a focused element that leaves the window while visible hands the
    /// focus on, with its keyboard outline, to an element of Avalonia's choosing (the diff's text
    /// in phase 2's check 19), which would change phase 2's pictures.
    /// </summary>
    private void FollowDialogs(DialogHostViewModel? dialogs)
    {
        if (ReferenceEquals(dialogs, _dialogs))
        {
            return;
        }

        if (_dialogs is not null)
        {
            _dialogs.Open.CollectionChanged -= OnDialogsChanged;
            _dialogs.Removing -= OnDialogRemoving;
        }

        _dialogs = dialogs;
        _focusBeforeDialog.Clear();
        if (dialogs is not null)
        {
            dialogs.Open.CollectionChanged += OnDialogsChanged;
            dialogs.Removing += OnDialogRemoving;
        }
    }

    private void OnDialogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var dialog in e.NewItems?.OfType<DialogViewModel>() ?? [])
            {
                _focusBeforeDialog[dialog] = FocusManager?.GetFocusedElement();
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _focusBeforeDialog.Clear();
        }
    }

    private void OnDialogRemoving(object? sender, DialogViewModel dialog)
    {
        _focusBeforeDialog.Remove(dialog, out var before);
        if (_dialogs is not { } dialogs || !ReferenceEquals(dialogs.Current, dialog))
        {
            // A dialog below the top one closed: the top one keeps the focus.
            return;
        }

        if (dialogs.Open.Count > 1 && before is InputElement { IsEffectivelyVisible: true } element)
        {
            element.Focus(NavigationMethod.Tab);
        }
        else if (DialogLayer.ContainerFromItem(dialog) is { } view)
        {
            view.IsVisible = false;
        }
    }

    private sealed class Removal(Action remove) : IDisposable
    {
        private Action? _remove = remove;

        public void Dispose()
        {
            _remove?.Invoke();
            _remove = null;
        }
    }
}
