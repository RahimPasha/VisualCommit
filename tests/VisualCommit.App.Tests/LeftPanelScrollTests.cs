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
/// The left panel's analogue of <see cref="StageListScrollTests"/>: a hide, a solo or its end, whose
/// rebuild keeps the number of rows of the scrolled branch list and changes what some branch rows
/// show, leaves the list where it was: its offset and its extent stay, and every row in view stays
/// at its place in the window, its own object with its own control, so that a press on such a row
/// is still a click when its release comes after the rebuild. The rows are matched by their key,
/// before and after. The list holds the section headers, 30 px high, and 26 px rows: the local
/// branches b01 on and main.
/// <para>
/// A solo dims every branch row but the soloed one and HEAD's, its end every such row again. When
/// such rows were replaced, Avalonia 12.1.3 counted each replaced slot at the top of the view as no
/// height, so the first row kept below them was placed where the top row in view had been, and the
/// scroll viewer moved the offset by all their heights: under a smaller offset the list went to its
/// top and made every row's control again, the click under a press lost with them; under a larger
/// one the offset and the extent ended those rows short. The branch rows now take what their rebuilt
/// rows show in place, and the list is told of no row (<c>RowCollection.ReplaceChanged</c>).
/// </para>
/// </summary>
public class LeftPanelScrollTests
{
    [AvaloniaFact]
    public async Task Hiding_a_branch_above_the_view_of_a_left_panel_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        // b01 lies far above the view; b45 to b60, main and four headers are in view.
        await AssertChangeKeepsTheListAsync(
            branches: 60,
            offset: null,
            change: inView =>
            {
                Assert.DoesNotContain(inView, row => row.Text == "b01");
                return Hide("b01");
            },
            pressed: inView => inView[4]);
    }

    [AvaloniaFact]
    public async Task Hiding_a_branch_in_the_middle_of_the_view_of_a_left_panel_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        await AssertChangeKeepsTheListAsync(
            branches: 60,
            offset: null,
            change: inView => Hide(inView[8].Text),
            pressed: inView => inView[12]);
    }

    [AvaloniaFact]
    public async Task Hiding_the_top_branch_in_view_of_a_left_panel_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        await AssertChangeKeepsTheListAsync(
            branches: 60,
            offset: null,
            change: inView => Hide(inView[0].Text),
            pressed: inView => inView[4]);
    }

    [AvaloniaFact]
    public async Task Hiding_the_top_branch_in_view_of_a_left_panel_scrolled_near_its_top_leaves_the_list_as_it_was()
    {
        // The header (30) and b01, b02 (26 each) above; b03 is the top row in view, 8 px of it hidden.
        await AssertChangeKeepsTheListAsync(
            branches: 35,
            offset: 90,
            change: inView => Hide(inView[0].Text),
            pressed: null);
    }

    [AvaloniaFact]
    public async Task Soloing_a_branch_while_the_left_panel_is_scrolled_by_less_than_the_rows_it_dims_above_main_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        // b01 to b20 are dimmed but b05; under an offset of 40 they are the top rows in view.
        await AssertChangeKeepsTheListAsync(
            branches: 20,
            offset: 40,
            change: _ => Solo("b05"),
            pressed: MainRow);
    }

    [AvaloniaFact]
    public async Task Soloing_a_branch_while_the_left_panel_is_scrolled_by_a_hundred_pixels_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        await AssertChangeKeepsTheListAsync(
            branches: 20,
            offset: 100,
            change: _ => Solo("b05"),
            pressed: MainRow);
    }

    [AvaloniaFact]
    public async Task Soloing_a_branch_while_the_left_panel_is_scrolled_to_its_bottom_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        // b45 to b60 in view are dimmed, as are b01 to b44 above them, all but b30.
        await AssertChangeKeepsTheListAsync(
            branches: 60,
            offset: null,
            change: _ => Solo("b30"),
            pressed: MainRow);
    }

    [AvaloniaFact]
    public async Task Ending_a_solo_while_the_left_panel_is_scrolled_near_its_top_leaves_the_list_and_the_click_under_a_press_as_they_were()
    {
        await AssertChangeKeepsTheListAsync(
            branches: 20,
            offset: 40,
            change: _ => StopSoloing("b05"),
            pressed: MainRow,
            first: Solo("b05"));
    }

    /// <summary>
    /// The app on a repository with the local branches b01 to b<paramref name="branches"/> and main,
    /// after <paramref name="first"/> if given; its left panel scrolled to <paramref name="offset"/>, or
    /// to its bottom without one;
    /// a press on the row in view that <paramref name="pressed"/> picks, if any, then the change that
    /// <paramref name="change"/> makes of the rows in view, the graph that follows it, and the press's
    /// release. Asserts that the offset and the extent are as before, that every row in view before is
    /// in view at the same place in the window, the same object with the same button, and that the
    /// release selected the pressed row.
    /// </summary>
    private static async Task AssertChangeKeepsTheListAsync(
        int branches,
        double? offset,
        Func<IReadOnlyList<RefListRow>, FilterChange> change,
        Func<IReadOnlyList<RefListRow>, RefListRow>? pressed,
        FilterChange? first = null)
    {
        using var repo = await ManyBranchesAsync(branches);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        if (first is not null)
        {
            await ApplyAsync(app, repository, first);
        }

        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        var wanted = offset ?? Bottom(scroller);
        scroller.Offset = new Vector(0, wanted);
        app.Settle();

        // The panel estimates the rows below the view from the heights of the rows it made the time
        // before, those in view before the scroll; a second layout bases that on the rows in view
        // now, as any later layout of the list would, so the extent read is the one a change must
        // keep, rather than one that the next layout refines by a few pixels.
        list.ItemsPanelRoot!.InvalidateMeasure();
        app.Settle();
        var offsetBefore = scroller.Offset.Y;
        var extent = scroller.Extent.Height;
        Assert.True(offsetBefore > 0, $"The left panel is not scrolled: extent {extent}, view {scroller.Viewport.Height}.");
        Assert.True(offset is null || offsetBefore == wanted, string.Create(CultureInfo.InvariantCulture, $"The left panel scrolls to {offsetBefore}, not to {wanted}: extent {extent}, view {scroller.Viewport.Height}."));

        var before = InView(app, list, scroller);
        var rowsInView = before.Select(entry => entry.Row).ToList();
        var made = change(rowsInView);
        (RefItemRow Row, Point Point)? press = null;
        if (pressed?.Invoke(rowsInView) is { } row)
        {
            Assert.True(row is RefItemRow { Kind: RefItemKind.LocalBranch }, $"{row.Text} is no local branch.");
            press = ((RefItemRow)row, app.BoundsOf(before.Single(entry => ReferenceEquals(entry.Row, row)).Button).Center);
            app.PressMouse(press.Value.Point);
        }

        await ApplyAsync(app, repository, made);

        var after = InView(app, list, scroller).ToDictionary(entry => entry.Row.Key, StringComparer.Ordinal);
        var problems = new List<string>();
        if (scroller.Offset.Y != offsetBefore)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {scroller.Offset.Y} for {offsetBefore}"));
        }

        if (scroller.Extent.Height != extent)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"extent {scroller.Extent.Height} for {extent}"));
        }

        foreach (var (shown, y, button) in before)
        {
            if (!after.TryGetValue(shown.Key, out var now))
            {
                problems.Add($"{shown.Text} is no longer in view");
                continue;
            }

            if (now.Y != y)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{shown.Text} at y {now.Y} for {y}"));
            }

            if (!ReferenceEquals(now.Row, shown))
            {
                problems.Add($"{shown.Text} is another row");
            }

            if (!ReferenceEquals(now.Button, button))
            {
                problems.Add($"{shown.Text} has another button");
            }
        }

        if (press is { } held)
        {
            app.ReleaseMouse(held.Point);
            await app.WaitForIdleAsync();
            var selected = repository.LeftPanel.Rows.OfType<RefItemRow>().Where(item => item.IsSelected).Select(item => item.Text).ToList();
            if (selected is not [var only] || only != held.Row.Text)
            {
                problems.Add($"the press on {held.Row.Text} selected {(selected.Count == 0 ? "nothing" : string.Join(", ", selected))}");
            }
        }

        Assert.True(
            problems.Count == 0,
            string.Create(CultureInfo.InvariantCulture, $"After {made.Description}: {string.Join("; ", problems)}. Offset {scroller.Offset.Y} for {offsetBefore}, extent {scroller.Extent.Height} for {extent}. In view before: {Describe(before)}; after: {Describe([.. after.Values.OrderBy(entry => entry.Y)])}."));
    }

    /// <summary>Makes <paramref name="change"/>, waits for the graph that follows it and checks that the panel shows it.</summary>
    private static async Task ApplyAsync(ShellDriver app, RepositoryViewModel repository, FilterChange change)
    {
        await change.Make(repository);
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(change.Shown(repository), $"The left panel does not show {change.Description}.");
    }

    /// <summary>A change of what the graph shows, what it is called, and whether the left panel's rows show it.</summary>
    private sealed record FilterChange(string Description, Func<RepositoryViewModel, Task> Make, Func<RepositoryViewModel, bool> Shown);

    private static FilterChange Hide(string name) => new(
        $"the hide of {name}",
        repository => repository.Operations.HideBranchAsync("refs/heads/" + name),
        repository => Branch(repository, name).Marks.IsHidden);

    private static FilterChange Solo(string name) => new(
        $"the solo of {name}",
        repository => repository.Operations.SoloBranchAsync("refs/heads/" + name),
        repository => Branch(repository, name).Marks.IsSoloed
            && repository.LeftPanel.Rows.OfType<RefItemRow>().Count(row => row.Kind == RefItemKind.LocalBranch && row.IsDimmed) > 1);

    private static FilterChange StopSoloing(string name) => new(
        $"the end of {name}'s solo",
        repository => repository.Operations.StopSoloingAsync(),
        repository => repository.LeftPanel.Rows.OfType<RefItemRow>().All(row => row.Marks == RefRowMarks.None));

    /// <summary>The list scrolled to its last row.</summary>
    private static double Bottom(ScrollViewer scroller) => scroller.Extent.Height - scroller.Viewport.Height;

    /// <summary>main's row, among the rows in view: HEAD's branch, which a solo leaves as it is.</summary>
    private static RefListRow MainRow(IReadOnlyList<RefListRow> inView) =>
        inView.Single(row => row is RefItemRow { Kind: RefItemKind.LocalBranch, Text: "main" });

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
