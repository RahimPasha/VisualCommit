using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A log that keeps its lines in memory, in order, for a test to look at: <see cref="Lines"/> their
/// levels and messages, <see cref="Entries"/> the exceptions they were given too (D380: a test that
/// no secret reaches the log checks everything logged, as the app's log file writes an exception's
/// <c>ToString()</c> after its line).
/// </summary>
internal sealed class ListLog : IAppLog
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Lines
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries.Select(entry => (entry.Level, entry.Message))];
            }
        }
    }

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Everything logged as the app's log file holds it: each message, and after it the exception's <c>ToString()</c> when one was given.</summary>
    public string Written
    {
        get
        {
            lock (_entries)
            {
                return string.Join('\n', _entries.Select(entry => entry.Exception is null ? entry.Message : entry.Message + '\n' + entry.Exception));
            }
        }
    }

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        lock (_entries)
        {
            _entries.Add((level, message, exception));
        }
    }
}
