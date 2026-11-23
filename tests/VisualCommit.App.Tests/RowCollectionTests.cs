using System.Collections.Specialized;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The two forms of <c>RowCollection.ReplaceAll</c> with a comparison on their own: which rows a
/// rebuild keeps, what it tells the view, and what the list holds afterwards. A rebuild with as many
/// rows as before changes each row that shows something new and keeps every other row: a row that
/// can show the new content itself (<see cref="IUpdatableRow{T}"/>, the left panel's branch rows and
/// headers) does, and the view is told nothing of it; the others are told of, and more than 64 of
/// them reset the list. In the form for rows of one height (the stage lists) each such row is removed
/// and its rebuilt row inserted. In the form for rows of mixed heights (the left panel) they are
/// taken by runs of consecutive rows: a run whose rows are all the same items as their rebuilt rows
/// (here the same name, there the same key) is replaced row by row, and a run that holds another
/// item (rows that only shift) is removed, last to first, and its rebuilt rows inserted, first to
/// last. Each run is told of on its own: a row between two runs that shows the same, or that changes
/// itself, stays the object it was, and only the rows told of count toward the reset. A rebuild that
/// changes the number of rows keeps the rows before and after the part that changed, for either
/// form.
/// </summary>
public class RowCollectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rebuild_with_as_many_rows_and_two_changes_far_apart_changes_those_two_alone(bool rowsOfOneHeight)
    {
        var list = Rows(20);
        var before = list.ToList();
        var rebuilt = Rebuilt(before, 3, 15);
        var changes = Record(list);

        Replace(list, rebuilt, Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, 3, 15), changes);
        AssertKept(before, list, changed: [3, 15]);
        Assert.Same(rebuilt[3], list[3]);
        Assert.Same(rebuilt[15], list[15]);
        Assert.Equal(rebuilt, list);
    }

    [Fact]
    public void A_stage_list_read_again_with_a_file_changed_on_each_side_of_an_unchanged_one_keeps_the_unchanged_ones_row()
    {
        var files = new ChangedFileList();
        files.SetMode(FileListMode.Flat);
        files.Show([new("a.txt", FileChangeKind.Modified), new("b.txt", FileChangeKind.Modified), new("c.txt", FileChangeKind.Modified)], keepFolders: true);
        var before = files.Rows.ToList();
        var changes = Record(files.Rows);

        files.Show([new("a.txt", FileChangeKind.Deleted), new("b.txt", FileChangeKind.Modified), new("c.txt", FileChangeKind.Added)], keepFolders: true);

        Assert.Equal([(NotifyCollectionChangedAction.Remove, 0), (NotifyCollectionChangedAction.Add, 0), (NotifyCollectionChangedAction.Remove, 2), (NotifyCollectionChangedAction.Add, 2)], changes);
        Assert.Same(before[1], files.Rows[1]);
        Assert.NotSame(before[0], files.Rows[0]);
        Assert.NotSame(before[2], files.Rows[2]);
        Assert.Equal(["a.txt", "b.txt", "c.txt"], files.Rows.Select(row => row.Key));
        Assert.Equal(["D", "M", "A"], files.Rows.Select(row => row.StatusLetter));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sixty_four_changed_rows_are_changed_one_by_one_and_sixty_five_reset_the_list(bool rowsOfOneHeight)
    {
        // Every other row from row 1 on: 64 changed rows with a kept row between each two.
        var list = Rows(130);
        var before = list.ToList();
        var changed = Enumerable.Range(0, 64).Select(i => 1 + (2 * i)).ToArray();
        var rebuilt = Rebuilt(before, changed);
        var changes = Record(list);

        Replace(list, rebuilt, Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, changed), changes);
        AssertKept(before, list, changed);
        Assert.Equal(rebuilt, list);

        // One more: the list is reset once, and holds the rebuilt rows.
        var longer = Rows(132);
        var more = Rebuilt(longer.ToList(), [.. Enumerable.Range(0, 65).Select(i => 1 + (2 * i))]);
        var resets = Record(longer);

        Replace(longer, more, Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.Equal(more, longer);
        Assert.All(Enumerable.Range(0, more.Count), i => Assert.Same(more[i], longer[i]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rebuild_with_as_many_rows_whose_rows_only_shift_by_one_holds_the_rebuilt_rows(bool rowsOfOneHeight)
    {
        // r02 is gone and n is new after r06: r03 to r06 each move up a row, and r07 on stay.
        var list = Rows(10);
        var before = list.ToList();
        List<Row> rebuilt = [Copy(before[0]), Copy(before[1]), .. before.Skip(3).Take(4).Select(Copy), new Row("n", 0), .. before.Skip(7).Select(Copy)];
        Assert.Equal(before.Count, rebuilt.Count);

        Replace(list, rebuilt, Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Equal(rebuilt, list);
        Assert.Same(before[0], list[0]);
        Assert.Same(before[1], list[1]);
        Assert.All(Enumerable.Range(7, 3), i => Assert.Same(before[i], list[i]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rebuild_that_changes_the_number_of_rows_keeps_the_rows_before_and_after_the_part_that_changed(bool rowsOfOneHeight)
    {
        var list = Rows(6);
        var before = list.ToList();
        List<Row> rebuilt = [Copy(before[0]), Copy(before[1]), new Row("x", 0), new Row("y", 0), new Row("z", 0), Copy(before[4]), Copy(before[5])];
        var changes = Record(list);

        Replace(list, rebuilt, Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Equal(
            [
                (NotifyCollectionChangedAction.Remove, 3), (NotifyCollectionChangedAction.Remove, 2),
                (NotifyCollectionChangedAction.Add, 2), (NotifyCollectionChangedAction.Add, 3), (NotifyCollectionChangedAction.Add, 4),
            ],
            changes);
        Assert.Equal(rebuilt, list);
        Assert.Same(before[0], list[0]);
        Assert.Same(before[1], list[1]);
        Assert.Same(before[4], list[5]);
        Assert.Same(before[5], list[6]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rebuild_that_shows_the_same_tells_the_view_nothing_and_keeps_every_row(bool rowsOfOneHeight)
    {
        var list = Rows(8);
        var before = list.ToList();
        var changes = Record(list);

        Replace(list, [.. before.Select(Copy)], Row.ShowSame, Row.SameItem, rowsOfOneHeight);

        Assert.Empty(changes);
        AssertKept(before, list, changed: []);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rebuild_with_as_many_rows_changes_the_rows_that_can_change_themselves_in_place_and_tells_the_view_only_of_the_others(bool rowsOfOneHeight)
    {
        // Rows 2 and 15 show a new version, which they can show themselves; row 9 shows a new kind,
        // which it cannot.
        var list = Updatable(20);
        var before = list.ToList();
        List<Changing> rebuilt = [.. before.Select((row, index) => index switch
        {
            2 or 15 => new Changing(row.Name, row.Kind, row.Version + 1),
            9 => new Changing(row.Name, row.Kind + 1, row.Version),
            _ => new Changing(row.Name, row.Kind, row.Version),
        })];
        var changes = Record(list);

        Replace(list, rebuilt, Changing.ShowSame, Changing.SameItem, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, 9), changes);
        AssertKept(before, list, changed: [9]);
        Assert.Same(rebuilt[9], list[9]);
        Assert.Equal(1, list[2].Version);
        Assert.Equal(1, list[15].Version);
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(list[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rows_that_change_themselves_reset_no_list_however_many_and_only_the_others_count_for_a_reset(bool rowsOfOneHeight)
    {
        // A hundred rows that each show a new version: no row is told of, every row stays.
        var list = Updatable(100);
        var before = list.ToList();
        var changes = Record(list);

        Replace(list, [.. before.Select(row => new Changing(row.Name, row.Kind, row.Version + 1))], Changing.ShowSame, Changing.SameItem, rowsOfOneHeight);

        Assert.Empty(changes);
        AssertKept(before, list, changed: []);
        Assert.All(list, row => Assert.Equal(1, row.Version));

        // Every other row from row 1 on shows a new kind, sixty-four of them, and every row between
        // them a new version: only the sixty-four are told of, one by one.
        var mixed = Updatable(130);
        var shown = mixed.ToList();
        var kinds = Enumerable.Range(0, 64).Select(i => 1 + (2 * i)).ToArray();
        List<Changing> rebuilt = [.. shown.Select((row, index) => kinds.Contains(index) ? new Changing(row.Name, row.Kind + 1, row.Version) : new Changing(row.Name, row.Kind, row.Version + 1))];
        var told = Record(mixed);

        Replace(mixed, rebuilt, Changing.ShowSame, Changing.SameItem, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, kinds), told);
        AssertKept(shown, mixed, kinds);
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(mixed[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));

        // Sixty-five such rows reset the list once, and it holds the rebuilt rows.
        var longer = Updatable(132);
        var more = Enumerable.Range(0, 65).Select(i => 1 + (2 * i)).ToArray();
        List<Changing> rebuiltMore = [.. longer.ToList().Select((row, index) => more.Contains(index) ? new Changing(row.Name, row.Kind + 1, row.Version) : new Changing(row.Name, row.Kind, row.Version + 1))];
        var resets = Record(longer);

        Replace(longer, rebuiltMore, Changing.ShowSame, Changing.SameItem, rowsOfOneHeight);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.All(Enumerable.Range(0, rebuiltMore.Count), i => Assert.Same(rebuiltMore[i], longer[i]));
    }

    [Fact]
    public void A_run_of_rows_that_only_shift_is_removed_and_inserted_alone_and_the_rows_on_both_sides_stay()
    {
        // r05 is gone and n is new after r09: r06 to r09 each move up a row; r15, apart from them,
        // shows a new version of itself.
        var list = Rows(20);
        var before = list.ToList();
        List<Row> rebuilt = [.. before.Take(5).Select(Copy), .. before.Skip(6).Take(4).Select(Copy), new Row("n", 0), .. before.Skip(10).Select((row, i) => i == 5 ? row with { Version = 1 } : Copy(row))];
        Assert.Equal(before.Count, rebuilt.Count);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal([.. Removed(5, 9), .. Added(5, 9), (NotifyCollectionChangedAction.Replace, 15)], changes);
        AssertKept(before, list, changed: [5, 6, 7, 8, 9, 15]);
        Assert.All(Enumerable.Range(5, 5).Append(15), i => Assert.Same(rebuilt[i], list[i]));
        Assert.Equal(rebuilt, list);
    }

    [Fact]
    public void A_run_of_rows_that_show_new_versions_of_the_same_items_is_replaced_row_by_row()
    {
        var list = Rows(12);
        var before = list.ToList();
        var rebuilt = Rebuilt(before, 4, 5, 6);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal([(NotifyCollectionChangedAction.Replace, 4), (NotifyCollectionChangedAction.Replace, 5), (NotifyCollectionChangedAction.Replace, 6)], changes);
        AssertKept(before, list, changed: [4, 5, 6]);
        Assert.Equal(rebuilt, list);
    }

    [Fact]
    public void A_run_that_holds_a_new_version_of_its_item_and_rows_that_only_shift_is_removed_and_inserted_whole()
    {
        // r04 shows a new version; right below it r05 is gone and n is new after r07.
        var list = Rows(10);
        var before = list.ToList();
        List<Row> rebuilt = [.. before.Take(4).Select(Copy), before[4] with { Version = 1 }, Copy(before[6]), Copy(before[7]), new Row("n", 0), .. before.Skip(8).Select(Copy)];
        Assert.Equal(before.Count, rebuilt.Count);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal([.. Removed(4, 7), .. Added(4, 7)], changes);
        AssertKept(before, list, changed: [4, 5, 6, 7]);
        Assert.Equal(rebuilt, list);
    }

    [Fact]
    public void Two_runs_with_kept_rows_and_a_row_that_changes_itself_between_them_are_each_told_of_by_their_own_kind()
    {
        // Rows 2 to 4 only shift (r02 gone, x new after r04); row 5 stays, row 6 shows a new version
        // itself, row 7 stays; rows 8 and 9 show a new kind of their own item, which they cannot show
        // themselves; rows 12 and 13 only shift again (r12 gone, y new after r13).
        var list = Updatable(16);
        var before = list.ToList();
        var rebuilt = TwoRuns(before);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Changing.ShowSame, Changing.SameItem);

        Assert.Equal(
            [
                .. Removed(2, 4), .. Added(2, 4),
                (NotifyCollectionChangedAction.Replace, 8), (NotifyCollectionChangedAction.Replace, 9),
                .. Removed(12, 13), .. Added(12, 13),
            ],
            changes);
        AssertKept(before, list, changed: [2, 3, 4, 8, 9, 12, 13]);
        Assert.Equal(1, list[6].Version);
        Assert.All(new[] { 2, 3, 4, 8, 9, 12, 13 }, i => Assert.Same(rebuilt[i], list[i]));
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(list[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));
    }

    [Fact]
    public void A_run_of_sixty_four_rows_that_only_shift_is_removed_and_inserted_and_one_of_sixty_five_resets_the_list()
    {
        // r01 is gone and n is new after r64: 64 rows told of, each counted once.
        var list = Rows(70);
        var before = list.ToList();
        List<Row> rebuilt = [Copy(before[0]), .. before.Skip(2).Take(63).Select(Copy), new Row("n", 0), .. before.Skip(65).Select(Copy)];
        Assert.Equal(before.Count, rebuilt.Count);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal([.. Removed(1, 64), .. Added(1, 64)], changes);
        AssertKept(before, list, changed: [.. Enumerable.Range(1, 64)]);
        Assert.Equal(rebuilt, list);

        // r01 gone and n new after r65: 65 rows, one reset.
        var longer = Rows(70);
        var shown = longer.ToList();
        List<Row> more = [Copy(shown[0]), .. shown.Skip(2).Take(64).Select(Copy), new Row("n", 0), .. shown.Skip(66).Select(Copy)];
        Assert.Equal(shown.Count, more.Count);
        var resets = Record(longer);

        longer.ReplaceAll(more, Row.ShowSame, Row.SameItem);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.All(Enumerable.Range(0, more.Count), i => Assert.Same(more[i], longer[i]));
    }

    [Fact]
    public void Rows_that_change_themselves_right_beside_a_run_of_rows_that_only_shift_keep_their_objects_and_the_view_is_told_of_the_run_alone()
    {
        // r02 is gone and x is new after r04: rows 2 to 4 only shift. r01 (row 1, right above them)
        // and r05 (row 5, right below them) show a new version, which they show themselves.
        var list = Updatable(10);
        var before = list.ToList();
        List<Changing> rebuilt = [Same(before[0]), Next(before[1]), Same(before[3]), Same(before[4]), new Changing("x", 0, 0), Next(before[5]), .. before.Skip(6).Select(Same)];
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Changing.ShowSame, Changing.SameItem);

        Assert.Equal([.. Removed(2, 4), .. Added(2, 4)], changes);
        AssertKept(before, list, changed: [2, 3, 4]);
        Assert.Equal(1, list[1].Version);
        Assert.Equal(1, list[5].Version);
        Assert.All(Enumerable.Range(2, 3), i => Assert.Same(rebuilt[i], list[i]));
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(list[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));
    }

    [Fact]
    public void Runs_of_rows_that_only_shift_with_rows_that_show_the_same_between_them_are_each_removed_and_inserted_and_the_rows_between_them_stay()
    {
        // Rows 2 and 3 only shift (r02 gone, x new after r03), rows 6 and 7 too (r06 gone, y new after
        // r07), and rows 10 and 11 (r10 gone, z new after r11); rows 4, 5, 8 and 9 show the same.
        var list = Rows(14);
        var before = list.ToList();
        var rebuilt = ThreeShiftedRuns(before);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        // Each run on its own, first to last; the rows between them are the objects they were.
        Assert.Equal([.. Removed(2, 3), .. Added(2, 3), .. Removed(6, 7), .. Added(6, 7), .. Removed(10, 11), .. Added(10, 11)], changes);
        AssertKept(before, list, changed: [2, 3, 6, 7, 10, 11]);
        Assert.All(new[] { 4, 5, 8, 9 }, i => Assert.Same(before[i], list[i]));
        Assert.All(new[] { 2, 3, 6, 7, 10, 11 }, i => Assert.Same(rebuilt[i], list[i]));
        Assert.Equal(rebuilt, list);

        // The form for rows of one height tells of each row that shows something new on its own,
        // and keeps the rows between them.
        var stage = Rows(14);
        var shown = stage.ToList();
        var told = Record(stage);

        stage.ReplaceAll(ThreeShiftedRuns(shown), Row.ShowSame);

        Assert.Equal(Told(rowsOfOneHeight: true, 2, 3, 6, 7, 10, 11), told);
        AssertKept(shown, stage, changed: [2, 3, 6, 7, 10, 11]);
    }

    [Fact]
    public void Two_runs_of_rows_that_only_shift_are_each_removed_and_inserted_and_a_row_that_changes_itself_between_them_is_not_told_of()
    {
        // Rows 2 to 4 only shift (r02 gone, x new after r04), and rows 8 and 9 (r08 gone, y new after
        // r09); between them rows 5 and 7 show the same and row 6 shows a new version itself.
        var list = Updatable(12);
        var before = list.ToList();
        List<Changing> rebuilt =
        [
            Same(before[0]), Same(before[1]), Same(before[3]), Same(before[4]), new Changing("x", 0, 0),
            Same(before[5]), Next(before[6]), Same(before[7]),
            Same(before[9]), new Changing("y", 0, 0), Same(before[10]), Same(before[11]),
        ];
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Changing.ShowSame, Changing.SameItem);

        Assert.Equal([.. Removed(2, 4), .. Added(2, 4), .. Removed(8, 9), .. Added(8, 9)], changes);
        AssertKept(before, list, changed: [2, 3, 4, 8, 9]);
        Assert.Equal(1, list[6].Version);
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(list[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));
    }

    [Fact]
    public void Two_runs_of_rows_that_only_shift_are_each_removed_and_inserted_and_a_run_between_them_that_shows_new_versions_of_its_items_is_replaced_row_by_row()
    {
        // Rows 2 to 4 only shift (r02 gone, x new after r04), and rows 9 and 10 (r09 gone, y new after
        // r10); between them rows 5 and 8 show the same, and rows 6 and 7 show a new version of their
        // own item, which they cannot show themselves.
        var list = Rows(13);
        var before = list.ToList();
        List<Row> rebuilt =
        [
            Copy(before[0]), Copy(before[1]), Copy(before[3]), Copy(before[4]), new Row("x", 0),
            Copy(before[5]), before[6] with { Version = 1 }, before[7] with { Version = 1 }, Copy(before[8]),
            Copy(before[10]), new Row("y", 0), Copy(before[11]), Copy(before[12]),
        ];
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal(
            [
                .. Removed(2, 4), .. Added(2, 4),
                (NotifyCollectionChangedAction.Replace, 6), (NotifyCollectionChangedAction.Replace, 7),
                .. Removed(9, 10), .. Added(9, 10),
            ],
            changes);
        AssertKept(before, list, changed: [2, 3, 4, 6, 7, 9, 10]);
        Assert.Equal(rebuilt, list);
    }

    [Fact]
    public void Rows_that_stay_between_two_runs_of_rows_that_only_shift_count_for_no_reset_and_the_rows_told_of_in_all_runs_count_together()
    {
        // Rows 1 and 2 only shift (r01 gone, a new row after r02), and rows 64 and 65 (r64 gone, a new
        // row after r65): four rows told of, in two runs, with the sixty-one rows between them, which
        // show the same, left as they are.
        var list = Rows(70);
        var before = list.ToList();
        var rebuilt = Shifted(before, (1, 2), (64, 65));
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame, Row.SameItem);

        Assert.Equal([.. Removed(1, 2), .. Added(1, 2), .. Removed(64, 65), .. Added(64, 65)], changes);
        AssertKept(before, list, changed: [1, 2, 64, 65]);
        Assert.Equal(rebuilt, list);

        // Rows 1 to 32 and rows 40 to 71: sixty-four rows told of, in two runs with seven rows that
        // show the same between them, each run removed and inserted on its own.
        var two = Rows(80);
        var shown = two.ToList();
        var rebuiltTwo = Shifted(shown, (1, 32), (40, 71));
        var told = Record(two);

        two.ReplaceAll(rebuiltTwo, Row.ShowSame, Row.SameItem);

        Assert.Equal([.. Removed(1, 32), .. Added(1, 32), .. Removed(40, 71), .. Added(40, 71)], told);
        AssertKept(shown, two, changed: [.. Enumerable.Range(1, 32), .. Enumerable.Range(40, 32)]);
        Assert.Equal(rebuiltTwo, two);

        // The second run a row longer, rows 40 to 72: sixty-five rows told of, which reset the list,
        // though neither run has more than sixty-four.
        var longer = Rows(80);
        var more = Shifted(longer.ToList(), (1, 32), (40, 72));
        var resets = Record(longer);

        longer.ReplaceAll(more, Row.ShowSame, Row.SameItem);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.All(Enumerable.Range(0, more.Count), i => Assert.Same(more[i], longer[i]));
    }

    [Fact]
    public void The_form_for_rows_of_mixed_heights_is_told_what_the_same_item_is()
    {
        var list = Rows(3);

        Assert.Throws<ArgumentNullException>(() => list.ReplaceAll([.. list.Select(Copy)], Row.ShowSame, sameItem: null!));
    }

    [Fact]
    public void The_form_for_rows_of_one_height_removes_and_inserts_each_row_it_tells_of_whatever_its_item()
    {
        // The rebuild of the two runs above, told of row by row.
        var list = Updatable(16);
        var before = list.ToList();
        var rebuilt = TwoRuns(before);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Changing.ShowSame);

        Assert.Equal(Told(rowsOfOneHeight: true, 2, 3, 4, 8, 9, 12, 13), changes);
        AssertKept(before, list, changed: [2, 3, 4, 8, 9, 12, 13]);
        Assert.Equal(1, list[6].Version);
        Assert.All(new[] { 2, 3, 4, 8, 9, 12, 13 }, i => Assert.Same(rebuilt[i], list[i]));
    }

    /// <summary>
    /// Sixteen rows rebuilt with two runs of rows that only shift (2 to 4, 12 and 13), a run of two
    /// rows that show a new kind of their own item (8 and 9), and row 6 at a version it can show itself.
    /// </summary>
    private static List<Changing> TwoRuns(IReadOnlyList<Changing> before) =>
    [
        Same(before[0]), Same(before[1]), Same(before[3]), Same(before[4]), new Changing("x", 0, 0),
        Same(before[5]), new Changing(before[6].Name, 0, 1), Same(before[7]),
        new Changing(before[8].Name, 1, 0), new Changing(before[9].Name, 1, 0),
        Same(before[10]), Same(before[11]), Same(before[13]), new Changing("y", 0, 0), Same(before[14]), Same(before[15]),
    ];

    /// <summary>Fourteen rows rebuilt with rows 2 and 3, 6 and 7, and 10 and 11 shifted by a row, the rows between them as they were.</summary>
    private static List<Row> ThreeShiftedRuns(IReadOnlyList<Row> before) =>
    [
        Copy(before[0]), Copy(before[1]), Copy(before[3]), new Row("x", 0),
        Copy(before[4]), Copy(before[5]), Copy(before[7]), new Row("y", 0),
        Copy(before[8]), Copy(before[9]), Copy(before[11]), new Row("z", 0),
        Copy(before[12]), Copy(before[13]),
    ];

    /// <summary>
    /// The rows rebuilt with each of <paramref name="runs"/> shifted by a row: the row at its first
    /// index gone, the rows after it up to its last each a row higher, and a new row at its last.
    /// </summary>
    private static List<Row> Shifted(IReadOnlyList<Row> before, params (int First, int Last)[] runs)
    {
        var rebuilt = before.Select(Copy).ToList();
        foreach (var (first, last) in runs)
        {
            for (var at = first; at < last; at++)
            {
                rebuilt[at] = Copy(before[at + 1]);
            }

            rebuilt[last] = new Row(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"n{first:D2}"), 0);
        }

        return rebuilt;
    }

    /// <summary>
    /// The stage lists' form for rows of one height (<paramref name="rowsOfOneHeight"/>), the left
    /// panel's otherwise, with <paramref name="sameItem"/> saying which rows stand for the same item.
    /// </summary>
    private static void Replace<TRow>(RowCollection<TRow> list, IReadOnlyList<TRow> rebuilt, Func<TRow, TRow, bool> showSame, Func<TRow, TRow, bool> sameItem, bool rowsOfOneHeight)
    {
        if (rowsOfOneHeight)
        {
            list.ReplaceAll(rebuilt, showSame);
        }
        else
        {
            list.ReplaceAll(rebuilt, showSame, sameItem);
        }
    }

    /// <summary>A list of <paramref name="count"/> rows that can change their version themselves, r00 on, each of kind 0 at version 0.</summary>
    private static RowCollection<Changing> Updatable(int count)
    {
        var list = new RowCollection<Changing>();
        list.ReplaceAll(Enumerable.Range(0, count).Select(i => new Changing(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"r{i:D2}"), 0, 0)));
        return list;
    }

    /// <summary>A list of <paramref name="count"/> rows, r00 on, each at version 0.</summary>
    private static RowCollection<Row> Rows(int count)
    {
        var list = new RowCollection<Row>();
        list.ReplaceAll(Enumerable.Range(0, count).Select(i => new Row(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"r{i:D2}"), 0)));
        return list;
    }

    /// <summary>The rows rebuilt as new objects, those at <paramref name="changed"/> at a new version.</summary>
    private static List<Row> Rebuilt(IReadOnlyList<Row> rows, params int[] changed) =>
        [.. rows.Select((row, index) => changed.Contains(index) ? row with { Version = row.Version + 1 } : Copy(row))];

    private static Row Copy(Row row) => row with { };

    private static Changing Same(Changing row) => new(row.Name, row.Kind, row.Version);

    private static Changing Next(Changing row) => new(row.Name, row.Kind, row.Version + 1);

    /// <summary>The Removes of the rows from <paramref name="first"/> to <paramref name="last"/>, the last first.</summary>
    private static IEnumerable<(NotifyCollectionChangedAction Action, int Index)> Removed(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Reverse().Select(index => (NotifyCollectionChangedAction.Remove, index));

    /// <summary>The Adds of the rows from <paramref name="first"/> to <paramref name="last"/>, the first first.</summary>
    private static IEnumerable<(NotifyCollectionChangedAction Action, int Index)> Added(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(index => (NotifyCollectionChangedAction.Add, index));

    /// <summary>
    /// What the view is told of the rows at <paramref name="changed"/>, first to last: for rows of one
    /// height a Remove and an Add at each index, for other rows a Replace.
    /// </summary>
    private static List<(NotifyCollectionChangedAction Action, int Index)> Told(bool rowsOfOneHeight, params int[] changed) =>
        rowsOfOneHeight
            ? [.. changed.SelectMany(index => new[] { (NotifyCollectionChangedAction.Remove, index), (NotifyCollectionChangedAction.Add, index) })]
            : [.. changed.Select(index => (NotifyCollectionChangedAction.Replace, index))];

    private static List<(NotifyCollectionChangedAction Action, int Index)> Record(INotifyCollectionChanged list)
    {
        var changes = new List<(NotifyCollectionChangedAction Action, int Index)>();
        list.CollectionChanged += (_, e) => changes.Add((e.Action, e.Action == NotifyCollectionChangedAction.Remove ? e.OldStartingIndex : e.NewStartingIndex));
        return changes;
    }

    /// <summary>Every row of <paramref name="before"/> but those at <paramref name="changed"/> is the same object at the same index in <paramref name="list"/>.</summary>
    private static void AssertKept<TRow>(IReadOnlyList<TRow> before, IReadOnlyList<TRow> list, int[] changed)
        where TRow : class
    {
        Assert.Equal(before.Count, list.Count);
        for (var i = 0; i < before.Count; i++)
        {
            if (!changed.Contains(i))
            {
                Assert.Same(before[i], list[i]);
            }
        }
    }

    /// <summary>A row: what it shows is its name and its version.</summary>
    private sealed record Row(string Name, int Version)
    {
        public static bool ShowSame(Row shown, Row rebuilt) => shown == rebuilt;

        /// <summary>A row's name is its item, as a left-panel row's key is.</summary>
        public static bool SameItem(Row shown, Row rebuilt) => shown.Name == rebuilt.Name;
    }

    /// <summary>A row that shows its name, its kind and its version, and can change its version itself.</summary>
    private sealed class Changing(string name, int kind, int version) : IUpdatableRow<Changing>
    {
        public string Name { get; } = name;

        public int Kind { get; } = kind;

        public int Version { get; private set; } = version;

        public static bool ShowSame(Changing shown, Changing rebuilt) =>
            shown.Name == rebuilt.Name && shown.Kind == rebuilt.Kind && shown.Version == rebuilt.Version;

        public static bool SameItem(Changing shown, Changing rebuilt) => shown.Name == rebuilt.Name;

        public bool CanUpdateTo(Changing rebuilt) => rebuilt.Name == Name && rebuilt.Kind == Kind;

        public void UpdateTo(Changing rebuilt) => Version = rebuilt.Version;
    }
}
