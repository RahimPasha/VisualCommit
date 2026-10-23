// Checks L26 and L27 of part L (docs/test-reports/phase-6.md): the sweep's welcome page and its
// clone form, and the graph of "ops" with its drag, selection, menus, tooltips and dialogs, each
// screen in both themes by one pair (ShotInBothThemesAsync), which also proves that the window
// follows the system's change both ways on that screen (11.14, D194). The part's map is in
// Phase6Checks.L.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

public sealed partial class Phase6ChecksL
{
    /// <summary>
    /// Check L26: the welcome page of an empty tab with a recent list and one workspace, its
    /// Workspaces section between <c>Init</c> and <c>Recent repositories</c>, the recent folders cut
    /// with "…"; then the clone form, which hides the section; both in both themes (11.14, 13.1;
    /// LD1 to LD3). Empty tab, System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L26_welcome_page_in_both_themes()
    {
        using var graph = await Scenarios.GraphAsync();
        using var linear = await Scenarios.LinearAsync();
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");

        // Step 1: no tab; the recent list graph (newest), then linear; "Web" in workspaces.json; System.
        var opened = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);
        ShellDriver.WriteSession(data.Path, new SessionState
        {
            Recent = [new RecentRepository(graph.Path, "graph", opened.AddHours(1)), new RecentRepository(linear.Path, "linear", opened)],
        });
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace()] });
        WriteSystemTheme(data);
        using var app = ShellDriver.Start(data.Path, PartL.Size.Width, PartL.Size.Height, systemDark: true);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();

        // At the start: the log's line, one tab "New tab" without a close button, the toolbar, the status bar.
        AssertSystemDarkStart(app);
        var strip = app.TabStrip();
        var tab = Assert.Single(strip.Tabs);
        Assert.Equal((PartL.NewTab, false), (tab.Title, tab.ShowsCloseButton));
        foreach (var id in PartL.DisabledOnEmptyTab)
        {
            AssertButton(app, id, false, null);
        }

        AssertButton(app, "SettingsButton", true, PartL.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartL.SwitchToLightSystem);
        Assert.Equal(PartL.NoRepository, app.Find<TextBlock>("CurrentBranch").Text);
        List<(string, string)> recent = [("graph", graph.Path), ("linear", linear.Path)];

        await ShotInBothThemesAsync(app, "l26a-welcome", assertEach: (picture, palette) => AssertWelcomePage(app, picture, palette, recent));

        // Step 2: Clone; the URL box takes the focus; the form in place of the buttons, the section and the list.
        app.Click(app.Find<Button>("CloneRepoButton"));
        await app.WaitForAsync(() => app.Find<TextBox>("CloneUrlBox").IsFocused, "CloneUrlBox to have the keyboard focus");
        await ShotInBothThemesAsync(app, "l26b-clone-form", assertEach: (picture, palette) => AssertCloneForm(app, picture, palette));

        // After Cancel: the page as after step 1.
        app.Click(app.Find<Button>("CloneCloseButton"));
        await app.WaitForAsync(() => app.Find<StackPanel>("WelcomeButtons").IsEffectivelyVisible, "the welcome page's buttons to show again");
        app.MoveToNeutral();
        AssertWelcomePage(app, app.Capture(), PaletteOf(app), recent);
    }

    /// <summary>
    /// L26 step 1's welcome page in the variant shown (13.1, LD3): in the graph area, its stack 392
    /// wide and centred vertically without scrolling; its texts in reading order; no error; the
    /// Workspaces section between the buttons' row and <c>Recent repositories</c>, 78 high, with the
    /// item "Web"; the recent items with their folders cut with "…", whole in their tooltips; the
    /// left and right panels as phase 0 gives them; the colours; no tooltip open.
    /// </summary>
    private static void AssertWelcomePage(ShellDriver app, Screenshot picture, Palette palette, IReadOnlyList<(string Name, string Folder)> recent)
    {
        var view = app.ActiveWelcomeView;
        var page = app.BoundsOf(view);
        Assert.True(
            Math.Abs(page.Left - PartL.GraphArea.Left) <= PartL.Tolerance && Math.Abs(page.Right - PartL.GraphArea.Right) <= PartL.Tolerance
                && Math.Abs(page.Top - PartL.GraphArea.Top) <= PartL.Tolerance && Math.Abs(page.Bottom - PartL.GraphArea.Bottom) <= PartL.Tolerance,
            $"The welcome page is at {page}, not in the graph area (x 260 to 700, y 88 to 674).");
        var buttons = view.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "WelcomeButtons");
        var stack = (StackPanel)buttons.GetVisualParent()!;
        var stackBounds = app.BoundsOf(stack);
        Assert.True(
            Math.Abs(stackBounds.Left - PartL.StackLeft) <= PartL.Tolerance && Math.Abs(stackBounds.Right - PartL.StackRight) <= PartL.Tolerance,
            $"The welcome page's stack is at {stackBounds}, not x 284 to 676 (392 wide).");
        Assert.True(Math.Abs(stackBounds.Center.Y - PartL.StackCentreY) <= 1, $"The stack's centre is at y {stackBounds.Center.Y}, not 381 ± 1 (LD3).");
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"The welcome page scrolls: {scroller.Extent.Height} of content in {scroller.Viewport.Height}.");
        Assert.Equal(PartL.WelcomeTexts(recent), TextsIn(view));
        Assert.False(app.Find<TextBlock>("WelcomeError").IsEffectivelyVisible, "WelcomeError shows.");

        // The Workspaces section (13.1 as built): between the buttons' row and "Recent repositories".
        var section = app.WelcomeWorkspaces();
        var buttonsRow = app.BoundsOf(app.Find<Button>("OpenRepoButton").GetVisualParent<StackPanel>()!);
        var headingRow = app.BoundsOf(view.GetVisualDescendants().OfType<DockPanel>().Single(panel => panel.Name == "WorkspacesHeadingRow"));
        Assert.True(Math.Abs(headingRow.Top - buttonsRow.Bottom - PartL.HeadingRowGap) <= PartL.Tolerance, $"The heading row's top is {headingRow.Top - buttonsRow.Bottom} below the buttons' row, not 24.");
        Assert.True(Math.Abs(headingRow.Height - PartL.HeadingRowHeight) <= PartL.Tolerance, $"The heading row is {headingRow.Height} high, not 20.");
        Assert.Equal(PartL.WorkspacesHeading, section.Heading);
        var heading = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "WorkspacesHeading");
        Assert.Equal((11d, FontWeight.SemiBold), (heading.FontSize, heading.FontWeight));
        Assert.Equal(palette.TextSecondary, (section.HeadingForeground as ISolidColorBrush)?.Color);
        Assert.True(Math.Abs(section.HeadingBounds.Left - stackBounds.Left) <= PartL.Tolerance, "WorkspacesHeading is not at the stack's left.");
        Assert.True(Math.Abs(section.NewWorkspaceBounds.Right - stackBounds.Right) <= PartL.Tolerance, "NewWorkspaceButton is not at the stack's right.");
        Assert.True(section.NewWorkspaceEnabled, "NewWorkspaceButton is disabled.");
        Assert.Equal((PartL.NewWorkspaceToolTip, PartL.NewWorkspaceToolTip), (section.NewWorkspaceToolTip, section.NewWorkspaceHelpText));
        var newWorkspace = app.Find<Button>("NewWorkspaceButton");
        var plus = newWorkspace.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single();
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconPlus"), plus.Data), "NewWorkspaceButton's icon is not IconPlus.");
        Assert.Equal((12d, 12d), (plus.Bounds.Width, plus.Bounds.Height));
        var label = newWorkspace.GetVisualDescendants().OfType<TextBlock>().Single(text => text.IsEffectivelyVisible);
        Assert.Equal(PartL.NewWorkspace, label.Text);
        Assert.True(Math.Abs(app.BoundsOf(label).Left - app.BoundsOf(plus).Right - 4) <= PartL.Tolerance, "New workspace…'s label is not 4 after its icon.");
        Assert.Equal(palette.TextSecondary, (label.Foreground as ISolidColorBrush)?.Color);
        Assert.Null(section.Notice);
        Assert.False(section.ShowsNoWorkspaces, "NoWorkspacesText shows beside a workspace.");
        var item = Assert.Single(section.Items);
        Assert.Equal((PartL.Workspace, PartL.Count(10), PartL.WebItemToolTip, PartL.Workspace), (item.Name, item.CountText, item.ToolTip, item.AutomationName));
        Assert.True(
            Math.Abs(item.Bounds.Height - PartL.ItemHeight) <= PartL.Tolerance && Math.Abs(item.Bounds.Left - stackBounds.Left) <= PartL.Tolerance && Math.Abs(item.Bounds.Right - stackBounds.Right) <= PartL.Tolerance,
            $"The workspace item is at {item.Bounds}, not 26 high across the stack's 392.");
        Assert.True(item.NameBounds.Left < item.CountBounds.Left, "The item's name is not left of its count.");
        Assert.Equal(palette.TextPrimary, (item.NameForeground as ISolidColorBrush)?.Color);
        Assert.Equal(palette.TextSecondary, (item.CountForeground as ISolidColorBrush)?.Color);
        Assert.True(Math.Abs(item.Bounds.Bottom - buttonsRow.Bottom - PartL.SectionHeight(1)) <= PartL.Tolerance, $"The Workspaces section is {item.Bounds.Bottom - buttonsRow.Bottom} high, not 78 (52 + 26 × 1).");
        var recentHeading = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == PartL.RecentRepositories);
        Assert.True(app.BoundsOf(recentHeading).Top >= item.Bounds.Bottom - 0.5, "Recent repositories is not below the Workspaces section.");

        // The recent list: each item's name (primary) and, 12 after it, its folder (secondary), cut with "…" at the item's right edge, whole in its tooltip.
        var items = view.GetVisualDescendants().OfType<Button>().Where(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "RecentRepository").ToList();
        Assert.Equal(recent.Select(entry => entry.Name), items.Select(button => Avalonia.Automation.AutomationProperties.GetName(button)));
        foreach (var (button, (name, folder)) in items.Zip(recent))
        {
            Assert.Equal(folder, ToolTip.GetTip(button) as string);
            var texts = button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).OrderBy(text => app.BoundsOf(text).X).ToList();
            Assert.Equal([name, folder], texts.Select(text => text.Text));
            Assert.Equal(palette.TextPrimary, (texts[0].Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(palette.TextSecondary, (texts[1].Foreground as ISolidColorBrush)?.Color);
            Assert.True(Math.Abs(app.BoundsOf(texts[1]).Left - app.BoundsOf(texts[0]).Right - 12) <= PartL.Tolerance, $"{name}'s folder does not start 12 after its name.");
            var drawn = DrawnLines.Of(texts[1]).Single();
            Assert.True(drawn.EndsWith('…') && drawn.Length < folder.Length, $"{name}'s folder is drawn \"{drawn}\", not cut with \"…\".");
            Assert.True(Math.Abs(app.BoundsOf(texts[1]).Right - (app.BoundsOf(button).Right - 8)) <= PartL.Tolerance, $"{name}'s folder does not end at the item's right edge.");
            AssertInk(picture, app.BoundsOf(texts[1]), palette.TextSecondary, palette.Window, $"{name}'s folder");
        }

        // The left and right panels as phase 0 gives them; the toolbar and nothing open.
        Assert.Equal(
            PartL.LeftPanelSections.SelectMany(name => new[] { name, "0" }),
            TextsIn(app.FindByAutomationId("LeftPanel")).Where(text => text != "Filter"));
        Assert.Equal(PartL.DetailsPlaceholder, TextsIn(app.FindByAutomationId("RightPanel")));

        // The colours of the variant.
        AssertColour(palette.Window, picture, new Point(270, 100), "graph area's background (VcWindowBackgroundBrush)");
        AssertInk(picture, item.NameBounds, palette.TextPrimary, palette.Window, "the item Web's name");
        AssertInk(picture, item.CountBounds, palette.TextSecondary, palette.Window, "10 repositories");
        AssertInk(picture, section.HeadingBounds, palette.TextSecondary, palette.Window, "Workspaces");
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Control>(), control => ToolTip.GetIsOpen(control));
    }

    /// <summary>
    /// L26 step 2's clone form in the variant shown (phase 1's 13a, 13.1): the buttons, the
    /// Workspaces section and the recent list hidden; the heading and its line above the form; the
    /// form's fields, the URL box empty with the keyboard focus; no error, no progress; the boxes'
    /// colours, the focused one's border the accent's.
    /// </summary>
    private static void AssertCloneForm(ShellDriver app, Screenshot picture, Palette palette)
    {
        var view = app.ActiveWelcomeView;
        Assert.False(app.Find<StackPanel>("WelcomeButtons").IsEffectivelyVisible, "WelcomeButtons shows beside the clone form.");
        Assert.False(app.Find<TextBlock>("WorkspacesHeading").IsEffectivelyVisible, "The Workspaces section shows beside the clone form.");
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "RecentRepository");
        Assert.Equal(PartL.CloneFormTexts, TextsIn(view));
        var url = app.Find<TextBox>("CloneUrlBox");
        Assert.True(url.IsFocused, "CloneUrlBox does not have the keyboard focus.");
        Assert.True(string.IsNullOrEmpty(url.Text), $"CloneUrlBox holds \"{url.Text}\".");
        Assert.True(app.Find<Button>("CloneBrowseButton").IsEffectivelyVisible && app.Find<Button>("CloneStartButton").IsEffectivelyVisible && app.Find<Button>("CloneCloseButton").IsEffectivelyVisible, "The form's buttons do not show.");
        Assert.True(app.BoundsOf(app.Find<Button>("CloneBrowseButton")).Left >= app.BoundsOf(app.Find<TextBox>("CloneParentBox")).Right - 0.5, "Browse is not at the parent box's right.");
        Assert.False(app.Find<TextBlock>("CloneError").IsEffectivelyVisible, "CloneError shows.");
        Assert.False(app.Find<ProgressBar>("CloneProgress").IsEffectivelyVisible, "CloneProgress shows.");
        var dark = IsDark(palette);
        foreach (var id in new[] { "CloneUrlBox", "CloneParentBox", "CloneNameBox" })
        {
            var box = app.Find<TextBox>(id);
            var bounds = app.BoundsOf(box);
            AssertColour(PartL.BoxBackground(dark), picture, new Point(bounds.Right - 6, bounds.Center.Y), $"{id}'s background (TextControlBackground)");
            var border = id == "CloneUrlBox" ? PartL.BoxFocusedBorder(dark) : PartL.BoxBorder(dark);
            AssertColour(border, picture, new Point(bounds.Left + 0.5, bounds.Center.Y), $"{id}'s left border ({(id == "CloneUrlBox" ? "focused, TextControlBorderBrushFocused" : "TextControlBorderBrush")})");
        }
    }
}
