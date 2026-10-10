using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Rendering;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// Makes a header row's line 26 high: its text (git's header line, kept in the document so that a
/// copied selection has it) is replaced by an empty inline object of that height. What the row
/// shows is drawn by the <see cref="HunkHeaderLayer"/>, which does not scroll sideways.
/// </summary>
internal sealed class HunkHeaderGenerator(DiffPane pane) : VisualLineElementGenerator
{
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.VisualLine.FirstDocumentLine;
        return startOffset == line.Offset && pane.RowAtLine(line.LineNumber)?.Kind == DiffRowKind.HunkHeader ? startOffset : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var line = CurrentContext.VisualLine.FirstDocumentLine;
        var textView = CurrentContext.TextView;
        var spacer = new Border { Width = 0, Height = DiffPane.HeaderHeight, IsHitTestVisible = false };

        // An inline object stands on the text's baseline; the line's text still reaches the font's
        // descent below it. Raising the object by that much makes the line exactly its height.
        var descent = textView.DefaultLineHeight - textView.DefaultBaseline;
        TextBlock.SetBaselineOffset(spacer, DiffPane.HeaderHeight - descent);
        return new InlineObjectElement(line.Length, spacer);
    }
}

/// <summary>
/// A layer of the text view, over the text, that holds a <see cref="HunkHeaderBar"/> for each header
/// row in view, as wide as the view and placed on its row. The bars are real controls, so their
/// buttons can be clicked and UI Automation finds them; they are kept in hunk order, so that the
/// first hunk's buttons come first.
/// </summary>
internal sealed class HunkHeaderLayer : Panel
{
    private readonly DiffPane _pane;
    private readonly SortedDictionary<int, HunkHeaderBar> _bars = [];

    public HunkHeaderLayer(DiffPane pane)
    {
        _pane = pane;
        ClipToBounds = true;
        pane.TextView.VisualLinesChanged += (_, _) => Update();
        pane.TextView.ScrollOffsetChanged += (_, _) => InvalidateArrange();
    }

    /// <summary>Drops every bar and makes them again: the theme, the buttons or the rows changed.</summary>
    public void Rebuild()
    {
        _bars.Clear();
        Children.Clear();
        Update();
    }

    /// <summary>Makes bars for the header rows now in view and drops those scrolled away.</summary>
    public void Update()
    {
        var textView = _pane.TextView;
        var inView = new HashSet<int>();
        if (textView.VisualLinesValid)
        {
            foreach (var line in textView.VisualLines)
            {
                var index = line.FirstDocumentLine.LineNumber - 1;
                if (_pane.RowAtLine(index + 1)?.Kind == DiffRowKind.HunkHeader)
                {
                    inView.Add(index);
                }
            }
        }

        var changed = false;
        foreach (var row in _bars.Keys.Where(row => !inView.Contains(row)).ToList())
        {
            Children.Remove(_bars[row]);
            _bars.Remove(row);
            changed = true;
        }

        foreach (var row in inView.Where(row => !_bars.ContainsKey(row)))
        {
            _bars[row] = new HunkHeaderBar(_pane, row);
            changed = true;
        }

        if (changed)
        {
            // Rows grow with their hunk, so row order is hunk order.
            Children.Clear();
            Children.AddRange(_bars.Values);
        }

        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        foreach (var bar in _bars.Values)
        {
            bar.Measure(new Size(width, DiffPane.HeaderHeight));
        }

        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (row, bar) in _bars)
        {
            if (_pane.VisualLineOf(row) is { } line)
            {
                bar.Arrange(new Rect(0, _pane.TopOf(line), finalSize.Width, DiffPane.HeaderHeight));
            }
            else
            {
                bar.Arrange(new Rect(0, -DiffPane.HeaderHeight, finalSize.Width, DiffPane.HeaderHeight));
            }
        }

        return finalSize;
    }
}

/// <summary>
/// What a hunk header row shows over the text column: the hunk background, git's header line in
/// the code font and the secondary colour from the start of the text column, cut with "…" 8
/// before the first button, and at the right end the hunk's buttons (22 high, 8 of padding each
/// side of the label, 4 apart, 6 from the right edge). Side by side, the left pane's bar shows
/// the text and the right pane's the buttons. A click on the bar selects nothing.
/// </summary>
internal sealed class HunkHeaderBar : Border
{
    /// <summary>The height of a hunk button.</summary>
    public const double ButtonHeight = 22;

    public HunkHeaderBar(DiffPane pane, int row)
    {
        Row = row;
        var header = pane.Rows[row];
        Hunk = header.Hunk;
        var palette = pane.Palette;
        Background = palette.Hunk;
        Height = DiffPane.HeaderHeight;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        if (pane.Side != PaneSide.Right)
        {
            var text = new TextBlock
            {
                Text = header.HeaderText,
                FontFamily = palette.CodeFont,
                FontSize = DiffPalette.CodeFontSize,
                Foreground = palette.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            ToolTip.SetTip(text, header.HeaderText);
            grid.Children.Add(text);
        }

        if (pane.Side != PaneSide.Left)
        {
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var owner = pane.Owner;
            switch (owner.HunkActions)
            {
                case HunkActions.StageAndDiscard:
                    buttons.Children.Add(MakeButton(owner, "Stage hunk", "StageHunkButton", HunkAction.Stage));
                    buttons.Children.Add(MakeButton(owner, "Discard hunk", "DiscardHunkButton", HunkAction.Discard));
                    break;
                case HunkActions.Unstage:
                    buttons.Children.Add(MakeButton(owner, "Unstage hunk", "UnstageHunkButton", HunkAction.Unstage));
                    break;
            }

            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
        }

        Child = grid;
    }

    /// <summary>The header's row.</summary>
    public int Row { get; }

    /// <summary>The header's hunk.</summary>
    public int Hunk { get; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Keep the text area from starting a selection on the header row.
        e.Handled = true;
    }

    private Button MakeButton(DiffTextView owner, string label, string automationId, HunkAction action)
    {
        var button = new Button
        {
            Content = label,
            Height = ButtonHeight,
            Padding = new Thickness(8, 0),
            FontSize = DiffPalette.CodeFontSize,
            FontFamily = owner.FontFamily,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        if (owner.TryFindResource("VcActionButton", out var theme) && theme is ControlTheme controlTheme)
        {
            button.Theme = controlTheme;
        }

        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => owner.RaiseHunkAction(Hunk, action);
        return button;
    }
}
