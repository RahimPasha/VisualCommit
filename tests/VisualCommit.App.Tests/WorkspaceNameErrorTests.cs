using VisualCommit.App.ViewModels.Dialogs;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The workspace's name dialog and what its box says (docs/phase-6-design.md 1.6, 14.6): the
/// trimmed name checked in order, empty, too long, a control character, another workspace's name
/// ignoring case; shown once the box was edited (D114); Create disabled until the name is valid,
/// Rename until it differs.
/// </summary>
public class WorkspaceNameErrorTests
{
    [Fact]
    public void An_empty_or_blank_name_asks_for_one()
    {
        Assert.Equal("Enter a name.", DialogText.WorkspaceNameError(string.Empty, []));
        Assert.Equal("Enter a name.", DialogText.WorkspaceNameError("   ", []));
        Assert.Equal(DialogText.EnterName, DialogText.WorkspaceNameError("\t", []));
    }

    [Fact]
    public void A_name_of_more_than_100_characters_is_refused_after_trimming()
    {
        Assert.Null(DialogText.WorkspaceNameError(new string('a', 100), []));
        Assert.Null(DialogText.WorkspaceNameError("  " + new string('a', 100) + "  ", []));
        Assert.Equal(
            "This is not a valid workspace name: it has more than 100 characters.",
            DialogText.WorkspaceNameError(new string('a', 101), []));

        // Characters as a reader counts them: an emoji is one.
        Assert.Null(DialogText.WorkspaceNameError(string.Concat(Enumerable.Repeat("😀", 100)), []));
        Assert.Equal(DialogText.WorkspaceNameTooLong, DialogText.WorkspaceNameError(string.Concat(Enumerable.Repeat("😀", 101)), []));
    }

    [Theory]
    [InlineData("Web\nteam")]
    [InlineData("Web\tteam")]
    [InlineData("Web\u0007")]
    [InlineData("Web\r\nteam")]
    public void A_line_break_or_another_control_character_is_refused(string name)
    {
        Assert.Equal(
            "This is not a valid workspace name: it has a line break or another control character.",
            DialogText.WorkspaceNameError(name, []));
    }

    [Fact]
    public void Another_workspaces_name_is_refused_ignoring_case_in_its_own_spelling()
    {
        Assert.Equal("A workspace named Web exists.", DialogText.WorkspaceNameError("web", ["Tools", "Web"]));
        Assert.Equal("A workspace named Web exists.", DialogText.WorkspaceNameError(" WEB ", ["Web"]));
        Assert.Null(DialogText.WorkspaceNameError("Web team", ["Web"]));
    }

    [Fact]
    public void The_checks_go_in_their_order()
    {
        // Too long and with a line break: the length is said first; a control character before a clash.
        Assert.Equal(DialogText.WorkspaceNameTooLong, DialogText.WorkspaceNameError(new string('a', 100) + "\nb", []));
        Assert.Equal(DialogText.WorkspaceNameHasControlCharacter, DialogText.WorkspaceNameError("Web\u0001", ["Web\u0001"]));
    }

    [Fact]
    public async Task New_workspace_has_an_empty_box_no_context_and_create_disabled_until_the_name_is_valid()
    {
        var dialog = WorkspaceNameDialogViewModel.Create(["Web"]);

        Assert.Equal("New workspace", dialog.Title);
        Assert.Equal("Create", dialog.ConfirmText);
        Assert.Equal("Workspace name", dialog.NamePlaceholder);
        Assert.True(dialog.ShowsName);
        Assert.False(dialog.ShowsContext);
        Assert.False(dialog.ShowsRepository);
        Assert.False(dialog.SelectsNameOnOpen);
        Assert.Equal(string.Empty, dialog.Name);
        Assert.False(dialog.CanConfirm);
        Assert.False(dialog.HasNameError);

        dialog.Name = "web";
        Assert.False(dialog.CanConfirm);
        Assert.Equal("A workspace named Web exists.", dialog.NameError);

        dialog.Name = "Tools";
        Assert.True(dialog.CanConfirm);
        Assert.False(dialog.HasNameError);

        dialog.Name = string.Empty;
        Assert.Equal("Enter a name.", dialog.NameError);

        dialog.Name = "Tools";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("Tools", (await dialog.Result)!.Name);
    }

    [Fact]
    public void Rename_workspace_selects_the_name_and_waits_until_it_differs_a_change_of_case_counting()
    {
        var dialog = WorkspaceNameDialogViewModel.Rename("Web", ["Tools"]);

        Assert.Equal("Rename workspace", dialog.Title);
        Assert.Equal("Rename", dialog.ConfirmText);
        Assert.Equal("Rename Web", dialog.Context);
        Assert.True(dialog.ShowsContext);
        Assert.True(dialog.SelectsNameOnOpen);
        Assert.True(dialog.IsRename);
        Assert.Equal("Web", dialog.Name);
        Assert.False(dialog.CanConfirm);

        // The workspace itself is no clash; a change of case alone may be saved.
        dialog.Name = "web";
        Assert.True(dialog.CanConfirm);
        Assert.False(dialog.HasNameError);

        // Spaces around the old name change nothing.
        dialog.Name = " Web ";
        Assert.False(dialog.CanConfirm);

        dialog.Name = "tools";
        Assert.Equal("A workspace named Tools exists.", dialog.NameError);
        Assert.False(dialog.CanConfirm);

        dialog.Name = "Web team";
        Assert.True(dialog.CanConfirm);
    }

    [Fact]
    public async Task Cancel_answers_nothing()
    {
        var dialog = WorkspaceNameDialogViewModel.Create([]);
        dialog.Name = "Web";

        dialog.CancelCommand.Execute(null);

        Assert.Null(await dialog.Result);
    }
}
