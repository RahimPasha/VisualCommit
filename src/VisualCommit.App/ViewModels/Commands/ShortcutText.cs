using System.Text;

namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The one formatter of key texts (docs/phase-6-design.md 6.4, 14.12, D214): words, not symbols,
/// on macOS too, joined by <c>+</c> (<c>Ctrl+Shift+P</c>, <c>Cmd+Shift+P</c>, <c>Option+Up</c>,
/// <c>Cmd+Delete</c>): the palette's, the menus', the tooltips', the settings list's and UI
/// Automation's <c>AcceleratorKey</c>.
/// </summary>
public static class ShortcutText
{
    /// <summary>
    /// <paramref name="shortcut"/> in <paramref name="platform"/>'s words: the command modifier
    /// (<c>Ctrl</c>, <c>Cmd</c>), the Control key where it is not the command modifier
    /// (<c>Ctrl</c>), <c>Shift</c>, <c>Alt</c> (<c>Option</c> on macOS), then the key by its
    /// name, but the digit alone, <c>Esc</c> for Escape and <c>,</c> for Comma; on macOS Backspace
    /// is written <c>Delete</c> and Delete <c>Forward Delete</c>, Apple's words.
    /// </summary>
    public static string Format(Shortcut shortcut, CommandKey platform)
    {
        var text = new StringBuilder();
        var modifiers = shortcut.Modifiers;
        var mac = platform == CommandKey.Cmd;
        if (modifiers.HasFlag(ShortcutModifiers.Command))
        {
            text.Append(mac ? "Cmd+" : "Ctrl+");
        }

        // The Control key itself: on Windows and Linux it is the command modifier, written once.
        if (modifiers.HasFlag(ShortcutModifiers.Control) && (mac || !modifiers.HasFlag(ShortcutModifiers.Command)))
        {
            text.Append("Ctrl+");
        }

        if (modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            text.Append("Shift+");
        }

        if (modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            text.Append(mac ? "Option+" : "Alt+");
        }

        text.Append(KeyName(shortcut.Key, mac));
        return text.ToString();
    }

    /// <summary>
    /// <paramref name="shortcuts"/> in <paramref name="platform"/>'s words, joined by <c>, </c>; a
    /// run of three or more digits in a row with the same modifiers (<see cref="AppCommand.GoToTab"/>'s
    /// Mod+1 to Mod+8) reads <c>Ctrl+1 to Ctrl+8</c>.
    /// </summary>
    public static string FormatAll(IReadOnlyList<Shortcut> shortcuts, CommandKey platform)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        var parts = new List<string>();
        for (var i = 0; i < shortcuts.Count;)
        {
            var end = i;
            while (end + 1 < shortcuts.Count && Follows(shortcuts[end], shortcuts[end + 1]))
            {
                end++;
            }

            if (end - i >= 2)
            {
                parts.Add($"{Format(shortcuts[i], platform)} to {Format(shortcuts[end], platform)}");
                i = end + 1;
            }
            else
            {
                parts.Add(Format(shortcuts[i], platform));
                i++;
            }
        }

        return string.Join(", ", parts);
    }

    /// <summary>Whether <paramref name="next"/> is the digit after <paramref name="shortcut"/>'s, with the same modifiers.</summary>
    private static bool Follows(Shortcut shortcut, Shortcut next) =>
        shortcut.Modifiers == next.Modifiers
        && shortcut.Key is >= ShortcutKey.D0 and < ShortcutKey.D9
        && next.Key == shortcut.Key + 1;

    private static string KeyName(ShortcutKey key, bool mac) => key switch
    {
        >= ShortcutKey.D0 and <= ShortcutKey.D9 => ((int)(key - ShortcutKey.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
        ShortcutKey.Escape => "Esc",
        ShortcutKey.Comma => ",",
        ShortcutKey.Backspace when mac => "Delete",
        ShortcutKey.Delete when mac => "Forward Delete",
        _ => key.ToString(),
    };
}
