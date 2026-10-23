using VisualCommit.App.ViewModels.Commands;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels;

// The keys and the command palette (D191, D192; docs/phase-6-design.md 5, 6): the commands' states
// and runs (AppCommands), the palette, and the repositories and workspaces it and the keys open.
public partial class MainWindowViewModel
{
    /// <summary>The most entries the session's runs keep (5.6).</summary>
    public const int MaxPaletteRuns = 20;

    private readonly List<string> _paletteRuns = [];
    private AppCommands? _commands;

    /// <summary>
    /// The shown key texts of the shell's buttons (6.8), from <see cref="KeyTexts.Current"/>: what
    /// their <c>AutomationProperties.AcceleratorKey</c> binds to, enabled or not.
    /// </summary>
    public ShellKeys Keys { get; } = new(KeyTexts.Current);

    /// <summary>What the window does for a key or a palette entry that moves the focus (6.3); set by <c>MainWindow</c> with its data context, null without a window.</summary>
    public IWindowKeyActions? WindowActions { get; set; }

    /// <summary>The commands for the active tab (6.4), which the router, the palette and the tests share, in <see cref="KeyTexts.Current"/>'s words.</summary>
    public AppCommands Commands => _commands ??= new AppCommands(this, KeyTexts.Current);

    /// <summary>The <see cref="PaletteEntry.Key"/>s the palette ran this session, newest first, at most <see cref="MaxPaletteRuns"/>; in memory, never saved (5.6).</summary>
    public IReadOnlyList<string> PaletteRuns => _paletteRuns;

    /// <summary>The window's services, for the commands (<see cref="AppCommands"/>).</summary>
    internal TabServices Services => _tabServices;

    /// <summary>
    /// The worktrees' read the last opening of the palette started (5.5), with the items' rebuild
    /// it may have led to; completed when there was none. Tests wait on it.
    /// </summary>
    public Task PaletteWorktreeRead { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Opens the command palette (5.2, 5.9) with <paramref name="query"/> in its box: builds the
    /// commands and the items, shows the palette, and once it has closed runs the chosen entry.
    /// The opening also reads the worktrees (5.5) and builds the items again when the other
    /// worktrees differ from those they were built with while the palette is still open. Does
    /// nothing while a dialog is open. Completes when the palette has closed and its entry has
    /// started.
    /// </summary>
    public async Task OpenPaletteAsync(string query = "")
    {
        if (Dialogs.IsOpen)
        {
            return;
        }

        CommandPaletteViewModel palette;
        try
        {
            var commands = Commands.PaletteCommands();
            var repository = ActiveTab is { IsDashboard: false, Repository: { IsClosed: false } open } ? open : null;
            var context = repository?.MenuContext;
            palette = new CommandPaletteViewModel(commands, Commands.PaletteItems(commands, context), _paletteRuns.ToList(), query);
            _log.Debug($"{PalettePrefix()}Command palette opened");
            PaletteWorktreeRead = repository is not null && context is not null
                ? ReplaceItemsAfterTheWorktreesAsync(palette, repository, context.OtherWorktrees, commands)
                : Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _log.Error("The command palette failed to open.", ex);
            return;
        }

        var entry = await Dialogs.ShowAsync(palette);
        if (entry is null)
        {
            return;
        }

        // The palette has closed and the focus is back (5.9): the entry goes first in the session's
        // runs, the run is logged, then the entry runs.
        _paletteRuns.Remove(entry.Key);
        _paletteRuns.Insert(0, entry.Key);
        if (_paletteRuns.Count > MaxPaletteRuns)
        {
            _paletteRuns.RemoveRange(MaxPaletteRuns, _paletteRuns.Count - MaxPaletteRuns);
        }

        var detail = entry.Detail is null ? string.Empty : $" ({entry.Detail})";
        _log.Debug($"{PalettePrefix()}Command palette ran: {entry.Label}{detail}");
        try
        {
            entry.Run();
        }
        catch (Exception ex)
        {
            _log.Error($"{PalettePrefix()}The command palette's {entry.Label} failed.", ex);
        }
    }

    /// <summary>The palette while it is the top dialog, or null (6.3: Mod+Shift+P then selects its box's text).</summary>
    public CommandPaletteViewModel? OpenPalette => Dialogs.Current as CommandPaletteViewModel;

    /// <summary>
    /// <c>Open repository…</c> and its key (5.10): the folder dialog <c>Open a repository</c>, then
    /// <see cref="OpenRepositoryAtAsync"/>; a cancelled dialog changes nothing and makes no tab.
    /// </summary>
    public async Task OpenRepositoryAsync()
    {
        var path = await _tabServices.Folders.PickFolderAsync("Open a repository");
        if (path is not null)
        {
            await OpenRepositoryAtAsync(path);
        }
    }

    /// <summary>
    /// Opens the repository at <paramref name="path"/> from the palette or a key (5.10): the tab
    /// that shows that worktree is activated and nothing opens again; else the active tab opens it
    /// when it is empty; else a new tab at the end does. A failure shows on that tab's welcome
    /// page.
    /// </summary>
    public async Task OpenRepositoryAtAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var showing = Tabs.FirstOrDefault(tab => (tab.RepositoryPath ?? tab.PendingPath) is { } folder && WorkspacePaths.Same(folder, path));
        if (showing is not null)
        {
            ActivateTabCommand.Execute(showing);
            return;
        }

        await TabForOpening().OpenAsync(path);
    }

    /// <summary><c>Clone repository…</c> and its key (5.10): the active tab when it is empty, else a new tab at the end, shows the clone form with the focus in its URL box.</summary>
    public void CloneRepository() => TabForOpening().Welcome.OpenCloneFormCommand.Execute(null);

    /// <summary>
    /// <c>Init repository…</c> and its key (5.10): the folder dialog <c>Choose a folder for the new
    /// repository</c>, then the active tab when it is empty, else a new tab at the end, makes the
    /// repository there; a cancelled dialog changes nothing and makes no tab.
    /// </summary>
    public async Task InitRepositoryAsync()
    {
        var path = await _tabServices.Folders.PickFolderAsync(WelcomeViewModel.InitFolderTitle);
        if (path is not null)
        {
            await TabForOpening().Welcome.InitAtAsync(path);
        }
    }

    /// <summary>The tab a repository opened from the palette or a key goes to (5.10's steps 2 and 3): the active tab when it is empty, else a new tab at the end.</summary>
    private RepoTabViewModel TabForOpening()
    {
        if (ActiveTab.IsEmpty)
        {
            return ActiveTab;
        }

        NewTabCommand.Execute(null);
        return ActiveTab;
    }

    /// <summary>The palette's log prefix (5.9): <c>{repo}: </c> while the active tab has a repository, nothing otherwise.</summary>
    private string PalettePrefix() => ActiveTab is { IsDashboard: false, Repository: { } repository } ? repository.Name + ": " : string.Empty;

    /// <summary>
    /// The worktrees' read the palette starts (5.5): when it ends with other worktrees than
    /// <paramref name="builtWith"/> while the palette is open, its items are built again on the
    /// context the read returned; otherwise they stay.
    /// </summary>
    private async Task ReplaceItemsAfterTheWorktreesAsync(
        CommandPaletteViewModel palette,
        RepositoryViewModel repository,
        IReadOnlyList<WorktreeInfo> builtWith,
        IReadOnlyList<PaletteEntry> commands)
    {
        try
        {
            var context = await repository.ReadMenuContextAsync();
            if (palette.IsClosed || !ReferenceEquals(ActiveTab.Repository, repository) || context.OtherWorktrees.SequenceEqual(builtWith))
            {
                return;
            }

            palette.ReplaceItems(Commands.PaletteItems(commands, context));
        }
        catch (Exception ex)
        {
            _log.Warning($"{repository.Name}: the palette's items could not be built again.", ex);
        }
    }
}
