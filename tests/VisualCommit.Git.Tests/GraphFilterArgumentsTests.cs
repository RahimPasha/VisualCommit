using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The revisions <c>git log</c> gets for what the graph hides or solos (docs/phase-5-design.md 8.2,
/// 0.6): excludes by names relative to their namespace with each remote's <c>HEAD</c>, the soloed
/// full names with HEAD, standard input past 30,000 characters, and nothing new without a filter.
/// That git shows exactly the expected commits for them is tested with the load (WP2a).
/// </summary>
public class GraphFilterArgumentsTests
{
    [Fact]
    public void Without_anything_hidden_or_soloed_the_revisions_are_todays()
    {
        var filter = new GraphFilter([], [], ["origin"]);

        Assert.True(filter.IsEmpty);
        Assert.Equal(["--branches", "--remotes", "--tags", "--ignore-missing", "HEAD", "--"], filter.RevisionArguments);
        Assert.Equal(GraphFilter.Unfiltered, filter.RevisionArguments);
        Assert.Equal(string.Empty, filter.Key);
    }

    [Fact]
    public void Hidden_branches_are_excluded_before_their_namespace_with_their_remotes_HEAD()
    {
        var filter = new GraphFilter(
            ["refs/remotes/origin/feature/alpha", "refs/heads/feature/alpha", "refs/remotes/my/remote/x", "refs/tags/v1"],
            [],
            ["origin", "my", "my/remote"]);

        Assert.Equal(["refs/heads/feature/alpha", "refs/remotes/my/remote/x", "refs/remotes/origin/feature/alpha"], filter.Hidden);
        Assert.Equal(["my/remote", "origin"], filter.RemotesWithHiddenBranches);
        Assert.Equal(
            [
                "--exclude=feature/alpha",
                "--branches",
                "--exclude=my/remote/x",
                "--exclude=origin/feature/alpha",
                "--exclude=my/remote/HEAD",
                "--exclude=origin/HEAD",
                "--remotes",
                "--tags",
                "--ignore-missing",
                "HEAD",
                "--",
            ],
            filter.RevisionArguments);
        Assert.False(filter.UsesStandardInput);
    }

    [Fact]
    public void Solo_names_the_soloed_refs_and_HEAD_and_leaves_the_hidden_ones_for_later()
    {
        var filter = new GraphFilter(["refs/heads/old"], ["refs/remotes/origin/feature/beta", "refs/heads/feature/beta"], ["origin"]);

        Assert.True(filter.IsSolo);
        Assert.Equal(["--ignore-missing", "refs/heads/feature/beta", "refs/remotes/origin/feature/beta", "HEAD", "--"], filter.RevisionArguments);
        Assert.Equal(["refs/heads/old"], filter.Hidden);
    }

    [Fact]
    public void Past_the_command_lines_limit_the_revisions_go_on_standard_input()
    {
        var hidden = Enumerable.Range(0, 1200).Select(i => $"refs/heads/feature/branch-number-{i:D4}").ToList();
        var filter = new GraphFilter(hidden, [], []);

        Assert.True(filter.UsesStandardInput);
        Assert.Equal(["--ignore-missing", "--stdin", "--"], filter.RevisionArguments);

        var head = new HeadState("main", new string('a', 40));
        var refs = new RepoRefs(
            head,
            [
                new GitRef("refs/heads/feature/branch-number-0001", "feature/branch-number-0001", RefKind.LocalBranch, new string('b', 40)),
                new GitRef("refs/heads/main", "main", RefKind.LocalBranch, new string('a', 40)),
                new GitRef("refs/tags/v1", "v1", RefKind.Tag, new string('c', 40)),
            ],
            [],
            []);
        Assert.Equal("refs/heads/main\nrefs/tags/v1\nHEAD\n", filter.StandardInput(refs));

        var solo = new GraphFilter([], [.. hidden], []);
        Assert.True(solo.UsesStandardInput);
        Assert.Equal(string.Concat(hidden.Order(StringComparer.Ordinal).Select(name => name + "\n")) + "HEAD\n", solo.StandardInput(refs));
    }

    [Fact]
    public void Two_filters_are_equal_when_they_load_the_same_graph()
    {
        var one = new GraphFilter(["refs/heads/b", "refs/heads/a", "refs/heads/a"], [], ["origin"]);
        var two = new GraphFilter(["refs/heads/a", "refs/heads/b"], [], []);
        var three = new GraphFilter(["refs/heads/a"], [], []);

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, three);
        Assert.NotEqual(one.Key, three.Key);
    }

    [Fact]
    public void A_remote_branchs_remote_is_the_longest_known_remote_it_starts_with()
    {
        Assert.Equal("my/remote", GraphFilter.RemoteOf("my/remote/x", ["my", "my/remote"]));
        Assert.Equal("origin", GraphFilter.RemoteOf("origin/feature/x", ["origin"]));
        Assert.Equal("unknown", GraphFilter.RemoteOf("unknown/x", []));
    }
}
