using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>Phase 2's checks of the stage panel's own actions: the lists, staging and unstaging whole files, commit and amend.</summary>
public partial class Phase2Checks
{
    [AvaloniaFact]
    public async Task Check_02_stage_panel_in_light_as_a_tree()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2, width: 1920, height: 1080, theme: AppTheme.Light);

        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        var flat = app.Capture();
        flat.Save(Phase, "02a-stage-panel-light");
        AssertWorkingRowBackground(app, flat, Palettes.Light.Selection);
        Assert.Equal(Phase2Expectations.UnstagedFlat, FileRows(app, "UnstagedFileList"));
        Assert.Equal(Phase2Expectations.StagedFlat, FileRows(app, "StagedFileList"));
        Assert.All(Phase2Expectations.UnstagedFlat.Select(row => row.Split(' ')[1]), name => Assert.NotNull(VisibleFileRow(app, "UnstagedFileList", name)));
        Assert.True(Graph(app).Columns.Sha.IsVisible && Graph(app).Columns.Author.IsVisible);
        Phase2Expectations.AssertStagePanelLayout(app, height: 1080);
        AssertNoClippedText(app);

        app.Click(app.Find<Button>("StageTreeButton"));
        var tree = app.Capture();
        tree.Save(Phase, "02b-stage-panel-tree");
        Assert.Equal(
            ["assets/", "  M logo.png", "data/", "  M blob.bin", "  M large.txt", "docs/", "  A guide.md", "  D old-notes.txt", "src/", "  M Calculator.cs", "M README.md"],
            FileRows(app, "UnstagedFileList"));
        Assert.Equal(
            ["config/", "  A settings.json", "src/", "  R helpers.py renamed from src/util.py", "M README.md"],
            FileRows(app, "StagedFileList"));
        Assert.Equal(FileListMode.Tree, new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.FileList);

        app.Click(app.Find<Button>("StageFlatButton"));
        Assert.Equal(Phase2Expectations.UnstagedFlat, FileRows(app, "UnstagedFileList"));
        Assert.Equal(FileListMode.Flat, new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.FileList);
    }

    [AvaloniaFact]
    public async Task Check_09_stage_and_unstage_whole_files()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        var before = WorkingTreeBytes(repo);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await ClickRowButtonAsync(app, "UnstagedFileList", "docs/guide.md", "RowStageButton");
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 4);
        app.Capture().Save(Phase, "09a-guide-staged");
        Assert.False(app.Session.Shell.ActiveTab.Repository!.IsDiffOpen);
        Assert.DoesNotContain("A guide.md docs", FileRows(app, "UnstagedFileList"));
        Assert.Equal(["M README.md", "A settings.json config", "A guide.md docs", "R helpers.py src renamed from src/util.py"], FileRows(app, "StagedFileList"));
        Assert.Equal("Commit 4 files", app.Find<Button>("CommitButton").Content);
        Assert.Contains("A  docs/guide.md", await PorcelainAsync(repo));

        app.Click(app.Find<Button>("StageAllButton"));
        await WaitForStagePanelAsync(app, unstaged: 0, staged: 9);
        app.Capture().Save(Phase, "09b-all-staged");
        Assert.True(app.Find<TextBlock>("NoUnstaged").IsEffectivelyVisible);
        Assert.False(app.Find<Button>("StageAllButton").IsEffectivelyVisible);
        Assert.False(app.Find<Button>("DiscardAllButton").IsEffectivelyVisible);
        Assert.Equal(
            ["M README.md", "M logo.png assets", "A settings.json config", "M blob.bin data", "M large.txt data", "A guide.md docs", "D old-notes.txt docs", "M Calculator.cs src", "R helpers.py src renamed from src/util.py"],
            FileRows(app, "StagedFileList"));
        Assert.Empty((await repo.GitAsync("diff", "--name-only")).StandardOutput);

        app.Click(app.Find<Button>("UnstageAllButton"));
        await WaitForStagePanelAsync(app, unstaged: 10, staged: 0);
        app.Capture().Save(Phase, "09c-all-unstaged");
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible);
        Assert.Equal(
            ["M README.md", "M logo.png assets", "A settings.json config", "M blob.bin data", "M large.txt data", "A guide.md docs", "D old-notes.txt docs", "M Calculator.cs src", "A helpers.py src", "D util.py src"],
            FileRows(app, "UnstagedFileList"));
        Assert.Empty((await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput);
        Assert.Equal(before, WorkingTreeBytes(repo));
    }

    [AvaloniaFact]
    public async Task Check_14_commit()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        app.Click(app.Find<TextBox>("CommitSummary"));
        app.Type("Add usage and settings");
        var typed = app.Capture();
        typed.Save(Phase, "14a-summary-typed");
        Assert.Equal("50", app.Find<TextBlock>("SummaryCounter").Text);
        Assert.True(app.Find<Button>("CommitButton").IsEffectivelyEnabled);

        app.Click(app.Find<TextBox>("CommitDescription"));
        app.Type("Explains how to use the calculator.");
        app.Click(app.Find<Button>("CommitButton"));
        await WaitForHeadAsync(app, repo, Phase2Expectations.FirstCommit);
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 0);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "14b-committed");

        var graph = Graph(app);
        Assert.True(graph.IsWorkingRowSelected);
        Assert.Equal(["Add usage and settings", "Add calculator", "Initial commit"], Subjects(graph));
        Assert.Equal("2026-01-02 09:00", app.Session.Shell.ActiveTab.Repository!.Dates.Format(graph.Data!.CommitAt(0).AuthorDate));
        Assert.Equal("main", Assert.Single(graph.LabelsInRow(0)).FullText);
        Assert.Empty(graph.LabelsInRow(1));
        Assert.Equal("Add usage and settings\n\nExplains how to use the calculator.\n", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd('\n') + "\n");
        Assert.Equal("Test Author <author@example.com>", (await repo.GitAsync("log", "-1", "--format=%an <%ae>")).StandardOutput.Trim());
        Assert.Equal("06faadcfa6435a48192116e2ffe76d1992d2f7c3", (await repo.GitAsync("rev-parse", "HEAD^")).StandardOutput.Trim());
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible);
        Assert.Equal(Phase2Expectations.UnstagedFlat, FileRows(app, "UnstagedFileList"));
        Assert.Equal(string.Empty, app.Find<TextBox>("CommitSummary").Text ?? string.Empty);
        Assert.Equal(string.Empty, app.Find<TextBox>("CommitDescription").Text ?? string.Empty);
        Assert.Equal("72", app.Find<TextBlock>("SummaryCounter").Text);
        Assert.Equal("Stage files to commit", app.Find<Button>("CommitButton").Content);
        Assert.False(app.Find<Button>("CommitButton").IsEffectivelyEnabled);
        Assert.Equal("main", app.Find<TextBlock>("CurrentBranch").Text);
    }

    [AvaloniaFact]
    public async Task Check_15_amend()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        app.Click(app.Find<TextBox>("CommitSummary"));
        app.Type("Add usage and settings");
        app.Click(app.Find<TextBox>("CommitDescription"));
        app.Type("Explains how to use the calculator.");
        app.Click(app.Find<Button>("CommitButton"));
        await WaitForHeadAsync(app, repo, Phase2Expectations.FirstCommit);
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 0);

        await ClickRowButtonAsync(app, "UnstagedFileList", "docs/guide.md", "RowStageButton");
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 1);
        app.Click(app.Find<CheckBox>("AmendCheckBox"));
        await app.WaitForAsync(() => app.Find<TextBox>("CommitSummary").Text == "Add usage and settings", "the summary of the commit to amend");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "15a-amend-ticked");
        Assert.Equal("Explains how to use the calculator.", app.Find<TextBox>("CommitDescription").Text);
        Assert.Equal("Amend previous commit", app.Find<Button>("CommitButton").Content);
        Assert.True(app.Find<Button>("CommitButton").IsEffectivelyEnabled);

        app.Click(app.Find<TextBox>("CommitSummary"));
        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type("Add usage, settings and a guide");
        app.Click(app.Find<Button>("CommitButton"));
        await WaitForHeadAsync(app, repo, Phase2Expectations.AmendedCommit);
        await WaitForStagePanelAsync(app, unstaged: 6, staged: 0);
        await app.WaitForAsync(() => Subjects(Graph(app))[0] == "Add usage, settings and a guide", "the amended commit in the graph");
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "15b-amended");

        Assert.Equal(["Add usage, settings and a guide", "Add calculator", "Initial commit"], Subjects(Graph(app)));
        Assert.Equal(-1, Graph(app).Data!.IndexOf(Phase2Expectations.FirstCommit));
        Assert.Equal("06faadcfa6435a48192116e2ffe76d1992d2f7c3", (await repo.GitAsync("rev-parse", "HEAD^")).StandardOutput.Trim());
        Assert.Contains("docs/guide.md", (await repo.GitAsync("ls-tree", "-r", "--name-only", "HEAD")).StandardOutput.Split('\n'));
        Assert.DoesNotContain("A guide.md docs", FileRows(app, "UnstagedFileList"));
        Assert.Equal(string.Empty, app.Find<TextBox>("CommitSummary").Text ?? string.Empty);
        Assert.False(app.Find<CheckBox>("AmendCheckBox").IsChecked);
    }

    [AvaloniaFact]
    public async Task Check_16_a_commit_git_refuses_shows_gits_message()
    {
        using var repo = await Scenarios.ChangesAsync();
        WriteRefusingHook(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        app.Click(app.Find<TextBox>("CommitSummary"));
        app.Type("Blocked");
        app.Click(app.Find<Button>("CommitButton"));
        await app.WaitForAsync(() => app.Find<TextBlock>("CommitError").IsEffectivelyVisible, "the commit's error");
        await app.WaitForAsync(() => app.Find<Button>("CommitButton").IsEffectivelyEnabled, "the commit button to be enabled again");
        var refused = app.Capture();
        refused.Save(Phase, "16-commit-refused");

        Assert.Equal("Commit blocked by the test hook", app.Find<TextBlock>("CommitError").Text);
        Assert.Equal(Palettes.Dark.Danger, Assert.IsAssignableFrom<ISolidColorBrush>(app.Find<TextBlock>("CommitError").Foreground).Color);
        Assert.Equal("06faadcfa6435a48192116e2ffe76d1992d2f7c3", await repo.HeadAsync());
        Assert.Equal(["Add calculator", "Initial commit"], Subjects(Graph(app)));
        Assert.Equal("Staged files (3)", app.Find<TextBlock>("StagedTitle").Text);
        Assert.Equal("Blocked", app.Find<TextBox>("CommitSummary").Text);
        AssertNoClippedText(app);
    }

    [AvaloniaFact]
    public async Task Check_22_unstage_a_renamed_file_with_its_rows_button()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        var before = WorkingTreeBytes(repo);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await ClickRowButtonAsync(app, "StagedFileList", "src/helpers.py", "RowUnstageButton");
        await WaitForStagePanelAsync(app, unstaged: 9, staged: 2);
        app.Capture().Save(Phase, "22-rename-unstaged");

        Assert.Equal(["M README.md", "A settings.json config"], FileRows(app, "StagedFileList"));
        Assert.Equal(
            ["M README.md", "M logo.png assets", "M blob.bin data", "M large.txt data", "A guide.md docs", "D old-notes.txt docs", "M Calculator.cs src", "A helpers.py src", "D util.py src"],
            FileRows(app, "UnstagedFileList"));
        Assert.Equal(["README.md", "config/settings.json"], (await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(before, WorkingTreeBytes(repo));
    }

    [AvaloniaFact]
    public async Task Check_25_the_summary_counter_and_the_commit_buttons_rules()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        var summary = app.Find<TextBox>("CommitSummary");
        var counter = app.Find<TextBlock>("SummaryCounter");
        var button = app.Find<Button>("CommitButton");

        app.Click(summary);
        app.Type("Explain how the calculator adds, subtracts, multiplies and divides numbers.");
        app.Capture().Save(Phase, "25a-summary-too-long");
        Assert.Equal("-3", counter.Text);
        Assert.Equal(Palettes.Dark.Warning, Assert.IsAssignableFrom<ISolidColorBrush>(counter.Foreground).Color);
        Assert.Equal("Commit 3 files", button.Content);
        Assert.True(button.IsEffectivelyEnabled);

        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type("   ");
        app.Capture().Save(Phase, "25b-summary-spaces");
        Assert.Equal("69", counter.Text);
        Assert.False(button.IsEffectivelyEnabled);

        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type("Keep");
        app.Click(app.Find<TextBox>("CommitDescription"));
        app.Type("Draft");
        app.Click(app.Find<CheckBox>("AmendCheckBox"));
        await app.WaitForAsync(() => button.Content as string == "Amend previous commit", "the amend button");
        app.Settle();
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "25c-amend-keeps-text");
        Assert.Equal("Keep", summary.Text);
        Assert.Equal("Draft", app.Find<TextBox>("CommitDescription").Text);
        Assert.True(button.IsEffectivelyEnabled);
    }

    /// <summary>Hovers a file row, then clicks one of its own buttons.</summary>
    private static async Task ClickRowButtonAsync(ShellDriver app, string list, string path, string buttonId)
    {
        var row = await ScrollToFileRowAsync(app, list, path);
        app.MoveMouse(row);
        var button = RowPanel(row).GetVisualDescendants().OfType<Button>()
            .Single(candidate => Avalonia.Automation.AutomationProperties.GetAutomationId(candidate) == buttonId);
        Assert.True(button.IsEffectivelyVisible, $"{buttonId} does not show on the row of {path}.");
        app.Click(button);
    }

    /// <summary>A file row's own button among the rows laid out now, by the file's name; null when it is not.</summary>
    private static Button? VisibleFileRow(ShellDriver app, string list, string name) =>
        app.Find<ItemsControl>(list).GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "StageFileRow"
                && (Avalonia.Automation.AutomationProperties.GetName(button) ?? string.Empty).EndsWith(name, StringComparison.Ordinal));

    private static Task WaitForHeadAsync(ShellDriver app, TempRepo repo, string sha) =>
        app.WaitForAsync(
            () => app.Session.Shell.ActiveTab.Repository is { Graph: { IsComplete: true } graph } && graph.Count > 0 && graph.Refs.Head.Sha == sha,
            $"HEAD at {sha[..7]} in the graph",
            TimeSpan.FromSeconds(20));

    private static async Task<List<string>> PorcelainAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Every file of the working tree (outside .git) with its bytes, to show that a step left them alone.</summary>
    private static SortedDictionary<string, string> WorkingTreeBytes(TempRepo repo) =>
        new(Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(repo.Path, file).StartsWith(".git", StringComparison.Ordinal))
            .ToDictionary(file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'), file => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))), StringComparer.Ordinal);

    /// <summary>A pre-commit hook that refuses every commit with a message on standard error (check 16).</summary>
    private static void WriteRefusingHook(TempRepo repo)
    {
        var hook = Path.Combine(repo.Path, ".git", "hooks", "pre-commit");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\necho 'Commit blocked by the test hook' >&2\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
