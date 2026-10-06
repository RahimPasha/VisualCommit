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
        var crashLog = new FileAppLog(AppPaths.Resolve().LogDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            crashLog.Error("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            crashLog.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
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
