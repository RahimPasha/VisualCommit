using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.GitConfigTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// A conditional include's file as a clone's binding reads it (D331, fixed at phase 7b's review before
/// the gate): <see cref="GitConfigIncludes"/>'s directive test and path rule, and
/// <see cref="GitConfiguration.ReadIncludedAsync"/> live: a relative path beside the file that holds
/// the directive, a nested include followed and a nested conditional one listed, other keys left out,
/// a missing file empty, a broken one and an unworkable path failing, one call run as the outside read.
/// </summary>
public class GitConfigIncludeReadTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "include-lab"));

    [Theory]
    [InlineData("includeif.gitdir:~/work/.path", true)]
    [InlineData("includeIf.gitdir/i:C:/Work/.Path", true)]
    [InlineData("includeif.hasconfig:remote.*.url:https://example.invalid/**.path", true)]
    [InlineData("includeif.onbranch:main.path", true)]
    [InlineData("includeif..path", true)]
    [InlineData("includeif.path", false)]
    [InlineData("include.path", false)]
    [InlineData("includeif.gitdir:~/work/.other", false)]
    [InlineData("url.https://example.invalid/.insteadof", false)]
    public void A_directive_is_any_includeif_path_key(string key, bool conditional) =>
        Assert.Equal(conditional, GitConfigIncludes.IsConditional(new GitConfigValue(GitConfigScope.Global, null, key, "x")));

    [Fact]
    public void A_path_resolves_as_git_resolves_it()
    {
        var origin = Path.Combine(Root, "conf", "global.gitconfig");
        var home = Path.Combine(Root, "home");

        Assert.Equal(Path.Combine(Root, "conf", "rules.gitconfig"), GitConfigIncludes.Resolve("rules.gitconfig", origin, home));
        Assert.Equal(Path.Combine(Root, "conf", "sub", "rules.gitconfig"), GitConfigIncludes.Resolve("sub/rules.gitconfig", origin, home));
        Assert.Equal(Path.Combine(Root, "rules.gitconfig"), GitConfigIncludes.Resolve("../rules.gitconfig", origin, home));
        Assert.Equal(Path.Combine(home, "work.gitconfig"), GitConfigIncludes.Resolve("~/work.gitconfig", origin, home));
        Assert.Equal(Path.GetFullPath(home), GitConfigIncludes.Resolve("~", origin, home));
        var absolute = Path.Combine(Root, "elsewhere", "abs.gitconfig");
        Assert.Equal(absolute, GitConfigIncludes.Resolve(GitPath(absolute), origin, home));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("~other/work.gitconfig")]
    [InlineData("%(prefix)/etc/work.gitconfig")]
    public void A_path_git_resolves_by_means_the_app_does_not_follow_is_none(string? path) =>
        Assert.Null(GitConfigIncludes.Resolve(path, Path.Combine(Root, "global.gitconfig"), Path.Combine(Root, "home")));

    [Fact]
    public void A_home_path_without_a_home_and_a_relative_path_from_the_command_line_are_none()
    {
        Assert.Null(GitConfigIncludes.Resolve("~/work.gitconfig", Path.Combine(Root, "global.gitconfig"), null));
        Assert.Null(GitConfigIncludes.Resolve("rules.gitconfig", null, Path.Combine(Root, "home")));
    }

    [Fact]
    public async Task A_relative_include_is_read_beside_its_file_with_its_nested_include_and_conditional_directive()
    {
        using var lab = new ConfigLab();
        Directory.CreateDirectory(lab.Combine("sub"));
        File.WriteAllText(lab.Combine("sub", "rules.gitconfig"), "[user]\n\temail = someone@example.invalid\n[url \"http://bob@example.invalid/\"]\n\tinsteadOf = http://example.invalid/\n[include]\n\tpath = nested.gitconfig\n");
        File.WriteAllText(lab.Combine("sub", "nested.gitconfig"), "[includeIf \"onbranch:main\"]\n\tpath = other.gitconfig\n[url \"http://carol@example.invalid/\"]\n\tpushInsteadOf = http://example.invalid/\n");
        lab.WriteGlobal("[includeIf \"gitdir:/nowhere/\"]\n\tpath = sub/rules.gitconfig\n");
        var directive = Assert.Single((await lab.Configuration.ReadAsync(null, TestCancelled)).Values, GitConfigIncludes.IsConditional);

        var included = await lab.Configuration.ReadIncludedAsync(directive, TestCancelled);

        var rules = lab.Combine("sub", "rules.gitconfig");
        var nested = lab.Combine("sub", "nested.gitconfig");
        Assert.Equal(
            [
                new(GitConfigScope.Global, rules, "url.http://bob@example.invalid/.insteadof", "http://example.invalid/"),
                new(GitConfigScope.Global, nested, "includeif.onbranch:main.path", "other.gitconfig"),
                new GitConfigValue(GitConfigScope.Global, nested, "url.http://carol@example.invalid/.pushinsteadof", "http://example.invalid/"),
            ],
            included);
    }

    [Fact]
    public async Task A_home_include_is_read_under_the_home_of_the_environment_git_gets()
    {
        using var home = new TempDirectory("include home");
        using var lab = new ConfigLab(extraEnvironment: new Dictionary<string, string?> { ["HOME"] = home.Path });
        File.WriteAllText(home.Combine("work.gitconfig"), "[url \"http://bob@example.invalid/\"]\n\tinsteadOf = http://example.invalid/\n");
        lab.WriteGlobal("[includeIf \"gitdir:~/work/\"]\n\tpath = ~/work.gitconfig\n");
        var directive = Assert.Single((await lab.Configuration.ReadAsync(null, TestCancelled)).Values, GitConfigIncludes.IsConditional);

        var included = await lab.Configuration.ReadIncludedAsync(directive, TestCancelled);

        Assert.Equal([new GitConfigValue(GitConfigScope.Global, home.Combine("work.gitconfig"), "url.http://bob@example.invalid/.insteadof", "http://example.invalid/")], included);
    }

    [Fact]
    public async Task A_missing_file_and_a_file_without_rules_read_empty()
    {
        using var lab = new ConfigLab();
        File.WriteAllText(lab.Combine("email.gitconfig"), "[user]\n\temail = someone@example.invalid\n");
        lab.WriteGlobal("[includeIf \"gitdir:/nowhere/\"]\n\tpath = missing.gitconfig\n[includeIf \"gitdir:/nowhere/\"]\n\tpath = email.gitconfig\n");
        var directives = (await lab.Configuration.ReadAsync(null, TestCancelled)).Values.Where(GitConfigIncludes.IsConditional).ToList();
        Assert.Equal(2, directives.Count);

        Assert.Empty(await lab.Configuration.ReadIncludedAsync(directives[0], TestCancelled));
        Assert.Empty(await lab.Configuration.ReadIncludedAsync(directives[1], TestCancelled));
    }

    [Fact]
    public async Task A_broken_file_fails_the_read_and_a_path_the_app_cannot_work_out_fails_without_git()
    {
        using var lab = new ConfigLab();
        File.WriteAllText(lab.Combine("broken.gitconfig"), "[url \"http://bob@example.invalid/\"\n\tinsteadOf = x\n");
        lab.WriteGlobal("[includeIf \"gitdir:/nowhere/\"]\n\tpath = broken.gitconfig\n[includeIf \"gitdir:/nowhere/\"]\n\tpath = ~nobody-here/work.gitconfig\n");
        var directives = (await lab.Configuration.ReadAsync(null, TestCancelled)).Values.Where(GitConfigIncludes.IsConditional).ToList();
        Assert.Equal(2, directives.Count);
        var calls = lab.Calls.Snapshot().Count;

        Assert.Equal(128, (await Assert.ThrowsAsync<GitException>(() => lab.Configuration.ReadIncludedAsync(directives[0], TestCancelled))).ExitCode);
        Assert.Equal(calls + 1, lab.Calls.Snapshot().Count);
        Assert.Null((await Assert.ThrowsAsync<GitException>(() => lab.Configuration.ReadIncludedAsync(directives[1], TestCancelled))).ExitCode);
        Assert.Equal(calls + 1, lab.Calls.Snapshot().Count);
    }

    [Fact]
    public async Task The_read_is_one_git_call_run_as_the_outside_read_runs()
    {
        using var lab = new ConfigLab();
        File.WriteAllText(lab.Combine("rules.gitconfig"), "[url \"http://bob@example.invalid/\"]\n\tinsteadOf = http://example.invalid/\n");
        lab.WriteGlobal("[includeIf \"gitdir:/nowhere/\"]\n\tpath = rules.gitconfig\n");
        var directive = Assert.Single((await lab.Configuration.ReadAsync(null, TestCancelled)).Values, GitConfigIncludes.IsConditional);
        var commands = new List<GitCommand>();
        using var configuration = new GitConfiguration(new CapturingRunner(lab.Runner, commands), lab.DataDirectory, lab.MergeToolsFile, lab.Environment);

        await configuration.ReadIncludedAsync(directive, TestCancelled);

        var command = Assert.Single(commands);
        Assert.Equal(
            ["config", "--file", lab.Combine("rules.gitconfig"), "--includes", "--show-scope", "--show-origin", "-z", "--get-regexp", @"^(url|includeif)\."],
            command.Arguments);
        Assert.Equal("0", command.Environment["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal((Path.GetFullPath(lab.DataDirectory), lab.Folder), (command.WorkingDirectory, command.Environment[GitIsolation.CeilingVariable]));
    }
}
