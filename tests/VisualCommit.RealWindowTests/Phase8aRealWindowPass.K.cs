// Part K's two launches of the 8a pass (docs/test-reports/phase-8a.md, K1, K2 and "The real window (part
// K)"; docs/phase-8a-design.md 6.3, 12.3.4 launches 11 and 12, 12.3.5; D425, D426): a hunk and two chosen
// lines staged by real keys from a stage row reached by F6 and Tab, with the toolbar reached by Shift+Tab;
// and a blame run picked by real arrows, with its menu by Shift+F10. Each launch is the twin of part K's
// scripted method 1 or 2 and takes its steps in the same order, every key pressed only after the wait its
// twin makes. The real pointer stays at the neutral point, off the stage rows, through the walks and at
// every capture: a hovered row would show its own buttons beside the focused row's (D425). Nothing is
// pressed that leaves the app: K2's menu is closed by Esc, "Copy SHA" never pressed (D444).
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Testing;
using Xunit;
using Xunit.Sdk;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase8aRealWindowPass
{
    /// <summary>The lanes of "history"'s graph (the scenario's H0): its Message column starts at x 438, as "changes"' does.</summary>
    private const int HistoryLanes = VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB.HistoryLanes;

    /// <summary><c>settings.json</c> of launch 12, written before the start: the light theme its twin starts in (D442).</summary>
    private const string LightSettingsFile = "{ \"theme\": \"Light\" }";

    /// <summary>The start's log line with Light chosen in <c>settings.json</c>.</summary>
    private const string LightAtStart = "Theme: Light.";

    /// <summary>The stage panel's title over the staged list; its text block has this automation id, which <c>K.Ids</c> does not hold (design 12.3.5).</summary>
    private const string StagedTitleId = "StagedTitle";

    /// <summary>The blame page's own automation id (a pane named by its title).</summary>
    private const string BlamePageId = "Blame";

    /// <summary>The automation id of an item of the app's menus, which its label names.</summary>
    private const string MenuItemId = "ContextMenuItem";

    /// <summary>How often a wait on git's state reads it again.</summary>
    private static readonly TimeSpan GitPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Launch 11 (design 12.3.4; the twin of part K's method 1, <c>Check_K1_hunk_by_keys</c>), one tab on a
    /// fresh "changes", dark, 1100×700:
    /// <list type="number">
    /// <item>A real click on graph row 0, the working-changes row; the stage panel with <c>Unstaged files
    /// (7)</c> and <c>Staged files (3)</c>; the pointer back at the neutral point, off the stage rows.</item>
    /// <item>A real F6: <c>README.md</c>'s <c>StageFileRow</c> in the unstaged list focused; 18 real Tabs along
    /// <c>K.CalculatorWalk</c>, each waited on by UI Automation's focused element (its id, and its row by the
    /// row's name or by its place on the row), ending on <c>src/Calculator.cs</c>'s <c>StageFileRow</c>;
    /// nothing staged.</item>
    /// <item>A real Enter: the focus in <c>DiffText</c> (the text's wait, nothing between); the diff of
    /// <c>Calculator.cs</c> with two hunk bars. A real Tab: the first hunk's <c>StageHunkButton</c> focused;
    /// <c>k1a-hunk-button</c>.</item>
    /// <item>A real Enter: <c>Staged files (4)</c> and one <c>StageHunkButton</c> left; <c>git diff --cached
    /// --numstat</c> prints <c>1 1 src/Calculator.cs</c>, <c>git diff --numstat</c> <c>4 0
    /// src/Calculator.cs</c>; then the text's wait; <c>k1b-hunk-staged</c>.</item>
    /// <item>A real Shift+Tab: <c>DiscardFileButton</c> focused; <c>k1c-toolbar</c>.</item>
    /// <item>A real Tab (the text's wait), Ctrl+Home, four Downs, two Shift+Downs: <c>Stage lines</c> and
    /// <c>Discard lines</c> in place of the file's buttons from the first Shift+Down on; the pointer at the
    /// neutral point; a real Shift+F10: the menu headed <c>Calculator.cs</c>, <c>Stage selected lines</c>
    /// highlighted; <c>k1d-lines-menu</c>, pointer kept.</item>
    /// <item>A real Enter on that item: the menu closed; <c>git diff --cached --numstat</c> prints <c>3 1
    /// src/Calculator.cs</c>, <c>git diff --numstat</c> <c>2 0 src/Calculator.cs</c>.</item>
    /// </list>
    /// run.txt gets the focused element after each key of the walk, which of the text's two conditions held at
    /// each of its waits, and the <c>--numstat</c> lines. The app closed by its window, exit code 0; the
    /// repository changed only in <c>src/Calculator.cs</c>'s index entry and status line.
    /// </summary>
    [Fact]
    public async Task Check_K1_hunk_by_keys()
    {
        using var notes = NoteLaunch(nameof(Check_K1_hunk_by_keys));
        AssertDesktopReady();
        using var repo = await Scenarios.ChangesAsync();
        var name = NameOf(repo);
        Assert.Equal(K.ChangesLaunch.Scenario, name);
        using var app = Opened(Launch(Run.NewDataDirectory(), OneTab(repo.Path)), name);
        var exact = await repo.ExactStateAsync();

        // Step 2: the working-changes row by a real click, the window's first input; the pointer back at
        // the neutral point, off the stage rows, for the whole walk (D425).
        var working = app.GraphRowPoint(0, ChangesLanes);
        app.ClickAt(working.X, working.Y);
        app.WaitForStageTitles(K.Unstaged7, K.Staged3, StepTimeout);
        app.MoveToNeutral();

        // Step 3: a real F6, README.md's own button in the unstaged list.
        Keys(app, VirtualKeyShort.F6);
        WaitForWalkStop(app, K.Ids.UnstagedFileList, K.Readme, K.Ids.StageFileRow, "F6");

        // Step 4: 18 real Tabs, each waited on by the focused element before the next.
        for (var tab = 1; tab <= K.CalculatorWalk.Count; tab++)
        {
            var (row, id) = K.CalculatorWalk[tab - 1];
            Keys(app, VirtualKeyShort.TAB);
            WaitForWalkStop(app, K.Ids.UnstagedFileList, row, id, string.Create(CultureInfo.InvariantCulture, $"Tab {tab}"));
        }

        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 5: a real Enter opens the diff, sent only while the walk's last stop, src/Calculator.cs's
        // own button, still has the focus after the read of git; the text's wait comes straight after it,
        // as nothing may move the focus in between; then the diff's header and its two hunk bars are read.
        KeysOn(app, new FocusTarget(K.Ids.StageFileRow, K.Calculator), VirtualKeyShort.RETURN);
        WaitForFocusInText(app, K.Ids.DiffText, StepTimeout);
        WaitForCalculatorDiff(app, hunks: 2);

        // Step 6 (k1a): a real Tab, the first hunk's Stage hunk.
        Keys(app, VirtualKeyShort.TAB);
        var firstHunk = WaitForFirstStageHunk(app);
        Picture(app, K.K1aHunkButton, keepPointer: false, _ => []);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 7 (k1b): a real Enter stages that hunk alone, sent only while that very button still has
        // the focus after the picture and the read of git. The focus stays on the button until the
        // diff's re-read makes the bars again, so the text's wait comes after the staged list and git
        // show the hunk staged and one Stage hunk is left (design 12.3.5).
        KeysOn(app, new FocusTarget(K.Ids.StageHunkButton, K.StageHunk, firstHunk), VirtualKeyShort.RETURN);
        app.WaitFor(
            () => app.NamesOnScreen(StagedTitleId).Contains(K.Staged4) && app.ShownElements(K.Ids.StageHunkButton).Count == 1,
            $"\"{K.Staged4}\" and one {K.Ids.StageHunkButton} left after the diff's re-read",
            StepTimeout);
        await AssertNumstatAsync(repo, "after the hunk", K.StagedAfterHunk, K.UnstagedAfterHunk);
        WaitForFocusInText(app, K.Ids.DiffText, StepTimeout);
        Picture(app, K.K1bHunkStaged, keepPointer: false, _ => []);

        // Step 8 (k1c): a real Shift+Tab, Discard file, the toolbar's last shown button.
        Keys(app, VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
        app.WaitForFocusOn(K.Ids.DiscardFileButton, StepTimeout);
        Note($"Shift+Tab: the focus on {FocusNow(app)}");
        Picture(app, K.K1cToolbar, keepPointer: false, _ => []);

        // Step 9 (k1d): a real Tab back into the text; Ctrl+Home, four Downs, two Shift+Downs on the hunk
        // that remains; the menu by a real Shift+F10, the pointer at the neutral point.
        Keys(app, VirtualKeyShort.TAB);
        WaitForFocusInText(app, K.Ids.DiffText, StepTimeout);
        SelectTheTwoLines(app);
        app.MoveToNeutral();
        Keys(app, VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
        var menu = WaitForMenuByKeys(app, name, K.CalculatorName, K.StageSelectedLines);
        Assert.Equal([K.StageSelectedLines, K.DiscardSelectedLines], menu.Take(2));
        Picture(app, K.K1dLinesMenu, keepPointer: true, _ => []);

        // Step 10: a real Enter on Stage selected lines, sent only while that item still has the focus
        // after the picture: the two lines staged.
        KeysOn(app, new FocusTarget(MenuItemId, K.StageSelectedLines), VirtualKeyShort.RETURN);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", StepTimeout);
        await WaitForNumstatAsync(app, repo, cached: true, K.StagedAfterLines, "after the two lines");
        await AssertNumstatAsync(repo, "after the two lines", K.StagedAfterLines, K.UnstagedAfterLines);

        Assert.Equal(0, app.Close());
        AssertChangedOnlyIn(exact, await repo.ExactStateAsync(), K.Calculator);
    }

    /// <summary>
    /// Launch 12 (design 12.3.4; the twin of part K's method 2, <c>Check_K2_blame_by_arrows</c>), one tab on a
    /// fresh "history" with <c>{"theme": "Light"}</c> in <c>settings.json</c>, 1100×700:
    /// <list type="number">
    /// <item>A real click on graph row 0; the stage panel with <c>src/Reader.cs</c>; the pointer back at the
    /// neutral point. A real F6: <c>src/Reader.cs</c>'s <c>StageFileRow</c> focused. A real Enter: the focus in
    /// <c>DiffText</c> (the text's wait).</item>
    /// <item>A real Ctrl+Shift+B: the focus in <c>BlameText</c> (the text's wait), the page <c>Blame of
    /// Reader.cs</c>, <c>in the working tree</c>, the diff gone.</item>
    /// <item>Three real Downs (<c>K.DownsToRun2</c>), each followed by a wait for what its pick shows in UI
    /// Automation: <c>DetailsSubject</c> <c>Add the parser</c>; the stage panel (<c>UnstagedTitle</c>
    /// <c>Unstaged files (1)</c>, no details) for the run not committed yet; <c>DetailsSubject</c> <c>Rename
    /// the parser to reader</c>; and the focus still in <c>BlameText</c> after each. <c>k2a-blame-run</c>.</item>
    /// <item>The pointer at the neutral point; a real Shift+F10: the run's menu headed <c>0575c7f Rename the
    /// parser to reader</c>, <c>Show in graph</c> highlighted; <c>k2b-blame-menu</c>, pointer kept.</item>
    /// <item>A real Esc: no menu; a real Esc: no page, the graph shown.</item>
    /// </list>
    /// run.txt gets each Down's wait, which of the text's two conditions held at each of its waits, and the
    /// menu's header and items. Nothing in the menu is pressed. The app closed by its window, exit code 0;
    /// the repository as it was.
    /// </summary>
    [Fact]
    public async Task Check_K2_blame_by_arrows()
    {
        using var notes = NoteLaunch(nameof(Check_K2_blame_by_arrows));
        AssertDesktopReady();
        using var repo = await Scenarios.HistoryAsync();
        var name = NameOf(repo);
        Assert.Equal(K.HistoryLaunch.Scenario, name);
        Assert.False(K.HistoryLaunch.Dark, "Launch 12's twin starts light.");
        var data = Run.NewDataDirectory();
        File.WriteAllText(new AppPaths(data).SettingsFile, LightSettingsFile);
        using var app = Opened(Launch(data, OneTab(repo.Path)), name);
        var light = app.LogLines().Count(line => line.Contains(LightAtStart, StringComparison.Ordinal));
        Note(string.Create(CultureInfo.InvariantCulture, $"settings.json {LightSettingsFile}; the log has \"{LightAtStart}\" {light} times"));
        Assert.Equal(1, light);
        var exact = await repo.ExactStateAsync();

        // Steps 2 and 3: the working-changes row by a real click; the pointer back at the neutral point;
        // a real F6 to src/Reader.cs's row; a real Enter, the text's wait straight after it.
        var working = app.GraphRowPoint(0, HistoryLanes);
        app.ClickAt(working.X, working.Y);
        app.WaitForStageTitles(K.Unstaged1, K.Staged0, StepTimeout);
        app.MoveToNeutral();
        Keys(app, VirtualKeyShort.F6);
        WaitForWalkStop(app, K.Ids.UnstagedFileList, K.Reader, K.Ids.StageFileRow, "F6");
        KeysOn(app, new FocusTarget(K.Ids.StageFileRow, K.Reader), VirtualKeyShort.RETURN);
        WaitForFocusInText(app, K.Ids.DiffText, StepTimeout);

        // Step 4: a real Ctrl+Shift+B, the blame with the focus in its text; then its title and caption,
        // and the diff gone (the page takes the diff's place).
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_B);
        WaitForFocusInText(app, K.Ids.BlameText, StepTimeout);
        app.WaitForText(K.Ids.BlameTitle, K.BlameTitle, StepTimeout);
        app.WaitForText(K.Ids.BlameCaption, K.BlameCaption, StepTimeout);
        app.WaitUntilGone(K.Ids.DiffText, StepTimeout);
        Note($"the blame page: \"{K.BlameTitle}\", \"{K.BlameCaption}\"; DiffText gone");

        // Step 5 (k2a): three real Downs, each waited on by what its pick shows in UI Automation (the
        // caret is not read: BlameText's peer gives its value alone, the gutter's no picked run).
        Assert.Equal(K.DownsToRun2, K.BlameDowns.Count);
        foreach (var (run, line, subject) in K.BlameDowns)
        {
            Keys(app, VirtualKeyShort.DOWN);
            var clock = Stopwatch.StartNew();
            if (subject is { } details)
            {
                app.WaitFor(
                    () => app.NamesOnScreen(K.Ids.DetailsSubject).Contains(details),
                    string.Create(CultureInfo.InvariantCulture, $"run {run} (line {line}): the details of \"{details}\""),
                    StepTimeout);
            }
            else
            {
                // The run not committed yet: the working-changes row selected behind the page, the right
                // panel the stage panel, whose UnstagedTitle shows only while it is shown.
                app.WaitFor(
                    () => app.NamesOnScreen(K.Ids.UnstagedTitle).Contains(K.Unstaged1) && app.ShownElements(K.Ids.DetailsSubject).Count == 0,
                    string.Create(CultureInfo.InvariantCulture, $"run {run} (line {line}, not committed yet): the stage panel with \"{K.Unstaged1}\" and no details"),
                    StepTimeout);
            }

            var shown = subject is null ? $"the stage panel, \"{K.Unstaged1}\"" : $"the details of \"{subject}\"";
            Note(string.Create(CultureInfo.InvariantCulture, $"Down to line {line}: run {run}, {shown}, after {clock.Elapsed.TotalSeconds:0.0} s"));
            WaitForFocusInText(app, K.Ids.BlameText, StepTimeout);
        }

        Picture(app, K.K2aBlameRun, keepPointer: false, _ => []);

        // Step 6 (k2b): the pointer at the neutral point; a real Shift+F10, run 2's menu.
        app.MoveToNeutral();
        Keys(app, VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
        var menu = WaitForMenuByKeys(app, name, K.RunMenuHeader, K.ShowInGraph);
        Assert.Equal([K.ShowInGraph, K.BlameBeforeThisCommit, K.FileHistory, K.CopySha], menu);
        Picture(app, K.K2bBlameMenu, keepPointer: true, _ => []);

        // Step 7: a real Esc, no menu; a real Esc, no page; nothing in the menu pressed.
        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", StepTimeout);
        Note($"after the menu's Esc: the focus on {FocusNow(app)}");
        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitFor(
            () => app.ShownElements(BlamePageId).Count == 0 && app.ShownElements(K.Ids.BlameText).Count == 0 && app.ShownElements("GraphRows").Count == 1,
            "the blame page to close and the graph to show",
            StepTimeout);

        Assert.Equal(0, app.Close());
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    // ---- Part K's steps ----

    /// <summary>
    /// Waits, at most <see cref="StepTimeout"/>, until UI Automation's focused element is the stop of a walk
    /// (design 12.3.5): its id <paramref name="id"/>, and the row of <paramref name="path"/> in
    /// <paramref name="list"/>, for a row's own button by its name (the path) and its place in that list,
    /// for a row's Stage or Discard by its vertical centre lying within that row's <c>StageFileRow</c>. A
    /// row that holds the focus shows its buttons to UI Automation at opacity 0 (D425), so an id alone does
    /// not name the row. run.txt gets the focused element; a timeout names it and ends the method before any
    /// further key.
    /// </summary>
    private void WaitForWalkStop(RealApp app, string list, string path, string id, string key)
    {
        var seen = "nothing";
        try
        {
            app.WaitFor(() => IsWalkStop(app, list, path, id, out seen), $"{key}: {path}'s {id} in {list} UI Automation's focused element", StepTimeout);
        }
        catch (TimeoutException ex)
        {
            var message = $"{ex.Message} The focus is on {seen}. No further key is sent.";
            Note(message);
            throw new XunitException(message);
        }

        Note($"{key}: the focus on {path}'s {id}: {seen}");
    }

    /// <summary>One look for <see cref="WaitForWalkStop"/>; <paramref name="seen"/> describes the focused element.</summary>
    private static bool IsWalkStop(RealApp app, string list, string path, string id, out string seen)
    {
        seen = FocusNow(app);
        if (app.FocusedElement() is not { } focused)
        {
            return false;
        }

        string? focusedId;
        string? focusedName;
        Rectangle bounds;
        try
        {
            focusedId = focused.Properties.AutomationId.ValueOrDefault;
            focusedName = focused.Properties.Name.ValueOrDefault;
            bounds = focused.BoundingRectangle;
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.FlaUIException)
        {
            return false;
        }

        if (focusedId != id || bounds.Width <= 0 || bounds.Height <= 0 || (id == K.Ids.StageFileRow && focusedName != path))
        {
            return false;
        }

        if (StageRowIn(app, list, path) is not { } row)
        {
            return false;
        }

        var rowBounds = row.BoundingRectangle;
        var centre = bounds.Top + (bounds.Height / 2.0);
        return centre > rowBounds.Top && centre < rowBounds.Bottom;
    }

    /// <summary>
    /// After the text's wait on <c>src/Calculator.cs</c>'s diff: <c>DiffFileName</c> <c>Calculator.cs</c> and
    /// <paramref name="hunks"/> <c>StageHunkButton</c>s shown, at most <see cref="StepTimeout"/>; noted. UI
    /// Automation is only read here, so the focus does not move.
    /// </summary>
    private void WaitForCalculatorDiff(RealApp app, int hunks)
    {
        app.WaitFor(
            () => app.NamesOnScreen(K.Ids.DiffFileName).Contains(K.CalculatorName) && app.ShownElements(K.Ids.StageHunkButton).Count == hunks,
            string.Create(CultureInfo.InvariantCulture, $"the diff of {K.CalculatorName} with {hunks} hunk bars"),
            StepTimeout);
        Note(string.Create(CultureInfo.InvariantCulture, $"the diff of {K.CalculatorName} in {string.Join(" | ", app.NamesOnScreen(K.Ids.DiffFolder))}, {hunks} {K.Ids.StageHunkButton}s; the toolbar shows {string.Join(", ", ToolbarShown(app))}"));
    }

    /// <summary>
    /// K1's step 6: waits, at most <see cref="StepTimeout"/>, until UI Automation's focused element is a
    /// <c>StageHunkButton</c>, then asserts it is the first hunk's, the uppermost of those shown. Noted.
    /// Returns that button, which step 7's Enter is sent to only while it still has the focus.
    /// </summary>
    private AutomationElement WaitForFirstStageHunk(RealApp app)
    {
        app.WaitForFocusOn(K.Ids.StageHunkButton, StepTimeout);
        var focused = app.FocusedElement() ?? throw new XunitException("No element has the keyboard focus after Tab.");
        var first = app.ShownElements(K.Ids.StageHunkButton)[0];
        Note($"Tab: the focus on {FocusNow(app)}; the first hunk's {K.Ids.StageHunkButton} at {first.BoundingRectangle} named \"{first.Name}\"");
        Assert.True(focused.BoundingRectangle == first.BoundingRectangle, $"Tab reached a {K.Ids.StageHunkButton} at {focused.BoundingRectangle}, not the first hunk's at {first.BoundingRectangle}.");
        Assert.Equal(K.StageHunk, first.Name);
        return first;
    }

    /// <summary>
    /// K1's step 9 on the hunk that remains: a real Ctrl+Home, <c>K.DownsToLines</c> real Downs and
    /// <c>K.ShiftDownsForLines</c> real Shift+Downs. The caret is not drawn and the selection is not in UI
    /// Automation (the text's peer is a plain Document), so no condition shows where a key left the caret
    /// (design 12.3.5): after each key, besides UI Automation's input queue, a wait at most
    /// <see cref="StepTimeout"/> for what can be read, the focus still in the text and the toolbar's
    /// buttons as they must be then: the file's before the first Shift+Down, <c>Stage lines</c> and
    /// <c>Discard lines</c> in their place from it on. The second Shift+Down changes nothing that can be
    /// read. The two rows are proven by git's lines after the Enter; run.txt says so.
    /// </summary>
    private void SelectTheTwoLines(RealApp app)
    {
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
        WaitForCaretKeyRead(app, "Ctrl+Home", lines: false);
        for (var down = 1; down <= K.DownsToLines; down++)
        {
            Keys(app, VirtualKeyShort.DOWN);
            WaitForCaretKeyRead(app, string.Create(CultureInfo.InvariantCulture, $"Down {down}"), lines: false);
        }

        for (var down = 1; down <= K.ShiftDownsForLines; down++)
        {
            Keys(app, VirtualKeyShort.SHIFT, VirtualKeyShort.DOWN);
            WaitForCaretKeyRead(app, string.Create(CultureInfo.InvariantCulture, $"Shift+Down {down}"), lines: true);
        }

        Note(string.Create(CultureInfo.InvariantCulture, $"Ctrl+Home, {K.DownsToLines} Downs, {K.ShiftDownsForLines} Shift+Downs: the toolbar shows {string.Join(", ", ToolbarShown(app))}; where each key left the caret is not in UI Automation, so each was waited on by the focus in {K.Ids.DiffText} and the toolbar alone, and git's lines after the Enter prove the two rows"));
        WaitForFocusInText(app, K.Ids.DiffText, StepTimeout);
    }

    /// <summary>
    /// After a caret key of <see cref="SelectTheTwoLines"/>: waits, at most <see cref="StepTimeout"/>, until
    /// the focus is still in <c>DiffText</c> and the toolbar shows the line buttons (<paramref name="lines"/>)
    /// or the file's. A timeout names both, before any further key.
    /// </summary>
    private static void WaitForCaretKeyRead(RealApp app, string key, bool lines)
    {
        var shown = "nothing read yet";
        var focus = "nothing read yet";
        try
        {
            app.WaitFor(
                () =>
                {
                    shown = string.Join(", ", ToolbarShown(app));
                    var inText = FocusInText(app, K.Ids.DiffText, out focus) is not null;
                    return inText && LinesButtonsShown(app) == lines;
                },
                $"after {key}: the focus still in {K.Ids.DiffText} and the toolbar showing {(lines ? $"{K.StageLines} and {K.DiscardLines} in place of the file's buttons" : "the file's buttons alone")}",
                StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new XunitException($"{ex.Message} The toolbar shows {shown}; UI Automation's focused element is {focus}. No further key is sent.");
        }
    }

    /// <summary>
    /// Whether the toolbar shows the line buttons (<c>Stage lines</c>, <c>Discard lines</c>, named so) and not
    /// the file buttons: true; the file buttons and not the line buttons: false; anything else: null.
    /// </summary>
    private static bool? LinesButtonsShown(RealApp app)
    {
        var lines = app.NamesOnScreen(K.Ids.StageLinesButton).SequenceEqual([K.StageLines]) && app.NamesOnScreen(K.Ids.DiscardLinesButton).SequenceEqual([K.DiscardLines]);
        var file = app.NamesOnScreen(K.Ids.StageFileButton).SequenceEqual([K.StageFile]) && app.NamesOnScreen(K.Ids.DiscardFileButton).SequenceEqual([K.DiscardFile]);
        return lines && !file && app.ShownElements(K.Ids.StageFileButton).Count == 0 && app.ShownElements(K.Ids.DiscardFileButton).Count == 0
            ? true
            : file && !lines && app.ShownElements(K.Ids.StageLinesButton).Count == 0 && app.ShownElements(K.Ids.DiscardLinesButton).Count == 0 ? false : null;
    }

    /// <summary>The diff toolbar's buttons shown, by id, for run.txt.</summary>
    private static IEnumerable<string> ToolbarShown(RealApp app) =>
        new[] { K.Ids.InlineDiffButton, K.Ids.SideBySideDiffButton, K.Ids.StageLinesButton, K.Ids.DiscardLinesButton, K.Ids.StageFileButton, K.Ids.DiscardFileButton, K.Ids.UnstageLinesButton, K.Ids.UnstageFileButton }
            .Where(id => app.ShownElements(id).Count > 0);

    /// <summary>
    /// After a real Shift+F10: the menu settled, headed <paramref name="header"/>, its first item
    /// <paramref name="highlighted"/> highlighted (the menu gives it the focus as it opens, D105), read with
    /// no key sent; run.txt gets the header, the items and how many of the log's lines say the menu opened
    /// by the keyboard. Returns the items' names.
    /// </summary>
    private IReadOnlyList<string> WaitForMenuByKeys(RealApp app, string repository, string header, string highlighted)
    {
        app.WaitForMenu(StepTimeout);
        app.WaitForText("ContextMenuHeader", header, StepTimeout);
        AutomationElement? Highlighted() => app.FindAll("ContextMenuItem").FirstOrDefault(RealApp.HasKeyboardFocus);
        app.WaitFor(() => Highlighted() is not null, "an item of the menu with the focus and the highlight", StepTimeout);
        var item = Highlighted()!;
        var items = app.MenuItemNames();
        var byKeyboard = app.CountLogLines(RealApp.MenuOpened(repository, header, "keyboard"));
        Note($"the menu \"{header}\": {string.Join(" | ", items)}; \"{item.Name}\" highlighted; the log's lines of it opened by keyboard: {byKeyboard}");
        Assert.True(item.Name == highlighted, $"The menu's highlighted item is \"{item.Name}\", not \"{highlighted}\": no key is sent to it (D444).");
        return items;
    }

    /// <summary>
    /// <c>git diff --cached --numstat</c> and <c>git diff --numstat</c> of <c>src/Calculator.cs</c> (without
    /// the optional locks, beside the running app), noted in run.txt with <paramref name="when"/>, then
    /// asserted to be <paramref name="staged"/> and <paramref name="unstaged"/>.
    /// </summary>
    private async Task AssertNumstatAsync(TempRepo repo, string when, string staged, string unstaged)
    {
        var cached = await NumstatAsync(repo, cached: true);
        var working = await NumstatAsync(repo, cached: false);
        Note($"{when}: git diff --cached --numstat prints \"{cached}\", git diff --numstat \"{working}\"");
        Assert.Equal(staged, cached);
        Assert.Equal(unstaged, working);
    }

    /// <summary>
    /// Waits until <c>git diff [--cached] --numstat -- src/Calculator.cs</c> prints <paramref name="expected"/>,
    /// read every <see cref="GitPollInterval"/>, at most <see cref="StepTimeout"/>: a write the app makes
    /// with no change in the window to wait on. Fails at once when the app has exited.
    /// </summary>
    private async Task WaitForNumstatAsync(RealApp app, TempRepo repo, bool cached, string expected, string when)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var seen = await NumstatAsync(repo, cached);
            if (seen == expected)
            {
                Note(string.Create(CultureInfo.InvariantCulture, $"{when}: git diff {(cached ? "--cached " : string.Empty)}--numstat printed \"{seen}\" {clock.Elapsed.TotalSeconds:0.0} s after the key"));
                return;
            }

            if (app.HasExited)
            {
                throw new InvalidOperationException($"The app exited while waiting for git diff --numstat to print \"{expected}\".");
            }

            if (clock.Elapsed >= StepTimeout)
            {
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"{when}: git diff {(cached ? "--cached " : string.Empty)}--numstat did not print \"{expected}\" within {StepTimeout.TotalSeconds:0} s; it printed \"{seen}\"."));
            }

            await Task.Delay(GitPollInterval, TestContext.Current.CancellationToken);
        }
    }

    /// <summary><c>git diff [--cached] --numstat -- src/Calculator.cs</c>, its two tabs written as single spaces, without the optional locks (the app's git runs beside it).</summary>
    private static async Task<string> NumstatAsync(TempRepo repo, bool cached)
    {
        string[] arguments = cached
            ? ["--no-optional-locks", "diff", "--cached", "--numstat", "--", K.Calculator]
            : ["--no-optional-locks", "diff", "--numstat", "--", K.Calculator];
        return (await repo.GitAsync(arguments)).StandardOutput.Trim().Replace('\t', ' ');
    }

    /// <summary>Two exact states (<see cref="TempRepo.ExactStateAsync"/>) differ, and only in lines that name <paramref name="path"/>: its index entry and its status line (K1's end).</summary>
    private static void AssertChangedOnlyIn(string before, string after, string path)
    {
        var old = before.Split('\n');
        var now = after.Split('\n');
        Assert.Equal(old.Length, now.Length);
        var changed = old.Zip(now).Where(pair => pair.First != pair.Second).ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, pair => Assert.True(
            pair.First.Contains(path, StringComparison.Ordinal) && pair.Second.Contains(path, StringComparison.Ordinal),
            $"A line changed that is not {path}'s: \"{pair.First}\" became \"{pair.Second}\"."));
    }
}
