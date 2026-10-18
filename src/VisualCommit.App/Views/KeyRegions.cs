using Avalonia;
using Avalonia.Controls;

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
/// in 6.6's order (an items control gives its selected or first focusable item). Data only:
/// nothing reads them until work package WP-K's navigation, so no view behaves or draws
/// differently.
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
}
