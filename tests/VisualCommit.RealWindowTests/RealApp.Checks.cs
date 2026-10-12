using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Testing;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// What phase 3's real-window checks share beyond design 7.4's list (docs/test-reports/phase-3.md,
/// each part's conventions and "The real-window pass for these checks"): the neutral point and
/// resting the pointer until a tooltip shows, replacing a box's text, the graph's rows below an
/// in-progress banner (D122), left-panel rows by their path, the status bar's progress, the log's
/// lines since a mark, and the window's own close button (check 31).
/// </summary>
public sealed partial class RealApp
{
    /// <summary>How long the waits of this file take at most when the check names no time: the scripted run's default (10 s).</summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The neutral point (<see cref="WindowLayout.NeutralX"/>, <see cref="WindowLayout.NeutralY"/>):
    /// where the pointer rests before a capture that is not about a hover, the same point as the
    /// scripted walk-through's (<c>ShellDriver.NeutralPoint</c>), so that no tooltip opens under
    /// the pointer in either picture.
    /// </summary>
    public static PointF NeutralPoint { get; } = new((float)WindowLayout.NeutralX, (float)WindowLayout.NeutralY);

    /// <summary>Moves the real pointer to <see cref="NeutralPoint"/> by setting the cursor (one jump, crossing nothing).</summary>
    public void MoveToNeutral() => SetPointer(NeutralPoint.X, NeutralPoint.Y);

    /// <summary>
    /// Puts the real pointer at a point of the client area by setting the cursor's position
    /// (<c>SetCursorPos</c>), not by FlaUI's animated move: the pointer jumps there and crosses
    /// nothing on its way, so that no menu item it would have crossed takes the highlight (D105).
    /// Windows sends the window one mouse move for the jump.
    /// </summary>
    public void SetPointer(float x, float y)
    {
        Mouse.Position = OnScreen(x, y);
        WaitUntilIdle();
    }

    /// <summary>
    /// Rests the real pointer on the centre of <paramref name="element"/> (one jump, see
    /// <see cref="SetPointer"/>) and waits until a tooltip with <paramref name="toolTip"/> is in the
    /// window's UI Automation tree (<see cref="WaitForToolTip"/>). Returns the tooltip's element.
    /// </summary>
    public AutomationElement RestPointerOn(AutomationElement element, string toolTip, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        var bounds = BoundsOf(element);
        SetPointer(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        return WaitForToolTip(toolTip, timeout);
    }

    /// <summary>
    /// Rests the real pointer on the open menu's item named <paramref name="name"/> by the path
    /// part A's conventions give ("Resting the pointer"): first to 2 px beyond the menu's left or
    /// right edge, whichever is nearer the pointer, at the item's vertical centre, then sideways
    /// onto the item's centre. Both moves are jumps (<see cref="SetPointer"/>), so the pointer
    /// crosses no other item and the highlight stays where it was. Then waits until the item's
    /// tooltip, <paramref name="toolTip"/>, is in the window (<see cref="WaitForToolTip"/>).
    /// </summary>
    public AutomationElement RestPointerOnMenuItem(string name, string toolTip, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var menu = BoundsOf(WaitForMenu());
        var item = FindAll("ContextMenuItem").Where(candidate => candidate.Name == name).ToList() switch
        {
            [var only] => only,
            [] => throw new InvalidOperationException($"The open menu has no item \"{name}\"; it has: {string.Join(" | ", MenuItemNames())}."),
            _ => throw new InvalidOperationException($"The open menu has more than one item \"{name}\"."),
        };
        var bounds = BoundsOf(item);
        var centreY = bounds.Y + (bounds.Height / 2);
        var pointer = PointerInClient();
        var outsideX = Math.Abs(pointer.X - menu.Left) <= Math.Abs(pointer.X - menu.Right) ? menu.Left - 2 : menu.Right + 2;
        SetPointer(outsideX, centreY);
        SetPointer(bounds.X + (bounds.Width / 2), centreY);
        return WaitForToolTip(toolTip, timeout);
    }

    /// <summary>
    /// Waits until a tooltip whose text is <paramref name="text"/> is in the window's UI Automation
    /// tree, and returns its element. Tooltips are drawn on the window's overlay layer (D105), so
    /// an open one is part of the window, and its capture shows it. Polls, never waits a fixed time.
    /// </summary>
    /// <exception cref="TimeoutException">No such tooltip came within <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null).</exception>
    public AutomationElement WaitForToolTip(string text, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        AutomationElement? found = null;
        IReadOnlyList<string> seen = [];
        try
        {
            WaitFor(
                () =>
                {
                    var tips = Window.FindAllDescendants(condition => condition.ByControlType(ControlType.ToolTip));
                    seen = [.. tips.Select(tip => tip.Name)];
                    found = tips.FirstOrDefault(tip => tip.Name == text || tip.FindAllDescendants().Any(part => part.Name == text));
                    return found is not null;
                },
                $"the tooltip \"{text}\"",
                timeout ?? DefaultWaitTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} Tooltips in the window: {(seen.Count == 0 ? "none" : string.Join(" | ", seen))}.", ex);
        }

        return found!;
    }

    /// <summary>
    /// Replaces what the text box with the automation id <paramref name="automationId"/> holds,
    /// as the report's "select all and type X" says for the real window: a real click into the
    /// box, Ctrl+A on the real keyboard, then the text typed (Backspace for an empty one).
    /// </summary>
    public void ReplaceText(string automationId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ClickWithMouse(Find(automationId));
        PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        if (text.Length == 0)
        {
            PressKey(VirtualKeyShort.BACK);
        }
        else
        {
            TypeText(text);
        }
    }

    /// <summary>
    /// Where the in-progress banner (<c>ConflictBanner</c>) is in the client area, or null while
    /// none is shown. It grows with its lines (D122), so rows below it are found from its bottom.
    /// </summary>
    public RectangleF? BannerBounds() =>
        FindAll("ConflictBanner").Select(BoundsOf).Where(bounds => bounds is { Width: > 0, Height: > 0 }).Cast<RectangleF?>().FirstOrDefault();

    /// <summary>
    /// The top of the graph's rows in the client area: the banner's bottom plus the column headers
    /// (28) while a banner is shown, otherwise 116 (<see cref="WindowLayout.RowsTopBelow"/>, D122).
    /// </summary>
    public float GraphRowsTop() => (float)WindowLayout.RowsTopBelow(BannerBounds()?.Bottom);

    /// <summary>
    /// The vertical centre of the graph's display row <paramref name="displayRow"/> (the
    /// working-changes row, when it shows, is display row 0), below the banner when one is shown.
    /// The graph's rows are not in UI Automation (D49); this is the row constants' answer.
    /// </summary>
    public float GraphRowCentreY(int displayRow) => (float)WindowLayout.RowCentreY(displayRow, GraphRowsTop());

    /// <summary>
    /// The point a check clicks or right-clicks on display row <paramref name="displayRow"/> "in its
    /// Message column": <see cref="WindowLayout.MessageClickInset"/> into the Message column of a
    /// graph of <paramref name="lanes"/> lanes, at the default panel widths, as
    /// <c>ShellDriver.GraphRowPoint</c> clicks it.
    /// </summary>
    public PointF GraphRowPoint(int displayRow, int lanes) =>
        new((float)(WindowLayout.MessageColumnX(lanes) + WindowLayout.MessageClickInset), GraphRowCentreY(displayRow));

    /// <summary>
    /// The left panel's row at <paramref name="path"/>, as the report writes it: the section's
    /// title, then each row's text down to the one meant, such as
    /// <c>RefRow("Remotes", "origin", "feature", "alpha")</c>. UI Automation lists the rows
    /// (<c>RefItem</c>, named by their text) and section headers (<c>Section</c>) without their
    /// depth, so each name is looked for as the first row of that name after the previous one,
    /// before the next section: the order in which the panel shows them. The section's title alone
    /// is its header.
    /// </summary>
    public AutomationElement RefRow(params string[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(path.Length, 1);
        var rows = Find("RefList").FindAllDescendants(condition => condition.ByAutomationId("RefItem").Or(condition.ByAutomationId("Section")))
            .Select(element => (Element: element, Bounds: BoundsOf(element), IsSection: element.AutomationId == "Section"))
            .OrderBy(row => row.Bounds.Y)
            .ToList();
        var index = rows.FindIndex(row => row.IsSection && row.Element.Name == path[0]);
        foreach (var name in path.Skip(1))
        {
            var start = index;
            index = start < 0 ? -1 : rows.FindIndex(start + 1, row => row.IsSection || row.Element.Name == name);
            if (index >= 0 && rows[index].IsSection)
            {
                index = -1;
            }
        }

        return index >= 0
            ? rows[index].Element
            : throw new InvalidOperationException(
                $"The left panel shows no row {string.Join(" › ", path)}; it shows: {string.Join(" | ", rows.Select(row => row.Element.Name))}.");
    }

    /// <summary>The status bar's operation text (<c>OperationStatus</c>) as UI Automation names it.</summary>
    public string OperationStatusText() => Find("OperationStatus").Name;

    /// <summary>
    /// Waits until the status bar's operation text ends with a percentage, "… N%", and returns N:
    /// "wait until the status bar shows a percentage" (checks 17 and 31, with the server holding
    /// the fetch).
    /// </summary>
    public int WaitForProgressPercent(TimeSpan? timeout = null)
    {
        Match? match = null;
        WaitFor(
            () => (match = PercentAtEnd().Match(OperationStatusText())).Success,
            "the status bar to show a percentage",
            timeout ?? DefaultWaitTimeout);
        return int.Parse(match!.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>How many lines the app's log has now: a mark for <see cref="LogLinesSince"/> ("exactly one line" counts the lines added since an input).</summary>
    public int LogMark() => LogLines().Count;

    /// <summary>The lines the app's log gained since <paramref name="mark"/> (<see cref="LogMark"/>).</summary>
    public IReadOnlyList<string> LogLinesSince(int mark)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mark);
        return [.. LogLines().Skip(mark)];
    }

    /// <summary>
    /// Clicks the window's own close button in its title bar with the real mouse, as a user closes
    /// the window, and does not wait for the app to exit (check 31: the finishing dialog keeps it
    /// open; <see cref="Close"/> would wait and throw). The button is found through UI Automation
    /// (the title bar's <c>Close</c>); should Windows not list it, the point is the title bar's
    /// right end, where the button is.
    /// </summary>
    public void ClickCloseButton()
    {
        Mouse.Click(CloseButtonOnScreen());
        WaitUntilIdle();
    }

    /// <summary>
    /// Waits until the app has exited, at most <paramref name="timeout"/>, and returns its exit
    /// code: after the window closed by itself or after "Quit now" (check 31, D36's 10 seconds).
    /// </summary>
    /// <exception cref="TimeoutException">The app was still running when the time was up.</exception>
    public int WaitForExit(TimeSpan timeout)
    {
        if (!_process.WaitForExit(timeout))
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"The app did not exit within {timeout.TotalSeconds:0.###} s."));
        }

        return _process.ExitCode;
    }

    /// <summary>The screen pixel at the centre of the title bar's close button.</summary>
    private Point CloseButtonOnScreen()
    {
        try
        {
            if (Window.TitleBar?.CloseButton is { BoundingRectangle: { Width: > 0, Height: > 0 } bounds })
            {
                return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Not listed by UI Automation: fall back to where Windows draws it.
        }

        // The caption buttons sit at the frame's right end, above the client area; the close
        // button is the last, about 46 logical pixels wide.
        NativeMethods.GetWindowRect(Handle, out var window);
        var client = ClientOriginOnScreen();
        NativeMethods.GetClientRect(Handle, out var clientRect);
        var clientRight = client.X + clientRect.Width;
        return new Point(clientRight - (int)(23 * Scaling), (window.Top + client.Y) / 2);
    }

    /// <summary>
    /// The processes the app started that run now, their own children included (git,
    /// git-remote-http, the askpass helper, ssh): what check 31 step 5 reads while the held fetch
    /// runs, to assert with <see cref="StillRunning"/> after "Quit now" that "no git process
    /// started by the app is left".
    /// </summary>
    public IReadOnlyList<(int Id, string Name)> DescendantProcesses()
    {
        var all = NativeMethods.Processes();
        var parents = new HashSet<int> { _process.Id };
        var found = new List<(int Id, string Name)>();
        for (var added = true; added;)
        {
            added = false;
            foreach (var (id, parentId, name) in all)
            {
                if (parents.Contains(parentId) && parents.Add(id))
                {
                    found.Add((id, name));
                    added = true;
                }
            }
        }

        return found;
    }

    /// <summary>Which of <paramref name="processIds"/> still run (<see cref="DescendantProcesses"/>).</summary>
    public static IReadOnlyList<int> StillRunning(IEnumerable<int> processIds)
    {
        ArgumentNullException.ThrowIfNull(processIds);
        var running = NativeMethods.Processes().Select(process => process.Id).ToHashSet();
        return [.. processIds.Where(running.Contains)];
    }

    /// <summary>Where the real pointer is now, in the client area's logical pixels.</summary>
    private PointF PointerInClient()
    {
        var position = Mouse.Position;
        var origin = ClientOriginOnScreen();
        return new PointF((float)((position.X - origin.X) / Scaling), (float)((position.Y - origin.Y) / Scaling));
    }

    [GeneratedRegex(@"(\d{1,3})%$", RegexOptions.CultureInvariant)]
    private static partial Regex PercentAtEnd();
}
