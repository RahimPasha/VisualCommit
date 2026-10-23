using Avalonia.Input;
using VisualCommit.App.ViewModels.Commands;

namespace VisualCommit.App.Views;

/// <summary>
/// Avalonia's keys in the app's words and back (docs/phase-6-design.md 6.4): a key event becomes a
/// <see cref="Shortcut"/> for the router, and a <see cref="Shortcut"/> a gesture for the harnesses.
/// </summary>
public static class ShortcutKeys
{
    /// <summary>
    /// <paramref name="e"/> as a <see cref="Shortcut"/>, or null for a key outside
    /// <see cref="ShortcutKey"/> or a modifier the app has no word for (the Windows key, Super, on
    /// a platform whose command modifier is Control). <paramref name="command"/> is the platform's
    /// command modifier.
    /// </summary>
    public static Shortcut? FromEvent(KeyEventArgs e, KeyModifiers command)
    {
        ArgumentNullException.ThrowIfNull(e);
        return From(e.Key, e.KeyModifiers, command);
    }

    /// <summary>
    /// <paramref name="key"/> with <paramref name="modifiers"/> as a <see cref="Shortcut"/> (6.4):
    /// the command modifier is <see cref="ShortcutModifiers.Command"/>; Control on a platform whose
    /// command modifier is Meta is <see cref="ShortcutModifiers.Control"/>; Meta on one whose
    /// command modifier is Control has no word, so null.
    /// </summary>
    public static Shortcut? From(Key key, KeyModifiers modifiers, KeyModifiers command)
    {
        if (KeyOf(key) is not { } shortcutKey)
        {
            return null;
        }

        var meta = command.HasFlag(KeyModifiers.Meta);
        var words = ShortcutModifiers.None;
        if (meta)
        {
            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                words |= ShortcutModifiers.Command;
            }

            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                words |= ShortcutModifiers.Control;
            }
        }
        else
        {
            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                return null;
            }

            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                words |= ShortcutModifiers.Command;
            }
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            words |= ShortcutModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            words |= ShortcutModifiers.Alt;
        }

        return new Shortcut(shortcutKey, words);
    }

    /// <summary>
    /// The gesture that presses <paramref name="shortcut"/> where the command modifier is
    /// <paramref name="command"/> (the harnesses' <c>PressShortcut</c>): Command is the command
    /// modifier, Control the Control key.
    /// </summary>
    public static KeyGesture ToGesture(Shortcut shortcut, KeyModifiers command)
    {
        var modifiers = KeyModifiers.None;
        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Command))
        {
            modifiers |= command == KeyModifiers.None ? KeyModifiers.Control : command;
        }

        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Control))
        {
            modifiers |= KeyModifiers.Control;
        }

        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (shortcut.Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }

        return new KeyGesture(KeyFor(shortcut.Key), modifiers);
    }

    /// <summary>
    /// The app's name of <paramref name="key"/>, or null (6.4): both names of a duplicate value
    /// (Return and Enter, Prior and PageUp, Next and PageDown) are one value; the number pad's 1 to
    /// 9 are the digits; OemComma is Comma; Apps is Menu; Back is Backspace.
    /// </summary>
    public static ShortcutKey? KeyOf(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ShortcutKey.A + (key - Key.A),
        >= Key.D0 and <= Key.D9 => ShortcutKey.D0 + (key - Key.D0),
        >= Key.NumPad1 and <= Key.NumPad9 => ShortcutKey.D1 + (key - Key.NumPad1),
        >= Key.F1 and <= Key.F12 => ShortcutKey.F1 + (key - Key.F1),
        Key.Enter => ShortcutKey.Enter,
        Key.Escape => ShortcutKey.Escape,
        Key.Tab => ShortcutKey.Tab,
        Key.OemComma => ShortcutKey.Comma,
        Key.PageUp => ShortcutKey.PageUp,
        Key.PageDown => ShortcutKey.PageDown,
        Key.Up => ShortcutKey.Up,
        Key.Down => ShortcutKey.Down,
        Key.Left => ShortcutKey.Left,
        Key.Right => ShortcutKey.Right,
        Key.Home => ShortcutKey.Home,
        Key.End => ShortcutKey.End,
        Key.Delete => ShortcutKey.Delete,
        Key.Back => ShortcutKey.Backspace,
        Key.Space => ShortcutKey.Space,
        Key.Apps => ShortcutKey.Menu,
        _ => null,
    };

    /// <summary>The Avalonia key of <paramref name="key"/> (the main keys, not the number pad's).</summary>
    public static Key KeyFor(ShortcutKey key) => key switch
    {
        >= ShortcutKey.A and <= ShortcutKey.Z => Key.A + (key - ShortcutKey.A),
        >= ShortcutKey.D0 and <= ShortcutKey.D9 => Key.D0 + (key - ShortcutKey.D0),
        >= ShortcutKey.F1 and <= ShortcutKey.F12 => Key.F1 + (key - ShortcutKey.F1),
        ShortcutKey.Enter => Key.Enter,
        ShortcutKey.Escape => Key.Escape,
        ShortcutKey.Tab => Key.Tab,
        ShortcutKey.Comma => Key.OemComma,
        ShortcutKey.PageUp => Key.PageUp,
        ShortcutKey.PageDown => Key.PageDown,
        ShortcutKey.Up => Key.Up,
        ShortcutKey.Down => Key.Down,
        ShortcutKey.Left => Key.Left,
        ShortcutKey.Right => Key.Right,
        ShortcutKey.Home => Key.Home,
        ShortcutKey.End => Key.End,
        ShortcutKey.Delete => Key.Delete,
        ShortcutKey.Backspace => Key.Back,
        ShortcutKey.Space => Key.Space,
        ShortcutKey.Menu => Key.Apps,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No Avalonia key for it."),
    };
}
