using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// What a command is for the active tab now (docs/phase-6-design.md 6.4): whether it applies here
/// (its key does something, and, for the palette's commands, whether it is listed), whether it is
/// enabled, and how the palette says it.
/// </summary>
/// <param name="IsListed">It applies to the active tab (5.4's "Listed" column; for a key, 6.1's "Applies"); a key that does not apply is consumed and does nothing.</param>
/// <param name="IsEnabled">It can run now.</param>
/// <param name="Label">The palette's label (5.4).</param>
/// <param name="Detail">The palette's detail: the repository's name while dialogs name it (3.8, KD2), the tab's title, the workspace it goes to; or null.</param>
/// <param name="DisabledReason">Why it cannot run, the menus' and buttons' reason; null when enabled or without a reason.</param>
public sealed record CommandState(bool IsListed, bool IsEnabled, string Label, string? Detail, string? DisabledReason)
{
    /// <summary>A command that does not apply to the active tab.</summary>
    public static CommandState NotListed(string label) => new(false, false, label, null, null);
}

/// <summary>
/// The window's commands for the active tab (docs/phase-6-design.md 5.4, 6.1, 6.4): the router's
/// keys, the palette's commands and the tests share these states, so a key and its entry are
/// enabled together. A state taken from a menu builder comes from that builder run on one
/// <see cref="RepositoryViewModel.MenuContext"/> snapshot, so a menu and the palette never disagree.
/// <c>MainWindowViewModel.Commands</c> holds one made with <see cref="KeyTexts.Current"/>; a test
/// of the Cmd texts makes its own with <c>new KeyTexts(CommandKey.Cmd)</c>.
/// </summary>
public sealed partial class AppCommands
{
    /// <summary>Close tab on the lone empty tab (5.4's row 14).</summary>
    public const string LoneEmptyTabText = "This is the only tab, and it is empty";

    /// <summary>Next tab and Previous tab with one tab (5.4's row 15).</summary>
    public const string OnlyOneTabText = "There is only one tab";

    /// <summary>Go to the commit message without working changes (5.4's row 5).</summary>
    public const string NoChangesToCommitText = "There are no changes to commit";

    /// <summary>The palette's query when Mod+Shift+D finds no dashboard open (6.5, KD16).</summary>
    public const string WorkspaceQuery = "workspace";

    private readonly MainWindowViewModel _shell;

    public AppCommands(MainWindowViewModel shell, KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(keys);
        _shell = shell;
        KeyTexts = keys;
    }

    /// <summary>Whose words the palette's keys use.</summary>
    public KeyTexts KeyTexts { get; }

    /// <summary>What <paramref name="command"/> is for the active tab now; <paramref name="argument"/> is Go to tab's place, from 1.</summary>
    public CommandState Describe(AppCommand command, int argument = 0) => Resolve(command, argument, new Snapshot(_shell)).State;

    /// <summary>
    /// Runs <paramref name="command"/> when it applies to the active tab and is enabled (6.3);
    /// returns whether it ran. The router consumes the key either way.
    /// </summary>
    public bool Run(AppCommand command, int argument = 0)
    {
        var resolved = Resolve(command, argument, new Snapshot(_shell));
        if (!resolved.State.IsListed || !resolved.State.IsEnabled || resolved.Run is null)
        {
            return false;
        }

        resolved.Run();
        return true;
    }

    /// <summary>The command's state and what runs it, from one snapshot of the active tab.</summary>
    private Resolved Resolve(AppCommand command, int argument, Snapshot snapshot)
    {
        var shell = _shell;
        var tab = snapshot.Tab;
        var repository = snapshot.Repository;
        var dashboard = tab.Dashboard;
        var toolbar = tab.Toolbar;
        switch (command)
        {
            case AppCommand.CommandPalette:
                return Always(CommandPaletteViewModel.PaletteTitle, () => Start(shell.OpenPaletteAsync(), CommandPaletteViewModel.PaletteTitle));

            case AppCommand.Settings:
                return Always(OperationTexts.OpenSettingsButton, () => shell.OpenSettingsCommand.Execute(SettingsSection.General));

            case AppCommand.ShowKeyboardShortcuts:
                return Always("Show keyboard shortcuts…", () => shell.OpenSettingsCommand.Execute(SettingsSection.KeyboardShortcuts));

            case AppCommand.ToggleActivityLog:
            {
                var log = shell.ActivityLog;
                var label = log.IsShown ? "Hide the activity log" : "Show the activity log";
                var enabled = log.ToggleCommand.CanExecute(null);
                return new(
                    new(repository is not null || dashboard is not null, enabled, label, null, enabled ? null : dashboard?.ActivityLogReason),
                    () => log.ToggleCommand.Execute(null));
            }

            case AppCommand.Refresh:
                if (repository is not null)
                {
                    return new(new(true, true, "Refresh", snapshot.RepositoryDetail, null), () => Start(repository.RefreshAsync(), "Refresh"));
                }

                return dashboard is not null
                    ? new(new(true, true, "Refresh", null, null), () => dashboard.RefreshCommand.Execute(null))
                    : NotListed("Refresh");

            case AppCommand.NewTab:
                return Always("New tab", () => shell.NewTabCommand.Execute(null));

            case AppCommand.CloseTab:
                return new(
                    new(true, tab.CanClose, "Close tab", tab.Title, tab.CanClose ? null : LoneEmptyTabText),
                    () => shell.CloseTabCommand.Execute(tab));

            case AppCommand.NextTab:
            case AppCommand.PreviousTab:
            {
                var tabs = shell.Tabs;
                var several = tabs.Count > 1;
                var step = command == AppCommand.NextTab ? 1 : -1;
                return new(
                    new(true, several, command == AppCommand.NextTab ? "Next tab" : "Previous tab", null, several ? null : OnlyOneTabText),
                    () => shell.ActivateTabCommand.Execute(tabs[(((tabs.IndexOf(tab) + step) % tabs.Count) + tabs.Count) % tabs.Count]));
            }

            case AppCommand.GoToTab:
            {
                var tabs = shell.Tabs;
                var exists = argument >= 1 && argument <= tabs.Count;
                return new(
                    new(exists, exists, $"Go to tab {argument}", null, null),
                    exists ? () => shell.ActivateTabCommand.Execute(tabs[argument - 1]) : null);
            }

            case AppCommand.GoToLastTab:
                return Always("Go to the last tab", () => shell.ActivateTabCommand.Execute(shell.Tabs[^1]));

            case AppCommand.GoToNextDashboard:
            {
                // With no dashboard open the key opens the palette at "workspace" (6.5); the palette
                // lists the command only while a dashboard is open (5.4's row 16).
                var next = NextDashboard(shell, tab);
                return new(
                    new(true, true, "Go to the next dashboard", next?.Title, null),
                    next is not null ? () => shell.ActivateTabCommand.Execute(next) : () => Start(shell.OpenPaletteAsync(WorkspaceQuery), CommandPaletteViewModel.PaletteTitle));
            }

            case AppCommand.OpenRepository:
                return Always("Open repository…", () => Start(shell.OpenRepositoryAsync(), "Open repository…"));

            case AppCommand.CloneRepository:
                return Always("Clone repository…", shell.CloneRepository);

            case AppCommand.CloneFromGitHub:
                // Listed while GitHub is not off (D299); signed out it signs in first (9.5, D300).
                return new(
                    new(!shell.Services.Hosting.IsOff, true, OperationTexts.CloneTexts.PaletteCommand, null, null),
                    () => Start(shell.CloneFromGitHubAsync(), OperationTexts.CloneTexts.PaletteCommand));

            case AppCommand.InitRepository:
                return Always("Init repository…", () => Start(shell.InitRepositoryAsync(), "Init repository…"));

            case AppCommand.NewWorkspace:
            {
                var commands = shell.Services.WorkspaceCommands;
                var reason = commands.CreateReason;
                return new(new(true, reason is null, WorkspaceTexts.NewWorkspace, null, reason), () => Start(commands.CreateAsync(tab), WorkspaceTexts.NewWorkspace));
            }

            case AppCommand.Fetch:
            case AppCommand.Pull:
            {
                var fetch = command == AppCommand.Fetch;
                var run = fetch ? () => toolbar.FetchCommand.Execute(null) : (Action)(() => toolbar.PullCommand.Execute(null));
                if (dashboard is not null)
                {
                    // On a dashboard the toolbar's Fetch and Pull are Fetch all and Pull all (4.10, 6.9).
                    var label = fetch ? OperationTexts.FetchEveryRepository(dashboard.Name) : OperationTexts.PullEveryRepository(dashboard.Name);
                    var enabled = fetch ? toolbar.CanFetch : toolbar.CanPull;
                    return new(new(true, enabled, label, null, fetch ? toolbar.FetchDisabledReason : toolbar.PullDisabledReason), run);
                }

                return FromItem(snapshot, snapshot.EmptyArea, fetch ? FetchAllRemotes : ToolbarPull, run);
            }

            case AppCommand.Push:
                return FromItem(snapshot, snapshot.EmptyArea, ToolbarPush, () => toolbar.PushCommand.Execute(null));

            case AppCommand.CreateBranch:
                return FromItem(snapshot, snapshot.EmptyArea, CreateBranch, () => toolbar.BranchCommand.Execute(null));

            case AppCommand.Stash:
                return FromItem(snapshot, snapshot.EmptyArea, StashChanges, () => toolbar.StashCommand.Execute(null));

            case AppCommand.Pop:
                return Toolbar(snapshot, "Pop the newest stash", toolbar.CanPop, toolbar.PopDisabledReason, () => toolbar.PopCommand.Execute(null));

            case AppCommand.Undo:
                return Toolbar(snapshot, toolbar.UndoText ?? "Undo", toolbar.CanUndo, toolbar.UndoDisabledReason, () => toolbar.UndoCommand.Execute(null));

            case AppCommand.Redo:
                return Toolbar(snapshot, toolbar.RedoText ?? "Redo", toolbar.CanRedo, toolbar.RedoDisabledReason, () => toolbar.RedoCommand.Execute(null));

            case AppCommand.Search:
                return Toolbar(snapshot, "Search commits", toolbar.CanSearch, toolbar.SearchDisabledReason, () => toolbar.SearchCommand.Execute(null));

            case AppCommand.NextMatch:
            case AppCommand.PreviousMatch:
            {
                var label = command == AppCommand.NextMatch ? "Next match" : "Previous match";
                if (repository?.Search is not { } search)
                {
                    return NotListed(label);
                }

                var go = command == AppCommand.NextMatch ? search.NextCommand : search.PreviousCommand;
                return new(new(true, search.HasMatches, label, snapshot.RepositoryDetail, search.HasMatches ? null : Graph.SearchViewModel.NoMatchesText), () => go.Execute(null));
            }

            case AppCommand.ShowReflog:
                return FromItem(snapshot, snapshot.EmptyArea, ShowReflog, null);

            case AppCommand.ShowBackups:
                return FromItem(snapshot, snapshot.EmptyArea, ShowBackups, null);

            case AppCommand.CreateTagAtHead:
                return FromItem(snapshot, snapshot.Section(RefSection.Tags), CreateTagAtHead, null);

            case AppCommand.AddRemote:
                return FromItem(snapshot, snapshot.Section(RefSection.Remotes), AddRemote, null);

            case AppCommand.ManageRemotes:
                return FromItem(snapshot, snapshot.Section(RefSection.Remotes), ManageRemotes, null);

            case AppCommand.ShowAllHiddenBranches:
                return FromItem(snapshot, snapshot.Section(RefSection.LocalBranches), ShowAllHiddenBranches, null);

            case AppCommand.StopSoloing:
                return FromItem(snapshot, snapshot.Section(RefSection.LocalBranches), StopSoloing, null);

            case AppCommand.StageAll:
            case AppCommand.UnstageAll:
            {
                var label = command == AppCommand.StageAll ? StageAll : UnstageAll;
                return repository is { HasWorkingChanges: true } ? FromItem(snapshot, snapshot.WorkingChanges, label, null) : NotListed(label);
            }

            case AppCommand.GoToCommitMessage:
            {
                const string label = "Go to the commit message";
                if (repository is null)
                {
                    return NotListed(label);
                }

                var reason = repository.IsResolverOpen ? ToolbarViewModel.CloseConflictResolverText
                    : !repository.HasWorkingChanges ? NoChangesToCommitText
                    : null;
                return new(
                    new(true, reason is null, label, snapshot.RepositoryDetail, reason),
                    () =>
                    {
                        repository.IsWorkingRowSelected = true;
                        shell.WindowActions?.FocusCommitMessage();
                    });
            }

            case AppCommand.Commit:
            {
                if (repository is null || !(repository.HasWorkingChanges || repository.Changes.ContinuesOperation))
                {
                    return NotListed("Commit");
                }

                // The commit button's text, state and reason (D199, 5.4's row 6).
                var changes = repository.Changes;
                var resolver = repository.IsResolverOpen;
                var enabled = !resolver && changes.CanCommit;
                var reason = resolver ? ToolbarViewModel.CloseConflictResolverText : enabled ? null : changes.CommitDisabledReason;
                return new(
                    new(true, enabled, changes.CommitButtonText, snapshot.RepositoryDetail, reason),
                    () =>
                    {
                        if (changes.CommitCommand.CanExecute(null))
                        {
                            changes.CommitCommand.Execute(null);
                        }
                    });
            }

            case AppCommand.FileHistoryOfDiff:
            case AppCommand.BlameOfDiff:
            {
                var label = command == AppCommand.FileHistoryOfDiff ? FileHistory : Blame;
                if (snapshot.Diff?.Items.FirstOrDefault(item => item.Command == command) is not { } item)
                {
                    return NotListed(label);
                }

                return new(new(true, item.IsEnabled, item.Label, snapshot.RepositoryDetail, item.DisabledReason), item.Invoke);
            }

            case AppCommand.FilterRefs:
                return repository is null
                    ? NotListed("Filter branches and tags")
                    : new(new(true, true, "Filter branches and tags", snapshot.RepositoryDetail, null), () => shell.WindowActions?.FocusFilter());

            case AppCommand.NextRegion:
                return Always("Next region", () => shell.WindowActions?.MoveFocusToRegion(backwards: false));

            case AppCommand.PreviousRegion:
                return Always("Previous region", () => shell.WindowActions?.MoveFocusToRegion(backwards: true));

            case AppCommand.UseDarkTheme:
                return Theme(AppTheme.Dark, "Use the dark theme");

            case AppCommand.UseLightTheme:
                return Theme(AppTheme.Light, "Use the light theme");

            case AppCommand.FollowSystemTheme:
                return Theme(AppTheme.System, "Follow the system's theme");

            default:
                // The keys only drawn and listed: their views take them (6.4).
                return NotListed(command.ToString());
        }

        Resolved Theme(AppTheme theme, string label) =>
            shell.Theme == theme ? NotListed(label) : Always(label, () => shell.UseTheme(theme));
    }

    /// <summary>The dashboard Mod+Shift+D goes to from <paramref name="from"/> (6.5): the next one after it among the dashboards, the first from a tab that is not one, wrapping; null with none open.</summary>
    private static RepoTabViewModel? NextDashboard(MainWindowViewModel shell, RepoTabViewModel from)
    {
        var tabs = shell.Tabs;
        var dashboards = tabs.Where(tab => tab.IsDashboard).ToList();
        if (dashboards.Count == 0)
        {
            return null;
        }

        var index = dashboards.IndexOf(from);
        return index < 0 ? dashboards[0] : dashboards[(index + 1) % dashboards.Count];
    }

    private static Resolved Always(string label, Action run) => new(new(true, true, label, null, null), run);

    /// <summary>
    /// Lets a command's task go on without waiting for it (the key or the palette has done its part:
    /// a folder dialog, the palette, a read), logging in the app's log a failure that got past the
    /// task's own handling (a folder dialog that throws), which would otherwise go unobserved.
    /// </summary>
    private void Start(Task task, string what) =>
        _ = task.ContinueWith(
            failed => _shell.Services.Log.Error($"{what} failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static Resolved NotListed(string label) => new(CommandState.NotListed(label), null);

    /// <summary>A repository's command enabled as the toolbar's button (5.4's row 2).</summary>
    private static Resolved Toolbar(Snapshot snapshot, string label, bool enabled, string? reason, Action run) =>
        snapshot.Repository is null ? NotListed(label) : new(new(true, enabled, label, snapshot.RepositoryDetail, enabled ? null : reason), run);

    /// <summary>
    /// A repository's command whose state is the menu item of <paramref name="label"/> in
    /// <paramref name="menu"/>, built on the snapshot's context (5.4): run by <paramref name="run"/>,
    /// or the item's own <c>Invoke</c> when null.
    /// </summary>
    private static Resolved FromItem(Snapshot snapshot, MenuModel? menu, string label, Action? run)
    {
        if (snapshot.Repository is null || menu?.Items.FirstOrDefault(item => item.Label == label) is not { } item)
        {
            return NotListed(label);
        }

        return new(new(true, item.IsEnabled, label, snapshot.RepositoryDetail, item.DisabledReason), run ?? item.Invoke);
    }

    /// <summary>A command's state and what runs it.</summary>
    private sealed record Resolved(CommandState State, Action? Run);

    /// <summary>
    /// The active tab as the commands read it, once per call: its repository (a repository tab's,
    /// not an opening, empty or dashboard tab's), one menu context and the menus built on it,
    /// each built when first asked for.
    /// </summary>
    private sealed class Snapshot
    {
        private readonly RepositoryMenuContext? _context;
        private readonly Dictionary<RefSection, MenuModel?> _sections = [];
        private MenuModel? _emptyArea;
        private MenuModel? _workingChanges;
        private MenuModel? _diff;
        private bool _diffBuilt;

        public Snapshot(MainWindowViewModel shell)
        {
            Tab = shell.ActiveTab;
            Repository = Tab is { IsDashboard: false, Repository: { IsClosed: false } repository } ? repository : null;
            _context = Repository?.MenuContext;
            RepositoryDetail = Repository is not null && shell.NamesRepositoryInDialogs ? Repository.Name : null;
        }

        public RepoTabViewModel Tab { get; }

        public RepositoryViewModel? Repository { get; }

        /// <summary>The detail of a repository's commands (5.4's rows 1 to 10, KD2): its name while dialogs name it (3.8).</summary>
        public string? RepositoryDetail { get; }

        public MenuModel? EmptyArea => _context is null ? null : _emptyArea ??= RepositoryMenus.ForEmptyGraphArea(_context, Repository!.MenuActions);

        public MenuModel? WorkingChanges => _context is null ? null : _workingChanges ??= RepositoryMenus.ForWorkingChanges(_context, Repository!.MenuActions);

        /// <summary>The open diff's menu (5.5), as its menu opens without a hunk; null without a diff.</summary>
        public MenuModel? Diff
        {
            get
            {
                if (!_diffBuilt)
                {
                    _diffBuilt = true;
                    _diff = _context is null ? null : Repository!.DiffMenuForPalette(_context);
                }

                return _diff;
            }
        }

        public MenuModel? Section(RefSection section)
        {
            if (_context is null)
            {
                return null;
            }

            if (!_sections.TryGetValue(section, out var menu))
            {
                menu = RepositoryMenus.ForSection(_context, section, Repository!.MenuActions);
                _sections[section] = menu;
            }

            return menu;
        }
    }
}
