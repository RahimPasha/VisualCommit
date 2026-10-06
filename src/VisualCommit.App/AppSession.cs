using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
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
        IGitCallLog gitCalls,
        MainWindowViewModel shell,
        MainWindow mainWindow)
    {
        Paths = paths;
        Log = log;
        Settings = settings;
        GitCalls = gitCalls;
        Shell = shell;
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => _windowClosed = true;
        Initialization = shell.InitializeAsync(_lifetime.Token);
    }

    public AppPaths Paths { get; }

    public IAppLog Log { get; }

    public ISettingsStore Settings { get; }

    /// <summary>Every git call this session made.</summary>
    public IGitCallLog GitCalls { get; }

    /// <summary>The view model of the main window.</summary>
    public MainWindowViewModel Shell { get; }

    /// <summary>The main window. Created but not shown; the caller shows it.</summary>
    public MainWindow MainWindow { get; }

    /// <summary>Completes when the start-up work that runs after the window is created (finding git) is done.</summary>
    public Task Initialization { get; }

    /// <summary>
    /// Starts the app on <paramref name="paths"/>: loads the settings, applies the saved theme
    /// and creates the main window. Must be called on the UI thread.
    /// </summary>
    public static AppSession Start(AppPaths paths, Application application)
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

        var gitCalls = new GitCallLog();
        var shell = new MainWindowViewModel(
            settings,
            themes,
            cancellationToken => GitLocator.DetectAsync(gitCalls, log, cancellationToken),
            log);
        var mainWindow = new MainWindow { DataContext = shell };

        return new AppSession(paths, log, settings, gitCalls, shell, mainWindow);
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

        Log.Info("VisualCommit closed.");
    }
}
