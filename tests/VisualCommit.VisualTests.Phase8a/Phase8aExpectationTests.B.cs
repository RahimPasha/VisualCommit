using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part B's expectations (docs/test-reports/phase-8a.md, "Conventions of part B"; design 7.4):
/// <c>Phase8aExpectations.B</c>'s lists equal <see cref="IconFile"/>'s, every picture name matches
/// the phase's pattern (design 0.3) and is the report's, and the sheet's rows fit 1100×700.
/// </summary>
public sealed partial class Phase8aExpectationTests
{
    [Fact]
    public void Part_B_s_lists_are_the_icon_files_lists()
    {
        Assert.Equal(IconFile.IcoSizes, B.IcoSizes);
        Assert.Equal(IconFile.PngSizes, B.PngSizes);
        Assert.Equal(IconFile.IconSetNames.Select(file => file.Name), B.IconSetNames);
        Assert.Equal(IconFile.AppImageSizes, B.AppImageSizes);
        Assert.Equal(IconFile.IcoFile, B.IcoFile);

        // Row 2: Linux's 16 to 128, then the .iconset's 16, 32 and 128 (7.5).
        Assert.Equal(
            [
                (IconFile.PngFile(16), 16), (IconFile.PngFile(24), 24), (IconFile.PngFile(32), 32),
                (IconFile.PngFile(48), 48), (IconFile.PngFile(64), 64), (IconFile.PngFile(128), 128),
                (IconFile.IconSetFile("icon_16x16"), 16), (IconFile.IconSetFile("icon_32x32"), 32), (IconFile.IconSetFile("icon_128x128"), 128),
            ],
            B.Row2);
        foreach (var (file, size) in B.Row2.Where(file => file.File.Contains(".iconset/", StringComparison.Ordinal)))
        {
            Assert.Contains((Path.GetFileNameWithoutExtension(file)!, size), IconFile.IconSetNames);
        }
    }

    [Fact]
    public void Part_B_s_picture_names_are_the_report_s_unique_and_follow_the_rule()
    {
        Assert.Equal(["b1-icon-sizes-dark", "b2-icon-sizes-light"], B.Pictures);
        Assert.Equal(
            ["b1-title-bar-dark-with-frame", "b1-title-bar-icon-dark", "b2-title-bar-light-with-frame", "b2-title-bar-icon-light", "b2-taskbar-button", "b2-taskbar-button-enlarged"],
            B.RealWindowPictures);
        var all = B.Pictures.Concat(B.RealWindowPictures).ToList();
        Assert.All(all, name => Assert.Matches(PictureName(), name));
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Part_B_s_sheet_rows_fit_1100_by_700()
    {
        Assert.Equal(LeftsOf(B.IcoSizes), B.Row1Lefts);
        Assert.Equal(B.Row1End, B.Row1Lefts[^1] + B.IcoSizes[^1]);
        Assert.Equal(LeftsOf([.. B.Row2.Select(file => file.Size)]), B.Row2Lefts);
        Assert.Equal(B.Row2End, B.Row2Lefts[^1] + B.Row2[^1].Size);
        Assert.True(B.Row1End <= DefaultSize.Width && B.Row2End <= DefaultSize.Width, "A row is wider than the window.");

        // Top to bottom: the heading, row 1 (its tallest image the 256), the sizes, row 2 (its tallest the 128).
        Assert.True(B.HeadingTop + (2 * B.HeadingFontSize) <= B.Row1Bottom - B.IcoSizes.Max(), "The heading reaches row 1's tallest image.");
        Assert.True(B.SizesTop >= B.Row1Bottom, "The sizes start above row 1's bottom line.");
        Assert.True(B.SizesTop + (2 * B.SizeFontSize) <= B.Row2Bottom - B.Row2.Max(file => file.Size), "Row 2's tallest image reaches the sizes.");
        Assert.True(B.Row2Bottom <= DefaultSize.Height, "Row 2 ends below the window.");
        Assert.True(B.Tolerance == 2 && B.IconTolerance == 2, "The report's tolerance is 2 per channel.");
        Assert.Equal(TimeSpan.FromSeconds(10), B.TaskbarButtonLimit);
        Assert.Contains(B.TaskbarButtonNameStart, B.TaskbarButtonNotRun, StringComparison.Ordinal);
    }

    /// <summary>A row's lefts: the first at <see cref="B.Left"/>, each next one a gap after the image before it.</summary>
    private static List<int> LeftsOf(IReadOnlyList<int> sizes)
    {
        var lefts = new List<int>(sizes.Count);
        var left = B.Left;
        foreach (var size in sizes)
        {
            lefts.Add(left);
            left += size + B.Gap;
        }

        return lefts;
    }
}
