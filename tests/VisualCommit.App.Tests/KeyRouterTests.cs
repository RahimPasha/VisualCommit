using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The window's key router (docs/phase-6-design.md 6.1 to 6.3, 6.12, D214): every key it takes on
/// "ops", each key of each row, runs its command once and is consumed; a disabled command's key
/// is consumed and runs nothing; every key declines with a menu or a dialog open and during a
/// branch drag; the palette's own key selects its query; a key with the Windows key held is no
/// key of the app. One app start per condition. The toolbar's, the menus' and the window's actions
/// are recorded.
/// </summary>
public class KeyRouterTests
{
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    [AvaloniaFact]
    public async Task Every_key_of_the_set_runs_its_command_once_and_is_consumed()
    {
        using var repo = await ChangedOpsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var (repository, toolbar, menus, window) = await RecordAsync(app);
        var shell = app.Session.Shell;
        var ops = shell.ActiveTab;
        app.Click(app.GraphRowPoint(4));

        // Row 1: the palette, and its own key while it is open selects its query (KD20).
        Assert.True(app.PressShortcutHandled(AppCommand.CommandPalette));
        Assert.True(app.IsPaletteOpen);
        app.TypeInPalette("push");
        Assert.True(app.PressShortcutHandled(AppCommand.CommandPalette));
        Assert.Equal((0, 4), (app.PaletteBox.SelectionStart, app.PaletteBox.SelectionEnd));
        app.PressKey(Key.Escape);

        // Row 2: the settings screen on General.
        Assert.True(app.PressShortcutHandled(AppCommand.Settings));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SettingsDialogViewModel { SelectedSection: SettingsSection.General }, "the settings screen");
        app.Session.Services.DialogHost.CancelCurrent();
        await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the settings screen to close");

        // Row 3: the activity log, shown and hidden.
        Assert.True(app.PressShortcutHandled(AppCommand.ToggleActivityLog));
        Assert.True(shell.ActivityLog.IsShown);
        Assert.True(app.PressShortcutHandled(AppCommand.ToggleActivityLog));
        Assert.False(shell.ActivityLog.IsShown);

        // Row 4: both of Refresh's keys read the refs again.
        foreach (var index in new[] { 0, 1 })
        {
            var calls = app.GitCalls().Count;
            Assert.True(app.PressShortcutHandled(AppCommand.Refresh, index));
            await app.WaitForAsync(() => app.GitCalls().Skip(calls).Any(call => call.CommandText.Contains("for-each-ref", StringComparison.Ordinal)), "the refresh's refs read");
            await app.WaitForIdleAsync();
        }

        // Rows 5 to 10: the tabs.
        Assert.True(app.PressShortcutHandled(AppCommand.NewTab));
        Assert.Equal(2, shell.Tabs.Count);
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        foreach (var (command, index, active) in new[] { (AppCommand.NextTab, 0, 0), (AppCommand.NextTab, 1, 1), (AppCommand.PreviousTab, 0, 0), (AppCommand.PreviousTab, 1, 1) })
        {
            Assert.True(app.PressShortcutHandled(command, index));
            Assert.Same(shell.Tabs[active], shell.ActiveTab);
        }

        Assert.True(app.PressShortcutHandled(AppCommand.GoToTab, 0));
        Assert.Same(ops, shell.ActiveTab);
        Assert.True(app.PressShortcutHandled(AppCommand.GoToTab, 1));
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        for (var place = 2; place < 8; place++)
        {
            // No tab there: consumed, nothing changes.
            Assert.True(app.PressShortcutHandled(AppCommand.GoToTab, place));
            Assert.Same(shell.Tabs[1], shell.ActiveTab);
        }

        Assert.True(app.PressShortcutHandled(AppCommand.GoToTab, 0));
        Assert.True(app.PressShortcutHandled(AppCommand.GoToLastTab));
        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        Assert.True(app.PressShortcutHandled(AppCommand.CloseTab));
        Assert.Same(ops, Assert.Single(shell.Tabs));

        // Row 11 with no dashboard: the palette at "workspace" (6.5, KD16).
        Assert.True(app.PressShortcutHandled(AppCommand.GoToNextDashboard));
        Assert.Equal("workspace", app.Palette.Query);
        Assert.Equal(["New workspace…"], app.Palette.Shown.Select(entry => entry.Label));
        app.PressKey(Key.Escape);

        // Rows 12 to 14: the folder dialogs, cancelled, make no tab; Clone takes a new tab at the end,
        // which Mod+W closes from its URL box.
        app.Folders.Answer(null);
        Assert.True(app.PressShortcutHandled(AppCommand.OpenRepository));
        await app.WaitForAsync(() => app.Folders.Titles.Count == 1, "Open's folder dialog");
        app.Folders.Answer(null);
        Assert.True(app.PressShortcutHandled(AppCommand.InitRepository));
        await app.WaitForAsync(() => app.Folders.Titles.Count == 2, "Init's folder dialog");
        Assert.Equal(["Open a repository", "Choose a folder for the new repository"], app.Folders.Titles);
        Assert.Single(shell.Tabs);
        Assert.True(app.PressShortcutHandled(AppCommand.CloneRepository));
        Assert.Equal(2, shell.Tabs.Count);
        Assert.True(shell.ActiveTab.Welcome.IsCloneFormOpen);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form");
        Assert.True(app.PressShortcutHandled(AppCommand.CloseTab));
        Assert.Same(ops, Assert.Single(shell.Tabs));
        app.Click(app.GraphRowPoint(4));

        // Rows 15 to 20: the toolbar's buttons.
        foreach (var command in new[] { AppCommand.Fetch, AppCommand.Pull, AppCommand.Push, AppCommand.CreateBranch, AppCommand.Stash, AppCommand.Pop })
        {
            Assert.True(app.PressShortcutHandled(command));
        }

        await app.WaitForAsync(() => toolbar.Calls.Count == 6, "the toolbar's six actions");
        Assert.Equal(["fetch", "pull", "push", "branch", "stash", "pop"], toolbar.Calls);

        // Rows 23 to 25: Search, then both keys of each match between the two matches of "alpha".
        Assert.True(app.PressShortcutHandled(AppCommand.Search));
        Assert.NotNull(repository.Search);
        app.Type("alpha");
        await app.WaitForAsync(() => repository.Search!.MatchRows.Count == 2, "the two matches of alpha");
        var selected = new List<int>();
        foreach (var (command, index) in new[] { (AppCommand.NextMatch, 0), (AppCommand.NextMatch, 1), (AppCommand.PreviousMatch, 0), (AppCommand.PreviousMatch, 1) })
        {
            // The move waits for the search to know the next match (phase 5 design 3.1), so under
            // load the selection follows the key a little later: wait for it, then record it.
            var before = repository.SelectedIndex;
            Assert.True(app.PressShortcutHandled(command, index));
            await app.WaitForAsync(
                () => repository.SelectedIndex != before && repository.Search!.MatchRows.Contains(repository.SelectedIndex),
                $"{command}'s key {index} to select the other match (from row {before})");
            selected.Add(repository.SelectedIndex);
        }

        // Between two matches each key moves the selection to the other one.
        Assert.Equal(2, selected.Distinct().Count());
        Assert.All(selected.Zip(selected.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => repository.Search is null, "the search bar to close");

        // Row 26: HEAD's reflog, through the empty graph area's item.
        Assert.True(app.PressShortcutHandled(AppCommand.ShowReflog));

        // Rows 27 and 28: the working changes' items.
        Assert.True(app.PressShortcutHandled(AppCommand.StageAll));
        Assert.True(app.PressShortcutHandled(AppCommand.UnstageAll));
        await app.WaitForAsync(() => menus.Calls.Count == 3, "the menus' three actions");
        Assert.Equal(["ShowReflogAsync", "StageAllAsync", "UnstageAllAsync"], menus.Calls.Select(call => call.Method));

        // Row 29: the working-changes row selected, then the window focuses the summary.
        Assert.True(app.PressShortcutHandled(AppCommand.GoToCommitMessage));
        Assert.True(repository.IsWorkingRowSelected);

        // Row 30 outside the message boxes declines (6.10).
        Assert.False(app.PressShortcutHandled(AppCommand.Commit));

        // Rows 31 and 32: the open diff's file history and blame.
        repository.OpenDiff(new ChangedFile("README.md", FileChangeKind.Modified), staged: true);
        await app.WaitForAsync(() => repository.Diff?.Diff is not null, "the diff");
        Assert.True(app.PressShortcutHandled(AppCommand.FileHistoryOfDiff));
        Assert.True(app.PressShortcutHandled(AppCommand.BlameOfDiff));
        await app.WaitForAsync(() => menus.Calls.Count == 5, "the diff's two actions");
        Assert.Equal(["ShowFileHistoryAsync", "ShowBlameAsync"], menus.Calls.Skip(3).Select(call => call.Method));

        // Rows 33 to 35: the filter and the regions, through the window's actions.
        Assert.True(app.PressShortcutHandled(AppCommand.FilterRefs));
        Assert.True(app.PressShortcutHandled(AppCommand.NextRegion));
        Assert.True(app.PressShortcutHandled(AppCommand.PreviousRegion));
        Assert.Equal(["commit message", "filter", "next region", "previous region"], window.Calls);

        // Every key ran once: the toolbar and the menus called nothing more.
        Assert.Equal(6, toolbar.Calls.Count);
        Assert.Equal(5, menus.Calls.Count);
    }

    [AvaloniaFact]
    public async Task A_disabled_or_inapplicable_commands_key_is_consumed_and_runs_nothing_and_the_Windows_key_is_no_key()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var (_, toolbar, menus, window) = await RecordAsync(app);
        var shell = app.Session.Shell;
        app.Click(app.GraphRowPoint(4));

        // ops is clean: Stash is disabled, Stage all and Unstage all are not listed; no search bar,
        // so no match; no diff; no message box.
        foreach (var command in new[] { AppCommand.Stash, AppCommand.StageAll, AppCommand.UnstageAll, AppCommand.GoToCommitMessage, AppCommand.NextMatch, AppCommand.PreviousMatch, AppCommand.FileHistoryOfDiff, AppCommand.BlameOfDiff, AppCommand.NextTab, AppCommand.PreviousTab })
        {
            Assert.True(app.PressShortcutHandled(command), $"{command}'s key is consumed.");
        }

        Assert.Empty(toolbar.Calls);
        Assert.Empty(menus.Calls);
        Assert.Empty(window.Calls);
        Assert.Single(shell.Tabs);

        // The Windows key held: no key of the app (6.4).
        Assert.False(app.PressKeyHandled(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift | RawInputModifiers.Meta));
        Assert.False(app.IsPaletteOpen);
        Assert.False(app.PressKeyHandled(Key.F6, RawInputModifiers.Meta));
        Assert.Empty(window.Calls);
    }

    [AvaloniaFact]
    public async Task The_settings_screen_opens_on_General_by_its_key_and_from_the_palette()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        var mark = app.LogMark();

        foreach (var how in new[] { SettingsOpening.Key, SettingsOpening.Palette })
        {
            var dialog = await app.OpenSettingsAsync(how);
            Assert.Equal(SettingsSection.General, dialog.SelectedSection);
            app.CancelSettings();
            await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the settings screen to close");
        }

        Assert.Single(app.LogLinesSince(mark), line => line.Contains("Command palette ran: Open settings…", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task With_the_resolver_open_Mod_F_is_consumed_and_runs_nothing()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var searches = 0;
        repository.Toolbar.SearchAction = () => searches++;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read");
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");
        app.ClickResultAt(1, 1);

        // Phase 5 passed a disabled Search's key on; the router consumes it (6.3, 16.1).
        Assert.True(app.PressShortcutHandled(AppCommand.Search));
        Assert.Equal(0, searches);
        Assert.Null(repository.Search);
        Assert.True(repository.IsResolverOpen);

        // Its reason alone, the disabled button's tooltip too (6.8).
        Assert.Equal((false, "Close the conflict resolver first", "Close the conflict resolver first"), (repository.Toolbar.CanSearch, repository.Toolbar.SearchDisabledReason, repository.Toolbar.SearchToolTip));
    }

    [AvaloniaFact]
    public async Task Every_key_declines_while_a_menu_is_open()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var (_, toolbar, menus, window) = await RecordAsync(app);
        var shell = app.Session.Shell;

        // A key the menu takes itself may close it (Ctrl+Tab moves the focus out of it; Enter with
        // any modifier runs its highlighted item, here the empty area's "Fetch all remotes"): the
        // menu opens again before the next key, which the router must leave too.
        PressEveryKey(app, () =>
        {
            if (!shell.Menu.IsOpen)
            {
                app.RightClick(new Point(484, 360));
                Assert.Equal("Fetch all remotes", app.Menu().Highlighted?.Label);
            }
        });
        Assert.Equal(["FetchAllAsync"], menus.Calls.Select(call => call.Method));
        AssertNothingRan(app, toolbar, RecordingActions.Create().Recorder, window);
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task Every_key_declines_while_a_dialog_is_open()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var (_, toolbar, menus, window) = await RecordAsync(app);
        var dialogs = app.Session.Services.DialogHost;

        // A key the dialog takes itself may close it (its focused button's Enter, Ctrl+Enter
        // included): a dialog opens again before the next key, which the router must leave too.
        PressEveryKey(app, () =>
        {
            if (!dialogs.IsOpen)
            {
                _ = dialogs.ConfirmAsync(new ConfirmationRequest("Question", "Go ahead?", string.Empty, "Go ahead", IsDestructive: false));
                app.Settle();
            }
        });
        AssertNothingRan(app, toolbar, menus, window);
        dialogs.CancelCurrent();
    }

    [AvaloniaFact]
    public async Task Every_key_declines_during_a_branch_drag()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var (repository, toolbar, menus, window) = await RecordAsync(app);
        repository.BranchDropMenu = null;
        var alpha = app.GraphLabelBounds(4, "feature/alpha").Center;
        var main = app.GraphLabelBounds(3, "main").Center;
        app.PressMouse(alpha);
        app.MoveMouseHeld(main, steps: 6);
        Assert.True(app.BranchDrag.IsDragging);

        var drags = 1;
        PressEveryKey(app, () =>
        {
            if (!app.BranchDrag.IsDragging)
            {
                app.ReleaseMouse(main);
                app.PressMouse(alpha);
                app.MoveMouseHeld(main, steps: 6);
                drags++;
            }

            Assert.True(app.BranchDrag.IsDragging);
        });
        AssertNothingRan(app, toolbar, menus, window);
        TestContext.Current.SendDiagnosticMessage($"The drag was started {drags} times.");
        app.PressKey(Key.Escape);
        app.ReleaseMouse(main);
    }

    /// <summary>
    /// Presses every key of every command the router takes, after <paramref name="ensure"/> has set
    /// the condition up again, and asserts that each declined (6.3): its key reached the control
    /// with the keyboard focus not handled, on the tunnelling pass the router takes keys on.
    /// </summary>
    private static void PressEveryKey(ShellDriver app, Action ensure)
    {
        foreach (var binding in AppShortcuts.Bindings.Where(binding => binding.Pass == KeyPass.Tunnel))
        {
            for (var index = 0; index < binding.Keys.Count; index++)
            {
                ensure();
                var focused = app.FocusedElement as InputElement
                    ?? throw new Xunit.Sdk.XunitException($"Nothing has the keyboard focus before {binding.Command}'s key {index}.");
                var key = ShortcutKeys.ToGesture(binding.Keys[index], app.CommandModifier).Key;
                bool? reachedUnhandled = null;
                void Record(object? sender, KeyEventArgs e)
                {
                    if (e.Key == key && ReferenceEquals(e.Source, focused))
                    {
                        reachedUnhandled ??= !e.Handled;
                    }
                }

                focused.AddHandler(InputElement.KeyDownEvent, Record, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                try
                {
                    app.PressShortcut(binding.Command, index);
                }
                finally
                {
                    focused.RemoveHandler(InputElement.KeyDownEvent, Record);
                }

                Assert.True(
                    reachedUnhandled == true,
                    $"{binding.Command}'s key {index} ({ShortcutText.Format(binding.Keys[index], CommandKey.Ctrl)}) did not reach {focused.GetType().Name} {AutomationProperties.GetAutomationId((StyledElement)focused)} unhandled: {(reachedUnhandled is null ? "it never arrived" : "something took it on the way")}.");
            }
        }
    }

    private static void AssertNothingRan(ShellDriver app, RecordingToolbar toolbar, RecordingActions menus, RecordingWindowActions window)
    {
        var shell = app.Session.Shell;
        Assert.Empty(toolbar.Calls);
        Assert.Empty(menus.Calls);
        Assert.Empty(window.Calls);
        Assert.Single(shell.Tabs);
        Assert.False(shell.ActivityLog.IsOpen);
        Assert.Empty(app.Folders.Titles);
        Assert.Null(shell.ActiveTab.Repository!.Search);
        Assert.Null(shell.ActiveTab.Repository.Page);
        Assert.Null(shell.OpenPalette);
        Assert.DoesNotContain(app.Session.Services.DialogHost.Open, dialog => dialog is SettingsDialogViewModel or CommandPaletteViewModel);
    }

    /// <summary>"ops" with a staged README.md and an unstaged src/core.txt, so that Stash, Stage all and Unstage all apply.</summary>
    private static async Task<TempRepo> ChangedOpsAsync()
    {
        var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "README.md"), "# Operations\n\nStaged.\n");
        await repo.GitAsync("add", "README.md");
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nkeys\n");
        return repo;
    }

    /// <summary>Waits for the repository and records its toolbar's, its menus' and the window's actions.</summary>
    private static async Task<(RepositoryViewModel Repository, RecordingToolbar Toolbar, RecordingActions Menus, RecordingWindowActions Window)> RecordAsync(ShellDriver app)
    {
        var repository = await app.WaitForRepositoryAsync();
        var toolbar = new RecordingToolbar();
        repository.Toolbar.Actions = toolbar;
        var (actions, menus) = RecordingActions.Create();
        repository.MenuActions = actions;
        var window = new RecordingWindowActions();
        app.Session.Shell.WindowActions = window;
        return (repository, toolbar, menus, window);
    }
}
