namespace VisualCommit.App.ViewModels.Commands;

/// <summary>One key with its modifiers (docs/phase-6-design.md 6.4), in the app's own words: no Avalonia types.</summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers held with it.</param>
public readonly record struct Shortcut(ShortcutKey Key, ShortcutModifiers Modifiers = ShortcutModifiers.None);

/// <summary>The modifiers of a <see cref="Shortcut"/> (6.4).</summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,

    /// <summary>The platform's command modifier: Ctrl on Windows and Linux, Cmd on macOS.</summary>
    Command = 1,

    Shift = 2,

    /// <summary>Alt, Option on macOS.</summary>
    Alt = 4,

    /// <summary>The Control key itself, for Ctrl+Tab and Ctrl+PageDown on macOS; one with <see cref="Command"/> where the command modifier is Control.</summary>
    Control = 8,
}

/// <summary>The keys the app's shortcuts use (6.4).</summary>
public enum ShortcutKey
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Enter,
    Escape,
    Tab,
    Comma,
    PageUp,
    PageDown,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    Delete,
    Backspace,
    Space,
    Menu,
}

/// <summary>Which platform's words a key text uses (6.4): Ctrl where the command modifier is Control, Cmd where it is Meta (macOS).</summary>
public enum CommandKey
{
    Ctrl,
    Cmd,
}
