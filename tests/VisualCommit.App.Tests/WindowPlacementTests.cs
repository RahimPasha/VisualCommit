using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>Headless tests of how the window keeps its placement and the panels their widths (D46).</summary>
public class WindowPlacementTests
{
    [AvaloniaFact]
    public void A_window_restored_maximised_keeps_its_normal_size_when_it_closes_maximised()
    {
        using var data = new TempDirectory("data");
        ShellDriver.WriteSession(data.Path, new SessionState { Window = new WindowPlacement(100, 80, 1300, 800, IsMaximized: true) });

        using (var app = ShellDriver.Start(data.Path, width: null, height: null))
        {
            Assert.Equal(WindowState.Maximized, app.Window.WindowState);
        }

        var saved = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current.Window;
        Assert.NotNull(saved);
        Assert.True(saved.IsMaximized);
        Assert.Equal(1300, saved.Width);
        Assert.Equal(800, saved.Height);
    }

    [AvaloniaFact]
    public void A_click_on_a_panel_edge_without_moving_keeps_both_panels_preferred_widths()
    {
        using var data = new TempDirectory("data");
        ShellDriver.WriteSession(data.Path, new SessionState { LeftPanelWidth = 500, RightPanelWidth = 700 });

        using (var app = ShellDriver.Start(data.Path, 1000, 700))
        {
            // The panels give way in a 1000-wide window.
            Assert.Equal(275.1, app.Window.PanelWidths.Left, 0.5);
            Assert.Equal(404.9, app.Window.PanelWidths.Right, 0.5);

            var middle = app.BoundsOf(app.Find<Grid>("MainArea")).Center.Y;
            var edge = new Point(1000 - app.Window.PanelWidths.Right, middle);
            app.Drag(edge, edge, steps: 1);
        }

        var saved = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current;
        Assert.Equal(500, saved.LeftPanelWidth);
        Assert.Equal(700, saved.RightPanelWidth);
    }

    [AvaloniaFact]
    public void Dragging_one_panel_in_a_narrow_window_keeps_the_other_panels_preference()
    {
        using var data = new TempDirectory("data");
        ShellDriver.WriteSession(data.Path, new SessionState { LeftPanelWidth = 500, RightPanelWidth = 700 });

        using (var app = ShellDriver.Start(data.Path, 1000, 700))
        {
            var middle = app.BoundsOf(app.Find<Grid>("MainArea")).Center.Y;
            var left = app.Window.PanelWidths.Left;
            app.Drag(new Point(left, middle), new Point(left - 40, middle));
        }

        var saved = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current;
        Assert.Equal(235.1, saved.LeftPanelWidth!.Value, 1.0);
        Assert.Equal(700, saved.RightPanelWidth);
    }
}
