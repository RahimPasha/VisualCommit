using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace VisualCommit.App.Controls;

/// <summary>
/// Draws one of the outlines from Theme/Icons.axaml as a stroke in the inherited text colour, so
/// an icon follows its button's enabled, disabled and theme colours without extra styling.
/// </summary>
public sealed class Icon : Control
{
    /// <summary>The side of the square grid the outlines in Theme/Icons.axaml are drawn on.</summary>
    private const double GridSize = 24;

    private const double StrokeThickness = 1.75;

    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty);
        WidthProperty.OverrideDefaultValue<Icon>(16);
        HeightProperty.OverrideDefaultValue<Icon>(16);
    }

    /// <summary>The outline to draw, on a 24 by 24 grid.</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>The stroke colour. Inherited from the parent, like text colour.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null)
        {
            return;
        }

        var size = Math.Min(Bounds.Width, Bounds.Height);
        var scale = size / GridSize;
        var offset = new Vector((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var pen = new Pen(Foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
        {
            context.DrawGeometry(null, pen, Data);
        }
    }
}
