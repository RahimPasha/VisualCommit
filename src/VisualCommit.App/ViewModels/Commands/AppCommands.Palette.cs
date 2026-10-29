using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Commands;

// The palette's two lists (docs/phase-6-design.md 5.4, 5.5): its commands for the active tab, in
// 5.4's order (KD1), each only where its row says; and its items, the repository's menus' items on
// one snapshot, then the window's tabs, repositories and workspaces.
public sealed partial class AppCommands
{
    /// <summary>5.4's rows, in order: the commands the palette lists where they apply.</summary>
    private static readonly AppCommand[] RepositoryRows =
    [
        AppCommand.Fetch, AppCommand.Pull, AppCommand.Push, AppCommand.CreateBranch, AppCommand.Stash,
        AppCommand.Pop, AppCommand.Undo, AppCommand.Redo, AppCommand.Search,
        AppCommand.NextMatch, AppCommand.PreviousMatch,
        AppCommand.StageAll, AppCommand.UnstageAll,
        AppCommand.GoToCommitMessage,
        AppCommand.Commit,
        AppCommand.ShowReflog, AppCommand.ShowBackups,
        AppCommand.Refresh,
        AppCommand.CreateTagAtHead, AppCommand.AddRemote, AppCommand.ManageRemotes, AppCommand.ShowAllHiddenBranches, AppCommand.StopSoloing,
        AppCommand.FilterRefs,
    ];

    /// <summary>
    /// The palette's commands for the active tab (5.4), in its rows' order: the repository's
    /// (rows 1 to 10) on a repository tab; Refresh, Fetch all and Pull all and the workspace's
    /// edits on a dashboard (rows 8, 11, 12); then the window's (rows 13 to 21).
    /// </summary>
    public IReadOnlyList<PaletteEntry> PaletteCommands()
    {
        var snapshot = new Snapshot(_shell);
        var entries = new List<PaletteEntry>();
        void Add(AppCommand command, bool listed = true)
        {
            var resolved = Resolve(command, 0, snapshot);
            if (listed && resolved.State.IsListed && resolved.Run is { } run)
            {
                var state = resolved.State;
                entries.Add(new PaletteEntry(state.Label, state.Detail, KeyTexts.Of(command), state.IsEnabled, state.DisabledReason, IsDestructive: false, PaletteEntryKind.Command, entries.Count, run) { Command = command });
            }
        }

        void AddEntry(string label, string? reason, Action run, bool destructive = false) =>
            entries.Add(new PaletteEntry(label, null, null, reason is null, reason, destructive, PaletteEntryKind.Command, entries.Count, run));

        var tab = snapshot.Tab;
        if (snapshot.Repository is not null)
        {
            foreach (var command in RepositoryRows)
            {
                Add(command);
            }
        }
        else if (tab.Dashboard is { } dashboard)
        {
            Add(AppCommand.Refresh);
            Add(AppCommand.Fetch);
            Add(AppCommand.Pull);

            // 5.4's row 12: the workspace's own edits (1.5).
            var commands = _shell.Services.WorkspaceCommands;
            var id = dashboard.WorkspaceId;
            AddEntry(WorkspaceTexts.AddRepository, commands.AddRepositoryReason, () => dashboard.AddRepositoryCommand.Execute(null));
            AddEntry(WorkspaceTexts.AddOpenTabs, commands.AddOpenTabsReason(id), () => dashboard.AddOpenTabsCommand.Execute(null));
            AddEntry(WorkspaceTexts.RenameWorkspace, commands.RenameReason, () => Start(commands.RenameAsync(id), WorkspaceTexts.RenameWorkspace));
            AddEntry(WorkspaceTexts.DeleteWorkspace, commands.DeleteReason, () => Start(commands.DeleteAsync(id), WorkspaceTexts.DeleteWorkspace), destructive: true);
        }

        Add(AppCommand.NewTab);
        Add(AppCommand.CloseTab);
        Add(AppCommand.NextTab);
        Add(AppCommand.PreviousTab);
        Add(AppCommand.GoToNextDashboard, listed: _shell.Tabs.Any(other => other.IsDashboard));
        Add(AppCommand.OpenRepository);
        Add(AppCommand.CloneRepository);
        Add(AppCommand.CloneFromGitHub);
        Add(AppCommand.InitRepository);
        Add(AppCommand.NewWorkspace);
        Add(AppCommand.ToggleActivityLog);
        Add(AppCommand.Settings);
        Add(AppCommand.ShowKeyboardShortcuts);
        if (!KnownEditors.IsChosen(_shell.Services.Settings.Current.Editor))
        {
            // While no editor is chosen (12.1, D226): the settings screen on Tools.
            AddEntry("Choose an editor…", null, () => _shell.OpenSettingsCommand.Execute(SettingsSection.Tools));
        }

        Add(AppCommand.UseDarkTheme);
        Add(AppCommand.UseLightTheme);
        Add(AppCommand.FollowSystemTheme);
        return entries;
    }

    /// <summary>
    /// The palette's items (5.5): on a repository tab its menus' items on one snapshot of
    /// <paramref name="context"/> (the active repository's <see cref="RepositoryViewModel.MenuContext"/>
    /// when null), then the window's: <c>Go to {title}</c> for every tab but the active one,
    /// <c>Open repository {name}</c> for every recent repository and then every workspace's not
    /// listed yet, but the active tab's, and <c>Open workspace {name}</c> for every workspace.
    /// </summary>
    /// <param name="commands">The palette's commands now (<see cref="PaletteCommands"/>, made again when null): an item of a listed command's label is left out.</param>
    /// <param name="context">The repository's menu context to build on, the one the worktrees' read returned; its current one when null.</param>
    public IReadOnlyList<PaletteEntry> PaletteItems(IReadOnlyList<PaletteEntry>? commands = null, RepositoryMenuContext? context = null)
    {
        var tab = _shell.ActiveTab;
        commands ??= PaletteCommands();
        var entries = new List<PaletteEntry>();
        if (tab is { IsDashboard: false, Repository: { IsClosed: false } repository })
        {
            entries.AddRange(repository.PaletteItems(KeyTexts, context ?? repository.MenuContext, commands));
        }

        entries.AddRange(WindowItems(entries.Count));
        return entries;
    }

    /// <summary>
    /// The repository's items (5.5) from the menus <see cref="RepositoryMenus"/> builds on
    /// <paramref name="context"/>, in 5.5's order: every local branch (the checked-out one first,
    /// then the left panel's, RefTreeOrder), every remote branch, tag, stash and remote, the selection's commit or
    /// commits (a stash row's stash), the working changes when there are any, then
    /// <paramref name="diff"/> and <paramref name="page"/>. Each item is its menu item, the menu's
    /// header as its detail; an item of a <paramref name="commands"/> entry's command and label is
    /// left out (that command is listed), and of items of one key the first is kept. Pure.
    /// </summary>
    public static IReadOnlyList<PaletteEntry> RepositoryItems(
        RepositoryMenuContext context,
        IRepositoryActions actions,
        KeyTexts keys,
        bool hasWorkingChanges,
        MenuModel? diff,
        MenuModel? page,
        IReadOnlyList<PaletteEntry> commands)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(commands);
        var listed = new HashSet<(AppCommand, string)>(commands.Where(entry => entry.Command is not null).Select(entry => (entry.Command!.Value, entry.Label)));
        var entries = new List<PaletteEntry>();
        var keysSeen = new HashSet<string>(StringComparer.Ordinal);

        void AddMenu(MenuModel? menu)
        {
            if (menu is null)
            {
                return;
            }

            var detail = menu.HeaderText;
            foreach (var item in menu.Items)
            {
                if (item.Command is { } command && listed.Contains((command, item.Label)))
                {
                    continue;
                }

                var entry = new PaletteEntry(
                    item.Label,
                    detail,
                    item.Command is { } keyed ? keys.Of(keyed) : null,
                    item.IsEnabled,
                    item.IsEnabled ? null : item.DisabledReason,
                    item.IsDestructive,
                    PaletteEntryKind.Item,
                    entries.Count,
                    item.Invoke)
                {
                    Command = item.Command,
                };
                if (keysSeen.Add(entry.Key))
                {
                    entries.Add(entry);
                }
            }
        }

        var current = context.CurrentBranch;
        if (current is not null)
        {
            AddMenu(RepositoryMenus.ForLocalBranch(context, current.FullName, actions));
        }

        // Then the left panel's order (5.5, KD1): folders before branches, names by NameOrder.
        foreach (var branch in context.LocalBranches.Where(branch => !ReferenceEquals(branch, current)).OrderBy(branch => branch.Name, RefTreeOrder.Instance))
        {
            AddMenu(RepositoryMenus.ForLocalBranch(context, branch.FullName, actions));
        }

        foreach (var branch in context.RemoteBranches)
        {
            AddMenu(RepositoryMenus.ForRemoteBranch(context, branch.FullName, actions));
        }

        foreach (var tag in context.Tags)
        {
            AddMenu(RepositoryMenus.ForTag(context, tag.FullName, actions));
        }

        foreach (var stash in context.Refs.Stashes)
        {
            AddMenu(RepositoryMenus.ForStash(context, stash.Sha, actions));
        }

        foreach (var remote in context.Remotes)
        {
            AddMenu(RepositoryMenus.ForRemote(context, remote, actions));
        }

        switch (context.SelectedCommits)
        {
            case [var commit] when commit.IsStash:
                AddMenu(RepositoryMenus.ForStash(context, commit.Sha, actions));
                break;
            case [var commit]:
                AddMenu(RepositoryMenus.ForCommit(context, commit.Sha, actions));
                break;
            case { Count: > 1 }:
                AddMenu(RepositoryMenus.ForCommits(context, actions));
                break;
        }

        if (hasWorkingChanges)
        {
            AddMenu(RepositoryMenus.ForWorkingChanges(context, actions));
        }

        AddMenu(diff);
        AddMenu(page);
        return entries;
    }

    /// <summary>
    /// The text of Go to tab's key for the tab at <paramref name="place"/> (from 1) of
    /// <paramref name="count"/> (5.5): Mod+{n} for a place up to 8, also on the last tab; Mod+9
    /// only for a last tab at place 9 or later; none for a tab at place 9 or later that is not the last.
    /// </summary>
    public static string? GoToTabKeyText(int place, int count, KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (place <= 8)
        {
            var shortcuts = AppShortcuts.All(AppCommand.GoToTab);
            return place >= 1 && place <= shortcuts.Count ? ShortcutText.Format(shortcuts[place - 1], keys.Platform) : null;
        }

        return place == count ? keys.Of(AppCommand.GoToLastTab) : null;
    }

    /// <summary>The window's items (5.5): the tabs but the active one, the repositories, the workspaces.</summary>
    private List<PaletteEntry> WindowItems(int order)
    {
        var entries = new List<PaletteEntry>();
        var tabs = _shell.Tabs;
        var active = _shell.ActiveTab;
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (ReferenceEquals(tab, active))
            {
                continue;
            }

            entries.Add(new PaletteEntry($"Go to {tab.Title}", $"Tab {i + 1}", GoToTabKeyText(i + 1, tabs.Count, KeyTexts), true, null, false, PaletteEntryKind.Item, order + entries.Count, () => _shell.ActivateTabCommand.Execute(tab)));
        }

        // Every recent repository, then every workspace's not listed yet, but the active tab's.
        var services = _shell.Services;
        var activeFolder = active.RepositoryPath ?? active.PendingPath;
        // Each folder as its detail shows it and as it is compared and opened: a workspace's saved
        // folder is the repository's working directory as its open dashboard reads it, when it
        // does (1.2's second rule), since the recent list and the tabs hold git's spelling, which a
        // saved folder through a link (macOS's /var for /private/var) is not; the detail stays the
        // folder as the workspace saved it. A workspace's folder is a repository's top or nothing
        // (D204): one its open dashboard found in a folder state is disabled with the row's State
        // tooltip, and one it has not read is opened only as a top (OpenRepositoryAtAsync).
        var folders = new List<(string Name, string Shown, string Path, bool Saved, string? Reason)>();
        foreach (var recent in services.Recent.Items)
        {
            folders.Add((recent.Name, recent.Path, recent.Path, false, null));
        }

        var workspaces = services.Workspaces.Current.Workspaces;
        foreach (var workspace in workspaces)
        {
            foreach (var repository in workspace.Repositories)
            {
                var path = _shell.OpenedWorkingDirectoryOf(workspace.Id, repository.Path) ?? repository.Path;
                if (!folders.Any(folder => WorkspacePaths.Same(folder.Path, path)))
                {
                    folders.Add((WorkspaceTexts.FolderName(repository.Path), repository.Path, path, true, _shell.FolderReasonOf(workspace.Id, repository.Path)));
                }
            }
        }

        foreach (var (name, shown, path, saved, reason) in folders)
        {
            if (activeFolder is not null && WorkspacePaths.Same(activeFolder, path))
            {
                continue;
            }

            entries.Add(new PaletteEntry($"Open repository {name}", shown, null, reason is null, reason, false, PaletteEntryKind.Item, order + entries.Count, () => Start(_shell.OpenRepositoryAtAsync(path, savedFolder: saved), $"Open repository {name}")));
        }

        foreach (var workspace in workspaces)
        {
            var id = workspace.Id;
            entries.Add(new PaletteEntry(
                $"Open workspace {workspace.Name}",
                WorkspaceTexts.RepositoryCount(workspace.Repositories.Count),
                null,
                true,
                null,
                false,
                PaletteEntryKind.Item,
                order + entries.Count,
                () => _shell.ShowWorkspace(id, _shell.ActiveTab)));
        }

        return entries;
    }
}
