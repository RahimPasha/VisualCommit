using Avalonia;
using Avalonia.Controls;

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
}
