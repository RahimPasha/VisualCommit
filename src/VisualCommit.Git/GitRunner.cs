using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

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
    /// machine; with prompts off, git fails instead of waiting on a terminal that is not there.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?> DefaultEnvironment = new Dictionary<string, string?>
    {
        ["LC_ALL"] = "en_US.UTF-8",
        ["LANG"] = "en_US.UTF-8",
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    private readonly IGitCallLog? _callLog;
    private readonly IAppLog _log;

    public GitRunner(string executablePath, IGitCallLog? callLog = null, IAppLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ExecutablePath = executablePath;
        _callLog = callLog;
        _log = log ?? NullAppLog.Instance;
    }

    public string ExecutablePath { get; }

    public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = DateTimeOffset.Now;
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
        var outputPump = output.PumpAsync(process.StandardOutput);
        var errorPump = error.PumpAsync(process.StandardError);
        var pumps = Task.WhenAll(outputPump, errorPump);
        var exit = process.WaitForExitAsync(CancellationToken.None);

        // Cancelling stops git, which ends the wait below. Should git not stop, the wait is given
        // up after StopTimeout: a call that was cancelled must return, whatever git does.
        var gaveUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopOnCancel = cancellationToken.Register(() =>
        {
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
        if (cancellationToken.IsCancellationRequested)
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
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
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
        DateTimeOffset startedAt,
        TimeSpan duration,
        GitCallOutcome outcome,
        int? exitCode,
        string standardOutput,
        string standardError)
    {
        var record = new GitCallRecord(
            startedAt,
            duration,
            command.DisplayText,
            command.WorkingDirectory,
            outcome,
            exitCode,
            standardOutput,
            standardError);

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

    /// <summary>
    /// Reads one of git's output streams to its end: splits it into lines for the handler, keeps
    /// the whole text when asked to, and always keeps the start of it for the call record.
    /// </summary>
    private sealed class OutputCollector(Action<string>? onLine, bool keepAll, bool splitOnCarriageReturn)
    {
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

        public string Head
        {
            get
            {
                lock (_gate)
                {
                    return _headCutOff ? _head.ToString() + GitCallRecord.TruncationMarker : _head.ToString();
                }
            }
        }

        /// <summary>Stops collecting. A pump that is still reading discards what arrives from now on.</summary>
        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
            }
        }

        public async Task PumpAsync(StreamReader reader)
        {
            var buffer = new char[8192];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
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
            var room = GitCallRecord.MaxOutputLength - _head.Length;
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
