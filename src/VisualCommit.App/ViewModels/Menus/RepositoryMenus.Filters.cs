// Phase 5's compare, hide and solo items (docs/phase-5-design.md 6.2, 8.3, 13.1) and the
// comparison's file menu: the builders of RepositoryMenus.cs call the hooks below where 13.1 puts
// their items. Work package WP7 fills them, and adds ForCompareFile here, with their texts in
// OperationTexts.Menus.Filters.cs. Until then each hook gives nothing, so no menu changes.

using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// "Compare {b} with {cur}" (6.2): in a local branch's menu (not the current branch's), a remote
    /// branch's and a tag's (<c>Compare {t} with {cur}</c>), first of its group before the copy item.
    /// </summary>
    private static MenuItem? CompareItem(RepositoryMenuContext context, GitRef reference, IRepositoryActions actions) => null;

    /// <summary>"Hide {b}" or "Show {b}", "Solo {b}" or "Stop soloing {b}" (8.3): a local or remote branch's, after its compare.</summary>
    private static IEnumerable<MenuItem?> HideAndSoloItems(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions) => [];

    /// <summary>"Hide every branch of {r}" or "Show every branch of {r}" (8.3): in a remote's menu, a group before "Copy URL".</summary>
    private static MenuItem? RemoteFilterItem(RepositoryMenuContext context, string remote, IRepositoryActions actions) => null;

    /// <summary>"Show all hidden branches" and "Stop soloing" (8.3): after the items of the Local branches and Remotes sections' menus.</summary>
    private static IEnumerable<MenuItem?> SectionFilterItems(RepositoryMenuContext context, IRepositoryActions actions) => [];

    /// <summary>"Compare {A} with {B}" (6.2): a drop's, in a group of its own at the end.</summary>
    private static MenuItem? DropCompareItem(RepositoryMenuContext context, string sourceRef, string targetRef, IRepositoryActions actions) => null;

    /// <summary>
    /// A file of a comparison's list (6.4, FD21), built as a commit's file is: header the file
    /// name; File history and Blame (at To, <see cref="FileMenuTarget.CommitSha"/>); then Open
    /// file, Show in folder, Copy path. A right-click opens it only, selecting no row.
    /// </summary>
    public static MenuModel ForCompareFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(Header(file.FileName), FileHistoryItems(context, file, DiffSide.Commit, actions), FileItems(file, actions));
    }
}
