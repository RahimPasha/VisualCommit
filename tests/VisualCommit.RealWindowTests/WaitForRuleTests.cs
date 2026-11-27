using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;
using Xunit;
using Xunit.Sdk;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The rule of <see cref="RealApp.WaitFor"/>'s looks (<see cref="RealApp.Poll"/>), which failures of a look count as
/// "not yet" (<see cref="RealApp.IsFailedLook"/>), how a time-out's message reads the window after the wait
/// (<see cref="RealApp.StateForMessage"/>), <see cref="RealApp.WaitForMenu"/>'s rule for a settled menu across a failed look
/// (<see cref="RealApp.SettlingRectangle"/>) and a typed token in a time-out's read (<see cref="RealApp.WithoutToken"/>), with
/// no app and no window. Each wait is given a fake clock that a recorded pause moves on by the pause asked for, an exit
/// code that the test sets, and a condition that the test scripts look by look, so that nothing sleeps and nothing depends
/// on timing. Phase 8a's gate met the case on 2026-11-27 (round 0f9099a): phase 3's check 22 failed because one look of a
/// wait, UI Automation's <c>FindAll</c> over the Remotes dialog while it built its rows again, threw a
/// <see cref="COMException"/>. As <see cref="Phase8aOrderTests"/>, the class constructs no fixture of a pass and touches
/// nothing of the desktop; it runs in the whole pass and alone with <c>--filter-class "*WaitForRuleTests"</c>.
/// </summary>
public class WaitForRuleTests
{
    private const string What = "the Remotes dialog to list mirror, origin";

    /// <summary>
    /// The gate's message, which its log gives without the HRESULT: .NET 10's words for E_FAIL (<see cref="Unspecified"/>)
    /// when the failed call leaves no error information, as <c>Marshal.GetExceptionForHR(0x80004005, -1)</c> and
    /// <c>new COMException()</c> give it (read on .NET 10.0.15). .NET words every other HRESULT with the system's text and
    /// the code (<see cref="ElementNotAvailableWords"/>), so the gate's <c>FindAll</c> failed with E_FAIL.
    /// </summary>
    private const string GateMessage = "Unexpected HRESULT has been returned from a call to a COM component.";

    /// <summary>E_FAIL, the HRESULT of the gate's look.</summary>
    private const int Unspecified = unchecked((int)0x80004005);

    /// <summary>UIA_E_ELEMENTNOTAVAILABLE, how a raw call into UI Automation fails on an element that has gone.</summary>
    private const int ElementNotAvailable = unchecked((int)0x80040201);

    /// <summary>
    /// .NET 10's words for <see cref="ElementNotAvailable"/> with no error information: the system's text for the same
    /// HRESULT (EVENT_E_ALL_SUBSCRIBERS_FAILED) and the code.
    /// </summary>
    private const string ElementNotAvailableWords = "An event was unable to invoke any of the subscribers (0x80040201)";

    /// <summary>The words FlaUI 5.0.0 gives a <see cref="PatternNotSupportedException"/> for the Value pattern, given here so that no test depends on FlaUI's own.</summary>
    private const string PatternReadWords = "The requested pattern 'Value [#10002]' is not supported";

    /// <summary>One second: eleven looks on the fake clock, at 0, 0.1, …, 1.0 s, with ten pauses between them.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);

    /// <summary>UI Automation's Value pattern (UIA_ValuePatternId), made here: FlaUI's constructor only keeps the id and the name.</summary>
    private static readonly FlaUI.Core.Identifiers.PatternId ValuePattern = new(10002, "Value", null!);

    /// <summary>A context menu's rectangle as <see cref="RealApp.WaitForMenu"/> reads it.</summary>
    private static readonly Rectangle Menu = new(640, 360, 220, 180);

    /// <summary><see cref="Menu"/> placed again, a little lower: a menu that has not settled.</summary>
    private static readonly Rectangle Moved = new(640, 372, 220, 180);

    /// <summary>
    /// A look that fails in UI Automation, a <see cref="COMException"/> as FlaUI hands on a failed <c>FindAll</c>, FlaUI's
    /// own exception for an element that has gone, or its exception for a pattern read that failed in UI Automation,
    /// counts as "not yet": the wait looks again after its pause and ends when a later look is met. Without the rule the
    /// first failed look ends the wait: the fact fails with that exception, after one look.
    /// </summary>
    [Theory]
    [InlineData(nameof(COMException))]
    [InlineData(nameof(ElementNotAvailableException))]
    [InlineData(nameof(PatternNotSupportedException))]
    public void A_look_that_fails_in_UI_Automation_counts_as_not_yet_and_the_wait_looks_again(string kind)
    {
        Exception failure = kind switch
        {
            nameof(COMException) => TreeChanged(),
            nameof(ElementNotAvailableException) => new ElementNotAvailableException("The element is not available."),
            nameof(PatternNotSupportedException) => PatternReadFailed(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such kind."),
        };
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
            $"Timed out after 1 s waiting for {What}. The last look failed in UI Automation with COMException (HRESULT 0x80004005) \"{GateMessage}\"; 11 of 11 looks failed.",
            timedOut.Message);
        Assert.Same(thrown[^1], timedOut.InnerException);
    }

    /// <summary>
    /// A wait whose last look read false after looks that failed says so: the last look read false, how many of all the
    /// looks failed, as the message of a wait whose last look failed counts them, and with what the last failed one did,
    /// which is its inner exception. A read that fails every time is so told from a condition that was simply false.
    /// Without the count of all the looks, the message counts the look that read false among the looks before it.
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
            $"Timed out after 1 s waiting for {What}. The last look read false; 3 of 11 looks failed in UI Automation, the last of them with ElementNotAvailableException \"The element is not available.\".",
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
    /// give, an <see cref="ArgumentException"/>, an assertion, an <see cref="IOException"/>, FlaUI's exception for an
    /// element that is not enabled and its exception for a pattern that the element does not give. It holds without the
    /// rule too: it guards that the rule takes no more than a failed look.
    /// </summary>
    [Theory]
    [InlineData(nameof(InvalidOperationException))]
    [InlineData(nameof(TimeoutException))]
    [InlineData(nameof(Win32Exception))]
    [InlineData(nameof(ArgumentException))]
    [InlineData(nameof(XunitException))]
    [InlineData(nameof(IOException))]
    [InlineData(nameof(ElementNotEnabledException))]
    [InlineData(nameof(PatternNotSupportedException))]
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
    /// UI Automation's failure on as one: the gate's E_FAIL, UIA_E_ELEMENTNOTAVAILABLE, a disconnected provider, and a raw
    /// call's time-out, COR_E_TIMEOUT, which .NET 10 hands on as a <see cref="COMException"/> too), FlaUI's
    /// <see cref="ElementNotAvailableException"/> (its <c>Com.Call</c>'s for UIA_E_ELEMENTNOTAVAILABLE) and FlaUI's
    /// <see cref="PatternNotSupportedException"/> around a <see cref="COMException"/> (a pattern's <c>Pattern</c> wraps any
    /// failure of the read so); nothing else, not even what <see cref="RealApp.IsReadFailure"/> takes for a single read:
    /// FlaUI's <c>InvalidOperationException</c> and <c>TimeoutException</c> and the <see cref="Win32Exception"/> its
    /// <c>Com.Call</c> makes of any other HRESULT, a <see cref="PatternNotSupportedException"/> for a pattern the element
    /// does not give (around FlaUI's <c>InvalidOperationException</c>, or around nothing), nor FlaUI's other exceptions,
    /// also those that <c>Com.Call</c> makes around a <see cref="COMException"/>. Without the rule for a pattern read the
    /// fact fails at the <see cref="PatternNotSupportedException"/> around a <see cref="COMException"/>.
    /// </summary>
    [Fact]
    public void Only_a_COMException_and_FlaUIs_exceptions_for_a_failed_read_count_as_a_failed_look()
    {
        Exception[] failedLooks =
        [
            TreeChanged(),
            new COMException(ElementNotAvailableWords, ElementNotAvailable),
            new COMException("The object invoked has disconnected from its clients. (0x80010108 (RPC_E_DISCONNECTED))", unchecked((int)0x80010108)),
            new COMException("Operation timed out. (0x80131505)", unchecked((int)0x80131505)),
            new ElementNotAvailableException(new COMException(ElementNotAvailableWords, ElementNotAvailable)),
            PatternReadFailed(),
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
            new ElementNotEnabledException(new COMException("0x80040200", unchecked((int)0x80040200))),
            new NoClickablePointException(),
            new FlaUI.Core.Exceptions.NotSupportedException(),
            new FlaUI.Core.Exceptions.NotSupportedException(new COMException("0x80040204", unchecked((int)0x80040204))),
            PatternNotGiven(),
            new PatternNotSupportedException(),
        ];

        Assert.All(failedLooks, failure => Assert.True(RealApp.IsFailedLook(failure), $"{failure.GetType().Name} \"{failure.Message}\" around {failure.InnerException?.GetType().Name ?? "nothing"} does not count as a failed look."));
        Assert.All(others, other => Assert.False(RealApp.IsFailedLook(other), $"{other.GetType().Name} \"{other.Message}\" around {other.InnerException?.GetType().Name ?? "nothing"} counts as a failed look."));
    }

    /// <summary>
    /// What a time-out's message reads of the window after the wait (<see cref="RealApp.StateForMessage"/>, which
    /// <c>WaitForTelling</c> and the passes' <c>WaitTelling</c> use): the read's words when it works; when it fails as a
    /// read through UI Automation fails (<see cref="RealApp.IsReadFailure"/>: UI Automation's COMException, or
    /// <see cref="RealApp.Find"/>'s <see cref="InvalidOperationException"/> for an element that has gone), words that say
    /// so and with what, so that the check reports the time-out and what <see cref="RealApp.Poll"/> says of its looks,
    /// not that read's exception; any other exception leaves. Before the helper such a read's exception replaced the
    /// time-out.
    /// </summary>
    [Fact]
    public void A_time_out_message_says_when_its_read_of_the_window_fails()
    {
        Assert.Equal("It says \"Ready\".", RealApp.StateForMessage(() => "It says \"Ready\"."));
        Assert.Equal(
            $"What the window shows could not be read for this message: COMException (HRESULT 0x80004005) \"{GateMessage}\".",
            RealApp.StateForMessage(() => throw TreeChanged()));
        Assert.Equal(
            "What the window shows could not be read for this message: InvalidOperationException \"No element with the automation id 'OperationStatus' is in the window.\".",
            RealApp.StateForMessage(() => throw new InvalidOperationException("No element with the automation id 'OperationStatus' is in the window.")));

        var other = new IOException("The log could not be read.");
        Assert.Same(other, Assert.Throws<IOException>(() => RealApp.StateForMessage(() => throw other)));
    }

    /// <summary>
    /// A failure that FlaUI wraps, its pattern read's <see cref="PatternNotSupportedException"/> or <c>Com.Call</c>'s
    /// <see cref="ElementNotAvailableException"/> (whose message FlaUI 5.0.0 leaves empty), is named with the
    /// <see cref="COMException"/> inside it, its HRESULT and its message, in a time-out's message as in a message's read of
    /// the window; a wrapper's empty message is left out. So the time-out of a pattern read that kept failing does not say
    /// only "is not supported". Without the rule for a pattern read the <see cref="PatternNotSupportedException"/> leaves
    /// at the first look; without the words the COMException inside is not named.
    /// </summary>
    [Fact]
    public void A_failure_that_FlaUI_wraps_is_named_with_the_COMException_inside()
    {
        var looks = new ScriptedLooks(() => throw PatternReadFailed());
        var time = new FakeTime();

        var timedOut = Assert.Throws<TimeoutException>(() => Wait(looks, time));

        Assert.Equal(
            $"Timed out after 1 s waiting for {What}. The last look failed in UI Automation with PatternNotSupportedException \"{PatternReadWords}\", which wraps COMException (HRESULT 0x80004005) \"{GateMessage}\"; 11 of 11 looks failed.",
            timedOut.Message);
        Assert.Equal(
            $"What the window shows could not be read for this message: ElementNotAvailableException, which wraps COMException (HRESULT 0x80040201) \"{ElementNotAvailableWords}\".",
            RealApp.StateForMessage(() => throw new ElementNotAvailableException(new COMException(ElementNotAvailableWords, ElementNotAvailable))));
    }

    /// <summary>
    /// <see cref="RealApp.WaitForMenu"/>'s rule across a failed look (<see cref="RealApp.SettlingRectangle"/>): a look that
    /// fails in UI Automation between two that read the same rectangle, which <see cref="RealApp.Poll"/> takes as "not
    /// yet", leaves no rectangle, so the two are not two looks in a row and the menu settles a look later, on the fourth.
    /// Without the rule the rectangle of the look before the failed one stays, and the menu settles on the third.
    /// </summary>
    [Fact]
    public void A_look_that_fails_between_two_of_the_same_menu_does_not_settle_it()
    {
        var settling = new RealApp.SettlingRectangle();
        var looks = new ScriptedLooks(
            () => settling.Look(() => Menu),
            () => settling.Look(() => throw TreeChanged()),
            () => settling.Look(() => Menu));
        var time = new FakeTime();

        Wait(looks, time);

        Assert.Equal(4, looks.Count);
        Assert.Equal(3, time.Pauses.Count);
    }

    /// <summary>
    /// <see cref="RealApp.WaitForMenu"/>'s rule with no failed look (<see cref="RealApp.SettlingRectangle"/>): a menu settles
    /// on the same rectangle in two looks in a row, not on the first look, not across a look that found no menu, and not
    /// while it moves. It holds without the rule for a failed look too: it guards what that rule leaves as it was.
    /// </summary>
    [Fact]
    public void A_menu_settles_on_the_same_rectangle_in_two_looks_in_a_row()
    {
        Assert.Equal(2, LooksToSettle(() => Menu));
        Assert.Equal(4, LooksToSettle(() => Menu, () => null, () => Menu));
        Assert.Equal(3, LooksToSettle(() => Menu, () => Moved));
        Assert.Equal(3, LooksToSettle(() => null, () => Menu));
    }

    /// <summary>
    /// A typed token in what a time-out's message reads of the window fails the check by its place (phase 7a's design 13.2,
    /// <see cref="RealApp.WithoutToken"/>, which <c>ConfirmSignIn</c>'s time-out reads through
    /// <see cref="RealApp.StateForMessage"/>): the place's words, never the token, and not taken as a read that failed.
    /// Text without the token is given as it is. Without the rule <see cref="RealApp.StateForMessage"/> takes the leak as a
    /// window it could not read, and no exception leaves.
    /// </summary>
    [Fact]
    public void A_typed_token_in_a_time_outs_read_of_the_window_fails_by_its_place()
    {
        const string token = "made-up-token-0123456789";
        Assert.Equal("Bad credentials.", RealApp.WithoutToken("Bad credentials.", token, "The dialog's error or note"));

        var leak = Assert.Throws<XunitException>(() => RealApp.StateForMessage(() => $"It still shows: {RealApp.WithoutToken($"Bad credentials for {token}.", token, "The dialog's error or note")}."));

        Assert.Equal("The dialog's error or note holds the typed token.", leak.Message);
    }

    /// <summary>The failure of the gate's look: <see cref="GateMessage"/> with E_FAIL.</summary>
    private static COMException TreeChanged() => new(GateMessage, Unspecified);

    /// <summary>A read of the Value pattern that failed in UI Automation, as FlaUI's <c>Pattern</c> wraps it: around <see cref="TreeChanged"/>.</summary>
    private static PatternNotSupportedException PatternReadFailed() => new(PatternReadWords, ValuePattern, TreeChanged());

    /// <summary>The Value pattern on an element that does not give it, as FlaUI's <c>Pattern</c> says so: around its own <see cref="InvalidOperationException"/>.</summary>
    private static PatternNotSupportedException PatternNotGiven() => new(PatternReadWords, ValuePattern, new InvalidOperationException("Native pattern is null"));

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
        nameof(PatternNotSupportedException) => PatternNotGiven(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such kind."),
    };

    /// <summary>One wait on <see cref="RealApp.Poll"/> with <paramref name="time"/>'s clock and pause, the app running unless <paramref name="exitCode"/> is given.</summary>
    private static void Wait(ScriptedLooks looks, FakeTime time, int? exitCode = null) =>
        RealApp.Poll(looks.Look, What, Limit, () => time.Now, () => exitCode, time.Pause);

    /// <summary>How many looks a wait on <see cref="RealApp.WaitForMenu"/>'s rule takes to settle, the menu's rectangle read as <paramref name="reads"/> script it look by look (the last again for every later look).</summary>
    private static int LooksToSettle(params Func<Rectangle?>[] reads)
    {
        var settling = new RealApp.SettlingRectangle();
        var looks = new ScriptedLooks([.. reads.Select(read => (Func<bool>)(() => settling.Look(read)))]);
        Wait(looks, new FakeTime());
        return looks.Count;
    }

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
