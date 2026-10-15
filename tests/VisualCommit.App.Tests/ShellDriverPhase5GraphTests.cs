using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The graph's pixel helpers of phase 5's part F (<c>ShellDriver.Phase5.Graph.cs</c>, decision
/// FD15 of docs/test-reports/phase-5.md): first on columns of pixels made by hand, so that each
/// rule is seen to hold and to fail, then on the app's own picture of phase 1's graph scenario at
/// the report's positions, so that a check that fails points at the app and not at the harness.
/// </summary>
public class ShellDriverPhase5GraphTests
{
    private static readonly Color Lane = Color.Parse("#7C8CFF");

    private static readonly Color Background = Color.Parse("#14161B");

    [AvaloniaFact]
    public void Dashes_and_gaps_of_three_are_dashed_and_the_range_may_cut_the_first_and_the_last()
    {
        // From the second pixel of a dash to the first of one: the first and last runs are cut.
        var shot = Column("##...###...###...###...#");

        Assert.Empty(ShellDriver.DashedPixelProblems(shot, 0, 0, 23, Lane, Background, minimumDashes: 5));
        ShellDriver.AssertDashedPixels(shot, 0, 0, 23, Lane, Background, 5, "the line");
        Assert.Equal(
            ["There are 5 dashes, fewer than 6.", "The runs: dash 0-1, gap 2-4, dash 5-7, gap 8-10, dash 11-13, gap 14-16, dash 17-19, gap 20-22, dash 23-23"],
            ShellDriver.DashedPixelProblems(shot, 0, 0, 23, Lane, Background, minimumDashes: 6));
    }

    [AvaloniaFact]
    public void Runs_of_two_and_four_pass_and_starts_five_to_seven_apart_pass()
    {
        // A dash of 4 then a gap of 2, a dash of 3 then a gap of 4: the starts 6 and 7 apart.
        var shot = Column("...####..###....###..##");

        Assert.Empty(ShellDriver.DashedPixelProblems(shot, 0, 0, 22, Lane, Background, minimumDashes: 4));
    }

    [AvaloniaFact]
    public void Runs_too_long_or_too_short_and_dashes_too_far_apart_are_named()
    {
        Assert.Contains(
            "The dash from y 3 to 7 is 5 pixels long, not 2 to 4.",
            ShellDriver.DashedPixelProblems(Column("...#####...###...#"), 0, 0, 17, Lane, Background, minimumDashes: 1));
        Assert.Contains(
            "The gap from y 3 to 3 is 1 pixels long, not 2 to 4.",
            ShellDriver.DashedPixelProblems(Column("###.###..###"), 0, 0, 11, Lane, Background, minimumDashes: 1));
        Assert.Contains(
            "The dashes at y 8 and 16 start 8 pixels apart, not 6 ± 1.",
            ShellDriver.DashedPixelProblems(Column("####....####....####....###"), 0, 0, 26, Lane, Background, minimumDashes: 1));
    }

    [AvaloniaFact]
    public void The_first_and_the_last_run_may_be_cut_short_but_not_longer_than_a_whole_one()
    {
        Assert.Contains(
            "The dash from y 0 to 6 is 7 pixels long, more than 4.",
            ShellDriver.DashedPixelProblems(Column("#######...###...###...##"), 0, 0, 23, Lane, Background, minimumDashes: 2));
        Assert.Contains(
            "The dash from y 0 to 13 is 14 pixels long, more than 4.",
            ShellDriver.DashedPixelProblems(Column("##############...###"), 0, 0, 19, Lane, Background, minimumDashes: 2));
        Assert.Contains(
            "The dash from y 12 to 19 is 8 pixels long, more than 4.",
            ShellDriver.DashedPixelProblems(Column("###...###...########"), 0, 0, 19, Lane, Background, minimumDashes: 2));

        // A line missing at the range's top for longer than a gap.
        Assert.Contains(
            "The gap from y 0 to 4 is 5 pixels long, more than 4.",
            ShellDriver.DashedPixelProblems(Column(".....###...###...###"), 0, 0, 19, Lane, Background, minimumDashes: 2));

        // FL0's beta stash (F15 step 2, 14 pixels) with the stash row's line drawn solid and only
        // the next row's dashed: two dashes, one start, yet not dashed.
        Assert.Equal(
            ["The dash from y 0 to 5 is 6 pixels long, more than 4.", "The runs: dash 0-5, gap 6-7, dash 8-10, gap 11-13"],
            ShellDriver.DashedPixelProblems(Column("######..###..."), 0, 0, 13, Lane, Background, minimumDashes: 2));
    }

    [AvaloniaFact]
    public void A_solid_line_or_no_line_at_all_is_not_dashed()
    {
        var solid = Column(new string('#', 40));
        var none = Column(new string('.', 40));

        Assert.Equal(
            ["The dash from y 0 to 39 is 40 pixels long, more than 4.", "There are 1 dashes, fewer than 5.", "The runs: dash 0-39"],
            ShellDriver.DashedPixelProblems(solid, 0, 0, 39, Lane, Background, minimumDashes: 5));
        Assert.Equal(
            ["The gap from y 0 to 39 is 40 pixels long, more than 4.", "There are 0 dashes, fewer than 5.", "The runs: gap 0-39"],
            ShellDriver.DashedPixelProblems(none, 0, 0, 39, Lane, Background, minimumDashes: 5));

        // Even with no minimum, the one run is longer than a dash or a gap.
        Assert.NotEmpty(ShellDriver.DashedPixelProblems(solid, 0, 0, 39, Lane, Background, minimumDashes: 0));
        var thrown = Assert.Throws<InvalidOperationException>(() => ShellDriver.AssertDashedPixels(solid, 0, 0, 39, Lane, Background, 5, "lane 0"));
        Assert.StartsWith("lane 0 at x 0 from y 0 to 39 is not dashed in #ff7c8cff: The dash from y 0 to 39 is 40 pixels long, more than 4. There are 1 dashes, fewer than 5.", thrown.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void A_pixel_is_a_dash_pixel_when_it_is_nearer_to_the_lanes_colour_than_to_the_background()
    {
        // The middle pixel of each run of 3 is a mix: 60 % of the lane's colour reads as a dash,
        // 40 % as a gap, so the pattern is still threes.
        var mostlyLane = Mix(0.6);
        var mostlyBackground = Mix(0.4);
        var shot = Column(
            [Lane, Lane, Background, mostlyBackground, Background, Lane, mostlyLane, Lane, Background, Background, Background, Lane]);

        Assert.Empty(ShellDriver.DashedPixelProblems(shot, 0, 0, 11, Lane, Background, minimumDashes: 3));
    }

    [AvaloniaFact]
    public void A_solid_line_is_the_lanes_colour_within_three_per_channel()
    {
        var near = Color.FromRgb((byte)(Lane.R - 3), (byte)(Lane.G + 3), Lane.B);
        var off = Color.FromRgb((byte)(Lane.R - 4), Lane.G, Lane.B);
        var shot = Column([Lane, near, Lane, off, Lane]);

        Assert.Empty(ShellDriver.SolidPixelProblems(shot, 0, 0, 2, Lane));
        Assert.Equal(["y 3 is #ff788cff."], ShellDriver.SolidPixelProblems(shot, 0, 0, 4, Lane));
        ShellDriver.AssertSolidPixels(shot, 0, 0, 2, Lane, "lane 0");
        var thrown = Assert.Throws<InvalidOperationException>(() => ShellDriver.AssertSolidPixels(shot, 0, 1, 4, Lane, "lane 0"));
        Assert.Equal("lane 0 at x 0 from y 1 to 4 is not solid #ff7c8cff: y 3 is #ff788cff.", thrown.Message);
    }

    [AvaloniaFact]
    public async Task In_the_apps_picture_of_the_graph_scenario_the_stash_line_reads_dashed_and_the_lanes_below_it_solid()
    {
        // F15 step 1's positions (report part F, GS0) in the app's window at 1100×700, dark.
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.MoveToNeutral();
        var shot = app.Capture();
        var lane1 = Color.Parse("#3FB97F");

        ShellDriver.AssertDashedPixels(shot, 406, 136, 175, Lane, Background, 5, "the stash's line");
        ShellDriver.AssertSolidPixels(shot, 406, 187, 219, Lane, "lane 0 below Add settings page");
        ShellDriver.AssertSolidPixels(shot, 422, 161, 219, lane1, "lane 1 below Highlight matches");

        // Each helper tells the two apart.
        Assert.NotEmpty(ShellDriver.DashedPixelProblems(shot, 406, 187, 219, Lane, Background, minimumDashes: 5));
        Assert.NotEmpty(ShellDriver.SolidPixelProblems(shot, 406, 136, 175, Lane));
    }

    /// <summary>A screenshot one pixel wide: '#' a pixel in the lane's colour, '.' one in the background's, top to bottom.</summary>
    private static Screenshot Column(string pixels) =>
        Column(pixels.Select(pixel => pixel == '#' ? Lane : Background).ToArray());

    private static Screenshot Column(Color[] pixels)
    {
        var bitmap = new WriteableBitmap(new PixelSize(1, pixels.Length), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
        {
            for (var y = 0; y < pixels.Length; y++)
            {
                var pixel = pixels[y];
                Marshal.Copy(new[] { pixel.B, pixel.G, pixel.R, pixel.A }, 0, buffer.Address + (y * buffer.RowBytes), 4);
            }
        }

        return new Screenshot(bitmap);
    }

    /// <summary>The lane's colour at <paramref name="share"/> over the background.</summary>
    private static Color Mix(double share)
    {
        static byte Channel(byte lane, byte background, double share) => (byte)Math.Round((share * lane) + ((1 - share) * background));
        return Color.FromRgb(Channel(Lane.R, Background.R, share), Channel(Lane.G, Background.G, share), Channel(Lane.B, Background.B, share));
    }
}
