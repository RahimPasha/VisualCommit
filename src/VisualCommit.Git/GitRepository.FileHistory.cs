using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// A file's history, its blame and a commit's whole tree (docs/phase-5-design.md 4.1, 4.2, 4.5, 0.1,
/// D158, D164, D175). Work package WP1 builds them here; until then each throws.
/// </summary>
public sealed partial class GitRepository
{
    public Task ReadFileHistoryAsync(string startSha, string path, Action<FileHistoryRow> onRow, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("A file's history is work package WP1's (docs/phase-5-design.md 4.1).");

    public Task<BlameResult> ReadBlameAsync(string? sha, string path, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Blame is work package WP1's (docs/phase-5-design.md 4.2).");

    public Task<IReadOnlyList<string>> ReadTreeFilesAsync(string sha, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("A commit's tree is work package WP1's (docs/phase-5-design.md 4.5).");
}
