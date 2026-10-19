using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="FetchRefspecs"/>, <see cref="FetchRefspec.Parse"/> and <see cref="RepoRefs"/>' mapping
/// (docs/phase-6-design.md 10.1, 10.2, 10.6), without git: every line form and case of 0.5, whose
/// git behaviour <see cref="FetchRefspecsParityTests"/> checks against git itself.
/// </summary>
public class FetchRefspecsTests
{
    private const string Default = "+refs/heads/*:refs/remotes/origin/*";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>Lines git takes (0.5's line forms, each checked with Git 2.36): the value, then the source, destination, negative and force read from it.</summary>
    public static TheoryData<string, string, string?, bool, bool> ValidLines => new()
    {
        { Default, "refs/heads/*", "refs/remotes/origin/*", false, true },
        { "refs/heads/*:refs/remotes/origin/*", "refs/heads/*", "refs/remotes/origin/*", false, false },
        { "refs/heads/main:refs/remotes/origin/trunk", "refs/heads/main", "refs/remotes/origin/trunk", false, false },
        { "+refs/pull/*/head:refs/remotes/origin/pr/*", "refs/pull/*/head", "refs/remotes/origin/pr/*", false, true },
        { "+refs/*:refs/*", "refs/*", "refs/*", false, true },
        { "*:refs/remotes/origin/*", "*", "refs/remotes/origin/*", false, false },
        { "refs/heads/*/x:refs/remotes/origin/*/y", "refs/heads/*/x", "refs/remotes/origin/*/y", false, false },
        { "refs/heads/f*:refs/remotes/origin/g*", "refs/heads/f*", "refs/remotes/origin/g*", false, false },
        { "main:refs/remotes/origin/m", "main", "refs/remotes/origin/m", false, false },
        { "main:x", "main", "x", false, false },
        { "@:refs/remotes/origin/h", "HEAD", "refs/remotes/origin/h", false, false },
        { Sha + ":refs/remotes/origin/c", Sha, "refs/remotes/origin/c", false, false },

        // No destination, or an empty one: fetched into FETCH_HEAD only, mapping nothing.
        { "refs/heads/main", "refs/heads/main", null, false, false },
        { "refs/heads/main:", "refs/heads/main", null, false, false },

        // Negative lines: a full name or a pattern; "^secret" and "^+refs/heads/secret" are taken and exclude nothing.
        { "^refs/heads/secret", "refs/heads/secret", null, true, false },
        { "^refs/heads/team/*", "refs/heads/team/*", null, true, false },
        { "^secret", "secret", null, true, false },
        { "^+refs/heads/secret", "+refs/heads/secret", null, true, false },
        { "^@", "HEAD", null, true, false },
    };

    /// <summary>
    /// Lines git refuses with <c>fatal: invalid refspec</c> (each checked with Git 2.36), and the
    /// lines git takes that name no source and are left out (an empty value, an empty source).
    /// </summary>
    public static TheoryData<string> NoLines =>
    [
        // Git takes these, and they map nothing (10.1).
        "", "+", ":", ":refs/remotes/origin/x", "+:refs/remotes/origin/x",

        // A negative line with a destination, "+^", an empty or commit-id negative line.
        "^refs/heads/secret:refs/remotes/origin/secret", "+^refs/heads/secret", "^", "^" + Sha, "^" + Sha.ToUpperInvariant(),

        // A pattern on one side only, two stars, a pattern without a destination.
        "refs/heads/*:refs/remotes/origin/x", "refs/heads/x:refs/remotes/origin/*", "refs/heads/*/*:refs/remotes/origin/*/*",
        "refs/heads/*x*:refs/remotes/origin/*", "+refs/heads/*:refs/remotes/origin/**", "refs/heads/*", "refs/heads/*:", "^refs/heads/*/*",

        // Sides that are no ref names; a value split at its last colon leaves one in the source.
        "refs/heads/a b:refs/remotes/origin/a", "refs/heads/a..b:refs/remotes/origin/x", "refs/heads/main:refs/remotes/origin/x.lock",
        "refs/heads/main:@", "refs/heads/main:refs/remotes//x", "refs/heads/main:refs/remotes/origin/a:b", "refs/heads/main::refs/remotes/origin/x",
        "refs/heads/ma~in:refs/remotes/origin/x", "/refs/heads/main:refs/remotes/origin/x", "refs/heads/main/:refs/remotes/origin/x",
        "refs/heads/.main:refs/remotes/origin/x", "refs/heads/main.:refs/remotes/origin/x", "refs/heads/a@{b:refs/remotes/origin/x",
        "refs/heads/*.lock:refs/remotes/origin/*", "^refs/heads/a b",
    ];

    [Theory]
    [MemberData(nameof(ValidLines))]
    public void Parse_reads_a_line_git_takes(string value, string source, string? destination, bool negative, bool force) =>
        Assert.Equal(new FetchRefspec("origin", source, destination, negative, force), FetchRefspec.Parse("origin", value));

    [Theory]
    [MemberData(nameof(NoLines))]
    public void Parse_gives_no_line_for_one_git_refuses_or_one_without_a_source(string value) =>
        Assert.Null(FetchRefspec.Parse("origin", value));

    [Fact]
    public void Parse_of_a_configuration_keeps_the_lines_in_order_and_leaves_out_those_it_cannot_read()
    {
        var refspecs = FetchRefspecs.Parse(
        [
            ("origin", Default),
            ("origin", "refs/heads/*:refs/remotes/origin/x"),
            ("Team.Dot.x", "+refs/heads/*:refs/remotes/Team.Dot.x/*"),
            ("origin", ""),
            ("origin", "^refs/heads/secret"),
        ]);

        Assert.Equal(
            [
                new FetchRefspec("origin", "refs/heads/*", "refs/remotes/origin/*", false, true),
                new FetchRefspec("Team.Dot.x", "refs/heads/*", "refs/remotes/Team.Dot.x/*", false, true),
                new FetchRefspec("origin", "refs/heads/secret", null, true, false),
            ],
            refspecs.Lines);
    }

    /// <summary>
    /// 0.5's cases by name: the remote's lines, a tracking ref, and where it is fetched from. The
    /// lines are <c>remote=value</c>; the result is the kind, the remote and the ref on it, or for
    /// two remotes the remotes in configuration order.
    /// </summary>
    public static TheoryData<string, string[], string, RemoteSourceKind, string?, string?> Cases => new()
    {
        // The default line, with and without "+": the identity.
        { "default", ["origin=" + Default], "refs/remotes/origin/feature/x", RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x" },
        { "default without +", ["origin=refs/heads/*:refs/remotes/origin/*"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },

        // B1 renaming, B2 another namespace, C1 one ref, D a prefix: renamed branches.
        { "B1", ["origin=+refs/heads/*:refs/remotes/origin/up/*"], "refs/remotes/origin/up/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "B1 in a folder", ["origin=+refs/heads/*:refs/remotes/origin/up/*"], "refs/remotes/origin/up/feature/x", RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x" },
        { "B2", ["origin=+refs/heads/*:refs/remotes/mirror/*"], "refs/remotes/mirror/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "C1", ["origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "C1, a ref no line names", ["origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/main", RemoteSourceKind.NotFetched, null, null },
        { "D", ["origin=+refs/heads/team/*:refs/remotes/origin/*"], "refs/remotes/origin/a", RemoteSourceKind.Mapped, "origin", "refs/heads/team/a" },

        // C2 two lines of one remote, in both orders: ambiguous, where git takes the first.
        { "C2", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "origin", null },
        { "C2 swapped", ["origin=refs/heads/main:refs/remotes/origin/trunk", "origin=" + Default], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "origin", null },
        { "C2, its other ref", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },

        // N an excluded candidate, in both orders: the remote maps nothing.
        { "N", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk", "origin=^refs/heads/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.NotFetched, null, null },
        { "N swapped", ["origin=refs/heads/main:refs/remotes/origin/trunk", "origin=" + Default, "origin=^refs/heads/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.NotFetched, null, null },
        { "N negative first", ["origin=^refs/heads/trunk", "origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.NotFetched, null, null },
        { "N, another ref", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk", "origin=^refs/heads/trunk"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },

        // E negative, a negative pattern, and an exact line whose source a negative line names
        // (stricter than git's tracking setup, which maps it, though the fetch leaves it alone).
        { "E", ["origin=" + Default, "origin=^refs/heads/secret"], "refs/remotes/origin/secret", RemoteSourceKind.NotFetched, null, null },
        { "E, another ref", ["origin=" + Default, "origin=^refs/heads/secret"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "a negative pattern", ["origin=" + Default, "origin=^refs/heads/team/*"], "refs/remotes/origin/team/a", RemoteSourceKind.NotFetched, null, null },
        { "a negative short name", ["origin=" + Default, "origin=^secret"], "refs/remotes/origin/secret", RemoteSourceKind.Mapped, "origin", "refs/heads/secret" },
        { "an exact line excluded", ["origin=refs/heads/secret:refs/remotes/origin/s", "origin=^refs/heads/secret"], "refs/remotes/origin/s", RemoteSourceKind.NotFetched, null, null },
        { "a negative line of another remote", ["origin=" + Default, "backup=^refs/heads/secret"], "refs/remotes/origin/secret", RemoteSourceKind.Mapped, "origin", "refs/heads/secret" },

        // P GitHub's pull requests beside the default: ambiguous; P2 the pull-request line alone: not a branch.
        { "P", ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "refs/remotes/origin/pr/1", RemoteSourceKind.AmbiguousRefs, "origin", null },
        { "P, a branch", ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "P2", ["origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "refs/remotes/origin/pr/1", RemoteSourceKind.Mapped, "origin", "refs/pull/1/head" },

        // G no line; a namespace no line fetches.
        { "G", [], "refs/remotes/origin/main", RemoteSourceKind.NotFetched, null, null },
        { "a namespace no line fetches", ["origin=" + Default], "refs/remotes/old/main", RemoteSourceKind.NotFetched, null, null },

        // H two remotes, T nested names: ambiguous between remotes, in configuration order.
        { "H", ["origin=" + Default, "backup=" + Default], "refs/remotes/origin/main", RemoteSourceKind.AmbiguousRemotes, "origin,backup", null },
        { "T", ["origin=" + Default, "team=+refs/heads/*:refs/remotes/team/*", "team/fork=+refs/heads/*:refs/remotes/team/fork/*"], "refs/remotes/team/fork/main", RemoteSourceKind.AmbiguousRemotes, "team,team/fork", null },
        { "T, the shorter name's own", ["origin=" + Default, "team=+refs/heads/*:refs/remotes/team/*", "team/fork=+refs/heads/*:refs/remotes/team/fork/*"], "refs/remotes/team/main", RemoteSourceKind.Mapped, "team", "refs/heads/main" },
        { "H with one excluded", ["origin=" + Default, "backup=" + Default, "backup=^refs/heads/main"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },
        { "H with one ambiguous", ["origin=" + Default, "backup=" + Default, "backup=refs/heads/x:refs/remotes/origin/main"], "refs/remotes/origin/main", RemoteSourceKind.AmbiguousRemotes, "origin,backup", null },

        // O2 a short source, taken as it is: mapped, not a branch.
        { "O2", ["origin=main:refs/remotes/origin/m"], "refs/remotes/origin/m", RemoteSourceKind.Mapped, "origin", "main" },

        // A star inside a component and in the middle; the text between may be empty.
        { "f* to g*", ["origin=refs/heads/f*:refs/remotes/origin/g*"], "refs/remotes/origin/geature/x", RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x" },
        { "f* to g*, nothing between", ["origin=refs/heads/f*:refs/remotes/origin/g*"], "refs/remotes/origin/g", RemoteSourceKind.Mapped, "origin", "refs/heads/f" },
        { "*/x to */y", ["origin=refs/heads/*/x:refs/remotes/origin/*/y"], "refs/remotes/origin/feature/y", RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x" },
        { "*/x to */y, no match", ["origin=refs/heads/*/x:refs/remotes/origin/*/y"], "refs/remotes/origin/feature/x", RemoteSourceKind.NotFetched, null, null },

        // The same source twice (a second section of the remote) is one candidate.
        { "the default twice", ["origin=" + Default, "origin=refs/heads/*:refs/remotes/origin/*"], "refs/remotes/origin/main", RemoteSourceKind.Mapped, "origin", "refs/heads/main" },

        // An empty source and an empty destination map nothing (git's tracking setup takes the
        // empty source for a branch with an empty name).
        { "an empty source", ["origin=:refs/remotes/origin/x"], "refs/remotes/origin/x", RemoteSourceKind.NotFetched, null, null },
        { "an empty source first", ["origin=:refs/remotes/origin/x", "origin=" + Default], "refs/remotes/origin/x", RemoteSourceKind.Mapped, "origin", "refs/heads/x" },
        { "an empty destination", ["origin=refs/heads/main:"], "refs/remotes/origin/main", RemoteSourceKind.NotFetched, null, null },
        { "no destination", ["origin=refs/heads/main"], "refs/remotes/origin/main", RemoteSourceKind.NotFetched, null, null },

        // A remote whose name keeps its case and dots.
        { "a dotted name", ["Team.Dot.x=+refs/heads/*:refs/remotes/Team.Dot.x/*"], "refs/remotes/Team.Dot.x/main", RemoteSourceKind.Mapped, "Team.Dot.x", "refs/heads/main" },
        { "another case", ["Team.Dot.x=+refs/heads/*:refs/remotes/Team.Dot.x/*"], "refs/remotes/team.dot.x/main", RemoteSourceKind.NotFetched, null, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void SourceOf_maps_a_tracking_ref_back_as_git_does_and_is_stricter_for_two_refs_of_one_remote(
        string name, string[] lines, string trackingRef, RemoteSourceKind kind, string? remote, string? remoteRef)
    {
        var source = Lines(lines).SourceOf(trackingRef);

        Assert.True(kind == source.Kind, $"{name}: {source.Kind}, expected {kind}");
        if (kind == RemoteSourceKind.AmbiguousRemotes)
        {
            Assert.Equal(remote!.Split(','), source.Remotes);
            Assert.Null(source.Remote);
        }
        else
        {
            Assert.Equal(remote, source.Remote);
            Assert.Empty(source.Remotes);
        }

        Assert.Equal(remoteRef, source.RemoteRef);
    }

    [Theory]
    [InlineData("refs/heads/main", true, "main")]
    [InlineData("refs/heads/feature/x", true, "feature/x")]
    [InlineData("refs/pull/1/head", false, null)]
    [InlineData("main", false, null)]
    [InlineData("refs/heads/", false, null)]
    public void A_mapped_source_is_a_branch_only_under_refs_heads(string remoteRef, bool isBranch, string? branch)
    {
        var source = new RemoteSource(RemoteSourceKind.Mapped, "origin", remoteRef, []);

        Assert.Equal((isBranch, branch), (source.IsBranch, source.Branch));
    }

    [Fact]
    public void Only_a_mapped_source_is_a_branch()
    {
        Assert.False(new RemoteSource(RemoteSourceKind.AmbiguousRefs, "origin", null, []).IsBranch);
        Assert.False(new RemoteSource(RemoteSourceKind.AmbiguousRemotes, null, null, ["origin", "backup"]).IsBranch);
        Assert.False(new RemoteSource(RemoteSourceKind.NotFetched, null, null, []).IsBranch);
        Assert.Null(new RemoteSource(RemoteSourceKind.NotFetched, null, null, []).Branch);
    }

    [Fact]
    public void IsIdentity_holds_for_the_default_lines_mapping_only()
    {
        var identity = Lines(["origin=" + Default]);
        var renamed = Lines(["origin=+refs/heads/*:refs/remotes/origin/up/*"]);
        var prefix = Lines(["origin=+refs/heads/team/*:refs/remotes/origin/*"]);

        Assert.True(identity.SourceOf("refs/remotes/origin/feature/x").IsIdentity("refs/remotes/origin/feature/x"));
        Assert.False(renamed.SourceOf("refs/remotes/origin/up/main").IsIdentity("refs/remotes/origin/up/main"));
        Assert.False(prefix.SourceOf("refs/remotes/origin/a").IsIdentity("refs/remotes/origin/a"));
        Assert.False(Lines(["origin=+refs/pull/*/head:refs/remotes/origin/pr/*"]).SourceOf("refs/remotes/origin/pr/1").IsIdentity("refs/remotes/origin/pr/1"));
        Assert.False(new RemoteSource(RemoteSourceKind.NotFetched, null, null, []).IsIdentity("refs/remotes/origin/main"));
    }

    /// <summary>The forward mapping (10.1): a remote, a ref on it, and the tracking refs its fetch fills.</summary>
    public static TheoryData<string[], string, string, string[]> Destinations => new()
    {
        { ["origin=" + Default], "origin", "refs/heads/main", ["refs/remotes/origin/main"] },
        { ["origin=" + Default], "backup", "refs/heads/main", [] },
        { ["origin=+refs/heads/*:refs/remotes/origin/up/*"], "origin", "refs/heads/main", ["refs/remotes/origin/up/main"] },
        { ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "origin", "refs/heads/main", ["refs/remotes/origin/main", "refs/remotes/origin/trunk"] },
        { ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "origin", "refs/heads/trunk", ["refs/remotes/origin/trunk"] },
        { ["origin=+refs/heads/team/*:refs/remotes/origin/*"], "origin", "refs/heads/team/a", ["refs/remotes/origin/a"] },
        { ["origin=+refs/heads/team/*:refs/remotes/origin/*"], "origin", "refs/heads/main", [] },
        { ["origin=" + Default, "origin=^refs/heads/secret"], "origin", "refs/heads/secret", [] },
        { ["origin=" + Default, "origin=^refs/heads/team/*"], "origin", "refs/heads/team/a", [] },
        { ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "origin", "refs/heads/pr/1", ["refs/remotes/origin/pr/1"] },
        { ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "origin", "refs/pull/1/head", ["refs/remotes/origin/pr/1"] },
        { ["origin=" + Default, "origin=refs/heads/*:refs/remotes/origin/*"], "origin", "refs/heads/main", ["refs/remotes/origin/main"] },
        { ["origin=refs/heads/main"], "origin", "refs/heads/main", [] },
        { ["origin=refs/heads/*/x:refs/remotes/origin/*/y"], "origin", "refs/heads/feature/x", ["refs/remotes/origin/feature/y"] },
    };

    [Theory]
    [MemberData(nameof(Destinations))]
    public void DestinationsOf_gives_the_tracking_refs_a_fetch_fills_from_a_ref(string[] lines, string remote, string remoteRef, string[] expected) =>
        Assert.Equal(expected, Lines(lines).DestinationsOf(remote, remoteRef));

    [Theory]
    [InlineData("refs/remotes/origin/main")]
    [InlineData("refs/remotes/origin/feature/x")]
    [InlineData("refs/remotes/team/fork/main")]
    [InlineData("refs/remotes/team/fork/feature/x")]
    [InlineData("refs/remotes/upstream/team/fork/x")]
    [InlineData("refs/remotes/a/b/c/main")]
    public void Default_maps_as_phase_5_split_names_for_remote_names_with_slashes(string trackingRef)
    {
        // No remote's name starts another's, so the default lines map every ref as phase 5's
        // split did: the longest configured name, and the rest as the branch.
        string[] remotes = ["origin", "team/fork", "upstream", "a/b/c"];

        var mapped = FetchRefspecs.Default(remotes).SourceOf(trackingRef);
        var assumed = new RepoRefs(new HeadState("main", null), [], [], remotes).SourceOf(trackingRef);

        Assert.Equal(RemoteSourceKind.Mapped, mapped.Kind);
        Assert.Equal((assumed.Kind, assumed.Remote, assumed.RemoteRef), (mapped.Kind, mapped.Remote, mapped.RemoteRef));
        Assert.True(mapped.IsIdentity(trackingRef));
    }

    [Fact]
    public void Default_with_one_remote_name_starting_another_is_ambiguous_where_phase_5_took_the_longest()
    {
        // T: git's tracking setup refuses team/fork/main ("not tracking: ambiguous information").
        string[] remotes = ["origin", "team", "team/fork"];

        var mapped = FetchRefspecs.Default(remotes).SourceOf("refs/remotes/team/fork/main");
        var assumed = new RepoRefs(new HeadState("main", null), [], [], remotes).SourceOf("refs/remotes/team/fork/main");

        Assert.Equal(RemoteSourceKind.AmbiguousRemotes, mapped.Kind);
        Assert.Equal(["team", "team/fork"], mapped.Remotes);
        Assert.Equal((RemoteSourceKind.Mapped, "team/fork", "refs/heads/main"), (assumed.Kind, assumed.Remote, assumed.RemoteRef));
        Assert.Equal(
            [new FetchRefspec("origin", "refs/heads/*", "refs/remotes/origin/*", false, true), new FetchRefspec("team", "refs/heads/*", "refs/remotes/team/*", false, true), new FetchRefspec("team/fork", "refs/heads/*", "refs/remotes/team/fork/*", false, true)],
            FetchRefspecs.Default(remotes).Lines);
    }

    [Fact]
    public void RepoRefs_without_refspecs_keeps_phase_5s_assumption()
    {
        // Every snapshot a test builds by hand: Mapped to the longest configured remote (else the
        // first part) and refs/heads/<the rest>, for a name and for a remote branch alike.
        var origin = RemoteBranch("refs/remotes/origin/feature/x", "origin");
        var fork = RemoteBranch("refs/remotes/team/fork/main", "team/fork");
        var gone = RemoteBranch("refs/remotes/gone/topic", "gone");
        var refs = new RepoRefs(new HeadState("main", null), [origin, fork, gone], [], ["origin", "team", "team/fork"]);

        Assert.Null(refs.Refspecs);
        AssertSource((RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x"), refs.SourceOf("refs/remotes/origin/feature/x"));
        AssertSource((RemoteSourceKind.Mapped, "team/fork", "refs/heads/main"), refs.SourceOf("refs/remotes/team/fork/main"));
        AssertSource((RemoteSourceKind.Mapped, "team", "refs/heads/main"), refs.SourceOf("refs/remotes/team/main"));
        AssertSource((RemoteSourceKind.Mapped, "gone", "refs/heads/topic"), refs.SourceOf("refs/remotes/gone/topic"));
        AssertSource((RemoteSourceKind.NotFetched, null, null), refs.SourceOf("refs/heads/main"));
        AssertSource((RemoteSourceKind.Mapped, "origin", "refs/heads/feature/x"), refs.SourceOf(origin));
        AssertSource((RemoteSourceKind.Mapped, "team/fork", "refs/heads/main"), refs.SourceOf(fork));
        AssertSource((RemoteSourceKind.Mapped, "gone", "refs/heads/topic"), refs.SourceOf(gone));
        AssertSource((RemoteSourceKind.NotFetched, null, null), refs.SourceOf(new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Sha)));
        Assert.True(refs.SourceOf(origin).IsIdentity(origin.FullName));

        // The local names phase 5 gave: the name less "<remote>/".
        Assert.Equal("feature/x", refs.LocalNameOf(origin));
        Assert.Equal("main", refs.LocalNameOf(fork));
        Assert.Equal("topic", refs.LocalNameOf(gone));
        Assert.Null(refs.LocalNameOf(new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Sha)));
    }

    [Fact]
    public void RepoRefs_with_refspecs_maps_by_them_and_names_the_local_branch_by_kind()
    {
        // A remote of each case of 0.5: B1 (origin), D (team), P (pulls), P2 (upstream), H (mirror
        // and backup), E (secret); each ref's RemoteName as the refs read gives it (10.3).
        var renamed = RemoteBranch("refs/remotes/origin/up/main", "origin");
        var prefixed = RemoteBranch("refs/remotes/team/a", "team");
        var pullRequest = RemoteBranch("refs/remotes/upstream/pr/7", "upstream");
        var ambiguousRefs = RemoteBranch("refs/remotes/pulls/pr/1", "pulls");
        var ambiguousRemotes = RemoteBranch("refs/remotes/mirror/main", "mirror");
        var notFetched = RemoteBranch("refs/remotes/secret/hidden", "secret");
        var refs = new RepoRefs(
            new HeadState("main", null),
            [renamed, prefixed, pullRequest, ambiguousRefs, ambiguousRemotes, notFetched],
            [],
            ["backup", "mirror", "origin", "pulls", "secret", "team", "upstream"])
        {
            Refspecs = Lines(
            [
                "origin=+refs/heads/*:refs/remotes/origin/up/*",
                "team=+refs/heads/team/*:refs/remotes/team/*",
                "pulls=+refs/heads/*:refs/remotes/pulls/*",
                "pulls=+refs/pull/*/head:refs/remotes/pulls/pr/*",
                "upstream=+refs/pull/*/head:refs/remotes/upstream/pr/*",
                "mirror=+refs/heads/*:refs/remotes/mirror/*",
                "backup=+refs/heads/*:refs/remotes/mirror/*",
                "secret=+refs/heads/*:refs/remotes/secret/*",
                "secret=^refs/heads/hidden",
            ]),
        };

        // Mapped, a branch: its name on the remote (10.6).
        AssertSource((RemoteSourceKind.Mapped, "origin", "refs/heads/main"), refs.SourceOf(renamed));
        Assert.Equal("main", refs.LocalNameOf(renamed));
        Assert.Equal("refs/heads/main", refs.SourceOf(renamed.FullName).RemoteRef);
        AssertSource((RemoteSourceKind.Mapped, "team", "refs/heads/team/a"), refs.SourceOf(prefixed));
        Assert.Equal("team/a", refs.LocalNameOf(prefixed));

        // Mapped, not a branch: today's name (P2).
        AssertSource((RemoteSourceKind.Mapped, "upstream", "refs/pull/7/head"), refs.SourceOf(pullRequest));
        Assert.Equal("pr/7", refs.LocalNameOf(pullRequest));

        // R2: today's name, the branch then made without an upstream (D201).
        AssertSource((RemoteSourceKind.AmbiguousRefs, "pulls", null), refs.SourceOf(ambiguousRefs));
        Assert.Equal("pr/1", refs.LocalNameOf(ambiguousRefs));

        // R1 and R3: none.
        var two = refs.SourceOf(ambiguousRemotes);
        Assert.Equal(RemoteSourceKind.AmbiguousRemotes, two.Kind);
        Assert.Equal(["mirror", "backup"], two.Remotes);
        Assert.Null(refs.LocalNameOf(ambiguousRemotes));
        AssertSource((RemoteSourceKind.NotFetched, null, null), refs.SourceOf(notFetched));
        Assert.Null(refs.LocalNameOf(notFetched));
    }

    [Fact]
    public void RepoRefs_with_refspecs_names_a_renamed_branch_by_its_name_on_the_remote_whatever_its_remote_name()
    {
        // B2: refs/remotes/mirror/main of origin checks out into main.
        var mirror = RemoteBranch("refs/remotes/mirror/main", "origin");
        var refs = new RepoRefs(new HeadState("main", null), [mirror], [], ["origin"])
        {
            Refspecs = Lines(["origin=+refs/heads/*:refs/remotes/mirror/*"]),
        };

        AssertSource((RemoteSourceKind.Mapped, "origin", "refs/heads/main"), refs.SourceOf(mirror));
        Assert.Equal("main", refs.LocalNameOf(mirror));
        Assert.False(refs.SourceOf(mirror).IsIdentity(mirror.FullName));
    }

    [Fact]
    public void RepoRefs_with_refspecs_answers_a_ref_that_is_not_a_remote_branch_with_not_fetched()
    {
        var refs = new RepoRefs(new HeadState("main", null), [], [], ["origin"]) { Refspecs = Lines(["origin=+refs/*:refs/*"]) };
        var local = new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Sha);

        AssertSource((RemoteSourceKind.NotFetched, null, null), refs.SourceOf(local));
        Assert.Null(refs.LocalNameOf(local));
    }

    private static FetchRefspecs Lines(string[] lines) =>
        FetchRefspecs.Parse(lines.Select(line =>
        {
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            return (line[..equals], line[(equals + 1)..]);
        }));

    private static GitRef RemoteBranch(string fullName, string remote) =>
        new(fullName, fullName["refs/remotes/".Length..], RefKind.RemoteBranch, Sha, RemoteName: remote);

    private static void AssertSource((RemoteSourceKind Kind, string? Remote, string? RemoteRef) expected, RemoteSource actual)
    {
        Assert.Equal(expected, (actual.Kind, actual.Remote, actual.RemoteRef));
        Assert.Empty(actual.Remotes);
    }
}
