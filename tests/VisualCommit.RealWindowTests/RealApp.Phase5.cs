using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// Phase 5's additions to the real-window driver (docs/phase-5-design.md 13.4 and 15.2, package
/// WP9; docs/test-reports/phase-5.md, "Conventions of every part", "The real window"): the help
/// texts of the toolbar's Undo, Redo and Search and of the status bar's Activity log toggle; the
/// window's keys of design 1.3; the graph's rows as UI Automation places them (<c>GraphRows</c>,
/// whose top follows the banner and the search bar); the activity log's rows by automation id
/// and name and its output boxes through their Value pattern; the search bar; the graph area's
/// pages (file history, blame, the reflog and the backups list), their rows and the points the
/// report clicks; the comparison in the right panel; and the left panel's eye and solo buttons.
/// Every wait is on a condition with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>How far right of a page's left edge its rows are clicked: window x 420 at 1100×700 (the report's BD1, <c>ShellDriver.PageRowPointX</c>).</summary>
    public const float PageRowPointX = 160;

    /// <summary>How far right of the blame gutter's left edge a line is clicked in it: window x 300 at 1100×700 (part B's conventions).</summary>
    public const float BlameGutterPointX = 40;

    /// <summary>The blame's line height: the code font's at 12, which the text and the gutter share (design 13.4).</summary>
    public const float BlameLineHeight = 15.84f;

    /// <summary>Where the blame's text starts after its number column (up to 99 lines, 30.4) and the 2 px the report adds (design 13.4).</summary>
    public const float BlameTextLeft = 30.4f + 2;

    // ---- Help texts and the toolbar's states (design 1.1, 1.2, 7.4) ----

    /// <summary>An element's help text (<c>AutomationProperties.HelpText</c>), empty when it has none.</summary>
    public static string HelpTextOf(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Properties.HelpText.ValueOrDefault ?? string.Empty;
    }

    /// <summary>
    /// Waits until the button <paramref name="automationId"/> (a toolbar button, the Activity log
    /// toggle) is enabled or not as <paramref name="enabled"/> says and, when
    /// <paramref name="helpText"/> is given, its help text reads it (empty for none). The message of
    /// a timeout says what it was.
    /// </summary>
    public void WaitForButtonState(string automationId, bool enabled, string? helpText, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        WaitForTelling(
            () => ShownElements(automationId) is [var button] && button.IsEnabled == enabled && (helpText is null || HelpTextOf(button) == helpText),
            $"{automationId} to be {(enabled ? "enabled" : "disabled")}{(helpText is null ? string.Empty : $" with the help text \"{helpText}\"")}",
            timeout ?? DefaultWaitTimeout,
            () => ShownElements(automationId) is [var button]
                ? $"It is {(button.IsEnabled ? "enabled" : "disabled")} with the help text \"{HelpTextOf(button)}\"."
                : $"{ShownElements(automationId).Count} such elements are on screen.");
    }

    // ---- The window's keys (design 1.3) ----

    /// <summary>Ctrl+F on the real keyboard: the toolbar's Search (<c>ShellDriver.PressSearch</c>).</summary>
    public void PressSearch() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);

    /// <summary>
    /// Ctrl+Z on the real keyboard meant for the window (<c>ShellDriver.PressGitUndo</c>): the
    /// toolbar's Undo when no text box and no resolver takes it. The same keys as
    /// <see cref="PressUndo"/>, the text undo; a check uses this one where the key is the window's.
    /// </summary>
    public void PressGitUndo() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);

    /// <summary>Ctrl+Y on the real keyboard, the first of the platform's redo gestures (<c>ShellDriver.PressGitRedo(0)</c>).</summary>
    public void PressGitRedo() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);

    // ---- The graph's rows ----

    /// <summary>
    /// Where the graph's rows are (<c>GraphRows</c>, a control element of its own): below the
    /// column headers, which a banner and the search bar move down, and above the activity log's
    /// splitter while the log is open.
    /// </summary>
    public RectangleF GraphRowsBounds() =>
        ShownElements("GraphRows") is [var rows]
            ? BoundsOf(rows)
            : throw new InvalidOperationException($"{ShownElements("GraphRows").Count} graphs are on screen, not one.");

    /// <summary>Whether the graph's rows (<c>GraphRows</c>) have the keyboard focus.</summary>
    public bool GraphHasKeyboardFocus() => ShownElements("GraphRows") is [var rows] && HasKeyboardFocus(rows);

    // ---- The activity log (design 2.1, 2.2) ----

    /// <summary>The activity log's panel's automation id.</summary>
    public const string ActivityLogId = "ActivityLog";

    /// <summary>Where the activity log's panel is, or null while it is closed.</summary>
    public RectangleF? ActivityLogBounds() =>
        ShownElements(ActivityLogId) is [var panel] ? BoundsOf(panel) : null;

    /// <summary>Waits until the activity log's panel is open (<paramref name="shown"/>) or closed.</summary>
    public void WaitForActivityLog(bool shown, TimeSpan? timeout = null) =>
        WaitFor(() => ActivityLogBounds() is not null == shown, shown ? "the activity log to open" : "the activity log to close", timeout ?? DefaultWaitTimeout);

    /// <summary>
    /// Waits until the activity log's panel is open with its top at <paramref name="top"/> and its
    /// bottom at <paramref name="bottom"/>, ±<paramref name="tolerance"/> (part A's AD9), and returns
    /// where it is: a height set by a drag or read from the session is applied after the layout.
    /// </summary>
    public RectangleF WaitForActivityLogAt(float top, float bottom, float tolerance, TimeSpan? timeout = null)
    {
        RectangleF? bounds = null;
        WaitForTelling(
            () => (bounds = ActivityLogBounds()) is { } panel && Math.Abs(panel.Top - top) <= tolerance && Math.Abs(panel.Bottom - bottom) <= tolerance,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the activity log at y {top} to {bottom} (±{tolerance})"),
            timeout ?? DefaultWaitTimeout,
            () => bounds is { } panel ? $"It is at {panel}." : "It is closed.");
        return bounds!.Value;
    }

    /// <summary>One row of the activity log's list as UI Automation shows it: its kind (its automation id), its name, where it is, whether it lies whole in the list's view.</summary>
    public sealed record LogRow(string Kind, string Name, RectangleF Bounds, bool InView, AutomationElement Element);

    /// <summary>
    /// The activity log's rows the list has made, top to bottom (design 13.4):
    /// <c>ActivityLogOperation</c> named by its title, <c>ActivityLogLine</c> by its text,
    /// <c>ActivityLogCall</c> by its command, each with whether it lies whole in the list's view
    /// (the list is virtualised: rows far out of view are not in the tree at all).
    /// </summary>
    public IReadOnlyList<LogRow> ActivityLogRows()
    {
        if (ShownElements(ActivityLogId) is not [var panel])
        {
            return [];
        }

        var list = panel.FindFirstDescendant(condition => condition.ByAutomationId("ActivityLogList"));
        var view = list is null ? BoundsOf(panel) : BoundsOf(list);
        return [.. panel.FindAllDescendants(condition => condition.ByAutomationId("ActivityLogOperation")
                .Or(condition.ByAutomationId("ActivityLogLine"))
                .Or(condition.ByAutomationId("ActivityLogCall")))
            .Where(element => element.BoundingRectangle is { Width: > 0, Height: > 0 })
            .Select(element => (Element: element, Bounds: BoundsOf(element)))
            .OrderBy(row => row.Bounds.Top)
            .Select(row => new LogRow(
                row.Element.AutomationId,
                row.Element.Name,
                row.Bounds,
                row.Bounds.Top >= view.Top - 0.5f && row.Bounds.Bottom <= view.Bottom + 0.5f,
                row.Element))];
    }

    /// <summary>The names of the activity log's rows in the list's view, top to bottom: what run.txt records of the panel as UI Automation exposes it.</summary>
    public IReadOnlyList<string> ActivityLogNamesInView() => [.. ActivityLogRows().Where(row => row.InView).Select(row => row.Name)];

    /// <summary>
    /// Waits until the <paramref name="occurrence"/>-th (from 0, top to bottom) of the operation and
    /// call rows the list has made named <paramref name="name"/> lies whole in the list's view, and
    /// returns it: the row a check clicks, as <c>ShellDriver.ActivityLogRow</c> finds it.
    /// </summary>
    public LogRow WaitForActivityLogRow(string name, int occurrence = 0, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegative(occurrence);
        LogRow? found = null;
        WaitForTelling(
            () =>
            {
                var rows = ActivityLogRows().Where(row => row.Kind is "ActivityLogOperation" or "ActivityLogCall" && row.Name == name).ToList();
                found = rows.Count > occurrence ? rows[occurrence] : null;
                return found is { InView: true };
            },
            $"the activity log's row \"{name}\" ({occurrence}) whole in the list's view",
            timeout ?? DefaultWaitTimeout,
            () => $"The rows in view: {string.Join(" | ", ActivityLogNamesInView())}.");
        return found!;
    }

    /// <summary>Clicks the activity log's row (<see cref="WaitForActivityLogRow"/>) at its centre with the real mouse: an operation or a call opens or closes.</summary>
    public void ClickActivityLogRow(string name, int occurrence = 0) => ClickWithMouse(WaitForActivityLogRow(name, occurrence).Element);

    /// <summary>
    /// The texts of the output boxes under opened calls (<c>ActivityLogOutput</c>) that lie whole in
    /// the list's view, top to bottom, read through their Value pattern: each part's title on its
    /// own line over its text, parts a blank line apart, or <c>No output</c>.
    /// </summary>
    public IReadOnlyList<string> ActivityLogOutputs()
    {
        if (ShownElements(ActivityLogId) is not [var panel])
        {
            return [];
        }

        var list = panel.FindFirstDescendant(condition => condition.ByAutomationId("ActivityLogList"));
        var view = list is null ? BoundsOf(panel) : BoundsOf(list);
        return [.. panel.FindAllDescendants(condition => condition.ByAutomationId("ActivityLogOutput"))
            .Where(element => element.BoundingRectangle is { Width: > 0, Height: > 0 })
            .Select(element => (Element: element, Bounds: BoundsOf(element)))
            .Where(box => box.Bounds.Top >= view.Top - 0.5f && box.Bounds.Bottom <= view.Bottom + 0.5f)
            .OrderBy(box => box.Bounds.Top)
            .Select(box => box.Element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? string.Empty)];
    }

    /// <summary>Waits until an output box in the list's view reads <paramref name="text"/> (<see cref="ActivityLogOutputs"/>).</summary>
    public void WaitForActivityLogOutput(string text, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        WaitForTelling(
            () => ActivityLogOutputs().Contains(text),
            $"an output box reading \"{text.Replace("\n", "⏎", StringComparison.Ordinal)}\" in the activity log's view",
            timeout ?? DefaultWaitTimeout,
            () => $"The boxes in view read: {string.Join(" | ", ActivityLogOutputs().Select(box => "\"" + box.Replace("\n", "⏎", StringComparison.Ordinal) + "\""))}.");
    }

    // ---- The search bar (design 3.1) ----

    /// <summary>Whether the search bar (<c>SearchBar</c>) shows.</summary>
    public bool IsSearchBarShown => ShownElements("SearchBar").Count > 0;

    /// <summary>The search's count as UI Automation names it (<c>SearchCount</c>): "2 matches", "1 of 2", or empty.</summary>
    public string SearchCountText() => FindAll("SearchCount").FirstOrDefault(element => !element.IsOffscreen)?.Name ?? string.Empty;

    /// <summary>Waits until the search's count reads <paramref name="count"/>: the search has ended and its matches are applied.</summary>
    public void WaitForSearchCount(string count, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(count);
        WaitForTelling(
            () => SearchCountText() == count,
            $"the search's count to read \"{count}\"",
            timeout ?? DefaultWaitTimeout,
            () => $"It reads \"{SearchCountText()}\".");
    }

    // ---- The graph area's pages (design 4, 5, 11.1) ----

    /// <summary>
    /// Waits until the page <paramref name="automationId"/> (<c>FileHistory</c>, <c>Blame</c>,
    /// <c>Reflog</c>: each a pane named by its title) shows named <paramref name="title"/>, and returns it.
    /// </summary>
    public AutomationElement WaitForPage(string automationId, string title, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        ArgumentException.ThrowIfNullOrEmpty(title);
        AutomationElement? page = null;
        WaitForTelling(
            () => (page = ShownElements(automationId).FirstOrDefault(element => element.Name == title)) is not null,
            $"the page {automationId} \"{title}\"",
            timeout ?? DefaultWaitTimeout,
            () => $"The pages on screen: {string.Join(" | ", new[] { "FileHistory", "Blame", "Reflog" }.SelectMany(id => ShownElements(id).Select(element => id + " \"" + element.Name + "\"")))}.");
        return page!;
    }

    /// <summary>Whether any of the graph area's pages shows.</summary>
    public bool IsAnyPageShown => new[] { "FileHistory", "Blame", "Reflog" }.Any(id => ShownElements(id).Count > 0);

    /// <summary>
    /// Waits until the list of the page <paramref name="pageId"/> shows exactly the rows
    /// <paramref name="names"/> (their automation names, top to bottom) with the automation id
    /// <paramref name="rowId"/> (<c>FileHistoryRow</c> named <c>{short} {subject}</c>,
    /// <c>ReflogRow</c> by its selector, <c>BackupRow</c> by the backup's name), counting only the
    /// rows on screen.
    /// </summary>
    public void WaitForPageRows(string pageId, string rowId, IReadOnlyList<string> names, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        WaitForTelling(
            () => PageRowNames(pageId, rowId).SequenceEqual(names),
            $"the rows of {pageId} to be {string.Join(", ", names)}",
            timeout ?? DefaultWaitTimeout,
            () => $"They are {string.Join(", ", PageRowNames(pageId, rowId))}.");
    }

    /// <summary>The rows with the automation id <paramref name="rowId"/> inside the page <paramref name="pageId"/> that are on screen, top to bottom.</summary>
    public IReadOnlyList<AutomationElement> PageRows(string pageId, string rowId)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageId);
        ArgumentException.ThrowIfNullOrEmpty(rowId);
        return ShownElements(pageId) is [var page]
            ? [.. page.FindAllDescendants(condition => condition.ByAutomationId(rowId))
                .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 })
                .OrderBy(row => row.BoundingRectangle.Top)]
            : [];
    }

    /// <summary>The names of <see cref="PageRows"/>.</summary>
    public IReadOnlyList<string> PageRowNames(string pageId, string rowId) => [.. PageRows(pageId, rowId).Select(row => row.Name)];

    /// <summary>The row named <paramref name="name"/> among <see cref="PageRows"/>; throws when there is not exactly one.</summary>
    public AutomationElement PageRow(string pageId, string rowId, string name) =>
        PageRows(pageId, rowId).Where(row => row.Name == name).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"{pageId} shows {found.Count} rows named \"{name}\"; its rows: {string.Join(" | ", PageRowNames(pageId, rowId))}."),
        };

    /// <summary>
    /// The point the report clicks on a page's row (BD1): <see cref="PageRowPointX"/> right of the
    /// page's left edge (window x 420), at the row's vertical centre.
    /// </summary>
    public PointF PageRowPoint(string pageId, AutomationElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var page = ShownElements(pageId) is [var shown] ? BoundsOf(shown) : throw new InvalidOperationException($"The page {pageId} is not shown.");
        var bounds = BoundsOf(row);
        return new PointF(page.Left + PageRowPointX, bounds.Top + (bounds.Height / 2));
    }

    /// <summary>Clicks a page's row at its point (<see cref="PageRowPoint"/>) with the real mouse.</summary>
    public void ClickPageRow(string pageId, string rowId, string name)
    {
        var point = PageRowPoint(pageId, PageRow(pageId, rowId, name));
        ClickAt(point.X, point.Y);
    }

    /// <summary>Right-clicks a page's row at its point (<see cref="PageRowPoint"/>) with the real mouse.</summary>
    public void RightClickPageRow(string pageId, string rowId, string name)
    {
        var point = PageRowPoint(pageId, PageRow(pageId, rowId, name));
        RightClickAt(point.X, point.Y);
    }

    /// <summary>Waits until the page row named <paramref name="name"/> has the keyboard focus: the selected row has it (design 4.1).</summary>
    public void WaitForPageRowFocus(string pageId, string rowId, string name, TimeSpan? timeout = null) =>
        WaitForTelling(
            () => PageRows(pageId, rowId).Where(HasKeyboardFocus).Select(row => row.Name).SequenceEqual([name]),
            $"the row \"{name}\" of {pageId} alone to have the keyboard focus",
            timeout ?? DefaultWaitTimeout,
            () => $"Rows with it: {string.Join(" | ", PageRows(pageId, rowId).Where(HasKeyboardFocus).Select(row => row.Name))}.");

    /// <summary>The text of the element <paramref name="automationId"/> inside the page <paramref name="pageId"/> (a title, the hosted diff's side label), or null when none is on screen.</summary>
    public string? PageText(string pageId, string automationId) =>
        ShownElements(pageId) is [var page]
            ? page.FindAllDescendants(condition => condition.ByAutomationId(automationId)).FirstOrDefault(element => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 })?.Name
            : null;

    /// <summary>Waits until the element <paramref name="automationId"/> inside the page <paramref name="pageId"/> reads <paramref name="text"/> (<see cref="PageText"/>).</summary>
    public void WaitForPageText(string pageId, string automationId, string text, TimeSpan? timeout = null) =>
        WaitForTelling(
            () => PageText(pageId, automationId) == text,
            $"{automationId} in {pageId} to read \"{text}\"",
            timeout ?? DefaultWaitTimeout,
            () => $"It reads \"{PageText(pageId, automationId) ?? "(nothing on screen)"}\".");

    /// <summary>The blame's text (<c>BlameText</c>, a Document) read through its Value pattern.</summary>
    public string BlameTextValue() =>
        Find("BlameText").Patterns.Value.PatternOrDefault?.Value.ValueOrDefault
        ?? throw new InvalidOperationException("The blame's text has no Value pattern.");

    /// <summary>
    /// The point of blame line <paramref name="line"/> (from 1) in the gutter: <see cref="BlameGutterPointX"/>
    /// right of the gutter's left edge (<c>BlameGutter</c>'s bounding rectangle), halfway down the
    /// line from the text's top (<c>BlameText</c>, at its top: the checks scroll nothing).
    /// </summary>
    public PointF BlameGutterPoint(int line)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        var gutter = BoundsOf(Find("BlameGutter"));
        var text = BoundsOf(Find("BlameText"));
        return new PointF(gutter.Left + BlameGutterPointX, text.Top + ((line - 0.5f) * BlameLineHeight));
    }

    /// <summary>
    /// Where column <paramref name="column"/> of blame line <paramref name="line"/> (both from 1)
    /// starts, halfway down the line, from <c>BlameText</c>'s bounding rectangle (design 13.4, as
    /// phase 4's <see cref="ResultPoint"/>): x = its left + 30.4 + 2 + 7.2 × (column − 1).
    /// </summary>
    public PointF BlameTextPoint(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        var text = BoundsOf(Find("BlameText"));
        return new PointF(text.Left + BlameTextLeft + (CodeCharacterWidth * (column - 1)), text.Top + ((line - 0.5f) * BlameLineHeight));
    }

    // ---- The comparison (design 6.4) ----

    /// <summary>
    /// Waits until the right panel shows the comparison (<c>Compare</c>) with
    /// <paramref name="from"/> and <paramref name="to"/> (<c>CompareFrom</c>, <c>CompareTo</c>, named
    /// by their texts) and has read it (<c>Loading…</c> gone, its files' title shown).
    /// </summary>
    public void WaitForComparison(string from, string to, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(from);
        ArgumentException.ThrowIfNullOrEmpty(to);
        WaitForTelling(
            () => ShownElements("Compare").Count == 1
                && NamesOnScreen("CompareFrom").SequenceEqual([from])
                && NamesOnScreen("CompareTo").SequenceEqual([to])
                && ShownElements("CompareLoading").Count == 0
                && ShownElements("CompareFilesTitle").Count == 1,
            $"the comparison \"{from}\", \"{to}\"",
            timeout ?? DefaultWaitTimeout,
            () => $"The right panel shows {ShownElements("Compare").Count} comparisons, From \"{string.Join(" | ", NamesOnScreen("CompareFrom"))}\", To \"{string.Join(" | ", NamesOnScreen("CompareTo"))}\", loading {ShownElements("CompareLoading").Count > 0}.");
    }

    /// <summary>The comparison's file row named <paramref name="name"/> (<c>ChangedFile</c> inside <c>CompareFileList</c>); throws when there is not exactly one on screen.</summary>
    public AutomationElement CompareFileRow(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var list = ShownElements("CompareFileList") is [var shown] ? shown : throw new InvalidOperationException("The comparison's file list is not shown.");
        return list.FindAllDescendants(condition => condition.ByAutomationId("ChangedFile"))
            .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 } && row.Name == name)
            .ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"The comparison's list shows {found.Count} files named \"{name}\"."),
        };
    }

    /// <summary>The details panel's file row named <paramref name="name"/> (<c>ChangedFile</c> inside <c>FileList</c>), waited for.</summary>
    public AutomationElement DetailsFileRow(string name, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AutomationElement? row = null;
        WaitFor(
            () => (row = ShownElements("FileList").FirstOrDefault()?
                .FindAllDescendants(condition => condition.ByAutomationId("ChangedFile"))
                .FirstOrDefault(candidate => !candidate.IsOffscreen && candidate.Name == name && candidate.BoundingRectangle is { Width: > 0, Height: > 0 })) is not null,
            $"the details' file row {name}",
            timeout ?? DefaultWaitTimeout);
        return row!;
    }

    // ---- The left panel's eye and solo buttons (design 8.3) ----

    /// <summary>
    /// The eye (<c>RefHiddenToggle</c>) or solo (<c>RefSoloToggle</c>) button of the left panel's row
    /// at <paramref name="path"/> (<see cref="RefRow"/>'s path): the one on screen whose vertical
    /// centre is the row's; null when the row shows none.
    /// </summary>
    public AutomationElement? RefRowToggle(string automationId, params string[] path)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        var row = BoundsOf(RefRow(path));
        var centre = row.Top + (row.Height / 2);
        return ShownElements(automationId).FirstOrDefault(toggle =>
        {
            var bounds = BoundsOf(toggle);
            return Math.Abs(bounds.Top + (bounds.Height / 2) - centre) <= 2 && bounds.Left >= row.Left - 0.5f && bounds.Right <= row.Right + 0.5f;
        });
    }

    /// <summary>Whether the left panel shows a row at <paramref name="path"/> (<see cref="RefRow"/> without the throw).</summary>
    public bool HasRefRow(params string[] path)
    {
        try
        {
            RefRow(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
