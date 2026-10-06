using System.Globalization;
using System.Text;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>
/// Writes the log to a local file, one file per day: <c>visualcommit-yyyyMMdd.log</c> in the
/// log folder. Each entry opens the file for itself alone, appends and closes it, so several
/// logs and several instances of the app can share a file without overwriting each other, and
/// nothing is lost when the app stops abruptly.
/// </summary>
public sealed class FileAppLog : IAppLog
{
    public const string FilePrefix = "visualcommit-";

    /// <summary>Log files older than this many days are deleted when a log is created.</summary>
    public const int DaysKept = 14;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>How often a write is retried while another process has the file open.</summary>
    private const int WriteAttempts = 40;

    /// <summary>One lock for all logs of this process: they may share a file.</summary>
    private static readonly Lock Gate = new();

    private readonly string _directory;
    private readonly TimeProvider _clock;

    public FileAppLog(string directory, LogLevel minimumLevel = LogLevel.Debug, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _clock = clock ?? TimeProvider.System;
        MinimumLevel = minimumLevel;
        DeleteOldFiles();
    }

    public LogLevel MinimumLevel { get; }

    /// <summary>The file that entries written now go to.</summary>
    public string CurrentFile => FileFor(_clock.GetLocalNow());

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var now = _clock.GetLocalNow();
        var entry = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(Label(level)).Append("] ")
            .Append(message);
        if (exception is not null)
        {
            entry.AppendLine().Append(exception);
        }

        entry.AppendLine();

        var bytes = Utf8NoBom.GetBytes(entry.ToString());
        var file = FileFor(now);

        lock (Gate)
        {
            for (var attempt = 1; attempt <= WriteAttempts; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(_directory);

                    // No other writer while this one appends: two writers that both seek to the
                    // end would overwrite each other. Readers are still let in.
                    using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
                    stream.Write(bytes);
                    return;
                }
                catch (IOException) when (attempt < WriteAttempts && Directory.Exists(_directory))
                {
                    // Another instance of the app is writing its own entry. Try again shortly.
                    Thread.Sleep(5);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A log that cannot be written must not take the app down with it.
                    return;
                }
            }
        }
    }

    private string FileFor(DateTimeOffset time) =>
        Path.Combine(_directory, FilePrefix + time.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        _ => "ERR",
    };

    private void DeleteOldFiles()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            var oldest = FileFor(_clock.GetLocalNow().AddDays(-DaysKept));
            foreach (var file in Directory.EnumerateFiles(_directory, FilePrefix + "*.log"))
            {
                // The date in the name sorts as text, so an ordinal comparison finds the old files.
                if (string.CompareOrdinal(Path.GetFileName(file), Path.GetFileName(oldest)) < 0)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Clean-up is best effort.
        }
    }
}
