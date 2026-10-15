using Avalonia.Input;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 5's shell in the driver (docs/phase-5-design.md 1.3, 15.2; docs/test-reports/phase-5.md,
/// part A and its decision AD12, package WP5): the window's keys for the toolbar's Search, Undo
/// and Redo. Phase 4's <see cref="Undo"/> and <see cref="Redo"/> send the same keys and stay the
/// text undo of a text box and the resolver: a check uses these where the key is meant for the
/// window, so that its code says which handler should take it.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// Presses the command modifier with F (Ctrl+F; Cmd+F where the platform's command modifier
    /// is Cmd), the toolbar's Search (1.3).
    /// </summary>
    public void PressSearch() => PressKey(Key.F, Raw(CommandModifier));

    /// <summary>Presses the platform's undo gesture (<see cref="UndoGesture"/>, Ctrl+Z), the toolbar's Undo when no text box or the resolver takes it (1.3).</summary>
    public void PressGitUndo() => PressGesture(UndoGesture);

    /// <summary>
    /// Presses one of the platform's redo gestures, the toolbar's Redo when no text box or the
    /// resolver takes it (1.3): the <paramref name="gesture"/>-th of <c>HotkeyConfiguration.Redo</c>
    /// (Ctrl+Y, then Ctrl+Shift+Z on Windows and in the headless platform).
    /// </summary>
    public void PressGitRedo(int gesture = 0)
    {
        var gestures = RedoGestures;
        if (gesture < 0 || gesture >= gestures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(gesture), gesture, $"The platform names {gestures.Count} redo gestures.");
        }

        PressGesture(gestures[gesture]);
    }

    /// <summary>Every redo gesture of the platform, in its order (<c>HotkeyConfiguration.Redo</c>).</summary>
    public IReadOnlyList<KeyGesture> RedoGestures => [.. Hotkeys.Redo];
}
