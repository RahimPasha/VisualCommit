using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The pick dialog (<c>PickDialog</c>, docs/phase-3-design.md 2.21, 4f): a filter box
/// (<c>PickFilter</c>) over a list (<c>PickList</c>, items <c>PickItem</c> named by their text),
/// such as "Set upstream for topic" over the remote branches. The confirm button needs an item
/// selected. Typing in the filter keeps the items that contain it, ignoring case; a selected item
/// that the filter hides is no longer selected. Answers the selected item, or null when cancelled.
/// </summary>
public sealed partial class PickDialogViewModel : DialogViewModel<string?>
{
    private readonly IReadOnlyList<string> _all;

    /// <param name="title">The card's title, such as "Set upstream for topic".</param>
    /// <param name="items">The items, in the order shown.</param>
    /// <param name="selected">The item selected when it opens (the current upstream), or null.</param>
    /// <param name="confirmText">The confirm button's label, such as "Set upstream".</param>
    public PickDialogViewModel(string title, IReadOnlyList<string> items, string? selected, string confirmText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(confirmText);
        Title = title;
        ConfirmText = confirmText;
        _all = items;
        Items = [.. items];
        Selected = selected is not null && items.Contains(selected) ? selected : null;
    }

    public override string Title { get; }

    public string ConfirmText { get; }

    /// <summary>The items the filter lets through, in the order given.</summary>
    public ObservableCollection<string> Items { get; }

    /// <summary>What the filter box holds.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>The selected item, or null.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Selected { get; set; }

    protected override string? CancelledResult => null;

    partial void OnFilterChanged(string value)
    {
        var shown = _all.Where(item => item.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
        if (Selected is not null && !shown.Contains(Selected))
        {
            Selected = null;
        }

        // Rebuilt in place, so the list keeps its selection of an item that stays.
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!shown.Contains(Items[i]))
            {
                Items.RemoveAt(i);
            }
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (i >= Items.Count || Items[i] != shown[i])
            {
                Items.Insert(i, shown[i]);
            }
        }
    }

    private bool CanConfirm() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Complete(Selected);
}
