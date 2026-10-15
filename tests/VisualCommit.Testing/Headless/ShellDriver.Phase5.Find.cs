using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 5's part F of the driver (docs/test-reports/phase-5.md, "Conventions of part F"; design
/// 15.2, package WP7): the search bar and its marks read from the graph control's state (never
/// from pixels), the comparison in the right panel, the left panel's marks of hidden and soloed
/// branches, and the waits for a search, a filter's reload and a comparison. The real-window pass's
/// twins find the same controls by automation id and name (<c>RealApp.Phase5*.cs</c>).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The search bar's automation id (design 13.4).</summary>
    public const string SearchBarId = "SearchBar";

    /// <summary>The comparison's automation id (design 13.4).</summary>
    public const string CompareId = "Compare";

    private RepositoryViewModel ActiveRepository =>
        Session.Shell.ActiveTab.Repository ?? throw new InvalidOperationException("The active tab has no repository.");

    /// <summary>
    /// Waits until the search of the box's current text and mode has ended and its matches are
    /// applied (part F's convention): the box's delay has passed (300 ms in the Commits mode, 600 ms
    /// in the others), git has searched every row, and the marks are drawn from them. A wait on the
    /// condition, never on a time.
    /// </summary>
    /// <exception cref="InvalidOperationException">The search bar is not open.</exception>
    public async Task WaitForSearchAsync(TimeSpan? timeout = null)
    {
        var search = ActiveRepository.SearchBar ?? throw new InvalidOperationException("The search bar is not open.");
        await WaitForAsync(() => search.IsSettled && ReferenceEquals(ActiveRepository.SearchMarks, search.Marks), "the search to end and its matches to be applied", timeout);
    }

    /// <summary>Waits until the comparison in the right panel has been read (part F's convention): <c>Loading…</c> is gone and, for branches, the counts show.</summary>
    /// <exception cref="InvalidOperationException">No comparison is shown.</exception>
    public async Task WaitForCompareAsync(TimeSpan? timeout = null)
    {
        var comparison = ActiveRepository.Comparison ?? throw new InvalidOperationException("The right panel shows no comparison.");
        await WaitForAsync(
            () => ReferenceEquals(ActiveRepository.Comparison, comparison) && !comparison.IsLoading && (!comparison.IsBranches || comparison.HasCounts || comparison.HasError || comparison.HasNotice),
            "the comparison to be read",
            timeout);
    }

    /// <summary>
    /// What the graph marks of the search (design 3.3, 15.2), read from the graph control's
    /// <c>Search</c> and its rows, never from pixels: whether a search is active, and which commit
    /// rows (as <c>SelectedIndex</c> counts them) are marked and which dimmed. The working-changes
    /// row is neither (FD2).
    /// </summary>
    public SearchMarksSnapshot SearchMarks()
    {
        Settle();
        var graph = Find<CommitGraphControl>("GraphRows");
        var marks = graph.Search;
        var data = graph.Data;
        var marked = new List<int>();
        var dimmed = new List<int>();
        if (marks.IsActive && data is not null)
        {
            for (var row = 0; row < data.Count; row++)
            {
                (marks.IsMatch(data.CommitAt(row).Sha) ? marked : dimmed).Add(row);
            }
        }

        return new SearchMarksSnapshot(marks.IsActive, marked, dimmed);
    }

    /// <summary>
    /// The search bar as it shows (design 3.1): the mode, the text and placeholder, whether the box
    /// has the keyboard focus, the count, previous and next with their tooltips, the close button's
    /// tooltip, the progress bar, and the parts' bounds in the window. Null while it is closed.
    /// </summary>
    public SearchBarSnapshot? SearchBar()
    {
        Settle();
        var bar = Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == SearchBarId && control.IsEffectivelyVisible);
        if (bar is null)
        {
            return null;
        }

        var box = Named<TextBox>(bar, "SearchBox");
        var count = Named<TextBlock>(bar, "SearchCount");
        var previous = Named<Button>(bar, "SearchPrevious");
        var next = Named<Button>(bar, "SearchNext");
        var close = Named<Button>(bar, "SearchClose");
        var mode = Named<Button>(bar, "SearchModeButton");
        var progress = Named<ProgressBar>(bar, "SearchProgress");
        return new SearchBarSnapshot(
            Named<TextBlock>(bar, "SearchModeText").Text ?? string.Empty,
            box.Text ?? string.Empty,
            box.PlaceholderText ?? string.Empty,
            box.IsKeyboardFocusWithin,
            count.Text ?? string.Empty,
            count.IsEffectivelyVisible,
            ToolTip.GetTip(count) as string,
            previous.IsEffectivelyEnabled,
            next.IsEffectivelyEnabled,
            ToolTip.GetTip(previous) as string,
            ToolTip.GetTip(next) as string,
            ToolTip.GetTip(close) as string,
            progress.IsEffectivelyVisible,
            ToolTip.GetTip(progress) as string,
            BoundsOf(bar),
            BoundsOf(mode),
            BoundsOf(box),
            BoundsOf(count),
            BoundsOf(previous),
            BoundsOf(next),
            BoundsOf(close));
    }

    /// <summary>Chooses a search mode as a user does: a click on the mode button, then on the item labelled <paramref name="label"/> (<c>Files: a path or file name</c>).</summary>
    public void ChooseSearchMode(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Click(FindByAutomationId("SearchModeButton"));
        ClickMenuItem(label);
    }

    /// <summary>
    /// The comparison in the right panel (design 6.4): its title, From and To with their texts,
    /// whether their links are enabled and their tooltips, the counts, the switch, whether Swap and
    /// Close show, <c>Loading…</c>, what it says about itself, the files' title and phase 1's rows
    /// of its list. Null while none is shown.
    /// </summary>
    public CompareSnapshot? Compare()
    {
        Settle();
        var view = Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == CompareId && control.IsEffectivelyVisible);
        if (view is null || view.DataContext is not CompareViewModel comparison)
        {
            return null;
        }

        var from = Named<Button>(view, "CompareFrom");
        var to = Named<Button>(view, "CompareTo");
        var counts = Named<TextBlock>(view, "CompareCounts");
        var all = Named<CheckBox>(view, "CompareAllDifferences");
        var notice = Named<TextBlock>(view, "CompareNotice");
        var filesTitle = Named<TextBlock>(view, "CompareFilesTitle");
        return new CompareSnapshot(
            Named<TextBlock>(view, "CompareTitle").Text ?? string.Empty,
            comparison.FromText,
            from.IsEffectivelyEnabled,
            ToolTip.GetTip(from) as string,
            comparison.ToText,
            to.IsEffectivelyEnabled,
            ToolTip.GetTip(to) as string,
            counts.IsEffectivelyVisible ? counts.Text : null,
            all.IsEffectivelyVisible,
            all.IsChecked == true,
            Named<Button>(view, "CompareSwap").IsEffectivelyVisible,
            Named<Button>(view, "CompareClose").IsEffectivelyVisible,
            Named<TextBlock>(view, "CompareLoading").IsEffectivelyVisible,
            notice.IsEffectivelyVisible ? notice.Text : null,
            filesTitle.IsEffectivelyVisible ? filesTitle.Text : null,
            [.. comparison.Files.Where(row => row.IsFile)]);
    }

    /// <summary>
    /// Clicks the row of <paramref name="path"/> (relative to the working tree, <c>src/Reader.cs</c>)
    /// in the comparison's list (<c>CompareFileList</c>), found by its path (the row's key), since a
    /// file name can repeat in a flat list: its diff opens in the graph area.
    /// </summary>
    /// <exception cref="InvalidOperationException">The list has no such file.</exception>
    public void ClickCompareFile(string path) => Click(CompareFileRow(path));

    /// <summary>The laid-out row of <paramref name="path"/> in the comparison's list: what a click (its diff) or a right-click (its menu) is aimed at.</summary>
    /// <exception cref="InvalidOperationException">The list has no such file.</exception>
    public Button CompareFileRow(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var list = Find<ItemsControl>("CompareFileList");
        var rows = ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>())).ToList();
        var index = rows.FindIndex(row => row.IsFile && row.Key == path);
        if (index < 0)
        {
            throw new InvalidOperationException($"CompareFileList has no file {path}; it has: {string.Join(", ", rows.Where(row => row.IsFile).Select(row => row.Key))}.");
        }

        list.ScrollIntoView(index);
        Settle();
        return list.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => ReferenceEquals(button.DataContext, rows[index]) && button.Classes.Contains("fileRow") && button.IsEffectivelyVisible && button.Bounds.Height > 0)
            ?? throw new InvalidOperationException($"The row of {path} in CompareFileList is not laid out.");
    }

    private static T Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named '{name}' is in the {scope.GetType().Name}.");
}

/// <summary>What the graph marks of a search (design 3.3), as <see cref="ShellDriver.SearchMarks"/> reads it.</summary>
/// <param name="IsActive">A search with a text is active: the graph marks and dims.</param>
/// <param name="Marked">The marked commit rows, top first.</param>
/// <param name="Dimmed">The dimmed commit rows, top first.</param>
public sealed record SearchMarksSnapshot(bool IsActive, IReadOnlyList<int> Marked, IReadOnlyList<int> Dimmed);

/// <summary>The search bar as it shows (design 3.1), as <see cref="ShellDriver.SearchBar"/> reads it; bounds in the window.</summary>
public sealed record SearchBarSnapshot(
    string Mode,
    string Text,
    string Placeholder,
    bool BoxHasFocus,
    string Count,
    bool CountShown,
    string? CountToolTip,
    bool PreviousEnabled,
    bool NextEnabled,
    string? PreviousToolTip,
    string? NextToolTip,
    string? CloseToolTip,
    bool ProgressShown,
    string? ProgressToolTip,
    Rect Bounds,
    Rect ModeButtonBounds,
    Rect BoxBounds,
    Rect CountBounds,
    Rect PreviousBounds,
    Rect NextBounds,
    Rect CloseBounds);

/// <summary>The comparison in the right panel (design 6.4), as <see cref="ShellDriver.Compare"/> reads it.</summary>
/// <param name="Files">The list's file rows, phase 1's, in the order shown.</param>
public sealed record CompareSnapshot(
    string Title,
    string From,
    bool FromEnabled,
    string? FromToolTip,
    string To,
    bool ToEnabled,
    string? ToToolTip,
    string? Counts,
    bool AllDifferencesShown,
    bool AllDifferencesChecked,
    bool SwapShown,
    bool CloseShown,
    bool IsLoading,
    string? Notice,
    string? FilesTitle,
    IReadOnlyList<ChangedFileRow> Files);
