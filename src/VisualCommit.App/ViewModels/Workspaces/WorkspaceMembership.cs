using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// Whether a repository is in a workspace already (docs/phase-6-design.md 1.2's second rule):
/// a saved path is <see cref="WorkspacePaths.Same"/> as its working directory, or a row whose
/// repository is open (3.4) reports that working directory, which covers a saved path through a
/// junction, a symbolic link or another spelling git sees through. "Add repository…", "Add open
/// tabs", "Locate…" and the scan's "Already in this workspace" ask it.
/// </summary>
public static class WorkspaceMembership
{
    /// <summary>
    /// Whether <paramref name="workspace"/> holds the repository whose working tree is
    /// <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="workingDirectory">The repository's <c>IGitRepository.WorkingDirectory</c>.</param>
    /// <param name="openedWorkingDirectoryOf">
    /// The working directory of the row whose saved folder is the argument, while that row's
    /// repository is open (the open dashboard's <c>DashboardViewModel.OpenedWorkingDirectoryOf</c>);
    /// null, or a function that answers null, when no dashboard of the workspace is open.
    /// </param>
    public static bool Contains(Workspace workspace, string workingDirectory, Func<string, string?>? openedWorkingDirectoryOf) =>
        SavedPathOf(workspace, workingDirectory, openedWorkingDirectoryOf) is not null;

    /// <summary>
    /// The saved path by which <paramref name="workspace"/> holds the repository whose working tree
    /// is <paramref name="workingDirectory"/>, the first in the workspace's order; null when it does
    /// not hold it. The rules are <see cref="Contains"/>'s.
    /// </summary>
    public static string? SavedPathOf(Workspace workspace, string workingDirectory, Func<string, string?>? openedWorkingDirectoryOf)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        foreach (var repository in workspace.Repositories)
        {
            if (WorkspacePaths.Same(repository.Path, workingDirectory)
                || (openedWorkingDirectoryOf?.Invoke(repository.Path) is { } opened && WorkspacePaths.Same(opened, workingDirectory)))
            {
                return repository.Path;
            }
        }

        return null;
    }
}
