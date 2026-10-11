using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Graph;

/// <summary>
/// The graph area of a tab with a repository open: column headers above the custom-drawn rows,
/// a scroll bar over the rows' right edge, and the "No commits yet" and error states. The view
/// connects what bindings cannot: the view model's reveal requests and the Q1 measurements
/// (D50), the headers' widths and the scroll bar.
/// </summary>
public partial class RepositoryGraphView : UserControl
{
    private RepositoryViewModel? _viewModel;
    private bool _updatingScrollBar;

    /// <summary>The context request on its way to the rows came from the keyboard (Shift+F10, the Menu key): it has no pointer position.</summary>
    private bool _requestedByKeyboard;

    public RepositoryGraphView()
    {
        InitializeComponent();

        GraphRows.RowsDrawn += (_, _) => _viewModel?.OnRowsDrawn();
        GraphRows.FrameDrawn += (_, duration) => _viewModel?.OnFrameDrawn(duration);
        GraphRows.PropertyChanged += OnGraphRowsPropertyChanged;
        GraphScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        GraphScrollBar.PointerWheelChanged += OnScrollBarWheel;

        // The menus (docs/phase-3-design.md 4a, 4b, 4c): the rows' requests come with what is
        // under the point; whether a request came from the keyboard is seen on its way down
        // (it carries no pointer position), before the rows handle it.
        AddHandler(ContextRequestedEvent, (_, e) => _requestedByKeyboard = !e.TryGetPosition(null, out Point _),RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        GraphRows.ContextRequestedAt += OnGraphContextRequested;
        GraphRows.LabelDoubleTapped += OnLabelDoubleTapped;

        ApplyColumns(GraphRows.Columns);
        UpdateScrollBar();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RevealRequested -= OnRevealRequested;
        }

        _viewModel = DataContext as RepositoryViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RevealRequested += OnRevealRequested;
        }

        ConnectDropMenu();
        base.OnDataContextChanged(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ConnectDropMenu();
    }

    /// <summary>
    /// Gives the repository the drop's menu (4g): a branch dropped onto a branch opens it at the
    /// release point on this window's menu layer, once the drop's facts are read. A menu set
    /// already (a test's) is kept.
    /// </summary>
    private void ConnectDropMenu()
    {
        if (_viewModel is { BranchDropMenu: null } viewModel && ContextMenuRequests.Layer(this) is { } layer)
        {
            viewModel.BranchDropMenu = viewModel.DropMenuOpener(layer);
        }
    }

    /// <summary>
    /// A menu asked for over the rows (4b): the working-changes row's, a commit's (or the
    /// selection's, or a stash's), a label's, or the empty area's below the last row. From the
    /// keyboard it opens at the bottom-left of the focused row's message cell.
    /// </summary>
    private void OnGraphContextRequested(object? sender, GraphContextRequestedEventArgs e)
    {
        if (_viewModel is not { } viewModel || ContextMenuRequests.Layer(this) is not { } layer || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var point = GraphRows.TranslatePoint(e.Position, topLevel) ?? e.Position;
        var position = new MenuPosition(point.X, point.Y);
        var openedBy = _requestedByKeyboard ? MenuOpenedBy.Keyboard : MenuOpenedBy.Pointer;
        e.Handled = e.Hit switch
        {
            { Kind: GraphHitKind.WorkingRow } => viewModel.OpenWorkingChangesMenu(layer, position, openedBy),
            { Kind: GraphHitKind.Commit, Sha: { } sha } => viewModel.OpenCommitMenu(layer, sha, position, openedBy),
            { Kind: GraphHitKind.Label, Sha: { } sha, Label: { } label } => viewModel.OpenLabelMenu(layer, sha, LabelOf(label), position, openedBy),
            _ => viewModel.OpenEmptyGraphAreaMenu(layer, position, openedBy),
        };
    }

    /// <summary>
    /// A request the rows did not take: over "No commits yet" or the error text, which lie over the
    /// rows, it is the empty graph area's menu (4b); the column headers have none.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        if (ContextMenuRequests.Find<Control>(e.Source, control => ReferenceEquals(control, NoCommits) || ReferenceEquals(control, GraphError)) is { } area)
        {
            var (position, openedBy) = ContextMenuRequests.Where(e, area);
            e.Handled = viewModel.OpenEmptyGraphAreaMenu(layer, position, openedBy);
        }
    }

    /// <summary>A double-click on a branch label checks it out (4c); the "+N" label, a tag, a stash and HEAD only select (D93).</summary>
    private void OnLabelDoubleTapped(object? sender, GraphHit hit)
    {
        if (_viewModel is { } viewModel && hit.Label is { IsOverflow: false } label && LabelOf(label) is { } refLabel)
        {
            viewModel.DoubleTapLabel(refLabel);
        }
    }

    /// <summary>The label a drawn label stands for; null for the "+N" label, which stands for its commit.</summary>
    private static RefLabel? LabelOf(PlacedLabel label) =>
        label.IsOverflow ? null : new RefLabel(label.FullText, label.Kind, label.RefName);

    private void OnRevealRequested(object? sender, int index) => GraphRows.Reveal(index);

    private void OnGraphRowsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CommitGraphControl.ColumnsProperty)
        {
            ApplyColumns(GraphRows.Columns);
        }
        else if (e.Property == CommitGraphControl.ScrollOffsetProperty
            || e.Property == CommitGraphControl.MaxScrollOffsetProperty
            || e.Property == CommitGraphControl.ViewportHeightProperty)
        {
            UpdateScrollBar();
        }
    }

    /// <summary>The user moved the scroll bar: scroll the rows, which passes the offset on to the view model.</summary>
    private void OnScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && !_updatingScrollBar)
        {
            GraphRows.SetCurrentValue(CommitGraphControl.ScrollOffsetProperty, GraphScrollBar.Value);
        }
    }

    /// <summary>The scroll bar lies over the rows' right edge, so the wheel over it scrolls the rows as it does anywhere else on them.</summary>
    private void OnScrollBarWheel(object? sender, PointerWheelEventArgs e)
    {
        var offset = GraphRows.ScrollOffset - (e.Delta.Y * CommitGraphControl.RowsPerWheelNotch * CommitGraphControl.RowHeight);
        GraphRows.SetCurrentValue(CommitGraphControl.ScrollOffsetProperty, Math.Clamp(offset, 0, GraphRows.MaxScrollOffset));
        e.Handled = true;
    }

    private void UpdateScrollBar()
    {
        _updatingScrollBar = true;
        try
        {
            GraphScrollBar.Maximum = GraphRows.MaxScrollOffset;
            GraphScrollBar.ViewportSize = GraphRows.ViewportHeight;
            GraphScrollBar.LargeChange = Math.Max(CommitGraphControl.RowHeight, GraphRows.ViewportHeight);
            GraphScrollBar.Value = GraphRows.ScrollOffset;
            GraphScrollBar.IsVisible = GraphRows.MaxScrollOffset > 0;
        }
        finally
        {
            _updatingScrollBar = false;
        }
    }

    /// <summary>Gives each header its column's width and hides the headers of hidden columns.</summary>
    private void ApplyColumns(GraphColumns columns)
    {
        var definitions = HeaderColumns.ColumnDefinitions;
        Apply(0, columns.Refs, HeaderRefs);
        Apply(1, columns.Graph, HeaderGraph);
        Apply(2, columns.Message, HeaderMessage);
        Apply(3, columns.Author, HeaderAuthor);
        Apply(4, columns.Date, HeaderDate);
        Apply(5, columns.Sha, HeaderSha);

        void Apply(int index, GraphColumn column, TextBlock header)
        {
            definitions[index].Width = new GridLength(column.Width);
            header.IsVisible = column.IsVisible;
        }
    }
}
