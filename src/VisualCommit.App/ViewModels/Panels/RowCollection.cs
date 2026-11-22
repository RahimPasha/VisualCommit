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
    /// <summary>The most rows a partial update changes one by one; a larger change resets the list.</summary>
    private const int MaxPartialChange = 64;

    /// <summary>
    /// Replaces the rows with <paramref name="rows"/>, telling the view only about the part that
    /// changed when that part is small: the rows before and after it that
    /// <paramref name="showSame"/> calls equal stay as they are. A folder opened in a long list
    /// then inserts its children without the list laying itself out again from estimated sizes,
    /// which moved the rows under the pointer. When the part that changed has as many rows as
    /// before, each of its rows is compared with the one rebuilt in its place, and only those that
    /// show something new are changed, one by one (<see cref="ReplaceChanged"/>): two rows marked
    /// far apart (a refresh's counts, the hidden branches' tooltips once the graph without them is
    /// shown) leave every row between them, and its control, as it was, so that a press on one of
    /// those rows is still a click when its release comes after the rebuild.
    /// <paramref name="rowsOfOneHeight"/> says whether the view shows every row at one height (the
    /// stage lists' 24 px), which decides how the view is told of a row that cannot change itself.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<T> rows, Func<T, T, bool> showSame, bool rowsOfOneHeight)
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
            ReplaceChanged(rows, prefix, removed, showSame, rowsOfOneHeight);
            return;
        }

        if (removed + added > MaxPartialChange)
        {
            ReplaceAll(rows);
            return;
        }

        for (var i = removed - 1; i >= 0; i--)
        {
            RemoveAt(prefix + i);
        }

        for (var i = 0; i < added; i++)
        {
            Insert(prefix + i, rows[prefix + i]);
        }
    }

    /// <summary>
    /// The <paramref name="count"/> rows from <paramref name="start"/> on, where as many rows were
    /// rebuilt: a row that does not show the same as its rebuilt row changes itself to show it when
    /// it is an <see cref="IUpdatableRow{T}"/> that can, and the list tells the view nothing of it;
    /// each other such row gives way to its rebuilt row, from the first to the last, and the view is
    /// told of each, unless more than <see cref="MaxPartialChange"/> do, which resets the list. Rows
    /// of one height are removed and the rebuilt row inserted at their index; other rows are replaced.
    /// <para>
    /// Avalonia's virtualising stack panel (12.1.3) keeps the place of the first row it has made, and
    /// finds the row at the top of the view by adding the heights of the rows it has made from there.
    /// A replace of a row above those, or of one below the first, leaves the rows in view where they
    /// are. A replace of the first, the top row in view, empties its slot and keeps that place, and
    /// the search passes over the empty slot without its height, as over those of the rows replaced
    /// right below it: the first row kept below them is placed where the top row in view was, and the
    /// scroll viewer moves the offset by the heights of all the rows replaced above that row. Nothing
    /// moves in the window then, but the offset and the extent end that much short, and the rows jump
    /// by it when the list is scrolled back to its top; when the offset is smaller than those heights,
    /// the list goes to its top at once and makes every row's control again, the click under a press
    /// lost with them. A solo, which dims every branch row but the soloed one and HEAD's, did that to
    /// the left panel when its rows were replaced.
    /// </para>
    /// <para>
    /// A remove at or above the first row the panel has made, and an insert above it, make it forget
    /// that row's place and estimate the top row in view as the offset divided by the mean height of
    /// the rows it has made. That is exact when every row has one height, as in the stage lists, so a
    /// remove and an insert leave them where they were in each case. Where the heights differ, as in
    /// the left panel, whose 30 px headers sit among 26 px rows, the estimate misses by rows deep in
    /// the list, and the panel then makes the controls of the rows in view again. So the left panel's
    /// rows change themselves where they can, and a hide, a solo and its end, a refresh's commits and
    /// counts, and a branch's pull request tell the view nothing of the list. A left-panel row that
    /// cannot change itself is still replaced, with the cost above: one whose key, text or opening
    /// changes at its index (rows that only shift, a message of the pull requests' section that
    /// gives way to another), and a pull request row that shows something new.
    /// </para>
    /// </summary>
    private void ReplaceChanged(IReadOnlyList<T> rows, int start, int count, Func<T, T, bool> showSame, bool rowsOfOneHeight)
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

        foreach (var index in changed)
        {
            if (rowsOfOneHeight)
            {
                RemoveAt(index);
                Insert(index, rows[index]);
            }
            else
            {
                this[index] = rows[index];
            }
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
