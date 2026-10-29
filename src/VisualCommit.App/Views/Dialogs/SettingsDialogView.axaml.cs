using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The settings screen (<c>SettingsDialog</c>, docs/phase-6-design.md 7.1): its first focus is
/// the shown section's button, never a text box; Up and Down move between the section buttons;
/// the shown section starts at its top; a field the view model names takes the focus (the first
/// wrong one after a refused Save, Other…'s program box).
/// </summary>
public partial class SettingsDialogView : DialogView
{
    private SettingsDialogViewModel? _model;

    public SettingsDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as SettingsDialogViewModel);
        SettingsNav.AddHandler(KeyDownEvent, OnNavKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The settings screen gives its first focus to a section button, not a text box (7.1).</summary>
    protected override bool FocusesTextBoxOnOpening => false;

    /// <summary>The shown section's button.</summary>
    protected override Control? InitialFocus => NavButtonOf(_model?.SelectedSection ?? SettingsSection.General);

    private Button NavButtonOf(SettingsSection section) => section switch
    {
        SettingsSection.Git => SettingsNavGit,
        SettingsSection.Tools => SettingsNavTools,
        SettingsSection.KeyboardShortcuts => SettingsNavShortcuts,
        SettingsSection.Accounts => SettingsNavAccounts,
        _ => SettingsNavGeneral,
    };

    private void Follow(SettingsDialogViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.FocusRequested -= OnFocusRequested;
        }

        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.FocusRequested += OnFocusRequested;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsDialogViewModel.SelectedSection))
        {
            // Each section starts at its top.
            SectionScroller.Offset = default;
        }
    }

    /// <summary>Focuses the control <paramref name="automationId"/> once the layout shows it (the section may just have changed).</summary>
    private void OnFocusRequested(object? sender, string automationId) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                var control = this.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(candidate => AutomationProperties.GetAutomationId(candidate) == automationId && candidate.IsEffectivelyVisible);
                if (control is null)
                {
                    return;
                }

                control.Focus(NavigationMethod.Tab);
                control.BringIntoView();
                if (control is TextBox box)
                {
                    // The caret at the end and nothing selected, as a dialog's box on opening: the
                    // Tab focus selects the whole text, which a caret already at the end leaves
                    // (clearing the selection puts the caret at its start, so it comes first).
                    box.ClearSelection();
                    box.CaretIndex = box.Text?.Length ?? 0;
                }
            },
            DispatcherPriority.Loaded);

    /// <summary>Up and Down move between the section buttons (7.1); the section shows on a click or Enter only.</summary>
    private void OnNavKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        var buttons = SettingsNav.Children.OfType<Button>().ToList();
        var index = buttons.FindIndex(button => button.IsFocused);
        if (index < 0)
        {
            return;
        }

        var next = e.Key == Key.Up ? Math.Max(0, index - 1) : Math.Min(buttons.Count - 1, index + 1);
        buttons[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
