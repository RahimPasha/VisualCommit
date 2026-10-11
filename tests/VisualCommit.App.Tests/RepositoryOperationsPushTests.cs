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
/// classes, so that xunit runs them side by side:
/// <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
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

        public void Dispose()
        {
        }
    }
}
