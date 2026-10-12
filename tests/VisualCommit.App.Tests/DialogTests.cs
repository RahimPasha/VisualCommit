using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The dialogs of phase 3 (docs/phase-3-design.md 4f, D104, D114): each view model's rules, and
/// in the window the views, the focus, Enter and Esc, the stack, and UI Automation.
/// </summary>
public class DialogTests
{
    private static readonly string[] OpsBranches = ["feature/alpha", "feature/conflict", "feature/ff", "main"];

    // ---- The name dialog: branches -------------------------------------------------------

    [Fact]
    public void Creating_a_branch_opens_empty_with_no_message_and_the_button_disabled()
    {
        var dialog = BranchNameDialogViewModel.Create("From 88d6b91 \"Main work\"", OpsBranches, ignoreCase: true);

        Assert.Equal("Create branch", dialog.Title);
        Assert.Equal("Create branch", dialog.ConfirmText);
        Assert.Equal("From 88d6b91 \"Main work\"", dialog.Context);
        Assert.True(dialog.ShowsName);
        Assert.Equal("Branch name", dialog.NamePlaceholder);
        Assert.Equal(string.Empty, dialog.Name);
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.False(dialog.HasNameError);
        Assert.False(dialog.CanConfirm);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        Assert.True(dialog.ShowsOption);
        Assert.Equal("Check out the new branch", dialog.OptionText);
        Assert.True(dialog.IsOptionChecked);
        Assert.False(dialog.ShowsMessage);
        Assert.False(dialog.IsRename);
    }

    [Theory]
    [InlineData("feature", "This conflicts with the branch feature/alpha.")]
    [InlineData("feature/alpha/x", "This conflicts with the branch feature/alpha.")]
    [InlineData("main", "A branch named main exists.")]
    [InlineData("Main", "A branch named main exists in another case.")]
    [InlineData("Feature/beta", "This conflicts with the branch feature/alpha.")]
    [InlineData("bad..name", "This is not a valid branch name: it contains ...")]
    [InlineData("-x", "This is not a valid branch name: it starts with -.")]
    [InlineData("HEAD", "This is not a valid branch name: it cannot be HEAD.")]
    [InlineData("has space", "This is not a valid branch name: it contains a space.")]
    public void A_branch_name_that_cannot_be_created_says_why_and_disables_the_button(string name, string message)
    {
        var dialog = BranchNameDialogViewModel.Create("From main", OpsBranches, ignoreCase: true);

        dialog.Name = name;

        Assert.Equal(message, dialog.NameError);
        Assert.True(dialog.HasNameError);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public void Where_case_is_not_compared_a_name_in_another_case_is_a_new_name()
    {
        var dialog = BranchNameDialogViewModel.Create("From main", OpsBranches, ignoreCase: false);

        dialog.Name = "Main";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.True(dialog.CanConfirm);

        dialog.Name = "Feature/beta";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.True(dialog.CanConfirm);
    }

    [Fact]
    public void Enter_a_name_shows_only_once_the_box_was_edited_and_is_empty()
    {
        var dialog = BranchNameDialogViewModel.Create("From main", OpsBranches, ignoreCase: true);
        Assert.Equal(string.Empty, dialog.NameError);

        dialog.Name = "h";
        Assert.Equal(string.Empty, dialog.NameError);
        dialog.Name = string.Empty;

        Assert.Equal("Enter a name.", dialog.NameError);
        Assert.False(dialog.CanConfirm);
    }

    [Fact]
    public async Task A_valid_new_name_enables_the_button_and_confirming_answers_the_name_and_the_check_box()
    {
        var dialog = BranchNameDialogViewModel.Create("From main", OpsBranches, ignoreCase: true);

        dialog.Name = "hotfix";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        dialog.IsOptionChecked = false;
        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(new NameDialogResult("hotfix", string.Empty, false), await dialog.Result);
        Assert.True(dialog.IsClosed);
    }

    [Fact]
    public async Task Renaming_opens_with_the_old_name_and_enables_the_button_only_for_a_valid_new_name()
    {
        var dialog = BranchNameDialogViewModel.Rename("feature/conflict", OpsBranches, ignoreCase: true);

        Assert.Equal("Rename branch", dialog.Title);
        Assert.Equal("Rename", dialog.ConfirmText);
        Assert.Equal("Rename feature/conflict", dialog.Context);
        Assert.Equal("feature/conflict", dialog.Name);
        Assert.True(dialog.SelectsNameOnOpen);
        Assert.False(dialog.ShowsOption);
        Assert.True(dialog.IsRename);
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.False(dialog.CanConfirm);

        dialog.Name = "feature/alpha";
        Assert.Equal("A branch named feature/alpha exists.", dialog.NameError);
        Assert.False(dialog.CanConfirm);

        dialog.Name = "feature/conflict";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.False(dialog.CanConfirm);

        dialog.Name = "feature/conflicting";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.True(dialog.CanConfirm);
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("feature/conflicting", (await dialog.Result)!.Name);
    }

    [Fact]
    public async Task Cancel_and_Esc_answer_null()
    {
        var cancelled = BranchNameDialogViewModel.Create("From main", OpsBranches);
        cancelled.CancelCommand.Execute(null);
        Assert.Null(await cancelled.Result);

        var escaped = new TagDialogViewModel("At main", []);
        escaped.HandleEscape();
        Assert.Null(await escaped.Result);
    }

    // ---- The name dialog: tags and stashes ----------------------------------------------

    [Fact]
    public async Task The_tag_dialog_checks_the_name_with_tag_in_its_texts_and_answers_the_message()
    {
        var dialog = new TagDialogViewModel("At ce5ea6a \"Alpha two\"", ["v1.0", "v1.1"], ignoreCase: true);

        Assert.Equal("Create tag", dialog.Title);
        Assert.Equal("Create tag", dialog.ConfirmText);
        Assert.Equal("At ce5ea6a \"Alpha two\"", dialog.Context);
        Assert.Equal("Tag name", dialog.NamePlaceholder);
        Assert.True(dialog.ShowsMessage);
        Assert.True(dialog.IsMessageMultiline);
        Assert.Equal("Message (optional: makes an annotated tag)", dialog.MessagePlaceholder);
        Assert.False(dialog.ShowsOption);
        Assert.False(dialog.CanConfirm);

        dialog.Name = "v1.0";
        Assert.Equal("A tag named v1.0 exists.", dialog.NameError);
        dialog.Name = "v1 2";
        Assert.Equal("This is not a valid tag name: it contains a space.", dialog.NameError);
        dialog.Name = "v1.0/x";
        Assert.Equal("This conflicts with the tag v1.0.", dialog.NameError);
        dialog.Name = "v1.2";
        Assert.Equal(string.Empty, dialog.NameError);
        Assert.True(dialog.CanConfirm);

        dialog.Message = "#42 hotfix";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(new NameDialogResult("v1.2", "#42 hotfix", false), await dialog.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_stash_dialog_has_no_name_and_starts_with_the_last_choice_of_untracked_files(bool includeUntracked)
    {
        var dialog = new StashDialogViewModel(includeUntracked);

        Assert.Equal("Stash changes", dialog.Title);
        Assert.Equal("Stash", dialog.ConfirmText);
        Assert.False(dialog.ShowsName);
        Assert.False(dialog.ShowsContext);
        Assert.True(dialog.ShowsMessage);
        Assert.False(dialog.IsMessageMultiline);
        Assert.Equal("Message (optional)", dialog.MessagePlaceholder);
        Assert.Equal("Include untracked files", dialog.OptionText);
        Assert.Equal(includeUntracked, dialog.IsOptionChecked);
        Assert.True(dialog.CanConfirm);

        dialog.Message = "Save my work";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(new NameDialogResult(string.Empty, "Save my work", includeUntracked), await dialog.Result);
    }

    // ---- Reset, pick, choice and push ---------------------------------------------------

    [Fact]
    public async Task The_reset_dialog_names_the_target_chooses_Mixed_and_answers_the_mode()
    {
        var dialog = new ResetDialogViewModel("main", "8beca345049bf7e8bab3a7b4f1427eff941b87e3", "Add core");

        Assert.Equal("Reset main to 8beca34?", dialog.Title);
        Assert.Equal("Move main to \"Add core\" (8beca34).", dialog.Text);
        Assert.Equal("A backup is saved first, so this can be undone.", dialog.Detail);
        Assert.Equal(ResetMode.Mixed, dialog.Mode);
        Assert.True(dialog.IsMixed);
        Assert.False(dialog.IsHard);

        dialog.IsHard = true;
        Assert.Equal(ResetMode.Hard, dialog.Mode);
        Assert.False(dialog.IsMixed);
        dialog.IsMixed = false;
        Assert.Equal(ResetMode.Hard, dialog.Mode);
        dialog.IsSoft = true;
        Assert.Equal(ResetMode.Soft, dialog.Mode);

        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(ResetMode.Soft, await dialog.Result);

        var cancelled = new ResetDialogViewModel("HEAD", "88d6b91", "Main work");
        Assert.Equal("Reset HEAD to 88d6b91?", cancelled.Title);
        cancelled.HandleEscape();
        Assert.Null(await cancelled.Result);
    }

    [Fact]
    public void A_long_subject_is_cut_at_50_characters()
    {
        var subject = new string('x', 60);

        var dialog = new ResetDialogViewModel("main", "1234567", subject);

        Assert.Equal($"Move main to \"{new string('x', 50)}…\" (1234567).", dialog.Text);
        Assert.Equal($"1234567 \"{new string('x', 50)}…\"", DialogText.Commit("1234567890", subject));
    }

    [Fact]
    public async Task The_pick_dialog_filters_ignoring_case_and_needs_a_selection()
    {
        var dialog = new PickDialogViewModel("Set upstream for topic", ["origin/main", "origin/published"], null, "Set upstream");

        Assert.Equal("Set upstream for topic", dialog.Title);
        Assert.Equal(["origin/main", "origin/published"], dialog.Items);
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));

        dialog.Filter = "PUB";
        Assert.Equal(["origin/published"], dialog.Items);
        dialog.Selected = "origin/published";
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.Filter = "main";
        Assert.Equal(["origin/main"], dialog.Items);
        Assert.Null(dialog.Selected);

        dialog.Filter = string.Empty;
        Assert.Equal(["origin/main", "origin/published"], dialog.Items);
        dialog.Selected = "origin/main";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("origin/main", await dialog.Result);

        var changing = new PickDialogViewModel("Set upstream for topic", ["origin/main", "origin/published"], "origin/published", "Set upstream");
        Assert.Equal("origin/published", changing.Selected);
        changing.CancelCommand.Execute(null);
        Assert.Null(await changing.Result);
    }

    [Fact]
    public async Task The_choice_dialog_checks_the_first_option_and_one_at_a_time()
    {
        var dialog = new ChoiceDialogViewModel("Push v1.0 to…", ["mirror", "origin"], "Push");

        Assert.Equal(["mirror", "origin"], dialog.Options.Select(option => option.Text));
        Assert.True(dialog.Options[0].IsChecked);
        Assert.False(dialog.Options[1].IsChecked);
        Assert.Equal("mirror", dialog.Selected);
        Assert.False(dialog.IsDestructive);

        dialog.Options[1].IsChecked = true;
        Assert.False(dialog.Options[0].IsChecked);
        Assert.Equal("origin", dialog.Selected);

        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("origin", await dialog.Result);
        Assert.True(new ChoiceDialogViewModel("Delete v1.0 on…", ["origin"], "Delete", isDestructive: true).IsDestructive);
    }

    [Fact]
    public async Task The_push_dialog_pushes_to_a_branch_of_the_same_name_and_sets_it_as_upstream()
    {
        var dialog = new PushDialogViewModel("topic", ["origin"]);

        Assert.Equal("Push topic", dialog.Title);
        Assert.True(dialog.HasOneRemote);
        Assert.Equal("origin", dialog.Remote);
        Assert.Equal("topic", dialog.BranchName);
        Assert.True(dialog.SetUpstream);
        Assert.Equal(string.Empty, dialog.BranchError);
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.BranchName = string.Empty;
        Assert.Equal("Enter a name.", dialog.BranchError);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.BranchName = "a b";
        Assert.Equal("This is not a valid branch name: it contains a space.", dialog.BranchError);
        dialog.BranchName = "topic-2";
        Assert.Equal(string.Empty, dialog.BranchError);
        dialog.SetUpstream = false;

        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(new PushDialogResult("origin", "topic-2", false), await dialog.Result);

        var several = new PushDialogViewModel("topic", ["mirror", "origin"]);
        Assert.False(several.HasOneRemote);
        Assert.Equal("mirror", several.Remote);
        several.Remotes[1].IsChecked = true;
        Assert.Equal("origin", several.Remote);
    }

    // ---- The error dialog ----------------------------------------------------------------

    [Fact]
    public async Task The_error_dialog_shows_the_failure_and_answers_whether_the_extra_action_was_chosen()
    {
        var failure = new GitFailure(
            GitFailureKind.CheckoutBlockedByLocalChanges,
            "Your changes to these files would be overwritten by checking out feature/other.",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tnotes.txt\nAborting",
            "git switch --no-guess feature/other",
            1);
        var dialog = ErrorDialogViewModel.ForFailure("Checkout blocked", failure, "blocked", "Stash changes and check out");

        Assert.Equal("Checkout blocked", dialog.Title);
        Assert.Equal(failure.Summary, dialog.Summary);
        Assert.Equal("In blocked", dialog.RepositoryText);
        Assert.Equal(failure.GitOutput, dialog.Output);
        Assert.True(dialog.HasOutput);
        Assert.Equal("git switch --no-guess feature/other", dialog.Command);
        Assert.True(dialog.HasCommand);
        Assert.Equal("Stash changes and check out", dialog.ExtraAction);
        Assert.True(dialog.HasExtraAction);

        dialog.ExtraCommand.Execute(null);
        Assert.True(await dialog.Result);

        var closed = ErrorDialogViewModel.ForFailure("Checkout failed", failure, "blocked");
        Assert.False(closed.HasExtraAction);
        closed.ConfirmCommand.Execute(null);
        Assert.False(await closed.Result);

        var escaped = ErrorDialogViewModel.ForFailure("Checkout failed", failure, "blocked");
        escaped.HandleEscape();
        Assert.False(await escaped.Result);
    }

    [Fact]
    public void An_error_dialog_without_git_output_has_no_output_command_or_copy()
    {
        var dialog = new ErrorDialogViewModel(
            "Changes re-applied with conflicts",
            "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.",
            "blocked",
            output: null,
            command: "git stash pop --index stash@{0}");

        Assert.False(dialog.HasOutput);
        Assert.False(dialog.HasCommand);
        Assert.False(dialog.HasExtraAction);
        Assert.Equal("In blocked", dialog.RepositoryText);
    }

    [Fact]
    public async Task Copy_output_puts_exactly_the_output_on_the_clipboard_and_keeps_the_dialog_open()
    {
        var clipboard = new FakeClipboard();
        var output = "To http://127.0.0.1:47391/origin.git\n ! [rejected]        main -> main (fetch first)";
        var dialog = new ErrorDialogViewModel("Push rejected", "origin has commits that your main does not have. Pull first, then push again.", "sync", output, "git push --progress origin refs/heads/main:refs/heads/main", "Pull", clipboard);

        await dialog.CopyOutputCommand.ExecuteAsync(null);

        Assert.Equal([output], clipboard.Texts);
        Assert.False(dialog.IsClosed);
    }

    // ---- The finishing dialog ------------------------------------------------------------

    [Fact]
    public async Task The_finishing_dialog_lists_the_operations_and_answers_true_for_Quit_now()
    {
        var dialog = new FinishingDialogViewModel([new FinishingItem("Fetch origin — auth", false), new FinishingItem("Check out main with your changes — ops", true)]);

        Assert.Equal("Finishing operations", dialog.Title);
        Assert.Equal("VisualCommit closes when these operations end.", dialog.Text);
        Assert.Equal(2, dialog.Items.Count);
        dialog.QuitNowCommand.Execute(null);
        Assert.True(await dialog.Result);

        var kept = new FinishingDialogViewModel([new FinishingItem("Fetch origin — auth", false)]);
        kept.HandleEscape();
        Assert.False(await kept.Result);
    }

    // ---- The Remotes dialog --------------------------------------------------------------

    [Fact]
    public async Task The_Remotes_dialog_lists_the_remotes_with_their_credentials_hidden()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("origin", "http://127.0.0.1:47391/origin.git", null), new RemoteInfo("mirror", "https://alice:ghp_SECRET123@127.0.0.1:1/x.git", null));
        var dialog = new RemotesDialogViewModel(actions, ignoreCase: true);
        await dialog.Loading;

        Assert.Equal("Remotes", dialog.Title);
        Assert.True(dialog.IsListShown);
        Assert.Equal("Close", dialog.CancelText);
        Assert.Equal(["mirror", "origin"], dialog.Rows.Select(row => row.Name));
        Assert.Equal("https://***@127.0.0.1:1/x.git", dialog.Rows[0].ShownUrl);
        Assert.Equal("https://alice:ghp_SECRET123@127.0.0.1:1/x.git", dialog.Rows[0].Url);
        Assert.Equal("http://127.0.0.1:47391/origin.git", dialog.Rows[1].ShownUrl);
    }

    [Fact]
    public async Task Adding_a_remote_checks_the_name_and_the_URL_then_waits_for_the_operation()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("origin", "http://127.0.0.1:47391/origin.git", null));
        var dialog = new RemotesDialogViewModel(actions, ignoreCase: true);
        await dialog.Loading;

        dialog.AddCommand.Execute(null);
        Assert.True(dialog.IsFormShown);
        Assert.True(dialog.IsAdding);
        Assert.Equal("Cancel", dialog.CancelText);
        Assert.True(dialog.FetchNow);
        Assert.Equal(string.Empty, dialog.RemoteError);
        Assert.False(dialog.SaveCommand.CanExecute(null));

        dialog.RemoteName = "x";
        dialog.RemoteName = string.Empty;
        Assert.Equal("Enter a name.", dialog.RemoteError);
        dialog.RemoteName = "bad name";
        Assert.Equal("This is not a valid remote name: it contains a space.", dialog.RemoteError);
        dialog.RemoteName = "origin";
        Assert.Equal("A remote named origin exists.", dialog.RemoteError);
        dialog.RemoteName = "Origin";
        Assert.Equal("A remote named origin exists in another case.", dialog.RemoteError);
        dialog.RemoteName = "mirror";
        Assert.Equal(string.Empty, dialog.RemoteError);
        Assert.False(dialog.SaveCommand.CanExecute(null), "The URL is still empty.");
        dialog.RemoteUrl = "u";
        dialog.RemoteUrl = string.Empty;
        Assert.Equal("Enter a URL.", dialog.RemoteError);
        dialog.RemoteUrl = "http://127.0.0.1:47391/origin.git";
        Assert.Equal(string.Empty, dialog.RemoteError);
        Assert.True(dialog.SaveCommand.CanExecute(null));

        actions.HoldNext();
        var saving = dialog.SaveCommand.ExecuteAsync(null);
        Assert.True(dialog.IsSaving);
        Assert.Equal("Saving…", dialog.SaveText);
        Assert.False(dialog.SaveCommand.CanExecute(null));
        Assert.False(dialog.CancelOrCloseCommand.CanExecute(null));
        actions.Release();
        await saving;

        Assert.Equal(["add mirror http://127.0.0.1:47391/origin.git fetch"], actions.Calls);
        Assert.False(dialog.IsSaving);
        Assert.True(dialog.IsListShown);
        Assert.Equal(["mirror", "origin"], dialog.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task Git_refusing_the_remote_shows_its_message_in_the_form()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("origin", "http://127.0.0.1:47391/origin.git", null))
        {
            NextError = "error: remote mirror already exists.",
        };
        var dialog = new RemotesDialogViewModel(actions, ignoreCase: true);
        await dialog.Loading;
        dialog.AddCommand.Execute(null);
        dialog.RemoteName = "mirror";
        dialog.RemoteUrl = "x";
        dialog.FetchNow = false;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["add mirror x"], actions.Calls);
        Assert.True(dialog.IsFormShown);
        Assert.Equal("error: remote mirror already exists.", dialog.RemoteError);
    }

    [Fact]
    public async Task Editing_shows_the_URL_as_configured_and_saves_only_what_changed()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("mirror", "https://alice:ghp_SECRET123@127.0.0.1:1/x.git", null), new RemoteInfo("origin", "o", null));
        var dialog = new RemotesDialogViewModel(actions, ignoreCase: true);
        await dialog.Loading;

        dialog.EditCommand.Execute(dialog.Rows[0]);
        Assert.True(dialog.IsFormShown);
        Assert.False(dialog.IsAdding);
        Assert.Equal("mirror", dialog.RemoteName);
        Assert.Equal("https://alice:ghp_SECRET123@127.0.0.1:1/x.git", dialog.RemoteUrl);
        Assert.Equal(string.Empty, dialog.RemoteError);
        Assert.False(dialog.SaveCommand.CanExecute(null), "Nothing changed yet.");

        dialog.RemoteName = "backup";
        Assert.True(dialog.SaveCommand.CanExecute(null));
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.Equal(["rename mirror backup"], actions.Calls);

        dialog.EditCommand.Execute(dialog.Rows.Single(row => row.Name == "backup"));
        dialog.RemoteUrl = "https://example.com/y.git";
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.Equal(["rename mirror backup", "set-url backup https://example.com/y.git"], actions.Calls);
        Assert.True(dialog.IsListShown);
    }

    [Fact]
    public async Task Esc_in_the_form_goes_back_to_the_list_and_in_the_list_closes_the_dialog()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("origin", "o", null));
        var dialog = new RemotesDialogViewModel(actions);
        await dialog.Loading;
        dialog.AddCommand.Execute(null);

        dialog.HandleEscape();
        Assert.True(dialog.IsListShown);
        Assert.False(dialog.IsClosed);

        dialog.HandleEscape();
        Assert.True(dialog.IsClosed);
        Assert.Empty(actions.Calls);
    }

    [Fact]
    public async Task Remove_asks_through_the_actions_and_shows_the_list_again()
    {
        var actions = new FakeRemoteActions(new RemoteInfo("backup", "b", null), new RemoteInfo("origin", "o", null));
        var dialog = new RemotesDialogViewModel(actions);
        await dialog.Loading;

        await dialog.RemoveCommand.ExecuteAsync(dialog.Rows[0]);

        Assert.Equal(["remove backup"], actions.Calls);
        Assert.Equal(["origin"], dialog.Rows.Select(row => row.Name));
    }

    // ---- In the window ----------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Every_dialog_of_phase_3_is_found_by_its_type()
    {
        var locator = new DialogViewLocator();
        var actions = new FakeRemoteActions();

        Assert.IsType<NameDialogView>(locator.Build(BranchNameDialogViewModel.Create("From main", [])));
        Assert.IsType<NameDialogView>(locator.Build(new TagDialogViewModel("At main", [])));
        Assert.IsType<NameDialogView>(locator.Build(new StashDialogViewModel(true)));
        Assert.IsType<ResetDialogView>(locator.Build(new ResetDialogViewModel("main", "1234567", "s")));
        Assert.IsType<PickDialogView>(locator.Build(new PickDialogViewModel("t", ["a"], null, "OK")));
        Assert.IsType<ChoiceDialogView>(locator.Build(new ChoiceDialogViewModel("t", ["a"], "OK")));
        Assert.IsType<PushDialogView>(locator.Build(new PushDialogViewModel("b", ["origin"])));
        Assert.IsType<ErrorDialogView>(locator.Build(new ErrorDialogViewModel("t", "s", "r")));
        Assert.IsType<FinishingDialogView>(locator.Build(new FinishingDialogViewModel([])));
        var remotes = new RemotesDialogViewModel(actions);
        Assert.IsType<RemotesDialogView>(locator.Build(remotes));
        await remotes.Loading;
    }

    [AvaloniaFact]
    public async Task The_name_dialog_focuses_its_box_shows_its_message_once_edited_and_Enter_confirms()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialog = BranchNameDialogViewModel.Create("From 88d6b91 \"Main work\"", OpsBranches, ignoreCase: true);
        var answer = app.Session.Services.DialogHost.ShowAsync(dialog);
        app.Settle();

        var box = (TextBox)app.FindByAutomationId("NameBox");
        var error = (TextBlock)app.FindByAutomationId("NameError");
        var confirm = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.True(box.IsFocused);
        Assert.False(error.IsVisible);
        Assert.False(confirm.IsEffectivelyEnabled);
        Assert.Equal("Branch name", box.PlaceholderText);
        Assert.True(((CheckBox)app.FindByAutomationId("NameDialogOption")).IsChecked);
        Assert.Equal("From 88d6b91 \"Main work\"", ((TextBlock)app.FindByAutomationId("NameDialogContext")).Text);

        app.PressKey(Key.Enter);
        Assert.False(answer.IsCompleted, "Enter does nothing while the button is disabled.");

        app.Type("feature");
        Assert.True(error.IsVisible);
        Assert.Equal("This conflicts with the branch feature/alpha.", error.Text);
        Assert.Equal(Brush(app, "VcDangerBrush"), ((ISolidColorBrush)error.Foreground!).Color);

        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type("hotfix");
        Assert.False(error.IsVisible);
        Assert.True(confirm.IsEffectivelyEnabled);

        app.PressKey(Key.Enter);
        Assert.Equal(new NameDialogResult("hotfix", string.Empty, true), await answer);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
    }

    [AvaloniaFact]
    public async Task Renaming_selects_the_old_name_and_a_tags_message_box_takes_Enter_as_a_new_line()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Services.DialogHost;

        var rename = BranchNameDialogViewModel.Rename("feature/conflict", OpsBranches);
        var renaming = dialogs.ShowAsync(rename);
        app.Settle();
        var box = (TextBox)app.FindByAutomationId("NameBox");
        Assert.True(box.IsFocused);
        Assert.Equal("feature/conflict", box.SelectedText);
        app.PressKey(Key.Escape);
        Assert.Null(await renaming);

        var tag = new TagDialogViewModel("At ce5ea6a \"Alpha two\"", ["v1.0"]);
        var tagging = dialogs.ShowAsync(tag);
        app.Settle();
        Assert.True(((TextBox)app.FindByAutomationId("NameBox")).IsFocused);
        app.Type("v1.2");
        app.TypeInto("MessageBox", "#42 hotfix");
        app.PressKey(Key.Enter);
        app.Type("second line");
        Assert.False(tagging.IsCompleted, "Enter in the message makes a new line.");
        Assert.Equal(["#42 hotfix", "second line"], tag.Message.Split(["\r\n", "\n"], StringSplitOptions.None));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.Equal("v1.2", (await tagging)!.Name);
    }

    [AvaloniaFact]
    public async Task The_stash_dialog_focuses_its_message_and_Enter_stashes()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var stashing = app.Session.Services.DialogHost.ShowAsync(new StashDialogViewModel(includeUntracked: false));
        app.Settle();

        var message = (TextBox)app.FindByAutomationId("MessageBox");
        Assert.True(message.IsFocused);
        Assert.Equal("Message (optional)", message.PlaceholderText);
        Assert.False(app.FindByAutomationId("NameBox").IsVisible);
        Assert.False(((CheckBox)app.FindByAutomationId("NameDialogOption")).IsChecked);
        app.Type("Save my work");
        app.PressKey(Key.Enter);

        Assert.Equal(new NameDialogResult(string.Empty, "Save my work", false), await stashing);
    }

    [AvaloniaFact]
    public async Task The_error_dialog_focuses_Close_copies_the_output_and_hides_what_it_does_not_have()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Services.DialogHost;
        var output = "error: you need to resolve your current index first\nnotes.txt: needs merge";
        var failing = dialogs.ShowAsync(new ErrorDialogViewModel("Checkout failed", "Git could not check out main.", "blocked", output, "git switch --no-guess main", null, app.Session.Services.Clipboard));
        app.Settle();

        var close = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.True(close.IsFocused);
        Assert.Equal("In blocked", ((TextBlock)app.FindByAutomationId("ErrorRepository")).Text);
        var outputBox = (TextBox)app.FindByAutomationId("ErrorOutput");
        Assert.True(outputBox.IsVisible);
        Assert.True(outputBox.IsReadOnly);
        Assert.Equal(output, outputBox.Text);
        Assert.Equal("git switch --no-guess main", ((TextBlock)app.FindByAutomationId("ErrorCommand")).Text);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsVisible);

        app.Click(app.FindByAutomationId("ErrorCopyButton"));
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the copy");
        Assert.Equal(output, app.Clipboard.Text);
        Assert.False(failing.IsCompleted);
        app.Click(close);
        Assert.False(await failing);

        var informing = dialogs.ShowAsync(new ErrorDialogViewModel("Changes re-applied with conflicts", "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.", "blocked"));
        app.Settle();
        Assert.False(app.FindByAutomationId("ErrorOutput").IsVisible);
        Assert.False(app.FindByAutomationId("ErrorCommand").IsVisible);
        Assert.False(app.FindByAutomationId("ErrorCopyButton").IsVisible);
        Assert.False(app.FindByAutomationId("ErrorExtraButton").IsVisible);
        Assert.True(app.FindByAutomationId("ConfirmButton").IsVisible);
        app.PressKey(Key.Escape);
        Assert.False(await informing);
    }

    [AvaloniaFact]
    public async Task The_Remotes_dialog_asks_Remove_on_top_and_Esc_closes_only_the_question()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Services.DialogHost;
        var actions = new FakeRemoteActions(new RemoteInfo("backup", "https://alice:ghp_SECRET123@127.0.0.1:1/x.git", null), new RemoteInfo("origin", "http://127.0.0.1:47391/origin.git", null));
        bool? answered = null;
        actions.Removing = async name =>
        {
            answered = await dialogs.ConfirmAsync(new ConfirmationRequest(
                $"Remove remote {name}?",
                $"Remove the remote {name} and its 2 remote branches?",
                DialogText.BackupDetail,
                "Remove"));
            return answered.Value;
        };
        var remotes = new RemotesDialogViewModel(actions);
        var closing = dialogs.ShowAsync(remotes);
        await remotes.Loading;
        app.Settle();

        var view = app.Window.GetVisualDescendants().OfType<RemotesDialogView>().Single();
        Assert.True(view.FindControl<Button>("CancelButton")!.IsFocused, "Close has the focus.");
        var rows = view.GetVisualDescendants().OfType<DialogRow>().ToList();
        Assert.Equal(["backup", "origin"], rows.Select(AutomationProperties.GetName));
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("ghp_SECRET123", StringComparison.Ordinal) == true);
        var remove = rows[0].GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "RemoteRemoveButton");

        app.Click(remove);
        Assert.IsType<ConfirmationViewModel>(dialogs.Current);
        Assert.Equal(2, dialogs.Open.Count);
        Assert.True(view.IsEffectivelyVisible, "The Remotes dialog stays drawn under the question.");
        var question = app.Window.GetVisualDescendants().OfType<ConfirmationView>().Single();
        Assert.True(question.FindControl<Button>("CancelButton")!.IsFocused);

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => answered is not null, "the answer");
        Assert.False(answered);
        Assert.Same(remotes, dialogs.Current);
        Assert.True(remove.IsFocused, "The dialog below gets the focus back.");
        Assert.Equal(["remove backup"], actions.Calls);

        app.Click(view.FindControl<Button>("AddRemoteButton")!);
        Assert.True(((TextBox)app.FindByAutomationId("RemoteNameBox")).IsFocused, "The form's name box takes the focus.");
        app.PressKey(Key.Escape);
        Assert.True(remotes.IsListShown);
        Assert.False(remotes.IsClosed);
        app.PressKey(Key.Escape);
        await closing;
        Assert.False(dialogs.IsOpen);
    }

    [AvaloniaFact]
    public async Task Each_dialog_root_is_a_UI_Automation_control_element()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Services.DialogHost;
        var remotes = new RemotesDialogViewModel(new FakeRemoteActions(new RemoteInfo("origin", "o", null)));
        var opened = new List<(DialogViewModel Dialog, string Id)>
        {
            (remotes, "RemotesDialog"),
            (BranchNameDialogViewModel.Create("From main", []), "NameDialog"),
            (new ResetDialogViewModel("main", "1234567", "s"), "ResetDialog"),
            (new PickDialogViewModel("t", ["a"], null, "OK"), "PickDialog"),
            (new ChoiceDialogViewModel("t", ["a"], "OK"), "ChoiceDialog"),
            (new PushDialogViewModel("b", ["origin"]), "PushDialog"),
            (new FinishingDialogViewModel([new FinishingItem("Fetch origin — auth", false)]), "FinishingDialog"),
            (new ErrorDialogViewModel("t", "s", "r", "out", "git x"), "ErrorDialog"),
        };
        foreach (var (dialog, _) in opened)
        {
            _ = dialogs.ShowAsync(dialog);
        }

        var asking = dialogs.ConfirmAsync(new ConfirmationRequest("Q?", "Q", string.Empty, "OK"));
        await remotes.Loading;
        app.Settle();

        var elements = ControlElements(ControlAutomationPeer.CreatePeerForElement(app.Window));
        foreach (var id in opened.Select(entry => entry.Id).Append("ConfirmationDialog"))
        {
            var peer = Assert.Single(elements, element => element.GetAutomationId() == id);
            Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
        }

        var row = Assert.Single(elements, element => element.GetAutomationId() == "RemoteRow");
        Assert.Equal("origin", row.GetName());
        Assert.Contains(ControlElements(row), element => element.GetAutomationId() == "RemoteEditButton");
        Assert.Contains(ControlElements(row), element => element.GetAutomationId() == "RemoteRemoveButton");
        Assert.Contains(elements, element => element.GetAutomationId() == "FinishingItem" && element.GetName() == "Fetch origin — auth");

        while (dialogs.CancelCurrent())
        {
        }

        Assert.False(await asking);
    }

    [AvaloniaFact]
    public async Task Pictures_of_the_dialogs_in_both_themes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        var dialogs = app.Session.Services.DialogHost;
        var actions = new FakeRemoteActions(new RemoteInfo("mirror", "https://alice:ghp_SECRET123@127.0.0.1:1/x.git", null), new RemoteInfo("origin", "http://127.0.0.1:47391/origin.git", null));

        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            var suffix = theme == ThemeVariant.Dark ? "dark" : "light";

            var name = BranchNameDialogViewModel.Create("From 88d6b91 \"Main work\"", OpsBranches, ignoreCase: true);
            name.Name = "feature";
            Show(name, $"name-branch-{suffix}", 400);
            Show(BranchNameDialogViewModel.Rename("feature/conflict", OpsBranches), $"name-rename-{suffix}", 400);
            var tag = new TagDialogViewModel("At ce5ea6a \"Alpha two\"", ["v1.0", "v1.1"]) { Name = "v1.2", Message = "#42 hotfix" };
            Show(tag, $"name-tag-{suffix}", 400);
            Show(new StashDialogViewModel(true), $"name-stash-{suffix}", 400);
            Show(new ResetDialogViewModel("main", "8beca34", "Add core") { IsHard = true }, $"reset-hard-{suffix}", 400);
            Show(new ResetDialogViewModel("main", "8beca34", "Add core"), $"reset-mixed-{suffix}", 400);
            Show(new PickDialogViewModel("Set upstream for topic", ["origin/main", "origin/published"], "origin/published", "Set upstream"), $"pick-{suffix}", 460);
            Show(new ChoiceDialogViewModel("Push v1.0 to…", ["mirror", "origin"], "Push"), $"choice-{suffix}", 400);
            Show(new PushDialogViewModel("topic", ["origin"]), $"push-{suffix}", 400);
            Show(new PushDialogViewModel("topic", ["mirror", "origin"]), $"push-two-{suffix}", 400);
            Show(new FinishingDialogViewModel([new FinishingItem("Fetch origin — auth", false), new FinishingItem("Check out main with your changes — ops", true)]), $"finishing-{suffix}", 400);
            Show(
                new ErrorDialogViewModel(
                    "Push rejected",
                    "origin has commits that your main does not have. Pull first, then push again.",
                    "ops",
                    "To http://127.0.0.1:47391/origin.git\n ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs to 'http://127.0.0.1:47391/origin.git'\nhint: Updates were rejected because the remote contains work that you do\nhint: not have locally. This is usually caused by another repository pushing\nhint: to the same ref. You may want to first integrate the remote changes\nhint: (e.g., 'git pull ...') before pushing again.\nhint: See the 'Note about fast-forwards' in 'git push --help' for details.",
                    "git push --progress origin refs/heads/main:refs/heads/main",
                    "Pull"),
                $"error-push-rejected-{suffix}",
                400);
            Show(
                new ErrorDialogViewModel(
                    "Checkout blocked",
                    "Your changes to these files would be overwritten by checking out feature/other.",
                    "blocked",
                    "error: Your local changes to the following files would be overwritten by checkout:\n\tnotes.txt\nPlease commit your changes or stash them before you switch branches.\nAborting",
                    "git switch --no-guess feature/other",
                    "Stash changes and check out"),
                $"error-checkout-blocked-{suffix}",
                400);
            Show(new ErrorDialogViewModel("Changes re-applied with conflicts", "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file. Your changes are kept in the stash \"VisualCommit: before checking out feature/clash\".", "blocked"), $"error-no-output-{suffix}", 400);
            var deleting = dialogs.ConfirmAsync(new ConfirmationRequest("Delete branch?", "Delete the branch feature/ff? It has commits that hotfix does not have.", DialogText.BackupDetail, "Delete"));
            Capture($"confirmation-{suffix}", 400);
            dialogs.CancelCurrent();
            Assert.False(await deleting);
            var trusting = dialogs.ConfirmAsync(new ConfirmationRequest("ssh asks", "Confirm user presence for key ED25519 SHA256:abc?", string.Empty, "Yes", IsDestructive: false));
            Capture($"confirmation-yes-{suffix}", 400);
            dialogs.CancelCurrent();
            Assert.False(await trusting);

            var remotes = new RemotesDialogViewModel(actions);
            var closing = dialogs.ShowAsync(remotes);
            await remotes.Loading;
            Capture($"remotes-{suffix}", 460);
            remotes.AddCommand.Execute(null);
            remotes.RemoteName = "origin";
            Capture($"remotes-form-{suffix}", 460);
            remotes.HandleEscape();
            var asking = dialogs.ConfirmAsync(new ConfirmationRequest("Remove remote mirror?", "Remove the remote mirror and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was https://***@127.0.0.1:1/x.git.", DialogText.BackupDetail, "Remove"));
            Capture($"remotes-stacked-{suffix}", 400);
            dialogs.CancelCurrent();
            await asking;
            dialogs.CancelCurrent();
            await closing;
        }

        void Show(DialogViewModel dialog, string picture, double width)
        {
            var closed = dialogs.ShowAsync(dialog);
            Capture(picture, width);
            dialog.HandleEscape();
            Assert.True(closed.IsCompleted);
        }

        void Capture(string picture, double width)
        {
            app.Settle();
            var card = app.Window.GetVisualDescendants().OfType<Border>()
                .Last(border => border.Classes.Contains("dialogCard") && border.IsEffectivelyVisible);
            Assert.Equal(width, card.Bounds.Width);
            var clipped = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
            Assert.True(clipped.Count == 0, string.Join(Environment.NewLine, clipped));

            // The card's buttons lie inside it, apart from each other.
            var cardArea = app.BoundsOf(card);
            var buttons = card.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button is not (RadioButton or RepeatButton) && !button.GetVisualAncestors().OfType<ListBox>().Any())
                .Select(button => (Name: AutomationProperties.GetName(button), Area: app.BoundsOf(button)))
                .ToList();
            foreach (var (buttonName, area) in buttons)
            {
                Assert.True(cardArea.Contains(area), $"{buttonName} lies inside the card.");
                var touching = buttons.Where(other => other.Name != buttonName && other.Area.Inflate(-0.5).Intersects(area.Inflate(2))).ToList();
                Assert.True(touching.Count == 0, $"{picture}: {buttonName} {area} touches {string.Join(", ", touching)}.");
            }
            TestPictures.Save(app, "dialog-" + picture);
        }
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static List<AutomationPeer> ControlElements(AutomationPeer root)
    {
        var found = new List<AutomationPeer>();
        Collect(root);
        return found;

        void Collect(AutomationPeer peer)
        {
            if (peer.IsControlElement())
            {
                found.Add(peer);
            }

            foreach (var child in peer.GetChildren())
            {
                Collect(child);
            }
        }
    }

    /// <summary>The Remotes dialog's actions, recorded; the list changes as git would change it.</summary>
    internal sealed class FakeRemoteActions(params RemoteInfo[] remotes) : IRemoteActions
    {
        private readonly List<RemoteInfo> _remotes = [.. remotes];
        private TaskCompletionSource? _hold;
        private TaskCompletionSource? _held;

        public List<string> Calls { get; } = [];

        /// <summary>What the next add, rename or set-url answers instead of succeeding.</summary>
        public string? NextError { get; set; }

        /// <summary>What Remove does before it removes: the confirmation; false keeps the remote.</summary>
        public Func<string, Task<bool>>? Removing { get; set; }

        /// <summary>The next add, rename or set-url waits until <see cref="Release"/>.</summary>
        public void HoldNext() => _hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => (_held ?? _hold)?.TrySetResult();

        public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync() =>
            Task.FromResult<IReadOnlyList<RemoteInfo>>([.. _remotes.OrderBy(remote => remote.Name, StringComparer.Ordinal)]);

        public async Task<string?> AddRemoteAsync(string name, string url, bool fetchNow)
        {
            Calls.Add($"add {name} {url}{(fetchNow ? " fetch" : string.Empty)}");
            return await Finish(() => _remotes.Add(new RemoteInfo(name, url, null)));
        }

        public async Task<string?> RenameRemoteAsync(string name, string newName)
        {
            Calls.Add($"rename {name} {newName}");
            return await Finish(() => Replace(name, remote => remote with { Name = newName }));
        }

        public async Task<string?> SetRemoteUrlAsync(string name, string url)
        {
            Calls.Add($"set-url {name} {url}");
            return await Finish(() => Replace(name, remote => remote with { FetchUrl = url }));
        }

        public async Task RemoveRemoteAsync(string name)
        {
            Calls.Add($"remove {name}");
            if (Removing is null || await Removing(name))
            {
                _remotes.RemoveAll(remote => remote.Name == name);
            }
        }

        private void Replace(string name, Func<RemoteInfo, RemoteInfo> change)
        {
            var index = _remotes.FindIndex(remote => remote.Name == name);
            _remotes[index] = change(_remotes[index]);
        }

        private async Task<string?> Finish(Action change)
        {
            if (_hold is { } hold)
            {
                (_hold, _held) = (null, hold);
                await hold.Task;
            }

            if (NextError is { } error)
            {
                NextError = null;
                return error;
            }

            change();
            return null;
        }
    }
}
