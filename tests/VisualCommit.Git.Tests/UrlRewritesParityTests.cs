using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="UrlRewrites"/> against git itself (docs/phase-7a-design.md 4.2, 4.3, 4.8): for
/// repositories with rules and remotes, <c>Apply(RemoteUrls, ReadUrlRewritesAsync)</c> equals
/// <c>git remote get-url --all</c> and <c>--push --all</c> per remote; the read is one call, exit
/// 1 is no rule; and a rule's credentials stay out of the call record, also when its value has no
/// trailing <c>/</c> (the <c>\0</c> ends the authority, <see cref="GitCommand.EndsAuthority"/>).
/// </summary>
public class UrlRewritesParityTests
{
    [Fact]
    public async Task The_rules_give_what_git_remote_get_url_gives_for_fetch_and_push()
    {
        using var repo = await TempRepo.CreateAsync("rewrites");
        foreach (var (key, value) in new[]
        {
            ("url.https://github.com/.insteadOf", "gh:"),
            ("url.https://mirror.example/.insteadOf", "https://github.com/chain"),
            ("url.https://longer.example/.insteadOf", "https://github.com/chainx/"),
            ("url.https://first.example/.insteadOf", "tie:"),
            ("url.https://second.example/.insteadOf", "tie:"),
            ("url.ssh://git@github.com/.pushInsteadOf", "https://github.com/"),
        })
        {
            await repo.GitAsync("config", "--add", key, value);
        }

        await repo.GitAsync("remote", "add", "origin", "gh:o/r.git");
        await repo.GitAsync("remote", "add", "direct", "https://github.com/o/d.git");
        await repo.GitAsync("remote", "add", "chain", "https://github.com/chainx/y");
        await repo.GitAsync("remote", "add", "mirror", "https://github.com/chainz/q");
        await repo.GitAsync("remote", "add", "withpush", "https://github.com/o/w.git");
        await repo.GitAsync("config", "--add", "remote.withpush.pushurl", "gh:o/w-push.git");
        await repo.GitAsync("remote", "add", "multi", "tie:a");
        await repo.GitAsync("config", "--add", "remote.multi.url", "https://github.com/o/m.git");
        await repo.GitAsync("remote", "add", "alice", "https://alice@github.com/o/a.git");

        await AssertParityAsync(repo, ["origin", "direct", "chain", "mirror", "withpush", "multi", "alice"]);
    }

    [Fact]
    public async Task An_empty_prefix_rewrites_every_url_that_no_longer_rule_takes_as_git_does()
    {
        using var repo = await TempRepo.CreateAsync("empty-prefix");
        await repo.GitAsync("config", "--add", "url.https://empty.example/.insteadOf", "");
        await repo.GitAsync("config", "--add", "url.https://github.com/.insteadOf", "gh:");
        await repo.GitAsync("config", "--add", "url.https://push.example/.pushInsteadOf", "gh:");
        await repo.GitAsync("remote", "add", "origin", "gh:o/r.git");
        await repo.GitAsync("remote", "add", "other", "https://x.example/r.git");

        await AssertParityAsync(repo, ["origin", "other"]);
    }

    [Fact]
    public async Task The_read_is_one_config_call_and_exit_1_is_no_rule()
    {
        using var repo = await TempRepo.CreateAsync("no-rules");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var before = calls.Snapshot().Count;

        var rewrites = await repository.ReadUrlRewritesAsync(TestCancelled);

        Assert.Same(UrlRewrites.None, rewrites);
        var call = Assert.Single(calls.Snapshot().Skip(before));
        Assert.Equal(@"git config --null --get-regexp ^url\.", call.CommandText);
    }

    [Fact]
    public async Task The_read_leaves_out_inherited_command_line_configuration()
    {
        // As a bound scope does (D329): its calls never see an inherited GIT_CONFIG_PARAMETERS.
        using var repo = await TempRepo.CreateAsync("inherited-rules");
        await repo.GitAsync("config", "--add", "url.https://github.com/.insteadOf", "gh:");
        var environment = new Dictionary<string, string?>(repo.Environment)
        {
            ["GIT_CONFIG_PARAMETERS"] = "'url.https://inherited.example/.insteadof'='in:'",
        };
        var runner = new GitRunner(NewRunner().ExecutablePath, null, null, null, environment);
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);

        var rewrites = await repository.ReadUrlRewritesAsync(TestCancelled);

        Assert.Equal([new UrlRewrite("https://github.com/", "gh:", false)], rewrites.Rules);
    }

    [Fact]
    public async Task Credentials_in_global_rules_without_a_trailing_slash_stay_out_of_the_call_record()
    {
        using var repo = await TempRepo.CreateAsync("rule-secrets");
        var global = repo.Environment["GIT_CONFIG_GLOBAL"]!;
        await repo.GitAsync("config", "--file", global, "url.https://oauth2:SecretOne@a.example.insteadOf", "https://a.example");
        await repo.GitAsync("config", "--file", global, "url.https://oauth2:SecretTwo@b.example.insteadOf", "https://b.example");
        var calls = new GitCallLog();
        var runner = new GitRunner(NewRunner().ExecutablePath, calls, null, null, repo.Environment);
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);

        var rewrites = await repository.ReadUrlRewritesAsync(TestCancelled);

        Assert.Equal(["https://oauth2:SecretOne@a.example", "https://oauth2:SecretTwo@b.example"], rewrites.Rules.Select(rule => rule.Base));
        var records = calls.Snapshot();
        Assert.Contains(records, record => record.CommandText.StartsWith("git config --null --get-regexp", StringComparison.Ordinal) && record.StandardOutput.Contains("https://***@b.example", StringComparison.Ordinal));
        foreach (var secret in new[] { "SecretOne", "SecretTwo" })
        {
            Assert.All(records, record => Assert.DoesNotContain(secret, record.CommandText + record.StandardOutput + record.StandardError, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Credentials_in_local_rules_stay_out_of_the_undo_reads_call_record()
    {
        // Phase 5's undo read (GitRepository.Undo.cs) lists the repository's own keys in this form.
        using var repo = await TempRepo.CreateAsync("undo-secrets");
        await repo.GitAsync("config", "url.https://oauth2:SecretOne@a.example.insteadOf", "https://a.example");
        await repo.GitAsync("config", "url.https://oauth2:SecretTwo@b.example.insteadOf", "https://b.example");
        var calls = new GitCallLog();

        var result = await NewRunner(calls).RunAsync(new GitCommand("config", "--local", "--null", "--list") { WorkingDirectory = repo.Path }, TestCancelled);

        Assert.Contains("SecretTwo", result.StandardOutput, StringComparison.Ordinal);
        var record = Assert.Single(calls.Snapshot());
        Assert.Contains("url.https://***@a.example.insteadof", record.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("url.https://***@b.example.insteadof", record.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretOne", record.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretTwo", record.StandardOutput, StringComparison.Ordinal);
    }

    private static async Task AssertParityAsync(TempRepo repo, string[] remotes)
    {
        var repository = await OpenAsync(repo);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var rewrites = await repository.ReadUrlRewritesAsync(TestCancelled);

        var applied = rewrites.Apply(refs.RemoteUrls);

        Assert.Equal(remotes, applied.Select(remote => remote.Remote));
        foreach (var remote in applied)
        {
            Assert.Equal(await GetUrlsAsync(repo, remote.Remote, push: false), remote.Fetch);
            Assert.Equal(await GetUrlsAsync(repo, remote.Remote, push: true), remote.Push);
        }
    }

    private static async Task<string[]> GetUrlsAsync(TempRepo repo, string remote, bool push)
    {
        var result = push
            ? await repo.GitAsync("remote", "get-url", "--push", "--all", remote)
            : await repo.GitAsync("remote", "get-url", "--all", remote);
        return result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
    }
}
