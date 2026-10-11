using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VisualCommit.Core;

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

    private static void UseOverlayLayerForToolTip(Control control, RoutedEventArgs e)
    {
        if (!control.IsSet(ToolTip.ShouldUseOverlayLayerProperty))
        {
            ToolTip.SetShouldUseOverlayLayer(control, true);
        }
    }
}
