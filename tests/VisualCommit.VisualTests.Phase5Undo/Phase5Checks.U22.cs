// Check U22 of part U (docs/test-reports/phase-5.md): undo and redo of local configuration: an
// upstream set, a remote added (with "Fetch now"), renamed, given another URL and removed (its keys,
// its branches' upstreams and its remote branches back), a file ignored; "sync", "ops", dark,
// 1100×700. One method per action with its undo and redo, each on a fresh copy.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23.Direction;
using Steps = VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>U22 part A: topic's upstream set with the pick dialog, undone (its keys removed) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U22_set_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: "Set upstream…", filtered to origin/published.
        await Steps.OpenRowMenuAsync(app, "Local branches", "topic");
        app.ClickMenuItem("Set upstream…");
        await app.WaitForDialogAsync<PickDialogViewModel>();
        app.TypeInto("PickFilter", "pub");
        var list = (ListBox)app.FindByAutomationId(app.TopDialogView!, "PickList");
        await app.WaitForAsync(() => list.GetVisualDescendants().OfType<ListBoxItem>().Count(item => item.IsEffectivelyVisible) == 1, "the filtered list");
        app.Click(list.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.IsEffectivelyVisible && "origin/published".Equals(item.DataContext)));
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Set upstream of topic", mark);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.SetUpstream);
        AssertStatus(app, PartU2.TracksPublished);
        await U22.AssertUpstreamSetAsync(app, repo);

        // Step 2: Undo: the keys removed.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.UpstreamText);
        var shot = Shot(app, "u22a-upstream-undone");
        Steps.AssertSucceeded(app, calls, PartU2.RemoveTopicSection);
        Assert.Empty(await Steps.ConfigAsync(repo, @"^branch\.topic\."));
        Assert.Equal((0, 0), Steps.Arrows(app, "topic"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.UpstreamText));

        // Step 3: Redo: the keys added again, in order.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.UpstreamText);
        shot = Shot(app, "u22b-upstream-redone");
        AssertCalledInOrder(
            app,
            calls,
            "git config --local --add branch.topic.remote origin",
            "git config --local --add branch.topic.merge refs/heads/published");
        await U22.AssertUpstreamSetAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U22 part B: mirror added with "Fetch now" (the copy's origin.git), undone (its keys and its fetched refs gone) and redone without a fetch.</summary>
    [AvaloniaFact]
    public async Task Check_U22_add_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var config = await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config"));
        var url = await Steps.OriginUrlAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 4: "Add remote", mirror at origin.git, "Fetch now" ticked; Save; the fetch; Close.
        var remotes = await Steps.OpenRemotesAsync(app, "origin");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "AddRemoteButton"));
        app.TypeInto("RemoteNameBox", "mirror");
        app.TypeInto("RemoteUrlBox", url);
        Assert.True(((CheckBox)app.FindByAutomationId(app.TopDialogView!, "RemoteFetchOption")).IsChecked == true, "\"Fetch now\" is not ticked.");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Fetch mirror", mark, NetworkTimeout);
        await Steps.WaitForRemoteRowsAsync(app, remotes, "mirror", "origin");
        Steps.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(app, calls, PartU2.Command("remote", "add", "--", "mirror", url), PartU2.FetchMirror);
        var fetch = Steps.AssertSucceeded(app, calls, PartU2.FetchMirror);
        await Steps.AssertPrintedAsync(fetch, " * [new branch]      main       -> mirror/main");
        await Steps.AssertPrintedAsync(fetch, " * [new branch]      published  -> mirror/published");
        AssertStatus(app, PartU2.FetchedFromMirror);
        Assert.Equal([$"remote.mirror.url {url}", "remote.mirror.fetch +refs/heads/*:refs/remotes/mirror/*"], await Steps.ConfigAsync(repo, @"^remote\.mirror\."));
        Assert.Equal(
            [$"refs/remotes/mirror/main {PartU2.SharedWork}", $"refs/remotes/mirror/published {PartU2.PublishedWork}"],
            await Steps.RefsUnderAsync(repo, "refs/remotes/mirror/"));
        Assert.Equal(4, Steps.SectionCount(app, "Remotes"));
        Assert.Equal(["mirror (open)", "  main", "  published", .. U22.OriginRemotes], Steps.SectionLines(app, "Remotes"));
        AssertGraph(app, PartU2.SyncWithMirror, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.AddRemoteText));

        // Step 5: Undo: the remote's section removed, and its fetched refs deleted in the step's transaction (7.12).
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.AddRemoteText);
        var shot = Shot(app, "u22c-add-undone");
        Steps.AssertSucceeded(app, calls, "git config --local --remove-section remote.mirror");
        var transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.UndoMessage(PartU2.AddRemoteText)));
        Assert.Equal(
            [$"delete refs/remotes/mirror/main {PartU2.SharedWork}", $"delete refs/remotes/mirror/published {PartU2.PublishedWork}"],
            Steps.Lines(transaction.StandardInput ?? string.Empty).Order(StringComparer.Ordinal));
        Assert.Equal(["origin"], await Steps.GitLinesAsync(repo, "remote"));
        Assert.Empty(await Steps.RefsUnderAsync(repo, "refs/remotes/mirror/"));
        Assert.Equal(config, await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config")));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
        Assert.Equal(U22.OriginRemotes, Steps.SectionLines(app, "Remotes"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.AddRemoteText));

        // Step 6: Redo: the keys added again, and no fetch: the state right after the add.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.AddRemoteText);
        shot = Shot(app, "u22d-add-redone");
        AssertCalledInOrder(
            app,
            calls,
            PartU2.Command("config", "--local", "--add", "remote.mirror.url", url),
            "git config --local --add remote.mirror.fetch +refs/heads/*:refs/remotes/mirror/*");
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal([$"remote.mirror.url {url}", "remote.mirror.fetch +refs/heads/*:refs/remotes/mirror/*"], await Steps.ConfigAsync(repo, @"^remote\.mirror\."));
        Assert.Empty(await Steps.RefsUnderAsync(repo, "refs/remotes/mirror/"));
        Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
        Assert.Equal(["mirror (open)", .. U22.OriginRemotes], Steps.SectionLines(app, "Remotes"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.AddRemoteText));
        AssertNoClippedText(app);
    }

    /// <summary>U22 part C: origin renamed upstream in the Remotes dialog, undone (.git/config byte for byte as before) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U22_rename_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var config = await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config"));
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 7: origin's "Edit", the name replaced by upstream; Save; Close.
        var remotes = await Steps.OpenRemotesAsync(app, "origin");
        Steps.ClickRemoteRowButton(app, "origin", "RemoteEditButton");
        app.ReplaceText("RemoteNameBox", "upstream");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Rename remote origin", mark);
        await Steps.WaitForRemoteRowsAsync(app, remotes, "upstream");
        Steps.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.RenameRemote);
        AssertStatus(app, PartU2.RenamedRemote);
        await U22.AssertRenamedAsync(app, repo);

        // Step 8: Undo: renamed back.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.RenameRemoteText);
        var shot = Shot(app, "u22e-rename-undone");
        Steps.AssertSucceeded(app, calls, PartU2.RenameRemoteBack);
        Assert.Equal(config, await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config")));
        Assert.Equal(
            [$"refs/remotes/origin/main {PartU2.SharedWork}", $"refs/remotes/origin/published {PartU2.PublishedWork}"],
            await Steps.RefsUnderAsync(repo, "refs/remotes/"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Assert.Equal(U22.OriginRemotes, Steps.SectionLines(app, "Remotes"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.RenameRemoteText));

        // Step 9: Redo: renamed again.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.RenameRemoteText);
        shot = Shot(app, "u22f-rename-redone");
        Steps.AssertSucceeded(app, calls, PartU2.RenameRemote);
        await U22.AssertRenamedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U22 part D: origin's URL changed in the Remotes dialog, undone (the URL read at the start back) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U22_change_url()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var url = await Steps.OriginUrlAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 10: origin's "Edit", the URL replaced; Save; Close.
        var remotes = await Steps.OpenRemotesAsync(app, "origin");
        Steps.ClickRemoteRowButton(app, "origin", "RemoteEditButton");
        app.ReplaceText("RemoteUrlBox", PartU2.ExampleUrl);
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Change URL of origin", mark);
        await Steps.WaitForRemoteRowsAsync(app, remotes, "origin");
        Steps.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartU2.ChangedUrl);
        await U22.AssertUrlChangedAsync(app, repo);

        // Step 11: Undo: the URL read at the start back.
        var calls = await Steps.StepAsync(app, repo, Undo, PartU2.UrlText);
        var shot = Shot(app, "u22g-url-undone");
        Steps.AssertSucceeded(app, calls, PartU2.Command("remote", "set-url", "--", "origin", url));
        Assert.Equal(url, await Steps.OriginUrlAsync(repo));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.UrlText));

        // Step 12: Redo: the new URL again.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.UrlText);
        shot = Shot(app, "u22h-url-redone");
        Steps.AssertSucceeded(app, calls, PartU2.Command("remote", "set-url", "--", "origin", PartU2.ExampleUrl));
        await U22.AssertUrlChangedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U22 part E: origin removed with the confirmation stacked over the Remotes dialog, undone (its keys, upstreams and remote branches back) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U22_remove_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var config = await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config"));
        var keys = await Steps.GitLinesAsync(repo, "config", "--local", "--list");
        var url = await Steps.OriginUrlAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        string[] backups =
        [
            $"{PartU2.FirstBackup("remote", "origin/main")} {PartU2.SharedWork}",
            $"{PartU2.FirstBackup("remote", "origin/published")} {PartU2.PublishedWork}",
        ];

        // Step 13: origin's "Remove", then "Remove" in the confirmation over the dialog; Close.
        var remotes = await Steps.OpenRemotesAsync(app, "origin");
        Steps.ClickRemoteRowButton(app, "origin", "RemoteRemoveButton");
        await AssertConfirmationAsync(app, PartU2.RemoveRemoteTitle, PartU2.RemoveRemoteQuestion(url), PartU2.BackupDetail, "Remove");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Remove remote origin", mark);
        await Steps.WaitForRemoteRowsAsync(app, remotes);
        Steps.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.RemoveRemote);
        AssertStatus(app, PartU2.RemovedRemote);
        await U22.AssertRemoteRemovedAsync(app, repo, backups);

        // Step 14: Undo: the remote's keys and its branches' upstreams added, its remote branches created.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.RemoveRemoteText);
        var shot = Shot(app, "u22i-remove-undone");
        foreach (var command in new[]
        {
            PartU2.Command("config", "--local", "--add", "remote.origin.url", url),
            "git config --local --add remote.origin.fetch +refs/heads/*:refs/remotes/origin/*",
            "git config --local --add branch.main.remote origin",
            "git config --local --add branch.main.merge refs/heads/main",
            "git config --local --add branch.published.remote origin",
            "git config --local --add branch.published.merge refs/heads/published",
        })
        {
            Steps.AssertSucceeded(app, calls, command);
        }

        var transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.UndoMessage(PartU2.RemoveRemoteText)));
        Assert.Equal(
            [$"create refs/remotes/origin/main {PartU2.SharedWork}", $"create refs/remotes/origin/published {PartU2.PublishedWork}"],
            Steps.Lines(transaction.StandardInput ?? string.Empty).Order(StringComparer.Ordinal));
        Assert.Equal(["origin"], await Steps.GitLinesAsync(repo, "remote"));
        Assert.Equal(keys.Order(StringComparer.Ordinal), (await Steps.GitLinesAsync(repo, "config", "--local", "--list")).Order(StringComparer.Ordinal));
        Assert.Equal(config, await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "config")));
        Assert.Equal(PartU2.OnMain, (await Steps.StatusAsync(repo))[0]);
        Assert.Equal((1, 0), Steps.Arrows(app, "published"));
        AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
        Assert.Equal(U22.OriginRemotes, Steps.SectionLines(app, "Remotes"));
        Assert.Equal(backups, await Steps.BackupLinesAsync(repo));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.RemoveRemoteText));

        // Step 15: Redo: the remote branches deleted, then the sections removed.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.RemoveRemoteText);
        shot = Shot(app, "u22j-remove-redone");
        transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.RedoMessage(PartU2.RemoveRemoteText)));
        Assert.Equal(
            [$"delete refs/remotes/origin/main {PartU2.SharedWork}", $"delete refs/remotes/origin/published {PartU2.PublishedWork}"],
            Steps.Lines(transaction.StandardInput ?? string.Empty).Order(StringComparer.Ordinal));
        foreach (var section in new[] { "branch.main", "branch.published", "remote.origin" })
        {
            AssertCalledInOrder(app, calls, transaction.CommandText, $"git config --local --remove-section {section}");
        }

        await U22.AssertRemoteRemovedAsync(app, repo, backups, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U22 part F: "Ignore run.log" on an untracked file, undone (the .gitignore it created deleted) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U22_ignore()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("logs/run.log", PartU2.RunLog);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 16: the working-changes row, then "Ignore run.log" on the unstaged file.
        app.Click(app.WorkingRowPoint());
        await Steps.WaitForUnstagedAsync(app, "Unstaged files (1)");
        Assert.Equal(["A run.log logs"], Steps.FileRows(app, "UnstagedFileList"));
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "logs/run.log"));
        app.Menu();
        await Steps.RunMenuItemAsync(app, repo, "Ignore run.log", "Ignore run.log");
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartU2.Ignored);
        await U22.AssertIgnoredAsync(app, repo);

        // Step 17: Undo: the .gitignore the app created deleted.
        await Steps.StepAsync(app, repo, Undo, PartU2.IgnoreText);
        await Steps.WaitForUnstagedAsync(app, "Unstaged files (1)");
        var shot = Shot(app, "u22k-ignore-undone");
        Assert.False(File.Exists(Path.Combine(repo.Path, ".gitignore")), ".gitignore is still there.");
        Assert.Equal([PartU2.OnMain, "?? logs/run.log"], await Steps.StatusAsync(repo));
        Assert.Equal(["A run.log logs"], Steps.FileRows(app, "UnstagedFileList"));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.IgnoreText));

        // Step 18: Redo: .gitignore again.
        await Steps.StepAsync(app, repo, Redo, PartU2.IgnoreText);
        shot = Shot(app, "u22l-ignore-redone");
        await U22.AssertIgnoredAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>The helpers of check U22's methods.</summary>
    private static class U22
    {
        /// <summary>The sync scenario's left panel under Remotes as it is built: origin, open, with main and published.</summary>
        public static readonly string[] OriginRemotes = ["origin (open)", "  main", "  published"];

        /// <summary>U22 after steps 1 and 3: topic tracks origin/published (↑1 ↓1), the graph unchanged, T+.</summary>
        public static async Task AssertUpstreamSetAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(["branch.topic.remote origin", "branch.topic.merge refs/heads/published"], await Steps.ConfigAsync(repo, @"^branch\.topic\."));
            Assert.Equal((1, 1), Steps.Arrows(app, "topic"));
            AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.UpstreamText));
        }

        /// <summary>U22 after steps 7 and 9: the remote-tracking refs and the upstreams under upstream, S-up, T+.</summary>
        public static async Task AssertRenamedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(["upstream"], await Steps.GitLinesAsync(repo, "remote"));
            Assert.Equal(
                [$"refs/remotes/upstream/main {PartU2.SharedWork}", $"refs/remotes/upstream/published {PartU2.PublishedWork}"],
                await Steps.RefsUnderAsync(repo, "refs/remotes/"));
            Assert.Equal(["branch.main.remote upstream"], await Steps.ConfigAsync(repo, @"^branch\.main\.remote$"));
            Assert.Equal(["branch.published.remote upstream"], await Steps.ConfigAsync(repo, @"^branch\.published\.remote$"));
            AssertGraph(app, PartU2.SyncRenamed, PartU2.SyncLanes, picture: picture);
            Assert.Equal(2, Steps.SectionCount(app, "Remotes"));
            Assert.Equal(["upstream (open)", "  main", "  published"], Steps.SectionLines(app, "Remotes"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.RenameRemoteText));
        }

        /// <summary>U22 after steps 10 and 12: origin's URL is the example's, the graph unchanged, T+.</summary>
        public static async Task AssertUrlChangedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(PartU2.ExampleUrl, await Steps.OriginUrlAsync(repo));
            AssertGraph(app, PartU2.Sync, PartU2.SyncLanes, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.UrlText));
        }

        /// <summary>U22 after steps 13 and 15: no remote, no upstream keys, no remote branches, the backups, S-none, Remotes 0, T+.</summary>
        public static async Task AssertRemoteRemovedAsync(ShellDriver app, TempRepo repo, IReadOnlyList<string> backups, Screenshot? picture = null)
        {
            Assert.Empty(await Steps.GitLinesAsync(repo, "remote"));
            Assert.Empty(await Steps.ConfigAsync(repo, @"^remote\.origin\."));
            Assert.Empty(await Steps.ConfigAsync(repo, @"^branch\.(main|published)\.(remote|merge)$"));
            Assert.Empty(await Steps.RefsUnderAsync(repo, "refs/remotes/"));
            Assert.Equal(backups, await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.SyncWithoutRemote, PartU2.SyncLanes, picture: picture);
            Assert.Equal(0, Steps.SectionCount(app, "Remotes"));
            Assert.Equal((0, 0), Steps.Arrows(app, "published"));
            Assert.Equal((0, 0), Steps.Arrows(app, "main"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.RemoveRemoteText));
        }

        /// <summary>U22 after steps 16 and 18: .gitignore exactly "/logs/run.log\n", untracked alone, the stage panel's one row, T+.</summary>
        public static async Task AssertIgnoredAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(System.Text.Encoding.ASCII.GetBytes(PartU2.Gitignore), await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".gitignore")));
            Assert.Equal([PartU2.OnMain, "?? .gitignore"], await Steps.StatusAsync(repo));
            await Steps.WaitForUnstagedAsync(app, "Unstaged files (1)");
            await app.WaitForAsync(() => Steps.FileRows(app, "UnstagedFileList").SequenceEqual(["A .gitignore"]), "the stage panel to list .gitignore alone");
            AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.IgnoreText));
        }
    }
}
