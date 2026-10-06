namespace VisualCommit.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>The app's log. Implementations are safe to call from any thread and never throw.</summary>
public interface IAppLog
{
    void Write(LogLevel level, string message, Exception? exception = null);
}

public static class AppLogExtensions
{
    public static void Debug(this IAppLog log, string message) => log.Write(LogLevel.Debug, message);

    public static void Info(this IAppLog log, string message) => log.Write(LogLevel.Info, message);

    public static void Warning(this IAppLog log, string message, Exception? exception = null) =>
        log.Write(LogLevel.Warning, message, exception);

    public static void Error(this IAppLog log, string message, Exception? exception = null) =>
        log.Write(LogLevel.Error, message, exception);
}

/// <summary>A log that discards everything.</summary>
public sealed class NullAppLog : IAppLog
{
    public static NullAppLog Instance { get; } = new();

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
    }
}
