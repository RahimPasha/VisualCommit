using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// A list of rows that a panel rebuilds whole when something changes (a folder opened, the
/// filter typed into, a new commit shown). <see cref="ReplaceAll(IEnumerable{T})"/> swaps the
/// content with one reset notification, so the virtualised list in the view lays itself out once
/// instead of once per row.
/// </summary>
public sealed class RowCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// The most rows a partial update tells the view of one by one; more reset the list. A rebuild
    /// that changes the number of rows counts the rows it removes and those it inserts; one with as
    /// many rows counts the rows it tells of, a run removed and inserted each of its rows once.
    /// </summary>
    private const int MaxPartialChange = 64;

    /// <summary>
    /// Replaces the rows of a list that shows every row at one height (the stage lists) with
    /// <paramref name="rows"/>, as <see cref="ReplaceAll(IReadOnlyList{T}, Func{T, T, bool}, Func{T, T, bool})"/>
    /// does, but each row it tells the view of is removed and its rebuilt row inserted at its index,
    /// whatever the item it stands for, which leaves rows of one height where they were
    /// (<see cref="ReplaceChanged"/>).
    /// </summary>
    public void ReplaceAll(IReadOnlyList<T> rows, Func<T, T, bool> showSame) => Replace(rows, showSame, sameItem: null);

    /// <summary>
    /// Replaces the rows with <paramref name="rows"/>, telling the view only about the part that
    /// changed when that part is small: the rows before and after it that
    /// <paramref name="showSame"/> calls equal stay as they are. A folder opened in a long list
    /// then inserts its children without the list laying itself out again from estimated sizes,
    /// which moved the rows under the pointer. When the part that changed has as many rows as
    /// before, each of its rows is compared with the one rebuilt in its place, and only those that
    /// show something new are changed (<see cref="ReplaceChanged"/>): two rows marked far apart (a
    /// refresh's counts, the hidden branches' tooltips once the graph without them is shown) leave
    /// every row between them, and its control, as it was, so that a press on one of those rows is
    /// still a click when its release comes after the rebuild. This is the form for a list whose
    /// rows differ in height (the left panel); <paramref name="sameItem"/> says whether a row and the
    /// row rebuilt at its index stand for the same item (the left panel: the same key).
    /// </summary>
    public void ReplaceAll(IReadOnlyList<T> rows, Func<T, T, bool> showSame, Func<T, T, bool> sameItem)
    {
        ArgumentNullException.ThrowIfNull(sameItem);
        Replace(rows, showSame, sameItem);
    }

    /// <summary>The two forms of <c>ReplaceAll</c> with a comparison: <paramref name="sameItem"/> is null for rows of one height.</summary>
    private void Replace(IReadOnlyList<T> rows, Func<T, T, bool> showSame, Func<T, T, bool>? sameItem)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(showSame);

        var prefix = 0;
        while (prefix < Count && prefix < rows.Count && showSame(this[prefix], rows[prefix]))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < Count - prefix && suffix < rows.Count - prefix && showSame(this[Count - 1 - suffix], rows[rows.Count - 1 - suffix]))
        {
            suffix++;
        }

        var removed = Count - prefix - suffix;
        var added = rows.Count - prefix - suffix;
        if (removed == added)
        {
            ReplaceChanged(rows, prefix, removed, showSame, sameItem);
            return;
        }

        if (removed + added > MaxPartialChange)
        {
            ReplaceAll(rows);
            return;
        }

        RemoveAndInsert(rows, prefix, removed, added);
    }

    /// <summary>
    /// The <paramref name="count"/> rows from <paramref name="start"/> on, where as many rows were
    /// rebuilt. A row that shows the same as its rebuilt row stays. A row that does not, and is an
    /// <see cref="IUpdatableRow{T}"/> that can show it, changes itself, and the list tells the view
    /// nothing of it: in the left panel a branch, remote, folder, tag or stash row whose key, kind,
    /// name, tooltip, level and opening stay takes its new marks, commit, HEAD, counts, selection and
    /// pull request so, and a header its new count. Every other such row is told of, unless more than
    /// <see cref="MaxPartialChange"/> are, which resets the list. For rows of one height
    /// (<paramref name="sameItem"/> null) each is removed and its rebuilt row inserted at its index.
    /// For rows of mixed heights they go by runs, each run the most consecutive rows told of: a run
    /// whose rows all stand for the same items as their rebuilt rows is replaced row by row; a run in
    /// which a row stands for another item than its rebuilt row is removed, its last row first, and
    /// its rebuilt rows inserted, the first first, as the whole changed part was before phase 8a. In
    /// the left panel the first are rows whose key stays but which cannot change themselves (a pull
    /// request row that shows something new, or whose request changed; a message row of the pull
    /// requests' section whose text changes; a ref row whose name, tooltip, level or opening changes
    /// under its key); the second are rows that only shift, which a branch renamed in the app or
    /// outside it, its undo, or a fetch that drops one remote branch and brings another give every
    /// row between the old place and the new one, and a message row that gives way to another kind.
    /// <para>
    /// What the view does with each, in Avalonia's virtualising stack panel (12.1.3), which keeps the
    /// place of the first row it has made and finds the row at the top of the view by adding the
    /// heights of the rows it has made from there: a replace of a row above those rows, or of one
    /// below the first, leaves the rows in view where they are. A replace of the first, the top row
    /// in view, empties its slot and keeps that place, and the search passes over the empty slot
    /// without its height, as over those of the rows replaced right below it: the first row kept
    /// below them is placed where the top row in view was, and the scroll viewer moves the offset by
    /// the heights of all the consecutive rows replaced from the top row in view down to the first row
    /// not replaced. Nothing moves in the window then, but the offset and the extent end that much
    /// short, and the rows jump by it when the list is scrolled back to its top; when the offset is
    /// smaller than those heights, the list goes to its top at once and makes every row's control
    /// again, the click under a press lost with them. That is the cost the left panel's rows replaced
    /// row by row still carry, a known limit, met when such a row is the first row the panel has
    /// made, the top row in view. A solo, which dims every branch row
    /// but the soloed one and HEAD's, sent the list to its top so while those rows were replaced, and
    /// so did a rename while the rows it shifts were (20 branches scrolled by 100 px, the top row
    /// renamed to sort last: 18 rows replaced, 468 px).
    /// </para>
    /// <para>
    /// A remove at or above the first row the panel has made, and an insert above it, make it forget
    /// that row's place and estimate the top row in view as the offset divided by the mean height of
    /// the rows it has made; a run removed and inserted below that row leaves the rows above it where
    /// they are, and its own rows, other items, get new controls in any case. The estimate is exact when every
    /// row has one height, as in the stage lists, so a remove and an insert leave them where they
    /// were in each case tried. Where the heights differ, as in the left panel, whose 30 px headers
    /// sit among 26 px rows, the estimate misses, as it did before phase 8a: by pixels near the top
    /// of the list (the rename above leaves the offset at 105 for 100 and the extent 5 px longer,
    /// the rows where they were, the click kept), and by rows deep in a list with headers in view,
    /// where the panel makes the controls of rows in view again and a press on one of them loses its
    /// click, or draws the rows in view somewhere else. So, at the bottom of 60 branches, a rename of
    /// b01, far above the view, leaves the offset at 1226 for 1356 and five rows in view with new
    /// controls, where a replace of the two rows it shifts would have touched nothing; and with 20
    /// local and 20 remote branches scrolled down to origin/r03 at the top of the view, a fetch that
    /// drops r03 and brings r99 moves the rows in view 87 px down. A rebuild that changes the number
    /// of rows removes and inserts its changed part in the same way.
    /// </para>
    /// </summary>
    private void ReplaceChanged(IReadOnlyList<T> rows, int start, int count, Func<T, T, bool> showSame, Func<T, T, bool>? sameItem)
    {
        var updated = new List<(IUpdatableRow<T> Row, T Rebuilt)>();
        var changed = new List<int>();
        for (var i = start; i < start + count; i++)
        {
            if (showSame(this[i], rows[i]))
            {
                continue;
            }

            if (this[i] is IUpdatableRow<T> shown && shown.CanUpdateTo(rows[i]))
            {
                updated.Add((shown, rows[i]));
            }
            else
            {
                changed.Add(i);
            }
        }

        if (changed.Count > MaxPartialChange)
        {
            ReplaceAll(rows);
            return;
        }

        foreach (var (row, rebuilt) in updated)
        {
            row.UpdateTo(rebuilt);
        }

        if (sameItem is null)
        {
            foreach (var index in changed)
            {
                RemoveAndInsert(rows, index, 1, 1);
            }

            return;
        }

        // The runs of consecutive rows told of, first to last; each keeps its number of rows.
        for (var first = 0; first < changed.Count;)
        {
            var last = first;
            while (last + 1 < changed.Count && changed[last + 1] == changed[last] + 1)
            {
                last++;
            }

            var runStart = changed[first];
            var runCount = last - first + 1;
            if (Enumerable.Range(runStart, runCount).All(index => sameItem(this[index], rows[index])))
            {
                for (var index = runStart; index < runStart + runCount; index++)
                {
                    this[index] = rows[index];
                }
            }
            else
            {
                RemoveAndInsert(rows, runStart, runCount, runCount);
            }

            first = last + 1;
        }
    }

    /// <summary>
    /// Removes the <paramref name="removed"/> rows from <paramref name="start"/> on, the last first,
    /// then inserts the <paramref name="added"/> rows of <paramref name="rows"/> from that index on,
    /// the first first.
    /// </summary>
    private void RemoveAndInsert(IReadOnlyList<T> rows, int start, int removed, int added)
    {
        for (var i = removed - 1; i >= 0; i--)
        {
            RemoveAt(start + i);
        }

        for (var i = 0; i < added; i++)
        {
            Insert(start + i, rows[start + i]);
        }
    }

    /// <summary>
    /// Adds <paramref name="rows"/> after the last row and tells the view once, with one Add of
    /// them all: a list that grows as git prints (the file history) lays itself out once per batch
    /// instead of once per row, and keeps its selection, which a reset would drop.
    /// </summary>
    public void AddRange(IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        var start = Count;
        foreach (var row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, rows.ToList(), start));
    }

    /// <summary>Replaces every row with <paramref name="rows"/> and tells the view once.</summary>
    public void ReplaceAll(IEnumerable<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        CheckReentrancy();
        Items.Clear();
        foreach (var row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

/// <summary>
/// A row that can come to show what the row rebuilt in its place shows by changing its own
/// properties, which tell its control of themselves: <see cref="RowCollection{T}"/> then keeps the
/// row, and its control, and tells the view nothing of the list.
/// </summary>
/// <typeparam name="T">The rows of the list.</typeparam>
public interface IUpdatableRow<in T>
{
    /// <summary>Whether this row can show what <paramref name="rebuilt"/> shows by changing its own properties.</summary>
    bool CanUpdateTo(T rebuilt);

    /// <summary>Makes this row show what <paramref name="rebuilt"/> shows, when <see cref="CanUpdateTo"/> says it can.</summary>
    void UpdateTo(T rebuilt);
}
