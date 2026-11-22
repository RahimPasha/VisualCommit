using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The left panel's analogue of <see cref="StageListScrollTests"/>: a rebuild that keeps the number
/// of rows of the scrolled branch list, a hide's marks on the top branch in view, leaves every row
/// it keeps in view at its place in the window with its control, and moves the list's offset and
/// extent by less than the hidden branch's row. The list holds the section headers, 30 px high, and
/// 26 px rows: the local branches b01 to b35 and main. A replaced top row moved them by that whole
/// row (26 px, the extent 30). Its row removed and inserted again, the panel estimates the top row's
/// place from the heights of the rows it has made, 26 px each, which misses the header's 4 px above
/// them: the offset moves by those 4 px (the extent by 8), as it did before the rows between two
/// changed ones were kept (48f403c), while what the window shows stays where it was. No order of
/// the list's notifications keeps the offset exact in Avalonia 12.1.3 (RowCollection.ReplaceChanged).
/// </summary>
public class LeftPanelScrollTests
{
    private const int Branches = 35;

    [AvaloniaFact]
    public async Task Hiding_the_top_branch_in_view_of_a_scrolled_left_panel_moves_neither_the_rows_in_view_nor_the_offset_by_a_row()
    {
        using var repo = await ManyBranchesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height + 100, $"The left panel does not scroll: extent {scroller.Extent.Height}, view {scroller.Viewport.Height}.");

        // The header (30) and b01, b02 (26 each) above; b03 is the top row in view, 8 px of it hidden.
        const double offset = 90;
        scroller.Offset = new Vector(0, offset);
        app.Settle();
        Assert.Equal(offset, scroller.Offset.Y);
        var extent = scroller.Extent.Height;
        var before = InView(app, list, scroller);
        Assert.Equal("b03", before[0].Row.Text);

        await repository.Operations.HideBranchAsync("refs/heads/b03");
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(repository.LeftPanel.Rows.OfType<RefItemRow>().Single(row => row.Text == "b03").Marks.IsHidden, "b03's row is not marked hidden.");

        var after = InView(app, list, scroller);
        var hiddenRow = before[0].Button.Bounds.Height;
        Assert.Equal(26, hiddenRow);
        var problems = new List<string>();
        if (Math.Abs(scroller.Offset.Y - offset) >= hiddenRow)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {scroller.Offset.Y} for {offset}"));
        }

        if (Math.Abs(scroller.Extent.Height - extent) >= hiddenRow)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"extent {scroller.Extent.Height} for {extent}"));
        }

        foreach (var (row, y, button) in before.Where(entry => repository.LeftPanel.Rows.Contains(entry.Row)))
        {
            var now = after.FirstOrDefault(entry => ReferenceEquals(entry.Row, row));
            if (now.Row is null)
            {
                problems.Add($"{row.Text} is no longer in view");
                continue;
            }

            if (now.Y != y)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{row.Text} at y {now.Y} for {y}"));
            }

            if (!ReferenceEquals(now.Button, button))
            {
                problems.Add($"{row.Text} has another button");
            }
        }

        Assert.True(problems.Count == 0, $"After the hide: {string.Join("; ", problems)}. In view before: {Describe(before)}; after: {Describe(after)}.");
    }

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

    /// <summary>A repository with one commit on main and the branches b01 to b35 at it.</summary>
    private static async Task<TempRepo> ManyBranchesAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            var head = await repo.CommitFileAsync("a.txt", "a\n", "Add a.txt");
            var refs = new StringBuilder();
            for (var number = 1; number <= Branches; number++)
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
