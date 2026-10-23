using VisualCommit.App.ViewModels.Commands;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The palette's view model (docs/phase-6-design.md 5.2, 5.6, 5.7, 5.8, 5.12): the highlight, the queries, a disabled entry, the items replaced, Esc. Pure.</summary>
public class CommandPaletteViewModelTests
{
    [Fact]
    public void It_opens_on_the_commands_with_the_first_highlighted_and_its_title_names_it()
    {
        var palette = Palette(commands: 15);
        Assert.Equal("Command palette", palette.Title);
        Assert.Equal(15, palette.Shown.Count);
        Assert.Equal((0, "Command 0"), (palette.HighlightedIndex, palette.Highlighted?.Label));
        Assert.False(palette.ShowsNothing);
        Assert.True(palette.ShowsList);
        Assert.False(palette.Result.IsCompleted);
    }

    [Fact]
    public void Up_and_Down_wrap_and_PageUp_and_PageDown_move_by_eleven_to_the_ends()
    {
        var palette = Palette(commands: 33);
        palette.MoveHighlight(-1, wrap: true);
        Assert.Equal(32, palette.HighlightedIndex);
        palette.MoveHighlight(1, wrap: true);
        Assert.Equal(0, palette.HighlightedIndex);
        palette.MoveHighlight(1, wrap: true);
        Assert.Equal(1, palette.HighlightedIndex);

        palette.MoveHighlight(-1, wrap: true);
        Assert.Equal(11, CommandPaletteViewModel.PageRows);
        foreach (var expected in new[] { 11, 22, 32, 32 })
        {
            palette.MoveHighlight(CommandPaletteViewModel.PageRows, wrap: false);
            Assert.Equal(expected, palette.HighlightedIndex);
        }

        foreach (var expected in new[] { 21, 10, 0, 0 })
        {
            palette.MoveHighlight(-CommandPaletteViewModel.PageRows, wrap: false);
            Assert.Equal(expected, palette.HighlightedIndex);
        }
    }

    [Fact]
    public void Each_query_ranks_again_and_highlights_the_first_and_nothing_matching_shows_No_matches()
    {
        var palette = Palette(commands: 5, items: 5);
        palette.MoveHighlight(3, wrap: true);
        palette.Query = "item";
        Assert.Equal(5, palette.Shown.Count);
        Assert.Equal((0, "Item 0"), (palette.HighlightedIndex, palette.Highlighted?.Label));

        palette.Query = "zzz";
        Assert.True(palette.ShowsNothing);
        Assert.False(palette.ShowsList);
        Assert.Empty(palette.Shown);
        Assert.Null(palette.Highlighted);
        Assert.Equal(-1, palette.HighlightedIndex);
        Assert.Equal("No matches", CommandPaletteViewModel.NoMatchesText);

        // Nothing to move or run.
        palette.MoveHighlight(1, wrap: true);
        palette.RunHighlighted();
        Assert.False(palette.Result.IsCompleted);

        palette.Query = string.Empty;
        Assert.Equal((5, 0), (palette.Shown.Count, palette.HighlightedIndex));
    }

    [Fact]
    public void The_shown_list_is_refilled_with_one_reset_per_query()
    {
        var palette = Palette(commands: 5, items: 200);
        var events = 0;
        palette.Shown.CollectionChanged += (_, _) => events++;
        palette.Query = "i";
        Assert.Equal(1, events);
        palette.Query = "it";
        Assert.Equal(2, events);
    }

    [Fact]
    public async Task Enter_on_an_enabled_entry_answers_it_and_on_a_disabled_one_leaves_the_palette_open()
    {
        var disabled = Entry("Stash changes…", enabled: false);
        var enabled = Entry("Fetch all remotes");
        var palette = new CommandPaletteViewModel([disabled, enabled], [], []);

        palette.RunHighlighted();
        Assert.False(palette.Result.IsCompleted);
        Assert.False(palette.IsClosed);

        palette.Run(disabled);
        Assert.False(palette.IsClosed);

        palette.MoveHighlight(1, wrap: true);
        palette.RunHighlighted();
        Assert.True(palette.IsClosed);
        Assert.Same(enabled, await palette.Result);
    }

    [Fact]
    public async Task Esc_answers_null()
    {
        var palette = Palette(commands: 3);
        palette.HandleEscape();
        Assert.True(palette.IsClosed);
        Assert.Null(await palette.Result);
    }

    [Fact]
    public void Replacing_the_items_keeps_the_query_and_the_highlighted_key()
    {
        var palette = new CommandPaletteViewModel([], [Entry("Check out main", "main"), Entry("Check out feature/alpha", "feature/alpha"), Entry("Fast-forward feature/ff to origin/feature/ff", "feature/ff")], [], "check");
        Assert.Equal("check", palette.Query);
        palette.MoveHighlight(1, wrap: true);
        Assert.Equal("Check out feature/alpha", palette.Highlighted?.Label);

        palette.ReplaceItems([Entry("Check out main", "main"), Entry("Check out hotfix", "hotfix"), Entry("Check out feature/alpha", "feature/alpha")]);
        Assert.Equal("check", palette.Query);
        Assert.Equal(["Check out main", "Check out hotfix", "Check out feature/alpha"], palette.Shown.Select(entry => entry.Label));
        Assert.Equal((2, "Check out feature/alpha"), (palette.HighlightedIndex, palette.Highlighted?.Label));

        // A highlighted key the new items lack: the first.
        palette.ReplaceItems([Entry("Check out main", "main"), Entry("Check out hotfix", "hotfix")]);
        Assert.Equal((0, "Check out main"), (palette.HighlightedIndex, palette.Highlighted?.Label));
    }

    [Fact]
    public void A_starting_query_filters_at_once_and_a_second_palette_key_asks_the_view_to_select_it()
    {
        var palette = new CommandPaletteViewModel([Entry("New workspace…"), Entry("New tab")], [Entry("Open workspace Web", "10 repositories")], [], "workspace");
        Assert.Equal(["New workspace…", "Open workspace Web"], palette.Shown.Select(entry => entry.Label));
        var asked = 0;
        palette.SelectQueryRequested += (_, _) => asked++;
        palette.RequestSelectQuery();
        Assert.Equal(1, asked);
    }

    [Fact]
    public void The_list_sets_the_highlight_and_a_place_outside_it_changes_nothing()
    {
        var palette = Palette(commands: 4);
        palette.HighlightedIndex = 2;
        Assert.Equal("Command 2", palette.Highlighted?.Label);
        palette.HighlightedIndex = -1;
        palette.HighlightedIndex = 9;
        Assert.Equal(2, palette.HighlightedIndex);
    }

    [Fact]
    public void An_entry_shows_a_disabled_reason_in_its_details_place_and_its_key_by_label_and_detail()
    {
        var disabled = new PaletteEntry("Delete main…", "main", null, false, "You cannot delete the branch you are on", true, PaletteEntryKind.Item, 0, () => { });
        Assert.Equal(("You cannot delete the branch you are on", "You cannot delete the branch you are on", false), (disabled.ShownDetail, disabled.ToolTip, disabled.ShowsDanger));
        Assert.Equal("Delete main…\u001Fmain", disabled.Key);

        // A disabled entry without a reason (refs not read) shows its detail, dimmed (5.7).
        var unread = disabled with { DisabledReason = null };
        Assert.Equal(("main", null), (unread.ShownDetail, unread.ToolTip));

        var danger = disabled with { IsEnabled = true, DisabledReason = null, Detail = null, KeyText = "Ctrl+B" };
        Assert.Equal((null, true, true, "Delete main…"), (danger.ShownDetail, danger.ShowsDanger, danger.HasKey, danger.Key));
    }

    private static CommandPaletteViewModel Palette(int commands, int items = 0) =>
        new(
            [.. Enumerable.Range(0, commands).Select(i => Entry($"Command {i}", order: i))],
            [.. Enumerable.Range(0, items).Select(i => Entry($"Item {i}", $"item {i}", kind: PaletteEntryKind.Item, order: i))],
            []);

    private static PaletteEntry Entry(string label, string? detail = null, bool enabled = true, PaletteEntryKind kind = PaletteEntryKind.Command, int order = 0) =>
        new(label, detail, null, enabled, enabled ? null : "Not now", false, kind, order, () => { });
}
