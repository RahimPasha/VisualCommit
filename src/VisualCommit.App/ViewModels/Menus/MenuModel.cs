using VisualCommit.App.ViewModels.Commands;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// One entry of a context menu (docs/phase-3-design.md 4a, D86). Menus are data: pure functions of
/// the repository's state build them (<c>RepositoryMenus</c>), and the menu layer draws them
/// inside the window. No Avalonia types.
/// </summary>
public abstract record MenuEntry;

/// <summary>The first entry of every menu: what the menu acts on (<c>a1b2c3d Add core</c>, <c>feature/alpha</c>), in the secondary colour, cut with "…" and the whole text as tooltip.</summary>
/// <param name="Text">The text.</param>
/// <param name="ToolTip">A longer text for the tooltip, or null for <paramref name="Text"/>.</param>
public sealed record MenuHeader(string Text, string? ToolTip = null) : MenuEntry;

/// <summary>A line between groups of items.</summary>
public sealed record MenuSeparator : MenuEntry;

/// <summary>An item: its label, what it does, and whether it can be used now.</summary>
/// <param name="Label">What it says; an item that opens a dialog ends in "…".</param>
/// <param name="Invoke">What it does; the menu closes first and then posts this to the UI thread.</param>
/// <param name="IsEnabled">Whether it can be used now.</param>
/// <param name="DisabledReason">Why not, shown as its tooltip while disabled.</param>
/// <param name="IsDestructive">Drawn in the danger colour (delete, drop, force push, …).</param>
public sealed record MenuItem(
    string Label,
    Action Invoke,
    bool IsEnabled = true,
    string? DisabledReason = null,
    bool IsDestructive = false) : MenuEntry
{
    /// <summary>
    /// The keyed command the item runs on the same object (docs/phase-6-design.md 6.7), whose key
    /// the menu shows beside its label and the palette with its entry; null for every other item,
    /// which shows no key.
    /// </summary>
    public AppCommand? Command { get; init; }
}

/// <summary>A whole menu: its entries in order, the header first.</summary>
public sealed record MenuModel(IReadOnlyList<MenuEntry> Entries)
{
    /// <summary>The header's text, or null when the menu has none.</summary>
    public string? HeaderText => Entries.OfType<MenuHeader>().FirstOrDefault()?.Text;

    /// <summary>The items, without the header and separators.</summary>
    public IEnumerable<MenuItem> Items => Entries.OfType<MenuItem>();
}

/// <summary>A point in the main window's coordinates, where a menu opens.</summary>
public readonly record struct MenuPosition(double X, double Y);
