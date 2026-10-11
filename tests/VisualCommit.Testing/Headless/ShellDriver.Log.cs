using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// What the app wrote down, for phase 3's checks: its log (the lines the real-window pass reads
/// too, such as <c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt; after &lt;N&gt; ms: …</c> and
/// <c>Context menu opened: … by pointer</c>), its record of git calls (for the error dialog's
/// <c>ErrorOutput</c>, which is git's own output cleaned, design 1.6), and the time each step of a
/// check took (design 7.7's time budget).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The time since the driver started the app: the clock of <see cref="MarkStep"/>.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly List<StepTime> _steps = [];

    /// <summary>The app's log so far, one entry per line (Debug lines included), read from the session's log folder.</summary>
    public IReadOnlyList<string> LogLines() => AppLogFile.ReadLines(Session.Paths.DataDirectory);

    /// <summary>How many lines the log has now: a mark to read the lines added after an input (<see cref="LogLinesSince"/>).</summary>
    public int LogMark() => LogLines().Count;

    /// <summary>The lines added to the log since <paramref name="mark"/> (<see cref="LogMark"/>).</summary>
    public IReadOnlyList<string> LogLinesSince(int mark)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mark);
        return [.. LogLines().Skip(mark)];
    }

    /// <summary>How many lines of the log match <paramref name="pattern"/> now (since <paramref name="mark"/> when given).</summary>
    public int CountLogLines(Regex pattern, int mark = 0)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return LogLinesSince(mark).Count(line => pattern.IsMatch(line));
    }

    /// <summary>
    /// Waits until the log holds <paramref name="occurrence"/> lines that match
    /// <paramref name="pattern"/> after <paramref name="mark"/> (the start when 0), and returns the
    /// last of them. <see cref="AppLogFile.OperationEnded"/> and <see cref="AppLogFile.MenuOpened"/>
    /// give the patterns the checks wait for. The wait polls, letting the UI thread run.
    /// </summary>
    /// <exception cref="TimeoutException">The line did not come within <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null).</exception>
    public async Task<string> WaitForLogLineAsync(Regex pattern, int occurrence = 1, int mark = 0, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);
        List<string> matches = [];
        await WaitForAsync(
            () => (matches = [.. LogLinesSince(mark).Where(line => pattern.IsMatch(line))]).Count >= occurrence,
            string.Create(CultureInfo.InvariantCulture, $"log line {occurrence} like /{pattern}/"),
            timeout);
        return matches[occurrence - 1];
    }

    /// <summary>Every git call this app session made so far, oldest first (the session's <see cref="IGitCallLog"/>).</summary>
    public IReadOnlyList<GitCallRecord> GitCalls() => Session.GitCalls.Snapshot();

    /// <summary>
    /// The latest git call whose command line, as a user would type it, is
    /// <paramref name="commandText"/> (<see cref="GitCallRecord.CommandText"/>, the text the error
    /// dialog's <c>ErrorCommand</c> shows).
    /// </summary>
    /// <exception cref="InvalidOperationException">No such call was made.</exception>
    public GitCallRecord LastCall(string commandText)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandText);
        var calls = GitCalls();
        return calls.LastOrDefault(call => call.CommandText == commandText)
            ?? throw new InvalidOperationException(
                $"No git call \"{commandText}\" was made. The last calls were:\n" + string.Join('\n', calls.TakeLast(15).Select(call => call.CommandText)));
    }

    /// <summary>
    /// What the error dialog's <c>ErrorOutput</c> shows for a failed call (design 1.6):
    /// <see cref="GitOutputCleaner.Clean"/> of its standard error, or of its standard output when
    /// nothing is left of the error. On another git than the development machine's the checks
    /// assert this rather than the report's quoted lines (the conventions' "Git's own texts").
    /// </summary>
    public static string CleanedOutputOf(GitCallRecord call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return GitOutputCleaner.Clean(call.StandardError, call.StandardOutput);
    }

    /// <summary>
    /// Where <paramref name="text"/> appears in what the app wrote down: each log line and each
    /// part of a git call record (command, standard output, standard error) that holds it. Empty
    /// when it appears nowhere: what checks 21, 21b and 22 assert of a password, an answer or a
    /// token.
    /// </summary>
    public IReadOnlyList<string> WhereWritten(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var found = new List<string>();
        found.AddRange(LogLines().Where(line => line.Contains(text, StringComparison.Ordinal)).Select(line => "log: " + line));
        foreach (var call in GitCalls())
        {
            if (call.CommandText.Contains(text, StringComparison.Ordinal))
            {
                found.Add("git call: " + call.CommandText);
            }

            if (call.StandardOutput.Contains(text, StringComparison.Ordinal))
            {
                found.Add("standard output of: " + call.CommandText);
            }

            if (call.StandardError.Contains(text, StringComparison.Ordinal))
            {
                found.Add("standard error of: " + call.CommandText);
            }
        }

        return found;
    }

    /// <summary>The time since this driver started the app.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    /// <summary>The steps marked so far (<see cref="MarkStep"/>), each with the time since the app started.</summary>
    public IReadOnlyList<StepTime> StepTimes => [.. _steps];

    /// <summary>
    /// Notes that the step <paramref name="name"/> (a screenshot's name, for example) is done now,
    /// with the time since the app started and since the step before: what design 7.7's time
    /// budget is measured with. Returns the record.
    /// </summary>
    public StepTime MarkStep(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var at = _clock.Elapsed;
        var step = new StepTime(name, at, at - (_steps.Count > 0 ? _steps[^1].At : TimeSpan.Zero));
        _steps.Add(step);
        return step;
    }
}

/// <summary>One step of a check as <see cref="ShellDriver.MarkStep"/> noted it.</summary>
/// <param name="Name">The step: a screenshot's name, for example.</param>
/// <param name="At">The time since the app started.</param>
/// <param name="Took">The time since the step before (or since the start, for the first).</param>
public sealed record StepTime(string Name, TimeSpan At, TimeSpan Took)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{At.TotalMilliseconds,8:F0} ms  (+{Took.TotalMilliseconds,6:F0} ms)  {Name}");
}
