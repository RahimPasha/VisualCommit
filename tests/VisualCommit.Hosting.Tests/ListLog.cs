using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tests;

/// <summary>A log that keeps its lines in memory, in order, for a test to look at.</summary>
internal sealed class ListLog : IAppLog
{
    private readonly List<(LogLevel Level, string Message)> _lines = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        lock (_lines)
        {
            _lines.Add((level, message));
        }
    }
}
