// Part A, checks 23 and 24 (docs/test-reports/phase-3.md): the toolbar with a repository (its
// enabled states, the disabled buttons' tooltips, Branch, Stash and Pop) and the left panel's
// selection following the graph's (D98).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksA
{
    /// <summary>Check 23, steps 1 to 4: the toolbar on ops; Branch, Stash and Pop (ops).</summary>
    [AvaloniaFact]
    public async Task Check_23_toolbar_branch_stash_pop()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);

        // Step 1: the toolbar with a clean ops repository; the pointer rests on Stash.
        Shot(app, "23a-toolbar-with-repository");
        AssertToolbar(app, new Dictionary<string, string> { ["StashButton"] = PartA.NoChangesToStash });
        AssertStatus(app, "Ready");
        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "StashButton"));
        Shot(app, "23b-stash-disabled-tooltip", keepPointer: true);
        Assert.Equal(PartA.NoChangesToStash, tip);
        Assert.True(ToolTip.GetIsOpen(ToolbarButton(app, "StashButton")));

        // Step 2: Branch: the name dialog from HEAD; "topic/new" created and checked out.
        app.Click(ToolbarButton(app, "BranchButton"));
        await AssertNameDialogAsync(app, new NameDialogExpected(
            "Create branch", "From 88d6b91 \"Main work\"", "Branch name", null, false, "Check out the new branch", true, "Create branch", false));
        Shot(app, "23c-branch-dialog");
        app.Type("topic/new");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => repository.OperationStatus.Text == PartA.BranchCreatedMessage, $"\"{PartA.BranchCreatedMessage}\"");
        await app.WaitForIdleAsync();
        Shot(app, "23d-branch-created");
        Assert.Contains(app.GitCalls(), call => call.CommandText == "git switch --create topic/new " + PartA.MainWorkSha);
        Assert.Equal("topic/new", await GitTextAsync(repo, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal(PartA.MainWorkSha, await repo.HeadAsync());
        Assert.Equal("## topic/new", (await GitLinesAsync(repo, "status", "-sb"))[0]);
        Assert.StartsWith("88d6b91 Main work [CurrentBranch:topic/new, LocalBranch:main, ", GraphLines(app)[PartA.MainWorkRow], StringComparison.Ordinal);
        AssertBranch(app, "topic/new");
        AssertStatus(app, PartA.BranchCreatedMessage);
        Assert.Equal(["▾ Local branches 5", "  ▸ feature", "  ▾ topic", "    new (head)", "  main"], LeftPanelLines(app).Take(5));
        Assert.Equal(PaletteOf(app).Accent, await RefNameColourAsync(app, "Local branches", "topic", "new"));
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PullButton"] = PartA.TopicNoUpstream,
            ["StashButton"] = PartA.NoChangesToStash,
        });

        // Step 3: changes made outside the app; Stash with the dialog.
        repo.WriteFile("README.md", PartA.ToolbarReadme).WriteFile("scratch.txt", PartA.Scratch);
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow, "the working-changes row", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        AssertToolbar(app, new Dictionary<string, string> { ["PullButton"] = PartA.TopicNoUpstream });
        app.Click(ToolbarButton(app, "StashButton"));
        await AssertNameDialogAsync(app, new NameDialogExpected(
            "Stash changes", null, null, "Message (optional)", false, "Include untracked files", true, "Stash", true));
        var message = (TextBox)app.FindByAutomationId("MessageBox");
        Assert.False(message.AcceptsReturn);
        Assert.True(message.Bounds.Height < 40, $"The stash message box is {message.Bounds.Height} high, not one line.");
        Shot(app, "23e-stash-dialog");
        app.Type("Toolbar stash");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => repository.OperationStatus.Text == "Stashed the changes", "\"Stashed the changes\"");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => !Graph(app).ShowsWorkingRow && repository.Graph.Count == 9, "the stash in the graph and no working-changes row");
        Shot(app, "23f-stashed");
        Assert.Contains(app.GitCalls(), call => call.CommandText.StartsWith("git stash push --include-untracked --message ", StringComparison.Ordinal) && call.CommandText.Contains("Toolbar stash", StringComparison.Ordinal));
        Assert.Equal(["stash@{0}: On topic/new: Toolbar stash", "stash@{1}: On main: Experiment"], await GitLinesAsync(repo, "stash", "list"));
        Assert.Empty(await GitTextAsync(repo, "status", "--porcelain", "--untracked-files=all"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")));
        AssertStatus(app, "Stashed the changes");
        Assert.Contains("▾ Stashes 2", LeftPanelLines(app));
        AssertGraph(app, PartA.StashedGraph);
        Assert.Equal("2026-01-02T09:00:00+00:00", await GitTextAsync(repo, "log", "-1", "--format=%aI", "stash@{0}"));
        Assert.Equal(WindowLayout.GraphColumnWidth(4), Graph(app).Columns.Graph.Width);
        Assert.False(Graph(app).Columns.Date.IsVisible);
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PullButton"] = PartA.TopicNoUpstream,
            ["StashButton"] = PartA.NoChangesToStash,
        });

        // Step 4: Pop: the stash found by its id, popped; the files back.
        app.Click(ToolbarButton(app, "PopButton"));
        await app.WaitForAsync(() => repository.OperationStatus.Text == "Popped stash@{0}", "\"Popped stash@{0}\"");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow && repository.Graph.Count == 8, "the working-changes row and the ops graph");
        var shot = Shot(app, "23g-popped");
        Assert.Contains(app.GitCalls(), call => call.CommandText.StartsWith("git stash pop", StringComparison.Ordinal) && call.CommandText.EndsWith("stash@{0}", StringComparison.Ordinal));
        Assert.Equal(["stash@{0}: On main: Experiment"], await GitLinesAsync(repo, "stash", "list"));
        Assert.Equal([" M README.md", "?? scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(PartA.ToolbarReadme, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal(PartA.Scratch, File.ReadAllText(Path.Combine(repo.Path, "scratch.txt")));
        AssertStatus(app, "Popped stash@{0}");
        AssertGraph(app, PartA.PoppedGraph);
        Assert.True(Graph(app).Columns.Date.IsVisible);
        Assert.Equal(Graph(app).WorkingRowBounds.Bottom, Graph(app).RowBounds(0).Top, 0.5);
        AssertWorkingRingOnly(app, shot, 0, PaletteOf(app).Lane(0));
        AssertToolbar(app, new Dictionary<string, string> { ["PullButton"] = PartA.TopicNoUpstream });
    }

    /// <summary>Check 23, step 5: an unborn repository with a remote and an untracked file.</summary>
    [AvaloniaFact]
    public async Task Check_23_unborn_repository()
    {
        using var repo = await TempRepo.CreateAsync("unborn");
        await repo.AddBareRemoteAsync("origin");
        repo.WriteFile("notes.txt", "notes");
        using var data = new TempDirectory("data");
        using var app = await OpenUnbornAsync(data, repo);

        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "BranchButton"));
        var shot = Shot(app, "23h-unborn-branch-tooltip", keepPointer: true);
        Assert.Equal(PartA.MakeFirstCommit, tip);
        var graph = Graph(app);
        Assert.True(graph.ShowsWorkingRow);
        Assert.Equal(0, Repo(app).Graph.Count);
        Assert.True(app.Find<TextBlock>("NoCommitsTitle").IsEffectivelyVisible);
        Assert.Equal("No commits yet", app.Find<TextBlock>("NoCommitsTitle").Text);
        AssertWorkingRingOnly(app, shot, 0, PaletteOf(app).Lane(0), dashed: false);
        AssertBranch(app, "main");
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PullButton"] = PartA.MainNoUpstream,
            ["PushButton"] = PartA.MakeFirstCommit,
            ["BranchButton"] = PartA.MakeFirstCommit,
            ["StashButton"] = PartA.MakeFirstCommit,
            ["PopButton"] = PartA.NoStashes,
        });

        var pointer = new Point(PartA.UnbornAreaPoint.X, PartA.UnbornAreaPoint.Y);
        app.RightClick(pointer);
        var menu = app.Menu();
        shot = Shot(app, "23i-unborn-empty-area-menu");
        AssertMenu(menu, PartA.UnbornEmptyAreaMenu);
        AssertHighlighted(menu, "Fetch all remotes");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
    }

    /// <summary>Check 23, step 6: the linear scenario, without remotes.</summary>
    [AvaloniaFact]
    public async Task Check_23_no_remotes()
    {
        using var repo = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "PushButton"));
        Shot(app, "23j-no-remotes-push-tooltip", keepPointer: true);
        Assert.Equal(PartA.AddRemoteFirst, tip);
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["FetchButton"] = PartA.NoRemotes,
            ["PullButton"] = PartA.MainNoUpstream,
            ["PushButton"] = PartA.AddRemoteFirst,
            ["StashButton"] = PartA.NoChangesToStash,
            ["PopButton"] = PartA.NoStashes,
        });
    }

    /// <summary>Check 23, step 7: the edges scenario, HEAD detached.</summary>
    [AvaloniaFact]
    public async Task Check_23_detached_head()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "PullButton"));
        Shot(app, "23k-detached-pull-tooltip", keepPointer: true);
        Assert.Equal(PartA.DetachedPull, tip);
        AssertBranch(app, "Detached at 419190f");
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PullButton"] = PartA.DetachedPull,
            ["PushButton"] = PartA.DetachedPush,
            ["StashButton"] = PartA.NoChangesToStash,
            ["PopButton"] = PartA.NoStashes,
        });
    }

    /// <summary>Check 23, step 8: the edges scenario with loc checked out, whose upstream is local.</summary>
    [AvaloniaFact]
    public async Task Check_23_local_upstream()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("loc");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "PushButton"));
        Shot(app, "23l-local-upstream-push-tooltip", keepPointer: true);
        Assert.Equal(PartA.LocTracksLocal, tip);
        AssertBranch(app, "loc");
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PushButton"] = PartA.LocTracksLocal,
            ["StashButton"] = PartA.NoChangesToStash,
            ["PopButton"] = PartA.NoStashes,
        });
    }

    /// <summary>Check 23, step 9: a merge stopped by a conflict.</summary>
    [AvaloniaFact]
    public async Task Check_23_merge_in_progress()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.NotNull(app.BannerBounds);
        var tip = await app.RestPointerOnAsync(ToolbarButton(app, "PullButton"));
        Shot(app, "23m-merge-in-progress-pull-tooltip", keepPointer: true);
        Assert.Equal(PartA.FinishMerge, tip);
        AssertToolbar(app, new Dictionary<string, string>
        {
            ["PullButton"] = PartA.FinishMerge,
            ["StashButton"] = PartA.FinishMerge,
        });
    }

    /// <summary>Check 24: the left panel's highlighted ref follows the graph's selection (D98; ops).</summary>
    [AvaloniaFact]
    public async Task Check_24_left_panel_selection_follows_the_graph()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);

        // Step 1: a tag selects its commit and is highlighted.
        app.Click(await app.RefRowAsync("Tags", "v1.0"));
        await WaitForDetailsAsync(app, "Add core");
        var shot = Shot(app, "24a-ref-clicked");
        AssertRowSelected(app, PartA.AddCoreRow, shot);
        await AssertRefHighlightedAsync(app, "Tags", "v1.0");

        // Step 2: a tag added outside the app; the reload keeps the selection and the highlight.
        await repo.TagAsync("v1.2", "4ef1f7c");
        await app.WaitForAsync(() => ((VisualCommit.App.ViewModels.Panels.RefSectionRow)app.RefRowModel("Tags")).Count == 3, "Tags to show 3", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        shot = Shot(app, "24b-kept-after-reload");
        Assert.Equal(["▾ Tags 3", "  v1.0 (selected)", "  v1.1", "  v1.2"], LeftPanelLines(app).SkipWhile(line => !line.StartsWith("▾ Tags", StringComparison.Ordinal)).Take(4));
        Assert.Equal("4ef1f7c Initial commit [Tag:v1.2] 0/0", GraphLines(app)[PartA.InitialCommitRow]);
        AssertRowSelected(app, PartA.AddCoreRow, shot);
        await AssertRefHighlightedAsync(app, "Tags", "v1.0");

        // Step 3: a click in the graph highlights no ref, also where refs point at the commit.
        app.Click(app.GraphRowPoint(PartA.MainWorkRow));
        await WaitForDetailsAsync(app, "Main work");
        shot = Shot(app, "24c-graph-click-clears");
        AssertRowSelected(app, PartA.MainWorkRow, shot);
        await AssertRefHighlightedAsync(app, null);

        // Step 4: main highlighted; a click on its already selected row keeps it; Down clears it.
        app.Click(await app.RefRowAsync("Local branches", "main"));
        await AssertRefHighlightedAsync(app, "Local branches", "main");
        app.Click(app.GraphRowPoint(PartA.MainWorkRow));
        AssertRowSelected(app, PartA.MainWorkRow);
        await AssertRefHighlightedAsync(app, "Local branches", "main");
        app.PressKey(Key.Down);
        await WaitForDetailsAsync(app, "Alpha two");
        shot = Shot(app, "24d-key-clears");
        AssertRowSelected(app, PartA.AlphaTwoRow, shot);
        await AssertRefHighlightedAsync(app, null);

        // Step 5: the last clicked ref has the highlight.
        app.Click(await app.RefRowAsync("Tags", "v1.1"));
        await AssertRefHighlightedAsync(app, "Tags", "v1.1");
        app.Click(await app.RefRowAsync("Local branches", "main"));
        await WaitForDetailsAsync(app, "Main work");
        shot = Shot(app, "24e-last-clicked-ref");
        AssertRowSelected(app, PartA.MainWorkRow, shot);
        await AssertRefHighlightedAsync(app, "Local branches", "main");

        // Step 6: the parent link of "Add core" selects "Initial commit" and clears the highlight.
        app.Click(await app.RefRowAsync("Tags", "v1.0"));
        await WaitForDetailsAsync(app, "Add core");
        var parent = (Control)app.FindByAutomationId("ParentLink");
        Assert.Contains("4ef1f7c", TextsIn(parent));
        app.Click(parent);
        await WaitForDetailsAsync(app, "Initial commit");
        shot = Shot(app, "24f-parent-link-clears");
        AssertRowSelected(app, PartA.InitialCommitRow, shot);
        await AssertRefHighlightedAsync(app, null);

        // Step 7: v1.0, then Ctrl+click "Initial commit": two commits selected, nothing highlighted.
        app.Click(await app.RefRowAsync("Tags", "v1.0"));
        await AssertRefHighlightedAsync(app, "Tags", "v1.0");
        app.Click(app.GraphRowPoint(PartA.InitialCommitRow), app.CommandModifier);
        await app.WaitForAsync(() => app.FindByAutomationId("MultiSelectionView").IsEffectivelyVisible, "the multi-selection view");
        Shot(app, "24g-multi-selection-clears");
        Assert.Equal(2, repository.Selection.Count);
        Assert.Equal("2 commits selected", ((TextBlock)app.FindByAutomationId("MultiSelectionTitle")).Text);
        await AssertRefHighlightedAsync(app, null);

        // Step 8: the stash in the left panel, then its row in the graph: it stays highlighted.
        app.Click(await app.RefRowAsync("Stashes", "On main: Experiment"));
        await AssertRefHighlightedAsync(app, "Stashes", "On main: Experiment");
        app.Click(app.GraphRowPoint(PartA.StashRow));
        await WaitForDetailsAsync(app, "On main: Experiment");
        shot = Shot(app, "24h-stash-kept");
        AssertRowSelected(app, PartA.StashRow, shot);
        await AssertRefHighlightedAsync(app, "Stashes", "On main: Experiment");
    }

    /// <summary>
    /// Opens the app on an unborn repository: the default start's wait, without the graph's rows,
    /// which an unborn repository has none of; its status read and the queue idle.
    /// </summary>
    private static async Task<ShellDriver> OpenUnbornAsync(TempDirectory data, TempRepo repo)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        try
        {
            await app.WaitUntilReadyAsync();
            await app.WaitForAsync(
                () => app.Session.Shell.ActiveTab.Repository is { HasNoCommits: true } repository
                    && app.CountLogLines(AppLogFile.WorkingTreeRead(repository.Name)) > 0,
                "the unborn repository to open");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The working-changes row's ring in <paramref name="lane"/>, with the dashed line below it or, for an unborn HEAD, none.</summary>
    private static void AssertWorkingRingOnly(ShellDriver app, Screenshot screenshot, int lane, Avalonia.Media.Color colour, bool dashed = true)
    {
        var graph = Graph(app);
        var centre = graph.TranslatePoint(new Point(graph.LaneCenterX(lane), graph.WorkingRowBounds.Center.Y), app.Window)!.Value;
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The working-changes ring is not {colour} anywhere from 3 to 5 right of {centre}: {string.Join(", ", samples)}.");
        Assert.NotEqual(colour, screenshot.PixelAt(centre));
        var below = screenshot.PixelAt(new Point(centre.X - 1, centre.Y + 6));
        Assert.True(dashed == (below == colour), $"Below the ring at {centre} the pixel is {below}: {(dashed ? "no" : "a")} dashed line.");
    }
}
