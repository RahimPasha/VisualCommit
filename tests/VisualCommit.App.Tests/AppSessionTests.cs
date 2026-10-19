using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What <see cref="AppSession.Start"/> wires that no view model sees (docs/phase-6-design.md 18,
/// the lead's): the workspaces kept in <c>workspaces.json</c> beside the session (D189, 1.3).
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
}
