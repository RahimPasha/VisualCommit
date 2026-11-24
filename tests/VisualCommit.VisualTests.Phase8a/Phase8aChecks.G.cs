// Part G of phase 8a's walk-through, the user guide's links (docs/test-reports/phase-8a.md, "Part G";
// docs/phase-8a-design.md 1.7, 1.8, 1.13, 11.3.1, 11.5; D417, D452; rulings 77 to 85): the palette's
// "Open the user guide" with its key F1 and the shortcuts list's last group "Help" on "ops" (G1), the
// first-run screen's guide link in each state (G2) and About's guide link (G3). Four methods, four
// starts, as the report's methods table has them; none has a real-window twin, since no real window
// opens a browser. The harness's fake launcher records every address and opens nothing; G2's starts
// give a fake probe for git. "No git call" on "ops" is read with the quiet marks beside the live
// watcher (D280, ruling 77); on G2's empty tabs the plain count stands. The helpers are in
// Phase8aChecks.G.Helpers.cs.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part G: the user guide's links (docs/test-reports/phase-8a.md; design 11.5).</summary>
public sealed partial class Phase8aChecksG : Phase8aChecks
{
    /// <summary>
    /// Method 1, G1: on "ops", the palette's one row for <c>guide</c> with its key F1, F1 with the
    /// palette open opening nothing, Enter and then F1 on the graph each opening the guide's start
    /// page, Shift+F1 and Ctrl+F1 nothing, F1 over the settings screen nothing, and the Keyboard
    /// shortcuts section's end with its last group Help.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G1_palette_row_f1_and_the_help_group()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = app.Session.Shell.ActiveTab.Repository ?? throw new InvalidOperationException("The tab has no repository.");

        // Step 1: the graph drawn; "Main work" clicked in its Message column, the one selected row
        // with its details, the graph's rows with the keyboard focus.
        var exact = await repo.ExactStateAsync();
        Assert.Equal(G.MainWork, repository.Graph.CommitAt(G.MainWorkRow).Subject);
        var mainWork = repository.Graph.CommitAt(G.MainWorkRow).Sha;
        var point = app.GraphRowPoint(G.MainWorkRow);
        Assert.True(
            Math.Abs(point.X - G.MainWorkPoint.X) <= 0.5 && Math.Abs(point.Y - G.MainWorkPoint.Y) <= G.Tolerance,
            $"Main work's Message column is at {point}, not at {G.MainWorkPoint}.");
        app.Click(point);
        await app.WaitForAsync(
            () => repository.Selection.Shas is [var only] && only == mainWork
                && repository.Details is { IsLoading: false, HasCommit: true, Sha: var shown } && shown == mainWork
                && app.FocusedAutomationId() == G.GraphId,
            "Main work selected alone, its details shown and the graph's rows focused");
        app.WaitForCommitDetails();
        app.MoveToNeutral();
        AssertMainWorkSelectedAndFocused(app, repository, mainWork);
        Assert.Empty(app.Launcher.Requests);

        // Step 2: Ctrl+Shift+P: the palette and its worktree read; "guide" typed, one row (g1a).
        var callMark = await app.QuietCallMarkAsync();
        var operations = app.Session.Operations.Snapshot().Count;
        app.PressPalette();
        await app.WaitForAsync(
            () => app.IsPaletteOpen && app.PaletteBox.IsFocused && app.CallsSince(callMark).Any(call => call.CommandText == G.WorktreeList),
            "the palette open, its box focused and its worktree read in the call log");
        await app.Session.Shell.PaletteWorktreeRead;
        app.Settle();
        var beforeTyping = app.CallMark();
        app.Type(G.Query);
        await app.WaitForAsync(() => app.Palette.Shown.Count == 1, $"one row for \"{G.Query}\"");
        var g1a = Picture(app, G.G1aPaletteGuide);
        AssertGuideRow(app, g1a);
        Assert.True(app.PaletteBox.IsFocused, "The palette's box does not have the keyboard focus.");
        Assert.True(app.Session.Shell.Dialogs.IsOpen, "The palette is not a dialog of the dialog layer.");
        Assert.Equal(G.CardHeightOneRow, app.PaletteSnapshot().CardBounds.Height, 0.5);
        Assert.Empty(app.Launcher.Requests);
        await AssertOnlyTheWorktreeReadAsync(app, callMark, operations);
        Note($"G1 step 2: the calls made while \"{G.Query}\" was typed: {Described(app.CallsSince(beforeTyping))}.");
        var stepMark = await app.QuietStepMarkAsync();

        // Step 3: F1 with the palette open opens nothing (a dialog is open).
        app.PressKey(Key.F1);
        await app.WaitForIdleAsync();
        Assert.Empty(app.Launcher.Requests);
        Assert.True(app.IsPaletteOpen, "F1 closed the palette.");
        Assert.True(app.PaletteBox.IsFocused, "F1 took the focus from the palette's box.");
        Assert.Equal(G.Query, app.PaletteBox.Text);
        AssertGuideRow(app, null);
        await app.AssertNoStepCallsAsync(stepMark, "F1 with the palette open");

        // Step 4: Enter closes the palette and opens the start page once; the focus back in the graph.
        var logMark = app.LogMark();
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen && app.Launcher.Requests.Count == 1, "the palette closed and the launcher holding one request");
        await app.WaitForIdleAsync();
        AssertRequests(app, G.StartPage);
        await app.WaitForAsync(() => app.FocusedAutomationId() == G.GraphId, "the keyboard focus back in the graph's rows");
        await AssertNoDialogAsync(app);
        await app.AssertNoStepCallsAsync(stepMark, "Enter on Open the user guide");
        AssertNotLogged(app, logMark, G.FailedLine, G.NoLauncherLine);

        // Step 5: F1 on the graph opens the start page again.
        app.PressKey(Key.F1);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "the launcher to hold two requests");
        await app.WaitForIdleAsync();
        AssertRequests(app, G.StartPage, G.StartPage);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark);
        AssertMainWorkSelectedAndFocused(app, repository, mainWork);
        await app.AssertNoStepCallsAsync(stepMark, "F1 on the graph");

        // Step 6: Shift+F1 and Ctrl+F1 route nowhere.
        app.PressKey(Key.F1, RawInputModifiers.Shift);
        await app.WaitForIdleAsync();
        app.PressKey(Key.F1, RawInputModifiers.Control);
        await app.WaitForIdleAsync();
        AssertRequests(app, G.StartPage, G.StartPage);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark);
        AssertMainWorkSelectedAndFocused(app, repository, mainWork);
        await app.AssertNoStepCallsAsync(stepMark, "Shift+F1 and Ctrl+F1 on the graph");

        // Step 7: Ctrl+, on General; F1 over the screen opens nothing.
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        await WaitForFocusAsync(app, G.NavGeneralId);
        var settingsMark = await app.QuietStepMarkAsync();
        app.PressKey(Key.F1);
        await app.WaitForIdleAsync();
        Assert.Same(screen, app.SettingsDialog);
        Assert.Equal(SettingsSection.General, screen.SelectedSection);
        Assert.Equal(G.NavGeneralId, app.FocusedAutomationId());
        AssertRequests(app, G.StartPage, G.StartPage);
        var settingsCalls = app.CallsSince(settingsMark.Calls);
        Note($"G1 step 7: the calls after the step's mark, F1 included: {Described(settingsCalls)}.");
        await app.AssertNoStepCallsAsync(settingsMark, "F1 over the settings screen");

        // Step 8: Keyboard shortcuts, scrolled to its end with the wheel (g1b).
        app.ChooseSection(SettingsSection.KeyboardShortcuts);
        await app.WaitForAsync(() => screen.SelectedSection == SettingsSection.KeyboardShortcuts && app.Find<ItemsControl>(G.ShortcutListName).IsEffectivelyVisible, "the Keyboard shortcuts section");
        await ScrollSectionToEndAsync(app);
        Picture(app, G.G1bShortcutsHelp);
        AssertShortcutsEnd(app);
        AssertRequests(app, G.StartPage, G.StartPage);

        // Step 9: Esc closes the screen; nothing written, nothing else opened.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
        AssertRequests(app, G.StartPage, G.StartPage);
        await app.AssertNoStepCallsAsync(settingsMark, "Steps 7 to 9");
        var beforeExact = await app.QuietCallMarkAsync();
        Assert.Equal(exact, await repo.ExactStateAsync());
        Note($"G1 step 9: the session's calls while ExactStateAsync() ran, read with the watchers quiet: {Described(await app.QuietCallsSinceAsync(beforeExact))}.");
        Assert.False(File.Exists(SettingsFile(data)), "The steps wrote settings.json.");
        AssertNoStart(app);
    }

    /// <summary>
    /// Method 2, G2 in four states of one start: Missing, then by "Check again" the chosen git gone,
    /// too old and not working; in each Tab to the guide link, Enter opens the state's section and F1
    /// the start page; then the palette's row on the first-run screen.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G2_first_run_links_from_missing_through_check_again()
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(SettingsFile(data)).Update(settings => settings with { GitExecutable = F.Chosen });
        var probe = new FakeGitSetupProbe(
            FakeGitSetupProbe.Windows(),
            FakeGitAnswer.Missing,
            FakeGitAnswer.ChosenMissing(F.WindowsGit),
            FakeGitAnswer.TooOld(F.WindowsGit, GitSource.Path, G.TooOldVersion),
            FakeGitAnswer.ConfigFault(F.WindowsGit, G.BrokenConfigFile, G.BrokenConfigLine));
        using var app = await StartEmptyAsync(data, gitProbe: probe);

        // Step 1: Missing on Windows; settings.json read; a frame captured before the focus moves.
        await app.WaitForGitSetupAsync(guidance: G.MissingGuidance, choose: G.ChooseGitExe, winget: true);
        var settings = File.ReadAllBytes(SettingsFile(data));
        app.MoveToNeutral();
        var before = app.Capture();
        Assert.Equal([new GitLookup(F.Chosen, null)], probe.Lookups);
        AssertState(app, G.MissingTitle, G.MissingGuidance, G.MissingButtons, G.MissingButtonTexts, G.HowVisualCommitFindsGit);
        Assert.Equal(F.MissingBodyWindows, app.GitSetupText("GitSetupBody"));
        Assert.Empty(app.Launcher.Requests);
        Assert.Empty(app.GitCalls());

        // Step 2: F6 to the first button, Tab through the buttons to the guide link (g2a).
        await PressF6ToAsync(app, G.MissingButtons[0]);
        foreach (var id in G.MissingButtons.Skip(1).Append(G.GitSetupGuideLinkId))
        {
            app.PressKey(Key.Tab);
            await WaitForFocusAsync(app, id);
            Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        }

        var g2a = Picture(app, G.G2aFirstRunGuideLink);
        AssertGuideLinkFocused(app, G.HowVisualCommitFindsGit);
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Launcher.Requests);
        Assert.Single(probe.Lookups);
        AssertFrameClearOfInk(app, before, g2a, G.HowVisualCommitFindsGit);

        // Steps 3 and 4: Enter opens Missing's section, F1 the start page.
        await EnterAndF1Async(app, probe, G.MissingTitle, G.GitMissingAddress, before: 0);

        // Steps 5 and 6: Check again to the chosen git gone; Tab to the link; Enter and F1.
        await CheckAgainToAsync(app, probe, 2, G.ChosenMissingGuidance);
        await TabToGuideLinkAsync(app, G.ChosenMissingTitle, G.ChosenMissingGuidance, G.ChosenMissingButtons, G.ChosenMissingButtonTexts, requests: 2);
        await EnterAndF1Async(app, probe, G.ChosenMissingTitle, G.GitChosenMissingAddress, before: 2);

        // Steps 7 and 8: to too old.
        await CheckAgainToAsync(app, probe, 3, G.TooOldGuidance, winget: true);
        await TabToGuideLinkAsync(app, G.TooOldTitle, G.TooOldGuidance, G.TooOldButtons, G.TooOldButtonTexts, requests: 4);
        await EnterAndF1Async(app, probe, G.TooOldTitle, G.GitTooOldAddress, before: 4);

        // Steps 9 and 10: to not working, the broken configuration.
        await CheckAgainToAsync(app, probe, 4, G.NotWorkingGuidance);
        await TabToGuideLinkAsync(app, G.NotWorkingTitle, G.NotWorkingGuidance, G.NotWorkingButtons, G.NotWorkingButtonTexts, requests: 6);
        await EnterAndF1Async(app, probe, G.NotWorkingTitle, G.GitNotWorkingAddress, before: 6);

        // Step 11: the palette's row on the first-run screen; Esc runs nothing.
        app.MoveToNeutral();
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette open with its box focused");
        app.Type(G.Query);
        await app.WaitForAsync(() => app.Palette.Shown.Count == 1, $"one row for \"{G.Query}\"");
        AssertGuideRow(app, null);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
        app.Settle();
        Assert.Equal(8, app.Launcher.Requests.Count);

        // At the end: the eight addresses in order, four searches, settings.json as read, no git call.
        AssertRequests(app, [.. G.Method2Requests]);
        Assert.Equal(4, probe.Lookups.Count);
        Assert.All(probe.Lookups, lookup => Assert.Equal(new GitLookup(F.Chosen, null), lookup));
        Assert.Equal(settings, File.ReadAllBytes(SettingsFile(data)));
        Assert.Empty(app.GitCalls());
    }

    /// <summary>
    /// Method 3, G2 with the helper missing and git working: F6 and Tab to "What the helper does",
    /// Enter opens the helper's section, F1 the start page.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G2_first_run_link_with_the_helper_missing()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path));
        using var app = await StartEmptyAsync(data, gitProbe: probe, askPassHelper: F.MissingHelper);

        // Step 1: the helper missing; a frame captured before the focus moves.
        await app.WaitForGitSetupAsync(guidance: G.HelperMissingGuidance);
        app.MoveToNeutral();
        var before = app.Capture();
        AssertState(app, G.HelperMissingTitle, G.HelperMissingGuidance, G.HelperMissingButtons, G.HelperMissingButtonTexts, G.WhatTheHelperDoes);
        Assert.Equal(F.MissingHelper, app.GitSetupText("GitSetupPath"));
        var version = app.Session.Shell.Git?.Version?.ToString() ?? throw new InvalidOperationException("No git version is known.");
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, G.HelperMissingStatusFormat, version), ((TextBlock)app.FindByAutomationId("GitStatus")).Text);
        Assert.Single(probe.Lookups);
        Assert.Empty(app.Launcher.Requests);

        // Step 2: F6 to Check again, Tab to Continue without it, Tab to the link; a second frame.
        await PressF6ToAsync(app, G.HelperMissingButtons[0]);
        foreach (var id in G.HelperMissingButtons.Skip(1).Append(G.GitSetupGuideLinkId))
        {
            app.PressKey(Key.Tab);
            await WaitForFocusAsync(app, id);
            Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        }

        app.MoveToNeutral();
        var focused = app.Capture();
        AssertGuideLinkFocused(app, G.WhatTheHelperDoes);
        Assert.True(app.ShowsGitSetupView(), "The first-run screen went: Continue without it was pressed.");
        Assert.Equal(G.HelperMissingTitle, app.GitSetupText(G.GitSetupTitleId));
        Assert.Empty(app.Launcher.Requests);
        AssertFrameClearOfInk(app, before, focused, G.WhatTheHelperDoes);
        var calls = app.GitCalls().Count;

        // Step 3: Enter opens the helper's section.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the launcher to hold one request");
        app.Settle();
        AssertRequests(app, G.HelperMissingAddress);
        Assert.Equal(G.HelperMissingTitle, app.GitSetupText(G.GitSetupTitleId));
        Assert.Single(probe.Lookups);
        Assert.Equal(calls, app.GitCalls().Count);

        // Step 4: F1 opens the start page.
        app.PressKey(Key.F1);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "the launcher to hold two requests");
        app.Settle();
        AssertRequests(app, [.. G.Method3Requests]);
        Assert.Equal(G.HelperMissingTitle, app.GitSetupText(G.GitSetupTitleId));
        Assert.Single(probe.Lookups);
        Assert.Equal(calls, app.GitCalls().Count);

        // At the end.
        Assert.False(File.Exists(SettingsFile(data)), "The steps wrote settings.json.");
        AssertNoStart(app);
    }

    /// <summary>
    /// Method 4, G3: on "ops", the settings screen on About by Ctrl+, five Downs and Enter; four Tabs
    /// to the guide link, underlined and framed outside its text; Enter opens the start page once.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G3_about_guide_link()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);

        // Step 1.
        var exact = await repo.ExactStateAsync();
        app.MoveToNeutral();

        // Step 2: Ctrl+, then Down to About, Enter; a frame captured with the link not focused.
        await app.OpenSettingsAsync(SettingsOpening.Key);
        await WaitForFocusAsync(app, G.NavGeneralId);
        foreach (var id in G.DownsToAbout)
        {
            app.PressKey(Key.Down);
            await WaitForFocusAsync(app, id);
        }

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true } && InSettings<Control>(app, G.AboutSectionId).IsEffectivelyVisible, "About shown");
        await app.WaitForSettingsReadsAsync();
        app.MoveToNeutral();
        var before = app.Capture();
        AssertAboutWithGuideRow(app, before);
        Assert.Equal(G.NavAboutId, app.FocusedAutomationId());
        Assert.True(app.FocusVisible, "SettingsNavAbout has the focus without the keyboard's frame.");
        Assert.Contains("selected", InSettings<Button>(app, G.NavAboutId).Classes);
        Assert.Empty(app.Launcher.Requests);
        var stepMark = await app.QuietStepMarkAsync();

        // Step 3: four Tabs to the guide link (g3a).
        foreach (var id in G.TabsToGuideLink)
        {
            app.PressKey(Key.Tab);
            await WaitForFocusAsync(app, id);
            Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        }

        var g3a = Picture(app, G.G3aAboutGuideLink);
        Assert.True(IsUnderlined(LinkText(InSettings<Button>(app, G.AboutGuideLinkId))), "The guide link is not underlined while focused.");
        Assert.False(IsUnderlined(LinkText(InSettings<Button>(app, G.AboutRepositoryLinkId))), "The repository link is underlined.");
        Assert.Empty(app.Launcher.Requests);
        AssertFramedLink(app, before, g3a, G.AboutGuideLinkId);

        // Step 4: Enter opens the start page once; the link keeps the focus.
        var logMark = app.LogMark();
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the launcher to hold one request");
        await app.WaitForIdleAsync();
        AssertRequests(app, G.StartPage);
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "The settings screen left About.");
        Assert.Equal(G.AboutGuideLinkId, app.FocusedAutomationId());
        Assert.True(app.FocusVisible, "The guide link lost the keyboard's frame.");
        AssertNotLogged(app, logMark, G.AboutGuideFailedLine);
        await app.AssertNoStepCallsAsync(stepMark, "About's guide link");

        // Step 5: Esc; nothing written.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        await AssertNoDialogAsync(app);
        AssertRequests(app, G.StartPage);
        Assert.False(File.Exists(SettingsFile(data)), "About wrote settings.json.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }
}
