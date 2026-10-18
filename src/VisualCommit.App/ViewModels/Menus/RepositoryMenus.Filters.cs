// Phase 5's compare, hide and solo items (docs/phase-5-design.md 6.2, 8.3, 13.1) and the
// comparison's file menu (6.4, FD21): the builders of RepositoryMenus.cs call the hooks below where
// 13.1 puts their items, with their texts in OperationTexts.Menus.Filters.cs. A comparison names
// its sides by full ref names (FD20): From the current branch (or a drop's target), To the ref the
// menu is on (or a drop's source).

using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// "Compare {b} with {cur}" (6.2): in a local branch's menu (not the current branch's), a remote
    /// branch's and a tag's (<c>Compare {t} with {cur}</c>), first of its group before the copy item.
    /// Disabled while HEAD is unborn (12.4).
    /// </summary>
    private static MenuItem? CompareItem(RepositoryMenuContext context, GitRef reference, IRepositoryActions actions)
    {
        var from = context.CurrentBranch?.FullName ?? (context.Head.BranchName is { } branch ? HeadsPrefix + branch : "HEAD");
        var to = reference.FullName;
        return Item(actions, CompareWith(reference.Name, context.CurrentName), () => actions.CompareBranchesAsync(from, to), context.IsUnborn ? HeadIsUnborn : null);
    }

    /// <summary>
    /// "Hide {b}" or "Show {b}", "Solo {b}" or "Stop soloing {b}" (8.1, 8.3): a local or remote
    /// branch's, after its compare. Hide is disabled on the checked-out branch, which is always shown.
    /// </summary>
    private static IEnumerable<MenuItem?> HideAndSoloItems(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions)
    {
        var name = branch.Name;
        var fullName = branch.FullName;
        var filters = context.Filters;
        var hidden = filters?.Hidden.Contains(fullName, StringComparer.Ordinal) == true;
        var soloed = filters?.Soloed.Contains(fullName, StringComparer.Ordinal) == true;
        return
        [
            hidden
                ? Item(actions, ShowLabel(name), () => actions.ShowBranchAsync(fullName))
                : Item(actions, HideLabel(name), () => actions.HideBranchAsync(fullName), IsCurrent(context, branch) ? CheckedOutBranchAlwaysShown : null),
            soloed
                ? Item(actions, StopSoloingLabel(name), () => actions.StopSoloingBranchAsync(fullName))
                : Item(actions, SoloLabel(name), () => actions.SoloBranchAsync(fullName)),
        ];
    }

    /// <summary>"Hide every branch of {r}" or "Show every branch of {r}" (8.1, 8.3): in a remote's menu, a group before "Copy URL".</summary>
    private static MenuItem? RemoteFilterItem(RepositoryMenuContext context, string remote, IRepositoryActions actions) =>
        context.Filters?.HiddenRemotes.Contains(remote, StringComparer.Ordinal) == true
            ? Item(actions, ShowEveryBranchOf(remote), () => actions.ShowRemoteAsync(remote))
            : Item(actions, HideEveryBranchOf(remote), () => actions.HideRemoteAsync(remote));

    /// <summary>"Show all hidden branches" and "Stop soloing" (8.3): after the items of the Local branches and Remotes sections' menus, disabled with their reasons when there is nothing to undo (12.4).</summary>
    private static IEnumerable<MenuItem?> SectionFilterItems(RepositoryMenuContext context, IRepositoryActions actions)
    {
        var filters = context.Filters;
        var anyHidden = filters is not null && (filters.Hidden.Count > 0 || filters.HiddenRemotes.Count > 0);
        var anySoloed = filters is not null && filters.Soloed.Count > 0;
        return
        [
            Item(actions, ShowAllHiddenBranches, actions.ShowAllHiddenBranchesAsync, anyHidden ? null : NoBranchIsHidden),
            Item(actions, StopSoloing, actions.StopSoloingAsync, anySoloed ? null : NoBranchIsSoloed),
        ];
    }

    /// <summary>"Compare {A} with {B}" (6.2): a drop's, in a group of its own at the end; From is B (the target), To is A (the source).</summary>
    private static MenuItem? DropCompareItem(RepositoryMenuContext context, string sourceRef, string targetRef, IRepositoryActions actions)
    {
        var source = context.FindRef(sourceRef)?.Name ?? ShortName(sourceRef);
        var target = context.FindRef(targetRef)?.Name ?? ShortName(targetRef);
        return Item(actions, CompareWith(source, target), () => actions.CompareBranchesAsync(targetRef, sourceRef));
    }

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
        return Build(Header(file.FileName), FileHistoryItems(context, file, DiffSide.Commit, actions), FileItems(file, context, actions));
    }
}
