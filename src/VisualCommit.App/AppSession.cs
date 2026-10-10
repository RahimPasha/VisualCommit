using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
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
        MainWindowViewModel shell,
        MainWindow mainWindow)
    {
        Paths = paths;
        Log = log;
        Settings = settings;
        SessionState = session;
        GitCalls = gitCalls;
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
    /// dialog; the scripted walk-through passes a fake (D42).
    /// </summary>
    public static AppSession Start(AppPaths paths, Application application, IFolderPicker? folderPicker = null)
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

        MainWindow? mainWindow = null;
        var tabServices = new TabServices(
            new GitRepositoryProvider(new GitAccess(gitFound.Task), log),
            folderPicker ?? new StorageFolderPicker(() => mainWindow),
            settings,
            session,
            DateDisplay.Resolve(),
            log);
        var shell = new MainWindowViewModel(settings, themes, DetectGitAsync, log, tabServices, session);
        mainWindow = new MainWindow { DataContext = shell };
        mainWindow.UseSession(session);

        return new AppSession(paths, log, settings, session, gitCalls, shell, mainWindow);
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
            MainWindow.Close();
        }

        // Stops every tab's work and logs each graph's frame times (D50).
        Shell.CloseAll();

        Log.Info("VisualCommit closed.");
    }
}
