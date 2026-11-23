// Part G's helpers (docs/test-reports/phase-8a.md, "Conventions of part G"): the palette's one row,
// the call log's readings, the launcher's requests, the settings screen's controls, the Keyboard
// shortcuts section scrolled to its end (phase 6's S14 route, written again: its helper is private to
// phase 6's project), About's rows, the first-run screen's states and links, the reading of
// VcLinkButton's frame by LinkFrame as part A reads it, and the reading of a first-run link's frame,
// Fluent's on a VcFlatButton, against a capture taken before the focus moved (ruling 80).
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

public sealed partial class Phase8aChecksG
{
    /// <summary>The settings screen's other sections' panels, by their names, none of which shows beside Keyboard shortcuts.</summary>
    private static readonly string[] OtherSections = ["SettingsGeneral", "SettingsGit", "SettingsTools", "SettingsAccounts", "SettingsAbout"];

    /// <summary>
    /// A picture of part G (<see cref="Phase8aChecks.Shot"/>), the pointer at the neutral point: saved
    /// under its name, then no text cut off (shortened text whole in its tooltip) and no tooltip open.
    /// </summary>
    private static Screenshot Picture(ShellDriver app, string name)
    {
        var picture = Shot(app, name);
        AssertNoClippedText(app);
        Assert.False(ToolTipOpen(app), $"A tooltip is open in {name}.");
        return picture;
    }

    private static bool ToolTipOpen(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<Control>().Any(control => ToolTip.GetIsOpen(control));

    /// <summary>A line in the test's output, for what the report's open point 103 asks to be told and no assertion holds.</summary>
    private static void Note(string text) => TestContext.Current.TestOutputHelper?.WriteLine(text);

    /// <summary>Git calls as one line of their command texts, each with its operation when it ran in one.</summary>
    private static string Described(IEnumerable<GitCallRecord> calls)
    {
        var lines = calls.Select(call => call.CommandText + (call.OperationId is null ? string.Empty : $" (in {call.OperationId})")).ToList();
        return lines.Count == 0 ? "none" : string.Join("; ", lines);
    }

    /// <summary>The data folder's settings file, which no step of part G writes.</summary>
    private static string SettingsFile(TempDirectory data) => Path.Combine(data.Path, "settings.json");

    /// <summary>Waits until the control <paramref name="id"/> has the keyboard focus (the nearest ancestor with an id).</summary>
    private static Task WaitForFocusAsync(ShellDriver app, string id) =>
        app.WaitForAsync(() => app.FocusedAutomationId() == id, $"the keyboard focus on {id} (it is on {app.FocusedAutomationId() ?? "nothing"})");

    // ---- The launcher and the log ------------------------------------------------------------

    /// <summary>The launcher holds exactly these addresses, each an <c>OpenUrl</c>, in order: no start, nothing shown in a folder.</summary>
    private static void AssertRequests(ShellDriver app, params string[] addresses)
    {
        var requests = app.Launcher.Requests;
        Assert.True(
            requests.SequenceEqual(addresses.Select(address => new FileLaunch(FileLaunchKind.OpenUrl, address))),
            "The launcher's requests are not the expected ones.\nExpected:\n  " + string.Join("\n  ", addresses)
            + "\nActual:\n  " + string.Join("\n  ", requests.Select(request => $"{request.Kind} {request.FullPath}")));
        AssertNoStart(app);
    }

    /// <summary>The launcher started no program and showed no file in a folder.</summary>
    private static void AssertNoStart(ShellDriver app)
    {
        Assert.Empty(app.Launcher.Starts);
        Assert.Empty(app.Launcher.Revealed);
        Assert.Empty(app.Launcher.Opened);
        Assert.Empty(app.Launcher.Edits);
    }

    /// <summary>No log line since <paramref name="mark"/> holds any of <paramref name="lines"/>.</summary>
    private static void AssertNotLogged(ShellDriver app, int mark, params string[] lines)
    {
        var since = app.LogLinesSince(mark);
        foreach (var line in lines)
        {
            Assert.DoesNotContain(since, logged => logged.Contains(line, StringComparison.Ordinal));
        }
    }

    // ---- G1: the graph, the palette and the call log -------------------------------------------

    /// <summary>"Main work" is the graph's one selected row and the graph's rows have the keyboard focus.</summary>
    private static void AssertMainWorkSelectedAndFocused(ShellDriver app, VisualCommit.App.ViewModels.RepositoryViewModel repository, string mainWork)
    {
        app.Settle();
        Assert.Equal([mainWork], repository.Selection.Shas);
        Assert.Equal(G.GraphId, app.FocusedAutomationId());
    }

    /// <summary>
    /// The palette's one row for <c>guide</c>: <c>Open the user guide</c>, highlighted (selected), enabled,
    /// its label in the primary colour and not drawn as danger, no detail and no item status, its key
    /// <c>F1</c> shown and as its accelerator key, no help text; "No matches" hidden. With
    /// <paramref name="picture"/>, the highlighted row's background at a point of its padding clear of
    /// text is the selection's colour.
    /// </summary>
    private static void AssertGuideRow(ShellDriver app, Screenshot? picture)
    {
        var snapshot = app.PaletteSnapshot();
        Assert.Equal(G.Query, app.PaletteBox.Text);
        Assert.False(snapshot.ShowsNothing, "\"No matches\" shows.");
        var row = Assert.Single(snapshot.Rows);
        Assert.Equal(G.OpenUserGuide, row.Label);
        Assert.True(row.IsHighlighted, "The row is not highlighted.");
        Assert.True(row.IsEnabled, "The row is disabled.");
        Assert.Null(row.Detail);
        Assert.Equal(G.Key, row.KeyText);
        Assert.Equal(G.Primary, row.LabelColour);
        var item = app.PaletteView.GetVisualDescendants().OfType<ListBoxItem>().Single(container => container.IsEffectivelyVisible && container.DataContext is PaletteEntry);
        Assert.Equal(G.PaletteItemId, AutomationProperties.GetAutomationId(item));
        Assert.Equal(G.OpenUserGuide, AutomationProperties.GetName(item));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetItemStatus(item)), "The row has an item status.");
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(item)), "The row has a help text.");
        Assert.Equal(G.Key, AutomationProperties.GetAcceleratorKey(item));
        Assert.True(item.IsSelected, "The row is not selected.");
        var blocks = item.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name is not null).ToDictionary(block => block.Name!, block => block);
        Assert.DoesNotContain("danger", blocks[G.PaletteItemLabel].Classes);
        Assert.True(string.IsNullOrEmpty(blocks[G.PaletteItemDetail].Text), "The row shows a detail.");
        Assert.True(blocks[G.PaletteItemKey].IsEffectivelyVisible, "The row shows no key.");
        Assert.Equal(G.Key, blocks[G.PaletteItemKey].Text);
        Assert.False(app.FindByAutomationId(app.PaletteView, G.PaletteEmptyId).IsEffectivelyVisible, "\"No matches\" shows.");
        if (picture is not null)
        {
            var point = new Point(row.Bounds.Left + G.RowBackgroundInset, row.Bounds.Center.Y);
            var background = picture.PixelAt(point);
            Assert.True(background == G.Selection, $"The highlighted row's background at {point} is {Hex(background)}, not {Hex(G.Selection)}.");
        }
    }

    /// <summary>
    /// Once every watcher is quiet: since <paramref name="mark"/> git ran the palette's worktree read
    /// exactly once outside every operation, and besides it only whole refreshes; no operation ended
    /// since the operation log held <paramref name="operations"/> records (phase 6's K10 step 2).
    /// </summary>
    private static async Task AssertOnlyTheWorktreeReadAsync(ShellDriver app, long mark, int operations)
    {
        var calls = await app.QuietCallsSinceAsync(mark);
        static bool IsWorktreeRead(GitCallRecord call) => call.CommandText == G.WorktreeList && call.OperationId is null;
        Assert.True(calls.Count(IsWorktreeRead) == 1, "Since the palette's key git did not run its worktree read once: " + Described(calls));
        GitCallBudget.AssertOnlyWholeRefreshes([.. calls.Where(call => !IsWorktreeRead(call))], "Besides its worktree read, the palette's key and the typed query");
        var records = app.Session.Operations.Snapshot();
        Assert.True(records.Count == operations, "The palette ran an operation: " + string.Join(", ", records.Skip(operations).Select(record => record.Title)));
    }

    // ---- G1: the Keyboard shortcuts section ----------------------------------------------------

    /// <summary>
    /// Scrolls the shown section to its end with the wheel, as phase 6's S14 step 7 does: three
    /// notches down at the section's centre a turn, at most 60 turns, until its offset is within 0.5
    /// of its extent less its viewport; then the pointer at rest.
    /// </summary>
    private static async Task ScrollSectionToEndAsync(ShellDriver app)
    {
        var section = InSettings<ScrollViewer>(app, G.SectionScrollerId);
        Assert.Equal(G.SectionScrollerName, section.Name);
        var point = app.BoundsOf(section).Center;
        for (var turn = 0; turn < 60 && section.Offset.Y < section.Extent.Height - section.Viewport.Height - 0.5; turn++)
        {
            app.Wheel(point, -3);
        }

        await app.WaitForAsync(() => AtEnd(section), "the section to be scrolled to its end");
        app.MoveToNeutral();
        app.Settle();
    }

    private static bool AtEnd(ScrollViewer section) => Math.Abs(section.Offset.Y - (section.Extent.Height - section.Viewport.Height)) <= 0.5;

    /// <summary>
    /// G1 after its step 8: Keyboard shortcuts alone shown, its button selected; the list's 12
    /// headings and 51 rows; Help the last heading, after Dialogs and menus, 12.5 SemiBold; under it
    /// exactly one row, the list's last, named <c>Open the user guide: F1</c>, its action at the left
    /// and its keys at the right in the code font and the secondary colour, each on one line, at least
    /// 22 high; the Repository group's note the list's one note; the section at its end, Help's heading
    /// and row wholly inside the section's scroll viewer.
    /// </summary>
    private static void AssertShortcutsEnd(ShellDriver app)
    {
        Assert.Equal(VisualCommit.App.ViewModels.SettingsSection.KeyboardShortcuts, app.SettingsDialog!.SelectedSection);
        Assert.Contains("selected", InSettings<Button>(app, G.NavShortcutsId).Classes);
        foreach (var other in OtherSections)
        {
            var panel = SettingsView(app).GetVisualDescendants().OfType<Control>().Single(control => control.Name == other);
            Assert.False(panel.IsEffectivelyVisible, $"{other} shows beside Keyboard shortcuts.");
        }

        var list = app.Find<ItemsControl>(G.ShortcutListName);
        var headings = list.GetVisualDescendants().OfType<TextBlock>().Where(block => AutomationProperties.GetAutomationId(block) == G.ShortcutGroupId).ToList();
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(grid => AutomationProperties.GetAutomationId(grid) == G.ShortcutRowId).ToList();
        Assert.Equal(G.ShortcutGroups, headings.Count);
        Assert.Equal(G.ShortcutRows, rows.Count);
        var help = headings[^1];
        Assert.Equal(G.HelpGroup, help.Text);
        Assert.Equal(G.GroupBeforeHelp, headings[^2].Text);
        Assert.Equal((G.GroupHeadingSize, FontWeight.SemiBold), (help.FontSize, help.FontWeight));

        var helpTop = app.BoundsOf(help).Bottom - 0.5;
        var row = Assert.Single(rows, candidate => app.BoundsOf(candidate).Top >= helpTop);
        Assert.Same(rows[^1], row);
        Assert.Equal(G.HelpRowName, AutomationProperties.GetName(row));
        var blocks = row.Children.OfType<TextBlock>().ToList();
        Assert.Equal((G.OpenUserGuide, G.Key), (blocks[0].Text, blocks[1].Text));
        var (action, keys) = (blocks[0].Bounds, blocks[1].Bounds);
        Assert.True(
            action.X < 0.5 && keys.Right >= row.Bounds.Width - 0.5 && action.Right <= keys.X,
            $"The Help row's action and keys are not apart at its left and right: {action}, {keys} in {row.Bounds.Width}.");
        Assert.Equal((FontFamily)Application.Current!.FindResource("VcCodeFontFamily")!, blocks[1].FontFamily);
        Assert.Equal(G.Secondary, (blocks[1].Foreground as ISolidColorBrush)?.Color);
        Assert.Equal((1, 1), (blocks[0].TextLayout.TextLines.Count, blocks[1].TextLayout.TextLines.Count));
        Assert.True(row.Bounds.Height >= G.ShortcutRowHeight - 0.01, $"The Help row is {row.Bounds.Height} high, less than {G.ShortcutRowHeight}.");

        // No note under Help: the list's one note is the Repository group's, above it.
        var notes = list.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("settingsNote") && block.IsEffectivelyVisible).ToList();
        var note = Assert.Single(notes);
        Assert.Equal(G.RepositoryNote, note.Text);
        Assert.True(app.BoundsOf(note).Bottom <= app.BoundsOf(help).Top, "The list's note lies under Help.");

        // At the end, Help's heading and row whole in the section's viewer.
        var scroller = InSettings<ScrollViewer>(app, G.SectionScrollerId);
        Assert.True(AtEnd(scroller), $"The section is not at its end: offset {scroller.Offset.Y}, extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}.");
        var section = app.BoundsOf(scroller);
        foreach (var (what, bounds) in new[] { ("Help's heading", app.BoundsOf(help)), ("Help's row", app.BoundsOf(row)) })
        {
            Assert.True(
                bounds.Top >= section.Top - 0.5 && bounds.Bottom <= section.Bottom + 0.5 && bounds.Left >= section.Left - 0.5 && bounds.Right <= section.Right + 0.5,
                $"{what} at {bounds} is not wholly inside the section at {section}.");
        }
    }

    // ---- The settings screen and About (G1, G3) ------------------------------------------------

    /// <summary>The settings screen's view (<c>SettingsDialog</c>), wherever it is in the dialog stack.</summary>
    private static SettingsDialogView SettingsView(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<SettingsDialogView>().Single(view => view.IsEffectivelyVisible);

    /// <summary>A control of the settings screen by its automation id.</summary>
    private static T InSettings<T>(ShellDriver app, string id)
        where T : Control => (T)app.FindByAutomationId(SettingsView(app), id);

    private static TextBlock LinkText(Button link) => link.GetVisualDescendants().OfType<TextBlock>().Single();

    private static bool IsUnderlined(TextBlock block) =>
        block.TextDecorations is { Count: > 0 } decorations && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline);

    /// <summary>
    /// G3 after its step 2: About as part A's A1 after its step 3 reads it, over the "ops" tab: the
    /// pinned identity's texts, the labels in order in the form's first column in the secondary
    /// colour, the values in its third column, both links in the accent colour and not underlined,
    /// nothing cut off and no "@"; and the row <c>User guide</c>: its label at x 336, its link
    /// <c>Open the user guide</c> at x 480, named by its text, enabled, its ink the accent on the
    /// card in <paramref name="capture"/>, its tooltip's text.
    /// </summary>
    private static void AssertAboutWithGuideRow(ShellDriver app, Screenshot capture)
    {
        Assert.NotNull(app.Session.Shell.ActiveTab.Repository);
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "The settings screen does not show About.");
        var section = InSettings<StackPanel>(app, G.AboutSectionId);
        Assert.Equal(A.Name, InSettings<TextBlock>(app, A.NameId).Text);
        Assert.Equal(A.Creator, InSettings<TextBlock>(app, A.CreatorId).Text);
        var version = InSettings<TextBlock>(app, A.VersionId);
        Assert.Equal((PinnedVersion, PinnedVersion), (version.Text, AutomationProperties.GetName(version)));
        Assert.Equal(A.Copyright, InSettings<TextBlock>(app, A.CopyrightId).Text);
        Assert.Equal(A.MitLicense, InSettings<TextBlock>(app, A.LicenseId).Text);
        Assert.Equal(A.NoticesNote, InSettings<TextBlock>(app, A.NoticesNoteId).Text);
        Assert.Equal(A.ShowLicense, InSettings<Button>(app, G.AboutShowLicenseId).Content);
        Assert.Equal(A.ShowNotices, InSettings<Button>(app, G.AboutShowNoticesId).Content);

        // The labels, the guide's last, in the first column, secondary.
        var labels = section.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("settingsLabel")).ToList();
        Assert.Equal(A.Labels, labels.Select(label => label.Text));
        Assert.Equal(G.UserGuideLabel, labels[^1].Text);
        Assert.All(labels, label =>
        {
            Assert.True(Math.Abs(app.BoundsOf(label).X - G.LabelX) <= G.Tolerance, $"The label {label.Text} is at x {app.BoundsOf(label).X}, not {G.LabelX} ±{G.Tolerance}.");
            Assert.Equal(G.Secondary, (label.Foreground as ISolidColorBrush)?.Color);
        });

        // The values, in the third column.
        foreach (var id in new[] { A.VersionId, A.CopyrightId, A.LicenseId, A.NoticesNoteId, G.AboutRepositoryLinkId, G.AboutGuideLinkId })
        {
            var x = app.BoundsOf(InSettings<Control>(app, id)).X;
            Assert.True(Math.Abs(x - G.ValueX) <= G.Tolerance, $"{id} is at x {x}, not {G.ValueX} ±{G.Tolerance}.");
        }

        // The two links: the repository's as A1 has it, the guide's as G3 reads it.
        var repository = InSettings<Button>(app, G.AboutRepositoryLinkId);
        Assert.False(IsUnderlined(LinkText(repository)), "The repository link is underlined.");
        Assert.Equal(G.Accent, (LinkText(repository).Foreground as ISolidColorBrush)?.Color);
        var guide = InSettings<Button>(app, G.AboutGuideLinkId);
        var text = LinkText(guide);
        Assert.Equal(G.AboutGuideLink, text.Text);
        Assert.Equal(G.AboutGuideLink, AutomationProperties.GetName(guide));
        Assert.True(guide.IsEffectivelyEnabled, "The guide link is disabled.");
        Assert.False(IsUnderlined(text), "The guide link is underlined before it has the focus.");
        Assert.Equal(G.Accent, (text.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(G.AboutGuideTip, ToolTip.GetTip(guide) as string);
        Assert.False(ToolTip.GetIsOpen(guide), "The guide link's tooltip is open.");
        var bounds = app.BoundsOf(text);
        var ink = new Rect(bounds.X, bounds.Y, Math.Min(text.TextLayout.WidthIncludingTrailingWhitespace + 1, bounds.Width), bounds.Height);
        var problems = TextInk.Problems(capture, ink, G.Accent, G.Panel, null, G.AboutGuideLinkId);
        Assert.True(problems.Count == 0, string.Join('\n', problems));

        AssertNoClippedText(app);
        var texts = section.GetSelfAndVisualDescendants().OfType<Control>()
            .SelectMany(control => new[] { (control as TextBlock)?.Text, ToolTip.GetTip(control) as string, AutomationProperties.GetName(control), AutomationProperties.GetHelpText(control) })
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();
        Assert.DoesNotContain(texts, value => value!.Contains('@', StringComparison.Ordinal));
    }

    /// <summary>
    /// About's link <paramref name="id"/> focused by the keyboard in <paramref name="focused"/> against
    /// itself in <paramref name="unfocused"/>, by <see cref="LinkFrame"/>, as part A's A1 step 9 reads the
    /// repository link: its text's pixels unchanged but for the underline's rows, the underline across
    /// the text, a whole frame in Fluent's frame colour on four sides outside the text, and the card's
    /// background between the frame and the ink.
    /// </summary>
    private static void AssertFramedLink(ShellDriver app, Screenshot unfocused, Screenshot focused, string id)
    {
        Assert.True(
            app.Window.TryFindResource("SystemControlFocusVisualPrimaryBrush", app.Window.ActualThemeVariant, out var resource) && resource is ISolidColorBrush,
            "The theme has no SystemControlFocusVisualPrimaryBrush.");
        var frame = ((ISolidColorBrush)resource!).Color;
        var problems = LinkFrame.Problems(unfocused, focused, app.BoundsOf(LinkText(InSettings<Button>(app, id))), G.Accent, G.Panel, frame, id);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    // ---- G2: the first-run screen ----------------------------------------------------------------

    /// <summary>
    /// The first-run screen in a state: its title, its guidance, its buttons in their order (the
    /// last <c>Check again</c>), each shown, enabled and named by its text, then the one link,
    /// <c>GitSetupGuideLink</c>, named by and reading <paramref name="link"/> in the accent colour;
    /// no <c>GitSetupMoreLink</c> and no <c>GitSetupError</c>.
    /// </summary>
    private static void AssertState(ShellDriver app, string title, string guidance, IReadOnlyList<string> buttons, IReadOnlyList<string> texts, string link)
    {
        Assert.True(app.ShowsGitSetupView(), "The first-run screen is not shown.");
        Assert.Equal(title, app.GitSetupText(G.GitSetupTitleId));
        Assert.Equal(guidance, app.GitSetupText(G.GitSetupGuidanceId));
        Assert.Equal(buttons, app.GitSetupButtonIds());
        Assert.Equal(G.CheckAgain, texts[buttons.ToList().IndexOf("GitSetupCheckAgainButton")]);
        for (var i = 0; i < buttons.Count; i++)
        {
            var button = app.GitSetupPart(buttons[i]) ?? throw new Xunit.Sdk.XunitException($"{buttons[i]} is not shown.");
            Assert.True(button.IsEffectivelyEnabled, $"{buttons[i]} is disabled.");
            Assert.Equal(texts[i], AutomationProperties.GetName(button));
        }

        Assert.Equal([G.GitSetupGuideLinkId], app.GitSetupLinkIds());
        Assert.Null(app.GitSetupPart(G.GitSetupMoreLinkId));
        Assert.Null(app.GitSetupPart(G.GitSetupErrorId));
        var guide = app.GitSetupPart(G.GitSetupGuideLinkId)!;
        Assert.Equal(link, AutomationProperties.GetName(guide));
        var text = GuideLinkText(app);
        Assert.Equal(link, text.Text);
        Assert.Equal(G.Accent, (text.Foreground as ISolidColorBrush)?.Color);

        // The guide link comes after the last button.
        Assert.True(app.BoundsOf(guide).Top >= app.BoundsOf(app.GitSetupPart(buttons[^1])!).Bottom - 0.5, "The guide link is not below the buttons.");
    }

    private static TextBlock GuideLinkText(ShellDriver app) =>
        app.GitSetupPart(G.GitSetupGuideLinkId)!.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible);

    /// <summary>
    /// F6 as part F's F13 step 4 presses it: once without looking, then again while
    /// <paramref name="id"/> does not have the keyboard focus, at most four presses in all; the check
    /// fails if it does not have it then. It has it framed.
    /// </summary>
    private static async Task PressF6ToAsync(ShellDriver app, string id)
    {
        var presses = 0;
        do
        {
            app.PressF6();
            app.Settle();
            presses++;
        }
        while (app.FocusedAutomationId() != id && presses < G.MostF6Presses);
        await WaitForFocusAsync(app, id);
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
    }

    /// <summary>The guide link has the keyboard focus, framed, named by <paramref name="text"/>, its text in the accent colour (not hovered); no tooltip open.</summary>
    private static void AssertGuideLinkFocused(ShellDriver app, string text)
    {
        app.Settle();
        Assert.Equal(G.GitSetupGuideLinkId, app.FocusedAutomationId());
        Assert.True(app.FocusVisible, "The guide link has the focus without the keyboard's frame.");
        Assert.Equal(text, AutomationProperties.GetName(app.GitSetupPart(G.GitSetupGuideLinkId)!));
        Assert.Equal(G.Accent, (GuideLinkText(app).Foreground as ISolidColorBrush)?.Color);
        Assert.False(ToolTipOpen(app), "A tooltip is open.");
    }

    /// <summary>
    /// Steps 3 and 4 of a state (and 6, 8, 10): Enter on the focused guide link adds exactly the
    /// state's <paramref name="address"/>, then F1 adds the start page; neither starts a search, nor
    /// changes the screen, nor shows an error, nor calls git.
    /// </summary>
    private static async Task EnterAndF1Async(ShellDriver app, FakeGitSetupProbe probe, string title, string address, int before)
    {
        var lookups = probe.Lookups.Count;
        var calls = app.GitCalls().Count;
        var expected = app.Launcher.Requests.Select(request => request.FullPath).ToList();
        Assert.Equal(before, expected.Count);

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == before + 1, $"the launcher to hold {before + 1} requests");
        app.Settle();
        expected.Add(address);
        AssertRequests(app, [.. expected]);
        Assert.Equal(lookups, probe.Lookups.Count);
        Assert.Equal(title, app.GitSetupText(G.GitSetupTitleId));
        Assert.Null(app.GitSetupPart(G.GitSetupErrorId));

        app.PressKey(Key.F1);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == before + 2, $"the launcher to hold {before + 2} requests");
        app.Settle();
        expected.Add(G.StartPage);
        AssertRequests(app, [.. expected]);
        Assert.Equal(lookups, probe.Lookups.Count);
        Assert.Equal(title, app.GitSetupText(G.GitSetupTitleId));
        Assert.Null(app.GitSetupPart(G.GitSetupErrorId));
        Assert.Equal(calls, app.GitCalls().Count);
    }

    /// <summary>
    /// A click on "Check again", the pointer then at rest, and the wait until its search has ended
    /// (<paramref name="lookups"/> searches, the command not running, the button reading <c>Check
    /// again</c> and enabled) on the screen of <paramref name="guidance"/>; once the dispatcher has
    /// settled, the button has the keyboard focus again.
    /// </summary>
    private static async Task CheckAgainToAsync(ShellDriver app, FakeGitSetupProbe probe, int lookups, string guidance, bool winget = false)
    {
        app.Click(app.GitSetupPart("GitSetupCheckAgainButton")!);
        app.MoveToNeutral();
        await app.WaitForAsync(
            () => probe.Lookups.Count == lookups && !app.Session.Shell.CheckGitAgainCommand.IsRunning
                && app.GitSetupPart("GitSetupCheckAgainButton") is { IsEffectivelyEnabled: true } && app.GitSetupText("GitSetupCheckAgainButton") == G.CheckAgain,
            $"Check again to end after search {lookups}");
        await app.WaitForGitSetupAsync(guidance: guidance, choose: G.ChooseGitExe, winget: winget);
        app.Settle();
        Assert.Equal("GitSetupCheckAgainButton", app.FocusedAutomationId());
    }

    /// <summary>
    /// Steps 5, 7 and 9's expected screen, then Tab from "Check again" to the guide link, framed; the
    /// launcher holds <paramref name="requests"/> as after the step before.
    /// </summary>
    private static async Task TabToGuideLinkAsync(ShellDriver app, string title, string guidance, IReadOnlyList<string> buttons, IReadOnlyList<string> texts, int requests)
    {
        AssertState(app, title, guidance, buttons, texts, G.HowVisualCommitFindsGit);
        Assert.Equal("GitSetupCheckAgainButton", buttons[^1]);
        app.PressKey(Key.Tab);
        await WaitForFocusAsync(app, G.GitSetupGuideLinkId);
        AssertGuideLinkFocused(app, G.HowVisualCommitFindsGit);
        Assert.Equal(requests, app.Launcher.Requests.Count);
    }

    /// <summary>
    /// A first-run link's frame, Fluent's on a <c>VcFlatButton</c>, read against a capture of the same
    /// state taken before the focus moved, the pointer at the neutral point in both (ruling 80): inside
    /// the tight box of the link text's ink (the pixels inside its <c>TextBlock</c>'s bounds that are
    /// not the screen's background in <paramref name="before"/>, the background being the colour most
    /// of the button's pixels have there) no pixel differs; inside the button's bounds outside that box
    /// some pixel differs, where the frame is drawn. A failure names every row and column that differs
    /// inside the box, with each differing pixel's two colours.
    /// </summary>
    private static void AssertFrameClearOfInk(ShellDriver app, Screenshot before, Screenshot after, string text)
    {
        var link = app.GitSetupPart(G.GitSetupGuideLinkId)!;
        var block = GuideLinkText(app);
        Assert.Equal(text, block.Text);
        var button = PixelBox(app.BoundsOf(link), before);
        var textBox = PixelBox(app.BoundsOf(block), before);
        var background = MostFrequent(before, button);

        // The ink's tight box in the capture before the focus moved.
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (var y = textBox.Top; y <= textBox.Bottom; y++)
        {
            for (var x = textBox.Left; x <= textBox.Right; x++)
            {
                if (before.PixelAt(x, y) != background)
                {
                    (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
                }
            }
        }

        Assert.True(left <= right, $"No ink of \"{text}\" was found in its text's box {textBox} on the background {Hex(background)}.");
        var ink = (Left: left, Top: top, Right: right, Bottom: bottom);

        var inside = new List<(int X, int Y, Color Before, Color After)>();
        var outside = 0;
        for (var y = button.Top; y <= button.Bottom; y++)
        {
            for (var x = button.Left; x <= button.Right; x++)
            {
                var (was, now) = (before.PixelAt(x, y), after.PixelAt(x, y));
                if (was == now)
                {
                    continue;
                }

                if (x >= ink.Left && x <= ink.Right && y >= ink.Top && y <= ink.Bottom)
                {
                    inside.Add((x, y, was, now));
                }
                else
                {
                    outside++;
                }
            }
        }

        Note(string.Create(CultureInfo.InvariantCulture, $"G2 \"{text}\": the button's pixels x {button.Left} to {button.Right}, y {button.Top} to {button.Bottom}; the text's box x {textBox.Left} to {textBox.Right}, y {textBox.Top} to {textBox.Bottom}; the ink's box x {ink.Left} to {ink.Right}, y {ink.Top} to {ink.Bottom} on {Hex(background)}; {inside.Count} pixels differ inside the ink's box and {outside} outside it."));
        if (inside.Count > 0)
        {
            var message = new StringBuilder();
            message.Append(CultureInfo.InvariantCulture, $"The focus frame of \"{text}\" touches its ink: {inside.Count} pixels differ inside the ink's tight box x {ink.Left} to {ink.Right}, y {ink.Top} to {ink.Bottom} (the text's box x {textBox.Left} to {textBox.Right}, y {textBox.Top} to {textBox.Bottom}; the button's x {button.Left} to {button.Right}, y {button.Top} to {button.Bottom}; the background {Hex(background)}).");
            message.Append(CultureInfo.InvariantCulture, $"\nRows that differ: {string.Join(", ", inside.Select(pixel => pixel.Y).Distinct().Order())}.");
            message.Append(CultureInfo.InvariantCulture, $"\nColumns that differ: {string.Join(", ", inside.Select(pixel => pixel.X).Distinct().Order())}.");
            foreach (var pixel in inside)
            {
                message.Append(CultureInfo.InvariantCulture, $"\n  ({pixel.X}, {pixel.Y}): {Hex(pixel.Before)} before, {Hex(pixel.After)} focused");
            }

            Assert.Fail(message.ToString());
        }

        Assert.True(outside > 0, $"No pixel of \"{text}\"'s button outside its ink's box differs once it has the focus: no frame is drawn.");
    }

    /// <summary>The whole pixels a window rectangle covers in <paramref name="picture"/>, inclusive, clamped to the picture.</summary>
    private static (int Left, int Top, int Right, int Bottom) PixelBox(Rect bounds, Screenshot picture) =>
        (Math.Max(0, (int)Math.Floor(bounds.Left)),
         Math.Max(0, (int)Math.Floor(bounds.Top)),
         Math.Min(picture.Width - 1, (int)Math.Ceiling(bounds.Right) - 1),
         Math.Min(picture.Height - 1, (int)Math.Ceiling(bounds.Bottom) - 1));

    /// <summary>The colour most pixels of <paramref name="box"/> have in <paramref name="picture"/>.</summary>
    private static Color MostFrequent(Screenshot picture, (int Left, int Top, int Right, int Bottom) box)
    {
        var counts = new Dictionary<Color, int>();
        for (var y = box.Top; y <= box.Bottom; y++)
        {
            for (var x = box.Left; x <= box.Right; x++)
            {
                var colour = picture.PixelAt(x, y);
                counts[colour] = counts.GetValueOrDefault(colour) + 1;
            }
        }

        return counts.MaxBy(pair => pair.Value).Key;
    }

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
}
