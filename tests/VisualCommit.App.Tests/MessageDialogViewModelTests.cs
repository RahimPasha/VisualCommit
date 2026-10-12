using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The message dialog's contract (docs/phase-4-design.md 7.2, 7.4): how a message is split into
/// the summary and the description and joined again, the counter, the confirm button and its
/// reason, and the plain view with its automation ids. Work package WP5 tests its final look.
/// </summary>
public class MessageDialogViewModelTests
{
    [Theory]
    [InlineData("Add login", "Add login", "")]
    [InlineData("Add login\n", "Add login", "")]
    [InlineData("Add login\n\nThe form and its checks.\n", "Add login", "The form and its checks.")]
    [InlineData("\n\nAdd login  \r\n\r\n\r\nFirst line\r\n\r\nSecond paragraph\r\n\r\n", "Add login", "First line\n\nSecond paragraph")]
    [InlineData("Three\n\n#42 is the issue\n# not a comment either", "Three", "#42 is the issue\n# not a comment either")]
    [InlineData("", "", "")]
    public void A_message_is_split_into_its_first_line_and_the_rest_with_no_line_removed(string message, string summary, string description)
    {
        Assert.Equal((summary, description), MessageDialogViewModel.Split(message));
    }

    [Fact]
    public void The_parts_are_joined_by_a_blank_line_and_the_summary_stands_alone_without_a_description()
    {
        Assert.Equal("Three\n\n#42 is the issue", MessageDialogViewModel.Join("Three", "#42 is the issue"));
        Assert.Equal("Three", MessageDialogViewModel.Join("Three", string.Empty));
        Assert.Equal("Three", MessageDialogViewModel.Join("Three", "\n  "));

        var (summary, description) = MessageDialogViewModel.Split("Squash\n\nOne\n\nTwo\n");
        Assert.Equal("Squash\n\nOne\n\nTwo", MessageDialogViewModel.Join(summary, description));
    }

    [Fact]
    public async Task The_counter_the_confirm_button_and_the_answer_follow_the_summary()
    {
        var dialog = new MessageDialogViewModel("Reword 1a2b3c4", "Add login\n\nThe form.", "Save");

        Assert.Equal(("Reword 1a2b3c4", "Save"), (dialog.Title, dialog.ConfirmText));
        Assert.Equal(("Add login", "The form."), (dialog.Summary, dialog.Description));
        Assert.Equal(("63", false), (dialog.SummaryCounter, dialog.IsSummaryOverLimit));
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        Assert.Null(dialog.ConfirmToolTip);

        dialog.Summary = "   ";
        Assert.False(dialog.CanConfirm);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        Assert.Equal("Write a summary", dialog.ConfirmToolTip);

        dialog.Summary = new string('x', 75);
        Assert.Equal(("-3", true), (dialog.SummaryCounter, dialog.IsSummaryOverLimit));

        dialog.Summary = "Add the login form";
        dialog.Description = "#42 is the issue";
        Assert.Equal("Add the login form\n\n#42 is the issue", dialog.Message);
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("Add the login form\n\n#42 is the issue", await dialog.Result);

        var cancelled = new MessageDialogViewModel("Squash 2 commits", "One\n\nTwo", "Squash");
        cancelled.Cancel();
        Assert.Null(await cancelled.Result);
    }

    [AvaloniaFact]
    public async Task The_plain_view_has_its_ids_puts_the_caret_at_the_summarys_end_and_Enter_there_confirms()
    {
        Assert.IsType<MessageDialogView>(new DialogViewLocator().Build(new MessageDialogViewModel("t", "m", "Save")));

        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialog = new MessageDialogViewModel("Message of the squashed commit", "Three\n\n#42 is the issue", "Save");
        var answer = app.Session.Services.DialogHost.ShowAsync(dialog);
        app.Settle();

        Assert.Equal("Message of the squashed commit", ((TextBlock)app.FindByAutomationId("MessageDialogTitle")).Text);
        var summary = (TextBox)app.FindByAutomationId("MessageSummary");
        var description = (TextBox)app.FindByAutomationId("MessageDescription");
        Assert.True(summary.IsFocused);
        Assert.Equal(("Three", 5), (summary.Text, summary.CaretIndex));
        Assert.Equal("#42 is the issue", description.Text);
        Assert.Equal("67", ((TextBlock)app.FindByAutomationId("MessageSummaryCounter")).Text);
        Assert.Equal(120, description.Height);
        Assert.True(description.AcceptsReturn);
        Assert.NotNull(app.FindByAutomationId("MessageDialog"));
        Assert.Equal("Cancel", ((Button)app.FindByAutomationId("CancelButton")).Content);
        var confirm = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.Equal("Save", confirm.Content);

        summary.Text = string.Empty;
        app.Settle();
        Assert.False(confirm.IsEffectivelyEnabled);
        Assert.Equal("Write a summary", ToolTip.GetTip(confirm));

        app.TypeInto("MessageSummary", "Three, joined");
        app.PressKey(Key.Enter);
        Assert.Equal("Three, joined\n\n#42 is the issue", await answer);
    }
}
