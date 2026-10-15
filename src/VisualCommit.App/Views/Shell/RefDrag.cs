using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// Marks a control as the start of a branch drag (docs/phase-3-design.md 4g, D87): the left
/// panel's ref rows carry their row (<c>RefItemRow</c>) here, and the drag service (WP8) starts a
/// drag from a press on a control whose source is a local or remote branch. Work package WP0 set
/// it in LeftPanelView.axaml, so that no later package edits that file for the drag.
/// </summary>
public static class RefDrag
{
    /// <summary>What a drag that starts on the control drags: the left panel's row, or null for none.</summary>
    public static readonly AttachedProperty<object?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Source", typeof(RefDrag));

    public static object? GetSource(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(SourceProperty);
    }

    public static void SetSource(Control control, object? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Marks a control inside a drag source that is not part of the drag: the left panel's toggles
    /// of a hidden or soloed row (docs/phase-5-design.md 8.3). A press on it never arms a branch
    /// drag, and a double-click on it is no double-click on its row.
    /// </summary>
    public static readonly AttachedProperty<bool> IsExcludedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsExcluded", typeof(RefDrag));

    public static bool GetIsExcluded(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsExcludedProperty);
    }

    public static void SetIsExcluded(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsExcludedProperty, value);
    }

    /// <summary>Whether <paramref name="source"/> lies in a control marked <see cref="IsExcludedProperty"/>, below <paramref name="row"/> (or anywhere up when it is null).</summary>
    public static bool IsInExcluded(object? source, Visual? row = null)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, row); visual = visual.GetVisualParent())
        {
            if (visual is Control control && GetIsExcluded(control))
            {
                return true;
            }
        }

        return false;
    }
}
