using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Pages;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The pages in the whole window, on phase 5's scenarios, through the driver's helpers for them
/// (docs/phase-5-design.md 4, 5, 11, 13.4, 15.2; docs/test-reports/phase-5.md part B and its
/// decisions BD1 to BD14): each helper is tested here, as phase 4's are, and the pages' geometry,
/// texts, focus and clicks are those the report's checks B5 to B9 rely on. Window 1100×700, dark.
/// </summary>
public class ShellDriverPhase5PagesTests
{
    private static readonly Color Selection = Color.Parse("#2A3150");
    private static readonly Color Added = Color.Parse("#3FB97F");
    private static readonly Color Modified = Color.Parse("#E0A23B");
    private static readonly Color Renamed = Color.Parse("#7C8CFF");

    [AvaloniaFact]
    public async Task The_history_opens_from_the_stage_panel_beside_its_diff_and_only_a_rows_click_changes_the_right_panel()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // B5 steps 1 to 3.
        app.Click(app.WorkingRowPoint());
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs"));
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(4);
        await app.WaitForAsync(() => app.FileHistorySnapshot().DiffSideLabel == "c0bc2dd" && app.FileHistorySnapshot().Selected?.HasFocus == true, "the first row's diff and focus");
        var page = app.FileHistorySnapshot();

        Assert.Equal(new Rect(260, 88, 440, 586), page.Bounds);
        Assert.Equal(new Rect(260, 88, 440, 36), page.HeaderBounds);
        Assert.Equal(("History of Reader.cs", "src", null, true), (page.Title, page.Folder, page.From, page.BlameEnabled));
        Assert.False(page.IsSideBySide);
        Assert.Equal((124, 344), (page.ListBounds.Top, page.ListBounds.Bottom));
        Assert.Equal(new Rect(260, 344, 440, 330), page.DiffBounds);
        Assert.Equal(["c0bc2dd Read whole lines", "0575c7f Rename the parser to reader", "365482b Handle empty input", "1d5a7cf Add the parser"], page.Rows.Select(row => row.Name));
        Assert.Equal(
            [("M", Modified, null), ("R", Renamed, "renamed from src/Parser.cs"), ("M", Modified, "as src/Parser.cs"), ("A", Added, "added")],
            page.Rows.Select(row => (row.Letter, row.LetterColor!.Value, row.Secondary)));
        for (var row = 0; row < 4; row++)
        {
            Assert.Equal(new Rect(260, 124 + (26 * row), 440, 26), page.Rows[row].Bounds);
            Assert.Equal((null, null), (page.Rows[row].Author, page.Rows[row].Date));
        }

        Assert.True(page.Rows[0].IsSelected);
        Assert.True(page.Rows[0].HasFocus);
        Assert.Equal(Selection, page.Rows[0].Background);
        Assert.False(page.DiffShowsClose);
        Assert.True(repository.IsWorkingRowSelected);
        Assert.Equal("History of Reader.cs", AutomationProperties.GetName(app.FindByAutomationId("FileHistory")));
        Assert.Equal(new Point(420, 163), app.FileHistoryRowPoint(1));

        // Step 4: the user's click shows the commit in the right panel, selected behind the page.
        app.Click(app.FileHistoryRowPoint(1));
        app.WaitForCommitDetails();
        await app.WaitForAsync(() => app.FileHistorySnapshot().DiffSideLabel == "0575c7f", "the rename's diff");
        Assert.False(repository.IsWorkingRowSelected);
        Assert.Equal(7, repository.SelectedIndex);
        Assert.Equal("Rename the parser to reader", repository.Details.Subject);
        Assert.True(app.FileHistorySnapshot().Rows[1].HasFocus);

        // Down walks the list (step 6) and Esc closes the page, the graph taking the focus (11.1).
        app.PressKey(Key.Down);
        app.WaitForCommitDetails();
        Assert.Equal("Handle empty input", repository.Details.Subject);
        Assert.Equal(10, repository.SelectedIndex);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        await app.WaitForAsync(() => app.Find<CommitGraphControl>("GraphRows").IsFocused, "the graph to take the focus");
    }

    [AvaloniaFact]
    public async Task A_commits_diff_opens_over_the_history_Esc_closes_only_the_diff_and_its_menu_opens_another_history()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // B5 steps 5 and 6: a commit's file opens its history from that commit; Down shows 365482b.
        app.Click(app.GraphRowPoint(7));
        app.WaitForCommitDetails();
        app.RightClick(await app.FileRowAsync("FileList", "src/Reader.cs"));
        Assert.Equal(["Reader.cs", "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"], app.Menu().Lines());
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(3);
        Assert.Equal("from 0575c7f", app.FileHistorySnapshot().From);
        app.PressKey(Key.Down);
        app.WaitForCommitDetails();
        Assert.Equal("Handle empty input", repository.Details.Subject);

        // Step 7: the right panel's file opens phase 2's diff over the page; Esc closes only it.
        app.Click(await app.FileRowAsync("FileList", "src/Parser.cs"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true && app.ShownPage is null, "the diff over the page");
        Assert.Equal("365482b", app.Find<TextBlock>("DiffSideLabel").Text);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().ListHasFocus, "the page back with the focus in its list");
        Assert.True(app.FileHistorySnapshot().Rows[1].IsSelected);

        // The diff's menu has the commit's history and blame (4.4): its history from that commit.
        app.Click(await app.FileRowAsync("FileList", "src/Parser.cs"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true, "the diff again");
        // On its new line 10, the first added line of its hunk (B5 step 7), on the text.
        var row = app.DiffRowOfLine(10);
        Assert.True(row >= 0);
        var point = app.DiffTextPoint(row);
        Assert.True(point.X >= 260 + 240 - 1 && point.X <= 700, $"The diff's text point {point} is not on the text.");
        app.RightClick(point);
        Assert.Equal(["Parser.cs", "File history", "Blame", "—", "Open file (disabled: \"The file is not in the working tree\")", "Show in folder", "Copy path"], app.Menu().Lines());
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(2);
        var page = app.FileHistorySnapshot();
        Assert.Equal(("History of Parser.cs", "from 365482b"), (page.Title, page.From));
        Assert.Equal(["365482b Handle empty input", "1d5a7cf Add the parser"], page.Rows.Select(entry => entry.Name));
        Assert.Equal([null, "added"], page.Rows.Select(entry => entry.Secondary));
        Assert.False(repository.IsDiffOpen);
    }

    [AvaloniaFact]
    public async Task The_reflog_reads_again_after_an_operation_and_a_lost_commits_branch_takes_its_mark_away()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => app.LogLines().Any(line => line.Contains("reflog: Clean up backups: Succeeded", StringComparison.Ordinal)), "the backups' clean-up");

        // B8 steps 2, 5, 6 and 7.
        await repository.MenuActions.ShowReflogAsync(null);
        await app.WaitForPageAsync<ReflogView>();
        var reads = app.ReflogSnapshot().ReadsShown;
        app.RightClick(app.ReflogRowPoint("HEAD@{9}"));
        app.WaitForCommitDetails();
        Assert.Equal("Try a shortcut", repository.Details.Subject);
        Assert.Equal(
            ["d07c677 Try a shortcut", "Create branch here…", "Check out this commit", "—", "Reset main to this commit…", "Cherry-pick onto main", "—", "Show in graph (disabled: \"This commit is not in the graph\")", "Copy SHA"],
            app.Menu().Lines());
        app.ClickMenuItem("Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "shortcut");
        app.Click(app.FindByAutomationId("NameDialogOption"));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForIdleAsync();
        await app.WaitForReflogReadAsync(reads);

        Assert.Equal("d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f", await repo.RefTargetAsync("refs/heads/shortcut"));
        var reflog = app.ReflogSnapshot();
        Assert.Equal(36, reflog.Rows.Count);
        Assert.Equal(["HEAD@{1}", "HEAD@{11}", "HEAD@{17}", "HEAD@{23}", "HEAD@{33}"], reflog.Rows.Where(row => row.IsLost).Select(row => row.Selector));
        Assert.Equal("1 entry is not shown: it names no commit that exists", reflog.Footer);

        // Step 8: the graph has it now; a click selects it behind the page.
        app.Click(app.ReflogRowPoint("HEAD@{9}"));
        app.WaitForCommitDetails();
        Assert.Equal(1, repository.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task At_1920_the_history_lies_beside_its_diff_with_the_author_and_without_the_date()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1920, height: 1080);
        var repository = await app.WaitForRepositoryAsync();

        // B5 step 12, in the dark theme: a commit's file from HEAD's own commit has no "from" (BD24).
        app.Click(app.GraphRowPoint(0));
        app.WaitForCommitDetails();
        app.RightClick(await app.FileRowAsync("FileList", "src/Reader.cs"));
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(4);
        var page = app.FileHistorySnapshot();

        Assert.Equal(new Rect(260, 88, 1260, 966), page.Bounds);
        Assert.True(page.IsSideBySide);
        Assert.Null(page.From);
        Assert.Equal((260, 827), (page.ListBounds.Left, page.ListBounds.Right));
        Assert.Equal(new Rect(827, 124, 693, 930), page.DiffBounds);
        Assert.Equal(("Mira Kovac", null), (page.Rows[1].Author, page.Rows[1].Date));
        Assert.Equal(0, repository.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task The_blame_draws_its_runs_beside_the_text_and_a_click_and_a_double_click_reach_the_commit()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // B6 step 1.
        app.Click(app.WorkingRowPoint());
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs"));
        app.ClickMenuItem("Blame");
        await app.WaitForPageAsync<BlameView>();
        await app.WaitForAsync(() => app.BlameSnapshot().TextHasFocus, "the blame's text to take the focus");
        var blame = app.BlameSnapshot();

        Assert.Equal(("Blame of Reader.cs", "in the working tree"), (blame.Title, blame.Caption));
        Assert.Equal(new Rect(260, 124, 130, 550), blame.GutterBounds);
        Assert.Equal(new Rect(390, 124, 310, 550), blame.TextBounds);
        // Part B's formula gives column 1 at x 422.4 (the text's left + 30.4 + 2); the checks allow ±2.
        Assert.Equal(422.4, app.BlameTextPoint(1, 1).X, 2.0);
        Assert.Equal(24, blame.Text.Split('\n').Length);
        Assert.Equal(15, blame.Runs.Count);
        Assert.Equal(
            [(1, 2), (3, 3), (4, 4), (5, 7), (8, 8), (9, 9), (10, 10), (11, 12), (13, 13), (14, 14), (15, 19), (20, 20), (21, 21), (22, 23), (24, 24)],
            blame.Runs.Select(run => (run.FirstLine, run.LastLine)));
        Assert.Equal(["1d5a7cf", "2026-01-01"], blame.Runs[0].Texts.Select(text => text.Text));
        Assert.Equal(["Not committed yet"], blame.Runs[1].Texts.Select(text => text.Text));
        Assert.Equal(264, blame.Runs[0].Texts[0].X, 0.5);
        Assert.Equal(["VcPanelBackgroundBrush", "VcWindowBackgroundBrush"], blame.Runs.Take(2).Select(run => run.Background));
        Assert.True(blame.IsHighlighted);
        Assert.Equal("Blame of Reader.cs", AutomationProperties.GetName(app.FindByAutomationId("Blame")));

        // The points of part B's conventions (13.4).
        var gutter = app.BlameGutterPoint(1);
        Assert.Equal(new Point(300, 131.9), new Point(gutter.X, Math.Round(gutter.Y, 1)));
        var text = app.BlameTextPoint(10, 9);
        Assert.Equal(480.0, text.X, 2.0);
        Assert.Equal(274.5, text.Y, 0.5);

        // Step 2: the tooltip of a run (BD11).
        Assert.Equal(
            "1d5a7cf999727e7f724054348c81866a62025184\nMira Kovac <mira@example.com>\n2026-01-01 12:01\nAdd the parser\nas src/Parser.cs",
            await app.RestPointerAtAsync(app.BlameGutterPoint(1)));

        // Step 4: a click on a run in the gutter; step 5: on the text.
        app.Click(app.BlameGutterPoint(13));
        app.WaitForCommitDetails();
        Assert.Equal(("Read whole lines", 0, false), (repository.Details.Subject, repository.SelectedIndex, repository.IsWorkingRowSelected));
        Assert.Equal("VcSelectionBrush", app.BlameSnapshot().Runs[8].Background);
        app.Click(app.BlameTextPoint(10, 9));
        app.WaitForCommitDetails();
        Assert.Equal(("Handle empty input", 10), (repository.Details.Subject, repository.SelectedIndex));
        Assert.Equal(["VcSelectionBrush", "VcPanelBackgroundBrush"], new[] { app.BlameSnapshot().Runs[6].Background, app.BlameSnapshot().Runs[8].Background });

        // Step 11: a double-click on a run shows its commit in the graph.
        app.DoubleClick(app.BlameGutterPoint(20));
        await app.WaitForAsync(() => app.ShownPage is null, "the page to close");
        Assert.Equal(0, repository.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task All_files_lists_the_tree_and_an_unchanged_files_click_opens_its_blame_at_the_commit()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // B7 steps 1 to 4.
        app.Click(app.GraphRowPoint(0));
        app.WaitForCommitDetails();
        app.Click(app.FindByAutomationId("AllFilesButton"));
        await app.WaitForAsync(() => repository.Details.ChangedFilesTitle == "All files (6)" && !repository.Details.IsLoading, "all files");
        Assert.Equal(
            [("", "LICENSE", ""), ("", "README.md", ""), ("", "logo.png", "assets"), ("", "notes.md", "docs"), ("", "Config.cs", "src"), ("M", "Reader.cs", "src")],
            app.DetailsFileRows().Select(row => (row.Letter, row.Name, row.Folder)));

        app.Click(await app.FileRowAsync("FileList", "LICENSE"));
        await app.WaitForPageAsync<BlameView>();
        var blame = app.BlameSnapshot();
        Assert.Equal(("Blame of LICENSE", "at c0bc2dd Read whole lines"), (blame.Title, blame.Caption));
        Assert.Equal(["7829335", "2026-01-01"], Assert.Single(blame.Runs).Texts.Select(text => text.Text));
        Assert.True(app.DetailsFileRows().Single(row => row.Name == "LICENSE").IsSelected);
        Assert.False(repository.IsDiffOpen);

        app.Click(app.BlameGutterPoint(3));
        app.WaitForCommitDetails();
        Assert.Equal(("Initial commit", 13, "All files (3)"), (repository.Details.Subject, repository.SelectedIndex, repository.Details.ChangedFilesTitle));
    }

    [AvaloniaFact]
    public async Task The_reflog_and_the_backups_list_lay_out_their_rows_and_the_source_menu_opens_under_its_button()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => app.LogLines().Any(line => line.Contains("reflog: Clean up backups: Succeeded", StringComparison.Ordinal)), "the backups' clean-up");

        // B8 steps 1 and 2.
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.ClickMenuItem("Show reflog");
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot().ListHasFocus, "the reflog's list to take the focus");
        var reflog = app.ReflogSnapshot();

        Assert.Equal(("Reflog of HEAD", "HEAD"), (reflog.Title, reflog.SourceLabel));
        Assert.Equal(36, reflog.Rows.Count);
        Assert.DoesNotContain(reflog.Rows, row => row.Selector == "HEAD@{5}");
        Assert.Equal(["HEAD@{1}", "HEAD@{9}", "HEAD@{11}", "HEAD@{17}", "HEAD@{23}", "HEAD@{33}"], reflog.Rows.Where(row => row.IsLost).Select(row => row.Selector));
        Assert.Equal(("1 entry is not shown: it names no commit that exists", new Rect(260, 648, 440, 26)), (reflog.Footer, reflog.FooterBounds!.Value));
        Assert.Equal((124, 648), (reflog.ListBounds.Top, reflog.ListBounds.Bottom));
        Assert.Null(reflog.Selected);
        var first = reflog.Rows[0];
        Assert.Equal(new Rect(260, 124, 440, 26), first.Bounds);
        Assert.Null(first.TimeBounds);
        Assert.Equal((268, 340), (first.SelectorBounds!.Value.Left, first.SelectorBounds.Value.Right));
        Assert.Equal((348, 496), (first.ActionBounds!.Value.Left, first.ActionBounds.Value.Right));
        Assert.Equal(504, first.CommitBounds!.Value.Left);
        Assert.Equal(new Rect(672, 150, 20, 26), app.LostMarkBounds("HEAD@{1}"));
        Assert.Equal(new Point(420, 345), app.ReflogRowPoint("HEAD@{9}"));
        Assert.Equal("No branch, tag or remote branch reaches this commit", await app.RestPointerAtAsync(app.LostMarkBounds("HEAD@{1}").Center));

        // B8 step 4: a lost commit's row shows it in the right panel, the graph having no row for it.
        app.Click(app.ReflogRowPoint("HEAD@{9}"));
        app.WaitForCommitDetails();
        Assert.Equal(("Try a shortcut", -1), (repository.Details.Subject, repository.SelectedIndex));

        // B8 step 9: the source menu at the selector's bottom-left corner (BD7).
        app.Click(reflog.SourceBounds.Center);
        var menu = app.Menu();
        Assert.Equal(["Reflog and backups", "HEAD", "clash", "feature/renamed", "main", "topic", "—", "Backups"], menu.Lines());
        Assert.Equal(reflog.SourceBounds.BottomLeft, menu.Bounds.TopLeft);

        // B9 step 1: the backups list.
        app.ClickMenuItem("Backups");
        await app.WaitForAsync(() => app.ReflogSnapshot().Backups.Count == 15, "the backups list");
        var backups = app.ReflogSnapshot();
        Assert.Equal(("Backups", "Backups", null), (backups.Title, backups.SourceLabel, backups.FooterBounds));
        var top = backups.Backups[0];
        Assert.Equal(("branch-20260101T122701000Z", "2026-01-01 12:27:01", "Deleted branch doomed", "44e646b", "Doomed work"), (top.Name, top.Time, top.Words, top.ShortSha, top.Subject));
        Assert.Equal((268, 396), (top.TimeBounds!.Value.Left, top.TimeBounds.Value.Right));
        Assert.Equal((404, 524), (top.WordsBounds!.Value.Left, top.WordsBounds.Value.Right));
        Assert.Equal((532, 692), (app.BackupWordsBounds(top.Name).Right + 8, top.CommitBounds!.Value.Left + 160));
        Assert.Equal(new Point(420, 501), app.BackupRowPoint("abort-20260101T120601000Z"));
        Assert.Equal("Backups", AutomationProperties.GetName(app.FindByAutomationId("Reflog")));
    }

    [AvaloniaFact]
    public async Task Closing_a_page_reveals_the_commit_picked_on_it_when_the_graph_shows_again()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");

        // 11.1's "Revealing on the way back": enough commits on top that "Add the parser" lies out of
        // the graph's view (as B5 step 11 makes it with the activity log open).
        for (var commit = 1; commit <= 20; commit++)
        {
            // Only filler.txt: src/Reader.cs keeps its change, so the working-changes row shows.
            repo.WriteFile("filler.txt", $"line {commit}\n");
            await repo.GitAsync("add", "filler.txt");
            await repo.GitAsync("commit", "--quiet", "--message", $"Filler {commit}");
        }

        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var graph = app.Find<CommitGraphControl>("GraphRows");
        var parser = repository.Graph.IndexOf(PageFakes.AddParserSha);
        Assert.True(graph.RowBounds(parser).Top > graph.Bounds.Height, "Add the parser is in view at the start.");

        app.Click(app.WorkingRowPoint());
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs"));
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(4);
        app.Click(app.FileHistoryRowPoint(3));
        app.WaitForCommitDetails();
        Assert.Equal(parser, repository.SelectedIndex);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => repository.ShowsGraph, "the graph to show again");
        app.Capture();

        var bounds = graph.RowBounds(parser);
        var row = new Rect(app.BoundsOf(graph).TopLeft + bounds.TopLeft, bounds.Size);
        Assert.True(row.Top >= app.BoundsOf(graph).Top && row.Bottom <= app.BoundsOf(graph).Bottom, $"Add the parser is at {row}, outside the graph's view {app.BoundsOf(graph)}.");
        Assert.Equal("Add the parser", repository.Details.Subject);
    }
}
