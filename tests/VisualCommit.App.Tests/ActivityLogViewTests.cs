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
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
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
/// header, rows opened by click and by key, the list keeping to its end, the splitter's limits and
/// the session's height, Esc with the focus in the log, a row's menu, and each tab's log. The rows
/// are records added to the session's own logs, as the queue and the runner add theirs.
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
        Assert.Equal(ActivityLogViewModel.ShowText, ToolTip.GetTip(toggle));
        Assert.Equal(ActivityLogViewModel.ShowText, AutomationProperties.GetHelpText(toggle));
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
        Assert.Equal(ActivityLogViewModel.HideText, ToolTip.GetTip(toggle));
        Assert.Equal(ActivityLogViewModel.HideText, AutomationProperties.GetHelpText(toggle));
        var line = app.Find<Border>("ActivityLogSplitterLine");
        AssertBounds(new Rect(260, 473, 440, 1), app.BoundsOf(line));
        Assert.Equal(Brush(app, "VcBorderBrush"), ColorOf(line.Background));

        app.ClickActivityLogToggle();
        app.MoveToNeutral();
        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("ActivityLogSplitter").IsEffectivelyVisible);
        Assert.Equal(674, app.BoundsOf(graph).Bottom, 1);
        Assert.DoesNotContain("selected", toggle.Classes);
        Assert.Equal(ActivityLogViewModel.ShowText, ToolTip.GetTip(toggle));
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
        AddCall(app, repo.Path, "op-a2", "git switch --no-guess feature/kinds", exitCode: 1, error: "error: Your local changes would be overwritten by checkout:\n\tother.txt\nAborting\n");

        var rows = app.ActivityLog().Rows;
        Assert.Equal(2, rows.Count);
        var cleanUp = rows[0];
        Assert.Equal(
            (ActivityLogRowKind.Operation, ActivityLogOutcome.Succeeded, "09:00:00", "Clean up backups", "Removed 3 backups", "0 ms", false),
            (cleanUp.Kind, cleanUp.Outcome, cleanUp.Time, cleanUp.Title, cleanUp.Message, cleanUp.Duration, cleanUp.IsOpen));
        Assert.Equal(Brush(app, "VcSuccessBrush"), cleanUp.OutcomeColor);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), cleanUp.MessageColor);
        Assert.Equal(24, cleanUp.Bounds!.Value.Height, 1);
        AssertBounds(new Rect(260, 504, 440, 24), cleanUp.Bounds.Value);
        Assert.Equal(Brush(app, "VcDangerBrush"), rows[1].OutcomeColor);
        Assert.Equal(Brush(app, "VcDangerBrush"), rows[1].MessageColor);

        // A click opens the operation: its note, then its call, 22 high, indented 24.
        app.ClickActivityLogRow("Clean up backups");
        rows = app.ActivityLog().Rows;
        Assert.Equal(
            [
                (ActivityLogRowKind.Operation, 0, "Clean up backups"),
                (ActivityLogRowKind.Note, 1, "Removed backup reset-20251120T090001000Z (older than 30 days)"),
                (ActivityLogRowKind.Call, 1, "git update-ref --stdin"),
                (ActivityLogRowKind.Operation, 0, "Check out feature/kinds"),
            ],
            rows.Select(row => (row.Kind, row.Depth, row.AutomationName)));
        Assert.True(rows[0].IsOpen);
        Assert.Equal(22, rows[2].Bounds!.Value.Height, 1);
        Assert.Equal(("exit 0", Brush(app, "VcTextSecondaryBrush")), (rows[2].ExitText, rows[2].ExitColor));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), rows[1].TextColor);

        // The call opens to its box, titled Input, whose value UI Automation reads.
        app.ClickActivityLogRow("git update-ref --stdin");
        rows = app.ActivityLog().Rows;
        var box = rows[3];
        Assert.Equal((ActivityLogRowKind.Output, 2), (box.Kind, box.Depth));
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
        var switchCall = rows.Single(row => row.Command == "git switch --no-guess feature/kinds");
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
        Assert.False(app.ActivityLog().Rows[^1].IsInView);
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
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");
        AddOperation(app, WorktreeKey.Of(repository.Repository.GitDirectory), "op-r1", "Merge feature/theirs", OperationOutcome.Conflicts, "Merge stopped: 1 file has conflicts");

        app.ClickActivityLogToggle();

        // The panel spans what the graph area spans: the graph's and the right panel's columns.
        AssertBounds(new Rect(260, 474, 840, 200), app.ActivityLog().Bounds);
        var merge = app.ActivityLogRow("Merge feature/theirs");
        Assert.Equal((ActivityLogOutcome.Warning, Brush(app, "VcWarningBrush"), Brush(app, "VcWarningBrush")), (merge.Outcome, merge.OutcomeColor, merge.MessageColor));
        app.ClickActivityLogRow("Merge feature/theirs");
        app.PressKey(Key.Escape);

        Assert.False(app.ActivityLogView.IsEffectivelyVisible);
        Assert.True(repository.IsResolverOpen);
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

    /// <summary>The row of the log that has the keyboard focus, or null.</summary>
    private static ActivityLogRow? FocusedRow(ShellDriver app) =>
        (app.Window.FocusManager?.GetFocusedElement() as Control)?.DataContext as ActivityLogRow;

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
