// Part S's real-window steps (docs/test-reports/phase-6.md, part S, the RW steps in the Notes of S14
// and S15; docs/phase-6-design.md 11.8, 12.10, 17.2): S14 part A's steps 1 and 2 (the toolbar's
// Settings by a real click, the screen on General) and, in the same launch, Tools, the editor's Pick
// dialog, Other…, and Browse… answered in Windows' own Open dialog with the system folder's
// notepad.exe (never started), then Cancel, a real Ctrl+, and Esc; S15 at a real start with System
// chosen, read under the owner's app mode as it is (the pass never changes it, nor high contrast:
// D224), and a real click on Theme. Each launch on a fresh copy and a new data folder at 1100×700.
// Open file and Open in editor are never clicked.
using System.Drawing;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase6RealWindowPass
{
    /// <summary>What run.txt and the failure say when S15 finds high contrast on (11.8, D224).</summary>
    private const string HighContrastFailure =
        "S15 (System at start): failed. High contrast is on, and Avalonia follows its scheme: turn it off and run the pass again.";

    /// <summary>
    /// Check S14, part A's steps 1 and 2 and the report's RW steps after them, in one real launch on
    /// "settings" (an empty second tab, the session's global file seeded with
    /// <c>user.useConfigOnly = true</c>, SD3): <c>SettingsButton</c>'s help text and key read through UI
    /// Automation, a real click on it, the screen on General (<c>s14a-general</c>); Tools,
    /// <c>Choose…</c> (the Pick dialog's options and details), <c>Other…</c>, <c>Choose</c>,
    /// <c>Browse…</c>: Windows' Open dialog <c>Choose the editor's program</c> answered with the system
    /// folder's <c>notepad.exe</c>, which <c>EditorProgramBox</c> then holds (not pictured: the dialog
    /// shows the machine's folders); Cancel, nothing saved; a real Ctrl+, opening the screen on General,
    /// whose Git section names this launch's own global file, and Esc. Nothing is written.
    /// </summary>
    [Fact]
    public async Task Check_S14_settings_screen_sections_and_entries()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        var configFile = Path.Combine(repo.Path, ".git", "config");
        var configAtStart = File.ReadAllText(configFile);
        using var app = Launch(repo, globalGitConfig: PartS.SeedConfigOnly, emptySecondTab: true);
        var settingsFile = new AppPaths(app.DataDirectory).SettingsFile;
        AssertOwnGitConfiguration(app);
        Assert.Equal(PartS.SeedConfigOnly, File.ReadAllText(app.GlobalGitConfigFile));

        // Step 1: the two tabs; the toolbar's Settings directly left of Search, its help text and its key.
        app.WaitForStripTabs(StepTimeout, PartS.Settings, "New tab");
        var settings = app.Find("SettingsButton");
        Assert.Equal(PartS.SettingsLabel, settings.Name);
        AssertButton(app, "SettingsButton", true, PartS.SettingsToolTip);
        Assert.Equal(PartS.SettingsKey, RealApp.AcceleratorKeyOf(settings));
        var bounds = app.BoundsOf(settings);
        var search = app.BoundsOf(app.Find("SearchButton"));
        var theme = app.BoundsOf(app.Find("ThemeSwitch"));
        Assert.True(bounds.Right <= search.Left + 0.5 && Math.Abs(bounds.Top - search.Top) <= 1, $"Settings at {bounds} is not directly left of Search at {search}.");
        Assert.InRange(theme.Right, app.ClientSize.Width - 20, app.ClientSize.Width);

        // Step 2: a real click on Settings: the screen on General.
        app.ClickWithMouse(settings);
        var screen = app.WaitForSettings(StepTimeout);
        Assert.Equal(PartS.Title, screen.Name);
        app.WaitForSettingsSection(PartS.NavGeneral, StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("SettingsNavGeneral"), "the keyboard focus on General's button (7.1)", StepTimeout);
        AssertGeneral(app);
        Assert.Equal((PartS.Cancel, PartS.Save), (app.SettingsScreenButton("CancelButton").Name, app.SettingsScreenButton("ConfirmButton").Name));
        Assert.Empty(app.ShownElements("SettingsSaving"));
        Assert.Empty(app.ShownElements("SettingsNavError"));
        Compare(app, "s14a-general");

        // Tools, Choose…: the Pick dialog stacked over the screen, its options with their details (step 8's).
        app.ChooseSettingsSection(PartS.NavTools, StepTimeout);
        app.WaitForText("EditorValue", PartS.EditorNone, StepTimeout);
        Assert.Empty(app.ShownElements("EditorStatus"));
        app.ClickWithMouse(app.Find("EditorChooseButton"));
        var pick = app.WaitForPick(PartS.ChooseEditorTitle, StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("PickFilter"), "the Pick dialog's filter to have the keyboard focus", StepTimeout);
        var options = app.PickOptions(pick);
        Assert.Equal(PartS.EditorOptions.Select(option => option.Text), options.Select(option => option.Text));
        Assert.Equal(PartS.EditorOptions.Select(option => option.Detail), options.Select(option => option.Detail));
        Assert.Equal([PartS.EditorNone], options.Where(option => option.IsSelected).Select(option => option.Text));
        Assert.True(app.IsSettingsShown, "The settings screen is gone behind the Pick dialog (D104).");

        // Other…, Choose: the Program and Arguments rows, the focus in the empty Program box.
        app.ChoosePickOption(pick, PartS.OtherEditor, StepTimeout);
        app.WaitForText("EditorValue", PartS.EditorOther, StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("EditorProgramBox"), "the keyboard focus in the Program box", StepTimeout);
        Assert.Equal(string.Empty, app.BoxText("EditorProgramBox"));
        Assert.Equal(PartS.DefaultArguments, app.BoxText("EditorArgumentsBox"));
        Assert.Equal([PartS.ArgumentsHint], app.NamesOnScreen("EditorArgumentsHint"));
        Assert.Empty(app.ShownElements("EditorError"));

        // Browse…: Windows' Open dialog, found by its title; the system folder's notepad.exe typed and confirmed.
        var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        Assert.True(File.Exists(notepad), $"{notepad} does not exist.");
        app.ClickWithMouse(app.Find("EditorBrowseButton"));
        app.ChooseFileInDialog(PartS.ChooseProgramTitle, notepad, StepTimeout);
        WaitTelling(
            app,
            () => app.BoxText("EditorProgramBox") is { Length: > 0 } chosen && string.Equals(Path.GetFullPath(chosen), Path.GetFullPath(notepad), StringComparison.OrdinalIgnoreCase),
            $"the Program box to hold {notepad}",
            () => $"It holds \"{app.BoxText("EditorProgramBox")}\".");
        Run.Note($"S14: Windows' Open dialog \"{PartS.ChooseProgramTitle}\" answered {app.BoxText("EditorProgramBox")}.");
        Assert.True(app.IsSettingsShown, "The settings screen closed with the file dialog.");

        // Cancel: closed at once, without a question; nothing written.
        app.ClickWithMouse(app.SettingsScreenButton("CancelButton"));
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        AssertNoDialog(app);
        AssertNothingWritten(app, settingsFile, configFile, configAtStart);

        // A real Ctrl+,: a new screen on General, its focus on General's button; its Git section names
        // the launch's own global file (8.13), read and not changed; Esc closes it.
        app.PressSettingsKey();
        app.WaitForSettings(StepTimeout);
        app.WaitForSettingsSection(PartS.NavGeneral, StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("SettingsNavGeneral"), "the keyboard focus on General's button after Ctrl+,", StepTimeout);
        Assert.True(RealApp.IsOn(app.Find("SettingsThemeDark")), "Dark is not checked on the new screen.");
        app.ChooseSettingsSection(PartS.NavGit, StepTimeout);
        app.WaitForGitSettingsRead(StepTimeout);
        Assert.Empty(app.ShownElements("GitReadError"));
        var caption = app.Find("GitScopeCaption");
        Assert.Equal(PartS.CaptionGlobal, caption.Name);
        Assert.True(
            string.Equals(Path.GetFullPath(RealApp.HelpTextOf(caption)), Path.GetFullPath(app.GlobalGitConfigFile), StringComparison.OrdinalIgnoreCase),
            $"The Git section's caption names {RealApp.HelpTextOf(caption)}, not this launch's own global file {app.GlobalGitConfigFile} (8.13).");
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        AssertNothingWritten(app, settingsFile, configFile, configAtStart);

        Assert.Equal(0, app.Close());
        AssertNothingWritten(app, settingsFile, configFile, configAtStart);
    }

    /// <summary>
    /// Check S15 in the real window (11.8, D224), a launch of its own on "graph" with
    /// <c>"theme": "System"</c> in <c>settings.json</c>. Before the launch the owner's app mode and high
    /// contrast are read, and only read; with high contrast on, run.txt and the failure say so and
    /// nothing is launched. The start: the variant of the owner's mode, sampled at S15's spots, and
    /// <c>ThemeSwitch</c>'s help text of that variant with System chosen; the frame capture
    /// <c>s15-system-start-with-frame</c> (its title bar inspected) and <c>s15-system-start</c> against
    /// <c>s15e-restarted-light</c> (light) or <c>s15a-system-start-dark</c> (dark). A real click on
    /// Theme: the other variant, <c>"Dark"</c> or <c>"Light"</c> saved, <c>s15-theme-click</c> against
    /// <c>s15c-theme-click-dark</c> (from light) or <c>s15b-system-light</c> (from dark). Following a
    /// change of the mode is the scripted run's and the acceptance's by hand.
    /// </summary>
    [Fact]
    public async Task Check_S15_system_theme_at_a_real_start()
    {
        // Before the launch, read only.
        var light = RealApp.SystemUsesLightTheme();
        var highContrast = RealApp.HighContrastIsOn();
        Run.Note($"S15: Windows' app mode is {(light ? "light" : "dark")} (AppsUseLightTheme), high contrast {(highContrast ? "on" : "off")}; both read only (D224).");
        if (highContrast)
        {
            Run.Note(HighContrastFailure);
            Assert.Fail(HighContrastFailure);
        }

        using var repo = await Scenarios.GraphAsync();
        using var app = Launch(repo, settingsJson: PartS.SystemThemeFile);
        var name = NameOf(repo);
        Assert.Equal(PartS.Graph, name);
        var shown = light ? Light : Dark;
        var other = light ? Dark : Light;

        // The start: the variant of the owner's mode.
        Assert.Single(app.LogLines(), line => line.Contains(PartS.ThemeSystemAtStart(dark: !light), StringComparison.Ordinal));
        AssertButton(app, "ThemeSwitch", true, light ? PartS.ToDarkLeavingSystem : PartS.ToLightLeavingSystem);
        Assert.Equal("\"System\"", SavedSetting(app.DataDirectory, "theme"));
        app.MoveToNeutral();
        using (var frame = app.CaptureWindow())
        {
            Run.Save(frame, "s15-system-start-with-frame");
        }

        using (var start = app.CaptureClient())
        {
            Run.Save(start, "s15-system-start");
            AssertSpots(app, start, shown);
            Run.AssertMatchesScripted(start, "s15-system-start", light ? "s15e-restarted-light" : "s15a-system-start-dark");
        }

        // A real click on Theme: the other variant, saved as such (System left, 11.6).
        app.ClickWithMouse(app.Find("ThemeSwitch"));
        AssertButton(app, "ThemeSwitch", true, light ? PartS.ToLight : PartS.ToDark);
        var saved = light ? "\"Dark\"" : "\"Light\"";
        app.WaitFor(() => SavedSetting(app.DataDirectory, "theme") == saved, $"settings.json's theme to be {saved}", StepTimeout);
        Assert.Single(app.LogLines(), line => line.Contains(PartS.ThemeChanged(light ? PartS.ThemeDark : PartS.ThemeLight), StringComparison.Ordinal));
        app.MoveToNeutral();
        using (var clicked = app.CaptureClient())
        {
            AssertSpots(app, clicked, other);
        }

        Compare(app, "s15-theme-click", scriptedName: light ? "s15c-theme-click-dark" : "s15b-system-light");

        Assert.Equal(0, app.Close());
    }

    // ---- Part S's helpers ----

    /// <summary>
    /// General (7.3) as UI Automation reads it after the start: the theme's radios with Dark checked,
    /// auto-fetch on at 5 minutes, Keep ignored files and Include untracked files on, the extensions
    /// Open file asks about, PATHEXT's two, the empty <c>Also ask for</c> box, and no error.
    /// </summary>
    private static void AssertGeneral(RealApp app)
    {
        Assert.True(RealApp.IsOn(app.Find("SettingsThemeDark")), "Dark is not checked.");
        Assert.False(RealApp.IsOn(app.Find("SettingsThemeLight")), "Light is checked.");
        Assert.False(RealApp.IsOn(app.Find("SettingsThemeSystem")), "System is checked.");
        Assert.Equal(
            [PartS.ThemeDark, PartS.ThemeLight, PartS.ThemeSystem],
            new[] { "SettingsThemeDark", "SettingsThemeLight", "SettingsThemeSystem" }.Select(id => app.Find(id).Name));
        var texts = app.Texts();
        Assert.All(new[] { PartS.ThemeNote, PartS.AutoFetchNote, PartS.KeepIgnoredNote, PartS.StashUntrackedNote, PartS.RunnableIntro, PartS.RunnableUnix }, text => Assert.Contains(text, texts));
        Assert.True(RealApp.IsOn(app.Find("AutoFetchCheckBox")), "Fetch in the background is off.");
        Assert.Equal(PartS.DefaultMinutes, app.BoxText("AutoFetchMinutesBox"));
        Assert.True(RealApp.IsOn(app.Find("KeepIgnoredCheckBox")), "Keep ignored files is off.");
        Assert.True(RealApp.IsOn(app.Find("StashUntrackedCheckBox")), "Include untracked files when stashing is off.");
        Assert.Equal(PartS.RunnableList, app.Find("RunnableList").Name);
        Assert.Equal([PartS.RunnablePathExt], app.NamesOnScreen("RunnablePathExt"));
        Assert.Equal(string.Empty, app.BoxText("RunnableExtraBox"));
        Assert.Empty(app.ShownElements("RunnableError"));
        Assert.Empty(app.ShownElements("AutoFetchError"));
    }

    /// <summary>Nothing saved (S14 steps 10 and 11): no <c>settings.json</c>, the global file still the seed, <c>.git/config</c> as the copy's.</summary>
    private static void AssertNothingWritten(RealApp app, string settingsFile, string configFile, string configAtStart)
    {
        Assert.False(File.Exists(settingsFile), "settings.json exists: something was saved.");
        Assert.Equal(PartS.SeedConfigOnly, File.ReadAllText(app.GlobalGitConfigFile));
        Assert.Equal(configAtStart, File.ReadAllText(configFile));
    }

    /// <summary>S15's three spots (part S's "graph"): the graph area, the left panel, the tab strip's chrome, in <paramref name="palette"/>'s Window, Panel and Chrome, as phase 0's <c>AssertShell</c> samples.</summary>
    private static void AssertSpots(RealApp app, Bitmap capture, Palette palette)
    {
        var scaling = app.Scaling;
        RealWindowRun.AssertColour(Drawn(palette.Window), capture.ColourAt(PartS.WindowSpot.X, PartS.WindowSpot.Y, scaling), "graph area's background");
        RealWindowRun.AssertColour(Drawn(palette.Panel), capture.ColourAt(PartS.PanelSpot.X, PartS.PanelSpot.Y, scaling), "left panel's background");
        RealWindowRun.AssertColour(Drawn(palette.Chrome), capture.ColourAt(PartS.ChromeSpot.X, PartS.ChromeSpot.Y, scaling), "tab strip's chrome");
    }
}
