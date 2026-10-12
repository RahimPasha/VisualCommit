// Part C, check 16 (docs/test-reports/phase-3.md): tags: an annotated tag whose message starts
// with "#" (kept by --cleanup=whitespace), a lightweight tag, pushing a tag, deleting it on origin
// (and the lease refusing a second deletion), and deleting a tag here, each with its backup ref.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksC
{
    /// <summary>
    /// Check 16: the commit's menu and the tag dialog with its validation; an annotated tag
    /// "#42 hotfix" and a lightweight tag; the tag's menu, its push, its deletion on origin with
    /// the lease (and the second deletion refused), and its deletion here.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_16_tags()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the commit's menu.
        var pointer = app.GraphRowPoint(4);
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "16a-commit-menu");
        AssertSelection(app, shot, 4);
        AssertMenuAt(app, menu, shot, pointer, "Check out this commit", PartC.Check16CommitMenu);

        // Step 2: the tag dialog.
        app.ClickMenuItem("Create tag here…");
        await AssertTagDialogAsync(app, PartC.Check16Context, string.Empty, string.Empty, false);
        Shot(app, "16b-tag-dialog");

        // Step 3: names, then the message.
        app.TypeInto("NameBox", "v1.0");
        AssertNameError(app, "A tag named v1.0 exists.");
        AssertDialogButtons(app, "Create tag", false);
        app.ReplaceText("NameBox", "v1 2");
        AssertNameError(app, "This is not a valid tag name: it contains a space.");
        AssertDialogButtons(app, "Create tag", false);
        app.ReplaceText("NameBox", "v1.2");
        AssertNameError(app, string.Empty);
        AssertDialogButtons(app, "Create tag", true);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "MessageBox"));
        app.TypeInto("MessageBox", PartC.TagMessage);
        Shot(app, "16c-tag-dialog-filled");
        Assert.Equal("v1.2", ((TextBox)app.FindByAutomationId("NameBox")).Text);
        Assert.Equal(PartC.TagMessage, ((TextBox)app.FindByAutomationId("MessageBox")).Text);

        // Step 4: Create tag.
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Create tag v1.2", mark);
        shot = Shot(app, "16d-annotated-created");
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, $"git tag --annotate --cleanup=whitespace --file=- v1.2 {PartC.AlphaTwoSha}^{{commit}}");
        Assert.Equal(["tag"], await GitLinesAsync(repo, "cat-file", "-t", "v1.2"));
        Assert.Equal(
            [$"object {PartC.AlphaTwoSha}", "type commit", "tag v1.2", "tagger Test Author <author@example.com> 1767344400 +0000", string.Empty, PartC.TagMessage],
            (await repo.GitAsync("cat-file", "-p", "v1.2")).StandardOutput.TrimEnd('\n').Split('\n'));
        AssertGraph(app, shot, PartC.Check16Annotated, lanes: 3);
        AssertStatus(app, "Created tag v1.2");
        Assert.Equal(3, SectionRow(app, "Tags").Count);
        Assert.Equal(["v1.0", "v1.1", "v1.2"], SectionLines(app, "Tags"));

        // Step 5: a lightweight tag on "Fast-forward me".
        app.RightClick(app.GraphRowPoint(2));
        app.Menu();
        app.ClickMenuItem("Create tag here…");
        await AssertTagDialogAsync(app, PartC.Check16FfContext, string.Empty, string.Empty, false);
        app.TypeInto("NameBox", "ff-point");
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Create tag ff-point", mark);
        shot = Shot(app, "16e-lightweight-created");
        var ffSha = await RevParseAsync(repo, "refs/heads/feature/ff");
        AssertCalled(app, calls, $"git tag ff-point {ffSha}^{{commit}}");
        Assert.Equal(["commit"], await GitLinesAsync(repo, "cat-file", "-t", "ff-point"));
        AssertSelection(app, shot, 2);
        AssertGraph(app, shot, PartC.Check16Lightweight, lanes: 3);
        AssertStatus(app, "Created tag ff-point");
        Assert.Equal(4, SectionRow(app, "Tags").Count);
        Assert.Equal(["ff-point", "v1.0", "v1.1", "v1.2"], SectionLines(app, "Tags"));

        // Step 6: the tag's menu from the left panel, then Push.
        pointer = app.BoundsOf(await app.RefRowAsync("Tags", "v1.2")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "16f-tag-menu");
        AssertMenuAt(app, menu, shot, pointer, "Check out v1.2", PartC.Check16TagMenu);
        AssertNoLeftPanelSelection(app);
        AssertSelection(app, shot, 2);
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.ClickMenuItem("Push v1.2 to origin");
        await WaitForOperationAsync(app, repo, "Push tag v1.2", mark);
        Shot(app, "16g-pushed");
        var push = AssertCalled(app, calls, "git push --progress origin refs/tags/v1.2:refs/tags/v1.2");
        Assert.Contains(" * [new tag]         v1.2 -> v1.2", push.StandardError, StringComparison.Ordinal);
        var tagObject = await RevParseAsync(repo, "refs/tags/v1.2");
        Assert.Equal([$"{tagObject}\trefs/tags/v1.2", $"{PartC.AlphaTwoSha}\trefs/tags/v1.2^{{}}"], await GitLinesAsync(repo, "ls-remote", "--tags", "origin"));
        AssertStatus(app, "Pushed tag v1.2 to origin");

        // Step 7: delete v1.2 on origin.
        await OpenTagMenuAsync(app, "v1.2");
        app.ClickMenuItem("Delete v1.2 on origin…");
        await AssertConfirmationAsync(app, "Delete v1.2 on origin?", "Delete the tag v1.2 on origin? Everyone who uses origin loses it.", PartC.BackupDetail, "Delete on origin");
        Shot(app, "16h-delete-on-origin-question");
        var deleteCommand = $"git push --progress --force-with-lease=refs/tags/v1.2:{tagObject} origin --delete refs/tags/v1.2";
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete tag v1.2 on origin", mark);
        shot = Shot(app, "16i-deleted-on-origin");
        await AssertNoDialogAsync(app);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal("remote-tag", backup.Ref.Kind);
        Assert.Equal("origin/v1.2", backup.Ref.Name);
        Assert.Equal(tagObject, backup.Sha);
        Assert.Equal(["tag"], await GitLinesAsync(repo, "cat-file", "-t", backup.Name));
        Assert.Contains(PartC.TagMessage, (await repo.GitAsync("cat-file", "-p", backup.Name)).StandardOutput.Split('\n'));
        var deleted = AssertCalled(app, calls, deleteCommand);
        Assert.True(deleted.Succeeded);
        Assert.Contains(" - [deleted]         v1.2", deleted.StandardError, StringComparison.Ordinal);
        Assert.Empty(await GitLinesAsync(repo, "ls-remote", "--tags", "origin"));
        Assert.Contains("v1.2", await GitLinesAsync(repo, "tag", "--list"));
        AssertGraph(app, shot, PartC.Check16Lightweight, lanes: 3);
        AssertStatus(app, "Deleted tag v1.2 on origin");

        // Step 8: again: the lease refuses it.
        await OpenTagMenuAsync(app, "v1.2");
        app.ClickMenuItem("Delete v1.2 on origin…");
        await AssertConfirmationAsync(app, "Delete v1.2 on origin?", "Delete the tag v1.2 on origin? Everyone who uses origin loses it.", PartC.BackupDetail, "Delete on origin");
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete tag v1.2 on origin", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "16j-deletion-stopped");
        var backups = await BackupsAsync(repo);
        Assert.Equal(2, backups.Count);
        Assert.All(backups, entry => Assert.Equal(("remote-tag", "origin/v1.2", tagObject), (entry.Ref.Kind, entry.Ref.Name, entry.Sha)));
        Assert.True(backups[1].Ref.Time > backups[0].Ref.Time || string.CompareOrdinal(backups[1].Name, backups[0].Name) > 0);
        var origin = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Deletion stopped",
            "origin has no tag v1.2, or its v1.2 is not the same as yours.",
            OpsRepo,
            deleteCommand,
            ["Copy output", "Close"],
            PartC.T6(origin),
            PartC.T6Key));
        AssertErrorDialogParts(app, extra: null);
        Assert.Equal(1, app.LastCall(deleteCommand).ExitCode);
        AssertStatus(app, "Delete tag v1.2 on origin failed", OperationStatusTone.Danger);
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);

        // Step 9: delete v1.0 here.
        await OpenTagMenuAsync(app, "v1.0");
        app.ClickMenuItem("Delete v1.0…");
        await AssertConfirmationAsync(app, "Delete tag?", "Delete the tag v1.0 here? It stays on remotes it was pushed to.", PartC.BackupDetail, "Delete");
        Shot(app, "16k-delete-question");
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete tag v1.0", mark);
        shot = Shot(app, "16l-deleted");
        await AssertNoDialogAsync(app);
        backups = await BackupsAsync(repo);
        Assert.Equal(3, backups.Count);
        Assert.Equal(2, backups.Count(entry => entry.Ref.Kind == "remote-tag"));
        var tagBackup = Assert.Single(backups, entry => entry.Ref.Kind == "tag");
        Assert.Equal("v1.0", tagBackup.Ref.Name);
        Assert.StartsWith(PartC.V10TagObjectShort, tagBackup.Sha, StringComparison.Ordinal);
        var backupText = (await repo.GitAsync("cat-file", "-p", tagBackup.Name)).StandardOutput.Split('\n');
        Assert.Contains("tag v1.0", backupText);
        Assert.Contains("Version 1.0", backupText);
        var deleteTag = AssertCalled(app, calls, "git tag --delete v1.0");
        Assert.Contains("Deleted tag 'v1.0' (was c8e9976)", deleteTag.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["ff-point", "v1.1", "v1.2"], await GitLinesAsync(repo, "tag", "--list"));
        AssertGraph(app, shot, PartC.Check16Deleted, lanes: 3);
        AssertStatus(app, "Deleted tag v1.0");
        Assert.Equal(3, SectionRow(app, "Tags").Count);
        AssertNoClippedText(app);
    }

    /// <summary>Right-clicks a tag under Tags in the left panel and waits for its menu.</summary>
    private static async Task OpenTagMenuAsync(ShellDriver app, string tag)
    {
        app.RightClick(await app.RefRowAsync("Tags", tag));
        app.Menu();
    }

    /// <summary>
    /// The tag dialog (4f): "Create tag", its context, the name box with "Tag name" and the focus,
    /// <c>NameError</c>, the three-line message box with its placeholder, no option, "Cancel" and
    /// "Create tag".
    /// </summary>
    private static async Task AssertTagDialogAsync(ShellDriver app, string context, string name, string message, bool enabled)
    {
        await AssertNameDialogAsync(app, "Create tag", context, name, "Tag name", string.Empty, null, false, "Create tag", enabled);
        var box = (TextBox)app.FindByAutomationId(app.TopDialogView!, "MessageBox");
        Assert.True(box.IsEffectivelyVisible);
        Assert.Equal(message, box.Text ?? string.Empty);
        Assert.Equal(PartC.TagMessagePlaceholder, box.PlaceholderText);
        Assert.Contains("multiline", box.Classes);
        Assert.Equal(66, box.Bounds.Height, 1.0);
    }
}
