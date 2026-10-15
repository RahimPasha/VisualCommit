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
/// The file history page (docs/phase-5-design.md 4.1): see <see cref="FileHistoryViewModel"/>.
/// The code-behind lays out the list and the diff part by the page's width (BD2), gives the list
/// the keyboard focus when the page asks, opens a row's menu (a right-click selects the row
/// first, BD9), shows a row in the graph on a double-click, and closes the page on Esc
/// (<see cref="EscapeOrder.Page"/>) unless a diff covers it or a text box has the focus.
/// </summary>
public partial class FileHistoryView : UserControl
{
    /// <summary>The list's share of the height below the header when the page is narrower than <see cref="FileHistoryViewModel.SideBySideWidth"/> (BD2).</summary>
    private const double ListShareOfHeight = 0.4;

    /// <summary>The list's share of the width when the page is wider (BD2).</summary>
    private const double ListShareOfWidth = 0.45;

    /// <summary>The list keeps at least 3 rows above the diff (4.1).</summary>
    private const double MinimumListHeight = 3 * 26;

    private FileHistoryViewModel? _viewModel;
    private IDisposable? _escape;

    public FileHistoryView()
    {
        InitializeComponent();
        FileHistoryBody.SizeChanged += (_, e) => LayOut(e.NewSize);
        FileHistoryList.SizeChanged += (_, e) =>
        {
            if (_viewModel is { } viewModel)
            {
                viewModel.ListWidth = e.NewSize.Width;
            }
        };
        FileHistoryList.AddHandler(ContextRequestedEvent, OnContextRequested);
        FileHistoryList.AddHandler(DoubleTappedEvent, OnDoubleTapped);
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

        _viewModel = DataContext as FileHistoryViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.ListWidth = FileHistoryList.Bounds.Width;
            LayOut(FileHistoryBody.Bounds.Size);
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>
    /// Side by side from <see cref="FileHistoryViewModel.SideBySideWidth"/>: the list 45 % of the
    /// width, the diff the rest with a 1-px line on its left; otherwise the list 40 % of the
    /// height (at least 3 rows) above the diff, the line on the diff's top.
    /// </summary>
    private void LayOut(Size size)
    {
        var sideBySide = size.Width >= FileHistoryViewModel.SideBySideWidth;
        if (_viewModel is { } viewModel)
        {
            viewModel.IsSideBySide = sideBySide;
        }

        FileHistoryBody.RowDefinitions.Clear();
        FileHistoryBody.ColumnDefinitions.Clear();
        if (sideBySide)
        {
            var list = Math.Round(size.Width * ListShareOfWidth);
            FileHistoryBody.ColumnDefinitions.Add(new ColumnDefinition(list, GridUnitType.Pixel));
            FileHistoryBody.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetRow(FileHistoryDiff, 0);
            Grid.SetColumn(FileHistoryDiff, 1);
            FileHistoryDiff.BorderThickness = new Thickness(1, 0, 0, 0);
        }
        else
        {
            var list = Math.Max(MinimumListHeight, Math.Round(size.Height * ListShareOfHeight));
            FileHistoryBody.RowDefinitions.Add(new RowDefinition(list, GridUnitType.Pixel));
            FileHistoryBody.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            Grid.SetColumn(FileHistoryDiff, 0);
            Grid.SetRow(FileHistoryDiff, 1);
            FileHistoryDiff.BorderThickness = new Thickness(0, 1, 0, 0);
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(FocusSelectedRow, DispatcherPriority.Loaded);

    /// <summary>Gives the keyboard focus to the selected row (the list itself without one), scrolling it into view.</summary>
    private void FocusSelectedRow()
    {
        if (_viewModel is not { IsClosed: false } || !IsEffectivelyVisible)
        {
            return;
        }

        var index = FileHistoryList.SelectedIndex;
        if (index >= 0)
        {
            FileHistoryList.ScrollIntoView(index);
            if (FileHistoryList.ContainerFromIndex(index) is { } row)
            {
                row.Focus(NavigationMethod.Directional);
                return;
            }
        }

        FileHistoryList.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>A row's menu (4.1, BD8, BD9): a right-click selects the row as a click does, then opens it at the pointer; the Menu key and Shift+F10 at the selected row's bottom-left corner.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsClosed: false } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        var item = ContextMenuRequests.Find<ListBoxItem>(e.Source) ?? (FileHistoryList.SelectedIndex >= 0 ? FileHistoryList.ContainerFromIndex(FileHistoryList.SelectedIndex) as ListBoxItem : null);
        if (item?.DataContext is not FileHistoryRowViewModel row)
        {
            return;
        }

        viewModel.SelectedRow = row;
        item.Focus(NavigationMethod.Pointer);
        var (position, openedBy) = ContextMenuRequests.Where(e, item);
        layer.Open(viewModel.MenuFor(row), position, openedBy);
        e.Handled = true;
    }

    /// <summary>A double-click on a row does "Show in graph" (4.1).</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is { IsClosed: false } viewModel && ContextMenuRequests.Find<ListBoxItem>(e.Source)?.DataContext is FileHistoryRowViewModel row)
        {
            viewModel.ShowInGraph(row);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Esc (11.1, <see cref="EscapeOrder.Page"/>): the page closes and the graph takes the focus;
    /// it declines while a diff covers the page (it is not visible then) or a text box has the focus.
    /// </summary>
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

/// <summary>A page of the graph area as UI Automation sees it (13.4): a pane named by its title, a control element, as phase 4's views are.</summary>
internal sealed class PageAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
