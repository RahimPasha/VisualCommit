using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// One item of the pick dialog (docs/phase-6-design.md 7.7): its text, which is what the dialog
/// answers and is unique within a list, and an optional detail drawn after it in the secondary
/// colour (an editor's program, a merge tool's description).
/// </summary>
/// <param name="Text">The item's text, the answer.</param>
/// <param name="Detail">The detail, or null for none.</param>
public sealed record PickOption(string Text, string? Detail = null);

/// <summary>
/// The pick dialog (<c>PickDialog</c>, docs/phase-3-design.md 2.21, 4f): a filter box
/// (<c>PickFilter</c>) over a list (<c>PickList</c>, items <c>PickItem</c> named by their text),
/// such as "Set upstream for topic" over the remote branches. The confirm button needs an item
/// selected. Typing in the filter keeps the items that contain it, ignoring case; a selected item
/// that the filter hides is no longer selected. Answers the selected item, or null when cancelled.
/// Since phase 6 an item may have a detail (docs/phase-6-design.md 7.7: the settings screen's
/// "Choose an editor" and "Choose a merge tool"), which the filter matches too; the items, the
/// selection and the answer stay the texts.
/// </summary>
public sealed partial class PickDialogViewModel : DialogViewModel<string?>
{
    private readonly IReadOnlyList<PickOption> _all;

    /// <param name="title">The card's title, such as "Set upstream for topic".</param>
    /// <param name="items">The items, in the order shown.</param>
    /// <param name="selected">The item selected when it opens (the current upstream), or null.</param>
    /// <param name="confirmText">The confirm button's label, such as "Set upstream".</param>
    public PickDialogViewModel(string title, IReadOnlyList<string> items, string? selected, string confirmText)
        : this(title, OptionsOf(items), selected, confirmText)
    {
    }

    /// <param name="title">The card's title, such as "Choose an editor".</param>
    /// <param name="options">The items with their details, in the order shown; their texts are unique.</param>
    /// <param name="selected">The text of the item selected when it opens, or null.</param>
    /// <param name="confirmText">The confirm button's label, such as "Choose".</param>
    public PickDialogViewModel(string title, IReadOnlyList<PickOption> options, string? selected, string confirmText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(confirmText);
        Title = title;
        ConfirmText = confirmText;
        _all = options;
        Items = [.. options.Select(option => option.Text)];
        Selected = selected is not null && Items.Contains(selected) ? selected : null;
    }

    public override string Title { get; }

    public string ConfirmText { get; }

    /// <summary>Every item with its detail, in the order given, whatever the filter lets through.</summary>
    public IReadOnlyList<PickOption> Options => _all;

    /// <summary>The texts of the items the filter lets through, in the order given.</summary>
    public ObservableCollection<string> Items { get; }

    /// <summary>What the filter box holds.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>The selected item's text, or null.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? Selected { get; set; }

    protected override string? CancelledResult => null;

    /// <summary>The detail of the item <paramref name="text"/>, or null when it has none.</summary>
    public string? DetailOf(string? text) =>
        text is null ? null : _all.FirstOrDefault(option => string.Equals(option.Text, text, StringComparison.Ordinal))?.Detail;

    partial void OnFilterChanged(string value)
    {
        var shown = _all
            .Where(option => option.Text.Contains(value, StringComparison.OrdinalIgnoreCase)
                || (option.Detail?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(option => option.Text)
            .ToList();
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

    private static IReadOnlyList<PickOption> OptionsOf(IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items.Select(item => new PickOption(item))];
    }

    private bool CanConfirm() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Complete(Selected);
}
