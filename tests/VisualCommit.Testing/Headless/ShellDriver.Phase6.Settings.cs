using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>How a check opens the settings screen (report part S's conventions).</summary>
public enum SettingsOpening
{
    /// <summary>A click on the toolbar's <c>SettingsButton</c>.</summary>
    Button,

    /// <summary>The command modifier and <c>,</c> (Ctrl+, , Cmd+, on macOS; 6.1).</summary>
    Key,

    /// <summary>The palette's <c>Open settings…</c>: Ctrl+Shift+P, <c>settings</c>, Enter (5.4).</summary>
    Palette,
}

/// <summary>The Git section's two scopes (7.4).</summary>
public enum SettingsScope
{
    /// <summary><c>All repositories (global)</c>.</summary>
    Global,

    /// <summary><c>This repository</c>.</summary>
    Repository,
}

/// <summary>One control of the settings screen as drawn: its id, text, placeholder, check state, whether it is enabled, its tooltip and help text.</summary>
public sealed record SettingsFieldSnapshot(string Id, string? Text, string? Placeholder, bool? IsChecked, bool IsEnabled, string? ToolTip, string? HelpText);

/// <summary>
/// The settings screen as drawn (<see cref="ShellDriver.SettingsSnapshot"/>): the section shown and
/// every visible control of the card with an automation id, in reading order; several lines of one
/// id (a field's notices) appear in order.
/// </summary>
public sealed record SettingsSnapshot(SettingsSection Section, IReadOnlyList<SettingsFieldSnapshot> Fields)
{
    /// <summary>The control <paramref name="id"/>, or null when none shows.</summary>
    public SettingsFieldSnapshot? Field(string id) => Fields.FirstOrDefault(field => field.Id == id);

    /// <summary>The texts of every shown control <paramref name="id"/>, in order (a field's notice lines).</summary>
    public IReadOnlyList<string> Lines(string id) => [.. Fields.Where(field => field.Id == id).Select(field => field.Text ?? string.Empty)];

    /// <summary>Whether a control <paramref name="id"/> shows.</summary>
    public bool Shows(string id) => Fields.Any(field => field.Id == id);
}

/// <summary>
/// Phase 6's settings screen as the checks drive it (docs/phase-6-design.md 17.2, report part S):
/// opening it and waiting for its reads, its sections, fields and Pick dialogs, Save and Cancel,
/// every action a click or key on the screen's own controls.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The settings screen when it is open (anywhere in the dialog stack), or null.</summary>
    public SettingsDialogViewModel? SettingsDialog =>
        Session.Services.DialogHost.Open.OfType<SettingsDialogViewModel>().LastOrDefault();

    /// <summary>
    /// Opens the settings screen as <paramref name="how"/> says and waits until it shows and its
    /// reads have ended (SD29): <c>GitReading</c> and <c>EditorStatus</c> hidden, the fields filled.
    /// The key and the palette need the router and the palette (work package WP-K).
    /// </summary>
    public async Task<SettingsDialogViewModel> OpenSettingsAsync(SettingsOpening how = SettingsOpening.Button, TimeSpan? timeout = null)
    {
        switch (how)
        {
            case SettingsOpening.Button:
                Click(FindByAutomationId("SettingsButton"));
                break;
            case SettingsOpening.Key:
                PressKey(Key.OemComma, Raw(CommandModifier));
                break;
            case SettingsOpening.Palette:
                PressKey(Key.P, Raw(CommandModifier | KeyModifiers.Shift));
                Settle();
                Type("settings");
                PressKey(Key.Enter);
                break;
        }

        var dialog = await WaitForDialogAsync<SettingsDialogViewModel>(timeout);
        await WaitForSettingsReadsAsync(dialog, timeout);
        return dialog;
    }

    /// <summary>Waits until the open settings screen's reads have ended and their fields show (its opening's or a read back's).</summary>
    public async Task WaitForSettingsReadsAsync(SettingsDialogViewModel? dialog = null, TimeSpan? timeout = null)
    {
        dialog ??= SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        var loaded = dialog.Loaded;
        await WaitForAsync(() => loaded.IsCompleted && !dialog.Git.IsReading && !dialog.Tools.IsLooking, "the settings screen's reads", timeout);
        Settle();
    }

    /// <summary>
    /// The open settings screen as drawn: the section shown and each visible control of its card
    /// with an automation id (the section list, the shown section, the buttons), in reading order.
    /// </summary>
    public SettingsSnapshot SettingsSnapshot()
    {
        Settle();
        var dialog = SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        var view = SettingsView();
        var fields = view.GetVisualDescendants().OfType<Control>()
            .Where(control => AutomationProperties.GetAutomationId(control) is { Length: > 0 } && control.IsEffectivelyVisible && control.Bounds.Height > 0)
            .OrderBy(control => BoundsOf(control).Y)
            .ThenBy(control => BoundsOf(control).X)
            .Select(SnapshotOf)
            .ToList();
        return new SettingsSnapshot(dialog.SelectedSection, fields);
    }

    /// <summary>Shows a section by a click on its button (<c>SettingsNav…</c>).</summary>
    public void ChooseSection(SettingsSection section)
    {
        Click(SettingsControl(section switch
        {
            SettingsSection.Git => "SettingsNavGit",
            SettingsSection.Tools => "SettingsNavTools",
            SettingsSection.KeyboardShortcuts => "SettingsNavShortcuts",
            _ => "SettingsNavGeneral",
        }));
        Settle();
    }

    /// <summary>Shows a scope of the Git section by a click on <c>GitScopeGlobal</c> or <c>GitScopeRepository</c>.</summary>
    public void ChooseScope(SettingsScope scope)
    {
        if (SettingsDialog?.SelectedSection != SettingsSection.Git)
        {
            ChooseSection(SettingsSection.Git);
        }

        Click(SettingsControl(scope == SettingsScope.Global ? "GitScopeGlobal" : "GitScopeRepository"));
        Settle();
    }

    /// <summary>Replaces the name and the email of <paramref name="scope"/> (null leaves a box as it is; empty clears it).</summary>
    public void SetIdentity(SettingsScope scope, string? name, string? email)
    {
        ChooseScope(scope);
        if (name is not null)
        {
            ReplaceText("UserNameBox", name);
        }

        if (email is not null)
        {
            ReplaceText("UserEmailBox", email);
        }

        Settle();
    }

    /// <summary>Clicks the pull mode's radio of <paramref name="scope"/>: <c>PullNotSet</c> for <see cref="PullMode.NotSet"/>.</summary>
    public void ChoosePullMode(SettingsScope scope, PullMode mode)
    {
        ChooseScope(scope);
        Click(SettingsControl(mode switch
        {
            PullMode.Merge => "PullMerge",
            PullMode.MergeCommit => "PullMergeCommit",
            PullMode.Rebase => "PullRebase",
            PullMode.RebaseMerges => "PullRebaseMerges",
            PullMode.FastForwardOnly => "PullFastForwardOnly",
            _ => "PullNotSet",
        }));
        Settle();
    }

    /// <summary>The merge tool of the shown Git scope: <c>Choose…</c>, the Pick dialog's item <paramref name="name"/> (<c>Not set</c> for none), then <c>Choose</c>.</summary>
    public async Task ChooseMergeToolAsync(string name, TimeSpan? timeout = null)
    {
        if (SettingsDialog?.SelectedSection != SettingsSection.Git)
        {
            ChooseSection(SettingsSection.Git);
        }

        Click(SettingsControl("MergeToolChooseButton"));
        await PickAsync(name, timeout);
    }

    /// <summary>The editor: Tools, <c>Choose…</c>, the Pick dialog's item <paramref name="name"/> (<c>None</c>, an editor's name, <c>Other…</c>), then <c>Choose</c>.</summary>
    public async Task ChooseEditorAsync(string name, TimeSpan? timeout = null)
    {
        if (SettingsDialog?.SelectedSection != SettingsSection.Tools)
        {
            ChooseSection(SettingsSection.Tools);
        }

        Click(SettingsControl("EditorChooseButton"));
        await PickAsync(name, timeout);
    }

    /// <summary>The open Pick dialog's item <paramref name="text"/> clicked, then <c>Choose</c>; waits until the Pick dialog has closed.</summary>
    public async Task PickAsync(string text, TimeSpan? timeout = null)
    {
        var pick = await WaitForDialogAsync<PickDialogViewModel>(timeout);
        Click(PickItem(text));
        Settle();
        Click(FindByAutomationId(TopDialogView!, "ConfirmButton"));
        await WaitForAsync(() => pick.IsClosed, "the Pick dialog to close", timeout);
        Settle();
    }

    /// <summary>The open Pick dialog's item whose text is <paramref name="text"/>.</summary>
    public ListBoxItem PickItem(string text)
    {
        var list = (ListBox)FindByAutomationId(TopDialogView ?? throw new InvalidOperationException("No dialog is open."), "PickList");
        return list.GetVisualDescendants().OfType<ListBoxItem>()
            .FirstOrDefault(item => item.IsEffectivelyVisible && text.Equals(item.DataContext))
            ?? throw new InvalidOperationException($"The Pick dialog has no item \"{text}\".");
    }

    /// <summary>General's auto-fetch: the check box clicked to <paramref name="on"/>, then, when given, <paramref name="minutes"/> typed over the box's text.</summary>
    public void SetAutoFetch(bool on, int? minutes = null)
    {
        ShowGeneral();
        var box = (CheckBox)SettingsControl("AutoFetchCheckBox");
        if (box.IsChecked != on)
        {
            Click(box);
        }

        if (minutes is not null)
        {
            ReplaceText("AutoFetchMinutesBox", minutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Settle();
    }

    /// <summary>General's <c>KeepIgnoredCheckBox</c> clicked to <paramref name="on"/>.</summary>
    public void SetKeepIgnoredFiles(bool on)
    {
        ShowGeneral();
        var box = (CheckBox)SettingsControl("KeepIgnoredCheckBox");
        if (box.IsChecked != on)
        {
            Click(box);
        }

        Settle();
    }

    /// <summary>General's <c>RunnableExtraBox</c>: <paramref name="text"/> typed after what it holds.</summary>
    public void AddOpenFileExtension(string text)
    {
        ShowGeneral();
        TypeInto("RunnableExtraBox", text);
        Settle();
    }

    /// <summary>
    /// Clicks <c>Save</c> and waits for the whole Save (7.8): a refused one ends after its
    /// validation with the screen open; one that closes the screen ends after its writes, the read
    /// back and the close. Returns whether the screen closed.
    /// </summary>
    public async Task<bool> SaveSettingsAsync(TimeSpan? timeout = null)
    {
        var dialog = SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        Click(SettingsControl("ConfirmButton"));
        await WaitForSettingsSaveAsync(dialog, timeout);
        return dialog.IsClosed;
    }

    /// <summary>Waits until a Save of <paramref name="dialog"/> started by a click or by Enter has ended (its validation, writes and read back).</summary>
    public async Task WaitForSettingsSaveAsync(SettingsDialogViewModel dialog, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        Settle();
        await WaitForAsync(() => !dialog.ConfirmCommand.IsRunning, "the settings screen's Save", timeout);
        Settle();
    }

    /// <summary>Clicks <c>Cancel</c>: the screen closes without asking.</summary>
    public void CancelSettings()
    {
        Click(SettingsControl("CancelButton"));
        Settle();
    }

    private void ShowGeneral()
    {
        if (SettingsDialog?.SelectedSection != SettingsSection.General)
        {
            ChooseSection(SettingsSection.General);
        }
    }

    /// <summary>The settings screen's view as the dialog layer draws it (also under a Pick dialog).</summary>
    private Control SettingsView()
    {
        var dialog = SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        return Window.GetVisualDescendants().OfType<ContentPresenter>()
            .FirstOrDefault(presenter => ReferenceEquals(presenter.Content, dialog) && presenter.Child is not null)?.Child
            ?? throw new InvalidOperationException("The settings screen is not drawn.");
    }

    /// <summary>The settings screen's visible control <paramref name="automationId"/>.</summary>
    private Control SettingsControl(string automationId) => FindByAutomationId(SettingsView(), automationId);

    private static SettingsFieldSnapshot SnapshotOf(Control control)
    {
        var id = AutomationProperties.GetAutomationId(control)!;
        var (text, placeholder) = control switch
        {
            TextBox box => (box.Text, box.PlaceholderText),
            TextBlock block => (block.Text, null),
            ContentControl { Content: string content } => (content, null),
            ContentControl content => (VisibleTexts.In(content).FirstOrDefault(), null),
            _ => (AutomationProperties.GetName(control), null),
        };
        bool? isChecked = control is ToggleButton toggle ? toggle.IsChecked : null;
        return new SettingsFieldSnapshot(id, text, placeholder, isChecked, control.IsEffectivelyEnabled, ToolTip.GetTip(control) as string, AutomationProperties.GetHelpText(control));
    }
}
