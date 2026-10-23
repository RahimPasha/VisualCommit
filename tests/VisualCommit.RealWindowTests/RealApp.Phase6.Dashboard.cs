using System.Drawing;
using FlaUI.Core.AutomationElements;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The dashboard in the real window (docs/phase-6-design.md 2.5 to 2.7, 15.5, 17.2): its rows by
/// automation id and name (<c>DashboardRow</c>, a list item named by its repository) and their
/// cells by id (each a text element named by what it says, the Ahead / behind cell's arrows as
/// <c>{a} ahead, {n} behind</c>), the header's texts and buttons, and a row clicked, right-clicked
/// or double-clicked with the real mouse "in its Repository cell" (part W's WD22). Every wait is on
/// a condition with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The dashboard's root (a pane named by its workspace).</summary>
    public const string DashboardId = "Dashboard";

    /// <summary>A row's State cell before its first read (2.6).</summary>
    public const string ReadingText = "Reading…";

    /// <summary>Where a check clicks a row (part W's WD22): in its Repository cell, x 66 at 1100×700.</summary>
    public const float DashboardRowClickX = 66;

    /// <summary>
    /// One row of the dashboard as UI Automation shows it: its name, its cells' texts (empty for an
    /// empty cell), whether its <c>Cancel</c> and <c>Details…</c> show, whether it is selected, and
    /// where it is.
    /// </summary>
    public sealed record DashboardRowView(
        string Name,
        string Branch,
        string AheadBehind,
        string Changes,
        string State,
        string Operation,
        bool ShowsCancel,
        bool ShowsDetails,
        bool IsSelected,
        RectangleF Bounds,
        AutomationElement Element)
    {
        /// <summary>The row as one line, for a failure's message.</summary>
        public override string ToString() =>
            $"{Name} · {Branch} · {AheadBehind} · {Changes} · {State} · {Operation}{(ShowsCancel ? " [Cancel]" : string.Empty)}{(ShowsDetails ? " [Details…]" : string.Empty)}{(IsSelected ? " (selected)" : string.Empty)}";
    }

    /// <summary>Whether a dashboard shows (the active tab is a dashboard's).</summary>
    public bool IsDashboardShown => ShownElements(DashboardId).Count == 1;

    /// <summary>The dashboard's rows on screen, top to bottom; empty when no dashboard or no list shows.</summary>
    public IReadOnlyList<DashboardRowView> DashboardRows()
    {
        if (ShownElements("DashboardList") is not [var list])
        {
            return [];
        }

        return [.. list.FindAllDescendants(condition => condition.ByAutomationId("DashboardRow"))
            .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 })
            .Select(row => (Row: row, Bounds: BoundsOf(row)))
            .OrderBy(entry => entry.Bounds.Top)
            .Select(entry => RowView(entry.Row, entry.Bounds))];
    }

    /// <summary>The dashboard's rows as one line each, for a failure's message.</summary>
    public string DescribeDashboardRows() => string.Join(" | ", DashboardRows());

    /// <summary>The row named <paramref name="name"/>; throws when there is not exactly one on screen.</summary>
    public DashboardRowView DashboardRow(string name) =>
        DashboardRows().Where(row => row.Name == name).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"The dashboard shows {found.Count} rows named \"{name}\"; its rows: {DescribeDashboardRows()}."),
        };

    /// <summary>
    /// Waits until the row <paramref name="name"/> shows and <paramref name="condition"/> holds for it
    /// (its Operation cell reads a percentage, its branch shows): the real window's
    /// <c>WaitForRowAsync</c>.
    /// </summary>
    public DashboardRowView WaitForDashboardRow(string name, Func<DashboardRowView, bool> condition, string what, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(condition);
        DashboardRowView? found = null;
        WaitForTelling(
            () => DashboardRows().Where(row => row.Name == name).ToList() is [var only] && condition(found = only),
            $"the row {name}: {what}",
            timeout,
            () => $"The rows: {DescribeDashboardRows()}.");
        return found!;
    }

    /// <summary>
    /// Waits until no row of the dashboard reads (its State cell is no longer <c>Reading…</c>, its
    /// Operation cell none): with a read pass's line, the real window's
    /// <c>WaitForDashboardReadAsync</c> (part W's WD37).
    /// </summary>
    public void WaitForNoRowReading(TimeSpan timeout) =>
        WaitForTelling(
            () => DashboardRows() is { Count: > 0 } rows && rows.All(row => row.State != ReadingText && row.Operation != ReadingText),
            "every row of the dashboard to be read",
            timeout,
            () => $"The rows: {DescribeDashboardRows()}.");

    /// <summary>The point a check clicks on the row <paramref name="name"/>: in its Repository cell, at x 66, its vertical centre (WD22).</summary>
    public PointF DashboardRowPoint(string name)
    {
        var bounds = DashboardRow(name).Bounds;
        return new PointF(DashboardRowClickX, bounds.Top + (bounds.Height / 2));
    }

    /// <summary>Clicks the row <paramref name="name"/> at its point (<see cref="DashboardRowPoint"/>) with the real mouse.</summary>
    public void ClickDashboardRow(string name)
    {
        var point = DashboardRowPoint(name);
        ClickAt(point.X, point.Y);
    }

    /// <summary>Right-clicks the row <paramref name="name"/> at its point with the real mouse: its menu opens (2.7).</summary>
    public void RightClickDashboardRow(string name)
    {
        var point = DashboardRowPoint(name);
        RightClickAt(point.X, point.Y);
    }

    /// <summary>Double-clicks the row <paramref name="name"/> at its point with the real mouse: its repository opens in a tab (2.3).</summary>
    public void DoubleClickDashboardRow(string name)
    {
        var point = DashboardRowPoint(name);
        DoubleClickAt(point.X, point.Y);
    }

    /// <summary>The text of a header part on screen (<c>DashboardTitle</c>, <c>DashboardCount</c>, <c>DashboardBulkText</c>), or null when it is not on screen.</summary>
    public string? DashboardHeaderText(string automationId) => NamesOnScreen(automationId) is [var text] ? text : null;

    /// <summary>The run's bar in the header (<c>DashboardBulkProgress</c>): its value while it shows, else null.</summary>
    public double? DashboardBulkPercent() => ShownElements("DashboardBulkProgress") is [var bar] ? RangeValueOf(bar) : null;

    /// <summary>The status bar's progress bar (<c>OperationProgress</c>): its value while it shows, else null.</summary>
    public double? OperationProgressValue() => ShownElements("OperationProgress") is [var bar] ? RangeValueOf(bar) : null;

    /// <summary>
    /// The dashboard's texts on screen whose vertical centre lies between <paramref name="top"/> and
    /// <paramref name="bottom"/>, left to right: the column headings (y 124 to 152 at 1100×700, part
    /// W's geometry), which lie in a border UI Automation does not show as an element of its own.
    /// Empty while no dashboard shows.
    /// </summary>
    public IReadOnlyList<string> DashboardTextsBetween(float top, float bottom) =>
        ShownElements(DashboardId) is [var dashboard]
            ? [.. dashboard.FindAllDescendants(condition => condition.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
                .Where(text => !text.IsOffscreen && text.BoundingRectangle is { Width: > 0, Height: > 0 } && !string.IsNullOrEmpty(text.Name))
                .Select(text => (text.Name, Bounds: BoundsOf(text)))
                .Where(text => text.Bounds.Top + (text.Bounds.Height / 2) > top && text.Bounds.Top + (text.Bounds.Height / 2) < bottom)
                .OrderBy(text => text.Bounds.Left)
                .Select(text => text.Name)]
            : [];

    private DashboardRowView RowView(AutomationElement row, RectangleF bounds) => new(
        row.Name,
        CellText(row, "DashboardRowBranch"),
        CellText(row, "DashboardRowAheadBehind"),
        CellText(row, "DashboardRowChanges"),
        CellText(row, "DashboardRowState"),
        CellText(row, "DashboardRowOperation"),
        RowShows(row, "DashboardRowCancel"),
        RowShows(row, "DashboardRowDetails"),
        IsSelected(row),
        bounds,
        row);

    /// <summary>A cell's text: the name of the row's element with <paramref name="automationId"/>, empty when the cell shows nothing.</summary>
    private static string CellText(AutomationElement row, string automationId) =>
        row.FindFirstDescendant(condition => condition.ByAutomationId(automationId))?.Name ?? string.Empty;

    /// <summary>Whether the row's button <paramref name="automationId"/> (its Cancel, its Details…) shows.</summary>
    private static bool RowShows(AutomationElement row, string automationId) =>
        row.FindAllDescendants(condition => condition.ByAutomationId(automationId))
            .Any(element => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 });
}
