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
/// an empty source, an exact line a negative line excludes) git's own answer is asserted beside
/// it. Runs on Git 2.36 and on CI's git; git's whole sentence is asserted on 2.36 only.
/// </summary>
public class FetchRefspecsParityTests
{
    private const string Default = "+refs/heads/*:refs/remotes/origin/*";

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
        // C2 and P: two refs of one remote fill the tracking ref; git takes the first line's.
        { "C2", ["origin=" + Default, "origin=refs/heads/main:refs/remotes/origin/trunk"], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "refs/heads/trunk" },
        { "C2 swapped", ["origin=refs/heads/main:refs/remotes/origin/trunk", "origin=" + Default], "refs/remotes/origin/trunk", RemoteSourceKind.AmbiguousRefs, "refs/heads/main" },
        { "P", ["origin=" + Default, "origin=+refs/pull/*/head:refs/remotes/origin/pr/*"], "refs/remotes/origin/pr/1", RemoteSourceKind.AmbiguousRefs, "refs/heads/pr/1" },

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
