using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The helpers phase 3's visual checks share (WP10-core: <c>ShellDriver.Pointer.cs</c>,
/// <c>.Log.cs</c>, <c>.Checks.cs</c>, <see cref="WindowLayout"/>, <see cref="AppLogFile"/>),
/// tested on the real app and the scenario repositories, so that a check that fails points at the
/// app and not at the harness. The real-window pass's twins of these helpers
/// (<c>RealApp.Checks.cs</c>) can only be compiled here: they use the same numbers and patterns.
/// </summary>
public class ShellDriverCheckSupportTests
{
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void The_layout_numbers_are_the_reports()
    {
        Assert.Equal(116, WindowLayout.RowsTop);
        Assert.Equal(454, WindowLayout.MessageColumnX(lanes: 3));
        Assert.Equal(48, WindowLayout.GraphColumnWidth(1));
        Assert.Equal(96, WindowLayout.GraphColumnWidth(5));
        Assert.Equal(233, WindowLayout.RowCentreY(4));
        Assert.Equal(116 + 13 + 26, WindowLayout.RowCentreY(1));
        Assert.Equal(WindowLayout.RowsTop, WindowLayout.RowsTopBelow(null));
        Assert.Equal(160 + 28, WindowLayout.RowsTopBelow(160));

        // 4a's formula, with the heights the report gives: a commit's menu, the empty-area menu,
        // a branch's menu, the drop menu.
        Assert.Equal(232, WindowLayout.MenuHeight(items: 7, separators: 2));
        Assert.Equal(206, WindowLayout.MenuHeight(items: 6, separators: 2));
        Assert.Equal(406, WindowLayout.MenuHeight(items: 13, separators: 4));
        Assert.Equal(110, WindowLayout.MenuHeight(items: 3, separators: 0));
    }

    [Fact]
    public void The_log_patterns_match_the_lines_the_app_writes_and_nothing_else()
    {
        using var data = new TempDirectory("data");
        var log = new FileAppLog(new VisualCommit.Core.AppPaths(data.Path).LogDirectory);
        log.Info("ops: Fetch origin: Succeeded after 412 ms: Fetched from origin");
        log.Warning("sync: Push main: Failed after 95 ms: ! [rejected]        main -> main (fetch first)");
        log.Debug("ops: Context menu opened: ce5ea6a Alpha two by pointer");
        log.Debug("ops: working tree: 1 staged, 2 unstaged");

        var lines = AppLogFile.ReadLines(data.Path);

        Assert.Equal(4, lines.Count);
        Assert.Single(lines, line => AppLogFile.OperationEnded("ops", "Fetch origin", OperationOutcome.Succeeded, "Fetched from origin").IsMatch(line));
        Assert.Single(lines, line => AppLogFile.OperationEnded("ops", "Fetch origin").IsMatch(line));
        Assert.DoesNotContain(lines, line => AppLogFile.OperationEnded("ops", "Fetch").IsMatch(line));
        Assert.DoesNotContain(lines, line => AppLogFile.OperationEnded("ops", "Fetch origin", OperationOutcome.Cancelled).IsMatch(line));
        Assert.Single(lines, line => AppLogFile.OperationEnded("sync", "Push main", OperationOutcome.Failed).IsMatch(line));
        Assert.Single(lines, line => AppLogFile.MenuOpened("ops", "ce5ea6a Alpha two", "pointer").IsMatch(line));
        Assert.DoesNotContain(lines, line => AppLogFile.MenuOpened("ops", by: "keyboard").IsMatch(line));
        var status = Assert.Single(lines, line => AppLogFile.WorkingTreeRead("ops").IsMatch(line));
        Assert.Equal("2", AppLogFile.WorkingTreeRead("ops").Match(status).Groups["unstaged"].Value);
        Assert.Equal(string.Join('\n', lines) + "\n", AppLogFile.ReadText(data.Path).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Rows_without_a_banner_are_where_the_layout_numbers_put_them()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        Assert.Equal(8, repository.Graph.Count);
        Assert.Null(app.BannerBounds);
        Assert.Equal(WindowLayout.RowsTop, app.GraphRowsTop, 0.5);

        // The point the real-window pass computes for "Alpha two" (row 4, three lanes) is the one
        // the scripted run clicks.
        var alphaTwo = app.GraphRowPoint(4);
        Assert.Equal(new Point(WindowLayout.MessageColumnX(3) + WindowLayout.MessageClickInset, WindowLayout.RowCentreY(4)), alphaTwo);
        app.Click(alphaTwo);
        Assert.Equal(4, repository.SelectedIndex);
        Assert.Throws<InvalidOperationException>(() => app.WorkingRowPoint());
    }

    [AvaloniaFact]
    public async Task An_unborn_repository_counts_as_open_once_its_empty_history_and_its_status_are_shown()
    {
        // Check 23 step 5: HEAD unborn on main, a bare remote, an untracked file.
        using var repo = await TempRepo.CreateAsync("unborn");
        await repo.AddBareRemoteAsync();
        repo.WriteFile("notes.txt", "notes\n");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);

        var repository = await app.WaitForRepositoryAsync();

        Assert.Equal(0, repository.Graph.Count);
        Assert.True(repository.HasNoCommits);
        Assert.True(repository.HasWorkingChanges);
        Assert.Equal("main", ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);
    }

    [AvaloniaFact]
    public async Task Below_a_banner_the_rows_and_the_working_row_move_down_by_its_height()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => app.BannerBounds is not null, "the banner");

        // D122: the banner is at least 36 high, sits at the graph area's top, and the column
        // headers and rows follow it.
        var banner = app.BannerBounds!.Value;
        Assert.Equal(WindowLayout.GraphAreaTop, banner.Top, 0.5);
        Assert.True(banner.Height >= WindowLayout.BannerMinHeight, $"The banner is {banner.Height} high.");
        var rowsTop = WindowLayout.RowsTopBelow(banner.Bottom);
        Assert.Equal(rowsTop, app.GraphRowsTop, 0.5);

        // Display row 0 is the working-changes row; commit row N is display row N + 1.
        var working = app.WorkingRowPoint();
        Assert.Equal(WindowLayout.RowCentreY(0, rowsTop), working.Y, 0.5);
        Assert.Equal(WindowLayout.RowCentreY(4, rowsTop), app.GraphRowPoint(3).Y, 0.5);
        app.Click(working);
        Assert.True(repository.IsWorkingRowSelected);
        app.Click(app.GraphRowPoint(3));
        Assert.Equal(3, repository.SelectedIndex);

        // The banner's text is not hit-testable: resting on it shows the banner's own tooltip.
        var text = app.FindByAutomationId("BannerText");
        var owner = app.ToolTipOwnerAt(app.BoundsOf(text).Center);
        Assert.NotNull(owner);
        var tip = await app.RestPointerOnAsync(text);
        Assert.Equal(ToolTip.GetTip(owner) as string, tip);
        Assert.False(string.IsNullOrEmpty(tip));
        Assert.True(ToolTip.GetIsOpen(owner));
    }

    [AvaloniaFact]
    public async Task Resting_the_pointer_waits_for_the_tooltip_of_a_disabled_button_and_the_neutral_point_has_none()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        var stash = app.FindByAutomationId("StashButton");
        Assert.False(stash.IsEffectivelyEnabled);

        Assert.Same(stash, app.ToolTipOwnerAt(app.BoundsOf(stash).Center));
        Assert.Equal("There are no changes to stash", await app.RestPointerOnAsync(stash));
        Assert.True(ToolTip.GetIsOpen(stash));

        // Moving to the neutral point closes it, and nothing there has a tooltip.
        app.MoveToNeutral();
        Assert.Null(app.ToolTipOwnerAt(ShellDriver.NeutralPoint));
        await app.WaitForAsync(() => !ToolTip.GetIsOpen(stash), "the tooltip to close");
        Assert.Equal(new Point(WindowLayout.NeutralX, WindowLayout.NeutralY), ShellDriver.NeutralPoint);

        // Nothing at all there: a point in the empty status bar has no tooltip either.
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.RestPointerAtAsync(ShellDriver.NeutralPoint));
    }

    [AvaloniaFact]
    public async Task Resting_on_a_disabled_menu_item_shows_its_reason_and_leaves_the_highlight()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.RightClick(app.GraphRowPoint(6));
        Assert.Equal("Check out this commit", app.Menu().Highlighted?.Label);

        var item = app.MenuItemControl("Cherry-pick onto main");
        Assert.False(item.IsEffectivelyEnabled);
        Assert.Equal("This commit is already on main", await app.RestPointerOnMenuItemAsync("Cherry-pick onto main"));
        Assert.True(ToolTip.GetIsOpen(item));
        Assert.Equal("Check out this commit", app.Menu().Highlighted?.Label);

        // Not while a menu is open: the move could cross its items.
        Assert.Throws<InvalidOperationException>(app.MoveToNeutral);
        Assert.Throws<InvalidOperationException>(() => app.MenuItemControl("Rebase onto this commit"));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
    }

    [AvaloniaFact]
    public async Task The_log_lines_of_a_menu_and_an_operation_can_be_waited_for_and_counted_since_a_mark()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        Assert.True(app.LogLines().Count > 0);
        Assert.NotEmpty(app.LogLinesSince(0));
        var mark = app.LogMark();

        app.RightClick(app.GraphRowPoint(4));
        var opened = AppLogFile.MenuOpened("ops", "ce5ea6a Alpha two", "pointer");
        var line = await app.WaitForLogLineAsync(opened, mark: mark);
        Assert.Contains("[DBG] ops: Context menu opened: ce5ea6a Alpha two by pointer", line, StringComparison.Ordinal);
        Assert.Equal(1, app.CountLogLines(opened, mark));
        app.PressKey(Key.Escape);

        // An operation's end line: the toolbar's Fetch of the folder remote.
        mark = app.LogMark();
        app.Click(app.FindByAutomationId("FetchButton"));
        var fetched = await app.WaitForLogLineAsync(AppLogFile.OperationEnded("ops", "Fetch", OperationOutcome.Succeeded, "Fetched from all remotes"), mark: mark, timeout: NetworkTimeout);
        Assert.Contains("[INF] ops: Fetch: Succeeded after ", fetched, StringComparison.Ordinal);
        Assert.Contains(fetched, app.LogLinesSince(mark));
        await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForLogLineAsync(AppLogFile.OperationEnded("ops", "Fetch"), occurrence: 2, mark: mark, timeout: TimeSpan.FromMilliseconds(200)));

        // What the app wrote down can be searched for a text, which is how the checks prove a
        // secret was not.
        Assert.Contains(app.WhereWritten("Fetched from all remotes"), place => place.StartsWith("log: ", StringComparison.Ordinal));
        Assert.Contains(app.WhereWritten("git fetch --progress"), place => place.StartsWith("git call: ", StringComparison.Ordinal));
        Assert.Empty(app.WhereWritten("ghp_SECRET123"));
    }

    [AvaloniaFact]
    public async Task The_error_dialog_reads_as_it_shows_and_its_output_is_the_cleaned_output_of_the_failed_call()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // Check 9 step 7: a checkout git refuses during the merge.
        var checkout = repository.Operations.CheckoutAsync("refs/heads/feature/alpha");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        var dialog = app.ErrorDialog();

        Assert.Equal(("Checkout failed", "In ops"), (dialog.Title, dialog.Repository));
        Assert.Equal("git switch --no-guess feature/alpha", dialog.Command);
        Assert.Equal(["Copy output", "Close"], dialog.Buttons);
        var call = app.LastCall(dialog.Command!);
        Assert.False(call.Succeeded);
        Assert.Equal(ShellDriver.CleanedOutputOf(call), dialog.Output);
        Assert.Equal(dialog.Output!.Split('\n'), dialog.OutputLines);
        Assert.Contains(dialog.OutputLines, output => output.StartsWith("fatal: ", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => app.LastCall("git switch --no-guess no-such-branch"));

        // The neutral point is on the backdrop: no tooltip there either.
        Assert.Null(app.ToolTipOwnerAt(ShellDriver.NeutralPoint));

        app.Click(app.FindByAutomationId("ConfirmButton"));
        await checkout.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await app.WaitForIdleAsync();
        Assert.Throws<InvalidOperationException>(() => app.ErrorDialog());

        // The failure's status text has a tooltip (the whole message): not at the neutral point.
        Assert.NotNull(ToolTip.GetTip(app.FindByAutomationId("OperationStatus")));
        Assert.Null(app.ToolTipOwnerAt(ShellDriver.NeutralPoint));
    }

    [AvaloniaFact]
    public async Task ReplaceText_selects_what_the_box_holds_with_the_command_modifier_and_types_over_it()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        app.TypeInto("CloneUrlBox", "https://alice:ghp_SECRET123@127.0.0.1:1/x.git");

        app.ReplaceText("CloneUrlBox", "https://example.com/new.git");

        var url = (TextBox)app.FindByAutomationId("CloneUrlBox");
        Assert.Equal("https://example.com/new.git", url.Text);
        Assert.True(url.IsFocused);

        app.ReplaceText("CloneUrlBox", string.Empty);
        Assert.True(string.IsNullOrEmpty(url.Text));
        Assert.Throws<InvalidOperationException>(() => app.ReplaceText("CloneRepoButton", "x"));
    }

    [AvaloniaFact]
    public async Task RefRow_finds_a_row_by_its_path_under_its_own_section()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();

        // Two rows named "main" and two folders named "feature": each found under its own path.
        var localMain = Assert.IsType<RefItemRow>(app.RefRowModel("Local branches", "main"));
        var remoteMain = Assert.IsType<RefItemRow>(app.RefRowModel("Remotes", "origin", "main"));
        Assert.NotSame(localMain, remoteMain);
        Assert.True(localMain.IsHead);
        Assert.Equal(4, Assert.IsType<RefSectionRow>(app.RefRowModel("Local branches")).Count);

        // A row in a closed folder is not there until the folder is opened, as for a user.
        Assert.Throws<InvalidOperationException>(() => app.RefRowModel("Local branches", "feature", "alpha"));
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        var localAlpha = app.RefRowModel("Local branches", "feature", "alpha");
        var remoteAlpha = app.RefRowModel("Remotes", "origin", "feature", "alpha");
        Assert.NotSame(localAlpha, remoteAlpha);

        var row = await app.RefRowAsync("Remotes", "origin", "feature", "alpha");
        Assert.Same(remoteAlpha, row.DataContext);
        Assert.Equal("RefItem", Avalonia.Automation.AutomationProperties.GetAutomationId(row));
        Assert.Throws<InvalidOperationException>(() => app.RefRowModel("Tags", "v9.9"));
        Assert.Throws<InvalidOperationException>(() => app.RefRowModel("No such section"));
    }

    [AvaloniaFact]
    public async Task FileRowAsync_scrolls_a_file_list_to_the_files_row_in_the_stage_panel_and_the_details()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "the stage panel");

        // The unstaged list's last file is below the fold at 1100×700: it is scrolled to.
        var calculator = await app.FileRowAsync("UnstagedFileList", "src/Calculator.cs");
        Assert.Equal("src/Calculator.cs", Avalonia.Automation.AutomationProperties.GetName(calculator));
        Assert.True(app.BoundsOf(calculator).Bottom <= app.BoundsOf(app.FindByAutomationId("UnstagedFileList")).Bottom + 0.5);
        var settings = await app.FileRowAsync("StagedFileList", "config/settings.json");
        Assert.Equal("config/settings.json", ((ChangedFileRow)settings.DataContext!).Key);
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.FileRowAsync("StagedFileList", "no/such.file"));

        // The details' file list of a commit.
        app.Click(app.GraphRowPoint(0));
        await app.WaitForAsync(() => repository.Details.Files.Count > 0, "the commit's files");
        var file = await app.FileRowAsync("FileList", "src/Calculator.cs");
        Assert.Equal("ChangedFile", Avalonia.Automation.AutomationProperties.GetAutomationId(file));
    }

    [AvaloniaFact]
    public async Task The_progress_can_be_waited_for_and_recorded_and_the_neutral_point_stays_clear_of_the_widest_status_bar()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        using var texts = app.RecordStatusTexts();

        // Check 31's step 1: a held fetch with its percentage, and the toolbar's Fetch waiting.
        var held = server.HoldNextFetchPack();
        var fetch = repository.Operations.FetchAsync("origin");
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        var percent = await app.WaitForProgressPercentAsync(NetworkTimeout);
        Assert.InRange(percent, 1, 100);
        app.Click(app.FindByAutomationId("FetchButton"));
        await app.WaitForAsync(() => app.FindByAutomationId("OperationWaiting").IsEffectivelyVisible, "\"1 waiting\"");

        // The status bar at its widest: the neutral point is right of every part on its left and
        // left of the git version, and nothing there has a tooltip.
        foreach (var part in new[] { "OperationStatus", "OperationProgress", "OperationCancelButton", "OperationWaiting" })
        {
            Assert.True(app.BoundsOf(app.FindByAutomationId(part)).Right < WindowLayout.NeutralX, $"{part} reaches the neutral point.");
        }

        Assert.True(app.BoundsOf(app.FindByAutomationId("GitStatus")).Left > WindowLayout.NeutralX);
        Assert.Null(app.ToolTipOwnerAt(ShellDriver.NeutralPoint));

        // Cancel stops the held fetch; the waiting Fetch then runs (nothing holds it).
        app.Click(app.FindByAutomationId("OperationCancelButton"));
        await fetch.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        server.Release();
        await app.WaitForIdleAsync(timeout: NetworkTimeout);

        Assert.Equal("Fetching from origin…", texts.Texts[0]);
        Assert.Contains(texts.Texts, text => text.StartsWith("Fetching from origin… ", StringComparison.Ordinal) && text.EndsWith('%'));
        Assert.Contains("Fetching from all remotes…", texts.Texts);
        Assert.Equal("Fetched from all remotes", texts.Texts[^1]);
        texts.Dispose();
        Assert.Equal("Fetched from all remotes", app.Session.Shell.ActiveTab.OperationStatus.Text);
    }

    [AvaloniaFact]
    public async Task RequestClose_closes_an_idle_window_and_says_so()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        Assert.False(app.IsWindowClosed);

        Assert.True(app.RequestClose());
        Assert.True(app.IsWindowClosed);
    }

    [AvaloniaFact]
    public async Task MarkStep_notes_each_step_with_the_time_since_the_start_and_since_the_step_before()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        var first = app.MarkStep("01a-first");
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var second = app.MarkStep("01b-second");

        Assert.Equal([first, second], app.StepTimes);
        Assert.True(first.At > TimeSpan.Zero && first.Took == first.At);
        Assert.True(second.At >= first.At + TimeSpan.FromMilliseconds(15));
        Assert.Equal(second.At - first.At, second.Took);
        Assert.True(app.Elapsed >= second.At);
        Assert.Contains("01b-second", second.ToString(), StringComparison.Ordinal);
    }
}
