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
/// WP7-remotes: managing remotes and tags on a remote in <see cref="RepositoryOperations"/>
/// (docs/phase-3-design.md 2.23, 2.29), that nothing of the remote operations runs after the tab
/// closed, and the dialogs of the remote operations through the window,
/// against the scenario repositories, through <see cref="OperationsHarness"/>: for each, its
/// confirmation, the backup it saves, what git holds afterwards, the status bar's text, its
/// failure dialog with git's own output and its extra button, its cancel, and that a second
/// click runs nothing more. Folder remotes serve most of them. WP7-remotes' tests are four
/// classes, so that xunit runs them side by side:
/// <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
public class RepositoryOperationsRemotesTests
{
    // 2.23: remotes.

    [Fact]
    public async Task Add_rename_change_the_url_and_remove_a_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var shared = await repo.RefTargetAsync("refs/heads/main");
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        using var app = await OperationsHarness.OpenAsync(repo);
        IRemoteActions remotes = app.Operations;
        const string Token = "https://alice:ghp_SECRET123@127.0.0.1:1/x.git";
        app.Dialogs.Confirm(yes: false).Confirm();

        // Add with "Fetch now": the fetch is an operation of its own, under its own title.
        Assert.Null(await remotes.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), fetchNow: true));
        await app.WaitForIdleAsync();
        Assert.Equal("Fetched from mirror", app.StatusText);
        Assert.Equal(shared, await repo.RefTargetAsync("refs/remotes/mirror/main"));
        Assert.Contains(app.Log.Messages, message => message.Contains(": Add remote mirror: Succeeded after ", StringComparison.Ordinal));

        // Git's refusal goes back to the form, without a dialog.
        var refused = await remotes.AddRemoteAsync("origin", repo.SiblingPath("origin.git"), fetchNow: true);
        Assert.Equal("error: remote origin already exists.", refused);
        Assert.Equal("Add remote origin failed", app.StatusText);

        Assert.Null(await remotes.SetRemoteUrlAsync("mirror", Token));
        Assert.Equal("Changed the URL of mirror", app.StatusText);
        Assert.Equal(Token, (await repo.GitAsync("remote", "get-url", "mirror")).StandardOutput.Trim());
        Assert.Equal("https://***@127.0.0.1:1/x.git", (await remotes.ReadRemotesAsync()).Single(remote => remote.Name == "mirror").ShownUrl);

        Assert.Null(await remotes.RenameRemoteAsync("mirror", "backup"));
        Assert.Equal("Renamed remote mirror to backup", app.StatusText);
        Assert.Equal(["backup", "origin"], app.Operations.RemoteNames);

        // Remove: declined, then confirmed; the backup holds every remote branch.
        await remotes.RemoveRemoteAsync("backup");
        Assert.Equal(["backup", "origin"], app.Operations.RemoteNames);
        await remotes.RemoveRemoteAsync("backup");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Remove remote backup?",
                "Remove the remote backup and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was https://***@127.0.0.1:1/x.git.",
                "A backup is saved first, so this can be undone.",
                "Remove",
                true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Removed remote backup", app.StatusText);
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Matches("^refs/visualcommit/backup/remote-[0-9]{8}T[0-9]{9}Z/backup/main$", backups[0].Name);
        Assert.Equal((shared, published), (backups[0].Sha, backups[1].Sha));
        Assert.Equal(backups[0].Name[..^"main".Length] + "published", backups[1].Name);

        // D103: the token is in no log line, and nowhere under .git once the remote is gone.
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("ghp_SECRET123", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(repo.Path, ".git"), "*", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains("ghp_SECRET123", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_remotes_dialog_opens_on_its_list_its_add_form_or_a_remotes_form_and_removes_with_the_confirmation_on_top()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var url = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Keep().Keep().Keep();

        var managing = app.Operations.ManageRemotesAsync();
        var list = await app.Dialogs.WaitForAsync<RemotesDialogViewModel>();
        Assert.Equal((true, "origin", url), (list.IsListShown, list.Rows.Single().Name, list.Rows.Single().ShownUrl));
        list.Cancel();
        await managing.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        var adding = app.Operations.ShowAddRemoteAsync();
        await WaitUntilAsync(() => app.Dialogs.Shown.Count == 2, "the second dialog");
        var add = Assert.IsType<RemotesDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal((true, true, string.Empty, true), (add.IsFormShown, add.IsAdding, add.RemoteName, add.FetchNow));
        add.HandleEscape();
        add.HandleEscape();
        await adding.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        app.Dialogs.Confirm();
        var editing = app.Operations.ShowEditRemoteAsync("origin");
        await WaitUntilAsync(() => app.Dialogs.Shown.Count == 3, "the third dialog");
        var edit = Assert.IsType<RemotesDialogViewModel>(app.Dialogs.Shown[2]);
        Assert.Equal((true, false, "origin", url), (edit.IsFormShown, edit.IsAdding, edit.RemoteName, edit.RemoteUrl));
        edit.HandleEscape();
        await edit.RemoveCommand.ExecuteAsync(edit.Rows.Single());

        // The confirmation came on top of the dialog, which stays open and lists nothing now.
        Assert.Equal("Remove remote origin?", app.Dialogs.Confirmations.Single().Title);
        Assert.Same(edit, app.DialogHost.Current);
        Assert.Empty(edit.Rows);
        Assert.Equal("Removed remote origin", app.StatusText);
        edit.Cancel();
        await editing.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        app.AssertScriptFollowed();
    }

    // 2.29: tags on a remote.

    [Fact]
    public async Task Push_a_tag_to_the_one_remote_then_delete_it_there_with_a_backup_and_a_lease()
    {
        using var repo = await Scenarios.OperationsAsync();
        var tagObject = await repo.RefTargetAsync("refs/tags/v1.0");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm().Confirm().CloseError();

        await app.Operations.PushTagAsync("refs/tags/v1.0");

        Assert.Equal("Pushed tag v1.0 to origin", app.StatusText);
        Assert.Equal(tagObject, await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Empty(app.Dialogs.Shown);

        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");

        var confirmation = app.Dialogs.Confirmations[0];
        Assert.Equal(
            ("Delete v1.0 on origin?", "Delete the tag v1.0 on origin? Everyone who uses origin loses it.", "A backup is saved first, so this can be undone.", "Delete on origin", true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Deleted tag v1.0 on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Equal(tagObject, await repo.RefTargetAsync("refs/tags/v1.0"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/remote-tag-[0-9]{8}T[0-9]{9}Z/origin/v1.0$", backup.Name);
        Assert.Equal(tagObject, backup.Sha);

        // A remote without the tag: the lease refuses, and the deletion says so.
        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.1");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deletion stopped", "origin has no tag v1.1, or its v1.1 is not the same as yours."), (error.Title, error.Summary));
        Assert.Contains("stale info", error.Output, StringComparison.Ordinal);
        Assert.Equal(("Delete tag v1.1 on origin failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task With_several_remotes_a_tag_is_pushed_and_deleted_on_the_remote_chosen()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        var tagObject = await repo.RefTargetAsync("refs/tags/v1.0");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Cancel().Choose("origin").Choose("mirror").Confirm(yes: false).Choose("origin").Confirm();

        await app.Operations.PushTagAsync("refs/tags/v1.0");
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await app.Operations.PushTagAsync("refs/tags/v1.0");

        var choice = Assert.IsType<ChoiceDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal(("Push v1.0 to…", "Push", false), (choice.Title, choice.ConfirmText, choice.IsDestructive));
        Assert.Equal(["mirror", "origin"], choice.Options.Select(option => option.Text));
        Assert.Equal("Pushed tag v1.0 to origin", app.StatusText);
        Assert.Equal(tagObject, await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0", "mirror"));

        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");
        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");

        var deleteChoice = Assert.IsType<ChoiceDialogViewModel>(app.Dialogs.Shown[2]);
        Assert.Equal(("Delete v1.0 on…", "Delete", true), (deleteChoice.Title, deleteChoice.ConfirmText, deleteChoice.IsDestructive));
        Assert.Equal(["Delete v1.0 on mirror?", "Delete v1.0 on origin?"], app.Dialogs.Confirmations.Select(request => request.Title));
        Assert.Equal("Deleted tag v1.0 on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        app.AssertScriptFollowed();
    }

    // A closed tab.

    [Fact]
    public async Task After_the_tab_closed_no_remote_operation_starts_or_shows_anything()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        var remoteMain = await RemoteHeadAsync(repo, "refs/heads/main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Repository.Dispose();

        await app.Operations.FetchAllAsync();
        await app.Operations.FetchAsync("origin");
        await app.Operations.PullAsync();
        await app.Operations.PushCurrentBranchAsync();
        await app.Operations.PushBranchAsync("refs/heads/topic");
        await app.Operations.ForcePushAsync("refs/heads/published");
        await app.Operations.SetUpstreamAsync("refs/heads/topic");
        await app.Operations.UnsetUpstreamAsync("refs/heads/main");
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        await app.Operations.ManageRemotesAsync();
        await app.Operations.RemoveRemoteAsync("origin");
        await app.Operations.PushTagAsync("refs/tags/none");
        IRemoteActions remotes = app.Operations;
        Assert.NotNull(await remotes.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), fetchNow: true));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(remoteMain, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.main.merge"));
        Assert.NotNull(await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [AvaloniaFact]
    public async Task Through_the_window_the_dialogs_of_the_remote_operations_open_and_their_results_show()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();
        var status = (TextBlock)app.FindByAutomationId("OperationStatus");
        var operations = repository.Operations;

        // Check 19 steps 1 and 2: the toolbar's Push is rejected; "Pull" pulls.
        app.Click(app.FindByAutomationId("PushButton"));
        var rejected = await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-19a-push-rejected");
        Assert.Equal(("Push rejected", "Pull"), (rejected.Title, rejected.ExtraAction));
        Assert.Equal("Push main failed", status.Text);
        app.Click(app.FindByAutomationId("ErrorExtraButton"));
        await app.WaitForAsync(() => status.Text == "Pulled and merged origin/main into main", "the pull", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-19b-pulled");
        Assert.False(app.Session.Services.DialogHost.IsOpen);

        // Check 28 step 2: the pick dialog, filtered, an item clicked.
        var picking = operations.SetUpstreamAsync("refs/heads/topic");
        await app.WaitForDialogAsync<PickDialogViewModel>(NetworkTimeout);
        app.TypeInto("PickFilter", "pub");
        app.Click(DialogControl(app, "PickItem", "origin/published"));
        app.Settle();
        TestPictures.Save(app, "remotes-28b-pick");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => picking.IsCompleted, "the upstream to be set", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        Assert.Equal("topic now tracks origin/published", status.Text);
        await operations.UnsetUpstreamAsync("refs/heads/topic");
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        Assert.Equal("topic no longer tracks origin/published", status.Text);

        // Check 29 steps 1 and 2: the push dialog of a branch without an upstream.
        var pushing = operations.PushBranchAsync("refs/heads/topic");
        await app.WaitForDialogAsync<PushDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-29a-push-dialog");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => pushing.IsCompleted, "the push", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-29b-pushed");
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", status.Text);

        // Check 20's confirmation, cancelled.
        var forcing = operations.ForcePushAsync("refs/heads/published");
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-20c-force-push-confirmation");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => forcing.IsCompleted, "the cancelled force push", NetworkTimeout);
        Assert.Empty(await repo.BackupRefsAsync());

        // Check 30 step 4: delete a branch on its remote.
        var deleting = operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-30d-delete-confirmation");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => deleting.IsCompleted, "the deletion", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-30e-deleted");
        Assert.Equal("Deleted published on origin", status.Text);

        // Check 22: the Remotes dialog, its form, and the removal's confirmation stacked over it (cancelled).
        var managing = operations.ManageRemotesAsync();
        await app.WaitForDialogAsync<RemotesDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-22a-remotes");
        app.Click(app.FindByAutomationId("AddRemoteButton"));
        app.TypeInto("RemoteNameBox", "mirror");
        app.Settle();
        TestPictures.Save(app, "remotes-22-add-form");
        app.Click(app.FindByAutomationId("CancelButton"));
        app.Click(app.FindByAutomationId("RemoteRemoveButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-22d-remove-stacked");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForDialogAsync<RemotesDialogViewModel>(NetworkTimeout);
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => managing.IsCompleted, "the Remotes dialog to close", NetworkTimeout);
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
    }

    [AvaloniaFact]
    public async Task Through_the_window_a_tag_is_pushed_to_the_remote_chosen_in_the_choice_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();

        var pushing = repository.Operations.PushTagAsync("refs/tags/v1.0");
        await app.WaitForDialogAsync<ChoiceDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-29c-choice");
        app.Click(DialogControl(app, "ChoiceOption", "origin"));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => pushing.IsCompleted, "the push", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);

        Assert.Equal("Pushed tag v1.0 to origin", ((TextBlock)app.FindByAutomationId("OperationStatus")).Text);
        Assert.Equal(await repo.RefTargetAsync("refs/tags/v1.0"), await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0", "mirror"));
    }

    /// <summary>The control <paramref name="automationId"/> of the dialog on top that stands for <paramref name="text"/> (a list item, a radio button).</summary>
    private static Control DialogControl(ShellDriver app, string automationId, string text)
    {
        var dialog = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
        return dialog.GetVisualDescendants().OfType<Control>().First(control =>
            AutomationProperties.GetAutomationId(control) == automationId
            && control.IsEffectivelyVisible
            && (control.DataContext is string item ? item == text : control.DataContext is ChoiceOption option && option.Text == text));
    }
}
