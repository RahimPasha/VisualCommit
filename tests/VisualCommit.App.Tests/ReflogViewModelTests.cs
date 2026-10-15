using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.PageFakes;

namespace VisualCommit.App.Tests;

/// <summary>
/// The reflog page's view model without a UI (docs/phase-5-design.md 5.1 to 5.3, 11.3; report part
/// B's decisions BD3, BD5 to BD7, BD9): a reflog's rows with their shown selectors, times and lost
/// marks, the footer for skipped entries, an empty reflog's texts, the read again on every refresh
/// keeping the selection, the source menu, and the backups list with its times and words.
/// </summary>
public class ReflogViewModelTests
{
    private const string Shortcut = "d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";
    private const string Notes = "9dd61fde06301292b9b14d0911560b6d987f645d";

    [Fact]
    public async Task HEADs_reflog_lists_its_entries_newest_first_marks_the_lost_ones_and_counts_the_skipped()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["HEAD"] = new ReflogResult(
            [
                Entry("HEAD@{0}", 0, Notes, "checkout: moving from doomed to main", "Write the notes", 27),
                Entry("HEAD@{4}", 4, Shortcut, "commit: Try a shortcut", "Try a shortcut", 23),
            ],
            Skipped: 3);
        repository.Lost.Add(Shortcut);
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, "HEAD");

        await page.LoadAsync();

        Assert.Equal(("Reflog of HEAD", "HEAD", false), (page.Title, page.SourceLabel, page.ShowsBackups));
        Assert.Equal(
            [("HEAD@{0}", "2026-01-01 12:27", "checkout: moving from doomed to main", "9dd61fd", "Write the notes", false), ("HEAD@{4}", "2026-01-01 12:23", "commit: Try a shortcut", "d07c677", "Try a shortcut", true)],
            page.Rows.Cast<ReflogRowViewModel>().Select(row => (row.Selector, row.TimeText, row.Action, row.ShortSha, row.Subject, row.IsLost)));
        Assert.Equal("3 entries are not shown: they name no commit that exists", page.Footer);
        Assert.Null(page.SelectedRow);
        Assert.Equal(1, page.ReadsShown);
        Assert.Equal(1, repository.Reads["lost"]);

        repository.Reflogs["HEAD"] = repository.Reflogs["HEAD"] with { Skipped = 1 };
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Equal("1 entry is not shown: it names no commit that exists", page.Footer);
    }

    [Fact]
    public async Task A_branchs_reflog_shows_its_selectors_without_refs_heads_and_a_missing_one_says_so()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["refs/heads/main"] = new ReflogResult([Entry("refs/heads/main@{0}", 0, Notes, "reset: moving to 9dd61fd", "Write the notes", 25)], 0);
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, "refs/heads/main");

        await page.LoadAsync();

        Assert.Equal(("Reflog of main", "main"), (page.Title, page.SourceLabel));
        Assert.Equal("main@{0}", Assert.IsType<ReflogRowViewModel>(Assert.Single(page.Rows)).Selector);
        Assert.Null(page.Footer);

        // A deleted branch, an unborn HEAD: no reflog, and git's rules (5.2).
        page.ChooseSource("refs/heads/doomed");
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Equal(("Reflog of doomed", "doomed has no reflog", ReflogViewModel.ExpiryText), (page.Title, page.Message, page.Detail));
        Assert.Equal("Git keeps a reflog entry for 90 days, and for 30 days when no branch holds its commit.", page.Detail);
        Assert.Equal(1, repository.Reads["lost"]);
    }

    [Fact]
    public async Task A_row_the_user_picks_shows_its_commit_and_a_read_again_keeps_it_selected()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Notes, "a", "Write the notes", 27), Entry("HEAD@{1}", 1, Shortcut, "b", "Try a shortcut", 23)], 0);
        var host = new RecordingPageHost(repository);
        host.InGraph.Add(Notes);
        using var page = new ReflogViewModel(host, "HEAD");
        await page.LoadAsync();

        page.SelectedRow = page.Rows[1];
        Assert.Equal([Shortcut], host.Shown);

        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Equal(Shortcut, page.SelectedRow!.Sha);
        Assert.Equal([Shortcut], host.Shown);

        page.ShowInGraph(page.Rows[1]);
        page.ShowInGraph(page.Rows[0]);
        Assert.Equal([Notes], host.ShownInGraph);
        Assert.Equal(
            ["d07c677 Try a shortcut", "Create branch here…", "Check out this commit", "—", "Reset main to this commit…", "Cherry-pick onto main", "—", "Show in graph (disabled: \"This commit is not in the graph\")", "Copy SHA"],
            RepositoryMenusTests.Lines(page.MenuFor(page.Rows[1])));
    }

    [Fact]
    public async Task The_source_menu_switches_to_a_branch_and_to_the_backups()
    {
        var repository = new PageFakeRepository();
        var host = new RecordingPageHost(repository)
        {
            Refs = new RepoRefs(new HeadState("main", Notes), [new GitRef("refs/heads/clash", "clash", RefKind.LocalBranch, Notes), new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Notes, IsHead: true)], [], ["origin"]),
        };
        using var page = new ReflogViewModel(host, "HEAD");
        await page.LoadAsync();

        var menu = page.SourceMenu();
        Assert.Equal(["Reflog and backups", "HEAD", "clash", "main", "—", "Backups"], RepositoryMenusTests.Lines(menu));
        menu.Items.Single(item => item.Label == "Backups").Invoke();
        await WaitForAsync(() => page.ReadsShown == 2);

        Assert.Equal(("Backups", "Backups", true, "There are no backups"), (page.Title, page.SourceLabel, page.ShowsBackups, page.Message));
        page.SourceMenu().Items.Single(item => item.Label == "clash").Invoke();
        Assert.Equal("Reflog of clash", page.Title);
    }

    [Fact]
    public async Task The_backups_list_shows_each_backups_time_to_the_second_its_words_and_its_commit()
    {
        var repository = new PageFakeRepository();
        repository.Backups.Add(new BackupInfo(
            "branch-20260101T122701000Z",
            "branch",
            new DateTimeOffset(2026, 1, 1, 12, 27, 1, TimeSpan.Zero),
            null,
            [new BackupRefInfo("refs/visualcommit/backup/branch-20260101T122701000Z/doomed", "44e646bdcb1d920659f2b28b1b44e19f6ae45a7b", "doomed", false, "Doomed work", string.Empty, "44e646bdcb1d920659f2b28b1b44e19f6ae45a7b", "Doomed work")]));
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, source: null);

        await page.LoadAsync();

        var row = Assert.IsType<BackupRowViewModel>(Assert.Single(page.Rows));
        Assert.Equal(("branch-20260101T122701000Z", "2026-01-01 12:27:01", "Deleted branch doomed", "44e646b", "Doomed work"), (row.Name, row.TimeText, row.Words, row.ShortSha, row.Subject));
        Assert.Null(page.Footer);

        // BD9: picking a backup shows its commit.
        page.SelectedRow = row;
        Assert.Equal(["44e646bdcb1d920659f2b28b1b44e19f6ae45a7b"], host.Shown);
        Assert.Equal("Deleted branch doomed", RepositoryMenusTests.Lines(page.MenuFor(row))[0]);
    }

    [Fact]
    public void The_columns_follow_the_pages_width()
    {
        using var page = new ReflogViewModel(new RecordingPageHost(new PageFakeRepository()), "HEAD");

        // BD3: at 440 the time is hidden; the selector shows from 432, the time from 552.
        page.PageWidth = 440;
        Assert.Equal((false, true), (page.ShowsTime, page.ShowsSelector));
        page.PageWidth = 552;
        Assert.Equal((true, true), (page.ShowsTime, page.ShowsSelector));
        page.PageWidth = 431;
        Assert.Equal((false, false), (page.ShowsTime, page.ShowsSelector));
    }

    private static ReflogEntry Entry(string selector, int index, string sha, string message, string subject, int minute) =>
        new(selector, index, sha, [AddParserSha], message, subject, Minute(minute));

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "Timed out waiting for the page.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
