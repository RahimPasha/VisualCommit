using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Pages;

namespace VisualCommit.App.ViewModels;

// The repository's part of the command palette (docs/phase-6-design.md 5.4, 5.5): its menus'
// items as palette entries, built by the same RepositoryMenus builders on one snapshot.
public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// The palette's items of this repository (5.5) on <paramref name="context"/>, its
    /// <see cref="MenuContext"/> as taken when the palette opened or as the worktrees' read gave it:
    /// its refs' and the selection's menus, the working changes', the open diff's and the open
    /// page's selected row's, with <paramref name="keys"/>' key texts.
    /// </summary>
    public IReadOnlyList<PaletteEntry> PaletteItems(KeyTexts keys, RepositoryMenuContext context, IReadOnlyList<PaletteEntry> commands)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commands);
        if (Refs is null)
        {
            return [];
        }

        return AppCommands.RepositoryItems(context, MenuActions, keys, HasWorkingChanges, DiffMenuForPalette(context), PageMenuForPalette(), commands);
    }

    /// <summary>
    /// The open diff's menu as <see cref="OpenDiffMenu"/> builds it (<see cref="DiffMenu"/>),
    /// without a hunk (5.5): its selected lines' items when lines are selected, its file's items,
    /// and the file history and blame of its file, which Mod+Shift+H and Mod+Shift+B run (6.1's
    /// rows 31 and 32); null without a diff. Built at every opening of the palette and every such
    /// key on the UI thread, it reads no disk: whether the file is in the working tree is the
    /// diff's own, as its last read found it (<see cref="DiffViewModel.ExistsInWorkingTree"/>).
    /// </summary>
    public MenuModel? DiffMenuForPalette(RepositoryMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Diff is { } diff && !IsClosed ? DiffMenu(context, diff, existsInWorkingTree: diff.ExistsInWorkingTree) : null;
    }

    /// <summary>The open page's selected row's menu (5.5): the file history's row, the blame's run, the reflog's row; null without one.</summary>
    private MenuModel? PageMenuForPalette() => Page switch
    {
        FileHistoryViewModel { SelectedRow: { } row } history => history.MenuFor(row),
        BlameViewModel { SelectedRun: { } run } blame => blame.MenuFor(run),
        ReflogViewModel { SelectedRow: { } row } reflog => reflog.MenuFor(row),
        _ => null,
    };
}
