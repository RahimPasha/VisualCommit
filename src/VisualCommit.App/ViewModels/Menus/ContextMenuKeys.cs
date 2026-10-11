using Avalonia.Input;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// Which keys open a context menu (docs/phase-3-design.md 4a, D106). Avalonia raises the context
/// request itself, when the key is released, for every gesture in the platform's
/// <c>HotkeyConfiguration.OpenContextMenu</c>: the Menu key everywhere, and Shift+F10 too on
/// Win32 (not headless). The app raises it for Shift+F10 only where that list lacks the gesture,
/// so that each press opens exactly one menu on every platform.
/// </summary>
public static class ContextMenuKeys
{
    /// <summary>The Shift+F10 gesture.</summary>
    public static KeyGesture ShiftF10 { get; } = new(Key.F10, KeyModifiers.Shift);

    /// <summary>
    /// Whether the app must raise the context request for Shift+F10 itself: true when
    /// <paramref name="openContextMenu"/> (the platform's list, or null when the platform gives
    /// none) has no Shift+F10 gesture.
    /// </summary>
    public static bool AppRaisesShiftF10(IReadOnlyList<KeyGesture>? openContextMenu) =>
        openContextMenu is null || !openContextMenu.Any(gesture => gesture.Key == Key.F10 && gesture.KeyModifiers == KeyModifiers.Shift);
}
