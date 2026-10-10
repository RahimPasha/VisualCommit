using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Tests of the headless test driver itself: the parts that phase 1's checks lean on and that
/// nothing else proves, so that a check that fails points at the app and not at the harness.
/// </summary>
public class ShellDriverTests
{
    [AvaloniaFact]
    public async Task WaitForAsync_returns_once_work_on_the_thread_pool_has_posted_its_result_back()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        await WaitsForWorkThatPostsBackFromThePoolAsync(app);
    }

    // Not an [AvaloniaFact]: this is how a check that crosses a restart runs the app (D32).
    [Fact]
    public async Task WaitForAsync_also_works_inside_a_fresh_application()
    {
        using var data = new TempDirectory("data");

        var finished = await FreshApplication.RunAsync(
            typeof(ShellDriverTests).Assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path);
                await app.WaitUntilReadyAsync();
                await WaitsForWorkThatPostsBackFromThePoolAsync(app);
                return true;
            },
            TestContext.Current.CancellationToken);

        Assert.True(finished);
    }

    [AvaloniaFact]
    public async Task WaitForAsync_times_out_with_a_message_that_says_what_it_waited_for()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var looks = 0;
        var clock = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForAsync(
            () =>
            {
                looks++;
                return false;
            },
            "a condition that never comes true",
            TimeSpan.FromMilliseconds(300)));

        Assert.Equal("Timed out after 0.3 s waiting for a condition that never comes true.", error.Message);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(300), $"It gave up after {clock.Elapsed.TotalMilliseconds} ms.");
        Assert.True(looks > 2, $"It looked at the condition {looks} times; it should keep looking until the time is up.");
    }

    [AvaloniaFact]
    public async Task Wheel_reaches_the_control_under_the_mouse_with_the_sign_of_a_real_wheel()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        // Whatever control is at this spot of the window: the test is about the driver, not
        // about one of the app's views.
        var position = new Point(550, 400);
        var target = Assert.IsAssignableFrom<Control>(app.Window.InputHitTest(position));
        var events = new List<(Vector Delta, Point Position)>();
        target.AddHandler(
            InputElement.PointerWheelChangedEvent,
            (_, e) => events.Add((e.Delta, e.GetPosition(app.Window))),
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        app.Wheel(position, 1);
        app.Wheel(position, -3);

        // Up is positive and down negative, one notch is 1, as Avalonia reports a real wheel.
        Assert.Equal(new[] { (new Vector(0, 1), position), (new Vector(0, -3), position) }, events);
    }

    [AvaloniaFact]
    public async Task StartWithTabs_writes_a_session_file_with_those_tabs_before_the_app_starts()
    {
        using var data = new TempDirectory("data");
        using var first = await TempRepo.CreateAsync("first");
        using var second = await TempRepo.CreateAsync("second");

        using var app = ShellDriver.StartWithTabs(data.Path, [first.Path, null, second.Path], activeTab: 2);
        var saved = new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current;
        await app.WaitUntilReadyAsync();

        Assert.Equal(2, saved.ActiveTab);
        Assert.Equal(3, saved.Tabs.Count);
        Assert.True(SameFolder(first.Path, saved.Tabs[0].RepositoryPath), $"The first tab is {saved.Tabs[0].RepositoryPath}.");
        Assert.Null(saved.Tabs[1].RepositoryPath);
        Assert.True(SameFolder(second.Path, saved.Tabs[2].RepositoryPath), $"The third tab is {saved.Tabs[2].RepositoryPath}.");
    }

    [Fact]
    public void WriteSession_writes_everything_in_the_state_to_the_file_the_app_reads()
    {
        using var data = new TempDirectory("data");
        var state = new SessionState
        {
            Tabs = [new TabState(data.Combine("a")), new TabState(null)],
            ActiveTab = 1,
            Recent = [new RecentRepository(data.Combine("a"), "a", new DateTimeOffset(2026, 10, 9, 12, 30, 0, TimeSpan.Zero))],
            Window = new WindowPlacement(40, 60, 1280, 800, IsMaximized: false),
            LeftPanelWidth = 300,
            RightPanelWidth = 420,
        };

        ShellDriver.WriteSession(data.Path, state);

        var saved = new JsonSessionStore(Path.Combine(data.Path, "session.json")).Current;
        Assert.Equal(state.Tabs, saved.Tabs);
        Assert.Equal(state.ActiveTab, saved.ActiveTab);
        Assert.Equal(state.Recent, saved.Recent);
        Assert.Equal(state.Window, saved.Window);
        Assert.Equal(state.LeftPanelWidth, saved.LeftPanelWidth);
        Assert.Equal(state.RightPanelWidth, saved.RightPanelWidth);
    }

    /// <summary>
    /// Starts work on the thread pool that posts its result back to the UI thread, the way the
    /// app's loading does, and waits for it with <see cref="ShellDriver.WaitForAsync"/>. The
    /// work only starts after the first look at the condition, so the wait has to last.
    /// </summary>
    private static async Task WaitsForWorkThatPostsBackFromThePoolAsync(ShellDriver app)
    {
        var done = false;
        var looks = 0;
        var alwaysOnTheUiThread = true;
        var firstLook = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = Task.Run(async () =>
        {
            await firstLook.Task;
            await Task.Delay(50);
            Dispatcher.UIThread.Post(() => done = true);
        });

        await app.WaitForAsync(
            () =>
            {
                looks++;
                alwaysOnTheUiThread &= Dispatcher.UIThread.CheckAccess();
                firstLook.TrySetResult();
                return done;
            },
            "the work on the thread pool");

        Assert.True(done);
        Assert.True(looks > 1, "The condition was true at the first look, so nothing was awaited.");
        Assert.True(alwaysOnTheUiThread, "The condition was read off the UI thread.");
        await work;
    }

    /// <summary>
    /// Whether two paths name the same folder, however each is spelled: the app may keep a path
    /// as git prints it, through the symbolic link of macOS's temp folder, by a long name where
    /// Windows gave a short one, or with forward slashes.
    /// </summary>
    private static bool SameFolder(string expected, string? actual)
    {
        if (actual is null)
        {
            return false;
        }

        var marker = "same-folder-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(expected, marker), string.Empty);
        try
        {
            return File.Exists(Path.Combine(actual, marker));
        }
        finally
        {
            File.Delete(Path.Combine(expected, marker));
        }
    }
}
