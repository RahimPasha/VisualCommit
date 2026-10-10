using VisualCommit.App.Views;
using Xunit;

namespace VisualCommit.App.Tests;

public class PanelLayoutTests
{
    [Fact]
    public void Panels_keep_their_preferred_widths_when_there_is_room()
    {
        Assert.Equal((260.0, 400.0), PanelLayout.Fit(1100, 260, 400));
        Assert.Equal((500.0, 700.0), PanelLayout.Fit(1920, 500, 700));
    }

    [Fact]
    public void Panels_give_way_in_proportion_to_their_room_above_the_minimum()
    {
        // Phase 1, check 17: 500 and 700 preferred in a 1000-wide window.
        var (left, right) = PanelLayout.Fit(1000, 500, 700);

        Assert.Equal(320, 1000 - left - right, precision: 6);
        Assert.Equal(275.1, left, precision: 1);
        Assert.Equal(404.9, right, precision: 1);
    }

    [Fact]
    public void Panels_stop_at_their_minimums()
    {
        Assert.Equal((180.0, 280.0), PanelLayout.Fit(600, 500, 700));
    }

    [Fact]
    public void Preferred_widths_outside_a_panels_range_are_brought_into_it()
    {
        Assert.Equal((520.0, 280.0), PanelLayout.Fit(3000, 900, 100));
    }
}
