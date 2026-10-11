// Part D's checks on sync with origin at the server (docs/test-reports/phase-3.md, part D's "Sync
// at the server"): pull with its progress and Cancel, then push (18); pull from the branch's menu
// (18b); a rejected push (19); force push with lease (20).
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksD
{
    /// <summary>
    /// Check 18: a pull held at a percentage and cancelled with nothing integrated; the pull again,
    /// which merges; then the push.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_18_pull_cancelled_then_pull_and_push()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        await RemoteChangeAndLocalChangeAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1.
        Shot(app, "18a-before-pull");
        AssertGraph(app, PartD.SyncLocalChangeGraph, lanes: 3);
        AssertSyncLeftPanel(app, mainAhead: 1);
        var head = await repo.HeadAsync();
        var refs = await RefsAsync(repo);

        // Step 2: the pull's fetch held, then cancelled.
        _ = scenario.Server.HoldNextFetchPack();
        app.Click(ToolbarButton(app, "PullButton"));
        await app.WaitForProgressPercentAsync(NetworkTimeout);
        Shot(app, "18b-pull-progress");
        AssertHeldProgress(app, PartD.PullingMain);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(refs, await RefsAsync(repo));

        var mark = app.LogMark();
        ClickId(app, "OperationCancelButton");
        await app.WaitForIdleAsync(timeout: NetworkTimeout);
        scenario.Server.Release();
        AssertIdleStatus(app, PartD.PullCancelled);
        await AssertNoDialogAsync(app);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "MERGE_HEAD exists.");
        Assert.Equal(await CommitBySubjectAsync(repo, "Shared work"), await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Empty(LockFiles(repo));
        AssertGraph(app, PartD.SyncLocalChangeGraph, lanes: 3);
        AssertSyncLeftPanel(app, mainAhead: 1);
        Assert.Contains(app.LogLinesSince(mark), line => line.Contains("] sync: Pull: Cancelled after ", StringComparison.Ordinal));

        // Step 3: the pull again: a fetch, then the merge git pull would make.
        var calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForResultAsync(app, PartD.PulledAndMerged);
        Shot(app, "18c-pulled");
        AssertIdleStatus(app, PartD.PulledAndMerged);
        Assert.Equal(
            [PartD.FetchOriginCommand, PartD.MergeFetchHeadCommand],
            app.GitCalls().Skip(calls).Select(call => call.CommandText).Where(text => text.StartsWith("git fetch", StringComparison.Ordinal) || text.StartsWith("git merge", StringComparison.Ordinal)));
        await AssertPullMergeAsync(repo, scenario, head);
        AssertGraph(app, [.. PartD.SyncPulledGraph.Select(row => row.WithPort(scenario.Port))], lanes: 4, showsDate: false);
        AssertSyncLeftPanel(app, mainAhead: 2);
        Assert.Equal("## main...origin/main [ahead 2]", await StatusLineAsync(repo));

        // Step 4: the push; its running text is recorded, not pictured.
        calls = app.GitCalls().Count;
        using (var recorder = app.RecordStatusTexts())
        {
            app.Click(ToolbarButton(app, "PushButton"));
            await WaitForResultAsync(app, PartD.PushedMain);
            Assert.Contains(recorder.Texts, text => text.StartsWith(PartD.PushingMain, StringComparison.Ordinal));
        }

        Shot(app, "18d-pushed");
        AssertIdleStatus(app, PartD.PushedMain);
        Assert.Contains(PartD.PushMainCommand, app.GitCalls().Skip(calls).Select(call => call.CommandText));
        AssertGraph(app, [.. PartD.SyncPushedGraph.Select(row => row.WithPort(scenario.Port))], lanes: 4, showsDate: false);
        AssertSyncLeftPanel(app, mainAhead: 0);
        Assert.Equal(await repo.HeadAsync() + "\trefs/heads/main", (await repo.GitAsync("ls-remote", "origin", "refs/heads/main")).StandardOutput.Trim());
        Assert.Equal("## main...origin/main", await StatusLineAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 18b: the menus of origin/main and of the current branch main; "Pull from origin/main"
    /// as a fast-forward; a pull with nothing to do; a pull that <c>pull.ff=only</c> refuses.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_18b_pull_from_the_branch_menu()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: origin/main's menu, read and closed; then main's.
        var (remoteMenu, remotePointer) = await RightClickRowAsync(app, "Remotes", "origin", "main");
        AssertMenu(remoteMenu, PartD.RemoteMainMenu);
        AssertHighlighted(remoteMenu, "Merge origin/main into main");
        AssertMenuShape(app, remoteMenu);
        AssertMenuAtPointer(remoteMenu, remotePointer);
        var mark = app.LogMark();
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, mark);

        var (menu, pointer) = await RightClickRowAsync(app, "Local branches", "main");
        Shot(app, "18b1-current-branch-menu");
        AssertMenu(menu, PartD.CurrentMainMenu);
        AssertHighlighted(menu, "Pull from origin/main");
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        AssertNothingSelected(app);

        // Step 2: a fast-forward.
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Pull from origin/main");
        await WaitForResultAsync(app, PartD.PulledFastForward);
        Shot(app, "18b2-fast-forward");
        AssertIdleStatus(app, PartD.PulledFastForward);
        var merge = app.GitCalls().Skip(calls).Last(call => call.CommandText.StartsWith("git merge", StringComparison.Ordinal));
        Assert.Matches(new Regex(@"(?m)^Updating a7f2cfd\.\.[0-9a-f]{7}\r?$"), merge.StandardOutput);
        Assert.Matches(new Regex(@"(?m)^Fast-forward\r?$"), merge.StandardOutput);
        Assert.Equal("Remote change", await SubjectAsync(repo, "HEAD"));
        Assert.Equal(await CommitBySubjectAsync(repo, "Shared work"), await RevParseAsync(repo, "HEAD^1"));
        Assert.Null(await RevParseAsync(repo, "HEAD^2"));
        AssertGraph(app, PartD.SyncFastForwardedGraph, lanes: 3);
        AssertSyncLeftPanel(app, mainAhead: 0);

        // Step 3: nothing to pull.
        var head = await repo.HeadAsync();
        var refs = await RefsAsync(repo);
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForResultAsync(app, PartD.AlreadyUpToDate);
        AssertIdleStatus(app, PartD.AlreadyUpToDate);
        merge = app.GitCalls().Skip(calls).Last(call => call.CommandText.StartsWith("git merge", StringComparison.Ordinal));
        Assert.Contains("Already up to date.", merge.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(refs, await RefsAsync(repo));
        AssertGraph(app, PartD.SyncFastForwardedGraph, lanes: 3);

        // Step 4: diverged, with pull.ff=only: the fetch runs, the merge is refused.
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\nmore\n", "More remote work");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        await app.WaitForAsync(() => RowOf(app, "Local change") >= 0, "the graph to show \"Local change\"", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        app.Click(ToolbarButton(app, "PullButton"));
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "18b3-pull-refused");
        await AssertErrorDialogAsync(app, Expected(PartD.PullRefusedDialog, scenario.Port));
        AssertStatus(app, PartD.PullFailed, OperationStatusTone.Danger);
        AssertGraph(app, PartD.SyncPullRefusedGraph, lanes: 3);
        AssertLocalBranch(app, "main", ahead: 1, behind: 1, head: true);
        Assert.Equal("Local change", await SubjectAsync(repo, "HEAD"));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 19: a push rejected because origin has more; "Pull" in the dialog, then the push; a
    /// rejected push of another branch, and "Fetch origin" in the dialog.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_19_push_rejected()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        var remoteChange = await RemoteChangeAndLocalChangeAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var head = await repo.HeadAsync();

        // Step 1: the push is rejected.
        app.Click(ToolbarButton(app, "PushButton"));
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "19a-push-rejected");
        var dialog = await AssertErrorDialogAsync(app, Expected(PartD.PushRejectedMain, scenario.Port));
        AssertCard(app, "ErrorDialogCard", 400);
        AssertStatus(app, PartD.PushMainFailed, OperationStatusTone.Danger);
        AssertGraph(app, PartD.SyncLocalChangeGraph, lanes: 3);
        AssertSyncLeftPanel(app, mainAhead: 1);
        Assert.Equal(remoteChange, await ServerRefAsync(repo, scenario, "refs/heads/main"));
        ClickId(app, "ErrorCopyButton");
        Assert.Equal(dialog.Output, app.Clipboard.Text);
        AssertNoClippedText(app);

        // Step 2: "Pull" in the dialog.
        ClickId(app, "ErrorExtraButton");
        await WaitForResultAsync(app, PartD.PulledAndMerged);
        Shot(app, "19b-pulled");
        await AssertNoDialogAsync(app);
        AssertIdleStatus(app, PartD.PulledAndMerged);
        await AssertPullMergeAsync(repo, scenario, head);
        AssertGraph(app, [.. PartD.SyncPulledGraph.Select(row => row.WithPort(scenario.Port))], lanes: 4, showsDate: false);
        AssertSyncLeftPanel(app, mainAhead: 2);
        Assert.Equal("## main...origin/main [ahead 2]", await StatusLineAsync(repo));

        // Step 3: the push.
        app.Click(ToolbarButton(app, "PushButton"));
        await WaitForResultAsync(app, PartD.PushedMain);
        AssertIdleStatus(app, PartD.PushedMain);
        AssertGraph(app, [.. PartD.SyncPushedGraph.Select(row => row.WithPort(scenario.Port))], lanes: 4, showsDate: false);
        Assert.Equal("## main...origin/main", await StatusLineAsync(repo));

        // Step 4: published, pushed to from elsewhere, is rejected too.
        await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        await RightClickRowAsync(app, "Local branches", "published");
        app.ClickMenuItem("Push published to origin/published");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "19c-other-branch-rejected");
        await AssertErrorDialogAsync(app, Expected(PartD.PushRejectedPublished, scenario.Port));
        AssertStatus(app, PartD.PushPublishedFailed, OperationStatusTone.Danger);

        // Step 5: "Fetch origin" in the dialog.
        ClickId(app, "ErrorExtraButton");
        await WaitForResultAsync(app, PartD.FetchedFromOrigin);
        Shot(app, "19d-fetched");
        await AssertNoDialogAsync(app);
        AssertIdleStatus(app, PartD.FetchedFromOrigin);
        AssertGraph(app, [.. PartD.SyncPublishedElsewhereGraph.Select(row => row.WithPort(scenario.Port))], lanes: 5, showsDate: false);
        AssertLocalBranch(app, "published", ahead: 1, behind: 1, head: false);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 20: force push with lease of a rewritten branch (menu, confirmation, result, backup);
    /// then a fetch that brings someone else's commit, and a force push that stops.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_20_force_push_with_lease()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        var publishedWork = await CommitBySubjectAsync(repo, "Published work");
        await repo.CheckoutAsync("published");
        await repo.GitAsync("reset", "--hard", await CommitBySubjectAsync(repo, "Shared work"));
        await repo.CommitFileAsync("src/published.txt", "rewritten\n", "Rewritten work");
        await repo.CheckoutAsync("main");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1.
        Shot(app, "20a-rewritten");
        AssertGraph(app, PartD.SyncRewrittenGraph, lanes: 3);
        AssertLocalBranch(app, "published", ahead: 1, behind: 1, head: false);

        // Step 2: the branch's menu and the confirmation.
        var (menu, pointer) = await RightClickRowAsync(app, "Local branches", "published");
        Shot(app, "20b-branch-menu");
        AssertMenu(menu, PartD.DivergedPublishedMenu);
        AssertHighlighted(menu, "Check out published");
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        app.ClickMenuItem("Force push published…");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Shot(app, "20c-force-push-confirmation");
        await AssertConfirmationAsync(app, PartD.ForcePushTitle, PartD.ForcePushQuestion, PartD.BackupDetail, "Force push");
        AssertNoClippedText(app);

        // Step 3: the force push.
        var calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.ForcePushedPublished);
        Shot(app, "20d-force-pushed");
        AssertIdleStatus(app, PartD.ForcePushedPublished);
        var push = app.GitCalls().Skip(calls).Single(call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal(PartD.ForcePushCommand, push.CommandText);
        Assert.Matches(new Regex(@"(?m)^ \+ 8c6f545\.\.\.[0-9a-f]{7} published -> published \(forced update\)\r?$"), push.StandardError);
        AssertGraph(app, PartD.SyncForcePushedGraph, lanes: 2);
        AssertLocalBranch(app, "published", ahead: 0, behind: 0, head: false);
        var rewritten = await CommitBySubjectAsync(repo, "Rewritten work");
        Assert.Equal(rewritten, await ServerRefAsync(repo, scenario, "refs/heads/published"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        AssertBackup(backup, "force-push", "origin/published", publishedWork);

        // Step 4: someone else pushes; the toolbar's Fetch brings it; a commit on published outside the app.
        await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "rewritten\nsomeone else\n", "Someone else's work");
        app.Click(ToolbarButton(app, "FetchButton"));
        await WaitForResultAsync(app, PartD.FetchedFromAll);
        AssertIdleStatus(app, PartD.FetchedFromAll);
        await repo.CheckoutAsync("published");
        await repo.CommitFileAsync("src/more.txt", "more\n", "More rewritten work");
        await repo.CheckoutAsync("main");
        await app.WaitForAsync(
            () => RowOf(app, "More rewritten work") is >= 0 and var row
                && Graph(app).Data!.LabelsAt(row).Any(label => label.Text == "published")
                && ((Avalonia.Controls.TextBlock)app.FindByAutomationId("CurrentBranch")).Text == "main",
            "the graph to show \"More rewritten work\" with published, and the branch main",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        AssertGraph(app, PartD.SyncSomeoneElseGraph, lanes: 2);
        AssertLocalBranch(app, "published", ahead: 1, behind: 1, head: false);
        var someoneElse = await CommitBySubjectAsync(repo, "Someone else's work");

        // Step 5: the force push stops.
        await RightClickRowAsync(app, "Local branches", "published");
        app.ClickMenuItem("Force push published…");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        ClickId(app, "ConfirmButton");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "20e-force-push-stopped");
        await AssertErrorDialogAsync(app, Expected(PartD.ForcePushStoppedDialog, scenario.Port));
        AssertStatus(app, PartD.ForcePushPublishedFailed, OperationStatusTone.Danger);
        Assert.Equal(someoneElse, await ServerRefAsync(repo, scenario, "refs/heads/published"));
        AssertGraph(app, PartD.SyncSomeoneElseGraph, lanes: 2);
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        var first = backups.Single(entry => entry.Name == backup.Name);
        AssertBackup(first, "force-push", "origin/published", publishedWork);
        AssertBackup(backups.Single(entry => entry.Name != backup.Name), "force-push", "origin/published", someoneElse);
        AssertNoClippedText(app);
    }

    /// <summary>Check 18 step 1: "Remote change" pushed from elsewhere (12:05), then "Local change" committed on main (12:06).</summary>
    private static async Task<string> RemoteChangeAndLocalChangeAsync(TempRepo repo)
    {
        var remoteChange = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        return remoteChange;
    }

    /// <summary>Check 18 after step 3: HEAD is the pull's merge of "Local change" and "Remote change", by Test Author at the app's time.</summary>
    private static async Task AssertPullMergeAsync(TempRepo repo, ServerScenario scenario, string headBefore)
    {
        Assert.Equal(scenario.WithPort(PartD.MergeSubject), await SubjectAsync(repo, "HEAD"));
        var fields = (await repo.GitAsync("log", "-1", "--format=%an%x09%cn%x09%at%x09%ct")).StandardOutput.Trim().Split('\t');
        var time = AppCommitTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        Assert.Equal([TempRepo.AuthorName, TempRepo.AuthorName, time, time], fields);
        Assert.Equal("Local change", await SubjectAsync(repo, "HEAD^1"));
        Assert.Equal("Remote change", await SubjectAsync(repo, "HEAD^2"));
        Assert.Equal("2", (await repo.GitAsync("rev-list", "--count", headBefore + "..HEAD")).StandardOutput.Trim());
    }

    /// <summary>The left panel of sync with main ahead by <paramref name="mainAhead"/>: published ↑1, topic with no arrows.</summary>
    private static void AssertSyncLeftPanel(ShellDriver app, int mainAhead)
    {
        AssertLocalBranch(app, "main", ahead: mainAhead, behind: 0, head: true);
        AssertLocalBranch(app, "published", ahead: 1, behind: 0, head: false);
        AssertLocalBranch(app, "topic", ahead: 0, behind: 0, head: false);
    }

    /// <summary>What a ref of the repository the server serves points at.</summary>
    private static async Task<string?> ServerRefAsync(TempRepo repo, ServerScenario scenario, string fullRef)
    {
        var result = await repo.GitAllowingFailureAsync("--git-dir", scenario.Server.RepositoryPath, "rev-parse", "--verify", "--quiet", fullRef);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>Asserts a backup ref: its kind, that its time parses, the name after its stem, and what it points at.</summary>
    private static void AssertBackup(RefEntry backup, string kind, string name, string target)
    {
        var parsed = BackupRef.Parse(backup.Name);
        Assert.Equal(kind, parsed.Kind);
        Assert.False(parsed.IsChanges);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(target, backup.Sha);
    }
}
