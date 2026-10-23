using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Tooltips, help texts and accelerator keys in the window (docs/phase-6-design.md 6.8, 6.12, 14.11,
/// 15.5, D199, D215): the toolbar's, the tab strip's, the status bar's toggle and the welcome page's,
/// enabled and disabled; the active and another tab's close button; the commit button; the
/// resolver's five buttons; the interactive list's Cancel; the search bar's match buttons. The
/// expected texts are literals (6.12), in Windows' and Linux's words, the headless platform's.
/// </summary>
public class ShellToolTipTests
{
    [AvaloniaFact]
    public async Task The_shells_buttons_say_what_they_do_with_their_keys_and_a_disabled_one_its_reason()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        var shell = app.Session.Shell;

        // K11 step 2: tooltip = help text, then the AcceleratorKey.
        AssertButtons(
            app,
            ("FetchButton", "Fetch all remotes (Ctrl+Shift+F)", "Ctrl+Shift+F"),
            ("PullButton", "Pull from origin/main (Ctrl+Shift+L)", "Ctrl+Shift+L"),
            ("PushButton", "Push main to origin/main (Ctrl+Shift+K)", "Ctrl+Shift+K"),
            ("BranchButton", "Create a branch at HEAD (Ctrl+B)", "Ctrl+B"),
            ("StashButton", "There are no changes to stash", "Ctrl+Shift+S"),
            ("PopButton", "Pop the newest stash (Ctrl+Shift+O)", "Ctrl+Shift+O"),
            ("UndoButton", "Nothing to undo", "Ctrl+Z"),
            ("RedoButton", "Nothing to redo", "Ctrl+Y"),
            ("SettingsButton", "Open settings (Ctrl+,)", "Ctrl+,"),
            ("SearchButton", "Search commits (Ctrl+F)", "Ctrl+F"),
            ("ThemeSwitch", "Switch to the light theme", null),
            ("AddRepoButton", "Open, clone or init a repository (Ctrl+T)", "Ctrl+T"),
            ("CloseTabButton", "Close this tab (Ctrl+W)", "Ctrl+W"),
            ("ActivityLogToggle", "Show every git command the app has run (Ctrl+J)", "Ctrl+J"));
        Assert.Equal("Open a new tab", AutomationProperties.GetName(app.FindByAutomationId("AddRepoButton")));

        // Shown: the toggle hides the log.
        shell.ActivityLog.ToggleCommand.Execute(null);
        app.Settle();
        AssertButtons(app, ("ActivityLogToggle", "Hide the activity log (Ctrl+J)", "Ctrl+J"));
        shell.ActivityLog.ToggleCommand.Execute(null);

        // "+": a new tab with its welcome page; the toolbar's repository buttons disabled without a
        // tooltip, their keys kept; the toggle disabled with its first text and key (6.8).
        app.Click(app.FindByAutomationId("AddRepoButton"));
        Assert.Equal(2, shell.Tabs.Count);
        AssertButtons(
            app,
            ("OpenRepoButton", "Open a repository on this computer (Ctrl+O)", "Ctrl+O"),
            ("CloneRepoButton", "Clone a repository from a URL (Ctrl+Shift+C)", "Ctrl+Shift+C"),
            ("InitRepoButton", "Create a new repository in a folder (Ctrl+Shift+I)", "Ctrl+Shift+I"),
            ("FetchButton", null, "Ctrl+Shift+F"),
            ("PushButton", null, "Ctrl+Shift+K"),
            ("SearchButton", null, "Ctrl+F"),
            ("UndoButton", null, "Ctrl+Z"),
            ("ActivityLogToggle", "Show every git command the app has run (Ctrl+J)", "Ctrl+J"));
        Assert.False(app.FindByAutomationId("ActivityLogToggle").IsEffectivelyEnabled);

        // The close buttons: the active tab's names Mod+W, another tab's does not; swapped by a
        // click on the other tab and by Ctrl+Tab.
        AssertCloseButtons(app, active: 1);
        app.Click(TabButtons(app)[0]);
        AssertCloseButtons(app, active: 0);
        app.PressKey(Key.Tab, RawInputModifiers.Control);
        AssertCloseButtons(app, active: 1);
    }

    [AvaloniaFact]
    public async Task The_commit_button_names_its_key_while_enabled_and_keeps_its_key_disabled()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nmore\n");
        await repo.GitAsync("add", "src/core.txt");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        app.Click(app.WorkingRowPoint());
        var commit = (Button)app.FindByAutomationId("CommitButton");

        // Nothing typed: disabled, no tooltip and no help text, its key kept (K11 step 5).
        Assert.False(commit.IsEffectivelyEnabled);
        Assert.Null(ToolTip.GetTip(commit));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(commit)));
        Assert.Equal("Ctrl+Enter", AutomationProperties.GetAcceleratorKey(commit));

        repository.Changes.Summary = "Note more core work";
        app.Settle();
        Assert.True(commit.IsEffectivelyEnabled);
        AssertButtons(app, ("CommitButton", "Commit 1 file (Ctrl+Enter)", "Ctrl+Enter"));
        app.MoveMouse(commit);
        await app.WaitForAsync(() => ToolTip.GetIsOpen(commit), "the commit button's tooltip");
        TestPictures.Save(app, "wpk-commit-tooltip");
    }

    [AvaloniaFact]
    public async Task The_resolvers_five_buttons_name_their_keys_also_disabled()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read");
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");

        AssertButtons(
            app,
            ("ResolverPreviousConflict", "Previous conflict (Alt+Up)", "Alt+Up"),
            ("ResolverNextConflict", "Next conflict (Alt+Down)", "Alt+Down"),
            ("ResolverPreviousFile", "Previous file (Alt+Left)", "Alt+Left"),
            ("ResolverNextFile", "Next file (Alt+Right)", "Alt+Right"),
            ("ResolverCloseButton", "Close (Esc)", "Esc"));

        // One conflicted file: the file buttons are disabled, keep their texts and show no tooltip there.
        foreach (var id in new[] { "ResolverPreviousFile", "ResolverNextFile" })
        {
            var button = app.FindByAutomationId(id);
            Assert.False(button.IsEffectivelyEnabled);
            Assert.False(ToolTip.GetShowOnDisabled(button));
        }

        var next = app.FindByAutomationId("ResolverNextConflict");
        app.MoveMouse(next);
        await app.WaitForAsync(() => ToolTip.GetIsOpen(next), "Next conflict's tooltip");
        TestPictures.Save(app, "wpk-resolver-next-conflict-tooltip");

        // The macOS words.
        var mac = new KeyTexts(CommandKey.Cmd);
        Assert.Equal(
            ["Previous conflict (Option+Up)", "Next conflict (Option+Down)", "Previous file (Option+Left)", "Next file (Option+Right)", "Close (Esc)", "Cancel (Esc)"],
            [ShellToolTips.PreviousConflictFor(mac), ShellToolTips.NextConflictFor(mac), ShellToolTips.PreviousFileFor(mac), ShellToolTips.NextFileFor(mac), ShellToolTips.CloseResolverFor(mac), ShellToolTips.CancelListFor(mac)]);
    }

    [AvaloniaFact]
    public async Task The_interactive_lists_Cancel_and_the_search_bars_match_buttons_name_their_keys()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // The search bar's: Enter in the box, and the command's shown key everywhere (6.8).
        app.PressSearch();
        await app.WaitForAsync(() => repository.Search is not null, "the search bar");
        app.Settle();
        AssertButtons(
            app,
            ("SearchPrevious", "Previous match (Shift+Enter, Shift+F3)", "Shift+F3"),
            ("SearchNext", "Next match (Enter, F3)", "F3"));
        var mac = new KeyTexts(CommandKey.Cmd);
        Assert.Equal(("Previous match (Shift+Enter, Cmd+Shift+G)", "Next match (Enter, Cmd+G)"), (SearchViewModel.PreviousToolTipFor(mac), SearchViewModel.NextToolTipFor(mac)));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => repository.Search is null, "the search bar to close");

        // The interactive list's Cancel: Esc, the Esc chain's.
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Interactive rebase of main from here…");
        await app.WaitForAsync(() => repository.IsInteractiveRebaseOpen, "the interactive list");
        app.Settle();
        AssertButtons(app, ("RebaseCancelButton", "Cancel (Esc)", "Esc"));
    }

    private static void AssertButtons(ShellDriver app, params (string Id, string? ToolTip, string? Key)[] buttons)
    {
        app.Settle();
        foreach (var (id, tip, key) in buttons)
        {
            var button = app.FindByAutomationId(id);
            Assert.True(tip == ToolTip.GetTip(button) as string, $"{id}'s tooltip is \"{ToolTip.GetTip(button)}\", not \"{tip}\".");
            Assert.True((tip ?? string.Empty) == (AutomationProperties.GetHelpText(button) ?? string.Empty), $"{id}'s help text is \"{AutomationProperties.GetHelpText(button)}\", not \"{tip}\".");
            Assert.True(key == AutomationProperties.GetAcceleratorKey(button), $"{id}'s AcceleratorKey is \"{AutomationProperties.GetAcceleratorKey(button)}\", not \"{key}\".");
        }
    }

    private static List<Control> TabButtons(ShellDriver app) =>
        [.. app.Window.GetVisualDescendants().OfType<Control>().Where(control => AutomationProperties.GetAutomationId(control) is "RepoTab" or "DashboardTab" && control.IsEffectivelyVisible)];

    private static void AssertCloseButtons(ShellDriver app, int active)
    {
        app.Settle();
        var closes = app.Window.GetVisualDescendants().OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button) == "CloseTabButton" && button.IsEffectivelyVisible).ToList();
        Assert.Equal(app.Session.Shell.Tabs.Count, closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            var (tip, key) = i == active ? ("Close this tab (Ctrl+W)", "Ctrl+W") : ("Close this tab", (string?)null);
            Assert.Equal(tip, ToolTip.GetTip(closes[i]));
            Assert.Equal(tip, AutomationProperties.GetHelpText(closes[i]));
            Assert.Equal(key, AutomationProperties.GetAcceleratorKey(closes[i]));
        }
    }
}
