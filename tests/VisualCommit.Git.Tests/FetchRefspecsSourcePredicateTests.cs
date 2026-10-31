using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="FetchRefspecs.SourceOf(string, string, Func{string, bool})"/> (ruling 8,
/// docs/phase-7a-design.md 4.7): the candidates the predicate refuses are left out before rules 1
/// to 3, and a predicate that is always true gives the two-argument form's answer.
/// </summary>
public class FetchRefspecsSourcePredicateTests
{
    private static readonly FetchRefspecs Lines = FetchRefspecs.Parse(
    [
        ("origin", "+refs/heads/*:refs/remotes/origin/*"),
        ("origin", "+refs/pull/*/head:refs/remotes/origin/pr/*"),
        ("origin", "^refs/heads/secret"),
        ("upstream", "+refs/pull/*/head:refs/remotes/upstream/pr/*"),
    ]);

    [Theory]
    [InlineData("refs/remotes/origin/pr/fix-typo", "origin")]
    [InlineData("refs/remotes/origin/pr/7", "origin")]
    [InlineData("refs/remotes/origin/main", "origin")]
    [InlineData("refs/remotes/origin/secret", "origin")]
    [InlineData("refs/remotes/upstream/pr/7", "upstream")]
    [InlineData("refs/remotes/upstream/pr/7", "nowhere")]
    public void Always_true_gives_the_two_argument_forms_answer(string trackingRef, string remote)
    {
        Assert.Equal(Describe(Lines.SourceOf(trackingRef, remote)), Describe(Lines.SourceOf(trackingRef, remote, _ => true)));
    }

    [Fact]
    public void A_refused_candidate_leaves_the_other_one_mapped()
    {
        Assert.Equal(RemoteSourceKind.AmbiguousRefs, Lines.SourceOf("refs/remotes/origin/pr/fix-typo", "origin").Kind);

        var source = Lines.SourceOf("refs/remotes/origin/pr/fix-typo", "origin", candidate => candidate != "refs/pull/fix-typo/head");

        Assert.Equal((RemoteSourceKind.Mapped, "origin", "refs/heads/pr/fix-typo"), (source.Kind, source.Remote, source.RemoteRef));
    }

    [Fact]
    public void With_every_candidate_refused_the_ref_is_not_fetched()
    {
        Assert.Equal(RemoteSourceKind.NotFetched, Lines.SourceOf("refs/remotes/upstream/pr/x", "upstream", _ => false).Kind);
        Assert.Equal(RemoteSourceKind.NotFetched, Lines.SourceOf("refs/remotes/origin/pr/fix-typo", "origin", _ => false).Kind);
    }

    private static string Describe(RemoteSource source) => $"{source.Kind} {source.Remote} {source.RemoteRef} {string.Join(',', source.Remotes)}";
}
