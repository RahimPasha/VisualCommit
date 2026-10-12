// Part C, checks 15 and 15b (docs/test-reports/phase-3.md): stash save with the dialog, apply,
// pop and drop with its confirmation and backup ref; a pop refused by local changes and a pop that
// conflicts.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksC
{
    private const string OpsRepo = "ops";

    /// <summary>
    /// Check 15: stash save (the dialog, untracked files included), apply from the stash's menu,
    /// pop from the toolbar, stash without untracked files (the choice remembered), drop with its
    /// confirmation (Cancel, then Drop with a backup ref), and the dialog opening with the last
    /// confirmed choice.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_15_stash_save_apply_pop_drop()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n").WriteFile("todo.txt", "Later\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the changes.
        var shot = Shot(app, "15a-changes");
        AssertGraph(app, shot, PartC.G0, lanes: 3, PartC.Working);
        AssertSelection(app, shot, null);
        AssertToolbar(app, "StashButton", enabled: true);
        AssertToolbar(app, "PopButton", enabled: true);

        // Step 2: the stash dialog.
        app.Click(ToolbarButton(app, "StashButton"));
        await AssertStashDialogAsync(app, includeUntracked: true);
        Shot(app, "15b-stash-dialog");

        // Step 3: a message, then "Stash".
        app.TypeInto("MessageBox", "Save my work");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Stash", mark);
        shot = Shot(app, "15c-stashed");
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, "git stash push --include-untracked --message \"Save my work\"");
        Assert.Equal(["stash@{0}: On main: Save my work", "stash@{1}: On main: Experiment"], await StashListAsync(repo));
        Assert.Empty(await StatusAsync(repo));
        Assert.Equal(["todo.txt"], await GitLinesAsync(repo, "ls-tree", "-r", "--name-only", "stash@{0}^3"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "todo.txt")));
        AssertGraph(app, shot, PartC.Check15Stashed, lanes: 4);
        AssertStatus(app, "Stashed the changes");
        Assert.Equal(2, SectionRow(app, "Stashes").Count);
        Assert.Equal(["On main: Save my work", "On main: Experiment"], SectionLines(app, "Stashes"));
        AssertToolbar(app, "StashButton", enabled: false, PartC.NoChangesToStash);
        AssertToolbar(app, "PopButton", enabled: true);

        // Step 4: the older stash's menu from the left panel.
        var row = await app.RefRowAsync("Stashes", "On main: Experiment");
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        shot = Shot(app, "15d-stash-menu");
        AssertMenuAt(app, menu, shot, pointer, "Apply stash", PartC.Check15StashMenu);
        AssertNoLeftPanelSelection(app);
        AssertSelection(app, shot, null);

        // Step 5: Apply.
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.ClickMenuItem("Apply stash");
        await WaitForOperationAsync(app, repo, "Apply stash@{1}", mark);
        shot = Shot(app, "15e-applied");
        Assert.True(AssertCalled(app, calls, "git stash apply stash@{1}").Succeeded);
        Assert.Equal(["stash@{0}: On main: Save my work", "stash@{1}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal([" M README.md"], await StatusAsync(repo));
        AssertGraph(app, shot, PartC.Check15Stashed, lanes: 4, PartC.Working);
        AssertToolbar(app, "StashButton", enabled: true);
        AssertStatus(app, "Applied stash@{1}");

        // Step 6: Pop from the toolbar.
        var saveMyWork = await RevParseAsync(repo, "stash@{0}");
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForOperationAsync(app, repo, "Pop stash@{0}", mark);
        shot = Shot(app, "15f-popped");
        var pop = AssertCalled(app, calls, "git stash pop stash@{0}");
        Assert.Equal(0, pop.ExitCode);
        Assert.EndsWith($"Dropped stash@{{0}} ({saveMyWork})", pop.StandardOutput.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(["stash@{0}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal([" M README.md", " M src/core.txt", "?? todo.txt"], await StatusAsync(repo));
        Assert.Equal(["core", "main work", "more work"], FileLines(repo, "src/core.txt"));
        Assert.Equal(["Later"], FileLines(repo, "todo.txt"));
        AssertStatus(app, "Popped stash@{0}");
        AssertGraph(app, shot, PartC.G0, lanes: 3, PartC.Working);
        Assert.Equal(1, SectionRow(app, "Stashes").Count);

        // Step 7: Stash without untracked files.
        app.Click(ToolbarButton(app, "StashButton"));
        await AssertStashDialogAsync(app, includeUntracked: true);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "NameDialogOption"));
        Assert.False(((CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption")).IsChecked);
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Stash", mark);
        shot = Shot(app, "15g-stashed-without-untracked");
        AssertCalled(app, calls, "git stash push");
        Assert.Equal(["stash@{0}: " + PartC.WipMessage, "stash@{1}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal(["?? todo.txt"], await StatusAsync(repo));
        Assert.Contains("\"stashIncludeUntracked\": false", File.ReadAllText(Path.Combine(data.Path, "settings.json")), StringComparison.Ordinal);
        AssertGraph(app, shot, PartC.Check15WithoutUntracked, lanes: 4, PartC.Working);
        AssertStatus(app, "Stashed the changes");

        // Step 8: Drop "On main: Experiment": Cancel, then Drop.
        await OpenStashMenuAsync(app, "On main: Experiment");
        app.ClickMenuItem("Drop stash…");
        await AssertConfirmationAsync(app, "Drop stash?", "Drop stash@{1} \"On main: Experiment\"?", PartC.BackupDetail, "Drop");
        Shot(app, "15h-drop-question");
        ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.Equal(2, (await StashListAsync(repo)).Count);
        Assert.Empty(await repo.BackupRefsAsync());

        await OpenStashMenuAsync(app, "On main: Experiment");
        app.ClickMenuItem("Drop stash…");
        await AssertConfirmationAsync(app, "Drop stash?", "Drop stash@{1} \"On main: Experiment\"?", PartC.BackupDetail, "Drop");
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Drop stash@{1}", mark);
        shot = Shot(app, "15i-dropped");
        await AssertNoDialogAsync(app);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal("stash", backup.Ref.Kind);
        Assert.Null(backup.Ref.Name);
        Assert.False(backup.Ref.IsChanges);
        Assert.Equal(PartC.ExperimentStashSha, backup.Sha);
        Assert.Equal(PartC.MainWorkSha, await RevParseAsync(repo, backup.Name + "^1"));
        Assert.Equal(["index on main: 88d6b91 Main work"], await GitLinesAsync(repo, "log", "-1", "--format=%s", backup.Name + "^2"));
        var drop = AssertCalled(app, calls, "git stash drop stash@{1}");
        Assert.Contains($"Dropped stash@{{1}} ({PartC.ExperimentStashSha})", drop.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["stash@{0}: " + PartC.WipMessage], await StashListAsync(repo));
        AssertGraph(app, shot, PartC.Check15Dropped, lanes: 3, PartC.Working);
        AssertStatus(app, "Dropped stash@{1}");
        Assert.Equal(1, SectionRow(app, "Stashes").Count);

        // Step 9: the dialog opens with the last confirmed choice; Esc closes it.
        app.Click(ToolbarButton(app, "StashButton"));
        await AssertStashDialogAsync(app, includeUntracked: false);
        Shot(app, "15j-dialog-remembers");
        calls = app.GitCalls().Count;
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git stash push", StringComparison.Ordinal));
        Assert.Equal(["stash@{0}: " + PartC.WipMessage], await StashListAsync(repo));
        Assert.Equal(["?? todo.txt"], await StatusAsync(repo));
    }

    /// <summary>
    /// Check 15b: a pop refused by a local edit of the file it changes ("Applying the stash
    /// failed"), then, with the edit undone outside the app, a pop that conflicts: the warning text,
    /// the conflicts banner, the stash kept.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_15b_pop_refused_and_conflicting()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("README.md", "# Operations\n\nDifferent.\n", "Change README");
        repo.WriteFile("README.md", "# Operations\n\nLocal edit.\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: before.
        var shot = Shot(app, "15b1-before");
        AssertGraph(app, shot, PartC.Check15bBefore, lanes: 4, PartC.Working);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: true, ahead: 1);

        // Step 2: Pop, refused.
        var mark = app.LogMark();
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForOperationAsync(app, repo, "Pop stash@{0}", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "15b2-pop-refused");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Applying the stash failed",
            "Your changes to these files would be overwritten by applying stash@{0}.",
            OpsRepo,
            "git stash pop stash@{0}",
            ["Copy output", "Close"],
            PartC.T5,
            PartC.T5Key));
        AssertErrorDialogParts(app, extra: null);
        Assert.Equal(1, app.LastCall("git stash pop stash@{0}").ExitCode);
        AssertStatus(app, "Pop stash@{0} failed", OperationStatusTone.Danger);
        Assert.Equal(["stash@{0}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal(["# Operations", string.Empty, "Local edit."], FileLines(repo, "README.md"));

        // Step 3: Close.
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);

        // Step 4: the edit undone outside the app.
        await repo.GitAsync("checkout", "--", "README.md");
        await app.WaitForAsync(() => !Graph(app).ShowsWorkingRow, "the working-changes row to go", OutsideChangeTimeout);
        await app.WaitForIdleAsync();

        // Step 5: Pop again: it conflicts.
        var calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForOperationAsync(app, repo, "Pop stash@{0}", mark);
        await app.WaitForAsync(() => app.BannerBounds is not null, "the conflicts banner");
        shot = Shot(app, "15b3-pop-conflicts");
        await AssertNoDialogAsync(app);
        var pop = AssertCalled(app, calls, "git stash pop stash@{0}");
        Assert.Equal(1, pop.ExitCode);
        Assert.Contains("CONFLICT (content): Merge conflict in README.md", pop.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("The stash entry is kept in case you need it again.", pop.StandardOutput, StringComparison.Ordinal);
        AssertStatus(app, PartC.PopConflictStatus, OperationStatusTone.Warning);
        AssertConflictsBanner(app, shot, PartC.ConflictsBanner, PartC.ConflictsBannerTip);
        AssertToolbar(app, "StashButton", enabled: false, PartC.ResolveFirst);
        AssertToolbar(app, "PopButton", enabled: true);
        AssertGraph(app, shot, PartC.Check15bBefore, lanes: 4, PartC.WorkingConflict);
        Assert.Equal(["stash@{0}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal(["UU README.md"], await StatusAsync(repo));
        Assert.Equal(
            ["# Operations", string.Empty, "<<<<<<< Updated upstream", "Different.", "=======", "Experiment.", ">>>>>>> Stashed changes"],
            FileLines(repo, "README.md"));
        AssertNoOperationFiles(repo);

        // Step 6: the stage panel.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        Shot(app, "15b4-stage-panel");
        Assert.Equal(["U README.md"], FileRows(app, "UnstagedFileList"));
        await AssertConflictRowAsync(app, "README.md");
        Assert.Empty(FileRows(app, "StagedFileList"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitDescription"));
        AssertNoClippedText(app);
    }

    /// <summary>Right-clicks a stash under Stashes in the left panel and waits for its menu.</summary>
    private static async Task OpenStashMenuAsync(ShellDriver app, string message)
    {
        app.RightClick(await app.RefRowAsync("Stashes", message));
        app.Menu();
    }

    /// <summary>
    /// The stash dialog (4f, 6.2): "Stash changes", no name box and no context, the one-line message
    /// box empty with its placeholder and the focus, "Include untracked files" as the last confirmed
    /// choice left it, "Cancel" and "Stash" (enabled).
    /// </summary>
    private static async Task AssertStashDialogAsync(ShellDriver app, bool includeUntracked)
    {
        var model = await app.WaitForDialogAsync<NameDialogViewModel>();
        var view = app.TopDialogView!;
        Assert.Equal("Stash changes", ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal("Stash changes", model.Title);
        Assert.False(app.FindByAutomationId(view, "NameBox").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId(view, "NameDialogContext").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId(view, "NameError").IsEffectivelyVisible);
        var box = (TextBox)app.FindByAutomationId(view, "MessageBox");
        Assert.True(box.IsEffectivelyVisible);
        Assert.Equal(string.Empty, box.Text ?? string.Empty);
        Assert.Equal("Message (optional)", box.PlaceholderText);
        Assert.False(box.AcceptsReturn);
        Assert.DoesNotContain("multiline", box.Classes);
        await app.WaitForAsync(() => box.IsFocused, "the message box to have the keyboard focus");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.True(option.IsEffectivelyVisible);
        Assert.Equal("Include untracked files", option.Content);
        Assert.Equal(includeUntracked, option.IsChecked);
        AssertDialogButtons(app, "Stash", true);
    }
}
