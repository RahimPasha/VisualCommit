using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Where About opens from (D409, D417, D431): the palette lists "About VisualCommit…" once, right
/// after "Follow the system's theme", enabled and keyless, on a welcome, a repository and a dashboard
/// tab, and opens About with the focus on its section button; the macOS application menu, declared
/// by the app, holds one item, "About VisualCommit", and no "About Avalonia"; its command opens About
/// with a shell and does nothing while a dialog is open.
/// </summary>
public sealed class AboutEntryTests
{
    [AvaloniaFact]
    public async Task The_palette_lists_About_once_after_Follow_the_systems_theme_on_every_kind_of_tab()
    {
        using var repo = await Scenarios.GraphAsync();

        // A dashboard and a welcome tab beside it.
        using (var data = new TempDirectory("data"))
        {
            var workspace = new Workspace("solo", "Solo", [new WorkspaceRepository(repo.Path)]);
            using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [workspace] }, openDashboardOf: "solo", extraTabs: [new TabState(null)]);
            await app.WaitUntilReadyAsync();
            await app.WaitForDashboardReadAsync();
            var shell = app.Session.Shell;
            Assert.Contains(shell.Tabs, tab => tab.IsDashboard);
            Assert.Contains(shell.Tabs, tab => tab.IsEmpty);
            foreach (var tab in shell.Tabs.ToList())
            {
                shell.ActivateTabCommand.Execute(tab);
                app.Settle();
                AssertListedOnce(shell, tab.Title);
            }
        }

        // A repository tab; run, the entry opens About with the focus on About's section button.
        using (var data = new TempDirectory("data"))
        {
            using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
            await app.WaitForRepositoryAsync();
            var shell = app.Session.Shell;
            Assert.NotNull(shell.ActiveTab.Repository);
            var entry = AssertListedOnce(shell, shell.ActiveTab.Title);

            entry.Run();
            await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true }, "the settings screen on About");
            await app.WaitForAsync(() => app.FocusedAutomationId() == A.NavAboutId, "the focus on About's button");
            Assert.True(app.FocusVisible, "About's button has the focus without the keyboard's frame.");
        }
    }

    /// <summary>"About VisualCommit…" once among the palette's commands, right after "Follow the system's theme", enabled, keyless, with no detail.</summary>
    private static PaletteEntry AssertListedOnce(MainWindowViewModel shell, string tab)
    {
        var commands = shell.Commands.PaletteCommands();
        var about = Assert.Single(commands, entry => entry.Label == A.PaletteCommand);
        var index = commands.ToList().IndexOf(about);
        Assert.True(index > 0 && commands[index - 1].Label == A.BeforeAbout, $"On {tab} \"{A.PaletteCommand}\" follows \"{(index > 0 ? commands[index - 1].Label : "nothing")}\".");
        Assert.True(about.IsEnabled, $"\"{A.PaletteCommand}\" is disabled on {tab}.");
        Assert.Null(about.KeyText);
        Assert.Null(about.Detail);
        Assert.False(about.IsDestructive);
        Assert.Equal(AppCommand.About, about.Command);
        return about;
    }

    [AvaloniaFact]
    public void The_application_menu_holds_About_VisualCommit_alone()
    {
        var menu = NativeMenu.GetMenu(Application.Current!);
        Assert.NotNull(menu);
        var item = Assert.IsType<NativeMenuItem>(Assert.Single(menu!.Items));
        Assert.Equal(A.MenuItem, item.Header);
        Assert.DoesNotContain(menu.Items.OfType<NativeMenuItem>(), other => other.Header?.Contains("Avalonia", StringComparison.Ordinal) == true);
        Assert.Same(((App.App)Application.Current!).AboutCommand, item.Command);
    }

    [AvaloniaFact]
    public async Task The_menu_items_command_opens_About_and_does_nothing_while_a_dialog_is_open()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var application = (App.App)Application.Current!;
        var command = NativeMenu.GetMenu(application)!.Items.OfType<NativeMenuItem>().Single().Command!;
        var dialogs = app.Session.Services.DialogHost;
        try
        {
            // Without a shell it does nothing.
            application.Shell = null;
            command.Execute(null);
            app.Settle();
            Assert.False(dialogs.IsOpen);

            application.Shell = app.Session.Shell;
            command.Execute(null);
            await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true }, "the settings screen on About");
            await app.WaitForAsync(() => app.FocusedAutomationId() == A.NavAboutId, "the focus on About's button");

            // The settings screen open: nothing more opens.
            command.Execute(null);
            app.Settle();
            Assert.Single(dialogs.Open);

            // Another dialog open over the window: nothing opens either.
            dialogs.CancelCurrent();
            await app.WaitForAsync(() => !dialogs.IsOpen, "the settings screen to close");
            var confirmation = dialogs.ConfirmAsync(new ConfirmationRequest("Discard changes?", "Discard?", string.Empty, "Discard"));
            app.Settle();
            command.Execute(null);
            app.Settle();
            Assert.Single(dialogs.Open);
            Assert.IsType<ConfirmationViewModel>(dialogs.Current);
            dialogs.CancelCurrent();
            Assert.False(await confirmation);
        }
        finally
        {
            application.Shell = null;
        }
    }
}
