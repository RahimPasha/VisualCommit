using Avalonia;
using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>
/// The assertions every part of phase 5's walk-through shares (Phase5Checks.Support.cs) and the
/// helpers of Phase5Expectations.Common.cs, tried once, and the history scenario opened in the
/// app, so that a check that fails points at the app and not at the assertion or the scenario.
/// No pictures: these are not checks of the report.
/// </summary>
public sealed class Phase5SupportTests : Phase5Checks
{
    [Theory]
    [InlineData("2026-01-02T09:00:00+00:00")]
    [InlineData("2026-01-02T09:00:00Z")]
    public void A_git_date_in_UTC_passes_whichever_way_the_git_version_writes_it(string gitText)
    {
        AssertGitDate(AppCommitTime, gitText);
        AssertGitDate(AppCommitTime, gitText + "\n");
        Assert.ThrowsAny<Exception>(() => AssertGitDate(AppCommitTime, "2026-01-02T09:00:00+01:00"));
    }

    [Fact]
    public async Task The_working_tree_is_the_full_folder_git_names_without_a_trailing_separator()
    {
        using var repo = await TempRepo.CreateAsync();
        var workingTree = await WorkingTreeAsync(repo);

        Assert.Equal(Path.GetFullPath(workingTree), workingTree);
        Assert.False(Path.EndsInDirectorySeparator(workingTree), $"Ends with a separator: {workingTree}");
        var marker = $"same-folder-{Guid.NewGuid():N}";
        File.WriteAllText(Path.Combine(repo.Path, marker), string.Empty);
        Assert.True(File.Exists(Path.Combine(workingTree, marker)), $"{workingTree} is not the folder {repo.Path}.");
    }

    [Fact]
    public void A_backup_refs_name_is_taken_apart_with_every_kind_the_backups_list_shows()
    {
        var undone = BackupRef.Parse("refs/visualcommit/backup/tag-undone-20260102T090001000Z/v1.0-rc");
        Assert.Equal(("tag-undone", (int?)null, false, "v1.0-rc"), (undone.Kind, undone.Number, undone.IsChanges, undone.Name));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 1, TimeSpan.Zero), undone.Time);
        Assert.Equal("tag-undone-20260102T090001000Z", undone.Stem);

        var rebase = BackupRef.Parse("refs/visualcommit/backup/rebase-20260102T090000123Z/feature/topic");
        Assert.Equal(("rebase", (int?)null, false, "feature/topic"), (rebase.Kind, rebase.Number, rebase.IsChanges, rebase.Name));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, 123, TimeSpan.Zero), rebase.Time);

        var skip = BackupRef.Parse("refs/visualcommit/backup/skip-20260102T090000000Z");
        var skipChanges = BackupRef.Parse("refs/visualcommit/backup/skip-20260102T090000000Z-changes");
        Assert.Equal(("skip", false, (string?)null), (skip.Kind, skip.IsChanges, skip.Name));
        Assert.Equal(("skip", true), (skipChanges.Kind, skipChanges.IsChanges));
        Assert.Equal(skip.Stem, skipChanges.Stem);

        var tag = BackupRef.Parse("refs/visualcommit/backup/remote-tag-20260102T090000000Z-3/origin/v1.2");
        Assert.Equal(("remote-tag", (int?)3, "origin/v1.2"), (tag.Kind, tag.Number, tag.Name));

        // The fourteen kinds of the backups list's words (design 12.3).
        Assert.Equal(
            ["abort", "branch", "detached", "discard", "force-push", "rebase", "remote", "remote-branch", "remote-tag", "reset", "skip", "stash", "tag", "tag-undone"],
            BackupKinds.Order(StringComparer.Ordinal));
        foreach (var kind in BackupKinds)
        {
            Assert.Equal(kind, BackupRef.Parse($"refs/visualcommit/backup/{kind}-20260102T090000000Z").Kind);
        }

        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/heads/main"));
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/visualcommit/backup/reset-2026"));
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/visualcommit/backup/merge-20260102T090000000Z"));
    }

    /// <summary>
    /// Part P's "exact state": the same for a state read twice, different when a file's contents,
    /// the index, an untracked file or a ref changes, and the same again once each is put back.
    /// </summary>
    [Fact]
    public async Task The_exact_state_tells_the_files_the_index_and_the_refs_apart()
    {
        using var repo = await Scenarios.ProtectAsync();
        var built = await ExactStateAsync(repo);
        Assert.Equal(built, await ExactStateAsync(repo));
        Assert.Contains("settings.local.json: {", built, StringComparison.Ordinal);
        Assert.DoesNotContain(".git" + Path.DirectorySeparatorChar, built, StringComparison.Ordinal);

        repo.WriteFile("src/app.txt", "app\nlocal\n");
        var changed = await ExactStateAsync(repo);
        Assert.NotEqual(built, changed);
        await repo.GitAsync("add", "--", "src/app.txt");
        Assert.NotEqual(changed, await ExactStateAsync(repo));
        await repo.GitAsync("reset", "--quiet", "--", "src/app.txt");
        Assert.Equal(changed, await ExactStateAsync(repo));
        await repo.GitAsync("checkout", "--", "src/app.txt");
        Assert.Equal(built, await ExactStateAsync(repo));

        repo.WriteFile("scratch.txt", "scratch\n");
        Assert.NotEqual(built, await ExactStateAsync(repo));
        File.Delete(Path.Combine(repo.Path, "scratch.txt"));
        await repo.GitAsync("branch", "topic");
        Assert.NotEqual(built, await ExactStateAsync(repo));
        await repo.GitAsync("branch", "-D", "topic");
        Assert.Equal(built, await ExactStateAsync(repo));
    }

    [Fact]
    public void The_menu_height_is_4as_formula()
    {
        Assert.Equal(232, MenuHeight(7, 2));
        Assert.Equal(84, MenuHeight(2, 0));
    }

    [Theory]
    [InlineData("a1a-toolbar")]
    [InlineData("a2c-activity-log")]
    [InlineData("b5c-history")]
    [InlineData("f10b-search-marks")]
    [InlineData("p24a-checkout-blocked")]
    [InlineData("u16a-undo")]
    [InlineData("u23c2-refused")]
    [InlineData("15a-numbered")]
    public void A_picture_name_takes_phase_5s_part_letters(string name) => Assert.Matches(PictureName(), name);

    [Theory]
    [InlineData("c12a-protected")]
    [InlineData("d18a-light")]
    [InlineData("a1a")]
    [InlineData("A1a-toolbar")]
    [InlineData("a1abcd-toolbar")]
    [InlineData("a1a-Toolbar")]
    [InlineData("a1a--toolbar")]
    public void A_name_of_no_part_or_not_of_the_reports_form_is_refused(string name) => Assert.DoesNotMatch(PictureName(), name);

    [Fact]
    public void The_new_tokens_colours_are_the_token_tables()
    {
        Assert.Equal(Avalonia.Media.Color.Parse("#F2C94C"), Dark.SearchMatch);
        Assert.Equal(Avalonia.Media.Color.Parse("#B98900"), Light.SearchMatch);
    }

    /// <summary>Phase 4 design 5.2's layout rules, on made-up positions: what the banner assertion accepts and what it reports.</summary>
    [Fact]
    public void The_banners_layout_rules_accept_buttons_beside_the_text_or_on_a_row_of_their_own()
    {
        var banner = new Rect(260, 74, 600, 36);
        var text = new Rect(296, 83, 300, 18);
        Rect[] beside = [new(612, 79, 80, 26), new(698, 79, 60, 26), new(764, 79, 88, 26)];
        Assert.Empty(BannerLayoutProblems(banner, text, 1, beside, oneLine: true, BannerButtonPlacement.BesideText));
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, beside, oneLine: false, null));
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, beside, null, BannerButtonPlacement.OwnRow));

        var tall = new Rect(260, 74, 440, 86);
        var wrapped = new Rect(296, 83, 396, 36);
        Rect[] row = [new(452, 126, 80, 26), new(538, 126, 60, 26), new(604, 126, 88, 26)];
        Assert.Empty(BannerLayoutProblems(tall, wrapped, 2, row, oneLine: false, BannerButtonPlacement.OwnRow));
        Assert.NotEmpty(BannerLayoutProblems(tall, wrapped, 2, row, null, BannerButtonPlacement.BesideText));
    }

    /// <summary>
    /// The history scenario opened in the app (Phase5ScenarioTests pins it with git): its fourteen
    /// commits in the graph's order, the working-changes row for src/Reader.cs, main checked out,
    /// no dialog and no menu.
    /// </summary>
    [AvaloniaFact]
    public async Task The_history_scenario_opens_with_its_commits_and_its_working_changes()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        var graph = Graph(app);
        var rows = graph.Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        Assert.True(rows.IsComplete, "The graph's history is not complete.");
        Assert.Equal(
            [
                "Read whole lines", "Stop tracing", "Write new notes", "Merge branch 'feature/readme'", "Explain the reader", "Remove the notes", "Compare whole lines",
                "Rename the parser to reader", "Add the comparer", "Add tracing", "Handle empty input", "Add the logo", "Add the parser", "Initial commit",
            ],
            Enumerable.Range(0, rows.Count).Select(row => rows.CommitAt(row).Subject));
        Assert.Equal(2, rows.MaxLaneCount);
        Assert.True(graph.ShowsWorkingRow, "The working-changes row is hidden.");
        AssertBranch(app, "main");
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark: 0);
    }
}
