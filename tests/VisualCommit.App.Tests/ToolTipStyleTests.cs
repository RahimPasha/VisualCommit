using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Tooltips in the app's colours (docs/phase-6-design.md 11.12): Fluent's template with its three
/// brushes set by <c>Theme/Tokens.axaml</c> in each theme, the panel's background, the primary
/// text and the border, in place of Fluent's grey ones; a tooltip open while the theme changes
/// takes the new colours. The Theme switch's tooltip on an empty tab, with System chosen and the
/// harness's fake system turned dark and light under it.
/// </summary>
public class ToolTipStyleTests
{
    private static readonly (string Background, string Foreground, string Border) Dark = ("#1A1D24", "#E4E7EC", "#323845");

    private static readonly (string Background, string Foreground, string Border) Light = ("#F5F6F8", "#1B1F27", "#D2D6DE");

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tooltip_has_the_themes_colours_and_takes_the_other_themes_while_it_is_open(bool startDark)
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.System });
        using var app = ShellDriver.Start(data.Path, systemDark: startDark);
        await app.WaitUntilReadyAsync();
        var themeSwitch = app.Find<ToolbarButton>("ThemeSwitch");

        await app.RestPointerOnAsync(themeSwitch);
        TestPictures.Save(app, $"tooltip-{(startDark ? "dark" : "light")}");
        var size = AssertToolTip(app, startDark);

        await app.SetSystemThemeAsync(!startDark);
        Assert.True(ToolTip.GetIsOpen(themeSwitch), "The tooltip closed when the theme changed.");
        await app.WaitForToolTipAsync(themeSwitch);
        TestPictures.Save(app, $"tooltip-{(startDark ? "dark" : "light")}-flipped");
        AssertToolTip(app, !startDark);

        await app.SetSystemThemeAsync(startDark);
        await app.WaitForToolTipAsync(themeSwitch);
        Assert.Equal(size, AssertToolTip(app, startDark));
    }

    /// <summary>
    /// The open tooltip's brushes and pixels in the theme that shows: the background 2 inside its
    /// border, its text's ink reaching the foreground, the border's colour on its left edge, and none
    /// of Fluent's own colours of that theme inside it (report LD19). Returns its size.
    /// </summary>
    private static Size AssertToolTip(ShellDriver app, bool dark)
    {
        Assert.Equal(dark, app.ShowsDarkTheme);
        var (background, foreground, border) = dark ? Dark : Light;
        var tip = app.Window.GetVisualDescendants().OfType<ToolTip>().Single(candidate => candidate.IsEffectivelyVisible);
        Assert.Equal(Color.Parse(background), Assert.IsAssignableFrom<ISolidColorBrush>(tip.Background).Color);
        Assert.Equal(Color.Parse(foreground), Assert.IsAssignableFrom<ISolidColorBrush>(tip.Foreground).Color);
        Assert.Equal(Color.Parse(border), Assert.IsAssignableFrom<ISolidColorBrush>(tip.BorderBrush).Color);
        Assert.Equal(new Thickness(1), tip.BorderThickness);
        Assert.Equal(new Thickness(8, 5, 8, 7), tip.Padding);
        Assert.Equal(Color.Parse(background), app.ThemeColour("ToolTipBackground"));

        var shot = app.Capture();
        var bounds = app.BoundsOf(tip);
        var left = (int)Math.Ceiling(bounds.X);
        var top = (int)Math.Ceiling(bounds.Y);
        var right = (int)Math.Floor(bounds.Right) - 1;
        var bottom = (int)Math.Floor(bounds.Bottom) - 1;
        var middle = (int)bounds.Center.Y;
        Assert.Equal(Color.Parse(background), shot.PixelAt(left + 3, middle));
        Assert.Contains(Color.Parse(border), new[] { shot.PixelAt(left, middle), shot.PixelAt(left + 1, middle) });

        var inside = new List<Color>();
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                inside.Add(shot.PixelAt(x, y));
            }
        }

        // The 12-px text is drawn with subpixel antialiasing, which colours each channel of a pixel
        // apart: hardly a pixel is the foreground exactly, but each channel's ink reaches it
        // (dark text: the least of each channel; light text: the most), as nothing else inside
        // the tooltip is darker (light) or lighter (dark) than the text.
        var ink = Color.FromRgb(
            Extreme(inside.Select(colour => colour.R), dark),
            Extreme(inside.Select(colour => colour.G), dark),
            Extreme(inside.Select(colour => colour.B), dark));
        Assert.Equal(Color.Parse(foreground), ink);
        var fluent = dark ? new[] { "#2B2B2B", "#FFFFFF" } : new[] { "#F2F2F2", "#000000" };
        foreach (var old in fluent)
        {
            Assert.DoesNotContain(Color.Parse(old), inside);
        }

        return bounds.Size;
    }

    /// <summary>The text's extreme of one channel: the most on a dark background, the least on a light one.</summary>
    private static byte Extreme(IEnumerable<byte> channel, bool dark) => dark ? channel.Max() : channel.Min();
}
