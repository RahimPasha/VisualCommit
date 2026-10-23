using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>What a dashboard row's menu does (docs/phase-6-design.md 2.7): the dashboard implements it.</summary>
public interface IDashboardRowActions
{
    /// <summary>Where a failed item is logged.</summary>
    IAppLog Log { get; }

    /// <summary><c>Open</c> (2.3).</summary>
    void Open(DashboardRowViewModel row);

    /// <summary><c>Fetch</c>: the toolbar's Fetch in the row's view model (4.7).</summary>
    Task FetchAsync(DashboardRowViewModel row);

    /// <summary><c>Pull</c>: the toolbar's Pull in the row's view model (4.7).</summary>
    Task PullAsync(DashboardRowViewModel row);

    /// <summary><c>Cancel {title}</c>: the row's status's Cancel.</summary>
    void Cancel(DashboardRowViewModel row);

    /// <summary><c>Error details…</c>: the kept dialog (4.6).</summary>
    void ShowErrorDetails(DashboardRowViewModel row);

    /// <summary><c>Show in folder</c>.</summary>
    Task ShowInFolderAsync(DashboardRowViewModel row);

    /// <summary><c>Copy path</c>: the folder as the platform writes it.</summary>
    Task CopyPathAsync(DashboardRowViewModel row);

    /// <summary><c>Move up</c> (Alt+Up).</summary>
    void MoveUp(DashboardRowViewModel row);

    /// <summary><c>Move down</c> (Alt+Down).</summary>
    void MoveDown(DashboardRowViewModel row);

    /// <summary><c>Locate…</c> (1.10).</summary>
    Task LocateAsync(DashboardRowViewModel row);

    /// <summary><c>Remove from workspace</c> (1.10, Delete).</summary>
    void Remove(DashboardRowViewModel row);
}

/// <summary>What the header's <c>…</c> menu does (2.5): the dashboard implements it.</summary>
public interface IDashboardHeaderActions
{
    /// <summary>Where a failed item is logged.</summary>
    IAppLog Log { get; }

    /// <summary><c>Add open tabs</c> (1.7).</summary>
    void AddOpenTabs();

    /// <summary><c>Rename workspace…</c> (1.6).</summary>
    Task RenameAsync();

    /// <summary><c>Delete workspace…</c> (1.6).</summary>
    Task DeleteAsync();
}

/// <summary>
/// A row's state as its menu needs it (2.7): each item's reason, null while it can run, and what
/// shows. The dashboard makes it from the row (<c>DashboardViewModel.MenuStateOf</c>).
/// </summary>
/// <param name="Name">The header: the repository's name.</param>
/// <param name="Folder">The header's tooltip: the saved folder.</param>
/// <param name="OpenReason"><c>Open</c>'s: <c>Wait for {name} to be read</c>, or a folder state's tooltip.</param>
/// <param name="FetchReason"><c>Fetch</c>'s: <c>Open</c>'s, the toolbar's, or a running run's.</param>
/// <param name="PullReason"><c>Pull</c>'s: as <paramref name="FetchReason"/>.</param>
/// <param name="RunningTitle">The title of the operation of the user that runs in the row's queue, or null.</param>
/// <param name="RunningIsCancellable">That operation could be cancelled (a network one, the merge tool): <c>Cancel {title}</c> shows.</param>
/// <param name="CanCancel">It can be cancelled now (its cancel window is open).</param>
/// <param name="HasKeptDialog">A run kept a dialog: <c>Error details…</c> shows.</param>
/// <param name="ShowInFolderReason"><c>Show in folder</c>'s: the folder is gone.</param>
/// <param name="MoveUpReason"><c>Move up</c>'s: read-only, the first row.</param>
/// <param name="MoveDownReason"><c>Move down</c>'s: read-only, the last row.</param>
/// <param name="ShowsLocate"><c>Locate…</c> shows: a folder state (1.10).</param>
/// <param name="LocateReason"><c>Locate…</c>'s: read-only.</param>
/// <param name="RemoveReason"><c>Remove from workspace</c>'s: read-only, a running run holds the row.</param>
public sealed record DashboardRowMenuState(
    string Name,
    string Folder,
    string? OpenReason,
    string? FetchReason,
    string? PullReason,
    string? RunningTitle,
    bool RunningIsCancellable,
    bool CanCancel,
    bool HasKeptDialog,
    string? ShowInFolderReason,
    string? MoveUpReason,
    string? MoveDownReason,
    bool ShowsLocate,
    string? LocateReason,
    string? RemoveReason);

/// <summary>
/// The dashboard's menus (docs/phase-6-design.md 2.5, 2.7, 14.10), data as D86: pure functions of
/// the state they are given. Only what can never apply is left out (D77); what can be explained is
/// disabled with its reason.
/// </summary>
public static class DashboardMenus
{
    /// <summary>
    /// A row's menu (2.7): header the repository's name with its folder as tooltip; <c>Open</c>;
    /// —; <c>Fetch</c>; <c>Pull</c>; <c>Cancel {title}</c> while an operation that could be
    /// cancelled runs; <c>Error details…</c> while a dialog is kept; —; <c>Show in folder</c>;
    /// <c>Copy path</c>; —; <c>Move up</c>; <c>Move down</c>; <c>Locate…</c> for a folder state;
    /// <c>Remove from workspace</c>. The items whose action the list's own keys run on the selected
    /// row show those keys in <paramref name="keys"/>' words (6.7, D192, D215): <c>Open</c> Enter,
    /// <c>Move up</c> and <c>Move down</c> Alt+Up and Alt+Down, <c>Remove from workspace</c> Delete
    /// (Cmd+Backspace, written <c>Cmd+Delete</c>, on macOS); <see cref="KeyTexts.Current"/>'s words
    /// when none are given.
    /// </summary>
    public static MenuModel ForRow(DashboardRowViewModel row, DashboardRowMenuState state, IDashboardRowActions actions, KeyTexts? keys = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);
        var platform = (keys ?? KeyTexts.Current).Platform;
        string Key(Shortcut shortcut) => ShortcutText.Format(shortcut, platform);
        var entries = new List<MenuEntry>
        {
            new MenuHeader(state.Name, state.Folder),
            Item(actions.Log, DashboardTexts.Open, () => actions.Open(row), state.OpenReason) with { KeyText = Key(AppShortcuts.DashboardOpen) },
            new MenuSeparator(),
            Item(actions.Log, DashboardTexts.Fetch, () => actions.FetchAsync(row), state.FetchReason),
            Item(actions.Log, DashboardTexts.Pull, () => actions.PullAsync(row), state.PullReason),
        };
        if (state is { RunningTitle: { } title, RunningIsCancellable: true })
        {
            entries.Add(Item(actions.Log, DashboardTexts.CancelTitle(title), () => actions.Cancel(row), state.CanCancel ? null : DashboardTexts.NoLongerCancellable(title)));
        }

        if (state.HasKeptDialog)
        {
            entries.Add(Item(actions.Log, DashboardTexts.ErrorDetails, () => actions.ShowErrorDetails(row), null));
        }

        entries.Add(new MenuSeparator());
        entries.Add(Item(actions.Log, OperationTexts.MenuTexts.ShowInFolder, () => actions.ShowInFolderAsync(row), state.ShowInFolderReason));
        entries.Add(Item(actions.Log, OperationTexts.MenuTexts.CopyPath, () => actions.CopyPathAsync(row), null));
        entries.Add(new MenuSeparator());
        entries.Add(Item(actions.Log, DashboardTexts.MoveUp, () => actions.MoveUp(row), state.MoveUpReason) with { KeyText = Key(AppShortcuts.DashboardMoveUp) });
        entries.Add(Item(actions.Log, DashboardTexts.MoveDown, () => actions.MoveDown(row), state.MoveDownReason) with { KeyText = Key(AppShortcuts.DashboardMoveDown) });
        if (state.ShowsLocate)
        {
            entries.Add(Item(actions.Log, DashboardTexts.Locate, () => actions.LocateAsync(row), state.LocateReason));
        }

        entries.Add(Item(actions.Log, DashboardTexts.RemoveFromWorkspace, () => actions.Remove(row), state.RemoveReason) with { KeyText = Key(AppShortcuts.DashboardRemove(platform)[0]) });
        return new MenuModel(entries);
    }

    /// <summary>
    /// The header's <c>…</c> menu (2.5): header the workspace's name; <c>Add open tabs</c>; —;
    /// <c>Rename workspace…</c>; <c>Delete workspace…</c> (destructive), each disabled with its
    /// reason (1.5).
    /// </summary>
    public static MenuModel ForHeader(string workspace, string? addOpenTabsReason, string? renameReason, string? deleteReason, IDashboardHeaderActions actions)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(actions);
        return new MenuModel(
        [
            new MenuHeader(workspace),
            Item(actions.Log, DashboardTexts.AddOpenTabs, actions.AddOpenTabs, addOpenTabsReason),
            new MenuSeparator(),
            Item(actions.Log, DashboardTexts.RenameWorkspace, actions.RenameAsync, renameReason),
            Item(actions.Log, DashboardTexts.DeleteWorkspace, actions.DeleteAsync, deleteReason, isDestructive: true),
        ]);
    }

    private static MenuItem Item(IAppLog log, string label, Action action, string? disabledReason) =>
        Item(log, label, () =>
        {
            action();
            return Task.CompletedTask;
        }, disabledReason);

    private static MenuItem Item(IAppLog log, string label, Func<Task> action, string? disabledReason, bool isDestructive = false) =>
        new(label, () => Start(log, label, action), disabledReason is null, disabledReason, isDestructive);

    private static void Start(IAppLog log, string label, Func<Task> action)
    {
        Task task;
        try
        {
            task = action();
        }
        catch (Exception ex)
        {
            log.Error($"The menu item \"{label}\" failed.", ex);
            return;
        }

        _ = task.ContinueWith(
            failed => log.Error($"The menu item \"{label}\" failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
