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
}
