using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VisualCommit.Core;

namespace VisualCommit.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Only the real desktop app starts a session here. Headless tests have no desktop
        // lifetime; they call AppSession.Start themselves with a temporary data folder.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = AppSession.Start(AppPaths.Resolve(), this);
            desktop.MainWindow = session.MainWindow;
            desktop.Exit += (_, _) => session.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
