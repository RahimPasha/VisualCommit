using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>Tag create and delete (docs/phase-3-design.md 2.27, 2.28). Pushing a tag and deleting one on a remote are in <c>GitRepository.Remotes.cs</c> (2.29).</summary>
public sealed partial class GitRepository
{
    public async Task CreateTagAsync(string name, string target, string? message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        // The commit, never a tag object: a tag of an annotated tag would be a nested tag. A full
        // SHA or a full ref name keeps a branch and a tag of the same name apart.
        var commit = target + "^{commit}";
        GitCommand command;
        if (string.IsNullOrWhiteSpace(message))
        {
            command = new GitCommand("tag", name, commit) { WorkingDirectory = WorkingDirectory };
        }
        else
        {
            // The message on standard input with only white space cleaned up: git's default
            // cleanup would take "#42 hotfix" for a comment and leave the message empty (D68's way, section 0).
            command = new GitCommand("tag", "--annotate", "--cleanup=whitespace", "--file=-", name, commit)
            {
                WorkingDirectory = WorkingDirectory,
                StandardInput = message,
            };
        }

        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    public async Task DeleteTagAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var command = new GitCommand("tag", "--delete", name) { WorkingDirectory = WorkingDirectory };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }
}
