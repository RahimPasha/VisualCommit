using System.Collections.ObjectModel;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Undo;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels;

/// <summary>What every repo tab uses: the app's services and the shared list of recent repositories.</summary>
public sealed class TabServices
{
    public TabServices(
        IRepositoryProvider repositories,
        IFolderPicker folders,
        ISettingsStore settings,
        ISessionStore session,
        DateDisplay dates,
        IAppLog? log = null,
        TimeProvider? clock = null,
        IDialogService? dialogs = null,
        IOperationLog? operations = null,
        IGitPromptBroker? prompts = null,
        IFileLauncher? launcher = null,
        IClipboardService? clipboard = null,
        IGitCallLog? gitCalls = null,
        TimeProvider? recordClock = null,
        UndoHistories? undoHistories = null,
        IWorkspaceStore? workspaces = null,
        IGitConfiguration? gitSettings = null,
        ToolCatalogues? tools = null,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dates);
        Repositories = repositories;
        Folders = folders;
        Settings = settings;
        Dates = dates;
        Log = log ?? NullAppLog.Instance;
        Session = session;
        Clock = clock ?? TimeProvider.System;
        Recent = new RecentRepositories(session, Clock);
        Dialogs = dialogs ?? new DialogHostViewModel();
        DialogHost = Dialogs as DialogHostViewModel ?? new DialogHostViewModel();
        Operations = operations ?? new OperationLog();
        Prompts = prompts ?? NullGitPromptBroker.Instance;
        Launcher = launcher;
        Clipboard = clipboard;
        GitCalls = gitCalls;
        RecordClock = recordClock ?? TimeProvider.System;
        UndoHistories = undoHistories ?? new UndoHistories();

        // A filter of a repository whose folder is gone is dropped at start-up (docs/phase-5-design.md 8.4).
        GraphFilters = new GraphFilterStore(session, Clock);
        GraphFilters.DropMissingFolders();

        // Phase 6 (docs/phase-6-design.md 1.3, 3.4, 4.3, 8.1, 12.9, 2.10).
        Workspaces = workspaces ?? new InMemoryWorkspaceStore();
        GitSettings = gitSettings;
        Tools = tools;
        ReadClock = readClock ?? Clock;
        OpenRepositories = new OpenRepositories();
        BulkSlots = new BulkSlots();
        WorkspaceCommands = new WorkspaceCommands(this);
    }

    public IRepositoryProvider Repositories { get; }

    public IFolderPicker Folders { get; }

    public ISettingsStore Settings { get; }

    public DateDisplay Dates { get; }

    public IAppLog Log { get; }

    public RecentRepositories Recent { get; }

    /// <summary>
    /// The session file (D46): the tabs, the recent list, the panels' sizes, and phase 5's graph
    /// filters per repository and the activity log's height (docs/phase-5-design.md 8.4, 2.1),
    /// which are written when they change.
    /// </summary>
    public ISessionStore Session { get; }

    /// <summary>The window's confirmations (D69).</summary>
    public IDialogService Dialogs { get; }

    /// <summary>
    /// The window's dialog layer, for dialogs beyond a confirmation (D104): <see cref="Dialogs"/>
    /// itself, or a layer of its own when a test gave a fake confirmation service.
    /// </summary>
    public DialogHostViewModel DialogHost { get; }

    /// <summary>The clock: the recent list's times and auto-fetch's timer.</summary>
    public TimeProvider Clock { get; }

    /// <summary>The record of every operation of the session (docs/phase-3-design.md 1.7), handed to each repository's queue.</summary>
    public IOperationLog Operations { get; }

    /// <summary>The prompt channel (section 3): each operation and each clone opens a scope here.</summary>
    public IGitPromptBroker Prompts { get; }

    /// <summary>Opens files and shows them in the file manager (2.32); null where nothing may be opened (view-model tests).</summary>
    public IFileLauncher? Launcher { get; }

    /// <summary>The clipboard (2.32); null where nothing may be copied (view-model tests).</summary>
    public IClipboardService? Clipboard { get; }

    /// <summary>
    /// Whether a dialog that an operation of a repository opens names that repository ("In
    /// &lt;repo&gt;" under its title, docs/phase-3-design.md 4f): the window's one rule
    /// (<see cref="MainWindowViewModel.NamesRepositoryInDialogs"/>, more than one tab has a
    /// repository), which the window sets here when it is made. False without a window.
    /// </summary>
    public Func<bool> NamesRepositoryInDialogs { get; set; } = static () => false;

    /// <summary>
    /// The record of every git call of the session (1.7), each with its operation's id: where an
    /// operation finds the command and the output of a failed call that its git method reported
    /// only as an outcome (an empty cherry-pick, 2.12). Null where nothing records the calls.
    /// </summary>
    public IGitCallLog? GitCalls { get; }

    /// <summary>
    /// The clock of the times the activity log shows, given to each repository's queue
    /// (docs/phase-5-design.md 2.3): the system clock, or the harnesses' frozen one.
    /// </summary>
    public TimeProvider RecordClock { get; }

    /// <summary>The undo histories of the session, one per worktree (docs/phase-5-design.md 7.1), shared by every tab.</summary>
    public UndoHistories UndoHistories { get; }

    /// <summary>What each repository's graph hides and solos, kept in <see cref="Session"/> (docs/phase-5-design.md 8.4, D159).</summary>
    public GraphFilterStore GraphFilters { get; }

    /// <summary>The user's workspaces (D189, docs/phase-6-design.md 1.3): the session's store, or one in memory for view-model tests.</summary>
    public IWorkspaceStore Workspaces { get; }

    /// <summary>
    /// The workspaces as every welcome page lists them (13.1), shared as <see cref="RecentRepositories.Items"/>
    /// is. Work package WP-W keeps it: rebuilt on <see cref="IWorkspaceStore.Changed"/>, sorted by name, then id.
    /// </summary>
    public ObservableCollection<WorkspaceItem> WorkspaceItems { get; } = [];

    /// <summary>Every workspace flow (1.5 to 1.10), one per session: the welcome page, the dashboard and the palette run the same code.</summary>
    public WorkspaceCommands WorkspaceCommands { get; }

    /// <summary>The window, through which a workspace opens (1.3, 2.2); set by the window when it is made, null without one.</summary>
    public IWorkspaceHost? WorkspaceHost { get; set; }

    /// <summary>The session's repository view models by worktree: the tabs' and the dashboards' summary-mode ones (3.4).</summary>
    public OpenRepositories OpenRepositories { get; }

    /// <summary>The window's slots for the rows of bulk runs (4.3).</summary>
    public BulkSlots BulkSlots { get; }

    /// <summary>Git's own settings, read and written by the settings screen (8.1); null where there is none (view-model tests, and the session until work package WP2).</summary>
    public IGitConfiguration? GitSettings { get; }

    /// <summary>
    /// What the machine has installed, as the harnesses fix it (12.9): <see cref="ToolCatalogues.Fixed"/>
    /// in the harnesses; null for the machine's own (the settings screen, Open file and the merge
    /// tools' lookup then read the machine).
    /// </summary>
    public ToolCatalogues? Tools { get; }

    /// <summary>
    /// The clock of a dashboard's 10 s rule (2.10): <see cref="Clock"/> unless the harness gives one
    /// (17.2), so that a check moves it without waiting while every other time of the session runs on.
    /// </summary>
    public TimeProvider ReadClock { get; }

    /// <summary>
    /// Opens the settings screen on a section (7.2): set by the window, for the editor failure's
    /// "Open settings…" (12.7); null without a window.
    /// </summary>
    public Func<SettingsSection, Task>? OpenSettings { get; set; }

    /// <summary>
    /// Raised on the UI thread after a Save of the settings screen wrote any git key (8.11): every
    /// repository view model reads <c>merge.tool</c> again while it shows conflicts.
    /// </summary>
    public event EventHandler? GitSettingsChanged;

    /// <summary>Raises <see cref="GitSettingsChanged"/> (the settings screen's Save, 8.11).</summary>
    public void RaiseGitSettingsChanged() => GitSettingsChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>The sections of the settings screen (docs/phase-6-design.md 7.1, 7.2): where it opens.</summary>
public enum SettingsSection
{
    General,
    Git,
    Tools,
    KeyboardShortcuts,
}

/// <summary>A repository in the welcome page's recent list.</summary>
/// <param name="Path">Its working tree.</param>
/// <param name="Name">Its folder's name.</param>
public sealed record RecentRepositoryItem(string Path, string Name);

/// <summary>
/// The recently opened repositories (D42, D46), newest first and at most
/// <see cref="SessionState.MaxRecent"/>, shared by every tab's welcome page and kept in the
/// session file.
/// </summary>
public sealed class RecentRepositories
{
    private readonly ISessionStore _session;
    private readonly TimeProvider _clock;

    public RecentRepositories(ISessionStore session, TimeProvider clock)
    {
        _session = session;
        _clock = clock;
        foreach (var recent in session.Current.Recent.Take(SessionState.MaxRecent))
        {
            Items.Add(new RecentRepositoryItem(recent.Path, recent.Name));
        }
    }

    /// <summary>The list the welcome pages show.</summary>
    public ObservableCollection<RecentRepositoryItem> Items { get; } = [];

    /// <summary>Puts a repository at the top of the list, removing an older entry for the same folder, and saves the list.</summary>
    public void Add(string path, string name)
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (SamePath(Items[i].Path, path))
            {
                Items.RemoveAt(i);
            }
        }

        Items.Insert(0, new RecentRepositoryItem(path, name));
        while (Items.Count > SessionState.MaxRecent)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        var now = _clock.GetUtcNow();
        _session.Update(state => state with
        {
            Recent = [.. Items.Select(item => new RecentRepository(item.Path, item.Name, SamePath(item.Path, path)
                ? now
                : state.Recent.FirstOrDefault(old => SamePath(old.Path, item.Path))?.LastOpened ?? now))],
        });
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a)),
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b)),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
