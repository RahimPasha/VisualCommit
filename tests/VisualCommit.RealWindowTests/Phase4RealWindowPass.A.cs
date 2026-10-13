// Part A's real-window steps (docs/test-reports/phase-4.md, part A, the "Real window" column of A1
// to A5): A1 step 1, A2 part A (steps 1 to 3) and A3 step 4, each in a launch of its own on a fresh
// "rebase" copy at 1100×700, dark. A4 and A5 are "n/a" (the report gives the reasons). Graph rows are
// right-clicked at the row constants (no banner: row r's centre at y 129 + 26 r, the Message column
// at x 454 with three lanes); the labels of A3's drag at the centres the scripted run measured
// (Phase4Expectations.PartA); menus are found by ContextMenu and their items by ContextMenuItem.
using System.Drawing;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase4RealWindowPass
{
    /// <summary>
    /// Check A1, step 1, with the real mouse: the right-click on "Main more" (not on HEAD) in its
    /// Message column, the commit menu with its rebase items (284 high, "Check out this commit"
    /// highlighted), the pointer brought onto the disabled "Interactive rebase of feature/topic from
    /// here…" by the conventions' path, its reason's tooltip drawn inside the window
    /// (<c>a1a-commit-not-on-head</c>), then Esc. Git's state does not change.
    /// </summary>
    [Fact]
    public async Task Check_A1_commit_menu_not_on_head()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        Assert.Equal(PartA.RebaseRepo, name);
        var before = await StateAsync(repo);

        var mainMore = RowPoint(app, PartA.MainMoreRow, PartA.RebaseLanes);
        var mark = app.LogMark();
        app.RightClickAt(mainMore.X, mainMore.Y);
        AssertMenu(app, PartA.MainMoreCommitMenu, PartA.CommitMenuHeight);
        WaitForDetails(app, "Main more");
        AssertFocusedItem(app, "Check out this commit");
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened(name).IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened(name, "1dd58b8 Main more", "pointer").IsMatch(line));

        var tip = app.RestPointerOnMenuItem("Interactive rebase of feature/topic from here…", PartA.NotOnTopicReason);
        AssertToolTipInside(app, tip, PartA.NotOnTopicReason);
        AssertFocusedItem(app, "Check out this commit");
        Compare(app, "a1a-commit-not-on-head", keepPointer: true);
        CloseMenuWithEscape(app);

        Assert.Equal(before, await StateAsync(repo));
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(PartA.TopicTwoSha, await repo.HeadAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check A2, part A, with the real mouse: the graph before (<c>a2a-before</c>); main's menu in
    /// the left panel and "Rebase feature/topic onto main" clicked by its ContextMenuItem, the
    /// queue's log line awaited, the graph after (<c>a2b-rebased-onto-main</c>); row 0 clicked, the
    /// rewritten tip's details (<c>a2c-rebased-commit-details</c>); git's state asserted as in the
    /// scripted run. Part B is scripted only (the report: its menu is the commit menu A1 pictures,
    /// and its item starts the same operation).
    /// </summary>
    [Fact]
    public async Task Check_A2_rebase_onto_a_branch()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);

        // Step 1: R0, nothing selected, the details' placeholder, Ready.
        app.WaitForText("DetailsPlaceholder", PartA.NothingSelectedPlaceholder);
        Assert.Equal(PartA.Ready, app.OperationStatusText());
        Compare(app, "a2a-before");

        // Step 2: "Rebase feature/topic onto main" from main's menu: no question, no dialog after.
        app.RightClickWithMouse(app.RefRow("Local branches", "main"));
        AssertMenu(app, PartA.MainBranchMenu, PartA.LocalBranchWithUpstreamMenuHeight);
        AssertFocusedItem(app, "Check out main");
        var rebase = ExpectOperation(app, name, PartA.RebaseTopicOntoMainTitle);
        app.ClickMenuItem("Rebase feature/topic onto main");
        rebase.Wait(OperationOutcome.Succeeded, PartA.RebasedTopicOntoMain);
        WaitForStatus(app, PartA.RebasedTopicOntoMain);
        AssertNoDialog(app);
        app.WaitForText("DetailsPlaceholder", PartA.NothingSelectedPlaceholder);
        Compare(app, "a2b-rebased-onto-main");

        // Git: the two commits plain git makes, on main's tip; the backup of the old tip (1.2).
        Assert.Equal(["Topic two", "Topic one", "Main more", "Main work", "Add core", "Initial commit"], await GitLinesAsync(repo, "log", "--format=%s", "feature/topic"));
        Assert.Equal(PartA.TopicTwoOntoMainSha, await repo.RefTargetAsync("refs/heads/feature/topic"));
        await AssertCommitAsync(repo, PartA.TopicTwoOntoMainSha, ScenarioTime(3), AppCommitTime, PartA.TopicOneOntoMainSha);
        await AssertCommitAsync(repo, PartA.TopicOneOntoMainSha, ScenarioTime(2), AppCommitTime, PartA.MainMoreSha);
        Assert.Equal(["## feature/topic...origin/feature/topic [ahead 4, behind 2]"], await GitLinesAsync(repo, "status", "-sb"));
        AssertNoRebaseFolder(repo);
        Assert.Equal(PartA.TopicTwoSha, File.ReadAllText(Path.Combine(repo.Path, ".git", "ORIG_HEAD")).Trim());
        Assert.Equal(
            [
                "rebase (finish): returning to refs/heads/feature/topic",
                "rebase (pick): Topic two",
                "rebase (pick): Topic one",
                $"rebase (start): checkout {PartA.MainMoreSha}",
            ],
            await GitLinesAsync(repo, "log", "--walk-reflogs", "-4", "--format=%gs", "HEAD"));
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal(("rebase", (int?)null, false, "feature/topic"), (backup.Ref.Kind, backup.Ref.Number, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(PartA.TopicTwoSha, backup.Sha);
        Assert.Equal(PartA.MainMoreSha, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(PartA.MainMoreSha, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(PartA.OtherOneSha, await repo.RefTargetAsync("refs/heads/feature/other"));
        Assert.Equal(PartA.TopicTwoSha, await repo.RefTargetAsync("refs/remotes/origin/feature/topic"));
        Assert.Equal(
            [$"{PartA.TopicTwoSha}\trefs/heads/feature/topic", $"{PartA.MainMoreSha}\trefs/heads/main"],
            await GitLinesAsync(repo, "ls-remote", "--heads", "origin"));
        Assert.DoesNotContain(app.LogLines(), line => line.Contains("Removed backup", StringComparison.Ordinal));

        // Step 3: row 0, the rewritten tip: its details.
        var tipRow = RowPoint(app, 0, PartA.RebaseLanes);
        app.ClickAt(tipRow.X, tipRow.Y);
        WaitForDetails(app, "Topic two", PartA.TopicTwoOntoMainSha);
        Compare(app, "a2c-rebased-commit-details");

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check A3, step 4, with the real mouse: the press on the label feature/other (row 0), 5 moves
    /// with the button held, the release over the label main (row 1); the drop menu with
    /// "Check out feature/other and rebase it onto main" (<c>a3c-drop-other-onto-main</c>), then
    /// Esc. The button is released in a <c>finally</c>; nothing changes in git.
    /// </summary>
    [Fact]
    public async Task Check_A3_drop_of_another_branch_onto_main()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var app = Launch(repo);
        var before = await StateAsync(repo);

        var from = new PointF((float)PartA.OtherLabelCentre.X, (float)PartA.OtherLabelCentre.Y);
        var onto = new PointF((float)PartA.MainLabelCentre.X, (float)PartA.MainLabelCentre.Y);
        app.MouseDownAt(from.X, from.Y);
        var held = true;
        try
        {
            app.MoveHeldTo(onto.X, onto.Y, steps: PartA.DragSteps);
            held = false;
            app.MouseUpAt(onto.X, onto.Y);
        }
        finally
        {
            if (held)
            {
                app.MouseUpAt(onto.X, onto.Y);
            }
        }

        AssertMenu(app, PartA.OtherOntoMainDropMenu, PartA.DropWithRebaseMenuHeight);
        WaitForDetails(app, "Other one");
        AssertFocusedItem(app, "Check out main and merge feature/other into it");
        Compare(app, "a3c-drop-other-onto-main", keepPointer: true);
        CloseMenuWithEscape(app);

        Assert.Equal(before, await StateAsync(repo));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(0, app.Close());
    }
}
