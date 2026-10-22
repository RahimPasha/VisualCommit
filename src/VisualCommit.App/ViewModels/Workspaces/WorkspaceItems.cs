using System.Collections.ObjectModel;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// One workspace as the welcome page lists it (docs/phase-6-design.md 1.3, 13.1): an item of
/// <c>TabServices.WorkspaceItems</c>, which every welcome page shares as it shares the recent
/// list. <see cref="WorkspaceItems"/> keeps that list: rebuilt on <c>IWorkspaceStore.Changed</c>,
/// sorted by name (ignoring case), then id.
/// </summary>
/// <param name="Id">The workspace's <c>Workspace.Id</c>.</param>
/// <param name="Name">Its name.</param>
/// <param name="CountText"><c>{N} repositories</c>, <c>1 repository</c> or <c>No repositories</c>.</param>
/// <param name="ToolTipText">Its repositories' folder names in order, joined by <c>, </c>, at most 8, then <c>, …</c>; or <c>No repositories yet</c>.</param>
public sealed record WorkspaceItem(string Id, string Name, string CountText, string ToolTipText);

/// <summary>
/// The welcome pages' list of workspaces (docs/phase-6-design.md 1.3, 13.1): what each workspace
/// shows, in the list's order, and the keeping of <c>TabServices.WorkspaceItems</c> in step with
/// the store. Used on the UI thread, where the store raises its <c>Changed</c> for the app's edits.
/// </summary>
public static class WorkspaceItems
{
    /// <summary>The item of <paramref name="workspace"/>.</summary>
    public static WorkspaceItem ItemOf(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return new WorkspaceItem(
            workspace.Id,
            workspace.Name,
            WorkspaceTexts.RepositoryCount(workspace.Repositories.Count),
            WorkspaceTexts.FolderNames(workspace.Repositories.Select(repository => repository.Path)));
    }

    /// <summary>
    /// The items of <paramref name="list"/>, by name ignoring case, then by id: predictable, and
    /// workspaces need no order of their own (13.1).
    /// </summary>
    public static IReadOnlyList<WorkspaceItem> ItemsOf(WorkspaceList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return [.. list.Workspaces
            .Select(ItemOf)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Fills <paramref name="items"/> from <paramref name="store"/> now and again after each
    /// <see cref="IWorkspaceStore.Changed"/>, for the session's lifetime (the store and the list
    /// live as long as the session's <c>TabServices</c>).
    /// </summary>
    public static void Keep(IWorkspaceStore store, ObservableCollection<WorkspaceItem> items)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(items);
        Fill(items, ItemsOf(store.Current));
        store.Changed += (_, _) => Fill(items, ItemsOf(store.Current));
    }

    /// <summary>Makes <paramref name="items"/> hold <paramref name="wanted"/>, touching it only where they differ.</summary>
    private static void Fill(ObservableCollection<WorkspaceItem> items, IReadOnlyList<WorkspaceItem> wanted)
    {
        if (items.SequenceEqual(wanted))
        {
            return;
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < items.Count)
            {
                if (items[i] != wanted[i])
                {
                    items[i] = wanted[i];
                }
            }
            else
            {
                items.Add(wanted[i]);
            }
        }

        while (items.Count > wanted.Count)
        {
            items.RemoveAt(items.Count - 1);
        }
    }
}
