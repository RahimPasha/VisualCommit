using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>Phase 4's icons (docs/phase-4-design.md 7.3) are outlines on the 24 by 24 grid of the others.</summary>
public class IconsTests
{
    [AvaloniaTheory]
    [InlineData("IconGrip", 8, 4, 8, 16)]
    [InlineData("IconChevronLeft", 9, 6, 6, 12)]
    [InlineData("IconChevronUp", 6, 9, 12, 6)]
    public void A_phase_4_icon_is_a_geometry_inside_the_24_grid(string key, double x, double y, double width, double height)
    {
        Assert.True(Application.Current!.TryFindResource(key, out var resource), $"{key} is not a resource.");
        var geometry = Assert.IsAssignableFrom<Geometry>(resource);

        // The grip's six dots are circles of radius 1 around (9|15, 5|12|19).
        var bounds = geometry.Bounds;
        Assert.Equal(x, bounds.X, 0.01);
        Assert.Equal(y, bounds.Y, 0.01);
        Assert.Equal(width, bounds.Width, 0.01);
        Assert.Equal(height, bounds.Height, 0.01);
    }
}
