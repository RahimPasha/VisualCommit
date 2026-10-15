using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Several commits selected (docs/phase-3-design.md 4h, D88) and the branch drop's menu (4g):
/// the details panel's list while three or more commits are selected (two compare, D162:
/// <c>ShowMultiSelection</c> in RepositoryViewModel.Find.cs), and the selection mapped again by
/// its SHAs when a reloaded history replaces the graph.
/// </summary>
public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// Makes the menu for a branch dropped onto a branch (4g), which the window's drag service
    /// opens at the drop point; null, or a function that returns null, opens nothing. The menus'
    /// package (WP7) fills it.
    /// </summary>
    public Func<BranchDrop, MenuModel?>? BranchDropMenu { get; set; }

    /// <summary>The selected commits, as their rows of the graph show them now, in row order.</summary>
    public IReadOnlyList<CommitInfo> SelectedCommits =>
        [.. Selection.Shas
            .Select(sha => Graph.IndexOf(sha))
            .Where(index => index >= 0)
            .Order()
            .Select(index => Graph.CommitAt(index))];

    partial void OnSelectionChanged(GraphSelection oldValue, GraphSelection newValue) => ShowMultiSelection(newValue);

    /// <summary>
    /// A reloaded history replaced the graph (4h): SHAs it no longer has are dropped (it is
    /// complete, so none waits for a later page); with fewer than two left the multi-selection
    /// ends and the one left, if any, is the single selection.
    /// </summary>
    partial void OnGraphSwapped(CommitGraphData data)
    {
        var selection = Selection;
        if (!selection.IsMultiple)
        {
            return;
        }

        var kept = selection.Shas.Where(sha => data.IndexOf(sha) >= 0).ToList();
        var mapped = kept.Count switch
        {
            0 => GraphSelection.Empty,
            1 => new GraphSelection(kept, anchor: kept[0]),
            _ => new GraphSelection(kept, selection.Anchor, selection.Focus),
        };

        if (mapped.Equals(selection))
        {
            // The same commits on other rows: the list follows the new order.
            ShowMultiSelection(selection);
        }
        else
        {
            Selection = mapped;
        }
    }
}
