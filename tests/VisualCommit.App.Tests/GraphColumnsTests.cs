using VisualCommit.App.Views.Graph;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The commit graph's column layout (D49). The numbers are copied from the phase 1 test report,
/// "What a repository tab must show", not from the code.
/// </summary>
public class GraphColumnsTests
{
    [Fact]
    public void At_440_wide_with_3_lanes_the_graph_shows_branch_graph_message_and_date()
    {
        var columns = GraphColumns.Compute(440, 3);

        Assert.Equal(new GraphColumn(0, 130, true), columns.Refs);
        Assert.Equal(new GraphColumn(130, 64, true), columns.Graph);
        Assert.Equal(new GraphColumn(194, 126, true), columns.Message);
        Assert.False(columns.ShowsAuthor);
        Assert.True(columns.ShowsDate);
        Assert.False(columns.ShowsSha);
        Assert.Equal(320, columns.Date.X);
        Assert.Equal(120, columns.Date.Width);
        Assert.Equal(0, columns.Author.Width);
        Assert.Equal(0, columns.Sha.Width);
    }

    [Fact]
    public void At_1260_wide_all_six_columns_show_in_order()
    {
        var columns = GraphColumns.Compute(1260, 3);

        Assert.True(columns.ShowsAuthor && columns.ShowsDate && columns.ShowsSha);
        Assert.Equal(734, columns.Message.Width);
        Assert.Equal(
            [(0d, 130d), (130d, 64d), (194d, 734d), (928d, 140d), (1068d, 120d), (1188d, 72d)],
            new[] { columns.Refs, columns.Graph, columns.Message, columns.Author, columns.Date, columns.Sha }.Select(column => (column.X, column.Width)));
        Assert.Equal(1260, columns.Sha.Right);
    }

    [Fact]
    public void At_440_wide_with_2_lanes_the_graph_column_is_its_minimum_48()
    {
        var columns = GraphColumns.Compute(440, 2);

        Assert.Equal(48, columns.Graph.Width);
        Assert.Equal(142, columns.Message.Width);
        Assert.True(columns.ShowsDate);
        Assert.False(columns.ShowsAuthor);
        Assert.False(columns.ShowsSha);
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(1, 48)]
    [InlineData(2, 48)]
    [InlineData(3, 64)]
    [InlineData(5, 96)]
    [InlineData(14, 240)]
    [InlineData(40, 240)]
    public void The_graph_column_is_16_per_lane_plus_16_between_48_and_240(int lanes, double width)
    {
        Assert.Equal(width, GraphColumns.Compute(1260, lanes).Graph.Width);
    }

    [Theory]
    // 130 + 64 + 120 message + 140 + 120 + 72 = 646: everything fits exactly.
    [InlineData(646, true, true, true)]
    // One pixel less: SHA goes first.
    [InlineData(645, true, true, false)]
    // Without SHA: 574 fits exactly; one less hides Author.
    [InlineData(574, true, true, false)]
    [InlineData(573, false, true, false)]
    // Without Author: 434 fits exactly; one less hides Date too.
    [InlineData(434, false, true, false)]
    [InlineData(433, false, false, false)]
    public void Narrow_widths_hide_sha_then_author_then_date(double width, bool author, bool date, bool sha)
    {
        var columns = GraphColumns.Compute(width, 3);

        Assert.Equal(author, columns.ShowsAuthor);
        Assert.Equal(date, columns.ShowsDate);
        Assert.Equal(sha, columns.ShowsSha);
        Assert.True(columns.Message.Width >= GraphColumns.MinMessageWidth);
    }

    [Fact]
    public void The_message_keeps_120_even_when_nothing_else_can_give_way()
    {
        var columns = GraphColumns.Compute(320, 12);

        Assert.Equal(208, columns.Graph.Width);
        Assert.Equal(120, columns.Message.Width);
        Assert.False(columns.ShowsDate);
    }

    [Fact]
    public void Lanes_are_16_apart_from_16_into_the_graph_column()
    {
        var columns = GraphColumns.Compute(440, 3);

        Assert.Equal(146, columns.LaneCenterX(0));
        Assert.Equal(162, columns.LaneCenterX(1));
        Assert.Equal(178, columns.LaneCenterX(2));
    }

    [Fact]
    public void Two_layouts_of_the_same_width_and_lanes_are_equal()
    {
        Assert.Equal(GraphColumns.Compute(700, 4), GraphColumns.Compute(700, 4));
        Assert.NotEqual(GraphColumns.Compute(700, 4), GraphColumns.Compute(700, 5));
    }
}
