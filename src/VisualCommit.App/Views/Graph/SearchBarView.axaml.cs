using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Graph;

/// <summary>
/// The search bar (docs/phase-5-design.md 3.1; see <see cref="SearchViewModel"/>). The code-behind
/// adds what bindings cannot: the box takes the keyboard focus with its text selected when the bar
/// opens or asks for it, Enter and Shift+Enter in the box go to the next and previous match, the
/// mode button opens the mode menu at its bottom-left corner, Esc closes the bar while the focus
/// is in it (<see cref="EscapeOrder.Search"/>), and the count is cut, then hidden, when the bar is
/// too narrow for the box's 120.
/// </summary>
public partial class SearchBarView : UserControl
{
    /// <summary>The box's narrowest (3.1).</summary>
    public const double MinBoxWidth = 120;

    /// <summary>The count's widest (3.1).</summary>
    public const double MaxCountWidth = 110;

    /// <summary>Below this much room the count is hidden instead of cut.</summary>
    private const double MinCountWidth = 24;

    private SearchViewModel? _viewModel;
    private IDisposable? _escape;

    public SearchBarView()
    {
        InitializeComponent();
        SearchBox.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
        SearchModeButton.Click += OnModeButtonClick;
        SearchParts.SizeChanged += (_, _) => FitCount();
        SearchModeButton.SizeChanged += (_, _) => FitCount();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SearchBarAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.Search);
        }

        FocusBoxLater();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _escape?.Dispose();
        _escape = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
        }

        _viewModel = DataContext as SearchViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;

            // A bar that opens has the focus in its box (3.1).
            FocusBoxLater();
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Esc closes the bar while the focus is in it (3.1); elsewhere it goes on down the chain.</summary>
    private bool HandleEscape()
    {
        if (_viewModel is null || !IsEffectivelyVisible || !IsKeyboardFocusWithin)
        {
            return false;
        }

        _viewModel.CloseCommand.Execute(null);
        return true;
    }

    private void OnFocusRequested(object? sender, EventArgs e) => FocusBoxLater();

    /// <summary>Gives the box the focus with its text selected, once the layout has made it visible (a hidden control cannot take the focus).</summary>
    private void FocusBoxLater() =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_viewModel is not null && SearchBox.IsEffectivelyVisible)
                {
                    SearchBox.Focus(NavigationMethod.Tab);
                    SearchBox.SelectAll();
                }
            },
            DispatcherPriority.Loaded);

    /// <summary>Enter goes to the next match, Shift+Enter to the previous one; the focus stays in the box (FD3).</summary>
    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || e.Key is not (Key.Enter or Key.Return))
        {
            return;
        }

        if (e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            _viewModel.Enter(backwards: e.KeyModifiers == KeyModifiers.Shift);
            e.Handled = true;
        }
    }

    /// <summary>The mode menu (3.1, FD5), with its top-left at the button's bottom-left corner, as phase 4's action buttons open theirs.</summary>
    private void OnModeButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || ContextMenuRequests.Layer(this) is not { } layer || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        if (SearchModeButton.TranslatePoint(new Point(0, SearchModeButton.Bounds.Height), topLevel) is { } corner)
        {
            // Opened by the pointer and placed with the pointer's gap taken back (phase 4's 14.3).
            layer.Open(_viewModel.ModeMenu(), new MenuPosition(corner.X - MenuLayer.PointerGap, corner.Y - MenuLayer.PointerGap), MenuOpenedBy.Pointer);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Keeps the box at least <see cref="MinBoxWidth"/> wide (3.1): the count takes at most
    /// <see cref="MaxCountWidth"/> and what the box leaves, cut with "…" (its whole text in its
    /// tooltip), and is hidden when too little is left; the buttons keep their places.
    /// </summary>
    private void FitCount()
    {
        var width = SearchParts.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        // The mode button and its gap, the box's gap, the count's gap, and the three 22-wide buttons with theirs.
        var fixedParts = SearchModeButton.Bounds.Width + SearchBox.Margin.Left + SearchCount.Margin.Left
            + SearchPrevious.Width + SearchPrevious.Margin.Left
            + SearchNext.Width + SearchNext.Margin.Left
            + SearchClose.Width + SearchClose.Margin.Left;
        var room = width - fixedParts - MinBoxWidth;
        SearchCount.MaxWidth = Math.Clamp(room, 0, MaxCountWidth);
        SearchCount.IsVisible = room >= MinCountWidth;
    }

    /// <summary>The bar is a UI Automation control element (a pane named "Search"), so the real-window pass finds it by its automation id.</summary>
    private sealed class SearchBarAutomationPeer(SearchBarView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
