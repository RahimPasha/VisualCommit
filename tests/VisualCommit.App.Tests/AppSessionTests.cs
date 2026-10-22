using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What <see cref="AppSession.Start"/> wires that no view model sees (docs/phase-6-design.md 18,
/// the lead's): the workspaces kept in <c>workspaces.json</c> beside the session (D189, 1.3), and the
/// theme's start-up line and its service's end (11.5).
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
}
