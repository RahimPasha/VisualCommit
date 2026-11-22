using System.Collections.Specialized;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="RowCollection{T}.ReplaceAll(IReadOnlyList{T}, Func{T, T, bool}, bool)"/> on its own: which
/// rows a rebuild keeps, what it tells the view, and what the list holds afterwards. A rebuild with
/// as many rows as before changes each row that shows something new and keeps every other row: a
/// row that can show the new content itself (<see cref="IUpdatableRow{T}"/>, the left panel's
/// branch rows and headers) does, and the view is told nothing of it; of the others, up to 64 are
/// told of one by one, and more reset the list: for rows of one height (the stage lists) each such
/// row is removed and its rebuilt row inserted, for other rows (the left panel) it is replaced. A
/// rebuild that changes the number of rows keeps the rows before and after the part that changed,
/// for either.
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

        list.ReplaceAll(rebuilt, Row.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll(rebuilt, Row.ShowSame, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, changed), changes);
        AssertKept(before, list, changed);
        Assert.Equal(rebuilt, list);

        // One more: the list is reset once, and holds the rebuilt rows.
        var longer = Rows(132);
        var more = Rebuilt(longer.ToList(), [.. Enumerable.Range(0, 65).Select(i => 1 + (2 * i))]);
        var resets = Record(longer);

        longer.ReplaceAll(more, Row.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll(rebuilt, Row.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll(rebuilt, Row.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll([.. before.Select(Copy)], Row.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll(rebuilt, Changing.ShowSame, rowsOfOneHeight);

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

        list.ReplaceAll([.. before.Select(row => new Changing(row.Name, row.Kind, row.Version + 1))], Changing.ShowSame, rowsOfOneHeight);

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

        mixed.ReplaceAll(rebuilt, Changing.ShowSame, rowsOfOneHeight);

        Assert.Equal(Told(rowsOfOneHeight, kinds), told);
        AssertKept(shown, mixed, kinds);
        Assert.All(Enumerable.Range(0, rebuilt.Count), i => Assert.True(Changing.ShowSame(mixed[i], rebuilt[i]), $"Row {i} does not show what its rebuilt row shows."));

        // Sixty-five such rows reset the list once, and it holds the rebuilt rows.
        var longer = Updatable(132);
        var more = Enumerable.Range(0, 65).Select(i => 1 + (2 * i)).ToArray();
        List<Changing> rebuiltMore = [.. longer.ToList().Select((row, index) => more.Contains(index) ? new Changing(row.Name, row.Kind + 1, row.Version) : new Changing(row.Name, row.Kind, row.Version + 1))];
        var resets = Record(longer);

        longer.ReplaceAll(rebuiltMore, Changing.ShowSame, rowsOfOneHeight);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.All(Enumerable.Range(0, rebuiltMore.Count), i => Assert.Same(rebuiltMore[i], longer[i]));
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
    }

    /// <summary>A row that shows its name, its kind and its version, and can change its version itself.</summary>
    private sealed class Changing(string name, int kind, int version) : IUpdatableRow<Changing>
    {
        public string Name { get; } = name;

        public int Kind { get; } = kind;

        public int Version { get; private set; } = version;

        public static bool ShowSame(Changing shown, Changing rebuilt) =>
            shown.Name == rebuilt.Name && shown.Kind == rebuilt.Kind && shown.Version == rebuilt.Version;

        public bool CanUpdateTo(Changing rebuilt) => rebuilt.Name == Name && rebuilt.Kind == Kind;

        public void UpdateTo(Changing rebuilt) => Version = rebuilt.Version;
    }
}
