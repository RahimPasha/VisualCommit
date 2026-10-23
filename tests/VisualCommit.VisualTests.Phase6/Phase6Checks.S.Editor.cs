// Check S17 (docs/test-reports/phase-6.md, part S): Open file's extensions and Open in editor
// (design 7.3, 7.5, 7.7, 12.1 to 12.9, D226): run.py opens at once with 12.9's PATHEXT; with .py
// added in General, Open file asks first (SD23); Visual Studio Code chosen from the fixed
// catalogue in the Pick dialog stacked over Tools; Open in editor right after Open file in a
// commit file's, an unstaged file's, a staged file's and the diff's menus (SD20); its click records
// 12.9's program and the file's absolute path and asks nothing; the palette's "Choose an editor…"
// gone once an editor is chosen.
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S17, one app start (steps 1 to 8), on "settings" with notes.txt staged and draft.txt untracked.</summary>
    [AvaloniaFact]
    public async Task Check_S17_open_file_extensions_and_editor()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: notes.txt with a third line staged, draft.txt untracked, before the start.
        repo.WriteFile(PartS.NotesFile, PartS.NotesThreeLines);
        await repo.GitAsync("add", "--", PartS.NotesFile);
        repo.WriteFile(PartS.DraftFile, PartS.DraftText);
        using var app = await StartAsync(data, repo);
        var start = app.CallMark();
        var folder = await WorkingTreeAsync(repo);
        AssertGraph(app, PartS.St0, PartS.StLanes, PartS.St0wWorking);
        Assert.Equal(PartS.S17Status, await GitLinesAsync(repo, "status", "--porcelain=v1"));
        var runPy = Path.Combine(folder, PartS.RunPyFile);

        // Step 2: Initial commit (commit row 2, display row 3), its run.py's menu, Open file: no question.
        layout.Step = "step 2";
        var point = app.GraphRowPoint(2);
        layout.Near(PartS.StRowPoint(3).X, point.X, "Initial commit's click point's x");
        layout.Near(PartS.StRowPoint(3).Y, point.Y, "Initial commit's click point's y");
        app.Click(point);
        await AssertDetailsAsync(app, "Initial commit", PartS.ChangedFilesThree, ["A README.md", "A notes.txt", "A run.py"]);
        var menu = await RightClickFileAsync(app, "FileList", PartS.RunPyFile);
        AssertMenu(menu, PartS.RunPyFile, PartS.FileHistory, PartS.Blame, "—", PartS.OpenFile, PartS.ShowInFolder, PartS.CopyPath);
        layout.Near(171, menu.Bounds.Height, "the commit file's menu's height");
        app.ClickMenuItem(PartS.OpenFile);
        await app.WaitForAsync(() => app.Launcher.Opened.Count == 1, "Open file to open run.py");
        await AssertNoDialogAsync(app);
        Assert.True(WorkspacePaths.Same(Assert.Single(app.Launcher.Opened), runPy), $"Open file opened {app.Launcher.Opened[0]}, not {runPy}.");
        Assert.True(Path.IsPathFullyQualified(app.Launcher.Opened[0]), "Open file's path is not absolute.");

        // Step 3: .py added in General; Save writes settings.json and no git key; the reads at the opening and the read back.
        layout.Step = "step 3";
        var sequence = app.CallMark();
        var screen = await app.OpenSettingsAsync();
        var opened = app.CallMark();
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: null, until: opened);
        app.TypeInto("RunnableExtraBox", PartS.PyExtension);
        AssertGeneral(app, layout, extra: PartS.PyExtension);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        AssertSettingsJson(data, ("extraRunnableExtensions", "\".py\""));
        await AssertScreenReadsAsync(app, opened, data, repo, varExit: null);
        AssertNoWrite(app, start);

        // Step 4: Open file on run.py asks first now (12.8's union); Cancel opens nothing.
        layout.Step = "step 4";
        menu = await RightClickFileAsync(app, "FileList", PartS.RunPyFile);
        app.ClickMenuItem(PartS.OpenFile);
        await AssertConfirmationAsync(app, PartS.OpenFileQuestion(PartS.RunPyFile), PartS.OpenFileText, string.Empty, PartS.OpenFileConfirm, destructive: false);
        ShotAtRest(app, "s17a-open-file-asks");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Single(app.Launcher.Requests);

        // Step 5: the palette lists "Choose an editor…"; the editor's Pick dialog over Tools; Visual Studio Code chosen and saved.
        layout.Step = "step 5";
        var palette = await PaletteRowsAsync(app, "editor");
        var choose = palette.SingleOrDefault(row => row.Label == PartS.PaletteChooseEditor)
            ?? throw new Xunit.Sdk.XunitException($"The palette lists no \"{PartS.PaletteChooseEditor}\" for \"editor\": {string.Join(" | ", palette.Select(row => row.Label))}.");
        Assert.Null(choose.KeyText);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the command palette to close");
        screen = await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.Tools);
        app.Click(app.FindByAutomationId("EditorChooseButton"));
        await app.WaitForDialogAsync<PickDialogViewModel>();
        ShotAtRest(app, "s17b-editor-pick");
        await AssertPickAsync(app, layout, PartS.ChooseEditorTitle, PartS.EditorOptions, PartS.EditorNone);
        await app.PickAsync(PartS.VisualStudioCode);
        ShotAtRest(app, "s17c-tools-editor-chosen");
        AssertToolsKnown(app, layout);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        AssertSettingsJson(data, ("extraRunnableExtensions", "\".py\""), ("editor", EditorJson()));

        // Step 6: no "Choose an editor…" now, and no "Open in editor" (5.5).
        layout.Step = "step 6";
        palette = await PaletteRowsAsync(app, "editor");
        Assert.DoesNotContain(palette, row => row.Label == PartS.PaletteChooseEditor);
        Assert.DoesNotContain(palette, row => row.Label == PartS.OpenInEditor);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the command palette to close");

        // Step 7: run.py's menu with Open in editor after Open file; its click records the editor's start and asks nothing.
        layout.Step = "step 7";
        menu = await RightClickFileAsync(app, "FileList", PartS.RunPyFile);
        ShotAtRest(app, "s17d-file-menu-with-editor");
        AssertMenu(menu, PartS.RunPyFile, PartS.FileHistory, PartS.Blame, "—", PartS.OpenFile, PartS.OpenInEditor, PartS.ShowInFolder, PartS.CopyPath);
        layout.Near(197, menu.Bounds.Height, "the commit file's menu's height with Open in editor");
        Assert.True(menu.Item(PartS.OpenInEditor).IsEnabled && menu.Item(PartS.OpenInEditor).ToolTip is null, "Open in editor is disabled or has a reason.");
        var operations = OperationCount(app);
        var undo = app.UndoRedoState().UndoToolTip;
        var mark = app.LogMark();
        app.ClickMenuItem(PartS.OpenInEditor);
        await app.WaitForAsync(() => app.Launcher.Edits.Count == 1, "Open in editor to start the editor");
        await AssertNoDialogAsync(app);
        var edit = Assert.Single(app.Launcher.Edits);
        Assert.Equal(PartS.VsCodeProgram, edit.FileName);
        Assert.True(edit.Arguments.Count == 1 && WorkspacePaths.Same(edit.Arguments[0], runPy) && Path.IsPathFullyQualified(edit.Arguments[0]), $"The editor's arguments are [{string.Join(", ", edit.Arguments)}], not [{runPy}].");
        Assert.Equal(OperatingSystem.IsWindows() ? PartS.VsCodeFolderOnWindows : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), edit.WorkingDirectory);
        Assert.Contains(PartS.ElectronRunAsNode, edit.RemovedVariables);
        Assert.Equal(2, app.Launcher.Requests.Count);
        Assert.Equal(operations, OperationCount(app));
        Assert.Equal(undo, app.UndoRedoState().UndoToolTip);
        Assert.Single(app.LogLinesSince(mark), line => line.Contains(PartS.OpenInEditorLog + PartS.VsCodeProgram, StringComparison.Ordinal));

        // Step 8: the unstaged file's, the staged file's and the staged diff's menus end with the four file items.
        layout.Step = "step 8";
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository?.IsWorkingRowSelected == true, "the working-changes row to be selected");
        AssertEndsWithFileItems(await RightClickFileAsync(app, "UnstagedFileList", PartS.DraftFile));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        AssertEndsWithFileItems(await RightClickFileAsync(app, "StagedFileList", PartS.NotesFile));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        app.Click(await app.FileRowAsync("StagedFileList", PartS.NotesFile));
        await app.WaitForAsync(() => app.Window.GetVisualDescendants().OfType<DiffTextView>().Any(view => view.Name == "DiffText" && view.IsEffectivelyVisible) && app.DiffRowOfLine(3) >= 0, "the staged diff of notes.txt");
        app.RightClick(app.DiffTextPoint(app.DiffRowOfLine(3)));
        AssertEndsWithFileItems(app.Menu());
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Equal(2, app.Launcher.Requests.Count);
        AssertNoWrite(app, start);
        Assert.DoesNotContain(app.CallsSince(start), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal) || call.CommandText.StartsWith("git stash push", StringComparison.Ordinal));
        Assert.Equal(PartS.S17Status, await GitLinesAsync(repo, "status", "--porcelain=v1"));
        layout.AssertNone();
    }

    /// <summary>Tools with Visual Studio Code chosen (7.5, S17 step 5): its name (no <c>(git's editor)</c>), its program in the secondary colour with the whole path as its tooltip, <c>Choose…</c>, the note; no Program or Arguments row, no error.</summary>
    private static void AssertToolsKnown(ShellDriver app, Layout layout)
    {
        var snapshot = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Tools, snapshot.Section);
        AssertText(snapshot, "EditorValue", PartS.VisualStudioCode);
        AssertText(snapshot, "EditorProgram", PartS.VsCodeProgram);
        Assert.Equal(PartS.VsCodeProgram, Field(snapshot, "EditorProgram").ToolTip);
        var program = Named<VisualCommit.App.Controls.TrimmedTextBlock>(app, "EditorProgram");
        Assert.Equal(PaletteOf(app).TextSecondary, (program.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(TextTrimming.LeadingCharacterEllipsis, program.TextTrimming);
        AssertText(snapshot, "EditorChooseButton", PartS.Choose);
        AssertText(snapshot, "EditorNote", PartS.EditorNote);
        AssertHidden(snapshot, "EditorProgramBox");
        AssertHidden(snapshot, "EditorArgumentsBox");
        AssertHidden(snapshot, "EditorError");
        AssertNoClippedText(app, layout);
    }

    /// <summary>settings.json's <c>editor</c> as S17 step 5 saves it: <c>vscode</c>, the fixed catalogue's program, no arguments (12.6).</summary>
    private static string EditorJson() =>
        JsonSerializer.Serialize(new Dictionary<string, string?> { ["id"] = PartS.EditorStoredId, ["program"] = PartS.VsCodeProgram, ["arguments"] = null });

    /// <summary>A file menu's last group (SD20): exactly Open file, Open in editor (enabled, no reason), Show in folder, Copy path, after a separator.</summary>
    private static void AssertEndsWithFileItems(MenuSnapshot menu)
    {
        var lines = menu.Lines();
        string[] group = ["—", PartS.OpenFile, PartS.OpenInEditor, PartS.ShowInFolder, PartS.CopyPath];
        Assert.True(lines.Count > group.Length && lines.TakeLast(group.Length).SequenceEqual(group), $"The menu \"{menu.Header}\" does not end with the file items: {string.Join(" | ", lines)}.");
        Assert.True(menu.Item(PartS.OpenInEditor).IsEnabled, $"Open in editor is disabled in \"{menu.Header}\".");
    }

    /// <summary>Right-clicks a file's row in a file list and returns the menu it opens.</summary>
    private static async Task<MenuSnapshot> RightClickFileAsync(ShellDriver app, string list, string path)
    {
        app.RightClick(await app.FileRowAsync(list, path));
        return app.Menu();
    }

    /// <summary>The details panel showing <paramref name="subject"/> with its changed files' title and rows (status letter and path).</summary>
    private static async Task AssertDetailsAsync(ShellDriver app, string subject, string title, IReadOnlyList<string> files)
    {
        await app.WaitForAsync(
            () => app.Window.GetVisualDescendants().OfType<SelectableTextBlock>().Any(block => block.Name == "DetailsSubject" && block.IsEffectivelyVisible && block.Text == subject),
            $"the details of \"{subject}\"");
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        var rows = app.Session.Shell.ActiveTab.Repository!.Details.Files.Where(row => row.IsFile).Select(row => $"{row.StatusLetter} {row.Key}").ToList();
        Assert.Equal(files, rows);
    }

    /// <summary>Ctrl+Shift+P, <paramref name="query"/> typed: the palette's rows (the palette left open).</summary>
    private static async Task<IReadOnlyList<PaletteRow>> PaletteRowsAsync(ShellDriver app, string query)
    {
        app.PressKey(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.IsPaletteOpen, "the command palette to open");
        app.TypeInPalette(query);
        return app.PaletteSnapshot().Rows;
    }
}
