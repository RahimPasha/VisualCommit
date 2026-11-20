using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What <see cref="AppSession.Start"/> wires that no view model sees (docs/phase-6-design.md 18,
/// the lead's): the workspaces kept in <c>workspaces.json</c> beside the session (D189, 1.3), the
/// theme's start-up line and its service's end (11.5), the auto-fetch variable's start-up line (7.3),
/// the hosting session with its two start-up lines (docs/phase-7a-design.md 7.11), and what the
/// start-up timeline is told (docs/phase-8a-design.md 1.9, 1.10).
/// </summary>
public class AppSessionTests
{
    [AvaloniaFact]
    public void A_session_reads_its_workspaces_from_workspaces_json_and_saves_a_change_there()
    {
        using var data = new TempDirectory("data");
        var web = new Workspace("w-web", "Web", [new WorkspaceRepository(Path.Combine(data.Path, "api")), new WorkspaceRepository(Path.Combine(data.Path, "docs"))]);
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [web] });

        using (var app = ShellDriver.Start(data.Path))
        {
            var current = app.Session.Workspaces.Current;
            Assert.Equal(["Web"], current.Workspaces.Select(workspace => workspace.Name));
            Assert.Equal(web.Repositories, current.Workspaces[0].Repositories);
            Assert.False(app.Session.Workspaces.IsReadOnly);

            app.Session.Workspaces.Update(list => list with { Workspaces = [.. list.Workspaces, new Workspace("w-tools", "Tools", [])] });
        }

        var saved = new JsonWorkspaceStore(new AppPaths(data.Path).WorkspacesFile).Current;
        Assert.Equal(["Web", "Tools"], saved.Workspaces.Select(workspace => workspace.Name));
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.System, false, "Theme: System (the system's theme is light).")]
    [InlineData(AppTheme.System, true, "Theme: System (the system's theme is dark).")]
    [InlineData(AppTheme.Light, true, "Theme: Light.")]
    [InlineData(AppTheme.Dark, false, "Theme: Dark.")]
    public void A_start_logs_the_theme_it_applied_and_a_closed_session_no_longer_follows_the_system(AppTheme theme, bool systemDark, string line)
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(new AppPaths(data.Path).SettingsFile).Update(settings => settings with { Theme = theme });

        var app = ShellDriver.Start(data.Path, systemDark: systemDark);
        var themes = app.Session.Themes;
        var changes = 0;
        themes.Changed += (_, _) => changes++;
        app.Dispose();
        app.SystemTheme.Set(!systemDark);

        // The line comes once, at the start; after the session the system's change reaches nothing.
        var lines = AppLogFile.ReadLines(data.Path).ToList();
        var themeLine = Assert.Single(lines, entry => entry.Contains("] Theme: ", StringComparison.Ordinal));
        Assert.EndsWith("] " + line, themeLine, StringComparison.Ordinal);
        Assert.True(lines.IndexOf(themeLine) < lines.FindIndex(entry => entry.EndsWith("] VisualCommit closed.", StringComparison.Ordinal)));
        Assert.Equal(0, changes);
        Assert.Equal(theme == AppTheme.Dark || theme == AppTheme.System && systemDark, themes.IsDark);
    }

    [AvaloniaTheory]
    [InlineData("15", "VISUALCOMMIT_AUTO_FETCH_MINUTES=15 overrides the auto-fetch setting (20 minutes).")]
    [InlineData("0", "VISUALCOMMIT_AUTO_FETCH_MINUTES=0 overrides the auto-fetch setting (20 minutes).")]
    [InlineData("soon", null)]
    [InlineData(null, null)]
    public void A_whole_number_in_the_auto_fetch_variable_is_logged_once_at_the_start_through_the_reader_the_scheduler_gets(string? value, string? line)
    {
        // docs/phase-6-design.md 7.3: said once, after the settings are loaded; the line and the
        // scheduler's interval read one source, so no test changes the process's environment.
        using var data = new TempDirectory("data");
        new JsonSettingsStore(new AppPaths(data.Path).SettingsFile).Update(settings => settings with { AutoFetchMinutes = 20 });
        Func<string, string?> reader = name => name == AutoFetchScheduler.MinutesVariable ? value : null;

        using (var session = StartSession(data.Path, reader))
        {
            Assert.Same(reader, session.Shell.AutoFetch.ReadEnvironment);
        }

        var lines = AutoFetchLines(data.Path);
        if (line is null)
        {
            Assert.Empty(lines);
        }
        else
        {
            Assert.EndsWith("] " + line, Assert.Single(lines), StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public void Without_a_reader_the_line_says_the_processs_own_value()
    {
        // The test processes set the variable to 0 (D112; AutoFetchSchedulerTests.The_test_processes_turn_it_off).
        using var data = new TempDirectory("data");

        StartSession(data.Path, readEnvironment: null).Dispose();

        Assert.EndsWith("] VISUALCOMMIT_AUTO_FETCH_MINUTES=0 overrides the auto-fetch setting (5 minutes).", Assert.Single(AutoFetchLines(data.Path)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void In_the_tests_environment_the_session_has_GitHub_off_and_its_tokens_in_memory()
    {
        // docs/phase-7a-design.md 7.11: with neither given, both come from the process's
        // environment, which the test processes set to no GitHub and tokens in memory (1.8).
        using var data = new TempDirectory("data");

        using (var session = StartSession(data.Path, readEnvironment: null))
        {
            Assert.IsType<VisualCommit.Hosting.HostingSession>(session.Services.Hosting);
            Assert.False(session.Services.Hosting.BindsGit);
        }

        var lines = AppLogFile.ReadLines(data.Path).ToList();
        Assert.EndsWith("] GitHub: off.", Assert.Single(lines, entry => entry.Contains("] GitHub: ", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.EndsWith("] Token store: memory.", Assert.Single(lines, entry => entry.Contains("] Token store: ", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_session_tells_its_timeline_the_window_git_found_the_restored_repositories_and_their_first_rows()
    {
        // docs/phase-8a-design.md 1.9, 1.10: WindowOpened on the window's Opened, GitFound once the
        // first search found a usable git (the machine's isolated one), TabsRestored with the tabs
        // that open a repository (the dashboard is not counted) and FirstRows once per repository.
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.LinearAsync();
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [new Workspace("w1", "W one", [])] });
        ShellDriver.WriteSession(data.Path, new SessionState { Tabs = [new TabState(repo.Path), new TabState(null, "w1")], ActiveTab = 0 });
        var timeline = new RecordingTimeline();

        using var session = StartSession(data.Path, readEnvironment: null, startup: timeline);
        session.MainWindow.Show();
        await session.Initialization;

        Assert.Same(timeline, session.Services.Startup);
        Assert.Equal(GitAvailability.Available, session.Shell.Git?.Availability);
        Assert.Equal(1, timeline.WindowOpened);
        Assert.Equal(1, timeline.GitFound);
        Assert.Equal([1], timeline.TabsRestored);

        // The restored dashboards come first in the strip (docs/phase-6-design.md 1.11).
        var repository = Assert.Single(session.Shell.Tabs, tab => tab.Repository is not null).Repository!;
        repository.OnRowsDrawn();
        repository.OnRowsDrawn();
        Assert.Equal([repository.Name], timeline.FirstRows);
    }

    [AvaloniaFact]
    public async Task A_first_search_that_finds_no_usable_git_tells_the_timeline_nothing_of_git()
    {
        using var data = new TempDirectory("data");
        var timeline = new RecordingTimeline();
        var probe = new AnsweringProbe(new GitDetection(GitAvailability.NotFound, null, null, null));

        using var session = StartSession(data.Path, readEnvironment: null, startup: timeline, gitProbe: probe);
        session.MainWindow.Show();
        await session.Initialization;

        Assert.Same(probe.Answer, session.Shell.Git);
        Assert.Equal(1, timeline.WindowOpened);
        Assert.Equal(0, timeline.GitFound);
        Assert.Equal([0], timeline.TabsRestored);
    }

    /// <summary>
    /// A session as <see cref="ShellDriver.Start"/> makes one, with <paramref name="readEnvironment"/>
    /// as its environment reader, <paramref name="startup"/> as its start-up timeline and
    /// <paramref name="gitProbe"/> as its search for git (each null: the session's default).
    /// </summary>
    private static AppSession StartSession(
        string dataDirectory,
        Func<string, string?>? readEnvironment,
        IStartupTimeline? startup = null,
        IGitSetupProbe? gitProbe = null) =>
        AppSession.Start(
            new AppPaths(dataDirectory),
            Avalonia.Application.Current!,
            new FakeFolderPicker(),
            new FakeFileLauncher(),
            new FakeClipboard(),
            gitEnvironment: GitIsolation.SessionGitEnvironment(dataDirectory),
            systemTheme: new FakeSystemTheme(true),
            tools: ToolCatalogues.Fixed,
            readEnvironment: readEnvironment,
            gitProbe: gitProbe,
            startup: startup);

    private static List<string> AutoFetchLines(string dataDirectory) =>
        [.. AppLogFile.ReadLines(dataDirectory).Where(entry => entry.Contains(AutoFetchScheduler.MinutesVariable, StringComparison.Ordinal))];

    /// <summary>A timeline that keeps what it was told; <see cref="IStartupTimeline.GitFound"/> may come from another thread.</summary>
    private sealed class RecordingTimeline : IStartupTimeline
    {
        private readonly Lock _gate = new();
        private readonly List<int> _tabsRestored = [];
        private readonly List<string> _firstRows = [];
        private int _windowOpened;
        private int _gitFound;

        public int WindowOpened => Volatile.Read(ref _windowOpened);

        public int GitFound => Volatile.Read(ref _gitFound);

        public IReadOnlyList<int> TabsRestored
        {
            get
            {
                lock (_gate)
                {
                    return [.. _tabsRestored];
                }
            }
        }

        public IReadOnlyList<string> FirstRows
        {
            get
            {
                lock (_gate)
                {
                    return [.. _firstRows];
                }
            }
        }

        void IStartupTimeline.WindowOpened() => Interlocked.Increment(ref _windowOpened);

        void IStartupTimeline.GitFound() => Interlocked.Increment(ref _gitFound);

        void IStartupTimeline.TabsRestored(int count)
        {
            lock (_gate)
            {
                _tabsRestored.Add(count);
            }
        }

        void IStartupTimeline.FirstRows(string repositoryName)
        {
            lock (_gate)
            {
                _firstRows.Add(repositoryName);
            }
        }
    }

    /// <summary>A search for git that answers with one detection.</summary>
    private sealed class AnsweringProbe(GitDetection answer) : IGitSetupProbe
    {
        public GitDetection Answer { get; } = answer;

        public Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken) =>
            Task.FromResult(Answer);

        public GitGuidance ReadGuidance() => new(GuidancePlatform.Linux, null, null);
    }
}
