// What part K's checks share (docs/test-reports/phase-6.md, "Part K: the command palette and the
// keys", its conventions): keys pressed as literal gestures with the platform's command modifier
// (KD9), the texts in the platform's words, the palette's rows as drawn and in its view model, the
// menus' key column, tooltips with their help texts and accelerator keys, the focus and its
// region, the log's palette lines, the call log's "no write", and git's own reads of the
// scenarios. The checks themselves are in Phase6Checks.K.cs (K10), Phase6Checks.K.Keys.cs (K11,
// K12) and Phase6Checks.K.Dashboard.cs (K13).
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksK
{
    // ---- Keys as literal gestures (KD9) in the platform's words. ----

    /// <summary>Whether the platform's command modifier is Cmd (macOS): the keys pressed and the texts follow it (design 6.4); headless it is Control everywhere (0.7).</summary>
    private static bool CommandIsMeta(ShellDriver app) => app.CommandModifier.HasFlag(KeyModifiers.Meta);

    /// <summary>The platform's command modifier as raw input modifiers: Control headless, Meta where the platform names it.</summary>
    private static RawInputModifiers Mod(ShellDriver app) => CommandIsMeta(app) ? RawInputModifiers.Meta : RawInputModifiers.Control;

    /// <summary>Presses Mod+<paramref name="key"/> (with Shift when asked) as one literal gesture.</summary>
    private static void PressMod(ShellDriver app, Key key, bool shift = false) =>
        app.PressKey(key, Mod(app) | (shift ? RawInputModifiers.Shift : RawInputModifiers.None));

    /// <summary>Mod+Shift+P, the palette's key (6.1's row 1), pressed literally.</summary>
    private static void PressPaletteKey(ShellDriver app) => PressMod(app, Key.P, shift: true);

    /// <summary>A key with Control itself held (Ctrl+Tab, Ctrl+PageDown: Control on every platform, 6.5).</summary>
    private static void PressControl(ShellDriver app, Key key, bool shift = false) =>
        app.PressKey(key, RawInputModifiers.Control | (shift ? RawInputModifiers.Shift : RawInputModifiers.None));

    /// <summary><paramref name="text"/>, written in the Windows and Linux words, in the platform's (<see cref="PartK.OnPlatform"/>).</summary>
    private static string Words(ShellDriver app, string text) => PartK.OnPlatform(text, CommandIsMeta(app));

    private static string? WordsOrNull(ShellDriver app, string? text) => PartK.OnPlatformOrNull(text, CommandIsMeta(app));

    // ---- The log. ----

    /// <summary>A log line's message: what follows its time and level.</summary>
    private static string Message(string line) => LogLine().Match(line) is { Success: true } match ? match.Groups["message"].Value : line;

    /// <summary>A log line's level word (<c>DBG</c>, <c>INF</c>, <c>WRN</c>, <c>ERR</c>).</summary>
    private static string Level(string line) => LogLine().Match(line) is { Success: true } match ? match.Groups["level"].Value : string.Empty;

    [GeneratedRegex(@"^.*?\[(?<level>[A-Z]{3})\] (?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex LogLine();

    /// <summary>The messages of the log's lines since <paramref name="mark"/> that hold <paramref name="text"/>.</summary>
    private static List<string> Messages(ShellDriver app, int mark, string text) =>
        [.. app.LogLinesSince(mark).Where(line => line.Contains(text, StringComparison.Ordinal)).Select(Message)];

    /// <summary>Asserts that the palette's log lines since <paramref name="mark"/> are exactly <paramref name="expected"/>, each at Debug (14.14).</summary>
    private static void AssertPaletteLines(ShellDriver app, int mark, params string[] expected)
    {
        var lines = app.LogLinesSince(mark).Where(line => line.Contains("Command palette ", StringComparison.Ordinal)).ToList();
        Assert.True(
            lines.Select(Message).SequenceEqual(expected, StringComparer.Ordinal),
            "The palette's log lines are not the expected ones.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", lines));
        Assert.All(lines, line => Assert.Equal("DBG", Level(line)));
    }

    /// <summary>The number of <c>Command palette ran:</c> lines since <paramref name="mark"/>.</summary>
    private static int RanLines(ShellDriver app, int mark) => Messages(app, mark, PartK.PaletteRanText).Count;

    /// <summary>The messages since <paramref name="mark"/> at Warning or Error.</summary>
    private static List<string> WarningsAndErrors(ShellDriver app, int mark) =>
        [.. app.LogLinesSince(mark).Where(line => Level(line) is "WRN" or "ERR")];

    // ---- The call log. ----

    /// <summary>Whether a call writes (part K's "No write"): it ran in an operation, or its command is one of git's writes (<see cref="PartK.WritePrefixes"/>).</summary>
    private static bool IsWrite(GitCallRecord call) => call.OperationId is not null || IsWriteCommand(call);

    /// <summary>Whether a call's command is one of git's writes (<see cref="PartK.WritePrefixes"/>): an operation's own write among its reads.</summary>
    private static bool IsWriteCommand(GitCallRecord call) =>
        PartK.WritePrefixes.Any(prefix => call.CommandText.StartsWith(prefix, StringComparison.Ordinal)) || PartK.IsConfigWrite(call.CommandText);

    /// <summary>Asserts that no call since <paramref name="sequence"/> wrote, and that no operation ended since <paramref name="operations"/> (the operation log's count).</summary>
    private static void AssertNoWrite(ShellDriver app, long sequence, int? operations = null, string? what = null)
    {
        var writes = app.CallsSince(sequence).Where(IsWrite).ToList();
        Assert.True(writes.Count == 0, $"{what ?? "The step"} wrote: " + string.Join("\n", writes.Select(call => $"{call.CommandText} (operation {call.OperationId ?? "none"})")));
        if (operations is { } count)
        {
            var records = app.Session.Operations.Snapshot();
            Assert.True(records.Count == count, $"{what ?? "The step"} ran an operation: " + string.Join(", ", records.Skip(count).Select(record => record.Title)));
        }
    }

    /// <summary>Asserts that git was not called since <paramref name="sequence"/> at all.</summary>
    private static void AssertNoCall(ShellDriver app, long sequence, string what)
    {
        var calls = app.CallsSince(sequence);
        Assert.True(calls.Count == 0, $"{what}: git was called:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
    }

    /// <summary>The operation log's records since <paramref name="count"/> records.</summary>
    private static List<VisualCommit.Core.Operations.OperationRecord> OperationsSince(ShellDriver app, int count) =>
        [.. app.Session.Operations.Snapshot().Skip(count)];

    /// <summary>
    /// The one operation <paramref name="title"/> since <paramref name="count"/> records and its calls:
    /// it succeeded, and among its calls <paramref name="command"/> exited 0; returns that call.
    /// </summary>
    private static (VisualCommit.Core.Operations.OperationRecord Record, GitCallRecord Call, List<GitCallRecord> Calls) AssertOperation(ShellDriver app, int count, string title, string command)
    {
        var records = OperationsSince(app, count);
        var record = Assert.Single(records, candidate => candidate.Title == title);
        Assert.Equal(VisualCommit.Core.Operations.OperationOutcome.Succeeded, record.Outcome);
        var calls = app.GitCalls().Where(call => call.OperationId == record.Id).ToList();
        var call = calls.LastOrDefault(candidate => candidate.CommandText == command)
            ?? throw new Xunit.Sdk.XunitException($"The operation {title} did not run \"{command}\". It ran:\n" + string.Join('\n', calls.Select(candidate => candidate.CommandText)));
        Assert.True(call.Succeeded, $"\"{command}\" exited {call.ExitCode}:\n{call.StandardError}");
        return (record, call, calls);
    }

    /// <summary>A stream's lines, split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    // ---- Git's own reads of the scenarios (the check's, with GIT_OPTIONAL_LOCKS=0). ----

    private static readonly IReadOnlyDictionary<string, string?> OptionalLocksOff = new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" };

    /// <summary>Plain git's standard output as lines (exit 0 asserted).</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var result = await repo.GitWithEnvironmentAllowingFailureAsync(OptionalLocksOff, arguments);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} exited {result.ExitCode}.\n{result.StandardError}");
        return [.. result.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
    }

    /// <summary>What a revision names, or null.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitWithEnvironmentAllowingFailureAsync(OptionalLocksOff, "rev-parse", "--verify", "--quiet", revision);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    /// <summary><c>git status --porcelain=v1 --branch</c>: the branch line and the files.</summary>
    private static Task<List<string>> StatusAsync(TempRepo repo) => GitLinesAsync(repo, "status", "--porcelain=v1", "--branch");

    /// <summary>The stash list's messages (<c>git stash list --format=%gs</c>).</summary>
    private static Task<List<string>> StashListAsync(TempRepo repo) => GitLinesAsync(repo, "stash", "list", "--format=%gd: %gs");

    // ---- The palette. ----

    /// <summary>The palette's rows that lie whole in its list's view (5.3: rows 143 to 479), top to bottom.</summary>
    private static List<PaletteRow> RowsInView(PaletteSnapshot snapshot) =>
        [.. snapshot.Rows.Where(row => row.Bounds.Top >= PartK.ListTop - 0.5 && row.Bounds.Bottom <= PartK.ListTop + (PartK.RowHeight * PartK.RowsInView) + 0.5).OrderBy(row => row.Bounds.Top)];

    /// <summary>A palette row as the report writes it, from the snapshot.</summary>
    private static string Described(PaletteRow row) =>
        new PartK.Entry(row.Label, row.Detail, row.KeyText, row.IsEnabled ? null : row.Reason ?? "(no reason)").ToString()
        + (row.IsHighlighted ? " (highlighted)" : string.Empty);

    /// <summary>A palette entry of the view model as the report writes it.</summary>
    private static string Described(PaletteEntry entry) =>
        new PartK.Entry(entry.Label, entry.Detail, entry.KeyText, entry.IsEnabled ? null : entry.DisabledReason ?? "(no reason)", entry.ShowsDanger).ToString();

    /// <summary>
    /// Asserts the palette's view model's matches (<c>Shown</c>) from the top: their first
    /// <paramref name="expected"/>.Length entries are exactly these (label, detail, key, enabled or
    /// its reason, danger), and when <paramref name="count"/> is given, that many in all.
    /// </summary>
    private static void AssertShown(ShellDriver app, IReadOnlyList<PartK.Entry> expected, int? count = null, int from = 0)
    {
        var shown = app.Palette.Shown.ToList();
        var actual = shown.Skip(from).Take(expected.Count).Select(Described).ToList();
        var want = expected.Select(entry => WordsEntry(app, entry).ToString()).ToList();
        Assert.True(
            actual.SequenceEqual(want, StringComparer.Ordinal),
            "The palette's matches are not the expected ones.\nExpected:\n  " + string.Join("\n  ", want) + "\nActual:\n  " + string.Join("\n  ", shown.Select(Described)));
        if (count is { } all)
        {
            Assert.True(shown.Count == all, $"The palette shows {shown.Count} matches, not {all}:\n  " + string.Join("\n  ", shown.Select(Described)));
        }
    }

    /// <summary>An expected entry with its key in the platform's words.</summary>
    private static PartK.Entry WordsEntry(ShellDriver app, PartK.Entry entry) => entry with { Key = WordsOrNull(app, entry.Key) };

    /// <summary>
    /// Asserts the palette's rows in view, top to bottom, as drawn (5.3, 5.7, part K's "The
    /// palette"): each row's label, detail (a disabled entry's item status), key, enabled or its
    /// reason in the detail's place, and the highlighted one; each row centred at y 157 + 28 k ±2
    /// (k from the top of the view), 28 high, x 259 to 841; its label from x 269 ±2 at 12.5 in the
    /// primary colour (the danger colour for an enabled destructive entry, the disabled colour for
    /// a disabled one), its detail 8 after the label and its key right-aligned at x 831 ±2, both at
    /// 11.5 in the secondary colour (disabled: the disabled colour); a row without a key has no key
    /// column and its detail runs to x 831 (the lead's ruling 4). With <paramref name="picture"/>, the
    /// highlighted row's background is <c>VcSelectionBrush</c> in its padding (5 into it, clear of its text) and the others' are not.
    /// Returns the rows.
    /// </summary>
    private static List<PaletteRow> AssertRowsInView(ShellDriver app, IReadOnlyList<PartK.Entry> expected, int highlighted, Screenshot? picture = null)
    {
        var snapshot = app.PaletteSnapshot();
        var rows = RowsInView(snapshot);
        var want = expected.Select((entry, index) => (WordsEntry(app, entry) with { Danger = false }).ToString() + (index == highlighted ? " (highlighted)" : string.Empty)).ToList();
        Assert.True(
            rows.Select(Described).SequenceEqual(want, StringComparer.Ordinal),
            "The palette's rows in view are not the expected ones.\nExpected:\n  " + string.Join("\n  ", want) + "\nActual:\n  " + string.Join("\n  ", rows.Select(Described)));
        var palette = PaletteOf(app);
        var list = (ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList");
        for (var k = 0; k < rows.Count; k++)
        {
            var row = rows[k];
            var entry = expected[k];
            AssertNear(PartK.RowCentreY(k), row.Bounds.Center.Y, PartK.Tolerance, $"Row {k} ({row.Label})'s centre");
            AssertNear(PartK.RowHeight, row.Bounds.Height, 0.5, $"Row {k} ({row.Label})'s height");
            AssertNear(PartK.BoxBounds.Left, row.Bounds.Left, 0.5, $"Row {k} ({row.Label})'s left");
            AssertNear(PartK.BoxBounds.Right, row.Bounds.Right, 0.5, $"Row {k} ({row.Label})'s right");
            var container = list.GetRealizedContainers().OfType<ListBoxItem>().First(item => item.DataContext is PaletteEntry candidate && candidate.Label == row.Label && candidate.Detail == (entry.Detail ?? candidate.Detail) && Math.Abs(app.BoundsOf(item).Top - row.Bounds.Top) < 0.5);
            var parts = container.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name is not null).ToDictionary(block => block.Name!, block => block);
            var label = parts["CommandPaletteItemLabel"];
            var detail = parts["CommandPaletteItemDetail"];
            var key = parts["CommandPaletteItemKey"];
            var labelColour = !entry.IsEnabled ? palette.TextDisabled : entry.Danger ? palette.Danger : palette.TextPrimary;
            var secondary = entry.IsEnabled ? palette.TextSecondary : palette.TextDisabled;
            Assert.Equal(labelColour, row.LabelColour);
            Assert.Equal((PartK.LabelSize, PartK.DetailSize, PartK.DetailSize), (label.FontSize, detail.FontSize, key.FontSize));
            Assert.Equal(secondary, (detail.Foreground as ISolidColorBrush)?.Color);
            AssertNear(PartK.LabelLeft, app.BoundsOf(label).Left, PartK.Tolerance, $"Row {k} ({row.Label})'s label's left");
            if (!string.IsNullOrEmpty(detail.Text))
            {
                AssertNear(app.BoundsOf(label).Right + PartK.DetailGap, app.BoundsOf(detail).Left, PartK.Tolerance, $"Row {k} ({row.Label})'s detail's left");
            }

            if (entry.Key is not null)
            {
                Assert.True(key.IsEffectivelyVisible, $"Row {k} ({row.Label}) shows no key.");
                Assert.Equal(secondary, (key.Foreground as ISolidColorBrush)?.Color);
                AssertNear(PartK.ContentRight, app.BoundsOf(key).Right, PartK.Tolerance, $"Row {k} ({row.Label})'s key's right");
            }
            else
            {
                // No key column (the lead's ruling 4): the key hidden, the detail running to the row's padding.
                Assert.False(key.IsVisible, $"Row {k} ({row.Label}) has a key column without a key.");
                AssertNear(PartK.ContentRight, app.BoundsOf(detail).Right, PartK.Tolerance, $"Row {k} ({row.Label})'s detail's right (no key column)");
            }

            if (picture is not null)
            {
                var background = picture.PixelAt(new Point(row.Bounds.Left + 5, row.Bounds.Center.Y));
                if (k == highlighted)
                {
                    Assert.True(background == palette.Selection, $"The highlighted row {k} ({row.Label}) is drawn on {background}, not VcSelectionBrush {palette.Selection}.");
                }
                else
                {
                    Assert.True(background != palette.Selection, $"Row {k} ({row.Label}) is drawn on VcSelectionBrush but is not highlighted.");
                }
            }
        }

        Assert.Equal(highlighted, rows.FindIndex(row => row.IsHighlighted));
        return rows;
    }

    /// <summary>The palette's realised row of <paramref name="label"/> (and <paramref name="detail"/>), its <c>CommandPaletteItem</c>.</summary>
    private static ListBoxItem PaletteItem(ShellDriver app, string label, string? detail = null)
    {
        var list = (ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList");
        return list.GetRealizedContainers().OfType<ListBoxItem>()
            .FirstOrDefault(item => item.DataContext is PaletteEntry entry && entry.Label == label && (detail is null || entry.Detail == detail))
            ?? throw new Xunit.Sdk.XunitException($"The palette shows no row \"{label}\".");
    }

    /// <summary>Asserts a palette row's UI Automation (5.11): its id, Name the label, ItemStatus the detail, HelpText a disabled entry's reason, AcceleratorKey the key, selected while highlighted.</summary>
    private static void AssertRowAutomation(ShellDriver app, ListBoxItem item, string name, string? itemStatus, string? helpText, string? key, bool selected)
    {
        Assert.Equal("CommandPaletteItem", AutomationProperties.GetAutomationId(item));
        Assert.Equal(name, AutomationProperties.GetName(item));
        Assert.Equal(itemStatus, NullIfEmpty(AutomationProperties.GetItemStatus(item)));
        Assert.Equal(helpText, NullIfEmpty(AutomationProperties.GetHelpText(item)));
        Assert.Equal(WordsOrNull(app, key), NullIfEmpty(AutomationProperties.GetAcceleratorKey(item)));
        Assert.Equal(selected, item.IsSelected);
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    /// <summary>
    /// Asserts the palette's card and box where 5.3 puts them at 1100×700: the card x 250 to 850 from
    /// y 96, <paramref name="rows"/> rows high (56 + 28 × rows), the box x 259 to 841, y 105 to 137.
    /// </summary>
    private static void AssertCard(PaletteSnapshot snapshot, int rows)
    {
        AssertNear(new Rect(PartK.CardLeft, PartK.CardTop, PartK.CardRight - PartK.CardLeft, PartK.CardHeight(rows)), snapshot.CardBounds, 0.5, "The palette's card");
        AssertNear(PartK.BoxBounds, snapshot.BoxBounds, 0.5, "The palette's box");
    }

    /// <summary>The palette's list's vertical offset.</summary>
    private static double ListOffset(ShellDriver app)
    {
        var list = (ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList");
        var viewer = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        return viewer.Offset.Y;
    }

    /// <summary>Waits until the palette's opening's worktree read is in the call log (5.5), then asserts it is the only call since <paramref name="sequence"/> and no write (K10 step 2).</summary>
    private static async Task AssertOnlyTheWorktreeReadAsync(ShellDriver app, long sequence)
    {
        await app.WaitForAsync(() => app.CallsSince(sequence).Any(call => call.CommandText == PartK.WorktreeList), "the palette's worktree read (git worktree list --porcelain) in the call log");
        await app.Session.Shell.PaletteWorktreeRead;
        app.Settle();
        var calls = app.CallsSince(sequence);
        Assert.True(
            calls.Count == 1 && calls[0].CommandText == PartK.WorktreeList && calls[0].OperationId is null,
            "Since the palette's key git ran more than its worktree read:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
    }

    // ---- Menus with their keys (6.7). ----

    /// <summary>
    /// Asserts an open menu's key column (part K's "Menus"): the lines with their keys
    /// (<see cref="MenuSnapshot.LinesWithKeys"/>); each item's <c>Header</c> its label alone; each
    /// keyed item's key a <c>ContextMenuItemKey</c> at 12.5 right-aligned 10 before the item's right
    /// edge (±2) and at least 24 right of its label, in the secondary colour (disabled: the disabled
    /// colour), also its <c>AcceleratorKey</c>; an item without a key has neither; the menu's height
    /// by 4a (±2) and its width <paramref name="width"/> ±3.
    /// </summary>
    private static void AssertKeyedMenu(ShellDriver app, MenuSnapshot menu, IReadOnlyList<string> lines, double? width)
    {
        var want = lines.Select(line => Words(app, line)).ToList();
        var actual = menu.LinesWithKeys();
        Assert.True(
            actual.SequenceEqual(want, StringComparer.Ordinal),
            "The menu is not the expected one.\nExpected:\n  " + string.Join("\n  ", want) + "\nActual:\n  " + string.Join("\n  ", actual));
        AssertMenuShape(app, menu);
        var (items, separators) = PartK.MenuCounts(lines);
        AssertNear(MenuHeight(items, separators), menu.Bounds.Height, 2, "The menu's height");
        if (width is { } expectedWidth)
        {
            AssertNear(expectedWidth, menu.Bounds.Width, PartK.MenuWidthTolerance, "The menu's width");
        }

        var palette = PaletteOf(app);
        foreach (var entry in menu.Items)
        {
            var item = app.MenuItemControl(entry.Label);
            Assert.Equal(entry.Label, item.Header as string);
            var key = ShellDriver.MenuKeyBlockOf(item);
            var accelerator = NullIfEmpty(AutomationProperties.GetAcceleratorKey(item));
            if (entry.KeyText is null)
            {
                Assert.Null(key);
                Assert.True(accelerator is null, $"The item \"{entry.Label}\" has no key but the AcceleratorKey {accelerator}.");
                continue;
            }

            Assert.NotNull(key);
            Assert.Equal(entry.KeyText, accelerator);
            Assert.Equal("ContextMenuItemKey", AutomationProperties.GetAutomationId(key));
            Assert.Equal(PartK.LabelSize, key!.FontSize);
            Assert.Equal(entry.IsEnabled ? palette.TextSecondary : palette.TextDisabled, (key.Foreground as ISolidColorBrush)?.Color);
            var itemBounds = app.BoundsOf(item);
            var keyBounds = app.BoundsOf(key);
            AssertNear(itemBounds.Right - PartK.MenuKeyRightInset, keyBounds.Right, PartK.Tolerance, $"The key of \"{entry.Label}\"'s right edge");
            var label = item.GetVisualDescendants().OfType<TrimmedTextBlock>().First(block => block.IsEffectivelyVisible);
            var labelRight = app.BoundsOf(label).Left + label.DesiredSize.Width;
            Assert.True(keyBounds.Left - labelRight >= PartK.MenuKeyGap - 0.5, string.Create(CultureInfo.InvariantCulture, $"The key of \"{entry.Label}\" starts {keyBounds.Left - labelRight} right of its label, less than 24."));
            if (!entry.IsEnabled)
            {
                Assert.Equal(palette.TextDisabled, (label.Foreground as ISolidColorBrush)?.Color ?? entry.TextColor);
            }
        }
    }

    // ---- Tooltips, help texts and accelerator keys (6.8). ----

    /// <summary>
    /// Asserts a control's tooltip (<c>ToolTip.GetTip</c>), help text (the same, 6.8) and
    /// <c>AcceleratorKey</c> as <paramref name="expected"/> gives them, and whether it is enabled.
    /// </summary>
    private static void AssertKeyed(ShellDriver app, Control control, PartK.Keyed expected)
    {
        var tip = WordsOrNull(app, expected.ToolTip);
        Assert.True(control.IsEffectivelyEnabled == expected.Enabled, $"{expected.Id} is {(control.IsEffectivelyEnabled ? "enabled" : "disabled")}, expected {(expected.Enabled ? "enabled" : "disabled")}.");
        Assert.True(ToolTip.GetTip(control) as string == tip, $"{expected.Id}'s tooltip is \"{ToolTip.GetTip(control)}\", expected \"{tip}\".");
        Assert.True(NullIfEmpty(ShellDriver.HelpTextOf(control)) == tip, $"{expected.Id}'s help text is \"{ShellDriver.HelpTextOf(control)}\", expected \"{tip}\".");
        var key = WordsOrNull(app, expected.Key);
        Assert.True(NullIfEmpty(ShellDriver.AcceleratorKeyOf(control)) == key, $"{expected.Id}'s AcceleratorKey is \"{ShellDriver.AcceleratorKeyOf(control)}\", expected \"{key}\".");
    }

    /// <summary><see cref="AssertKeyed(ShellDriver, Control, PartK.Keyed)"/> for the window's control of the id <see cref="PartK.Keyed.Id"/>.</summary>
    private static void AssertKeyed(ShellDriver app, PartK.Keyed expected) => AssertKeyed(app, app.FindByAutomationId(expected.Id), expected);

    /// <summary>
    /// Asserts the open tooltip under the pointer (<paramref name="owner"/>'s) in the app's colours
    /// (11.12; part K's "Tooltips"): <c>#1A1D24</c> background, <c>#323845</c> border and the
    /// primary text's ink in the dark theme, its text <paramref name="text"/>, below the owner.
    /// </summary>
    private static void AssertToolTipLook(ShellDriver app, Control owner, string text, Screenshot picture)
    {
        Assert.True(ToolTip.GetIsOpen(owner), $"The tooltip of {owner.Name ?? AutomationProperties.GetAutomationId(owner)} is not open.");
        Assert.Equal(text, ToolTip.GetTip(owner) as string);
        var palette = PaletteOf(app);
        var tip = app.Window.GetVisualDescendants().OfType<ToolTip>().Single(candidate => candidate.IsEffectivelyVisible);
        Assert.Equal(palette.Panel, (tip.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.TextPrimary, (tip.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Border, (tip.BorderBrush as ISolidColorBrush)?.Color);
        var bounds = app.BoundsOf(tip);
        AssertColour(palette.Panel, picture, new Point(Math.Ceiling(bounds.X) + 3, bounds.Center.Y), "tooltip's background (VcPanelBackgroundBrush, 11.12)");
        var area = new Rect(Math.Ceiling(bounds.X), Math.Ceiling(bounds.Y), Math.Floor(bounds.Width) - 1, Math.Floor(bounds.Height) - 1);
        var problems = TextInk.Problems(picture, area, palette.TextPrimary, palette.Panel, Colors.White, "the tooltip's text");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        var texts = tip.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).ToList();
        Assert.Contains(text, texts);
    }

    // ---- The focus. ----

    /// <summary>Asserts the focused element's automation id (or the nearest ancestor's) and its region (6.6).</summary>
    private static void AssertFocus(ShellDriver app, string automationId, KeyRegionRole region)
    {
        app.Settle();
        Assert.True(app.FocusedAutomationId() == automationId, $"The focus is on {app.FocusedAutomationId() ?? "nothing"} ({app.FocusedElement?.GetType().Name}), not {automationId}.");
        Assert.Equal(region, app.FocusedRegion());
    }

    /// <summary>Whether the focused element shows the keyboard's focus frame (<c>:focus-visible</c>, moved as by Tab).</summary>
    private static bool FocusVisible(ShellDriver app) => app.FocusedElement is StyledElement element && element.Classes.Contains(":focus-visible");

    // ---- The window's state. ----

    /// <summary>The tabs' titles in the strip's order.</summary>
    private static List<string> TabTitles(ShellDriver app) => [.. app.Session.Shell.Tabs.Select(tab => tab.Title)];

    /// <summary>The active tab's title.</summary>
    private static string ActiveTitle(ShellDriver app) => app.Session.Shell.ActiveTab.Title;

    /// <summary>The active tab <c>session.json</c> holds, read back by the store.</summary>
    private static int SavedActiveTab(TempDirectory data) => new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current.ActiveTab;

    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repository(ShellDriver app) =>
        app.Session.Shell.ActiveTab.Repository ?? throw new Xunit.Sdk.XunitException($"The active tab {ActiveTitle(app)} has no repository.");

    /// <summary>The graph's selected SHAs.</summary>
    private static IReadOnlyList<string> Selected(ShellDriver app) => Repository(app).Selection.Shas;

    /// <summary>Waits until the details panel shows <paramref name="sha"/> and <paramref name="subject"/>.</summary>
    private static Task WaitForDetailsAsync(ShellDriver app, string sha, string subject) =>
        app.WaitForAsync(
            () => Shown(app, "DetailsSha") is SelectableTextBlock shown && shown.Text == sha && Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject,
            $"the details panel to show {sha[..7]} {subject}");

    /// <summary>The window's shown control of <paramref name="automationId"/>, or null.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>The undo and redo buttons as part K writes them, with their keys in the platform's words.</summary>
    private static void AssertUndoRedo(ShellDriver app, string? undo, string? redo)
    {
        var expected = new UndoRedoButtons(
            undo is not null,
            undo is null ? PartK.NothingToUndo : Words(app, PartK.UndoToolTip(undo)),
            redo is not null,
            redo is null ? PartK.NothingToRedo : Words(app, PartK.RedoToolTip(redo)));
        Assert.Equal(expected, app.UndoRedoState());
    }

    /// <summary>The left panel's row model at <paramref name="path"/>, as a branch's row.</summary>
    private static RefItemRow RefItem(ShellDriver app, params string[] path) => (RefItemRow)app.RefRowModel(path);

    /// <summary>The colour the left panel draws a branch row's name in.</summary>
    private static async Task<Color?> RefNameColourAsync(ShellDriver app, params string[] path)
    {
        var row = await app.RefRowAsync(path);
        var name = row.GetVisualDescendants().Prepend(row).OfType<TextBlock>().FirstOrDefault(block => block.Name == "RefName")
            ?? row.GetVisualAncestors().OfType<Control>().First().GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "RefName");
        return (name.Foreground as ISolidColorBrush)?.Color;
    }

    /// <summary>The stage panel's title texts and the commit button.</summary>
    private static (string? Unstaged, string? Staged, Button Commit) StagePanel(ShellDriver app) =>
        ((Shown(app, "UnstagedTitle") as TextBlock)?.Text, (Shown(app, "StagedTitle") as TextBlock)?.Text, (Button)app.FindByAutomationId("CommitButton"));

    /// <summary>The commit button's text as shown.</summary>
    private static string? ButtonText(Button button) =>
        button.Content as string ?? button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.IsEffectivelyVisible)?.Text;

    /// <summary>Asserts <paramref name="actual"/> within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
    private static void AssertNear(double expected, double actual, double tolerance, string what) =>
        Assert.True(Math.Abs(actual - expected) <= tolerance, string.Create(CultureInfo.InvariantCulture, $"{what} is {actual}, expected {expected} ±{tolerance}."));

    /// <summary>Asserts a rectangle within <paramref name="tolerance"/> of <paramref name="expected"/> at each edge.</summary>
    private static void AssertNear(Rect expected, Rect actual, double tolerance, string what) =>
        Assert.True(
            Math.Abs(actual.Left - expected.Left) <= tolerance && Math.Abs(actual.Top - expected.Top) <= tolerance
                && Math.Abs(actual.Right - expected.Right) <= tolerance && Math.Abs(actual.Bottom - expected.Bottom) <= tolerance,
            string.Create(CultureInfo.InvariantCulture, $"{what} is at {actual}, expected {expected} ±{tolerance}."));

    /// <summary>
    /// The pixels of <paramref name="after"/> that differ from <paramref name="before"/> outside
    /// <paramref name="excluded"/>, every second pixel each way: "around the card the window shows as
    /// it did before" (K10 step 2, no dimming).
    /// </summary>
    private static List<string> DifferencesOutside(Screenshot before, Screenshot after, Rect excluded)
    {
        var found = new List<string>();
        for (var y = 0; y < Math.Min(before.Height, after.Height); y += 2)
        {
            for (var x = 0; x < Math.Min(before.Width, after.Width); x += 2)
            {
                if (excluded.Contains(new Point(x, y)))
                {
                    continue;
                }

                var was = before.PixelAt(x, y);
                var now = after.PixelAt(x, y);
                if (was != now && found.Count < 20)
                {
                    found.Add(string.Create(CultureInfo.InvariantCulture, $"({x}, {y}) {was} → {now}"));
                }
            }
        }

        return found;
    }

    /// <summary>The window's text block of <paramref name="automationId"/>'s text.</summary>
    private static string? TextOf(ShellDriver app, string automationId) => app.FindByAutomationId(automationId) switch
    {
        TextBlock block => block.Text,
        TextBox box => box.Text,
        _ => null,
    };

    /// <summary>The <c>workspaces.json</c> paths of the workspace <paramref name="id"/>, read as text.</summary>
    private static List<string> WorkspacePathsOf(TempDirectory data, string id)
    {
        var file = new AppPaths(data.Path).WorkspacesFile;
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = JsonDocument.Parse(stream);
        var workspace = json.RootElement.GetProperty("workspaces").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == id);
        return [.. workspace.GetProperty("repositories").EnumerateArray().Select(repository => repository.GetProperty("path").GetString()!)];
    }

    // ---- Dashboard rows as the report writes them. ----

    private static DashboardTone ToneOf(string tone) => tone switch
    {
        PartK.Primary => DashboardTone.Primary,
        PartK.Secondary => DashboardTone.Secondary,
        PartK.Warning => DashboardTone.Warning,
        PartK.Danger => DashboardTone.Danger,
        _ => throw new ArgumentException($"\"{tone}\" is no tone of the report.", nameof(tone)),
    };

    private static ExpectedCell Expected(PartK.Cell cell) =>
        cell.IsEmpty && cell.ToolTip is null ? ExpectedCell.Empty : new ExpectedCell(cell.Text, ToneOf(cell.Tone), cell.ToolTip, cell.Name);

    private static ExpectedRow Expected(PartK.Row row) =>
        new(row.Branch, Expected(row.AheadBehind), Expected(row.Changes), Expected(row.State), Expected(row.Operation));

    /// <summary>
    /// Asserts a dashboard's rows (the active one's, or <paramref name="snapshot"/>'s): their names in
    /// <paramref name="rows"/>' order, each row's cells as the report writes them (<see cref="Phase6Checks.AssertRow"/>),
    /// the <paramref name="selected"/> row selected and no other.
    /// </summary>
    private static void AssertRows(ShellDriver app, IReadOnlyList<PartK.Row> rows, string? selected = null, DashboardSnapshot? snapshot = null)
    {
        var dashboard = snapshot ?? app.DashboardSnapshot();
        Assert.Equal(rows.Select(row => row.Name), dashboard.Rows.Select(row => row.Name));
        Assert.Equal(selected, dashboard.SelectedRow);
        foreach (var row in rows)
        {
            AssertRow(app, row.Name, Expected(row) with { Selected = row.Name == selected }, dashboard);
        }
    }
}
