using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
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
    private bool _disposed;
    private bool _windowClosed;

    private AppSession(
        AppPaths paths,
        IAppLog log,
        ISettingsStore settings,
        ISessionStore session,
        IGitCallLog gitCalls,
        TabServices services,
        MainWindowViewModel shell,
        MainWindow mainWindow)
    {
        Paths = paths;
        Log = log;
        Settings = settings;
        SessionState = session;
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
    /// </summary>
    public static AppSession Start(
        AppPaths paths,
        Application application,
        IFolderPicker? folderPicker = null,
        IFileLauncher? launcher = null,
        IClipboardService? clipboard = null,
        IGitPromptHandler? promptHandler = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(application);

        Directory.CreateDirectory(paths.DataDirectory);
        var log = new FileAppLog(paths.LogDirectory);
        log.Info(
            $"VisualCommit {AppVersion} started. Data folder: {paths.DataDirectory}. " +
            $"{RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}.");

        var settings = new JsonSettingsStore(paths.SettingsFile, log);

        // The theme is applied before the window exists, so the first frame already has it.
        var themes = new ThemeService(application);
        themes.Apply(settings.Current.Theme);

        var session = new JsonSessionStore(paths.SessionFile, log);
        var gitCalls = new GitCallLog();

        // Git is looked for once the window exists (MainWindowViewModel.InitializeAsync); the
        // services that need it wait for that search through GitAccess (D45).
        var gitFound = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<GitDetection> DetectGitAsync(CancellationToken cancellationToken)
        {
            try
            {
                var detection = await GitLocator.DetectAsync(gitCalls, log, cancellationToken);
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
        // prompt channel of the session, and the file launcher and clipboard of the window.
        MainWindow? mainWindow = null;
        var dialogs = new DialogHostViewModel();
        var promptPresenter = new PromptDialogPresenter(dialogs, dialogs.Notices);
        var prompts = PromptDialogs.CreateBroker(promptHandler ?? new PromptDialogs(promptPresenter, log), log);
        var tabServices = new TabServices(
            new GitRepositoryProvider(new GitAccess(gitFound.Task), log),
            folderPicker ?? new StorageFolderPicker(() => mainWindow),
            settings,
            session,
            DateDisplay.Resolve(),
            log,
            dialogs: dialogs,
            operations: new OperationLog(),
            prompts: prompts,
            launcher: launcher ?? new FileLauncher(() => mainWindow, log),
            clipboard: clipboard ?? new ClipboardService(() => mainWindow));
        var shell = new MainWindowViewModel(settings, themes, DetectGitAsync, log, tabServices, session);

        // A prompt dialog names its repository when more than one tab has one (4f).
        promptPresenter.ShowsRepositoryName =() => shell.Tabs.Count(tab => tab.Repository is not null) > 1;
        mainWindow = new MainWindow { DataContext = shell };
        mainWindow.UseSession(session);

        return new AppSession(paths, log, settings, session, gitCalls, tabServices, shell, mainWindow);
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

        Log.Info("VisualCommit closed.");
    }
}
