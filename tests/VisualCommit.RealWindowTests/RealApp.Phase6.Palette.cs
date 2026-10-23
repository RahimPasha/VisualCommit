using System.Drawing;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Testing;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The command palette in the real window (docs/phase-6-design.md 5.11, 17.2): a real Ctrl+Shift+P
/// (<c>PressKeys(CONTROL, SHIFT, KEY_P)</c>), the palette found by its id (<c>CommandPalette</c>, a
/// pane named <c>Command palette</c>), its query typed key by key into <c>CommandPaletteBox</c>,
/// its rows read through UI Automation (each <c>CommandPaletteItem</c>'s <c>Name</c> the label,
/// <c>ItemStatus</c> the detail, <c>HelpText</c> a disabled entry's reason, <c>AcceleratorKey</c> the
/// key, selected while highlighted), <c>No matches</c> (<c>CommandPaletteEmpty</c>), and the log's
/// <c>Command palette opened</c> and <c>Command palette ran:</c> lines (Debug, 14.14). Every wait
/// is on a condition with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The palette's root.</summary>
    public const string PaletteId = "CommandPalette";

    /// <summary>One row of the palette as UI Automation shows it.</summary>
    /// <param name="Label">Its label (its name).</param>
    /// <param name="Detail">Its detail (its item status), empty for none.</param>
    /// <param name="Reason">A disabled entry's reason (its help text), empty for none.</param>
    /// <param name="Key">Its key's text (its accelerator key), empty for none.</param>
    /// <param name="IsHighlighted">Whether it is highlighted (selected).</param>
    /// <param name="Bounds">Where it is.</param>
    /// <param name="Element">The row's element.</param>
    public sealed record PaletteRowView(string Label, string Detail, string Reason, string Key, bool IsHighlighted, RectangleF Bounds, AutomationElement Element)
    {
        /// <summary>The row as one line, for a failure's message.</summary>
        public override string ToString() =>
            $"{Label}{(Detail.Length > 0 ? " · " + Detail : string.Empty)}{(Key.Length > 0 ? " · " + Key : string.Empty)}{(Reason.Length > 0 ? $" (disabled: \"{Reason}\")" : string.Empty)}{(IsHighlighted ? " (highlighted)" : string.Empty)}";
    }

    /// <summary>Ctrl+Shift+P on the real keyboard: the palette's key (6.1).</summary>
    public void PressPalette() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);

    /// <summary>Whether the palette shows.</summary>
    public bool IsPaletteShown => ShownElements(PaletteId).Count == 1;

    /// <summary>Waits until the palette shows, and returns its root.</summary>
    public AutomationElement WaitForPalette(TimeSpan timeout)
    {
        WaitFor(() => IsPaletteShown, "the command palette", timeout);
        return ShownElements(PaletteId)[0];
    }

    /// <summary>Waits until the palette has closed.</summary>
    public void WaitForPaletteClosed(TimeSpan timeout) => WaitUntilGone(PaletteId, timeout);

    /// <summary>The palette's box's text (<c>CommandPaletteBox</c>, through its Value pattern).</summary>
    public string PaletteQuery() => BoxText("CommandPaletteBox");

    /// <summary>The palette's rows on screen, top to bottom; empty when it shows none (<c>No matches</c>) or is closed.</summary>
    public IReadOnlyList<PaletteRowView> PaletteRows()
    {
        if (ShownElements("CommandPaletteList") is not [var list])
        {
            return [];
        }

        return [.. list.FindAllDescendants(condition => condition.ByAutomationId("CommandPaletteItem"))
            .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 })
            .Select(row => (Row: row, Bounds: BoundsOf(row)))
            .OrderBy(entry => entry.Bounds.Top)
            .Select(entry => new PaletteRowView(
                entry.Row.Name,
                ItemStatusOf(entry.Row),
                HelpTextOf(entry.Row),
                AcceleratorKeyOf(entry.Row),
                IsSelected(entry.Row),
                entry.Bounds,
                entry.Row))];
    }

    /// <summary>The palette's rows as one line each, for a failure's message.</summary>
    public string DescribePaletteRows() => string.Join(" | ", PaletteRows());

    /// <summary>Whether the palette shows <c>No matches</c> in place of its list.</summary>
    public bool PaletteShowsNothing => ShownElements("CommandPaletteEmpty").Count == 1 && ShownElements("CommandPaletteList").Count == 0;

    /// <summary>
    /// Types <paramref name="text"/> into the palette's box on the real keyboard, key by key, and
    /// waits until the box holds <paramref name="query"/> (the whole query after the typing: the
    /// text typed, or what it replaced a selection with).
    /// </summary>
    public void TypeInPalette(string text, string query, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(query);
        TypeText(text);
        WaitForTelling(
            () => PaletteQuery() == query,
            $"the palette's box to hold \"{query}\"",
            timeout,
            () => $"It holds \"{PaletteQuery()}\".");
    }

    /// <summary>
    /// The pattern of the palette's Debug line on each opening (14.14):
    /// <c>{repo}: Command palette opened</c>, without the prefix while the active tab has no repository.
    /// </summary>
    public static Regex PaletteOpened(string? repository) =>
        new(repository is null ? @"\] Command palette opened$" : $@"\] {Regex.Escape(repository)}: Command palette opened$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The pattern of the palette's Debug line on each entry it runs (14.14):
    /// <c>{repo}: Command palette ran: {label}</c>, with <c> ({detail})</c> when the entry has one.
    /// A null <paramref name="label"/> matches any entry.
    /// </summary>
    public static Regex PaletteRan(string? repository, string? label = null)
    {
        var prefix = repository is null ? string.Empty : Regex.Escape(repository) + ": ";
        var entry = label is null ? ".*" : Regex.Escape(label) + @"( \(.*\))?";
        return new Regex($@"\] {prefix}Command palette ran: {entry}$", RegexOptions.CultureInvariant);
    }

    /// <summary>How many of the log's lines match <paramref name="pattern"/> since <paramref name="mark"/> (<see cref="LogMark"/>).</summary>
    public int CountLogLinesSince(Regex pattern, int mark)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return LogLinesSince(mark).Count(line => pattern.IsMatch(line));
    }

    /// <summary>A line of the app's log that holds <paramref name="text"/> (<see cref="AppLogFile.Containing"/>).</summary>
    public static Regex LogContaining(string text) => AppLogFile.Containing(text);
}
