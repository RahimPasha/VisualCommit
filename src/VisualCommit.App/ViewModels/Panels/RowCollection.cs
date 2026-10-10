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
    /// which moved the rows under the pointer.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<T> rows, Func<T, T, bool> showSame)
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
