using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// Phase 3's additions to the real-window driver (docs/phase-3-design.md 7.4): clicks with keys
/// held, a drag in separate steps with the button always released, and waiting for the
/// operation queue's line in the app's log. The menu helpers (<c>MenuItemNames()</c>,
/// <c>ClickMenuItem</c>) come with the menu adapter (WP5), in this file.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>How often <see cref="WaitForLogLine"/> reads the log.</summary>
    private static readonly TimeSpan LogPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Whether this driver pressed the real left button and has not released it yet.</summary>
    private bool _buttonHeld;

    /// <summary>
    /// The pattern of the line the operation queue logs when an operation has ended (design 1.7):
    /// <c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt; after &lt;N&gt; ms: …</c>, for
    /// <see cref="WaitForLogLine"/>. The repository and the title are matched as written.
    /// </summary>
    public static Regex OperationEnded(string repository, string title, OperationOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Regex(
            string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(repository)}: {Regex.Escape(title)}: {outcome} after \d+ ms"),
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Moves the real mouse to a point of the client area and clicks the left button while the
    /// keys <paramref name="held"/> are held down, such as <see cref="VirtualKeyShort.CONTROL"/>
    /// or <see cref="VirtualKeyShort.SHIFT"/> for a multi-selection (4h). The keys are released
    /// even when the click fails.
    /// </summary>
    public void ClickAt(float x, float y, params VirtualKeyShort[] held)
    {
        ArgumentNullException.ThrowIfNull(held);
        var point = OnScreen(x, y);
        Mouse.MoveTo(point);
        using (Keyboard.Pressing(held))
        {
            // The app reads the keys with the press: let Windows take them in first.
            Wait.UntilInputIsProcessed();
            Mouse.Click(point);
            Wait.UntilInputIsProcessed();
        }

        WaitUntilIdle();
    }

    /// <summary>
    /// Moves the real mouse to a point of the client area and presses the left button without
    /// releasing it: the start of a drag that the test goes on with <see cref="MoveHeldTo"/>,
    /// captures in the middle, and ends with <see cref="MouseUpAt"/>. Put the steps after it in a
    /// <c>try</c> whose <c>finally</c> calls <see cref="MouseUpAt"/>; should a test still leave the
    /// button held, <see cref="Dispose"/> releases it.
    /// </summary>
    public void MouseDownAt(float x, float y)
    {
        Mouse.MoveTo(OnScreen(x, y));
        Mouse.Down(MouseButton.Left);
        _buttonHeld = true;
        WaitUntilIdle();
    }

    /// <summary>
    /// Moves the real mouse, with the button pressed by <see cref="MouseDownAt"/> held, from where
    /// it is to a point of the client area in <paramref name="steps"/> steps, so that the app sees
    /// a movement and not a jump. When a move fails the button is released before the error
    /// goes on, so it is never left held down.
    /// </summary>
    public void MoveHeldTo(float x, float y, int steps = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        if (!_buttonHeld)
        {
            throw new InvalidOperationException("No button is held: call MouseDownAt first.");
        }

        try
        {
            var from = Mouse.Position;
            var to = OnScreen(x, y);
            for (var step = 1; step <= steps; step++)
            {
                Thread.Sleep(DragStepInterval);
                Mouse.MoveTo(new Point(
                    from.X + ((to.X - from.X) * step / steps),
                    from.Y + ((to.Y - from.Y) * step / steps)));
            }

            WaitUntilIdle();
        }
        catch
        {
            ReleaseHeldButton();
            throw;
        }
    }

    /// <summary>
    /// Moves the real mouse to a point of the client area and releases the left button there: the
    /// drop that ends <see cref="MouseDownAt"/>. The button is released even when the move fails.
    /// </summary>
    public void MouseUpAt(float x, float y)
    {
        if (!_buttonHeld)
        {
            throw new InvalidOperationException("No button is held: call MouseDownAt first.");
        }

        try
        {
            Mouse.MoveTo(OnScreen(x, y));
        }
        finally
        {
            ReleaseHeldButton();
        }

        WaitUntilIdle();
    }

    /// <summary>How many lines of the app's log match <paramref name="pattern"/> now: what a test reads before an operation, to wait for the next match after it.</summary>
    public int CountLogLines(Regex pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return LogLines().Count(line => pattern.IsMatch(line));
    }

    /// <summary>
    /// Waits until the app's log holds <paramref name="occurrence"/> lines that match
    /// <paramref name="pattern"/> (<see cref="OperationEnded"/> gives the queue's
    /// <c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt;</c> lines, design 1.7) and returns the
    /// last of them. For the second fetch of a run, pass
    /// <c>CountLogLines(pattern) + 1</c> read before it.
    /// </summary>
    /// <exception cref="TimeoutException">The line did not come within <paramref name="timeout"/>.</exception>
    /// <exception cref="InvalidOperationException">The app exited while the test waited.</exception>
    public string WaitForLogLine(Regex pattern, TimeSpan timeout, int occurrence = 1)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var matches = LogLines().Where(line => pattern.IsMatch(line)).ToList();
            if (matches.Count >= occurrence)
            {
                return matches[occurrence - 1];
            }

            if (_process.HasExited)
            {
                throw new InvalidOperationException($"The app exited with code {_process.ExitCode} while waiting for a log line like /{pattern}/.");
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out after {timeout.TotalSeconds:0.###} s waiting for log line {occurrence} like /{pattern}/; {matches.Count} came."));
            }

            Thread.Sleep(LogPollInterval);
        }
    }

    /// <summary>The app's log so far, one entry per line (an exception's stack follows its entry on lines of its own).</summary>
    private IEnumerable<string> LogLines() =>
        ReadLog(DataDirectory).Split('\n').Select(line => line.TrimEnd('\r'));

    /// <summary>Releases the left button if this driver holds it.</summary>
    private void ReleaseHeldButton()
    {
        if (!_buttonHeld)
        {
            return;
        }

        try
        {
            Mouse.Up(MouseButton.Left);
        }
        catch (Exception ex)
        {
            Problems.Enqueue($"Releasing the mouse button failed: {ex.Message}");
        }
        finally
        {
            _buttonHeld = false;
        }
    }
}
