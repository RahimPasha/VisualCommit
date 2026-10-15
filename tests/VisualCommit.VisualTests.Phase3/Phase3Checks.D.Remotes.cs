// Part D's checks of remotes and upstreams (docs/test-reports/phase-3.md, checks 22, 28, 29 and
// 30): the Remotes dialog, setting and removing an upstream, pushing a branch without an upstream,
// pushing a tag and deleting it on a remote, fast-forwarding a branch that is not checked out,
// deleting a branch on a remote, and a drop refused for a branch checked out in another worktree.
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksD
{
    /// <summary>
    /// Check 22: the Remotes dialog from the section header's menu; add a remote with "Fetch now";
    /// change its URL to one with a token, shown hidden; rename it; remove it with the confirmation
    /// stacked over the dialog.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_22_remotes_dialog()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        var serverUrl = scenario.WithPort(PartD.ServerUrl);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the header's menu, then "Manage remotes…".
        var (menu, pointer) = await RightClickRowAsync(app, "Remotes");
        AssertMenu(menu, PartD.RemotesHeaderMenu);
        AssertHighlighted(menu, "Add remote…");
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Manage remotes…");
        var remotes = await app.WaitForDialogAsync<RemotesDialogViewModel>();
        await WaitForRemoteRowsAsync(app, remotes, "origin");
        Shot(app, "22a-remotes");
        AssertCard(app, "RemotesDialogCard", 460);
        Assert.Equal("Remotes", DialogText(app, "RemotesTitle"));
        Assert.Equal([("origin", serverUrl)], RemoteRows(app));
        AssertRemoteRowButtons(app, "origin");
        var add = DialogButton(app, "AddRemoteButton");
        Assert.True(add.IsEffectivelyVisible && "Add remote".Equals(add.Content));
        Assert.True(app.BoundsOf(add).Top >= app.BoundsOf(RemoteRowControl(app, "origin")).Bottom, "\"Add remote\" is not below the list.");
        Assert.Equal("Close", DialogButton(app, "CancelButton").Content);
        Assert.False(DialogButton(app, "ConfirmButton").IsEffectivelyVisible, "The list shows a second button.");
        AssertNoClippedText(app);

        // Step 2: the add form.
        ClickId(app, "AddRemoteButton");
        await AssertFocusedAsync(app, "RemoteNameBox");
        var view = app.TopDialogView!;
        Assert.Equal("Name", ((TextBox)app.FindByAutomationId(view, "RemoteNameBox")).PlaceholderText);
        Assert.Equal("URL", ((TextBox)app.FindByAutomationId(view, "RemoteUrlBox")).PlaceholderText);
        var fetchNow = (CheckBox)app.FindByAutomationId(view, "RemoteFetchOption");
        Assert.True(fetchNow.IsEffectivelyVisible && fetchNow.IsChecked == true, "\"Fetch now\" is not shown ticked.");
        Assert.Equal("Fetch now", fetchNow.Content);
        Assert.False(app.FindByAutomationId(view, "RemoteError").IsEffectivelyVisible, "RemoteError shows while empty.");
        Assert.Equal(("Cancel", "Save"), ((string)DialogButton(app, "CancelButton").Content!, (string)DialogButton(app, "ConfirmButton").Content!));
        Assert.False(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Save\" is enabled with an empty form.");
        Assert.False(RemoteRowControl(app, "origin").IsEffectivelyVisible, "The list shows beside the form.");
        app.TypeInto("RemoteNameBox", "mirror");
        Assert.False(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Save\" is enabled without a URL.");
        app.TypeInto("RemoteUrlBox", serverUrl);
        Shot(app, "22b-add-form");
        Assert.Equal("mirror", DialogText(app, "RemoteNameBox"));
        Assert.Equal(serverUrl, DialogText(app, "RemoteUrlBox"));
        Assert.False(app.FindByAutomationId(app.TopDialogView!, "RemoteError").IsEffectivelyVisible, "RemoteError shows.");
        Assert.True(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Save\" is disabled.");
        AssertNoClippedText(app);

        var calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.FetchedFromMirror);
        await WaitForRemoteRowsAsync(app, remotes, "mirror", "origin");
        Shot(app, "22c-mirror-added");
        Assert.Equal([("mirror", serverUrl), ("origin", serverUrl)], RemoteRows(app));
        AssertIdleStatus(app, PartD.FetchedFromMirror);
        var fetch = app.GitCalls().Skip(calls).Single(call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Contains(" * [new branch]      main       -> mirror/main", Lines(fetch.StandardError));
        Assert.Contains(" * [new branch]      published  -> mirror/published", Lines(fetch.StandardError));
        AssertSections(app, ("Local branches", 3), ("Remotes", 4), ("Pull requests", 0), ("Tags", 0), ("Stashes", 0));
        Assert.Equal([("mirror", true), ("origin", true)], Remotes(app));
        Assert.Equal(["main", "published"], RemoteChildren(app, "mirror"));
        Assert.Equal(["main", "published"], RemoteChildren(app, "origin"));
        AssertGraph(app, PartD.SyncMirrorGraph, lanes: 2);

        // Step 3: a URL with a token, shown hidden.
        ClickRemoteRowButton(app, "mirror", "RemoteEditButton");
        app.ReplaceText("RemoteUrlBox", PartD.TokenUrl);
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.ChangedUrlOfMirror);
        await app.WaitForAsync(() => !remotes.IsFormShown && RemoteRows(app).Contains(("mirror", PartD.HiddenTokenUrl)), "the list with mirror's new URL");
        Shot(app, "22d-token-hidden");
        Assert.Equal([("mirror", PartD.HiddenTokenUrl), ("origin", serverUrl)], RemoteRows(app));
        var url = RemoteRowControl(app, "mirror").GetVisualDescendants().OfType<TextBlock>().Single(block => AutomationProperties.GetAutomationId(block) == "RemoteUrl");
        AssertTip(url, PartD.HiddenTokenUrl);
        Assert.Equal(PartD.TokenUrl, (await repo.GitAsync("remote", "get-url", "mirror")).StandardOutput.Trim());
        AssertIdleStatus(app, PartD.ChangedUrlOfMirror);
        Assert.DoesNotContain(TextsIn(app.TopDialogView!), text => text.Contains(PartD.Token, StringComparison.Ordinal));

        // Step 4: rename; the edit form shows the URL as configured.
        ClickRemoteRowButton(app, "mirror", "RemoteEditButton");
        Assert.Equal(PartD.TokenUrl, DialogText(app, "RemoteUrlBox"));
        app.ReplaceText("RemoteNameBox", "backup");
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.RenamedMirror);
        await WaitForRemoteRowsAsync(app, remotes, "backup", "origin");
        Assert.Equal([("backup", PartD.HiddenTokenUrl), ("origin", serverUrl)], RemoteRows(app));
        AssertIdleStatus(app, PartD.RenamedMirror);
        Assert.Equal(["main", "published"], RemoteChildren(app, "backup"));

        // Step 5: Remove asks on top of the dialog.
        ClickRemoteRowButton(app, "backup", "RemoteRemoveButton");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Shot(app, "22e-remove-stacked");
        await AssertConfirmationAsync(app, PartD.RemoveBackupTitle, PartD.RemoveBackupQuestion, PartD.BackupDetail, "Remove");
        AssertStacked(app);
        AssertNoClippedText(app);
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.RemovedBackup);
        await app.WaitForDialogAsync<RemotesDialogViewModel>();
        await WaitForRemoteRowsAsync(app, remotes, "origin");
        Shot(app, "22f-removed");
        Assert.Equal([("origin", serverUrl)], RemoteRows(app));
        AssertIdleStatus(app, PartD.RemovedBackup);
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Single(backups.Select(backup => BackupRef.Parse(backup.Name).Time).Distinct());
        AssertBackup(backups.Single(backup => backup.Name.EndsWith("/backup/main", StringComparison.Ordinal)), "remote", "backup/main", await CommitBySubjectAsync(repo, "Shared work"));
        AssertBackup(backups.Single(backup => backup.Name.EndsWith("/backup/published", StringComparison.Ordinal)), "remote", "backup/published", await CommitBySubjectAsync(repo, "Published work"));
        AssertSections(app, ("Local branches", 3), ("Remotes", 2), ("Pull requests", 0), ("Tags", 0), ("Stashes", 0));
        Assert.Equal([("origin", true)], Remotes(app));
        AssertGraph(app, PartD.SyncGraph, lanes: 2);

        // Step 6: Close.
        ClickId(app, "CancelButton");
        await AssertNoDialogAsync(app);
        AssertNotWritten(app, PartD.Token);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(repo.Path, ".git"), "*", SearchOption.AllDirectories),
            file => File.ReadAllText(file, Encoding.Latin1).Contains(PartD.Token, StringComparison.Ordinal));
    }

    /// <summary>
    /// Check 28: set topic's upstream with the pick dialog (filter typed), change it, stop tracking.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_28_set_change_and_stop_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: topic's menu.
        var (menu, pointer) = await RightClickRowAsync(app, "Local branches", "topic");
        Shot(app, "28a-topic-menu");
        AssertMenu(menu, PartD.TopicMenu);
        AssertHighlighted(menu, "Check out topic");
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
        AssertNothingSelected(app);

        // Step 2: the pick dialog, filtered, an item clicked.
        app.ClickMenuItem("Set upstream…");
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        AssertCard(app, "PickDialogCard", 460);
        Assert.Equal(PartD.PickTitle, DialogText(app, "PickTitle"));
        Assert.Equal("Filter", ((TextBox)app.FindByAutomationId(app.TopDialogView!, "PickFilter")).PlaceholderText);
        await AssertFocusedAsync(app, "PickFilter");
        Assert.Equal(PartD.UpstreamCandidates.Select(item => (item, false)), PickItems(app));
        Assert.Equal("Set upstream", DialogButton(app, "ConfirmButton").Content);
        Assert.False(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Set upstream\" is enabled with nothing selected.");
        app.TypeInto("PickFilter", "pub");
        await app.WaitForAsync(() => PickItems(app).Count == 1, "the filtered list");
        Assert.Equal([("origin/published", false)], PickItems(app));
        app.Click(PickItem(app, "origin/published"));
        Shot(app, "28b-pick");
        Assert.Equal([("origin/published", true)], PickItems(app));
        Assert.True(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Set upstream\" is disabled with an item selected.");
        AssertNoClippedText(app);

        // Step 3.
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.TracksPublished);
        AssertIdleStatus(app, PartD.TracksPublished);
        AssertLocalBranch(app, "topic", ahead: 1, behind: 1, head: false);
        Assert.Equal("origin", await ConfigValueAsync(repo, "branch.topic.remote"));
        Assert.Equal("refs/heads/published", await ConfigValueAsync(repo, "branch.topic.merge"));
        AssertUnchangedSync(app);
        _ = pick;

        // Step 4: the menu now, then "Change upstream…".
        (menu, pointer) = await RightClickRowAsync(app, "Local branches", "topic");
        Shot(app, "28c-topic-menu-tracking");
        AssertMenu(menu, PartD.TopicTrackingMenu);
        AssertHighlighted(menu, "Check out topic");
        AssertMenuShape(app, menu);

        // Phase 5's "Compare topic with main", "Show reflog of topic", "Hide topic" and "Solo topic" make it too
        // high to fit below the pointer: 4a moves it (docs/test-reports/phase-5.md, "Changes to expected results": 28c).
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Change upstream…");
        await app.WaitForDialogAsync<PickDialogViewModel>();
        await app.WaitForAsync(() => PickItems(app).Count == 2, "both candidates");
        Assert.Equal([("origin/main", false), ("origin/published", true)], PickItems(app));
        app.Click(PickItem(app, "origin/main"));
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.TracksMain);
        AssertIdleStatus(app, PartD.TracksMain);
        AssertLocalBranch(app, "topic", ahead: 1, behind: 0, head: false);
        AssertUnchangedSync(app);

        // Step 5: stop tracking.
        await RightClickRowAsync(app, "Local branches", "topic");
        app.ClickMenuItem("Stop tracking origin/main");
        await WaitForResultAsync(app, PartD.StoppedTracking);
        Shot(app, "28d-stopped");
        AssertIdleStatus(app, PartD.StoppedTracking);
        AssertLocalBranch(app, "topic", ahead: 0, behind: 0, head: false);
        Assert.False((await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.topic\.")).Succeeded, "A branch.topic.* setting is left.");
        AssertUnchangedSync(app);
    }

    /// <summary>Check 29, steps 1 and 2: push topic, which has no upstream, with the push dialog.</summary>
    [AvaloniaFact]
    public async Task Check_29_push_a_branch_without_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the push dialog.
        await RightClickRowAsync(app, "Local branches", "topic");
        app.ClickMenuItem("Push topic…");
        await app.WaitForDialogAsync<PushDialogViewModel>();
        await AssertFocusedAsync(app, "PushBranchBox");
        Shot(app, "29a-push-dialog");
        AssertCard(app, "PushDialogCard", 400);
        Assert.Equal(PartD.PushTopicTitle, DialogText(app, "PushTitle"));
        var view = app.TopDialogView!;
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "To" && block.IsEffectivelyVisible);
        Assert.Equal("origin", DialogText(app, "PushRemote"));
        Assert.Empty(ChoiceOptions(app));
        Assert.Equal("topic", DialogText(app, "PushBranchBox"));
        var setUpstream = (CheckBox)app.FindByAutomationId(view, "PushSetUpstream");
        Assert.True(setUpstream.IsChecked == true && "Set as upstream".Equals(setUpstream.Content), "\"Set as upstream\" is not ticked.");
        Assert.Equal(["Cancel", "Push"], DialogButtons(app));
        AssertNoClippedText(app);

        // Step 2: Push.
        var calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.PushedTopic);
        Shot(app, "29b-pushed");
        AssertIdleStatus(app, PartD.PushedTopic);
        Assert.Contains(PartD.PushTopicCommand, app.GitCalls().Skip(calls).Select(call => call.CommandText));
        Assert.Equal("refs/heads/topic", await ConfigValueAsync(repo, "branch.topic.merge"));
        Assert.Equal(await CommitBySubjectAsync(repo, "Topic work"), await RevParseAsync(repo, "refs/remotes/origin/topic"));
        AssertSections(app, ("Local branches", 3), ("Remotes", 3), ("Pull requests", 0), ("Tags", 0), ("Stashes", 0));
        Assert.Equal([("origin", true)], Remotes(app));
        Assert.Equal(["main", "published", "topic"], RemoteChildren(app, "origin"));
        AssertLocalBranch(app, "topic", ahead: 0, behind: 0, head: false);
        Assert.Equal(["topic", "origin/topic"], Graph(app).Data!.LabelsAt(0).Select(label => label.Text));
        Assert.Equal("Topic work", Graph(app).Data!.CommitAt(0).Subject);
    }

    /// <summary>Check 29, steps 3 to 5: push the tag v1.0 to one of two remotes, then delete it there.</summary>
    [AvaloniaFact]
    public async Task Check_29_push_and_delete_a_tag_on_a_remote()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var tagObject = await RevParseAsync(repo, "refs/tags/v1.0");
        var addCore = await CommitBySubjectAsync(repo, "Add core");

        // Step 3: the tag's menu, then the choice dialog.
        var (menu, pointer) = await RightClickRowAsync(app, "Tags", "v1.0");
        AssertMenu(menu, PartD.TagMenu);
        AssertHighlighted(menu, "Check out v1.0");
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Push v1.0…");
        await app.WaitForDialogAsync<ChoiceDialogViewModel>();
        Shot(app, "29c-choice");
        AssertCard(app, "ChoiceDialogCard", 400);
        Assert.Equal(PartD.PushTagTitle, DialogText(app, "ChoiceTitle"));
        Assert.Equal([("mirror", true), ("origin", false)], ChoiceOptions(app));
        Assert.Equal(["Cancel", "Push"], DialogButtons(app));
        Assert.DoesNotContain("danger", DialogButton(app, "ConfirmButton").Classes);
        Assert.Equal([("mirror", true), ("origin", true)], Remotes(app));
        Assert.Empty(RemoteChildren(app, "mirror"));
        Assert.Equal(["feature", "main"], RemoteChildren(app, "origin"));
        Assert.False(app.RefRowModel("Remotes", "origin", "feature").IsOpen, "origin's folder feature is open.");
        AssertNoClippedText(app);

        // Step 4: to origin.
        app.Click(ChoiceOption(app, "origin"));
        var calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.PushedTag);
        AssertIdleStatus(app, PartD.PushedTag);
        Assert.Contains(PartD.PushTagCommand, app.GitCalls().Skip(calls).Select(call => call.CommandText));
        Assert.Equal([$"{tagObject}\trefs/tags/v1.0", $"{addCore}\trefs/tags/v1.0^{{}}"], Lines((await repo.GitAsync("ls-remote", "--tags", "origin")).StandardOutput));
        Assert.Empty(Lines((await repo.GitAsync("ls-remote", "--tags", "mirror")).StandardOutput));
        AssertGraph(app, PartD.OpsGraph, lanes: 3);

        // Step 5: delete it on origin.
        (menu, _) = await RightClickRowAsync(app, "Tags", "v1.0");
        AssertMenu(menu, PartD.TagMenu);
        app.ClickMenuItem("Delete v1.0 on a remote…");
        await app.WaitForDialogAsync<ChoiceDialogViewModel>();
        Shot(app, "29d-delete-choice");
        Assert.Equal(PartD.DeleteTagChoiceTitle, DialogText(app, "ChoiceTitle"));
        Assert.Equal([("mirror", true), ("origin", false)], ChoiceOptions(app));
        Assert.Equal(["Cancel", "Delete"], DialogButtons(app));
        Assert.True(DialogButton(app, "ConfirmButton").Classes.Contains("danger"), "\"Delete\" is not in the danger style.");
        app.Click(ChoiceOption(app, "origin"));
        ClickId(app, "ConfirmButton");
        await AssertConfirmationAsync(app, PartD.DeleteTagTitle, PartD.DeleteTagQuestion, PartD.BackupDetail, "Delete on origin");
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) == "ChoiceDialog" && control.IsEffectivelyVisible);
        calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.DeletedTag);
        Shot(app, "29e-tag-deleted-on-origin");
        AssertIdleStatus(app, PartD.DeletedTag);
        var delete = app.GitCalls().Skip(calls).Single(call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal($"git push --progress --force-with-lease=refs/tags/v1.0:{tagObject} origin --delete refs/tags/v1.0", delete.CommandText);
        Assert.Contains(" - [deleted]         v1.0", Lines(delete.StandardError));
        Assert.Empty(Lines((await repo.GitAsync("ls-remote", "--tags", "origin")).StandardOutput));
        AssertBackup(Assert.Single(await repo.BackupRefsAsync()), "remote-tag", "origin/v1.0", tagObject!);
        Assert.Equal(tagObject, await RevParseAsync(repo, "refs/tags/v1.0"));
        AssertGraph(app, PartD.OpsGraph, lanes: 3);
        Assert.Equal([("mirror", true), ("origin", true)], Remotes(app));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 30, steps 1 to 5: push published; fetch what was pushed from elsewhere; fast-forward
    /// published, which is not checked out; delete it on origin; its menu with the upstream gone.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_30_fast_forward_and_delete_on_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: push published.
        await RightClickRowAsync(app, "Local branches", "published");
        app.ClickMenuItem("Push published to origin/published");
        await WaitForResultAsync(app, PartD.PushedPublished);
        AssertIdleStatus(app, PartD.PushedPublished);
        AssertLocalBranch(app, "published", ahead: 0, behind: 0, head: false);
        Assert.Equal(["published", "origin/published"], LabelsOf(app, "Unpushed work"));

        // Step 2: pushed from elsewhere, fetched.
        await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nunpushed\nelsewhere\n", "Published elsewhere");
        app.Click(ToolbarButton(app, "FetchButton"));
        await WaitForResultAsync(app, PartD.FetchedFromAll);
        AssertIdleStatus(app, PartD.FetchedFromAll);
        AssertLocalBranch(app, "published", ahead: 0, behind: 1, head: false);
        Assert.Equal(["origin/published"], LabelsOf(app, "Published elsewhere"));
        Assert.Equal("2026-01-01 12:05", Graph(app).Dates!.Format(Graph(app).Data!.CommitAt(RowOf(app, "Published elsewhere")).AuthorDate));
        var elsewhere = await CommitBySubjectAsync(repo, "Published elsewhere");

        // Step 3: the fast-forward of a branch that is not checked out.
        var (menu, pointer) = await RightClickRowAsync(app, "Local branches", "published");
        Shot(app, "30a-fast-forward-enabled");
        AssertMenu(menu, PartD.BehindPublishedMenu);
        AssertHighlighted(menu, "Check out published");
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Fast-forward published to origin/published");
        await WaitForResultAsync(app, PartD.FastForwarded);
        Shot(app, "30b-fast-forwarded");
        AssertIdleStatus(app, PartD.FastForwarded);
        var fastForward = app.GitCalls().Skip(calls).Single(call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal("git fetch --no-auto-maintenance --no-write-fetch-head . refs/remotes/origin/published:refs/heads/published", fastForward.CommandText);
        Assert.Contains("From .", Lines(fastForward.StandardError));
        Assert.Matches(new Regex(@"(?m)^   8e65315\.\.[0-9a-f]{7}  origin/published -> published\r?$"), fastForward.StandardError);
        AssertLocalBranch(app, "published", ahead: 0, behind: 0, head: false);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        AssertGraph(app, PartD.SyncFastForwardedPublishedGraph, lanes: 2);

        // Step 4: delete published on origin.
        (menu, pointer) = await RightClickRowAsync(app, "Remotes", "origin", "published");
        Shot(app, "30c-remote-branch-menu");
        AssertMenu(menu, PartD.RemotePublishedMenu);
        AssertHighlighted(menu, "Check out origin/published");
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Delete published on origin…");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Shot(app, "30d-delete-confirmation");
        await AssertConfirmationAsync(app, PartD.DeleteBranchTitle, PartD.DeleteBranchQuestion, PartD.BackupDetail, "Delete on origin");
        calls = app.GitCalls().Count;
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.DeletedBranch);
        Shot(app, "30e-deleted");
        AssertIdleStatus(app, PartD.DeletedBranch);
        var delete = app.GitCalls().Skip(calls).Single(call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal($"git push --progress --force-with-lease=refs/heads/published:{elsewhere} origin --delete refs/heads/published", delete.CommandText);
        Assert.Contains(" - [deleted]         published", Lines(delete.StandardError));
        Assert.Equal(["refs/heads/main"], Lines((await repo.GitAsync("ls-remote", "--heads", "origin")).StandardOutput).Select(line => line.Split('\t')[1]));
        Assert.Null(await RevParseAsync(repo, "refs/remotes/origin/published"));
        AssertBackup(Assert.Single(await repo.BackupRefsAsync()), "remote-branch", "origin/published", elsewhere);
        AssertSections(app, ("Local branches", 3), ("Remotes", 1), ("Pull requests", 0), ("Tags", 0), ("Stashes", 0));
        Assert.Equal(["main"], RemoteChildren(app, "origin"));
        Assert.Equal(["published"], LabelsOf(app, "Published elsewhere"));
        Assert.Equal("refs/heads/published", await ConfigValueAsync(repo, "branch.published.merge"));
        Assert.Equal("[gone]", (await repo.GitAsync("for-each-ref", "--format=%(upstream:track)", "refs/heads/published")).StandardOutput.Trim());
        AssertLocalBranch(app, "published", ahead: 0, behind: 0, head: false);

        // Step 5: the menu with the upstream gone; the pointer rests on the disabled force push.
        (menu, pointer) = await RightClickRowAsync(app, "Local branches", "published");
        AssertMenu(menu, PartD.GonePublishedMenu);
        AssertHighlighted(menu, "Check out published");
        Assert.True(menu.Item("Push published to origin/published").IsEnabled);
        AssertMenuPlaced(app, menu, pointer);
        var tip = await app.RestPointerOnMenuItemAsync("Force push published…");
        Shot(app, "30f-upstream-gone", keepPointer: true);
        Assert.Equal(PartD.GoneForcePushTip, tip);
        AssertHighlighted(app.Menu(), "Check out published");
        var mark = app.LogMark();
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, mark);
    }

    /// <summary>
    /// Check 30, step 6: on edges, main dragged onto side, which is checked out in the linked
    /// worktree: phase 3's two drop entries are disabled with the worktree's reason; of phase 4's
    /// two, the rebase is disabled (main is up to date with side) and the interactive one is enabled.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_30_drop_refused_for_a_branch_in_another_worktree()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        // The folder as `git worktree list --porcelain` names it, in the platform's form (WorktreeInfo.Path).
        var worktree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Lines((await repo.GitAsync("worktree", "list", "--porcelain")).StandardOutput)
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => line["worktree ".Length..])
            .Single(path => path.EndsWith(Scenarios.EdgeCasesWorktree, StringComparison.Ordinal))));
        var refs = await RefsAsync(repo);
        var head = await repo.HeadAsync();
        var worktreeHead = (await repo.GitAsync("-C", worktree, "rev-parse", "HEAD")).StandardOutput.Trim();
        var selected = Graph(app).SelectedIndex;

        // Step 6: press on main, move past the threshold, over side, release.
        var from = app.BoundsOf(await app.RefRowAsync("Local branches", "main")).Center;
        var to = app.BoundsOf(await app.RefRowAsync("Local branches", "side")).Center;
        app.PressMouse(from);
        app.MoveMouseHeld(from + new Point(0, 8), steps: 2);
        app.MoveMouseHeld(to, steps: 4);
        app.ReleaseMouse(to);
        // Phase 4's group "Check out main and rebase it" follows phase 3's; its interactive item is
        // the only enabled one, so it has the highlight; phase 5's "Compare main with side" ends
        // it in a group of its own (180 high; 145 in phase 4, 84 before).
        var menu = app.Menu();
        AssertMenu(menu, PartD.WorktreeDropMenu(worktree));
        AssertHighlighted(menu, "Check out main and rebase it onto side interactively…");
        Assert.Equal(MenuHeight(items: 5, separators: 2), menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, to);
        var tip = await app.RestPointerOnMenuItemAsync("Fast-forward side to main");
        Shot(app, "30g-worktree-refused", keepPointer: true);
        Assert.Equal(PartD.WorktreeReason(worktree), tip);
        Assert.Equal(PartD.WorktreeReason(worktree), app.Menu().Item("Check out side and merge main into it").ToolTip);
        AssertNoClippedText(app);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Nothing changed: no graph selection, HEAD detached on "Detached work", the refs and the worktree as they were.
        Assert.Equal(selected, Graph(app).SelectedIndex);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("Detached work", await SubjectAsync(repo, "HEAD"));
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(worktreeHead, (await repo.GitAsync("-C", worktree, "rev-parse", "HEAD")).StandardOutput.Trim());
    }

    /// <summary>Check 28's "Throughout": main and published (↑1) unchanged, and the graph as sync's.</summary>
    private static void AssertUnchangedSync(ShellDriver app)
    {
        AssertLocalBranch(app, "main", ahead: 0, behind: 0, head: true);
        AssertLocalBranch(app, "published", ahead: 1, behind: 0, head: false);
        AssertGraph(app, PartD.SyncGraph, lanes: 2);
    }

    /// <summary>The labels of the row whose subject is <paramref name="subject"/>, in drawing order.</summary>
    private static List<string> LabelsOf(ShellDriver app, string subject)
    {
        var row = RowOf(app, subject);
        Assert.True(row >= 0, $"The graph has no row \"{subject}\".");
        return [.. Graph(app).Data!.LabelsAt(row).Select(label => label.Text)];
    }

    /// <summary>The lines of git's output, without empty ones and line ends.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0)];

    /// <summary>The Remotes dialog's rows: each remote's name and the URL its row shows.</summary>
    private static List<(string Name, string Url)> RemoteRows(ShellDriver app)
    {
        var view = app.TopDialogView!;
        return [.. view.GetVisualDescendants().OfType<Control>()
            .Where(control => AutomationProperties.GetAutomationId(control) == "RemoteRow" && control.IsEffectivelyVisible)
            .OrderBy(control => app.BoundsOf(control).Y)
            .Select(control => (
                AutomationProperties.GetName(control) ?? string.Empty,
                control.GetVisualDescendants().OfType<TextBlock>().Single(block => AutomationProperties.GetAutomationId(block) == "RemoteUrl").Text ?? string.Empty))];
    }

    /// <summary>The Remotes dialog's row of <paramref name="name"/>.</summary>
    private static Control RemoteRowControl(ShellDriver app, string name) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .Single(control => AutomationProperties.GetAutomationId(control) == "RemoteRow" && AutomationProperties.GetName(control) == name);

    /// <summary>Waits until the Remotes dialog shows its list with exactly these remotes.</summary>
    private static async Task WaitForRemoteRowsAsync(ShellDriver app, RemotesDialogViewModel remotes, params string[] names)
    {
        await app.WaitForAsync(
            () => app.Session.Services.DialogHost.Current == remotes && !remotes.IsFormShown && RemoteRows(app).Select(row => row.Name).SequenceEqual(names),
            "the Remotes dialog to list " + string.Join(", ", names));
    }

    /// <summary>Asserts a remote row's "Edit" and "Remove" (in the danger colour).</summary>
    private static void AssertRemoteRowButtons(ShellDriver app, string name)
    {
        var buttons = RemoteRowControl(app, name).GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToList();
        Assert.Equal(["Edit", "Remove"], buttons.OrderBy(button => app.BoundsOf(button).X).Select(button => (string)button.Content!));
        var remove = buttons.Single(button => "Remove".Equals(button.Content));
        Assert.Equal(PaletteOf(app).Danger, (remove.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>Clicks one of a remote row's buttons (<c>RemoteEditButton</c>, <c>RemoteRemoveButton</c>).</summary>
    private static void ClickRemoteRowButton(ShellDriver app, string name, string automationId)
    {
        var button = RemoteRowControl(app, name).GetVisualDescendants().OfType<Button>()
            .Single(candidate => AutomationProperties.GetAutomationId(candidate) == automationId);
        app.Click(button);
    }

    /// <summary>Check 22 step 5: the confirmation on top of the Remotes dialog, each on a backdrop of its own, the Remotes dialog still drawn.</summary>
    private static void AssertStacked(ShellDriver app)
    {
        var remotes = app.Window.GetVisualDescendants().OfType<Control>()
            .Single(control => AutomationProperties.GetAutomationId(control) == "RemotesDialog");
        Assert.True(remotes.IsEffectivelyVisible, "The Remotes dialog is not drawn under the confirmation.");
        var backdrops = app.Window.GetVisualDescendants().OfType<Panel>()
            .Count(panel => panel.Classes.Contains("dialogBackdrop") && panel.IsEffectivelyVisible);
        Assert.Equal(2, backdrops);
    }

    /// <summary>The pick dialog's listed items, in order, each with whether it is selected.</summary>
    private static List<(string Item, bool Selected)> PickItems(ShellDriver app)
    {
        var list = (ListBox)app.FindByAutomationId(app.TopDialogView!, "PickList");
        return [.. list.GetVisualDescendants().OfType<ListBoxItem>()
            .Where(item => item.IsEffectivelyVisible)
            .OrderBy(item => app.BoundsOf(item).Y)
            .Select(item => ((string)item.DataContext!, item.IsSelected))];
    }

    /// <summary>The pick dialog's item <paramref name="text"/>.</summary>
    private static ListBoxItem PickItem(ShellDriver app, string text)
    {
        var list = (ListBox)app.FindByAutomationId(app.TopDialogView!, "PickList");
        return list.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.IsEffectivelyVisible && text.Equals(item.DataContext));
    }
}
