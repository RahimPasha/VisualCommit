using Avalonia;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Controls;

/// <summary>
/// The graph's part in dragging a branch onto a branch (docs/phase-3-design.md 4g, D87): its
/// local and remote branch labels are drag sources and drop targets, and the label being dragged
/// is drawn at half opacity. <see cref="BranchDragService"/> runs the drag.
/// </summary>
public sealed partial class CommitGraphControl
{
    /// <summary>The opacity of the label being dragged (4g).</summary>
    public const double DraggedSourceOpacity = 0.5;

    /// <summary>The label being dragged: its row's commit and its full ref name; null when none.</summary>
    private (string Sha, string RefName)? _draggedLabel;

    /// <summary>The lane colour number (0 to 7) the node of <paramref name="sha"/>'s row is drawn in, which its labels are filled or outlined with; null when it is not in the graph.</summary>
    public int? LabelColorOf(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return Data is { } data && data.IndexOf(sha) is >= 0 and var index ? Modulo(data.RowAt(index).NodeColor) : null;
    }

    /// <summary>
    /// The drop target at <paramref name="point"/> (in the control's coordinates): a local or
    /// remote branch label, not the "+N" label, a tag, a stash or HEAD. Null elsewhere.
    /// </summary>
    public DropTarget? DropTargetAt(Point point)
    {
        if (HitTest(point) is not { Kind: GraphHitKind.Label, Label: { } label } || BranchOf(label) is not { } branch)
        {
            return null;
        }

        return new DropTarget(branch.RefName, label.FullText, branch.IsRemote, this, label.Bounds, IsLabel: true);
    }

    /// <summary>A press on a branch label may start a drag of that branch, once it moves far enough (4g); not with a modifier held.</summary>
    private void TryBeginDrag(Point position, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None
            || HitTest(position) is not { Kind: GraphHitKind.Label, Sha: { } sha, Label: { } label }
            || BranchOf(label) is not { } branch
            || BranchDragService.For(this) is not { } service)
        {
            return;
        }

        var source = new DragSource(
            branch.RefName,
            label.FullText,
            branch.IsRemote,
            label.Kind == RefLabelKind.CurrentBranch,
            sha,
            dimmed =>
            {
                _draggedLabel = dimmed ? (sha, branch.RefName) : null;
                InvalidateVisual();
            });
        service.TryBegin(source, e);
    }

    /// <summary>The branch a label stands for: its full ref name and whether it is a remote branch; null for any other label.</summary>
    private static (string RefName, bool IsRemote)? BranchOf(PlacedLabel label) =>
        !label.IsOverflow && label.RefName is { } refName && label.Kind is RefLabelKind.LocalBranch or RefLabelKind.CurrentBranch or RefLabelKind.RemoteBranch
            ? (refName, label.Kind == RefLabelKind.RemoteBranch)
            : null;
}
