using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>Phase 2's checks of the diff's text: both modes, highlighting, and the actions on hunks and lines.</summary>
public partial class Phase2Checks
{
    private static readonly string[] CalculatorInline =
    [
        "@@ -12,7 +12,7 @@ public sealed class Calculator",
        "C 12/12",
        "C 13/13 public int Add(int a, int b)",
        "C 14/14 {",
        "R 15/- return a + b;",
        "A -/15 return a + b + _offset;",
        "C 16/16 }",
        "C 17/17",
        "C 18/18 public int Subtract(int a, int b)",
        "@@ -29,4 +29,8 @@ public sealed class Calculator",
        "C 29/29 {",
        "C 30/30 return a / b;",
        "C 31/31 }",
        "A -/32",
        "A -/33 public int Negate(int a) => -a;",
        "A -/34",
        "A -/35 public int Square(int a) => a * a;",
        "C 32/36 }",
    ];

    private static readonly string[] CalculatorSideBySide =
    [
        "@@ -12,7 +12,7 @@ public sealed class Calculator",
        "C 12 | C 12",
        "C 13 public int Add(int a, int b) | C 13 public int Add(int a, int b)",
        "C 14 { | C 14 {",
        "R 15 return a + b; | A 15 return a + b + _offset;",
        "C 16 } | C 16 }",
        "C 17 | C 17",
        "C 18 public int Subtract(int a, int b) | C 18 public int Subtract(int a, int b)",
        "@@ -29,4 +29,8 @@ public sealed class Calculator",
        "C 29 { | C 29 {",
        "C 30 return a / b; | C 30 return a / b;",
        "C 31 } | C 31 }",
        "· | A 32",
        "· | A 33 public int Negate(int a) => -a;",
        "· | A 34",
        "· | A 35 public int Square(int a) => a * a;",
        "C 32 } | C 36 }",
    ];

    [AvaloniaFact]
    public async Task Check_03_diff_of_a_modified_file_inline_and_side_by_side()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2, width: 1400, height: 900);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await OpenTextDiffAsync(app, "UnstagedFileList", "src/Calculator.cs");
        app.MoveMouse(new Point(130, 900 - 13));
        var inline = app.Capture();
        inline.Save(Phase, "03a-calculator-inline");
        AssertDiffHeader(app, "M", "Calculator.cs", "src", "Unstaged");
        AssertToolbar(app, inline: true, ["StageFileButton", "DiscardFileButton"]);
        var view = DiffText(app);
        Assert.Equal(CalculatorInline, Rows(view));
        Assert.Equal(["StageHunkButton", "DiscardHunkButton", "StageHunkButton", "DiscardHunkButton"], HunkButtons(app));
        AssertCalculatorColours(app, inline, Phase2Expectations.Dark, Palettes.Dark, dark: true);
        Assert.True(Row(app, "UnstagedFileList", "src/Calculator.cs").IsSelected);
        AssertNoClippedText(app);

        app.Click(app.Find<Button>("SideBySideDiffButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).SequenceEqual(CalculatorSideBySide), "the side-by-side rows");
        app.MoveMouse(new Point(130, 900 - 13));
        var sideBySide = app.Capture();
        sideBySide.Save(Phase, "03b-calculator-side-by-side");
        AssertToolbar(app, inline: false, ["StageFileButton", "DiscardFileButton"]);
        Assert.Equal(DiffMode.SideBySide, new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.DiffMode);
        view = DiffText(app);
        Assert.Equal(view.RowBounds(4)!.Value.Y, Window(app, view.TextRangeBounds(new DiffLineRef(0, 3), 0, 1)!.Value).Y - app.BoundsOf(view).Y, 1.0);
        AssertRowColour(app, sideBySide, 12, Phase2Expectations.Dark.Filler, left: true, "a filler row on the left");
        AssertRowColour(app, sideBySide, 12, Phase2Expectations.Dark.Added, left: false, "the added line 32 on the right");
        AssertRowColour(app, sideBySide, 4, Phase2Expectations.Dark.Removed, left: true, "the removed line 15 on the left");
        AssertWordBackground(app, sideBySide, new DiffLineRef(0, 4), "_offset", Phase2Expectations.Dark.AddedWord);
        AssertNoClippedText(app);

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "the diff to close");
        app.Capture().Save(Phase, "03c-closed");
        Assert.True(Graph(app).IsEffectivelyVisible);
        Assert.True(Graph(app).IsWorkingRowSelected);
        Assert.All(app.Session.Shell.ActiveTab.Changes!.Unstaged.Rows, row => Assert.False(row.IsSelected));
    }

    [AvaloniaFact]
    public async Task Check_04_added_deleted_and_renamed_files_both_modes_light()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2, width: 1920, height: 1080, theme: AppTheme.Light);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        var light = Phase2Expectations.Light;

        await OpenTextDiffAsync(app, "UnstagedFileList", "docs/guide.md");
        var guide = app.Capture();
        guide.Save(Phase, "04a-added-inline");
        AssertDiffHeader(app, "A", "guide.md", "docs", "Unstaged");
        Assert.Equal(["@@ -0,0 +1,4 @@", "A -/1 # Guide", "A -/2", "A -/3 Add numbers with Add.", "A -/4 Subtract them with Subtract."], Rows(DiffText(app)));
        Assert.Equal(["StageHunkButton", "DiscardHunkButton"], HunkButtons(app));
        AssertBrush(Color.Parse("#800000"), DiffText(app).ForegroundAt(new DiffLineRef(0, 0), 2), "# Guide");
        Assert.Equal(FontWeight.Bold, DiffText(app).FontWeightAt(new DiffLineRef(0, 0), 2));
        AssertRowColour(app, guide, 1, light.Added, left: true, "an added row");

        app.Click(app.Find<Button>("SideBySideDiffButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 5 && Rows(DiffText(app))[1].Contains('|'), "the side-by-side rows");
        var guideSides = app.Capture();
        guideSides.Save(Phase, "04b-added-side-by-side");
        Assert.Equal(["@@ -0,0 +1,4 @@", "· | A 1 # Guide", "· | A 2", "· | A 3 Add numbers with Add.", "· | A 4 Subtract them with Subtract."], Rows(DiffText(app)));
        AssertRowColour(app, guideSides, 1, light.Filler, left: true, "a filler row on the left");

        await OpenTextDiffAsync(app, "UnstagedFileList", "docs/old-notes.txt");
        app.Capture().Save(Phase, "04c-deleted-side-by-side");
        AssertDiffHeader(app, "D", "old-notes.txt", "docs", "Unstaged");
        Assert.Contains("selected", app.Find<Button>("SideBySideDiffButton").Classes);
        Assert.Equal(["@@ -1,2 +0,0 @@", "R 1 Old notes. | ·", "R 2 They are out of date. | ·"], Rows(DiffText(app)));

        app.Click(app.Find<Button>("InlineDiffButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).SequenceEqual(["@@ -1,2 +0,0 @@", "R 1/- Old notes.", "R 2/- They are out of date."]), "the inline rows");
        app.Capture().Save(Phase, "04d-deleted-inline");

        await OpenTextDiffAsync(app, "StagedFileList", "src/helpers.py");
        app.MoveMouse(new Point(130, 1080 - 13));
        var helpers = app.Capture();
        helpers.Save(Phase, "04e-renamed-inline");
        AssertDiffHeader(app, "R", "helpers.py", "src", "Staged");
        Assert.Equal("renamed from src/util.py", app.Find<TextBlock>("DiffOrigin").Text);
        AssertToolbar(app, inline: true, ["UnstageFileButton"]);
        Assert.Equal(["UnstageHunkButton"], HunkButtons(app));
        Assert.Equal(
            ["@@ -1,4 +1,4 @@", "R 1/- \"\"\"Small helpers for the calculator.\"\"\"", "A -/1 \"\"\"Helpers shared by the calculator.\"\"\"", "C 2/2", "C 3/3", "C 4/4 def clamp(value, low, high):"],
            Rows(DiffText(app)));
        var view = DiffText(app);
        AssertBrush(Color.Parse("#A31515"), view.ForegroundAt(new DiffLineRef(0, 1), 5), "the docstring");
        AssertBrush(Color.Parse("#0000FF"), view.ForegroundAt(new DiffLineRef(0, 4), 0), "def");
        AssertBrush(Color.Parse("#795E26"), view.ForegroundAt(new DiffLineRef(0, 4), 4), "clamp");
        AssertWordBackground(app, helpers, new DiffLineRef(0, 0), "Small", light.RemovedWord);
        AssertWordBackground(app, helpers, new DiffLineRef(0, 1), "shared", light.AddedWord);
        AssertWordBackground(app, helpers, new DiffLineRef(0, 1), "calculator", light.Added);

        app.Click(app.Find<Button>("SideBySideDiffButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 5, "the side-by-side rows");
        app.MoveMouse(new Point(130, 1080 - 13));
        app.Capture().Save(Phase, "04f-renamed-side-by-side");
        Assert.Equal(
            ["@@ -1,4 +1,4 @@", "R 1 \"\"\"Small helpers for the calculator.\"\"\" | A 1 \"\"\"Helpers shared by the calculator.\"\"\"", "C 2 | C 2", "C 3 | C 3", "C 4 def clamp(value, low, high): | C 4 def clamp(value, low, high):"],
            Rows(DiffText(app)));
        AssertNoClippedText(app);
    }

    [AvaloniaFact]
    public async Task Check_07_very_large_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await OpenDiffAsync(app, "UnstagedFileList", "data/large.txt", DiffBody.VeryLarge);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "07a-very-large");
        Assert.Contains("Very large diff", VisibleTexts(app, "DiffView"));
        Assert.Equal("30,000 lines removed and 30,000 added.", app.Find<TextBlock>("LargeDiffText").Text);
        Assert.True(app.Find<Button>("ShowLargeDiffButton").IsEffectivelyVisible);
        AssertToolbar(app, inline: true, ["StageFileButton", "DiscardFileButton"]);

        app.Click(app.Find<Button>("ShowLargeDiffButton"));
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository!.Diff!.ShowsText && DiffText(app).Rows.Count == 60_001, "the large diff's rows", TimeSpan.FromSeconds(30));
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 700 - 13));
        var shown = app.Capture();
        shown.Save(Phase, "07b-large-shown");
        var view = DiffText(app);
        Assert.Equal(["@@ -1,30000 +1,30000 @@", "R 1/- Line 00001 of the large file."], Rows(view).Take(2));
        Assert.Equal(["StageHunkButton", "DiscardHunkButton"], HunkButtons(app));
        AssertBrush(Palettes.Dark.Primary, view.ForegroundAt(new DiffLineRef(0, 0), 0), "the first line's text");
        AssertWordBackground(app, shown, new DiffLineRef(0, 0), "Line", Phase2Expectations.Dark.Removed);

        app.PressKey(Key.End, RawInputModifiers.Control);
        await SettleDiffAsync(app);
        await app.WaitForAsync(() => DiffText(app).RowBounds(60_000) is not null, "the last row in view");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "07c-large-end");
        view = DiffText(app);
        Assert.Equal(["A -/29998 Row 29998 of the large file.", "A -/29999 Row 29999 of the large file.", "A -/30000 Row 30000 of the large file."], Rows(view).TakeLast(3));
        Assert.Equal(app.BoundsOf(view).Height, view.RowBounds(60_000)!.Value.Bottom, 2.0);

        app.Click(app.Find<Button>("SideBySideDiffButton"));
        await app.WaitForAsync(() => DiffText(app).Rows.Count == 30_001, "the side-by-side rows", TimeSpan.FromSeconds(30));
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "07d-large-side-by-side");
        Assert.Equal("R 1 Line 00001 of the large file. | A 1 Row 00001 of the large file.", Rows(DiffText(app))[1]);
    }

    [AvaloniaFact]
    public async Task Check_08_a_commits_file_opens_its_diff()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);

        ClickRow(app, 0);
        await WaitForDetailsAsync(app, "Add calculator");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "08a-commit-details");
        var details = ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel"));
        Assert.Contains("Changed files (6)", details);

        var file = app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "ChangedFile" && Avalonia.Automation.AutomationProperties.GetName(button) == "Calculator.cs");
        app.Click(file);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository!.Diff is { ShowsText: true } && DiffText(app).Rows.Count == 33, "the commit's diff of Calculator.cs");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "08b-commit-diff");
        AssertDiffHeader(app, "A", "Calculator.cs", "src", "06faadc");
        AssertToolbar(app, inline: true, []);
        Assert.Empty(HunkButtons(app));
        Assert.Equal(["@@ -0,0 +1,32 @@", "A -/1 namespace Demo;"], Rows(DiffText(app)).Take(2));
        Assert.Contains("selected", file.Classes);

        app.Click(app.Find<Button>("CloseDiffButton"));
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "the diff to close");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "08c-closed");
        Assert.Equal(0, Graph(app).SelectedIndex);
        Assert.DoesNotContain("selected", file.Classes);
    }

    [AvaloniaFact]
    public async Task Check_10_stage_one_hunk()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenCalculatorAsync(data, repo);

        app.Click(HunkButtons(app, "StageHunkButton")[0]);
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 4);
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 9, "the unstaged diff's one remaining hunk");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "10a-hunk-staged");
        Assert.Equal(CalculatorInline.Skip(9), Rows(DiffText(app)));
        Assert.Contains("M Calculator.cs src", FileRows(app, "StagedFileList"));
        var staged = (await repo.GitAsync("diff", "--cached", "--", "src/Calculator.cs")).StandardOutput;
        Assert.Equal(["@@ -12,7 +12,7 @@ public sealed class Calculator"], staged.Split('\n').Where(line => line.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Contains("+        return a + b + _offset;", staged.Split('\n'));
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));

        await OpenTextDiffAsync(app, "StagedFileList", "src/Calculator.cs");
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "10b-staged-diff");
        AssertDiffHeader(app, "M", "Calculator.cs", "src", "Staged");
        AssertToolbar(app, inline: true, ["UnstageFileButton"]);
        Assert.Equal(["UnstageHunkButton"], HunkButtons(app));
        Assert.Equal(CalculatorInline.Take(9), Rows(DiffText(app)));
    }

    [AvaloniaFact]
    public async Task Check_11_stage_one_line_then_unstage_it()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenCalculatorAsync(data, repo);
        app.Click(HunkButtons(app, "StageHunkButton")[0]);
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 4);
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 9, "the unstaged diff's one remaining hunk");
        await SettleDiffAsync(app);

        var square = new DiffLineRef(0, 6);
        Assert.Equal("    public int Square(int a) => a * a;", app.Session.Shell.ActiveTab.Repository!.Diff!.Diff!.Hunks[0].Lines[6].Text);
        app.Click(LineNumber(app, square));
        await app.WaitForAsync(() => app.Find<Button>("StageLinesButton").IsEffectivelyVisible, "the line buttons");
        app.MoveMouse(new Point(130, 900 - 13));
        var selected = app.Capture();
        selected.Save(Phase, "11a-line-selected");
        AssertToolbar(app, inline: true, ["StageLinesButton", "DiscardLinesButton"]);
        AssertRowColour(app, selected, 7, Palettes.Dark.Selection, left: true, "the selected row");

        app.Click(app.Find<Button>("StageLinesButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).FirstOrDefault() == "@@ -29,5 +29,8 @@ public sealed class Calculator", "the unstaged diff after the line was staged");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "11b-line-staged");
        Assert.Equal(
            ["@@ -29,5 +29,8 @@ public sealed class Calculator", "C 29/29 {", "C 30/30 return a / b;", "C 31/31 }", "A -/32", "A -/33 public int Negate(int a) => -a;", "A -/34", "C 32/35 public int Square(int a) => a * a;", "C 33/36 }"],
            Rows(DiffText(app)));
        AssertToolbar(app, inline: true, ["StageFileButton", "DiscardFileButton"]);
        var index = Scenarios.ChangesFiles.CalculatorCommitted
            .Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal)
            .Replace("        return a / b;\n    }\n}\n", "        return a / b;\n    }\n    public int Square(int a) => a * a;\n}\n", StringComparison.Ordinal);
        Assert.Equal(index, (await repo.GitAsync("show", ":src/Calculator.cs")).StandardOutput);

        await OpenTextDiffAsync(app, "StagedFileList", "src/Calculator.cs");
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "11c-staged-diff");
        Assert.Equal(
            [.. CalculatorInline.Take(9), "@@ -29,4 +29,5 @@ public sealed class Calculator", "C 29/29 {", "C 30/30 return a / b;", "C 31/31 }", "A -/32 public int Square(int a) => a * a;", "C 32/33 }"],
            Rows(DiffText(app)));

        app.Click(LineNumber(app, new DiffLineRef(1, 3)));
        await app.WaitForAsync(() => app.Find<Button>("UnstageLinesButton").IsEffectivelyVisible, "the Unstage lines button");
        app.Click(app.Find<Button>("UnstageLinesButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 9, "the staged diff's one remaining hunk");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "11d-line-unstaged");
        Assert.Equal(CalculatorInline.Take(9), Rows(DiffText(app)));
        Assert.Equal(
            Scenarios.ChangesFiles.CalculatorCommitted.Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal),
            (await repo.GitAsync("show", ":src/Calculator.cs")).StandardOutput);
        Assert.Equal(
            ["@@ -12,7 +12,7 @@ public sealed class Calculator"],
            (await repo.GitAsync("diff", "--cached", "--", "src/Calculator.cs")).StandardOutput.Split('\n').Where(line => line.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Contains(
            "@@ -29,4 +29,8 @@ public sealed class Calculator",
            (await repo.GitAsync("diff", "--", "src/Calculator.cs")).StandardOutput.Split('\n'));
    }

    [AvaloniaFact]
    public async Task Check_12_unstage_a_hunk()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await OpenTextDiffAsync(app, "StagedFileList", "README.md");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "12a-readme-staged");
        Assert.Equal(["@@ -1,3 +1,7 @@", "C 1/1 # Calculator", "C 2/2", "C 3/3 A small calculator for tests.", "A -/4", "A -/5 ## Usage", "A -/6", "A -/7 Create a Calculator and call Add."], Rows(DiffText(app)));
        Assert.Equal(["UnstageHunkButton"], HunkButtons(app));
        AssertBrush(Color.Parse("#569CD6"), DiffText(app).ForegroundAt(new DiffLineRef(0, 4), 0), "## Usage");

        app.Click(HunkButtons(app, "UnstageHunkButton")[0]);
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 2);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "the emptied diff to close");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "12b-hunk-unstaged");
        Assert.DoesNotContain("M README.md", FileRows(app, "StagedFileList"));
        Assert.Contains("M README.md", FileRows(app, "UnstagedFileList"));
        Assert.Equal(Scenarios.ChangesFiles.ReadmeCommitted, (await repo.GitAsync("show", ":README.md")).StandardOutput);
        Assert.Equal(["@@ -1,3 +1,7 @@"], (await repo.GitAsync("diff", "--", "README.md")).StandardOutput.Split('\n').Where(line => line.StartsWith("@@", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public async Task Check_13_line_endings_with_autocrlf()
    {
        using var repo = await Scenarios.CrlfAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 1);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 1, staged: 0);

        await OpenTextDiffAsync(app, "UnstagedFileList", "notes.txt");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "13a-crlf-diff");
        var rows = Rows(DiffText(app));
        Assert.Equal("@@ -1,5 +1,5 @@", rows[0]);
        Assert.Equal("@@ -15,6 +15,6 @@ Note 14", rows[7]);
        Assert.Equal(["R 2/- Note 2", "A -/2 Note 2, changed"], rows.Skip(2).Take(2));
        Assert.Equal(["R 18/- Note 18", "A -/18 Note 18, changed"], rows.Skip(11).Take(2));
        Assert.All(DiffText(app).Rows.SelectMany(row => new[] { row.Left?.Text, row.Right?.Text }).OfType<string>(), text => Assert.DoesNotMatch("[\r␍]", text));

        app.Click(HunkButtons(app, "StageHunkButton")[0]);
        await WaitForStagePanelAsync(app, unstaged: 1, staged: 1);
        await app.WaitForAsync(() => HunkButtons(app).Count == 2, "the one remaining hunk");
        app.Click(HunkButtons(app, "DiscardHunkButton")[0]);
        await WaitForDialogAsync(app);
        Assert.Equal("Discard this hunk of notes.txt?", app.Find<TextBlock>("ConfirmQuestion").Text);
        app.Click(app.Find<Button>("ConfirmButton"));
        await WaitForStagePanelAsync(app, unstaged: 0, staged: 1);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "the emptied diff to close");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "13b-crlf-staged-and-discarded");
        Assert.Equal(["M notes.txt"], FileRows(app, "StagedFileList"));
        var index = Scenarios.CrlfNotes(changed: false, lineEnding: "\n").Replace("Note 2\n", "Note 2, changed\n", StringComparison.Ordinal);
        Assert.Equal(index, (await repo.GitAsync("show", ":notes.txt")).StandardOutput);
        Assert.Equal(index.Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));
        Assert.Equal("Discarded changes to notes.txt.", app.Find<TextBlock>("RestoreBarText").Text);
        Assert.True(Graph(app).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Check_19_discard_a_hunk_and_a_line()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenCalculatorAsync(data, repo);
        var file = Path.Combine(repo.Path, "src", "Calculator.cs");

        app.Click(HunkButtons(app, "DiscardHunkButton")[1]);
        await WaitForDialogAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "19a-discard-hunk-dialog");
        Assert.Equal("Discard this hunk of Calculator.cs?", app.Find<TextBlock>("ConfirmQuestion").Text);
        Assert.True(app.FindByAutomationId("DiffView").IsEffectivelyVisible);

        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == 9, "the diff's one remaining hunk");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "19b-hunk-discarded");
        Assert.Equal(CalculatorInline.Take(9), Rows(DiffText(app)));
        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted.Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal), File.ReadAllText(file));

        app.Click(app.Find<Button>("RestoreDiscardButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Count == CalculatorInline.Length, "both hunks again");
        await SettleDiffAsync(app);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(file));
        Assert.Equal(CalculatorInline, Rows(DiffText(app)));

        app.Click(LineNumber(app, new DiffLineRef(1, 4)));
        await app.WaitForAsync(() => app.Find<Button>("DiscardLinesButton").IsEffectivelyVisible, "the line buttons");
        app.Click(app.Find<Button>("DiscardLinesButton"));
        await WaitForDialogAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "19c-discard-line-dialog");
        Assert.Equal("Discard 1 selected line of Calculator.cs?", app.Find<TextBlock>("ConfirmQuestion").Text);

        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => Rows(DiffText(app)).Contains("@@ -29,4 +29,7 @@ public sealed class Calculator"), "the second hunk without the line");
        await SettleDiffAsync(app);
        app.MoveMouse(new Point(130, 900 - 13));
        app.Capture().Save(Phase, "19d-line-discarded");
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking.Replace("    public int Negate(int a) => -a;\n", string.Empty, StringComparison.Ordinal), File.ReadAllText(file));
        Assert.Equal(
            [.. CalculatorInline.Take(9), "@@ -29,4 +29,7 @@ public sealed class Calculator", "C 29/29 {", "C 30/30 return a / b;", "C 31/31 }", "A -/32", "A -/33", "A -/34 public int Square(int a) => a * a;", "C 32/35 }"],
            Rows(DiffText(app)));
    }

    [Fact]
    public async Task Check_21_the_diff_mode_survives_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase2Checks).Assembly;
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo, commits: 2, width: 1400, height: 900);
                app.Click(WorkingRowPoint(app));
                await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
                await OpenTextDiffAsync(app, "UnstagedFileList", "README.md");
                app.Click(app.Find<Button>("SideBySideDiffButton"));
                await app.WaitForAsync(() => Rows(DiffText(app)).Count == 7, "the side-by-side rows");
                return true;
            },
            cancellationToken);

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo, commits: 2, width: 1400, height: 900);
                app.Click(WorkingRowPoint(app));
                await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
                await OpenTextDiffAsync(app, "UnstagedFileList", "README.md");
                app.MoveMouse(new Point(130, 900 - 13));
                var shot = app.Capture();
                shot.Save(Phase, "21-side-by-side-after-restart");
                Assert.Contains("selected", app.Find<Button>("SideBySideDiffButton").Classes);
                Assert.Equal(
                    ["@@ -1,6 +1,6 @@", "C 1 # Calculator | C 1 # Calculator", "C 2 | C 2", "R 3 A small calculator for tests. | A 3 A small calculator for the visual checks.", "C 4 | C 4", "C 5 ## Usage | C 5 ## Usage", "C 6 | C 6"],
                    Rows(DiffText(app)));
                AssertWordBackground(app, shot, new DiffLineRef(0, 2), "tests", Phase2Expectations.Dark.RemovedWord);
                AssertWordBackground(app, shot, new DiffLineRef(0, 3), "visual", Phase2Expectations.Dark.AddedWord);
                AssertWordBackground(app, shot, new DiffLineRef(0, 3), "small", Phase2Expectations.Dark.Added);
                AssertBrush(Color.Parse("#569CD6"), DiffText(app).ForegroundAt(new DiffLineRef(0, 0), 2), "# Calculator");
                Assert.Equal(FontWeight.Bold, DiffText(app).FontWeightAt(new DiffLineRef(0, 0), 2));
                Assert.Equal(DiffMode.SideBySide, new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.DiffMode);
                return true;
            },
            cancellationToken);
    }

    // ----- helpers -----

    /// <summary>Checks 10, 11 and 19 start as check 3: the changes scenario at 1400×900, the unstaged diff of src/Calculator.cs.</summary>
    private static async Task<ShellDriver> OpenCalculatorAsync(TempDirectory data, TempRepo repo)
    {
        var app = await OpenAsync(data, repo, commits: 2, width: 1400, height: 900);
        try
        {
            app.Click(WorkingRowPoint(app));
            await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
            await OpenTextDiffAsync(app, "UnstagedFileList", "src/Calculator.cs");
            Assert.Equal(CalculatorInline, Rows(DiffText(app)));
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static DiffTextView DiffText(ShellDriver app) => app.Find<DiffTextView>("DiffText");

    /// <summary>Opens a file's diff as text and waits until its rows are laid out.</summary>
    private static async Task OpenTextDiffAsync(ShellDriver app, string list, string path)
    {
        await OpenDiffAsync(app, list, path, DiffBody.Text);
        await app.WaitForAsync(() => DiffText(app).Rows.Count > 0 && DiffText(app).RowBounds(0) is not null, $"the rows of {path}'s diff");
        await SettleDiffAsync(app);
    }

    /// <summary>Lets the diff text finish its layout: its header rows' heights settle a layout pass after the lines.</summary>
    private static async Task SettleDiffAsync(ShellDriver app)
    {
        for (var i = 0; i < 4; i++)
        {
            app.Settle();
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }

        app.Capture();
    }

    /// <summary>
    /// The diff's rows as text. Inline: "K old/new text" (K the kind's letter, "-" for no number);
    /// side by side: "K number text | K number text", "·" for a filler. Hunk headers as git wrote
    /// them. Texts without their indentation.
    /// </summary>
    private static List<string> Rows(DiffTextView view) =>
        [.. view.Rows.Select(row => row.Kind switch
        {
            DiffRowKind.HunkHeader => row.HeaderText ?? string.Empty,
            _ when view.Mode == DiffMode.Inline && row.Left is { } only => Join($"{Letter(only)} {Number(only.OldNumber)}/{Number(only.NewNumber)}", only.Text.Trim()),
            _ => $"{Cell(row.Left)} | {Cell(row.Right)}",
        })];

    private static string Cell(DiffCell? cell) => cell is null ? "·" : Join($"{Letter(cell)} {Number(cell.Number)}", cell.Text.Trim());

    private static string Letter(DiffCell cell) => cell.Kind switch
    {
        DiffLineKind.Added => "A",
        DiffLineKind.Removed => "R",
        _ => "C",
    };

    private static string Number(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string Join(string head, string text) => text.Length == 0 ? head : $"{head} {text}";

    /// <summary>The hunk buttons in UI Automation's order, by automation id.</summary>
    private static List<string> HunkButtons(ShellDriver app) =>
        [.. HunkButtonControls(app).Select(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button)!)];

    private static List<Button> HunkButtons(ShellDriver app, string automationId) =>
        [.. HunkButtonControls(app).Where(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == automationId)];

    private static IEnumerable<Button> HunkButtonControls(ShellDriver app) =>
        DiffText(app).GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(button) is "StageHunkButton" or "DiscardHunkButton" or "UnstageHunkButton");

    /// <summary>The toolbar: the mode toggle in use, and exactly these action buttons showing.</summary>
    private static void AssertToolbar(ShellDriver app, bool inline, string[] actions)
    {
        Assert.True(app.Find<Button>("InlineDiffButton").IsEffectivelyVisible);
        Assert.Equal(inline, app.Find<Button>("InlineDiffButton").Classes.Contains("selected"));
        Assert.Equal(!inline, app.Find<Button>("SideBySideDiffButton").Classes.Contains("selected"));
        string[] all = ["StageLinesButton", "DiscardLinesButton", "UnstageLinesButton", "StageFileButton", "DiscardFileButton", "UnstageFileButton"];
        Assert.Equal(actions, all.Where(id => app.Find<Button>(id).IsEffectivelyVisible));
    }

    /// <summary>Check 3's colours: row backgrounds, the word highlights of line 15, and the syntax colours.</summary>
    private static void AssertCalculatorColours(ShellDriver app, Screenshot screenshot, Phase2Expectations.DiffPalette diff, Palettes.Palette palette, bool dark)
    {
        var view = DiffText(app);
        AssertRowColour(app, screenshot, 0, diff.Hunk, left: true, "the first hunk's header");
        AssertRowColour(app, screenshot, 1, palette.Window, left: true, "a context row");
        AssertRowColour(app, screenshot, 4, diff.Removed, left: true, "the removed line 15");
        AssertRowColour(app, screenshot, 5, diff.Added, left: true, "the added line 15");
        var removed = new DiffLineRef(0, 3);
        var added = new DiffLineRef(0, 4);
        AssertWordBackground(app, screenshot, added, "_offset", diff.AddedWord);
        AssertWordBackground(app, screenshot, added, "return", diff.Added);
        AssertWordBackground(app, screenshot, removed, "return", diff.Removed);
        AssertWordBackground(app, screenshot, removed, "b;", diff.Removed);

        AssertBrush(Color.Parse(dark ? "#C586C0" : "#AF00DB"), view.ForegroundAt(added, 8), "return");
        AssertBrush(Color.Parse(dark ? "#9CDCFE" : "#001080"), view.ForegroundAt(added, 23), "_offset");
        AssertBrush(Color.Parse(dark ? "#D4D4D4" : "#000000"), view.ForegroundAt(added, 17), "+");
        AssertBrush(palette.Primary, view.ForegroundAt(added, 30), ";");
        var negate = new DiffLineRef(1, 4);
        AssertBrush(Color.Parse(dark ? "#569CD6" : "#0000FF"), view.ForegroundAt(negate, 4), "public");
        AssertBrush(Color.Parse(dark ? "#569CD6" : "#0000FF"), view.ForegroundAt(negate, 11), "int");
        AssertBrush(Color.Parse(dark ? "#DCDCAA" : "#795E26"), view.ForegroundAt(negate, 15), "Negate");
        AssertBrush(Color.Parse(dark ? "#9CDCFE" : "#001080"), view.ForegroundAt(negate, 26), "a");
    }

    private static void AssertBrush(Color expected, IBrush? brush, string what) =>
        Assert.True(brush is ISolidColorBrush solid && solid.Color == expected, $"{what} is drawn with {(brush as ISolidColorBrush)?.Color.ToString() ?? "no solid brush"}, not {expected}.");

    /// <summary>
    /// A row's background: sampled 6 from the right edge of its half (or of the whole width
    /// inline), 3 below its top, where no text reaches in these diffs.
    /// </summary>
    private static void AssertRowColour(ShellDriver app, Screenshot screenshot, int row, Color colour, bool left, string what)
    {
        var view = DiffText(app);
        var origin = app.BoundsOf(view).TopLeft;
        var bounds = view.RowBounds(row) ?? throw new InvalidOperationException($"Row {row} is not in view.");
        var sideBySide = view.Rows.Any(candidate => candidate.Right is not null);
        var right = !sideBySide ? bounds.Right - 6 : left ? (bounds.Width / 2) - 8 : bounds.Right - 20;
        ShellExpectations.AssertColour(colour, screenshot, origin + new Point(right, bounds.Y + 3), what);
    }

    /// <summary>The background behind a word of a line: sampled 1 inside the top-left corner of its first character's cell.</summary>
    private static void AssertWordBackground(ShellDriver app, Screenshot screenshot, DiffLineRef line, string word, Color colour)
    {
        var view = DiffText(app);
        var diff = app.Session.Shell.ActiveTab.Repository!.Diff!.Diff!;
        var text = diff.Hunks[line.Hunk].Lines[line.Line].Text;
        var start = text.IndexOf(word, StringComparison.Ordinal);
        Assert.True(start >= 0, $"\"{word}\" is not in \"{text}\".");
        var bounds = Window(app, view.TextRangeBounds(line, start, 1) ?? throw new InvalidOperationException($"\"{word}\" is not in view."));
        ShellExpectations.AssertColour(colour, screenshot, bounds.TopLeft + new Point(1, 1), $"the background behind \"{word}\"");
    }

    /// <summary>A rectangle of the diff text control in window coordinates.</summary>
    private static Rect Window(ShellDriver app, Rect rect) => rect.Translate((Vector)app.BoundsOf(DiffText(app)).TopLeft);

    /// <summary>Where to click a line's number, in window coordinates.</summary>
    private static Point LineNumber(ShellDriver app, DiffLineRef line) =>
        app.BoundsOf(DiffText(app)).TopLeft + (DiffText(app).LineNumberPoint(line) ?? throw new InvalidOperationException($"Line {line} is not in view."));

    /// <summary>A stage list's row for a file.</summary>
    private static App.ViewModels.Panels.ChangedFileRow Row(ShellDriver app, string list, string path) =>
        ((IEnumerable<App.ViewModels.Panels.ChangedFileRow>)app.Find<ItemsControl>(list).ItemsSource!).Single(row => row.Key == path);
}
