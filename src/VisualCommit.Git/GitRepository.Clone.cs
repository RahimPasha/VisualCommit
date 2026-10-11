using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    /// <summary>
    /// Clones <paramref name="url"/> into <paramref name="destination"/> and opens the result,
    /// reporting git's progress lines as they come (see <see cref="CloneProgressParser"/>).
    /// <para>
    /// The runner turns git's terminal prompts off. <paramref name="environment"/> holds the
    /// variables of the clone's prompt scope (phase 3 design 3.3), which lead git's and ssh's
    /// prompts to the app's dialogs; without it a clone that needs credentials fails with git's
    /// message. If the clone fails or is cancelled, a destination folder the clone created is
    /// removed again; a destination that existed (empty) is emptied and kept.
    /// </para>
    /// </summary>
    /// <exception cref="IOException">The destination is a file, or a folder that is not empty.</exception>
    /// <exception cref="GitException">Git reported an error; the message is git's own, without its progress lines (<see cref="GitOutputCleaner"/>).</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled; git was stopped.</exception>
    public static async Task<GitRepository> CloneAsync(
        IGitRunner runner,
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IAppLog? log = null,
        IReadOnlyDictionary<string, string?>? environment = null)
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
            Environment = environment ?? new Dictionary<string, string?>(),
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
                // A failure in the middle of a clone leaves progress lines in front of the message
                // that matters. Git shows URLs without their credentials; the text is cleaned of
                // them all the same, because the app shows it.
                var text = GitOutputCleaner.Clean(result.StandardError, result.StandardOutput);
                throw new GitException(command.DisplayText, result.ExitCode, text.Length > 0 ? text : GitCommand.HideCredentials(result.StandardError));
            }

            return await OpenAsync(runner, target, cancellationToken, log).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Info($"Cloning {GitCommand.HideCredentials(url)} into {target} did not complete ({ex.GetType().Name}); removing what it wrote.");
            await RemoveCloneAsync(target, keepFolder: existed, log).ConfigureAwait(false);
            throw;
        }
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
