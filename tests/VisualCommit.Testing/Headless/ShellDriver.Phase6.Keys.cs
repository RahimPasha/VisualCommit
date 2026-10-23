using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's keys in the driver (docs/phase-6-design.md 6.1 to 6.6, 6.12, 17.2): a command's key
/// from the app's catalogue (<see cref="PressShortcut"/>, for the tests of the router; the
/// walk-through presses literal gestures, KD9), Ctrl+Tab and F6, the region that holds the focus,
/// and the help texts and accelerator keys UI Automation reads.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// Presses <paramref name="command"/>'s <paramref name="index"/>-th key of
    /// <see cref="AppShortcuts"/> (6.4), the command modifier the platform's
    /// (<see cref="CommandModifier"/>, Control headless).
    /// </summary>
    public void PressShortcut(AppCommand command, int index = 0)
    {
        var keys = AppShortcuts.All(command);
        if (index < 0 || index >= keys.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{command} has {keys.Count} keys.");
        }

        var gesture = ShortcutKeys.ToGesture(keys[index], CommandModifier);
        PressKey(gesture.Key, Raw(gesture.KeyModifiers));
    }

    /// <summary>
    /// <see cref="PressShortcut"/>, returning whether the key press was handled once its route had
    /// ended (the window's last bubbling handler sees it): the router consumes a key it takes.
    /// </summary>
    public bool PressShortcutHandled(AppCommand command, int index = 0)
    {
        var keys = AppShortcuts.All(command);
        if (index < 0 || index >= keys.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{command} has {keys.Count} keys.");
        }

        var gesture = ShortcutKeys.ToGesture(keys[index], CommandModifier);
        return PressKeyHandled(gesture.Key, Raw(gesture.KeyModifiers));
    }

    /// <summary><see cref="PressKey"/>, returning whether the key press was handled once its route had ended.</summary>
    public bool PressKeyHandled(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        bool? handled = null;
        void Record(object? sender, KeyEventArgs e)
        {
            if (e.Key == key)
            {
                handled = e.Handled;
            }
        }

        Window.AddHandler(InputElement.KeyDownEvent, Record, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            PressKey(key, modifiers);
        }
        finally
        {
            Window.RemoveHandler(InputElement.KeyDownEvent, Record);
        }

        return handled ?? throw new InvalidOperationException($"The window saw no key press of {key}.");
    }

    /// <summary>Ctrl+Tab, or with <paramref name="shift"/> Ctrl+Shift+Tab: the next or previous tab, with Control on every platform (6.5).</summary>
    public void PressCtrlTab(bool shift = false) => PressKey(Key.Tab, shift ? RawInputModifiers.Control | RawInputModifiers.Shift : RawInputModifiers.Control);

    /// <summary>F6, or with <paramref name="shift"/> Shift+F6: the next or previous region (6.6).</summary>
    public void PressF6(bool shift = false) => PressKey(Key.F6, shift ? RawInputModifiers.Shift : RawInputModifiers.None);

    /// <summary>The element with the keyboard focus, or null.</summary>
    public IInputElement? FocusedElement => Window.FocusManager?.GetFocusedElement();

    /// <summary>The region (6.6's <see cref="KeyRegion.RoleProperty"/>) the focused element lies in; <see cref="KeyRegionRole.None"/> when none holds it.</summary>
    public KeyRegionRole FocusedRegion() => KeyRegion.RegionOf(FocusedElement as Visual);

    /// <summary>Whether the focused element is <paramref name="control"/> or lies inside it.</summary>
    public bool FocusIsWithin(Visual control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return FocusedElement is Visual focused && (ReferenceEquals(focused, control) || control.IsVisualAncestorOf(focused));
    }

    /// <summary>The automation id of the focused element or of the nearest ancestor that has one.</summary>
    public string? FocusedAutomationId() =>
        (FocusedElement as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetAutomationId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

    /// <summary>A control's <c>AutomationProperties.AcceleratorKey</c>, which UI Automation reads as its key (6.8, 15.5).</summary>
    public static string? AcceleratorKeyOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AutomationProperties.GetAcceleratorKey(control);
    }

    /// <summary>A control's <c>AutomationProperties.HelpText</c>, its tooltip's text (6.8).</summary>
    public static string? HelpTextOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return AutomationProperties.GetHelpText(control);
    }

    /// <summary>The key a keyed menu item draws at its right (6.7: its <c>ContextMenuItemKey</c>'s text), or null for an item without one.</summary>
    public static string? MenuKeyTextOf(Avalonia.Controls.MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => AutomationProperties.GetAutomationId(block) == MenuLayer.KeyAutomationId)?.Text;
    }

    /// <summary>A keyed menu item's key text block (6.7), or null for an item without one.</summary>
    public static TextBlock? MenuKeyBlockOf(Avalonia.Controls.MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => AutomationProperties.GetAutomationId(block) == MenuLayer.KeyAutomationId);
    }
}
