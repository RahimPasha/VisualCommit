using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The settings screen in the real window (docs/phase-6-design.md 7, 15.5, 17.2): opened by its
/// key (a real Ctrl+,), its sections by their buttons and the controls only each section shows,
/// its fields by id, Save and Cancel inside its own card (a Pick dialog stacked on it has buttons
/// of the same ids), and the Pick dialog's options with their details (7.7). Every wait is on a
/// condition with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The settings screen's root (a pane named <c>Settings</c>).</summary>
    public const string SettingsDialogId = "SettingsDialog";

    /// <summary>The Pick dialog's root (a pane named by its title).</summary>
    public const string PickDialogId = "PickDialog";

    /// <summary>The settings screen's sections by the name of their button in its section list (7.1).</summary>
    public static IReadOnlyDictionary<string, string> SettingsNavIds { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["General"] = "SettingsNavGeneral",
        ["Git"] = "SettingsNavGit",
        ["Tools"] = "SettingsNavTools",
        ["Keyboard shortcuts"] = "SettingsNavShortcuts",
    };

    /// <summary>Ctrl+, on the real keyboard: the settings screen's key (6.1, D197).</summary>
    public void PressSettingsKey() => PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);

    /// <summary>Whether the settings screen shows.</summary>
    public bool IsSettingsShown => ShownElements(SettingsDialogId).Count == 1;

    /// <summary>Waits until the settings screen shows, and returns its root.</summary>
    public AutomationElement WaitForSettings(TimeSpan timeout)
    {
        WaitFor(() => IsSettingsShown, "the settings screen", timeout);
        return ShownElements(SettingsDialogId)[0];
    }

    /// <summary>
    /// The section the screen shows, by a control only that section shows (the sections' panels are
    /// hidden, not removed): <c>General</c> (its theme's radios), <c>Git</c> (the scope switch),
    /// <c>Tools</c> (the editor's value), <c>Keyboard shortcuts</c> (its intro line); null for none.
    /// </summary>
    public string? ShownSettingsSection() =>
        ShownElements("SettingsThemeDark").Count == 1 ? "General"
        : ShownElements("GitScopeGlobal").Count == 1 ? "Git"
        : ShownElements("EditorValue").Count == 1 ? "Tools"
        : ShownElements("ShortcutsIntro").Count == 1 ? "Keyboard shortcuts"
        : null;

    /// <summary>Waits until the screen shows the section <paramref name="section"/> (<see cref="ShownSettingsSection"/>).</summary>
    public void WaitForSettingsSection(string section, TimeSpan timeout) =>
        WaitForTelling(
            () => ShownSettingsSection() == section,
            $"the settings screen to show {section}",
            timeout,
            () => $"It shows {ShownSettingsSection() ?? "no section"}.");

    /// <summary>Clicks the section's button in the screen's list with the real mouse and waits until the section shows.</summary>
    public void ChooseSettingsSection(string section, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(section);
        ClickWithMouse(Find(SettingsNavIds[section]));
        WaitForSettingsSection(section, timeout);
    }

    /// <summary>The settings screen's own button <paramref name="automationId"/> (<c>CancelButton</c>, <c>ConfirmButton</c>, Save), inside its card.</summary>
    public AutomationElement SettingsScreenButton(string automationId) => FindIn(WaitForSettings(DefaultWaitTimeout), automationId);

    /// <summary>Waits until the screen's Git section has read git's configuration: the scope switch shows and <c>Reading git's configuration…</c> does not (7.4).</summary>
    public void WaitForGitSettingsRead(TimeSpan timeout) =>
        WaitFor(() => ShownElements("GitScopeGlobal").Count == 1 && ShownElements("GitReading").Count == 0, "the Git section to have read git's configuration", timeout);

    // ---- The Pick dialog (7.7) ----

    /// <summary>One option of the Pick dialog: its text (its name), its detail (its help text; empty for none), whether it is selected.</summary>
    public sealed record PickOption(string Text, string Detail, bool IsSelected, AutomationElement Element);

    /// <summary>Waits until the Pick dialog titled <paramref name="title"/> shows, stacked on what opened it, and returns its root.</summary>
    public AutomationElement WaitForPick(string title, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        WaitForTelling(
            () => ShownElements(PickDialogId) is [var pick] && FindIn(pick, "PickTitle").Name == title,
            $"the Pick dialog \"{title}\"",
            timeout,
            () => $"{ShownElements(PickDialogId).Count} Pick dialogs show.");
        return ShownElements(PickDialogId)[0];
    }

    /// <summary>
    /// The options of the Pick dialog <paramref name="pick"/>, top to bottom (<c>PickItem</c>, named by
    /// its text): each with its detail (7.7) as its help text, which the view sets on each item it
    /// makes (<c>PickDialogView</c>'s <c>ContainerPrepared</c>: the whole detail, even where it is drawn
    /// cut at its start), empty for an option without one.
    /// </summary>
    public IReadOnlyList<PickOption> PickOptions(AutomationElement pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        return [.. pick.FindAllDescendants(condition => condition.ByAutomationId("PickItem"))
            .Where(item => item.BoundingRectangle is { Width: > 0, Height: > 0 })
            .OrderBy(item => item.BoundingRectangle.Top)
            .Select(item => new PickOption(item.Name, HelpTextOf(item), IsSelected(item), item))];
    }

    /// <summary>
    /// Clicks the option <paramref name="text"/> of the Pick dialog <paramref name="pick"/>, then its
    /// confirm button (<c>Choose</c>), with the real mouse, and waits until the dialog has gone.
    /// </summary>
    public void ChoosePickOption(AutomationElement pick, string text, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentException.ThrowIfNullOrEmpty(text);
        var option = PickOptions(pick).Where(candidate => candidate.Text == text).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"The Pick dialog has {found.Count} options \"{text}\"; it has: {string.Join(" | ", PickOptions(pick).Select(candidate => candidate.Text))}."),
        };
        ClickWithMouse(option.Element);
        WaitFor(() => IsSelected(option.Element), $"the option \"{text}\" to be selected", timeout);
        ClickWithMouse(FindIn(pick, "ConfirmButton"));
        WaitUntilGone(PickDialogId, timeout);
    }
}
