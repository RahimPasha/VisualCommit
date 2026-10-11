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
//   menu is open, a drag runs, or the capture is about a hover (keepPointer: true).
// - Every wait is on a condition (ShellDriver.WaitForAsync, WaitForIdleAsync, WaitForLogLineAsync,
//   WaitForToolTipAsync, WaitForDialogAsync), never a fixed time; a change made outside the app is
//   waited for with OutsideChangeTimeout.
// - Rows are clicked with ShellDriver.GraphRowPoint / WorkingRowPoint (from the graph control's
//   bounds, right below a banner too, D122); left-panel rows with RefRowAsync(section, …, name);
//   file rows with FileRowAsync(list, path). A position the real-window pass needs that only the
//   scripted run can measure (a label's centre, for check 7's drag) goes into the part's
//   expectations as a constant that the scripted check asserts (±1) and the pass uses.
// - Menus are asserted with AssertMenu (the report's notation), AssertHighlighted, AssertMenuShape
//   and AssertMenuAtPointer; "no menu" with AssertNoMenuAsync(app, app.LogMark() read before the
//   input); the error dialog with AssertErrorDialogAsync (git's lines word for word only on
//   Git 2.36); the status bar with AssertStatus and AssertBranch.
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
    /// pointer stays where the drag is). The step's time is noted for design 7.7's budget
    /// (artifacts/visual/phase-3/timings/).
    /// </summary>
    protected static Screenshot Shot(ShellDriver app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!PictureName().IsMatch(name))
        {
            throw new ArgumentException($"\"{name}\" is not a picture name of the report: <check><step>-<what>, such as 01a-commit-menu.", nameof(name));
        }

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
    /// call log recorded them; then, on Git 2.36, the report's quoted lines word for word, and on
    /// another git the classifying key line among them. Returns what the dialog shows.
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

        var call = app.LastCall(expected.Command);
        Assert.False(call.Succeeded, $"The call \"{call.CommandText}\" succeeded; the dialog shows a failure.");
        Assert.Equal(ShellDriver.CleanedOutputOf(call), dialog.Output);
        if (await Phase3Expectations.IsReportGitAsync())
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
        }
        else if (expected.KeyLine is { } key)
        {
            Assert.Contains(key, dialog.OutputLines);
        }

        return dialog;
    }

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
    /// Asserts the in-progress banner (<c>ConflictBanner</c>, design section 5, D122): its text
    /// (<c>BannerText</c>), its Abort button's label (<c>BannerAbortButton</c>; null for a banner
    /// without one), and a height of at least 36, exactly 36 when <paramref name="oneLine"/> is
    /// true (null: the check states no line count). Returns where the banner is.
    /// </summary>
    protected static Rect AssertBanner(ShellDriver app, string text, string? abortText, bool? oneLine = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        var bounds = app.BannerBounds ?? throw new Xunit.Sdk.XunitException("No banner is shown.");
        Assert.Equal(text, ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        var abort = (Button)app.FindByAutomationId("BannerAbortButton");
        Assert.Equal(abortText is not null, abort.IsEffectivelyVisible);
        if (abortText is not null)
        {
            Assert.Equal(abortText, abort.Content);
        }

        Assert.True(bounds.Height >= WindowLayout.BannerMinHeight - 0.5, $"The banner is {bounds.Height} high, less than 36 (D122).");
        if (oneLine is { } single)
        {
            Assert.Equal(single, Math.Abs(bounds.Height - WindowLayout.BannerMinHeight) < 0.5);
        }

        return bounds;
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
    protected static List<string> TextsIn(Visual region) => Phase0.ShellExpectations.TextsIn(region);

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
