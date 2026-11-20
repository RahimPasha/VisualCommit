// Phase 8a's support (docs/phase-8a-design.md 1.15): copied from Phase7bChecks.Support.cs with 8a's
// names: Shot and its picture and timing helpers (artifacts/visual/phase-8a/timings/), the menu,
// dialog, status, clipped-text, tooltip and error-dialog assertions, the call assertions (a long
// mark, presence and order only), and the picture pattern of design 0.3; the start of a check on an
// empty tab or on one repository (StartEmptyAsync, OpenAsync), which takes a probe for git and the
// helper's path. Every provider stays off. Shared helpers live here only; a part's own go in its
// part files.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

public abstract partial class Phase8aChecks
{
    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>The picture names saved in this run (this test process), so that two steps saving one name fail instead of overwriting.</summary>
    private static readonly HashSet<string> SavedPictures = new(StringComparer.Ordinal);

    /// <summary>The phase, as the docs write it (design 0.3).</summary>
    protected const string Phase = Phase8aExpectations.Phase;

    /// <summary>
    /// One empty tab at the given size in the given theme, git looked for by
    /// <paramref name="gitProbe"/> (the machine's search when null) and the prompt broker's helper
    /// looked for at <paramref name="askPassHelper"/> (next to <c>VisualCommit.Git</c> when null), the
    /// pointer at rest, once the start-up work has ended (finding git).
    /// </summary>
    protected static async Task<ShellDriver> StartEmptyAsync(
        TempDirectory data,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        bool systemDark = true,
        IGitSetupProbe? gitProbe = null,
        string? askPassHelper = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        UseTheme(data, theme);
        var app = ShellDriver.Start(data.Path, width, height, systemDark: systemDark, gitProbe: gitProbe, askPassHelper: askPassHelper);
        try
        {
            await app.WaitUntilReadyAsync();
        }
        catch
        {
            app.Dispose();
            throw;
        }

        app.MoveToNeutral();
        return app;
    }

    /// <summary>
    /// One tab on the repository at <paramref name="folder"/>, at the given size in the given theme,
    /// git looked for by <paramref name="gitProbe"/> (the machine's search when null); waits for the
    /// repository (<see cref="ShellDriver.WaitForRepositoryAsync"/>) and for the watchers to be
    /// quiet, and rests the pointer. Dispose the driver at the end of the check.
    /// </summary>
    protected static async Task<ShellDriver> OpenAsync(
        TempDirectory data,
        string folder,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        bool systemDark = true,
        IGitSetupProbe? gitProbe = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(folder);
        UseTheme(data, theme);
        var app = ShellDriver.StartWithTabs(data.Path, [folder], width: width, height: height, systemDark: systemDark, gitProbe: gitProbe);
        try
        {
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Writes <paramref name="theme"/> into the data folder's settings before the start; dark, the default, writes nothing.</summary>
    private static void UseTheme(TempDirectory data, AppTheme theme)
    {
        if (theme != AppTheme.Dark)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = theme });
        }
    }

    /// <summary>
    /// Saves a picture of the window as artifacts/visual/phase-8a/scripted/<paramref name="name"/>.png
    /// (the first picture of a run empties the folder, <see cref="VisualRun.Save"/>) and returns it
    /// for pixel assertions. The name is the report's, "&lt;check&gt;&lt;step&gt;-&lt;what&gt;", and is
    /// refused when a step of this run saved it already (<see cref="ClaimPictureName"/>).
    /// Before the capture the pointer moves to the neutral point, as both harnesses do before a
    /// capture that is not about a hover, unless <paramref name="keepPointer"/> is true (a hover,
    /// a tooltip or a drag of a list's row), a menu is open or a branch drag runs. Before that it
    /// waits until the details panel is not reading a commit's details
    /// (<see cref="ShellDriver.WaitForCommitDetails"/>). The step's time is noted for design 0.6's
    /// budget (artifacts/visual/phase-8a/timings/).
    /// </summary>
    protected static Screenshot Shot(ShellDriver app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        ClaimPictureName(name);
        var screenshot = CaptureForPicture(app, keepPointer);
        screenshot.Save(Phase, name);
        NoteTime(app.MarkStep(name));
        return screenshot;
    }

    /// <summary><see cref="Shot"/>'s capture: the details panel's read waited for, the pointer at the neutral point unless kept, a menu is open or a drag runs.</summary>
    private static Screenshot CaptureForPicture(ShellDriver app, bool keepPointer)
    {
        app.WaitForCommitDetails();
        WaitForAuthorAvatar(app);
        if (!keepPointer && !app.IsMenuOpen && !IsDragging(app))
        {
            app.MoveToNeutral();
        }

        return app.Capture();
    }

    /// <summary>
    /// While the active tab's details name an author's image (<c>Details.AuthorAvatarFile</c>) that
    /// <c>DetailsAuthorAvatar</c> does not show yet (it is decoded off the UI thread), waits for it.
    /// Nothing to wait for without a file, as always with GitHub off, or without the panel in the window.
    /// </summary>
    private static void WaitForAuthorAvatar(ShellDriver app)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            app.Settle();
            if (app.Session.Shell.ActiveTab.Repository?.Details is not { AuthorAvatarFile: not null }
                || app.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().FirstOrDefault(ellipse => ellipse.Name == "DetailsAuthorAvatar") is not { } avatar
                || avatar.IsVisible)
            {
                return;
            }

            if (clock.Elapsed >= ShellDriver.DefaultWaitTimeout)
            {
                throw new TimeoutException("Timed out waiting for the details panel to show the author's avatar.");
            }

            Thread.Sleep(10);
        }
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
    /// Asserts a menu's height by phase 3's 4a formula, 8 + 24 + 26 × items + 9 × separators (±2),
    /// and that it lies at least 4 px inside the window.
    /// </summary>
    protected static void AssertMenuShape(ShellDriver app, MenuSnapshot menu)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(menu);
        var items = menu.Items.Count;
        var separators = menu.Entries.Count - items;
        var expected = WindowLayout.MenuHeight(items, separators);
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
    /// "No menu opens" / "no menu is open", as phase 3's conventions assert it: once the input is
    /// settled and the tab is idle, the menu layer has no menu, no <c>Context menu opened</c> line
    /// was logged since <paramref name="logMark"/> (<see cref="ShellDriver.LogMark"/>, read before
    /// the input; null to skip), and no menu is in the window.
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

    /// <summary>"No dialog": once the app is idle after the input (the operation's refresh included), the dialog layer has nothing open.</summary>
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
    /// Asserts the status bar's operation text (<c>OperationStatus</c>) and its tone (secondary,
    /// the warning colour for a stop with conflicts, the danger colour for "&lt;Title&gt; failed"),
    /// and that the text is drawn in the brush the tone names in the active theme
    /// (<c>VcTextSecondaryBrush</c>, <c>VcWarningBrush</c>, <c>VcDangerBrush</c>): the token, not a
    /// colour of this file, so that the contrast round's new values need no change here.
    /// </summary>
    protected static void AssertStatus(ShellDriver app, string text, OperationStatusTone tone = OperationStatusTone.Secondary)
    {
        ArgumentNullException.ThrowIfNull(app);
        var status = app.Session.Shell.ActiveTab.OperationStatus;
        Assert.Equal(text, status.Text);
        Assert.Equal(tone, status.Tone);
        var block = (TextBlock)app.FindByAutomationId("OperationStatus");
        Assert.Equal(text, block.Text);
        var key = tone switch
        {
            OperationStatusTone.Warning => "VcWarningBrush",
            OperationStatusTone.Danger => "VcDangerBrush",
            _ => "VcTextSecondaryBrush",
        };
        Assert.True(block.TryFindResource(key, block.ActualThemeVariant, out var token), $"The theme has no {key}.");
        Assert.Equal((token as ISolidColorBrush)?.Color, (block.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>Asserts the status bar's branch text (<c>CurrentBranch</c>): a branch's name or "Detached at &lt;sha&gt;".</summary>
    protected static void AssertBranch(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Equal(text, ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);
    }

    /// <summary>
    /// The git calls the app made after <paramref name="mark"/> (a <see cref="GitCallRecord.Sequence"/>
    /// read before the input), as their command lines. Presence and order only: a count goes through
    /// <c>GitCallBudget</c>.
    /// </summary>
    protected static List<string> CallsSince(ShellDriver app, long mark)
    {
        ArgumentNullException.ThrowIfNull(app);
        return [.. app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText)];
    }

    /// <summary>Asserts that the app made the call <paramref name="command"/> after <paramref name="mark"/>, and returns its (last) record. Presence only.</summary>
    protected static GitCallRecord AssertCalled(ShellDriver app, long mark, string command)
    {
        ArgumentNullException.ThrowIfNull(app);
        var call = app.GitCalls().Where(record => record.Sequence > mark).LastOrDefault(record => record.CommandText == command);
        Assert.True(call is not null, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', CallsSince(app, mark)));
        return call!;
    }

    /// <summary>Asserts the order of calls the app made after <paramref name="mark"/>: these commands, each after the one before. Order only.</summary>
    protected static void AssertCalledInOrder(ShellDriver app, long mark, params string[] commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var calls = CallsSince(app, mark);
        var at = -1;
        foreach (var command in commands)
        {
            var next = calls.FindIndex(at + 1, call => call == command);
            Assert.True(next > at, $"The app did not run \"{command}\" after the calls before it. It ran:\n" + string.Join('\n', calls));
            at = next;
        }
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

    /// <summary>Asserts that <paramref name="text"/> is in no log line or call record.</summary>
    protected static void AssertNotWritten(ShellDriver app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        var found = app.WhereWritten(text);
        Assert.True(found.Count == 0, $"\"{text}\" was written down:\n" + string.Join('\n', found));
    }

    /// <summary>The visible texts inside a control, in reading order (top to bottom, then left to right).</summary>
    protected static List<string> TextsIn(Visual region) => VisibleTexts.In(region);

    /// <summary>Whether a branch drag runs in the window: its pointer must stay where the drag is.</summary>
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

    /// <summary>
    /// Takes <paramref name="name"/> for a picture of this run: refused when it is no picture name of
    /// the report (<see cref="Phase8aExpectations.PictureName"/>) or a step of this run has saved it
    /// already, so that two steps saving one name fail the run instead of one overwriting the other.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not of the report's form.</exception>
    /// <exception cref="InvalidOperationException">The name was saved already in this run.</exception>
    protected static void ClaimPictureName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!PictureName().IsMatch(name))
        {
            throw new ArgumentException($"\"{name}\" is not a picture name of the report: <check><step>-<what>, such as f3a-chosen-gone.", nameof(name));
        }

        lock (TimingGate)
        {
            if (!SavedPictures.Add(name))
            {
                throw new InvalidOperationException($"The picture \"{name}\" was saved already in this run: two steps must not save one name.");
            }
        }
    }

    /// <summary>Notes a step's time in artifacts/visual/phase-8a/timings/&lt;test&gt;.txt; the first note of a test in a run starts its file afresh.</summary>
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

    /// <summary>
    /// The error dialog on top: its title, summary and "In …" line; git's cleaned output holding
    /// <paramref name="keyLine"/> and ending with <paramref name="lastLine"/>; the command; the
    /// buttons left to right (<see cref="ButtonsOf"/>).
    /// </summary>
    protected static void AssertErrorDialog(
        ShellDriver app,
        ErrorDialogViewModel dialog,
        string title,
        string summary,
        string repository,
        string keyLine,
        string lastLine,
        string command,
        string[] buttons)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(dialog);
        var view = app.TopDialogView!;
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ErrorTitle")).Text);
        Assert.Equal(summary, ((TextBlock)app.FindByAutomationId(view, "ErrorSummary")).Text);
        Assert.Equal("In " + repository, ((TextBlock)app.FindByAutomationId(view, "ErrorRepository")).Text);
        var output = dialog.Output ?? string.Empty;
        Assert.Contains(keyLine, output, StringComparison.Ordinal);
        Assert.EndsWith(lastLine, output.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(command, dialog.Command);
        Assert.Equal(buttons, ButtonsOf(app, view));
    }

    /// <summary>The visible buttons of a dialog's view, left to right, as their texts.</summary>
    protected static List<string> ButtonsOf(ShellDriver app, Visual view) =>
        [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Bounds.Height > 0
                && AutomationProperties.GetAutomationId(button) is "CancelButton" or "ConfirmButton" or "ErrorCopyButton" or "ErrorExtraButton")
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => AutomationProperties.GetName(button) ?? button.Content as string ?? string.Empty)];
}
