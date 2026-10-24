using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="DirectoryRenames"/>, git's directory-rename detection as Keep ignored files' reads
/// take it (merge-ort's rules, the lab's shapes M1 to M6 on Git 2.36, 2.55 and 2.56): what one
/// <c>git diff-tree -r -z -M --name-status &lt;ours&gt; &lt;theirs&gt;</c> says git may write. Pure, no git.
/// </summary>
public class DirectoryRenamesTests
{
    [Fact]
    public void The_name_status_output_is_read_with_renames_holding_two_paths()
    {
        var entries = DirectoryRenames.Parse("D\0dir/new.txt\0R100\0dir/a.txt\0newdir/a.txt\0M\0a b é.txt\0A\0x\0");

        Assert.Equal(
            [new('D', "dir/new.txt", null), new('R', "dir/a.txt", "newdir/a.txt"), new('M', "a b é.txt", null), new('A', "x", null)],
            entries);
        Assert.Empty(DirectoryRenames.Parse(string.Empty));
    }

    [Fact]
    public void A_renames_levels_are_its_folders_then_each_pair_of_parents_whose_names_agree()
    {
        // merge-ort's update_dir_rename_counts: "a/b/c/d/e/foo.c" -> "a/b/some/thing/else/e/foo.c".
        Assert.Equal([("a/b/c/d/e", "a/b/some/thing/else/e"), ("a/b/c/d", "a/b/some/thing/else")], DirectoryRenames.Levels("a/b/c/d/e/foo.c", "a/b/some/thing/else/e/foo.c"));
        Assert.Equal([("dir", "newdir")], DirectoryRenames.Levels("dir/a.txt", "newdir/a.txt"));
        Assert.Equal([("a/b", "c/b"), ("a", "c")], DirectoryRenames.Levels("a/b/x", "c/b/x"));

        // Into the root, and the root itself never.
        Assert.Equal([("dir", string.Empty)], DirectoryRenames.Levels("dir/a.txt", "a.txt"));
        Assert.Equal([("x/sub", "sub"), ("x", string.Empty)], DirectoryRenames.Levels("x/sub/f", "sub/f"));
        Assert.Empty(DirectoryRenames.Levels("a.txt", "dir/a.txt"));

        // A rename inside its folder renames no folder.
        Assert.Empty(DirectoryRenames.Levels("dir/a.txt", "dir/b.txt"));
    }

    [Fact]
    public void A_directory_the_other_side_renamed_whole_moves_this_sides_new_file_either_way()
    {
        // M1, theirs renamed dir: ours' dir/new.txt, which theirs lacks, goes to newdir.
        Assert.Equal(["newdir/new.txt"], Moved("D dir/new.txt", "R dir/a.txt newdir/a.txt", "R dir/b.txt newdir/b.txt", "R dir/c.txt newdir/c.txt"));

        // M2, ours renamed it: theirs' dir/new.txt, which ours lacks, goes to newdir too.
        Assert.Equal(["newdir/new.txt"], Moved("A dir/new.txt", "R newdir/a.txt dir/a.txt", "R newdir/b.txt dir/b.txt"));

        // Deeper files keep their folders under the new one; into the root (M3).
        Assert.Equal(["newdir/sub/new.txt"], Moved("D dir/sub/new.txt", "R dir/a.txt newdir/a.txt"));
        Assert.Equal(["new.txt"], Moved("D dir/new.txt", "R dir/a.txt a.txt", "R dir/b.txt b.txt"));
    }

    [Fact]
    public void The_most_files_decide_where_a_directory_went_and_a_tie_counts_every_candidate()
    {
        // M4: two of dir's files to n1, one to n2: git moves new.txt to n1 alone.
        Assert.Equal(["n1/new.txt"], Moved("D dir/new.txt", "R dir/a.txt n1/a.txt", "R dir/b.txt n1/b.txt", "R dir/c.txt n2/c.txt"));

        // M5: one each (git renames to neither, "directory rename split"); the read's counts are
        // not git's, which counts against the merge base, so both are asked about.
        Assert.Equal(["n1/new.txt", "n2/new.txt"], Moved("D dir/new.txt", "R dir/a.txt n1/a.txt", "R dir/b.txt n2/b.txt"));
    }

    [Fact]
    public void A_directory_that_still_holds_a_file_on_the_renaming_side_is_not_renamed()
    {
        // M6 as the read can tell it: theirs still has dir/c.txt (changed, or added there).
        Assert.Empty(Moved("D dir/new.txt", "M dir/c.txt", "R dir/a.txt newdir/a.txt"));
        Assert.Empty(Moved("D dir/new.txt", "A dir/other.txt", "R dir/a.txt newdir/a.txt"));

        // A file unchanged on both sides is not in the read: the safe side counts the rename.
        Assert.Equal(["newdir/new.txt"], Moved("D dir/new.txt", "R dir/a.txt newdir/a.txt"));

        // No rename, nothing moves.
        Assert.Empty(Moved("D dir/new.txt", "A newdir/a.txt"));
    }

    private static IReadOnlyList<string> Moved(params string[] entries) =>
        DirectoryRenames.Moved([.. entries.Select(entry =>
        {
            var fields = entry.Split(' ');
            return new DirectoryRenames.Entry(fields[0][0], fields[1], fields.Length > 2 ? fields[2] : null);
        })]);
}
