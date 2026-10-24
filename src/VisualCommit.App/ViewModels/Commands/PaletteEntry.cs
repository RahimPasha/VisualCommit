namespace VisualCommit.App.ViewModels.Commands;

/// <summary>Which of the palette's two lists an entry is in (docs/phase-6-design.md 5.4, 5.5).</summary>
public enum PaletteEntryKind
{
    /// <summary>A command (5.4): shown at once, also in an empty box.</summary>
    Command,

    /// <summary>An item (5.5): an object's menu item, a tab, a repository or a workspace; shown only once the user types.</summary>
    Item,
}

/// <summary>
/// One entry of the command palette (docs/phase-6-design.md 5.2), without Avalonia types: as the
/// menu item or command that does the same says it, enabled or disabled with the same reason.
/// </summary>
/// <param name="Label">What it says (<c>Check out feature/alpha</c>, <c>Fetch all remotes</c>).</param>
/// <param name="Detail">An item's menu header (<c>feature/alpha</c>, <c>a1b2c3d Add core</c>), a repository's folder, a command's repository or tab; or null.</param>
/// <param name="KeyText">The key's text in the platform's words (<c>Ctrl+Shift+F</c>), or null for none.</param>
/// <param name="IsEnabled">Whether it can run now.</param>
/// <param name="DisabledReason">Why not, shown in the detail's place while disabled; null when enabled or without a reason.</param>
/// <param name="IsDestructive">Drawn in the danger colour while enabled.</param>
/// <param name="Kind">A command or an item.</param>
/// <param name="Order">Its place in its list (source order, 5.6).</param>
/// <param name="Run">What it does: a menu item's <c>Invoke</c> or the command's run, called after the palette has closed (5.9).</param>
public sealed record PaletteEntry(
    string Label,
    string? Detail,
    string? KeyText,
    bool IsEnabled,
    string? DisabledReason,
    bool IsDestructive,
    PaletteEntryKind Kind,
    int Order,
    Action Run)
{
    /// <summary>The separator between a key's label and detail (U+001F, the unit separator).</summary>
    public const char KeySeparator = '\u001F';

    /// <summary>
    /// What identifies the entry (5.2): its label, then U+001F and its detail when it has one. The
    /// highlight across a rebuild, the session's runs and the items' duplicates go by it.
    /// </summary>
    public string Key => Detail is null ? Label : $"{Label}{KeySeparator}{Detail}";

    /// <summary>The keyed command a menu item runs on its object (6.7), or null: an item of a listed command's label is left out (5.5's duplicates).</summary>
    public AppCommand? Command { get; init; }

    /// <summary>What the row shows after the label: a disabled entry's reason in the detail's place (5.7), else the detail.</summary>
    public string? ShownDetail => !IsEnabled && DisabledReason is not null ? DisabledReason : Detail;

    /// <summary>The row's tooltip: a disabled entry's reason, whole (5.7); null otherwise.</summary>
    public string? ToolTip => IsEnabled ? null : DisabledReason;

    /// <summary>The row has a key column (5.3): a key text to show.</summary>
    public bool HasKey => KeyText is not null;

    /// <summary>An enabled destructive entry: its label in the danger colour (5.3).</summary>
    public bool ShowsDanger => IsEnabled && IsDestructive;
}
