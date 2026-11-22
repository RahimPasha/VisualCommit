using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The left panel's analogue of <see cref="StageListScrollTests"/>: a hide, whose rebuild keeps the
/// number of rows of the scrolled branch list and marks one branch's row, leaves every row in view
/// at its place in the window, and every row but the hidden branch's with its own object and its
/// own control, so that a press on such a row is still a click when its release comes after the
/// rebuild. The rows are matched by their key, before and after. The list holds the section
/// headers, 30 px high, and 26 px rows: the local branches b01 on and main.
/// <para>
/// A hide above the view or lower in it also leaves the list's offset as it was, and at the bottom
/// its extent. A hide of the top row in view moves the offset by that row's height, and the test
/// allows no more: nothing moves in the window, but the row's Replace leaves the panel's first slot
/// empty, which Avalonia 12.1.3 counts as no height, so the rows below it are placed a row higher
/// in the list and the scroll viewer moves the offset with them (<c>RowCollection.ReplaceChanged</c>).
/// A remove and an insert would leave the offset a few pixels off near the top of the list, but
/// deep in it the panel estimates the top row from the rows' mean height, misses by rows with
/// headers in view, and makes the controls in view again, the click under a press lost with them.
/// </para>
/// </summary>
public class LeftPanelScrollTests
{
    private const double RowHeight = 26;

    [AvaloniaFact]
    public async Task Hiding_a_branch_above_the_view_of_a_left_panel_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        // b01 lies far above the view; b45 to b60, main and four headers are in view.
        await AssertHideKeepsTheListAsync(
            branches: 60,
            offsetOf: Bottom,
            hidden: _ => "b01",
            pressed: 4,
            topRowInView: false);
    }

    [AvaloniaFact]
    public async Task Hiding_a_branch_in_the_middle_of_the_view_of_a_left_panel_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        await AssertHideKeepsTheListAsync(
            branches: 60,
            offsetOf: Bottom,
            hidden: inView => inView[8].Text,
            pressed: 12,
            topRowInView: false);
    }

    [AvaloniaFact]
    public async Task Hiding_the_top_branch_in_view_of_a_left_panel_scrolled_to_its_bottom_leaves_every_other_row_in_view_and_the_click_under_a_press_as_they_were()
    {
        await AssertHideKeepsTheListAsync(
            branches: 60,
            offsetOf: Bottom,
            hidden: inView => inView[0].Text,
            pressed: 4,
            topRowInView: true);
    }

    [AvaloniaFact]
    public async Task Hiding_the_top_branch_in_view_of_a_left_panel_scrolled_near_its_top_leaves_every_other_row_in_view_as_it_was()
    {
        // The header (30) and b01, b02 (26 each) above; b03 is the top row in view, 8 px of it hidden.
        await AssertHideKeepsTheListAsync(
            branches: 35,
            offsetOf: _ => 90,
            hidden: inView => inView[0].Text,
            pressed: null,
            topRowInView: true);
    }

    /// <summary>
    /// The app on a repository with the local branches b01 to b<paramref name="branches"/> and main,
    /// its left panel scrolled to <paramref name="offsetOf"/>; a press on the row in view at
    /// <paramref name="pressed"/>, if given, then a hide of the branch <paramref name="hidden"/>
    /// names (from the rows in view), the graph without it, and the press's release. Asserts that
    /// every row in view before is in view at the same place in the window, every one but the hidden
    /// branch's the same object with the same button, that the release selected the pressed row,
    /// and that the offset, and at the bottom the extent, are as before; with
    /// <paramref name="topRowInView"/>, that the offset moved by at most the hidden row's height.
    /// </summary>
    private static async Task AssertHideKeepsTheListAsync(
        int branches,
        Func<ScrollViewer, double> offsetOf,
        Func<IReadOnlyList<RefListRow>, string> hidden,
        int? pressed,
        bool topRowInView)
    {
        using var repo = await ManyBranchesAsync(branches);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height + 100, $"The left panel does not scroll: extent {scroller.Extent.Height}, view {scroller.Viewport.Height}.");
        scroller.Offset = new Vector(0, offsetOf(scroller));
        app.Settle();
        var offset = scroller.Offset.Y;
        var extent = scroller.Extent.Height;
        var atBottom = offset + scroller.Viewport.Height >= extent;
        Assert.True(offset > 0, "The left panel is not scrolled.");

        var before = InView(app, list, scroller);
        var name = hidden([.. before.Select(entry => entry.Row)]);
        var hiddenKey = Branch(repository, name).Key;
        Assert.True(topRowInView == (before[0].Row.Key == hiddenKey), $"{name} is {(topRowInView ? "not the top row in view" : "the top row in view")}: {Describe(before)}.");
        var hiddenHeight = topRowInView ? before[0].Button.Bounds.Height : 0;
        Assert.True(!topRowInView || hiddenHeight == RowHeight, $"{name}'s row is {hiddenHeight} px high.");

        (RefItemRow Row, Point Point)? press = null;
        if (pressed is { } index)
        {
            var entry = before[index];
            Assert.True(entry.Row is RefItemRow { Kind: RefItemKind.LocalBranch } && entry.Row.Key != hiddenKey, $"Row {index} in view, {entry.Row.Text}, is no other local branch.");
            press = ((RefItemRow)entry.Row, app.BoundsOf(entry.Button).Center);
            app.PressMouse(press.Value.Point);
        }

        await repository.Operations.HideBranchAsync("refs/heads/" + name);
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(Branch(repository, name).Marks.IsHidden, $"{name}'s row is not marked hidden.");

        var after = InView(app, list, scroller).ToDictionary(entry => entry.Row.Key, StringComparer.Ordinal);
        var problems = new List<string>();
        var moved = Math.Abs(scroller.Offset.Y - offset);
        if (topRowInView ? moved > hiddenHeight : moved != 0)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {scroller.Offset.Y} for {offset}"));
        }

        if (!topRowInView && atBottom && scroller.Extent.Height != extent)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"extent {scroller.Extent.Height} for {extent}"));
        }

        foreach (var (row, y, button) in before)
        {
            if (!after.TryGetValue(row.Key, out var now))
            {
                problems.Add($"{row.Text} is no longer in view");
                continue;
            }

            if (now.Y != y)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{row.Text} at y {now.Y} for {y}"));
            }

            if (row.Key == hiddenKey)
            {
                continue;
            }

            if (!ReferenceEquals(now.Row, row))
            {
                problems.Add($"{row.Text} is another row");
            }

            if (!ReferenceEquals(now.Button, button))
            {
                problems.Add($"{row.Text} has another button");
            }
        }

        if (press is { } held)
        {
            app.ReleaseMouse(held.Point);
            await app.WaitForIdleAsync();
            var selected = repository.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Text).ToList();
            if (selected is not [var only] || only != held.Row.Text)
            {
                problems.Add($"the press on {held.Row.Text} selected {(selected.Count == 0 ? "nothing" : string.Join(", ", selected))}");
            }
        }

        Assert.True(
            problems.Count == 0,
            string.Create(CultureInfo.InvariantCulture, $"After the hide of {name}: {string.Join("; ", problems)}. Offset {scroller.Offset.Y} for {offset}, extent {scroller.Extent.Height} for {extent}. In view before: {Describe(before)}; after: {Describe([.. after.Values.OrderBy(entry => entry.Y)])}."));
    }

    /// <summary>The list scrolled to its last row.</summary>
    private static double Bottom(ScrollViewer scroller) => scroller.Extent.Height - scroller.Viewport.Height;

    /// <summary>The local branch <paramref name="name"/>'s row.</summary>
    private static RefItemRow Branch(RepositoryViewModel repository, string name) =>
        repository.LeftPanel.Rows.OfType<RefItemRow>().Single(row => row.Kind == RefItemKind.LocalBranch && row.Text == name);

    /// <summary>The rows that meet the list's view, top to bottom: each one's row, its place in the window and its button.</summary>
    private static List<(RefListRow Row, double Y, Button Button)> InView(ShellDriver app, ItemsControl list, ScrollViewer scroller)
    {
        var view = app.BoundsOf(scroller);
        return [.. list.GetRealizedContainers()
            .Where(container => container.IsEffectivelyVisible)
            .Select(container => container.GetVisualDescendants().OfType<Button>().First())
            .Where(button => button.DataContext is RefListRow)
            .Select(button => (Row: (RefListRow)button.DataContext!, app.BoundsOf(button).Y, button.Bounds.Height, Button: button))
            .Where(entry => entry.Y + entry.Height > view.Top && entry.Y < view.Bottom)
            .OrderBy(entry => entry.Y)
            .Select(entry => (entry.Row, entry.Y, entry.Button))];
    }

    private static string Describe(List<(RefListRow Row, double Y, Button Button)> rows) =>
        string.Join(", ", rows.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Row.Text}@{entry.Y}")));

    /// <summary>A repository with one commit on main and the branches b01 to b<paramref name="branches"/> at it.</summary>
    private static async Task<TempRepo> ManyBranchesAsync(int branches)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            var head = await repo.CommitFileAsync("a.txt", "a\n", "Add a.txt");
            var refs = new StringBuilder();
            for (var number = 1; number <= branches; number++)
            {
                refs.Append(CultureInfo.InvariantCulture, $"create refs/heads/b{number:D2} {head}\n");
            }

            await repo.GitWithInputAsync(refs.ToString(), "update-ref", "--stdin");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
