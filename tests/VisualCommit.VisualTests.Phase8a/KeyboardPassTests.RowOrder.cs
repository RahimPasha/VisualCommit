using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Rebase;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using U = VisualCommit.VisualTests.Phase8a.Phase8aExpectations.U;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Tab and Shift+Tab go through a list's rows in the rows' order (design 6.3.4): every list whose
/// rows hold elements Tab stops at, after rows were inserted while it showed (a folder opened, a
/// branch made or a file changed outside the app, a list scrolled so that controls are made again).
/// The lists make their rows' controls as they are needed and add the control of an inserted row
/// last, and Avalonia's Tab follows the controls. Each fact walks one list from the stop before it,
/// with Tab through every stop its rows hold to the stop after it, and back with Shift+Tab; a stop is
/// written by its automation id and its row's key. Two facts are guards, which held before: the
/// activity log's list and the interactive rebase's list, each one stop (their Tab navigation is
/// <c>Once</c>), after a row was inserted or moved while it showed. The fact of the stage panel's two
/// lists also holds the step from one list straight into the other when no stop lies between them.
/// </summary>
public sealed partial class KeyboardPassTests
{
    /// <summary>
    /// "ops", the left panel: Local branches › feature opened by Enter, feature/alpha hidden (its
    /// eye shows) and feature/beta made outside the app, so that alpha, beta, conflict and ff come
    /// after the rows the panel made first. From the filter, Tab goes through every row in the rows'
    /// order, the eye after alpha's own button, to the graph; Shift+Tab back to the filter.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_walks_the_left_panels_rows_in_their_order_after_a_folder_opened_a_branch_hidden_and_a_branch_made_outside_the_app()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusLeftSectionAsync(app);
        await app.TabToAsync(focused => focused is Button { DataContext: RefItemRow { Key: FeatureFolderKey } }, "Local branches › feature");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.RefRowModel(U.LocalBranches, U.Feature) is RefItemRow { IsOpen: true }, "Local branches › feature open");
        await app.WaitForIdleAsync();

        await repository.Operations.HideBranchAsync("refs/heads/feature/alpha");
        await app.WaitForGraphReloadAsync();
        await repo.GitAsync("branch", "feature/beta");
        await repository.RefreshAsync();
        await app.WaitForIdleAsync();
        await app.WaitForGraphReloadAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        Assert.True(repository.LeftPanel.Rows.Any(row => row.Key == "ref:refs/heads/feature/beta"), "The left panel does not show feature/beta.");
        Assert.True(((RefItemRow)repository.LeftPanel.Rows.Single(row => row.Key == AlphaKey)).ShowsHiddenToggle, "alpha's eye does not show.");

        app.Click(app.Find<TextBox>("FilterBox"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "FilterBox", "the focus in the filter");
        var forward = repository.LeftPanel.Rows.SelectMany(LeftPanelStops).ToList();

        // The opened folder's rows follow it, the eye right after alpha's own button.
        var folder = forward.IndexOf("RefItem " + FeatureFolderKey);
        Assert.Equal(
            ["RefItem " + AlphaKey, "RefHiddenToggle " + AlphaKey, "RefItem ref:refs/heads/feature/beta", "RefItem ref:refs/heads/feature/conflict", "RefItem " + FfKey],
            forward.Skip(folder + 1).Take(5));

        AssertWalk(app, "FilterBox", forward, AfterRefList, Reversed(forward));
    }

    /// <summary>
    /// The left panel of a repository with main and b01 to b40, b25 soloed (its solo button shows),
    /// scrolled down by 400 pixels and back up by 104, so that the rows that came into view at the
    /// top were made after the others. From the filter, Tab goes through every row in the rows' order,
    /// from the first, also the rows below the view, to the graph; Shift+Tab back to the filter.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_walks_the_left_panels_rows_in_their_order_after_the_list_was_scrolled_down_and_up()
    {
        using var repo = await BranchesAsync(40);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        await repository.Operations.SoloBranchAsync("refs/heads/b25");
        await app.WaitForGraphReloadAsync();
        await app.WaitForIdleAsync();
        app.Settle();
        Assert.True(((RefItemRow)repository.LeftPanel.Rows.Single(row => row.Key == "ref:refs/heads/b25")).ShowsSoloToggle, "b25's solo button does not show.");

        var list = app.Find<ItemsControl>("RefList");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        foreach (var offset in new[] { 400.0, 296.0 })
        {
            scroller.Offset = new Vector(0, offset);
            app.Settle();
            list.ItemsPanelRoot!.InvalidateMeasure();
            app.Settle();
        }

        Assert.Equal(296, scroller.Offset.Y);
        Assert.True(list.ContainerFromIndex(0) is null, "The list still holds its first row's control.");

        app.Click(app.Find<TextBox>("FilterBox"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "FilterBox", "the focus in the filter");
        var forward = repository.LeftPanel.Rows.SelectMany(LeftPanelStops).ToList();
        Assert.Contains("RefSoloToggle ref:refs/heads/b25", forward);

        AssertWalk(app, "FilterBox", forward, AfterRefList, Reversed(forward));
    }

    /// <summary>
    /// The commit details of "Add the tree" in tree mode: the folder a closed and opened again by
    /// Enter, so that a/one.txt and a/two.txt come after the rows the list made first. From All files,
    /// the stop before the list, Tab goes through the rows in their order to the stop after the list;
    /// Shift+Tab back.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_walks_the_details_tree_in_its_rows_order_after_a_folder_was_closed_and_opened()
    {
        using var repo = await TreeCommitAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(app.GraphRowPoint(0));
        app.WaitForCommitDetails();
        var files = Repository(app).Details.Files;
        await app.WaitForAsync(() => files.Count == TreeFiles.Length, "the commit's four files");
        await app.WaitForIdleAsync();
        var tree = app.FindByAutomationId("TreeFilesButton");
        await app.WaitForAsync(() => tree.IsEffectivelyVisible && tree.Bounds.Width > 0, "the details' Tree button");
        app.Click(tree);
        await app.WaitForAsync(() => files is [{ IsFolder: true, Key: "a" }, ..] && files.Count == 6, $"the files as a tree (they are {string.Join(", ", files.Select(row => row.Key))})");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        await CloseAndOpenFolderAsync(app, files, "FileTreeFolder", "a");
        app.Click(tree);
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "AllFilesButton", "All files, the stop before the list");

        var forward = files.Select(row => FileStop(row, "FileTreeFolder", "ChangedFile")).ToList();
        AssertWalk(app, "AllFilesButton", forward, AfterDetailsFiles, Reversed(forward));
    }

    /// <summary>
    /// The comparison of "Initial commit" with "Add the tree" (the two commits selected) in tree
    /// mode: the folder a closed and opened again by Enter. From the Tree button, Tab goes through
    /// the rows in their order to the stop after the list; Shift+Tab back.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_walks_a_comparisons_tree_in_its_rows_order_after_a_folder_was_closed_and_opened()
    {
        using var repo = await TreeCommitAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.Click(app.GraphRowPoint(1), KeyModifiers.Control);
        await app.WaitForCompareAsync();
        var tree = app.FindByAutomationId("CompareTreeButton");
        app.Click(tree);
        var files = Repository(app).Comparison!.Files;
        await app.WaitForAsync(() => files is [{ IsFolder: true, Key: "a" }, ..] && files.Count == 6, $"the comparison's files as a tree (they are {string.Join(", ", files.Select(row => row.Key))})");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        await CloseAndOpenFolderAsync(app, files, "FileTreeFolder", "a");
        app.Click(tree);
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        Assert.True(app.FocusedAutomationId() == "CompareTreeButton", $"The click on Tree left the focus on {app.DescribeFocus()}.");

        var forward = files.Select(row => FileStop(row, "FileTreeFolder", "ChangedFile")).ToList();
        AssertWalk(app, "CompareTreeButton", forward, AfterCompareFiles, Reversed(forward));
    }

    /// <summary>
    /// "changes", the stage panel's unstaged list: 0-new.txt made outside the app, so that its row
    /// comes first and its control after the others. From Discard all, Tab goes through every row in
    /// the rows' order, each row's own button, its Stage and its Discard, to Unstage all; Shift+Tab
    /// goes back over the rows' own buttons (a row's buttons show only while it holds the focus, is
    /// selected or is under the pointer) to Discard all.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_walks_the_unstaged_list_in_its_rows_order_after_a_file_made_outside_the_app_came_first()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var unstaged = Repository(app).Changes.Unstaged.Rows;
        repo.WriteFile(NewFirstFile, "new\n");
        await app.WaitForAsync(() => unstaged is [{ Key: NewFirstFile }, ..], $"{NewFirstFile} first in the unstaged list");
        await app.WaitForIdleAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();

        app.Find<Button>("DiscardAllButton").Focus(NavigationMethod.Tab);
        app.Settle();
        var forward = unstaged.SelectMany(row => (string[])[$"StageFileRow {row.Key}", $"RowStageButton {row.Key}", $"RowDiscardButton {row.Key}"]).ToList();
        var backward = unstaged.Reverse().Select(row => $"StageFileRow {row.Key}").ToList();
        AssertWalk(app, "DiscardAllButton", forward, "UnstageAllButton", backward);
    }

    /// <summary>
    /// "changes" with 0-a.txt made and 0-b.txt made and staged outside the app, so that each list's
    /// first row has its control after the others, and Unstage all disabled (as it is while its own
    /// run goes on), so that no stop lies between the stage panel's two lists: from Discard all, Tab
    /// goes through the unstaged rows and on through the staged rows from their first, each row's own
    /// button and its Unstage, to the summary; Shift+Tab goes back over the rows' own buttons, from
    /// the staged list's last row and from its first into the unstaged list's last, to Discard all.
    /// </summary>
    [AvaloniaFact]
    public async Task Tab_goes_from_the_unstaged_list_straight_into_the_staged_list_when_no_stop_lies_between_them()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        repo.WriteFile("0-a.txt", "a\n");
        repo.WriteFile("0-b.txt", "b\n");
        await repo.GitAsync("add", "--", "0-b.txt");
        await app.WaitForAsync(
            () => changes.Unstaged.Rows is [{ Key: "0-a.txt" }, ..] && changes.Staged.Rows is [{ Key: "0-b.txt" }, ..],
            "0-a.txt first in the unstaged list and 0-b.txt first in the staged list");
        await app.WaitForIdleAsync();
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        app.Find<Button>("UnstageAllButton").IsEnabled = false;
        app.Find<Button>("DiscardAllButton").Focus(NavigationMethod.Tab);
        app.Settle();

        string[] forward =
        [
            .. changes.Unstaged.Rows.SelectMany(row => (string[])[$"StageFileRow {row.Key}", $"RowStageButton {row.Key}", $"RowDiscardButton {row.Key}"]),
            .. changes.Staged.Rows.SelectMany(row => (string[])[$"StageFileRow {row.Key}", $"RowUnstageButton {row.Key}"]),
        ];
        string[] backward =
        [
            .. Enumerable.Reverse(changes.Staged.Rows).Select(row => $"StageFileRow {row.Key}"),
            .. Enumerable.Reverse(changes.Unstaged.Rows).Select(row => $"StageFileRow {row.Key}"),
        ];
        AssertWalk(app, "DiscardAllButton", forward, "CommitSummary", backward);
    }

    /// <summary>
    /// The activity log, an operation opened by a click so that its call's row is inserted below it:
    /// the log's rows are one stop (their Tab navigation is <c>Once</c>), so Tab from the clicked row
    /// leaves the log, Shift+Tab comes back to that row, and Shift+Tab again leaves the log. A guard:
    /// this holds without a walk of its own.
    /// </summary>
    [AvaloniaFact]
    public async Task The_activity_logs_rows_are_one_stop_after_an_operation_was_opened()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => app.Session.Shell.ActivityLog.IsShown, "the activity log");
        var key = WorktreeKey.Of(repository.Repository.GitDirectory);
        var nine = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);
        foreach (var (id, title) in new[] { ("op-a", "Read the notes"), ("op-b", "Read the readme") })
        {
            var sequence = RecordSequence.Next();
            app.Session.Operations.Start(new OperationStart(id, key, repository.Name, title, sequence, nine));
            app.Session.Operations.Add(new OperationRecord(id, repository.Name, title, nine, TimeSpan.Zero, OperationOutcome.Succeeded, "Read")
            {
                RepositoryKey = key,
                Sequence = sequence,
                DisplayedStart = nine,
            });
            app.Session.GitCalls.Add(new GitCallRecord(nine, TimeSpan.Zero, $"git show HEAD:{id}.txt", repo.Path, GitCallOutcome.Completed, 0, "Some notes\n", string.Empty, id)
            {
                Sequence = RecordSequence.Next(),
                DisplayedStart = nine,
            });
        }

        app.Settle();
        app.ClickActivityLogRow("Read the readme");
        await app.WaitForAsync(() => app.Window.GetVisualDescendants().OfType<ActivityLogRowPanel>().Any(row => AutomationProperties.GetName(row) == "git show HEAD:op-b.txt" && row.IsEffectivelyVisible), "the opened operation's call");
        app.Settle();
        var rows = app.Find<ItemsControl>("ActivityLogRows");
        var clicked = (Control)app.FocusedElement!;
        Assert.True(clicked is ActivityLogRowPanel && AutomationProperties.GetName(clicked) == "Read the readme", $"The click left the focus on {app.DescribeFocus()}.");

        app.PressKey(Key.Tab);
        app.Settle();
        Assert.False(app.FocusIsWithin(rows), $"Tab from the clicked row stayed in the log, on {app.DescribeFocus()}.");
        app.PressKey(Key.Tab, RawInputModifiers.Shift);
        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, clicked), $"Shift+Tab back into the log reached {app.DescribeFocus()}, not the clicked row.");
        app.PressKey(Key.Tab, RawInputModifiers.Shift);
        app.Settle();
        Assert.False(app.FocusIsWithin(rows), $"Shift+Tab from the clicked row stayed in the log, on {app.DescribeFocus()}.");
    }

    /// <summary>
    /// "interactive", the interactive rebase of main from Base (the graph's seventh row), as the
    /// rebase list's checks open it: its second row clicked and moved one row down by Alt+Down, so
    /// that its item moved while the list showed (the list makes the moved row's control again). The
    /// list's rows are one stop (their Tab navigation is <c>Once</c>), so Tab from the moved row
    /// leaves the list, Shift+Tab comes back to that row, and Shift+Tab again leaves the list. A
    /// guard: this holds without a walk of its own.
    /// </summary>
    [AvaloniaFact]
    public async Task The_interactive_rebases_rows_are_one_stop_after_a_row_was_moved()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Interactive rebase of main from here…");
        await app.WaitForRebaseListAsync();
        var before = app.RebaseListSnapshot();
        Assert.True(before.Rows.Count >= 3, $"The list holds {before.Rows.Count} rows, too few to move the second one down.");
        var moved = before.Rows[1];
        app.ClickRebaseRow(moved.Subject);
        app.Settle();
        Assert.True(app.RebaseListSnapshot().Row(moved.Subject).HasKeyboardFocus, $"The click on {moved.Name} left the focus on {app.DescribeFocus()}.");

        app.PressKey(Key.Down, RawInputModifiers.Alt);
        await app.WaitForIdleAsync();
        app.Settle();
        var after = app.RebaseListSnapshot();
        Assert.True(
            after.Rows[2].Name == moved.Name && after.Rows[2].IsSelected && after.Rows[2].HasKeyboardFocus,
            $"Alt+Down did not move {moved.Name} to the third row with the focus: the rows are {string.Join(", ", after.Rows.Select(row => row.Name))}, the focus on {app.DescribeFocus()}.");
        var rows = (ItemsControl)app.FindByAutomationId("RebaseRows");
        bool OnMovedRow() => app.FocusedElement is RebaseListRow row && AutomationProperties.GetName(row) == moved.Name;

        app.PressKey(Key.Tab);
        app.Settle();
        Assert.False(app.FocusIsWithin(rows), $"Tab from the moved row stayed in the list, on {app.DescribeFocus()}.");
        app.PressKey(Key.Tab, RawInputModifiers.Shift);
        app.Settle();
        Assert.True(OnMovedRow(), $"Shift+Tab back into the list reached {app.DescribeFocus()}, not the moved row {moved.Name}.");
        app.PressKey(Key.Tab, RawInputModifiers.Shift);
        app.Settle();
        Assert.False(app.FocusIsWithin(rows), $"Shift+Tab from the moved row stayed in the list, on {app.DescribeFocus()}.");
    }

    // ---- The walk ------------------------------------------------------------------------------

    /// <summary>The file a fact makes outside the app, which sorts before "changes"'s README.md.</summary>
    private const string NewFirstFile = "0-new.txt";

    /// <summary>What Tab reaches after the left panel's list: the graph, as before (the unfixed code's walk left the list there from its last control).</summary>
    private const string AfterRefList = "GraphRows";

    /// <summary>What Tab reaches after the details' changed files: the left splitter, the next stop in the window's tree, as before.</summary>
    private const string AfterDetailsFiles = "LeftSplitter";

    /// <summary>What Tab reaches after a comparison's changed files: the left splitter, as before.</summary>
    private const string AfterCompareFiles = "LeftSplitter";

    /// <summary>The files "Add the tree" adds, in the flat list's order.</summary>
    private static readonly string[] TreeFiles = ["a/one.txt", "a/two.txt", "b/three.txt", "c.txt"];

    /// <summary>
    /// The focus on <paramref name="before"/>: Tab once for every stop of <paramref name="forward"/>
    /// and once more, which must reach those stops, each with the keyboard's frame, and then
    /// <paramref name="after"/>; then Shift+Tab once for every stop of <paramref name="backward"/>
    /// and once more, which must reach those and then <paramref name="before"/>. The whole walk is
    /// taken before it is compared, so that a failure shows it all.
    /// </summary>
    private static void AssertWalk(ShellDriver app, string before, IReadOnlyList<string> forward, string after, IReadOnlyList<string> backward)
    {
        Assert.True(StopOf(app) == before, $"The walk starts on {StopOf(app)}, not {before}.");
        var walked = new List<string>();
        var unframed = new List<string>();
        foreach (var (count, shift) in new[] { (forward.Count + 1, false), (backward.Count + 1, true) })
        {
            for (var tab = 0; tab < count; tab++)
            {
                app.PressKey(Key.Tab, shift ? RawInputModifiers.Shift : RawInputModifiers.None);
                app.Settle();
                walked.Add((shift ? "Shift+Tab: " : "Tab: ") + StopOf(app));
                if (!app.FocusVisible)
                {
                    unframed.Add(StopOf(app));
                }
            }
        }

        string[] expected = [.. forward.Select(stop => "Tab: " + stop), "Tab: " + after, .. backward.Select(stop => "Shift+Tab: " + stop), "Shift+Tab: " + before];
        Assert.True(
            expected.SequenceEqual(walked),
            "The walk went otherwise (expected, then reached):\n" + string.Join("\n", expected.Select((stop, index) => (stop == walked[index] ? "  " : "! ") + stop + (stop == walked[index] ? string.Empty : "  |  " + walked[index]))));
        Assert.True(unframed.Count == 0, $"These stops show no keyboard frame: {string.Join(", ", unframed)}.");
    }

    /// <summary>
    /// The focused element as a walk writes it: an element of a row by its automation id and its
    /// row's key; any other by its automation id, else its name, else its type.
    /// </summary>
    private static string StopOf(ShellDriver app)
    {
        if (app.FocusedElement is not Control control)
        {
            return app.FocusedElement?.GetType().Name ?? "nothing";
        }

        var id = AutomationProperties.GetAutomationId(control);
        var key = control.DataContext switch
        {
            RefListRow row => row.Key,
            ChangedFileRow row => row.Key,
            _ => null,
        };
        return (string.IsNullOrEmpty(id) ? control.Name ?? control.GetType().Name : id) + (key is null ? string.Empty : " " + key);
    }

    /// <summary>
    /// The stops a left panel row holds in Tab's order: its own button, then its eye and its solo
    /// button while they show; a row whose button is disabled (the "Pull requests" placeholder of a
    /// repository without a hosted remote, a message row that takes no click) none.
    /// </summary>
    private static IEnumerable<string> LeftPanelStops(RefListRow row) => !row.ActivateCommand.CanExecute(null) ? [] : row switch
    {
        RefSectionRow => [$"Section {row.Key}"],
        RefItemRow item =>
        [
            $"RefItem {item.Key}",
            .. item.ShowsHiddenToggle ? [$"RefHiddenToggle {item.Key}"] : Array.Empty<string>(),
            .. item.ShowsSoloToggle ? [$"RefSoloToggle {item.Key}"] : Array.Empty<string>(),
        ],
        PullRequestRow => [$"PullRequestItem {row.Key}"],
        PullRequestMessageRow => [$"PullRequestMessage {row.Key}"],
        _ => [],
    };

    /// <summary>A file list's row as a stop: its folder's button or its file's, by their automation ids.</summary>
    private static string FileStop(ChangedFileRow row, string folderId, string fileId) => $"{(row.IsFolder ? folderId : fileId)} {row.Key}";

    private static List<string> Reversed(IEnumerable<string> stops) => [.. stops.Reverse()];

    /// <summary>Tab from the focused Tree button to the folder <paramref name="folder"/>, then Enter closes it and Enter opens it again.</summary>
    private static async Task CloseAndOpenFolderAsync(ShellDriver app, IReadOnlyList<ChangedFileRow> files, string automationId, string folder)
    {
        await TabToFolderAsync(app, automationId, folder);
        var count = files.Count;
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => files.Count < count, $"the folder {folder} closed");
        await app.WaitForIdleAsync();
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => files.Count == count, $"the folder {folder} open again");
        await app.WaitForIdleAsync();
        app.Settle();
    }

    /// <summary>A repository whose first commit adds README.md and whose second, "Add the tree", adds <see cref="TreeFiles"/>.</summary>
    private static async Task<TempRepo> TreeCommitAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("README.md", "# Tree\n", "Initial commit");
            foreach (var name in TreeFiles)
            {
                repo.WriteFile(name, name + "\n");
            }

            await repo.CommitAsync("Add the tree");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>A repository with one commit on main and the branches b01 to b<paramref name="count"/> at it.</summary>
    private static async Task<TempRepo> BranchesAsync(int count)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("README.md", "# Branches\n", "Initial commit");
            for (var number = 1; number <= count; number++)
            {
                await repo.CreateBranchAsync(string.Create(CultureInfo.InvariantCulture, $"b{number:D2}"));
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
