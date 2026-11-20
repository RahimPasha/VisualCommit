using Avalonia.Input;
using Avalonia.Interactivity;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The repository browser "Clone from GitHub" (<c>CloneFromGitHubDialog</c>, docs/phase-7a-design.md
/// 9.4, D301), laid out as the pick dialog: the filter box (<c>CloneFromGitHubFilter</c>) takes the
/// focus, the list (<c>CloneFromGitHubList</c>, items <c>CloneFromGitHubItem</c> named by their full
/// names) shows a row's detail after its name, and the lines of the list's state follow it. Enter in
/// the list chooses its selected row, as "Choose" does.
/// </summary>
public partial class CloneFromGitHubDialogView : DialogView
{
    public CloneFromGitHubDialogView()
    {
        InitializeComponent();

        // Taken on the way down: the list's item takes Enter for itself (it selects its row), so the
        // dialog's own Enter never sees it there.
        CloneFromGitHubList.AddHandler(KeyDownEvent, OnListEnter, RoutingStrategies.Tunnel);
    }

    protected override Avalonia.Controls.TextBox? InitialTextBox => CloneFromGitHubFilter;

    /// <summary>
    /// Enter, with the focus in the list and a row selected, runs the confirm button's command when
    /// it can run, as a click on "Choose" does; with no row selected the item has it.
    /// </summary>
    private void OnListEnter(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || CloneFromGitHubList.SelectedItem is null)
        {
            return;
        }

        if (ConfirmButton is { IsEffectivelyEnabled: true, Command: { } command } && command.CanExecute(ConfirmButton.CommandParameter))
        {
            command.Execute(ConfirmButton.CommandParameter);
            e.Handled = true;
        }
    }
}
