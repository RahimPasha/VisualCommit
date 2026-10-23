// Check S16 (docs/test-reports/phase-6.md, part S): auto-fetch's minutes and "Include untracked
// files when stashing" (design 7.3, 7.8, 7.9, SD14): a file's 120 shown and kept without an error
// until the box is edited; 61 refused at Save with the section marked; 15 saved by Enter in the
// box; off saved as 0 and shown again unchecked at 5; the stash dialog's box following the setting
// both ways; the environment's override logged.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S16, one app start (steps 1 to 8), on "settings" with a change and a settings.json of 120 minutes and the stash setting off (SD14).</summary>
    [AvaloniaFact]
    public async Task Check_S16_auto_fetch_and_stash_settings()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: notes.txt changed, draft.txt untracked, settings.json written before the start.
        repo.WriteFile(PartS.NotesFile, PartS.NotesThreeLines).WriteFile(PartS.DraftFile, PartS.DraftText);
        File.WriteAllText(SettingsFile(data), PartS.S16SettingsFile);
        var written = File.ReadAllBytes(SettingsFile(data));
        using var app = await StartAsync(data, repo);
        var start = app.CallMark();
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.AutoFetchOverride)));
        AssertGraph(app, PartS.St0, PartS.StLanes, PartS.St0wWorking);
        var screen = await app.OpenSettingsAsync();
        ShotAtRest(app, "s16a-general-from-file");
        AssertGeneral(app, layout, minutes: "120", stashUntracked: false);

        // Step 2: Save with nothing changed: closed, settings.json untouched (7.8 step 2).
        layout.Step = "step 2";
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        Assert.Equal(written, File.ReadAllBytes(SettingsFile(data)));

        // Step 3: 61 typed: the error at once (D114); Save refused, General marked.
        layout.Step = "step 3";
        screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ReplaceText("AutoFetchMinutesBox", "61");
        AssertMinutesError(app, layout);
        Assert.False(await app.SaveSettingsAsync(), "Save closed the screen with 61 minutes.");
        ShotAtRest(app, "s16b-minutes-error");
        Assert.Equal(SettingsSection.General, screen.SelectedSection);
        AssertMinutesError(app, layout);
        AssertNav(app, "SettingsNavGeneral", "SettingsNavGeneral");
        await AssertFocusAsync(app, "AutoFetchMinutesBox");
        Assert.Equal(written, File.ReadAllBytes(SettingsFile(data)));

        // Step 4: 15 typed: the error and the dot gone (SD9); the stash setting on; Enter in the box saves (7.8).
        layout.Step = "step 4";
        app.ReplaceText("AutoFetchMinutesBox", "15");
        AssertHidden(app.SettingsSnapshot(), "AutoFetchError");
        AssertNav(app, "SettingsNavGeneral");
        ClickField(app, "StashUntrackedCheckBox");
        AssertChecked(app.SettingsSnapshot(), "StashUntrackedCheckBox", true);
        app.Click(app.FindByAutomationId("AutoFetchMinutesBox"));
        app.PressKey(Key.Enter);
        await app.WaitForSettingsSaveAsync(screen);
        await WaitForScreenClosedAsync(app, screen);
        AssertSettingsJson(data, ("autoFetchMinutes", "15"), ("stashIncludeUntracked", "true"));

        // Step 5: the stash dialog starts with the setting, on (7.9); Esc stashes nothing.
        layout.Step = "step 5";
        var stash = ToolbarButton(app, "StashButton");
        Assert.True(stash.IsEffectivelyEnabled, "Stash is disabled.");
        Assert.Equal(PartS.StashToolTip, ToolTip.GetTip(stash) as string);
        var saved = File.ReadAllBytes(SettingsFile(data));
        app.Click(stash);
        await AssertStashDialogAsync(app, includeUntracked: true);
        ShotAtRest(app, "s16c-stash-dialog-untracked");
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.DoesNotContain(app.CallsSince(start), call => call.CommandText.StartsWith("git stash", StringComparison.Ordinal));
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal(saved, File.ReadAllBytes(SettingsFile(data)));

        // Step 6: auto-fetch off keeps its 15 in a disabled box; saved as 0, the stash setting off.
        layout.Step = "step 6";
        screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        var snapshot = app.SettingsSnapshot();
        AssertChecked(snapshot, "AutoFetchCheckBox", true);
        AssertBox(snapshot, "AutoFetchMinutesBox", "15");
        AssertChecked(snapshot, "StashUntrackedCheckBox", true);
        ClickField(app, "AutoFetchCheckBox");
        snapshot = app.SettingsSnapshot();
        AssertChecked(snapshot, "AutoFetchCheckBox", false);
        AssertBox(snapshot, "AutoFetchMinutesBox", "15", enabled: false);
        ClickField(app, "StashUntrackedCheckBox");
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        AssertSettingsJson(data, ("autoFetchMinutes", "0"), ("stashIncludeUntracked", "false"));

        // Step 7: 0 shows the box unchecked at 5 (7.3); Esc writes nothing.
        layout.Step = "step 7";
        saved = File.ReadAllBytes(SettingsFile(data));
        screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        ShotAtRest(app, "s16d-auto-fetch-off");
        AssertGeneral(app, layout, autoFetch: false, minutes: PartS.DefaultMinutes, minutesEnabled: false, stashUntracked: false);
        await EscapeScreenAsync(app);
        Assert.Equal(saved, File.ReadAllBytes(SettingsFile(data)));

        // Step 8: the stash dialog starts unchecked; Esc stashes nothing; no git write in the whole check.
        layout.Step = "step 8";
        app.Click(ToolbarButton(app, "StashButton"));
        await AssertStashDialogAsync(app, includeUntracked: false);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Empty(await repo.StashListAsync());
        Assert.DoesNotContain(app.CallsSince(start), call => call.CommandText.StartsWith("git stash", StringComparison.Ordinal));
        AssertNoWrite(app, start);
        Assert.Equal(saved, File.ReadAllBytes(SettingsFile(data)));
        layout.AssertNone();
    }

    /// <summary><c>AutoFetchError</c> under the Fetching row, in the danger colour (7.3).</summary>
    private static void AssertMinutesError(ShellDriver app, Layout layout)
    {
        AssertText(app.SettingsSnapshot(), "AutoFetchError", PartS.AutoFetchError);
        var error = Named<TextBlock>(app, "AutoFetchError");
        Assert.Equal(PaletteOf(app).Danger, (error.Foreground as ISolidColorBrush)?.Color);
        layout.Check(app.BoundsOf(error).Y >= app.BoundsOf(Named<TextBox>(app, "AutoFetchMinutesBox")).Bottom - 0.5, "AutoFetchError is not under the Fetching row.");
    }

    /// <summary>The stash dialog (phase 3's): <c>Stash changes</c>, an empty <c>MessageBox</c> with its placeholder, <c>Include untracked files</c> as the setting gives it, Cancel and Stash.</summary>
    private static async Task AssertStashDialogAsync(ShellDriver app, bool includeUntracked)
    {
        var dialog = await app.WaitForDialogAsync<StashDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The stash dialog has no view.");
        Assert.Equal(PartS.StashDialogTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        var message = (TextBox)app.FindByAutomationId(view, "MessageBox");
        Assert.True(message.IsEffectivelyVisible, "The stash dialog shows no MessageBox.");
        Assert.Equal(string.Empty, message.Text ?? string.Empty);
        Assert.Equal(PartS.StashMessagePlaceholder, message.PlaceholderText);
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((PartS.StashOption, (bool?)includeUntracked), (option.Content as string, option.IsChecked));
        Assert.Equal(includeUntracked, dialog.IsOptionChecked);
        var buttons = view.GetVisualDescendants().OfType<Button>().Where(button => button is not Avalonia.Controls.Primitives.ToggleButton && button.IsEffectivelyVisible && button.Content is string).OrderBy(button => app.BoundsOf(button).X).Select(button => (string)button.Content!).ToList();
        Assert.Equal([PartS.Cancel, PartS.StashConfirm], buttons);
    }
}
