using System.Collections.Specialized;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="RowCollection{T}.ReplaceAll(IReadOnlyList{T}, Func{T, T, bool})"/> on its own: which
/// rows a rebuild keeps, what it tells the view, and what the list holds afterwards. A rebuild with
/// as many rows as before removes and inserts again each row that shows something new and keeps
/// every other row, up to 64 such rows, and resets the list above that; a rebuild that changes the
/// number of rows keeps the rows before and after the part that changed.
/// </summary>
public class RowCollectionTests
{
    [Fact]
    public void A_rebuild_with_as_many_rows_and_two_changes_far_apart_replaces_those_two_alone()
    {
        var list = Rows(20);
        var before = list.ToList();
        var rebuilt = Rebuilt(before, 3, 15);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame);

        Assert.Equal([(NotifyCollectionChangedAction.Remove, 3), (NotifyCollectionChangedAction.Add, 3), (NotifyCollectionChangedAction.Remove, 15), (NotifyCollectionChangedAction.Add, 15)], changes);
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

    [Fact]
    public void Sixty_four_changed_rows_are_replaced_one_by_one_and_sixty_five_reset_the_list()
    {
        // Every other row from row 1 on: 64 changed rows with a kept row between each two.
        var list = Rows(130);
        var before = list.ToList();
        var changed = Enumerable.Range(0, 64).Select(i => 1 + (2 * i)).ToArray();
        var rebuilt = Rebuilt(before, changed);
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame);

        Assert.Equal(changed.SelectMany(index => new[] { (NotifyCollectionChangedAction.Remove, index), (NotifyCollectionChangedAction.Add, index) }), changes);
        AssertKept(before, list, changed);
        Assert.Equal(rebuilt, list);

        // One more: the list is reset once, and holds the rebuilt rows.
        var longer = Rows(132);
        var more = Rebuilt(longer.ToList(), [.. Enumerable.Range(0, 65).Select(i => 1 + (2 * i))]);
        var resets = Record(longer);

        longer.ReplaceAll(more, Row.ShowSame);

        Assert.Equal([(NotifyCollectionChangedAction.Reset, -1)], resets);
        Assert.Equal(more, longer);
        Assert.All(Enumerable.Range(0, more.Count), i => Assert.Same(more[i], longer[i]));
    }

    [Fact]
    public void A_rebuild_with_as_many_rows_whose_rows_only_shift_by_one_holds_the_rebuilt_rows()
    {
        // r02 is gone and n is new after r06: r03 to r06 each move up a row, and r07 on stay.
        var list = Rows(10);
        var before = list.ToList();
        List<Row> rebuilt = [Copy(before[0]), Copy(before[1]), .. before.Skip(3).Take(4).Select(Copy), new Row("n", 0), .. before.Skip(7).Select(Copy)];
        Assert.Equal(before.Count, rebuilt.Count);

        list.ReplaceAll(rebuilt, Row.ShowSame);

        Assert.Equal(rebuilt, list);
        Assert.Same(before[0], list[0]);
        Assert.Same(before[1], list[1]);
        Assert.All(Enumerable.Range(7, 3), i => Assert.Same(before[i], list[i]));
    }

    [Fact]
    public void A_rebuild_that_changes_the_number_of_rows_keeps_the_rows_before_and_after_the_part_that_changed()
    {
        var list = Rows(6);
        var before = list.ToList();
        List<Row> rebuilt = [Copy(before[0]), Copy(before[1]), new Row("x", 0), new Row("y", 0), new Row("z", 0), Copy(before[4]), Copy(before[5])];
        var changes = Record(list);

        list.ReplaceAll(rebuilt, Row.ShowSame);

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

    [Fact]
    public void A_rebuild_that_shows_the_same_tells_the_view_nothing_and_keeps_every_row()
    {
        var list = Rows(8);
        var before = list.ToList();
        var changes = Record(list);

        list.ReplaceAll([.. before.Select(Copy)], Row.ShowSame);

        Assert.Empty(changes);
        AssertKept(before, list, changed: []);
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

    private static List<(NotifyCollectionChangedAction Action, int Index)> Record(INotifyCollectionChanged list)
    {
        var changes = new List<(NotifyCollectionChangedAction Action, int Index)>();
        list.CollectionChanged += (_, e) => changes.Add((e.Action, e.Action == NotifyCollectionChangedAction.Remove ? e.OldStartingIndex : e.NewStartingIndex));
        return changes;
    }

    /// <summary>Every row of <paramref name="before"/> but those at <paramref name="changed"/> is the same object at the same index in <paramref name="list"/>.</summary>
    private static void AssertKept(IReadOnlyList<Row> before, IReadOnlyList<Row> list, int[] changed)
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
}
