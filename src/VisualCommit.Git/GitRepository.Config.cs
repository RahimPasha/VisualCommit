using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>This repository's own settings, written by the settings screen's "This repository" (docs/phase-6-design.md 8.3).</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// <c>git config --local --replace-all &lt;key&gt; &lt;value&gt;</c>, or <c>--unset-all &lt;key&gt;</c>
    /// for a null value (exit 5 counting as done: the key was absent), one call per write, in order,
    /// in the working tree (8.3); in a linked worktree git writes the common <c>.git/config</c>
    /// (0.3). The first failure stops the list and throws <see cref="GitException"/> with git's
    /// output (a held <c>.git/config.lock</c>: <c>could not lock config file</c>, exit 255; 8.5).
    /// <paramref name="cancellationToken"/> stops the list before a write, never during one, which
    /// would leave git's lock file behind.
    /// </summary>
    public async Task WriteConfigAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writes);
        foreach (var write in writes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = GitConfigWrites.Command("--local", write, WorkingDirectory);
            var result = await _runner.RunAsync(command, CancellationToken.None).ConfigureAwait(false);
            GitConfigWrites.EnsureWritten(command, result, write, _log);
        }
    }
}
