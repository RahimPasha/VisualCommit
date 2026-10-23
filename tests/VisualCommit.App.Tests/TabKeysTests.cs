using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The tab keys (docs/phase-6-design.md 6.5, 6.12, D213): Ctrl+Tab and Ctrl+Shift+Tab round the
/// strip in its order, Mod+1 to Mod+8 and Mod+9, Mod+W, Ctrl+Tab leaving the resolver's result
/// while Ctrl+PageDown switches from there, Mod+Shift+D round the dashboards or to the palette,
/// and the session saved after each switch. The keys share an app start per setup.
/// </summary>
public class TabKeysTests
{
    [AvaloniaFact]
    public async Task Ctrl_Tab_and_the_places_go_round_the_strip_and_the_session_follows()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null, null]);
        await app.WaitForRepositoryAsync();
        var shell = app.Session.Shell;
        var session = app.Session.Services.Session;
        Assert.Equal(3, shell.Tabs.Count);

        // Ctrl+Tab and Ctrl+Shift+Tab, wrapping, in the strip's order; each switch saved.
        foreach (var (shift, expected) in new[] { (false, 1), (false, 2), (false, 0), (true, 2), (true, 1), (true, 0) })
        {
            Assert.True(app.PressKeyHandled(Key.Tab, shift ? RawInputModifiers.Control | RawInputModifiers.Shift : RawInputModifiers.Control));
            Assert.Same(shell.Tabs[expected], shell.ActiveTab);
            Assert.Equal(expected, session.Current.ActiveTab);
        }

        // Mod+1 to Mod+8: the place's tab, or nothing where there is none; Mod+9 the last.
        foreach (var (key, expected) in new[] { (Key.D2, 1), (Key.D3, 2), (Key.D1, 0), (Key.D4, 0), (Key.D8, 0), (Key.D9, 2) })
        {
            Assert.True(app.PressKeyHandled(key, RawInputModifiers.Control));
            Assert.Same(shell.Tabs[expected], shell.ActiveTab);
            Assert.Equal(expected, session.Current.ActiveTab);
        }

        // Ctrl+PageUp and Ctrl+PageDown likewise.
        Assert.True(app.PressKeyHandled(Key.PageUp, RawInputModifiers.Control));
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        Assert.True(app.PressKeyHandled(Key.PageDown, RawInputModifiers.Control));
        Assert.Same(shell.Tabs[2], shell.ActiveTab);

        // Mod+W closes the active tab; on the lone empty tab it does nothing.
        Assert.True(app.PressKeyHandled(Key.W, RawInputModifiers.Control));
        Assert.Equal(2, shell.Tabs.Count);
        app.PressKey(Key.W, RawInputModifiers.Control);
        Assert.Single(shell.Tabs);
        Assert.False(shell.ActiveTab.IsEmpty);
        app.PressKey(Key.W, RawInputModifiers.Control);
        var lone = Assert.Single(shell.Tabs);
        Assert.True(lone.IsEmpty);
        Assert.True(app.PressKeyHandled(Key.W, RawInputModifiers.Control));
        Assert.Same(lone, Assert.Single(shell.Tabs));

        // Mod+Shift+D without a dashboard: the palette at "workspace".
        Assert.True(app.PressKeyHandled(Key.D, RawInputModifiers.Control | RawInputModifiers.Shift));
        Assert.Equal("workspace", app.Palette.Query);
        Assert.Equal((9, 9), (app.PaletteBox.SelectionStart, app.PaletteBox.CaretIndex));
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task In_the_resolvers_result_Ctrl_Tab_leaves_it_and_Ctrl_PageDown_switches()
    {
        using var data = new TempDirectory("data");
        using var conflicts = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var ops = await Scenarios.OperationsAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [conflicts.Path, ops.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var shell = app.Session.Shell;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read");
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");
        app.ClickResultAt(1, 1);
        Assert.True(InResult(app));
        var text = app.ResultText;

        // Ctrl+Tab declines: the result leaves Tab with Control to the navigation (phase 4 design 11.10).
        app.PressKey(Key.Tab, RawInputModifiers.Control);
        Assert.False(InResult(app));
        Assert.Equal(text, app.ResultText);
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
        Assert.Equal(0, app.Session.Services.Session.Current.ActiveTab);

        // Ctrl+PageDown, taken there too, switches; Ctrl+PageUp comes back to the resolver as it was.
        app.ClickResultAt(1, 1);
        Assert.True(app.PressKeyHandled(Key.PageDown, RawInputModifiers.Control));
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        Assert.Equal(1, app.Session.Services.Session.Current.ActiveTab);
        Assert.True(app.PressKeyHandled(Key.PageUp, RawInputModifiers.Control));
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
        Assert.True(repository.IsResolverOpen);
        Assert.Equal(text, app.ResultText);
    }

    [AvaloniaFact]
    public async Task Mod_Shift_D_goes_round_the_dashboards_and_from_another_tab_to_the_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList
        {
            Workspaces =
            [
                new Workspace("web", "Web", [new WorkspaceRepository(repo.Path)]),
                new Workspace("tools", "Tools", [new WorkspaceRepository(repo.Path)]),
            ],
        });
        using var app = ShellDriver.StartWithTabs(data.Path, [new TabState(null, "web"), new TabState(null, "tools")]);
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync();
        var shell = app.Session.Shell;

        foreach (var expected in new[] { "Tools", "Web", "Tools" })
        {
            Assert.True(app.PressKeyHandled(Key.D, RawInputModifiers.Control | RawInputModifiers.Shift));
            Assert.Equal(expected, shell.ActiveTab.Title);
        }

        // From a tab that is no dashboard: the first dashboard.
        Assert.True(app.PressKeyHandled(Key.T, RawInputModifiers.Control));
        Assert.Equal(3, shell.Tabs.Count);
        Assert.True(app.PressKeyHandled(Key.D, RawInputModifiers.Control | RawInputModifiers.Shift));
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
        Assert.Equal(0, app.Session.Services.Session.Current.ActiveTab);
        Assert.False(app.IsPaletteOpen);
    }

    private static bool InResult(ShellDriver app) =>
        app.FocusedElement is Visual focused && focused.GetSelfAndVisualAncestors().OfType<ConflictResultEditor>().Any();
}
