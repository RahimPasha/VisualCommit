using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless UI tests of the left panel and the commit details panel on their own, each in a
/// window the size the panel has at 1100×700, with the app's theme. What the panels look like in
/// the whole window is asserted by the scripted walk-through in VisualCommit.VisualTests.
/// </summary>
public class PanelViewTests
{
    /// <summary>The main area's height at 1100×700: the window less the tab strip, toolbar and status bar.</summary>
    private const double PanelHeight = 700 - 36 - 52 - 26;

    [AvaloniaFact]
    public void The_left_panel_of_a_tab_without_a_repository_shows_phase_0s_sections()
    {
        using var host = Host.Show(new LeftPanelView { DataContext = new LeftPanelViewModel() }, 260);

        Assert.Equal(
            ["Local branches", "0", "Remotes", "0", "Pull requests", "0", "Tags", "0", "Stashes", "0"],
            host.TextsInReadingOrder().Where(text => text != "Filter"));
        var filter = host.Find<TextBox>("FilterBox");
        Assert.Equal("Filter", filter.PlaceholderText);
        Assert.Equal(new Thickness(8, 8, 8, 6), filter.Margin);
        var sections = host.WithAutomationId("Section");
        Assert.Equal(5, sections.Count);
        Assert.All(sections, section => Assert.Equal(30, section.Bounds.Height));
        Assert.True(host.BoundsOf(filter).Bottom <= host.BoundsOf(host.WithAutomationId("RefList").Single()).Y + 1);
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
    }

    [AvaloniaFact]
    public void Typing_in_the_filter_box_filters_the_list()
    {
        var panel = new LeftPanelViewModel();
        panel.Update(LeftPanelViewModelTests.GraphRefs());
        using var host = Host.Show(new LeftPanelView { DataContext = panel }, 260);

        host.Click(host.Find<TextBox>("FilterBox"));
        host.Window.KeyTextInput("sea");
        host.Settle();

        Assert.Equal("sea", panel.FilterText);
        Assert.Equal(
            ["Local branches", "1", "feature", "search", "1", "Remotes", "1", "origin", "feature", "search", "Pull requests", "0", "Tags", "0", "Stashes", "0"],
            host.TextsInReadingOrder().Where(text => text != "sea"));
    }

    [AvaloniaFact]
    public void The_left_panel_of_the_graph_scenario_shows_its_tree_with_items_26_high()
    {
        var panel = new LeftPanelViewModel();
        panel.Update(LeftPanelViewModelTests.GraphRefs());
        using var host = Host.Show(new LeftPanelView { DataContext = panel }, 260);

        Assert.Equal(
            [
                "Local branches", "4", "bugfix", "feature", "main", "1", "1",
                "Remotes", "2", "origin", "feature", "main",
                "Pull requests", "0",
                "Tags", "2", "v0.1", "v0.2",
                "Stashes", "1", "On main: Work in progress on README",
            ],
            host.TextsInReadingOrder().Where(text => text != "Filter"));
        var items = host.WithAutomationId("RefItem");
        Assert.All(items, item => Assert.Equal(26, item.Bounds.Height));

        // Indented 14 per level: the names of a folder at level 1 and one at level 2.
        var names = host.Window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name == "RefName").ToList();
        var bugfix = host.BoundsOf(names.Single(text => text.Text == "bugfix"));
        var remoteFeature = host.BoundsOf(names.Where(text => text.Text == "feature").Last());
        Assert.Equal(14, remoteFeature.X - bugfix.X, 0.5);

        // The branch HEAD is on is drawn in the accent colour.
        var main = names.First(text => text.Text == "main");
        Assert.Equal(Brush("VcAccentBrush"), main.Foreground);

        // Only the stash's message is too long for the panel: it ends in "…", whole in its tooltip.
        const string message = "On main: Work in progress on README";
        Assert.Equal([$"\"{message}\" is shortened with an ellipsis."], LayoutAudit.FindClippedText(host.Window));
        Assert.Equal(message, ToolTip.GetTip(names.Single(text => text.Text == message)));
        Assert.Equal("origin/feature", ToolTip.GetTip(names.Where(text => text.Text == "feature").Last()));
    }

    [AvaloniaFact]
    public void Clicking_a_ref_raises_RefActivated_and_gives_it_the_selection_background()
    {
        var panel = new LeftPanelViewModel();
        panel.Update(LeftPanelViewModelTests.GraphRefs());
        var activated = new List<string>();
        panel.RefActivated += (_, sha) => activated.Add(sha);
        using var host = Host.Show(new LeftPanelView { DataContext = panel }, 260);
        var tag = host.WithAutomationId("RefItem").Single(item => Avalonia.Automation.AutomationProperties.GetName(item) == "v0.1");

        host.Click(tag);
        host.Window.MouseMove(new Point(1, 1));
        host.Settle();

        Assert.Equal([LeftPanelViewModelTests.Sha('b')], activated);
        Assert.Equal(Brush("VcSelectionBrush"), ((Button)tag).Background);
        var other = host.WithAutomationId("RefItem").Single(item => Avalonia.Automation.AutomationProperties.GetName(item) == "v0.2");
        Assert.NotEqual(Brush("VcSelectionBrush"), ((Button)other).Background);
    }

    [AvaloniaFact]
    public void Clicking_a_folder_opens_it()
    {
        var panel = new LeftPanelViewModel();
        panel.Update(LeftPanelViewModelTests.GraphRefs());
        using var host = Host.Show(new LeftPanelView { DataContext = panel }, 260);

        host.Click(host.WithAutomationId("RefItem").First(item => Avalonia.Automation.AutomationProperties.GetName(item) == "feature"));

        Assert.Equal(
            ["Local branches", "4", "bugfix", "feature", "login", "search", "1", "main", "1", "1"],
            host.TextsInReadingOrder().Where(text => text != "Filter").Take(10));
    }

    [AvaloniaFact]
    public void The_details_panel_without_a_commit_shows_phase_0s_placeholder()
    {
        var details = new CommitDetailsViewModel(null, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        using var host = Host.Show(new RightPanelView { DataContext = details }, 400);

        Assert.Equal(["Commit details", "Select a commit to see its details."], host.TextsInReadingOrder());
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
    }

    [AvaloniaFact]
    public async Task The_details_panel_shows_a_commit_and_a_click_on_a_parent_raises_ParentActivated()
    {
        var first = new string('1', 40);
        var second = new string('2', 40);
        var repository = new CommitDetailsViewModelTests.FakeRepository(sha => Task.FromResult(CommitDetailsViewModelTests.Details(sha) with
        {
            Subject = "Merge branch 'feature/login'",
            Parents = [first, second],
            Files = [new ChangedFile("src/login.txt", FileChangeKind.Added), new ChangedFile("src/validation/rules.txt", FileChangeKind.Added)],
        }));
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        var activated = new List<string>();
        details.ParentActivated += (_, sha) => activated.Add(sha);
        using var host = Host.Show(new RightPanelView { DataContext = details }, 400);

        await details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);
        host.Settle();

        Assert.Equal(
            [
                "Commit details",
                "Merge branch 'feature/login'",
                "Author", "Test Author <author@example.com>",
                "Date", "2026-01-01 12:00",
                "SHA", new string('a', 40),
                "Parents", "1111111", "2222222",
                "Changed files (2)", "Flat", "Tree",
                "A", "login.txt", "src",
                "A", "rules.txt", "src/validation",
            ],
            host.TextsInReadingOrder());
        Assert.Equal(Brush("VcSuccessBrush"), host.Find<TextBlock>("FileStatus").Foreground);
        Assert.Equal(Brush("VcSelectionBrush"), host.Find<Button>("FlatFilesButton").Background);
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));

        var links = host.WithAutomationId("ParentLink");
        Assert.Equal(["1111111", "2222222"], links.Select(link => Avalonia.Automation.AutomationProperties.GetName(link)));
        host.Click(links[1]);

        Assert.Equal([second], activated);
    }

    [AvaloniaFact]
    public async Task Clicking_Tree_shows_the_files_in_folders()
    {
        var repository = new CommitDetailsViewModelTests.FakeRepository(sha => Task.FromResult(CommitDetailsViewModelTests.Details(sha) with
        {
            Files = [new ChangedFile("README.md", FileChangeKind.Modified), new ChangedFile("src/main-app.txt", FileChangeKind.Renamed, "src/app.txt")],
        }));
        var settings = new MainWindowViewModelTests.FakeSettings();
        var details = new CommitDetailsViewModel(repository, settings, new DateDisplay(TimeZoneInfo.Utc));
        using var host = Host.Show(new RightPanelView { DataContext = details }, 400);
        await details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);
        host.Settle();

        host.Click(host.Find<Button>("TreeFilesButton"));
        host.Window.MouseMove(new Point(1, 1));
        host.Settle();

        Assert.Equal(Core.Settings.FileListMode.Tree, settings.Current.FileList);
        Assert.Equal(Brush("VcSelectionBrush"), host.Find<Button>("TreeFilesButton").Background);
        var fileList = host.WithAutomationId("FileList").Single();
        Assert.Equal(
            ["src", "R", "main-app.txt", "renamed from src/app.txt", "M", "README.md"],
            Host.TextsIn(fileList));
        Assert.All(
            fileList.GetVisualDescendants().OfType<ContentPresenter>().Where(row => row.GetVisualParent() is VirtualizingStackPanel),
            row => Assert.Equal(24, row.Bounds.Height));
    }

    [AvaloniaFact]
    public async Task A_long_message_scrolls_and_leaves_the_file_list_its_share_of_the_panel()
    {
        var body = string.Join('\n', Enumerable.Range(1, 80).Select(line => $"Line {line} of a long explanation."));
        var repository = new CommitDetailsViewModelTests.FakeRepository(sha => Task.FromResult(CommitDetailsViewModelTests.Details(sha) with { Body = body }));
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        using var host = Host.Show(new RightPanelView { DataContext = details }, 400);

        await details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);
        host.Settle();

        // Below the 36-high header, the subject to the parents take at most 60% and scroll.
        var header = host.Find<ScrollViewer>("DetailsHeaderScroller");
        Assert.True(header.Bounds.Height <= ((PanelHeight - 36) * 0.6) + 0.5, $"The top part is {header.Bounds.Height} high.");
        Assert.True(header.Extent.Height > header.Viewport.Height, "The top part does not scroll.");
        var files = host.WithAutomationId("FileList").Single();
        Assert.True(host.BoundsOf(files).Height > 150, $"The file list is {files.Bounds.Height} high.");
        Assert.Equal(PanelHeight, host.BoundsOf(files).Bottom, 0.5);
    }

    /// <summary>
    /// The details panel as the main window shows it at 1100×700: 400 wide, at the main window's
    /// font size. A commit whose committer and commit date differ from its author's shows the
    /// "Commit date" row, the widest label, and still its whole SHA on one line, in both themes.
    /// Phase 4's pictures a3d, b8c and c11j showed the last character on a second line.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_whole_sha_stays_on_one_line_beside_the_commit_date_row(string theme)
    {
        var repository = new CommitDetailsViewModelTests.FakeRepository(sha => Task.FromResult(CommitDetailsViewModelTests.Details(sha) with
        {
            CommitterName = "Other Committer",
            CommitDate = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero),
        }));
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        var fontSize = MainWindowFontSize();
        using var host = Host.Show(new RightPanelView { DataContext = details }, 400, fontSize, theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);

        // a3d's SHA, whose last "f" wrapped; forty 4s, the widest hex digit (321 wide at 12.5
        // against 287 of room); forty a's (282 wide), which fit at the panel's size but would not
        // at Avalonia's default of 14, so that the size the fit starts from is the panel's.
        foreach (var (full, fits) in new[] { ("14e607bf248c8917ab93680c48cba392f60546ff", false), (new string('4', 40), false), (new string('a', 40), true) })
        {
            await details.ShowAsync(full, cancellationToken: TestContext.Current.CancellationToken);
            host.Settle();

            var sha = host.Find<SelectableTextBlock>("DetailsSha");
            Assert.Equal(full, sha.Text);
            Assert.Contains("Commit date", host.TextsInReadingOrder());
            Assert.True(sha.TextLayout.TextLines.Count == 1, $"{full} takes {sha.TextLayout.TextLines.Count} lines at size {sha.FontSize} in {sha.Bounds.Width:F1}.");
            Assert.Empty(LayoutAudit.FindClippedText(host.Window));

            // Only a SHA that would wrap is drawn smaller; one that fits keeps the panel's size,
            // so every picture whose SHA fitted is drawn as before.
            Assert.Equal(!fits, sha.IsSet(Avalonia.Controls.Documents.TextElement.FontSizeProperty));
            Assert.True(fits ? sha.FontSize == fontSize : sha.FontSize < fontSize, $"{full} is drawn at {sha.FontSize}; the panel's size is {fontSize}.");
        }
    }

    /// <summary>The font size the main window gives everything in it, the details panel included.</summary>
    private static double MainWindowFontSize()
    {
        var window = new MainWindow();
        try
        {
            return window.FontSize;
        }
        finally
        {
            window.Close();
        }
    }

    private static IBrush? Brush(string key) =>
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>A panel view shown alone in a headless window.</summary>
    private sealed class Host : IDisposable
    {
        private Host(Window window)
        {
            Window = window;
        }

        public Window Window { get; }

        /// <param name="fontSize">The window's font size; null: Avalonia's default, not the main window's.</param>
        /// <param name="theme">The window's theme; null: the application's.</param>
        public static Host Show(Control view, double width, double? fontSize = null, ThemeVariant? theme = null)
        {
            var window = new Window { Width = width, Height = PanelHeight, Content = view };
            if (fontSize is { } size)
            {
                window.FontSize = size;
            }

            if (theme is not null)
            {
                // On the window, not the application: other tests on the same UI thread may
                // change the application's theme.
                window.RequestedThemeVariant = theme;
            }

            window.Show();
            var host = new Host(window);
            host.Settle();
            return host;
        }

        public void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public T Find<T>(string name)
            where T : Control =>
            Window.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

        public List<Control> WithAutomationId(string automationId) =>
            Window.GetVisualDescendants().OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId)
                .ToList();

        public Rect BoundsOf(Visual visual) =>
            new(visual.TranslatePoint(default, Window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);

        public void Click(Visual visual)
        {
            var centre = BoundsOf(visual).Center;
            Window.MouseMove(centre);
            Window.MouseDown(centre, MouseButton.Left);
            Window.MouseUp(centre, MouseButton.Left);
            Settle();
        }

        public List<string> TextsInReadingOrder() => TextsIn(Window);

        /// <summary>The visible texts inside a control, top to bottom, then left to right (as the visual tests read them).</summary>
        public static List<string> TextsIn(Visual region)
        {
            var items = region.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
                .Select(text => (Text: text.Text!, Area: new Rect(text.TranslatePoint(default, region) ?? default, text.Bounds.Size)))
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

        public void Dispose() => Window.Close();
    }
}
