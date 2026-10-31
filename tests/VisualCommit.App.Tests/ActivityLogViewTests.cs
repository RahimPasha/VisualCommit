using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.App.Views.Conflicts;
using VisualCommit.App.Views.Rebase;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The activity log in the window (docs/phase-5-design.md 1.2, 2.1, 2.2; the report's part A):
/// the status bar's toggle, the panel under the graph area and its geometry at 1100×700, the
/// header, rows read as drawn, opened by click and by key, the list keeping to its end, the
/// splitter's limits and the session's height, Esc with the focus in the log and the resolver, the
/// interactive list or a page open, and where the focus goes then (AD6), a row's menu, and each
/// tab's log. The rows are records added to the session's own logs, as the queue and the runner add
/// theirs.
/// </summary>
public class ActivityLogViewTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public async Task The_toggle_opens_the_panel_under_the_graph_area_and_a_closed_panel_takes_no_room()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null], activeTab: 1);
        await app.WaitUntilReadyAsync();

        // A tab without a repository: the toggle disabled, with its tooltip, not selected (1.2).
        var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
        Assert.False(toggle.IsEffectivelyEnabled);
        Assert.Equal("Show every git command the app has run (Ctrl+J)", ToolTip.GetTip(toggle));
        Assert.Equal("Show every git command the app has run (Ctrl+J)", AutomationProperties.GetHelpText(toggle));
        Assert.DoesNotContain("selected", toggle.Classes);
        Assert.Equal(Brush(app, "VcTextDisabledBrush"), ColorOf(toggle.Foreground));

        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[0]);
        await app.WaitForRepositoryAsync();
        var graph = app.Window.GetVisualDescendants().OfType<CommitGraphControl>().First(control => control.IsEffectivelyVisible);
        Assert.Equal(674, app.BoundsOf(graph).Bottom, 1);
        Assert.True(toggle.IsEffectivelyEnabled);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), ColorOf(toggle.Foreground));
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);

        app.ClickActivityLogToggle();
        app.MoveToNeutral();
        var log = app.ActivityLog();

        // At 1100×700 (the report's part A): the panel 200 high above the status bar, the splitter's
        // 5 px above it, the content above that; the side panels at their full height.
        AssertBounds(new Rect(260, 474, 440, 200), log.Bounds);
        AssertBounds(new Rect(260, 474, 440, 30), log.HeaderBounds);
        AssertBounds(new Rect(260, 504, 440, 170), log.ListBounds);
        AssertBounds(new Rect(260, 469, 440, 5), app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")));
        Assert.Equal(469, app.BoundsOf(graph).Bottom, 1);
        Assert.Equal(674, app.BoundsOf(app.FindByAutomationId("LeftPanel")).Bottom, 1);
        Assert.Equal(674, app.BoundsOf(app.FindByAutomationId("RightPanel")).Bottom, 1);
        Assert.Equal(("Activity log", "history", false), (log.Title, log.RepositoryName, log.ShowReads));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), log.RepositoryNameColor);
        Assert.Empty(log.Rows);
        Assert.Contains("selected", toggle.Classes);
        Assert.Equal(Brush(app, "VcControlBackgroundBrush"), ColorOf(toggle.Background));
        Assert.Equal(Brush(app, "VcTextPrimaryBrush"), ColorOf(toggle.Foreground));
        Assert.Equal("Hide the activity log (Ctrl+J)", ToolTip.GetTip(toggle));
        Assert.Equal("Hide the activity log (Ctrl+J)", AutomationProperties.GetHelpText(toggle));
        var line = app.Find<Border>("ActivityLogSplitterLine");
        AssertBounds(new Rect(260, 473, 440, 1), app.BoundsOf(line));
        Assert.Equal(Brush(app, "VcBorderBrush"), ColorOf(line.Background));

        app.ClickActivityLogToggle();
        app.MoveToNeutral();
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ActivityLogSplitter").IsEffectivelyVisible);
        Assert.Equal(674, app.BoundsOf(graph).Bottom, 1);
        Assert.DoesNotContain("selected", toggle.Classes);
        Assert.Equal("Show every git command the app has run (Ctrl+J)", ToolTip.GetTip(toggle));
        Assert.Equal(200, app.ActivityLogModel.PreferredHeight);
    }

    [AvaloniaFact]
    public async Task Rows_show_their_parts_open_by_click_and_by_Right_close_by_Left_and_the_list_keeps_to_its_end()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        app.ClickActivityLogToggle();

        AddOperation(app, key, "op-a1", "Clean up backups", OperationOutcome.Succeeded, "Removed 3 backups", notes: ["Removed backup reset-20251120T090001000Z (older than 30 days)"]);
        AddCall(app, repo.Path, "op-a1", "git update-ref --stdin", input: "delete refs/visualcommit/backup/reset-20251120T090001000Z da31c977\n");
        AddOperation(app, key, "op-a2", "Check out feature/kinds", OperationOutcome.Failed, "Check out feature/kinds failed", summary: "1 conflict prevents checkout. Commit, stash, or discard your changes first.");
        AddCall(app, repo.Path, "op-a2", "git switch --no-guess --no-overwrite-ignore feature/kinds", exitCode: 1, error: "error: Your local changes would be overwritten by checkout:\n\tother.txt\nAborting\n");

        var rows = app.ActivityLog().Rows;
        Assert.Equal(2, rows.Count);
        var cleanUp = rows[0];
        Assert.Equal(
            (ActivityLogRowKind.Operation, "IconChevronRight", "IconCheck", "09:00:00", "Clean up backups", "Removed 3 backups", "0 ms", false),
            (cleanUp.Kind, cleanUp.Chevron, cleanUp.Glyph, cleanUp.Time, cleanUp.Title, cleanUp.Message, cleanUp.Duration, cleanUp.IsOpen));
        Assert.Equal((0.0, Brush(app, "VcSuccessBrush")), (cleanUp.Indent, cleanUp.GlyphColor));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), cleanUp.MessageColor);
        Assert.Equal(24, cleanUp.Bounds!.Value.Height, 1);
        AssertBounds(new Rect(260, 504, 440, 24), cleanUp.Bounds.Value);
        Assert.Equal(("IconClose", Brush(app, "VcDangerBrush")), (rows[1].Glyph, rows[1].GlyphColor));
        Assert.Equal(Brush(app, "VcDangerBrush"), rows[1].MessageColor);

        // A click opens the operation, its chevron down: its note, then its call, 22 high, indented 24.
        app.ClickActivityLogRow("Clean up backups");
        rows = app.ActivityLog().Rows;
        Assert.Equal(
            [
                (ActivityLogRowKind.Operation, 0, 0.0, "Clean up backups"),
                (ActivityLogRowKind.Note, 1, 24.0, "Removed backup reset-20251120T090001000Z (older than 30 days)"),
                (ActivityLogRowKind.Call, 1, 24.0, "git update-ref --stdin"),
                (ActivityLogRowKind.Operation, 0, 0.0, "Check out feature/kinds"),
            ],
            rows.Select(row => (row.Kind, row.Depth, row.Indent ?? double.NaN, row.AutomationName)));
        Assert.Equal((true, "IconChevronDown"), (rows[0].IsOpen, rows[0].Chevron));
        Assert.Equal(22, rows[2].Bounds!.Value.Height, 1);
        Assert.Equal(
            ("IconChevronRight", "exit 0", Brush(app, "VcTextSecondaryBrush"), "git update-ref --stdin", false, "0 ms"),
            (rows[2].Chevron, rows[2].ExitText, rows[2].ExitColor, rows[2].Command, rows[2].OnTemporaryIndex, rows[2].Duration));
        Assert.Equal(("Removed backup reset-20251120T090001000Z (older than 30 days)", Brush(app, "VcTextSecondaryBrush")), (rows[1].Text, rows[1].TextColor));
        Assert.Null(rows[1].Chevron);

        // The call opens to its box, titled Input, indented 48, whose value UI Automation reads.
        app.ClickActivityLogRow("git update-ref --stdin");
        rows = app.ActivityLog().Rows;
        Assert.Equal("IconChevronDown", rows[2].Chevron);
        var box = rows[3];
        Assert.Equal((ActivityLogRowKind.Output, 2, 48.0), (box.Kind, box.Depth, box.Indent));
        Assert.Equal([new ActivityLogOutputPart("Input", "delete refs/visualcommit/backup/reset-20251120T090001000Z da31c977")], box.Parts);
        var output = (ActivityLogOutputBox)app.FindByAutomationId("ActivityLogOutput");
        Assert.Equal("Input\ndelete refs/visualcommit/backup/reset-20251120T090001000Z da31c977", output.Text);
        Assert.True(rows[4].IsInView, "The list stayed at its end as the rows opened (AD15).");

        // With the keyboard: the call closed by a click, Down to the failed checkout, Right opens
        // it, Left closes it; a Down past the last row stays there.
        app.ClickActivityLogRow("git update-ref --stdin");
        Assert.DoesNotContain(app.ActivityLog().Rows, row => row.Kind == ActivityLogRowKind.Output);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("Check out feature/kinds", FocusedRow(app)?.AutomationName);
        app.PressKey(Key.Right);
        rows = app.ActivityLog().Rows;
        var summary = rows.Single(row => row.Kind == ActivityLogRowKind.FailureSummary);
        Assert.Equal(("1 conflict prevents checkout. Commit, stash, or discard your changes first.", Brush(app, "VcDangerBrush")), (summary.Text, summary.TextColor));
        var switchCall = rows.Single(row => row.Command == "git switch --no-guess --no-overwrite-ignore feature/kinds");
        Assert.Equal(("exit 1", Brush(app, "VcDangerBrush")), (switchCall.ExitText, switchCall.ExitColor));
        Assert.True(rows[^1].IsInView);
        app.PressKey(Key.Left);
        Assert.DoesNotContain(app.ActivityLog().Rows, row => row.Kind == ActivityLogRowKind.FailureSummary);
        Assert.Equal("Check out feature/kinds", FocusedRow(app)?.AutomationName);
        app.PressKey(Key.Up);
        app.PressKey(Key.Left);
        Assert.Equal("Clean up backups", FocusedRow(app)?.AutomationName);

        // Rows that come keep the list at its end; once scrolled up, it stays where it is.
        for (var i = 0; i < 12; i++)
        {
            AddOperation(app, key, $"op-b{i}", $"Fetch {i}", OperationOutcome.Succeeded, "Fetched");
        }

        Assert.True(app.ActivityLogRow("Fetch 11").IsInView);
        var list = app.Find<ScrollViewer>("ActivityLogList");
        list.Offset = new Vector(0, 0);
        app.Settle();
        AddOperation(app, key, "op-c", "Fetch 12", OperationOutcome.Succeeded, "Fetched");
        Assert.Equal(0, list.Offset.Y, 1);
        rows = app.ActivityLog().Rows;
        Assert.False(rows[^1].IsInView);

        // A row the list has not realised has no parts: the snapshot reads only what is drawn.
        var unrealised = rows.Where(row => row.Bounds is null).ToList();
        Assert.NotEmpty(unrealised);
        Assert.All(unrealised, row => Assert.Equal((null, null, null, null, null), (row.Title, row.Glyph, row.Chevron, row.Time, row.Indent)));
        Assert.Equal("Fetch 12", rows[^1].AutomationName);

        // What the controls draw, not the rows behind them: a title changed on its text block shows.
        var title = app.Window.GetVisualDescendants().OfType<TrimmedTextBlock>()
            .First(text => text.Name == "ActivityLogOperationTitle" && text.IsEffectivelyVisible && text.Text == "Clean up backups");
        title.Text = "Drawn title";
        Assert.Equal(("Drawn title", "Clean up backups"), (app.ActivityLog().Rows[0].Title, ((ActivityLogOperationRow)app.ActivityLogModel.Current!.Rows[0]).Title));
    }

    /// <summary>
    /// A list scrolled up that becomes short enough to show its end (its rows closed) keeps to its
    /// end again (AD15): an operation opened afterwards shows its last call (found by phase 5's A2,
    /// which closes a row the keys brought into view).
    /// </summary>
    [AvaloniaFact]
    public async Task A_list_scrolled_up_then_shortened_to_its_end_keeps_to_its_end_again()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        app.ClickActivityLogToggle();
        AddOperation(app, key, "op-s1", "Merge feature/theirs", OperationOutcome.Conflicts, "Merge stopped: 1 file has conflicts");
        for (var i = 0; i < 12; i++)
        {
            AddCall(app, repo.Path, "op-s1", $"git status {i}");
        }

        // Opened, the merge's calls run past the list's end, which the list keeps in view.
        app.ClickActivityLogRow("Merge feature/theirs");
        Assert.True(app.ActivityLog().Rows[^1].IsInView, "The list did not keep to its end as the row opened.");

        // As A2 does: a call's box opened and closed by a click, Left to the merge's row, which
        // the list scrolls up to, and Left again closes it: the one row left is the list's end.
        app.ClickActivityLogRow("git status 9");
        app.ClickActivityLogRow("git status 9");
        Assert.Equal("git status 9", FocusedRow(app)?.AutomationName);
        app.PressKey(Key.Left);
        app.Settle();
        Assert.Equal("Merge feature/theirs", FocusedRow(app)?.AutomationName);
        Assert.True(app.ActivityLogRow("Merge feature/theirs").IsInView);
        app.PressKey(Key.Left);
        app.Settle();
        Assert.Single(app.ActivityLog().Rows);
        var list = app.Find<ScrollViewer>("ActivityLogList");

        // A row opened now makes the list longer, and the list keeps to its end again.
        AddOperation(app, key, "op-s2", "Abort the merge", OperationOutcome.Succeeded, "Aborted the merge");
        for (var i = 0; i < 12; i++)
        {
            AddCall(app, repo.Path, "op-s2", $"git merge --abort {i}");
        }

        app.ClickActivityLogRow("Abort the merge");
        var rows = app.ActivityLog().Rows;
        Assert.Equal("git merge --abort 11", rows[^1].AutomationName);
        Assert.True(rows[^1].IsInView, "The list did not keep to its end once it was at its end again.");
        Assert.True(list.Offset.Y > 0, "The list did not scroll to its end.");
    }

    /// <summary>
    /// A click on the list's last call opens its box at the list's end, which the list scrolls to
    /// (AD15): the call keeps the keyboard focus, so that the keys of 2.2 and Esc go on working,
    /// in a short list and in one that runs past its view (found by phase 5's A2).
    /// </summary>
    [AvaloniaFact]
    public async Task A_call_opened_by_a_click_at_the_lists_end_keeps_the_focus()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        app.ClickActivityLogToggle();
        AddOperation(app, key, "op-f1", "Merge tool for notes.txt", OperationOutcome.Cancelled, "Merge tool for notes.txt cancelled");
        AddCall(app, repo.Path, "op-f1", "git ls-files --unmerged -z -- notes.txt", output: "100644 624b469 1\tnotes.txt\n");
        AddCall(app, repo.Path, "op-f1", "git mergetool --tool=slow --no-prompt -- notes.txt", output: "Merging:\nnotes.txt\n");

        // A short list: the box opens under the last call, all of it in view.
        app.ClickActivityLogRow("Merge tool for notes.txt");
        app.ClickActivityLogRow("git mergetool --tool=slow --no-prompt -- notes.txt");
        Assert.Equal(ActivityLogRowKind.Output, app.ActivityLog().Rows[^1].Kind);
        Assert.Equal("git mergetool --tool=slow --no-prompt -- notes.txt", FocusedRow(app)?.AutomationName);
        app.ClickActivityLogRow("git mergetool --tool=slow --no-prompt -- notes.txt");
        app.ClickActivityLogRow("Merge tool for notes.txt");

        // A list that runs past its view: the box opens at its end, which the list scrolls to.
        AddOperation(app, key, "op-f2", "Abort the merge", OperationOutcome.Succeeded, "Aborted the merge");
        for (var i = 0; i < 12; i++)
        {
            AddCall(app, repo.Path, "op-f2", $"git merge --abort {i}", output: "one\ntwo\nthree\nfour\n");
        }

        app.ClickActivityLogRow("Abort the merge");
        app.ClickActivityLogRow("git merge --abort 11");
        var rows = app.ActivityLog().Rows;
        Assert.Equal(ActivityLogRowKind.Output, rows[^1].Kind);
        Assert.True(rows[^1].IsInView, "The list did not keep to its end as the call opened.");
        Assert.Equal("git merge --abort 11", FocusedRow(app)?.AutomationName);

        // The keys work from there and keep the focus too: Left closes the call, Right opens it
        // again at the list's end, Up goes to the call above it.
        app.PressKey(Key.Left);
        app.Settle();
        Assert.Equal(ActivityLogRowKind.Call, app.ActivityLog().Rows[^1].Kind);
        Assert.Equal("git merge --abort 11", FocusedRow(app)?.AutomationName);
        app.PressKey(Key.Right);
        app.Settle();
        rows = app.ActivityLog().Rows;
        Assert.True(rows[^1] is { Kind: ActivityLogRowKind.Output, IsInView: true }, "Right did not open the call's box at the list's end.");
        Assert.Equal("git merge --abort 11", FocusedRow(app)?.AutomationName);
        app.PressKey(Key.Up);
        app.Settle();
        Assert.Equal("git merge --abort 10", FocusedRow(app)?.AutomationName);
    }

    [AvaloniaFact]
    public async Task The_splitter_keeps_the_panel_between_its_limits_and_the_session_keeps_its_height_across_a_restart()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using (var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]))
        {
            await app.WaitForRepositoryAsync();
            app.ClickActivityLogToggle();
            Assert.Null(app.Session.SessionState.Current.ActivityLogHeight);

            app.DragActivityLogSplitter(-100);
            AssertBounds(new Rect(260, 374, 440, 300), app.ActivityLog().Bounds);
            Assert.Equal(300, app.Session.SessionState.Current.ActivityLogHeight!.Value, 2);

            app.DragActivityLogSplitter(-300);
            AssertBounds(new Rect(260, 253, 440, 421), app.ActivityLog().Bounds);
            Assert.Equal(421, app.Session.SessionState.Current.ActivityLogHeight!.Value, 2);

            app.DragActivityLogSplitter(400);
            AssertBounds(new Rect(260, 554, 440, 120), app.ActivityLog().Bounds);
            Assert.Equal(120, app.Session.SessionState.Current.ActivityLogHeight!.Value, 2);

            app.DragActivityLogSplitter(-180);
            Assert.Equal(300, app.Session.SessionState.Current.ActivityLogHeight!.Value, 2);
        }

        Assert.Equal(300, new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current.ActivityLogHeight!.Value, 2);
        using var again = ShellDriver.Start(data.Path);
        await again.WaitForRepositoryAsync();
        Assert.False(again.ActivityLogView.IsEffectivelyVisible, "The panel is closed when the app starts (2.1).");
        again.ClickActivityLogToggle();
        AssertBounds(new Rect(260, 374, 440, 300), again.ActivityLog().Bounds);

        // A lower window: the panel gives way first, down to 120, the content keeping 160.
        again.Window.Height = 560;
        again.Settle();
        var height = again.BoundsOf(again.FindByAutomationId("CommitGraph")).Height;
        Assert.Equal(Math.Min(300, height - 165), again.ActivityLog().Bounds.Height, 1);
        again.Window.Height = 700;
        again.Settle();
        Assert.Equal(300, again.ActivityLog().Bounds.Height, 1);
    }

    [AvaloniaFact]
    public async Task Esc_closes_the_log_only_with_the_focus_in_it_and_gives_the_focus_back_to_the_graph_or_the_page()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        AddOperation(app, key, "op-e1", "Fetch", OperationOutcome.Succeeded, "Fetched from all remotes");
        app.ClickActivityLogToggle();

        // The focus in the graph: Esc is not the log's.
        app.Click(app.GraphRowPoint(1));
        app.PressKey(Key.Escape);
        Assert.True(app.ActivityLogView.IsEffectivelyVisible);

        // The focus in the log: Esc closes it and the graph has the focus again.
        app.ClickActivityLogRow("Fetch");
        Assert.True(app.ActivityLogView.HasFocusWithin);
        app.PressKey(Key.Escape);
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        await app.WaitForAsync(() => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl, "the focus on the graph");

        // A page open (340 before 370): the first Esc closes only the log, and the page has the focus.
        var page = new FocusPage();
        await repository.OpenPageAsync(page);
        app.ClickActivityLogToggle();
        app.ClickActivityLogRow("Fetch");
        app.PressKey(Key.Escape);
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.Same(page, repository.Page);
        await app.WaitForAsync(() => page.FocusRequests == 1, "the page asked for the focus");

        // The close button with the focus in the log does the same.
        app.ClickActivityLogToggle();
        app.ClickActivityLogRow("Fetch");
        app.Click(app.FindByAutomationId("ActivityLogClose"));
        Assert.False(app.ActivityLogModel.IsOpen);
        Assert.Same(page, repository.Page);
    }

    [AvaloniaFact]
    public async Task With_the_resolver_open_Esc_in_the_log_closes_the_log_and_leaves_the_resolver()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read");
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");
        AddOperation(app, WorktreeKey.Of(repository.Repository.GitDirectory), "op-r1", "Merge feature/theirs", OperationOutcome.Conflicts, "Merge stopped: 1 file has conflicts");

        app.ClickActivityLogToggle();

        // The panel spans what the graph area spans: the graph's and the right panel's columns.
        AssertBounds(new Rect(260, 474, 840, 200), app.ActivityLog().Bounds);
        var merge = app.ActivityLogRow("Merge feature/theirs");
        Assert.Equal(("IconAlert", Brush(app, "VcWarningBrush"), Brush(app, "VcWarningBrush")), (merge.Glyph, merge.GlyphColor, merge.MessageColor));
        app.ClickActivityLogRow("Merge feature/theirs");
        app.PressKey(Key.Escape);

        // The log closed, the resolver stayed (340 before its 350), and the focus went back into
        // the resolver, the graph area's content, not to the banner above it (AD6).
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.True(repository.IsResolverOpen);
        await app.WaitForAsync(() => FocusedIn<ConflictResolverView>(app), "the focus in the resolver");
    }

    [AvaloniaFact]
    public async Task With_the_interactive_list_open_Esc_in_the_log_closes_the_log_and_gives_the_list_the_focus()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.InteractiveAsync();
        var baseSha = (await repo.GitAsync("rev-parse", "main~6")).StandardOutput.Trim();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync(repository);
        var list = await repository.Repository.ReadRebaseListAsync(baseSha, "refs/heads/main", fromCommit: true, cancellationToken: TestContext.Current.CancellationToken);
        await repository.ShowInteractiveRebaseAsync(new InteractiveRebaseRequest(new RebaseTarget(baseSha, baseSha[..7]), "refs/heads/main", FromHere: true, CheckoutFirst: false), list);
        await app.WaitForRebaseListAsync();
        AddOperation(app, WorktreeKey.Of(repository.Repository.GitDirectory), "op-i1", "Fetch", OperationOutcome.Succeeded, "Fetched");

        app.ClickActivityLogToggle();
        app.ClickActivityLogRow("Fetch");
        Assert.True(app.ActivityLogView.HasFocusWithin);
        app.PressKey(Key.Escape);

        // 340 before the list's 360: the first Esc closes only the log, and the list has the focus.
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.True(repository.IsInteractiveRebaseOpen);
        Assert.True(app.IsRebaseListOpen);
        await app.WaitForAsync(() => FocusedIn<InteractiveRebaseView>(app), "the focus in the interactive list");
    }

    [AvaloniaFact]
    public async Task A_rows_menu_copies_and_each_tab_shows_its_own_log_with_its_rows_left_open()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var other = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, other.Path]);
        await app.WaitUntilReadyAsync();
        var first = await app.WaitForRepositoryAsync();
        var firstKey = WorktreeKey.Of(first.Repository.GitDirectory);
        AddOperation(app, firstKey, "op-m1", "Push main", OperationOutcome.Succeeded, "Pushed main to origin");
        AddCall(app, repo.Path, "op-m1", "git push --progress origin main", output: "To origin\n   c0bc2dd..d1e23a3  main -> main\n");
        app.ClickActivityLogToggle();
        app.ClickActivityLogRow("Push main");

        app.RightClick(app.ActivityLogRow("git push --progress origin main").Bounds!.Value.Center);
        var menu = app.Menu();
        Assert.Equal("git push --progress origin main", menu.Header);
        Assert.Equal(["Copy command", "Copy output"], menu.Items.Select(item => item.Label));
        app.ClickMenuItem("Copy command");
        await app.WaitForAsync(() => app.Clipboard.Texts.Count == 1, "the copy");
        Assert.Equal("git push --progress origin main", app.Clipboard.Texts[0]);

        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[1]);
        var second = await app.WaitForRepositoryAsync();
        AddOperation(app, WorktreeKey.Of(second.Repository.GitDirectory), "op-m2", "Fetch", OperationOutcome.Succeeded, "Fetched");
        Assert.True(app.ActivityLogView.IsEffectivelyVisible, "The panel's being open is the window's.");
        Assert.Equal(["Fetch"], app.ActivityLog().Rows.Select(row => row.AutomationName));
        Assert.Equal("history", app.ActivityLog().RepositoryName);

        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[0]);
        app.Settle();
        Assert.Equal(["Push main", "git push --progress origin main"], app.ActivityLog().Rows.Select(row => row.AutomationName));
    }

    [AvaloniaFact]
    public async Task A_repository_opened_through_a_link_shows_its_openings_reads_under_Show_reads()
    {
        // A folder reached through a symbolic link or a junction (macOS's temporary folder is one:
        // /var leads to /private/var; CI run 38847538648): the opening's first read runs in the
        // folder as it was opened, while git reports the working tree with the link resolved. That
        // read is still the worktree's (2.3, AD11 as ruled in D183), whether the link leads to the
        // working tree or the tab opened a folder inside it.
        using var repo = await Scenarios.HistoryAsync();
        using var other = await Scenarios.HistoryAsync();
        using var links = new TempDirectory("links");
        Directory.CreateDirectory(Path.Combine(other.Path, "sub"));
        var top = links.Combine("top");
        var otherLink = links.Combine("other");
        var sub = Path.Combine(otherLink, "sub");
        LinkFolder(top, repo.Path);
        LinkFolder(otherLink, other.Path);
        try
        {
            using var data = new TempDirectory("data");
            using var app = ShellDriver.StartWithTabs(data.Path, [top, sub]);
            await app.WaitUntilReadyAsync();
            var first = await app.WaitForRepositoryAsync();
            Assert.False(
                WorktreeKey.Comparer.Equals(top, first.Path),
                $"Git reported the working tree as the link {top}: the test needs a link git resolves.");
            Assert.Contains(app.GitCalls(), call => call.CommandText == OpeningRevParse && call.OperationId is null && WorktreeKey.Comparer.Equals(call.WorkingDirectory, top));
            app.ClickActivityLogToggle();
            app.Click(app.FindByAutomationId("ActivityLogShowReads"));
            app.Settle();

            Assert.Equal([OpeningRevParse, CommonDirRevParse], TopLevelNames(app).Take(2));

            app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[1]);
            var second = await app.WaitForRepositoryAsync();
            Assert.False(
                WorktreeKey.Comparer.Equals(otherLink, second.Path),
                $"Git reported the working tree as the link {otherLink}: the test needs a link git resolves.");
            Assert.Contains(app.GitCalls(), call => call.CommandText == OpeningRevParse && call.OperationId is null && WorktreeKey.Comparer.Equals(call.WorkingDirectory, sub));
            Assert.True(app.ActivityLog().ShowReads, "Show reads is not checked on the second tab.");
            Assert.Equal([OpeningRevParse, CommonDirRevParse], TopLevelNames(app).Take(2));
        }
        finally
        {
            Directory.Delete(top);
            Directory.Delete(otherLink);
        }
    }

    private const string OpeningRevParse = "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir";

    private const string CommonDirRevParse = "git rev-parse --git-common-dir";

    /// <summary>The names of the log's top-level rows, top to bottom.</summary>
    private static List<string> TopLevelNames(ShellDriver app) =>
        [.. app.ActivityLog().Rows.Where(row => row.Depth == 0).Select(row => row.AutomationName)];

    /// <summary>
    /// Makes <paramref name="link"/> lead to the folder <paramref name="target"/>: a junction on
    /// Windows, which needs no privilege there (a symbolic link needs Developer Mode or an
    /// administrator), a symbolic link elsewhere. <see cref="Directory.Delete(string)"/> removes
    /// either without touching the target.
    /// </summary>
    private static void LinkFolder(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/d", "/c", "mklink", "/J", link, target },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");

        // A line on each at most: neither pipe can fill while the other is read.
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link), $"mklink /J {link} {target} failed ({process.ExitCode}): {output}{error}");
    }

    /// <summary>The row of the log that has the keyboard focus, or null.</summary>
    private static ActivityLogRow? FocusedRow(ShellDriver app) =>
        (app.Window.FocusManager?.GetFocusedElement() as Control)?.DataContext as ActivityLogRow;

    /// <summary>Whether the keyboard focus is inside a <typeparamref name="T"/> of the window.</summary>
    private static bool FocusedIn<T>(ShellDriver app)
        where T : Visual =>
        app.Window.FocusManager?.GetFocusedElement() is Visual focused && focused.GetSelfAndVisualAncestors().OfType<T>().Any();

    private static void AddOperation(ShellDriver app, string key, string id, string title, OperationOutcome outcome, string message, IReadOnlyList<string>? notes = null, string? summary = null)
    {
        var sequence = RecordSequence.Next();
        app.Session.Operations.Start(new OperationStart(id, key, "history", title, sequence, Nine));
        app.Session.Operations.Add(new OperationRecord(id, "history", title, Nine, TimeSpan.Zero, outcome, message)
        {
            RepositoryKey = key,
            Sequence = sequence,
            DisplayedStart = Nine,
            Notes = notes ?? [],
            FailureSummary = summary,
        });
        app.Settle();
    }

    private static void AddCall(ShellDriver app, string folder, string operationId, string command, int exitCode = 0, string output = "", string error = "", string? input = null)
    {
        app.Session.GitCalls.Add(new GitCallRecord(Nine, TimeSpan.Zero, command, folder, GitCallOutcome.Completed, exitCode, output, error, operationId)
        {
            Sequence = RecordSequence.Next(),
            DisplayedStart = Nine,
            StandardInput = input,
        });
        app.Settle();
    }

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.True(
            Math.Abs(expected.X - actual.X) <= 2 && Math.Abs(expected.Y - actual.Y) <= 2
            && Math.Abs(expected.Width - actual.Width) <= 2 && Math.Abs(expected.Height - actual.Height) <= 2,
            $"Expected {expected}, got {actual}.");
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>A page that counts how often it was asked to take the focus.</summary>
    private sealed class FocusPage : IGraphAreaPage
    {
        public string Title => "Focus page";

        public int FocusRequests { get; private set; }

        public event EventHandler? FocusRequested
        {
            add { }
            remove { }
        }

        public void Close()
        {
        }

        public void RequestFocus() => FocusRequests++;

        public void OnRefsRead(RepoRefs refs)
        {
        }

        public void Dispose()
        {
        }
    }
}
