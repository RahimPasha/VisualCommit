// Part L of phase 8a's walk-through, the contrast round (docs/test-reports/phase-8a.md, "Part L";
// docs/phase-8a-design.md 2.5; D410, D418): the placeholders in their own token at full opacity
// (L1 to L3), the dark danger on hovered, selected and control backgrounds (L4) and the danger
// button's dark label (L5), and the accent on hovered and selected rows (L6). Seven methods, seven
// starts, as the report's methods table has them; methods 1 and 2 are the twins of the real
// window's two launches (design 12.3.4 launches 1 and 2) and take their steps in the same order.
// The helpers are in Phase8aChecks.L.Helpers.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Commands;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part L: the contrast round (docs/test-reports/phase-8a.md; design 2.5).</summary>
public sealed partial class Phase8aChecksL : Phase8aChecks
{
    /// <summary>
    /// Method 1, L1, L5 dark, L2 and L5 light on a fresh "changes", dark then light (the twin of
    /// the real window's launch 1): the three placeholders and the counter, the focused filter and
    /// the typed <c>sea</c>, the discard's danger button with its dark label, then the same in light.
    /// Nothing is confirmed.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L1_L5_L2_changes_placeholders_and_danger_button()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();

        // L1 step 2: the working-changes row; the three boxes empty and unfocused.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app);
        var shot = Picture(app, L.L1aPlaceholdersDark);
        Assert.True(app.ShowsDarkTheme, "The window does not show the dark theme.");
        AssertThreePlaceholders(app, shot);

        // L1 step 3: Ctrl+Shift+E; the focused, empty filter box (a frame, not saved).
        await FocusFilterAsync(app);
        AssertPlaceholder(app, app.Capture(), Box(app, "FilterBox"), L.Filter, focused: true);

        // L1 step 4: sea typed.
        await TypeIntoFilterAsync(app);
        shot = Picture(app, L.L1bFilterTypedDark);
        AssertTyped(app, shot, Box(app, "FilterBox"), L.Typed);

        // L1 step 5: the filter emptied.
        await EmptyFilterAsync(app);

        // L5 steps 6 and 7, dark: the discard asked, then cancelled.
        await AskDiscardAsync(app);
        shot = Picture(app, L.L5aDiscardDialogDark);
        AssertDiscardDialog(app, shot);
        await CancelDiscardAsync(app, repo, exact);

        // L2 step 8: ThemeSwitch; the window light; the three boxes again.
        app.Click(app.FindByAutomationId("ThemeSwitch"));
        await app.WaitForAsync(() => !app.ShowsDarkTheme, "the window to show the light theme");
        shot = Picture(app, L.L2aPlaceholdersLight);
        AssertThreePlaceholders(app, shot);

        // L2 step 9: the focused, empty filter (a frame), then sea typed.
        await FocusFilterAsync(app);
        AssertPlaceholder(app, app.Capture(), Box(app, "FilterBox"), L.Filter, focused: true);
        await TypeIntoFilterAsync(app);
        shot = Picture(app, L.L2bFilterTypedLight);
        AssertTyped(app, shot, Box(app, "FilterBox"), L.Typed);

        // L2 step 10: the filter emptied.
        await EmptyFilterAsync(app);

        // L5 steps 11 and 12, light.
        await AskDiscardAsync(app);
        shot = Picture(app, L.L5bDiscardDialogLight);
        AssertDiscardDialog(app, shot);
        await CancelDiscardAsync(app, repo, exact);
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 2, L4 a and L6 a to c on a fresh "graph", dark (the twin of the real window's launch
    /// 2): <c>feature/login</c>'s menu by Shift+F10 with "Delete…" highlighted by the arrows; HEAD's
    /// <c>main</c> idle, hovered and selected.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L4a_L6_graph_danger_item_and_head_row()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        var colours = L.Dark;

        // L4 step 2: feature opened; login selected, with the focus; Shift+F10; Down to Delete feature/login….
        await OpenFeatureFolderAsync(app);
        var login = (Button)await app.RefRowAsync(L.LocalBranches, L.Feature, L.Login);
        app.Click(login);
        await app.WaitForAsync(() => login.IsFocused && IsSelected(app, login, L.LocalBranches, L.Feature, L.Login), "feature/login's row selected with the keyboard focus");
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Assert.Equal(L.FeatureLoginMenuHeader, menu.Header);
        for (var guard = 0; menu.Highlighted?.Label != L.DeleteFeatureLogin && guard < menu.Items.Count; guard++)
        {
            app.PressKey(Key.Down);
            menu = app.Menu();
        }

        AssertHighlighted(menu, L.DeleteFeatureLogin);
        var shot = Picture(app, L.L4aDeleteItemMenu, keepPointer: true);
        menu = app.Menu();
        var delete = menu.Item(L.DeleteFeatureLogin);
        Assert.True(delete.IsEnabled && delete.IsDestructive && delete.IsHighlighted, $"{L.DeleteFeatureLogin} is not enabled, destructive and highlighted.");
        Assert.Equal(colours.Danger, delete.TextColor);
        AssertBrush(colours.Danger, app.MenuItemControl(L.DeleteFeatureLogin).Foreground, $"{L.DeleteFeatureLogin}'s label (VcDangerBrush)");
        AssertColour(colours.Hover, shot, new Point(delete.Bounds.Right - 6, delete.Bounds.Top + 3), $"{L.DeleteFeatureLogin}'s highlight (VcHoverBackgroundBrush)");
        AssertInk(shot, delete.Bounds, colours.Danger, colours.Hover, $"{L.DeleteFeatureLogin}'s label", colours.DangerReplaced);
        Assert.Equal(colours.Lane3, TokenOf(app, "VcLane3Brush"));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // L6 steps 3 to 5: HEAD's main idle, hovered at its row's centre, selected.
        await AssertHeadRowAsync(app, L.L6aHeadIdle, L.L6bHeadHovered, L.L6cHeadSelected, otherSelected: (L.LocalBranches, L.Feature, L.Login));
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
    }

    /// <summary>
    /// Method 3, L3 on a fresh "graph", dark: the placeholder in the palette, the search bar, the
    /// Pick dialog, the Name dialog, the Remotes form and the settings screen's user name and email,
    /// each reached by an earlier check's route and closed unconfirmed.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L3_placeholders_in_every_other_box()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        app.MoveToNeutral();

        // Step 2: the palette.
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.TopDialogView is CommandPaletteView && app.PaletteBox.IsFocused, "the palette with its box focused");
        var shot = Picture(app, L.L3aPalette);
        AssertPlaceholder(app, shot, app.PaletteBox, L.PalettePlaceholder, focused: true);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 3: the search bar, on Commits.
        app.PressSearch();
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true } bar && bar.Mode == L.SearchModeCommits, "the search bar on Commits with its box focused");
        shot = Picture(app, L.L3bSearch);
        AssertPlaceholder(app, shot, Box(app, "SearchBox"), L.SearchPlaceholder, focused: true);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SearchBar() is null, "the search bar to close");
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 4: Set upstream… on feature/login: the Pick dialog.
        await OpenFeatureFolderAsync(app);
        app.RightClick(await app.RefRowAsync(L.LocalBranches, L.Feature, L.Login));
        app.Menu();
        app.ClickMenuItem(L.SetUpstreamItem);
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(L.SetUpstreamTitle, pick.Title);
        await app.WaitForAsync(() => DialogBox(app, "PickFilter").IsFocused, "PickFilter to have the keyboard focus");
        shot = Picture(app, L.L3cPickFilter);
        AssertPlaceholder(app, shot, DialogBox(app, "PickFilter"), L.Filter, focused: true);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 5: Create branch here… on HEAD's commit: the Name dialog.
        app.RightClick(app.GraphRowPoint(HeadRow));
        app.Menu();
        app.ClickMenuItem(L.CreateBranchItem);
        var name = await app.WaitForDialogAsync<NameDialogViewModel>();
        Assert.Equal(L.CreateBranchTitle, name.Title);
        await app.WaitForAsync(() => DialogBox(app, "NameBox").IsFocused, "NameBox to have the keyboard focus");
        shot = Picture(app, L.L3dBranchName);
        AssertPlaceholder(app, shot, DialogBox(app, "NameBox"), L.BranchName, focused: true);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 6: Manage remotes…; Add remote: the form's two boxes.
        await OpenRemotesDialogAsync(app);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "AddRemoteButton"));
        await app.WaitForAsync(() => DialogBox(app, "RemoteNameBox").IsFocused, "RemoteNameBox to have the keyboard focus");
        shot = Picture(app, L.L3eRemoteForm);
        AssertPlaceholder(app, shot, DialogBox(app, "RemoteNameBox"), L.RemoteName, focused: true);
        AssertPlaceholder(app, shot, DialogBox(app, "RemoteUrlBox"), L.RemoteUrl, focused: false);
        await CloseDialogsAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 7: the settings screen's Git section on the global scope: the user name and email.
        var settingsFile = SettingsText(data);
        app.MoveToNeutral();
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync();
        Assert.True(app.FindByAutomationId("GitScopeGlobal").Classes.Contains("selected"), "All repositories (global) is not selected.");
        Assert.Equal(L.GlobalScope, app.SettingsSnapshot().Field("GitScopeGlobal")?.Text);
        shot = Picture(app, L.L3fSettingsName);
        AssertPlaceholder(app, shot, Box(app, "UserNameBox"), L.NotSet, focused: false);
        AssertPlaceholder(app, shot, Box(app, "UserEmailBox"), L.NotSet, focused: false);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(settingsFile, SettingsText(data));
        Assert.False(File.Exists(ShellDriver.GlobalGitConfigPath(data.Path)), "The session's global git configuration was written.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 4, L4 b to d on a fresh "graph", dark: a destructive palette entry highlighted, a "D"
    /// hovered and selected in the commit details, the Remotes dialog's "Remove" at rest and hovered.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L4_danger_text_on_rows_and_controls()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        var colours = L.Dark;
        app.MoveToNeutral();

        // Step 2: the palette; delete main; its first row highlighted.
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette with its box focused");
        app.TypeInPalette(L.PaletteQuery);
        await app.WaitForAsync(() => app.PaletteSnapshot().Rows is [{ IsHighlighted: true } first, ..] && first.Label == L.DeleteMainOnOrigin, $"the palette's first row to read {L.DeleteMainOnOrigin}, highlighted");
        var shot = Picture(app, L.L4bDeleteEntryPalette);
        var row = app.PaletteSnapshot().Rows[0];
        Assert.Equal((L.DeleteMainOnOrigin, L.DeleteMainOnOriginDetail, true, true), (row.Label, row.Detail, row.IsEnabled, row.IsHighlighted));
        Assert.True(app.Palette.Shown[0].ShowsDanger, $"{L.DeleteMainOnOrigin} is not destructive.");
        Assert.Equal(colours.Danger, row.LabelColour);
        var label = PaletteLabelOf(app, 0);
        AssertText(app, shot, label, colours.Danger, colours.Selection, new Point(row.Bounds.Right - 4, row.Bounds.Top + 3), $"{L.DeleteMainOnOrigin}'s label", colours.DangerReplaced);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 3: Add settings page's details; the pointer rests on docs/notes.txt's row: hovered.
        app.Click(app.GraphRowPoint(HeadRow));
        await WaitForDetailsAsync(app, L.HeadCommit);
        var notes = await app.FileRowAsync("FileList", L.Notes);
        Assert.Equal(L.Notes, await app.RestPointerOnAsync(notes));
        shot = Picture(app, L.L4cDeletedFileHovered, keepPointer: true);
        Assert.True(notes.IsPointerOver, $"{L.Notes}'s row is not hovered.");
        var letter = StatusOf(notes);
        Assert.Equal("D", letter.Text);
        AssertText(app, shot, letter, colours.Danger, colours.Hover, ClearOf(app, notes), $"{L.Notes}'s D on a hovered row", colours.DangerReplaced);

        // Step 4: the row clicked: selected, its diff open; the pointer at the neutral point.
        app.Click(notes);
        await WaitForDiffAsync(app, L.Notes);
        notes = await app.FileRowAsync("FileList", L.Notes);
        shot = Picture(app, L.L4dDeletedFileSelected);
        Assert.True(notes.Classes.Contains("selected") && !notes.IsPointerOver, $"{L.Notes}'s row is not selected, or still hovered.");
        AssertText(app, shot, StatusOf(notes), colours.Danger, colours.Selection, ClearOf(app, notes), $"{L.Notes}'s D on a selected row", colours.DangerReplaced);
        await CloseDiffAsync(app);

        // Step 5: the Remotes dialog: origin's Remove at rest.
        await OpenRemotesDialogAsync(app);
        var remove = RemoveButton(app);
        shot = Picture(app, L.L4eRemoveButtonAtRest);
        Assert.Equal(L.RemoveRemote, remove.Content as string);
        Assert.False(remove.IsPointerOver, "Remove is hovered.");
        AssertText(app, shot, LabelOf(remove), colours.Danger, colours.Control, LeftPadding(app, remove), "Remove at rest", colours.DangerReplaced);

        // Step 6: the pointer moved onto Remove (it has no tooltip).
        app.MoveMouse(remove);
        await app.WaitForAsync(() => remove.IsPointerOver, "Remove to be hovered");
        shot = Picture(app, L.L4fRemoveButtonHovered, keepPointer: true);
        AssertText(app, shot, LabelOf(remove), colours.Danger, colours.Hover, LeftPadding(app, remove), "Remove hovered", colours.DangerReplaced);
        Assert.Null(ToolTip.GetTip(remove));

        // Step 7: Esc until no dialog shows.
        await CloseDialogsAsync(app);
        Assert.Equal(colours.Lane3, TokenOf(app, "VcLane3Brush"));
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Contains(L.Origin, (await repo.GitAsync("remote")).StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    /// Method 5, L6 d to f on a fresh "graph", dark: a renamed file's "R" in the commit details
    /// idle, hovered and selected; in the comparison of main with origin/main, selected; in the
    /// file history, its row selected.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L6_renamed_file_on_rows()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        var colours = L.Dark;

        // Step 2: Add settings page's details; the R idle.
        app.Click(app.GraphRowPoint(HeadRow));
        await WaitForDetailsAsync(app, L.HeadCommit);
        app.MoveToNeutral();
        var renamed = await app.FileRowAsync("FileList", L.MainApp);
        var shot = Picture(app, L.L6dRenamedIdle);
        await AssertRenamedAndDeletedAsync(app, shot, renamed, colours.Accent, colours.Panel, colours.AccentOnRow);

        // Step 3: the pointer rests on the row: hovered, its tooltip open.
        Assert.Equal(L.RenamedToolTip(L.MainApp, L.App), await app.RestPointerOnAsync(renamed));
        shot = Picture(app, L.L6eRenamedHovered, keepPointer: true);
        Assert.True(renamed.IsPointerOver, $"{L.MainApp}'s row is not hovered.");
        await AssertRenamedAndDeletedAsync(app, shot, renamed, colours.AccentOnRow, colours.Hover, colours.Accent);

        // Step 4: the row clicked: selected, its diff open.
        app.Click(renamed);
        await WaitForDiffAsync(app, L.MainApp);
        renamed = await app.FileRowAsync("FileList", L.MainApp);
        shot = Picture(app, L.L6fRenamedSelected);
        Assert.True(renamed.Classes.Contains("selected") && !renamed.IsPointerOver, $"{L.MainApp}'s row is not selected, or still hovered.");
        await AssertRenamedAndDeletedAsync(app, shot, renamed, colours.AccentOnRow, colours.Selection, colours.Accent);

        // Step 5: the diff closed; main's label dropped on origin/main's; Compare main with origin/main.
        await CloseDiffAsync(app);
        var from = app.GraphLabelBounds(HeadRow, L.Main).Center;
        var to = app.GraphLabelBounds(HeadRow + 1, "origin/main").Center;
        app.PressMouse(from);
        app.MoveMouseHeld(to, steps: 6);
        Assert.True(app.BranchDrag.IsDragging, "The moves did not start a drag.");
        app.ReleaseMouse(to);
        app.Menu();
        app.ClickMenuItem(L.CompareMainWithOriginMain);
        await app.WaitForAsync(() => app.Compare() is { IsLoading: false }, "the comparison");
        var comparison = app.Compare()!;
        Assert.Equal(("From origin/main", "To " + L.Main), (comparison.From, comparison.To));
        var compared = await app.FileRowAsync("CompareFileList", L.MainApp);
        Assert.Equal("R", StatusOf(compared).Text);
        Assert.Equal(L.RenamedToolTip(L.MainApp, L.App), ToolTip.GetTip(compared) as string);
        app.Click(compared);
        await WaitForDiffAsync(app, L.MainApp);
        compared = await app.FileRowAsync("CompareFileList", L.MainApp);
        shot = Picture(app, L.L6gCompareRenamedSelected);
        Assert.True(compared.Classes.Contains("selected"), $"The comparison's row of {L.MainApp} is not selected.");
        AssertText(app, shot, StatusOf(compared), colours.AccentOnRow, colours.Selection, ClearOf(app, compared), "the comparison's selected R", colours.Accent);

        // Step 6: the diff and the comparison closed; File history of src/main-app.txt; its row of Add settings page.
        await CloseDiffAsync(app);
        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null && app.Session.Shell.ActiveTab.Repository!.ShowsGraph, "the comparison to close and the graph to show");
        app.Click(app.GraphRowPoint(HeadRow));
        await WaitForDetailsAsync(app, L.HeadCommit);
        app.RightClick(await app.FileRowAsync("FileList", L.MainApp));
        app.Menu();
        app.ClickMenuItem(L.FileHistoryItem);
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForAsync(() => app.FileHistorySnapshot() is { IsLoading: false } page && page.Rows.Count > 0, "the file history's rows");
        var index = app.FileHistorySnapshot().Rows.ToList().FindIndex(history => history.Subject == L.HeadCommit);
        Assert.True(index >= 0, $"The file history has no row of {L.HeadCommit}.");
        app.Click(app.FileHistoryRowPoint(index));
        await app.WaitForAsync(() => app.FileHistorySnapshot().Selected?.Subject == L.HeadCommit, $"the history's row of {L.HeadCommit} to be selected");
        shot = Picture(app, L.L6hHistoryRenamedSelected);
        var history = app.FileHistorySnapshot().Rows[index];
        Assert.Equal(("R", true), (history.Letter, history.IsSelected));
        var item = HistoryItem(app, index);
        Assert.True(item.IsSelected, "The history's row is not selected (ListBoxItem:selected).");
        AssertText(app, shot, StatusOf(item), colours.AccentOnRow, colours.Selection, ClearOf(app, item), "the history's selected R", colours.Accent);
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>Method 6, L6 g on a fresh "changes", dark: the staged rename's "R" hovered, then selected.</summary>
    [AvaloniaFact]
    public async Task Check_L6_staged_rename_on_rows()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        var colours = L.Dark;

        // Step 2: the stage panel; the pointer rests on the staged src/helpers.py.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app);
        var row = await app.FileRowAsync("StagedFileList", L.Helpers);
        Assert.Equal(L.RenamedToolTip(L.Helpers, L.Util), await app.RestPointerOnAsync(row));
        var shot = Picture(app, L.L6iStagedRenamedHovered, keepPointer: true);
        var panel = StageRowOf(row);
        Assert.True(panel.IsPointerOver, $"{L.Helpers}'s row is not hovered.");
        var letter = StatusOf(row);
        Assert.Equal("R", letter.Text);
        AssertText(app, shot, letter, colours.AccentOnRow, colours.Hover, ClearOf(app, panel), $"{L.Helpers}'s R on a hovered row", colours.Accent);

        // Step 3: the row clicked: selected, its diff open.
        app.Click(row);
        await WaitForDiffAsync(app, L.Helpers);
        row = await app.FileRowAsync("StagedFileList", L.Helpers);
        shot = Picture(app, L.L6jStagedRenamedSelected);
        panel = StageRowOf(row);
        Assert.True(panel.Classes.Contains("selected") && !panel.IsPointerOver, $"{L.Helpers}'s row is not selected, or still hovered.");
        AssertText(app, shot, StatusOf(row), colours.AccentOnRow, colours.Selection, ClearOf(app, panel), $"{L.Helpers}'s R on a selected row", colours.Accent);
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>Method 7, L6 h on a fresh "graph", light from the start: HEAD's main idle, hovered and selected, the accent in all three.</summary>
    [AvaloniaFact]
    public async Task Check_L6_head_row_light()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path, theme: AppTheme.Light);
        Assert.False(app.ShowsDarkTheme, "The window does not show the light theme.");
        var exact = await repo.ExactStateAsync();

        await AssertHeadRowAsync(app, L.L6kHeadIdleLight, L.L6lHeadHoveredLight, L.L6mHeadSelectedLight, otherSelected: null);
        Assert.Equal(exact, await repo.ExactStateAsync());
    }
}
