using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Git;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase0;

/// <summary>
/// The expected shell, as written in "What the shell must show" in
/// docs/test-reports/phase-0.md. The numbers and colours here are copied from that report, not
/// from the app's theme files, so the two are checked against each other.
/// </summary>
public static class ShellExpectations
{
    public const double TabStripHeight = 36;
    public const double ToolbarHeight = 52;
    public const double StatusBarHeight = 26;
    public const double LeftPanelWidth = 260;
    public const double RightPanelWidth = 400;
    public const double SizeTolerance = 2;

    public static readonly string[] ToolbarLabels =
        ["Undo", "Redo", "Fetch", "Pull", "Push", "Branch", "Stash", "Pop", "Search", "Theme"];

    public static readonly string[] LeftPanelSections =
        ["Local branches", "Remotes", "Pull requests", "Tags", "Stashes"];

    public static readonly string[] GraphColumns =
        ["Branch / Tag", "Graph", "Message", "Author", "Date", "SHA"];

    /// <summary>The colours of one theme, from the report's colour table.</summary>
    public sealed record Palette(Color Chrome, Color Panel, Color Graph, Color Text, Color Accent);

    public static Palette Dark { get; } = new(
        Color.Parse("#20242C"), Color.Parse("#1A1D24"), Color.Parse("#14161B"), Color.Parse("#E4E7EC"), Color.Parse("#7C8CFF"));

    public static Palette Light { get; } = new(
        Color.Parse("#EBEDF1"), Color.Parse("#F5F6F8"), Color.Parse("#FFFFFF"), Color.Parse("#1B1F27"), Color.Parse("#4353D8"));

    public static Palette PaletteOf(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;

    /// <summary>
    /// Asserts that the window shows the whole expected shell in the given theme, with the left
    /// and right panels at the given widths: positions and sizes of the regions, their text,
    /// which buttons are enabled, the git version, the colours in the screenshot, and that no
    /// text is cut off.
    /// </summary>
    public static async Task AssertShellAsync(
        ShellDriver app,
        Screenshot screenshot,
        AppTheme theme,
        double leftPanelWidth = LeftPanelWidth,
        double rightPanelWidth = RightPanelWidth)
    {
        var width = app.Window.ClientSize.Width;
        var height = app.Window.ClientSize.Height;
        Assert.Equal(width, screenshot.Width);
        Assert.Equal(height, screenshot.Height);

        // Regions.
        var mainTop = TabStripHeight + ToolbarHeight;
        var mainHeight = height - mainTop - StatusBarHeight;
        var graphWidth = width - leftPanelWidth - rightPanelWidth;
        var tabs = AssertRegion(app, "RepoTabs", new Rect(0, 0, width, TabStripHeight));
        var toolbar = AssertRegion(app, "Toolbar", new Rect(0, TabStripHeight, width, ToolbarHeight));
        var left = AssertRegion(app, "LeftPanel", new Rect(0, mainTop, leftPanelWidth, mainHeight));
        var graph = AssertRegion(app, "CommitGraph", new Rect(leftPanelWidth, mainTop, graphWidth, mainHeight));
        var right = AssertRegion(app, "RightPanel", new Rect(width - rightPanelWidth, mainTop, rightPanelWidth, mainHeight));
        var status = AssertRegion(app, "StatusBar", new Rect(0, height - StatusBarHeight, width, StatusBarHeight));

        // Repo tabs: the placeholder tab with its accent line, then the add button.
        Assert.Contains("No repository", TextsIn(tabs));
        var addButton = app.Find<Button>("AddRepoButton");
        Assert.True(app.BoundsOf(addButton).X > app.BoundsOf(app.Find<Border>("PlaceholderTab")).Right - 1);

        // Toolbar: the buttons in order, and only Theme enabled.
        var buttons = toolbar.GetVisualDescendants().OfType<ToolbarButton>()
            .OrderBy(button => app.BoundsOf(button).X)
            .ToList();
        Assert.Equal(ToolbarLabels, buttons.Select(button => button.Label));
        Assert.Equal(["Theme"], buttons.Where(button => button.IsEffectivelyEnabled).Select(button => button.Label));
        Assert.True(app.BoundsOf(buttons[^1]).Right > width - 20, "Search and Theme sit at the right end of the toolbar.");
        Assert.True(app.BoundsOf(buttons[7]).Right < app.BoundsOf(buttons[8]).X, "The left group ends before Search.");

        // The Theme button shows the theme it switches to.
        var themeIcon = theme == AppTheme.Dark ? "IconSun" : "IconMoon";
        Assert.Same(Application.Current!.FindResource(themeIcon), app.Find<ToolbarButton>("ThemeSwitch").IconData);

        // Left panel: filter box, then the sections in order with a zero count each.
        var filter = app.Find<TextBox>("FilterBox");
        Assert.Equal("Filter", filter.PlaceholderText);
        var sectionTexts = TextsIn(left).Where(text => text != "Filter").ToList();
        Assert.Equal(LeftPanelSections.SelectMany(section => new[] { section, "0" }), sectionTexts);
        Assert.True(app.BoundsOf(filter).Bottom <= app.BoundsOf(app.Find<StackPanel>("Sections")).Y + 1);

        // Commit graph: column headers in order, then the empty state.
        Assert.Equal(
            [.. GraphColumns, "No repository open", "Open, clone or init a repository to see its history."],
            TextsIn(graph));

        // Right panel.
        Assert.Equal(["Commit details", "Select a commit to see its details."], TextsIn(right));

        // Status bar: the git version must be the one git itself reports.
        var gitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem());
        Assert.NotNull(gitPath);
        var versionOutput = await new GitRunner(gitPath).RunAsync(new GitCommand("--version"), TestContext.Current.CancellationToken);
        Assert.True(GitVersion.TryParse(versionOutput.StandardOutput, out var gitVersion));
        Assert.Equal(["No repository", "Ready", $"Git {gitVersion}", "Activity log"], TextsIn(status));

        // No text is clipped, cut off or shortened.
        Assert.Empty(LayoutAudit.FindClippedText(app.Window));

        // Colours, sampled from an empty spot of each region.
        var palette = PaletteOf(theme);
        AssertColour(palette.Chrome, screenshot, new Point(width - 30, TabStripHeight / 2), "repo tabs");
        var toolbarGap = (app.BoundsOf(buttons[7]).Right + app.BoundsOf(buttons[8]).X) / 2;
        AssertColour(palette.Chrome, screenshot, new Point(toolbarGap, TabStripHeight + (ToolbarHeight / 2)), "toolbar");
        var statusGap = (app.BoundsOf(app.Find<TextBlock>("OperationStatus")).Right + app.BoundsOf(app.Find<TextBlock>("GitStatus")).X) / 2;
        AssertColour(palette.Chrome, screenshot, new Point(statusGap, height - (StatusBarHeight / 2)), "status bar");
        var headers = app.BoundsOf(app.Find<Border>("ColumnHeaders"));
        AssertColour(palette.Chrome, screenshot, new Point(headers.X + 4, headers.Center.Y), "graph column headers");
        AssertColour(palette.Panel, screenshot, new Point(leftPanelWidth / 2, height - StatusBarHeight - 40), "left panel");
        AssertColour(palette.Panel, screenshot, new Point(width - (rightPanelWidth / 2), height - StatusBarHeight - 40), "right panel");
        AssertColour(palette.Graph, screenshot, new Point(leftPanelWidth + (graphWidth / 2), headers.Bottom + 30), "commit graph");
        var accentLine = app.BoundsOf(app.Find<Border>("TabAccentLine"));
        AssertColour(palette.Accent, screenshot, accentLine.Center, "accent line on the tab");

        // Main text colour: the title of the empty state is drawn in it.
        var title = graph.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "No repository open");
        Assert.True(
            ContainsColour(screenshot, app.BoundsOf(title), palette.Text),
            $"The text \"No repository open\" is not drawn in the main text colour {palette.Text}.");
    }

    /// <summary>Asserts where a region is and returns it.</summary>
    public static Control AssertRegion(ShellDriver app, string automationId, Rect expected)
    {
        var region = app.FindByAutomationId(automationId);
        var actual = app.BoundsOf(region);
        Assert.True(
            Math.Abs(actual.X - expected.X) <= SizeTolerance
            && Math.Abs(actual.Y - expected.Y) <= SizeTolerance
            && Math.Abs(actual.Width - expected.Width) <= SizeTolerance
            && Math.Abs(actual.Height - expected.Height) <= SizeTolerance,
            $"{automationId} is at {actual}, expected {expected}.");
        return region;
    }

    /// <summary>The visible texts inside a region, in reading order: top to bottom, then left to right.</summary>
    public static List<string> TextsIn(Visual region)
    {
        var items = region.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => (Text: text.Text!, Area: new Rect(text.TranslatePoint(default, region) ?? default, text.Bounds.Size)))
            .OrderBy(item => item.Area.Center.Y)
            .ToList();

        // Texts whose centres are within a few pixels of each other vertically are on one row.
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

    public static void AssertColour(Color expected, Screenshot screenshot, Point position, string what)
    {
        var actual = screenshot.PixelAt(position);
        Assert.True(actual == expected, $"The {what} at {position} is {actual}, expected {expected}.");
    }

    private static bool ContainsColour(Screenshot screenshot, Rect area, Color colour)
    {
        for (var y = (int)area.Top; y < (int)area.Bottom; y++)
        {
            for (var x = (int)area.Left; x < (int)area.Right; x++)
            {
                if (screenshot.PixelAt(x, y) == colour)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
