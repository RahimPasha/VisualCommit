using System.Collections.Concurrent;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Tests;

/// <summary>A log that keeps its entries in memory for a test to look at.</summary>
internal sealed class RecordingLog : IAppLog
{
    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    /// <summary>The messages written so far, in order.</summary>
    public IReadOnlyList<string> Messages => Entries.Select(entry => entry.Message).ToList();

    public void Write(LogLevel level, string message, Exception? exception = null) =>
        Entries.Enqueue((level, message, exception));
}
