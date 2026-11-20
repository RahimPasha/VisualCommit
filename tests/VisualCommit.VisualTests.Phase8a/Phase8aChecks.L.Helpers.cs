// Part L's helpers (docs/test-reports/phase-8a.md, part L's conventions): how a colour is read, a
// text box's placeholder and typed text with the caret's column left out, a status letter's and a
// ref name's ink, the stage panel's wait, and the dialogs the steps pass through.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

public sealed partial class Phase8aChecksL
{
    /// <summary>The colours of the theme the window shows.</summary>
    private static L.Colours ColoursOf(ShellDriver app) => app.ShowsDarkTheme ? L.Dark : L.Light;

    /// <summary>The colour a token replaced, where the round changed it in this theme (dark); null where it kept it (light).</summary>
    private static Color? ReplacedIn(ShellDriver app, Color replaced) => app.ShowsDarkTheme ? replaced : null;

    // ---- Colours read from a picture and from the tree --------------------------------------------

    /// <summary>A small text's ink (<see cref="TextInk"/>): within 8 of <paramref name="foreground"/>, and more than 8 from <paramref name="replaced"/> when given.</summary>
    private static void AssertInk(Screenshot picture, Rect area, Color foreground, Color background, string what, Color? replaced = null)
    {
        var problems = TextInk.Problems(picture, area, foreground, background, replaced, what);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>A background: the exact colour at a point clear of text.</summary>
    private static void AssertColour(Color expected, Screenshot picture, Point point, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(actual == expected, $"The {what} at {point} is {Hex(actual)}, not {Hex(expected)}.");
    }

    /// <summary>A pixel within ±<paramref name="tolerance"/> per channel of <paramref name="expected"/>: a backdrop's blend.</summary>
    private static void AssertNear(Color expected, Screenshot picture, Point point, int tolerance, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(
            Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance,
            $"The {what} at {point} is {Hex(actual)}, not within {tolerance} of {Hex(expected)}.");
    }

    /// <summary>A control's brush, read in the tree, resolves to <paramref name="expected"/>.</summary>
    private static void AssertBrush(Color expected, IBrush? brush, string what)
    {
        var actual = (brush as ISolidColorBrush)?.Color;
        Assert.True(actual == expected, $"{what} is drawn in {(actual is { } colour ? Hex(colour) : "no solid brush")}, not {Hex(expected)}.");
    }

    /// <summary>The colour the application's token <paramref name="key"/> resolves to in the window's theme.</summary>
    private static Color TokenOf(ShellDriver app, string key)
    {
        var variant = app.ShowsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(Application.Current!.TryGetResource(key, variant, out var value) && value is ISolidColorBrush, $"The {variant} theme has no solid brush {key}.");
        return ((ISolidColorBrush)value!).Color;
    }

    /// <summary><c>TextControlPlaceholderOpacity</c> resolves to 1 in the window's theme (Fluent's own is 0.5).</summary>
    private static void AssertPlaceholderOpacityKey(ShellDriver app)
    {
        var variant = app.ShowsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(
            Application.Current!.TryGetResource("TextControlPlaceholderOpacity", variant, out var value) && value is double opacity && opacity == L.PlaceholderOpacity,
            $"TextControlPlaceholderOpacity is {value} in the {variant} theme, not {L.PlaceholderOpacity}.");
    }

    /// <summary>
    /// The backdrop over the window, as phase 6's L31 asserts it: at a point of the graph area
    /// (300, 600), the window's background under the backdrop, within ±8 of the blend.
    /// </summary>
    private static void AssertBackdrop(Screenshot picture, L.Colours colours, Color window)
    {
        var alpha = colours.Backdrop.A / 255.0;
        var blend = Color.FromRgb(
            (byte)Math.Round(window.R * (1 - alpha)),
            (byte)Math.Round(window.G * (1 - alpha)),
            (byte)Math.Round(window.B * (1 - alpha)));
        AssertNear(blend, picture, new Point(300, 600), 8, "backdrop over the graph area");
    }

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    // ---- Text boxes -----------------------------------------------------------------------------

    /// <summary>The template's placeholder of a text box (Fluent's <c>PART_Placeholder</c>).</summary>
    private static TextBlock PlaceholderOf(TextBox box) =>
        box.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_Placeholder");

    /// <summary>The template's text presenter of a text box.</summary>
    private static TextPresenter PresenterOf(TextBox box) =>
        box.GetVisualDescendants().OfType<TextPresenter>().Single(presenter => presenter.Name == "PART_TextPresenter");

    /// <summary>
    /// The caret's column in the window (a focused box draws its caret at its caret index, over
    /// the text's line): the presenter's left plus the caret's offset in its layout.
    /// </summary>
    private static double CaretX(ShellDriver app, TextBox box)
    {
        var presenter = PresenterOf(box);
        var hit = presenter.TextLayout.HitTestTextPosition(box.CaretIndex);
        return app.BoundsOf(presenter).X + hit.X;
    }

    /// <summary>A rectangle less the columns of a caret at <paramref name="caretX"/> (2 px either side), keeping the wider part.</summary>
    private static Rect LessCaret(Rect area, double caretX)
    {
        var left = new Rect(area.Left, area.Top, Math.Max(0, caretX - 2 - area.Left), area.Height);
        var right = new Rect(caretX + 2, area.Top, Math.Max(0, area.Right - caretX - 2), area.Height);
        return left.Width >= right.Width ? left : right;
    }

    /// <summary>
    /// Asserts an empty box's placeholder (the report's "How each colour is read"): the box empty,
    /// its placeholder text <paramref name="text"/>, <c>PART_Placeholder</c> visible at opacity 1 in
    /// the token's colour, its ink the token's on the box's colour (more than 8 from the colour it
    /// replaces), read in <c>PART_Placeholder</c>'s rectangle less the caret's column when the box
    /// has the focus; the box's background at a point of its left padding. Whether the box has the
    /// focus is asserted as <paramref name="focused"/> says.
    /// </summary>
    private static void AssertPlaceholder(ShellDriver app, Screenshot picture, TextBox box, string text, bool focused)
    {
        var colours = ColoursOf(app);
        var name = AutomationProperties.GetAutomationId(box) ?? box.Name;
        Assert.True(string.IsNullOrEmpty(box.Text), $"{name} holds \"{box.Text}\", not nothing.");
        Assert.True(box.IsFocused == focused, $"{name} {(box.IsFocused ? "has" : "does not have")} the keyboard focus.");
        Assert.Equal(text, box.PlaceholderText);
        var placeholder = PlaceholderOf(box);
        Assert.Equal(text, placeholder.Text);
        Assert.True(placeholder.IsEffectivelyVisible, $"{name} shows no placeholder.");
        Assert.True(placeholder.Opacity == L.PlaceholderOpacity, $"{name}'s placeholder is drawn at opacity {placeholder.Opacity}, not 1.");
        AssertBrush(colours.Placeholder, placeholder.Foreground, $"{name}'s placeholder (VcTextPlaceholderBrush)");
        var bounds = app.BoundsOf(box);
        AssertColour(colours.Box, picture, new Point(bounds.Left + 4, bounds.Center.Y), $"{name}'s background (TextControlBackground)");
        var area = app.BoundsOf(placeholder);
        if (focused)
        {
            area = LessCaret(area, CaretX(app, box));
        }

        AssertInk(picture, area, colours.Placeholder, colours.Box, $"{name}'s placeholder \"{text}\"", colours.PlaceholderReplaced);
    }

    /// <summary>
    /// Asserts a box's typed text: <paramref name="text"/> in it, no placeholder shown, its ink the
    /// typed text's colour on the box's, read in the text's own rectangle less the caret's column.
    /// </summary>
    private static void AssertTyped(ShellDriver app, Screenshot picture, TextBox box, string text)
    {
        var colours = ColoursOf(app);
        Assert.Equal(text, box.Text);
        Assert.False(PlaceholderOf(box).IsEffectivelyVisible, $"{box.Name} shows its placeholder while it holds \"{text}\".");
        var presenter = PresenterOf(box);
        var origin = app.BoundsOf(presenter).TopLeft;
        var layout = presenter.TextLayout;
        var area = new Rect(origin.X, origin.Y, layout.WidthIncludingTrailingWhitespace, layout.Height);
        area = box.IsFocused ? LessCaret(area, CaretX(app, box)) : area;
        AssertInk(picture, area, colours.Typed, colours.Box, $"the text typed into {box.Name}");
    }

    /// <summary>A text box of the window or of the dialog on top, by its automation id.</summary>
    private static TextBox Box(ShellDriver app, string automationId) => (TextBox)app.FindByAutomationId(automationId);

    /// <summary>A text box of the dialog on top, by its automation id.</summary>
    private static TextBox DialogBox(ShellDriver app, string automationId) =>
        (TextBox)app.FindByAutomationId(app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open."), automationId);

    /// <summary>
    /// The Summary box's counter (<c>SummaryCounter</c>): <c>72</c>, its ink the secondary text's on
    /// the box, more than 8 from the placeholder's, so that it does not read as the placeholder (D418).
    /// </summary>
    private static void AssertCounter(ShellDriver app, Screenshot picture)
    {
        var colours = ColoursOf(app);
        var counter = (TextBlock)app.FindByAutomationId("SummaryCounter");
        Assert.Equal(L.SummaryLimit, counter.Text);
        AssertBrush(colours.Secondary, counter.Foreground, "SummaryCounter (VcTextSecondaryBrush)");
        AssertInk(picture, app.BoundsOf(counter), colours.Secondary, colours.Box, "the Summary box's counter", colours.Placeholder);

        // The placeholder's rectangle ends at the box's right padding, so the counter lies outside it.
        var summary = Box(app, "CommitSummary");
        var placeholder = app.BoundsOf(PlaceholderOf(summary));
        Assert.True(
            placeholder.Right <= app.BoundsOf(summary).Right - L.SummaryRightPadding + 0.5 && placeholder.Right <= app.BoundsOf(counter).Left + 0.5,
            $"Summary's placeholder at {placeholder} reaches into its right padding or the counter at {app.BoundsOf(counter)}.");
    }

    /// <summary>
    /// L1's and L2's three boxes after the working-changes row is clicked: <c>FilterBox</c>,
    /// <c>CommitSummary</c> and <c>CommitDescription</c> empty, unfocused, each with its placeholder;
    /// the opacity key; the counter.
    /// </summary>
    private static void AssertThreePlaceholders(ShellDriver app, Screenshot picture)
    {
        AssertPlaceholder(app, picture, Box(app, "FilterBox"), L.Filter, focused: false);
        AssertPlaceholder(app, picture, Box(app, "CommitSummary"), L.Summary, focused: false);
        AssertPlaceholder(app, picture, Box(app, "CommitDescription"), L.Description, focused: false);
        AssertPlaceholderOpacityKey(app);
        AssertCounter(app, picture);
    }

    // ---- Rows: a ref's name and a file's status letter -------------------------------------------

    /// <summary>The name block (<c>RefName</c>) of the left panel's row at <paramref name="path"/>, and the row's button.</summary>
    private static async Task<(Button Row, TextBlock Name)> RefNameAsync(ShellDriver app, params string[] path)
    {
        var row = (Button)await app.RefRowAsync(path);
        var name = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "RefName");
        return (row, name);
    }

    /// <summary>The ink rectangle of a text block's own text: its left, as wide as its text is laid out.</summary>
    private static Rect TextArea(ShellDriver app, TextBlock block)
    {
        var bounds = app.BoundsOf(block);
        return new Rect(bounds.X, bounds.Y, Math.Min(block.TextLayout.WidthIncludingTrailingWhitespace + 1, bounds.Width), bounds.Height);
    }

    /// <summary>
    /// Asserts a text's colour in the tree and in the picture: its <c>Foreground</c>
    /// <paramref name="expected"/>, its ink that colour on <paramref name="background"/>, more than 8
    /// from <paramref name="replaced"/> when given, and the background at <paramref name="clear"/>.
    /// </summary>
    private static void AssertText(ShellDriver app, Screenshot picture, TextBlock block, Color expected, Color background, Point clear, string what, Color? replaced = null)
    {
        AssertBrush(expected, block.Foreground, what);
        AssertColour(background, picture, clear, $"background of {what}");
        AssertInk(picture, TextArea(app, block), expected, background, what, replaced);
    }

    /// <summary>The status letter (<c>TextBlock.status</c>) of a file row.</summary>
    private static TextBlock StatusOf(Control row) =>
        row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("status") && text.IsEffectivelyVisible);

    /// <summary>
    /// A point of a row clear of its text and of its buttons: 2 px in from its left edge, in the
    /// indent before its name or letter, at its middle.
    /// </summary>
    private static Point ClearOf(ShellDriver app, Control row)
    {
        var bounds = app.BoundsOf(row);
        return new Point(bounds.Left + 2, bounds.Center.Y);
    }

    /// <summary>
    /// Waits until the stage panel shows "changes"' lists (7 unstaged, 3 staged) and is settled,
    /// as phase 2's checks wait.
    /// </summary>
    private static Task WaitForStagePanelAsync(ShellDriver app) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == "Unstaged files (7)"
                && app.Find<TextBlock>("StagedTitle").Text == "Staged files (3)"
                && app.StagePanelSettled,
            "the stage panel with 7 unstaged and 3 staged files");

    /// <summary>Waits until the details panel shows the commit with <paramref name="subject"/> and its file list.</summary>
    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => app.Session.Shell.ActiveTab.Repository?.Details is { } details && details.Subject == subject && !details.IsLoading,
            $"the details of \"{subject}\"");
        app.WaitForCommitDetails();
    }

    /// <summary>Waits until a diff of <paramref name="path"/> is open in place of the graph, read.</summary>
    private static Task WaitForDiffAsync(ShellDriver app, string path) =>
        app.WaitForAsync(
            () => app.Session.Shell.ActiveTab.Repository is { ShowsDiff: true, Diff: { } diff } && diff.Target.Path == path && diff.Body != DiffBody.Loading,
            $"the diff of {path} to open");

    /// <summary>Whether no tooltip is open in the window.</summary>
    private static bool NoToolTipOpen(ShellDriver app) =>
        !app.Window.GetVisualDescendants().OfType<Control>().Any(control => ToolTip.GetIsOpen(control));

    /// <summary>Whether a left-panel row is selected (its model and its button's class).</summary>
    private static bool IsSelected(ShellDriver app, Button row, params string[] path) =>
        app.RefRowModel(path) is RefItemRow { IsSelected: true } && row.Classes.Contains("selected");

    // ---- The steps' routes -------------------------------------------------------------------------

    /// <summary>The graph row of "Add settings page", HEAD's commit on <c>main</c>, in the "graph" scenario (phase 1's table); <c>origin/main</c>'s is the next.</summary>
    private const int HeadRow = 2;

    /// <summary>Opens <c>Local branches › feature</c> by a click on its row.</summary>
    private static async Task OpenFeatureFolderAsync(ShellDriver app)
    {
        app.Click(await app.RefRowAsync(L.LocalBranches, L.Feature));
        await app.WaitForAsync(() => app.RefRowModel(L.LocalBranches, L.Feature) is RefItemRow { IsOpen: true }, "Local branches › feature to open");
    }

    /// <summary>Ctrl+Shift+E (phase 6's keys): the left panel's filter box takes the keyboard focus.</summary>
    private static async Task FocusFilterAsync(ShellDriver app)
    {
        app.PressShortcut(AppCommand.FilterRefs);
        await app.WaitForAsync(() => Box(app, "FilterBox").IsFocused, "FilterBox to have the keyboard focus");
    }

    /// <summary>Types <c>sea</c> into the focused filter box and waits until the left panel filters by it.</summary>
    private static async Task TypeIntoFilterAsync(ShellDriver app)
    {
        app.Type(L.Typed);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.LeftPanel.FilterText == L.Typed && Box(app, "FilterBox").Text == L.Typed, "the left panel to filter by sea");
    }

    /// <summary>Empties the filter (the command modifier + A, Backspace) and waits until the left panel lists <c>main</c> again.</summary>
    private static async Task EmptyFilterAsync(ShellDriver app)
    {
        app.PressKey(Key.A, app.CommandModifier == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control);
        app.PressKey(Key.Back);
        await app.WaitForAsync(
            () => string.IsNullOrEmpty(Box(app, "FilterBox").Text)
                && app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Any(row => row.Text == L.Main && row.IsHead),
            "the filter to be empty and the left panel to list main again");
    }

    /// <summary>
    /// The discard of <c>src/Calculator.cs</c> asked as phase 2's check 17 asks it: the pointer
    /// moved onto its unstaged row, <c>RowDiscardButton</c> clicked once it shows; then the
    /// confirmation, its Cancel focused.
    /// </summary>
    private static async Task AskDiscardAsync(ShellDriver app)
    {
        var row = await app.FileRowAsync("UnstagedFileList", L.Calculator);
        app.MoveMouse(row);
        Button? discard = null;
        await app.WaitForAsync(
            () => (discard = StageRowOf(row).GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == "RowDiscardButton" && button.IsEffectivelyVisible)) is not null,
            $"RowDiscardButton to show on {L.Calculator}'s row");
        app.Click(discard!);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        await app.WaitForAsync(() => app.FindByAutomationId(app.TopDialogView!, "CancelButton").IsFocused, "Cancel to have the keyboard focus");
    }

    /// <summary>
    /// L5's confirmation (the report's L5): its texts; Cancel focused; Discard the danger button,
    /// its background the danger colour at a point of its left padding, its label's ink and
    /// <c>Foreground</c> <c>VcDangerTextBrush</c> on it; the backdrop over the window.
    /// </summary>
    private static void AssertDiscardDialog(ShellDriver app, Screenshot picture)
    {
        var colours = ColoursOf(app);
        var view = app.TopDialogView!;
        Assert.Equal(L.DiscardTitle, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(L.DiscardQuestion, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        Assert.Equal(L.DiscardNote, ((TextBlock)app.FindByAutomationId(view, "ConfirmDetail")).Text);
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        Assert.True(cancel.IsFocused, "Cancel does not have the keyboard focus.");
        Assert.Equal(L.Cancel, cancel.Content as string);
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(L.Discard, confirm.Content as string);
        Assert.True(confirm.Classes.Contains("danger"), "Discard is not the danger button.");
        var bounds = app.BoundsOf(confirm);
        AssertColour(colours.Danger, picture, new Point(bounds.Left + 4, bounds.Center.Y), "Discard's background (VcDangerBrush)");
        Assert.Equal(colours.DangerText, TokenOf(app, "VcDangerTextBrush"));
        AssertBrush(colours.DangerText, confirm.Foreground, "Discard's label (VcDangerTextBrush)");
        var label = confirm.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible);
        AssertInk(picture, TextArea(app, label), colours.DangerText, colours.Danger, "Discard's label", ReplacedIn(app, colours.DangerTextReplaced));
        AssertBackdrop(picture, colours, colours.Window);
    }

    /// <summary>Cancel: no dialog; <c>src/Calculator.cs</c> still modified and unstaged; the repository as it was.</summary>
    private static async Task CancelDiscardAsync(ShellDriver app, TempRepo repo, string exact)
    {
        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Contains(" M " + L.Calculator, (await repo.GitAsync("status", "--porcelain")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// L6 a to c on HEAD's <c>main</c> (the report's L6, methods 2 and 7): a) the pointer at the
    /// neutral point, the row neither hovered nor selected: the accent on the panel; b) the pointer
    /// at the row's centre, right of the name, so that no tooltip opens: the row's accent on the
    /// hover; c) the row clicked, the pointer at the neutral point: the row's accent on the
    /// selection, <paramref name="otherSelected"/>'s row no longer selected. In light the row's
    /// accent is the accent.
    /// </summary>
    private static async Task AssertHeadRowAsync(ShellDriver app, string idle, string hovered, string selected, (string, string, string)? otherSelected)
    {
        var colours = ColoursOf(app);
        string[] path = [L.LocalBranches, L.Main];

        // a) idle.
        app.MoveToNeutral();
        var (row, name) = await RefNameAsync(app, path);
        Assert.True(((RefItemRow)app.RefRowModel(path)).IsHead, "main is not HEAD's branch.");
        var shot = Shot(app, idle);
        Assert.False(row.IsPointerOver || IsSelected(app, row, path), "main's row is hovered or selected.");
        AssertText(app, shot, name, colours.Accent, colours.Panel, ClearOf(app, row), "main idle (VcAccentBrush)", ReplacedIn(app, colours.AccentOnRow));

        // b) hovered at the row's centre.
        app.MoveMouse(row);
        await app.WaitForAsync(() => row.IsPointerOver, "main's row to be hovered");
        Assert.True(app.BoundsOf(row).Center.X > TextArea(app, name).Right, "The row's centre lies on main's text.");
        shot = Shot(app, hovered, keepPointer: true);
        Assert.True(row.IsPointerOver, "main's row is not hovered.");
        Assert.False(ToolTip.GetIsOpen(name), "main's name shows its tooltip.");
        AssertText(app, shot, name, colours.AccentOnRow, colours.Hover, ClearOf(app, row), "main hovered (VcAccentOnRowBrush)", ReplacedIn(app, colours.Accent));

        // c) selected, the pointer away.
        app.Click(row);
        app.MoveToNeutral();
        await app.WaitForAsync(() => IsSelected(app, row, path) && !row.IsPointerOver, "main's row to be selected, the pointer away");
        shot = Shot(app, selected);
        if (otherSelected is var (section, folder, branch))
        {
            Assert.False(((RefItemRow)app.RefRowModel(section, folder, branch)).IsSelected, $"{folder}/{branch}'s row is still selected.");
        }

        AssertText(app, shot, name, colours.AccentOnRow, colours.Selection, ClearOf(app, row), "main selected (VcAccentOnRowBrush)", ReplacedIn(app, colours.Accent));
    }

    /// <summary>
    /// In the commit details of "Add settings page": the "R" of <c>src/main-app.txt</c> drawn in
    /// <paramref name="expected"/> on <paramref name="background"/> (more than 8 from
    /// <paramref name="replaced"/>), and the "D" of <c>docs/notes.txt</c>, idle, in the danger colour.
    /// </summary>
    private static async Task AssertRenamedAndDeletedAsync(ShellDriver app, Screenshot picture, Control renamed, Color expected, Color background, Color replaced)
    {
        var colours = ColoursOf(app);
        var letter = StatusOf(renamed);
        Assert.Equal("R", letter.Text);
        AssertText(app, picture, letter, expected, background, ClearOf(app, renamed), $"{L.MainApp}'s R", replaced);
        var notes = await app.FileRowAsync("FileList", L.Notes);
        Assert.False(notes.IsPointerOver || notes.Classes.Contains("selected"), $"{L.Notes}'s row is hovered or selected.");
        AssertText(app, picture, StatusOf(notes), colours.Danger, colours.Panel, ClearOf(app, notes), $"{L.Notes}'s D", colours.DangerReplaced);
    }

    /// <summary>The Remotes dialog by the left panel's <c>Remotes</c> header and <c>Manage remotes…</c> (phase 3's check 22), listing <c>origin</c>.</summary>
    private static async Task OpenRemotesDialogAsync(ShellDriver app)
    {
        app.RightClick(await app.RefRowAsync(L.Remotes));
        app.Menu();
        app.ClickMenuItem(L.ManageRemotesItem);
        await app.WaitForDialogAsync<RemotesDialogViewModel>();
        await app.WaitForAsync(
            () => app.TopDialogView is { } view && view.GetVisualDescendants().OfType<Control>()
                .Any(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "RemoteRow" && AutomationProperties.GetName(control) == L.Origin),
            "the Remotes dialog to list origin");
    }

    /// <summary>Presses Esc until no dialog shows (at most three times), then asserts none does.</summary>
    private static async Task CloseDialogsAsync(ShellDriver app)
    {
        for (var guard = 0; app.TopDialogView is not null && guard < 3; guard++)
        {
            var shown = app.TopDialogView;
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => !ReferenceEquals(app.TopDialogView, shown) || app.TopDialogView is null || !IsFormOpen(app), "Esc to close the dialog or its form");
        }

        await AssertNoDialogAsync(app);
    }

    /// <summary>Whether the Remotes dialog's form shows (its name box).</summary>
    private static bool IsFormOpen(ShellDriver app) =>
        app.TopDialogView is { } view && view.GetVisualDescendants().OfType<TextBox>().Any(box => box.Name == "RemoteNameBox" && box.IsEffectivelyVisible);

    /// <summary>Closes the open diff by its close button.</summary>
    private static async Task CloseDiffAsync(ShellDriver app)
    {
        app.Click(app.FindByAutomationId("CloseDiffButton"));
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository?.IsDiffOpen == false, "the diff to close");
    }

    /// <summary><c>origin</c>'s <c>Remove</c> in the Remotes dialog.</summary>
    private static Button RemoveButton(ShellDriver app) => (Button)app.FindByAutomationId(app.TopDialogView!, "RemoteRemoveButton");

    /// <summary>A button's label as its content presenter draws it.</summary>
    private static TextBlock LabelOf(Button button) => button.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible);

    /// <summary>A point of a button's left padding, clear of its centred label.</summary>
    private static Point LeftPadding(ShellDriver app, Control button)
    {
        var bounds = app.BoundsOf(button);
        return new Point(bounds.Left + 4, bounds.Center.Y);
    }

    /// <summary>The label of the palette's row <paramref name="index"/> (<c>CommandPaletteItemLabel</c>).</summary>
    private static TextBlock PaletteLabelOf(ShellDriver app, int index)
    {
        var list = (ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList");
        var container = list.ContainerFromIndex(index) ?? throw new Xunit.Sdk.XunitException($"The palette's row {index} is not laid out.");
        return container.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "CommandPaletteItemLabel");
    }

    /// <summary>The file history's row <paramref name="index"/> as its list's item.</summary>
    private static ListBoxItem HistoryItem(ShellDriver app, int index)
    {
        var list = app.Find<ListBox>("FileHistoryList");
        return list.ContainerFromIndex(index) as ListBoxItem ?? throw new Xunit.Sdk.XunitException($"The history's row {index} is not laid out.");
    }

    /// <summary>The stage panel's row (<c>Panel.stageRow</c>) a file row's button lies in.</summary>
    private static Panel StageRowOf(Control row) =>
        row.GetVisualAncestors().OfType<Panel>().First(panel => panel.Classes.Contains("stageRow"));

    /// <summary>The settings file's text, or null while there is none.</summary>
    private static string? SettingsText(TempDirectory data)
    {
        var file = Path.Combine(data.Path, "settings.json");
        return File.Exists(file) ? File.ReadAllText(file) : null;
    }
}
