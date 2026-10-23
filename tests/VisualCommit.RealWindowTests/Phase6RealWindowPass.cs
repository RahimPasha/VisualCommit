// What is where in phase 6's real-window pass (docs/test-reports/phase-6.md: "Run", "Conventions of
// every part" and each part's "The real window", the RW steps in the Notes of W1, W2, W4, W9, K10,
// S14, S15 and L34; docs/phase-6-design.md 11.8, 17.2 to 17.6 and section 18's WP-R):
// - Phase6RealWindowPass.cs (this file): the run (Phase6Run: one folder of pictures and one run.txt
//   for the whole pass), the order of its methods (Phase6Order, 17.6's), launching the real app on
//   a scenario or on the workspace "Web" (and again on the same data folder for a restart), waiting
//   for a read pass of the dashboard, comparing a capture with the scripted picture of the same step,
//   and what the parts share (menus, the toolbar, the dashboard's header, rows and status bar, the
//   files the app writes, git's state read back).
// - Phase6RealWindowPass.W.cs (W1 part A, W2, W4, W9 with its real restart), .S.cs (S14, S15),
//   .K.cs (K10's first method) and .L.cs (L34, the real window's own check): the parts' real-window
//   steps, in the same partial class. Part G has no real-window step (its report says why).
// - RealApp.Phase6.cs (the files a launch starts from, the per-launch git environment, the tab strip
//   with its dashboards, UI Automation's patterns, 11.8's read-only reads of the desktop),
//   RealApp.Phase6.Dashboard.cs (the rows and cells), .Settings.cs (the screen, its sections, the Pick
//   dialog), .Palette.cs (a real Ctrl+Shift+P, the rows), with the earlier phases' RealApp files
//   (menus, tooltips, log lines, the native folder dialog; Windows' Open dialog, ChooseFileInDialog,
//   in RealApp.cs).
// - The expectations of the scripted walk-through (VisualTests.Phase6*/Phase6Expectations*.cs) are
//   compiled into this project too (see the .csproj), so both harnesses use the same constants and
//   texts.
//
// Conventions for the part files (phase 5's, with phase 6's report):
// - One test method per launch, named after the scripted method whose RW steps it runs (L34 has no
//   scripted twin); a check with a real restart (W9) is one method with two launches on one data
//   folder. Every launch is a fresh copy of the check's scenario on a new data folder at 1100×700;
//   the report's RW steps are all at that size (1920×1080 does not fit the development machine's
//   screen at 200 percent: those steps are scripted only).
// - Declare the scenario (and a server) first, then the app: `using var app = Launch(...)`. When a
//   step fails, the app is disposed first, which stops it, and then the repository goes. No step may
//   leave a window on the desktop.
// - Pictures: Compare(app, "<name>") with the scripted picture's name (the report's Screenshots
//   column, "real window"); S15's three have names of their own and the twin of the owner's app mode.
//   Compare rests the pointer at the neutral point first unless a menu is open or the capture is
//   about a tooltip (keepPointer: true), and asserts at most 3% differing pixels (D33, D58). The
//   scripted pictures must exist: run `dotnet test` at the repo root before the pass.
// - After an input, wait for the condition the step names (a log line, a text, a row's cell), each
//   with one of the pass's limits; never for a fixed time. Never click "Copy …", "Open file", "Open
//   in editor" or "Show in folder", never start a program, never change a setting of the desktop
//   (its theme, high contrast, scaling: D224); act only in the app's window and its own native
//   dialogs.
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase6;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 6's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase6Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase6Expectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// Runs phase 6's real-window methods in design 17.6's order: W1, W2, W4, W9, K10, S14, S15, L34
/// (eight methods, nine launches). A method the list does not name runs after them, by name.
/// </summary>
public sealed class Phase6Order : ITestCaseOrderer
{
    /// <summary>The methods in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(Phase6RealWindowPass.Check_W1_workspace_from_the_welcome_page),
        nameof(Phase6RealWindowPass.Check_W2_ten_repositories),
        nameof(Phase6RealWindowPass.Check_W4_fetch_all_progress),
        nameof(Phase6RealWindowPass.Check_W9_restart),
        "Check_K10_palette_search_and_run",
        nameof(Phase6RealWindowPass.Check_S14_settings_screen_sections_and_entries),
        nameof(Phase6RealWindowPass.Check_S15_system_theme_at_a_real_start),
        "Check_L34_light_overlays_against_their_twins",
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return [.. testCases.OrderBy(test => Rank(test.TestMethodName)).ThenBy(test => test.TestMethodName, StringComparer.Ordinal)];
    }

    private static int Rank(string? method) => method is not null && Methods.Contains(method) ? Methods.ToList().IndexOf(method) : int.MaxValue;
}

/// <summary>
/// The real-window pass of phase 6: the steps of docs/test-reports/phase-6.md marked "RW", repeated
/// in the real app on the Windows desktop with the real mouse and keyboard. Screenshots go to
/// artifacts/visual/phase-6/real-window/ and are compared with the scripted walk-through's of the
/// same step (D33, D58), which must have run first (<c>dotnet test</c> at the repo root). The parts'
/// checks are in Phase6RealWindowPass.W.cs, .K.cs, .S.cs and .L.cs.
/// </summary>
[TestCaseOrderer(typeof(Phase6Order))]
public partial class Phase6RealWindowPass(Phase6Run fixture) : IClassFixture<Phase6Run>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown.</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation or a read pass may take to end (its log line): the dashboard's first pass alone is some 72 git processes, four at a time (the report's OperationChainTimeout, design 17.6).</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a change of the window that follows an input may take to show: the scripted run's default wait.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a network step may take (the report's NetworkTimeout): a held fetch's percentage, a released run's end.</summary>
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    private RealWindowRun Run => fixture.Run;

    /// <summary>The working tree's folder name: the tab's name and the repository's in the log.</summary>
    private static string NameOf(TempRepo repo) => Path.GetFileName(repo.Path);

    // ---- Launches ----

    /// <summary>
    /// Starts the real app on a new data folder with one tab on <paramref name="repo"/> (and an empty
    /// second tab when <paramref name="emptySecondTab"/>), its <c>settings.json</c> written as
    /// <paramref name="settingsJson"/> and its global git file seeded with
    /// <paramref name="globalGitConfig"/> when given (17.2), in a 1100×700 client area, and waits until
    /// the repository has opened (<see cref="Opened"/>).
    /// </summary>
    private RealApp Launch(TempRepo repo, string? settingsJson = null, string? globalGitConfig = null, bool emptySecondTab = false)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var data = Run.NewDataDirectory();
        if (settingsJson is not null)
        {
            File.WriteAllText(new AppPaths(data).SettingsFile, settingsJson);
        }

        if (globalGitConfig is not null)
        {
            RealApp.SeedGlobalGitConfig(data, globalGitConfig);
        }

        TabState[] tabs = emptySecondTab ? [new TabState(repo.Path), new TabState(null)] : [new TabState(repo.Path)];
        var app = Sized(RealApp.Launch(data, new SessionState { Tabs = tabs, ActiveTab = 0 }));
        return Opened(app, NameOf(repo), loadsBefore: 0, readsBefore: 0);
    }

    /// <summary>
    /// The start on the workspace "Web" (part W's Defaults, <c>OpenWorkspaceAsync</c>'s twin): writes
    /// <c>workspaces.json</c> with <paramref name="copy"/>'s "Web" (id <c>web</c>, its ten folders in
    /// the dashboard's order) and <c>session.json</c> with one tab, its dashboard, active, on a new data
    /// folder; starts the app at 1100×700, dark; waits for the dashboard's first read pass of the ten
    /// (<see cref="WaitForPass"/>) and rests the pointer at the neutral point.
    /// </summary>
    private RealApp LaunchOnWorkspace(TempWorkspace copy)
    {
        ArgumentNullException.ThrowIfNull(copy);
        var data = Run.NewDataDirectory();
        var workspace = copy.ToWorkspace();
        RealApp.WriteWorkspaces(data, new WorkspaceList { Workspaces = [workspace] });
        var app = Sized(RealApp.Launch(data, new SessionState { Tabs = [new TabState(null, workspace.Id)], ActiveTab = 0 }));
        try
        {
            WaitForPass(app, mark: 0, PartW.Names.Length);
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Sizes the window to 1100×700 and notes the display once. Disposes the app when that fails.</summary>
    private RealApp Sized(RealApp app)
    {
        try
        {
            app.SetClientSize(DefaultSize.Width, DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Waits until <paramref name="repository"/> has opened: its history loaded and its status shown,
    /// the log lines the app writes for both, after the <paramref name="loadsBefore"/> and
    /// <paramref name="readsBefore"/> the log held before the launch. Disposes the app when that fails.
    /// </summary>
    private static RealApp Opened(RealApp app, string repository, int loadsBefore, int readsBefore)
    {
        try
        {
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

    /// <summary>A read pass's line of the workspace "Web" (14.14), any count: <c>Web: read {n} repositories in {ms} ms</c>.</summary>
    private static Regex AnyPass { get; } = new(@"\] Web: read \d+ repositor(y|ies) in \d+ ms", RegexOptions.CultureInvariant);

    /// <summary>
    /// Waits for a read pass of the dashboard of "Web" over <paramref name="repositories"/> rows since
    /// <paramref name="mark"/> (its line, <c>Web: read {n} repositories in</c>), then until no row
    /// reads: the real window's <c>WaitForDashboardReadAsync</c>. The scripted run asserts that it is
    /// the one pass (WD27). In a real window, Windows' own folder dialog takes the activation and gives
    /// it back when it closes, which may start design 2.10's trigger 1 for rows read more than 10 s
    /// before: so another pass is no failure here, and run.txt names every pass since the mark when
    /// there was more than one.
    /// </summary>
    private void WaitForPass(RealApp app, int mark, int repositories)
    {
        var line = AppLogFile.Containing(PartW.ReadPassStart(PartW.Workspace, repositories));
        app.WaitFor(() => app.CountLogLinesSince(line, mark) > 0, $"the read pass's line \"{PartW.ReadPassStart(PartW.Workspace, repositories)}…\"", OperationTimeout);
        app.WaitForNoRowReading(OperationTimeout);
        var passes = app.LogLinesSince(mark).Where(candidate => AnyPass.IsMatch(candidate)).ToList();
        if (passes.Count != 1)
        {
            Run.Note($"{passes.Count} read passes since the step's start (one of {repositories} expected; design 2.10's trigger 1 after a native dialog): {string.Join(" | ", passes.Select(pass => pass[(pass.IndexOf("] Web: ", StringComparison.Ordinal) + 2)..]))}");
        }
    }

    /// <summary>Notes in run.txt when the folder server could not have port 47393: its URLs then show another port, which no picture of the pass shows (design 17.2, D97).</summary>
    private void NotePort(AuthRemoteServer server)
    {
        if (server.PortWasTaken)
        {
            Run.Note($"Port {AuthRemoteServer.Phase6VisualChecksPort} was taken: the folder server ran on {server.Port} (D97).");
        }
    }

    // ---- Pictures ----

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

    // ---- Menus (phase 3's conventions, phase 6's "Menus") ----

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
    /// items' labels in order, each disabled item with its reason as its help text, every other item
    /// enabled; the height the report gives (±2); and that the menu lies at least 4 px inside the
    /// window. Returns where the menu is.
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

    // ---- The toolbar, the status bar and the dashboard's header (part W's TD and TR) ----

    /// <summary>A toolbar button by its id: waits until it is enabled or not and, when <paramref name="helpText"/> is given, its help text (its tooltip) reads it.</summary>
    private static void AssertButton(RealApp app, string id, bool enabled, string? helpText) =>
        app.WaitForButtonState(id, enabled, helpText, StepTimeout);

    /// <summary>
    /// The toolbar on a dashboard tab (4.10, part W's TD and TR): Fetch and Pull enabled with the
    /// workspace's texts and keys, or disabled with <paramref name="reason"/>; Push, Branch, Stash,
    /// Pop, Undo, Redo and Search disabled; Settings and Theme enabled with their texts.
    /// </summary>
    private static void AssertDashboardToolbar(RealApp app, string workspace, string? reason = null)
    {
        AssertButton(app, "FetchButton", reason is null, reason ?? PartW.FetchAllToolTip(workspace));
        AssertButton(app, "PullButton", reason is null, reason ?? PartW.PullAllToolTip(workspace));
        foreach (var id in PartW.DisabledWithoutToolTip)
        {
            AssertButton(app, id, false, helpText: null);
        }

        AssertButton(app, "SettingsButton", true, PartW.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartW.ThemeToLight);
    }

    /// <summary>
    /// The dashboard's header (2.5, 4.9) as UI Automation reads it: the title and the count; the
    /// run's text (or none), its bar's value (or no bar) and <c>Cancel all</c> shown or not; Add
    /// repository… enabled; Fetch all and Pull all enabled with their texts or disabled with
    /// <paramref name="fetchPullReason"/>; Refresh enabled with its text; More. Waits for it.
    /// </summary>
    private static void AssertDashboardHeader(RealApp app, string workspace, string count, string? runText = null, int? progress = null, bool cancelAll = false, string? fetchPullReason = null)
    {
        WaitTelling(
            app,
            () => app.DashboardHeaderText("DashboardTitle") == workspace
                && app.DashboardHeaderText("DashboardCount") == count
                && app.DashboardHeaderText("DashboardBulkText") == runText
                && (progress is null ? app.ShownElements("DashboardBulkProgress").Count == 0 : (int?)Math.Round(app.DashboardBulkPercent() ?? -1) == progress)
                && app.ShownElements("DashboardCancelAll").Count == (cancelAll ? 1 : 0),
            $"the header \"{workspace}\", \"{count}\", run \"{runText}\", bar {progress?.ToString(CultureInfo.InvariantCulture) ?? "none"}, Cancel all {(cancelAll ? "shown" : "hidden")}",
            () => $"It reads \"{app.DashboardHeaderText("DashboardTitle")}\", \"{app.DashboardHeaderText("DashboardCount")}\", run \"{app.DashboardHeaderText("DashboardBulkText")}\", bar {app.DashboardBulkPercent()?.ToString(CultureInfo.InvariantCulture) ?? "none"}, Cancel all {app.ShownElements("DashboardCancelAll").Count}.");
        Assert.True(app.Find("DashboardAddRepository").IsEnabled, "The header's Add repository… is disabled.");
        AssertButton(app, "DashboardFetchAll", fetchPullReason is null, fetchPullReason ?? PartW.FetchAllToolTip(workspace));
        AssertButton(app, "DashboardPullAll", fetchPullReason is null, fetchPullReason ?? PartW.PullAllToolTip(workspace));
        AssertButton(app, "DashboardRefresh", true, PartW.RefreshToolTip(workspace));
        var more = app.Find("DashboardMore");
        Assert.Equal((PartW.More, true), (more.Name, more.IsEnabled));
    }

    /// <summary>
    /// The status bar on a dashboard tab (2.8, 4.9): the workspace's name in the branch part, the
    /// operation text, the progress bar's value (or none), its Cancel shown or not, and the waiting
    /// count (or none). Waits for it.
    /// </summary>
    private static void AssertDashboardStatusBar(RealApp app, string workspace, string text, int? progress = null, bool cancel = false, string? waiting = null)
    {
        app.WaitForText("CurrentBranch", workspace, StepTimeout);
        WaitTelling(
            app,
            () => app.OperationStatusText() == text
                && (progress is null ? app.ShownElements("OperationProgress").Count == 0 : (int?)Math.Round(app.OperationProgressValue() ?? -1) == progress)
                && app.ShownElements("OperationCancelButton").Count == (cancel ? 1 : 0)
                && (waiting is null ? app.ShownElements("OperationWaiting").Count == 0 : app.NamesOnScreen("OperationWaiting").SequenceEqual([waiting])),
            $"the status bar \"{text}\", bar {progress?.ToString(CultureInfo.InvariantCulture) ?? "none"}, Cancel {(cancel ? "shown" : "hidden")}, waiting \"{waiting}\"",
            () => $"It reads \"{app.OperationStatusText()}\", bar {app.OperationProgressValue()?.ToString(CultureInfo.InvariantCulture) ?? "none"}, Cancel {app.ShownElements("OperationCancelButton").Count}, waiting \"{string.Join(" | ", app.NamesOnScreen("OperationWaiting"))}\".");
    }

    /// <summary>The status bar's Activity log toggle: enabled or not, with this help text (its tooltip).</summary>
    private static void AssertActivityToggle(RealApp app, bool enabled, string helpText) =>
        AssertButton(app, "ActivityLogToggle", enabled, helpText);

    /// <summary>TD (part W's conventions): the toolbar's and the header's Fetch and Pull enabled, the header idle with <paramref name="count"/> (or the last run's <paramref name="result"/>), the status bar with the workspace and <c>Ready</c> or the result.</summary>
    private static void AssertTd(RealApp app, string count, string? result = null)
    {
        AssertDashboardToolbar(app, PartW.Workspace);
        AssertDashboardHeader(app, PartW.Workspace, count, runText: result);
        AssertDashboardStatusBar(app, PartW.Workspace, result ?? PartW.Ready);
    }

    // ---- The dashboard's rows (part W's "A row is written") ----

    /// <summary>W0's rows by name (scenario facts 2.3), old-site's tooltip naming its saved folder.</summary>
    private static Dictionary<string, PartW.Row> W0Of(TempWorkspace copy) =>
        PartW.W0(copy.PathOf("old-site")).ToDictionary(row => row.Name, StringComparer.Ordinal);

    /// <summary>A cell as UI Automation names it: the arrows' automation name, else its text (empty for an empty cell).</summary>
    private static string Named(PartW.Cell cell) => cell.Name ?? cell.Text;

    /// <summary>
    /// Waits until the dashboard shows exactly <paramref name="expected"/>, top to bottom, as UI
    /// Automation reads them: each row's name, its Branch, Ahead / behind, Changes, State and
    /// Operation cells, its Cancel shown only for the rows in <paramref name="cancel"/>, and only
    /// <paramref name="selected"/> selected (none when null). The cells' tones and tooltips are the
    /// scripted run's (UI Automation gives neither).
    /// </summary>
    private static void AssertRows(RealApp app, IReadOnlyList<PartW.Row> expected, string? selected = null, IReadOnlyCollection<string>? cancel = null)
    {
        var want = expected.Select(row => string.Join(
            " · ",
            row.Name,
            row.Branch,
            Named(row.AheadBehind),
            Named(row.Changes),
            Named(row.State),
            Named(row.Operation)) + ((cancel?.Contains(row.Name) ?? false) ? " [Cancel]" : string.Empty) + (row.Name == selected ? " (selected)" : string.Empty)).ToList();
        List<string> Shown() => [.. app.DashboardRows().Select(row => string.Join(" · ", row.Name, row.Branch, row.AheadBehind, row.Changes, row.State, row.Operation) + (row.ShowsCancel ? " [Cancel]" : string.Empty) + (row.IsSelected ? " (selected)" : string.Empty))];
        try
        {
            app.WaitFor(() => Shown().SequenceEqual(want), "the dashboard's rows", StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message}\nExpected:\n{string.Join('\n', want)}\nShown:\n{string.Join('\n', Shown())}", ex);
        }
    }

    /// <summary>The column headings at 1100 (2.6), left to right, in the band below the header (y 124 to 152).</summary>
    private static void AssertHeadings(RealApp app) =>
        app.WaitFor(
            () => app.DashboardTextsBetween((float)PartW.HeaderBottom, (float)PartW.HeadingsBottom).SequenceEqual(PartW.Headings1100),
            $"the column headings {string.Join(", ", PartW.Headings1100)} (they are {string.Join(", ", app.DashboardTextsBetween((float)PartW.HeaderBottom, (float)PartW.HeadingsBottom))})",
            StepTimeout);

    /// <summary>No dialog of the dialog layer is open (phase 3's "Absence"), phase 6's included.</summary>
    private static void AssertNoDialog(RealApp app)
    {
        foreach (var dialog in new[] { "ConfirmationDialog", "ErrorDialog", "MessageDialog", "NameDialog", "ResetDialog", "AddRepositoriesDialog", RealApp.SettingsDialogId, RealApp.PickDialogId, RealApp.PaletteId })
        {
            Assert.True(app.ShownElements(dialog).Count == 0, $"A dialog ({dialog}) is open.");
        }
    }

    /// <summary>Waits for <paramref name="condition"/> as <see cref="RealApp.WaitFor"/> does, at most <paramref name="timeout"/> (<see cref="StepTimeout"/> when null); on a timeout the message adds what <paramref name="state"/> reads then.</summary>
    private static void WaitTelling(RealApp app, Func<bool> condition, string what, Func<string> state, TimeSpan? timeout = null)
    {
        try
        {
            app.WaitFor(condition, what, timeout ?? StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} {state()}", ex);
        }
    }

    /// <summary>Asserts that exactly one line of the log since <paramref name="mark"/> holds <paramref name="text"/>.</summary>
    private static void AssertOneLogLine(RealApp app, int mark, string text)
    {
        var lines = app.LogLinesSince(mark).Where(line => line.Contains(text, StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count == 1, $"The log has {lines.Count} lines with \"{text}\" since the mark, not one:\n" + string.Join('\n', lines));
    }

    // ---- The files the app writes ----

    /// <summary>Whether <paramref name="path"/> names the folder <paramref name="folder"/> (part W's <c>SameFolder</c>): <see cref="WorkspacePaths.Same"/>, which on Windows also expands short names.</summary>
    private static bool SameFolder(string folder, string path) => WorkspacePaths.Same(path, folder);

    /// <summary>One workspace of <c>workspaces.json</c> as the file holds it.</summary>
    private sealed record SavedWorkspace(string Id, string Name, IReadOnlyList<string> Paths);

    /// <summary>
    /// <c>workspaces.json</c> as the data folder holds it (<c>version</c> and each workspace's
    /// <c>id</c>, <c>name</c> and paths), or null while it is missing or being written.
    /// </summary>
    private static (int Version, IReadOnlyList<SavedWorkspace> Workspaces)? SavedWorkspaces(string data)
    {
        var file = new AppPaths(data).WorkspacesFile;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            IReadOnlyList<SavedWorkspace> workspaces = [.. root.GetProperty("workspaces").EnumerateArray().Select(workspace => new SavedWorkspace(
                workspace.GetProperty("id").GetString()!,
                workspace.GetProperty("name").GetString()!,
                [.. workspace.GetProperty("repositories").EnumerateArray().Select(repository => repository.GetProperty("path").GetString()!)]))];
            return (root.GetProperty("version").GetInt32(), workspaces);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Waits until <c>workspaces.json</c> holds version 1 and one workspace with <paramref name="id"/>
    /// (null: 32 lowercase hex digits, WD26) and <paramref name="name"/>, its paths naming
    /// <paramref name="folders"/> in order (<see cref="SameFolder"/>): the app writes it after the
    /// change. Returns the workspace's id.
    /// </summary>
    private static string WaitForWorkspacesFile(RealApp app, string data, string? id, string name, IReadOnlyList<string> folders)
    {
        bool Holds((int Version, IReadOnlyList<SavedWorkspace> Workspaces)? saved) =>
            saved is (1, [var workspace])
            && (id is null ? Regex.IsMatch(workspace.Id, "^[0-9a-f]{32}$") : workspace.Id == id)
            && workspace.Name == name
            && workspace.Paths.Count == folders.Count
            && workspace.Paths.Zip(folders).All(pair => SameFolder(pair.Second, pair.First));
        string Described() => SavedWorkspaces(data) is { } saved
            ? $"version {saved.Version}, " + string.Join("; ", saved.Workspaces.Select(workspace => $"{workspace.Id} \"{workspace.Name}\" [{string.Join(", ", workspace.Paths)}]"))
            : "no readable file";
        try
        {
            app.WaitFor(() => Holds(SavedWorkspaces(data)), $"workspaces.json to hold \"{name}\" with {folders.Count} folders", StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It holds: {Described()}.", ex);
        }

        return SavedWorkspaces(data)!.Value.Workspaces[0].Id;
    }

    /// <summary>
    /// The session as the data folder holds it, read as the app reads it (<c>JsonSessionStore</c>),
    /// or null while the app is writing it. The store reads a copy in a folder of its own: on a file it
    /// cannot read, the store moves the file aside, which must never happen to the session of the app
    /// that runs (and a restart reads). As phase 5's pass reads it.
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

    /// <summary>
    /// Waits until the session the app saved has the tabs <paramref name="tabs"/> (a repository's
    /// folder matched by <see cref="SameFolder"/>, a dashboard by its workspace's id) and the active
    /// tab <paramref name="activeTab"/>.
    /// </summary>
    private static void WaitForSession(RealApp app, string data, IReadOnlyList<TabState> tabs, int activeTab)
    {
        bool Holds(SessionState? session) =>
            session is not null
            && session.ActiveTab == activeTab
            && session.Tabs.Count == tabs.Count
            && session.Tabs.Zip(tabs).All(pair =>
                pair.First.WorkspaceId == pair.Second.WorkspaceId
                && (pair.Second.RepositoryPath is null ? pair.First.RepositoryPath is null : pair.First.RepositoryPath is { } saved && SameFolder(pair.Second.RepositoryPath, saved)));
        try
        {
            app.WaitFor(() => Holds(SavedSession(data)), $"session.json to hold {tabs.Count} tabs, the active {activeTab}", StepTimeout);
        }
        catch (TimeoutException ex)
        {
            var session = SavedSession(data);
            throw new TimeoutException($"{ex.Message} It holds: {(session is null ? "no readable file" : string.Join(", ", session.Tabs) + $", active {session.ActiveTab}")}.", ex);
        }
    }

    /// <summary>A property of <c>settings.json</c> as its JSON text (<c>"Dark"</c> with its quotes), or null without the file or the property.</summary>
    private static string? SavedSetting(string data, string name)
    {
        var file = new AppPaths(data).SettingsFile;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var json = JsonDocument.Parse(stream);
            return json.RootElement.TryGetProperty(name, out var value) ? value.GetRawText() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asserts that the launch's git calls used a global configuration of its own, in its own data
    /// folder (design 8.12, 8.13, 17.2): <c>&lt;data&gt;/harness/global.gitconfig</c>, whose folder the
    /// launch made, and no file of the machine's.
    /// </summary>
    private static void AssertOwnGitConfiguration(RealApp app)
    {
        var global = app.GlobalGitConfigFile;
        Assert.Equal(Path.Combine(app.DataDirectory, "harness", "global.gitconfig"), global);
        Assert.Equal(global, RealApp.GitEnvironmentOf(app.DataDirectory)["GIT_CONFIG_GLOBAL"]);
        Assert.True(Directory.Exists(Path.GetDirectoryName(global)), $"The launch's harness folder {Path.GetDirectoryName(global)} does not exist.");
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output as lines, without empty ones and line-end carriage returns.</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary>The exact state (<see cref="TempRepo.ExactStateAsync"/>) of each of the copy's nine repositories with a folder.</summary>
    private static async Task<Dictionary<string, string>> ExactStatesAsync(TempWorkspace copy)
    {
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            states[name] = await repo.ExactStateAsync();
        }

        return states;
    }

    /// <summary>Asserts that every repository's exact state is still <paramref name="before"/>'s: nothing ran in git.</summary>
    private static async Task AssertExactStatesAsync(TempWorkspace copy, IReadOnlyDictionary<string, string> before)
    {
        foreach (var (name, state) in before)
        {
            using var repo = copy.Repo(name);
            Assert.True(state == await repo.ExactStateAsync(), $"{name} changed.");
        }
    }

    /// <summary>Asserts each repository's refs (<c>git for-each-ref</c>) as <paramref name="refs"/> gives them.</summary>
    private static async Task AssertRefsAsync(TempWorkspace copy, IReadOnlyDictionary<string, string[]> refs)
    {
        foreach (var (name, expected) in refs)
        {
            using var repo = copy.Repo(name);
            Assert.Equal(expected, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        }
    }

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();

    [GeneratedRegex(@"^(?<label>.*) \(disabled: ""(?<reason>.*)""\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DisabledMark();

    /// <summary>A colour of the expectations' palettes as the screen capture gives colours.</summary>
    private static Color Drawn(Avalonia.Media.Color colour) => Color.FromArgb(colour.A, colour.R, colour.G, colour.B);
}
