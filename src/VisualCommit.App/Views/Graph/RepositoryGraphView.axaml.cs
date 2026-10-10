using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;

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

    public RepositoryGraphView()
    {
        InitializeComponent();

        GraphRows.RowsDrawn += (_, _) => _viewModel?.OnRowsDrawn();
        GraphRows.FrameDrawn += (_, duration) => _viewModel?.OnFrameDrawn(duration);
        GraphRows.PropertyChanged += OnGraphRowsPropertyChanged;
        GraphScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        GraphScrollBar.PointerWheelChanged += OnScrollBarWheel;

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

        base.OnDataContextChanged(e);
    }

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
