using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>What a workspace's saved folder is (docs/phase-6-design.md 1.10, D204).</summary>
public enum WorkspaceFolderState
{
    /// <summary>A repository's working tree, saved at its top: its row reads it.</summary>
    Ready,

    /// <summary>The path is not fully qualified, or the folder does not exist (no git call): <c>Folder not found</c>.</summary>
    FolderNotFound,

    /// <summary>No working tree holds it (no <c>.git</c>, a bare repository, an orphaned worktree), or it lies inside another working tree: <c>Not a repository</c>.</summary>
    NotARepository,

    /// <summary>Git refused it (such as "dubious ownership") or no usable git was found: <c>Git could not open it</c>.</summary>
    NotOpened,
}

/// <summary>
/// A saved folder sorted before anything reads it (docs/phase-6-design.md 1.10): every dashboard
/// row opens its repository through <see cref="ProbeAsync"/> (2.10), and Locate… and the add flows
/// use its parts. No part of a probe blocks the UI thread: the existence check runs on the thread
/// pool, and the provider's open runs there too.
/// </summary>
/// <param name="State">What the folder is.</param>
/// <param name="Repository">For <see cref="WorkspaceFolderState.Ready"/>, the repository opened at the folder; null otherwise (a repository found around a folder inside it is dropped, never read further).</param>
/// <param name="ShortText">The row's State cell (2.6): <c>Folder not found</c>, <c>Not a repository</c>, <c>Git could not open it</c>; null when ready.</param>
/// <param name="ToolTip">The State cell's tooltip, also the reason of the row's Open, Fetch and Pull (2.7); null when ready.</param>
public sealed record WorkspaceFolderProbe(WorkspaceFolderState State, IGitRepository? Repository, string? ShortText, string? ToolTip)
{
    /// <summary><see cref="WorkspaceFolderState.FolderNotFound"/>'s State cell.</summary>
    public const string FolderNotFoundText = "Folder not found";

    /// <summary><see cref="WorkspaceFolderState.NotARepository"/>'s State cell.</summary>
    public const string NotARepositoryText = "Not a repository";

    /// <summary><see cref="WorkspaceFolderState.NotOpened"/>'s State cell.</summary>
    public const string NotOpenedText = "Git could not open it";

    /// <summary>
    /// Sorts <paramref name="savedPath"/> by 1.10's table, in this order: a path that is not fully
    /// qualified, or a folder <paramref name="directoryExists"/> (on the thread pool;
    /// <see cref="Directory.Exists(string)"/> unless a test gives one) says is gone, is
    /// <see cref="WorkspaceFolderState.FolderNotFound"/> without a git call; then
    /// <paramref name="provider"/> opens it: <see cref="NotARepositoryException"/> is
    /// <see cref="WorkspaceFolderState.NotARepository"/>, a <see cref="GitException"/> or
    /// <see cref="GitUnavailableException"/> <see cref="WorkspaceFolderState.NotOpened"/>; an opened
    /// repository is <see cref="WorkspaceFolderState.Ready"/> when
    /// <see cref="IGitRepository.OpenedAtTopAsync"/> says the folder is its top, otherwise
    /// <see cref="WorkspaceFolderState.NotARepository"/> inside it. Any other exception, and a
    /// cancellation, is the caller's.
    /// </summary>
    public static async Task<WorkspaceFolderProbe> ProbeAsync(
        IRepositoryProvider provider,
        string savedPath,
        CancellationToken cancellationToken,
        Func<string, bool>? directoryExists = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(savedPath);
        directoryExists ??= Directory.Exists;
        if (!Path.IsPathFullyQualified(savedPath)
            || !await Task.Run(() => directoryExists(savedPath), cancellationToken).ConfigureAwait(false))
        {
            return FolderNotFound(savedPath);
        }

        IGitRepository repository;
        try
        {
            repository = await provider.OpenAsync(savedPath, cancellationToken).ConfigureAwait(false);
        }
        catch (NotARepositoryException)
        {
            return NotARepository(savedPath);
        }
        catch (GitException ex)
        {
            return NotOpened(savedPath, FirstLine(ex.StandardError) ?? ex.Message);
        }
        catch (GitUnavailableException ex)
        {
            return NotOpened(savedPath, ex.Message);
        }

        bool atTop;
        try
        {
            atTop = await repository.OpenedAtTopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GitException ex)
        {
            return NotOpened(savedPath, FirstLine(ex.StandardError) ?? ex.Message);
        }
        catch (GitUnavailableException ex)
        {
            return NotOpened(savedPath, ex.Message);
        }

        // A folder inside another working tree: the outer repository is dropped here, never read
        // further or acted on (0.8).
        return atTop
            ? new WorkspaceFolderProbe(WorkspaceFolderState.Ready, repository, null, null)
            : NotARepositoryInside(savedPath, repository.WorkingDirectory);
    }

    /// <summary><see cref="WorkspaceFolderState.FolderNotFound"/>: <c>{path} does not exist</c>.</summary>
    public static WorkspaceFolderProbe FolderNotFound(string path) =>
        new(WorkspaceFolderState.FolderNotFound, null, FolderNotFoundText, $"{path} does not exist");

    /// <summary><see cref="WorkspaceFolderState.NotARepository"/>: <c>{path} is not a git repository</c>.</summary>
    public static WorkspaceFolderProbe NotARepository(string path) =>
        new(WorkspaceFolderState.NotARepository, null, NotARepositoryText, $"{path} is not a git repository");

    /// <summary><see cref="WorkspaceFolderState.NotARepository"/> inside another working tree: <c>{path} is not a git repository of its own: it lies inside {outer}</c>.</summary>
    public static WorkspaceFolderProbe NotARepositoryInside(string path, string outer) =>
        new(WorkspaceFolderState.NotARepository, null, NotARepositoryText, $"{path} is not a git repository of its own: it lies inside {outer}");

    /// <summary><see cref="WorkspaceFolderState.NotOpened"/>: <c>Git could not open {path}: {line}</c>, the line git's first of standard error or the exception's message.</summary>
    public static WorkspaceFolderProbe NotOpened(string path, string line) =>
        new(WorkspaceFolderState.NotOpened, null, NotOpenedText, $"Git could not open {path}: {line}");

    private static string? FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
