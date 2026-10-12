using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// What phase 4's real-window pass (Phase4RealWindowPass*.cs, package WP8) needs beyond the drivers
/// of WP-S, WP5 and WP6 (RealApp.Phase4.cs, .Phase4.Rebase.cs, .Phase4.Conflicts.cs): texts waited
/// for by automation id, the stage panel's rows and titles, the platform's undo, the keyboard focus
/// of the list's rows, the result's bar texts, and where a tooltip lies. Each waits on a condition,
/// never for a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The elements with <paramref name="automationId"/> that are on screen, top to bottom, then left to right.</summary>
    public IReadOnlyList<AutomationElement> ShownElements(string automationId) =>
        [.. FindAll(automationId)
            .Where(element => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 })
            .OrderBy(element => element.BoundingRectangle.Top)
            .ThenBy(element => element.BoundingRectangle.Left)];

    /// <summary>The names (texts) of the on-screen elements with <paramref name="automationId"/>, top to bottom, then left to right (<see cref="ShownElements"/>).</summary>
    public IReadOnlyList<string> NamesOnScreen(string automationId) => [.. ShownElements(automationId).Select(element => element.Name)];

    /// <summary>
    /// Waits until an on-screen element with <paramref name="automationId"/> is named
    /// <paramref name="text"/> (a text's text, a button's label), at most
    /// <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null). The message of a
    /// timeout says what the element read.
    /// </summary>
    public void WaitForText(string automationId, string text, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        ArgumentNullException.ThrowIfNull(text);
        IReadOnlyList<string> seen = [];
        try
        {
            WaitFor(
                () => (seen = NamesOnScreen(automationId)).Contains(text),
                $"{automationId} to read \"{text}\"",
                timeout ?? DefaultWaitTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It reads: {(seen.Count == 0 ? "nothing (not on screen)" : string.Join(" | ", seen.Select(name => "\"" + name + "\"")))}.", ex);
        }
    }

    /// <summary>Waits until no element with <paramref name="automationId"/> is on screen (a dialog closed, a menu gone).</summary>
    public void WaitUntilGone(string automationId, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        WaitFor(() => ShownElements(automationId).Count == 0, $"no {automationId} on screen", timeout ?? DefaultWaitTimeout);
    }

    /// <summary>Whether <paramref name="element"/> has the keyboard focus, as UI Automation reports it.</summary>
    public static bool HasKeyboardFocus(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Properties.HasKeyboardFocus.ValueOrDefault;
    }

    /// <summary>
    /// The row of the file <paramref name="path"/> (its repository-relative path, the row's name)
    /// in one of the stage panel's lists (<c>UnstagedFileList</c>, <c>StagedFileList</c>):
    /// <c>StageFileRow</c>, found inside that list, since a file can be in both. Waits for it.
    /// </summary>
    public AutomationElement StageFileRow(string list, string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(list);
        ArgumentException.ThrowIfNullOrEmpty(path);
        AutomationElement? row = null;
        WaitFor(
            () => (row = FindAll(list).FirstOrDefault(element => !element.IsOffscreen)?
                .FindAllDescendants(condition => condition.ByAutomationId("StageFileRow"))
                .FirstOrDefault(candidate => candidate.Name == path && !candidate.IsOffscreen)) is not null,
            $"the row of {path} in {list}",
            timeout ?? DefaultWaitTimeout);
        return row!;
    }

    /// <summary>Waits until the stage panel shows with these titles over its two lists (<c>UnstagedTitle</c>, <c>StagedTitle</c>).</summary>
    public void WaitForStageTitles(string unstagedTitle, string stagedTitle, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(unstagedTitle);
        ArgumentException.ThrowIfNullOrEmpty(stagedTitle);
        WaitForTelling(
            () => NamesOnScreen("UnstagedTitle").Contains(unstagedTitle) && NamesOnScreen("StagedTitle").Contains(stagedTitle),
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"",
            timeout ?? DefaultWaitTimeout,
            () => $"It shows \"{string.Join(" | ", NamesOnScreen("UnstagedTitle"))}\" and \"{string.Join(" | ", NamesOnScreen("StagedTitle"))}\".");
    }

    /// <summary>
    /// The platform's undo on the real keyboard: Ctrl+Z on Windows (the report's conventions, the
    /// first gesture of <c>HotkeyConfiguration.Undo</c>, which the scripted run's
    /// <c>ShellDriver.Undo()</c> presses).
    /// </summary>
    public void PressUndo() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);

    /// <summary>Select all on the real keyboard: Ctrl+A in the focused text box.</summary>
    public void PressSelectAll() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);

    /// <summary>Whether the interactive rebase's list is shown.</summary>
    public bool IsRebaseListShown => FindAll(RebaseListId).Any(element => !element.IsOffscreen);

    /// <summary>
    /// Waits until the list's row <paramref name="name"/> (<c>{short} {subject}</c>) has the
    /// keyboard focus: the selected row has it (design 2.2), and UI Automation reports no
    /// selection of its own for the rows.
    /// </summary>
    public void WaitForRebaseRowFocus(string name, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        WaitForTelling(
            () => RebaseRows().Where(row => HasKeyboardFocus(row)).Select(row => row.Name).SequenceEqual([name]),
            $"the list's row \"{name}\" alone to have the keyboard focus",
            timeout ?? DefaultWaitTimeout,
            () => $"Rows with it: {string.Join(" | ", RebaseRows().Where(row => HasKeyboardFocus(row)).Select(row => row.Name))}.");
    }

    /// <summary>Waits until the row's action button reads <paramref name="action"/> (<c>Pick</c>, <c>Fixup</c>, <c>Squash</c>, <c>Reword</c>, <c>Drop</c>).</summary>
    public void WaitForRebaseAction(string name, string action, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(action);
        WaitFor(
            () => RebaseRows().Any(row => row.Name == name) && RebaseActionButton(name).Name == action,
            $"the row \"{name}\"'s action to read \"{action}\"",
            timeout ?? DefaultWaitTimeout);
    }

    /// <summary>Waits until the list's rows in view are, top to bottom, the rows named <paramref name="names"/>.</summary>
    public void WaitForRebaseRowOrder(IReadOnlyList<string> names, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        WaitForTelling(
            () => RebaseRowNames().SequenceEqual(names),
            $"the list's rows to be {string.Join(", ", names)}",
            timeout ?? DefaultWaitTimeout,
            () => $"They are {string.Join(", ", RebaseRowNames())}.");
    }

    /// <summary>Waits until the second line right under the row reads <paramref name="text"/> (<see cref="RebaseSecondLine"/>).</summary>
    public void WaitForRebaseSecondLine(string name, string text, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(text);
        WaitFor(
            () => RebaseRows().Any(row => row.Name == name) && RebaseSecondLine(name)?.Name == text,
            $"the second line under \"{name}\" to read \"{text}\"",
            timeout ?? DefaultWaitTimeout);
    }

    /// <summary>Waits until the interactive rebase's list has closed.</summary>
    public void WaitForRebaseListClosed(TimeSpan? timeout = null) =>
        WaitFor(() => !IsRebaseListShown, "the interactive rebase list to close", timeout ?? DefaultWaitTimeout);

    /// <summary>
    /// The texts of the block bars in view (<c>ConflictBarState</c>: <c>Used in the result</c> or
    /// <c>Not used</c>, shown in place of a resolved bar's buttons, design 11.5), top to bottom,
    /// then left to right (the ours pane's before the theirs pane's on one row).
    /// </summary>
    public IReadOnlyList<string> BarStateTexts() => NamesOnScreen("ConflictBarState");

    /// <summary>Waits until the result's count (<c>ConflictResultCount</c>) reads <paramref name="count"/>: the resolver has parsed the result (design 11.5, 14.3).</summary>
    public void WaitForResultCount(string count, TimeSpan? timeout = null) => WaitForText("ConflictResultCount", count, timeout);

    /// <summary>Where a tooltip element lies in the client area, and whether that is inside it.</summary>
    public (RectangleF Bounds, bool Inside) ToolTipPlace(AutomationElement tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        var bounds = BoundsOf(tip);
        var client = ClientSize;
        var inside = bounds.Left >= -0.5 && bounds.Top >= -0.5 && bounds.Right <= client.Width + 0.5 && bounds.Bottom <= client.Height + 0.5;
        return (bounds, inside);
    }

    /// <summary>As <see cref="WaitFor"/>; a timeout's message ends with what <paramref name="state"/> says the window shows then.</summary>
    private void WaitForTelling(Func<bool> condition, string what, TimeSpan timeout, Func<string> state)
    {
        try
        {
            WaitFor(condition, what, timeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} {state()}", ex);
        }
    }
}
