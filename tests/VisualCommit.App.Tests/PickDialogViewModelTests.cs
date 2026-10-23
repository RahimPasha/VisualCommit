using VisualCommit.App.ViewModels.Dialogs;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The pick dialog's items with a detail (docs/phase-6-design.md 7.7, 7.11): the items, the
/// selection and the answer stay the texts; the filter matches a text or a detail; phase 3's
/// constructor of strings is unchanged.
/// </summary>
public class PickDialogViewModelTests
{
    private static readonly PickOption[] Editors =
    [
        new("None", "no Open in editor in the file menus"),
        new("Visual Studio Code", @"C:\Tools\Microsoft VS Code\Code.exe"),
        new("Sublime Text", @"C:\Tools\Sublime Text\subl.exe"),
        new("Other…", "a program of your choice"),
    ];

    [Fact]
    public async Task Options_with_details_list_their_texts_and_answer_the_text()
    {
        var dialog = new PickDialogViewModel("Choose an editor", Editors, "None", "Choose");

        Assert.Equal(("Choose an editor", "Choose"), (dialog.Title, dialog.ConfirmText));
        Assert.Equal(["None", "Visual Studio Code", "Sublime Text", "Other…"], dialog.Items);
        Assert.Equal(Editors, dialog.Options);
        Assert.Equal("None", dialog.Selected);
        Assert.Equal(@"C:\Tools\Sublime Text\subl.exe", dialog.DetailOf("Sublime Text"));
        Assert.Null(dialog.DetailOf("Notepad"));
        Assert.Null(dialog.DetailOf(null));

        dialog.Selected = "Visual Studio Code";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("Visual Studio Code", await dialog.Result);
    }

    [Fact]
    public void The_filter_matches_a_detail_as_well_as_a_text_ignoring_case()
    {
        var dialog = new PickDialogViewModel("Choose an editor", Editors, "Other…", "Choose");

        dialog.Filter = "SUBL";
        Assert.Equal(["Sublime Text"], dialog.Items);
        Assert.Null(dialog.Selected);

        dialog.Filter = @"c:\tools";
        Assert.Equal(["Visual Studio Code", "Sublime Text"], dialog.Items);

        dialog.Filter = "program";
        Assert.Equal(["Other…"], dialog.Items);

        dialog.Filter = string.Empty;
        Assert.Equal(["None", "Visual Studio Code", "Sublime Text", "Other…"], dialog.Items);
    }

    [Fact]
    public async Task Phase_3s_strings_are_options_without_a_detail_and_behave_as_before()
    {
        var dialog = new PickDialogViewModel("Set upstream for topic", ["origin/main", "origin/published"], "origin/published", "Set upstream");

        Assert.Equal(["origin/main", "origin/published"], dialog.Items);
        Assert.Equal([new PickOption("origin/main"), new PickOption("origin/published")], dialog.Options);
        Assert.Null(dialog.DetailOf("origin/main"));
        Assert.Equal("origin/published", dialog.Selected);

        dialog.Filter = "MAIN";
        Assert.Equal(["origin/main"], dialog.Items);
        Assert.Null(dialog.Selected);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));

        dialog.CancelCommand.Execute(null);
        Assert.Null(await dialog.Result);
    }

    [Fact]
    public void A_selected_text_that_is_not_an_option_selects_nothing()
    {
        Assert.Null(new PickDialogViewModel("Choose a merge tool", [new PickOption("Not set", "Merge tool… stays disabled")], "slow", "Choose").Selected);
    }
}
