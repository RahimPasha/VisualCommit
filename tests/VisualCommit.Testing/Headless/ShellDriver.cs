using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App;
using VisualCommit.App.Services;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Session;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Runs the whole app in headless mode for a test and drives it the way a user would: with mouse
/// moves, presses and releases at window positions. One driver is one running instance of the
/// app; disposing it closes the app. Use it on the UI thread, inside an <c>[AvaloniaFact]</c>
/// or <see cref="FreshApplication.RunAsync{T}"/>.
/// </summary>
public sealed class ShellDriver : IDisposable
{
    /// <summary>How long <see cref="WaitForAsync"/> waits when the test names no time.</summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pause between two looks at a condition in <see cref="WaitForAsync"/>: short enough that
    /// a wait ends soon after the condition comes true, long enough not to keep a core busy.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

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

    /// <summary>
    /// Starts the app as <see cref="Start"/> does, after writing a session file that has these
    /// tabs open: the way a test starts the app with repositories open (D46). Each entry is the
    /// working tree of the repository in that tab, or null for an empty tab.
    /// </summary>
    public static ShellDriver StartWithTabs(
        string dataDirectory,
        IReadOnlyList<string?> tabs,
        int activeTab = 0,
        int? width = 1100,
        int? height = 700)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        WriteSession(
            dataDirectory,
            new SessionState { Tabs = tabs.Select(path => new TabState(path)).ToList(), ActiveTab = activeTab });
        return Start(dataDirectory, width, height);
    }

    /// <summary>
    /// Writes the session file of a data folder before the app starts on it, for a test that
    /// needs more than open tabs: the recent list, the window's placement, the panel widths.
    /// </summary>
    public static void WriteSession(string dataDirectory, SessionState state) =>
        JsonSessionStore.Write(new AppPaths(dataDirectory).SessionFile, state);

    /// <summary>Waits for the app's start-up work (finding git) and for the layout to settle.</summary>
    public async Task WaitUntilReadyAsync()
    {
        await Session.Initialization;
        Settle();
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> is true, for work that ends later than the step
    /// that started it, such as loading a repository on the thread pool. Between looks it lets
    /// the UI thread run what was posted to it and then yields for a moment, so that work on
    /// other threads can finish and post back. The condition is read on the UI thread.
    /// It does not render: a condition about what is drawn needs <see cref="Capture"/>.
    /// </summary>
    /// <param name="condition">What to wait for. Read once per look.</param>
    /// <param name="what">What is awaited, in words, for the message when it does not come.</param>
    /// <param name="timeout">How long to wait; <see cref="DefaultWaitTimeout"/> when null.</param>
    /// <exception cref="TimeoutException">The condition was still false when the time was up.</exception>
    public async Task WaitForAsync(Func<bool> condition, string what, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        Dispatcher.UIThread.VerifyAccess();

        var limit = timeout ?? DefaultWaitTimeout;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            Settle();
            if (condition())
            {
                return;
            }

            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out after {limit.TotalSeconds:0.###} s waiting for {what}."));
            }

            // No ConfigureAwait(false): the next look must run on the UI thread, which an
            // [AvaloniaFact] keeps running while the test awaits.
            await Task.Delay(PollInterval);
        }
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

    /// <summary>Moves the mouse to the centre of a control without pressing a button: a hover.</summary>
    public void MoveMouse(Visual visual) => MoveMouse(BoundsOf(visual).Center);

    /// <summary>Moves the mouse to a window position without pressing a button: a hover.</summary>
    public void MoveMouse(Point position)
    {
        Window.MouseMove(position);
        Settle();
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

    /// <summary>
    /// Moves the mouse to a window position and turns the wheel, as a real wheel would:
    /// a positive <paramref name="deltaY"/> scrolls up (away from the user), a negative one down,
    /// and one notch is 1.0. The control under the mouse gets one wheel event with this delta.
    /// </summary>
    public void Wheel(Point position, double deltaY)
    {
        Window.MouseMove(position);
        Window.MouseWheel(position, new Vector(0, deltaY));
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
