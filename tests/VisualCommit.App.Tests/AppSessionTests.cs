using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What <see cref="AppSession.Start"/> wires that no view model sees (docs/phase-6-design.md 18,
/// the lead's): the workspaces kept in <c>workspaces.json</c> beside the session (D189, 1.3), the
/// theme's start-up line and its service's end (11.5), and the auto-fetch variable's start-up line (7.3).
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

    /// <summary>A session as <see cref="ShellDriver.Start"/> makes one, with <paramref name="readEnvironment"/> as its environment reader.</summary>
    private static AppSession StartSession(string dataDirectory, Func<string, string?>? readEnvironment) =>
        AppSession.Start(
            new AppPaths(dataDirectory),
            Avalonia.Application.Current!,
            new FakeFolderPicker(),
            new FakeFileLauncher(),
            new FakeClipboard(),
            gitEnvironment: GitIsolation.SessionGitEnvironment(dataDirectory),
            systemTheme: new FakeSystemTheme(true),
            tools: ToolCatalogues.Fixed,
            readEnvironment: readEnvironment);

    private static List<string> AutoFetchLines(string dataDirectory) =>
        [.. AppLogFile.ReadLines(dataDirectory).Where(entry => entry.Contains(AutoFetchScheduler.MinutesVariable, StringComparison.Ordinal))];
}
