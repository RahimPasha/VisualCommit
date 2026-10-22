using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The tab strip keeps phase 1's places while the tabs fit, then shrinks the widest, then scrolls
/// with the active tab in view (docs/phase-6-design.md 13.3, D228), and a dashboard's tab has its
/// icon, padding, automation id and item status (13.4).
/// </summary>
public class RepoTabStripPanelTests
{
    private static readonly GitDetection GitFound = new(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null);

    // ---- 13.3's arithmetic, row by row of its table. ----

    [Theory]
    [InlineData(1000, 8)]
    [InlineData(1100, 3)]
    [InlineData(1100, 8)]
    [InlineData(1920, 15)]
    public void Tabs_that_fit_keep_their_natural_widths(int window, int count)
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit(Natural(count), window - 38);

        Assert.False(scrolls);
        Assert.All(widths, width => Assert.Equal(120, width));
    }

    [Fact]
    public void Twelve_tabs_at_1000_shrink_to_two_of_81_and_ten_of_80()
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit(Natural(12), 962);

        Assert.False(scrolls);
        Assert.Equal([81, 81, .. Enumerable.Repeat(80.0, 10)], widths);
    }

    [Fact]
    public void Eleven_tabs_at_1100_shrink_to_six_of_97_and_five_of_96()
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit(Natural(11), 1062);

        Assert.False(scrolls);
        Assert.Equal([.. Enumerable.Repeat(97.0, 6), .. Enumerable.Repeat(96.0, 5)], widths);
        Assert.Equal(1062, widths.Sum());
    }

    [Fact]
    public void Twenty_three_tabs_at_1920_shrink_to_81_or_82()
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit(Natural(23), 1882);

        Assert.False(scrolls);
        Assert.Equal([.. Enumerable.Repeat(82.0, 19), .. Enumerable.Repeat(81.0, 4)], widths);
        Assert.Equal(1882, widths.Sum());
    }

    [Theory]
    [InlineData(1000, 13)]
    [InlineData(1100, 14)]
    [InlineData(1920, 24)]
    public void Tabs_that_do_not_fit_at_80_each_scroll(int window, int count)
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit(Natural(count), window - 38);

        Assert.True(scrolls);
        Assert.All(widths, width => Assert.Equal(80, width));
    }

    [Fact]
    public void The_widest_tabs_shrink_first()
    {
        var (widths, scrolls) = RepoTabStripPanel.Fit([220, 120, 120], 400);

        Assert.False(scrolls);
        Assert.Equal([160.0, 120, 120], widths);

        // The cap lies between the natural widths; the pixel left over goes to the leftmost tab at the cap.
        (widths, _) = RepoTabStripPanel.Fit([200, 130, 220, 120], 541);
        Assert.Equal([146.0, 130, 145, 120], widths);
    }

    // ---- The strip in RepoTabsView. ----

    [AvaloniaFact]
    public void Tabs_that_fit_at_1100_are_placed_as_phase_1s_strip_placed_them()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(Path.Combine("repos", "graph")), new TabState(Path.Combine("repos", "design-system")), new TabState(Path.Combine("repos", "a-design-system-whose-folder-name-is-rather-long")), new TabState(null)] });
        using var host = Show(shell, 1100);

        var tabs = Tabs(host);
        Assert.Equal(4, tabs.Count);
        // Phase 1's horizontal StackPanel measured each tab with an infinite width and placed it
        // at that width, at least 120 (the tab's old MinWidth), one after another: a short title
        // gives clamp(51 + its width, 120, 220) (0.8), a long one is cut with "…" inside 220.
        var bounds = tabs.Select(tab => BoundsIn(host, tab)).ToList();
        var x = 0.0;
        for (var i = 0; i < tabs.Count; i++)
        {
            tabs[i].Measure(Size.Infinity);
            var old = Math.Max(120, tabs[i].DesiredSize.Width);
            Assert.Equal(x, bounds[i].X, 0.5);
            Assert.Equal(old, bounds[i].Width, 0.5);
            x += old;
        }

        Assert.Equal(120, bounds[0].Width, 0.5);
        Assert.Equal(51 + TextWidth(Named<TextBlock>(tabs[1], "TabTitle")), bounds[1].Width, 1.0);
        Assert.InRange(bounds[1].Width, 121, 219);
        Assert.InRange(bounds[2].Width, 200, 220);
        Assert.EndsWith("…", DrawnLines.Of(Named<TextBlock>(tabs[2], "TabTitle"))[0], StringComparison.Ordinal);
        Assert.Equal(120, bounds[3].Width, 0.5);
        var add = AddButton(host);
        Assert.Equal(x + 2, BoundsIn(host, add).X, 0.5);
        Assert.Equal(0, Panel(host).Offset);
        Assert.Equal("Open a new tab", AutomationProperties.GetName(add));
        TestPictures.Save(host, "tabs-three");
    }

    [AvaloniaFact]
    public void Fourteen_tabs_at_1100_scroll_with_the_active_tab_whole_and_plus_at_the_end()
    {
        var shell = Create(new SessionState());
        for (var i = 1; i < 14; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        using var host = Show(shell, 1100);

        var panel = Panel(host);
        Assert.True(panel.IsScrolling);
        Assert.Equal(58, panel.Offset, 0.5);
        var tabs = Tabs(host);
        Assert.All(tabs, tab => Assert.Equal(80, tab.Bounds.Width, 0.5));
        Assert.Equal(-58, BoundsIn(host, tabs[0]).X, 0.5);
        Assert.Equal(982, BoundsIn(host, tabs[13]).X, 0.5);
        Assert.Equal(1062, BoundsIn(host, tabs[13]).Right, 0.5);
        var add = BoundsIn(host, AddButton(host));
        Assert.Equal(1064, add.X, 0.5);
        Assert.Equal(1098, add.Right, 0.5);
        TestPictures.Save(host, "tabs-fourteen");
    }

    [AvaloniaFact]
    public void The_active_tab_is_brought_into_view_from_either_side()
    {
        var shell = Create(new SessionState());
        for (var i = 1; i < 14; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        using var host = Show(shell, 1100);
        var panel = Panel(host);
        Assert.Equal(58, panel.Offset, 0.5);

        // From the right: the first tab, partly out on the left, comes whole into view.
        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset, 0.5);

        // A tab already whole in view moves nothing.
        shell.ActivateTabCommand.Execute(shell.Tabs[5]);
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset, 0.5);

        // From the left: the last tab comes back as little as needed.
        shell.ActivateTabCommand.Execute(shell.Tabs[13]);
        DiffHost.Settle();
        Assert.Equal(58, panel.Offset, 0.5);

        // Closing a tab changes the count: the strip shrinks instead of scrolling.
        shell.CloseTabCommand.Execute(shell.Tabs[13]);
        DiffHost.Settle();
        Assert.False(panel.IsScrolling);
        Assert.Equal(0, panel.Offset);
    }

    [AvaloniaFact]
    public void The_wheel_scrolls_the_tabs_only_while_they_overflow()
    {
        var shell = Create(new SessionState());
        for (var i = 1; i < 14; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        using var host = Show(shell, 1100);
        var panel = Panel(host);
        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset);

        // Down (or right) shows the tabs further right, by 80 a notch, clamped; the active tab may leave the view.
        var strip = BoundsIn(host, panel).Center;
        host.Window.MouseWheel(strip, new Vector(0, -1));
        DiffHost.Settle();
        Assert.Equal(58, panel.Offset, 0.5);
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
        host.Window.MouseWheel(strip, new Vector(0, 1));
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset, 0.5);
        host.Window.MouseWheel(strip, new Vector(-1, 0));
        DiffHost.Settle();
        Assert.Equal(58, panel.Offset, 0.5);

        // Without an overflow the wheel moves nothing.
        shell.CloseTabCommand.Execute(shell.Tabs[13]);
        DiffHost.Settle();
        host.Window.MouseWheel(strip, new Vector(0, -1));
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset);
    }

    [AvaloniaFact]
    public void A_tab_that_asks_to_be_brought_into_view_is()
    {
        var shell = Create(new SessionState());
        for (var i = 1; i < 14; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        using var host = Show(shell, 1100);
        var panel = Panel(host);
        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset);

        var lastButton = Tabs(host)[13].GetVisualDescendants().OfType<Button>().First(button => button.Name == "TabButton");
        lastButton.BringIntoView();
        DiffHost.Settle();
        Assert.Equal(58, panel.Offset, 0.5);

        // A tab button that takes the focus is brought into view too.
        var firstButton = Tabs(host)[0].GetVisualDescendants().OfType<Button>().First(button => button.Name == "TabButton");
        firstButton.Focus(Avalonia.Input.NavigationMethod.Tab);
        DiffHost.Settle();
        Assert.Equal(0, panel.Offset, 0.5);
    }

    [AvaloniaFact]
    public void A_dashboards_tab_has_its_icon_padding_id_and_item_status()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(null, "web"), new TabState(null)], ActiveTab = 0 }, Web());
        using var host = Show(shell, 1100);

        var tabs = Tabs(host);
        var dashboard = tabs[0];
        var button = Named<Button>(dashboard, "TabButton");
        var icon = Named<Icon>(dashboard, "TabIcon");
        var title = Named<TextBlock>(dashboard, "TabTitle");
        Assert.Equal("DashboardTab", AutomationProperties.GetAutomationId(button));
        Assert.Equal("Web", AutomationProperties.GetName(button));
        Assert.Equal("Web", ToolTip.GetTip(button));
        Assert.Equal("Workspace", AutomationProperties.GetItemStatus(button));
        Assert.Equal(new Thickness(32, 0, 8, 0), button.Padding);
        Assert.True(icon.IsEffectivelyVisible);
        Assert.False(icon.IsHitTestVisible);
        var iconBounds = BoundsIn(host, icon);
        Assert.Equal(new Size(12, 12), iconBounds.Size);
        Assert.Equal(14, iconBounds.X - BoundsIn(host, dashboard).X, 0.5);
        Assert.Equal(6, BoundsIn(host, title).X - iconBounds.Right, 0.5);
        Assert.Equal(120, dashboard.Bounds.Width, 0.5);
        DiffColors.AssertBrush(Color.Parse("#E4E7EC"), icon.Foreground, "the active dashboard tab's icon");

        // An empty tab: no icon, today's padding, its id and no item status.
        var other = Named<Button>(tabs[1], "TabButton");
        Assert.Equal("RepoTab", AutomationProperties.GetAutomationId(other));
        Assert.Null(AutomationProperties.GetItemStatus(other));
        Assert.Equal(new Thickness(14, 0, 8, 0), other.Padding);
        Assert.False(Named<Icon>(tabs[1], "TabIcon").IsEffectivelyVisible);

        // The icon takes the title's colour: secondary on a tab that is not active.
        shell.ActivateTabCommand.Execute(shell.Tabs[1]);
        DiffHost.Settle();
        DiffColors.AssertBrush(Color.Parse("#9BA3B0"), icon.Foreground, "an inactive dashboard tab's icon");
        TestPictures.Save(host, "tabs-dashboard");
    }

    [AvaloniaFact]
    public void A_dashboards_title_gets_its_tab_less_69_whole_at_97_and_only_the_ellipsis_at_80()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(null, "web")] }, Web());
        for (var i = 0; i < 10; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        using var host = Show(shell, 1100);
        var dashboard = Tabs(host)[0];
        var title = Named<TextBlock>(dashboard, "TabTitle");
        Assert.Equal(97, dashboard.Bounds.Width, 0.5);
        Assert.Equal(97 - 69, TitleRoom(dashboard), 0.5);
        Assert.Equal(["Web"], DrawnLines.Of(title));
        TestPictures.Save(host, "tabs-eleven");

        shell.NewTabCommand.Execute(null);
        shell.NewTabCommand.Execute(null);
        shell.NewTabCommand.Execute(null);
        DiffHost.Settle();
        Assert.Equal(80, dashboard.Bounds.Width, 0.5);
        Assert.Equal(80 - 69, TitleRoom(dashboard), 0.5);
        Assert.True(title.Bounds.Width <= 80 - 69 + 0.5);
        Assert.Equal(["…"], DrawnLines.Of(title));
        TestPictures.Save(host, "tabs-fourteen-dashboard");
    }

    /// <summary>The room a tab gives its title: the tab button's width less its padding.</summary>
    private static double TitleRoom(Border tab)
    {
        var button = Named<Button>(tab, "TabButton");
        return button.Bounds.Width - button.Padding.Left - button.Padding.Right;
    }

    [AvaloniaFact]
    public async Task In_the_window_a_dashboard_and_ten_tabs_shrink_and_fourteen_scroll()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithWorkspace(data.Path, Web(), "web", [.. Enumerable.Repeat(new TabState(null), 10)]);
        await app.WaitUntilReadyAsync();

        var strip = app.TabStrip();
        Assert.Equal(11, strip.Tabs.Count);
        Assert.Equal([.. Enumerable.Repeat(97.0, 6), .. Enumerable.Repeat(96.0, 5)], strip.Tabs.Select(tab => Math.Round(tab.Bounds.Width)));
        Assert.Equal(0, strip.Tabs[0].Bounds.X, 0.5);
        Assert.Equal(582, strip.Tabs[6].Bounds.X, 0.5);
        Assert.Equal((1064.0, 1098.0), (Math.Round(strip.AddButtonBounds.X), Math.Round(strip.AddButtonBounds.Right)));
        Assert.Equal("Open a new tab", strip.AddButtonName);
        Assert.Equal(0, strip.Offset);
        Assert.False(strip.IsScrolling);

        var web = strip.Tabs[0];
        Assert.Equal(("Web", "Web", "DashboardTab", "Web", "Workspace"), (web.Title, web.DrawnTitle, web.AutomationId, web.ToolTip, web.ItemStatus));
        Assert.True(web.IsDashboard && web.IsActive && web.ShowsCloseButton);
        Assert.Equal(new Size(12, 12), web.IconBounds!.Value.Size);
        Assert.Equal(14, web.IconBounds.Value.X - web.Bounds.X, 0.5);
        Assert.All(strip.Tabs.Skip(1), tab =>
        {
            // A title gets its tab's width less 51 (45 or 46 here): "New tab" (49.7) is cut with "…".
            Assert.Equal(("RepoTab", (string?)null, "New tab"), (tab.AutomationId, tab.ItemStatus, tab.ToolTip));
            Assert.EndsWith("…", tab.DrawnTitle, StringComparison.Ordinal);
            Assert.StartsWith(tab.DrawnTitle.TrimEnd('…'), "New tab", StringComparison.Ordinal);
        });
        TestPictures.Save(app, "strip-eleven-in-the-window");

        for (var i = 0; i < 3; i++)
        {
            app.Click(app.Find<Button>("AddRepoButton"));
        }

        strip = app.TabStrip();
        Assert.True(strip.IsScrolling);
        Assert.Equal(58, strip.Offset, 0.5);
        Assert.Equal(13, strip.ActiveIndex);
        Assert.Equal((982.0, 1062.0), (Math.Round(strip.Tabs[13].Bounds.X), Math.Round(strip.Tabs[13].Bounds.Right)));
        Assert.Equal(-58, strip.Tabs[0].Bounds.X, 0.5);
        Assert.Equal("…", strip.Tabs[0].DrawnTitle);
        Assert.Equal((1064.0, 1098.0), (Math.Round(strip.AddButtonBounds.X), Math.Round(strip.AddButtonBounds.Right)));
        TestPictures.Save(app, "strip-fourteen-in-the-window");
    }

    private static List<double> Natural(int count) => [.. Enumerable.Repeat(120.0, count)];

    private static WorkspaceList Web() => new() { Workspaces = [new Workspace("web", "Web", [])] };

    private static MainWindowViewModel Create(SessionState state, WorkspaceList? workspaces = null)
    {
        var session = new MemorySession(state);
        var settings = new MainWindowViewModelTests.FakeSettings();
        var tabs = new TabServices(new NoRepositories(), new NoFolders(), settings, session, new DateDisplay(TimeZoneInfo.Utc), workspaces: new InMemoryWorkspaceStore(workspaces));
        return new MainWindowViewModel(settings, new MainWindowViewModelTests.FakeThemes(), _ => Task.FromResult(GitFound), NullAppLog.Instance, tabs, session);
    }

    /// <summary>The strip alone at the window's text size (12.5), <paramref name="width"/> wide.</summary>
    private static DiffHost Show(MainWindowViewModel shell, double width)
    {
        var view = new RepoTabsView { DataContext = shell, FontSize = 12.5, Height = 36, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        return DiffHost.Show(view, width, 100, ThemeVariant.Dark);
    }

    private static RepoTabStripPanel Panel(DiffHost host) => host.Window.GetVisualDescendants().OfType<RepoTabStripPanel>().Single();

    private static List<Border> Tabs(DiffHost host) =>
        [.. Panel(host).Children.Select(child => child.GetVisualDescendants().OfType<Border>().First(border => border.Name == "RepoTab"))];

    private static Button AddButton(DiffHost host) => host.Window.GetVisualDescendants().OfType<Button>().First(button => button.Name == "AddRepoButton");

    private static Rect BoundsIn(DiffHost host, Visual visual) => new(host.ToWindow(visual, default), visual.Bounds.Size);

    private static T Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>The width <paramref name="title"/>'s whole text takes in its own font.</summary>
    private static double TextWidth(TextBlock title)
    {
        var block = new TextBlock { Text = title.Text, FontSize = title.FontSize, FontFamily = title.FontFamily, FontWeight = title.FontWeight };
        block.Measure(Size.Infinity);
        return block.DesiredSize.Width;
    }

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotARepositoryException(path);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : VisualCommit.App.Services.IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
