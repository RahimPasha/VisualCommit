using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git;

/// <summary>
/// Starts the git executable, streams its output and records every call. See
/// <see cref="IGitRunner"/> for the contract.
/// </summary>
public sealed class GitRunner : IGitRunner
{
    /// <summary>
    /// How long, once git itself has exited, the output pumps may go without progress before they
    /// are given up. Progress is a chunk of output read or a line handler still at work: a slow
    /// handler, or a pump that is still working through what git wrote, is waited for. Only a
    /// process that git left behind (a hook's background job, a daemon) and that keeps the pipes
    /// open without writing is given up, this long after the last progress.
    /// </summary>
    private static readonly TimeSpan PipeDrainTimeout = TimeSpan.FromSeconds(2);

    /// <summary>How long a cancelled call waits for git to be gone before it returns without it.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Applied to every call. English messages keep git's output parseable and the same on every
    /// machine: <c>LANGUAGE</c> too, which gettext puts before <c>LC_ALL</c> (a German desktop sets
    /// <c>de_DE:de</c>); there is no English catalogue, so git keeps its own text. With prompts
    /// off, git fails instead of waiting on a terminal that is not there.
    /// The editor is git's shell's no-op command, so a command that wants an editor goes on
    /// without one instead of opening the user's editor out of sight (D68); so is the editor of a
    /// rebase's list of commits, which a user's <c>sequence.editor</c> would otherwise open even
    /// with <c>GIT_EDITOR</c> set (D102). Paths are taken literally, so a file named
    /// <c>[id].tsx</c> means that file and not <c>d.tsx</c> too (D74).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?> DefaultEnvironment = new Dictionary<string, string?>
    {
        ["LC_ALL"] = "en_US.UTF-8",
        ["LANG"] = "en_US.UTF-8",
        ["LANGUAGE"] = "en",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_EDITOR"] = ":",
        ["GIT_SEQUENCE_EDITOR"] = ":",
        ["GIT_LITERAL_PATHSPECS"] = "1",
    };

    private readonly IGitCallLog? _callLog;
    private readonly IAppLog _log;
    private readonly TimeProvider _recordClock;

    /// <param name="executablePath">The git executable.</param>
    /// <param name="callLog">Where every call is recorded; null records none.</param>
    /// <param name="log">The app's log, which gets one line per call.</param>
    /// <param name="recordClock">
    /// The clock of the times the activity log shows (<see cref="GitCallRecord.DisplayedStart"/> and
    /// its duration, docs/phase-5-design.md 2.3); the system clock by default. The record's own
    /// <see cref="GitCallRecord.StartedAt"/> and <see cref="GitCallRecord.Duration"/> stay the system's.
    /// </param>
    /// <param name="environment">
    /// The app session's own git environment (docs/phase-6-design.md 8.12, 17.2, D220): the
    /// harnesses' <c>GIT_CONFIG_GLOBAL</c> and <c>XDG_CONFIG_HOME</c> in the session's data folder;
    /// null for none, as the desktop app runs. Work package WP-S applies it to every call, after
    /// <see cref="DefaultEnvironment"/> and before the command's own.
    /// </param>
    public GitRunner(string executablePath, IGitCallLog? callLog = null, IAppLog? log = null, TimeProvider? recordClock = null, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ExecutablePath = executablePath;
        _callLog = callLog;
        _log = log ?? NullAppLog.Instance;
        _recordClock = recordClock ?? TimeProvider.System;
        SessionEnvironment = environment ?? new Dictionary<string, string?>();
    }

    public string ExecutablePath { get; }

    /// <summary>
    /// The session's git environment given to the constructor (empty for none): what the settings
    /// screen's global file and its identity notices read over the process's own variables
    /// (docs/phase-6-design.md 8.2, 8.6, 8.12).
    /// </summary>
    public IReadOnlyDictionary<string, string?> SessionEnvironment { get; }

    /// <remarks>
    /// The caller's thread only queues the call; all of it runs on the thread pool (D59). Run from
    /// the UI thread, the start of a process held that thread for 300 ms while the window drew its
    /// first frame, which delayed the history of a repository opened at start-up by as much.
    /// </remarks>
    public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default) =>
        Task.Run(() => RunHereAsync(command, cancellationToken), CancellationToken.None);

    private async Task<GitResult> RunHereAsync(GitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // The number that orders the call in the activity log, taken as it starts (2.3).
        var startedAt = new CallStart(DateTimeOffset.Now, RecordSequence.Next(), _recordClock.GetUtcNow(), _recordClock.GetTimestamp());
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = BuildStartInfo(command) };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Record(command, startedAt, stopwatch.Elapsed, GitCallOutcome.FailedToStart, null, string.Empty, ex.Message);
            throw new GitException(command.DisplayText, null, ex.Message, ex);
        }

        using var job = OperatingSystem.IsWindows() ? WindowsJob.TryAttach(process) : null;
        void Stop() => Kill(process, job);

        var output = new OutputCollector(command.OnOutputLine, keepAll: command.OnOutputLine is null, splitOnCarriageReturn: false);
        var error = new OutputCollector(command.OnErrorLine, keepAll: true, splitOnCarriageReturn: true);
        var outputPump = StartPump(output, process.StandardOutput);
        var errorPump = StartPump(error, process.StandardError);
        var pumps = Task.WhenAll(outputPump, errorPump);
        var exit = process.WaitForExitAsync(CancellationToken.None);

        // Cancelling stops git, which ends the wait below. Should git not stop, the wait is given
        // up after StopTimeout: a call that was cancelled must return, whatever git does. A
        // cancel that comes after git has exited stops nothing (stopped stays false), so the call
        // that finished keeps its result.
        var gaveUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = false;
        using var stopOnCancel = cancellationToken.Register(() =>
        {
            Volatile.Write(ref stopped, !exit.IsCompleted);
            Stop();
            _ = Task.Delay(StopTimeout, CancellationToken.None)
                .ContinueWith(_ => gaveUp.TrySetResult(), TaskScheduler.Default);
        });

        try
        {
            await WriteInputAsync(process, command.StandardInput).ConfigureAwait(false);

            // Wait for git to exit. A pump that fails before that means an output handler
            // threw: awaiting it rethrows the exception at once, without waiting for git.
            var pending = new List<Task> { exit, gaveUp.Task, outputPump, errorPump };
            while (!exit.IsCompleted && !gaveUp.Task.IsCompleted)
            {
                var finished = await Task.WhenAny(pending).ConfigureAwait(false);
                await finished.ConfigureAwait(false);
                pending.Remove(finished);
            }

            if (exit.IsCompleted)
            {
                await DrainAsync(outputPump, errorPump, output, error, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Abandon(pumps, output, error);
                _log.Warning($"{command.DisplayText} was cancelled but did not stop within {StopTimeout.TotalSeconds:F0} seconds. It is left behind.");
            }
        }
        catch (Exception ex)
        {
            // An output handler threw, or a pipe broke. Do not leave git running behind us.
            Stop();
            await Task.WhenAny(exit, Task.Delay(StopTimeout)).ConfigureAwait(false);

            // Only the record still needs the output, so it is not waited for beyond the fixed time.
            await DrainAsync(Settled(outputPump), Settled(errorPump), output, error, new CancellationToken(canceled: true)).ConfigureAwait(false);
            Record(command, startedAt, stopwatch.Elapsed, GitCallOutcome.Cancelled, null, output.Head, error.Head);
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The git call was cancelled.", ex, cancellationToken);
            }

            throw;
        }

        var duration = stopwatch.Elapsed;

        // A cancel that came after git had exited by itself (or so close before that git still
        // exited with success) and after its output was read in full stopped nothing: the call
        // keeps its result, so a push that went through is not reported as cancelled.
        var endedByItself = exit.IsCompleted && pumps.IsCompletedSuccessfully && (!Volatile.Read(ref stopped) || process.ExitCode == 0);
        if (cancellationToken.IsCancellationRequested && !endedByItself)
        {
            Record(command, startedAt, duration, GitCallOutcome.Cancelled, null, output.Head, error.Head);
            throw new OperationCanceledException(cancellationToken);
        }

        Record(command, startedAt, duration, GitCallOutcome.Completed, process.ExitCode, output.Head, error.Head);
        return new GitResult(process.ExitCode, output.All, error.All, duration);
    }

    private ProcessStartInfo BuildStartInfo(GitCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = command.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = command.Encoding ?? Utf8NoBom,
            StandardOutputEncoding = command.Encoding ?? Utf8NoBom,

            // Git's messages are text whatever the command's content is.
            StandardErrorEncoding = Utf8NoBom,
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in DefaultEnvironment.Concat(command.Environment))
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        return startInfo;
    }

    /// <summary>
    /// Reads one of git's output streams on a thread of its own, which also runs its line handler
    /// (D59). On Windows the pipes are synchronous, so a pending read holds a thread for as long as
    /// git is silent: on the thread pool, the reads of the calls that opening a repository starts
    /// together would use up its threads and hold up the history's first page.
    /// </summary>
    private static Task StartPump(OutputCollector collector, StreamReader reader) =>
        Task.Factory.StartNew(
            () => collector.Pump(reader),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static async Task WriteInputAsync(Process process, string? input)
    {
        try
        {
            if (!string.IsNullOrEmpty(input))
            {
                await process.StandardInput.WriteAsync(input).ConfigureAwait(false);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // Git exited without reading its input. Its exit code and error text tell the story.
        }
    }

    /// <summary>
    /// Waits for the output pumps once git has exited, for as long as they make progress: a chunk
    /// was read, or a line handler is at work. When <see cref="PipeDrainTimeout"/> has passed
    /// since git exited and since the last progress, the pumps are left to end on their own and
    /// what they read later is dropped: only a process git left behind holds the pipes open that
    /// long without writing.
    /// <para>
    /// Once <paramref name="cancellationToken"/> is cancelled, the call's output is no longer
    /// wanted: progress no longer counts, and the pumps are given up when
    /// <see cref="PipeDrainTimeout"/> has passed since git exited, so that a cancelled call
    /// returns soon whatever its handlers do (D36). A pump that fails ends the wait with its
    /// exception.
    /// </para>
    /// </summary>
    private async Task DrainAsync(Task outputPump, Task errorPump, OutputCollector output, OutputCollector error, CancellationToken cancellationToken)
    {
        var pumps = Task.WhenAll(outputPump, errorPump);
        var exitedAt = Stopwatch.GetTimestamp();
        while (true)
        {
            // A handler that threw while the other pipe is still held open must not be lost.
            foreach (var pump in new[] { outputPump, errorPump })
            {
                if (pump.IsFaulted)
                {
                    await pump.ConfigureAwait(false);
                }
            }

            var followProgress = !cancellationToken.IsCancellationRequested;
            TimeSpan wait;
            if (followProgress && (output.IsHandlingLine || error.IsHandlingLine))
            {
                wait = PipeDrainTimeout;
            }
            else
            {
                var lastProgress = followProgress
                    ? Math.Max(exitedAt, Math.Max(output.LastProgress, error.LastProgress))
                    : exitedAt;
                wait = PipeDrainTimeout - Stopwatch.GetElapsedTime(lastProgress);
            }

            if (wait <= TimeSpan.Zero)
            {
                break;
            }

            // Cancelling ends the wait at once, to be looked at again without following progress.
            var delay = followProgress ? Task.Delay(wait, cancellationToken) : Task.Delay(wait, CancellationToken.None);
            if (await Task.WhenAny(pumps, delay).ConfigureAwait(false) == pumps)
            {
                await pumps.ConfigureAwait(false);
                return;
            }
        }

        Abandon(pumps, output, error);
        _log.Debug("A process started by git kept its output open after git exited; its output is no longer read.");
    }

    /// <summary>A task that completes when <paramref name="task"/> does, but never fails.</summary>
    private static Task Settled(Task task) =>
        task.ContinueWith(completed => _ = completed.Exception, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>Stops collecting output and leaves the pumps to end on their own, whenever the pipes close.</summary>
    private static void Abandon(Task pumps, OutputCollector output, OutputCollector error)
    {
        output.Close();
        error.Close();
        _ = pumps.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Stops git and everything it started (ssh, remote helpers, hooks), which would otherwise
    /// keep running and keep the output pipes open. Never throws: it runs inside a cancellation
    /// callback, where an exception would land on whoever cancelled.
    /// </summary>
    private static void Kill(Process process, WindowsJob? job)
    {
        try
        {
            // Only on Windows is the whole tree stopped: through the job object, with the
            // process-tree kill as a fallback when the job could not be created.
            //
            // On macOS and Linux the tree kill is not used at all. It works by suspending
            // processes while it walks the process list, and with it in place test runs froze
            // whole CI machines on macOS (decision D36). There, only git itself is killed. What
            // git started usually ends by itself when its pipes to git break, and the bounded
            // waits in RunAsync keep a call from depending on it.
            if (OperatingSystem.IsWindows())
            {
                job?.Terminate();
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (Exception)
        {
            // Already gone, or the tree could not be walked. The plain kill below still applies.
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception)
        {
            // Already gone.
        }
    }

    private void Record(
        GitCommand command,
        CallStart startedAt,
        TimeSpan duration,
        GitCallOutcome outcome,
        int? exitCode,
        string standardOutput,
        string standardError)
    {
        // The output is recorded with the credentials of every URL hidden, as the command line
        // is: git remote -v and some error texts print URLs with tokens in them (D103). Git's
        // streams arrive hidden and cut already (OutputCollector.Head hides before it cuts), and
        // hiding again changes nothing. The call belongs to the operation the queue is running in
        // this flow, if any (1.7). Standard input is kept only where the command records it
        // (update-ref --stdin's ref names and SHAs, docs/phase-5-design.md 2.3), hidden and cut the same way.
        var record = new GitCallRecord(
            startedAt.Time,
            duration,
            command.DisplayText,
            command.WorkingDirectory,
            outcome,
            exitCode,
            GitCommand.HideCredentials(standardOutput),
            GitCommand.HideCredentials(standardError),
            GitOperationScope.Current)
        {
            Sequence = startedAt.Sequence,
            DisplayedStart = startedAt.DisplayedTime,
            DisplayedDuration = _recordClock.GetElapsedTime(startedAt.DisplayedTimestamp),
            StandardInput = command.RecordsStandardInput ? Head(GitCommand.HideCredentials(command.StandardInput ?? string.Empty)) : null,
            OnTemporaryIndex = command.Environment.TryGetValue(TemporaryIndexVariable, out var index) && index is not null,
        };

        var result = outcome == GitCallOutcome.Completed ? $"exit {exitCode}" : outcome.ToString();
        var where = command.WorkingDirectory is null ? string.Empty : $" in {command.WorkingDirectory}";
        _log.Write(
            record.Succeeded ? LogLevel.Debug : LogLevel.Info,
            $"{record.CommandText}{where}: {result}, {duration.TotalMilliseconds:F0} ms");

        try
        {
            _callLog?.Add(record);
        }
        catch (Exception ex)
        {
            // A listener of the call log failed. That must not turn a finished git call into an error.
            _log.Error("A git call log listener failed.", ex);
        }
    }

    /// <summary>The start of <paramref name="text"/> as the call record keeps it: at most <see cref="GitCallRecord.MaxOutputLength"/> characters, then the cut's marker.</summary>
    private static string Head(string text) =>
        text.Length > GitCallRecord.MaxOutputLength ? text[..GitCallRecord.MaxOutputLength] + GitCallRecord.TruncationMarker : text;

    /// <summary>The variable that points git at another index than the user's: a snapshot's temporary one.</summary>
    private const string TemporaryIndexVariable = "GIT_INDEX_FILE";

    /// <summary>When a call started: the system's time for the record and the log, the number that orders it, and the record clock's time and timestamp for the activity log.</summary>
    private readonly record struct CallStart(DateTimeOffset Time, long Sequence, DateTimeOffset DisplayedTime, long DisplayedTimestamp);

    /// <summary>
    /// Reads one of git's output streams to its end: splits it into lines for the handler, keeps
    /// the whole text when asked to, and always keeps the start of it for the call record.
    /// </summary>
    private sealed class OutputCollector(Action<string>? onLine, bool keepAll, bool splitOnCarriageReturn)
    {
        /// <summary>
        /// How much more than <see cref="GitCallRecord.MaxOutputLength"/> is kept for the record, so
        /// that a URL that the cut falls into is still whole when its credentials are hidden.
        /// </summary>
        private const int HeadSlack = 1024;

        private readonly Lock _gate = new();
        private readonly StringBuilder? _all = keepAll ? new StringBuilder() : null;
        private readonly StringBuilder _head = new();
        private readonly StringBuilder _line = new();
        private bool _headCutOff;
        private bool _lastWasCarriageReturn;
        private volatile bool _closed;
        private volatile bool _handlingLine;
        private long _lastProgress = Stopwatch.GetTimestamp();

        /// <summary>When a chunk was last read or a line handler last returned, as a <see cref="Stopwatch"/> timestamp.</summary>
        public long LastProgress => Interlocked.Read(ref _lastProgress);

        /// <summary>Whether the line handler is running now.</summary>
        public bool IsHandlingLine => _handlingLine;

        public string All
        {
            get
            {
                lock (_gate)
                {
                    return _all?.ToString() ?? string.Empty;
                }
            }
        }

        /// <summary>The start of the output for the call record: its credentials hidden, then cut off (<see cref="RecordedOutput"/>).</summary>
        public string Head
        {
            get
            {
                lock (_gate)
                {
                    return RecordedOutput(_head.ToString(), _headCutOff);
                }
            }
        }

        /// <summary>
        /// What the record keeps of <paramref name="text"/>, the start of a stream that git wrote
        /// more of when <paramref name="cutOff"/>: the credentials of every URL hidden on the whole
        /// text first (D103), then cut to <see cref="GitCallRecord.MaxOutputLength"/> with
        /// <see cref="GitCallRecord.TruncationMarker"/>. A URL's authority that runs on to the cut
        /// is left out from its <c>://</c>: its <c>@</c> may lie beyond the cut, and then its user
        /// information was not found to be hidden.
        /// </summary>
        public static string RecordedOutput(string text, bool cutOff)
        {
            const string SchemeEnd = "://";
            var hidden = GitCommand.HideCredentials(text);
            if (!cutOff && hidden.Length <= GitCallRecord.MaxOutputLength)
            {
                return hidden;
            }

            var end = Math.Min(hidden.Length, GitCallRecord.MaxOutputLength);
            var schemeEnd = end < SchemeEnd.Length ? -1 : hidden.LastIndexOf(SchemeEnd, end - 1, end, StringComparison.Ordinal);
            if (schemeEnd >= 0)
            {
                var authorityStart = schemeEnd + SchemeEnd.Length;
                var authorityEnded = false;
                for (var i = authorityStart; i < end && !authorityEnded; i++)
                {
                    authorityEnded = hidden[i] is '/' or '?' or '#' || char.IsWhiteSpace(hidden[i]);
                }

                if (!authorityEnded)
                {
                    end = authorityStart;
                }
            }

            return hidden[..end] + GitCallRecord.TruncationMarker;
        }

        /// <summary>Stops collecting. A pump that is still reading discards what arrives from now on.</summary>
        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
            }
        }

        public void Pump(StreamReader reader)
        {
            var buffer = new char[8192];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                MarkProgress();
                if (!Collect(buffer.AsSpan(0, read)))
                {
                    return;
                }

                if (onLine is not null)
                {
                    SplitLines(buffer.AsSpan(0, read));
                }
            }

            if (onLine is not null && _line.Length > 0)
            {
                EmitLine();
            }
        }

        /// <summary>Keeps a chunk of output. Returns false once the collector is closed.</summary>
        private bool Collect(ReadOnlySpan<char> chunk)
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return false;
                }

                _all?.Append(chunk);
                AppendToHead(chunk);
                return true;
            }
        }

        private void AppendToHead(ReadOnlySpan<char> chunk)
        {
            var room = GitCallRecord.MaxOutputLength + HeadSlack - _head.Length;
            if (chunk.Length > room)
            {
                _headCutOff = true;
                chunk = chunk[..room];
            }

            _head.Append(chunk);
        }

        private void SplitLines(ReadOnlySpan<char> chunk)
        {
            foreach (var c in chunk)
            {
                if (c == '\n')
                {
                    // The "\n" of a "\r\n" pair already ended its line at the "\r".
                    if (!(splitOnCarriageReturn && _lastWasCarriageReturn))
                    {
                        EmitLine();
                    }
                }
                else if (c == '\r' && splitOnCarriageReturn)
                {
                    EmitLine();
                }
                else
                {
                    _line.Append(c);
                }

                _lastWasCarriageReturn = c == '\r';
            }
        }

        private void EmitLine()
        {
            // Without carriage-return splitting, a "\r" before the "\n" is part of a Windows line ending.
            if (!splitOnCarriageReturn && _line.Length > 0 && _line[^1] == '\r')
            {
                _line.Length--;
            }

            var line = _line.ToString();
            _line.Clear();

            // Once the call has given up on this output, its handler gets no more of it.
            if (_closed)
            {
                return;
            }

            _handlingLine = true;
            try
            {
                onLine!(line);
            }
            finally
            {
                MarkProgress();
                _handlingLine = false;
            }
        }

        private void MarkProgress() => Interlocked.Exchange(ref _lastProgress, Stopwatch.GetTimestamp());
    }
}
