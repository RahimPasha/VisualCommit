using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    /// <summary>
    /// Clones <paramref name="url"/> into <paramref name="destination"/> and opens the result,
    /// reporting git's progress lines as they come (see <see cref="CloneProgressParser"/>).
    /// <para>
    /// Git cannot ask for credentials (the runner turns prompts off), so a clone that needs them
    /// fails with git's message. If the clone fails or is cancelled, a destination folder the
    /// clone created is removed again; a destination that existed (empty) is emptied and kept.
    /// </para>
    /// </summary>
    /// <exception cref="IOException">The destination is a file, or a folder that is not empty.</exception>
    /// <exception cref="GitException">Git reported an error; the message is git's own.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled; git was stopped.</exception>
    public static async Task<GitRepository> CloneAsync(
        IGitRunner runner,
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        log ??= NullAppLog.Instance;
        cancellationToken.ThrowIfCancellationRequested();

        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var parent = Path.GetDirectoryName(target);
        if (parent is null)
        {
            throw new IOException($"Cannot clone into {target}: it is the root of a drive.");
        }

        if (File.Exists(target))
        {
            throw new IOException($"Cannot clone into {target}: a file of that name exists.");
        }

        var existed = Directory.Exists(target);
        if (existed && Directory.EnumerateFileSystemEntries(target).Any())
        {
            throw new IOException($"Cannot clone into {target}: the folder is not empty.");
        }

        Directory.CreateDirectory(parent);

        // The destination is given relative to the folder git runs in, so that git's own
        // "Cloning into '...'" line names just the folder.
        var command = new GitCommand("clone", "--progress", "--", url, Path.GetFileName(target))
        {
            WorkingDirectory = parent,
            OnErrorLine = line =>
            {
                if (progress is not null && CloneProgressParser.Parse(line) is { } parsed)
                {
                    progress.Report(parsed);
                }
            },
        };

        try
        {
            var result = await runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new GitException(command.DisplayText, result.ExitCode, CloneErrorText(result.StandardError));
            }

            return await OpenAsync(runner, target, cancellationToken, log).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Info($"Cloning {url} into {target} did not complete ({ex.GetType().Name}); removing what it wrote.");
            await RemoveCloneAsync(target, keepFolder: existed, log).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Git's error output without its progress lines, which a failure in the middle of a clone
    /// leaves in front of the message that matters.
    /// </summary>
    private static string CloneErrorText(string standardError)
    {
        var lines = standardError
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("Cloning into", StringComparison.Ordinal)
                && CloneProgressParser.Parse(line) is not { Percent: not null });
        var text = string.Join('\n', lines);
        return text.Length > 0 ? text : standardError;
    }

    /// <summary>
    /// Removes what a failed or cancelled clone wrote. Git marks its object files read-only, and
    /// on Windows a git that was just stopped can hold a file for a moment, so the attributes are
    /// cleared and the removal is retried for a while. What cannot be removed is logged, not thrown:
    /// the clone's own error is the one the caller needs.
    /// </summary>
    private static async Task RemoveCloneAsync(string target, bool keepFolder, IAppLog log)
    {
        const int Attempts = 20;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!Directory.Exists(target))
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                if (keepFolder)
                {
                    foreach (var folder in Directory.EnumerateDirectories(target))
                    {
                        Directory.Delete(folder, recursive: true);
                    }

                    foreach (var file in Directory.EnumerateFiles(target))
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.Delete(target, recursive: true);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == Attempts)
                {
                    log.Warning($"Could not remove what the clone wrote to {target}.", ex);
                    return;
                }

                await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
