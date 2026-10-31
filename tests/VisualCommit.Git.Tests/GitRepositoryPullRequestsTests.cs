using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// A pull request's head fetched into <c>FETCH_HEAD</c> only (docs/phase-7a-design.md 10.5, 10.6;
/// D305): the command's form, that it writes no ref, tag or configuration whatever the remote's
/// fetch lines, <c>fetch.prune</c> and <c>tagOpt</c> say, and that the SHA comes from the file's
/// one line of this fetch, never from a <c>rev-parse</c> of its own.
/// </summary>
public sealed class GitRepositoryPullRequestsTests
{
    private const string Fetch8 = "fetch --progress --no-tags --refmap= --write-fetch-head origin refs/pull/8/head";

    [Fact]
    public async Task The_fetch_is_one_call_of_its_form_and_the_sha_is_the_lines()
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        var runner = new RecordingRunner(NewRunner());
        var repository = await GitRepository.OpenAsync(runner, clone.Path, TestCancelled);
        runner.Clear();

        var sha = await repository.FetchToFetchHeadAsync("origin", "refs/pull/8/head", NetworkOptions.None, TestCancelled);

        Assert.Equal(server.Head8, sha);
        Assert.Equal([Fetch8], runner.Calls);
    }

    [Fact]
    public async Task A_refs_pull_line_prune_and_tag_opt_write_no_ref_and_no_configuration()
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        await clone.GitAsync("config", "--add", "remote.origin.fetch", "+refs/pull/*/head:refs/remotes/origin/pr/*");
        await clone.ConfigAsync("fetch.prune", "true");
        await clone.ConfigAsync("remote.origin.tagOpt", "--tags");

        // A stale remote-tracking ref a prune would delete, and a tag a tag fetch would bring.
        await clone.GitAsync("update-ref", "refs/remotes/origin/stale", await RevAsync(clone, "HEAD"));
        await server.Author.TagAsync("v9");
        await server.Author.PushAsync("origin", false, "refs/tags/v9");
        var refsBefore = await GitLinesAsync(clone, "for-each-ref", "--format=%(refname) %(objectname)");
        var configBefore = await GitTextAsync(clone, "config", "--local", "--list");
        var repository = await OpenAsync(clone);

        var sha = await repository.FetchToFetchHeadAsync("origin", "refs/pull/8/head", NetworkOptions.None, TestCancelled);

        Assert.Equal(server.Head8, sha);
        Assert.Equal(refsBefore, await GitLinesAsync(clone, "for-each-ref", "--format=%(refname) %(objectname)"));
        Assert.Equal(configBefore, await GitTextAsync(clone, "config", "--local", "--list"));
        Assert.Equal(server.Head8, await RevAsync(clone, "FETCH_HEAD^{commit}"));
        var fetchHead = ReadGitFile(clone, "FETCH_HEAD");
        Assert.StartsWith($"{server.Head8}\t\t'refs/pull/8/head' of ", fetchHead, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_pull_request_is_git_s_failure()
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        var repository = await OpenAsync(clone);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.FetchToFetchHeadAsync("origin", "refs/pull/99/head", NetworkOptions.None, TestCancelled));

        Assert.Equal(128, failure.ExitCode);
        Assert.Contains("refs/pull/99/head", failure.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("refs/heads/main")]
    [InlineData("refs/pull/8/merge")]
    [InlineData("refs/pull/x/head")]
    [InlineData("refs/pull//head")]
    [InlineData("refs/pull/8/head/more")]
    [InlineData("pull/8/head")]
    public async Task Any_other_source_ref_is_refused_without_a_call(string sourceRef)
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        var runner = new RecordingRunner(NewRunner());
        var repository = await GitRepository.OpenAsync(runner, clone.Path, TestCancelled);
        runner.Clear();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.FetchToFetchHeadAsync("origin", sourceRef, NetworkOptions.None, TestCancelled));

        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("two lines")]
    [InlineData("empty")]
    [InlineData("for merge not")]
    public async Task A_fetch_head_rewritten_after_the_fetch_gives_null(string rewrite)
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        var fetchHeadPath = Path.Combine(clone.Path, ".git", "FETCH_HEAD");
        var other = await RevAsync(clone, "HEAD");
        var text = rewrite switch
        {
            "other" => $"{server.Head8}\t\t'refs/pull/7/head' of {server.Bare}\n",
            "two lines" => $"{server.Head8}\t\t'refs/pull/8/head' of {server.Bare}\n{other}\tnot-for-merge\tbranch 'main' of {server.Bare}\n",
            "empty" => string.Empty,
            _ => $"{server.Head8}\tnot-for-merge\t'refs/pull/8/head' of {server.Bare}\n",
        };
        var runner = new RewritingRunner(NewRunner(), "fetch", fetchHeadPath, text);
        var repository = await GitRepository.OpenAsync(runner, clone.Path, TestCancelled);
        runner.Clear();

        var sha = await repository.FetchToFetchHeadAsync("origin", "refs/pull/8/head", NetworkOptions.None, TestCancelled);

        Assert.Null(sha);
        Assert.True(runner.Rewrote);
        Assert.Equal([Fetch8], runner.Calls);
    }

    [Fact]
    public async Task In_a_linked_worktree_the_worktree_s_own_file_is_read()
    {
        using var server = await ServerAsync();
        using var clone = await server.Author.CloneSiblingAsync("clone", server.Bare);
        var linked = clone.SiblingPath("linked");
        await clone.GitAsync("worktree", "add", "--quiet", "-b", "other", linked, "HEAD");

        // The main worktree's file names another pull request; the linked one's fetch must not read it.
        var mainFile = Path.Combine(clone.Path, ".git", "FETCH_HEAD");
        var decoy = $"{await RevAsync(clone, "HEAD")}\t\t'refs/pull/8/head' of {server.Bare}\n";
        File.WriteAllText(mainFile, decoy);
        var repository = await GitRepository.OpenAsync(NewRunner(), linked, TestCancelled);

        var sha = await repository.FetchToFetchHeadAsync("origin", "refs/pull/8/head", NetworkOptions.None, TestCancelled);

        Assert.Equal(server.Head8, sha);
        Assert.Equal(decoy, File.ReadAllText(mainFile));
        Assert.True(File.Exists(Path.Combine(repository.GitDirectory, "FETCH_HEAD")));
        Assert.NotEqual(Path.GetFullPath(Path.Combine(clone.Path, ".git")), Path.GetFullPath(repository.GitDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    /// <summary>
    /// A bare "server" with <c>main</c> and a pull request's head <c>refs/pull/8/head</c> that is no
    /// branch, pushed from <c>Author</c>, whose clone in a test has none of the head's objects.
    /// </summary>
    private static async Task<Server> ServerAsync()
    {
        var author = await TempRepo.CreateAsync();
        try
        {
            await author.CommitFileAsync("f.txt", "base\n", "Initial commit");
            var bare = await author.AddBareRemoteAsync();
            await author.PushAsync("origin", false, "main");

            // A clone checks out the server's HEAD, which the blank bare repository may name otherwise.
            await author.GitAsync("--git-dir", bare, "symbolic-ref", "HEAD", "refs/heads/main");
            await author.GitAsync("switch", "--quiet", "-c", "contributor");
            var head8 = await author.CommitFileAsync("f.txt", "changed\n", "A contributor's change");
            await author.PushAsync("origin", false, "contributor:refs/pull/8/head");
            await author.GitAsync("switch", "--quiet", "main");
            return new Server(author, bare, head8);
        }
        catch
        {
            author.Dispose();
            throw;
        }
    }

    private sealed record Server(TempRepo Author, string Bare, string Head8) : IDisposable
    {
        public void Dispose() => Author.Dispose();
    }

    /// <summary>A runner that records every call's arguments; the calls go to real git.</summary>
    private class RecordingRunner(IGitRunner real) : IGitRunner
    {
        private readonly Lock _gate = new();
        private readonly List<string> _calls = [];

        public string ExecutablePath => real.ExecutablePath;

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _calls];
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _calls.Clear();
            }
        }

        public virtual async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _calls.Add(string.Join(' ', command.Arguments));
            }

            return await real.RunAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A recording runner that, when a call whose first argument is <paramref name="verb"/> ends,
    /// writes <paramref name="text"/> to <paramref name="path"/>: another fetch in the same worktree
    /// between the fetch and the file's read (10.5).
    /// </summary>
    private sealed class RewritingRunner(IGitRunner real, string verb, string path, string text) : RecordingRunner(real)
    {
        public bool Rewrote { get; private set; }

        public override async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            var result = await base.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (command.Arguments.Count > 0 && command.Arguments[0] == verb && result.Succeeded)
            {
                File.WriteAllText(path, text);
                Rewrote = true;
            }

            return result;
        }
    }
}
