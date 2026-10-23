// Part S's own helpers (docs/test-reports/phase-6.md, "Conventions of part S"): the start with
// the session's global file seeded, the rest point (SD2), the settings screen as drawn (its card,
// its fields, its Pick dialogs), the screen's reads and "no write" in the call log (SD29), git's
// configuration files and settings.json read back, the toolbar's Settings and Theme, and the end of
// an operation. Only part S's writer edits this file; what every part shares is in
// Phase6Checks.Support.cs.
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>
    /// A check's layout (part S's "Positions"): each number the report states (±2 unless said) and
    /// each layout rule (apart, in one row, inside its column, not cut), measured when its step runs
    /// and asserted at the method's end (<see cref="AssertNone"/>), so that a number that differs does
    /// not hide the later steps and their pictures. Texts, states and colours are asserted at once.
    /// </summary>
    private sealed class Layout
    {
        private readonly List<string> _problems = [];

        /// <summary>The step the next findings belong to, for their messages (<c>step 2</c>).</summary>
        public string Step { get; set; } = "the start";

        /// <summary>A number of the report: <paramref name="actual"/> within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
        public void Near(double expected, double actual, string what, double tolerance = 2)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                _problems.Add(string.Create(CultureInfo.InvariantCulture, $"{Step}: {what} is {actual}; the report gives {expected} (±{tolerance})."));
            }
        }

        /// <summary>A layout rule that must hold.</summary>
        public void Check(bool holds, string what)
        {
            if (!holds)
            {
                _problems.Add($"{Step}: {what}");
            }
        }

        /// <summary>Asserts that every number and rule held, naming each that did not.</summary>
        public void AssertNone() =>
            Assert.True(_problems.Count == 0, "The layout differs from the report's:\n" + string.Join('\n', _problems));
    }

    // ---- Starts ----

    /// <summary>
    /// Part S's default start (its conventions' "Defaults"): the session's global file seeded with
    /// <paramref name="seed"/> when given (<see cref="ShellDriver.SeedGlobalGitConfig"/>), a session
    /// of <paramref name="tabs"/> (the copy alone when null; null entries are empty tabs), the app at
    /// the given size in the dark theme with the fake system dark, waited for until the active tab's
    /// graph has drawn its rows, its status is shown and its queue is idle, the pointer at the rest
    /// point (SD2).
    /// </summary>
    private static async Task<ShellDriver> StartAsync(
        TempDirectory data,
        TempRepo repo,
        string? seed = null,
        IReadOnlyList<string?>? tabs = null,
        int width = 1100,
        int height = 700,
        bool systemDark = true)
    {
        if (seed is not null)
        {
            ShellDriver.SeedGlobalGitConfig(data.Path, seed);
        }

        var app = ShellDriver.StartWithTabs(data.Path, tabs ?? [repo.Path], width: width, height: height, systemDark: systemDark);
        try
        {
            await app.WaitForRepositoryAsync(OperationChainTimeout);
            Rest(app);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The rest point (SD2): (760, 687) at 1100×700, on the backdrop below the card (760, 1000) at 1920×1080.</summary>
    private static Point RestPoint(ShellDriver app) => app.Window.ClientSize.Height >= 1000 ? PartS.RestAt1920 : PartS.RestAt1100;

    /// <summary>Moves the pointer to the rest point (SD2).</summary>
    private static void Rest(ShellDriver app) => app.MoveMouse(RestPoint(app));

    /// <summary>
    /// A picture of part S (<see cref="Phase6Checks.Shot"/>) with the pointer at the rest point (SD2):
    /// the support's neutral point at 1100×700; at 1920×1080 (760, 1000), where nothing has a
    /// tooltip, the pointer then kept there.
    /// </summary>
    private static Screenshot ShotAtRest(ShellDriver app, string name)
    {
        if (RestPoint(app) == PartS.RestAt1100)
        {
            return Shot(app, name);
        }

        Rest(app);
        return Shot(app, name, keepPointer: true);
    }

    // ---- The settings screen ----

    /// <summary>The open settings screen's view model; fails when it is not open.</summary>
    private static SettingsDialogViewModel Screen(ShellDriver app) =>
        app.SettingsDialog ?? throw new Xunit.Sdk.XunitException("The settings screen is not open.");

    /// <summary>A control of the settings screen by its name (its view's <c>x:Name</c>).</summary>
    private static T Named<T>(ShellDriver app, string name)
        where T : Control =>
        app.Find<T>(name);

    /// <summary>The shown field <paramref name="id"/> of <paramref name="snapshot"/>; fails when it does not show.</summary>
    private static SettingsFieldSnapshot Field(SettingsSnapshot snapshot, string id) =>
        snapshot.Field(id) ?? throw new Xunit.Sdk.XunitException($"The settings screen shows no {id}. It shows: {string.Join(", ", snapshot.Fields.Select(field => field.Id).Distinct())}.");

    /// <summary>Asserts the text of the shown field <paramref name="id"/>.</summary>
    private static void AssertText(SettingsSnapshot snapshot, string id, string expected) =>
        Assert.True(Field(snapshot, id).Text == expected, $"{id} reads \"{Field(snapshot, id).Text}\", expected \"{expected}\".");

    /// <summary>Asserts that no field <paramref name="id"/> shows.</summary>
    private static void AssertHidden(SettingsSnapshot snapshot, string id) =>
        Assert.False(snapshot.Shows(id), $"The settings screen shows {id}: \"{snapshot.Field(id)?.Text}\".");

    /// <summary>Asserts a shown check box's or radio's check state.</summary>
    private static void AssertChecked(SettingsSnapshot snapshot, string id, bool expected) =>
        Assert.True(Field(snapshot, id).IsChecked == expected, $"{id} is {(Field(snapshot, id).IsChecked == true ? "checked" : "unchecked")}, expected {(expected ? "checked" : "unchecked")}.");

    /// <summary>Asserts a shown text box: its text, its placeholder (when given) and whether it is enabled.</summary>
    private static void AssertBox(SettingsSnapshot snapshot, string id, string text, string? placeholder = null, bool enabled = true)
    {
        var field = Field(snapshot, id);
        Assert.True(field.Text == text, $"{id} holds \"{field.Text}\", expected \"{text}\".");
        if (placeholder is not null)
        {
            Assert.True(field.Placeholder == placeholder, $"{id}'s placeholder is \"{field.Placeholder}\", expected \"{placeholder}\".");
        }

        Assert.True(field.IsEnabled == enabled, $"{id} is {(field.IsEnabled ? "enabled" : "disabled")}.");
    }

    /// <summary>The texts of every shown field <paramref name="id"/> in order (a field's notice lines), against <paramref name="expected"/>.</summary>
    private static void AssertLines(SettingsSnapshot snapshot, string id, params string[] expected)
    {
        var lines = snapshot.Lines(id);
        Assert.True(lines.SequenceEqual(expected), $"{id} shows [{string.Join(" | ", lines)}], expected [{string.Join(" | ", expected)}].");
    }

    /// <summary>The section list's buttons: <paramref name="selected"/> in the <c>selected</c> look and the others not; the error dot at the right of exactly <paramref name="withError"/> (none when empty), with its help text.</summary>
    private static void AssertNav(ShellDriver app, string selected, params string[] withError)
    {
        foreach (var id in new[] { "SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts" })
        {
            var button = Named<Button>(app, id);
            Assert.True(button.Classes.Contains("selected") == (id == selected), $"{id} is {(button.Classes.Contains("selected") ? string.Empty : "not ")}selected; {selected} is the shown section.");
            var dot = button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().FirstOrDefault();
            var shows = dot is { IsEffectivelyVisible: true };
            Assert.True(shows == withError.Contains(id), $"{id}'s error dot (SettingsNavError) {(shows ? "shows" : "does not show")}.");
            if (shows)
            {
                Assert.Equal(PartS.HasError, AutomationProperties.GetHelpText(dot!));
                Assert.Equal(PartS.HasError, AutomationProperties.GetHelpText(button));
                Assert.Equal(PaletteOf(app).Danger, (dot!.Fill as ISolidColorBrush)?.Color);
                Assert.Equal((6.0, 6.0), (dot.Bounds.Width, dot.Bounds.Height));
                Assert.True(app.BoundsOf(dot).Right <= app.BoundsOf(button).Right + 0.5 && app.BoundsOf(dot).X > app.BoundsOf(button).Center.X, $"{id}'s dot is not at its right.");
            }
        }
    }

    /// <summary>The control with the keyboard focus is <paramref name="id"/> (by automation id).</summary>
    private static async Task AssertFocusAsync(ShellDriver app, string id) =>
        await app.WaitForAsync(() => app.FocusedElement is Control focused && AutomationProperties.GetAutomationId(focused) == id, $"the keyboard focus on {id} (it is on {app.FocusedAutomationId() ?? "nothing"})");

    /// <summary>The settings card (<c>Border.dialogCard.settings</c>) exactly at <paramref name="card"/> (7.1), in the panel's colours with a 1-px border; the window behind under the backdrop.</summary>
    private static void AssertCard(ShellDriver app, Rect card)
    {
        var border = Named<Border>(app, "SettingsCard");
        Assert.True(border.Classes.Contains("dialogCard") && border.Classes.Contains("settings"), "The settings card is not Border.dialogCard.settings.");
        Assert.Equal(card, app.BoundsOf(border));
        var palette = PaletteOf(app);
        Assert.Equal(palette.Panel, (border.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Border, (border.BorderBrush as ISolidColorBrush)?.Color);
        Assert.Equal(new Thickness(1), border.BorderThickness);
        var backdrop = border.GetVisualAncestors().OfType<Panel>().First(panel => panel.Classes.Contains("dialogBackdrop"));
        Assert.True(backdrop.IsEffectivelyVisible, "The settings screen has no backdrop.");
        Assert.Equal(palette.Backdrop, (backdrop.Background as ISolidColorBrush)?.Color);
    }

    /// <summary>
    /// The frame of the screen at the card <paramref name="card"/> (7.1, S14 steps 2 and 14): the
    /// card exactly, its texts and structure asserted at once (the title 16 SemiBold, the four
    /// section buttons, Cancel and Save, no <c>SettingsSaving</c>); its other numbers (part S's
    /// "Positions", ±2) into <paramref name="layout"/>: the title from about
    /// (card + 20, card + 20), about 21 high; the body 14 below it, about <paramref name="bodyHeight"/>
    /// high; <c>SettingsNav</c> at the card's left + 20 to + 196, its four buttons 176 × 30, 2 apart,
    /// from about y card + 55, their text 10 in; the section from x card + 216, 622 wide; Cancel and
    /// Save 8 apart on the row <paramref name="buttonsTop"/> to <paramref name="buttonsBottom"/>, Save
    /// ending at about the card's right − 22.
    /// </summary>
    private static void AssertFrame(ShellDriver app, Layout layout, Rect card, double bodyHeight, double buttonsTop, double buttonsBottom)
    {
        AssertCard(app, card);
        var title = Named<TextBlock>(app, "SettingsTitle");
        Assert.Equal(PartS.Title, title.Text);
        Assert.Equal(16, title.FontSize);
        Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        layout.Near(card.X + 20, app.BoundsOf(title).X, "The title's left (about)");
        layout.Near(card.Y + 20, app.BoundsOf(title).Y, "The title's top (about)");
        layout.Near(21, app.BoundsOf(title).Height, "The title's height (about 21)");
        var nav = Named<StackPanel>(app, "SettingsNav");
        var section = Named<ScrollViewer>(app, "SectionScroller");
        layout.Near(app.BoundsOf(title).Bottom + 14, app.BoundsOf(nav).Y, "The body's top, 14 below the title,");
        layout.Near(bodyHeight, app.BoundsOf(section).Height, "The body's height (about)");
        layout.Near(card.X + 20, app.BoundsOf(nav).X, "SettingsNav's left");
        layout.Near(card.X + 196, app.BoundsOf(nav).Right, "SettingsNav's right");
        var buttons = new[] { "SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts" }.Select(id => Named<Button>(app, id)).ToList();
        Assert.Equal(["General", "Git", "Tools", "Keyboard shortcuts"], buttons.Select(button => button.GetVisualDescendants().OfType<TextBlock>().First().Text));
        layout.Near(card.Y + 55, app.BoundsOf(buttons[0]).Y, "The first section button's top (about)");
        for (var index = 0; index < buttons.Count; index++)
        {
            var bounds = app.BoundsOf(buttons[index]);
            layout.Near(176, bounds.Width, $"Section button {index + 1}'s width");
            layout.Near(30, bounds.Height, $"Section button {index + 1}'s height");
            if (index > 0)
            {
                layout.Near(2, bounds.Y - app.BoundsOf(buttons[index - 1]).Bottom, $"The gap above section button {index + 1}");
            }

            var text = buttons[index].GetVisualDescendants().OfType<TextBlock>().First();
            layout.Near(10, app.BoundsOf(text).X - bounds.X, $"Section button {index + 1}'s text inset");
        }

        layout.Near(card.X + 216, app.BoundsOf(section).X, "The section's left");
        layout.Near(622, app.BoundsOf(section).Width, "The section's width");
        var cancel = (Button)app.FindByAutomationId("CancelButton");
        var save = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.Equal((PartS.Cancel, PartS.Save), ((string?)cancel.Content, (string?)save.Content));
        Assert.True(app.BoundsOf(cancel).Right <= app.BoundsOf(save).X, "Cancel is not left of Save.");
        layout.Near(8, app.BoundsOf(save).X - app.BoundsOf(cancel).Right, "The gap between Cancel and Save");
        layout.Near(card.Right - 22, app.BoundsOf(save).Right, "Save's right (about)");
        foreach (var button in new[] { cancel, save })
        {
            layout.Near(buttonsTop, app.BoundsOf(button).Y, $"{button.Content}'s top");
            layout.Near(buttonsBottom, app.BoundsOf(button).Bottom, $"{button.Content}'s bottom");
        }

        Assert.False(Named<TextBlock>(app, "SettingsSaving").IsEffectivelyVisible, "SettingsSaving shows.");
    }

    /// <summary>
    /// General as S14 step 2 gives it (7.3), with the values given: the labels in the 132-wide
    /// column, secondary, each starting in its control's first line; the controls from x 480 (the
    /// section's left + 144); the explanations secondary, 11.5, 4 below; the theme's radios 20 apart;
    /// the minutes' box 48 wide, centred; the extra extensions' box 320 wide; no error or warning; the
    /// section without a scroll bar; no text cut.
    /// </summary>
    private static void AssertGeneral(
        ShellDriver app,
        Layout layout,
        string theme = PartS.ThemeDark,
        bool autoFetch = true,
        string minutes = PartS.DefaultMinutes,
        bool minutesEnabled = true,
        bool keepIgnored = true,
        bool stashUntracked = true,
        string extra = "")
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.General, snapshot.Section);
        foreach (var (id, label) in new[] { ("SettingsThemeDark", PartS.ThemeDark), ("SettingsThemeLight", PartS.ThemeLight), ("SettingsThemeSystem", PartS.ThemeSystem) })
        {
            AssertText(snapshot, id, label);
            AssertChecked(snapshot, id, label == theme);
        }

        AssertText(snapshot, "AutoFetchCheckBox", PartS.AutoFetch);
        AssertChecked(snapshot, "AutoFetchCheckBox", autoFetch);
        AssertBox(snapshot, "AutoFetchMinutesBox", minutes, enabled: minutesEnabled);
        AssertHidden(snapshot, "AutoFetchError");
        AssertText(snapshot, "KeepIgnoredCheckBox", PartS.KeepIgnored);
        AssertChecked(snapshot, "KeepIgnoredCheckBox", keepIgnored);
        AssertText(snapshot, "StashUntrackedCheckBox", PartS.StashUntracked);
        AssertChecked(snapshot, "StashUntrackedCheckBox", stashUntracked);
        AssertText(snapshot, "RunnableIntro", PartS.RunnableIntro);
        AssertText(snapshot, "RunnableList", PartS.RunnableList);
        if (OperatingSystem.IsWindows())
        {
            AssertText(snapshot, "RunnablePathExt", PartS.RunnablePathExt);
        }
        else
        {
            AssertHidden(snapshot, "RunnablePathExt");
        }

        AssertBox(snapshot, "RunnableExtraBox", extra, PartS.RunnablePlaceholder);
        AssertHidden(snapshot, "RunnableError");
        AssertText(snapshot, "RunnableUnix", PartS.RunnableUnix);

        // The layout (7.1, 7.3).
        var general = Named<StackPanel>(app, "SettingsGeneral");
        var section = app.BoundsOf(Named<ScrollViewer>(app, "SectionScroller"));
        var palette = PaletteOf(app);
        var labels = general.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("settingsLabel")).ToList();
        Assert.Equal([PartS.ThemeLabel, PartS.FetchingLabel, PartS.WorkingTreeLabel, PartS.OpeningFilesLabel], labels.Select(label => label.Text));
        foreach (var label in labels)
        {
            var bounds = app.BoundsOf(label);
            layout.Near(section.X, bounds.X, $"the label {label.Text}'s left");
            layout.Check(bounds.Right <= section.X + 132 + 0.5, $"the label {label.Text} passes its 132-wide column.");
            Assert.Equal(palette.TextSecondary, (label.Foreground as ISolidColorBrush)?.Color);
            var grid = (Grid)label.Parent!;
            var controls = (Control)grid.Children.First(child => Grid.GetColumn((Control)child) == 2);
            var first = controls is StackPanel stack ? (Control)stack.Children[0] : controls;
            layout.Near(section.X + 144, app.BoundsOf(first).X, $"{label.Text}'s controls' left");
            layout.Check(bounds.Y >= app.BoundsOf(first).Y - 1 && bounds.Y < app.BoundsOf(first).Bottom, $"the label {label.Text} does not start in its control's first line: label at {bounds.Y}, line {app.BoundsOf(first)}.");
        }

        foreach (var note in general.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("settingsNote") && block.IsEffectivelyVisible))
        {
            Assert.Equal(palette.TextSecondary, (note.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(11.5, note.FontSize);
            var parent = (Panel)note.Parent!;
            var index = parent.Children.IndexOf(note);
            var above = parent.Children.Take(index).LastOrDefault(child => child.IsVisible);
            if (above is not null)
            {
                layout.Near(4, app.BoundsOf(note).Y - app.BoundsOf(above).Bottom, $"the gap above the note \"{note.Text}\"", 1);
            }
        }

        var rows = general.Children.OfType<Grid>().ToList();
        for (var index = 1; index < rows.Count; index++)
        {
            layout.Near(18, app.BoundsOf(rows[index]).Y - app.BoundsOf(rows[index - 1]).Bottom, $"the gap above General's row {index + 1}", 1);
        }

        var dark = app.BoundsOf(Named<RadioButton>(app, "SettingsThemeDark"));
        var light = app.BoundsOf(Named<RadioButton>(app, "SettingsThemeLight"));
        var system = app.BoundsOf(Named<RadioButton>(app, "SettingsThemeSystem"));
        layout.Near(20, light.X - dark.Right, "the gap between Dark and Light", 1);
        layout.Near(20, system.X - light.Right, "the gap between Light and System", 1);
        layout.Check(dark.Y == light.Y && light.Y == system.Y, "the theme's radios are not in a row.");
        var minutesBox = Named<TextBox>(app, "AutoFetchMinutesBox");
        layout.Near(48, minutesBox.Bounds.Width, "AutoFetchMinutesBox's width");
        Assert.Equal(TextAlignment.Center, minutesBox.TextAlignment);
        var minutesLabel = ((StackPanel)minutesBox.Parent!).Children.OfType<TextBlock>().Single();
        Assert.Equal(PartS.Minutes, minutesLabel.Text);
        var check = app.BoundsOf(Named<CheckBox>(app, "AutoFetchCheckBox"));
        layout.Check(Math.Abs(check.Center.Y - app.BoundsOf(minutesBox).Center.Y) <= 1 && Math.Abs(app.BoundsOf(minutesLabel).Center.Y - app.BoundsOf(minutesBox).Center.Y) <= 1, "the Fetching row is not on one line.");
        layout.Near(10, app.BoundsOf(Named<CheckBox>(app, "StashUntrackedCheckBox")).Y - app.BoundsOf(((Panel)Named<CheckBox>(app, "KeepIgnoredCheckBox").Parent!).Children[1]).Bottom, "the gap above StashUntrackedCheckBox", 1);
        layout.Near(320, Named<TextBox>(app, "RunnableExtraBox").Bounds.Width, "RunnableExtraBox's width");
        var list = Named<ItemsControl>(app, "RunnableList");
        var code = (FontFamily)Application.Current!.FindResource("VcCodeFontFamily")!;
        Assert.All(list.GetVisualDescendants().OfType<TextBlock>(), block => Assert.Equal(code, block.FontFamily));
        Assert.Equal(PartS.AlsoAskFor, ((StackPanel)Named<TextBox>(app, "RunnableExtraBox").Parent!).Children.OfType<TextBlock>().Single().Text);
        AssertNoErrorOrWarning(app);
        AssertFits(app, layout);
        AssertNoClippedText(app, layout);
    }

    /// <summary>No error and no warning line shows in the shown section.</summary>
    private static void AssertNoErrorOrWarning(ShellDriver app)
    {
        var section = Named<ScrollViewer>(app, "SectionScroller");
        var shown = section.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && (block.Classes.Contains("dialogError") || block.Classes.Contains("dialogWarning")))
            .Select(block => $"{AutomationProperties.GetAutomationId(block)}: {block.Text}")
            .ToList();
        Assert.True(shown.Count == 0, "The section shows errors or warnings: " + string.Join("; ", shown));
    }

    /// <summary>The shown section fits without a scroll bar: its extent within its viewport (a layout rule).</summary>
    private static void AssertFits(ShellDriver app, Layout layout)
    {
        var section = Named<ScrollViewer>(app, "SectionScroller");
        layout.Check(section.Extent.Height <= section.Viewport.Height + 0.5, string.Create(CultureInfo.InvariantCulture, $"the section scrolls: {section.Extent.Height} high in {section.Viewport.Height}."));
    }

    /// <summary>No text in the window is cut off, but shortened text with its whole in a tooltip (phase 2's rule; a layout rule).</summary>
    private static void AssertNoClippedText(ShellDriver app, Layout layout)
    {
        var problems = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        layout.Check(problems.Count == 0, "clipped text: " + string.Join("; ", problems));
    }

    /// <summary>The shown section's content height (its extent), for the report's "about" heights.</summary>
    private static double SectionHeight(ShellDriver app) => Named<ScrollViewer>(app, "SectionScroller").Extent.Height;

    /// <summary>
    /// Git in the global scope as S14 step 4 gives it (7.4), with the values given: the scope switch
    /// (28 high, global selected, This repository enabled with its tooltip), the caption 6 below with
    /// the global file's path as its tooltip and help text, the boxes 14 below, the six radios 8
    /// apart 18 below, the merge tool 18 below; no <c>IdentityInEffect</c>, no <c>GitReading</c>.
    /// </summary>
    private static void AssertGitGlobal(
        ShellDriver app,
        Layout layout,
        TempDirectory data,
        TempRepo? repo,
        string name = "",
        string email = "",
        string checkedPull = "PullNotSet",
        string mergeTool = PartS.NotSet,
        string[]? nameOrigins = null,
        string[]? emailOrigins = null,
        string[]? pullNotices = null,
        string? repositoryFolder = null)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Git, snapshot.Section);
        AssertScopeSwitch(app, layout, snapshot, SettingsScope.Global, repo is not null, repositoryFolder);
        AssertText(snapshot, "GitScopeCaption", PartS.CaptionGlobal);
        var caption = Field(snapshot, "GitScopeCaption");
        var file = ShellDriver.GlobalGitConfigPath(data.Path);
        Assert.True(caption.ToolTip is { } tip && GitFileNames.SamePath(tip, file), $"The global caption's tooltip is \"{caption.ToolTip}\", not the global file {file}.");
        Assert.Equal(caption.ToolTip, caption.HelpText);
        Assert.Equal(PaletteOf(app).TextSecondary, (Named<TextBlock>(app, "GitScopeCaption").Foreground as ISolidColorBrush)?.Color);
        AssertBox(snapshot, "UserNameBox", name, PartS.NotSet);
        AssertBox(snapshot, "UserEmailBox", email, PartS.NotSet);
        AssertLines(snapshot, "UserNameOrigin", nameOrigins ?? []);
        AssertLines(snapshot, "UserEmailOrigin", emailOrigins ?? []);
        AssertHidden(snapshot, "UserNameError");
        AssertHidden(snapshot, "UserEmailError");
        AssertHidden(snapshot, "UserEmailWarning");
        AssertHidden(snapshot, "IdentityInEffect");
        AssertHidden(snapshot, "IdentityNotices");
        AssertPullRadios(snapshot, PartS.NotSet, PartS.PullNotSetGlobalTip, checkedPull);
        AssertHidden(snapshot, "PullOther");
        AssertLines(snapshot, "PullNotices", pullNotices ?? []);
        AssertHidden(snapshot, "PullInEffect");
        AssertHidden(snapshot, "BranchRebaseNotice");
        AssertText(snapshot, "MergeToolValue", mergeTool);
        AssertMergeToolChoose(snapshot);
        AssertHidden(snapshot, "GitReading");
        AssertGitLayout(app, layout);
    }

    /// <summary>The scope switch (7.4): two buttons 28 high, <paramref name="shown"/> in the <c>selected</c> look; This repository enabled with <c>{name} — {folder}</c>, or disabled with its reason.</summary>
    private static void AssertScopeSwitch(ShellDriver app, Layout layout, SettingsSnapshot snapshot, SettingsScope shown, bool repositoryEnabled, string? repositoryFolder)
    {
        var global = Named<Button>(app, "GitScopeGlobal");
        var repository = Named<Button>(app, "GitScopeRepository");
        Assert.Equal(PartS.ScopeGlobal, global.Content);
        Assert.Equal(PartS.ScopeRepository, repository.Content);
        layout.Near(28, global.Bounds.Height, "GitScopeGlobal's height");
        layout.Near(28, repository.Bounds.Height, "GitScopeRepository's height");
        Assert.Equal(shown == SettingsScope.Global, global.Classes.Contains("selected"));
        Assert.Equal(shown == SettingsScope.Repository, repository.Classes.Contains("selected"));
        var field = Field(snapshot, "GitScopeRepository");
        Assert.Equal(repositoryEnabled, field.IsEnabled);
        if (repositoryEnabled)
        {
            var prefix = PartS.Settings + " — ";
            Assert.True(field.ToolTip is { } tip && tip.StartsWith(prefix, StringComparison.Ordinal) && repositoryFolder is not null && WorkspacePaths.Same(tip[prefix.Length..], repositoryFolder), $"This repository's tooltip is \"{field.ToolTip}\", not \"{prefix}{repositoryFolder}\".");
        }
        else
        {
            Assert.Equal(PartS.ScopeRepositoryDisabled, field.ToolTip);
            Assert.True(ToolTip.GetShowOnDisabled(repository) || field.ToolTip is not null, "The disabled This repository shows no reason.");
        }

        Assert.Equal(field.ToolTip, field.HelpText);
    }

    /// <summary>The pull radios (7.4, 8.7): <c>PullNotSet</c> labelled and tooltipped as the scope gives it, then the five others, one per line, 8 apart; only <paramref name="checkedId"/> checked.</summary>
    private static void AssertPullRadios(SettingsSnapshot snapshot, string notSetLabel, string notSetTip, string checkedId)
    {
        AssertText(snapshot, "PullNotSet", notSetLabel);
        Assert.Equal(notSetTip, Field(snapshot, "PullNotSet").ToolTip);
        AssertChecked(snapshot, "PullNotSet", checkedId == "PullNotSet");
        foreach (var (id, label) in PartS.PullRadios)
        {
            AssertText(snapshot, id, label);
            AssertChecked(snapshot, id, checkedId == id);
        }

        if (checkedId == "PullOther")
        {
            AssertChecked(snapshot, "PullOther", true);
        }
    }

    /// <summary><c>MergeToolChooseButton</c> <c>Choose…</c>, enabled (the fixed list there at once, D202), and <c>MergeToolNote</c>.</summary>
    private static void AssertMergeToolChoose(SettingsSnapshot snapshot)
    {
        AssertText(snapshot, "MergeToolChooseButton", PartS.Choose);
        Assert.True(Field(snapshot, "MergeToolChooseButton").IsEnabled, "Choose… of the merge tool is disabled.");
        AssertText(snapshot, "MergeToolNote", PartS.MergeToolNote);
    }

    /// <summary>The Git section's spacing (7.4): the caption 6 below the switch, the identity 14 below it, the Pull and Merge tool rows 18 below what comes before; the radios one per line, 8 apart.</summary>
    private static void AssertGitLayout(ShellDriver app, Layout layout)
    {
        var scope = app.BoundsOf(Named<StackPanel>(app, "GitScope"));
        var caption = app.BoundsOf(Named<TextBlock>(app, "GitScopeCaption"));
        layout.Near(6, caption.Y - scope.Bottom, "the gap above GitScopeCaption", 1);
        var name = app.BoundsOf(Named<TextBox>(app, "UserNameBox"));
        layout.Near(14, name.Y - caption.Bottom, "the gap above the identity", 1);
        var radios = new[] { "PullNotSet", "PullMerge", "PullMergeCommit", "PullRebase", "PullRebaseMerges", "PullFastForwardOnly" }.Select(id => app.BoundsOf(Named<RadioButton>(app, id))).ToList();
        for (var index = 1; index < radios.Count; index++)
        {
            layout.Near(8, radios[index].Y - radios[index - 1].Bottom, $"the gap above pull radio {index + 1}", 1);
            layout.Near(radios[0].X, radios[index].X, $"pull radio {index + 1}'s left", 0.5);
        }

        var pullRow = (Control)Named<RadioButton>(app, "PullNotSet").GetVisualAncestors().OfType<Grid>().First();
        var mergeRow = (Control)Named<TextBlock>(app, "MergeToolValue").GetVisualAncestors().OfType<Grid>().First();
        var beforePull = ((Panel)pullRow.Parent!).Children.Take(((Panel)pullRow.Parent!).Children.IndexOf(pullRow)).Last(child => child.IsVisible);
        layout.Near(18, app.BoundsOf(pullRow).Y - app.BoundsOf(beforePull).Bottom, "the gap above the Pull row", 1);
        layout.Near(18, app.BoundsOf(mergeRow).Y - app.BoundsOf(pullRow).Bottom, "the gap above the Merge tool row", 1);
    }

    /// <summary>
    /// Git in "This repository" (7.4), with the values given: the caption, the boxes and their
    /// placeholders, <c>IdentityInEffect</c> and <c>IdentityNotices</c>, the pull radios with
    /// <c>Same as global</c>, <c>PullInEffect</c>, the merge tool's <c>Same as global (…)</c>.
    /// </summary>
    private static void AssertGitRepository(
        ShellDriver app,
        Layout layout,
        string? repositoryFolder,
        string name,
        string? namePlaceholder,
        string email,
        string? emailPlaceholder,
        string identity,
        string[] identityNotices,
        string checkedPull,
        string pullInEffect,
        string mergeTool = PartS.MergeToolSameAsGlobalNotSet)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Git, snapshot.Section);
        AssertScopeSwitch(app, layout, snapshot, SettingsScope.Repository, repositoryEnabled: true, repositoryFolder);
        AssertText(snapshot, "GitScopeCaption", PartS.CaptionRepository);
        AssertBox(snapshot, "UserNameBox", name, namePlaceholder);
        AssertBox(snapshot, "UserEmailBox", email, emailPlaceholder);
        AssertLines(snapshot, "UserNameOrigin");
        AssertLines(snapshot, "UserEmailOrigin");
        AssertHidden(snapshot, "UserNameError");
        AssertHidden(snapshot, "UserEmailError");
        AssertHidden(snapshot, "UserEmailWarning");
        AssertText(snapshot, "IdentityInEffect", identity);
        Assert.Equal(PaletteOf(app).TextSecondary, (Named<TextBlock>(app, "IdentityInEffect").Foreground as ISolidColorBrush)?.Color);
        AssertLines(snapshot, "IdentityNotices", identityNotices);
        AssertPullRadios(snapshot, PartS.PullSameAsGlobal, PartS.PullNotSetRepositoryTip, checkedPull);
        AssertText(snapshot, "PullInEffect", pullInEffect);
        AssertText(snapshot, "MergeToolValue", mergeTool);
        AssertMergeToolChoose(snapshot);
        AssertHidden(snapshot, "GitReading");
    }

    /// <summary>Clicks a control of the settings screen by its automation id, as a user would.</summary>
    private static void ClickField(ShellDriver app, string id)
    {
        var control = app.SettingsSnapshot().Field(id) is null
            ? throw new Xunit.Sdk.XunitException($"The settings screen shows no {id} to click.")
            : app.Window.GetVisualDescendants().OfType<Control>().First(candidate => AutomationProperties.GetAutomationId(candidate) == id && candidate.IsEffectivelyVisible);
        app.Click(control);
    }

    /// <summary>Waits until the settings screen has closed (a Save that closed it, Cancel, Esc).</summary>
    private static Task WaitForScreenClosedAsync(ShellDriver app, SettingsDialogViewModel screen) =>
        app.WaitForAsync(() => screen.IsClosed && app.SettingsDialog is null, "the settings screen to close");

    /// <summary>Presses Esc and waits until the screen has closed.</summary>
    private static async Task EscapeScreenAsync(ShellDriver app)
    {
        var screen = Screen(app);
        app.PressKey(Avalonia.Input.Key.Escape);
        await WaitForScreenClosedAsync(app, screen);
    }

    // ---- The Pick dialog (7.7) ----

    /// <summary>One item of the open Pick dialog as drawn: its text, its detail (null when hidden), its automation name and help text, whether it is selected, its bounds.</summary>
    private sealed record PickRow(string Text, string? Detail, string? Name, string? HelpText, bool Selected, Rect Bounds, Rect TextBounds, Rect? DetailBounds, Color? DetailColour, double? DetailSize);

    /// <summary>The open Pick dialog's items, top to bottom.</summary>
    private static List<PickRow> PickRows(ShellDriver app)
    {
        app.Settle();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var list = (ListBox)app.FindByAutomationId(view, "PickList");
        return [.. list.GetVisualDescendants().OfType<ListBoxItem>()
            .Where(item => item.IsEffectivelyVisible && item.DataContext is string)
            .OrderBy(item => app.BoundsOf(item).Y)
            .Select(item =>
            {
                var text = item.GetVisualDescendants().OfType<TextBlock>().First(block => block is not TrimmedTextBlock);
                var detail = item.GetVisualDescendants().OfType<TrimmedTextBlock>().FirstOrDefault(block => block.Name == "PickItemDetail" && block.IsEffectivelyVisible);
                return new PickRow(
                    (string)item.DataContext!,
                    detail?.Text,
                    AutomationProperties.GetName(item),
                    AutomationProperties.GetHelpText(item),
                    item.IsSelected,
                    app.BoundsOf(item),
                    app.BoundsOf(text),
                    detail is null ? null : app.BoundsOf(detail),
                    (detail?.Foreground as ISolidColorBrush)?.Color,
                    detail?.FontSize);
            })];
    }

    /// <summary>
    /// The Pick dialog on top as 7.7 and S14 step 8 give it: <c>PickDialog</c> stacked over the
    /// settings screen (both backdrops drawn, the settings card behind), the wide card (460),
    /// <paramref name="title"/>, <c>PickFilter</c> focused and empty, the items (text — detail) in
    /// order, each 28 high, its text whole, its detail 12 to its right in the secondary colour at
    /// 11.5, named by its text with its detail as help text, <paramref name="selected"/> selected,
    /// all in view without scrolling; Cancel and Choose.
    /// </summary>
    private static async Task AssertPickAsync(ShellDriver app, Layout layout, string title, IReadOnlyList<(string Text, string Detail)> options, string selected)
    {
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The Pick dialog has no view.");
        Assert.Equal("PickDialog", AutomationProperties.GetAutomationId(view));
        Assert.NotNull(app.SettingsDialog);
        Assert.Equal(2, app.Session.Services.DialogHost.Open.Count);
        Assert.True(Named<Border>(app, "SettingsCard").IsEffectivelyVisible, "The settings card is not drawn behind the Pick dialog.");
        Assert.Equal(2, app.Window.GetVisualDescendants().OfType<Panel>().Count(panel => panel.Classes.Contains("dialogBackdrop") && panel.IsEffectivelyVisible));
        var card = view.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard"));
        Assert.True(card.Classes.Contains("wide"), "The Pick dialog is not on the wide card.");
        layout.Near(460, app.BoundsOf(card).Width, "the Pick dialog's card's width", 1);
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "PickTitle")).Text);
        var filter = (TextBox)app.FindByAutomationId(view, "PickFilter");
        await app.WaitForAsync(() => filter.IsFocused, "the Pick dialog's filter to have the keyboard focus (SD6)");
        Assert.Equal(string.Empty, filter.Text ?? string.Empty);
        var rows = PickRows(app);
        Assert.True(
            rows.Select(row => (row.Text, row.Detail ?? string.Empty)).SequenceEqual(options),
            "The Pick dialog's options are not the expected ones.\nExpected:\n  " + string.Join("\n  ", options.Select(option => $"{option.Text} — {option.Detail}")) + "\nActual:\n  " + string.Join("\n  ", rows.Select(row => $"{row.Text} — {row.Detail}")));
        var list = (ListBox)app.FindByAutomationId(view, "PickList");
        var palette = PaletteOf(app);
        foreach (var row in rows)
        {
            layout.Near(28, row.Bounds.Height, $"the option {row.Text}'s height", 0.5);
            Assert.Equal(row.Text, row.Name);
            Assert.Equal(options.First(option => option.Text == row.Text).Detail, row.HelpText);
            Assert.Equal(row.Text == selected, row.Selected);
            Assert.Equal(palette.TextSecondary, row.DetailColour);
            Assert.Equal(11.5, row.DetailSize);
            layout.Near(12, row.DetailBounds!.Value.X - row.TextBounds.Right, $"the gap before the option {row.Text}'s detail", 1);
            layout.Check(row.Bounds.Bottom <= app.BoundsOf(list).Bottom + 0.5, $"the option {row.Text} is not in view.");
        }

        Assert.Equal(selected, pick.Selected);
        var buttons = view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && button.Content is string).OrderBy(button => app.BoundsOf(button).X).Select(button => (string)button.Content!).ToList();
        Assert.Equal([PartS.Cancel, PartS.PickConfirm], buttons);
        AssertNoClippedText(app, layout);
    }

    // ---- The call log (part S's "The screen's reads") ----

    /// <summary>Whether a call's folder is <paramref name="folder"/>: as given, or as git spells it (<paramref name="gitFolder"/>; on macOS <c>/private/var</c> where the path says <c>/var</c>).</summary>
    private static bool InFolder(GitCallRecord call, string folder, string? gitFolder = null) =>
        call.WorkingDirectory is { } directory
        && (WorkspacePaths.Same(directory, folder)
            || (gitFolder is not null && WorkspacePaths.Same(directory, gitFolder))
            || (OperatingSystem.IsMacOS() && WorkspacePaths.Same(directory, "/private" + folder)));

    /// <summary>A call as the report writes it: its command, its folder's kind and its exit code.</summary>
    private sealed record ExpectedCall(string Command, string Where, int? ExitCode, string? Output = null);

    /// <summary>
    /// The screen's reads (SD29) since <paramref name="sequence"/> (and up to <paramref name="until"/>
    /// when given: an opening's reads before a read back's), asserted as a set: the outside read in
    /// the data folder (exit 0); with <paramref name="repo"/>, the repository read and both
    /// <c>git var</c> in its working tree (<paramref name="varExit"/>, not asserted when null: without
    /// a seed git guesses an identity from the machine, SD3; 128 with the seed's refusal, its last
    /// line asserted; and <paramref name="ident"/> on standard output when they succeed), then Pull's
    /// reader's calls <paramref name="pull"/> in its
    /// order; nothing else, unless <paramref name="others"/> lets a call through (a step's own
    /// writes); no write unless <paramref name="allowWrites"/>; no <c>git mergetool</c>.
    /// </summary>
    private static async Task AssertScreenReadsAsync(
        ShellDriver app,
        long sequence,
        TempDirectory data,
        TempRepo? repo,
        int? varExit = 0,
        string? ident = null,
        IReadOnlyList<PartS.PullRead>? pull = null,
        Func<GitCallRecord, bool>? others = null,
        bool allowWrites = false,
        long? until = null)
    {
        var gitFolder = repo is null ? null : await WorkingTreeAsync(repo);
        var window = Window(app, sequence, until);
        var calls = window.Where(call => others is null || !others(call)).ToList();
        var expected = new List<ExpectedCall> { new(PartS.ConfigListCommand, "data", 0) };
        if (repo is not null)
        {
            expected.Add(new(PartS.ConfigListCommand, "repo", 0));
            expected.Add(new(PartS.VarAuthorCommand, "repo", varExit, ident));
            expected.Add(new(PartS.VarCommitterCommand, "repo", varExit, ident));
            expected.AddRange((pull ?? PartS.PullReadsNothingSet).Select(read => new ExpectedCall(read.Command, "repo", read.ExitCode, read.Output)));
        }

        string Where(GitCallRecord call) =>
            InFolder(call, data.Path) ? "data" : repo is not null && InFolder(call, repo.Path, gitFolder) ? "repo" : call.WorkingDirectory ?? "(none)";
        string Describe(GitCallRecord call) => $"{call.CommandText} in {Where(call)} → {call.ExitCode}";
        var described = string.Join("\n  ", calls.Select(Describe));
        Assert.True(calls.Count == expected.Count, $"The screen's reads are {calls.Count} calls, not {expected.Count}:\n  {described}");
        foreach (var want in expected)
        {
            var match = calls.FirstOrDefault(call => call.CommandText == want.Command && Where(call) == want.Where && (want.ExitCode is null || call.ExitCode == want.ExitCode));
            Assert.True(match is not null, $"No call \"{want.Command}\" in the {want.Where} folder with exit {want.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "(any)"}. The calls:\n  {described}");
            if (want.Output is { Length: > 0 } output && want.Command.StartsWith("git var ", StringComparison.Ordinal))
            {
                Assert.Equal($"{output} {PartS.IdentDate}", match!.StandardOutput.Trim());
            }
            else if (want.Output is not null && !want.Command.StartsWith("git var ", StringComparison.Ordinal) && want.Command != PartS.ConfigListCommand)
            {
                Assert.Equal(want.Output, match!.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
            }

            calls.Remove(match!);
        }

        if (repo is not null)
        {
            AssertPullReadsInOrder(app, sequence, pull ?? PartS.PullReadsNothingSet, until);
            if (varExit is 128)
            {
                foreach (var command in new[] { PartS.VarAuthorCommand, PartS.VarCommitterCommand })
                {
                    var call = window.Last(record => record.CommandText == command);
                    Assert.Equal(PartS.NoEmailGiven, call.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n')[^1]);
                }
            }
        }

        if (!allowWrites)
        {
            Assert.DoesNotContain(window, IsWrite);
        }

        AssertNoMergetool(app);
    }

    /// <summary>The calls since <paramref name="sequence"/> and up to <paramref name="until"/> (all after it when null).</summary>
    private static List<GitCallRecord> Window(ShellDriver app, long sequence, long? until) =>
        [.. app.CallsSince(sequence).Where(call => until is null || call.Sequence <= until)];

    /// <summary>Pull's reader's calls since <paramref name="sequence"/> in its order (8.9), each with its exit code; the reads after them are not made.</summary>
    private static void AssertPullReadsInOrder(ShellDriver app, long sequence, IReadOnlyList<PartS.PullRead> reads, long? until = null)
    {
        var commands = reads.Select(read => read.Command).ToHashSet(StringComparer.Ordinal);
        commands.UnionWith([PartS.GetPullFfCommand, PartS.BoolPullFfCommand, PartS.GetBranchRebaseCommand, PartS.GetPullRebaseCommand, PartS.BoolPullRebaseCommand]);
        var made = Window(app, sequence, until).Where(call => commands.Contains(call.CommandText)).OrderBy(call => call.Sequence).ToList();
        Assert.True(
            made.Select(call => (call.CommandText, call.ExitCode ?? -1)).SequenceEqual(reads.Select(read => (read.Command, read.ExitCode))),
            "Pull's reader's calls are not the report's, in its order.\nExpected:\n  " + string.Join("\n  ", reads.Select(read => $"{read.Command} → {read.ExitCode}")) + "\nActual:\n  " + string.Join("\n  ", made.Select(call => $"{call.CommandText} → {call.ExitCode}")));
    }

    /// <summary>"No write" (part S's "The screen's reads"): no <c>git config</c> call with <c>--replace-all</c> or <c>--unset-all</c> since <paramref name="sequence"/>.</summary>
    private static void AssertNoWrite(ShellDriver app, long sequence)
    {
        var writes = app.CallsSince(sequence).Where(IsWrite).Select(call => call.CommandText).ToList();
        Assert.True(writes.Count == 0, "Git's configuration was written:\n  " + string.Join("\n  ", writes));
    }

    /// <summary>A <c>git config</c> write: <c>--replace-all</c> or <c>--unset-all</c>.</summary>
    private static bool IsWrite(GitCallRecord call) =>
        call.CommandText.StartsWith("git config ", StringComparison.Ordinal) && PartS.WriteMarks.Any(mark => call.CommandText.Contains(" " + mark + " ", StringComparison.Ordinal));

    /// <summary>No method of part S makes a <c>git mergetool</c> call (the fixed catalogue answers, 12.9).</summary>
    private static void AssertNoMergetool(ShellDriver app) =>
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith("git mergetool", StringComparison.Ordinal));

    /// <summary>The writes since <paramref name="sequence"/>, oldest first, each with its folder's kind and exit code, against <paramref name="expected"/> (the conventions' "in this order").</summary>
    private static async Task AssertWritesAsync(ShellDriver app, long sequence, TempDirectory data, TempRepo repo, params (string Command, string Where)[] expected)
    {
        var gitFolder = await WorkingTreeAsync(repo);
        var writes = app.CallsSince(sequence).Where(IsWrite).OrderBy(call => call.Sequence).ToList();
        var actual = writes.Select(call => (call.CommandText, InFolder(call, data.Path) ? "data" : InFolder(call, repo.Path, gitFolder) ? "repo" : call.WorkingDirectory ?? "(none)", call.ExitCode)).ToList();
        Assert.True(
            actual.SequenceEqual(expected.Select(write => (write.Command, write.Where, (int?)0))),
            "The writes are not the expected ones.\nExpected:\n  " + string.Join("\n  ", expected.Select(write => $"{write.Command} in {write.Where} → 0")) + "\nActual:\n  " + string.Join("\n  ", actual.Select(write => $"{write.CommandText} in {write.Item2} → {write.ExitCode}")));
    }

    /// <summary>
    /// The ordered subsequence (part S's "An operation's calls in this order") of the calls since
    /// <paramref name="sequence"/> whose command <paramref name="expected"/> or <paramref name="absent"/>
    /// names: exactly <paramref name="expected"/>, each with its exit code, in this order.
    /// </summary>
    private static void AssertOperationCalls(ShellDriver app, long sequence, IReadOnlyList<(string Command, int ExitCode)> expected, params string[] absent)
    {
        var named = expected.Select(call => call.Command).Concat(absent).ToHashSet(StringComparer.Ordinal);
        var calls = app.CallsSince(sequence).OrderBy(call => call.Sequence).Where(call => named.Contains(call.CommandText)).Select(call => (call.CommandText, call.ExitCode ?? -1)).ToList();
        Assert.True(
            calls.SequenceEqual(expected),
            "The operation's calls are not the expected ones, in this order.\nExpected:\n  " + string.Join("\n  ", expected.Select(call => $"{call.Command} → {call.ExitCode}")) + "\nActual:\n  " + string.Join("\n  ", calls.Select(call => $"{call.CommandText} → {call.Item2}")));
    }

    // ---- Files ----

    /// <summary>The session's global file's text, or null while it does not exist.</summary>
    private static string? GlobalText(TempDirectory data)
    {
        var file = ShellDriver.GlobalGitConfigPath(data.Path);
        return File.Exists(file) ? File.ReadAllText(file) : null;
    }

    /// <summary>The copy's <c>.git/config</c>, as its bytes read as text.</summary>
    private static string ConfigText(TempRepo repo) => File.ReadAllText(Path.Combine(repo.Path, ".git", "config"));

    /// <summary>The data folder's <c>settings.json</c> path.</summary>
    private static string SettingsFile(TempDirectory data) => Path.Combine(data.Path, "settings.json");

    /// <summary>"settings.json does not exist": the app wrote no settings.</summary>
    private static void AssertNoSettingsFile(TempDirectory data) =>
        Assert.False(File.Exists(SettingsFile(data)), "settings.json exists: the app wrote settings.");

    /// <summary>
    /// settings.json by its parsed properties (part S's "settings.json"): every property of a save,
    /// the defaults but those <paramref name="changed"/> names (each as its JSON text).
    /// </summary>
    private static void AssertSettingsJson(TempDirectory data, params (string Name, string Json)[] changed)
    {
        Assert.True(File.Exists(SettingsFile(data)), "settings.json does not exist.");
        using var stream = new FileStream(SettingsFile(data), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = JsonDocument.Parse(stream);
        var actual = json.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
        var expected = PartS.SettingsDefaults.ToDictionary(entry => entry.Name, entry => entry.Default, StringComparer.Ordinal);
        foreach (var (name, value) in changed)
        {
            expected[name] = value;
        }

        Assert.True(
            actual.Count == expected.Count && expected.All(entry => actual.TryGetValue(entry.Key, out var value) && Compact(value) == Compact(entry.Value)),
            "settings.json is not the expected one.\nExpected: " + string.Join(", ", expected.Select(entry => $"{entry.Key}={entry.Value}")) + "\nActual:   " + string.Join(", ", actual.Select(entry => $"{entry.Key}={Compact(entry.Value)}")));
    }

    /// <summary>A JSON value's text without its whitespace outside strings, for comparing an indented object with the report's.</summary>
    private static string Compact(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    // ---- The toolbar ----

    /// <summary><c>SettingsButton</c> (7.2, D197): a <c>ToolbarButton</c> with <c>IconSettings</c> and <c>Settings</c>, enabled, its tooltip and help text <c>Open settings (Ctrl+,)</c> and its key <c>Ctrl+,</c>.</summary>
    private static void AssertSettingsButton(ShellDriver app)
    {
        var button = ToolbarButton(app, "SettingsButton");
        Assert.Equal(PartS.SettingsLabel, button.Label);
        Assert.Same(Application.Current!.FindResource("IconSettings"), button.IconData);
        Assert.True(button.IsEffectivelyEnabled, "Settings is disabled.");
        Assert.Equal(PartS.SettingsToolTip, ToolTip.GetTip(button) as string);
        Assert.Equal(PartS.SettingsToolTip, ShellDriver.HelpTextOf(button));
        Assert.Equal(PartS.SettingsKey, ShellDriver.AcceleratorKeyOf(button));
    }

    /// <summary><c>ThemeSwitch</c> (11.6): the icon of the theme it switches to (<c>IconSun</c> and no <c>light</c> class while dark, <c>IconMoon</c> with it while light) and its tooltip and help text.</summary>
    private static void AssertThemeSwitch(ShellDriver app, bool dark, string toolTip)
    {
        var button = ToolbarButton(app, "ThemeSwitch");
        Assert.Equal(!dark, button.Classes.Contains("light"));
        var icon = dark ? "IconSun" : "IconMoon";
        Assert.True(ReferenceEquals(Application.Current!.FindResource(icon), button.IconData), $"ThemeSwitch does not show {icon}.");
        Assert.Equal(toolTip, ToolTip.GetTip(button) as string);
        Assert.Equal(toolTip, ShellDriver.HelpTextOf(button));
    }

    // ---- Operations ----

    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    /// <summary>Waits until the operation <paramref name="title"/> started since <paramref name="mark"/> ended with <paramref name="outcome"/> (the queue's log line), then until the tab is idle; a failure's error dialog is then open.</summary>
    private static async Task WaitForOutcomeAsync(ShellDriver app, TempRepo repo, string title, int mark, OperationOutcome outcome, TimeSpan? timeout = null)
    {
        await WaitForOperationAsync(app, repo, title, mark, timeout ?? OperationChainTimeout);
        var pattern = AppLogFile.OperationEnded(NameOf(repo), title, outcome);
        var lines = app.LogLinesSince(mark).Where(line => pattern.IsMatch(line)).ToList();
        Assert.True(lines.Count == 1, $"Expected one log line like /{pattern}/; found {lines.Count}. The log since the input:\n" + string.Join('\n', app.LogLinesSince(mark).Where(line => line.Contains(title, StringComparison.Ordinal))));
        if (outcome == OperationOutcome.Failed)
        {
            await app.WaitForDialogAsync<ErrorDialogViewModel>();
        }
    }

    /// <summary>Clicks the dialog's <c>Close</c> and waits until no dialog is open.</summary>
    private static async Task CloseDialogAsync(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var close = view.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Content as string == PartS.Close);
        app.Click(close);
        await AssertNoDialogAsync(app);
    }

    /// <summary>The toolbar's Undo by its tooltip (<c>Undo {text} (Ctrl+Z)</c> or <c>Nothing to undo</c>).</summary>
    private static void AssertUndo(ShellDriver app, string toolTip) => Assert.Equal(toolTip, app.UndoRedoState().UndoToolTip);

    /// <summary>The number of operations the session's queues have recorded.</summary>
    private static int OperationCount(ShellDriver app) => app.Session.Operations.Snapshot().Count;

    /// <summary>Plain git's output as lines (the check's own reads, through <see cref="TempRepo"/>), line ends as line feeds.</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>A file of the working tree, line ends as line feeds.</summary>
    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The ahead and behind counts of <c>git status --porcelain=v2 --branch</c>: <c>+a -b</c>.</summary>
    private static async Task<string> AheadBehindAsync(TempRepo repo) =>
        (await GitLinesAsync(repo, "status", "--porcelain=v2", "--branch")).Single(line => line.StartsWith("# branch.ab ", StringComparison.Ordinal))["# branch.ab ".Length..];
}
