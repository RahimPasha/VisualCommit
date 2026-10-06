using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// One run of the built app in a real window on the Windows desktop. It is found and inspected
/// through UI Automation, clicked with the real mouse, and captured from the screen.
/// All positions taken or returned by this class are in logical pixels of the window's client
/// area, the same units as the scripted walk-through, unless a name says "screen".
/// </summary>
public sealed class RealApp : IDisposable
{
    private static readonly string Executable =
        typeof(RealApp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "AppExecutable").Value!;

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
        startInfo.Environment["VISUALCOMMIT_DATA_DIR"] = dataDirectory;
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

    /// <summary>Finds an element by its automation id. Throws when it is not in the window.</summary>
    public AutomationElement Find(string automationId) =>
        Window.FindFirstDescendant(condition => condition.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"No element with the automation id '{automationId}' is in the window.");

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
        var bounds = element.BoundingRectangle;
        Mouse.Click(new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));
        WaitUntilIdle();
    }

    /// <summary>Moves the real mouse to a point of the client area where nothing reacts to it.</summary>
    public void MoveMouseTo(float x, float y)
    {
        var origin = ClientOriginOnScreen();
        Mouse.MoveTo(new Point(origin.X + (int)(x * Scaling), origin.Y + (int)(y * Scaling)));
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
