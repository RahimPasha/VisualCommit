using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VisualCommit.App.Controls;

/// <summary>A toolbar button: an icon above a short label. Styled in Theme/Controls.axaml.</summary>
public sealed class ToolbarButton : Button
{
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<ToolbarButton, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ToolbarButton, string?>(nameof(Label));

    /// <summary>The icon outline, one of the geometries in Theme/Icons.axaml.</summary>
    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    /// <summary>The text under the icon.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ToolbarButton);
}
