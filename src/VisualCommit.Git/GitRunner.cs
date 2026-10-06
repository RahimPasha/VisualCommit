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
    /// How long to keep reading output after git itself has exited. Everything git wrote is
    /// readable at once; only a process that git left behind (a hook's background job, a daemon)
    /// can keep the pipes open longer, and its output is not waited for.
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
                await DrainAsync(pumps, output, error).ConfigureAwait(false);
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
            await DrainAsync(pumps.ContinueWith(_ => { }, TaskScheduler.Default), output, error).ConfigureAwait(false);
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
    /// Waits for the output pumps once git has exited, but only for <see cref="PipeDrainTimeout"/>.
    /// After that the pumps are left to end on their own and what they read later is dropped.
    /// </summary>
    private async Task DrainAsync(Task pumps, OutputCollector output, OutputCollector error)
    {
        if (await Task.WhenAny(pumps, Task.Delay(PipeDrainTimeout)).ConfigureAwait(false) == pumps)
        {
            await pumps.ConfigureAwait(false);
            return;
        }

        Abandon(pumps, output, error);
        _log.Debug("A process started by git kept its output open after git exited; its output is no longer read.");
    }

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
            if (OperatingSystem.IsWindows())
            {
                job?.Terminate();
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Already gone, or the tree could not be walked. The plain kill below still applies.
        }

        try
        {
            // The tree kill is not all-or-nothing. On macOS and Linux it first suspends each
            // process and then lists its children; if listing fails, git is left suspended:
            // alive, holding its pipes, never exiting. A plain kill ends it in every state.
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
        private bool _closed;

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
            onLine!(line);
        }
    }
}
