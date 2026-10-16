// Check U20 of part U (docs/test-reports/phase-5.md): undo and redo of a stash, an apply, a pop and
// a drop (each stash back as stash@{0} with its id and message), of an annotated tag's creation
// (kept in a tag-undone- backup and brought back as the same tag object) and of a tag's deletion;
// "ops", dark, 1100×700. One method per action with its undo and redo, each on a fresh copy.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
    /// <summary>U20 part A: the toolbar's Stash with untracked files, undone (popped with its index) and redone (the same stash again).</summary>
    [AvaloniaFact]
    public async Task Check_U20_stash()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", PartU2.CoreMoreWork).WriteFile("todo.txt", PartU2.Todo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: Stash, "Save my work", untracked files included (the default).
        app.Click(ToolbarButton(app, "StashButton"));
        await app.WaitForDialogAsync<NameDialogViewModel>();
        Assert.True(((CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption")).IsChecked == true, "\"Include untracked files\" is not ticked.");
        app.TypeInto("MessageBox", "Save my work");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Stash", mark);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.StashPush);
        AssertStatus(app, PartU2.Stashed);
        await U20.AssertStashedAsync(app, repo);

        // Step 2: Undo: popped with its index, found by its commit.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.StashText);
        var shot = Shot(app, "u20a-stash-undone");
        var pop = Steps.AssertSucceeded(app, calls, PartU2.StashPopIndex);
        Assert.Contains($"Dropped stash@{{0}} ({PartU2.SaveMyWork})", pop.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["## main...origin/main", " M src/core.txt", "?? todo.txt"], await Steps.StatusAsync(repo));
        Assert.Equal(PartU2.CoreMoreWork, Steps.FileText(repo, "src/core.txt"));
        Assert.Equal(PartU2.Todo, Steps.FileText(repo, "todo.txt"));
        Assert.Equal(PartU2.ExperimentOnly, await Steps.StashListAsync(repo));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working, picture: shot);
        Assert.Equal(1, Steps.SectionCount(app, "Stashes"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.StashText));

        // Step 3: Redo: the same stash again (the same trees, parents and date, D72).
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.StashText);
        shot = Shot(app, "u20b-stash-redone");
        Steps.AssertSucceeded(app, calls, PartU2.StashPush);
        await U20.AssertStashedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U20 part B: "Apply stash" from the stash's menu, undone by the index entries and the files' bytes (no stash call), and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U20_apply()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        var index = await Steps.GitLinesAsync(repo, "ls-files", "--stage");

        // Step 4: Apply.
        await Steps.OpenRowMenuAsync(app, "Stashes", "On main: Experiment");
        var calls = await Steps.RunMenuItemAsync(app, repo, "Apply stash", "Apply stash@{0}");
        Steps.AssertSucceeded(app, calls, PartU2.StashApply);
        AssertStatus(app, PartU2.Applied);
        await U20.AssertAppliedAsync(app, repo);

        // Step 5: Undo: the index entries, then README.md's recorded bytes; no git stash call. The
        // call log keeps no input of update-index, so the entries are read back from the index.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.ApplyText);
        var shot = Shot(app, "u20c-apply-undone");
        Assert.Equal(
            [PartU2.IndexInfo],
            Steps.OperationCallsSince(app, calls).Where(call => call.StartsWith("git update-index", StringComparison.Ordinal)));
        Steps.AssertSucceeded(app, calls, PartU2.IndexInfo);
        U20.AssertNoStashWrite(app, calls);
        var undone = await Steps.GitLinesAsync(repo, "ls-files", "--stage");
        Assert.Equal(index, undone);
        Assert.Contains(undone, line => line.StartsWith($"100644 {PartU2.ReadmeBlob}", StringComparison.Ordinal) && line.EndsWith(" 0\tREADME.md", StringComparison.Ordinal));
        Assert.Equal(Scenarios.OperationsFiles.Readme, Steps.FileText(repo, "README.md"));
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        Assert.Equal(PartU2.ExperimentOnly, await Steps.StashListAsync(repo));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.ApplyText));

        // Step 6: Redo: the entries and README.md's bytes after the apply again.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.ApplyText);
        shot = Shot(app, "u20d-apply-redone");
        Steps.AssertSucceeded(app, calls, PartU2.IndexInfo);
        U20.AssertNoStashWrite(app, calls);
        await U20.AssertAppliedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U20 part C: "Pop stash" (a backup saved first, D171), undone by storing the stash again and putting the files back, and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U20_pop()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 7: Pop.
        await Steps.OpenRowMenuAsync(app, "Stashes", "On main: Experiment");
        var calls = await Steps.RunMenuItemAsync(app, repo, "Pop stash", "Pop stash@{0}");
        Steps.AssertSucceeded(app, calls, PartU2.StashPop);
        U20.AssertBackupBefore(app, calls, PartU2.FirstBackup("stash"), PartU2.StashPop);
        AssertStatus(app, PartU2.Popped);
        await U20.AssertPoppedAsync(app, repo, PartU2.PopText);

        // Step 8: Undo: the stash stored again first, then the index entries and README.md's bytes.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.PopText);
        var shot = Shot(app, "u20e-pop-undone");
        AssertCalledInOrder(app, calls, PartU2.StashStore, PartU2.IndexInfo);
        Assert.Equal(PartU2.ExperimentOnly, await Steps.StashListAsync(repo));
        Assert.Equal(["On main: Experiment"], await Steps.GitLinesAsync(repo, "log", "-g", "-n", "1", "--format=%gs", "refs/stash"));
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.Readme, Steps.FileText(repo, "README.md"));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Assert.Equal(1, Steps.SectionCount(app, "Stashes"));
        Assert.Equal([$"{PartU2.FirstBackup("stash")} {PartU2.Experiment}"], await Steps.BackupLinesAsync(repo));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.PopText));

        // Step 9: Redo: the entries and bytes forward, then the stash dropped, found by its commit.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.PopText);
        shot = Shot(app, "u20f-pop-redone");
        AssertCalledInOrder(app, calls, PartU2.IndexInfo, PartU2.StashDrop);
        await U20.AssertPoppedAsync(app, repo, PartU2.PopText, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U20 part D: "Drop stash…" with phase 3's confirmation, undone by storing the stash again, and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U20_drop()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 10: Drop, confirmed.
        await Steps.OpenRowMenuAsync(app, "Stashes", "On main: Experiment");
        app.ClickMenuItem("Drop stash…");
        await AssertConfirmationAsync(app, PartU2.DropTitle, PartU2.DropQuestion, PartU2.BackupDetail, "Drop");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Drop stash@{0}", mark);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.StashDrop);
        AssertStatus(app, PartU2.Dropped);
        await U20.AssertDroppedAsync(app, repo);

        // Step 11: Undo: the stash stored again.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.DropText);
        var shot = Shot(app, "u20g-drop-undone");
        Steps.AssertSucceeded(app, calls, PartU2.StashStore);
        Assert.Equal(PartU2.ExperimentOnly, await Steps.StashListAsync(repo));
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Assert.Equal(1, Steps.SectionCount(app, "Stashes"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.DropText));

        // Step 12: Redo: dropped again.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.DropText);
        shot = Shot(app, "u20h-drop-redone");
        Steps.AssertSucceeded(app, calls, PartU2.StashDrop);
        await U20.AssertDroppedAsync(app, repo, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>U20 part E: an annotated tag created, undone (its object kept in a tag-undone- backup first) and redone as the same tag object.</summary>
    [AvaloniaFact]
    public async Task Check_U20_create_annotated_tag()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 13: "Create tag here…" on Alpha two: v1.2 with the message "#42 hotfix".
        Steps.OpenCommitMenu(app, PartU2.AlphaTwoRow);
        app.ClickMenuItem("Create tag here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "v1.2");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "MessageBox"));
        app.TypeInto("MessageBox", "#42 hotfix");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Create tag v1.2", mark);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.CreateTag);
        AssertStatus(app, PartU2.CreatedTag);
        await U20.AssertTagCreatedAsync(app, repo);
        Assert.Empty(await Steps.BackupLinesAsync(repo));

        // Step 14: Undo: the tag object kept in a tag-undone- backup first, then the tag deleted in the step's transaction.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.CreateTagText);
        var shot = Shot(app, "u20i-tag-undone");
        var backupName = PartU2.FirstBackup("tag-undone", "v1.2");
        Assert.Equal([$"{backupName} {PartU2.V12TagObject}"], await Steps.BackupLinesAsync(repo));
        var transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.UndoMessage(PartU2.CreateTagText)));
        Assert.Equal($"delete refs/tags/v1.2 {PartU2.V12TagObject}\n", transaction.StandardInput);
        U20.AssertBackupBefore(app, calls, backupName, transaction.CommandText);
        Assert.Equal(["v1.0", "v1.1"], await Steps.TagsAsync(repo));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Tags"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.CreateTagText));

        // Step 15: Redo: the same tag object again; the backup stays.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.CreateTagText);
        shot = Shot(app, "u20j-tag-redone");
        transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.RedoMessage(PartU2.CreateTagText)));
        Assert.Equal($"create refs/tags/v1.2 {PartU2.V12TagObject}\n", transaction.StandardInput);
        Assert.Equal(["tag"], await Steps.GitLinesAsync(repo, "cat-file", "-t", "v1.2"));
        await U20.AssertTagCreatedAsync(app, repo, picture: shot);
        Assert.Equal([$"{backupName} {PartU2.V12TagObject}"], await Steps.BackupLinesAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>U20 part F: a tag deleted with phase 3's confirmation, undone (the tag object back) and redone.</summary>
    [AvaloniaFact]
    public async Task Check_U20_delete_tag()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);
        var backupName = PartU2.FirstBackup("tag", "v1.0");

        // Step 16: "Delete v1.0…", confirmed.
        await Steps.OpenRowMenuAsync(app, "Tags", "v1.0");
        app.ClickMenuItem("Delete v1.0…");
        await AssertConfirmationAsync(app, PartU2.DeleteTagTitle, PartU2.DeleteTagQuestion, PartU2.BackupDetail, "Delete");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Delete tag v1.0", mark);
        await AssertNoDialogAsync(app);
        Steps.AssertSucceeded(app, calls, PartU2.DeleteTag);
        AssertStatus(app, PartU2.DeletedTag);
        await U20.AssertTagDeletedAsync(app, repo, backupName);

        // Step 17: Undo: the tag object back, in the step's transaction.
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.DeleteTagText);
        var shot = Shot(app, "u20k-tag-delete-undone");
        var transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.UndoMessage(PartU2.DeleteTagText)));
        Assert.Equal($"create refs/tags/v1.0 {PartU2.V10TagObject}\n", transaction.StandardInput);
        Assert.Equal(PartU2.V10TagObject, await Steps.RefAsync(repo, "refs/tags/v1.0"));
        var tag = (await repo.GitAsync("cat-file", "-p", "v1.0")).StandardOutput.Split('\n');
        Assert.Contains("tag v1.0", tag);
        Assert.Contains("Version 1.0", tag);
        Assert.Contains(tag, line => line.StartsWith("tagger ", StringComparison.Ordinal) && line.EndsWith($" {ScenarioTime(2).ToUnixTimeSeconds()} +0000", StringComparison.Ordinal));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Assert.Equal(2, Steps.SectionCount(app, "Tags"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.DeleteTagText));

        // Step 18: Redo: deleted again; the backup stays.
        calls = await Steps.StepAsync(app, repo, Redo, PartU2.DeleteTagText);
        shot = Shot(app, "u20l-tag-delete-redone");
        transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.RedoMessage(PartU2.DeleteTagText)));
        Assert.Equal($"delete refs/tags/v1.0 {PartU2.V10TagObject}\n", transaction.StandardInput);
        await U20.AssertTagDeletedAsync(app, repo, backupName, picture: shot);
        AssertNoClippedText(app);
    }

    /// <summary>The helpers of check U20's methods.</summary>
    private static class U20
    {
        /// <summary>U20 after steps 1 and 3: the stash list, a clean tree, G0-S, Stashes 2 and T+.</summary>
        public static async Task AssertStashedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(PartU2.TwoStashes, await Steps.StashListAsync(repo));
            Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
            AssertGraph(app, PartU2.G0TwoStashes, lanes: 4, picture: picture);
            Assert.Equal(2, Steps.SectionCount(app, "Stashes"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.StashText));
        }

        /// <summary>
        /// "No git stash call" in an apply's undo and redo (7.3: index entries and files): no stash
        /// command but the stash list the history reads before and after every step (7.7).
        /// </summary>
        public static void AssertNoStashWrite(ShellDriver app, int mark) =>
            Assert.DoesNotContain(
                CallsSince(app, mark),
                call => call.StartsWith("git stash", StringComparison.Ordinal) && !call.StartsWith("git stash list", StringComparison.Ordinal));

        /// <summary>U20 after steps 4 and 6: README.md changed, the stash kept, G0+w, T+.</summary>
        public static async Task AssertAppliedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal([PartU2.OnMain, " M README.md"], await Steps.StatusAsync(repo));
            Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, Steps.FileText(repo, "README.md"));
            Assert.Equal(PartU2.ExperimentOnly, await Steps.StashListAsync(repo));
            AssertGraph(app, PartU2.G0, PartU2.G0Lanes, PartU2.Working, picture: picture);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.ApplyText));
        }

        /// <summary>U20 after steps 7 and 9: README.md changed, no stash, one backup, G0-s with the working row in Main work's lane, Stashes 0, T+.</summary>
        public static async Task AssertPoppedAsync(ShellDriver app, TempRepo repo, string text, Screenshot? picture = null)
        {
            Assert.Equal([PartU2.OnMain, " M README.md"], await Steps.StatusAsync(repo));
            Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, Steps.FileText(repo, "README.md"));
            Assert.Empty(await Steps.StashListAsync(repo));
            Assert.Equal([$"{PartU2.FirstBackup("stash")} {PartU2.Experiment}"], await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.G0NoStash, PartU2.G0Lanes, PartU2.WorkingAfterPop, picture: picture);
            Assert.Equal(0, Steps.SectionCount(app, "Stashes"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(text));
        }

        /// <summary>U20 after steps 10 and 12: no stash, its backup, G0-s, Stashes 0, T+.</summary>
        public static async Task AssertDroppedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Empty(await Steps.StashListAsync(repo));
            Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
            Assert.Equal([$"{PartU2.FirstBackup("stash")} {PartU2.Experiment}"], await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.G0NoStash, PartU2.G0Lanes, picture: picture);
            Assert.Equal(0, Steps.SectionCount(app, "Stashes"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.DropText));
        }

        /// <summary>
        /// A backup ref was written before <paramref name="command"/> (the pop of step 7, the tag's
        /// deletion of step 14): the call that wrote it (the backup's own <c>update-ref</c>, whatever
        /// its form) comes before that command among the calls since <paramref name="mark"/>.
        /// </summary>
        public static void AssertBackupBefore(ShellDriver app, int mark, string backupName, string command)
        {
            var calls = app.GitCalls().Skip(mark).ToList();
            var backup = calls.FindIndex(call => call.CommandText.Contains(backupName, StringComparison.Ordinal) || (call.StandardInput?.Contains(backupName, StringComparison.Ordinal) ?? false));
            var after = calls.FindIndex(call => call.CommandText == command);
            Assert.True(backup >= 0 && backup < after, $"{backupName} was not written before \"{command}\":\n" + string.Join('\n', calls.Select(call => call.CommandText)));
        }

        /// <summary>U20 after steps 13 and 15: v1.2 the annotated tag object, G0 v1.2, Tags 3, T+.</summary>
        public static async Task AssertTagCreatedAsync(ShellDriver app, TempRepo repo, Screenshot? picture = null)
        {
            Assert.Equal(PartU2.V12TagObject, await Steps.RefAsync(repo, "refs/tags/v1.2"));
            Assert.Equal(PartU2.V12Object, (await repo.GitAsync("cat-file", "-p", "v1.2")).StandardOutput.TrimEnd('\n').Split('\n'));
            AssertGraph(app, PartU2.G0WithV12, PartU2.G0Lanes, picture: picture);
            Assert.Equal(3, Steps.SectionCount(app, "Tags"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.CreateTagText));
        }

        /// <summary>U20 after steps 16 and 18: no v1.0, its backup, G0 −v1.0, Tags 1, T+.</summary>
        public static async Task AssertTagDeletedAsync(ShellDriver app, TempRepo repo, string backupName, Screenshot? picture = null)
        {
            Assert.Null(await Steps.RefAsync(repo, "refs/tags/v1.0"));
            Assert.Equal(["v1.1"], await Steps.TagsAsync(repo));
            Assert.Equal([$"{backupName} {PartU2.V10TagObject}"], await Steps.BackupLinesAsync(repo));
            AssertGraph(app, PartU2.G0WithoutV10, PartU2.G0Lanes, picture: picture);
            Assert.Equal(1, Steps.SectionCount(app, "Tags"));
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.DeleteTagText));
        }
    }
}
