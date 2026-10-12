using System.Collections.Concurrent;
using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Answers prompts from a script and records every prompt it was given (docs/phase-3-design.md
/// 3.5). Each prompt takes the next step of the script; when the script has run out, the prompt is
/// refused. A step can hold the prompt until its token is cancelled, as a dialog that is left open.
/// </summary>
internal sealed class RecordingPromptHandler : IGitPromptHandler
{
    private readonly ConcurrentQueue<Func<GitPrompt, CancellationToken, Task<PromptAnswer>>> _script = new();
    private readonly ConcurrentQueue<GitPrompt> _prompts = new();

    public RecordingPromptHandler(params PromptAnswer[] answers)
    {
        foreach (var answer in answers)
        {
            Then(answer);
        }
    }

    /// <summary>Every prompt given so far, in order.</summary>
    public IReadOnlyList<GitPrompt> Prompts => [.. _prompts];

    /// <summary>Completes when a held prompt's token is cancelled (its dialog would close).</summary>
    public TaskCompletionSource HeldPromptClosed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a prompt has started to be held.</summary>
    public TaskCompletionSource HoldBegan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordingPromptHandler Then(PromptAnswer answer) => Then((_, _) => Task.FromResult(answer));

    public RecordingPromptHandler Then(Func<GitPrompt, CancellationToken, Task<PromptAnswer>> step)
    {
        _script.Enqueue(step);
        return this;
    }

    /// <summary>A step that keeps the prompt open until its token is cancelled, then refuses it.</summary>
    public RecordingPromptHandler ThenHold() => Then(async (_, cancellationToken) =>
    {
        HoldBegan.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            HeldPromptClosed.TrySetResult();
        }

        return PromptAnswer.Refused;
    });

    public Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
        _prompts.Enqueue(prompt);
        return _script.TryDequeue(out var step) ? step(prompt, cancellationToken) : Task.FromResult(PromptAnswer.Refused);
    }
}

/// <summary>What the prompt tests share: git and ssh-keygen run as the app runs them, with a scope's variables.</summary>
internal static class PromptTestSupport
{
    /// <summary>A step should never take this long; the tests fail instead of hanging.</summary>
    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Runs git in <paramref name="repo"/> with its isolation, without any askpass program or proxy
    /// the test process inherited, and with <paramref name="scope"/>'s variables on top, as the
    /// operation queue hands them to an operation (1.3 step 3). <paramref name="runner"/> defaults
    /// to one without a call log, because <c>git credential fill</c> prints the password.
    /// </summary>
    public static Task<GitResult> GitAsync(
        TempRepo repo,
        IReadOnlyDictionary<string, string?> scopeEnvironment,
        string[] arguments,
        string? input = null,
        IGitRunner? runner = null) =>
        StoppableGitAsync(repo, scopeEnvironment, arguments, input, runner, RepositoryTestSupport.TestCancelled);

    /// <summary><see cref="GitAsync"/> with a token that stops git.</summary>
    public static Task<GitResult> StoppableGitAsync(
        TempRepo repo,
        IReadOnlyDictionary<string, string?> scopeEnvironment,
        string[] arguments,
        string? input,
        IGitRunner? runner,
        CancellationToken cancellationToken)
    {
        var command = new GitCommand(arguments)
        {
            WorkingDirectory = repo.Path,
            StandardInput = input,
            Environment = Environment(repo, scopeEnvironment),
        };
        return (runner ?? RepositoryTestSupport.NewRunner()).RunAsync(command, cancellationToken);
    }

    /// <summary>The repo's isolation, nothing inherited that could answer or redirect, then the scope's variables.</summary>
    public static Dictionary<string, string?> Environment(TempRepo repo, IReadOnlyDictionary<string, string?> scopeEnvironment)
    {
        var environment = new Dictionary<string, string?>(repo.Environment);
        foreach (var variable in new[]
        {
            "GIT_ASKPASS", "SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "GCM_INTERACTIVE", "SSH_ASKPASS_PROMPT",
            "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy",
        })
        {
            environment[variable] = null;
        }

        environment["no_proxy"] = "127.0.0.1,localhost";
        environment["NO_PROXY"] = "127.0.0.1,localhost";
        foreach (var (name, value) in scopeEnvironment)
        {
            environment[name] = value;
        }

        return environment;
    }

    /// <summary>The input of <c>git credential fill</c> for <paramref name="url"/>'s protocol and host.</summary>
    public static string CredentialRequest(string protocol, string host) => $"protocol={protocol}\nhost={host}\n\n";

    /// <summary>The value of <paramref name="key"/> in <c>git credential fill</c>'s output, or null.</summary>
    public static string? CredentialValue(string output, string key) =>
        output.Split('\n').Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith(key + "=", StringComparison.Ordinal))?[(key.Length + 1)..];

    /// <summary>A copy of the helper (its app host and the files it loads) in <paramref name="folder"/>; returns the copy's path.</summary>
    public static string CopyHelperTo(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.EnumerateFiles(AskPassLocator.Folder, "visualcommit-askpass*"))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), overwrite: true);
        }

        return Path.Combine(folder, AskPassLocator.FileName);
    }

    /// <summary>
    /// ssh-keygen: on Windows the one Git for Windows bundles (in <c>usr/bin</c> of git's
    /// installation, found from the git the tests run), elsewhere the one on PATH
    /// (<c>scripts/setup-linux.sh</c> installs openssh-client).
    /// </summary>
    public static string FindSshKeygen()
    {
        var name = OperatingSystem.IsWindows() ? "ssh-keygen.exe" : "ssh-keygen";
        if (OperatingSystem.IsWindows())
        {
            var git = GitLocator.FindExecutable(GitSearchContext.FromSystem())
                ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");
            var folder = Path.GetDirectoryName(Path.GetFullPath(git));
            for (var level = 0; level < 4 && folder is not null; level++, folder = Path.GetDirectoryName(folder))
            {
                var candidate = Path.Combine(folder, "usr", "bin", name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        foreach (var folder in (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim('"'), name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"The tests need {name}: next to git on Windows, on PATH elsewhere.");
    }

    /// <summary>Runs a program with standard input closed and <paramref name="environment"/> added; returns its exit code and output.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunProgramAsync(
        string program,
        IReadOnlyDictionary<string, string?> environment,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
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

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{program} did not start.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(RepositoryTestSupport.TestCancelled);
        var error = process.StandardError.ReadToEndAsync(RepositoryTestSupport.TestCancelled);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(RepositoryTestSupport.TestCancelled);
        timeout.CancelAfter(StepTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await output, await error);
    }

    /// <summary>Asserts that <paramref name="secret"/> is in no call record and no log line.</summary>
    public static void AssertNowhere(string secret, IGitCallLog calls, ListLog log)
    {
        Assert.NotEmpty(calls.Snapshot());
        foreach (var record in calls.Snapshot())
        {
            Assert.DoesNotContain(secret, record.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, record.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, record.StandardError, StringComparison.Ordinal);
        }

        Assert.All(log.Entries, entry => Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal));
    }
}
