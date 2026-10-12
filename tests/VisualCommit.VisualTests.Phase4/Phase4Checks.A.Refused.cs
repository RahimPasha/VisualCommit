// Part A, check A5 (docs/test-reports/phase-4.md): rebases git refuses (a changed tracked file, an
// untracked file in the way) and "Check out … and rebase it" the app refuses before the checkout.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksA
{
    /// <summary>
    /// Check A5, part A: a changed tracked file: "Rebase feature/topic onto main" refused by git
    /// (the error dialog's title, summary and git's lines; nothing changed but the backup), then
    /// "Check out feature/other and rebase it onto main" refused by the app before the checkout
    /// ("rebase" with src/core.txt changed).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A5_refused_for_a_changed_file()
    {
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("src/core.txt", "core\nlocal edit\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the working-changes row on top, in Topic two's lane; R0's rows one lower.
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, PartA.A5Working, showsDate: true, picture: app.Capture());
        AssertWorkingDash(app, app.Capture(), PartA.A5Working);
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));

        // Step 2: the rebase is offered (uncommitted changes disable no rebase item); git refuses it.
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "main");
        AssertMenu(menu, PartA.MainBranchMenu);
        Assert.True(menu.Item("Rebase feature/topic onto main").IsEnabled);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Rebase feature/topic onto main");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(OperationChainTimeout);
        await WaitForOperationAsync(app, repo, PartA.RebaseTopicOntoMainTitle, mark);
        Shot(app, "a5a-refused-changed-file");
        var rebase = AssertCall(app, calls, PartA.RebaseOntoMainCommand, 1);
        AssertNoRebaseFolder(repo);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartA.RebaseFailedTitle, PartA.DirtyTreeSummary, PartA.RebaseRepo, PartA.RebaseOntoMainCommand, PartA.ButtonsWithOutput, PartA.Ta1, PartA.Ta1Key));
        if (await IsReportGitAsync())
        {
            Assert.Equal(string.Empty, rebase.StandardOutput);
        }

        AssertStatus(app, PartA.RebaseTopicOntoMainFailed, OperationStatusTone.Danger);
        var line = AssertOperationLine(app, mark, PartA.RebaseRepo, PartA.RebaseTopicOntoMainTitle, OperationOutcome.Failed, PartA.Ta1Key);
        Assert.Contains(" [WRN] ", line, StringComparison.Ordinal);
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(PartA.TopicTwoSha, await repo.HeadAsync());
        Assert.False(InGitDir(repo, "ORIG_HEAD"), "The refused rebase wrote ORIG_HEAD.");
        Assert.Equal("core\nlocal edit\n", WorkingFile(repo, "src/core.txt"));
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, PartA.A5Working);

        // The backup was saved before git ran (1.3), and stays (14.3).
        AssertCalledInOrder(app, calls, "git update-ref --stdin", PartA.RebaseOntoMainCommand);
        await AssertOneRebaseBackupAsync(repo, "feature/topic", PartA.TopicTwoSha);
        var backup = Assert.Single(await BackupsAsync(repo)).Name;
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 3: the drop of feature/other onto main (A3 step 4's menu); the app refuses before the checkout.
        var reflog = await ReflogCountAsync(repo);
        var other = app.GraphLabelBounds(PartA.OtherOneRow, "feature/other").Center;
        var main = app.GraphLabelBounds(PartA.MainMoreRow, "main").Center;
        menu = Drop(app, other, main);
        AssertRowSelected(app, PartA.OtherOneRow);
        AssertMenuAsReported(app, menu, PartA.OtherOntoMainDropMenu, "Check out main and merge feature/other into it", PartA.DropWithRebaseMenuHeight);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem("Check out feature/other and rebase it onto main");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(OperationChainTimeout);
        await WaitForOperationAsync(app, repo, PartA.CheckOutOtherAndRebaseTitle, mark);
        Shot(app, "a5b-check-out-and-rebase-refused");
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git switch", StringComparison.Ordinal) || call.StartsWith("git rebase", StringComparison.Ordinal) || call.StartsWith("git checkout", StringComparison.Ordinal));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartA.RebaseFailedTitle, PartA.DirtyTreeSummary, PartA.RebaseRepo, null, PartA.CloseOnly, []));
        AssertStatus(app, PartA.CheckOutOtherAndRebaseFailed, OperationStatusTone.Danger);
        AssertBranch(app, "feature/topic");
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(reflog, await ReflogCountAsync(repo));
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(PartA.OtherOneSha, await RevParseAsync(repo, "refs/heads/feature/other"));
        Assert.Equal(backup, Assert.Single(await BackupsAsync(repo)).Name);
        AssertNoClippedText(app);

        // The app read rebase.autoStash (not set), then the status, and refused (1.3 step 1).
        // Asserted last, so that what comes before is checked on its own.
        var autoStash = AssertCall(app, calls, PartA.AutoStashCommand, 1);
        Assert.Equal(string.Empty, autoStash.StandardOutput);
        Assert.Contains(CallsSince(app, calls).SkipWhile(call => call != PartA.AutoStashCommand), call => call.StartsWith("git status", StringComparison.Ordinal));
        await CloseDialogAsync(app);
    }

    /// <summary>
    /// Check A5, part B: an untracked docs/main.txt where main has one: "Rebase feature/topic onto
    /// main" refused by git ("rebase" with the untracked file).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A5_refused_for_an_untracked_file()
    {
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("docs/main.txt", "mine\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 4: the working-changes row shows (the untracked file); git refuses the rebase.
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, PartA.A5Working, showsDate: true);
        Assert.Equal(["?? docs/"], await GitLinesAsync(repo, "status", "--porcelain"));
        await RightClickRefAsync(app, "Local branches", "main");
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Rebase feature/topic onto main");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(OperationChainTimeout);
        await WaitForOperationAsync(app, repo, PartA.RebaseTopicOntoMainTitle, mark);
        Shot(app, "a5c-refused-untracked-file");
        var rebase = AssertCall(app, calls, PartA.RebaseOntoMainCommand, 1);
        AssertNoRebaseFolder(repo);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartA.RebaseFailedTitle, PartA.UntrackedInTheWaySummary, PartA.RebaseRepo, PartA.RebaseOntoMainCommand, PartA.ButtonsWithOutput, PartA.Ta2, PartA.Ta2Key));
        if (await IsReportGitAsync())
        {
            Assert.Equal(string.Empty, rebase.StandardOutput);
        }

        AssertStatus(app, PartA.RebaseTopicOntoMainFailed, OperationStatusTone.Danger);
        AssertOperationLine(app, mark, PartA.RebaseRepo, PartA.RebaseTopicOntoMainTitle, OperationOutcome.Failed, PartA.Ta2Key);
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(PartA.TopicTwoSha, await repo.HeadAsync());
        Assert.Equal("mine\n", WorkingFile(repo, "docs/main.txt"));
        Assert.Equal(["## feature/topic...origin/feature/topic", "?? docs/"], await GitLinesAsync(repo, "status", "-sb"));
        await AssertOneRebaseBackupAsync(repo, "feature/topic", PartA.TopicTwoSha);
        AssertGraph(app, PartA.R0, PartA.RebaseLanes, PartA.A5Working);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }

    /// <summary>Clicks the error dialog's "Close" and waits until no dialog is open.</summary>
    private static async Task CloseDialogAsync(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var close = view.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Content as string == "Close");
        app.Click(close);
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// The working-changes row's dashed line from its ring down its lane (phase 2's drawing): a
    /// dash, a gap, a dash below the ring, 1 left of the lane's centre.
    /// </summary>
    private static void AssertWorkingDash(ShellDriver app, Screenshot picture, WorkingChangesRow working)
    {
        var graph = Graph(app);
        var centre = graph.TranslatePoint(new Point(graph.LaneCenterX(working.Lane), graph.WorkingRowBounds.Center.Y), app.Window)!.Value;
        var colour = PaletteOf(app).Lane(working.Colour);
        var x = centre.X - 1;
        AssertColour(colour, picture, new Point(x, centre.Y + 6), "the working-changes row's dash below its ring");
        Assert.NotEqual(colour, picture.PixelAt(new Point(x, centre.Y + 9)));
        AssertColour(colour, picture, new Point(x, centre.Y + 12), "the working-changes row's next dash");
    }
}
