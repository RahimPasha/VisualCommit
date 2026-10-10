using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 2's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase2Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: 2);

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The real-window pass of phase 2: the checks of docs/test-reports/phase-2.md marked for it,
/// repeated in the real app on the Windows desktop with the real mouse and keyboard. Screenshots
/// go to artifacts/visual/phase-2/real-window/ and are compared with the scripted walk-through's
/// of the same step (D33, D58), which must have run first (<c>dotnet test</c> at the repo root).
/// </summary>
public partial class Phase2RealWindowPass(Phase2Run fixture) : IClassFixture<Phase2Run>
{
    /// <summary>The top of the graph's rows in the window: tabs 36, toolbar 52, column headers 28.</summary>
    private const float RowsTop = 36 + 52 + 28;

    private const float RowHeight = 26;

    /// <summary>Where the mouse rests between steps: on the status bar, out of every hover and tooltip.</summary>
    private const float RestX = 130;

    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    private RealWindowRun Run => fixture.Run;

    [Fact]
    public async Task Check_1_working_changes_row_and_stage_panel()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        Run.NoteDisplay(app);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        app.MoveMouseTo(RestX, 687);
        using (var row = app.CaptureClient())
        {
            Run.Save(row, "01a-working-row");
            Run.AssertMatchesScripted(row, "01a-working-row", "01a-working-row");
        }

        ClickWorkingRow(app);
        WaitForStagePanel(app, unstaged: 7, staged: 3);
        app.MoveMouseTo(RestX, 687);
        using (var panel = app.CaptureClient())
        {
            Run.Save(panel, "01b-stage-panel");
            Run.AssertMatchesScripted(panel, "01b-stage-panel", "01b-stage-panel");
        }

        Assert.Equal(0, app.Close());
    }

    [Fact]
    public async Task Check_14_commit_typed_with_the_real_keyboard()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        ClickWorkingRow(app);
        WaitForStagePanel(app, unstaged: 7, staged: 3);

        app.ClickWithMouse(app.Find("CommitSummary"));
        app.TypeText("Add usage and settings");
        app.ClickWithMouse(app.Find("CommitDescription"));
        app.TypeText("Explains how to use the calculator.");
        app.ClickWithMouse(app.Find("CommitButton"));
        WaitForLog(app, data, "changes: loaded 3 commits");
        WaitForStatus(app, data, "changes", staged: 0, unstaged: 7);
        WaitForStagePanel(app, unstaged: 7, staged: 0);
        app.MoveMouseTo(RestX, 687);
        using (var committed = app.CaptureClient())
        {
            Run.Save(committed, "14b-committed");
            Run.AssertMatchesScripted(committed, "14b-committed", "14b-committed");
        }

        Assert.Equal(0, app.Close());
        Assert.Equal("f976a24c1cb298e1c293542624fb96fe0b2d81c5", await repo.HeadAsync());
        Assert.Equal("Add usage and settings\n\nExplains how to use the calculator.", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Check_17_discard_a_file_with_the_dialog_and_restore_it()
    {
        using var repo = await Scenarios.ChangesAsync();
        var file = Path.Combine(repo.Path, "src", "Calculator.cs");
        var before = File.ReadAllBytes(file);
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        ClickWorkingRow(app);
        WaitForStagePanel(app, unstaged: 7, staged: 3);

        // Step 2: the row's Discard button, shown while the real mouse is over the row.
        ClickRowButton(app, "UnstagedFileList", "src/Calculator.cs", "RowDiscardButton");
        app.WaitFor(() => app.FindAll("ConfirmQuestion").Any(text => text.Name == "Discard the changes to Calculator.cs?"), "the confirmation dialog", LoadTimeout);
        app.MoveMouseTo(RestX, 687);
        using (var dialog = app.CaptureClient())
        {
            Run.Save(dialog, "17a-discard-dialog");
            Run.AssertMatchesScripted(dialog, "17a-discard-dialog", "17a-discard-dialog");
        }

        // Step 3: Cancel changes nothing.
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("ConfirmQuestion").Count == 0, "the dialog to close", LoadTimeout);
        Assert.Equal(before, File.ReadAllBytes(file));

        // Step 4: Discard.
        ClickRowButton(app, "UnstagedFileList", "src/Calculator.cs", "RowDiscardButton");
        app.WaitFor(() => app.FindAll("ConfirmButton").Count == 1, "the confirmation dialog", LoadTimeout);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        app.WaitFor(() => app.FindAll("RestoreBarText").Any(text => text.Name == "Discarded changes to Calculator.cs."), "the restore bar", LoadTimeout);
        WaitForStagePanel(app, unstaged: 6, staged: 3);
        app.MoveMouseTo(RestX, 687);
        using (var discarded = app.CaptureClient())
        {
            Run.Save(discarded, "17b-discarded");
            Run.AssertMatchesScripted(discarded, "17b-discarded", "17b-discarded");
        }

        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted, File.ReadAllText(file));

        // Step 5: Restore.
        app.ClickWithMouse(app.Find("RestoreDiscardButton"));
        app.WaitFor(() => app.FindAll("RestoreBarText").Count == 0, "the restore bar to go", LoadTimeout);
        WaitForStagePanel(app, unstaged: 7, staged: 3);
        app.MoveMouseTo(RestX, 687);
        using (var restored = app.CaptureClient())
        {
            Run.Save(restored, "17c-restored");
            Run.AssertMatchesScripted(restored, "17c-restored", "17c-restored");
        }

        Assert.Equal(0, app.Close());
        Assert.Equal(before, File.ReadAllBytes(file));
    }

    [Fact]
    public async Task Check_20_a_file_written_outside_the_real_app_appears()
    {
        using var repo = await Scenarios.LinearAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        WaitForLog(app, data, "linear: loaded 3 commits");
        WaitForStatus(app, data, "linear", staged: 0, unstaged: 0);

        repo.WriteFile("notes.txt", "A note\n");
        WaitForStatus(app, data, "linear", staged: 0, unstaged: 1);
        app.MoveMouseTo(RestX, 687);
        using (var appeared = app.CaptureClient())
        {
            Run.Save(appeared, "20b-row-appeared");
            Run.AssertMatchesScripted(appeared, "20b-row-appeared", "20b-row-appeared");
        }

        Assert.Equal(0, app.Close());
    }

    [Fact]
    public async Task Check_3_diff_inline_and_side_by_side()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path, 1400, 900);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        ClickWorkingRow(app, rowsLeft: 260, graphWidth: 740);
        WaitForStagePanel(app, unstaged: 7, staged: 3);

        ClickFileRow(app, "UnstagedFileList", "src/Calculator.cs");
        WaitForHunkButtons(app, "StageHunkButton", 2);
        app.MoveMouseTo(RestX, 887);
        using (var inline = app.CaptureClient())
        {
            Run.Save(inline, "03a-calculator-inline");
            Run.AssertMatchesScripted(inline, "03a-calculator-inline", "03a-calculator-inline");
        }

        app.ClickWithMouse(app.Find("SideBySideDiffButton"));
        app.WaitFor(() => app.FindAll("DiffTextRight").Count == 1, "the side-by-side halves", LoadTimeout);
        WaitForHunkButtons(app, "StageHunkButton", 2);
        app.MoveMouseTo(RestX, 887);
        using (var sideBySide = app.CaptureClient())
        {
            Run.Save(sideBySide, "03b-calculator-side-by-side");
            Run.AssertMatchesScripted(sideBySide, "03b-calculator-side-by-side", "03b-calculator-side-by-side");
        }

        Assert.Equal(0, app.Close());
    }

    [Fact]
    public async Task Check_5_image_diff()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        ClickWorkingRow(app);
        WaitForStagePanel(app, unstaged: 7, staged: 3);

        ClickFileRow(app, "UnstagedFileList", "assets/logo.png");
        app.WaitFor(() => app.FindAll("AfterCaption").Any(text => text.Name.Contains("pixels", StringComparison.Ordinal)), "the images' captions", LoadTimeout);
        app.MoveMouseTo(RestX, 687);
        using (var image = app.CaptureClient())
        {
            Run.Save(image, "05-image-diff");
            Run.AssertMatchesScripted(image, "05-image-diff", "05-image-diff");
        }

        Assert.Equal(0, app.Close());
    }

    [Fact]
    public async Task Check_10_stage_one_hunk_with_the_real_mouse()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path, 1400, 900);
        WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
        ClickWorkingRow(app, rowsLeft: 260, graphWidth: 740);
        WaitForStagePanel(app, unstaged: 7, staged: 3);
        ClickFileRow(app, "UnstagedFileList", "src/Calculator.cs");
        WaitForHunkButtons(app, "StageHunkButton", 2);

        app.ClickWithMouse(app.FindAll("StageHunkButton")[0]);
        WaitForStagePanel(app, unstaged: 7, staged: 4);
        WaitForHunkButtons(app, "StageHunkButton", 1);
        app.MoveMouseTo(RestX, 887);
        using (var staged = app.CaptureClient())
        {
            Run.Save(staged, "10a-hunk-staged");
            Run.AssertMatchesScripted(staged, "10a-hunk-staged", "10a-hunk-staged");
        }

        ClickFileRow(app, "StagedFileList", "src/Calculator.cs");
        WaitForHunkButtons(app, "UnstageHunkButton", 1);
        app.MoveMouseTo(RestX, 887);
        using (var stagedDiff = app.CaptureClient())
        {
            Run.Save(stagedDiff, "10b-staged-diff");
            Run.AssertMatchesScripted(stagedDiff, "10b-staged-diff", "10b-staged-diff");
        }

        Assert.Equal(0, app.Close());
        Assert.Contains(
            "@@ -12,7 +12,7 @@ public sealed class Calculator",
            (await repo.GitAsync("diff", "--cached", "--", "src/Calculator.cs")).StandardOutput.Split('\n'));
    }

    [Fact]
    public async Task Check_21_the_diff_mode_survives_a_real_restart()
    {
        using var repo = await Scenarios.ChangesAsync();
        var data = Run.NewDataDirectory();
        using (var app = LaunchWithTabs(data, repo.Path, 1400, 900))
        {
            WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
            ClickWorkingRow(app, rowsLeft: 260, graphWidth: 740);
            WaitForStagePanel(app, unstaged: 7, staged: 3);
            ClickFileRow(app, "UnstagedFileList", "README.md");
            WaitForHunkButtons(app, "StageHunkButton", 1);
            app.ClickWithMouse(app.Find("SideBySideDiffButton"));
            app.WaitFor(() => app.FindAll("DiffTextRight").Count == 1, "the side-by-side halves", LoadTimeout);
            Assert.Equal(0, app.Close());
        }

        using (var app = RealApp.Launch(data))
        {
            app.SetClientSize(1400, 900);
            WaitForStatus(app, data, "changes", staged: 3, unstaged: 7);
            ClickWorkingRow(app, rowsLeft: 260, graphWidth: 740);
            WaitForStagePanel(app, unstaged: 7, staged: 3);
            ClickFileRow(app, "UnstagedFileList", "README.md");
            app.WaitFor(() => app.FindAll("DiffTextRight").Count == 1, "the side-by-side halves after the restart", LoadTimeout);
            WaitForHunkButtons(app, "StageHunkButton", 1);
            app.MoveMouseTo(RestX, 887);
            using var restarted = app.CaptureClient();
            Run.Save(restarted, "21-side-by-side-after-restart");
            Run.AssertMatchesScripted(restarted, "21-side-by-side-after-restart", "21-side-by-side-after-restart");
            Assert.Equal(0, app.Close());
        }
    }

    /// <summary>Clicks a file's row in one stage list, found inside that list (a file can be in both).</summary>
    private static void ClickFileRow(RealApp app, string list, string path)
    {
        AutomationElement? row = null;
        app.WaitFor(
            () => (row = app.Find(list).FindAllDescendants(condition => condition.ByAutomationId("StageFileRow")).FirstOrDefault(candidate => candidate.Name == path)) is not null,
            $"the row of {path} in {list}",
            LoadTimeout);
        app.ClickWithMouse(row!);
    }

    /// <summary>
    /// Waits until the diff shows this many of a hunk button, then a moment more: the text control
    /// settles the heights of its header rows a layout pass after its lines.
    /// </summary>
    private static void WaitForHunkButtons(RealApp app, string automationId, int count)
    {
        app.WaitFor(() => app.FindAll(automationId).Count == count, $"{count} {automationId}", LoadTimeout);
        Thread.Sleep(500);
    }

    /// <summary>Starts the app with one tab on <paramref name="repository"/>, in a window of the given size.</summary>
    private static RealApp LaunchWithTabs(string data, string repository, int width = 1100, int height = 700)
    {
        var app = RealApp.Launch(data, new SessionState { Tabs = [new TabState(repository)] });
        app.SetClientSize(width, height);
        return app;
    }

    /// <summary>
    /// Clicks the working-changes row, which UI Automation sees only as part of the graph (D49):
    /// at its position, in the Message column's free part (the graph scenarios here have one lane,
    /// so the Message column starts 130 + 48 into the graph area, which starts at 260).
    /// </summary>
    private static void ClickWorkingRow(RealApp app, float rowsLeft = 260, float graphWidth = 440) =>
        app.ClickAt(rowsLeft + Math.Min(130 + 48 + 60, graphWidth / 2), RowsTop + (RowHeight / 2));

    private static void WaitForStagePanel(RealApp app, int unstaged, int staged) =>
        app.WaitFor(
            () => app.FindAll("UnstagedTitle").Any(text => text.Name == $"Unstaged files ({unstaged})")
                && app.FindAll("StagedTitle").Any(text => text.Name == $"Staged files ({staged})"),
            $"the stage panel with {unstaged} unstaged and {staged} staged files",
            LoadTimeout);

    /// <summary>
    /// Scrolls a stage list with the real wheel until a file's row shows, moves the real mouse over
    /// the row, and clicks one of the buttons that then show on it.
    /// </summary>
    private static void ClickRowButton(RealApp app, string list, string path, string buttonId)
    {
        var listBounds = app.BoundsOf(app.Find(list));
        app.ScrollWheel(listBounds.X + (listBounds.Width / 2), listBounds.Y + (listBounds.Height / 2), -3);
        AutomationElement? row = null;
        app.WaitFor(
            () => (row = app.FindAll("StageFileRow").FirstOrDefault(candidate => candidate.Name == path)) is not null,
            $"the row of {path}",
            LoadTimeout);
        var bounds = app.BoundsOf(row!);
        app.MoveMouseTo(bounds.X + 40, bounds.Y + (bounds.Height / 2));
        app.WaitFor(() => app.FindAll(buttonId).Count == 1, $"the {buttonId} of {path}", LoadTimeout);
        app.ClickWithMouse(app.Find(buttonId));
    }

    /// <summary>Waits until the app has logged the working tree with these counts as its latest status.</summary>
    private static void WaitForStatus(RealApp app, string data, string name, int staged, int unstaged) =>
        app.WaitFor(
            () => Regex.Matches(RealApp.ReadLog(data), $"{Regex.Escape(name)}: working tree: (\\d+) staged, (\\d+) unstaged") is { Count: > 0 } matches
                && matches[^1].Groups[1].Value == staged.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && matches[^1].Groups[2].Value == unstaged.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"the working tree of {name} with {staged} staged and {unstaged} unstaged files",
            LoadTimeout);

    /// <summary>Waits until the app's log holds <paramref name="text"/>.</summary>
    private static void WaitForLog(RealApp app, string data, string text) =>
        app.WaitFor(() => RealApp.ReadLog(data).Contains(text, StringComparison.Ordinal), $"the log line \"{text}\"", LoadTimeout);
}
