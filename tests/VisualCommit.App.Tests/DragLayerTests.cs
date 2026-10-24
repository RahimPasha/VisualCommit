using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using VisualCommit.App.Services;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.GraphSelectionTests;

namespace VisualCommit.App.Tests;

/// <summary>
/// The drag layer's colours when the theme changes in the middle of a branch drag
/// (<see cref="DragLayer"/>, docs/phase-6-design.md 11.14, design 0.6's gap): with System chosen
/// and the system turning light and dark again while <c>feature/alpha</c> is held over
/// <c>main</c>, the pill and the target's outline are drawn at once in the colours of the theme
/// that shows, without a move of the pointer; Esc still ends the drag. The ops scenario at
/// 1100×700, as phase 3's check 7 and part L's L27.
/// </summary>
public class DragLayerTests
{
    private const int MainWork = 3;
    private const int AlphaTwo = 4;

    [AvaloniaFact]
    public async Task A_theme_change_mid_drag_redraws_the_pill_and_the_outline()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.System });
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        await GraphShownAsync(app);
        var layer = app.Find<DragLayer>("DragLayer");
        var service = layer.Service!;
        var main = app.GraphLabelBounds(MainWork, "main");

        app.PressMouse(app.GraphLabelBounds(AlphaTwo, "feature/alpha").Center);
        app.MoveMouseHeld(main.Center, steps: 5);
        Assert.True(service.IsDragging);

        var dark = app.Capture();
        var outline = layer.OutlineBounds!.Value;
        Assert.Equal(main.Inflate(3), outline);
        AssertOutline(dark, outline, "#7C8CFF", "dark");
        AssertPill(dark, layer, "#C95967", "dark");

        // The system turns light: no pointer move, yet the pill and the outline are light.
        await app.SetSystemThemeAsync(dark: false);
        Assert.True(service.IsDragging);
        var light = app.Capture();
        TestPictures.Save(app, "drag-layer-flipped-light");
        AssertOutline(light, outline, "#4353D8", "light");
        AssertPill(light, layer, "#CC5361", "light");
        Assert.False(ContainsColour(light, Color.Parse("#7C8CFF")), "The light picture still has the dark outline's colour #7C8CFF.");

        // And dark again.
        await app.SetSystemThemeAsync(dark: true);
        var back = app.Capture();
        AssertOutline(back, outline, "#7C8CFF", "dark again");
        AssertPill(back, layer, "#C95967", "dark again");

        // Esc ends the drag; the release then drops nothing and opens no menu.
        app.PressKey(Key.Escape);
        Assert.False(service.IsDragging);
        app.ReleaseMouse(main.Center);
        app.Capture();
        Assert.Null(layer.PillBounds);
        Assert.Null(layer.OutlineBounds);
        Assert.False(app.IsMenuOpen);
    }

    /// <summary>The middle of the outline's top stroke and of its left stroke, exactly the drag target's colour (the report's L27 points).</summary>
    private static void AssertOutline(Screenshot shot, Rect outline, string colour, string what)
    {
        var expected = Color.Parse(colour);
        Assert.True(expected == shot.PixelAt(new Point(outline.Center.X, outline.Top + 1)), $"The outline's top stroke in the {what} picture: {shot.PixelAt(new Point(outline.Center.X, outline.Top + 1))}, not {colour}.");
        Assert.True(expected == shot.PixelAt(new Point(outline.X + 1, outline.Center.Y)), $"The outline's left stroke in the {what} picture: {shot.PixelAt(new Point(outline.X + 1, outline.Center.Y))}, not {colour}.");
    }

    /// <summary>
    /// The pill's left padding, 4 inside its left end and 6 below its top, where it lies over the
    /// selected row 4: lane colour 3 at 85 % over <c>VcSelectionBrush</c>, within 8 per channel.
    /// </summary>
    private static void AssertPill(Screenshot shot, DragLayer layer, string colour, string what)
    {
        var pill = layer.PillBounds!.Value;
        var expected = Color.Parse(colour);
        var actual = shot.PixelAt(new Point(pill.X + 4, pill.Top + 6));
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 8 && Math.Abs(expected.G - actual.G) <= 8 && Math.Abs(expected.B - actual.B) <= 8,
            $"The pill in the {what} picture: {actual}, not about {colour}.");
    }

    private static bool ContainsColour(Screenshot shot, Color colour)
    {
        for (var y = 0; y < shot.Height; y++)
        {
            for (var x = 0; x < shot.Width; x++)
            {
                if (shot.PixelAt(x, y) == colour)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
