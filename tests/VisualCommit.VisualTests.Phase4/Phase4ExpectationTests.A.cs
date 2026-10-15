using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// Part A's expectations (Phase4Expectations.A.cs) held against each other and against the
/// report's numbers: the menus' heights by 4a's formula, the graphs' rows and lanes, the helpers.
/// No app: a mistake copying the report shows here, apart from the app.
/// </summary>
public sealed class Phase4ExpectationTestsA
{
    public static TheoryData<string, double> Menus() => new()
    {
        { nameof(PartA.MainMoreCommitMenu), PartA.CommitMenuHeight },
        { nameof(PartA.MainWorkCommitMenu), PartA.CommitMenuHeight },
        { nameof(PartA.TopicOneCommitMenu), PartA.CommitMenuHeight },
        { nameof(PartA.TopicTwoCommitMenu), PartA.CommitMenuHeight },
        { nameof(PartA.MainBranchMenu), PartA.LocalBranchWithUpstreamMenuHeight },
        { nameof(PartA.OriginMainMenu), PartA.RemoteBranchMenuHeight },
        { nameof(PartA.OriginFeatureTopicMenu), PartA.RemoteBranchMenuHeight },
        { nameof(PartA.FeatureTopicMenu), PartA.CurrentBranchWithUpstreamMenuHeight },
        { nameof(PartA.SyncMainMenu), PartA.CurrentBranchWithUpstreamMenuHeight },
        { nameof(PartA.ChainMenu), PartA.SeveralCommitsMenuHeight },
        { nameof(PartA.NotAChainMenu), PartA.SeveralCommitsMenuHeight },
        { nameof(PartA.NotOnHeadMenu), PartA.SeveralCommitsMenuHeight },
        { nameof(PartA.RootMenu), PartA.SeveralCommitsMenuHeight },
        { nameof(PartA.TopicOntoMainDropMenu), PartA.DropWithRebaseMenuHeight },
        { nameof(PartA.TopicOntoOriginMainDropMenu), PartA.DropWithRebaseMenuHeight },
        { nameof(PartA.OriginMainOntoTopicDropMenu), PartA.DropOfRemoteBranchMenuHeight },
        { nameof(PartA.OtherOntoMainDropMenu), PartA.DropWithRebaseMenuHeight },
        { nameof(PartA.ConflictedFileMenu), PartA.ConflictedFileMenuHeight },
    };

    /// <summary>Each menu's height in the report is 4a's formula on its items and separators.</summary>
    [Theory]
    [MemberData(nameof(Menus))]
    public void A_menus_height_is_4as_formula_on_its_items(string menu, double height)
    {
        var lines = (string[])typeof(PartA).GetField(menu)!.GetValue(null)!;
        var (items, separators) = PartA.MenuCounts(lines);
        Assert.Equal(height, MenuHeight(items, separators));
    }

    [Fact]
    public void The_menu_counts_leave_out_the_header_and_count_the_separators()
    {
        Assert.Equal((9, 2), PartA.MenuCounts(PartA.MainMoreCommitMenu));
        Assert.Equal((16, 5), PartA.MenuCounts(PartA.MainBranchMenu));
        Assert.Equal((3, 0), PartA.MenuCounts(PartA.OriginMainOntoTopicDropMenu));
    }

    /// <summary>"Main work"'s menu is "Main more"'s with its own header (A2 step 4: A1 step 1's items).</summary>
    [Fact]
    public void Main_works_menu_is_main_mores_with_its_header()
    {
        Assert.Equal("dd6a6fc Main work", PartA.MainWorkCommitMenu[0]);
        Assert.Equal(PartA.MainMoreCommitMenu.Skip(1), PartA.MainWorkCommitMenu.Skip(1));
    }

    /// <summary>The current branch's menu (A1 step 7) for feature/topic: 11 items, 4 separators, no rebase item.</summary>
    [Fact]
    public void The_current_branchs_menu_has_pull_with_rebase_and_no_rebase_item()
    {
        Assert.Equal("Pull from origin/feature/topic (rebase)", PartA.FeatureTopicMenu[2]);
        Assert.DoesNotContain(PartA.FeatureTopicMenu, line => line.StartsWith("Rebase ", StringComparison.Ordinal));
        Assert.Equal(PartA.FeatureTopicMenu.Length, PartA.SyncMainMenu.Length);
    }

    public static TheoryData<string, int, int> Graphs() => new()
    {
        { nameof(PartA.R0), 7, PartA.RebaseLanes },
        { nameof(PartA.R1), 9, PartA.RebaseLanes },
        { nameof(PartA.R2), 9, PartA.RebaseLanes },
        { nameof(PartA.R3), 7, PartA.R3Lanes },
        { nameof(PartA.S1), 6, 3 },
        { nameof(PartA.S2), 7, 3 },
        { nameof(PartA.S3), 5, 2 },
        { nameof(PartA.S4), 6, 3 },
        { nameof(PartA.S5), 6, 2 },
        { nameof(PartA.S6), 5, 2 },
    };

    /// <summary>Each graph has the report's number of rows; every node is in one of its lanes; every row states its short SHA, its date and where lanes end.</summary>
    [Theory]
    [MemberData(nameof(Graphs))]
    public void A_graph_has_the_reports_rows_in_its_lanes(string graph, int rows, int lanes)
    {
        var table = (CommitRow[])typeof(PartA).GetField(graph)!.GetValue(null)!;
        Assert.Equal(rows, table.Length);
        Assert.All(table, row =>
        {
            Assert.InRange(row.Lane, 0, lanes - 1);
            Assert.Equal(7, row.ShortSha!.Length);
            Assert.Matches(@"^2026-01-01 12:0\d$", row.Date!);
            Assert.NotNull(row.Ends);
            Assert.All(row.Ends!, lane => Assert.InRange(lane, 0, lanes - 1));
        });
        Assert.Equal(1, table.Count(row => row.Labels.Any(label => label.EndsWith(CurrentMark, StringComparison.Ordinal) || label == "HEAD")));
    }

    /// <summary>The short SHAs in the graphs are the starts of the full ids the report gives.</summary>
    [Fact]
    public void The_graphs_short_shas_are_the_full_ids_starts()
    {
        Assert.StartsWith(PartA.R1[0].ShortSha!, PartA.TopicTwoOntoMainSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R1[1].ShortSha!, PartA.TopicOneOntoMainSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R2[0].ShortSha!, PartA.TopicTwoOntoMainWorkSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R2[1].ShortSha!, PartA.TopicOneOntoMainWorkSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R3[0].ShortSha!, PartA.OtherOneOntoMainSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.S2[0].ShortSha!, PartA.LocalWorkRebasedSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.S4[0].ShortSha!, PartA.PublishedElsewhereSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.S5[0].ShortSha!, PartA.UnpushedWorkContinuedSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R0[PartA.MainMoreRow].ShortSha!, PartA.MainMoreSha, StringComparison.Ordinal);
        Assert.StartsWith(PartA.R0[PartA.TopicTwoRow].ShortSha!, PartA.TopicTwoSha, StringComparison.Ordinal);
        Assert.Equal("Main work", PartA.R2[PartA.MainWorkRowInR2].Message);
    }

    /// <summary>The left panels differ from R0's only where the report says.</summary>
    [Fact]
    public void The_left_panels_differ_only_in_the_rows_the_report_names()
    {
        Assert.Equal(PartA.R0LeftPanel.Length, PartA.R1LeftPanel.Length);
        Assert.Contains("    topic (head) ↑4 ↓2", PartA.R1LeftPanel);
        Assert.Contains("    topic (head) ↑3 ↓2", PartA.R2LeftPanel);
        Assert.Single(PartA.R0LeftPanel.Except(PartA.R1LeftPanel));
        Assert.Equal(["    other (head)", "    topic"], PartA.R3LeftPanel.Except(PartA.R0LeftPanel));
        Assert.Equal(["  published ↑1 ↓1"], PartA.S4LeftPanel.Except(PartA.S3LeftPanel));
    }

    /// <summary>TA3 for two commits and one (the conventions' "one Rebasing (1/1) for one commit").</summary>
    [Fact]
    public void Ta3_lists_one_rebasing_line_per_commit_then_the_success()
    {
        Assert.Equal(["Rebasing (1/2)", "Rebasing (2/2)", "Successfully rebased and updated refs/heads/feature/topic."], PartA.Ta3("feature/topic", 2));
        Assert.Equal(["Rebasing (1/1)", "Successfully rebased and updated refs/heads/main."], PartA.Ta3("main", 1));
    }

    /// <summary>The commands part A expects, as the call log writes them.</summary>
    [Fact]
    public void The_commands_are_written_as_the_call_log_writes_them()
    {
        Assert.Equal("git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146", PartA.RebaseOntoMainCommand);
        Assert.Equal("git merge-base --is-ancestor 8e653159ca5e460a187117d51b3f18bdf81f5c96 FETCH_HEAD", PartA.IsAncestorOfFetchHead(PartA.UnpushedWorkSha));
        Assert.Equal("git merge-base --fork-point refs/remotes/origin/main HEAD", PartA.ForkPoint("origin/main"));
        Assert.Equal("git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/origin/main...HEAD --", PartA.CountAll("origin/main"));
        Assert.Equal(
            "git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/origin/main...HEAD ^a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c --",
            PartA.CountAfterForkPoint("origin/main", PartA.SharedWorkSha));
    }

    /// <summary>The author date in the stopped rebase's author-script is Unpushed work's, 2026-01-01 12:03 UTC.</summary>
    [Fact]
    public void The_author_scripts_date_is_unpushed_works()
    {
        Assert.Equal(1767268980, new DateTimeOffset(2026, 1, 1, 12, 3, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        Assert.Equal(129.0 + 26, WindowLayout.RowCentreY(1));
    }
}
