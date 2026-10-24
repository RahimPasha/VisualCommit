using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views;

/// <summary>The regions F6 and Shift+F6 go round (docs/phase-6-design.md 6.6), in their order.</summary>
public enum KeyRegionRole
{
    /// <summary>No region of its own: the nearest ancestor's.</summary>
    None,

    /// <summary>The left panel (<c>LeftPanelView</c>).</summary>
    Left,

    /// <summary>The graph area (<c>CommitGraphView</c>'s <c>GraphAreaGrid</c>; the activity log inside it is a region of its own).</summary>
    GraphArea,

    /// <summary>The right panel (<c>RightPanelView</c> or <c>WorkingChangesView</c>).</summary>
    Right,

    /// <summary>The activity log (<c>ActivityLogView</c>).</summary>
    ActivityLog,
}

/// <summary>
/// The marks of F6's regions (docs/phase-6-design.md 6.6): <see cref="RoleProperty"/> on each
/// region's root, <see cref="IsMainProperty"/> on the elements a region gives the focus to, tried
/// in 6.6's order (an items control gives its selected or first focusable item). The window's
/// <see cref="KeyRegionNavigator"/> reads them for F6 and Shift+F6.
/// </summary>
public static class KeyRegion
{
    /// <summary>The region whose root the control is; <see cref="KeyRegionRole.None"/> for every other control.</summary>
    public static readonly AttachedProperty<KeyRegionRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<Control, KeyRegionRole>("Role", typeof(KeyRegion));

    /// <summary>Whether the control is one its region gives the focus to (6.6's "where the focus goes").</summary>
    public static readonly AttachedProperty<bool> IsMainProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsMain", typeof(KeyRegion));

    public static KeyRegionRole GetRole(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(RoleProperty);
    }

    public static void SetRole(Control control, KeyRegionRole value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(RoleProperty, value);
    }

    public static bool GetIsMain(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsMainProperty);
    }

    public static void SetIsMain(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsMainProperty, value);
    }

    /// <summary>The region <paramref name="visual"/> lies in: its nearest root's (itself included), or <see cref="KeyRegionRole.None"/>.</summary>
    public static KeyRegionRole RegionOf(Visual? visual) => RootOf(visual) is { } root ? GetRole(root) : KeyRegionRole.None;

    /// <summary>The nearest region root of <paramref name="visual"/> (itself included), or null.</summary>
    public static Control? RootOf(Visual? visual) =>
        visual?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => GetRole(control) != KeyRegionRole.None);
}

/// <summary>
/// F6 and Shift+F6 (docs/phase-6-design.md 6.6, D213): the focus goes round the regions shown, in
/// their order (the left panel, the graph area, the right panel, the activity log), from the one
/// that holds it, wrapping; with none, F6 starts at the first and Shift+F6 at the last. A region
/// gives the focus to its last focused element while that is still visible, enabled and inside
/// it, else to the first of 6.6's marks that can take it (an items control gives its selected or
/// first focusable item), else to its first focusable element; a region with nothing focusable is
/// skipped. The graph area's target is <see cref="CommitGraphView.GraphAreaFocusTarget"/>'s. The
/// focus moves with <see cref="NavigationMethod.Tab"/>, so the keyboard outline shows. One per
/// window.
/// </summary>
public sealed class KeyRegionNavigator
{
    /// <summary>The regions in their order (6.6).</summary>
    private static readonly KeyRegionRole[] Order = [KeyRegionRole.Left, KeyRegionRole.GraphArea, KeyRegionRole.Right, KeyRegionRole.ActivityLog];

    /// <summary>6.6's marks, tried in this order within a region (by name).</summary>
    private static readonly string[] Marks =
    [
        "RefList", "FilterBox", "GraphRows", "DiffText", "FileHistoryList", "BlameText", "ReflogList", "RebaseRows", "ConflictResult",
        "OpenRepoButton", "FileList", "CompareFileList", "MultiSelectionList", "UnstagedFileList", "StagedFileList", "CommitSummary",
        "ActivityLogList", "DashboardList",
    ];

    private readonly Dictionary<KeyRegionRole, WeakReference<InputElement>> _last = [];

    /// <summary>The focus moved to <paramref name="source"/> (the window's <c>GotFocus</c>): its region remembers it.</summary>
    public void Remember(object? source)
    {
        if (source is InputElement element && KeyRegion.RegionOf(element) is var role and not KeyRegionRole.None)
        {
            _last[role] = new WeakReference<InputElement>(element);
        }
    }

    /// <summary>
    /// Moves the focus to the next region of <paramref name="window"/> after the one holding
    /// <paramref name="focused"/>, or with <paramref name="backwards"/> the previous; returns whether
    /// it moved.
    /// </summary>
    public bool Move(Visual window, IInputElement? focused, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(window);
        var regions = ShownRegions(window);
        if (regions.Count == 0)
        {
            return false;
        }

        var current = regions.FindIndex(region => region.Role == KeyRegion.RegionOf(focused as Visual));
        var step = backwards ? -1 : 1;

        // From no region, F6 starts at the first shown and Shift+F6 at the last; the region that
        // holds the focus comes last, wrapping round.
        var start = current >= 0 ? current + step : backwards ? regions.Count - 1 : 0;
        for (var i = 0; i < regions.Count; i++)
        {
            var (role, root) = regions[(((start + (step * i)) % regions.Count) + regions.Count) % regions.Count];
            if (TargetIn(role, root) is { } target && target.Focus(NavigationMethod.Tab))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The element <paramref name="root"/>'s region gives the focus to, or null when nothing in it can take it.</summary>
    public InputElement? TargetIn(KeyRegionRole role, Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_last.TryGetValue(role, out var last) && last.TryGetTarget(out var remembered)
            && remembered is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true }
            && remembered.IsAttachedToVisualTree() && ReferenceEquals(KeyRegion.RootOf(remembered), root))
        {
            return remembered;
        }

        if (role == KeyRegionRole.GraphArea)
        {
            return root.GetSelfAndVisualAncestors().OfType<CommitGraphView>().FirstOrDefault()?.GraphAreaFocusTarget() is { } graphArea
                && graphArea.IsEffectivelyVisible && ReferenceEquals(KeyRegion.RootOf(graphArea), root)
                ? graphArea
                : FirstFocusable(root, root);
        }

        var marks = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => KeyRegion.GetIsMain(control) && control.IsEffectivelyVisible && ReferenceEquals(KeyRegion.RootOf(control), root))
            .OrderBy(control => Array.IndexOf(Marks, control.Name) is var index and >= 0 ? index : Marks.Length);
        foreach (var mark in marks)
        {
            if (TargetOf(mark, root) is { } target)
            {
                return target;
            }
        }

        return FirstFocusable(root, root);
    }

    /// <summary>The regions shown now, in their order, each with its root (the first visible one of its role).</summary>
    private static List<(KeyRegionRole Role, Control Root)> ShownRegions(Visual window)
    {
        var roots = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => KeyRegion.GetRole(control) != KeyRegionRole.None && control.IsEffectivelyVisible && control.Bounds is { Width: > 0, Height: > 0 })
            .ToList();
        var regions = new List<(KeyRegionRole, Control)>();
        foreach (var role in Order)
        {
            if (roots.FirstOrDefault(root => KeyRegion.GetRole(root) == role) is { } root)
            {
                regions.Add((role, root));
            }
        }

        return regions;
    }

    /// <summary>
    /// What a marked control gives the focus to: an items control its selected item, else its
    /// first, as the item or its first focusable part; a scroll viewer or a control that takes no
    /// focus its first focusable part; any other control itself.
    /// </summary>
    private static InputElement? TargetOf(Control mark, Control root)
    {
        if (mark is ItemsControl items)
        {
            var containers = items.GetRealizedContainers().OrderBy(items.IndexFromContainer).ToList();
            var selected = items is SelectingItemsControl { SelectedIndex: >= 0 } selecting ? selecting.ContainerFromIndex(selecting.SelectedIndex) : null;
            selected ??= containers.FirstOrDefault(IsMarkedSelected);
            foreach (var container in selected is null ? containers : containers.Prepend(selected))
            {
                if (container is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element)
                {
                    return element;
                }

                if (FirstFocusable(container, root) is { } part)
                {
                    return part;
                }
            }

            return null;
        }

        if (mark is ScrollViewer || !mark.Focusable)
        {
            return FirstFocusable(mark, root);
        }

        return mark.IsEffectivelyEnabled ? mark : null;
    }

    /// <summary>Whether a container shows its item selected: it or a part of it has the class <c>selected</c>.</summary>
    private static bool IsMarkedSelected(Control container) =>
        container.Classes.Contains("selected") || container.GetVisualDescendants().OfType<Control>().Any(part => part.Classes.Contains("selected"));

    /// <summary>The first element in <paramref name="within"/> that can take the focus by Tab, in the visual tree's order, inside <paramref name="root"/>'s region.</summary>
    private static InputElement? FirstFocusable(Visual? within, Control root) =>
        within?.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element => element is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true }
                && KeyboardNavigation.GetIsTabStop(element)
                && ReferenceEquals(KeyRegion.RootOf(element), root));
}
