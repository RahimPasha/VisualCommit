namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The one formatter of key texts (docs/phase-6-design.md 6.4, 14.12, D214): words, not symbols,
/// on macOS too, joined by <c>+</c> (<c>Ctrl+Shift+P</c>, <c>Cmd+Shift+P</c>, <c>Option+Up</c>,
/// <c>Cmd+Delete</c>): the palette's, the menus', the tooltips', the settings list's and UI
/// Automation's <c>AcceleratorKey</c>. Work package WP-K gives it its bodies; nothing calls them before then.
/// </summary>
public static class ShortcutText
{
    /// <summary><paramref name="shortcut"/> in <paramref name="platform"/>'s words.</summary>
    public static string Format(Shortcut shortcut, CommandKey platform) =>
        throw new NotImplementedException("Work package WP-K builds ShortcutText.Format (docs/phase-6-design.md 6.4).");

    /// <summary><paramref name="shortcuts"/> joined by <c>, </c>; a run of <see cref="AppCommand.GoToTab"/>'s reads <c>Ctrl+1 to Ctrl+8</c>.</summary>
    public static string FormatAll(IReadOnlyList<Shortcut> shortcuts, CommandKey platform) =>
        throw new NotImplementedException("Work package WP-K builds ShortcutText.FormatAll (docs/phase-6-design.md 6.4).");
}
