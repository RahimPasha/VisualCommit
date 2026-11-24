using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The keyboard pass's tests that start the app (docs/phase-8a-design.md 6.4; D416, D417, D425 to
/// D427, D452): the diff's toolbar and hunks by Tab, chosen lines by the arrows and Shift+F10, a
/// blame's menu just opened, the focus after "Choose" in the repository browser and the create
/// form's Pick, Enter on a dialog's selected row or checked option, a loading diff that no longer
/// takes a focus the user moved, Amend while a commit runs, Ctrl+Tab from the stage panel and the
/// graph, a row's Discard cancelled, the browser's switch of providers, the splitters by the
/// arrows, and Tab out of the read-only texts. No pictures: part K's checks take those.
/// </summary>
public sealed class KeyboardPassTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>Shift+Tab from the diff's text goes to the toolbar's last shown button, "Discard file" on an unstaged diff (6.3.1).</summary>
    [AvaloniaFact]
    public async Task Shift_Tab_from_the_diffs_text_reaches_the_toolbars_Discard_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        app.PressKey(Key.Tab, RawInputModifiers.Shift);

        Assert.True(app.FocusedAutomationId() == "DiscardFileButton", $"Shift+Tab from the text did not reach Discard file: the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Discard file shows no keyboard frame.");
    }

    /// <summary>Tab from the text and Enter on "Stage hunk" stage that hunk alone and leave the focus in the text, without a frame (6.3.1).</summary>
    [AvaloniaFact]
    public async Task Tab_and_Enter_on_Stage_hunk_stage_that_hunk_alone_and_leave_the_focus_in_the_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        app.PressKey(Key.Tab);
        Assert.True(app.FocusedAutomationId() == "StageHunkButton", $"Tab from the text did not reach Stage hunk: the focus is on {app.DescribeFocus()}.");
        var diff = Repository(app).Diff!;
        var before = diff.Diff;
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !ReferenceEquals(diff.Diff, before) && diff.ShowsText, "the diff read again");
        await app.WaitForIdleAsync();

        Assert.Equal(Phase8aExpectations.K.StagedAfterHunk, await NumstatAsync(repo, cached: true));
        Assert.Equal(Phase8aExpectations.K.UnstagedAfterHunk, await NumstatAsync(repo, cached: false));
        Assert.Equal(Phase8aExpectations.K.IndexAfterHunk, (await repo.GitAsync("show", ":src/Calculator.cs")).StandardOutput.ReplaceLineEndings("\n"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "The text shows the keyboard's frame.");
    }

    /// <summary>
    /// Tab to the first hunk's "Discard hunk" and Enter, the question confirmed: the question's close
    /// gives the button its focus back without the frame (D104), and when its bar leaves with the
    /// diff read again, the text takes the focus, without a frame, because the button asked for the
    /// action while it had the frame (6.3.1's "or raised the action while it had").
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_and_Enter_on_Discard_hunk_confirmed_leave_the_focus_in_the_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "StageHunkButton", "Stage hunk");
        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "DiscardHunkButton", "Discard hunk");
        Assert.True(app.FocusVisible, "Discard hunk shows no keyboard frame.");
        var diff = Repository(app).Diff!;
        var before = diff.Diff;
        app.PressKey(Key.Enter);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => !ReferenceEquals(diff.Diff, before) && diff.ShowsText, "the diff read again");
        await app.WaitForIdleAsync();

        // The first hunk, Add's change, is gone from the working tree; the four added lines stay.
        Assert.Equal(Phase8aExpectations.K.UnstagedAfterHunk, await NumstatAsync(repo, cached: false));
        Assert.Empty(await NumstatAsync(repo, cached: true));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "The text shows the keyboard's frame.");
    }

    /// <summary>
    /// G1: F6 from another region gives the diff's text the focus; Ctrl+Home, Down to row 4 of the
    /// second hunk (past the first hunk's header and lines; its own header row 0, then git's three
    /// context lines) and Shift+Down twice select that hunk's added empty line and <c>Negate</c>, two
    /// of its four added lines; Shift+F10's "Stage selected lines" stages those two alone (passes
    /// before the keyboard pass too).
    /// </summary>
    [AvaloniaFact]
    public async Task Lines_chosen_by_F6_the_arrows_and_Shift_F10_are_staged()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var diff = Repository(app).Diff!;
        Assert.Equal(2, diff.Diff!.Hunks.Count);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRegion() != KeyRegionRole.GraphArea, "the focus out of the graph area");
        for (var f6 = 0; app.FocusedAutomationId() != "DiffText"; f6++)
        {
            Assert.True(f6 < 4, $"Four F6 did not bring the focus back to the diff's text: it is on {app.DescribeFocus()}.");
            app.PressF6();
        }

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
        Assert.All(diff.SelectedChanges, line => Assert.Equal(1, line.Hunk));
        Assert.Equal(
            [string.Empty, "    public int Negate(int a) => -a;"],
            diff.SelectedChanges.OrderBy(line => line.Line).Select(line => diff.Diff!.Hunks[line.Hunk].Lines[line.Line].Text));
        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Assert.Equal(Phase8aExpectations.K.CalculatorName, menu.Header);
        Assert.Equal(Phase8aExpectations.K.StageSelectedLines, menu.Highlighted?.Label);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        await app.WaitForAsync(() => diff.SelectedChanges.Count == 0, "the diff read again");
        await app.WaitForIdleAsync();

        Assert.Equal(Phase8aExpectations.K.StagedAfterG1, await NumstatAsync(repo, cached: true));
        Assert.Equal(Phase8aExpectations.K.UnstagedAfterG1, await NumstatAsync(repo, cached: false));
        Assert.Equal(Phase8aExpectations.K.IndexAfterG1, (await repo.GitAsync("show", ":src/Calculator.cs")).StandardOutput.ReplaceLineEndings("\n"));
    }

    /// <summary>A blame just opened, nothing picked, answers Shift+F10 with run 0's menu, the caret's run (6.3.2).</summary>
    [AvaloniaFact]
    public async Task A_blame_just_opened_answers_Shift_F10_with_run_0s_menu()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenReaderBlameAsync(app);
        Assert.Null(((BlameViewModel)Repository(app).Page!).SelectedRun);

        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);

        var menu = app.Menu();
        Assert.Equal(Phase8aExpectations.K.Run0Sha + " " + Phase8aExpectations.K.AddTheParser, menu.Header);
        Assert.Equal(Phase8aExpectations.K.ShowInGraph, menu.Highlighted?.Label);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
    }

    /// <summary>
    /// A right-click in the gutter picks a run and opens its menu (phase 5's BD9) without touching the
    /// text, which keeps the keyboard focus: it also puts the caret, which is not drawn, on the run's
    /// first line, so that after Esc the arrows go on from that run (6.3.2). The right-click lands on
    /// the last line of the first run below run 1 that has more lines than one; Up then picks the run
    /// above it. Before, the caret stayed on line 1, where the blame opened it, and Up picked run 0.
    /// </summary>
    [AvaloniaFact]
    public async Task After_a_right_click_in_the_gutter_and_Esc_Up_picks_the_run_above_the_one_clicked()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenReaderBlameAsync(app);
        var blame = (BlameViewModel)Repository(app).Page!;
        var text = (VisualCommit.App.Controls.Blame.BlameTextEditor)app.FindByAutomationId("BlameText");
        Assert.Equal(1, text.TextArea.Caret.Line);
        var run = app.BlameSnapshot().Runs.First(run => run.Index >= 2 && run.LastLine > run.FirstLine);

        app.RightClick(app.BlameGutterPoint(run.LastLine));
        await app.WaitForAsync(() => app.IsMenuOpen, "the run's menu");

        Assert.Equal(run.Index, blame.SelectedRun?.Index);
        Assert.Equal(run.FirstLine, text.TextArea.Caret.Line);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen && app.FocusedAutomationId() == "BlameText", $"the menu to close and the focus to come back to the text (it is on {app.DescribeFocus()})");

        app.PressKey(Key.Up);

        Assert.Equal(run.FirstLine - 1, text.TextArea.Caret.Line);
        Assert.Equal(run.Index - 1, blame.SelectedRun?.Index);
    }

    /// <summary>
    /// A right-click on the blame's text inside a selection picks the run under it and opens its
    /// menu, and leaves the selection and the caret as they were, as editors do: the run's menu has
    /// no copy of its own, and after Esc the selection is still there to copy. A right-click on the
    /// text outside the selection takes the caret to the picked run's first line, the selection gone,
    /// as one in the gutter does (6.3.2). Before, a right-click inside the selection cleared it too.
    /// </summary>
    [AvaloniaFact]
    public async Task A_right_click_on_the_text_inside_a_selection_keeps_the_selection_and_one_outside_it_takes_the_caret_to_the_run()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenReaderBlameAsync(app);
        var blame = (BlameViewModel)Repository(app).Page!;
        var text = (VisualCommit.App.Controls.Blame.BlameTextEditor)app.FindByAutomationId("BlameText");
        var document = text.Document!;
        var runs = app.BlameSnapshot().Runs;
        var selected = (Start: 0, End: document.GetLineByNumber(2).EndOffset);
        text.Select(selected.Start, selected.End - selected.Start);
        var caret = text.TextArea.Caret.Offset;

        app.RightClick(app.BlameTextPoint(1, 2));
        await app.WaitForAsync(() => app.IsMenuOpen, "the run's menu");

        Assert.Equal(runs.Single(run => run.FirstLine <= 1 && run.LastLine >= 1).Index, blame.SelectedRun?.Index);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen && app.FocusedAutomationId() == "BlameText", $"the menu to close and the focus to come back to the text (it is on {app.DescribeFocus()})");

        Assert.Equal(selected, (text.SelectionStart, text.SelectionStart + text.SelectionLength));
        Assert.Equal(caret, text.TextArea.Caret.Offset);

        var outside = runs.First(run => run.FirstLine > 2 && document.GetText(document.GetLineByNumber(run.FirstLine)).Length >= 2);
        app.RightClick(app.BlameTextPoint(outside.FirstLine, 2));
        await app.WaitForAsync(() => app.IsMenuOpen, "the run's menu");

        Assert.Equal(outside.Index, blame.SelectedRun?.Index);
        Assert.True(text.TextArea.Selection.IsEmpty);
        Assert.Equal(outside.FirstLine, text.TextArea.Caret.Line);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
    }

    /// <summary>
    /// F5-A: after Choose and after Esc in the repository browser, "From GitHub…" has the focus
    /// back, without the keyboard's frame (6.3.5); the browser disabled it while it ran. The row is
    /// chosen by Enter in the list, as 6.3.9's route and K5 have it.
    /// </summary>
    [AvaloniaFact]
    public async Task After_Choose_and_after_Esc_in_the_repository_browser_its_button_has_the_focus()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
        await app.SignInAsync(served.OctoToken, Cancelled);
        var button = app.Find<Button>("CloneFromGitHubButton");
        await app.WaitForAsync(() => button.IsEffectivelyVisible, "From GitHub… to show");

        // Choose.
        await app.TabToAsync(focused => ReferenceEquals(focused, button), "From GitHub…", shift: true);
        app.PressKey(Key.Enter);
        var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        await app.TabToAsync(focused => focused is Control control && control.GetSelfAndVisualAncestors().OfType<Control>().Any(ancestor => AutomationProperties.GetAutomationId(ancestor) == "CloneFromGitHubList"), "the browser's list");
        app.PressKey(Key.Home);
        await app.WaitForAsync(() => browser.Selected?.FullName == Phase8aExpectations.K.OctoHello, "octo/hello selected");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
        await app.WaitForAsync(() => button.IsFocused, $"From GitHub… to have the focus after Choose (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "From GitHub… shows the keyboard's frame after Choose.");
        Assert.Equal(Phase8aExpectations.K.HelloUrl(served.GitHub.Port), app.Session.Shell.ActiveTab.Welcome.CloneUrl);

        // Esc.
        app.PressKey(Key.Enter);
        browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
        await app.WaitForAsync(() => button.IsFocused, $"From GitHub… to have the focus after Esc (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "From GitHub… shows the keyboard's frame after Esc.");
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith("git clone", StringComparison.Ordinal));
    }

    /// <summary>
    /// Enter in the repository browser's list chooses only a selected row, when Choose can run: with
    /// the focus in the list and no row selected, the browser stays open and the form's URL empty.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_in_the_repository_browsers_list_with_no_row_selected_chooses_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.SignInAsync(served.OctoToken, Cancelled);
        var button = app.Find<Button>("CloneFromGitHubButton");
        await app.WaitForAsync(() => button.IsEffectivelyVisible, "From GitHub… to show");
        app.Click(button);
        var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        await app.TabToAsync(focused => focused is Control control && control.GetSelfAndVisualAncestors().OfType<Control>().Any(ancestor => AutomationProperties.GetAutomationId(ancestor) == "CloneFromGitHubList"), "the browser's list");
        Assert.Null(browser.Selected);

        app.PressKey(Key.Enter);
        app.Settle();

        Assert.False(browser.IsClosed, "Enter in the list closed the browser while no row was selected.");
        Assert.True(string.IsNullOrEmpty(app.Session.Shell.ActiveTab.Welcome.CloneUrl), $"The form's URL is {app.Session.Shell.ActiveTab.Welcome.CloneUrl}.");
        browser.CancelCommand.Execute(null);
        await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
    }

    /// <summary>
    /// Enter on a selected row of a pick dialog's list confirms it, as a click on its confirm button
    /// does: in "Set upstream for topic", Tab from the filter into the list, Enter with no row
    /// selected confirms nothing (the row's item has the key), End selects origin/published, and
    /// Enter sets it as topic's upstream.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_selected_row_of_a_pick_list_confirms_it_as_its_button_does()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(await app.RefRowAsync("Local branches", "topic"));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "topic's menu");
        app.ClickMenuItem("Set upstream…");
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedAutomationId() == "PickFilter", $"the focus in the filter (it is on {app.DescribeFocus()})");
        await app.TabToAsync(focused => focused is Control control && control.GetSelfAndVisualAncestors().OfType<Control>().Any(ancestor => AutomationProperties.GetAutomationId(ancestor) == "PickList"), "the pick list");
        Assert.Null(pick.Selected);

        app.PressKey(Key.Enter);
        app.Settle();
        Assert.False(pick.IsClosed, "Enter in the list closed the pick dialog while no row was selected.");

        app.PressKey(Key.End);
        await app.WaitForAsync(() => pick.Selected == "origin/published", "origin/published selected");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => pick.IsClosed, "the pick dialog to close");
        await app.WaitForIdleAsync();

        Assert.Equal("refs/heads/published", (await repo.GitAsync("config", "--get", "branch.topic.merge")).StandardOutput.Trim());
    }

    /// <summary>
    /// Enter on a choice dialog's checked option confirms it, as a click on its button does; on an
    /// option not checked, Enter is the radio button's own and checks it. "Push v1.0…" with two
    /// remotes opens on mirror, checked and focused; Tab to origin and Enter check origin, Shift+Tab
    /// to mirror and Enter check mirror again, and Enter pushes the tag to mirror.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_choice_dialogs_checked_option_confirms_it_as_its_button_does()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(await app.RefRowAsync("Tags", "v1.0"));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "v1.0's menu");
        app.ClickMenuItem("Push v1.0…");
        var choice = await app.WaitForDialogAsync<ChoiceDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedElement is RadioButton { IsChecked: true } radio && AutomationProperties.GetName(radio) == "mirror", $"the focus on mirror, checked (it is on {app.DescribeFocus()})");

        await app.TabToAsync(focused => focused is RadioButton radio && AutomationProperties.GetName(radio) == "origin", "origin");
        app.PressKey(Key.Enter);
        app.Settle();
        Assert.False(choice.IsClosed, "Enter on origin, not checked, closed the choice.");
        Assert.Equal("origin", choice.Selected);
        await app.TabToAsync(focused => focused is RadioButton radio && AutomationProperties.GetName(radio) == "mirror", "mirror", shift: true);
        app.PressKey(Key.Enter);
        app.Settle();
        Assert.False(choice.IsClosed, "Enter on mirror, not checked, closed the choice.");
        Assert.Equal("mirror", choice.Selected);

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => choice.IsClosed, "the choice to close");
        await app.WaitForIdleAsync();

        Assert.Equal(await repo.RefTargetAsync("refs/tags/v1.0"), (await repo.GitAsync("ls-remote", "mirror", "refs/tags/v1.0")).StandardOutput.Split('\t')[0]);
    }

    /// <summary>
    /// Enter on the push dialog's checked remote pushes, as a click on Push does: "Push topic…" with
    /// two remotes, Shift+Tab from the branch box to origin, not checked, Enter checks it, and Enter
    /// again pushes topic to origin.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_the_push_dialogs_checked_remote_pushes_as_its_button_does()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(await app.RefRowAsync("Local branches", "topic"));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "topic's menu");
        app.ClickMenuItem("Push topic…");
        var push = await app.WaitForDialogAsync<PushDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedAutomationId() == "PushBranchBox", $"the focus in the branch box (it is on {app.DescribeFocus()})");

        await app.TabToAsync(focused => focused is RadioButton radio && AutomationProperties.GetName(radio) == "origin", "origin", shift: true);
        app.PressKey(Key.Enter);
        app.Settle();
        Assert.False(push.IsClosed, "Enter on origin, not checked, closed the push dialog.");
        Assert.True(((RadioButton)app.FocusedElement!).IsChecked, "Enter on origin did not check it.");

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => push.IsClosed, "the push dialog to close");
        await app.WaitForIdleAsync();

        Assert.Equal(await repo.RefTargetAsync("refs/heads/topic"), (await repo.GitAsync("ls-remote", "origin", "refs/heads/topic")).StandardOutput.Split('\t')[0]);
    }

    /// <summary>After the create form's Pick of a base, closed by Esc or by its choice, the form's "Choose…" has the focus (ruling 51).</summary>
    [AvaloniaFact]
    public async Task After_the_create_forms_Pick_its_Choose_has_the_focus()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        await app.WaitForRepositoryAsync();
        await app.SignInAsync(served.OctoToken, Cancelled);
        await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed("hello"));
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();

        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "main's menu");
        app.ClickMenuItem("Create pull request…");
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await app.WaitForAsync(() => form.BaseText == "origin/main", "the form's base");
        var choose = (Button)app.FindByAutomationId("PullRequestBaseChooseButton");

        app.Click(choose);
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => pick.IsClosed, "the Pick to close");
        await app.WaitForAsync(() => choose.IsFocused, $"Choose… to have the focus after Esc (it is on {app.DescribeFocus()})");

        app.Click(choose);
        pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        await app.PickAsync("origin/topic/no-pr");
        await app.WaitForAsync(() => form.BaseText == "origin/topic/no-pr", "the chosen base");
        await app.WaitForAsync(() => choose.IsFocused, $"Choose… to have the focus after the choice (it is on {app.DescribeFocus()})");
        Assert.False(form.IsClosed);
        form.CancelCommand.Execute(null);
    }

    /// <summary>
    /// F6: an unstaged diff's read held while the user presses Ctrl+Shift+M leaves the focus in the
    /// summary once the read ends (6.3.6). Before it, a read held over a menu opened and closed on
    /// the row, the focus back on the row by the menu's own return, still lets the text take the
    /// focus (6.9 item 6).
    /// </summary>
    [AvaloniaFact]
    public async Task A_diff_read_held_while_the_user_moves_to_the_summary_leaves_the_focus_there()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, held.Probe);
        await ShowStagePanelAsync(app);

        // The menu's return keeps the request: the text takes the focus once the read ends.
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Readme, "the focus on README.md's row");
        held.HoldNext(Phase8aExpectations.K.DiffFilesWord);
        app.PressKey(Key.Enter);
        var call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.Equal(Phase8aExpectations.K.DiffFilesWord, call.Arguments[0]);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        app.Menu();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen && app.FocusedRowPath() == Phase8aExpectations.K.Readme, "the menu closed and the focus back on the row");
        held.Release();
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "README.md's diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");

        // Ctrl+Shift+M while the read is held: the summary keeps the focus.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");
        for (var f6 = 0; app.FocusedRowPath() != Phase8aExpectations.K.Readme; f6++)
        {
            Assert.True(f6 < 4, $"Four F6 did not bring the focus back to README.md's row: it is on {app.DescribeFocus()}.");
            app.PressF6();
        }

        held.HoldNext(Phase8aExpectations.K.DiffFilesWord);
        app.PressKey(Key.Enter);
        call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.StartsWith(Phase8aExpectations.K.HeldDiffStart, string.Join(' ', call.Arguments), StringComparison.Ordinal);
        Assert.EndsWith(Phase8aExpectations.K.HeldDiffEnd, string.Join(' ', call.Arguments), StringComparison.Ordinal);
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");
        held.Release();
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "README.md's diff");
        app.Capture();
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.True(app.FocusedAutomationId() == "CommitSummary", $"The read took the focus from the summary to {app.DescribeFocus()}.");
    }

    /// <summary>A diff whose body is empty (a pure rename) and turns to text while the summary has the focus does not take it (6.3.6).</summary>
    [AvaloniaFact]
    public async Task An_empty_diff_that_turns_to_text_while_the_summary_has_the_focus_leaves_it_there()
    {
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("a.txt", "one\n");
        await repo.CommitAsync("Add a");
        await repo.GitAsync("mv", "a.txt", "b.txt");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        app.Click(await app.FileRowAsync("StagedFileList", "b.txt"));
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsEmpty: true }, "b.txt's diff without content changes");
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");

        repo.WriteFile("b.txt", "one\ntwo\n");
        await repo.GitAsync("add", "b.txt");
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "b.txt's diff with its text");
        app.Capture();
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.True(app.FocusedAutomationId() == "CommitSummary", $"The diff's text took the focus from the summary to {app.DescribeFocus()}.");
    }

    /// <summary>A diff opened by Enter on a stage row still takes the focus (6.3.6; passes before the keyboard pass too).</summary>
    [AvaloniaFact]
    public async Task A_diff_opened_by_Enter_on_a_row_takes_the_focus()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Readme, "the focus on README.md's row");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "README.md's diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "The text shows the keyboard's frame.");
    }

    /// <summary>
    /// F7: Amend, focused by Tab, is disabled while the commit runs (held at <c>git commit</c>) and
    /// gets the focus back, with the keyboard's frame, once it has ended (6.3.5, 6.3.7).
    /// </summary>
    [AvaloniaFact]
    public async Task Amend_focused_by_Tab_is_disabled_while_the_commit_runs_and_gets_the_focus_back()
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
        Assert.True(app.FocusVisible, "Amend shows no keyboard frame.");

        held.HoldNext(Phase8aExpectations.K.CommitWord);
        var changes = Repository(app).Changes;
        changes.CommitCommand.Execute(null);
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        app.Settle();
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled while the commit runs.");
        Assert.Equal(Phase8aExpectations.K.Committing, changes.CommitButtonText);

        held.Release();
        await app.WaitForAsync(() => !changes.IsCommitting, "the commit to end");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => amend.IsFocused, $"Amend to have the focus back (it is on {app.DescribeFocus()})");
        Assert.True(amend.IsEffectivelyEnabled);
        Assert.True(app.FocusVisible, "Amend shows no keyboard frame after the commit.");
        Assert.Equal(Phase8aExpectations.K.First, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    /// <summary>
    /// Amend, focused by Tab when a held commit starts, gets the focus back only if the commit took
    /// it away: the user's Ctrl+Shift+M to the summary while the commit runs leaves the focus in
    /// the summary once the commit has ended (6.3.5, 6.3.7).
    /// </summary>
    [AvaloniaFact]
    public async Task Amend_focused_by_Tab_leaves_the_focus_the_user_moved_while_the_commit_ran()
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
        Assert.True(app.FocusVisible, "Amend shows no keyboard frame.");

        held.HoldNext(Phase8aExpectations.K.CommitWord);
        var changes = Repository(app).Changes;
        changes.CommitCommand.Execute(null);
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary while the commit runs");

        held.Release();
        await app.WaitForAsync(() => !changes.IsCommitting && !changes.CommitCommand.IsRunning, "the commit to end");
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.Equal(Phase8aExpectations.K.First, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.True(app.FocusedAutomationId() == "CommitSummary", $"The commit's return took the focus from the summary to {app.DescribeFocus()}.");
    }

    /// <summary>
    /// A keyboard Stage of README.md, held at <c>git add</c>, while the user moves the focus to the
    /// summary by Ctrl+Shift+M: once the stage has ended and the lists show it, the summary keeps the
    /// focus. The return after a row's action places a focus the action took away, never one the
    /// user moved meanwhile (6.3.4, 6.3.5).
    /// </summary>
    [AvaloniaFact]
    public async Task A_keyboard_Stage_held_while_the_user_moves_to_the_summary_leaves_the_focus_there()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, held.Probe);
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        await PressReadmesStageHeldAsync(app, held);

        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary while the stage runs");
        held.Release();
        await WaitForReadmeStagedAsync(app, repo, repository);

        Assert.True(app.FocusedAutomationId() == "CommitSummary", $"The stage's return took the focus from the summary to {app.DescribeFocus()}.");
    }

    /// <summary>
    /// A keyboard Stage of README.md, held at <c>git add</c>, while the user goes to another tab whose
    /// stage panel shows: once the stage has ended, the focus is where the switch of tabs left it, and
    /// no row of the other tab's lists has taken it (6.3.4, 6.3.5).
    /// </summary>
    [AvaloniaFact]
    public async Task A_keyboard_Stage_held_while_the_user_goes_to_another_tab_leaves_that_tabs_lists_alone()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = ShellDriver.StartWithTabs(data.Path, [first.Path, second.Path], activeTab: 1, gitProbe: held.Probe);
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();

        // The second tab's stage panel, then the first tab's, where README.md's Stage is pressed.
        await ShowStagePanelAsync(app);
        var other = Repository(app);
        app.PressCtrlTab();
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is { } shown && !ReferenceEquals(shown, other), "the first tab");
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await ShowStagePanelAsync(app);
        var mine = Repository(app);
        await PressReadmesStageHeldAsync(app, held);

        app.PressCtrlTab();
        await app.WaitForAsync(
            () => ReferenceEquals(app.Session.Shell.ActiveTab.Repository, other) && app.StageRows("UnstagedFileList").Count > 0,
            "the second tab's stage panel");
        app.Settle();
        var afterSwitch = app.FocusedElement;
        var afterSwitchText = app.DescribeFocus();
        held.Release();
        await WaitForReadmeStagedAsync(app, first, mine);

        Assert.True(
            ReferenceEquals(app.FocusedElement, afterSwitch),
            $"The stage's return moved the focus from {afterSwitchText}, where the switch of tabs left it, to {app.DescribeFocus()}.");
        Assert.False(
            app.FocusIsWithin(app.FindByAutomationId("UnstagedFileList")) || app.FocusIsWithin(app.FindByAutomationId("StagedFileList")),
            $"A row of the second tab's lists has the focus: {app.DescribeFocus()}.");
    }

    /// <summary>
    /// Ctrl+Tab from a stage row's own button, focused by F6, to another tab whose stage panel shows,
    /// nothing running: the panel is shared by the tabs, and its list drops the first tab's rows, the
    /// focused button with them, so the focus is on nothing; the first F6 then starts at the first
    /// region, the left panel's first section, with the keyboard's frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_focused_stage_row_leaves_the_focus_on_nothing_and_F6_then_starts_at_the_left_panel()
    {
        var facts = await CtrlTabFromAsync(FocusReadmesRowAsync);

        Assert.Equal(ReadmesRowFact, facts.Placed);
        Assert.False(facts.PlacedStillInWindow, "README.md's row's own button is still in the window after Ctrl+Tab.");
        Assert.Equal(NothingFact, facts.AfterSwitch);
        Assert.Equal(LeftSectionFact, facts.AfterF6);
    }

    /// <summary>
    /// Ctrl+Tab from a stage row's Stage, reached by Tab from the row, nothing running: as from the
    /// row's own button, the button leaves the window with the first tab's rows and the focus is on
    /// nothing, and the first F6 starts at the left panel's first section. A running Stage is not
    /// what leaves the focus on nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_stage_rows_Stage_reached_by_Tab_leaves_the_focus_on_nothing_and_F6_then_starts_at_the_left_panel()
    {
        var facts = await CtrlTabFromAsync(async app =>
        {
            await FocusReadmesRowAsync(app);
            await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RowStageButton", "README.md's Stage");
        });

        Assert.Equal(ReadmesStageFact, facts.Placed);
        Assert.False(facts.PlacedStillInWindow, "README.md's Stage is still in the window after Ctrl+Tab.");
        Assert.Equal(NothingFact, facts.AfterSwitch);
        Assert.Equal(LeftSectionFact, facts.AfterF6);
    }

    /// <summary>
    /// Ctrl+Tab from the graph, focused by the click on the working-changes row, nothing running: the
    /// graph's control is shared by the tabs and keeps the focus, now showing the second tab's graph;
    /// the first F6 then goes to the next region, the second tab's stage panel, on its first row's
    /// own button with the keyboard's frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_the_graph_keeps_the_focus_on_the_graph_and_F6_then_goes_to_the_other_tabs_first_stage_row()
    {
        var facts = await CtrlTabFromAsync(app => app.WaitForAsync(() => app.FocusedAutomationId() == "GraphRows", $"the focus on the graph (it is on {app.DescribeFocus()})"));

        Assert.Equal(GraphFact, facts.Placed);
        Assert.True(facts.SameAfterSwitch, "Another element than the graph has the focus after Ctrl+Tab.");
        Assert.Equal(GraphFact, facts.AfterSwitch);
        Assert.Equal(ReadmesRowFact, facts.AfterF6);
    }

    /// <summary>
    /// Ctrl+Tab from the commit summary, focused by Ctrl+Shift+M, nothing running: the summary's box
    /// is the shared panel's and keeps the focus, now holding the second tab's summary; the first F6
    /// then goes past the right panel, the last region shown, round to the left panel's first
    /// section, with the keyboard's frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_the_commit_summary_keeps_the_focus_in_the_summary_and_F6_then_goes_round_to_the_left_panel()
    {
        var facts = await CtrlTabFromAsync(async app =>
        {
            app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");
        });

        Assert.Equal(SummaryFact, facts.Placed);
        Assert.True(facts.SameAfterSwitch, "Another element than the summary has the focus after Ctrl+Tab.");
        Assert.Equal(SummaryFact, facts.AfterSwitch);
        Assert.Equal(LeftSectionFact, facts.AfterF6);
    }

    /// <summary>
    /// A stage row's Discard, reached by Tab and pressed by Enter, cancelled in its question by Esc
    /// and then by Enter on its focused Cancel: each time the focus is back on the row's own button,
    /// with the keyboard's frame, its Discard undrawn, and the working tree as it was (6.3.4's "gives
    /// the focus back to its row").
    /// </summary>
    [AvaloniaFact]
    public async Task A_rows_Discard_by_the_keys_cancelled_in_its_question_gives_the_focus_to_the_rows_own_button_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var exact = await repo.ExactStateAsync();
        await FocusReadmesRowAsync(app);

        foreach (var cancel in new[] { "Esc", "Cancel" })
        {
            await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RowStageButton", "README.md's Stage");
            await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RowDiscardButton", "README.md's Discard");
            var discard = (Button)app.FocusedElement!;
            Assert.True(app.FocusVisible, "README.md's Discard shows no keyboard frame.");
            app.PressKey(Key.Enter);
            var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            await app.WaitForAsync(() => app.FocusedElement is Button { Name: "CancelButton" }, $"the focus on the question's Cancel (it is on {app.DescribeFocus()})");
            app.PressKey(cancel == "Esc" ? Key.Escape : Key.Enter);
            await app.WaitForAsync(() => question.IsClosed, $"the question to close by {cancel}");
            await app.WaitForIdleAsync();
            app.Settle();

            Assert.True(FocusFact(app) == ReadmesRowFact, $"After {cancel} in the question the focus is on {FocusFact(app)}.");
            Assert.Equal(0.0, discard.Opacity);
        }

        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    // The focus as FocusFact names it.
    private const string NothingFact = "nothing";
    private const string ReadmesRowFact = "Button StageFileRow \"README.md\" of README.md in UnstagedFileList [Right], framed";
    private const string ReadmesStageFact = "Button RowStageButton \"Stage README.md\" of README.md in UnstagedFileList [Right], framed";
    private const string GraphFact = "CommitGraphControl GraphRows \"Commit graph\" [GraphArea]";
    private const string SummaryFact = "TextBox CommitSummary \"Summary\" [Right]";
    private const string LeftSectionFact = "Button Section \"Local branches\" [Left], framed";

    /// <summary>F6 from the graph to the stage panel's first row, README.md's own button.</summary>
    private static async Task FocusReadmesRowAsync(ShellDriver app)
    {
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Readme, $"the focus on README.md's row (it is on {app.DescribeFocus()})");
    }

    /// <summary>
    /// The focused element: "nothing", "the window", or its type, its own automation id, its name,
    /// the stage row and list it lies in, its region, and ", framed" when it shows the keyboard's
    /// frame.
    /// </summary>
    private static string FocusFact(ShellDriver app)
    {
        switch (app.FocusedElement)
        {
            case null:
                return NothingFact;
            case Window:
                return "the window";
            case Control control:
                var id = AutomationProperties.GetAutomationId(control);
                var fact = $"{control.GetType().Name} {(string.IsNullOrEmpty(id) ? "(no id)" : id)} \"{AutomationProperties.GetName(control)}\"";
                if (ShellDriver.StageRowOf(control)?.DataContext is ChangedFileRow { Key: var path })
                {
                    fact += $" of {path}";
                }

                if (control.GetVisualAncestors().OfType<ItemsControl>().FirstOrDefault(list => list.Name is "UnstagedFileList" or "StagedFileList") is { } list)
                {
                    fact += $" in {list.Name}";
                }

                return fact + $" [{app.FocusedRegion()}]" + (app.FocusVisible ? ", framed" : string.Empty);
            case var other:
                return other.GetType().Name;
        }
    }

    /// <summary>Where the focus was put, whether that element is still in the window and has the focus after Ctrl+Tab, and the focus after Ctrl+Tab and after the first F6.</summary>
    private sealed record TabSwitchFacts(string Placed, bool PlacedStillInWindow, bool SameAfterSwitch, string AfterSwitch, string AfterF6);

    /// <summary>
    /// Two tabs on "changes", each showing its stage panel, nothing running, the first active;
    /// <paramref name="place"/> puts the focus somewhere in it; then Ctrl+Tab to the second, and F6.
    /// </summary>
    private static async Task<TabSwitchFacts> CtrlTabFromAsync(Func<ShellDriver, Task> place)
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [first.Path, second.Path], activeTab: 1);
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();

        // The second tab's stage panel, then the first tab's, where the focus is put.
        await ShowStagePanelAsync(app);
        var other = Repository(app);
        app.PressCtrlTab();
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is { } shown && !ReferenceEquals(shown, other), "the first tab");
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        await ShowStagePanelAsync(app);
        await place(app);
        app.Settle();
        var placed = app.FocusedElement as Visual;
        var placedFact = FocusFact(app);

        app.PressCtrlTab();
        await app.WaitForAsync(
            () => ReferenceEquals(app.Session.Shell.ActiveTab.Repository, other) && app.StageRows("UnstagedFileList").Count > 0,
            "the second tab's stage panel");
        await app.WaitForIdleAsync();
        app.Settle();
        var stillInWindow = placed?.IsAttachedToVisualTree() == true;
        var same = placed is not null && ReferenceEquals(app.FocusedElement, placed);
        var afterSwitch = FocusFact(app);

        app.PressF6();
        app.Settle();
        return new TabSwitchFacts(placedFact, stillInWindow, same, afterSwitch, FocusFact(app));
    }

    /// <summary>G9: in the browser of two signed-in providers, Enter on the switch's other provider keeps the focus there, and Tab goes on to the filter.</summary>
    [AvaloniaFact]
    public async Task The_providers_switch_keeps_the_focus_and_Tab_goes_on_to_the_filter()
    {
        var copy = await Scenarios.Hosting7bAsync();
        ServedHosting7b served;
        try
        {
            served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        await using var serving = served;
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub, gitLab: served.GitLab);
        await app.WaitUntilReadyAsync();
        await app.SignInAsync(HostingProvider.GitHub, served.GitHubToken, null, Cancelled);
        await app.SignInAsync(HostingProvider.GitLab, served.GitLabToken, null, Cancelled);
        app.MoveToNeutral();
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        var button = app.Find<Button>("CloneFromGitHubButton");
        await app.WaitForAsync(() => button.IsEffectivelyVisible, "the browse button");
        app.Click(button);
        var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneFromGitHubFilter", "the filter focused");

        // Shift+Tab to the switch's last provider (GitLab, D353's order), then Enter on it.
        await app.TabToAsync(focused => focused is Button { DataContext: CloneProviderChoice { IsSelected: false } }, "the switch's other provider", shift: true);
        var other = (Button)app.FocusedElement!;
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => other.DataContext is CloneProviderChoice { IsSelected: true }, "the other provider shown");
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the other provider's list");
        Assert.True(other.IsFocused, $"The switch lost the focus to {app.DescribeFocus()}.");

        await app.TabToAsync(focused => focused is TextBox { Name: "CloneFromGitHubFilter" }, "the filter");
        browser.CancelCommand.Execute(null);
        await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
    }

    /// <summary>
    /// G8, D427: Tab reaches the left panel's splitter, Right widens the panel by 10, and the
    /// session keeps the width, fitted as a drag's end is.
    /// </summary>
    [AvaloniaFact]
    public async Task Right_on_the_left_splitter_reached_by_Tab_widens_the_panel_by_10_and_the_session_keeps_it()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var splitter = app.Find<GridSplitter>("LeftSplitter");
        var left = app.FindByAutomationId("LeftPanel");
        Assert.Equal(Phase8aExpectations.K.LeftBefore, left.Bounds.Width, 1.0);
        app.PressKey(Key.E, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "FilterBox", "the focus in the filter");
        for (var tab = 0; !splitter.IsFocused; tab++)
        {
            Assert.True(tab < Phase8aExpectations.K.WalkLimit, $"{Phase8aExpectations.K.WalkLimit} Tabs did not reach LeftSplitter: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab);
        }

        Assert.True(app.FocusVisible, "LeftSplitter shows no keyboard frame.");
        app.PressKey(Key.Right);
        await app.WaitForAsync(() => Math.Abs(left.Bounds.Width - Phase8aExpectations.K.LeftAfter) < 1, $"the left panel 270 wide (it is {left.Bounds.Width})");

        await app.WaitForAsync(() => app.Session.SessionState.Current.LeftPanelWidth is { } width && Math.Abs(width - Phase8aExpectations.K.LeftAfter) < 1, $"the session's left width 270 (it is {app.Session.SessionState.Current.LeftPanelWidth})");
        Assert.Equal(Phase8aExpectations.K.RightWidth, app.FindByAutomationId("RightPanel").Bounds.Width, 1.0);
    }

    /// <summary>
    /// G8, D427: the activity log's splitter by Tab and Up: the log 10 taller, and the height kept
    /// as a drag's end keeps it. The splitter's keyboard move raises no drag's end:
    /// <c>CommitGraphView</c> keeps it after a handled Up or Down, as <c>MainWindow</c> does for the
    /// side panels' splitters.
    /// </summary>
    [AvaloniaFact]
    public async Task Up_on_the_activity_logs_splitter_reached_by_Tab_heightens_the_log_by_10_and_keeps_it()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => app.Session.Shell.ActivityLog.IsShown, "the activity log");
        var splitter = app.Find<GridSplitter>("ActivityLogSplitter");
        var log = app.Session.Shell.ActivityLog;
        var before = log.PreferredHeight;
        var shown = app.FindByAutomationId("ActivityLog").Bounds.Height;
        app.Click(app.GraphRowPoint(0));
        for (var tab = 0; !splitter.IsFocused; tab++)
        {
            Assert.True(tab < Phase8aExpectations.K.WalkLimit, $"{Phase8aExpectations.K.WalkLimit} Tabs did not reach ActivityLogSplitter: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab);
        }

        app.PressKey(Key.Up);
        await app.WaitForAsync(() => Math.Abs(app.FindByAutomationId("ActivityLog").Bounds.Height - (shown + 10)) < 1, "the activity log 10 taller");
        await app.WaitForAsync(() => Math.Abs(log.PreferredHeight - (before + 10)) < 1, $"the log's kept height {before + 10} (it is {log.PreferredHeight})");
        Assert.Equal(log.PreferredHeight, app.Session.SessionState.Current.ActivityLogHeight);
    }

    /// <summary>
    /// Ruling 52: Tab leaves the read-only texts that are no editors, the activity log's output and
    /// the commit's body: after a click in each, Tab leaves the focus nowhere inside it.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_leaves_the_activity_logs_output_and_the_commits_body()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);

        // The commit's body: "Add settings page", HEAD's commit, the graph's third row, has one. A click in it, Tab.
        app.Click(app.GraphRowPoint(2));
        app.WaitForCommitDetails();
        var body = (Control)app.FindByAutomationId("DetailsBody");
        Assert.True(body.IsEffectivelyVisible, "The commit shows no body.");
        app.Click(body);
        app.PressKey(Key.Tab);
        Assert.False(app.FocusIsWithin(body), "Tab did not leave DetailsBody.");

        // The activity log's output: an operation with a call that read something, opened.
        var repository = Repository(app);
        app.ClickActivityLogToggle();
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        var nine = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);
        var sequence = RecordSequence.Next();
        app.Session.Operations.Start(new OperationStart("op-k", key, repository.Name, "Read the notes", sequence, nine));
        app.Session.Operations.Add(new OperationRecord("op-k", repository.Name, "Read the notes", nine, TimeSpan.Zero, OperationOutcome.Succeeded, "Read")
        {
            RepositoryKey = key,
            Sequence = sequence,
            DisplayedStart = nine,
        });
        app.Session.GitCalls.Add(new GitCallRecord(nine, TimeSpan.Zero, "git show HEAD:notes.txt", repo.Path, GitCallOutcome.Completed, 0, "Some notes\n", string.Empty, "op-k")
        {
            Sequence = RecordSequence.Next(),
            DisplayedStart = nine,
        });
        app.Settle();
        app.ClickActivityLogRow("Read the notes");
        app.ClickActivityLogRow("git show HEAD:notes.txt");
        var part = await WaitForPartTextAsync(app);
        app.Click(part);
        app.PressKey(Key.Tab);
        Assert.False(app.FocusIsWithin(part), "Tab did not leave ActivityLogPartText.");
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static RepositoryViewModel Repository(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    /// <summary>One tab on <paramref name="folder"/>, git looked for by <paramref name="probe"/> (the machine's search when null), the repository read and the watchers quiet, the pointer at rest.</summary>
    private static async Task<ShellDriver> OpenAsync(TempDirectory data, string folder, IGitSetupProbe? probe = null)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [folder], gitProbe: probe);
        try
        {
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The working-changes row clicked, its stage panel's lists shown, the app idle and the pointer at rest.</summary>
    private static async Task ShowStagePanelAsync(ShellDriver app)
    {
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.StageRows("UnstagedFileList").Count + app.StageRows("StagedFileList").Count > 0, "the stage panel's rows");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
    }

    /// <summary>src/Calculator.cs's unstaged diff opened by a click on its row, its text shown with the focus.</summary>
    private static async Task OpenCalculatorDiffAsync(ShellDriver app)
    {
        await ShowStagePanelAsync(app);
        app.Click(await app.FileRowAsync("UnstagedFileList", Phase8aExpectations.K.Calculator));
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "src/Calculator.cs's diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        app.MoveToNeutral();
    }

    /// <summary>src/Reader.cs's blame, opened from its unstaged diff by Ctrl+Shift+B, its text shown with the focus.</summary>
    private static async Task OpenReaderBlameAsync(ShellDriver app)
    {
        await ShowStagePanelAsync(app);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Reader, "the focus on src/Reader.cs's row");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true } && app.FocusedAutomationId() == "DiffText", "src/Reader.cs's diff with the focus");
        app.PressKey(Key.B, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(
            () => Repository(app).Page is BlameViewModel { ShowsText: true } && app.FocusedAutomationId() == "BlameText",
            $"the blame with the focus in its text (it is on {app.DescribeFocus()})");
    }

    /// <summary>
    /// F6 to README.md's row, Tab to its Stage (with the keyboard's frame), the next <c>git add</c>
    /// held, Enter: the held call has started.
    /// </summary>
    private static async Task PressReadmesStageHeldAsync(ShellDriver app, HeldGitRunner held)
    {
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == Phase8aExpectations.K.Readme, "the focus on README.md's row");
        await app.TabToAsync(
            focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RowStageButton"
                && ShellDriver.StageRowOf(control)?.DataContext is ChangedFileRow { Key: Phase8aExpectations.K.Readme },
            "README.md's Stage");
        Assert.True(app.FocusVisible, "README.md's Stage shows no keyboard frame.");
        held.HoldNext(Phase8aExpectations.K.AddWord);
        app.PressKey(Key.Enter);
        var call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.Equal((Phase8aExpectations.K.AddWord, Phase8aExpectations.K.Readme), (call.Arguments[0], call.Arguments[^1]));
    }

    /// <summary>
    /// The held stage of README.md released: waits until its run has ended and the lists show it
    /// (six unstaged rows), and for the queue to be idle; git's index has README.md staged.
    /// </summary>
    private static async Task WaitForReadmeStagedAsync(ShellDriver app, TempRepo repo, RepositoryViewModel repository)
    {
        var changes = repository.Changes;
        await app.WaitForAsync(
            () => !changes.StageFileCommand.IsRunning && changes.UnstagedTitle == Phase8aExpectations.K.Unstaged6,
            "README.md's stage to end and the lists to show it");
        await app.WaitForIdleAsync(repository);
        app.Settle();
        Assert.Equal("M  README.md", (await repo.GitAsync("status", "--porcelain", "--", Phase8aExpectations.K.Readme)).StandardOutput.TrimEnd('\r', '\n'));
    }

    /// <summary><c>git diff [--cached] --numstat -- src/Calculator.cs</c>, its two tabs written as single spaces.</summary>
    private static async Task<string> NumstatAsync(TempRepo repo, bool cached)
    {
        string[] arguments = cached
            ? ["diff", "--cached", "--numstat", "--", Phase8aExpectations.K.Calculator]
            : ["diff", "--numstat", "--", Phase8aExpectations.K.Calculator];
        return (await repo.GitAsync(arguments)).StandardOutput.Trim().Replace('\t', ' ');
    }

    /// <summary>The activity log's first shown output text (<c>ActivityLogPartText</c>).</summary>
    private static async Task<Control> WaitForPartTextAsync(ShellDriver app)
    {
        Control? part = null;
        await app.WaitForAsync(
            () => (part = app.Window.GetVisualDescendants().OfType<SelectableTextBlock>().FirstOrDefault(block => block.Name == "ActivityLogPartText" && block.IsEffectivelyVisible)) is not null,
            "the call's output");
        return part!;
    }

    /// <summary>The scenario "hosting" served by the fake of GitHub on a free port.</summary>
    private static async Task<ServedHosting> ServeAsync()
    {
        var copy = await Scenarios.HostingAsync();
        try
        {
            return await Scenarios.ServeHostingAsync(copy, port: 0, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }
}
