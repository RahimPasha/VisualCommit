// What is where in phase 3's scripted walk-through (docs/test-reports/phase-3.md):
// - Phase3Checks.Support.cs (this file): the base class Phase3Checks with what every check uses:
//   the default start on a scenario, the server scenarios, saving pictures, and the assertions
//   the checks repeat (menus, dialogs, the status bar, "no menu", "no dialog", clipped text).
// - Phase3Checks.A.cs to .D.cs: the checks of the report's parts A to D, in the classes
//   Phase3ChecksA to Phase3ChecksD (each derives from Phase3Checks and is partial: a part may add
//   files Phase3Checks.A.<topic>.cs of its own). One class per part, because xunit runs the tests
//   of one class one after another and different classes side by side: the four parts then share
//   the time of the walk-through (design 7.7). Only part D starts servers, so its checks, which
//   all want port 47391, never run at the same time.
// - The project: these files are VisualCommit.VisualTests.Phase3, apart from phases 0 to 2's
//   walk-through (VisualCommit.VisualTests). Every [AvaloniaFact] of a test assembly runs on that
//   assembly's one headless UI thread, one at a time, whatever its class; a project of its own is
//   a test process with a UI thread of its own, which "dotnet test" runs beside the other.
// - Phase3Expectations.Common.cs and Phase3Expectations.A.cs to .D.cs: what the checks compare
//   against, also compiled by the real-window pass.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase3.cs (design 7.4's list:
//   modifier clicks, drags, menus, dialogs, typing, the queue), ShellDriver.Pointer.cs (neutral
//   point, tooltips), ShellDriver.Log.cs (log lines, git calls, step times), ShellDriver.Checks.cs
//   (opening, rows, the banner, left-panel rows, replacing text, the error dialog, progress,
//   closing). WindowLayout and AppLogFile in tests/VisualCommit.Testing are shared with RealApp.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase3RealWindowPass*.cs.
//
// Conventions for the part files:
// - A test method is named Check_<NN>[<suffix>]_<what>, with one method per app start (the
//   report's "One test per app start": each [AvaloniaFact] gets a fresh application), for example
//   Check_01_commit_menu, Check_01_detached_head_menu (step 6, edges), Check_01_merge_commit_menu
//   (step 7, graph). A check whose later start goes on from an earlier one's state (14b after 14)
//   repeats the steps it needs without saving their pictures again (Shot is only for the pictures
//   the check's own steps name).
// - Pictures are saved only through Shot(app, "<check><step>-<what>"), with exactly the names of
//   the report's Screenshots column ("01a-commit-menu"); the real-window pass saves its picture of
//   the same step under the same name. Shot moves the pointer to the neutral point first unless a
//   menu is open, a drag runs, or the capture is about a hover (keepPointer: true), and waits
//   until the details panel has read the selected commit's details (ShellDriver.WaitForCommitDetails).
// - Every wait is on a condition (ShellDriver.WaitForAsync, WaitForIdleAsync, WaitForLogLineAsync,
//   WaitForToolTipAsync, WaitForDialogAsync), never a fixed time; a change made outside the app is
//   waited for with OutsideChangeTimeout.
// - Rows are clicked with ShellDriver.GraphRowPoint / WorkingRowPoint (from the graph control's
//   bounds, right below a banner too, D122); left-panel rows with RefRowAsync(section, …, name);
//   file rows with FileRowAsync(list, path). A position the real-window pass needs that only the
//   scripted run can measure (a label's centre, for check 7's drag) goes into the part's
//   expectations as a constant that the scripted check asserts (±1) and the pass uses.
// - Menus are asserted with AssertMenu (the report's notation), AssertHighlighted, AssertMenuShape
//   and AssertMenuAtPointer (AssertMenuPlaced where phase 4's and phase 5's taller menus no
//   longer fit below the pointer: every branch, tag, remote, section-header and drop menu);
//   "no menu" with AssertNoMenuAsync(app, app.LogMark() read before the input); the error
//   dialog with AssertErrorDialogAsync (git's lines word for word only on Git 2.36); the
//   in-progress banner with AssertBanner (its text, phase 4's Continue and Skip, Abort, and
//   whether the buttons stand under the text); the status bar with AssertStatus and AssertBranch.
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>
/// The scripted walk-through of phase 3: what every check of docs/test-reports/phase-3.md shares.
/// The checks themselves are in <c>Phase3ChecksA</c> to <c>Phase3ChecksD</c>, one class per part of
/// the report. Each check performs its steps on the whole app in headless mode, saves the pictures
/// its Steps name under artifacts/visual/phase-3/scripted/, and asserts the expected results.
/// </summary>
public abstract partial class Phase3Checks
{
    /// <summary>The phase whose report these checks are and whose picture folder they fill.</summary>
    protected const int Phase = Phase3Expectations.Phase;

    /// <summary>
    /// How long a wait for a change made outside the app (through <see cref="TempRepo"/>, seen by
    /// the app's file watcher) may take: 30 seconds (part D's "Waiting"; a hot laptop has missed
    /// 5 before). The expected result is the state after the wait, not how long it took.
    /// </summary>
    protected static readonly TimeSpan OutsideChangeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a network operation against the test server may take before a check gives up:
    /// the git calls go through a real HTTP server and git's own helper processes.
    /// </summary>
    protected static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>
    /// The checks' default start (each part's "Unless a check says otherwise"): writes a session
    /// with one tab on <paramref name="repo"/> (a fresh copy of the named scenario), starts the app
    /// at the given size in the given theme, waits until the graph has drawn its rows, the working
    /// tree's status is shown and the operation queue is idle
    /// (<see cref="ShellDriver.WaitForRepositoryAsync"/>), and rests the pointer at the neutral
    /// point. Dispose the driver at the end of the check (<c>using var app = …</c>).
    /// </summary>
    protected static async Task<ShellDriver> OpenAsync(
        TempDirectory data,
        TempRepo repo,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(repo);
        if (theme == AppTheme.Light)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });
        }

        var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height);
        try
        {
            await app.WaitForRepositoryAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The auth scenario at the credential test server (part D, "The server"): the server on port
    /// 47391 (another when that one is taken: <see cref="ServerScenario.Port"/>), asking for
    /// alice / s3cret unless <paramref name="requireAuth"/> is false, and a fresh
    /// <see cref="Scenarios.AuthRemoteAsync"/> copy whose <c>origin</c> it serves. Dispose it at
    /// the end of the check, after the app (<c>await using var scenario = …</c>, declared first).
    /// </summary>
    protected static async Task<ServerScenario> AuthAtServerAsync(bool requireAuth = true)
    {
        var server = await AuthRemoteServer.StartForVisualChecksAsync();
        try
        {
            server.RequireAuth = requireAuth;
            var repo = await Scenarios.AuthRemoteAsync(server);
            return new ServerScenario(server, repo);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// The sync scenario with <c>origin</c> at the server (part D's "Sync at the server", checks 18
    /// to 20 and 22): a fresh <see cref="Scenarios.RemoteSyncAsync"/> copy, the server serving its
    /// own <c>origin.git</c> in place without asking for a sign-in (design 7.3), and <c>origin</c>'s
    /// URL set to <c>http://127.0.0.1:47391/origin.git</c>. <see cref="Scenarios.PushFromElsewhereAsync"/>
    /// still pushes into what the server serves.
    /// </summary>
    protected static async Task<ServerScenario> SyncAtServerAsync()
    {
        var repo = await Scenarios.RemoteSyncAsync();
        AuthRemoteServer? server = null;
        try
        {
            server = await AuthRemoteServer.StartForVisualChecksAsync(existingRepository: repo.SiblingPath("origin.git"));
            server.RequireAuth = false;
            await repo.GitAsync("remote", "set-url", "origin", server.Url);
            return new ServerScenario(server, repo);
        }
        catch
        {
            repo.Dispose();
            if (server is not null)
            {
                await server.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>
    /// Saves a picture of the window as artifacts/visual/phase-3/scripted/<paramref name="name"/>.png
    /// (the first picture of a run empties the folder, <see cref="VisualRun.Save"/>) and returns it
    /// for pixel assertions. The name is the report's, "&lt;check&gt;&lt;step&gt;-&lt;what&gt;".
    /// Before the capture the pointer moves to the neutral point, as both harnesses do before a
    /// capture that is not about a hover, unless <paramref name="keepPointer"/> is true (a hover or
    /// tooltip capture), a menu is open (the pointer stays where it opened it) or a drag runs (the
    /// pointer stays where the drag is). Before that it waits until the details panel is not
    /// reading a commit's details (<see cref="ShellDriver.WaitForCommitDetails"/>): an input that
    /// selected a row (a right-click that opens a menu, too) is pictured with that row's details,
    /// never with the commit selected before. The step's time is noted for design 7.7's budget
    /// (artifacts/visual/phase-3/timings/).
    /// </summary>
    protected static Screenshot Shot(ShellDriver app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!PictureName().IsMatch(name))
        {
            throw new ArgumentException($"\"{name}\" is not a picture name of the report: <check><step>-<what>, such as 01a-commit-menu.", nameof(name));
        }

        app.WaitForCommitDetails();
        if (!keepPointer && !app.IsMenuOpen && !IsDragging(app))
        {
            app.MoveToNeutral();
        }

        var screenshot = app.Capture();
        screenshot.Save(Phase, name);
        NoteTime(app.MarkStep(name));
        return screenshot;
    }

    /// <summary>
    /// Asserts a menu as the report writes it: the header, then each item's label in order, "—"
    /// for a separator, " (danger)" after an enabled destructive item and
    /// <c> (disabled: "reason")</c> after a disabled one (<see cref="MenuSnapshot.Lines"/>).
    /// </summary>
    protected static void AssertMenu(MenuSnapshot menu, params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var actual = menu.Lines();
        Assert.True(
            actual.SequenceEqual(lines),
            "The menu is not the expected one.\nExpected:\n  " + string.Join("\n  ", lines) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>Asserts which item has the highlight (D105: the first enabled one when the menu opens), or none when <paramref name="label"/> is null.</summary>
    protected static void AssertHighlighted(MenuSnapshot menu, string? label)
    {
        ArgumentNullException.ThrowIfNull(menu);
        Assert.Equal(label, menu.Highlighted?.Label);
    }

    /// <summary>
    /// Asserts a menu's height by 4a's formula, 8 + 24 + 26 × items + 9 × separators (±2), and
    /// that it lies at least 4 px inside the window.
    /// </summary>
    protected static void AssertMenuShape(ShellDriver app, MenuSnapshot menu)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(menu);
        var items = menu.Items.Count;
        var separators = menu.Entries.Count - items;
        var expected = Phase3Expectations.MenuHeight(items, separators);
        Assert.True(
            Math.Abs(menu.Bounds.Height - expected) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; 4a gives {expected} for {items} items and {separators} separators."));
        var window = app.Window.ClientSize;
        var inset = WindowLayout.MenuEdgeInset - 0.5;
        Assert.True(
            menu.Bounds.Left >= inset && menu.Bounds.Top >= inset && menu.Bounds.Right <= window.Width - inset && menu.Bounds.Bottom <= window.Height - inset,
            $"The menu at {menu.Bounds} is not 4 px inside the {window.Width}x{window.Height} window.");
    }

    /// <summary>Asserts that a menu opened by the pointer at <paramref name="pointer"/> has its top-left 2 right of and 2 below it (±2, 4a).</summary>
    protected static void AssertMenuAtPointer(MenuSnapshot menu, Point pointer)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var expected = pointer + new Point(WindowLayout.MenuPointerGap, WindowLayout.MenuPointerGap);
        Assert.True(
            Math.Abs(menu.Bounds.X - expected.X) <= 2 && Math.Abs(menu.Bounds.Y - expected.Y) <= 2,
            $"The menu's top-left is {menu.Bounds.TopLeft}, expected {expected} (the pointer + (2, 2)).");
    }

    /// <summary>
    /// Asserts where a menu opened by the pointer at <paramref name="pointer"/> is, by 4a's placement
    /// rule ("The menu", Placement), for a step that gives no position of its own: its top-left 2
    /// right of and 2 below the pointer (<see cref="AssertMenuAtPointer"/>) when it fits there;
    /// flipped to the left of the pointer (its right edge 2 left of it) when it would cross the
    /// window's right edge, above it when it would cross the bottom; then kept 4 inside the window
    /// (±2). Phase 4's items, and phase 5's Compare, Hide and Solo, make some of phase 3's menus
    /// too high to fit below the pointer (phase 4's report: "the position only where its expected
    /// result states it").
    /// </summary>
    protected static void AssertMenuPlaced(ShellDriver app, MenuSnapshot menu, Point pointer)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(menu);
        var window = app.Window.ClientSize;
        var size = menu.Bounds.Size;
        var gap = WindowLayout.MenuPointerGap;
        var inset = WindowLayout.MenuEdgeInset;
        var x = pointer.X + gap + size.Width > window.Width ? pointer.X - gap - size.Width : pointer.X + gap;
        var y = pointer.Y + gap + size.Height > window.Height ? pointer.Y - gap - size.Height : pointer.Y + gap;
        x = Math.Max(Math.Min(x, window.Width - inset - size.Width), inset);
        y = Math.Max(Math.Min(y, window.Height - inset - size.Height), inset);
        Assert.True(
            Math.Abs(menu.Bounds.X - x) <= 2 && Math.Abs(menu.Bounds.Y - y) <= 2,
            $"The menu's top-left is {menu.Bounds.TopLeft}; 4a places a {size} menu opened at {pointer} at ({x}, {y}).");
    }

    /// <summary>
    /// "No menu opens" / "no menu is open", as the conventions assert it: once the input is settled
    /// and the tab is idle, the menu layer has no menu (<c>Menu.IsOpen</c> false, <c>Current</c>
    /// null), no <c>Context menu opened</c> line was logged since <paramref name="logMark"/>
    /// (<see cref="ShellDriver.LogMark"/>, read before the input; null to skip), and no menu is in
    /// the window.
    /// </summary>
    protected static async Task AssertNoMenuAsync(ShellDriver app, int? logMark)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Settle();
        if (app.Session.Shell.ActiveTab.Repository is not null)
        {
            await app.WaitForIdleAsync();
        }

        Assert.False(app.Session.Shell.Menu.IsOpen, $"A menu is open: {app.Session.Shell.Menu.Current?.HeaderText}.");
        Assert.Null(app.Session.Shell.Menu.Current);
        if (logMark is { } mark)
        {
            var opened = app.LogLinesSince(mark).Where(line => line.Contains(": Context menu opened: ", StringComparison.Ordinal)).ToList();
            Assert.True(opened.Count == 0, "A menu was opened since the input:\n" + string.Join('\n', opened));
        }

        Assert.Equal(0, app.MenuCount);
    }

    /// <summary>
    /// "No dialog", as the conventions assert it: once the app is idle after the input (the
    /// operation's refresh included), the dialog layer has nothing open.
    /// </summary>
    protected static async Task AssertNoDialogAsync(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Session.Shell.ActiveTab.Repository is not null)
        {
            await app.WaitForIdleAsync();
        }

        var current = app.Session.Services.DialogHost.Current;
        Assert.True(current is null, $"A dialog is open: {current?.GetType().Name} \"{current?.Title}\".");
        Assert.Null(app.TopDialogView);
    }

    /// <summary>
    /// Asserts the status bar's operation text (<c>OperationStatus</c>) and its tone (4e: secondary,
    /// the warning colour for a stop with conflicts, the danger colour for "&lt;Title&gt; failed"),
    /// and that the text is drawn in the tone's colour of the active theme.
    /// </summary>
    protected static void AssertStatus(ShellDriver app, string text, OperationStatusTone tone = OperationStatusTone.Secondary)
    {
        ArgumentNullException.ThrowIfNull(app);
        var status = app.Session.Shell.ActiveTab.OperationStatus;
        Assert.Equal(text, status.Text);
        Assert.Equal(tone, status.Tone);
        var block = (TextBlock)app.FindByAutomationId("OperationStatus");
        Assert.Equal(text, block.Text);
        var palette = PaletteOf(app);
        var colour = tone switch
        {
            OperationStatusTone.Warning => palette.Warning,
            OperationStatusTone.Danger => palette.Danger,
            _ => palette.TextSecondary,
        };
        Assert.Equal(colour, (block.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>Asserts the status bar's branch text (<c>CurrentBranch</c>): a branch's name or "Detached at &lt;sha&gt;".</summary>
    protected static void AssertBranch(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(text, ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);
    }

    /// <summary>
    /// Asserts the error dialog on top (design 1.6, 4f) as the conventions' "Git's own texts" say:
    /// always the app's own parts (title, summary, "In &lt;repo&gt;", the command, the buttons);
    /// for a dialog with git's output, that <c>ErrorOutput</c> is exactly
    /// <c>GitOutputCleaner.Clean</c> of the failed call's own standard error and output as the
    /// call log recorded them; on every git, without the app's code, that it is what git printed
    /// (<see cref="AssertGitOutputShown"/>, with the classifying key line, which every dialog of
    /// git's output names); then, on Git 2.36, the report's quoted lines word for word. Returns
    /// what the dialog shows.
    /// </summary>
    protected static async Task<ErrorDialogSnapshot> AssertErrorDialogAsync(ShellDriver app, ExpectedErrorDialog expected)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(expected);
        var dialog = app.ErrorDialog();
        Assert.Equal(expected.Title, dialog.Title);
        Assert.Equal(expected.Summary, dialog.Summary);
        Assert.Equal("In " + expected.Repository, dialog.Repository);
        Assert.Equal(expected.Command, dialog.Command);
        Assert.Equal(expected.Buttons, dialog.Buttons);
        if (expected.Command is null)
        {
            Assert.Null(dialog.Output);
            return dialog;
        }

        Assert.NotNull(dialog.Output);
        if (!expected.OutputFromGit)
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
            return dialog;
        }

        var key = expected.KeyLine
            ?? throw new Xunit.Sdk.XunitException("A dialog of git's output needs the line that classifies the failure (ExpectedErrorDialog.KeyLine): on another git than 2.36 it is what proves the output.");
        var call = app.LastCall(expected.Command);
        Assert.False(call.Succeeded, $"The call \"{call.CommandText}\" succeeded; the dialog shows a failure.");
        Assert.Equal(ShellDriver.CleanedOutputOf(call), dialog.Output);
        AssertGitOutputShown(call, key, dialog.Output);
        if (await Phase3Expectations.IsReportGitAsync())
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
        }

        return dialog;
    }

    /// <summary>
    /// What the error dialog shows of a failed call (<paramref name="shown"/>, its
    /// <c>ErrorOutput</c>), held against what git printed as the call log recorded it, by rules
    /// of the test's own and not the app's cleaner (the conventions' "Git's own texts", on any
    /// git): <paramref name="keyLine"/>, the line that classifies the failure, is a line git
    /// printed (to standard error, or to standard output where git writes its failure there, as
    /// an empty revert does) and a line of the dialog; every line of the dialog is a line git
    /// printed, so nothing is made up or cut; every line of standard error that starts with
    /// <c>fatal:</c>, <c>error:</c> or <c> ! [</c> is shown, so nothing that says what failed is
    /// dropped; and no line shows git's progress count (<c>45% (9/20)</c>) or a carriage return.
    /// </summary>
    internal static void AssertGitOutputShown(GitCallRecord call, string keyLine, string shown)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNullOrEmpty(keyLine);
        ArgumentNullException.ThrowIfNull(shown);
        var error = PrintedLines(call.StandardError);
        var printed = error.Concat(PrintedLines(call.StandardOutput)).ToHashSet(StringComparer.Ordinal);
        var lines = shown.Split('\n');
        Assert.True(printed.Contains(keyLine), $"git did not print the key line \"{keyLine}\". It printed:\n{call.StandardError}\n{call.StandardOutput}");
        Assert.True(lines.Contains(keyLine, StringComparer.Ordinal), $"The dialog does not show the key line \"{keyLine}\". It shows:\n{shown}");
        foreach (var line in lines)
        {
            Assert.True(printed.Contains(line), $"The dialog shows a line git did not print: \"{line}\".");
        }

        foreach (var line in error.Where(line => line.StartsWith("fatal:", StringComparison.Ordinal) || line.StartsWith("error:", StringComparison.Ordinal) || line.StartsWith(" ! [", StringComparison.Ordinal)))
        {
            Assert.True(lines.Contains(line, StringComparer.Ordinal), $"The dialog leaves out git's line \"{line}\". It shows:\n{shown}");
        }

        Assert.DoesNotContain('\r', shown);
        Assert.DoesNotMatch(GitProgressCount(), shown);
    }

    /// <summary>The lines of git's raw output: split at every line feed and carriage return (git ends a progress update with one), without trailing spaces and empty lines.</summary>
    private static List<string> PrintedLines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>The count of a progress line of git or of the other side, as in <c>Receiving objects:  45% (9/20)</c>.</summary>
    [GeneratedRegex(@"\d{1,3}% \(\d+/\d+\)")]
    private static partial Regex GitProgressCount();

    /// <summary>
    /// Asserts the confirmation on top (phase 2's dialog, D69, with phase 3's texts): its title
    /// (<c>ConfirmTitle</c>), question (<c>ConfirmQuestion</c>), detail (<c>ConfirmDetail</c>,
    /// hidden when <paramref name="detail"/> is empty), "Cancel" with the keyboard focus, and the
    /// confirm button's label, in the danger style when <paramref name="destructive"/>. Waits for
    /// the dialog first.
    /// </summary>
    protected static async Task AssertConfirmationAsync(ShellDriver app, string title, string question, string detail, string confirmText, bool destructive = true)
    {
        ArgumentNullException.ThrowIfNull(app);
        var dialog = await app.WaitForDialogAsync<ConfirmationViewModel>();
        var view = app.TopDialogView ?? throw new InvalidOperationException("The confirmation has no view in the window.");
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        var detailBlock = (TextBlock)app.FindByAutomationId(view, "ConfirmDetail");
        Assert.Equal(detail.Length > 0, detailBlock.IsEffectivelyVisible);
        if (detail.Length > 0)
        {
            Assert.Equal(detail, detailBlock.Text);
        }

        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        Assert.Equal("Cancel", cancel.Content);
        await app.WaitForAsync(() => cancel.IsFocused, "the confirmation's Cancel to have the keyboard focus");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(confirmText, confirm.Content);
        Assert.Equal(destructive, confirm.Classes.Contains("danger"));
        Assert.Equal(destructive, dialog.IsDestructive);
    }

    /// <summary>
    /// Asserts the in-progress banner (<c>ConflictBanner</c>, design section 5, D122, with phase
    /// 4's buttons: docs/phase-4-design.md 5.1 and 5.2, phase 4's report "Phase 3's banner"): its
    /// text (<c>BannerText</c>); its Continue and Skip buttons (<c>BannerContinueButton</c>,
    /// <c>BannerSkipButton</c>, labelled "Continue" and "Skip"), each shown as
    /// <paramref name="continueButton"/> and <paramref name="skipButton"/> say (null: not shown),
    /// enabled or disabled with its reason as its tooltip; its Abort button's label
    /// (<c>BannerAbortButton</c>; null for a banner without one); the buttons left to right, 6
    /// apart, the last 8 from the banner's right edge, beside the text or, when
    /// <paramref name="buttonsBelow"/> is true, on a row of their own under it (5.2's layout; null:
    /// the check states neither); and a height of at least 36, exactly 36 when
    /// <paramref name="oneLine"/> is true (null: the check states no line count). Returns where the
    /// banner is.
    /// </summary>
    protected static Rect AssertBanner(
        ShellDriver app,
        string text,
        string? abortText,
        bool? oneLine = null,
        BannerButton? continueButton = null,
        BannerButton? skipButton = null,
        bool? buttonsBelow = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Settle();
        var bounds = app.BannerBounds ?? throw new Xunit.Sdk.XunitException("No banner is shown.");
        var textBlock = (TextBlock)app.FindByAutomationId("BannerText");
        Assert.Equal(text, textBlock.Text);
        AssertBannerButton(app.BannerContinueButton, "Continue", continueButton);
        AssertBannerButton(app.BannerSkipButton, "Skip", skipButton);
        var abort = (Button)app.FindByAutomationId("BannerAbortButton");
        Assert.Equal(abortText is not null, abort.IsEffectivelyVisible);
        if (abortText is not null)
        {
            Assert.Equal(abortText, abort.Content);
        }

        var shown = new[] { app.BannerContinueButton, app.BannerSkipButton, abort }.Where(button => button.IsEffectivelyVisible).Select(app.BoundsOf).ToList();
        for (var i = 1; i < shown.Count; i++)
        {
            Assert.True(
                Math.Abs(shown[i].Left - shown[i - 1].Right - 6) < 0.5 && Math.Abs(shown[i].Top - shown[i - 1].Top) < 0.5,
                $"The banner's buttons are not in a row 6 apart: {string.Join(", ", shown)}.");
        }

        if (shown.Count > 0)
        {
            Assert.True(Math.Abs(bounds.Right - 8 - shown[^1].Right) < 0.5, $"The banner's last button ends at {shown[^1].Right}, not 8 from the banner's right edge {bounds.Right}.");
            if (buttonsBelow is { } below)
            {
                var layout = textBlock.GetVisualAncestors().OfType<VisualCommit.App.Views.Shell.BannerLayoutPanel>().First();
                Assert.True(below == layout.ButtonsBelow, below ? "The banner's buttons stand beside its text, not under it." : "The banner's buttons stand under its text, not beside it.");
                var textBounds = app.BoundsOf(textBlock);
                Assert.True(
                    below ? shown[0].Top >= textBounds.Bottom - 0.5 : shown[0].Left >= textBounds.Right - 0.5,
                    $"The buttons at {shown[0]} are not {(below ? "under" : "beside")} the text at {textBounds}.");
            }
        }

        Assert.True(bounds.Height >= WindowLayout.BannerMinHeight - 0.5, $"The banner is {bounds.Height} high, less than 36 (D122).");
        if (oneLine is { } single)
        {
            Assert.True(single == Math.Abs(bounds.Height - WindowLayout.BannerMinHeight) < 0.5, $"The banner is {bounds.Height} high: {(single ? "not one line" : "one line")}.");
        }

        return bounds;
    }

    /// <summary>One of the banner's Continue and Skip as a check expects it (null: not shown): its label, enabled or not, and its reason as its tooltip, shown while disabled.</summary>
    private static void AssertBannerButton(Button button, string label, BannerButton? expected)
    {
        Assert.True((expected is not null) == button.IsEffectivelyVisible, $"The banner's {label} is {(button.IsEffectivelyVisible ? "shown" : "not shown")}.");
        if (expected is null)
        {
            return;
        }

        Assert.Equal(label, button.Content);
        Assert.True(expected.IsEnabled == button.IsEffectivelyEnabled, $"The banner's {label} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        Assert.Equal(expected.DisabledReason, ToolTip.GetTip(button) as string);
        Assert.True(ToolTip.GetShowOnDisabled(button), $"The banner's {label} does not show its tooltip while disabled.");
    }

    /// <summary>
    /// The banner's Continue or Skip as a check expects it (docs/phase-4-design.md 5.1): enabled
    /// (<see cref="Enabled"/>), or disabled with its reason (10.4) as its tooltip.
    /// </summary>
    protected sealed record BannerButton(string? DisabledReason)
    {
        /// <summary>Shown and enabled, with no tooltip.</summary>
        public static BannerButton Enabled { get; } = new((string?)null);

        /// <summary>Whether the button is enabled.</summary>
        public bool IsEnabled => DisabledReason is null;

        /// <summary>Shown and disabled, with <paramref name="reason"/> as its tooltip.</summary>
        public static BannerButton Disabled(string reason) => new(reason);
    }

    /// <summary>Asserts that no text in the window is cut off, except shortened text that shows the whole in its tooltip (phase 2's rule).</summary>
    protected static void AssertNoClippedText(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var problems = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(problems.Count == 0, "Clipped text:\n" + string.Join("\n", problems));
    }

    /// <summary>Asserts a control's tooltip text (<c>ToolTip.GetTip</c>) without picturing it, for reasons the steps do not rest the pointer on.</summary>
    protected static void AssertTip(Control control, string? expected)
    {
        ArgumentNullException.ThrowIfNull(control);
        Assert.Equal(expected, ToolTip.GetTip(control) as string);
    }

    /// <summary>"Neither 's3cret' nor 'wrong' is in any log line or call record" (checks 21, 21b, 22).</summary>
    protected static void AssertNotWritten(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        var found = app.WhereWritten(text);
        Assert.True(found.Count == 0, $"\"{text}\" was written down:\n" + string.Join('\n', found));
    }

    /// <summary>The colour at a window position of a picture, against the expected one.</summary>
    protected static void AssertColour(Color expected, Screenshot screenshot, Point position, string what)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        var actual = screenshot.PixelAt(position);
        Assert.True(actual == expected, $"The {what} at {position} is {actual}, expected {expected}.");
    }

    /// <summary>The date the harness gives the app's commits and ref updates (D72): 2026-01-02 09:00 UTC.</summary>
    protected static readonly DateTimeOffset AppCommitTime = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The scenario's clock time of a commit made at 12:<paramref name="minute"/> on 2026-01-01 UTC.</summary>
    protected static DateTimeOffset ScenarioTime(int minute) => new(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

    /// <summary>
    /// Asserts that a date git printed in its strict ISO form (<c>%aI</c>, <c>%cI</c>) is the
    /// instant <paramref name="expected"/> in the zone <paramref name="expected"/> carries. Git 2.36
    /// writes UTC as <c>+00:00</c> and git 2.55 as <c>Z</c>, so the text is never compared: the
    /// instant and the offset are (<see cref="GitIsoDates"/>).
    /// </summary>
    protected static void AssertGitDate(DateTimeOffset expected, string gitText)
    {
        ArgumentNullException.ThrowIfNull(gitText);
        var actual = GitIsoDates.Parse(gitText);
        Assert.Equal((expected, expected.Offset), (actual, actual.Offset));
    }

    /// <summary>
    /// The working tree's folder as git names it (<c>rev-parse --show-toplevel</c>), which is the
    /// folder the app shows and passes on: the physical one (macOS: <c>/private/var/…</c> for a
    /// temporary folder made as <c>/var/…</c>), in the platform's form, full and without a trailing
    /// separator. Compare a path the app reports with a path built from this, never from
    /// <c>repo.Path</c>.
    /// </summary>
    protected static async Task<string> WorkingTreeAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var topLevel = (await repo.GitAsync("rev-parse", "--show-toplevel")).StandardOutput.TrimEnd('\r', '\n');
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
    }

    /// <summary>The graph's rows (the control <c>GraphRows</c>).</summary>
    protected static CommitGraphControl Graph(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Find<CommitGraphControl>("GraphRows");
    }

    /// <summary>A toolbar button by its automation id (<c>FetchButton</c>, <c>StashButton</c>, …).</summary>
    protected static ToolbarButton ToolbarButton(ShellDriver app, string automationId)
    {
        ArgumentNullException.ThrowIfNull(app);
        return (ToolbarButton)app.FindByAutomationId(automationId);
    }

    /// <summary>The palette of the theme the window shows.</summary>
    protected static Phase3Expectations.Palette PaletteOf(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? Phase3Expectations.Light : Phase3Expectations.Dark;
    }

    /// <summary>The visible texts inside a control, in reading order (top to bottom, then left to right).</summary>
    protected static List<string> TextsIn(Visual region) => VisibleTexts.In(region);

    /// <summary>Whether a branch drag runs in the window (4g): its pointer must stay where the drag is.</summary>
    private static bool IsDragging(ShellDriver app)
    {
        try
        {
            return app.BranchDrag.IsDragging;
        }
        catch (InvalidOperationException)
        {
            // No drag layer in the window (no repository open): no drag either.
            return false;
        }
    }

    /// <summary>Notes a step's time in artifacts/visual/phase-3/timings/&lt;test&gt;.txt; the first note of a test in a run starts its file afresh.</summary>
    private static void NoteTime(StepTime step)
    {
        var test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
        var file = Path.Combine(Path.GetDirectoryName(VisualRun.FolderFor(Phase))!, "timings", FileNameOf(test) + ".txt");
        lock (TimingGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            if (TimingsStarted.Add(test))
            {
                File.WriteAllText(file, test + "\n");
            }

            File.AppendAllText(file, step + "\n");
        }
    }

    private static string FileNameOf(string text) =>
        string.Concat(text.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_'));

    [GeneratedRegex(@"^\d{2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PictureName();
}

/// <summary>
/// A scenario repository with the credential test server it talks to (part D). Dispose it after
/// the app: it disposes the repository, then the server.
/// </summary>
public sealed class ServerScenario(AuthRemoteServer server, TempRepo repo) : IAsyncDisposable
{
    public AuthRemoteServer Server { get; } = server;

    public TempRepo Repo { get; } = repo;

    /// <summary>The server's port: 47391 unless it was taken (<see cref="AuthRemoteServer.PortWasTaken"/>), and then what the expected texts show in its place.</summary>
    public int Port => Server.Port;

    /// <summary><paramref name="text"/> with this server's port in place of 47391 (<see cref="Phase3Expectations.WithPort"/>).</summary>
    public string WithPort(string text) => Phase3Expectations.WithPort(text, Port);

    public async ValueTask DisposeAsync()
    {
        Repo.Dispose();
        await Server.DisposeAsync();
    }
}

/// <summary>
/// The error dialog a check expects (<see cref="Phase3Checks.AssertErrorDialogAsync"/>).
/// </summary>
/// <param name="Title">"Checkout blocked", "Push rejected", … (2.33).</param>
/// <param name="Summary">The summary, word for word.</param>
/// <param name="Repository">The repository's name: the dialog shows "In &lt;name&gt;".</param>
/// <param name="Command">The failed call as <c>ErrorCommand</c> shows it, or null for a dialog without git's output (then there is no <c>ErrorOutput</c> either).</param>
/// <param name="Buttons">The visible buttons, left to right: "Copy output", the extra action if any, "Close".</param>
/// <param name="Lines">The output's lines as the report quotes them (Git 2.36's, or the app's own when <paramref name="OutputFromGit"/> is false).</param>
/// <param name="KeyLine">The line that classifies the failure, asserted on any git; null when the report names none.</param>
/// <param name="OutputFromGit">False for an output the app writes itself (check 17b's lock files), asserted word for word on every git.</param>
public sealed record ExpectedErrorDialog(
    string Title,
    string Summary,
    string Repository,
    string? Command,
    IReadOnlyList<string> Buttons,
    IReadOnlyList<string> Lines,
    string? KeyLine = null,
    bool OutputFromGit = true);
