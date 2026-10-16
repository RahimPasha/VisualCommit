// Part U, check U17 (docs/test-reports/phase-5.md): undo and redo of resets on "ops": soft, mixed
// with nothing staged, mixed with staged, partly staged and untracked work, and hard with the files
// and the index back from its snapshot (its redo saving a new one). One method per reset, each on
// a fresh copy (design 15.4, UD17); the resets are phase 3's check 10 (UD5).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>Check U17, part A: a soft reset of main to Add core, undone and redone: soft moves in the step's transaction ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U17_soft_reset()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var text = PartU.ResetText("soft");

        // Step 1: the reset, Soft chosen.
        await ResetToAddCoreAsync(app, 6, new Point(484, 285), "ResetSoft");
        AssertStatus(app, PartU.StatusReset("soft"));
        Assert.Equal(PartU.AddCoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", "M  src/core.txt"], await StatusAsync(repo));
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        AssertArrows(app, 0, 1, "Local branches", "main");
        AssertRowSelected(app, 6, PartU.AddCoreSha);
        Assert.Equal([$"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}"], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));

        // Step 2: Undo: the soft move back in the transaction, no read-tree or restore.
        var calls = await UndoAsync(app, repo, text);
        var shot = Shot(app, "u17a-soft-undone");
        AssertTransaction(app, calls, PartU.Undo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.MainWorkSha, PartU.AddCoreSha)]);
        AssertNoCall(app, calls, "git read-tree");
        AssertNoCall(app, calls, "git restore");
        Assert.Equal(PartU.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Equal([PartU.Undo.Message(text)], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal([$"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}"], await BackupLinesAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertRowSelected(app, 6, PartU.AddCoreSha);
        AssertButtons(app, UndoRedoButtons.CanRedo(text));

        // Step 3: Redo: as after step 1, still one backup.
        calls = await RedoAsync(app, repo, text);
        shot = Shot(app, "u17b-soft-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.AddCoreSha, PartU.MainWorkSha)]);
        Assert.Equal(PartU.AddCoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", "M  src/core.txt"], await StatusAsync(repo));
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertArrows(app, 0, 1, "Local branches", "main");
        AssertRowSelected(app, 6, PartU.AddCoreSha);
        Assert.Equal([$"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}"], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));
        AssertNoClippedText(app);
    }

    /// <summary>Check U17, part B: a mixed reset with nothing staged, undone and redone: the index from the commit moved back to, then the transaction ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U17_mixed_reset()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var text = PartU.ResetText("mixed");

        // Step 4: the reset, Mixed left chosen.
        await ResetToAddCoreAsync(app, 6, new Point(484, 285), null);
        AssertStatus(app, PartU.StatusReset("mixed"));
        Assert.Equal(["## main...origin/main [behind 1]", " M src/core.txt"], await StatusAsync(repo));
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        AssertArrows(app, 0, 1, "Local branches", "main");
        Assert.Equal([$"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}"], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));

        // Step 5: Undo: restore --staged from Main work, then the transaction.
        var calls = await UndoAsync(app, repo, text);
        var shot = Shot(app, "u17c-mixed-undone");
        AssertWrites(
            app,
            calls,
            ["git restore", "git update-ref", "git read-tree"],
            PartU.Git("restore", "--staged", "--source=" + PartU.MainWorkSha, "--", "."),
            PartU.Transaction(PartU.Undo.Message(text)));
        AssertTransaction(app, calls, PartU.Undo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.MainWorkSha, PartU.AddCoreSha)]);
        Assert.Equal(PartU.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(text));

        // Step 6: Redo: restore --staged from Add core, then the transaction.
        calls = await RedoAsync(app, repo, text);
        shot = Shot(app, "u17d-mixed-redone");
        AssertWrites(
            app,
            calls,
            ["git restore", "git update-ref", "git read-tree"],
            PartU.Git("restore", "--staged", "--source=" + PartU.AddCoreSha, "--", "."),
            PartU.Transaction(PartU.Redo.Message(text)));
        AssertTransaction(app, calls, PartU.Redo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.AddCoreSha, PartU.MainWorkSha)]);
        Assert.Equal(PartU.AddCoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", " M src/core.txt"], await StatusAsync(repo));
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertArrows(app, 0, 1, "Local branches", "main");
        Assert.Equal([$"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}"], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U17, part C: a mixed reset over staged, partly staged and untracked work, undone (the
    /// index back from the -changes commit) and redone ("ops" with phase 3's check 10 part C changes).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U17_mixed_reset_with_staged_changes()
    {
        using var repo = await OpsWithPartCChangesAsync();
        var indexBefore = await GitTextAsync(repo, "write-tree");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var text = PartU.ResetText("mixed");

        // Before step 7: G0+w, the stage panel of phase 3's check 10 step 7.
        AssertGraph(app, PartU.G0, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        await ClickWorkingRowAsync(app);
        await AssertStagePanelAsync(app, PartU.PartCUnstaged, PartU.PartCStaged);
        Assert.Equal(PartU.PartCStatus, await StatusAsync(repo));

        // Step 7: the reset to Add core (display row 7), Mixed.
        await ResetToAddCoreAsync(app, 6, new Point(484, 311), null);
        AssertStatus(app, PartU.StatusReset("mixed"));
        Assert.Equal(["## main...origin/main [behind 1]", " M README.md", " M src/core.txt", "?? scratch.txt", "?? todo.txt"], await StatusAsync(repo));
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        AssertArrows(app, 0, 1, "Local branches", "main");
        var changes = await AssertMixedBackupsAsync(repo);
        Assert.Equal(indexBefore, await GitTextAsync(repo, "rev-parse", changes + "^{tree}"));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));

        // Step 8: Undo: restore --staged from the -changes commit, then the transaction.
        var calls = await UndoAsync(app, repo, text);
        await ClickWorkingRowAsync(app);
        var shot = Shot(app, "u17e-mixed-staged-undone");
        AssertWrites(
            app,
            calls,
            ["git restore", "git update-ref", "git read-tree"],
            PartU.Git("restore", "--staged", "--source=" + changes, "--", "."),
            PartU.Transaction(PartU.Undo.Message(text)));
        AssertTransaction(app, calls, PartU.Undo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.MainWorkSha, PartU.AddCoreSha)]);
        Assert.Equal(PartU.PartCStatus, await StatusAsync(repo));
        Assert.Equal(indexBefore, await GitTextAsync(repo, "write-tree"));
        Assert.Equal(PartU.CoreStaged, await GitTextAsync(repo, "show", ":src/core.txt") + "\n");
        Assert.Equal(PartU.Scratch, await GitTextAsync(repo, "show", ":scratch.txt") + "\n");
        AssertPartCWorkingTree(repo);
        await AssertStagePanelAsync(app, PartU.PartCUnstaged, PartU.PartCStaged);
        AssertGraph(app, PartU.G0, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);
        Assert.Equal(changes, await AssertMixedBackupsAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanRedo(text));

        // Step 9: Redo: restore --staged from Add core, then the transaction; no new backup.
        calls = await RedoAsync(app, repo, text);
        shot = Shot(app, "u17f-mixed-staged-redone");
        AssertWrites(
            app,
            calls,
            ["git restore", "git update-ref", "git read-tree"],
            PartU.Git("restore", "--staged", "--source=" + PartU.AddCoreSha, "--", "."),
            PartU.Transaction(PartU.Redo.Message(text)));
        AssertTransaction(app, calls, PartU.Redo.Message(text), [PartU.UpdateLine("refs/heads/main", PartU.AddCoreSha, PartU.MainWorkSha)]);
        Assert.Equal(PartU.AddCoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", " M README.md", " M src/core.txt", "?? scratch.txt", "?? todo.txt"], await StatusAsync(repo));
        AssertPartCWorkingTree(repo);
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertArrows(app, 0, 1, "Local branches", "main");
        Assert.Equal(changes, await AssertMixedBackupsAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U17, part D: a hard reset over part C's changes, undone (a Keep move, the index and
    /// the files back from its snapshot) and redone (the app's own hard reset with a new snapshot,
    /// the same commit) ("ops" with phase 3's check 10 part C changes).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U17_hard_reset()
    {
        using var repo = await OpsWithPartCChangesAsync();
        var indexBefore = await GitTextAsync(repo, "write-tree");
        var cachedBefore = await GitTextAsync(repo, "diff", "--cached");
        var diffBefore = await GitTextAsync(repo, "diff");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var text = PartU.ResetText("hard");

        // Step 10: the hard reset to Add core (display row 7).
        await ResetToAddCoreAsync(app, 6, new Point(484, 311), "ResetHard");
        AssertStatus(app, PartU.StatusReset("hard"));
        Assert.Equal(["## main...origin/main [behind 1]", "?? todo.txt"], await StatusAsync(repo));
        AssertHardResetFiles(repo);
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        AssertArrows(app, 0, 1, "Local branches", "main");
        var (snapshot, index) = await AssertHardBackupsAsync(repo, indexBefore, 1);
        AssertButtons(app, UndoRedoButtons.CanUndo(text));

        // Step 11: Undo, in 7.3's order: refresh, read-tree, the Keep move's update-ref, the index, the files.
        var calls = await UndoAsync(app, repo, text);
        AssertRowSelected(app, 6, PartU.AddCoreSha);
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git restore"],
            PartU.Refresh,
            PartU.ReadTree(PartU.AddCoreSha, PartU.MainWorkSha),
            PartU.KeepMoveRef(PartU.Undo.Message(text), "main", PartU.MainWorkSha, PartU.AddCoreSha),
            PartU.Git("restore", "--staged", "--source=" + index, "--", "."),
            PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", "README.md", "scratch.txt", "src/core.txt"));
        Assert.Equal(0, app.LastCall(PartU.ReadTree(PartU.AddCoreSha, PartU.MainWorkSha)).ExitCode);
        Assert.Equal(PartU.PartCStatus, await StatusAsync(repo));
        Assert.Equal(indexBefore, await GitTextAsync(repo, "write-tree"));
        Assert.Equal(cachedBefore, await GitTextAsync(repo, "diff", "--cached"));
        Assert.Equal(diffBefore, await GitTextAsync(repo, "diff"));
        AssertPartCWorkingTree(repo);
        AssertGraph(app, PartU.G0, PartU.OpsLanes, PartU.WorkingRow, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanRedo(text));
        await ClickWorkingRowAsync(app);
        var shot = Shot(app, "u17g-hard-undone");
        await AssertStagePanelAsync(app, PartU.PartCUnstaged, PartU.PartCStaged);
        AssertGraph(app, PartU.G0, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);

        // Step 12: Redo: the app's hard reset with a new snapshot (the same commit): the two new
        // backups in one transaction, then the reset, with git's own reflog entry.
        calls = await RedoAsync(app, repo, text);
        shot = Shot(app, "u17h-hard-redone");
        var reset = PartU.Git("reset", "--hard", PartU.AddCoreSha, "--");
        AssertCalledInOrder(app, calls, PartU.BackupWrite, reset);
        AssertBackupWrite(
            app,
            calls,
            [
                PartU.CreateLine(PartU.BackupRefName("reset", 2), PartU.MainWorkSha),
                PartU.CreateLine(PartU.BackupRefName("reset", 2, "-changes"), snapshot),
            ]);
        Assert.True(AssertCalled(app, calls, reset).Succeeded, $"\"{reset}\" failed.");
        Assert.Equal(["reset: moving to " + PartU.AddCoreSha], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal(["## main...origin/main [behind 1]", "?? todo.txt"], await StatusAsync(repo));
        AssertHardResetFiles(repo);
        AssertGraph(app, PartU.G6, PartU.OpsLanes, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertArrows(app, 0, 1, "Local branches", "main");
        Assert.Equal(
            [
                $"{PartU.BackupRefName("reset", 1)} {PartU.MainWorkSha}",
                $"{PartU.BackupRefName("reset", 1, "-changes")} {snapshot}",
                $"{PartU.BackupRefName("reset", 2)} {PartU.MainWorkSha}",
                $"{PartU.BackupRefName("reset", 2, "-changes")} {snapshot}",
            ],
            await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(text));
        AssertNoClippedText(app);
    }

    // ---- U17's own helpers ----

    /// <summary>"Ops" with phase 3's check 10 part C changes made outside the app before it starts (UD5).</summary>
    private static async Task<TempRepo> OpsWithPartCChangesAsync()
    {
        var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", PartU.CoreStaged);
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", PartU.CoreUnstaged);
        repo.WriteFile("README.md", PartU.ReadmeLocalEdit);
        repo.WriteFile("scratch.txt", PartU.Scratch);
        await repo.GitAsync("add", "scratch.txt");
        repo.WriteFile("todo.txt", PartU.OpsTodo);
        return repo;
    }

    /// <summary>
    /// Phase 3's reset: Add core (commit row <paramref name="row"/>) right-clicked in its Message
    /// column at the report's point, "Reset main to this commit…", the mode's radio button clicked
    /// (none: Mixed as the dialog opens), then "Reset"; waits until the operation has ended.
    /// </summary>
    private static async Task ResetToAddCoreAsync(ShellDriver app, int row, Point point, string? mode)
    {
        RightClickRowAndChoose(app, row, point, PartU.ResetMenuItem);
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        if (mode is null)
        {
            Assert.True(((RadioButton)app.FindByAutomationId("ResetMixed")).IsChecked == true, "Mixed is not chosen.");
        }
        else
        {
            app.Click(app.FindByAutomationId(mode));
        }

        await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
    }

    /// <summary>
    /// The mixed reset's two backups (U17 step 7): <c>reset-…01</c> at Main work, and its
    /// <c>-changes</c> commit with the one parent Main work, its subject, and the index before the
    /// reset as its tree. Returns the -changes commit.
    /// </summary>
    private static async Task<string> AssertMixedBackupsAsync(TempRepo repo)
    {
        var backups = await BackupsAsync(repo);
        Assert.Equal([PartU.BackupRefName("reset", 1), PartU.BackupRefName("reset", 1, "-changes")], backups.Select(backup => backup.Name));
        Assert.Equal(PartU.MainWorkSha, backups[0].Sha);
        var changes = backups[1].Sha;
        Assert.Equal(PartU.MainWorkSha, await GitTextAsync(repo, "log", "-1", "--format=%P", changes));
        Assert.Equal(PartU.MixedIndexSubject, await GitTextAsync(repo, "log", "-1", "--format=%s", changes));
        Assert.Equal(["README.md", "scratch.txt", "src/core.txt"], await GitLinesAsync(repo, "ls-tree", "-r", "--name-only", changes));
        Assert.Equal(Scenarios.OperationsFiles.Readme, await GitTextAsync(repo, "show", changes + ":README.md") + "\n");
        Assert.Equal(PartU.Scratch, await GitTextAsync(repo, "show", changes + ":scratch.txt") + "\n");
        Assert.Equal(PartU.CoreStaged, await GitTextAsync(repo, "show", changes + ":src/core.txt") + "\n");
        return changes;
    }

    /// <summary>
    /// The hard reset's backups (U17 step 10): <c>reset-…01</c> at Main work and its
    /// <c>-changes</c> snapshot, whose first parent is Main work and second the index commit
    /// (parent Main work, its subject, the index before the reset as its tree), with its subject,
    /// the changed paths as its body, and the files as they were (no todo.txt). Returns the
    /// snapshot and the index commit.
    /// </summary>
    private static async Task<(string Snapshot, string Index)> AssertHardBackupsAsync(TempRepo repo, string indexBefore, int count)
    {
        var backups = await BackupsAsync(repo);
        Assert.Equal([PartU.BackupRefName("reset", count), PartU.BackupRefName("reset", count, "-changes")], backups.Select(backup => backup.Name));
        Assert.Equal(PartU.MainWorkSha, backups[0].Sha);
        var snapshot = backups[1].Sha;
        var parents = (await GitTextAsync(repo, "log", "-1", "--format=%P", snapshot)).Split(' ');
        Assert.Equal(2, parents.Length);
        Assert.Equal(PartU.MainWorkSha, parents[0]);
        var index = parents[1];
        Assert.Equal(PartU.MainWorkSha, await GitTextAsync(repo, "log", "-1", "--format=%P", index));
        Assert.Equal(PartU.HardIndexSubject, await GitTextAsync(repo, "log", "-1", "--format=%s", index));
        Assert.Equal(indexBefore, await GitTextAsync(repo, "rev-parse", index + "^{tree}"));
        Assert.Equal(PartU.HardSnapshotSubject, await GitTextAsync(repo, "log", "-1", "--format=%s", snapshot));
        Assert.Equal(["README.md", "scratch.txt", "src/core.txt"], (await GitTextAsync(repo, "log", "-1", "--format=%b", snapshot)).Trim().Split('\n'));
        Assert.Equal(["README.md", "scratch.txt", "src/core.txt"], await GitLinesAsync(repo, "ls-tree", "-r", "--name-only", snapshot));
        Assert.Equal(PartU.ReadmeLocalEdit, await GitTextAsync(repo, "show", snapshot + ":README.md") + "\n");
        Assert.Equal(PartU.Scratch, await GitTextAsync(repo, "show", snapshot + ":scratch.txt") + "\n");
        Assert.Equal(PartU.CoreUnstaged, await GitTextAsync(repo, "show", snapshot + ":src/core.txt") + "\n");
        return (snapshot, index);
    }

    /// <summary>The working tree as part C left it: README.md's local edit, core.txt's four lines, scratch.txt and todo.txt.</summary>
    private static void AssertPartCWorkingTree(TempRepo repo)
    {
        Assert.Equal(PartU.ReadmeLocalEdit, WorkingFile(repo, "README.md"));
        Assert.Equal(PartU.CoreUnstaged, WorkingFile(repo, "src/core.txt"));
        Assert.Equal(PartU.Scratch, WorkingFile(repo, "scratch.txt"));
        Assert.Equal(PartU.OpsTodo, WorkingFile(repo, "todo.txt"));
    }

    /// <summary>The files after the hard reset: README.md and core.txt as Add core has them, scratch.txt gone, todo.txt untouched.</summary>
    private static void AssertHardResetFiles(TempRepo repo)
    {
        Assert.Equal(Scenarios.OperationsFiles.Readme, WorkingFile(repo, "README.md"));
        Assert.Equal(Scenarios.OperationsFiles.Core, WorkingFile(repo, "src/core.txt"));
        Assert.Null(WorkingFile(repo, "scratch.txt"));
        Assert.Equal(PartU.OpsTodo, WorkingFile(repo, "todo.txt"));
    }
}
