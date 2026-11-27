using System.ComponentModel;
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The rule of <see cref="RealApp.WaitFor"/>'s looks (<see cref="RealApp.Poll"/>) and which failures of a look count as
/// "not yet" (<see cref="RealApp.IsFailedLook"/>), with no app and no window. Each wait is given a fake clock that a
/// recorded pause moves on by the pause asked for, an exit code that the test sets, and a condition that the test
/// scripts look by look, so that nothing sleeps and nothing depends on timing. Phase 8a's gate met the case on
/// 2026-11-27 (round 0f9099a): phase 3's check 22 failed because one look of a wait, UI Automation's <c>FindAll</c>
/// over the Remotes dialog while it built its rows again, threw a <see cref="COMException"/>. As
/// <see cref="Phase8aOrderTests"/>, the class constructs no fixture of a pass and touches nothing of the desktop; it
/// runs in the whole pass and alone with <c>--filter-class "*WaitForRuleTests"</c>.
/// </summary>
public class WaitForRuleTests
{
    private const string What = "the Remotes dialog to list mirror, origin";

    /// <summary>
    /// The gate's message, "Unexpected HRESULT has been returned from a call to a COM component.", with
    /// UIA_E_ELEMENTNOTAVAILABLE as its HRESULT: the gate's log gives the message alone.
    /// </summary>
    private const string GateMessage = "Unexpected HRESULT has been returned from a call to a COM component.";

    private const int ElementNotAvailable = unchecked((int)0x80040201);

    /// <summary>One second: eleven looks on the fake clock, at 0, 0.1, …, 1.0 s, with ten pauses between them.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A look that fails in UI Automation, a <see cref="COMException"/> as FlaUI hands on a failed <c>FindAll</c>, or
    /// FlaUI's own exception for an element that has gone, counts as "not yet": the wait looks again after its pause
    /// and ends when a later look is met. Without the rule the first failed look ends the wait: the fact fails with
    /// that exception, after one look.
    /// </summary>
    [Theory]
    [InlineData(nameof(COMException))]
    [InlineData(nameof(ElementNotAvailableException))]
    public void A_look_that_fails_in_UI_Automation_counts_as_not_yet_and_the_wait_looks_again(string kind)
    {
        Exception failure = kind == nameof(COMException) ? TreeChanged() : new ElementNotAvailableException("The element is not available.");
        var looks = new ScriptedLooks(() => throw failure, () => throw failure, () => true);
        var time = new FakeTime();

        Wait(looks, time);

        Assert.Equal(3, looks.Count);
        Assert.Equal([RealApp.PollInterval, RealApp.PollInterval], time.Pauses);
    }

    /// <summary>
    /// A wait whose every look fails in UI Automation fails when its time is up, as one whose condition stays false
    /// does, with a <see cref="TimeoutException"/> whose message says that the last look failed, with what (the type,
    /// the HRESULT and the message) and how many of the looks failed; the last failure is its inner exception. Without
    /// the rule a <see cref="COMException"/> leaves at the first look instead of a <see cref="TimeoutException"/>.
    /// </summary>
    [Fact]
    public void A_wait_whose_every_look_fails_times_out_and_names_the_last_failure()
    {
        List<COMException> thrown = [];
        var looks = new ScriptedLooks(() =>
        {
            var failure = TreeChanged();
            thrown.Add(failure);
            throw failure;
        });
        var time = new FakeTime();

        var timedOut = Assert.Throws<TimeoutException>(() => Wait(looks, time));

        Assert.Equal(11, looks.Count);
        Assert.Equal(10, time.Pauses.Count);
        Assert.Equal(
            $"Timed out after 1 s waiting for {What}. The last look failed in UI Automation with COMException (HRESULT 0x80040201) \"{GateMessage}\"; 11 of 11 looks failed.",
            timedOut.Message);
        Assert.Same(thrown[^1], timedOut.InnerException);
    }

    /// <summary>
    /// A wait whose last look read false after looks that failed says so: the last look read false, how many looks
    /// before it failed and with what the last of them did, which is its inner exception. A read that fails every time
    /// is so told from a condition that was simply false. Without the rule the first failed look ends the wait with
    /// its <see cref="COMException"/>.
    /// </summary>
    [Fact]
    public void A_wait_whose_last_look_read_false_after_failed_ones_says_how_many_failed()
    {
        var gone = new ElementNotAvailableException("The element is not available.");
        var looks = new ScriptedLooks(() => throw TreeChanged(), () => throw TreeChanged(), () => throw gone, () => false);
        var time = new FakeTime();

        var timedOut = Assert.Throws<TimeoutException>(() => Wait(looks, time));

        Assert.Equal(11, looks.Count);
        Assert.Equal(
            $"Timed out after 1 s waiting for {What}. The last look read false; 3 of 11 looks before it failed in UI Automation, the last of them with ElementNotAvailableException \"The element is not available.\".",
            timedOut.Message);
        Assert.Same(gone, timedOut.InnerException);
    }

    /// <summary>
    /// A condition that stays false, with no look failing, times out with the message it had before the rule, word
    /// for word, and no inner exception, after looks every <see cref="RealApp.PollInterval"/>. Without the rule it holds
    /// too: it guards what the rule leaves as it was.
    /// </summary>
    [Fact]
    public void A_condition_that_stays_false_times_out_with_the_message_it_had()
    {
        var looks = new ScriptedLooks(() => false);
        var time = new FakeTime();

        var timedOut = Assert.Throws<TimeoutException>(() => Wait(looks, time));

        Assert.Equal($"Timed out after 1 s waiting for {What}.", timedOut.Message);
        Assert.Null(timedOut.InnerException);
        Assert.Equal(11, looks.Count);
        Assert.Equal(10, time.Pauses.Count);
        Assert.All(time.Pauses, pause => Assert.Equal(RealApp.PollInterval, pause));
    }

    /// <summary>
    /// An app that has exited ends the wait at once, with no pause: after a look that failed in UI Automation, as after
    /// a look that read false, with the message it had. Without the rule the failed look's <see cref="COMException"/>
    /// leaves instead of the <see cref="InvalidOperationException"/> that names the exit code; the second half holds
    /// either way.
    /// </summary>
    [Fact]
    public void An_app_that_has_exited_ends_the_wait_at_once_also_after_a_failed_look()
    {
        var failed = new ScriptedLooks(() => throw TreeChanged());
        var time = new FakeTime();

        var exited = Assert.Throws<InvalidOperationException>(() => Wait(failed, time, exitCode: 3));

        Assert.Equal($"The app exited with code 3 while waiting for {What}.", exited.Message);
        Assert.Equal(1, failed.Count);
        Assert.Empty(time.Pauses);

        var unmet = new ScriptedLooks(() => false);
        exited = Assert.Throws<InvalidOperationException>(() => Wait(unmet, time, exitCode: 3));

        Assert.Equal($"The app exited with code 3 while waiting for {What}.", exited.Message);
        Assert.Equal(1, unmet.Count);
        Assert.Empty(time.Pauses);
    }

    /// <summary>
    /// Every other exception of a look leaves the wait at once, the same object, with no pause: <see cref="RealApp.Find"/>'s
    /// <see cref="InvalidOperationException"/> for an element that is not in the window, a <see cref="TimeoutException"/>
    /// of a wait inside the look, the harness's <see cref="Win32Exception"/> for a list of windows that Windows did not
    /// give, an <see cref="ArgumentException"/>, an assertion, an <see cref="IOException"/> and FlaUI's exception for an
    /// element that is not enabled. It holds without the rule too: it guards that the rule takes no more than a failed look.
    /// </summary>
    [Theory]
    [InlineData(nameof(InvalidOperationException))]
    [InlineData(nameof(TimeoutException))]
    [InlineData(nameof(Win32Exception))]
    [InlineData(nameof(ArgumentException))]
    [InlineData(nameof(XunitException))]
    [InlineData(nameof(IOException))]
    [InlineData(nameof(ElementNotEnabledException))]
    public void Another_exception_of_a_look_leaves_the_wait_at_once(string kind)
    {
        var other = Other(kind);
        var looks = new ScriptedLooks(() => throw other);
        var time = new FakeTime();

        var left = Assert.ThrowsAny<Exception>(() => Wait(looks, time));

        Assert.Same(other, left);
        Assert.Equal(1, looks.Count);
        Assert.Empty(time.Pauses);
    }

    /// <summary>
    /// What <see cref="RealApp.IsFailedLook"/> takes as a look that failed while the automation tree changed: a
    /// <see cref="COMException"/> of any HRESULT (FlaUI 5.0.0's <c>FindAll</c>, <c>FindFirst</c> and property reads hand
    /// UI Automation's failure on as one) and FlaUI's <see cref="ElementNotAvailableException"/> (its <c>Com.Call</c>'s
    /// for UIA_E_ELEMENTNOTAVAILABLE); nothing else, not even what <see cref="RealApp.IsReadFailure"/> takes for a single
    /// read: FlaUI's <c>InvalidOperationException</c> and <c>TimeoutException</c> and the <see cref="Win32Exception"/> its
    /// <c>Com.Call</c> makes of any other HRESULT, nor FlaUI's other exceptions. Without the rule no failure counts, and
    /// the fact fails at the first <see cref="COMException"/>.
    /// </summary>
    [Fact]
    public void Only_a_COMException_and_FlaUIs_ElementNotAvailableException_count_as_a_failed_look()
    {
        Exception[] failedLooks =
        [
            TreeChanged(),
            new COMException("Unspecified error", unchecked((int)0x80004005)),
            new COMException("The object invoked has disconnected from its clients.", unchecked((int)0x80010108)),
            new ElementNotAvailableException(new COMException("The element is not available.", ElementNotAvailable)),
        ];
        Exception[] others =
        [
            new InvalidOperationException("No element with the automation id 'RemoteRow' is in the window."),
            new InvalidOperationException("UIA Invalid Operation"),
            new TimeoutException("UIA Timeout"),
            new TimeoutException("Timed out after 10 s waiting for the menu."),
            new Win32Exception(0, GateMessage),
            new ArgumentException("E_INVALIDARG"),
            new IOException("No read through UI Automation."),
            new XunitException("An assertion of the check."),
            new ElementNotEnabledException(),
            new NoClickablePointException(),
            new FlaUI.Core.Exceptions.NotSupportedException(),
        ];

        Assert.All(failedLooks, failure => Assert.True(RealApp.IsFailedLook(failure), $"{failure.GetType().Name} \"{failure.Message}\" does not count as a failed look."));
        Assert.All(others, other => Assert.False(RealApp.IsFailedLook(other), $"{other.GetType().Name} \"{other.Message}\" counts as a failed look."));
    }

    /// <summary>The failure of the gate's look: <see cref="GateMessage"/> with UIA_E_ELEMENTNOTAVAILABLE.</summary>
    private static COMException TreeChanged() => new(GateMessage, ElementNotAvailable);

    /// <summary>One exception of each kind that is no failed look.</summary>
    private static Exception Other(string kind) => kind switch
    {
        nameof(InvalidOperationException) => new InvalidOperationException("No element with the automation id 'RemoteRow' is in the window."),
        nameof(TimeoutException) => new TimeoutException("Timed out after 10 s waiting for the menu."),
        nameof(Win32Exception) => new Win32Exception(1400, "Windows did not list its top-level windows (EnumWindows failed with error 1400)."),
        nameof(ArgumentException) => new ArgumentException("E_INVALIDARG"),
        nameof(XunitException) => new XunitException("An assertion of the check."),
        nameof(IOException) => new IOException("The log could not be read."),
        nameof(ElementNotEnabledException) => new ElementNotEnabledException(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such kind."),
    };

    /// <summary>One wait on <see cref="RealApp.Poll"/> with <paramref name="time"/>'s clock and pause, the app running unless <paramref name="exitCode"/> is given.</summary>
    private static void Wait(ScriptedLooks looks, FakeTime time, int? exitCode = null) =>
        RealApp.Poll(looks.Look, What, Limit, () => time.Now, () => exitCode, time.Pause);

    /// <summary>A wait's clock and pauses: <see cref="Pause"/> records each pause asked for and moves <see cref="Now"/> on by it. Nothing sleeps.</summary>
    private sealed class FakeTime
    {
        public TimeSpan Now { get; private set; }

        public List<TimeSpan> Pauses { get; } = [];

        public void Pause(TimeSpan pause)
        {
            Pauses.Add(pause);
            Now += pause;
        }
    }

    /// <summary>A condition scripted look by look: each look plays the next step, the last step again for every later look; <see cref="Count"/> counts the looks, the failed ones among them.</summary>
    private sealed class ScriptedLooks(params Func<bool>[] steps)
    {
        public int Count { get; private set; }

        public bool Look()
        {
            var step = steps[Math.Min(Count, steps.Length - 1)];
            Count++;
            return step();
        }
    }
}
