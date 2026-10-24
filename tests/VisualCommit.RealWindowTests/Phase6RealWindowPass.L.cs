// Part L's real-window check (docs/test-reports/phase-6.md, part L, L34; docs/phase-6-design.md 11.8,
// 11.15, 17.4; D224, LD23): the real window's own, with no scripted method. The app starts with
// "theme": "Light" in settings.json, whatever the owner's app mode, on a fresh "ops" copy at 1100×700;
// four light overlays, each closed before the next, are compared with the light pictures L31 takes
// under System with the fake system light (System's light draws Light's pixels, 11.15). The pass reads
// nothing of the desktop's settings here and changes none: never Windows' theme or high contrast.
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase6RealWindowPass
{
    /// <summary>L34's <c>settings.json</c>, written before the launch: an explicit Light, which does not depend on the owner's mode (11.8).</summary>
    private const string LightThemeFile = "{ \"theme\": \"Light\" }";

    /// <summary>The start's log line with Light chosen (11.5, 14.14).</summary>
    private const string LightAtStart = "Theme: Light.";

    /// <summary>
    /// Check L34 (11.8, D224, LD23), one launch on "ops" with <c>"theme": "Light"</c>: the window light
    /// after the launch, before any input (the chrome, panel and window colours of the light variant
    /// at spots clear of text, rows and lanes; <c>ThemeSwitch</c>'s help text; the log's
    /// <c>Theme: Light.</c>); Fetch by a real click and the activity log opened
    /// (<c>l31a-activity-log-light</c>); <c>Delete feature/alpha…</c> from alpha's menu by a real
    /// right-click and its question (<c>l31b-delete-question-light</c>), Cancel; the palette by a real
    /// Ctrl+Shift+P (<c>l31c-palette-light</c>), Esc; the settings screen by a real Ctrl+, on General with
    /// Light checked (<c>l31d-settings-light</c>, L31's single picture of that name), Esc. Nothing changes
    /// in git, no backup is made, and the launch's global git file is its own data folder's.
    /// </summary>
    [Fact]
    public async Task Check_L34_light_overlays_against_their_twins()
    {
        using var repo = await Scenarios.OperationsAsync();
        var exact = await repo.ExactStateAsync();
        using var app = Launch(repo, settingsJson: LightThemeFile);
        var name = NameOf(repo);
        Assert.Equal(PartK.Ops, name);
        AssertOwnGitConfiguration(app);

        // Step 1: after the launch, before any input: the window light through the real platform.
        using (var start = app.CaptureClient())
        {
            AssertLightShell(app, start);
        }

        AssertButton(app, "ThemeSwitch", true, PartL.SwitchToDark);
        Assert.Single(app.LogLines(), line => line.Contains(LightAtStart, StringComparison.Ordinal));
        Assert.Equal("\"Light\"", SavedSetting(app.DataDirectory, "theme"));

        // Step 2: Fetch by a real click; the activity log opened with its one row (l31a).
        var fetch = AppLogFile.OperationEnded(name, PartK.Fetch, OperationOutcome.Succeeded, PartL.FetchedFromAll);
        var fetched = app.CountLogLines(fetch);
        app.ClickWithMouse(app.Find("FetchButton"));
        app.WaitForLogLine(fetch, NetworkTimeout, fetched + 1);
        WaitForOperationStatus(app, PartL.FetchedFromAll);
        app.ClickWithMouse(app.Find("ActivityLogToggle"));
        app.WaitForActivityLog(shown: true, StepTimeout);
        AssertButton(app, "ActivityLogToggle", true, PartL.HideActivityLog);
        var row = app.WaitForActivityLogRow(PartK.Fetch, timeout: StepTimeout);
        Assert.Equal("ActivityLogOperation", row.Kind);
        Assert.Single(app.ActivityLogRows());
        Compare(app, "l31a-activity-log-light");
        app.ClickWithMouse(app.Find("ActivityLogToggle"));
        app.WaitForActivityLog(shown: false, StepTimeout);

        // Step 3: Local branches › feature opened; a real right-click on its alpha; Delete feature/alpha…: the question (l31b).
        app.ClickWithMouse(app.RefRow("Local branches", "feature"));
        app.WaitFor(() => app.HasRefRow("Local branches", "feature", "alpha"), "the folder feature to open", StepTimeout);
        app.RightClickWithMouse(app.RefRow("Local branches", "feature", "alpha"));
        app.ClickMenuItem(PartL.DeleteAlpha);
        app.WaitFor(() => app.ShownElements("ConfirmationDialog").Count == 1, "the deletion's question", StepTimeout);
        app.WaitForText("ConfirmTitle", PartL.DeleteBranchTitle, StepTimeout);
        Assert.Equal([PartL.DeleteAlphaQuestion], app.NamesOnScreen("ConfirmQuestion"));
        Assert.Equal([PartL.BackupFirst], app.NamesOnScreen("ConfirmDetail"));
        Assert.Equal([PartL.Delete], app.NamesOnScreen("ConfirmButton"));
        Compare(app, "l31b-delete-question-light");
        app.ClickWithMouse(app.FindIn(app.ShownElements("ConfirmationDialog")[0], "CancelButton"));
        app.WaitUntilGone("ConfirmationDialog", StepTimeout);
        AssertNoDialog(app);
        Assert.Equal([PartK.AlphaTwoSha], await GitLinesAsync(repo, "rev-parse", "refs/heads/feature/alpha"));

        // Step 4: the pointer to the neutral point (Cancel left it where the palette's list opens,
        // 5.8); a real Ctrl+Shift+P: the palette, its first command highlighted (l31c).
        app.MoveToNeutral();
        var mark = app.LogMark();
        app.PressPalette();
        app.WaitForPalette(StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("CommandPaletteBox"), "the palette's box to have the keyboard focus", StepTimeout);
        AssertOneLogLine(app, mark, "] " + PartL.PaletteOpened);
        AssertHighlightInView(app, PartL.PaletteFirst12[0].Label);
        var first = app.PaletteRowsInView()[0];
        Assert.Equal((PartL.PaletteFirst12[0].Label, true, PartL.FetchKey), (first.Label, first.IsHighlighted, first.Key));
        Compare(app, "l31c-palette-light");
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitForPaletteClosed(StepTimeout);

        // Step 5: the pointer to the neutral point; a real Ctrl+,: the settings screen on General, Light checked (l31d's single picture).
        app.MoveToNeutral();
        app.PressSettingsKey();
        app.WaitForSettings(StepTimeout);
        app.WaitForSettingsSection(PartS.NavGeneral, StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("SettingsNavGeneral"), "the keyboard focus on General's button", StepTimeout);
        Assert.False(RealApp.IsOn(app.Find("SettingsThemeDark")), "Dark is checked with Light chosen.");
        Assert.True(RealApp.IsOn(app.Find("SettingsThemeLight")), "Light is not checked.");
        Assert.False(RealApp.IsOn(app.Find("SettingsThemeSystem")), "System is checked with Light chosen.");
        Compare(app, PartL.SettingsLight);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        AssertNoDialog(app);

        Assert.Equal(0, app.Close());
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal("\"Light\"", SavedSetting(app.DataDirectory, "theme"));
        Assert.False(File.Exists(app.GlobalGitConfigFile), $"The launch wrote a global git configuration ({app.GlobalGitConfigFile}).");
    }

    /// <summary>
    /// The light window at L34's start (11.8; phase 0's <c>AssertShell</c> colours, <c>#EBEDF1</c>,
    /// <c>#F5F6F8</c>, <c>#FFFFFF</c>), sampled at spots of "ops" clear of text, rows and lanes: the tab
    /// strip's chrome right of its one tab and the status bar's at the neutral point; the left panel
    /// and the details panel near their bottoms; the graph area below its eight rows.
    /// </summary>
    private static void AssertLightShell(RealApp app, System.Drawing.Bitmap capture)
    {
        var scaling = app.Scaling;
        var size = app.ClientSize;
        RealWindowRun.AssertColour(Drawn(Light.Chrome), capture.ColourAt(PartS.ChromeSpot.X, PartS.ChromeSpot.Y, scaling), "tab strip's chrome");
        RealWindowRun.AssertColour(Drawn(Light.Chrome), capture.ColourAt(RealApp.NeutralPoint.X, RealApp.NeutralPoint.Y, scaling), "status bar's chrome");
        RealWindowRun.AssertColour(Drawn(Light.Panel), capture.ColourAt(130, size.Height - 66, scaling), "left panel's background");
        RealWindowRun.AssertColour(Drawn(Light.Panel), capture.ColourAt(size.Width - 200, size.Height - 66, scaling), "details panel's background");
        RealWindowRun.AssertColour(Drawn(Light.Window), capture.ColourAt(PartS.WindowSpot.X, PartS.WindowSpot.Y, scaling), "graph area's background");
    }
}
