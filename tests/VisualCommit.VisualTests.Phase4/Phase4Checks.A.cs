// Part A of phase 4's scripted walk-through (docs/test-reports/phase-4.md, "Part A: rebase from
// the menus and by drag, pull with rebase"): checks A1 to A5 of design 13.4. This class is part
// A's alone; its expected texts are in Phase4Expectations.A.cs (Phase4Expectations.PartA). One
// test method per app start, named as part A's conventions name them. This file holds A1 (the new
// menu items); Phase4Checks.A.Rebase.cs A2 and A3, Phase4Checks.A.Pull.cs A4,
// Phase4Checks.A.Refused.cs A5, and Phase4Checks.A.Helpers.cs what they share.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>The checks of part A: rebase from the menus and by drag, and pull with rebase.</summary>
public sealed partial class Phase4ChecksA : Phase4Checks
{
    /// <summary>
    /// Check A1: the new menu items of a commit not on HEAD, a commit on HEAD's line, HEAD's own
    /// commit, a local and two remote branches, the current branch with "Pull … (rebase)", and
    /// several commits: a chain that can be squashed and three selections that cannot ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A1_new_menu_items()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);

        // At the start: R0, its left panel, nothing selected, the branch feature/topic. With no
        // banner, row r's centre is at y 129 + 26 r and the Message column starts at x 454 (three
        // lanes): where the real-window pass right-clicks.
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, showsDate: true);
        await AssertLeftPanelAsync(app, PartA.R0LeftPanel);
        AssertNothingSelected(app);
        AssertBranch(app, "feature/topic");
        Assert.Equal(WindowLayout.RowCentreY(PartA.MainMoreRow), app.GraphRowPoint(PartA.MainMoreRow).Y, 0.5);
        Assert.Equal(WindowLayout.MessageColumnX(PartA.RebaseLanes) + WindowLayout.MessageClickInset, app.GraphRowPoint(PartA.MainMoreRow).X, 0.5);

        // Step 1: "Main more", not on HEAD: the pointer rests on the disabled "from here".
        var mark = app.LogMark();
        var (menu, _) = RightClickCommit(app, PartA.MainMoreRow);
        var tip = await app.RestPointerOnMenuItemAsync("Interactive rebase of feature/topic from here…");
        var shot = Shot(app, "a1a-commit-not-on-head", keepPointer: true);
        Assert.Equal(PartA.NotOnTopicReason, tip);
        Assert.True(ToolTip.GetIsOpen(app.MenuItemControl("Interactive rebase of feature/topic from here…")));
        AssertToolTipInsideWindow(app, PartA.NotOnTopicReason);
        AssertRowSelected(app, PartA.MainMoreRow);
        menu = app.Menu();
        AssertMenuAsReported(app, menu, PartA.MainMoreCommitMenu, "Check out this commit", PartA.CommitMenuHeight, shot);
        Assert.Single(app.LogLinesSince(mark), line => AppLogFile.MenuOpened(PartA.RebaseRepo).IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => AppLogFile.MenuOpened(PartA.RebaseRepo, "1dd58b8 Main more", "pointer").IsMatch(line));
        await CloseMenuAsync(app);

        // Step 2: "Topic one", on HEAD's linear line: the pointer rests on the disabled rebase.
        (menu, _) = RightClickCommit(app, PartA.TopicOneRow);
        tip = await app.RestPointerOnMenuItemAsync("Rebase feature/topic onto this commit");
        shot = Shot(app, "a1b-commit-on-head-line", keepPointer: true);
        Assert.Equal(PartA.AlreadyOnTopicReason, tip);
        AssertToolTipInsideWindow(app, PartA.AlreadyOnTopicReason);
        AssertRowSelected(app, PartA.TopicOneRow);
        menu = app.Menu();
        AssertMenuAsReported(app, menu, PartA.TopicOneCommitMenu, "Check out this commit", PartA.CommitMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 3: "Topic two", HEAD's own commit.
        (menu, _) = RightClickCommit(app, PartA.TopicTwoRow);
        shot = Shot(app, "a1c-head-commit");
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertMenuAsReported(app, menu, PartA.TopicTwoCommitMenu, "Check out this commit", PartA.CommitMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 4: the local branch main in the left panel: its menu only (D114).
        (menu, _) = await RightClickRefAsync(app, "Local branches", "main");
        shot = Shot(app, "a1d-local-branch");
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertNoRefHighlighted(app);
        AssertMenuAsReported(app, menu, PartA.MainBranchMenu, "Check out main", PartA.LocalBranchWithUpstreamMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 5: the remote branch origin/main.
        (menu, _) = await RightClickRefAsync(app, "Remotes", "origin", "main");
        shot = Shot(app, "a1e-remote-branch");
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertNoRefHighlighted(app);
        AssertMenuAsReported(app, menu, PartA.OriginMainMenu, "Check out origin/main", PartA.RemoteBranchMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 6: the remote folder feature opened; its topic, HEAD's upstream.
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        await app.WaitForIdleAsync();
        Assert.True(app.RefRowModel("Remotes", "origin", "feature").IsOpen, "The remote folder feature did not open.");
        Assert.Contains("      topic", LeftPanelLines(app));
        (menu, _) = await RightClickRefAsync(app, "Remotes", "origin", "feature", "topic");
        AssertMenuAsReported(app, menu, PartA.OriginFeatureTopicMenu, "Merge origin/feature/topic into feature/topic", PartA.RemoteBranchMenuHeight);
        await CloseMenuAsync(app);

        // Step 7: the current branch feature/topic: "Pull … (rebase)", no rebase item.
        (menu, _) = await RightClickRefAsync(app, "Local branches", "feature", "topic");
        shot = Shot(app, "a1f-current-branch");
        AssertNoRefHighlighted(app);
        AssertMenuAsReported(app, menu, PartA.FeatureTopicMenu, "Pull from origin/feature/topic", PartA.CurrentBranchWithUpstreamMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 8: "Topic two" and "Topic one", a chain on HEAD's line: Squash enabled.
        app.Click(app.GraphRowPoint(PartA.TopicTwoRow));
        app.Click(app.GraphRowPoint(PartA.TopicOneRow), app.CommandModifier);
        (menu, _) = RightClickCommit(app, PartA.TopicOneRow);
        await AssertMultiSelectionAsync(app, "b0dfa3f Topic two", "1219e45 Topic one");
        shot = Shot(app, "a1g-squash-enabled");
        AssertRowsSelected(app, PartA.TopicTwoRow, PartA.TopicOneRow);
        AssertMenuAsReported(app, menu, PartA.ChainMenu, "Squash 2 commits…", PartA.SeveralCommitsMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 9: "Topic two" and "Add core", no chain: the pointer rests on the disabled Squash.
        app.Click(app.GraphRowPoint(PartA.TopicOneRow), app.CommandModifier);
        app.Click(app.GraphRowPoint(PartA.AddCoreRow), app.CommandModifier);
        (menu, _) = RightClickCommit(app, PartA.AddCoreRow);
        await AssertMultiSelectionAsync(app, "b0dfa3f Topic two", "996fdc1 Add core");
        tip = await app.RestPointerOnMenuItemAsync("Squash 2 commits…");
        shot = Shot(app, "a1h-squash-not-a-chain", keepPointer: true);
        Assert.Equal(PartA.NotAChainReason, tip);
        AssertToolTipInsideWindow(app, PartA.NotAChainReason);
        AssertRowsSelected(app, PartA.TopicTwoRow, PartA.AddCoreRow);
        menu = app.Menu();
        AssertMenuAsReported(app, menu, PartA.NotAChainMenu, "Revert 2 commits", PartA.SeveralCommitsMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 10: "Main more" and "Main work", not on HEAD.
        app.Click(app.GraphRowPoint(PartA.MainMoreRow));
        app.Click(app.GraphRowPoint(PartA.MainWorkRow), app.CommandModifier);
        (menu, _) = RightClickCommit(app, PartA.MainWorkRow);
        AssertRowsSelected(app, PartA.MainMoreRow, PartA.MainWorkRow);
        AssertMenuAsReported(app, menu, PartA.NotOnHeadMenu, "Cherry-pick 2 commits onto feature/topic", PartA.SeveralCommitsMenuHeight);
        await CloseMenuAsync(app);

        // Step 11: "Add core" and "Initial commit", the root among them.
        app.Click(app.GraphRowPoint(PartA.AddCoreRow));
        app.Click(app.GraphRowPoint(PartA.InitialCommitRow), app.CommandModifier);
        (menu, _) = RightClickCommit(app, PartA.InitialCommitRow);
        AssertRowsSelected(app, PartA.AddCoreRow, PartA.InitialCommitRow);
        AssertMenuAsReported(app, menu, PartA.RootMenu, "Revert 2 commits", PartA.SeveralCommitsMenuHeight);
        await CloseMenuAsync(app);

        // Git's state is unchanged throughout: the refs as built, HEAD on feature/topic, no backup.
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(PartA.TopicTwoSha, await repo.HeadAsync());
        Assert.Empty(await BackupsAsync(repo));
        await AssertNoDialogAsync(app);
    }
}
