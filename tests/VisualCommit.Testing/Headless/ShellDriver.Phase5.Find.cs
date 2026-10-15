using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
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
