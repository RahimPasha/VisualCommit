using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="CommitPager"/>'s place for each stash, on made-up histories: by date or above its
/// base without a filter (D54), and while the graph hides or solos branches only where its base is
/// shown, or not at all (docs/phase-5-design.md 8.2, D178). The real history's pages are in
/// <see cref="CommitLogTests"/>, the filtered history against git in <see cref="GraphFilterTests"/>.
/// A history is written as git prints it, newest first: <c>"C: B"</c> is commit C with parent B,
/// each a minute older than the one before.
/// </summary>
public class CommitPagerTests
{
    private static readonly DateTimeOffset Newest = new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Without_a_filter_a_stash_goes_before_the_first_older_commit_and_one_whose_base_never_comes_goes_last()
    {
        // S1 is newer than C, so it goes above C, the first commit older than it; S2 waits for its
        // base, which never comes, and goes after everything.
        var rows = Page(
            ["D: C", "C: B", "B: A", "A:"],
            [Stash("S1", "B", minutesOld: 0.5), Stash("S2", "gone", minutesOld: 10)],
            shownTips: null);

        Assert.Equal(["D", "S1", "C", "B", "A", "S2"], rows);
    }

    [Fact]
    public void With_a_filter_a_stash_on_a_shown_tip_is_placed_by_date_as_without_one()
    {
        // B is a shown branch's tip: the stash, newer than C, goes above C, as D54 places it.
        var rows = Page(
            ["D: A", "C: A", "B: A", "A:"],
            [Stash("S", "B", minutesOld: 0.5)],
            shownTips: ["D", "C", "B"]);

        Assert.Equal(["D", "S", "C", "B", "A"], rows);
    }

    [Fact]
    public void With_a_filter_a_stash_whose_base_is_not_known_yet_waits_for_its_base()
    {
        // A is shown, but nothing says so before a commit names it: the stash, newer than every
        // commit, waits until B names A, and is placed by date from then on.
        var rows = Page(
            ["D: X", "C: X", "B: A", "X:", "A:"],
            [Stash("S", "A", minutesOld: 0.5)],
            shownTips: ["D", "C", "B"]);

        Assert.Equal(["D", "C", "B", "S", "X", "A"], rows);
    }

    [Fact]
    public void With_a_filter_a_stash_goes_in_just_above_its_base_when_that_comes_first()
    {
        // The stash is older than every commit: it goes in just above its base, as without a filter.
        var rows = Page(
            ["B: A", "A:"],
            [Stash("S", "A", minutesOld: 5)],
            shownTips: ["B"]);

        Assert.Equal(["B", "S", "A"], rows);
    }

    [Fact]
    public void With_a_filter_a_stash_whose_base_is_never_shown_is_left_out()
    {
        var rows = Page(
            ["C: B", "B: A", "A:"],
            [Stash("Hidden base", "H", minutesOld: 0.5), Stash("Old, hidden base", "H", minutesOld: 10)],
            shownTips: ["C"]);

        Assert.Equal(["C", "B", "A"], rows);
    }

    [Fact]
    public void With_a_filter_and_no_commit_at_all_every_stash_is_left_out()
    {
        var pages = new List<IReadOnlyList<CommitInfo>>();
        var pager = new CommitPager([Stash("S", "A", minutesOld: 1)], ["A"], pages.Add);

        pager.Finish();

        Assert.Empty(pages);
    }

    /// <summary>
    /// What a pager hands over for <paramref name="history"/> (lines such as <c>"C: B"</c>, newest
    /// first, a minute apart from <see cref="Newest"/>) and <paramref name="stashes"/>: the rows'
    /// names in order. A null <paramref name="shownTips"/> is no filter.
    /// </summary>
    private static List<string> Page(string[] history, StashEntry[] stashes, string[]? shownTips)
    {
        var pages = new List<IReadOnlyList<CommitInfo>>();
        var pager = new CommitPager(stashes, shownTips, pages.Add);
        for (var i = 0; i < history.Length; i++)
        {
            var colon = history[i].IndexOf(':', StringComparison.Ordinal);
            var parents = history[i][(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var date = Newest.AddMinutes(-i);
            pager.Add(new CommitInfo(history[i][..colon], parents, "Test Author", "author@example.com", date, date, history[i]));
        }

        pager.Finish();
        var rows = pages.SelectMany(page => page).ToList();
        Assert.All(rows.Where(row => row.Kind == CommitKind.Stash), stash => Assert.Single(stash.Parents));
        return [.. rows.Select(row => row.Sha)];
    }

    /// <summary>A stash <paramref name="name"/> made on <paramref name="baseSha"/>, <paramref name="minutesOld"/> minutes before <see cref="Newest"/>.</summary>
    private static StashEntry Stash(string name, string baseSha, double minutesOld)
    {
        var date = Newest.AddMinutes(-minutesOld);
        return new StashEntry(0, name, baseSha, "On main: " + name, "Test Author", "author@example.com", date, date);
    }
}
