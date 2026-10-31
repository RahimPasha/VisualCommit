using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// "Add GitHub account" (docs/phase-7a-design.md 8.4). On opening, the token box takes the keyboard
/// focus; Enter signs in (<see cref="DialogView"/>). After a refusal the box takes the focus back
/// with its text selected, so that a new token replaces it; after a failed save, "Close" does.
/// </summary>
public partial class SignInDialogView : DialogView
{
    private SignInDialogViewModel? _model;

    public SignInDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as SignInDialogViewModel);
    }

    protected override TextBox? InitialTextBox => SignInToken;

    private void Follow(SignInDialogViewModel? model)
    {
        if (_model is not null)
        {
            _model.FocusRequested -= OnFocusRequested;
        }

        _model = model;
        if (model is not null)
        {
            model.FocusRequested += OnFocusRequested;
        }
    }

    /// <summary>Focuses the control <paramref name="automationId"/> once the layout has enabled it again.</summary>
    private void OnFocusRequested(object? sender, string automationId) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                var control = this.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(candidate => AutomationProperties.GetAutomationId(candidate) == automationId && candidate.IsEffectivelyVisible && candidate.IsEffectivelyEnabled);
                if (control is null)
                {
                    return;
                }

                control.Focus(control is TextBox ? NavigationMethod.Unspecified : NavigationMethod.Tab);
                if (control is TextBox box)
                {
                    box.SelectAll();
                }
            },
            DispatcherPriority.Loaded);
}
