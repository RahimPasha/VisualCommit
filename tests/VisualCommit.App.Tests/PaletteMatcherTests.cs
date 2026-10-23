using VisualCommit.App.ViewModels.Commands;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The palette's matching and ranking (docs/phase-6-design.md 5.6, 5.12, D212), rule by rule. Pure; its time per query is <see cref="PaletteGrowthTests"/>'.</summary>
public class PaletteMatcherTests
{
    [Theory]
    [InlineData("check out feature/alpha", "Check out feature/alpha", null, 0)]
    [InlineData("CHECK", "Check out feature/alpha", null, 0)]
    [InlineData("feature/alpha", "Check out origin/feature/alpha", null, 1)]
    [InlineData("out alpha", "Check out feature/alpha", null, 1)]
    [InlineData("hotfix", "Create branch here/hotfix", null, 1)]
    [InlineData("fix", "Merge pr-fix", null, 1)]
    [InlineData("fix", "Merge snake_fix", null, 1)]
    [InlineData("fix", "Open v1.fix", null, 1)]
    [InlineData("fix", "Pull from (fix)", null, 1)]
    [InlineData("fix", "Undo commit \"fix the parser\"", null, 1)]
    [InlineData("eck", "Check out main", null, 2)]
    [InlineData("out ain", "Check out main", null, 2)]
    [InlineData("main", "Delete main on origin…", "origin/main", 1)]
    [InlineData("origin/main", "Delete main on origin…", "origin/main", 3)]
    [InlineData("delete 88d6b91", "Delete branch…", "88d6b91 Main work", 3)]
    public void An_entry_is_ranked_in_its_tier(string query, string label, string? detail, int tier)
    {
        var entry = Entry(label, detail);
        Assert.Equal(tier, PaletteMatcher.TierOf(query.Trim(), PaletteMatcher.Words(query), entry));
    }

    [Theory]
    [InlineData("check zzz", "Check out main", null)]
    [InlineData("zzz", "Check out main", "main")]
    [InlineData("main origin", "Delete main…", "main")]
    public void Every_word_must_occur_in_the_label_or_the_detail(string query, string label, string? detail) =>
        Assert.Null(PaletteMatcher.TierOf(query.Trim(), PaletteMatcher.Words(query), Entry(label, detail)));

    [Fact]
    public void Matches_rank_by_tier_then_enabled_then_the_sessions_runs_then_source_order()
    {
        var commands = new[]
        {
            Entry("Delete main…", "main", enabled: false, order: 0),
            Entry("Delete main on origin…", "origin/main", order: 1),
        };
        var items = new[]
        {
            Entry("Open main", null, kind: PaletteEntryKind.Item, order: 0),
            Entry("Rename main…", "main", kind: PaletteEntryKind.Item, order: 1),
            Entry("Domain main tools", null, kind: PaletteEntryKind.Item, order: 2),
        };

        // "delete main": both tier 0; the enabled first (5.6), whatever the source order.
        Assert.Equal(["Delete main on origin…", "Delete main…"], Labels(PaletteMatcher.Rank("delete main", commands, items, [])));

        // "main": tier 1 for every label that has the word; a disabled one is not lifted over a lower
        // tier, nor sunk below one; within tier 1 the enabled first, then the source order
        // (commands before items).
        Assert.Equal(
            ["Delete main on origin…", "Open main", "Rename main…", "Domain main tools", "Delete main…"],
            Labels(PaletteMatcher.Rank("main", commands, items, [])));

        // The session's runs, newest first, rank above the others within a tier and its enabled.
        Assert.Equal(
            ["Rename main…", "Open main", "Delete main on origin…", "Domain main tools", "Delete main…"],
            Labels(PaletteMatcher.Rank("main", commands, items, [items[1].Key, items[0].Key])));

        // Enabled before disabled is within a tier only: a disabled tier-0 match ("Main menu" starts
        // with the query) stays above an enabled tier-2 one ("main" inside "Domain").
        var mixed = new[] { Entry("Domain", null, order: 0), Entry("Main menu", null, enabled: false, order: 1) };
        Assert.Equal(["Main menu", "Domain"], Labels(PaletteMatcher.Rank("main", mixed, [], [])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_or_blank_query_lists_the_commands_only_the_sessions_runs_first(string query)
    {
        var commands = new[]
        {
            Entry("Fetch all remotes", null, order: 0),
            Entry("Stash changes…", null, enabled: false, order: 1),
            Entry("Create branch…", null, order: 2),
            Entry("Push", null, order: 3),
        };
        var items = new[] { Entry("Check out main", "main", kind: PaletteEntryKind.Item) };

        // Disabled commands keep their places in an empty box (KD1).
        Assert.Equal(["Fetch all remotes", "Stash changes…", "Create branch…", "Push"], Labels(PaletteMatcher.Rank(query, commands, items, [])));
        Assert.Equal(
            ["Push", "Create branch…", "Fetch all remotes", "Stash changes…"],
            Labels(PaletteMatcher.Rank(query, commands, items, [commands[3].Key, commands[2].Key, items[0].Key])));
    }

    [Fact]
    public void Items_show_only_once_the_user_types_and_case_is_ignored()
    {
        var commands = new[] { Entry("Fetch all remotes", null) };
        var items = new[] { Entry("Check out feature/alpha", "feature/alpha", kind: PaletteEntryKind.Item) };
        Assert.Equal(["Fetch all remotes"], Labels(PaletteMatcher.Rank(string.Empty, commands, items, [])));
        Assert.Equal(["Check out feature/alpha"], Labels(PaletteMatcher.Rank("FEATURE/ALPHA", commands, items, [])));
        Assert.Equal(["Fetch all remotes"], Labels(PaletteMatcher.Rank("  fetch   ALL ", commands, items, [])));
        Assert.Empty(PaletteMatcher.Rank("zzz", commands, items, []));
    }

    /// <summary>
    /// Entries half of which match <c>check feat</c>: branches' check-outs and tags' deletions. The
    /// time per query is measured alone in the process (<see cref="PaletteGrowthTests"/>).
    /// </summary>
    internal static List<PaletteEntry> Many(int count) =>
        [.. Enumerable.Range(0, count).Select(i => i % 2 == 0
            ? Entry($"Check out feature/topic-{i}", $"feature/topic-{i}", kind: PaletteEntryKind.Item, order: i)
            : Entry($"Delete tag v{i}…", $"v{i}", kind: PaletteEntryKind.Item, order: i))];

    private static PaletteEntry Entry(string label, string? detail, bool enabled = true, PaletteEntryKind kind = PaletteEntryKind.Command, int order = 0) =>
        new(label, detail, null, enabled, enabled ? null : "Not now", false, kind, order, () => { });

    private static string[] Labels(IEnumerable<PaletteEntry> entries) => [.. entries.Select(entry => entry.Label)];
}
