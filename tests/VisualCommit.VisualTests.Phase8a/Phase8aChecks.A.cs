// Part A of phase 8a's walk-through, About and the third-party notices (docs/test-reports/phase-8a.md,
// "Part A"; docs/phase-8a-design.md 8.3, 8.5; R6, D398, D409, D417, D431, D432): the settings screen's
// sixth section in dark, light and at 1920×1080, the license and the notices in the text dialog, and
// the palette's "About VisualCommit…". Five methods, five starts, as the report's methods table has
// them; method 1 is the twin of the real window's A1 and A4 in launch 3, and method 3 of A1's capture
// at 1920×1080. Every start gets the pinned identity; nothing is launched but A1's one address, which
// the harness's fake launcher records; the clipboard is the harness's. The helpers are in
// Phase8aChecks.A.Helpers.cs.
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part A: About and the third-party notices (docs/test-reports/phase-8a.md; design 8.5).</summary>
public sealed partial class Phase8aChecksA : Phase8aChecks
{
    /// <summary>
    /// Method 1, A1 and A4 (the twin of the real window's launch 3): About from the palette, dark; the
    /// license by Tab and Enter, Copy all and Esc; then A1's walk on, the repository link pressed.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A1_A4_about_dark_and_license()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var colours = A.Dark;

        // A1 step 1: the welcome page, the pointer at rest.
        Assert.True(app.Session.Shell.ActiveTab.IsEmpty, "The start is not on the empty tab.");
        app.MoveToNeutral();

        // Step 2: the palette, "about", one row.
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette open with its box focused");
        app.TypeInPalette(A.PaletteQuery);
        await app.WaitForAsync(() => app.Palette.Shown.Count == 1, "one row for \"about\"");
        AssertPaletteRow(app);
        Assert.Empty(app.Launcher.Requests);

        // Step 3: Enter: About, its section button selected and focused with the keyboard's frame.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen && app.SettingsDialog is not null, "the palette closed and the settings screen open");
        await WaitForAboutAsync(app);
        await WaitForFocusAsync(app, A.NavAboutId);
        var about = Picture(app, A.A1aAboutDark);
        Assert.Equal("Settings", InSettings<TextBlock>(app, "SettingsTitle").Text);
        AssertCard(app, full: false);
        AssertSectionList(app, about, full: false);
        Assert.True(app.FocusVisible, "SettingsNavAbout has the focus without the keyboard's frame.");
        AssertAbout(app, about, full: false);
        Assert.Empty(app.Launcher.Requests);

        // A4 step 4: Tab to Show license…, framed.
        app.PressKey(Key.Tab);
        await WaitForFocusAsync(app, A.ShowLicenseId);
        Assert.True(app.FocusVisible, "Show license… has the focus without the keyboard's frame.");
        Assert.IsType<SettingsDialogViewModel>(app.Session.Services.DialogHost.Current);

        // Step 5: Enter: the license over the screen, the focus in the text.
        var logMark = app.LogMark();
        app.PressKey(Key.Enter);
        await app.WaitForDialogAsync<TextDocumentDialogViewModel>();
        await app.WaitForAsync(() => FocusInText(app) && DocumentText(app).TextArea.TextView.VisualLinesValid, "the license with the focus in its text");
        var license = Picture(app, A.A4aLicense);
        AssertTextDialog(app, license, A.LicenseTitle);
        Assert.False(InTextDialog<Control>(app, A.SwitchId).IsVisible, "The license shows a switch.");
        var text = DocumentText(app);
        var expected = RepositoryText(A.LicenseFile);
        Assert.Equal(expected, text.Document.Text);
        Assert.Equal(A.LicenseLines, expected.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(A.LicenseLines + 1, text.Document.LineCount);
        Assert.Equal((1, A.LicenseLines + 1), LinesInView(text));
        Assert.True(text.ScrollExtent.Height <= text.ScrollViewport.Height + 0.5, $"The license scrolls: {ScrollState(text)}.");
        Assert.Equal(A.TextBelowTitleAlone, app.BoundsOf(text).Top - app.BoundsOf(InTextDialog<TextBlock>(app, A.DialogTitleId)).Bottom, 0.5);
        AssertInk(license, FirstLineRect(app, text), colours.Primary, colours.Control, "the license's first line");
        AssertColour(colours.Control, license, new Point(app.BoundsOf(text).Center.X, app.BoundsOf(text).Bottom - 12), "text's background");
        AssertBackdropOverSettings(app, license);
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "The settings screen left About.");
        Assert.Null(app.Clipboard.Text);
        Assert.Empty(app.Launcher.Requests);

        // Step 6: Copy all: LICENSE whole on the clipboard, the dialog open.
        app.Click(InTextDialog<Button>(app, A.CopyId));
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the clipboard to hold a text");
        app.MoveToNeutral();
        Assert.Equal([expected], app.Clipboard.Texts);
        Assert.IsType<TextDocumentDialogViewModel>(app.Session.Services.DialogHost.Current);
        Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains(A.CopyFailedLine, StringComparison.Ordinal));

        // Step 7: Esc: the dialog alone closes, the focus back on Show license…, framed.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SettingsDialogViewModel, "the text dialog to close");
        await WaitForFocusAsync(app, A.ShowLicenseId);
        Assert.True(app.FocusVisible, "Show license… has the focus back without the keyboard's frame.");
        Assert.True(app.SettingsDialog is { ShowsAbout: true });
        Assert.Equal([expected], app.Clipboard.Texts);
        Assert.Empty(app.Launcher.Requests);

        // A1 step 8: Tab to Show third-party notices….
        app.PressKey(Key.Tab);
        await WaitForFocusAsync(app, A.ShowNoticesId);
        Assert.True(app.FocusVisible);
        Assert.IsType<SettingsDialogViewModel>(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Launcher.Requests);

        // Step 9: Tab to the repository link, underlined while focused.
        app.PressKey(Key.Tab);
        await WaitForFocusAsync(app, A.RepositoryLinkId);
        var linkFocus = Picture(app, A.A1bAboutLinkFocus);
        Assert.True(app.FocusVisible);
        AssertLink(app, linkFocus, A.RepositoryLinkId, A.RepositoryLink, A.RepositoryTip, underlined: true);
        AssertLink(app, linkFocus, A.GuideLinkId, A.OpenUserGuide, A.GuideTip, underlined: false);
        Assert.Empty(app.Launcher.Requests);

        // Step 10: Enter: exactly the repository's address, nothing else.
        logMark = app.LogMark();
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the launcher to hold one request");
        Assert.Equal([new FileLaunch(FileLaunchKind.OpenUrl, A.RepositoryAddress)], app.Launcher.Requests);
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "The settings screen left About.");
        Assert.Equal(A.RepositoryLinkId, app.FocusedAutomationId());
        Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains(A.RepositoryFailedLine, StringComparison.Ordinal));

        // Step 11: Tab to the guide link, underlined; the repository link no longer.
        app.PressKey(Key.Tab);
        await WaitForFocusAsync(app, A.GuideLinkId);
        Assert.True(app.FocusVisible);
        Assert.True(IsUnderlined(TextOf(InSettings<Button>(app, A.GuideLinkId))), "The guide link is not underlined while focused.");
        Assert.False(IsUnderlined(TextOf(InSettings<Button>(app, A.RepositoryLinkId))), "The repository link is still underlined.");
        Assert.Single(app.Launcher.Requests);

        // Step 12: Esc closes the screen; About saved nothing.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        Assert.Single(app.Launcher.Requests);
        Assert.False(File.Exists(SettingsFile(data)), "About wrote settings.json.");
    }

    /// <summary>Method 2, A2: About in the light theme, reached by the toolbar's Settings and a click on its button.</summary>
    [AvaloniaFact]
    public async Task Check_A2_about_light()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data, theme: AppTheme.Light);
        var before = File.ReadAllBytes(SettingsFile(data));

        // Step 2: the toolbar's Settings: General, light.
        var screen = await app.OpenSettingsAsync(SettingsOpening.Button);
        Assert.Equal(SettingsSection.General, screen.SelectedSection);
        Assert.Contains("selected", InSettings<Button>(app, "SettingsNavGeneral").Classes);
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);

        // Step 3: a click on About.
        app.ChooseSection(SettingsSection.About);
        await WaitForAboutAsync(app);
        app.MoveToNeutral();
        var picture = Picture(app, A.A2aAboutLight);
        AssertSectionList(app, picture, full: false);
        AssertAbout(app, picture, full: false);

        // Step 4: Esc; nothing written, nothing launched.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        Assert.Equal(before, File.ReadAllBytes(SettingsFile(data)));
        Assert.Empty(app.Launcher.Requests);
    }

    /// <summary>Method 3, A3: About at 1920×1080 by Ctrl+,, five Downs and Enter (the twin of A1's capture at that size).</summary>
    [AvaloniaFact]
    public async Task Check_A3_about_1920()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data, FullSize.Width, FullSize.Height);

        // Step 2: Ctrl+, on General, its button focused.
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        await WaitForFocusAsync(app, "SettingsNavGeneral");
        AssertCard(app, full: true);
        Assert.Equal(SettingsSection.General, screen.SelectedSection);

        // Step 3: five Downs, each waited on by the focus moving on; the shown section stays General.
        foreach (var id in A.NavIds.Skip(1))
        {
            app.PressKey(Key.Down);
            await WaitForFocusAsync(app, id);
        }

        Assert.True(app.FocusVisible, "SettingsNavAbout has the focus without the keyboard's frame.");
        Assert.Equal(SettingsSection.General, screen.SelectedSection);

        // Step 4: Enter shows About, the focus staying on its button.
        app.PressKey(Key.Enter);
        await WaitForAboutAsync(app);
        var picture = Picture(app, A.A3aAbout1920);
        Assert.Equal(A.NavAboutId, app.FocusedAutomationId());
        Assert.True(app.FocusVisible);
        AssertSectionList(app, picture, full: true);
        AssertAbout(app, picture, full: true);

        // Step 5: Esc; nothing written, nothing launched.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        Assert.False(File.Exists(SettingsFile(data)), "About wrote settings.json.");
        Assert.Empty(app.Launcher.Requests);
    }

    /// <summary>Method 4, A5: the third-party notices: within 1 s, the end by End, Avalonia's file by its switch button, Close.</summary>
    [AvaloniaFact]
    public async Task Check_A5_notices()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var colours = A.Dark;

        // Step 2: Ctrl+, then a click on About.
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.About);
        await WaitForAboutAsync(app);

        // Step 3: Show third-party notices…, timed from the click to the dialog laid out.
        var notices = RepositoryText(A.NoticesFile);
        var clock = Stopwatch.StartNew();
        app.Click(InSettings<Button>(app, A.ShowNoticesId));
        await app.WaitForAsync(
            () => app.TopDialogView is TextDocumentDialogView
                && InTextDialog<Control>(app, A.SwitchId).IsVisible
                && DocumentText(app) is { } shown && shown.Document?.Text == notices
                && shown.TextArea.TextView.VisualLinesValid && shown.TextArea.TextView.GetVisualLine(1) is not null,
            "the notices laid out from their first line");
        clock.Stop();
        var took = clock.Elapsed;
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"A5: the notices were laid out {took.TotalMilliseconds:0} ms after the click."));
        await app.WaitForAsync(() => FocusInText(app), "the focus in the notices' text");
        var top = Picture(app, A.A5aNoticesTop);
        AssertTextDialog(app, top, A.NoticesTitle);
        AssertSwitch(app, top, A.NoticesSwitchIds[0]);
        var text = DocumentText(app);
        Assert.Equal(A.TextBelowSwitch, app.BoundsOf(text).Top - app.BoundsOf(InTextDialog<Control>(app, A.SwitchId)).Bottom, 0.5);
        Assert.Equal(0, text.ScrollOffset.Y);
        Assert.Equal(1, LinesInView(text).First);
        AssertInk(top, FirstLineRect(app, text), colours.Primary, colours.Control, "the notices' first line");
        Assert.Empty(app.Launcher.Requests);

        // Step 4: End: the notices' end at the view's bottom; a timeout names the offset, the extent
        // and the viewport as they were when the wait gave up.
        app.PressKey(Key.End);
        try
        {
            await app.WaitForAsync(() => AtEnd(text), "the notices scrolled to their end");
        }
        catch (TimeoutException timeout)
        {
            throw new TimeoutException($"{timeout.Message} ({ScrollState(text)})", timeout);
        }

        var end = Picture(app, A.A5bNoticesEnd);
        AssertAtTheEnd(text, notices);
        AssertSwitch(app, end, A.NoticesSwitchIds[0]);

        // Step 5: Avalonia's file, from its top.
        var avalonia = RepositoryText(A.AvaloniaNoticeFile);
        app.Click(InTextDialog<Button>(app, A.NoticesSwitchIds[1]));
        await app.WaitForAsync(
            () => text.Document?.Text == avalonia && text.TextArea.TextView.VisualLinesValid && text.TextArea.TextView.GetVisualLine(1) is not null,
            "Avalonia's file laid out");
        var picture = Picture(app, A.A5cNoticesAvalonia);
        AssertSwitch(app, picture, A.NoticesSwitchIds[1]);
        Assert.Equal(A.NoticesTitle, InTextDialog<TextBlock>(app, A.DialogTitleId).Text);
        Assert.Equal(0, text.ScrollOffset.Y);
        Assert.Equal(1, LinesInView(text).First);

        // Step 6: Close: the focus back on Show third-party notices….
        app.Click(InTextDialog<Button>(app, A.CloseId));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SettingsDialogViewModel, "the text dialog to close");
        await WaitForFocusAsync(app, A.ShowNoticesId);
        Assert.True(app.SettingsDialog is { ShowsAbout: true });
        Assert.Null(app.Clipboard.Text);
        Assert.Empty(app.Launcher.Requests);

        // Step 7: Esc.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        Assert.False(File.Exists(SettingsFile(data)), "About wrote settings.json.");

        // Step 3's bound, asserted last, so that a slow opening still lets the steps after it run and
        // save their pictures; the time is in the output either way.
        Assert.True(took <= A.NoticesWithin, string.Create(CultureInfo.InvariantCulture, $"The notices took {took.TotalMilliseconds:0} ms from the click, past {A.NoticesWithin.TotalMilliseconds:0} ms."));
    }

    /// <summary>Method 5, A6: "About VisualCommit…" on a repository tab; nothing in the repository changes.</summary>
    [AvaloniaFact]
    public async Task Check_A6_palette_entry_on_a_repository()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        app.MoveToNeutral();

        // Step 2: the palette, "about": one row, highlighted.
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette open with its box focused");
        app.TypeInPalette(A.PaletteQuery);
        await app.WaitForAsync(() => app.Palette.Shown.Count == 1, "one row for \"about\"");
        var picture = Picture(app, A.A6aPaletteAbout);
        var row = AssertPaletteRow(app);
        AssertColour(A.Dark.Selection, picture, new Point(row.Bounds.Right - 8, row.Bounds.Center.Y), "highlighted row");
        Assert.Equal(A.Dark.Primary, row.LabelColour);
        Assert.Equal(84, app.PaletteSnapshot().CardBounds.Height, 0.5);
        Assert.Empty(app.Launcher.Requests);

        // Step 3: Enter: About, its button focused with the frame.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen && app.SettingsDialog is not null, "the palette closed and the settings screen open");
        await WaitForAboutAsync(app);
        await WaitForFocusAsync(app, A.NavAboutId);
        Assert.True(app.FocusVisible);
        Assert.Contains("selected", InSettings<Button>(app, A.NavAboutId).Classes);
        AssertAboutTexts(app);

        // Step 4: Esc; the repository as it was.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.False(File.Exists(SettingsFile(data)), "About wrote settings.json.");
        Assert.Empty(app.Launcher.Requests);
    }

    /// <summary>
    /// The palette's one row for "about": <c>About VisualCommit…</c>, highlighted, enabled, no detail,
    /// no key (none shown, no accelerator key), not drawn as danger; "No matches" hidden.
    /// </summary>
    private static PaletteRow AssertPaletteRow(ShellDriver app)
    {
        var snapshot = app.PaletteSnapshot();
        Assert.Equal(A.PaletteQuery, app.PaletteBox.Text);
        Assert.False(snapshot.ShowsNothing);
        var row = Assert.Single(snapshot.Rows);
        Assert.Equal(A.PaletteCommand, row.Label);
        Assert.True(row.IsHighlighted, "The row is not highlighted.");
        Assert.True(row.IsEnabled, "The row is disabled.");
        Assert.Null(row.Detail);
        Assert.Null(row.KeyText);
        var item = app.PaletteView.GetVisualDescendants().OfType<ListBoxItem>().Single(container => container.IsEffectivelyVisible && container.DataContext is PaletteEntry);
        Assert.Equal("CommandPaletteItem", AutomationProperties.GetAutomationId(item));
        Assert.Equal(A.PaletteCommand, AutomationProperties.GetName(item));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetAcceleratorKey(item)), "The row has a key.");
        Assert.DoesNotContain("danger", item.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CommandPaletteItemLabel").Classes);
        Assert.False(app.FindByAutomationId(app.PaletteView, "CommandPaletteEmpty").IsEffectivelyVisible, "\"No matches\" shows.");
        return row;
    }

    /// <summary>
    /// The notices' switch: its five flat buttons 28 high, 4 apart, in one row 12 below the title, in
    /// order with their labels; <paramref name="shown"/> alone selected, its background the control
    /// colour at a point clear of its text.
    /// </summary>
    private static void AssertSwitch(ShellDriver app, Screenshot picture, string shown)
    {
        var switchPanel = InTextDialog<Control>(app, A.SwitchId);
        Assert.True(switchPanel.IsVisible);
        Assert.Equal(A.SwitchBelowTitle, app.BoundsOf(switchPanel).Top - app.BoundsOf(InTextDialog<TextBlock>(app, A.DialogTitleId)).Bottom, 0.5);
        var buttons = A.NoticesSwitchIds.Select(id => InTextDialog<Button>(app, id)).ToList();
        Assert.Equal(A.NoticesSwitch, buttons.Select(button => button.Content as string));
        Assert.Equal(A.NoticesSwitch, buttons.Select(button => AutomationProperties.GetName(button)));
        var flat = Application.Current!.FindResource("VcFlatButton");
        for (var i = 0; i < buttons.Count; i++)
        {
            var bounds = app.BoundsOf(buttons[i]);
            Assert.Same(flat, buttons[i].Theme);
            Assert.Equal(A.SwitchButtonHeight, bounds.Height);
            Assert.Equal(app.BoundsOf(buttons[0]).Top, bounds.Top);
            if (i > 0)
            {
                Assert.Equal(A.SwitchButtonGap, bounds.Left - app.BoundsOf(buttons[i - 1]).Right, 0.5);
            }

            var selected = A.NoticesSwitchIds[i] == shown;
            Assert.True(buttons[i].Classes.Contains("selected") == selected, $"{A.NoticesSwitchIds[i]} is {(selected ? "not " : string.Empty)}selected.");
        }

        var shownBounds = app.BoundsOf(buttons[A.NoticesSwitchIds.ToList().IndexOf(shown)]);
        AssertColour(ColoursOf(app).Control, picture, new Point(shownBounds.Left + 3, shownBounds.Center.Y), "shown text's switch button");
    }

    /// <summary>
    /// End's place: the document's last line, the empty one after the file's final line end, is the
    /// view's last visual line, whole; the file's last line that is not empty shows whole with only
    /// empty lines below it; its first line is out of view.
    /// </summary>
    private static void AssertAtTheEnd(DocumentTextView text, string file)
    {
        var document = text.Document;
        Assert.Equal(string.Empty, document.GetText(document.Lines[^1]));
        var (first, last) = LinesInView(text);
        Assert.Equal(document.LineCount, last);
        var lastWritten = document.Lines.Last(line => document.GetText(line).Length > 0);
        Assert.Equal(file.TrimEnd('\n').Split('\n')[^1], document.GetText(lastWritten));
        Assert.True(lastWritten.LineNumber >= first, $"The file's last line ({lastWritten.LineNumber}) is not in view (lines {first} to {last}).");
        Assert.True(first > 1, "The file's first line is still in view.");
        var textView = text.TextArea.TextView;
        Assert.Equal(textView.VisualLines[^1].LastDocumentLine.LineNumber, document.LineCount);
    }
}
