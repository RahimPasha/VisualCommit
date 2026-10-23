// What is where in phase 5's real-window pass (docs/test-reports/phase-5.md: the "Real window"
// column of "Checks", "Conventions of every part" and each part's conventions; docs/phase-5-design.md
// 13.4, 15.2 and 16, package WP9):
// - Phase5RealWindowPass.cs (this file): the run (Phase5Run: one folder of pictures and one run.txt
//   for the whole pass), launching the real app on a scenario (and again on the same data folder
//   for a restart), waiting for an operation's log line and for the backups' clean-up, comparing a
//   capture with the scripted picture of the same step, and what the parts share (menus, the
//   toolbar's states, the details panel, the status bar, git's state read back).
// - Phase5RealWindowPass.A.cs (A1, A2, A3, A4), .B.cs (B5, B6, B7, B8, B9), .F.cs (F10, F12, F14),
//   .U.cs (U16), .P.cs (P24, P25): the parts' real-window steps, in the same partial class. F11,
//   F13, F15, U17 to U23, P26 and P27 are "n/a" in the report (their reasons are in its Real window
//   column).
// - RealApp.Phase5.cs (help texts, keys, the activity log, the search bar, the pages, the
//   comparison, the left panel's toggles), with phase 4's RealApp.Phase4*.cs and phase 3's
//   RealApp.Phase3.cs and RealApp.Checks.cs (menus, tooltips, rows below the banner, left-panel
//   rows, the close button).
// - The expectations of the scripted walk-through (VisualTests.Phase5/Phase5Expectations*.cs and
//   VisualTests.Phase5Undo/Phase5Expectations.U*.cs) are compiled into this project too (see the
//   .csproj), so both harnesses use the same constants and texts.
//
// Conventions for the part files (phase 4's, with phase 5's report):
// - One test method per launch, named after the check whose real-window steps it runs; a check
//   with a real restart (A3, F14) is one method with two launches on one data folder. Every launch
//   is a fresh copy of the check's scenario at 1100×700 in the dark theme: the report's RW steps
//   are all at that size (1920×1080 does not fit the development machine's screen: B5's method 2
//   and F10's part B are scripted only).
// - Declare the scenario first, then the app: `using var app = Launch(...)`. When a step fails, the
//   app is disposed first, which stops it and releases a mouse button a drag left held
//   (RealApp.Dispose), and then the repository goes. No step may leave a window on the desktop.
// - Pictures: Compare(app, "<name>") with the scripted picture's name (the report's Screenshots
//   column, "real window"); it rests the pointer at the neutral point first unless a menu is open or
//   the capture is about a tooltip (keepPointer: true), and asserts at most 3% differing pixels
//   (D33, D58). The scripted pictures must exist: run `dotnet test` at the repo root before the pass.
// - After an operation, wait for its log line (ExpectOperation before the input, then Wait); after
//   any other input, wait for the condition the step names, each with a limit; never for a fixed
//   time. Never click "Copy …", "Open file" or "Show in folder".
// - Graph rows: RowPoint (the row constants, below the banner when one shows, D122); left-panel
//   rows: RealApp.RefRow; page rows: RealApp.PageRowPoint (window x 420, BD1); blame lines:
//   RealApp.BlameGutterPoint and BlameTextPoint (from BlameGutter's and BlameText's bounds).
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase5;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 5's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase5Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase5Expectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The real-window pass of phase 5: the steps of docs/test-reports/phase-5.md marked "RW", repeated
/// in the real app on the Windows desktop with the real mouse and keyboard. Screenshots go to
/// artifacts/visual/phase-5/real-window/ and are compared with the scripted walk-through's of the
/// same step (D33, D58), which must have run first (<c>dotnet test</c> at the repo root). The parts'
/// checks are in Phase5RealWindowPass.A.cs to .P.cs.
/// </summary>
public partial class Phase5RealWindowPass(Phase5Run fixture) : IClassFixture<Phase5Run>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown.</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation may take to end (its log line): an undo, a merge and the clean-up run chains of git calls (the report's OperationChainTimeout).</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a change of the window that follows an input may take to show (a page's rows, a comparison, the log's rows): the scripted run's default wait, 30 s since <c>5bdd1de</c>.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The app's commits' date (D72): 2026-01-02 09:00 UTC.</summary>
    private static readonly DateTimeOffset AppCommitTime = GitIsolation.AppCommitDate;

    private RealWindowRun Run => fixture.Run;

    /// <summary>The working tree's folder name: the tab's name and the repository's in the log.</summary>
    private static string NameOf(TempRepo repo) => Path.GetFileName(repo.Path);

    /// <summary>
    /// Starts the real app on a new data folder with one tab on <paramref name="repo"/> (and, when
    /// given, the activity log's height in the session, A2's 320) in a 1100×700 client area, dark
    /// (the default theme), and waits until the repository has opened (<see cref="Opened"/>). The
    /// first launch of the pass notes the display in run.txt.
    /// </summary>
    private RealApp Launch(TempRepo repo, double? activityLogHeight = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var data = Run.NewDataDirectory();
        var app = RealApp.Launch(data, new SessionState { Tabs = [new TabState(repo.Path)], ActiveTab = 0, ActivityLogHeight = activityLogHeight });
        return Opened(app, NameOf(repo), loadsBefore: 0, readsBefore: 0);
    }

    /// <summary>
    /// Starts the real app again on <paramref name="data"/>, writing no session, so that the session
    /// the last instance saved opens its tabs (a real restart, A3 and F14), and waits until the
    /// repository <paramref name="repository"/> has opened in the new process: the data folder's log
    /// already holds the last instance's lines, so the new process's are counted after them.
    /// </summary>
    private RealApp Relaunch(string data, string repository)
    {
        var lines = AppLogFile.ReadLines(data);
        var loadsBefore = lines.Count(line => Loaded(repository).IsMatch(line));
        var readsBefore = lines.Count(line => AppLogFile.WorkingTreeRead(repository).IsMatch(line));
        return Opened(RealApp.Launch(data), repository, loadsBefore, readsBefore);
    }

    /// <summary>
    /// Sizes the window to 1100×700, notes the display once, and waits until <paramref name="repository"/>
    /// has opened: its history loaded and its status shown, the log lines the app writes for both,
    /// after the <paramref name="loadsBefore"/> and <paramref name="readsBefore"/> the log held
    /// before the launch. Disposes the app when that fails.
    /// </summary>
    private RealApp Opened(RealApp app, string repository, int loadsBefore, int readsBefore)
    {
        try
        {
            app.SetClientSize(Phase5Expectations.DefaultSize.Width, Phase5Expectations.DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            app.WaitForLogLine(Loaded(repository), LoadTimeout, loadsBefore + 1);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(repository), LoadTimeout, readsBefore + 1);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The line a repository logs when its graph has loaded: <c>&lt;repo&gt;: loaded N commits in M ms</c>.</summary>
    private static Regex Loaded(string repository) => AppLogFile.Containing($"] {repository}: loaded ");

    /// <summary>
    /// Waits for the backups' clean-up at opening on a scenario that holds backups due for it (the
    /// conventions of every part, 15.2): the queue's line
    /// <c>&lt;repo&gt;: Clean up backups: Succeeded after &lt;N&gt; ms: &lt;message&gt;</c>.
    /// </summary>
    private static void WaitForCleanUp(RealApp app, string repository, string message) =>
        app.WaitForLogLine(AppLogFile.OperationEnded(repository, "Clean up backups", OperationOutcome.Succeeded, message), OperationTimeout);

    /// <summary>
    /// Captures the client area, saves it as <c>&lt;name&gt;.png</c> and asserts that it matches the
    /// scripted picture of the same name (or <paramref name="scriptedName"/>) within
    /// <see cref="RealWindowRun.MaxDifference"/> (D33, D58); run.txt gets a line with the difference
    /// and <c>&lt;name&gt;-difference.png</c> shows it. The pointer moves to the neutral point first,
    /// as both harnesses do before a capture that is not about a hover, unless
    /// <paramref name="keepPointer"/> is true: a capture with a menu open or of a tooltip.
    /// </summary>
    private void Compare(RealApp app, string name, bool keepPointer = false, string? scriptedName = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!keepPointer)
        {
            app.MoveToNeutral();
        }

        using var capture = app.CaptureClient();
        Run.Save(capture, name);
        Run.AssertMatchesScripted(capture, name, scriptedName ?? name);
    }

    /// <summary>Notes in run.txt what UI Automation exposes of the activity log's rows in view at <paramref name="picture"/>: the pass's own record of names (A2).</summary>
    private void NoteLogRows(RealApp app, string picture) =>
        Run.Note($"{picture}: the activity log's rows in view, as UI Automation names them: {string.Join(" | ", app.ActivityLogRows().Where(row => row.InView).Select(row => row.Kind.Replace("ActivityLog", string.Empty, StringComparison.Ordinal) + " \"" + row.Name + "\""))}");

    /// <summary>
    /// Notes how many times the operation <paramref name="title"/> of <paramref name="repository"/>
    /// has ended so far, before the input that starts it; <see cref="PendingOperation.Wait"/> then
    /// waits for its next end line, which the queue writes after the operation's refresh: the real
    /// window's "wait until idle".
    /// </summary>
    private static PendingOperation ExpectOperation(RealApp app, string repository, string title)
    {
        ArgumentNullException.ThrowIfNull(app);
        var pattern = AppLogFile.OperationEnded(repository, title);
        return new PendingOperation(app, pattern, app.CountLogLines(pattern));
    }

    /// <summary>
    /// Notes how many graph loads of <paramref name="repository"/> the log holds (<c>loaded N
    /// commits</c>), before the input that reloads the graph; <see cref="PendingLoad.Wait"/> waits
    /// for the next and for the reload bar (<c>GraphReloading</c>) to be gone: part F's "wait until
    /// the graph has reloaded".
    /// </summary>
    private static PendingLoad ExpectGraphLoad(RealApp app, string repository)
    {
        var pattern = Loaded(repository);
        return new PendingLoad(app, pattern, app.CountLogLines(pattern));
    }

    /// <summary>The point of a graph row to click "in its Message column", from the row constants (D49), below the banner when one shows (D122).</summary>
    private static PointF RowPoint(RealApp app, int displayRow, int lanes) => app.GraphRowPoint(displayRow, lanes);

    // ---- Menus (phase 3's conventions, phase 5's "Menus") ----

    /// <summary>The labels of a menu as the report writes it: its items without the header, the separators and the marks after a label.</summary>
    private static List<string> ItemLabels(IEnumerable<string> lines) =>
        [.. lines.Skip(1).Where(line => line != "—").Select(line => MenuMark().Replace(line, string.Empty))];

    /// <summary>The disabled items of a menu as the report writes it, with their reasons (each item's tooltip).</summary>
    private static List<(string Label, string Reason)> DisabledItems(IEnumerable<string> lines) =>
        [.. lines.Skip(1)
            .Select(line => DisabledMark().Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups["label"].Value, match.Groups["reason"].Value))];

    /// <summary>
    /// Asserts the open menu as the report writes it (<paramref name="lines"/>, header first): its
    /// items' labels in order, each disabled item with its reason as its help text (its tooltip),
    /// every other item enabled; the height the report gives (±2, 4a); and that the menu lies at
    /// least 4 px inside the window. Returns where the menu is.
    /// </summary>
    private static RectangleF AssertMenu(RealApp app, IReadOnlyList<string> lines, double height)
    {
        var menu = app.BoundsOf(app.WaitForMenu());
        Assert.Equal(ItemLabels(lines), app.MenuItemNames());
        var disabled = DisabledItems(lines);
        foreach (var item in app.FindAll("ContextMenuItem"))
        {
            var reason = disabled.Where(entry => entry.Label == item.Name).Select(entry => entry.Reason).FirstOrDefault();
            if (reason is null)
            {
                Assert.True(item.IsEnabled, $"The menu item \"{item.Name}\" is disabled.");
            }
            else
            {
                Assert.False(item.IsEnabled, $"The menu item \"{item.Name}\" is enabled; the report says it is disabled with \"{reason}\".");
                Assert.Equal(reason, RealApp.HelpTextOf(item));
            }
        }

        Assert.True(
            Math.Abs(menu.Height - height) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Height} high; the report says {height} (±{WindowLayout.MenuHeightTolerance})."));
        var client = app.ClientSize;
        var inset = (float)WindowLayout.MenuEdgeInset - 0.5f;
        Assert.True(
            menu.Left >= inset && menu.Top >= inset && menu.Right <= client.Width - inset && menu.Bottom <= client.Height - inset,
            $"The menu at {menu} is not 4 px inside the {client.Width}x{client.Height} window.");
        return menu;
    }

    /// <summary>The open menu's item <paramref name="label"/> alone has the keyboard focus: its highlight (D105).</summary>
    private static void AssertFocusedItem(RealApp app, string label) =>
        app.WaitFor(
            () => app.FindAll("ContextMenuItem").Where(RealApp.HasKeyboardFocus).Select(item => item.Name).SequenceEqual([label]),
            $"\"{label}\" alone to have the keyboard focus",
            RealApp.DefaultWaitTimeout);

    /// <summary>Esc closes the open menu; waits until no menu is in the window.</summary>
    private static void CloseMenuWithEscape(RealApp app)
    {
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", RealApp.DefaultWaitTimeout);
    }

    /// <summary>The tooltip <paramref name="tip"/> (its text <paramref name="text"/>) lies inside the client area (D105: tooltips are drawn on the window's overlay layer).</summary>
    private static void AssertToolTipInside(RealApp app, FlaUI.Core.AutomationElements.AutomationElement tip, string text)
    {
        var (bounds, inside) = app.ToolTipPlace(tip);
        var client = app.ClientSize;
        Assert.True(inside, $"The tooltip \"{text}\" at {bounds} is not inside the {client.Width}x{client.Height} client area.");
    }

    // ---- The toolbar and the status bar (part A's conventions) ----

    /// <summary>
    /// Asserts the toolbar's buttons as part A writes them (<see cref="Phase5Expectations.PartA.ButtonState"/>),
    /// as far as UI Automation tells them: each enabled or not, and its help text, which is its
    /// tooltip (empty where there is none). Phase 5 read the help texts of Undo, Redo and Search
    /// only; since phase 6 every toolbar button has one, an enabled button's with its key
    /// (docs/phase-6-design.md 6.8, 15.5; phase 6's report, "Changes to expected results", A1), as
    /// the scripted run's <c>AssertToolbar</c> asserts it. Waits for the states, which follow the
    /// queue's <c>Changed</c> on the UI thread.
    /// </summary>
    private static void AssertToolbar(RealApp app, IEnumerable<Phase5Expectations.PartA.ButtonState> states)
    {
        foreach (var state in states)
        {
            app.WaitForButtonState(state.Id, state.Enabled, state.ToolTip ?? string.Empty);
        }
    }

    /// <summary>Waits until Undo and Redo are as given: enabled or not, with these help texts (their tooltips).</summary>
    private static void AssertUndoRedo(RealApp app, bool undoEnabled, string undoText, bool redoEnabled, string redoText)
    {
        app.WaitForButtonState("UndoButton", undoEnabled, undoText);
        app.WaitForButtonState("RedoButton", redoEnabled, redoText);
    }

    /// <summary>Waits until the status bar's Activity log toggle is enabled or not with the help text of an open (<paramref name="open"/>) or closed panel.</summary>
    private static void AssertToggle(RealApp app, bool enabled, bool open) =>
        app.WaitForButtonState(
            "ActivityLogToggle",
            enabled,
            open ? Phase5Expectations.PartA.HideActivityLog : Phase5Expectations.PartA.ShowActivityLog);

    /// <summary>Waits until the status bar's operation text is <paramref name="text"/> (it follows the queue's log line on the UI thread).</summary>
    private static void WaitForStatus(RealApp app, string text)
    {
        try
        {
            app.WaitFor(() => app.OperationStatusText() == text, $"the status bar to say \"{text}\"", RealApp.DefaultWaitTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It says \"{app.OperationStatusText()}\".", ex);
        }
    }

    /// <summary>Waits until the status bar's branch text (<c>CurrentBranch</c>) is <paramref name="text"/>.</summary>
    private static void WaitForBranch(RealApp app, string text) => app.WaitForText("CurrentBranch", text);

    // ---- The details panel and the stage panel ----

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static void WaitForDetails(RealApp app, string subject) =>
        app.WaitFor(() => app.FindAll("DetailsSubject").Any(element => element.Name == subject), $"the details of \"{subject}\"", StepTimeout);

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/> whose SHA is <paramref name="sha"/> (<c>DetailsSha</c>).</summary>
    private static void WaitForDetails(RealApp app, string subject, string sha) =>
        app.WaitFor(
            () => app.FindAll("DetailsSubject").Any(element => element.Name == subject) && app.FindAll("DetailsSha").Any(element => element.Name == sha),
            $"the details of \"{subject}\" ({sha})",
            StepTimeout);

    /// <summary>Waits until the details panel shows its placeholder: nothing selected.</summary>
    private static void WaitForNothingSelected(RealApp app) =>
        app.WaitForText("DetailsPlaceholder", Phase5Expectations.PartA.NothingSelected, StepTimeout);

    /// <summary>
    /// Clicks the working-changes row (display row 0, below the banner while one shows) and waits
    /// for the stage panel with these titles.
    /// </summary>
    private static void ClickWorkingRow(RealApp app, int lanes, string unstagedTitle, string stagedTitle)
    {
        var working = RowPoint(app, 0, lanes);
        app.ClickAt(working.X, working.Y);
        app.WaitForStageTitles(unstagedTitle, stagedTitle, StepTimeout);
    }

    /// <summary>
    /// Moves the real mouse over a file's row in a stage list, so that its buttons show, and clicks
    /// one of them (<c>RowStageButton</c>, <c>RowDiscardButton</c>), as phase 2's pass does.
    /// </summary>
    private static void ClickFileRowButton(RealApp app, string list, string path, string buttonId)
    {
        var row = app.BoundsOf(app.StageFileRow(list, path, StepTimeout));
        app.MoveMouseTo(row.X + 40, row.Y + (row.Height / 2));
        app.WaitFor(() => app.ShownElements(buttonId).Count == 1, $"the {buttonId} of {path}", StepTimeout);
        app.ClickWithMouse(app.ShownElements(buttonId)[0]);
    }

    /// <summary>
    /// Types <paramref name="text"/> into the text box <paramref name="automationId"/> with the real
    /// keyboard, key by key, after a real click into it, and waits until the box holds it (its
    /// Value pattern).
    /// </summary>
    private static void TypeInto(RealApp app, string automationId, string text)
    {
        app.ClickWithMouse(app.Find(automationId));
        app.TypeText(text);
        app.WaitFor(() => BoxText(app, automationId) == text, $"{automationId} to hold \"{text}\"", RealApp.DefaultWaitTimeout);
    }

    /// <summary>A text box's text, read through its Value pattern.</summary>
    private static string BoxText(RealApp app, string automationId) => app.Find(automationId).AsTextBox().Text;

    /// <summary>No dialog is open: no element of the app's dialogs is on screen (phase 3's "Absence").</summary>
    private static void AssertNoDialog(RealApp app)
    {
        foreach (var dialog in new[] { "ConfirmationDialog", "ErrorDialog", "MessageDialog", "NameDialog", "ResetDialog" })
        {
            Assert.True(app.ShownElements(dialog).Count == 0, $"A dialog ({dialog}) is open.");
        }
    }

    /// <summary>
    /// Waits for the error dialog with <paramref name="title"/> and asserts what UI Automation reads
    /// of it: its summary, its command (when given), its extra button (or none), and its output
    /// (<c>ErrorOutput</c>, a text box read through its Value pattern) holding
    /// <paramref name="keyLine"/> and, on Git 2.36 (the report's git), exactly <paramref name="lines"/>.
    /// </summary>
    private static async Task AssertErrorDialogAsync(RealApp app, string title, string summary, string? command, string? extraButton, string? keyLine, IReadOnlyList<string>? lines)
    {
        app.WaitForText("ErrorTitle", title, StepTimeout);
        Assert.Equal([summary], app.NamesOnScreen("ErrorSummary"));
        if (command is not null)
        {
            Assert.Equal([command], app.NamesOnScreen("ErrorCommand"));
        }

        Assert.Equal(extraButton is null ? [] : [extraButton], app.NamesOnScreen("ErrorExtraButton"));
        if (extraButton is not null)
        {
            Assert.True(app.ShownElements("ErrorExtraButton")[0].IsEnabled, $"\"{extraButton}\" is disabled.");
        }

        if (keyLine is null)
        {
            Assert.Empty(app.ShownElements("ErrorOutput"));
            return;
        }

        var output = app.Find("ErrorOutput").AsTextBox().Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Contains(keyLine, output);
        if (lines is not null && await Phase5Expectations.IsReportGitAsync())
        {
            Assert.Equal(lines, output);
        }
    }

    /// <summary>Closes the error dialog with its Close (<c>ConfirmButton</c>) and waits until it has gone.</summary>
    private static void CloseErrorDialog(RealApp app)
    {
        app.ClickWithMouse(app.Find("ConfirmButton"));
        app.WaitUntilGone("ErrorDialog", StepTimeout);
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output as lines, without empty ones and line-end carriage returns.</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary>Plain git's output, trimmed of its last line ends.</summary>
    private static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.TrimEnd('\r', '\n');

    /// <summary>What a revision names (<c>git rev-parse --verify --quiet</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>The newest <paramref name="count"/> subjects of a reflog (<c>git log --walk-reflogs --format=%gs</c>), newest first.</summary>
    private static async Task<List<string>> ReflogAsync(TempRepo repo, string reference, int count) =>
        await GitLinesAsync(repo, "log", "--walk-reflogs", $"-{count.ToString(CultureInfo.InvariantCulture)}", "--format=%gs", reference);

    /// <summary>Every ref with what it points at (<c>git for-each-ref</c>), the backups included.</summary>
    private static Task<List<string>> RefsAsync(TempRepo repo) => GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");

    /// <summary>A file of the working tree as git wrote it, line ends as line feeds.</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// The working tree's folder as git names it (<c>rev-parse --show-toplevel</c>), full and
    /// without a trailing separator: what the app saves in the session (the scripted run's
    /// <c>WorkingTreeAsync</c>).
    /// </summary>
    private static async Task<string> WorkingTreeAsync(TempRepo repo) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(await GitTextAsync(repo, "rev-parse", "--show-toplevel")));

    /// <summary>
    /// The session as the data folder holds it (<see cref="SavedSession"/>), waited for while the
    /// app is writing it: a condition polled every 100 ms, at most <see cref="StepTimeout"/>.
    /// </summary>
    private static SessionState ReadSession(string data)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (SavedSession(data) is { } session)
            {
                return session;
            }

            if (clock.Elapsed >= StepTimeout)
            {
                throw new TimeoutException($"The session file in {data} could not be read within {StepTimeout.TotalSeconds} s.");
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// The session as the data folder holds it, read as the app reads it (<c>JsonSessionStore</c>),
    /// or null while the app is writing it. The store reads a copy in a folder of its own: on a
    /// file it cannot read, the store moves the file aside, which must never happen to the session
    /// of the app that runs (and a restart reads).
    /// </summary>
    private static SessionState? SavedSession(string data)
    {
        var folder = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "session-" + Guid.NewGuid().ToString("N")[..12]);
        try
        {
            string json;
            using (var stream = new FileStream(new AppPaths(data).SessionFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }

            // Half a file, caught while the app swaps in its new one, is no session.
            using (JsonDocument.Parse(json))
            {
            }

            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, "session.json");
            File.WriteAllText(copy, json);
            return new JsonSessionStore(copy).Current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left behind in the temp folder.
            }
        }
    }

    /// <summary>Asserts a commit as git stores it: its parents (in order), author and committer (Test Author), author and commit dates as instants.</summary>
    private static async Task AssertCommitAsync(TempRepo repo, string sha, DateTimeOffset authorDate, DateTimeOffset commitDate, params string[] parents)
    {
        var fields = (await GitLinesAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", sha)).Single().Split('\x1f');
        Assert.Equal(parents, fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        Assert.Equal(authorDate, GitIsoDates.Parse(fields[3]));
        Assert.Equal(commitDate, GitIsoDates.Parse(fields[4]));
    }

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();

    [GeneratedRegex(@"^(?<label>.*) \(disabled: ""(?<reason>.*)""\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DisabledMark();

    /// <summary>An operation a step is about to start, and how many times it had ended before (<see cref="ExpectOperation"/>).</summary>
    private sealed record PendingOperation(RealApp App, Regex Pattern, int EndedBefore)
    {
        /// <summary>
        /// Waits for the operation's next end line and asserts its outcome and, when given, its
        /// message (the status bar's text, design 12.2). Returns the line.
        /// </summary>
        public string Wait(OperationOutcome outcome, string? message = null, TimeSpan? timeout = null)
        {
            var line = App.WaitForLogLine(Pattern, timeout ?? OperationTimeout, EndedBefore + 1);
            Assert.Contains($": {outcome} after ", line, StringComparison.Ordinal);
            if (message is not null)
            {
                Assert.EndsWith(": " + message, line, StringComparison.Ordinal);
            }

            return line;
        }
    }

    /// <summary>A graph load a step is about to start, and how many the log held before (<see cref="ExpectGraphLoad"/>).</summary>
    private sealed record PendingLoad(RealApp App, Regex Pattern, int LoadsBefore)
    {
        /// <summary>Waits for the next <c>loaded N commits</c> line and for the reload bar to be gone. Returns the line.</summary>
        public string Wait()
        {
            var line = App.WaitForLogLine(Pattern, LoadTimeout, LoadsBefore + 1);
            App.WaitUntilGone("GraphReloading", StepTimeout);
            return line;
        }
    }
}
