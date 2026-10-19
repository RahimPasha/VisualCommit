using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.GitConfigTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="FetchRefspecs.SourceOf"/> against git's own tracking setup (docs/phase-6-design.md
/// 10.9, D96's model): each case of 0.5 as fetch lines (<c>git config --add</c>) and tracking refs
/// made with <c>update-ref</c>, no fetch, then <c>git branch --track probe &lt;T&gt;</c> for each
/// tracking ref. A mapped ref is what git tracks (<c>branch.probe.remote</c> and <c>.merge</c>);
/// one two remotes fetch is git's "not tracking: ambiguous information"; one no line fetches is
/// git's "is not a branch", with no branch left. Where the mapping is stricter on purpose (C2, P,
/// F beside the default on one remote, an empty source, an exact line a negative line excludes)
/// git's own answer is asserted beside it. <see cref="FetchRefspecs.DestinationsOf"/> against
/// git's fetch (a bare server, no tracking ref, then <c>git fetch origin</c>) and, where it
/// differs on purpose, against git's push (C2). Runs on Git 2.36 and on CI's git; git's whole
/// sentence is asserted on 2.36 only.
/// </summary>
public class FetchRefspecsParityTests
{
    private const string Default = "+refs/heads/*:refs/remotes/origin/*";

    /// <summary>C2's second line, beside <see cref="Default"/>.</summary>
    private const string Trunk = "refs/heads/main:refs/remotes/origin/trunk";

    /// <summary>0.5's F: what <c>git remote add --mirror=fetch</c> writes.</summary>
    private const string Mirror = "+refs/*:refs/*";

    /// <summary>A case's name, its lines (<c>remote=value</c>; <c>remote=</c> configures a remote without a line), and the tracking refs probed.</summary>
    public static TheoryData<string, string[], string[]> Agreeing => new()
    {
        { "default", ["origin=" + Default], ["refs/remotes/origin/main", "refs/remotes/origin/feature/x"] },
        { "default without +", ["origin=refs/heads/*:refs/remotes/origin/*"], ["refs/remotes/origin/main"] },
        { "B1", ["origin=+refs/heads/*:refs/remotes/origin/up/*"], ["refs/remotes/origin/up/main", "refs/remotes/origin/up/feature/x"] },
        { "B2", ["origin=+refs/heads/*:refs/remotes/mirror/*"], ["refs/remotes/mirror/main", "refs/remotes/origin/stray"] },
        { "C1", ["origin=refs/heads/main:refs/remotes/origin/trunk"], ["refs/remotes/origin/trunk", "refs/remotes/origin/main"] },
        { "C2's other ref", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], ["refs/remotes/origin/main"] },
        { "D", ["origin=+refs/heads/team/*:refs/remotes/origin/*"], ["refs/remotes/origin/a"] },
        { "N", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk", "origin=^refs/heads/trunk"], ["refs/remotes/origin/trunk", "refs/remotes/origin/main"] },
        { "N swapped", ["origin=refs/heads/main:refs/remotes/origin/trunk", "origin=" + Default, "origin=^refs/heads/trunk"], ["refs/remotes/origin/trunk"] },
        { "E", ["origin=" + Default, "origin=^refs/heads/secret"], ["refs/remotes/origin/secret", "refs/remotes/origin/main"] },
        { "a negative pattern", ["origin=" + Default, "origin=^refs/heads/team/*"], ["refs/remotes/origin/team/a"] },
        { "a negative short name", ["origin=" + Default, "origin=^secret"], ["refs/remotes/origin/secret"] },
        { "a negative line of another remote", ["origin=" + Default, "backup=^refs/heads/secret"], ["refs/remotes/origin/secret"] },
        { "P's branch", ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], ["refs/remotes/origin/main"] },
        { "P2", ["origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], ["refs/remotes/origin/pr/1"] },
        { "G", ["origin="], ["refs/remotes/origin/main"] },
        { "a namespace no line fetches", ["origin=" + Default], ["refs/remotes/old/main"] },
        { "H", ["origin=" + Default, "backup=" + Default], ["refs/remotes/origin/main"] },
        { "H with one excluded", ["origin=" + Default, "backup=" + Default, "backup=^refs/heads/main"], ["refs/remotes/origin/main"] },
        { "H with one ambiguous", ["origin=" + Default, "backup=" + Default, "backup=refs/heads/x:refs/remotes/origin/main"], ["refs/remotes/origin/main"] },
        { "T", ["origin=" + Default, "team=+refs/heads/*:refs/remotes/team/*", "team/fork=+refs/heads/*:refs/remotes/team/fork/*"], ["refs/remotes/team/fork/main", "refs/remotes/team/main"] },
        { "O2", ["origin=main:refs/remotes/origin/m"], ["refs/remotes/origin/m"] },
        { "f* to g*", ["origin=refs/heads/f*:refs/remotes/origin/g*"], ["refs/remotes/origin/geature/x", "refs/remotes/origin/g"] },
        { "*/x to */y", ["origin=refs/heads/*/x:refs/remotes/origin/*/y"], ["refs/remotes/origin/feature/y", "refs/remotes/origin/feature/x"] },
        { "the default twice", ["origin=" + Default, "origin=refs/heads/*:refs/remotes/origin/*"], ["refs/remotes/origin/main"] },
        { "an empty source after the default", ["origin=" + Default, "origin=:refs/remotes/origin/x"], ["refs/remotes/origin/x"] },
        { "an empty destination", ["origin=refs/heads/main:"], ["refs/remotes/origin/main"] },
        { "no destination", ["origin=refs/heads/main"], ["refs/remotes/origin/main"] },
        { "a dotted name", ["Team.Dot.x=+refs/heads/*:refs/remotes/Team.Dot.x/*"], ["refs/remotes/Team.Dot.x/main"] },

        // F: a fetch mirror beside another remote makes that remote's every tracking ref
        // ambiguous between the two; its own tracking refs map to themselves on the mirror.
        { "F, another remote", ["origin=" + Default, "m=" + Mirror], ["refs/remotes/origin/main", "refs/remotes/m/main"] },
        { "F, another remote first", ["m=" + Mirror, "origin=" + Default], ["refs/remotes/origin/main", "refs/remotes/m/main"] },
        { "F alone", ["origin=" + Mirror], ["refs/remotes/origin/main"] },
        { "F's other ref on the same remote", ["origin=" + Default, "origin=" + Mirror], ["refs/remotes/m/main"] },
    };

    [Theory]
    [MemberData(nameof(Agreeing))]
    public async Task Gits_tracking_setup_agrees_with_the_mapping(string name, string[] lines, string[] trackingRefs)
    {
        using var repo = await RepoWithLinesAsync(lines, trackingRefs);
        var refspecs = Parse(lines);

        foreach (var trackingRef in trackingRefs)
        {
            var source = refspecs.SourceOf(trackingRef);
            var git = await ProbeAsync(repo, trackingRef);
            var what = $"{name}, {trackingRef}: the mapping says {source.Kind} {source.Remote} {source.RemoteRef}; git exits {git.ExitCode} ({git.Error.Trim()})";

            switch (source.Kind)
            {
                case RemoteSourceKind.Mapped:
                    Assert.True(git.ExitCode == 0, what);
                    Assert.Equal((source.Remote, source.RemoteRef), (git.Remote, git.Merge));
                    break;
                case RemoteSourceKind.AmbiguousRemotes:
                    Assert.True(git.ExitCode == 128 && git.Error.Contains("not tracking: ambiguous information", StringComparison.Ordinal), what);
                    Assert.Equal((null, null), (git.Remote, git.Merge));
                    if (await GitIs236Async())
                    {
                        Assert.StartsWith($"fatal: not tracking: ambiguous information for ref '{trackingRef}'\n", git.Error, StringComparison.Ordinal);
                    }

                    break;
                case RemoteSourceKind.NotFetched:
                    Assert.True(git.ExitCode == 128 && git.Error.Contains("is not a branch", StringComparison.Ordinal), what);
                    Assert.Equal((null, null), (git.Remote, git.Merge));
                    Assert.False(git.LeftABranch, what);
                    if (await GitIs236Async())
                    {
                        Assert.StartsWith($"fatal: cannot set up tracking information; starting point '{trackingRef}' is not a branch\n", git.Error, StringComparison.Ordinal);
                    }

                    break;
                default:
                    Assert.Fail($"{what}: a case where the mapping differs from git belongs among the differences.");
                    break;
            }
        }
    }

    /// <summary>
    /// The deliberate differences: a case's lines, its tracking ref, how the mapping takes it, and
    /// the ref on <c>origin</c> git's tracking setup gives instead.
    /// </summary>
    public static TheoryData<string, string[], string, RemoteSourceKind, string> Differing => new()
    {
        // C2, P and F beside the default on one remote: two refs of one remote fill the tracking
        // ref; git takes the first line's.
        { "C2", ["origin=" + Default, "origin=" + Trunk], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "refs/heads/trunk" },
        { "C2 swapped", ["origin=" + Trunk, "origin=" + Default], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "refs/heads/main" },
        { "P", ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "refs/remotes/origin/pr/1", RemoteSourceKind.AmbiguousRefs, "refs/heads/pr/1" },
        { "F on the same remote", ["origin=" + Default, "origin=" + Mirror], "refs/remotes/origin/main", RemoteSourceKind.AmbiguousRefs, "refs/heads/main" },
        { "F on the same remote swapped", ["origin=" + Mirror, "origin=" + Default], "refs/remotes/origin/main", RemoteSourceKind.AmbiguousRefs, "refs/remotes/origin/main" },

        // An empty source: git tracks a branch with an empty name.
        { "an empty source", ["origin=:refs/remotes/origin/x"], "refs/remotes/origin/x", RemoteSourceKind.NotFetched, "" },
        { "an empty source first", ["origin=:refs/remotes/origin/x", "origin=" + Default], "refs/remotes/origin/x", RemoteSourceKind.Mapped, "" },

        // An exact line whose source a negative line names: git's tracking setup checks only the
        // sources pattern lines give against negative lines, and maps it; its fetch leaves the
        // tracking ref alone.
        { "an exact line excluded", ["origin=refs/heads/secret:refs/remotes/origin/s", "origin=^refs/heads/secret"], "refs/remotes/origin/s", RemoteSourceKind.NotFetched, "refs/heads/secret" },
        { "an exact line excluded by a pattern", ["origin=refs/heads/secret:refs/remotes/origin/s", "origin=^refs/heads/sec*"], "refs/remotes/origin/s", RemoteSourceKind.NotFetched, "refs/heads/secret" },
    };

    [Theory]
    [MemberData(nameof(Differing))]
    public async Task The_mapping_is_stricter_than_gits_tracking_setup_on_purpose(string name, string[] lines, string trackingRef, RemoteSourceKind kind, string gitsMerge)
    {
        using var repo = await RepoWithLinesAsync(lines, [trackingRef]);

        var source = Parse(lines).SourceOf(trackingRef);
        var git = await ProbeAsync(repo, trackingRef);

        Assert.True(kind == source.Kind, $"{name}: the mapping says {source.Kind}, expected {kind}");
        if (kind == RemoteSourceKind.Mapped)
        {
            Assert.NotEqual(gitsMerge, source.RemoteRef);
        }

        Assert.True(git.ExitCode == 0, $"{name}: git exits {git.ExitCode} ({git.Error.Trim()})");
        Assert.Equal(("origin", gitsMerge), (git.Remote, git.Merge));
    }

    [Fact]
    public async Task Gits_fetch_leaves_alone_a_tracking_ref_whose_exact_lines_source_a_negative_line_names()
    {
        // Why the mapping says NotFetched where git's tracking setup maps the ref (above): the
        // fetch applies the negative line to the remote's refs, so refs/remotes/origin/s is never
        // written, while the default line beside it fills origin/main.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.CreateBranchAsync("secret");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", false, "refs/heads/main", "refs/heads/secret");
        await repo.GitAsync("update-ref", "-d", "refs/remotes/origin/main");
        await repo.GitAsync("update-ref", "-d", "refs/remotes/origin/secret");
        await repo.GitAsync("config", "--unset-all", "remote.origin.fetch");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "refs/heads/secret:refs/remotes/origin/s");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "^refs/heads/secret");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", Default);

        await repo.GitAsync("fetch", "--quiet", "origin");

        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/s"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/secret"));
        Assert.Equal(await repo.HeadAsync(), await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(
            RemoteSourceKind.NotFetched,
            Parse(["origin=refs/heads/secret:refs/remotes/origin/s", "origin=^refs/heads/secret", "origin=" + Default]).SourceOf("refs/remotes/origin/s").Kind);
    }

    /// <summary>
    /// The forward mapping's cases (10.1, 10.9): origin's lines, and the tracking refs git's fetch
    /// writes from <see cref="RepoWithServerAsync"/>'s server (<c>feature/x</c>, <c>main</c>,
    /// <c>secret</c>, <c>team/a</c> and <c>refs/pull/1/head</c>; no <c>trunk</c>, which C2's
    /// fetch refuses), in byte order. Checked with Git 2.36 and 2.55.
    /// </summary>
    public static TheoryData<string, string[], string[]> Fetched => new()
    {
        { "default", [Default], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/secret", "refs/remotes/origin/team/a"] },
        { "B1", ["+refs/heads/*:refs/remotes/origin/up/*"], ["refs/remotes/origin/up/feature/x", "refs/remotes/origin/up/main", "refs/remotes/origin/up/secret", "refs/remotes/origin/up/team/a"] },
        { "C1", [Trunk], ["refs/remotes/origin/trunk"] },

        // C2: main goes to both, in either order.
        { "C2", [Default, Trunk], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/secret", "refs/remotes/origin/team/a", "refs/remotes/origin/trunk"] },
        { "C2 swapped", [Trunk, Default], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/secret", "refs/remotes/origin/team/a", "refs/remotes/origin/trunk"] },
        { "D", ["+refs/heads/team/*:refs/remotes/origin/*"], ["refs/remotes/origin/a"] },

        // E and a negative pattern: what they name goes nowhere. N: main still goes to trunk.
        { "E", [Default, "^refs/heads/secret"], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/team/a"] },
        { "a negative pattern", [Default, "^refs/heads/team/*"], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/secret"] },
        { "an exact line excluded", ["refs/heads/secret:refs/remotes/origin/s", "^refs/heads/secret", Default], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/team/a"] },
        { "N", [Default, Trunk, "^refs/heads/trunk"], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/secret", "refs/remotes/origin/team/a", "refs/remotes/origin/trunk"] },

        // P: pr/1 comes from refs/pull/1/head (the server has no branch pr/1); P2 alone.
        { "P", [Default, "+refs/pull/*/head:refs/remotes/origin/pr/*"], ["refs/remotes/origin/feature/x", "refs/remotes/origin/main", "refs/remotes/origin/pr/1", "refs/remotes/origin/secret", "refs/remotes/origin/team/a"] },
        { "P2", ["+refs/pull/*/head:refs/remotes/origin/pr/*"], ["refs/remotes/origin/pr/1"] },

        // A star inside a component and in the middle; every ref of the server; no destination.
        { "f* to g*", ["refs/heads/f*:refs/remotes/origin/g*"], ["refs/remotes/origin/geature/x"] },
        { "*/x to */y", ["refs/heads/*/x:refs/remotes/origin/*/y"], ["refs/remotes/origin/feature/y"] },
        { "every ref", ["+refs/*:refs/remotes/origin/*"], ["refs/remotes/origin/heads/feature/x", "refs/remotes/origin/heads/main", "refs/remotes/origin/heads/secret", "refs/remotes/origin/heads/team/a", "refs/remotes/origin/pull/1/head"] },
        { "no destination", ["refs/heads/main"], [] },
    };

    [Theory]
    [MemberData(nameof(Fetched))]
    public async Task Gits_fetch_fills_exactly_the_tracking_refs_DestinationsOf_gives(string name, string[] originLines, string[] fetched)
    {
        // 10.1: DestinationsOf is "git's forward mapping, which the fetch uses". Over every ref
        // the server holds, its union is what the fetch writes.
        using var repo = await RepoWithServerAsync(originLines);
        var refspecs = Parse([.. originLines.Select(line => "origin=" + line)]);
        var serverRefs = await ServerRefsAsync(repo);
        Assert.Empty(await TrackingRefsAsync(repo));

        var result = await repo.GitAllowingFailureAsync("fetch", "--quiet", "origin");

        Assert.True(result.Succeeded, $"{name}: git fetch exits {result.ExitCode} ({result.StandardError.Trim()})");
        Assert.Equal(["refs/heads/feature/x", "refs/heads/main", "refs/heads/secret", "refs/heads/team/a", "refs/pull/1/head"], serverRefs);
        Assert.Equal(fetched, await TrackingRefsAsync(repo));
        Assert.Equal(
            fetched,
            serverRefs.SelectMany(remoteRef => refspecs.DestinationsOf("origin", remoteRef)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false, "refs/remotes/origin/main")]
    [InlineData(true, "refs/remotes/origin/trunk")]
    public async Task Gits_push_fills_only_the_first_tracking_ref_where_DestinationsOf_follows_the_fetch(bool swapped, string pushed)
    {
        // C2, a deliberate difference: a push of main updates the tracking ref of the first line
        // that maps refs/heads/main (git's remote_find_tracking), while the fetch fills both, as
        // DestinationsOf gives them. 10.4's push guard asks what the next fetch does.
        string[] lines = swapped ? [Trunk, Default] : [Default, Trunk];
        string[] destinations = swapped
            ? ["refs/remotes/origin/trunk", "refs/remotes/origin/main"]
            : ["refs/remotes/origin/main", "refs/remotes/origin/trunk"];
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.AddBareRemoteAsync();
        await SetOriginLinesAsync(repo, lines);

        await repo.PushAsync("origin", false, "refs/heads/main:refs/heads/main");

        Assert.Equal([pushed], await TrackingRefsAsync(repo));
        Assert.Equal(destinations, Parse([.. lines.Select(line => "origin=" + line)]).DestinationsOf("origin", "refs/heads/main"));

        await repo.GitAsync("fetch", "--quiet", "origin");

        Assert.Equal(["refs/remotes/origin/main", "refs/remotes/origin/trunk"], await TrackingRefsAsync(repo));
        Assert.Equal(await repo.HeadAsync(), await repo.RefTargetAsync("refs/remotes/origin/trunk"));
    }

    /// <summary>
    /// A repository with one commit and the branches <c>feature/x</c>, <c>secret</c> and
    /// <c>team/a</c> beside <c>main</c>; a bare remote <c>origin</c> holding them and
    /// <c>refs/pull/1/head</c>; no ref under <c>refs/remotes/</c>; and origin's fetch lines replaced
    /// by <paramref name="originLines"/>. The push names the server by its path, and the tracking
    /// refs are deleted after it all the same: Git 2.56 (CI's Windows and macOS, run 39207577529)
    /// updated origin's tracking refs on that push, which 2.36, 2.43 and 2.55 leave alone.
    /// </summary>
    private static async Task<TempRepo> RepoWithServerAsync(string[] originLines)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            await repo.CreateBranchAsync("feature/x");
            await repo.CreateBranchAsync("secret");
            await repo.CreateBranchAsync("team/a");
            var server = await repo.AddBareRemoteAsync();
            await repo.GitAsync("push", "--quiet", server, "refs/heads/feature/x", "refs/heads/main", "refs/heads/secret", "refs/heads/team/a", "HEAD:refs/pull/1/head");
            var tracking = (await repo.GitAsync("for-each-ref", "--format=%(refname)", "refs/remotes")).StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'));
            var deletions = string.Concat(tracking.Select(name => "delete " + name + "\n"));
            if (deletions.Length > 0)
            {
                await repo.GitWithInputAsync(deletions, "update-ref", "--no-deref", "--stdin");
            }

            await SetOriginLinesAsync(repo, originLines);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Replaces <c>remote.origin.fetch</c> with <paramref name="lines"/>, in order.</summary>
    private static async Task SetOriginLinesAsync(TempRepo repo, string[] lines)
    {
        await repo.GitAsync("config", "--unset-all", "remote.origin.fetch");
        foreach (var line in lines)
        {
            await repo.GitAsync("config", "--add", "remote.origin.fetch", line);
        }
    }

    /// <summary>The refs the server <c>origin</c> holds (<c>git ls-remote --refs</c>), in byte order.</summary>
    private static async Task<string[]> ServerRefsAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("ls-remote", "--refs", "origin")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r')[(line.IndexOf('\t', StringComparison.Ordinal) + 1)..])
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// The refs under <c>refs/remotes/</c> that are not symbolic, in byte order: what the fetch
    /// lines have git write. (Git 2.48 and later also make <c>refs/remotes/origin/HEAD</c>, a
    /// symbolic ref, from the server's HEAD, which no fetch line maps.)
    /// </summary>
    private static async Task<string[]> TrackingRefsAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("for-each-ref", "--format=%(refname)%09%(symref)", "refs/remotes")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split('\t'))
            .Where(fields => fields[1].Length == 0)
            .Select(fields => fields[0])
            .Order(StringComparer.Ordinal)];

    /// <summary>A repository with one commit, each remote of <paramref name="lines"/> configured with a URL and its lines added in order, and each tracking ref at HEAD.</summary>
    private static async Task<TempRepo> RepoWithLinesAsync(string[] lines, string[] trackingRefs)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            var configured = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (remote, value) in Split(lines))
            {
                if (configured.Add(remote))
                {
                    await repo.ConfigAsync($"remote.{remote}.url", repo.SiblingPath(remote.Replace('/', '-') + ".git"));
                }

                if (value is not null)
                {
                    await repo.GitAsync("config", "--add", $"remote.{remote}.fetch", value);
                }
            }

            foreach (var trackingRef in trackingRefs)
            {
                await repo.GitAsync("update-ref", trackingRef, "HEAD");
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// <c>git branch --track probe &lt;T&gt;</c>: its exit code and error output, the upstream it
    /// wrote, and whether it left the branch; then <c>git branch -D probe</c>, its failure ignored.
    /// </summary>
    private static async Task<Probe> ProbeAsync(TempRepo repo, string trackingRef)
    {
        var result = await repo.GitAllowingFailureAsync("branch", "--track", "probe", trackingRef);
        var remote = await ConfigValueAsync(repo, "branch.probe.remote");
        var merge = await ConfigValueAsync(repo, "branch.probe.merge");
        var leftABranch = (await repo.GitAllowingFailureAsync("show-ref", "--verify", "--quiet", "refs/heads/probe")).Succeeded;
        await repo.GitAllowingFailureAsync("branch", "-D", "probe");
        await repo.GitAllowingFailureAsync("config", "--remove-section", "branch.probe");
        return new Probe(result.ExitCode, result.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal), remote, merge, leftABranch);
    }

    /// <summary>A setting's value, or null when it is not set (<c>git config --get</c>, exit 1).</summary>
    private static async Task<string?> ConfigValueAsync(TempRepo repo, string key)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get", key);
        return result.ExitCode == 1 ? null : result.StandardOutput.TrimEnd('\n', '\r');
    }

    private static FetchRefspecs Parse(string[] lines) =>
        FetchRefspecs.Parse(Split(lines).Where(line => line.Value is not null).Select(line => (line.Remote, line.Value!)));

    private static IEnumerable<(string Remote, string? Value)> Split(string[] lines) =>
        lines.Select(line =>
        {
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            var value = line[(equals + 1)..];
            return (line[..equals], equals == line.Length - 1 ? null : (string?)value);
        });

    private sealed record Probe(int ExitCode, string Error, string? Remote, string? Merge, bool LeftABranch);
}
