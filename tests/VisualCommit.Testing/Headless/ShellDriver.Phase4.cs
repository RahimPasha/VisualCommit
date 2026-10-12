using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 4's shared additions to the driver (docs/phase-4-design.md 13.2, package WP-S): the
/// platform's undo and redo, the banner's Continue and Skip, and the result editor's caret held
/// still (<see cref="ConflictResultEditor.CaretBlinks"/>, set false by <see cref="Start"/>). The
/// interactive rebase's list has its own part (ShellDriver.Phase4.Rebase.cs, WP5), the conflict
/// resolver too (ShellDriver.Phase4.Conflicts.cs, WP6).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The banner's Continue button (design 5.2, 7.4).</summary>
    public const string BannerContinueId = "BannerContinueButton";

    /// <summary>The banner's Skip button (design 5.2, 7.4).</summary>
    public const string BannerSkipId = "BannerSkipButton";

    /// <summary>
    /// The platform's undo gesture: the first of Avalonia's <c>HotkeyConfiguration.Undo</c> from
    /// the window's platform settings, as the app reads it (Ctrl+Z on Windows and Linux; the
    /// headless platform's own in a scripted check). The checks say "the platform's undo".
    /// </summary>
    public KeyGesture UndoGesture => FirstGesture(Hotkeys.Undo, "undo");

    /// <summary>The platform's redo gesture: the first of <c>HotkeyConfiguration.Redo</c> (Ctrl+Y on Windows and Linux).</summary>
    public KeyGesture RedoGesture => FirstGesture(Hotkeys.Redo, "redo");

    /// <summary>Presses the platform's undo gesture (<see cref="UndoGesture"/>) on whatever has the keyboard focus.</summary>
    public void Undo() => PressGesture(UndoGesture);

    /// <summary>Presses the platform's redo gesture (<see cref="RedoGesture"/>) on whatever has the keyboard focus.</summary>
    public void Redo() => PressGesture(RedoGesture);

    /// <summary>Presses and releases a gesture's key with its modifiers held.</summary>
    public void PressGesture(KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        PressKey(gesture.Key, Raw(gesture.KeyModifiers));
    }

    /// <summary>The banner's Continue button (<c>BannerContinueButton</c>), shown or not.</summary>
    public Button BannerContinueButton => (Button)FindByAutomationId(BannerContinueId);

    /// <summary>The banner's Skip button (<c>BannerSkipButton</c>), shown or not.</summary>
    public Button BannerSkipButton => (Button)FindByAutomationId(BannerSkipId);

    /// <summary>
    /// Clicks the banner's Continue, as a user would. Throws when it is hidden or disabled: a click
    /// on it then does nothing, and a check that clicks it expects it to act.
    /// </summary>
    public void ClickBannerContinue() => ClickBannerButton(BannerContinueButton, "Continue");

    /// <summary>
    /// Clicks the banner's Skip, as a user would (it asks first, design 7.2). Throws when it is
    /// hidden or disabled.
    /// </summary>
    public void ClickBannerSkip() => ClickBannerButton(BannerSkipButton, "Skip");

    /// <summary>Holds the result editor's caret still, so that two pictures of one state are the same (design 11.3, D151).</summary>
    private static void HoldCaretsStill() => ConflictResultEditor.CaretBlinks = false;

    private PlatformHotkeyConfiguration Hotkeys =>
        Window.GetPlatformSettings()?.HotkeyConfiguration
        ?? throw new InvalidOperationException("The window has no platform settings, so it has no undo or redo gesture.");

    private static KeyGesture FirstGesture(IEnumerable<KeyGesture> gestures, string what) =>
        gestures.FirstOrDefault() ?? throw new InvalidOperationException($"The platform names no {what} gesture.");

    private void ClickBannerButton(Button button, string what)
    {
        Settle();
        if (!button.IsEffectivelyVisible || !button.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException(
                $"The banner's {what} cannot be clicked: visible {button.IsEffectivelyVisible}, enabled {button.IsEffectivelyEnabled} ({ToolTip.GetTip(button) as string ?? "no reason given"}).");
        }

        Click(button);
    }
}
