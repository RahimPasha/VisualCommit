// Check U21 of part U (docs/test-reports/phase-5.md): undo and redo of changes to a remote, against
// the scenarios' local bare origin, each with its question (design 7.5, D155, D170): a push that
// created a branch (its upstream keys too), a force push, a branch deleted on the remote, a tag
// pushed, a tag deleted on the remote; and the undo of a push whose lease fails because someone
// pushed since; "sync", "ops", dark, 1100×700. One method per action, each on a fresh copy.
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23.Direction;
using Steps = VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>U21 part A: topic pushed with "Set as upstream", undone (deleted on origin, its keys removed) and redone, each after its question.</summary>
    [AvaloniaFact]
    public async Task Check_U21_push_new_branch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: "Push topic…", then "Push" ("Set as upstream" ticked).
        await Steps.OpenRowMenuAsync(app, "Local branches", "topic");
        app.ClickMenuItem("Push topic…");
        await app.WaitForDialogAsync<PushDialogViewModel>();
        Assert.True(((Avalonia.Controls.CheckBox)app.FindByAutomationId(app.TopDialogView!, "PushSetUpstream")).IsChecked == true, "\"Set as upstream\" is not ticked.");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Push topic", mark, NetworkTimeout);
        await AssertNoDialogAsync(app);
        var push = Steps.AssertSucceeded(app, calls, PartU2.PushTopic);
        await Steps.AssertPrintedAsync(push, " * [new branch]      topic -> topic");
        AssertStatus(app, PartU2.PushedTopic);
        await U21.AssertTopicPushedAsync(app, repo);

        // Step 2: Undo asks first.
        await Steps.AskAsync(app, repo, Undo, PartU2.PushTopicText, PartU2.UndoPushTopic);
        Shot(app, "u21a-push-undo-question");

        // Step 3: "Undo": deleted on origin first, then the upstream's keys removed.
        calls = await Steps.ConfirmAsync(app, repo, Undo, PartU2.PushTopicText);
        var shot = Shot(app, "u21b-push-undone");
        var delete = PartU2.StepPush("refs/heads/topic", PartU2.TopicWork, null);
        AssertCalledInOrder(app, calls, delete, PartU2.RemoveTopicSection);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, delete), PartU2.DeletedTopicLine);
        Assert.Equal(
            [$"{PartU2.SharedWork} refs/heads/main", $"{PartU2.PublishedWork} refs/heads/published"],
            await Steps.LsRemoteAsync(repo, "--heads"));
        Assert.Null(await Steps.RefAsync(repo, "refs/remotes/origin/topic"));
        Assert.Empty(await Steps.ConfigAsync(repo, @"^branch\.topic\."));
        Assert.Equal(PartU2.TopicWork, await Steps.RefAsync(repo, "refs/heads/topic"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
        Assert.Equal((0, 0), Steps.Arrows(app, "topic"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.PushTopicText));

        // Step 4: Redo asks too.
        await Steps.AskAsync(app, repo, Redo, PartU2.PushTopicText, PartU2.RedoPushTopic);
        Shot(app, "u21c-push-redo-question");

        // Step 5: "Redo": pushed with an empty lease (the branch must not exist), then the keys added again.
        calls = await Steps.ConfirmAsync(app, repo, Redo, PartU2.PushTopicText);
        shot = Shot(app, "u21d-push-redone");
        var again = PartU2.StepPush("refs/heads/topic", string.Empty, PartU2.TopicWork);
        AssertCalledInOrder(
            app,
            calls,
            again,
            "git config --local --add branch.topic.remote origin",
            "git config --local --add branch.topic.merge refs/heads/topic");
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, again), PartU2.NewTopicLine);
        await U21.AssertTopicPushedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U21 part B: published force-pushed after phase 3's check 20 rewrite, undone and redone, each after its question.</summary>
    [AvaloniaFact]
    public async Task Check_U21_force_push()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CheckoutAsync("published");
        await repo.GitAsync("reset", "--hard", PartU2.SharedWork);
        Assert.Equal(PartU2.RewrittenWork, await repo.CommitFileAsync("src/published.txt", PartU2.Rewritten, "Rewritten work"));
        await repo.CheckoutAsync("main");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.SyncRewritten, lanes: 3);
        Assert.Equal((1, 1), Steps.Arrows(app, "published"));
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        var backup = $"{PartU2.FirstBackup("force-push", "origin/published")} {PartU2.PublishedWork}";

        // Step 6: "Force push published…", confirmed.
        await Steps.OpenRowMenuAsync(app, "Local branches", "published");
        app.ClickMenuItem("Force push published…");
        await AssertConfirmationAsync(app, PartU2.ForcePushTitle, PartU2.ForcePushQuestion, PartU2.BackupDetail, "Force push");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Force push published", mark, NetworkTimeout);
        await AssertNoDialogAsync(app);
        var push = Steps.AssertSucceeded(app, calls, PartU2.ForcePush);
        await Steps.AssertPrintedAsync(push, " + 8c6f545...4f6a372 published -> published (forced update)");
        AssertStatus(app, PartU2.ForcePushed);
        Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
        await U21.AssertForcePushedAsync(app, repo);

        // Step 7: Undo asks first.
        await Steps.AskAsync(app, repo, Undo, PartU2.ForcePushText, PartU2.UndoForcePush);
        Shot(app, "u21e-force-undo-question");

        // Step 8: "Undo": origin's published back at Published work, leased on Rewritten work.
        calls = await Steps.ConfirmAsync(app, repo, Undo, PartU2.ForcePushText);
        var shot = Shot(app, "u21f-force-undone");
        var back = PartU2.StepPush("refs/heads/published", PartU2.RewrittenWork, PartU2.PublishedWork);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, back), PartU2.ForceUndoneLine);
        Assert.Contains($"{PartU2.PublishedWork} refs/heads/published", await Steps.LsRemoteAsync(repo, "--heads"));
        Assert.Equal(PartU2.PublishedWork, await Steps.RefAsync(repo, "refs/remotes/origin/published"));
        Assert.Equal(PartU2.RewrittenWork, await Steps.RefAsync(repo, "refs/heads/published"));
        Assert.Equal((1, 1), Steps.Arrows(app, "published"));
        AssertGraph(app, PartU2.SyncRewritten, lanes: 3, picture: shot);
        Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.ForcePushText));

        // Step 9: Redo asks too.
        await Steps.AskAsync(app, repo, Redo, PartU2.ForcePushText, PartU2.RedoForcePush);
        Shot(app, "u21g-force-redo-question");

        // Step 10: "Redo": Rewritten work again, leased on Published work; no new backup.
        calls = await Steps.ConfirmAsync(app, repo, Redo, PartU2.ForcePushText);
        shot = Shot(app, "u21h-force-redone");
        var forward = PartU2.StepPush("refs/heads/published", PartU2.PublishedWork, PartU2.RewrittenWork);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, forward), PartU2.ForceRedoneLine);
        Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
        await U21.AssertForcePushedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U21 part C: published deleted on origin from the remote branch's menu, undone (pushed back) and redone, each after its question.</summary>
    [AvaloniaFact]
    public async Task Check_U21_delete_remote_branch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        var backup = $"{PartU2.FirstBackup("remote-branch", "origin/published")} {PartU2.PublishedWork}";
        var delete = PartU2.StepPush("refs/heads/published", PartU2.PublishedWork, null);

        // Step 11: "Delete published on origin…", confirmed.
        await Steps.OpenRowMenuAsync(app, "Remotes", "origin", "published");
        app.ClickMenuItem("Delete published on origin…");
        await AssertConfirmationAsync(app, PartU2.DeleteBranchTitle, PartU2.DeleteBranchQuestion, PartU2.BackupDetail, "Delete on origin");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete published on origin", mark, NetworkTimeout);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, delete);
        AssertStatus(app, PartU2.DeletedPublished);
        await U21.AssertPublishedDeletedAsync(app, repo, backup);

        // Step 12: Undo asks first.
        await Steps.AskAsync(app, repo, Undo, PartU2.DeleteRemoteBranchText, PartU2.UndoDeletePublished);
        Shot(app, "u21i-delete-undo-question");

        // Step 13: "Undo": pushed back with an empty lease.
        calls = await Steps.ConfirmAsync(app, repo, Undo, PartU2.DeleteRemoteBranchText);
        var shot = Shot(app, "u21j-delete-undone");
        var back = PartU2.StepPush("refs/heads/published", string.Empty, PartU2.PublishedWork);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, back), PartU2.NewPublishedLine);
        Assert.Contains($"{PartU2.PublishedWork} refs/heads/published", await Steps.LsRemoteAsync(repo, "--heads"));
        Assert.Equal(PartU2.PublishedWork, await Steps.RefAsync(repo, "refs/remotes/origin/published"));
        Assert.Equal((1, 0), Steps.Arrows(app, "published"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.DeleteRemoteBranchText));

        // Step 14: Redo asks too.
        await Steps.AskAsync(app, repo, Redo, PartU2.DeleteRemoteBranchText, PartU2.RedoDeletePublished);
        Shot(app, "u21k-delete-redo-question");

        // Step 15: "Redo": deleted again, leased on Published work.
        calls = await Steps.ConfirmAsync(app, repo, Redo, PartU2.DeleteRemoteBranchText);
        shot = Shot(app, "u21l-delete-redone");
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, delete), PartU2.DeletedPublishedLine);
        await U21.AssertPublishedDeletedAsync(app, repo, backup, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U21 part D: the lightweight v1.1 pushed, undone (deleted on origin) and redone, each after its question.</summary>
    [AvaloniaFact]
    public async Task Check_U21_push_tag()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 16: "Push v1.1 to origin".
        await Steps.OpenRowMenuAsync(app, "Tags", "v1.1");
        var calls = await Steps.RunMenuItemAsync(app, repo, "Push v1.1 to origin", "Push tag v1.1");
        await AssertNoDialogAsync(app);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, PartU2.PushTag), " * [new tag]         v1.1 -> v1.1");
        AssertStatus(app, PartU2.PushedTag);
        await U21.AssertTagPushedAsync(app, repo, PartU2.PushTagText);

        // Step 17: Undo asks first.
        await Steps.AskAsync(app, repo, Undo, PartU2.PushTagText, PartU2.UndoPushTag);
        Shot(app, "u21m-tag-undo-question");

        // Step 18: "Undo": deleted on origin, leased on Main work; the local v1.1 stays.
        calls = await Steps.ConfirmAsync(app, repo, Undo, PartU2.PushTagText);
        var shot = Shot(app, "u21n-tag-undone");
        var delete = PartU2.StepPush("refs/tags/v1.1", PartU2.MainWork, null);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, delete), PartU2.DeletedTagLine);
        Assert.Empty(await Steps.LsRemoteAsync(repo, "--tags"));
        Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/tags/v1.1"));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.PushTagText));

        // Step 19: Redo asks too.
        await Steps.AskAsync(app, repo, Redo, PartU2.PushTagText, PartU2.RedoPushTag);
        Shot(app, "u21o-tag-redo-question");

        // Step 20: "Redo": pushed again with an empty lease.
        calls = await Steps.ConfirmAsync(app, repo, Redo, PartU2.PushTagText);
        shot = Shot(app, "u21p-tag-redone");
        var again = PartU2.StepPush("refs/tags/v1.1", string.Empty, PartU2.MainWork);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, again), PartU2.NewTagLine);
        await U21.AssertTagPushedAsync(app, repo, PartU2.PushTagText, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U21 part E: v1.1, pushed before the app starts, deleted on origin, undone (pushed back) and redone, each after its question.</summary>
    [AvaloniaFact]
    public async Task Check_U21_delete_remote_tag()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("push", "--quiet", "origin", "refs/tags/v1.1");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        Assert.Equal([$"{PartU2.MainWork} refs/tags/v1.1"], await Steps.LsRemoteAsync(repo, "--tags"));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        var backup = $"{PartU2.FirstBackup("remote-tag", "origin/v1.1")} {PartU2.MainWork}";
        var delete = PartU2.StepPush("refs/tags/v1.1", PartU2.MainWork, null);

        // Step 21: "Delete v1.1 on origin…", confirmed.
        await Steps.OpenRowMenuAsync(app, "Tags", "v1.1");
        app.ClickMenuItem("Delete v1.1 on origin…");
        await AssertConfirmationAsync(app, PartU2.DeleteRemoteTagTitle, PartU2.DeleteRemoteTagQuestion, PartU2.BackupDetail, "Delete on origin");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete tag v1.1 on origin", mark, NetworkTimeout);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, delete);
        AssertStatus(app, PartU2.DeletedRemoteTag);
        await U21.AssertRemoteTagDeletedAsync(app, repo, backup);

        // Step 22: Undo asks first.
        await Steps.AskAsync(app, repo, Undo, PartU2.DeleteRemoteTagText, PartU2.UndoDeleteTag);
        Shot(app, "u21q-tag-delete-undo-question");

        // Step 23: "Undo": pushed back with an empty lease.
        calls = await Steps.ConfirmAsync(app, repo, Undo, PartU2.DeleteRemoteTagText);
        var shot = Shot(app, "u21r-tag-delete-undone");
        var back = PartU2.StepPush("refs/tags/v1.1", string.Empty, PartU2.MainWork);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, back), PartU2.NewTagLine);
        await U21.AssertTagPushedAsync(app, repo, PartU2.DeleteRemoteTagText, canRedo: true, picture: shot);

        // Step 24: Redo asks too.
        await Steps.AskAsync(app, repo, Redo, PartU2.DeleteRemoteTagText, PartU2.RedoDeleteTag);
        Shot(app, "u21s-tag-delete-redo-question");

        // Step 25: "Redo": deleted again; the backup stays.
        calls = await Steps.ConfirmAsync(app, repo, Redo, PartU2.DeleteRemoteTagText);
        shot = Shot(app, "u21t-tag-delete-redone");
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, delete), PartU2.DeletedTagLine);
        await U21.AssertRemoteTagDeletedAsync(app, repo, backup, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// U21 part F: published pushed, then "Published elsewhere" pushed to origin from another clone
    /// without a fetch in the app: the undo's lease fails, git's output in the error dialog, nothing
    /// changed, the entry kept.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U21_lease_fails()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 26: "Push published to origin/published".
        await Steps.OpenRowMenuAsync(app, "Local branches", "published");
        var calls = await Steps.RunMenuItemAsync(app, repo, "Push published to origin/published", "Push published");
        await AssertNoDialogAsync(app);
        await Steps.AssertPrintedAsync(Steps.AssertSucceeded(app, calls, PartU2.PushPublished), "   8c6f545..8e65315  published -> published");
        AssertStatus(app, PartU2.PushedPublished);
        AssertGraph(app, PartU2.SyncPublishedPushed, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.PushPublishedText));
        var keys = await Steps.ConfigAsync(repo, @"^branch\.published\.");
        var trackingRefs = await Steps.RefsUnderAsync(repo, "refs/remotes/");

        // Step 27: someone else pushes to origin; the app does not fetch.
        Assert.Equal(PartU2.PublishedElsewhere, await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", PartU2.Elsewhere, "Published elsewhere"));
        await app.WaitForIdleAsync();
        Assert.Contains($"{PartU2.PublishedElsewhere} refs/heads/published", await Steps.LsRemoteAsync(repo, "--heads"));
        Assert.Equal(PartU2.UnpushedWork, await Steps.RefAsync(repo, "refs/remotes/origin/published"));
        AssertGraph(app, PartU2.SyncPublishedPushed, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.PushPublishedText));

        // Step 28: Undo asks: the remote's old value from git's summary line (7.7).
        await Steps.AskAsync(app, repo, Undo, PartU2.PushPublishedText, PartU2.UndoLeasedPush);
        Shot(app, "u21u-lease-question");

        // Step 29: "Undo": git refuses the stale lease; the error dialog.
        var origin = await Steps.OriginUrlAsync(repo);
        var push = PartU2.StepPush("refs/heads/published", PartU2.UnpushedWork, PartU2.PublishedWork);
        calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        await app.WaitForIdleAsync(timeout: NetworkTimeout);
        Shot(app, "u21v-lease-failed");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Undo failed",
            PartU2.LeaseSummary,
            PartU2.SyncRepo,
            push,
            ["Copy output", "Close"],
            PartU2.Tu2(origin),
            PartU2.Tu2Key));
        AssertNoClippedText(app);
        Assert.Equal(1, app.LastCall(push).ExitCode);
        AssertStatus(app, $"{Steps.Title(Undo, PartU2.PushPublishedText)} failed", OperationStatusTone.Danger);
        await app.WaitForLogLineAsync(
            AppLogFile.OperationEnded(PartU2.SyncRepo, Steps.Title(Undo, PartU2.PushPublishedText), OperationOutcome.Failed, $"To {origin}"),
            mark: mark);
        Assert.Contains($"{PartU2.PublishedElsewhere} refs/heads/published", await Steps.LsRemoteAsync(repo, "--heads"));
        Assert.Equal(PartU2.UnpushedWork, await Steps.RefAsync(repo, "refs/heads/published"));
        Assert.Equal(trackingRefs, await Steps.RefsUnderAsync(repo, "refs/remotes/"));
        Assert.Equal(keys, await Steps.ConfigAsync(repo, @"^branch\.published\."));
        Assert.DoesNotContain(
            CallsSince(app, calls),
            call => call.StartsWith("git update-ref", StringComparison.Ordinal)
                || call.StartsWith("git config --local --add", StringComparison.Ordinal)
                || call.StartsWith("git config --local --remove-section", StringComparison.Ordinal));

        // Step 30: "Close": the entry stays (outside changes never remove one, 7.4).
        Steps.ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.PushPublishedText));
        AssertGraph(app, PartU2.SyncPublishedPushed, PartU2.SyncLanes);
        AssertNoClippedText(app);
    }

    /// <summary>The helpers of check U21's methods.</summary>
    private static class U21
    {
        /// <summary>U21 after steps 1 and 5: topic on origin and tracked, S-topic, Remotes 3, T+.</summary>
        public static async Task AssertTopicPushedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Contains($"{PartU2.TopicWork} refs/heads/topic", await Steps.LsRemoteAsync(repo, "--heads"));
            Assert.Equal(PartU2.TopicWork, await Steps.RefAsync(repo, "refs/remotes/origin/topic"));
            Assert.Equal(["branch.topic.remote origin", "branch.topic.merge refs/heads/topic"], await Steps.ConfigAsync(repo, @"^branch\.topic\."));
            AssertGraph(app, PartU2.SyncTopicPushed, PartU2.SyncLanes, picture: picture);
            Assert.Equal(3, Steps.SectionCount(app, "Remotes"));
            Assert.Equal((0, 0), Steps.Arrows(app, "topic"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.PushTopicText));
        }

        /// <summary>U21 after steps 6 and 10: origin's published is Rewritten work, S-F1, T+.</summary>
        public static async Task AssertForcePushedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Contains($"{PartU2.RewrittenWork} refs/heads/published", await Steps.LsRemoteAsync(repo, "--heads"));
            Assert.Equal(PartU2.RewrittenWork, await Steps.RefAsync(repo, "refs/remotes/origin/published"));
            Assert.Equal((0, 0), Steps.Arrows(app, "published"));
            AssertGraph(app, PartU2.SyncForcePushed, PartU2.SyncLanes, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.ForcePushText));
        }

        /// <summary>U21 after steps 11 and 15: origin has main only, S-del, Remotes 1, one backup, T+.</summary>
        public static async Task AssertPublishedDeletedAsync(ShellDriver app, TempRepo repo, string backup, Screenshot? picture = null)
        {
            Assert.Equal([$"{PartU2.SharedWork} refs/heads/main"], await Steps.LsRemoteAsync(repo, "--heads"));
            Assert.Null(await Steps.RefAsync(repo, "refs/remotes/origin/published"));
            Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.SyncPublishedDeleted, PartU2.SyncLanes, picture: picture);
            Assert.Equal(1, Steps.SectionCount(app, "Remotes"));
            Assert.Equal((0, 0), Steps.Arrows(app, "published"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.DeleteRemoteBranchText));
        }

        /// <summary>U21 after steps 16, 20 and 23: origin has v1.1 at Main work (a remote's tags are not drawn: G0), T+ or T− of <paramref name="text"/>.</summary>
        public static async Task AssertTagPushedAsync(ShellDriver app, TempRepo repo, string text, bool canRedo = false, Screenshot? picture = null)
        {
            Assert.Equal([$"{PartU2.MainWork} refs/tags/v1.1"], await Steps.LsRemoteAsync(repo, "--tags"));
            Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/tags/v1.1"));
            AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: picture);
            Steps.AssertButtons(app, canRedo ? UndoRedoButtons.CanRedo(text) : UndoRedoButtons.CanUndo(text));
        }

        /// <summary>U21 after steps 21 and 25: origin has no tag, the local v1.1 and the backup stay, T+.</summary>
        public static async Task AssertRemoteTagDeletedAsync(ShellDriver app, TempRepo repo, string backup, Screenshot? picture = null)
        {
            Assert.Empty(await Steps.LsRemoteAsync(repo, "--tags"));
            Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/tags/v1.1"));
            Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.DeleteRemoteTagText));
        }
    }
}
