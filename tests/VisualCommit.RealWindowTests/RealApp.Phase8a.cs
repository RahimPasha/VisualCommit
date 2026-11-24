using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.EventHandlers;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using VisualCommit.App.Services;
using VisualCommit.Git;
using VisualCommit.Testing;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The real-window driver's additions for Phase8aRealWindowPass's launches (docs/phase-8a-design.md
/// 12.2, 12.3): the built executable's path and its version; the stand-in for git in
/// this project's output and the variables that point the app at it; a second start beside a running
/// app; the window's state on the desktop (minimised, visible, the foreground window) read and set
/// through Win32; the window's icons and a capture of any part of the screen, the taskbar's button
/// among them; another top-level window of the app's process; and the graph row's events recorded
/// for a screen reader's route. Every wait is on a condition with a limit, never on a fixed time.
/// Nothing here changes a setting of the desktop (D224).
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The automation id of the notice a start shows when the app on its data folder does not answer.</summary>
    public const string InstanceNoticeId = "InstanceNotice";

    /// <summary>The built app's executable, the file every launch starts (its <c>AppExecutable</c> metadata).</summary>
    public static string ExecutablePath => Executable;

    /// <summary>
    /// The stand-in for git, <c>visualcommit-git-stand-in.exe</c> (<see cref="GitStandIn.FileName"/>),
    /// in this project's output folder, where the project reference to it puts its app host.
    /// </summary>
    /// <exception cref="FileNotFoundException">It is not there.</exception>
    public static string StandInPath
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, GitStandIn.FileName);
            return File.Exists(path)
                ? path
                : throw new FileNotFoundException($"The stand-in for git is missing at {path}: build tests/VisualCommit.RealWindowTests, which references it.", path);
        }
    }

    /// <summary>
    /// The variables that point a launch's app at a stand-in for git, given to
    /// <see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/>, which sets them last:
    /// <c>VISUALCOMMIT_GIT_STAND_IN</c> as <paramref name="standIn"/> (a full path, or <c>none</c> for no
    /// git), and the stand-in's own <c>VISUALCOMMIT_GIT_STAND_IN_VERSION</c> (its <c>--version</c>
    /// line) and <c>VISUALCOMMIT_GIT_STAND_IN_FAILURE</c> (a text written to standard error with exit
    /// 128), each removed when null.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> StandInEnvironment(string standIn, string? version = null, string? failure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(standIn);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [GitLocator.StandInVariable] = standIn,
            [GitStandIn.VersionVariable] = version,
            [GitStandIn.FailureVariable] = failure,
        };
    }

    /// <summary>
    /// Starts the built app a second time on <paramref name="dataDirectory"/>, beside a launch that runs
    /// on it, with <see cref="StartInfoFor"/>'s environment and <paramref name="environment"/>'s
    /// variables, its output drained. No window is looked for: the caller waits for its exit
    /// (<see cref="BesideStart.WaitForExit"/>), and disposing it stops it if it still runs.
    /// </summary>
    public static BesideStart StartBeside(string dataDirectory, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        if (!File.Exists(Executable))
        {
            throw new FileNotFoundException(
                "The app has not been built. Build it in the configuration the tests run in, for example: dotnet build -c Release",
                Executable);
        }

        var process = Process.Start(StartInfoFor(dataDirectory, environment)) ?? throw new InvalidOperationException("The second start did not start.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new BesideStart(process);
    }

    /// <summary>Minimises the window (<c>ShowWindow</c> with <c>SW_MINIMIZE</c>) and waits until it is iconic.</summary>
    public void Minimise()
    {
        Win32.ShowWindow(Handle, Win32.SwMinimize);
        WaitFor(() => IsIconic, "the window to be minimised", DefaultWaitTimeout);
    }

    /// <summary>Whether the window is minimised (<c>IsIconic</c>).</summary>
    public bool IsIconic => Win32.IsIconic(Handle);

    /// <summary>Whether the window shows on the desktop: visible (<c>IsWindowVisible</c>) and not minimised.</summary>
    public bool IsVisibleOnDesktop => Win32.IsWindowVisible(Handle) && !Win32.IsIconic(Handle);

    /// <summary>
    /// Whether this window is the desktop's foreground window (<c>GetForegroundWindow</c>), the window
    /// the real keyboard's keys reach: a key is sent only while this holds.
    /// </summary>
    public bool ForegroundIsThis => Win32.GetForegroundWindow() == Handle;

    /// <summary>
    /// The element UI Automation reports as having the keyboard focus, or null when there is none or it
    /// cannot be read: what a real Enter would press, and where a text's focus lies when its own element
    /// has no automation id.
    /// </summary>
    public AutomationElement? FocusedElement()
    {
        try
        {
            return _automation.FocusedElement();
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            return null;
        }
    }

    /// <summary>
    /// The window's two icons as Windows holds them (<c>WM_GETICON</c> with <c>ICON_SMALL</c> and
    /// <c>ICON_BIG</c>, each null when the window gives none), with the window's DPI and the sizes
    /// Windows draws them at for it (<c>GetSystemMetricsForDpi</c> of <c>SM_CXSMICON</c> and
    /// <c>SM_CXICON</c>). The caller disposes the result.
    /// </summary>
    public WindowIconSet WindowIcons()
    {
        var small = Win32.SendMessageW(Handle, Win32.WmGetIcon, Win32.IconSmall, IntPtr.Zero);
        var big = Win32.SendMessageW(Handle, Win32.WmGetIcon, Win32.IconBig, IntPtr.Zero);
        var dpi = NativeMethods.GetDpiForWindow(Handle);
        return new WindowIconSet(
            BitmapOfIcon(small),
            BitmapOfIcon(big),
            dpi,
            Win32.GetSystemMetricsForDpi(Win32.SmCxSmIcon, dpi),
            Win32.GetSystemMetricsForDpi(Win32.SmCxIcon, dpi));
    }

    /// <summary>
    /// Captures a rectangle of the screen, in real pixels, whatever lies there: for what lies outside the
    /// window, such as the taskbar's button. Waits for the app to be idle first, as the other captures do.
    /// </summary>
    public static Bitmap CaptureScreen(Rectangle screenBounds)
    {
        WaitUntilIdle();
        using var capture = Capture.Rectangle(screenBounds);
        return new Bitmap(capture.Bitmap);
    }

    /// <summary>
    /// The taskbar's button whose UI Automation name starts with <paramref name="nameStart"/>, looked for
    /// from UI Automation's desktop root (the taskbars, <c>Shell_TrayWnd</c> and on another monitor
    /// <c>Shell_SecondaryTrayWnd</c>, and their buttons) every 100 ms for at most
    /// <paramref name="limit"/>: its name and its bounds on the screen, or null when none came.
    /// </summary>
    public TaskbarEntry? TaskbarButton(string nameStart, TimeSpan limit)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameStart);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (FindTaskbarButton(nameStart) is { } found)
            {
                return found;
            }

            if (clock.Elapsed >= limit)
            {
                return null;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>
    /// A top-level window of the app's process other than the notice (<see cref="InstanceNoticeId"/>), on
    /// the screen: the main window that "Try again" opens in the process that showed the notice. Null
    /// while there is none. One look; the caller waits for it.
    /// </summary>
    public Window? OtherWindowOfProcess()
    {
        try
        {
            return _automation.GetDesktop()
                .FindAllChildren(condition => condition.ByProcessId(_process.Id))
                .Where(element => element.Properties.AutomationId.ValueOrDefault != InstanceNoticeId
                    && !element.IsOffscreen
                    && element.BoundingRectangle is { Width: > 0, Height: > 0 })
                .Select(element => element.AsWindow())
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            return null;
        }
    }

    /// <summary>
    /// Clicks the window's title bar once with the real mouse, to make it the foreground window: in its
    /// middle, away from its left end, where the icon opens the system menu (a double click there closes
    /// the window), and from the caption buttons at its right end. Its non-client area gives no control
    /// the focus.
    /// </summary>
    public void ClickTitleBar()
    {
        NativeMethods.GetWindowRect(Handle, out var window);
        var client = ClientOriginOnScreen();
        Mouse.Click(new Point(window.Left + (window.Width / 2), (window.Top + client.Y) / 2));
        WaitUntilIdle();
    }

    /// <summary>
    /// The built app's product version as Windows reads it from <c>VisualCommit.dll</c>'s version
    /// resource (<see cref="FileVersionInfo.ProductVersion"/>), which the build fills with the
    /// assembly's informational version: <c>0.1.0+&lt;commit&gt;</c> when the build knew its commit.
    /// </summary>
    public static string BuiltProductVersion() => FileVersionInfo.GetVersionInfo(BuiltAssemblyPath).ProductVersion ?? string.Empty;

    /// <summary>
    /// The version the built app shows in About: <see cref="AppVersionInfo.Parse"/> of
    /// <see cref="BuiltProductVersion"/>, such as <c>0.1.0 (3dc0887)</c>, or <c>0.1.0</c> when the build
    /// carries no commit.
    /// </summary>
    public static string BuiltVersionDisplay() => AppVersionInfo.Parse(BuiltProductVersion()).Display;

    /// <summary>
    /// Starts recording the events a screen reader hears from the graph's row: UI Automation's
    /// <c>Name</c> property changes and <c>LiveRegionChanged</c> events on <c>GraphRows</c>' subtree,
    /// each queued with its kind, its sender and its time and nothing else done in the handler (no UI
    /// Automation call there). The senders' ids are read when <see cref="SelectedRowEvents.Events"/>
    /// is asked, from the cache the handlers were registered with. Read the graph's first child
    /// before (D455): cold, a move raises no event from the row. Dispose it to stop recording.
    /// </summary>
    public SelectedRowEvents RecordSelectedRowEvents() => new(_automation, Find("GraphRows"));

    /// <summary><c>VisualCommit.dll</c> beside the executable: the app's own assembly, whose version resource holds the product version.</summary>
    private static string BuiltAssemblyPath => Path.Combine(Path.GetDirectoryName(Executable)!, "VisualCommit.dll");

    /// <summary>A copy of an icon's image with its alpha, or null for no icon. The handle stays the window's: it is not destroyed.</summary>
    private static Bitmap? BitmapOfIcon(IntPtr icon)
    {
        if (icon == IntPtr.Zero)
        {
            return null;
        }

        using var managed = Icon.FromHandle(icon);
        return managed.ToBitmap();
    }

    /// <summary>One look for <see cref="TaskbarButton"/>: the first button on a taskbar whose name starts with <paramref name="nameStart"/>, with non-empty bounds.</summary>
    private TaskbarEntry? FindTaskbarButton(string nameStart)
    {
        try
        {
            var bars = _automation.GetDesktop()
                .FindAllChildren(condition => condition.ByClassName("Shell_TrayWnd").Or(condition.ByClassName("Shell_SecondaryTrayWnd")));
            foreach (var bar in bars)
            {
                foreach (var button in bar.FindAllDescendants(condition => condition.ByControlType(ControlType.Button)))
                {
                    var name = button.Properties.Name.ValueOrDefault;
                    if (name is not null && name.StartsWith(nameStart, StringComparison.Ordinal) && button.BoundingRectangle is { Width: > 0, Height: > 0 } bounds)
                    {
                        return new TaskbarEntry(name, bounds);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            // The taskbar redrew while it was walked: the next look reads it again.
        }

        return null;
    }

    /// <summary>A button of the taskbar: its UI Automation name and its bounds on the screen, in real pixels.</summary>
    public sealed record TaskbarEntry(string Name, Rectangle ScreenBounds);

    /// <summary>
    /// The window's icons (<see cref="WindowIcons"/>): <c>ICON_SMALL</c>'s and <c>ICON_BIG</c>'s images
    /// (null where the window gives none), the window's DPI, and <c>SM_CXSMICON</c> and <c>SM_CXICON</c>
    /// at that DPI, the sizes Windows draws the small icon (the title bar's) and the big one at.
    /// </summary>
    public sealed record WindowIconSet(Bitmap? Small, Bitmap? Big, uint Dpi, int SmallIconMetric, int IconMetric) : IDisposable
    {
        public void Dispose()
        {
            Small?.Dispose();
            Big?.Dispose();
        }
    }

    /// <summary>
    /// A second start of the app beside a launch (<see cref="StartBeside"/>): its process, waited for
    /// until it exits. Disposing it stops it if it still runs, so that a failed check leaves no process.
    /// </summary>
    public sealed class BesideStart : IDisposable
    {
        private readonly Process _process;

        internal BesideStart(Process process) => _process = process;

        /// <summary>The second start's process id, which the first instance's log names.</summary>
        public int Id => _process.Id;

        /// <summary>Whether it has exited.</summary>
        public bool HasExited => _process.HasExited;

        /// <summary>Waits until it exits, at most <paramref name="timeout"/>, and returns its exit code.</summary>
        /// <exception cref="TimeoutException">It still ran when the time was up.</exception>
        public int WaitForExit(TimeSpan timeout)
        {
            if (!_process.WaitForExit(timeout))
            {
                throw new TimeoutException(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"The second start (process {_process.Id}) did not exit within {timeout.TotalSeconds:0.###} s."));
            }

            return _process.ExitCode;
        }

        public void Dispose()
        {
            Stop(_process);
            _process.Dispose();
        }
    }

    /// <summary>One event from <c>GraphRows</c>' subtree: its kind (<see cref="SelectedRowEvents.NameChanged"/> or <see cref="SelectedRowEvents.LiveRegionChanged"/>), its sender's automation id (null where it could not be read), and when it came, from the recording's start.</summary>
    public sealed record RowEvent(string Kind, string? AutomationId, TimeSpan At);

    /// <summary>
    /// The graph row's events while recording (<see cref="RecordSelectedRowEvents"/>). The handlers
    /// queue the kind, the sender and the time only; <see cref="Events"/> reads each sender's id
    /// afterwards, from the cache that came with the event, else by asking UI Automation, outside any
    /// handler. Disposing it removes both handlers.
    /// </summary>
    public sealed class SelectedRowEvents : IDisposable
    {
        /// <summary>The kind of a <c>Name</c> property change.</summary>
        public const string NameChanged = "Name";

        /// <summary>The kind of a <c>LiveRegionChanged</c> event.</summary>
        public const string LiveRegionChanged = "LiveRegionChanged";

        private readonly ConcurrentQueue<(string Kind, AutomationElement Sender, TimeSpan At)> _queue = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly CacheRequest _cache;
        private readonly PropertyChangedEventHandlerBase _names;
        private readonly AutomationEventHandlerBase _live;

        internal SelectedRowEvents(UIA3Automation automation, AutomationElement rows)
        {
            _cache = new CacheRequest { TreeScope = TreeScope.Element };
            _cache.Add(automation.PropertyLibrary.Element.AutomationId);
            using (_cache.Activate())
            {
                _names = rows.RegisterPropertyChangedEvent(
                    TreeScope.Subtree,
                    (sender, _, _) => _queue.Enqueue((NameChanged, sender, _clock.Elapsed)),
                    automation.PropertyLibrary.Element.Name);
                try
                {
                    _live = rows.RegisterAutomationEvent(
                        automation.EventLibrary.Element.LiveRegionChangedEvent,
                        TreeScope.Subtree,
                        (sender, _) => _queue.Enqueue((LiveRegionChanged, sender, _clock.Elapsed)));
                }
                catch
                {
                    _names.Dispose();
                    throw;
                }
            }
        }

        /// <summary>How long the recording has run: a mark to compare the events' times with.</summary>
        public TimeSpan Now => _clock.Elapsed;

        /// <summary>Every event so far, in the order they came, each sender's id read now, outside the handlers.</summary>
        public IReadOnlyList<RowEvent> Events() => [.. _queue.Select(entry => new RowEvent(entry.Kind, IdOf(entry.Sender), entry.At))];

        public void Dispose()
        {
            _live.Dispose();
            _names.Dispose();
        }

        /// <summary>A sender's automation id: from the event's cache, else from UI Automation; null when neither answers.</summary>
        private string? IdOf(AutomationElement sender)
        {
            try
            {
                using (_cache.Activate())
                {
                    return sender.Properties.AutomationId.ValueOrDefault;
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or FlaUI.Core.Exceptions.FlaUIException)
            {
                // Not in the cache: asked outside the handler.
            }

            try
            {
                return sender.Properties.AutomationId.ValueOrDefault;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or FlaUI.Core.Exceptions.FlaUIException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The Win32 calls of these launches beside the shared <see cref="NativeMethods"/>: the window's
    /// state on the desktop and its icons. Each only reads, but for <c>ShowWindow</c>, which minimises
    /// the app's own window.
    /// </summary>
    private static class Win32
    {
        public const int SwMinimize = 6;
        public const uint WmGetIcon = 0x007F;
        public const int SmCxIcon = 11;
        public const int SmCxSmIcon = 49;

        public static readonly IntPtr IconSmall = new(0);
        public static readonly IntPtr IconBig = new(1);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetricsForDpi(int index, uint dpi);
    }
}
