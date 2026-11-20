using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The single-instance notice in the headless app (docs/phase-8a-design.md 1.11, 4.6): an
/// <see cref="InstanceNoticeWindow"/> over an <see cref="InstanceNoticeViewModel"/> with the
/// platform's sentence, the folder given and the theme on the window's own
/// <see cref="TopLevel.RequestedThemeVariant"/> (never the application's, which other tests on the
/// same UI thread may change), and a fake try in place of the guard (<see cref="HeldNoticeTry"/>),
/// which a step holds and answers. No guard, no session, no data folder. Part I's checks capture
/// it at its own size, 480 wide at scale 1; screen readers' sweep shows it the same way.
/// </summary>
internal sealed class InstanceNoticeHost : IDisposable
{
    private static readonly TimeSpan WaitLimit = ShellDriver.DefaultWaitTimeout;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastStep;

    private InstanceNoticeHost(InstanceNoticeWindow window, InstanceNoticeViewModel notice, HeldNoticeTry tryAgain)
    {
        Window = window;
        Notice = notice;
        Try = tryAgain;
        window.Closed += (_, _) => IsOpen = false;
    }

    public InstanceNoticeWindow Window { get; }

    public InstanceNoticeViewModel Notice { get; }

    /// <summary>The fake try that "Try again" runs.</summary>
    public HeldNoticeTry Try { get; }

    /// <summary>The window is shown and has not closed.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>The element with the keyboard focus, or null.</summary>
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    /// <summary>
    /// Shows the notice for <paramref name="platform"/> on <paramref name="folder"/> in
    /// <paramref name="theme"/>; its "Try again" runs <paramref name="tryAgain"/> (a new held try
    /// when null).
    /// </summary>
    public static InstanceNoticeHost Show(GuidancePlatform platform, string folder, ThemeVariant theme, HeldNoticeTry? tryAgain = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(theme);
        tryAgain ??= new HeldNoticeTry();
        var notice = new InstanceNoticeViewModel(platform, folder, tryAgain.Run);
        var window = new InstanceNoticeWindow { DataContext = notice, RequestedThemeVariant = theme };
        var host = new InstanceNoticeHost(window, notice, tryAgain);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    /// <summary>The control with this automation id in the notice.</summary>
    public T Find<T>(string automationId)
        where T : Control =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId)
            ?? throw new InvalidOperationException($"No {typeof(T).Name} with the automation id '{automationId}' is in the notice.");

    /// <summary>Where a control is in the notice's frame, in logical pixels (the frame's scale is 1).</summary>
    public Rect BoundsOf(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        var topLeft = visual.TranslatePoint(default, Window) ?? throw new InvalidOperationException("The control is not in the notice.");
        return new Rect(topLeft, visual.Bounds.Size);
    }

    /// <summary>Lets the UI thread finish everything it has queued: bindings, layout, posted focus.</summary>
    public static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>
    /// Presses and releases one key, with optional modifiers. A press that closed the notice (Esc
    /// runs Close on the way down) has no release: the window is gone.
    /// </summary>
    public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        if (IsOpen)
        {
            Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        }

        Settle();
    }

    /// <summary>Waits on the UI thread until <paramref name="condition"/> holds, letting it run what was posted.</summary>
    public async Task WaitForAsync(Func<bool> condition, string what)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            Settle();
            if (condition())
            {
                return;
            }

            if (clock.Elapsed >= WaitLimit)
            {
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"Timed out after {WaitLimit.TotalSeconds:0} s waiting for {what}."));
            }

            // No ConfigureAwait(false): the next look must run on the UI thread.
            await Task.Delay(PollInterval);
        }
    }

    /// <summary>Renders the notice and returns its frame: its own size, at scale 1.</summary>
    public Screenshot Capture()
    {
        Settle();
        var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The notice has not rendered a frame.");
        return new Screenshot(frame);
    }

    /// <summary>A step's time since the notice was shown and since the step before, for the timings.</summary>
    public StepTime MarkStep(string name)
    {
        var at = _clock.Elapsed;
        var step = new StepTime(name, at, at - _lastStep);
        _lastStep = at;
        return step;
    }

    public void Dispose()
    {
        if (IsOpen)
        {
            Window.Close();
            Settle();
        }
    }
}

/// <summary>
/// A "Try again" in place of the guard's: each try waits until a step answers it
/// (<see cref="Answer"/>), and <see cref="Asked"/> counts the tries, so that a check sees one was
/// never asked.
/// </summary>
internal sealed class HeldNoticeTry
{
    private readonly Lock _gate = new();
    private TaskCompletionSource<NoticeRetry>? _pending;
    private int _asked;

    /// <summary>How many tries were asked for.</summary>
    public int Asked
    {
        get
        {
            lock (_gate)
            {
                return _asked;
            }
        }
    }

    /// <summary>A try is waiting for its answer.</summary>
    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                return _pending is { Task.IsCompleted: false };
            }
        }
    }

    public Task<NoticeRetry> Run()
    {
        lock (_gate)
        {
            _asked++;
            _pending = new TaskCompletionSource<NoticeRetry>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pending.Task;
        }
    }

    /// <summary>Answers the try that waits.</summary>
    /// <exception cref="InvalidOperationException">No try waits.</exception>
    public void Answer(NoticeRetry answer)
    {
        lock (_gate)
        {
            if (_pending is not { Task.IsCompleted: false } pending)
            {
                throw new InvalidOperationException("No try waits for an answer.");
            }

            pending.SetResult(answer);
        }
    }
}
