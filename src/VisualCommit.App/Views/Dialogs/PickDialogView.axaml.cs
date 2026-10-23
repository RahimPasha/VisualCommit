using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The pick dialog (<c>PickDialog</c>, docs/phase-3-design.md 2.21, 4f). The filter box takes the
/// focus. Since phase 6 an item may show a detail after its text (docs/phase-6-design.md 7.7),
/// which is also the item's help text; the item keeps its id and its name, the text.
/// </summary>
public partial class PickDialogView : DialogView
{
    public PickDialogView()
    {
        InitializeComponent();

        // Each PickItem's help text is its option's detail (7.7, 15.5); a container is prepared again
        // whenever it shows another item.
        PickList.ContainerPrepared += (_, e) =>
            AutomationProperties.SetHelpText(e.Container, (DataContext as PickDialogViewModel)?.DetailOf(e.Container.DataContext as string));
    }

    /// <summary>The detail of an item: the item's text and the dialog's view model in, the option's detail (or null) out.</summary>
    public static IMultiValueConverter DetailConverter { get; } = new DetailOfItem();

    private sealed class DetailOfItem : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Count == 2 && values[1] is PickDialogViewModel dialog ? dialog.DetailOf(values[0] as string) : null;
    }
}

/// <summary>
/// A pick item's row (docs/phase-6-design.md 7.7): its text at its own width, cut with "…" only
/// when the row is too narrow for it alone, then 12 apart the detail in what is left, which cuts
/// the detail (at its start) rather than the text. Two children: the text, then the detail; a
/// hidden detail takes no room, so an item without one is laid out as phase 3's.
/// </summary>
public sealed class PickRowPanel : Panel
{
    /// <summary>The space between the text and the detail.</summary>
    public const double Gap = 12;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }

        var text = Children[0];
        text.Measure(availableSize);
        var width = text.DesiredSize.Width;
        var height = text.DesiredSize.Height;
        if (Children.Count > 1 && Children[1] is { IsVisible: true } detail)
        {
            var left = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - width - Gap);
            detail.Measure(new Size(left, availableSize.Height));
            width += Gap + detail.DesiredSize.Width;
            height = Math.Max(height, detail.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        var text = Children[0];
        var textWidth = Math.Min(text.DesiredSize.Width, finalSize.Width);
        text.Arrange(Centred(0, textWidth, text.DesiredSize.Height, finalSize.Height));
        if (Children.Count > 1 && Children[1] is { IsVisible: true } detail)
        {
            var x = textWidth + Gap;
            detail.Arrange(Centred(x, Math.Max(0, finalSize.Width - x), detail.DesiredSize.Height, finalSize.Height));
        }

        return finalSize;
    }

    private static Rect Centred(double x, double width, double height, double rowHeight) =>
        new(x, Math.Max(0, (rowHeight - height) / 2), width, Math.Min(height, rowHeight));
}
