using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The message dialog's contract (docs/phase-4-design.md 7.2, 7.4): how a message is split into
/// the summary and the description and joined again, the counter, the confirm button and its
/// reason, and the view with its automation ids; then the view as the report's part B uses it
/// (docs/test-reports/phase-4.md, "The message dialog"): the wide card, the boxes' sizes, the
/// focus and caret, the empty summary's disabled button and its tooltip, Enter in each box, Esc,
/// the counter past 72, and the squash dialog of section 3.
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
        Assert.Equal("Three\n\nFirst\nSecond", MessageDialogViewModel.Join("Three", "First\r\nSecond"));

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

    private static RawInputModifiers CommandModifier(ShellDriver app) =>
        app.CommandModifier == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [AvaloniaFact]
    public async Task The_dialog_is_on_the_wide_card_and_Enter_in_the_description_starts_a_new_line_while_Esc_cancels()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var answer = app.Session.Services.DialogHost.ShowAsync(new MessageDialogViewModel("Reword 938844a", "Two\n", "Save"));
        app.Settle();

        var dialog = app.MessageDialog();
        Assert.Equal(
            ("Reword 938844a", "Two", 3, true, "69", "", false, "Save", true, (string?)null, "Cancel", (string?)null),
            (dialog.Title, dialog.Summary, dialog.SummaryCaret, dialog.SummaryHasFocus, dialog.Counter, dialog.Description, dialog.DescriptionHasFocus,
                dialog.ConfirmText, dialog.ConfirmEnabled, dialog.ConfirmToolTip, dialog.CancelText, dialog.Repository));
        Assert.Equal(460, dialog.CardBounds.Width);
        Assert.Equal((550.0, 350.0), (dialog.CardBounds.Center.X, dialog.CardBounds.Center.Y));
        Assert.Equal(32, dialog.SummaryBounds.Height);
        Assert.Equal(120, dialog.DescriptionBounds.Height);
        Assert.Equal(dialog.SummaryBounds.Width, dialog.DescriptionBounds.Width);
        Assert.Equal(8, dialog.DescriptionBounds.Y - dialog.SummaryBounds.Bottom);

        // Enter in the description is a new line (the platform's), not the confirm button; the
        // answer has line feeds.
        app.Click(dialog.DescriptionBounds.Center);
        app.Type("First line");
        app.PressKey(Key.Enter);
        app.Type("Second line");
        Assert.Equal("First line\nSecond line", app.MessageDialog().Description.ReplaceLineEndings("\n"));
        Assert.False(answer.IsCompleted);
        Assert.Equal("Two\n\nFirst line\nSecond line", app.Dialog<MessageDialogViewModel>().Message);

        app.PressKey(Key.Escape);
        Assert.Null(await answer);
        Assert.Null(app.TopDialogView);
    }

    [AvaloniaFact]
    public async Task An_empty_summary_disables_Save_with_its_tooltip_and_the_counter_warns_past_72()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var answer = app.Session.Services.DialogHost.ShowAsync(new MessageDialogViewModel("Message of the squashed commit", "Three\n\n#42 is the issue\n\nFour\n", "Save"));
        app.Settle();
        Assert.Equal(("Three", "67", "#42 is the issue\n\nFour"), (app.MessageDialog().Summary, app.MessageDialog().Counter, app.MessageDialog().Description));

        // B7 step 7: select all and Delete in the summary.
        app.PressKey(Key.A, CommandModifier(app));
        app.PressKey(Key.Delete);
        var empty = app.MessageDialog();
        Assert.Equal(("", "72", false, "Write a summary"), (empty.Summary, empty.Counter, empty.ConfirmEnabled, empty.ConfirmToolTip));
        Assert.Equal("Write a summary", await app.RestPointerOnAsync(app.FindByAutomationId("ConfirmButton")));
        app.PressKey(Key.Enter);
        Assert.False(answer.IsCompleted);

        // Past 72 characters the counter is negative, in the warning colour.
        app.TypeInto("MessageSummary", new string('x', 73));
        var counter = (TextBlock)app.FindByAutomationId("MessageSummaryCounter");
        Assert.Equal("-1", counter.Text);
        Assert.Equal(Color.Parse("#E0A23B"), ((ISolidColorBrush)counter.Foreground!).Color);

        app.ReplaceText("MessageSummary", "Three and four");
        app.Click(app.MessageDialog().DescriptionBounds.Center);
        app.PressKey(Key.A, CommandModifier(app));
        app.Type("#42 is the issue");
        Assert.Equal("58", app.MessageDialog().Counter);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.Equal("Three and four\n\n#42 is the issue", await answer);
    }

    [AvaloniaFact]
    public async Task The_squash_dialog_starts_with_the_commits_messages_oldest_first_and_answers_them_joined()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var message = RebaseTodo.SquashMessage(["Three\n\n#42 is the issue\n", "Four\n"]);
        var answer = app.Session.Services.DialogHost.ShowAsync(new MessageDialogViewModel("Squash 2 commits", message, "Squash") { RepositoryName = "interactive" });
        app.Settle();

        var dialog = app.MessageDialog();
        Assert.Equal(
            ("Squash 2 commits", "Three", "67", "#42 is the issue\n\nFour", "Squash", true, "In interactive"),
            (dialog.Title, dialog.Summary, dialog.Counter, dialog.Description, dialog.ConfirmText, dialog.ConfirmEnabled, dialog.Repository));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.Equal("Three\n\n#42 is the issue\n\nFour", await answer);
    }
}
