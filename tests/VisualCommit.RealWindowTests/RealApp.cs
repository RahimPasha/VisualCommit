using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// One run of the built app in a real window on the Windows desktop. It is found and inspected
/// through UI Automation, driven with the real mouse and keyboard, and captured from the screen.
/// All positions taken or returned by this class are in logical pixels of the window's client
/// area, the same units as the scripted walk-through, unless a name says "screen".
/// Every method that gives input waits afterwards until the app has handled it and painted.
/// </summary>
public sealed class RealApp : IDisposable
{
    private static readonly string Executable =
        typeof(RealApp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "AppExecutable").Value!;

    /// <summary>
    /// The pause between two notches of <see cref="ScrollWheel"/>. A real wheel turned briskly
    /// sends a notch every few tens of milliseconds; sending them all at once would let the app
    /// fold them into one scroll and draw a single frame.
    /// </summary>
    private static readonly TimeSpan WheelNotchInterval = TimeSpan.FromMilliseconds(30);

    /// <summary>The pause between two steps of <see cref="DragWithMouse"/>, so the app sees a movement and not a jump.</summary>
    private static readonly TimeSpan DragStepInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>How often <see cref="WaitFor"/> looks at its condition.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly Process _process;
    private readonly UIA3Automation _automation;
    private readonly FlaUI.Core.Application _application;

    private RealApp(Process process, UIA3Automation automation, FlaUI.Core.Application application, Window window)
    {
        _process = process;
        _automation = automation;
        _application = application;
        Window = window;
        Handle = window.Properties.NativeWindowHandle.Value;
    }

    /// <summary>Anything that went wrong while cleaning up after a run. The pass writes these into its run notes.</summary>
    public static System.Collections.Concurrent.ConcurrentQueue<string> Problems { get; } = new();

    /// <summary>The app's main window, as UI Automation sees it.</summary>
    public Window Window { get; }

    public IntPtr Handle { get; }

    /// <summary>The display scaling of the window: 1 at 100%, 2 at 200%.</summary>
    public double Scaling => NativeMethods.GetDpiForWindow(Handle) / 96.0;

    /// <summary>The client area's size in logical pixels.</summary>
    public SizeF ClientSize
    {
        get
        {
            NativeMethods.GetClientRect(Handle, out var client);
            return new SizeF((float)(client.Width / Scaling), (float)(client.Height / Scaling));
        }
    }

    /// <summary>The size of the work area (the screen without the taskbar) of the window's monitor, in logical pixels.</summary>
    public SizeF WorkAreaSize
    {
        get
        {
            var work = WorkArea();
            return new SizeF((float)(work.Width / Scaling), (float)(work.Height / Scaling));
        }
    }

    /// <summary>
    /// The whole window with its frame, in screen pixels, as Windows reports it (GetWindowRect).
    /// On Windows 10 and 11 that includes the invisible border the window is resized by, so it
    /// is a few pixels larger than what is drawn. For a check that the window comes back where
    /// and as large as it was: compare it before closing and after starting again.
    /// </summary>
    public Rectangle ScreenBounds
    {
        get
        {
            NativeMethods.GetWindowRect(Handle, out var window);
            return new Rectangle(window.Left, window.Top, window.Width, window.Height);
        }
    }

    /// <summary>Whether the window is maximised.</summary>
    public bool IsMaximized => NativeMethods.IsZoomed(Handle);

    /// <summary>Whether the app is still running.</summary>
    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Starts the built app on a data folder after writing its session file, so that the app
    /// starts with what <paramref name="session"/> holds: open tabs, recent repositories, the
    /// window's placement and the panel widths (D46). This is how a check opens repositories.
    /// </summary>
    public static RealApp Launch(string dataDirectory, SessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        JsonSessionStore.Write(new AppPaths(dataDirectory).SessionFile, session);
        return Launch(dataDirectory);
    }

    /// <summary>
    /// The app's log files in a data folder, oldest first: <c>logs/visualcommit-yyyyMMdd.log</c>,
    /// one per day. A run's data folder is new, so they hold that run's log and nothing older.
    /// </summary>
    public static IReadOnlyList<string> LogFiles(string dataDirectory)
    {
        var folder = new AppPaths(dataDirectory).LogDirectory;
        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, FileAppLog.FilePrefix + "*.log").Order(StringComparer.Ordinal).ToList()
            : [];
    }

    /// <summary>
    /// The text of the app's log in a data folder, all files in order: what a check reads to
    /// find the measurements the app logs (D50). It can be read while the app is still writing.
    /// </summary>
    public static string ReadLog(string dataDirectory)
    {
        var text = new System.Text.StringBuilder();
        foreach (var file in LogFiles(dataDirectory))
        {
            // The app keeps no file open between entries, and lets others read while it appends.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text.Append(reader.ReadToEnd());
        }

        return text.ToString();
    }

    /// <summary>Starts the built app on a data folder and waits for its main window.</summary>
    public static RealApp Launch(string dataDirectory)
    {
        if (!File.Exists(Executable))
        {
            throw new FileNotFoundException(
                "The app has not been built. Build it in the configuration the tests run in, for example: dotnet build -c Release",
                Executable);
        }

        // The app gets output pipes of its own. If it inherited the test host's, a leftover app
        // window would keep "dotnet test" waiting for ever.
        var startInfo = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment[AppPaths.DataDirectoryVariable] = dataDirectory;

        // The app's git calls must not depend on this machine's git configuration, as in the
        // scripted walk-through (HeadlessTestApp.IsolateFromTheMachine).
        var emptyGitConfig = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "isolation", "gitconfig");
        Directory.CreateDirectory(Path.GetDirectoryName(emptyGitConfig)!);
        if (!File.Exists(emptyGitConfig))
        {
            File.WriteAllText(emptyGitConfig, string.Empty);
        }

        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = emptyGitConfig;

        // Dates in UTC, as in the scripted walk-through, whatever this machine's time zone (D43).
        startInfo.Environment[DateDisplay.TimeZoneVariable] = "UTC";

        // Nothing a surrounding process can hand git through its environment, as for the
        // scripted walk-through's app.
        foreach (var variable in GitIsolation.InheritedVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The app did not start.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var automation = new UIA3Automation();
        try
        {
            // Attached by id: FlaUI then has a process object of its own. Given ours, it
            // disposes it, and this class could no longer wait for the app or stop it.
            var application = FlaUI.Core.Application.Attach(process.Id);
            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(30))
                ?? throw new TimeoutException("The app's main window did not appear within 30 seconds.");

            var app = new RealApp(process, automation, application, window);

            // On top of everything else, so that the screen captures show the app and the mouse
            // reaches it, whatever other windows are open.
            NativeMethods.SetWindowPos(
                app.Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);
            WaitUntilIdle();
            return app;
        }
        catch
        {
            automation.Dispose();
            Stop(process);
            process.Dispose();
            throw;
        }
    }

    /// <summary>Whether a client area of this size, plus the window frame, fits the work area of the screen.</summary>
    public bool Fits(int width, int height)
    {
        var (frameWidth, frameHeight) = FrameSize();
        var work = WorkArea();
        return (width * Scaling) + frameWidth <= work.Width && (height * Scaling) + frameHeight <= work.Height;
    }

    /// <summary>Resizes the window so that its client area has this size, and moves it to the top-left of the work area.</summary>
    public void SetClientSize(int width, int height)
    {
        if (!Fits(width, height))
        {
            throw new InvalidOperationException($"A {width}x{height} window does not fit this screen at {Scaling:P0} scaling.");
        }

        var (frameWidth, frameHeight) = FrameSize();
        var work = WorkArea();
        NativeMethods.SetWindowPos(
            Handle, IntPtr.Zero, work.Left, work.Top,
            (int)Math.Round(width * Scaling) + frameWidth,
            (int)Math.Round(height * Scaling) + frameHeight,
            NativeMethods.SwpNoZOrder);
        WaitUntilIdle();

        var actual = ClientSize;
        if (Math.Abs(actual.Width - width) > 1 || Math.Abs(actual.Height - height) > 1)
        {
            throw new InvalidOperationException($"The client area is {actual.Width}x{actual.Height}, not the requested {width}x{height}.");
        }
    }

    /// <summary>
    /// Moves the window so that its top-left corner is at this screen position, keeping its
    /// size. The position is the one <see cref="ScreenBounds"/> reports.
    /// </summary>
    public void Move(int screenX, int screenY)
    {
        NativeMethods.SetWindowPos(
            Handle, IntPtr.Zero, screenX, screenY, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder);
        WaitUntilIdle();
    }

    /// <summary>Finds an element by its automation id. Throws when it is not in the window.</summary>
    public AutomationElement Find(string automationId) =>
        Window.FindFirstDescendant(condition => condition.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"No element with the automation id '{automationId}' is in the window.");

    /// <summary>Finds every element with this automation id, in the order UI Automation lists them. Empty when there is none.</summary>
    public IReadOnlyList<AutomationElement> FindAll(string automationId) =>
        Window.FindAllDescendants(condition => condition.ByAutomationId(automationId));

    /// <summary>
    /// Waits until <paramref name="condition"/> is true, looking every 100 ms: for work that the
    /// app finishes after the input that started it, such as loading a repository.
    /// </summary>
    /// <param name="condition">What to wait for; it may ask UI Automation or read the log.</param>
    /// <param name="what">What is awaited, in words, for the message when it does not come.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <exception cref="TimeoutException">The condition was still false when the time was up.</exception>
    /// <exception cref="InvalidOperationException">The app exited while the test waited.</exception>
    public void WaitFor(Func<bool> condition, string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(what);

        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"The app exited with code {_process.ExitCode} while waiting for {what}.");
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Timed out after {timeout.TotalSeconds:0.###} s waiting for {what}."));
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>The names of all text elements in the window.</summary>
    public IReadOnlyList<string> Texts() =>
        Window.FindAllDescendants(condition => condition.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
            .Select(element => element.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

    /// <summary>Where an element is in the client area.</summary>
    public RectangleF BoundsOf(AutomationElement element)
    {
        var origin = ClientOriginOnScreen();
        var bounds = element.BoundingRectangle;
        return new RectangleF(
            (float)((bounds.X - origin.X) / Scaling),
            (float)((bounds.Y - origin.Y) / Scaling),
            (float)(bounds.Width / Scaling),
            (float)(bounds.Height / Scaling));
    }

    /// <summary>Moves the real mouse to the centre of an element and clicks the left button.</summary>
    public void ClickWithMouse(AutomationElement element)
    {
        Mouse.Click(CentreOnScreen(element));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to the centre of an element and clicks the right button.</summary>
    public void RightClickWithMouse(AutomationElement element)
    {
        Mouse.RightClick(CentreOnScreen(element));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to the centre of an element and double-clicks the left button.</summary>
    public void DoubleClickWithMouse(AutomationElement element)
    {
        Mouse.DoubleClick(CentreOnScreen(element));
        WaitUntilIdle();
    }

    /// <summary>
    /// Moves the real mouse to a point of the client area and clicks the left button: for what
    /// UI Automation cannot find on its own, such as a row of the commit graph (D49).
    /// </summary>
    public void ClickAt(float x, float y)
    {
        Mouse.Click(OnScreen(x, y));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to a point of the client area and double-clicks the left button.</summary>
    public void DoubleClickAt(float x, float y)
    {
        Mouse.DoubleClick(OnScreen(x, y));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to a point of the client area and clicks the right button.</summary>
    public void RightClickAt(float x, float y)
    {
        Mouse.RightClick(OnScreen(x, y));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to a point of the client area where nothing reacts to it.</summary>
    public void MoveMouseTo(float x, float y)
    {
        Mouse.MoveTo(OnScreen(x, y));
        WaitUntilIdle();
    }

    /// <summary>
    /// Presses the left button at one point of the client area, moves to another in
    /// <paramref name="steps"/> steps and releases it there: a drag, such as of a splitter.
    /// The button is released even when a move fails, so it is never left held down.
    /// </summary>
    public void DragWithMouse(float fromX, float fromY, float toX, float toY, int steps = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        var from = OnScreen(fromX, fromY);
        var to = OnScreen(toX, toY);

        Mouse.MoveTo(from);
        Mouse.Down(MouseButton.Left);
        try
        {
            for (var step = 1; step <= steps; step++)
            {
                Thread.Sleep(DragStepInterval);
                Mouse.MoveTo(new Point(
                    from.X + ((to.X - from.X) * step / steps),
                    from.Y + ((to.Y - from.Y) * step / steps)));
            }
        }
        finally
        {
            Mouse.Up(MouseButton.Left);
        }

        WaitUntilIdle();
    }

    /// <summary>
    /// Moves the real mouse to a point of the client area and turns the wheel by
    /// <paramref name="notches"/>: positive scrolls up, negative down, as FlaUI's
    /// <see cref="Mouse.Scroll"/>. Each notch is its own wheel event, a moment after the last,
    /// as from a real wheel.
    /// </summary>
    public void ScrollWheel(float x, float y, int notches)
    {
        Mouse.MoveTo(OnScreen(x, y));
        var direction = Math.Sign(notches);
        for (var notch = 0; notch < Math.Abs(notches); notch++)
        {
            if (notch > 0)
            {
                Thread.Sleep(WheelNotchInterval);
            }

            Mouse.Scroll(direction);
        }

        WaitUntilIdle();
    }

    /// <summary>Types text into whatever has the keyboard focus, character by character, as the keyboard would.</summary>
    public void TypeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Keyboard.Type(text);
        WaitUntilIdle();
    }

    /// <summary>Presses and releases one key, such as <see cref="VirtualKeyShort.DOWN"/> or <see cref="VirtualKeyShort.RETURN"/>.</summary>
    public void PressKey(VirtualKeyShort key)
    {
        Keyboard.Type(key);
        WaitUntilIdle();
    }

    /// <summary>
    /// Presses a combination: the last key is pressed and released while the ones before it
    /// are held, for example <c>PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F)</c>
    /// for Ctrl+F. The held keys are released even when the press fails.
    /// </summary>
    public void PressKeys(params VirtualKeyShort[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentOutOfRangeException.ThrowIfLessThan(keys.Length, 1);

        using (Keyboard.Pressing(keys[..^1]))
        {
            Keyboard.Type(keys[^1]);
        }

        WaitUntilIdle();
    }

    /// <summary>Captures the client area from the screen, in real pixels.</summary>
    public Bitmap CaptureClient()
    {
        WaitUntilIdle();
        NativeMethods.GetClientRect(Handle, out var client);
        var origin = ClientOriginOnScreen();
        using var capture = Capture.Rectangle(new Rectangle(origin.X, origin.Y, client.Width, client.Height));
        return new Bitmap(capture.Bitmap);
    }

    /// <summary>Captures the whole window with its frame from the screen.</summary>
    public Bitmap CaptureWindow()
    {
        WaitUntilIdle();
        NativeMethods.GetWindowRect(Handle, out var window);
        using var capture = Capture.Rectangle(new Rectangle(window.Left, window.Top, window.Width, window.Height));
        return new Bitmap(capture.Bitmap);
    }

    /// <summary>Closes the window as its close button would and waits for the app to exit. Returns the exit code.</summary>
    public int Close()
    {
        Window.Close();
        if (!_process.WaitForExit(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("The app did not exit within 15 seconds of closing its window.");
        }

        return _process.ExitCode;
    }

    /// <summary>Makes sure the app is gone, however the test ended, and releases UI Automation.</summary>
    public void Dispose()
    {
        Stop(_process);
        _application.Dispose();
        _automation.Dispose();
        _process.Dispose();
    }

    /// <summary>
    /// Stops the app if it is still running. A test that fails half-way must not leave a
    /// window on the user's desktop, so every failure to stop it is kept in <see cref="Problems"/>.
    /// </summary>
    private static void Stop(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill();
            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                Problems.Enqueue($"The app (process {process.Id}) was still running 10 seconds after it was stopped.");
            }
        }
        catch (Exception ex)
        {
            Problems.Enqueue($"Stopping the app failed: {ex}");
        }
    }

    /// <summary>Gives the app time to handle input and paint: UI Automation's input queue, then a short pause for the frame.</summary>
    private static void WaitUntilIdle()
    {
        Wait.UntilInputIsProcessed();
        Thread.Sleep(400);
    }

    private Point ClientOriginOnScreen()
    {
        var origin = default(NativeMethods.Point);
        NativeMethods.ClientToScreen(Handle, ref origin);
        return new Point(origin.X, origin.Y);
    }

    /// <summary>The screen pixel of a client-area point given in logical pixels.</summary>
    private Point OnScreen(float x, float y)
    {
        var origin = ClientOriginOnScreen();
        return new Point(origin.X + (int)(x * Scaling), origin.Y + (int)(y * Scaling));
    }

    /// <summary>The screen pixel at the centre of an element.</summary>
    private static Point CentreOnScreen(AutomationElement element)
    {
        var bounds = element.BoundingRectangle;
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }

    private (int Width, int Height) FrameSize()
    {
        NativeMethods.GetWindowRect(Handle, out var window);
        NativeMethods.GetClientRect(Handle, out var client);
        return (window.Width - client.Width, window.Height - client.Height);
    }

    private NativeMethods.Rect WorkArea()
    {
        var info = new NativeMethods.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        NativeMethods.GetMonitorInfoW(NativeMethods.MonitorFromWindow(Handle, NativeMethods.MonitorDefaultToNearest), ref info);
        return info.Work;
    }
}
