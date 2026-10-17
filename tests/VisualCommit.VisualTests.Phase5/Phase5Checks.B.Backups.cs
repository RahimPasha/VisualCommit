// Check B9 of phase 5's walk-through (docs/test-reports/phase-5.md, part B): the backups list from
// "Show backups" and its restores (design 5.3, 7.5, 7.10, 12, 13.2; D75, BD15 to BD21): a deleted
// branch restored, a deleted remote branch pushed back, discarded files restored with D75's question,
// an abort's and a skip's changes, a mixed reset's staged changes. Four test methods, each on a fresh
// copy of "reflog". The part's map is in Phase5Checks.B.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Views.Pages;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksB
{
    /// <summary>
    /// Check B9, method 1: the list, a cut text's tooltip, the deleted branch doomed restored (its
    /// item then disabled), the deleted remote branch feature/gone pushed back after its question.
    /// "reflog", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B9_backups_list_branch_and_push_back()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        await WaitForCleanUpAsync(app, repo);

        // Step 1: the backups list.
        await OpenBackupsAsync(app);
        var shot = Shot(app, "b9a-backups");
        var page = app.ReflogSnapshot();
        AssertPageInPlaceOfGraph(app, PageBounds, page.Bounds);
        AssertReflogHeader(app, page, BackupsTitle, BackupsTitle);
        AssertBackupRows(app, page, Backups);
        Assert.Equal(PageRowCentreY(AbortBackupRow), page.Backups[AbortBackupRow].Bounds!.Value.Center.Y);
        Assert.True(page.Backups[^1].Bounds!.Value.Bottom <= page.ListBounds.Bottom, "Not every backup is in view.");
        Assert.Equal(((string?)null, (Rect?)null), (page.Footer, page.FooterBounds));
        Assert.Empty(page.Rows);
        Assert.DoesNotContain(app.Find<ListBox>("ReflogList").GetVisualDescendants().OfType<Border>(), mark => mark.Name == "ReflogLostMark" && mark.IsEffectivelyVisible);
        Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "Opening the list changed the right panel.");
        AssertColour(Dark.Window, shot, new Point((BackupTimeX.Right + BackupWordsX.Left) / 2, PageRowCentreY(0)), "list's background between row 0's time and words");
        AssertNoClippedText(app);

        // Step 2: the words of row 9, cut with "…", whole in their tooltip.
        var stash = Backups[StashBackupRow];
        Assert.True(app.BackupWordsBounds(stash.Name).Contains(StashWordsPoint), $"{StashWordsPoint} is not on the words of row 9 ({app.BackupWordsBounds(stash.Name)}).");
        Assert.True(BackupWords(app, StashBackupRow).IsTrimmed, "The words of row 9 are not cut.");
        Assert.Equal(stash.Words, await app.RestPointerAtAsync(StashWordsPoint));
        Shot(app, "b9b-cut-words-tooltip", keepPointer: true);
        AssertToolTipShown(app, stash.Words);

        // Step 3: row 0's menu; the right-click selects it and shows its commit (BD9).
        app.RightClick(BackupRowPointAsReported(app, DoomedBackupRow));
        var menu = app.Menu();
        await AssertDetailsAsync(app, DoomedWork, -1);
        shot = Shot(app, "b9c-branch-menu");
        AssertMenuAsReported(app, menu, BackupMenu("Deleted branch doomed", RestoreBranchDoomed), RestoreBranchDoomed, BackupMenuHeight, shot);
        Assert.True(app.ReflogSnapshot().Backups[DoomedBackupRow].IsSelected, "The right-click did not select row 0.");

        // Step 4: Restore branch doomed, no question; its item then disabled.
        var reads = app.ReflogSnapshot().ReadsShown;
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(RestoreBranchDoomed);
        await AssertOperationDoneAsync(app, repo, RestoreDoomed, mark);
        await AssertNoDialogAsync(app);
        page = await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft, reads);
        Assert.Single(CallsSince(app, calls), call => call == RestoreDoomedCommand);
        var restore = AssertCalled(app, calls, RestoreDoomedCommand);
        Assert.True(restore.Succeeded, $"\"{RestoreDoomedCommand}\" failed: {restore.StandardError}");
        Assert.Equal(RestoreDoomedInput + "\n", restore.StandardInput);
        Assert.Equal(DoomedWorkSha, await repo.RefTargetAsync("refs/heads/doomed"));
        Assert.Equal([DoomedReflog], Split((await repo.GitAsync("log", "--walk-reflogs", "--format=%gs", "refs/heads/doomed")).StandardOutput));
        var config = await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.doomed\.");
        Assert.Equal((1, string.Empty), (config.ExitCode, config.StandardOutput));
        Assert.Equal(["## main...origin/main [ahead 2]"], Split((await repo.GitAsync("status", "--porcelain", "--branch")).StandardOutput));
        Assert.Contains(page.Backups, row => row.Name == Backups[DoomedBackupRow].Name);
        Assert.Equal(DoomedWorkSha, await repo.RefTargetAsync(BackupRef.Prefix + "branch-20260101T122701000Z/doomed"));
        AssertGraph(app, RLB9a, ReflogLanes, showsDate: true);
        var (branches, branchRows) = Section(app, "Local branches");
        Assert.Equal(5, branches);
        Assert.Contains("doomed", branchRows);
        app.RightClick(BackupRowPointAsReported(app, DoomedBackupRow));
        Assert.Equal(BranchDoomedExists, await app.RestPointerOnMenuItemAsync(RestoreBranchDoomed));
        shot = Shot(app, "b9d-restore-disabled", keepPointer: true);
        AssertMenuAsReported(app, app.Menu(), BackupMenu("Deleted branch doomed", $"{RestoreBranchDoomed} (disabled: \"{BranchDoomedExists}\")"), CreateBranchHere, BackupMenuHeight);
        AssertToolTipShown(app, BranchDoomedExists);
        await AssertDetailsAsync(app, DoomedWork, 0);
        await CloseMenuAsync(app);
        var refs = await RefsAsync(repo);

        // Step 5: row 5's menu, the deleted remote branch.
        app.RightClick(BackupRowPointAsReported(app, RemoteBranchBackupRow));
        menu = app.Menu();
        await AssertDetailsAsync(app, AddTheApp, 8);
        shot = Shot(app, "b9e-remote-branch-menu");
        AssertMenuAsReported(app, menu, BackupMenu("Deleted feature/gone on origin", PushFeatureGoneBack), PushFeatureGoneBack, BackupMenuHeight, shot);

        // Step 6: its question.
        app.ClickMenuItem(PushFeatureGoneBack);
        await AssertConfirmationAsync(app, PushBackTitle, PushBackQuestion, string.Empty, Push, destructive: false);
        Shot(app, "b9f-push-question");
        Assert.Equal(refs, await RefsAsync(repo));

        // Step 7: Push; Esc: the graph with feature/gone's remote-tracking label on Add the app.
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertOperationDoneAsync(app, repo, PushFeatureGone, mark, NetworkTimeout);
        var push = AssertCalled(app, calls, PushBackCommand);
        Assert.True(push.Succeeded, $"The push exited {push.ExitCode}.");
        if (await IsReflogTextGitAsync())
        {
            Assert.Contains(PushBackSummary.Trim(), push.StandardError.Replace("\r", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.Trim()));
        }

        var remote = Split((await repo.GitAsync("ls-remote", "origin")).StandardOutput)
            .Select(line => line.Split('\t'))
            .Where(parts => parts[1].StartsWith("refs/heads/", StringComparison.Ordinal))
            .Select(parts => (parts[1], parts[0]))
            .ToList();
        Assert.Equal(OriginHeadsAfterPush, remote);
        Assert.Equal(AddTheAppSha, await repo.RefTargetAsync("refs/remotes/origin/feature/gone"));
        Assert.Equal(["update by push"], Split((await repo.GitAsync("log", "--walk-reflogs", "--format=%gs", "refs/remotes/origin/feature/gone")).StandardOutput));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        shot = Shot(app, "b9g-pushed-back");
        AssertNoClippedText(app);
        AssertGraph(app, RLB9b, ReflogLanes, showsDate: true, picture: shot);
        AssertRowWhollyInView(app, 8, 337);
        await AssertDetailsAsync(app, AddTheApp, 8);
        Assert.Equal(3, Section(app, "Remotes").Count);
    }

    /// <summary>
    /// Check B9, method 2: the discard snapshot restored over a file changed since (BD19): D75's
    /// question, the newer version saved first in a snapshot of its own. "reflog", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B9_discarded_files_with_the_question()
    {
        using var repo = await Scenarios.ReflogAsync();
        repo.WriteFile("src/app.txt", OwnEdit);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        await WaitForCleanUpAsync(app, repo);

        // Step 8: the discard's menu; the working-changes row behind the page.
        await OpenBackupsAsync(app);
        Assert.True(Graph(app).ShowsWorkingRow, "The working-changes row does not show behind the page.");
        Assert.Equal([" M src/app.txt"], await StatusAllAsync(repo));
        app.RightClick(BackupRowPointAsReported(app, DiscardBackupRow));
        var menu = app.Menu();
        await AssertDetailsAsync(app, DiscardSnapshot, -1);
        var shot = Shot(app, "b9h-discard-menu");
        AssertMenuAsReported(app, menu, BackupMenu("Before discarding 2 files", RestoreTheDiscardedFiles), RestoreTheDiscardedFiles, BackupMenuHeight, shot);
        var refs = await RefsAsync(repo);

        // Step 9: D75's question: only src/app.txt counts as changed (BD15).
        app.ClickMenuItem(RestoreTheDiscardedFiles);
        await AssertConfirmationAsync(app, RestoreQuestionTitle, RestoreQuestionOneFile, RestoreQuestionDetail, Restore);
        Shot(app, "b9i-restore-question");
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal([" M src/app.txt"], await StatusAllAsync(repo));
        Assert.Equal(OwnEdit, ReadFile(repo, "src/app.txt"));

        // Step 10: Restore: the newer version's snapshot first, then the restore.
        var reads = app.ReflogSnapshot().ReadsShown;
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertOperationDoneAsync(app, repo, RestoreDiscardedFiles, mark);
        var page = await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft + 1, reads);
        Shot(app, "b9j-files-restored");
        AssertNoClippedText(app);
        await AssertNewSnapshotAsync(app, repo, calls, OwnEditSnapshot, OwnEdit, RestoreDiscardCommand);
        Assert.Equal(AppStaged, ReadFile(repo, "src/app.txt"));
        Assert.Equal(Todo, ReadFile(repo, "notes/todo.txt"));
        Assert.Equal([" M src/app.txt", "?? notes/todo.txt"], await StatusAllAsync(repo));
        Assert.Empty(Split((await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput));
        AssertBackupRows(app, page, [new(FirstNewBackupName, FirstNewBackupTime, NewSnapshotWords, OwnEditSnapshot[..7], NewSnapshotSubject), .. Backups]);
    }

    /// <summary>
    /// Check B9, method 3: the abort's changes restored without a question (nothing changed since,
    /// BD20: every path counts), then the skip's with D75's question. "reflog", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B9_abort_and_skip_changes()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        await WaitForCleanUpAsync(app, repo);

        // Step 11: the abort's menu; Restore the changes…, no question.
        await OpenBackupsAsync(app);
        app.RightClick(BackupRowPointAsReported(app, AbortBackupRow));
        var menu = app.Menu();
        await AssertDetailsAsync(app, MainWork, 6);
        var shot = Shot(app, "b9k-abort-menu");
        AssertMenuAsReported(app, menu, BackupMenu("Before aborting", RestoreTheChanges), RestoreTheChanges, BackupMenuHeight, shot);
        var reads = app.ReflogSnapshot().ReadsShown;
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(RestoreTheChanges);
        await AssertOperationDoneAsync(app, repo, RestoreAbortChanges, mark);
        await AssertNoDialogAsync(app);
        await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft, reads);
        Shot(app, "b9l-abort-restored");
        AssertNoClippedText(app);
        Assert.True(AssertCalled(app, calls, RestoreAbortCommand).Succeeded);
        Assert.Equal(ClashNotes, ReadFile(repo, "docs/clash.txt"));
        Assert.Equal(AppMainWorkAndClash, ReadFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await StatusAllAsync(repo));
        Assert.Equal(BackupsLeft, (await BackupsAsync(repo)).Select(entry => entry.Ref.Stem).Distinct().Count());

        // Step 12: the skip's: src/app.txt has changed since, so D75's question; Restore.
        app.RightClick(BackupRowPointAsReported(app, SkipBackupRow));
        AssertMenuAsReported(app, app.Menu(), BackupMenu("Before skipping a commit", RestoreTheChanges), RestoreTheChanges, BackupMenuHeight);
        await AssertDetailsAsync(app, MainWork, 6);
        app.ClickMenuItem(RestoreTheChanges);
        await AssertConfirmationAsync(app, RestoreQuestionTitle, RestoreQuestionOneFile, RestoreQuestionDetail, Restore);
        Shot(app, "b9m-skip-question");
        Assert.Equal(AppMainWorkAndClash, ReadFile(repo, "src/app.txt"));
        reads = app.ReflogSnapshot().ReadsShown;
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertOperationDoneAsync(app, repo, RestoreSkipChanges, mark);
        var page = await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft + 1, reads);
        Shot(app, "b9n-skip-restored");
        AssertNoClippedText(app);
        await AssertNewSnapshotAsync(app, repo, calls, SkipNewerSnapshot, AppMainWorkAndClash, RestoreSkipCommand);
        Assert.Equal(AppResolvedThenSkipped, ReadFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await StatusAllAsync(repo));
        AssertBackupRows(app, page, [new(FirstNewBackupName, FirstNewBackupTime, NewSnapshotWords, SkipNewerSnapshot[..7], NewSnapshotSubject), .. Backups]);
    }

    /// <summary>
    /// Check B9, method 4: the mixed reset's staged changes restored for the snapshot's own path
    /// (BD16), then the item disabled; the stage panel. "reflog", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B9_staged_changes_of_a_mixed_reset()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        await WaitForCleanUpAsync(app, repo);
        var appAtHead = ReadFile(repo, "src/app.txt");

        // Step 13: the mixed reset's menu.
        await OpenBackupsAsync(app);
        app.RightClick(BackupRowPointAsReported(app, MixedResetBackupRow));
        var menu = app.Menu();
        await AssertDetailsAsync(app, ClashNotes316, 3);
        var shot = Shot(app, "b9o-mixed-reset-menu");
        AssertMenuAsReported(app, menu, BackupMenu("Before a mixed reset", RestoreTheStagedChanges), RestoreTheStagedChanges, BackupMenuHeight, shot);

        // Step 14: Restore the staged changes, no question; the item then disabled.
        var reads = app.ReflogSnapshot().ReadsShown;
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(RestoreTheStagedChanges);
        await AssertOperationDoneAsync(app, repo, RestoreStagedChanges, mark);
        await AssertNoDialogAsync(app);
        await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft, reads);
        Assert.True(AssertCalled(app, calls, RestoreStagedCommand).Succeeded);
        Assert.Equal([StagedAppEntry], Split((await repo.GitAsync("ls-files", "--stage", "src/app.txt")).StandardOutput));
        Assert.Equal(AppStaged, (await repo.GitAsync("show", ":src/app.txt")).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(appAtHead, ReadFile(repo, "src/app.txt"));
        Assert.Equal(["MM src/app.txt"], await StatusAllAsync(repo));
        Assert.Contains("+staged", Split((await repo.GitAsync("diff", "--cached")).StandardOutput));
        Assert.Contains("-staged", Split((await repo.GitAsync("diff")).StandardOutput));
        Assert.Equal(NotesWritten, ReadFile(repo, "docs/notes.txt"));
        Assert.Equal(BackupsLeft, (await BackupsAsync(repo)).Select(entry => entry.Ref.Stem).Distinct().Count());
        app.RightClick(BackupRowPointAsReported(app, MixedResetBackupRow));
        Assert.Equal(UnstageYourChangesFirst, await app.RestPointerOnMenuItemAsync(RestoreTheStagedChanges));
        Shot(app, "b9p-staged-disabled", keepPointer: true);
        AssertMenuAsReported(app, app.Menu(), BackupMenu("Before a mixed reset", $"{RestoreTheStagedChanges} (disabled: \"{UnstageYourChangesFirst}\")"), CreateBranchHere, BackupMenuHeight);
        AssertToolTipShown(app, UnstageYourChangesFirst);

        // Step 15: Esc twice; the working-changes row: the stage panel.
        await CloseMenuAsync(app);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        app.Click(WorkingRowPointAsReported(app));
        await AssertStagePanelAsync(app, "Unstaged files (1)", ["M app.txt src"], "Staged files (1)", ["M app.txt src"]);
        Shot(app, "b9q-stage-panel");
        AssertNoClippedText(app);
        await AssertNoMenuAsync(app, null);
        Assert.Null(repository.Page);
        AssertGraph(app, RL0, ReflogLanes, new WorkingChangesRow("Working changes", 0, 0), showsDate: true);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected.");
    }

    /// <summary>Opens the backups list from the Local branches section's menu (5.1) and waits for its 15 rows.</summary>
    private static async Task<ReflogSnapshot> OpenBackupsAsync(ShellDriver app)
    {
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.ClickMenuItem("Show backups");
        return await WaitForReflogAsync(app, BackupsTitle, BackupsTitle, BackupsLeft);
    }

    /// <summary>The words of backup row <paramref name="row"/>, as drawn.</summary>
    private static TrimmedTextBlock BackupWords(ShellDriver app, int row)
    {
        var item = (ListBoxItem)app.Find<ListBox>("ReflogList").ContainerFromIndex(row)!;
        return Named<TrimmedTextBlock>(item, "BackupWords");
    }

    /// <summary>A file of the working tree, its line endings as written.</summary>
    private static string ReadFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>
    /// Asserts BD15's snapshot of the newer version, made before the restore (D75): the run's
    /// first backup <c>discard-20260102T090001000Z</c> → <paramref name="snapshot"/> (asserted by
    /// its id, D72's dates fix it), a commit on HEAD by <c>VisualCommit</c> at 2026-01-02 09:00
    /// with its message and src/app.txt <paramref name="newer"/>; then the restore
    /// <paramref name="restore"/>, after it; 16 backups.
    /// </summary>
    private static async Task AssertNewSnapshotAsync(ShellDriver app, TempRepo repo, int calls, string snapshot, string newer, string restore)
    {
        Assert.Equal(snapshot, await repo.RefTargetAsync(FirstNewBackup));
        var commit = Split((await repo.GitAsync("show", "--no-patch", "--format=%P%n%an <%ae>%n%cn <%ce>%n%aI%n%cI", snapshot)).StandardOutput);
        Assert.Equal([MainTip, SnapshotIdentity, SnapshotIdentity], commit.Take(3));
        AssertGitDate(AppCommitTime, commit[3]);
        AssertGitDate(AppCommitTime, commit[4]);
        Assert.Equal(NewSnapshotMessage, (await repo.GitAsync("show", "--no-patch", "--format=%B", snapshot)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'));
        Assert.Equal(newer, (await repo.GitAsync("show", snapshot + ":src/app.txt")).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        var made = CallsSince(app, calls);
        var saved = made.FindIndex(call => call.Contains(FirstNewBackup, StringComparison.Ordinal));
        var restored = made.FindIndex(call => call == restore);
        Assert.True(saved >= 0 && restored > saved, $"The snapshot was not saved before \"{restore}\". The app ran:\n" + string.Join('\n', made));
        Assert.True(app.GitCalls().Skip(calls).Single(call => call.CommandText == restore).Succeeded);
        Assert.Equal(BackupsLeft + 1, (await BackupsAsync(repo)).Select(entry => entry.Ref.Stem).Distinct().Count());
    }
}
