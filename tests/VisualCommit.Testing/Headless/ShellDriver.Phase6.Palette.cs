using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views.Commands;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The command palette in the driver (docs/phase-6-design.md 5.11, 17.2): Mod+Shift+P, typing into
/// its box, what it shows as a snapshot, and running an entry by the keyboard alone.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The palette is the top dialog.</summary>
    public bool IsPaletteOpen => Session.Shell.OpenPalette is not null;

    /// <summary>The open palette's view model; throws when it is not open.</summary>
    public CommandPaletteViewModel Palette => Session.Shell.OpenPalette ?? throw new InvalidOperationException("The command palette is not open.");

    /// <summary>The open palette's view (<c>CommandPalette</c>); throws when it is not open.</summary>
    public CommandPaletteView PaletteView =>
        TopDialogView as CommandPaletteView ?? throw new InvalidOperationException("The command palette is not the top dialog.");

    /// <summary>Presses Mod+Shift+P (the command modifier the platform's, Control headless), the palette's key (6.1's row 1).</summary>
    public void PressPalette()
    {
        PressKey(Key.P, Raw(CommandModifier | KeyModifiers.Shift));
        Settle();
    }

    /// <summary>Replaces the palette's box's text with <paramref name="text"/> by typing it (its text selected first by the command modifier and A, which the box takes).</summary>
    public void TypeInPalette(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var box = PaletteBox;
        if (!box.IsFocused)
        {
            box.Focus();
            Settle();
        }

        if (!string.IsNullOrEmpty(box.Text))
        {
            PressKey(Key.A, Raw(CommandModifier));
        }

        Type(text);
    }

    /// <summary>The palette's box (<c>CommandPaletteBox</c>).</summary>
    public TextBox PaletteBox => (TextBox)FindByAutomationId(PaletteView, "CommandPaletteBox");

    /// <summary>What the open palette shows (5.11): its query, its card's and box's bounds, its realised rows in order, and whether nothing matches.</summary>
    public PaletteSnapshot PaletteSnapshot()
    {
        Settle();
        var view = PaletteView;
        var palette = Palette;
        var list = (ListBox)FindByAutomationId(view, "CommandPaletteList");
        var rows = new List<PaletteRow>();
        if (list.IsEffectivelyVisible)
        {
            foreach (var container in list.GetRealizedContainers().OfType<ListBoxItem>().OrderBy(list.IndexFromContainer))
            {
                if (container.DataContext is not PaletteEntry || !container.IsEffectivelyVisible)
                {
                    continue;
                }

                // As drawn (5.3, 5.7): the label, the detail's place (a disabled entry's reason
                // where it has one, its detail then being the row's item status) and the key,
                // shown or not.
                var blocks = container.GetVisualDescendants().OfType<TextBlock>().ToList();
                var label = blocks.First(block => block.Name == "CommandPaletteItemLabel");
                var shownDetail = NullIfEmpty(blocks.First(block => block.Name == "CommandPaletteItemDetail").Text);
                var key = blocks.First(block => block.Name == "CommandPaletteItemKey");
                var enabled = !label.Classes.Contains("disabled");
                var showsReason = !enabled && AutomationProperties.GetHelpText(container) is not null;
                rows.Add(new PaletteRow(
                    label.Text ?? string.Empty,
                    showsReason ? AutomationProperties.GetItemStatus(container) : shownDetail,
                    key.IsVisible ? key.Text : null,
                    enabled,
                    showsReason ? shownDetail : null,
                    container.IsSelected,
                    BoundsOf(container),
                    label.Foreground is ISolidColorBrush brush ? brush.Color : default));
            }
        }

        return new PaletteSnapshot(
            palette.Query,
            BoundsOf(FindByAutomationId(view, "CommandPaletteCard")),
            BoundsOf(PaletteBox),
            rows,
            palette.ShowsNothing);

        static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// Runs the palette's entry of <paramref name="label"/> (and <paramref name="detail"/>, when
    /// given) by the keyboard alone (5.11): Down until it is highlighted, then Enter. Throws when
    /// the palette shows no such entry.
    /// </summary>
    public Task RunPaletteEntryAsync(string label, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        var palette = Palette;
        var index = palette.Shown.ToList().FindIndex(entry => entry.Label == label && (detail is null || entry.Detail == detail));
        if (index < 0)
        {
            throw new InvalidOperationException($"The palette shows no entry \"{label}\"{(detail is null ? string.Empty : $" ({detail})")}; it shows: {string.Join(" | ", palette.Shown.Take(40).Select(entry => entry.Label))}.");
        }

        for (var guard = 0; palette.HighlightedIndex != index && guard <= palette.Shown.Count; guard++)
        {
            PressKey(Key.Down);
        }

        PressKey(Key.Enter);
        Settle();
        return Task.CompletedTask;
    }
}

/// <summary>What the open palette shows (docs/phase-6-design.md 5.11).</summary>
/// <param name="Query">The box's text.</param>
/// <param name="CardBounds">The card (<c>CommandPaletteCard</c>) in the window's logical pixels.</param>
/// <param name="BoxBounds">The box (<c>CommandPaletteBox</c>).</param>
/// <param name="Rows">The realised rows, in order.</param>
/// <param name="ShowsNothing">Nothing matches: <c>No matches</c> shows in place of the list.</param>
public sealed record PaletteSnapshot(string Query, Rect CardBounds, Rect BoxBounds, IReadOnlyList<PaletteRow> Rows, bool ShowsNothing);

/// <summary>One realised row of the palette (docs/phase-6-design.md 5.3, 5.11), read from what the row draws.</summary>
/// <param name="Label">The label's text (<c>CommandPaletteItemLabel</c>).</param>
/// <param name="Detail">
/// The detail's text (<c>CommandPaletteItemDetail</c>), or null when it shows none; for a disabled
/// entry with a reason, whose detail's place shows the reason, the row's item status.
/// </param>
/// <param name="KeyText">The key's text (<c>CommandPaletteItemKey</c>) while the key shows, or null.</param>
/// <param name="IsEnabled">Whether it can run: its label not drawn disabled.</param>
/// <param name="Reason">A disabled entry's reason, as the detail's place shows it; null otherwise.</param>
/// <param name="IsHighlighted">The highlighted row.</param>
/// <param name="Bounds">The row in the window's logical pixels.</param>
/// <param name="LabelColour">The colour its label is drawn in.</param>
public sealed record PaletteRow(string Label, string? Detail, string? KeyText, bool IsEnabled, string? Reason, bool IsHighlighted, Rect Bounds, Color LabelColour);
