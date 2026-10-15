// What every part of phase 5's walk-through shares (docs/phase-5-design.md 15.3); the map of the
// project is in Phase5Checks.cs. Copied from phase 4's Phase4Checks.Support.cs, which stays as it
// is, with phase 5's part letters (a, b, f, p and u) in the picture names. The undo checks' project,
// VisualCommit.VisualTests.Phase5Undo, compiles this file too: both projects check with these
// helpers and save their pictures in phase 5's one folder.
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public abstract partial class Phase5Checks
{
    /// <summary>The phase whose report these checks are and whose picture folder they fill.</summary>
    protected const int Phase = Phase5Expectations.Phase;

    /// <summary>
    /// How long a wait for a change made outside the app (through <see cref="TempRepo"/>, seen by
    /// the app's file watcher) may take: 30 seconds (phase 3's; a hot laptop has missed 5 before).
    /// The expected result is the state after the wait, not how long it took.
    /// </summary>
    protected static readonly TimeSpan OutsideChangeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a network operation (a push or a pull through a file remote, the undo of a push) may take before a check gives up.</summary>
    protected static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long an operation that runs a chain of git commands (a rebase, Continue to the next
    /// stop, an interactive rebase with its execs) may take: under a full default run such a
    /// chain needed 60 seconds where it takes 18 alone (phase 3's handoff, "Rules worth keeping").
    /// </summary>
    protected static readonly TimeSpan OperationChainTimeout = TimeSpan.FromSeconds(60);

    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>
    /// The checks' default start: writes a session with one tab on <paramref name="repo"/> (a fresh
    /// copy of the named scenario), starts the app at the given size in the given theme, waits
    /// until the graph has drawn its rows, the working tree's status is shown and the operation
    /// queue is idle (<see cref="ShellDriver.WaitForRepositoryAsync"/>), and rests the pointer at
    /// the neutral point. Dispose the driver at the end of the check (<c>using var app = …</c>).
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
    /// Saves a picture of the window as artifacts/visual/phase-5/scripted/<paramref name="name"/>.png
    /// (the first picture of a run of both phase 5 projects empties the folder, <see cref="VisualRun.Save"/>) and returns it
    /// for pixel assertions. The name is the report's, "&lt;check&gt;&lt;step&gt;-&lt;what&gt;".
    /// Before the capture the pointer moves to the neutral point, as both harnesses do before a
    /// capture that is not about a hover, unless <paramref name="keepPointer"/> is true (a hover,
    /// a tooltip or a drag of a list's row), a menu is open or a branch drag runs. Before that it
    /// waits until the details panel is not reading a commit's details
    /// (<see cref="ShellDriver.WaitForCommitDetails"/>). The step's time is noted for design 15.6's
    /// budget (artifacts/visual/phase-5/timings/).
    /// </summary>
    protected static Screenshot Shot(ShellDriver app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!PictureName().IsMatch(name))
        {
            throw new ArgumentException($"\"{name}\" is not a picture name of the report: <check><step>-<what>, such as u16a-undo.", nameof(name));
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
    /// Asserts a menu's height by phase 3's 4a formula, 8 + 24 + 26 × items + 9 × separators (±2),
    /// and that it lies at least 4 px inside the window.
    /// </summary>
    protected static void AssertMenuShape(ShellDriver app, MenuSnapshot menu)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(menu);
        var items = menu.Items.Count;
        var separators = menu.Entries.Count - items;
        var expected = MenuHeight(items, separators);
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
    /// Asserts the error dialog on top as phase 3's "Git's own texts" say: always the app's own
    /// parts (title, summary, "In &lt;repo&gt;", the command, the buttons); for a dialog with git's
    /// output, that <c>ErrorOutput</c> is exactly <c>GitOutputCleaner.Clean</c> of the failed
    /// call's own standard error and output as the call log recorded them; on every git, without
    /// the app's code, that it is what git printed (<see cref="AssertGitOutputShown"/>, with the
    /// classifying key line); then, on Git 2.36, the report's quoted lines word for word. Returns
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
        if (await IsReportGitAsync())
        {
            Assert.Equal(expected.Lines, dialog.OutputLines);
        }

        return dialog;
    }

    /// <summary>
    /// What the error dialog shows of a failed call (<paramref name="shown"/>), held against what
    /// git printed as the call log recorded it, by rules of the test's own and not the app's
    /// cleaner: <paramref name="keyLine"/> is a line git printed and a line of the dialog; every
    /// line of the dialog is a line git printed; every line of standard error that starts with
    /// <c>fatal:</c>, <c>error:</c> or <c> ! [</c> is shown; and no line shows git's progress count
    /// or a carriage return.
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

    /// <summary>The lines of git's raw output: split at every line feed and carriage return, without trailing spaces and empty lines.</summary>
    private static List<string> PrintedLines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>The count of a progress line of git, as in <c>Receiving objects:  45% (9/20)</c>.</summary>
    [GeneratedRegex(@"\d{1,3}% \(\d+/\d+\)")]
    private static partial Regex GitProgressCount();

    /// <summary>
    /// Asserts the confirmation on top (D69, with phase 4 design 7.2's texts): its title
    /// (<c>ConfirmTitle</c>), question (<c>ConfirmQuestion</c>), detail (<c>ConfirmDetail</c>,
    /// hidden when <paramref name="detail"/> is empty), the cancel button's label
    /// (<paramref name="cancelText"/>: "Cancel", or "Keep editing" for a question about leaving
    /// work, phase 4 design 7.2) with the keyboard focus, and the confirm button's label, in the danger
    /// style when <paramref name="destructive"/> (else the primary one). Waits for the dialog first.
    /// </summary>
    protected static async Task AssertConfirmationAsync(
        ShellDriver app,
        string title,
        string question,
        string detail,
        string confirmText,
        bool destructive = true,
        string cancelText = "Cancel")
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
        Assert.Equal(cancelText, cancel.Content);
        await app.WaitForAsync(() => cancel.IsFocused, "the confirmation's cancel button to have the keyboard focus");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(confirmText, confirm.Content);
        Assert.Equal(destructive, confirm.Classes.Contains("danger"));
        Assert.Equal(destructive, dialog.IsDestructive);
    }

    /// <summary>
    /// Asserts the in-progress banner (<c>ConflictBanner</c>, phase 4 design 5.2, D122 as amended): its
    /// text (<c>BannerText</c>); each of its buttons, Continue (<c>BannerContinueButton</c>), Skip
    /// (<c>BannerSkipButton</c>) and Abort (<c>BannerAbortButton</c>), shown or not as expected
    /// (null: not shown), with its label, whether it is enabled and its tooltip (a disabled one's
    /// reason, shown on the disabled button too); the buttons in that order left to right, 26
    /// high, 6 apart, the last 8 from the banner's right edge; whether the text and the buttons
    /// fit on <paramref name="oneLine"/> (one line of text, the buttons beside it, the banner
    /// exactly 36 high; "more": higher than 36), and where the buttons stand,
    /// <paramref name="placement"/> (beside the text, which keeps at least 240 px, or on a row of
    /// their own under it). The banner is at least 36 high. Null for <paramref name="oneLine"/> or
    /// <paramref name="placement"/>: the check states nothing about it. Returns where the banner is.
    /// </summary>
    protected static Rect AssertBanner(
        ShellDriver app,
        string text,
        string? abortText,
        BannerButton? continueButton = null,
        BannerButton? skipButton = null,
        bool? oneLine = null,
        BannerButtonPlacement? placement = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Settle();
        var bounds = app.BannerBounds ?? throw new Xunit.Sdk.XunitException("No banner is shown.");
        var textBlock = (TextBlock)app.FindByAutomationId("BannerText");
        Assert.Equal(text, textBlock.Text);

        var continueControl = AssertBannerButton(app, ShellDriver.BannerContinueId, "Continue", continueButton);
        var skipControl = AssertBannerButton(app, ShellDriver.BannerSkipId, "Skip", skipButton);
        var abortControl = AssertBannerButton(app, "BannerAbortButton", "Abort", abortText is null ? null : new BannerButton(abortText));
        var buttons = new[] { continueControl, skipControl, abortControl }.OfType<Button>().Select(app.BoundsOf).ToList();

        var problems = BannerLayoutProblems(bounds, app.BoundsOf(textBlock), TextLineCount(textBlock), buttons, oneLine, placement);
        Assert.True(problems.Count == 0, "The banner's layout is not the expected one:\n" + string.Join('\n', problems));
        return bounds;
    }

    /// <summary>
    /// What is wrong with a banner's layout (phase 4 design 5.2), from where its parts are: the banner
    /// <paramref name="banner"/>, its text <paramref name="text"/> of <paramref name="textLines"/>
    /// lines, and its shown buttons <paramref name="buttons"/> in the order Continue, Skip, Abort.
    /// Empty when nothing is. Kept apart from the window so that its rules are tested on their own.
    /// </summary>
    internal static List<string> BannerLayoutProblems(Rect banner, Rect text, int textLines, IReadOnlyList<Rect> buttons, bool? oneLine, BannerButtonPlacement? placement)
    {
        var problems = new List<string>();
        var tolerance = 1.0;
        if (banner.Height < WindowLayout.BannerMinHeight - 0.5)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner is {banner.Height} high, less than {WindowLayout.BannerMinHeight}."));
        }

        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            if (Math.Abs(button.Height - BannerButtonHeight) > 0.5)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Button {index + 1} is {button.Height} high, not {BannerButtonHeight}."));
            }

            if (index > 0 && Math.Abs(button.Left - buttons[index - 1].Right - BannerButtonGap) > tolerance)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Buttons {index} and {index + 1} are {button.Left - buttons[index - 1].Right} apart, not {BannerButtonGap}."));
            }
        }

        if (buttons.Count > 0 && Math.Abs(banner.Right - buttons[^1].Right - BannerButtonRightInset) > tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The last button is {banner.Right - buttons[^1].Right} from the banner's right edge, not {BannerButtonRightInset}."));
        }

        var besideText = buttons.All(button => button.Left >= text.Right - 0.5 && button.Top < text.Bottom && button.Bottom > text.Top);
        var ownRow = buttons.Count > 0
            && buttons.All(button => button.Top >= text.Bottom - 0.5 && Math.Abs(button.Top - buttons[0].Top) < 0.5);
        if (placement is { } where && buttons.Count > 0)
        {
            if (where == BannerButtonPlacement.BesideText && !besideText)
            {
                problems.Add("The buttons do not stand beside the text: " + Describe(text, buttons));
            }
            else if (where == BannerButtonPlacement.OwnRow && !ownRow)
            {
                problems.Add("The buttons are not on a row of their own under the text: " + Describe(text, buttons));
            }
        }

        if (buttons.Count > 0 && besideText && text.Width < BannerTextMinWidth - 0.5)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The buttons stand beside a text {text.Width} wide, less than {BannerTextMinWidth}: they belong on a row of their own."));
        }

        if (oneLine is { } single)
        {
            var isOneLine = textLines == 1 && Math.Abs(banner.Height - WindowLayout.BannerMinHeight) < 0.5 && (buttons.Count == 0 || besideText);
            if (single && !isOneLine)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner is not on one line: {textLines} lines of text, {banner.Height} high, buttons {(besideText ? "beside the text" : "under it")}."));
            }
            else if (!single && (banner.Height < WindowLayout.BannerMinHeight + 0.5 || (textLines == 1 && besideText)))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"The banner takes one line: {textLines} lines of text, {banner.Height} high."));
            }
        }

        return problems;
    }

    /// <summary>
    /// Asserts one of the banner's buttons by its automation id: hidden (or not in the window)
    /// when <paramref name="expected"/> is null; else shown with its label, enabled or not, its
    /// tooltip when the check states one (a disabled button's reason always), shown on a disabled
    /// button too. Returns the shown button, or null.
    /// </summary>
    private static Button? AssertBannerButton(ShellDriver app, string automationId, string what, BannerButton? expected)
    {
        var button = app.Window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId && control.IsEffectivelyVisible);
        if (expected is null)
        {
            Assert.True(button is null, $"The banner shows {what} (\"{button?.Content}\"); the check expects none.");
            return null;
        }

        Assert.True(button is not null, $"The banner shows no {what}; the check expects \"{expected.Text}\".");
        Assert.Equal(expected.Text, button!.Content as string);
        Assert.True(expected.Enabled == button.IsEffectivelyEnabled, $"The banner's {what} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        if (!expected.Enabled || expected.ToolTip is not null)
        {
            Assert.Equal(expected.ToolTip, ToolTip.GetTip(button) as string);
        }

        if (!expected.Enabled)
        {
            Assert.True(ToolTip.GetShowOnDisabled(button), $"The disabled {what} does not show its reason (ToolTip.ShowOnDisabled).");
        }

        return button;
    }

    /// <summary>How many lines a text block's text takes as laid out.</summary>
    private static int TextLineCount(TextBlock block) => block.TextLayout.TextLines.Count;

    private static string Describe(Rect text, IReadOnlyList<Rect> buttons) =>
        string.Create(CultureInfo.InvariantCulture, $"text at {text}, buttons at {string.Join(", ", buttons)}");

    /// <summary>
    /// Asserts the graph as the report's tables write it, row by row from the top: each row's
    /// subject, node lane and colour, and its labels in drawing order (<see cref="CommitRow"/>:
    /// " ✓" after the branch HEAD is on, "HEAD" for a detached HEAD), with what the row states
    /// besides (short SHA, merge, stash, date, the lanes that end in its node); then the number of
    /// rows, the lane count with the Graph column's width (16 per lane plus 16, at least 48), the
    /// working-changes row (its text and ring) or its absence, and whether the Date column shows.
    /// With <paramref name="picture"/>, each node's colour (a stash's ring) and the working-changes
    /// ring are also sampled in it. The history must be complete.
    /// </summary>
    protected static void AssertGraph(
        ShellDriver app,
        IReadOnlyList<CommitRow> rows,
        int lanes,
        WorkingChangesRow? working = null,
        bool? showsDate = null,
        Screenshot? picture = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(rows);
        var graph = Graph(app);
        var data = graph.Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        Assert.True(data.IsComplete, "The graph's history is not complete.");
        var actual = Enumerable.Range(0, data.Count).Select(row => DescribeRow(graph, row)).ToList();
        Assert.True(
            data.Count == rows.Count && Enumerable.Range(0, rows.Count).All(row => RowMatches(graph, rows[row], row)),
            "The graph is not the expected one.\nExpected:\n  " + string.Join("\n  ", rows) + "\nActual:\n  " + string.Join("\n  ", actual));

        Assert.Equal(lanes, data.MaxLaneCount);
        Assert.Equal(WindowLayout.GraphColumnWidth(lanes), graph.Columns.Graph.Width);
        if (showsDate is { } date)
        {
            Assert.Equal(date, graph.Columns.ShowsDate);
        }

        Assert.True((working is not null) == graph.ShowsWorkingRow, $"The working-changes row is {(graph.ShowsWorkingRow ? "shown" : "hidden")}.");
        if (working is not null)
        {
            Assert.Equal(working.Text, graph.WorkingRowText ?? CommitGraphControl.DefaultWorkingRowText);
        }

        if (picture is null)
        {
            return;
        }

        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;
        for (var index = 0; index < rows.Count; index++)
        {
            var expected = rows[index];
            var centre = new Point(origin.X + graph.LaneCenterX(expected.Lane), origin.Y + graph.RowBounds(index).Center.Y);
            if (expected.Stash)
            {
                AssertRing(picture, centre, palette.Lane(expected.Colour), $"row {index}'s stash ring");
            }
            else
            {
                AssertColour(palette.Lane(expected.Colour), picture, centre, $"node of row {index} (\"{expected.Message}\")");
            }
        }

        if (working is not null)
        {
            var ring = new Point(origin.X + graph.LaneCenterX(working.Lane), origin.Y + graph.WorkingRowBounds.Center.Y);
            AssertRing(picture, ring, palette.Lane(working.Colour), "the working-changes ring");
        }
    }

    private static bool RowMatches(CommitGraphControl graph, CommitRow want, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        return commit.Subject == want.Message
            && layout.NodeLane == want.Lane
            && layout.NodeColor == want.Colour
            && GraphLabels(data, row).SequenceEqual(want.Labels)
            && (want.ShortSha is null || commit.Sha.StartsWith(want.ShortSha, StringComparison.Ordinal))
            && (want.Merge is not { } merge || merge == commit.Parents.Count > 1)
            && want.Stash == (commit.Kind == CommitKind.Stash)
            && (want.Date is null || graph.Dates?.Format(commit.AuthorDate) == want.Date)
            && (want.Ends is null || want.Ends.Order().SequenceEqual(EndingLanes(layout)));
    }

    private static string DescribeRow(CommitGraphControl graph, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        return new CommitRow(
            commit.Subject,
            layout.NodeLane,
            layout.NodeColor,
            [.. GraphLabels(data, row)],
            commit.Sha[..7],
            commit.Parents.Count > 1,
            commit.Kind == CommitKind.Stash,
            graph.Dates?.Format(commit.AuthorDate),
            EndingLanes(layout)).ToString();
    }

    /// <summary>A row's labels as the report writes them: their texts in drawing order, " ✓" after the branch HEAD is on.</summary>
    private static IEnumerable<string> GraphLabels(CommitGraphData data, int row) =>
        data.LabelsAt(row).Select(label => label.Kind == RefLabelKind.CurrentBranch ? Current(label.Text) : label.Text);

    /// <summary>The lanes other than a row's own that end in its node, in order.</summary>
    private static List<int> EndingLanes(GraphRow layout) =>
        [.. layout.Lines.Where(line => line.Kind == GraphLineKind.IntoNode && line.FromLane != layout.NodeLane).Select(line => line.FromLane).Distinct().Order()];

    /// <summary>A ring 10 across and 2 wide: between 3 and 5 pixels right of its centre some pixel has the colour, and its centre does not.</summary>
    private static void AssertRing(Screenshot screenshot, Point centre, Color colour, string what)
    {
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The {what} at {centre} is not {colour} anywhere from 3 to 5 pixels right of its centre: {string.Join(", ", samples)}.");
        Assert.NotEqual(colour, screenshot.PixelAt(centre));
    }

    /// <summary>The backup refs (<c>refs/visualcommit/backup/</c>), parsed (<see cref="BackupRef"/>), with what each points at, in git's order of their names.</summary>
    protected static async Task<List<(BackupRef Ref, string Name, string Sha)>> BackupsAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return [.. (await repo.BackupRefsAsync()).Select(entry => (BackupRef.Parse(entry.Name), entry.Name, entry.Sha))];
    }

    /// <summary>The git calls the app made since <paramref name="mark"/> (a count of <see cref="ShellDriver.GitCalls"/> read before the input), as their command lines.</summary>
    protected static List<string> CallsSince(ShellDriver app, int mark)
    {
        ArgumentNullException.ThrowIfNull(app);
        return [.. app.GitCalls().Skip(mark).Select(call => call.CommandText)];
    }

    /// <summary>Asserts that the app made the call <paramref name="command"/> since <paramref name="mark"/>, and returns its (last) record.</summary>
    protected static GitCallRecord AssertCalled(ShellDriver app, int mark, string command)
    {
        ArgumentNullException.ThrowIfNull(app);
        var call = app.GitCalls().Skip(mark).LastOrDefault(record => record.CommandText == command);
        Assert.True(call is not null, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', CallsSince(app, mark)));
        return call!;
    }

    /// <summary>Asserts the order of calls the app made since <paramref name="mark"/>: these commands, each after the one before.</summary>
    protected static void AssertCalledInOrder(ShellDriver app, int mark, params string[] commands)
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

    /// <summary>
    /// Waits until the queue logged the end of the operation <paramref name="title"/> since
    /// <paramref name="mark"/> (<see cref="ShellDriver.LogMark"/>, read before the input that
    /// started it), whatever its outcome, and the tab is idle, its refresh included. At most
    /// <paramref name="timeout"/> (<see cref="OperationChainTimeout"/> when null).
    /// </summary>
    protected static async Task WaitForOperationAsync(ShellDriver app, TempRepo repo, string title, int mark, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(repo);
        var limit = timeout ?? OperationChainTimeout;
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(NameOf(repo), title), mark: mark, timeout: limit);
        await app.WaitForIdleAsync(timeout: limit);
    }

    /// <summary>The working tree's folder name: the tab's and the repository's name.</summary>
    protected static string NameOf(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return Path.GetFileName(repo.Path);
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
    /// instant <paramref name="expected"/> in the zone it carries. Git 2.36 writes UTC as
    /// <c>+00:00</c> and git 2.55 as <c>Z</c>, so the text is never compared (<see cref="GitIsoDates"/>).
    /// </summary>
    protected static void AssertGitDate(DateTimeOffset expected, string gitText)
    {
        ArgumentNullException.ThrowIfNull(gitText);
        var actual = GitIsoDates.Parse(gitText);
        Assert.Equal((expected, expected.Offset), (actual, actual.Offset));
    }

    /// <summary>
    /// The working tree's folder as git names it (<c>rev-parse --show-toplevel</c>), which is the
    /// folder the app shows and passes on, in the platform's form, full and without a trailing
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
    protected static Palette PaletteOf(ShellDriver app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ? Light : Dark;
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

    /// <summary>Notes a step's time in artifacts/visual/phase-5/timings/&lt;test&gt;.txt; the first note of a test in a run starts its file afresh.</summary>
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
    /// A picture's name: the check (its part's letter, as the report numbers phase 5's checks: a
    /// for the shell and the activity log, b for the pages, f for finding, p for protected changes
    /// and u for undo, design 15.4; and its number; or the number alone), the step (up to three
    /// letters or digits), then words joined by hyphens: <c>a2c-activity-log</c>, <c>u16b-undone</c>.
    /// The letter c is no part's, so that a name never reads as the requirement C12.
    /// </summary>
    [GeneratedRegex(@"^[abfpu]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    protected static partial Regex PictureName();
}

/// <summary>
/// The error dialog a check expects (<see cref="Phase5Checks.AssertErrorDialogAsync"/>).
/// </summary>
/// <param name="Title">"Rebase failed", "Undo failed", … (phase 4 design 10.1, design 12.1).</param>
/// <param name="Summary">The summary, word for word.</param>
/// <param name="Repository">The repository's name: the dialog shows "In &lt;name&gt;".</param>
/// <param name="Command">The failed call as <c>ErrorCommand</c> shows it, or null for a dialog without git's output (then there is no <c>ErrorOutput</c> either).</param>
/// <param name="Buttons">The visible buttons, left to right: "Copy output", the extra action if any, "Close".</param>
/// <param name="Lines">The output's lines as the report quotes them (Git 2.36's, or the app's own when <paramref name="OutputFromGit"/> is false).</param>
/// <param name="KeyLine">The line that classifies the failure, asserted on any git; null when the report names none.</param>
/// <param name="OutputFromGit">False for an output the app writes itself, asserted word for word on every git.</param>
public sealed record ExpectedErrorDialog(
    string Title,
    string Summary,
    string Repository,
    string? Command,
    IReadOnlyList<string> Buttons,
    IReadOnlyList<string> Lines,
    string? KeyLine = null,
    bool OutputFromGit = true);
