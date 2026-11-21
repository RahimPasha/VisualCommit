using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;

namespace VisualCommit.App;

public partial class App : Application
{
    static App()
    {
        // Every tooltip is drawn inside the window, as the context menu is (docs/phase-3-design.md
        // 4a, D105): a tooltip in a window of its own reached past the main window's edge and was
        // cut in a capture of its client area. The property's default cannot be overridden for
        // Control, the type it is registered on (Avalonia refuses: its metadata is set), so the
        // default is given as each tooltip opens: Avalonia raises ToolTipOpening on the control
        // before it binds the tooltip's popup to the control's value. A control that sets the
        // property itself keeps its own value. Once per process (a static constructor).
        ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>(UseOverlayLayerForToolTip, handledEventsToo: true);
    }

    public App()
    {
        AboutCommand = new RelayCommand(ShowAbout);
    }

    /// <summary>The window's shell, once the session has started; null before, and in a harness that sets none.</summary>
    public MainWindowViewModel? Shell { get; set; }

    /// <summary>
    /// The macOS application menu's "About VisualCommit" (D431): the settings screen on About, as
    /// the palette's "About VisualCommit…" opens it. While a dialog is open, the settings screen
    /// among them, it does nothing, as Mod+, does.
    /// </summary>
    public ICommand AboutCommand { get; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (NativeMenu.GetMenu(this)?.Items.OfType<NativeMenuItem>().FirstOrDefault() is { } about)
        {
            about.Command = AboutCommand;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Only the real desktop app starts a session here. Headless tests have no desktop
        // lifetime; they call AppSession.Start themselves with a temporary data folder.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var launch = AppLaunch.Current;
            if (launch is { Mode: AppStartMode.NotAnswered, Guard: { } guard })
            {
                ShowNotice(desktop, launch, guard);
            }
            else
            {
                desktop.MainWindow = StartSession(desktop, launch);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Starts the session and hands its main window back; the guard, when there is one, answers a
    /// second start from the window's opening on (brought forward on the UI thread) and, once the
    /// window has closed, says the app is ending. No dispatch while the dispatcher stops.
    /// </summary>
    private MainWindow StartSession(IClassicDesktopStyleApplicationLifetime desktop, AppLaunch launch)
    {
        var session = AppSession.Start(AppPaths.Resolve(), this, startup: launch.Timeline);
        desktop.Exit += (_, _) => session.Dispose();
        Shell = session.Shell;
        var window = session.MainWindow;
        if (launch.Guard is { } guard)
        {
            window.Opened += (_, _) => guard.ComeForward =
                () => Dispatcher.UIThread.InvokeAsync(window.BringForward).GetTask().GetAwaiter().GetResult();
            window.Closed += (_, _) => guard.ComeForward = () => ForwardAnswer.Closing;
        }

        return window;
    }

    /// <summary>
    /// Another VisualCommit holds the data folder and did not answer (D421): the notice alone, as the
    /// main window, with no session and nothing written. The lifetime waits for an explicit end, so
    /// that it outlives the notice when "Try again" finds the folder free and starts the session in
    /// this process (D422). The exit code stays 2 until a try hands over or starts here. The notice's
    /// theme is the saved one, read without writing; System is the platform's mode now.
    /// </summary>
    private void ShowNotice(IClassicDesktopStyleApplicationLifetime desktop, AppLaunch launch, SingleInstanceGuard guard)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        launch.ExitCode = 2;
        var paths = AppPaths.Resolve();
        var platform = OperatingSystem.IsWindows() ? GuidancePlatform.Windows
            : OperatingSystem.IsMacOS() ? GuidancePlatform.MacOS
            : GuidancePlatform.Linux;
        var notice = new InstanceNoticeViewModel(platform, paths.DataDirectory, () => Task.Run(() => InstanceNoticeViewModel.RetryOn(guard)));
        var dark = InstanceNoticeViewModel.ReadTheme(paths.SettingsFile) switch
        {
            AppTheme.Light => false,
            AppTheme.System => PlatformSystemTheme.IsDarkIn(PlatformSettings?.GetColorValues()),
            _ => true,
        };
        var window = new InstanceNoticeWindow
        {
            DataContext = notice,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };

        // On the UI thread; the window closes itself after this, once the notice has ended.
        notice.Finished += (_, outcome) =>
        {
            switch (outcome)
            {
                case NoticeOutcome.StartHere:
                    var main = StartSession(desktop, launch);
                    desktop.MainWindow = main;
                    main.Show();
                    desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
                    launch.ExitCode = 0;
                    break;
                case NoticeOutcome.HandedOver:
                    launch.ExitCode = 0;
                    desktop.Shutdown(launch.ExitCode);
                    break;
                default:
                    desktop.Shutdown(launch.ExitCode);
                    break;
            }
        };
        desktop.MainWindow = window;
    }

    private void ShowAbout()
    {
        if (Shell is { } shell && !shell.Dialogs.IsOpen)
        {
            shell.OpenSettingsCommand.Execute(SettingsSection.About);
        }
    }

    private static void UseOverlayLayerForToolTip(Control control, RoutedEventArgs e)
    {
        if (!control.IsSet(ToolTip.ShouldUseOverlayLayerProperty))
        {
            ToolTip.SetShouldUseOverlayLayer(control, true);
        }
    }
}
