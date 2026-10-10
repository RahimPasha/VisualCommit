using Avalonia;
using Avalonia.Controls;
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

    public MainWindow()
    {
        InitializeComponent();

        MainArea.SizeChanged += (_, _) => FitPanels();
        foreach (var splitter in new[] { LeftSplitter, RightSplitter })
        {
            splitter.DragStarted += (_, _) => _dragged = splitter;
            splitter.DragCompleted += (_, _) => PanelDragged(splitter);
        }

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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
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
}
