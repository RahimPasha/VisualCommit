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
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Runs the whole app in headless mode for a test and drives it the way a user would: with mouse
/// moves, presses and releases at window positions. One driver is one running instance of the
/// app; disposing it closes the app. Use it on the UI thread, inside an <c>[AvaloniaFact]</c>
/// or <see cref="FreshApplication.RunAsync{T}"/>.
/// </summary>
public sealed partial class ShellDriver : IDisposable
{
    /// <summary>
    /// How long <see cref="WaitForAsync"/> waits when the test names no time: 30 s, the limit
    /// phases 2 to 4 gave a wait after a write. Phase 5 records every action for undo (a stash
    /// makes 17 git calls), and in the full run's seven projects side by side a step took 5 to 8
    /// times as long as alone, so 10 s ran out for operations that end in 1 to 3 s alone. A wait
    /// still ends as soon as its condition holds.
    /// </summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The pause between two looks at a condition in <see cref="WaitForAsync"/>: short enough that
    /// a wait ends soon after the condition comes true, long enough not to keep a core busy.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    private ShellDriver(
        AppSession session,
        FakeFolderPicker folders,
        FakeFileLauncher launcher,
        FakeClipboard clipboard,
        FakeSystemTheme systemTheme,
        TimeProvider? readClock)
    {
        Session = session;
        Folders = folders;
        Launcher = launcher;
        Clipboard = clipboard;
        SystemTheme = systemTheme;
        ReadClock = readClock;

        // Check 31 waits for the window to close by itself (ShellDriver.Checks.cs).
        session.MainWindow.Closed += (_, _) => IsWindowClosed = true;
    }

    /// <summary>The app instance under test.</summary>
    public AppSession Session { get; }

    /// <summary>The app's folder dialog: queue the folder the next Open, Init or Browse "chooses" (D42).</summary>
    public FakeFolderPicker Folders { get; }

    /// <summary>
    /// The operating system's mode as this session sees it (docs/phase-6-design.md 11.3, 17.2):
    /// dark unless <see cref="Start"/> was told otherwise; <see cref="FakeSystemTheme.Set"/> flips it.
    /// </summary>
    public FakeSystemTheme SystemTheme { get; }

    /// <summary>
    /// The clock of a dashboard's 10 s rule that <see cref="Start"/> was given (docs/phase-6-design.md
    /// 2.10, 17.2), such as a <see cref="ManualTimeProvider"/> the check moves; null when it was given
    /// none and the session's own clock reads for it.
    /// </summary>
    public TimeProvider? ReadClock { get; }

    public MainWindow Window => Session.MainWindow;

    /// <summary>
    /// Starts the app on <paramref name="dataDirectory"/> through the same start-up code as the
    /// desktop app, and shows its window at the given size in logical pixels. Pass null for a
    /// size to keep the one the app chose itself, for a check of what the app restores.
    /// The folder dialog, the file launcher and the clipboard are fakes (<see cref="Folders"/>,
    /// <see cref="Launcher"/>, <see cref="Clipboard"/>), so nothing outside the window is opened or
    /// changed. Prompts of git and ssh go to the app's own dialogs, unless
    /// <paramref name="promptHandler"/> (a <see cref="ScriptedPromptHandler"/>) answers them instead.
    /// The record clock stands at <see cref="AppClocks.HarnessTime"/> and the backup clock is fixed
    /// there (docs/phase-5-design.md 2.3), as the real-window pass's are.
    /// <para>
    /// Phase 6 (docs/phase-6-design.md 17.2, 8.12, 12.9, 11.3, 2.10): the session's git calls get
    /// a global configuration of their own in the data folder
    /// (<see cref="GitIsolation.SessionGitEnvironment"/>; a restart on the same folder keeps what
    /// the app wrote, and <see cref="SeedGlobalGitConfig"/> writes it before the start), the fixed
    /// tool catalogues (<see cref="ToolCatalogues.Fixed"/>), so that no picture shows this
    /// machine's editors, merge tools or <c>PATHEXT</c>, and a system theme of their own
    /// (<see cref="SystemTheme"/>), dark unless <paramref name="systemDark"/> is false.
    /// <paramref name="readClock"/> is the clock of a dashboard's 10 s rule (<see cref="ReadClock"/>);
    /// without it the session's own clock.
    /// </para>
    /// </summary>
    public static ShellDriver Start(
        string dataDirectory,
        int? width = 1100,
        int? height = 700,
        IGitPromptHandler? promptHandler = null,
        bool systemDark = true,
        TimeProvider? readClock = null)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("No Avalonia application is running. Is the test an [AvaloniaFact]?");

        // The conflict resolver's caret does not blink in a scripted check (D151).
        HoldCaretsStill();

        var folders = new FakeFolderPicker();
        var launcher = new FakeFileLauncher();
        var clipboard = new FakeClipboard();
        var systemTheme = new FakeSystemTheme(systemDark);
        // The activity log's times read 09:00:00 and its durations 0 ms, and the backups get the
        // same names in every run, still in order (docs/phase-5-design.md 2.3).
        var session = AppSession.Start(
            new AppPaths(dataDirectory),
            application,
            folders,
            launcher,
            clipboard,
            promptHandler,
            recordClock: new FrozenClock(AppClocks.HarnessTime),
            backupClock: BackupClock.Fixed(AppClocks.HarnessTime),
            gitEnvironment: GitIsolation.SessionGitEnvironment(dataDirectory),
            systemTheme: systemTheme,
            tools: ToolCatalogues.Fixed,
            readClock: readClock);
        if (width is not null)
        {
            session.MainWindow.Width = width.Value;
        }

        if (height is not null)
        {
            session.MainWindow.Height = height.Value;
        }

        session.MainWindow.Show();

        var driver = new ShellDriver(session, folders, launcher, clipboard, systemTheme, readClock);
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
        int? height = 700,
        IGitPromptHandler? promptHandler = null,
        bool systemDark = true,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        WriteSession(
            dataDirectory,
            new SessionState { Tabs = tabs.Select(path => new TabState(path)).ToList(), ActiveTab = activeTab });
        return Start(dataDirectory, width, height, promptHandler, systemDark, readClock);
    }

    /// <summary>
    /// Writes the session file of a data folder before the app starts on it, for a test that
    /// needs more than open tabs: the recent list, the window's placement, the panel widths.
    /// </summary>
    public static void WriteSession(string dataDirectory, SessionState state) =>
        JsonSessionStore.Write(new AppPaths(dataDirectory).SessionFile, state);

    /// <summary>
    /// The global git configuration of the app sessions on <paramref name="dataDirectory"/>
    /// (docs/phase-6-design.md 8.12, 17.2): <c>&lt;data&gt;/harness/global.gitconfig</c>, which
    /// <see cref="Start"/> hands the session (<see cref="GitIsolation.SessionGitEnvironment"/>) and
    /// git makes at its first write. The real-window pass's app gets the same file.
    /// </summary>
    public static string GlobalGitConfigPath(string dataDirectory) => GitIsolation.SessionGlobalConfigFile(dataDirectory);

    /// <summary>
    /// Writes <paramref name="text"/> as the whole global git configuration of the sessions on
    /// <paramref name="dataDirectory"/> (<see cref="GlobalGitConfigPath"/>), before the app starts on
    /// it: a check's seeded <c>pull.ff = only</c> or identity (docs/phase-6-design.md 17.1, 17.4).
    /// Written as given, in UTF-8 without a byte order mark, its folder made.
    /// </summary>
    public static void SeedGlobalGitConfig(string dataDirectory, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var path = GlobalGitConfigPath(dataDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

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

    /// <summary>
    /// Finds the control whose automation id is <paramref name="automationId"/>, where the user
    /// would look for it: in the open context menu first, then in the view of the dialog on top
    /// (<see cref="TopDialogView"/>: the confirmation over the Remotes dialog has its own
    /// <c>ConfirmButton</c>), then anywhere in the window, a visible control before a hidden one.
    /// </summary>
    public Control FindByAutomationId(string automationId) =>
        FindWhereShown(automationId)
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

    /// <summary>
    /// Moves the mouse to a window position and double-clicks the left button: two presses at one
    /// spot, which Avalonia counts as a double-click when the second comes within the platform's
    /// double-tap time, 500 ms, of the first by the window's input clock. That clock is real time,
    /// and the public helpers run the dispatcher's jobs and render a frame around every event, so
    /// the presses lay as far apart as the first click's work took: in the full run's projects
    /// side by side more than 500 ms, and the double-click came as two single clicks (P25's on
    /// Local branches › main checked nothing out; 700 ms between the presses does the same alone).
    /// The clock stands still for the presses (<see cref="HeadlessInputClock"/>), as a user's
    /// double-click carries the times of the presses however busy the app is; the app handles
    /// them as before.
    /// </summary>
    public void DoubleClick(Point position)
    {
        Window.MouseMove(position);
        using (HeadlessInputClock.Pause(Window))
        {
            for (var click = 0; click < 2; click++)
            {
                Window.MouseDown(position, MouseButton.Left);
                Window.MouseUp(position, MouseButton.Left);
            }
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
