using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using Xunit;

namespace VisualCommit.VisualTests.Phase1;

/// <summary>
/// The scripted walk-through of phase 1. Each test is one numbered check of
/// docs/test-reports/phase-1.md: it performs the check's steps on the whole app in headless mode,
/// saves a screenshot after every step under artifacts/visual/phase-1/scripted/, and asserts the
/// expected result. This part holds the checks of the shell: the welcome page, opening,
/// initialising and cloning, tabs, restarts, the window and the panels (checks 10 to 17).
/// </summary>
public partial class Phase1Checks
{
    private const int Phase = 1;
    private static readonly TimeSpan CloneTimeout = TimeSpan.FromSeconds(90);

    [AvaloniaFact]
    public async Task Check_10_welcome_page_and_tabs()
    {
        using var graph = await Scenarios.GraphAsync();
        using var linear = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        var opened = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);
        ShellDriver.WriteSession(data.Path, new SessionState
        {
            Recent = [new RecentRepository(graph.Path, "graph", opened.AddHours(1)), new RecentRepository(linear.Path, "linear", opened)],
        });

        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        var welcome = app.Capture();
        welcome.Save(Phase, "10a-welcome-page");
        Assert.Equal(["New tab"], Tabs(app));
        Assert.False(app.Find<Button>("CloseTabButton").IsEffectivelyVisible);
        Assert.Equal(
            [.. ShellExpectations.WelcomeTexts[..6], "graph", graph.Path, "linear", linear.Path],
            ShellExpectations.TextsIn(app.FindByAutomationId("CommitGraph")));
        Assert.Equal(
            ShellExpectations.LeftPanelSections.SelectMany(section => new[] { section, "0" }),
            ShellExpectations.TextsIn(app.FindByAutomationId("LeftPanel")).Where(text => text != "Filter"));
        Assert.Equal(["Commit details", "Select a commit to see its details."], ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal("No repository", app.Find<TextBlock>("CurrentBranch").Text);
        AssertNoClippedText(app);

        app.Click(app.Find<Button>("AddRepoButton"));
        app.Capture().Save(Phase, "10b-second-tab");
        var shell = app.Session.Shell;
        Assert.Equal(["New tab", "New tab"], Tabs(app));
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        Assert.All(CloseButtons(app), button => Assert.True(button.IsEffectivelyVisible));

        app.Click(CloseButtons(app)[1]);
        app.Capture().Save(Phase, "10c-second-tab-closed");
        Assert.Equal(["New tab"], Tabs(app));
        Assert.False(app.Find<Button>("CloseTabButton").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Check_11_open_a_repository()
    {
        using var linear = await Scenarios.LinearAsync();
        using var plain = new TempDirectory("plain");
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });

        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        var tab = app.Session.Shell.ActiveTab;

        app.Folders.Answer(plain.Path);
        app.Click(app.Find<Button>("OpenRepoButton"));
        await app.WaitForAsync(() => tab.Welcome.HasError, "the error of opening a plain folder");
        var error = app.Capture();
        error.Save(Phase, "11a-not-a-repository");
        Assert.Equal($"{plain.Path} is not a git repository.", app.Find<TextBlock>("WelcomeError").Text);
        ShellExpectations.AssertColour(ShellExpectations.Light.Graph, error, ShellExpectations.GraphSamplePoint(ShellExpectations.LeftPanelWidth), "graph area");
        Assert.True(
            ContainsColour(error, app.BoundsOf(app.Find<TextBlock>("WelcomeError")), Palettes.Light.Danger),
            "The error is not drawn in the danger colour.");
        Assert.Equal(["New tab"], Tabs(app));

        app.Folders.Answer(linear.Path);
        app.Click(app.Find<Button>("OpenRepoButton"));
        await WaitForGraphAsync(app, tab, 3);
        app.Capture().Save(Phase, "11b-linear-opened");
        Assert.Equal(["linear"], Tabs(app));
        Assert.Equal(["Describe the project", "Add greeting", "Add README"], Subjects(tab));
        Assert.Equal("main", app.Find<TextBlock>("CurrentBranch").Text);

        app.Click(app.Find<Button>("AddRepoButton"));
        app.Capture().Save(Phase, "11c-recent-in-new-tab");
        var recent = ShellExpectations.TextsIn(app.FindByAutomationId("CommitGraph"));
        Assert.Equal(["linear", linear.Path], recent[6..8]);

        var session = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current;
        Assert.Equal([linear.Path, null], session.Tabs.Select(saved => saved.RepositoryPath));
        Assert.Equal(linear.Path, Assert.Single(session.Recent).Path);
    }

    [AvaloniaFact]
    public async Task Check_12_init_a_repository()
    {
        using var data = new TempDirectory("data");
        using var folders = new TempDirectory("init");
        var fresh = folders.Combine("fresh");
        Directory.CreateDirectory(fresh);

        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var tab = app.Session.Shell.ActiveTab;

        app.Folders.Answer(fresh);
        app.Click(app.Find<Button>("InitRepoButton"));
        await app.WaitForAsync(() => tab.Repository?.HasNoCommits == true, "the new repository to show that it has no commits");
        app.Capture().Save(Phase, "12-initialised");

        Assert.True(Directory.Exists(Path.Combine(fresh, ".git")));
        Assert.Equal(["fresh"], Tabs(app));
        var graphTexts = ShellExpectations.TextsIn(app.FindByAutomationId("CommitGraph"));
        Assert.Contains("No commits yet", graphTexts);
        Assert.Contains("Make the first commit in this repository to see it here.", graphTexts);
        Assert.Equal(
            ShellExpectations.LeftPanelSections.Select(_ => "0"),
            ShellExpectations.TextsIn(app.FindByAutomationId("LeftPanel")).Where(text => text.All(char.IsDigit)));

        var branch = await GitInAsync(fresh, "symbolic-ref", "--short", "HEAD");
        Assert.Equal(branch, app.Find<TextBlock>("CurrentBranch").Text);
    }

    [AvaloniaFact]
    public async Task Check_13_clone_with_progress()
    {
        var source = await LargeHistory.GetAsync();
        using var data = new TempDirectory("data");
        using var parent = new TempDirectory("clones");

        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var tab = app.Session.Shell.ActiveTab;
        var welcome = tab.Welcome;

        app.Click(app.Find<Button>("CloneRepoButton"));
        await app.WaitForAsync(() => app.Find<TextBox>("CloneUrlBox").IsFocused, "the URL field to have the focus");
        app.Capture().Save(Phase, "13a-clone-form");
        Assert.True(welcome.IsCloneFormOpen);
        Assert.Equal(string.Empty, app.Find<TextBox>("CloneUrlBox").Text);

        app.Type(new Uri(source).AbsoluteUri);
        app.Click(app.Find<TextBox>("CloneParentBox"));
        app.Type(parent.Path);
        app.Click(app.Find<TextBox>("CloneNameBox"));
        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type("cloned");
        Assert.Equal("cloned", welcome.CloneName);

        app.Click(app.Find<Button>("CloneStartButton"));
        await app.WaitForAsync(
            () => welcome.IsCloning && welcome.CloneProgressValue is >= 1 and <= 99,
            "the clone to report a percentage between 1 and 99",
            CloneTimeout);
        var during = app.Capture();
        during.Save(Phase, "13b-clone-in-progress");
        Assert.False(app.Find<TextBox>("CloneUrlBox").IsEffectivelyEnabled);
        Assert.True(app.Find<Button>("CloneCancelButton").IsEffectivelyVisible);
        Assert.Matches(@"^[A-Z][a-z]+( [a-z]+)* \d{1,3}%$", app.Find<TextBlock>("CloneProgressText").Text);

        await WaitForGraphAsync(app, tab, LargeHistory.CommitCount, CloneTimeout);
        app.Capture().Save(Phase, "13c-cloned");
        Assert.Equal(["cloned"], Tabs(app));
        Assert.True(Directory.Exists(Path.Combine(parent.Path, "cloned", ".git")));
        Assert.Equal(LargeHistory.SubjectOf(LargeHistory.CommitCount), tab.Repository!.Graph.CommitAt(0).Subject);
        Assert.Contains(app.Session.Shell.ActiveTab.Welcome.Recent, item => item.Name == "cloned");
    }

    [AvaloniaFact]
    public async Task Check_14_a_failed_and_a_cancelled_clone()
    {
        var source = await LargeHistory.GetAsync();
        using var data = new TempDirectory("data");
        using var parent = new TempDirectory("clones");
        var missing = new Uri(parent.Combine("does-not-exist")).AbsoluteUri;
        var expected = await GitCloneErrorAsync(missing);

        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var welcome = app.Session.Shell.ActiveTab.Welcome;
        app.Click(app.Find<Button>("CloneRepoButton"));

        await TypeCloneFormAsync(app, missing, parent.Path, "cloned");
        app.Click(app.Find<Button>("CloneStartButton"));
        await app.WaitForAsync(() => !welcome.IsCloning && welcome.HasError, "the failed clone's error", CloneTimeout);
        app.Capture().Save(Phase, "14a-clone-failed");
        Assert.Contains(expected, app.Find<TextBlock>("CloneError").Text);
        Assert.True(app.Find<TextBox>("CloneUrlBox").IsEffectivelyEnabled);
        Assert.Equal(missing, welcome.CloneUrl);
        Assert.False(Directory.Exists(parent.Combine("cloned")));

        app.Click(app.Find<TextBox>("CloneUrlBox"));
        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type(new Uri(source).AbsoluteUri);
        app.Click(app.Find<Button>("CloneStartButton"));
        await app.WaitForAsync(() => welcome.IsCloning && welcome.CloneProgressText != "Starting", "the clone to report progress", CloneTimeout);
        app.Click(app.Find<Button>("CloneCancelButton"));
        await app.WaitForAsync(() => !welcome.IsCloning, "the clone to stop", CloneTimeout);
        app.Capture().Save(Phase, "14b-clone-cancelled");
        Assert.Equal("Clone cancelled.", app.Find<TextBlock>("CloneError").Text);
        Assert.True(app.Find<TextBox>("CloneUrlBox").IsEffectivelyEnabled);
        Assert.False(Directory.Exists(parent.Combine("cloned")));
    }

    // Not an [AvaloniaFact]: each half runs with a new Application, as after a restart (D32).
    [Fact]
    public async Task Check_15_three_tabs_restored_after_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase1Checks).Assembly;
        using var graph = await Scenarios.GraphAsync();
        using var linear = await Scenarios.LinearAsync();
        var large = await LargeHistory.GetAsync();
        using var data = new TempDirectory("data");

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.StartWithTabs(data.Path, [graph.Path, linear.Path, large], activeTab: 1);
                await app.WaitUntilReadyAsync();
                var shell = app.Session.Shell;

                app.Capture().Save(Phase, "15a-three-tabs");
                Assert.Equal(["graph", "linear", "large-history-v1"], Tabs(app));
                Assert.Same(shell.Tabs[1], shell.ActiveTab);
                Assert.Equal(["Describe the project", "Add greeting", "Add README"], Subjects(shell.ActiveTab));

                app.Click(TabButtons(app)[2]);
                app.Capture().Save(Phase, "15b-third-tab");
                Assert.Same(shell.Tabs[2], shell.ActiveTab);
                Assert.Equal(LargeHistory.CommitCount, shell.ActiveTab.Repository!.Graph.Count);
                Assert.Equal("main", app.Find<TextBlock>("CurrentBranch").Text);

                app.Click(TabButtons(app)[0]);
                Assert.Same(shell.Tabs[0], shell.ActiveTab);
                return true;
            },
            cancellationToken);

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path);
                await app.WaitUntilReadyAsync();
                var shell = app.Session.Shell;

                app.Capture().Save(Phase, "15c-restarted");
                Assert.Equal(["graph", "linear", "large-history-v1"], Tabs(app));
                Assert.Same(shell.Tabs[0], shell.ActiveTab);
                Assert.Equal(13, shell.ActiveTab.Repository!.Graph.Count);
                return true;
            },
            cancellationToken);
    }

    [Fact]
    public async Task Check_16_window_and_panels_restored_after_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase1Checks).Assembly;
        using var data = new TempDirectory("data");

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path, 1920, 1080);
                await app.WaitUntilReadyAsync();
                var middle = app.BoundsOf(app.Find<Grid>("MainArea")).Center.Y;
                app.Drag(new Point(260, middle), new Point(320, middle));
                app.Drag(new Point(1920 - 400, middle), new Point(1920 - 460, middle));
                app.Window.Width = 1300;
                app.Window.Height = 800;
                app.Settle();
                app.Capture().Save(Phase, "16a-before-closing");
                AssertPanels(app, 320, 460);
                return true;
            },
            cancellationToken);

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path, width: null, height: null);
                await app.WaitUntilReadyAsync();
                app.Capture().Save(Phase, "16b-restarted");
                Assert.Equal(new Size(1300, 800), app.Window.ClientSize);
                AssertPanels(app, 320, 460);
                Assert.Equal(520, app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width, 2.0);
                return true;
            },
            cancellationToken);

        var session = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current;
        Assert.Equal(1300, session.Window!.Width);
        Assert.Equal(800, session.Window.Height);
        Assert.Equal(320, session.LeftPanelWidth!.Value, 1.0);
        Assert.Equal(460, session.RightPanelWidth!.Value, 1.0);
    }

    [AvaloniaFact]
    public async Task Check_17_panels_give_way_in_a_narrow_window()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, 1920, 1080);
        await app.WaitUntilReadyAsync();
        var middle = app.BoundsOf(app.Find<Grid>("MainArea")).Center.Y;

        app.Drag(new Point(260, middle), new Point(500, middle));
        app.Drag(new Point(1920 - 400, middle), new Point(1920 - 700, middle));
        app.Capture().Save(Phase, "17a-wide-panels");
        AssertPanels(app, 500, 700);
        Assert.Equal(720, app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width, 2.0);

        app.Window.Width = 1000;
        app.Window.Height = 700;
        app.Settle();
        app.Capture().Save(Phase, "17b-narrow-window");
        AssertPanels(app, 275, 405);
        Assert.Equal(320, app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width, 2.0);
        Assert.True(app.BoundsOf(app.FindByAutomationId("RightPanel")).Right <= 1000 + 0.5, "The right panel is cut off by the window's edge.");
        AssertNoClippedText(app);

        app.Window.Width = 1920;
        app.Window.Height = 1080;
        app.Settle();
        app.Capture().Save(Phase, "17c-wide-again");
        AssertPanels(app, 500, 700);
    }

    private static List<string> Tabs(ShellDriver app) =>
        [.. app.Session.Shell.Tabs.Select(tab => tab.Title)];

    private static List<Button> TabButtons(ShellDriver app) =>
        [.. AllNamed<Button>(app, "TabButton")];

    private static List<Button> CloseButtons(ShellDriver app) =>
        [.. AllNamed<Button>(app, "CloseTabButton")];

    private static IEnumerable<T> AllNamed<T>(ShellDriver app, string name)
        where T : Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(app.Window).OfType<T>()
            .Where(control => control.Name == name)
            .OrderBy(control => app.BoundsOf(control).X);

    private static List<string> Subjects(RepoTabViewModel tab)
    {
        var graph = tab.Repository!.Graph;
        return [.. Enumerable.Range(0, graph.Count).Select(index => graph.CommitAt(index).Subject)];
    }

    private static Task WaitForGraphAsync(ShellDriver app, RepoTabViewModel tab, int count, TimeSpan? timeout = null) =>
        app.WaitForAsync(
            () => tab.Repository is { Graph: { IsComplete: true } graph } && graph.Count == count,
            $"a graph of {count} commits",
            timeout);

    private static void AssertPanels(ShellDriver app, double left, double right)
    {
        Assert.Equal(left, app.BoundsOf(app.FindByAutomationId("LeftPanel")).Width, 2.0);
        Assert.Equal(right, app.BoundsOf(app.FindByAutomationId("RightPanel")).Width, 2.0);
    }

    private static void AssertNoClippedText(ShellDriver app) =>
        Assert.Empty(LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true));

    private static async Task TypeCloneFormAsync(ShellDriver app, string url, string parent, string name)
    {
        await app.WaitForAsync(() => app.Find<TextBox>("CloneUrlBox").IsFocused, "the URL field to have the focus");
        app.Type(url);
        app.Click(app.Find<TextBox>("CloneParentBox"));
        app.Type(parent);
        app.Click(app.Find<TextBox>("CloneNameBox"));
        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type(name);
    }

    private static bool ContainsColour(Screenshot screenshot, Rect area, Avalonia.Media.Color colour)
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

    /// <summary>Runs git in a folder, cut off from the machine as the app is, and returns its trimmed output.</summary>
    private static async Task<string> GitInAsync(string folder, params string[] arguments)
    {
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!);
        var command = new GitCommand(arguments) { WorkingDirectory = folder };
        return (await runner.RunAsync(command, TestContext.Current.CancellationToken)).EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>The "fatal:" line that <c>git clone</c> prints for <paramref name="url"/>, as a terminal would show it.</summary>
    private static async Task<string> GitCloneErrorAsync(string url)
    {
        using var scratch = new TempDirectory("clone-error");
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!);
        var result = await runner.RunAsync(
            new GitCommand("clone", "--", url, "probe") { WorkingDirectory = scratch.Path },
            TestContext.Current.CancellationToken);
        Assert.NotEqual(0, result.ExitCode);
        return result.StandardError.Split('\n').Select(line => line.Trim()).First(line => line.StartsWith("fatal:", StringComparison.Ordinal));
    }
}
