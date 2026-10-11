namespace VisualCommit.Testing;

/// <summary>
/// The main window's fixed layout in logical pixels, as the visual test reports give it (phase 0's
/// "What the shell must show", phase 1's graph columns, phase 3's menu, banner and "The pointer
/// before a capture"). Both harnesses of the visual test gate use these numbers: the scripted
/// walk-through (<c>ShellDriver</c> and the checks' expectations) and the real-window pass
/// (<c>RealApp</c>), which cannot see the graph's rows (D49) and clicks them at positions computed
/// from the same numbers. They are copied from the reports, not read from the app, so that a check
/// compares the two.
/// </summary>
public static class WindowLayout
{
    /// <summary>The repo tabs' strip at the top of the window.</summary>
    public const double TabStripHeight = 36;

    /// <summary>The toolbar, below the tabs.</summary>
    public const double ToolbarHeight = 52;

    /// <summary>The status bar at the bottom of the window.</summary>
    public const double StatusBarHeight = 26;

    /// <summary>The left panel's default width; the graph area starts here.</summary>
    public const double LeftPanelWidth = 260;

    /// <summary>The right panel's default width.</summary>
    public const double RightPanelWidth = 400;

    /// <summary>Where the graph area (and the left and right panels) start: below the tabs and the toolbar, 88.</summary>
    public const double GraphAreaTop = TabStripHeight + ToolbarHeight;

    /// <summary>The graph's column headers, above its rows (and below an in-progress banner).</summary>
    public const double ColumnHeaderHeight = 28;

    /// <summary>One row of the graph (phase 1).</summary>
    public const double RowHeight = 26;

    /// <summary>The top of the graph's rows while no banner is shown: 116.</summary>
    public const double RowsTop = GraphAreaTop + ColumnHeaderHeight;

    /// <summary>The Branch / Tag column, the graph's first.</summary>
    public const double BranchTagColumnWidth = 130;

    /// <summary>Where a row's first label starts in the Branch / Tag column (phase 1).</summary>
    public const double LabelStart = 6;

    /// <summary>
    /// How far into the Message column a check clicks or right-clicks a row ("in its Message
    /// column"), in both harnesses: <c>ShellDriver.GraphRowPoint</c> and the real-window pass.
    /// </summary>
    public const double MessageClickInset = 30;

    /// <summary>The in-progress banner's least height: exactly this while its text fits on one line (D122).</summary>
    public const double BannerMinHeight = 36;

    /// <summary>A menu's padding above and below its entries (4 + 4) together with its header row (24).</summary>
    public const double MenuChromeHeight = 8 + 24;

    /// <summary>One item of a context menu (4a).</summary>
    public const double MenuItemHeight = 26;

    /// <summary>One separator of a context menu (4a).</summary>
    public const double MenuSeparatorHeight = 9;

    /// <summary>How far a menu's height may be from <see cref="MenuHeight"/> (4a: ±2).</summary>
    public const double MenuHeightTolerance = 2;

    /// <summary>A menu opened by the pointer has its top-left corner this far right of and below the pointer (4a).</summary>
    public const double MenuPointerGap = 2;

    /// <summary>A menu that would cross the window's edge is moved to stay this far inside it (4a).</summary>
    public const double MenuEdgeInset = 4;

    /// <summary>
    /// The neutral point, x: where both harnesses rest the pointer before a capture that is not
    /// about a hover and has no menu open, so that no tooltip or hover background is in either
    /// picture. At 1100×700 it is the empty middle of the status bar (part D's "The real-window
    /// pass for these checks"): right of the operation's text, its bar, Cancel and the "waiting"
    /// text, left of the git version; at 1400×900 and 1920×1080 it lies in the graph area below
    /// the last row of every phase 3 scenario. <c>ShellDriverCheckSupportTests</c> checks that
    /// nothing under it has a tooltip, also while the status bar is at its widest.
    /// </summary>
    public const double NeutralX = 760;

    /// <summary>The neutral point, y (see <see cref="NeutralX"/>).</summary>
    public const double NeutralY = 687;

    /// <summary>The Graph column's width for <paramref name="lanes"/> lanes: 16 per lane plus 16, at least 48 and at most 240 (phase 1).</summary>
    public static double GraphColumnWidth(int lanes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lanes);
        return Math.Clamp((16 * lanes) + 16, 48, 240);
    }

    /// <summary>
    /// Where the Message column starts in the window, at the default panel widths, for a graph of
    /// <paramref name="lanes"/> lanes: 260 + 130 + the Graph column (454 with three lanes).
    /// </summary>
    public static double MessageColumnX(int lanes) => LeftPanelWidth + BranchTagColumnWidth + GraphColumnWidth(lanes);

    /// <summary>
    /// The top of the graph's rows when a banner's bottom edge is at <paramref name="bannerBottom"/>
    /// (D122: the column headers and rows move down by the banner's height, which depends on its
    /// lines), or <see cref="RowsTop"/> without a banner (null).
    /// </summary>
    public static double RowsTopBelow(double? bannerBottom) =>
        bannerBottom is { } bottom ? bottom + ColumnHeaderHeight : RowsTop;

    /// <summary>
    /// The vertical centre of display row <paramref name="displayRow"/> (0 at the top; the
    /// working-changes row, when it shows, is display row 0) with the rows starting at
    /// <paramref name="rowsTop"/>.
    /// </summary>
    public static double RowCentreY(int displayRow, double rowsTop = RowsTop)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayRow);
        return rowsTop + (RowHeight * displayRow) + (RowHeight / 2);
    }

    /// <summary>
    /// The height of a menu that fits the window (4a): 8 + 24 + 26 × items + 9 × separators,
    /// give or take <see cref="MenuHeightTolerance"/>.
    /// </summary>
    public static double MenuHeight(int items, int separators)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(items);
        ArgumentOutOfRangeException.ThrowIfNegative(separators);
        return MenuChromeHeight + (MenuItemHeight * items) + (MenuSeparatorHeight * separators);
    }
}
