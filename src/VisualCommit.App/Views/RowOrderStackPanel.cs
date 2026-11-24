using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VisualCommit.App.Views;

/// <summary>
/// The items panel of a list whose rows hold elements Tab stops at (the left panel's refs, the
/// commit details' and a comparison's changed files, the stage panel's two lists): Tab and
/// Shift+Tab go through the rows in the order of the list's items, as they would if every row's
/// control were made and in its place. The panel makes only the rows near its view, reuses the
/// controls of the others and adds the control of a row it makes last, after a row was inserted or
/// the list scrolled up; Avalonia's own Tab follows the controls' order, so it would reach an
/// inserted row after all the others, enter a scrolled list in its middle and never reach the rows
/// below the view.
/// <para>
/// Inside a row, Tab and Shift+Tab go from stop to stop in the row's own order (its button, then the
/// buttons it shows, such as a branch's eye or a file's Stage). From a row's last stop Tab goes to
/// the next row's first, bringing that row into view; from a row's first stop Shift+Tab goes to the
/// previous row's last, or with <see cref="BackwardToRowStart"/> to its first. Tab into the list from
/// a stop before it reaches its first row's first stop; Shift+Tab into it from a stop after it
/// reaches its last row's last stop (its first with <see cref="BackwardToRowStart"/>). Past the last
/// row Tab, and before the first Shift+Tab, leave the list as Avalonia leaves it, to the stop after
/// or before the list; when that stop lies in another such list of the same region, as the stage
/// panel's two lists lie when no button is shown between them, that list is entered at its end.
/// A row without a stop (its button disabled) is passed over. A list whose Tab navigation is not
/// <see cref="KeyboardNavigationMode.Continue"/> (one stop, such as <c>Once</c>) is left to Avalonia.
/// F6, the arrows and Enter or Space on a row are not touched.
/// </para>
/// </summary>
public sealed class RowOrderStackPanel : VirtualizingStackPanel, ICustomKeyboardNavigation
{
    /// <summary>
    /// Whether Shift+Tab into a row, from the row after it or from the stop after the list, reaches the
    /// row's first stop, its own button, and not its last: the stage panel's lists (D425), whose rows
    /// show their buttons while they hold the focus, are selected or under the pointer.
    /// </summary>
    public bool BackwardToRowStart { get; set; }

    /// <summary>
    /// The elements of a row that Tab stops at now, in the row's own order: the row's control or the
    /// elements in it that can take the focus by Tab and are shown and enabled.
    /// </summary>
    public static List<InputElement> StopsOf(Control row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [.. row.GetSelfAndVisualDescendants().OfType<InputElement>().Where(IsStop)];
    }

    /// <inheritdoc/>
    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        if (direction is not (NavigationDirection.Next or NavigationDirection.Previous) || !WalksRows)
        {
            return (false, null);
        }

        var backwards = direction == NavigationDirection.Previous;
        if (element is not Visual visual || !this.IsVisualAncestorOf(visual))
        {
            // Tab or Shift+Tab from outside lands in the list: at its first row, or its last.
            return EndOfList(backwards) is { } end ? (true, end) : (false, null);
        }

        if (RowOf(visual) is not { } row || IndexFromContainer(row) is not (>= 0 and var index))
        {
            return (false, null);
        }

        var stops = StopsOf(row);
        var at = stops.FindIndex(stop => ReferenceEquals(stop, element));
        if (at < 0)
        {
            return (false, null);
        }

        var step = backwards ? -1 : 1;
        if (at + step >= 0 && at + step < stops.Count)
        {
            return (true, stops[at + step]);
        }

        // The row's last stop, or its first: the next row with a stop, or the previous one.
        for (var other = index + step; other >= 0 && other < Items.Count; other += step)
        {
            if (ScrollIntoView(other) is not { } made)
            {
                return (false, null);
            }

            if (StopsOf(made) is { Count: > 0 } next)
            {
                return (true, End(next, backwards));
            }
        }

        // Past the list's end: another such list right after it, or before it, is entered at its
        // end; else Avalonia leaves the list from its last control, or its first, as it always did.
        return (true, ListBeside(backwards));
    }

    /// <summary>Whether Tab walks the rows: the list's Tab navigation is <see cref="KeyboardNavigationMode.Continue"/>.</summary>
    private bool WalksRows => ItemsControl is { } list && KeyboardNavigation.GetTabNavigation(list) == KeyboardNavigationMode.Continue;

    private static bool IsStop(Visual visual) =>
        visual is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element
        && KeyboardNavigation.GetIsTabStop(element);

    /// <summary>Where a move into a row's <paramref name="stops"/> lands: Tab at the first, Shift+Tab at the last (the first with <see cref="BackwardToRowStart"/>).</summary>
    private InputElement End(List<InputElement> stops, bool backwards) => backwards && !BackwardToRowStart ? stops[^1] : stops[0];

    /// <summary>The first row's stop, or with <paramref name="backwards"/> the last row's, passing over rows without a stop; null when there is none.</summary>
    private InputElement? EndOfList(bool backwards)
    {
        var count = Items.Count;
        for (var index = backwards ? count - 1 : 0; index >= 0 && index < count; index += backwards ? -1 : 1)
        {
            if (ScrollIntoView(index) is not { } row)
            {
                return null;
            }

            if (StopsOf(row) is { Count: > 0 } stops)
            {
                return End(stops, backwards);
            }
        }

        return null;
    }

    /// <summary>The control of this panel that holds <paramref name="visual"/>: its row.</summary>
    private Control? RowOf(Visual visual)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current.GetVisualParent(), this))
            {
                return current as Control;
            }
        }

        return null;
    }

    /// <summary>
    /// When the first stop after this list in the visual tree's order (before it, with
    /// <paramref name="backwards"/>) within the list's region lies in another such list, that list's
    /// end; null when it is any other stop or there is none, which Avalonia then finds itself.
    /// </summary>
    private InputElement? ListBeside(bool backwards)
    {
        if ((KeyRegion.RootOf(this) ?? (Visual?)TopLevel.GetTopLevel(this)) is not { } root)
        {
            return null;
        }

        var visuals = root.GetVisualDescendants().ToList();
        var step = backwards ? -1 : 1;
        for (var i = visuals.IndexOf(this) + step; i >= 0 && i < visuals.Count; i += step)
        {
            var visual = visuals[i];
            if (this.IsVisualAncestorOf(visual) || visual.IsVisualAncestorOf(this))
            {
                continue;
            }

            if (visual is RowOrderStackPanel other)
            {
                if (!other.IsEffectivelyVisible || other.Items.Count == 0)
                {
                    continue;
                }

                if (!other.WalksRows)
                {
                    return null;
                }

                if (other.EndOfList(backwards) is { } end)
                {
                    return end;
                }

                continue;
            }

            // A stop inside another such list is that list's: it is met above, before its rows
            // going forward and after them going back.
            if (visual.FindAncestorOfType<RowOrderStackPanel>() is null && IsStop(visual))
            {
                return null;
            }
        }

        return null;
    }
}
