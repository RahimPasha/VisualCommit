// Part A's own helpers (docs/test-reports/phase-5.md, part A): starting on a session that holds the
// panel's height, the toolbar and the status bar as part A's conventions assert them, the tabs,
// the activity log's panel, rows and boxes read through ShellDriver.ActivityLog() (AD12), an
// operation's calls held against the call log, the stage panel, and git read back. Only part A's
// writer edits this file; what every part shares is in Phase5Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksA
{
    // ---- Starting ----

    /// <summary>
    /// A2's start: a session with one tab on <paramref name="repo"/> that also holds the panel's
    /// height <paramref name="logHeight"/> (<c>ShellDriver.WriteSession</c>, then
    /// <c>ShellDriver.Start</c>), the repository open as <see cref="Phase5Checks.OpenAsync"/> waits
    /// for it, then the clean-up's record <c>Clean up backups</c> and an idle queue, the pointer at
    /// the neutral point.
    /// </summary>
    private static async Task<ShellDriver> OpenWithBackupsDueAsync(TempDirectory data, TempRepo repo, double logHeight)
    {
        ShellDriver.WriteSession(data.Path, new SessionState { Tabs = [new TabState(repo.Path)], ActiveTab = 0, ActivityLogHeight = logHeight });
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitForRepositoryAsync();
            await app.WaitForOperationRecordAsync(PartA.CleanUpTitle);
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Whether the platform's command modifier is Cmd (macOS): the keys and Search's tooltip follow it.</summary>
    private static bool CommandIsMeta(ShellDriver app) => app.CommandModifier.HasFlag(KeyModifiers.Meta);

    // ---- The tabs ----

    /// <summary>The tab strip's tab titled <paramref name="title"/> (its button, <c>RepoTab</c> named by the title).</summary>
    private static Button TabButton(ShellDriver app, string title)
    {
        app.Settle();
        var buttons = app.Window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == "RepoTab")
            .ToList();
        return buttons.SingleOrDefault(button => AutomationProperties.GetName(button) == title)
            ?? throw new Xunit.Sdk.XunitException($"No tab \"{title}\"; the tabs are: {string.Join(", ", buttons.Select(AutomationProperties.GetName))}.");
    }

    /// <summary>The tabs' titles left to right and the active one's.</summary>
    private static void AssertTabs(ShellDriver app, string active, params string[] titles)
    {
        app.Settle();
        var shell = app.Session.Shell;
        Assert.Equal(titles, shell.Tabs.Select(tab => tab.Title));
        Assert.Equal(active, shell.ActiveTab.Title);
        var buttons = titles.Select(title => app.BoundsOf(TabButton(app, title)).X).ToList();
        Assert.True(buttons.SequenceEqual(buttons.Order()), "The tabs are not drawn left to right in their order.");
    }

    // ---- The toolbar and the status bar ----

    /// <summary>
    /// Asserts each toolbar button as part A's conventions say: enabled or not
    /// (<c>IsEffectivelyEnabled</c>), its tooltip (null: none), its automation help text, which
    /// equals the tooltip (empty where there is none; every button's since phase 6), a disabled
    /// button that has a tooltip showing it while disabled, and its icon and label drawn in the
    /// primary colour while enabled and the disabled colour while not.
    /// </summary>
    private static void AssertToolbar(ShellDriver app, IReadOnlyList<PartA.ButtonState> states)
    {
        app.Settle();
        var palette = PaletteOf(app);
        foreach (var state in states)
        {
            var button = ToolbarButton(app, state.Id);
            Assert.True(state.Enabled == button.IsEffectivelyEnabled, $"{state.Id} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}; the report says {(state.Enabled ? "enabled" : "disabled")}.");
            Assert.True(state.ToolTip == ToolTip.GetTip(button) as string, $"{state.Id}'s tooltip is \"{ToolTip.GetTip(button)}\"; the report says \"{state.ToolTip}\".");

            // Since phase 6 every button's help text is its tooltip (docs/phase-6-design.md 6.8, 15.5).
            Assert.True(
                (state.ToolTip ?? string.Empty) == (AutomationProperties.GetHelpText(button) ?? string.Empty),
                $"{state.Id}'s help text is \"{AutomationProperties.GetHelpText(button)}\"; its tooltip is \"{state.ToolTip}\".");

            if (!state.Enabled && state.ToolTip is not null)
            {
                Assert.True(ToolTip.GetShowOnDisabled(button), $"{state.Id} does not show its tooltip while disabled.");
            }

            var label = button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == button.Label);
            var colour = state.Enabled ? palette.TextPrimary : palette.TextDisabled;
            Assert.True(colour == (label.Foreground as ISolidColorBrush)?.Color, $"{state.Id}'s label is drawn in {(label.Foreground as ISolidColorBrush)?.Color}, expected {colour}.");
            var icon = button.GetVisualDescendants().OfType<Icon>().Single();
            Assert.True(colour == (icon.Foreground as ISolidColorBrush)?.Color, $"{state.Id}'s icon is drawn in {(icon.Foreground as ISolidColorBrush)?.Color}, expected {colour}.");
        }
    }

    /// <summary>The git version as git reports it, as the status bar writes it: <c>Git 2.36.0</c> on the development machine.</summary>
    private static async Task<string> GitVersionTextAsync()
    {
        var path = GitLocator.FindExecutable(GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("Git was not found on this machine.");
        var result = await new GitRunner(path).RunAsync(new GitCommand("--version"), TestContext.Current.CancellationToken);
        Assert.True(GitVersion.TryParse(result.StandardOutput, out var version), $"git --version printed \"{result.StandardOutput}\".");
        return "Git " + version;
    }

    /// <summary>The status bar's texts, left to right: the branch, the operation's text, the git version and the toggle's label.</summary>
    private static async Task AssertStatusBarTextsAsync(ShellDriver app, string branch, string status)
    {
        Assert.Equal([branch, status, await GitVersionTextAsync(), PartA.ActivityLogLabel], TextsIn(app.FindByAutomationId("StatusBar")));
    }

    /// <summary>
    /// Asserts the status bar's Activity log toggle (1.2, part A's conventions): enabled or not,
    /// the class <c>selected</c> while the panel is open, its tooltip and help text (the show text
    /// while closed, the hide text while open), where it is (x 1005 to 1100, y 675 to 700), its
    /// label and icon in the disabled, secondary or (selected) primary colour, and with
    /// <paramref name="picture"/> its background at (1008, 678): the control background while
    /// selected, otherwise none (the status bar's).
    /// </summary>
    private static void AssertToggle(ShellDriver app, bool enabled, bool selected, Screenshot? picture = null)
    {
        app.Settle();
        var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
        var palette = PaletteOf(app);
        Assert.True(enabled == toggle.IsEffectivelyEnabled, $"The Activity log toggle is {(toggle.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        Assert.True(selected == toggle.Classes.Contains("selected"), $"The toggle {(selected ? "lacks" : "has")} the class selected.");
        var tip = selected ? PartA.HideActivityLog : PartA.ShowActivityLog;
        Assert.Equal(tip, ToolTip.GetTip(toggle) as string);
        Assert.Equal(tip, AutomationProperties.GetHelpText(toggle));
        AssertNear(PartA.ToggleBounds, app.BoundsOf(toggle), "the Activity log toggle");
        var label = toggle.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == PartA.ActivityLogLabel);
        var colour = !enabled ? palette.TextDisabled : selected ? palette.TextPrimary : palette.TextSecondary;
        Assert.True(colour == (label.Foreground as ISolidColorBrush)?.Color, $"The toggle's label is drawn in {(label.Foreground as ISolidColorBrush)?.Color}, expected {colour}.");
        var icon = toggle.GetVisualDescendants().OfType<Icon>().Single();
        Assert.True(colour == (icon.Foreground as ISolidColorBrush)?.Color, $"The toggle's icon is drawn in {(icon.Foreground as ISolidColorBrush)?.Color}, expected {colour}.");
        if (picture is not null)
        {
            AssertColour(selected ? palette.Control : palette.Chrome, picture, PartA.ToggleSample, "background of the Activity log toggle");
        }
    }

    // ---- Geometry ----

    /// <summary>Asserts that a rectangle lies where the report puts it, ±2 on each edge (AD9).</summary>
    private static void AssertNear(Rect expected, Rect actual, string what)
    {
        Assert.True(
            Math.Abs(expected.Left - actual.Left) <= PartA.Tolerance && Math.Abs(expected.Top - actual.Top) <= PartA.Tolerance
                && Math.Abs(expected.Right - actual.Right) <= PartA.Tolerance && Math.Abs(expected.Bottom - actual.Bottom) <= PartA.Tolerance,
            string.Create(CultureInfo.InvariantCulture, $"{what} is at {actual}; the report says {expected} (±{PartA.Tolerance})."));
    }

    /// <summary>
    /// No activity log in the layout (2.1): the panel and its splitter hidden, the graph area's
    /// content (here the graph's rows, or the page shown) down to the status bar's top, y 674; the
    /// side panels at full height.
    /// </summary>
    private static void AssertNoPanel(ShellDriver app)
    {
        app.Settle();
        Assert.False(app.ActivityLogView.IsEffectivelyVisible, "The activity log is shown.");
        Assert.False(app.FindByAutomationId("ActivityLogSplitter").IsEffectivelyVisible, "The activity log's splitter is shown.");
        Assert.Equal(PartA.GraphArea.Bottom, ContentBottom(app), PartA.Tolerance);
        AssertSidePanels(app);
    }

    /// <summary>
    /// The panel open at <paramref name="height"/> (±2), as part A's conventions place it: x 260 to
    /// 700, y 674 − h to 674; the splitter the 5 px above with its 1-px line in the border colour;
    /// the header its first 30 px and the list the rest; the graph area's content (the graph's
    /// rows, or the page shown) down to y 669 − h; the side panels at full height. With
    /// <paramref name="picture"/>, the splitter's line is sampled.
    /// </summary>
    private static ActivityLogSnapshot AssertPanelAt(ShellDriver app, double height, Screenshot? picture = null)
    {
        var log = app.ActivityLog();
        Assert.True(app.ActivityLogView.IsEffectivelyVisible, "The activity log is not shown.");
        AssertNear(PartA.PanelAt(height), log.Bounds, "The activity log");
        AssertNear(PartA.SplitterAt(height), app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")), "The activity log's splitter");
        AssertNear(new Rect(log.Bounds.X, log.Bounds.Y, log.Bounds.Width, PartA.HeaderHeight), log.HeaderBounds, "The activity log's header");
        AssertNear(new Rect(log.Bounds.X, log.Bounds.Y + PartA.HeaderHeight, log.Bounds.Width, log.Bounds.Height - PartA.HeaderHeight), log.ListBounds, "The activity log's list");
        var line = app.Find<Border>("ActivityLogSplitterLine");
        Assert.True(line.IsEffectivelyVisible, "The splitter's line is not shown.");
        Assert.Equal(1, line.Bounds.Height);
        Assert.Equal(PaletteOf(app).Border, (line.Background as ISolidColorBrush)?.Color);
        var lineBounds = app.BoundsOf(line);
        Assert.Equal(log.Bounds.Top - 1, lineBounds.Top, PartA.Tolerance);
        if (picture is not null)
        {
            AssertColour(PaletteOf(app).Border, picture, new Point(lineBounds.X + 100, lineBounds.Top), "the splitter's line");
        }

        Assert.Equal(PartA.GraphArea.Bottom - height - PartA.SplitterHeight, ContentBottom(app), PartA.Tolerance);
        AssertSidePanels(app);
        return log;
    }

    /// <summary>Where the graph area's content ends: the page's bottom while a page shows, else the graph's rows'.</summary>
    private static double ContentBottom(ShellDriver app) =>
        app.ShownPage is { } page ? app.BoundsOf(page).Bottom : app.BoundsOf(Graph(app)).Bottom;

    /// <summary>The left panel (x 0 to 260) and the right panel (x 700 to 1100) keep the full height, y 88 to 674.</summary>
    private static void AssertSidePanels(ShellDriver app)
    {
        AssertNear(new Rect(0, PartA.GraphArea.Top, PartA.GraphArea.Left, PartA.GraphArea.Height), app.BoundsOf(app.FindByAutomationId("LeftPanel")), "The left panel");
        AssertNear(new Rect(PartA.GraphArea.Right, PartA.GraphArea.Top, 400, PartA.GraphArea.Height), app.BoundsOf(app.FindByAutomationId("RightPanel")), "The right panel");
    }

    /// <summary>The panel's header (2.2): <c>Activity log</c>, the repository's name in the secondary colour, <c>Show reads</c> checked or not, the close button at the right.</summary>
    private static void AssertHeader(ShellDriver app, ActivityLogSnapshot log, string repository, bool showReads)
    {
        Assert.Equal(PartA.LogTitle, log.Title);
        Assert.Equal(repository, log.RepositoryName);
        Assert.Equal(PaletteOf(app).TextSecondary, log.RepositoryNameColor);
        Assert.Equal(showReads, log.ShowReads);
        Assert.True(log.ShowReadsBounds.Right <= log.CloseBounds.Left + 0.5, "Show reads is not left of the close button.");
        Assert.Equal(log.HeaderBounds.Right, log.CloseBounds.Right, 8.0);
        var close = (Button)app.FindByAutomationId("ActivityLogClose");
        Assert.True(close.IsEffectivelyVisible, "The panel's close button is hidden.");
        Assert.Same(Application.Current!.FindResource(PartA.IconClose), close.GetVisualDescendants().OfType<Icon>().Single().Data);
        var check = (CheckBox)app.FindByAutomationId("ActivityLogShowReads");
        Assert.Equal(PartA.ShowReads, TextsIn(check).Single());
    }

    // ---- The log's rows ----

    /// <summary>
    /// The colour an operation's outcome glyph has (2.2): the success, warning or danger colour or
    /// the secondary one; null while it runs.
    /// </summary>
    private static Color? GlyphColour(ShellDriver app, string? glyph)
    {
        var palette = PaletteOf(app);
        return glyph switch
        {
            PartA.IconCheck => palette.Success,
            PartA.IconAlert => palette.Warning,
            PartA.IconClose => palette.Danger,
            PartA.IconMinus => palette.TextSecondary,
            _ => null,
        };
    }

    /// <summary>
    /// Asserts whether a row lies whole in the list's view, as the report says (<paramref
    /// name="inView"/>): true, it does, and the row is asserted as drawn; false, the list, kept at
    /// its end (AD15), has scrolled it out of the view; null where the report says neither.
    /// </summary>
    private static void AssertView(ActivityLogRowSnapshot row, bool? inView)
    {
        if (inView is { } expected)
        {
            Assert.True(
                expected == row.IsInView,
                $"The row \"{row.AutomationName}\" is {(row.IsInView ? "whole in" : "not whole in")} the list's view; the report says it is {(expected ? "in" : "out of")} view.");
        }
    }

    /// <summary>
    /// Asserts an operation's row as the report writes it, <c>[glyph] time · title · message ·
    /// duration</c> (2.2): at the top level, its open state, its outcome, the frozen time
    /// <c>09:00:00</c>, its title (its automation name too), its message, and its duration
    /// (<c>0 ms</c>; none while it runs, AD4); whether it is in the list's view (<see
    /// cref="AssertView"/>). While the list draws the row: not indented, 24 high, its chevron, its
    /// glyph in its tone (none while running), the time in the secondary colour and the message in
    /// <paramref name="messageColour"/>. A row that the list, kept at its end, has scrolled out of
    /// reach is not drawn (AD15): then what the list holds for it.
    /// </summary>
    private static void AssertOperationRow(ShellDriver app, ActivityLogRowSnapshot row, string? glyph, string title, string message, Color messageColour, bool open, bool? inView = true)
    {
        var what = $"The row \"{row.AutomationName}\"";
        Assert.True(row.Kind == ActivityLogRowKind.Operation, $"{what} is a {row.Kind}, not an operation.");
        Assert.Equal(0, row.Depth);
        Assert.Equal(open, row.IsOpen);
        Assert.Equal(title, row.AutomationName);
        AssertView(row, inView);
        if (row.Bounds is null)
        {
            var model = Assert.IsType<ActivityLogOperationRow>(ModelRow(app, row));
            Assert.Equal((glyph, title, message, PartA.Time, glyph is null ? string.Empty : PartA.Duration), (IconOf(model.Outcome), model.Title, model.Message, model.Time, model.Duration));
            Assert.Equal(messageColour, ColourOf(app, model.MessageTone));
            return;
        }

        Assert.Equal(0, row.Indent!.Value, 0.5);
        Assert.Equal(PartA.OperationRowHeight, row.Bounds!.Value.Height, 0.5);
        Assert.Equal(open ? PartA.ChevronOpen : PartA.ChevronClosed, row.Chevron);
        Assert.Equal(glyph, row.Glyph);
        Assert.Equal(GlyphColour(app, glyph), row.GlyphColor);
        Assert.Equal(PartA.Time, row.Time);
        Assert.Equal(title, row.Title);
        Assert.Equal(message, row.Message);
        Assert.True(messageColour == row.MessageColor, $"{what}'s message is drawn in {row.MessageColor}, expected {messageColour}.");
        Assert.Equal(glyph is null ? string.Empty : PartA.Duration, row.Duration);
        Assert.Equal((Color?)PaletteOf(app).TextSecondary, (((TextBlock)Named(app, row, "ActivityLogTime")).Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>
    /// Asserts a call's row, <c>exit · command · duration</c> (2.2): at <paramref name="depth"/>,
    /// its open state, its exit text, its command (its automation name too), <c>(on a temporary
    /// index)</c> after it or not, and <c>0 ms</c>; whether it is in the list's view (<see
    /// cref="AssertView"/>). While the list draws it: indented 24 per level, 22 high, its chevron,
    /// the exit text in the secondary colour for <c>exit 0</c> and <c>cancelled</c> and the danger
    /// colour for another code, the command in the code font; otherwise what the list holds for it
    /// (AD15).
    /// </summary>
    private static void AssertCallRow(ShellDriver app, ActivityLogRowSnapshot row, string exit, string command, bool open, int depth = 1, bool onTemporaryIndex = false, bool? inView = true)
    {
        var what = $"The row \"{row.AutomationName}\"";
        var palette = PaletteOf(app);
        var exitColour = exit is PartA.Exit0 or PartA.Cancelled ? palette.TextSecondary : palette.Danger;
        Assert.True(row.Kind == ActivityLogRowKind.Call, $"{what} is a {row.Kind}, not a call.");
        Assert.Equal(depth, row.Depth);
        Assert.Equal(open, row.IsOpen);
        Assert.Equal(command, row.AutomationName);
        AssertView(row, inView);
        if (row.Bounds is null)
        {
            var model = Assert.IsType<ActivityLogCallRow>(ModelRow(app, row));
            Assert.Equal((exit, command, onTemporaryIndex, PartA.Duration), (model.ExitText, model.Command, model.OnTemporaryIndex, model.Duration));
            Assert.Equal(exitColour, ColourOf(app, model.ExitTone));
            return;
        }

        Assert.Equal(PartA.IndentStep * depth, row.Indent!.Value, 0.5);
        Assert.Equal(PartA.LineRowHeight, row.Bounds!.Value.Height, 0.5);
        Assert.Equal(open ? PartA.ChevronOpen : PartA.ChevronClosed, row.Chevron);
        Assert.Equal(exit, row.ExitText);
        Assert.True(exitColour == row.ExitColor, $"{what}'s \"{exit}\" is drawn in {row.ExitColor}, expected {exitColour}.");
        Assert.Equal(command, row.Command);
        Assert.Equal(onTemporaryIndex, row.OnTemporaryIndex);
        Assert.Equal(PartA.Duration, row.Duration);
        var commandBlock = (TextBlock)Named(app, row, "ActivityLogCommand");
        Assert.Same(Application.Current!.FindResource("VcCodeFontFamily"), commandBlock.FontFamily);
    }

    /// <summary>
    /// Asserts a line under an opened operation (AD3): its kind and text, and whether it is in the
    /// list's view (<see cref="AssertView"/>); while drawn, indented 24, 22 high, in the danger
    /// colour (the failure's summary) or the secondary one (a note); otherwise what the list holds
    /// for it (AD15).
    /// </summary>
    private static void AssertLineRow(ShellDriver app, ActivityLogRowSnapshot row, ActivityLogRowKind kind, string text, bool? inView = true)
    {
        var what = $"The row \"{row.AutomationName}\"";
        var colour = kind == ActivityLogRowKind.FailureSummary ? PaletteOf(app).Danger : PaletteOf(app).TextSecondary;
        Assert.True(row.Kind == kind, $"{what} is a {row.Kind}, not a {kind}.");
        Assert.Equal(1, row.Depth);
        Assert.Equal(text, row.AutomationName);
        AssertView(row, inView);
        if (row.Bounds is null)
        {
            var model = Assert.IsType<ActivityLogLineRow>(ModelRow(app, row));
            Assert.Equal(text, model.Text);
            Assert.Equal(colour, ColourOf(app, model.Tone));
            return;
        }

        Assert.Equal(PartA.IndentStep, row.Indent!.Value, 0.5);
        Assert.Equal(PartA.LineRowHeight, row.Bounds!.Value.Height, 0.5);
        Assert.Equal(text, row.Text);
        Assert.True(colour == row.TextColor, $"{what} is drawn in {row.TextColor}, expected {colour}.");
    }

    /// <summary>The list's row behind a snapshot's row (the active worktree's, at the same place).</summary>
    private static ActivityLogRow ModelRow(ShellDriver app, ActivityLogRowSnapshot row)
    {
        var rows = app.ActivityLogModel.Current?.Rows ?? throw new Xunit.Sdk.XunitException("The activity log shows no worktree.");
        var model = rows[row.Index];
        Assert.Equal(row.AutomationName, model.AutomationName);
        return model;
    }

    /// <summary>The key of the glyph an outcome draws (2.2), null while running.</summary>
    private static string? IconOf(ActivityLogOutcome outcome) => outcome switch
    {
        ActivityLogOutcome.Succeeded => PartA.IconCheck,
        ActivityLogOutcome.Warning => PartA.IconAlert,
        ActivityLogOutcome.Failed => PartA.IconClose,
        ActivityLogOutcome.Cancelled => PartA.IconMinus,
        _ => null,
    };

    /// <summary>The colour of a tone of 2.2 in the window's theme.</summary>
    private static Color ColourOf(ShellDriver app, ActivityLogTone tone)
    {
        var palette = PaletteOf(app);
        return tone switch
        {
            ActivityLogTone.Primary => palette.TextPrimary,
            ActivityLogTone.Success => palette.Success,
            ActivityLogTone.Warning => palette.Warning,
            ActivityLogTone.Danger => palette.Danger,
            _ => palette.TextSecondary,
        };
    }

    /// <summary>Asserts an opened call's box (2.2): indented under its call, its titled parts in order, each holding what the report gives, and no other part.</summary>
    private static void AssertOutputBox(ActivityLogRowSnapshot row, int depth, params (string Title, IReadOnlyList<string> Lines)[] parts)
    {
        Assert.True(row.Kind == ActivityLogRowKind.Output, $"The row after the opened call is a {row.Kind}, not its box.");
        Assert.True(row.Bounds is not null, "The opened call's box is not drawn.");
        Assert.Equal(depth, row.Depth);
        Assert.Equal(PartA.IndentStep * depth, row.Indent!.Value, 0.5);
        var actual = row.Parts ?? throw new Xunit.Sdk.XunitException("The box shows no parts.");
        Assert.Equal(parts.Select(part => part.Title), actual.Select(part => part.Title));
        for (var index = 0; index < parts.Length; index++)
        {
            Assert.Equal(parts[index].Lines, actual[index].Text.Split('\n'));
        }

        Assert.DoesNotContain(ActivityLogViewModel.NoOutputText, row.Text, StringComparison.Ordinal);
    }

    /// <summary>A named text of a drawn row (its controls carry the names of 2.2's parts).</summary>
    private static Control Named(ShellDriver app, ActivityLogRowSnapshot row, string name)
    {
        var bounds = row.Bounds ?? throw new Xunit.Sdk.XunitException($"The row \"{row.AutomationName}\" is not drawn.");
        return app.ActivityLogView.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Name == name && control.IsEffectivelyVisible)
            .FirstOrDefault(control => bounds.Contains(app.BoundsOf(control).Center))
            ?? throw new Xunit.Sdk.XunitException($"The row \"{row.AutomationName}\" draws no {name}.");
    }

    /// <summary>The rows of the list that are at the top level (operations, and reads under <c>Show reads</c>).</summary>
    private static List<ActivityLogRowSnapshot> TopLevel(ActivityLogSnapshot log) => [.. log.Rows.Where(row => row.Depth == 0)];

    /// <summary>The rows under the operation row at <paramref name="index"/>, down to the next top-level row.</summary>
    private static List<ActivityLogRowSnapshot> Under(ActivityLogSnapshot log, int index) =>
        [.. log.Rows.Skip(index + 1).TakeWhile(row => row.Depth > 0)];

    /// <summary>The index of the (first) top-level row named <paramref name="name"/>.</summary>
    private static int IndexOf(ActivityLogSnapshot log, string name)
    {
        var index = log.Rows.ToList().FindIndex(row => row.Depth == 0 && row.AutomationName == name);
        Assert.True(index >= 0, $"The activity log has no row \"{name}\". It has: {string.Join(", ", log.Rows.Select(row => row.AutomationName))}.");
        return index;
    }

    /// <summary>The newest record of the session's operation log titled <paramref name="title"/>.</summary>
    private static OperationRecord Record(ShellDriver app, string title) =>
        app.Session.Operations.Snapshot().LastOrDefault(record => record.Title == title)
        ?? throw new Xunit.Sdk.XunitException($"The operation log has no record \"{title}\".");

    /// <summary>
    /// The calls of an opened operation (part A's "An operation's calls"): the call rows under it
    /// are exactly the records of the call log that carry the operation's id, in the order of their
    /// sequence numbers, each once. Returns them.
    /// </summary>
    private static List<GitCallRecord> AssertOperationCalls(ShellDriver app, ActivityLogSnapshot log, string title)
    {
        var record = Record(app, title);
        var calls = app.GitCalls().Where(call => call.OperationId == record.Id).OrderBy(call => call.Sequence).ToList();
        var rows = Under(log, IndexOf(log, title)).Where(row => row.Kind == ActivityLogRowKind.Call).Select(row => row.AutomationName).ToList();
        Assert.True(
            rows.SequenceEqual(calls.Select(call => call.CommandText)),
            $"The calls under \"{title}\" are not its calls in the call log, in order.\nRows:\n  {string.Join("\n  ", rows)}\nCall log:\n  {string.Join("\n  ", calls.Select(call => call.CommandText))}");
        Assert.True(calls.Count > 0, $"\"{title}\" has no calls.");
        return calls;
    }

    /// <summary>Asserts that <paramref name="expected"/> are among the commands in this order (a subsequence).</summary>
    private static void AssertInOrder(IReadOnlyList<string> actual, IReadOnlyList<string> expected, string what)
    {
        var at = -1;
        foreach (var command in expected)
        {
            var next = -1;
            for (var index = at + 1; index < actual.Count; index++)
            {
                if (actual[index] == command)
                {
                    next = index;
                    break;
                }
            }

            Assert.True(next > at, $"{what}: \"{command}\" does not follow the commands before it. They are:\n  {string.Join("\n  ", actual)}");
            at = next;
        }
    }

    /// <summary>Clicks the log's row named <paramref name="name"/> (the <paramref name="occurrence"/>-th, in view) and lets the list settle.</summary>
    private static void ClickRow(ShellDriver app, string name, int occurrence = 0)
    {
        app.ClickActivityLogRow(name, occurrence);
        app.Settle();
    }

    /// <summary>The list is at its end (AD15): scrolled as far down as it goes.</summary>
    private static void AssertListAtEnd(ShellDriver app)
    {
        app.Settle();
        var list = app.Find<ScrollViewer>("ActivityLogList");
        Assert.True(list.Offset.Y >= list.Extent.Height - list.Viewport.Height - 1, string.Create(CultureInfo.InvariantCulture, $"The log's list is not at its end: offset {list.Offset.Y}, extent {list.Extent.Height}, viewport {list.Viewport.Height}."));
    }

    // ---- The stage panel ----

    /// <summary>A file list's rows as "letter name folder" (<c>UnstagedFileList</c>, <c>StagedFileList</c>).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>Waits until the stage panel shows with these titles over its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle
                && app.StagePanelSettled,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>The text a box holds (<c>CommitSummary</c>, <c>CommitDescription</c>).</summary>
    private static string BoxText(ShellDriver app, string automationId) => ((TextBox)app.FindByAutomationId(automationId)).Text ?? string.Empty;

    // ---- The graph's selection ----

    /// <summary>Nothing is selected: no commit, not the working-changes row, and the details panel's placeholder.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var graph = Graph(app);
        Assert.Equal(-1, graph.SelectedIndex);
        Assert.False(graph.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.True(graph.Selection.IsEmpty, "Commits are selected: " + string.Join(", ", graph.Selection.Shas));
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
        Assert.Equal(PartA.NothingSelected, placeholder.Text);
    }

    /// <summary>The working-changes row alone is selected.</summary>
    private static void AssertWorkingRowSelected(ShellDriver app)
    {
        app.Settle();
        var graph = Graph(app);
        Assert.True(graph.IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal(-1, graph.SelectedIndex);
    }

    /// <summary>The keyboard focus is on the graph's rows (<c>CommitGraphControl</c>).</summary>
    private static void AssertFocusInGraph(ShellDriver app)
    {
        app.Settle();
        var focused = app.Window.FocusManager?.GetFocusedElement();
        Assert.True(ReferenceEquals(focused, Graph(app)), $"The keyboard focus is on {focused?.GetType().Name ?? "nothing"}, not the graph.");
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output, line ends as line feeds, without its last line feed.</summary>
    private static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Plain git's output as lines (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = await GitTextAsync(repo, arguments);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>What a revision names (<c>git rev-parse --verify</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>A ref's reflog, newest first: its first <paramref name="count"/> messages.</summary>
    private static Task<string[]> ReflogAsync(TempRepo repo, string reference, int count) =>
        GitLinesAsync(repo, "log", "--walk-reflogs", $"-{count.ToString(CultureInfo.InvariantCulture)}", "--format=%gs", reference);

    /// <summary>The worktree's key (2.3): its own git folder, in full.</summary>
    private static async Task<string> WorktreeKeyAsync(TempRepo repo) =>
        WorktreeKey.Of(await GitTextAsync(repo, "rev-parse", "--absolute-git-dir"));

    /// <summary>A working-tree file's text, as git wrote it (line ends as line feeds).</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);
}
