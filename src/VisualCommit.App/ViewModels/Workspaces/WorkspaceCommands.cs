using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// Every workspace flow of docs/phase-6-design.md 1.5 to 1.10, one per session
/// (<c>TabServices.WorkspaceCommands</c>), so that the welcome page, the dashboard and the palette
/// run the same code: with the store, the dialog layer, the folder dialog, the repository provider
/// and the host. Each command has its reason, null while it can run, and its run, which checks
/// again and does nothing when it cannot; a run never throws. While the store is read-only every
/// command but Open is disabled with <see cref="EditReason"/>. Errors use the C11 error dialog with
/// the workspace's name as its "In {name}" line and git's output where git said something.
/// Workspace edits are not in the undo history (D154). Used on the UI thread.
/// </summary>
public sealed class WorkspaceCommands
{
    /// <summary>The read-only reason of a newer version's file (1.5).</summary>
    public const string NewerVersionText = "workspaces.json was made by a newer version of VisualCommit";

    /// <summary>The read-only reason of a file that could not be read (1.5).</summary>
    public const string UnreadableText = "workspaces.json could not be read";

    private readonly TabServices _services;

    /// <summary>
    /// The session's commands. Also keeps <c>TabServices.WorkspaceItems</c>, the welcome pages'
    /// list, in step with the store from now on (13.1).
    /// </summary>
    public WorkspaceCommands(TabServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        WorkspaceItems.Keep(services.Workspaces, services.WorkspaceItems);
    }

    /// <summary>Why no workspace can be edited this session (1.3, 1.5): the store's read-only cause in words; null while it can.</summary>
    public string? EditReason => _services.Workspaces.ReadOnlyCause switch
    {
        WorkspaceReadOnlyCause.NewerVersion => NewerVersionText,
        WorkspaceReadOnlyCause.Unreadable => UnreadableText,
        _ => null,
    };

    /// <summary>Why <c>New workspace…</c> cannot run (1.6), or null.</summary>
    public string? CreateReason => EditReason;

    /// <summary>
    /// <c>New workspace…</c> (1.6): the name dialog, then the workspace is created and opened from
    /// <paramref name="from"/> (2.2: from a welcome page its empty tab becomes the dashboard).
    /// Returns the new workspace's id, or null when nothing was created.
    /// </summary>
    public async Task<string?> CreateAsync(RepoTabViewModel? from)
    {
        try
        {
            if (CreateReason is not null)
            {
                return null;
            }

            var dialog = WorkspaceNameDialogViewModel.Create(Store.Current.Workspaces.Select(workspace => workspace.Name));
            if (await _services.DialogHost.ShowAsync(dialog) is not { } answer || CreateReason is not null)
            {
                return null;
            }

            var id = Guid.NewGuid().ToString("N");
            Store.Update(list => WorkspaceEdits.Create(list, id, answer.Name));
            if (Store.Current.Find(id) is null)
            {
                return null;
            }

            _services.WorkspaceHost?.ShowWorkspace(id, from);
            return id;
        }
        catch (Exception ex)
        {
            _services.Log.Error("Creating a workspace failed.", ex);
            return null;
        }
    }

    /// <summary>Opens the workspace <paramref name="workspaceId"/> from <paramref name="from"/> through the host (2.2): never disabled.</summary>
    public void Open(string workspaceId, RepoTabViewModel? from)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        _services.WorkspaceHost?.ShowWorkspace(workspaceId, from);
    }

    /// <summary>Why <c>Rename workspace…</c> cannot run (1.6), or null.</summary>
    public string? RenameReason(string workspaceId) => EditReason;

    /// <summary><c>Rename workspace…</c> (1.6): the name dialog, then the rename; the dashboard's title, the welcome pages and the palette follow the store.</summary>
    public async Task RenameAsync(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        try
        {
            if (RenameReason(workspaceId) is not null || Find(workspaceId) is not { } workspace)
            {
                return;
            }

            var others = Store.Current.Workspaces.Where(other => other.Id != workspaceId).Select(other => other.Name);
            var dialog = WorkspaceNameDialogViewModel.Rename(workspace.Name, others);
            if (await _services.DialogHost.ShowAsync(dialog) is not { } answer || RenameReason(workspaceId) is not null)
            {
                return;
            }

            Store.Update(list => WorkspaceEdits.Rename(list, workspaceId, answer.Name));
        }
        catch (Exception ex)
        {
            _services.Log.Error($"Renaming the workspace {workspaceId} failed.", ex);
        }
    }

    /// <summary>Why <c>Delete workspace…</c> cannot run (1.6), or null.</summary>
    public string? DeleteReason(string workspaceId) => EditReason;

    /// <summary>
    /// <c>Delete workspace…</c> (1.6): the destructive confirmation, then the deletion; its
    /// dashboard closes as by its close button (the dashboard follows the store), and the tabs
    /// opened from it stay. Nothing on disk changes.
    /// </summary>
    public async Task DeleteAsync(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        try
        {
            if (DeleteReason(workspaceId) is not null || Find(workspaceId) is not { } workspace)
            {
                return;
            }

            var request = new ConfirmationRequest(
                WorkspaceTexts.DeleteTitle,
                WorkspaceTexts.DeleteQuestion(workspace.Name, workspace.Repositories.Count),
                _services.WorkspaceHost?.HasOpenDashboard(workspaceId) == true ? WorkspaceTexts.DeleteDetail : string.Empty,
                WorkspaceTexts.DeleteConfirm,
                IsDestructive: true);
            if (!await _services.Dialogs.ConfirmAsync(request) || DeleteReason(workspaceId) is not null)
            {
                return;
            }

            Store.Update(list => WorkspaceEdits.Delete(list, workspaceId));
        }
        catch (Exception ex)
        {
            _services.Log.Error($"Deleting the workspace {workspaceId} failed.", ex);
        }
    }

    /// <summary>Why <c>Add repository…</c> cannot run (1.7), or null.</summary>
    public string? AddRepositoryReason(string workspaceId) => EditReason;

    /// <summary>
    /// <c>Add repository…</c> (1.7): the folder dialog, then a working tree's top appended at once,
    /// or any other folder scanned and offered as 1.9's checklist. Returns the path appended (or
    /// already held) for a top, which the dashboard selects; null otherwise.
    /// <list type="bullet">
    /// <item>A working tree's top (opened, and <see cref="IGitRepository.OpenedAtTopAsync"/> true): its
    /// <see cref="IGitRepository.WorkingDirectory"/> appended, nothing when the workspace holds it
    /// already (1.2); either way the path is returned.</item>
    /// <item>A folder inside a working tree, a folder that is no repository, a bare repository:
    /// scanned (1.8), the repository holding the folder, if any, listed first and unchecked; a
    /// folder inside a repository is scanned, not replaced by its top (a saved folder must be a top).</item>
    /// <item>A <see cref="GitException"/>: <c>Adding a repository failed</c>, <c>Git could not open {folder}.</c>
    /// with git's output; a <see cref="GitUnavailableException"/>: its message as the summary.</item>
    /// </list>
    /// </summary>
    public async Task<string?> AddRepositoryAsync(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        try
        {
            if (AddRepositoryReason(workspaceId) is not null || Find(workspaceId) is not { } workspace)
            {
                return null;
            }

            var folder = await _services.Folders.PickFolderAsync(WorkspaceTexts.AddRepositoryFolderTitle(workspace.Name));
            if (folder is null)
            {
                return null;
            }

            IGitRepository? enclosing = null;
            try
            {
                var repository = await _services.Repositories.OpenAsync(folder);
                if (await repository.OpenedAtTopAsync())
                {
                    return AppendTop(workspaceId, repository.WorkingDirectory);
                }

                enclosing = repository;
            }
            catch (NotARepositoryException)
            {
                // Scanned below: a folder of repositories, a bare repository.
            }
            catch (GitException ex)
            {
                await ShowGitErrorAsync(WorkspaceTexts.AddingFailedTitle, WorkspaceTexts.GitCouldNotOpen(folder), workspace.Name, ex);
                return null;
            }
            catch (GitUnavailableException ex)
            {
                await ShowErrorAsync(WorkspaceTexts.AddingFailedTitle, ex.Message, workspace.Name);
                return null;
            }

            if (AddRepositoryReason(workspaceId) is not null || Find(workspaceId) is null)
            {
                return null;
            }

            var dialog = new AddRepositoriesDialogViewModel(
                workspace.Name,
                folder,
                _services.Repositories,
                workingDirectory => Find(workspaceId) is { } current && WorkspaceMembership.Contains(current, workingDirectory, OpenedOf(workspaceId)),
                enclosing,
                _services.Log);
            var showing = _services.DialogHost.ShowAsync(dialog);
            _ = dialog.Start();
            var answer = await showing;
            if (answer is { Count: > 0 } && AddRepositoryReason(workspaceId) is null)
            {
                Store.Update(list => WorkspaceEdits.Append(list, workspaceId, answer));
            }

            return null;
        }
        catch (Exception ex)
        {
            _services.Log.Error($"Adding a repository to the workspace {workspaceId} failed.", ex);
            return null;
        }
    }

    /// <summary>Why <c>Add open tabs</c> cannot run (1.7): read-only, <c>No tab has a repository open</c>, <c>Every open repository is in this workspace</c>; or null.</summary>
    public string? AddOpenTabsReason(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        if (EditReason is { } readOnly)
        {
            return readOnly;
        }

        var folders = _services.WorkspaceHost?.OpenRepositoryFolders() ?? [];
        if (folders.Count == 0)
        {
            return WorkspaceTexts.NoTabHasARepositoryOpen;
        }

        return Find(workspaceId) is { } workspace && FoldersToAdd(workspace, folders).Count == 0
            ? WorkspaceTexts.EveryOpenRepositoryIsInTheWorkspace
            : null;
    }

    /// <summary>
    /// <c>Add open tabs</c> (1.7): appends, in tab order, the working trees of the tabs with a
    /// repository open (<see cref="IWorkspaceHost.OpenRepositoryFolders"/>) that the workspace does
    /// not hold (1.2); no dialog.
    /// </summary>
    public void AddOpenTabs(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        if (AddOpenTabsReason(workspaceId) is not null || Find(workspaceId) is not { } workspace)
        {
            return;
        }

        var add = FoldersToAdd(workspace, _services.WorkspaceHost?.OpenRepositoryFolders() ?? []);
        Store.Update(list => WorkspaceEdits.Append(list, workspaceId, add));
    }

    /// <summary>Why <c>Remove from workspace</c> cannot run on <paramref name="path"/> (1.10), or null; a bulk run holding the row adds its own reason (4.7).</summary>
    public string? RemoveReason(string workspaceId, string path) => EditReason;

    /// <summary><c>Remove from workspace</c> (1.10): at once, no question; nothing on disk changes.</summary>
    public void Remove(string workspaceId, string path)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(path);
        if (RemoveReason(workspaceId, path) is null)
        {
            Store.Update(list => WorkspaceEdits.Remove(list, workspaceId, path));
        }
    }

    /// <summary>Why <c>Move up</c> cannot run on <paramref name="path"/> (1.10): read-only, <c>It is the first repository</c>; or null.</summary>
    public string? MoveUpReason(string workspaceId, string path) =>
        EditReason ?? (IndexOf(workspaceId, path) == 0 ? WorkspaceTexts.FirstRepository : null);

    /// <summary><c>Move up</c> (1.10, Alt+Up).</summary>
    public void MoveUp(string workspaceId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (MoveUpReason(workspaceId, path) is null)
        {
            Store.Update(list => WorkspaceEdits.Move(list, workspaceId, path, -1));
        }
    }

    /// <summary>Why <c>Move down</c> cannot run on <paramref name="path"/> (1.10): read-only, <c>It is the last repository</c>; or null.</summary>
    public string? MoveDownReason(string workspaceId, string path)
    {
        if (EditReason is { } readOnly)
        {
            return readOnly;
        }

        var index = IndexOf(workspaceId, path);
        return index >= 0 && index == Find(workspaceId)!.Repositories.Count - 1 ? WorkspaceTexts.LastRepository : null;
    }

    /// <summary><c>Move down</c> (1.10, Alt+Down).</summary>
    public void MoveDown(string workspaceId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (MoveDownReason(workspaceId, path) is null)
        {
            Store.Update(list => WorkspaceEdits.Move(list, workspaceId, path, 1));
        }
    }

    /// <summary>Why <c>Locate…</c> cannot run on <paramref name="path"/> (1.10), or null.</summary>
    public string? LocateReason(string workspaceId, string path) => EditReason;

    /// <summary>
    /// <c>Locate…</c> (1.10): the folder dialog titled <c>Locate {folder name}</c>, then the chosen
    /// folder's repository top (also from a folder inside it) in the saved path's place. Refusals,
    /// in <c>Locating the repository failed</c>, change nothing: <c>{folder} is not a git
    /// repository.</c>; <c>{top} is already in {workspace}.</c>; <c>Git could not open {folder}.</c>
    /// with git's output. The same folder again changes nothing.
    /// </summary>
    public async Task LocateAsync(string workspaceId, string path)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            if (LocateReason(workspaceId, path) is not null || Find(workspaceId) is not { } workspace)
            {
                return;
            }

            var folder = await _services.Folders.PickFolderAsync(WorkspaceTexts.LocateFolderTitle(WorkspaceTexts.FolderName(path)));
            if (folder is null)
            {
                return;
            }

            string top;
            try
            {
                top = (await _services.Repositories.OpenAsync(folder)).WorkingDirectory;
            }
            catch (NotARepositoryException)
            {
                await ShowErrorAsync(WorkspaceTexts.LocatingFailedTitle, WorkspaceTexts.NotAGitRepository(folder), workspace.Name);
                return;
            }
            catch (GitException ex)
            {
                await ShowGitErrorAsync(WorkspaceTexts.LocatingFailedTitle, WorkspaceTexts.GitCouldNotOpen(folder), workspace.Name, ex);
                return;
            }
            catch (GitUnavailableException ex)
            {
                await ShowErrorAsync(WorkspaceTexts.LocatingFailedTitle, ex.Message, workspace.Name);
                return;
            }

            if (LocateReason(workspaceId, path) is not null || Find(workspaceId) is not { } current || WorkspacePaths.Same(top, path))
            {
                return;
            }

            var others = current with { Repositories = [.. current.Repositories.Where(repository => !WorkspacePaths.Same(repository.Path, path))] };
            if (WorkspaceMembership.Contains(others, top, OpenedOf(workspaceId)))
            {
                await ShowErrorAsync(WorkspaceTexts.LocatingFailedTitle, WorkspaceTexts.AlreadyIn(top, current.Name), current.Name);
                return;
            }

            Store.Update(list => WorkspaceEdits.Replace(list, workspaceId, path, top));
        }
        catch (Exception ex)
        {
            _services.Log.Error($"Locating {path} for the workspace {workspaceId} failed.", ex);
        }
    }

    private IWorkspaceStore Store => _services.Workspaces;

    private Workspace? Find(string workspaceId) => Store.Current.Find(workspaceId);

    /// <summary>1.2's second rule for the workspace: the open dashboard's rows, through the host.</summary>
    private Func<string, string?> OpenedOf(string workspaceId) =>
        savedPath => _services.WorkspaceHost?.OpenedWorkingDirectoryOf(workspaceId, savedPath);

    /// <summary>The place of <paramref name="path"/> in the workspace, or −1.</summary>
    private int IndexOf(string workspaceId, string path)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(path);
        var repositories = Find(workspaceId)?.Repositories ?? [];
        for (var i = 0; i < repositories.Count; i++)
        {
            if (WorkspacePaths.Same(repositories[i].Path, path))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A top that was opened: appended unless held; the path appended or the saved path that holds it.</summary>
    private string AppendTop(string workspaceId, string top)
    {
        if (Find(workspaceId) is { } workspace && WorkspaceMembership.SavedPathOf(workspace, top, OpenedOf(workspaceId)) is { } held)
        {
            return held;
        }

        Store.Update(list => WorkspaceEdits.Append(list, workspaceId, [top]));
        return top;
    }

    /// <summary>The open tabs' working trees the workspace does not hold, in tab order, each once.</summary>
    private List<string> FoldersToAdd(Workspace workspace, IReadOnlyList<string> folders)
    {
        var opened = OpenedOf(workspace.Id);
        var add = new List<string>();
        foreach (var folder in folders)
        {
            if (!WorkspaceMembership.Contains(workspace, folder, opened) && !add.Any(other => WorkspacePaths.Same(other, folder)))
            {
                add.Add(folder);
            }
        }

        return add;
    }

    private Task ShowGitErrorAsync(string title, string summary, string workspaceName, GitException failure) =>
        _services.DialogHost.ShowAsync(new ErrorDialogViewModel(
            title,
            summary,
            workspaceName,
            GitOutputCleaner.Clean(failure.StandardError, failure.StandardOutput),
            failure.CommandText,
            clipboard: _services.Clipboard,
            log: _services.Log));

    private Task ShowErrorAsync(string title, string summary, string workspaceName) =>
        _services.DialogHost.ShowAsync(new ErrorDialogViewModel(title, summary, workspaceName, clipboard: _services.Clipboard, log: _services.Log));
}
