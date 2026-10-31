using System.Diagnostics;
using System.Text;
using VisualCommit.Core.Operations;

namespace VisualCommit.Testing;

/// <summary>
/// Runs the app's askpass helper (<c>visualcommit-askpass</c>, design 3.1) the way OpenSSH runs
/// <c>SSH_ASKPASS</c>, for the tests of the prompt dialogs that no ssh server can reach (design
/// 3.5, check 21b steps 4 to 6): the helper as a program of its own, not through a shell, with the
/// prompt text as its one argument; <c>SSH_ASKPASS_PROMPT</c> set when ssh would set it
/// (<c>confirm</c>, <c>none</c>) and absent otherwise; and the environment of an open prompt
/// scope on top of the test process's, as git and ssh would hand it on. Standard input is closed
/// at once (the helper never reads it); standard output and standard error are collected.
/// <para>
/// The helper is work package WP3's: its app host is expected next to the test assembly, as
/// <see cref="HelperPath"/> says, and its command line is the documented one (one argument, the
/// prompt; the answer and a line feed on standard output and exit code 0; exit code 1 and no
/// output for no answer). Until it exists a run throws <see cref="FileNotFoundException"/>.
/// </para>
/// </summary>
public static class AskPassRunner
{
    /// <summary>The helper's executable name without its extension (design 3.1).</summary>
    public const string HelperName = "visualcommit-askpass";

    /// <summary>The variable through which OpenSSH tells the askpass program what kind of prompt it is.</summary>
    public const string PromptKindVariable = "SSH_ASKPASS_PROMPT";

    /// <summary>How long a run may take when the test names no time: a dialog the test never answers fails the test instead of hanging it.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The helper's app host as the test project's build puts it: next to the test assembly,
    /// <c>visualcommit-askpass.exe</c> on Windows and <c>visualcommit-askpass</c> elsewhere
    /// (design 3.1: <c>VisualCommit.Git</c> references the helper's project, so its app host lands in
    /// every test project's output).
    /// </summary>
    public static string HelperPath =>
        Path.Combine(AppContext.BaseDirectory, HelperName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

    /// <summary>Runs the helper with <paramref name="prompt"/> within the open <paramref name="scope"/>, as OpenSSH would.</summary>
    /// <param name="scope">An open prompt scope, from the broker under test; its variables (pipe, token, operation id) go into the helper's environment.</param>
    /// <param name="prompt">The prompt text, exactly as ssh would pass it (trailing space included).</param>
    /// <param name="sshPromptKind"><c>confirm</c> or <c>none</c> for <c>SSH_ASKPASS_PROMPT</c>; null leaves it out, as for a passphrase or a host-key question.</param>
    /// <param name="helperPath">The program to run; <see cref="HelperPath"/> when null.</param>
    /// <param name="timeout">How long the run may take; <see cref="DefaultTimeout"/> when null. The helper is stopped when it runs longer.</param>
    /// <param name="cancellationToken">Stops the helper and the wait.</param>
    public static Task<AskPassResult> RunAsync(
        PromptScope scope,
        string prompt,
        string? sshPromptKind = null,
        string? helperPath = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ObjectDisposedException.ThrowIf(scope.IsDisposed, scope);
        return RunAsync(scope.Environment, prompt, sshPromptKind, helperPath, timeout, cancellationToken);
    }

    /// <summary>Runs the helper with <paramref name="prompt"/> and the variables <paramref name="environment"/> gives (a null value removes one), as OpenSSH would.</summary>
    public static async Task<AskPassResult> RunAsync(
        IReadOnlyDictionary<string, string?> environment,
        string prompt,
        string? sshPromptKind = null,
        string? helperPath = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo(environment, prompt, sshPromptKind, helperPath);
        return await RunAsync(startInfo, null, $"the prompt \"{prompt}\"", timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the helper as git runs a credential helper (docs/phase-7a-design.md 4.5):
    /// <c>--vc-credential</c> and <paramref name="action"/> as its arguments (none after the first
    /// when <paramref name="action"/> is null), <paramref name="standardInput"/> written to its
    /// input and the input closed, and the variables <paramref name="environment"/> gives on top of
    /// the test process's (a null value removes one). <c>SSH_ASKPASS_PROMPT</c> is removed.
    /// </summary>
    public static Task<AskPassResult> RunCredentialAsync(
        string? action,
        string standardInput,
        IReadOnlyDictionary<string, string?> environment,
        string? helperPath = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        string[] arguments = action is null ? ["--vc-credential"] : ["--vc-credential", action];
        var startInfo = CreateStartInfo(environment, arguments, null, helperPath);
        return RunAsync(startInfo, standardInput, $"the credential action \"{action}\"", timeout, cancellationToken);
    }

    private static async Task<AskPassResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        string description,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(startInfo.FileName))
        {
            throw new FileNotFoundException(
                $"The askpass helper is not in the test's output. It is built by the project src/VisualCommit.AskPass (work package WP3), which VisualCommit.Git references.",
                startInfo.FileName);
        }

        var limit = timeout ?? DefaultTimeout;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{startInfo.FileName} did not start.");

        // A prompt's helper never reads its input: closed at once, as nothing would answer it. A
        // credential helper gets git's request first, as git writes it before it reads.
        if (standardInput is not null)
        {
            try
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The helper ended without reading it all: its exit code tells.
            }
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(limit);
        try
        {
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It ended meanwhile.
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"The askpass helper was still running after {limit.TotalSeconds:0.###} s with {description}."));
        }

        return new AskPassResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    /// <summary>
    /// How <see cref="RunAsync(IReadOnlyDictionary{string, string?}, string, string?, string?, TimeSpan?, CancellationToken)"/>
    /// starts the helper: the program, one argument, the test process's environment with
    /// <paramref name="environment"/> applied (null removes a variable) and
    /// <c>SSH_ASKPASS_PROMPT</c> set to <paramref name="sshPromptKind"/> or removed. Its streams
    /// are redirected, standard output and standard error read as UTF-8.
    /// </summary>
    public static ProcessStartInfo CreateStartInfo(
        IReadOnlyDictionary<string, string?> environment,
        string prompt,
        string? sshPromptKind = null,
        string? helperPath = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // One argument, passed as it is, however many spaces, quotes or line feeds it holds.
        return CreateStartInfo(environment, [prompt], sshPromptKind, helperPath);
    }

    private static ProcessStartInfo CreateStartInfo(
        IReadOnlyDictionary<string, string?> environment,
        IReadOnlyList<string> arguments,
        string? sshPromptKind,
        string? helperPath)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var startInfo = new ProcessStartInfo(helperPath ?? HelperPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (variable, value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(variable);
            }
            else
            {
                startInfo.Environment[variable] = value;
            }
        }

        if (sshPromptKind is null)
        {
            startInfo.Environment.Remove(PromptKindVariable);
        }
        else
        {
            startInfo.Environment[PromptKindVariable] = sshPromptKind;
        }

        return startInfo;
    }
}

/// <summary>How a run of the askpass helper ended.</summary>
/// <param name="ExitCode">0 when it answered, 1 when it had no answer (design 3.1).</param>
/// <param name="StandardOutput">The answer and its line feed, or nothing.</param>
/// <param name="StandardError">What it wrote to standard error; empty normally.</param>
public sealed record AskPassResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>The answer as ssh reads it: the output up to its first carriage return or line feed; null when the helper exited with an error.</summary>
    public string? Answer => ExitCode == 0 ? StandardOutput.Split('\r', '\n')[0] : null;
}
