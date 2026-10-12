// Part A, checks A2 and A3 (docs/test-reports/phase-4.md): a plain rebase from the branch menu
// and from the commit menu, and a rebase by drag with "Check out feature/other and rebase it onto
// main".
using Avalonia.Headless.XUnit;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksA
{
    /// <summary>
    /// Check A2, part A: "Rebase feature/topic onto main" from the branch menu: the graphs before
    /// and after, the status, the new commits and their dates, the backup ref and its target, and
    /// the details of the rewritten tip ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A2_rebase_onto_a_branch()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: R0, nothing selected, Ready.
        var shot = Shot(app, "a2a-before");
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        await AssertLeftPanelAsync(app, PartA.R0LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: true, "feature", "topic");
        AssertStatus(app, PartA.Ready);
        AssertBranch(app, "feature/topic");

        // Step 2: "Rebase feature/topic onto main" from main's menu: no question, no dialog.
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "main");
        AssertMenuAsReported(app, menu, PartA.MainBranchMenu, "Check out main", PartA.LocalBranchWithUpstreamMenuHeight);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using (var recorder = app.RecordStatusTexts())
        {
            app.ClickMenuItem("Rebase feature/topic onto main");
            await WaitForOperationAsync(app, repo, PartA.RebaseTopicOntoMainTitle, mark);
            Assert.Contains(PartA.RebasingTopicOntoMain, recorder.Texts);
        }

        shot = Shot(app, "a2b-rebased-onto-main");
        await AssertNoDialogAsync(app);
        var rebase = AssertCall(app, calls, PartA.RebaseOntoMainCommand, 0);
        await AssertGitPrintedAsync(rebase.StandardError, PartA.Ta3("feature/topic", 2), PartA.RebaseOntoMainCommand);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git rebase", StringComparison.Ordinal) && call != PartA.RebaseOntoMainCommand);
        AssertStatus(app, PartA.RebasedTopicOntoMain);
        AssertOperationLine(app, mark, PartA.RebaseRepo, PartA.RebaseTopicOntoMainTitle, OperationOutcome.Succeeded, PartA.RebasedTopicOntoMain);
        AssertNoBackupRemoved(app, mark);
        AssertGraph(app, PartA.R1, PartA.RebaseLanes, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        await AssertLeftPanelAsync(app, PartA.R1LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 4, behind: 2, head: true, "feature", "topic");
        AssertBranch(app, "feature/topic");

        // Git: the two commits plain git makes, on main's tip; the backup of the old tip.
        Assert.Equal(["Topic two", "Topic one", "Main more", "Main work", "Add core", "Initial commit"], await GitLinesAsync(repo, "log", "--format=%s", "feature/topic"));
        Assert.Equal(PartA.TopicTwoOntoMainSha, await RevParseAsync(repo, "refs/heads/feature/topic"));
        await AssertCommitAsync(repo, PartA.TopicTwoOntoMainSha, "Topic two", PartA.TopicOneOntoMainSha, ScenarioTime(3), AppCommitTime);
        await AssertCommitAsync(repo, PartA.TopicOneOntoMainSha, "Topic one", PartA.MainMoreSha, ScenarioTime(2), AppCommitTime);
        Assert.Equal(["## feature/topic...origin/feature/topic [ahead 4, behind 2]"], await GitLinesAsync(repo, "status", "-sb"));
        AssertNoRebaseFolder(repo);
        Assert.Equal(PartA.TopicTwoSha, GitDirFile(repo, "ORIG_HEAD").Trim());
        Assert.Equal(
            [
                "rebase (finish): returning to refs/heads/feature/topic",
                "rebase (pick): Topic two",
                "rebase (pick): Topic one",
                $"rebase (start): checkout {PartA.MainMoreSha}",
            ],
            await ReflogAsync(repo, 4));
        await AssertOneRebaseBackupAsync(repo, "feature/topic", PartA.TopicTwoSha);
        Assert.Equal(PartA.MainMoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(PartA.MainMoreSha, await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Equal(PartA.OtherOneSha, await RevParseAsync(repo, "refs/heads/feature/other"));
        Assert.Equal(PartA.TopicTwoSha, await RevParseAsync(repo, "refs/remotes/origin/feature/topic"));
        Assert.Equal([$"refs/heads/feature/topic {PartA.TopicTwoSha}", $"refs/heads/main {PartA.MainMoreSha}"], await OriginHeadsAsync(repo));

        // Step 3: the new tip's details.
        app.Click(app.GraphRowPoint(0));
        await WaitForDetailsAsync(app, "Topic two");
        shot = Shot(app, "a2c-rebased-commit-details");
        AssertRowSelected(app, 0);
        await AssertDetailsAsync(app, "Topic two", "2026-01-01 12:03", "2026-01-02 09:00", PartA.TopicTwoOntoMainSha, ["befae26"], "A two.txt topic");
        AssertGraph(app, PartA.R1, PartA.RebaseLanes, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check A2, part B: "Rebase feature/topic onto this commit" from the commit menu of "Main
    /// work": the selection stays on Main work, the status names its short SHA ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A2_rebase_onto_a_commit()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 4: the right-click selects "Main work" and opens its menu; its rebase runs.
        var (menu, _) = RightClickCommit(app, PartA.MainWorkRow);
        AssertRowSelected(app, PartA.MainWorkRow);
        AssertMenuAsReported(app, menu, PartA.MainWorkCommitMenu, "Check out this commit", PartA.CommitMenuHeight);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Rebase feature/topic onto this commit");
        await WaitForOperationAsync(app, repo, PartA.RebaseTopicOntoMainWorkTitle, mark);
        await WaitForDetailsAsync(app, "Main work");
        var shot = Shot(app, "a2d-rebased-onto-commit");
        await AssertNoDialogAsync(app);
        var rebase = AssertCall(app, calls, PartA.RebaseOntoMainWorkCommand, 0);
        await AssertGitPrintedAsync(rebase.StandardError, PartA.Ta3("feature/topic", 2), PartA.RebaseOntoMainWorkCommand);
        AssertStatus(app, PartA.RebasedTopicOntoMainWork);
        AssertOperationLine(app, mark, PartA.RebaseRepo, PartA.RebaseTopicOntoMainWorkTitle, OperationOutcome.Succeeded, PartA.RebasedTopicOntoMainWork);
        AssertNoBackupRemoved(app, mark);
        AssertGraph(app, PartA.R2, PartA.RebaseLanes, showsDate: true, picture: shot);
        AssertRowSelected(app, PartA.MainWorkRowInR2);
        Assert.Equal(PartA.MainWorkSha, Repo(app).Selection.Single);
        await AssertLeftPanelAsync(app, PartA.R2LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 3, behind: 2, head: true, "feature", "topic");

        // Git.
        Assert.Equal(["Topic two", "Topic one", "Main work", "Add core", "Initial commit"], await GitLinesAsync(repo, "log", "--format=%s", "feature/topic"));
        Assert.Equal(PartA.TopicTwoOntoMainWorkSha, await RevParseAsync(repo, "refs/heads/feature/topic"));
        await AssertCommitAsync(repo, PartA.TopicTwoOntoMainWorkSha, "Topic two", PartA.TopicOneOntoMainWorkSha, ScenarioTime(3), AppCommitTime);
        await AssertCommitAsync(repo, PartA.TopicOneOntoMainWorkSha, "Topic one", PartA.MainWorkSha, ScenarioTime(2), AppCommitTime);
        Assert.Equal(["## feature/topic...origin/feature/topic [ahead 3, behind 2]"], await GitLinesAsync(repo, "status", "-sb"));
        AssertNoRebaseFolder(repo);
        var reflog = await ReflogAsync(repo, 4);
        Assert.Equal("rebase (finish): returning to refs/heads/feature/topic", reflog[0]);
        Assert.Equal($"rebase (start): checkout {PartA.MainWorkSha}", reflog[3]);
        await AssertOneRebaseBackupAsync(repo, "feature/topic", PartA.TopicTwoSha);
        Assert.Equal(PartA.MainMoreSha, await RevParseAsync(repo, "refs/heads/main"));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check A3: rebase by drag: the drop menus with their rebase items (the current branch onto
    /// main and onto origin/main; a remote branch onto the current branch, which offers none;
    /// feature/other, not current, onto main), then "Check out feature/other and rebase it onto
    /// main": the graph, HEAD, the backup ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A3_rebase_by_drag()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);

        // The label centres both harnesses press and drop on, measured here (±1) for the real window.
        var topic = app.GraphLabelBounds(PartA.TopicTwoRow, "feature/topic").Center;
        var main = app.GraphLabelBounds(PartA.MainMoreRow, "main").Center;
        var other = app.GraphLabelBounds(PartA.OtherOneRow, "feature/other").Center;
        Assert.True(
            Near(PartA.OtherLabelCentre, other) && Near(PartA.MainLabelCentre, main) && Near(PartA.TopicLabelCentre, topic),
            $"The labels' centres are feature/other {other}, main {main}, feature/topic {topic}; PartA holds {PartA.OtherLabelCentre}, {PartA.MainLabelCentre}, {PartA.TopicLabelCentre}.");

        // Step 1: the current branch feature/topic onto main.
        var calls = app.GitCalls().Count;
        var menu = Drop(app, topic, main);
        var shot = Shot(app, "a3a-drop-current-onto-main");
        AssertDropFactRead(app, calls);
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertMenuAsReported(app, menu, PartA.TopicOntoMainDropMenu, "Check out main and merge feature/topic into it", PartA.DropWithRebaseMenuHeight, shot);
        await CloseMenuAsync(app);
        Assert.Equal(refs, await RefsAsync(repo));

        // Step 2: in the left panel, topic onto origin/main: the row got no click.
        calls = app.GitCalls().Count;
        var from = app.BoundsOf(await app.RefRowAsync("Local branches", "feature", "topic")).Center;
        var onto = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", "main")).Center;
        menu = Drop(app, from, onto);
        shot = Shot(app, "a3b-drop-onto-remote-branch");
        AssertDropFactRead(app, calls);
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertNoRefHighlighted(app);
        AssertMenuAsReported(app, menu, PartA.TopicOntoOriginMainDropMenu, "Push feature/topic to origin/main", PartA.DropWithRebaseMenuHeight, shot);
        await CloseMenuAsync(app);

        // Step 3: origin/main onto topic: no rebase item.
        calls = app.GitCalls().Count;
        from = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", "main")).Center;
        onto = app.BoundsOf(await app.RefRowAsync("Local branches", "feature", "topic")).Center;
        menu = Drop(app, from, onto);
        AssertDropFactRead(app, calls);
        AssertRowSelected(app, PartA.TopicTwoRow);
        AssertMenuAsReported(app, menu, PartA.OriginMainOntoTopicDropMenu, "Merge origin/main into feature/topic", PartA.DropOfRemoteBranchMenuHeight);
        await CloseMenuAsync(app);
        Assert.Equal(refs, await RefsAsync(repo));

        // Step 4: feature/other, not current, onto main.
        calls = app.GitCalls().Count;
        menu = Drop(app, other, main);
        shot = Shot(app, "a3c-drop-other-onto-main");
        AssertDropFactRead(app, calls);
        AssertRowSelected(app, PartA.OtherOneRow);
        AssertMenuAsReported(app, menu, PartA.OtherOntoMainDropMenu, "Check out main and merge feature/other into it", PartA.DropWithRebaseMenuHeight, shot);

        // Step 5: "Check out feature/other and rebase it onto main".
        var mark = app.LogMark();
        calls = app.GitCalls().Count;
        List<string> running;
        using (var recorder = app.RecordStatusTexts())
        {
            app.ClickMenuItem("Check out feature/other and rebase it onto main");
            await WaitForOperationAsync(app, repo, PartA.CheckOutOtherAndRebaseTitle, mark);
            running = [.. recorder.Texts];
        }

        await WaitForDetailsAsync(app, "Other one");
        shot = Shot(app, "a3d-checked-out-and-rebased");
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(app, calls, PartA.SwitchToOtherCommand, PartA.RebaseOntoMainCommand);
        AssertCall(app, calls, PartA.SwitchToOtherCommand, 0);
        var rebase = AssertCall(app, calls, PartA.RebaseOntoMainCommand, 0);
        await AssertGitPrintedAsync(rebase.StandardError, PartA.Ta3("feature/other", 1), PartA.RebaseOntoMainCommand);
        AssertStatus(app, PartA.RebasedOtherOntoMain);
        AssertOperationLine(app, mark, PartA.RebaseRepo, PartA.CheckOutOtherAndRebaseTitle, OperationOutcome.Succeeded, PartA.RebasedOtherOntoMain);
        AssertBranch(app, "feature/other");
        AssertGraph(app, PartA.R3, PartA.R3Lanes, showsDate: true, picture: shot);
        AssertRowSelected(app, 0);
        await AssertDetailsAsync(app, "Other one", "2026-01-01 12:06", "2026-01-02 09:00", PartA.OtherOneOntoMainSha, ["1dd58b8"], "A one.txt other");
        await AssertLeftPanelAsync(app, PartA.R3LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: true, "feature", "other");
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: false, "feature", "topic");

        // Git.
        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal(PartA.OtherOneOntoMainSha, await repo.HeadAsync());
        await AssertCommitAsync(repo, PartA.OtherOneOntoMainSha, "Other one", PartA.MainMoreSha, ScenarioTime(6), AppCommitTime);
        Assert.Equal(["## feature/other"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal(PartA.TopicTwoSha, await RevParseAsync(repo, "refs/heads/feature/topic"));
        Assert.Equal(PartA.MainMoreSha, await RevParseAsync(repo, "refs/heads/main"));
        AssertNoRebaseFolder(repo);
        Assert.Equal(
            [
                "rebase (finish): returning to refs/heads/feature/other",
                "rebase (pick): Other one",
                $"rebase (start): checkout {PartA.MainMoreSha}",
                "checkout: moving from feature/topic to feature/other",
            ],
            await ReflogAsync(repo, 4));
        await AssertOneRebaseBackupAsync(repo, "feature/other", PartA.OtherOneSha);
        Assert.False(InGitDir(repo, "rebase-merge/visualcommit-return"), "rebase-merge/visualcommit-return was written.");
        AssertNoClippedText(app);

        // Before the checkout the app read rebase.autoStash (1.3 step 1: not set, so it checked the
        // status, which was clean). This and the running texts are asserted last, so that what comes
        // before is checked on its own.
        AssertCalledInOrder(app, calls, PartA.AutoStashCommand, PartA.SwitchToOtherCommand, PartA.RebaseOntoMainCommand);
        var autoStash = AssertCall(app, calls, PartA.AutoStashCommand, 1);
        Assert.Equal(string.Empty, autoStash.StandardOutput);

        // The running texts (10.1, not pictured): the checkout's, then the rebase's.
        var checkingOut = running.IndexOf(PartA.CheckingOutOther);
        var rebasing = running.IndexOf(PartA.RebasingOtherOntoMain);
        Assert.True(
            checkingOut >= 0 && rebasing > checkingOut,
            $"The running texts were not \"{PartA.CheckingOutOther}\", then \"{PartA.RebasingOtherOntoMain}\": {string.Join(" | ", running)}");
    }

    /// <summary>The backups are one rebase backup of <paramref name="branch"/> pointing at <paramref name="oldTip"/>, its time parsed (design 1.2).</summary>
    private static async Task AssertOneRebaseBackupAsync(TempRepo repo, string branch, string oldTip)
    {
        var backups = await BackupsAsync(repo);
        var backup = Assert.Single(backups);
        Assert.Equal("rebase", backup.Ref.Kind);
        Assert.False(backup.Ref.IsChanges);
        Assert.Null(backup.Ref.Number);
        Assert.Equal(branch, backup.Ref.Name);
        Assert.Equal($"{BackupRef.Prefix}{backup.Ref.Stem}/{branch}", backup.Name);
        Assert.Equal(oldTip, backup.Sha);
    }

    /// <summary>Whether two points are the same within a pixel.</summary>
    private static bool Near((double X, double Y) expected, Avalonia.Point actual) =>
        Math.Abs(expected.X - actual.X) <= 1 && Math.Abs(expected.Y - actual.Y) <= 1;
}
