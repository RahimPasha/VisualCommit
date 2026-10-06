namespace VisualCommit.Core.Git;

/// <summary>What a finished git call produced.</summary>
/// <param name="ExitCode">Git's exit code; 0 means success.</param>
/// <param name="StandardOutput">Everything written to standard output, or empty when the command streamed it through <see cref="GitCommand.OnOutputLine"/>.</param>
/// <param name="StandardError">Everything written to standard error.</param>
/// <param name="Duration">How long the call took.</param>
public sealed record GitResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Returns this result, or throws a <see cref="GitException"/> carrying git's own error text when the call failed.</summary>
    public GitResult EnsureSuccess(GitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Succeeded ? this : throw new GitException(command.DisplayText, ExitCode, StandardError);
    }
}

/// <summary>A git call that failed: it could not be started, or it exited with an error.</summary>
public sealed class GitException : Exception
{
    public GitException(string commandText, int? exitCode, string standardError, Exception? innerException = null)
        : base(BuildMessage(commandText, exitCode, standardError), innerException)
    {
        CommandText = commandText;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public string CommandText { get; }

    /// <summary>Git's exit code, or null when git could not be started.</summary>
    public int? ExitCode { get; }

    /// <summary>What git wrote to standard error.</summary>
    public string StandardError { get; }

    private static string BuildMessage(string commandText, int? exitCode, string standardError)
    {
        var outcome = exitCode is null ? "could not be started" : $"failed with exit code {exitCode}";
        var detail = standardError.Trim();
        return detail.Length == 0
            ? $"'{commandText}' {outcome}."
            : $"'{commandText}' {outcome}: {detail}";
    }
}
