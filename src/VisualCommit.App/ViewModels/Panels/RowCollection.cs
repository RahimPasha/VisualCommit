using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// A list of rows that a panel rebuilds whole when something changes (a folder opened, the
/// filter typed into, a new commit shown). <see cref="ReplaceAll"/> swaps the content with one
/// reset notification, so the virtualised list in the view lays itself out once instead of once
/// per row.
/// </summary>
public sealed class RowCollection<T> : ObservableCollection<T>
{
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
