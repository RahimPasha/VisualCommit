// Part L's two launches of the 8a pass (docs/test-reports/phase-8a.md, "The real window (part L)";
// docs/phase-8a-design.md 12.3.4 launches 1 and 2; D410, D418): the placeholders in their own token, dark
// and light, the typed filter, the dark danger button's label, the danger menu item highlighted by the
// keys, and HEAD's name in the accent on its row idle, hovered and selected. Each launch is the twin of
// part L's scripted method 1 or 2 and takes its steps in the same order; nothing is confirmed.
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;
using AvaloniaColor = Avalonia.Media.Color;

namespace VisualCommit.RealWindowTests;

public partial class Phase8aRealWindowPass
{
    /// <summary>The lanes of "changes"' graph: two commits on <c>main</c>, one lane, so that its Message column starts at x 438.</summary>
    private const int ChangesLanes = 1;

    /// <summary>"changes"' stage panel once the working-changes row is selected: seven unstaged files, three staged.</summary>
    private const string ChangesUnstagedTitle = "Unstaged files (7)";

    /// <inheritdoc cref="ChangesUnstagedTitle"/>
    private const string ChangesStagedTitle = "Staged files (3)";

    /// <summary>How many notches of the wheel a list is turned at most to bring a row into its view.</summary>
    private const int MostNotches = 10;

    /// <summary>
    /// Launch 1 (design 12.3.4; the twin of part L's method 1, <c>Check_L1_L5_L2_changes_placeholders_and_danger_button</c>),
    /// one tab on a fresh "changes", dark, 1100×700:
    /// <list type="number">
    /// <item>L1: a real click on graph row 0, the working-changes row; <c>FilterBox</c>, <c>CommitSummary</c>
    /// and <c>CommitDescription</c> empty and unfocused, each placeholder's ink <c>#8F97A4</c> on the box's
    /// <c>#272C36</c> (more than 8 from <c>#616773</c>), <c>CommitSummary</c>'s rectangle ending at its
    /// 40 px right padding; <c>l1a-placeholders-dark</c>.</item>
    /// <item>A real Ctrl+Shift+E, <c>sea</c> typed on the real keyboard: its ink <c>#E4E7EC</c>, less the
    /// caret's column; <c>l1b-filter-typed-dark</c>; the filter emptied, as the twin's L5 starts.</item>
    /// <item>L5 dark: the discard of <c>src/Calculator.cs</c> asked as the twin asks it (the list brought to
    /// the row, the real pointer onto the row, a real click on its <c>RowDiscardButton</c>); a spot of the
    /// danger button <c>#EA7D86</c>, its label's ink <c>#10121A</c> (more than 8 from <c>#FFFFFF</c>);
    /// <c>l5a-discard-dialog-dark</c>; Cancel; <c>git status --porcelain</c> still lists the file modified.</item>
    /// <item>A real click on <c>ThemeSwitch</c>; L2: the same three boxes <c>#68707D</c> on <c>#FFFFFF</c>
    /// (more than 8 from <c>#ADB2B8</c>); <c>l2a-placeholders-light</c>; a real Ctrl+Shift+E, <c>sea</c>
    /// typed: its ink <c>#1B1F27</c> on <c>#FFFFFF</c>, less the caret's column; <c>l2b-filter-typed-light</c>.</item>
    /// </list>
    /// The app closed by its window, exit code 0; the repository as it was.
    /// </summary>
    [Fact]
    public async Task Check_L_changes_placeholders_and_danger_button()
    {
        using var notes = NoteLaunch(L.ChangesLaunch.Method);
        AssertDesktopReady();
        using var repo = await Scenarios.ChangesAsync();
        using var app = Opened(Launch(Run.NewDataDirectory(), OneTab(repo.Path)), NameOf(repo));
        var exact = await repo.ExactStateAsync();

        // L1: graph row 0, the working-changes row, by a real click; the stage panel with its lists; the
        // three boxes empty and unfocused.
        var workingRow = app.GraphRowPoint(0, ChangesLanes);
        app.ClickAt(workingRow.X, workingRow.Y);
        app.WaitForStageTitles(ChangesUnstagedTitle, ChangesStagedTitle, StepTimeout);
        AssertBoxesEmptyAndUnfocused(app);
        Picture(app, L.L1aPlaceholdersDark, keepPointer: false, capture => Placeholders(app, capture, L.Dark));

        // L1: Ctrl+Shift+E and sea on the real keyboard: the typed text's ink, less the caret's column.
        TypeIntoFilter(app);
        Picture(app, L.L1bFilterTypedDark, keepPointer: false, capture => TypedText(app, capture, L.Dark));

        // The filter emptied, as the twin's L5 starts.
        EmptyFilter(app);

        // L5 dark: the discard asked; the danger button's background and its dark label; Cancel.
        var dialog = AskDiscard(app);
        Picture(app, L.L5aDiscardDialogDark, keepPointer: false, capture => DangerButton(app, capture, dialog));
        Press(app, app.FindIn(dialog, "CancelButton"));
        app.WaitUntilGone("ConfirmationDialog", StepTimeout);
        var status = (await repo.GitAsync("--no-optional-locks", "status", "--porcelain")).StandardOutput;
        var modified = status.Split('\n').Any(line => line.TrimEnd('\r') == " M " + L.Calculator);
        Note($"after Cancel, git status --porcelain lists {L.Calculator} modified: {modified}");
        Assert.True(modified, $"After Cancel, git status --porcelain does not list {L.Calculator} modified: {status}");
        await AssertStateUnchangedAsync(repo, exact, "after Cancel");

        // A real click on ThemeSwitch; the window light, named by FilterBox's background turned white.
        Press(app, app.Find("ThemeSwitch"));
        WaitForSpot(app, "FilterBox's background in the light theme (TextControlBackground)", LeftPaddingOf(app, app.Find("FilterBox")), L.Light.Box);

        // L2: the same three boxes in light.
        AssertBoxesEmptyAndUnfocused(app);
        Picture(app, L.L2aPlaceholdersLight, keepPointer: false, capture => Placeholders(app, capture, L.Light));

        // L2: Ctrl+Shift+E and sea in light: its ink, less the caret's column.
        TypeIntoFilter(app);
        Picture(app, L.L2bFilterTypedLight, keepPointer: false, capture => TypedText(app, capture, L.Light));

        Assert.Equal(0, CloseNoted(app));
        await AssertStateUnchangedAsync(repo, exact, "at the end");
    }

    /// <summary>
    /// Launch 2 (design 12.3.4; the twin of part L's method 2, <c>Check_L4a_L6_graph_danger_item_and_head_row</c>),
    /// one tab on a fresh "graph", dark, 1100×700:
    /// <list type="number">
    /// <item>L4 a: <c>Local branches › feature</c> opened and its <c>login</c> selected by real clicks, as the
    /// twin's; the pointer to the neutral point (so that the menu opens with no item hovered); a real
    /// Shift+F10: the menu headed <c>feature/login</c>; real Down until <c>Delete feature/login…</c> is
    /// highlighted (the focus and the highlight go together): its highlight <c>#2F3542</c>, its label's ink
    /// <c>#EA7D86</c> (more than 8 from <c>#E5606B</c>); <c>l4a-delete-item-menu</c>, pointer kept; Esc.</item>
    /// <item>L6 a: <c>main</c>'s row, the pointer at the neutral point: the panel's <c>#1A1D24</c>, the
    /// name's ink <c>#7C8CFF</c> (more than 8 from <c>#8B9AFF</c>); <c>l6a-head-idle</c>.</item>
    /// <item>L6 b: the pointer at the row's centre: the hover <c>#2F3542</c>, the name's ink <c>#8B9AFF</c>
    /// (more than 8 from <c>#7C8CFF</c>), no tooltip; <c>l6b-head-hovered</c>, pointer kept.</item>
    /// <item>L6 c: a real click on the row, the pointer at the neutral point: the selection <c>#2A3150</c>,
    /// the name's ink <c>#8B9AFF</c>, <c>feature/login</c>'s row no longer selected; <c>l6c-head-selected</c>.</item>
    /// </list>
    /// L6's three inks are read in the rectangle of <c>main</c>'s text element; where UI Automation gives
    /// none, each read is a problem, asserted after its picture's comparison (<see cref="ReadHeadInk"/>).
    /// The app closed by its window, exit code 0; the repository as it was, no backup made.
    /// </summary>
    [Fact]
    public async Task Check_L_graph_danger_item_and_head_row()
    {
        using var notes = NoteLaunch(L.GraphLaunch.Method);
        AssertDesktopReady();
        using var repo = await Scenarios.GraphAsync();
        using var app = Opened(Launch(Run.NewDataDirectory(), OneTab(repo.Path)), NameOf(repo));
        var exact = await repo.ExactStateAsync();
        var dark = L.Dark;

        // L4 a: feature opened by a real click; login selected by a real click, with the keyboard focus.
        Press(app, app.RefRow(L.LocalBranches, L.Feature));
        app.WaitFor(() => app.HasRefRow(L.LocalBranches, L.Feature, L.Login), "Local branches › feature to open", StepTimeout);
        Press(app, app.RefRow(L.LocalBranches, L.Feature, L.Login));
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.RefRow(L.LocalBranches, L.Feature, L.Login)), "feature/login's row to have the keyboard focus", StepTimeout);

        // The pointer to the neutral point before the menu opens under it; Shift+F10; Down to Delete feature/login….
        app.MoveToNeutral();
        Keys(app, VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
        app.WaitForMenu(StepTimeout);
        app.WaitForText("ContextMenuHeader", L.FeatureLoginMenuHeader, StepTimeout);
        var delete = HighlightMenuItem(app, L.DeleteFeatureLogin);
        Picture(app, L.L4aDeleteItemMenu, keepPointer: true, capture =>
        {
            var item = app.BoundsOf(delete);
            return
            [
                .. ReadSpot(capture, app.Scaling, $"{L.DeleteFeatureLogin}'s highlight (VcHoverBackgroundBrush)", new PointF(item.Right - 6, item.Top + 3), dark.Hover),
                .. ReadInk(capture, app.Scaling, $"{L.DeleteFeatureLogin}'s label (VcDangerBrush)", LabelArea(app, delete, L.DeleteFeatureLogin, $"{L.DeleteFeatureLogin}'s label"), dark.Danger, dark.Hover, dark.DangerReplaced),
            ];
        });
        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", StepTimeout);

        // L6 a: main's row, the pointer at the neutral point: neither hovered nor selected.
        var main = app.RefRow(L.LocalBranches, L.Main);
        var clear = ClearOf(app, main);
        var name = TextArea(app, main, L.Main);
        Picture(app, L.L6aHeadIdle, keepPointer: false, capture =>
        [
            .. ReadSpot(capture, app.Scaling, "main's row idle (VcPanelBackgroundBrush)", clear, dark.Panel),
            .. ReadHeadInk(capture, app.Scaling, "main idle (VcAccentBrush)", name, dark.Accent, dark.Panel, dark.AccentOnRow),
        ]);

        // L6 b: the pointer at the row's centre, right of main's name, where no tooltip opens: hovered.
        app.SetPointerOn(main);
        WaitForSpot(app, "main's row hovered (VcHoverBackgroundBrush)", clear, dark.Hover);
        Picture(app, L.L6bHeadHovered, keepPointer: true, capture =>
        [
            .. ReadSpot(capture, app.Scaling, "main's row hovered (VcHoverBackgroundBrush)", clear, dark.Hover),
            .. ReadHeadInk(capture, app.Scaling, "main hovered (VcAccentOnRowBrush)", name, dark.AccentOnRow, dark.Hover, dark.Accent),
            .. NoToolTip(app, L.L6bHeadHovered),
        ]);

        // L6 c: a real click on the row, the pointer at the neutral point: selected, and login no longer.
        Press(app, main);
        app.MoveToNeutral();
        WaitForSpot(app, "main's row selected (VcSelectionBrush)", clear, dark.Selection);
        var login = ClearOf(app, app.RefRow(L.LocalBranches, L.Feature, L.Login));
        Picture(app, L.L6cHeadSelected, keepPointer: false, capture =>
        [
            .. ReadSpot(capture, app.Scaling, "main's row selected (VcSelectionBrush)", clear, dark.Selection),
            .. ReadHeadInk(capture, app.Scaling, "main selected (VcAccentOnRowBrush)", name, dark.AccentOnRow, dark.Selection, dark.Accent),
            .. ReadSpot(capture, app.Scaling, "feature/login's row no longer selected (VcPanelBackgroundBrush)", login, dark.Panel),
        ]);

        Assert.Equal(0, CloseNoted(app));
        await AssertStateUnchangedAsync(repo, exact, "at the end");
        var backups = await repo.BackupRefsAsync();
        Note($"at the end: backup refs (refs/visualcommit/backup/): {(backups.Count == 0 ? "none" : string.Join(", ", backups.Select(entry => entry.Name)))}");
        Assert.Empty(backups);
    }

    // ---- Part L's steps ----

    /// <summary>
    /// L6 a to c's read of <c>main</c>'s ink (<see cref="ReadInk"/>) in <paramref name="name"/>, the rectangle
    /// of its text element (<see cref="TextArea"/>). Where UI Automation gives none, the read is a problem
    /// (<see cref="NoHeadNameArea"/>), noted, which <see cref="Picture"/> asserts after the comparison, as
    /// K2's gutter spots are: the comparison cannot carry this colour (design 12.3.3's fallback), since it
    /// matches colours within 48 a channel (D58) and L6's two accents lie 15 apart at most.
    /// </summary>
    private IEnumerable<string> ReadHeadInk(Bitmap capture, double scaling, string what, RectangleF? name, AvaloniaColor foreground, AvaloniaColor background, AvaloniaColor replaced)
    {
        if (name is not { } area)
        {
            var missing = NoHeadNameArea(what);
            Note(missing);
            return [missing];
        }

        return ReadInk(capture, scaling, what, area, foreground, background, replaced);
    }

    /// <summary>L6's problem when <c>main</c>'s name has no rectangle (<see cref="ReadHeadInk"/>): the read <paramref name="what"/>, why it was not read and why the comparison cannot stand for it.</summary>
    internal static string NoHeadNameArea(string what) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{what}: UI Automation gives {L.Main}'s name no text element in its row or beside it, so its ink was not read; the comparison with the twin, which matches colours within 48 a channel (D58), cannot tell {Hex(Drawn(L.Dark.Accent))} from {Hex(Drawn(L.Dark.AccentOnRow))}.");

    /// <summary>L1's and L2's three boxes: <c>FilterBox</c>, <c>CommitSummary</c>, <c>CommitDescription</c>, with the right padding each box's text area ends at.</summary>
    private static IReadOnlyList<(string Id, string Placeholder, float RightPadding)> PlaceholderBoxes { get; } =
    [
        ("FilterBox", L.Filter, BoxInset),
        ("CommitSummary", L.Summary, (float)L.SummaryRightPadding),
        ("CommitDescription", L.Description, BoxInset),
    ];

    /// <summary>The three boxes are empty and none has the keyboard focus, as UI Automation reports them.</summary>
    private void AssertBoxesEmptyAndUnfocused(RealApp app)
    {
        foreach (var (id, _, _) in PlaceholderBoxes)
        {
            var box = app.Find(id);
            var text = app.BoxText(id);
            var focused = RealApp.HasKeyboardFocus(box);
            Note($"{id}: holds \"{text}\", focused {focused}");
            Assert.True(text.Length == 0, $"{id} holds \"{text}\", not nothing.");
            Assert.False(focused, $"{id} has the keyboard focus.");
        }
    }

    /// <summary>
    /// L1's and L2's reads in one capture: each box's background at a point of its left padding, and its
    /// placeholder's ink in the box's text area, more than 8 from the colour the placeholder replaced.
    /// </summary>
    private IEnumerable<string> Placeholders(RealApp app, Bitmap capture, L.Colours colours)
    {
        List<string> problems = [];
        foreach (var (id, placeholder, rightPadding) in PlaceholderBoxes)
        {
            var box = app.Find(id);
            problems.AddRange(ReadSpot(capture, app.Scaling, $"{id}'s background (TextControlBackground)", LeftPaddingOf(app, box), colours.Box));
            problems.AddRange(ReadInk(capture, app.Scaling, $"{id}'s placeholder \"{placeholder}\" (VcTextPlaceholderBrush)", BoxTextArea(app, box, rightPadding), colours.Placeholder, colours.Box, colours.PlaceholderReplaced));
        }

        return problems;
    }

    /// <summary>
    /// The typed <c>sea</c>'s reads: <c>FilterBox</c> holds it and has the focus, its background at a point
    /// of its left padding, and the text's ink in the box's text area less the caret's column. Each read is
    /// noted and returned as a problem, never asserted here, so that the comparison with the twin runs first.
    /// </summary>
    private IEnumerable<string> TypedText(RealApp app, Bitmap capture, L.Colours colours)
    {
        var box = app.Find("FilterBox");
        var text = app.BoxText("FilterBox");
        var focused = RealApp.HasKeyboardFocus(box);
        Note($"FilterBox holds \"{text}\", focused {focused}");
        List<string> problems = [];
        if (text != L.Typed)
        {
            problems.Add($"FilterBox holds \"{text}\", not \"{L.Typed}\".");
        }

        if (!focused)
        {
            problems.Add("FilterBox does not have the keyboard focus.");
        }

        problems.AddRange(ReadSpot(capture, app.Scaling, "FilterBox's background (TextControlBackground)", LeftPaddingOf(app, box), colours.Box));
        var what = $"the text \"{L.Typed}\" typed into FilterBox (TextControlForeground)";
        problems.AddRange(ReadInk(capture, app.Scaling, what, TypedArea(app, capture, box, colours.Box, what), colours.Typed, colours.Box, replaced: null));
        return problems;
    }

    /// <summary>
    /// A real Ctrl+Shift+E: <c>FilterBox</c> UI Automation's focused element; <c>sea</c> typed on the real
    /// keyboard; a wait until the box holds it and the left panel filters by it (<c>main</c> no longer listed).
    /// </summary>
    private static void TypeIntoFilter(RealApp app)
    {
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
        app.WaitForFocusOn("FilterBox", StepTimeout);
        TypeKeys(app, L.Typed);
        app.WaitFor(
            () => app.BoxText("FilterBox") == L.Typed && !app.HasRefRow(L.LocalBranches, L.Main),
            $"FilterBox to hold \"{L.Typed}\" and the left panel to filter by it",
            StepTimeout);
    }

    /// <summary>The filter emptied as the twin empties it: a real Ctrl+A and Backspace; a wait until the box is empty and the left panel lists <c>main</c> again.</summary>
    private static void EmptyFilter(RealApp app)
    {
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keys(app, VirtualKeyShort.BACK);
        app.WaitFor(
            () => app.BoxText("FilterBox").Length == 0 && app.HasRefRow(L.LocalBranches, L.Main),
            "FilterBox to be empty and the left panel to list main again",
            StepTimeout);
    }

    /// <summary>
    /// The discard of <c>src/Calculator.cs</c> asked as the twin asks it: the unstaged list brought to the
    /// row (the twin's list scrolls it into view), the real pointer moved onto the row's centre, a real click
    /// on its <c>RowDiscardButton</c> once it shows; then the confirmation <c>Discard changes?</c> with its
    /// question, <c>Cancel</c> UI Automation's keyboard focus. Returns the dialog.
    /// </summary>
    private AutomationElement AskDiscard(RealApp app)
    {
        var row = BringStageRowIntoView(app, "UnstagedFileList", L.Calculator);
        app.SetPointerOn(row);
        Press(app, RowButton(app, "UnstagedFileList", L.Calculator, "RowDiscardButton"));
        app.WaitFor(() => app.ShownElements("ConfirmationDialog").Count == 1, "the discard's confirmation", StepTimeout);
        var dialog = app.ShownElements("ConfirmationDialog")[0];
        app.WaitForText("ConfirmQuestion", L.DiscardQuestion, StepTimeout);
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.FindIn(dialog, "CancelButton")), "Cancel to have the keyboard focus", StepTimeout);
        return dialog;
    }

    /// <summary>
    /// L5 dark's reads: the confirmation's title, its buttons' names, Cancel's focus; the danger button's
    /// background at a point of its left padding, <c>#EA7D86</c>; its label's ink <c>#10121A</c> on it,
    /// more than 8 from the <c>#FFFFFF</c> it replaced. Each read is noted and returned as a problem, never
    /// asserted here, so that the comparison with the twin runs first. The button is read, never pressed.
    /// </summary>
    private IEnumerable<string> DangerButton(RealApp app, Bitmap capture, AutomationElement dialog)
    {
        var dark = L.Dark;
        var titles = app.NamesOnScreen("ConfirmTitle");
        var cancel = app.FindIn(dialog, "CancelButton");
        var confirm = app.FindIn(dialog, "ConfirmButton");
        var (cancelName, cancelFocused, confirmName) = (cancel.Name, RealApp.HasKeyboardFocus(cancel), confirm.Name);
        Note($"the discard's confirmation: title {string.Join(" | ", titles.Select(title => $"\"{title}\""))}; \"{cancelName}\" focused {cancelFocused}; \"{confirmName}\"");
        List<string> problems = [];
        if (!titles.SequenceEqual([L.DiscardTitle]))
        {
            problems.Add($"The confirmation's title reads {string.Join(" | ", titles.Select(title => $"\"{title}\""))}, not \"{L.DiscardTitle}\".");
        }

        if (cancelName != L.Cancel || !cancelFocused)
        {
            problems.Add($"The confirmation's Cancel is named \"{cancelName}\" and focused {cancelFocused}, not \"{L.Cancel}\" and focused.");
        }

        if (confirmName != L.Discard)
        {
            problems.Add($"The confirmation's danger button is named \"{confirmName}\", not \"{L.Discard}\".");
        }

        problems.AddRange(ReadSpot(capture, app.Scaling, "Discard's background (VcDangerBrush)", LeftPaddingOf(app, confirm), dark.Danger));
        problems.AddRange(ReadInk(capture, app.Scaling, "Discard's label (VcDangerTextBrush)", LabelArea(app, confirm, L.Discard, "Discard's label"), dark.DangerText, dark.Danger, dark.DangerTextReplaced));
        return problems;
    }

    /// <summary>
    /// Turns the real wheel over <paramref name="list"/> (a stage list), one notch at a time at its centre,
    /// until the row of <paramref name="path"/> lies wholly inside the list's view, as the twin's
    /// <c>ScrollIntoView</c> brings it: at most <see cref="MostNotches"/> notches within
    /// <see cref="StepTimeout"/>. run.txt gets the notches. Returns the row (<c>StageFileRow</c>).
    /// </summary>
    private AutomationElement BringStageRowIntoView(RealApp app, string list, string path)
    {
        var clock = Stopwatch.StartNew();
        for (var notches = 0; ; notches++)
        {
            var view = app.BoundsOf(app.ShownElements(list) is [var only] ? only : throw new InvalidOperationException($"The window shows {app.ShownElements(list).Count} {list}, not one."));
            var row = StageRowIn(app, list, path);
            var bounds = row is null ? RectangleF.Empty : app.BoundsOf(row);
            if (row is not null && bounds.Top >= view.Top - 0.5f && bounds.Bottom <= view.Bottom + 0.5f)
            {
                Note(string.Create(CultureInfo.InvariantCulture, $"{path}'s row wholly in {list}'s view after {notches} notches of the wheel, at y {bounds.Top:0.#} to {bounds.Bottom:0.#}"));
                return row;
            }

            if (notches >= MostNotches || clock.Elapsed >= StepTimeout)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}'s row did not come wholly into {list}'s view after {notches} notches of the wheel: {(row is null ? "it is not laid out" : $"it lies at y {bounds.Top:0.#} to {bounds.Bottom:0.#}")}, the view at y {view.Top:0.#} to {view.Bottom:0.#}."));
            }

            // Up (a positive notch) while the row lies above the view, down otherwise.
            app.ScrollWheel(view.X + (view.Width / 2), view.Y + (view.Height / 2), row is not null && bounds.Top < view.Top ? 1 : -1);
        }
    }

    /// <summary>The row of <paramref name="path"/> in the stage list <paramref name="list"/>, laid out (its bounds not empty); null when it is not.</summary>
    private static AutomationElement? StageRowIn(RealApp app, string list, string path) =>
        app.ShownElements(list).FirstOrDefault()?
            .FindAllDescendants(condition => condition.ByAutomationId("StageFileRow"))
            .FirstOrDefault(row => row.Name == path && row.BoundingRectangle is { Width: > 0, Height: > 0 });

    /// <summary>
    /// The button <paramref name="id"/> of <paramref name="path"/>'s row in <paramref name="list"/>, waited
    /// for: among the shown elements of that id, the one whose vertical centre lies within the row's
    /// <c>StageFileRow</c> (a focused row shows its buttons to UI Automation too, D425, so an id alone can
    /// meet two rows' buttons); exactly one, within <see cref="StepTimeout"/>.
    /// </summary>
    private static AutomationElement RowButton(RealApp app, string list, string path, string id)
    {
        AutomationElement? found = null;
        var count = 0;
        try
        {
            app.WaitFor(
                () =>
                {
                    if (StageRowIn(app, list, path) is not { } row)
                    {
                        return false;
                    }

                    var bounds = app.BoundsOf(row);
                    var onRow = app.ShownElements(id)
                        .Where(button =>
                        {
                            var place = app.BoundsOf(button);
                            var centre = place.Top + (place.Height / 2);
                            return centre > bounds.Top && centre < bounds.Bottom;
                        })
                        .ToList();
                    count = onRow.Count;
                    found = count == 1 ? onRow[0] : null;
                    return found is not null;
                },
                $"one {id} on {path}'s row in {list}",
                StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} The row showed {count}.", ex);
        }

        return found!;
    }

    /// <summary>
    /// Real Down until the open menu's item <paramref name="label"/> is highlighted: a menu highlights its
    /// first enabled item and gives it the focus as it opens, and Down moves both (D105), so the highlight
    /// is UI Automation's keyboard focus. Each Down is followed by a wait until the focus has moved; at
    /// most as many presses as the menu has items. run.txt gets the presses. Returns the item, enabled.
    /// </summary>
    private AutomationElement HighlightMenuItem(RealApp app, string label)
    {
        AutomationElement? Highlighted() => app.FindAll("ContextMenuItem").FirstOrDefault(RealApp.HasKeyboardFocus);
        app.WaitFor(() => Highlighted() is not null, "an item of the menu with the focus and the highlight", StepTimeout);
        var items = app.FindAll("ContextMenuItem").Count;
        var presses = 0;
        while (Highlighted() is not { } now || now.Name != label)
        {
            if (presses >= items)
            {
                throw new InvalidOperationException($"{presses} presses of Down did not highlight \"{label}\"; the menu has: {string.Join(" | ", app.MenuItemNames())}.");
            }

            var before = Highlighted()?.Name;
            Keys(app, VirtualKeyShort.DOWN);
            presses++;
            app.WaitFor(() => Highlighted() is { } next && next.Name != before, $"Down to move the highlight from \"{before}\"", StepTimeout);
        }

        var item = Highlighted()!;
        var enabled = item.IsEnabled;
        Note($"\"{label}\" highlighted after {presses} presses of Down, enabled {enabled}");
        Assert.True(enabled, $"The menu item \"{label}\" is disabled.");
        return item;
    }

    /// <summary>A problem when a tooltip shows in the window, as none does in the twin of a hover that lies off every text with a tooltip; run.txt names what shows.</summary>
    private IEnumerable<string> NoToolTip(RealApp app, string picture)
    {
        var tips = ToolTipsShown(app).Select(tip => tip.Name).ToList();
        Note($"{picture}: tooltips shown: {(tips.Count == 0 ? "none" : string.Join(" | ", tips))}");
        return tips.Count == 0 ? [] : [$"{picture} has a tooltip open, which its twin has not: {string.Join(" | ", tips)}."];
    }
}
