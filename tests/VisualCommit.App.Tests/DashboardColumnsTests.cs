using VisualCommit.App.Views.Workspaces;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The dashboard's column layout (docs/phase-6-design.md 2.6, D206): pure.</summary>
public class DashboardColumnsTests
{
    [Fact]
    public void At_1100_the_folder_is_hidden_and_operation_takes_the_rest()
    {
        var columns = DashboardColumns.Compute(1100);

        Assert.False(columns.ShowsFolder);
        AssertSpan(columns, DashboardColumn.Repository, 0, 132);
        AssertSpan(columns, DashboardColumn.Branch, 132, 282);
        AssertSpan(columns, DashboardColumn.AheadBehind, 282, 412);
        AssertSpan(columns, DashboardColumn.Changes, 412, 632);
        AssertSpan(columns, DashboardColumn.State, 632, 812);
        AssertSpan(columns, DashboardColumn.Operation, 812, 1100);
        Assert.Equal(288, columns[DashboardColumn.Operation].Width);
    }

    [Fact]
    public void At_1920_the_folder_shows_and_operation_is_460()
    {
        var columns = DashboardColumns.Compute(1920);

        Assert.True(columns.ShowsFolder);
        AssertSpan(columns, DashboardColumn.Repository, 0, 132);
        AssertSpan(columns, DashboardColumn.Folder, 132, 780);
        AssertSpan(columns, DashboardColumn.Branch, 780, 930);
        AssertSpan(columns, DashboardColumn.AheadBehind, 930, 1060);
        AssertSpan(columns, DashboardColumn.Changes, 1060, 1280);
        AssertSpan(columns, DashboardColumn.State, 1280, 1460);
        AssertSpan(columns, DashboardColumn.Operation, 1460, 1920);
    }

    [Theory]
    [InlineData(1279, false, 0, 467)]
    [InlineData(1280, true, 168, 300)]
    [InlineData(1440, true, 248, 380)]
    [InlineData(1000, false, 0, 188)]
    [InlineData(900, false, 0, 180)]
    public void The_folder_shows_from_1280_wide(double width, bool showsFolder, double folder, double operation)
    {
        var columns = DashboardColumns.Compute(width);

        Assert.Equal(showsFolder, columns.ShowsFolder);
        Assert.Equal(folder, columns[DashboardColumn.Folder].Width);
        Assert.Equal(operation, columns[DashboardColumn.Operation].Width);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1100)]
    [InlineData(1279)]
    [InlineData(1280)]
    [InlineData(1440)]
    [InlineData(1920)]
    [InlineData(2560)]
    public void The_columns_leave_no_gaps(double width)
    {
        var columns = DashboardColumns.Compute(width);
        var right = 0.0;
        foreach (var column in Enum.GetValues<DashboardColumn>())
        {
            var span = columns[column];
            if (!span.IsVisible)
            {
                Assert.Equal(0, span.Width);
                continue;
            }

            Assert.Equal(right, span.X);
            right = span.Right;
        }

        Assert.Equal(Math.Max(width, DashboardColumns.FixedWidth + DashboardColumns.MinOperation), right);
    }

    [Fact]
    public void Two_layouts_of_one_width_are_equal()
    {
        Assert.Equal(DashboardColumns.Compute(1100), DashboardColumns.Compute(1100));
        Assert.NotEqual(DashboardColumns.Compute(1100), DashboardColumns.Compute(1920));
    }

    private static void AssertSpan(DashboardColumns columns, DashboardColumn column, double left, double right)
    {
        var span = columns[column];
        Assert.True(span.IsVisible, $"{column} is shown");
        Assert.Equal((left, right), (span.X, span.Right));
    }
}
