using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// The diff view's colours and code font, looked up in the theme resources for a control's theme
/// variant, as <c>CommitGraphControl</c> does. Resolved again when the theme changes.
/// </summary>
internal sealed class DiffPalette
{
    /// <summary>The size of code, gutter numbers and hunk header text.</summary>
    public const double CodeFontSize = 12;

    private readonly Dictionary<Color, IImmutableBrush> _syntaxBrushes = [];

    public required bool IsDark { get; init; }

    public required IImmutableBrush Background { get; init; }

    public required IImmutableBrush Added { get; init; }

    public required IImmutableBrush AddedWord { get; init; }

    public required IImmutableBrush Removed { get; init; }

    public required IImmutableBrush RemovedWord { get; init; }

    public required IImmutableBrush Hunk { get; init; }

    public required IImmutableBrush Filler { get; init; }

    public required IImmutableBrush TextPrimary { get; init; }

    public required IImmutableBrush TextSecondary { get; init; }

    public required IImmutableBrush Selection { get; init; }

    public required IImmutableBrush Border { get; init; }

    public required FontFamily CodeFont { get; init; }

    public static DiffPalette Resolve(Control control)
    {
        var theme = control.ActualThemeVariant;

        IImmutableBrush Brush(string key) =>
            control.TryFindResource(key, theme, out var value) && value is IBrush brush
                ? brush.ToImmutable()
                : Brushes.Transparent.ToImmutable();

        var font = control.TryFindResource("VcCodeFontFamily", theme, out var value) && value is FontFamily family
            ? family
            : FontFamily.Default;

        return new DiffPalette
        {
            IsDark = theme != Avalonia.Styling.ThemeVariant.Light,
            Background = Brush("VcWindowBackgroundBrush"),
            Added = Brush("VcDiffAddedBrush"),
            AddedWord = Brush("VcDiffAddedWordBrush"),
            Removed = Brush("VcDiffRemovedBrush"),
            RemovedWord = Brush("VcDiffRemovedWordBrush"),
            Hunk = Brush("VcDiffHunkBrush"),
            Filler = Brush("VcDiffFillerBrush"),
            TextPrimary = Brush("VcTextPrimaryBrush"),
            TextSecondary = Brush("VcTextSecondaryBrush"),
            Selection = Brush("VcSelectionBrush"),
            Border = Brush("VcBorderBrush"),
            CodeFont = font,
        };
    }

    /// <summary>A row's background: by the kind of the line on that side; a filler (no cell) has the filler colour.</summary>
    public IImmutableBrush RowBackground(DiffDisplayRow row, DiffCell? cell)
    {
        if (row.Kind == DiffRowKind.HunkHeader)
        {
            return Hunk;
        }

        if (cell is null)
        {
            return Filler;
        }

        if (row.Kind == DiffRowKind.NoNewline)
        {
            return Background;
        }

        return cell.Kind switch
        {
            DiffLineKind.Added => Added,
            DiffLineKind.Removed => Removed,
            _ => Background,
        };
    }

    /// <summary>The brush of a syntax colour, made once per colour.</summary>
    public IImmutableBrush SyntaxBrush(Color color)
    {
        if (!_syntaxBrushes.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(color);
            _syntaxBrushes[color] = brush;
        }

        return brush;
    }
}
