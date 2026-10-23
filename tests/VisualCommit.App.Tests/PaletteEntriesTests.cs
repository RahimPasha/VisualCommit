using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using MenuItem = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the palette lists (docs/phase-6-design.md 5.4, 5.5, 5.12, D212): its items equal the
/// menus' items the same builders give on one snapshot, without the menus 5.5 leaves out, one of
/// each key and none of a listed command's label; its commands in 5.4's order with the menus' and
/// the buttons' states, on a repository tab, an empty tab and a dashboard; the window's items with
/// their keys; and the build at the size of 70,000 items.
/// </summary>
public class PaletteEntriesTests
{
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    [Fact]
    public async Task Every_item_is_its_menu_item_with_the_menus_header_as_its_detail()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Select(MainWork);
        var (actions, _) = RecordingActions.Create();
        var context = app.Repository.MenuContext;
        var keys = new KeyTexts(CommandKey.Ctrl);

        var items = AppCommands.RepositoryItems(context, actions, keys, hasWorkingChanges: false, diff: null, page: null, commands: []);

        // 5.5's order: the checked-out branch first, then the left panel's order (folders first);
        // the remote branches; the tags; the stash; the remote; the selection's commit.
        MenuModel[] menus =
        [
            RepositoryMenus.ForLocalBranch(context, "refs/heads/main", actions),
            RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/alpha", actions),
            RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/conflict", actions),
            RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/ff", actions),
            RepositoryMenus.ForRemoteBranch(context, "refs/remotes/origin/feature/alpha", actions),
            RepositoryMenus.ForRemoteBranch(context, "refs/remotes/origin/main", actions),
            RepositoryMenus.ForTag(context, "refs/tags/v1.0", actions),
            RepositoryMenus.ForTag(context, "refs/tags/v1.1", actions),
            RepositoryMenus.ForStash(context, context.Refs.Stashes[0].Sha, actions),
            RepositoryMenus.ForRemote(context, "origin", actions),
            RepositoryMenus.ForCommit(context, MainWork, actions),
        ];
        var expected = menus
            .SelectMany(menu => menu.Items.Select(item => (item.Label, Detail: menu.HeaderText, item.IsEnabled, Reason: item.IsEnabled ? null : item.DisabledReason, item.IsDestructive, Key: item.Command is { } command ? keys.Of(command) : null)))
            .DistinctBy(entry => (entry.Label, entry.Detail))
            .ToList();
        Assert.Equal(expected, items.Select(entry => (entry.Label, entry.Detail, entry.IsEnabled, Reason: entry.DisabledReason, entry.IsDestructive, Key: entry.KeyText)));
        Assert.All(items, entry => Assert.Equal(PaletteEntryKind.Item, entry.Kind));
        Assert.Equal(Enumerable.Range(0, items.Count), items.Select(entry => entry.Order));

        // Their headers, as K10 reads them.
        Assert.Contains(items, entry => (entry.Label, entry.Detail) == ("Delete main on origin…", "origin/main"));
        Assert.Contains(items, entry => (entry.Label, entry.Detail, entry.DisabledReason) == ("Delete main…", "main", "You cannot delete the branch you are on"));
        Assert.Contains(items, entry => (entry.Label, entry.Detail) == ("Create branch here…", "88d6b91 Main work"));
        Assert.Contains(items, entry => (entry.Label, entry.Detail, entry.KeyText) == ("Pull from origin/main", "main", "Ctrl+Shift+L"));
        Assert.Contains(items, entry => (entry.Label, entry.Detail, entry.KeyText) == ("Pop stash", "stash@{0} On main: Experiment", "Ctrl+Shift+O"));

        // What 5.5 leaves out: the empty graph area's and the sections' menus (5.4's commands), the
        // drop's, the activity log's, the resolver's.
        Assert.DoesNotContain(items, entry => entry.Detail is "ops" or "Local branches" or "Remotes" or "Tags" or "Stashes" || entry.Detail?.Contains('→', StringComparison.Ordinal) == true);

        // An item runs its menu item.
        var copy = items.First(entry => entry.Label == "Copy SHA" && entry.Detail == "88d6b91 Main work");
        copy.Run();

        // The same items with macOS's key texts.
        var mac = AppCommands.RepositoryItems(context, actions, new KeyTexts(CommandKey.Cmd), false, null, null, []);
        Assert.Contains(mac, entry => (entry.Label, entry.KeyText) == ("Push main to origin/main", "Cmd+Shift+K"));
    }

    [Theory]
    [InlineData("alpha", new[] { "alpha", "feature/x", "Main" })]
    [InlineData("Main", new[] { "Main", "feature/x", "alpha" })]
    [InlineData("feature/x", new[] { "feature/x", "alpha", "Main" })]
    public void The_local_branches_come_checked_out_first_then_in_the_left_panels_order(string checkedOut, string[] expected)
    {
        // 5.5, KD1: the checked-out branch, then the left panel's order (RefTreeOrder): in each
        // folder its subfolders before its branches, names compared ignoring case first. The refs'
        // own order (for-each-ref's, by bytes) is Main, alpha, feature/x.
        GitRef Branch(string name) => new($"refs/heads/{name}", name, RefKind.LocalBranch, MainWork, IsHead: name == checkedOut);
        var refs = new RepoRefs(new HeadState(checkedOut, MainWork), [Branch("Main"), Branch("alpha"), Branch("feature/x")], [], []);
        var context = new RepositoryMenuContext("order", "C:/order", refs, WorkingTreeStatus.Clean, [], ToolbarViewModel.StatesFor(refs, WorkingTreeStatus.Clean), IsOperationRunning: false);
        var (actions, _) = RecordingActions.Create();

        var items = AppCommands.RepositoryItems(context, actions, new KeyTexts(CommandKey.Ctrl), false, null, null, []);

        Assert.Equal(expected, items.Select(entry => entry.Detail).Distinct());
    }

    [Fact]
    public async Task One_item_of_each_key_and_none_of_a_listed_commands_label()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nmore\n", TestContext.Current.CancellationToken);
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = app.Repository.MenuContext.Refs.Stashes[0].Sha;
        app.Select(stash);
        var (actions, _) = RecordingActions.Create();
        var context = app.Repository.MenuContext;
        var keys = new KeyTexts(CommandKey.Ctrl);
        var commands = new[]
        {
            new PaletteEntry("Stage all", null, "Ctrl+Shift+A", true, null, false, PaletteEntryKind.Command, 0, () => { }) { Command = AppCommand.StageAll },
            new PaletteEntry("Stash changes…", null, "Ctrl+Shift+S", true, null, false, PaletteEntryKind.Command, 1, () => { }) { Command = AppCommand.Stash },
            new PaletteEntry("Push", null, "Ctrl+Shift+K", true, null, false, PaletteEntryKind.Command, 2, () => { }) { Command = AppCommand.Push },
        };

        var items = AppCommands.RepositoryItems(context, actions, keys, hasWorkingChanges: true, diff: null, page: null, commands);

        // The stash selected in the graph is listed among the stashes too: its items once.
        Assert.Single(items, entry => (entry.Label, entry.Detail) == ("Apply stash", "stash@{0} On main: Experiment"));

        // The working changes' Stage all and Stash changes… are the listed commands; Unstage all and
        // Create branch here… are not.
        Assert.DoesNotContain(items, entry => entry.Detail == "Working changes" && entry.Label is "Stage all" or "Stash changes…");
        Assert.Contains(items, entry => (entry.Label, entry.Detail, entry.KeyText) == ("Unstage all", "Working changes", "Ctrl+Shift+X"));
        Assert.Contains(items, entry => (entry.Label, entry.Detail, entry.KeyText) == ("Create branch here…", "Working changes", "Ctrl+B"));

        // An item of another label stays: the checked-out branch's push, found by "push main".
        Assert.Contains(items, entry => (entry.Label, entry.Detail) == ("Push main to origin/main", "main"));
        Assert.Equal(items.Count, items.Select(entry => entry.Key).Distinct().Count());
    }

    [AvaloniaFact]
    public async Task On_ops_the_commands_are_5_4s_with_the_menus_and_buttons_states()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        var shell = app.Session.Shell;
        var commands = shell.Commands.PaletteCommands();

        // K10 step 2's 33 commands, in 5.4's order (KD1), no detail on one tab without a dashboard.
        Assert.Equal(
            [
                "Fetch all remotes · Ctrl+Shift+F", "Pull · Ctrl+Shift+L", "Push · Ctrl+Shift+K", "Create branch… · Ctrl+B",
                "Stash changes… · Ctrl+Shift+S (disabled: There are no changes to stash)", "Pop the newest stash · Ctrl+Shift+O",
                "Undo · Ctrl+Z (disabled: Nothing to undo)", "Redo · Ctrl+Y (disabled: Nothing to redo)", "Search commits · Ctrl+F",
                "Go to the commit message · Ctrl+Shift+M (disabled: There are no changes to commit)", "Show reflog · Ctrl+Shift+R", "Show backups",
                "Refresh · F5", "Create tag at HEAD…", "Add remote…", "Manage remotes…",
                "Show all hidden branches (disabled: No branch is hidden)", "Stop soloing (disabled: No branch is soloed)", "Filter branches and tags · Ctrl+Shift+E",
                "New tab · Ctrl+T", "Close tab · ops · Ctrl+W", "Next tab · Ctrl+Tab (disabled: There is only one tab)", "Previous tab · Ctrl+Shift+Tab (disabled: There is only one tab)",
                "Open repository… · Ctrl+O", "Clone repository… · Ctrl+Shift+C", "Init repository… · Ctrl+Shift+I", "New workspace…",
                "Show the activity log · Ctrl+J", "Open settings… · Ctrl+,", "Show keyboard shortcuts…", "Choose an editor…",
                "Use the light theme", "Follow the system's theme",
            ],
            commands.Select(Line));
        Assert.All(commands, entry => Assert.Equal(PaletteEntryKind.Command, entry.Kind));

        // The repository's commands have the states of the empty graph area's and the sections' items.
        var (actions, _) = RecordingActions.Create();
        var context = repository.MenuContext;
        var menus = new[]
        {
            RepositoryMenus.ForEmptyGraphArea(context, actions),
            RepositoryMenus.ForSection(context, RefSection.Tags, actions)!,
            RepositoryMenus.ForSection(context, RefSection.Remotes, actions)!,
            RepositoryMenus.ForSection(context, RefSection.LocalBranches, actions)!,
        };
        foreach (var label in new[] { "Fetch all remotes", "Pull", "Push", "Create branch…", "Stash changes…", "Show reflog", "Show backups", "Create tag at HEAD…", "Add remote…", "Manage remotes…", "Show all hidden branches", "Stop soloing" })
        {
            var item = menus.SelectMany(menu => menu.Items).First(item => item.Label == label);
            var entry = commands.Single(entry => entry.Label == label);
            Assert.Equal((item.IsEnabled, item.IsEnabled ? null : item.DisabledReason), (entry.IsEnabled, entry.DisabledReason));
        }

        // The theme chosen now is left out; System chosen leaves out "Follow the system's theme".
        shell.UseTheme(AppTheme.System);
        Assert.Equal(["Use the dark theme", "Use the light theme"], shell.Commands.PaletteCommands().Where(entry => entry.Label.StartsWith("Use the", StringComparison.Ordinal) || entry.Label.StartsWith("Follow", StringComparison.Ordinal)).Select(entry => entry.Label));
        shell.UseTheme(AppTheme.Dark);

        // "Choose an editor…" while no editor is chosen opens the settings screen on Tools (12.1).
        var choose = shell.Commands.PaletteCommands().Single(entry => entry.Label == "Choose an editor…");
        Assert.True(choose.IsEnabled);
        choose.Run();
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is ViewModels.Dialogs.SettingsDialogViewModel, "the settings screen");
        var settings = (ViewModels.Dialogs.SettingsDialogViewModel)app.Session.Services.DialogHost.Current!;
        Assert.Equal(SettingsSection.Tools, settings.SelectedSection);
        app.Session.Services.DialogHost.CancelCurrent();
        await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the settings screen to close");
        app.Session.Settings.Update(current => current with { Editor = new EditorSetting(VisualCommit.App.Services.KnownEditors.CustomId, Path.Combine(data.Path, "editor.exe")) });
        Assert.DoesNotContain(shell.Commands.PaletteCommands(), entry => entry.Label == "Choose an editor…");

        // The search bar open adds Next match and Previous match after Search commits (row 3).
        repository.OpenSearch();
        app.Settle();
        Assert.Equal(
            ["Search commits", "Next match", "Previous match", "Go to the commit message"],
            shell.Commands.PaletteCommands().Select(entry => entry.Label).SkipWhile(label => label != "Search commits").Take(4));
        Assert.Equal(("F3", false, "No matches"), shell.Commands.PaletteCommands().Where(entry => entry.Label == "Next match").Select(entry => (entry.KeyText, entry.IsEnabled, entry.DisabledReason)).Single());
    }

    [AvaloniaFact]
    public async Task The_commit_entry_is_the_commit_buttons_text_state_and_reason()
    {
        // 5.4's row 6 on ops (5.12): not listed without working changes (the 33 commands above);
        // with a file staged and a summary typed, "Commit 1 file" enabled with its key.
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nmore\n", TestContext.Current.CancellationToken);
        await repo.GitAsync("add", "--", "src/core.txt");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.Status.Staged.Count == 1, "the staged file's read");
        var shell = app.Session.Shell;

        // The summary empty: disabled as the button is, without a reason (6.8).
        var empty = Command(shell, "Commit 1 file");
        Assert.Equal((false, null, false, null), (empty.IsEnabled, empty.DisabledReason, repository.Changes.CanCommit, repository.Changes.CommitDisabledReason));

        repository.Changes.Summary = "Note more core work";
        Assert.Equal("Commit 1 file · Ctrl+Enter", Line(Command(shell, "Commit 1 file")));
        Assert.Equal("Go to the commit message · Ctrl+Shift+M", Line(Command(shell, "Go to the commit message")));
    }

    [AvaloniaTheory]
    [InlineData(Scenarios.ConflictsStop.Rebase)]
    [InlineData(Scenarios.ConflictsStop.Merge)]
    public async Task On_a_stop_with_a_conflict_the_commit_entry_and_the_commit_message_say_what_the_button_and_the_resolver_say(Scenarios.ConflictsStop stop)
    {
        // 5.4's rows 5 and 6, 5.12: a repository without a remote, stopped with notes.txt conflicted.
        using var repo = await Scenarios.ConflictsStoppedAsync(stop);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read");
        var shell = app.Session.Shell;
        var changes = repository.Changes;

        // Without a remote, Fetch all remotes is disabled with the empty area's reason.
        Assert.Equal("Fetch all remotes · Ctrl+Shift+F (disabled: This repository has no remotes)", Line(Command(shell, "Fetch all remotes")));

        // Row 6 is the commit button: its text, state and reason; a rebase's "Continue rebase".
        var commit = Command(shell, changes.CommitButtonText);
        Assert.Equal((changes.CanCommit, changes.CommitDisabledReason), (commit.IsEnabled, commit.DisabledReason));
        Assert.False(commit.IsEnabled);
        if (stop == Scenarios.ConflictsStop.Rebase)
        {
            Assert.Equal("Continue rebase · Ctrl+Enter (disabled: Resolve the conflicts first (1 file))", Line(commit));
        }

        Assert.Equal("Go to the commit message · Ctrl+Shift+M", Line(Command(shell, "Go to the commit message")));

        // The resolver open: both say to close it first (ToolbarViewModel.CloseConflictResolverText).
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");
        Assert.True(repository.IsResolverOpen);
        Assert.Equal($"{changes.CommitButtonText} · Ctrl+Enter (disabled: Close the conflict resolver first)", Line(Command(shell, changes.CommitButtonText)));
        Assert.Equal("Go to the commit message · Ctrl+Shift+M (disabled: Close the conflict resolver first)", Line(Command(shell, "Go to the commit message")));
    }

    [AvaloniaFact]
    public async Task The_window_items_go_to_the_tabs_with_their_keys_and_open_each_folder_once()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var shell = app.Session.Shell;

        // 5.10's second case: the active tab is empty, so it opens the repository (and remembers it).
        await shell.OpenRepositoryAtAsync(repo.Path);
        await app.WaitForRepositoryAsync();
        Assert.Single(shell.Tabs);
        Assert.Equal("ops", app.Session.Services.Recent.Items[0].Name);
        for (var i = 0; i < 4; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        app.Settle();

        // Five tabs, the first active: tabs 2 to 5, the fifth with Ctrl+5, not Ctrl+9 (5.5).
        Assert.Equal(
            ["Go to New tab · Tab 2 · Ctrl+2", "Go to New tab · Tab 3 · Ctrl+3", "Go to New tab · Tab 4 · Ctrl+4", "Go to New tab · Tab 5 · Ctrl+5"],
            GoTo(shell.Commands.PaletteItems()));
        Assert.Equal(["Cmd+2", "Cmd+3", "Cmd+4", "Cmd+5"], new AppCommands(shell, new KeyTexts(CommandKey.Cmd)).PaletteItems().Where(entry => entry.Label.StartsWith("Go to ", StringComparison.Ordinal)).Select(entry => entry.KeyText));

        // Ten tabs: tab 8 Ctrl+8, tab 9 none, tab 10 (the last) Ctrl+9.
        for (var i = 0; i < 5; i++)
        {
            shell.NewTabCommand.Execute(null);
        }

        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        app.Settle();
        var keyed = shell.Commands.PaletteItems().Where(entry => entry.Label.StartsWith("Go to ", StringComparison.Ordinal)).ToDictionary(entry => entry.Detail!, entry => entry.KeyText);
        Assert.Equal(("Ctrl+8", null, "Ctrl+9"), (keyed["Tab 8"], keyed["Tab 9"], keyed["Tab 10"]));
        var mac = new AppCommands(shell, new KeyTexts(CommandKey.Cmd)).PaletteItems().Where(entry => entry.Label.StartsWith("Go to ", StringComparison.Ordinal)).ToDictionary(entry => entry.Detail!, entry => entry.KeyText);
        Assert.Equal(("Cmd+5", "Cmd+8", null, "Cmd+9"), (mac["Tab 5"], mac["Tab 8"], mac["Tab 9"], mac["Tab 10"]));

        // "Open repository ops": the active tab's folder is left out; from an empty tab it is listed once.
        Assert.DoesNotContain(shell.Commands.PaletteItems(), entry => entry.Label == "Open repository ops");
        shell.ActivateTabCommand.Execute(shell.Tabs[3]);
        app.Settle();
        // The recent list holds the folder as git spells it (macOS's /private/var for /var).
        Assert.Equal([shell.Tabs[0].RepositoryPath], shell.Commands.PaletteItems().Where(entry => entry.Label == "Open repository ops").Select(entry => entry.Detail));

        // Its run activates the tab that shows the worktree (5.10's first case): nothing opens again.
        shell.Commands.PaletteItems().Single(entry => entry.Label == "Open repository ops").Run();
        await app.WaitForAsync(() => ReferenceEquals(shell.ActiveTab, shell.Tabs[0]), "the ops tab to be active");
        Assert.Equal(10, shell.Tabs.Count);
    }

    [AvaloniaFact]
    public async Task An_empty_tab_lists_the_windows_commands_and_a_dashboard_its_own()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        var workspace = new Workspace("solo", "Solo", [new WorkspaceRepository(repo.Path)]);
        using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [workspace] }, openDashboardOf: "solo", extraTabs: [new TabState(null)]);
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync();
        var shell = app.Session.Shell;

        // The dashboard (rows 8, 11, 12, then the window's; KD2: no detail but Close tab's).
        Assert.Equal(
            [
                "Refresh · F5", "Fetch every repository of Solo · Ctrl+Shift+F", "Pull every repository of Solo · Ctrl+Shift+L",
                "Add repository…", "Add open tabs (disabled: No tab has a repository open)", "Rename workspace…", "Delete workspace…",
                "New tab · Ctrl+T", "Close tab · Solo · Ctrl+W", "Next tab · Ctrl+Tab", "Previous tab · Ctrl+Shift+Tab", "Go to the next dashboard · Solo · Ctrl+Shift+D",
                "Open repository… · Ctrl+O", "Clone repository… · Ctrl+Shift+C", "Init repository… · Ctrl+Shift+I", "New workspace…",
                "Show the activity log · Ctrl+J (disabled: Select a repository to see its activity)", "Open settings… · Ctrl+,", "Show keyboard shortcuts…", "Choose an editor…",
                "Use the light theme", "Follow the system's theme",
            ],
            shell.Commands.PaletteCommands().Select(Line));
        Assert.True(shell.Commands.PaletteCommands().Single(entry => entry.Label == "Delete workspace…").IsDestructive);
        Assert.Equal(["Open repository ops · " + repo.Path, "Open workspace Solo · 1 repository"], shell.Commands.PaletteItems().Where(entry => entry.Label.StartsWith("Open ", StringComparison.Ordinal)).Select(entry => $"{entry.Label} · {entry.Detail}"));

        // The empty tab: no repository's commands, no workspace's.
        shell.ActivateTabCommand.Execute(shell.Tabs[1]);
        app.Settle();
        var empty = shell.Commands.PaletteCommands();
        Assert.Equal("New tab", empty[0].Label);
        Assert.DoesNotContain(empty, entry => entry.Label is "Refresh" or "Fetch all remotes" or "Show the activity log" or "Add open tabs");
        Assert.Equal("Go to the next dashboard · Solo · Ctrl+Shift+D", Line(empty.Single(entry => entry.Label == "Go to the next dashboard")));
    }

    [AvaloniaFact]
    public async Task A_workspace_folder_inside_another_repository_is_disabled_and_never_opens_the_outer_one()
    {
        // D204: a workspace's folder is its repository's top or nothing. "inner" lies inside ops'
        // working tree (its .git deleted, say): the dashboard reads it as Not a repository, the
        // palette's item says why, and an open of the saved folder is refused, not ops opened.
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        var inner = Path.Combine(repo.Path, "inner");
        Directory.CreateDirectory(inner);
        var workspace = new Workspace("solo", "Solo", [new WorkspaceRepository(repo.Path), new WorkspaceRepository(inner)]);
        using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [workspace] }, openDashboardOf: "solo");
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync();
        var shell = app.Session.Shell;
        var row = shell.ActiveTab.Dashboard!.Rows[1];
        Assert.Equal("Not a repository", row.State.Text);
        var reason = row.State.ToolTip!;
        Assert.StartsWith($"{inner} is not a git repository of its own: it lies inside ", reason, StringComparison.Ordinal);

        Assert.Equal(
            ["Open repository ops · " + repo.Path, $"Open repository inner · {inner} (disabled: {reason})"],
            shell.Commands.PaletteItems().Where(entry => entry.Label.StartsWith("Open repository ", StringComparison.Ordinal)).Select(Line));

        await shell.OpenRepositoryAtAsync(inner, savedFolder: true);
        app.Settle();

        Assert.Equal(2, shell.Tabs.Count);
        Assert.All(shell.Tabs, tab => Assert.Null(tab.Repository));
        Assert.Equal(reason + ".", shell.ActiveTab.Welcome.ErrorText);
    }

    /// <summary>An entry as the report writes a row: label · detail · key, then a disabled entry's reason.</summary>
    private static string Line(PaletteEntry entry)
    {
        var parts = new[] { entry.Label, entry.Detail, entry.KeyText }.Where(part => part is not null);
        var line = string.Join(" · ", parts);
        return entry.IsEnabled ? line : $"{line} (disabled: {entry.DisabledReason})";
    }

    /// <summary>The window's command of <paramref name="label"/> as the palette lists it now.</summary>
    private static PaletteEntry Command(MainWindowViewModel shell, string label) =>
        Assert.Single(shell.Commands.PaletteCommands(), entry => entry.Label == label);

    private static IEnumerable<string> GoTo(IEnumerable<PaletteEntry> items) =>
        items.Where(entry => entry.Label.StartsWith("Go to ", StringComparison.Ordinal)).Select(Line);

    /// <summary>A repository with 100 local branches, 200 remote branches and <paramref name="tags"/> tags.</summary>
    internal static RepositoryMenuContext SizeContext(int tags)
    {
        var refs = new List<GitRef>
        {
            new("refs/heads/main", "main", RefKind.LocalBranch, MainWork, Upstream: "origin/main", IsHead: true, UpstreamRef: "refs/remotes/origin/main", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/main"),
        };
        for (var i = 1; i < 100; i++)
        {
            refs.Add(new GitRef($"refs/heads/topic/b{i:D3}", $"topic/b{i:D3}", RefKind.LocalBranch, MainWork, Upstream: $"origin/topic/b{i:D3}", UpstreamRef: $"refs/remotes/origin/topic/b{i:D3}", UpstreamRemote: "origin", UpstreamRemoteRef: $"refs/heads/topic/b{i:D3}"));
        }

        for (var i = 0; i < 200; i++)
        {
            refs.Add(new GitRef($"refs/remotes/origin/topic/b{i:D3}", $"origin/topic/b{i:D3}", RefKind.RemoteBranch, MainWork, RemoteName: "origin"));
        }

        for (var i = 0; i < tags; i++)
        {
            refs.Add(new GitRef($"refs/tags/v{i}", $"v{i}", RefKind.Tag, MainWork));
        }

        var repoRefs = new RepoRefs(new HeadState("main", MainWork), refs, [], ["origin"]);
        return new RepositoryMenuContext("big", "C:/big", repoRefs, WorkingTreeStatus.Clean, [], ToolbarViewModel.StatesFor(repoRefs, WorkingTreeStatus.Clean), IsOperationRunning: false);
    }
}
