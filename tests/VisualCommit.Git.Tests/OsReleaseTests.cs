using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The Linux distribution from <c>os-release</c> (docs/phase-8a-design.md 5.2, 5.3): the first-run
/// screen's package command by <c>ID</c> and <c>ID_LIKE</c>, its name by <c>PRETTY_NAME</c>. Made-up
/// texts in the files' form; no file of the machine is read.
/// </summary>
public class OsReleaseTests
{
    [Fact]
    public void The_ids_and_the_pretty_name_are_read_unquoted()
    {
        var distribution = OsRelease.Parse("""
            PRETTY_NAME="Ubuntu 22.04.5 LTS"
            NAME="Ubuntu"
            VERSION_ID="22.04"
            ID=ubuntu
            ID_LIKE=debian
            """);

        Assert.NotNull(distribution);
        Assert.Equal("ubuntu", distribution.Id);
        Assert.Equal(["debian"], distribution.IdLike);
        Assert.Equal("Ubuntu 22.04.5 LTS", distribution.PrettyName);
    }

    [Fact]
    public void Single_quotes_double_quotes_and_their_escapes_are_undone()
    {
        var distribution = OsRelease.Parse(
            "ID='fedora'\n" +
            "ID_LIKE=\"rhel  centos\"\n" +
            "PRETTY_NAME=\"Demo \\\"Linux\\\" \\$1 \\`x\\` \\\\ end\"\n");

        Assert.NotNull(distribution);
        Assert.Equal("fedora", distribution.Id);
        Assert.Equal(["rhel", "centos"], distribution.IdLike);
        Assert.Equal("Demo \"Linux\" $1 `x` \\ end", distribution.PrettyName);
    }

    [Fact]
    public void Comments_blank_lines_and_line_ends_of_windows_are_skipped()
    {
        var distribution = OsRelease.Parse("# a comment\r\n\r\n  ID=arch  \r\n#ID=debian\r\nBUILD_ID=rolling\r\n");

        Assert.NotNull(distribution);
        Assert.Equal("arch", distribution.Id);
        Assert.Empty(distribution.IdLike);
        Assert.Null(distribution.PrettyName);
    }

    [Fact]
    public void A_later_line_of_a_key_wins()
    {
        var distribution = OsRelease.Parse("ID=debian\nPRETTY_NAME=First\nID=opensuse-leap\nPRETTY_NAME=\"Second\"\n");

        Assert.NotNull(distribution);
        Assert.Equal(("opensuse-leap", "Second"), (distribution.Id, distribution.PrettyName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NAME=Nothing\nPRETTY_NAME=\"No id\"\n")]
    [InlineData("ID=\n")]
    [InlineData("ID=\"\"\n")]
    public void Without_an_id_there_is_no_distribution(string text) => Assert.Null(OsRelease.Parse(text));

    [Fact]
    public void The_usr_lib_file_is_read_when_etc_has_none_and_a_file_that_cannot_be_read_counts_as_none()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/usr/lib/os-release"] = "ID=alpine\n",
        };

        var fromUsrLib = OsRelease.Read(OsRelease.Files, files.ContainsKey, file => files[file]);
        var unreadable = OsRelease.Read(OsRelease.Files, _ => true, file => file == "/etc/os-release" ? throw new IOException("denied") : "ID=centos\n");
        var none = OsRelease.Read(OsRelease.Files, _ => false, _ => throw new InvalidOperationException("Not read."));

        Assert.Equal(["/etc/os-release", "/usr/lib/os-release"], OsRelease.Files);
        Assert.Equal("alpine", fromUsrLib?.Id);
        Assert.Equal("centos", unreadable?.Id);
        Assert.Null(none);
    }
}
