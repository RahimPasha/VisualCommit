// Check B8 of phase 5's walk-through (docs/test-reports/phase-5.md, part B): the reflog page from the
// Local branches section's menu (design 5.1, 5.2, 11.1, 11.3): HEAD's with its lost marks and its gap
// line, a lost commit in the right panel and its entry menu, "Create branch here…" from it, the
// source menu, a branch's reflog by the source menu and by "Show reflog of {b}"; Esc. The part's map
// is in Phase5Checks.B.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Pages;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksB
{
    /// <summary>
    /// Check B8: HEAD's reflog with its lost marks and its gap line; d07c677, lost by a hard reset,
    /// in the right panel and its menu; a branch created at it; main's and topic's reflogs; Esc.
    /// "reflog", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B8_reflog_and_a_branch_from_a_lost_commit()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        await WaitForCleanUpAsync(app, repo);
        var refs = await RefsAsync(repo);
        var headLog = HeadReflogLines(repo);

        // At the start: RL0, nothing selected, the details placeholder.
        AssertGraph(app, RL0, ReflogLanes, showsDate: true);
        Assert.Equal((-1, false), (repository.SelectedIndex, repository.IsWorkingRowSelected));
        Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "The details placeholder does not show.");

        // Step 1: the Local branches section's menu.
        app.RightClick(await app.RefRowAsync("Local branches"));
        var menu = app.Menu();
        var shot = Shot(app, "b8a-section-menu");
        AssertMenuAsReported(app, menu, LocalBranchesMenu, "Create branch…", LocalBranchesMenuHeight, shot);

        // Step 2: Show reflog: HEAD's 36 entries, the lost marks, the footer.
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Show reflog");
        var page = await WaitForReflogAsync(app, ReflogOf("HEAD"), "HEAD", HeadReflog.Count);
        await app.WaitForAsync(() => app.ReflogSnapshot().ListHasFocus, "the reflog's list to take the keyboard focus");
        shot = Shot(app, "b8b-head-reflog");
        page = app.ReflogSnapshot();
        await AssertNoMenuAsync(app, null);
        AssertPageInPlaceOfGraph(app, PageBounds, page.Bounds);
        AssertReflogHeader(app, page, ReflogOf("HEAD"), "HEAD");
        Assert.Equal((ContentTop, ReflogListBottom), (page.ListBounds.Top, page.ListBounds.Bottom));
        await AssertReflogRowsAsync(app, page, HeadReflog);
        Assert.Equal(20, page.Rows.Count(row => row.Bounds is { } bounds && bounds.Top >= page.ListBounds.Top - 0.5 && bounds.Bottom <= page.ListBounds.Bottom + 0.5));
        Assert.True(page.Rows[19].Bounds!.Value.Bottom <= ReflogListBottom, "HEAD@{20} is not wholly in view.");
        Assert.Equal(HeadLost, page.Rows.Where(row => row.IsLost).Select(row => row.Selector));
        foreach (var (selector, y) in new[] { ("HEAD@{1}", 163.0), ("HEAD@{9}", 345.0), ("HEAD@{11}", 397.0), ("HEAD@{17}", 553.0) })
        {
            var mark = app.LostMarkBounds(selector);
            Assert.Equal(y, mark.Center.Y, 0.5);
            AssertDrawnIn(shot, mark, Dark.Warning, $"{selector}'s lost mark", IconColourTolerance);
        }

        Assert.Equal((HeadFooter, (Rect?)FooterBounds), (page.Footer, page.FooterBounds));
        Assert.Equal(HeadFooter, app.Find<TrimmedTextBlock>("ReflogFooterText").Text);
        Assert.Null(page.Selected);
        Assert.True(page.ListHasFocus, "The reflog's list does not have the keyboard focus.");
        Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "Opening the page changed the right panel.");
        foreach (var read in HeadReflogReads)
        {
            AssertCalled(app, calls, read);
        }

        Assert.Equal(refs, await RefsAsync(repo));
        AssertNoClippedText(app);

        // Step 3: the lost mark's tooltip.
        var lostMark = app.LostMarkBounds("HEAD@{1}").Center;
        Assert.Equal(new Point(682, 163), lostMark);
        Assert.Equal(LostToolTip, await app.RestPointerAtAsync(lostMark));
        Shot(app, "b8c-lost-tooltip", keepPointer: true);
        AssertToolTipShown(app, LostToolTip);

        // Step 4: HEAD@{9}, d07c677, a lost commit: the right panel shows it, the graph selects nothing.
        var row9 = app.ReflogRowPoint("HEAD@{9}");
        Assert.Equal(PageRowPoint(8), row9);
        app.Click(row9);
        await AssertDetailsAsync(app, TryAShortcut, -1);
        Assert.Equal("HEAD@{9}", app.ReflogSnapshot().Selected?.Selector);

        // Step 5: its menu; Show in graph is disabled.
        app.RightClick(row9);
        Assert.Equal(NotInTheGraph, await app.RestPointerOnMenuItemAsync(ShowInGraph));
        shot = Shot(app, "b8d-entry-menu", keepPointer: true);
        AssertMenuAsReported(app, app.Menu(), ShortcutEntryMenu, CreateBranchHere, EntryMenuHeight, shot);
        AssertToolTipShown(app, NotInTheGraph);

        // Step 6: Create branch here…: phase 3's name dialog.
        app.ClickMenuItem(CreateBranchHere);
        await AssertNameDialogAsync(app);
        Shot(app, "b8e-name-dialog");

        // Step 7: shortcut, not checked out.
        var reads = app.ReflogSnapshot().ReadsShown;
        var mark7 = app.LogMark();
        calls = app.GitCalls().Count;
        app.TypeInto("NameBox", ShortcutName);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "NameDialogOption"));
        Assert.False(((CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption")).IsChecked);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertOperationDoneAsync(app, repo, CreateShortcut, mark7);
        await app.WaitForReflogReadAsync(reads);
        Shot(app, "b8f-branch-created");
        AssertNoClippedText(app);
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, CreateShortcutCommand);
        Assert.Equal(ShortcutSha, await repo.RefTargetAsync("refs/heads/shortcut"));
        Assert.Equal([ShortcutReflog], Split((await repo.GitAsync("log", "--walk-reflogs", "--format=%gs", "refs/heads/shortcut")).StandardOutput));
        Assert.Equal("## main...origin/main [ahead 2]", Split((await repo.GitAsync("status", "--porcelain", "--branch")).StandardOutput).Single());
        Assert.Equal(headLog, HeadReflogLines(repo));
        AssertGraph(app, RLB8, ReflogLanes, showsDate: true);
        var (branches, branchRows) = Section(app, "Local branches");
        Assert.Equal(5, branches);
        Assert.Equal(LocalBranchesAfterShortcut, branchRows);
        page = app.ReflogSnapshot();
        Assert.Equal(HeadReflog.Count, page.Rows.Count);
        Assert.Equal(HeadLostAfterShortcut, page.Rows.Where(row => row.IsLost).Select(row => row.Selector));
        Assert.Equal(HeadFooter, page.Footer);
        await AssertReflogRowsAsync(app, page, [.. HeadReflog.Select(row => row.Selector == "HEAD@{9}" ? row with { Lost = false } : row)]);

        // Step 8: the graph has d07c677 now: a click selects it behind the page.
        row9 = app.ReflogRowPoint("HEAD@{9}");
        Assert.Equal(PageRowPoint(8), row9);
        app.Click(row9);
        await AssertDetailsAsync(app, TryAShortcut, 1);

        // Step 9: the source menu, then main's reflog.
        page = app.ReflogSnapshot();
        app.Click(page.SourceBounds.Center);
        menu = app.Menu();
        shot = Shot(app, "b8g-source-menu");
        AssertMenuAsReported(app, menu, SourceMenu, "HEAD", SourceMenuHeight, shot);
        Assert.Equal(page.SourceBounds.BottomLeft, menu.Bounds.TopLeft, PointWithin(2));
        app.ClickMenuItem("main");
        page = await WaitForReflogAsync(app, ReflogOf("main"), "main", MainReflog.Count, page.ReadsShown);
        Shot(app, "b8h-main-reflog");
        AssertNoClippedText(app);
        AssertReflogHeader(app, page, ReflogOf("main"), "main");
        await AssertReflogRowsAsync(app, page, MainReflog);
        Assert.Equal(["main@{3}"], page.Rows.Where(row => row.IsLost).Select(row => row.Selector));
        Assert.Equal(((string?)null, (Rect?)null), (page.Footer, page.FooterBounds));
        await AssertDetailsAsync(app, TryAShortcut, 1);

        // Step 10: topic's reflog from its menu in the left panel; the right-click selects nothing there (D114).
        var selectedBefore = SelectedLeftRows(app);
        app.RightClick(await app.RefRowAsync("Local branches", "topic"));
        var lines = app.Menu().Lines().ToList();
        Assert.Contains(ShowReflogOfTopic, lines);
        Assert.True(lines.IndexOf(ShowReflogOfTopic) < lines.IndexOf("Copy branch name"), "Show reflog of topic is not before Copy branch name.");
        Assert.True(app.Menu().Item(ShowReflogOfTopic).IsEnabled, "Show reflog of topic is disabled.");
        Assert.Equal(selectedBefore, SelectedLeftRows(app));
        app.ClickMenuItem(ShowReflogOfTopic);
        page = await WaitForReflogAsync(app, ReflogOf("topic"), "topic", TopicReflog.Count);
        Shot(app, "b8i-topic-reflog");
        AssertNoClippedText(app);
        await AssertReflogRowsAsync(app, page, TopicReflog);
        Assert.Equal(selectedBefore, SelectedLeftRows(app));
        await AssertDetailsAsync(app, TryAShortcut, 1);

        // Step 11: Esc: the graph with shortcut's row selected and in view.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        await app.WaitForAsync(() => Graph(app).IsFocused, "the graph to take the focus");
        shot = Shot(app, "b8j-back-to-graph");
        AssertNoClippedText(app);
        AssertGraph(app, RLB8, ReflogLanes, showsDate: true, picture: shot);
        AssertRowWhollyInView(app, 1, 155);
        AssertColour(Dark.Selection, shot, new Point(PageRowX + 100, 155 + 8), "shortcut's row background");
        await AssertDetailsAsync(app, TryAShortcut, 1);
        Assert.Equal(ShortcutSha, await repo.RefTargetAsync("refs/heads/shortcut"));
        var refsNow = await RefsAsync(repo);
        Assert.Equal(refs.Append("refs/heads/shortcut " + ShortcutSha).Order(StringComparer.Ordinal), refsNow.Order(StringComparer.Ordinal));
    }

    /// <summary>HEAD's reflog file's lines (<c>.git/logs/HEAD</c>), null entries included: "HEAD's reflog still 36 entries".</summary>
    private static List<string> HeadReflogLines(TempRepo repo) =>
        [.. File.ReadAllLines(Path.Combine(repo.Path, ".git", "logs", "HEAD"))];

    /// <summary>The left panel's selected rows, by key.</summary>
    private static List<string> SelectedLeftRows(ShellDriver app)
    {
        app.Settle();
        return [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key)];
    }

    /// <summary>
    /// Asserts phase 3's name dialog from d07c677 (2.4): <c>Create branch</c>, its context, the
    /// empty name box with its placeholder and the keyboard focus, <c>Check out the new branch</c>
    /// ticked, <c>Cancel</c> and <c>Create branch</c> disabled.
    /// </summary>
    private static async Task AssertNameDialogAsync(ShellDriver app)
    {
        await app.WaitForDialogAsync<NameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal(CreateBranchTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal(ShortcutContext, ((TextBlock)app.FindByAutomationId(view, "NameDialogContext")).Text);
        var name = (TextBox)app.FindByAutomationId(view, "NameBox");
        Assert.True(string.IsNullOrEmpty(name.Text), $"The name box holds \"{name.Text}\".");
        Assert.Equal(BranchNamePlaceholder, name.PlaceholderText);
        await app.WaitForAsync(() => name.IsFocused, "the name box to have the keyboard focus");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((CheckOutTheNewBranch, (bool?)true), (option.Content as string, option.IsChecked));
        Assert.Equal("Cancel", ((Button)app.FindByAutomationId(view, "CancelButton")).Content);
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal((CreateBranchTitle, false), (confirm.Content as string, confirm.IsEffectivelyEnabled));
    }
}
