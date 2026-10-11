using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// What an open context menu shows (docs/phase-3-design.md 4a), as <see cref="ShellDriver.Menu"/>
/// reads it from the window: its header, its entries in order and where it is.
/// </summary>
/// <param name="Header">The header's full text, or null when the menu has none.</param>
/// <param name="HeaderToolTip">The header's tooltip (its full text unless the menu gave a longer one).</param>
/// <param name="Entries">The items and separators after the header, in order.</param>
/// <param name="Bounds">Where the menu is drawn, in the window's logical pixels (its border included).</param>
public sealed record MenuSnapshot(string? Header, string? HeaderToolTip, IReadOnlyList<MenuEntrySnapshot> Entries, Rect Bounds)
{
    /// <summary>The items, without the separators.</summary>
    public IReadOnlyList<MenuEntrySnapshot> Items => Entries.Where(entry => !entry.IsSeparator).ToList();

    /// <summary>The highlighted item, or null when none is (a menu without an enabled item).</summary>
    public MenuEntrySnapshot? Highlighted => Entries.SingleOrDefault(entry => entry.IsHighlighted);

    /// <summary>The item with this label. Throws when there is none or more than one.</summary>
    public MenuEntrySnapshot Item(string label) =>
        Items.Where(item => item.Label == label).ToList() switch
        {
            [var item] => item,
            [] => throw new InvalidOperationException($"The menu \"{Header}\" has no item \"{label}\"; it has: {string.Join(" | ", Lines())}."),
            _ => throw new InvalidOperationException($"The menu \"{Header}\" has more than one item \"{label}\"."),
        };

    /// <summary>
    /// The menu written as the test report writes menus: the header first, then each item's label,
    /// "—" for a separator, " (danger)" after an enabled destructive item and
    /// <c> (disabled: "reason")</c> after a disabled one. Highlighting is left out.
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>();
        if (Header is not null)
        {
            lines.Add(Header);
        }

        lines.AddRange(Entries.Select(entry => entry switch
        {
            { IsSeparator: true } => "—",
            { IsEnabled: false, ToolTip: { } reason } => string.Create(CultureInfo.InvariantCulture, $"{entry.Label} (disabled: \"{reason}\")"),
            { IsEnabled: false } => entry.Label + " (disabled)",
            { IsDestructive: true } => entry.Label + " (danger)",
            _ => entry.Label,
        }));
        return lines;
    }

    public override string ToString() => string.Join(" | ", Lines());
}

/// <summary>One entry of an open menu: an item or a separator.</summary>
/// <param name="Label">The item's label; "—" for a separator.</param>
/// <param name="IsSeparator">Whether it is a separator.</param>
/// <param name="IsEnabled">Whether the item can be used.</param>
/// <param name="IsDestructive">Whether the menu marks the item destructive (drawn in the danger colour while enabled).</param>
/// <param name="IsHighlighted">Whether the item has the highlight (the focus's background).</param>
/// <param name="ToolTip">The item's tooltip text (a disabled item's reason), or null.</param>
/// <param name="TextColor">The colour its text is drawn in, or null when it is not a plain colour.</param>
/// <param name="Bounds">Where the entry is drawn, in the window's logical pixels.</param>
public sealed record MenuEntrySnapshot(
    string Label,
    bool IsSeparator,
    bool IsEnabled,
    bool IsDestructive,
    bool IsHighlighted,
    string? ToolTip,
    Color? TextColor,
    Rect Bounds);
