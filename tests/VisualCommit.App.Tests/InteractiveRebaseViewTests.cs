using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the interactive rebase's list as drawn (docs/phase-4-design.md 2.2, 7.4;
/// docs/test-reports/phase-4.md part B, "The list's geometry"): the view laid over the real
/// window's graph area at 1100×700, dark, so that every position is the report's window position.
/// </summary>
public class InteractiveRebaseViewTests
{
    private static readonly Color DarkSelection = Color.Parse("#2A3150");
    private static readonly Color DarkSecondary = Color.Parse("#9BA3B0");

    [AvaloniaFact]
    public async Task The_list_opens_as_L0_with_the_reports_geometry()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        TestPictures.Save(harness.App, "rebase-list-l0");
        var list = harness.Snapshot();

        Assert.Equal(new Rect(260, 88, 440, 586), list.Bounds);
        Assert.Equal(new Rect(260, 88, 440, 36), list.HeaderBounds);
        Assert.Equal(new Rect(260, 630, 440, 44), list.FooterBounds);
        Assert.Equal(("Interactive rebase of main from 4b4622c", "6 commits", true), (list.Header, list.Count, list.CountShown));
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], list.Subjects);
        for (var index = 0; index < list.Rows.Count; index++)
        {
            var row = list.Rows[index];
            Assert.Equal(new Rect(260, 124 + (30 * index), 440, 30), row.Bounds);
            Assert.Equal("Pick", row.ActionText);
            Assert.Equal(new Rect(288, 128 + (30 * index), 84, 22), row.ActionButtonBounds);
            Assert.Equal(380, row.ShaBounds!.Value.X);
            Assert.Equal(64, row.ShaBounds!.Value.Width);
            Assert.Equal(452, row.SubjectBounds!.Value.X);
            Assert.Null(row.AuthorBounds);
            Assert.Equal(DarkSecondary, row.ShaColor);
            Assert.Equal(DarkSecondary, row.GripColor);
            Assert.Equal(new Rect(265, 132 + (30 * index), 14, 14), row.GripBounds);
        }

        Assert.Equal(new Rect(260, 304, 440, 30), list.BaseRow!.Bounds);
        Assert.Equal(DarkSelection, list.Rows[0].Background);
        Assert.True(list.Rows[0].HasKeyboardFocus);
        Assert.False(list.CanStart);
        Assert.Equal("Change an action or the order first", list.StartToolTip);
        Assert.Equal(688, list.StartBounds.Right);
        Assert.Equal(8, list.StartBounds.X - list.CancelBounds.Right);
    }
}
