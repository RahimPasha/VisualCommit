using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Where the settings screen opens from (docs/phase-6-design.md 7.2, D197, 7.11): the toolbar's
/// <c>SettingsButton</c> at the start of the right-hand group, enabled on every kind of tab and
/// bound without a parameter, with its tooltip, and <c>OpenSettingsCommand</c>, which gives the
/// screen the active tab's repository (none on an empty or a dashboard tab) and opens it on the
/// section it is given.
/// </summary>
public class SettingsEntryTests
{
    [AvaloniaFact]
    public async Task The_settings_button_is_the_first_of_the_right_group_and_enabled_on_every_tab()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var workspaces = new WorkspaceList { Workspaces = [new Workspace("web", "Web", [])] };
        using var app = ShellDriver.StartWithWorkspace(data.Path, workspaces, "web", [new TabState(repo.Path), new TabState(null)]);
        await app.WaitUntilReadyAsync();

        var settings = app.Find<ToolbarButton>("SettingsButton");
        var search = app.Find<ToolbarButton>("SearchButton");
        var theme = app.Find<ToolbarButton>("ThemeSwitch");
        Assert.Equal("Settings", settings.Label);
        Assert.Same(Avalonia.Application.Current!.FindResource("IconSettings"), settings.IconData);
        Assert.Null(settings.CommandParameter);
        Assert.True(app.BoundsOf(settings).Right <= app.BoundsOf(search).X + 0.5);
        Assert.True(app.BoundsOf(search).Right <= app.BoundsOf(theme).X + 0.5);
        Assert.True(app.BoundsOf(theme).Right > app.Window.ClientSize.Width - 20, "Theme still ends within 20 of the right edge.");
        var pop = app.Find<ToolbarButton>("PopButton");
        Assert.True(app.BoundsOf(pop).Right < app.BoundsOf(settings).X, "The left group ends before Settings.");

        foreach (var index in new[] { 0, 1, 2 })
        {
            app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[index]);
            if (index == 1)
            {
                await app.WaitForRepositoryAsync();
            }

            app.Settle();
            Assert.True(settings.IsEffectivelyEnabled, $"Settings is enabled on tab {index}.");
            Assert.Equal(ToolTipText(), ToolTip.GetTip(settings));
            Assert.Equal(ToolTipText(), AutomationProperties.GetHelpText(settings));
            Assert.Equal(KeyTexts.Current.Of(AppCommand.Settings), AutomationProperties.GetAcceleratorKey(settings));
        }
    }

    [Fact]
    public void The_tooltip_names_the_key_in_the_platforms_words()
    {
        // 6.8, 14.11: "Open settings (Ctrl+,)", "Cmd+," on macOS. The key texts come from the key
        // table, which work package WP-K fills (6.4); until it does a command has no key text and
        // the tooltip is the text alone.
        var ctrl = new KeyTexts(CommandKey.Ctrl);
        var cmd = new KeyTexts(CommandKey.Cmd);

        Assert.Contains(ctrl.Of(AppCommand.Settings), new string?[] { null, "Ctrl+," });
        Assert.Contains(cmd.Of(AppCommand.Settings), new string?[] { null, "Cmd+," });
        Assert.Equal(ctrl.Of(AppCommand.Settings) is null ? "Open settings" : "Open settings (Ctrl+,)", MainWindowViewModel.SettingsToolTipFor(ctrl));
        Assert.Equal(cmd.Of(AppCommand.Settings) is null ? "Open settings" : "Open settings (Cmd+,)", MainWindowViewModel.SettingsToolTipFor(cmd));
    }

    [AvaloniaFact]
    public async Task A_click_opens_the_screen_on_general_and_the_command_on_the_section_it_is_given_with_the_active_repository()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();

        var dialog = await app.OpenSettingsAsync();
        Assert.Equal(SettingsSection.General, dialog.SelectedSection);
        Assert.True(dialog.Git.CanUseRepositoryScope);
        Assert.Equal(repository.Name, dialog.Git.RepositoryName);
        Assert.Equal("main", dialog.Git.Branch);
        app.CancelSettings();

        app.Session.Shell.OpenSettingsCommand.Execute(SettingsSection.KeyboardShortcuts);
        var shortcuts = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        Assert.Equal(SettingsSection.KeyboardShortcuts, shortcuts.SelectedSection);
        shortcuts.Cancel();

        // 12.7's "Open settings…" goes through TabServices.OpenSettings, set by the window.
        var opening = app.Session.Services.OpenSettings!(SettingsSection.Tools);
        var tools = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        Assert.Equal(SettingsSection.Tools, tools.SelectedSection);
        tools.Cancel();
        await opening;

        // An empty tab: no repository.
        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[1]);
        app.Session.Shell.OpenSettingsCommand.Execute(null);
        var empty = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        await empty.Loaded;
        Assert.False(empty.Git.CanUseRepositoryScope);
        Assert.Equal(SettingsSection.General, empty.SelectedSection);
        empty.Cancel();
    }

    [AvaloniaFact]
    public async Task On_a_dashboard_tab_the_screen_has_no_repository()
    {
        using var data = new TempDirectory("data");
        var workspaces = new WorkspaceList { Workspaces = [new Workspace("web", "Web", [])] };
        using var app = ShellDriver.StartWithWorkspace(data.Path, workspaces, "web");
        await app.WaitUntilReadyAsync();
        Assert.True(app.Session.Shell.ActiveTab.IsDashboard);

        app.Click(app.FindByAutomationId("SettingsButton"));
        var dialog = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        await dialog.Loaded;

        Assert.False(dialog.Git.CanUseRepositoryScope);
        Assert.Equal("Open a repository's tab to change its own settings", dialog.Git.RepositoryScopeToolTip);
        dialog.Cancel();
    }

    private static string ToolTipText() => MainWindowViewModel.SettingsToolTipFor(KeyTexts.Current);
}
