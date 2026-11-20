using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 8a's keyboard pass in the driver (docs/phase-8a-design.md 6.4, 6.5): whether the focus shows
/// the keyboard's frame, the stage panel's file rows and their buttons as the tree has them, and a
/// walk of Tabs that asserts where each one lands.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The class a stage panel's file row (its <c>Panel</c>) has.</summary>
    public const string StageRowClass = "stageRow";

    /// <summary>
    /// Whether the focused element shows the keyboard's frame: it has <c>:focus-visible</c>, which
    /// Avalonia sets for a focus moved by Tab or the arrows, never for a click or for
    /// <see cref="NavigationMethod.Unspecified"/>.
    /// </summary>
    public bool FocusVisible => FocusedElement is StyledElement element && element.Classes.Contains(":focus-visible");

    /// <summary>
    /// The realised file rows (<c>Panel.stageRow</c>) of the stage panel's list
    /// <paramref name="list"/> (<c>UnstagedFileList</c> or <c>StagedFileList</c>), shown ones only,
    /// top to bottom.
    /// </summary>
    public IReadOnlyList<Panel> StageRows(string list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var items = (ItemsControl)FindByAutomationId(list);
        return [.. items.GetRealizedContainers()
            .Where(container => container.IsEffectivelyVisible)
            .OrderBy(items.IndexFromContainer)
            .Select(container => container.GetVisualDescendants().OfType<Panel>().FirstOrDefault(panel => panel.Classes.Contains(StageRowClass)))
            .OfType<Panel>()];
    }

    /// <summary>The realised file row of <paramref name="path"/> in <paramref name="list"/>, or null.</summary>
    public Panel? StageRow(string list, string path) =>
        StageRows(list).FirstOrDefault(row => row.DataContext is ChangedFileRow { Key: var key } && key == path);

    /// <summary>The file row (<c>Panel.stageRow</c>) that holds <paramref name="visual"/>, or null.</summary>
    public static Panel? StageRowOf(Visual? visual) =>
        visual?.GetSelfAndVisualAncestors().OfType<Panel>().FirstOrDefault(panel => panel.Classes.Contains(StageRowClass));

    /// <summary>
    /// A file row's button by its automation id: its own (<c>StageFileRow</c>), or one of its actions
    /// (<c>RowStageButton</c>, <c>RowDiscardButton</c>, <c>RowUnstageButton</c>), shown or not.
    /// </summary>
    public static Button RowButton(Panel row, string automationId)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == automationId)
            ?? throw new InvalidOperationException($"The row has no {automationId}.");
    }

    /// <summary>The path of the file row whose own button (<c>StageFileRow</c>) has the focus, or null when the focus is on none.</summary>
    public string? FocusedRowPath() =>
        FocusedElement is Control focused && AutomationProperties.GetAutomationId(focused) == "StageFileRow"
            ? AutomationProperties.GetName(focused)
            : null;

    /// <summary>
    /// Presses Tab (Shift+Tab with <paramref name="shift"/>) once and waits until the focus is on
    /// <paramref name="expected"/>'s element, which <paramref name="what"/> names for the message,
    /// whose failure ends the walk before any further key.
    /// </summary>
    public async Task TabToAsync(Func<IInputElement?, bool> expected, string what, bool shift = false)
    {
        ArgumentNullException.ThrowIfNull(expected);
        PressKey(Key.Tab, shift ? RawInputModifiers.Shift : RawInputModifiers.None);
        try
        {
            await WaitForAsync(() => expected(FocusedElement), what);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException($"{(shift ? "Shift+Tab" : "Tab")} did not reach {what}: the focus is on {DescribeFocus()}.");
        }
    }

    /// <summary>The focused element in words, for a failure's message: its type, its automation id or the nearest one above it, and its name.</summary>
    public string DescribeFocus() =>
        FocusedElement is not { } focused
            ? "nothing"
            : focused is Control control
                ? $"{control.GetType().Name} {FocusedAutomationId() ?? "(no id)"} \"{AutomationProperties.GetName(control)}\""
                : focused.GetType().Name;
}
