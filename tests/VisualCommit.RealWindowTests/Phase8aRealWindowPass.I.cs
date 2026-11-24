// Part I's three launches of the 8a pass (docs/test-reports/phase-8a.md, I1, I2 and "The real window (part
// I)"; docs/phase-8a-design.md 1.11, 4.3, 12.3.4 launches 4 to 6, 12.3.5; D421, D422, D443, D448): a second
// start on the same data folder hands over to the first and exits 0; the notice a start shows when the
// folder's lock is held and no instance answers, its "Try again" left unanswered and its "Close" ending
// the process with 2; and "Try again" starting the app in the same process once the lock is free. The
// notice is never sized; its captures are compared with their twins with the folder line masked. No
// launch writes anything into its data folder before the start but launch 4's session.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase8aRealWindowPass
{
    /// <summary>
    /// Launch 4 (design 12.3.4; I1, real window only, since no harness runs <c>Program.Main</c>): a new data
    /// folder D with a session of one empty tab written before the start, dark, 1100×700.
    /// <list type="number">
    /// <item>The welcome page; D holds <c>instance.lock</c> and <c>instance.json</c> (<c>v</c> 1, <c>pid</c> the
    /// app's); the log has the holder's line once.</item>
    /// <item>The window minimised; <c>session.json</c>'s and <c>instance.json</c>'s bytes read.</item>
    /// <item>A second start on D with the same executable and environment: it exits 0 within 10 s, having
    /// shown no window; within 2 s the app's window is not iconic and visible; the foreground noted, not
    /// asserted; the log has the second's "asked to come to the front" and the app's "answered activated";
    /// both files' bytes unchanged.</item>
    /// <item>The app closed by its window, exit code 0.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Check_I1_second_start_hands_over()
    {
        using var notes = NoteLaunch(nameof(Check_I1_second_start_hands_over));
        AssertDesktopReady();
        var data = Run.NewDataDirectory();
        var paths = new AppPaths(data);
        var folder = Path.GetFullPath(data);
        using var app = Launch(data, OneTab(null));
        WaitForWelcome(app);

        // After step 1: the lock and instance.json, the holder's line once.
        Assert.True(File.Exists(paths.InstanceLockFile), $"The data folder holds no {Path.GetFileName(paths.InstanceLockFile)}.");
        var (version, holder) = ReadInstanceFile(paths.InstanceFile);
        Note(string.Create(CultureInfo.InvariantCulture, $"instance.json: v {version}, pid {holder}; the app's process {app.ProcessId}"));
        Assert.Equal(1, version);
        Assert.Equal(app.ProcessId, holder);
        var holds = string.Format(CultureInfo.InvariantCulture, I.HoldsTheFolderLine, folder);
        var holdsLines = app.LogLines().Count(line => line.Contains(holds, StringComparison.Ordinal));
        Note(string.Create(CultureInfo.InvariantCulture, $"the log has the holder's line {holdsLines} times"));
        Assert.Equal(1, holdsLines);

        // Step 2: minimised; the two files' bytes.
        app.Minimise();
        var session = ReadShared(paths.SessionFile);
        var instance = ReadShared(paths.InstanceFile);

        // Step 3: the second start beside it, in the same environment.
        using var second = RealApp.StartBeside(data);
        var clock = Stopwatch.StartNew();
        var looksWithWindow = 0;
        app.WaitFor(
            () =>
            {
                if (app.HasWindowOfProcess(second.Id))
                {
                    looksWithWindow++;
                }

                return second.HasExited;
            },
            string.Create(CultureInfo.InvariantCulture, $"the second start (process {second.Id}) to exit"),
            I.SecondStartExitsWithin);
        var code = second.WaitForExit(I.SecondStartExitsWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"the second start (process {second.Id}) exited with code {code} after {clock.Elapsed.TotalSeconds:0.0} s; a window of it seen in {looksWithWindow} looks"));
        Assert.Equal(I.HandOverExitCode, code);
        Assert.True(looksWithWindow == 0, $"The second start showed a window ({looksWithWindow} looks found one).");

        clock.Restart();
        app.WaitFor(() => !app.IsIconic && app.IsVisibleOnDesktop, "the app's window not iconic and visible on the desktop", I.FirstComesBackWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"the app's window back after {clock.Elapsed.TotalSeconds:0.0} s; the foreground window: {app.ForegroundIsThis} (noted, not asserted)"));

        var asked = app.WaitForLogLine(RealApp.LogContaining(string.Format(CultureInfo.InvariantCulture, I.AskedToComeForwardLine, folder, holder)), StepTimeout);
        var answered = app.WaitForLogLine(RealApp.LogContaining(string.Format(CultureInfo.InvariantCulture, I.AnsweredActivatedLine, second.Id)), StepTimeout);
        Note($"the second's line: {asked}");
        Note($"the app's line: {answered}");

        var sessionAfter = ReadShared(paths.SessionFile);
        var instanceAfter = ReadShared(paths.InstanceFile);
        Note($"session.json unchanged: {session.AsSpan().SequenceEqual(sessionAfter)}; instance.json unchanged: {instance.AsSpan().SequenceEqual(instanceAfter)}");
        Assert.Equal(session, sessionAfter);
        Assert.Equal(instance, instanceAfter);

        // Step 4.
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Launch 5 (design 12.3.4; the twin of part I's method 1, <c>Check_I2_notice_dark_try_unanswered</c>): a
    /// new data folder D whose <c>instance.lock</c> the test holds, with no <c>instance.json</c>, no session
    /// and no settings; the app started on D, not sized.
    /// <list type="number">
    /// <item>The process's main window is the notice within 15 s; <c>InstanceNoticeFolder</c>'s name D's path;
    /// <c>i2a-notice-dark</c>, the folder line masked.</item>
    /// <item>The notice the foreground window with <c>Try again</c> focused, else one real click on its title
    /// bar and a wait of 5 s, else the method fails before any key.</item>
    /// <item>A real Enter: while <c>Try again</c> reads <c>Trying…</c>, disabled, <c>i2b-notice-trying</c>;
    /// within 10 s of the Enter <c>It still did not answer.</c>; <c>i2c-notice-no-answer</c>.</item>
    /// <item>A real click on <c>Close</c>: exit code 2 within 5 s; D holds <c>harness</c>,
    /// <c>instance.lock</c> and <c>logs</c> alone; the log's Warning that no instance.json answered.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Check_I2_notice_and_exit_code_2()
    {
        using var notes = NoteLaunch(nameof(Check_I2_notice_and_exit_code_2));
        AssertDesktopReady();
        var data = Run.NewDataDirectory();
        var paths = new AppPaths(data);
        using var held = HoldInstanceLock(paths);
        using var app = LaunchNotice(data);
        NoticePicture(app, I.I2aNoticeDark);

        // Before any key: the notice in front with Try again focused.
        EnsureForegroundBeforeFirstKey(app, () => NoticeReady(app), NoticeReadyWhat, I.NotForegroundMessage);

        // A real Enter, as the twin presses it: Trying…, disabled; i2b while it lasts.
        Keys(app, VirtualKeyShort.RETURN);
        var sinceEnter = Stopwatch.StartNew();
        app.WaitFor(
            () => app.Find(I.TryAgainId) is var button && button.Name == I.Trying && !button.IsEnabled,
            $"{I.TryAgainId} to read \"{I.Trying}\", disabled",
            StepTimeout);
        Note(string.Create(CultureInfo.InvariantCulture, $"\"{I.Trying}\" after {sinceEnter.Elapsed.TotalSeconds:0.0} s"));
        app.SetPointerOnTitleBar();
        app.WaitFor(() => ToolTipsShown(app).Count == 0, $"no tooltip in the notice before {I.I2bNoticeTrying}", StepTimeout);
        using (var trying = Capture(app, I.I2bNoticeTrying, keepPointer: true))
        {
            var tryAgain = app.Find(I.TryAgainId);
            Assert.True(
                tryAgain.Name == I.Trying && !tryAgain.IsEnabled,
                $"{I.TryAgainId} read \"{tryAgain.Name}\" (enabled {tryAgain.IsEnabled}) right after {I.I2bNoticeTrying} was captured: the try ended before the capture.");
            CompareMasked(app, trying, I.I2bNoticeTrying);
        }

        // Within 10 s of the Enter: the status; then Try again back, enabled and focused; i2c.
        var left = I.StatusShowsWithin - sinceEnter.Elapsed;
        app.WaitForText(I.StatusId, I.StillNoAnswer, left > TimeSpan.Zero ? left : TimeSpan.Zero);
        Note(string.Create(CultureInfo.InvariantCulture, $"\"{I.StillNoAnswer}\" after {sinceEnter.Elapsed.TotalSeconds:0.0} s"));
        app.WaitFor(
            () => app.Find(I.TryAgainId) is var button && button.Name == I.TryAgain && button.IsEnabled && app.FocusedAutomationId() == I.TryAgainId,
            $"{I.TryAgainId} to read \"{I.TryAgain}\" again, enabled and UI Automation's focused element",
            StepTimeout);
        NoticePicture(app, I.I2cNoticeNoAnswer);

        // A real click on Close: exit code 2; the data folder as the notice leaves it.
        Press(app, app.Find(I.CloseId));
        var sinceClose = Stopwatch.StartNew();
        var code = app.WaitForExit(I.ClosedExitWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"after Close the app exited with code {code} after {sinceClose.Elapsed.TotalSeconds:0.0} s"));
        Assert.Equal(I.ClosedExitCode, code);

        var entries = Directory.EnumerateFileSystemEntries(data).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal).ToList();
        Note($"the data folder holds: {string.Join(", ", entries)}");
        Assert.Equal(I.NoticeDataFolderEntries, entries);
        var warning = string.Format(CultureInfo.InvariantCulture, I.NoInstanceFileLine, Path.GetFullPath(data));
        var warned = RealApp.ReadLog(data).Contains(warning, StringComparison.Ordinal);
        Note($"the log has \"{warning}\": {warned}");
        Assert.True(warned, $"The log does not hold \"{warning}\".");
    }

    /// <summary>
    /// Launch 6 (design 12.3.4; D422): as launch 5 up to the notice, without its pictures; then the test
    /// frees <c>instance.lock</c>; the guard of launch 5; a real Enter on <c>Try again</c>: within 15 s the
    /// process shows its main window, with <c>GitStatus</c> in it, and the notice has closed;
    /// <c>instance.json</c>'s <c>pid</c> is the app's. That window closed by its window: exit code 0 within 15 s.
    /// </summary>
    [Fact]
    public void Check_I2_notice_try_again_starts_here()
    {
        using var notes = NoteLaunch(nameof(Check_I2_notice_try_again_starts_here));
        AssertDesktopReady();
        var data = Run.NewDataDirectory();
        var paths = new AppPaths(data);
        using var held = HoldInstanceLock(paths);
        using var app = LaunchNotice(data);

        // The lock freed; the guard; a real Enter on Try again.
        held.Dispose();
        Note($"the test released {Path.GetFileName(paths.InstanceLockFile)}");
        EnsureForegroundBeforeFirstKey(app, () => NoticeReady(app), NoticeReadyWhat, I.NotForegroundMessage);
        Keys(app, VirtualKeyShort.RETURN);
        var clock = Stopwatch.StartNew();

        // The main window in the same process, GitStatus in it; the notice closed. The RealApp's own window
        // is still the notice's.
        Window? main = null;
        app.WaitFor(
            () =>
            {
                main = app.OtherWindowOfProcess();
                return main is not null && HasGitStatus(main) && !app.IsVisibleOnDesktop;
            },
            "the main window, with GitStatus, in the notice's process, and the notice closed",
            I.StartedHereWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"the main window \"{main!.Title}\" after {clock.Elapsed.TotalSeconds:0.0} s, the notice closed"));

        var (version, holder) = ReadInstanceFile(paths.InstanceFile);
        Note(string.Create(CultureInfo.InvariantCulture, $"instance.json: v {version}, pid {holder}; the app's process {app.ProcessId}"));
        Assert.Equal(app.ProcessId, holder);

        // That window closed by its window: exit code 0.
        main.Close();
        clock.Restart();
        var code = app.WaitForExit(I.StartedHereWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"the app exited with code {code} after {clock.Elapsed.TotalSeconds:0.0} s"));
        Assert.Equal(I.StartedHereExitCode, code);
    }

    // ---- Part I's steps ----

    /// <summary>What launches 5 and 6 wait for before their first key, as run.txt names it.</summary>
    private const string NoticeReadyWhat = "the notice the foreground window with Try again UI Automation's focused element";

    /// <summary>The notice is the foreground window and <c>InstanceNoticeTryAgain</c> is UI Automation's focused element: a real Enter then reaches Try again alone.</summary>
    private static bool NoticeReady(RealApp app) => app.ForegroundIsThis && app.FocusedAutomationId() == I.TryAgainId;

    /// <summary>
    /// Opens <paramref name="paths"/>' <c>instance.lock</c> as a running instance holds it, with
    /// <see cref="FileShare.None"/>, and with no <c>instance.json</c>: a start on the folder then finds it
    /// held and no instance to ask. Declared before the app, so that the app stops first and the file is
    /// released after it.
    /// </summary>
    private FileStream HoldInstanceLock(AppPaths paths)
    {
        Assert.False(File.Exists(paths.InstanceFile), $"The new data folder already holds {Path.GetFileName(paths.InstanceFile)}.");
        var held = new FileStream(paths.InstanceLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Note($"the test holds {paths.InstanceLockFile} (FileShare.None), with no instance.json");
        return held;
    }

    /// <summary>
    /// Starts the app on <paramref name="data"/>, whose lock the test holds, with no session and not sized
    /// (design 12.3.4 launch 5): the process's main window must be the notice (<c>InstanceNotice</c>), found
    /// within <see cref="Phase8aExpectations.I.NoticeShowsWithin"/>, and <c>InstanceNoticeFolder</c>'s
    /// automation name D's full path. run.txt gets the time and the notice's size. Disposes the app when that
    /// fails.
    /// </summary>
    private RealApp LaunchNotice(string data)
    {
        var clock = Stopwatch.StartNew();
        var app = Launch(data, session: null, environment: null, sized: false);
        try
        {
            var took = clock.Elapsed;
            var id = app.Window.AutomationId;
            Note(string.Create(CultureInfo.InvariantCulture, $"the process's main window \"{id}\" after {took.TotalSeconds:0.0} s, its client area {app.ClientSize.Width:0.#}x{app.ClientSize.Height:0.#} logical pixels at {app.Scaling:0.##}"));
            Assert.Equal(I.NoticeId, id);
            Assert.True(took <= I.NoticeShowsWithin, string.Create(CultureInfo.InvariantCulture, $"The notice came after {took.TotalSeconds:0.0} s, past {I.NoticeShowsWithin.TotalSeconds:0} s."));
            var folder = app.Find(I.FolderId).Name;
            Note($"{I.FolderId}'s name: {folder}");
            Assert.Equal(Path.GetFullPath(data), folder);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A picture of the notice (design 12.3.4 launch 5; D443 as D448 amends it): the pointer on the notice's
    /// title bar, off its client area (the main window's neutral point lies outside the notice), and no
    /// tooltip in it; the client area captured under the twin's name and compared with the folder line
    /// masked (<see cref="CompareMasked"/>).
    /// </summary>
    private void NoticePicture(RealApp app, string name)
    {
        app.SetPointerOnTitleBar();
        app.WaitFor(() => ToolTipsShown(app).Count == 0, $"no tooltip in the notice before {name}", StepTimeout);
        using var shot = Capture(app, name, keepPointer: true);
        CompareMasked(app, shot, name);
    }

    /// <summary>Whether <paramref name="window"/> holds <c>GitStatus</c>, the status bar's git, as the main window does; false while UI Automation cannot read it yet.</summary>
    private static bool HasGitStatus(Window window)
    {
        try
        {
            return window.FindFirstDescendant(condition => condition.ByAutomationId("GitStatus")) is not null;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or FlaUI.Core.Exceptions.FlaUIException)
        {
            return false;
        }
    }

    /// <summary>
    /// <c>instance.json</c>'s <c>v</c> and <c>pid</c> (1.11: <c>{"v":1,"pid":…,"endpoint":…,"started":…}</c>),
    /// read without keeping the instance that holds it from replacing it.
    /// </summary>
    private static (int Version, int ProcessId) ReadInstanceFile(string path)
    {
        using var document = JsonDocument.Parse(ReadShared(path));
        return (document.RootElement.GetProperty("v").GetInt32(), document.RootElement.GetProperty("pid").GetInt32());
    }

    /// <summary>A file's bytes, read while the app may replace or write it (shared for reading, writing and deleting).</summary>
    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
