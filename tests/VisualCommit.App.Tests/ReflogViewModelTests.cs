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

        // More entries than the page reads: the footer says so first (5.2, 15.9).
        repository.Reflogs["HEAD"] = repository.Reflogs["HEAD"] with { More = true };
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => page.ReadsShown == 3);
        Assert.Equal("Only the newest 500 entries are shown. 1 entry is not shown: it names no commit that exists", page.Footer);
        repository.Reflogs["HEAD"] = repository.Reflogs["HEAD"] with { Skipped = 0 };
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => page.ReadsShown == 4);
        Assert.Equal(ReflogViewModel.MoreText, page.Footer);
    }

    [Fact]
    public async Task A_newer_read_cancels_the_lost_commits_walk_of_the_read_it_replaces()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Notes, "a", "Write the notes", 27), Entry("HEAD@{1}", 1, Shortcut, "b", "Try a shortcut", 23)], 0);
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, "HEAD");
        await page.LoadAsync();

        // Two refreshes in a row (15.9): the first read's walk stops; only the second one's shows.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.LostGate = gate;
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => repository.ReadCount("lost") == 2);
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => repository.CancelledLostWalks == 1);
        gate.SetResult();
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Equal((1, 3), (repository.CancelledLostWalks, repository.ReadCount("lost")));
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

        // A commit HEAD's history holds cannot be picked onto it again (2.12), whatever the graph has selected.
        host.OnHead.Add(Shortcut);
        Assert.Contains("Cherry-pick onto main (disabled: \"This commit is already on main\")", RepositoryMenusTests.Lines(page.MenuFor(page.Rows[1])));
    }

    [Fact]
    public async Task A_read_again_keeps_the_rows_it_did_not_change_and_their_lost_marks_until_the_walk_ends()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Notes, "a", "Write the notes", 27), Entry("HEAD@{1}", 1, Shortcut, "b", "Try a shortcut", 23)], 0);
        repository.Lost.Add(Shortcut);
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, "HEAD");
        await page.LoadAsync();
        var rows = page.Rows.ToList();
        Assert.Same(host.Refs, page.ShownFor);

        // The refresh's read after a ref moved: the same entries keep their rows (and the list its
        // containers and focus); the lost mark stays until the new walk ends, then follows it.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.LostGate = gate;
        repository.Lost.Clear();
        var refreshed = host.Refs with { Refs = [.. host.Refs.Refs, new GitRef("refs/heads/kept", "kept", RefKind.LocalBranch, Shortcut)] };
        page.OnRefsRead(refreshed);
        await WaitForAsync(() => repository.ReadCount("lost") == 2);
        Assert.Equal(rows, page.Rows);
        Assert.True(Assert.IsType<ReflogRowViewModel>(page.Rows[1]).IsLost);
        Assert.Same(host.Refs, page.ShownFor);
        gate.SetResult();
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Same(rows[1], page.Rows[1]);
        Assert.False(Assert.IsType<ReflogRowViewModel>(page.Rows[1]).IsLost);
        Assert.Same(refreshed, page.ShownFor);

        // A new entry on top shifts every selector: those rows are new, and a row's commit keeps
        // the mark it had until the walk says otherwise.
        repository.LostGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsType<ReflogRowViewModel>(page.Rows[0]).IsLost = true;
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Shortcut, "c", "Try a shortcut", 28), Entry("HEAD@{1}", 1, Notes, "a", "Write the notes", 27), Entry("HEAD@{2}", 2, Shortcut, "b", "Try a shortcut", 23)], 0);
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => repository.ReadCount("lost") == 3);
        Assert.Equal(["HEAD@{0}", "HEAD@{1}", "HEAD@{2}"], page.Rows.Select(row => row.Name));
        Assert.DoesNotContain(page.Rows, row => rows.Contains(row));
        Assert.Equal([false, true, false], page.Rows.Cast<ReflogRowViewModel>().Select(row => row.IsLost));
        repository.LostGate.SetResult();
        await WaitForAsync(() => page.ReadsShown == 3);
        Assert.All(page.Rows.Cast<ReflogRowViewModel>(), row => Assert.False(row.IsLost));
    }

    /// <summary>
    /// Step 25's review: every refresh walked again for the lost marks, also one that moved no
    /// ref (a stage writes the index), which takes seconds in a large repository without a
    /// commit-graph. A commit's mark depends only on the refs and the reflog's commits: a read
    /// again for the same takes the last walk's marks, and is shown as a read all the same.
    /// </summary>
    [Fact]
    public async Task A_read_again_for_the_same_refs_and_commits_takes_the_last_walks_marks()
    {
        var repository = new PageFakeRepository();
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Notes, "a", "Write the notes", 27), Entry("HEAD@{1}", 1, Shortcut, "b", "Try a shortcut", 23)], 0);
        repository.Lost.Add(Shortcut);
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, "HEAD");
        await page.LoadAsync();
        Assert.Equal(1, repository.ReadCount("lost"));

        // The same refs in another snapshot (the remotes are no part of the walk), the same entries.
        var same = host.Refs with { Remotes = ["origin", "upstream"] };
        page.OnRefsRead(same);
        await WaitForAsync(() => page.ReadsShown == 2);
        Assert.Equal(1, repository.ReadCount("lost"));
        Assert.Same(same, page.ShownFor);
        Assert.Equal([false, true], page.Rows.Cast<ReflogRowViewModel>().Select(row => row.IsLost));

        // A new entry, or a ref moved, walks again.
        repository.Reflogs["HEAD"] = new ReflogResult([Entry("HEAD@{0}", 0, Shortcut, "c", "Try a shortcut", 28), Entry("HEAD@{1}", 1, Notes, "a", "Write the notes", 27), Entry("HEAD@{2}", 2, Shortcut, "b", "Try a shortcut", 23), Entry("HEAD@{3}", 3, ParentSha, "d", "Earlier", 20)], 0);
        page.OnRefsRead(same);
        await WaitForAsync(() => page.ReadsShown == 3);
        Assert.Equal(2, repository.ReadCount("lost"));
        var moved = same with { Head = new HeadState("main", Shortcut) };
        page.OnRefsRead(moved);
        await WaitForAsync(() => page.ReadsShown == 4);
        Assert.Equal(3, repository.ReadCount("lost"));
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
    public async Task A_force_pushs_backup_is_restored_only_while_the_remote_tracking_reflog_has_the_commit_the_push_left()
    {
        const string Before = "f7a69be38486b8b82fa0f12b0a6c0482af8a0424";
        const string Pushed = "362fbd633bc18c7553bf271ad7d31f1db055af2d";
        var repository = new PageFakeRepository();
        repository.Backups.Add(new BackupInfo(
            "force-push-20260101T120901000Z",
            "force-push",
            new DateTimeOffset(2026, 1, 1, 12, 9, 1, TimeSpan.Zero),
            null,
            [new BackupRefInfo("refs/visualcommit/backup/force-push-20260101T120901000Z/origin/topic", Before, "origin/topic", false, "Topic work", string.Empty, Before, "Topic work")]));
        var remote = new ReflogResult([Entry("refs/remotes/origin/topic@{0}", 0, Pushed, "update by push", "Topic work", 9), Entry("refs/remotes/origin/topic@{1}", 1, Before, "update by push", "Topic work", 2)], 0);
        repository.Reflogs["refs/remotes/origin/topic"] = remote;
        var host = new RecordingPageHost(repository);
        using var page = new ReflogViewModel(host, source: null);

        // 7.10: the lease is the "update by push" entry whose older neighbour is the backup's commit.
        Assert.Equal(Pushed, remote.ForcePushLease(Before));
        Assert.Null(remote.ForcePushLease(Pushed));
        Assert.Null((remote with { Entries = [remote.Entries[0] with { Message = "fetch: forced-update" }, remote.Entries[1]] }).ForcePushLease(Before));

        await page.LoadAsync();
        var row = Assert.IsType<BackupRowViewModel>(Assert.Single(page.Rows));
        Assert.True(row.HasForcePushLease);
        Assert.Equal("Push topic back to origin…", RepositoryMenusTests.Lines(page.MenuFor(row))[1]);
        Assert.Equal(1, repository.ReadCount("reflog refs/remotes/origin/topic"));

        // The remote-tracking reflog no longer has it (expired, or the ref was fetched over).
        repository.Reflogs.Remove("refs/remotes/origin/topic");
        page.OnRefsRead(host.Refs);
        await WaitForAsync(() => page.ReadsShown == 2);
        row = Assert.IsType<BackupRowViewModel>(Assert.Single(page.Rows));
        Assert.False(row.HasForcePushLease);
        Assert.Equal("Push topic back to origin… (disabled: \"The commit your force push left can no longer be found\")", RepositoryMenusTests.Lines(page.MenuFor(row))[1]);
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
