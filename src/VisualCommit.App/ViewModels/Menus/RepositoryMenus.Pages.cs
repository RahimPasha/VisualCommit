// Phase 5's file history, blame, reflog and backups items (docs/phase-5-design.md 4.4, 5.1, 5.2,
// 5.3, 7.10, 13.1) and the menus of their pages: the builders of RepositoryMenus.cs call the
// hooks below where 13.1 puts their items. Work package WP6 fills them, and adds the page
// builders (ForFileHistoryRow, ForBlameRun, ForReflogEntry, ForBackup) here, with their texts in
// OperationTexts.Menus.Pages.cs. Until then each hook gives nothing, so no menu changes.

using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// "File history" and "Blame" (4.4), a group of their own before "Open file" in the file
    /// menus: of the stage lists' files (<paramref name="side"/> Unstaged or Staged), a commit's
    /// files and a diff's. Left out in the conflict resolver's header menu
    /// (<see cref="FileMenuTarget.InConflictResolver"/>) and for a nested repository.
    /// </summary>
    private static IEnumerable<MenuItem?> FileHistoryItems(RepositoryMenuContext context, FileMenuTarget file, DiffSide side, IRepositoryActions actions) => [];

    /// <summary>"Show reflog" and "Show backups" (5.1): in the working-changes row's, the empty graph area's and the Local branches section's menus.</summary>
    private static IEnumerable<MenuItem?> ReflogItems(RepositoryMenuContext context, IRepositoryActions actions) => [];

    /// <summary>"Show reflog of {b}" (5.1): in a local branch's menu, after its compare; null for a remote branch.</summary>
    private static MenuItem? BranchReflogItem(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions) => null;
}
