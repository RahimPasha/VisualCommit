using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>Phase 2's checks of discarding with the dialog and restoring, and of the working-changes row in other repositories.</summary>
public partial class Phase2Checks
{
    private const string SnapshotLine = "A snapshot is saved first, so the changes can be restored.";

    [AvaloniaFact]
    public async Task Check_17_discard_a_file_cancel_discard_restore_dismiss()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        var file = Path.Combine(repo.Path, "src", "Calculator.cs");
        var before = File.ReadAllBytes(file);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await ClickRowButtonAsync(app, "UnstagedFileList", "src/Calculator.cs", "RowDiscardButton");
        await WaitForDialogAsync(app);
        var dialog = app.Capture();
        dialog.Save(Phase, "17a-discard-dialog");
        AssertDialog(app, dialog, "Discard the changes to Calculator.cs?");
        Assert.False(app.Session.Shell.ActiveTab.Repository!.IsDiffOpen);

        app.Click(app.Find<Button>("CancelButton"));
        await WaitForNoDialogAsync(app);
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Equal("Unstaged files (7)", app.Find<TextBlock>("UnstagedTitle").Text);

        await ClickRowButtonAsync(app, "UnstagedFileList", "src/Calculator.cs", "RowDiscardButton");
        await WaitForDialogAsync(app);
        app.Click(app.Find<Button>("ConfirmButton"));
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 3);
        await app.WaitForAsync(() => app.Find<Border>("RestoreBar").IsEffectivelyVisible, "the restore bar");
        app.MoveMouse(new Point(130, 700 - 13));
        var discarded = app.Capture();
        discarded.Save(Phase, "17b-discarded");
        Assert.False(app.FindByAutomationId("ConfirmationDialog").IsEffectivelyVisible);
        Assert.Equal("Discarded changes to Calculator.cs.", app.Find<TextBlock>("RestoreBarText").Text);
        Assert.True(app.Find<Button>("RestoreDiscardButton").IsEffectivelyVisible);
        Assert.True(app.Find<Button>("DismissRestoreButton").IsEffectivelyVisible);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted, File.ReadAllText(file));
        Assert.DoesNotContain("M Calculator.cs src", FileRows(app, "UnstagedFileList"));
        var snapshots = await SnapshotRefsAsync(repo);
        var snapshot = Assert.Single(snapshots);
        Assert.Equal(await repo.HeadAsync(), (await repo.GitAsync("rev-parse", snapshot + "^")).StandardOutput.Trim());
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, (await repo.GitAsync("show", snapshot + ":src/Calculator.cs")).StandardOutput);
        var snapshotCommit = (await repo.GitAsync("rev-parse", snapshot)).StandardOutput.Trim();
        Assert.Equal(-1, Graph(app).Data!.IndexOf(snapshotCommit));
        Assert.DoesNotContain(ShellExpectations.TextsIn(app.FindByAutomationId("LeftPanel")), text => text.Contains("visualcommit", StringComparison.OrdinalIgnoreCase));

        app.Click(app.Find<Button>("RestoreDiscardButton"));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        await app.WaitForAsync(() => !app.Find<Border>("RestoreBar").IsEffectivelyVisible, "the restore bar to go");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "17c-restored");
        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.Contains("M Calculator.cs src", FileRows(app, "UnstagedFileList"));
        Assert.Single(await SnapshotRefsAsync(repo));

        await ClickRowButtonAsync(app, "UnstagedFileList", "src/Calculator.cs", "RowDiscardButton");
        await WaitForDialogAsync(app);
        app.Click(app.Find<Button>("ConfirmButton"));
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 3);
        await app.WaitForAsync(() => app.Find<Border>("RestoreBar").IsEffectivelyVisible, "the restore bar");
        app.Click(app.Find<Button>("DismissRestoreButton"));
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "17d-dismissed");
        Assert.False(app.Find<Border>("RestoreBar").IsEffectivelyVisible);
        Assert.Equal("Unstaged files (6)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted, File.ReadAllText(file));
        Assert.Equal(2, (await SnapshotRefsAsync(repo)).Count);
    }

    [AvaloniaFact]
    public async Task Check_18_discard_all_then_restore_untracked_and_binary_files_included()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        var before = WorkingTreeBytes(repo);
        var index = (await repo.GitAsync("write-tree")).StandardOutput.Trim();
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        app.Click(app.Find<Button>("DiscardAllButton"));
        await WaitForDialogAsync(app);
        var dialog = app.Capture();
        dialog.Save(Phase, "18a-discard-all-dialog");
        AssertDialog(app, dialog, "Discard the changes to 7 files?");

        app.PressKey(Key.Escape);
        await WaitForNoDialogAsync(app);
        Assert.Equal(before, WorkingTreeBytes(repo));

        app.Click(app.Find<Button>("DiscardAllButton"));
        await WaitForDialogAsync(app);
        app.Click(app.Find<Button>("ConfirmButton"));
        await WaitForStagePanelAsync(app, unstaged: 0, staged: 3);
        await app.WaitForAsync(() => app.Find<Border>("RestoreBar").IsEffectivelyVisible, "the restore bar");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "18b-all-discarded");
        Assert.True(app.Find<TextBlock>("NoUnstaged").IsEffectivelyVisible);
        Assert.Equal("Discarded changes to 7 files.", app.Find<TextBlock>("RestoreBarText").Text);
        Assert.False(File.Exists(Path.Combine(repo.Path, "docs", "guide.md")));
        Assert.Equal(Scenarios.ChangesFiles.OldNotesCommitted, File.ReadAllText(Path.Combine(repo.Path, "docs", "old-notes.txt")));
        Assert.Equal(Scenarios.ChangesFiles.ReadmeStaged, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal(Scenarios.ChangesFiles.LogoCommitted, File.ReadAllBytes(Path.Combine(repo.Path, "assets", "logo.png")));
        Assert.Equal(Scenarios.ChangesFiles.BlobCommitted, File.ReadAllBytes(Path.Combine(repo.Path, "data", "blob.bin")));
        Assert.Equal(Scenarios.ChangesFiles.LargeCommitted, File.ReadAllText(Path.Combine(repo.Path, "data", "large.txt")));
        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));
        Assert.Empty((await repo.GitAsync("diff", "--name-only")).StandardOutput);
        Assert.Equal(index, (await repo.GitAsync("write-tree")).StandardOutput.Trim());

        app.Click(app.Find<Button>("RestoreDiscardButton"));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "18c-all-restored");
        Assert.Equal(before, WorkingTreeBytes(repo));
        Assert.Equal(Phase2Expectations.UnstagedFlat, FileRows(app, "UnstagedFileList"));
        Assert.Equal(index, (await repo.GitAsync("write-tree")).StandardOutput.Trim());
    }

    [AvaloniaFact]
    public async Task Check_23_the_working_changes_row_when_heads_commit_is_not_the_first()
    {
        using var repo = await Scenarios.GraphAsync();
        repo.WriteFile("todo.txt", "Later\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 13);
        var shot = app.Capture();
        shot.Save(Phase, "23-working-row-above-the-stash");

        var graph = Graph(app);
        Assert.True(graph.ShowsWorkingRow);
        Assert.Equal(CommitGraphControl.RowHeight, graph.RowBounds(0).Top - graph.WorkingRowBounds.Top);
        Assert.Equal("On main: Work in progress on README", graph.Data!.CommitAt(0).Subject);
        Assert.Equal("Add settings page", graph.Data.CommitAt(2).Subject);
        Assert.Equal(graph.Data.Refs.Head.Sha, graph.Data.CommitAt(2).Sha);
        AssertRing(shot, WorkingRingCentre(app), Palettes.Dark.Lane(0), "working-changes row's ring");
        AssertDashedLine(app, shot, Palettes.Dark.Lane(0));

        // The stash's ring is drawn over the dashed line, as in phase 1.
        var origin = app.BoundsOf(graph).TopLeft;
        var stash = new Point(origin.X + graph.LaneCenterX(0), origin.Y + graph.RowBounds(0).Center.Y);
        AssertRing(shot, stash, Palettes.Dark.Lane(0), "stash's ring");
        Assert.Equal(Palettes.Dark.Window, shot.PixelAt(stash));
    }

    [AvaloniaFact]
    public async Task Check_24_the_first_commit_of_a_new_repository()
    {
        using var data = new TempDirectory("data");
        using var folders = new TempDirectory("init");
        var fresh = folders.Combine("fresh");
        Directory.CreateDirectory(fresh);
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var tab = app.Session.Shell.ActiveTab;
        app.Folders.Answer(fresh);
        app.Click(app.Find<Button>("InitRepoButton"));
        await app.WaitForAsync(() => tab.Repository?.HasNoCommits == true, "the new repository");

        await GitInAsync(fresh, "config", "user.name", TempRepo.AuthorName);
        await GitInAsync(fresh, "config", "user.email", TempRepo.AuthorEmail);
        File.WriteAllText(Path.Combine(fresh, "first.txt"), "First\n");
        await app.WaitForAsync(() => tab.Repository!.HasWorkingChanges, "the working-changes row", TimeSpan.FromSeconds(5));
        app.MoveMouse(new Point(130, 700 - 13));
        var unborn = app.Capture();
        unborn.Save(Phase, "24a-working-row-without-commits");
        var graph = Graph(app);
        Assert.True(graph.ShowsWorkingRow);
        AssertRing(unborn, WorkingRingCentre(app), Palettes.Dark.Lane(0), "working-changes row's ring");
        var below = WorkingRingCentre(app) + new Point(-1, 7);
        Assert.Equal(Palettes.Dark.Window, unborn.PixelAt(below));
        Assert.True(app.Find<StackPanel>("NoCommits").IsEffectivelyVisible);

        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 1, staged: 0);
        app.Capture().Save(Phase, "24b-stage-panel-without-commits");
        Assert.Equal(["A first.txt"], FileRows(app, "UnstagedFileList"));
        Assert.False(app.Find<CheckBox>("AmendCheckBox").IsEffectivelyEnabled);

        await ClickRowButtonAsync(app, "UnstagedFileList", "first.txt", "RowStageButton");
        await WaitForStagePanelAsync(app, unstaged: 0, staged: 1);
        app.Click(app.Find<TextBox>("CommitSummary"));
        app.Type("First commit");
        Assert.Equal("Commit 1 file", app.Find<Button>("CommitButton").Content);
        app.Click(app.Find<Button>("CommitButton"));
        await app.WaitForAsync(() => tab.Repository!.Graph is { IsComplete: true, Count: 1 } && !tab.Repository.HasWorkingChanges && tab.Repository.SelectedIndex == 0, "the first commit, selected");
        await WaitForDetailsAsync(app, "First commit");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "24c-first-commit");

        Assert.False(graph.ShowsWorkingRow);
        Assert.Equal(["First commit"], Subjects(graph));
        Assert.Equal(await GitInAsync(fresh, "symbolic-ref", "--short", "HEAD"), Assert.Single(graph.LabelsInRow(0)).FullText);
        Assert.Equal("2026-01-02 09:00", tab.Repository!.Dates.Format(graph.Data!.CommitAt(0).AuthorDate));
        Assert.False(app.Find<StackPanel>("NoCommits").IsEffectivelyVisible);
        Assert.Contains("Changed files (1)", ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal(await ExpectedFirstCommitAsync(folders), graph.Data.CommitAt(0).Sha);
    }

    private static Task WaitForDialogAsync(ShellDriver app) =>
        app.WaitForAsync(() => app.FindByAutomationId("ConfirmationDialog").IsEffectivelyVisible && app.Find<Button>("CancelButton").IsFocused, "the confirmation dialog, Cancel focused");

    private static Task WaitForNoDialogAsync(ShellDriver app) =>
        app.WaitForAsync(() => !app.FindByAutomationId("ConfirmationDialog").IsEffectivelyVisible, "the dialog to close");

    /// <summary>The discard dialog as "The confirmation dialog" says: texts, buttons, the focus on Cancel, the backdrop over the window.</summary>
    private static void AssertDialog(ShellDriver app, Screenshot screenshot, string question)
    {
        Assert.Equal("Discard changes?", app.Find<TextBlock>("ConfirmTitle").Text);
        Assert.Equal(question, app.Find<TextBlock>("ConfirmQuestion").Text);
        Assert.Equal(SnapshotLine, app.Find<TextBlock>("ConfirmDetail").Text);
        Assert.Equal("Cancel", app.Find<Button>("CancelButton").Content);
        Assert.Equal("Discard", app.Find<Button>("ConfirmButton").Content);
        Assert.True(app.Find<Button>("CancelButton").IsFocused);
        var card = app.BoundsOf(app.Find<Border>("ConfirmationCard"));
        Assert.Equal(400, card.Width, ShellExpectations.SizeTolerance);
        Assert.Equal(app.Window.ClientSize.Width / 2, card.Center.X, ShellExpectations.SizeTolerance);
        Assert.Equal(app.Window.ClientSize.Height / 2, card.Center.Y, ShellExpectations.SizeTolerance);
        Assert.True(app.BoundsOf(app.Find<Button>("CancelButton")).Right <= app.BoundsOf(app.Find<Button>("ConfirmButton")).Left - 7.5);

        // The backdrop: black at 60% over the tab strip's chrome colour.
        var chrome = Palettes.Dark.Chrome;
        var expected = Color.FromRgb((byte)Math.Round(chrome.R * 0.4), (byte)Math.Round(chrome.G * 0.4), (byte)Math.Round(chrome.B * 0.4));
        var sample = screenshot.PixelAt(new Point(app.Window.ClientSize.Width - 300, 10));
        Assert.True(Math.Abs(sample.R - expected.R) <= 2 && Math.Abs(sample.G - expected.G) <= 2 && Math.Abs(sample.B - expected.B) <= 2, $"The backdrop over the tab strip is {sample}, not {expected}.");
        ShellExpectations.AssertColour(Palettes.Dark.Danger, screenshot, app.BoundsOf(app.Find<Button>("ConfirmButton")).TopLeft + new Point(4, 4), "the Discard button");
    }

    private static async Task<List<string>> SnapshotRefsAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("for-each-ref", "--format=%(refname)", "refs/visualcommit/backup/")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Runs git in a folder, cut off from the machine as the app is (the test process's isolation), and returns its trimmed output.</summary>
    private static async Task<string> GitInAsync(string folder, params string[] arguments)
    {
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!);
        var command = new GitCommand(arguments) { WorkingDirectory = folder };
        return (await runner.RunAsync(command, TestContext.Current.CancellationToken)).EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>The commit plain git makes for check 24: the same file, message, identity and date in a new repository.</summary>
    private static async Task<string> ExpectedFirstCommitAsync(TempDirectory folders)
    {
        var plain = folders.Combine("plain");
        Directory.CreateDirectory(plain);
        await GitInAsync(plain, "init", "--quiet");
        await GitInAsync(plain, "config", "user.name", TempRepo.AuthorName);
        await GitInAsync(plain, "config", "user.email", TempRepo.AuthorEmail);
        File.WriteAllText(Path.Combine(plain, "first.txt"), "First\n");
        await GitInAsync(plain, "add", "first.txt");
        await GitInAsync(plain, "commit", "--quiet", "--message", "First commit");
        return await GitInAsync(plain, "rev-parse", "HEAD");
    }
}
