using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The focus given back after a run that took it away (docs/phase-8a-design.md 6.3.4, 6.3.5, 6.3.7;
/// D104): a button whose command asks first and is disabled while it runs, pressed by the keys and
/// its question cancelled (the diff's Discard file and Discard lines, the stage panel's Discard all,
/// Stage all's question on conflict markers, Restore's question after a later change); a row's
/// Stage or Discard chosen from its menu opened by the keys; Amend, focused when a commit started
/// from the palette.
/// </summary>
public sealed partial class KeyboardPassTests
{
    /// <summary>
    /// The diff's "Discard file", reached by Shift+Tab from the text and pressed by Enter, cancelled
    /// in its question by Esc and then, pressed again, by Enter on the question's focused Cancel:
    /// each time "Discard file" has the focus back with the keyboard's frame, and nothing changed.
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_file_by_the_keys_cancelled_in_its_question_gives_the_focus_back_to_it_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var exact = await repo.ExactStateAsync();
        await app.TabToAsync(focused => IsButton(focused, "DiscardFileButton"), "Discard file", shift: true);

        foreach (var cancel in new[] { "Esc", "Cancel" })
        {
            await CancelQuestionByTheKeysAsync(app, cancel);

            Assert.True(app.FocusedAutomationId() == "DiscardFileButton", $"After {cancel} in the question the focus is on {app.DescribeFocus()}.");
            Assert.True(app.FocusVisible, $"Discard file shows no keyboard frame after {cancel}.");
        }

        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// "Discard file" clicked, its question cancelled by Esc: the mouse's path is as before, and the
    /// focus is not given back with the keyboard's frame (passes before the fix too).
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_file_clicked_and_cancelled_is_not_given_the_focus_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var exact = await repo.ExactStateAsync();

        app.Click(app.FindByAutomationId("DiscardFileButton"));
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => question.IsClosed, "the question to close by Esc");
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.False(app.FocusedAutomationId() == "DiscardFileButton" && app.FocusVisible, "Discard file, clicked, has the focus back with the keyboard's frame.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Two lines of the remaining hunk chosen by the arrows (as G1 chooses them), "Discard lines"
    /// reached by Shift+Tab from the text and pressed by Enter, its question cancelled by Esc: the
    /// cancel leaves no line selected (as before), so "Discard lines" is gone and the text, where the
    /// lines were chosen, has the focus, without a frame; nothing changed.
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_lines_by_the_keys_cancelled_in_its_question_gives_the_focus_to_the_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var exact = await repo.ExactStateAsync();
        var diff = Repository(app).Diff!;
        app.PressKey(Key.Home, RawInputModifiers.Control);
        var downs = 1 + diff.Diff!.Hunks[0].Lines.Count + Phase8aExpectations.K.DownsToLines;
        for (var down = 0; down < downs; down++)
        {
            app.PressKey(Key.Down);
        }

        for (var down = 0; down < Phase8aExpectations.K.ShiftDownsForLines; down++)
        {
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        await app.WaitForAsync(() => diff.SelectedChanges.Count == 2, "two changed lines selected");
        await app.TabToAsync(focused => IsButton(focused, "DiscardLinesButton"), "Discard lines", shift: true);

        await CancelQuestionByTheKeysAsync(app, "Esc");

        Assert.Empty(diff.SelectedChanges);
        Assert.True(app.FocusedAutomationId() == "DiffText", $"After Esc in the question the focus is on {app.DescribeFocus()}.");
        Assert.False(app.FocusVisible, "The text shows the keyboard's frame after Esc.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// The stage panel's "Discard all", reached by Shift+Tab from the first unstaged row, pressed by
    /// Enter, its question cancelled by Esc: "Discard all" has the focus back with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_all_by_the_keys_cancelled_in_its_question_gives_the_focus_back_to_it_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var exact = await repo.ExactStateAsync();
        await FocusReadmesRowAsync(app);
        await app.TabToAsync(focused => IsButton(focused, "DiscardAllButton"), "Discard all", shift: true);

        await CancelQuestionByTheKeysAsync(app, "Esc");

        Assert.True(app.FocusedAutomationId() == "DiscardAllButton", $"After Esc in the question the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Discard all shows no keyboard frame after Esc.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// A merge stopped with both.txt conflicted, its markers in the file: "Stage all", reached by
    /// Shift+Tab from the conflicted row, pressed by Enter, asks about the markers; Esc: "Stage all"
    /// has the focus back with the frame, and both.txt is still conflicted.
    /// </summary>
    [AvaloniaFact]
    public async Task Stage_all_by_the_keys_cancelled_in_its_markers_question_gives_the_focus_back_to_it_with_the_frame()
    {
        using var repo = await Scenarios.ConflictAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var exact = await repo.ExactStateAsync();
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == "both.txt", $"the focus on both.txt's row (it is on {app.DescribeFocus()})");
        await ShiftTabToAsync(app, "StageAllButton");

        await CancelQuestionByTheKeysAsync(app, "Esc");

        Assert.True(app.FocusedAutomationId() == "StageAllButton", $"After Esc in the question the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Stage all shows no keyboard frame after Esc.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// README.md's unstaged change discarded, then README.md changed again, so that Restore asks
    /// first (D75): "Restore", reached by Shift+Tab from the first unstaged row, pressed by Enter, its
    /// question cancelled by Esc: "Restore" has the focus back with the frame, and nothing changed.
    /// </summary>
    [AvaloniaFact]
    public async Task Restore_by_the_keys_cancelled_in_its_question_gives_the_focus_back_to_it_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        var changes = repository.Changes;

        // The discard, run by its command and confirmed by a click: the graph keeps the focus.
        changes.DiscardFileCommand.Execute(changes.Unstaged.Rows.Single(row => row.Key == Phase8aExpectations.K.Readme));
        var discard = await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => discard.IsClosed, "the discard's question to close");
        await app.WaitForAsync(() => changes.HasRestore && changes.UnstagedTitle == Phase8aExpectations.K.Unstaged6, "the discard and its restore bar");
        await app.WaitForIdleAsync(repository);
        repo.WriteFile(Phase8aExpectations.K.Readme, "Changed again after the discard.\n");
        await app.WaitForAsync(() => changes.UnstagedTitle == Phase8aExpectations.K.Unstaged7, "README.md changed again");
        await app.WaitForIdleAsync(repository);
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();
        var exact = await repo.ExactStateAsync();
        await FocusReadmesRowAsync(app);
        await ShiftTabToAsync(app, "RestoreDiscardButton");

        await CancelQuestionByTheKeysAsync(app, "Esc");

        Assert.True(app.FocusedAutomationId() == "RestoreDiscardButton", $"After Esc in the question the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Restore shows no keyboard frame after Esc.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// "Stage" chosen from README.md's menu, opened by Shift+F10 on its row's own button reached by F6:
    /// once README.md has left the unstaged list, the row now at its place, assets/logo.png's, has the
    /// focus with the keyboard's frame, as after the row's own Stage (6.3.4).
    /// </summary>
    [AvaloniaFact]
    public async Task Stage_chosen_from_a_rows_menu_by_the_keys_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        await FocusReadmesRowAsync(app);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Assert.Equal("Stage", menu.Highlighted?.Label);

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        await WaitForReadmeStagedAsync(app, repo, repository);

        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Logo, $"the focus on assets/logo.png's row (it is on {app.DescribeFocus()})");
        Assert.True(app.FocusVisible, "assets/logo.png's row shows no keyboard frame.");
    }

    /// <summary>
    /// "Discard changes" chosen from README.md's menu, opened by Shift+F10, and confirmed: once
    /// README.md has left the unstaged list, the row now at its place has the focus with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_chosen_from_a_rows_menu_by_the_keys_and_confirmed_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        await FocusReadmesRowAsync(app);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        app.Menu();
        app.PressKey(Key.Down);
        await app.WaitForAsync(() => app.Menu().Highlighted?.Label == "Discard changes…", $"Discard changes… highlighted (it is {app.Menu().Highlighted?.Label})");

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => question.IsClosed, "the question to close");
        await app.WaitForAsync(() => changes.UnstagedTitle == Phase8aExpectations.K.Unstaged6, "README.md's unstaged change to be discarded");
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.Equal("M  README.md", (await repo.GitAsync("status", "--porcelain", "--", Phase8aExpectations.K.Readme)).StandardOutput.TrimEnd('\r', '\n'));
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Logo, $"the focus on assets/logo.png's row (it is on {app.DescribeFocus()})");
        Assert.True(app.FocusVisible, "assets/logo.png's row shows no keyboard frame.");
    }

    /// <summary>
    /// F7 by the palette: Amend, focused by Tab, the palette opened and its "Commit 3 files" run by
    /// the keys while the commit is held at <c>git commit</c>: the palette's close gives Amend the
    /// focus without the frame (D104), the commit disables Amend, and once it has ended Amend has the
    /// focus back, still without the frame (6.3.5, 6.3.7).
    /// </summary>
    [AvaloniaFact]
    public async Task Amend_focused_when_a_commit_starts_from_the_palette_gets_the_focus_back()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, held.Probe);
        await ShowStagePanelAsync(app);
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");
        app.Type(Phase8aExpectations.K.First);
        var amend = app.Find<CheckBox>("AmendCheckBox");
        await app.TabToAsync(focused => focused is TextBox { Name: "CommitDescription" }, "the description");
        await app.TabToAsync(focused => ReferenceEquals(focused, amend), "Amend");
        var changes = Repository(app).Changes;

        held.HoldNext(Phase8aExpectations.K.CommitWord);
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen, "the palette");
        app.TypeInPalette("Commit");
        await app.RunPaletteEntryAsync(changes.CommitButtonText);
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        app.Settle();
        Assert.False(app.IsPaletteOpen, "The palette is still open.");
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled while the commit runs.");

        held.Release();
        await app.WaitForAsync(() => !changes.IsCommitting && !changes.CommitCommand.IsRunning, "the commit to end");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => amend.IsFocused, $"Amend to have the focus back (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "Amend shows the keyboard's frame after a commit run from the palette.");
        Assert.Equal(Phase8aExpectations.K.First, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    /// <summary>Whether <paramref name="focused"/> is the control whose automation id is <paramref name="id"/>.</summary>
    private static bool IsButton(IInputElement? focused, string id) => focused is Control control && AutomationProperties.GetAutomationId(control) == id;

    /// <summary>Shift+Tab, at most six times, each followed by a wait, until the control <paramref name="id"/> has the focus, with the keyboard's frame.</summary>
    private static async Task ShiftTabToAsync(ShellDriver app, string id)
    {
        for (var press = 0; app.FocusedAutomationId() != id; press++)
        {
            Assert.True(press < 6, $"Six Shift+Tabs did not reach {id}: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab, RawInputModifiers.Shift);
            app.Settle();
        }

        Assert.True(app.FocusVisible, $"{id} shows no keyboard frame.");
    }

    /// <summary>
    /// Enter on the focused button, which shows the keyboard's frame, opens a question; with
    /// <paramref name="cancel"/> "Esc" Esc cancels it, with "Cancel" Enter on its focused Cancel;
    /// then the app is idle.
    /// </summary>
    private static async Task CancelQuestionByTheKeysAsync(ShellDriver app, string cancel)
    {
        Assert.True(app.FocusVisible, $"{app.DescribeFocus()} shows no keyboard frame before Enter.");
        app.PressKey(Key.Enter);
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        await app.WaitForAsync(() => app.FocusedElement is Button { Name: "CancelButton" }, $"the focus on the question's Cancel (it is on {app.DescribeFocus()})");
        app.PressKey(cancel == "Esc" ? Key.Escape : Key.Enter);
        await app.WaitForAsync(() => question.IsClosed, $"the question to close by {cancel}");
        await app.WaitForIdleAsync();
        app.Settle();
    }
}
