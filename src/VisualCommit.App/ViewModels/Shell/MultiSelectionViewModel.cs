using System.Globalization;

namespace VisualCommit.App.ViewModels.Shell;

/// <summary>
/// What the details panel shows while two or more commits are selected in the graph
/// (docs/phase-3-design.md 4h): how many, and each one's short id and subject in row order, then
/// a hint. Immutable: the repository makes a new one whenever the selection or the graph changes.
/// The view is <c>Views/Shell/MultiSelectionView</c>, found by its name.
/// </summary>
public sealed class MultiSelectionViewModel
{
    /// <summary>The hint under the list.</summary>
    public const string HintText = "Right-click a selected commit for what can be done with all of them.";

    /// <param name="items">The selected commits in row order.</param>
    public MultiSelectionViewModel(IReadOnlyList<MultiSelectionItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        Title = string.Create(CultureInfo.InvariantCulture, $"{items.Count} commits selected");
    }

    /// <summary>"3 commits selected".</summary>
    public string Title { get; }

    /// <summary>The selected commits, in the graph's row order.</summary>
    public IReadOnlyList<MultiSelectionItem> Items { get; }

    public string Hint => HintText;
}

/// <summary>One selected commit in the details panel's list.</summary>
/// <param name="Sha">Its full id.</param>
/// <param name="ShortSha">Its first 7 characters.</param>
/// <param name="Subject">The first line of its message.</param>
public sealed record MultiSelectionItem(string Sha, string ShortSha, string Subject)
{
    /// <summary>The name UI Automation gives the row: the short id and the subject.</summary>
    public string AccessibleName => ShortSha + " " + Subject;
}
