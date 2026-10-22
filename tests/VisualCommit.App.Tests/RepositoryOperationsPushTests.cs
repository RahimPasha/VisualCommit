using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP7-remotes: push, force push with lease, upstreams and deleting a branch on a remote in
/// <see cref="RepositoryOperations"/> (docs/phase-3-design.md 2.19 to 2.22),
/// against the scenario repositories, through <see cref="OperationsHarness"/>: for each, its
/// confirmation, the backup it saves, what git holds afterwards, the status bar's text, its
/// failure dialog with git's own output and its extra button, its cancel, and that a second
/// click runs nothing more. Folder remotes serve most of them. WP7-remotes' tests are four
/// classes in one xunit collection, which runs beside those of the other sub-packages
/// (<see cref="RepositoryOperationsTestSupport"/> says why): <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RemotesCollection)]
public class RepositoryOperationsPushTests
{
    // 2.19: push.

    [Fact]
    public async Task Push_of_the_current_branch_goes_to_its_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);
        IToolbarActions toolbar = app.Operations;

        await toolbar.PushAsync();

        Assert.Contains("Pushing main to origin/main…", texts);
        Assert.Equal(("Pushed main to origin/main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(local, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal(0, app.Operations.CurrentBranch?.Ahead);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_rejected_push_of_the_current_branch_offers_Pull_and_the_push_then_goes_through()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.PushCurrentBranchAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push rejected", "origin has commits that your main does not have. Pull first, then push again.", "Pull"), (error.Title, error.Summary, error.ExtraAction));
        Assert.Contains(" ! [rejected]        main -> main (fetch first)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress origin refs/heads/main:refs/heads/main", error.Command);

        // "Pull" ran the pull, which merged.
        Assert.Equal("Pulled and merged origin/main into main", app.StatusText);
        var parents = (await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD")).StandardOutput.Trim().Split(' ');
        Assert.Equal([local, remote], parents[1..]);

        await app.Operations.PushCurrentBranchAsync();

        Assert.Equal("Pushed main to origin/main", app.StatusText);
        Assert.Equal(await repo.HeadAsync(), await RemoteHeadAsync(repo, "refs/heads/main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_rejected_push_of_another_branch_offers_Fetch_of_its_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.PushBranchAsync("refs/heads/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push rejected", "origin has commits that your published does not have. Pull first, then push again.", "Fetch origin"), (error.Title, error.Summary, error.ExtraAction));
        Assert.Contains(" ! [rejected]        published -> published (fetch first)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress origin refs/heads/published:refs/heads/published", error.Command);
        Assert.Equal("Fetched from origin", app.StatusText);
        Assert.Equal(elsewhere, await repo.RefTargetAsync("refs/remotes/origin/published"));
        Assert.Equal((1, 1), (app.Operations.FindRef("refs/heads/published")?.Ahead, app.Operations.FindRef("refs/heads/published")?.Behind));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_push_that_fails_otherwise_opens_Push_failed_without_an_extra_button()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.GitAsync("remote", "set-url", "origin", repo.SiblingPath("nowhere.git"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PushCurrentBranchAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push failed", "Git could not push main to origin/main."), (error.Title, error.Summary));
        Assert.False(error.HasExtraAction);
        Assert.False(string.IsNullOrWhiteSpace(error.Output));
        Assert.Equal(("Push main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Push_of_a_branch_without_an_upstream_goes_through_the_push_dialog_and_can_set_it_as_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Push().Cancel();

        await app.Operations.PushBranchAsync("refs/heads/topic");

        var dialog = Assert.IsType<PushDialogViewModel>(app.Dialogs.Shown.Single());
        Assert.Equal(("Push topic", true, "origin", "topic", true), (dialog.Title, dialog.HasOneRemote, dialog.Remote, dialog.BranchName, dialog.SetUpstream));
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/topic"));
        Assert.Equal(topic, await repo.RefTargetAsync("refs/remotes/origin/topic"));
        Assert.Equal("refs/heads/topic", await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Equal("origin/topic", app.Operations.FindRef("refs/heads/topic")?.Upstream);

        // Now it has an upstream: a push goes there without a dialog. A cancelled dialog of another branch runs nothing.
        await repo.GitAsync("branch", "other", "refs/heads/main");
        await app.Repository.RefreshAsync();
        var operations = app.Repository.Queue.LastResult;
        await app.Operations.PushBranchAsync("refs/heads/other");
        Assert.Same(operations, app.Repository.Queue.LastResult);
        Assert.Equal(2, app.Dialogs.Shown.Count);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Push_through_the_dialog_to_another_name_without_setting_the_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Push(branch: "topic-2", setUpstream: false);

        await app.Operations.PushBranchAsync("refs/heads/topic");

        Assert.Equal("Pushed topic to origin/topic-2", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/topic-2"));
        Assert.Null(await ConfigAsync(repo, "branch.topic.merge"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_drops_push_goes_to_the_remote_branch_it_was_dropped_on()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: true);

        Assert.Equal("Pushed topic to origin/main, and set it as upstream", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Fetch_pull_and_push_without_choices_are_deduplicated_and_nothing_else_is()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var queue = new KeyRecordingQueue();
        var operations = new RepositoryOperations(app.Repository, queue, app.Services, app.Log);
        app.Dialogs.Push().Confirm().Pick("origin/main").Confirm();

        await operations.FetchAllAsync();
        await operations.FetchAsync("origin");
        await operations.PullAsync();
        await operations.PushCurrentBranchAsync();
        await operations.PushBranchAsync("refs/heads/topic");
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: false);
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: true);
        await operations.ForcePushAsync("refs/heads/published");
        await operations.SetUpstreamAsync("refs/heads/topic");
        await operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        Assert.Equal(
            [
                ("Fetch", "fetch:*"),
                ("Fetch origin", "fetch:origin"),
                ("Pull", "pull:refs/heads/main"),
                ("Push main", "push:refs/heads/main->origin/refs/heads/main"),
                ("Push topic", null),
                ("Push topic", "push:refs/heads/topic->origin/refs/heads/main"),
                ("Push topic", null),
                ("Force push published", null),
                ("Set upstream of topic", null),
                ("Delete published on origin", null),
            ],
            queue.Operations.Select(operation => (operation.Title, operation.DedupeKey)));
        Assert.All(queue.Operations.Where(operation => operation.Kind == OperationKind.Network), operation => Assert.Equal(PromptMode.Ask, operation.Prompts));
        Assert.Equal(
            ["Fetching from all remotes…", "Fetching from origin…", "Pulling origin/main into main…", "Pushing main to origin/main…", "Pushing topic to origin/topic…"],
            queue.Operations.Take(5).Select(operation => operation.RunningText));
        app.AssertScriptFollowed();
    }

    // 2.20: force push with lease.

    [Fact]
    public async Task Force_push_confirms_backs_up_the_remote_branch_and_replaces_it_and_is_stopped_by_a_fetched_commit()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        await repo.CheckoutAsync("published");
        await repo.GitAsync("reset", "--quiet", "--hard", "refs/heads/main");
        var rewritten = await repo.CommitFileAsync("src/published.txt", "rewritten\n", "Rewritten work");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false).Confirm().Confirm().CloseError();

        // Declined: nothing.
        await app.Operations.ForcePushAsync("refs/heads/published");
        Assert.Equal(published, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        // Check 20 step 3, which relies on published's reflog (12:02 to 12:05) being no older than
        // origin/published's last update (12:03), both older than the app's calls (D111).
        await app.Operations.ForcePushAsync("refs/heads/published");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Force push published?",
                "Replace published on origin with your published? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your published, even ones the last fetch brought.",
                "A backup is saved first, so this can be undone.",
                "Force push",
                true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Force-pushed published to origin/published", app.StatusText);
        Assert.Equal(rewritten, await RemoteHeadAsync(repo, "refs/heads/published"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/force-push-[0-9]{8}T[0-9]{9}Z/origin/published$", backup.Name);
        Assert.Equal(published, backup.Sha);

        // Steps 4 and 5: someone else pushed, the toolbar's Fetch brought it, a local commit follows; the lease stops the push.
        var someoneElse = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "rewritten\nsomeone else\n", "Someone else's work");
        await app.Operations.FetchAllAsync();
        await repo.CheckoutAsync("published");
        await repo.CommitFileAsync("src/more.txt", "more\n", "More rewritten work");
        await repo.CheckoutAsync("main");
        await app.Repository.RefreshAsync();

        await app.Operations.ForcePushAsync("refs/heads/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Force push stopped", "origin/published has commits that your published does not include. Pull or look at them before forcing."), (error.Title, error.Summary));
        Assert.Contains(" ! [rejected]        published -> published (remote ref updated since checkout)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published", error.Command);
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Force push published failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(someoneElse, await RemoteHeadAsync(repo, "refs/heads/published"));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Contains(backups, entry => entry.Sha == someoneElse && entry.Name.EndsWith("/origin/published", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Force_push_of_a_branch_whose_upstream_has_another_name_asks_to_replace_the_upstream()
    {
        // fix tracks origin/main: the push replaces main on origin, which the question must name.
        using var repo = await Scenarios.RemoteSyncAsync();
        var main = await RemoteHeadAsync(repo, "refs/heads/main");
        await repo.GitAsync("branch", "--quiet", "--track", "fix", "origin/main");
        await repo.CheckoutAsync("fix");
        await repo.GitAsync("commit", "--quiet", "--amend", "-m", "Rewritten main work");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false);

        await app.Operations.ForcePushAsync("refs/heads/fix");

        var confirmation = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Force push fix?",
                "Replace main on origin with your fix? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your fix, even ones the last fetch brought."),
            (confirmation.Title, confirmation.Question));
        Assert.Equal(main, await RemoteHeadAsync(repo, "refs/heads/main"));
        app.AssertScriptFollowed();
    }

    // 2.21: upstreams.

    [Fact]
    public async Task Set_change_and_stop_tracking_an_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var selectedOnOpening = new List<string?>();
        var itemsSeen = new List<string[]>();
        app.Dialogs
            .Answer<PickDialogViewModel>(dialog =>
            {
                selectedOnOpening.Add(dialog.Selected);
                itemsSeen.Add([.. dialog.Items]);
                dialog.Filter = "pub";
                itemsSeen.Add([.. dialog.Items]);
                dialog.Selected = "origin/published";
                dialog.ConfirmCommand.Execute(null);
            })
            .Answer<PickDialogViewModel>(dialog =>
            {
                selectedOnOpening.Add(dialog.Selected);
                dialog.Selected = "origin/main";
                dialog.ConfirmCommand.Execute(null);
            })
            .Cancel();

        await app.Operations.SetUpstreamAsync("refs/heads/topic");

        var pick = Assert.IsType<PickDialogViewModel>(app.Dialogs.Shown[0]);
        Assert.Equal(("Set upstream for topic", "Set upstream"), (pick.Title, pick.ConfirmText));
        Assert.Equal([["origin/main", "origin/published"], ["origin/published"]], itemsSeen);
        Assert.Equal("topic now tracks origin/published", app.StatusText);
        Assert.Equal(("origin", "refs/heads/published"), (await ConfigAsync(repo, "branch.topic.remote"), await ConfigAsync(repo, "branch.topic.merge")));
        var topic = app.Operations.FindRef("refs/heads/topic");
        Assert.Equal((1, 1), (topic?.Ahead, topic?.Behind));

        await app.Operations.SetUpstreamAsync("refs/heads/topic");

        Assert.Equal([null, "origin/published"], selectedOnOpening);
        Assert.Equal("topic now tracks origin/main", app.StatusText);
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.topic.merge"));

        // A cancelled pick changes nothing.
        await app.Operations.SetUpstreamAsync("refs/heads/topic");
        Assert.Equal("topic now tracks origin/main", app.StatusText);

        await app.Operations.UnsetUpstreamAsync("refs/heads/topic");

        Assert.Equal("topic no longer tracks origin/main", app.StatusText);
        Assert.Null(await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Null(await ConfigAsync(repo, "branch.topic.remote"));
        Assert.Null(app.Operations.FindRef("refs/heads/topic")?.Upstream);
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    // 2.22: delete a branch on a remote.

    [Fact]
    public async Task Delete_on_a_remote_confirms_backs_up_and_deletes_with_a_lease()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        Assert.Equal(published, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Delete published on origin?", "Delete the branch published on origin? Everyone who uses origin loses it.", "A backup is saved first, so this can be undone.", "Delete on origin", true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Deleted published on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/published"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/remote-branch-[0-9]{8}T[0-9]{9}Z/origin/published$", backup.Name);
        Assert.Equal(published, backup.Sha);
        Assert.True(app.Operations.FindRef("refs/heads/published")?.UpstreamGone);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Delete_on_a_remote_is_stopped_when_someone_pushed_since_the_last_fetch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm().CloseError();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deletion stopped", "published on origin has changed since you last fetched. Fetch and look at it before deleting."), (error.Title, error.Summary));
        Assert.Contains("stale info", error.Output, StringComparison.Ordinal);
        Assert.Equal($"git push --progress --force-with-lease=refs/heads/published:{published} origin --delete refs/heads/published", error.Command);
        Assert.Equal(("Delete published on origin failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(elsewhere, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Equal(published, Assert.Single(await repo.BackupRefsAsync()).Sha);
        app.AssertScriptFollowed();
    }

    // Fetch refspecs (docs/phase-6-design.md 10.4, 10.5, 10.9; D100, D222, GD24), on "refspecs"
    // (RS0): fork fetches its branches into fork/up/*, origin its branches and GitHub's pull
    // requests into origin/pr/*.

    private const string Parser = "8394a5e2e769c4601161cbce8c239ff2aa246eb9";
    private const string Topic = "de4a87cd5a84d5ed135863187487f81fe89d030c";

    [Fact]
    public async Task A_drops_push_onto_a_renamed_remote_branch_pushes_to_the_branchs_name_on_its_remote()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);

        await app.Operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/fork/up/main", setUpstream: false);

        // 10.4 #8: refs/heads/topic:refs/heads/main on fork, named fork/up/main, titled as the item (GD24).
        AssertRanOnce(app, "Push topic to fork/up/main");
        Assert.Contains("Pushing topic to fork/up/main…", texts);
        Assert.Equal(("Pushed topic to fork/up/main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git push --progress fork refs/heads/topic:refs/heads/main" && call.Succeeded);
        Assert.Equal(Topic, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/up/main", "fork"));

        // Git moves the renamed tracking ref through the fetch line (scenario facts 8, item 6).
        Assert.Equal(Topic, await repo.RefTargetAsync("refs/remotes/fork/up/main"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/fork/up/up/main"));
        Assert.Empty(app.Dialogs.Shown);

        // The title, the running text and the dedupe key; onto an identity-mapped branch the title stays "Push {b}".
        var queue = new KeyRecordingQueue();
        var operations = new RepositoryOperations(app.Repository, queue, app.Services, app.Log);
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/fork/up/main", setUpstream: false);
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/fork/up/main", setUpstream: true);
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: false);
        Assert.Equal(
            [
                ("Push topic to fork/up/main", "Pushing topic to fork/up/main…", "push:refs/heads/topic->fork/refs/heads/main"),
                ("Push topic to fork/up/main", "Pushing topic to fork/up/main…", null),
                ("Push topic", "Pushing topic to origin/main…", "push:refs/heads/topic->origin/refs/heads/main"),
            ],
            queue.Operations.Select(operation => (operation.Title, operation.RunningText, operation.DedupeKey)));
    }

    [Fact]
    public async Task A_drop_onto_a_ref_that_is_no_branch_of_one_remote_starts_no_push()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        // A menu built before the lines changed reaches these (10.4 #7): R2 and R4.
        await app.Operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/pr/1", setUpstream: false);
        await app.Operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/upstream/pr/7", setUpstream: true);
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/pr/1");
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/upstream/pr/7");

        Assert.Null(app.LastResult);
        Assert.Empty(app.Dialogs.Shown);
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal(4, app.Log.Messages.Count(message => message.Contains(": not started, ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Deleting_a_renamed_remote_branch_deletes_its_branch_on_the_remote_with_the_mapped_lease_and_backup()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);
        app.Dialogs.Confirm();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/fork/up/main");

        // 10.4 #9: the branch's name on fork, the lease on fork's main at the tracking ref's commit.
        var confirmation = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Delete main on fork?", "Delete the branch main on fork? Everyone who uses fork loses it.", "A backup is saved first, so this can be undone.", "Delete on fork", true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Contains("Deleting main on fork…", texts);
        Assert.Equal("Deleted main on fork", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git push --progress --force-with-lease=refs/heads/main:{Parser} fork --delete refs/heads/main" && call.Succeeded);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/fork/up/main"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/remote-branch-{BackupTime}/fork/main$", backup.Name);
        Assert.Equal(Parser, backup.Sha);

        // "Push main back to fork…" brings it back, by the name the backup recorded.
        var saved = (await app.Repository.Repository.ReadBackupsAsync(TestContext.Current.CancellationToken)).Single(entry => entry.Kind == "remote-branch");
        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(saved);
        Assert.Equal("Push main back to fork?", app.Dialogs.Confirmations[^1].Title);
        Assert.Equal("Pushed main back to fork", app.StatusText);
        Assert.Equal(Parser, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Equal(Parser, await repo.RefTargetAsync("refs/remotes/fork/up/main"));

        // Deleted again, then undone: the undo pushes main back to fork.
        app.Dialogs.Confirm().Confirm();
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/fork/up/main");
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        await app.Operations.UndoAsync();
        Assert.Equal(Parser, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/up/main", "fork"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_push_that_would_give_a_tracking_ref_a_second_source_is_refused_before_git()
    {
        // A branch made by phase 5's checkout, or by --track in a terminal, tracks refs/heads/pr/1
        // on origin (0.5's P), whose push makes every later fetch of origin fail.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("branch", "--quiet", "--track", "pr/1", "refs/remotes/origin/pr/1");
        Assert.Equal("refs/heads/pr/1", await ConfigAsync(repo, "branch.pr/1.merge"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().Confirm().CloseError().Push(remote: "origin").CloseError();
        const string Refused = "Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is fetched from more than one ref of origin.";

        // Push to its upstream.
        await app.Operations.PushBranchAsync("refs/heads/pr/1");
        AssertRefused(app.Dialogs.Errors[^1], "Push failed");
        Assert.Equal(("Push pr/1 failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        // Force push: asked first, then refused before its backup.
        await app.Operations.ForcePushAsync("refs/heads/pr/1");
        Assert.Equal("Force push pr/1?", app.Dialogs.Confirmations[^1].Title);
        AssertRefused(app.Dialogs.Errors[^1], "Force push failed");
        Assert.Equal(("Force push pr/1 failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Empty(await repo.BackupRefsAsync());

        // The push dialog's push of the branch's own name to origin (G25 step 7).
        await repo.GitAsync("branch", "--unset-upstream", "pr/1");
        await app.Repository.RefreshAsync();
        await app.Operations.PushBranchAsync("refs/heads/pr/1");
        var dialog = Assert.IsType<PushDialogViewModel>(app.Dialogs.Shown.Single(shown => shown is PushDialogViewModel));
        Assert.Equal(("Push pr/1", "pr/1", true), (dialog.Title, dialog.BranchName, dialog.SetUpstream));
        AssertRefused(app.Dialogs.Errors[^1], "Push failed");

        Assert.Equal(3, app.Dialogs.Errors.Count);
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/pr/1"));
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("fetch", "--quiet", "origin")).ExitCode);
        Assert.Contains(app.Log.Messages, message => message.Contains($": Push pr/1: refused, {Refused}", StringComparison.Ordinal));
        app.AssertScriptFollowed();

        static void AssertRefused(ErrorDialogViewModel error, string title)
        {
            Assert.Equal((title, Refused), (error.Title, error.Summary));
            Assert.Equal((null, null), (error.Output, error.Command));
            Assert.False(error.HasExtraAction);
        }
    }

    [Fact]
    public async Task Of_two_lines_that_fetch_a_branch_its_push_runs_and_a_new_branch_they_would_share_is_refused()
    {
        // 0.5's C2 without the server's trunk: origin/trunk is fetched from refs/heads/main and,
        // through the default line, from refs/heads/trunk once that exists.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "refs/heads/main:refs/remotes/origin/trunk");
        await repo.GitAsync("fetch", "--quiet", "origin");
        var local = await repo.CommitFileAsync("more.txt", "more\n", "More work");
        await repo.GitAsync("branch", "--quiet", "trunk");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Push(remote: "origin").CloseError();

        // main exists on origin and fills origin/main alone: the push runs.
        await app.Operations.PushCurrentBranchAsync();
        Assert.Equal("Pushed main to origin/main", app.StatusText);
        Assert.Equal(local, await RemoteHeadAsync(repo, "refs/heads/main"));

        // A new trunk would fill origin/trunk beside main.
        await app.Operations.PushBranchAsync("refs/heads/trunk");
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push failed", "Pushing trunk to origin/trunk would stop every fetch of origin: origin/trunk is fetched from more than one ref of origin."), (error.Title, error.Summary));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/trunk"));
        Assert.Single(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_force_pushs_backup_with_no_branch_tracking_it_is_pushed_back_to_the_branch_its_remote_fills_it_from()
    {
        // forked tracks fork/up/main (fork's main) and replaces it by a force push; then it is deleted.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("branch", "--quiet", "--track", "forked", "refs/remotes/fork/up/main");
        await repo.CheckoutAsync("forked");
        await repo.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
        var rewritten = await repo.CommitFileAsync("forked.txt", "forked\n", "Rewritten on fork");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm();
        await app.Operations.ForcePushAsync("refs/heads/forked");
        Assert.Equal("Force-pushed forked to fork/up/main", app.StatusText);
        Assert.Equal(rewritten, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        await repo.GitAsync("branch", "--quiet", "-D", "forked");
        await app.Repository.RefreshAsync();

        var backup = (await app.Repository.Repository.ReadBackupsAsync(TestContext.Current.CancellationToken)).Single(entry => entry.Kind == "force-push");
        Assert.Equal("fork/up/main", backup.Main?.Name);

        // 10.4 #12: by the fork's line, main on fork, not phase 5's up/main.
        var (actions, _) = RecordingActions.Create();
        Assert.Equal("Push main back to fork…", ViewModels.Menus.RepositoryMenus.ForBackup(app.Repository.MenuContext, backup, actions, hasForcePushLease: true).Items.First().Label);
        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(backup);

        Assert.Equal("Push main back to fork?", app.Dialogs.Confirmations[^1].Title);
        Assert.Equal("Pushed main back to fork", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText.StartsWith($"git push --progress --force-with-lease=refs/heads/main:{rewritten} fork ", StringComparison.Ordinal) && call.Succeeded);
        Assert.Equal(Parser, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/up/main", "fork"));
        app.AssertScriptFollowed();
    }

    /// <summary>A queue that runs nothing: it records each operation, as given, and answers that it was cancelled.</summary>
    private sealed class KeyRecordingQueue : IOperationQueue
    {
        private readonly List<GitOperation> _operations = [];

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<OperationFinishedEventArgs>? Finished
        {
            add { }
            remove { }
        }

        public IReadOnlyList<GitOperation> Operations => _operations;

        public OperationSnapshot? Current => null;

        public int Waiting => 0;

        public string? FirstWaitingTitle => null;

        public OperationResult? LastResult => null;

        public Task<OperationResult> RunAsync(GitOperation operation)
        {
            _operations.Add(operation);
            return Task.FromResult(new OperationResult(OperationOutcome.Cancelled, $"{operation.Title} cancelled", null));
        }

        public bool TryStartInBackground(GitOperation operation) => false;

        public Task WhenIdleAsync() => Task.CompletedTask;

        public void CancelCurrent()
        {
        }

        public void DropWaiting()
        {
        }

        public void Cancel(GitOperation operation)
        {
        }

        public void Dispose()
        {
        }
    }
}
