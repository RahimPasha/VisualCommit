using Avalonia;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Logging;

namespace VisualCommit.App;

internal static class Program
{
    // Avalonia is not initialised yet when Main starts: no Avalonia types or
    // SynchronizationContext-reliant code before BuildAvaloniaApp is called.
    [STAThread]
    public static int Main(string[] args)
    {
        // An updater's start-up hook, when the app has one, goes first, before the guard below: it
        // starts the app with arguments of its own, and must never be handed over to a running one.
        var paths = AppPaths.Resolve();
        var crashLog = new FileAppLog(paths.LogDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            crashLog.Error("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            crashLog.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };

        // One instance per data folder (D421). A second start asks the first to come to the front
        // and ends with 0 before Avalonia starts; when the first does not answer, the app shows a
        // notice and no session. Every wait here comes before any UI thread exists. Disposed after
        // the lifetime, and on the rethrow below.
        using var guard = SingleInstanceGuard.TryTake(paths, crashLog);
        var mode = AppStartMode.Normal;
        if (guard.Outcome == GuardOutcome.Held)
        {
            switch (guard.ResolveHeld())
            {
                case HeldResolution.HandedOver:
                    return 0;
                case HeldResolution.NotAnswered:
                    mode = AppStartMode.NotAnswered;
                    break;
            }
        }

        // The start's timeline, the guard and what the window shows, which App reads.
        AppLaunch.Current = new() { Timeline = StartupTimeline.FromProcess(crashLog), Guard = guard, Mode = mode };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            // 2 when the notice closed unanswered (D421), else 0.
            return AppLaunch.Current.ExitCode;
        }
        catch (Exception ex)
        {
            crashLog.Error("VisualCommit stopped because of an error.", ex);
            throw;
        }
    }

    // Also used by the IDE's visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
