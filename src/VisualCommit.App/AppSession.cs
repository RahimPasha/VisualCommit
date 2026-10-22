using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Undo;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git;

namespace VisualCommit.App;

/// <summary>
/// One running instance of the app: its services and its main window, all working on one data
/// folder. This is the composition root. The desktop app starts one session in
/// <see cref="App.OnFrameworkInitializationCompleted"/>; tests start their own on a temporary
/// data folder, and "restart" by disposing a session and starting another on the same folder.
/// </summary>
public sealed class AppSession : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly FileAppLog _log;
    private readonly PlatformSystemTheme? _ownedSystemTheme;
    private bool _disposed;
    private bool _windowClosed;

    private AppSession(
        AppPaths paths,
        FileAppLog log,
        ISettingsStore settings,
        ISessionStore session,
        IWorkspaceStore workspaces,
        IThemeService themes,
        PlatformSystemTheme? ownedSystemTheme,
        IGitCallLog gitCalls,
        TabServices services,
        MainWindowViewModel shell,
        MainWindow mainWindow)
    {
        Paths = paths;
        _log = log;
        Log = log;
        Settings = settings;
        SessionState = session;
        Workspaces = workspaces;
        Themes = themes;
        _ownedSystemTheme = ownedSystemTheme;
        GitCalls = gitCalls;
        Services = services;
        Shell = shell;
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => _windowClosed = true;
        Initialization = shell.InitializeAsync(_lifetime.Token);
    }

    public AppPaths Paths { get; }

    public IAppLog Log { get; }

    public ISettingsStore Settings { get; }

    /// <summary>The open tabs, recent repositories, window placement and panel widths (D46).</summary>
    public ISessionStore SessionState { get; }

    /// <summary>
    /// The user's workspaces (D189, docs/phase-6-design.md 1.3), kept in <c>workspaces.json</c> by
    /// <c>JsonWorkspaceStore</c>.
    /// </summary>
    public IWorkspaceStore Workspaces { get; }

    /// <summary>The app's theme (D194, docs/phase-6-design.md 11.5): the user's choice and what the window shows.</summary>
    public IThemeService Themes { get; }

    /// <summary>Every git call this session made.</summary>
    public IGitCallLog GitCalls { get; }

    /// <summary>What every tab uses, phase 3's services included: the operation log, the prompt broker, the file launcher and the clipboard.</summary>
    public TabServices Services { get; }

    /// <summary>Every operation this session ran (docs/phase-3-design.md 1.7).</summary>
    public IOperationLog Operations => Services.Operations;

    /// <summary>The view model of the main window.</summary>
    public MainWindowViewModel Shell { get; }

    /// <summary>The main window. Created but not shown; the caller shows it.</summary>
    public MainWindow MainWindow { get; }

    /// <summary>Completes when the start-up work that runs after the window is created (finding git) is done.</summary>
    public Task Initialization { get; }

    /// <summary>
    /// Starts the app on <paramref name="paths"/>: loads the settings and the session, applies
    /// the saved theme and creates the main window with the tabs of the last session. Must be
    /// called on the UI thread. <paramref name="folderPicker"/> replaces the platform's folder
    /// dialog; the scripted walk-through passes a fake (D42). <paramref name="launcher"/>,
    /// <paramref name="clipboard"/> and <paramref name="promptHandler"/> replace the file launcher,
    /// the clipboard and the prompt dialogs the same way (docs/phase-3-design.md 2.32, 3.5, 7.4).
    /// <paramref name="recordClock"/> gives the activity log's times to every queue and to the git
    /// runner, <paramref name="backupClock"/> names the backups of every repository the app opens
    /// and is their clean-up's "now" (docs/phase-5-design.md 2.3, D173); without them, the clocks
    /// the environment asks for (<see cref="AppClocks"/>: the system's, or the real-window pass's
    /// fixed ones). The headless harness gives fixed ones.
    /// <para>
    /// Phase 6 (docs/phase-6-design.md 17.2, 11.5, 12.9, 7.3, 2.10): <paramref name="gitEnvironment"/>
    /// is the session's own git environment, which its one git runner applies (the harnesses' global
    /// configuration in the data folder; none for the desktop app); <paramref name="systemTheme"/>
    /// the operating system's mode (null: the platform's, which the session makes, owns and
    /// disposes; the headless harness gives a fake); <paramref name="tools"/> what the machine has
    /// installed (null: <see cref="ToolCatalogues.FromEnvironment"/>, and null again the machine's;
    /// the harnesses give <see cref="ToolCatalogues.Fixed"/>); <paramref name="readEnvironment"/>
    /// the environment reader auto-fetch's interval goes by (null: the process's);
    /// <paramref name="readClock"/> the clock of a dashboard's 10 s rule (null: the session's clock).
    /// </para>
    /// </summary>
    public static AppSession Start(
        AppPaths paths,
        Application application,
        IFolderPicker? folderPicker = null,
        IFileLauncher? launcher = null,
        IClipboardService? clipboard = null,
        IGitPromptHandler? promptHandler = null,
        TimeProvider? recordClock = null,
        BackupClock? backupClock = null,
        IReadOnlyDictionary<string, string?>? gitEnvironment = null,
        ISystemTheme? systemTheme = null,
        ToolCatalogues? tools = null,
        Func<string, string?>? readEnvironment = null,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(application);
        recordClock ??= AppClocks.RecordClockFromEnvironment();
        backupClock ??= AppClocks.BackupClockFromEnvironment();
        tools ??= ToolCatalogues.FromEnvironment();

        // Every key text is in the platform's words (docs/phase-6-design.md 6.4), set before the
        // window is made: Cmd where the command modifier is Meta (macOS), Ctrl otherwise.
        KeyTexts.Current = new KeyTexts(
            application.PlatformSettings?.HotkeyConfiguration.CommandModifiers.HasFlag(KeyModifiers.Meta) == true
                ? CommandKey.Cmd
                : CommandKey.Ctrl);

        Directory.CreateDirectory(paths.DataDirectory);
        var log = new FileAppLog(paths.LogDirectory);
        log.Info(
            $"VisualCommit {AppVersion} started. Data folder: {paths.DataDirectory}. " +
            $"{RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}.");

        var settings = new JsonSettingsStore(paths.SettingsFile, log);

        // The theme is applied before the window exists, so the first frame already has it (11.5).
        var ownedSystemTheme = systemTheme is null ? new PlatformSystemTheme(application.PlatformSettings) : null;
        var themes = new ThemeService(application, systemTheme ?? ownedSystemTheme!, log);
        themes.Apply(settings.Current.Theme);
        log.Info(settings.Current.Theme switch
        {
            AppTheme.Light => "Theme: Light.",
            AppTheme.System => $"Theme: System (the system's theme is {(themes.IsDark ? "dark" : "light")}).",
            _ => "Theme: Dark.",
        });

        var session = new JsonSessionStore(paths.SessionFile, log);

        // The workspaces (1.3, D189), after the session: workspaces.json beside it.
        var workspaces = new JsonWorkspaceStore(paths.WorkspacesFile, log);

        // An operation's git calls are kept as long as its record (docs/phase-5-design.md 2.3).
        var operations = new OperationLog();
        var gitCalls = new GitCallLog(operations: operations);

        // Git is looked for once the window exists (MainWindowViewModel.InitializeAsync); the
        // services that need it wait for that search through GitAccess (D45).
        var gitFound = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<GitDetection> DetectGitAsync(CancellationToken cancellationToken)
        {
            try
            {
                var detection = await GitLocator.DetectAsync(gitCalls, log, recordClock, gitEnvironment, cancellationToken);
                gitFound.TrySetResult(detection);
                return detection;
            }
            catch (OperationCanceledException)
            {
                gitFound.TrySetCanceled(cancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                gitFound.TrySetException(ex);
                throw;
            }
        }

        // Phase 3 (docs/phase-3-design.md 1.7, 2.32, 3.1): one record of the operations, the
        // prompt channel of the session, and the file launcher and clipboard of the window. Phase 5
        // (docs/phase-5-design.md 2.3, 7.1): the record clock of every queue, and the undo
        // histories of the session's worktrees.
        MainWindow? mainWindow = null;
        var dialogs = new DialogHostViewModel();
        var promptPresenter = new PromptDialogPresenter(dialogs, dialogs.Notices);
        var prompts = PromptDialogs.CreateBroker(promptHandler ?? new PromptDialogs(promptPresenter, log), log);

        // Git's own settings (docs/phase-6-design.md 8.1): one instance over the session's runner,
        // with the kept list of merge tools in the data folder (8.10); disposed with the session.
        var gitAccess = new GitAccess(gitFound.Task);
        var gitSettings = new GitConfiguration(gitAccess.GetRunnerAsync, paths.DataDirectory, paths.MergeToolsFile, gitEnvironment, log);
        var tabServices = new TabServices(
            new GitRepositoryProvider(gitAccess, log, backupClock),
            folderPicker ?? new StorageFolderPicker(() => mainWindow),
            settings,
            session,
            DateDisplay.Resolve(),
            log,
            dialogs: dialogs,
            operations: operations,
            prompts: prompts,
            launcher: launcher ?? new FileLauncher(() => mainWindow, log),
            clipboard: clipboard ?? new ClipboardService(() => mainWindow),
            gitCalls: gitCalls,
            recordClock: recordClock,
            undoHistories: new UndoHistories(),
            workspaces: workspaces,
            gitSettings: gitSettings,
            tools: tools,
            readClock: readClock);
        var shell = new MainWindowViewModel(settings, themes, DetectGitAsync, log, tabServices, session, readEnvironment);

        // A prompt dialog names its repository by the window's one rule, as every other dialog (4f).
        promptPresenter.ShowsRepositoryName = () => shell.NamesRepositoryInDialogs;
        mainWindow = new MainWindow { DataContext = shell };
        mainWindow.UseSession(session);

        return new AppSession(paths, log, settings, session, workspaces, themes, ownedSystemTheme, gitCalls, tabServices, shell, mainWindow);
    }

    public static string AppVersion =>
        typeof(AppSession).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        if (!_windowClosed)
        {
            // The session ends: the window does not wait for running operations (1.8).
            Shell.ConfirmClose();
            MainWindow.Close();
        }

        // Stops every tab's work and logs each graph's frame times (D50).
        Shell.CloseAll();
        (Services.Prompts as IDisposable)?.Dispose();

        // Stops the merge tools' lookup (docs/phase-6-design.md 8.1, 8.10).
        Services.GitSettings?.Dispose();

        // The theme service stops following the system before the system's theme it owns goes (11.5).
        (Themes as IDisposable)?.Dispose();
        _ownedSystemTheme?.Dispose();

        Log.Info("VisualCommit closed.");

        // Git calls the session cancelled end a moment later and would log that they did: into
        // a data folder a test may have deleted by then (recreating it), or into the log of the
        // next session on the same folder. Nothing is written after "closed".
        _log.Close();
    }
}
