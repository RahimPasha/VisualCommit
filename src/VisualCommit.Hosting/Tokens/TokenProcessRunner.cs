using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Runs a store's helper program (docs/phase-7a-design.md 3.5): macOS' <c>/usr/bin/security</c>.
/// Tests put a recording runner in its place.
/// </summary>
public interface ITokenProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/>, writes
    /// <paramref name="standardInput"/> (when given) and closes it, and waits at most
    /// <paramref name="limit"/>: past it the process tree is stopped and the result has no exit code.
    /// A cancel stops the process tree too, and throws.
    /// </summary>
    Task<TokenProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken ct);
}

/// <summary>
/// How a store's helper program ended. Not a record: a read's <see cref="StandardOutput"/> is the
/// token in base64, so the text of this object gives the exit code only.
/// </summary>
public sealed class TokenProcessResult(int? exitCode, string standardOutput, string standardError)
{
    /// <summary>The exit code; null when the process was stopped at its time limit.</summary>
    public int? ExitCode { get; } = exitCode;

    public string StandardOutput { get; } = standardOutput;

    public string StandardError { get; } = standardError;

    public override string ToString() =>
        ExitCode is { } code ? $"TokenProcessResult(exit code {code})" : "TokenProcessResult(stopped at the limit)";
}

/// <summary>
/// The default runner: the arguments through <see cref="ProcessStartInfo.ArgumentList"/>, the three
/// streams redirected in UTF-8, the process tree killed at the limit or on a cancel. It writes no
/// log: standard input and output may hold a token.
/// </summary>
public sealed class TokenProcessRunner : ITokenProcessRunner
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // How long the output may take to end after the process tree was stopped.
    private static readonly TimeSpan DrainLimit = TimeSpan.FromSeconds(2);

    public static TokenProcessRunner Instance { get; } = new();

    public Task<TokenProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        // Starting a process blocks for a moment: never on the caller's thread (Q2).
        return Task.Run(() => RunCoreAsync(fileName, arguments, standardInput, limit, ct), ct);
    }

    private static async Task<TokenProcessResult> RunCoreAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken ct)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = info };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(limit);
        try
        {
            try
            {
                if (standardInput is not null)
                {
                    await process.StandardInput.WriteAsync(standardInput.AsMemory(), stop.Token).ConfigureAwait(false);
                }

                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // The process ended before it read its input; its exit code says why.
            }

            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            await DrainAsync(output, error).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return new TokenProcessResult(null, string.Empty, string.Empty);
        }

        return new TokenProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended on its own meanwhile, or cannot be stopped: nothing more to do.
        }
    }

    private static async Task DrainAsync(Task<string> output, Task<string> error)
    {
        try
        {
            await Task.WhenAll(output, error).WaitAsync(DrainLimit).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or ObjectDisposedException)
        {
            // The output is not used after a stop.
        }
    }
}
