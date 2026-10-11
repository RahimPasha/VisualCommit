using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The confirmation dialog's keyboard (D69): the focus stays on the dialog while it is open.</summary>
public class ConfirmationViewTests
{
    [AvaloniaFact]
    public async Task Tab_goes_round_the_dialogs_buttons_and_never_to_the_window_behind()
    {
        var dialogs = new DialogHostViewModel();
        var asking = dialogs.ConfirmAsync(new ConfirmationRequest("Discard changes?", "Discard the changes to a.txt?", string.Empty, "Discard"));
        var behind = new Button { Content = "Behind" };
        var view = new ConfirmationView { DataContext = dialogs.Current };
        using var host = DiffHost.Show(new Panel { Children = { behind, view } }, 800, 600, ThemeVariant.Dark);
        var cancel = view.FindControl<Button>("CancelButton")!;
        var confirm = view.FindControl<Button>("ConfirmButton")!;
        Assert.True(cancel.IsFocused, "Cancel has the focus when the dialog opens.");

        foreach (var (expected, name) in new[] { (confirm, "Discard"), (cancel, "Cancel"), (confirm, "Discard") })
        {
            host.PressKey(Key.Tab);
            Assert.True(expected.IsFocused, $"Tab moves the focus to {name}.");
        }

        host.PressKey(Key.Tab, RawInputModifiers.Shift);
        Assert.True(cancel.IsFocused, "Shift+Tab moves the focus back to Cancel.");
        Assert.False(behind.IsFocused);

        Assert.True(dialogs.CancelCurrent());
        Assert.False(await asking);
    }
}
