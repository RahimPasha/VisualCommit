using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="GitOutputCleaner"/> and <see cref="LockFiles"/> (phase 3 design 1.6 and 1.8).</summary>
public class GitOutputCleanerTests
{
    [Fact]
    public void Progress_lines_and_the_cloning_line_go_and_gits_message_stays_whole()
    {
        var stderr = "Cloning into 'x'...\nremote: Enumerating objects: 5, done.\nremote: Counting objects:  20% (1/5)\rremote: Counting objects: 100% (5/5), done.\n" +
            "Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s\rReceiving objects: 100% (20/20), done.   \n" +
            "To https://example.com/x.git\n ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs to 'https://example.com/x.git'\n" +
            "hint: Updates were rejected because the remote contains work that you do\nhint: not have locally.\n";

        Assert.Equal(
            "remote: Enumerating objects: 5, done.\nTo https://example.com/x.git\n ! [rejected]        main -> main (fetch first)\n" +
            "error: failed to push some refs to 'https://example.com/x.git'\nhint: Updates were rejected because the remote contains work that you do\nhint: not have locally.",
            GitOutputCleaner.Clean(stderr));
    }

    [Fact]
    public void Credentials_are_hidden_and_standard_output_stands_in_for_an_empty_standard_error()
    {
        Assert.Equal(
            "fatal: Authentication failed for 'https://***@example.com/x.git/'",
            GitOutputCleaner.Clean("fatal: Authentication failed for 'https://alice:ghp_SECRET@example.com/x.git/'\r\n"));
        Assert.Equal(
            "Auto-merging a.txt\nCONFLICT (content): Merge conflict in a.txt\nAutomatic merge failed; fix conflicts and then commit the result.",
            GitOutputCleaner.Clean("  \n", "Auto-merging a.txt\nCONFLICT (content): Merge conflict in a.txt\nAutomatic merge failed; fix conflicts and then commit the result.\n"));
        Assert.Equal(string.Empty, GitOutputCleaner.Clean(string.Empty, string.Empty));
    }

    [Theory]
    [InlineData("Receiving objects:  45% (9/20)", true)]
    [InlineData("remote: Compressing objects: 100% (3/3), done.", true)]
    [InlineData("Updating files:  12% (120/1000)", true)]
    [InlineData("fatal: repository 'https://host/my%20repo.git/' not found", false)]
    [InlineData("error: pathspec '50%' did not match any file(s) known to git", false)]
    public void A_progress_line_has_a_stage_a_colon_and_a_percentage(string line, bool progress) =>
        Assert.Equal(progress, GitOutputCleaner.IsProgressLine(line));

    [Fact]
    public void Lock_files_written_since_a_time_are_found_in_every_git_folder_and_older_ones_are_not()
    {
        using var folder = new TempDirectory("locks");
        var git = Directory.CreateDirectory(folder.Combine("repo", ".git", "refs", "heads")).Parent!.Parent!.FullName;
        var common = Directory.CreateDirectory(folder.Combine("common")).FullName;
        var since = DateTimeOffset.UtcNow;
        var old = Path.Combine(git, "index.lock");
        File.WriteAllText(old, string.Empty);
        File.SetLastWriteTimeUtc(old, since.UtcDateTime.AddMinutes(-10));
        var branch = Path.Combine(git, "refs", "heads", "main.lock");
        var packed = Path.Combine(common, "packed-refs.lock");
        File.WriteAllText(branch, string.Empty);
        File.WriteAllText(packed, string.Empty);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");

        var found = LockFiles.FindSince([git, common, folder.Combine("missing")], since);

        Assert.Equal(new[] { branch, packed }.Select(Path.GetFullPath).Order(StringComparer.Ordinal), found);
    }
}
