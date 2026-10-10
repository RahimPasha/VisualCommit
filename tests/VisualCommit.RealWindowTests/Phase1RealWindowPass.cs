using System.Globalization;
using System.Text.RegularExpressions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.App.Services;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 1's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase1Run : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: 1);

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The real-window pass of phase 1: the checks of docs/test-reports/phase-1.md marked for it,
/// repeated in the real app on the Windows desktop with the real mouse and keyboard. Screenshots
/// go to artifacts/visual/phase-1/real-window/ and are compared with the scripted walk-through's
/// of the same step (D33), which must have run first (<c>dotnet test</c> at the repo root).
/// </summary>
public class Phase1RealWindowPass(Phase1Run fixture) : IClassFixture<Phase1Run>
{
    private const int Phase = 1;

    /// <summary>The top of the graph's rows in the window: tabs 36, toolbar 52, column headers 28.</summary>
    private const float RowsTop = 36 + 52 + 28;

    private const float RowHeight = 26;

    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    private RealWindowRun Run => fixture.Run;

    /// <summary>The middle of row <paramref name="row"/> of the graph, at the height of a click.</summary>
    private static float RowY(int row) => RowsTop + (row * RowHeight) + (RowHeight / 2);

    [Fact]
    public async Task Checks_1_3_and_6_graph_details_and_filter()
    {
        using var repo = await Scenarios.GraphAsync();

        // Check 1: the graph scenario's graph.
        var data = Run.NewDataDirectory();
        using (var app = LaunchWithTabs(data, repo.Path))
        {
            Run.NoteDisplay(app);
            WaitForLog(app, data, "graph: loaded 13 commits");
            app.MoveMouseTo(130, 687);
            using var graph = app.CaptureClient();
            Run.Save(graph, "01-graph-dark-1100x700");
            Run.AssertMatchesScripted(graph, "01-graph-dark-1100x700", "01-graph-dark-1100x700");
            Assert.Equal(0, app.Close());
        }

        // Check 3: click a row with the real mouse, then switch the file list to a tree.
        data = Run.NewDataDirectory();
        using (var app = LaunchWithTabs(data, repo.Path))
        {
            WaitForLog(app, data, "graph: loaded 13 commits");
            app.ClickAt(500, RowY(2));
            app.WaitFor(
                () => app.FindAll("ChangedFilesTitle").Any(title => title.Name == "Changed files (5)") && app.FindAll("ChangedFile").Count == 5,
                "the five changed files of \"Add settings page\"",
                LoadTimeout);
            using (var flat = app.CaptureClient())
            {
                Run.Save(flat, "03a-details-flat");
                Run.AssertMatchesScripted(flat, "03a-details-flat", "03a-details-flat");
            }

            app.ClickWithMouse(app.Find("TreeFilesButton"));

            // Away from the button, whose tooltip would otherwise be in the picture.
            app.MoveMouseTo(130, 687);
            using (var tree = app.CaptureClient())
            {
                Run.Save(tree, "03b-details-tree");
                Run.AssertMatchesScripted(tree, "03b-details-tree", "03b-details-tree");
            }

            Assert.Equal(0, app.Close());
            Assert.Equal(FileListMode.Tree, new JsonSettingsStore(new Core.AppPaths(data).SettingsFile).Current.FileList);
        }

        // Check 6: type into the filter with the real keyboard, then clear it.
        data = Run.NewDataDirectory();
        using (var app = LaunchWithTabs(data, repo.Path))
        {
            WaitForLog(app, data, "graph: loaded 13 commits");
            app.ClickWithMouse(app.Find("FilterBox"));
            app.TypeText("sea");
            app.MoveMouseTo(130, 687);
            using (var filtered = app.CaptureClient())
            {
                Run.Save(filtered, "06a-filter-sea");
                Run.AssertMatchesScripted(filtered, "06a-filter-sea", "06a-filter-sea");
            }

            app.ClickWithMouse(app.Find("FilterBox"));
            app.PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            app.PressKey(VirtualKeyShort.BACK);
            app.MoveMouseTo(130, 687);
            using (var cleared = app.CaptureClient())
            {
                Run.Save(cleared, "06b-filter-cleared");
                Run.AssertMatchesScripted(cleared, "06b-filter-cleared", "06b-filter-cleared");
            }

            Assert.Equal(0, app.Close());
        }
    }

    [Fact]
    public async Task Checks_8_and_9_the_100k_commit_repo_and_Q1()
    {
        var large = await LargeHistory.GetAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, large);

        // Check 8, step 2: the top. The whole history is loaded first, so the scroll bar is the
        // same as in the scripted screenshot.
        WaitForLog(app, data, "large-history-v1: loaded 100000 commits");
        app.MoveMouseTo(130, 687);
        using (var top = app.CaptureClient())
        {
            Run.Save(top, "08a-large-top");
            Run.AssertMatchesScripted(top, "08a-large-top", "08a-large-top");
        }

        // Step 3: ten notches of the real wheel.
        app.ScrollWheel(500, 300, -10);
        app.MoveMouseTo(130, 687);
        using (var wheel = app.CaptureClient())
        {
            Run.Save(wheel, "08b-large-wheel");
            Run.AssertMatchesScripted(wheel, "08b-large-wheel", "08b-large-wheel");
        }

        // Step 4: the tag in the middle, clicked in the left panel. Inspected by eye.
        app.ClickWithMouse(app.FindAll("RefItem").First(item => item.Name == LargeHistory.MiddleTag));
        using (var middle = app.CaptureClient())
        {
            Run.Save(middle, "08c-large-middle");
        }

        // Steps 5 and 6: End and Home in the graph.
        app.ClickAt(500, RowY(2));
        app.PressKey(VirtualKeyShort.END);
        app.MoveMouseTo(130, 687);
        using (var end = app.CaptureClient())
        {
            Run.Save(end, "08d-large-end");
            Run.AssertMatchesScripted(end, "08d-large-end", "08d-large-end");
        }

        app.PressKey(VirtualKeyShort.HOME);
        app.MoveMouseTo(130, 687);
        using (var home = app.CaptureClient())
        {
            Run.Save(home, "08e-large-home");
            Run.AssertMatchesScripted(home, "08e-large-home", "08e-large-home");
        }

        // Check 9: scroll through the history with the real wheel, close, and read the app's
        // own measurements (D50).
        app.ScrollWheel(500, 300, -200);
        app.ScrollWheel(500, 300, 200);
        Assert.Equal(0, app.Close());

        var log = RealApp.ReadLog(data);
        var firstRows = Number(log, @"large-history-v1: first graph rows drawn after (\d+) ms");
        var frames = Regex.Match(log, @"large-history-v1: graph frames: (\d+) drawn, 95th percentile ([\d.]+) ms, longest ([\d.]+) ms");
        Assert.True(frames.Success, "The log has no graph frame statistics for the 100k-commit repo.");
        var p95 = double.Parse(frames.Groups[2].Value, CultureInfo.InvariantCulture);
        var longest = double.Parse(frames.Groups[3].Value, CultureInfo.InvariantCulture);
        Run.Note(string.Create(CultureInfo.InvariantCulture, $"Q1: first graph rows of the 100k-commit repo after {firstRows} ms (at most 2000)"));
        Run.Note(string.Create(CultureInfo.InvariantCulture, $"Q1: {frames.Groups[1].Value} graph frames, 95th percentile {p95} ms (at most 8), longest {longest} ms (at most 33)"));
        Assert.InRange(firstRows, 0, 2000);
        Assert.InRange(p95, 0, 8);
        Assert.InRange(longest, 0, 33);
    }

    [Fact]
    public async Task Check_11_open_with_the_native_folder_dialog()
    {
        using var linear = await Scenarios.LinearAsync();
        using var plain = new TempDirectory("plain");
        var data = Run.NewDataDirectory();
        new JsonSettingsStore(new Core.AppPaths(data).SettingsFile).Update(settings => settings with { Theme = AppTheme.Light });

        using var app = RealApp.Launch(data);
        app.SetClientSize(1100, 700);

        app.ClickWithMouse(app.Find("OpenRepoButton"));
        app.ChooseFolderInDialog("Open a repository", plain.Path, LoadTimeout);
        app.WaitFor(() => app.Texts().Contains($"{plain.Path} is not a git repository."), "the error for a plain folder", LoadTimeout);
        app.MoveMouseTo(130, 687);
        using (var error = app.CaptureClient())
        {
            Run.Save(error, "11a-not-a-repository");
        }

        app.ClickWithMouse(app.Find("OpenRepoButton"));
        app.ChooseFolderInDialog("Open a repository", linear.Path, LoadTimeout);
        WaitForLog(app, data, "linear: loaded 3 commits");
        app.MoveMouseTo(130, 687);
        using (var opened = app.CaptureClient())
        {
            Run.Save(opened, "11b-linear-opened");
            Run.AssertMatchesScripted(opened, "11b-linear-opened", "11b-linear-opened");
        }

        Assert.Equal(0, app.Close());
        var session = new JsonSessionStore(new Core.AppPaths(data).SessionFile).Current;
        Assert.Equal(linear.Path, Assert.Single(session.Tabs).RepositoryPath);
        Assert.Equal(linear.Path, Assert.Single(session.Recent).Path);
    }

    [Fact]
    public async Task Checks_15_and_16_tabs_window_and_panels_across_a_real_restart()
    {
        using var graph = await Scenarios.GraphAsync();
        using var linear = await Scenarios.LinearAsync();
        var large = await LargeHistory.GetAsync();

        // Check 15: three tabs, the second active; then the first; then a real restart.
        var data = Run.NewDataDirectory();
        using (var app = RealApp.Launch(data, new SessionState
        {
            Tabs = [new TabState(graph.Path), new TabState(linear.Path), new TabState(large)],
            ActiveTab = 1,
        }))
        {
            app.SetClientSize(1100, 700);
            WaitForLog(app, data, "large-history-v1: loaded 100000 commits");
            WaitForLog(app, data, "linear: loaded 3 commits");
            WaitForLog(app, data, "graph: loaded 13 commits");
            app.MoveMouseTo(130, 687);
            using (var three = app.CaptureClient())
            {
                Run.Save(three, "15a-three-tabs");
                Run.AssertMatchesScripted(three, "15a-three-tabs", "15a-three-tabs");
            }

            var tabs = app.FindAll("RepoTab").OrderBy(tab => app.BoundsOf(tab).X).ToList();
            Assert.Equal(["graph", "linear", "large-history-v1"], tabs.Select(tab => tab.Name));
            app.ClickWithMouse(tabs[0]);
            Assert.Equal(0, app.Close());
        }

        using (var app = RealApp.Launch(data))
        {
            WaitForLog(app, data, "graph: loaded 13 commits", occurrences: 2);
            app.MoveMouseTo(130, 687);
            using var restarted = app.CaptureClient();
            Run.Save(restarted, "15c-restarted");
            Run.AssertMatchesScripted(restarted, "15c-restarted", "15c-restarted");
            Assert.Equal(["graph", "linear", "large-history-v1"], app.FindAll("RepoTab").OrderBy(tab => app.BoundsOf(tab).X).Select(tab => tab.Name));
            Assert.Equal(0, app.Close());
        }

        // Check 16: panel widths, window size and position across a real restart. 1400x900
        // instead of 1920x1080, which does not fit the development screen.
        data = Run.NewDataDirectory();
        System.Drawing.Rectangle placed;
        using (var app = RealApp.Launch(data))
        {
            app.SetClientSize(1400, 900);
            var middle = 36 + 52 + ((900 - 36 - 52 - 26) / 2f);
            app.DragWithMouse(260, middle, 320, middle);
            app.DragWithMouse(1400 - 400, middle, 1400 - 460, middle);
            app.SetClientSize(1300, 800);
            app.Move(app.ScreenBounds.X + 60, app.ScreenBounds.Y + 40);
            placed = app.ScreenBounds;
            Assert.Equal(0, app.Close());
        }

        using (var app = RealApp.Launch(data))
        {
            app.MoveMouseTo(130, 787);
            Assert.InRange(app.ClientSize.Width, 1299, 1301);
            Assert.InRange(app.ClientSize.Height, 799, 801);
            Assert.Equal(placed.Location, app.ScreenBounds.Location);
            Run.Note($"Check 16: the window was moved to {placed.Location} and came back at {app.ScreenBounds.Location}, {app.ClientSize.Width:F0}x{app.ClientSize.Height:F0}.");
            using var restarted = app.CaptureClient();
            Run.Save(restarted, "16b-restarted");
            Run.AssertMatchesScripted(restarted, "16b-restarted", "16b-restarted");
            Assert.Equal(0, app.Close());
        }
    }

    [Fact]
    public async Task Check_18_a_commit_made_in_a_terminal_appears()
    {
        using var repo = await Scenarios.GraphAsync();
        var data = Run.NewDataDirectory();
        using var app = LaunchWithTabs(data, repo.Path);
        WaitForLog(app, data, "graph: loaded 13 commits");
        app.ClickAt(500, RowY(2));

        await repo.CommitFileAsync("terminal.txt", "from a terminal\n", "Commit from a terminal");
        WaitForLog(app, data, "graph: loaded 14 commits");

        // The file is written before it is committed: with the working tree watched (phase 2,
        // D71) a working-changes row can show in between, until the next refresh.
        app.WaitFor(
            () => Regex.Matches(RealApp.ReadLog(data), @"graph: working tree: \d+ staged, \d+ unstaged") is { Count: > 0 } matches
                && matches[^1].Value.EndsWith(": 0 staged, 0 unstaged", StringComparison.Ordinal),
            "a clean working tree",
            LoadTimeout);
        using (var appeared = app.CaptureClient())
        {
            Run.Save(appeared, "18a-terminal-commit");
            Run.AssertMatchesScripted(appeared, "18a-terminal-commit", "18a-terminal-commit");
        }

        Assert.Equal(0, app.Close());
    }

    /// <summary>Starts the app with one tab on <paramref name="repository"/>, in a 1100x700 window.</summary>
    private static RealApp LaunchWithTabs(string data, string repository)
    {
        var app = RealApp.Launch(data, new SessionState { Tabs = [new TabState(repository)] });
        app.SetClientSize(1100, 700);
        return app;
    }

    /// <summary>Waits until the app's log holds <paramref name="text"/> at least <paramref name="occurrences"/> times.</summary>
    private static void WaitForLog(RealApp app, string data, string text, int occurrences = 1) =>
        app.WaitFor(
            () => Regex.Matches(RealApp.ReadLog(data), Regex.Escape(text)).Count >= occurrences,
            $"the log line \"{text}\"",
            LoadTimeout);

    private static long Number(string log, string pattern)
    {
        var match = Regex.Match(log, pattern);
        Assert.True(match.Success, $"The log has no line matching {pattern}.");
        return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
