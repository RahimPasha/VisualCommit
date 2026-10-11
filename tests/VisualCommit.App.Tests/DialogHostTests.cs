using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The dialog layer as a stack (D104, docs/phase-3-design.md 4f) and its view locator.</summary>
public class DialogHostTests
{
    [Fact]
    public async Task A_dialog_opened_over_another_goes_on_top_and_Esc_cancels_the_top_one_only()
    {
        var host = new DialogHostViewModel();

        var below = host.ConfirmAsync(new ConfirmationRequest("Remotes", "Below", string.Empty, "Close"));
        var onTop = host.ConfirmAsync(new ConfirmationRequest("Remove remote origin?", "On top", string.Empty, "Remove"));

        Assert.Equal(["Remotes", "Remove remote origin?"], host.Open.Select(dialog => dialog.Title));
        Assert.Equal("Remove remote origin?", host.Current!.Title);
        Assert.True(host.CancelCurrent());
        Assert.False(await onTop);
        Assert.False(below.IsCompleted);
        Assert.Equal("Remotes", host.Current!.Title);

        ((ConfirmationViewModel)host.Current).ConfirmCommand.Execute(null);

        Assert.True(await below);
        Assert.Empty(host.Open);
        Assert.Null(host.Current);
        Assert.False(host.IsOpen);
        Assert.False(host.CancelCurrent());
    }

    [Fact]
    public async Task A_dialog_that_closes_below_the_top_one_leaves_the_top_one_current()
    {
        var host = new DialogHostViewModel();
        var finishing = new FinishingDialogViewModel([new FinishingItem("Push main — graph", false)]);
        var quitting = host.ShowAsync(finishing);
        var asking = host.ConfirmAsync(new ConfirmationRequest("Sign in?", "On top", string.Empty, "Sign in"));

        finishing.CloseAsFinished();

        Assert.False(await quitting);
        Assert.True(finishing.IsClosed);
        Assert.Equal("Sign in?", host.Current!.Title);
        Assert.True(host.CancelCurrent());
        Assert.False(await asking);
        Assert.Null(host.Current);
    }

    [AvaloniaFact]
    public async Task The_view_of_a_dialog_is_found_by_its_type()
    {
        var host = new DialogHostViewModel();
        var asking = host.ConfirmAsync(new ConfirmationRequest("Discard changes?", "Discard?", string.Empty, "Discard"));
        var locator = new DialogViewLocator();

        Assert.True(locator.Match(host.Current));
        Assert.IsType<ConfirmationView>(locator.Build(host.Current));
        Assert.False(locator.Match(new object()));

        host.CancelCurrent();
        await asking;
    }

    [AvaloniaFact]
    public async Task In_the_window_the_dialog_below_stays_drawn_and_gets_the_focus_back_when_the_top_one_closes()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Shell.Dialogs;

        var below = dialogs.ConfirmAsync(new ConfirmationRequest("Remotes", "Below", string.Empty, "Close"));
        app.Settle();
        var belowView = Views(app).Single();
        var belowCancel = belowView.FindControl<Button>("CancelButton")!;
        Assert.True(belowCancel.IsFocused);

        var onTop = dialogs.ConfirmAsync(new ConfirmationRequest("Remove remote origin?", "On top", string.Empty, "Remove"));
        app.Settle();
        var views = Views(app);
        Assert.Equal(2, views.Count);
        Assert.True(views[1].FindControl<Button>("CancelButton")!.IsFocused);
        Assert.True(belowView.IsEffectivelyVisible);

        app.PressKey(Key.Escape);

        Assert.False(await onTop);
        Assert.Single(Views(app));
        Assert.True(belowCancel.IsFocused, "The dialog below gets the focus back.");
        app.PressKey(Key.Escape);
        Assert.False(await below);
        Assert.Empty(Views(app));
    }

    private static List<ConfirmationView> Views(ShellDriver app) =>
        [.. app.Window.GetVisualDescendants().OfType<ConfirmationView>()];
}
