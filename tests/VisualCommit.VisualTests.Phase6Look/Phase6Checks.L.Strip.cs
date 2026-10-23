// Check L33 of part L (docs/test-reports/phase-6.md): the tab strip shrinks, then scrolls (design
// 13.3, 13.4; D187, D205, D228): eight tabs of 120 at 1000 wide in today's places; at 1100 the Web
// dashboard, its nine repositories that can open and new tabs: shrinking at eleven and twelve,
// scrolling from fourteen with the active tab whole; Ctrl+Tab round to the first brings it into
// view. Dark only, five single pictures. The part's map is in Phase6Checks.L.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
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
    /// Check L33: one start at 1000×700 on "Web"'s dashboard and seven of its repositories (LD21),
    /// the window then 1100 wide; design-system and notes opened by the palette, the eleventh to
    /// fourteenth tabs from "+"; Ctrl+Tab round to the first. Git unchanged. Dark.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L33_tab_strip_shrinks_then_scrolls()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in PartL.WebNames.Where(name => name != "old-site"))
        {
            using var repo = copy.Repo(name);
            exact[name] = await repo.ExactStateAsync();
        }

        // Step 1: "Web" and a session of the dashboard and seven repositories, scratch active, at 1000 wide.
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace()] });
        List<TabState> tabs = [new TabState(null, PartL.WorkspaceId), .. PartL.StripStart.Skip(1).Select(name => new TabState(copy.PathOf(name)))];
        using var app = ShellDriver.StartWithTabs(data.Path, tabs, PartL.StripActive, PartL.NarrowSize.Width, PartL.NarrowSize.Height);
        await app.WaitUntilReadyAsync();
        await app.WaitForRepositoryAsync(OperationChainTimeout);
        Assert.Equal("scratch", app.Session.Shell.ActiveTab.Title);
        Shot(app, "l33a-eight-tabs-1000");
        var strip = AssertStrip(app, PartL.StripStart, PartL.Ts8, active: 7, offset: 0, plus: PartL.PlusAt1000);
        AssertDashboardTabLook(app, strip.Tabs[0], active: false);
        Assert.All(strip.Tabs, tab => Assert.Equal(tab.Title, tab.DrawnTitle));
        Assert.Equal(PartL.OpenANewTab, strip.AddButtonName);
        Assert.Equal(PartL.PlusToolTip, ToolTip.GetTip(app.Find<Button>("AddRepoButton")) as string);
        Assert.All(strip.Tabs.Skip(1), tab => Assert.Equal(PartL.RepoTabId, tab.AutomationId));
        Assert.Equal("experiment", app.Find<TextBlock>("CurrentBranch").Text);

        // Step 2: 1100 wide; design-system and notes by the palette; "+".
        app.Window.Width = PartL.Size.Width;
        app.Settle();
        AssertStrip(app, PartL.StripStart, PartL.Ts8, active: 7, offset: 0, plus: PartL.PlusAt1000);
        await OpenByPaletteAsync(app, PartL.OpenDesignSystem, "design-system");
        AssertStrip(app, [.. PartL.StripStart, "design-system"], PartL.Ts9, active: 8, offset: 0, plus: PartL.PlusAt1100Shrunk);
        await OpenByPaletteAsync(app, PartL.OpenNotes, "notes");
        AssertStrip(app, [.. PartL.StripStart, "design-system", "notes"], PartL.Ts10, active: 9, offset: 0, plus: PartL.PlusAt1100Shrunk);
        app.Click(app.Find<Button>("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 11 && app.Session.Shell.ActiveTab.ShowsWelcome, "the eleventh tab's welcome page");
        app.MoveToNeutral();
        Shot(app, "l33b-eleven-tabs");
        List<string> titles = [.. PartL.StripStart, "design-system", "notes", PartL.NewTab];
        strip = AssertStrip(app, titles, PartL.Ts11, active: 10, offset: 0, plus: PartL.PlusAt1100Shrunk);
        Assert.Equal("Web", strip.Tabs[0].DrawnTitle);
        Assert.EndsWith("…", strip.Tabs[2].DrawnTitle, StringComparison.Ordinal);
        Assert.EndsWith("…", strip.Tabs[8].DrawnTitle, StringComparison.Ordinal);
        AssertNewTabWelcome(app, copy);

        // Step 3: "+": twelve tabs, no scrolling.
        app.Click(app.Find<Button>("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 12, "the twelfth tab");
        app.MoveToNeutral();
        Shot(app, "l33c-twelve-tabs");
        titles.Add(PartL.NewTab);
        strip = AssertStrip(app, titles, PartL.Ts12, active: 11, offset: 0, plus: PartL.PlusAt1100Shrunk);
        Assert.EndsWith("…", strip.Tabs[0].DrawnTitle, StringComparison.Ordinal);
        AssertNewTabWelcome(app, copy);

        // Step 4: "+" twice: thirteen tabs shrunk, then fourteen of 80, scrolled.
        app.Click(app.Find<Button>("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 13, "the thirteenth tab");
        titles.Add(PartL.NewTab);
        AssertStrip(app, titles, PartL.Ts13, active: 12, offset: 0, plus: PartL.PlusAt1100Shrunk);
        app.Click(app.Find<Button>("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 14, "the fourteenth tab");
        app.MoveToNeutral();
        Shot(app, "l33d-fourteen-tabs-scrolled");
        titles.Add(PartL.NewTab);
        strip = AssertStrip(app, titles, PartL.Ts14, active: 13, offset: PartL.Ts14Offset, plus: PartL.PlusAt1100Shrunk);
        Assert.True(strip.IsScrolling, "The strip does not scroll with fourteen tabs.");
        Assert.Equal("…", strip.Tabs[0].DrawnTitle);
        Assert.True(Math.Abs(strip.Tabs[13].Bounds.Left - 982) <= 0.5 && Math.Abs(strip.Tabs[13].Bounds.Right - 1062) <= 0.5, $"The active tab is at {strip.Tabs[13].Bounds}, not x 982 to 1062.");
        Assert.True(Math.Abs(strip.Tabs[0].Bounds.Left + 58) <= 0.5 && Math.Abs(strip.Tabs[0].Bounds.Right - 22) <= 0.5, $"The first tab is at {strip.Tabs[0].Bounds}, not x −58 to 22.");
        Assert.True(Math.Abs(strip.PanelBounds.Left) <= 0.5 && strip.PanelBounds.Right <= 1062.5, $"The strip's panel is at {strip.PanelBounds}: it does not clip at x 0 and 1062.");
        AssertNewTabWelcome(app, copy);

        // Step 5: Ctrl+Tab round to the first tab: the dashboard, in view.
        var mark = app.LogMark();
        app.PressCtrlTab();
        await app.WaitForDashboardReadAsync(since: mark, timeout: OperationChainTimeout);
        app.MoveToNeutral();
        Shot(app, "l33e-first-tab-in-view");
        strip = AssertStrip(app, titles, PartL.Ts14, active: 0, offset: 0, plus: PartL.PlusAt1100Shrunk);
        AssertDashboardTabLook(app, strip.Tabs[0], active: true);
        Assert.True(Math.Abs(strip.Tabs[13].Bounds.Left - 1040) <= 0.5, $"The last tab starts at x {strip.Tabs[13].Bounds.Left}, not 1040 (its first 22 px shown).");
        var dashboard = app.DashboardSnapshot();
        Assert.Equal(PartL.WebNames, dashboard.Rows.Select(row => row.Name));
        Assert.Equal("Folder not found", dashboard.Row("old-site").State.Text);
        foreach (var name in PartL.WebNames.Where(name => name != "old-site"))
        {
            using var repo = copy.Repo(name);
            Assert.True(exact[name] == await repo.ExactStateAsync(), $"{name} changed.");
        }
    }

    /// <summary>
    /// The strip as drawn against a state of L33's table (13.3): the tabs' titles in order, each
    /// tab's width and place (±0.5; tab k at the widths before it less the offset), the active
    /// tab, the offset, "+"'s place; every tab's tooltip and automation name its whole title, its
    /// close button shown. Returns the snapshot.
    /// </summary>
    private static TabStripSnapshot AssertStrip(ShellDriver app, IReadOnlyList<string> titles, IReadOnlyList<double> widths, int active, double offset, Rect plus)
    {
        var strip = app.TabStrip();
        Assert.Equal(titles, strip.Tabs.Select(tab => tab.Title));
        Assert.Equal(widths.Count, strip.Tabs.Count);
        var x = -offset;
        for (var index = 0; index < widths.Count; index++)
        {
            var tab = strip.Tabs[index];
            Assert.True(
                Math.Abs(tab.Bounds.Left - x) <= 0.5 && Math.Abs(tab.Bounds.Width - widths[index]) <= 0.5,
                $"Tab {index} ({tab.Title}) is at x {tab.Bounds.Left}, {tab.Bounds.Width} wide; 13.3 gives x {x}, {widths[index]} wide.");
            Assert.Equal(tab.Title, tab.ToolTip);
            Assert.Equal(tab.Title, tab.AutomationName);
            Assert.True(tab.ShowsCloseButton, $"Tab {index} ({tab.Title}) shows no close button.");
            Assert.Equal(index == active, tab.IsActive);
            x += widths[index];
        }

        Assert.Equal(active, strip.ActiveIndex);
        Assert.Equal(offset, strip.Offset, 0.5);
        Assert.True(Math.Abs(strip.AddButtonBounds.Left - plus.Left) <= 0.5 && Math.Abs(strip.AddButtonBounds.Right - plus.Right) <= 0.5, $"\"+\" is at {strip.AddButtonBounds}, not x {plus.Left} to {plus.Right}.");
        return strip;
    }

    /// <summary>The dashboard's tab (13.4): <c>DashboardTab</c>, item status <c>Workspace</c>, <c>IconWorkspace</c> 12×12 14 from its left, the title 6 after it, both primary while active and secondary otherwise.</summary>
    private static void AssertDashboardTabLook(ShellDriver app, TabSnapshot tab, bool active)
    {
        var palette = PaletteOf(app);
        Assert.True(tab.IsDashboard, $"{tab.Title}'s tab is not a dashboard's.");
        Assert.Equal((PartL.DashboardTabId, PartL.Workspace, PartL.ItemStatusWorkspace), (tab.AutomationId, tab.Title, tab.ItemStatus));
        var icon = tab.IconBounds ?? throw new Xunit.Sdk.XunitException("The dashboard's tab shows no TabIcon.");
        Assert.True(Math.Abs(icon.Width - 12) <= 0.5 && Math.Abs(icon.Height - 12) <= 0.5 && Math.Abs(icon.Left - tab.Bounds.Left - 14) <= PartL.Tolerance, $"TabIcon is at {icon}, not 12×12 14 into the tab at {tab.Bounds}.");
        Assert.True(Math.Abs(tab.TitleBounds.Left - icon.Right - 6) <= PartL.Tolerance, "The dashboard tab's title does not start 6 after its icon.");
        var colour = active ? palette.TextPrimary : palette.TextSecondary;
        Assert.Equal(colour, (tab.IconForeground as ISolidColorBrush)?.Color);
        Assert.Equal(colour, (tab.TitleForeground as ISolidColorBrush)?.Color);
    }

    /// <summary>
    /// A new tab's welcome page with the recent list the palette made (L33 step 2): its texts in
    /// reading order, the recent list notes (opened last) then design-system, each folder the copy's
    /// one of that name (as the app keeps it), cut with "…" and whole in its tooltip.
    /// </summary>
    private static void AssertNewTabWelcome(ShellDriver app, TempWorkspace copy)
    {
        Assert.True(app.Session.Shell.ActiveTab.ShowsWelcome, "The active tab shows no welcome page.");
        var recent = app.Session.Shell.ActiveTab.Welcome.Recent.Select(item => (item.Name, item.Path)).ToList();
        Assert.Equal(["notes", "design-system"], recent.Select(item => item.Name));
        foreach (var (name, path) in recent)
        {
            Assert.True(
                path.EndsWith(Path.DirectorySeparatorChar + name, StringComparison.Ordinal) && path.Contains(Path.GetFileName(copy.Root), StringComparison.Ordinal),
                $"The recent {name} is {path}, not the copy's folder of that name.");
        }

        Assert.Equal(PartL.WelcomeTexts(recent), TextsIn(app.ActiveWelcomeView));
        var items = app.ActiveWelcomeView.GetVisualDescendants().OfType<Button>()
            .Where(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "RecentRepository").ToList();
        foreach (var (button, (name, path)) in items.Zip(recent))
        {
            Assert.Equal(path, ToolTip.GetTip(button) as string);
            var folder = button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).OrderBy(text => app.BoundsOf(text).X).Last();
            AssertFolderCut(folder, path, name);
        }

        var section = app.WelcomeWorkspaces();
        var item = Assert.Single(section.Items);
        Assert.Equal((PartL.Workspace, PartL.Count(10), PartL.WebItemToolTip), (item.Name, item.CountText, item.ToolTip));
    }

    /// <summary>The palette's <c>Open repository {name}</c> by the keyboard (5.10): a new tab at the end, its graph drawn.</summary>
    private static async Task OpenByPaletteAsync(ShellDriver app, string query, string name)
    {
        app.MoveToNeutral();
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen, "the command palette");
        app.TypeInPalette(query);
        app.Settle();
        app.PressKey(Avalonia.Input.Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen && app.Session.Shell.ActiveTab.Title == name, $"{name}'s tab");
        await app.WaitForRepositoryAsync(OperationChainTimeout);
    }
}
