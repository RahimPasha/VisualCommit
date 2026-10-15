using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Pages;

/// <summary>
/// The reflog page and the backups list (docs/phase-5-design.md 5.1 to 5.3): see
/// <see cref="ReflogViewModel"/>. The code-behind hands the page's width to the view model for a
/// row's columns (BD3), opens the source menu at the selector's bottom-left corner (BD7) and a
/// row's menu (a right-click selects the row first, BD9), shows a row in the graph on a
/// double-click, gives the list the keyboard focus when the page opens (BD10), and closes the
/// page on Esc (<see cref="EscapeOrder.Page"/>) unless a diff covers it or a text box has the focus.
/// </summary>
public partial class ReflogView : UserControl
{
    private ReflogViewModel? _viewModel;
    private IDisposable? _escape;

    public ReflogView()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            if (_viewModel is { } viewModel)
            {
                viewModel.PageWidth = e.NewSize.Width;
            }
        };
        ReflogSource.Click += OnSourceClick;
        ReflogList.AddHandler(ContextRequestedEvent, OnContextRequested);
        ReflogList.AddHandler(DoubleTappedEvent, OnDoubleTapped);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.Page);
        }
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

        _viewModel = DataContext as ReflogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.PageWidth = Bounds.Width;

            // The page opens with the keyboard focus on its list, no row selected (BD10).
            OnFocusRequested(this, EventArgs.Empty);
        }

        base.OnDataContextChanged(e);
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_viewModel is { IsClosed: false } && IsEffectivelyVisible)
                {
                    ReflogList.Focus(NavigationMethod.Unspecified);
                }
            },
            DispatcherPriority.Loaded);

    /// <summary>The source menu (5.1, BD7): opened with its top-left corner at the selector's bottom-left corner.</summary>
    private void OnSourceClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not { IsClosed: false } viewModel || ContextMenuRequests.Layer(this) is not { } layer
            || TopLevel.GetTopLevel(this) is not { } topLevel || ReflogSource.TranslatePoint(new Point(0, ReflogSource.Bounds.Height), topLevel) is not { } corner)
        {
            return;
        }

        // Opened by the pointer and placed with the pointer's gap taken back, as phase 4's action buttons' menus (14.3).
        layer.Open(viewModel.SourceMenu(), new MenuPosition(corner.X - MenuLayer.PointerGap, corner.Y - MenuLayer.PointerGap), MenuOpenedBy.Pointer);
        e.Handled = true;
    }

    /// <summary>
    /// A row's menu (5.2, 5.3, BD8, BD9): a right-click on a row selects it as a click does, then
    /// opens it at the pointer, and one on the list's empty part opens nothing; the Menu key and
    /// Shift+F10 open the selected row's at its bottom-left corner.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsClosed: false } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        var item = ContextMenuRequests.Find<ListBoxItem>(e.Source) ?? (!e.TryGetPosition(this, out _) ? FileHistoryView.SelectedItem(ReflogList) : null);
        if (item?.DataContext is not PageListRow row)
        {
            return;
        }

        viewModel.SelectedRow = row;
        item.Focus(NavigationMethod.Pointer);
        var (position, openedBy) = ContextMenuRequests.Where(e, item);
        layer.Open(viewModel.MenuFor(row), position, openedBy);
        e.Handled = true;
    }

    /// <summary>A double-click on a row does "Show in graph" (5.2).</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is { IsClosed: false } viewModel && ContextMenuRequests.Find<ListBoxItem>(e.Source)?.DataContext is PageListRow row)
        {
            viewModel.ShowInGraph(row);
            e.Handled = true;
        }
    }

    /// <summary>Esc (11.1): the page closes, unless a diff covers it or a text box has the focus.</summary>
    private bool HandleEscape()
    {
        if (_viewModel is not { IsClosed: false } viewModel || !IsEffectivelyVisible
            || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return false;
        }

        viewModel.Close();
        return true;
    }
}
