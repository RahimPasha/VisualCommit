// Part K's real-window steps (docs/test-reports/phase-6.md, part K, the RW steps in K10's Notes;
// docs/phase-6-design.md 5.11, 6.5, 6.6, 6.8, 17.4): K10 method 1, steps 1 to 6, in one launch on a
// fresh "ops" copy at 1100×700, dark, with real keys (Ctrl+Shift+P, PageDown, Down, Up, Tab, Ctrl+A,
// Enter, Esc, Ctrl+T), the queries typed key by key, and the real mouse (the wheel over the list,
// the pointer onto and a click on a row, a press outside the card); then, in the same launch, real
// Ctrl+T, Ctrl+Tab, Ctrl+Shift+E and F6 (KD22), and the keys UI Automation gives Fetch and a menu
// item. The palette's rows are read through UI Automation (each CommandPaletteItem's name, item
// status, help text, accelerator key and selection), the log's lines from the log file. Method 2,
// K11 to K13 are scripted only (the report says why).
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase6RealWindowPass
{
    /// <summary>
    /// Check K10, method 1, steps 1 to 6, with the real mouse and keyboard on "ops": "Main work"
    /// clicked; a real Ctrl+Shift+P over the graph, the commands with their keys and the first
    /// highlighted (<c>k10a-palette-commands</c>), PageDown three times and the wrapping arrows, the
    /// wheel's notch over the list; <c>zzz</c> typed key by key matching nothing, Tab staying in the box,
    /// Ctrl+A and <c>delete main</c> (<c>k10b-palette-delete-main</c>), Enter on and a click on the disabled
    /// <c>Delete main…</c> doing nothing, Ctrl+T declining; a second Ctrl+Shift+P, <c>check out
    /// feature/alpha</c> and Enter: the checkout after the palette has closed
    /// (<c>k10c-checked-out-by-palette</c>); <c>create branch</c> and a click on <c>Create branch…</c>: its
    /// dialog (<c>k10d-create-branch-from-palette</c>), Esc; the session's run first in an empty palette,
    /// Esc, and a press outside the card. Then real Ctrl+T, Ctrl+Tab, Ctrl+Shift+E and F6, and the
    /// keys of Fetch and of the empty area's menu item <c>Fetch all remotes</c>.
    /// </summary>
    [Fact]
    public async Task Check_K10_palette_search_and_run()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        Assert.Equal(PartK.Ops, name);
        var start = app.LogMark();

        // At the start: nothing to undo or redo, one tab.
        app.WaitForStripTabs(StepTimeout, PartK.Ops);
        AssertButton(app, "UndoButton", false, PartK.NothingToUndo);
        AssertButton(app, "RedoButton", false, PartK.NothingToRedo);

        // Step 1: a real click on "Main work" (display row 3 of three lanes) in its Message column.
        var point = app.GraphRowPoint(PartK.MainWorkRow, PartK.Ops0Lanes);
        Assert.True(
            Math.Abs(point.X - PartK.MainWorkPoint.X) <= 0.5 && Math.Abs(point.Y - PartK.MainWorkPoint.Y) <= PartK.Tolerance,
            $"Main work's Message column is at {point}, not ({PartK.MainWorkPoint.X}, {PartK.MainWorkPoint.Y}).");
        app.ClickAt(point.X, point.Y);
        WaitForMainWorkDetails(app);
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph's rows to have the keyboard focus", StepTimeout);
        app.MoveToNeutral();

        // Step 2: a real Ctrl+Shift+P (k10a): the commands at once, the first highlighted, the box focused.
        var mark = app.LogMark();
        app.PressPalette();
        var palette = app.WaitForPalette(StepTimeout);
        Assert.Equal(PartK.PaletteName, palette.Name);
        app.WaitFor(() => app.HasFocusOnScreen("CommandPaletteBox"), "the palette's box to have the keyboard focus", StepTimeout);
        Assert.Equal(PartK.PaletteBoxName, app.Find("CommandPaletteBox").Name);
        Assert.Equal(PartK.PaletteListName, app.Find("CommandPaletteList").Name);
        Assert.Equal(string.Empty, app.PaletteQuery());
        Assert.False(app.PaletteShowsNothing, "The empty palette shows No matches.");
        AssertPaletteInView(app, PartK.OpsCommands[..PartK.RowsInView], highlighted: 0);
        AssertPaletteRow(app, "Fetch all remotes", detail: null, reason: null, key: "Ctrl+Shift+F", highlighted: true);
        AssertPaletteRow(app, "Stash changes…", detail: null, reason: PartK.NoChangesToStash, key: "Ctrl+Shift+S", highlighted: false);
        AssertOneLogLine(app, mark, "] " + PartK.PaletteOpened(PartK.Ops));
        Compare(app, "k10a-palette-commands");

        // PageDown three times: rows 11, 22 and 32 (clamped), each whole in view; Down wraps to 0, Up back to 32.
        foreach (var row in PartK.PageDownRows)
        {
            app.PressKeys(VirtualKeyShort.NEXT);
            AssertHighlightInView(app, PartK.OpsCommands[row].Label);
        }

        app.PressKeys(VirtualKeyShort.DOWN);
        AssertHighlightInView(app, PartK.OpsCommands[0].Label);
        app.PressKeys(VirtualKeyShort.UP);
        AssertPaletteInView(app, PartK.OpsCommands[PartK.FirstRowInViewAtEnd..], highlighted: PartK.RowsInView - 1);
        Assert.Equal(string.Empty, app.PaletteQuery());

        // The real wheel over the list: the pointer's move highlights the row under it (the sixth in
        // view), the notch scrolls up, not to the top; nothing runs, the box keeps the focus.
        app.MoveMouseTo((float)PartK.WheelPoint.X, (float)PartK.WheelPoint.Y);
        AssertHighlightInView(app, PartK.OpsCommands[PartK.WheelRow].Label);
        app.ScrollWheel((float)PartK.WheelPoint.X, (float)PartK.WheelPoint.Y, 1);
        WaitTelling(
            app,
            () => app.PaletteRowsInView() is [var first, ..] && IndexOf(first.Label) is > 0 and < PartK.FirstRowInViewAtEnd,
            "the wheel's notch to scroll the list up, not to its top",
            () => $"The rows in view: {app.DescribePaletteRows()}.");
        Assert.True(app.IsPaletteShown, "The wheel closed the palette.");
        Assert.True(app.HasFocusOnScreen("CommandPaletteBox"), "The wheel took the keyboard focus from the box.");
        Assert.Equal(string.Empty, app.PaletteQuery());
        Assert.Equal(0, RanLines(app, mark));
        app.MoveToNeutral();

        // Step 3: zzz typed key by key matches nothing; Tab stays in the box; Ctrl+A, delete main replaces it (k10b).
        app.TypeInPalette(PartK.QueryNothing, PartK.QueryNothing, StepTimeout);
        app.WaitFor(() => app.PaletteShowsNothing, "the palette to show No matches", StepTimeout);
        Assert.Equal([PartK.NoMatches], app.NamesOnScreen("CommandPaletteEmpty"));
        app.PressKey(VirtualKeyShort.TAB);
        Assert.True(app.HasFocusOnScreen("CommandPaletteBox"), "Tab took the keyboard focus out of the box (5.8).");
        Assert.Equal(PartK.QueryNothing, app.PaletteQuery());
        app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        app.TypeInPalette(PartK.QueryDeleteMain, PartK.QueryDeleteMain, StepTimeout);
        AssertPaletteInView(app, PartK.DeleteMain, highlighted: 0);
        AssertPaletteRow(app, "Delete main on origin…", detail: "origin/main", reason: null, key: null, highlighted: true);
        AssertPaletteRow(app, "Delete main…", detail: "main", reason: PartK.CannotDeleteCurrentBranch, key: null, highlighted: false);
        Compare(app, "k10b-palette-delete-main");

        // Up wraps to the disabled row 1; Enter does nothing (5.7); Down back to row 0.
        app.PressKeys(VirtualKeyShort.UP);
        AssertHighlightInView(app, PartK.DeleteMain[1].Label);
        app.PressKey(VirtualKeyShort.RETURN);
        Assert.True(app.IsPaletteShown, "Enter on a disabled entry closed the palette.");
        Assert.Equal(0, RanLines(app, mark));
        app.PressKeys(VirtualKeyShort.DOWN);
        AssertHighlightInView(app, PartK.DeleteMain[0].Label);

        // The real pointer onto row 1 highlights it; a click on it runs nothing, the box keeps the focus.
        app.MoveMouseTo((float)PartK.PaletteRow1Point.X, (float)PartK.PaletteRow1Point.Y);
        AssertHighlightInView(app, PartK.DeleteMain[1].Label);
        app.ClickAt((float)PartK.PaletteRow1Point.X, (float)PartK.PaletteRow1Point.Y);
        Assert.True(app.IsPaletteShown, "A click on a disabled entry closed the palette.");
        Assert.Equal(PartK.QueryDeleteMain, app.PaletteQuery());
        Assert.True(app.HasFocusOnScreen("CommandPaletteBox"), "The click took the keyboard focus from the box (the rows take none, 5.8).");
        Assert.Equal(0, RanLines(app, mark));
        app.MoveToNeutral();

        // Ctrl+T declines while a dialog is open (6.3).
        app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        app.WaitForStripTabs(StepTimeout, PartK.Ops);
        Assert.Equal(PartK.QueryDeleteMain, app.PaletteQuery());
        Assert.True(app.IsPaletteShown, "Ctrl+T closed the palette.");

        // Step 4: Ctrl+Shift+P selects the whole query (KD20), so the typing replaces it; Enter runs the
        // checkout after the palette has closed (5.9).
        app.PressPalette();
        Assert.True(app.IsPaletteShown, "The second Ctrl+Shift+P closed the palette.");
        AssertOneLogLine(app, mark, "] " + PartK.PaletteOpened(PartK.Ops));
        app.TypeInPalette(PartK.QueryCheckOut, PartK.QueryCheckOut, StepTimeout);
        AssertPaletteInView(app, PartK.CheckOutRows, highlighted: 0);
        var logMark = app.LogMark();
        var checkout = AppLogFile.OperationEnded(name, PartK.CheckOutFeatureAlpha, OperationOutcome.Succeeded, PartK.CheckedOutFeatureAlpha);
        var ended = app.CountLogLines(checkout);
        app.PressKey(VirtualKeyShort.RETURN);
        app.WaitForPaletteClosed(StepTimeout);
        app.WaitForLogLine(checkout, OperationTimeout, ended + 1);
        app.WaitFor(app.GraphHasKeyboardFocus, "the keyboard focus back in the graph's rows", StepTimeout);
        AssertOneLogLine(app, logMark, "] " + PartK.PaletteRan(PartK.Ops, PartK.CheckOutFeatureAlphaLabel, "feature/alpha"));
        Assert.Equal(["refs/heads/feature/alpha"], await GitLinesAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Equal([PartK.AlphaTwoSha], await GitLinesAsync(repo, "rev-parse", "HEAD"));
        Assert.Equal([PartK.AlphaBranchLine], await GitLinesAsync(repo, "status", "--porcelain", "--branch"));
        Assert.Equal([$"stash@{{0}}: {PartK.StashMessage}"], await GitLinesAsync(repo, "stash", "list"));

        // The picture (k10c): Main work still selected with its details, feature/alpha the branch.
        WaitForMainWorkDetails(app);
        WaitForOperationStatus(app, PartK.CheckedOutFeatureAlpha);
        app.WaitForText("CurrentBranch", "feature/alpha", StepTimeout);
        app.WaitFor(() => app.HasRefRow("Local branches", "feature", "alpha"), "the Local branches' feature folder open with alpha", StepTimeout);
        AssertButton(app, "UndoButton", true, PartK.UndoToolTip(PartK.CheckoutOfFeatureAlpha));
        AssertButton(app, "RedoButton", false, PartK.NothingToRedo);
        Compare(app, "k10c-checked-out-by-palette");

        // Step 5: create branch; the real pointer onto row 0 and a click on it run Create branch… as
        // Enter would; its dialog (k10d); Esc.
        mark = app.LogMark();
        app.PressPalette();
        app.WaitForPalette(StepTimeout);
        app.TypeInPalette(PartK.QueryCreateBranch, PartK.QueryCreateBranch, StepTimeout);
        AssertPaletteInView(app, PartK.CreateBranch, highlighted: 0);
        app.MoveMouseTo((float)PartK.PaletteRow0Point.X, (float)PartK.PaletteRow0Point.Y);
        AssertHighlightInView(app, PartK.CreateBranch[0].Label);
        app.ClickAt((float)PartK.PaletteRow0Point.X, (float)PartK.PaletteRow0Point.Y);
        app.WaitForText("NameDialogTitle", PartK.CreateBranchTitle, StepTimeout);
        Assert.False(app.IsPaletteShown, "The click left the palette open.");
        AssertOneLogLine(app, mark, "] " + PartK.PaletteOpened(PartK.Ops));
        AssertOneLogLine(app, mark, "] " + PartK.PaletteRan(PartK.Ops, "Create branch…"));
        AssertCreateBranchDialog(app);
        Compare(app, "k10d-create-branch-from-palette");
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone("NameDialog", StepTimeout);
        AssertNoDialog(app);
        Assert.Equal(PartK.Ops0LocalBranches, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname)", "refs/heads"));
        app.WaitFor(app.GraphHasKeyboardFocus, "the keyboard focus back in the graph's rows after Esc", StepTimeout);

        // Step 6: the empty palette lists the session's run first (5.6); Esc; then a press outside the card.
        mark = app.LogMark();
        app.PressPalette();
        app.WaitForPalette(StepTimeout);
        AssertPaletteInView(app, PartK.AfterTheRuns, highlighted: 0);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitForPaletteClosed(StepTimeout);
        app.WaitFor(app.GraphHasKeyboardFocus, "the keyboard focus back in the graph's rows after Esc", StepTimeout);
        app.PressPalette();
        app.WaitForPalette(StepTimeout);
        var menuMark = app.LogMark();
        app.ClickAt((float)PartK.OutsideCardPoint.X, (float)PartK.OutsideCardPoint.Y);
        app.WaitForPaletteClosed(StepTimeout);
        Assert.Empty(app.FindAll("ContextMenu"));
        Assert.DoesNotContain(app.LogLinesSince(menuMark), line => line.Contains("Context menu opened", StringComparison.Ordinal));
        WaitForMainWorkDetails(app);
        app.WaitFor(app.GraphHasKeyboardFocus, "the keyboard focus back in the graph's rows after the press outside", StepTimeout);
        Assert.Equal(0, RanLines(app, mark));

        // Over the method: four openings and two runs.
        Assert.Equal(4, app.CountLogLinesSince(AppLogFile.Containing("] " + PartK.PaletteOpened(PartK.Ops)), start));
        Assert.Equal(2, RanLines(app, start));

        // Then, in the same launch: real Ctrl+T, Ctrl+Tab back to ops, Ctrl+Shift+E into FilterBox, F6 to GraphRows (KD22).
        app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        app.WaitForStripTabs(StepTimeout, PartK.Ops, PartK.NewTabTitle);
        app.WaitFor(() => app.ShownElements("NewWorkspaceButton").Count == 1, "the new tab's welcome page", StepTimeout);
        app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.TAB);
        app.WaitForText("CurrentBranch", "feature/alpha", StepTimeout);
        app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
        app.WaitForFocusedElement("FilterBox", StepTimeout);
        app.PressKey(VirtualKeyShort.F6);
        app.WaitForFocusedElement("GraphRows", StepTimeout);

        // Fetch's key and help text, as UI Automation gives them.
        var fetch = app.Find("FetchButton");
        AssertButton(app, "FetchButton", true, PartK.OpsToolbar.Single(button => button.Id == "FetchButton").ToolTip);
        Assert.Equal("Ctrl+Shift+F", RealApp.AcceleratorKeyOf(fetch));

        // A real right-click in the empty graph area: Fetch all remotes's key (UI Automation's property 30006) and the key it draws; Esc.
        app.RightClickAt((float)PartK.EmptyAreaPoint.X, (float)PartK.EmptyAreaPoint.Y);
        app.WaitForMenu();
        var item = app.FindAll("ContextMenuItem").Single(candidate => candidate.Name == PartK.FetchAllRemotes);
        Assert.Equal("Ctrl+Shift+F", RealApp.AcceleratorKeyOf(item));
        Assert.Equal("Ctrl+Shift+F", app.MenuItemKeyText(item));
        CloseMenuWithEscape(app);

        Assert.Equal(0, app.Close());
    }

    // ---- Part K's helpers ----

    /// <summary>The index of the command <paramref name="label"/> among the empty palette's on "ops" (<see cref="PartK.OpsCommands"/>), or -1.</summary>
    private static int IndexOf(string label) => Array.FindIndex(PartK.OpsCommands, entry => entry.Label == label);

    /// <summary>How many <c>Command palette ran:</c> lines of "ops" the log has since <paramref name="mark"/>.</summary>
    private static int RanLines(RealApp app, int mark) =>
        app.CountLogLinesSince(AppLogFile.Containing($"] {PartK.Ops}: {PartK.PaletteRanText}"), mark);

    /// <summary>A palette row as the report writes it, for comparing: label · detail · key · reason, and the highlight.</summary>
    private static string Line(string label, string? detail, string? key, string? reason, bool highlighted) =>
        $"{label} · {detail ?? string.Empty} · {key ?? string.Empty} · {reason ?? string.Empty}{(highlighted ? " (highlighted)" : string.Empty)}";

    /// <summary>
    /// Waits until the palette's rows whole in its list's view are <paramref name="expected"/>, top to
    /// bottom, as UI Automation reads them (name, item status, accelerator key, help text), the row
    /// <paramref name="highlighted"/> of them highlighted (selected) and no other. The rows' colours
    /// and widths are the scripted run's.
    /// </summary>
    private static void AssertPaletteInView(RealApp app, IReadOnlyList<PartK.Entry> expected, int highlighted)
    {
        var want = expected.Select((entry, index) => Line(entry.Label, entry.Detail, entry.Key, entry.Reason, index == highlighted)).ToList();
        List<string> Shown() => [.. app.PaletteRowsInView().Select(row => Line(row.Label, row.Detail, row.Key, row.Reason, row.IsHighlighted))];
        WaitTelling(
            app,
            () => Shown().SequenceEqual(want),
            "the palette's rows in view",
            () => $"Expected:\n{string.Join('\n', want)}\nShown:\n{string.Join('\n', Shown())}");
    }

    /// <summary>Waits until the highlighted row is <paramref name="label"/>, whole in the list's view.</summary>
    private static void AssertHighlightInView(RealApp app, string label) =>
        WaitTelling(
            app,
            () => app.PaletteRows().Where(row => row.IsHighlighted).ToList() is [var row] && row.Label == label && row.InView,
            $"the palette's highlight on \"{label}\", whole in view",
            () => $"The highlighted rows: {string.Join(" | ", app.PaletteRows().Where(row => row.IsHighlighted))}; in view: {app.DescribePaletteRows()}.");

    /// <summary>The row <paramref name="label"/> as UI Automation gives it (K10's "UIA of row …"): its item status, help text, accelerator key and selection.</summary>
    private static void AssertPaletteRow(RealApp app, string label, string? detail, string? reason, string? key, bool highlighted)
    {
        var row = app.PaletteRowsInView().Where(candidate => candidate.Label == label).ToList() switch
        {
            [var only] => only,
            var found => throw new Xunit.Sdk.XunitException($"{found.Count} rows in view are named \"{label}\"; in view: {app.DescribePaletteRows()}."),
        };
        Assert.Equal((detail ?? string.Empty, reason ?? string.Empty, key ?? string.Empty, highlighted), (row.Detail, row.Reason, row.Key, row.IsHighlighted));
    }

    /// <summary>Waits until the details panel shows "Main work" (<see cref="PartK.MainWorkSha"/>), the selected row's commit.</summary>
    private static void WaitForMainWorkDetails(RealApp app) =>
        app.WaitFor(
            () => app.FindAll("DetailsSubject").Any(element => element.Name == "Main work") && app.FindAll("DetailsSha").Any(element => element.Name == PartK.MainWorkSha),
            $"the details of \"Main work\" ({PartK.MainWorkSha})",
            StepTimeout);

    /// <summary>Waits until the status bar's operation text is <paramref name="text"/>.</summary>
    private static void WaitForOperationStatus(RealApp app, string text) =>
        WaitTelling(app, () => app.OperationStatusText() == text, $"the status bar to say \"{text}\"", () => $"It says \"{app.OperationStatusText()}\".");

    /// <summary>
    /// The name dialog Create branch… opens (phase 3's, unchanged; K10 step 5) as UI Automation reads
    /// it: its title, its context from HEAD's commit, the empty name box with its placeholder and the
    /// keyboard focus, no error, the option ticked, Cancel and a disabled Create branch.
    /// </summary>
    private static void AssertCreateBranchDialog(RealApp app)
    {
        Assert.Equal([PartK.FromAlphaTwo], app.NamesOnScreen("NameDialogContext"));
        app.WaitFor(() => app.HasFocusOnScreen("NameBox"), "the name box to have the keyboard focus", StepTimeout);
        Assert.Equal(string.Empty, app.BoxText("NameBox"));
        Assert.Equal(PartK.BranchNamePlaceholder, app.Find("NameBox").Name);
        Assert.Empty(app.ShownElements("NameError"));
        var option = app.Find("NameDialogOption");
        Assert.Equal(PartK.CheckOutTheNewBranch, option.Name);
        Assert.True(RealApp.IsOn(option), $"\"{PartK.CheckOutTheNewBranch}\" is not ticked.");
        Assert.Equal([PartK.Cancel], app.NamesOnScreen("CancelButton"));
        var create = app.Find("ConfirmButton");
        Assert.Equal((PartK.CreateBranchTitle, false), (create.Name, create.IsEnabled));
    }
}
