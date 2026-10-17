using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The labels of <see cref="CommitGraphData"/> under what the graph hides or solos
/// (docs/phase-5-design.md 8.1 to 8.3): a hidden branch's label goes, in solo mode every branch's
/// but the soloed ones'; the checked-out branch keeps its label, tags and stashes keep theirs,
/// and <see cref="CommitGraphData.Refs"/> stays whole. The rows a filter loads are tested against
/// git in <c>GraphFilterTests</c>.
/// </summary>
public class CommitGraphDataTests
{
    private static readonly DateTimeOffset Date = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// main (checked out) on "m"; feature/alpha on "a" with its upstream origin/feature/alpha; a
    /// local branch named origin/x and the remote branch origin/x on "x"; the tag v1 on "m"; the
    /// tag t on "a"; a stash on "m".
    /// </summary>
    private static readonly RepoRefs AllRefs = new(
        new HeadState("main", "m"),
        [
            new GitRef("refs/heads/feature/alpha", "feature/alpha", RefKind.LocalBranch, "a", Upstream: "origin/feature/alpha"),
            new GitRef("refs/heads/main", "main", RefKind.LocalBranch, "m", IsHead: true),
            new GitRef("refs/heads/origin/x", "origin/x", RefKind.LocalBranch, "x"),
            new GitRef("refs/remotes/origin/feature/alpha", "origin/feature/alpha", RefKind.RemoteBranch, "a", RemoteName: "origin"),
            new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, "m", RemoteName: "origin"),
            new GitRef("refs/remotes/origin/x", "origin/x", RefKind.RemoteBranch, "x", RemoteName: "origin"),
            new GitRef("refs/tags/t", "t", RefKind.Tag, "a"),
            new GitRef("refs/tags/v1", "v1", RefKind.Tag, "m"),
        ],
        [new StashEntry(0, "s", "m", "On main: Work", "Test Author", "author@example.com", Date, Date)],
        ["origin"]);

    [Fact]
    public void Without_a_filter_every_ref_and_stash_has_its_label()
    {
        var data = Load(null);

        Assert.Equal(["CurrentBranch main", "RemoteBranch origin/main", "Tag v1"], Labels(data, "m"));
        Assert.Equal(["LocalBranch feature/alpha", "RemoteBranch origin/feature/alpha", "Tag t"], Labels(data, "a"));
        Assert.Equal(["LocalBranch origin/x", "RemoteBranch origin/x"], Labels(data, "x"));
        Assert.Equal(["Stash stash@{0}"], Labels(data, "s"));
    }

    [Fact]
    public void A_filter_that_hides_and_solos_nothing_leaves_every_label()
    {
        var plain = Load(null);
        var data = Load(new GraphFilter([], [], ["origin"]));

        foreach (var sha in new[] { "s", "m", "a", "x" })
        {
            Assert.Equal(Labels(plain, sha), Labels(data, sha));
        }
    }

    [Fact]
    public void A_hidden_branch_loses_its_label_and_tags_keep_theirs()
    {
        var data = Load(new GraphFilter(["refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha"], [], ["origin"]));

        Assert.Equal(["Tag t"], Labels(data, "a"));
        Assert.Equal(["CurrentBranch main", "RemoteBranch origin/main", "Tag v1"], Labels(data, "m"));
        Assert.Same(AllRefs, data.Refs);
    }

    [Fact]
    public void Full_names_tell_a_local_branch_from_the_remote_branch_of_the_same_name()
    {
        var hideLocal = Load(new GraphFilter(["refs/heads/origin/x"], [], ["origin"]));
        var hideRemote = Load(new GraphFilter(["refs/remotes/origin/x"], [], ["origin"]));

        Assert.Equal(["RemoteBranch origin/x"], Labels(hideLocal, "x"));
        Assert.Equal(["LocalBranch origin/x"], Labels(hideRemote, "x"));
    }

    [Fact]
    public void The_checked_out_branch_keeps_its_label_while_hidden_and_its_upstream_does_not()
    {
        var data = Load(new GraphFilter(["refs/heads/main", "refs/remotes/origin/main"], [], ["origin"]));

        Assert.Equal(["CurrentBranch main", "Tag v1"], Labels(data, "m"));
        Assert.Equal(["Stash stash@{0}"], Labels(data, "s"));
    }

    [Fact]
    public void Solo_labels_only_the_soloed_branches_and_the_checked_out_one_and_every_tag()
    {
        var data = Load(new GraphFilter(["refs/heads/origin/x"], ["refs/heads/feature/alpha"], ["origin"]));

        Assert.Equal(["LocalBranch feature/alpha", "Tag t"], Labels(data, "a"));
        Assert.Equal(["CurrentBranch main", "Tag v1"], Labels(data, "m"));
        Assert.Empty(Labels(data, "x"));
        Assert.Equal(["Stash stash@{0}"], Labels(data, "s"));
    }

    [Fact]
    public void A_detached_HEAD_keeps_its_label_under_a_filter()
    {
        var detached = AllRefs with { Head = new HeadState(null, "a"), Refs = [.. AllRefs.Refs.Select(gitRef => gitRef with { IsHead = false })] };
        var data = Load(new GraphFilter([], ["refs/heads/origin/x"], ["origin"]), detached);

        Assert.Equal(["DetachedHead HEAD", "Tag t"], Labels(data, "a"));
        Assert.Equal(["Tag v1"], Labels(data, "m"));
        Assert.Equal(["LocalBranch origin/x"], Labels(data, "x"));
    }

    /// <summary>The graph of <see cref="AllRefs"/> (or <paramref name="refs"/>) loaded with <paramref name="filter"/>: the stash, then "m", "a" and "x", each a root.</summary>
    private static CommitGraphData Load(GraphFilter? filter, RepoRefs? refs = null)
    {
        var data = new CommitGraphData(refs ?? AllRefs, filter);
        var layout = new GraphLayout();
        CommitInfo[] commits =
        [
            new("s", ["m"], "Test Author", "author@example.com", Date, Date, "On main: Work", CommitKind.Stash),
            .. new[] { "m", "a", "x" }.Select(sha => new CommitInfo(sha, [], "Test Author", "author@example.com", Date, Date, sha)),
        ];
        data.Append(commits, [.. commits.Select(layout.Add)]);
        data.Complete();
        return data;
    }

    private static List<string> Labels(CommitGraphData data, string sha) =>
        [.. data.LabelsAt(data.IndexOf(sha)).Select(label => $"{label.Kind} {label.Text}")];
}
