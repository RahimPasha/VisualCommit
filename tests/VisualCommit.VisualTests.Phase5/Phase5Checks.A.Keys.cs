// Check A4 of part A (docs/test-reports/phase-5.md): the window's keys (design 1.3). Two test
// methods, each one app start: the keys on "history" (Search's key, a text box's own undo, the
// toolbar's Undo and every Redo gesture), and in the conflict resolver, which keeps its own undo and
// redo and where Search's key does nothing.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksA
{
    /// <summary>
    /// Check A4, method 1: Ctrl+F opens the search bar and Esc closes it; Ctrl+Z in the commit
    /// summary undoes the typing only, also when the box has nothing left to undo; Ctrl+Z with the
    /// focus in the graph undoes the commit, and each of the platform's redo gestures redoes it.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A4_keys()
    {
        using var repo = await Scenarios.HistoryAsync();
        repo.WriteFile(PartA.ScratchPath, PartA.ScratchText);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var meta = CommandIsMeta(app);

        // At the start: H0 with the working-changes row (src/Reader.cs changed, scratch.txt new), nothing selected.
        AssertGraph(app, PartA.H0, PartA.HistoryLanes, PartA.Working, showsDate: true);
        Assert.Equal([" M src/Reader.cs", "?? scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        AssertNothingSelected(app);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
        Assert.Equal(PartA.SearchToolTip(meta), ToolTip.GetTip(ToolbarButton(app, "SearchButton")) as string);

        // Step 1: Ctrl+F opens the search bar above the column headers, its box focused; Esc closes it.
        app.PressSearch();
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search bar with the keyboard focus in its box");
        var shot = Shot(app, PartA.A4SearchByKey);
        AssertSearchBarOpened(app, shot);
        app.PressKey(Key.Escape);
        app.Settle();
        Assert.Null(app.SearchBar());
        AssertHistoryRowsInPlace(app, workingRow: true);
        AssertFocusInGraph(app);
        AssertNothingSelected(app);

        // Step 2: src/Reader.cs alone staged with its row's Stage button, then committed; scratch.txt stays.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (2)", "Staged files (0)");
        await ClickFileRowButtonAsync(app, "UnstagedFileList", PartA.ReaderPath, "RowStageButton");
        await app.WaitForIdleAsync();
        await WaitForStagePanelAsync(app, "Unstaged files (1)", "Staged files (1)");
        app.TypeInto("CommitSummary", PartA.CommitSummary);
        app.Click(app.FindByAutomationId("CommitButton"));
        await app.WaitForIdleAsync();
        await WaitForStagePanelAsync(app, "Unstaged files (1)", "Staged files (0)");
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]", "?? scratch.txt"], await GitLinesAsync(repo, "status", "-sb"));
        AssertGraph(app, PartA.HA1, PartA.HistoryLanes, PartA.Working, showsDate: true);
        AssertStagePanel(app, ["A scratch.txt"], [], string.Empty);
        Assert.Equal(UndoRedoButtons.CanUndo(PartA.CommitText), app.UndoRedoState());
        var callsAfterCommit = app.GitCalls().Count;
        var operationsAfterCommit = app.Session.Operations.Snapshot().Count;

        // Step 3: the summary's own undo takes back the typing, also when there is nothing left; the commit stays.
        app.Click(app.WorkingRowPoint());
        app.TypeInto("CommitSummary", PartA.TypedSummary);
        Assert.Equal(PartA.TypedSummary, BoxText(app, "CommitSummary"));
        var presses = 0;
        while (BoxText(app, "CommitSummary").Length > 0 && presses < 8)
        {
            app.Undo();
            app.Settle();
            presses++;
            await AssertCommitNotUndoneAsync(app, repo, callsAfterCommit);
        }

        Assert.Equal(string.Empty, BoxText(app, "CommitSummary"));
        Assert.Equal(1, presses);
        app.Undo();
        app.Settle();
        Assert.Equal(string.Empty, BoxText(app, "CommitSummary"));
        await AssertCommitNotUndoneAsync(app, repo, callsAfterCommit);
        Assert.Equal(operationsAfterCommit, app.Session.Operations.Snapshot().Count);
        app.Click(app.WorkingRowPoint());
        app.Settle();
        shot = Shot(app, PartA.A4SummaryTypingUndone);
        Assert.Equal(PartA.SummaryPlaceholder, ((TextBox)app.FindByAutomationId("CommitSummary")).PlaceholderText);
        AssertWorkingRowSelected(app);
        AssertFocusInGraph(app);

        // Step 4: Ctrl+Z with the focus in the graph undoes the commit.
        var mark = app.GitCalls().Count;
        var operations = app.Session.Operations.Snapshot().Count;
        using (var texts = app.RecordStatusTexts())
        {
            app.PressGitUndo();
            await app.WaitForIdleAsync();
            Assert.Contains(PartA.UndoingCommit, texts.Texts);
        }

        shot = Shot(app, PartA.A4UndoneByKey);
        await AssertNoDialogAsync(app);
        Assert.Equal([PartA.UndoCommit], app.Session.Operations.Snapshot().Skip(operations).Select(record => record.Title));
        AssertStatus(app, PartA.UndidCommit);
        AssertStepCall(app, mark, PartA.UndoUpdateRef, PartA.UndoInput);
        await AssertUndoneAsync(repo);
        AssertGraph(app, PartA.H0, PartA.HistoryLanes, PartA.Working, showsDate: true, picture: shot);
        AssertWorkingRowSelected(app);
        AssertStagePanel(app, ["A scratch.txt"], ["M Reader.cs src"], PartA.CommitSummary);
        Assert.Equal(PartA.CommitOneFile, ((Button)app.FindByAutomationId("CommitButton")).Content);
        Assert.Equal(UndoRedoButtons.CanRedo(PartA.CommitText), app.UndoRedoState());
        await AssertMainAheadAsync(app, 0);

        // Step 5: Ctrl+Y redoes it, and takes the message back out of the summary.
        mark = app.GitCalls().Count;
        operations = app.Session.Operations.Snapshot().Count;
        app.PressGitRedo(0);
        await app.WaitForIdleAsync();
        shot = Shot(app, PartA.A4RedoneByKey);
        await AssertNoDialogAsync(app);
        Assert.Equal([PartA.RedoCommit], app.Session.Operations.Snapshot().Skip(operations).Select(record => record.Title));
        AssertStatus(app, PartA.RedidCommit);
        AssertStepCall(app, mark, PartA.RedoUpdateRef, PartA.RedoInput);
        await AssertRedoneAsync(repo);
        Assert.Equal([PartA.RedoReflog, PartA.UndoReflog, PartA.CommitReflog], await ReflogAsync(repo, "HEAD", 3));
        AssertGraph(app, PartA.HA1, PartA.HistoryLanes, PartA.Working, showsDate: true, picture: shot);
        AssertStagePanel(app, ["A scratch.txt"], [], string.Empty);
        Assert.Equal(UndoRedoButtons.CanUndo(PartA.CommitText), app.UndoRedoState());

        // Step 6: every other redo gesture of the platform redoes too, each after its own undo (AD10).
        var gestures = app.RedoGestures.Count;
        Assert.True(gestures >= 2, $"The platform names {gestures} redo gestures; the headless platform's list is Ctrl+Y and Ctrl+Shift+Z.");
        for (var gesture = 1; gesture < gestures; gesture++)
        {
            mark = app.GitCalls().Count;
            app.PressGitUndo();
            await app.WaitForIdleAsync();
            AssertStepCall(app, mark, PartA.UndoUpdateRef, PartA.UndoInput);
            await AssertUndoneAsync(repo);
            Assert.Equal(UndoRedoButtons.CanRedo(PartA.CommitText), app.UndoRedoState());
            mark = app.GitCalls().Count;
            app.PressGitRedo(gesture);
            await app.WaitForIdleAsync();
            AssertStepCall(app, mark, PartA.RedoUpdateRef, PartA.RedoInput);
            await AssertRedoneAsync(repo);
            Assert.Equal(UndoRedoButtons.CanUndo(PartA.CommitText), app.UndoRedoState());
        }

        List<string> steps = [.. Enumerable.Range(0, gestures).SelectMany(_ => new[] { PartA.RedoReflog, PartA.UndoReflog }), PartA.CommitReflog];
        var reflog = await ReflogAsync(repo, "HEAD", steps.Count);
        Assert.Equal(steps, reflog);
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// Check A4, method 2: with the resolver open, Search is disabled and Ctrl+F does nothing;
    /// Ctrl+Z and Ctrl+Y in the result are the resolver's own undo and redo (phase 4's 11.5), and
    /// no operation runs.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A4_keys_in_the_resolver()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var meta = CommandIsMeta(app);
        var status = await GitLinesAsync(repo, "status", "--porcelain");

        // Step 7: GC-M and its banner; the resolver opened on notes.txt; Ctrl+F opens nothing.
        AssertGraph(app, PartA.ConflictsGraph, PartA.ConflictsLanes, PartA.WorkingOneConflict, showsDate: false);
        Assert.Equal(PartA.MergeBanner, ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.Equal(new UndoRedoButtons(false, PartA.FinishMerge, false, PartA.FinishMerge), app.UndoRedoState());
        var search = ToolbarButton(app, "SearchButton");
        Assert.True(search.IsEffectivelyEnabled, "Search is disabled before the resolver opens.");
        Assert.Equal(PartA.SearchToolTip(meta), ToolTip.GetTip(search) as string);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        app.Click(await app.FileRowAsync("UnstagedFileList", PartA.NotesPath));
        await app.WaitForResolverAsync(PartA.NotesPath);
        AssertToolbar(app, [new PartA.ButtonState("SearchButton", false, PartA.CloseResolverFirst)]);
        Assert.Equal(new UndoRedoButtons(false, PartA.FinishMerge, false, PartA.FinishMerge), app.UndoRedoState());
        var before = app.Resolver();
        Assert.Equal(Scenarios.ConflictsFiles.NotesMergeConflicted, before.Result);
        app.PressSearch();
        app.Settle();
        Assert.Null(app.SearchBar());
        Assert.Equal(before.Result, app.Resolver().Result);
        Assert.Equal(before.ResultCount, app.Resolver().ResultCount);
        Assert.NotNull(app.ShownResolver);

        // Step 8: Use ours on block 1 puts the focus in the result; the undo there is the resolver's.
        var operations = app.Session.Operations.Snapshot().Count;
        app.ClickBarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        await app.WaitForResultParsedAsync();
        var chosen = app.Resolver();
        Assert.Equal(PartA.NotesR1, chosen.Result);
        Assert.Equal(PartA.OneConflictLeft, chosen.ResultCount);
        Assert.True(chosen.ResultHasFocus, "Use ours did not put the keyboard focus in the result.");
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(2, 1), chosen.Caret);
        app.Undo();
        await app.WaitForResultParsedAsync();
        Shot(app, PartA.A4ResolverUndo);
        var undone = app.Resolver();
        Assert.Equal(Scenarios.ConflictsFiles.NotesMergeConflicted, undone.Result);
        Assert.Equal(20, undone.Result!.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(PartA.TwoConflictsLeft, undone.ResultCount);
        Assert.Null(app.BarText(0, ConflictPaneSide.Ours));
        Assert.NotNull(app.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        app.Redo();
        await app.WaitForResultParsedAsync();
        Assert.Equal(PartA.NotesR1, app.Resolver().Result);
        Assert.Equal(PartA.OneConflictLeft, app.Resolver().ResultCount);

        // Nothing ran and nothing was written: the resolver writes nothing before Mark resolved.
        Assert.Equal(operations, app.Session.Operations.Snapshot().Count);
        Assert.DoesNotContain(app.Session.Operations.Snapshot(), record => record.Title.StartsWith("Undo ", StringComparison.Ordinal) || record.Title.StartsWith("Redo ", StringComparison.Ordinal));
        Assert.Equal(status, await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(["UU notes.txt"], status);
        Assert.Equal(PartA.NotesBlobAtStop, (await GitTextAsync(repo, "hash-object", PartA.NotesPath))[..7]);
        Assert.Equal(PartA.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
    }

    /// <summary>
    /// The search bar as Ctrl+F opens it (A4 step 1, 3.1): under the toolbar, above the column
    /// headers, x 260 to 700 and y 88 to 124, on the chrome background with a 1-px border line along
    /// its bottom; the mode <c>Commits</c>, the box empty with its placeholder and the keyboard
    /// focus, no count, previous and next disabled, close; the column headers and the rows 36 lower;
    /// no row marked or dimmed; Search still enabled.
    /// </summary>
    private static void AssertSearchBarOpened(ShellDriver app, Screenshot shot)
    {
        var bar = app.SearchBar() ?? throw new Xunit.Sdk.XunitException("No search bar is shown.");
        AssertNear(new Rect(PartA.GraphArea.Left, PartA.GraphArea.Top, PartA.GraphArea.Width, PartA.SearchBarHeight), bar.Bounds, "The search bar");
        Assert.Equal(PartA.SearchMode, bar.Mode);
        Assert.Equal(string.Empty, bar.Text);
        Assert.Equal(PartA.SearchPlaceholder, bar.Placeholder);
        Assert.True(bar.BoxHasFocus, "The search box does not have the keyboard focus.");
        Assert.Equal(string.Empty, bar.Count);
        Assert.False(bar.PreviousEnabled, "Previous is enabled.");
        Assert.False(bar.NextEnabled, "Next is enabled.");
        Assert.Equal(PartA.PreviousMatch, bar.PreviousToolTip);
        Assert.Equal(PartA.NextMatch, bar.NextToolTip);
        Assert.Equal(PartA.CloseSearch, bar.CloseToolTip);
        var palette = PaletteOf(app);
        AssertColour(palette.Chrome, shot, new Point(bar.Bounds.Right - 3, bar.Bounds.Top + 4), "search bar's background");
        AssertColour(palette.Border, shot, new Point(bar.Bounds.Right - 3, bar.Bounds.Bottom - 1), "search bar's bottom line");

        // The column headers and the rows moved down by the bar's 36.
        var graph = app.BoundsOf(Graph(app));
        Assert.Equal(WindowLayout.RowsTop + PartA.SearchBarHeight, graph.Top, 0.5);
        Assert.Equal(WindowLayout.RowCentreY(0) + PartA.SearchBarHeight, app.WorkingRowPoint().Y, PartA.Tolerance);
        Assert.Equal(WindowLayout.RowCentreY(1) + PartA.SearchBarHeight, app.GraphRowPoint(0).Y, PartA.Tolerance);
        var marks = app.SearchMarks();
        Assert.True(marks.Marked.Count == 0 && marks.Dimmed.Count == 0, $"Rows are marked ({string.Join(", ", marks.Marked)}) or dimmed ({string.Join(", ", marks.Dimmed)}).");
        Assert.True(ToolbarButton(app, "SearchButton").IsEffectivelyEnabled, "Search is disabled while the bar is open.");
    }

    /// <summary>
    /// A step's transaction (7.3, 7.5): among the calls since <paramref name="mark"/> exactly one
    /// <c>git update-ref</c>, the step's own <paramref name="command"/>, exit 0, with
    /// <paramref name="input"/> as its one line.
    /// </summary>
    private static void AssertStepCall(ShellDriver app, int mark, string command, string input)
    {
        var updates = app.GitCalls().Skip(mark).Where(call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal)).ToList();
        var call = Assert.Single(updates);
        Assert.Equal(command, call.CommandText);
        Assert.True(call.Succeeded, $"\"{call.CommandText}\" exited {call.ExitCode}: {call.StandardError}");
        Assert.Equal(input, call.StandardInput?.TrimEnd('\n'));
    }

    /// <summary>
    /// After a press that the summary took (A4 step 3): the commit is not undone. No record of an
    /// undo, no <c>git update-ref</c> since the commit, main still at the commit, and Undo still
    /// offers it.
    /// </summary>
    private static async Task AssertCommitNotUndoneAsync(ShellDriver app, TempRepo repo, int callsAfterCommit)
    {
        Assert.DoesNotContain(app.Session.Operations.Snapshot(), record => record.Title == PartA.UndoCommit);
        Assert.DoesNotContain(app.GitCalls().Skip(callsAfterCommit), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(UndoRedoButtons.CanUndo(PartA.CommitText), app.UndoRedoState());
    }

    /// <summary>Git as H-A0u: main back at Read whole lines, src/Reader.cs staged, scratch.txt untracked, the commit kept, the undo in HEAD's and main's reflogs.</summary>
    private static async Task AssertUndoneAsync(TempRepo repo)
    {
        Assert.Equal(PartA.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main", "M  src/Reader.cs", "?? scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain=v1", "-b"));
        Assert.True((await repo.GitAllowingFailureAsync("cat-file", "-e", PartA.DescribeTheLinesSha)).Succeeded, "The undone commit no longer exists.");
        Assert.Equal([PartA.UndoReflog], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal([PartA.UndoReflog], await ReflogAsync(repo, "refs/heads/main", 1));
    }

    /// <summary>Git as after A4 step 2 again: main at Describe the lines, scratch.txt untracked, the redo newest in HEAD's and main's reflogs.</summary>
    private static async Task AssertRedoneAsync(TempRepo repo)
    {
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]", "?? scratch.txt"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal([PartA.RedoReflog], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal([PartA.RedoReflog], await ReflogAsync(repo, "refs/heads/main", 1));
    }

    /// <summary>
    /// The stage panel: its two lists' rows ("letter name folder"), "No staged changes" while the
    /// staged list is empty, the summary as given and the description empty.
    /// </summary>
    private static void AssertStagePanel(ShellDriver app, string[] unstaged, string[] staged, string summary)
    {
        Assert.Equal($"Unstaged files ({unstaged.Length})", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal($"Staged files ({staged.Length})", app.Find<TextBlock>("StagedTitle").Text);
        Assert.Equal(unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(staged, FileRows(app, "StagedFileList"));
        var noStaged = app.Find<TextBlock>("NoStaged");
        Assert.Equal(staged.Length == 0, noStaged.IsEffectivelyVisible);
        Assert.Equal("No staged changes", noStaged.Text);
        Assert.Equal(summary, BoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, BoxText(app, "CommitDescription"));
    }

    /// <summary>
    /// Moves the pointer onto a file's row in a stage list, so that its buttons show, and clicks
    /// one of them (<c>RowStageButton</c>, as A4 step 2 stages src/Reader.cs alone, AD8).
    /// </summary>
    private static async Task ClickFileRowButtonAsync(ShellDriver app, string list, string path, string buttonId)
    {
        var row = await app.FileRowAsync(list, path);
        app.MoveMouse(row);
        Button? button = null;
        await app.WaitForAsync(
            () => (button = row.GetVisualParent()!.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(candidate => candidate.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(candidate) == buttonId)) is not null,
            $"the {buttonId} of {path}");
        app.Click(button!);
    }
}
