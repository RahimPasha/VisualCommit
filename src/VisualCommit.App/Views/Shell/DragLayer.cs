using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using VisualCommit.App.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The layer a dragged branch's pill is drawn on (docs/phase-3-design.md 4g, D87): a canvas over
/// everything but the dialog layer that never takes input itself, so that the drop target under
/// the pointer is still found. It makes the window's <see cref="BranchDragService"/>, puts it in
/// the window's Esc chain, and draws what the service says: the pill with the branch's name in
/// its label style at 85% opacity, 8 right of and 8 below the pointer, and a 2-pixel outline in
/// <c>VcDragTargetBrush</c> around a valid target under the pointer (1 pixel away from a label,
/// inside a row).
/// </summary>
public class DragLayer : Canvas
{
    /// <summary>The pill's opacity (4g).</summary>
    public const double PillOpacity = 0.85;

    /// <summary>How far right of and below the pointer the pill's top-left corner is (4g).</summary>
    public const double PillOffset = 8;

    /// <summary>The thickness of a target's outline (4g).</summary>
    public const double OutlineThickness = 2;

    /// <summary>
    /// The space between a label and its outline: a local label is filled with its lane colour,
    /// which for lane 0 is the accent the outline is drawn in, so the two would run together.
    /// </summary>
    public const double LabelOutlineGap = 1;

    private const double LabelCornerRadius = 4;
    private const double IconGridSize = 24;
    private const double LabelIconStroke = 1.3;

    private readonly DragVisual _visual;
    private BranchDragService? _service;
    private IDisposable? _escape;

    public DragLayer()
    {
        IsHitTestVisible = false;
        _visual = new DragVisual(this);
        Children.Add(_visual);
    }

    /// <summary>The window's drag service, once the layer is in a window.</summary>
    public BranchDragService? Service => _service;

    /// <summary>Where the pill is drawn while a branch is dragged, in this layer's coordinates; null otherwise.</summary>
    public Rect? PillBounds { get; private set; }

    /// <summary>Where the target's outline is drawn (its outer edge), in this layer's coordinates; null without a target.</summary>
    public Rect? OutlineBounds { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _service = new BranchDragService(topLevel, this);
        _service.Changed += (_, _) => _visual.InvalidateVisual();
        if (topLevel is MainWindow window)
        {
            _escape = window.AddEscapeHandler(_service, EscapeOrder.Drag);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _escape?.Dispose();
        _escape = null;
        _service?.Dispose();
        _service = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // The drawing covers the whole layer, wherever the pointer goes.
        _visual.Arrange(new Rect(finalSize));
        return size;
    }

    /// <summary>Draws the drag (a panel's own rendering cannot be overridden): the target's outline, then the pill over it.</summary>
    private void Draw(DrawingContext context)
    {
        PillBounds = null;
        OutlineBounds = null;
        if (_service is not { IsDragging: true, Source: { } source } service || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var theme = ActualThemeVariant;
        IImmutableBrush Brush(string key) =>
            this.TryFindResource(key, theme, out var value) && value is IBrush brush ? brush.ToImmutable() : Brushes.Transparent.ToImmutable();

        if (service.Target is { } target && target.Owner.TranslatePoint(target.Bounds.TopLeft, this) is { } topLeft)
        {
            // Around a label, which is small; inside a row, which fills the panel's width.
            var bounds = new Rect(topLeft, target.Bounds.Size);
            var outer = target.IsLabel ? bounds.Inflate(LabelOutlineGap + OutlineThickness) : bounds;
            var radius = target.IsLabel ? LabelCornerRadius + LabelOutlineGap + OutlineThickness : 0;
            var pen = new ImmutablePen(Brush("VcDragTargetBrush"), OutlineThickness);
            context.DrawRectangle(null, pen, new RoundedRect(outer.Deflate(OutlineThickness / 2), Math.Max(0, radius - (OutlineThickness / 2))));
            OutlineBounds = outer;
        }

        if (topLevel.TranslatePoint(service.Position, this) is not { } pointer)
        {
            return;
        }

        DrawPill(context, source, service.SourceColor, pointer + new Point(PillOffset, PillOffset), Brush);
    }

    /// <summary>The pill: a local branch filled with its lane colour, a remote one outlined in it, as the graph draws their labels.</summary>
    private void DrawPill(DrawingContext context, DragSource source, int color, Point topLeft, Func<string, IImmutableBrush> brush)
    {
        var lane = brush("VcLane" + color.ToString(CultureInfo.InvariantCulture) + "Brush");
        var textBrush = source.IsRemote ? brush("VcTextPrimaryBrush") : brush("VcLabelTextBrush");
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.SemiBold);
        using var text = new TextLayout(source.Name, typeface, CommitGraphControl.LabelFontSize, textBrush, textWrapping: TextWrapping.NoWrap, maxLines: 1);
        var icon = source.IsCurrent && this.TryFindResource("IconCheck", ActualThemeVariant, out var found) ? found as Geometry : null;
        var width = CommitGraphControl.LabelPadding
            + (icon is null ? 0 : CommitGraphControl.LabelIconSize + CommitGraphControl.LabelIconGap)
            + text.WidthIncludingTrailingWhitespace
            + CommitGraphControl.LabelPadding;
        var bounds = new Rect(topLeft, new Size(Math.Ceiling(width), CommitGraphControl.LabelHeight));
        PillBounds = bounds;

        using (context.PushOpacity(PillOpacity))
        {
            if (source.IsRemote)
            {
                // Outlined, on the graph's background as a remote label sits on it.
                context.DrawRectangle(brush("VcWindowBackgroundBrush"), new ImmutablePen(lane, 1), new RoundedRect(bounds.Deflate(0.5), LabelCornerRadius - 0.5));
            }
            else
            {
                context.DrawRectangle(lane, null, new RoundedRect(bounds, LabelCornerRadius));
            }

            var x = bounds.X + CommitGraphControl.LabelPadding;
            if (icon is not null)
            {
                var scale = CommitGraphControl.LabelIconSize / IconGridSize;
                var iconTop = bounds.Y + ((CommitGraphControl.LabelHeight - CommitGraphControl.LabelIconSize) / 2);
                var pen = new ImmutablePen(textBrush, LabelIconStroke * IconGridSize / CommitGraphControl.LabelIconSize, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, iconTop)))
                {
                    context.DrawGeometry(null, pen, icon);
                }

                x += CommitGraphControl.LabelIconSize + CommitGraphControl.LabelIconGap;
            }

            text.Draw(context, new Point(x, bounds.Y + ((CommitGraphControl.LabelHeight - text.Height) / 2)));
        }
    }

    /// <summary>The layer's one child, as large as the layer, which draws the drag.</summary>
    private sealed class DragVisual(DragLayer owner) : Control
    {
        public override void Render(DrawingContext context) => owner.Draw(context);
    }
}
