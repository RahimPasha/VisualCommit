using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using Xunit;
using static VisualCommit.VisualTests.Phase1.GraphScenarioExpectations;

namespace VisualCommit.VisualTests.Phase1;

/// <summary>
/// The checks of phase 1 that look at an open repository: the graph, the commit details, the
/// left panel and its filter, the 100k-commit repo, Q1, and changes made outside the app
/// (checks 1 to 9 and 18 of docs/test-reports/phase-1.md).
/// </summary>
public partial class Phase1Checks
{
    private const string AddSettingsPage = "Add settings page";

    [AvaloniaTheory]
    [InlineData(1, 1100, 700, AppTheme.Dark)]
    [InlineData(2, 1920, 1080, AppTheme.Light)]
    public async Task Checks_1_and_2_the_graph_scenario(int check, int width, int height, AppTheme theme)
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        if (theme == AppTheme.Light)
        {
            UseLightTheme(data);
        }

        using var app = await OpenGraphScenarioAsync(data, repo, width, height);
        var screenshot = app.Capture();
        screenshot.Save(Phase, check == 1 ? "01-graph-dark-1100x700" : "02-graph-light-1920x1080");

        var palette = theme == AppTheme.Dark ? Palettes.Dark : Palettes.Light;
        Assert.Equal(theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.Equal(["graph"], Tabs(app));
        Assert.True(app.Find<Button>("CloseTabButton").IsEffectivelyVisible);
        AssertGraphScenario(app, screenshot, palette, allColumns: check == 2);
        Assert.Equal(-1, Graph(app).SelectedIndex);
        Assert.Equal(["Commit details", "Select a commit to see its details."], ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal("main", app.Find<TextBlock>("CurrentBranch").Text);
        AssertGraphScenarioLeftPanel(app, screenshot, palette);
        Assert.Equal(PanelWidthsOf(app), (ShellExpectations.LeftPanelWidth, ShellExpectations.RightPanelWidth));
        AssertNoClippedText(app);
    }

    [Fact]
    public async Task Check_3_commit_details_and_the_file_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase1Checks).Assembly;
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        var sha = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenGraphScenarioAsync(data, repo);
                ClickRow(app, 2);
                var details = await WaitForDetailsAsync(app, AddSettingsPage);
                var flat = app.Capture();
                flat.Save(Phase, "03a-details-flat");

                Assert.Equal(2, Graph(app).SelectedIndex);
                AssertRowBackground(app, flat, 2, Palettes.Dark.Selection);
                Assert.Equal(
                    ["Commit details", AddSettingsPage, "The settings page lists the defaults.\nIt replaces the old notes."],
                    ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel"))[..3]);
                var body = (TextBlock)app.FindByAutomationId("DetailsBody");
                Assert.Equal(2, body.TextLayout.TextLines.Count);
                AssertDetailsField(app, "Author", "Test Author <author@example.com>");
                AssertDetailsField(app, "Date", "2026-01-01 12:10");
                AssertDetailsField(app, "SHA", sha);
                Assert.DoesNotContain("Committer", ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
                Assert.Equal(["2775228"], ParentLinks(app));
                Assert.Contains("Changed files (5)", ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
                Assert.Equal(
                    [
                        "M README.md",
                        "D notes.txt docs",
                        "R main-app.txt src renamed from src/app.txt",
                        "A defaults.txt src/settings",
                        "A page.txt src/settings",
                    ],
                    FileRows(app));
                AssertStatusLetterColours(app, flat, Palettes.Dark);

                app.Click(app.FindByAutomationId("TreeFilesButton"));
                app.Capture().Save(Phase, "03b-details-tree");
                Assert.Equal(
                    [
                        "docs",
                        "D notes.txt",
                        "src",
                        "settings",
                        "A defaults.txt",
                        "A page.txt",
                        "R main-app.txt renamed from src/app.txt",
                        "M README.md",
                    ],
                    FileRows(app));
                Assert.Equal(FileListMode.Tree, new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.FileList);
                return true;
            },
            cancellationToken);

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenGraphScenarioAsync(data, repo);
                ClickRow(app, 2);
                await WaitForDetailsAsync(app, AddSettingsPage);
                app.Capture().Save(Phase, "03c-tree-after-restart");
                Assert.Equal("docs", FileRows(app)[0]);
                return true;
            },
            cancellationToken);
    }

    [AvaloniaFact]
    public async Task Check_4_details_of_a_merge_and_of_a_stash()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        UseLightTheme(data);
        using var app = await OpenGraphScenarioAsync(data, repo);

        ClickRow(app, 7);
        await WaitForDetailsAsync(app, "Merge branch 'feature/login'");
        app.Capture().Save(Phase, "04a-merge-details");
        Assert.Equal(["10bcd42", "4a62ef9"], ParentLinks(app));
        Assert.Contains("Changed files (2)", ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal(["A login.txt src", "A rules.txt src/validation"], FileRows(app));

        app.Click(ParentLink(app, "4a62ef9"));
        await WaitForDetailsAsync(app, "Validate passwords");
        app.Capture().Save(Phase, "04b-parent-selected");
        Assert.Equal(9, Graph(app).SelectedIndex);

        ClickRow(app, 0);
        await WaitForDetailsAsync(app, "On main: Work in progress on README");
        app.Capture().Save(Phase, "04c-stash-details");
        AssertDetailsField(app, "Stash", "stash@{0}");
        Assert.Equal(["47e6ec7"], ParentLinks(app));
        Assert.Contains("Changed files (1)", ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal(["M README.md"], FileRows(app));
    }

    [AvaloniaFact]
    public async Task Check_5_left_panel_sections_folders_and_counts()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenGraphScenarioAsync(data, repo);

        var screenshot = app.Capture();
        screenshot.Save(Phase, "05a-left-panel");
        AssertGraphScenarioLeftPanel(app, screenshot, Palettes.Dark);

        app.Click(RefItem(app, "feature", index: 0));
        app.Capture().Save(Phase, "05b-feature-folder-open");
        var texts = LeftPanelTexts(app);
        var feature = texts.IndexOf("feature");
        Assert.Equal(["feature", "login", "search", "↑1"], texts[feature..(feature + 4)]);
        Assert.Equal("main", texts[feature + 4]);

        app.Click(SectionHeader(app, "Tags"));
        app.Capture().Save(Phase, "05c-tags-closed");
        texts = LeftPanelTexts(app);
        var tags = texts.IndexOf("Tags");
        Assert.Equal(["Tags", "2", "Stashes", "1"], texts[tags..(tags + 4)]);
        Assert.DoesNotContain("v0.1", texts);
    }

    [AvaloniaFact]
    public async Task Check_6_the_filter_narrows_the_list()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenGraphScenarioAsync(data, repo);
        var before = LeftPanelTexts(app);

        app.Click(app.Find<TextBox>("FilterBox"));
        app.Type("sea");
        app.Capture().Save(Phase, "06a-filter-sea");
        Assert.Equal(
            ["Local branches", "1", "feature", "search", "↑1", "Remotes", "1", "origin", "feature", "search", "Pull requests", "0", "Tags", "0", "Stashes", "0"],
            LeftPanelTexts(app));

        app.PressKey(Key.A, RawInputModifiers.Control);
        app.PressKey(Key.Back);
        app.Capture().Save(Phase, "06b-filter-cleared");
        Assert.Equal(before, LeftPanelTexts(app));
    }

    [AvaloniaFact]
    public async Task Check_7_a_ref_in_the_left_panel_selects_its_commit()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenGraphScenarioAsync(data, repo);

        app.Click(RefItem(app, "v0.1"));
        await WaitForDetailsAsync(app, "Add app skeleton");
        app.Capture().Save(Phase, "07a-tag-selected");
        Assert.Equal(11, Graph(app).SelectedIndex);

        app.Click(RefItem(app, "On main: Work in progress on README"));
        await WaitForDetailsAsync(app, "On main: Work in progress on README");
        app.Capture().Save(Phase, "07b-stash-selected");
        Assert.Equal(0, Graph(app).SelectedIndex);

        app.Click(RefItem(app, "main", index: 1));
        await WaitForDetailsAsync(app, "Fix typo in docs");
        var remote = app.Capture();
        remote.Save(Phase, "07c-remote-branch-selected");
        Assert.Equal(3, Graph(app).SelectedIndex);
        AssertBackground(remote, app.BoundsOf(RefItem(app, "main", index: 1)), Palettes.Dark.Selection, "the selected item in the left panel");
    }

    [AvaloniaFact]
    public async Task Check_8_the_100k_commit_repo_top_middle_and_bottom()
    {
        var large = await LargeHistory.GetAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [large]);
        await app.WaitUntilReadyAsync();
        var graph = Graph(app);
        await app.WaitForAsync(() => graph.Data is { IsComplete: true, Count: LargeHistory.CommitCount }, "all 100,000 commits", TimeSpan.FromSeconds(60));

        var top = app.Capture();
        top.Save(Phase, "08a-large-top");
        Assert.Equal(0, graph.FirstVisibleRow);
        AssertLargeRows(app, top);
        Assert.Equal(["main"], graph.LabelsInRow(0).Select(label => label.FullText));
        Assert.Equal(RefLabelKind.CurrentBranch, graph.LabelsInRow(0)[0].Kind);
        Assert.Equal(["feature"], graph.LabelsInRow(1).Select(label => label.FullText));

        app.Wheel(app.BoundsOf(graph).Center, -10);
        var wheel = app.Capture();
        wheel.Save(Phase, "08b-large-wheel");
        Assert.Equal(30, graph.FirstVisibleRow);
        Assert.Equal("Main commit 99970", graph.Data!.CommitAt(30).Subject);
        AssertLargeRows(app, wheel);

        app.Click(RefItem(app, LargeHistory.MiddleTag));
        await WaitForDetailsAsync(app, LargeHistory.SubjectOf(50_000));
        var middle = app.Capture();
        middle.Save(Phase, "08c-large-middle");
        Assert.Equal(50_000, graph.SelectedIndex);
        Assert.InRange(graph.RowBounds(50_000).Center.Y - (graph.ViewportHeight / 2), -1.5 * CommitGraphControl.RowHeight, 1.5 * CommitGraphControl.RowHeight);
        Assert.Equal([LargeHistory.MiddleTag], graph.LabelsInRow(50_000).Select(label => label.FullText));
        AssertLargeRows(app, middle);

        ClickRow(app, graph.FirstVisibleRow + 1);
        app.PressKey(Key.End);

        // The mouse goes to an empty spot, as in the real-window pass, where a tooltip over a
        // message cut short would otherwise cover part of the picture.
        app.MoveMouse(new Point(130, 687));
        await WaitForDetailsAsync(app, LargeHistory.SubjectOf(1));
        var end = app.Capture();
        end.Save(Phase, "08d-large-end");
        Assert.Equal(LargeHistory.CommitCount - 1, graph.SelectedIndex);
        Assert.Equal(graph.MaxScrollOffset, graph.ScrollOffset, 0.5);
        Assert.Equal(graph.ViewportHeight, graph.RowBounds(LargeHistory.CommitCount - 1).Bottom, 0.5);
        Assert.Equal("Main commit 1", graph.Data.CommitAt(graph.SelectedIndex).Subject);
        AssertLargeRows(app, end);

        app.PressKey(Key.Home);
        app.MoveMouse(new Point(130, 687));
        await WaitForDetailsAsync(app, LargeHistory.SubjectOf(LargeHistory.CommitCount));
        var home = app.Capture();
        home.Save(Phase, "08e-large-home");
        Assert.Equal(0, graph.SelectedIndex);
        Assert.Equal(0, graph.ScrollOffset);
        AssertLargeRows(app, home);

        var left = LeftPanelTexts(app);
        Assert.Equal("2", left[left.IndexOf("Local branches") + 1]);
        Assert.Equal("1", left[left.IndexOf("Tags") + 1]);
    }

    [AvaloniaFact]
    public async Task Check_9_Q1_time_to_the_first_graph_and_frame_times()
    {
        var large = await LargeHistory.GetAsync();
        using var data = new TempDirectory("data");
        using (var app = ShellDriver.StartWithTabs(data.Path, [large]))
        {
            await app.WaitUntilReadyAsync();
            var graph = Graph(app);
            for (var notch = 0; notch < 200; notch++)
            {
                app.Wheel(app.BoundsOf(graph).Center, -1);
                app.Capture();
            }

            for (var notch = 0; notch < 200; notch++)
            {
                app.Wheel(app.BoundsOf(graph).Center, 1);
                app.Capture();
            }
        }

        var log = string.Join('\n', Directory.GetFiles(Path.Combine(data.Path, "logs")).Select(File.ReadAllText));
        Assert.Matches(@"large-history-v1: first graph rows drawn after \d+ ms", log);
        Assert.Matches(@"large-history-v1: loaded 100000 commits in \d+ ms", log);
        Assert.Matches(@"large-history-v1: graph frames: \d+ drawn, 95th percentile [\d.]+ ms, longest [\d.]+ ms", log);

        // An indication only (D50): the numbers are judged in the real-window pass.
        TestContext.Current.SendDiagnosticMessage("Q1 (headless, indication): " + string.Join(" | ", log.Split('\n').Where(line => line.Contains("large-history-v1:", StringComparison.Ordinal))));
    }

    [AvaloniaFact]
    public async Task Check_18_changes_made_in_a_terminal_appear()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenGraphScenarioAsync(data, repo);
        var graph = Graph(app);
        ClickRow(app, 2);
        await WaitForDetailsAsync(app, AddSettingsPage);

        await repo.CommitFileAsync("terminal.txt", "from a terminal\n", "Commit from a terminal");
        await app.WaitForAsync(() => graph.Data is { IsComplete: true, Count: 14 }, "the commit made outside the app", TimeSpan.FromSeconds(5));

        // The file is written before it is committed: with the working tree watched (phase 2,
        // D71) a working-changes row can show in between, until the next refresh.
        await app.WaitForAsync(() => !graph.ShowsWorkingRow, "no working-changes row", TimeSpan.FromSeconds(5));
        var appeared = app.Capture();
        appeared.Save(Phase, "18a-terminal-commit");
        Assert.Equal("Commit from a terminal", graph.Data!.CommitAt(0).Subject);
        Assert.Equal("2026-01-01 12:13", graph.Data.CommitAt(0).AuthorDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(["main"], graph.LabelsInRow(0).Select(label => label.FullText));
        Assert.Equal(0, graph.Data.RowAt(0).NodeLane);
        Assert.Equal(Core.Git.CommitKind.Stash, graph.Data.CommitAt(1).Kind);
        Assert.Equal(3, graph.SelectedIndex);
        Assert.Equal(AddSettingsPage, graph.Data.CommitAt(3).Subject);
        Assert.Equal(AddSettingsPage, ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel"))[1]);
        var left = LeftPanelTexts(app);
        var main = left.IndexOf("main");
        Assert.Equal(["main", "↑2", "↓1"], left[main..(main + 3)]);

        await repo.DetachAsync("v0.2");
        await app.WaitForAsync(() => app.Find<TextBlock>("CurrentBranch").Text == "Detached at 2775228", "the status bar to show the detached HEAD", TimeSpan.FromSeconds(5));
        await app.WaitForAsync(() => graph.Data is { IsComplete: true } data && data.IndexOf(data.Refs.Head.Sha!) >= 0, "the graph to follow");
        await app.WaitForAsync(() => !graph.ShowsWorkingRow, "no working-changes row", TimeSpan.FromSeconds(5));
        var detached = app.Capture();
        detached.Save(Phase, "18b-detached");
        var bump = graph.Data!.IndexOf(graph.Data.Refs.Head.Sha!);
        Assert.Equal("Bump version", graph.Data.CommitAt(bump).Subject);
        Assert.Equal(RefLabelKind.DetachedHead, graph.LabelsInRow(bump)[0].Kind);
        Assert.Equal("HEAD", graph.LabelsInRow(bump)[0].FullText);
        Assert.DoesNotContain(graph.LabelsInRow(graph.Data.IndexOf((await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim())), label => label.Kind == RefLabelKind.CurrentBranch);
        AssertNotAccent(app, detached, RefItem(app, "main", index: 0));
    }

    // ----- helpers -----

    private static async Task<ShellDriver> OpenGraphScenarioAsync(TempDirectory data, TempRepo repo, int width = 1100, int height = 700)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height);
        try
        {
            await app.WaitUntilReadyAsync();
            var graph = Graph(app);
            await app.WaitForAsync(() => graph.Data is { IsComplete: true, Count: 13 }, "the graph scenario's 13 rows");
            app.MoveMouse(new Point(130, height - 13));
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static void UseLightTheme(TempDirectory data) =>
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });

    private static CommitGraphControl Graph(ShellDriver app) => app.Find<CommitGraphControl>("GraphRows");

    private static Point RowPoint(ShellDriver app, int row, double x = 250)
    {
        var graph = Graph(app);
        var bounds = graph.RowBounds(row);
        var origin = app.BoundsOf(graph).TopLeft;
        return new Point(origin.X + x, origin.Y + bounds.Center.Y);
    }

    private static void ClickRow(ShellDriver app, int row) => app.Click(RowPoint(app, row));

    private static async Task<IReadOnlyList<string>> WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == subject && Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "DetailsSubject"),
            $"the details of \"{subject}\"");
        app.Settle();
        return ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel"));
    }

    private static void AssertDetailsField(ShellDriver app, string label, string value)
    {
        var texts = ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel"));
        var index = texts.IndexOf(label);
        Assert.True(index >= 0, $"The details have no \"{label}\" row: {string.Join(" | ", texts)}");
        Assert.Equal(value, texts[index + 1]);
    }

    private static List<string> ParentLinks(ShellDriver app) =>
        [.. AllByAutomationId(app, "ParentLink").OrderBy(link => app.BoundsOf(link).X).Select(link => Avalonia.Automation.AutomationProperties.GetName(link) ?? string.Empty)];

    private static Control ParentLink(ShellDriver app, string shortSha) =>
        AllByAutomationId(app, "ParentLink").Single(link => Avalonia.Automation.AutomationProperties.GetName(link) == shortSha);

    /// <summary>The changed-file rows as their texts joined by spaces, top to bottom.</summary>
    private static List<string> FileRows(ShellDriver app)
    {
        var list = (ItemsControl)app.FindByAutomationId("FileList");
        return
        [
            .. list.GetRealizedContainers()
                .Where(item => item.IsEffectivelyVisible)
                .OrderBy(item => app.BoundsOf(item).Y)
                .Select(item => string.Join(' ', ShellExpectations.TextsIn(item))),
        ];
    }

    private static void AssertStatusLetterColours(ShellDriver app, Screenshot screenshot, Palettes.Palette palette)
    {
        var expected = new Dictionary<string, Color> { ["A"] = palette.Success, ["M"] = palette.Warning, ["D"] = palette.Danger, ["R"] = palette.Accent };
        foreach (var letter in app.FindByAutomationId("FileList").GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text is "A" or "M" or "D" or "R"))
        {
            Assert.True(
                ContainsColour(screenshot, app.BoundsOf(letter), expected[letter.Text!]),
                $"The status letter {letter.Text} is not drawn in {expected[letter.Text!]}.");
        }
    }

    /// <summary>
    /// The left panel's texts in reading order, without the filter's hint. The ahead and behind
    /// counts are drawn as an arrow icon and a number; they are written here as the report writes
    /// them, "↑1" and "↓1", from the name of the text that holds the number.
    /// </summary>
    private static List<string> LeftPanelTexts(ShellDriver app)
    {
        var panel = app.FindByAutomationId("LeftPanel");
        var items = panel.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text) && text.Text != "Filter")
            .Select(text => (Text: text.Name switch
            {
                "AheadCount" => "↑" + text.Text,
                "BehindCount" => "↓" + text.Text,
                _ => text.Text!,
            }, Area: app.BoundsOf(text)))
            .OrderBy(item => item.Area.Center.Y)
            .ToList();

        var texts = new List<string>();
        for (var start = 0; start < items.Count;)
        {
            var end = start + 1;
            while (end < items.Count && items[end].Area.Center.Y - items[end - 1].Area.Center.Y < 6)
            {
                end++;
            }

            texts.AddRange(items[start..end].OrderBy(item => item.Area.X).Select(item => item.Text));
            start = end;
        }

        return texts;
    }

    private static Control RefItem(ShellDriver app, string name, int index = 0) =>
        AllByAutomationId(app, "RefItem")
            .Where(item => Avalonia.Automation.AutomationProperties.GetName(item) == name)
            .OrderBy(item => app.BoundsOf(item).Y)
            .ElementAt(index);

    private static Control SectionHeader(ShellDriver app, string title) =>
        AllByAutomationId(app, "Section").Single(item => Avalonia.Automation.AutomationProperties.GetName(item) == title);

    private static IEnumerable<Control> AllByAutomationId(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId);

    private static (double Left, double Right) PanelWidthsOf(ShellDriver app) =>
        (app.BoundsOf(app.FindByAutomationId("LeftPanel")).Width, app.BoundsOf(app.FindByAutomationId("RightPanel")).Width);

    private static void AssertRowBackground(ShellDriver app, Screenshot screenshot, int row, Color colour)
    {
        // The far right of the Message column, where no text reaches in this scenario.
        var graph = Graph(app);
        var x = app.BoundsOf(graph).X + graph.Columns.Message.Right - 4;
        var y = RowPoint(app, row).Y - 9;
        ShellExpectations.AssertColour(colour, screenshot, new Point(x, y), $"the background of row {row}");
    }

    private static void AssertBackground(Screenshot screenshot, Rect area, Color colour, string what) =>
        ShellExpectations.AssertColour(colour, screenshot, new Point(area.Right - 3, area.Top + 3), what);

    private static void AssertNotAccent(ShellDriver app, Screenshot screenshot, Control item) =>
        Assert.NotEqual(Palettes.Dark.Accent, TextColour(item, "main"));

    /// <summary>
    /// The colour a small text is drawn in. Text of 12.5 pixels is anti-aliased so thinly that no
    /// pixel of it need reach the exact colour, so the brush it is drawn with is read instead.
    /// </summary>
    private static Color TextColour(Control item, string text)
    {
        var block = item.GetVisualDescendants().OfType<TextBlock>().First(candidate => candidate.Text == text);
        return Assert.IsAssignableFrom<ISolidColorBrush>(block.Foreground).Color;
    }

    /// <summary>
    /// Asserts a stash's ring: somewhere between 3 and 5 pixels right of the centre (the ring's
    /// width) the pixel has the lane's colour, unblended by anti-aliasing.
    /// </summary>
    private static void AssertRing(Screenshot screenshot, Point centre, Color colour, string what)
    {
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The {what} is not {colour} anywhere from 3 to 5 pixels right of its centre: {string.Join(", ", samples)}.");
    }

    /// <summary>Asserts the graph scenario's table: labels, node lanes, colours and shapes, texts and columns.</summary>
    private static void AssertGraphScenario(ShellDriver app, Screenshot screenshot, Palettes.Palette palette, bool allColumns)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        Assert.Equal(Rows.Count, data.Count);
        Assert.Equal(3, data.MaxLaneCount);

        // Columns and headers.
        Assert.Equal(130, graph.Columns.Refs.Width);
        Assert.Equal(64, graph.Columns.Graph.Width);
        Assert.Equal(allColumns, graph.Columns.ShowsAuthor);
        Assert.Equal(allColumns, graph.Columns.ShowsSha);
        Assert.True(graph.Columns.ShowsDate);
        Assert.Equal(
            allColumns ? ["Branch / Tag", "Graph", "Message", "Author", "Date", "SHA"] : ["Branch / Tag", "Graph", "Message", "Date"],
            ShellExpectations.TextsIn(app.Find<Border>("ColumnHeaders")));

        var origin = app.BoundsOf(graph).TopLeft;
        for (var row = 0; row < Rows.Count; row++)
        {
            var expected = Rows[row];
            var commit = data.CommitAt(row);
            var layout = data.RowAt(row);
            Assert.Equal(expected.Message, commit.Subject);
            Assert.StartsWith(expected.Sha, commit.Sha, StringComparison.Ordinal);
            Assert.Equal(expected.Date, ((CommitGraphControl)graph).Dates!.Format(commit.AuthorDate));
            Assert.Equal(expected.Lane, layout.NodeLane);
            Assert.Equal(expected.Color, layout.NodeColor);
            Assert.Equal(expected.Node == Node.Merge, commit.Parents.Count > 1);

            var labels = graph.LabelsInRow(row);
            if (expected.Label is null)
            {
                Assert.Empty(labels);
            }
            else
            {
                var label = Assert.Single(labels);
                Assert.Equal(expected.Label, label.FullText);
                Assert.Equal(expected.LabelKind, label.Kind);
                Assert.True(label.Text == expected.Label || (label.Text.EndsWith('…') && expected.Label.StartsWith(label.Text[..^1], StringComparison.Ordinal)), $"Row {row}'s label shows \"{label.Text}\".");
            }

            // The node, in its lane's colour: the middle of a filled circle, or the ring of a stash.
            var centre = new Point(origin.X + graph.LaneCenterX(expected.Lane), origin.Y + graph.RowBounds(row).Center.Y);
            if (expected.Node == Node.Stash)
            {
                ShellExpectations.AssertColour(palette.Window, screenshot, centre, $"the inside of row {row}'s stash ring");
                AssertRing(screenshot, centre, palette.Lane(expected.Color), $"row {row}'s stash ring");
            }
            else
            {
                ShellExpectations.AssertColour(palette.Lane(expected.Color), screenshot, centre, $"row {row}'s node");
            }
        }
    }

    /// <summary>Asserts the left panel of the graph scenario as the report's tree shows it.</summary>
    private static void AssertGraphScenarioLeftPanel(ShellDriver app, Screenshot screenshot, Palettes.Palette palette)
    {
        var texts = LeftPanelTexts(app);
        Assert.Equal(
            [
                "Local branches", "4", "bugfix", "feature", "main", "↑1", "↓1",
                "Remotes", "2", "origin", "feature", "main",
                "Pull requests", "0",
                "Tags", "2", "v0.1", "v0.2",
                "Stashes", "1", "On main: Work in progress on README",
            ],
            texts);
        Assert.Equal(palette.Accent, TextColour(RefItem(app, "main", index: 0), "main"));
        Assert.Equal(palette.Primary, TextColour(RefItem(app, "main", index: 1), "main"));
    }

    /// <summary>Asserts every row in view of the 100k-commit repo: its subject, lane and node colour.</summary>
    private static void AssertLargeRows(ShellDriver app, Screenshot screenshot)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        var origin = app.BoundsOf(graph).TopLeft;
        var first = graph.FirstVisibleRow;
        var last = first;
        while (last + 1 < data.Count && graph.RowBounds(last + 1).Top < graph.ViewportHeight)
        {
            last++;
        }

        for (var row = first; row <= last; row++)
        {
            var number = LargeHistory.CommitInRow(row);
            Assert.Equal(LargeHistory.SubjectOf(number), data.CommitAt(row).Subject);
            var lane = LargeHistory.LaneOf(number);
            Assert.Equal(lane, data.RowAt(row).NodeLane);
            var centreY = origin.Y + graph.RowBounds(row).Center.Y;
            if (centreY < origin.Y + 6 || centreY > origin.Y + graph.ViewportHeight - 6)
            {
                continue;
            }

            var colour = lane == 0 ? 0 : ((LargeHistory.CommitCount - MergeAbove(number)) / LargeHistory.CycleLength) + 1;
            Assert.Equal(colour, data.RowAt(row).NodeColor);
            var palette = app.Window.ActualThemeVariant == ThemeVariant.Light ? Palettes.Light : Palettes.Dark;
            ShellExpectations.AssertColour(palette.Lane(colour), screenshot, new Point(origin.X + graph.LaneCenterX(lane), centreY), $"the node of row {row}");
        }
    }

    /// <summary>The number of the merge commit just above a feature commit.</summary>
    private static int MergeAbove(int number) => ((number / LargeHistory.CycleLength) + 1) * LargeHistory.CycleLength;
}
