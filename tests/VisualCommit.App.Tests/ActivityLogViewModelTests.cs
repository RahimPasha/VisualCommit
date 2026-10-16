using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The activity log's view model (docs/phase-5-design.md 2, D173; the report's part A): the rows it
/// makes from the session's records as they come, in the order they started, an operation opening
/// to its failure's summary, its notes and its calls, a call to its output box; the reads of the
/// shown worktree with "Show reads"; what each worktree keeps; the panel's state and height.
/// Without a synchronization context the records are taken at once, on the test's thread.
/// </summary>
public class ActivityLogViewModelTests
{
    private static readonly DateTimeOffset Nine = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly ActivityLogSource Graph = new(KeyOf("graph"), Tree("graph"), "graph");

    private static readonly ActivityLogSource Other = new(KeyOf("other"), Tree("other"), "other");

    [Fact]
    public void An_operation_shows_running_until_its_record_comes_and_then_its_outcome_message_and_duration()
    {
        var (operations, _, log) = Make();
        log.Show(Graph);
        operations.Start(Start("op-1", 10, "Check out feature/kinds"));

        var row = Assert.IsType<ActivityLogOperationRow>(Assert.Single(log.Current!.Rows));
        Assert.Equal(
            (ActivityLogOutcome.Running, false, "09:00:00", "Check out feature/kinds", "Running…", string.Empty),
            (row.Outcome, row.HasGlyph, row.Time, row.Title, row.Message, row.Duration));
        Assert.Equal("Check out feature/kinds", row.AutomationName);

        operations.Add(Record("op-1", 10, "Check out feature/kinds", OperationOutcome.Failed, "Check out feature/kinds failed") with { DisplayedDuration = TimeSpan.FromMilliseconds(1234) });

        Assert.Same(row, Assert.Single(log.Current.Rows));
        Assert.Equal(
            (ActivityLogOutcome.Failed, true, "Check out feature/kinds failed", ActivityLogTone.Danger, "1.2 s"),
            (row.Outcome, row.HasGlyph, row.Message, row.MessageTone, row.Duration));
    }

    [Theory]
    [InlineData(OperationOutcome.Succeeded, false, ActivityLogOutcome.Succeeded, ActivityLogTone.Secondary)]
    [InlineData(OperationOutcome.Succeeded, true, ActivityLogOutcome.Warning, ActivityLogTone.Warning)]
    [InlineData(OperationOutcome.Conflicts, false, ActivityLogOutcome.Warning, ActivityLogTone.Warning)]
    [InlineData(OperationOutcome.Failed, false, ActivityLogOutcome.Failed, ActivityLogTone.Danger)]
    [InlineData(OperationOutcome.Cancelled, false, ActivityLogOutcome.Cancelled, ActivityLogTone.Secondary)]
    public void Each_outcome_has_its_glyph_and_its_messages_tone(OperationOutcome outcome, bool warning, ActivityLogOutcome glyph, ActivityLogTone tone)
    {
        var (operations, _, log) = Make();
        log.Show(Graph);

        operations.Add(Record("op-1", 1, "Merge feature/theirs", outcome, "message") with { IsWarning = warning });

        var row = (ActivityLogOperationRow)log.Current!.Rows[0];
        Assert.Equal((glyph, tone), (row.Outcome, row.MessageTone));
    }

    [Fact]
    public void A_stage_panel_write_has_no_message_and_durations_read_in_ms_below_a_second()
    {
        var (operations, _, log) = Make();
        log.Show(Graph);

        operations.Add(Record("op-1", 1, "Stage", OperationOutcome.Failed, "Stage failed") with { Errors = ErrorDisplay.StagePanel });
        operations.Add(Record("op-2", 2, "Commit", OperationOutcome.Succeeded, string.Empty) with { DisplayedDuration = TimeSpan.FromMilliseconds(999.9) });

        var rows = log.Current!.Rows.Cast<ActivityLogOperationRow>().ToList();
        Assert.Equal((string.Empty, "0 ms"), (rows[0].Message, rows[0].Duration));
        Assert.Equal("999 ms", rows[1].Duration);
        Assert.Equal("0 ms", ActivityLogRow.FormatDuration(TimeSpan.Zero));
        Assert.Equal("1.0 s", ActivityLogRow.FormatDuration(TimeSpan.FromSeconds(1)));
        Assert.Equal("12.3 s", ActivityLogRow.FormatDuration(TimeSpan.FromMilliseconds(12_345)));
    }

    [Fact]
    public void Rows_follow_the_order_operations_and_calls_started_whatever_their_times_and_whenever_the_log_was_made()
    {
        // The harnesses freeze the times, so only the sequence numbers order the rows (2.3).
        var operations = new OperationLog();
        var calls = new GitCallLog(operations: operations);
        operations.Add(Record("op-2", 20, "Second"));
        operations.Add(Record("op-1", 10, "First"));
        calls.Add(Read("git status", 15, Tree("graph")));
        calls.Add(Read("git log", 5, Tree("graph")));

        // The panel's view model made late reads what the logs hold already.
        var log = new ActivityLogViewModel(operations, calls, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc));
        log.Show(Graph);
        log.ShowReads = true;

        Assert.Equal(["git log", "First", "git status", "Second"], log.Current!.Rows.Select(row => row.AutomationName));
        operations.Add(Record("op-3", 12, "Third, started before the status"));
        Assert.Equal(["git log", "First", "Third, started before the status", "git status", "Second"], log.Current.Rows.Select(row => row.AutomationName));
    }

    [Fact]
    public void An_opened_operation_shows_its_summary_its_notes_and_its_calls_in_the_order_they_started()
    {
        var (operations, calls, log) = Make();
        log.Show(Graph);
        operations.Add(Record("op-1", 1, "Clean up backups", OperationOutcome.Succeeded, "Removed 3 backups") with
        {
            IsBackground = true,
            Notes = ["Removed backup reset-20251120T090001000Z (older than 30 days)", "Removed backup branch-20251120T090002000Z (older than 30 days)"],
        });
        calls.Add(Call("git update-ref --stdin", "op-1", 3) with { StandardInput = "delete refs/visualcommit/backup/reset-20251120T090001000Z da31c977\n" });
        calls.Add(Call("git for-each-ref", "op-1", 2));
        calls.Add(Call("git push", "op-other", 4));
        Assert.Single(log.Current!.Rows);

        log.ToggleRow(log.Current.Rows[0]);

        Assert.Equal(
            [
                (ActivityLogRowKind.Operation, 0, "Clean up backups"),
                (ActivityLogRowKind.Note, 1, "Removed backup reset-20251120T090001000Z (older than 30 days)"),
                (ActivityLogRowKind.Note, 1, "Removed backup branch-20251120T090002000Z (older than 30 days)"),
                (ActivityLogRowKind.Call, 1, "git for-each-ref"),
                (ActivityLogRowKind.Call, 1, "git update-ref --stdin"),
            ],
            log.Current.Rows.Select(row => (row.Kind, row.Depth, row.AutomationName)));
        Assert.True(((ActivityLogOperationRow)log.Current.Rows[0]).IsOpen);
        Assert.Equal(24, log.Current.Rows[1].Indent);
        Assert.All(log.Current.Rows.OfType<ActivityLogLineRow>(), line => Assert.Equal(ActivityLogTone.Secondary, line.Tone));

        // A summary set after the record comes first among the lines, in the danger colour.
        operations.SetFailureSummary("op-1", "Git could not clean up.");
        var summary = Assert.IsType<ActivityLogLineRow>(log.Current.Rows[1]);
        Assert.Equal((ActivityLogRowKind.FailureSummary, "Git could not clean up.", ActivityLogTone.Danger), (summary.Kind, summary.Text, summary.Tone));

        log.ToggleRow(log.Current.Rows[0]);
        Assert.Single(log.Current.Rows);
    }

    [Fact]
    public void A_call_opens_to_its_input_output_and_error_each_titled_or_says_there_is_no_output()
    {
        var (operations, calls, log) = Make();
        log.Show(Graph);
        operations.Add(Record("op-1", 1, "Merge feature/theirs", OperationOutcome.Conflicts, "Merge stopped: 1 file has conflicts"));
        calls.Add(Call("git merge --no-edit refs/heads/feature/theirs", "op-1", 2, exitCode: 1) with
        {
            StandardOutput = "Auto-merging notes.txt\nCONFLICT (content): Merge conflict in notes.txt\n",
            StandardError = "warning: something\n",
            StandardInput = "update refs/heads/main c0bc2dd d1e23a3\n",
        });
        calls.Add(Call("git ls-files --unmerged -z", "op-1", 3) with { OnTemporaryIndex = true });
        calls.Add(Call("git -c mergetool.keepBackup=false mergetool --tool=slow", "op-1", 4) with { Outcome = GitCallOutcome.Cancelled, ExitCode = null });
        calls.Add(Call("git nowhere", "op-1", 5) with { Outcome = GitCallOutcome.FailedToStart, ExitCode = null });
        log.ToggleRow(log.Current!.Rows[0]);

        var callRows = log.Current.Rows.OfType<ActivityLogCallRow>().ToList();
        Assert.Equal(
            [("exit 1", ActivityLogTone.Danger), ("exit 0", ActivityLogTone.Secondary), ("cancelled", ActivityLogTone.Secondary), ("failed to start", ActivityLogTone.Danger)],
            callRows.Select(row => (row.ExitText, row.ExitTone)));
        Assert.Equal([false, true, false, false], callRows.Select(row => row.OnTemporaryIndex));

        log.SetOpen(callRows[0], open: true);
        log.SetOpen(callRows[1], open: true);

        var outputs = log.Current.Rows.OfType<ActivityLogOutputRow>().ToList();
        Assert.Equal(2, outputs.Count);
        Assert.Equal(
            [
                new ActivityLogOutputPart("Input", "update refs/heads/main c0bc2dd d1e23a3"),
                new ActivityLogOutputPart("Output", "Auto-merging notes.txt\nCONFLICT (content): Merge conflict in notes.txt"),
                new ActivityLogOutputPart("Error", "warning: something"),
            ],
            outputs[0].Parts);
        Assert.Equal(
            "Input\nupdate refs/heads/main c0bc2dd d1e23a3\n\nOutput\nAuto-merging notes.txt\nCONFLICT (content): Merge conflict in notes.txt\n\nError\nwarning: something",
            outputs[0].Text);
        Assert.Equal((true, "No output", 2, 48.0), (outputs[1].IsEmpty, outputs[1].Text, outputs[1].Depth, outputs[1].Indent));
        Assert.Same(outputs[0], log.Current.Rows[log.Current.Rows.IndexOf(callRows[0]) + 1]);

        // Right and Left on a row that does not open change nothing.
        var count = log.Current.Rows.Count;
        log.SetOpen(outputs[0], open: false);
        Assert.Equal(count, log.Current.Rows.Count);
    }

    [Fact]
    public void Reads_show_with_Show_reads_under_the_worktree_whose_working_tree_holds_their_folder()
    {
        var (operations, calls, log) = Make();
        var nested = new ActivityLogSource(WorktreeKey.Of(Path.Combine(Tree("graph"), ".git", "worktrees", "inner")), Path.Combine(Tree("graph"), "inner"), "graph");
        log.Show(nested);
        log.Show(Other);
        log.Show(Graph);
        operations.Add(Record("op-1", 1, "Fetch") with { RepositoryKey = Graph.Key });
        calls.Add(Read("git rev-parse --is-inside-work-tree --show-toplevel", 2, Path.Combine(Tree("graph"), "src", "deep")));
        calls.Add(Read("git status", 3, Tree("graph")));
        calls.Add(Read("git status inner", 4, Path.Combine(Tree("graph"), "inner")));
        calls.Add(Read("git status other", 5, Tree("other")));
        calls.Add(Read("git status graph2", 6, Tree("graph2")));
        calls.Add(Read("git --version", 7, null));
        calls.Add(Call("git fetch", "op-1", 8));
        Assert.Equal(["Fetch"], log.Current!.Rows.Select(row => row.AutomationName));

        log.ShowReads = true;

        Assert.Equal(["Fetch", "git rev-parse --is-inside-work-tree --show-toplevel", "git status"], log.Current.Rows.Select(row => row.AutomationName));
        Assert.All(log.Current.Rows.OfType<ActivityLogCallRow>(), read => Assert.Equal((0, true), (read.Depth, read.IsRead)));
        log.Show(Other);
        Assert.Equal(["git status other"], log.Current.Rows.Select(row => row.AutomationName));
        log.Show(nested);
        Assert.Equal(["git status inner"], log.Current.Rows.Select(row => row.AutomationName));

        log.ShowReads = false;
        log.Show(Graph);
        Assert.Equal(["Fetch"], log.Current.Rows.Select(row => row.AutomationName));
    }

    [Fact]
    public void A_read_made_before_its_worktree_was_shown_is_its_read_once_it_is()
    {
        // AD11: a tab's opening reads come before the tab has its repository.
        var (_, calls, log) = Make();
        log.ShowReads = true;
        log.Show(Other);
        calls.Add(Read("git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir", 1, Tree("graph")));
        Assert.Empty(log.Current!.Rows);

        log.Show(Graph);

        Assert.Equal(["git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir"], log.Current.Rows.Select(row => row.AutomationName));
    }

    [Fact]
    public void A_read_made_in_a_folder_the_worktree_was_opened_from_is_its_read_whatever_path_git_reports_for_it()
    {
        // Opened through a link (2.3, AD11): the opening's first read runs in the folder as it was
        // opened, git reports the working tree with the link resolved and the opening asks it again
        // there. A second tab that opened the same worktree from another folder adds that folder.
        const string opening = "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir";
        var (_, calls, log) = Make();
        log.ShowReads = true;
        var link = Tree("link");
        var throughLink = Graph with { OpenedFolder = link };
        var mapped = Path.Combine(Tree("mapped"), "src");
        var fromMapped = Graph with { OpenedFolder = mapped };
        calls.Add(Read(opening, 1, link));
        calls.Add(Read("git rev-parse --git-common-dir", 2, Tree("graph")));
        calls.Add(Read("git status under the link", 3, Path.Combine(link, "src")));
        calls.Add(Read("git status linked", 4, Tree("linked")));
        log.Show(throughLink);

        Assert.Equal([opening, "git rev-parse --git-common-dir", "git status under the link"], log.Current!.Rows.Select(row => row.AutomationName));

        calls.Add(Read("git status mapped", 5, mapped));
        Assert.Equal(3, log.Current.Rows.Count);
        log.Show(Other);
        log.Show(fromMapped);
        Assert.Equal([opening, "git rev-parse --git-common-dir", "git status under the link", "git status mapped"], log.Current.Rows.Select(row => row.AutomationName));
        log.Show(throughLink);
        Assert.Equal([opening, "git rev-parse --git-common-dir", "git status under the link", "git status mapped"], log.Current.Rows.Select(row => row.AutomationName));
        log.Show(Other);
        Assert.Empty(log.Current.Rows);
    }

    [Fact]
    public void A_burst_of_reads_takes_no_operation_call_away_and_old_reads_leave_the_list()
    {
        var operations = new OperationLog();
        var calls = new GitCallLog(capacity: 3, operations: operations);
        var log = new ActivityLogViewModel(operations, calls, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc));
        log.Show(Graph);
        log.ShowReads = true;
        operations.Add(Record("op-1", 1, "Push main"));
        calls.Add(Call("git push origin main", "op-1", 2));
        log.ToggleRow(log.Current!.Rows[0]);
        for (var i = 0; i < 10; i++)
        {
            calls.Add(Read($"git status {i}", 10 + i, Tree("graph")));
        }

        Assert.Equal(
            ["Push main", "git push origin main", "git status 7", "git status 8", "git status 9"],
            log.Current.Rows.Select(row => row.AutomationName));
    }

    [Fact]
    public void A_worktree_not_shown_keeps_no_rows_so_no_record_the_call_log_dropped_stays_alive()
    {
        // 15.9: the rows of a worktree left (another tab, a closed one) held the records they showed
        // past the call log's bounds; Show makes them again from what the log keeps.
        var operations = new OperationLog();
        var calls = new GitCallLog(capacity: 3, operations: operations);
        var log = new ActivityLogViewModel(operations, calls, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc));
        log.Show(Graph);
        log.ShowReads = true;
        calls.Add(Read("git status 1", 1, Tree("graph")));
        calls.Add(Read("git status 2", 2, Tree("graph")));
        var graph = log.Current!;
        Assert.Equal(["git status 1", "git status 2"], graph.Rows.Select(row => row.AutomationName));

        log.Show(Other);
        Assert.Empty(graph.Rows);
        for (var i = 0; i < 3; i++)
        {
            calls.Add(Read($"git status other {i}", 3 + i, Tree("other")));
        }

        // The ring dropped graph's reads: none is held by a row, and none comes back.
        Assert.Empty(graph.Rows);
        log.Show(Graph);
        Assert.Same(graph, log.Current);
        Assert.Empty(graph.Rows);

        log.Show(null);
        Assert.Empty(graph.Rows);
    }

    [Fact]
    public void An_evicted_operation_leaves_with_its_calls_and_one_whose_calls_were_dropped_says_so()
    {
        var operations = new OperationLog(capacity: 2);
        var calls = new GitCallLog(operations: operations, operationCallBudget: 700);
        var log = new ActivityLogViewModel(operations, calls, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc));
        log.Show(Graph);
        operations.Add(Record("op-1", 1, "First"));
        calls.Add(Call("git one", "op-1", 2) with { StandardOutput = new string('x', 100) });
        operations.Add(Record("op-2", 3, "Second"));
        log.ToggleRow(log.Current!.Rows[0]);
        Assert.Equal(["First", "git one", "Second"], log.Current.Rows.Select(row => row.AutomationName));

        // A call of op-2 past the budget drops op-1's: op-1 says its commands are no longer kept.
        calls.Add(Call("git two", "op-2", 4) with { StandardOutput = new string('y', 100) });
        Assert.Equal(["First", ActivityLogViewModel.NoLongerKeptText, "Second"], log.Current.Rows.Select(row => row.AutomationName));
        Assert.Equal(ActivityLogRowKind.NoLongerKept, log.Current.Rows[1].Kind);

        // op-3's record pushes op-1 out of the operation log: its row goes.
        operations.Add(Record("op-3", 5, "Third"));
        Assert.Equal(["Second", "Third"], log.Current.Rows.Select(row => row.AutomationName));
    }

    [Fact]
    public void Each_worktree_keeps_its_open_rows_and_scroll_and_the_open_rows_stay_while_the_panel_is_closed()
    {
        var (operations, calls, log) = Make();
        operations.Add(Record("op-1", 1, "Fetch") with { RepositoryKey = Graph.Key });
        operations.Add(Record("op-2", 2, "Push other") with { RepositoryKey = Other.Key });
        calls.Add(Call("git fetch", "op-1", 3));
        log.Show(Graph);
        log.Open();
        log.ToggleRow(log.Current!.Rows[0]);
        var graph = log.Current;
        graph.IsScrolledToEnd = false;
        graph.ScrollOffset = 12;

        log.Show(Other);
        Assert.Equal(["Push other"], log.Current.Rows.Select(row => row.AutomationName));
        Assert.True(log.Current.IsScrolledToEnd);

        log.Close();
        log.Show(Graph);
        log.Open();
        Assert.Same(graph, log.Current);
        Assert.Equal(["Fetch", "git fetch"], log.Current.Rows.Select(row => row.AutomationName));
        Assert.Equal((false, 12.0), (log.Current.IsScrolledToEnd, log.Current.ScrollOffset));
    }

    [Fact]
    public void The_panel_opens_and_closes_for_the_window_and_hides_on_a_tab_without_a_repository()
    {
        var (_, _, log) = Make();
        var closing = 0;
        log.Closing += (_, _) => closing++;
        Assert.Equal((false, false, false, "Show every git command the app has run"), (log.CanToggle, log.IsOpen, log.IsShown, log.ToggleToolTip));
        Assert.False(log.ToggleCommand.CanExecute(null));

        log.Show(Graph);
        Assert.Equal((true, "graph"), (log.ToggleCommand.CanExecute(null), log.RepositoryName));
        log.ToggleCommand.Execute(null);
        Assert.Equal((true, true, "Hide the activity log"), (log.IsOpen, log.IsShown, log.ToggleToolTip));

        // A tab without a repository hides the panel; the window keeps it open for the next one.
        log.Show(null);
        Assert.Equal((true, false, false, "Show every git command the app has run", string.Empty), (log.IsOpen, log.IsShown, log.CanToggle, log.ToggleToolTip, log.RepositoryName));
        log.Show(Other);
        Assert.Equal((true, "other"), (log.IsShown, log.RepositoryName));

        log.ToggleCommand.Execute(null);
        Assert.Equal((false, false, 1), (log.IsOpen, log.IsShown, closing));
        log.CloseCommand.Execute(null);
        Assert.Equal(1, closing);
    }

    [Fact]
    public void The_height_is_the_sessions_and_fits_the_graph_area()
    {
        var session = new MemorySession(new SessionState { ActivityLogHeight = 320 });
        var log = new ActivityLogViewModel(new OperationLog(), null, session, new DateDisplay(TimeZoneInfo.Utc));
        Assert.Equal(320, log.PreferredHeight);
        Assert.Equal(200, new ActivityLogViewModel(new OperationLog(), null, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc)).PreferredHeight);

        log.SaveHeight(300);
        Assert.Equal((300.0, (double?)300), (log.PreferredHeight, session.Current.ActivityLogHeight));

        // 586 is the graph area at 1100×700: at most 421 (AD1), at least 120.
        Assert.Equal(200, ActivityLogViewModel.Fit(586, 200));
        Assert.Equal(421, ActivityLogViewModel.Fit(586, 900));
        Assert.Equal(120, ActivityLogViewModel.Fit(586, 50));
        Assert.Equal(801, ActivityLogViewModel.Fit(966, 2000));

        // A window too low for both: the panel gives way below 120, down to its header.
        Assert.Equal(100, ActivityLogViewModel.Fit(265, 200));
        Assert.Equal(30, ActivityLogViewModel.Fit(150, 200));
    }

    [Fact]
    public async Task Records_from_another_thread_are_all_taken_and_each_shows_once()
    {
        var (operations, calls, log) = Make();
        log.Show(Graph);
        log.ShowReads = true;

        Parallel.For(0, 50, i =>
        {
            operations.Start(Start($"op-{i}", 1000 + (i * 3), $"Operation {i}"));
            calls.Add(Call($"git call {i}", $"op-{i}", 1001 + (i * 3)));
            operations.Add(Record($"op-{i}", 1000 + (i * 3), $"Operation {i}"));
            calls.Add(Read($"git read {i}", 1002 + (i * 3), Tree("graph")));
        });

        // Under a synchronization context the records are taken over in posted batches.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (log.Current!.Rows.Count < 100 && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var names = log.Current.Rows.Select(row => row.AutomationName).ToList();
        Assert.Equal(100, names.Count);
        Assert.Equal(Enumerable.Range(0, 50).SelectMany(i => new[] { $"Operation {i}", $"git read {i}" }), names);
    }

    [Fact]
    public void A_rows_menu_copies_its_command_its_output_or_its_title()
    {
        var (operations, calls, log) = Make();
        log.Show(Graph);
        operations.Add(Record("op-1", 1, "Merge feature/theirs"));
        calls.Add(Call("git merge refs/heads/feature/theirs", "op-1", 2) with { StandardOutput = "Auto-merging notes.txt\n", StandardError = "warning: x\n" });
        calls.Add(Call("git status", "op-1", 3));
        log.ToggleRow(log.Current!.Rows[0]);
        var rows = log.Current.Rows;
        var (actions, recorder) = RecordingActions.Create();
        var context = new RepositoryMenuContext("graph", Tree("graph"), new RepoRefs(new HeadState("main", null), [], [], []), WorkingTreeStatus.Clean, [], ToolbarMenuState.Disabled, false);

        var operation = RepositoryMenus.ForActivityLogRow(context, ActivityLogViewModel.MenuTargetOf(rows[0])!, actions);
        var merge = RepositoryMenus.ForActivityLogRow(context, ActivityLogViewModel.MenuTargetOf(rows[1])!, actions);
        var status = RepositoryMenus.ForActivityLogRow(context, ActivityLogViewModel.MenuTargetOf(rows[2])!, actions);

        Assert.Equal("Merge feature/theirs", operation.HeaderText);
        Assert.Equal(["Copy title"], operation.Items.Select(item => item.Label));
        Assert.Equal("git merge refs/heads/feature/theirs", merge.HeaderText);
        Assert.Equal(["Copy command", "Copy output"], merge.Items.Select(item => item.Label));
        Assert.Equal(["Copy command"], status.Items.Select(item => item.Label));
        Assert.DoesNotContain(merge.Entries, entry => entry is MenuSeparator);
        Assert.Equal(new ActivityLogMenuTarget(null, "git merge refs/heads/feature/theirs", "Auto-merging notes.txt\nwarning: x"), ActivityLogViewModel.MenuTargetOf(rows[1]));
        Assert.Null(ActivityLogViewModel.MenuTargetOf(new ActivityLogLineRow(ActivityLogRowKind.Note, "Removed backup x")));

        operation.Items.Single().Invoke();
        foreach (var item in merge.Items)
        {
            item.Invoke();
        }

        Assert.Equal(
            ["CopyTextAsync(Merge feature/theirs)", "CopyTextAsync(git merge refs/heads/feature/theirs)", "CopyTextAsync(Auto-merging notes.txt\nwarning: x)"],
            recorder.Calls.Select(call => call.ToString()));
    }

    private static (OperationLog Operations, GitCallLog Calls, ActivityLogViewModel Log) Make()
    {
        var operations = new OperationLog();
        var calls = new GitCallLog(operations: operations);
        return (operations, calls, new ActivityLogViewModel(operations, calls, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc)));
    }

    private static string Tree(string name) => Path.Combine(Path.GetTempPath(), "activity-log-tests", name);

    private static string KeyOf(string name) => WorktreeKey.Of(Path.Combine(Tree(name), ".git"));

    private static OperationStart Start(string id, long sequence, string title) => new(id, Graph.Key, "graph", title, sequence, Nine);

    private static OperationRecord Record(string id, long sequence, string title, OperationOutcome outcome = OperationOutcome.Succeeded, string message = "") =>
        new(id, "graph", title, DateTimeOffset.UnixEpoch, TimeSpan.Zero, outcome, message)
        {
            RepositoryKey = Graph.Key,
            Sequence = sequence,
            DisplayedStart = Nine,
        };

    private static GitCallRecord Call(string command, string operationId, long sequence, int exitCode = 0) =>
        new(DateTimeOffset.UnixEpoch, TimeSpan.Zero, command, Tree("graph"), GitCallOutcome.Completed, exitCode, string.Empty, string.Empty, operationId)
        {
            Sequence = sequence,
            DisplayedStart = Nine,
        };

    private static GitCallRecord Read(string command, long sequence, string? folder) =>
        new(DateTimeOffset.UnixEpoch, TimeSpan.Zero, command, folder, GitCallOutcome.Completed, 0, string.Empty, string.Empty)
        {
            Sequence = sequence,
            DisplayedStart = Nine,
        };

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }
}
