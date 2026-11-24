using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The left panel's analogue of <see cref="StageListScrollTests"/>. The list holds the section
/// headers, 30 px high, and 26 px rows: the local branches b01 on and main, and in one fact the
/// remote origin's branches. Every fact but the one that hides the top row in view near the top of
/// the list presses a row in view before the change, or while git makes it, and releases it after
/// the refresh.
/// <para>
/// A hide, a solo or its end, whose rebuild keeps the number of rows of the scrolled branch list and
/// changes what some branch rows show, leaves the list exactly where it was: its offset and its
/// extent stay, and every row in view stays at its place in the window, its own object with its own
/// control, so that the press on such a row is still a click. The rows are matched by their key,
/// before and after. A solo dims every branch row but the soloed one and HEAD's, its end every such
/// row again. When such rows were replaced, Avalonia 12.1.3 counted each replaced slot at the top of
/// the view as no height, so the first row kept below them was placed where the top row in view had
/// been, and the scroll viewer moved the offset by all their heights: under a smaller offset the list
/// went to its top and made every row's control again, the click under a press lost with them; under
/// a larger one the offset and the extent ended those rows short. The branch rows now take what their
/// rebuilt rows show in place, and the list is told of no row (<c>RowCollection.ReplaceChanged</c>).
/// </para>
/// <para>
/// A rename, outside the app, through the app's rename or through its undo, keeps the number of rows
/// too, but every row between the branch's old place and its new one shifts by a row: other branches
/// at those indexes, which the list is told of by removing that run of rows and inserting the rebuilt
/// ones, as before phase 8a. Two renames in one refresh, or a fetch that drops two remote branches
/// and brings two others, shift two runs with rows that show the same between them: each run is
/// removed and inserted on its own, and the rows between them stay, the objects they were, so that
/// near the top of the list a press on one of them is still a click; before phase 8a they were
/// removed and inserted with the runs, and that press lost its click. A row the list need not tell
/// of is never told of. When the shifted rows were replaced, a rename of the top row in view under
/// an offset smaller than their heights sent the list to its top and lost the click, and deeper in
/// the list left the offset and the extent short by those heights; a row that keeps its item but
/// cannot show its rebuilt row itself is still replaced so, a known limit that these facts do not
/// meet; and a press on a replaced row itself loses its click, as in every version. The remove makes
/// the panel estimate where the top row in view is, from the mean height of the rows it has made,
/// which with headers among the rows is not exact. The facts allow the offset and the extent to move
/// by as much as that gives, each stating its numbers and those before phase 8a's change: one run,
/// up to 5 px near the top of the list and about 20 px at the bottom of 60 branches, as before; two
/// runs, 2 px near the top of the list where it was 1, and deep in the list more than before, a
/// known limit, because the rows left in place between the runs stay in the panel's mean: 108 px at
/// the bottom of 60 branches where it was 61, and 172 px for the fetch at the bottom of 41 local and
/// 31 remote branches, each side's main among them, where it was 166. They assert that nothing moves
/// in the window, that every row the list is not told of keeps its object, that the rows outside
/// the span from the first shifted row to the last keep their controls too, as do the rows between
/// the runs where the fact says so, and that the press is a click.
/// </para>
/// <para>
/// A branch created below the view changes the number of rows: the section's header, far above the
/// view, shows its new count itself, and the list is told of the new row alone, so every row stays,
/// the rows in view with their places and their controls, and the press is a click. Before, the
/// header ended the rows the list kept at its start, and the rows from it down to the new one were
/// removed and inserted, or reset the list above 64 rows. A branch created above the view keeps
/// every row too, and the list is told of its row alone; there the panel estimates where the rows in
/// view are, as it did when the header was removed and inserted with the new row, and draws them a
/// row lower, 4 px short, with new controls, and the press loses its click, a known limit stated in
/// that fact and not asserted.
/// </para>
/// <para>
/// Not asserted here, because the estimate does worse there, as it did before phase 8a: a rename
/// far above the view deep in the list (b01 at the bottom of 60 branches: the offset 1226 for 1356),
/// after which the panel makes five controls in view again, and the press on one of them loses its
/// click, where a replace of the two rows it shifts touched nothing; and a fetch that drops the top
/// remote branch in view and brings another, after which the rows in view are drawn 87 px lower than
/// before. Nor the other cases of the known limit above, where a row left in place, one that shows
/// the same or one that changes itself, lies between the first and the last row that shows something
/// new, told of or changing itself (the span the code before phase 8a removed and inserted): at the
/// bottom of 60 branches, b45 renamed b50x with main (HEAD's branch, the eleventh row below the run)
/// moved to another commit by the same refresh leaves the offset at 1241 for 1356, where it was 1368;
/// at the bottom of 61 branches, b46 renamed b60x with b61, right below the run, moved by the same
/// refresh leaves 1342 for 1386, where it was 1364. Nothing moves in the window there and no press
/// loses a click that the code before phase 8a kept; deep in the list, kept rows in view may get new
/// controls, as they did before phase 8a (<c>RowCollection.ReplaceChanged</c>).
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

    [AvaloniaFact]
    public async Task Renaming_the_top_branch_in_view_so_that_it_sorts_last_while_the_left_panel_is_scrolled_by_less_than_the_rows_it_shifts_keeps_the_list_and_the_click_under_a_press()
    {
        // b03, the top row in view under an offset of 100, becomes c99 after b20: b04 to b20 each
        // move up a row and c99 takes b20's, 18 rows of 26 px that are other branches than before.
        // The bounds are what the estimate gave before phase 8a's change (48f403c), 105 for 100 and
        // 698 for 693; a replace of those rows sent the list to its top, and the click on main with it.
        await AssertShiftKeepsTheListAsync(
            branches: 20,
            offset: 100,
            shift: Outside(("b03", "c99")),
            topInView: "b03",
            bounds: new ShiftBounds(Offset: 5, Extent: 5));
    }

    [AvaloniaFact]
    public async Task Renaming_a_branch_below_the_top_row_in_view_of_a_left_panel_scrolled_near_its_top_keeps_the_list_and_the_click_under_a_press()
    {
        // b01, the top row in view under an offset of 40, stays; b03 becomes c99, so b04 to b20 move
        // up a row. A remove below the first row the panel has made leaves its place known, and the
        // offset stays; the extent ends 2 px longer, 687 for 685, as before phase 8a and with a replace.
        await AssertShiftKeepsTheListAsync(
            branches: 20,
            offset: 40,
            shift: Outside(("b03", "c99")),
            topInView: "b01",
            bounds: new ShiftBounds(Offset: 0, Extent: 2));
    }

    [AvaloniaFact]
    public async Task Renaming_the_top_branch_in_view_of_a_left_panel_scrolled_to_its_bottom_keeps_the_rows_in_view_and_the_click_under_a_press()
    {
        // b45, the top row in view at the bottom of 60 branches, becomes b60x after b60: 16 rows
        // shift. The bounds are what the estimate gave deep in the list before phase 8a's change,
        // 1335 for 1356 and 1880 for 1900; a replace of those rows left 940 and 1484.
        await AssertShiftKeepsTheListAsync(
            branches: 60,
            offset: null,
            shift: Outside(("b45", "b60x")),
            topInView: "b45",
            bounds: new ShiftBounds(Offset: 21, Extent: 20));
    }

    [AvaloniaFact]
    public async Task Renaming_two_branches_of_a_left_panel_scrolled_near_its_top_so_that_two_runs_of_rows_shift_keeps_the_rows_between_them_and_the_click_under_a_press_on_one_of_them()
    {
        // b03, the top row in view under an offset of 100, becomes b05x and b08 becomes b10x in one
        // refresh: b04, b05 and b05x take rows 3 to 5, b09, b10 and b10x rows 8 to 10, two runs told
        // of; b06, pressed, and b07 between them stay, their rows and their controls. The bounds are
        // what this gives, 98 for 100 and 691 for 693. Before phase 8a's change (48f403c), which
        // removed and inserted the rows from b03 to b10 as one, the estimate gave 99 and 692, but b06
        // and b07 were new rows with new controls, and the press on b06 lost its click.
        await AssertShiftKeepsTheListAsync(
            branches: 20,
            offset: 100,
            shift: Outside(("b03", "b05x"), ("b08", "b10x")),
            topInView: "b03",
            bounds: new ShiftBounds(Offset: 2, Extent: 2),
            pressed: "ref:refs/heads/b06",
            between: ["ref:refs/heads/b06", "ref:refs/heads/b07"],
            betweenButtons: true);
    }

    [AvaloniaFact]
    public async Task Renaming_two_branches_of_a_left_panel_scrolled_to_its_bottom_so_that_two_runs_of_rows_shift_with_rows_kept_between_them_keeps_the_rows_in_view_and_the_click_under_a_press()
    {
        // At the bottom of 60 branches, b45 (the top row in view) becomes b48x and b55 becomes b58x in
        // one refresh: b46 to b48 and b56 to b58 move up a row, two runs told of; b49 to b54 between
        // them stay, their rows and their controls. The bounds are what this gives, 1248 for 1356 and
        // 1793 for 1900, a known limit: the rows left in place stay in the panel's estimate of the top
        // row's place. Before phase 8a's change, which removed and inserted the rows from b45 to b58
        // as one, the estimate gave 1295 and 1840, and b49 to b54 were new rows.
        await AssertShiftKeepsTheListAsync(
            branches: 60,
            offset: null,
            shift: Outside(("b45", "b48x"), ("b55", "b58x")),
            topInView: "b45",
            bounds: new ShiftBounds(Offset: 108, Extent: 107),
            between: [.. Enumerable.Range(49, 6).Select(number => string.Create(CultureInfo.InvariantCulture, $"ref:refs/heads/b{number}"))],
            betweenButtons: true);
    }

    [AvaloniaFact]
    public async Task A_fetch_that_drops_two_remote_branches_and_brings_two_others_while_the_left_panel_is_scrolled_to_its_bottom_keeps_the_rows_in_view_and_the_click_under_a_press()
    {
        // b01 to b40 and main, then origin's main and r01 to r30, scrolled to the bottom: origin/r13
        // is the top row in view. The app's fetch, with fetch.prune set, drops r10 and r16 and brings
        // r13a and r19a: r11 to r13a and r17 to r19a are other branches than before at their rows,
        // two runs told of; r14 and r15 between them stay, the same rows, which the panel gives new
        // controls (not asserted). The bounds are what this gives, 1544 for 1716 and 2088 for 2260, a
        // known limit as in the fact above; before phase 8a's change, which removed and inserted the
        // rows from r11 to r19a as one, 1550 and 2094, and r14 and r15 were new rows.
        await AssertShiftKeepsTheListAsync(
            branches: 40,
            offset: null,
            shift: Fetch(drop: ["r10", "r16"], bring: ["r13a", "r19a"]),
            topInView: "r13",
            bounds: new ShiftBounds(Offset: 172, Extent: 172),
            remoteBranches: 30,
            pressed: "ref:refs/remotes/origin/r25",
            between: ["ref:refs/remotes/origin/r14", "ref:refs/remotes/origin/r15"]);
    }

    [AvaloniaFact]
    public async Task Renaming_the_top_branch_in_view_with_the_apps_rename_keeps_the_list_and_the_click_under_a_press_that_comes_while_git_renames_it()
    {
        // The first fact's rename through the branch menu's "Rename b03…", with its bounds. Its
        // FollowRenamedBranch moves only the graph filter's entries for the branch, none here, and
        // scrolls nothing.
        await AssertShiftKeepsTheListAsync(
            branches: 20,
            offset: 100,
            shift: InTheApp("b03", "c99"),
            topInView: "b03",
            bounds: new ShiftBounds(Offset: 5, Extent: 5));
    }

    [AvaloniaFact]
    public async Task Undoing_a_rename_whose_branch_comes_back_as_the_top_row_in_view_keeps_the_list_and_the_click_under_a_press_that_comes_while_git_renames_it_back()
    {
        // b03 was renamed c99 before the scroll, so b04 is the top row in view; the undo brings b03
        // back there and moves b04 to b20 down a row, with the first fact's bounds.
        await AssertShiftKeepsTheListAsync(
            branches: 20,
            offset: 100,
            shift: Undo("b03", "c99"),
            topInView: "b04",
            bounds: new ShiftBounds(Offset: 5, Extent: 5));
    }

    [AvaloniaFact]
    public async Task Creating_a_branch_below_the_view_of_a_scrolled_left_panel_keeps_the_header_the_rows_in_view_and_the_click_under_a_press()
    {
        // 60 branches scrolled by 400 px: b15 to b40 or so in view, the header far above them. b99,
        // made outside the app, sorts after b60, below the view, and the header's count grows by one.
        // The header shows its count itself, so the list is told of b99's row alone. Before, the
        // header ended the rows kept at the list's start, and the rows from it down to b99, 61
        // removed and 62 inserted, reset the list: every row in view got a new control, and the
        // press lost its click.
        using var repo = await ManyBranchesAsync(60);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 400);
        app.Settle();
        list.ItemsPanelRoot!.InvalidateMeasure();
        app.Settle();
        var offsetBefore = scroller.Offset.Y;
        Assert.Equal(400, offsetBefore);
        var header = repository.LeftPanel.Rows[0];
        var rowsBefore = repository.LeftPanel.Rows.ToList();
        var before = InView(app, list, scroller);
        Assert.DoesNotContain(before, entry => entry.Row is RefSectionRow);
        var target = before[4].Row;
        var point = app.BoundsOf(before[4].Button).Center;
        app.PressMouse(point);

        await repo.GitAsync("branch", "b99");
        await repository.RefreshAsync();
        await app.WaitForIdleAsync();
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(HasBranch(repository, "b99"), "The left panel does not show b99.");

        var after = InView(app, list, scroller).ToDictionary(entry => entry.Row.Key, StringComparer.Ordinal);
        var problems = new List<string>();
        if (!ReferenceEquals(repository.LeftPanel.Rows[0], header) || ((RefSectionRow)header).Count != 62)
        {
            problems.Add($"the header is {(ReferenceEquals(repository.LeftPanel.Rows[0], header) ? "the same row" : "another row")} with the count {((RefSectionRow)repository.LeftPanel.Rows[0]).Count}");
        }

        var others = rowsBefore.Where(row => !repository.LeftPanel.Rows.Contains(row)).Select(row => row.Text).ToList();
        if (others.Count > 0)
        {
            problems.Add($"{others.Count} rows were made again: {string.Join(", ", others.Take(5))}");
        }

        if (scroller.Offset.Y != offsetBefore)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {scroller.Offset.Y} for {offsetBefore}"));
        }

        foreach (var (shown, y, button) in before)
        {
            if (!after.TryGetValue(shown.Key, out var now))
            {
                problems.Add($"{shown.Text} is no longer in view");
            }
            else if (now.Y != y || !ReferenceEquals(now.Row, shown) || !ReferenceEquals(now.Button, button))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{shown.Text} at y {now.Y} for {y}, {(ReferenceEquals(now.Row, shown) ? "the same row" : "another row")}, {(ReferenceEquals(now.Button, button) ? "the same button" : "another button")}"));
            }
        }

        app.ReleaseMouse(point);
        await app.WaitForIdleAsync();
        var selected = repository.LeftPanel.Rows.OfType<RefItemRow>().Where(item => item.IsSelected).Select(item => item.Text).ToList();
        if (selected is not [var only] || only != target.Text)
        {
            problems.Add($"the press on {target.Text} selected {(selected.Count == 0 ? "nothing" : string.Join(", ", selected))}");
        }

        Assert.True(problems.Count == 0, $"After b99 was created: {string.Join("; ", problems)}. In view before: {Describe(before)}.");
    }

    [AvaloniaFact]
    public async Task Creating_a_branch_above_the_view_of_a_scrolled_left_panel_keeps_every_row_and_tells_the_list_of_the_new_row_alone()
    {
        // 60 branches scrolled by 400 px, as in the fact above; a00, made outside the app, sorts
        // before b01, right under the header and far above the view. The header shows its new count
        // itself and every row stays the object it was: the list is told of a00's row alone, one Add
        // at index 1, and nothing sends the list to its top. Before, the header was removed and
        // inserted with a00, a Remove and two Adds. What the panel does with an insert above the
        // first row it has made is Avalonia's estimate, the same then and now, and is not asserted:
        // the offset stays 400, the extent becomes 1742 for 1720 where the row is 26 px, and the
        // rows in view are drawn 22 px lower, 4 px above their true places (the estimate takes the
        // mean of the 26 px rows it has made and misses the 30 px header above them), each with a
        // new control, b36 out of view; a press on one of them, b19, loses its click, as it did
        // before (measured on 57ebeff's rows, the same numbers). The row under the pointer has
        // moved away in any case: the true places are a row lower.
        using var repo = await ManyBranchesAsync(60);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 400);
        app.Settle();
        list.ItemsPanelRoot!.InvalidateMeasure();
        app.Settle();
        Assert.Equal(400, scroller.Offset.Y);
        var header = repository.LeftPanel.Rows[0];
        var rowsBefore = repository.LeftPanel.Rows.ToList();
        var before = InView(app, list, scroller);
        Assert.DoesNotContain(before, entry => entry.Row is RefSectionRow);
        Assert.DoesNotContain(before, entry => entry.Row.Text is "b01" or "b02");
        var changes = new List<NotifyCollectionChangedEventArgs>();
        repository.LeftPanel.Rows.CollectionChanged += (_, e) => changes.Add(e);
        var point = app.BoundsOf(before[4].Button).Center;
        app.PressMouse(point);

        await repo.GitAsync("branch", "a00");
        await repository.RefreshAsync();
        await app.WaitForIdleAsync();
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        app.ReleaseMouse(point);
        await app.WaitForIdleAsync();
        Assert.True(HasBranch(repository, "a00"), "The left panel does not show a00.");

        var rowsAfter = repository.LeftPanel.Rows.ToList();
        Assert.Same(header, rowsAfter[0]);
        Assert.Equal(62, ((RefSectionRow)header).Count);
        Assert.Equal("a00", rowsAfter[1].Text);
        Assert.Equal(rowsBefore.Skip(1), rowsAfter.Skip(2));
        Assert.True(rowsBefore.Skip(1).Zip(rowsAfter.Skip(2)).All(pair => ReferenceEquals(pair.First, pair.Second)), "A row of the branches was made again.");
        var change = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Add, change.Action);
        Assert.Equal(1, change.NewStartingIndex);
        Assert.Same(rowsAfter[1], Assert.Single(change.NewItems!.Cast<RefListRow>()));
        Assert.Equal(400, scroller.Offset.Y);
    }

    /// <summary>
    /// The app on a repository with the local branches b01 to b<paramref name="branches"/> and main,
    /// and with <paramref name="remoteBranches"/> its remote origin's main and r01 on, fetched with
    /// fetch.prune set; its git held by a <see cref="HeldGitRunner"/>; <paramref name="shift"/>'s
    /// set-up, then the left panel scrolled to <paramref name="offset"/>, or to its bottom without one,
    /// with <paramref name="topInView"/> its top row in view; a press on the row whose key is
    /// <paramref name="pressed"/>; then the change that <paramref name="shift"/> makes, which keeps
    /// the number of rows and moves some rows by a row: other branches than before at those indexes,
    /// which the list is told of. The press comes where <paramref name="shift"/> says, its release
    /// after the refresh. Asserts that the offset and the extent moved by no more than
    /// <paramref name="bounds"/> says; that each row in view before has, at its index, a row in view
    /// at its place in the window, nothing moving on screen; that every row whose item stays at its
    /// index, which the list is not told of, is the same object; that each row in view outside the
    /// span from the first moved row to the last (main and the headers below them, the rows above
    /// them) has the same button; that the rows whose keys <paramref name="between"/> names, in view
    /// between two runs of moved rows, are the same objects and, with
    /// <paramref name="betweenButtons"/>, have the same buttons; and that the release selected the
    /// pressed row.
    /// </summary>
    private static async Task AssertShiftKeepsTheListAsync(
        int branches,
        double? offset,
        Shift shift,
        string topInView,
        ShiftBounds bounds,
        int remoteBranches = 0,
        string pressed = "ref:refs/heads/main",
        string[]? between = null,
        bool betweenButtons = false)
    {
        using var repo = await ManyBranchesAsync(branches);
        using var remote = remoteBranches > 0 ? await RemoteAsync(repo, remoteBranches) : null;
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700, gitProbe: held.Probe);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        var context = new ShiftContext(app, repository, repo, remote, held);
        if (shift.Prepare is { } prepare)
        {
            await prepare(context);
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            app.Settle();
        }

        app.MoveToNeutral();
        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        var wanted = offset ?? Bottom(scroller);
        scroller.Offset = new Vector(0, wanted);
        app.Settle();

        // As in AssertChangeKeepsTheListAsync: the extent read after a second layout.
        list.ItemsPanelRoot!.InvalidateMeasure();
        app.Settle();
        var offsetBefore = scroller.Offset.Y;
        var extent = scroller.Extent.Height;
        Assert.True(offsetBefore > 0, $"The left panel is not scrolled: extent {extent}, view {scroller.Viewport.Height}.");
        Assert.True(offset is null || offsetBefore == wanted, string.Create(CultureInfo.InvariantCulture, $"The left panel scrolls to {offsetBefore}, not to {wanted}: extent {extent}, view {scroller.Viewport.Height}."));

        var rowsBefore = repository.LeftPanel.Rows.ToList();
        var before = InView(app, list, scroller);
        Assert.Equal(topInView, before[0].Row.Text);
        var target = (RefItemRow)before.Single(entry => entry.Row.Key == pressed).Row;
        var point = app.BoundsOf(before.Single(entry => ReferenceEquals(entry.Row, target)).Button).Center;

        await shift.Make(context, () => app.PressMouse(point));
        await app.WaitForIdleAsync();
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(shift.Shown(repository), $"The left panel does not show {shift.Description}.");

        var rowsAfter = repository.LeftPanel.Rows.ToList();
        Assert.Equal(rowsBefore.Count, rowsAfter.Count);
        var movedRows = Enumerable.Range(0, rowsBefore.Count).Where(index => rowsBefore[index].Key != rowsAfter[index].Key).ToList();
        Assert.True(movedRows.Count > 1, $"{shift.Description} moved {movedRows.Count} rows.");
        var firstMoved = movedRows[0];
        var lastMoved = movedRows[^1];
        var after = InView(app, list, scroller);
        var problems = new List<string>();
        var offsetAfter = scroller.Offset.Y;
        var extentAfter = scroller.Extent.Height;
        if (Math.Abs(offsetAfter - offsetBefore) > bounds.Offset)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {offsetAfter} for {offsetBefore}, more than {bounds.Offset} off"));
        }

        if (Math.Abs(extentAfter - extent) > bounds.Extent)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"extent {extentAfter} for {extent}, more than {bounds.Extent} off"));
        }

        // The list is told of the moved rows alone: every other row stays the object it was.
        var otherRows = Enumerable.Range(0, rowsBefore.Count)
            .Where(index => !movedRows.Contains(index) && !ReferenceEquals(rowsBefore[index], rowsAfter[index]))
            .Select(index => rowsBefore[index].Text)
            .ToList();
        if (otherRows.Count > 0)
        {
            problems.Add($"rows whose items stay at their indexes are other rows: {string.Join(", ", otherRows)}");
        }

        var afterByIndex = after.ToDictionary(entry => rowsAfter.IndexOf(entry.Row));
        foreach (var (shown, y, button) in before)
        {
            var index = rowsBefore.IndexOf(shown);
            if (!afterByIndex.TryGetValue(index, out var now))
            {
                problems.Add($"row {index} ({shown.Text}, now {rowsAfter[index].Text}) is no longer in view");
                continue;
            }

            if (now.Y != y)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"row {index} ({now.Row.Text}) at y {now.Y} for {y}"));
            }

            if ((index < firstMoved || index > lastMoved) && !ReferenceEquals(now.Button, button))
            {
                problems.Add($"{shown.Text} has another button");
            }
        }

        foreach (var key in between ?? [])
        {
            var index = rowsBefore.FindIndex(row => row.Key == key);
            Assert.True(index > firstMoved && index < lastMoved && !movedRows.Contains(index), $"{key} is not between two runs of moved rows: row {index}, moved rows {string.Join(", ", movedRows)}.");
            var shown = before.SingleOrDefault(entry => ReferenceEquals(entry.Row, rowsBefore[index]));
            Assert.True(shown.Row is not null, $"{key} is not in view.");
            if (!ReferenceEquals(rowsAfter[index], shown.Row))
            {
                problems.Add($"{shown.Row.Text}, between the runs, is another row");
            }

            if (betweenButtons && (!afterByIndex.TryGetValue(index, out var now) || !ReferenceEquals(now.Button, shown.Button)))
            {
                problems.Add($"{shown.Row.Text}, between the runs, has another button");
            }
        }

        app.ReleaseMouse(point);
        await app.WaitForIdleAsync();
        var selected = repository.LeftPanel.Rows.OfType<RefItemRow>().Where(item => item.IsSelected).ToList();
        if (selected is not [var only] || only.Key != pressed)
        {
            problems.Add($"the press on {target.Text} selected {(selected.Count == 0 ? "nothing" : string.Join(", ", selected.Select(item => item.Text)))}");
        }

        Assert.True(
            problems.Count == 0,
            string.Create(CultureInfo.InvariantCulture, $"After {shift.Description}: {string.Join("; ", problems)}. Offset {offsetAfter} for {offsetBefore}, extent {extentAfter} for {extent}. In view before: {Describe(before)}; after: {Describe(after)}."));
    }

    /// <summary>How far a shift may move the offset and the extent: no further than Avalonia's estimate moves them, which each fact states with what it was before phase 8a's change.</summary>
    private sealed record ShiftBounds(double Offset, double Extent);

    /// <summary>The app, its repository, the repository on disk, its remote origin if it has one, and the git runner that can hold a call.</summary>
    private sealed record ShiftContext(ShellDriver App, RepositoryViewModel Repository, TempRepo Repo, TempRepo? Remote, HeldGitRunner Held);

    /// <summary>
    /// A rename that moves rows: what is done before the scroll (<see cref="Prepare"/>), then the
    /// rename itself, which calls the press it is given where the press is to come, and whether the
    /// left panel shows it.
    /// </summary>
    private sealed record Shift(string Description, Func<ShiftContext, Task>? Prepare, Func<ShiftContext, Action, Task> Make, Func<RepositoryViewModel, bool> Shown);

    /// <summary>
    /// The rename of each of <paramref name="renames"/> outside the app, after the press, then the
    /// refresh a watcher would start: one rename with <c>git branch -m</c>; more in one
    /// <c>git update-ref --stdin</c> transaction, so that they all come within the quiet period of the
    /// app's watcher (300 ms) and one rebuild shows them, with no git process between them.
    /// </summary>
    private static Shift Outside(params (string From, string To)[] renames) => new(
        $"the rename of {string.Join(" and ", renames.Select(rename => $"{rename.From} to {rename.To}"))} outside the app",
        null,
        async (context, press) =>
        {
            press();
            if (renames is [var (from, to)])
            {
                await context.Repo.GitAsync("branch", "-m", from, to);
            }
            else
            {
                var commands = new StringBuilder();
                foreach (var rename in renames)
                {
                    var target = await context.Repo.RefTargetAsync("refs/heads/" + rename.From) ?? throw new InvalidOperationException($"No branch {rename.From}.");
                    commands.Append(CultureInfo.InvariantCulture, $"create refs/heads/{rename.To} {target}\ndelete refs/heads/{rename.From}\n");
                }

                await context.Repo.GitWithInputAsync(commands.ToString(), "update-ref", "--stdin");
            }

            await context.Repository.RefreshAsync();
        },
        repository => renames.All(rename => HasBranch(repository, rename.To) && !HasBranch(repository, rename.From)));

    /// <summary>
    /// The branches <paramref name="drop"/> deleted on the remote and <paramref name="bring"/> made
    /// there, then the press and the app's fetch of origin (<c>RepositoryOperations.FetchAsync</c>),
    /// which prunes, as fetch.prune is set.
    /// </summary>
    private static Shift Fetch(string[] drop, string[] bring) => new(
        $"the fetch that drops origin's {string.Join(" and ", drop)} and brings {string.Join(" and ", bring)}",
        null,
        async (context, press) =>
        {
            var remote = context.Remote ?? throw new InvalidOperationException("The repository has no remote.");
            foreach (var name in drop)
            {
                await remote.GitAsync("branch", "-D", name);
            }

            foreach (var name in bring)
            {
                await remote.GitAsync("branch", name);
            }

            press();
            await context.Repository.Operations.FetchAsync("origin");
        },
        repository => bring.All(name => HasRemoteBranch(repository, name)) && !drop.Any(name => HasRemoteBranch(repository, name)));

    /// <summary>
    /// The branch menu's "Rename …" (<c>RepositoryOperations.RenameBranchAsync</c>): its dialog
    /// answered and confirmed with a click, then the press while git renames the branch, held until
    /// the press has come.
    /// </summary>
    private static Shift InTheApp(string from, string to) => new(
        $"the app's rename of {from} to {to}",
        null,
        async (context, press) =>
        {
            var app = context.App;
            var renaming = context.Repository.Operations.RenameBranchAsync("refs/heads/" + from);
            await app.WaitForDialogAsync<BranchNameDialogViewModel>();
            app.ReplaceText("NameBox", to);
            app.Settle();
            context.Held.HoldNext("branch");
            app.Click(app.FindByAutomationId(app.TopDialogView ?? throw new InvalidOperationException("No dialog is open."), "ConfirmButton"));
            await HeldAsync(context, "--move", from, to);
            press();
            context.Held.Release();
            await renaming;
        },
        repository => HasBranch(repository, to) && !HasBranch(repository, from));

    /// <summary>
    /// The app's rename of <paramref name="from"/> to <paramref name="to"/> before the scroll, then the
    /// toolbar's Undo (<c>RepositoryOperations.UndoAsync</c>), the press while git renames the branch
    /// back, held until the press has come.
    /// </summary>
    private static Shift Undo(string from, string to) => new(
        $"the undo of the app's rename of {from} to {to}",
        async context =>
        {
            var app = context.App;
            var renaming = context.Repository.Operations.RenameBranchAsync("refs/heads/" + from);
            await app.WaitForDialogAsync<BranchNameDialogViewModel>();
            app.ReplaceText("NameBox", to);
            app.Settle();
            app.Click(app.FindByAutomationId(app.TopDialogView ?? throw new InvalidOperationException("No dialog is open."), "ConfirmButton"));
            await renaming;
            await app.WaitForIdleAsync();
            Assert.True(HasBranch(context.Repository, to), $"{from} was not renamed {to}.");
        },
        async (context, press) =>
        {
            context.Held.HoldNext("branch");
            var undoing = context.Repository.Operations.UndoAsync();
            await HeldAsync(context, "--move", to, from);
            press();
            context.Held.Release();
            await undoing;
        },
        repository => HasBranch(repository, from) && !HasBranch(repository, to));

    /// <summary>Waits until the held git call has come and checks that it is <c>git branch</c> with <paramref name="arguments"/>.</summary>
    private static async Task HeldAsync(ShiftContext context, params string[] arguments)
    {
        await context.App.WaitForAsync(() => context.Held.Started.IsCompleted, "the rename's git branch to be held");
        var command = await context.Held.Started;
        Assert.Equal(["branch", .. arguments], command.Arguments.ToArray());
    }

    private static bool HasBranch(RepositoryViewModel repository, string name) =>
        repository.LeftPanel.Rows.OfType<RefItemRow>().Any(row => row.Kind == RefItemKind.LocalBranch && row.Text == name);

    private static bool HasRemoteBranch(RepositoryViewModel repository, string name) =>
        repository.LeftPanel.Rows.Any(row => row.Key == "ref:refs/remotes/origin/" + name);

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

    /// <summary>
    /// A repository with one commit on main and the branches r01 to r<paramref name="branches"/> at
    /// it, added to <paramref name="repo"/> as origin and fetched, with fetch.prune set there.
    /// </summary>
    private static async Task<TempRepo> RemoteAsync(TempRepo repo, int branches)
    {
        var remote = await TempRepo.CreateAsync("remote");
        try
        {
            var head = await remote.CommitFileAsync("r.txt", "r\n", "Add r.txt");
            var refs = new StringBuilder();
            for (var number = 1; number <= branches; number++)
            {
                refs.Append(CultureInfo.InvariantCulture, $"create refs/heads/r{number:D2} {head}\n");
            }

            await remote.GitWithInputAsync(refs.ToString(), "update-ref", "--stdin");
            await repo.GitAsync("remote", "add", "origin", remote.Path);
            await repo.GitAsync("fetch", "--quiet", "origin");
            await repo.ConfigAsync("fetch.prune", "true");
            return remote;
        }
        catch
        {
            remote.Dispose();
            throw;
        }
    }
}
