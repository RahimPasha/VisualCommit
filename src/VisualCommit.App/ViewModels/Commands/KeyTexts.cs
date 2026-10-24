namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The key texts of one platform (docs/phase-6-design.md 6.4): what a command's key reads as, and
/// a text with its key appended. <see cref="Current"/> is set once by <c>AppSession.Start</c>
/// before it makes the window, from the platform's command modifier; a test of the Cmd texts makes
/// its own <c>new KeyTexts(CommandKey.Cmd)</c>.
/// </summary>
/// <param name="platform">Whose words the texts use.</param>
public sealed class KeyTexts(CommandKey platform)
{
    /// <summary>The app's key texts: Ctrl's until <c>AppSession.Start</c> sets them (the headless platform gives Control too).</summary>
    public static KeyTexts Current { get; set; } = new(CommandKey.Ctrl);

    /// <summary>Whose words the texts use.</summary>
    public CommandKey Platform { get; } = platform;

    /// <summary>The text of <paramref name="command"/>'s shown key, or null for a command without one.</summary>
    public string? Of(AppCommand command) =>
        AppShortcuts.Shown(command, Platform) is { } shortcut ? ShortcutText.Format(shortcut, Platform) : null;

    /// <summary><c>{text} ({key})</c> with <paramref name="command"/>'s shown key, or <paramref name="text"/> alone for a command without one (6.8).</summary>
    public string WithKey(string text, AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Of(command) is { } key ? $"{text} ({key})" : text;
    }
}
