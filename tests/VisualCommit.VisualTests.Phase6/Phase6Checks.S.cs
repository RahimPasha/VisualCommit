// Part S of phase 6's scripted walk-through (docs/test-reports/phase-6.md, "Part S: the settings
// screen and its effects"): checks S14 to S21 of design 17.4. This class is part S's alone; its
// expected texts are in Phase6Expectations.S.cs (Phase6Expectations.PartS). One test method per app
// start, named as part S's "Test methods and app starts" names them (SD1): thirteen methods,
// fourteen app starts (S15's restart a plain [Fact] with two halves). This file holds S14 (the
// screen, its sections, entries and Cancel, at 1100×700 and 1920×1080); Phase6Checks.S.Theme.cs
// S15 (the System theme followed), Phase6Checks.S.General.cs S16 (auto-fetch and the stash
// setting), Phase6Checks.S.Editor.cs S17 (Open file's extensions and the editor),
// Phase6Checks.S.Identity.cs S18 (the identity), Phase6Checks.S.Pull.cs S19 (the pull modes),
// Phase6Checks.S.MergeTool.cs S20 (the merge tool), Phase6Checks.S.Ignored.cs S21 (Keep ignored
// files), and Phase6Checks.S.Helpers.cs what they share.
using Avalonia;
using Avalonia.Automation;
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

/// <summary>The checks of part S: the settings screen and its effects (S14 to S21).</summary>
public sealed partial class Phase6ChecksS : Phase6Checks
{
    /// <summary>
    /// Check S14, part A (steps 1 to 13): the toolbar's Settings; the screen opened by the button,
    /// its card, section list, first focus, Up and Down, Enter, and General, Tools, Git (on the
    /// global scope, This repository a click away, edits kept per scope) and Keyboard shortcuts;
    /// the editor's Pick dialog, Other… and Browse… with a program that does not exist, refused at
    /// Save; Cancel and Esc changing nothing; Ctrl+, and the palette's three entries; and an empty
    /// tab, where This repository is disabled.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_S14_settings_screen_sections_and_entries()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: a session of two tabs, the copy active and an empty one; the seed (SD3).
        using var app = await StartAsync(data, repo, PartS.SeedConfigOnly, [repo.Path, null]);
        var configAtStart = ConfigText(repo);
        var start = app.CallMark();
        AssertGraph(app, PartS.St0, PartS.StLanes, working: null);
        Assert.Equal([PartS.Settings, "New tab"], app.Session.Shell.Tabs.Select(tab => tab.Title));
        Assert.Equal(PartS.Settings, app.Session.Shell.ActiveTab.Title);
        AssertToolbarGroups(app);
        AssertSettingsButton(app);
        await AssertNoDialogAsync(app);
        var folder = await WorkingTreeAsync(repo);

        layout.Step = "step 2";
        // Step 2: a click on Settings; the screen on General, its reads ended.
        var sequence = app.CallMark();
        app.Click(app.FindByAutomationId("SettingsButton"));
        var screen = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        await app.WaitForSettingsReadsAsync(screen);
        Assert.Empty(app.Session.Services.DialogHost.Open.OfType<ConfirmationViewModel>());
        Assert.Equal(PartS.Title, AutomationProperties.GetName(app.TopDialogView!));
        Assert.Equal("SettingsDialog", AutomationProperties.GetAutomationId(app.TopDialogView!));
        ShotAtRest(app, "s14a-general");
        AssertFrame(app, layout, PartS.CardAt1100, bodyHeight: 493, buttonsTop: 610, buttonsBottom: 640);
        AssertNav(app, "SettingsNavGeneral");
        await AssertFocusAsync(app, "SettingsNavGeneral");
        Assert.IsNotType<TextBox>(app.FocusedElement);
        AssertGeneral(app, layout);
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 128);

        layout.Step = "step 3";
        // Step 3: Down twice moves the focus down the list, General still shown; Enter shows Tools and saves nothing.
        sequence = app.CallMark();
        app.PressKey(Key.Down);
        await AssertFocusAsync(app, "SettingsNavGit");
        Assert.Equal(SettingsSection.General, screen.SelectedSection);
        app.PressKey(Key.Down);
        await AssertFocusAsync(app, "SettingsNavTools");
        Assert.Equal(SettingsSection.General, screen.SelectedSection);
        app.PressKey(Key.Enter);
        app.Settle();
        Assert.Equal(SettingsSection.Tools, screen.SelectedSection);
        ShotAtRest(app, "s14b-tools");
        AssertNav(app, "SettingsNavTools");
        await AssertFocusAsync(app, "SettingsNavTools");
        Assert.False(screen.IsClosed, "Enter on a section button closed the screen.");
        Assert.False(Named<TextBlock>(app, "SettingsSaving").IsEffectivelyVisible, "SettingsSaving shows.");
        AssertNoSettingsFile(data);
        Assert.Empty(app.CallsSince(sequence));
        AssertToolsNone(app);

        layout.Step = "step 4";
        // Step 4: Git, on the global scope though a repository's tab is active (D200).
        app.ChooseSection(SettingsSection.Git);
        ShotAtRest(app, "s14c-git-global");
        AssertNav(app, "SettingsNavGit");
        AssertGitGlobal(app, layout, data, repo, repositoryFolder: folder);
        AssertFits(app, layout);

        layout.Step = "step 5";
        // Step 5: This repository; a scope switch reads nothing (SD29).
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s14d-git-repository");
        AssertGitRepository(
            app,
            layout,
            folder,
            name: string.Empty,
            namePlaceholder: PartS.NotSet,
            email: string.Empty,
            emailPlaceholder: PartS.NotSet,
            identity: PartS.NoEmailSet,
            identityNotices: [PartS.UseConfigOnly],
            checkedPull: "PullNotSet",
            pullInEffect: PartS.PullNothingSet(PartS.Main));
        Assert.Null(app.SettingsSnapshot().Field("BranchRebaseNotice"));
        AssertFits(app, layout);
        Assert.Empty(app.CallsSince(sequence));

        layout.Step = "step 6";
        // Step 6: each scope keeps its own edit (D200).
        app.TypeInto("UserNameBox", PartS.LocalName);
        app.ChooseScope(SettingsScope.Global);
        app.TypeInto("UserNameBox", PartS.GlobalName);
        app.ChooseScope(SettingsScope.Repository);
        AssertBox(app.SettingsSnapshot(), "UserNameBox", PartS.LocalName);
        app.ChooseScope(SettingsScope.Global);
        AssertBox(app.SettingsSnapshot(), "UserNameBox", PartS.GlobalName);
        AssertNoErrorOrWarning(app);
        AssertNoWrite(app, start);

        layout.Step = "step 7";
        // Step 7: Keyboard shortcuts, its top, then scrolled to its end.
        app.ChooseSection(SettingsSection.KeyboardShortcuts);
        ShotAtRest(app, "s14e-shortcuts-top");
        AssertNav(app, "SettingsNavShortcuts");
        AssertShortcuts(app, layout);
        await ScrollSectionToEndAsync(app);
        ShotAtRest(app, "s14f-shortcuts-end");
        AssertLastGroupInView(app, layout);

        layout.Step = "step 8";
        // Step 8: Tools, Choose…: the Pick dialog stacked over the screen; Other…, Choose.
        app.ChooseSection(SettingsSection.Tools);
        app.Click(app.FindByAutomationId("EditorChooseButton"));
        await AssertPickAsync(app, layout, PartS.ChooseEditorTitle, PartS.EditorOptions, PartS.EditorNone);
        await app.PickAsync(PartS.OtherEditor);
        AssertToolsOther(app, layout, program: string.Empty);
        await AssertFocusAsync(app, "EditorProgramBox");

        layout.Step = "step 9";
        // Step 9: Browse… answered with a program that does not exist (SD16); Save refused by its validation.
        app.Folders.AnswerFile(PartS.MissingProgram);
        sequence = app.CallMark();
        app.Click(app.FindByAutomationId("EditorBrowseButton"));
        await app.WaitForAsync(() => app.Folders.FileTitles.Count == 1, "the file dialog to be asked for");
        var saved = await app.SaveSettingsAsync();
        Assert.False(saved, "Save closed the screen.");
        ShotAtRest(app, "s14g-tools-other-error");
        Assert.Equal([PartS.ChooseProgramTitle], app.Folders.FileTitles);
        Assert.Empty(app.Folders.Titles);
        AssertToolsOther(app, layout, program: PartS.MissingProgram, error: PartS.NoFileAtPath);
        AssertNav(app, "SettingsNavTools", "SettingsNavTools");
        await AssertFocusAsync(app, "EditorProgramBox");
        Assert.False(screen.IsClosed);
        AssertNoSettingsFile(data);
        AssertNoWrite(app, start);
        Assert.Equal(PartS.SeedConfigOnly, GlobalText(data));
        Assert.Equal(configAtStart, ConfigText(repo));

        layout.Step = "step 10";
        // Step 10: Cancel: closed at once, without a question; nothing written.
        app.CancelSettings();
        await WaitForScreenClosedAsync(app, screen);
        await AssertNoDialogAsync(app);
        await AssertFocusAsync(app, "SettingsButton");
        AssertNoSettingsFile(data);
        Assert.Equal(PartS.SeedConfigOnly, GlobalText(data));
        Assert.Equal(configAtStart, ConfigText(repo));
        AssertNoWrite(app, start);

        layout.Step = "step 11";
        // Step 11: Ctrl+, opens a new screen on General as after step 2; Esc closes it.
        app.PressKey(Key.OemComma, RawInputModifiers.Control);
        var second = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        await app.WaitForSettingsReadsAsync(second);
        Assert.NotSame(screen, second);
        Assert.Equal(SettingsSection.General, second.SelectedSection);
        AssertNav(app, "SettingsNavGeneral");
        await AssertFocusAsync(app, "SettingsNavGeneral");
        Assert.Equal(PartS.EditorNone, second.Tools.EditorValue);
        await EscapeScreenAsync(app);
        AssertNoWrite(app, start);
        AssertNoSettingsFile(data);

        layout.Step = "step 12";
        // Step 12: the palette's three entries (SD26).
        await RunPaletteEntryAsync(app, "settings", PartS.PaletteOpenSettings, "Ctrl+,", SettingsSection.General, "SettingsNavGeneral");
        await RunPaletteEntryAsync(app, "keyboard shortcuts", PartS.PaletteShowShortcuts, null, SettingsSection.KeyboardShortcuts, "SettingsNavShortcuts");
        await RunPaletteEntryAsync(app, "editor", PartS.PaletteChooseEditor, null, SettingsSection.Tools, "SettingsNavTools");
        AssertNoWrite(app, start);
        AssertNoSettingsFile(data);

        layout.Step = "step 13";
        // Step 13: the empty tab: Settings there too; This repository disabled; the outside read alone.
        app.Click(TabButton(app, "New tab"));
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Title == "New tab", "the empty tab to be active");
        AssertSettingsButton(app);
        sequence = app.CallMark();
        var third = await app.OpenSettingsAsync();
        Assert.Equal(SettingsSection.General, third.SelectedSection);
        AssertNav(app, "SettingsNavGeneral");
        app.ChooseSection(SettingsSection.Git);
        AssertGitGlobal(app, layout, data, repo: null);
        await AssertScreenReadsAsync(app, sequence, data, repo: null);
        await EscapeScreenAsync(app);
        AssertNoWrite(app, start);
        AssertNoSettingsFile(data);
        Assert.Equal(PartS.SeedConfigOnly, GlobalText(data));
        Assert.Equal(configAtStart, ConfigText(repo));
        layout.AssertNone();
    }

    /// <summary>
    /// Check S14, part B (steps 14 to 16): the screen at 1920×1080, its card exactly at (530, 200),
    /// 860 × 680, the card not growing; General, Git on the global scope and Keyboard shortcuts;
    /// nothing written.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_S14_settings_screen_at_1920x1080()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 14" };

        // Step 14: one tab, the same seed, 1920×1080; Ctrl+,.
        using var app = await StartAsync(data, repo, PartS.SeedConfigOnly, width: 1920, height: 1080);
        var configAtStart = ConfigText(repo);
        var start = app.CallMark();
        var folder = await WorkingTreeAsync(repo);
        AssertGraph(app, PartS.St0, PartS.StLanes, working: null);
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        ShotAtRest(app, "s14h-general-1920");
        AssertFrame(app, layout, PartS.CardAt1920, bodyHeight: 553, buttonsTop: 830, buttonsBottom: 860);
        AssertNav(app, "SettingsNavGeneral");
        await AssertFocusAsync(app, "SettingsNavGeneral");
        AssertGeneral(app, layout);
        await AssertScreenReadsAsync(app, start, data, repo, varExit: 128);

        layout.Step = "step 15";
        // Step 15: Git on the global scope (D200), at these coordinates.
        app.ChooseSection(SettingsSection.Git);
        ShotAtRest(app, "s14i-git-global-1920");
        AssertNav(app, "SettingsNavGit");
        AssertGitGlobal(app, layout, data, repo, repositoryFolder: folder);
        AssertFits(app, layout);

        layout.Step = "step 16";
        // Step 16: Keyboard shortcuts as after step 7; Esc; nothing written.
        app.ChooseSection(SettingsSection.KeyboardShortcuts);
        AssertShortcuts(app, layout);
        await EscapeScreenAsync(app);
        Assert.True(screen.IsClosed);
        AssertNoWrite(app, start);
        AssertNoSettingsFile(data);
        Assert.Equal(PartS.SeedConfigOnly, GlobalText(data));
        Assert.Equal(configAtStart, ConfigText(repo));
        layout.AssertNone();
    }

    /// <summary>
    /// The toolbar's groups as S14 step 1 gives them (7.2, D197): the right group Settings, Search,
    /// Theme in one row, Settings directly left of Search, Theme ending within 20 of the window's
    /// right edge; the left group Undo to Pop in its order in the same row, ending before Settings.
    /// </summary>
    private static void AssertToolbarGroups(ShellDriver app)
    {
        var right = new[] { "SettingsButton", "SearchButton", "ThemeSwitch" }.Select(id => app.BoundsOf(ToolbarButton(app, id))).ToList();
        Assert.True(right.All(bounds => Math.Abs(bounds.Y - right[0].Y) < 0.5), "The right group is not in one row.");
        Assert.Equal(right[0].Right, right[1].X, 0.5);
        Assert.True(right[1].Right <= right[2].X + 0.5, "Theme is not right of Search.");
        Assert.True(app.Window.ClientSize.Width - right[2].Right <= 20, "Theme does not end within 20 of the window's right edge.");
        var left = new[] { "UndoButton", "RedoButton", "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton" }.Select(id => app.BoundsOf(ToolbarButton(app, id))).ToList();
        Assert.True(left.Zip(left.Skip(1)).All(pair => pair.First.Right <= pair.Second.X + 0.5 && Math.Abs(pair.First.Y - pair.Second.Y) < 0.5), "The left group is not Undo to Pop in one row.");
        Assert.True(left[^1].Right < right[0].X, "The left group does not end before Settings.");
        Assert.Equal(right[0].Y, left[0].Y, 0.5);
    }

    /// <summary>Tools with no editor (7.5, S14 step 3): <c>Editor</c>, <c>None</c>, no program, <c>Choose…</c> enabled, the note; no Program or Arguments row, no error, no <c>EditorStatus</c>.</summary>
    private static void AssertToolsNone(ShellDriver app)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Tools, snapshot.Section);
        Assert.Contains(Named<StackPanel>(app, "SettingsTools").GetVisualDescendants().OfType<TextBlock>(), block => block.Classes.Contains("settingsLabel") && block.Text == PartS.EditorLabel && block.IsEffectivelyVisible);
        AssertText(snapshot, "EditorValue", PartS.EditorNone);
        AssertHidden(snapshot, "EditorProgram");
        AssertText(snapshot, "EditorChooseButton", PartS.Choose);
        Assert.True(Field(snapshot, "EditorChooseButton").IsEnabled);
        AssertText(snapshot, "EditorNote", PartS.EditorNote);
        Assert.Equal(PaletteOf(app).TextSecondary, (Named<TextBlock>(app, "EditorNote").Foreground as ISolidColorBrush)?.Color);
        AssertHidden(snapshot, "EditorProgramBox");
        AssertHidden(snapshot, "EditorArgumentsBox");
        AssertHidden(snapshot, "EditorError");
        AssertHidden(snapshot, "EditorStatus");
    }

    /// <summary>Tools with Other… (7.5, 12.5, S14 steps 8 and 9): <c>Other</c>; Program (320 wide) with Browse…, Arguments <c>{path}</c> and its hint; <c>EditorError</c> as given (none when null), in the danger colour under the Arguments rows.</summary>
    private static void AssertToolsOther(ShellDriver app, Layout layout, string program, string? error = null)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Tools, snapshot.Section);
        AssertText(snapshot, "EditorValue", PartS.EditorOther);
        AssertHidden(snapshot, "EditorProgram");
        AssertBox(snapshot, "EditorProgramBox", program);
        layout.Near(320, Named<TextBox>(app, "EditorProgramBox").Bounds.Width, "EditorProgramBox's width");
        AssertText(snapshot, "EditorBrowseButton", PartS.Browse);
        AssertBox(snapshot, "EditorArgumentsBox", PartS.DefaultArguments);
        layout.Near(320, Named<TextBox>(app, "EditorArgumentsBox").Bounds.Width, "EditorArgumentsBox's width");
        AssertText(snapshot, "EditorArgumentsHint", PartS.ArgumentsHint);
        var labels = Named<StackPanel>(app, "SettingsTools").GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("settingsLabel") && block.IsEffectivelyVisible).Select(block => block.Text).ToList();
        Assert.Equal([PartS.EditorLabel, PartS.ProgramLabel, PartS.ArgumentsLabel], labels);
        if (error is null)
        {
            AssertHidden(snapshot, "EditorError");
        }
        else
        {
            AssertText(snapshot, "EditorError", error);
            var block = Named<TextBlock>(app, "EditorError");
            Assert.Contains("dialogError", block.Classes);
            Assert.Equal(PaletteOf(app).Danger, (block.Foreground as ISolidColorBrush)?.Color);
            layout.Check(app.BoundsOf(block).Y >= app.BoundsOf(Named<TextBlock>(app, "EditorArgumentsHint")).Bottom - 0.5, "EditorError is not under the Arguments rows.");
        }
    }

    /// <summary>
    /// Keyboard shortcuts (7.6, S14 step 7): <c>These keys are fixed.</c>, then the eleven groups in
    /// 6.11's order, each heading 12.5 SemiBold, with exactly the report's 50 rows and keys; each row
    /// at least 22 high, its action at the left and its keys at the right in the code font and the
    /// secondary colour, apart and not cut, named <c>{label}: {key text}</c>; the Extend row's keys
    /// on two lines, every other row one; the Repository group's note under its rows and no other
    /// note; the section scrolls.
    /// </summary>
    private static void AssertShortcuts(ShellDriver app, Layout layout)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.KeyboardShortcuts, snapshot.Section);
        AssertText(snapshot, "ShortcutsIntro", PartS.ShortcutsIntro);
        Assert.Equal(PaletteOf(app).TextSecondary, (Named<TextBlock>(app, "ShortcutsIntro").Foreground as ISolidColorBrush)?.Color);
        var list = Named<ItemsControl>(app, "ShortcutList");
        var groups = list.GetVisualDescendants().OfType<TextBlock>().Where(block => AutomationProperties.GetAutomationId(block) == "ShortcutGroup").ToList();
        Assert.Equal(PartS.ShortcutGroups.Select(group => group.Group), groups.Select(group => group.Text));
        Assert.All(groups, group => Assert.Equal((12.5, FontWeight.SemiBold), (group.FontSize, group.FontWeight)));
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(grid => AutomationProperties.GetAutomationId(grid) == "ShortcutRow").ToList();
        var expected = PartS.ShortcutGroups.SelectMany(group => group.Rows).ToList();
        Assert.Equal(expected.Count, rows.Count);
        var code = (FontFamily)Application.Current!.FindResource("VcCodeFontFamily")!;
        var palette = PaletteOf(app);
        foreach (var (row, (action, keys)) in rows.Zip(expected))
        {
            var blocks = row.Children.OfType<TextBlock>().ToList();
            Assert.Equal((action, keys), (blocks[0].Text, blocks[1].Text));
            Assert.Equal($"{action}: {keys}", AutomationProperties.GetName(row));
            layout.Check(row.Bounds.Height >= 22 - 0.01, $"the row {action} is {row.Bounds.Height} high, less than 22.");
            Assert.Equal(code, blocks[1].FontFamily);
            Assert.Equal(palette.TextSecondary, (blocks[1].Foreground as ISolidColorBrush)?.Color);
            var actionBounds = blocks[0].Bounds;
            var keysBounds = blocks[1].Bounds;
            layout.Check(actionBounds.X < 0.5 && keysBounds.Right >= row.Bounds.Width - 0.5 && actionBounds.Right <= keysBounds.X, $"the row {action}'s action and keys are not apart at its left and right: {actionBounds}, {keysBounds} in {row.Bounds.Width}.");
            layout.Check(actionBounds.Bottom <= row.Bounds.Height + 0.5 && keysBounds.Bottom <= row.Bounds.Height + 0.5, $"the row {action}'s texts pass the row.");
            var lines = blocks[1].TextLayout.TextLines.Count;
            layout.Check(lines == (action == PartS.ExtendSelection ? 2 : 1), $"the row {action}'s keys take {lines} lines.");
            layout.Check(blocks[0].TextLayout.TextLines.Count == 1, $"the row {action}'s action takes {blocks[0].TextLayout.TextLines.Count} lines.");
            layout.Check(!blocks[1].TextLayout.TextLines.Any(line => line.HasCollapsed) && !blocks[0].TextLayout.TextLines.Any(line => line.HasCollapsed), $"the row {action}'s text is cut.");
            if (action == PartS.ExtendSelection)
            {
                layout.Check(row.Bounds.Height > 22, "the Extend row is not taller than 22.");
            }
        }

        var notes = list.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("settingsNote") && block.IsEffectivelyVisible).ToList();
        var note = Assert.Single(notes);
        Assert.Equal(PartS.RepositoryNote, note.Text);
        var repositoryGroup = groups.Single(group => group.Text == PartS.NotedGroup);
        var lastRepositoryRow = rows[expected.FindIndex(row => row.Action == "Show reflog")];
        layout.Check(app.BoundsOf(note).Y >= app.BoundsOf(lastRepositoryRow).Bottom - 0.5 && app.BoundsOf(note).Y > app.BoundsOf(repositoryGroup).Y, "the Repository note is not under its rows.");
        var section = Named<ScrollViewer>(app, "SectionScroller");
        layout.Check(section.Extent.Height > section.Viewport.Height + 0.5, "the Keyboard shortcuts section does not scroll.");
    }

    /// <summary>Scrolls the shown section (<c>SettingsSection</c>) to its end with the wheel, as a user would.</summary>
    private static async Task ScrollSectionToEndAsync(ShellDriver app)
    {
        var section = Named<ScrollViewer>(app, "SectionScroller");
        var point = app.BoundsOf(section).Center;
        for (var turn = 0; turn < 60 && section.Offset.Y < section.Extent.Height - section.Viewport.Height - 0.5; turn++)
        {
            app.Wheel(point, -3);
        }

        await app.WaitForAsync(() => section.Offset.Y >= section.Extent.Height - section.Viewport.Height - 0.5, "the section to be scrolled to its end");
        Rest(app);
    }

    /// <summary>The last group, Dialogs and menus, with its three rows wholly in view (S14 step 7's second picture).</summary>
    private static void AssertLastGroupInView(ShellDriver app, Layout layout)
    {
        var section = app.BoundsOf(Named<ScrollViewer>(app, "SectionScroller"));
        var list = Named<ItemsControl>(app, "ShortcutList");
        var last = list.GetVisualDescendants().OfType<TextBlock>().Last(block => AutomationProperties.GetAutomationId(block) == "ShortcutGroup");
        Assert.Equal(PartS.LastGroup, last.Text);
        layout.Check(app.BoundsOf(last).Y >= section.Y - 0.5, "the last group's heading is not in view.");
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(grid => AutomationProperties.GetAutomationId(grid) == "ShortcutRow").TakeLast(3).ToList();
        Assert.Equal(PartS.ShortcutGroups[^1].Rows.Select(row => row.Action), rows.Select(row => row.Children.OfType<TextBlock>().First().Text));
        foreach (var row in rows)
        {
            layout.Check(app.BoundsOf(row).Y >= section.Y - 0.5 && app.BoundsOf(row).Bottom <= section.Bottom + 0.5, $"the row {AutomationProperties.GetName(row)} is not wholly in view.");
        }
    }

    /// <summary>
    /// S14 step 12: Ctrl+Shift+P, <paramref name="query"/> typed: the first row, highlighted, is
    /// <paramref name="label"/> with its key (or none); Enter closes the palette and opens the screen
    /// on <paramref name="section"/>, its button selected and focused; the run logged; Esc closes the screen.
    /// </summary>
    private static async Task RunPaletteEntryAsync(ShellDriver app, string query, string label, string? key, SettingsSection section, string navId)
    {
        var mark = app.LogMark();
        app.PressKey(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.IsPaletteOpen, "the command palette to open");
        app.TypeInPalette(query);
        var palette = app.PaletteSnapshot();
        var first = palette.Rows[0];
        Assert.True(first.Label == label && first.IsHighlighted, $"The palette's first row for \"{query}\" is \"{first.Label}\" ({(first.IsHighlighted ? "highlighted" : "not highlighted")}), not \"{label}\". Its rows: {string.Join(" | ", palette.Rows.Select(row => row.Label))}.");
        Assert.Equal(key, first.KeyText);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the command palette to close");
        var screen = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        await app.WaitForSettingsReadsAsync(screen);
        Assert.Equal(section, screen.SelectedSection);
        AssertNav(app, navId);
        await AssertFocusAsync(app, navId);
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.PaletteRan(PartS.Settings, label)), mark));
        await EscapeScreenAsync(app);
        await AssertNoDialogAsync(app);
    }

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
}
