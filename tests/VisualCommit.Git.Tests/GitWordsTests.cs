using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitWords"/>: git's words on both sides of the versions that changed them (D420), the
/// floor's 2.34.1 against 2.35 and 2.36, as git's sources at their tags write them, and the version
/// of the git the tests run.
/// </summary>
public class GitWordsTests
{
    private static readonly GitVersion Floor = new(2, 34, 1);
    private static readonly GitVersion Git235 = new(2, 35, 0);
    private static readonly GitVersion Git2352 = new(2, 35, 2);
    private static readonly GitVersion Git236 = new(2, 36, 0);

    [Fact]
    public void The_predicates_change_at_2_35_and_2_36()
    {
        Assert.Equal([false, true, true], new[] { Floor, Git235, Git236 }.Select(GitWords.HasZdiff3));
        Assert.Equal([false, true, true], new[] { Floor, Git235, Git236 }.Select(GitWords.FetchGuardsEveryWorktree));
        Assert.Equal([false, false, true], new[] { Floor, Git235, Git236 }.Select(GitWords.StashNamesTheWholeBranch));
    }

    [Fact]
    public void A_stash_names_the_part_after_the_last_slash_below_2_36_and_the_whole_branch_from_it()
    {
        Assert.Equal("alpha", GitWords.StashBranch("feature/alpha", Floor));
        Assert.Equal("alpha", GitWords.StashBranch("feature/alpha", Git2352));
        Assert.Equal("feature/alpha", GitWords.StashBranch("feature/alpha", Git236));
        Assert.Equal("main", GitWords.StashBranch("main", Floor));
        Assert.Equal("main", GitWords.StashBranch("main", Git236));
        Assert.Equal("c", GitWords.StashBranch("a/b/c", Floor));
        Assert.Equal("a/b/c", GitWords.StashBranch("a/b/c", Git236));
    }

    [Fact]
    public void Check_14s_stash_line_changes_only_in_its_branch()
    {
        const string Line = "stash@{0}: On feature/other: VisualCommit: before checking out feature/clash";

        Assert.Equal("stash@{0}: On other: VisualCommit: before checking out feature/clash", GitWords.StashText(Line, Floor));
        Assert.Equal(Line, GitWords.StashText(Line, Git236));
    }

    [Fact]
    public void Check_23s_graph_row_and_undo_labels_and_U18s_stash_take_the_last_part()
    {
        const string Row = "??????? On topic/new: Toolbar stash [Stash:stash@{0}] 0/0";
        const string Undo = "Undo stash \"On topic/new: Toolbar stash\"";
        const string UndoPop = "Undo pop of stash \"On topic/new: Toolbar stash\"";
        const string Wip = "WIP on feature/other: 850a7c0 Other change";

        Assert.Equal("??????? On new: Toolbar stash [Stash:stash@{0}] 0/0", GitWords.StashText(Row, Floor));
        Assert.Equal("Undo stash \"On new: Toolbar stash\"", GitWords.StashText(Undo, Floor));
        Assert.Equal("Undo pop of stash \"On new: Toolbar stash\"", GitWords.StashText(UndoPop, Floor));
        Assert.Equal("WIP on other: 850a7c0 Other change", GitWords.StashText(Wip, Floor));
        Assert.Equal([Row, Undo, UndoPop, Wip], new[] { Row, Undo, UndoPop, Wip }.Select(text => GitWords.StashText(text, Git236)));
    }

    [Fact]
    public void A_stash_texts_index_and_untracked_commits_and_a_name_with_a_space_are_read_as_git_writes_them()
    {
        Assert.Equal("index on b: 1111111 Base", GitWords.StashText("index on a/b: 1111111 Base", Floor));
        Assert.Equal("untracked files on b: 1111111 Base", GitWords.StashText("untracked files on a/b: 1111111 Base", Floor));
        Assert.Equal("On main: Experiment", GitWords.StashText("On main: Experiment", Floor));

        // Not at the text's start or after a space or a quote, or no branch (a name with a space;
        // detached, "(no branch)"): unchanged.
        Assert.Equal("XOn a/b: done", GitWords.StashText("XOn a/b: done", Floor));
        Assert.Equal("On a b/c: done", GitWords.StashText("On a b/c: done", Floor));
        Assert.Equal("WIP on (no branch): 1111111 Base", GitWords.StashText("WIP on (no branch): 1111111 Base", Floor));
    }

    [Fact]
    public void Tracking_set_up_reads_as_each_git_prints_it()
    {
        Assert.Equal("Branch 'alpha-copy' set up to track remote branch 'feature/alpha' from 'origin'.", GitWords.TrackingSetUp("alpha-copy", "origin", "feature/alpha", Floor));
        Assert.Equal("branch 'alpha-copy' set up to track 'origin/feature/alpha'.", GitWords.TrackingSetUp("alpha-copy", "origin", "feature/alpha", Git235));
        Assert.Equal("branch 'alpha-copy' set up to track 'origin/feature/alpha'.", GitWords.TrackingSetUp("alpha-copy", "origin", "feature/alpha", Git236));
    }

    [Fact]
    public async Task The_version_is_the_one_git_reports()
    {
        using var repo = await TempRepo.CreateAsync();
        Assert.True(GitVersion.TryParse((await repo.GitAsync("--version")).StandardOutput, out var reported));

        Assert.Equal(reported, await GitWords.VersionAsync());
        Assert.Same(GitWords.VersionAsync(), GitWords.VersionAsync());
    }
}
