using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App;
using VisualCommit.App.Views;
using VisualCommit.Core;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Runs the whole app in headless mode for a test and drives it the way a user would: with mouse
/// moves, presses and releases at window positions. One driver is one running instance of the
/// app; disposing it closes the app. Use it on the UI thread, inside an <c>[AvaloniaFact]</c>.
/// </summary>
public sealed class ShellDriver : IDisposable
{
    private ShellDriver(AppSession session)
    {
        Session = session;
    }

    /// <summary>The app instance under test.</summary>
    public AppSession Session { get; }

    public MainWindow Window => Session.MainWindow;

    /// <summary>
    /// Starts the app on <paramref name="dataDirectory"/> through the same start-up code as the
    /// desktop app, and shows its window at the given size in logical pixels. Pass null for a
    /// size to keep the one the app chose itself, for a check of what the app restores.
    /// </summary>
    public static ShellDriver Start(string dataDirectory, int? width = 1100, int? height = 700)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("No Avalonia application is running. Is the test an [AvaloniaFact]?");

        var session = AppSession.Start(new AppPaths(dataDirectory), application);
        if (width is not null)
        {
            session.MainWindow.Width = width.Value;
        }

        if (height is not null)
        {
            session.MainWindow.Height = height.Value;
        }

        session.MainWindow.Show();

        var driver = new ShellDriver(session);
        driver.Settle();
        return driver;
    }

    /// <summary>Waits for the app's start-up work (finding git) and for the layout to settle.</summary>
    public async Task WaitUntilReadyAsync()
    {
        await Session.Initialization;
        Settle();
    }

    /// <summary>Lets the UI thread finish everything it has queued: bindings, layout, rendering requests.</summary>
    public void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>Finds the control with this name anywhere in the window.</summary>
    public T Find<T>(string name)
        where T : Control =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named '{name}' is in the window.");

    /// <summary>Finds the control whose automation id is <paramref name="automationId"/>.</summary>
    public Control FindByAutomationId(string automationId) =>
        Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId)
        ?? throw new InvalidOperationException($"No control with the automation id '{automationId}' is in the window.");

    /// <summary>Where a control is in the window, in logical pixels.</summary>
    public Rect BoundsOf(Visual visual)
    {
        var topLeft = visual.TranslatePoint(default, Window)
            ?? throw new InvalidOperationException("The control is not in the window.");
        return new Rect(topLeft, visual.Bounds.Size);
    }

    /// <summary>Moves the mouse to the centre of a control and clicks the left button.</summary>
    public void Click(Visual visual) => Click(BoundsOf(visual).Center);

    /// <summary>Moves the mouse to a window position and clicks the left button.</summary>
    public void Click(Point position)
    {
        Window.MouseMove(position);
        Window.MouseDown(position, MouseButton.Left);
        Window.MouseUp(position, MouseButton.Left);
        Settle();
    }

    /// <summary>Moves the mouse to the centre of a control and clicks the right button.</summary>
    public void RightClick(Visual visual) => RightClick(BoundsOf(visual).Center);

    /// <summary>Moves the mouse to a window position and clicks the right button.</summary>
    public void RightClick(Point position)
    {
        Window.MouseMove(position);
        Window.MouseDown(position, MouseButton.Right);
        Window.MouseUp(position, MouseButton.Right);
        Settle();
    }

    /// <summary>Moves the mouse to the centre of a control and double-clicks the left button.</summary>
    public void DoubleClick(Visual visual) => DoubleClick(BoundsOf(visual).Center);

    /// <summary>Moves the mouse to a window position and double-clicks the left button.</summary>
    public void DoubleClick(Point position)
    {
        Window.MouseMove(position);
        for (var click = 0; click < 2; click++)
        {
            // Two presses at one spot in quick succession: Avalonia counts them as a double-click.
            Window.MouseDown(position, MouseButton.Left);
            Window.MouseUp(position, MouseButton.Left);
        }

        Settle();
    }

    /// <summary>Types text into whatever has the keyboard focus, as key presses would.</summary>
    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Settle();
    }

    /// <summary>Presses and releases one key, with optional modifiers such as Control.</summary>
    public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    /// <summary>Presses the left button at <paramref name="from"/>, moves to <paramref name="to"/> in small steps and releases.</summary>
    public void Drag(Point from, Point to, int steps = 6)
    {
        Window.MouseMove(from);
        Window.MouseDown(from, MouseButton.Left);
        for (var step = 1; step <= steps; step++)
        {
            Window.MouseMove(from + ((to - from) * step / steps), RawInputModifiers.LeftMouseButton);
            Settle();
        }

        Window.MouseUp(to, MouseButton.Left);
        Settle();
    }

    /// <summary>Renders the window and returns the frame as a screenshot.</summary>
    public Screenshot Capture()
    {
        Settle();
        var frame = Window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("The window has not rendered a frame.");
        return new Screenshot(frame);
    }

    public void Dispose() => Session.Dispose();
}
