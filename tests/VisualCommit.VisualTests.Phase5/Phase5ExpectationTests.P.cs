using VisualCommit.Core.Graph;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>
/// Part P's expectations (Phase5Expectations.P.cs) held against each other and against the
/// report's numbers: the graphs derived from P0, the stash's menu by 4a's formula, git's texts and
/// the commands. No app: a mistake copying the report shows here, apart from the app.
/// </summary>
public sealed class Phase5ExpectationTestsP
{
    /// <summary>P0 has seven commits in two lanes, the scenario's SHAs in order; lane 1 ends in Stop tracking's node.</summary>
    [Fact]
    public void P0_is_the_scenario_as_built()
    {
        Assert.Equal(7, PartP.P0.Length);
        Assert.Equal(
            new[] { PartP.MainWorkSha, PartP.OtherFilesSha, PartP.OtherEditsSha, PartP.DescribeSha, PartP.StopTrackingSha, PartP.AddGuideSha, PartP.InitialCommitSha }.Select(sha => sha[..7]),
            PartP.P0.Select(row => row.ShortSha));
        Assert.All(PartP.P0, row => Assert.InRange(row.Lane, 0, PartP.Lanes - 1));
        Assert.Equal(new[] { 1 }, PartP.P0[PartP.StopTrackingRow].Ends);
        Assert.Equal("Describe the app", PartP.P0[PartP.DescribeRow].Message);
        Assert.Equal("Other files", PartP.P0[PartP.OtherFilesRow].Message);
        Assert.Equal("Add the guide", PartP.P0[PartP.AddGuideRow].Message);
    }

    /// <summary>P0h is P0 with HEAD detached at Main work: only row 0's labels differ.</summary>
    [Fact]
    public void P0h_differs_from_P0_in_row_0s_labels_only()
    {
        Assert.Equal(["HEAD", "main"], PartP.P0h[0].Labels);
        Assert.Equal(PartP.P0.Skip(1), PartP.P0h.Skip(1));
        Assert.Equal(PartP.P0[0] with { Labels = PartP.P0h[0].Labels }, PartP.P0h[0]);
    }

    /// <summary>P1 is P0 one lower under the stash, with HEAD detached on Describe the app and main without its check mark.</summary>
    [Fact]
    public void P1_is_P0_under_the_stash_with_HEAD_on_describe_the_app()
    {
        Assert.Equal(PartP.P0.Length + 1, PartP.P1.Length);
        var stash = PartP.P1[PartP.StashRow];
        Assert.True(stash.Stash);
        Assert.Equal((PartP.AutoStashListed, "256077f", 0, 0), (stash.Message, stash.ShortSha, stash.Lane, stash.Colour));
        Assert.Equal(PartP.AutoStashSha[..7], stash.ShortSha);
        for (var row = 0; row < PartP.P0.Length; row++)
        {
            var before = PartP.P0[row];
            var after = PartP.P1[row + 1];
            Assert.Equal((before.Message, before.Lane, before.Colour, before.ShortSha, before.Date), (after.Message, after.Lane, after.Colour, after.ShortSha, after.Date));
            Assert.Equal(before.Ends, after.Ends);
        }

        Assert.Equal(["main"], PartP.P1[PartP.MainWorkRowUnderStash].Labels);
        Assert.Equal(["HEAD", "v1.1"], PartP.P1[PartP.DescribeRowUnderStash].Labels);
        Assert.Equal("Describe the app", PartP.P1[PartP.DescribeRowUnderStash].Message);
    }

    /// <summary>P1m is P1 with main checked out again: main with its check mark, v1.1 alone on Describe the app.</summary>
    [Fact]
    public void P1m_is_P1_with_main_checked_out()
    {
        Assert.Equal(PartP.P1.Length, PartP.P1m.Length);
        Assert.Equal([Current("main")], PartP.P1m[PartP.MainWorkRowUnderStash].Labels);
        Assert.Equal(["v1.1"], PartP.P1m[PartP.DescribeRowUnderStash].Labels);
        for (var row = 0; row < PartP.P1.Length; row++)
        {
            Assert.Equal(PartP.P1[row] with { Labels = PartP.P1m[row].Labels }, PartP.P1m[row]);
        }
    }

    /// <summary>P0s is P1m with P27 part B's stash on top, dated by the copy's clock.</summary>
    [Fact]
    public void P0s_is_P1m_with_the_experiment_stash()
    {
        Assert.Equal(PartP.P1m.Skip(1), PartP.P0s.Skip(1));
        var stash = PartP.P0s[0];
        Assert.True(stash.Stash);
        Assert.Equal((PartP.ExperimentListed, PartP.ExperimentStashSha[..7], "2026-01-01 12:07"), (stash.Message, stash.ShortSha, stash.Date));
        Assert.Equal(["stash@{0}"], stash.Labels);
    }

    /// <summary>A stash on top draws its line dashed out of its ring and into its base's node, both in lane 0.</summary>
    [Fact]
    public void The_stash_on_top_dashes_two_lines_in_lane_0()
    {
        Assert.Equal(new[] { (0, GraphLineKind.OutOfNode, 0), (1, GraphLineKind.IntoNode, 0) }, PartP.StashOnTopDashes);
    }

    /// <summary>
    /// The drawn labels' positions agree with the conventions' widths (<c>main</c> ✓ 50.8,
    /// <c>main</c> 37.8, <c>other</c> 40.6, <c>v1.1</c> 43.7, <c>HEAD</c> 42.8, <c>stash@{0}</c>
    /// 68.3), the first from x 266 and each next one 4 after the one before.
    /// </summary>
    [Fact]
    public void The_drawn_labels_are_the_conventions_widths_4_apart_from_266()
    {
        var current = new Dictionary<(string, bool), double>
        {
            [("main", true)] = 50.8,
            [("main", false)] = 37.8,
            [("other", false)] = 40.6,
            [("v1.1", false)] = 43.7,
            [("HEAD", false)] = 42.8,
            [("stash@{0}", false)] = 68.3,
        };
        var graphs = new (CommitRow[] Rows, (int Row, PartP.DrawnLabel[] Labels)[] Drawn)[]
        {
            (PartP.P0, PartP.P0Labels), (PartP.P0h, PartP.P0hLabels), (PartP.P1, PartP.P1Labels), (PartP.P1m, PartP.P1mLabels),
        };
        foreach (var (rows, drawn) in graphs)
        {
            foreach (var (row, labels) in drawn)
            {
                Assert.Equal(rows[row].Labels.Select(label => label.Replace(CurrentMark, string.Empty, StringComparison.Ordinal)), labels.Select(label => label.Text));
                var left = 266.0;
                foreach (var label in labels)
                {
                    var isCurrent = rows[row].Labels.Contains(Current(label.Text));
                    Assert.Equal(left, label.Left, 0.05);
                    Assert.Equal(current[(label.Text, isCurrent)], label.Right - label.Left, 0.05);
                    left = label.Right + 4;
                }
            }
        }
    }

    /// <summary>The stash's menu (P27 step 9) is 145 high by 4a's formula: four items and one separator.</summary>
    [Fact]
    public void The_stash_menus_height_is_4as_formula()
    {
        var (items, separators) = PartP.MenuCounts(PartP.ExperimentStashMenu);
        Assert.Equal((4, 1), (items, separators));
        Assert.Equal(PartP.StashMenuHeight, MenuHeight(items, separators));
        Assert.Equal("Apply stash", PartP.ExperimentStashMenu[1]);
        Assert.Equal(PartP.ApplyStashItem, PartP.ExperimentStashMenu[1]);
    }

    /// <summary>Each refusal's sentence carries git's count: TP1 names one path, TP3 three, TP4 and TP5 one each.</summary>
    [Fact]
    public void The_summaries_count_the_tab_lines_under_gits_headers()
    {
        Assert.Single(PartP.Tp1, line => line.StartsWith('\t'));
        Assert.Equal(3, PartP.Tp3.Count(line => line.StartsWith('\t')));
        Assert.Single(PartP.Tp4, line => line.StartsWith('\t'));
        Assert.Single(PartP.Tp5, line => line.StartsWith('\t'));
        Assert.Single(PartP.Tp6, line => line.StartsWith('\t'));
        Assert.StartsWith("1 conflict prevents checkout.", PartP.OneConflictPreventsCheckout, StringComparison.Ordinal);
        Assert.StartsWith("3 conflicts prevent checkout.", PartP.ThreeConflictsPreventCheckout, StringComparison.Ordinal);
        Assert.Equal(["\tdocs/guide.txt", "\tsrc/app.txt", "\tsrc/lib.txt"], PartP.Tp3.Where(line => line.StartsWith('\t')));
    }

    /// <summary>Each of git's texts holds its key line; TP8 is TP7 then the pick's fatal line; TP9's key is its names' two-space indent (PD10).</summary>
    [Fact]
    public void Gits_texts_hold_their_key_lines()
    {
        Assert.Equal(PartP.CheckoutChangesKey, PartP.Tp1[0]);
        Assert.Equal(PartP.CheckoutChangesKey, PartP.Tp3[0]);
        Assert.Equal(PartP.CheckoutUntrackedKey, PartP.Tp4[0]);
        Assert.Equal(PartP.CheckoutFolderKey, PartP.Tp5[0]);
        Assert.Equal(PartP.CheckoutUntrackedKey, PartP.Tp6[0]);
        Assert.Equal(PartP.MergeChangesKey, PartP.Tp7[0]);
        Assert.Equal([.. PartP.Tp7, "fatal: cherry-pick failed"], PartP.Tp8);
        Assert.Contains(PartP.Tp9Key, PartP.Tp9);
        Assert.DoesNotContain(PartP.Tp9, line => line.StartsWith("error:", StringComparison.Ordinal));
        Assert.Equal(PartP.Tp10Key, PartP.Tp10[0]);
        Assert.Equal(PartP.Tp11Key, PartP.Tp11[0]);
        Assert.Equal(PartP.MergeChangesKey, PartP.Tp12[0]);
        Assert.Equal("fatal: revert failed", PartP.Tp12[^1]);
        Assert.Contains("\tsettings.local.json", PartP.Tp6);
    }

    /// <summary>The commands name full SHAs where the app passes them, and the titles their short SHAs.</summary>
    [Fact]
    public void The_commands_and_titles_name_the_scenarios_commits()
    {
        Assert.Equal("git switch --create topic 19a90447e124bf3a74124a5673df655601c53d5b", PartP.CreateTopicCommand);
        Assert.Equal("git switch --detach --no-overwrite-ignore f39ea2d9addec45f926151cd2267cb57ea0b16aa", PartP.DetachNoOverwriteIgnoreCommand(PartP.DescribeSha));
        Assert.Equal("git switch --detach 41f8398f95fa73a5614e32760cc511673aa6a80d", PartP.DetachCommand(PartP.MainWorkSha));
        Assert.Equal("git stash push --include-untracked --message \"Auto stash before checking out f39ea2d\"", PartP.AutoStashCommand("f39ea2d"));
        Assert.Equal("git cherry-pick 4f5e8215c9321acae3a2ea386bda05107915f2da", PartP.CherryPickOtherEditsCommand);
        Assert.Equal("git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa", PartP.RevertDescribeCommand);
        Assert.Equal(("Check out f39ea2d", "Cherry-pick 4f5e821", "Revert f39ea2d"), (PartP.CheckOut(PartP.DescribeSha), PartP.CherryPickOtherEdits, PartP.RevertDescribe));
        Assert.Equal("Detached at 41f8398", PartP.DetachedAt(PartP.MainWorkSha));
        Assert.Equal("HEAD is now detached at 41f8398", PartP.DetachedAtStatus(PartP.MainWorkSha));
        Assert.Equal("Dropped stash@{0} (256077fbd990adad7fe00df31789590d7370aff0)", PartP.Dropped(PartP.AutoStashSha));
        Assert.Equal(PartP.AutoStashListed, "On main: " + PartP.AutoStashMessage("f39ea2d"));
        Assert.Equal(PartP.ChangesInAutoStash, $"Your changes are in the stash \"{PartP.AutoStashMessage("f39ea2d")}\".");
        Assert.Equal("pop of stash \"" + PartP.AutoStashListed + "\"", PartP.PopAutoStashEntry);
    }

    /// <summary>The pop's backup is the fixed backup clock's first name, of the kind "stash" (D171).</summary>
    [Fact]
    public void The_pops_backup_is_the_first_of_the_fixed_clock()
    {
        var backup = BackupRef.Parse(PartP.PopBackup);
        Assert.Equal(("stash", new DateTimeOffset(2026, 1, 2, 9, 0, 1, TimeSpan.Zero), (string?)null), (backup.Kind, backup.Time, backup.Name));
    }
}
