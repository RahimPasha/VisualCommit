// Part A, checks 4 and 26 (docs/test-reports/phase-3.md): the menus of files in the stage lists,
// the diff view and a commit's files, the working-changes row's menu, and ignoring files.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksA
{
    /// <summary>Check 4: file menus in the stage lists, the diff view (staged, unstaged with and without lines, a commit's), a commit's files and the working-changes row (changes).</summary>
    [AvaloniaFact]
    public async Task Check_04_file_menus()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var calculator = Path.Combine(repo.Path, "src", "Calculator.cs");
        // The app passes on the folder as git names it (macOS: /private/var), not as the test made it.
        var workingTree = await WorkingTreeAsync(repo);
        var snapshots = await GitTextAsync(repo, "for-each-ref", "refs/visualcommit");

        // Step 1: the unstaged, tracked src/Calculator.cs.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        var row = await app.FileRowAsync("UnstagedFileList", "src/Calculator.cs");
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "04a-unstaged-tracked-menu");
        AssertMenu(menu, PartA.CalculatorUnstagedMenu);
        AssertHighlighted(menu, "Stage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        Assert.Equal(PartA.OnlyUntracked, menu.Item("Ignore all *.cs files").ToolTip);
        Assert.False(((ChangedFileRow)row.DataContext!).IsSelected);
        Assert.False(repository.IsDiffOpen);
        Assert.True(Graph(app).IsEffectivelyVisible);
        var bytes = File.ReadAllBytes(calculator);
        app.ClickMenuItem("Discard changes…");
        await AssertConfirmationAsync(app, "Discard changes?", "Discard the changes to Calculator.cs?", PartA.SnapshotLine, "Discard");
        Shot(app, "04b-discard-from-menu");
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(bytes, File.ReadAllBytes(calculator));
        Assert.Equal(snapshots, await GitTextAsync(repo, "for-each-ref", "refs/visualcommit"));

        // Step 2: the untracked docs/guide.md; "Show in folder", then "Copy path".
        row = await app.FileRowAsync("UnstagedFileList", "docs/guide.md");
        pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04c-untracked-menu");
        AssertMenu(menu, PartA.GuideUntrackedMenu);
        // Every item but phase 5's File history and Blame, which an untracked file cannot use (4.4, 14.1).
        Assert.All(menu.Items.Where(item => item.Label is not ("File history" or "Blame")), item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertHighlighted(menu, "Stage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Show in folder");
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the file manager to be asked");
        Assert.Equal([FullPath(Path.Combine(workingTree, "docs", "guide.md"))], app.Launcher.Revealed.Select(FullPath));
        app.RightClick(app.BoundsOf(await app.FileRowAsync("UnstagedFileList", "docs/guide.md")).Center);
        app.Menu();
        app.ClickMenuItem("Copy path");
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the path on the clipboard");
        Assert.Equal(["docs/guide.md"], app.Clipboard.Texts);
        Assert.Single(app.Launcher.Requests);

        // Step 3: the staged config/settings.json; "Unstage".
        var settings = File.ReadAllBytes(Path.Combine(repo.Path, "config", "settings.json"));
        row = await app.FileRowAsync("StagedFileList", "config/settings.json");
        pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04d-staged-menu");
        AssertMenu(menu, PartA.SettingsStagedMenu);
        AssertHighlighted(menu, "Unstage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Unstage");
        await WaitForStagePanelAsync(app, unstaged: 8, staged: 2);
        await app.WaitForIdleAsync();
        Shot(app, "04e-unstaged-from-menu");
        Assert.Equal(PartA.StagedAfterUnstage, FileRows(app, "StagedFileList"));
        Assert.Equal(PartA.UnstagedAfterUnstage, FileRows(app, "UnstagedFileList"));
        Assert.Empty(await GitTextAsync(repo, "ls-files", "--stage", "config/settings.json"));
        Assert.Equal(settings, File.ReadAllBytes(Path.Combine(repo.Path, "config", "settings.json")));

        // Step 4: "Open file" on the staged src/helpers.py.
        var state = await GitStateAsync(repo);
        app.RightClick(app.BoundsOf(await app.FileRowAsync("StagedFileList", "src/helpers.py")).Center);
        app.Menu();
        app.ClickMenuItem("Open file");
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "the file to be opened");
        Assert.Equal([FullPath(Path.Combine(workingTree, "src", "helpers.py"))], app.Launcher.Opened.Select(FullPath));
        await AssertNoDialogAsync(app);
        Assert.Equal(state, await GitStateAsync(repo));

        // Step 5: README.md's staged diff, right-clicked on its line "## Usage".
        app.Click(await app.FileRowAsync("StagedFileList", "README.md"));
        await WaitForTextDiffAsync(app, "README.md");
        Assert.True(repository.Diff!.IsStaged);
        Assert.Equal(PartA.ReadmeStagedDiffHeaderAndAdded, DiffRows(app).Where(line => !line.StartsWith("C ", StringComparison.Ordinal)));
        Assert.Single(DiffRows(app), line => line.StartsWith("@@", StringComparison.Ordinal));
        pointer = DiffTextPoint(app, DiffRowOfLine(app, PartA.UsageLine));
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04f-staged-diff-menu");
        AssertMenu(menu, PartA.ReadmeStagedDiffMenu);
        AssertHighlighted(menu, "Unstage hunk");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.ReadmeStagedDiffMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.True(repository.IsDiffOpen);
        Assert.Equal("README.md", repository.Diff!.Target.Path);

        // Step 6: Calculator.cs's unstaged diff, right-clicked on line 15 (its first hunk), no line selected.
        app.Click(await app.FileRowAsync("UnstagedFileList", "src/Calculator.cs"));
        await WaitForTextDiffAsync(app, "src/Calculator.cs");
        Assert.Equal(PartA.CalculatorInline, DiffRows(app));
        pointer = DiffTextPoint(app, DiffRowOfLine(app, 15));
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04g-diff-menu-no-selection");
        AssertMenu(menu, PartA.CalculatorDiffMenu);
        AssertHighlighted(menu, "Stage hunk");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.True(repository.IsDiffOpen);

        // Step 7: line 33 selected by its number, then its menu; "Stage selected lines".
        var negate = new DiffLineRef(1, 4);
        app.Click(LineNumberPoint(app, negate));
        await app.WaitForAsync(() => DiffText(app).SelectedChanges.SetEquals([negate]), "line 33 to be selected");
        pointer = DiffTextPoint(app, DiffRowOfLine(app, 33));
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04h-diff-menu-lines");
        Assert.True(DiffText(app).SelectedChanges.SetEquals([negate]), "The right-press changed the selected lines.");
        // Sampled behind the line's first character, a space of its indentation: away from the
        // menu and its shadow, and from the line numbers' column, which keeps the added colour.
        var indentation = DiffText(app).TextRangeBounds(negate, 0, 1)!.Value;
        AssertColour(PaletteOf(app).Selection, shot, DiffText(app).TranslatePoint(indentation.Center, app.Window)!.Value, "selection background of line 33");
        AssertMenu(menu, PartA.CalculatorDiffLinesMenu);
        AssertHighlighted(menu, "Stage selected lines");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Stage selected lines");
        await WaitForStagePanelAsync(app, unstaged: 8, staged: 3);
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => DiffRows(app).SequenceEqual(PartA.CalculatorInline.Take(9).Concat(PartA.CalculatorSecondHunkAfterNegate)), "the unstaged diff after the line was staged");
        Shot(app, "04i-line-staged");
        var cached = await GitLinesAsync(repo, "diff", "--cached", "--", "src/Calculator.cs");
        Assert.Equal([PartA.NegateStagedHunkHeader], cached.Where(line => line.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Equal(["+" + PartA.NegateLine], cached.Where(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)));
        var index = (await GitTextAsync(repo, "show", ":src/Calculator.cs")).Split('\n');
        Assert.Equal(33, index.Length);
        Assert.Equal(PartA.NegateLine, index[31]);
        Assert.Equal(PartA.StagedAfterNegate, FileRows(app, "StagedFileList"));

        // Step 8: Esc closes the diff; a commit's file in the details panel, then its diff.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !repository.IsDiffOpen, "the diff to close");
        var addCalculator = Enumerable.Range(0, repository.Graph.Count).Single(index => repository.Graph.CommitAt(index).Subject == "Add calculator");
        app.Click(app.GraphRowPoint(addCalculator));
        await WaitForDetailsAsync(app, "Add calculator");
        row = await app.FileRowAsync("FileList", "src/Calculator.cs");
        pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04j-commit-file-menu");
        Assert.False(repository.IsDiffOpen);
        AssertRowSelected(app, addCalculator);
        AssertMenu(menu, PartA.CommitFileMenu);

        // Phase 5's File history comes first now (docs/phase-5-design.md 4.4, 14.1).
        AssertHighlighted(menu, "File history");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Copy path");
        await app.WaitForAsync(() => app.Clipboard.Texts.Count == 2, "the path on the clipboard");
        Assert.Equal("src/Calculator.cs", app.Clipboard.Text);
        app.Click(await app.FileRowAsync("FileList", "src/Calculator.cs"));
        await WaitForTextDiffAsync(app, "src/Calculator.cs");
        Assert.True(repository.Diff!.IsAdded);
        Assert.Single(DiffRows(app), line => line.StartsWith("@@", StringComparison.Ordinal));
        pointer = DiffTextPoint(app, DiffRowOfLine(app, 1));
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04k-commit-diff-menu");
        AssertMenu(menu, PartA.CommitFileMenu);

        // Phase 5's File history comes first now (docs/phase-5-design.md 4.4, 14.1).
        AssertHighlighted(menu, "File history");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.CommitFileMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.True(repository.IsDiffOpen);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !repository.IsDiffOpen, "the diff to close");
        Assert.True(Graph(app).IsEffectivelyVisible);

        // Step 9: the working-changes row's menu selects the row; "Show in folder"; "Discard all changes…" cancelled.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, unstaged: 8, staged: 3);
        pointer = app.WorkingRowPoint();
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "04l-working-row-menu");
        Assert.True(repository.IsWorkingRowSelected);
        Assert.True(app.FindByAutomationId("StagePanel").IsEffectivelyVisible);
        AssertMenu(menu, PartA.WorkingRowMenu);
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertHighlighted(menu, "Stage all");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Show in folder");
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 3, "the file manager to be asked");
        Assert.Equal(workingTree, FullPath(app.Launcher.Requests[^1].FullPath));
        Assert.Equal(FileLaunchKind.Reveal, app.Launcher.Requests[^1].Kind);
        var status = await GitTextAsync(repo, "status", "--porcelain");
        app.RightClick(app.WorkingRowPoint());
        app.Menu();
        app.ClickMenuItem("Discard all changes…");
        await AssertConfirmationAsync(app, "Discard changes?", "Discard the changes to 8 files?", PartA.SnapshotLine, "Discard");
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(status, await GitTextAsync(repo, "status", "--porcelain"));
        Assert.Equal(snapshots, await GitTextAsync(repo, "for-each-ref", "refs/visualcommit"));

        // Step 10: "Stage all".
        app.RightClick(app.WorkingRowPoint());
        app.Menu();
        app.ClickMenuItem("Stage all");
        await WaitForStagePanelAsync(app, unstaged: 0, staged: 9);
        await app.WaitForIdleAsync();
        var none = (TextBlock)app.FindByAutomationId("StagePanel").GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "NoUnstaged");
        Assert.True(none.IsEffectivelyVisible);
        Assert.Equal("No unstaged changes", none.Text);
        Assert.Equal(PartA.StagedAfterStageAll, FileRows(app, "StagedFileList"));
        Assert.Empty(await GitTextAsync(repo, "diff"));

        // Step 11: with everything staged, "Stage all" and "Discard all changes…" are disabled.
        pointer = app.WorkingRowPoint();
        app.RightClick(pointer);
        app.Menu();
        var tip = await app.RestPointerOnMenuItemAsync("Stage all");
        shot = Shot(app, "04m-working-row-menu-all-staged", keepPointer: true);
        Assert.Equal("Nothing to stage", tip);
        menu = app.Menu();
        AssertMenu(menu, PartA.WorkingRowMenuAllStaged);
        AssertHighlighted(menu, "Unstage all");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PaletteOf(app).TextDisabled, menu.Item("Discard all changes…").TextColor);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
    }

    /// <summary>Check 26: ignoring untracked files by name and by extension, from the list and from the diff view (ops with files written before the start).</summary>
    [AvaloniaFact]
    public async Task Check_26_ignore()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(".gitignore", "*.tmp")
            .WriteFile("logs/run.log", "run\n")
            .WriteFile("trace.log", "trace\n")
            .WriteFile("TODO", "todo\n")
            .WriteFile("drafts/[wip].txt", "wip\n")
            .WriteFile("src/core.txt", "core\nmain work\nlocal\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var gitignore = Path.Combine(repo.Path, ".gitignore");

        // Step 1: the working-changes row's lists.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 0);
        Shot(app, "26a-untracked-files");
        Assert.Equal(PartA.IgnoreStartUnstaged, FileRows(app, "UnstagedFileList"));
        Assert.Empty(FileRows(app, "StagedFileList"));

        // Step 2: "Ignore run.log" adds /logs/run.log after the line feed the file lacked.
        var row = await app.FileRowAsync("UnstagedFileList", "logs/run.log");
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "26b-untracked-file-menu");
        AssertMenu(menu, PartA.RunLogMenu);
        // Every item but phase 5's File history and Blame, which an untracked file cannot use (4.4, 14.1).
        Assert.All(menu.Items.Where(item => item.Label is not ("File history" or "Blame")), item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertHighlighted(menu, "Stage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        var mark = app.LogMark();
        app.ClickMenuItem("Ignore run.log");
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded("ops", "Ignore run.log", OperationOutcome.Succeeded, "Added /logs/run.log to .gitignore"), mark: mark);
        await WaitForStagePanelAsync(app, unstaged: 5, staged: 0);
        await app.WaitForIdleAsync();
        Shot(app, "26c-ignored-by-name");
        Assert.Equal("*.tmp\n/logs/run.log\n", File.ReadAllText(gitignore));
        Assert.DoesNotContain(FileRows(app, "UnstagedFileList"), line => line.Contains("run.log", StringComparison.Ordinal));
        AssertStatus(app, "Added /logs/run.log to .gitignore");
        Assert.Equal([" M src/core.txt", "?? .gitignore", "?? TODO", "?? drafts/[wip].txt", "?? trace.log"], await GitLinesAsync(repo, "status", "--porcelain", "--untracked-files=all"));
        AssertGraph(app, PartA.OpsGraph);

        // Step 3: the diff of trace.log; "Ignore all *.log files" from its menu closes it.
        app.Click(await app.FileRowAsync("UnstagedFileList", "trace.log"));
        await WaitForTextDiffAsync(app, "trace.log");
        Assert.True(repository.Diff!.IsUnstaged && repository.Diff.IsAdded);
        Assert.Equal("Unstaged", ((TextBlock)app.FindByAutomationId("DiffSideLabel")).Text);
        Assert.Equal(["@@ -0,0 +1 @@", "A -/1 trace"], DiffRows(app));
        pointer = DiffTextPoint(app, DiffRowOfLine(app, 1));
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "26d-diff-menu-untracked");
        AssertMenu(menu, PartA.TraceLogDiffMenu);
        AssertHighlighted(menu, "Stage hunk");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.ClickMenuItem("Ignore all *.log files");
        await app.WaitForAsync(() => repository.OperationStatus.Text == "Added *.log to .gitignore", "the status bar's \"Added *.log to .gitignore\"");
        await WaitForStagePanelAsync(app, unstaged: 4, staged: 0);
        await app.WaitForAsync(() => !repository.IsDiffOpen, "the diff to close");
        await app.WaitForIdleAsync();
        Shot(app, "26e-ignored-by-extension");
        Assert.Equal("*.tmp\n/logs/run.log\n*.log\n", File.ReadAllText(gitignore));
        Assert.True(Graph(app).IsEffectivelyVisible);
        Assert.Equal(["A .gitignore", "A TODO", "A [wip].txt drafts", "M core.txt src"], FileRows(app, "UnstagedFileList"));
        AssertStatus(app, "Added *.log to .gitignore");

        // Step 4: "Ignore [wip].txt" escapes the bracket.
        app.RightClick(app.BoundsOf(await app.FileRowAsync("UnstagedFileList", "drafts/[wip].txt")).Center);
        app.Menu();
        app.ClickMenuItem("Ignore [wip].txt");
        await app.WaitForAsync(() => repository.OperationStatus.Text == "Added /drafts/\\[wip].txt to .gitignore", "the status bar's \"Added /drafts/\\[wip].txt to .gitignore\"");
        await WaitForStagePanelAsync(app, unstaged: 3, staged: 0);
        await app.WaitForIdleAsync();
        Assert.Equal("*.tmp\n/logs/run.log\n*.log\n/drafts/\\[wip].txt\n", File.ReadAllText(gitignore));
        Assert.StartsWith(".gitignore:4:", await CheckIgnoreAsync(repo, "drafts/[wip].txt"), StringComparison.Ordinal);
        Assert.Equal(["A .gitignore", "A TODO", "M core.txt src"], FileRows(app, "UnstagedFileList"));
        AssertStatus(app, "Added /drafts/\\[wip].txt to .gitignore");

        // Step 5: a name without an extension has no "Ignore all" item.
        row = await app.FileRowAsync("UnstagedFileList", "TODO");
        pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "26f-no-extension-menu");
        AssertMenu(menu, PartA.TodoMenu);
        AssertHighlighted(menu, "Stage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 6: a tracked file's Ignore items are disabled.
        row = await app.FileRowAsync("UnstagedFileList", "src/core.txt");
        pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        app.Menu();
        var tip = await app.RestPointerOnMenuItemAsync("Ignore all *.txt files");
        shot = Shot(app, "26g-tracked-file-ignore-disabled", keepPointer: true);
        Assert.Equal(PartA.OnlyUntracked, tip);
        menu = app.Menu();
        AssertMenu(menu, PartA.CoreTrackedMenu);
        AssertHighlighted(menu, "Stage");
        AssertMenuLook(app, menu, shot);
        AssertMenuPlaced(app, menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        Assert.Equal("*.tmp\n/logs/run.log\n*.log\n/drafts/\\[wip].txt\n", File.ReadAllText(gitignore));
        Assert.Equal([" M src/core.txt", "?? .gitignore", "?? TODO"], await GitLinesAsync(repo, "status", "--porcelain", "--untracked-files=all"));
    }
}
