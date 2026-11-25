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
using FlaUI.Core.WindowsAPI;
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
/// among them; another top-level window of the app's process; the graph row's events recorded for a
/// screen reader's route, and reads through UI Automation that never throw (<see cref="ReadOr{T}"/>);
/// Windows' Open dialog confirmed only while it is the foreground window.
/// Every wait is on a condition with a limit, never on a fixed time, and no call into the app's process
/// waits without one. Nothing here changes a setting of the desktop (D224).
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The automation id of the notice a start shows when the app on its data folder does not answer.</summary>
    public const string InstanceNoticeId = "InstanceNotice";

    /// <summary>
    /// What a read through UI Automation gives in place of a value it could not read (<see cref="ReadOr{T}"/>): the
    /// element went, UI Automation refused or timed out, or a property was asked of a cache that does not hold it.
    /// </summary>
    public const string NotReadable = "(not readable)";

    /// <summary>How long a message sent to the app's window (<c>WM_GETICON</c>) may take to be answered before it counts as unanswered.</summary>
    private const uint MessageAnswerMilliseconds = 5000;

    /// <summary>How long Windows' Open dialog may take to be the foreground window with its file name box focused before its Enter.</summary>
    private static readonly TimeSpan DialogForegroundWithin = TimeSpan.FromSeconds(5);

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

    /// <summary>
    /// Minimises the window and waits, at most <see cref="DefaultWaitTimeout"/>, until it is iconic:
    /// <c>ShowWindowAsync</c> with <c>SW_MINIMIZE</c>, which posts the change to the window's thread, so
    /// that an app that does not answer cannot hold the test.
    /// </summary>
    public void Minimise()
    {
        Win32.ShowWindowAsync(Handle, Win32.SwMinimize);
        WaitFor(() => IsIconic, "the window to be minimised", DefaultWaitTimeout);
    }

    /// <summary>Whether the window is minimised (<c>IsIconic</c>).</summary>
    public bool IsIconic => Win32.IsIconic(Handle);

    /// <summary>Whether the window shows on the desktop: visible (<c>IsWindowVisible</c>) and not minimised.</summary>
    public bool IsVisibleOnDesktop => Win32.IsWindowVisible(Handle) && !Win32.IsIconic(Handle);

    /// <summary>
    /// Whether the window this <see cref="RealApp"/> was made on is gone (<c>IsWindow</c> false: destroyed,
    /// not only hidden), read from Windows alone, so that a step that expects the window to close asks UI
    /// Automation nothing about a window that is going.
    /// </summary>
    public bool IsWindowGone => !Win32.IsWindow(Handle);

    /// <summary>
    /// Whether this window is the desktop's foreground window (<c>GetForegroundWindow</c>), the window
    /// the real keyboard's keys reach: a key is sent only while this holds.
    /// </summary>
    public bool ForegroundIsThis => Win32.GetForegroundWindow() == Handle;

    /// <summary>
    /// The element UI Automation reports as having the keyboard focus, or null when there is none or it
    /// cannot be read (<see cref="IsReadFailure"/>): what a real Enter would press, and where a text's focus
    /// lies when its own element has no automation id.
    /// </summary>
    public AutomationElement? FocusedElement()
    {
        try
        {
            return _automation.FocusedElement();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// The automation id of UI Automation's focused element, or null when there is none or it cannot be
    /// read: as <see cref="FocusedAutomationId"/>, but an element that goes between the look and the read
    /// (FlaUI's <c>ElementNotAvailableException</c>, a focus that moves as a dialog closes or a button
    /// turns disabled), or any other read that fails (<see cref="IsReadFailure"/>), counts as none, so that a
    /// wait on it looks again instead of ending.
    /// </summary>
    public string? FocusedId()
    {
        if (FocusedElement() is not { } focused)
        {
            return null;
        }

        try
        {
            return focused.Properties.AutomationId.ValueOrDefault;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is how a read through UI Automation fails: an
    /// <see cref="ArgumentException"/> (E_INVALIDARG, a cached read of a property the element's cache does not
    /// hold), a <see cref="COMException"/>, a <see cref="TimeoutException"/> (UI Automation's own time-out,
    /// 0x80131505), an <see cref="InvalidOperationException"/>, a FlaUI exception (an element no longer
    /// available, a property or a pattern not supported or not cached), or a
    /// <see cref="System.ComponentModel.Win32Exception"/>. FlaUI 5.0.0's <c>Com.Call</c>, which wraps
    /// <c>UIA3Automation.FromHandle</c> and <c>FocusedElement</c>, catches a <see cref="COMException"/>, turns
    /// 0x80040200 to 0x80040204 into FlaUI's exceptions, 0x80131505 into a <see cref="TimeoutException"/> and
    /// 0x80131509 into an <see cref="InvalidOperationException"/>, and throws every other one again as a
    /// <see cref="System.ComponentModel.Win32Exception"/>, so a <see cref="COMException"/> never leaves those
    /// two calls.
    /// </summary>
    public static bool IsReadFailure(Exception exception) =>
        exception is ArgumentException or COMException or TimeoutException or InvalidOperationException
            or FlaUI.Core.Exceptions.FlaUIException or System.ComponentModel.Win32Exception;

    /// <summary>
    /// A read through UI Automation that never throws: what <paramref name="read"/> gives, or
    /// <paramref name="unreadable"/> when it fails as <see cref="IsReadFailure"/> says. For a read whose failure
    /// should be noted and judged, not end the method before it judges.
    /// </summary>
    public static T ReadOr<T>(Func<T> read, T unreadable)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            return read();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return unreadable;
        }
    }

    /// <summary>Waits until UI Automation's focused element has the automation id <paramref name="automationId"/>, read by <see cref="FocusedId"/>.</summary>
    public void WaitForFocusOn(string automationId, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        WaitForTelling(
            () => FocusedId() == automationId,
            $"UI Automation's focused element to be {automationId}",
            timeout,
            () => $"It is {FocusedId() ?? "none"}.");
    }

    /// <summary>
    /// The window's two icons as Windows holds them (<c>WM_GETICON</c> with <c>ICON_SMALL</c> and
    /// <c>ICON_BIG</c>, each null when the window gives none), with the window's DPI and the sizes
    /// Windows draws them at for it (<c>GetSystemMetricsForDpi</c> of <c>SM_CXSMICON</c> and
    /// <c>SM_CXICON</c>). Each message is sent with <c>SendMessageTimeoutW</c> (<c>SMTO_BLOCK</c>,
    /// <c>SMTO_ABORTIFHUNG</c>, <see cref="MessageAnswerMilliseconds"/>): one the app does not answer in
    /// time counts as no icon, and the result says so. The caller disposes the result.
    /// </summary>
    public WindowIconSet WindowIcons()
    {
        var smallAnswered = AskIcon(Win32.IconSmall, out var small);
        var bigAnswered = AskIcon(Win32.IconBig, out var big);
        var dpi = NativeMethods.GetDpiForWindow(Handle);
        return new WindowIconSet(
            BitmapOfIcon(small),
            BitmapOfIcon(big),
            dpi,
            Win32.GetSystemMetricsForDpi(Win32.SmCxSmIcon, dpi),
            Win32.GetSystemMetricsForDpi(Win32.SmCxIcon, dpi),
            !smallAnswered,
            !bigAnswered);
    }

    /// <summary>
    /// The offset of the window's visible frame inside <see cref="CaptureWindow"/>'s capture, in screen
    /// pixels: DWM's extended frame bounds (<c>DWMWA_EXTENDED_FRAME_BOUNDS</c>) less <c>GetWindowRect</c>'s
    /// corner, which on Windows 10 and 11 lies outside the invisible border the window is resized by. Null
    /// when DWM does not answer or the offset is not one such a border gives (negative, or a quarter of the
    /// window or more); <paramref name="described"/> gives both rectangles for run.txt either way.
    /// </summary>
    public Point? VisibleFrameOffset(out string described)
    {
        NativeMethods.GetWindowRect(Handle, out var window);
        var result = Win32.DwmGetWindowAttribute(Handle, Win32.DwmwaExtendedFrameBounds, out var frame, Marshal.SizeOf<NativeMethods.Rect>());
        if (result != 0)
        {
            described = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"GetWindowRect ({window.Left}, {window.Top}, {window.Right}, {window.Bottom}); DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) failed with 0x{result:X8}");
            return null;
        }

        described = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"GetWindowRect ({window.Left}, {window.Top}, {window.Right}, {window.Bottom}), the visible frame ({frame.Left}, {frame.Top}, {frame.Right}, {frame.Bottom})");
        var offset = new Point(frame.Left - window.Left, frame.Top - window.Top);
        return offset.X < 0 || offset.Y < 0 || offset.X >= window.Width / 4 || offset.Y >= window.Height / 4 ? null : offset;
    }

    /// <summary>
    /// Waits for Windows' Open dialog the app opened with <paramref name="title"/>, puts
    /// <paramref name="file"/> into its file name box (automation id 1148) and confirms it with a real
    /// Enter, as <see cref="ChooseFileInDialog"/> does, but with the guard that no key reaches another
    /// window: the path goes in through the box's Value pattern alone (never typed: a box that gives no
    /// Value pattern fails the step with nothing typed), and the Enter is sent only once the dialog is the
    /// desktop's foreground window and its file name box UI Automation's focused element, waited for at
    /// most <see cref="DialogForegroundWithin"/>; otherwise the step fails with no Enter sent. The dialog
    /// is never clicked to bring it forward: a click could land on whatever covers it. Returns run.txt's
    /// words for the guard.
    /// </summary>
    public string ChooseFileInDialogGuarded(string title, string file, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        Window? dialog = null;
        WaitFor(
            () =>
            {
                dialog = Window.ModalWindows.FirstOrDefault(window => TitleOf(window) == title)
                    ?? TopLevelElementsOf(_process.Id)
                        .Select(element => element.AsWindow())
                        .FirstOrDefault(window => TitleOf(window) == title);
                return dialog is not null;
            },
            $"the file dialog \"{title}\"",
            timeout);

        var handle = dialog!.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The file dialog \"{title}\" gives no window handle: no key was sent.");
        }

        // The box is an edit inside the file name combo box (1148), or the combo box itself.
        var name = DialogControl(dialog, "1148", "the file dialog's file name box", timeout);
        var box = name.FindFirstDescendant(condition => condition.ByControlType(ControlType.Edit)) ?? name;
        if (!box.Patterns.Value.IsSupported)
        {
            throw new InvalidOperationException("The file dialog's file name box gives no Value pattern: the path is not typed and no Enter is sent.");
        }

        box.Patterns.Value.Pattern.SetValue(file);
        WaitFor(() => box.Patterns.Value.Pattern.Value.ValueOrDefault == file, "the file dialog's file name box to hold the path", timeout);

        // Enter in the box confirms the whole path, as a user does (ChooseFileInDialog says why not the
        // Open button). Sent only while the dialog is in front with the box focused.
        box.Focus();
        var clock = Stopwatch.StartNew();
        WaitFor(
            () => Win32.GetForegroundWindow() == handle && FocusedElement() is { } focused && (focused.Equals(box) || focused.Equals(name)),
            $"the file dialog \"{title}\" to be the foreground window with its file name box focused; no Enter was sent, as it would reach another window",
            DialogForegroundWithin);
        var guard = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"the path set through the file name box's Value pattern; the Enter sent once the dialog was the foreground window with that box focused, {clock.Elapsed.TotalSeconds:0.0} s after the box was given the focus");
        PressKey(VirtualKeyShort.RETURN);
        WaitFor(() => !Window.ModalWindows.Any(window => TitleOf(window) == title), "the file dialog to close", timeout);
        WaitUntilIdle();
        return guard;
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
    /// The taskbar's buttons whose UI Automation names start with <paramref name="nameStart"/>, looked for
    /// (<see cref="FindTaskbarButtons"/>: the taskbars, <c>Shell_TrayWnd</c> and on another monitor
    /// <c>Shell_SecondaryTrayWnd</c>, found in Windows' list of windows by their classes, each asked of UI
    /// Automation by its handle, and their buttons searched through UI Automation) every 100 ms for at most
    /// <paramref name="limit"/>, until a look finds one on <paramref name="display"/>
    /// (<see cref="TaskbarEntry.LiesOn"/>): every such button of that look, on every taskbar, with its name,
    /// its bounds on the screen and its taskbar; at the limit, the last look's, which are then on other
    /// displays, or none. A button does not say whose process it is: Windows groups the buttons by the
    /// executable's path, so each other running VisualCommit has a button of its own, with the same name,
    /// and with more than one display one app can have a button on each display's taskbar.
    /// </summary>
    public IReadOnlyList<TaskbarEntry> TaskbarButtons(string nameStart, TimeSpan limit, Rectangle display)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameStart);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var found = FindTaskbarButtons(nameStart);
            if (found.Exists(entry => entry.LiesOn(display)) || clock.Elapsed >= limit)
            {
                return found;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>
    /// The display that holds the window, in real pixels of the virtual screen, its taskbar included:
    /// <c>MonitorFromWindow</c>'s display (the nearest when the window lies on none) and
    /// <c>GetMonitorInfo</c>'s <c>rcMonitor</c> of it.
    /// </summary>
    public Rectangle DisplayBounds
    {
        get
        {
            var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (!NativeMethods.GetMonitorInfoW(NativeMethods.MonitorFromWindow(Handle, NativeMethods.MonitorDefaultToNearest), ref info))
            {
                throw new InvalidOperationException("Windows gave no display for the app's window (MonitorFromWindow, GetMonitorInfo).");
            }

            return Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom);
        }
    }

    /// <summary>
    /// A top-level window of the app's process other than the notice (<see cref="InstanceNoticeId"/>), on
    /// the screen, that <paramref name="accept"/> takes: the main window that "Try again" opens in the
    /// process that showed the notice. Each such window is offered in the order <c>EnumWindows</c> gives,
    /// until one is taken, so that a popup of the app above the main window does not hide it. Null while
    /// none is taken, and null when a read through UI Automation fails (<see cref="IsReadFailure"/>). One
    /// look; the caller waits for it and looks again. Found by their handles (<see cref="TopLevelElementsOf"/>):
    /// a failed enumeration of Windows' list leaves the call.
    /// </summary>
    public Window? OtherWindowOfProcess(Func<Window, bool> accept)
    {
        ArgumentNullException.ThrowIfNull(accept);
        var elements = TopLevelElementsOf(_process.Id);
        try
        {
            return elements
                .Where(element => element.Properties.AutomationId.ValueOrDefault != InstanceNoticeId
                    && !element.IsOffscreen
                    && element.BoundingRectangle is { Width: > 0, Height: > 0 })
                .Select(element => element.AsWindow())
                .FirstOrDefault(accept);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// Clicks the window's title bar once with the real mouse, to make it the foreground window: in its
    /// middle, away from its left end, where the icon opens the system menu (a double click there closes
    /// the window), and from the caption buttons at its right end. Its non-client area gives no control
    /// the focus. The click is made only when the window under that point (<c>WindowFromPoint</c>, its
    /// root by <c>GetAncestor</c>) is this one; when another window lies over it, such as one kept on top
    /// of every other, nothing is clicked and <see cref="InvalidOperationException"/> names that window.
    /// </summary>
    public void ClickTitleBar()
    {
        var point = TitleBarMiddle();
        var under = Win32.GetAncestor(Win32.WindowFromPoint(new NativeMethods.Point { X = point.X, Y = point.Y }), Win32.GaRoot);
        if (under != Handle)
        {
            throw new InvalidOperationException(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"The title bar's middle at ({point.X}, {point.Y}) lies under another window ({WindowWords(under)}), not this one: nothing was clicked."));
        }

        Mouse.Click(point);
        WaitUntilIdle();
    }

    /// <summary>
    /// Puts the real pointer on the point <see cref="ClickTitleBar"/> clicks, in one jump and with no
    /// click: on the window, off its client area, so that no control under it is hovered and no tooltip
    /// of the client area opens. For a window that is no main window, such as the notice, where the
    /// client area's neutral point lies outside it.
    /// </summary>
    public void SetPointerOnTitleBar()
    {
        Mouse.Position = TitleBarMiddle();
        WaitUntilIdle();
    }

    /// <summary>
    /// The height of the window's top edge with its title bar, in screen pixels: from the window's top
    /// as <c>GetWindowRect</c> gives it to the client area's top (<c>ClientToScreen</c>). A capture
    /// of the window with its frame (<see cref="CaptureWindow"/>) holds the title bar in these rows.
    /// </summary>
    public int TitleBarHeight
    {
        get
        {
            NativeMethods.GetWindowRect(Handle, out var window);
            return ClientOriginOnScreen().Y - window.Top;
        }
    }

    /// <summary>The app's process id, which its <c>instance.json</c> and its log's lines name.</summary>
    public int ProcessId => _process.Id;

    /// <summary>
    /// Whether the process <paramref name="processId"/> has a visible top-level window on the desktop now,
    /// as Windows lists them: one look, for a check that a start showed no window while it ran. When Windows
    /// fails to list its windows, a <see cref="System.ComponentModel.Win32Exception"/>, never false.
    /// </summary>
    public bool HasWindowOfProcess(int processId) => Win32.VisibleTopLevelWindowsOf((uint)processId).Count > 0;

    /// <summary>
    /// The visible top-level windows of a process as UI Automation elements, found in Windows' own list of
    /// windows and asked of UI Automation by their handles, never by a search of the desktop's children.
    /// Such a search makes UI Automation ask every program on the desktop, and one that answers slowly
    /// holds the search, and the harness's wait with it, for seconds: launch 6's wait for the main window
    /// ran out on it in about two runs of five on the owner's desktop, and in none of sixteen by the handle.
    /// In the order <c>EnumWindows</c> gives. A window whose look fails (<see cref="IsReadFailure"/>) is left
    /// out; the list itself is read whole, here and not when the result is walked, so that a failed
    /// enumeration (<see cref="System.ComponentModel.Win32Exception"/>) leaves the call and never reads as no
    /// window.
    /// </summary>
    private List<AutomationElement> TopLevelElementsOf(int processId)
    {
        List<AutomationElement> found = [];
        foreach (var window in Win32.VisibleTopLevelWindowsOf((uint)processId))
        {
            AutomationElement? element = null;
            try
            {
                element = _automation.FromHandle(window);
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                // The window went between the list and the look, or UI Automation could not read it: the
                // look skips it.
            }

            if (element is not null)
            {
                found.Add(element);
            }
        }

        return found;
    }

    /// <summary>The screen point in the middle of the title bar: half the window's width across, half the title bar's height down.</summary>
    private Point TitleBarMiddle()
    {
        NativeMethods.GetWindowRect(Handle, out var window);
        var client = ClientOriginOnScreen();
        return new Point(window.Left + (window.Width / 2), (window.Top + client.Y) / 2);
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
    /// is asked, outside the handlers, and never throw (<see cref="SelectedRowEvents"/> says how). Read
    /// the graph's first child before (D455): cold, a move raises no event from the row. Dispose it to
    /// stop recording.
    /// </summary>
    public SelectedRowEvents RecordSelectedRowEvents() => new(_automation, Find("GraphRows"));

    /// <summary><c>VisualCommit.dll</c> beside the executable: the app's own assembly, whose version resource holds the product version.</summary>
    private static string BuiltAssemblyPath => Path.Combine(Path.GetDirectoryName(Executable)!, "VisualCommit.dll");

    /// <summary>
    /// <c>WM_GETICON</c> with <paramref name="which"/>, sent with a limit: false when the app did not
    /// answer within <see cref="MessageAnswerMilliseconds"/> (or is hung), and <paramref name="icon"/> is
    /// then none; true with the icon's handle, or none when the window gives that icon none.
    /// </summary>
    private bool AskIcon(IntPtr which, out IntPtr icon)
    {
        var sent = Win32.SendMessageTimeoutW(Handle, Win32.WmGetIcon, which, IntPtr.Zero, Win32.SmtoBlock | Win32.SmtoAbortIfHung, MessageAnswerMilliseconds, out icon);
        if (sent == IntPtr.Zero)
        {
            icon = IntPtr.Zero;
            return false;
        }

        return true;
    }

    /// <summary>A top-level window in words for a message: its handle, its class and its title, as Windows gives them.</summary>
    private static string WindowWords(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return "none";
        }

        var type = new System.Text.StringBuilder(256);
        var title = new System.Text.StringBuilder(256);
        _ = Win32.GetClassNameW(window, type, type.Capacity);
        _ = Win32.GetWindowTextW(window, title, title.Capacity);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"handle 0x{window.ToInt64():X}, class \"{type}\", title \"{title}\"");
    }

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

    /// <summary>
    /// One look for <see cref="TaskbarButtons"/>: every button on a taskbar whose name starts with
    /// <paramref name="nameStart"/>, with non-empty bounds, the taskbars in the order Windows lists them and
    /// each one's buttons in the order UI Automation's search of it gives. None when a read through UI
    /// Automation failed while the taskbars were walked (<see cref="IsReadFailure"/>, a taskbar that changed
    /// among them), so that a look never counts part of a taskbar. The taskbars' windows are listed outside
    /// that catch: a failed enumeration (<see cref="System.ComponentModel.Win32Exception"/>) leaves the call and
    /// never reads as no taskbar.
    /// </summary>
    private List<TaskbarEntry> FindTaskbarButtons(string nameStart)
    {
        // The taskbars found in Windows' list of windows by their classes.
        var bars = Win32.VisibleTopLevelWindowsOfClass("Shell_TrayWnd", "Shell_SecondaryTrayWnd");
        List<TaskbarEntry> found = [];
        try
        {
            // Each taskbar asked of UI Automation by its handle, its buttons searched through UI Automation.
            foreach (var bar in bars.Select(window => _automation.FromHandle(window)))
            {
                var taskbar = bar.Properties.ClassName.ValueOrDefault ?? "a taskbar";
                foreach (var button in bar.FindAllDescendants(condition => condition.ByControlType(ControlType.Button)))
                {
                    var name = button.Properties.Name.ValueOrDefault;
                    if (name is not null && name.StartsWith(nameStart, StringComparison.Ordinal) && button.BoundingRectangle is { Width: > 0, Height: > 0 } bounds)
                    {
                        found.Add(new TaskbarEntry(name, bounds, taskbar));
                    }
                }
            }
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // A taskbar redrew while it was walked, or a read of it failed: the next look reads it again, whole.
            return [];
        }

        return found;
    }

    /// <summary>A button of the taskbar: its UI Automation name, its bounds on the screen, in real pixels, and the class of the taskbar it lies on (<c>Shell_TrayWnd</c>, or <c>Shell_SecondaryTrayWnd</c> on another display).</summary>
    public sealed record TaskbarEntry(string Name, Rectangle ScreenBounds, string Taskbar)
    {
        /// <summary>Whether the button's centre lies inside <paramref name="display"/> (real pixels, <see cref="DisplayBounds"/>): the button is on that display's taskbar.</summary>
        public bool LiesOn(Rectangle display) =>
            display.Contains(ScreenBounds.X + (ScreenBounds.Width / 2), ScreenBounds.Y + (ScreenBounds.Height / 2));
    }

    /// <summary>
    /// The window's icons (<see cref="WindowIcons"/>): <c>ICON_SMALL</c>'s and <c>ICON_BIG</c>'s images
    /// (null where the window gives none, or did not answer in time), the window's DPI, <c>SM_CXSMICON</c>
    /// and <c>SM_CXICON</c> at that DPI, the sizes Windows draws the small icon (the title bar's) and the
    /// big one at, and whether each <c>WM_GETICON</c> went unanswered within its limit.
    /// </summary>
    public sealed record WindowIconSet(Bitmap? Small, Bitmap? Big, uint Dpi, int SmallIconMetric, int IconMetric, bool SmallUnanswered, bool BigUnanswered) : IDisposable
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

    /// <summary>
    /// One event from <c>GraphRows</c>' subtree: its kind (<see cref="SelectedRowEvents.NameChanged"/> or
    /// <see cref="SelectedRowEvents.LiveRegionChanged"/>), its sender's automation id (<see cref="NotReadable"/>
    /// where no read of the sender, outside the handlers, gave it, null where the sender gives none), and when
    /// it came, from the recording's start.
    /// </summary>
    public sealed record RowEvent(string Kind, string? AutomationId, TimeSpan At);

    /// <summary>
    /// The graph row's events while recording (<see cref="RecordSelectedRowEvents"/>). The handlers
    /// queue the kind, the sender and the time only. <see cref="Events"/>, outside any handler, reads
    /// each sender's id from UI Automation at that moment, keeps it once read, and never throws: a read
    /// that fails leaves <see cref="NotReadable"/>, which later calls try again,
    /// <see cref="IdReadAttempts"/> reads in all. Disposing it removes both handlers.
    /// </summary>
    public sealed class SelectedRowEvents : IDisposable
    {
        /// <summary>The kind of a <c>Name</c> property change.</summary>
        public const string NameChanged = "Name";

        /// <summary>The kind of a <c>LiveRegionChanged</c> event.</summary>
        public const string LiveRegionChanged = "LiveRegionChanged";

        /// <summary>
        /// How many calls of <see cref="Events"/> read a sender's id that could not be read before
        /// <see cref="NotReadable"/> stands for it: a sender that was busy may answer the next call, one that
        /// went answers none, and each failed read costs a call into the app.
        /// </summary>
        private const int IdReadAttempts = 3;

        private readonly ConcurrentQueue<(string Kind, AutomationElement Sender, TimeSpan At)> _queue = new();
        private readonly List<Received> _received = [];
        private readonly Lock _gate = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly PropertyChangedEventHandlerBase _names;
        private readonly AutomationEventHandlerBase _live;

        internal SelectedRowEvents(UIA3Automation automation, AutomationElement rows)
        {
            // No cache request: FlaUI 5.0.0 passes none to UI Automation when it registers an event
            // handler, so a sender comes with no property cached, and a cached read of its id fails every
            // time. The id is read now, outside the handlers (TryReadId).
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

        /// <summary>How long the recording has run: a mark to compare the events' times with.</summary>
        public TimeSpan Now => _clock.Elapsed;

        /// <summary>
        /// Every event so far, in the order they came, outside the handlers: the events queued since the last
        /// call taken in, the ids not read yet read (<see cref="TryReadId"/>), and none of it throws.
        /// </summary>
        public IReadOnlyList<RowEvent> Events()
        {
            lock (_gate)
            {
                while (_queue.TryDequeue(out var queued))
                {
                    _received.Add(new Received(queued.Kind, queued.Sender, queued.At));
                }

                foreach (var received in _received.Where(item => !item.Settled))
                {
                    var read = TryReadId(received.Sender, out var id);
                    received.Took(read, id);
                }

                return [.. _received.Select(item => new RowEvent(item.Kind, item.Id, item.At))];
            }
        }

        public void Dispose()
        {
            _live.Dispose();
            _names.Dispose();
        }

        /// <summary>
        /// A sender's automation id, read from UI Automation now, outside the handlers, never throwing: false
        /// with <see cref="NotReadable"/> when the read fails (<see cref="IsReadFailure"/>). No cache is read:
        /// FlaUI 5.0.0 registers both handlers without handing UI Automation a cache request (it passes
        /// none), so a sender comes with no property cached and a cached read would fail every time.
        /// </summary>
        private static bool TryReadId(AutomationElement sender, out string? id)
        {
            try
            {
                id = sender.Properties.AutomationId.ValueOrDefault;
                return true;
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                id = NotReadable;
                return false;
            }
        }

        /// <summary>
        /// One event taken from the queue, with its sender's id as read so far: settled once read, or once
        /// <see cref="IdReadAttempts"/> reads have failed, when <see cref="NotReadable"/> stands.
        /// </summary>
        private sealed class Received(string kind, AutomationElement sender, TimeSpan at)
        {
            private int _attempts;

            public string Kind { get; } = kind;

            public AutomationElement Sender { get; } = sender;

            public TimeSpan At { get; } = at;

            public string? Id { get; private set; } = NotReadable;

            public bool Settled { get; private set; }

            /// <summary>Takes one read's result: the id when <paramref name="read"/>, else one failed attempt more.</summary>
            public void Took(bool read, string? id)
            {
                Id = id;
                Settled = read || ++_attempts >= IdReadAttempts;
            }
        }
    }

    /// <summary>
    /// The Win32 calls of these launches beside the shared <see cref="NativeMethods"/>: the window's
    /// state on the desktop, its icons, its visible frame, the window under a point and the top-level
    /// windows Windows lists (<c>EnumWindows</c>). Each only reads, but for <c>ShowWindowAsync</c>, which
    /// minimises the app's own window. No call waits on the app's thread without a limit: <c>WM_GETICON</c>
    /// goes by <c>SendMessageTimeoutW</c>, the minimise is posted, and <c>GetWindowTextW</c> reads another
    /// process's window's title without a message.
    /// </summary>
    private static class Win32
    {
        public const int SwMinimize = 6;
        public const uint WmGetIcon = 0x007F;
        public const int SmCxIcon = 11;
        public const int SmCxSmIcon = 49;
        public const uint SmtoBlock = 0x0001;
        public const uint SmtoAbortIfHung = 0x0002;
        public const uint GaRoot = 2;
        public const int DwmwaExtendedFrameBounds = 9;

        public static readonly IntPtr IconSmall = new(0);
        public static readonly IntPtr IconBig = new(1);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindowAsync(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr window);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        /// <summary>
        /// The visible top-level windows of one of the window classes named, as Windows lists them.
        /// <see cref="System.ComponentModel.Win32Exception"/> when Windows fails to list them (<see cref="Enumerate"/>).
        /// </summary>
        public static List<IntPtr> VisibleTopLevelWindowsOfClass(params string[] classes)
        {
            var found = new List<IntPtr>();
            var name = new System.Text.StringBuilder(256);
            Enumerate(window =>
            {
                if (IsWindowVisible(window) && GetClassNameW(window, name, name.Capacity) > 0 && classes.Contains(name.ToString(), StringComparer.Ordinal))
                {
                    found.Add(window);
                }
            });
            return found;
        }

        /// <summary>
        /// The visible top-level windows of a process, in the order <c>EnumWindows</c> gives (the Z order in
        /// practice), as Windows lists them. <see cref="System.ComponentModel.Win32Exception"/> when Windows
        /// fails to list them (<see cref="Enumerate"/>).
        /// </summary>
        public static List<IntPtr> VisibleTopLevelWindowsOf(uint process)
        {
            var found = new List<IntPtr>();
            Enumerate(window =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == process && IsWindowVisible(window))
                {
                    found.Add(window);
                }
            });
            return found;
        }

        /// <summary>
        /// Hands every top-level window Windows lists (<c>EnumWindows</c>) to <paramref name="each"/>. When
        /// <c>EnumWindows</c> returns false with an error set, a
        /// <see cref="System.ComponentModel.Win32Exception"/> with that error: a failed enumeration never reads as
        /// no window, so that a check that asserts there is none (I1's second start) fails then instead of passing.
        /// </summary>
        private static void Enumerate(Action<IntPtr> each)
        {
            if (!EnumWindows(
                    (window, _) =>
                    {
                        each(window);
                        return true;
                    },
                    IntPtr.Zero)
                && Marshal.GetLastPInvokeError() is var error and not 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Windows did not list its top-level windows (EnumWindows failed with error {error}).");
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMilliseconds, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(NativeMethods.Point point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr window, System.Text.StringBuilder name, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(IntPtr window, System.Text.StringBuilder text, int capacity);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeMethods.Rect value, int size);
    }
}
