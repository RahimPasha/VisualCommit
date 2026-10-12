using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The helpers of Phase3Expectations.Common.cs that the checks lean on: backup ref names and the server's port in texts.</summary>
public class Phase3ExpectationsTests
{
    [Fact]
    public void A_backup_refs_name_is_taken_apart_into_kind_time_snapshot_and_name()
    {
        var reset = Phase3Expectations.BackupRef.Parse("refs/visualcommit/backup/reset-20260102T090000123Z");
        Assert.Equal(("reset", false, (string?)null), (reset.Kind, reset.IsChanges, reset.Name));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, 123, TimeSpan.Zero), reset.Time);

        var changes = Phase3Expectations.BackupRef.Parse("refs/visualcommit/backup/abort-20260102T090000000Z-changes");
        Assert.Equal(("abort", true, (string?)null), (changes.Kind, changes.IsChanges, changes.Name));

        var tag = Phase3Expectations.BackupRef.Parse("refs/visualcommit/backup/remote-tag-20260102T090000000Z-2/origin/v1.2");
        Assert.Equal(("remote-tag", false, "origin/v1.2"), (tag.Kind, tag.IsChanges, tag.Name));

        var branch = Phase3Expectations.BackupRef.Parse("refs/visualcommit/backup/branch-20260102T090000000Z/feature/ff");
        Assert.Equal(("branch", "feature/ff"), (branch.Kind, branch.Name));

        Assert.Throws<FormatException>(() => Phase3Expectations.BackupRef.Parse("refs/heads/main"));
        Assert.Throws<FormatException>(() => Phase3Expectations.BackupRef.Parse("refs/visualcommit/backup/reset-2026"));
    }

    [Fact]
    public void A_text_with_the_visual_checks_port_takes_another_port_when_the_server_had_to_use_one()
    {
        Assert.Equal("Sign in to 127.0.0.1:50123", Phase3Expectations.WithPort("Sign in to 127.0.0.1:47391", 50123));
        Assert.Equal("Sign in to 127.0.0.1:47391", Phase3Expectations.WithPort("Sign in to 127.0.0.1:47391", AuthRemoteServer.VisualChecksPort));
    }

    [Fact]
    public void The_menu_height_is_4as_formula()
    {
        Assert.Equal(232, Phase3Expectations.MenuHeight(7, 2));
        Assert.Equal(84, Phase3Expectations.MenuHeight(2, 0));
    }

    /// <summary>
    /// The heights phase 4's report gives phase 3's menus ("Phase 3's menus after phase 4") are
    /// 4a's formula for their lists: the constants and the lists agree.
    /// </summary>
    [Fact]
    public void Phase_4s_heights_of_phase_3s_menus_are_4as_formula_for_their_lists()
    {
        static double HeightOf(string[] lines) =>
            Phase3Expectations.MenuHeight(lines.Skip(1).Count(line => line != "—"), lines.Count(line => line == "—"));

        Assert.Equal(Phase3Expectations.PartA.CommitMenuHeight, HeightOf(Phase3Expectations.PartA.AlphaTwoCommitMenu));
        Assert.Equal(284, Phase3Expectations.PartA.CommitMenuHeight);
        Assert.Equal(Phase3Expectations.PartA.FeatureAlphaMenuHeight, HeightOf(Phase3Expectations.PartA.FeatureAlphaMenu));
        Assert.Equal(458, Phase3Expectations.PartA.FeatureAlphaMenuHeight);
        Assert.Equal(Phase3Expectations.PartA.OriginFeatureAlphaMenuHeight, HeightOf(Phase3Expectations.PartA.OriginFeatureAlphaMenu));
        Assert.Equal(293, Phase3Expectations.PartA.OriginFeatureAlphaMenuHeight);
        Assert.Equal(Phase3Expectations.PartA.DropMenuHeight, HeightOf(Phase3Expectations.PartA.AlphaOntoMainDropMenu));
        Assert.Equal(171, Phase3Expectations.PartA.DropMenuHeight);
        Assert.Equal(Phase3Expectations.PartD.AlphaTwoMenuHeight, HeightOf(Phase3Expectations.PartD.AlphaTwoMenu));
        Assert.Equal(284, Phase3Expectations.PartD.AlphaTwoMenuHeight);
        Assert.Equal(354, HeightOf(Phase3Expectations.PartA.MainMenu));
        Assert.Equal(380, HeightOf(Phase3Expectations.PartA.FeatureFfMenu));
        Assert.Equal(293, HeightOf(Phase3Expectations.PartA.LocMenu));
        Assert.Equal(145, HeightOf(Phase3Expectations.PartA.AlphaOntoConflictDropMenu));
        Assert.Equal(145, HeightOf(Phase3Expectations.PartB.ThreeCommitsMenu));
        Assert.Equal(284, HeightOf(Phase3Expectations.PartB.ConflictedFileMenu));
        Assert.Equal(458, HeightOf(Phase3Expectations.PartB.MainMenuWhileDetached));
        Assert.Equal(458, HeightOf(Phase3Expectations.PartC.Check13MainMenu));
        Assert.Equal(145, HeightOf(Phase3Expectations.PartD.WorktreeDropMenu("x")));
    }
}
