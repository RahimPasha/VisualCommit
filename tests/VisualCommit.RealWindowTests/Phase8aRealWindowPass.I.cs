// Part I's three launches of the 8a pass (docs/test-reports/phase-8a.md, I1, I2 and "The real window (part
// I)"; docs/phase-8a-design.md 1.11, 4.3, 12.3.4 launches 4 to 6, 12.3.5; D421, D422, D443, D448, D459): a
// second start on the same data folder hands over to the first and exits 0; the notice a start shows when
// the folder's lock is held and no instance answers, its "Try again" left unanswered and its "Close" ending
// the process with 2; and "Try again" starting the app in the same process once the lock is free. The
// notice is never sized; its captures are taken once it is in front with Try again focused, compared with
// their twins with the folder line masked, and its words are read by name beside them (D459). No launch
// writes anything into its data folder before the start but launch 4's session.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Core.Git;
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
    /// shown no window (looked for at each of the wait's looks, one every 100 ms; run.txt gives how many
    /// looks found one of how many); within 2 s the app's window is not iconic and visible; the foreground
    /// noted, not asserted; the log has the second's "asked to come to the front" and the app's "answered
    /// activated"; both files' bytes unchanged.</item>
    /// <item>The app closed by its window, exit code 0.</item>
    /// </list>
    /// run.txt gets every value asserted, before it is asserted.
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
        var locked = File.Exists(paths.InstanceLockFile);
        Note($"after step 1: {Path.GetFileName(paths.InstanceLockFile)} in the data folder: {(locked ? "present" : "missing")}");
        Assert.True(locked, $"The data folder holds no {Path.GetFileName(paths.InstanceLockFile)}.");
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
        var looks = 0;
        var looksWithWindow = 0;
        app.WaitFor(
            () =>
            {
                looks++;
                if (app.HasWindowOfProcess(second.Id))
                {
                    looksWithWindow++;
                }

                return second.HasExited;
            },
            string.Create(CultureInfo.InvariantCulture, $"the second start (process {second.Id}) to exit"),
            I.SecondStartExitsWithin);
        var code = second.WaitForExit(I.SecondStartExitsWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"the second start (process {second.Id}) exited with code {code} after {clock.Elapsed.TotalSeconds:0.0} s; a window of it seen in {looksWithWindow} of {looks} looks, one every 100 ms"));
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
        Assert.Equal(0, CloseNoted(app));
    }

    /// <summary>
    /// Launch 5 (design 12.3.4 as D459 amends it; the twin of part I's method 1,
    /// <c>Check_I2_notice_dark_try_unanswered</c>): a new data folder D whose <c>instance.lock</c> the test
    /// holds, with no <c>instance.json</c>, no session and no settings; the app started on D, not sized.
    /// <list type="number">
    /// <item>The process's main window is the notice within 15 s; <c>InstanceNoticeFolder</c>'s name D's
    /// path.</item>
    /// <item>Before any capture and any key: the notice the foreground window with <c>Try again</c> UI
    /// Automation's focused element, as its twin shows it, else one real click on its title bar and a wait
    /// of at most 5 s, else the method fails here with the guard's message.</item>
    /// <item>The notice's words by name: its heading, its body, its sentence and its two buttons read through
    /// UI Automation against the twin's texts (a text UI Automation does not give, as one that reads
    /// otherwise, is noted and fails the method: D459 widened the picture's share only in exchange for these
    /// words held by name); <c>i2a-notice-dark</c>, the folder line masked; then the words asserted.</item>
    /// <item>A real Enter: while <c>Try again</c> reads <c>Trying…</c>, disabled, <c>i2b-notice-trying</c>,
    /// and Try again read again right after its capture, noted, then asserted; within 10 s of the Enter
    /// <c>It still did not answer.</c>, waited for by name, read again and noted (its wait fails when it
    /// never comes); <c>i2c-notice-no-answer</c>.</item>
    /// <item>A real click on <c>Close</c>: exit code 2 within 5 s; D holds <c>harness</c>,
    /// <c>instance.lock</c> and <c>logs</c> alone; the log's Warning that no instance.json answered.</item>
    /// </list>
    /// Each picture is compared masked (<c>CompareMasked</c>): its height within 3 px of the twin's and at
    /// most 5 % of its pixels differing (D459).
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

        // Before i2a and any key (D459): the notice in front with Try again focused, as its twin shows it;
        // else one click on its title bar and a wait of 5 s; else the method fails here.
        EnsureForegroundBeforeFirstKey(app, NoticeReady, NoticeReadyWhat, I.NotForegroundMessage, $"before {I.I2aNoticeDark} and the first key");

        // The notice's words by name, as its twin asserts them (D459): one UI Automation does not give
        // fails as one that reads otherwise; i2a; then the words asserted.
        var words = ReadNoticeWords(
            app,
            I.I2aNoticeDark,
            unreadableFails: true,
            (I.HeadingId, I.Heading),
            (I.BodyId, I.Body),
            (I.IfNoWindowId, I.IfNoWindow(GuidancePlatform.Windows)),
            (I.CloseId, I.Close),
            (I.TryAgainId, I.TryAgain));
        NoticePicture(app, I.I2aNoticeDark);
        Assert.True(words.Count == 0, string.Join('\n', words));

        // A real Enter, as the twin presses it: Trying…, disabled; i2b while it lasts.
        KeysOn(app, new FocusTarget(I.TryAgainId, I.TryAgain), VirtualKeyShort.RETURN);
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
            var (label, enabled) = (tryAgain.Name, tryAgain.IsEnabled);
            Note($"{I.TryAgainId} right after {I.I2bNoticeTrying} was captured: \"{label}\", enabled {enabled}");
            Assert.True(
                label == I.Trying && !enabled,
                $"{I.TryAgainId} read \"{label}\" (enabled {enabled}) right after {I.I2bNoticeTrying} was captured: the try ended before the capture.");
            CompareMasked(app, trying, I.I2bNoticeTrying);
        }

        // Within 10 s of the Enter: the status, waited for by its name, which fails when it never comes;
        // then Try again back, enabled and focused; the status's words read again and noted, an unreadable
        // read noted alone, as its wait has held it by name (D459); i2c.
        var left = I.StatusShowsWithin - sinceEnter.Elapsed;
        app.WaitForText(I.StatusId, I.StillNoAnswer, left > TimeSpan.Zero ? left : TimeSpan.Zero);
        Note(string.Create(CultureInfo.InvariantCulture, $"\"{I.StillNoAnswer}\" after {sinceEnter.Elapsed.TotalSeconds:0.0} s"));
        app.WaitFor(
            () => app.Find(I.TryAgainId) is var button && button.Name == I.TryAgain && button.IsEnabled && app.FocusedId() == I.TryAgainId,
            $"{I.TryAgainId} to read \"{I.TryAgain}\" again, enabled and UI Automation's focused element",
            StepTimeout);
        var status = ReadNoticeWords(app, I.I2cNoticeNoAnswer, unreadableFails: false, (I.StatusId, I.StillNoAnswer));
        NoticePicture(app, I.I2cNoticeNoAnswer);
        Assert.True(status.Count == 0, string.Join('\n', status));

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
        EnsureForegroundBeforeFirstKey(app, NoticeReady, NoticeReadyWhat, I.NotForegroundMessage);
        KeysOn(app, new FocusTarget(I.TryAgainId, I.TryAgain), VirtualKeyShort.RETURN);
        var clock = Stopwatch.StartNew();

        // The main window in the same process, GitStatus in it; the notice closed. The RealApp's own window
        // is still the notice's. The notice's end is read from Windows, and the main window is found by its
        // handle (RealApp.OtherWindowOfProcess), every window of the process offered to HasGitStatus: a
        // search of the desktop's children through UI Automation asks every program on the desktop, and this
        // wait ran out on such a search in about two runs of five, though the app had closed the notice
        // within 0.1 s and opened its window within 1.5 s. Each look keeps what it read, so that a wait that
        // runs out says which of its grounds did not hold.
        Window? main = null;
        var (noticeGone, windows) = (false, 0);
        try
        {
            app.WaitFor(
                () =>
                {
                    noticeGone = app.IsWindowGone;
                    windows = app.VisibleWindowCount;
                    if (!noticeGone)
                    {
                        return false;
                    }

                    main = app.OtherWindowOfProcess(HasGitStatus);
                    return main is not null;
                },
                "the main window, with GitStatus, in the notice's process, and the notice closed",
                I.StartedHereWithin);
        }
        catch (TimeoutException ex)
        {
            var look = MainWindowLook(noticeGone, windows, main is not null);
            Note(string.Create(CultureInfo.InvariantCulture, $"no main window after {clock.Elapsed.TotalSeconds:0.0} s; the wait's last look: {look}"));
            throw new TimeoutException($"{ex.Message} The wait's last look: {look}.", ex);
        }

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

    /// <summary>
    /// The guard's condition before launches 5's and 6's first key, on what it read once: the notice is the
    /// foreground window (<paramref name="foreground"/>) and <c>InstanceNoticeTryAgain</c> is UI Automation's
    /// focused element (<paramref name="focusedId"/>): a real Enter then reaches Try again alone.
    /// </summary>
    private static bool NoticeReady(bool foreground, string? focusedId) => foreground && focusedId == I.TryAgainId;

    /// <summary>
    /// The notice's words held by name before <paramref name="picture"/> (D459), since its picture's share is
    /// wider than a main window's: each of <paramref name="words"/>' elements, the one on screen with that
    /// automation id, read once through UI Automation (its name: a text's text, a button's label) and compared
    /// with the twin's text, as <c>Phase8aChecksI</c> asserts it. Each is noted in run.txt as held, not held or
    /// not readable, with the reason. A text that reads otherwise is a problem. A text UI Automation does not
    /// give (no such element on screen, more than one, an empty name, a read that fails) is a problem too when
    /// <paramref name="unreadableFails"/>: D459 widened the notice's share only in exchange for its words held
    /// by name, so only this makes a missing line of the notice fail. Without it (the status line, which its
    /// wait has already held by name) it is noted alone. Returns the problems, for the caller to assert after
    /// the picture's comparison, so that run.txt holds both.
    /// </summary>
    private List<string> ReadNoticeWords(RealApp app, string picture, bool unreadableFails, params (string Id, string Text)[] words)
    {
        List<string> problems = [];
        foreach (var (id, text) in words)
        {
            var (name, unreadable) = ReadNoticeWord(app, id);
            if (unreadable is not null)
            {
                Note($"before {picture}: {id} not readable through UI Automation ({unreadable}; the twin's \"{text}\"): {(unreadableFails ? "a problem (D459)" : "noted alone, its wait having read it by name")}");
                if (unreadableFails)
                {
                    problems.Add($"{id} could not be read through UI Automation before {picture} ({unreadable}), so its twin's \"{text}\" is not held by name (D459).");
                }

                continue;
            }

            var held = string.Equals(name, text, StringComparison.Ordinal);
            Note($"before {picture}: {id} reads \"{name}\", the twin's \"{text}\": {(held ? "held" : "not held")}");
            if (!held)
            {
                problems.Add($"{id} reads \"{name}\" before {picture}, not its twin's \"{text}\".");
            }
        }

        return problems;
    }

    /// <summary>
    /// One read of <see cref="ReadNoticeWords"/>, never throwing: the name of the one element <paramref name="id"/>
    /// on screen, or null with why UI Automation did not give it: no such element on screen, more than one, a
    /// read that failed (<see cref="RealApp.IsReadFailure"/>), an empty name.
    /// </summary>
    private static (string? Name, string? Unreadable) ReadNoticeWord(RealApp app, string id)
    {
        IReadOnlyList<AutomationElement> shown;
        try
        {
            shown = app.ShownElements(id);
        }
        catch (Exception ex) when (RealApp.IsReadFailure(ex))
        {
            return (null, $"the look for it failed ({ex.GetType().Name})");
        }

        if (shown.Count != 1)
        {
            return (null, shown.Count == 0 ? "no such element on screen" : string.Create(CultureInfo.InvariantCulture, $"{shown.Count} such elements on screen"));
        }

        string? name;
        try
        {
            name = shown[0].Properties.Name.ValueOrDefault;
        }
        catch (Exception ex) when (RealApp.IsReadFailure(ex))
        {
            return (null, $"its name could not be read ({ex.GetType().Name})");
        }

        return string.IsNullOrEmpty(name) ? (null, "an empty name") : (name, null);
    }

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
    /// automation name D's full path. run.txt gets the time, the notice's size and, once in the run, the
    /// display. Disposes the app when that fails.
    /// </summary>
    private RealApp LaunchNotice(string data)
    {
        var clock = Stopwatch.StartNew();
        var app = Launch(data, session: null, environment: null, sized: false);
        try
        {
            var took = clock.Elapsed;

            // The display, once, as Sized notes it, so that a launch run alone has it in run.txt too.
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

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

    /// <summary>
    /// Launch 6's last look for the main window in words, for run.txt and the failure when its wait runs out:
    /// its three grounds as read, whether the notice's window was gone (<see cref="RealApp.IsWindowGone"/>),
    /// how many visible top-level windows the process had in Windows' list (<see cref="RealApp.VisibleWindowCount"/>)
    /// and whether one of them was accepted (<see cref="RealApp.OtherWindowOfProcess"/>, <see cref="HasGitStatus"/>),
    /// which is looked for only once the notice's window is gone.
    /// </summary>
    private static string MainWindowLook(bool noticeGone, int windows, bool accepted)
    {
        var notice = noticeGone ? "gone (IsWindow false)" : "still there (IsWindow true)";
        var taken = (accepted, noticeGone) switch
        {
            (true, _) => "one of them accepted",
            (false, true) => "none of them accepted (a window other than the notice, on the screen, with GitStatus in it)",
            (false, false) => "none looked at, as the notice's window was still there",
        };
        return string.Create(CultureInfo.InvariantCulture, $"the notice's window {notice}; {windows} visible top-level windows of the process in Windows' list; {taken}");
    }

    /// <summary>Whether <paramref name="window"/> holds <c>GitStatus</c>, the status bar's git, as the main window does; false while UI Automation cannot read it yet (<see cref="RealApp.IsReadFailure"/>), to be looked at again.</summary>
    private static bool HasGitStatus(Window window)
    {
        try
        {
            return window.FindFirstDescendant(condition => condition.ByAutomationId("GitStatus")) is not null;
        }
        catch (Exception ex) when (RealApp.IsReadFailure(ex))
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
