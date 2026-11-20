using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
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
/// form's Pick, a loading diff that no longer takes a focus the user moved, Amend while a commit
/// runs, the browser's switch of providers, the splitters by the arrows, and Tab out of the
/// read-only texts. No pictures: part K's checks take those.
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
    /// G1: F6 from another region gives the diff's text the focus; Ctrl+Home, Down four times and
    /// Shift+Down twice select rows 4 and 5 (the removed and the added line in Add); Shift+F10's
    /// "Stage selected lines" stages them (passes before the keyboard pass too).
    /// </summary>
    [AvaloniaFact]
    public async Task Lines_chosen_by_F6_the_arrows_and_Shift_F10_are_staged()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRegion() != KeyRegionRole.GraphArea, "the focus out of the graph area");
        for (var f6 = 0; app.FocusedAutomationId() != "DiffText"; f6++)
        {
            Assert.True(f6 < 4, $"Four F6 did not bring the focus back to the diff's text: it is on {app.DescribeFocus()}.");
            app.PressF6();
        }

        app.PressKey(Key.Home, RawInputModifiers.Control);
        for (var down = 0; down < Phase8aExpectations.K.DownsToLines; down++)
        {
            app.PressKey(Key.Down);
        }

        for (var down = 0; down < Phase8aExpectations.K.ShiftDownsForLines; down++)
        {
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        await app.WaitForAsync(() => Repository(app).Diff!.SelectedChanges.Count == 2, "two changed lines selected");
        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Assert.Equal(Phase8aExpectations.K.CalculatorName, menu.Header);
        Assert.Equal(Phase8aExpectations.K.StageSelectedLines, menu.Highlighted?.Label);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        await app.WaitForAsync(() => Repository(app).Diff!.SelectedChanges.Count == 0, "the diff read again");
        await app.WaitForIdleAsync();

        Assert.Equal(Phase8aExpectations.K.StagedAfterHunk, await NumstatAsync(repo, cached: true));
        Assert.Equal(Phase8aExpectations.K.UnstagedAfterHunk, await NumstatAsync(repo, cached: false));
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
    /// F5-A: after Choose and after Esc in the repository browser, "From GitHub…" has the focus
    /// back, without the keyboard's frame (6.3.5); the browser disabled it while it ran. Choose is
    /// reached by Tab from the list and pressed by Enter: Enter in the list itself is the list's
    /// item's, which selects it and chooses nothing.
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
        await app.TabToAsync(focused => focused is Control { Name: "CancelButton" }, "Cancel");
        await app.TabToAsync(focused => focused is Control { Name: "ConfirmButton" }, "Choose");
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
        var call = await held.Started.WaitAsync(Cancelled);
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
        call = await held.Started.WaitAsync(Cancelled);
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
        await held.Started.WaitAsync(Cancelled);
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
    /// as a drag's end keeps it. It fails on the code as it is (the height stays 200): the splitter's
    /// keyboard move raises no drag's end, and the line that keeps it is in
    /// <c>CommitGraphView.axaml.cs</c>, which another package owns; a fixer after wave 2's merges adds
    /// it with this test (docs/phase-8a-design.md 0.4) and removes the skip.
    /// </summary>
    [AvaloniaFact(Skip = "D427: the activity log's keyboard move is not kept yet; its line in CommitGraphView.axaml.cs goes to a fixer after wave 2's merges (docs/phase-8a-design.md 0.4).")]
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
